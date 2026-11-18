// Gauss rifle: first-person view model, scope, ammunition, and real projectile ballistics (muzzle velocity,
// gravity drop, wind drift, time of flight). Fires sniper rounds and, from the under-barrel tube, explosive charges
// that fly the same trajectory and detonate on impact.
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace PaxRomana
{
    public sealed class HitResult
    {
        public string Type;               // soldier, stone, wood, dirt, water, none
        public Soldier Soldier; public bool Head; public Vector3 Normal, Point; public float Dist;
    }

    public sealed class Bullet
    {
        public Vector3 Pos, Vel, Origin; public float T; public bool Done, Cam, Explosive; public HitResult Result;
    }

    public sealed class Predicted { public Soldier Soldier; public bool Head; public Vector3 Point; public float T; }

    public sealed class Weapon
    {
        static readonly Vector3 HIP = new Vector3(0.17f, -0.18f, -0.42f), ADS = new Vector3(0, -0.0635f, -0.2f);
        public static readonly int[] ZOOMS = { 4, 6, 8, 12, 16 };
        static readonly int[] ZEROS = { 100, 200, 300, 400, 500, 600 };

        readonly GameSim W; readonly Player player;
        public float Ads, Cool, ReloadT, Recoil, FlashT, ScopeFlash, LastCool = 1.25f;
        public bool WantAds; public int ZoomI = 2, ZeroI = 3, Mag = 5, Reserve = 40, Charges = 8;
        public readonly List<Bullet> Bullets = new List<Bullet>();
        public int Shots, Hits, Heads; public float Longest;
        public Action<bool> OnFire; public Action OnDry;
        // view model pose, for the renderer
        public bool ModelVisible, FlashVisible; public Matrix ModelLocal = Matrix.Identity, BoltLocal = Matrix.Identity; public float FlashRoll;

        public Weapon(GameSim world, Player player) { W = world; this.player = player; }

        public int Zoom { get { return ZOOMS[ZoomI]; } }
        public int Zero { get { return ZEROS[ZeroI]; } }
        public bool Scoped { get { return Ads > 0.85f; } }
        public void CycleZoom(int d) { ZoomI = M.Clamp(ZoomI + d, 0, ZOOMS.Length - 1); }
        public void CycleZero(int d) { ZeroI = M.Clamp(ZeroI + d, 0, ZEROS.Length - 1); }

        public void Reload()
        {
            if (ReloadT > 0 || Mag == 5 || Reserve == 0) return;
            ReloadT = 2.6f; WantAds = false; W.Audio.Reload();
        }

        // Fires an explosive charge. Returns the projectile, or null if it could not fire.
        public Bullet FireExplosive()
        {
            if (Cool > 0 || ReloadT > 0) return null;
            if (Charges == 0) { W.Audio.Dry(); Cool = 0.3f; if (OnDry != null) OnDry(); return null; }
            Charges--;
            return Launch(true);
        }

        // Returns the bullet if a round was fired.
        public Bullet Fire()
        {
            if (Cool > 0 || ReloadT > 0) return null;
            if (Mag == 0) { W.Audio.Dry(); Cool = 0.3f; if (OnDry != null) OnDry(); if (Reserve > 0) Reload(); return null; }
            Mag--;
            return Launch(false);
        }

        Bullet Launch(bool explosive)
        {
            Cool = explosive ? 1.5f : 1.25f; Shots++; LastCool = Cool;
            Camera cam = player.Cam;
            Vector3 dir = cam.Forward, up = cam.Up, right = cam.Right;
            if (!Scoped) { float a = M.Rand() * M.TAU, s = M.Rand() * 0.022f; dir = Vector3.Normalize(dir + right * (M.Cos(a) * s) + up * (M.Sin(a) * s)); }
            // sight zero: launch slightly high so the round crosses the line of sight at the zero range
            float tz = Zero / Config.MUZZLE_V, elev = M.Atan2(0.5f * Config.GRAVITY * tz * tz, Zero);
            dir = Vector3.Normalize(dir * M.Cos(elev) + up * M.Sin(elev));
            Bullet b = new Bullet { Pos = cam.Pos + dir * 0.6f, Vel = dir * Config.MUZZLE_V, Origin = cam.Pos, Explosive = explosive };
            Bullets.Add(b);
            Recoil = explosive ? 1.5f : 1; FlashT = 0.06f; ScopeFlash = 1;
            player.Kick += (Scoped ? 0.012f : 0.05f) * (explosive ? 1.8f : 1); player.KickYaw += (M.Rand() - 0.5f) * 0.008f;
            if (explosive) W.Audio.Launch(); else { W.Audio.Shot(); W.Audio.Bolt(0.45f); }
            W.Legion.AlertAround(cam.Pos, 0, 0, explosive ? 6 : 3);
            if (OnFire != null) OnFire(explosive);
            return b;
        }

        // Dry-run the trajectory against (velocity-led) soldiers and the masonry to decide on a kill-cam.
        public Predicted Predict(Bullet b)
        {
            Vector3 p = b.Pos, v = b.Vel, q;
            const float dt = 1f / 200;
            Predicted hit = null;
            for (float t = 0; t < 1.6f && hit == null; t += dt)
            {
                q = p;
                v.Y -= Config.GRAVITY * dt; v += Config.WIND * dt;
                p += v * dt;
                if (p.Y < Terrain.GroundAt(p.X, p.Z)) return null;
                foreach (Soldier s in W.Legion.Soldiers)
                {
                    if (!s.Alive || Math.Abs(s.Pos.X - p.X) + Math.Abs(s.Pos.Z - p.Z) > 12) continue;
                    SoldierHit h = s.HitTest(q, p, t);
                    if (h.Hit) { hit = new Predicted { Soldier = s, Head = h.Head, Point = Vector3.Lerp(q, p, h.T), T = t }; break; }
                }
            }
            if (hit == null) return null;
            // masonry in the way?
            const int steps = 5;
            Vector3 cur = b.Pos, vv = b.Vel, a0 = b.Pos; float st = hit.T / steps;
            for (int i = 0; i < steps; i++)
            {
                vv.Y -= Config.GRAVITY * st; cur += vv * st;
                if (W.Colliders.Blocked(a0, cur, i == steps - 1 ? 0.6f : 0)) return null;
                a0 = cur;
            }
            return hit;
        }

        void Step(Bullet b, float dt)
        {
            Vector3 p0 = b.Pos;
            b.Vel.Y -= Config.GRAVITY * dt; b.Vel += Config.WIND * dt;
            Vector3 p1 = b.Pos + b.Vel * dt;
            Vector3 d = p1 - p0; float len = d.Length(); d /= len;
            float best = 2; HitResult res = null;
            // soldiers
            foreach (Soldier s in W.Legion.Soldiers)
            {
                if (!s.Alive || Math.Abs(s.Pos.X - p0.X) + Math.Abs(s.Pos.Z - p0.Z) > len + 6) continue;
                SoldierHit h = s.HitTest(p0, p1);
                if (h.Hit && h.T < best) { best = h.T; res = new HitResult { Type = "soldier", Soldier = s, Head = h.Head }; }
            }
            // masonry and timber
            float hd; Vector3 hn; bool wood;
            if (W.Colliders.Raycast(p0, d, len, out hd, out hn, out wood) && hd / len < best)
            {
                best = hd / len;
                res = new HitResult { Type = wood ? "wood" : "stone", Normal = hn };
            }
            // ground and water
            int steps = Math.Max(1, (int)Math.Ceiling(len / 2));
            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps; if (t >= best) break;
                Vector3 v = p0 + d * (len * t);
                float gy = Terrain.GroundAt(v.X, v.Z);
                if (v.Y < Math.Max(gy, 0)) { best = Math.Max(0, t - 0.5f / steps); res = new HitResult { Type = gy < 0 ? "water" : "dirt", Normal = Vector3.Up }; break; }
            }
            b.T += dt;
            if (res == null) { b.Pos = p1; if (b.T > 4) { b.Done = true; b.Result = new HitResult { Type = "none" }; } return; }
            b.Pos = p0 + d * (len * best);
            b.Done = true; b.Result = res; res.Point = b.Pos;
            float dist = Vector3.Distance(b.Pos, b.Origin), delay = b.Cam ? 0 : dist / 343;
            res.Dist = dist;
            if (b.Explosive) { W.Explode(b.Pos, dist, delay); return; }
            if (res.Type == "soldier")
            {
                Vector3 dir = Vector3.Normalize(b.Vel);
                W.Fx.Blood(b.Pos, dir);
                W.Legion.Kill(res.Soldier, dir, res.Head);
                Hits++; if (res.Head) Heads++; Longest = Math.Max(Longest, dist);
                W.Audio.Hit(res.Head); W.Audio.Impact("flesh", delay, dist);
                W.OnKill(res);
            }
            else
            {
                if (res.Type == "water") W.Fx.Splash(b.Pos); else W.Fx.Dust(b.Pos, res.Normal, res.Type);
                W.Audio.Impact(res.Type == "stone" ? "stone" : "dirt", delay, dist);
                W.Legion.AlertAround(b.Pos, 9, 9, 7);
            }
        }

        // dtReal drives the view model; dt (time-scaled) drives projectiles.
        public void Update(float dtReal, float dt, bool visible)
        {
            Player P = player;
            for (int i = 0; i < Bullets.Count; i++)
            {
                Bullet b = Bullets[i];
                if (b.Done) continue;
                float rem = dt;
                while (rem > 1e-6f && !b.Done) { float h = Math.Min(rem, 1f / 120); Step(b, h); rem -= h; }
            }
            Bullets.RemoveAll(b => b.Done && !b.Cam);

            Cool = Math.Max(0, Cool - dtReal);
            if (ReloadT > 0)
            {
                ReloadT -= dtReal;
                if (ReloadT <= 0) { int n = Math.Min(5 - Mag, Reserve); Mag += n; Reserve -= n; }
            }
            float want = WantAds && ReloadT <= 0 && !P.Sprinting ? 1 : 0;
            Ads += (want - Ads) * Math.Min(1, dtReal * 11);
            if (Math.Abs(want - Ads) < 0.003f) Ads = want;
            P.Scoped = Scoped; P.Zoom = Zoom;

            float sFov = M.Deg(2 * (float)Math.Atan(Math.Tan(M.Rad(Player.BASE_FOV / 2)) / Zoom));
            float k = M.Smoothstep(0.55f, 0.9f, Ads);
            P.Cam.Fov = M.Lerp(Player.BASE_FOV, sFov, k);
            P.Cam.Near = Scoped ? 1.4f : 0.25f;

            // view model
            Recoil *= M.Exp(-dtReal * 7);
            ScopeFlash *= M.Exp(-dtReal * 9);
            float t = P.T;
            ModelVisible = visible && Ads < 0.8f;
            Vector3 mp = Vector3.Lerp(HIP, ADS, Ads);
            mp.X += M.Cos(P.Bob) * 0.006f * P.MoveAmt * (1 - Ads) + M.Sin(t * 0.8f) * 0.0015f;
            mp.Y += M.Sin(P.Bob * 2) * 0.006f * P.MoveAmt * (1 - Ads) + M.Sin(t * 1.1f) * 0.002f - (P.Sprinting ? 0.05f : 0);
            mp.Z += Recoil * 0.07f;
            float rx = Recoil * 0.09f - (P.Sprinting ? 0.35f : 0), ry = (P.Sprinting ? 0.5f : 0) + (1 - Ads) * 0.02f, rz = 0;
            if (ReloadT > 0) { float u = M.Sin(Math.Min(1, (2.6f - ReloadT) / 2.6f) * M.PI); rx -= u * 0.6f; rz = u * 0.5f; mp.Y -= u * 0.06f; }
            ModelLocal = Matrix.CreateRotationZ(rz) * Matrix.CreateRotationY(ry) * Matrix.CreateRotationX(rx) * Matrix.CreateTranslation(mp);
            // bolt cycle
            float c = LastCool - Cool, lift = 0, back = 0;
            if (Cool > 0 && c > 0.4f && c < 1.1f)
            {
                float u = (c - 0.4f) / 0.7f;
                lift = M.Sin(Math.Min(1, u * 3) * M.HALF_PI) * (u < 0.75f ? 1 : (1 - u) * 4);
                back = M.Sin(M.Clamp((u - 0.15f) / 0.7f) * M.PI);
            }
            BoltLocal = Matrix.CreateRotationZ(lift * 1.1f) * Matrix.CreateTranslation(0, 0.014f, 0.07f + back * 0.07f);
            FlashT -= dtReal;
            FlashVisible = FlashT > 0;
            if (FlashVisible) FlashRoll = M.Rand() * 6;
        }

        // ---- view model geometry: the rifle and the sniper's arms, vertex coloured; the bolt is a separate piece ----
        static Geo Zc(float r0, float r1, float len, float z, float y = 0, int seg = 16)
        {
            return Geo.Cylinder(r1, r0, len, seg).RotateX(-M.HALF_PI).Translate(0, y, z - len / 2);
        }

        public static Geo BuildRifle()
        {
            const int CARBON = 0x4a515b, STEEL = 0x2b2e34, BLACK = 0x17181b, CLOTH = 0x8f969f, GLOVE = 0x1b1d21, LENS = 0x2a4f88;
            Geo g = new Geo();
            // composite stock: fore-end, wrist and butt
            g.Add(Geo.Box(0.036f, 0.043f, 0.45f).Translate(0, -0.0255f, -0.145f).Paint(CARBON));
            g.Add(Geo.Box(0.034f, 0.085f, 0.1f).RotateX(-0.35f).Translate(0, -0.052f, 0.118f).Paint(CARBON));
            g.Add(Geo.Box(0.036f, 0.105f, 0.3f).RotateX(-0.09f).Translate(0, -0.048f, 0.295f).Paint(CARBON));
            g.Add(Zc(0.018f, 0.018f, 0.24f, 0.1f, 0.014f).Paint(STEEL));            // receiver
            g.Add(Zc(0.012f, 0.0095f, 0.66f, -0.14f, 0.014f).Paint(STEEL));         // barrel
            g.Add(Zc(0.0125f, 0.0125f, 0.03f, -0.77f, 0.014f).Paint(BLACK));        // crown
            g.Add(Zc(0.017f, 0.017f, 0.26f, -0.2f, -0.022f).Paint(BLACK));          // under-barrel charge tube
            g.Add(Zc(0.019f, 0.019f, 0.02f, -0.455f, -0.022f).Paint(STEEL));
            g.Add(Geo.Box(0.03f, 0.04f, 0.07f).Translate(0, -0.06f, -0.01f).Paint(BLACK));     // magazine
            g.Add(Geo.Torus(0.024f, 0.003f, 6, 14, M.PI).RotateY(M.HALF_PI).RotateX(M.PI).Translate(0, -0.05f, 0.075f).Paint(STEEL));
            // scope
            const float sy = 0.0635f;
            g.Add(Zc(0.0145f, 0.0145f, 0.3f, 0.1f, sy).Paint(BLACK));
            g.Add(Zc(0.0145f, 0.026f, 0.05f, -0.2f, sy).Paint(BLACK)); g.Add(Zc(0.026f, 0.026f, 0.07f, -0.25f, sy).Paint(BLACK));
            g.Add(Zc(0.021f, 0.0145f, 0.03f, 0.13f, sy).Paint(BLACK)); g.Add(Zc(0.021f, 0.021f, 0.05f, 0.18f, sy).Paint(BLACK));
            g.Add(Geo.Cylinder(0.011f, 0.011f, 0.028f, 12).Translate(0, sy + 0.02f, -0.05f).Paint(BLACK));
            g.Add(Geo.Cylinder(0.011f, 0.011f, 0.028f, 12).RotateZ(M.HALF_PI).Translate(0.02f, sy, -0.05f).Paint(BLACK));
            g.Add(Geo.Box(0.022f, 0.034f, 0.02f).Translate(0, 0.04f, 0.02f).Paint(STEEL)); g.Add(Geo.Box(0.022f, 0.034f, 0.02f).Translate(0, 0.04f, -0.12f).Paint(STEEL));
            g.Add(Geo.Circle(0.0245f, 20).RotateY(M.PI).Translate(0, sy, -0.318f).Paint(LENS));
            // arms
            g.Add(Geo.Capsule(new Vector3(-0.2f, -0.26f, 0.22f), new Vector3(-0.02f, -0.07f, -0.16f), 0.04f).Paint(CLOTH));
            g.Add(Geo.Sphere(0.042f, 12, 10).Scale(1.1f, 0.8f, 1.5f).Translate(-0.012f, -0.055f, -0.19f).Paint(GLOVE));
            g.Add(Geo.Capsule(new Vector3(0.16f, -0.3f, 0.42f), new Vector3(0.03f, -0.1f, 0.09f), 0.042f).Paint(CLOTH));
            g.Add(Geo.Sphere(0.04f, 12, 10).Scale(1, 1.25f, 1.1f).Translate(0.012f, -0.075f, 0.07f).Paint(GLOVE));
            return g;
        }

        public static Geo BuildBolt()
        {
            Geo g = new Geo();
            g.Add(Geo.Cylinder(0.0045f, 0.0045f, 0.05f, 8).RotateZ(M.HALF_PI).Translate(0.035f, 0, 0).Paint(0x2b2e34));
            g.Add(Geo.Sphere(0.011f, 10, 8).Translate(0.062f, 0, 0).Paint(0x17181b));
            return g;
        }
    }
}
