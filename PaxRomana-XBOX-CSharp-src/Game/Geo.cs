// Procedural geometry: triangle soups with positions, normals, texture coordinates and optional vertex colours,
// plus the primitive shapes the world is built from. Front faces wind counter-clockwise; +Y is up; v = 0 is the
// bottom of a texture (the renderer flips it on upload).
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace PaxRomana
{
    public sealed class Geo
    {
        public readonly List<Vector3> P = new List<Vector3>();
        public readonly List<Vector3> N = new List<Vector3>();
        public readonly List<Vector2> T = new List<Vector2>();
        public List<Color> C;            // null = white
        public int Count { get { return P.Count; } }

        public void Vert(Vector3 p, Vector3 n, Vector2 t) { P.Add(p); N.Add(n); T.Add(t); if (C != null) C.Add(Color.White); }

        public Geo Add(Geo g)
        {
            if (g.C != null && C == null) { C = new List<Color>(P.Count + g.P.Count); for (int i = 0; i < P.Count; i++) C.Add(Color.White); }
            P.AddRange(g.P); N.AddRange(g.N); T.AddRange(g.T);
            if (C != null) { if (g.C != null) C.AddRange(g.C); else for (int i = 0; i < g.P.Count; i++) C.Add(Color.White); }
            return this;
        }

        // Adds g transformed by m (rotation, uniform-ish scale and translation), tinted by tint.
        public void AddTransformed(Geo g, ref Matrix m, Color tint, bool useTint)
        {
            if (useTint && C == null) { C = new List<Color>(P.Count + g.P.Count); for (int i = 0; i < P.Count; i++) C.Add(Color.White); }
            for (int i = 0; i < g.P.Count; i++)
            {
                P.Add(Vector3.Transform(g.P[i], m));
                Vector3 n = Vector3.TransformNormal(g.N[i], m);
                float l = n.Length(); N.Add(l > 1e-6f ? n / l : Vector3.Up);
                T.Add(g.T[i]);
                if (C != null) C.Add(useTint ? (g.C != null ? Mul(g.C[i], tint) : tint) : (g.C != null ? g.C[i] : Color.White));
            }
        }
        static Color Mul(Color a, Color b) { return new Color((byte)(a.R * b.R / 255), (byte)(a.G * b.G / 255), (byte)(a.B * b.B / 255), a.A); }

        public Geo Paint(Color c)
        {
            C = new List<Color>(P.Count);
            for (int i = 0; i < P.Count; i++) C.Add(c);
            return this;
        }
        public Geo Paint(int hex) { return Paint(M.Rgb(hex)); }

        public Geo Translate(float x, float y, float z)
        {
            Vector3 d = new Vector3(x, y, z);
            for (int i = 0; i < P.Count; i++) P[i] += d;
            return this;
        }
        public Geo Translate(Vector3 d) { return Translate(d.X, d.Y, d.Z); }

        public Geo Scale(float x, float y, float z)
        {
            for (int i = 0; i < P.Count; i++)
            {
                Vector3 p = P[i]; P[i] = new Vector3(p.X * x, p.Y * y, p.Z * z);
                Vector3 n = N[i]; n = new Vector3(n.X / x, n.Y / y, n.Z / z);
                float l = n.Length(); N[i] = l > 1e-9f ? n / l : N[i];
            }
            if (x * y * z < 0) Flip();
            return this;
        }
        public Geo Scale(float s) { for (int i = 0; i < P.Count; i++) P[i] *= s; return this; }

        public Geo Rotate(Matrix m)
        {
            for (int i = 0; i < P.Count; i++) { P[i] = Vector3.TransformNormal(P[i], m); N[i] = Vector3.TransformNormal(N[i], m); }
            return this;
        }
        public Geo RotateX(float a) { return Rotate(Matrix.CreateRotationX(a)); }
        public Geo RotateY(float a) { return Rotate(Matrix.CreateRotationY(a)); }
        public Geo RotateZ(float a) { return Rotate(Matrix.CreateRotationZ(a)); }
        public Geo Rotate(Quaternion q) { return Rotate(Matrix.CreateFromQuaternion(q)); }

        // reverse the winding of every triangle
        public Geo Flip()
        {
            for (int i = 0; i + 2 < P.Count; i += 3)
            {
                Vector3 p = P[i + 1]; P[i + 1] = P[i + 2]; P[i + 2] = p;
                Vector3 n = N[i + 1]; N[i + 1] = N[i + 2]; N[i + 2] = n;
                Vector2 t = T[i + 1]; T[i + 1] = T[i + 2]; T[i + 2] = t;
                if (C != null) { Color c = C[i + 1]; C[i + 1] = C[i + 2]; C[i + 2] = c; }
            }
            return this;
        }

        // one normal per triangle (faceted look)
        public Geo FlatNormals()
        {
            for (int i = 0; i + 2 < P.Count; i += 3)
            {
                Vector3 n = Vector3.Cross(P[i + 1] - P[i], P[i + 2] - P[i]);
                float l = n.Length(); n = l > 1e-12f ? n / l : Vector3.Up;
                N[i] = N[i + 1] = N[i + 2] = n;
            }
            return this;
        }

        // ---- indexed scratch builder --------------------------------------------------------------------------------
        sealed class Grid
        {
            public readonly List<Vector3> p = new List<Vector3>(), n = new List<Vector3>();
            public readonly List<Vector2> t = new List<Vector2>();
            public readonly Geo g = new Geo();
            public int V(Vector3 pos, Vector3 nor, Vector2 uv) { p.Add(pos); n.Add(nor); t.Add(uv); return p.Count - 1; }
            public void Tri(int a, int b, int c) { g.Vert(p[a], n[a], t[a]); g.Vert(p[b], n[b], t[b]); g.Vert(p[c], n[c], t[c]); }
        }

        // ---- primitives ---------------------------------------------------------------------------------------------
        static readonly Vector3[][] BoxFaces =
        {
            new[] { new Vector3(1, 0, 0), new Vector3(0, 0, -1), new Vector3(0, 1, 0) },
            new[] { new Vector3(-1, 0, 0), new Vector3(0, 0, 1), new Vector3(0, 1, 0) },
            new[] { new Vector3(0, 1, 0), new Vector3(1, 0, 0), new Vector3(0, 0, -1) },
            new[] { new Vector3(0, -1, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1) },
            new[] { new Vector3(0, 0, 1), new Vector3(1, 0, 0), new Vector3(0, 1, 0) },
            new[] { new Vector3(0, 0, -1), new Vector3(-1, 0, 0), new Vector3(0, 1, 0) },
        };

        public static Geo Box(float w, float h, float d)
        {
            Geo g = new Geo();
            Vector3 half = new Vector3(w / 2, h / 2, d / 2);
            foreach (Vector3[] f in BoxFaces)
            {
                Vector3 n = f[0], u = f[1] * half, v = f[2] * half, c = n * half;
                Vector3 p0 = c - u - v, p1 = c + u - v, p2 = c + u + v, p3 = c - u + v;
                g.Vert(p0, n, new Vector2(0, 0)); g.Vert(p1, n, new Vector2(1, 0)); g.Vert(p2, n, new Vector2(1, 1));
                g.Vert(p0, n, new Vector2(0, 0)); g.Vert(p2, n, new Vector2(1, 1)); g.Vert(p3, n, new Vector2(0, 1));
            }
            return g;
        }

        // Axis along Y. Angle 0 faces +Z and grows towards +X.
        public static Geo Cylinder(float rt, float rb, float h, int seg = 12, bool open = false, float ts = 0, float tl = M.TAU)
        {
            Grid G = new Grid();
            float slope = (rb - rt) / h, half = h / 2;
            int[,] idx = new int[2, seg + 1];
            for (int y = 0; y <= 1; y++)
            {
                float radius = y * (rb - rt) + rt;
                for (int x = 0; x <= seg; x++)
                {
                    float u = (float)x / seg, th = u * tl + ts, s = M.Sin(th), c = M.Cos(th);
                    idx[y, x] = G.V(new Vector3(radius * s, -y * h + half, radius * c), Vector3.Normalize(new Vector3(s, slope, c)), new Vector2(u, 1 - y));
                }
            }
            for (int x = 0; x < seg; x++)
            {
                int a = idx[0, x], b = idx[1, x], c = idx[1, x + 1], d = idx[0, x + 1];
                if (rt > 0) G.Tri(a, b, d);
                if (rb > 0) G.Tri(b, c, d);
            }
            if (!open)
            {
                for (int top = 0; top < 2; top++)
                {
                    float radius = top == 1 ? rt : rb, sign = top == 1 ? 1 : -1;
                    if (radius <= 0) continue;
                    Vector3 n = new Vector3(0, sign, 0);
                    int centre = G.V(new Vector3(0, half * sign, 0), n, new Vector2(0.5f, 0.5f));
                    int first = -1;
                    for (int x = 0; x <= seg; x++)
                    {
                        float th = (float)x / seg * tl + ts, s = M.Sin(th), c = M.Cos(th);
                        int v = G.V(new Vector3(radius * s, half * sign, radius * c), n, new Vector2(c * 0.5f + 0.5f, s * 0.5f * sign + 0.5f));
                        if (x == 0) first = v;
                    }
                    for (int x = 0; x < seg; x++)
                    {
                        int i = first + x;
                        if (top == 1) G.Tri(i, i + 1, centre); else G.Tri(i + 1, i, centre);
                    }
                }
            }
            return G.g;
        }

        public static Geo Cone(float r, float h, int seg = 12) { return Cylinder(0, r, h, seg); }

        public static Geo Sphere(float r, int ws = 12, int hs = 8, float phiStart = 0, float phiLen = M.TAU, float thetaStart = 0, float thetaLen = M.PI)
        {
            Grid G = new Grid();
            float thetaEnd = Math.Min(thetaStart + thetaLen, M.PI);
            int[,] grid = new int[hs + 1, ws + 1];
            for (int iy = 0; iy <= hs; iy++)
            {
                float v = (float)iy / hs, th = thetaStart + v * thetaLen;
                for (int ix = 0; ix <= ws; ix++)
                {
                    float u = (float)ix / ws, ph = phiStart + u * phiLen;
                    Vector3 p = new Vector3(-r * M.Cos(ph) * M.Sin(th), r * M.Cos(th), r * M.Sin(ph) * M.Sin(th));
                    float l = p.Length();
                    grid[iy, ix] = G.V(p, l > 1e-9f ? p / l : new Vector3(0, iy == 0 ? 1 : -1, 0), new Vector2(u, 1 - v));
                }
            }
            for (int iy = 0; iy < hs; iy++)
                for (int ix = 0; ix < ws; ix++)
                {
                    int a = grid[iy, ix + 1], b = grid[iy, ix], c = grid[iy + 1, ix], d = grid[iy + 1, ix + 1];
                    if (iy != 0 || thetaStart > 0) G.Tri(a, b, d);
                    if (iy != hs - 1 || thetaEnd < M.PI - 1e-5f) G.Tri(b, c, d);
                }
            return G.g;
        }

        // In the XY plane, facing +Z.
        public static Geo Plane(float w, float h, int sx = 1, int sy = 1)
        {
            Grid G = new Grid();
            Vector3 n = new Vector3(0, 0, 1);
            for (int iy = 0; iy <= sy; iy++)
                for (int ix = 0; ix <= sx; ix++)
                    G.V(new Vector3(ix * w / sx - w / 2, h / 2 - iy * h / sy, 0), n, new Vector2((float)ix / sx, 1 - (float)iy / sy));
            int row = sx + 1;
            for (int iy = 0; iy < sy; iy++)
                for (int ix = 0; ix < sx; ix++)
                {
                    int a = ix + row * iy, b = ix + row * (iy + 1), c = ix + 1 + row * (iy + 1), d = ix + 1 + row * iy;
                    G.Tri(a, b, d); G.Tri(b, c, d);
                }
            return G.g;
        }

        // Ring in the XY plane around Z.
        public static Geo Torus(float radius, float tube, int radialSeg, int tubularSeg, float arc = M.TAU)
        {
            Grid G = new Grid();
            for (int j = 0; j <= radialSeg; j++)
                for (int i = 0; i <= tubularSeg; i++)
                {
                    float u = (float)i / tubularSeg * arc, v = (float)j / radialSeg * M.TAU;
                    Vector3 p = new Vector3((radius + tube * M.Cos(v)) * M.Cos(u), (radius + tube * M.Cos(v)) * M.Sin(u), tube * M.Sin(v));
                    Vector3 c = new Vector3(radius * M.Cos(u), radius * M.Sin(u), 0);
                    G.V(p, Vector3.Normalize(p - c), new Vector2((float)i / tubularSeg, (float)j / radialSeg));
                }
            for (int j = 1; j <= radialSeg; j++)
                for (int i = 1; i <= tubularSeg; i++)
                {
                    int a = (tubularSeg + 1) * j + i - 1, b = (tubularSeg + 1) * (j - 1) + i - 1, c = (tubularSeg + 1) * (j - 1) + i, d = (tubularSeg + 1) * j + i;
                    G.Tri(a, b, d); G.Tri(b, c, d);
                }
            return G.g;
        }

        public static Geo Octahedron(float r)
        {
            Vector3[] v = { new Vector3(1, 0, 0), new Vector3(-1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, -1, 0), new Vector3(0, 0, 1), new Vector3(0, 0, -1) };
            int[] idx = { 0, 2, 4, 0, 4, 3, 0, 3, 5, 0, 5, 2, 1, 2, 5, 1, 5, 3, 1, 3, 4, 1, 4, 2 };
            Geo g = new Geo();
            for (int i = 0; i < idx.Length; i++) g.Vert(v[idx[i]] * r, Vector3.Up, Vector2.Zero);
            return g.FlatNormals();
        }

        // Disc in the XY plane, facing +Z.
        public static Geo Circle(float r, int seg)
        {
            Geo g = new Geo();
            Vector3 n = new Vector3(0, 0, 1);
            for (int i = 0; i < seg; i++)
            {
                float a0 = (float)i / seg * M.TAU, a1 = (float)(i + 1) / seg * M.TAU;
                g.Vert(new Vector3(r * M.Cos(a0), r * M.Sin(a0), 0), n, new Vector2(M.Cos(a0) * 0.5f + 0.5f, M.Sin(a0) * 0.5f + 0.5f));
                g.Vert(new Vector3(r * M.Cos(a1), r * M.Sin(a1), 0), n, new Vector2(M.Cos(a1) * 0.5f + 0.5f, M.Sin(a1) * 0.5f + 0.5f));
                g.Vert(Vector3.Zero, n, new Vector2(0.5f, 0.5f));
            }
            return g;
        }

        // Flat ring in the XZ plane, facing +Y (both sides are drawn by the renderer).
        public static Geo Ring(float r0, float r1, int seg)
        {
            Geo g = new Geo();
            for (int i = 0; i < seg; i++)
            {
                float a0 = (float)i / seg * M.TAU, a1 = (float)(i + 1) / seg * M.TAU;
                Vector3 p0 = new Vector3(M.Cos(a0) * r0, 0, M.Sin(a0) * r0), p1 = new Vector3(M.Cos(a0) * r1, 0, M.Sin(a0) * r1);
                Vector3 p2 = new Vector3(M.Cos(a1) * r1, 0, M.Sin(a1) * r1), p3 = new Vector3(M.Cos(a1) * r0, 0, M.Sin(a1) * r0);
                g.Vert(p0, Vector3.Up, Vector2.Zero); g.Vert(p2, Vector3.Up, Vector2.Zero); g.Vert(p1, Vector3.Up, Vector2.Zero);
                g.Vert(p0, Vector3.Up, Vector2.Zero); g.Vert(p3, Vector3.Up, Vector2.Zero); g.Vert(p2, Vector3.Up, Vector2.Zero);
            }
            return g;
        }

        // A rounded rod from a to b (cylinder with a ball at each end).
        public static Geo Capsule(Vector3 a, Vector3 b, float r, int seg = 10)
        {
            Vector3 d = b - a; float len = d.Length();
            Geo g = Cylinder(r, r, len, seg, true);
            g.Add(Sphere(r, seg, 6).Translate(0, len / 2, 0)).Add(Sphere(r, seg, 6).Translate(0, -len / 2, 0));
            g.Rotate(FromUnitVectors(Vector3.Up, d / len)).Translate((a + b) * 0.5f);
            return g;
        }

        // The rotation that turns unit vector a onto unit vector b.
        public static Quaternion FromUnitVectors(Vector3 a, Vector3 b)
        {
            float r = Vector3.Dot(a, b) + 1;
            Quaternion q;
            if (r < 1e-6f)
            {
                q = Math.Abs(a.X) > Math.Abs(a.Z) ? new Quaternion(-a.Y, a.X, 0, 0) : new Quaternion(0, -a.Z, a.Y, 0);
            }
            else
            {
                Vector3 c = Vector3.Cross(a, b);
                q = new Quaternion(c.X, c.Y, c.Z, r);
            }
            q.Normalize();
            return q;
        }
    }

    // An indexed mesh with 16-bit indices: terrain tiles.
    public sealed class IndexedMesh
    {
        public Vector3[] P, N; public Color[] C; public Vector2[] T; public ushort[] Idx;
    }
}
