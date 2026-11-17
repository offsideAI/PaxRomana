// First-person controller: pointer-lock look, walk / crouch / prone on the ridge, scope sway and breath control.
import * as THREE from 'three';
import { groundAt } from './terrain.js';
import { SPAWN } from './config.js';

const EYE = [1.7, 1.08, 0.42];
const SWAY = [0.0021, 0.0011, 0.00042];
export const BASE_FOV = 68;

export class Player {
  constructor(camera, dom, obstacles, audio) {
    this.camera = camera; this.dom = dom; this.obstacles = obstacles; this.audio = audio;
    this.pos = new THREE.Vector3(SPAWN.x, 0, SPAWN.z);
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
      if (e.code === 'KeyC') this.stance = this.stance === 1 ? 0 : 1;
      if (e.code === 'KeyZ') this.stance = this.stance === 2 ? 0 : 2;
      this.onKey && this.onKey(e.code);
    });
    addEventListener('keyup', (e) => { this.keys[e.code] = false; });
    addEventListener('blur', () => { this.keys = {}; });
  }

  lock() { this.dom.requestPointerLock && this.dom.requestPointerLock(); }

  forward(out) { return this.camera.getWorldDirection(out); }

  update(dt) {
    const K = this.keys;
    this.t += dt;
    if (this.enabled && this.locked) {
      // keyboard look: arrows, easing in from a slow start so taps make fine corrections through the scope
      const lx = (K.ArrowLeft ? 1 : 0) - (K.ArrowRight ? 1 : 0), ly = (K.ArrowUp ? 1 : 0) - (K.ArrowDown ? 1 : 0);
      if (lx || ly) {
        this.lookT += dt;
        const rate = (0.2 + 0.8 * Math.min(1, this.lookT / 0.6)) * 1.7 * (this.camera.fov / BASE_FOV) * dt;
        this.yaw += lx * rate;
        this.pitch = THREE.MathUtils.clamp(this.pitch + ly * rate, -1.35, 1.35);
      } else this.lookT = 0;
      let f = (K.KeyW ? 1 : 0) - (K.KeyS ? 1 : 0), s = (K.KeyD ? 1 : 0) - (K.KeyA ? 1 : 0);
      const sprint = K.ShiftLeft && !this.scoped && this.stance === 0 && f > 0;
      let sp = [4.2, 2.1, 0.8][this.stance] * (sprint ? 1.6 : 1) * (this.scoped ? 0.35 : 1);
      const l = Math.hypot(f, s);
      if (l > 0) {
        f /= l; s /= l;
        const sy = Math.sin(this.yaw), cy = Math.cos(this.yaw);
        this.pos.x += (-sy * f + cy * s) * sp * dt;
        this.pos.z += (-cy * f - sy * s) * sp * dt;
        this.bob += sp * dt * 1.9;
        this.stepAcc += sp * dt;
        if (this.stepAcc > 2.2) { this.stepAcc = 0; this.audio.step(); }
      }
      this.moveAmt += ((l > 0 ? sp / 4.2 : 0) - this.moveAmt) * Math.min(1, dt * 8);
      this.sprinting = sprint && l > 0;
      // hold the ridge
      const dx = this.pos.x - SPAWN.x, dz = this.pos.z - SPAWN.z, d = Math.hypot(dx, dz);
      if (d > 48) { this.pos.x = SPAWN.x + dx / d * 48; this.pos.z = SPAWN.z + dz / d * 48; }
      for (const o of this.obstacles) {
        const ox = this.pos.x - o.x, oz = this.pos.z - o.z, od = Math.hypot(ox, oz), r = o.r + 0.4;
        if (od < r && od > 1e-4) { this.pos.x = o.x + ox / od * r; this.pos.z = o.z + oz / od * r; }
      }
      // breath
      this.holding = this.scoped && K.ShiftLeft && this.exhausted <= 0 && this.breath > 0;
      if (this.holding) {
        this.breath -= dt / 6.5;
        this.beatT -= dt; if (this.beatT <= 0) { this.beatT = 0.85; this.audio.heartbeat(); }
        if (this.breath <= 0) { this.breath = 0; this.exhausted = 3.2; }
      } else { this.breath = Math.min(1, this.breath + dt / 5); this.beatT = 0; }
      this.exhausted = Math.max(0, this.exhausted - dt);
    } else this.moveAmt *= 0.9;

    this.pos.y = groundAt(this.pos.x, this.pos.z);
    this.eye += (EYE[this.stance] - this.eye) * Math.min(1, dt * 7);
    const bobY = Math.sin(this.bob * 2) * 0.035 * this.moveAmt, bobX = Math.cos(this.bob) * 0.02 * this.moveAmt;

    // scope sway: slow figure-eight plus tremor, calmer when crouched/prone or holding breath
    let amp = SWAY[this.stance] * (this.scoped ? 1 : 0.25) * (1 + this.moveAmt * 4);
    if (this.holding) amp *= 0.12; else if (this.exhausted > 0) amp *= 2.6;
    const t = this.t;
    const tx = (Math.sin(t * 0.9) + 0.5 * Math.sin(t * 2.3 + 1.7) + 0.18 * Math.sin(t * 7.1)) * amp;
    const ty = (Math.sin(t * 1.3 + 0.6) * Math.cos(t * 0.45) + 0.4 * Math.sin(t * 2.9) + 0.18 * Math.sin(t * 6.3 + 2.0)) * amp;
    this.swayX += (tx - this.swayX) * Math.min(1, dt * 10);
    this.swayY += (ty - this.swayY) * Math.min(1, dt * 10);

    this.kick *= Math.exp(-dt * 5.5); this.kickYaw *= Math.exp(-dt * 5.5);
    const c = this.camera;
    c.position.set(this.pos.x, this.pos.y + this.eye + bobY, this.pos.z);
    c.rotation.set(this.pitch + this.swayY + this.kick, this.yaw + this.swayX + this.kickYaw + bobX * 0.2, 0, 'YXZ');
  }
}
