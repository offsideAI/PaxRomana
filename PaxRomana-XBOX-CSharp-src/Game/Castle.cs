// Burg Adlerstein, a German hill castle held by a wartime garrison (limestone curtain walls, round towers with
// witch-hat roofs, the keep with the vault, the armoury and a chapel), the garrison's camp, the river bridge, the
// broken wall at the sniper's nest, and the enrichment works in the distance.
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace PaxRomana
{
    // Collects geometry per material, with world-scale texture coordinates so masonry courses line up across pieces.
    public sealed class Builder
    {
        public readonly Dictionary<string, Geo> Parts = new Dictionary<string, Geo>();
        public readonly Mulberry Rng = new Mulberry(7);

        public Geo Add(string key, Geo g)
        {
            Geo dst;
            if (!Parts.TryGetValue(key, out dst)) { dst = new Geo(); Parts[key] = dst; }
            dst.Add(g);
            return g;
        }

        public Geo Box(string key, float w, float h, float d, float x, float y, float z, float ry = 0, float tile = 4, float rz = 0)
        {
            Geo g = Geo.Box(w, h, d);
            float off = Rng.Next() * tile;
            for (int i = 0; i < g.Count; i++)
            {
                Vector3 p = g.P[i], n = g.N[i]; float py = p.Y + y;
                if (Math.Abs(n.Y) > 0.5f) g.T[i] = new Vector2((p.X + off) / tile, (p.Z + off) / tile);
                else if (Math.Abs(n.X) > 0.5f) g.T[i] = new Vector2((p.Z + off) / tile, py / tile);
                else g.T[i] = new Vector2((p.X + off) / tile, py / tile);
            }
            if (rz != 0) g.RotateZ(rz);
            if (ry != 0) g.RotateY(ry);
            g.Translate(x, y, z);
            return Add(key, g);
        }

        public Geo Cyl(string key, float rt, float rb, float h, float x, float y, float z, int seg = 28, float tile = 4, bool open = false, float ts = 0, float tl = M.TAU)
        {
            Geo g = Geo.Cylinder(rt, rb, h, seg, open, ts, tl);
            float su = Math.Max(1, (float)Math.Round(tl * Math.Max(rt, rb) / tile)), sv = h / tile, vo = (y - h / 2) / tile;
            for (int i = 0; i < g.Count; i++) g.T[i] = new Vector2(g.T[i].X * su, g.T[i].Y * sv + vo);
            g.Translate(x, y, z);
            return Add(key, g);
        }

        // triangular prism (gabled roof or tent), ridge along Z unless ry is given
        public Geo Prism(string key, float w, float h, float len, float x, float y, float z, float ry = 0, float tile = 3)
        {
            float r = w / 1.732f;
            Geo g = Geo.Cylinder(r, r, len, 3);
            for (int i = 0; i < g.Count; i++) { Vector2 t = g.T[i]; g.T[i] = new Vector2(t.Y * len / tile, t.X * 3 * w / tile); }
            g.RotateX(-M.HALF_PI);
            g.Translate(0, r * 0.5f, 0);
            g.Scale(1, h / (r * 1.5f), 1);
            if (ry != 0) g.RotateY(ry);
            g.Translate(x, y, z);
            return Add(key, g);
        }
    }

    public sealed class Banner
    {
        public float W, H; public int Sx, Sy; public Vector3 Pos; public float RotY; public bool Flag;
    }

    public sealed class DoorLeaf { public Vector3 Pivot; public float W, H, Closed, Open, Angle; }

    public sealed class Door
    {
        public string Name; public Solid Solid; public float X, Z, Reach, Amt, Want; public bool Broken, Iron;
        public readonly List<DoorLeaf> Leaves = new List<DoorLeaf>();
    }

    public struct Gap { public float At, W, H; public Gap(float at, float w, float h) { At = at; W = w; H = h; } }

    public sealed class Castle
    {
        public const float P = Config.PLATEAU;
        public readonly Builder B = new Builder();
        public readonly List<Solid> Solids = new List<Solid>();
        public readonly List<Door> Doors = new List<Door>();
        public readonly List<Banner> Banners = new List<Banner>();
        public readonly List<Vector3> Smoke = new List<Vector3>(), Torches = new List<Vector3>();
        public float KeepX, KeepZ, KeepTop, KeepHalf, GateTop;
        public const float CapX = 0, CapZ = -14.2f, CapR = 2.3f;      // in front of the strongbox in the vault of the keep
        public const float PoleX = 0, PoleZ = 15;                      // the flagpole in the courtyard
        public Geo Keys;                                                // the key ring on the strongbox, drawn until it is taken
        public static readonly string[] ColliderKeys = { "stone", "plaster", "concrete", "slate", "gold", "timber", "tent", "tentDark", "iron" };

        void Slit(float x, float y, float z, float ry, float w = 0.28f, float h = 1.7f) { B.Box("dark", w, h, 0.3f, x, y, z, ry); }
        void Win(float x, float y, float z, float ry, float w = 1.0f, float h = 2.3f)
        {
            B.Box("dark", w, h, 0.36f, x, y, z, ry);
            B.Box("dark", w * 0.707f, w * 0.707f, 0.36f, x, y + h / 2, z, ry, 4, M.PI / 4);
            B.Box("stone", w + 0.5f, 0.22f, 0.5f, x, y - h / 2 - 0.1f, z, ry);
        }

        // A straight wall with doorways. alongX: runs from a to b in x, occupying c0..c1 in z (else the other way round).
        void Run(bool alongX, float a, float b, float c0, float c1, float y0, float y1, Gap[] gaps = null, string key = "stone")
        {
            float cur = a;
            if (gaps != null)
            {
                Gap[] sorted = (Gap[])gaps.Clone();
                Array.Sort(sorted, (p, q) => p.At.CompareTo(q.At));
                foreach (Gap g in sorted)
                {
                    Seg(alongX, cur, g.At - g.W / 2, c0, c1, y0, y1, true, key);
                    Seg(alongX, g.At - g.W / 2, g.At + g.W / 2, c0, c1, P + g.H, y1, false, key);      // lintel
                    cur = g.At + g.W / 2;
                }
            }
            Seg(alongX, cur, b, c0, c1, y0, y1, true, key);
        }
        void Seg(bool alongX, float u0, float u1, float c0, float c1, float ya, float yb, bool solid, string key)
        {
            if (u1 - u0 < 0.01f) return;
            float cm = (c0 + c1) / 2, um = (u0 + u1) / 2, ym = (ya + yb) / 2;
            if (alongX) B.Box(key, u1 - u0, yb - ya, c1 - c0, um, ym, cm); else B.Box(key, c1 - c0, yb - ya, u1 - u0, cm, ym, um);
            if (solid) Solids.Add(alongX ? Solid.Box(u0, u1, c0, c1) : Solid.Box(c0, c1, u0, u1));
        }

        // Hollow rectangular building. N is -z, S is +z.
        void Shell(float x0, float x1, float z0, float z1, float y0, float y1, float th, Gap[] n, Gap[] s, Gap[] e, Gap[] w, string key = "plaster")
        {
            Run(true, x0, x1, z1 - th, z1, y0, y1, s, key);
            Run(true, x0, x1, z0, z0 + th, y0, y1, n, key);
            Run(false, z0 + th, z1 - th, x0, x0 + th, y0, y1, w, key);
            Run(false, z0 + th, z1 - th, x1 - th, x1, y0, y1, e, key);
            B.Box("stone", x1 - x0 - th * 2, 0.1f, z1 - z0 - th * 2, (x0 + x1) / 2, P, (z0 + z1) / 2, 0, 2);      // flagstone floor
        }

        void Wall(float ax, float az, float bx, float bz, float nx, float nz)
        {
            float top = Config.WALK_Y;
            float len = M.Hypot(bx - ax, bz - az), cx = (ax + bx) / 2, cz = (az + bz) / 2;
            float ry = M.Atan2(-(bz - az), bx - ax), H = Config.WALL_H + 12;
            B.Box("stone", len, H, 3, cx, top - H / 2, cz, ry);
            B.Box("stone", len, 4, 4.2f, cx, P - 1.5f, cz, ry);                                   // battered plinth
            B.Box("stone", len, 0.45f, 0.5f, cx + nx * 1.7f, top - 0.25f, cz + nz * 1.7f, ry);     // corbel table
            B.Box("stone", len, 1.0f, 0.55f, cx + nx * 1.55f, top + 0.5f, cz + nz * 1.55f, ry);    // parapet
            B.Box("stone", len, 0.35f, 0.4f, cx - nx * 1.3f, top + 0.17f, cz - nz * 1.3f, ry);     // inner kerb
            Solids.Add(Solid.Box(Math.Min(ax, bx) - (nx != 0 ? 1.9f : 0), Math.Max(ax, bx) + (nx != 0 ? 1.9f : 0), Math.Min(az, bz) - (nz != 0 ? 1.9f : 0), Math.Max(az, bz) + (nz != 0 ? 1.9f : 0)));
            int n = (int)Math.Floor(len / 2.5f);
            for (int i = 0; i < n; i++)
            {
                float t = (i + 0.5f) / n;
                B.Box("stone", 1.45f, 0.95f, 0.55f, ax + (bx - ax) * t + nx * 1.55f, top + 1.47f, az + (bz - az) * t + nz * 1.55f, ry);
            }
        }

        // round tower with machicolations and a slate witch-hat roof
        void Tower(float x, float z, float r, float hAbove, float roofH, bool flag = true)
        {
            float y0 = P - 14, H = hAbove + 14, yt = P + hAbove;
            B.Cyl("stone", r, r, H, x, y0 + H / 2, z, 32);
            B.Cyl("stone", r, r + 1.1f, 7, x, P - 4, z, 32);
            int nc = (int)Math.Round(r * 3.6f);
            for (int i = 0; i < nc; i++)
            {
                float a = (float)i / nc * M.TAU;
                B.Box("stone", 0.5f, 1.0f, 0.9f, x + M.Sin(a) * (r + 0.3f), yt - 0.4f, z + M.Cos(a) * (r + 0.3f), a);
            }
            B.Cyl("stone", r + 0.75f, r + 0.75f, 3.0f, x, yt + 1.6f, z, 32);
            int nw = (int)Math.Round(r * 1.6f);
            for (int i = 0; i < nw; i++)
            {
                float a = (float)i / nw * M.TAU + 0.2f;
                B.Box("dark", 0.6f, 1.1f, 0.3f, x + M.Sin(a) * (r + 0.72f), yt + 1.9f, z + M.Cos(a) * (r + 0.72f), a);
            }
            B.Cyl("slate", r + 1.45f, r + 1.45f, 0.18f, x, yt + 3.15f, z, 32, 3);
            B.Cyl("slate", 0.05f, r + 1.35f, roofH, x, yt + 3.2f + roofH / 2, z, 32, 3);
            B.Cyl("iron", 0.05f, 0.07f, 3.4f, x, yt + 3.2f + roofH + 1.6f, z, 6);
            B.Add("iron", Geo.Sphere(0.16f, 8, 6).Translate(x, yt + 3.2f + roofH - 0.05f, z));
            for (int lv = 0; lv < 3; lv++)
                for (int i = 0; i < 6; i++)
                {
                    float a = i / 6f * M.TAU + lv * 0.5f;
                    Slit(x + M.Sin(a) * (r - 0.1f), P + 3 + lv * ((hAbove - 6) / 3), z + M.Cos(a) * (r - 0.1f), a);
                }
            if (flag) Banners.Add(new Banner { W = 2.6f, H = 1.3f, Sx = 10, Sy = 4, Pos = new Vector3(x, yt + 3.2f + roofH + 1.7f, z), RotY = -0.35f, Flag = true });
        }

        void Torch(float x, float y, float z, bool light = true)
        {
            B.Box("iron", 0.08f, 0.5f, 0.08f, x, y - 0.3f, z);
            B.Add("ember", Geo.Sphere(0.13f, 8, 6).Scale(1, 1.5f, 1).Translate(x, y, z));
            if (light) Torches.Add(new Vector3(x, y, z));
        }
        void Block(string key, float w, float h, float d, float x, float z, float y = 0)
        {
            B.Box(key, w, h, d, x, P + y + h / 2, z, 0, 2);
            Solids.Add(Solid.Box(x - w / 2, x + w / 2, z - d / 2, z + d / 2));
        }

        // Each leaf swings on a hinge; the door's solid blocks the doorway until the door is open.
        void AddDoor(string name, bool iron, float h, float x0, float x1, float z0, float z1, float[][] leaves)
        {
            Door d = new Door { Name = name, Iron = iron, Solid = Solid.Box(x0, x1, z0, z1), X = (x0 + x1) / 2, Z = (z0 + z1) / 2, Reach = 2.6f + Math.Max(x1 - x0, z1 - z0) / 2 };
            foreach (float[] l in leaves) d.Leaves.Add(new DoorLeaf { Pivot = new Vector3(l[0], P, l[1]), W = l[2], H = h, Closed = l[3], Open = l[4], Angle = l[3] });
            Solids.Add(d.Solid); Doors.Add(d);
        }

        public static Castle Build()
        {
            Castle c = new Castle();
            c.Make();
            return c;
        }

        void Make()
        {
            Wall(-45, 35, -5, 35, 0, 1); Wall(5, 35, 45, 35, 0, 1);
            Wall(-45, -35, 45, -35, 0, -1);
            Wall(45, -35, 45, 35, 1, 0); Wall(-45, -35, -45, 35, -1, 0);

            float[][] corners = { new[] { 45f, 35 }, new[] { -45f, 35 }, new[] { 45f, -35 }, new[] { -45f, -35 } };
            foreach (float[] t in corners) Tower(t[0], t[1], 7, 20, 13);
            Tower(46.5f, 0, 5, 17, 9, false); Tower(-46.5f, 0, 5, 17, 9, false); Tower(0, -36.5f, 5, 17, 9, false);
            foreach (float[] t in corners) Solids.Add(Solid.Circle(t[0], t[1], 7.8f));
            foreach (float[] t in new[] { new[] { 46.5f, 0 }, new[] { -46.5f, 0 }, new[] { 0, -36.5f }, new[] { -10.5f, 37.5f }, new[] { 10.5f, 37.5f } }) Solids.Add(Solid.Circle(t[0], t[1], 5.8f));

            // ---- gatehouse ----
            float gTop = P + 17;
            GateTop = gTop;
            Tower(-10.5f, 37.5f, 5, 19, 10); Tower(10.5f, 37.5f, 5, 19, 10);
            const float GW = 2.3f, GH = 5.2f;                                        // gateway half-width and height
            foreach (int sx in new[] { -1, 1 })
            {
                B.Box("stone", 6 - GW, 17 + 12, 7, sx * (6 + GW) / 2, gTop - 14.5f, 36.5f);
                Solids.Add(Solid.Box(sx > 0 ? GW : -6, sx > 0 ? 6 : -GW, 33, 40.5f));
            }
            B.Box("stone", GW * 2, 17 - GH, 7, 0, (gTop + P + GH) / 2, 36.5f);
            B.Box("stone", 12, 1.0f, 0.55f, 0, gTop + 0.5f, 39.75f);
            for (int i = 0; i < 4; i++) B.Box("stone", 1.45f, 0.95f, 0.55f, -4.1f + i * 2.75f, gTop + 1.47f, 39.75f);
            B.Box("stone", 12, 0.5f, 0.6f, 0, gTop - 0.3f, 40.2f);
            B.Add("dark", Geo.Cylinder(2.3f, 2.3f, 0.5f, 20).RotateX(M.HALF_PI).Translate(0, P + 5.2f, 39.85f));
            // portcullis, raised into the arch
            for (int i = -3; i <= 3; i++) B.Box("iron", 0.1f, 2.5f, 0.1f, i * 0.66f, P + GH + 1.2f, 40.15f);
            for (int i = 0; i < 2; i++) B.Box("iron", 4.5f, 0.1f, 0.1f, 0, P + GH + 0.5f + i * 1.1f, 40.15f);
            Slit(-3.5f, P + 11, 40, 0); Slit(3.5f, P + 11, 40, 0);
            Banners.Add(new Banner { W = 3, H = 6.4f, Sx = 6, Sy = 14, Pos = new Vector3(0, P + 12.2f, 40.35f) });

            // ---- the keep, with the vault on its ground floor ----
            const float KX = 0, KZ = -10, KW = 17, ROOM = 5; float kTop = P + 32, KH = KW / 2;
            KeepX = KX; KeepZ = KZ; KeepTop = kTop; KeepHalf = KH;
            B.Box("plaster", KW, 32 - ROOM, KW, KX, (kTop + P + ROOM) / 2, KZ);
            Shell(KX - KH, KX + KH, KZ - KH, KZ + KH, P - 4, P + ROOM, 1.5f, null, new[] { new Gap(KX, 1.8f, 2.8f) }, null, null);
            Run(true, KX - KH + 1.5f, KX + KH - 1.5f, KZ - 0.3f, KZ + 0.3f, P, P + ROOM, new[] { new Gap(KX, 1.6f, 2.6f) }, "plaster");   // vault wall
            B.Box("stone", KW + 1.2f, 0.6f, KW + 1.2f, KX, kTop - 0.3f, KZ);
            foreach (int[] d in new[] { new[] { 0, 1 }, new[] { 0, -1 }, new[] { 1, 0 }, new[] { -1, 0 } })
            {
                int nx = d[0], nz = d[1];
                float ry = nx != 0 ? M.HALF_PI : 0, o = KW / 2 + 0.25f;
                B.Box("stone", KW + 1.1f, 1.0f, 0.55f, KX + nx * o, kTop + 0.5f, KZ + nz * o, ry);
                for (int i = 0; i < 6; i++)
                {
                    float t = -6.4f + i * 2.56f;
                    B.Box("stone", 1.45f, 0.95f, 0.55f, KX + nx * o + (nx != 0 ? 0 : t), kTop + 1.47f, KZ + nz * o + (nz != 0 ? 0 : t), ry);
                }
                for (int lv = 0; lv < 3; lv++)
                    foreach (float s in new[] { -3.6f, 3.6f })
                        Win(KX + nx * (KW / 2) + (nx != 0 ? 0 : s), P + 9 + lv * 7.5f, KZ + nz * (KW / 2) + (nz != 0 ? 0 : s), nx != 0 ? nx * M.HALF_PI : (nz > 0 ? 0 : M.PI), 1.0f, 2.5f);
            }
            {
                Geo g = Geo.Cone(8.4f, 9, 4).RotateY(M.PI / 4);
                for (int i = 0; i < g.Count; i++) g.T[i] = new Vector2(g.T[i].X * 16, g.T[i].Y * 3.4f);
                B.Add("slate", g.Translate(KX, kTop + 4.5f, KZ));
            }
            B.Cyl("gold", 0.05f, 0.07f, 3, KX, kTop + 10.4f, KZ, 6);
            foreach (int[] d in new[] { new[] { 1, 1 }, new[] { 1, -1 }, new[] { -1, 1 }, new[] { -1, -1 } })
            {
                float bx = KX + d[0] * KW / 2, bz = KZ + d[1] * KW / 2;
                B.Cyl("stone", 1.9f, 0.3f, 3, bx, kTop - 5.5f, bz, 16);
                B.Cyl("stone", 1.9f, 1.9f, 7.5f, bx, kTop - 0.25f, bz, 16);
                B.Cyl("slate", 0.04f, 2.4f, 5, bx, kTop + 6, bz, 16, 3);
                for (int i = 0; i < 3; i++) { float a = M.Atan2(d[0], d[1]) + (i - 1) * 0.9f; Slit(bx + M.Sin(a) * 1.85f, kTop + 1.2f, bz + M.Cos(a) * 1.85f, a, 0.22f, 1.2f); }
            }
            foreach (float s in new[] { -4.5f, 4.5f }) Banners.Add(new Banner { W = 2.6f, H = 11, Sx = 6, Sy = 20, Pos = new Vector3(KX + s, kTop - 9, KZ + KW / 2 + 0.45f) });
            Banners.Add(new Banner { W = 4.2f, H = 2.1f, Sx = 12, Sy = 5, Pos = new Vector3(KX, kTop + 11, KZ), RotY = -0.35f, Flag = true });

            // ---- armoury (great hall) and chapel ----
            Shell(-43, -30, -28, 18, P - 1, P + 11, 1, null, null, new[] { new Gap(-5, 1.8f, 2.8f) }, null);
            B.Prism("slate", 14.4f, 7.5f, 47.5f, -36.5f, P + 11, -5);
            for (int i = 0; i < 6; i++) Win(-29.95f, P + 5.5f, -24 + i * 7.6f, M.HALF_PI, 1.1f, 3);
            B.Box("stone", 1.6f, 7, 1.6f, -36.5f, P + 17, 8); B.Box("stone", 1.6f, 7, 1.6f, -36.5f, P + 17, -18);
            Shell(30, 42, -32, -12, P - 1, P + 9, 1, null, null, null, new[] { new Gap(-22, 1.6f, 2.7f) });
            Solids.Add(Solid.Box(34.3f, 37.7f, -12.2f, -8.8f));
            B.Prism("slate", 13.2f, 7, 21.4f, 36, P + 9, -22);
            B.Box("stone", 3.4f, 20, 3.4f, 36, P + 9, -10.5f);
            // bell tower: louvred belfry, slate spire and a cross
            foreach (int[] d in new[] { new[] { 0, 1 }, new[] { 0, -1 }, new[] { 1, 0 }, new[] { -1, 0 } })
                B.Box("dark", d[0] != 0 ? 0.3f : 1.3f, 2.2f, d[1] != 0 ? 0.3f : 1.3f, 36 + d[0] * 1.62f, P + 16.6f, -10.5f + d[1] * 1.62f);
            B.Box("stone", 4.0f, 0.4f, 4.0f, 36, P + 19.2f, -10.5f);
            B.Add("slate", Geo.Cone(2.7f, 9, 4).RotateY(M.PI / 4).Translate(36, P + 23.9f, -10.5f));
            B.Box("gold", 0.1f, 1.7f, 0.1f, 36, P + 29.1f, -10.5f); B.Box("gold", 0.8f, 0.1f, 0.1f, 36, P + 29.4f, -10.5f);
            for (int i = 0; i < 3; i++) Win(36 + (i - 1) * 3.4f, P + 5, -11.95f, 0, 0.8f, 2.2f);
            Slit(36, P + 16, -8.75f, 0, 0.5f, 1.6f);

            // ---- furnishings ----
            // armoury: long table, benches, hearth
            B.Box("timber", 1.7f, 0.12f, 16, -36.5f, P + 0.92f, -5, 0, 2);
            foreach (float z in new[] { -12f, -5, 2 }) B.Box("timber", 1.3f, 0.86f, 0.2f, -36.5f, P + 0.43f, z, 0, 2);
            Solids.Add(Solid.Box(-37.35f, -35.65f, -13, 3));
            foreach (int sx in new[] { -1, 1 }) Block("timber", 0.45f, 0.5f, 14, -36.5f + sx * 1.6f, -5);
            B.Box("dark", 0.5f, 2.4f, 3.4f, -41.9f, P + 1.2f, 8); B.Box("stone", 0.7f, 0.4f, 4.4f, -41.8f, P + 2.6f, 8);
            B.Add("ember", Geo.Sphere(0.5f, 8, 6).Scale(0.5f, 0.3f, 1.6f).Translate(-41.6f, P + 0.2f, 8));
            Torch(-31.3f, P + 3.2f, 6, false); Torch(-31.3f, P + 3.2f, -16, false);
            Torches.Add(new Vector3(-37, P + 4.5f, -5));
            // chapel: altar, cross, pews
            Block("stone", 2.6f, 1.1f, 1.0f, 36, -29.4f);
            B.Box("gold", 0.12f, 1.5f, 0.12f, 36, P + 3.0f, -30.85f); B.Box("gold", 0.8f, 0.12f, 0.12f, 36, P + 3.3f, -30.85f);
            foreach (float z in new[] { -17f, -19.5f, -24.5f, -27 }) Block("timber", 5, 0.55f, 0.5f, 36.6f, z);
            Torches.Add(new Vector3(36, P + 4.2f, -22));
            // keep antechamber: drums and a weapon rack; vault: the strongbox on its plinth, and crates
            foreach (float[] t in new[] { new[] { -5.6f, -4.2f }, new[] { -4.4f, -4.4f }, new[] { -5.5f, -5.5f } }) { B.Cyl("timber", 0.45f, 0.4f, 1.0f, t[0], P + 0.5f, t[1], 12, 1.5f); Solids.Add(Solid.Circle(t[0], t[1], 0.48f)); }
            Block("timber", 2.6f, 1.9f, 0.35f, 5, -3.35f);
            for (int i = 0; i < 5; i++) B.Cyl("iron", 0.025f, 0.025f, 2.3f, 4 + i * 0.5f, P + 1.2f, -3.6f, 5);
            Torch(-6.7f, P + 2.9f, -6.5f); Torch(6.7f, P + 2.9f, -14);
            Block("stone", 0.9f, 1.0f, 0.9f, 0, -15.6f);
            B.Box("stone", 1.2f, 0.12f, 1.2f, 0, P + 1.04f, -15.6f);
            B.Box("iron", 0.62f, 0.34f, 0.42f, 0, P + 1.27f, -15.6f); B.Box("gold", 0.66f, 0.04f, 0.46f, 0, P + 1.3f, -15.6f);      // strongbox, banded
            Keys = new Geo();
            Keys.Add(Geo.Torus(0.07f, 0.012f, 6, 14).Translate(0, P + 1.46f, -15.6f));
            foreach (float a in new[] { -0.5f, 0, 0.5f })
                Keys.Add(Geo.Box(0.022f, 0.16f, 0.012f).Translate(0, -0.08f, 0).RotateZ(a).Translate(M.Sin(a) * 0.07f, P + 1.39f, -15.6f));
            foreach (float[] t in new[] { new[] { 5f, -16.2f }, new[] { 5.4f, -12.6f } }) { Block("timber", 1.3f, 0.75f, 0.8f, t[0], t[1]); B.Box("iron", 1.34f, 0.08f, 0.84f, t[0], P + 0.5f, t[1]); }
            // courtyard well
            B.Cyl("stone", 1.25f, 1.35f, 0.9f, -16, P + 0.45f, 16, 20); B.Add("dark", Geo.Cylinder(0.95f, 0.95f, 0.06f, 20).Translate(-16, P + 0.9f, 16));
            foreach (int sx in new[] { -1, 1 }) B.Box("timber", 0.16f, 2.2f, 0.16f, -16 + sx * 1.2f, P + 1.6f, 16, 0, 1.5f);
            B.Box("timber", 2.8f, 0.16f, 0.16f, -16, P + 2.7f, 16, 0, 1.5f);
            Solids.Add(Solid.Circle(-16, 16, 1.4f));

            // ---- bridge over the river ----
            {
                float bz = (Config.BRIDGE_Z0 + Config.BRIDGE_Z1) / 2, len = Config.BRIDGE_Z1 - Config.BRIDGE_Z0, bx = Config.BRIDGE_X, by = Config.BRIDGE_Y;
                B.Box("concrete", 5.4f, 1.0f, len, bx, by - 0.5f, bz);
                foreach (int s in new[] { -1, 1 }) B.Box("concrete", 0.45f, 1.0f, len, bx + s * 2.6f, by + 0.5f, bz);
                for (int i = 0; i < 5; i++)
                {
                    float z = Config.BRIDGE_Z0 + 8 + i * (len - 16) / 4;
                    B.Box("concrete", 6.4f, 9, 2.6f, bx, by - 5, z);
                    B.Box("concrete", 2.2f, 9, 2.2f, bx - 3.4f, by - 5.6f, z, M.PI / 4);
                    B.Box("concrete", 2.2f, 9, 2.2f, bx + 3.4f, by - 5.6f, z, M.PI / 4);
                }
            }

            // ---- ruined wall at the sniper's nest ----
            {
                float fx = -Config.SPAWN_X, fz = -Config.SPAWN_Z, fl = M.Hypot(fx, fz);
                float f0 = fx / fl, f1 = fz / fl, r0 = -f1, r1 = f0;
                float ry = M.Atan2(-r1, r0);
                float[] hs = { 1.5f, 1.0f, 0.75f, 0.45f, 0, 0, 0.5f, 0.8f, 1.25f };
                for (int k = 0; k < hs.Length; k++)
                {
                    float h = hs[k];
                    if (h == 0) continue;
                    float x = Config.SPAWN_X + f0 * 3.4f + r0 * (k - 4) * 1.35f, z = Config.SPAWN_Z + f1 * 3.4f + r1 * (k - 4) * 1.35f;
                    B.Box("concrete", 1.4f, h + 1.2f, 0.8f, x, Terrain.GroundAt(x, z) + h / 2 - 0.6f, z, ry + (B.Rng.Next() - 0.5f) * 0.08f);
                    Solids.Add(Solid.Circle(x, z, 0.6f));
                }
                for (int i = 0; i < 5; i++)
                {
                    float a = 2 + B.Rng.Next() * 3, b = (B.Rng.Next() - 0.5f) * 12;
                    float x = Config.SPAWN_X + f0 * a + r0 * b;
                    float a2 = 2 + B.Rng.Next() * 3, b2 = (B.Rng.Next() - 0.5f) * 12;
                    float z = Config.SPAWN_Z + f1 * a2 + r1 * b2;
                    B.Box("concrete", 0.6f, 0.4f, 0.45f, x, Terrain.GroundAt(x, z) + 0.1f, z, B.Rng.Next() * 3, 4, B.Rng.Next() * 0.4f);
                }
            }

            // ---- the garrison's camp ----
            {
                float cy = Config.CAMP_H, CXc = Config.CAMP_X, CZc = Config.CAMP_Z, CR = Config.CAMP_R;
                for (int gx = -4; gx <= 4; gx++)
                    for (int gz = -4; gz <= 4; gz++)
                    {
                        if (gx == 0 || gz == 0) continue;                  // the two lanes through the camp
                        float x = CXc + gx * 8.2f, z = CZc + gz * 8.2f;
                        if (M.Hypot(x - CXc, z - CZc) > CR - 9) continue;
                        B.Prism("tent", 3.4f, 2.4f, 4.2f, x, Terrain.GroundAt(x, z) - 0.05f, z, (B.Rng.Next() - 0.5f) * 0.12f, 2);
                        Solids.Add(Solid.Box(x - 1.6f, x + 1.6f, z - 2.1f, z + 2.1f));
                    }
                // command tent
                B.Prism("tentDark", 5.5f, 3.6f, 7, CXc - 9, cy, CZc - 9, M.HALF_PI, 2);
                Solids.Add(Solid.Box(CXc - 12.5f, CXc - 5.5f, CZc - 11.6f, CZc - 6.4f));
                Solids.Add(Solid.Ring(CXc, CZc, CR, 0.075f));        // stockade, gateway to the south
                int n = (int)Math.Floor(M.TAU * CR / 0.52f);
                for (int i = 0; i < n; i++)
                {
                    float a = (float)i / n * M.TAU;
                    if (Math.Abs(a) < 0.085f || Math.Abs(a - M.TAU) < 0.085f) continue;
                    float x = CXc + M.Sin(a) * CR, z = CZc + M.Cos(a) * CR, g = Terrain.GroundAt(x, z);
                    float h = 2.5f + B.Rng.Next() * 0.5f, rr = 0.15f + B.Rng.Next() * 0.05f;
                    B.Cyl("timber", rr, rr, h, x, g + h / 2 - 0.3f, z, 6, 1.5f);
                    B.Cyl("timber", 0.01f, rr, 0.45f, x, g + h - 0.08f, z, 6, 1.5f);
                }
                // watchtower
                float tx = CXc + 26, tz = CZc + 34, tg = Terrain.GroundAt(tx, tz);
                foreach (int[] d in new[] { new[] { 1, 1 }, new[] { 1, -1 }, new[] { -1, 1 }, new[] { -1, -1 } })
                {
                    Solids.Add(Solid.Circle(tx + d[0] * 1.6f, tz + d[1] * 1.6f, 0.25f));
                    B.Box("timber", 0.3f, 10.6f, 0.3f, tx + d[0] * 1.6f, tg + 5.3f, tz + d[1] * 1.6f, 0, 1.5f);
                    B.Box("timber", 0.14f, 4.6f, 0.14f, tx + d[0] * 1.6f, tg + 3.4f, tz, 0, 1.5f, d[0] * d[1] * 0.75f);
                }
                B.Box("timber", 3.9f, 0.25f, 3.9f, tx, tg + 7, tz, 0, 1.5f);
                foreach (int[] d in new[] { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } })
                    B.Box("timber", d[0] != 0 ? 0.12f : 3.7f, 0.12f, d[1] != 0 ? 0.12f : 3.7f, tx + d[0] * 1.8f, tg + 8.05f, tz + d[1] * 1.8f, 0, 1.5f);
                B.Add("timber", Geo.Cone(3.2f, 1.6f, 4).RotateY(M.PI / 4).Translate(tx, tg + 11.3f, tz));
                // flagpole
                B.Cyl("timber", 0.05f, 0.06f, 5.2f, CXc, cy + 2.6f, CZc + 3, 6, 1.5f);
                Solids.Add(Solid.Circle(CXc, CZc + 3, 0.15f));
                Banners.Add(new Banner { W = 2.4f, H = 1.2f, Sx = 8, Sy = 4, Pos = new Vector3(CXc, cy + 4.5f, CZc + 3), RotY = -0.35f, Flag = true });
                // camp fires
                foreach (float[] t in new[] { new[] { 6f, 6 }, new[] { -14f, 12 } })
                {
                    float x = CXc + t[0], z = CZc + t[1];
                    for (int i = 0; i < 9; i++) { float a = i / 9f * M.TAU; B.Box("stone", 0.35f, 0.3f, 0.3f, x + M.Sin(a) * 0.7f, cy + 0.1f, z + M.Cos(a) * 0.7f, a); }
                    B.Add("ember", Geo.Sphere(0.42f, 8, 6).Scale(1, 0.45f, 1).Translate(x, cy + 0.15f, z));
                    Smoke.Add(new Vector3(x, cy + 0.5f, z));
                    Solids.Add(Solid.Circle(x, z, 0.85f));
                }
            }
            Smoke.Add(new Vector3(-36.5f, P + 21, 8)); Smoke.Add(new Vector3(-36.5f, P + 21, -18));

            // ---- doors ----
            const float HP = M.HALF_PI;
            AddDoor("the castle gate", false, GH, -GW, GW, 39.2f, 39.8f, new[] { new[] { -GW, 39.5f, GW, 0, HP }, new[] { GW, 39.5f, GW, M.PI, HP } });
            AddDoor("the armoury door", false, 2.8f, -30.8f, -30.2f, -5.9f, -4.1f, new[] { new[] { -30.5f, -5.9f, 1.8f, -HP, -M.PI } });
            AddDoor("the chapel door", false, 2.7f, 30.2f, 30.8f, -22.8f, -21.2f, new[] { new[] { 30.5f, -22.8f, 1.6f, -HP, 0 } });
            AddDoor("the keep door", false, 2.8f, KX - 0.9f, KX + 0.9f, KZ + KH - 1.05f, KZ + KH - 0.45f, new[] { new[] { KX - 0.9f, KZ + KH - 0.75f, 1.8f, 0, HP } });
            AddDoor("the vault door", true, 2.6f, KX - 0.8f, KX + 0.8f, KZ - 0.3f, KZ + 0.3f, new[] { new[] { KX - 0.8f, KZ, 1.6f, 0, HP } });

            // ---- the flagpole in the courtyard ----
            B.Add("iron", Geo.Cylinder(0.09f, 0.12f, 12, 8).Translate(PoleX, P + 6, PoleZ));
            Solids.Add(Solid.Circle(PoleX, PoleZ, 0.2f));
            Banners.Add(new Banner { W = 4.6f, H = 2.3f, Sx = 12, Sy = 5, Pos = new Vector3(PoleX, P + 10.6f, PoleZ), RotY = -0.35f, Flag = true });
        }

        // Puts the doors back to how a mission starts.
        public void Reset()
        {
            foreach (Door d in Doors)
            {
                d.Amt = d.Want = 0; d.Broken = false; d.Solid.Off = false;
                foreach (DoorLeaf l in d.Leaves) l.Angle = l.Closed;
            }
        }
    }

    // The U-235 enrichment works on its own levelled ground north-east of the valley: process halls, chimney stacks,
    // cooling towers, tanks and a walled perimeter. Scenery in this level (it is outside the playable valley and has
    // no collision); the keys in the castle vault open its gates.
    public sealed class Works
    {
        public readonly Builder B = new Builder();
        public readonly List<Vector3> Stacks = new List<Vector3>();      // chimney tops

        public static Works Build()
        {
            Works c = new Works();
            Builder B = c.B; Mulberry rng = new Mulberry(235);
            const float X = Config.WORKS_X, Z = Config.WORKS_Z, G = Config.WORKS_H, TILE = 16;
            // process halls in two rows, long axis east-west, with a monitor roof along each ridge
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < 3; i++)
                {
                    float w = 120 + rng.Next() * 30, h = 20 + rng.Next() * 8, d = 34, x = X - 150 + i * 150 + (rng.Next() - 0.5f) * 14, z = Z - 55 + row * 105;
                    B.Box("works", w, h + 6, d, x, G + h / 2 - 3, z, 0, TILE);
                    B.Prism("slate", d + 2, 6, w + 2, x, G + h, z, M.HALF_PI, 6);
                    B.Box("works", w * 0.8f, 3.5f, 7, x, G + h + 5.2f, z, 0, TILE);
                }
            // the cascade building: taller, windowless
            B.Box("concrete", 70, 44, 46, X + 30, G + 19, Z - 140, 0, 8);
            B.Box("concrete", 22, 12, 22, X + 30, G + 47, Z - 140, 0, 8);
            // chimney stacks
            foreach (float[] t in new[] { new[] { -190f, -130 }, new[] { -120f, -150 }, new[] { 150f, -135 }, new[] { 205f, 10 }, new[] { -205f, 60 } })
            {
                float h = 70 + rng.Next() * 30, x = X + t[0], z = Z + t[1];
                B.Cyl("brick", 2.4f, 4.4f, h, x, G + h / 2, z, 14, 6);
                B.Cyl("concrete", 2.9f, 2.9f, 1.4f, x, G + h - 3, z, 14, 6);
                c.Stacks.Add(new Vector3(x, G + h, z));
            }
            // cooling towers: a waisted concrete shell
            foreach (float[] t in new[] { new[] { 120f, 130 }, new[] { 185f, 115 } })
            {
                float x = X + t[0], z = Z + t[1];
                B.Cyl("concrete", 19, 28, 34, x, G + 17, z, 28, 10);
                B.Cyl("concrete", 22, 19, 20, x, G + 44, z, 28, 10);
            }
            // tanks and a water tower
            for (int i = 0; i < 4; i++) B.Cyl("concrete", 11, 11, 13, X - 190 + i * 30, G + 6.5f, Z + 135, 20, 8);
            B.Cyl("concrete", 1.6f, 2.2f, 30, X - 60, G + 15, Z + 150, 10, 6); B.Cyl("concrete", 7.5f, 5, 9, X - 60, G + 33, Z + 150, 18, 6);
            // perimeter wall with watch towers at the corners and a gatehouse on the valley side
            const float PW = 250, PD = 195;
            foreach (int sgn in new[] { -1, 1 })
            {
                B.Box("concrete", PW * 2, 7, 1.6f, X, G + 2.5f, Z + sgn * PD, 0, 8);
                B.Box("concrete", 1.6f, 7, PD * 2, X + sgn * PW, G + 2.5f, Z, 0, 8);
                foreach (int s2 in new[] { -1, 1 })
                {
                    B.Box("concrete", 7, 19, 7, X + sgn * PW, G + 8.5f, Z + s2 * PD, 0, 8);
                    B.Add("slate", Geo.Cone(6.2f, 4, 4).RotateY(M.PI / 4).Translate(X + sgn * PW, G + 20, Z + s2 * PD));
                }
            }
            B.Box("concrete", 26, 13, 9, X - 40, G + 5.5f, Z + PD, 0, 8);
            B.Box("dark", 9, 7.5f, 9.4f, X - 40, G + 2.7f, Z + PD);
            return c;
        }
    }
}
