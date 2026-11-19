// Headless checks of the game rules: no window, no audio, no controller. Drives the same GameSim the game runs,
// through the same controller and keyboard state the platform layer fills in.
//
//     cd tests && dotnet run -c Release
using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;

namespace PaxRomana
{
    static class Tests
    {
        static int failures;
        const float DT = 1f / 60;

        static void Check(bool ok, string what)
        {
            Console.WriteLine((ok ? "  ok    " : "  FAIL  ") + what);
            if (!ok) failures++;
        }

        static GameSim sim; static float[] axes = new float[4], btn = new float[16];

        static void Tick(int n = 1)
        {
            for (int i = 0; i < n; i++) { sim.Pad.SetState(true, axes, btn); sim.Frame(DT); Array.Clear(sim.Km.Hit, 0, sim.Km.Hit.Length); }
        }
        static void Release() { Array.Clear(axes, 0, 4); Array.Clear(btn, 0, 16); }
        static void Press(int b) { btn[b] = 1; Tick(); btn[b] = 0; Tick(); }

        static void AimAt(Vector3 target)
        {
            Vector3 eye = sim.Player.Pos + new Vector3(0, 1.7f, 0), d = target - eye;
            sim.Player.Yaw = M.Atan2(-d.X, -d.Z);
            sim.Player.Pitch = M.Atan2(d.Y, M.Hypot(d.X, d.Z));
        }

        static void Go(float x, float z, string label, float maxT = 30)
        {
            float t = 0;
            while (t < maxT && M.Hypot(x - sim.Player.Pos.X, z - sim.Player.Pos.Z) > 0.4f)
            {
                sim.Player.Yaw = M.Atan2(-(x - sim.Player.Pos.X), -(z - sim.Player.Pos.Z));
                sim.Km.Down[(int)K.W] = true; Tick(); t += DT;
            }
            sim.Km.Down[(int)K.W] = false;
            float d = M.Hypot(x - sim.Player.Pos.X, z - sim.Player.Pos.Z);
            Check(d < 0.6f, label + " -> (" + sim.Player.Pos.X.ToString("0.0") + ", " + sim.Player.Pos.Z.ToString("0.0") + ")");
        }

        static string Prompt { get { return "[" + sim.Hud.PromptKey + "] " + sim.Hud.PromptText; } }

        static void Shoot(Vector3 target)
        {
            AimAt(target); btn[Btn.LT] = 1; Tick(30); AimAt(target); Tick(4);
            btn[Btn.RT] = 1; Tick(); btn[Btn.RT] = 0; Tick(80);
        }

        // A whole run of Level One with the controller: the four interlocks, the hall, the device, the way out.
        static void LevelOne(World zero, NullAudio audio)
        {
            const float FY = Facility.FY;
            Stopwatch sw = Stopwatch.StartNew();
            World one = World.BuildLevelOne(zero);
            Facility fac = one.Facility;
            Console.WriteLine("        built in " + sw.ElapsedMilliseconds + " ms: " + one.Colliders.Triangles + " collision triangles, " + one.Solids.Count + " solids, " + fac.Doors.Count + " doors, " + fac.Props.Count + " moving parts");

            // choosing the level from the menu
            sim = new GameSim(zero, audio, new Pad(), new KeyMouse(), new Hud()); sim.Hud.SetReady(); Release();
            Tick(); Press(Btn.RIGHT);
            Check(sim.LevelRequest == -1, "with level choice off, D-pad right on the menu does nothing");
            sim.CanSwitchLevels = true; Tick(); Press(Btn.RIGHT);
            Check(sim.LevelRequest == 1 && sim.Hud.LevelSelect, "with it on (DEBUG_MODE), D-pad right on the menu asks for Level One");

            sim = new GameSim(one, audio, new Pad(), new KeyMouse(), new Hud()); sim.Hud.SetReady(); Release();
            Sabotage sab = (Sabotage)sim.Mission;
            Tick(); Press(Btn.A);
            IntroCard card = one.Intro.At(1.5f);
            Check(sim.Intro && card != null && card.Title == "LEVEL ONE" && card.Sub == "BLITZSCHEISS MOUNTAIN", "A starts the Level One intro: \"" + card.Title + " · " + card.Sub + "\"");
            int voices = audio.Voices; Tick(60); Press(Btn.A);
            Check(sim.Started && !sim.Intro && audio.Voices == voices, "it has no voice-over, and A skips it");
            Check(sim.World.Level == 1 && Math.Abs(sim.Player.Pos.Y - FY) < 0.05f && sim.Mission.Outdoors == 0 && sim.Garrison.Total == 5, "you arrive in the service lock, underground, with " + sim.Garrison.Total + " guards somewhere ahead");

            // the lock door, by hand
            Go(0, 2.3f, "up to the lock door"); Tick(2);
            Check(sim.Hud.PromptText == "Open the lock door", "prompt: " + Prompt);
            Press(Btn.A); Tick(60); Go(0, -4, "through it");
            Check(sab.Stage == 0, "objective: \"" + sim.Hud.ObjLine + "\"");

            // interlock 1: the crate on the plate
            Vector3 stop = sim.Player.Pos; sim.Player.Yaw = 0; sim.Km.Down[(int)K.W] = true; Tick(60 * 6); sim.Km.Down[(int)K.W] = false;
            Check(sim.Player.Pos.Z > -22.6f && fac.Interlock[0].Amt < 0.01f, "the first interlock door is shut and cannot be opened by hand (stopped at z = " + sim.Player.Pos.Z.ToString("0.0") + ")");
            Go(-5.1f, -16.9f, "to the crate"); Tick(2);
            Check(sim.Hud.PromptText == "Pick up the crate", "prompt: " + Prompt);
            Press(Btn.A); Check(sab.Carrying == 0, "A picks it up");
            Go(5, -6.6f, "carry it to the plate"); sim.Player.Yaw = 0; Tick(2);
            Check(sim.Hud.PromptText == "Put the crate down", "prompt: " + Prompt);
            Press(Btn.A); Tick(70);
            Check(sab.Carrying == -1 && fac.Plates[0].Down && fac.Interlock[0].Amt > 0.99f && sab.Solved[0], "put down on the plate, it holds the door open");
            Go(0, -19, "back to the door"); Go(0, -26, "through the first interlock");

            // interlock 2: the beam
            Check(!sab.BeamHome && fac.Interlock[1].Amt < 0.01f && fac.Beams.Count >= 1, "the beam goes astray and the second door is shut");
            Go(2.3f, -31, "to the first mirror"); Tick(2);
            Check(sim.Hud.PromptText == "Turn the mirror", "prompt: " + Prompt);
            Press(Btn.A); Tick(2);
            Check(!sab.BeamHome && fac.Beams.Count == 3, "one mirror turned: the beam reaches the second mirror and goes astray there, " + fac.Beams.Count + " legs");
            Go(2.3f, -41, "to the second mirror"); Press(Btn.A); Tick(70);
            Check(sab.BeamHome && fac.Beams.Count == 3 && fac.Interlock[1].Amt > 0.99f && sab.Solved[1], "both turned: the beam is home in the receiver and the door opens");
            Go(0, -44, "to the door"); Go(0, -50, "through the second interlock");

            // interlock 3: three targets lit at once, with the rifle
            sim.Player.AlwaysSteady = true;
            Shoot(fac.Targets[0].Pos);
            Check(fac.Targets[0].LitT > 0 && !sab.Solved[2] && fac.Interlock[2].Amt < 0.01f, "one target lit is not enough");
            Shoot(fac.Targets[1].Pos); Shoot(fac.Targets[2].Pos);
            Check(sab.Solved[2] && sim.Weapon.Mag == 2, "three rounds, three targets lit together: solved");
            Release(); Tick(70); sim.Player.AlwaysSteady = false;
            Go(0, -76, "to the door"); Go(0, -82, "through the third interlock");

            // interlock 4: the gap and the gates
            sim.Player.Yaw = 0; sim.Km.Down[(int)K.W] = true; int fell = 0;
            while (sim.Hud.HintText.IndexOf("fell") < 0 && fell++ < 60 * 8) Tick();
            sim.Km.Down[(int)K.W] = false; Tick(2);
            Check(sim.Hud.HintText.StartsWith("You fell") && sim.Player.Pos.Z > -90 && Math.Abs(sim.Player.Pos.Y - FY) < 0.1f, "walking into the gap puts you back on the near side: \"" + sim.Hud.HintText + "\"");
            Go(4.9f, -83.2f, "to the second crate"); Press(Btn.A);
            Check(sab.Carrying == 1, "picked up");
            int steps = 0;
            while (sim.Player.Pos.Z > -100 && steps++ < 60 * 12)
            {
                sim.Player.Yaw = M.Atan2(-(-9.5f - sim.Player.Pos.X), -(-85 - sim.Player.Pos.Z));
                sim.Km.Down[(int)K.W] = true; Tick();
            }
            sim.Km.Down[(int)K.W] = false;
            Check(sim.Player.Pos.X > 6 && sim.Player.Pos.Z < -100 && sab.Carrying == 1, "walking into the blue gate puts you out of the orange one across the gap, crate in hand -> (" + sim.Player.Pos.X.ToString("0.0") + ", " + sim.Player.Pos.Z.ToString("0.0") + ")");
            Go(-3.6f, -106, "along the far side to the plate"); sim.Player.Yaw = M.HALF_PI; Tick(2);
            Press(Btn.A); Tick(70);
            Check(fac.Plates[1].Down && fac.Interlock[3].Amt > 0.99f && sab.Solved[3], "crate on the far plate: the fourth interlock opens");
            Go(0, -108, "to the door"); Go(0, -114.5f, "into the centrifuge hall");

            // the hall
            Check(sab.Stage == 4 && sab.Guards == 5, "objective: \"" + sim.Hud.ObjLine + "\"");
            Go(0, -140.8f, "to the cascade controls"); Tick(90);
            Check(sab.Progress == 0 && sim.Hud.CaptureText.StartsWith("Contested"), "the device cannot be planted while guards live: \"" + sim.Hud.CaptureText + "\"");
            foreach (Soldier s in sim.Garrison.Soldiers) s.Kill(Vector3.UnitX, false);
            Tick(60 * 3);
            Check(sab.Progress > 0.45f && sab.Progress < 0.55f && !sab.Planted, "hall clear: half planted after 3 s (" + sim.Hud.CaptureText + ")");
            Tick(60 * 4);
            Check(sab.Planted && fac.Device.Visible && fac.HallExit.Want == 1 && sab.Stage == 7, "device armed after 6 s; the tunnel door opens: \"" + sim.Hud.ObjLine + "\"");

            // the way out
            Go(20.6f, -142, "along the cross aisle to the tunnel door"); Go(27, -142, "into the service tunnel"); Go(50, -142, "up the ramp");
            Check(sim.Player.Pos.Y > FY + 3.5f && sim.Player.Pos.Y < FY + 4.5f, "the ramp climbs: " + (sim.Player.Pos.Y - FY).ToString("0.0") + " m above the hall floor");
            sim.Player.Yaw = -M.HALF_PI; sim.Km.Down[(int)K.W] = true; Tick(60 * 4); sim.Km.Down[(int)K.W] = false;
            Check(sim.Player.Pos.X < 60 && !sab.BlastOpen, "the blast door blocks the tunnel (stopped at x = " + sim.Player.Pos.X.ToString("0.0") + "); hint: \"" + sim.Hud.HintText + "\"");
            sim.Player.AlwaysSteady = true; Shoot(fac.Release.Pos); Release(); sim.Player.AlwaysSteady = false;
            Check(sab.BlastOpen, "a round through its lock opens it");
            Tick(60); Go(70, -142, "through the blast door"); Go(99, -142, "out of the tunnel mouth");
            Check(sim.Mission.Outdoors > 0.5f && sab.Stage == 7 && sim.Hud.PromptText == "", "outside, but not yet clear of the mountain");
            Go(115, -142, "out along the ledge"); Tick(2);
            Check(sab.Stage == 8 && sim.Hud.PromptText == "Detonate", "clear of the mountain. Prompt: " + Prompt);
            int bangs = audio.Explosions;
            Press(Btn.A);
            Check(sab.Done && audio.Explosions == bangs + 1 && sim.Pad.Out[0] > 0.9f, "A detonates: the blast is heard and felt (grips " + sim.Pad.Out[0].ToString("0.00") + ")");
            Tick(60 * 7);
            Check(sim.Over && sim.Win && sim.Hud.EndVisible && sim.Hud.Level == 1, "Level One ends in victory: " + sim.Hud.Stats.Kills + "/" + sim.Hud.Stats.Total + " guards down, " + sim.Hud.Stats.Shots + " shots");
            Press(Btn.A);
            Check(sim.RestartRequested && sim.LevelRequest == -1, "A on its end screen plays Level One again");
            Release();
        }

        static int Main()
        {
            Stopwatch sw = Stopwatch.StartNew();
            World world = World.Build((p, text) => { }, false);
            Console.WriteLine("World built in " + sw.ElapsedMilliseconds + " ms: " + world.Colliders.Triangles + " collision triangles, " + world.Solids.Count + " solids, "
                + world.Terrain.Count + " terrain tiles, " + world.Veg.Near.Count + " + " + world.Veg.Far.Count + " vegetation chunks, " + world.Castle.Doors.Count + " doors");
            Check(world.Colliders.Triangles > 20000, "collision mesh has the castle and the camp in it");
            Check(Math.Abs(Terrain.GroundAt(0, 0) - Config.PLATEAU) < 0.5f, "the castle rock is at " + Terrain.GroundAt(0, 0).ToString("0.0") + " m");

            NullAudio audio = new NullAudio();
            sim = new GameSim(world, audio, new Pad(), new KeyMouse(), new Hud());
            sim.Hud.SetReady();
            for (int i = 0; i < 60; i++) Tick();
            Check(!sim.Started && sim.Hud.MenuVisible, "starts on the title screen");

            Console.WriteLine("1. Intro and controller");
            Press(Btn.A);
            Check(sim.Intro && !sim.Started && !sim.Hud.MenuVisible && sim.Hud.IntroOn && sim.Hud.Cine, "A on the title screen starts the Level Zero intro");
            Tick(120);
            IntroCard card = sim.World.Intro.At(sim.Hud.IntroT);
            Check(card != null && card.Title == "LEVEL ZERO", "two seconds in, the title card reads \"" + (card == null ? "" : card.Title + ": " + card.Sub) + "\"");
            Check(!sim.Player.Enabled && sim.Time == 0, "the mission clock does not run during the intro");
            Check(audio.Voices == 1 && audio.LastVoice == 0, "the narration for the title card has started");
            Press(Btn.A);
            Check(!sim.Intro && !sim.Hud.IntroOn && !sim.Hud.Cine, "A skips the intro");
            Check(audio.VoiceStops >= 1, "skipping stops the narration");
            Check(sim.Started && sim.Player.Enabled && !sim.Hud.MenuVisible && sim.Hud.IsPad, "the mission starts, with controller prompts");
            Vector3 p0 = sim.Player.Pos; float yaw0 = sim.Player.Yaw;
            axes[1] = -1; Tick(60); Release();
            float moved = Vector3.Distance(sim.Player.Pos, p0);
            Check(moved > 2.5f && moved < 4.6f, "one second of left stick forward walks " + moved.ToString("0.00") + " m");
            Vector3 p1 = sim.Player.Pos;
            axes[1] = -1; btn[Btn.LB] = 1; Tick(60); Release();
            float ran = Vector3.Distance(sim.Player.Pos, p1);
            Check(ran > moved * 1.35f && sim.Player.Stance == 0, "holding LB runs: " + ran.ToString("0.00") + " m in a second");
            Press(Btn.B); p1 = sim.Player.Pos;
            axes[1] = -1; btn[Btn.L3] = 1; Tick(); btn[Btn.L3] = 0; Tick(59); Release();
            ran = Vector3.Distance(sim.Player.Pos, p1);
            Check(ran > moved * 1.35f && sim.Player.Stance == 0, "clicking the left stick from a crouch stands up and runs: " + ran.ToString("0.00") + " m in a second");
            axes[1] = -1; btn[Btn.LB] = 1; Tick(20); btn[Btn.LT] = 1; Tick(40);
            Check(sim.Weapon.Scoped && !sim.Player.Sprinting && sim.Player.Holding, "pulling LT while running raises the scope, and LB then holds breath");
            Release(); Tick(30); yaw0 = sim.Player.Yaw;
            axes[2] = 1; Tick(60); Release();
            float turned = M.Deg(yaw0 - sim.Player.Yaw);
            Check(turned > 100 && turned < 260, "one second of right stick turns " + turned.ToString("0") + " degrees to the right");
            Press(Btn.B); Check(sim.Player.Stance == 1, "B crouches"); Press(Btn.B);
            Press(Btn.MENU); Check(sim.Paused && sim.Hud.MenuVisible, "Menu pauses"); Press(Btn.A); Check(!sim.Paused, "A resumes");

            Console.WriteLine("2. Rifle shot and its haptics");
            sim.Player.Pos = new Vector3(Config.SPAWN_X, Terrain.GroundAt(Config.SPAWN_X, Config.SPAWN_Z), Config.SPAWN_Z);
            Soldier guard = sim.Garrison.Soldiers[9];           // a gate guard: stands still until the alarm
            Vector3 chest = guard.Pos + new Vector3(0, 1.25f, 0);
            AimAt(chest);
            sim.Player.AlwaysSteady = true;        // take scope sway out of it: this checks the ballistics, not the marksman
            btn[Btn.LT] = 1; btn[Btn.LB] = 1; Tick(40);
            Check(sim.Weapon.Scoped && sim.Player.Holding, "LT raises the scope, LB holds breath");
            AimAt(chest); Tick(20);
            float range = Vector3.Distance(sim.Player.Cam.Pos, chest);
            sim.Garrison.AlertLevel = 0;
            sim.AlwaysKillCam = true;              // otherwise the kill-cam starts on some shots and not others
            btn[Btn.RT] = 1; Tick();
            Check(sim.Weapon.Mag == 4 && audio.Shots == 1, "RT fires one round");
            Check(sim.Pad.Out[3] > 0.9f && sim.Pad.Out[0] > 0.8f, "the shot kicks the right trigger (" + sim.Pad.Out[3].ToString("0.00") + ") and the grips (" + sim.Pad.Out[0].ToString("0.00") + ")");
            Tick(); Check(sim.Weapon.Mag == 4, "holding RT does not fire again");
            btn[Btn.RT] = 0;
            bool boltTick = false, quiet = false;
            for (int i = 0; i < 90; i++) { Tick(); if (i > 20 && sim.Pad.Out[0] < 0.01f && sim.Pad.Out[1] < 0.01f) quiet = true; if (quiet && sim.Pad.Out[1] > 0.2f && sim.Pad.Out[0] < 0.01f) boltTick = true; }
            Check(boltTick, "the bolt cycling ticks the light motor after the recoil fades");
            Check(sim.KcActive && sim.Hud.Cine && guard.Alive, "the kill-cam follows the round in slow motion");
            Press(Btn.A);
            Check(!sim.KcActive, "A skips the kill-cam");
            Tick(60);
            sim.AlwaysKillCam = false;
            Check(!guard.Alive, "the round kills the gate guard at " + range.ToString("0") + " m (zero " + sim.Weapon.Zero + " m)");
            Check(sim.Weapon.Hits == 1 && sim.Hud.Kills == 1, "the kill is counted");
            Release(); Tick(120);

            Console.WriteLine("3. Explosive charge");
            Vector3 gate = new Vector3(1, Config.PLATEAU + 2.5f, 39.8f);
            Door gateDoor = world.Castle.Doors[0];
            float hd; Vector3 hn; bool wood;
            bool blockedBefore = world.Colliders.Raycast(new Vector3(1, Config.PLATEAU + 1.5f, 50), new Vector3(0, 0, -1), 60, out hd, out hn, out wood);
            Check(blockedBefore && Math.Abs(50 - hd - 39.57f) < 0.3f, "a closed gate stops a bullet at z = " + (50 - hd).ToString("0.0"));
            AimAt(gate); btn[Btn.LT] = 1; btn[Btn.LB] = 1; Tick(40); AimAt(gate); Tick(10);
            int alive0 = sim.Garrison.AliveCount;
            btn[Btn.RB] = 1; Tick(); btn[Btn.RB] = 0;
            Check(sim.Weapon.Charges == 7, "RB fires a charge (7 left)");
            float peak = 0; int frames = 0;
            while (audio.Explosions == 0 && frames++ < 240) Tick();
            Check(audio.Explosions == 1, "the charge detonates after " + (frames * DT).ToString("0.00") + " s of flight");
            for (int i = 0; i < 150; i++) { Tick(); peak = Math.Max(peak, sim.Pad.Out[0]); }
            Check(gateDoor.Broken && gateDoor.Solid.Off, "the blast breaches the castle gate");
            Check(alive0 - sim.Garrison.AliveCount >= 1, "the blast kills " + (alive0 - sim.Garrison.AliveCount) + " defender(s) at the gate");
            Check(peak > 0.15f, "the explosion rumbles when its sound arrives (peak " + peak.ToString("0.00") + ")");
            bool blockedAfter = world.Colliders.Raycast(new Vector3(1, Config.PLATEAU + 1.5f, 50), new Vector3(0, 0, -1), 14, out hd, out hn, out wood);
            Check(!blockedAfter, "after the breach a bullet passes through the gateway");
            Release(); Tick(30);

            Console.WriteLine("4. The keys");
            sim.Player.Pos = new Vector3(0, Config.PLATEAU, 50); Tick(2);
            Check(sim.Capture.Stage == 0, "outside the walls the objective is to breach: \"" + sim.Hud.ObjLine + "\"");
            Go(0, 30, "walk in through the breached gate"); Go(4, 12, "across the courtyard, past the flagpole"); Go(0, 0.3f, "up to the keep");
            Check(sim.Hud.PromptText == "Open the keep door", "prompt at the keep: [" + sim.Hud.PromptKey + "] " + sim.Hud.PromptText);
            Press(Btn.A); Tick(60);
            Go(0, -7.4f, "into the keep, up to the vault wall");
            Check(sim.Hud.PromptText == "Open the vault door", "prompt at the vault: [" + sim.Hud.PromptKey + "] " + sim.Hud.PromptText);
            Press(Btn.A); Tick(60);
            Go(0, -13.6f, "into the vault, up to the strongbox");
            Tick(120);
            Check(sim.Capture.Progress == 0 && sim.Hud.CaptureText.StartsWith("Contested"), "the strongbox cannot be cracked while defenders live: \"" + sim.Hud.CaptureText + "\"");
            int inside = 0; Soldier lastOne = null;
            foreach (Soldier s in sim.Garrison.Soldiers) if (s.Alive && Capture.InWalls(s.Pos.X, s.Pos.Z)) { inside++; lastOne = s; }
            Console.WriteLine("        " + inside + " defenders inside the walls of " + sim.Garrison.Total + " in the garrison");
            foreach (Soldier s in sim.Garrison.Soldiers) if (s.Alive && s != lastOne && Capture.InWalls(s.Pos.X, s.Pos.Z)) s.Kill(Vector3.UnitX, false);
            Tick(2);
            Check(sim.Hud.HintText.StartsWith("Last defender"), "hint for the last one: \"" + sim.Hud.HintText + "\"");
            lastOne.Kill(Vector3.UnitX, true); Tick(2);
            Check(sim.Capture.Defenders == 0 && sim.Capture.Stage == 3, "walls clear: \"" + sim.Hud.ObjLine + "\"");
            Tick(60 * 4);
            Check(sim.Capture.Progress > 0.45f && sim.Capture.Progress < 0.55f, "half way after 4 s (" + sim.Capture.Progress.ToString("0.00") + ")");
            Tick(60 * 5);
            Check(sim.Capture.Done && sim.Hud.ObjLine == "You have the keys to the enrichment zone", "the strongbox opens after 8 s: \"" + sim.Hud.ObjLine + "\"");
            Tick(60 * 4);
            Check(sim.Over && sim.Win && sim.Hud.EndVisible, "Level Zero ends in victory: " + sim.Hud.Stats.Kills + "/" + sim.Hud.Stats.Total + " down, " + sim.Hud.Stats.Charges + " charge used");

            Console.WriteLine("5. New mission and doors");
            Press(Btn.A);
            Check(sim.LevelRequest == 1 && !sim.RestartRequested, "A on the end screen of a won Level Zero goes on to Level One");
            sim = new GameSim(world, audio, new Pad(), new KeyMouse(), new Hud());
            sim.Hud.SetReady();
            Check(!gateDoor.Broken && !gateDoor.Solid.Off && !sim.Capture.Done, "a new mission restores the gate and the strongbox");
            Tick(); Press(Btn.A);
            int introFrames = 0, voicesBefore = audio.Voices; bool sawOrders = false;
            while (sim.Intro && introFrames++ < 60 * 120) { Tick(); IntroCard c2 = sim.World.Intro.At(sim.Hud.IntroT); if (c2 != null && c2.Kicker == "YOUR ORDERS, AGENT") sawOrders = true; }
            Check(!sim.Intro && sim.Started && sawOrders, "left alone, the intro plays its briefing and ends by itself after " + (introFrames * DT).ToString("0.0") + " s");
            Check(audio.Voices - voicesBefore == sim.World.Intro.Cards.Length && audio.LastVoice == sim.World.Intro.Cards.Length - 1, "each of the " + sim.World.Intro.Cards.Length + " cards starts its narration, in order");
            sim.SetPlaying(false); Press(Btn.A);
            Check(!sim.Intro && sim.Player.Enabled, "resuming from pause does not replay the intro");
            // a kill-cam left to play out, from the nest, before any sentry has been disturbed
            Soldier other = sim.Garrison.Soldiers[10];
            Vector3 chest2 = other.Pos + new Vector3(0, 1.25f, 0);
            sim.Player.AlwaysSteady = true; sim.AlwaysKillCam = true;
            AimAt(chest2); btn[Btn.LT] = 1; Tick(40); AimAt(chest2); Tick(10);
            btn[Btn.RT] = 1; Tick(); btn[Btn.RT] = 0;
            Check(sim.KcActive, "second kill-cam starts");
            int kcFrames = 0;
            while (sim.KcActive && kcFrames++ < 60 * 20) Tick();
            Check(!sim.KcActive && !other.Alive && !sim.Hud.Cine, "it ends by itself after " + (kcFrames * DT).ToString("0.0") + " s with the guard down");
            Release(); sim.AlwaysKillCam = false; Tick(90);
            Check(sim.Weapon.Bullets.Count == 0, "no stray rounds are left in flight");
            sim.Player.AlwaysSteady = false;
            sim.Player.Pos = new Vector3(0, Config.PLATEAU, 41.5f); Tick(2);
            Check(sim.Hud.PromptText == "Open the castle gate" && sim.Hud.PromptKey == "A", "prompt at the gate: [" + sim.Hud.PromptKey + "] " + sim.Hud.PromptText);
            Vector3 before = sim.Player.Pos;
            sim.Player.Yaw = 0; sim.Km.Down[(int)K.W] = true; Tick(120); sim.Km.Down[(int)K.W] = false;
            Check(sim.Player.Pos.Z > 39.5f, "the closed gate blocks the way (stopped at z = " + sim.Player.Pos.Z.ToString("0.0") + ")");
            Press(Btn.A); Tick(60);
            Check(gateDoor.Amt > 0.99f && gateDoor.Solid.Off, "A opens it");
            Go(0, 30, "walk in through the open gate");
            // keyboard twin
            sim.Km.Hit[(int)K.G] = true; sim.Km.AnyInput = true; Tick(); sim.Km.AnyInput = false;
            Check(sim.Weapon.Charges == 7 && !sim.Hud.IsPad, "G fires a charge from the keyboard and the prompts switch back");

            Console.WriteLine("6. Level One: the facility under BlitzScheiss Mountain");
            LevelOne(world, audio);

            Console.WriteLine("7. Sound synthesis");
            sw.Restart();
            System.Collections.Generic.Dictionary<string, short[]> pcm = Synth.RenderAll();
            Console.WriteLine("        " + pcm.Count + " clips synthesised in " + sw.ElapsedMilliseconds + " ms");
            string silentClips = "", clippedClips = ""; float seconds = 0;
            foreach (System.Collections.Generic.KeyValuePair<string, short[]> kv in pcm)
            {
                int top = 0; foreach (short v in kv.Value) top = Math.Max(top, Math.Abs((int)v));
                seconds += kv.Value.Length / (float)Synth.SR;
                if (top < 600) silentClips += kv.Key + " "; if (top > 32100) clippedClips += kv.Key + " ";
                Console.WriteLine("        " + kv.Key.PadRight(10) + (kv.Value.Length / (float)Synth.SR).ToString("0.00").PadLeft(6) + " s  peak " + (top / 327.67f).ToString("0") + "%");
            }
            Check(pcm.Count == 19 && silentClips.Length == 0, "every clip has sound in it" + (silentClips.Length > 0 ? " (silent: " + silentClips + ")" : ""));
            Check(clippedClips.Length == 0, "no clip is driven into clipping" + (clippedClips.Length > 0 ? " (" + clippedClips + ")" : ""));
            Check(seconds < 60, "about " + seconds.ToString("0") + " s of audio in memory");

            Console.WriteLine("8. Intro voice-over");
            string dir = AppContext.BaseDirectory, voiceDir = null;
            for (int up = 0; up < 8 && dir != null && voiceDir == null; up++)
            {
                string candidate = System.IO.Path.Combine(dir, "assets", "voice");
                if (System.IO.Directory.Exists(candidate)) voiceDir = candidate; else dir = System.IO.Path.GetDirectoryName(dir.TrimEnd(System.IO.Path.DirectorySeparatorChar));
            }
            Check(voiceDir != null && IntroVoice.Files.Length == IntroScript.LevelZero.Cards.Length, "assets/voice is there, with one clip listed per intro card");
            if (voiceDir != null)
                for (int i = 0; i < IntroVoice.Files.Length; i++)
                {
                    string path = System.IO.Path.Combine(voiceDir, IntroVoice.Files[i]);
                    Wav w = System.IO.File.Exists(path) ? Wav.Parse(System.IO.File.ReadAllBytes(path)) : null;
                    int top = 0;
                    if (w != null) for (int k = 0; k + 1 < w.Pcm.Length; k += 2) top = Math.Max(top, Math.Abs((int)BitConverter.ToInt16(w.Pcm, k)));
                    IntroCard ic = IntroScript.LevelZero.Cards[i];
                    Check(w != null && w.Channels == 1 && top > 8000 && Math.Abs(w.Seconds - IntroVoice.Seconds[i]) < 0.05f && ic.End - ic.Start > w.Seconds,
                        IntroVoice.Files[i] + (w == null ? " is missing or not 16-bit PCM" : ": " + w.Seconds.ToString("0.00") + " s at " + w.Rate + " Hz, fits its card (" + (ic.End - ic.Start).ToString("0.0") + " s)"));
                }
            Check(Wav.Parse(new byte[60]) == null && Wav.Parse(null) == null, "a file that is not a WAVE file is rejected, not played");

            Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : failures + " CHECK(S) FAILED");
            return failures == 0 ? 0 : 1;
        }
    }
}
