// Procedural textures and the baked fonts. Everything is generated in memory at load: there are no image assets.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Microsoft.Xna.Framework;

namespace PaxRomana
{
    // A CPU-side image (straight alpha, row 0 at the top).
    public sealed class Tex
    {
        public int W, H; public Color[] Px;
        public bool Repeat = true, Mips = true;
        public Tex(int w, int h) { W = w; H = h; Px = new Color[w * h]; }
    }

    public struct Glyph { public int X, Y, W, H, XOff, YOff; public float Adv; }

    public sealed class FontFace
    {
        public string Name; public int Size, Ascent, Descent, W, H; public byte[] Alpha;
        public readonly Dictionary<char, Glyph> Glyphs = new Dictionary<char, Glyph>();

        public float Measure(string s, float px)
        {
            float k = px / Size, w = 0;
            for (int i = 0; i < s.Length; i++) { Glyph g; if (Glyphs.TryGetValue(s[i], out g)) w += g.Adv; }
            return w * k;
        }

        static FontFace[] all;
        public static FontFace Get(string name)
        {
            if (all == null) all = Load();
            foreach (FontFace f in all) if (f.Name == name) return f;
            return all[0];
        }

        static FontFace[] Load()
        {
            byte[] packed = Convert.FromBase64String(string.Concat(FontData.Chunks));
            using (MemoryStream src = new MemoryStream(packed))
            using (GZipStream gz = new GZipStream(src, CompressionMode.Decompress))
            using (MemoryStream raw = new MemoryStream())
            {
                gz.CopyTo(raw);
                raw.Position = 0;
                BinaryReader r = new BinaryReader(raw);
                int n = r.ReadByte();
                FontFace[] fonts = new FontFace[n];
                for (int i = 0; i < n; i++)
                {
                    FontFace f = new FontFace();
                    f.Name = Encoding.ASCII.GetString(r.ReadBytes(r.ReadByte()));
                    f.Size = r.ReadUInt16(); f.Ascent = r.ReadUInt16(); f.Descent = r.ReadUInt16(); f.W = r.ReadUInt16(); f.H = r.ReadUInt16();
                    int count = r.ReadUInt16();
                    for (int k = 0; k < count; k++)
                    {
                        char ch = (char)r.ReadUInt16();
                        Glyph g = new Glyph();
                        g.X = r.ReadUInt16(); g.Y = r.ReadUInt16(); g.W = r.ReadUInt16(); g.H = r.ReadUInt16();
                        g.XOff = r.ReadInt16(); g.YOff = r.ReadInt16(); g.Adv = r.ReadSingle();
                        f.Glyphs[ch] = g;
                    }
                    f.Alpha = r.ReadBytes(f.W * f.H);
                    fonts[i] = f;
                }
                return fonts;
            }
        }
    }

    // Minimal 2D painter over a Tex: enough to draw leaves, the banner and the skyline windows.
    public sealed class Canvas
    {
        public readonly Tex T;
        public Canvas(int w, int h, Color clear) { T = new Tex(w, h); for (int i = 0; i < T.Px.Length; i++) T.Px[i] = clear; }

        public void Blend(int x, int y, float r, float g, float b, float a)
        {
            if (x < 0 || y < 0 || x >= T.W || y >= T.H || a <= 0) return;
            if (a > 1) a = 1;
            Color d = T.Px[y * T.W + x];
            float da = d.A / 255f, oa = a + da * (1 - a);
            if (oa <= 0) return;
            float k = da * (1 - a);
            T.Px[y * T.W + x] = new Color(
                (byte)M.Clamp((r * 255 * a + d.R * k) / oa, 0, 255), (byte)M.Clamp((g * 255 * a + d.G * k) / oa, 0, 255),
                (byte)M.Clamp((b * 255 * a + d.B * k) / oa, 0, 255), (byte)M.Clamp(oa * 255, 0, 255));
        }

        public void Rect(float x, float y, float w, float h, Vector3 c, float a = 1)
        {
            int x0 = (int)Math.Round(x), y0 = (int)Math.Round(y), x1 = (int)Math.Round(x + w), y1 = (int)Math.Round(y + h);
            for (int j = y0; j < y1; j++) for (int i = x0; i < x1; i++) Blend(i, j, c.X, c.Y, c.Z, a);
        }

        public void Ellipse(float cx, float cy, float rx, float ry, float rot, Vector3 c, float a = 1)
        {
            float rm = Math.Max(rx, ry) + 1, cs = M.Cos(rot), sn = M.Sin(rot);
            for (int j = (int)(cy - rm); j <= (int)(cy + rm); j++)
                for (int i = (int)(cx - rm); i <= (int)(cx + rm); i++)
                {
                    float dx = i + 0.5f - cx, dy = j + 0.5f - cy;
                    float u = (dx * cs + dy * sn) / rx, v = (-dx * sn + dy * cs) / ry;
                    float d = M.Sqrt(u * u + v * v);
                    float cov = M.Clamp((1 - d) * Math.Min(rx, ry) + 0.5f);
                    if (cov > 0) Blend(i, j, c.X, c.Y, c.Z, a * cov);
                }
        }

        public void Line(float x0, float y0, float x1, float y1, float width, Vector3 c, float a = 1)
        {
            float hw = width / 2, dx = x1 - x0, dy = y1 - y0, len2 = dx * dx + dy * dy;
            int ax = (int)(Math.Min(x0, x1) - hw - 1), bx = (int)(Math.Max(x0, x1) + hw + 1), ay = (int)(Math.Min(y0, y1) - hw - 1), by = (int)(Math.Max(y0, y1) + hw + 1);
            for (int j = ay; j <= by; j++)
                for (int i = ax; i <= bx; i++)
                {
                    float px = i + 0.5f - x0, py = j + 0.5f - y0;
                    float t = len2 > 0 ? M.Clamp((px * dx + py * dy) / len2) : 0;
                    float d = M.Hypot(px - dx * t, py - dy * t);
                    float cov = M.Clamp(hw - d + 0.5f);
                    if (cov > 0) Blend(i, j, c.X, c.Y, c.Z, a * cov);
                }
        }

        // convex or concave polygon, even-odd fill
        public void Poly(float[] xy, Vector3 c, float a = 1)
        {
            int n = xy.Length / 2;
            float minY = 1e9f, maxY = -1e9f;
            for (int i = 0; i < n; i++) { minY = Math.Min(minY, xy[i * 2 + 1]); maxY = Math.Max(maxY, xy[i * 2 + 1]); }
            List<float> xs = new List<float>();
            for (int j = (int)minY; j <= (int)maxY; j++)
            {
                float y = j + 0.5f; xs.Clear();
                for (int i = 0; i < n; i++)
                {
                    float ax = xy[i * 2], ay = xy[i * 2 + 1], bx = xy[((i + 1) % n) * 2], by = xy[((i + 1) % n) * 2 + 1];
                    if ((ay <= y && by > y) || (by <= y && ay > y)) xs.Add(ax + (y - ay) / (by - ay) * (bx - ax));
                }
                xs.Sort();
                for (int k = 0; k + 1 < xs.Count; k += 2)
                    for (int i = (int)Math.Round(xs[k]); i < (int)Math.Round(xs[k + 1]); i++) Blend(i, j, c.X, c.Y, c.Z, a);
            }
        }

        // text centred on cx with its baseline at y
        public void Text(FontFace f, string s, float cx, float baseline, float px, Vector3 c)
        {
            float k = px / f.Size, x = cx - f.Measure(s, px) / 2;
            foreach (char ch in s)
            {
                Glyph g;
                if (!f.Glyphs.TryGetValue(ch, out g)) continue;
                int w = (int)Math.Ceiling(g.W * k), h = (int)Math.Ceiling(g.H * k);
                float ox = x + g.XOff * k, oy = baseline - f.Ascent * k + g.YOff * k;
                for (int j = 0; j < h; j++)
                    for (int i = 0; i < w; i++)
                    {
                        int sx = Math.Min(g.W - 1, (int)(i / k)), sy = Math.Min(g.H - 1, (int)(j / k));
                        float al = f.Alpha[(g.Y + sy) * f.W + g.X + sx] / 255f;
                        if (al > 0) Blend((int)ox + i, (int)oy + j, c.X, c.Y, c.Z, al);
                    }
                x += g.Adv * k;
            }
        }
    }

    public sealed class Textures
    {
        public Tex Stone, Slate, Ground, Rock, Bark, Timber, Leather, Water, Leaves, Banner, Windows, Cloud, Soft, Glow, Scope, Vignette, White;

        delegate void Fn(float u, float v, out float r, out float g, out float b, out float h);

        // fn(u, v) gives a colour and a height; the height is turned into a little baked relief, lit from the upper left.
        static Tex Gen(int size, Fn fn, float relief)
        {
            Tex t = new Tex(size, size);
            float[] hgt = new float[size * size], col = new float[size * size * 3];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r, g, b, h;
                    fn((float)x / size, (float)y / size, out r, out g, out b, out h);
                    int i = y * size + x;
                    col[i * 3] = r; col[i * 3 + 1] = g; col[i * 3 + 2] = b; hgt[i] = h;
                }
            for (int y = 0; y < size; y++)
            {
                int yu = ((y - 1 + size) % size) * size, yd = ((y + 1) % size) * size;
                for (int x = 0; x < size; x++)
                {
                    int xl = (x - 1 + size) % size, xr = (x + 1) % size, i = y * size + x;
                    float dx = hgt[y * size + xr] - hgt[y * size + xl], dy = hgt[yd + x] - hgt[yu + x];
                    float k = M.Clamp(1 - (dx + dy) * relief, 0.45f, 1.6f);
                    t.Px[i] = M.Col(col[i * 3] * k, col[i * 3 + 1] * k, col[i * 3 + 2] * k);
                }
            }
            return t;
        }

        static float H(int a, int b, int s) { return (float)Noise.Hash2(a, b, s); }

        // Ashlar courses; tinted limestone grey, cream or brick red by the material that uses it. One tile = 4 m.
        static Tex MakeStone(int size)
        {
            const int rows = 12;
            return Gen(size, (float u, float v, out float r, out float g, out float b, out float h) =>
            {
                int row = (int)Math.Floor(v * rows); float fv = v * rows - row;
                int cols = H(row, 3, 5) > 0.5f ? 7 : 5;
                float bu = u * cols + H(row, 7, 5);
                int colI = (int)Math.Floor(bu); float fu = bu - colI;
                float id = H(((colI % cols) + cols) % cols, row, 9);
                float ex = Math.Min(fu, 1 - fu) / cols * size, ey = Math.Min(fv, 1 - fv) / rows * size;
                float e = Math.Min(ex, ey);
                float mortar = M.Smoothstep(1.2f, 4.5f, e * 1024f / size);
                float grain = Noise.Fbm(u * 48, v * 48, 3, 21, 48);
                float stain = Noise.Fbm(u * 3, v * 5, 4, 31, 3);
                float drip = Noise.Fbm(u * 24, v * 2, 2, 41, 2);
                float k = (0.82f + id * 0.3f) * (0.86f + grain * 0.28f) * (0.72f + stain * 0.4f) * (0.9f + drip * 0.14f);
                r = 0.80f * k; g = 0.735f * k; b = 0.615f * k;
                if (id > 0.82f) { r *= 0.9f; g *= 0.92f; b *= 0.98f; }
                float m = 1 - mortar;
                r = r * mortar + 0.42f * m; g = g * mortar + 0.37f * m; b = b * mortar + 0.3f * m;
                h = mortar * (0.75f + id * 0.25f) + grain * 0.22f;
            }, 0.55f);
        }

        static Tex MakeSlate(int size)
        {
            const int rows = 12, cols = 9;
            return Gen(size, (float u, float v, out float r, out float g, out float b, out float h) =>
            {
                int row = (int)Math.Floor(v * rows); float fv = v * rows - row;
                float bu = u * cols + (row % 2) * 0.5f;
                int colI = (int)Math.Floor(bu); float fu = bu - colI;
                float id = H(((colI % cols) + cols) % cols, row, 13);
                float gap = M.Smoothstep(0.0f, 0.06f, Math.Min(fu, 1 - fu));
                float lip = M.Smoothstep(0.0f, 0.12f, 1 - fv);
                float n = Noise.Fbm(u * 30, v * 30, 3, 5, 30);
                float k = (0.7f + id * 0.55f) * (0.85f + n * 0.3f) * (0.55f + 0.45f * gap) * (0.6f + 0.4f * lip) * 2.6f;
                r = 0.23f * k; g = 0.265f * k; b = 0.33f * k; h = fv * 0.8f * gap + n * 0.1f;
            }, 0.25f);
        }

        // Ground cover: a light, mottled pattern of clumps and blades, multiplied by the terrain's vertex colours.
        static Tex MakeGround(int size)
        {
            return Gen(size, (float u, float v, out float r, out float g, out float b, out float h) =>
            {
                float n = Noise.Fbm(u * 6, v * 6, 4, 3, 6), s = Noise.Fbm(u * 48, v * 48, 2, 8, 48), f = Noise.Fbm(u * 110, v * 110, 2, 18, 110);
                float k = 0.72f + n * 0.2f + s * 0.16f + f * 0.12f;
                r = k * (0.98f + n * 0.06f); g = k; b = k * (0.96f - n * 0.05f); h = n * 0.4f + s * 0.4f + f * 0.4f;
            }, 0.7f);
        }

        static Tex MakeRock(int size)
        {
            return Gen(size, (float u, float v, out float r, out float g, out float b, out float h) =>
            {
                float gg = Noise.Fbm(u * 6, v * 6, 5, 4, 6);
                float cr = Math.Abs(Noise.Fbm(u * 5 + gg * 0.6f, v * 5, 4, 14, 5) - 0.5f) * 2;
                float crack = M.Smoothstep(0.0f, 0.07f, cr);
                float k = (0.55f + 0.7f * gg) * (0.55f + 0.45f * crack);
                r = 0.6f * k; g = 0.56f * k; b = 0.5f * k; h = gg * crack;
            }, 0.8f);
        }

        static Tex MakeBark(int size)
        {
            return Gen(size, (float u, float v, out float r, out float g, out float b, out float h) =>
            {
                float n = Noise.Fbm(u * 10, v * 2, 4, 2, 2);
                float ridge = Math.Abs(M.Sin((u * 14 + n * 2.5f) * M.PI));
                float k = 0.45f + ridge * 0.6f + Noise.Fbm(u * 40, v * 40, 2, 9, 40) * 0.2f;
                r = 0.30f * k; g = 0.24f * k; b = 0.18f * k; h = ridge;
            }, 0.4f);
        }

        static Tex MakeTimber(int size)
        {
            return Gen(size, (float u, float v, out float r, out float g, out float b, out float h) =>
            {
                float w = Noise.Fbm(u * 3, v * 14, 4, 52, 3);
                float k = (0.6f + 0.5f * w + 0.25f * Math.Abs(M.Sin(u * M.PI * 8))) * 1.5f;
                r = 0.36f * k; g = 0.27f * k; b = 0.18f * k; h = w;
            }, 0.5f);
        }

        static Tex MakeLeather(int size)
        {
            return Gen(size, (float u, float v, out float r, out float g, out float b, out float h) =>
            {
                float n = Noise.Fbm(u * 6, v * 6, 4, 33, 6), w = Noise.Fbm(u * 90, v * 90, 2, 43, 90);
                float seam = M.Smoothstep(0.0f, 0.02f, Math.Abs(((u * 4) % 1) - 0.5f));
                float k = (0.75f + n * 0.4f) * (0.9f + w * 0.2f) * (0.7f + 0.3f * seam);
                r = 0.66f * k; g = 0.56f * k; b = 0.42f * k; h = n * 0.5f + w * 0.5f;
            }, 0.4f);
        }

        // ripples on the river: a brightness pattern multiplied into the water colour
        static Tex MakeWater(int size)
        {
            return Gen(size, (float u, float v, out float r, out float g, out float b, out float h) =>
            {
                h = Noise.Fbm(u * 8, v * 8, 4, 77, 8);
                float k = 0.78f + 0.3f * Noise.Fbm(u * 16, v * 5, 3, 78, 1);
                r = g = b = k;
            }, 2.2f);
        }

        // sunlit leaves: a share of each sprite is pulled towards a bright yellow-green
        static Vector3 Frost(Vector3 c, float k) { return Vector3.Lerp(c, new Vector3(0.56f, 0.70f, 0.24f), k * 0.55f); }

        static Vector3 Hsl(float h, float s, float l)
        {
            h = ((h % 360) + 360) % 360 / 360f; s /= 100; l /= 100;
            float q = l < 0.5f ? l * (1 + s) : l + s - l * s, p = 2 * l - q;
            return new Vector3(Hue(p, q, h + 1f / 3), Hue(p, q, h), Hue(p, q, h - 1f / 3));
        }
        static float Hue(float p, float q, float t)
        {
            if (t < 0) t += 1; if (t > 1) t -= 1;
            if (t < 1f / 6) return p + (q - p) * 6 * t;
            if (t < 0.5f) return q;
            if (t < 2f / 3) return p + (q - p) * (2f / 3 - t) * 6;
            return p;
        }

        // Three foliage sprites side by side in one 1024 x 256 atlas: broadleaf, pine needles, cypress. The fourth tile is unused.
        static Tex MakeLeaves()
        {
            const int S = 256;
            Canvas c = new Canvas(S * 4, S, new Color(58, 92, 40, 0));
            for (int kind = 0; kind < 3; kind++)
            {
                Mulberry r = new Mulberry((uint)(kind + 1));
                float ox = kind * S;
                if (kind == 1)
                {
                    for (int i = 0; i < 30; i++)
                    {
                        float a = r.Next() * M.TAU, d = M.Sqrt(r.Next()) * 88, cx = S / 2 + M.Cos(a) * d, cy = S / 2 + M.Sin(a) * d;
                        for (int k = 0; k < 26; k++)
                        {
                            float an = r.Next() * M.TAU, len = 18 + r.Next() * 24;
                            Vector3 col = Frost(Hsl(92 + r.Next() * 30, 34 + r.Next() * 20, 15 + r.Next() * 16), 0.18f + r.Next() * 0.3f);
                            float lw = 1.6f + r.Next();
                            c.Line(ox + cx, cy, ox + cx + M.Cos(an) * len, cy + M.Sin(an) * len, lw, col);
                        }
                    }
                }
                else
                {
                    float hue = kind == 2 ? 105 : 78;
                    for (int i = 0; i < 120; i++)
                    {
                        float a = r.Next() * M.TAU, d = M.Pow(r.Next(), 0.6f) * 104;
                        float x = S / 2 + M.Cos(a) * d, y = S / 2 + M.Sin(a) * d;
                        float light = kind == 2 ? 12 + r.Next() * 14 : 20 + r.Next() * 20 + (i > 80 ? 6 : 0);
                        Vector3 col = Frost(Hsl(hue + r.Next() * 34, 44 + r.Next() * 24, light), (kind == 2 ? 0.15f : 0.3f) + r.Next() * 0.35f);
                        float rx = 9 + r.Next() * 8, ry = 4.5f + r.Next() * 4, rot = r.Next() * 3.14f;
                        c.Ellipse(ox + x, y, rx, ry, rot, col);
                    }
                }
            }
            c.T.Repeat = false;
            return c.T;
        }

        // The garrison's flag: a red field with a white disc and a black emblem. The texture is square with the
        // disc in the middle; the renderer maps it so the disc stays round on a banner of any shape, and the red
        // edge texels stretch over the rest of the cloth (so the border must be a flat colour).
        static Tex MakeBanner()
        {
            const int S = 256, SS = 4;
            Tex t = new Tex(S, S) { Repeat = false };
            Vector3 red = M.Hex(0xb5181c), white = new Vector3(0.93f, 0.92f, 0.88f), black = new Vector3(0.05f, 0.05f, 0.05f);
            const float R = 0.36f;                   // disc radius, as a share of the texture
            const float ARM = R * 2 * 0.62f / 5;      // the emblem sits on a 5 x 5 grid of this unit
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float disc = 0, ink = 0;
                    for (int j = 0; j < SS; j++)
                        for (int i = 0; i < SS; i++)
                        {
                            float px = (x + (i + 0.5f) / SS) / S - 0.5f, py = 0.5f - (y + (j + 0.5f) / SS) / S;      // y up
                            if (px * px + py * py > R * R) continue;
                            disc++;
                            if (Emblem(px / ARM, py / ARM)) ink++;
                        }
                    disc /= SS * SS; ink /= SS * SS;
                    Vector3 c = Vector3.Lerp(red, white, disc);
                    c = Vector3.Lerp(c, black, ink);
                    t.Px[y * S + x] = M.Col(c.X, c.Y, c.Z);
                }
            return t;
        }

        // Is the point (in units of one arm width, origin at the centre, y up) inside the emblem?
        static bool Emblem(float x, float y)
        {
            if (!Config.HISTORICAL_INSIGNIA)
                return (Math.Abs(x) < 0.6f && Math.Abs(y) < 2.3f) || (Math.Abs(y) < 0.6f && Math.Abs(x) < 2.3f);      // plain cross
            const float c = 0.70710678f;
            float u = (x + y) * c, v = (y - x) * c;                  // turn the figure onto its point
            if (Math.Abs(u) < 0.5f && Math.Abs(v) < 2.5f) return true;
            if (Math.Abs(v) < 0.5f && Math.Abs(u) < 2.5f) return true;
            if (v > 1.5f && v < 2.5f && u > 0 && u < 2.5f) return true;        // top arm, hooked right
            if (u > 1.5f && u < 2.5f && v < 0 && v > -2.5f) return true;       // right arm, hooked down
            if (v < -1.5f && v > -2.5f && u < 0 && u > -2.5f) return true;     // bottom arm, hooked left
            if (u < -1.5f && u > -2.5f && v > 0 && v < 2.5f) return true;      // left arm, hooked up
            return false;
        }

        // Factory wall for the enrichment works: board-marked concrete with bands of small dark panes.
        // One repeat = 4 storeys by 8 bays.
        static Tex MakeWindows()
        {
            const int S = 128;
            Canvas c = new Canvas(S, S, new Color(150, 148, 140));
            Mulberry rng = new Mulberry(31);
            for (int i = 0; i < S * S; i++)
            {
                int x = i % S, y = i / S;
                float n = 0.86f + Noise.Fbm(x * 0.09, y * 0.3, 3, 71) * 0.26f - (y % 32 < 2 ? 0.12f : 0);       // pour lines
                Color p = c.T.Px[i];
                c.T.Px[i] = new Color((byte)(p.R * n), (byte)(p.G * n), (byte)(p.B * n));
            }
            for (int j = 0; j < 4; j++)
                for (int i = 0; i < 8; i++)
                {
                    float x0 = i * 16 + 2.5f, y0 = j * 32 + 8;
                    for (int py = 0; py < 3; py++)
                        for (int px = 0; px < 3; px++)
                        {
                            float k = 0.10f + rng.Next() * 0.12f;
                            c.Rect(x0 + px * 3.8f, y0 + py * 5.4f, 3.0f, 4.6f, new Vector3(k * 0.8f, k * 0.95f, k * 1.15f));
                        }
                }
            return c.T;
        }

        // Cloud deck: fair-weather cumulus. Coverage in alpha, colour lit from the sun's side.
        static Tex MakeCloud(int size)
        {
            Tex t = new Tex(size, size);
            Vector3 sun = Config.SUN_DIR; Vector2 sd = Vector2.Normalize(new Vector2(sun.X, sun.Z)) * 0.07f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size * 6, v = (float)y / size * 6;
                    float d = Noise.Fbm(u, v, 5, 61, 6), d2 = Noise.Fbm(u + sd.X * 6, v + sd.Y * 6, 5, 61, 6);
                    float cov = M.Smoothstep(0.52f, 0.70f, d);
                    float lit = M.Clamp((d - d2) * 5 + 0.55f);
                    Vector3 col = Vector3.Lerp(new Vector3(0.66f, 0.70f, 0.78f), new Vector3(1.0f, 0.99f, 0.96f), lit);
                    col = Vector3.Lerp(col, new Vector3(0.96f, 0.97f, 1.0f), (1 - cov) * 0.5f);
                    t.Px[y * size + x] = M.Col(col.X, col.Y, col.Z, cov * 0.92f);
                }
            return t;
        }

        // soft round sprite; power shapes the falloff
        static Tex MakeDisc(int size, float inner, float power)
        {
            Tex t = new Tex(size, size); t.Repeat = false;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f, d = M.Hypot(dx, dy);
                    float a = M.Pow(M.Smoothstep(0.5f, inner, d), power);
                    t.Px[y * size + x] = new Color((byte)255, (byte)255, (byte)255, (byte)(a * 255));
                }
            return t;
        }

        // The view through the scope: clear glass that darkens towards the rim, black outside it.
        static Tex MakeScope(int size)
        {
            Tex t = new Tex(size, size); t.Repeat = false; t.Mips = false;
            const float R = 0.46f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f, r = M.Hypot(dx, dy);
                    float glass = M.Smoothstep(R, R - 0.008f, r);
                    float shade = 0.5f + 0.5f * M.Smoothstep(R - 0.01f, R - 0.2f, r);
                    float a = 1 - glass * shade;
                    float rim = M.Smoothstep(0.012f, 0.0f, Math.Abs(r - (R - 0.004f))) * 0.12f;
                    t.Px[y * size + x] = new Color((byte)(rim * 120), (byte)(rim * 170), (byte)(rim * 255), (byte)(M.Clamp(a) * 255));
                }
            return t;
        }

        static Tex MakeVignette(int size)
        {
            Tex t = new Tex(size, size); t.Repeat = false; t.Mips = false;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f, r = M.Hypot(dx * 1.25f, dy);
                    float a = (1 - M.Smoothstep(0.78f, 0.22f, r)) * 0.42f;
                    t.Px[y * size + x] = new Color((byte)0, (byte)0, (byte)6, (byte)(a * 255));
                }
            return t;
        }

        public static Textures Make(Action<float> progress)
        {
            Textures t = new Textures();
            t.Stone = MakeStone(512); progress(0.25f);
            t.Slate = MakeSlate(256); t.Ground = MakeGround(256); t.Rock = MakeRock(256); progress(0.5f);
            t.Bark = MakeBark(128); t.Timber = MakeTimber(256); t.Leather = MakeLeather(256); t.Water = MakeWater(256); progress(0.7f);
            t.Leaves = MakeLeaves(); t.Banner = MakeBanner(); t.Windows = MakeWindows(); t.Cloud = MakeCloud(256); progress(0.9f);
            t.Soft = MakeDisc(64, 0.08f, 1); t.Glow = MakeDisc(64, 0.0f, 2.2f); t.Scope = MakeScope(1024); t.Vignette = MakeVignette(256);
            t.White = new Tex(4, 4); for (int i = 0; i < 16; i++) t.White.Px[i] = Color.White;
            t.White.Mips = false;
            progress(1);
            return t;
        }
    }
}
