// Frosted grass that follows the player, three tree species and boulders. Trees and boulders are baked into
// static chunks (no instancing needed); foliage lighting is baked into vertex colours.
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PaxRomana
{
    // The light rig, shared by the renderer and by anything that bakes lighting into vertex colours.
    public static class Lighting
    {
        public static readonly Vector3 Sun = new Vector3(1.0f, 0.74f, 0.50f) * 0.95f;      // low winter sun
        public static readonly Vector3 Sky = new Vector3(0.26f, 0.32f, 0.46f);            // from straight above
        public static readonly Vector3 Bounce = new Vector3(0.15f, 0.16f, 0.22f);         // off the snow, from the far side
        public static readonly Vector3 Ambient = new Vector3(0.30f, 0.33f, 0.43f);
        public static readonly Vector3 BounceFrom = Vector3.Normalize(new Vector3(0.76f, -0.4f, -0.46f));
        public static readonly Vector3 Fog = M.Hex(0xb7b4c8);                             // cold violet haze
        public const float FogStart = 60, FogEnd = 3300;

        public static Vector3 Bake(Vector3 n)
        {
            return Ambient + Sun * Math.Max(0, Vector3.Dot(n, Config.SUN_DIR)) + Sky * Math.Max(0, n.Y) + Bounce * Math.Max(0, Vector3.Dot(n, BounceFrom));
        }
    }

    public sealed class VegChunk
    {
        public Geo Trunk = new Geo(), Leaf = new Geo(), Rock = new Geo();
        public Vector3 Min = new Vector3(1e9f), Max = new Vector3(-1e9f);
        public void Grow(Vector3 p, float r) { Min = Vector3.Min(Min, p - new Vector3(r)); Max = Vector3.Max(Max, p + new Vector3(r)); }
    }

    public sealed class GrassCell
    {
        public VertexPositionColor[] V; public int N, Draw; public bool Visible, Dirty; public BoundingSphere Bs;
        public int Cx = int.MinValue, Cz = int.MinValue;
    }

    public sealed class Grass
    {
        public const int GC = 24, GN = 7;        // cell size (m) and cells per side of the window around the player
        public const int TuftVerts = 15, TuftIndices = 27;
        public readonly int Cap;
        public readonly GrassCell[] Cells = new GrassCell[GN * GN];
        static readonly Vector3[] Tuft = MakeTuft();
        readonly List<int> stale = new List<int>();
        readonly List<float> staleD = new List<float>();
        int[] staleCx = new int[GN * GN], staleCz = new int[GN * GN];

        public Grass(int count)
        {
            Cap = Math.Max(200, count / 25);
            for (int i = 0; i < Cells.Length; i++) Cells[i] = new GrassCell { V = new VertexPositionColor[Cap * TuftVerts] };
        }

        static Vector3[] MakeTuft()
        {
            Vector3[] v = new Vector3[TuftVerts];
            for (int b = 0; b < 3; b++)
            {
                float a = b * 2.1f + 0.3f, ca = M.Cos(a), sa = M.Sin(a), ox = M.Cos(a * 1.7f) * 0.07f, oz = M.Sin(a * 1.7f) * 0.07f;
                const float w = 0.032f;
                float[][] pts = { new[] { -w, 0, 0 }, new[] { w, 0, 0 }, new[] { -w * 0.7f, 0.5f, 0.06f }, new[] { w * 0.7f, 0.5f, 0.06f }, new[] { 0, 1, 0.24f } };
                for (int k = 0; k < 5; k++) v[b * 5 + k] = new Vector3(pts[k][0] * ca - pts[k][2] * sa + ox, pts[k][1], pts[k][0] * sa + pts[k][2] * ca + oz);
            }
            return v;
        }

        // index pattern for one tuft, repeated for every tuft in a cell
        public static ushort[] Indices(int cap)
        {
            ushort[] idx = new ushort[cap * TuftIndices];
            int[] pat = { 0, 1, 2, 1, 3, 2, 2, 3, 4 };
            for (int t = 0; t < cap; t++)
                for (int b = 0; b < 3; b++)
                    for (int k = 0; k < 9; k++) idx[t * TuftIndices + b * 9 + k] = (ushort)(t * TuftVerts + b * 5 + pat[k]);
            return idx;
        }

        static int Mod(int a) { return ((a % GN) + GN) % GN; }

        // A cell always regrows the same tufts: the generator is seeded by the cell's coordinates.
        void Fill(GrassCell cell, int cx, int cz)
        {
            Mulberry rng = new Mulberry(unchecked((uint)((cx * 73856093) ^ (cz * 19349663))));
            int n = 0; float lo = 1e9f, hi = -1e9f;
            for (int i = 0; i < Cap; i++)
            {
                float x = (cx + rng.Next()) * GC, z = (cz + rng.Next()) * GC, r1 = rng.Next(), r2 = rng.Next(), r3 = rng.Next(), r4 = rng.Next(), r5 = rng.Next(), r6 = rng.Next();
                float h = Terrain.GroundAt(x, z);
                if (h < 1.0f) continue;                                                                   // river bed and shore
                if (Math.Abs(x) < 64 && Math.Abs(z) < 64 && M.Hypot(x, z) < 60) continue;                 // Kremlin rock
                if (M.Hypot(x - Config.CAMP_X, z - Config.CAMP_Z) < Config.CAMP_R + 5) continue;          // trampled camp
                if (M.Hypot((Terrain.GroundAt(x + 0.8f, z) - h) / 0.8f, (Terrain.GroundAt(x, z + 0.8f) - h) / 0.8f) > 0.6f) continue;   // bare rock
                if (x > -10 && x < 185 && z > 30 && z < 310 && (M.DistToPath(x, z, Config.ROAD) < 3 || M.DistToPath(x, z, Config.ROAD2) < 3)) continue;
                float patch = Noise.Fbm(x * 0.09, z * 0.09, 2, 77);
                float yaw = r1 * M.TAU, cs = M.Cos(yaw), sn = M.Sin(yaw);
                float sx = 1 + r2 * 0.8f, sy = (0.24f + r3 * 0.3f) * (0.6f + patch * 1.2f), sz = 1 + r4 * 0.8f;
                float t = M.Smoothstep(0.3f, 0.7f, Terrain.MacroAt(x, z)), k = (0.8f + r5 * 0.45f) * 0.9f;
                float cr = 0.7f * M.Lerp(1.22f, 0.74f, t) * k, cg = 0.74f * M.Lerp(1.04f, 1.0f, t) * k, cb = 0.66f * M.Lerp(0.66f, 0.78f, t) * k;
                if (r6 < 0.05f) { cr = 0.42f * k; cg = 0.36f * k; cb = 0.12f * k; }      // dry straw
                for (int v = 0; v < TuftVerts; v++)
                {
                    Vector3 p = Tuft[v];
                    // dark at the root, rimed white towards the tip
                    float px = p.X * sx, pz = p.Z * sz, shade = M.Lerp(0.5f, 1.15f, p.Y), frost = 0.35f + 0.5f * p.Y;
                    cell.V[n * TuftVerts + v] = new VertexPositionColor(
                        new Vector3(x + px * cs + pz * sn, h - 0.03f + p.Y * sy, z - px * sn + pz * cs),
                        M.Col(M.Lerp(cr * shade, 0.80f, frost), M.Lerp(cg * shade, 0.85f, frost), M.Lerp(cb * shade, 0.93f, frost)));
                }
                n++; lo = Math.Min(lo, h); hi = Math.Max(hi, h);
            }
            cell.Bs = new BoundingSphere(new Vector3((cx + 0.5f) * GC, n > 0 ? (lo + hi) / 2 : 0, (cz + 0.5f) * GC), M.Hypot(GC * 0.75f, n > 0 ? (hi - lo) / 2 + 1.5f : 0));
            cell.Cx = cx; cell.Cz = cz; cell.N = n; cell.Dirty = true;
        }

        // Re-centres the meadow on (px, pz); refills at most `budget` cells per call, nearest first.
        public void Update(float px, float pz, int budget = 1)
        {
            int pcx = (int)Math.Floor(px / GC), pcz = (int)Math.Floor(pz / GC), Hh = (GN - 1) / 2;
            stale.Clear(); staleD.Clear();
            for (int dz = -Hh; dz <= Hh; dz++)
                for (int dx = -Hh; dx <= Hh; dx++)
                {
                    int cx = pcx + dx, cz = pcz + dz, slot = Mod(cz) * GN + Mod(cx);
                    GrassCell cell = Cells[slot];
                    float d = M.Hypot((cx + 0.5f) * GC - px, (cz + 0.5f) * GC - pz);
                    if (cell.Cx != cx || cell.Cz != cz) { cell.Visible = false; stale.Add(slot); staleD.Add(d); staleCx[slot] = cx; staleCz[slot] = cz; continue; }
                    cell.Draw = (int)(cell.N * (1 - 0.72f * M.Smoothstep(28, 80, d)));      // thin out with distance
                    cell.Visible = cell.Draw > 0;
                }
            for (int done = 0; done < budget && stale.Count > 0; done++)
            {
                int best = 0;
                for (int i = 1; i < stale.Count; i++) if (staleD[i] < staleD[best]) best = i;
                int slot = stale[best];
                Fill(Cells[slot], staleCx[slot], staleCz[slot]);
                stale.RemoveAt(best); staleD.RemoveAt(best);
            }
        }
    }

    public sealed class Vegetation
    {
        public readonly Dictionary<long, VegChunk> Near = new Dictionary<long, VegChunk>(), Far = new Dictionary<long, VegChunk>();
        public readonly List<Solid> Obstacles = new List<Solid>();
        public Grass Grass;
        const float NEAR_CELL = 150, FAR_CELL = 700;

        sealed class Species { public Geo Trunk, Leaf, LiteTrunk, LiteLeaf; public Vector3 Tint; }

        static VegChunk Chunk(Dictionary<long, VegChunk> map, float cell, float x, float z)
        {
            long key = ((long)Math.Floor(x / cell) << 32) ^ (uint)(int)Math.Floor(z / cell);
            VegChunk c;
            if (!map.TryGetValue(key, out c)) { c = new VegChunk(); map[key] = c; }
            return c;
        }

        // Foliage cards scattered around branch ends, with normals that puff outward from the canopy centre.
        static Geo Cards(Mulberry rng, List<Vector3> centres, int per, float size, float spread, Vector3 canopy, float squash, int kind)
        {
            Geo all = new Geo();
            foreach (Vector3 c in centres)
                for (int i = 0; i < per; i++)
                {
                    float s = size * (0.75f + rng.Next() * 0.5f);
                    Geo g = Geo.Plane(s, s);
                    float ex = rng.Next() * M.TAU, ey = rng.Next() * M.TAU, ez = rng.Next() * M.TAU;
                    g.Rotate(Matrix.CreateRotationZ(ez) * Matrix.CreateRotationY(ey) * Matrix.CreateRotationX(ex));
                    g.Translate(c.X + (rng.Next() - 0.5f) * spread, c.Y + (rng.Next() - 0.5f) * spread * squash, c.Z + (rng.Next() - 0.5f) * spread);
                    for (int k = 0; k < g.Count; k++)
                    {
                        Vector3 p = g.P[k];
                        g.N[k] = Vector3.Normalize(new Vector3(p.X - canopy.X, (p.Y - canopy.Y) * 1.2f + 0.6f, p.Z - canopy.Z));
                        g.T[k] = new Vector2((g.T[k].X + kind) / 4f, g.T[k].Y);
                    }
                    all.Add(g);
                }
            return all;
        }

        static Geo Limb(Vector3 a, Vector3 b, float r0, float r1, int seg = 6)
        {
            Vector3 d = b - a; float len = d.Length();
            Geo g = Geo.Cylinder(r1, r0, len, seg, true);
            for (int i = 0; i < g.Count; i++) g.T[i] = new Vector2(g.T[i].X, g.T[i].Y * len * 0.5f);
            g.Rotate(Geo.FromUnitVectors(Vector3.Up, d / len));
            g.Translate((a + b) * 0.5f);
            return g;
        }

        static Species Make(int kind, Mulberry rng)
        {
            Species sp = new Species();
            Geo trunk = new Geo(); List<Vector3> centres = new List<Vector3>();
            if (kind == 0)          // birch-like broadleaf
            {
                trunk.Add(Limb(new Vector3(0, -0.5f, 0), new Vector3(0.2f, 3.6f, 0.1f), 0.42f, 0.3f, 8));
                sp.LiteTrunk = Limb(new Vector3(0, -0.5f, 0), new Vector3(0.2f, 5.5f, 0.1f), 0.42f, 0.2f, 5);
                for (int i = 0; i < 8; i++)
                {
                    float a = i / 8f * M.TAU + rng.Next(), r = 1.6f + rng.Next() * 2.2f;
                    Vector3 c = new Vector3(M.Cos(a) * r, 5.2f + rng.Next() * 3.4f, M.Sin(a) * r);
                    centres.Add(c);
                    trunk.Add(Limb(new Vector3(0.2f, 3.3f, 0.1f), c, 0.2f, 0.05f, 5));
                }
                centres.Add(new Vector3(0, 8.6f, 0));
                sp.Leaf = Cards(rng, centres, 8, 2.7f, 2.2f, new Vector3(0, 6.5f, 0), 1, 0);
                sp.LiteLeaf = Cards(rng, centres, 2, 3.9f, 2.2f, new Vector3(0, 6.5f, 0), 1, 0);
                sp.Tint = M.Hex(0xd9e2ee);
            }
            else if (kind == 1)     // umbrella pine
            {
                trunk.Add(Limb(new Vector3(0, -0.5f, 0), new Vector3(0.5f, 5, 0.2f), 0.34f, 0.26f, 7));
                trunk.Add(Limb(new Vector3(0.5f, 5, 0.2f), new Vector3(0.2f, 9.5f, 0), 0.26f, 0.18f, 7));
                sp.LiteTrunk = Limb(new Vector3(0, -0.5f, 0), new Vector3(0.3f, 9.8f, 0.1f), 0.34f, 0.16f, 5);
                for (int i = 0; i < 9; i++)
                {
                    float a = i / 9f * M.TAU, r = i > 0 ? 1.5f + rng.Next() * 2.4f : 0;
                    Vector3 c = new Vector3(0.2f + M.Cos(a) * r, 10.6f + rng.Next() * 0.9f, M.Sin(a) * r);
                    centres.Add(c);
                    trunk.Add(Limb(new Vector3(0.2f, 9.2f, 0), c, 0.12f, 0.04f, 4));
                }
                sp.Leaf = Cards(rng, centres, 7, 2.5f, 1.9f, new Vector3(0.2f, 9.8f, 0), 0.4f, 1);
                sp.LiteLeaf = Cards(rng, centres, 2, 3.6f, 1.9f, new Vector3(0.2f, 9.8f, 0), 0.4f, 1);
                sp.Tint = M.Hex(0x9fb9b0);
            }
            else                    // cypress / spruce column
            {
                trunk.Add(Limb(new Vector3(0, -0.5f, 0), new Vector3(0, 3, 0), 0.2f, 0.14f, 6));
                sp.Leaf = new Geo();
                for (int i = 0; i < 12; i++)
                {
                    Vector3 c = new Vector3(0, 1.6f + i * 1.0f, 0);
                    sp.Leaf.Add(Cards(rng, new List<Vector3> { c }, 5, 1.9f - i * 0.09f, 1.5f - i * 0.1f, new Vector3(0, c.Y - 1.5f, 0), 0.8f, 2));
                }
                sp.LiteTrunk = trunk; sp.LiteLeaf = sp.Leaf;
                sp.Tint = M.Hex(0x8fa8a2);
            }
            sp.Trunk = trunk;
            return sp;
        }

        Species[] defs;
        Mulberry rng;
        int placed;

        void Place(int kind, float x, float z, float s, bool inner)
        {
            float y = inner ? Terrain.GroundAt(x, z) : Terrain.HeightAt(x, z);
            float yaw = rng.Next() * M.TAU, sy = s * (0.9f + rng.Next() * 0.25f);
            Matrix m = Matrix.CreateScale(s, sy, s) * Matrix.CreateRotationY(yaw) * Matrix.CreateTranslation(x, y, z);
            Species sp = defs[kind];
            VegChunk c = Chunk(inner ? Near : Far, inner ? NEAR_CELL : FAR_CELL, x, z);
            c.Grow(new Vector3(x, y + 6 * s, z), 9 * s);
            c.Trunk.AddTransformed(inner ? sp.Trunk : sp.LiteTrunk, ref m, Color.White, false);
            Geo leaf = inner ? sp.Leaf : sp.LiteLeaf;
            for (int i = 0; i < leaf.Count; i++)
            {
                Vector3 n = Vector3.Normalize(Vector3.TransformNormal(leaf.N[i], m));
                Vector3 l = Lighting.Bake(n) * sp.Tint;
                c.Leaf.P.Add(Vector3.Transform(leaf.P[i], m)); c.Leaf.N.Add(n); c.Leaf.T.Add(leaf.T[i]);
                if (c.Leaf.C == null) c.Leaf.C = new List<Color>();
                c.Leaf.C.Add(M.Col(l.X, l.Y, l.Z));
            }
            if (inner) Obstacles.Add(Solid.Circle(x, z, (kind == 2 ? 0.3f : 0.5f) * s));
            placed++;
        }

        static bool Ok(float x, float z)
        {
            float h = Terrain.GroundAt(x, z);
            if (h < 1.8f) return false;
            if (M.Hypot(x, z) < 118) return false;
            if (M.Hypot(x - Config.CAMP_X, z - Config.CAMP_Z) < Config.CAMP_R + 26) return false;
            if (Math.Abs(z - Config.RiverZ(x)) < 20) return false;
            if (M.DistToPath(x, z, Config.ROAD) < 7 || M.DistToPath(x, z, Config.ROAD2) < 7) return false;
            if (x > -110 && x < 350 && z > 262 && z < Config.SPAWN_Z + 6) return false;    // keep sightlines from the ridge
            if (M.Hypot(x - Config.SPAWN_X, z - Config.SPAWN_Z) < 7) return false;
            const float e = 4;      // slope reject
            if (Math.Abs(Terrain.GroundAt(x + e, z) - Terrain.GroundAt(x - e, z)) + Math.Abs(Terrain.GroundAt(x, z + e) - Terrain.GroundAt(x, z - e)) > 7) return false;
            return true;
        }

        public static Vegetation Build(int grassCount)
        {
            Vegetation v = new Vegetation();
            v.Make(grassCount);
            return v;
        }

        void Make(int grassCount)
        {
            rng = new Mulberry(2024);
            defs = new[] { Make(0, rng), Make(1, rng), Make(2, rng) };
            float X0 = Config.INNER_X0, Z0 = Config.INNER_Z0, SZ = Config.INNER_SIZE, SX = Config.SPAWN_X, SPZ = Config.SPAWN_Z;
            // valley and hills inside the dense tile
            int tries = 0;
            while (placed < 1150 && tries++ < 40000)
            {
                float x = X0 + rng.Next() * SZ, z = Z0 + rng.Next() * SZ;
                float forest = Noise.Fbm(x * 0.006 + 7, z * 0.006 + 3, 3, 44);
                if (rng.Next() > M.Smoothstep(0.38f, 0.62f, forest) * 0.9f + 0.05f) continue;
                if (!Ok(x, z)) continue;
                float k = rng.Next();
                Place(k < 0.62f ? 0 : k < 0.86f ? 1 : 2, x, z, 0.8f + rng.Next() * 0.6f, true);
            }
            // an avenue along the roads, pines framing the nest
            foreach (float[][] road in new[] { Config.ROAD, Config.ROAD2 })
                for (int i = 0; i < road.Length - 1; i++)
                {
                    float ax = road[i][0], az = road[i][1], bx = road[i + 1][0], bz = road[i + 1][1], len = M.Hypot(bx - ax, bz - az), nx = -(bz - az) / len, nz = (bx - ax) / len;
                    for (float d = 6; d < len; d += 13)
                        foreach (int s in new[] { -1, 1 })
                        {
                            float x = ax + (bx - ax) * d / len + nx * s * 5.5f, z = az + (bz - az) * d / len + nz * s * 5.5f;
                            if (M.Hypot(x, z) < 62 || Terrain.GroundAt(x, z) < 2.2f || M.Hypot(x - Config.CAMP_X, z - Config.CAMP_Z) < Config.CAMP_R + 6) continue;
                            if (rng.Next() < 0.75f) Place(2, x, z, 0.9f + rng.Next() * 0.5f, true);
                        }
                }
            Place(0, SX + 9, SPZ + 5, 1.5f, true);
            Place(1, SX - 13, SPZ + 9, 1.3f, true);
            Place(1, SX - 20, SPZ - 3, 1.1f, true);
            Place(2, SX + 16, SPZ - 6, 1.1f, true);
            // far forests on the outer hills
            for (int i = 0; i < 1500; i++)
            {
                float a = rng.Next() * M.TAU, r = 620 + rng.Next() * 1500, x = 60 + M.Cos(a) * r, z = 190 + M.Sin(a) * r;
                if (x > X0 && x < X0 + SZ && z > Z0 && z < Z0 + SZ) continue;
                if (Noise.Fbm(x * 0.004, z * 0.004, 2, 45) < 0.45f || Terrain.HeightAt(x, z) < 3) continue;
                Place(rng.Next() < 0.7f ? 0 : 1, x, z, 1.2f + rng.Next() * 0.9f, false);
            }

            // boulders
            Geo rg = Geo.Sphere(1, 10, 7);
            for (int i = 0; i < rg.Count; i++)
            {
                Vector3 p = rg.P[i];
                float k = 0.72f + Noise.Fbm(p.X * 1.7 + 5, p.Y * 1.7 + p.Z * 1.3, 3, 8) * 0.6f;
                rg.P[i] = new Vector3(p.X * k, p.Y * k * 0.7f, p.Z * k);
                rg.T[i] = rg.T[i] * 2;
            }
            rg.FlatNormals();
            for (int i = 0; i < 420; i++)
            {
                float x, z;
                if (i < 150) { float a = rng.Next() * M.TAU, r = 64 + rng.Next() * 60; x = M.Cos(a) * r; z = M.Sin(a) * r; }
                else if (i < 230) { float a = rng.Next() * M.TAU, r = 4 + rng.Next() * 80; x = SX + M.Cos(a) * r; z = SPZ + M.Sin(a) * r; }
                else { x = X0 + rng.Next() * SZ; z = Z0 + rng.Next() * SZ; }
                float s = i < 230 ? 0.4f + rng.Next() * 1.9f : 0.6f + rng.Next() * 3;
                float ex = rng.Next() * 0.6f, ey = rng.Next() * M.TAU, ez = rng.Next() * 0.6f;
                float y = Terrain.GroundAt(x, z) - s * 0.15f;
                Matrix m = Matrix.CreateScale(s, s * (0.6f + rng.Next() * 0.6f), s * (0.7f + rng.Next() * 0.6f))
                    * Matrix.CreateRotationZ(ez) * Matrix.CreateRotationY(ey) * Matrix.CreateRotationX(ex) * Matrix.CreateTranslation(x, y, z);
                VegChunk c = Chunk(Near, NEAR_CELL, x, z);
                c.Grow(new Vector3(x, y, z), s * 1.6f);
                c.Rock.AddTransformed(rg, ref m, Color.White, false);
                if (s >= 0.8f) Obstacles.Add(Solid.Circle(x, z, s * 0.8f));
            }

            Grass = new Grass(grassCount);
            Grass.Update(Config.SPAWN_X, Config.SPAWN_Z, 1000);
            Grass.Update(Config.SPAWN_X, Config.SPAWN_Z, 0);
            defs = null;
        }
    }
}
