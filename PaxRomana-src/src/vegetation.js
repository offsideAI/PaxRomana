// Instanced grass meadow that follows the player, three tree species, boulders.
import * as THREE from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';
import { groundAt, macroAt, heightAt } from './terrain.js';
import { SPAWN, CAMP, INNER, ROAD, ROAD2, riverZ, U } from './config.js';
import { mulberry32, fbm, smoothstep, lerp } from './noise.js';

const FIX_NORMAL = (s) => {
  s.fragmentShader = s.fragmentShader.replace('#include <normal_fragment_maps>', '#include <normal_fragment_maps>\nnormal = normalize(vNormal);');
};

function grassGeometry() {
  const pos = [], idx = [];
  for (let b = 0; b < 3; b++) {
    const a = b * 2.1 + 0.3, ca = Math.cos(a), sa = Math.sin(a), ox = Math.cos(a * 1.7) * 0.07, oz = Math.sin(a * 1.7) * 0.07;
    const w = 0.055, pts = [[-w, 0, 0], [w, 0, 0], [-w * 0.7, 0.5, 0.06], [w * 0.7, 0.5, 0.06], [0, 1, 0.24]];
    const base = pos.length / 3;
    for (const [x, y, z] of pts) pos.push(x * ca - z * sa + ox, y, x * sa + z * ca + oz);
    idx.push(base, base + 1, base + 2, base + 1, base + 3, base + 2, base + 2, base + 3, base + 4);
  }
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
  g.setAttribute('normal', new THREE.Float32BufferAttribute(new Array(pos.length / 3).fill([0, 1, 0]).flat(), 3));
  g.setIndex(idx);
  return g;
}

const GC = 24, GN = 7;      // grass cell size (m) and cells per side of the window around the player

function buildGrass(count) {
  const mat = new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.8, side: THREE.DoubleSide });
  mat.onBeforeCompile = (s) => {
    s.uniforms.uTime = U.uTime;
    s.vertexShader = s.vertexShader
      .replace('#include <common>', '#include <common>\nuniform float uTime;\nvarying float vGh;')
      .replace('#include <begin_vertex>', `#include <begin_vertex>
        vGh = position.y;
        #ifdef USE_INSTANCING
          vec3 ip = instanceMatrix[3].xyz;
          float gust = sin(uTime * 0.7 + ip.x * 0.05 + ip.z * 0.03) * 0.5 + 0.5;
          float w = sin(uTime * 1.9 + ip.x * 0.33 + ip.z * 0.27) * 0.6 + sin(uTime * 3.7 + ip.x * 1.3 - ip.z * 0.9) * 0.25;
          float hh = position.y * position.y;
          transformed.x += (w * 0.2 + gust * 0.25) * hh;
          transformed.z += w * 0.12 * hh;
        #endif`);
    s.fragmentShader = s.fragmentShader
      .replace('#include <common>', '#include <common>\nvarying float vGh;')
      .replace('#include <color_fragment>', '#include <color_fragment>\ndiffuseColor.rgb *= mix(0.42, 1.35, vGh);');
    FIX_NORMAL(s);
  };
  // The meadow is a 7 x 7 window of cells that follows the player. Each slot is addressed toroidally, so walking
  // into a new row only refills the cells that scrolled out; a cell always regrows the same blades (seeded by its coords).
  const cap = Math.max(200, Math.round(count / 25)), geo = grassGeometry(), group = new THREE.Group();
  const m = new THREE.Matrix4(), q = new THREE.Quaternion(), p = new THREE.Vector3(), sc = new THREE.Vector3(), up = new THREE.Vector3(0, 1, 0);
  const col = new THREE.Color(), base = new THREE.Color().setRGB(0.3, 0.41, 0.15, THREE.SRGBColorSpace);
  const cells = [];
  for (let i = 0; i < GN * GN; i++) {
    const mesh = new THREE.InstancedMesh(geo, mat, cap);
    mesh.setColorAt(0, col);
    mesh.receiveShadow = true; mesh.visible = false;
    mesh.boundingSphere = new THREE.Sphere();
    group.add(mesh);
    cells.push({ mesh, cx: NaN, cz: NaN, n: 0 });
  }
  const mod = (a) => ((a % GN) + GN) % GN;
  function fill(cell, cx, cz) {
    const rng = mulberry32(((cx * 73856093) ^ (cz * 19349663)) >>> 0), mesh = cell.mesh;
    let n = 0, lo = 1e9, hi = -1e9;
    for (let i = 0; i < cap; i++) {
      const x = (cx + rng()) * GC, z = (cz + rng()) * GC, r1 = rng(), r2 = rng(), r3 = rng(), r4 = rng(), r5 = rng(), r6 = rng();
      const h = groundAt(x, z);
      if (h < 1.0) continue;                                                       // river bed and shore
      if (Math.abs(x) < 64 && Math.abs(z) < 64 && Math.hypot(x, z) < 60) continue;  // castle rock
      if (Math.hypot(x - CAMP.x, z - CAMP.z) < CAMP.r + 5) continue;                // trampled camp
      if (Math.hypot((groundAt(x + 0.8, z) - h) / 0.8, (groundAt(x, z + 0.8) - h) / 0.8) > 0.6) continue;   // bare rock
      if (x > -10 && x < 185 && z > 30 && z < 310 && (distToPath(x, z, ROAD) < 3 || distToPath(x, z, ROAD2) < 3)) continue;
      const patch = fbm(x * 0.09, z * 0.09, 2, 77);
      p.set(x, h - 0.03, z);
      q.setFromAxisAngle(up, r1 * 6.283);
      sc.set(1 + r2 * 0.8, (0.32 + r3 * 0.38) * (0.6 + patch * 1.3), 1 + r4 * 0.8);
      m.compose(p, q, sc);
      mesh.setMatrixAt(n, m);
      const t = smoothstep(0.3, 0.7, macroAt(x, z)), k = 0.8 + r5 * 0.45;
      col.setRGB(base.r * lerp(1.22, 0.74, t) * k, base.g * lerp(1.04, 1.0, t) * k, base.b * lerp(0.66, 0.78, t) * k);
      if (r6 < 0.05) col.setRGB(0.42 * k, 0.36 * k, 0.12 * k);     // dry straw
      mesh.setColorAt(n, col);
      n++; lo = Math.min(lo, h); hi = Math.max(hi, h);
    }
    mesh.instanceMatrix.needsUpdate = true; mesh.instanceColor.needsUpdate = true;
    mesh.boundingSphere.center.set((cx + 0.5) * GC, n ? (lo + hi) / 2 : 0, (cz + 0.5) * GC);
    mesh.boundingSphere.radius = Math.hypot(GC * 0.75, n ? (hi - lo) / 2 + 1.5 : 0);
    cell.cx = cx; cell.cz = cz; cell.n = n;
  }
  const stale = [];
  // Re-centre the meadow on (px, pz); refills at most `budget` cells per call, nearest first.
  function update(px, pz, budget = 1) {
    const pcx = Math.floor(px / GC), pcz = Math.floor(pz / GC), H = (GN - 1) / 2;
    stale.length = 0;
    for (let dz = -H; dz <= H; dz++) for (let dx = -H; dx <= H; dx++) {
      const cx = pcx + dx, cz = pcz + dz, cell = cells[mod(cz) * GN + mod(cx)];
      const d = Math.hypot((cx + 0.5) * GC - px, (cz + 0.5) * GC - pz);
      if (cell.cx !== cx || cell.cz !== cz) { cell.mesh.visible = false; stale.push({ cell, cx, cz, d }); continue; }
      cell.mesh.count = Math.floor(cell.n * (1 - 0.72 * smoothstep(28, 80, d)));     // thin out with distance
      cell.mesh.visible = cell.mesh.count > 0;
    }
    if (!stale.length) return;
    stale.sort((a, b) => a.d - b.d);
    for (let i = 0; i < stale.length && i < budget; i++) fill(stale[i].cell, stale[i].cx, stale[i].cz);
  }
  update(SPAWN.x, SPAWN.z, Infinity);
  update(SPAWN.x, SPAWN.z, 0);
  return { group, update };
}

// ---- trees ----
function cards(rng, centers, per, size, spread, canopy, squash = 1) {
  const geos = [], e = new THREE.Euler(), v = new THREE.Vector3();
  for (const c of centers) for (let i = 0; i < per; i++) {
    const s = size * (0.75 + rng() * 0.5);
    const g = new THREE.PlaneGeometry(s, s);
    e.set(rng() * 6.283, rng() * 6.283, rng() * 6.283);
    g.applyMatrix4(new THREE.Matrix4().makeRotationFromEuler(e));
    g.translate(c.x + (rng() - 0.5) * spread, c.y + (rng() - 0.5) * spread * squash, c.z + (rng() - 0.5) * spread);
    const p = g.attributes.position, n = g.attributes.normal;
    for (let k = 0; k < p.count; k++) {
      v.set(p.getX(k) - canopy.x, (p.getY(k) - canopy.y) * 1.2 + 0.6, p.getZ(k) - canopy.z).normalize();
      n.setXYZ(k, v.x, v.y, v.z);
    }
    geos.push(g);
  }
  return mergeGeometries(geos, false);
}

function limb(a, b, r0, r1, seg = 6) {
  const d = new THREE.Vector3().subVectors(b, a), len = d.length();
  const g = new THREE.CylinderGeometry(r1, r0, len, seg, 1, true);
  const uv = g.attributes.uv; for (let i = 0; i < uv.count; i++) uv.setY(i, uv.getY(i) * len * 0.5);
  g.applyQuaternion(new THREE.Quaternion().setFromUnitVectors(new THREE.Vector3(0, 1, 0), d.normalize()));
  g.translate((a.x + b.x) / 2, (a.y + b.y) / 2, (a.z + b.z) / 2);
  return g;
}

function species(kind, rng) {
  const V = (x, y, z) => new THREE.Vector3(x, y, z);
  const trunk = [], centers = [];
  let leaf;
  if (kind === 'oak') {
    trunk.push(limb(V(0, -0.5, 0), V(0.2, 3.6, 0.1), 0.42, 0.3, 8));
    for (let i = 0; i < 8; i++) {
      const a = (i / 8) * 6.283 + rng(), r = 1.6 + rng() * 2.2;
      const c = V(Math.cos(a) * r, 5.2 + rng() * 3.4, Math.sin(a) * r);
      centers.push(c);
      trunk.push(limb(V(0.2, 3.3, 0.1), c, 0.2, 0.05, 5));
    }
    centers.push(V(0, 8.6, 0));
    leaf = cards(rng, centers, 8, 2.7, 2.2, V(0, 6.5, 0));
  } else if (kind === 'pine') {       // umbrella / stone pine
    trunk.push(limb(V(0, -0.5, 0), V(0.5, 5, 0.2), 0.34, 0.26, 7), limb(V(0.5, 5, 0.2), V(0.2, 9.5, 0), 0.26, 0.18, 7));
    for (let i = 0; i < 9; i++) {
      const a = (i / 9) * 6.283, r = i ? 1.5 + rng() * 2.4 : 0;
      const c = V(0.2 + Math.cos(a) * r, 10.6 + rng() * 0.9, Math.sin(a) * r);
      centers.push(c);
      trunk.push(limb(V(0.2, 9.2, 0), c, 0.12, 0.04, 4));
    }
    leaf = cards(rng, centers, 7, 2.5, 1.9, V(0.2, 9.8, 0), 0.4);
  } else {                             // cypress
    trunk.push(limb(V(0, -0.5, 0), V(0, 3, 0), 0.2, 0.14, 6));
    for (let i = 0; i < 12; i++) centers.push(V(0, 1.6 + i * 1.0, 0));
    const geos = centers.map((c, i) => cards(rng, [c], 5, 1.9 - i * 0.09, (1.5 - i * 0.1), V(0, c.y - 1.5, 0), 0.8));
    leaf = mergeGeometries(geos, false);
  }
  return { trunk: mergeGeometries(trunk, false), leaf };
}

function distToPath(x, z, path) {
  let best = 1e9;
  for (let i = 0; i < path.length - 1; i++) {
    const ax = path[i][0], az = path[i][1], bx = path[i + 1][0], bz = path[i + 1][1];
    const dx = bx - ax, dz = bz - az, t = Math.max(0, Math.min(1, ((x - ax) * dx + (z - az) * dz) / (dx * dx + dz * dz)));
    best = Math.min(best, Math.hypot(x - ax - dx * t, z - az - dz * t));
  }
  return best;
}

export function buildVegetation(tex, { grassCount = 130000 } = {}) {
  const group = new THREE.Group();
  const rng = mulberry32(2024);
  const grass = buildGrass(grassCount);
  group.add(grass.group);

  const barkMat = new THREE.MeshStandardMaterial({ map: tex.bark.map, normalMap: tex.bark.normalMap, roughness: 0.95 });
  const leafMat = (map, color) => {
    const m = new THREE.MeshStandardMaterial({ map, color, alphaTest: 0.42, side: THREE.DoubleSide, roughness: 0.75 });
    m.onBeforeCompile = (s) => {
      s.uniforms.uTime = U.uTime;
      s.vertexShader = s.vertexShader
        .replace('#include <common>', '#include <common>\nuniform float uTime;')
        .replace('#include <begin_vertex>', `#include <begin_vertex>
          #ifdef USE_INSTANCING
            vec3 ip = instanceMatrix[3].xyz;
            float sw = sin(uTime * 1.3 + ip.x * 0.2 + position.y * 0.6) + sin(uTime * 2.9 + position.x * 2.0 + ip.z) * 0.4;
            transformed.x += sw * 0.012 * position.y; transformed.z += sw * 0.007 * position.y;
          #endif`);
      FIX_NORMAL(s);
    };
    return m;
  };
  const defs = {
    oak: { ...species('oak', rng), mat: leafMat(tex.leafOak, 0xe2ecc0), list: [] },
    pine: { ...species('pine', rng), mat: leafMat(tex.leafPine, 0xd8e6c8), list: [] },
    cypress: { ...species('cypress', rng), mat: leafMat(tex.leafCypress, 0xc6d8c0), list: [] },
  };
  const obstacles = [];
  const m4 = new THREE.Matrix4(), q = new THREE.Quaternion(), up = new THREE.Vector3(0, 1, 0);
  const place = (kind, x, z, s = 0.8 + rng() * 0.6, inner = true) => {
    const y = inner ? groundAt(x, z) : heightAt(x, z);
    q.setFromAxisAngle(up, rng() * 6.283);
    m4.compose(new THREE.Vector3(x, y, z), q, new THREE.Vector3(s, s * (0.9 + rng() * 0.25), s));
    defs[kind].list.push(m4.clone());
    if (inner) obstacles.push({ x, z, r: (kind === 'cypress' ? 0.3 : 0.5) * s });
  };
  const ok = (x, z) => {
    const h = groundAt(x, z);
    if (h < 1.8) return false;
    if (Math.hypot(x, z) < 118) return false;
    if (Math.hypot(x - CAMP.x, z - CAMP.z) < CAMP.r + 26) return false;
    if (Math.abs(z - riverZ(x)) < 20) return false;
    if (distToPath(x, z, ROAD) < 7 || distToPath(x, z, ROAD2) < 7) return false;
    if (x > -110 && x < 350 && z > 262 && z < SPAWN.z + 6) return false;    // keep sightlines from the ridge
    if (Math.hypot(x - SPAWN.x, z - SPAWN.z) < 7) return false;
    const e = 4; // slope reject
    if (Math.abs(groundAt(x + e, z) - groundAt(x - e, z)) + Math.abs(groundAt(x, z + e) - groundAt(x, z - e)) > 7) return false;
    return true;
  };
  // valley + hills inside the dense tile
  let tries = 0;
  while (defs.oak.list.length + defs.pine.list.length + defs.cypress.list.length < 1150 && tries++ < 40000) {
    const x = INNER.x0 + rng() * INNER.size, z = INNER.z0 + rng() * INNER.size;
    const forest = fbm(x * 0.006 + 7, z * 0.006 + 3, 3, 44);
    if (rng() > smoothstep(0.38, 0.62, forest) * 0.9 + 0.05) continue;
    if (!ok(x, z)) continue;
    const k = rng();
    place(k < 0.62 ? 'oak' : k < 0.86 ? 'pine' : 'cypress', x, z);
  }
  // cypress avenue along the road, pines framing the nest
  for (const road of [ROAD, ROAD2]) for (let i = 0; i < road.length - 1; i++) {
    const [ax, az] = road[i], [bx, bz] = road[i + 1], len = Math.hypot(bx - ax, bz - az), nx = -(bz - az) / len, nz = (bx - ax) / len;
    for (let d = 6; d < len; d += 13) for (const s of [-1, 1]) {
      const x = ax + (bx - ax) * d / len + nx * s * 5.5, z = az + (bz - az) * d / len + nz * s * 5.5;
      if (Math.hypot(x, z) < 62 || groundAt(x, z) < 2.2 || Math.hypot(x - CAMP.x, z - CAMP.z) < CAMP.r + 6) continue;
      if (rng() < 0.75) place('cypress', x, z, 0.9 + rng() * 0.5);
    }
  }
  place('oak', SPAWN.x + 9, SPAWN.z + 5, 1.5);
  place('pine', SPAWN.x - 13, SPAWN.z + 9, 1.3);
  place('pine', SPAWN.x - 20, SPAWN.z - 3, 1.1);
  place('cypress', SPAWN.x + 16, SPAWN.z - 6, 1.1);
  // far forests on the outer hills
  for (let i = 0; i < 1500; i++) {
    const a = rng() * 6.283, r = 620 + rng() * 1500, x = 60 + Math.cos(a) * r, z = 190 + Math.sin(a) * r;
    if (x > INNER.x0 && x < INNER.x0 + INNER.size && z > INNER.z0 && z < INNER.z0 + INNER.size) continue;
    if (fbm(x * 0.004, z * 0.004, 2, 45) < 0.45 || heightAt(x, z) < 3) continue;
    place(rng() < 0.7 ? 'oak' : 'pine', x, z, 1.2 + rng() * 0.9, false);
  }
  for (const k of Object.keys(defs)) {
    const d = defs[k], n = d.list.length;
    const t = new THREE.InstancedMesh(d.trunk, barkMat, n), l = new THREE.InstancedMesh(d.leaf, d.mat, n);
    d.list.forEach((mm, i) => { t.setMatrixAt(i, mm); l.setMatrixAt(i, mm); });
    t.castShadow = l.castShadow = true; l.receiveShadow = true;
    group.add(t, l);
  }

  // boulders
  const rg = new THREE.IcosahedronGeometry(1, 2), rp = rg.attributes.position;
  for (let i = 0; i < rp.count; i++) {
    const x = rp.getX(i), y = rp.getY(i), z = rp.getZ(i);
    const k = 0.72 + fbm(x * 1.7 + 5, y * 1.7 + z * 1.3, 3, 8) * 0.6;
    rp.setXYZ(i, x * k, y * k * 0.7, z * k);
  }
  rg.computeVertexNormals();
  const rocks = new THREE.InstancedMesh(rg, new THREE.MeshStandardMaterial({ map: tex.rock.map, normalMap: tex.rock.normalMap, roughness: 0.95, color: 0xd8d2c6 }), 420);
  for (let i = 0; i < 420; i++) {
    let x, z;
    if (i < 150) { const a = rng() * 6.283, r = 64 + rng() * 60; x = Math.cos(a) * r; z = Math.sin(a) * r; }
    else if (i < 230) { const a = rng() * 6.283, r = 4 + rng() * 80; x = SPAWN.x + Math.cos(a) * r; z = SPAWN.z + Math.sin(a) * r; }
    else { x = INNER.x0 + rng() * INNER.size; z = INNER.z0 + rng() * INNER.size; }
    const s = i < 230 ? 0.4 + rng() * 1.9 : 0.6 + rng() * 3;
    q.setFromEuler(new THREE.Euler(rng() * 0.6, rng() * 6.283, rng() * 0.6));
    m4.compose(new THREE.Vector3(x, groundAt(x, z) - s * 0.15, z), q, new THREE.Vector3(s, s * (0.6 + rng() * 0.6), s * (0.7 + rng() * 0.6)));
    rocks.setMatrixAt(i, m4);
    if (s >= 0.8) obstacles.push({ x, z, r: s * 0.8 });
  }
  rocks.castShadow = rocks.receiveShadow = true;
  group.add(rocks);
  return { group, obstacles, update: grass.update };
}
