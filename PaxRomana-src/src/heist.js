// The heist: doors that open and close, loot that goes in the bag, and the mission stages
// (break in -> find the vault key -> take the Crown Jewels -> get out through the gate).
const TAKE_REACH = 2.6;

export class Heist {
  constructor(castle, player, hud, audio) {
    this.doors = castle.doors; this.loot = castle.loot; this.player = player; this.hud = hud; this.audio = audio;
    this.bag = []; this.hasKey = false; this.hasJewels = false; this.escaped = false; this.stage = -1;
    this.nearDoor = null; this.nearLoot = null;
  }

  get value() { return this.bag.reduce((a, i) => a + i.value, 0); }
  get treasures() { return this.loot.filter((i) => !i.key).length; }
  get taken() { return this.bag.filter((i) => !i.key).length; }

  // inside the curtain walls or the gate passage
  inside() { const p = this.player.pos; return Math.abs(p.x) < 43.5 && p.z > -33.5 && p.z < (Math.abs(p.x) < 2.4 ? 39.4 : 33.4); }

  // O: open or close the nearest door
  useDoor() {
    const d = this.nearDoor;
    if (!d) return;
    if (d.locked && !this.hasKey) { this.hud.hint('Locked. The key must be somewhere in the castle', 3000); this.audio.locked(); return; }
    if (d.locked) { d.locked = false; this.hud.feed('Vault unlocked'); }
    d.want = d.want ? 0 : 1;
    this.audio.door();
  }

  // L: put the nearest piece of loot in the bag
  take() {
    const it = this.nearLoot;
    if (!it) return;
    it.taken = true; it.mesh.visible = false;
    this.bag.push(it);
    if (it.key) this.hasKey = true;
    if (it.jewels) this.hasJewels = true;
    this.audio.loot();
    this.hud.feed(it.key ? 'Taken: <b>the vault key</b>' : `In the bag: <b>${it.name}</b>${it.value ? ` <em>${it.value.toLocaleString('en')} aurei</em>` : ''}`, 'head');
    this.hud.bag(this.bag, this.value);
  }

  update(dt) {
    const p = this.player.pos;
    let nd = null, bd = 1e9;
    for (const d of this.doors) {
      const step = dt / 0.65;
      d.amt += Math.max(-step, Math.min(step, d.want - d.amt));
      d.solid.off = d.amt > 0.5;
      const e = d.amt * d.amt * (3 - 2 * d.amt);
      for (const l of d.leaves) l.pivot.rotation.y = l.closed + (l.open - l.closed) * e;
      const dist = Math.hypot(p.x - d.x, p.z - d.z);
      if (dist < d.reach && dist < bd) { bd = dist; nd = d; }
    }
    let nl = null, bl = TAKE_REACH;
    for (const it of this.loot) {
      if (it.taken) continue;
      const dist = Math.hypot(p.x - it.x, p.z - it.z);
      if (dist < bl) { bl = dist; nl = it; }
    }
    this.nearDoor = nd; this.nearLoot = nl;
    this.hud.prompt(nl ? `<kbd>L</kbd> Take ${nl.name}` : nd ? (nd.locked && !this.hasKey ? `<kbd>O</kbd> ${cap(nd.name)} · locked` : `<kbd>O</kbd> ${nd.want ? 'Close' : 'Open'} ${nd.name}`) : '');

    const inside = this.inside();
    if (this.hasJewels && !inside) this.escaped = true;
    const stage = this.hasJewels ? 3 : this.hasKey ? 2 : inside ? 1 : 0;
    if (stage !== this.stage) {
      this.stage = stage;
      this.hud.objectiveLine([
        'Break into Château de Montvallon',
        'Search the castle for the key to the vault',
        'Open the vault in the keep and take the Crown Jewels',
        'Escape through the gate with the Crown Jewels',
      ][stage]);
    }
  }
}

const cap = (s) => s[0].toUpperCase() + s.slice(1);
