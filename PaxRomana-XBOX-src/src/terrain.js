// Heightfield terrain: a dense inner tile around the valley + a coarse outer ring to the horizon.
import * as THREE from 'three';
import { fbm, smoothstep, lerp } from './noise.js';
import { PLATEAU, CAMP, INNER, riverZ, ROAD, ROAD2, U } from './config.js';

export function heightAt(x, z) {
  let h = (fbm(x * 0.0012 + 10, z * 0.0012 + 10, 5, 1) - 0.5) * 110 + 22;
  h += (fbm(x * 0.011, z * 0.011, 3, 2) - 0.5) * 5;
  const rv = Math.hypot(x - 60, z - 190);
  // open valley between the ridge and the castle
  const vm = 1 - smoothstep(330, 720, rv);
  h = lerp(h, 8 + (h - 22) * 0.12, vm);
  // river
  const rm = 1 - smoothstep(12, 46, Math.abs(z - riverZ(x)));
  h = lerp(h, -3.2, rm);
  // castle rock
  const d = Math.hypot(x, z);
  if (d < 150) {
    const hm = Math.pow(1 - smoothstep(58, 150, d), 0.8);
    const crag = (fbm(x * 0.06, z * 0.06, 3, 5) - 0.5) * 7 * smoothstep(60, 82, d) * hm;
    h = lerp(h, PLATEAU, hm) + crag;
  }
  // legion camp terrace
  const dc = Math.hypot(x - CAMP.x, z - CAMP.z);
  if (dc < 100) h = lerp(h, CAMP.h, 1 - smoothstep(58, 100, dc));
  // sniper's ridge
  const ex = (x - 120) / 260, ez = (z - 435) / 105;
  h += 72 * Math.exp(-1.2 * (ex * ex + ez * ez));
  // distant mountains
  const far = smoothstep(900, 2800, rv);
  if (far > 0) h += far * far * fbm(x * 0.0008 + 3, z * 0.0008 + 9, 4, 9) * 380;
  return h;
}

export const macroAt = (x, z) => fbm(x * 0.004 + 50, z * 0.004 + 50, 3, 11);

const N = INNER.n, CELL = INNER.size / INNER.n;
let grid = null;

export function groundAt(x, z) {
  if (!grid) return heightAt(x, z);
  const gx = (x - INNER.x0) / CELL, gz = (z - INNER.z0) / CELL;
  if (gx < 0 || gz < 0 || gx >= N || gz >= N) return heightAt(x, z);
  const i = Math.floor(gx), j = Math.floor(gz), fx = gx - i, fz = gz - j;
  const a = grid[j * (N + 1) + i], b = grid[j * (N + 1) + i + 1];
  const c = grid[(j + 1) * (N + 1) + i], d = grid[(j + 1) * (N + 1) + i + 1];
  return a + (b - a) * fx + (c - a) * fz + (a - b - c + d) * fx * fz;
}

function gridGeometry(x0, z0, cell, nx, nz, heightFn, skip) {
  const pos = new Float32Array((nx + 1) * (nz + 1) * 3);
  let k = 0;
  for (let j = 0; j <= nz; j++) for (let i = 0; i <= nx; i++) {
    const x = x0 + i * cell, z = z0 + j * cell;
    pos[k++] = x; pos[k++] = heightFn(x, z, i, j); pos[k++] = z;
  }
  const idx = [];
  for (let j = 0; j < nz; j++) for (let i = 0; i < nx; i++) {
    if (skip && skip(i, j)) continue;
    const a = j * (nx + 1) + i, b = a + nx + 1, c = a + 1, d = b + 1;
    idx.push(a, b, c, b, d, c);
  }
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.BufferAttribute(pos, 3));
  g.setIndex(new THREE.BufferAttribute(new Uint32Array(idx), 1));
  g.computeVertexNormals();
  return g;
}

function splatTexture() {
  const S = 1024, c = document.createElement('canvas');
  c.width = c.height = S;
  const ctx = c.getContext('2d');
  const img = ctx.createImageData(S, S), d = img.data;
  for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) {
    const wx = INNER.x0 + (x / S) * INNER.size, wz = INNER.z0 + (y / S) * INNER.size;
    const i = (y * S + x) * 4;
    d[i] = 0; d[i + 1] = macroAt(wx, wz) * 255; d[i + 2] = 0; d[i + 3] = 255;
  }
  ctx.putImageData(img, 0, 0);
  const P = (x, z) => [(x - INNER.x0) / INNER.size * S, (z - INNER.z0) / INNER.size * S];
  const m = S / INNER.size;
  ctx.globalCompositeOperation = 'lighter';
  const blob = (x, z, r, a) => {
    const [cx, cy] = P(x, z);
    const g = ctx.createRadialGradient(cx, cy, r * m * 0.55, cx, cy, r * m);
    g.addColorStop(0, `rgba(${a},0,0,1)`); g.addColorStop(1, 'rgba(0,0,0,1)');
    ctx.fillStyle = g; ctx.beginPath(); ctx.arc(cx, cy, r * m, 0, 6.283); ctx.fill();
  };
  blob(0, 0, 70, 210);
  blob(CAMP.x, CAMP.z, CAMP.r + 14, 190);
  ctx.lineCap = ctx.lineJoin = 'round';
  for (const road of [ROAD, ROAD2]) {
    for (const [w, a] of [[9, 60], [6, 90], [3.5, 110]]) {
      ctx.strokeStyle = `rgba(${a},0,0,1)`; ctx.lineWidth = w * m;
      ctx.beginPath();
      road.forEach(([x, z], i) => { const [cx, cy] = P(x, z); i ? ctx.lineTo(cx, cy) : ctx.moveTo(cx, cy); });
      ctx.stroke();
    }
  }
  const t = new THREE.CanvasTexture(c);
  t.colorSpace = THREE.NoColorSpace;
  return t;
}

export function buildTerrain(tex) {
  // inner tile heights (edges snapped to the coarse grid so the seam is watertight)
  const OC = 30, per = OC / CELL;
  grid = null;
  const g = new Float32Array((N + 1) * (N + 1));
  const edge = (fixed, t, alongX) => {
    const a = Math.floor(t / per) * per, b = Math.min(N, a + per), f = (t - a) / per;
    const p = (q) => alongX ? heightAt(INNER.x0 + q * CELL, fixed) : heightAt(fixed, INNER.z0 + q * CELL);
    return lerp(p(a), p(b), f);
  };
  for (let j = 0; j <= N; j++) for (let i = 0; i <= N; i++) {
    const x = INNER.x0 + i * CELL, z = INNER.z0 + j * CELL;
    let h;
    if (j === 0 || j === N) h = edge(z, i, true);
    else if (i === 0 || i === N) h = edge(x, j, false);
    else h = heightAt(x, z);
    g[j * (N + 1) + i] = h;
  }
  const innerGeo = gridGeometry(INNER.x0, INNER.z0, CELL, N, N, (x, z, i, j) => g[j * (N + 1) + i]);
  grid = g;

  const ON = 240, i0 = 100, span = INNER.size / OC;
  const outerGeo = gridGeometry(INNER.x0 - i0 * OC, INNER.z0 - i0 * OC, OC, ON, ON, (x, z) => heightAt(x, z),
    (i, j) => i >= i0 && i < i0 + span && j >= i0 && j < i0 + span);

  const mat = new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.96, metalness: 0 });
  const uni = {
    tGrass: { value: tex.grass.map }, tRock: { value: tex.rock.map }, tDirt: { value: tex.dirt.map },
    tSplat: { value: splatTexture() },
    uSplat: { value: new THREE.Vector3(INNER.x0, INNER.z0, INNER.size) },
  };
  mat.onBeforeCompile = (s) => {
    Object.assign(s.uniforms, uni);
    s.vertexShader = s.vertexShader
      .replace('#include <common>', '#include <common>\nvarying vec3 vWPos;\nvarying vec3 vWN;')
      .replace('#include <begin_vertex>', '#include <begin_vertex>\nvWPos = (modelMatrix * vec4(transformed, 1.0)).xyz;\nvWN = normal;');
    s.fragmentShader = s.fragmentShader
      .replace('#include <common>', `#include <common>
        varying vec3 vWPos; varying vec3 vWN;
        uniform sampler2D tGrass, tRock, tDirt, tSplat; uniform vec3 uSplat;`)
      .replace('#include <map_fragment>', `
        vec2 tp = vWPos.xz;
        vec3 gA = texture2D(tGrass, tp * 0.21).rgb;
        vec3 gB = texture2D(tGrass, tp * 0.033 + 0.37).rgb;
        vec3 gC = texture2D(tGrass, tp * 0.0041).rgb;
        vec3 grassC = (gA * 0.5 + gB * 0.6) * (0.6 + gC.g * 1.3);
        vec2 suv = (tp - uSplat.xy) / uSplat.z;
        float sIn = step(0.0, suv.x) * step(suv.x, 1.0) * step(0.0, suv.y) * step(suv.y, 1.0);
        vec4 sp = texture2D(tSplat, vec2(suv.x, 1.0 - suv.y));
        float macro = mix(0.5, sp.g, sIn);
        grassC *= mix(vec3(1.22, 1.04, 0.66), vec3(0.74, 1.0, 0.78), smoothstep(0.3, 0.7, macro));
        vec3 rockC = texture2D(tRock, tp * 0.085).rgb * (0.55 + 0.9 * texture2D(tRock, tp * 0.0093).g);
        grassC = mix(grassC, vec3(0.80, 0.84, 0.91) * (0.72 + gB.g * 0.55), 0.86);      // snow cover
        vec3 dirtC = texture2D(tDirt, tp * 0.3).rgb * (0.4 + 0.35 * gB.g);              // wet, trodden slush
        float slope = 1.0 - normalize(vWN).y;
        float rockW = smoothstep(0.13, 0.3, slope + (gB.g - 0.3) * 0.22);
        float shore = 1.0 - smoothstep(0.2, 1.8, vWPos.y);
        float dirtW = clamp(sp.r * sIn * 1.25 * (0.75 + gB.g) + shore, 0.0, 1.0);
        vec3 tc = mix(grassC, dirtC, dirtW);
        tc = mix(tc, rockC, rockW);
        diffuseColor.rgb = tc;`);
  };

  const inner = new THREE.Mesh(innerGeo, mat), outer = new THREE.Mesh(outerGeo, mat);
  inner.receiveShadow = true; outer.receiveShadow = true;
  const group = new THREE.Group();
  group.add(inner, outer);

  // river / lakes
  const wn = tex.waterNormal;
  wn.repeat.set(500, 500);
  const water = new THREE.Mesh(
    new THREE.PlaneGeometry(9000, 9000).rotateX(-Math.PI / 2),
    new THREE.MeshStandardMaterial({
      color: 0x0f1c2b, roughness: 0.07, metalness: 0.35, normalMap: wn,
      normalScale: new THREE.Vector2(0.35, 0.35), transparent: true, opacity: 0.93, envMapIntensity: 1.6,
    }));
  water.position.set(60, 0, 190);
  water.onBeforeRender = () => { wn.offset.set(U.uTime.value * 0.012, U.uTime.value * 0.004); };
  group.add(water);
  return group;
}
