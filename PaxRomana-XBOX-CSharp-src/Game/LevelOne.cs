// Level One: the enrichment facility under BlitzScheiss Mountain. The agent arrives in the service lock, has to get
// through four interlocks (weight on a plate, a beam steered by mirrors, three targets lit at once, a crate carried
// across a gap through a pair of linked gates), clears the centrifuge hall, plants a device at the cascade controls,
// escapes up the service tunnel and sets the device off from outside the mountain.
//
// Facility is the place: rooms, doors and the things that move. Sabotage is the rules.
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace PaxRomana
{
    public sealed class Crate { public Vector3 Pos, Home; public bool Carried; public Solid Solid; public Prop Prop; }
    public sealed class Plate { public float X, Z; public bool Down; public Prop Prop; }
    public sealed class Mirror { public float X, Z; public int Kind; public Prop Prop; }        // Kind 0 turns east to south, 1 turns east to north
    public sealed class Target { public Vector3 Pos; public float LitT; public Prop Prop; }
    public sealed class Gate { public float X, Z, Nx; public Prop Prop; }                      // Nx: which way it faces along x (+1 or -1)

    public sealed class Facility : Site
    {
        public const float FY = 100;                 // floor height
        const float T = 1;                           // wall thickness
        public const float HubX = 0, HubZ = -142, HubR = 2.4f;                     // the cascade controls
        public const float TunnelZ = -142, MouthX = 96, DetonateX = 112;           // the service tunnel and the way out
        public const float BlastDoorX = 60;
        public static readonly string[] Keys = { "panel", "floor", "ceiling", "steel", "concrete", "rock", "dark", "iron", "hazard" };

        public readonly Crate[] Crates = new Crate[2];
        public readonly Plate[] Plates = new Plate[2];
        public readonly Mirror[] Mirrors = new Mirror[2];
        public readonly Target[] Targets = new Target[3];
        public readonly Gate[] Gates = new Gate[2];
        public Target Release;                        // the blast door's lock: shoot it
        public Door LockDoor, BlastDoor, HallExit; public readonly Door[] Interlock = new Door[4];
        public readonly List<Vector3> Lamps = new List<Vector3>();
        public Prop Emitter, Receiver, Device, HubRing, ArrivalPad; public readonly List<Prop> Rotors = new List<Prop>();
        public static readonly Vector3 EmitterPos = new Vector3(-8.6f, FY + 1.3f, -30), ReceiverPos = new Vector3(-8.6f, FY + 1.3f, -42);
        public IndexedMesh Mountain;

        // ---- the ground ----
        public static float TunnelFloor(float x) { return FY + 12 * M.Clamp((x - 30) / 60); }

        // The mountainside: a ledge at the tunnel mouth, falling away to the east, and the mountain rising behind it.
        public static float Outside(float x, float z)
        {
            float n = Noise.Fbm(x * 0.02 + 3, z * 0.02 + 7, 3, 71) - 0.5f;
            if (x >= MouthX)
            {
                float d = x - MouthX;
                return FY + 12 - 0.2f * Math.Max(0, d - 30) - 0.0006f * Math.Max(0, d - 30) * Math.Max(0, d - 30) + n * 6 * M.Smoothstep(24, 90, d);
            }
            float up = MouthX - x, ridge = M.Exp(-((z + 130) / 320) * ((z + 130) / 320));
            return FY + 12 + Math.Min(up, 26) * 1.6f + 150 * (1 - M.Exp(-Math.Max(0, up - 26) / 110)) * (0.35f + 0.65f * ridge) + n * 14 * M.Smoothstep(8, 60, up);
        }

        public static float FloorAt(float x, float z)
        {
            if (x > MouthX) return Outside(x, z);
            if (x > 23 && Math.Abs(z - TunnelZ) < 3.5f) return TunnelFloor(x);
            if (z > -100 && z < -90 && Math.Abs(x) < 9.5f) return FY - 9;        // the gap in the fourth interlock
            return FY;
        }

        // ---- building ----
        void Run(bool alongX, float a, float b, float c0, float c1, float y0, float y1, Gap[] gaps, string key)
        {
            float cur = a;
            if (gaps != null)
                foreach (Gap g in gaps)
                {
                    Seg(alongX, cur, g.At - g.W / 2, c0, c1, y0, y1, true, key);
                    Seg(alongX, g.At - g.W / 2, g.At + g.W / 2, c0, c1, FY + g.H, y1, false, key);      // lintel
                    cur = g.At + g.W / 2;
                }
            Seg(alongX, cur, b, c0, c1, y0, y1, true, key);
        }
        void Seg(bool alongX, float u0, float u1, float c0, float c1, float ya, float yb, bool solid, string key)
        {
            if (u1 - u0 < 0.01f || yb - ya < 0.01f) return;
            float cm = (c0 + c1) / 2, um = (u0 + u1) / 2, ym = (ya + yb) / 2;
            if (alongX) B.Box(key, u1 - u0, yb - ya, c1 - c0, um, ym, cm); else B.Box(key, c1 - c0, yb - ya, u1 - u0, cm, ym, um);
            if (solid) Solids.Add(alongX ? Solid.Box(u0, u1, c0, c1) : Solid.Box(c0, c1, u0, u1));
        }

        // A room given by its inside: x0..x1, z0..z1, height h. The walls stand outside that, one metre thick, with
        // door openings where asked. Lamps hang in a grid under the ceiling.
        void Room(float x0, float x1, float z0, float z1, float h, Gap[] n, Gap[] s, Gap[] e, Gap[] w, bool floor = true)
        {
            float cx = (x0 + x1) / 2, cz = (z0 + z1) / 2, wx = x1 - x0 + 2 * T, wz = z1 - z0 + 2 * T;
            if (floor) B.Box("floor", wx, 0.6f, wz, cx, FY - 0.3f, cz, 0, 3);
            B.Box("ceiling", wx, 0.6f, wz, cx, FY + h + 0.3f, cz, 0, 3);
            Run(true, x0 - T, x1 + T, z0 - T, z0, FY, FY + h, n, "panel");
            Run(true, x0 - T, x1 + T, z1, z1 + T, FY, FY + h, s, "panel");
            Run(false, z0, z1, x0 - T, x0, FY, FY + h, w, "panel");
            Run(false, z0, z1, x1, x1 + T, FY, FY + h, e, "panel");
            // a dark skirting line and a band under the ceiling give the white panels some scale
            B.Box("dark", x1 - x0, 0.12f, 0.04f, cx, FY + 0.06f, z0 + 0.02f); B.Box("dark", x1 - x0, 0.12f, 0.04f, cx, FY + 0.06f, z1 - 0.02f);
            B.Box("dark", 0.04f, 0.12f, z1 - z0, x0 + 0.02f, FY + 0.06f, cz); B.Box("dark", 0.04f, 0.12f, z1 - z0, x1 - 0.02f, FY + 0.06f, cz);
            int nx = Math.Max(1, (int)Math.Round((x1 - x0) / 9)), nz = Math.Max(1, (int)Math.Round((z1 - z0) / 9));
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    float lx = x0 + (i + 0.5f) * (x1 - x0) / nx, lz = z0 + (j + 0.5f) * (z1 - z0) / nz;
                    B.Box("lamp", 1.6f, 0.08f, 0.5f, lx, FY + h - 0.05f, lz);
                    Lamps.Add(new Vector3(lx, FY + h - 0.3f, lz));
                }
        }

        // A double door in a wall that runs along x, in the doorway centred on x = 0, swinging away to the north.
        Door DoorAt(string name, float z, bool auto)
        {
            const float HP = M.HALF_PI, W = 1.2f;
            Door d = AddDoor(name, true, 3.2f, -W, W, z - 0.3f, z + 0.3f, new[] { new[] { -W, z, W, 0, HP }, new[] { W, z, W, M.PI, HP } }, FY);
            d.Auto = auto;
            // a light strip over the doorway shows whether it is held shut
            return d;
        }

        static Geo Disc(float r, float h) { return Geo.Cylinder(r, r, h, 24).Translate(0, h / 2, 0); }

        Prop Add(Prop p) { Props.Add(p); return p; }

        public static Facility Build()
        {
            Facility f = new Facility();
            f.Make();
            return f;
        }

        void Make()
        {
            Gap door = new Gap(0, 2.4f, 3.2f);
            Gap[] dn = { door };

            // ---- the service lock, where the agent arrives ----
            Room(-6, 6, 0, 12, 5, dn, null, null, null);
            B.Add("steel", Geo.Torus(1.5f, 0.07f, 6, 40).RotateX(M.HALF_PI).Translate(0, FY + 0.05f, 8));
            ArrivalPad = Add(new Prop { Shape = Disc(1.4f, 0.03f), World = Matrix.CreateTranslation(0, FY + 0.01f, 8), Lit = false });
            foreach (int s in new[] { -1, 1 }) B.Box("steel", 0.5f, 2.2f, 1.6f, s * 5.6f, FY + 1.1f, 4);           // lockers
            LockDoor = DoorAt("the lock door", -1, false);

            // ---- interlock 1: weight on the plate ----
            Room(-9, 9, -22, -2, 6, dn, dn, null, null);
            Plates[0] = new Plate { X = 5, Z = -8 };
            Crates[0] = new Crate { Home = new Vector3(-6, FY, -18) };
            B.Box("hazard", 2.6f, 0.02f, 2.6f, 5, FY + 0.011f, -8);
            Interlock[0] = DoorAt("the first interlock", -23, true);

            // ---- interlock 2: the beam ----
            Room(-9, 9, -46, -24, 6, dn, dn, null, null);
            Mirrors[0] = new Mirror { X = 4, Z = -30, Kind = 0 };
            Mirrors[1] = new Mirror { X = 4, Z = -42, Kind = 1 };
            foreach (Mirror m in Mirrors)
            {
                B.Cyl("steel", 0.28f, 0.36f, 0.9f, m.X, FY + 0.45f, m.Z, 12, 2);
                Solids.Add(Solid.Circle(m.X, m.Z, 0.55f));
            }
            B.Box("steel", 0.5f, 0.7f, 0.7f, -8.75f, EmitterPos.Y, EmitterPos.Z); B.Box("steel", 0.5f, 0.7f, 0.7f, -8.75f, ReceiverPos.Y, ReceiverPos.Z);
            Emitter = Add(new Prop { Shape = Geo.Sphere(0.16f, 10, 8), World = Matrix.CreateTranslation(EmitterPos), Lit = false, Colour = new Vector3(1.0f, 0.25f, 0.2f) });
            Receiver = Add(new Prop { Shape = Geo.Sphere(0.2f, 10, 8), World = Matrix.CreateTranslation(ReceiverPos), Lit = false });
            Interlock[1] = DoorAt("the second interlock", -47, true);

            // ---- interlock 3: three targets at once ----
            Room(-9, 9, -78, -48, 7, dn, dn, null, null);
            Targets[0] = new Target { Pos = new Vector3(-6, FY + 4.6f, -77.95f) };
            Targets[1] = new Target { Pos = new Vector3(6, FY + 4.6f, -77.95f) };
            Targets[2] = new Target { Pos = new Vector3(8.95f, FY + 4.6f, -62) };
            for (int i = 0; i < 3; i++)
            {
                Target t = Targets[i];
                Matrix face = i < 2 ? Matrix.CreateRotationX(M.HALF_PI) : Matrix.CreateRotationZ(M.HALF_PI);        // the disc lies flat: stand it up on its wall
                t.Prop = Add(new Prop { Shape = Disc(0.55f, 0.06f), World = face * Matrix.CreateTranslation(t.Pos), Lit = false });
                if (i < 2) B.Add("steel", Geo.Torus(0.62f, 0.05f, 6, 28).Translate(t.Pos.X, t.Pos.Y, t.Pos.Z + 0.02f));
                else B.Add("steel", Geo.Torus(0.62f, 0.05f, 6, 28).RotateY(M.HALF_PI).Translate(t.Pos.X - 0.02f, t.Pos.Y, t.Pos.Z));
            }
            for (int i = 0; i < 4; i++) B.Box("concrete", 1.2f, 1.1f, 0.5f, -6 + i * 4, FY + 0.55f, -56);               // a firing line to rest behind
            Interlock[2] = DoorAt("the third interlock", -79, true);

            // ---- interlock 4: the gap and the linked gates ----
            Room(-9, 9, -110, -80, 7, dn, dn, null, null, false);
            B.Box("floor", 20, 0.6f, 11, 0, FY - 0.3f, -84.5f, 0, 3);            // near side, z -90 .. -79
            B.Box("floor", 20, 0.6f, 11, 0, FY - 0.3f, -105.5f, 0, 3);           // far side, z -111 .. -100
            B.Box("dark", 20, 0.4f, 10, 0, FY - 9.2f, -95);                      // the bottom of the gap
            B.Box("panel", 18, 9, 0.3f, 0, FY - 4.5f, -89.85f); B.Box("panel", 18, 9, 0.3f, 0, FY - 4.5f, -100.15f);
            foreach (int s in new[] { -1, 1 }) B.Box("panel", T, 9, 10, s * 9.5f, FY - 4.5f, -95);
            B.Box("hazard", 18, 0.02f, 0.5f, 0, FY + 0.011f, -89.7f); B.Box("hazard", 18, 0.02f, 0.5f, 0, FY + 0.011f, -100.3f);
            Gates[0] = new Gate { X = -9, Z = -85, Nx = 1 };
            Gates[1] = new Gate { X = 9, Z = -105, Nx = -1 };
            foreach (Gate g in Gates)
            {
                B.Box("steel", 0.3f, 3.4f, 0.3f, g.X + g.Nx * 0.15f, FY + 1.7f, g.Z - 1.25f); B.Box("steel", 0.3f, 3.4f, 0.3f, g.X + g.Nx * 0.15f, FY + 1.7f, g.Z + 1.25f);
                B.Box("steel", 0.3f, 0.3f, 2.8f, g.X + g.Nx * 0.15f, FY + 3.25f, g.Z);
                g.Prop = Add(new Prop { Shape = Geo.Box(0.06f, 3.0f, 2.2f), World = Matrix.CreateTranslation(g.X + g.Nx * 0.06f, FY + 1.55f, g.Z), Lit = false });
            }
            Crates[1] = new Crate { Home = new Vector3(6, FY, -84) };
            Plates[1] = new Plate { X = -5, Z = -106 };
            B.Box("hazard", 2.6f, 0.02f, 2.6f, -5, FY + 0.011f, -106);
            Interlock[3] = DoorAt("the fourth interlock", -111, true);

            foreach (Plate p in Plates) p.Prop = Add(new Prop { Shape = Disc(0.9f, 0.08f), World = Matrix.CreateTranslation(p.X, FY, p.Z), Spec = 0.4f });
            foreach (Crate c in Crates)
            {
                Geo g = Geo.Box(0.8f, 0.8f, 0.8f).Paint(0x8f959c);
                foreach (int s in new[] { -1, 1 }) { g.Add(Geo.Box(0.84f, 0.08f, 0.84f).Translate(0, s * 0.36f, 0).Paint(0x3a3f46)); }
                g.Add(Geo.Box(0.3f, 0.3f, 0.82f).Paint(0xd8a61c)); g.Add(Geo.Box(0.82f, 0.3f, 0.3f).Paint(0xd8a61c));
                c.Prop = Add(new Prop { Shape = g, Spec = 0.3f });
                c.Solid = Solid.Circle(c.Home.X, c.Home.Z, 0.55f);
                Solids.Add(c.Solid);
            }
            foreach (Mirror m in Mirrors)
            {
                Geo g = Geo.Box(1.5f, 1.5f, 0.06f).Paint(0xbfd6e6).Add(Geo.Box(1.6f, 0.08f, 0.1f).Translate(0, 0.78f, 0).Paint(0x3a3f46)).Add(Geo.Box(1.6f, 0.08f, 0.1f).Translate(0, -0.78f, 0).Paint(0x3a3f46));
                m.Prop = Add(new Prop { Shape = g, Spec = 1, TwoSided = true });
            }

            // ---- the centrifuge hall ----
            Room(-22, 22, -170, -112, 14, null, dn, new[] { new Gap(TunnelZ, 3.0f, 3.4f) }, null);
            foreach (int side in new[] { -1, 1 })
                for (int col = 0; col < 6; col++)
                {
                    float x = side * (5 + col * 3);
                    foreach (float z0 in new[] { -120f, -148f })
                    {
                        for (int row = 0; row < 5; row++)
                        {
                            float z = z0 - row * 4;
                            B.Cyl("steel", 0.3f, 0.3f, 6.4f, x, FY + 3.5f, z, 12, 3);
                            B.Cyl("concrete", 0.5f, 0.55f, 0.3f, x, FY + 0.15f, z, 12, 3);
                            B.Cyl("dark", 0.1f, 0.1f, 0.9f, x, FY + 7.1f, z, 6);
                            Solids.Add(Solid.Circle(x, z, 0.5f));
                            Rotors.Add(Add(new Prop { Shape = Geo.Cylinder(0.33f, 0.33f, 0.12f, 12), World = Matrix.CreateTranslation(x, FY + 5.6f, z), Lit = false }));
                        }
                        B.Add("steel", Geo.Cylinder(0.11f, 0.11f, 17.4f, 8).RotateX(M.HALF_PI).Translate(x, FY + 7.6f, z0 - 8));        // header pipe along the row
                    }
                }
            foreach (float z in new[] { -128f, -156f }) B.Add("steel", Geo.Cylinder(0.16f, 0.16f, 41, 8).RotateZ(M.HALF_PI).Translate(0, FY + 8.3f, z));
            // the cascade controls, in the cross aisle
            B.Box("steel", 2.6f, 1.0f, 0.9f, HubX, FY + 0.5f, HubZ - 1.3f); B.Box("dark", 2.4f, 0.5f, 0.06f, HubX, FY + 1.25f, HubZ - 1.0f, 0, 4, 0);
            B.Box("lamp", 0.5f, 0.2f, 0.04f, HubX - 0.7f, FY + 1.25f, HubZ - 0.96f);
            Solids.Add(Solid.Box(HubX - 1.3f, HubX + 1.3f, HubZ - 1.75f, HubZ - 0.85f));
            HubRing = Add(new Prop { Shape = Geo.Torus(HubR, 0.06f, 6, 60).RotateX(M.HALF_PI), World = Matrix.CreateTranslation(HubX, FY + 0.1f, HubZ), Lit = false });
            Geo dev = Geo.Box(0.5f, 0.28f, 0.34f).Paint(0x4d5640);
            for (int i = 0; i < 4; i++) dev.Add(Geo.Cylinder(0.045f, 0.045f, 0.3f, 8).RotateZ(M.HALF_PI).Translate(0, 0.19f, -0.12f + i * 0.08f).Paint(0x9a4a36));
            dev.Add(Geo.Box(0.12f, 0.06f, 0.12f).Translate(0.18f, 0.17f, 0).Paint(0x17181b));
            Device = Add(new Prop { Shape = dev, World = Matrix.CreateTranslation(HubX + 0.6f, FY + 1.14f, HubZ - 1.3f), Visible = false });
            foreach (float bx in new[] { -9f, 9f }) Banners.Add(new Banner { W = 3, H = 8, Sx = 6, Sy = 16, Pos = new Vector3(bx, FY + 9, -169.6f) });

            // ---- the service tunnel, up and out ----
            {
                float z = TunnelZ, len = M.Hypot(60, 12), a = M.Atan2(12, 60);
                HallExit = AddDoor("the tunnel door", true, 3.2f, 22.2f, 22.8f, z - 1.5f, z + 1.5f, new[] { new[] { 22.5f, z - 1.5f, 1.5f, -M.HALF_PI, 0 }, new[] { 22.5f, z + 1.5f, 1.5f, M.HALF_PI, 0 } }, FY);
                HallExit.Auto = true;
                B.Box("floor", 8, 0.6f, 7, 27, FY - 0.3f, z, 0, 3);                                              // level start
                B.Box("floor", len, 0.6f, 7, 60, FY + 6 - 0.3f, z, 0, 3, a);                                      // the ramp
                B.Box("floor", 12, 0.6f, 7, 96, FY + 12 - 0.3f, z, 0, 3);                                          // level top, out to the ledge
                B.Box("ceiling", 8, 0.6f, 7, 27, FY + 4.8f, z, 0, 3);
                B.Box("ceiling", len, 0.6f, 7, 60, FY + 6 + 4.8f, z, 0, 3, a);
                B.Box("ceiling", 8, 0.6f, 7, 94, FY + 12 + 4.8f, z, 0, 3);
                foreach (int s in new[] { -1, 1 })
                {
                    B.Box("concrete", 75, 20, T, 60.5f, FY + 8, z + s * 3, 0, 4);
                    Solids.Add(Solid.Box(23, 98, z + s * 3 - 0.5f, z + s * 3 + 0.5f));
                }
                for (int i = 0; i < 6; i++) { float x = 30 + i * 11; B.Box("lamp", 0.5f, 0.08f, 1.2f, x, TunnelFloor(x) + 4.4f, z); Lamps.Add(new Vector3(x, TunnelFloor(x) + 4.2f, z)); }
                // the blast door half way up, and its lock above it
                float by = TunnelFloor(BlastDoorX);
                BlastDoor = AddDoor("the blast door", true, 4.2f, BlastDoorX - 0.3f, BlastDoorX + 0.3f, z - 2.5f, z + 2.5f,
                    new[] { new[] { BlastDoorX, z - 2.5f, 2.5f, -M.HALF_PI, 0 }, new[] { BlastDoorX, z + 2.5f, 2.5f, M.HALF_PI, 0 } }, by);
                BlastDoor.Auto = true;
                Release = new Target { Pos = new Vector3(BlastDoorX - 0.2f, by + 4.5f, z) };
                B.Box("steel", 0.4f, 0.7f, 5, BlastDoorX, by + 4.5f, z);
                Release.Prop = Add(new Prop { Shape = Disc(0.32f, 0.06f), World = Matrix.CreateRotationZ(M.HALF_PI) * Matrix.CreateTranslation(Release.Pos), Lit = false });
                // the portal in the mountainside: a concrete face let into the slope, with an apron in front of it
                foreach (int s in new[] { -1, 1 })
                {
                    B.Box("concrete", 16.2f, 22, 4.5f, 92.1f, FY + 21, z + s * 5.75f, 0, 4);          // wing walls, out past the mouth
                    Solids.Add(Solid.Box(84, MouthX + 4.2f, s > 0 ? z + 3 : z - 8, s > 0 ? z + 8 : z - 3));
                }
                B.Box("concrete", 12, 14.9f, 7, 90, FY + 24.55f, z, 0, 4);
                B.Box("concrete", 4.4f, 1, 16, 98.1f, FY + 11.5f, z, 0, 4);
                B.Box("hazard", 0.3f, 0.02f, 7, 97, FY + 12.011f, z);
                // the mountainside either side of the portal cannot be climbed
                Solids.Add(Solid.Box(MouthX - 6, MouthX, -700, z - 8)); Solids.Add(Solid.Box(MouthX - 6, MouthX, z + 8, 400));
            }
            Mountain = Terrain.Patch(-300, -622, 8, 120, 120, Outside, (x, z) => x > 84 && x < 100 && Math.Abs(z - TunnelZ) < 8);
        }

        // Puts everything back to how the level starts.
        public override void Reset()
        {
            base.Reset();
            foreach (Crate c in Crates) { c.Pos = c.Home; c.Carried = false; c.Solid.X = c.Home.X; c.Solid.Z = c.Home.Z; c.Solid.Off = false; }
            Mirrors[0].Kind = 0; Mirrors[1].Kind = 1;
            foreach (Target t in Targets) t.LitT = 0;
            Release.LitT = 0;
            foreach (Plate p in Plates) p.Down = false;
            Device.Visible = false;
            Beams.Clear(); Glows.Clear();
        }

        public static Arena MakeArena()
        {
            return new Arena
            {
                Ground = FloorAt, X0 = -30, X1 = 190, Z0 = -215, Z1 = 20, Water = false, Bridge = false,
                Spawn = new Vector3(0, FY, 8), SpawnYaw = 0, EdgeHint = "The mountain falls away here. Stay near the tunnel mouth",
            };
        }

        // Cold strip lighting inside; the summer sky outside.
        public static LightRig Indoor()
        {
            return new LightRig
            {
                Sun = new Vector3(0.46f, 0.48f, 0.52f), SunDir = Vector3.Normalize(new Vector3(-0.3f, 0.85f, 0.4f)), Sky = new Vector3(0.22f, 0.24f, 0.28f),
                Bounce = new Vector3(0.26f, 0.26f, 0.27f), BounceFrom = Vector3.Normalize(new Vector3(0.35f, -0.5f, -0.45f)), Ambient = new Vector3(0.56f, 0.58f, 0.63f),
                Fog = new Vector3(0.07f, 0.08f, 0.10f), FogStart = 35, FogEnd = 260,
            };
        }

        // The guards in the centrifuge hall.
        public void Post(Garrison g)
        {
            g.Add(new Route(Pts(-2, -116, -2, -166), false, FY), 6);
            g.Add(new Route(Pts(2, -166, 2, -116), false, FY), 10);
            g.Add(new Route(Pts(-20, HubZ + 2, -5, HubZ + 2), false, FY), 3);
            g.Add(new Route(Pts(20, HubZ - 2, 5, HubZ - 2), false, FY), 5);
            g.Add(new Route(Pts(-3.2f, -168, 3.2f, -168), false, FY), 0, 0, true, 0, true);          // the duty officer, at the far wall
            g.Seal();
        }
        static float[][] Pts(params float[] xz) { float[][] p = new float[xz.Length / 2][]; for (int i = 0; i < p.Length; i++) p[i] = new[] { xz[i * 2], xz[i * 2 + 1] }; return p; }
    }

    public sealed class Sabotage : Mission
    {
        const float FY = Facility.FY;
        const float PLANT = 6;            // seconds at the controls to plant the device
        const float LIT = 8;              // seconds a target stays lit
        readonly Facility f;
        public bool[] Solved = new bool[4];
        public bool Planted, BlastOpen;
        public int Guards = -1, Carrying = -1;
        public bool BeamHome;              // the beam reaches the receiver
        public Action Detonated;           // the game adds the fireworks
        int action; int actionIndex; float gateT, tickT, spin = 1, t; bool toldBlast, toldHall, lastFar;
        const int NONE = 0, PICK = 1, DROP = 2, MIRROR = 3, DOOR = 4, DETONATE = 5;

        public Sabotage(Facility facility, Player player, Garrison garrison, Hud hud, IAudio audio, Pad pad) : base(facility, player, garrison, hud, audio, pad)
        {
            f = facility; EndDelay = 6; Outdoors = 0;
        }

        public override void TitleCamera(float time, out Vector3 pos, out Vector3 look)
        {
            float a = time * 0.05f;
            pos = new Vector3(M.Sin(a) * 15, FY + 10.5f, Facility.HubZ + M.Cos(a) * 22);
            look = new Vector3(0, FY + 3.5f, Facility.HubZ);
        }

        static float Flat(Vector3 a, float x, float z) { return M.Hypot(a.X - x, a.Z - z); }
        Vector3 Ahead(float d) { return new Vector3(player.Pos.X - M.Sin(player.Yaw) * d, player.Pos.Y, player.Pos.Z - M.Cos(player.Yaw) * d); }

        // ---- the use button ----
        public override bool Use()
        {
            switch (action)
            {
                case PICK:
                    Carrying = actionIndex; f.Crates[Carrying].Carried = true; f.Crates[Carrying].Solid.Off = true;
                    audio.Door(); return true;
                case DROP: Drop(); return true;
                case MIRROR:
                    f.Mirrors[actionIndex].Kind = 1 - f.Mirrors[actionIndex].Kind;
                    audio.Door(); return true;
                case DOOR: return UseDoor();
                case DETONATE:
                    Done = true;
                    audio.Loot();
                    hud.Feed("<b>DETONATION</b> the cascade is gone", "head");
                    if (Detonated != null) Detonated();
                    return true;
            }
            return false;
        }

        void Drop()
        {
            Crate c = f.Crates[Carrying];
            Vector3 to = Ahead(1.3f);
            Carrying = -1; c.Carried = false;
            if (Facility.FloorAt(to.X, to.Z) < FY - 1)
            {
                // over the edge: it is gone, and a new one is delivered where the first one stood
                c.Pos = c.Home;
                hud.Hint("The crate fell into the gap. Another has been delivered", 3500);
            }
            else
            {
                to.Y = FY;
                c.Solid.Off = true;                                   // not against itself
                for (int pass = 0; pass < 3; pass++) if (!Collision.Resolve(ref to, 0.5f, f.Solids)) break;
                c.Pos = new Vector3(to.X, FY, to.Z);
            }
            c.Solid.X = c.Pos.X; c.Solid.Z = c.Pos.Z; c.Solid.Off = false;
            audio.Step();
        }

        // ---- bullets and blasts ----
        public override void BulletHit(Vector3 p)
        {
            foreach (Target tg in f.Targets) if (Vector3.Distance(p, tg.Pos) < 0.8f && !Solved[2]) { tg.LitT = LIT; audio.Hit(true); }
            if (Planted && !BlastOpen && Vector3.Distance(p, f.Release.Pos) < 0.7f) OpenBlastDoor();
        }

        public override void Blast(Vector3 p)
        {
            base.Blast(p);
            foreach (Target tg in f.Targets) if (Vector3.Distance(p, tg.Pos) < 3 && !Solved[2]) tg.LitT = LIT;
            if (Planted && !BlastOpen && Vector3.Distance(p, f.Release.Pos) < 7) OpenBlastDoor();
        }

        void OpenBlastDoor()
        {
            BlastOpen = true; f.BlastDoor.Want = 1; audio.Door();
            hud.Feed("<b>BLAST DOOR</b> the lock is shot through", "head");
            pad.Rumble(low: 0.5f, ms: 300, decay: true);
        }

        void Solve(int i, string what)
        {
            if (Solved[i]) return;
            Solved[i] = true;
            audio.Loot();
            hud.Feed("<b>INTERLOCK " + (i + 1) + "</b> " + what, "head");
            pad.Rumble(low: 0.4f, high: 0.4f, ms: 220, decay: true);
        }

        // The beam leaves the emitter going east and turns at each mirror it meets. Returns true if it ends in the receiver.
        bool TraceBeam()
        {
            Vector3 p = Facility.EmitterPos; int dx = 1, dz = 0;
            Vector3 red = new Vector3(1.0f, 0.2f, 0.15f);
            for (int bounce = 0; bounce < 6; bounce++)
            {
                // how far to the wall of the room in this direction
                float reach = dx > 0 ? 9 - p.X : dx < 0 ? p.X + 9 : dz > 0 ? -24 - p.Z : p.Z + 46;
                Mirror hit = null; bool blocked = false, home = false;
                foreach (Mirror m in f.Mirrors)
                {
                    float along = dx != 0 ? (m.X - p.X) * dx : (m.Z - p.Z) * dz, off = dx != 0 ? Math.Abs(m.Z - p.Z) : Math.Abs(m.X - p.X);
                    if (off < 0.3f && along > 0.2f && along < reach) { reach = along; hit = m; }
                }
                foreach (Crate c in f.Crates)
                {
                    if (c.Carried || Math.Abs(c.Pos.Y - FY) > 0.1f) continue;
                    float along = dx != 0 ? (c.Pos.X - p.X) * dx : (c.Pos.Z - p.Z) * dz, off = dx != 0 ? Math.Abs(c.Pos.Z - p.Z) : Math.Abs(c.Pos.X - p.X);
                    if (off < 0.45f && along > 0.2f && along < reach) { reach = along - 0.4f; hit = null; blocked = true; }
                }
                if (dx < 0 && Math.Abs(Facility.ReceiverPos.Z - p.Z) < 0.3f && !blocked && hit == null) home = true;
                Vector3 q = p + new Vector3(dx, 0, dz) * reach;
                f.Beams.Add(new Beam { A = p, B = q, Colour = red, Width = 0.05f });
                if (home) return true;
                if (hit == null) return false;
                // kind 0 turns east to south and back; kind 1 turns east to north and back (z grows to the south)
                int ndx = hit.Kind == 0 ? dz : -dz, ndz = hit.Kind == 0 ? dx : -dx;
                dx = ndx; dz = ndz; p = q;
            }
            return false;
        }

        public override void Update(float dt)
        {
            t += dt;
            Vector3 p = player.Pos;
            UpdateDoors(dt);
            f.Beams.Clear(); f.Glows.Clear();

            // ---- interlock 1 and 4: plates ----
            for (int i = 0; i < 2; i++)
            {
                Plate pl = f.Plates[i];
                bool down = Flat(p, pl.X, pl.Z) < 0.9f && Math.Abs(p.Y - FY) < 1;
                foreach (Crate c in f.Crates) if (!c.Carried && Flat(c.Pos, pl.X, pl.Z) < 0.9f) down = true;
                if (down != pl.Down) { pl.Down = down; audio.Step(); }
                pl.Prop.World = Matrix.CreateTranslation(pl.X, FY - (down ? 0.05f : 0), pl.Z);
                pl.Prop.Colour = down ? new Vector3(0.2f, 0.85f, 0.35f) : new Vector3(0.85f, 0.25f, 0.2f);
            }
            f.Interlock[0].Want = f.Plates[0].Down ? 1 : 0;
            f.Interlock[3].Want = f.Plates[1].Down ? 1 : 0;
            if (f.Plates[0].Down && f.Interlock[0].Amt > 0.9f) Solve(0, "the door holds while the plate is down");
            if (f.Plates[1].Down && f.Interlock[3].Amt > 0.9f) Solve(3, "the way to the centrifuge hall is open");

            // ---- interlock 2: the beam ----
            BeamHome = TraceBeam();
            f.Interlock[1].Want = BeamHome ? 1 : 0;
            if (BeamHome) Solve(1, "the beam is home");
            f.Receiver.Colour = BeamHome ? new Vector3(0.2f, 0.95f, 0.4f) : new Vector3(0.25f, 0.27f, 0.3f);
            foreach (Mirror m in f.Mirrors)
                m.Prop.World = Matrix.CreateRotationY(m.Kind == 1 ? M.PI / 4 : -M.PI / 4) * Matrix.CreateTranslation(m.X, FY + 1.3f, m.Z);

            // ---- interlock 3: the targets ----
            int lit = 0;
            foreach (Target tg in f.Targets)
            {
                if (!Solved[2]) tg.LitT = Math.Max(0, tg.LitT - dt);
                if (tg.LitT > 0) lit++;
                tg.Prop.Colour = Solved[2] ? new Vector3(0.2f, 0.95f, 0.4f) : tg.LitT > 0 ? new Vector3(1.0f, 0.62f, 0.12f) * (tg.LitT < 2 && (int)(t * 6) % 2 == 0 ? 0.5f : 1) : new Vector3(0.22f, 0.24f, 0.27f);
            }
            if (lit == 3) Solve(2, "three lit at once");
            f.Interlock[2].Want = Solved[2] ? 1 : 0;

            // ---- interlock 4: the gates, and the gap ----
            gateT = Math.Max(0, gateT - dt);
            for (int i = 0; i < 2 && gateT <= 0; i++)
            {
                Gate g = f.Gates[i], o = f.Gates[1 - i];
                if (Math.Abs(p.Z - g.Z) < 1.1f && (p.X - g.X) * g.Nx < 1.0f && Math.Abs(p.Y - FY) < 1)
                {
                    player.Pos = new Vector3(o.X + o.Nx * 1.7f, FY, o.Z);
                    player.Yaw = o.Nx > 0 ? -M.HALF_PI : M.HALF_PI;          // facing out of the gate you arrive at
                    gateT = 1.2f; p = player.Pos;
                    audio.Hit(false);
                    pad.Rumble(high: 0.5f, ms: 90);
                }
            }
            foreach (Gate g in f.Gates)
            {
                float k = 0.75f + 0.25f * M.Sin(t * 3 + g.X);
                g.Prop.Colour = (g.Nx > 0 ? new Vector3(0.25f, 0.7f, 1.0f) : new Vector3(1.0f, 0.6f, 0.2f)) * k;
            }
            if (p.Z < -79 && p.Z > -111 && Math.Abs(p.X) < 9.5f)
            {
                if (p.Y < FY - 3)
                {
                    // fell into the gap: back to the side you were on
                    player.Pos = lastFar ? new Vector3(0, FY, -104) : new Vector3(0, FY, -84);
                    p = player.Pos;
                    hud.Hint("You fell into the gap. The gates are the way across", 3500);
                    pad.Rumble(low: 0.6f, ms: 200, decay: true);
                }
                else if (p.Y > FY - 0.5f) lastFar = p.Z < -95;
            }

            // ---- crates ----
            for (int i = 0; i < 2; i++)
            {
                Crate c = f.Crates[i];
                if (c.Carried)
                {
                    Vector3 hold = player.Cam.Pos + player.Cam.Forward * 1.15f - Vector3.Up * 0.62f;
                    c.Pos = hold;
                    c.Prop.World = Matrix.CreateRotationY(player.Yaw) * Matrix.CreateTranslation(hold);
                }
                else c.Prop.World = Matrix.CreateTranslation(c.Pos.X, c.Pos.Y + 0.4f, c.Pos.Z);
            }

            // ---- the centrifuge hall ----
            int guards = 0;
            foreach (Soldier s in garrison.Soldiers) if (s.Alive) guards++;
            bool inHall = p.Z < -111.5f && p.X < 22.5f;
            if (guards != Guards)
            {
                Guards = guards;
                if (guards == 0 && !toldHall && !Planted) { toldHall = true; hud.Feed("<b>HALL CLEAR</b> plant the device at the cascade controls", "head"); pad.Rumble(low: 0.5f, high: 0.5f, ms: 260, decay: true); }
            }
            bool atHub = M.Hypot(p.X - Facility.HubX, p.Z - Facility.HubZ) < Facility.HubR && Math.Abs(p.Y - FY) < 1;
            if (!Planted)
            {
                if (atHub && guards == 0)
                {
                    Progress = Math.Min(1, Progress + dt / PLANT);
                    tickT -= dt;
                    if (tickT <= 0) { tickT = 0.5f; pad.Rumble(high: 0.12f + Progress * 0.3f, ms: 45); }
                    if (Progress >= 1)
                    {
                        Planted = true; f.Device.Visible = true; f.HallExit.Want = 1;
                        audio.Loot(); audio.Horn();
                        hud.Feed("<b>DEVICE ARMED</b> get out of the mountain", "warn");
                        hud.Hint("The tunnel door is open. Up the service tunnel and out", 6000);
                        pad.Rumble(low: 0.9f, high: 0.9f, lt: 0.5f, rt: 0.5f, ms: 700, decay: true);
                    }
                }
                else Progress = Math.Max(0, Progress - dt / 10);
            }
            f.HubRing.Visible = !Planted;
            f.HubRing.Colour = guards == 0 ? new Vector3(1.0f, 0.78f, 0.25f) : new Vector3(0.9f, 0.2f, 0.15f);
            // the rotors glow while the cascade runs, and die with it
            spin = Done ? Math.Max(0, spin - dt * 0.6f) : 1;
            Vector3 rotor = new Vector3(0.25f, 0.8f, 1.0f) * (0.5f + 0.5f * spin) * spin + new Vector3(0.05f, 0.05f, 0.06f);
            foreach (Prop r in f.Rotors) r.Colour = rotor;

            // ---- the way out ----
            f.Release.Prop.Colour = BlastOpen ? new Vector3(0.2f, 0.95f, 0.4f) : Planted ? new Vector3(1.0f, 0.25f, 0.2f) * (0.6f + 0.4f * M.Sin(t * 6)) : new Vector3(0.3f, 0.1f, 0.1f);
            if (Planted && !BlastOpen && !toldBlast && p.X > 30 && Math.Abs(p.Z - Facility.TunnelZ) < 3.5f)
            {
                toldBlast = true;
                hud.Hint("Blast door. Shoot the red lock above it, or put a charge on it", 7000);
            }
            Outdoors = M.Smoothstep(82, 104, p.X);
            bool outside = p.X > Facility.DetonateX;
            f.ArrivalPad.Colour = new Vector3(0.25f, 0.7f, 1.0f) * (0.55f + 0.25f * M.Sin(t * 2));

            // lamps
            foreach (Vector3 l in f.Lamps) f.Glows.Add(new Glow { Pos = l, Size = 1.7f, Colour = new Color((byte)200, (byte)220, (byte)255, (byte)70) });
            foreach (Target tg in f.Targets) if (tg.LitT > 0 || Solved[2]) f.Glows.Add(new Glow { Pos = tg.Pos, Size = 1.3f, Colour = Solved[2] ? new Color((byte)80, (byte)255, (byte)130, (byte)150) : new Color((byte)255, (byte)170, (byte)60, (byte)170) });
            f.Glows.Add(new Glow { Pos = Facility.EmitterPos, Size = 0.7f, Colour = new Color((byte)255, (byte)70, (byte)50, (byte)200) });
            foreach (Gate g in f.Gates) f.Glows.Add(new Glow { Pos = new Vector3(g.X + g.Nx * 0.3f, FY + 1.6f, g.Z), Size = 2.6f, Colour = g.Nx > 0 ? new Color((byte)70, (byte)170, (byte)255, (byte)90) : new Color((byte)255, (byte)150, (byte)60, (byte)90) });

            // ---- what the use button would do now, and the prompt for it ----
            action = NONE;
            if (!Done)
            {
                if (Carrying >= 0) { action = DROP; hud.Prompt(Key, "Put the crate down"); }
                else
                {
                    float best = 2.0f;
                    for (int i = 0; i < 2; i++) { float d = Flat(p, f.Crates[i].Pos.X, f.Crates[i].Pos.Z); if (d < best && Math.Abs(p.Y - FY) < 1) { best = d; action = PICK; actionIndex = i; } }
                    for (int i = 0; i < 2 && action == NONE; i++) if (Flat(p, f.Mirrors[i].X, f.Mirrors[i].Z) < 2.2f) { action = MIRROR; actionIndex = i; }
                    if (action == NONE && Planted && outside) action = DETONATE;
                    if (action == NONE && NearDoor != null) action = DOOR;
                    if (action == PICK) hud.Prompt(Key, "Pick up the crate");
                    else if (action == MIRROR) hud.Prompt(Key, "Turn the mirror");
                    else if (action == DETONATE) hud.Prompt(Key, "Detonate");
                    else if (action == DOOR) hud.Prompt(Key, (NearDoor.Want > 0 ? "Close " : "Open ") + NearDoor.Name);
                    else hud.Prompt("", "");
                }
            }
            else hud.Prompt("", "");

            // ---- progress bar and objective ----
            hud.Capture(Planted ? 0 : Progress, Planted ? "" : atHub && guards > 0 ? "Contested: " + guards + " guard" + (guards == 1 ? "" : "s") + " in the hall"
                : Progress > 0.002f ? "Planting the device " + (int)Math.Round(Progress * 100) + "%" : "");
            int zone = p.Z > -23 ? 0 : p.Z > -47 ? 1 : p.Z > -79 ? 2 : p.Z > -111.5f ? 3 : 4;
            int stage; string text;
            if (Done) { stage = 9; text = "The centrifuges are destroyed"; }
            else if (Planted) { stage = outside ? 8 : 7; text = outside ? "Clear of the mountain: detonate" : "Get out of the mountain: up the service tunnel"; }
            else if (zone == 4 || inHall) { stage = guards > 0 ? 4 : atHub ? 6 : 5; text = guards > 0 ? "Clear the guards in the centrifuge hall: " + guards + " left" : atHub ? "Stay at the controls" : "Plant the device at the cascade controls"; }
            else
            {
                stage = zone;
                switch (zone)
                {
                    case 0: text = "Interlock 1 of 4: hold the door open with weight"; break;
                    case 1: text = "Interlock 2 of 4: bring the beam home to the receiver"; break;
                    case 2: text = "Interlock 3 of 4: light all three targets at once"; break;
                    default: text = "Interlock 4 of 4: get the crate across the gap"; break;
                }
            }
            Objective(text);
            Stage = stage;
        }
    }
}
