// Draws the world with MonoGame's built-in effects only (BasicEffect and AlphaTestEffect), so the game needs no
// compiled shaders and no content pipeline: every vertex buffer and texture is created here from generated data.
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PaxRomana
{
    public struct Vtx : IVertexType
    {
        public Vector3 P, N; public Color C; public Vector2 T;
        public Vtx(Vector3 p, Vector3 n, Color c, Vector2 t) { P = p; N = n; C = c; T = t; }
        public static readonly VertexDeclaration Decl = new VertexDeclaration(
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
            new VertexElement(24, VertexElementFormat.Color, VertexElementUsage.Color, 0),
            new VertexElement(28, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0));
        VertexDeclaration IVertexType.VertexDeclaration { get { return Decl; } }
    }

    public sealed class Renderer
    {
        const float FAR = 9000;
        readonly GraphicsDevice gd;
        readonly BasicEffect fx; readonly AlphaTestEffect leafFx;
        readonly RasterizerState cullBack = new RasterizerState { CullMode = CullMode.CullClockwiseFace };
        readonly RasterizerState cullNone = new RasterizerState { CullMode = CullMode.None };

        sealed class Mesh { public VertexBuffer Vb; public IndexBuffer Ib; public int Prims; public BoundingSphere Bs; }
        sealed class Mat { public Texture2D Tex; public Vector3 Colour = Vector3.One; public float Spec, Alpha = 1; public bool Lit = true, Fog = true, TwoSided, VCol; }
        sealed class Part { public Mesh Mesh; public Mat Mat; }
        sealed class ChunkGpu { public Mesh Trunk, Rock, Leaf; public BoundingSphere Bs; }
        sealed class BannerGpu { public Banner B; public Vtx[] V; public short[] Idx; public Vector2[] Uv; public Vector3[] Base; }
        sealed class LeafGpu { public Mesh Leaf; public Mesh[] Straps; public DoorLeaf L; public Door D; }

        // textures
        public Texture2D White, Soft, Glow, ScopeTex, Vignette;
        Texture2D stone, slate, snow, rock, bark, timber, leather, water, leaves, banner, windows, cloud;
        // world
        readonly List<Part> statics = new List<Part>(), emissive = new List<Part>();
        readonly List<Mesh> terrain = new List<Mesh>();
        readonly List<ChunkGpu> chunks = new List<ChunkGpu>();
        readonly List<BannerGpu> banners = new List<BannerGpu>();
        readonly List<LeafGpu> doorLeaves = new List<LeafGpu>();
        Mesh[][] soldier; Mesh rifle, bolt, waterMesh, ringMesh, shockMesh, skyMesh, bulletMesh, tether;
        Mat soldierMat, timberMat, ironMat, bannerMat;
        VertexBuffer[] grassVb; IndexBuffer grassIb; int grassCap;
        World world;
        readonly Matrix[] parts = new Matrix[Soldier.PARTS];
        // dynamic sprites
        readonly VertexPositionColorTexture[] qv = new VertexPositionColorTexture[4 * 4096]; readonly short[] qi = new short[6 * 4096]; int qn;
        readonly VertexPositionColor[] birdV = new VertexPositionColor[16 * 6];
        readonly VertexPositionColorTexture[] cloudV = new VertexPositionColorTexture[13 * 25]; short[] cloudI;
        Matrix view, proj; Vector3 camPos, camRight, camUp, camFwd; BoundingFrustum frustum = new BoundingFrustum(Matrix.Identity);

        public Renderer(GraphicsDevice device)
        {
            gd = device;
            fx = new BasicEffect(gd);
            fx.AmbientLightColor = Lighting.Ambient;
            fx.DirectionalLight0.Enabled = true; fx.DirectionalLight0.Direction = -Config.SUN_DIR; fx.DirectionalLight0.DiffuseColor = Lighting.Sun; fx.DirectionalLight0.SpecularColor = Lighting.Sun;
            fx.DirectionalLight1.Enabled = true; fx.DirectionalLight1.Direction = Vector3.Down; fx.DirectionalLight1.DiffuseColor = Lighting.Sky; fx.DirectionalLight1.SpecularColor = Vector3.Zero;
            fx.DirectionalLight2.Enabled = true; fx.DirectionalLight2.Direction = -Lighting.BounceFrom; fx.DirectionalLight2.DiffuseColor = Lighting.Bounce; fx.DirectionalLight2.SpecularColor = Vector3.Zero;
            fx.FogColor = Lighting.Fog; fx.FogStart = Lighting.FogStart; fx.FogEnd = Lighting.FogEnd;
            fx.PreferPerPixelLighting = true; fx.SpecularPower = 28;
            leafFx = new AlphaTestEffect(gd);
            leafFx.FogColor = Lighting.Fog; leafFx.FogStart = Lighting.FogStart; leafFx.FogEnd = Lighting.FogEnd; leafFx.FogEnabled = true;
            leafFx.VertexColorEnabled = true; leafFx.AlphaFunction = CompareFunction.Greater; leafFx.ReferenceAlpha = 107;
            for (int i = 0; i < 4096; i++)
            {
                qi[i * 6] = (short)(i * 4); qi[i * 6 + 1] = (short)(i * 4 + 1); qi[i * 6 + 2] = (short)(i * 4 + 2);
                qi[i * 6 + 3] = (short)(i * 4); qi[i * 6 + 4] = (short)(i * 4 + 2); qi[i * 6 + 5] = (short)(i * 4 + 3);
            }
            White = new Texture2D(gd, 1, 1, false, SurfaceFormat.Color);
            White.SetData(new[] { Color.White });
        }

        // ---- uploads ----------------------------------------------------------------------------------------------
        public Texture2D Upload(Tex t)
        {
            Texture2D tex = new Texture2D(gd, t.W, t.H, t.Mips, SurfaceFormat.Color);
            Color[] px = t.Px; int w = t.W, h = t.H, level = 0;
            tex.SetData(0, null, px, 0, px.Length);
            while (t.Mips && (w > 1 || h > 1))
            {
                int nw = Math.Max(1, w / 2), nh = Math.Max(1, h / 2);
                Color[] np = new Color[nw * nh];
                for (int y = 0; y < nh; y++)
                    for (int x = 0; x < nw; x++)
                    {
                        // alpha-weighted box filter, so transparent texels do not darken the edges of foliage
                        float r = 0, g = 0, b = 0, a = 0;
                        for (int k = 0; k < 4; k++)
                        {
                            Color c = px[Math.Min(h - 1, y * 2 + (k >> 1)) * w + Math.Min(w - 1, x * 2 + (k & 1))];
                            float ca = c.A + 0.001f; r += c.R * ca; g += c.G * ca; b += c.B * ca; a += ca;
                        }
                        np[y * nw + x] = new Color((byte)(r / a), (byte)(g / a), (byte)(b / a), (byte)Math.Min(255, a / 4));
                    }
                level++;
                tex.SetData(level, null, np, 0, np.Length);
                px = np; w = nw; h = nh;
            }
            return tex;
        }

        Mesh Up(Geo g)
        {
            if (g == null || g.Count < 3) return null;
            Vtx[] v = new Vtx[g.Count];
            for (int i = 0; i < v.Length; i++) v[i] = new Vtx(g.P[i], g.N[i], g.C != null ? g.C[i] : Color.White, new Vector2(g.T[i].X, 1 - g.T[i].Y));
            Mesh m = new Mesh { Vb = new VertexBuffer(gd, Vtx.Decl, v.Length, BufferUsage.WriteOnly), Prims = v.Length / 3, Bs = BoundingSphere.CreateFromPoints(g.P) };
            m.Vb.SetData(v);
            return m;
        }

        Mesh Up(IndexedMesh g)
        {
            Vtx[] v = new Vtx[g.P.Length];
            for (int i = 0; i < v.Length; i++) v[i] = new Vtx(g.P[i], g.N[i], g.C[i], g.T[i]);
            Mesh m = new Mesh
            {
                Vb = new VertexBuffer(gd, Vtx.Decl, v.Length, BufferUsage.WriteOnly), Ib = new IndexBuffer(gd, IndexElementSize.SixteenBits, g.Idx.Length, BufferUsage.WriteOnly),
                Prims = g.Idx.Length / 3, Bs = BoundingSphere.CreateFromPoints(g.P),
            };
            m.Vb.SetData(v); m.Ib.SetData(g.Idx);
            return m;
        }

        Mesh UpLeaf(Geo g)
        {
            if (g == null || g.Count < 3) return null;
            VertexPositionColorTexture[] v = new VertexPositionColorTexture[g.Count];
            for (int i = 0; i < v.Length; i++) v[i] = new VertexPositionColorTexture(g.P[i], g.C[i], new Vector2(g.T[i].X, 1 - g.T[i].Y));
            Mesh m = new Mesh { Vb = new VertexBuffer(gd, VertexPositionColorTexture.VertexDeclaration, v.Length, BufferUsage.WriteOnly), Prims = v.Length / 3 };
            m.Vb.SetData(v);
            return m;
        }

        // The textures the title and loading screens need; available before the world is.
        public void LoadBasics(Textures t)
        {
            Soft = Upload(t.Soft); Glow = Upload(t.Glow); ScopeTex = Upload(t.Scope); Vignette = Upload(t.Vignette);
        }

        public void SetWorld(World w)
        {
            world = w;
            Textures t = w.Tex;
            stone = Upload(t.Stone); slate = Upload(t.Slate); snow = Upload(t.Snow); rock = Upload(t.Rock); bark = Upload(t.Bark); timber = Upload(t.Timber);
            leather = Upload(t.Leather); water = Upload(t.Water); leaves = Upload(t.Leaves); banner = Upload(t.Banner); windows = Upload(t.Windows); cloud = Upload(t.Cloud);
            if (Soft == null) LoadBasics(t);

            foreach (IndexedMesh m in w.Terrain) terrain.Add(Up(m));

            timberMat = new Mat { Tex = timber, Colour = M.Hex(0x7a8496), Spec = 0.35f };       // plasteel
            ironMat = new Mat { Colour = M.Hex(0x34353a), Spec = 0.6f };
            Dictionary<string, Mat> mats = new Dictionary<string, Mat>
            {
                { "stone", new Mat { Tex = stone, Colour = M.Hex(0xb04a3c) * 1.45f, Spec = 0.04f } },       // Kremlin red brick
                { "plaster", new Mat { Tex = stone, Colour = M.Hex(0xf2dfa4) * 1.3f, Spec = 0.03f } },      // ochre palace walls
                { "concrete", new Mat { Tex = stone, Colour = M.Hex(0x8f949c) * 1.35f, Spec = 0.03f } },
                { "slate", new Mat { Tex = slate, Colour = M.Hex(0x4f9a78) * 1.15f, Spec = 0.3f } },         // green tented roofs
                { "dark", new Mat { Colour = M.Hex(0x0b0a09) } },
                { "iron", ironMat },
                { "gold", new Mat { Colour = M.Hex(0xd9b04a), Spec = 0.9f } },
                { "timber", timberMat },
                { "tent", new Mat { Tex = leather, Colour = M.Hex(0x6a7482) * 1.5f, TwoSided = true } },
                { "tentRed", new Mat { Tex = leather, Colour = M.Hex(0xa8362a) * 1.5f, TwoSided = true } },
                { "neon", new Mat { Colour = new Vector3(0.25f, 0.88f, 1.0f), Lit = false } },
                { "star", new Mat { Colour = new Vector3(1.0f, 0.13f, 0.11f), Lit = false } },
                { "ember", new Mat { Colour = new Vector3(1.0f, 0.42f, 0.1f), Lit = false } },
                { "tower", new Mat { Tex = windows, Colour = new Vector3(1.15f), Lit = false } },
                { "beaconPole", new Mat { Colour = M.Hex(0x22262e), Spec = 0.4f } },
                { "beacon", new Mat { Colour = new Vector3(1.0f, 0.22f, 0.18f), Lit = false } },
                { "strip", new Mat { Colour = new Vector3(0.35f, 0.95f, 1.0f), Lit = false } },
            };
            foreach (Builder b in new[] { w.Castle.B, w.City.B })
                foreach (KeyValuePair<string, Geo> kv in b.Parts)
                {
                    Mat mat; if (!mats.TryGetValue(kv.Key, out mat)) mat = new Mat();
                    Mesh mesh = Up(kv.Value);
                    if (mesh == null) continue;
                    (mat.Lit ? statics : emissive).Add(new Part { Mesh = mesh, Mat = mat });
                }
            tether = Up(w.City.Tether);

            foreach (Door d in w.Castle.Doors)
                foreach (DoorLeaf l in d.Leaves)
                {
                    LeafGpu lg = new LeafGpu { D = d, L = l, Leaf = Up(TileUv(Geo.Box(l.W, l.H, 0.14f).Translate(l.W / 2, l.H / 2, 0))), Straps = new Mesh[1] };
                    Geo straps = new Geo();
                    foreach (float y in new[] { 0.18f, 0.5f, 0.82f }) straps.Add(Geo.Box(l.W * 0.9f, 0.09f, 0.19f).Translate(l.W * 0.47f, l.H * y, 0));
                    lg.Straps[0] = Up(straps);
                    doorLeaves.Add(lg);
                }

            foreach (Banner b in w.Castle.Banners)
            {
                Geo g = Geo.Plane(b.W, b.H, b.Sx, b.Sy);
                if (b.Flag) g.Translate(b.W / 2, 0, 0);
                // index the grid so each frame only moves (Sx + 1) x (Sy + 1) vertices
                int row = b.Sx + 1, n = row * (b.Sy + 1);
                BannerGpu bg = new BannerGpu { B = b, V = new Vtx[n], Base = new Vector3[n], Uv = new Vector2[n], Idx = new short[b.Sx * b.Sy * 6] };
                for (int iy = 0; iy <= b.Sy; iy++)
                    for (int ix = 0; ix <= b.Sx; ix++)
                    {
                        int i = iy * row + ix;
                        bg.Base[i] = new Vector3(ix * b.W / b.Sx - b.W / 2 + (b.Flag ? b.W / 2 : 0), b.H / 2 - iy * b.H / b.Sy, 0);
                        bg.Uv[i] = new Vector2((float)ix / b.Sx, 1 - (float)iy / b.Sy);
                    }
                int k = 0;
                for (int iy = 0; iy < b.Sy; iy++)
                    for (int ix = 0; ix < b.Sx; ix++)
                    {
                        int a = ix + row * iy, bb = ix + row * (iy + 1), c = ix + 1 + row * (iy + 1), d = ix + 1 + row * iy;
                        bg.Idx[k++] = (short)a; bg.Idx[k++] = (short)bb; bg.Idx[k++] = (short)d; bg.Idx[k++] = (short)bb; bg.Idx[k++] = (short)c; bg.Idx[k++] = (short)d;
                    }
                banners.Add(bg);
            }
            bannerMat = new Mat { Tex = banner, TwoSided = true };
            ringMesh = Up(Geo.Torus(Castle.CapR, 0.07f, 6, 72).RotateX(M.HALF_PI).Translate(Castle.CapX, Config.PLATEAU + 0.12f, Castle.CapZ));
            shockMesh = Up(Geo.Ring(0.82f, 1, 48));

            foreach (Dictionary<long, VegChunk> map in new[] { w.Veg.Near, w.Veg.Far })
                foreach (VegChunk c in map.Values)
                {
                    Vector3 centre = (c.Min + c.Max) * 0.5f;
                    chunks.Add(new ChunkGpu { Trunk = Up(c.Trunk), Rock = Up(c.Rock), Leaf = UpLeaf(c.Leaf), Bs = new BoundingSphere(centre, (c.Max - c.Min).Length() * 0.5f) });
                }
            Grass gr = w.Veg.Grass;
            grassCap = gr.Cap;
            grassVb = new VertexBuffer[gr.Cells.Length];
            for (int i = 0; i < grassVb.Length; i++) grassVb[i] = new VertexBuffer(gd, VertexPositionColor.VertexDeclaration, grassCap * Grass.TuftVerts, BufferUsage.WriteOnly);
            grassIb = new IndexBuffer(gd, IndexElementSize.SixteenBits, grassCap * Grass.TuftIndices, BufferUsage.WriteOnly);
            grassIb.SetData(Grass.Indices(grassCap));
            foreach (GrassCell c in gr.Cells) c.Dirty = c.N > 0;

            soldier = new Mesh[Soldier.PARTS][];
            for (int i = 0; i < Soldier.PARTS; i++) soldier[i] = new[] { Up(w.SoldierParts[i][0]), w.SoldierParts[i][1] == w.SoldierParts[i][0] ? null : Up(w.SoldierParts[i][1]) };
            soldierMat = new Mat { VCol = true, Spec = 0.35f, Colour = new Vector3(1.6f) };
            rifle = Up(w.Rifle); bolt = Up(w.Bolt);
            bulletMesh = Up(Geo.Cylinder(0.004f, 0.012f, 0.09f, 10).RotateX(-M.HALF_PI));

            // river and lakes: one big sheet at y = 0
            {
                Geo g = Geo.Plane(9000, 9000).RotateX(-M.HALF_PI);
                for (int i = 0; i < g.Count; i++) g.T[i] = g.T[i] * 500;
                waterMesh = Up(g);
            }
            // sky dome, coloured per vertex: haze at the horizon, deep blue overhead, a warm glow around the sun
            {
                Geo g = Geo.Sphere(1, 48, 24);
                VertexPositionColor[] v = new VertexPositionColor[g.Count];
                for (int i = 0; i < v.Length; i++) v[i] = new VertexPositionColor(g.P[i], SkyColour(g.P[i]));
                skyMesh = new Mesh { Vb = new VertexBuffer(gd, VertexPositionColor.VertexDeclaration, v.Length, BufferUsage.WriteOnly), Prims = v.Length / 3 };
                skyMesh.Vb.SetData(v);
            }
            {
                List<short> idx = new List<short>();
                const int rings = 12, seg = 24;
                for (int r = 0; r < rings; r++)
                    for (int s = 0; s < seg; s++)
                    {
                        int a = r * (seg + 1) + s, b = a + seg + 1;
                        idx.Add((short)a); idx.Add((short)b); idx.Add((short)(a + 1)); idx.Add((short)b); idx.Add((short)(b + 1)); idx.Add((short)(a + 1));
                    }
                cloudI = idx.ToArray();
            }
        }

        static Geo TileUv(Geo g)
        {
            for (int i = 0; i < g.Count; i++) { Vector3 p = g.P[i]; g.T[i] = Math.Abs(g.N[i].Z) > 0.5f ? new Vector2(p.X / 1.5f, p.Y / 1.5f) : new Vector2(p.Z, p.Y / 1.5f); }
            return g;
        }

        static Color SkyColour(Vector3 d)
        {
            float t = M.Clamp(d.Y);
            Vector3 zenith = new Vector3(0.24f, 0.34f, 0.58f), horizon = Lighting.Fog;
            Vector3 c = Vector3.Lerp(horizon, zenith, M.Pow(t, 0.5f));
            float sun = Math.Max(0, Vector3.Dot(d, Config.SUN_DIR));
            // warm band along the horizon on the sun's side, and a glow around the disc
            c += new Vector3(0.42f, 0.2f, 0.02f) * M.Pow(sun, 3) * (1 - M.Smoothstep(0.0f, 0.5f, d.Y));
            c += new Vector3(0.6f, 0.36f, 0.14f) * M.Pow(sun, 28);
            if (d.Y < 0) c = Vector3.Lerp(c, horizon, M.Clamp(-d.Y * 6));
            return M.Col(c.X, c.Y, c.Z);
        }

        // ---- state helpers ----------------------------------------------------------------------------------------
        void Use(Mat m)
        {
            fx.LightingEnabled = m.Lit; fx.TextureEnabled = m.Tex != null; fx.Texture = m.Tex; fx.VertexColorEnabled = m.VCol;
            fx.DiffuseColor = m.Colour; fx.Alpha = m.Alpha; fx.FogEnabled = m.Fog;
            fx.SpecularColor = new Vector3(m.Spec);
            gd.RasterizerState = m.TwoSided ? cullNone : cullBack;
        }
        void Unlit(Texture2D tex, Vector3 colour, float alpha, bool fog, bool vcol)
        {
            fx.LightingEnabled = false; fx.TextureEnabled = tex != null; fx.Texture = tex; fx.VertexColorEnabled = vcol;
            fx.DiffuseColor = colour; fx.Alpha = alpha; fx.FogEnabled = fog; fx.SpecularColor = Vector3.Zero;
        }
        void Draw(Mesh m, Matrix w)
        {
            if (m == null) return;
            fx.World = w;
            fx.CurrentTechnique.Passes[0].Apply();
            gd.SetVertexBuffer(m.Vb);
            if (m.Ib != null) { gd.Indices = m.Ib; gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, m.Prims); }
            else gd.DrawPrimitives(PrimitiveType.TriangleList, 0, m.Prims);
        }
        bool Visible(BoundingSphere s) { return frustum.Intersects(s); }

        void Quad(Vector3 c, float half, Color col)
        {
            if (qn >= 4096) return;
            Vector3 r = camRight * half, u = camUp * half; int i = qn * 4;
            qv[i] = new VertexPositionColorTexture(c - r - u, col, new Vector2(0, 1)); qv[i + 1] = new VertexPositionColorTexture(c + r - u, col, new Vector2(1, 1));
            qv[i + 2] = new VertexPositionColorTexture(c + r + u, col, new Vector2(1, 0)); qv[i + 3] = new VertexPositionColorTexture(c - r + u, col, new Vector2(0, 0));
            qn++;
        }
        void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Color col)
        {
            if (qn >= 4096) return;
            int i = qn * 4;
            qv[i] = new VertexPositionColorTexture(p0, col, new Vector2(0, 1)); qv[i + 1] = new VertexPositionColorTexture(p1, col, new Vector2(1, 1));
            qv[i + 2] = new VertexPositionColorTexture(p2, col, new Vector2(1, 0)); qv[i + 3] = new VertexPositionColorTexture(p3, col, new Vector2(0, 0));
            qn++;
        }
        void Flush(Texture2D tex, bool fog)
        {
            if (qn == 0) return;
            Unlit(tex, Vector3.One, 1, fog, true);
            fx.World = Matrix.Identity;
            fx.CurrentTechnique.Passes[0].Apply();
            gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, qv, 0, qn * 4, qi, 0, qn * 2);
            qn = 0;
        }

        // ---- frame ------------------------------------------------------------------------------------------------
        public void DrawWorld(GameSim sim)
        {
            Camera cam = sim.ActiveCam;
            float aspect = gd.Viewport.AspectRatio;
            view = cam.View; proj = cam.Projection(aspect, cam.Near, FAR);
            camPos = cam.Pos; camRight = cam.Right; camUp = cam.Up; camFwd = cam.Forward;
            frustum = new BoundingFrustum(view * proj);
            fx.View = view; fx.Projection = proj;
            float clock = sim.Clock;

            gd.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, new Color(Lighting.Fog), 1, 0);
            gd.SamplerStates[0] = SamplerState.AnisotropicWrap;
            gd.BlendState = BlendState.Opaque;

            // sky and clouds: no depth, centred on the camera
            gd.DepthStencilState = DepthStencilState.None; gd.RasterizerState = cullNone;
            Unlit(null, Vector3.One, 1, false, true);
            fx.World = Matrix.CreateScale(FAR * 0.5f) * Matrix.CreateTranslation(camPos);
            fx.CurrentTechnique.Passes[0].Apply();
            gd.SetVertexBuffer(skyMesh.Vb); gd.DrawPrimitives(PrimitiveType.TriangleList, 0, skyMesh.Prims);
            DrawClouds(clock);

            // ---- opaque ----
            gd.BlendState = BlendState.Opaque; gd.DepthStencilState = DepthStencilState.Default; gd.RasterizerState = cullBack;
            gd.SamplerStates[0] = SamplerState.AnisotropicWrap;
            Mat ground = new Mat { Tex = snow, VCol = true, Spec = 0.06f, Colour = new Vector3(0.98f) };
            Use(ground);
            foreach (Mesh m in terrain) if (Visible(m.Bs)) Draw(m, Matrix.Identity);
            foreach (Part p in statics) { Use(p.Mat); Draw(p.Mesh, Matrix.Identity); }
            foreach (LeafGpu l in doorLeaves)
            {
                if (l.D.Broken) continue;
                Matrix w = Matrix.CreateRotationY(l.L.Angle) * Matrix.CreateTranslation(l.L.Pivot);
                Use(l.D.Iron ? ironMat : timberMat); Draw(l.Leaf, w);
                Use(ironMat); Draw(l.Straps[0], w);
            }
            // trunks and boulders
            Mat barkMat = new Mat { Tex = bark, Colour = new Vector3(1.5f) }, rockMat = new Mat { Tex = rock, Colour = M.Hex(0xd8d2c6) * 1.25f };
            Use(barkMat);
            foreach (ChunkGpu c in chunks) if (c.Trunk != null && Visible(c.Bs)) Draw(c.Trunk, Matrix.Identity);
            Use(rockMat);
            foreach (ChunkGpu c in chunks) if (c.Rock != null && Visible(c.Bs)) Draw(c.Rock, Matrix.Identity);
            DrawSoldiers(sim);
            DrawBanners(sim, clock);

            // grass: unlit, coloured per vertex
            gd.RasterizerState = cullNone;
            Unlit(null, Vector3.One, 1, true, true);
            fx.World = Matrix.Identity;
            fx.CurrentTechnique.Passes[0].Apply();
            gd.Indices = grassIb;
            GrassCell[] cells = world.Veg.Grass.Cells;
            for (int i = 0; i < cells.Length; i++)
            {
                GrassCell c = cells[i];
                if (c.Dirty) { c.Dirty = false; if (c.N > 0) grassVb[i].SetData(c.V, 0, c.N * Grass.TuftVerts); }
                if (!c.Visible || c.Draw <= 0 || !Visible(c.Bs)) continue;
                gd.SetVertexBuffer(grassVb[i]);
                gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, c.Draw * 9);
            }
            // foliage: alpha tested, lighting baked into vertex colours
            gd.SamplerStates[0] = SamplerState.LinearClamp;
            leafFx.View = view; leafFx.Projection = proj; leafFx.World = Matrix.Identity; leafFx.Texture = leaves;
            leafFx.CurrentTechnique.Passes[0].Apply();
            foreach (ChunkGpu c in chunks)
            {
                if (c.Leaf == null || !Visible(c.Bs)) continue;
                gd.SetVertexBuffer(c.Leaf.Vb); gd.DrawPrimitives(PrimitiveType.TriangleList, 0, c.Leaf.Prims);
            }
            gd.SamplerStates[0] = SamplerState.AnisotropicWrap;

            // lights: neon strips, tower stars, embers, skyline windows, the capture ring
            gd.RasterizerState = cullBack;
            foreach (Part p in emissive) { Use(p.Mat); Draw(p.Mesh, Matrix.Identity); }
            Unlit(null, sim.Capture.Done ? new Vector3(0.3f, 0.9f, 1.0f) : new Vector3(1.0f, 0.2f, 0.15f), 1, false, false);
            Draw(ringMesh, Matrix.Identity);
            DrawBirds(sim);

            // ---- transparent ----
            gd.BlendState = BlendState.NonPremultiplied; gd.DepthStencilState = DepthStencilState.DepthRead;
            // water, drifting slowly
            Use(new Mat { Tex = water, Colour = new Vector3(0.20f, 0.27f, 0.36f), Spec = 0.9f, Alpha = 0.9f });
            Draw(waterMesh, Matrix.CreateTranslation(60 + (clock * 0.216f) % 18, 0, 190 + (clock * 0.072f) % 18));
            gd.RasterizerState = cullNone;
            Unlit(null, new Vector3(0.5f, 0.95f, 1.0f), 0.8f, false, false);
            Draw(tether, Matrix.Identity);
            DrawSprites(sim);
        }

        void DrawClouds(float clock)
        {
            const int rings = 12, seg = 24; const float R = 18000, H = 1900, TILE = 37500;
            float ox = camPos.X, oz = camPos.Z, du = clock * 0.0035f / 6, dv = clock * 0.0012f / 6;
            for (int r = 0; r <= rings; r++)
            {
                float rad = R * r / rings, fade = M.Smoothstep(17000, 4000, rad);
                for (int s = 0; s <= seg; s++)
                {
                    float a = (float)s / seg * M.TAU, x = ox + M.Cos(a) * rad, z = oz + M.Sin(a) * rad;
                    cloudV[r * (seg + 1) + s] = new VertexPositionColorTexture(new Vector3(x, H, z), new Color((byte)255, (byte)255, (byte)255, (byte)(fade * 255)), new Vector2(x / TILE + du, z / TILE + dv));
                }
            }
            gd.BlendState = BlendState.NonPremultiplied;
            gd.SamplerStates[0] = SamplerState.LinearWrap;
            Unlit(cloud, Vector3.One, 1, false, true);
            // the far plane is closer than the cloud deck's rim, so the deck gets its own projection
            Matrix keep = fx.Projection;
            fx.Projection = Matrix.CreatePerspectiveFieldOfView(proj.M22 > 0 ? 2 * (float)Math.Atan(1 / proj.M22) : 1, gd.Viewport.AspectRatio, 50, 40000);
            fx.World = Matrix.Identity;
            fx.CurrentTechnique.Passes[0].Apply();
            gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, cloudV, 0, cloudV.Length, cloudI, 0, cloudI.Length / 3);
            fx.Projection = keep;
        }

        void DrawSoldiers(GameSim sim)
        {
            Use(soldierMat);
            float reach = 260 * (Player.BASE_FOV / sim.ActiveCam.Fov);
            foreach (Soldier s in sim.Legion.Soldiers)
            {
                Vector3 mid = s.Pos + new Vector3(0, 1, 0);
                if (!Visible(new BoundingSphere(mid, 3))) continue;
                s.Matrices(parts);
                int k = s.Centurion ? 1 : 0;
                // a few pixels tall at this range: body, shield and legs are enough
                bool far = Vector3.Distance(mid, camPos) > reach;
                for (int i = 0; i < Soldier.PARTS; i++)
                {
                    if (far && (i == Soldier.ARM_L || i == Soldier.ARM_R || i == Soldier.PILUM || i == Soldier.HEAD)) continue;
                    Draw(soldier[i][k] ?? soldier[i][0], parts[i]);
                }
            }
        }

        void DrawBanners(GameSim sim, float t)
        {
            gd.SamplerStates[0] = SamplerState.LinearClamp;
            foreach (BannerGpu bg in banners)
            {
                Banner b = bg.B;
                if (!Visible(new BoundingSphere(b.Pos, Math.Max(b.W, b.H) + 1))) continue;
                Matrix w = Matrix.CreateRotationY(b.RotY) * Matrix.CreateTranslation(b.Pos);
                Vector3 n = Vector3.TransformNormal(Vector3.Backward, w);
                for (int i = 0; i < bg.V.Length; i++)
                {
                    Vector3 p = bg.Base[i]; Vector2 uv = bg.Uv[i];
                    if (b.Flag) { float pin = uv.X; p.Z += M.Sin(uv.X * 7 - t * 5 + uv.Y * 2) * 0.28f * pin; p.Y += M.Sin(uv.X * 4 - t * 3.1f) * 0.12f * pin; }
                    else { float pin = 1 - uv.Y; p.Z += M.Sin(uv.Y * 8 - t * 2.3f + p.X * 1.5f) * 0.16f * pin; }
                    bg.V[i] = new Vtx(Vector3.Transform(p, w), n, Color.White, new Vector2(uv.X, 1 - uv.Y));
                }
                bannerMat.Colour = b.Blue ? M.Hex(0x6fd0ff) * 1.5f : new Vector3(1.5f);
                Use(bannerMat);
                fx.World = Matrix.Identity;
                fx.CurrentTechnique.Passes[0].Apply();
                gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, bg.V, 0, bg.V.Length, bg.Idx, 0, bg.Idx.Length / 3);
            }
            gd.SamplerStates[0] = SamplerState.AnisotropicWrap;
            gd.RasterizerState = cullBack;
        }

        void DrawBirds(GameSim sim)
        {
            Color col = M.Rgb(0x15130f);
            Vector3 a = new Vector3(0, 0, -0.18f), b = new Vector3(0.75f, 0, 0.05f), c = new Vector3(0, 0, 0.2f);
            int n = 0;
            foreach (Bird bird in sim.Fx.Birds)
            {
                Matrix body = Matrix.CreateRotationY(bird.RotY) * Matrix.CreateTranslation(bird.Pos);
                for (int side = 0; side < 2; side++)
                {
                    Matrix wing = (side == 0 ? Matrix.CreateRotationZ(bird.Flap) : Matrix.CreateScale(-1, 1, 1) * Matrix.CreateRotationZ(-bird.Flap)) * body;
                    birdV[n++] = new VertexPositionColor(Vector3.Transform(a, wing), col);
                    birdV[n++] = new VertexPositionColor(Vector3.Transform(b, wing), col);
                    birdV[n++] = new VertexPositionColor(Vector3.Transform(c, wing), col);
                }
            }
            gd.RasterizerState = cullNone;
            Unlit(null, Vector3.One, 1, true, true);
            fx.World = Matrix.Identity;
            fx.CurrentTechnique.Passes[0].Apply();
            gd.DrawUserPrimitives(PrimitiveType.TriangleList, birdV, 0, n / 3);
        }

        void DrawSprites(GameSim sim)
        {
            Fx f = sim.Fx;
            gd.SamplerStates[0] = SamplerState.LinearClamp;
            gd.RasterizerState = cullNone;
            // blob shadows under soldiers nearby
            float near = 160 * (Player.BASE_FOV / sim.ActiveCam.Fov);
            foreach (Soldier s in sim.Legion.Soldiers)
            {
                if (!s.Alive || Vector3.DistanceSquared(s.Pos, camPos) > near * near) continue;
                Vector3 p = s.Pos + new Vector3(0, 0.05f, 0);
                Color c = new Color((byte)0, (byte)0, (byte)10, (byte)110);
                Quad(p + new Vector3(-0.6f, 0, -0.6f), p + new Vector3(0.6f, 0, -0.6f), p + new Vector3(0.6f, 0, 0.6f), p + new Vector3(-0.6f, 0, 0.6f), c);
            }
            Flush(Soft, true);
            // shockwaves
            foreach (Shockwave r in f.Rings)
            {
                if (!r.Visible) continue;
                Unlit(null, new Vector3(1.0f, 0.89f, 0.69f), r.Opacity, false, false);
                Draw(shockMesh, Matrix.CreateScale(r.Scale) * Matrix.CreateTranslation(r.Pos));
            }
            // tracers
            foreach (Bullet b in sim.Weapon.Bullets)
            {
                if (b.Done) continue;
                float travelled = Vector3.Distance(b.Pos, b.Origin), len = Math.Min(b.Cam ? 0.9f : 26, travelled * 0.5f);      // kept short in the kill-cam, which rides just behind the round
                if (len < 0.05f) continue;
                Vector3 dir = Vector3.Normalize(b.Vel), toCam = camPos - b.Pos;
                Vector3 side = Vector3.Cross(dir, toCam);
                if (side.LengthSquared() < 1e-8f) continue;
                side = Vector3.Normalize(side) * (b.Explosive ? 0.12f : b.Cam ? 0.012f : 0.045f);
                Vector3 tail = b.Pos - dir * len;
                Color c = b.Explosive ? new Color((byte)255, (byte)138, (byte)58, (byte)130) : new Color((byte)255, (byte)241, (byte)216, (byte)(b.Cam ? 70 : 56));
                Quad(tail - side, tail + side, b.Pos + side, b.Pos - side, c);
            }
            Flush(White, false);
            // kill-cam round
            Bullet kb = sim.KcBullet;
            if (kb != null && !kb.Done)
            {
                gd.BlendState = BlendState.Opaque; gd.DepthStencilState = DepthStencilState.Default;
                Use(new Mat { Colour = M.Hex(0xc8793a) * 1.3f, Spec = 1 });
                Draw(bulletMesh, Matrix.CreateScale(2.2f) * Matrix.CreateWorld(kb.Pos, Vector3.Normalize(kb.Vel), Vector3.Up));
                gd.BlendState = BlendState.NonPremultiplied; gd.DepthStencilState = DepthStencilState.DepthRead; gd.RasterizerState = cullNone;
            }
            // smoke, dust, debris, water
            for (int i = 0; i < f.Count; i++)
            {
                Particle p = f.P[i];
                if (p.R > 1.5f) continue;
                Quad(new Vector3(p.X, p.Y, p.Z), p.Size * 0.5f, M.Col(p.R, p.G, p.B, p.Alpha));
            }
            Flush(Soft, true);
            if (f.SnowVisible)
            {
                Color sc = new Color((byte)255, (byte)255, (byte)255, (byte)205);
                for (int i = 0; i < Fx.SNOW; i++) Quad(f.Snow[i], 0.035f, sc);
                Flush(Soft, false);
            }

            // ---- glows: additive ----
            gd.BlendState = BlendState.Additive;
            for (int i = 0; i < f.Count; i++)
            {
                Particle p = f.P[i];
                if (p.R <= 1.5f) continue;
                Quad(new Vector3(p.X, p.Y, p.Z), p.Size * 0.6f, M.Col(1.0f, 0.4f + (p.G - 1.2f) * 0.3f, 0.1f, Math.Min(1, p.Alpha * 1.25f)));
            }
            Flush(Soft, false);         // the fireball: broad, soft-edged sprites
            if (f.FlashT > 0)
            {
                float k = f.FlashT / 0.4f;
                Quad(f.FlashPos, 38 * k + 8, M.Col(1.0f, 0.72f, 0.42f, k * k));
                Quad(f.FlashPos, 10 * k + 2, M.Col(1.0f, 0.95f, 0.8f, k));
            }
            foreach (Bullet b in sim.Weapon.Bullets)
            {
                if (b.Done || !b.Explosive) continue;
                float s = 1 + Vector3.Distance(b.Pos, b.Origin) * 0.02f;
                Quad(b.Pos, 0.3f * s, M.Col(1.0f, 0.55f, 0.15f, 1)); Quad(b.Pos, 0.1f * s, M.Col(1.0f, 0.95f, 0.8f, 1));
            }
            Flush(Glow, false);
            foreach (Vector3 p in world.Castle.Stars) { Quad(p, 3.2f, new Color((byte)255, (byte)40, (byte)30, (byte)150)); }
            foreach (Vector3 p in world.City.Beacons) { float pulse = 0.6f + 0.4f * M.Sin(sim.Clock * 1.7f + p.X); Quad(p, 22, new Color((byte)255, (byte)50, (byte)40, (byte)(170 * pulse))); }
            foreach (Vector3 p in world.Castle.Torches) Quad(p, 1.3f, new Color((byte)255, (byte)150, (byte)70, (byte)120));
            foreach (Vector3 p in world.Castle.Smoke) if (p.Y < Config.PLATEAU) Quad(p, 1.6f, new Color((byte)255, (byte)140, (byte)60, (byte)130));
            Flush(Glow, true);
            // the sun, low over the valley
            Vector3 sunPos = camPos + Config.SUN_DIR * 6000;
            Quad(sunPos, 2600, new Color((byte)255, (byte)170, (byte)110, (byte)70));
            Quad(sunPos, 520, new Color((byte)255, (byte)226, (byte)190, (byte)255));
            Flush(Glow, false);
            gd.BlendState = BlendState.NonPremultiplied;
        }

        // The rifle and the sniper's arms, drawn last with their own depth range so they never clip into walls.
        public void DrawViewModel(GameSim sim)
        {
            Weapon wp = sim.Weapon;
            if (sim.KcActive || !wp.ModelVisible) return;
            Camera cam = sim.Player.Cam;
            gd.Clear(ClearOptions.DepthBuffer, Color.Black, 1, 0);
            gd.BlendState = BlendState.Opaque; gd.DepthStencilState = DepthStencilState.Default; gd.RasterizerState = cullNone;
            fx.View = cam.View; fx.Projection = cam.Projection(gd.Viewport.AspectRatio, 0.02f, 10);
            Use(new Mat { VCol = true, Spec = 0.5f, Colour = new Vector3(1.5f), Fog = false, TwoSided = true });
            Matrix model = wp.ModelLocal * cam.World;
            Draw(rifle, model);
            Draw(bolt, wp.BoltLocal * model);
            if (wp.FlashVisible)
            {
                gd.BlendState = BlendState.Additive; gd.DepthStencilState = DepthStencilState.DepthRead;
                camPos = cam.Pos; camRight = cam.Right; camUp = cam.Up;
                Vector3 p = Vector3.Transform(new Vector3(0, 0.014f, -0.9f), model);
                Quad(p, 0.22f, new Color((byte)255, (byte)217, (byte)160, (byte)230)); Quad(p, 0.09f, Color.White);
                Flush(Glow, false);
            }
            fx.View = view; fx.Projection = proj;
        }

        // How strongly an explosion lights up the screen: fades with distance and time.
        public float BlastGlare(GameSim sim)
        {
            if (sim.Fx.FlashT <= 0) return 0;
            float k = sim.Fx.FlashT / 0.4f, d = Vector3.Distance(sim.Fx.FlashPos, sim.ActiveCam.Pos);
            return k * k * M.Clamp(1 - d / 140) * 0.5f;
        }
    }
}
