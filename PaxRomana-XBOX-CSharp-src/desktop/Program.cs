// Desktop launcher for the same game the Xbox runs: macOS, Windows or Linux, with a controller or keyboard and mouse.
//
//     cd desktop && dotnet run -c Release                     play in a window
//     dotnet run -c Release -- --fullscreen                   play full screen
//     dotnet run -c Release -- --console                      behave as on the Xbox: controller prompts, no mouse
//     dotnet run -c Release -- --level 1                      start on Level One
//     dotnet run -c Release -- --shots <folder>               render a fixed set of views to PNG files and exit (a visual check)
//     dotnet run -c Release -- --voice-check                  load the intro voice-over as the game does, list it, and exit (plays nothing)
using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PaxRomana
{
    static class Program
    {
        // Reads the voice-over files from where the program was built to, exactly as the game does, and makes a sound
        // effect of each without playing it. Returns 0 if every clip loaded.
        static int VoiceCheck()
        {
            byte[][] files = PaxGame.ReadVoice(); int bad = 0;
            for (int i = 0; i < files.Length; i++)
            {
                Wav w = Wav.Parse(files[i]);
                if (w == null) { Console.WriteLine(IntroVoice.Files[i] + ": " + (files[i] == null ? "not found" : "not a 16-bit PCM WAVE file")); bad++; continue; }
                string made = "sound effect created";
                try { using (Microsoft.Xna.Framework.Audio.SoundEffect fx = new Microsoft.Xna.Framework.Audio.SoundEffect(w.Pcm, w.Rate, Microsoft.Xna.Framework.Audio.AudioChannels.Mono)) made += " (" + fx.Duration.TotalSeconds.ToString("0.00") + " s)"; }
                catch (Exception e) { made = "no sound effect: " + e.GetType().Name; bad++; }
                Console.WriteLine(IntroVoice.Files[i] + ": " + files[i].Length + " bytes, " + w.Rate + " Hz, " + w.Seconds.ToString("0.00") + " s, " + made);
            }
            return bad == 0 ? 0 : 1;
        }

        static void Main(string[] args)
        {
            string shots = null; bool full = false, mute = false, console = false; int level = 0;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--shots" && i + 1 < args.Length) shots = args[++i];
                else if (args[i] == "--fullscreen") full = true;
                else if (args[i] == "--mute") mute = true;
                else if (args[i] == "--console") console = true;
                else if (args[i] == "--level" && i + 1 < args.Length) int.TryParse(args[++i], out level);
                else if (args[i] == "--voice-check") { Environment.Exit(VoiceCheck()); }
            }
            using (DesktopGame game = new DesktopGame(shots, full))
            {
                game.Mute = mute || shots != null;
                game.IsConsole = console;
                game.StartLevel = level;
                game.Run();
            }
        }
    }

    sealed class DesktopGame : PaxGame
    {
        readonly string shots; int step; float wait; bool capture; RenderTarget2D target;

        public DesktopGame(string shots, bool full)
        {
            this.shots = shots;
            Window.Title = "Pax Romana: Tertia Roma";
            Window.AllowUserResizing = true;
            if (full)
            {
                Graphics.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
                Graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
                Graphics.IsFullScreen = true; Graphics.HardwareModeSwitch = false;
            }
            else { Graphics.PreferredBackBufferWidth = shots != null ? 1280 : 1600; Graphics.PreferredBackBufferHeight = shots != null ? 720 : 900; }
        }

        // ---- visual check: pose the camera in a handful of places, save what the renderer draws, and quit ----
        readonly System.Diagnostics.Stopwatch clock = new System.Diagnostics.Stopwatch(); double updateMs, drawMs; int frames;

        protected override void Update(GameTime gameTime)
        {
            clock.Restart();
            base.Update(gameTime);
            if (shots == null || Sim == null) return;
            updateMs += clock.Elapsed.TotalMilliseconds; frames++;
            if (wait > 0) { wait -= (float)gameTime.ElapsedGameTime.TotalSeconds; if (wait > 0) return; capture = true; return; }
            if (StartLevel == 1 && step < 26) step = 26;          // with the level option, only the Level One views
            GameSim s = Sim;
            Vector3 nest = new Vector3(Config.SPAWN_X, Terrain.GroundAt(Config.SPAWN_X, Config.SPAWN_Z) + 1.7f, Config.SPAWN_Z);
            switch (step)
            {
                case 0: wait = 0.5f; break;                                                            // title screen
                case 1: s.Begin(true); wait = Mid(0); break;                                            // intro: the Level Zero title card
                case 2: wait = Mid(1) - Mid(0); break;                                                  // ... the enrichment works
                case 3: wait = Mid(2) - Mid(1); break;                                                  // ... the keep
                case 4: wait = Mid(3) - Mid(2); break;                                                  // ... the orders
                case 5: s.EndIntro(); s.Hud.HintT = 0; wait = 0.67f; break;                             // the nest, rifle at the hip
                case 6: Near(s.Garrison.Soldiers[24], 2.6f, 0.9f); wait = 0.3f; break;                  // a soldier by the camp fire, close up
                case 7: Near(s.Garrison.Soldiers[16], 3.4f, -1.6f); wait = 0.3f; break;                 // the officer and his section
                case 8: Teleport(Config.SPAWN_X, Config.SPAWN_Z, 0, 0); Aim(new Vector3(Config.WORKS_X, 40, Config.WORKS_Z)); wait = 0.5f; break;   // the works from the nest
                case 9: Aim(new Vector3(0, 48, 0)); s.Weapon.WantAds = true; wait = 0.83f; break;       // through the scope at the castle
                case 10: s.Weapon.ZoomI = 4; Aim(s.Garrison.Soldiers[9].Pos + new Vector3(0, 1.25f, 0)); s.AlwaysKillCam = true; s.Player.AlwaysSteady = true; wait = 0.5f; break;   // 16x on a gate guard
                case 11: s.Trigger(); wait = 0.5f; break;                                               // kill-cam: the round in flight
                case 12: wait = 5.9f; break;                                                           // ... arriving
                case 13: wait = 4.33f; break;                                                           // ... and back behind the scope
                case 14: Aim(new Vector3(1, 33, 39.8f)); wait = 0.17f; break;
                case 15: s.TriggerExplosive(); wait = 0.48f; break;                                      // a charge landing on the gate
                case 16: s.Weapon.WantAds = false; Teleport(0, 58, 0, 0.12f); wait = 0.67f; break;       // below the breached gate
                case 17: Teleport(-14, 22, -1.2f, 0.25f); wait = 0.67f; break;                           // courtyard: keep, chapel, flagpole
                case 18: foreach (Door d in World.Site.Doors) d.Want = 1; Teleport(0, -11.2f, 0, -0.28f); wait = 1.0f; break;   // in the vault, at the strongbox
                case 19: Teleport(-8, 4, 0.75f, 0.0f); wait = 0.6f; break;
                case 20: s.TriggerExplosive(); wait = 0.15f; break;                                       // an explosion close by
                case 21: wait = 0.75f; break;                                                            // its smoke
                case 22: Teleport(120, 80, 0.9f, 0.05f); wait = 0.67f; break;                            // the road and the camp
                case 23: Teleport(430, -385, 0, 0.02f); Aim(new Vector3(Config.WORKS_X, 45, Config.WORKS_Z)); wait = 0.5f; break;   // the works from the edge of the valley
                case 24: Teleport(60, 170, 0.1f, 0.0f); s.Hud.ToggleKeys(); wait = 0.5f; break;          // by the bridge, with the controls overlay
                case 25: s.Hud.ToggleKeys(); s.Hud.End(true, new EndStats { Kills = 21, Total = 29, Shots = 30, Hits = 24, Heads = 9, Longest = 412, Time = 754, Alarms = 1, Charges = 5 }); wait = 0.33f; break;
                // ---- Level One ----
                case 26: if (World.Level != 1) NewMission(1); wait = 0.5f; break;                                               // its title menu
                case 27: Sim.CanSwitchLevels = true; Sim.Begin(true); wait = Mid(0); break;               // intro: the Level One title card
                case 28: wait = Mid(1) - Mid(0); break;                                                   // ... the beam room
                case 29: Sim.EndIntro(); Sim.Hud.HintT = 0; wait = 0.6f; break;                           // the service lock
                case 30: Teleport(-7.5f, -3.5f, 0, 0); Aim(new Vector3(4, Facility.FY + 0.6f, -14)); wait = 0.5f; break;      // interlock 1: crate and plate
                case 31: Teleport(-6.5f, -25.5f, 0, 0); Aim(new Vector3(3, Facility.FY + 1.2f, -37)); wait = 0.5f; break;     // interlock 2: the beam
                case 32: Teleport(0, -51, 0, 0); Aim(new Vector3(2, Facility.FY + 3.4f, -78)); wait = 0.5f; break;            // interlock 3: the targets
                case 33: ((Sabotage)Sim.Mission).BulletHit(World.Facility.Targets[0].Pos); ((Sabotage)Sim.Mission).BulletHit(World.Facility.Targets[2].Pos); wait = 0.3f; break;   // two of them lit
                case 34: Teleport(-6.5f, -81.5f, 0, 0); Sim.Mission.Update(0); Sim.Mission.Use(); Aim(new Vector3(6, Facility.FY - 1, -103)); wait = 0.5f; break;       // interlock 4: the gap and the gates, crate in hand
                case 35: Teleport(0, -113.5f, 0, 0); Aim(new Vector3(0, Facility.FY + 4.5f, -165)); wait = 0.5f; break;       // the centrifuge hall
                case 36: Teleport(-3.2f, -136.5f, 0, 0); Aim(new Vector3(6, Facility.FY + 2.5f, -150)); wait = 0.5f; break;    // among the centrifuges, by the controls
                case 37: Teleport(34, -142, 0, 0); Aim(new Vector3(60, Facility.TunnelFloor(60) + 2.2f, -142)); wait = 0.5f; break;   // the service tunnel and its blast door
                case 38: ((Sabotage)Sim.Mission).Planted = true; Teleport(108, -137, 0, 0); Aim(new Vector3(97, Facility.FY + 12.5f, -142)); wait = 0.6f; break;   // outside, looking back at the portal and its threshold
                case 39: Sim.Mission.Use(); wait = 0.22f; break;                                           // detonation
                case 40: wait = 1.6f; break;                                                              // smoke from the mouth
                case 41: wait = 5.5f; break;                                                              // the end screen
                default:
                    Console.WriteLine("average per frame: update " + (updateMs / frames).ToString("0.00") + " ms, draw " + (drawMs / frames).ToString("0.00") + " ms over " + frames + " frames");
                    Exit(); return;
            }
            step++;
        }

        float Mid(int card) { return (Sim.World.Intro.Cards[card].Start + Sim.World.Intro.Cards[card].End) / 2; }      // the middle of an intro card

        void Aim(Vector3 target)
        {
            Vector3 eye = Sim.Player.Pos + new Vector3(0, 1.7f, 0), d = target - eye;
            Sim.Player.Yaw = M.Atan2(-d.X, -d.Z); Sim.Player.Pitch = M.Atan2(d.Y, M.Hypot(d.X, d.Z));
        }
        // Stand in front of a soldier, a little to one side, and look at him.
        void Near(Soldier so, float dist, float side)
        {
            float fx = M.Sin(so.Yaw), fz = M.Cos(so.Yaw);
            float x = so.Pos.X + fx * dist + fz * side, z = so.Pos.Z + fz * dist - fx * side;
            Sim.Player.Pos = new Vector3(x, so.Pos.Y, z);
            Aim(so.Pos + new Vector3(0, 1.45f, 0));
        }
        void Teleport(float x, float z, float yaw, float pitch)
        {
            Sim.Player.Pos = new Vector3(x, Sim.World.Arena.Ground(x, z), z); Sim.Player.Yaw = yaw; Sim.Player.Pitch = pitch;
        }

        protected override void Draw(GameTime gameTime)
        {
            if (shots == null || Sim == null || !capture) { clock.Restart(); base.Draw(gameTime); drawMs += clock.Elapsed.TotalMilliseconds; return; }
            capture = false;
            if (target == null) target = new RenderTarget2D(GraphicsDevice, 1280, 720, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            Output = target;
            base.Draw(gameTime);
            Output = null;
            GraphicsDevice.SetRenderTarget(null);
            Directory.CreateDirectory(shots);
            using (FileStream f = File.Create(Path.Combine(shots, "shot" + step.ToString("00") + ".png"))) target.SaveAsPng(f, target.Width, target.Height);
            Console.WriteLine("saved shot " + step);
        }
    }
}
