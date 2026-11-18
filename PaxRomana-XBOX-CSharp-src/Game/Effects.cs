// Particles (dust, chips, blood mist, water, smoke, explosions), drifting summer seeds and two flocks of birds over the valley.
// Simulation only: the renderer turns these into camera-facing sprites.
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace PaxRomana
{
    public struct Particle
    {
        public float X, Y, Z, Vx, Vy, Vz, Life, T, S0, S1, R, G, B, A, Grav, Drag;
        public float Size, Alpha;       // current values, filled by Update
    }

    public sealed class Bird { public Vector3 Pos; public float RotY, Flap, A, W, R, Cx, Cz, H, Ph; }
    public sealed class Shockwave { public Vector3 Pos; public float T = 1; public bool Visible; public float Scale, Opacity; }
    sealed class Emitter { public Vector3 Pos; public float Scale, Acc; }

    public sealed class Fx
    {
        public const int MAX = 1600, MOTES = 260; const float MOTE_R = 22;
        public readonly Particle[] P = new Particle[MAX];
        public int Count;
        public readonly Vector3[] Motes = new Vector3[MOTES];
        public bool MotesVisible = true;
        public readonly List<Bird> Birds = new List<Bird>();
        public readonly Shockwave[] Rings = { new Shockwave(), new Shockwave(), new Shockwave() };
        public Vector3 FlashPos; public float FlashT;         // explosion flash (0.4 s)
        readonly List<Emitter> emitters = new List<Emitter>();

        public Fx()
        {
            for (int i = 0; i < MOTES; i++) Motes[i] = new Vector3((M.Rand() - 0.5f) * 2 * MOTE_R, (M.Rand() - 0.5f) * 2 * MOTE_R, (M.Rand() - 0.5f) * 2 * MOTE_R);
            for (int i = 0; i < 16; i++)
            {
                bool flock = i >= 9;
                Birds.Add(new Bird
                {
                    A = M.Rand() * 0.5f + (flock ? 2 : 0), W = (flock ? -0.07f : 0.09f) * (0.9f + M.Rand() * 0.2f), R = (flock ? 120 : 75) + M.Rand() * 14,
                    Cx = flock ? 90 : 5, Cz = flock ? 190 : -5, H = (flock ? 62 : 80) + M.Rand() * 7, Ph = M.Rand() * 6,
                });
            }
        }

        public void Spawn(float x, float y, float z, float vx, float vy, float vz, float life, float s0, float s1, float r, float g, float b, float a, float grav = 0, float drag = 1)
        {
            if (Count >= MAX) { Array.Copy(P, 1, P, 0, MAX - 1); Count = MAX - 1; }
            P[Count++] = new Particle { X = x, Y = y, Z = z, Vx = vx, Vy = vy, Vz = vz, Life = life, S0 = s0, S1 = s1, R = r, G = g, B = b, A = a, Grav = grav, Drag = drag };
        }

        static float R() { return M.Rand(); }

        public void Dust(Vector3 p, Vector3 n, string kind)
        {
            float c0, c1, c2;
            if (kind == "stone") { c0 = 0.62f; c1 = 0.56f; c2 = 0.46f; } else if (kind == "wood") { c0 = 0.4f; c1 = 0.3f; c2 = 0.2f; } else { c0 = 0.52f; c1 = 0.44f; c2 = 0.31f; }
            for (int i = 0; i < 16; i++)
            {
                float sp = 1 + R() * 3.5f, k = 0.8f + R() * 0.4f;
                Spawn(p.X, p.Y, p.Z, n.X * sp + (R() - 0.5f) * 2.4f, n.Y * sp + R() * 2, n.Z * sp + (R() - 0.5f) * 2.4f,
                    1.2f + R() * 1.6f, 0.25f, 1.6f + R() * 1.8f, c0 * k, c1 * k, c2 * k, 0.55f, 1.2f, 2.2f);
            }
            for (int i = 0; i < 8; i++)      // chips
                Spawn(p.X, p.Y, p.Z, n.X * 4 + (R() - 0.5f) * 6, n.Y * 4 + R() * 5, n.Z * 4 + (R() - 0.5f) * 6,
                    0.9f, 0.09f, 0.05f, c0 * 0.7f, c1 * 0.7f, c2 * 0.7f, 1, 9.8f, 0.3f);
        }

        public void Blood(Vector3 p, Vector3 dir)
        {
            for (int i = 0; i < 14; i++)
            {
                float sp = 2 + R() * 5;
                Spawn(p.X, p.Y, p.Z, dir.X * sp + (R() - 0.5f) * 2.5f, dir.Y * sp + (R() - 0.3f) * 2.5f, dir.Z * sp + (R() - 0.5f) * 2.5f,
                    0.5f + R() * 0.5f, 0.1f, 0.5f + R() * 0.5f, 0.32f, 0.015f, 0.01f, 0.75f, 5, 2.5f);
            }
        }

        public void Splash(Vector3 p)
        {
            for (int i = 0; i < 22; i++)
                Spawn(p.X, 0.1f, p.Z, (R() - 0.5f) * 2.5f, 4 + R() * 6, (R() - 0.5f) * 2.5f,
                    0.9f + R() * 0.6f, 0.3f, 1.2f, 0.85f, 0.9f, 0.92f, 0.6f, 9.8f, 0.6f);
        }

        // Fireball, smoke column, debris, a flash of light and a ground shockwave.
        public void Explosion(Vector3 p)
        {
            for (int i = 0; i < 36; i++)
            {
                float a = R() * M.TAU, e = R() * 1.4f, sp = 4 + R() * 13, c = M.Cos(e);
                Spawn(p.X, p.Y + 0.4f, p.Z, M.Cos(a) * c * sp, M.Sin(e) * sp, M.Sin(a) * c * sp,
                    0.45f + R() * 0.5f, 1.2f, 5 + R() * 4, 3.4f, 1.2f + R() * 0.8f, 0.22f, 0.9f, -1.5f, 3.2f);
            }
            for (int i = 0; i < 26; i++)
            {
                float a = R() * M.TAU, sp = 1 + R() * 4.5f, k = 0.1f + R() * 0.1f;
                Spawn(p.X, p.Y + 0.6f, p.Z, M.Cos(a) * sp, 2 + R() * 4, M.Sin(a) * sp,
                    3 + R() * 3.5f, 2, 9 + R() * 6, k, k, k * 1.05f, 0.62f, -0.3f, 1.1f);
            }
            for (int i = 0; i < 26; i++)
            {
                float a = R() * M.TAU, sp = 5 + R() * 16;
                Spawn(p.X, p.Y + 0.3f, p.Z, M.Cos(a) * sp, 5 + R() * 16, M.Sin(a) * sp,
                    1.5f, 0.24f, 0.1f, 0.12f, 0.1f, 0.09f, 1, 9.8f, 0.25f);
            }
            FlashPos = new Vector3(p.X, p.Y + 2.2f, p.Z); FlashT = 0.4f;
            Shockwave ring = Rings[0];
            foreach (Shockwave r in Rings) if (r.T > ring.T) ring = r;
            ring.T = 0; ring.Pos = new Vector3(p.X, p.Y + 0.25f, p.Z); ring.Visible = true;
        }

        public void AddSmoke(Vector3 pos, float scale) { emitters.Add(new Emitter { Pos = pos, Scale = scale, Acc = R() }); }

        public void Clear() { Count = 0; FlashT = 0; foreach (Shockwave r in Rings) { r.Visible = false; r.T = 1; } }

        public void Update(float dt, Vector3 cam, float fov)
        {
            foreach (Emitter e in emitters)
            {
                e.Acc += dt * 5;
                while (e.Acc > 1)
                {
                    e.Acc -= 1;
                    float k = 0.55f + R() * 0.25f;
                    Spawn(e.Pos.X + (R() - 0.5f) * 0.4f, e.Pos.Y, e.Pos.Z + (R() - 0.5f) * 0.4f,
                        0.9f + R() * 0.5f, 1.5f + R() * 0.8f, 0.25f, 9 + R() * 4, 0.7f * e.Scale, 7 * e.Scale, k, k * 0.97f, k * 0.93f, 0.34f, -0.05f, 0.12f);
                }
            }
            int n = 0;
            for (int i = 0; i < Count; i++)
            {
                Particle q = P[i];
                q.T += dt;
                if (q.T >= q.Life) continue;
                float dr = M.Exp(-q.Drag * dt);
                q.Vx *= dr; q.Vz *= dr; q.Vy = q.Vy * dr - q.Grav * dt;
                q.X += q.Vx * dt; q.Y += q.Vy * dt; q.Z += q.Vz * dt;
                float u = q.T / q.Life;
                q.Alpha = q.A * Math.Min(1, u * 8) * (1 - u) * (1 - u);
                q.Size = q.S0 + (q.S1 - q.S0) * M.Sqrt(u);
                P[n++] = q;
            }
            Count = n;

            // explosion flash and shockwave
            FlashT = Math.Max(0, FlashT - dt);
            foreach (Shockwave r in Rings)
            {
                if (!r.Visible) continue;
                r.T += dt / 0.55f;
                if (r.T >= 1) { r.Visible = false; continue; }
                r.Scale = 1 + r.T * 17;
                r.Opacity = 0.55f * (1 - r.T) * (1 - r.T);
            }
            // seeds and pollen on the breeze: a box of motes that stays wrapped around the camera; hidden through the scope, where near ones would fill the glass
            MotesVisible = fov > 30;
            for (int i = 0; i < MOTES; i++)
            {
                Vector3 s = Motes[i];
                s.X += (0.5f + (i % 7) * 0.05f) * dt; s.Y += M.Sin(i * 1.7f + s.X * 0.6f) * 0.12f * dt; s.Z += 0.15f * dt;
                float d = s.X - cam.X; if (d > MOTE_R) s.X -= MOTE_R * 2; else if (d < -MOTE_R) s.X += MOTE_R * 2;
                d = s.Y - cam.Y; if (d > MOTE_R) s.Y -= MOTE_R * 2; else if (d < -MOTE_R) s.Y += MOTE_R * 2;
                d = s.Z - cam.Z; if (d > MOTE_R) s.Z -= MOTE_R * 2; else if (d < -MOTE_R) s.Z += MOTE_R * 2;
                Motes[i] = s;
            }
            foreach (Bird b in Birds)
            {
                b.A += b.W * dt;
                b.Pos = new Vector3(b.Cx + M.Cos(b.A) * b.R, b.H + M.Sin(b.A * 2.3f + b.Ph) * 4, b.Cz + M.Sin(b.A) * b.R * 0.7f);
                b.RotY = -b.A + (b.W > 0 ? M.PI : 0);
                b.Flap = M.Sin(b.Ph + b.A * 40) * 0.8f;
            }
        }
    }
}
