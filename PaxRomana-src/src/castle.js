// Château de Montvallon + the legion's marching camp, bridge and the ruined wall at the sniper's nest.
import * as THREE from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';
import { PLATEAU, WALL_H, WALK_Y, CAMP, SPAWN, BRIDGE, riverZ, U } from './config.js';
import { groundAt } from './terrain.js';
import { mulberry32 } from './noise.js';

class Builder {
  constructor() { this.parts = {}; this.rng = mulberry32(7); }
  add(key, g) {
    const n = g.index ? g.toNonIndexed() : g;
    for (const a of Object.keys(n.attributes)) if (!['position', 'normal', 'uv'].includes(a)) n.deleteAttribute(a);
    (this.parts[key] ||= []).push(n);
    return n;
  }
  // Box with world-scale UVs so masonry courses line up across pieces.
  box(key, w, h, d, x, y, z, ry = 0, tile = 4, rz = 0) {
    const g = new THREE.BoxGeometry(w, h, d);
    const p = g.attributes.position, n = g.attributes.normal, uv = g.attributes.uv;
    const off = this.rng() * tile;
    for (let i = 0; i < p.count; i++) {
      const px = p.getX(i), py = p.getY(i) + y, pz = p.getZ(i);
      if (Math.abs(n.getY(i)) > 0.5) uv.setXY(i, (px + off) / tile, (pz + off) / tile);
      else if (Math.abs(n.getX(i)) > 0.5) uv.setXY(i, (pz + off) / tile, py / tile);
      else uv.setXY(i, (px + off) / tile, py / tile);
    }
    if (rz) g.rotateZ(rz);
    if (ry) g.rotateY(ry);
    g.translate(x, y, z);
    return this.add(key, g);
  }
  cyl(key, rt, rb, h, x, y, z, seg = 28, tile = 4, open = false, ts = 0, tl = Math.PI * 2) {
    const g = new THREE.CylinderGeometry(rt, rb, h, seg, 1, open, ts, tl);
    const uv = g.attributes.uv;
    const su = Math.max(1, Math.round((tl * Math.max(rt, rb)) / tile)), sv = h / tile, vo = (y - h / 2) / tile;
    for (let i = 0; i < uv.count; i++) uv.setXY(i, uv.getX(i) * su, uv.getY(i) * sv + vo);
    g.translate(x, y, z);
    return this.add(key, g);
  }
  // triangular prism (gabled roof / tent). Ridge along Z unless ry given.
  prism(key, w, h, len, x, y, z, ry = 0, tile = 3) {
    const r = w / 1.732;
    const g = new THREE.CylinderGeometry(r, r, len, 3, 1, false);
    const uv = g.attributes.uv;
    for (let i = 0; i < uv.count; i++) { const u = uv.getX(i), v = uv.getY(i); uv.setXY(i, v * len / tile, u * 3 * w / tile); }
    g.rotateX(-Math.PI / 2);
    g.translate(0, r * 0.5, 0);
    g.scale(1, h / (r * 1.5), 1);
    if (ry) g.rotateY(ry);
    g.translate(x, y, z);
    return this.add(key, g);
  }
  build(mats, group) {
    const out = {};
    for (const k of Object.keys(this.parts)) {
      const m = new THREE.Mesh(mergeGeometries(this.parts[k], false), mats[k]);
      m.castShadow = m.receiveShadow = true;
      m.name = k;
      group.add(m); out[k] = m;
    }
    return out;
  }
}

function bannerMaterial(map, flag) {
  const m = new THREE.MeshStandardMaterial({ map, side: THREE.DoubleSide, roughness: 0.85 });
  m.onBeforeCompile = (s) => {
    s.uniforms.uTime = U.uTime;
    s.vertexShader = s.vertexShader
      .replace('#include <common>', '#include <common>\nuniform float uTime;')
      .replace('#include <begin_vertex>', flag
        ? '#include <begin_vertex>\nfloat pin = uv.x; transformed.z += sin(uv.x * 7.0 - uTime * 5.0 + uv.y * 2.0) * 0.28 * pin; transformed.y += sin(uv.x * 4.0 - uTime * 3.1) * 0.12 * pin;'
        : '#include <begin_vertex>\nfloat pin = 1.0 - uv.y; transformed.z += sin(uv.y * 8.0 - uTime * 2.3 + position.x * 1.5) * 0.16 * pin;');
  };
  return m;
}

export function buildCastle(tex) {
  const B = new Builder();
  const group = new THREE.Group();
  const banners = [];
  const top = WALK_Y;
  const hang = bannerMaterial(tex.banner, false), flagM = bannerMaterial(tex.banner, true);

  const slit = (x, y, z, ry, w = 0.28, h = 1.7) => B.box('dark', w, h, 0.3, x, y, z, ry);
  const win = (x, y, z, ry, w = 1.0, h = 2.3) => {
    B.box('dark', w, h, 0.36, x, y, z, ry);
    B.box('dark', w * 0.707, w * 0.707, 0.36, x, y + h / 2, z, ry, 4, Math.PI / 4);
    B.box('stone', w + 0.5, 0.22, 0.5, x, y - h / 2 - 0.1, z, ry);
  };

  // ---- curtain walls ----
  function wall(ax, az, bx, bz, nx, nz) {
    const len = Math.hypot(bx - ax, bz - az), cx = (ax + bx) / 2, cz = (az + bz) / 2;
    const ry = Math.atan2(-(bz - az), bx - ax), H = WALL_H + 12;
    B.box('stone', len, H, 3, cx, top - H / 2, cz, ry);
    B.box('stone', len, 4, 4.2, cx, PLATEAU - 1.5, cz, ry);                         // battered plinth
    B.box('stone', len, 0.45, 0.5, cx + nx * 1.7, top - 0.25, cz + nz * 1.7, ry);   // corbel table
    B.box('stone', len, 1.0, 0.55, cx + nx * 1.55, top + 0.5, cz + nz * 1.55, ry);  // parapet
    B.box('stone', len, 0.35, 0.4, cx - nx * 1.3, top + 0.17, cz - nz * 1.3, ry);   // inner kerb
    const n = Math.floor(len / 2.5);
    for (let i = 0; i < n; i++) {
      const t = (i + 0.5) / n;
      B.box('stone', 1.45, 0.95, 0.55, ax + (bx - ax) * t + nx * 1.55, top + 1.47, az + (bz - az) * t + nz * 1.55, ry);
    }
  }
  wall(-45, 35, -5, 35, 0, 1); wall(5, 35, 45, 35, 0, 1);
  wall(-45, -35, 45, -35, 0, -1);
  wall(45, -35, 45, 35, 1, 0); wall(-45, -35, -45, 35, -1, 0);

  // ---- round towers with machicolations and slate "poivrière" roofs ----
  function tower(x, z, r, hAbove, roofH, flag = true) {
    const y0 = PLATEAU - 14, H = hAbove + 14, yt = PLATEAU + hAbove;
    B.cyl('stone', r, r, H, x, y0 + H / 2, z, 32);
    B.cyl('stone', r, r + 1.1, 7, x, PLATEAU - 4, z, 32);
    const nc = Math.round(r * 3.6);
    for (let i = 0; i < nc; i++) {
      const a = (i / nc) * Math.PI * 2;
      B.box('stone', 0.5, 1.0, 0.9, x + Math.sin(a) * (r + 0.3), yt - 0.4, z + Math.cos(a) * (r + 0.3), a);
    }
    B.cyl('stone', r + 0.75, r + 0.75, 3.0, x, yt + 1.6, z, 32);
    const nw = Math.round(r * 1.6);
    for (let i = 0; i < nw; i++) {
      const a = (i / nw) * Math.PI * 2 + 0.2;
      B.box('dark', 0.6, 1.1, 0.3, x + Math.sin(a) * (r + 0.72), yt + 1.9, z + Math.cos(a) * (r + 0.72), a);
    }
    B.cyl('slate', r + 1.45, r + 1.45, 0.18, x, yt + 3.15, z, 32, 3);
    B.cyl('slate', 0.05, r + 1.35, roofH, x, yt + 3.2 + roofH / 2, z, 32, 3);
    B.cyl('gold', 0.05, 0.07, 2.6, x, yt + 3.2 + roofH + 1.2, z, 6);
    for (let lv = 0; lv < 3; lv++) for (let i = 0; i < 6; i++) {
      const a = (i / 6) * Math.PI * 2 + lv * 0.5;
      slit(x + Math.sin(a) * (r - 0.1), PLATEAU + 3 + lv * ((hAbove - 6) / 3), z + Math.cos(a) * (r - 0.1), a);
    }
    if (flag) {
      const f = new THREE.Mesh(new THREE.PlaneGeometry(2.6, 1.3, 10, 4), flagM);
      f.geometry.translate(1.3, 0, 0);
      f.position.set(x, yt + 3.2 + roofH + 1.7, z);
      f.rotation.y = -0.35;
      banners.push(f);
    }
  }
  for (const [x, z] of [[45, 35], [-45, 35], [45, -35], [-45, -35]]) tower(x, z, 7, 20, 13);
  tower(46.5, 0, 5, 17, 9, false); tower(-46.5, 0, 5, 17, 9, false); tower(0, -36.5, 5, 17, 9, false);

  // ---- gatehouse ----
  const gTop = PLATEAU + 17;
  tower(-10.5, 37.5, 5, 19, 10); tower(10.5, 37.5, 5, 19, 10);
  B.box('stone', 12, 17 + 12, 7, 0, gTop - 14.5, 36.5);
  B.box('stone', 12, 1.0, 0.55, 0, gTop + 0.5, 39.75);
  for (let i = 0; i < 4; i++) B.box('stone', 1.45, 0.95, 0.55, -4.1 + i * 2.75, gTop + 1.47, 39.75);
  B.box('stone', 12, 0.5, 0.6, 0, gTop - 0.3, 40.2);
  B.box('dark', 4.6, 5.2, 0.5, 0, PLATEAU + 2.6, 39.85);
  B.add('dark', new THREE.CylinderGeometry(2.3, 2.3, 0.5, 20).rotateX(Math.PI / 2).translate(0, PLATEAU + 5.2, 39.85));
  for (let i = -3; i <= 3; i++) B.box('iron', 0.1, 7.2, 0.1, i * 0.66, PLATEAU + 3.6, 40.15);
  for (let i = 0; i < 6; i++) B.box('iron', 4.5, 0.1, 0.1, 0, PLATEAU + 0.8 + i * 1.1, 40.15);
  slit(-3.5, PLATEAU + 11, 40, 0); slit(3.5, PLATEAU + 11, 40, 0);
  {
    const b = new THREE.Mesh(new THREE.PlaneGeometry(3, 6.4, 6, 14), hang);
    b.position.set(0, PLATEAU + 12.2, 40.35);
    banners.push(b);
  }

  // ---- keep (donjon) ----
  const KX = 0, KZ = -10, KW = 17, kTop = PLATEAU + 32;
  B.box('stone', KW, 36, KW, KX, kTop - 18, KZ);
  B.box('stone', KW + 1.2, 0.6, KW + 1.2, KX, kTop - 0.3, KZ);
  for (const [nx, nz] of [[0, 1], [0, -1], [1, 0], [-1, 0]]) {
    const ry = nx ? Math.PI / 2 : 0, o = KW / 2 + 0.25;
    B.box('stone', KW + 1.1, 1.0, 0.55, KX + nx * o, kTop + 0.5, KZ + nz * o, ry);
    for (let i = 0; i < 6; i++) {
      const t = -6.4 + i * 2.56;
      B.box('stone', 1.45, 0.95, 0.55, KX + nx * o + (nx ? 0 : t), kTop + 1.47, KZ + nz * o + (nz ? 0 : t), ry);
    }
    for (let lv = 0; lv < 3; lv++) for (const s of [-3.6, 3.6]) {
      win(KX + nx * (KW / 2) + (nx ? 0 : s), PLATEAU + 9 + lv * 7.5, KZ + nz * (KW / 2) + (nz ? 0 : s), nx ? nx * Math.PI / 2 : (nz > 0 ? 0 : Math.PI), 1.0, 2.5);
    }
  }
  B.add('slate', (() => {
    const g = new THREE.ConeGeometry(8.4, 9, 4, 1); g.rotateY(Math.PI / 4);
    const uv = g.attributes.uv; for (let i = 0; i < uv.count; i++) uv.setXY(i, uv.getX(i) * 16, uv.getY(i) * 3.4);
    return g.translate(KX, kTop + 4.5, KZ);
  })());
  B.cyl('gold', 0.05, 0.07, 3, KX, kTop + 10.4, KZ, 6);
  for (const [sx, sz] of [[1, 1], [1, -1], [-1, 1], [-1, -1]]) {
    const bx = KX + sx * KW / 2, bz = KZ + sz * KW / 2;
    B.cyl('stone', 1.9, 0.3, 3, bx, kTop - 5.5, bz, 16);
    B.cyl('stone', 1.9, 1.9, 7.5, bx, kTop - 0.25, bz, 16);
    B.cyl('slate', 0.04, 2.4, 5, bx, kTop + 6, bz, 16, 3);
    for (let i = 0; i < 3; i++) { const a = Math.atan2(sx, sz) + (i - 1) * 0.9; slit(bx + Math.sin(a) * 1.85, kTop + 1.2, bz + Math.cos(a) * 1.85, a, 0.22, 1.2); }
  }
  for (const s of [-4.5, 4.5]) {
    const b = new THREE.Mesh(new THREE.PlaneGeometry(2.6, 11, 6, 20), hang);
    b.position.set(KX + s, kTop - 9, KZ + KW / 2 + 0.45);
    banners.push(b);
  }
  {
    const f = new THREE.Mesh(new THREE.PlaneGeometry(4.2, 2.1, 12, 5), flagM);
    f.geometry.translate(2.1, 0, 0); f.position.set(KX, kTop + 11, KZ); f.rotation.y = -0.35;
    banners.push(f);
  }

  // ---- great hall, chapel ----
  B.box('stone', 13, 12, 46, -36.5, PLATEAU + 5, -5);
  B.prism('slate', 14.4, 7.5, 47.5, -36.5, PLATEAU + 11, -5);
  for (let i = 0; i < 6; i++) win(-29.95, PLATEAU + 5.5, -24 + i * 7.6, Math.PI / 2, 1.1, 3);
  B.box('stone', 1.6, 7, 1.6, -36.5, PLATEAU + 17, 8); B.box('stone', 1.6, 7, 1.6, -36.5, PLATEAU + 17, -18);
  B.box('stone', 12, 10, 20, 36, PLATEAU + 4, -22);
  B.prism('slate', 13.2, 7, 21.4, 36, PLATEAU + 9, -22);
  B.box('stone', 3.4, 20, 3.4, 36, PLATEAU + 9, -10.5);
  B.add('slate', new THREE.ConeGeometry(2.9, 8, 4, 1).rotateY(Math.PI / 4).translate(36, PLATEAU + 23, -10.5));
  B.box('gold', 0.1, 1.6, 0.1, 36, PLATEAU + 27.6, -10.5); B.box('gold', 0.8, 0.1, 0.1, 36, PLATEAU + 27.9, -10.5);
  for (let i = 0; i < 3; i++) win(36 + (i - 1) * 3.4, PLATEAU + 5, -11.95 + 0, 0, 0.8, 2.2);
  slit(36, PLATEAU + 16, -8.75, 0, 0.5, 1.6);

  // ---- bridge over the river ----
  {
    const bz = (BRIDGE.z0 + BRIDGE.z1) / 2, len = BRIDGE.z1 - BRIDGE.z0;
    B.box('stone', 5.4, 1.0, len, BRIDGE.x, BRIDGE.y - 0.5, bz);
    for (const s of [-1, 1]) B.box('stone', 0.45, 1.0, len, BRIDGE.x + s * 2.6, BRIDGE.y + 0.5, bz);
    for (let i = 0; i < 5; i++) {
      const z = BRIDGE.z0 + 8 + i * (len - 16) / 4;
      B.box('stone', 6.4, 9, 2.6, BRIDGE.x, BRIDGE.y - 5, z);
      B.box('stone', 2.2, 9, 2.2, BRIDGE.x - 3.4, BRIDGE.y - 5.6, z, Math.PI / 4);
      B.box('stone', 2.2, 9, 2.2, BRIDGE.x + 3.4, BRIDGE.y - 5.6, z, Math.PI / 4);
    }
  }

  // ---- ruined wall at the sniper's nest ----
  {
    const fx = -SPAWN.x, fz = -SPAWN.z, fl = Math.hypot(fx, fz), f = [fx / fl, fz / fl], r = [-f[1], f[0]];
    const ry = Math.atan2(-r[1], r[0]);
    const hs = [1.5, 1.0, 0.75, 0.45, 0, 0, 0.5, 0.8, 1.25];
    hs.forEach((h, k) => {
      if (!h) return;
      const x = SPAWN.x + f[0] * 3.4 + r[0] * (k - 4) * 1.35, z = SPAWN.z + f[1] * 3.4 + r[1] * (k - 4) * 1.35;
      B.box('stone', 1.4, h + 1.2, 0.8, x, groundAt(x, z) + h / 2 - 0.6, z, ry + (B.rng() - 0.5) * 0.08);
    });
    for (let i = 0; i < 5; i++) {
      const x = SPAWN.x + f[0] * (2 + B.rng() * 3) + r[0] * (B.rng() - 0.5) * 12, z = SPAWN.z + f[1] * (2 + B.rng() * 3) + r[1] * (B.rng() - 0.5) * 12;
      B.box('stone', 0.6, 0.4, 0.45, x, groundAt(x, z) + 0.1, z, B.rng() * 3, 4, B.rng() * 0.4);
    }
  }

  // ---- legion marching camp ----
  const smoke = [];
  {
    const cy = CAMP.h;
    for (let gx = -4; gx <= 4; gx++) for (let gz = -4; gz <= 4; gz++) {
      if (gx === 0 || gz === 0) continue;                  // via principalis / via praetoria
      const x = CAMP.x + gx * 8.2, z = CAMP.z + gz * 8.2;
      if (Math.hypot(x - CAMP.x, z - CAMP.z) > CAMP.r - 9) continue;
      B.prism('tent', 3.4, 2.4, 4.2, x, groundAt(x, z) - 0.05, z, (B.rng() - 0.5) * 0.12, 2);
    }
    // praetorium (command tent)
    B.prism('tentRed', 5.5, 3.6, 7, CAMP.x - 9, cy, CAMP.z - 9, Math.PI / 2, 2);
    // palisade with a south gate
    const n = Math.floor((2 * Math.PI * CAMP.r) / 0.52);
    for (let i = 0; i < n; i++) {
      const a = (i / n) * Math.PI * 2;
      if (Math.abs(a) < 0.085 || Math.abs(a - Math.PI * 2) < 0.085) continue;
      const x = CAMP.x + Math.sin(a) * CAMP.r, z = CAMP.z + Math.cos(a) * CAMP.r, g = groundAt(x, z);
      const h = 2.5 + B.rng() * 0.5, rr = 0.15 + B.rng() * 0.05;
      B.cyl('timber', rr, rr, h, x, g + h / 2 - 0.3, z, 6, 1.5);
      B.cyl('timber', 0.01, rr, 0.45, x, g + h - 0.08, z, 6, 1.5);
    }
    // watchtower
    const tx = CAMP.x + 26, tz = CAMP.z + 34, tg = groundAt(tx, tz);
    for (const [sx, sz] of [[1, 1], [1, -1], [-1, 1], [-1, -1]]) {
      B.box('timber', 0.3, 10.6, 0.3, tx + sx * 1.6, tg + 5.3, tz + sz * 1.6, 0, 1.5);
      B.box('timber', 0.14, 4.6, 0.14, tx + sx * 1.6, tg + 3.4, tz, 0, 1.5, sx * sz * 0.75);
    }
    B.box('timber', 3.9, 0.25, 3.9, tx, tg + 7, tz, 0, 1.5);
    for (const [sx, sz] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) B.box('timber', sx ? 0.12 : 3.7, 0.12, sz ? 0.12 : 3.7, tx + sx * 1.8, tg + 8.05, tz + sz * 1.8, 0, 1.5);
    B.add('timber', new THREE.ConeGeometry(3.2, 1.6, 4, 1).rotateY(Math.PI / 4).translate(tx, tg + 11.3, tz));
    // aquila standard
    B.cyl('timber', 0.05, 0.06, 5.2, CAMP.x, cy + 2.6, CAMP.z + 3, 6, 1.5);
    B.box('gold', 1.3, 0.12, 0.08, CAMP.x, cy + 4.3, CAMP.z + 3);
    B.add('gold', new THREE.SphereGeometry(0.22, 10, 8).scale(1, 1.2, 0.8).translate(CAMP.x, cy + 5.45, CAMP.z + 3));
    B.box('gold', 1.1, 0.14, 0.3, CAMP.x, cy + 5.55, CAMP.z + 3, 0, 4, 0.0);
    const v = new THREE.Mesh(new THREE.PlaneGeometry(1.2, 1.5, 4, 6), hang);
    v.position.set(CAMP.x, cy + 3.5, CAMP.z + 3.06);
    banners.push(v);
    // camp fires
    for (const [fx, fz] of [[6, 6], [-14, 12]]) {
      const x = CAMP.x + fx, z = CAMP.z + fz;
      for (let i = 0; i < 9; i++) { const a = i / 9 * 6.283; B.box('stone', 0.35, 0.3, 0.3, x + Math.sin(a) * 0.7, cy + 0.1, z + Math.cos(a) * 0.7, a); }
      B.add('ember', new THREE.SphereGeometry(0.42, 8, 6).scale(1, 0.45, 1).translate(x, cy + 0.15, z));
      smoke.push(new THREE.Vector3(x, cy + 0.5, z));
    }
  }
  smoke.push(new THREE.Vector3(-36.5, PLATEAU + 21, 8), new THREE.Vector3(-36.5, PLATEAU + 21, -18));

  const S = tex.stone;
  const mats = {
    stone: new THREE.MeshStandardMaterial({ map: S.map, normalMap: S.normalMap, normalScale: new THREE.Vector2(1.3, 1.3), roughness: 0.93 }),
    slate: new THREE.MeshStandardMaterial({ map: tex.slate.map, normalMap: tex.slate.normalMap, roughness: 0.5, metalness: 0.12 }),
    dark: new THREE.MeshStandardMaterial({ color: 0x0b0a09, roughness: 1 }),
    iron: new THREE.MeshStandardMaterial({ color: 0x1b1b1d, roughness: 0.6, metalness: 0.9 }),
    gold: new THREE.MeshStandardMaterial({ color: 0xd9b04a, roughness: 0.3, metalness: 1 }),
    timber: new THREE.MeshStandardMaterial({ map: tex.timber.map, normalMap: tex.timber.normalMap, roughness: 0.9 }),
    tent: new THREE.MeshStandardMaterial({ map: tex.leather.map, normalMap: tex.leather.normalMap, roughness: 0.85, side: THREE.DoubleSide }),
    tentRed: new THREE.MeshStandardMaterial({ map: tex.leather.map, color: 0xa8362a, roughness: 0.85 }),
    ember: new THREE.MeshStandardMaterial({ color: 0x1a0d08, emissive: 0xff5a12, emissiveIntensity: 2.5 }),
  };
  const meshes = B.build(mats, group);
  for (const b of banners) { b.castShadow = true; group.add(b); }
  const colliders = ['stone', 'slate', 'timber', 'tent', 'tentRed', 'iron'].map((k) => meshes[k]).filter(Boolean);
  return { group, colliders, smoke, keep: { x: KX, z: KZ, top: kTop, half: KW / 2 }, gateTop: gTop };
}
