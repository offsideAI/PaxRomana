// First-person controller: pointer-lock look, free roam on foot (walk / crouch / prone, wade and swim, cross the bridge),
// scope sway and breath control.
import * as THREE from 'three';
import { groundAt } from './terrain.js';
import { SPAWN, INNER, BRIDGE } from './config.js';
import { resolve } from './collision.js';
import { B } from './gamepad.js';

const EYE = [1.7, 1.08, 0.42];
const SWAY = [0.0021, 0.0011, 0.00042];
export const BASE_FOV = 68;
const RADIUS = 0.4;                    // player body radius
const SWIM_Y = -1.35;                  // feet height when afloat (water surface is y = 0)
const EDGE = 14, X0 = INNER.x0 + EDGE, X1 = INNER.x0 + INNER.size - EDGE, Z0 = INNER.z0 + EDGE, Z1 = INNER.z0 + INNER.size - EDGE;
const DECK = 2.7;                      // bridge half-width
const PIERS = [0, 1, 2, 3, 4].map((i) => {
  const z = BRIDGE.z0 + 8 + i * (BRIDGE.z1 - BRIDGE.z0 - 16) / 4;
  return { x0: BRIDGE.x - 4.8, x1: BRIDGE.x + 4.8, z0: z - 1.3, z1: z + 1.3 };
});

export class Player {
  // solids: static footprints (see collision.js); soldiers: the garrison, whose living members also block the way
  constructor(camera, dom, solids, audio, soldiers = [], pad = null) {
    this.camera = camera; this.dom = dom; this.solids = solids; this.audio = audio; this.soldiers = soldiers;
    this.pad = pad; this.padMode = false; this.padSprint = false; this.aimSlow = 1; this.turnT = 0; this.shake = 0;
    this._ls = { x: 0, y: 0 }; this._rs = { x: 0, y: 0 };
    this.pos = new THREE.Vector3(SPAWN.x, groundAt(SPAWN.x, SPAWN.z), SPAWN.z);
    this.onBridge = false; this.swimming = false; this.depth = 0; this.onEdge = null;
    this.yaw = Math.atan2(SPAWN.x, SPAWN.z); this.pitch = -0.1;
    this.stance = 0; this.eye = EYE[0];
    this.keys = {}; this.locked = false; this.enabled = false;
    this.kick = 0; this.kickYaw = 0; this.breath = 1; this.exhausted = 0; this.holding = false;
    this.scoped = false; this.zoom = 8; this.bob = 0; this.moveAmt = 0; this.stepAcc = 0; this.beatT = 0;
    this.swayX = 0; this.swayY = 0; this.lookT = 0; this.t = Math.random() * 100;

    document.addEventListener('pointerlockchange', () => { this.locked = document.pointerLockElement === dom; this.onLock && this.onLock(this.locked); });
    document.addEventListener('mousemove', (e) => {
      if (!this.locked || !this.enabled) return;
      const k = 0.0021 * (camera.fov / BASE_FOV);
      this.yaw -= e.movementX * k;
      this.pitch = THREE.MathUtils.clamp(this.pitch - e.movementY * k, -1.35, 1.35);
    });
    addEventListener('keydown', (e) => {
      if (this.enabled && /^(Arrow|Space|Enter|Minus|Equal)/.test(e.code)) e.preventDefault();
      if (e.repeat) return;
      this.keys[e.code] = true;
      if (!this.enabled) return;
      if (e.code === 'KeyC' && !this.swimming) this.stance = this.stance === 1 ? 0 : 1;
      if (e.code === 'KeyZ' && !this.swimming) this.stance = this.stance === 2 ? 0 : 2;
      this.onKey && this.onKey(e.code);
    });
    addEventListener('keyup', (e) => { this.keys[e.code] = false; });
    addEventListener('blur', () => { this.keys = {}; });
  }

  lock() { this.dom.requestPointerLock && this.dom.requestPointerLock(); }

  forward(out) { return this.camera.getWorldDirection(out); }

  // Keep the player out of walls, trees, boulders, tents and living soldiers, on the bridge deck, and inside the valley.
  _collide() {
    const P = this.pos, B = BRIDGE;
    for (let pass = 0; pass < 2; pass++) if (!resolve(P, RADIUS, this.solids)) break;
    for (const s of this.soldiers) {
      if (!s.alive || Math.abs(s.pos.y - P.y) > 1.6) continue;
      const dx = P.x - s.pos.x, dz = P.z - s.pos.z, d = Math.hypot(dx, dz), r = RADIUS + 0.32;
      if (d < r && d > 1e-4) { P.x = s.pos.x + dx / d * r; P.z = s.pos.z + dz / d * r; }
    }
    // bridge: walk on from either bank, parapets keep you on the deck, swim under the arches
    const g = groundAt(P.x, P.z);
    if (P.z > B.z0 && P.z < B.z1 && Math.abs(P.x - B.x) < DECK) {
      if (!this.onBridge) {
        if (g > B.y - 0.6) this.onBridge = true;
        else if (g > 0.4) P.x = B.x + (P.x < B.x ? -1 : 1) * (DECK + 0.01);     // no headroom under the deck here
      }
    } else this.onBridge = false;
    if (this.onBridge) {
      if (g < B.y + 0.9) P.x = Math.max(B.x - 2.1, Math.min(B.x + 2.1, P.x));    // between the parapets
    } else if (g < B.y - 0.5) resolve(P, RADIUS, PIERS);
    // edge of the playable valley
    if (P.x < X0 || P.x > X1 || P.z < Z0 || P.z > Z1) {
      P.x = Math.max(X0, Math.min(X1, P.x)); P.z = Math.max(Z0, Math.min(Z1, P.z));
      this.onEdge && this.onEdge();
    }
  }

  update(dt) {
    const K = this.keys;
    this.t += dt;
    const pad = this.pad, ls = this._ls, rs = this._rs;
    ls.x = ls.y = rs.x = rs.y = 0;
    if (pad && pad.connected && this.enabled) { pad.stick(0, ls); pad.stick(1, rs); }
    if (this.enabled && (this.locked || this.padMode)) {
      // keyboard look: arrows, easing in from a slow start so taps make fine corrections through the scope
      const lx = (K.ArrowLeft ? 1 : 0) - (K.ArrowRight ? 1 : 0), ly = (K.ArrowUp ? 1 : 0) - (K.ArrowDown ? 1 : 0);
      if (lx || ly) {
        this.lookT += dt;
        const rate = (0.2 + 0.8 * Math.min(1, this.lookT / 0.6)) * 1.7 * (this.camera.fov / BASE_FOV) * dt;
        this.yaw += lx * rate;
        this.pitch = THREE.MathUtils.clamp(this.pitch + ly * rate, -1.35, 1.35);
      } else this.lookT = 0;
      // controller look: squared response for fine aim, a turn boost when the stick is held hard over (hip only),
      // slower through the scope and slower again while the reticle is on a target (aim friction)
      const rm = Math.hypot(rs.x, rs.y);
      if (rm > 0) {
        this.turnT = rm > 0.92 && !this.scoped ? this.turnT + dt : 0;
        const boost = 1 + Math.min(1, this.turnT / 0.45) * 0.7;
        const zoomK = this.scoped ? Math.pow(this.camera.fov / BASE_FOV, 0.82) * 0.8 : 1;
        const rate = 2.5 * rm * boost * zoomK * this.aimSlow * dt;      // rm twice over: curve = stick squared
        this.yaw -= rs.x * rate;
        this.pitch = THREE.MathUtils.clamp(this.pitch - rs.y * rate * 0.72, -1.35, 1.35);
      } else this.turnT = 0;
      let f = (K.KeyW ? 1 : 0) - (K.KeyS ? 1 : 0), s = (K.KeyD ? 1 : 0) - (K.KeyA ? 1 : 0), analog = 1;
      if (!f && !s && (ls.x || ls.y)) { f = -ls.y; s = ls.x; analog = Math.min(1, Math.hypot(f, s)); }
      if (pad && pad.pressed(B.L3)) this.padSprint = true;                 // click the left stick to sprint
      if (analog < 0.6 || f <= 0) this.padSprint = false;
      const sprint = (K.ShiftLeft || this.padSprint) && !this.scoped && this.stance === 0 && f > 0;
      const wet = Math.min(1, Math.max(0, (this.depth - 0.3) / 1.0));       // wading slows you, swimming more so
      let sp = [4.2, 2.1, 0.8][this.stance] * (sprint && wet === 0 ? 1.6 : 1) * (this.scoped ? 0.35 : 1) * (1 - 0.58 * wet) * analog;
      const l = Math.hypot(f, s);
      if (l > 0) {
        f /= l; s /= l;
        const sy = Math.sin(this.yaw), cy = Math.cos(this.yaw);
        this.pos.x += (-sy * f + cy * s) * sp * dt;
        this.pos.z += (-cy * f - sy * s) * sp * dt;
        this.bob += sp * dt * 1.9;
        this.stepAcc += sp * dt;
        if (this.stepAcc > 2.2) { this.stepAcc = 0; if (!this.swimming) this.audio.step(); }
      }
      this.moveAmt += ((l > 0 ? sp / 4.2 : 0) - this.moveAmt) * Math.min(1, dt * 8);
      this.sprinting = sprint && wet === 0 && l > 0;
      this._collide();
      // breath
      this.holding = this.scoped && (K.ShiftLeft || (pad && pad.down(B.LB))) && this.exhausted <= 0 && this.breath > 0;
      if (this.holding) {
        this.breath -= dt / 6.5;
        this.beatT -= dt; if (this.beatT <= 0) { this.beatT = 0.85; this.audio.heartbeat(); }
        if (this.breath <= 0) { this.breath = 0; this.exhausted = 3.2; }
      } else { this.breath = Math.min(1, this.breath + dt / 5); this.beatT = 0; }
      this.exhausted = Math.max(0, this.exhausted - dt);
    } else this.moveAmt *= 0.9;

    // footing: ground, bridge deck, or afloat in the river
    const g = groundAt(this.pos.x, this.pos.z);
    this.depth = this.onBridge ? 0 : Math.max(0, -g);
    this.swimming = !this.onBridge && g < SWIM_Y;
    if (this.swimming) this.stance = 0;
    const footY = this.onBridge ? Math.max(g, BRIDGE.y) : Math.max(g, SWIM_Y);
    const dy = footY - this.pos.y;
    this.pos.y = Math.abs(dy) > 1.5 ? footY : this.pos.y + dy * Math.min(1, dt * 16);
    this.eye += (EYE[this.stance] - this.eye) * Math.min(1, dt * 7);
    const bobY = Math.sin(this.bob * 2) * 0.035 * this.moveAmt + (this.swimming ? Math.sin(this.t * 1.7) * 0.05 : 0);
    const bobX = Math.cos(this.bob) * 0.02 * this.moveAmt;

    // scope sway: slow figure-eight plus tremor, calmer when crouched/prone or holding breath
    let amp = SWAY[this.stance] * (this.scoped ? 1 : 0.25) * (1 + this.moveAmt * 4);
    if (this.holding) amp *= 0.12; else if (this.exhausted > 0) amp *= 2.6;
    const t = this.t;
    const tx = (Math.sin(t * 0.9) + 0.5 * Math.sin(t * 2.3 + 1.7) + 0.18 * Math.sin(t * 7.1)) * amp;
    const ty = (Math.sin(t * 1.3 + 0.6) * Math.cos(t * 0.45) + 0.4 * Math.sin(t * 2.9) + 0.18 * Math.sin(t * 6.3 + 2.0)) * amp;
    this.swayX += (tx - this.swayX) * Math.min(1, dt * 10);
    this.swayY += (ty - this.swayY) * Math.min(1, dt * 10);

    this.kick *= Math.exp(-dt * 5.5); this.kickYaw *= Math.exp(-dt * 5.5);
    this.shake *= Math.exp(-dt * 4.5);
    const shx = this.shake * Math.sin(this.t * 61) * 0.02, shy = this.shake * Math.sin(this.t * 47 + 1.3) * 0.02;
    const c = this.camera;
    c.position.set(this.pos.x, this.pos.y + this.eye + bobY, this.pos.z);
    c.rotation.set(this.pitch + this.swayY + this.kick + shy, this.yaw + this.swayX + this.kickYaw + bobX * 0.2 + shx, shx * 0.5, 'YXZ');
  }
}
