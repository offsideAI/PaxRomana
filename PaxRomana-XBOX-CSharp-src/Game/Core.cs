// Shared constants, maths helpers and deterministic noise.
// Everything under Game/ is written against C# 7.3 and .NET Standard 2.0, because that is what the Xbox (UWP) build can use.
using System;
using Microsoft.Xna.Framework;

namespace PaxRomana
{
    public static class Config
    {
        public const float PLATEAU = 30;                 // height of the castle rock
        public const float WALL_H = 13;                  // curtain wall height
        public const float WALK_Y = PLATEAU + WALL_H;
        public const float SPAWN_X = 120, SPAWN_Z = 412;
        public const float CAMP_X = 170, CAMP_Z = 10, CAMP_R = 52, CAMP_H = 9;
        public const float INNER_X0 = -540, INNER_Z0 = -410, INNER_SIZE = 1200;
        public const int INNER_N = 400;
        public static readonly Vector3 SUN_DIR = Vector3.Normalize(new Vector3(-0.52f, 0.66f, 0.50f));   // high summer sun, early afternoon
        // The U-235 enrichment works beyond the valley: scenery in this level, the setting of the next ones.
        public const float WORKS_X = 450, WORKS_Z = -800, WORKS_H = 18;
        // While true, either level can be chosen from the title and pause menus (D-pad left / right). Set it to
        // false for a release build: Level One then opens only after Level Zero has been won in the same session.
        public static readonly bool DEBUG_MODE = true;
        // true: the garrison flies the historical flag. false: a plain black cross on the white disc, for places
        // where showing the historical symbol is restricted.
        public static readonly bool HISTORICAL_INSIGNIA = true;
        public const float MUZZLE_V = 880;
        public const float GRAVITY = 9.81f;
        public static readonly Vector3 WIND = new Vector3(0.5f, 0, 0.12f);     // lateral acceleration on projectiles (m/s^2)
        public static float RiverZ(float x) { return 210 + 30 * M.Sin(x * 0.006f); }
        public static readonly float[][] ROAD = { new[] { 0f, 42 }, new[] { 8f, 60 }, new[] { 30f, 85 }, new[] { 70f, 108 }, new[] { 115f, 95 }, new[] { 150f, 72 }, new[] { 170f, 63 } };
        public static readonly float[][] ROAD2 = { new[] { 70f, 108 }, new[] { 62f, 150 }, new[] { 55f, 185 }, new[] { 55f, 255 }, new[] { 72f, 300 } };
        public const float BRIDGE_X = 55, BRIDGE_Z0 = 180, BRIDGE_Z1 = 260, BRIDGE_Y = 3.4f;
    }

    public static class M
    {
        public const float PI = 3.14159265358979f, TAU = 6.28318530717959f, HALF_PI = 1.5707963267949f;
        public static readonly Random Rng = new Random();
        public static float Rand() { return (float)Rng.NextDouble(); }

        public static float Sin(float a) { return (float)Math.Sin(a); }
        public static float Cos(float a) { return (float)Math.Cos(a); }
        public static float Tan(float a) { return (float)Math.Tan(a); }
        public static float Atan2(float y, float x) { return (float)Math.Atan2(y, x); }
        public static float Sqrt(float a) { return (float)Math.Sqrt(a); }
        public static float Pow(float a, float b) { return (float)Math.Pow(a, b); }
        public static float Exp(float a) { return (float)Math.Exp(a); }
        public static float Abs(float a) { return a < 0 ? -a : a; }
        public static float Floor(float a) { return (float)Math.Floor(a); }
        public static float Min(float a, float b) { return a < b ? a : b; }
        public static float Max(float a, float b) { return a > b ? a : b; }
        public static float Hypot(float a, float b) { return (float)Math.Sqrt(a * a + b * b); }
        public static float Clamp(float x, float a = 0, float b = 1) { return x < a ? a : (x > b ? b : x); }
        public static int Clamp(int x, int a, int b) { return x < a ? a : (x > b ? b : x); }
        public static float Lerp(float a, float b, float t) { return a + (b - a) * t; }
        public static float Smoothstep(float a, float b, float x) { float t = Clamp((x - a) / (b - a)); return t * t * (3 - 2 * t); }
        public static float Deg(float rad) { return rad * 57.29577951f; }
        public static float Rad(float deg) { return deg * 0.01745329252f; }
        public static Color Rgb(int hex) { return new Color((hex >> 16) & 255, (hex >> 8) & 255, hex & 255); }
        public static Vector3 Hex(int hex) { return new Vector3(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f); }
        public static Color Col(float r, float g, float b, float a = 1)
        {
            return new Color((byte)(Clamp(r) * 255 + 0.5f), (byte)(Clamp(g) * 255 + 0.5f), (byte)(Clamp(b) * 255 + 0.5f), (byte)(Clamp(a) * 255 + 0.5f));
        }
        public static float DistToPath(float x, float z, float[][] path)
        {
            float best = 1e9f;
            for (int i = 0; i < path.Length - 1; i++)
            {
                float ax = path[i][0], az = path[i][1], bx = path[i + 1][0], bz = path[i + 1][1];
                float dx = bx - ax, dz = bz - az, t = Clamp(((x - ax) * dx + (z - az) * dz) / (dx * dx + dz * dz));
                best = Min(best, Hypot(x - ax - dx * t, z - az - dz * t));
            }
            return best;
        }
    }

    public static class Noise
    {
        public static double Hash2(int ix, int iy, int seed = 0)
        {
            unchecked
            {
                int h = (ix * 374761393) ^ (iy * 668265263) ^ ((seed + 1) * 1274126177);
                h = (h ^ (int)((uint)h >> 13)) * 1274126177;
                h ^= (int)((uint)h >> 16);
                return (uint)h / 4294967296.0;
            }
        }

        public static double V(double x, double y, int seed = 0, int period = 0)
        {
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
            double fx = x - ix, fy = y - iy;
            double ux = fx * fx * fx * (fx * (fx * 6 - 15) + 10);
            double uy = fy * fy * fy * (fy * (fy * 6 - 15) + 10);
            int x0 = ix, x1 = ix + 1, y0 = iy, y1 = iy + 1;
            if (period != 0)
            {
                x0 = ((x0 % period) + period) % period; x1 = ((x1 % period) + period) % period;
                y0 = ((y0 % period) + period) % period; y1 = ((y1 % period) + period) % period;
            }
            double a = Hash2(x0, y0, seed), b = Hash2(x1, y0, seed), c = Hash2(x0, y1, seed), d = Hash2(x1, y1, seed);
            return a + (b - a) * ux + (c - a) * uy + (a - b - c + d) * ux * uy;
        }

        public static float Fbm(double x, double y, int oct = 4, int seed = 0, int period = 0)
        {
            double a = 0.5, f = 1, s = 0, n = 0;
            for (int i = 0; i < oct; i++)
            {
                s += a * V(x * f, y * f, seed + i * 17, period != 0 ? (int)(period * f) : 0);
                n += a; a *= 0.5; f *= 2;
            }
            return (float)(s / n);
        }
    }

    // Small seeded generator (mulberry32), so the world is laid out the same way on every run.
    public sealed class Mulberry
    {
        uint a;
        public Mulberry(uint seed) { a = seed; }
        public float Next()
        {
            unchecked
            {
                a += 0x6D2B79F5u;
                uint t = (a ^ (a >> 15)) * (1u | a);
                t = (t + ((t ^ (t >> 7)) * (61u | t))) ^ t;
                return (float)((t ^ (t >> 14)) / 4294967296.0);
            }
        }
    }

    // A 2D footprint the player cannot walk through: an axis-aligned box, a circle, or a ring with a gateway facing +z.
    public sealed class Solid
    {
        public int Kind;                 // 0 box, 1 circle, 2 ring
        public float X0, X1, Z0, Z1;     // box
        public float X, Z, R, Gap;       // circle / ring (Gap = half-angle of the gateway)
        public bool Off;                 // an open door
        public static Solid Box(float x0, float x1, float z0, float z1) { return new Solid { Kind = 0, X0 = x0, X1 = x1, Z0 = z0, Z1 = z1 }; }
        public static Solid Circle(float x, float z, float r) { return new Solid { Kind = 1, X = x, Z = z, R = r }; }
        public static Solid Ring(float x, float z, float r, float gap) { return new Solid { Kind = 2, X = x, Z = z, R = r, Gap = gap }; }
    }

    public static class Collision
    {
        // Pushes pos (x, z) out of every solid it overlaps. Returns true if it touched anything.
        public static bool Resolve(ref Vector3 pos, float rad, System.Collections.Generic.IList<Solid> solids)
        {
            bool hit = false;
            for (int i = 0; i < solids.Count; i++)
            {
                Solid s = solids[i];
                if (s.Off) continue;
                if (s.Kind == 0)
                {
                    float a = pos.X - (s.X0 - rad), b = (s.X1 + rad) - pos.X, c = pos.Z - (s.Z0 - rad), d = (s.Z1 + rad) - pos.Z;
                    if (a <= 0 || b <= 0 || c <= 0 || d <= 0) continue;
                    float m = Math.Min(Math.Min(a, b), Math.Min(c, d));
                    if (m == a) pos.X = s.X0 - rad; else if (m == b) pos.X = s.X1 + rad; else if (m == c) pos.Z = s.Z0 - rad; else pos.Z = s.Z1 + rad;
                    hit = true;
                    continue;
                }
                float dx = pos.X - s.X, dz = pos.Z - s.Z;
                if (s.Kind == 2)
                {
                    float w = 0.35f + rad;
                    if (Math.Abs(dx) > s.R + w || Math.Abs(dz) > s.R + w) continue;
                    float dd = M.Hypot(dx, dz);
                    if (Math.Abs(dd - s.R) >= w || dd < 1e-4f) continue;
                    if (Math.Abs(M.Atan2(dx, dz)) < s.Gap) continue;          // through the gateway
                    float k = (s.R + (dd > s.R ? w : -w)) / dd;
                    pos.X = s.X + dx * k; pos.Z = s.Z + dz * k;
                    hit = true;
                    continue;
                }
                float r = s.R + rad;
                if (Math.Abs(dx) >= r || Math.Abs(dz) >= r) continue;
                float dist = M.Hypot(dx, dz);
                if (dist >= r) continue;
                if (dist < 1e-4f) { pos.X = s.X + r; hit = true; continue; }
                pos.X = s.X + dx / dist * r; pos.Z = s.Z + dz / dist * r;
                hit = true;
            }
            return hit;
        }
    }
}
