// The garrison: patrol routes on the wall-walks, the keep, the gate, the road, the bridge and the camp.
import * as THREE from 'three';
import { Soldier } from './soldier.js';
import { groundAt } from './terrain.js';
import { PLATEAU, WALK_Y, CAMP, ROAD, BRIDGE } from './config.js';

class Route {
  constructor(pts, { loop = false, y = null } = {}) {
    this.pts = pts; this.loop = loop; this.y = y;
    if (loop) this.pts = [...pts, pts[0]];
    this.cum = [0];
    for (let i = 1; i < this.pts.length; i++) this.cum.push(this.cum[i - 1] + Math.hypot(this.pts[i][0] - this.pts[i - 1][0], this.pts[i][1] - this.pts[i - 1][1]));
    this.total = this.cum[this.cum.length - 1];
    this.o = { x: 0, z: 0, tx: 0, tz: 1 };
  }
  at(s) {
    s = this.loop ? ((s % this.total) + this.total) % this.total : Math.max(0, Math.min(this.total, s));
    let i = 1;
    while (i < this.cum.length - 1 && this.cum[i] < s) i++;
    const a = this.pts[i - 1], b = this.pts[i], len = this.cum[i] - this.cum[i - 1] || 1, t = (s - this.cum[i - 1]) / len;
    const o = this.o;
    o.x = a[0] + (b[0] - a[0]) * t; o.z = a[1] + (b[1] - a[1]) * t;
    o.tx = (b[0] - a[0]) / len; o.tz = (b[1] - a[1]) / len;
    return o;
  }
  yAt(x, z) { return this.y !== null ? this.y : groundAt(x, z); }
}

export class Legion {
  constructor(scene, castle) {
    this.scene = scene; this.soldiers = []; this.alertLevel = 0; this.alarm = false; this.onAlarm = null;
    const W = WALK_Y, add = (o) => { const s = new Soldier(o); this.soldiers.push(s); scene.add(s.root); return s; };
    const R = (pts, o) => new Route(pts, o);

    // wall-walks
    add({ route: R([[-36, 34.5], [-17.5, 34.5]], { y: W }), s: 6 });
    add({ route: R([[17.5, 34.5], [36, 34.5]], { y: W }), s: 2 });
    add({ route: R([[17.5, 34.5], [36, 34.5]], { y: W }), s: 16 });
    add({ route: R([[15, 45.5], [39, 45.5]]), s: 4 });                 // foot of the south wall
    add({ route: R(ROAD.slice(4)), s: 10 });                           // camp road
    add({ route: R([[-36, -34.5], [-7.5, -34.5]], { y: W }), s: 12 });
    add({ route: R([[7.5, -34.5], [36, -34.5]], { y: W }), s: 3 });
    // gatehouse roof + ground guards (they only move once alarmed)
    const gt = castle.gateTop;
    add({ route: R([[-4, 38.4], [-0.8, 38.4]], { y: gt }), hold: true, face: 0, s: 1.28 });
    add({ route: R([[0.8, 38.4], [4, 38.4]], { y: gt }), hold: true, face: 0, s: 1.97 });
    add({ route: R([[-3.6, 42], [-3.6, 47]]), hold: true, face: 0, s: 0 });
    add({ route: R([[3.6, 42], [3.6, 47]]), hold: true, face: 0, s: 0 });
    // keep roof
    const k = castle.keep, o = k.half - 1.6;
    const loop = R([[k.x - o, k.z + o], [k.x + o, k.z + o], [k.x + o, k.z - o], [k.x - o, k.z - o]], { loop: true, y: k.top });
    add({ route: loop, s: 3, speed: 1.0 }); add({ route: loop, s: 3 + loop.total / 2, speed: 1.0 });
    // road patrol (pair)
    const road = R(ROAD.slice(0, 5));
    add({ route: road, s: 30, lat: 0.7 }); add({ route: road, s: 30, lat: -0.7 });
    // bridge sentry
    add({ route: R([[BRIDGE.x, BRIDGE.z0 + 14], [BRIDGE.x, BRIDGE.z1 - 14]], { y: BRIDGE.y }), s: 10 });
    // a contubernium drilling around the camp, centurion at its head
    const ring = [];
    for (let i = 0; i < 48; i++) { const a = -(i / 48) * Math.PI * 2; ring.push([CAMP.x + Math.sin(a) * (CAMP.r + 13), CAMP.z + Math.cos(a) * (CAMP.r + 13)]); }
    const drill = R(ring, { loop: true });
    add({ route: drill, s: 104.5, lat: 0, centurion: true, speed: 1.35 });
    for (let r = 0; r < 3; r++) for (const lat of [-0.75, 0.75]) add({ route: drill, s: 102 - r * 1.7, lat, speed: 1.35 });
    // watchtower lookout
    const tx = CAMP.x + 26, tz = CAMP.z + 34;
    add({ route: R([[tx - 1.1, tz + 1.1], [tx + 1.1, tz + 1.1]], { y: groundAt(tx, tz) + 7.12 }), hold: true, face: 0.3, s: 1 });
    // camp: two by the fire, one walking the via principalis
    add({ route: R([[CAMP.x + 7.6, CAMP.z + 7.5], [CAMP.x + 12, CAMP.z + 16]]), hold: true, face: -2.3 });
    add({ route: R([[CAMP.x + 4.6, CAMP.z + 4.6], [CAMP.x - 3, CAMP.z + 20]]), hold: true, face: 0.8 });
    add({ route: R([[CAMP.x, CAMP.z + 40], [CAMP.x, CAMP.z - 30]]), s: 20 });

    this.total = this.soldiers.length;
  }

  get alive() { let n = 0; for (const s of this.soldiers) if (s.alive) n++; return n; }

  alertAround(p, radius, seconds, raise) {
    for (const s of this.soldiers) if (s.alive && s.pos.distanceTo(p) < radius) s.alert(seconds);
    this.alertLevel = Math.min(100, this.alertLevel + raise);
    if (this.alertLevel >= 100 && !this.alarm) {
      this.alarm = true; this.alarmT = 45;
      for (const s of this.soldiers) s.alert(45);
      this.onAlarm && this.onAlarm();
    }
  }

  kill(s, dir, head) {
    s.kill(dir, head, this.scene);
    this.alertAround(s.pos, 16, 14, head ? 9 : 14);
  }

  update(dt, time) {
    for (const s of this.soldiers) s.update(dt, time);
    if (this.alarm) {
      this.alarmT -= dt;
      if (this.alarmT <= 0) { this.alarm = false; this.alertLevel = 55; }
    } else this.alertLevel = Math.max(0, this.alertLevel - dt * 1.6);
  }
}
