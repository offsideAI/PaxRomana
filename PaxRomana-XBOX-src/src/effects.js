// Particles (dust, chips, blood mist, water, smoke, explosions), falling snow and a flock of birds over the valley.
import * as THREE from 'three';

const MAX = 1600, SNOW_R = 26;

export class FX {
  constructor(scene) {
    this.n = 0;
    this.pos = new Float32Array(MAX * 3); this.col = new Float32Array(MAX * 4); this.size = new Float32Array(MAX);
    this.p = [];
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.BufferAttribute(this.pos, 3).setUsage(THREE.DynamicDrawUsage));
    g.setAttribute('aColor', new THREE.BufferAttribute(this.col, 4).setUsage(THREE.DynamicDrawUsage));
    g.setAttribute('aSize', new THREE.BufferAttribute(this.size, 1).setUsage(THREE.DynamicDrawUsage));
    this.mat = new THREE.ShaderMaterial({
      transparent: true, depthWrite: false,
      uniforms: { uScale: { value: 800 } },
      vertexShader: `
        attribute vec4 aColor; attribute float aSize; uniform float uScale; varying vec4 vC;
        void main() { vC = aColor; vec4 mv = modelViewMatrix * vec4(position, 1.0);
          gl_PointSize = min(512.0, aSize * uScale / max(0.1, -mv.z)); gl_Position = projectionMatrix * mv; }`,
      fragmentShader: `
        varying vec4 vC;
        void main() { vec2 c = gl_PointCoord - 0.5; float d = length(c);
          float a = smoothstep(0.5, 0.08, d);
          a *= 0.75 + 0.25 * sin(c.x * 23.0 + vC.r * 40.0) * sin(c.y * 19.0 + vC.g * 30.0);
          gl_FragColor = vec4(vC.rgb, vC.a * a); }`,
    });
    this.points = new THREE.Points(g, this.mat);
    this.points.frustumCulled = false;
    this.points.renderOrder = 5;
    scene.add(this.points);
    this.emitters = [];
    this.birds = this._birds(scene);

    // explosion flash (one pooled light) and shockwave rings
    this.flash = new THREE.PointLight(0xffa050, 0, 110, 1.6);
    scene.add(this.flash); this.flashT = 0;
    this.rings = [];
    const rg = new THREE.RingGeometry(0.82, 1, 48).rotateX(-Math.PI / 2);
    for (let i = 0; i < 3; i++) {
      const m = new THREE.Mesh(rg, new THREE.MeshBasicMaterial({ color: 0xffe2b0, transparent: true, opacity: 0, depthWrite: false, fog: false, side: THREE.DoubleSide }));
      m.visible = false; m.frustumCulled = false; scene.add(m);
      this.rings.push({ m, t: 1 });
    }

    // snowfall: a box of flakes that stays wrapped around the camera
    const SN = 1500, sp = new Float32Array(SN * 3);
    for (let i = 0; i < SN * 3; i++) sp[i] = (Math.random() - 0.5) * 2 * SNOW_R;
    const sg = new THREE.BufferGeometry();
    sg.setAttribute('position', new THREE.BufferAttribute(sp, 3).setUsage(THREE.DynamicDrawUsage));
    this.snow = new THREE.Points(sg, new THREE.PointsMaterial({ color: 0xffffff, size: 0.085, transparent: true, opacity: 0.8, depthWrite: false }));
    this.snow.frustumCulled = false;
    scene.add(this.snow);
  }

  spawn(x, y, z, vx, vy, vz, life, s0, s1, r, g, b, a, grav = 0, drag = 1) {
    if (this.p.length >= MAX) this.p.shift();
    this.p.push({ x, y, z, vx, vy, vz, life, t: 0, s0, s1, r, g, b, a, grav, drag });
  }

  dust(p, n, kind) {
    const c = kind === 'stone' ? [0.62, 0.56, 0.46] : kind === 'wood' ? [0.4, 0.3, 0.2] : [0.4, 0.33, 0.24];
    for (let i = 0; i < 16; i++) {
      const sp = 1 + Math.random() * 3.5, k = 0.8 + Math.random() * 0.4;
      this.spawn(p.x, p.y, p.z, n.x * sp + (Math.random() - 0.5) * 2.4, n.y * sp + Math.random() * 2, n.z * sp + (Math.random() - 0.5) * 2.4,
        1.2 + Math.random() * 1.6, 0.25, 1.6 + Math.random() * 1.8, c[0] * k, c[1] * k, c[2] * k, 0.55, 1.2, 2.2);
    }
    for (let i = 0; i < 8; i++) {  // chips
      this.spawn(p.x, p.y, p.z, n.x * 4 + (Math.random() - 0.5) * 6, n.y * 4 + Math.random() * 5, n.z * 4 + (Math.random() - 0.5) * 6,
        0.9, 0.09, 0.05, c[0] * 0.7, c[1] * 0.7, c[2] * 0.7, 1, 9.8, 0.3);
    }
  }
  blood(p, dir) {
    for (let i = 0; i < 14; i++) {
      const sp = 2 + Math.random() * 5;
      this.spawn(p.x, p.y, p.z, dir.x * sp + (Math.random() - 0.5) * 2.5, dir.y * sp + (Math.random() - 0.3) * 2.5, dir.z * sp + (Math.random() - 0.5) * 2.5,
        0.5 + Math.random() * 0.5, 0.1, 0.5 + Math.random() * 0.5, 0.32, 0.015, 0.01, 0.75, 5, 2.5);
    }
  }
  splash(p) {
    for (let i = 0; i < 22; i++) {
      this.spawn(p.x, 0.1, p.z, (Math.random() - 0.5) * 2.5, 4 + Math.random() * 6, (Math.random() - 0.5) * 2.5,
        0.9 + Math.random() * 0.6, 0.3, 1.2, 0.85, 0.9, 0.92, 0.6, 9.8, 0.6);
    }
  }
  // Fireball, smoke column, debris, a flash of light and a ground shockwave.
  explosion(p) {
    const R = Math.random;
    for (let i = 0; i < 36; i++) {
      const a = R() * 6.283, e = R() * 1.4, sp = 4 + R() * 13, c = Math.cos(e);
      this.spawn(p.x, p.y + 0.4, p.z, Math.cos(a) * c * sp, Math.sin(e) * sp, Math.sin(a) * c * sp,
        0.45 + R() * 0.5, 1.2, 5 + R() * 4, 3.4, 1.2 + R() * 0.8, 0.22, 0.9, -1.5, 3.2);
    }
    for (let i = 0; i < 26; i++) {
      const a = R() * 6.283, sp = 1 + R() * 4.5, k = 0.1 + R() * 0.1;
      this.spawn(p.x, p.y + 0.6, p.z, Math.cos(a) * sp, 2 + R() * 4, Math.sin(a) * sp,
        3 + R() * 3.5, 2, 9 + R() * 6, k, k, k * 1.05, 0.62, -0.3, 1.1);
    }
    for (let i = 0; i < 26; i++) {
      const a = R() * 6.283, sp = 5 + R() * 16;
      this.spawn(p.x, p.y + 0.3, p.z, Math.cos(a) * sp, 5 + R() * 16, Math.sin(a) * sp,
        1.5, 0.24, 0.1, 0.12, 0.1, 0.09, 1, 9.8, 0.25);
    }
    this.flash.position.set(p.x, p.y + 2.2, p.z); this.flashT = 0.4;
    const ring = this.rings.reduce((a, b) => (b.t > a.t ? b : a));
    ring.t = 0; ring.m.position.set(p.x, p.y + 0.25, p.z); ring.m.visible = true;
  }

  addSmoke(pos, scale = 1) { this.emitters.push({ pos, scale, acc: Math.random() }); }

  update(dt, camera, viewH) {
    for (const e of this.emitters) {
      e.acc += dt * 5;
      while (e.acc > 1) {
        e.acc -= 1;
        const k = 0.55 + Math.random() * 0.25;
        this.spawn(e.pos.x + (Math.random() - 0.5) * 0.4, e.pos.y, e.pos.z + (Math.random() - 0.5) * 0.4,
          0.9 + Math.random() * 0.5, 1.5 + Math.random() * 0.8, 0.25, 9 + Math.random() * 4, 0.7 * e.scale, 7 * e.scale, k, k * 0.97, k * 0.93, 0.34, -0.05, 0.12);
      }
    }
    const P = this.p;
    let n = 0;
    for (let i = 0; i < P.length; i++) {
      const q = P[i];
      q.t += dt;
      if (q.t >= q.life) continue;
      const dr = Math.exp(-q.drag * dt);
      q.vx *= dr; q.vz *= dr; q.vy = q.vy * dr - q.grav * dt;
      q.x += q.vx * dt; q.y += q.vy * dt; q.z += q.vz * dt;
      const u = q.t / q.life;
      this.pos[n * 3] = q.x; this.pos[n * 3 + 1] = q.y; this.pos[n * 3 + 2] = q.z;
      this.col[n * 4] = q.r; this.col[n * 4 + 1] = q.g; this.col[n * 4 + 2] = q.b;
      this.col[n * 4 + 3] = q.a * Math.min(1, u * 8) * (1 - u) * (1 - u);
      this.size[n] = q.s0 + (q.s1 - q.s0) * Math.sqrt(u);
      P[n++] = q;
    }
    P.length = n;
    const g = this.points.geometry;
    g.setDrawRange(0, n);
    g.attributes.position.needsUpdate = g.attributes.aColor.needsUpdate = g.attributes.aSize.needsUpdate = true;
    this.mat.uniforms.uScale.value = viewH / (2 * Math.tan(THREE.MathUtils.degToRad(camera.fov) / 2));

    // explosion flash and shockwave
    this.flashT = Math.max(0, this.flashT - dt);
    this.flash.intensity = 9000 * Math.pow(this.flashT / 0.4, 2);
    for (const r of this.rings) {
      if (!r.m.visible) continue;
      r.t += dt / 0.55;
      if (r.t >= 1) { r.m.visible = false; continue; }
      r.m.scale.setScalar(1 + r.t * 17);
      r.m.material.opacity = 0.55 * (1 - r.t) * (1 - r.t);
    }
    // snow
    const sn = this.snow.geometry.attributes.position, sa = sn.array, cp = camera.position;
    this.snow.visible = camera.fov > 30;            // not through the scope, where near flakes would fill the glass
    for (let i = 0; i < sa.length; i += 3) {
      sa[i] += (0.5 + (i % 7) * 0.05) * dt; sa[i + 1] -= (1.0 + (i % 5) * 0.16) * dt; sa[i + 2] += 0.15 * dt;
      for (let k = 0; k < 3; k++) {
        const d = sa[i + k] - (k === 0 ? cp.x : k === 1 ? cp.y : cp.z);
        if (d > SNOW_R) sa[i + k] -= SNOW_R * 2; else if (d < -SNOW_R) sa[i + k] += SNOW_R * 2;
      }
    }
    sn.needsUpdate = true;

    for (const b of this.birds) {
      b.a += b.w * dt;
      b.g.position.set(b.cx + Math.cos(b.a) * b.r, b.h + Math.sin(b.a * 2.3 + b.ph) * 4, b.cz + Math.sin(b.a) * b.r * 0.7);
      b.g.rotation.y = -b.a + (b.w > 0 ? Math.PI : 0);
      const f = Math.sin(b.ph + b.a * 40) * 0.8;
      b.l.rotation.z = f; b.r2.rotation.z = -f;
    }
  }

  _birds(scene) {
    const wing = new THREE.BufferGeometry();
    wing.setAttribute('position', new THREE.Float32BufferAttribute([0, 0, -0.18, 0.75, 0, 0.05, 0, 0, 0.2], 3));
    wing.computeVertexNormals();
    const m = new THREE.MeshBasicMaterial({ color: 0x15130f, side: THREE.DoubleSide, fog: true });
    const out = [];
    for (let i = 0; i < 16; i++) {
      const g = new THREE.Group(), l = new THREE.Mesh(wing, m), r2 = new THREE.Mesh(wing, m);
      r2.scale.x = -1; g.add(l, r2); scene.add(g);
      const flock = i < 9 ? 0 : 1;
      out.push({ g, l, r2, a: Math.random() * 0.5 + flock * 2, w: (flock ? -0.07 : 0.09) * (0.9 + Math.random() * 0.2), r: (flock ? 120 : 75) + Math.random() * 14,
        cx: flock ? 90 : 5, cz: flock ? 190 : -5, h: (flock ? 62 : 80) + Math.random() * 7, ph: Math.random() * 6 });
    }
    return out;
  }
}
