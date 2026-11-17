// Procedural textures — everything is generated at load, no image assets.
import * as THREE from 'three';
import { fbm, hash2, smoothstep, clamp, mulberry32 } from './noise.js';

function canvas(w, h = w) {
  const c = document.createElement('canvas');
  c.width = w; c.height = h;
  return c;
}

function finish(c, srgb, repeat = true, aniso = 8) {
  const t = new THREE.CanvasTexture(c);
  if (repeat) t.wrapS = t.wrapT = THREE.RepeatWrapping;
  t.colorSpace = srgb ? THREE.SRGBColorSpace : THREE.NoColorSpace;
  t.anisotropy = aniso;
  return t;
}

// fn(u, v) -> [r, g, b, height] (0..1). Returns { map, normalMap }.
function gen(size, fn, nStrength = 2.5, wantNormal = true) {
  const c = canvas(size), ctx = c.getContext('2d');
  const img = ctx.createImageData(size, size), d = img.data;
  const hgt = new Float32Array(size * size);
  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      const o = fn(x / size, y / size);
      const i = (y * size + x) * 4;
      d[i] = clamp(o[0]) * 255; d[i + 1] = clamp(o[1]) * 255; d[i + 2] = clamp(o[2]) * 255; d[i + 3] = 255;
      hgt[y * size + x] = o[3];
    }
  }
  ctx.putImageData(img, 0, 0);
  const out = { map: finish(c, true) };
  if (wantNormal) {
    const nc = canvas(size), nctx = nc.getContext('2d');
    const nimg = nctx.createImageData(size, size), nd = nimg.data;
    for (let y = 0; y < size; y++) {
      const yu = ((y - 1 + size) % size) * size, yd = ((y + 1) % size) * size;
      for (let x = 0; x < size; x++) {
        const xl = (x - 1 + size) % size, xr = (x + 1) % size;
        const dx = (hgt[y * size + xr] - hgt[y * size + xl]) * nStrength;
        const dy = (hgt[yd + x] - hgt[yu + x]) * nStrength;
        const inv = 1 / Math.hypot(dx, dy, 1);
        const i = (y * size + x) * 4;
        nd[i] = (-dx * inv * 0.5 + 0.5) * 255;
        nd[i + 1] = (dy * inv * 0.5 + 0.5) * 255;
        nd[i + 2] = (inv * 0.5 + 0.5) * 255;
        nd[i + 3] = 255;
      }
    }
    nctx.putImageData(nimg, 0, 0);
    out.normalMap = finish(nc, false);
  }
  return out;
}

// Cream "tuffeau" ashlar limestone, as on Loire châteaux. Tile = 4 m.
function stone(size) {
  const rows = 12;
  return gen(size, (u, v) => {
    const row = Math.floor(v * rows), fv = v * rows - row;
    const cols = hash2(row, 3, 5) > 0.5 ? 7 : 5;
    const bu = u * cols + hash2(row, 7, 5);
    const colI = Math.floor(bu), fu = bu - colI;
    const id = hash2(((colI % cols) + cols) % cols, row, 9);
    const ex = Math.min(fu, 1 - fu) / cols * size, ey = Math.min(fv, 1 - fv) / rows * size;
    const e = Math.min(ex, ey);
    const mortar = smoothstep(1.2, 4.5, e);
    const grain = fbm(u * 48, v * 48, 3, 21, 48);
    const stain = fbm(u * 3, v * 5, 4, 31, 3);
    const drip = fbm(u * 24, v * 2, 2, 41, 2);
    let k = (0.82 + id * 0.3) * (0.86 + grain * 0.28) * (0.72 + stain * 0.4) * (0.9 + drip * 0.14);
    let r = 0.80 * k, g = 0.735 * k, b = 0.615 * k;
    if (id > 0.82) { r *= 0.9; g *= 0.92; b *= 0.98; }       // odd cooler block
    const m = 1 - mortar;
    r = r * mortar + 0.42 * m; g = g * mortar + 0.37 * m; b = b * mortar + 0.3 * m;
    const h = mortar * (0.75 + id * 0.25) + grain * 0.22 - (1 - smoothstep(0, 10, e)) * 0.1;
    return [r, g, b, h];
  }, 3.2);
}

function slate(size) {
  const rows = 12, cols = 9;
  return gen(size, (u, v) => {
    const row = Math.floor(v * rows), fv = v * rows - row;
    const bu = u * cols + (row % 2) * 0.5;
    const colI = Math.floor(bu), fu = bu - colI;
    const id = hash2(((colI % cols) + cols) % cols, row, 13);
    const gap = smoothstep(0.0, 0.06, Math.min(fu, 1 - fu));
    const lip = smoothstep(0.0, 0.12, 1 - fv);   // lower edge (canvas v grows downward => fv≈1 is bottom)
    const n = fbm(u * 30, v * 30, 3, 5, 30);
    const k = (0.7 + id * 0.55) * (0.85 + n * 0.3) * (0.55 + 0.45 * gap) * (0.6 + 0.4 * lip);
    return [0.23 * k, 0.265 * k, 0.33 * k, fv * 0.8 * gap + n * 0.1];
  }, 3.0);
}

function grass(size) {
  return gen(size, (u, v) => {
    const n = fbm(u * 8, v * 8, 4, 3, 8), s = fbm(u * 64, v * 64, 2, 8, 64);
    const k = 0.75 + s * 0.5;
    const t = smoothstep(0.3, 0.7, n);
    return [(0.21 + 0.13 * t) * k, (0.31 + 0.12 * t) * k, (0.10 + 0.05 * t) * k, s];
  }, 2, false);
}

function rock(size) {
  return gen(size, (u, v) => {
    const g = fbm(u * 6, v * 6, 5, 4, 6);
    const cr = Math.abs(fbm(u * 5 + g * 0.6, v * 5, 4, 14, 5) - 0.5) * 2;
    const crack = smoothstep(0.0, 0.07, cr);
    const k = (0.55 + 0.7 * g) * (0.55 + 0.45 * crack);
    return [0.6 * k, 0.56 * k, 0.5 * k, g * crack];
  }, 4);
}

function dirt(size) {
  return gen(size, (u, v) => {
    const n = fbm(u * 10, v * 10, 4, 6, 10), p = fbm(u * 70, v * 70, 2, 16, 70);
    const peb = smoothstep(0.72, 0.8, p);
    const k = 0.7 + n * 0.5;
    return [(0.47 + peb * 0.12) * k, (0.38 + peb * 0.12) * k, (0.26 + peb * 0.12) * k, n * 0.5 + peb * 0.5];
  }, 2, false);
}

function bark(size) {
  return gen(size, (u, v) => {
    const n = fbm(u * 10, v * 2, 4, 2, 2);
    const ridge = Math.abs(Math.sin((u * 14 + n * 2.5) * Math.PI));
    const k = 0.45 + ridge * 0.6 + fbm(u * 40, v * 40, 2, 9, 40) * 0.2;
    return [0.30 * k, 0.24 * k, 0.18 * k, ridge];
  }, 3);
}

function wood(size) {
  return gen(size, (u, v) => {
    const w = fbm(u * 2, v * 10, 4, 12, 2);
    const ring = 0.5 + 0.5 * Math.sin((u * 9 + w * 3.2) * Math.PI * 2);
    const fine = fbm(u * 4, v * 120, 2, 22, 4);
    const t = ring * 0.7 + fine * 0.3;
    return [0.16 + 0.22 * t, 0.085 + 0.12 * t, 0.04 + 0.06 * t, t * 0.3];
  }, 1.2);
}

function timber(size) {   // weathered planks for palisade / gates
  return gen(size, (u, v) => {
    const w = fbm(u * 3, v * 14, 4, 52, 3);
    const k = 0.6 + 0.5 * w + 0.25 * Math.abs(Math.sin(u * Math.PI * 8));
    return [0.36 * k, 0.27 * k, 0.18 * k, w];
  }, 2.5);
}

function leather(size) {   // tent hide
  return gen(size, (u, v) => {
    const n = fbm(u * 6, v * 6, 4, 33, 6), w = fbm(u * 90, v * 90, 2, 43, 90);
    const seam = smoothstep(0.0, 0.02, Math.abs(((u * 4) % 1) - 0.5));
    const k = (0.75 + n * 0.4) * (0.9 + w * 0.2) * (0.7 + 0.3 * seam);
    return [0.66 * k, 0.56 * k, 0.42 * k, n * 0.5 + w * 0.5];
  }, 2);
}

function waterNormal(size) {
  return gen(size, (u, v) => {
    const h = fbm(u * 8, v * 8, 4, 77, 8);
    return [0, 0, 0, h];
  }, 5).normalMap;
}

function leaves(kind, seed) {
  const S = 256, c = canvas(S), ctx = c.getContext('2d'), r = mulberry32(seed);
  ctx.clearRect(0, 0, S, S);
  if (kind === 'needle') {
    for (let i = 0; i < 30; i++) {
      const a = r() * 6.283, d = Math.sqrt(r()) * 88, cx = S / 2 + Math.cos(a) * d, cy = S / 2 + Math.sin(a) * d;
      for (let k = 0; k < 26; k++) {
        const an = r() * 6.283, len = 18 + r() * 24;
        ctx.strokeStyle = `hsl(${88 + r() * 30}, ${30 + r() * 20}%, ${13 + r() * 16}%)`;
        ctx.lineWidth = 1.6 + r();
        ctx.beginPath(); ctx.moveTo(cx, cy); ctx.lineTo(cx + Math.cos(an) * len, cy + Math.sin(an) * len); ctx.stroke();
      }
    }
  } else {
    const hue = kind === 'cypress' ? 105 : 78;
    for (let i = 0; i < 120; i++) {
      const a = r() * 6.283, d = Math.pow(r(), 0.6) * 104;
      const x = S / 2 + Math.cos(a) * d, y = S / 2 + Math.sin(a) * d;
      const light = kind === 'cypress' ? 10 + r() * 14 : 17 + r() * 20 + (i > 80 ? 6 : 0);
      ctx.fillStyle = `hsl(${hue + r() * 34}, ${38 + r() * 24}%, ${light}%)`;
      ctx.beginPath();
      ctx.ellipse(x, y, 9 + r() * 8, 4.5 + r() * 4, r() * 3.14, 0, 6.283);
      ctx.fill();
    }
  }
  const t = finish(c, true, false, 4);
  return t;
}

function banner() {
  const W = 256, H = 512, c = canvas(W, H), ctx = c.getContext('2d');
  const g = ctx.createLinearGradient(0, 0, W, 0);
  g.addColorStop(0, '#6d0f0c'); g.addColorStop(0.5, '#a31a14'); g.addColorStop(1, '#730f0c');
  ctx.fillStyle = g; ctx.fillRect(0, 0, W, H);
  ctx.strokeStyle = '#d9b24c'; ctx.lineWidth = 9; ctx.strokeRect(14, 14, W - 28, H - 28);
  ctx.lineWidth = 2; ctx.strokeRect(28, 28, W - 56, H - 56);
  ctx.fillStyle = '#d9b24c';
  // laurel wreath
  for (let i = 0; i < 26; i++) {
    const a = -Math.PI / 2 + (i / 26) * Math.PI * 2;
    if (Math.abs(a - Math.PI / 2) < 0.25) continue;
    const x = W / 2 + Math.cos(a) * 70, y = 190 + Math.sin(a) * 70;
    ctx.save(); ctx.translate(x, y); ctx.rotate(a + 1.1 * Math.sign(Math.cos(a)));
    ctx.beginPath(); ctx.ellipse(0, 0, 15, 6, 0, 0, 6.283); ctx.fill(); ctx.restore();
  }
  // eagle (stylised)
  ctx.beginPath();
  ctx.moveTo(W / 2, 150); ctx.lineTo(W / 2 + 48, 172); ctx.lineTo(W / 2 + 18, 186); ctx.lineTo(W / 2 + 10, 226);
  ctx.lineTo(W / 2, 212); ctx.lineTo(W / 2 - 10, 226); ctx.lineTo(W / 2 - 18, 186); ctx.lineTo(W / 2 - 48, 172);
  ctx.closePath(); ctx.fill();
  ctx.font = 'bold 62px "Times New Roman", Georgia, serif';
  ctx.textAlign = 'center';
  ctx.fillText('SPQR', W / 2, 350);
  ctx.fillRect(60, 372, W - 120, 4);
  for (let i = 0; i < 12; i++) ctx.fillRect(22 + i * 18.5, H - 40, 9, 30);
  // cloth weave noise
  const img = ctx.getImageData(0, 0, W, H), d = img.data;
  for (let i = 0; i < d.length; i += 4) {
    const n = 0.86 + Math.random() * 0.2;
    d[i] *= n; d[i + 1] *= n; d[i + 2] *= n;
  }
  ctx.putImageData(img, 0, 0);
  return finish(c, true, false);
}

export function makeTextures(size = 1024) {
  const half = size >> 1;
  return {
    stone: stone(size), slate: slate(half), grass: grass(half), rock: rock(half), dirt: dirt(half),
    bark: bark(256), wood: wood(half), timber: timber(256), leather: leather(256),
    waterNormal: waterNormal(half),
    leafOak: leaves('oak', 1), leafPine: leaves('needle', 2), leafCypress: leaves('cypress', 3),
    banner: banner(),
  };
}
