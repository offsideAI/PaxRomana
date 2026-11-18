// Sound. The effects are fully synthesised: rifle report with valley echo, bolt, impacts, siren, explosions, wind.
// Each is computed into PCM at load, then played through MonoGame's SoundEffect. The one thing that comes from
// files is the recorded voice-over for the intro (assets/voice/*.wav, made by tools/makevoice.py).
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Audio;

namespace PaxRomana
{
    // Offline synthesiser: filtered noise bursts and swept tones mixed into a buffer, with a reverb send.
    public sealed class Synth
    {
        public const int SR = 32000;
        const int LP = 0, HP = 1, BP = 2;
        const int SINE = 0, SQUARE = 1, TRI = 2, SAW = 3;
        static float[] white, brown;
        readonly float[] dry, wet;
        readonly Random rng;

        Synth(float seconds, int seed) { dry = new float[(int)(seconds * SR)]; wet = new float[dry.Length]; rng = new Random(seed); }

        static void Sources()
        {
            if (white != null) return;
            Random r = new Random(12345);
            int len = SR * 2; float[] w = new float[len], b = new float[len]; float last = 0;
            for (int i = 0; i < len; i++) { w[i] = (float)(r.NextDouble() * 2 - 1); last = (last + 0.02f * w[i]) / 1.02f; b[i] = last * 3.5f; }
            brown = b; white = w;
        }

        // gain envelope of a burst: quick exponential attack, exponential decay to silence at dur
        static float Env(float tau, float dur, float gain, float attack)
        {
            if (tau < 0 || tau >= dur) return 0;
            if (tau < attack) return 0.0001f * (float)Math.Pow(gain / 0.0001f, tau / attack);
            return gain * (float)Math.Pow(0.0001f / gain, (tau - attack) / (dur - attack));
        }

        void Noise(float[] src, float t, float dur, int type, float f0, float f1, float gain, float q = 0.8f, float attack = 0.002f, float send = 0, bool flat = false)
        {
            int start = (int)(t * SR), n = (int)(dur * SR), off = rng.Next(src.Length);
            float x1 = 0, x2 = 0, y1 = 0, y2 = 0, b0 = 0, b1 = 0, b2 = 0, a1 = 0, a2 = 0;
            f1 = Math.Max(20, f1); q = Math.Max(0.5f, q);
            for (int i = 0; i < n && start + i < dry.Length; i++)
            {
                if ((i & 15) == 0)
                {
                    float f = f0 * (float)Math.Pow(f1 / f0, (float)i / n);
                    double w0 = 2 * Math.PI * Math.Min(f, SR * 0.45f) / SR, cs = Math.Cos(w0), al = Math.Sin(w0) / (2 * q), a0 = 1 + al;
                    if (type == LP) { b0 = (float)((1 - cs) / 2 / a0); b1 = (float)((1 - cs) / a0); b2 = b0; }
                    else if (type == HP) { b0 = (float)((1 + cs) / 2 / a0); b1 = (float)(-(1 + cs) / a0); b2 = b0; }
                    else { b0 = (float)(al / a0); b1 = 0; b2 = -b0; }
                    a1 = (float)(-2 * cs / a0); a2 = (float)((1 - al) / a0);
                }
                float x = src[(off + i) % src.Length];
                float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                float s = y * (flat ? gain : Env((float)i / SR, dur, gain, attack));
                dry[start + i] += s; wet[start + i] += s * send;
            }
        }

        void Tone(float t, float dur, int wave, float f0, float f1, float gain, float send = 0, float attack = 0.003f)
        {
            int start = (int)(t * SR), n = (int)(dur * SR); double ph = 0;
            f1 = Math.Max(20, f1);
            for (int i = 0; i < n && start + i < dry.Length; i++)
            {
                float f = f0 * (float)Math.Pow(f1 / f0, (float)i / n);
                ph += f / SR; ph -= Math.Floor(ph);
                float v = wave == SINE ? (float)Math.Sin(ph * 2 * Math.PI) : wave == SQUARE ? (ph < 0.5 ? 1 : -1) : wave == TRI ? (float)(4 * Math.Abs(ph - 0.5) - 1) : (float)(2 * ph - 1);
                float s = v * Env((float)i / SR, dur, gain, attack);
                dry[start + i] += s; wet[start + i] += s * send;
            }
        }

        // one sustained horn note: sawtooth harmonics through a low-pass, linear swell and release
        void Horn(float t, float freq, float len)
        {
            foreach (float k in new[] { 1f, 2.005f, 3.01f })
            {
                int start = (int)(t * SR), n = (int)(len * SR); double ph = 0; float lp = 0, peak = 0.09f / k;
                float a = 1 - (float)Math.Exp(-2 * Math.PI * 820 / SR);
                for (int i = 0; i < n && start + i < dry.Length; i++)
                {
                    ph += freq * k / SR; ph -= Math.Floor(ph);
                    lp += ((float)(2 * ph - 1) - lp) * a;
                    float tau = (float)i / SR, g = tau < 0.25f ? peak * tau / 0.25f : tau > len - 0.2f ? peak * (len - tau) / 0.2f : peak;
                    dry[start + i] += lp * g * 2; wet[start + i] += lp * g * 2;
                }
            }
        }

        // master lifts the quiet mechanical sounds so they carry on a television; tanh keeps loud ones from clipping.
        // Schroeder reverb on the send bus (four damped combs, two all-passes), mixed back in: the valley's echo.
        short[] Finish(float master = 0.8f, float reverb = 0.9f)
        {
            float[] outp = new float[dry.Length];
            bool any = false;
            for (int i = 0; i < wet.Length; i++) if (wet[i] != 0) { any = true; break; }
            if (any)
            {
                float[] acc = new float[wet.Length];
                foreach (float ms in new[] { 29.7f, 37.1f, 41.1f, 43.7f })
                {
                    int d = (int)(ms * SR / 1000); float g = (float)Math.Pow(10, -3.0 * (ms / 1000) / 2.4), lp = 0;
                    float[] buf = new float[d]; int p = 0;
                    for (int i = 0; i < wet.Length; i++)
                    {
                        float y = buf[p];
                        lp += (y - lp) * 0.45f;
                        buf[p] = wet[i] + lp * g; p = (p + 1) % d;
                        acc[i] += y * 0.25f;
                    }
                }
                foreach (float ms in new[] { 5.0f, 1.7f })
                {
                    int d = (int)(ms * SR / 1000); float[] buf = new float[d]; int p = 0;
                    for (int i = 0; i < acc.Length; i++)
                    {
                        float bo = buf[p], x = acc[i], y = -0.7f * x + bo;
                        buf[p] = x + 0.7f * y; p = (p + 1) % d;
                        acc[i] = y;
                    }
                }
                for (int i = 0; i < outp.Length; i++) outp[i] = acc[i] * reverb;
            }
            short[] pcm = new short[dry.Length];
            for (int i = 0; i < pcm.Length; i++) pcm[i] = (short)(Math.Tanh((dry[i] + outp[i]) * master) * 32000);
            // a short fade at the very end, so a clipped tail never clicks
            int fade = Math.Min(pcm.Length, SR / 50);
            for (int i = 0; i < fade; i++) pcm[pcm.Length - 1 - i] = (short)(pcm[pcm.Length - 1 - i] * (float)i / fade);
            return pcm;
        }

        // Renders every clip the game uses. Pure computation: safe on a background thread.
        public static Dictionary<string, short[]> RenderAll()
        {
            Sources();
            Dictionary<string, short[]> c = new Dictionary<string, short[]>();
            Synth s;

            s = new Synth(6.0f, 1);
            s.Noise(white, 0, 0.16f, HP, 1800, 900, 1.0f, 0.8f, 0.002f, 0.5f);
            s.Noise(white, 0, 0.55f, LP, 2600, 120, 1.3f, 0.8f, 0.002f, 0.8f);
            s.Tone(0, 0.3f, SINE, 140, 38, 1.2f, 0.3f);
            s.Noise(brown, 1.15f, 0.9f, LP, 900, 150, 0.5f, 0.8f, 0.08f, 0.6f);        // slap-back from the castle walls
            s.Noise(brown, 2.3f, 1.4f, LP, 500, 100, 0.28f, 0.8f, 0.2f, 0.6f);         // then the far hills
            c["shot"] = s.Finish();

            s = new Synth(0.6f, 2);
            foreach (float[] k in new[] { new[] { 0f, 3200, 0.3f }, new[] { 0.13f, 1900, 0.4f }, new[] { 0.36f, 2300, 0.4f }, new[] { 0.5f, 3600, 0.32f } })
            { s.Noise(white, k[0], 0.05f, BP, k[1], k[1] * 0.7f, k[2], 3); s.Tone(k[0], 0.04f, SQUARE, k[1] * 0.5f, k[1] * 0.3f, 0.05f); }
            c["bolt"] = s.Finish(1.8f);

            s = new Synth(2.4f, 3);
            foreach (float[] k in new[] { new[] { 0.2f, 1500 }, new[] { 0.9f, 900 }, new[] { 1.5f, 2100 }, new[] { 2.0f, 2800 }, new[] { 2.15f, 2000 } }) s.Noise(white, k[0], 0.07f, BP, k[1], k[1] * 0.6f, 0.4f, 2.5f);
            c["reload"] = s.Finish(1.8f);

            s = new Synth(0.06f, 4); s.Noise(white, 0, 0.03f, BP, 2500, 2000, 0.25f, 4); c["dry"] = s.Finish(2.2f);

            s = new Synth(2.0f, 5); s.Tone(0, 0.16f, SINE, 120, 50, 0.7f, 0.4f); s.Noise(brown, 0, 0.12f, LP, 700, 200, 0.6f, 0.8f, 0.002f, 0.4f); c["flesh"] = s.Finish();
            s = new Synth(2.4f, 6); s.Noise(white, 0, 0.09f, BP, 3400, 1500, 0.5f, 2, 0.002f, 0.8f); s.Tone(0.02f, 0.5f, SINE, 3000, 900, 0.1f, 0.9f); c["stone"] = s.Finish(1.4f);
            s = new Synth(2.0f, 7); s.Noise(brown, 0, 0.2f, LP, 600, 120, 0.6f, 0.8f, 0.002f, 0.5f); c["dirt"] = s.Finish(1.4f);

            s = new Synth(0.1f, 8); s.Tone(0, 0.07f, TRI, 1300, 1000, 0.18f); c["hit"] = s.Finish(1.6f);
            s = new Synth(0.2f, 9); s.Tone(0, 0.07f, TRI, 1900, 1500, 0.18f); s.Tone(0.06f, 0.1f, TRI, 2500, 2300, 0.14f); c["hithead"] = s.Finish(1.6f);

            s = new Synth(9.0f, 10);
            s.Horn(1.2f, 147, 1.8f); s.Horn(3.2f, 147, 0.7f); s.Horn(4.0f, 196, 2.4f);
            c["horn"] = s.Finish();

            s = new Synth(0.35f, 11); s.Tone(0, 0.12f, SINE, 62, 40, 0.5f); s.Tone(0.17f, 0.12f, SINE, 54, 36, 0.35f); c["heartbeat"] = s.Finish();

            s = new Synth(2.2f, 12); s.Tone(0, 0.26f, SINE, 190, 48, 0.9f, 0.3f); s.Noise(white, 0, 0.24f, BP, 1000, 220, 0.5f, 0.8f, 0.002f, 0.4f); c["launch"] = s.Finish();

            s = new Synth(4.6f, 13);
            s.Tone(0, 0.9f, SINE, 92, 24, 1.4f, 0.6f);
            s.Noise(brown, 0, 1.7f, LP, 900, 60, 1.3f, 0.8f, 0.004f, 0.8f);
            s.Noise(white, 0, 0.35f, BP, 2200, 300, 0.5f, 0.8f, 0.002f, 0.5f);
            c["explosion"] = s.Finish();

            s = new Synth(0.75f, 14); s.Noise(brown, 0, 0.55f, BP, 260, 520, 0.3f, 6, 0.05f); s.Noise(brown, 0.5f, 0.16f, LP, 300, 80, 0.5f); c["door"] = s.Finish(2.2f);
            s = new Synth(0.6f, 15); for (int i = 0; i < 3; i++) { float f = new[] { 1320f, 1760, 2640 }[i]; s.Tone(i * 0.07f, 0.35f, SINE, f, f, 0.12f); } c["loot"] = s.Finish(1.8f);
            for (int i = 0; i < 3; i++) { s = new Synth(0.14f, 16 + i); s.Noise(brown, 0, 0.11f, BP, 500 + i * 130, 250, 0.22f, 0.7f); c["step" + i] = s.Finish(3.0f); }

            // wind in the frozen grass: eight seconds, cross-faded so it loops without a seam
            {
                const float len = 8, over = 1;
                s = new Synth(len + over, 20);
                s.Noise(brown, 0, len + over, BP, 420, 420, 1, 0.5f, 0.002f, 0, true);
                int n = (int)(len * SR), o = (int)(over * SR); short[] loop = new short[n];
                for (int i = 0; i < n; i++)
                {
                    float gust = 0.22f + 0.1f * (float)Math.Sin(2 * Math.PI * i / n) + 0.05f * (float)Math.Sin(2 * Math.PI * i / n * 3 + 1.3);
                    float v = s.dry[i];
                    if (i < o) { float k = (float)i / o; v = v * k + s.dry[n + i] * (1 - k); }
                    loop[i] = (short)Math.Max(-32000, Math.Min(32000, v * gust * 32000));
                }
                c["wind"] = loop;
            }
            return c;
        }
    }

    // A minimal reader for the voice-over files: uncompressed 16-bit PCM in a RIFF/WAVE container.
    public sealed class Wav
    {
        public int Rate, Channels; public byte[] Pcm;
        public float Seconds { get { return Pcm.Length / 2f / Channels / Rate; } }

        // Returns null if the bytes are not a 16-bit PCM WAVE file.
        public static Wav Parse(byte[] b)
        {
            if (b == null || b.Length < 44 || b[0] != 'R' || b[1] != 'I' || b[2] != 'F' || b[3] != 'F' || b[8] != 'W' || b[9] != 'A' || b[10] != 'V' || b[11] != 'E') return null;
            Wav w = new Wav(); bool fmt = false;
            int p = 12;
            while (p + 8 <= b.Length)
            {
                string id = "" + (char)b[p] + (char)b[p + 1] + (char)b[p + 2] + (char)b[p + 3];
                int size = BitConverter.ToInt32(b, p + 4); p += 8;
                if (size < 0 || p + size > b.Length) size = b.Length - p;
                if (id == "fmt " && size >= 16)
                {
                    if (BitConverter.ToInt16(b, p) != 1 || BitConverter.ToInt16(b, p + 14) != 16) return null;       // PCM, 16 bits
                    w.Channels = BitConverter.ToInt16(b, p + 2); w.Rate = BitConverter.ToInt32(b, p + 4); fmt = true;
                }
                else if (id == "data")
                {
                    if (!fmt || w.Channels < 1 || w.Channels > 2 || w.Rate < 8000) return null;
                    w.Pcm = new byte[size - size % (2 * w.Channels)];
                    Buffer.BlockCopy(b, p, w.Pcm, 0, w.Pcm.Length);
                    return w;
                }
                p += size + (size & 1);
            }
            return null;
        }
    }

    public sealed class SynthAudio : IAudio
    {
        SoundEffect[] voice; SoundEffectInstance speaking;

        readonly Dictionary<string, SoundEffect> clips = new Dictionary<string, SoundEffect>();
        struct Pending { public string Name; public float Delay, Vol; }
        readonly List<Pending> queue = new List<Pending>();
        SoundEffectInstance wind;
        public float Master = 1;

        // Call on the main thread. Throws if there is no audio device.
        public SynthAudio(Dictionary<string, short[]> pcm)
        {
            foreach (KeyValuePair<string, short[]> kv in pcm)
            {
                byte[] b = new byte[kv.Value.Length * 2];
                Buffer.BlockCopy(kv.Value, 0, b, 0, b.Length);
                clips[kv.Key] = new SoundEffect(b, Synth.SR, AudioChannels.Mono);
            }
            wind = clips["wind"].CreateInstance();
            wind.IsLooped = true; wind.Volume = 0.5f; wind.Play();
        }

        void Play(string name, float vol = 1, float delay = 0)
        {
            if (delay > 0.01f) { queue.Add(new Pending { Name = name, Delay = delay, Vol = vol }); return; }
            SoundEffect fx;
            if (clips.TryGetValue(name, out fx)) { try { fx.Play(M.Clamp(vol * Master), 0, 0); } catch (Exception) { /* too many voices: drop this one */ } }
        }

        public void Update(float dt)
        {
            for (int i = queue.Count - 1; i >= 0; i--)
            {
                Pending p = queue[i]; p.Delay -= dt;
                if (p.Delay <= 0) { queue.RemoveAt(i); Play(p.Name, p.Vol); } else queue[i] = p;
            }
        }

        public void Shot() { Play("shot"); }
        public void Bolt(float delay) { Play("bolt", 1, delay); }
        public void Reload() { Play("reload"); }
        public void Dry() { Play("dry"); }
        public void Impact(string kind, float delay, float dist) { Play(kind == "flesh" || kind == "stone" ? kind : "dirt", Math.Max(0.12f, 1 - dist / 700), delay); }
        public void Hit(bool head) { Play(head ? "hithead" : "hit"); }
        public void Horn() { Play("horn"); }
        public void Heartbeat() { Play("heartbeat"); }
        public void Launch() { Play("launch"); }
        public void Explosion(float delay, float dist) { Play("explosion", M.Clamp(1.3f - dist / 700, 0.35f, 1), delay); }
        public void Door() { Play("door"); }
        public void Loot() { Play("loot"); }
        public void Step() { Play("step" + M.Rng.Next(3), 0.8f); }

        // The recorded voice-over clips, in the order of IntroVoice.Files. A file that is missing or unreadable is
        // skipped: that card simply plays without narration. Call on the main thread.
        public void SetVoice(byte[][] files)
        {
            voice = new SoundEffect[files.Length];
            for (int i = 0; i < files.Length; i++)
            {
                Wav w = Wav.Parse(files[i]);
                if (w != null) voice[i] = new SoundEffect(w.Pcm, w.Rate, w.Channels == 2 ? AudioChannels.Stereo : AudioChannels.Mono);
            }
        }
        public void Voice(int clip)
        {
            StopVoice();
            if (voice == null || clip < 0 || clip >= voice.Length || voice[clip] == null) return;
            try { speaking = voice[clip].CreateInstance(); speaking.Volume = M.Clamp(Master); speaking.Play(); } catch (Exception) { speaking = null; }
        }
        public void StopVoice()
        {
            if (speaking == null) return;
            try { speaking.Stop(); speaking.Dispose(); } catch (Exception) { }
            speaking = null;
        }
    }
}
