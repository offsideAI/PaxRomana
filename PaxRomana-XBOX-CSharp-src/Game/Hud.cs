// Heads-up display state. The rules write to it; HudRenderer draws it. Text may carry <b>…</b> and <em>…</em> runs.
using System;
using System.Collections.Generic;

namespace PaxRomana
{
    public sealed class FeedEntry { public string Text, Cls; public float Age; }

    public sealed class EndStats
    {
        public int Kills, Total, Shots, Hits, Heads, Alarms, Charges; public float Longest, Time;
    }

    public sealed class Hud
    {
        public string ObjLine = "Breach the Kremlin";
        public int Kills, Total; public float Alert; public bool Alarm;
        public bool CaptureShow; public float CaptureProgress; public string CaptureText = "";
        public readonly List<FeedEntry> FeedList = new List<FeedEntry>();
        public string PromptKey = "", PromptText = "";
        public string HintText = ""; public float HintT;
        public float MarkerT; public bool MarkerHead;
        public bool ScopeOn; public float Range, Breath = 1; public int Zero = 400, Zoom = 8; public string Wind = ""; public bool Exhausted;
        public bool ShowKeys, Crosshair = true, Cine, IsPad;
        public int Mag = 5, Reserve = 40, Charges = 8, Stance; public string Status = "";
        public bool MenuVisible = true, Resume, Ready, Failed; public float LoadP; public string LoadText = "Preparing…";
        public bool EndVisible, Win; public EndStats Stats;
        public float CineK;         // eased 0..1, for the letterbox bars

        public void Loading(float p, string text) { LoadP = p; LoadText = text; }
        public void SetReady() { Ready = true; LoadP = 1; }
        public void ToggleKeys() { ShowKeys = !ShowKeys; }
        public void ShowMenu(bool v, bool resume) { MenuVisible = v; if (resume) Resume = true; }
        public void Prompt(string key, string text) { PromptKey = key; PromptText = text; }
        public void Capture(float progress, string text)
        {
            CaptureShow = progress > 0.002f || text.Length > 0;
            CaptureProgress = progress;
            CaptureText = text.Length > 0 ? text : (progress >= 1 ? "Kremlin captured" : "Raising your standard " + (int)Math.Round(progress * 100) + "%");
        }
        public void Hitmarker(bool head) { MarkerT = 0.35f; MarkerHead = head; }
        public void Feed(string text, string cls = "")
        {
            FeedList.Insert(0, new FeedEntry { Text = text, Cls = cls });
            while (FeedList.Count > 5) FeedList.RemoveAt(FeedList.Count - 1);
        }
        public void Hint(string text, float ms = 5000) { HintText = text; HintT = ms / 1000f; }
        public void End(bool win, EndStats s) { EndVisible = true; Win = win; Stats = s; }

        public void Update(float dt)
        {
            for (int i = FeedList.Count - 1; i >= 0; i--) { FeedList[i].Age += dt; if (FeedList[i].Age > 5.2f) FeedList.RemoveAt(i); }
            HintT = Math.Max(0, HintT - dt);
            MarkerT = Math.Max(0, MarkerT - dt);
            CineK += ((Cine ? 1 : 0) - CineK) * Math.Min(1, dt * 8);
        }
    }

    // Capturing the Kremlin: doors (opened by hand or blown in), the count of defenders still inside the walls,
    // and the flag in the courtyard, which can only be raised once those defenders are gone.
    public sealed class Capture
    {
        const float HOLD = 8;         // seconds at the flagpole to raise your standard
        readonly Castle castle; readonly Player player; readonly Legion legion; readonly Hud hud; readonly IAudio audio; readonly Pad pad;
        public float Progress; public bool Done; public int Stage = -1, Defenders = -1; public Door NearDoor;
        float tickT; bool told; string text;

        public Capture(Castle castle, Player player, Legion legion, Hud hud, IAudio audio, Pad pad)
        {
            this.castle = castle; this.player = player; this.legion = legion; this.hud = hud; this.audio = audio; this.pad = pad;
        }

        public static bool InWalls(float x, float z) { return Math.Abs(x) < 48.5f && Math.Abs(z) < 41.5f; }

        string Whereabouts(Soldier s)
        {
            float y = s.Pos.Y - Config.PLATEAU;
            if (Math.Abs(s.Pos.X - castle.KeepX) < castle.KeepHalf && Math.Abs(s.Pos.Z - castle.KeepZ) < castle.KeepHalf) return y > 10 ? "on the Senate roof" : "inside the Senate";
            if (y > 14) return "on the gate tower";
            if (y > 8) return "on the wall-walk";
            return "in the courtyard";
        }

        // inside the curtain walls or the gate passage
        public bool Inside() { var p = player.Pos; return Math.Abs(p.X) < 43.5f && p.Z > -33.5f && p.Z < (Math.Abs(p.X) < 2.4f ? 39.4f : 33.4f); }

        // open or close the nearest door
        public bool UseDoor()
        {
            Door d = NearDoor;
            if (d == null || d.Broken) return false;
            d.Want = d.Want > 0 ? 0 : 1;
            audio.Door();
            return true;
        }

        // An explosion at p: doors within reach are blown off their hinges for good.
        public int Blast(Microsoft.Xna.Framework.Vector3 p)
        {
            int n = 0;
            foreach (Door d in castle.Doors)
            {
                if (d.Broken || M.Hypot(p.X - d.X, p.Z - d.Z) > 5.5f || Math.Abs(p.Y - Config.PLATEAU) > 9) continue;
                d.Broken = true; d.Amt = d.Want = 1; d.Solid.Off = true;
                hud.Feed("<b>BREACH</b> " + d.Name + " is down", "head");
                n++;
            }
            return n;
        }

        public void Update(float dt)
        {
            var p = player.Pos;
            string key = pad.Active ? "A" : "O";
            Door nd = null; float bd = 1e9f;
            foreach (Door d in castle.Doors)
            {
                if (d.Broken) continue;
                float step = dt / 0.65f;
                d.Amt += M.Clamp(d.Want - d.Amt, -step, step);
                d.Solid.Off = d.Amt > 0.5f;
                float e = d.Amt * d.Amt * (3 - 2 * d.Amt);
                foreach (DoorLeaf l in d.Leaves) l.Angle = l.Closed + (l.Open - l.Closed) * e;
                float dist = M.Hypot(p.X - d.X, p.Z - d.Z);
                if (dist < d.Reach && dist < bd) { bd = dist; nd = d; }
            }
            NearDoor = nd;

            // defenders still alive inside the walls
            int left = 0; List<string> spots = new List<string>();
            foreach (Soldier s in legion.Soldiers)
                if (s.Alive && InWalls(s.Pos.X, s.Pos.Z))
                {
                    left++;
                    if (left <= 3) { string w = Whereabouts(s); if (!spots.Contains(w)) spots.Add(w); }
                }
            if (left != Defenders)
            {
                Defenders = left;
                if (left > 0 && left <= 2 && !Done) hud.Hint((left == 1 ? "Last defender" : "Two defenders left") + ": " + string.Join(" and ", spots.ToArray()), 6000);
                if (left == 0 && !told) { told = true; hud.Feed("<b>WALLS CLEAR</b> raise your standard at the flagpole", "head"); pad.Rumble(low: 0.5f, high: 0.5f, ms: 260, decay: true); }
            }

            // the flag
            bool atFlag = M.Hypot(p.X - Castle.CapX, p.Z - Castle.CapZ) < Castle.CapR && Math.Abs(p.Y - Config.PLATEAU) < 3;
            if (!Done)
            {
                if (atFlag && left == 0)
                {
                    Progress = Math.Min(1, Progress + dt / HOLD);
                    tickT -= dt;
                    if (tickT <= 0) { tickT = 0.5f; pad.Rumble(high: 0.12f + Progress * 0.3f, ms: 45); }
                    if (Progress >= 1)
                    {
                        Done = true;
                        castle.CapFlag.Blue = true;
                        audio.Loot(); pad.Rumble(low: 0.9f, high: 0.9f, lt: 0.5f, rt: 0.5f, ms: 900, decay: true);
                    }
                }
                else Progress = Math.Max(0, Progress - dt / 14);
            }
            hud.Capture(Progress, atFlag && left > 0 && !Done ? "Contested: " + left + " defender" + (left == 1 ? "" : "s") + " inside the walls" : "");

            if (nd != null) hud.Prompt(key, (nd.Want > 0 ? "Close " : "Open ") + nd.Name); else hud.Prompt("", "");

            int stage = Done ? 4 : left > 0 ? (Inside() ? 1 : 0) : atFlag ? 3 : 2;
            string t;
            switch (stage)
            {
                case 0: t = "Breach the Kremlin: open the Spasskaya gate, or blow it in"; break;
                case 1: t = "Clear the defenders inside the walls: " + left + " left"; break;
                case 2: t = "Raise your standard at the flagpole in the courtyard"; break;
                case 3: t = "Hold the flagpole"; break;
                default: t = "The Kremlin is yours"; break;
            }
            if (t != text) { text = t; hud.ObjLine = t; }
            Stage = stage;
        }
    }
}
