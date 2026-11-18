// What a bullet can hit: the masonry and timber of the Kremlin and the camp (a bounding-volume hierarchy over their
// triangles) and the door leaves, which move.
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace PaxRomana
{
    public sealed class Colliders
    {
        Vector3[] ta, tb, tc; bool[] wood;
        Vector3[] nMin, nMax; int[] nA, nB;      // inner node: children nA, nB. leaf: nA = -(first + 1), nB = count
        int nodes;
        public List<Door> Doors = new List<Door>();
        public int Triangles { get { return ta.Length; } }

        public Colliders(Castle castle)
        {
            List<Vector3> a = new List<Vector3>(), b = new List<Vector3>(), c = new List<Vector3>(); List<bool> w = new List<bool>();
            foreach (string key in Castle.ColliderKeys)
            {
                Geo g;
                if (!castle.B.Parts.TryGetValue(key, out g)) continue;
                bool isWood = key == "timber" || key.StartsWith("tent");
                for (int i = 0; i + 2 < g.Count; i += 3) { a.Add(g.P[i]); b.Add(g.P[i + 1]); c.Add(g.P[i + 2]); w.Add(isWood); }
            }
            Doors = castle.Doors;
            int n = a.Count;
            int[] order = new int[n]; float[] keys = new float[n];
            Vector3[] cen = new Vector3[n];
            for (int i = 0; i < n; i++) { order[i] = i; cen[i] = (a[i] + b[i] + c[i]) / 3f; }
            nMin = new Vector3[n * 2 + 1]; nMax = new Vector3[n * 2 + 1]; nA = new int[n * 2 + 1]; nB = new int[n * 2 + 1];
            if (n > 0) BuildNode(order, keys, cen, a, b, c, 0, n);
            ta = new Vector3[n]; tb = new Vector3[n]; tc = new Vector3[n]; wood = new bool[n];
            for (int i = 0; i < n; i++) { int o = order[i]; ta[i] = a[o]; tb[i] = b[o]; tc[i] = c[o]; wood[i] = w[o]; }
        }

        int BuildNode(int[] order, float[] keys, Vector3[] cen, List<Vector3> a, List<Vector3> b, List<Vector3> c, int start, int count)
        {
            int id = nodes++;
            Vector3 mn = new Vector3(1e30f), mx = new Vector3(-1e30f), cmn = mn, cmx = mx;
            for (int i = start; i < start + count; i++)
            {
                int o = order[i];
                mn = Vector3.Min(mn, Vector3.Min(a[o], Vector3.Min(b[o], c[o])));
                mx = Vector3.Max(mx, Vector3.Max(a[o], Vector3.Max(b[o], c[o])));
                cmn = Vector3.Min(cmn, cen[o]); cmx = Vector3.Max(cmx, cen[o]);
            }
            nMin[id] = mn; nMax[id] = mx;
            Vector3 ext = cmx - cmn;
            if (count <= 4 || (ext.X < 1e-5f && ext.Y < 1e-5f && ext.Z < 1e-5f)) { nA[id] = -(start + 1); nB[id] = count; return id; }
            int axis = ext.X > ext.Y ? (ext.X > ext.Z ? 0 : 2) : (ext.Y > ext.Z ? 1 : 2);
            for (int i = start; i < start + count; i++) { Vector3 p = cen[order[i]]; keys[i] = axis == 0 ? p.X : axis == 1 ? p.Y : p.Z; }
            Array.Sort(keys, order, start, count);
            int half = count / 2;
            int l = BuildNode(order, keys, cen, a, b, c, start, half);
            int r = BuildNode(order, keys, cen, a, b, c, start + half, count - half);
            nA[id] = l; nB[id] = r;
            return id;
        }

        static bool Slab(ref Vector3 o, ref Vector3 inv, ref Vector3 mn, ref Vector3 mx, float tMax)
        {
            float t1 = (mn.X - o.X) * inv.X, t2 = (mx.X - o.X) * inv.X;
            float tmin = Math.Min(t1, t2), tmax = Math.Max(t1, t2);
            t1 = (mn.Y - o.Y) * inv.Y; t2 = (mx.Y - o.Y) * inv.Y;
            tmin = Math.Max(tmin, Math.Min(t1, t2)); tmax = Math.Min(tmax, Math.Max(t1, t2));
            t1 = (mn.Z - o.Z) * inv.Z; t2 = (mx.Z - o.Z) * inv.Z;
            tmin = Math.Max(tmin, Math.Min(t1, t2)); tmax = Math.Min(tmax, Math.Max(t1, t2));
            return tmax >= Math.Max(tmin, 0) && tmin <= tMax;
        }

        readonly int[] stack = new int[96];

        // Nearest hit along origin + dir * t for t in [0, maxDist]. dir must be normalised.
        public bool Raycast(Vector3 o, Vector3 d, float maxDist, out float dist, out Vector3 normal, out bool isWood)
        {
            dist = maxDist; normal = Vector3.Up; isWood = false;
            bool hit = false;
            if (nodes > 0)
            {
                Vector3 inv = new Vector3(1f / (Math.Abs(d.X) < 1e-9f ? 1e-9f : d.X), 1f / (Math.Abs(d.Y) < 1e-9f ? 1e-9f : d.Y), 1f / (Math.Abs(d.Z) < 1e-9f ? 1e-9f : d.Z));
                int sp = 0; stack[sp++] = 0;
                while (sp > 0)
                {
                    int id = stack[--sp];
                    if (!Slab(ref o, ref inv, ref nMin[id], ref nMax[id], dist)) continue;
                    if (nA[id] < 0)
                    {
                        int first = -nA[id] - 1, end = first + nB[id];
                        for (int i = first; i < end; i++)
                        {
                            Vector3 e1 = tb[i] - ta[i], e2 = tc[i] - ta[i];
                            Vector3 p = Vector3.Cross(d, e2);
                            float det = Vector3.Dot(e1, p);
                            if (det > -1e-9f && det < 1e-9f) continue;
                            float idet = 1f / det;
                            Vector3 tv = o - ta[i];
                            float u = Vector3.Dot(tv, p) * idet;
                            if (u < 0 || u > 1) continue;
                            Vector3 q = Vector3.Cross(tv, e1);
                            float v = Vector3.Dot(d, q) * idet;
                            if (v < 0 || u + v > 1) continue;
                            float t = Vector3.Dot(e2, q) * idet;
                            if (t < 0 || t >= dist) continue;
                            dist = t; hit = true; isWood = wood[i];
                            normal = Vector3.Normalize(Vector3.Cross(e1, e2));
                            if (Vector3.Dot(normal, d) > 0) normal = -normal;
                        }
                    }
                    else if (sp + 2 <= stack.Length) { stack[sp++] = nA[id]; stack[sp++] = nB[id]; }
                }
            }
            // door leaves: boxes swinging on their hinges
            for (int k = 0; k < Doors.Count; k++)
            {
                Door door = Doors[k];
                if (door.Broken) continue;
                for (int j = 0; j < door.Leaves.Count; j++)
                {
                    DoorLeaf l = door.Leaves[j];
                    Matrix back = Matrix.CreateRotationY(-l.Angle);
                    Vector3 lo = Vector3.TransformNormal(o - l.Pivot, back), ld = Vector3.TransformNormal(d, back);
                    float tmin = 0, tmax = dist; int axis = -1; float sign = 0;
                    if (!Axis(lo.X, ld.X, 0, l.W, 0, ref tmin, ref tmax, ref axis, ref sign)) continue;
                    if (!Axis(lo.Y, ld.Y, 0, l.H, 1, ref tmin, ref tmax, ref axis, ref sign)) continue;
                    if (!Axis(lo.Z, ld.Z, -0.07f, 0.07f, 2, ref tmin, ref tmax, ref axis, ref sign)) continue;
                    if (axis < 0 || tmin >= dist) continue;
                    dist = tmin; hit = true; isWood = !door.Iron;
                    Vector3 ln = axis == 0 ? new Vector3(sign, 0, 0) : axis == 1 ? new Vector3(0, sign, 0) : new Vector3(0, 0, sign);
                    normal = Vector3.TransformNormal(ln, Matrix.CreateRotationY(l.Angle));
                }
            }
            return hit;
        }

        static bool Axis(float o, float d, float lo, float hi, int which, ref float tmin, ref float tmax, ref int axis, ref float sign)
        {
            if (Math.Abs(d) < 1e-9f) return o >= lo && o <= hi;
            float t1 = (lo - o) / d, t2 = (hi - o) / d, s = -1;
            if (t1 > t2) { float t = t1; t1 = t2; t2 = t; s = 1; }
            if (t1 > tmin) { tmin = t1; axis = which; sign = s; }
            if (t2 < tmax) tmax = t2;
            return tmin <= tmax;
        }

        public bool Blocked(Vector3 from, Vector3 to, float shorten = 0)
        {
            Vector3 d = to - from; float len = d.Length();
            if (len < 1e-4f) return false;
            float dist; Vector3 n; bool w;
            return Raycast(from, d / len, Math.Max(0, len - shorten), out dist, out n, out w);
        }
    }
}
