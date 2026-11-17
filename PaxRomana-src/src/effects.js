// Particles (dust, stone chips, blood mist, water, smoke) and a flock of birds over the valley.
import * as THREE from 'three';

const MAX = 1400;

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
