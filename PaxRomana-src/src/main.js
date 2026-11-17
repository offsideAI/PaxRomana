// PAX ROMANA — a one-level sniper mission. Entry point and game loop.
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

const hud = new HUD();
const params = new URLSearchParams(location.search);
const LOW = params.has('low');
const nextFrame = () => new Promise((r) => requestAnimationFrame(() => setTimeout(r, 0)));

async function boot() {
  const canvas = document.getElementById('c');
  const renderer = new THREE.WebGLRenderer({ canvas, antialias: false, powerPreference: 'high-performance' });
  renderer.setPixelRatio(Math.min(devicePixelRatio, LOW ? 1 : 1.5));
  renderer.setSize(innerWidth, innerHeight);
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 0.62;
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFSoftShadowMap;

  const scene = new THREE.Scene();
  const camera = new THREE.PerspectiveCamera(BASE_FOV, innerWidth / innerHeight, 0.07, 30000);
  scene.add(camera);
  const killCam = new THREE.PerspectiveCamera(48, innerWidth / innerHeight, 0.05, 30000);

  hud.loading(0.05, 'Quarrying tuffeau limestone…'); await nextFrame();
  const tex = makeTextures(LOW ? 512 : 1024);
  hud.loading(0.3, 'Raising the hills of Gaul…'); await nextFrame();
  scene.add(buildTerrain(tex));
  const sky = buildSky(scene, renderer);
  hud.loading(0.5, 'Building Château de Montvallon…'); await nextFrame();
  const castle = buildCastle(tex);
  scene.add(castle.group);
  hud.loading(0.65, 'Planting oak, pine and cypress…'); await nextFrame();
  const veg = buildVegetation(tex, { grassCount: LOW ? 50000 : 130000 });
  scene.add(veg.group);
  hud.loading(0.85, 'Mustering the legion…'); await nextFrame();
  const legion = new Legion(scene, castle);
  const fx = new FX(scene);
  castle.smoke.forEach((p, i) => fx.addSmoke(p, i < 2 ? 1 : 1.5));
  const audio = new Audio();
  const player = new Player(camera, canvas, [...castle.solids, ...veg.obstacles], audio, legion.soldiers);
  const world = { colliders: castle.colliders, legion, fx, audio, onKill: null, onMiss: null };
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
    hud.feed('<b>ALARM</b> — the cornu sounds. The garrison is running.', 'warn');
  };
  world.onKill = (r) => {
    hud.hitmarker(r.head);
    const who = r.soldier.centurion ? 'Centurion' : 'Legionary';
    hud.feed(`${who} down <em>${Math.round(r.dist)} m</em>${r.head ? ' <b>HEADSHOT</b>' : ''}`, r.head ? 'head' : '');
    if (legion.alive === 0) { state.endT = 3.2; state.win = true; }
  };

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

  // ---- input (every mouse action has a keyboard twin) ----
  function trigger() {
    if (kc.active) { if (kc.t > 0.4) endKillCam(); return; }
    if (player.swimming) { hud.hint('You cannot shoot while swimming', 1800); return; }
    tryFire();
  }
  canvas.addEventListener('mousedown', (e) => {
    if (!player.locked || !player.enabled) return;
    if (e.button === 0) trigger();
    if (e.button === 2 && !kc.active) weapon.wantAds = true;
  });
  addEventListener('mouseup', (e) => { if (e.button === 2) weapon.wantAds = false; });
  addEventListener('contextmenu', (e) => e.preventDefault());
  addEventListener('wheel', (e) => { if (player.enabled && weapon.ads > 0.5) weapon.cycleZoom(e.deltaY < 0 ? 1 : -1); }, { passive: true });
  player.onKey = (code) => {
    if (code === 'KeyR') weapon.reload();
    if (code === 'BracketRight' || code === 'KeyE') weapon.cycleZero(1);
    if (code === 'BracketLeft' || code === 'KeyQ') weapon.cycleZero(-1);
    if (code === 'KeyF' && !kc.active) weapon.wantAds = !weapon.wantAds;
    if (code === 'Space' || code === 'Enter' || code === 'NumpadEnter' || code === 'KeyX') trigger();
    if (code === 'Equal' || code === 'NumpadAdd') weapon.cycleZoom(1);
    if (code === 'Minus' || code === 'NumpadSubtract') weapon.cycleZoom(-1);
    if (code === 'KeyH') hud.toggleKeys();
  };
  let edgeT = 0;
  player.onEdge = () => { if (edgeT <= 0) { edgeT = 6; hud.hint('Edge of the valley — the mission lies behind you', 3000); } };

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
      if (!spotted) hud.feed('<b>SPOTTED</b> — a sentry has seen you', 'warn');
    }
    spotted = seen;
  }

  player.onLock = (locked) => {
    if (state.over) return;
    player.enabled = locked;
    hud.showMenu(!locked, state.started);
    if (!locked) { weapon.wantAds = false; hud.el.start.focus(); }
  };
  hud.el.start.addEventListener('click', () => {
    audio.init();
    player.lock();
    if (!state.started) {
      state.started = true;
      setTimeout(() => hud.hint('Press H for all keyboard and mouse controls', 7000), 1200);
    }
  });
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
      if (!kc.active) player.update(dtReal);
      if (player.swimming) weapon.wantAds = false;
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
      hud.ammo(weapon.mag, weapon.reserve);
      hud.stance(player.stance);
      hud.status(weapon.reloadT > 0 ? 'RELOADING' : weapon.mag === 0 ? (weapon.reserve ? 'PRESS R TO RELOAD' : 'NO AMMUNITION') : '');
      hud.crosshair(weapon.ads < 0.3 && !kc.active);
      let wind = '';
      if (scoped) {
        v1.set(1, 0, 0).applyQuaternion(camera.quaternion);
        const cross = WIND.dot(v1), sp = (WIND.length() * 5).toFixed(1);
        wind = `${cross > 0.15 ? '→' : cross < -0.15 ? '←' : '·'} ${sp} m/s`;
      }
      hud.scope(scoped, { range, zero: weapon.zero, zoom: weapon.zoom, wind, breath: player.breath, exhausted: player.exhausted > 0 });
      if (!state.over && weapon.mag === 0 && weapon.reserve === 0 && weapon.bullets.length === 0 && legion.alive > 0 && state.endT < 0) { state.endT = 2; state.win = false; }
      if (state.endT > 0 && !kc.active) {
        state.endT -= dtReal;
        if (state.endT <= 0) {
          state.over = true; player.enabled = false;
          document.exitPointerLock && document.exitPointerLock();
          hud.end(state.win, { ...weapon.stats, kills: legion.total - legion.alive, total: legion.total, time: state.time, alarms: state.alarms });
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
  if (params.has('debug')) window.PAX = { scene, camera, player, weapon, legion, renderer, state, castle };
}

boot().catch((e) => {
  console.error(e);
  document.getElementById('loadText').textContent = `Failed to start: ${e.message}`;
});
