// DOM heads-up display.
const $ = (id) => document.getElementById(id);

export class HUD {
  constructor() {
    this.el = {
      hud: $('hud'), kills: $('kills'), total: $('total'), alert: $('alertFill'), alertBox: $('alert'), feed: $('feed'),
      cross: $('crosshair'), marker: $('hitmarker'), mag: $('mag'), reserve: $('reserve'), rounds: $('rounds'), stance: $('stance'),
      scope: $('scope'), range: $('range'), zero: $('zero'), zoom: $('zoom'), wind: $('wind'), breath: $('breathFill'),
      menu: $('menu'), start: $('start'), load: $('loadFill'), loadText: $('loadText'), end: $('end'), hint: $('hint'), keys: $('keys'), status: $('reloadMsg'),
      objLine: $('objLine'), prompt: $('prompt'), charges: $('charges'), capture: $('capture'), capFill: $('capFill'), capText: $('capText'),
    };
    this._cache = {};
  }
  _set(key, el, v) { if (this._cache[key] !== v) { this._cache[key] = v; el.textContent = v; } }

  loading(p, text) { this.el.load.style.width = `${Math.round(p * 100)}%`; this.el.loadText.textContent = text; }
  ready() { this.el.start.disabled = false; this.el.start.textContent = 'Take the shot'; this.el.loadText.textContent = document.body.classList.contains('pad') ? 'Press A to deploy' : 'Press Enter or click to deploy'; this.el.start.focus(); }
  toggleKeys() { this.el.keys.classList.toggle('show'); }
  showMenu(v, resume) {
    this.el.menu.classList.toggle('hidden', !v);
    if (resume) { this.el.start.textContent = 'Resume'; }
    this.el.hud.classList.toggle('hidden', v);
  }
  objectiveLine(text) { this.el.objLine.textContent = text; }
  prompt(html) { if (this._cache.prompt !== html) { this._cache.prompt = html; this.el.prompt.innerHTML = html; this.el.prompt.classList.toggle('show', !!html); } }
  charges(n) { if (this._cache.ch !== n) { this._cache.ch = n; this.el.charges.innerHTML = Array.from({ length: 8 }, (_, i) => `<i class="${i < n ? '' : 'spent'}"></i>`).join(''); } }
  capture(progress, text) {
    const show = progress > 0.002 || !!text;
    this.el.capture.classList.toggle('show', show);
    if (!show) return;
    this.el.capFill.style.width = `${Math.round(progress * 100)}%`;
    this._set('cap', this.el.capText, text || (progress >= 1 ? 'Kremlin captured' : `Raising your standard ${Math.round(progress * 100)}%`));
  }
  // Switch button labels between controller and keyboard.
  device(isPad) { if (this._cache.dev !== isPad) { this._cache.dev = isPad; document.body.classList.toggle('pad', isPad); if (!this.el.start.disabled) this.el.loadText.textContent = isPad ? 'Press A to deploy' : 'Press Enter or click to deploy'; } }
  objective(k, total) { this._set('k', this.el.kills, String(k)); this._set('t', this.el.total, String(total)); }
  alert(v, alarm) { this.el.alert.style.width = `${v}%`; this.el.alertBox.classList.toggle('alarm', alarm); }
  ammo(mag, reserve) {
    if (this._cache.mag !== mag) {
      this._cache.mag = mag; this.el.mag.textContent = mag;
      this.el.rounds.innerHTML = Array.from({ length: 5 }, (_, i) => `<i class="${i < mag ? '' : 'spent'}"></i>`).join('');
    }
    this._set('res', this.el.reserve, String(reserve));
  }
  status(text) { this._set('status', this.el.status, text); }
  stance(i) { this._set('st', this.el.stance, ['STANDING', 'CROUCHED', 'PRONE'][i]); }
  scope(on, { range, zero, zoom, wind, breath, exhausted }) {
    this.el.scope.classList.toggle('on', on);
    this.el.cross.classList.toggle('off', on);
    if (!on) return;
    this._set('rg', this.el.range, range > 0 ? `${Math.round(range)} m` : '— m');
    this._set('zr', this.el.zero, `${zero} m`);
    this._set('zm', this.el.zoom, `${zoom}×`);
    this._set('wd', this.el.wind, wind);
    this.el.breath.style.width = `${breath * 100}%`;
    this.el.breath.classList.toggle('out', exhausted);
  }
  crosshair(show) { this.el.cross.classList.toggle('hide', !show); }
  hitmarker(head) {
    const m = this.el.marker;
    m.className = ''; void m.offsetWidth; m.className = head ? 'show head' : 'show';
  }
  feed(text, cls = '') {
    const d = document.createElement('div');
    d.className = `entry ${cls}`; d.innerHTML = text;
    this.el.feed.prepend(d);
    setTimeout(() => d.classList.add('gone'), 4200);
    setTimeout(() => d.remove(), 5200);
    while (this.el.feed.children.length > 5) this.el.feed.lastChild.remove();
  }
  hint(text, ms = 5000) {
    this.el.hint.textContent = text; this.el.hint.classList.add('show');
    clearTimeout(this._ht); this._ht = setTimeout(() => this.el.hint.classList.remove('show'), ms);
  }
  cine(v) { document.body.classList.toggle('cine', v); }
  end(win, s) {
    const acc = s.shots ? Math.round((s.hits / s.shots) * 100) : 0;
    const mm = Math.floor(s.time / 60), ss = String(Math.floor(s.time % 60)).padStart(2, '0');
    const rating = !win ? '—' : acc >= 85 && s.alarms === 0 ? 'UMBRA · Ghost of the Moskva' : acc >= 65 ? 'SAGITTARIUS · Marksman' : 'TIRO · Recruit';
    this.el.end.innerHTML = `
      <div class="panel">
        <div class="eyebrow">${win ? 'Mission accomplished' : 'Mission failed'}</div>
        <h1>${win ? 'The Kremlin is yours' : 'Mission failed'}</h1>
        <p class="sub">${win ? 'Your standard flies over the Senate. The Third Rome has a new master.' : 'The legion still holds the Kremlin. Rome endures another day.'}</p>
        <div class="stats">
          <div><b>${s.kills}/${s.total}</b><span>Legionaries</span></div>
          <div><b>${acc}%</b><span>Accuracy</span></div>
          <div><b>${s.heads}</b><span>Headshots</span></div>
          <div><b>${Math.round(s.longest)} m</b><span>Longest shot</span></div>
          <div><b>${mm}:${ss}</b><span>Time</span></div>
          <div><b>${s.alarms}</b><span>Alarms raised</span></div>
          <div><b>${s.charges}</b><span>Charges used</span></div>
        </div>
        <div class="rating">${rating}</div>
        <button id="again" onclick="location.reload()">Deploy again</button>
        <div class="again-hint padonly">Press A</div>
      </div>`;
    this.el.end.classList.remove('hidden');
    document.getElementById('again').focus();
    this.el.hud.classList.add('hidden');
  }
}
