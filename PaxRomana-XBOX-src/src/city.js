// The skyline of future Moscow behind the Kremlin: glass towers with lit windows, beacon spires and an orbital
// tether rising out of the haze. Scenery only: it is beyond the playable valley and has no collision.
import * as THREE from 'three';
import { Builder } from './castle.js';
import { heightAt } from './terrain.js';
import { mulberry32 } from './noise.js';

function windowTexture() {
  const N = 8, S = 128, c = document.createElement('canvas');
  c.width = c.height = S;
  const ctx = c.getContext('2d'), rng = mulberry32(31), cell = S / N;
  ctx.fillStyle = '#000'; ctx.fillRect(0, 0, S, S);
  const tints = ['#ffd9a0', '#ffe9c8', '#9fe8ff', '#bfd4ff', '#ff9fd0'];
  for (let j = 0; j < N; j++) for (let i = 0; i < N; i++) {
    if (rng() < 0.42) continue;
    ctx.globalAlpha = 0.35 + rng() * 0.65;
    ctx.fillStyle = tints[Math.floor(rng() * (rng() < 0.75 ? 2 : tints.length))];
    ctx.fillRect(i * cell + 3, j * cell + 4, cell - 6, cell - 9);
  }
  ctx.globalAlpha = 1;
  const t = new THREE.CanvasTexture(c);
  t.wrapS = t.wrapT = THREE.RepeatWrapping; t.colorSpace = THREE.SRGBColorSpace; t.anisotropy = 4;
  return t;
}

export function buildCity() {
  const B = new Builder(), rng = mulberry32(2187), group = new THREE.Group();
  const TILE = 34;                                   // one texture repeat = 8 floors x 8 bays
  let n = 0, tries = 0;
  while (n < 85 && tries++ < 600) {
    // an arc north of the Kremlin, from west round to east, 520 - 1500 m out
    const a = -Math.PI * (0.06 + rng() * 0.88), r = 520 + Math.pow(rng(), 0.8) * 980;
    const x = Math.cos(a) * r * 1.25, z = Math.sin(a) * r - 60;
    const g = heightAt(x, z);
    if (g < 2 || g > 150) continue;
    const tall = rng() < 0.22, w = 26 + rng() * 34, d = 26 + rng() * 34, h = (tall ? 230 + rng() * 190 : 70 + rng() * 120);
    B.box('tower', w, h + 30, d, x, g + h / 2 - 15, z, rng() * 0.6, TILE);
    if (tall) {
      B.box('tower', w * 0.6, h * 0.22, d * 0.6, x, g + h + h * 0.11, z, 0, TILE);
      B.cyl('beaconPole', 0.7, 1.4, 46, x, g + h * 1.22 + 23, z, 6);
      B.add('beacon', new THREE.SphereGeometry(2.6, 8, 6).translate(x, g + h * 1.22 + 47, z));
    } else if (rng() < 0.5) B.box('strip', w + 0.6, 1.6, d + 0.6, x, g + h * (0.55 + rng() * 0.4), z, 0, TILE);
    n++;
  }
  const win = windowTexture();
  const mats = {
    tower: new THREE.MeshStandardMaterial({ color: 0x1b2433, roughness: 0.25, metalness: 0.75, emissive: 0xffffff, emissiveMap: win, emissiveIntensity: 2.4 }),
    beaconPole: new THREE.MeshStandardMaterial({ color: 0x22262e, roughness: 0.5, metalness: 0.8 }),
    beacon: new THREE.MeshBasicMaterial({ color: new THREE.Color(3.2, 0.25, 0.2), toneMapped: false }),
    strip: new THREE.MeshBasicMaterial({ color: new THREE.Color(0.35, 1.7, 2.3), toneMapped: false }),
  };
  const meshes = B.build(mats, group);
  for (const m of Object.values(meshes)) { m.castShadow = false; m.receiveShadow = false; }

  // orbital tether: a thread of light from the horizon into the sky
  const tether = new THREE.Mesh(new THREE.CylinderGeometry(3.5, 9, 9000, 8, 1, true).translate(0, 4500, 0),
    new THREE.MeshBasicMaterial({ color: new THREE.Color(0.5, 1.5, 2.4), toneMapped: false, fog: false, transparent: true, opacity: 0.85 }));
  tether.position.set(-420, 40, -1750);
  group.add(tether);
  return group;
}
