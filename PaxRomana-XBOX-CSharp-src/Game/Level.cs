// What every level has in common. A level is a place (a Site: its geometry, footprints, doors and moving props),
// the ground under it (an Arena), its light (a LightRig) and its rules (a Mission). Level Zero is the castle
// (Castle.cs, Capture in Hud.cs); Level One is the facility under the mountain (LevelOne.cs).
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace PaxRomana
{
    // Something that moves or changes while the level runs: a crate, a pressure plate, a mirror, a lamp. The renderer
    // uploads Shape once and draws it each frame with the current World, Colour and Visible.
    public sealed class Prop
    {
        public Geo Shape; public Matrix World = Matrix.Identity; public Vector3 Colour = Vector3.One;
        public bool Lit = true, Visible = true, TwoSided;
        public float Spec;
    }

    // A straight beam of light, drawn additively.
    public struct Beam { public Vector3 A, B; public Vector3 Colour; public float Width; }

    // A glow drawn as a soft sprite: a lamp, a status light.
    public struct Glow { public Vector3 Pos; public float Size; public Color Colour; }

    public abstract class Site
    {
        public readonly Builder B = new Builder();
        public readonly List<Solid> Solids = new List<Solid>();
        public readonly List<Door> Doors = new List<Door>();
        public readonly List<Banner> Banners = new List<Banner>();
        public readonly List<Vector3> Smoke = new List<Vector3>(), Torches = new List<Vector3>();
        public readonly List<Prop> Props = new List<Prop>();
        public readonly List<Beam> Beams = new List<Beam>();           // rebuilt by the mission every frame
        public readonly List<Glow> Glows = new List<Glow>();           // rebuilt by the mission every frame

        // Each leaf swings on a hinge; the door's solid blocks the doorway until the door is open.
        // leaves: pivot x, pivot z, width, closed angle, open angle. floor is the height the door stands on.
        protected Door AddDoor(string name, bool iron, float h, float x0, float x1, float z0, float z1, float[][] leaves, float floor)
        {
            Door d = new Door { Name = name, Iron = iron, Solid = Solid.Box(x0, x1, z0, z1), X = (x0 + x1) / 2, Z = (z0 + z1) / 2, Y = floor, Reach = 2.6f + Math.Max(x1 - x0, z1 - z0) / 2 };
            foreach (float[] l in leaves) d.Leaves.Add(new DoorLeaf { Pivot = new Vector3(l[0], floor, l[1]), W = l[2], H = h, Closed = l[3], Open = l[4], Angle = l[3] });
            Solids.Add(d.Solid); Doors.Add(d);
            return d;
        }

        // Puts the doors back to how a mission starts.
        public virtual void Reset()
        {
            foreach (Door d in Doors)
            {
                d.Amt = d.Want = 0; d.Broken = false; d.Solid.Off = false;
                foreach (DoorLeaf l in d.Leaves) l.Angle = l.Closed;
            }
        }
    }

    // The ground the player walks on and bullets land in.
    public sealed class Arena
    {
        public Func<float, float, float> Ground;       // height of the ground under (x, z)
        public float X0, X1, Z0, Z1;                    // the playable rectangle
        public bool Water;                              // ground below y = 0 is under water (the river)
        public bool Bridge;                             // the river bridge of Level Zero
        public Vector3 Spawn; public float SpawnYaw;
        public string EdgeHint = "";
    }

    public sealed class LightRig
    {
        public Vector3 Sun, SunDir, Sky, Bounce, BounceFrom, Ambient, Fog; public float FogStart, FogEnd;

        public static LightRig Outdoor()
        {
            return new LightRig
            {
                Sun = Lighting.Sun, SunDir = Config.SUN_DIR, Sky = Lighting.Sky, Bounce = Lighting.Bounce, BounceFrom = Lighting.BounceFrom,
                Ambient = Lighting.Ambient, Fog = Lighting.Fog, FogStart = Lighting.FogStart, FogEnd = Lighting.FogEnd,
            };
        }

        public static LightRig Lerp(LightRig a, LightRig b, float k)
        {
            if (k <= 0) return a; if (k >= 1) return b;
            return new LightRig
            {
                Sun = Vector3.Lerp(a.Sun, b.Sun, k), SunDir = Vector3.Normalize(Vector3.Lerp(a.SunDir, b.SunDir, k)), Sky = Vector3.Lerp(a.Sky, b.Sky, k),
                Bounce = Vector3.Lerp(a.Bounce, b.Bounce, k), BounceFrom = Vector3.Normalize(Vector3.Lerp(a.BounceFrom, b.BounceFrom, k)),
                Ambient = Vector3.Lerp(a.Ambient, b.Ambient, k), Fog = Vector3.Lerp(a.Fog, b.Fog, k),
                FogStart = M.Lerp(a.FogStart, b.FogStart, k), FogEnd = M.Lerp(a.FogEnd, b.FogEnd, k),
            };
        }
    }

    // The rules of one level: what the objective is, what the use button does, what a bullet or a blast changes.
    // The doors are common to every level, so they are handled here.
    public abstract class Mission
    {
        protected readonly Site site; protected readonly Player player; protected readonly Garrison garrison; protected readonly Hud hud; protected readonly IAudio audio; protected readonly Pad pad;
        public float Progress; public bool Done; public int Stage = -1;
        public Door NearDoor;
        public float EndDelay = 3;          // seconds between Done and the end screen
        public float Outdoors = 1;          // 0 deep inside, 1 under the sky: blends the light and the haze
        string objective;

        protected Mission(Site site, Player player, Garrison garrison, Hud hud, IAudio audio, Pad pad)
        {
            this.site = site; this.player = player; this.garrison = garrison; this.hud = hud; this.audio = audio; this.pad = pad;
        }

        public abstract void Update(float dt);
        // The use button (A, or O on the keyboard). Returns true if it did something.
        public virtual bool Use() { return UseDoor(); }
        // An explosion at p.
        public virtual void Blast(Vector3 p) { BlastDoors(p); }
        // A round landed on masonry, timber or the ground at p.
        public virtual void BulletHit(Vector3 p) { }
        // Where the camera sits behind the title menu.
        public abstract void TitleCamera(float t, out Vector3 pos, out Vector3 look);

        protected void Objective(string text) { if (text != objective) { objective = text; hud.ObjLine = text; } }
        protected string Key { get { return pad.Active ? "A" : "O"; } }

        // Swings the doors towards open or closed and finds the one the player could operate by hand.
        protected void UpdateDoors(float dt)
        {
            Vector3 p = player.Pos;
            Door nd = null; float bd = 1e9f;
            foreach (Door d in site.Doors)
            {
                if (d.Broken) continue;
                float step = dt / 0.65f;
                d.Amt += M.Clamp(d.Want - d.Amt, -step, step);
                d.Solid.Off = d.Amt > 0.5f;
                float e = d.Amt * d.Amt * (3 - 2 * d.Amt);
                foreach (DoorLeaf l in d.Leaves) l.Angle = l.Closed + (l.Open - l.Closed) * e;
                if (d.Auto || d.Sealed || Math.Abs(p.Y - d.Y) > 4) continue;
                float dist = M.Hypot(p.X - d.X, p.Z - d.Z);
                if (dist < d.Reach && dist < bd) { bd = dist; nd = d; }
            }
            NearDoor = nd;
        }

        // open or close the nearest door
        public bool UseDoor()
        {
            Door d = NearDoor;
            if (d == null || d.Broken) return false;
            d.Want = d.Want > 0 ? 0 : 1;
            audio.Door();
            return true;
        }

        // An explosion at p: doors within reach are blown off their hinges for good. Doors a machine holds shut are
        // too heavy for that.
        protected int BlastDoors(Vector3 p)
        {
            int n = 0;
            foreach (Door d in site.Doors)
            {
                if (d.Broken || d.Auto || M.Hypot(p.X - d.X, p.Z - d.Z) > 5.5f || Math.Abs(p.Y - d.Y) > 9) continue;
                d.Broken = true; d.Amt = d.Want = 1; d.Solid.Off = true;
                hud.Feed("<b>BREACH</b> " + d.Name + " is down", "head");
                n++;
            }
            return n;
        }
    }
}
