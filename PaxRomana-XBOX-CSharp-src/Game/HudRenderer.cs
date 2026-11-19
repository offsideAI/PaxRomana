// Draws the HUD, the scope, the menus and the end screen with SpriteBatch and the baked fonts.
// Sizes are in "design pixels" at 720 lines and scale with the screen height, so text stays readable on a television.
// Everything keeps inside the title-safe area (5% in from the sides, 6% from top and bottom).
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PaxRomana
{
    public sealed class HudRenderer
    {
        static readonly Color Gold = new Color(210, 174, 99), Bone = new Color(239, 230, 210), Blood = new Color(179, 38, 28), Ink = new Color(11, 10, 8), Cyan = new Color(53, 216, 255);
        readonly GraphicsDevice gd; readonly SpriteBatch sb; readonly Renderer r;
        sealed class Face { public FontFace F; public Texture2D Tex; }
        readonly Face ui, bold, display;
        float U, W, H;          // scale and screen size
        float fade = 1;         // opacity of the in-game HUD (fades out for the kill-cam)

        public HudRenderer(GraphicsDevice device, Renderer renderer)
        {
            gd = device; r = renderer; sb = new SpriteBatch(gd);
            ui = Load("Ui"); bold = Load("UiBold"); display = Load("Display");
        }

        Face Load(string name)
        {
            FontFace f = FontFace.Get(name);
            Tex t = new Tex(f.W, f.H) { Repeat = false };
            for (int i = 0; i < t.Px.Length; i++) t.Px[i] = new Color((byte)255, (byte)255, (byte)255, f.Alpha[i]);
            return new Face { F = f, Tex = r.Upload(t) };      // mip-mapped, so small labels stay clean
        }

        // ---- primitives -------------------------------------------------------------------------------------------
        static Color A(Color c, float a) { return new Color(c.R, c.G, c.B, (byte)(M.Clamp(a) * c.A)); }
        void Rect(float x, float y, float w, float h, Color c) { sb.Draw(r.White, new Vector2(x, y), null, c, 0, Vector2.Zero, new Vector2(w, h), SpriteEffects.None, 0); }
        void Rot(float cx, float cy, float w, float h, float angle, Color c) { sb.Draw(r.White, new Vector2(cx, cy), null, c, angle, new Vector2(0.5f, 0.5f), new Vector2(w, h), SpriteEffects.None, 0); }
        void Frame(float x, float y, float w, float h, float t, Color c) { Rect(x, y, w, t, c); Rect(x, y + h - t, w, t, c); Rect(x, y + t, t, h - 2 * t, c); Rect(x + w - t, y + t, t, h - 2 * t, c); }
        void Dot(float cx, float cy, float rad, Color c) { sb.Draw(r.Soft, new Vector2(cx - rad * 1.25f, cy - rad * 1.25f), null, c, 0, Vector2.Zero, rad * 2.5f / r.Soft.Width, SpriteEffects.None, 0); }

        float Width(Face f, string s, float px, float track = 0) { return f.F.Measure(s, px) + track * px * s.Length; }

        // Draws s with its top-left at (x, y). align: 0 left, 1 centre, 2 right. track is extra letter spacing in em.
        float Text(Face f, string s, float x, float y, float px, Color c, int align = 0, float track = 0, bool shadow = true)
        {
            float w = Width(f, s, px, track);
            if (align == 1) x -= (w - track * px) / 2; else if (align == 2) x -= w - track * px;
            float k = px / f.F.Size;
            if (shadow && c.A > 0) Glyphs(f, s, x + Math.Max(1, U), y + Math.Max(1, U), k, track * px, new Color((byte)0, (byte)0, (byte)0, (byte)(c.A * 0.7f)));
            Glyphs(f, s, x, y, k, track * px, c);
            return w;
        }
        void Glyphs(Face f, string s, float x, float y, float k, float track, Color c)
        {
            for (int i = 0; i < s.Length; i++)
            {
                Glyph g;
                if (!f.F.Glyphs.TryGetValue(s[i], out g)) continue;
                if (g.W > 0) sb.Draw(f.Tex, new Vector2(x + g.XOff * k, y + g.YOff * k), new Rectangle(g.X, g.Y, g.W, g.H), c, 0, Vector2.Zero, k, SpriteEffects.None, 0);
                x += g.Adv * k + track;
            }
        }

        // ---- rich text: <b>bold</b>, <em>dim</em>, <k>key cap</k>, wrapped to a width ----
        struct Word { public string S; public int Style; public float W; public bool Glue; }      // Glue: no space before it
        readonly List<Word> words = new List<Word>();

        // Returns the height used. Pass draw = false to measure.
        float Rich(string s, float x, float y, float px, float maxW, float lineH, Color c, Color boldC, bool draw = true, int align = 0)
        {
            words.Clear();
            int style = 0, i = 0; bool sawSpace = true;
            while (i < s.Length)
            {
                if (s[i] == '<')
                {
                    int e = s.IndexOf('>', i);
                    string tag = s.Substring(i + 1, e - i - 1);
                    style = tag == "b" ? 1 : tag == "em" ? 2 : tag == "k" ? 3 : 0;
                    i = e + 1; continue;
                }
                if (s[i] == ' ') { sawSpace = true; i++; continue; }
                int j = i;
                while (j < s.Length && s[j] != '<' && (s[j] != ' ' || style == 3)) j++;
                string w = s.Substring(i, j - i);
                float ww = style == 3 ? Width(bold, w, px * 0.8f, 0.06f) + 14 * U : Width(style == 1 ? bold : ui, w, px);
                words.Add(new Word { S = w, Style = style, W = ww, Glue = !sawSpace });
                sawSpace = false;
                i = j;
            }
            float space = px * 0.24f, cy = y; int start = 0;
            while (start < words.Count)
            {
                float lw = 0; int end = start;
                while (end < words.Count)
                {
                    float add = words[end].W + (end > start && !words[end].Glue ? space : 0);
                    if (lw + add > maxW && end > start) break;
                    lw += add; end++;
                }
                if (draw)
                {
                    float cx = align == 1 ? x + (maxW - lw) / 2 : align == 2 ? x + maxW - lw : x;
                    for (int k = start; k < end; k++)
                    {
                        Word w = words[k];
                        if (k > start && !w.Glue) cx += space;
                        if (w.Style == 3)
                        {
                            float kh = px * 1.15f, ky = cy + px * 0.08f;
                            Rect(cx, ky, w.W, kh, A(Gold, 0.09f * fadeOf(c))); Frame(cx, ky, w.W, kh, Math.Max(1, U), A(Gold, 0.5f * fadeOf(c)));
                            Text(bold, w.S, cx + w.W / 2, cy + px * 0.16f, px * 0.8f, A(Gold, fadeOf(c)), 1, 0.06f, false);
                        }
                        else Text(w.Style == 1 ? bold : ui, w.S, cx, cy, px, w.Style == 1 ? boldC : w.Style == 2 ? A(c, 0.7f) : c);
                        cx += w.W;
                    }
                }
                cy += lineH; start = end;
            }
            return cy - y;
        }
        static float fadeOf(Color c) { return c.A / 255f; }

        // ---- frame ------------------------------------------------------------------------------------------------
        public void Draw(Hud hud, GameSim sim, float glare)
        {
            W = gd.Viewport.Width; H = gd.Viewport.Height; U = H / 720f;
            sb.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            bool inGame = sim != null && sim.Started && !hud.EndVisible;
            if (sim != null)
            {
                // lens: vignette, muzzle or blast glare, then the scope
                float scope = sim.GradeScope;
                if (scope < 1) sb.Draw(r.Vignette, new Rectangle(0, 0, (int)W, (int)H), A(Color.White, 1 - scope));
                float flash = Math.Min(0.6f, sim.GradeFlash * 0.45f + glare);
                if (flash > 0.003f) Rect(0, 0, W, H, new Color((byte)255, (byte)226, (byte)188, (byte)(flash * 255)));
                if (scope > 0.01f) Scope(hud, scope);
            }
            fade += ((hud.Cine ? 0 : 1) - fade) * 0.2f;
            if (inGame && fade > 0.01f) InGame(hud);
            if (hud.CineK > 0.005f) { float bar = 0.105f * H * hud.CineK; Rect(0, 0, W, bar, Color.Black); Rect(0, H - bar, W, bar, Color.Black); }
            if (hud.IntroOn) IntroCards(hud);
            if (hud.EndVisible) End(hud);
            else if (hud.MenuVisible) Menu(hud, sim != null && sim.Started);
            sb.End();
        }

        // Shown instead of the game if it hit an error it could not recover from.
        public void DrawError(string message)
        {
            W = gd.Viewport.Width; H = gd.Viewport.Height; U = H / 720f;
            sb.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            float x = 0.08f * W, y = 0.1f * H;
            Text(display, "PAX ROMANA", x, y, 40 * U, Gold, 0, 0.1f);
            Text(bold, "The game hit an error and had to stop. Please pass on the text below.", x, y + 64 * U, 20 * U, Bone);
            string clean = message.Replace('<', '(').Replace('>', ')').Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
            Rich(clean, x, y + 110 * U, 15 * U, 0.84f * W, 21 * U, A(Bone, 0.85f), Bone);
            sb.End();
        }

        void Scope(Hud hud, float k)
        {
            // glass: a square mask the height of the screen, black bars either side
            float side = H, x0 = (W - side) / 2;
            Color m = A(Color.White, k);
            sb.Draw(r.ScopeTex, new Rectangle((int)x0, 0, (int)side + 1, (int)H), m);
            Rect(0, 0, x0 + 1, H, A(Color.Black, k)); Rect(x0 + side, 0, W - x0 - side + 1, H, A(Color.Black, k));
            if (!hud.ScopeOn) return;
            // reticle: 1000 units span 92% of the screen height
            float u = 0.00092f * H, cx = W / 2, cy = H / 2; Color ink = new Color(10, 10, 10);
            Rect(cx - 500 * u, cy - 3.5f * u, 350 * u, 7 * u, ink); Rect(cx + 150 * u, cy - 3.5f * u, 350 * u, 7 * u, ink);
            Rect(cx - 3.5f * u, cy + 150 * u, 7 * u, 350 * u, ink);
            float t = Math.Max(1, 1.3f * u);
            Rect(cx - 150 * u, cy - t / 2, 144 * u, t, ink); Rect(cx + 6 * u, cy - t / 2, 144 * u, t, ink);
            Rect(cx - t / 2, cy - 500 * u, t, 494 * u, ink); Rect(cx - t / 2, cy + 6 * u, t, 144 * u, ink);
            for (int i = 1; i <= 4; i++)
            {
                float d = i * 30 * u;
                Dot(cx + d, cy, 2.6f * u, ink); Dot(cx - d, cy, 2.6f * u, ink); Dot(cx, cy - d, 2.6f * u, ink);
                Rect(cx - (9 - i) * u, cy + d - t / 2, (9 - i) * 2 * u, t, ink);
            }
            Dot(cx, cy, 1.6f * u, ink);
            // readouts
            float ry = cy + 0.08f * H, lx = cx - 0.21f * H, rx = cx + 0.21f * H;
            Readout("RANGE", hud.Range > 0 ? (int)Math.Round(hud.Range) + " m" : "— m", lx, ry, 2);
            Readout("ZERO", hud.Zero + " m", lx, ry + 52 * U, 2);
            Readout("ZOOM", hud.Zoom + "×", rx, ry, 0);
            Readout("WIND", hud.Wind, rx, ry + 52 * U, 0);
            float bw = 150 * U, by = cy + 0.33f * H;
            Text(ui, "BREATH", cx, by, 11 * U, A(Bone, 0.7f), 1, 0.26f);
            Rect(cx - bw / 2, by + 18 * U, bw, 3 * U, new Color((byte)255, (byte)255, (byte)255, (byte)40));
            Rect(cx - bw / 2, by + 18 * U, bw * hud.Breath, 3 * U, hud.Exhausted ? Blood : Bone);
        }
        void Readout(string label, string value, float x, float y, int align)
        {
            Text(ui, label, x, y, 10 * U, A(Gold, 0.9f), align, 0.3f);
            Text(bold, value, x, y + 13 * U, 21 * U, new Color(244, 239, 226), align, 0.05f);
        }

        void InGame(Hud hud)
        {
            float f = fade, sx = 0.05f * W, sy = 0.06f * H;
            // ---- objective ----
            {
                float x = sx + 14 * U, y = sy;
                Text(ui, "OBJECTIVE", x, y, 13 * U, A(Gold, f), 0, 0.34f);
                float h = Rich(hud.ObjLine, x, y + 20 * U, 20 * U, 0.34f * W, 25 * U, A(Bone, f), A(Bone, f));
                float cy = y + 22 * U + h;
                float w = Text(bold, hud.Kills.ToString(), x, cy, 34 * U, A(Bone, f));
                w += Text(ui, "/", x + w + 4 * U, cy + 9 * U, 22 * U, A(Bone, f * 0.5f)) + 8 * U;
                w += Text(bold, hud.Total.ToString(), x + w, cy, 34 * U, A(Bone, f));
                Text(ui, "DEFENDERS DOWN", x + w + 12 * U, cy + 16 * U, 13 * U, A(Bone, f * 0.7f), 0, 0.2f);
                cy += 46 * U;
                bool pulse = hud.Alarm && (DateTime.UtcNow.Millisecond / 350) % 2 == 0;
                Text(ui, hud.Alarm ? "ALARM" : "GARRISON ALERT", x, cy, 11 * U, hud.Alarm ? A(new Color(255, 90, 72), f * (pulse ? 0.4f : 1)) : A(Bone, f * 0.7f), 0, 0.26f);
                Rect(x, cy + 17 * U, 190 * U, 3 * U, new Color((byte)255, (byte)255, (byte)255, (byte)(40 * f)));
                Rect(x, cy + 17 * U, 190 * U * hud.Alert / 100f, 3 * U, A(Color.Lerp(Gold, Blood, hud.Alert / 100f), f));
                Rect(sx, sy, 2 * U, cy + 20 * U - sy, A(Gold, f));
            }
            // ---- capture bar ----
            if (hud.CaptureShow)
            {
                float w = Math.Min(420 * U, 0.6f * W), x = (W - w) / 2, y = 0.09f * H;
                Text(bold, hud.CaptureText.ToUpperInvariant(), W / 2, y, 16 * U, A(Bone, f), 1, 0.2f);
                Rect(x, y + 28 * U, w, 8 * U, new Color((byte)10, (byte)9, (byte)7, (byte)(150 * f)));
                Frame(x, y + 28 * U, w, 8 * U, Math.Max(1, U), A(new Color(120, 220, 255), 0.5f * f));
                Rect(x + U, y + 29 * U, (w - 2 * U) * hud.CaptureProgress, 6 * U, A(Color.Lerp(Cyan, new Color(184, 241, 255), hud.CaptureProgress), f));
            }
            // ---- feed ----
            {
                float y = sy, right = W - sx;
                foreach (FeedEntry e in hud.FeedList)
                {
                    float a = f * (e.Age > 4.2f ? 1 - (e.Age - 4.2f) : 1), slide = (1 - M.Clamp(e.Age / 0.25f)) * 24 * U;
                    bool warn = e.Cls == "warn";
                    float px = 19 * U, tw = Rich(e.Text, 0, 0, px, 2000, px, Color.White, Color.White, false) > 0 ? LastLineWidth(px) : 0;
                    float bx = right - tw - 26 * U + slide;
                    Rect(bx, y, tw + 26 * U, 34 * U, new Color((byte)10, (byte)9, (byte)7, (byte)(150 * a)));
                    Rect(right - 2 * U + slide, y, 2 * U, 34 * U, A(warn ? Blood : e.Cls == "head" ? Gold : A(Gold, 0.55f), a));
                    Rich(e.Text, bx + 12 * U, y + 6 * U, px, 2000, px, A(Bone, a), A(warn ? new Color(255, 106, 85) : Gold, a));
                    y += 40 * U;
                }
            }
            // ---- door prompt ----
            if (hud.PromptText.Length > 0)
            {
                float px = 19 * U, kw = Math.Max(26 * U, Width(bold, hud.PromptKey, 16 * U) + 14 * U), tw = Width(ui, hud.PromptText, px, 0.1f);
                float w = kw + 10 * U + tw + 40 * U, x = (W - w) / 2, y = H * 0.73f - 42 * U;
                Rect(x, y, w, 42 * U, new Color((byte)10, (byte)9, (byte)7, (byte)(160 * f)));
                Frame(x, y, w, 42 * U, Math.Max(1, U), A(Gold, 0.45f * f));
                Rect(x + 20 * U, y + 9 * U, kw, 24 * U, A(Gold, f));
                Text(bold, hud.PromptKey, x + 20 * U + kw / 2, y + 11.5f * U, 16 * U, A(new Color(20, 17, 12), f), 1, 0, false);
                Text(ui, hud.PromptText, x + 30 * U + kw, y + 9 * U, px, A(Bone, f), 0, 0.1f);
            }
            // ---- hint ----
            if (hud.HintT > 0)
            {
                float a = f * M.Clamp(hud.HintT / 0.6f), px = 19 * U; string s = hud.HintText.ToUpperInvariant();
                float tw = Width(ui, s, px, 0.14f), w = tw + 36 * U, x = (W - w) / 2, y = H * 0.82f - 40 * U;
                Rect(x, y, w, 40 * U, new Color((byte)10, (byte)9, (byte)7, (byte)(140 * a)));
                Frame(x, y, w, 40 * U, Math.Max(1, U), A(Gold, 0.3f * a));
                Text(ui, s, x + 18 * U, y + 8 * U, px, A(Bone, a), 0, 0.14f);
            }
            // ---- crosshair and hit marker ----
            float cx = W / 2, cyy = H / 2;
            if (hud.Crosshair && !hud.ScopeOn)
            {
                Color c = new Color((byte)255, (byte)255, (byte)255, (byte)(217 * f)); float t = Math.Max(1.5f, 1.5f * U), l = 9 * U;
                Rect(cx + l, cyy - t / 2, l, t, c); Rect(cx - 2 * l, cyy - t / 2, l, t, c); Rect(cx - t / 2, cyy + l, t, l, c); Rect(cx - t / 2, cyy - 2 * l, t, l, c);
            }
            if (hud.MarkerT > 0)
            {
                float k = hud.MarkerT / 0.35f, s = (1 + 0.5f * M.Clamp((k - 0.7f) / 0.3f)) * U;
                Color c = A(hud.MarkerHead ? new Color(255, 74, 56) : Color.White, k);
                for (int i = 0; i < 4; i++)
                {
                    float dx = (i % 2 == 0 ? 1 : -1) * 14.5f * s, dy = (i < 2 ? 1 : -1) * 9 * s;
                    Rot(cx + dx, cyy + dy, 13 * s, 2 * s, (i == 0 || i == 3) ? M.PI / 4 : -M.PI / 4, c);
                }
            }
            // ---- ammunition ----
            {
                float right = W - sx, y = H - sy;
                string st = hud.Stance == 0 ? "STANDING" : hud.Stance == 1 ? "CROUCHED" : "PRONE";
                float sw = Text(ui, st, right, y - 14 * U, 11 * U, A(Gold, f), 2, 0.24f);
                Text(ui, "SNIPER RIFLE · EXPLOSIVE CHARGES", right - sw - 14 * U, y - 14 * U, 11 * U, A(Bone, f * 0.7f), 2, 0.24f);
                y -= 22 * U;
                float rw = Text(ui, hud.Reserve.ToString(), right, y - 34 * U, 28 * U, A(Bone, f * 0.75f), 2);
                float slw = Text(ui, "/", right - rw - 6 * U, y - 34 * U, 26 * U, A(Bone, f * 0.5f), 2);
                Text(bold, hud.Mag.ToString(), right - rw - slw - 12 * U, y - 50 * U, 46 * U, A(Bone, f), 2);
                y -= 58 * U;
                for (int i = 0; i < 5; i++) Rect(right - (5 - i) * 11 * U + 5 * U, y - 24 * U, 6 * U, 24 * U, i < hud.Mag ? A(new Color(216, 178, 104), f) : new Color((byte)255, (byte)255, (byte)255, (byte)(36 * f)));
                y -= 32 * U;
                for (int i = 0; i < 8; i++)
                {
                    float px = right - (8 - i) * 16 * U + 10.5f * U;
                    if (i < hud.Charges) { Dot(px, y - 6 * U, 9 * U, new Color((byte)255, (byte)106, (byte)26, (byte)(70 * f))); Dot(px, y - 6 * U, 5.5f * U, A(new Color(255, 138, 58), f)); }
                    else Dot(px, y - 6 * U, 5.5f * U, new Color((byte)255, (byte)255, (byte)255, (byte)(36 * f)));
                }
                if (hud.Status.Length > 0) Text(bold, hud.Status, right, y - 40 * U, 14 * U, A(Gold, f), 2, 0.3f);
            }
            if (hud.ShowKeys) Keys(hud);
        }

        float LastLineWidth(float px)
        {
            float w = 0, space = px * 0.24f;
            for (int i = 0; i < words.Count; i++) w += words[i].W + (i > 0 && !words[i].Glue ? space : 0);
            return w;
        }

        static readonly string[][] KeyRows =
        {
            new[] { "Move", "Left stick", "W A S D" },
            new[] { "Run (hold)", "LB, or click left stick", "Shift" },
            new[] { "Look / aim", "Right stick", "Mouse or arrows" },
            new[] { "Scope", "LT (hold)", "Right button or F" },
            new[] { "Fire rifle", "RT", "Left button or Space" },
            new[] { "Fire explosive charge", "RB", "Middle button or G" },
            new[] { "Hold breath (scoped)", "LB", "Shift" },
            new[] { "Zoom", "D-pad up / down, or Y", "Wheel or + / −" },
            new[] { "Sight zero", "D-pad left / right", "Q / E" },
            new[] { "Reload", "X", "R" },
            new[] { "Open / close door, skip kill-cam", "A", "O, Space" },
            new[] { "Crouch / prone", "B / click right stick", "C / Z" },
            new[] { "Pause", "Menu", "Esc" },
            new[] { "This overlay", "View", "H" },
        };

        void Keys(Hud hud)
        {
            float w = 640 * U, rowH = 31 * U, h = 96 * U + KeyRows.Length * rowH, x = (W - w) / 2, y = (H - h) / 2;
            Rect(x, y, w, h, new Color((byte)12, (byte)10, (byte)8, (byte)225));
            Frame(x, y, w, h, Math.Max(1, U), A(Gold, 0.35f));
            Text(ui, "CONTROLS", x + 32 * U, y + 24 * U, 13 * U, Gold, 0, 0.34f, false);
            float c1 = x + 32 * U, c2 = x + 290 * U, c3 = x + 470 * U, ty = y + 50 * U;
            Text(ui, "CONTROLLER", c2, ty, 11 * U, Gold, 0, 0.28f, false); Text(ui, "KEYBOARD / MOUSE", c3, ty, 11 * U, Gold, 0, 0.28f, false);
            ty += 20 * U;
            foreach (string[] row in KeyRows)
            {
                Rect(x + 32 * U, ty, w - 64 * U, Math.Max(1, U), new Color((byte)255, (byte)255, (byte)255, (byte)20));
                Text(ui, row[0], c1, ty + 5 * U, 17 * U, A(Bone, 0.75f), 0, 0, false);
                Text(bold, row[1], c2, ty + 5 * U, 17 * U, Bone, 0, 0.04f, false);
                Text(ui, row[2], c3, ty + 5 * U, 17 * U, A(Bone, 0.8f), 0, 0, false);
                ty += rowH;
            }
            Text(ui, hud.IsPad ? "VIEW TO CLOSE" : "H TO CLOSE", W / 2, ty + 6 * U, 12 * U, A(Bone, 0.7f), 1, 0.2f, false);
        }

        void Veil() { Rect(0, 0, W, H, new Color((byte)8, (byte)7, (byte)5, (byte)150)); }

        float Panel(float w, float h)
        {
            float x = (W - w) / 2, y = (H - h) / 2;
            Rect(x, y, w, h, new Color((byte)14, (byte)12, (byte)9, (byte)218));
            Frame(x, y, w, h, Math.Max(1, U), A(Gold, 0.28f));
            Frame(x + 6 * U, y + 6 * U, w - 12 * U, h - 12 * U, Math.Max(1, U), A(Gold, 0.12f));
            return y;
        }

        void Button(string label, float cx, float y, bool enabled)
        {
            float tw = Width(display, label, 16 * U, 0.32f), w = tw + 88 * U, h = 50 * U;
            Rect(cx - w / 2, y, w, h, enabled ? new Color(207, 170, 94) : new Color(96, 90, 78));
            Text(display, label, cx, y + 15 * U, 16 * U, Ink, 1, 0.32f, false);
        }

        void Menu(Hud hud, bool paused)
        {
            Veil();
            float w = 800 * U, h = 600 * U, x = (W - w) / 2, y = Panel(w, h), cx = W / 2, cy = y + 40 * U;
            Text(ui, "LEVEL ZERO · BAVARIA · SUMMER 1944", cx, cy, 13 * U, Gold, 1, 0.34f); cy += 28 * U;
            float t1 = Width(display, "PAX ", 84 * U, 0.1f), t2 = Width(display, "ROMANA", 84 * U, 0.1f), tx = cx - (t1 + t2 - 0.1f * 84 * U) / 2;
            Text(display, "PAX", tx, cy, 84 * U, Bone, 0, 0.1f); Text(display, "ROMANA", tx + t1, cy, 84 * U, Gold, 0, 0.1f); cy += 98 * U;
            Color sub = A(Bone, 0.82f);
            Text(ui, "The Reich is enriching uranium-235. The keys to the works lie in the vault of Burg Adlerstein.", cx, cy, 18 * U, sub, 1, 0.02f); cy += 26 * U;
            Text(ui, "You are an agent of the Research and Analysis Wing. One rifle, from the ridge across the river.", cx, cy, 18 * U, sub, 1, 0.02f); cy += 44 * U;
            Rect(x + 52 * U, cy, w - 104 * U, Math.Max(1, U), A(Gold, 0.2f)); cy += 22 * U;
            float colL = x + 52 * U, colW = (w - 104 * U - 34 * U) * 0.535f, colR = colL + colW + 34 * U, colRW = w - 104 * U - 34 * U - colW;
            Text(display, "MISSION", colL, cy, 13 * U, Gold, 0, 0.3f); Text(display, "CONTROLS", colR, cy, 13 * U, Gold, 0, 0.3f);
            float ly = cy + 26 * U; Color body = A(Bone, 0.85f);
            ly += Rich("<b>Take the keys.</b> Clear every defender inside the castle walls, then crack the strongbox in the vault of the keep.", colL, ly, 16 * U, colW, 23 * U, body, Bone) + 8 * U;
            ly += Rich("Your rifle fires sniper rounds and <b>explosive charges</b>. A charge kills everything near it and blows doors and the gate off their hinges.", colL, ly, 16 * U, colW, 23 * U, body, Bone);
            string[] list = hud.IsPad
                ? new[] { "<k>LT</k> scope <k>RT</k> fire <k>RB</k> explosive", "<k>LB</k> run · hold breath <k>X</k> reload", "<k>A</k> doors <k>B</k> crouch", "D-pad zoom and zero", "<k>View</k> all controls <k>Menu</k> pause" }
                : new[] { "Mouse aim <k>WASD</k> move", "LMB fire <k>G</k> explosive RMB scope", "Wheel zoom <k>Q</k> <k>E</k> zero", "<k>Shift</k> breath <k>R</k> reload <k>O</k> doors", "<k>H</k> all controls · Xbox controller works too" };
            float ry = cy + 26 * U;
            foreach (string s in list) ry += Rich(s, colR, ry, 15 * U, colRW, 29 * U, A(Bone, 0.9f), Bone);
            cy = Math.Max(ly, ry) + 18 * U;
            Rect(x + 52 * U, cy, w - 104 * U, Math.Max(1, U), A(Gold, 0.2f)); cy += 26 * U;
            Button(hud.Failed ? "FAILED" : !hud.Ready ? "LOADING…" : paused ? "RESUME" : "TAKE THE SHOT", cx, cy, hud.Ready); cy += 72 * U;
            Rect(cx - 0.3f * w, cy, 0.6f * w, 2 * U, new Color((byte)255, (byte)255, (byte)255, (byte)26));
            Rect(cx - 0.3f * w, cy, 0.6f * w * M.Clamp(hud.LoadP), 2 * U, Gold); cy += 12 * U;
            string lt = !hud.Ready ? hud.LoadText : hud.IsPad ? (paused ? "Press A to resume" : "Press A to deploy") : (paused ? "Press Enter or click to resume" : "Press Enter or click to deploy");
            Text(ui, lt.ToUpperInvariant(), cx, cy, 13 * U, A(Bone, 0.6f), 1, 0.2f);
        }

        // The Level Zero intro: a title card, then three briefing cards, in the lower part of the picture over the flight.
        void IntroCards(Hud hud)
        {
            float t = hud.IntroT, cx = W / 2;
            // open on black, and dip to black at the cuts between shots
            float dark = 1 - M.Smoothstep(0.0f, 1.2f, t);
            for (int i = 1; i < IntroScript.Cards.Length; i++) dark = Math.Max(dark, 1 - M.Clamp(Math.Abs(t - IntroScript.Cuts[i]) / 0.45f));
            dark = Math.Max(dark, M.Smoothstep(IntroScript.Length - 0.5f, IntroScript.Length, t));
            if (dark > 0.004f) Rect(0, 0, W, H, A(Color.Black, dark));
            IntroCard c = IntroScript.At(t);
            if (c != null)
            {
                float a = M.Smoothstep(0, 0.7f, t - c.Start) * M.Smoothstep(0, 0.6f, c.End - t);
                if (c.Title != null)
                {
                    float y = 0.36f * H;
                    Rect(0, 0, W, H, A(Color.Black, 0.38f * a));
                    Text(ui, c.Kicker, cx, y, 14 * U, A(Gold, a), 1, 0.36f); y += 34 * U;
                    Text(display, c.Title, cx, y, 96 * U, A(Bone, a), 1, 0.1f); y += 112 * U;
                    Rect(cx - 90 * U, y, 180 * U, Math.Max(1, U), A(Gold, 0.6f * a)); y += 18 * U;
                    Text(display, c.Sub, cx, y, 26 * U, A(Gold, a), 1, 0.34f);
                }
                else
                {
                    float y = 0.66f * H, w = Math.Min(0.8f * W, 900 * U);
                    Rect(0, y - 26 * U, W, 150 * U, new Color((byte)6, (byte)6, (byte)5, (byte)(120 * a)));
                    Text(ui, c.Kicker, cx, y - 8 * U, 13 * U, A(Gold, a), 1, 0.36f); y += 22 * U;
                    foreach (string line in c.Lines) y += Rich(line, cx - w / 2, y, 25 * U, w, 34 * U, A(Bone, a), A(Gold, a), true, 1);
                }
            }
            if (t > 1.5f) Text(ui, hud.IsPad ? "A  SKIP" : "ENTER  SKIP", W - 0.05f * W, H - 0.06f * H - 14 * U, 12 * U, A(Bone, 0.55f), 2, 0.26f);
        }

        void End(Hud hud)
        {
            Veil();
            EndStats s = hud.Stats; bool win = hud.Win;
            float w = 800 * U, h = 540 * U, x = (W - w) / 2, y = Panel(w, h), cx = W / 2, cy = y + 40 * U;
            Text(ui, win ? "LEVEL ZERO COMPLETE" : "LEVEL ZERO FAILED", cx, cy, 13 * U, Gold, 1, 0.34f); cy += 30 * U;
            Text(display, win ? "THE KEYS ARE YOURS" : "MISSION FAILED", cx, cy, 48 * U, Bone, 1, 0.06f); cy += 66 * U;
            Text(ui, win ? "The gates of the U-235 enrichment zone will open for you. Level One: go in, and break it from the inside." : "The garrison still holds the castle, and the keys with it.", cx, cy, 18 * U, A(Bone, 0.82f), 1, 0.02f); cy += 44 * U;
            Rect(x + 52 * U, cy, w - 104 * U, Math.Max(1, U), A(Gold, 0.2f)); cy += 24 * U;
            int acc = s.Shots > 0 ? (int)Math.Round(100f * s.Hits / s.Shots) : 0;
            int mm = (int)(s.Time / 60), ss = (int)(s.Time % 60);
            string[][] st =
            {
                new[] { s.Kills + "/" + s.Total, "DEFENDERS" }, new[] { acc + "%", "ACCURACY" }, new[] { s.Heads.ToString(), "HEADSHOTS" },
                new[] { (int)Math.Round(s.Longest) + " m", "LONGEST SHOT" }, new[] { mm + ":" + ss.ToString("00"), "TIME" }, new[] { s.Alarms.ToString(), "ALARMS RAISED" },
                new[] { s.Charges.ToString(), "CHARGES USED" },
            };
            float cw = (w - 104 * U) / 4;
            for (int i = 0; i < st.Length; i++)
            {
                float px = x + 52 * U + cw * (i % 4) + cw / 2, py = cy + (i / 4) * 74 * U;
                Text(bold, st[i][0], px, py, 34 * U, Bone, 1); Text(ui, st[i][1], px, py + 40 * U, 11 * U, A(Bone, 0.65f), 1, 0.26f);
            }
            cy += 150 * U;
            Rect(x + 52 * U, cy, w - 104 * U, Math.Max(1, U), A(Gold, 0.2f)); cy += 22 * U;
            string rating = !win ? "—" : acc >= 85 && s.Alarms == 0 ? "CHHAYA · THE SHADOW" : acc >= 65 ? "NISHANEBAAZ · MARKSMAN" : "RANGROOT · RECRUIT";
            Text(display, rating, cx, cy, 15 * U, Gold, 1, 0.24f); cy += 40 * U;
            Button("DEPLOY AGAIN", cx, cy, true); cy += 62 * U;
            Text(ui, hud.IsPad ? "PRESS A" : "PRESS ENTER", cx, cy, 13 * U, A(Bone, 0.7f), 1, 0.26f);
        }
    }
}
