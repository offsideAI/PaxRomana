// First-person controller: free roam on foot (walk / crouch / prone, wade and swim, cross the bridge), scope sway
// and breath control. Looks with the right stick, the mouse or the arrow keys.
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace PaxRomana
{
    public sealed class Camera
    {
        public Matrix World = Matrix.Identity;       // orientation and position
        public float Fov = Player.BASE_FOV, Near = 0.25f;
        public Vector3 Pos { get { return World.Translation; } }
        public Vector3 Forward { get { return World.Forward; } }
        public Vector3 Right { get { return World.Right; } }
        public Vector3 Up { get { return World.Up; } }
        public Matrix View { get { return Matrix.Invert(World); } }
        public Matrix Projection(float aspect, float near, float far) { return Matrix.CreatePerspectiveFieldOfView(M.Rad(Fov), aspect, near, far); }

        public void SetEuler(Vector3 pos, float pitch, float yaw, float roll)
        {
            World = Matrix.CreateRotationZ(roll) * Matrix.CreateRotationX(pitch) * Matrix.CreateRotationY(yaw) * Matrix.CreateTranslation(pos);
        }
        public void LookAt(Vector3 pos, Vector3 target)
        {
            Vector3 f = target - pos;
            if (f.LengthSquared() < 1e-8f) f = Vector3.Forward;
            f.Normalize();
            Vector3 up = Math.Abs(f.Y) > 0.999f ? Vector3.Forward : Vector3.Up;
            World = Matrix.CreateWorld(pos, f, up);
        }
    }

    public sealed class Player
    {
        public const float BASE_FOV = 68;
        static readonly float[] EYE = { 1.7f, 1.08f, 0.42f };
        static readonly float[] SWAY = { 0.0021f, 0.0011f, 0.00042f };
        static readonly float[] SPEED = { 4.2f, 2.1f, 0.8f };
        const float RADIUS = 0.4f;                 // body radius
        const float SWIM_Y = -1.35f;               // feet height when afloat (water surface is y = 0)
        const float EDGE = 14, X0 = Config.INNER_X0 + EDGE, X1 = Config.INNER_X0 + Config.INNER_SIZE - EDGE, Z0 = Config.INNER_Z0 + EDGE, Z1 = Config.INNER_Z0 + Config.INNER_SIZE - EDGE;
        const float DECK = 2.7f;                   // bridge half-width
        static readonly List<Solid> PIERS = MakePiers();
        static List<Solid> MakePiers()
        {
            List<Solid> l = new List<Solid>();
            for (int i = 0; i < 5; i++)
            {
                float z = Config.BRIDGE_Z0 + 8 + i * (Config.BRIDGE_Z1 - Config.BRIDGE_Z0 - 16) / 4;
                l.Add(Solid.Box(Config.BRIDGE_X - 4.8f, Config.BRIDGE_X + 4.8f, z - 1.3f, z + 1.3f));
            }
            return l;
        }

        public readonly Camera Cam = new Camera();
        readonly IList<Solid> solids; readonly IAudio audio; readonly List<Soldier> soldiers; readonly Pad pad; readonly KeyMouse km;
        public Vector3 Pos;
        public bool OnBridge, Swimming, Enabled, Scoped, Holding, Sprinting; public float Depth;
        public bool AlwaysSteady;          // no scope sway (used by the tests and the visual check)
        public float Yaw, Pitch = -0.1f, Eye = EYE[0], Kick, KickYaw, Breath = 1, Exhausted, Zoom = 8, Bob, MoveAmt, AimSlow = 1, Shake, T;
        public int Stance;
        public Action OnEdge, OnHeartbeat;
        bool padSprint; float turnT, stepAcc, beatT, swayX, swayY, lookT;

        public Player(IList<Solid> solids, IAudio audio, List<Soldier> soldiers, Pad pad, KeyMouse km)
        {
            this.solids = solids; this.audio = audio; this.soldiers = soldiers; this.pad = pad; this.km = km;
            Pos = new Vector3(Config.SPAWN_X, Terrain.GroundAt(Config.SPAWN_X, Config.SPAWN_Z), Config.SPAWN_Z);
            Yaw = M.Atan2(Config.SPAWN_X, Config.SPAWN_Z);
            T = M.Rand() * 100;
        }

        public Vector3 Forward { get { return Cam.Forward; } }

        // Keep the player out of walls, trees, boulders, tents and living soldiers, on the bridge deck, and inside the valley.
        void Collide()
        {
            for (int pass = 0; pass < 2; pass++) if (!Collision.Resolve(ref Pos, RADIUS, solids)) break;
            foreach (Soldier s in soldiers)
            {
                if (!s.Alive || Math.Abs(s.Pos.Y - Pos.Y) > 1.6f) continue;
                float dx = Pos.X - s.Pos.X, dz = Pos.Z - s.Pos.Z, d = M.Hypot(dx, dz), r = RADIUS + 0.32f;
                if (d < r && d > 1e-4f) { Pos.X = s.Pos.X + dx / d * r; Pos.Z = s.Pos.Z + dz / d * r; }
            }
            // bridge: walk on from either bank, parapets keep you on the deck, swim under the arches
            float bx = Config.BRIDGE_X, by = Config.BRIDGE_Y;
            float g = Terrain.GroundAt(Pos.X, Pos.Z);
            if (Pos.Z > Config.BRIDGE_Z0 && Pos.Z < Config.BRIDGE_Z1 && Math.Abs(Pos.X - bx) < DECK)
            {
                if (!OnBridge)
                {
                    if (g > by - 0.6f) OnBridge = true;
                    else if (g > 0.4f) Pos.X = bx + (Pos.X < bx ? -1 : 1) * (DECK + 0.01f);     // no headroom under the deck here
                }
            }
            else OnBridge = false;
            if (OnBridge)
            {
                if (g < by + 0.9f) Pos.X = M.Clamp(Pos.X, bx - 2.1f, bx + 2.1f);    // between the parapets
            }
            else if (g < by - 0.5f) Collision.Resolve(ref Pos, RADIUS, PIERS);
            // edge of the playable valley
            if (Pos.X < X0 || Pos.X > X1 || Pos.Z < Z0 || Pos.Z > Z1)
            {
                Pos.X = M.Clamp(Pos.X, X0, X1); Pos.Z = M.Clamp(Pos.Z, Z0, Z1);
                if (OnEdge != null) OnEdge();
            }
        }

        public void ToggleStance(int which) { if (!Swimming) Stance = Stance == which ? 0 : which; }

        public void Update(float dt)
        {
            T += dt;
            float lsx = 0, lsy = 0, rsx = 0, rsy = 0;
            if (pad.Connected && Enabled) { pad.Stick(0, out lsx, out lsy); pad.Stick(1, out rsx, out rsy); }
            if (Enabled)
            {
                float fovK = Cam.Fov / BASE_FOV;
                // mouse look
                if (km.MouseDX != 0 || km.MouseDY != 0)
                {
                    float k = 0.0021f * fovK;
                    Yaw -= km.MouseDX * k;
                    Pitch = M.Clamp(Pitch - km.MouseDY * k, -1.35f, 1.35f);
                }
                // keyboard look: arrows, easing in from a slow start so taps make fine corrections through the scope
                int lx = (km.IsDown(K.Left) ? 1 : 0) - (km.IsDown(K.Right) ? 1 : 0), ly = (km.IsDown(K.Up) ? 1 : 0) - (km.IsDown(K.Down) ? 1 : 0);
                if (lx != 0 || ly != 0)
                {
                    lookT += dt;
                    float rate = (0.2f + 0.8f * Math.Min(1, lookT / 0.6f)) * 1.7f * fovK * dt;
                    Yaw += lx * rate;
                    Pitch = M.Clamp(Pitch + ly * rate, -1.35f, 1.35f);
                }
                else lookT = 0;
                // controller look: squared response for fine aim, a turn boost when the stick is held hard over (hip only),
                // slower through the scope and slower again while the reticle is on a target (aim friction)
                float rm = M.Hypot(rsx, rsy);
                if (rm > 0)
                {
                    turnT = rm > 0.92f && !Scoped ? turnT + dt : 0;
                    float boost = 1 + Math.Min(1, turnT / 0.45f) * 0.7f;
                    float zoomK = Scoped ? M.Pow(fovK, 0.82f) * 0.8f : 1;
                    float rate = 2.5f * rm * boost * zoomK * AimSlow * dt;      // rm twice over: the curve is the stick squared
                    Yaw -= rsx * rate;
                    Pitch = M.Clamp(Pitch - rsy * rate * 0.72f, -1.35f, 1.35f);
                }
                else turnT = 0;

                float f = (km.IsDown(K.W) ? 1 : 0) - (km.IsDown(K.S) ? 1 : 0), s = (km.IsDown(K.D) ? 1 : 0) - (km.IsDown(K.A) ? 1 : 0), analog = 1;
                if (f == 0 && s == 0 && (lsx != 0 || lsy != 0)) { f = -lsy; s = lsx; analog = Math.Min(1, M.Hypot(f, s)); }
                if (pad.Pressed(Btn.L3)) padSprint = true;                     // click the left stick to sprint
                if (analog < 0.6f || f <= 0) padSprint = false;
                bool sprint = (km.IsDown(K.Shift) || padSprint) && !Scoped && Stance == 0 && f > 0;
                float wet = M.Clamp((Depth - 0.3f) / 1.0f);                     // wading slows you, swimming more so
                float sp = SPEED[Stance] * (sprint && wet == 0 ? 1.6f : 1) * (Scoped ? 0.35f : 1) * (1 - 0.58f * wet) * analog;
                float l = M.Hypot(f, s);
                if (l > 0)
                {
                    f /= l; s /= l;
                    float sy = M.Sin(Yaw), cy = M.Cos(Yaw);
                    Pos.X += (-sy * f + cy * s) * sp * dt;
                    Pos.Z += (-cy * f - sy * s) * sp * dt;
                    Bob += sp * dt * 1.9f;
                    stepAcc += sp * dt;
                    if (stepAcc > 2.2f) { stepAcc = 0; if (!Swimming) audio.Step(); }
                }
                MoveAmt += ((l > 0 ? sp / 4.2f : 0) - MoveAmt) * Math.Min(1, dt * 8);
                Sprinting = sprint && wet == 0 && l > 0;
                Collide();
                // breath
                Holding = Scoped && (km.IsDown(K.Shift) || pad.Down(Btn.LB)) && Exhausted <= 0 && Breath > 0;
                if (Holding)
                {
                    Breath -= dt / 6.5f;
                    beatT -= dt;
                    if (beatT <= 0) { beatT = 0.85f; audio.Heartbeat(); if (OnHeartbeat != null) OnHeartbeat(); }
                    if (Breath <= 0) { Breath = 0; Exhausted = 3.2f; }
                }
                else { Breath = Math.Min(1, Breath + dt / 5); beatT = 0; }
                Exhausted = Math.Max(0, Exhausted - dt);
            }
            else MoveAmt *= 0.9f;

            // footing: ground, bridge deck, or afloat in the river
            float g = Terrain.GroundAt(Pos.X, Pos.Z);
            Depth = OnBridge ? 0 : Math.Max(0, -g);
            Swimming = !OnBridge && g < SWIM_Y;
            if (Swimming) Stance = 0;
            float footY = OnBridge ? Math.Max(g, Config.BRIDGE_Y) : Math.Max(g, SWIM_Y);
            float dy = footY - Pos.Y;
            Pos.Y = Math.Abs(dy) > 1.5f ? footY : Pos.Y + dy * Math.Min(1, dt * 16);
            Eye += (EYE[Stance] - Eye) * Math.Min(1, dt * 7);
            float bobY = M.Sin(Bob * 2) * 0.035f * MoveAmt + (Swimming ? M.Sin(T * 1.7f) * 0.05f : 0);
            float bobX = M.Cos(Bob) * 0.02f * MoveAmt;

            // scope sway: slow figure-eight plus tremor, calmer when crouched or prone or holding breath
            float amp = SWAY[Stance] * (Scoped ? 1 : 0.25f) * (1 + MoveAmt * 4);
            if (Holding) amp *= 0.12f; else if (Exhausted > 0) amp *= 2.6f;
            if (AlwaysSteady) amp *= 0.03f;
            float t = T;
            float tx = (M.Sin(t * 0.9f) + 0.5f * M.Sin(t * 2.3f + 1.7f) + 0.18f * M.Sin(t * 7.1f)) * amp;
            float ty = (M.Sin(t * 1.3f + 0.6f) * M.Cos(t * 0.45f) + 0.4f * M.Sin(t * 2.9f) + 0.18f * M.Sin(t * 6.3f + 2.0f)) * amp;
            swayX += (tx - swayX) * Math.Min(1, dt * 10);
            swayY += (ty - swayY) * Math.Min(1, dt * 10);

            Kick *= M.Exp(-dt * 5.5f); KickYaw *= M.Exp(-dt * 5.5f);
            Shake *= M.Exp(-dt * 4.5f);
            float shx = Shake * M.Sin(T * 61) * 0.02f, shy = Shake * M.Sin(T * 47 + 1.3f) * 0.02f;
            Cam.SetEuler(new Vector3(Pos.X, Pos.Y + Eye + bobY, Pos.Z), Pitch + swayY + Kick + shy, Yaw + swayX + KickYaw + bobX * 0.2f + shx, shx * 0.5f);
        }
    }
}
