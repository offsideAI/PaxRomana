// 2D player collision against static solids: circles (trees, boulders, towers), axis-aligned boxes
// (walls, tents) and rings with a gateway (the camp palisade).
//   circle: { x, z, r }      box: { x0, x1, z0, z1 }      ring: { x, z, r, gap }  (gap = half-angle of the gateway, facing +z)

export function resolve(pos, rad, solids) {
  let hit = false;
  for (let i = 0; i < solids.length; i++) {
    const s = solids[i];
    if (s.x0 !== undefined) {
      const a = pos.x - (s.x0 - rad), b = (s.x1 + rad) - pos.x, c = pos.z - (s.z0 - rad), d = (s.z1 + rad) - pos.z;
      if (a <= 0 || b <= 0 || c <= 0 || d <= 0) continue;
      const m = Math.min(a, b, c, d);
      if (m === a) pos.x = s.x0 - rad; else if (m === b) pos.x = s.x1 + rad; else if (m === c) pos.z = s.z0 - rad; else pos.z = s.z1 + rad;
      hit = true;
      continue;
    }
    const dx = pos.x - s.x, dz = pos.z - s.z;
    if (s.gap !== undefined) {
      const w = 0.35 + rad;
      if (Math.abs(dx) > s.r + w || Math.abs(dz) > s.r + w) continue;
      const d = Math.hypot(dx, dz);
      if (Math.abs(d - s.r) >= w || d < 1e-4) continue;
      if (Math.abs(Math.atan2(dx, dz)) < s.gap) continue;          // through the gateway
      const k = (s.r + (d > s.r ? w : -w)) / d;
      pos.x = s.x + dx * k; pos.z = s.z + dz * k;
      hit = true;
      continue;
    }
    const r = s.r + rad;
    if (Math.abs(dx) >= r || Math.abs(dz) >= r) continue;
    const d = Math.hypot(dx, dz);
    if (d >= r) continue;
    if (d < 1e-4) { pos.x = s.x + r; hit = true; continue; }
    pos.x = s.x + dx / d * r; pos.z = s.z + dz / d * r;
    hit = true;
  }
  return hit;
}
