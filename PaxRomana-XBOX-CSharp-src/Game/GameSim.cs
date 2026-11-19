// PAX ROMANA, Level Zero: The Keys. Bavaria, summer 1944. An agent of the Research and Analysis Wing takes the keys
// to the U-235 enrichment zone from the vault of a garrisoned castle.
// World holds everything that is built once (terrain, castle, enrichment works, trees). GameSim is one mission: the rules,
// input handling and haptics, with no drawing or device access, so it can run headless in tests.
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace PaxRomana
{
    public sealed class World
    {
        public Textures Tex; public List<IndexedMesh> Terrain; public Castle Castle; public Works Works; public Vegetation Veg;
        public Colliders Colliders; public Geo[][] SoldierParts; public Geo Rifle, Bolt;
        public readonly List<Solid> Solids = new List<Solid>();

        public static World Build(Action<float, string> progress, bool textures = true, int grassCount = 40000)
        {
            World w = new World();
            progress(0.03f, "Quarrying castle stone…");
            if (textures) w.Tex = Textures.Make(p => progress(0.03f + p * 0.27f, "Quarrying castle stone…"));
            progress(0.3f, "Growing summer grass along the river…");
            w.Terrain = PaxRomana.Terrain.Build();
            progress(0.5f, "Raising Burg Adlerstein and the enrichment works…");
            w.Castle = Castle.Build();
            w.Works = Works.Build();
            w.Colliders = new Colliders(w.Castle);
            progress(0.65f, "Leafing the birch and pine…");
            w.Veg = Vegetation.Build(grassCount);
            progress(0.85f, "Posting the garrison…");
            w.SoldierParts = Soldier.BuildParts();
            w.Rifle = Weapon.BuildRifle(); w.Bolt = Weapon.BuildBolt();
            w.Solids.AddRange(w.Castle.Solids); w.Solids.AddRange(w.Veg.Obstacles);
            progress(0.95f, "Polishing the lens…");
            return w;
        }
    }

    public sealed class GameSim
    {
        public readonly World World; public readonly Garrison Garrison; public readonly Fx Fx; public readonly IAudio Audio; public readonly Player Player;
        public readonly Capture Capture; public readonly Weapon Weapon; public readonly Colliders Colliders; public readonly Hud Hud; public readonly Pad Pad; public readonly KeyMouse Km;
        public bool Console;                 // running on the console: controller prompts from the start
        public bool Started, Over, Win, RestartRequested;
        public bool AlwaysKillCam;           // every predicted hit gets the kill-cam (used by the visual check)
        public bool ShowIntro = true;        // play the Level Zero intro before the first mission of a session
        public bool Intro;                   // the intro is playing
        public float IntroT;
        int introVoiced = -1;                // the last intro card whose narration was started
        public float Time, GameTime, Clock; public int Alarms;
        float endT = -1, timeScale = 1, range, rangeT, edgeT, spotT, hintDelay = -1, titleT;
        bool ltHeld, rtHeld, spotted;
        // kill-cam
        public bool KcActive; public readonly Camera KillCam = new Camera { Fov = 48, Near = 0.05f };
        Bullet kcBullet; int kcPhase; float kcT, kcCooldown; Predicted kcHit; Vector3 kcLook, kcFrom;
        // what the renderer needs besides the objects above
        public float GradeScope, GradeFlash;
        public Camera ActiveCam { get { return KcActive ? KillCam : Player.Cam; } }
        public Bullet KcBullet { get { return kcBullet; } }
        public bool Paused { get { return Started && !Player.Enabled && !Over; } }
        // true while a mouse player is in the game: the platform hides and captures the cursor
        public bool WantMouseCapture { get { return Player.Enabled && !padMode; } }
        bool padMode;

        const float BLAST = 8.5f;

        public GameSim(World world, IAudio audio, Pad pad, KeyMouse km, Hud hud)
        {
            World = world; Audio = audio; Pad = pad; Km = km; Hud = hud;
            world.Castle.Reset();
            Colliders = world.Colliders;
            Garrison = new Garrison(world.Castle);
            Fx = new Fx();
            for (int i = 0; i < world.Castle.Smoke.Count; i++) Fx.AddSmoke(world.Castle.Smoke[i], i < 2 ? 1 : 1.5f);
            Player = new Player(world.Solids, audio, Garrison.Soldiers, pad, km);
            Capture = new Capture(world.Castle, Player, Garrison, hud, audio, pad);
            Weapon = new Weapon(this, Player);
            Hud.Total = Garrison.Total;

            Garrison.OnAlarm = () =>
            {
                Alarms++; Audio.Horn();
                Hud.Feed("<b>ALARM</b> the siren sounds. The garrison is running.", "warn");
                Pad.Rumble(low: 0.35f, ms: 500, decay: true); Pad.Rumble(low: 0.35f, ms: 500, delay: 700, decay: true);
            };
            // ---- haptics: what the controller feels ----
            Weapon.OnFire = explosive =>
            {
                if (explosive) { Pad.Rumble(rt: 0.9f, low: 1, high: 0.5f, ms: 300, decay: true); return; }
                Pad.Rumble(rt: 1, ms: 70);                                     // the trigger breaks
                Pad.Rumble(low: 0.95f, high: 0.75f, ms: 210, decay: true);     // recoil
                Pad.Rumble(high: 0.28f, ms: 35, delay: 520); Pad.Rumble(high: 0.34f, ms: 35, delay: 930);   // bolt back, bolt home
            };
            Weapon.OnDry = () => Pad.Rumble(rt: 0.3f, ms: 35);
            Player.OnHeartbeat = () => { Pad.Rumble(low: 0.22f, ms: 70); Pad.Rumble(low: 0.15f, ms: 70, delay: 170); };
            Player.OnEdge = () => { if (edgeT <= 0) { edgeT = 6; Hud.Hint("Edge of the valley. The castle lies behind you", 3000); } };
            SetTitleCamera();
        }

        public void OnKill(HitResult r)
        {
            Hud.Hitmarker(r.Head);
            string who = r.Soldier.Officer ? "Officer" : "Soldier";
            Pad.Rumble(high: r.Head ? 0.7f : 0.4f, lt: r.Head ? 0.5f : 0, ms: r.Head ? 110 : 60);
            Hud.Feed(who + " down <em>" + (int)Math.Round(r.Dist) + " m</em>" + (r.Head ? " <b>HEADSHOT</b>" : ""), r.Head ? "head" : "");
        }

        // An explosive charge goes off at p: fireball, blast radius, breached doors, and a rumble that arrives with the sound.
        public void Explode(Vector3 at, float dist, float delay)
        {
            Fx.Explosion(at); Audio.Explosion(delay, dist);
            float k = M.Clamp(1.1f - dist / 420, 0.22f, 1);
            Pad.Rumble(low: k, high: k * 0.7f, lt: k * 0.4f, rt: k * 0.4f, ms: 380 + 520 * k, delay: delay * 1000, decay: true);
            Player.Shake = Math.Max(Player.Shake, M.Clamp(1.6f - dist / 45, 0, 1.6f));
            int kills = 0;
            foreach (Soldier s in Garrison.Soldiers)
            {
                if (!s.Alive) continue;
                Vector3 v = s.Pos; v.Y += 0.9f;
                if (Vector3.Distance(v, at) > BLAST) continue;
                v -= at; v.Y = Math.Abs(v.Y) + 1.5f; v.Normalize();
                Garrison.Kill(s, v, false);
                kills++; Weapon.Hits++; Weapon.Longest = Math.Max(Weapon.Longest, dist);
            }
            Garrison.AlertAround(at, 70, 25, 40);
            Capture.Blast(at);
            if (kills > 0) { Hud.Hitmarker(false); Hud.Feed("Blast <em>" + (int)Math.Round(dist) + " m</em> <b>" + kills + " DOWN</b>", "head"); }
        }

        void Reload()
        {
            bool busy = Weapon.ReloadT > 0;
            Weapon.Reload();
            if (!busy && Weapon.ReloadT > 0) foreach (float d in new[] { 250f, 1050, 1850, 2450 }) Pad.Rumble(high: 0.3f, lt: 0.15f, ms: 40, delay: d);
        }

        // Laser rangefinder: distance along the line of sight to the ground, the masonry or a soldier.
        float Measure()
        {
            Camera cam = Player.Cam; Vector3 dir = cam.Forward, o = cam.Pos;
            float best = 0;
            for (float d = 4; d < 1800; d += 4)
            {
                Vector3 v = o + dir * d;
                if (v.Y < Math.Max(0, Terrain.GroundAt(v.X, v.Z))) { best = d - 2; break; }
            }
            float hd; Vector3 hn; bool wood;
            if (Colliders.Raycast(o, dir, best > 0 ? best : 1800, out hd, out hn, out wood)) best = hd;
            float reach = best > 0 ? best : 1800;
            Vector3 end = o + dir * reach;
            foreach (Soldier s in Garrison.Soldiers)
            {
                SoldierHit h = s.HitTest(o, end);
                if (h.Hit) { float d = h.T * reach; if (best == 0 || d < best) best = d; }
            }
            return best;
        }

        void StartKillCam(Bullet b, Predicted hit)
        {
            KcActive = true; kcBullet = b; kcPhase = 0; kcT = 0; kcHit = hit; b.Cam = true;
            Hud.Cine = true;
        }
        void EndKillCam()
        {
            KcActive = false; kcCooldown = 1;
            if (kcBullet != null) kcBullet.Cam = false;      // the round flies on at full speed; it is retired once it lands
            kcBullet = null; timeScale = 1; Hud.Cine = false;
        }

        void TryFire()
        {
            Bullet b = Weapon.Fire();
            if (b == null) return;
            if (kcCooldown > 0) return;
            Predicted hit = Weapon.Predict(b);
            if (hit == null) return;
            float dist = Vector3.Distance(hit.Point, Player.Cam.Pos); bool last = Garrison.AliveCount == 1;
            if (dist > 90 && (AlwaysKillCam || last || (hit.Head && M.Rand() < 0.65f) || M.Rand() < 0.22f)) StartKillCam(b, hit);
        }

        // ---- input: controller first, with keyboard and mouse twins ----
        public void Trigger()
        {
            if (KcActive) { if (kcT > 0.4f) EndKillCam(); return; }
            if (Player.Swimming) { Hud.Hint("You cannot shoot while swimming", 1800); return; }
            TryFire();
        }
        public void TriggerExplosive()
        {
            if (KcActive) return;
            if (Player.Swimming) { Hud.Hint("You cannot shoot while swimming", 1800); return; }
            if (Weapon.Charges == 0 && Weapon.Cool <= 0) Hud.Hint("No explosive charges left", 1600);
            Weapon.FireExplosive();
        }
        void UseDoor() { if (!KcActive && Capture.UseDoor()) Pad.Rumble(low: 0.35f, ms: 160, decay: true); }

        // Controller. LT scope, RT fire, RB explosive, LB run (hold breath when scoped), X reload, A use / skip,
        // B crouch, R3 prone, D-pad zoom and zero, L3 sprint, View controls, Menu pause.
        void PadInput()
        {
            if (!Pad.Connected) return;
            bool a = Pad.Pressed(Btn.A), menu = Pad.Pressed(Btn.MENU);
            bool rt = Pad.Value(Btn.RT) > 0.5f, rtEdge = rt && !rtHeld;
            rtHeld = rt;
            if (Over) { if (a || menu) RestartRequested = true; return; }
            if (Intro) { if ((a || menu || rtEdge) && IntroT > 0.5f) EndIntro(); return; }
            if (!Player.Enabled) { if ((a || menu) && Hud.Ready) Begin(true); return; }      // title or pause screen
            if (menu) { SetPlaying(false); return; }
            if (Pad.Pressed(Btn.VIEW)) Hud.ToggleKeys();
            if (KcActive) { if (a || rtEdge) Trigger(); return; }
            float lt = Pad.Value(Btn.LT);
            if (lt > 0.4f && !ltHeld) { ltHeld = true; Weapon.WantAds = true; Pad.Rumble(lt: 0.25f, ms: 30); }
            else if (lt < 0.2f && ltHeld) { ltHeld = false; Weapon.WantAds = false; }
            if (rtEdge) Trigger();
            if (Pad.Pressed(Btn.RB)) TriggerExplosive();
            if (Pad.Pressed(Btn.X)) Reload();
            if (a) UseDoor();
            if (Pad.Pressed(Btn.B)) Player.ToggleStance(1);
            if (Pad.Pressed(Btn.R3)) Player.ToggleStance(2);
            if (Pad.Pressed(Btn.UP)) Weapon.CycleZoom(1);
            if (Pad.Pressed(Btn.DOWN)) Weapon.CycleZoom(-1);
            if (Pad.Pressed(Btn.RIGHT)) Weapon.CycleZero(1);
            if (Pad.Pressed(Btn.LEFT)) Weapon.CycleZero(-1);
            if (Pad.Pressed(Btn.Y)) Weapon.ZoomI = (Weapon.ZoomI + 1) % Weapon.ZOOMS.Length;
        }

        void KeyInput()
        {
            if (Km.AnyInput) Pad.Active = false;
            bool go = Km.Pressed(K.Enter) || Km.Pressed(K.Space) || Km.LeftHit;
            if (Over) { if (go) RestartRequested = true; return; }
            if (Intro) { if ((go || Km.Pressed(K.Escape)) && IntroT > 0.5f) EndIntro(); return; }
            if (!Player.Enabled) { if (go && Hud.Ready) Begin(false); return; }
            if (Km.Pressed(K.Escape)) { SetPlaying(false); return; }
            if (Km.Pressed(K.C)) Player.ToggleStance(1);
            if (Km.Pressed(K.Z)) Player.ToggleStance(2);
            if (Km.Pressed(K.R)) Reload();
            if (Km.Pressed(K.BracketR) || Km.Pressed(K.E)) Weapon.CycleZero(1);
            if (Km.Pressed(K.BracketL) || Km.Pressed(K.Q)) Weapon.CycleZero(-1);
            if (Km.Pressed(K.F) && !KcActive) Weapon.WantAds = !Weapon.WantAds;
            if (Km.RightHit && !KcActive) Weapon.WantAds = true;
            if (Km.RightReleased) Weapon.WantAds = false;
            if (Km.Pressed(K.Space) || Km.Pressed(K.Enter) || Km.Pressed(K.X) || Km.LeftHit) Trigger();
            if (Km.Pressed(K.G) || Km.MiddleHit) TriggerExplosive();
            if (Km.Pressed(K.Plus)) Weapon.CycleZoom(1);
            if (Km.Pressed(K.Minus)) Weapon.CycleZoom(-1);
            if (Km.Wheel != 0 && Weapon.Ads > 0.5f) Weapon.CycleZoom(Km.Wheel > 0 ? 1 : -1);
            if (Km.Pressed(K.H)) Hud.ToggleKeys();
            if (Km.Pressed(K.O)) UseDoor();
        }

        // Aim friction: the right stick slows while the reticle is on or beside a defender, which makes a
        // 400 m shot with a thumbstick fair.
        void AimFriction()
        {
            float slow = 1;
            if (Pad.Active)
            {
                Vector3 dir = Player.Cam.Forward, o = Player.Cam.Pos;
                foreach (Soldier s in Garrison.Soldiers)
                {
                    if (!s.Alive) continue;
                    Vector3 v = s.Pos; v.Y += 1.1f; v -= o;
                    float along = Vector3.Dot(v, dir);
                    if (along < 3) continue;
                    float off = M.Sqrt(Math.Max(0, v.LengthSquared() - along * along));
                    if (off < 0.5f) { slow = 0.4f; break; }
                    if (off < 1.1f) slow = Math.Min(slow, 0.62f);
                }
            }
            Player.AimSlow = slow;
        }

        // Sentries notice a rifleman who walks up to them: closer if you crouch or crawl, and only with a clear line past the masonry.
        void Watch()
        {
            float R = (Player.Stance == 0 ? 45 : Player.Stance == 1 ? 28 : 13) * (Player.Swimming ? 0.5f : 1);
            bool seen = false;
            Vector3 eye = Player.Cam.Pos;
            foreach (Soldier s in Garrison.Soldiers)
            {
                if (!s.Alive) continue;
                Vector3 head = s.Pos; head.Y += 1.67f;
                float d = Vector3.Distance(head, eye);
                if (d > R) continue;
                if (!Colliders.Blocked(head, eye, 0.3f)) { seen = true; break; }
            }
            if (seen)
            {
                Garrison.AlertAround(Player.Pos, R + 25, 12, 14);
                if (!spotted) { Hud.Feed("<b>SPOTTED</b> a sentry has seen you", "warn"); Pad.Rumble(low: 0.4f, high: 0.2f, ms: 180, decay: true); }
            }
            spotted = seen;
        }

        // Playing or paused.
        public void SetPlaying(bool on)
        {
            if (Over) return;
            Player.Enabled = on;
            Hud.ShowMenu(!on, Started);
            if (!on) { Weapon.WantAds = false; ltHeld = false; Pad.Stop(); }
        }

        public void Begin(bool viaPad)
        {
            padMode = viaPad;
            if (!Started && ShowIntro && !Intro) { StartIntro(); return; }
            SetPlaying(true);
            if (!Started) { Started = true; hintDelay = 1.2f; }
        }

        // ---- the Level Zero intro: title and briefing cards over a flight across the valley ----
        void StartIntro()
        {
            Intro = true; IntroT = 0; ShowIntro = false; introVoiced = -1;
            Hud.ShowMenu(false, false); Hud.Cine = true; Hud.IntroOn = true; Hud.IntroT = 0;
        }
        public void EndIntro()
        {
            if (!Intro) return;
            Intro = false; Hud.Cine = false; Hud.IntroOn = false;
            Audio.StopVoice();
            Begin(padMode);
        }

        // Camera shots of the intro, one per card: where the camera starts and what it looks at, then where it ends
        // and what it looks at. Each shot lasts as long as its card (IntroScript.Cuts).
        static readonly float[][] IntroShots =
        {
            //      from x, y, z          looks at            to x, y, z            looks at
            new[] { 150f, 96, 470,        0, 44, 0,           110, 78, 300,         0, 46, 0 },                                             // from behind the ridge, the castle across the valley
            new[] { -60f, 120, 110,       Config.WORKS_X, 30, Config.WORKS_Z,       120, 105, -160,   Config.WORKS_X, 34, Config.WORKS_Z },   // over the castle towards the works
            new[] { 72f, 74, 84,          0, 50, -8,          -68, 70, 78,          0, 48, -8 },                                            // round the keep
            new[] { Config.SPAWN_X - 6, 100, Config.SPAWN_Z + 40,   0, 40, 0,       Config.SPAWN_X, 79, Config.SPAWN_Z + 2,   0, 40, 0 },    // down to the nest, between its trees
        };

        void SetIntroCamera()
        {
            float[] cuts = IntroScript.Cuts;
            int i = 0;
            while (i < IntroShots.Length - 1 && IntroT >= cuts[i + 1]) i++;
            float[] s = IntroShots[i];
            float k = M.Smoothstep(0, 1, (IntroT - cuts[i]) / (cuts[i + 1] - cuts[i]));
            Vector3 pos = Vector3.Lerp(new Vector3(s[0], s[1], s[2]), new Vector3(s[6], s[7], s[8]), k);
            Vector3 look = Vector3.Lerp(new Vector3(s[3], s[4], s[5]), new Vector3(s[9], s[10], s[11]), k);
            pos.Y = Math.Max(pos.Y, Terrain.GroundAt(pos.X, pos.Z) + 3);
            Player.Cam.LookAt(pos, look);
            Player.Cam.Fov = 52; Player.Cam.Near = 0.5f;
        }

        void SetTitleCamera()
        {
            float a = titleT * 0.03f;
            Vector3 pos = new Vector3(Config.SPAWN_X + M.Sin(a) * 40, Terrain.GroundAt(Config.SPAWN_X, Config.SPAWN_Z) + 9, Config.SPAWN_Z + 14);
            Player.Cam.LookAt(pos, new Vector3(0, 44, 0));
            Player.Cam.Fov = Player.BASE_FOV; Player.Cam.Near = 0.25f;
        }

        // One frame of the game. The platform has already filled Pad and Km for this frame.
        public void Frame(float dtReal)
        {
            PadInput(); Pad.Update(dtReal);
            KeyInput();
            Hud.IsPad = Pad.Active || Console;
            bool paused = Paused;

            // kill-cam time dilation
            if (KcActive)
            {
                kcT += dtReal;
                Bullet b = kcBullet; Vector3 up = Vector3.Up;
                if (kcPhase == 0)
                {
                    float d = Vector3.Distance(b.Pos, kcHit.Point);
                    timeScale = d > 28 ? 0.1f : 0.018f;
                    if (b.Done) { kcPhase = 1; kcT = 0; kcFrom = KillCam.Pos; kcLook = b.Pos; }
                    else
                    {
                        Vector3 v1 = Vector3.Normalize(b.Vel), v2 = Vector3.Normalize(Vector3.Cross(v1, up));
                        float orbit = kcT * 0.35f + 0.5f;
                        Vector3 pos = b.Pos - v1 * 1.25f + v2 * (M.Sin(orbit) * 0.42f) + up * (0.1f + M.Cos(orbit) * 0.12f);
                        KillCam.LookAt(pos, b.Pos + v1 * 4);
                    }
                }
                else
                {
                    timeScale = 0.22f;
                    Vector3 v1 = kcFrom - kcLook;
                    if (v1.LengthSquared() < 1e-6f) v1 = Vector3.Backward;
                    v1.Normalize();
                    Vector3 v2 = Vector3.Cross(v1, up);
                    if (v2.LengthSquared() < 1e-6f) v2 = Vector3.Right;
                    v2.Normalize();
                    Vector3 pos = kcFrom + v2 * (kcT * 0.5f) - v1 * (kcT * 0.35f);
                    pos.Y += kcT * 0.12f;
                    KillCam.LookAt(pos, new Vector3(kcLook.X, kcLook.Y - 0.3f, kcLook.Z));
                    if (kcT > 2.1f) EndKillCam();
                }
            }
            kcCooldown = Math.Max(0, kcCooldown - dtReal);

            float dt = paused ? 0 : dtReal * timeScale;
            GameTime += dt;
            Clock += paused ? 0 : dtReal * Math.Max(timeScale, 0.25f);

            if (!paused)
            {
                if (!KcActive && Started) { AimFriction(); Player.Aiming = Weapon.WantAds; Player.Update(dtReal); }
                if (Player.Swimming) Weapon.WantAds = false;
                if (!KcActive && Started) Capture.Update(dtReal);
                if (Capture.Done && endT < 0 && !Over) { endT = 3; Win = true; }
                World.Veg.Grass.Update(Player.Pos.X, Player.Pos.Z);
                edgeT -= dtReal; spotT -= dt;
                if (spotT <= 0 && Started && !Over && !KcActive) { spotT = 0.5f; Watch(); }
                Weapon.Update(dtReal, dt, !KcActive && Started);
                Garrison.Update(dt, GameTime);
                if (Started && !Over) Time += dt;
            }
            Camera cam = ActiveCam;
            Fx.Update(dt, cam.Pos, cam.Fov);

            // rangefinder
            bool scoped = Weapon.Scoped && !KcActive;
            rangeT -= dtReal;
            if (scoped && rangeT <= 0) { rangeT = 0.12f; range = Measure(); }

            // HUD
            if (Started)
            {
                Hud.Kills = Garrison.Total - Garrison.AliveCount; Hud.Total = Garrison.Total;
                Hud.Alert = Garrison.AlertLevel; Hud.Alarm = Garrison.Alarm;
                Hud.Mag = Weapon.Mag; Hud.Reserve = Weapon.Reserve; Hud.Charges = Weapon.Charges;
                Hud.Stance = Player.Stance;
                Hud.Status = Weapon.ReloadT > 0 ? "RELOADING" : Weapon.Mag == 0 ? (Weapon.Reserve > 0 ? (Hud.IsPad ? "PRESS X TO RELOAD" : "PRESS R TO RELOAD") : "NO AMMUNITION") : "";
                Hud.Crosshair = Weapon.Ads < 0.3f && !KcActive;
                Hud.ScopeOn = scoped;
                if (scoped)
                {
                    float cross = Vector3.Dot(Config.WIND, Player.Cam.Right);
                    Hud.Wind = (cross > 0.15f ? "> " : cross < -0.15f ? "< " : "· ") + (Config.WIND.Length() * 5).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " m/s";
                    Hud.Range = range; Hud.Zero = Weapon.Zero; Hud.Zoom = Weapon.Zoom; Hud.Breath = Player.Breath; Hud.Exhausted = Player.Exhausted > 0;
                }
                if (hintDelay > 0)
                {
                    hintDelay -= dtReal;
                    if (hintDelay <= 0)
                        Hud.Hint(Hud.IsPad ? "LT scope · RT fire · RB explosive · View shows all controls" : "Right mouse scope · Left mouse fire · G explosive · H shows all controls", 9000);
                }
                if (endT > 0 && !KcActive)
                {
                    endT -= dtReal;
                    if (endT <= 0)
                    {
                        Over = true; Player.Enabled = false; Pad.Stop();
                        Hud.End(Win, new EndStats
                        {
                            Kills = Garrison.Total - Garrison.AliveCount, Total = Garrison.Total, Shots = Weapon.Shots, Hits = Weapon.Hits, Heads = Weapon.Heads,
                            Longest = Weapon.Longest, Time = Time, Alarms = Alarms, Charges = 8 - Weapon.Charges,
                        });
                    }
                }
            }
            else
            {
                if (Intro)
                {
                    IntroT += dtReal; Hud.IntroT = IntroT;
                    if (IntroT >= IntroScript.Length) EndIntro();
                    else
                    {
                        SetIntroCamera();
                        // the narration for a card starts a moment after the card appears
                        int card = IntroScript.IndexAt(IntroT - IntroScript.VoiceDelay);
                        if (card >= 0 && card != introVoiced) { introVoiced = card; Audio.Voice(card); }
                    }
                }
                else
                {
                    // title screen: slow establishing drift over the valley
                    titleT += dtReal;
                    SetTitleCamera();
                }
            }

            GradeScope = KcActive ? 0 : M.Smoothstep(0.7f, 0.9f, Weapon.Ads);
            GradeFlash = Weapon.ScopeFlash * (Weapon.Scoped ? 0.5f : 0.12f);
            Hud.Update(dtReal);
        }
    }
}
