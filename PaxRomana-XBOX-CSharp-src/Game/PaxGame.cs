// The game shell: owns the window, loads the world on a background thread behind a loading screen, feeds controller,
// keyboard and mouse into the mission each frame, sends haptics back to the controller, and draws.
// The Xbox app and the desktop build both run this class; the Xbox app subclasses it for the few console specifics.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace PaxRomana
{
    public class PaxGame : Game
    {
        protected readonly GraphicsDeviceManager Graphics;
        protected World World; protected GameSim Sim;
        protected Renderer Renderer; HudRenderer hudRenderer;
        public bool IsConsole;                  // true on Xbox: controller prompts from the start, no mouse, no quitting to desktop
        public bool Mute;
        readonly Pad pad = new Pad(); readonly KeyMouse km = new KeyMouse();
        readonly Hud bootHud = new Hud();
        IAudio audio = new NullAudio(); SynthAudio synth;
        Task<Loaded> loader; volatile float loadP; volatile string loadText = "Preparing…";
        sealed class Loaded { public World World; public Dictionary<string, short[]> Pcm; public byte[][] Voice; }
        int padIndex = -1; readonly float[] axes = new float[4], buttons = new float[16], sent = new float[4];
        KeyboardState prevKeys; MouseState prevMouse; bool captured;
        protected RenderTarget2D Output;        // where the finished frame goes; null = the screen
        RenderTarget2D scene; SpriteBatch blit; bool noMsaa;
        string fatal;                           // set if the game hit an error it could not recover from
        bool introShown;

        public PaxGame()
        {
            Graphics = new GraphicsDeviceManager(this);
            Graphics.GraphicsProfile = GraphicsProfile.HiDef;
            // No multisampling on the back buffer: the Xbox swap chain cannot have it. The scene is anti-aliased in
            // an off-screen target instead (see Draw).
            Graphics.PreferMultiSampling = false;
            Graphics.SynchronizeWithVerticalRetrace = true;
            Graphics.PreferredDepthStencilFormat = DepthFormat.Depth24;
            IsFixedTimeStep = false;
        }

        protected override void LoadContent()
        {
            Renderer = new Renderer(GraphicsDevice);
            hudRenderer = new HudRenderer(GraphicsDevice, Renderer);
            blit = new SpriteBatch(GraphicsDevice);
            bool wantSound = !Mute;
            loader = Task.Run(() =>
            {
                Loaded l = new Loaded();
                l.World = World.Build((p, text) => { loadP = p; loadText = text; });
                if (wantSound) { l.Pcm = Synth.RenderAll(); l.Voice = ReadVoice(); }
                return l;
            });
        }

        // The intro voice-over, read from the files shipped with the app. A missing file is not an error: the intro
        // then plays that card without narration.
        public static byte[][] ReadVoice()
        {
            byte[][] files = new byte[IntroVoice.Files.Length][];
            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    using (System.IO.Stream s = TitleContainer.OpenStream(IntroVoice.Folder + "/" + IntroVoice.Files[i]))
                    using (System.IO.MemoryStream m = new System.IO.MemoryStream()) { s.CopyTo(m); files[i] = m.ToArray(); }
                }
                catch (Exception) { files[i] = null; }
            }
            return files;
        }

        void NewMission()
        {
            Hud hud = new Hud();
            hud.SetReady();
            Sim = new GameSim(World, audio, pad, km, hud) { Console = IsConsole, ShowIntro = !introShown };
            introShown = true;          // the intro plays once per session, not on every new mission
            pad.Stop();
        }

        // ---- input ----
        void ReadPad()
        {
            // keep the controller we were using; otherwise take the first one connected
            GamePadState st = padIndex >= 0 ? GamePad.GetState(padIndex, GamePadDeadZone.None) : default(GamePadState);
            if (padIndex < 0 || !st.IsConnected)
            {
                padIndex = -1;
                for (int i = 0; i < 4; i++) { st = GamePad.GetState(i, GamePadDeadZone.None); if (st.IsConnected) { padIndex = i; break; } }
            }
            if (padIndex < 0) { pad.SetState(false, axes, buttons); return; }
            axes[0] = st.ThumbSticks.Left.X; axes[1] = -st.ThumbSticks.Left.Y; axes[2] = st.ThumbSticks.Right.X; axes[3] = -st.ThumbSticks.Right.Y;
            GamePadButtons b = st.Buttons;
            buttons[Btn.A] = P(b.A); buttons[Btn.B] = P(b.B); buttons[Btn.X] = P(b.X); buttons[Btn.Y] = P(b.Y);
            buttons[Btn.LB] = P(b.LeftShoulder); buttons[Btn.RB] = P(b.RightShoulder);
            buttons[Btn.LT] = st.Triggers.Left; buttons[Btn.RT] = st.Triggers.Right;
            buttons[Btn.VIEW] = P(b.Back); buttons[Btn.MENU] = P(b.Start); buttons[Btn.L3] = P(b.LeftStick); buttons[Btn.R3] = P(b.RightStick);
            buttons[Btn.UP] = P(st.DPad.Up); buttons[Btn.DOWN] = P(st.DPad.Down); buttons[Btn.LEFT] = P(st.DPad.Left); buttons[Btn.RIGHT] = P(st.DPad.Right);
            pad.SetState(true, axes, buttons);
        }
        static float P(ButtonState s) { return s == ButtonState.Pressed ? 1 : 0; }

        static readonly Keys[][] KeyMap =
        {
            new[] { Keys.W }, new[] { Keys.A }, new[] { Keys.S }, new[] { Keys.D }, new[] { Keys.Left }, new[] { Keys.Right }, new[] { Keys.Up }, new[] { Keys.Down },
            new[] { Keys.LeftShift, Keys.RightShift }, new[] { Keys.C }, new[] { Keys.Z }, new[] { Keys.R }, new[] { Keys.E }, new[] { Keys.Q }, new[] { Keys.F }, new[] { Keys.G },
            new[] { Keys.H }, new[] { Keys.O }, new[] { Keys.X }, new[] { Keys.Space }, new[] { Keys.Enter }, new[] { Keys.OemPlus, Keys.Add }, new[] { Keys.OemMinus, Keys.Subtract },
            new[] { Keys.Escape }, new[] { Keys.OemOpenBrackets }, new[] { Keys.OemCloseBrackets },
        };

        void ReadKeysAndMouse()
        {
            KeyboardState ks = Keyboard.GetState();
            km.AnyInput = false;
            for (int i = 0; i < KeyMap.Length; i++)
            {
                bool down = false, was = false;
                foreach (Keys k in KeyMap[i]) { down |= ks.IsKeyDown(k); was |= prevKeys.IsKeyDown(k); }
                km.Down[i] = down; km.Hit[i] = down && !was;
                if (km.Hit[i]) km.AnyInput = true;
            }
            prevKeys = ks;
            km.MouseDX = km.MouseDY = 0; km.LeftHit = km.MiddleHit = km.RightHit = km.RightReleased = false; km.Wheel = 0;
            if (IsConsole) return;
            MouseState ms = Mouse.GetState();
            bool inside = IsActive && ms.X >= 0 && ms.Y >= 0 && ms.X < GraphicsDevice.Viewport.Width && ms.Y < GraphicsDevice.Viewport.Height;
            if (inside || captured)
            {
                km.LeftHit = ms.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released;
                km.MiddleHit = ms.MiddleButton == ButtonState.Pressed && prevMouse.MiddleButton == ButtonState.Released;
                km.RightDown = ms.RightButton == ButtonState.Pressed;
                km.RightHit = km.RightDown && prevMouse.RightButton == ButtonState.Released;
                km.RightReleased = !km.RightDown && prevMouse.RightButton == ButtonState.Pressed;
                int dw = ms.ScrollWheelValue - prevMouse.ScrollWheelValue;
                km.Wheel = dw > 0 ? 1 : dw < 0 ? -1 : 0;
                if (km.LeftHit || km.MiddleHit || km.RightHit || km.Wheel != 0) km.AnyInput = true;
            }
            // while a mouse player is in the game the cursor is hidden and held at the centre of the window
            bool want = Sim != null && Sim.WantMouseCapture && IsActive;
            if (want)
            {
                int cx = GraphicsDevice.Viewport.Width / 2, cy = GraphicsDevice.Viewport.Height / 2;
                if (captured) { km.MouseDX = ms.X - cx; km.MouseDY = ms.Y - cy; }
                Mouse.SetPosition(cx, cy);
                ms = new MouseState(cx, cy, ms.ScrollWheelValue, ms.LeftButton, ms.MiddleButton, ms.RightButton, ms.XButton1, ms.XButton2);
            }
            if (want != captured) { captured = want; IsMouseVisible = !want; }
            prevMouse = ms;
        }

        void SendRumble(bool force)
        {
            float[] o = pad.Out; bool changed = force;
            for (int i = 0; i < 4; i++) if (Math.Abs(o[i] - sent[i]) > 0.01f) changed = true;
            if (!changed || padIndex < 0) return;
            for (int i = 0; i < 4; i++) sent[i] = o[i];
            try { GamePad.SetVibration(padIndex, o[0], o[1], o[2], o[3]); } catch (Exception) { /* haptics are optional */ }
        }

        void StopRumble()
        {
            pad.Stop();
            for (int i = 0; i < 4; i++) pad.Out[i] = 0;
            SendRumble(true);
        }

        protected override void OnDeactivated(object sender, EventArgs args)
        {
            if (Sim != null && Sim.Intro) Sim.EndIntro();              // leaving the app mid-intro: stop the narration and go to the paused mission
            if (Sim != null && Sim.Player.Enabled) Sim.SetPlaying(false);
            StopRumble();
            base.OnDeactivated(sender, args);
        }

        protected override void OnExiting(object sender, EventArgs args) { StopRumble(); base.OnExiting(sender, args); }

        // An error on a console would otherwise just close the app; show it on screen instead so it can be reported.
        void Fail(Exception e)
        {
            if (fatal != null) return;
            string s = e.ToString();
            fatal = s.Length > 1100 ? s.Substring(0, 1100) : s;
            try { StopRumble(); } catch (Exception) { }
        }

        protected override void Update(GameTime gameTime)
        {
            if (fatal == null)
            {
                try { Step(gameTime); } catch (Exception e) { Fail(e); }
            }
            else
            {
                try { ReadKeysAndMouse(); if (!IsConsole && km.Pressed(K.Escape)) Exit(); } catch (Exception) { }
            }
            base.Update(gameTime);
        }

        void Step(GameTime gameTime)
        {
            float dt = Math.Min((float)gameTime.ElapsedGameTime.TotalSeconds, 0.05f);
            ReadPad(); ReadKeysAndMouse();
            if (Sim == null)
            {
                bootHud.Loading(loadP, loadText);
                bootHud.IsPad = IsConsole || pad.Connected;
                if (loader != null && loader.IsCompleted)
                {
                    if (loader.IsFaulted)
                    {
                        Exception e = loader.Exception.InnerException ?? loader.Exception;
                        loader = null;
                        Fail(e);
                    }
                    else
                    {
                        Loaded l = loader.Result; loader = null;
                        World = l.World;
                        Renderer.SetWorld(World);
                        if (l.Pcm != null)
                        {
                            try { synth = new SynthAudio(l.Pcm); if (l.Voice != null) synth.SetVoice(l.Voice); audio = synth; } catch (Exception) { audio = new NullAudio(); }      // no audio device: play silently
                        }
                        NewMission();
                    }
                }
                if (!IsConsole && km.Pressed(K.Escape)) Exit();
            }
            else
            {
                bool onMenu = !Sim.Player.Enabled && !Sim.Intro;
                Sim.Frame(dt);
                if (synth != null) synth.Update(dt);
                SendRumble(false);
                if (Sim.RestartRequested) NewMission();
                else if (onMenu && !IsConsole && km.Pressed(K.Escape)) Exit();
            }
        }

        // The scene is drawn into a 4x multisampled target the size of the output and copied across; the HUD is drawn
        // on top at full sharpness. If the target cannot be made, the scene is drawn straight to the output.
        RenderTarget2D SceneTarget()
        {
            if (noMsaa) return null;
            int w = Output != null ? Output.Width : GraphicsDevice.PresentationParameters.BackBufferWidth;
            int h = Output != null ? Output.Height : GraphicsDevice.PresentationParameters.BackBufferHeight;
            if (scene != null && scene.Width == w && scene.Height == h) return scene;
            if (scene != null) { scene.Dispose(); scene = null; }
            try { scene = new RenderTarget2D(GraphicsDevice, w, h, false, SurfaceFormat.Color, DepthFormat.Depth24, 4, RenderTargetUsage.DiscardContents); }
            catch (Exception) { scene = null; noMsaa = true; }
            return scene;
        }

        protected override void Draw(GameTime gameTime)
        {
            if (fatal == null)
            {
                try { Paint(); } catch (Exception e) { Fail(e); }
            }
            if (fatal != null)
            {
                try
                {
                    GraphicsDevice.SetRenderTarget(Output);
                    GraphicsDevice.Clear(new Color(9, 11, 18));
                    hudRenderer.DrawError(fatal);
                }
                catch (Exception) { }
            }
            base.Draw(gameTime);
        }

        void Paint()
        {
            if (Sim == null)
            {
                GraphicsDevice.SetRenderTarget(Output);
                GraphicsDevice.Clear(new Color(9, 11, 18));
                hudRenderer.Draw(bootHud, null, 0);
                return;
            }
            RenderTarget2D target = SceneTarget();
            GraphicsDevice.SetRenderTarget(target ?? Output);
            Renderer.DrawWorld(Sim);
            Renderer.DrawViewModel(Sim);
            if (target != null)
            {
                GraphicsDevice.SetRenderTarget(Output);
                blit.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                blit.Draw(target, Vector2.Zero, Color.White);
                blit.End();
            }
            hudRenderer.Draw(Sim.Hud, Sim, Renderer.BlastGlare(Sim));
        }
    }
}
