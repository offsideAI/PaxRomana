// Fully synthesised audio (Web Audio): rifle report with valley echo, bolt, impacts, cornu horn, ambience.
export class Audio {
  constructor() { this.ctx = null; }

  init() {
    if (this.ctx) { this.ctx.resume(); return; }
    const C = window.AudioContext || window.webkitAudioContext;
    if (!C) return;
    const ctx = this.ctx = new C();
    this.master = ctx.createGain(); this.master.gain.value = 0.8;
    const comp = ctx.createDynamicsCompressor();
    this.master.connect(comp); comp.connect(ctx.destination);
    // noise buffers
    const len = ctx.sampleRate * 2;
    this.white = ctx.createBuffer(1, len, ctx.sampleRate);
    this.brown = ctx.createBuffer(1, len, ctx.sampleRate);
    const w = this.white.getChannelData(0), b = this.brown.getChannelData(0);
    let last = 0;
    for (let i = 0; i < len; i++) { w[i] = Math.random() * 2 - 1; last = (last + 0.02 * w[i]) / 1.02; b[i] = last * 3.5; }
    // valley reverb impulse
    const il = ctx.sampleRate * 3.6, ir = ctx.createBuffer(2, il, ctx.sampleRate);
    for (let ch = 0; ch < 2; ch++) {
      const d = ir.getChannelData(ch); let lp = 0;
      for (let i = 0; i < il; i++) { const t = i / il; lp += ((Math.random() * 2 - 1) - lp) * (0.5 - t * 0.42); d[i] = lp * Math.pow(1 - t, 2.6); }
    }
    this.reverb = ctx.createConvolver(); this.reverb.buffer = ir;
    const rg = ctx.createGain(); rg.gain.value = 0.9;
    this.reverb.connect(rg); rg.connect(this.master);
    this._ambience();
  }

  _noise(buf, t, dur, type, f0, f1, gain, { q = 0.8, attack = 0.002, wet = 0, pan = 0 } = {}) {
    const ctx = this.ctx, s = ctx.createBufferSource(); s.buffer = buf; s.loop = true;
    const f = ctx.createBiquadFilter(); f.type = type; f.Q.value = q;
    f.frequency.setValueAtTime(f0, t); f.frequency.exponentialRampToValueAtTime(Math.max(20, f1), t + dur);
    const g = ctx.createGain();
    g.gain.setValueAtTime(0.0001, t); g.gain.exponentialRampToValueAtTime(gain, t + attack); g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    s.connect(f); f.connect(g);
    let out = g;
    if (pan && ctx.createStereoPanner) { const p = ctx.createStereoPanner(); p.pan.value = pan; g.connect(p); out = p; }
    out.connect(this.master);
    if (wet) { const wg = ctx.createGain(); wg.gain.value = wet; out.connect(wg); wg.connect(this.reverb); }
    s.start(t, Math.random()); s.stop(t + dur + 0.05);
  }
  _tone(t, dur, type, f0, f1, gain, wet = 0, attack = 0.003) {
    const ctx = this.ctx, o = ctx.createOscillator(); o.type = type;
    o.frequency.setValueAtTime(f0, t); o.frequency.exponentialRampToValueAtTime(Math.max(20, f1), t + dur);
    const g = ctx.createGain();
    g.gain.setValueAtTime(0.0001, t); g.gain.exponentialRampToValueAtTime(gain, t + attack); g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    o.connect(g); g.connect(this.master);
    if (wet) { const wg = ctx.createGain(); wg.gain.value = wet; g.connect(wg); wg.connect(this.reverb); }
    o.start(t); o.stop(t + dur + 0.05);
  }

  shot() {
    if (!this.ctx) return;
    const t = this.ctx.currentTime;
    this._noise(this.white, t, 0.16, 'highpass', 1800, 900, 1.0, { wet: 0.5 });
    this._noise(this.white, t, 0.55, 'lowpass', 2600, 120, 1.3, { wet: 0.8 });
    this._tone(t, 0.3, 'sine', 140, 38, 1.2, 0.3);
    // slap-back from the castle walls, then the far hills
    this._noise(this.brown, t + 1.15, 0.9, 'lowpass', 900, 150, 0.5, { attack: 0.08, wet: 0.6, pan: -0.3 });
    this._noise(this.brown, t + 2.3, 1.4, 'lowpass', 500, 100, 0.28, { attack: 0.2, wet: 0.6, pan: 0.4 });
  }
  bolt(delay = 0) {
    if (!this.ctx) return;
    const t = this.ctx.currentTime + delay;
    [[0, 3200, 0.3], [0.13, 1900, 0.4], [0.36, 2300, 0.4], [0.5, 3600, 0.32]].forEach(([d, f, g]) => {
      this._noise(this.white, t + d, 0.05, 'bandpass', f, f * 0.7, g, { q: 3 });
      this._tone(t + d, 0.04, 'square', f * 0.5, f * 0.3, 0.05);
    });
  }
  reload() {
    if (!this.ctx) return;
    const t = this.ctx.currentTime;
    [[0.2, 1500], [0.9, 900], [1.5, 2100], [2.0, 2800], [2.15, 2000]].forEach(([d, f]) => this._noise(this.white, t + d, 0.07, 'bandpass', f, f * 0.6, 0.4, { q: 2.5 }));
  }
  dry() { if (this.ctx) this._noise(this.white, this.ctx.currentTime, 0.03, 'bandpass', 2500, 2000, 0.25, { q: 4 }); }
  impact(kind, delay, dist) {
    if (!this.ctx) return;
    const t = this.ctx.currentTime + delay, g = Math.max(0.12, 1 - dist / 700);
    if (kind === 'flesh') { this._tone(t, 0.16, 'sine', 120, 50, 0.7 * g, 0.4); this._noise(this.brown, t, 0.12, 'lowpass', 700, 200, 0.6 * g, { wet: 0.4 }); }
    else if (kind === 'stone') { this._noise(this.white, t, 0.09, 'bandpass', 3400, 1500, 0.5 * g, { q: 2, wet: 0.8 }); this._tone(t + 0.02, 0.5, 'sine', 2600 + Math.random() * 900, 900, 0.1 * g, 0.9); }
    else this._noise(this.brown, t, 0.2, 'lowpass', 600, 120, 0.6 * g, { wet: 0.5 });
  }
  hit(head) { if (this.ctx) { const t = this.ctx.currentTime; this._tone(t, 0.07, 'triangle', head ? 1900 : 1300, head ? 1500 : 1000, 0.18); if (head) this._tone(t + 0.06, 0.1, 'triangle', 2500, 2300, 0.14); } }
  horn() {
    if (!this.ctx) return;
    const t = this.ctx.currentTime + 1.2;
    for (const [d, f, len] of [[0, 147, 1.8], [2.0, 147, 0.7], [2.8, 196, 2.4]]) {
      for (const k of [1, 2.005, 3.01]) {
        const o = this.ctx.createOscillator(), g = this.ctx.createGain(), lp = this.ctx.createBiquadFilter();
        o.type = 'sawtooth'; o.frequency.value = f * k; lp.type = 'lowpass'; lp.frequency.value = 820;
        g.gain.setValueAtTime(0.0001, t + d); g.gain.linearRampToValueAtTime(0.09 / k, t + d + 0.25); g.gain.setValueAtTime(0.09 / k, t + d + len - 0.2); g.gain.linearRampToValueAtTime(0.0001, t + d + len);
        o.connect(lp); lp.connect(g); g.connect(this.reverb); g.connect(this.master);
        o.start(t + d); o.stop(t + d + len + 0.1);
      }
    }
  }
  heartbeat() { if (this.ctx) { const t = this.ctx.currentTime; this._tone(t, 0.12, 'sine', 62, 40, 0.5); this._tone(t + 0.17, 0.12, 'sine', 54, 36, 0.35); } }
  launch() { if (this.ctx) { const t = this.ctx.currentTime; this._tone(t, 0.26, 'sine', 190, 48, 0.9, 0.3); this._noise(this.white, t, 0.24, 'bandpass', 1000, 220, 0.5, { q: 0.8, wet: 0.4 }); } }
  // delay (s) lets the report arrive after the flash; dist (m) quietens far blasts
  explosion(delay = 0, dist = 0) {
    if (!this.ctx) return;
    const t = this.ctx.currentTime + delay, g = Math.max(0.35, Math.min(1.2, 1.3 - dist / 700));
    this._tone(t, 0.9, 'sine', 92, 24, 1.4 * g, 0.6);
    this._noise(this.brown, t, 1.7, 'lowpass', 900, 60, 1.3 * g, { wet: 0.8, attack: 0.004 });
    this._noise(this.white, t, 0.35, 'bandpass', 2200, 300, 0.5 * g, { wet: 0.5 });
  }
  door() { if (this.ctx) { const t = this.ctx.currentTime; this._noise(this.brown, t, 0.55, 'bandpass', 260, 520, 0.3, { q: 6, attack: 0.05 }); this._noise(this.brown, t + 0.5, 0.16, 'lowpass', 300, 80, 0.5); } }
  locked() { if (this.ctx) { const t = this.ctx.currentTime; this._noise(this.white, t, 0.05, 'bandpass', 1800, 900, 0.25, { q: 3 }); this._noise(this.white, t + 0.11, 0.05, 'bandpass', 1500, 800, 0.2, { q: 3 }); } }
  loot() { if (this.ctx) { const t = this.ctx.currentTime; [1320, 1760, 2640].forEach((f, i) => this._tone(t + i * 0.07, 0.35, 'sine', f, f, 0.12)); } }
  step() { if (this.ctx) this._noise(this.brown, this.ctx.currentTime, 0.11, 'bandpass', 500 + Math.random() * 300, 250, 0.22, { q: 0.7 }); }

  _ambience() {
    const ctx = this.ctx;
    // wind in the grass
    const s = ctx.createBufferSource(); s.buffer = this.brown; s.loop = true;
    const f = ctx.createBiquadFilter(); f.type = 'bandpass'; f.frequency.value = 420; f.Q.value = 0.4;
    const g = ctx.createGain(); g.gain.value = 0.22;
    const lfo = ctx.createOscillator(), lg = ctx.createGain(); lfo.frequency.value = 0.09; lg.gain.value = 0.1;
    lfo.connect(lg); lg.connect(g.gain); lfo.start();
    const lfo2 = ctx.createOscillator(), lg2 = ctx.createGain(); lfo2.frequency.value = 0.05; lg2.gain.value = 160;
    lfo2.connect(lg2); lg2.connect(f.frequency); lfo2.start();
    s.connect(f); f.connect(g); g.connect(this.master); s.start();
    // crickets
    const c = ctx.createBufferSource(); c.buffer = this.white; c.loop = true;
    const cf = ctx.createBiquadFilter(); cf.type = 'bandpass'; cf.frequency.value = 5200; cf.Q.value = 14;
    const cg = ctx.createGain(); cg.gain.value = 0.0;
    const tr = ctx.createOscillator(), tg = ctx.createGain(); tr.type = 'square'; tr.frequency.value = 17; tg.gain.value = 0.012;
    tr.connect(tg); tg.connect(cg.gain); tr.start();
    c.connect(cf); cf.connect(cg); cg.connect(this.master); c.start();
    // birdsong
    const chirp = () => {
      if (ctx.state === 'running') {
        const t = ctx.currentTime, base = 2400 + Math.random() * 2200, n = 2 + Math.floor(Math.random() * 5), pan = Math.random() * 1.6 - 0.8;
        for (let i = 0; i < n; i++) {
          const o = ctx.createOscillator(), gg = ctx.createGain(), p = ctx.createStereoPanner ? ctx.createStereoPanner() : null;
          const tt = t + i * (0.09 + Math.random() * 0.05), f0 = base * (0.9 + Math.random() * 0.3);
          o.frequency.setValueAtTime(f0, tt); o.frequency.exponentialRampToValueAtTime(f0 * (Math.random() < 0.5 ? 1.35 : 0.75), tt + 0.07);
          gg.gain.setValueAtTime(0.0001, tt); gg.gain.exponentialRampToValueAtTime(0.022, tt + 0.012); gg.gain.exponentialRampToValueAtTime(0.0001, tt + 0.08);
          o.connect(gg);
          if (p) { p.pan.value = pan; gg.connect(p); p.connect(this.master); p.connect(this.reverb); } else gg.connect(this.master);
          o.start(tt); o.stop(tt + 0.1);
        }
      }
      setTimeout(chirp, 700 + Math.random() * 3800);
    };
    chirp();
  }
}
