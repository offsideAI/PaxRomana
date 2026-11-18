// Desktop launcher for the same game the Xbox runs: macOS, Windows or Linux, with a controller or keyboard and mouse.
//
//     cd desktop && dotnet run -c Release                     play in a window
//     dotnet run -c Release -- --fullscreen                   play full screen
//     dotnet run -c Release -- --console                      behave as on the Xbox: controller prompts, no mouse
//     dotnet run -c Release -- --shots <folder>               render a fixed set of views to PNG files and exit (a visual check)
using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PaxRomana
{
    static class Program
    {
        static void Main(string[] args)
        {
            string shots = null; bool full = false, mute = false, console = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--shots" && i + 1 < args.Length) shots = args[++i];
                else if (args[i] == "--fullscreen") full = true;
                else if (args[i] == "--mute") mute = true;
                else if (args[i] == "--console") console = true;
            }
            using (DesktopGame game = new DesktopGame(shots, full))
            {
                game.Mute = mute || shots != null;
                game.IsConsole = console;
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
            GameSim s = Sim;
            Vector3 nest = new Vector3(Config.SPAWN_X, Terrain.GroundAt(Config.SPAWN_X, Config.SPAWN_Z) + 1.7f, Config.SPAWN_Z);
            switch (step)
            {
                case 0: wait = 0.5f; break;                                                            // title screen
                case 1: s.Begin(true); s.Hud.HintT = 0; wait = 0.67f; break;                            // the nest, rifle at the hip
                case 2: Aim(nest + new Vector3(Config.SUN_DIR.X, 0.02f, Config.SUN_DIR.Z) * 500); wait = 0.5f; break;   // towards the low sun
                case 3: Aim(new Vector3(0, 48, 0)); s.Weapon.WantAds = true; wait = 0.83f; break;       // through the scope at the Kremlin
                case 4: s.Weapon.ZoomI = 4; Aim(s.Legion.Soldiers[9].Pos + new Vector3(0, 1.25f, 0)); s.AlwaysKillCam = true; s.Player.AlwaysSteady = true; wait = 0.5f; break;   // 16x on a gate guard
                case 5: s.Trigger(); wait = 0.5f; break;                                                // kill-cam: the round in flight
                case 6: wait = 5.9f; break;                                                            // ... arriving
                case 7: wait = 4.33f; break;                                                            // ... and back behind the scope
                case 8: Aim(new Vector3(1, 33, 39.8f)); wait = 0.17f; break;
                case 9: s.TriggerExplosive(); wait = 0.48f; break;                                       // a charge landing on the gate
                case 10: s.Weapon.WantAds = false; Teleport(0, 58, 0, 0.12f); wait = 0.67f; break;       // below the breached gate
                case 11: Teleport(-14, 20, -1.2f, 0.25f); wait = 0.67f; break;                           // courtyard: Senate, cathedral, flagpole
                case 12: Teleport(-8, 4, 0.75f, 0.0f); wait = 0.6f; break;
                case 13: s.TriggerExplosive(); wait = 0.15f; break;                                       // an explosion close by
                case 14: wait = 0.75f; break;                                                            // its smoke
                case 15: Teleport(120, 80, 0.9f, 0.05f); wait = 0.67f; break;                            // the road, camp and skyline
                case 16: Teleport(60, 170, 0.1f, 0.0f); s.Hud.ToggleKeys(); wait = 0.5f; break;          // by the bridge, with the controls overlay
                case 17: s.Hud.ToggleKeys(); s.Hud.End(true, new EndStats { Kills = 21, Total = 29, Shots = 30, Hits = 24, Heads = 9, Longest = 412, Time = 754, Alarms = 1, Charges = 5 }); wait = 0.33f; break;
                default:
                    Console.WriteLine("average per frame: update " + (updateMs / frames).ToString("0.00") + " ms, draw " + (drawMs / frames).ToString("0.00") + " ms over " + frames + " frames");
                    Exit(); return;
            }
            step++;
        }

        void Aim(Vector3 target)
        {
            Vector3 eye = Sim.Player.Pos + new Vector3(0, 1.7f, 0), d = target - eye;
            Sim.Player.Yaw = M.Atan2(-d.X, -d.Z); Sim.Player.Pitch = M.Atan2(d.Y, M.Hypot(d.X, d.Z));
        }
        void Teleport(float x, float z, float yaw, float pitch)
        {
            Sim.Player.Pos = new Vector3(x, Terrain.GroundAt(x, z), z); Sim.Player.Yaw = yaw; Sim.Player.Pitch = pitch;
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
