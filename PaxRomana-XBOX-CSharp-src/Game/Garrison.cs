// The garrison: a German infantryman of the Second World War (field-grey tunic, steel helmet, jackboots, belt kit
// and a slung rifle; the officer wears a peaked cap), built from primitives and animated procedurally, with
// head / torso / leg hit volumes, and the garrison's patrols.
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace PaxRomana
{
    public sealed class Route
    {
        readonly float[][] pts; readonly float[] cum;
        public readonly bool Loop; public readonly float Total; readonly float fixedY; readonly bool hasY;
        public float X, Z, Tx, Tz = 1;       // result of At()

        public Route(float[][] p, bool loop = false, float y = float.NaN)
        {
            Loop = loop; hasY = !float.IsNaN(y); fixedY = y;
            if (loop) { pts = new float[p.Length + 1][]; Array.Copy(p, pts, p.Length); pts[p.Length] = p[0]; } else pts = p;
            cum = new float[pts.Length];
            for (int i = 1; i < pts.Length; i++) cum[i] = cum[i - 1] + M.Hypot(pts[i][0] - pts[i - 1][0], pts[i][1] - pts[i - 1][1]);
            Total = cum[cum.Length - 1];
        }

        public void At(float s)
        {
            s = Loop ? ((s % Total) + Total) % Total : M.Clamp(s, 0, Total);
            int i = 1;
            while (i < cum.Length - 1 && cum[i] < s) i++;
            float[] a = pts[i - 1], b = pts[i]; float len = cum[i] - cum[i - 1]; if (len == 0) len = 1;
            float t = (s - cum[i - 1]) / len;
            X = a[0] + (b[0] - a[0]) * t; Z = a[1] + (b[1] - a[1]) * t;
            Tx = (b[0] - a[0]) / len; Tz = (b[1] - a[1]) / len;
        }
        public float YAt(float x, float z) { return hasY ? fixedY : Terrain.GroundAt(x, z); }
    }

    public struct SoldierHit { public bool Hit, Head; public float T; }

    public sealed class Soldier
    {
        // parts drawn per soldier, in this order
        public const int BODY = 0, HEAD = 1, HELMET = 2, ARM_L = 3, ARM_R = 4, KIT = 5, RIFLE = 6, LEG_L = 7, LEG_R = 8, PARTS = 9;

        public Route Route; public float S, Lat, WalkSpeed; public bool Hold, Officer; public int Dir = 1;
        public bool Alive = true, Moving; public float AlertT, PauseT, Phase, Seed, Yaw, TargetYaw, DeadT;
        public Vector3 Pos, Vel, FallDir, FallAxis = Vector3.UnitX;
        // pose
        float bodyY = 0.95f, bodyRx, headRy, headRx, legL, legR, armLx = -0.25f, armLz, armRx = -0.2f, armRz, rifleRz, kitRx;
        // a helmet knocked off by a head shot flies on its own
        public bool HelmetOff; Vector3 helmetPos, helmetV, helmetSpin, helmetRot; float helmetFloor; bool helmetFlying;

        public Soldier(Route route, float s = 0, float lat = 0, bool hold = false, float face = 0, bool centurion = false, float speed = 1.25f)
        {
            Route = route; S = s; Lat = lat; Hold = hold; WalkSpeed = speed; Officer = centurion;
            PauseT = hold ? 0 : M.Rand() * 2; Phase = M.Rand() * 6; Seed = M.Rand() * 100; Yaw = face; TargetYaw = face;
            if (route != null) Place(0);
        }

        void Place(float dt)
        {
            Route.At(S);
            float nx = Route.X + Route.Tz * Lat, nz = Route.Z - Route.Tx * Lat;
            float ny = Route.YAt(nx, nz);
            if (dt > 0) Vel = new Vector3((nx - Pos.X) / dt, 0, (nz - Pos.Z) / dt); else Vel = Vector3.Zero;
            Pos = new Vector3(nx, ny, nz);
            if (Moving) TargetYaw = M.Atan2(Route.Tx * Dir, Route.Tz * Dir);
        }

        public void Alert(float t) { if (Alive) { AlertT = Math.Max(AlertT, t); PauseT = Math.Min(PauseT, 0.25f + M.Rand() * 0.5f); } }

        public void Update(float dt, float time)
        {
            if (!Alive) { Dead(dt); return; }
            AlertT = Math.Max(0, AlertT - dt);
            bool alerted = AlertT > 0;
            Moving = false;
            if (Route != null && (!Hold || alerted))
            {
                if (PauseT > 0) PauseT -= dt;
                else
                {
                    Moving = true;
                    float sp = alerted ? 3.3f + (Seed % 1) * 0.8f : WalkSpeed;
                    S += Dir * sp * dt;
                    if (Route.Loop) S = (S + Route.Total) % Route.Total;
                    else if (S <= 0 || S >= Route.Total)
                    {
                        S = M.Clamp(S, 0, Route.Total);
                        Dir *= -1;
                        PauseT = alerted ? 0.2f + M.Rand() * 0.6f : 2 + M.Rand() * 5;
                    }
                }
                Place(dt);
            }
            else Vel = Vector3.Zero;
            if (!Moving) Vel = Vector3.Zero;

            float dy = TargetYaw - Yaw;
            dy = M.Atan2(M.Sin(dy), M.Cos(dy));
            Yaw += dy * Math.Min(1, dt * 5);

            float run = alerted ? 1.6f : 1;
            if (Moving)
            {
                Phase += dt * (alerted ? 11 : 5.6f);
                float sw = M.Sin(Phase) * 0.5f * run;
                legL = sw; legR = -sw;
                armLx = -sw * 0.25f - 0.25f; armRx = sw * 0.25f - 0.2f;
                bodyY = 0.95f + Math.Abs(M.Cos(Phase)) * 0.03f;
                bodyRx = alerted ? 0.16f : 0.03f;
                headRy *= 0.9f;
            }
            else
            {
                legL *= 0.85f; legR *= 0.85f;
                armLx = -0.25f; armRx = -0.2f;
                bodyY = 0.95f + M.Sin(time * 1.2f + Seed) * 0.004f;
                bodyRx = 0;
                float look = alerted ? M.Sin(time * 3.1f + Seed) * 0.9f : M.Sin(time * 0.45f + Seed) * 0.6f + M.Sin(time * 0.17f + Seed * 2) * 0.3f;
                headRy += (look - headRy) * Math.Min(1, dt * 3);
            }
        }

        void Dead(float dt)
        {
            if (helmetFlying)
            {
                helmetV.Y -= 9.81f * dt;
                helmetPos += helmetV * dt;
                helmetRot.X += helmetSpin.X * dt; helmetRot.Z += helmetSpin.Z * dt;
                if (helmetPos.Y < helmetFloor) { helmetPos.Y = helmetFloor; helmetFlying = false; }
            }
            if (DeadT >= 1.3f) return;
            DeadT = Math.Min(1.3f, DeadT + dt);
            float t = Math.Min(1, DeadT / 0.85f), e = t * t;
            legL = e * 0.5f; legR = -e * 0.25f;
            armLz = -e * 1.1f; armRz = e * 0.9f; armRx = -0.2f - e * 0.8f;
            rifleRz = -e * 1.2f; kitRx = e * 0.5f;
            headRx = -e * 0.4f;
        }

        // First hit along p0 -> p1. lead (seconds) moves the soldier along his velocity, for trajectory prediction.
        public SoldierHit HitTest(Vector3 p0, Vector3 p1, float lead = 0)
        {
            SoldierHit best = new SoldierHit();
            if (!Alive) return best;
            float px = Pos.X + Vel.X * lead, py = Pos.Y, pz = Pos.Z + Vel.Z * lead;
            float d, t;
            Vector3 a = new Vector3(px, py + 1.67f, pz);
            SegSeg(p0, p1, a, a, out d, out t);
            if (d < 0.155f) { best.Hit = true; best.Head = true; best.T = t; }
            SegSeg(p0, p1, new Vector3(px, py + 0.98f, pz), new Vector3(px, py + 1.46f, pz), out d, out t);
            if (d < 0.25f && (!best.Hit || t < best.T - 0.02f)) { best.Hit = true; best.Head = false; best.T = t; }
            SegSeg(p0, p1, new Vector3(px, py + 0.1f, pz), new Vector3(px, py + 0.95f, pz), out d, out t);
            if (d < 0.17f && !best.Hit) { best.Hit = true; best.Head = false; best.T = t; }
            return best;
        }

        // closest approach between segment p0->p1 and segment q0->q1: distance, and position along p (0..1)
        static void SegSeg(Vector3 p0, Vector3 p1, Vector3 q0, Vector3 q1, out float dist, out float s)
        {
            Vector3 d1 = p1 - p0, d2 = q1 - q0, r = p0 - q0;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r), t;
            if (a < 1e-12f) { s = 0; dist = (p0 - q0).Length(); return; }
            if (e < 1e-9f) { t = 0; s = M.Clamp(-Vector3.Dot(d1, r) / a); }
            else
            {
                float c = Vector3.Dot(d1, r), b = Vector3.Dot(d1, d2), den = a * e - b * b;
                s = den > 1e-9f ? M.Clamp((b * f - c * e) / den) : 0;
                t = (b * s + f) / e;
                if (t < 0) { t = 0; s = M.Clamp(-c / a); }
                else if (t > 1) { t = 1; s = M.Clamp((b - c) / a); }
            }
            dist = ((p0 + d1 * s) - (q0 + d2 * t)).Length();
        }

        public void Kill(Vector3 dir, bool head)
        {
            Alive = false; Vel = Vector3.Zero;
            FallDir = new Vector3(dir.X, 0, dir.Z);
            if (FallDir.LengthSquared() < 1e-6f) FallDir = new Vector3(0, 0, 1);
            FallDir.Normalize();
            FallAxis = Vector3.Normalize(Vector3.Cross(Vector3.Up, FallDir));
            if (head)
            {
                // the helmet is knocked clean off
                Matrix[] m = new Matrix[PARTS];
                Matrices(m);
                helmetPos = m[HEAD].Translation; helmetRot = new Vector3(0, Yaw, 0);
                helmetFloor = Pos.Y - 0.05f;
                helmetV = new Vector3(dir.X * 3 + (M.Rand() - 0.5f) * 2, 4.2f, dir.Z * 3 + (M.Rand() - 0.5f) * 2);
                helmetSpin = new Vector3(8 + M.Rand() * 6, 0, 5);
                HelmetOff = true; helmetFlying = true;
            }
        }

        // World matrices of the nine parts.
        public void Matrices(Matrix[] m)
        {
            Matrix root;
            if (Alive) root = Matrix.CreateRotationY(Yaw) * Matrix.CreateTranslation(Pos);
            else
            {
                float t = Math.Min(1, DeadT / 0.85f), e = t * t;
                Vector3 p = Pos + FallDir * (e * 0.35f);
                p.Y += M.Sin(e * M.PI) * 0.05f + e * 0.14f;
                root = Matrix.CreateRotationY(Yaw) * Matrix.CreateFromAxisAngle(FallAxis, e * 1.5f) * Matrix.CreateTranslation(p);
            }
            Matrix body = Matrix.CreateRotationX(bodyRx) * Matrix.CreateTranslation(0, bodyY, 0) * root;
            Matrix head = Matrix.CreateRotationY(headRy) * Matrix.CreateRotationX(headRx) * Matrix.CreateTranslation(0, 0.62f, 0) * body;
            m[BODY] = body; m[HEAD] = head;
            m[HELMET] = HelmetOff
                ? Matrix.CreateRotationZ(helmetRot.Z) * Matrix.CreateRotationY(helmetRot.Y) * Matrix.CreateRotationX(helmetRot.X) * Matrix.CreateTranslation(helmetPos)
                : head;
            m[ARM_L] = Matrix.CreateRotationZ(armLz) * Matrix.CreateRotationX(armLx) * Matrix.CreateTranslation(-0.265f, 0.5f, 0) * body;
            m[ARM_R] = Matrix.CreateRotationZ(armRz) * Matrix.CreateRotationX(armRx) * Matrix.CreateTranslation(0.265f, 0.5f, 0) * body;
            m[KIT] = Matrix.CreateRotationX(-kitRx * 0.3f) * body;                                                         // belt kit, worn on the back of the hips
            m[RIFLE] = Matrix.CreateRotationZ(rifleRz) * Matrix.CreateTranslation(0.2f, -0.42f, -0.17f) * body;      // slung behind the right shoulder
            m[LEG_L] = Matrix.CreateRotationX(legL) * Matrix.CreateTranslation(-0.09f, 0.93f, 0) * root;
            m[LEG_R] = Matrix.CreateRotationX(legR) * Matrix.CreateTranslation(0.09f, 0.93f, 0) * root;
        }

        // ---- geometry: one vertex-coloured mesh per part; [0] soldier, [1] officer where they differ ----
        const int SKIN = 0xc08e70, TUNIC = 0x4f5a4a, TROUSER = 0x555d50, LEATHER = 0x16130f, STEEL = 0x3f4a40, BUCKLE = 0xa8a495, WOOD = 0x4a2f1b, GUNMETAL = 0x23262a,
            KIT_C = 0x5d5a44, COLLAR = 0x2c3a2e, SILVER = 0xb9bcc0, CAP_BAND = 0x24352a;

        public static Geo[][] BuildParts()
        {
            Geo[][] g = new Geo[PARTS][];
            g[BODY] = new[] { Body(false), Body(true) };
            g[HEAD] = One(new Geo().Add(Geo.Sphere(0.105f, 14, 10).Translate(0, 0.1f, 0).Paint(SKIN)).Add(Geo.Box(0.03f, 0.04f, 0.03f).Translate(0, 0.085f, 0.105f).Paint(SKIN)));
            g[HELMET] = new[] { Helmet(), Cap() };
            Geo arm = new Geo()
                .Add(Geo.Cylinder(0.066f, 0.058f, 0.6f, 8).Translate(0, -0.28f, 0).Paint(TUNIC))
                .Add(Geo.Cylinder(0.06f, 0.06f, 0.05f, 8).Translate(0, -0.56f, 0).Paint(COLLAR))
                .Add(Geo.Sphere(0.046f, 8, 6).Translate(0, -0.63f, 0).Paint(SKIN));
            g[ARM_L] = One(arm); g[ARM_R] = One(arm);
            // belt kit on the back of the hips: gas-mask canister, bread bag, canteen and a rolled shelter quarter
            g[KIT] = One(new Geo()
                .Add(Geo.Cylinder(0.062f, 0.062f, 0.27f, 10).RotateZ(M.HALF_PI + 0.25f).Translate(0.02f, 0.0f, -0.2f).Paint(STEEL))
                .Add(Geo.Box(0.2f, 0.19f, 0.07f).Translate(-0.14f, -0.14f, -0.17f).Paint(KIT_C))
                .Add(Geo.Sphere(0.07f, 8, 6).Scale(1, 1.25f, 0.6f).Translate(0.14f, -0.15f, -0.18f).Paint(0x3a3324))
                .Add(Geo.Cylinder(0.05f, 0.05f, 0.34f, 8).RotateZ(M.HALF_PI).Translate(0, 0.2f, -0.2f).Paint(KIT_C))
                .Add(Geo.Box(0.07f, 0.1f, 0.05f).Translate(-0.12f, 0.03f, 0.185f).Paint(LEATHER))
                .Add(Geo.Box(0.07f, 0.1f, 0.05f).Translate(0.12f, 0.03f, 0.185f).Paint(LEATHER)));
            // bolt-action rifle, muzzle up, tilted a little across the back
            Geo rifle = new Geo()
                .Add(Geo.Box(0.05f, 0.34f, 0.11f).Translate(0, 0.17f, -0.012f).Paint(WOOD))            // butt
                .Add(Geo.Box(0.045f, 0.5f, 0.055f).Translate(0, 0.58f, 0).Paint(WOOD))                  // fore-end
                .Add(Geo.Cylinder(0.011f, 0.013f, 0.72f, 6).Translate(0, 0.74f, 0.02f).Paint(GUNMETAL))  // barrel
                .Add(Geo.Box(0.03f, 0.12f, 0.05f).Translate(0, 0.38f, 0.01f).Paint(GUNMETAL))           // action
                .Add(Geo.Cylinder(0.008f, 0.008f, 0.07f, 5).RotateZ(M.HALF_PI).Translate(0.04f, 0.4f, 0.01f).Paint(GUNMETAL))
                .Add(Geo.Box(0.012f, 0.86f, 0.03f).Translate(0, 0.52f, 0.06f).Paint(LEATHER));          // sling
            g[RIFLE] = One(rifle.RotateZ(0.14f));
            Geo leg = new Geo()
                .Add(Geo.Cylinder(0.082f, 0.062f, 0.5f, 8).Translate(0, -0.26f, 0).Paint(TROUSER))
                .Add(Geo.Cylinder(0.064f, 0.06f, 0.4f, 8).Translate(0, -0.68f, 0).Paint(LEATHER))        // jackboot
                .Add(Geo.Box(0.09f, 0.07f, 0.25f).Translate(0, -0.895f, 0.05f).Paint(LEATHER));
            g[LEG_L] = One(leg); g[LEG_R] = One(leg);
            return g;
        }
        static Geo[] One(Geo g) { return new[] { g, g }; }

        static Geo Body(bool officer)
        {
            Geo p = new Geo();
            p.Add(Geo.Cylinder(0.2f, 0.215f, 0.26f, 12).Scale(1, 1, 0.8f).Translate(0, -0.1f, 0).Paint(TUNIC));          // tunic skirt
            p.Add(Geo.Cylinder(0.215f, 0.195f, 0.5f, 14).Scale(1, 1, 0.76f).Translate(0, 0.29f, 0).Paint(TUNIC));          // chest
            foreach (int s in new[] { -1, 1 })
            {
                p.Add(Geo.Sphere(0.1f).Scale(1.1f, 0.55f, 1.0f).Translate(s * 0.215f, 0.5f, 0).Paint(TUNIC));                // shoulder
                p.Add(Geo.Box(0.1f, 0.012f, 0.05f).Translate(s * 0.2f, 0.553f, 0).Paint(officer ? SILVER : COLLAR));        // shoulder strap
                p.Add(Geo.Box(0.075f, 0.085f, 0.012f).Translate(s * 0.095f, 0.36f, 0.152f).Paint(COLLAR));                  // breast pocket flap
                p.Add(Geo.Box(0.03f, 0.5f, 0.012f).RotateZ(-s * 0.2f).Translate(s * 0.1f, 0.3f, 0.158f).Paint(LEATHER));    // Y-strap
            }
            p.Add(Geo.Cylinder(0.218f, 0.218f, 0.055f, 14).Scale(1, 1, 0.8f).Translate(0, 0.045f, 0).Paint(LEATHER));      // belt
            p.Add(Geo.Box(0.06f, 0.05f, 0.012f).Translate(0, 0.045f, 0.176f).Paint(BUCKLE));
            for (int i = 0; i < 5; i++) p.Add(Geo.Sphere(0.011f, 5, 4).Translate(0, 0.12f + i * 0.085f, 0.158f).Paint(BUCKLE));   // buttons
            p.Add(Geo.Cylinder(0.05f, 0.058f, 0.09f, 8).Translate(0, 0.585f, 0).Paint(SKIN));                               // neck
            p.Add(Geo.Torus(0.066f, 0.024f, 6, 12).RotateX(M.HALF_PI).Translate(0, 0.56f, 0).Paint(COLLAR));                 // collar
            if (officer)
            {
                p.Add(Geo.Box(0.07f, 0.11f, 0.04f).Translate(0.2f, -0.02f, 0.06f).Paint(LEATHER));                           // pistol holster
                p.Add(Geo.Box(0.028f, 0.5f, 0.012f).RotateZ(0.62f).Translate(0, 0.3f, 0.162f).Paint(LEATHER));                // cross strap
                p.Add(Geo.Box(0.03f, 0.03f, 0.012f).Translate(0, 0.52f, 0.165f).Paint(SILVER));                             // medal at the throat
            }
            return p;
        }

        // The steel helmet: a round crown with a skirt flared over the ears and the neck, and a short peak.
        static Geo Helmet()
        {
            return new Geo()
                .Add(Geo.Sphere(0.128f, 16, 8, 0, M.TAU, 0, M.PI * 0.5f).Scale(1, 0.95f, 1.08f).Translate(0, 0.118f, -0.004f).Paint(STEEL))
                .Add(Geo.Cylinder(0.129f, 0.158f, 0.06f, 16, true, M.HALF_PI, M.PI).Scale(1, 1, 1.08f).Translate(0, 0.09f, -0.004f).Paint(STEEL))     // skirt: sides and back
                .Add(Geo.Cylinder(0.129f, 0.142f, 0.028f, 16, true, -M.HALF_PI, M.PI).Scale(1, 1, 1.08f).Translate(0, 0.106f, -0.004f).Paint(STEEL))   // peak: front
                .Add(Geo.Box(0.012f, 0.1f, 0.012f).Translate(0.1f, 0.03f, 0.03f).Paint(LEATHER))
                .Add(Geo.Box(0.012f, 0.1f, 0.012f).Translate(-0.1f, 0.03f, 0.03f).Paint(LEATHER));
        }

        // The officer's peaked cap.
        static Geo Cap()
        {
            return new Geo()
                .Add(Geo.Cylinder(0.118f, 0.112f, 0.05f, 16).Translate(0, 0.165f, 0).Paint(CAP_BAND))
                .Add(Geo.Cylinder(0.158f, 0.12f, 0.055f, 16).Scale(1, 1, 1.05f).RotateX(-0.16f).Translate(0, 0.215f, 0.004f).Paint(TUNIC))
                .Add(Geo.Cylinder(0.158f, 0.158f, 0.012f, 16).Scale(1, 1, 1.05f).RotateX(-0.16f).Translate(0, 0.247f, 0.0f).Paint(TUNIC))
                .Add(Geo.Cylinder(0.13f, 0.13f, 0.01f, 12, false, -0.9f, 1.8f).Scale(1, 1, 1.25f).RotateX(0.3f).Translate(0, 0.142f, 0.012f).Paint(LEATHER))   // visor
                .Add(Geo.Box(0.03f, 0.025f, 0.01f).Translate(0, 0.225f, 0.13f).Paint(SILVER));
        }
    }

    // The garrison: patrol routes on the wall-walks, the keep roof, the gate, the road, the bridge and the camp.
    public sealed class Garrison
    {
        public readonly List<Soldier> Soldiers = new List<Soldier>();
        public float AlertLevel, AlarmT; public bool Alarm; public Action OnAlarm; public int Total;

        static float[][] Pts(params float[] xz) { float[][] p = new float[xz.Length / 2][]; for (int i = 0; i < p.Length; i++) p[i] = new[] { xz[i * 2], xz[i * 2 + 1] }; return p; }
        static float[][] Slice(float[][] a, int from, int to) { float[][] r = new float[to - from][]; Array.Copy(a, from, r, 0, to - from); return r; }

        Soldier Add(Route route, float s = 0, float lat = 0, bool hold = false, float face = 0, bool centurion = false, float speed = 1.25f)
        {
            Soldier so = new Soldier(route, s, lat, hold, face, centurion, speed);
            Soldiers.Add(so);
            return so;
        }

        public Garrison(Castle castle)
        {
            float W = Config.WALK_Y, CX = Config.CAMP_X, CZ = Config.CAMP_Z, CR = Config.CAMP_R;
            // wall-walks
            Add(new Route(Pts(-36, 34.5f, -17.5f, 34.5f), false, W), 6);
            Add(new Route(Pts(17.5f, 34.5f, 36, 34.5f), false, W), 2);
            Add(new Route(Pts(17.5f, 34.5f, 36, 34.5f), false, W), 16);
            Add(new Route(Pts(15, 45.5f, 39, 45.5f)), 4);                                    // foot of the south wall
            Add(new Route(Slice(Config.ROAD, 4, Config.ROAD.Length)), 10);                   // camp road
            Add(new Route(Pts(-36, -34.5f, -7.5f, -34.5f), false, W), 12);
            Add(new Route(Pts(7.5f, -34.5f, 36, -34.5f), false, W), 3);
            // gatehouse roof and ground guards (they only move once alarmed)
            float gt = castle.GateTop;
            Add(new Route(Pts(-4, 38.4f, -0.8f, 38.4f), false, gt), 1.28f, 0, true, 0);
            Add(new Route(Pts(0.8f, 38.4f, 4, 38.4f), false, gt), 1.97f, 0, true, 0);
            Add(new Route(Pts(-3.6f, 42, -3.6f, 47)), 0, 0, true, 0);
            Add(new Route(Pts(3.6f, 42, 3.6f, 47)), 0, 0, true, 0);
            // keep roof
            float kx = castle.KeepX, kz = castle.KeepZ, o = castle.KeepHalf - 1.6f;
            Route loop = new Route(Pts(kx - o, kz + o, kx + o, kz + o, kx + o, kz - o, kx - o, kz - o), true, castle.KeepTop);
            Add(loop, 3, 0, false, 0, false, 1.0f); Add(loop, 3 + loop.Total / 2, 0, false, 0, false, 1.0f);
            // road patrol (pair)
            Route road = new Route(Slice(Config.ROAD, 0, 5));
            Add(road, 30, 0.7f); Add(road, 30, -0.7f);
            // bridge sentry
            Add(new Route(Pts(Config.BRIDGE_X, Config.BRIDGE_Z0 + 14, Config.BRIDGE_X, Config.BRIDGE_Z1 - 14), false, Config.BRIDGE_Y), 10);
            // a section drilling around the camp, the officer at its head
            float[] ring = new float[96];
            for (int i = 0; i < 48; i++) { float a = -(i / 48f) * M.TAU; ring[i * 2] = CX + M.Sin(a) * (CR + 13); ring[i * 2 + 1] = CZ + M.Cos(a) * (CR + 13); }
            Route drill = new Route(Pts(ring), true);
            Add(drill, 104.5f, 0, false, 0, true, 1.35f);
            for (int r = 0; r < 3; r++) foreach (float lat in new[] { -0.75f, 0.75f }) Add(drill, 102 - r * 1.7f, lat, false, 0, false, 1.35f);
            // watchtower lookout
            float tx = CX + 26, tz = CZ + 34;
            Add(new Route(Pts(tx - 1.1f, tz + 1.1f, tx + 1.1f, tz + 1.1f), false, Terrain.GroundAt(tx, tz) + 7.12f), 1, 0, true, 0.3f);
            // camp: two by the fire, one walking the main lane
            Add(new Route(Pts(CX + 7.6f, CZ + 7.5f, CX + 12, CZ + 16)), 0, 0, true, -2.3f);
            Add(new Route(Pts(CX + 4.6f, CZ + 4.6f, CX - 3, CZ + 20)), 0, 0, true, 0.8f);
            Add(new Route(Pts(CX, CZ + 40, CX, CZ - 30)), 20);
            // inside the walls: a sentry crossing the courtyard and the guard outside the vault in the keep
            Add(new Route(Pts(-24, 26, 24, 26)), 14);
            Add(new Route(Pts(4.5f, -6.5f, -3, -6.5f)), 0, 0, true, 0);
            Total = Soldiers.Count;
        }

        public int AliveCount { get { int n = 0; foreach (Soldier s in Soldiers) if (s.Alive) n++; return n; } }

        public void AlertAround(Vector3 p, float radius, float seconds, float raise)
        {
            foreach (Soldier s in Soldiers) if (s.Alive && Vector3.Distance(s.Pos, p) < radius) s.Alert(seconds);
            AlertLevel = Math.Min(100, AlertLevel + raise);
            if (AlertLevel >= 100 && !Alarm)
            {
                Alarm = true; AlarmT = 45;
                foreach (Soldier s in Soldiers) s.Alert(45);
                if (OnAlarm != null) OnAlarm();
            }
        }

        public void Kill(Soldier s, Vector3 dir, bool head)
        {
            s.Kill(dir, head);
            AlertAround(s.Pos, 16, 14, head ? 9 : 14);
        }

        public void Update(float dt, float time)
        {
            foreach (Soldier s in Soldiers) s.Update(dt, time);
            if (Alarm)
            {
                AlarmT -= dt;
                if (AlarmT <= 0) { Alarm = false; AlertLevel = 55; }
            }
            else AlertLevel = Math.Max(0, AlertLevel - dt * 1.6f);
        }
    }
}
