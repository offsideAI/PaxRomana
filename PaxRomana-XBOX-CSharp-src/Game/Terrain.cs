// Heightfield terrain: a dense inner tile around the valley and a coarse outer ring out to the horizon.
// Meadow grass, bare earth on the roads and rock on the slopes are baked into vertex colours.
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace PaxRomana
{
    public static class Terrain
    {
        const int N = Config.INNER_N;
        const float CELL = Config.INNER_SIZE / Config.INNER_N;
        static float[] grid;

        public static float HeightAt(float x, float z)
        {
            double h = (Noise.Fbm(x * 0.0012 + 10, z * 0.0012 + 10, 5, 1) - 0.5) * 110 + 22;
            h += (Noise.Fbm(x * 0.011, z * 0.011, 3, 2) - 0.5) * 5;
            float rv = M.Hypot(x - 60, z - 190);
            // open valley between the ridge and the castle
            float vm = 1 - M.Smoothstep(330, 720, rv);
            h = h + ((8 + (h - 22) * 0.12) - h) * vm;
            // river
            float rm = 1 - M.Smoothstep(12, 46, Math.Abs(z - Config.RiverZ(x)));
            h = h + (-3.2 - h) * rm;
            // castle rock
            float d = M.Hypot(x, z);
            if (d < 150)
            {
                float hm = M.Pow(1 - M.Smoothstep(58, 150, d), 0.8f);
                float crag = (Noise.Fbm(x * 0.06, z * 0.06, 3, 5) - 0.5f) * 7 * M.Smoothstep(60, 82, d) * hm;
                h = h + (Config.PLATEAU - h) * hm + crag;
            }
            // garrison camp terrace
            float dc = M.Hypot(x - Config.CAMP_X, z - Config.CAMP_Z);
            if (dc < 100) h = h + (Config.CAMP_H - h) * (1 - M.Smoothstep(58, 100, dc));
            // sniper's ridge
            float ex = (x - 120) / 260, ez = (z - 435) / 105;
            h += 72 * Math.Exp(-1.2 * (ex * ex + ez * ez));
            // distant mountains
            float far = M.Smoothstep(900, 2800, rv);
            if (far > 0) h += far * far * Noise.Fbm(x * 0.0008 + 3, z * 0.0008 + 9, 4, 9) * 380;
            // level ground for the enrichment works (outside the playable valley)
            float dw = M.Hypot(x - Config.WORKS_X, z - Config.WORKS_Z);
            if (dw < 380) h = h + (Config.WORKS_H - h) * (1 - M.Smoothstep(250, 380, dw));
            return (float)h;
        }

        public static float MacroAt(float x, float z) { return Noise.Fbm(x * 0.004 + 50, z * 0.004 + 50, 3, 11); }

        // Height of the ground: bilinear on the inner grid, analytic outside it.
        public static float GroundAt(float x, float z)
        {
            if (grid == null) return HeightAt(x, z);
            float gx = (x - Config.INNER_X0) / CELL, gz = (z - Config.INNER_Z0) / CELL;
            if (gx < 0 || gz < 0 || gx >= N || gz >= N) return HeightAt(x, z);
            int i = (int)gx, j = (int)gz; float fx = gx - i, fz = gz - j;
            float a = grid[j * (N + 1) + i], b = grid[j * (N + 1) + i + 1];
            float c = grid[(j + 1) * (N + 1) + i], d = grid[(j + 1) * (N + 1) + i + 1];
            return a + (b - a) * fx + (c - a) * fz + (a - b - c + d) * fx * fz;
        }

        // how much the ground at (x, z) is worn to bare earth: the castle rock, the camp, the roads, the works yard
        static float Trodden(float x, float z)
        {
            float t = 0;
            float d = M.Hypot(x, z);
            t += (210f / 255) * (1 - M.Smoothstep(70 * 0.55f, 70, d));
            float rc = Config.CAMP_R + 14;
            t += (190f / 255) * (1 - M.Smoothstep(rc * 0.55f, rc, M.Hypot(x - Config.CAMP_X, z - Config.CAMP_Z)));
            if (x > -30 && x < 200 && z > 20 && z < 320)
            {
                float dr = Math.Min(M.DistToPath(x, z, Config.ROAD), M.DistToPath(x, z, Config.ROAD2));
                t += (60f / 255) * (1 - M.Smoothstep(3.0f, 5.5f, dr)) + (90f / 255) * (1 - M.Smoothstep(1.8f, 4.0f, dr)) + (110f / 255) * (1 - M.Smoothstep(0.6f, 2.6f, dr));
            }
            t += (200f / 255) * (1 - M.Smoothstep(230, 270, M.Hypot(x - Config.WORKS_X, z - Config.WORKS_Z)));
            return M.Clamp(t);
        }

        static Color ColourAt(float x, float z, float y, float ny)
        {
            float nb = Noise.Fbm(x * 0.05, z * 0.05, 3, 91);
            float gB = 0.25f + 0.3f * nb;
            float macro = MacroAt(x, z);
            // summer meadow: sun-yellowed in some places, lush in others, darker under the far forests
            Vector3 tint = Vector3.Lerp(new Vector3(1.22f, 1.04f, 0.72f), new Vector3(0.80f, 1.0f, 0.86f), M.Smoothstep(0.3f, 0.7f, macro));
            Vector3 grass = new Vector3(0.31f, 0.39f, 0.17f) * (0.8f + gB * 0.5f) * tint;
            Vector3 earth = new Vector3(0.50f, 0.41f, 0.29f) * (0.75f + gB * 0.7f);
            Vector3 rock = new Vector3(0.52f, 0.49f, 0.44f) * (0.7f + nb * 0.6f);
            float slope = 1 - ny;
            float rockW = Math.Max(M.Smoothstep(0.13f, 0.3f, slope + (gB - 0.3f) * 0.22f), M.Smoothstep(230, 400, y) * 0.85f);      // bare summits
            float shore = 1 - M.Smoothstep(0.2f, 1.8f, y);
            float dirtW = M.Clamp(Trodden(x, z) * 1.25f * (0.75f + gB) + shore);
            Vector3 c = Vector3.Lerp(grass, earth, dirtW);
            c = Vector3.Lerp(c, rock, rockW);
            return M.Col(c.X, c.Y, c.Z);
        }

        delegate float HeightFn(float x, float z, int i, int j);

        static IndexedMesh GridMesh(float x0, float z0, float cell, int nx, int nz, HeightFn hf, Func<int, int, bool> skip, bool exactNormals)
        {
            int vw = nx + 1, count = vw * (nz + 1);
            IndexedMesh m = new IndexedMesh { P = new Vector3[count], N = new Vector3[count], C = new Color[count], T = new Vector2[count] };
            int k = 0;
            for (int j = 0; j <= nz; j++)
                for (int i = 0; i <= nx; i++)
                {
                    float x = x0 + i * cell, z = z0 + j * cell;
                    m.P[k++] = new Vector3(x, hf(x, z, i, j), z);
                }
            for (int j = 0; j <= nz; j++)
                for (int i = 0; i <= nx; i++)
                {
                    int il = Math.Max(0, i - 1), ir = Math.Min(nx, i + 1), ju = Math.Max(0, j - 1), jd = Math.Min(nz, j + 1);
                    float dx = (m.P[j * vw + ir].Y - m.P[j * vw + il].Y) / ((ir - il) * cell);
                    float dz = (m.P[jd * vw + i].Y - m.P[ju * vw + i].Y) / ((jd - ju) * cell);
                    Vector3 p = m.P[j * vw + i];
                    if (exactNormals)
                    {
                        // sample the shared height grid, so neighbouring tiles agree along their common edge
                        dx = (GroundAt(p.X + cell, p.Z) - GroundAt(p.X - cell, p.Z)) / (2 * cell);
                        dz = (GroundAt(p.X, p.Z + cell) - GroundAt(p.X, p.Z - cell)) / (2 * cell);
                    }
                    Vector3 n = Vector3.Normalize(new Vector3(-dx, 1, -dz));
                    m.N[j * vw + i] = n;
                    m.C[j * vw + i] = ColourAt(p.X, p.Z, p.Y, n.Y);
                    m.T[j * vw + i] = new Vector2(p.X * 0.21f, p.Z * 0.21f);
                }
            List<ushort> idx = new List<ushort>(nx * nz * 6);
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    if (skip != null && skip(i, j)) continue;
                    int a = j * vw + i, b = a + vw, c = a + 1, d = b + 1;
                    idx.Add((ushort)a); idx.Add((ushort)b); idx.Add((ushort)c); idx.Add((ushort)b); idx.Add((ushort)d); idx.Add((ushort)c);
                }
            m.Idx = idx.ToArray();
            return m;
        }

        // Builds the height grid and returns the terrain tiles: 16 inner tiles (culled separately) and the outer ring.
        public static List<IndexedMesh> Build()
        {
            // inner tile heights (edges snapped to the coarse grid so the seam is watertight)
            const int OC = 30; int per = (int)(OC / CELL);
            grid = null;
            float[] g = new float[(N + 1) * (N + 1)];
            for (int j = 0; j <= N; j++)
                for (int i = 0; i <= N; i++)
                {
                    float x = Config.INNER_X0 + i * CELL, z = Config.INNER_Z0 + j * CELL, h;
                    if (j == 0 || j == N) h = Edge(z, i, true, per);
                    else if (i == 0 || i == N) h = Edge(x, j, false, per);
                    else h = HeightAt(x, z);
                    g[j * (N + 1) + i] = h;
                }
            grid = g;
            List<IndexedMesh> tiles = new List<IndexedMesh>();
            const int TILE = 100;
            for (int tj = 0; tj < N / TILE; tj++)
                for (int ti = 0; ti < N / TILE; ti++)
                {
                    int oi = ti * TILE, oj = tj * TILE;
                    tiles.Add(GridMesh(Config.INNER_X0 + oi * CELL, Config.INNER_Z0 + oj * CELL, CELL, TILE, TILE,
                        (x, z, i, j) => g[(oj + j) * (N + 1) + oi + i], null, true));
                }
            const int ON = 240, i0 = 100; int span = (int)(Config.INNER_SIZE / OC);
            tiles.Add(GridMesh(Config.INNER_X0 - i0 * OC, Config.INNER_Z0 - i0 * OC, OC, ON, ON, (x, z, i, j) => HeightAt(x, z),
                (i, j) => i >= i0 && i < i0 + span && j >= i0 && j < i0 + span, false));
            return tiles;
        }

        static float Edge(float fixedCoord, int t, bool alongX, int per)
        {
            int a = t / per * per, b = Math.Min(N, a + per); float f = (float)(t - a) / per;
            float pa = alongX ? HeightAt(Config.INNER_X0 + a * CELL, fixedCoord) : HeightAt(fixedCoord, Config.INNER_Z0 + a * CELL);
            float pb = alongX ? HeightAt(Config.INNER_X0 + b * CELL, fixedCoord) : HeightAt(fixedCoord, Config.INNER_Z0 + b * CELL);
            return M.Lerp(pa, pb, f);
        }
    }
}
