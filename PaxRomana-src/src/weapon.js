// Bolt-action rifle: first-person view model, scope, ammunition, and real projectile ballistics
// (muzzle velocity, gravity drop, wind drift, time of flight).
import * as THREE from 'three';
import { MUZZLE_V, GRAVITY, WIND } from './config.js';
import { groundAt } from './terrain.js';
import { BASE_FOV } from './player.js';

const HIP = new THREE.Vector3(0.17, -0.18, -0.42), ADS = new THREE.Vector3(0, -0.0635, -0.2);
export const ZOOMS = [4, 6, 8, 12, 16];
const ZEROS = [100, 200, 300, 400, 500, 600];

function between(geo, a, b) {
  const d = new THREE.Vector3().subVectors(b, a);
  geo.applyQuaternion(new THREE.Quaternion().setFromUnitVectors(new THREE.Vector3(0, 1, 0), d.clone().normalize()));
  geo.translate((a.x + b.x) / 2, (a.y + b.y) / 2, (a.z + b.z) / 2);
  return geo;
}

function buildRifle(tex) {
  const g = new THREE.Group();
  const wood = new THREE.MeshStandardMaterial({ map: tex.wood.map, normalMap: tex.wood.normalMap, roughness: 0.42, color: 0xffffff });
  tex.wood.map.repeat.set(3, 3); tex.wood.normalMap.repeat.set(3, 3);
  const steel = new THREE.MeshStandardMaterial({ color: 0x1a1c20, roughness: 0.32, metalness: 1 });
  const black = new THREE.MeshStandardMaterial({ color: 0x0c0c0d, roughness: 0.5, metalness: 0.7 });
  const cloth = new THREE.MeshStandardMaterial({ color: 0x3a3f2a, roughness: 0.95 });
  const glove = new THREE.MeshStandardMaterial({ color: 0x2a1c12, roughness: 0.7 });
  const lens = new THREE.MeshStandardMaterial({ color: 0x203a66, roughness: 0.05, metalness: 1, emissive: 0x0a1630 });
  const M = (geo, mat) => { const m = new THREE.Mesh(geo, mat); g.add(m); return m; };
  const zc = (r0, r1, len, z, y = 0, seg = 16) => new THREE.CylinderGeometry(r1, r0, len, seg).rotateX(-Math.PI / 2).translate(0, y, z - len / 2);

  // walnut stock (side profile extruded)
  const s = new THREE.Shape();
  s.moveTo(-0.44, -0.115); s.lineTo(-0.44, 0.0); s.quadraticCurveTo(-0.3, 0.03, -0.2, 0.022); s.quadraticCurveTo(-0.12, 0.012, -0.08, -0.004);
  s.lineTo(0.36, -0.004); s.lineTo(0.37, -0.03); s.quadraticCurveTo(0.2, -0.05, 0.04, -0.047);
  s.quadraticCurveTo(-0.04, -0.05, -0.085, -0.105); s.quadraticCurveTo(-0.125, -0.115, -0.15, -0.075); s.quadraticCurveTo(-0.3, -0.09, -0.44, -0.115);
  const st = new THREE.ExtrudeGeometry(s, { depth: 0.036, bevelEnabled: true, bevelSize: 0.007, bevelThickness: 0.007, bevelSegments: 3, curveSegments: 10 });
  st.translate(0, 0, -0.018); st.rotateY(Math.PI / 2);
  M(st, wood);
  M(zc(0.018, 0.018, 0.24, 0.1, 0.014), steel);            // receiver
  M(zc(0.012, 0.0095, 0.66, -0.14, 0.014), steel);         // barrel
  M(zc(0.0125, 0.0125, 0.03, -0.77, 0.014), black);        // crown
  M(new THREE.BoxGeometry(0.03, 0.04, 0.07).translate(0, -0.06, -0.01), black);   // magazine
  M(new THREE.TorusGeometry(0.024, 0.003, 6, 14, Math.PI).rotateY(Math.PI / 2).rotateX(Math.PI).translate(0, -0.05, 0.075), steel);
  // scope
  const sy = 0.0635;
  M(zc(0.0145, 0.0145, 0.3, 0.1, sy), black);
  M(zc(0.0145, 0.026, 0.05, -0.2, sy), black); M(zc(0.026, 0.026, 0.07, -0.25, sy), black);
  M(zc(0.021, 0.0145, 0.03, 0.13, sy), black); M(zc(0.021, 0.021, 0.05, 0.18, sy), black);
  M(new THREE.CylinderGeometry(0.011, 0.011, 0.028, 12).translate(0, sy + 0.02, -0.05), black);
  M(new THREE.CylinderGeometry(0.011, 0.011, 0.028, 12).rotateZ(Math.PI / 2).translate(0.02, sy, -0.05), black);
  M(new THREE.BoxGeometry(0.022, 0.034, 0.02).translate(0, 0.04, 0.02), steel); M(new THREE.BoxGeometry(0.022, 0.034, 0.02).translate(0, 0.04, -0.12), steel);
  M(new THREE.CircleGeometry(0.0245, 20).rotateY(Math.PI).translate(0, sy, -0.318), lens);
  // bolt
  const bolt = new THREE.Group(); bolt.position.set(0, 0.014, 0.07);
  bolt.add(new THREE.Mesh(new THREE.CylinderGeometry(0.0045, 0.0045, 0.05, 8).rotateZ(Math.PI / 2).translate(0.035, 0, 0), steel));
  bolt.add(new THREE.Mesh(new THREE.SphereGeometry(0.011, 10, 8).translate(0.062, 0, 0), black));
  g.add(bolt);
  // arms
  const V = (x, y, z) => new THREE.Vector3(x, y, z);
  M(between(new THREE.CapsuleGeometry(0.04, 0.4, 4, 10), V(-0.2, -0.26, 0.22), V(-0.02, -0.07, -0.16)), cloth);
  M(new THREE.SphereGeometry(0.042, 12, 10).scale(1.1, 0.8, 1.5).translate(-0.012, -0.055, -0.19), glove);
  M(between(new THREE.CapsuleGeometry(0.042, 0.36, 4, 10), V(0.16, -0.3, 0.42), V(0.03, -0.1, 0.09)), cloth);
  M(new THREE.SphereGeometry(0.04, 12, 10).scale(1, 1.25, 1.1).translate(0.012, -0.075, 0.07), glove);
  // muzzle flash
  const flash = new THREE.Mesh(new THREE.SphereGeometry(0.06, 8, 6).scale(1, 1, 2.6), new THREE.MeshBasicMaterial({ color: 0xffd9a0, transparent: true, opacity: 0.9, fog: false, toneMapped: false }));
  flash.position.set(0, 0.014, -0.9); flash.visible = false; g.add(flash);
  g.traverse((o) => { o.frustumCulled = false; });
  return { group: g, bolt, flash };
}

const _p0 = new THREE.Vector3(), _p1 = new THREE.Vector3(), _d = new THREE.Vector3(), _n = new THREE.Vector3(), _v = new THREE.Vector3();

export class Weapon {
  constructor(scene, camera, player, world, tex) {
    this.scene = scene; this.camera = camera; this.player = player; this.world = world;
    const r = buildRifle(tex);
    this.model = r.group; this.boltMesh = r.bolt; this.flash = r.flash;
    camera.add(this.model);
    this.light = new THREE.PointLight(0xffc27a, 0, 12); camera.add(this.light);
    this.ads = 0; this.wantAds = false; this.zoomI = 2; this.zeroI = 3;
    this.mag = 5; this.reserve = 40; this.cool = 0; this.reloadT = 0; this.recoil = 0; this.flashT = 0; this.scopeFlash = 0;
    this.bullets = []; this.ray = new THREE.Raycaster();
    this.stats = { shots: 0, hits: 0, heads: 0, longest: 0 };
    const bg = new THREE.CylinderGeometry(0.004, 0.012, 0.09, 10).rotateX(-Math.PI / 2);
    this.bulletMat = new THREE.MeshStandardMaterial({ color: 0xc8793a, roughness: 0.25, metalness: 1 });
    this.bulletGeo = bg;
    this.trailGeo = new THREE.CylinderGeometry(0.012, 0.03, 1, 6, 1, true).rotateX(-Math.PI / 2).translate(0, 0, 0.5);
    this.trailMat = new THREE.MeshBasicMaterial({ color: 0xfff1d8, transparent: true, opacity: 0.22, depthWrite: false, fog: false });
  }

  get zoom() { return ZOOMS[this.zoomI]; }
  get zero() { return ZEROS[this.zeroI]; }
  get scoped() { return this.ads > 0.85; }
  cycleZoom(d) { this.zoomI = THREE.MathUtils.clamp(this.zoomI + d, 0, ZOOMS.length - 1); }
  cycleZero(d) { this.zeroI = THREE.MathUtils.clamp(this.zeroI + d, 0, ZEROS.length - 1); }

  reload() {
    if (this.reloadT > 0 || this.mag === 5 || this.reserve === 0) return;
    this.reloadT = 2.6; this.wantAds = false; this.world.audio.reload();
  }

  // Returns the bullet if a round was fired.
  fire() {
    if (this.cool > 0 || this.reloadT > 0) return null;
    if (this.mag === 0) { this.world.audio.dry(); this.cool = 0.3; if (this.reserve) this.reload(); return null; }
    this.mag--; this.cool = 1.25; this.stats.shots++;
    const cam = this.camera;
    cam.updateMatrixWorld();
    const dir = cam.getWorldDirection(new THREE.Vector3());
    const up = new THREE.Vector3(0, 1, 0).applyQuaternion(cam.quaternion), right = new THREE.Vector3(1, 0, 0).applyQuaternion(cam.quaternion);
    if (!this.scoped) { const a = Math.random() * 6.283, s = Math.random() * 0.022; dir.addScaledVector(right, Math.cos(a) * s).addScaledVector(up, Math.sin(a) * s).normalize(); }
    // sight zero: launch slightly high so the round crosses the line of sight at the zero range
    const tz = this.zero / MUZZLE_V, elev = Math.atan2(0.5 * GRAVITY * tz * tz, this.zero);
    dir.multiplyScalar(Math.cos(elev)).addScaledVector(up, Math.sin(elev)).normalize();
    const b = {
      pos: cam.position.clone().addScaledVector(dir, 0.6), vel: dir.clone().multiplyScalar(MUZZLE_V), origin: cam.position.clone(),
      t: 0, done: false, result: null, cam: false,
      mesh: new THREE.Mesh(this.bulletGeo, this.bulletMat), trail: new THREE.Mesh(this.trailGeo, this.trailMat),
    };
    b.mesh.visible = false; b.trail.frustumCulled = false;
    this.scene.add(b.mesh, b.trail);
    this.bullets.push(b);
    this.recoil = 1; this.flashT = 0.06; this.scopeFlash = 1;
    this.player.kick += this.scoped ? 0.012 : 0.05; this.player.kickYaw += (Math.random() - 0.5) * 0.008;
    this.world.audio.shot(); this.world.audio.bolt(0.45);
    this.world.legion.alertAround(cam.position, 0, 0, 3);
    return b;
  }

  // Dry-run the trajectory against (velocity-led) soldiers and the castle to decide on a kill-cam.
  predict(b) {
    const p = b.pos.clone(), v = b.vel.clone(), q = new THREE.Vector3(), dt = 1 / 200;
    let hit = null;
    for (let t = 0; t < 1.6 && !hit; t += dt) {
      q.copy(p);
      v.y -= GRAVITY * dt; v.addScaledVector(WIND, dt);
      p.addScaledVector(v, dt);
      if (p.y < groundAt(p.x, p.z)) return null;
      for (const s of this.world.legion.soldiers) {
        if (!s.alive || Math.abs(s.pos.x - p.x) + Math.abs(s.pos.z - p.z) > 12) continue;
        const h = s.hitTest(q, p, t);
        if (h) { hit = { soldier: s, head: h.head, point: q.clone().lerp(p, h.t), t }; break; }
      }
    }
    if (!hit) return null;
    // masonry in the way?
    const steps = 5; q.copy(b.pos); const vv = b.vel.clone(), a = new THREE.Vector3().copy(b.pos), st = hit.t / steps;
    for (let i = 0; i < steps; i++) {
      vv.y -= GRAVITY * st; q.addScaledVector(vv, st);
      _d.subVectors(q, a); const len = _d.length() - (i === steps - 1 ? 0.6 : 0);
      this.ray.set(a, _d.normalize()); this.ray.far = len;
      if (this.ray.intersectObjects(this.world.colliders, false).length) return null;
      a.copy(q);
    }
    return hit;
  }

  _step(b, dt) {
    const W = this.world;
    _p0.copy(b.pos);
    b.vel.y -= GRAVITY * dt; b.vel.addScaledVector(WIND, dt);
    _p1.copy(b.pos).addScaledVector(b.vel, dt);
    _d.subVectors(_p1, _p0); const len = _d.length(); _d.divideScalar(len);
    let best = 2, res = null;
    // soldiers
    for (const s of W.legion.soldiers) {
      if (!s.alive || Math.abs(s.pos.x - _p0.x) + Math.abs(s.pos.z - _p0.z) > len + 6) continue;
      const h = s.hitTest(_p0, _p1);
      if (h && h.t < best) { best = h.t; res = { type: 'soldier', soldier: s, head: h.head }; }
    }
    // masonry / timber
    this.ray.set(_p0, _d); this.ray.far = len;
    const hits = this.ray.intersectObjects(W.colliders, false);
    if (hits.length && hits[0].distance / len < best) {
      best = hits[0].distance / len;
      _n.copy(hits[0].face.normal).transformDirection(hits[0].object.matrixWorld);
      const nm = hits[0].object.name;
      res = { type: nm === 'timber' || nm.startsWith('tent') ? 'wood' : 'stone', normal: _n.clone() };
    }
    // ground / water
    const steps = Math.max(1, Math.ceil(len / 2));
    for (let i = 1; i <= steps; i++) {
      const t = i / steps; if (t >= best) break;
      _v.copy(_p0).addScaledVector(_d, len * t);
      const gy = groundAt(_v.x, _v.z);
      if (_v.y < Math.max(gy, 0)) { best = Math.max(0, t - 0.5 / steps); res = { type: gy < 0 ? 'water' : 'dirt', normal: new THREE.Vector3(0, 1, 0) }; break; }
    }
    b.t += dt;
    if (!res) { b.pos.copy(_p1); if (b.t > 4) { b.done = true; b.result = { type: 'none' }; } return; }
    b.pos.copy(_p0).addScaledVector(_d, len * best);
    b.done = true; b.result = res; res.point = b.pos.clone();
    const dist = b.pos.distanceTo(b.origin), delay = b.cam ? 0 : dist / 343;
    res.dist = dist;
    if (res.type === 'soldier') {
      const dir = b.vel.clone().normalize();
      W.fx.blood(b.pos, dir);
      W.legion.kill(res.soldier, dir, res.head);
      this.stats.hits++; if (res.head) this.stats.heads++; this.stats.longest = Math.max(this.stats.longest, dist);
      W.audio.hit(res.head); W.audio.impact('flesh', delay, dist);
      W.onKill && W.onKill(res);
    } else {
      if (res.type === 'water') W.fx.splash(b.pos); else W.fx.dust(b.pos, res.normal, res.type);
      W.audio.impact(res.type === 'stone' ? 'stone' : 'dirt', delay, dist);
      W.legion.alertAround(b.pos, 9, 9, 7);
      W.onMiss && W.onMiss(res);
    }
  }

  // dtReal drives the view model; dt (time-scaled) drives projectiles.
  update(dtReal, dt, visible) {
    const P = this.player;
    // projectiles
    for (const b of this.bullets) {
      if (!b.done) {
        let rem = dt;
        while (rem > 1e-6 && !b.done) { const h = Math.min(rem, 1 / 120); this._step(b, h); rem -= h; }
      }
      b.mesh.position.copy(b.pos);
      _v.copy(b.pos).add(b.vel); b.mesh.lookAt(_v);
      b.mesh.visible = b.cam && !b.done;
      b.mesh.scale.setScalar(b.cam ? 2.2 : 1);
      b.trail.position.copy(b.pos); b.trail.quaternion.copy(b.mesh.quaternion);
      const tl = Math.min(b.cam ? 5 : 26, b.pos.distanceTo(b.origin) * 0.5);
      b.trail.scale.set(b.cam ? 0.6 : 2.2, b.cam ? 0.6 : 2.2, Math.max(0.01, tl));
      if (b.done) { b.fade = (b.fade ?? 0.25) - dtReal; b.trail.visible = false; }
    }
    this.bullets = this.bullets.filter((b) => { if (b.done && !b.cam) { this.scene.remove(b.mesh, b.trail); return false; } return true; });

    // state
    this.cool = Math.max(0, this.cool - dtReal);
    if (this.reloadT > 0) {
      this.reloadT -= dtReal;
      if (this.reloadT <= 0) { const n = Math.min(5 - this.mag, this.reserve); this.mag += n; this.reserve -= n; }
    }
    const want = this.wantAds && this.reloadT <= 0 && !P.sprinting ? 1 : 0;
    this.ads += (want - this.ads) * Math.min(1, dtReal * 11);
    if (Math.abs(want - this.ads) < 0.003) this.ads = want;
    P.scoped = this.scoped; P.zoom = this.zoom;

    const sFov = THREE.MathUtils.radToDeg(2 * Math.atan(Math.tan(THREE.MathUtils.degToRad(BASE_FOV / 2)) / this.zoom));
    const k = THREE.MathUtils.smoothstep(this.ads, 0.55, 0.9);
    const fov = THREE.MathUtils.lerp(BASE_FOV, sFov, k), near = this.scoped ? 1.4 : 0.07;
    if (Math.abs(fov - this.camera.fov) > 0.001 || near !== this.camera.near) { this.camera.fov = fov; this.camera.near = near; this.camera.updateProjectionMatrix(); }

    // view model
    this.recoil *= Math.exp(-dtReal * 7);
    this.scopeFlash *= Math.exp(-dtReal * 9);
    const m = this.model, t = P.t;
    m.visible = visible && this.ads < 0.8;
    m.position.lerpVectors(HIP, ADS, this.ads);
    m.position.x += Math.cos(P.bob) * 0.006 * P.moveAmt * (1 - this.ads) + Math.sin(t * 0.8) * 0.0015;
    m.position.y += Math.sin(P.bob * 2) * 0.006 * P.moveAmt * (1 - this.ads) + Math.sin(t * 1.1) * 0.002 - (P.sprinting ? 0.05 : 0);
    m.position.z += this.recoil * 0.07;
    m.rotation.set(this.recoil * 0.09 - (P.sprinting ? 0.35 : 0), (P.sprinting ? 0.5 : 0) + (1 - this.ads) * 0.02, 0);
    if (this.reloadT > 0) { const u = Math.sin(Math.min(1, (2.6 - this.reloadT) / 2.6) * Math.PI); m.rotation.x -= u * 0.6; m.rotation.z = u * 0.5; m.position.y -= u * 0.06; }
    // bolt cycle
    const c = 1.25 - this.cool;
    let lift = 0, back = 0;
    if (this.cool > 0 && c > 0.4 && c < 1.1) { const u = (c - 0.4) / 0.7; lift = Math.sin(Math.min(1, u * 3) * Math.PI / 2) * (u < 0.75 ? 1 : (1 - u) * 4); back = Math.sin(THREE.MathUtils.clamp((u - 0.15) / 0.7, 0, 1) * Math.PI); }
    this.boltMesh.rotation.z = lift * 1.1; this.boltMesh.position.z = 0.07 + back * 0.07;
    this.flashT -= dtReal;
    this.flash.visible = this.flashT > 0; this.light.intensity = this.flashT > 0 ? 30 : 0;
    if (this.flash.visible) this.flash.rotation.z = Math.random() * 6;
  }
}
