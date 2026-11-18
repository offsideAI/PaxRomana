// Controller input and haptics, plus the keyboard and mouse state used by the desktop build.
// The game loop fills these each frame from the platform; nothing here touches a device, so tests can drive them.
using System;
using System.Collections.Generic;

namespace PaxRomana
{
    // Button indices follow the standard gamepad layout.
    public static class Btn
    {
        public const int A = 0, B = 1, X = 2, Y = 3, LB = 4, RB = 5, LT = 6, RT = 7, VIEW = 8, MENU = 9, L3 = 10, R3 = 11, UP = 12, DOWN = 13, LEFT = 14, RIGHT = 15;
    }

    public sealed class Pad
    {
        const float DEAD = 0.16f;
        public readonly float[] Axes = new float[4];         // left x, left y, right x, right y (y is positive downwards)
        readonly float[] btn = new float[16], prev = new float[16];
        public bool Connected;      // a controller is present
        public bool Active;         // the controller was the last device used (drives the button prompts)
        public bool Enabled = true;
        public readonly float[] Out = new float[4];          // low motor, high motor, left trigger, right trigger (0..1)

        sealed class Pulse { public float Low, High, Lt, Rt, Dur, T; public bool Decay; }
        readonly List<Pulse> fx = new List<Pulse>();

        // Call once per frame with the controller's current state (axes[4], buttons[16] as 0..1), or connected = false.
        public void SetState(bool connected, float[] axes, float[] buttons)
        {
            for (int i = 0; i < 16; i++) prev[i] = btn[i];
            Connected = connected;
            for (int i = 0; i < 4; i++) Axes[i] = connected ? axes[i] : 0;
            for (int i = 0; i < 16; i++) btn[i] = connected ? buttons[i] : 0;
            if (connected && !Active)
            {
                if (M.Hypot(Axes[0], Axes[1]) > 0.5f || M.Hypot(Axes[2], Axes[3]) > 0.5f) Active = true;
                for (int i = 0; i < 16; i++) if (btn[i] > 0.5f) Active = true;
            }
        }

        public float Value(int i) { return btn[i]; }
        public bool Down(int i) { return btn[i] > 0.5f; }
        public bool Pressed(int i) { return btn[i] > 0.5f && prev[i] <= 0.5f; }
        public bool Released(int i) { return btn[i] <= 0.5f && prev[i] > 0.5f; }

        // Stick with a radial dead zone, rescaled so movement starts from zero at its edge. which: 0 = left, 1 = right.
        public void Stick(int which, out float x, out float y)
        {
            float ax = Axes[which * 2], ay = Axes[which * 2 + 1], m = M.Hypot(ax, ay);
            if (m < DEAD) { x = 0; y = 0; return; }
            float k = Math.Min(1, (m - DEAD) / (1 - DEAD)) / m;
            x = ax * k; y = ay * k;
        }

        // Queue a haptic pulse. low / high are the heavy and light body motors, lt / rt the trigger motors (0..1).
        // ms is the length, delay (ms) postpones it, decay fades it out instead of cutting it off.
        public void Rumble(float low = 0, float high = 0, float lt = 0, float rt = 0, float ms = 100, float delay = 0, bool decay = false)
        {
            if (!Enabled) return;
            if (fx.Count > 24) fx.RemoveAt(0);
            fx.Add(new Pulse { Low = low, High = high, Lt = lt, Rt = rt, Dur = ms / 1000f, T = -delay / 1000f, Decay = decay });
        }
        public void Stop() { fx.Clear(); }
        public int Pending { get { return fx.Count; } }

        // Mixes the active pulses into Out; the platform layer sends Out to the controller.
        public void Update(float dt)
        {
            float l = 0, h = 0, lt = 0, rt = 0; int n = 0;
            for (int i = 0; i < fx.Count; i++)
            {
                Pulse e = fx[i];
                e.T += dt;
                if (e.T >= e.Dur) continue;
                fx[n++] = e;
                if (e.T < 0) continue;
                float k = e.Decay ? 1 - e.T / e.Dur : 1;
                l += e.Low * k; h += e.High * k; lt += e.Lt * k; rt += e.Rt * k;
            }
            fx.RemoveRange(n, fx.Count - n);
            Out[0] = Math.Min(1, l); Out[1] = Math.Min(1, h); Out[2] = Math.Min(1, lt); Out[3] = Math.Min(1, rt);
        }
    }

    public enum K
    {
        W, A, S, D, Left, Right, Up, Down, Shift, C, Z, R, E, Q, F, G, H, O, X, Space, Enter, Plus, Minus, Escape, BracketL, BracketR,
        Count
    }

    public sealed class KeyMouse
    {
        public readonly bool[] Down = new bool[(int)K.Count], Hit = new bool[(int)K.Count];
        public float MouseDX, MouseDY;                  // pixels moved this frame while the mouse is captured
        public bool LeftHit, MiddleHit, RightDown, RightHit, RightReleased;
        public int Wheel;                               // +1 / -1 notches this frame
        public bool AnyInput;                           // a key or mouse button was used this frame

        public bool IsDown(K k) { return Down[(int)k]; }
        public bool Pressed(K k) { return Hit[(int)k]; }
        public void Clear()
        {
            Array.Clear(Down, 0, Down.Length); Array.Clear(Hit, 0, Hit.Length);
            MouseDX = MouseDY = 0; LeftHit = MiddleHit = RightDown = RightHit = RightReleased = false; Wheel = 0; AnyInput = false;
        }
    }

    public interface IAudio
    {
        void Shot(); void Bolt(float delay); void Reload(); void Dry(); void Impact(string kind, float delay, float dist); void Hit(bool head);
        void Horn(); void Heartbeat(); void Launch(); void Explosion(float delay, float dist); void Door(); void Loot(); void Step();
    }

    public sealed class NullAudio : IAudio
    {
        public int Shots, Explosions;
        public void Shot() { Shots++; } public void Bolt(float delay) { } public void Reload() { } public void Dry() { }
        public void Impact(string kind, float delay, float dist) { } public void Hit(bool head) { } public void Horn() { } public void Heartbeat() { }
        public void Launch() { } public void Explosion(float delay, float dist) { Explosions++; } public void Door() { } public void Loot() { } public void Step() { }
    }
}
