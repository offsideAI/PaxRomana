// Heads-up display state. The rules write to it; HudRenderer draws it. Text may carry <b>…</b> and <em>…</em> runs.
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace PaxRomana
{
    public sealed class FeedEntry { public string Text, Cls; public float Age; }

    public sealed class EndStats
    {
        public int Kills, Total, Shots, Hits, Heads, Alarms, Charges; public float Longest, Time;
    }

    public sealed class Hud
    {
        public string ObjLine = "Breach the castle";
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
        public bool IntroOn; public float IntroT; public IntroScript Intro;      // the intro: which card to show comes from Intro.Cards
        public int Level; public bool LevelSelect;      // which level this is, and whether the menu offers the other one

        public void Loading(float p, string text) { LoadP = p; LoadText = text; }
        public void SetReady() { Ready = true; LoadP = 1; }
        public void ToggleKeys() { ShowKeys = !ShowKeys; }
        public void ShowMenu(bool v, bool resume) { MenuVisible = v; if (resume) Resume = true; }
        public void Prompt(string key, string text) { PromptKey = key; PromptText = text; }
        public void Capture(float progress, string text)
        {
            CaptureShow = progress > 0.002f || text.Length > 0;
            CaptureProgress = progress;
            CaptureText = text.Length > 0 ? text : (progress >= 1 ? "You have the keys" : "Cracking the strongbox " + (int)Math.Round(progress * 100) + "%");
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

    // A level's intro: it names the level and gives the briefing over a few camera moves. Level Zero's is narrated
    // by the voice-over in assets/voice, and each of its cards stays up for as long as its narration runs (the clip
    // lengths are in IntroVoice). Level One's cards run for fixed times. A line may carry <b> and <em> runs.
    public sealed class IntroCard
    {
        public float Start, End; public string Kicker, Title, Sub; public string[] Lines;
    }

    public sealed class IntroScript
    {
        const float LEAD = 0.8f, GAP = 0.6f, TAIL = 0.9f;
        public const float VoiceDelay = 0.3f;         // the narration starts this long after its card appears
        public readonly IntroCard[] Cards;
        public readonly float Length;                 // seconds
        public readonly float[] Cuts;                 // the camera shot behind card i runs from Cuts[i] to Cuts[i + 1]
        // One shot per card: where the camera starts and what it looks at, then where it ends and what it looks at.
        public readonly float[][] Shots;
        public readonly bool Voiced;

        IntroScript(IntroCard[] cards, float[] seconds, float[][] shots, bool voiced)
        {
            Cards = cards; Shots = shots; Voiced = voiced;
            Cuts = new float[cards.Length + 1];
            float t = LEAD;
            for (int i = 0; i < cards.Length; i++)
            {
                float speech = i < seconds.Length ? seconds[i] : 4;
                cards[i].Start = t; cards[i].End = t + VoiceDelay + speech + TAIL;
                Cuts[i] = i == 0 ? 0 : t - GAP / 2;
                t = cards[i].End + GAP;
            }
            Length = t; Cuts[cards.Length] = t;
        }

        public int IndexAt(float t) { for (int i = 0; i < Cards.Length; i++) if (t >= Cards[i].Start && t < Cards[i].End) return i; return -1; }
        public IntroCard At(float t) { int i = IndexAt(t); return i < 0 ? null : Cards[i]; }

        public static readonly IntroScript LevelZero = new IntroScript(new[]
        {
            new IntroCard { Kicker = "RESEARCH AND ANALYSIS WING · FIELD DOSSIER", Title = "LEVEL ZERO", Sub = "THE KEYS" },
            new IntroCard { Kicker = "BAVARIA · SUMMER 1944", Lines = new[] { "Beyond this valley the Reich is enriching <b>uranium-235</b>.", "The works are walled, wired and watched. No one walks in." } },
            new IntroCard { Kicker = "BURG ADLERSTEIN", Lines = new[] { "The <b>keys to the enrichment zone</b> lie in a strongbox", "in the vault of the castle keep, behind a garrison of twenty-nine." } },
            new IntroCard { Kicker = "YOUR ORDERS, AGENT", Lines = new[] { "Clear the castle and <b>take the keys</b>.", "<em>Level One: enter the zone, and break it from the inside.</em>" } },
        }, IntroVoice.Seconds, new[]
        {
            //      from x, y, z          looks at            to x, y, z            looks at
            new[] { 150f, 96, 470,        0, 44, 0,           110, 78, 300,         0, 46, 0 },                                             // from behind the ridge, the castle across the valley
            new[] { -60f, 120, 110,       Config.WORKS_X, 30, Config.WORKS_Z,       120, 105, -160,   Config.WORKS_X, 34, Config.WORKS_Z },   // over the castle towards the works
            new[] { 72f, 74, 84,          0, 50, -8,          -68, 70, 78,          0, 48, -8 },                                            // round the keep
            new[] { Config.SPAWN_X - 6, 100, Config.SPAWN_Z + 40,   0, 40, 0,       Config.SPAWN_X, 79, Config.SPAWN_Z + 2,   0, 40, 0 },    // down to the nest, between its trees
        }, true);

        const float FY = Facility.FY;
        public static readonly IntroScript LevelOne = new IntroScript(new[]
        {
            new IntroCard { Kicker = "RESEARCH AND ANALYSIS WING · FIELD DOSSIER", Title = "LEVEL ONE", Sub = "BLITZSCHEISS MOUNTAIN" },
            new IntroCard { Kicker = "UNDER THE MOUNTAIN", Lines = new[] { "The keys open the service lock. <b>Four interlocks</b> stand", "between you and the centrifuge hall." } },
            new IntroCard { Kicker = "YOUR ORDERS, AGENT", Lines = new[] { "Plant the device at the cascade controls and get out.", "<b>Detonate from outside the mountain.</b>" } },
        }, new[] { 3.6f, 5.6f, 5.6f }, new[]
        {
            new[] { 0f, FY + 11, -116,      0, FY + 3, -168,    0, FY + 8.5f, -136,    0, FY + 3, -168 },                         // down the centrifuge hall
            new[] { 7f, FY + 4.6f, -25.5f,  0, FY + 1.3f, -38,  -6.5f, FY + 4.6f, -26, 0, FY + 1.3f, -38 },                      // the beam room
            new[] { 4.5f, FY + 3.4f, 11,    0, FY + 1.5f, -1,   0, FY + 1.75f, 8.6f,   0, FY + 1.55f, -1 },                       // the service lock, down to where you stand
        }, false);
    }

    // Taking the keys: doors (opened by hand or blown in), the count of defenders still inside the walls, and the
    // strongbox in the vault of the keep, which can only be cracked once those defenders are gone.
    public sealed class Capture : Mission
    {
        const float HOLD = 8;         // seconds at the strongbox to crack it
        readonly Castle castle;
        public int Defenders = -1;
        float tickT; bool told;

        public Capture(Castle castle, Player player, Garrison garrison, Hud hud, IAudio audio, Pad pad) : base(castle, player, garrison, hud, audio, pad)
        {
            this.castle = castle;
        }

        public override void TitleCamera(float t, out Vector3 pos, out Vector3 look)
        {
            float a = t * 0.03f;
            pos = new Vector3(Config.SPAWN_X + M.Sin(a) * 40, Terrain.GroundAt(Config.SPAWN_X, Config.SPAWN_Z) + 9, Config.SPAWN_Z + 14);
            look = new Vector3(0, 44, 0);
        }

        public static bool InWalls(float x, float z) { return Math.Abs(x) < 48.5f && Math.Abs(z) < 41.5f; }

        string Whereabouts(Soldier s)
        {
            float y = s.Pos.Y - Config.PLATEAU;
            if (Math.Abs(s.Pos.X - castle.KeepX) < castle.KeepHalf && Math.Abs(s.Pos.Z - castle.KeepZ) < castle.KeepHalf) return y > 10 ? "on the keep roof" : "inside the keep";
            if (y > 14) return "on the gate tower";
            if (y > 8) return "on the wall-walk";
            return "in the courtyard";
        }

        // inside the curtain walls or the gate passage
        public bool Inside() { var p = player.Pos; return Math.Abs(p.X) < 43.5f && p.Z > -33.5f && p.Z < (Math.Abs(p.X) < 2.4f ? 39.4f : 33.4f); }

        public override void Update(float dt)
        {
            var p = player.Pos;
            UpdateDoors(dt);
            Door nd = NearDoor;

            // defenders still alive inside the walls
            int left = 0; List<string> spots = new List<string>();
            foreach (Soldier s in garrison.Soldiers)
                if (s.Alive && InWalls(s.Pos.X, s.Pos.Z))
                {
                    left++;
                    if (left <= 3) { string w = Whereabouts(s); if (!spots.Contains(w)) spots.Add(w); }
                }
            if (left != Defenders)
            {
                Defenders = left;
                if (left > 0 && left <= 2 && !Done) hud.Hint((left == 1 ? "Last defender" : "Two defenders left") + ": " + string.Join(" and ", spots.ToArray()), 6000);
                if (left == 0 && !told) { told = true; hud.Feed("<b>WALLS CLEAR</b> take the keys from the vault in the keep", "head"); pad.Rumble(low: 0.5f, high: 0.5f, ms: 260, decay: true); }
            }

            // the strongbox
            bool atBox = M.Hypot(p.X - Castle.CapX, p.Z - Castle.CapZ) < Castle.CapR && Math.Abs(p.Y - Config.PLATEAU) < 3;
            if (!Done)
            {
                if (atBox && left == 0)
                {
                    Progress = Math.Min(1, Progress + dt / HOLD);
                    tickT -= dt;
                    if (tickT <= 0) { tickT = 0.5f; pad.Rumble(high: 0.12f + Progress * 0.3f, ms: 45); }
                    if (Progress >= 1)
                    {
                        Done = true;
                        audio.Loot(); pad.Rumble(low: 0.9f, high: 0.9f, lt: 0.5f, rt: 0.5f, ms: 900, decay: true);
                    }
                }
                else Progress = Math.Max(0, Progress - dt / 14);
            }
            hud.Capture(Progress, atBox && left > 0 && !Done ? "Contested: " + left + " defender" + (left == 1 ? "" : "s") + " inside the walls" : "");

            if (nd != null) hud.Prompt(Key, (nd.Want > 0 ? "Close " : "Open ") + nd.Name); else hud.Prompt("", "");

            int stage = Done ? 4 : left > 0 ? (Inside() ? 1 : 0) : atBox ? 3 : 2;
            string t;
            switch (stage)
            {
                case 0: t = "Breach the castle: open the gate, or blow it in"; break;
                case 1: t = "Clear the defenders inside the walls: " + left + " left"; break;
                case 2: t = "Take the keys from the vault in the keep"; break;
                case 3: t = "Stay at the strongbox"; break;
                default: t = "You have the keys to the enrichment zone"; break;
            }
            Objective(t);
            Stage = stage;
        }
    }
}
