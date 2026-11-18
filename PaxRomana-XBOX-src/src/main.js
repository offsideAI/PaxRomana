// PAX ROMANA: TERTIA ROMA. Capture the Kremlin of a future Moscow. Entry point and game loop.
import * as THREE from 'three';
import { U, WIND, SPAWN, CAMP } from './config.js';
import { makeTextures } from './textures.js';
import { buildTerrain, groundAt } from './terrain.js';
import { buildSky } from './sky.js';
import { buildCastle } from './castle.js';
import { buildVegetation } from './vegetation.js';
import { Legion } from './legion.js';
import { FX } from './effects.js';
import { Audio } from './audio.js';
import { Player, BASE_FOV } from './player.js';
import { Weapon } from './weapon.js';
import { buildPost } from './post.js';
import { HUD } from './hud.js';
import { Capture } from './capture.js';
import { buildCity } from './city.js';
import { Pad, B } from './gamepad.js';

const hud = new HUD();
const params = new URLSearchParams(location.search);
const XBOX = params.has('xbox') || /Xbox/i.test(navigator.userAgent);
const LOW = params.has('low');
const pad = new Pad();
const nextFrame = () => new Promise((r) => requestAnimationFrame(() => setTimeout(r, 0)));

async function boot() {
  const canvas = document.getElementById('c');
  const renderer = new THREE.WebGLRenderer({ canvas, antialias: false, powerPreference: 'high-performance' });
  renderer.setPixelRatio(XBOX || LOW ? 1 : Math.min(devicePixelRatio, 1.5));
  renderer.setSize(innerWidth, innerHeight);
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 0.62;
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFSoftShadowMap;

  const scene = new THREE.Scene();
  const camera = new THREE.PerspectiveCamera(BASE_FOV, innerWidth / innerHeight, 0.07, 30000);
  scene.add(camera);
  const killCam = new THREE.PerspectiveCamera(48, innerWidth / innerHeight, 0.05, 30000);

  hud.loading(0.05, 'Firing Kremlin brick…'); await nextFrame();
  const tex = makeTextures(LOW ? 512 : 1024);
  hud.loading(0.3, 'Laying snow over the Moskva valley…'); await nextFrame();
  scene.add(buildTerrain(tex));
  const sky = buildSky(scene, renderer);
  hud.loading(0.5, 'Raising the Kremlin and the skyline…'); await nextFrame();
  const castle = buildCastle(tex);
  scene.add(castle.group);
  scene.add(buildCity());
  hud.loading(0.65, 'Frosting the birch and pine…'); await nextFrame();
  const veg = buildVegetation(tex, { grassCount: LOW ? 40000 : XBOX ? 70000 : 100000 });
  scene.add(veg.group);
  hud.loading(0.85, 'Mustering the legion…'); await nextFrame();
  const legion = new Legion(scene, castle);
  const fx = new FX(scene);
  castle.smoke.forEach((p, i) => fx.addSmoke(p, i < 2 ? 1 : 1.5));
  const audio = new Audio();
  const player = new Player(camera, canvas, [...castle.solids, ...veg.obstacles], audio, legion.soldiers, pad);
  const capture = new Capture(castle, player, legion, hud, audio, pad);
  const world = { colliders: castle.colliders, legion, fx, audio, onKill: null, onMiss: null, explode: null };
  const weapon = new Weapon(scene, camera, player, world, tex);
  const post = buildPost(renderer, scene, camera);
  post.resize(innerWidth, innerHeight);

  // ---- game state ----
  const state = { started: false, over: false, time: 0, alarms: 0, endT: -1, win: false };
  const kc = { active: false, bullet: null, phase: 0, t: 0, hit: null, look: new THREE.Vector3(), from: new THREE.Vector3(), cooldown: 0 };
  let timeScale = 1, range = 0, rangeT = 0;
  const ray = new THREE.Raycaster();
  const v1 = new THREE.Vector3(), v2 = new THREE.Vector3(), v3 = new THREE.Vector3();

  legion.onAlarm = () => {
    state.alarms++; audio.horn();
    hud.feed('<b>ALARM</b> the klaxon sounds. The garrison is running.', 'warn');
    pad.rumble({ low: 0.35, ms: 500, decay: true }); pad.rumble({ low: 0.35, ms: 500, delay: 700, decay: true });
  };
  world.onKill = (r) => {
    hud.hitmarker(r.head);
    const who = r.soldier.centurion ? 'Centurion' : 'Legionary';
    pad.rumble({ high: r.head ? 0.7 : 0.4, lt: r.head ? 0.5 : 0, ms: r.head ? 110 : 60 });
    hud.feed(`${who} down <em>${Math.round(r.dist)} m</em>${r.head ? ' <b>HEADSHOT</b>' : ''}`, r.head ? 'head' : '');
  };

  // An explosive charge goes off at p: fireball, blast radius, breached doors, and a rumble that arrives with the sound.
  const BLAST = 8.5;
  world.explode = (p, dist, delay) => {
    const at = p.clone();
    fx.explosion(at); audio.explosion(delay, dist);
    const k = THREE.MathUtils.clamp(1.1 - dist / 420, 0.22, 1);
    pad.rumble({ low: k, high: k * 0.7, lt: k * 0.4, rt: k * 0.4, ms: 380 + 520 * k, delay: delay * 1000, decay: true });
    player.shake = Math.max(player.shake, THREE.MathUtils.clamp(1.6 - dist / 45, 0, 1.6));
    let kills = 0;
    for (const s of legion.soldiers) {
      if (!s.alive) continue;
      v1.copy(s.pos); v1.y += 0.9;
      if (v1.distanceTo(at) > BLAST) continue;
      v1.sub(at); v1.y = Math.abs(v1.y) + 1.5; v1.normalize();
      legion.kill(s, v1.clone(), false);
      kills++; weapon.stats.hits++; weapon.stats.longest = Math.max(weapon.stats.longest, dist);
    }
    legion.alertAround(at, 70, 25, 40);
    capture.blast(at);
    if (kills) { hud.hitmarker(false); hud.feed(`Blast <em>${Math.round(dist)} m</em> <b>${kills} DOWN</b>`, 'head'); }
  };

  // ---- haptics: what the controller feels ----
  weapon.onFire = (explosive) => {
    if (explosive) { pad.rumble({ rt: 0.9, low: 1, high: 0.5, ms: 300, decay: true }); return; }
    pad.rumble({ rt: 1, ms: 70 });                                   // the trigger breaks
    pad.rumble({ low: 0.95, high: 0.75, ms: 210, decay: true });     // recoil
    pad.rumble({ high: 0.28, ms: 35, delay: 520 }); pad.rumble({ high: 0.34, ms: 35, delay: 930 });   // bolt back, bolt home
  };
  weapon.onDry = () => pad.rumble({ rt: 0.3, ms: 35 });
  const beat = audio.heartbeat.bind(audio);
  audio.heartbeat = () => { beat(); pad.rumble({ low: 0.22, ms: 70 }); pad.rumble({ low: 0.15, ms: 70, delay: 170 }); };
  function reload() {
    const busy = weapon.reloadT > 0;
    weapon.reload();
    if (!busy && weapon.reloadT > 0) for (const d of [250, 1050, 1850, 2450]) pad.rumble({ high: 0.3, lt: 0.15, ms: 40, delay: d });
  }

  function measure() {
    camera.getWorldDirection(v1);
    let best = 0;
    v2.copy(camera.position);
    for (let d = 4; d < 1800; d += 4) {
      v3.copy(v2).addScaledVector(v1, d);
      if (v3.y < Math.max(0, groundAt(v3.x, v3.z))) { best = d - 2; break; }
    }
    ray.set(v2, v1); ray.far = best || 1800;
    const h = ray.intersectObjects(world.colliders, false);
    if (h.length) best = h[0].distance;
    v3.copy(v2).addScaledVector(v1, best || 1800);
    for (const s of legion.soldiers) {
      const hit = s.hitTest(v2, v3);
      if (hit) { const d = hit.t * (best || 1800); if (!best || d < best) best = d; }
    }
    return best;
  }

  function startKillCam(b, hit) {
    kc.active = true; kc.bullet = b; kc.phase = 0; kc.t = 0; kc.hit = hit; b.cam = true;
    hud.cine(true);
  }
  function endKillCam() {
    kc.active = false; kc.cooldown = 1;
    if (kc.bullet) { scene.remove(kc.bullet.mesh, kc.bullet.trail); kc.bullet.cam = false; }
    kc.bullet = null; timeScale = 1; hud.cine(false);
  }

  function tryFire() {
    const b = weapon.fire();
    if (!b) return;
    if (kc.cooldown > 0) return;
    const hit = weapon.predict(b);
    if (!hit) return;
    const dist = hit.point.distanceTo(camera.position), last = legion.alive === 1;
    if (dist > 90 && (last || (hit.head && Math.random() < 0.65) || Math.random() < 0.22)) startKillCam(b, hit);
  }

  // ---- input: controller first, with keyboard and mouse twins ----
  function trigger() {
    if (kc.active) { if (kc.t > 0.4) endKillCam(); return; }
    if (player.swimming) { hud.hint('You cannot shoot while swimming', 1800); return; }
    tryFire();
  }
  function triggerExplosive() {
    if (kc.active) return;
    if (player.swimming) { hud.hint('You cannot shoot while swimming', 1800); return; }
    if (weapon.charges === 0 && weapon.cool <= 0) hud.hint('No explosive charges left', 1600);
    weapon.fireExplosive();
  }
  function useDoor() { if (!kc.active && capture.useDoor()) pad.rumble({ low: 0.35, ms: 160, decay: true }); }
  canvas.addEventListener('mousedown', (e) => {
    if (!player.locked || !player.enabled) return;
    if (e.button === 0) trigger();
    if (e.button === 1) { e.preventDefault(); triggerExplosive(); }
    if (e.button === 2 && !kc.active) weapon.wantAds = true;
  });
  addEventListener('mouseup', (e) => { if (e.button === 2) weapon.wantAds = false; });
  addEventListener('contextmenu', (e) => e.preventDefault());
  addEventListener('wheel', (e) => { if (player.enabled && weapon.ads > 0.5) weapon.cycleZoom(e.deltaY < 0 ? 1 : -1); }, { passive: true });
  player.onKey = (code) => {
    if (code === 'KeyR') reload();
    if (code === 'BracketRight' || code === 'KeyE') weapon.cycleZero(1);
    if (code === 'BracketLeft' || code === 'KeyQ') weapon.cycleZero(-1);
    if (code === 'KeyF' && !kc.active) weapon.wantAds = !weapon.wantAds;
    if (code === 'Space' || code === 'Enter' || code === 'NumpadEnter' || code === 'KeyX') trigger();
    if (code === 'KeyG') triggerExplosive();
    if (code === 'Equal' || code === 'NumpadAdd') weapon.cycleZoom(1);
    if (code === 'Minus' || code === 'NumpadSubtract') weapon.cycleZoom(-1);
    if (code === 'KeyH') hud.toggleKeys();
    if (code === 'KeyO') useDoor();
  };

  // Controller. LT scope, RT fire, RB explosive, LB hold breath, X reload, A use / skip, B crouch, R3 prone,
  // D-pad zoom and zero, L3 sprint, View controls, Menu pause.
  let ltHeld = false, rtHeld = false;
  function padInput() {
    if (!pad.connected) return;
    const a = pad.pressed(B.A), menu = pad.pressed(B.MENU);
    const rt = pad.value(B.RT) > 0.5, rtEdge = rt && !rtHeld;
    rtHeld = rt;
    if (state.over) { if (a || menu) location.reload(); return; }
    if (!player.enabled) { if ((a || menu) && !hud.el.start.disabled) begin(true); return; }     // title or pause screen
    if (menu) { if (player.locked) document.exitPointerLock(); else setPlaying(false); return; }
    if (pad.pressed(B.VIEW)) hud.toggleKeys();
    if (kc.active) { if (a || rtEdge) trigger(); return; }
    const lt = pad.value(B.LT);
    if (lt > 0.4 && !ltHeld) { ltHeld = true; weapon.wantAds = true; pad.rumble({ lt: 0.25, ms: 30 }); }
    else if (lt < 0.2 && ltHeld) { ltHeld = false; weapon.wantAds = false; }
    if (rtEdge) trigger();
    if (pad.pressed(B.RB)) triggerExplosive();
    if (pad.pressed(B.X)) reload();
    if (a) useDoor();
    if (pad.pressed(B.B) && !player.swimming) player.stance = player.stance === 1 ? 0 : 1;
    if (pad.pressed(B.R3) && !player.swimming) player.stance = player.stance === 2 ? 0 : 2;
    if (pad.pressed(B.UP)) weapon.cycleZoom(1);
    if (pad.pressed(B.DOWN)) weapon.cycleZoom(-1);
    if (pad.pressed(B.RIGHT)) weapon.cycleZero(1);
    if (pad.pressed(B.LEFT)) weapon.cycleZero(-1);
    if (pad.pressed(B.Y)) weapon.zoomI = (weapon.zoomI + 1) % 5;
  }

  // Aim friction: the right stick slows while the reticle is on or beside a defender, which makes a
  // 400 m shot with a thumbstick fair.
  function aimFriction() {
    let slow = 1;
    if (pad.active) {
      camera.getWorldDirection(v1);
      for (const s of legion.soldiers) {
        if (!s.alive) continue;
        v2.copy(s.pos); v2.y += 1.1; v2.sub(camera.position);
        const along = v2.dot(v1);
        if (along < 3) continue;
        const off = Math.sqrt(Math.max(0, v2.lengthSq() - along * along));
        if (off < 0.5) { slow = 0.4; break; }
        if (off < 1.1) slow = Math.min(slow, 0.62);
      }
    }
    player.aimSlow = slow;
  }

  let edgeT = 0;
  player.onEdge = () => { if (edgeT <= 0) { edgeT = 6; hud.hint('Edge of the valley. The Kremlin lies behind you', 3000); } };

  // Sentries notice a rifleman who walks up to them: closer if you crouch or crawl, and only with a clear line past the masonry.
  let spotT = 0, spotted = false;
  function watch() {
    const R = [45, 28, 13][player.stance] * (player.swimming ? 0.5 : 1);
    let seen = false;
    v2.copy(camera.position);
    for (const s of legion.soldiers) {
      if (!s.alive) continue;
      v1.copy(s.pos); v1.y += 1.67;
      const d = v1.distanceTo(v2);
      if (d > R) continue;
      v3.subVectors(v2, v1).normalize();
      ray.set(v1, v3); ray.far = d - 0.3;
      if (ray.intersectObjects(world.colliders, false).length === 0) { seen = true; break; }
    }
    if (seen) {
      legion.alertAround(player.pos, R + 25, 12, 14);
      if (!spotted) { hud.feed('<b>SPOTTED</b> a sentry has seen you', 'warn'); pad.rumble({ low: 0.4, high: 0.2, ms: 180, decay: true }); }
    }
    spotted = seen;
  }

  // Playing / paused. A mouse player holds pointer lock; a controller player does not need it.
  function setPlaying(on) {
    if (state.over) return;
    player.enabled = on;
    hud.showMenu(!on, state.started);
    if (!on) { weapon.wantAds = false; ltHeld = false; pad.stop(); hud.el.start.focus(); }
  }
  player.onLock = (locked) => {
    if (state.over) return;
    if (locked) { player.padMode = false; setPlaying(true); }
    else if (!player.padMode) setPlaying(false);
  };
  function begin(viaPad) {
    audio.init();
    if (viaPad) { player.padMode = true; setPlaying(true); } else player.lock();
    if (!state.started) {
      state.started = true;
      setTimeout(() => hud.hint(pad.active || XBOX
        ? 'LT scope · RT fire · RB explosive · View shows all controls'
        : 'Right mouse scope · Left mouse fire · G explosive · H shows all controls', 9000), 1200);
    }
  }
  hud.el.start.addEventListener('click', () => begin(XBOX));
  addEventListener('resize', () => {
    renderer.setSize(innerWidth, innerHeight);
    camera.aspect = killCam.aspect = innerWidth / innerHeight;
    camera.updateProjectionMatrix(); killCam.updateProjectionMatrix();
    post.resize(innerWidth, innerHeight);
  });

  // ---- loop ----
  const clock = new THREE.Clock();
  const shadowC = new THREE.Vector3();
  const SHADOW_ANCHORS = [
    { c: new THREE.Vector3(0, 48, 0), ext: 72, reach: 75 },                 // the whole château, keep to tower bases
    { c: new THREE.Vector3(CAMP.x, CAMP.h + 4, CAMP.z), ext: 76, reach: 80 }, // camp, palisade and the drill ring
  ];
  let gameTime = 0;

  function frame() {
    requestAnimationFrame(frame);
    const dtReal = Math.min(clock.getDelta(), 0.05);
    pad.poll(); padInput(); pad.update(dtReal);
    hud.device(pad.active || XBOX);
    const running = player.enabled || !state.started || state.over;
    const paused = state.started && !player.enabled && !state.over;

    // kill-cam time dilation
    if (kc.active) {
      kc.t += dtReal;
      const b = kc.bullet;
      if (kc.phase === 0) {
        const d = b.pos.distanceTo(kc.hit.point);
        timeScale = d > 28 ? 0.1 : 0.018;
        if (b.done) { kc.phase = 1; kc.t = 0; kc.from.copy(killCam.position); kc.look.copy(b.pos); }
        else {
          v1.copy(b.vel).normalize();
          v2.crossVectors(v1, camera.up).normalize();
          const orbit = kc.t * 0.35 + 0.5;
          killCam.position.copy(b.pos).addScaledVector(v1, -1.25).addScaledVector(v2, Math.sin(orbit) * 0.42).addScaledVector(camera.up, 0.1 + Math.cos(orbit) * 0.12);
          v3.copy(b.pos).addScaledVector(v1, 4);
          killCam.lookAt(v3);
        }
      } else {
        timeScale = 0.22;
        v1.subVectors(kc.from, kc.look).normalize();
        v2.crossVectors(v1, camera.up).normalize();
        killCam.position.copy(kc.from).addScaledVector(v2, kc.t * 0.5).addScaledVector(v1, -kc.t * 0.35);
        killCam.position.y += kc.t * 0.12;
        killCam.lookAt(kc.look.x, kc.look.y - 0.3, kc.look.z);
        if (kc.t > 2.1) endKillCam();
      }
    }
    kc.cooldown = Math.max(0, kc.cooldown - dtReal);

    const dt = paused ? 0 : dtReal * timeScale;
    gameTime += dt;
    U.uTime.value += paused ? 0 : dtReal * Math.max(timeScale, 0.25);

    if (!paused) {
      if (!kc.active) { aimFriction(); player.update(dtReal); }
      if (player.swimming) weapon.wantAds = false;
      if (!kc.active && state.started) capture.update(dtReal);
      if (capture.done && state.endT < 0 && !state.over) { state.endT = 3; state.win = true; }
      veg.update(player.pos.x, player.pos.z);
      edgeT -= dtReal; spotT -= dt;
      if (spotT <= 0 && state.started && !state.over && !kc.active) { spotT = 0.5; watch(); }
      weapon.update(dtReal, dt, !kc.active && state.started);
      legion.update(dt, gameTime);
      if (state.started && !state.over) state.time += dt;
    }
    const cam = kc.active ? killCam : camera;
    fx.update(dt, cam, innerHeight);
    sky.update(cam);

    // rangefinder + adaptive shadow focus
    const scoped = weapon.scoped && !kc.active;
    rangeT -= dtReal;
    if (scoped && rangeT <= 0) { rangeT = 0.12; range = measure(); }
    if (kc.active) sky.setShadow(shadowC.copy(kc.hit.point).divideScalar(20).round().multiplyScalar(20), 60);
    else if (scoped && weapon.zoom >= 6) {
      // Through the scope, sharpen the sun shadows on whichever landmark the barrel points at. The box is fixed per
      // landmark (not centred on the aim point), so it cannot jump as the reticle sways across walls, gaps and sky.
      camera.getWorldDirection(v1);
      let pick = null, bestD = 1e9;
      for (const a of SHADOW_ANCHORS) {
        const t = v2.subVectors(a.c, camera.position).dot(v1);
        const d = t > 0 ? v2.addScaledVector(v1, -t).length() : v2.length();
        if (d < a.reach && d < bestD) { bestD = d; pick = a; }
      }
      if (pick) sky.setShadow(pick.c, pick.ext); else sky.setShadow(shadowC.set(60, 25, 10), 200);
    } else sky.setShadow(shadowC.set(60, 25, 10), 200);

    // HUD
    if (state.started) {
      hud.objective(legion.total - legion.alive, legion.total);
      hud.alert(legion.alertLevel, legion.alarm);
      hud.ammo(weapon.mag, weapon.reserve); hud.charges(weapon.charges);
      hud.stance(player.stance);
      hud.status(weapon.reloadT > 0 ? 'RELOADING' : weapon.mag === 0 ? (weapon.reserve ? (pad.active ? 'PRESS X TO RELOAD' : 'PRESS R TO RELOAD') : 'NO AMMUNITION') : '');
      hud.crosshair(weapon.ads < 0.3 && !kc.active);
      let wind = '';
      if (scoped) {
        v1.set(1, 0, 0).applyQuaternion(camera.quaternion);
        const cross = WIND.dot(v1), sp = (WIND.length() * 5).toFixed(1);
        wind = `${cross > 0.15 ? '→' : cross < -0.15 ? '←' : '·'} ${sp} m/s`;
      }
      hud.scope(scoped, { range, zero: weapon.zero, zoom: weapon.zoom, wind, breath: player.breath, exhausted: player.exhausted > 0 });
      if (state.endT > 0 && !kc.active) {
        state.endT -= dtReal;
        if (state.endT <= 0) {
          state.over = true; player.enabled = false;
          document.exitPointerLock && document.exitPointerLock(); pad.stop();
          hud.end(state.win, { ...weapon.stats, kills: legion.total - legion.alive, total: legion.total, time: state.time, alarms: state.alarms, charges: 8 - weapon.charges });
        }
      }
    } else {
      // title screen: slow establishing drift over the valley
      const a = performance.now() * 0.00003;
      camera.position.set(SPAWN.x + Math.sin(a) * 40, groundAt(SPAWN.x, SPAWN.z) + 9, SPAWN.z + 14);
      camera.lookAt(0, 44, 0);
    }

    const g = post.grade;
    g.uTime.value = performance.now() * 0.001;
    g.uScope.value = kc.active ? 0 : THREE.MathUtils.smoothstep(weapon.ads, 0.7, 0.9);
    g.uFlash.value = weapon.scopeFlash * (weapon.scoped ? 0.5 : 0.12);
    g.uCine.value += ((kc.active ? 1 : 0) - g.uCine.value) * Math.min(1, dtReal * 8);
    post.renderPass.camera = cam;
    post.composer.render(dtReal);
  }

  // warm up shaders, then hand over
  hud.loading(0.95, 'Polishing the lens…'); await nextFrame();
  camera.position.set(SPAWN.x, groundAt(SPAWN.x, SPAWN.z) + 9, SPAWN.z + 14); camera.lookAt(0, 44, 0);
  weapon.model.visible = false;
  renderer.compile(scene, camera);
  hud.loading(1, ''); hud.ready();
  frame();
  if (params.has('debug')) window.PAX = { scene, camera, player, weapon, legion, renderer, state, castle, capture, pad, world };
}

boot().catch((e) => {
  console.error(e);
  document.getElementById('loadText').textContent = `Failed to start: ${e.message}`;
});
