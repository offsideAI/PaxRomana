// Xbox controller input and haptics.
// Input comes from the native Xbox host when the game runs inside it (the host posts controller state into the page),
// otherwise from the browser Gamepad API. Haptics go the same two ways: the host drives all four motors (two body
// motors plus the impulse motors in the triggers); a browser gets dual-rumble, and trigger-rumble where supported.
export const B = { A: 0, B: 1, X: 2, Y: 3, LB: 4, RB: 5, LT: 6, RT: 7, VIEW: 8, MENU: 9, L3: 10, R3: 11, UP: 12, DOWN: 13, LEFT: 14, RIGHT: 15 };
const DEAD = 0.16;

export class Pad {
  constructor() {
    this.axes = [0, 0, 0, 0]; this.btn = new Array(16).fill(0); this.prev = new Array(16).fill(0);
    this.connected = false;      // a controller is present
    this.active = false;         // the controller was the last device used (drives button prompts)
    this.fx = []; this.out = [0, 0, 0, 0]; this.sendT = 0; this.silent = true; this.enabled = true;
    this.native = null; this.nativeT = -1e9; this.web = null;
    this.bridge = typeof window !== 'undefined' && window.chrome && window.chrome.webview ? window.chrome.webview : null;
    if (this.bridge) {
      this.bridge.addEventListener('message', (e) => {
        const d = e.data;
        if (d && d.t === 'pad') { this.native = d; this.nativeT = performance.now(); }
      });
    }
    if (typeof addEventListener !== 'undefined') {
      addEventListener('keydown', () => { this.active = false; });
      addEventListener('mousedown', () => { this.active = false; });
    }
  }

  poll() {
    for (let i = 0; i < 16; i++) this.prev[i] = this.btn[i];
    let ok = false;
    if (this.native && performance.now() - this.nativeT < 500 && !this.native.none) {
      const d = this.native;
      for (let i = 0; i < 4; i++) this.axes[i] = d.a[i] || 0;
      for (let i = 0; i < 16; i++) this.btn[i] = d.b[i] || 0;
      ok = true; this.web = null;
    } else if (typeof navigator !== 'undefined' && navigator.getGamepads) {
      let gp = null;
      for (const g of navigator.getGamepads()) if (g && g.connected && (!gp || (g.mapping === 'standard' && gp.mapping !== 'standard'))) gp = g;
      if (gp) {
        for (let i = 0; i < 4; i++) this.axes[i] = gp.axes[i] || 0;
        for (let i = 0; i < 16; i++) this.btn[i] = gp.buttons[i] ? gp.buttons[i].value || (gp.buttons[i].pressed ? 1 : 0) : 0;
        ok = true; this.web = gp;
      }
    }
    if (!ok) { this.axes.fill(0); this.btn.fill(0); }
    this.connected = ok;
    if (ok && !this.active) {
      if (Math.hypot(this.axes[0], this.axes[1]) > 0.5 || Math.hypot(this.axes[2], this.axes[3]) > 0.5) this.active = true;
      for (let i = 0; i < 16; i++) if (this.btn[i] > 0.5) this.active = true;
    }
  }

  value(i) { return this.btn[i]; }
  down(i) { return this.btn[i] > 0.5; }
  pressed(i) { return this.btn[i] > 0.5 && this.prev[i] <= 0.5; }
  released(i) { return this.btn[i] <= 0.5 && this.prev[i] > 0.5; }

  // Stick with a radial dead zone, rescaled so movement starts from zero at its edge. which: 0 = left, 1 = right.
  stick(which, out) {
    const x = this.axes[which * 2], y = this.axes[which * 2 + 1], m = Math.hypot(x, y);
    if (m < DEAD) { out.x = 0; out.y = 0; return out; }
    const k = Math.min(1, (m - DEAD) / (1 - DEAD)) / m;
    out.x = x * k; out.y = y * k;
    return out;
  }

  // Queue a haptic pulse. low / high are the heavy and light body motors, lt / rt the trigger motors (0..1).
  // ms is the length, delay (ms) postpones it, decay fades it out instead of cutting it off.
  rumble({ low = 0, high = 0, lt = 0, rt = 0, ms = 100, delay = 0, decay = false }) {
    if (!this.enabled) return;
    if (this.fx.length > 24) this.fx.shift();
    this.fx.push({ low, high, lt, rt, dur: ms / 1000, t: -delay / 1000, decay });
  }
  stop() { this.fx.length = 0; }

  // Mix the active pulses and send the result to the controller.
  update(dt) {
    let l = 0, h = 0, lt = 0, rt = 0, n = 0;
    for (const e of this.fx) {
      e.t += dt;
      if (e.t >= e.dur) continue;
      this.fx[n++] = e;
      if (e.t < 0) continue;
      const k = e.decay ? 1 - e.t / e.dur : 1;
      l += e.low * k; h += e.high * k; lt += e.lt * k; rt += e.rt * k;
    }
    this.fx.length = n;
    const o = this.out;
    o[0] = Math.min(1, l); o[1] = Math.min(1, h); o[2] = Math.min(1, lt); o[3] = Math.min(1, rt);
    const quiet = o[0] + o[1] + o[2] + o[3] < 0.004;
    this.sendT -= dt;
    if (quiet && this.silent) return;
    if (!quiet && this.sendT > 0) return;
    this.sendT = 0.05; this.silent = quiet;
    if (this.bridge) {
      this.bridge.postMessage({ t: 'rumble', l: +o[0].toFixed(2), h: +o[1].toFixed(2), lt: +o[2].toFixed(2), rt: +o[3].toFixed(2) });
      return;
    }
    const act = this.web && this.web.vibrationActuator;
    if (!act || !act.playEffect) return;
    try {
      if (quiet) { act.reset && act.reset(); return; }
      const trig = act.effects && act.effects.includes('trigger-rumble');
      const p = act.playEffect(trig ? 'trigger-rumble' : 'dual-rumble', {
        duration: 90, startDelay: 0,
        strongMagnitude: o[0], weakMagnitude: trig ? o[1] : Math.min(1, o[1] + (o[2] + o[3]) * 0.5),
        leftTrigger: o[2], rightTrigger: o[3],
      });
      if (p && p.catch) p.catch(() => {});
    } catch (e) { /* haptics are optional */ }
  }
}
