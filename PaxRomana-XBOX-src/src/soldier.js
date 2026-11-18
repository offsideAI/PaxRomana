// A legionary of the Third Rome: segmented composite armour, crested helmet, riot scutum and shock pilum. Built from primitives,
// animated procedurally, with head / torso / leg hit volumes.
import * as THREE from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';

const C = {     // legionaries of the Third Rome: black composite armour, red trim, the old crest kept
  skin: 0x14171c, tunic: 0x2a2f3a, leather: 0x15171b, steel: 0x3d444e, bronze: 0xc0281e,
  crest: 0xe0201a, wood: 0x1c1e22, shield: 0x1b222c, gold: 0xc8322a, cloak: 0x8a1210,
};

function part() {
  const lists = { matte: [], metal: [] };
  const api = {
    add(geo, hex, metal = false) {
      const g = geo.index ? geo.toNonIndexed() : geo;
      const c = new THREE.Color(hex), n = g.attributes.position.count, arr = new Float32Array(n * 3);
      for (let i = 0; i < n; i++) { arr[i * 3] = c.r; arr[i * 3 + 1] = c.g; arr[i * 3 + 2] = c.b; }
      g.setAttribute('color', new THREE.BufferAttribute(arr, 3));
      g.deleteAttribute('uv');
      lists[metal ? 'metal' : 'matte'].push(g);
      return api;
    },
    build() {
      return {
        matte: lists.matte.length ? mergeGeometries(lists.matte, false) : null,
        metal: lists.metal.length ? mergeGeometries(lists.metal, false) : null,
      };
    },
  };
  return api;
}
const cyl = (rt, rb, h, seg = 10, open = false, ts = 0, tl = Math.PI * 2) => new THREE.CylinderGeometry(rt, rb, h, seg, 1, open, ts, tl);
const sph = (r, w = 12, h = 8) => new THREE.SphereGeometry(r, w, h);
const box = (w, h, d) => new THREE.BoxGeometry(w, h, d);

let GEO = null, MAT = null;
function assets() {
  if (GEO) return;
  MAT = {
    matte: new THREE.MeshStandardMaterial({ vertexColors: true, roughness: 0.82, side: THREE.DoubleSide }),
    metal: new THREE.MeshStandardMaterial({ vertexColors: true, roughness: 0.34, metalness: 0.92, side: THREE.DoubleSide }),
  };
  const body = (centurion) => {
    const p = part();
    p.add(cyl(0.17, 0.225, 0.4, 12).scale(1, 1, 0.8).translate(0, -0.15, 0), C.tunic);
    for (let i = 0; i < 6; i++) p.add(cyl(0.2 + (i % 2) * 0.008, 0.207 + (i % 2) * 0.008, 0.074, 14).scale(1, 1, 0.78).translate(0, 0.085 + i * 0.078, 0), C.steel, true);
    for (const s of [-1, 1]) {
      p.add(sph(0.115).scale(1.15, 0.5, 1.05).translate(s * 0.235, 0.52, 0), C.steel, true);
      p.add(sph(0.1).scale(1.1, 0.4, 1.0).translate(s * 0.27, 0.47, 0), C.steel, true);
    }
    p.add(cyl(0.214, 0.214, 0.05, 14).scale(1, 1, 0.8).translate(0, 0.045, 0), C.leather);
    for (let i = 0; i < 4; i++) p.add(box(0.026, 0.2, 0.012).translate(-0.06 + i * 0.04, -0.08, 0.175), C.leather);
    p.add(sph(0.03, 8, 6).scale(1, 1, 0.5).translate(0, 0.045, 0.172), C.bronze, true);
    p.add(cyl(0.05, 0.058, 0.09, 8).translate(0, 0.585, 0), C.skin);
    p.add(new THREE.TorusGeometry(0.066, 0.026, 6, 12).rotateX(Math.PI / 2).translate(0, 0.565, 0), C.tunic);
    p.add(box(0.045, 0.46, 0.03).rotateZ(0.12).translate(0.215, -0.1, -0.02), C.leather);
    p.add(sph(0.03, 6, 5).translate(0.245, 0.15, -0.02), C.bronze, true);
    if (centurion) p.add(box(0.44, 0.82, 0.03).translate(0, 0.16, -0.185), C.cloak);
    return p.build();
  };
  const head = part()
    .add(sph(0.105, 14, 10).translate(0, 0.1, 0), C.skin)
    .add(box(0.03, 0.04, 0.03).translate(0, 0.085, 0.105), C.skin)
    .build().matte;
  const helmet = (centurion) => {
    const m = part()
      .add(new THREE.SphereGeometry(0.124, 16, 8, 0, Math.PI * 2, 0, Math.PI * 0.55).translate(0, 0.105, 0), C.steel, true)
      .add(new THREE.TorusGeometry(0.124, 0.011, 5, 16).rotateX(Math.PI / 2).translate(0, 0.118, 0), C.bronze, true)
      .add(cyl(0.16, 0.175, 0.014, 12, false, Math.PI / 2, Math.PI).rotateX(-0.25).translate(0, 0.045, -0.02), C.steel, true)
      .add(box(0.014, 0.11, 0.075).translate(0.108, 0.04, 0.04), C.steel, true)
      .add(box(0.014, 0.11, 0.075).translate(-0.108, 0.04, 0.04), C.steel, true)
      .add(box(0.2, 0.018, 0.03).translate(0, 0.135, 0.115), C.bronze, true);
    const crest = cyl(0.2, 0.2, 0.03, 16, false, 0, Math.PI).rotateZ(Math.PI / 2);
    if (centurion) crest.rotateY(Math.PI / 2);
    m.add(crest.translate(0, 0.11, 0), C.crest);
    return m.build();
  };
  const leg = part()
    .add(cyl(0.072, 0.05, 0.86, 8).translate(0, -0.44, 0), C.skin)
    .add(cyl(0.056, 0.06, 0.24, 8).translate(0, -0.75, 0), C.leather)
    .add(box(0.09, 0.07, 0.24).translate(0, -0.89, 0.05), C.leather)
    .build().matte;
  const arm = part()
    .add(cyl(0.066, 0.072, 0.17, 8).translate(0, -0.06, 0), C.tunic)
    .add(cyl(0.045, 0.038, 0.5, 8).translate(0, -0.36, 0), C.skin)
    .add(sph(0.046, 8, 6).translate(0, -0.63, 0), C.skin)
    .build().matte;
  const shield = part()
    .add(cyl(0.42, 0.42, 1.08, 10, true, -0.62, 1.24).translate(0, 0, -0.42), C.shield)
    .add(cyl(0.424, 0.424, 0.035, 10, true, -0.62, 1.24).translate(0, 0.53, -0.42), C.gold, true)
    .add(cyl(0.424, 0.424, 0.035, 10, true, -0.62, 1.24).translate(0, -0.53, -0.42), C.gold, true)
    .add(cyl(0.423, 0.423, 0.05, 10, true, -0.3, 0.6).translate(0, 0, -0.42), C.gold, true)
    .add(sph(0.075, 10, 8).scale(1, 1, 0.6).translate(0, 0, 0.01), C.gold, true)
    .build();
  const pilum = part()
    .add(cyl(0.015, 0.015, 1.35, 6).translate(0, 0.675, 0), C.wood)
    .add(box(0.05, 0.12, 0.05).translate(0, 1.36, 0), C.wood)
    .add(cyl(0.005, 0.009, 0.62, 5).translate(0, 1.72, 0), C.steel, true)
    .add(cyl(0.001, 0.016, 0.07, 4).translate(0, 2.06, 0), C.steel, true)
    .build();
  GEO = { body: [body(false), body(true)], head, helmet: [helmet(false), helmet(true)], leg, arm, shield, pilum };
}

// closest approach between segment p0->p1 and segment q0->q1; returns [distance, t along p]
const _d1 = new THREE.Vector3(), _d2 = new THREE.Vector3(), _r = new THREE.Vector3(), _c1 = new THREE.Vector3(), _c2 = new THREE.Vector3();
function segSeg(p0, p1, q0, q1) {
  _d1.subVectors(p1, p0); _d2.subVectors(q1, q0); _r.subVectors(p0, q0);
  const a = _d1.dot(_d1), e = _d2.dot(_d2), f = _d2.dot(_r);
  let s, t;
  if (e < 1e-9) { t = 0; s = THREE.MathUtils.clamp(-_d1.dot(_r) / a, 0, 1); }
  else {
    const c = _d1.dot(_r), b = _d1.dot(_d2), den = a * e - b * b;
    s = den > 1e-9 ? THREE.MathUtils.clamp((b * f - c * e) / den, 0, 1) : 0;
    t = (b * s + f) / e;
    if (t < 0) { t = 0; s = THREE.MathUtils.clamp(-c / a, 0, 1); }
    else if (t > 1) { t = 1; s = THREE.MathUtils.clamp((b - c) / a, 0, 1); }
  }
  _c1.copy(p0).addScaledVector(_d1, s); _c2.copy(q0).addScaledVector(_d2, t);
  return [_c1.distanceTo(_c2), s];
}

const _a = new THREE.Vector3(), _b = new THREE.Vector3();
const UP = new THREE.Vector3(0, 1, 0);

export class Soldier {
  constructor({ route = null, s = 0, lat = 0, hold = false, face = 0, centurion = false, pos = null, speed = 1.25 }) {
    assets();
    this.route = route; this.s = s; this.lat = lat; this.hold = hold; this.dir = 1; this.walkSpeed = speed;
    this.centurion = centurion;
    this.alive = true; this.alertT = 0; this.pauseT = hold ? 0 : Math.random() * 2; this.phase = Math.random() * 6;
    this.seed = Math.random() * 100; this.yaw = face; this.targetYaw = face; this.moving = false;
    this.pos = new THREE.Vector3(); this.vel = new THREE.Vector3(); this.deadT = 0;
    this.fallAxis = new THREE.Vector3(1, 0, 0); this.debris = null;

    const mk = (g, m) => { const mesh = new THREE.Mesh(g, m); mesh.castShadow = true; return mesh; };
    const k = centurion ? 1 : 0;
    this.root = new THREE.Group();
    this.body = new THREE.Group(); this.body.position.y = 0.95;
    this.body.add(mk(GEO.body[k].matte, MAT.matte), mk(GEO.body[k].metal, MAT.metal));
    this.head = new THREE.Group(); this.head.position.y = 0.62;
    this.helmet = new THREE.Group();
    this.helmet.add(mk(GEO.helmet[k].metal, MAT.metal), mk(GEO.helmet[k].matte, MAT.matte));
    this.head.add(mk(GEO.head, MAT.matte), this.helmet);
    this.armL = mk(GEO.arm, MAT.matte); this.armL.position.set(-0.265, 0.5, 0);
    this.armR = mk(GEO.arm, MAT.matte); this.armR.position.set(0.265, 0.5, 0); this.armR.rotation.x = -0.45;
    this.shield = new THREE.Group();
    this.shield.add(mk(GEO.shield.matte, MAT.matte), mk(GEO.shield.metal, MAT.metal));
    this.shield.position.set(-0.33, 0.1, 0.14); this.shield.rotation.y = -0.55;
    this.pilum = new THREE.Group();
    this.pilum.add(mk(GEO.pilum.matte, MAT.matte), mk(GEO.pilum.metal, MAT.metal));
    this.pilum.position.set(0.3, -0.95, 0.27);
    this.body.add(this.head, this.armL, this.armR, this.shield, this.pilum);
    this.legL = mk(GEO.leg, MAT.matte); this.legL.position.set(-0.09, 0.93, 0);
    this.legR = mk(GEO.leg, MAT.matte); this.legR.position.set(0.09, 0.93, 0);
    this.root.add(this.body, this.legL, this.legR);
    if (pos) this.pos.copy(pos);
    if (route) this._place(0);
    this.root.position.copy(this.pos); this.root.rotation.y = this.yaw;
  }

  _place(dt) {
    const o = this.route.at(this.s);
    const nx = o.x + o.tz * this.lat, nz = o.z - o.tx * this.lat;
    const ny = this.route.yAt(nx, nz);
    if (dt > 0) this.vel.set((nx - this.pos.x) / dt, 0, (nz - this.pos.z) / dt); else this.vel.set(0, 0, 0);
    this.pos.set(nx, ny, nz);
    if (this.moving) this.targetYaw = Math.atan2(o.tx * this.dir, o.tz * this.dir);
  }

  alert(t) { if (this.alive) { this.alertT = Math.max(this.alertT, t); this.pauseT = Math.min(this.pauseT, 0.25 + Math.random() * 0.5); } }

  update(dt, time) {
    if (!this.alive) return this._dead(dt);
    this.alertT = Math.max(0, this.alertT - dt);
    const alerted = this.alertT > 0;
    this.moving = false;
    if (this.route && (!this.hold || alerted)) {
      if (this.pauseT > 0) this.pauseT -= dt;
      else {
        this.moving = true;
        const sp = alerted ? 3.3 + (this.seed % 1) * 0.8 : this.walkSpeed;
        this.s += this.dir * sp * dt;
        if (this.route.loop) this.s = (this.s + this.route.total) % this.route.total;
        else if (this.s <= 0 || this.s >= this.route.total) {
          this.s = THREE.MathUtils.clamp(this.s, 0, this.route.total);
          this.dir *= -1;
          this.pauseT = alerted ? 0.2 + Math.random() * 0.6 : 2 + Math.random() * 5;
        }
      }
      this._place(dt);
    } else this.vel.set(0, 0, 0);
    if (!this.moving) this.vel.set(0, 0, 0);

    let dy = this.targetYaw - this.yaw;
    dy = Math.atan2(Math.sin(dy), Math.cos(dy));
    this.yaw += dy * Math.min(1, dt * 5);
    this.root.position.copy(this.pos);
    this.root.rotation.y = this.yaw;

    const run = alerted ? 1.6 : 1;
    if (this.moving) {
      this.phase += dt * (alerted ? 11 : 5.6);
      const sw = Math.sin(this.phase) * 0.5 * run;
      this.legL.rotation.x = sw; this.legR.rotation.x = -sw;
      this.armL.rotation.x = -sw * 0.25 - 0.25;
      this.body.position.y = 0.95 + Math.abs(Math.cos(this.phase)) * 0.03;
      this.body.rotation.x = alerted ? 0.16 : 0.03;
      this.head.rotation.y *= 0.9;
    } else {
      this.legL.rotation.x *= 0.85; this.legR.rotation.x *= 0.85;
      this.armL.rotation.x = -0.25;
      this.body.position.y = 0.95 + Math.sin(time * 1.2 + this.seed) * 0.004;
      this.body.rotation.x = 0;
      const look = alerted ? Math.sin(time * 3.1 + this.seed) * 0.9 : Math.sin(time * 0.45 + this.seed) * 0.6 + Math.sin(time * 0.17 + this.seed * 2) * 0.3;
      this.head.rotation.y += (look - this.head.rotation.y) * Math.min(1, dt * 3);
    }
  }

  _dead(dt) {
    if (this.debris) {
      const d = this.debris;
      d.v.y -= 9.81 * dt;
      d.obj.position.addScaledVector(d.v, dt);
      d.obj.rotation.x += d.spin.x * dt; d.obj.rotation.z += d.spin.z * dt;
      if (d.obj.position.y < d.floor) { d.obj.position.y = d.floor; d.v.set(0, 0, 0); d.spin.set(0, 0, 0); this.debris = null; }
    }
    if (this.deadT >= 1.3) return;
    this.deadT = Math.min(1.3, this.deadT + dt);
    const t = Math.min(1, this.deadT / 0.85), e = t * t;
    this.root.quaternion.setFromAxisAngle(this.fallAxis, e * 1.5).multiply(_q.setFromAxisAngle(UP, this.yaw));
    this.root.position.copy(this.pos).addScaledVector(this.fallDir, e * 0.35);
    this.root.position.y += Math.sin(e * Math.PI) * 0.05 + e * 0.14;
    this.legL.rotation.x = e * 0.5; this.legR.rotation.x = -e * 0.25;
    this.armL.rotation.z = -e * 1.1; this.armR.rotation.z = e * 0.9; this.armR.rotation.x = -0.45 - e * 0.8;
    this.pilum.rotation.z = -e * 1.2; this.shield.rotation.x = e * 0.5;
    this.head.rotation.x = -e * 0.4;
  }

  // Returns { t, head } for the first hit along p0->p1, or null.
  hitTest(p0, p1, lead = 0) {
    if (!this.alive) return null;
    const px = this.pos.x + this.vel.x * lead, py = this.pos.y, pz = this.pos.z + this.vel.z * lead;
    let best = null;
    _a.set(px, py + 1.67, pz);
    let [d, t] = segSeg(p0, p1, _a, _a);
    if (d < 0.155) best = { t, head: true };
    _a.set(px, py + 0.98, pz); _b.set(px, py + 1.46, pz);
    [d, t] = segSeg(p0, p1, _a, _b);
    if (d < 0.25 && (!best || t < best.t - 0.02)) best = { t, head: false };
    _a.set(px, py + 0.1, pz); _b.set(px, py + 0.95, pz);
    [d, t] = segSeg(p0, p1, _a, _b);
    if (d < 0.17 && !best) best = { t, head: false };
    return best;
  }

  kill(dir, head, scene) {
    this.alive = false; this.vel.set(0, 0, 0);
    this.fallDir = new THREE.Vector3(dir.x, 0, dir.z).normalize();
    this.fallAxis.crossVectors(UP, this.fallDir).normalize();
    if (head && scene) {
      // the helmet is knocked clean off
      this.helmet.getWorldPosition(_a);
      this.head.remove(this.helmet); scene.add(this.helmet);
      this.helmet.position.copy(_a); this.helmet.rotation.y = this.yaw;
      this.debris = {
        obj: this.helmet, floor: this.pos.y - 0.05,
        v: new THREE.Vector3(dir.x * 3 + (Math.random() - 0.5) * 2, 4.2, dir.z * 3 + (Math.random() - 0.5) * 2),
        spin: new THREE.Vector3(8 + Math.random() * 6, 0, 5),
      };
    }
  }
}
const _q = new THREE.Quaternion();
