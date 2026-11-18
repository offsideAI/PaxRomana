// Capturing the Kremlin: doors (opened by hand or blown in), the count of defenders still inside the walls,
// and the flag in the courtyard, which can only be raised once those defenders are gone.
const HOLD = 8;         // seconds at the flagpole to raise your standard

const inWalls = (x, z) => Math.abs(x) < 48.5 && Math.abs(z) < 41.5;

function whereabouts(s, castle) {
  const k = castle.keep, y = s.pos.y - 30;
  if (Math.abs(s.pos.x - k.x) < k.half && Math.abs(s.pos.z - k.z) < k.half) return y > 10 ? 'on the Senate roof' : 'inside the Senate';
  if (y > 14) return 'on the gate tower';
  if (y > 8) return 'on the wall-walk';
  return 'in the courtyard';
}

export class Capture {
  constructor(castle, player, legion, hud, audio, pad) {
    this.castle = castle; this.doors = castle.doors; this.point = castle.capture;
    this.player = player; this.legion = legion; this.hud = hud; this.audio = audio; this.pad = pad;
    this.progress = 0; this.done = false; this.stage = -1; this.defenders = -1; this.nearDoor = null; this.tickT = 0; this.told = false;
  }

  // inside the curtain walls or the gate passage
  inside() { const p = this.player.pos; return Math.abs(p.x) < 43.5 && p.z > -33.5 && p.z < (Math.abs(p.x) < 2.4 ? 39.4 : 33.4); }

  // open or close the nearest door
  useDoor() {
    const d = this.nearDoor;
    if (!d || d.broken) return false;
    d.want = d.want ? 0 : 1;
    this.audio.door();
    return true;
  }

  // An explosion at p: doors within reach are blown off their hinges for good.
  blast(p) {
    let n = 0;
    for (const d of this.doors) {
      if (d.broken || Math.hypot(p.x - d.x, p.z - d.z) > 5.5 || Math.abs(p.y - 30) > 9) continue;
      d.broken = true; d.amt = d.want = 1; d.solid.off = true;
      for (const l of d.leaves) { l.pivot.position.y = -500; l.pivot.updateMatrixWorld(true); }      // out of sight and out of the line of fire
      this.hud.feed(`<b>BREACH</b> ${d.name} is down`, 'head');
      n++;
    }
    return n;
  }

  update(dt) {
    const p = this.player.pos, pt = this.point, key = this.pad.active ? 'A' : 'O';
    let nd = null, bd = 1e9;
    for (const d of this.doors) {
      if (d.broken) continue;
      const step = dt / 0.65;
      d.amt += Math.max(-step, Math.min(step, d.want - d.amt));
      d.solid.off = d.amt > 0.5;
      const e = d.amt * d.amt * (3 - 2 * d.amt);
      for (const l of d.leaves) l.pivot.rotation.y = l.closed + (l.open - l.closed) * e;
      const dist = Math.hypot(p.x - d.x, p.z - d.z);
      if (dist < d.reach && dist < bd) { bd = dist; nd = d; }
    }
    this.nearDoor = nd;

    // defenders still alive inside the walls
    let left = 0; const spots = [];
    for (const s of this.legion.soldiers) if (s.alive && inWalls(s.pos.x, s.pos.z)) { left++; if (spots.length < 3) spots.push(whereabouts(s, this.castle)); }
    if (left !== this.defenders) {
      this.defenders = left;
      if (left > 0 && left <= 2 && !this.done) this.hud.hint(`${left === 1 ? 'Last defender' : 'Two defenders left'}: ${[...new Set(spots)].join(' and ')}`, 6000);
      if (left === 0 && !this.told) { this.told = true; this.hud.feed('<b>WALLS CLEAR</b> raise your standard at the flagpole', 'head'); this.pad.rumble({ low: 0.5, high: 0.5, ms: 260, decay: true }); }
    }

    // the flag
    const atFlag = Math.hypot(p.x - pt.x, p.z - pt.z) < pt.r && Math.abs(p.y - 30) < 3;
    if (!this.done) {
      if (atFlag && left === 0) {
        this.progress = Math.min(1, this.progress + dt / HOLD);
        this.tickT -= dt;
        if (this.tickT <= 0) { this.tickT = 0.5; this.pad.rumble({ high: 0.12 + this.progress * 0.3, ms: 45 }); }
        if (this.progress >= 1) {
          this.done = true;
          pt.flag.material = pt.capMat; pt.ring.material.color.setRGB(0.3, 1.7, 2.3);
          this.audio.loot(); this.pad.rumble({ low: 0.9, high: 0.9, lt: 0.5, rt: 0.5, ms: 900, decay: true });
        }
      } else this.progress = Math.max(0, this.progress - dt / 14);
    }
    this.hud.capture(this.progress, atFlag && left > 0 && !this.done ? `Contested: ${left} defender${left === 1 ? '' : 's'} inside the walls` : '');

    this.hud.prompt(nd ? `<kbd>${key}</kbd> ${nd.want ? 'Close' : 'Open'} ${nd.name}` : '');

    const stage = this.done ? 4 : left > 0 ? (this.inside() ? 1 : 0) : atFlag ? 3 : 2;
    const text = [
      'Breach the Kremlin: open the Spasskaya gate, or blow it in',
      `Clear the defenders inside the walls: ${left} left`,
      'Raise your standard at the flagpole in the courtyard',
      'Hold the flagpole',
      'The Kremlin is yours',
    ][stage];
    if (text !== this.text) { this.text = text; this.hud.objectiveLine(text); }
    this.stage = stage;
  }
}
