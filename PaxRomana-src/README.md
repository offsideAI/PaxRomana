# Pax Romana

A one-level browser sniper mission. Rome never fell; in 1214 her legions still garrison the
châteaux of Gaul. Thin out the garrison of Château de Montvallon from a ridge 400 m across the valley, then break in and steal the Crown Jewels from the keep vault.

Three.js + Vite. Every asset (stone, slate, terrain, foliage, soldiers, rifle, sound) is generated
procedurally at load — there are no image, model or audio files.

## Run

```sh
npm install
npm run dev      # then open the printed URL (http://localhost:5173)
```

`npm run build && npm run preview` serves the production bundle. Append `?low` to the URL on weaker
GPUs (smaller textures, less grass, 1× pixel ratio); `?debug` exposes `window.PAX`.

## Controls

| Input | Action |
| --- | --- |
| F (toggle) / RMB (hold) | Scope |
| Space (or Enter / X) / LMB | Fire, skip kill-cam |
| + and − / mouse wheel | Zoom 4× – 16× |
| Arrow keys / mouse | Look and aim |
| H | Toggle the controls overlay |
| Q / E | Sight zero, 100 – 600 m |
| Shift | Hold breath (scoped) / sprint |
| C / Z | Crouch / prone (press again to stand) |
| R | Reload |
| O | Open / close the nearest door |
| L | Put the nearest loot in the bag |
| WASD, Esc, Enter | Move, pause, resume |

## Mission

Break into Château de Montvallon, find the key to the keep vault, take the Crown Jewels and carry them back out through
the gate. The castle gate, great hall, chapel, keep and vault each have a door (`O`); the vault is locked until you have
its key. Loot (`L`) goes into the loot bag shown under the objective; the silver, chalice and coffer are optional extras.
Killing the garrison is no longer required, only useful.

You start on the ridge but can roam the whole valley on foot: down to the river, across the bridge (or swim, slowly and
without your rifle shouldered), up to the castle walls and into the legion's camp through its south gate. Walls, towers,
tents, the palisade, trees and boulders are solid. Sentries who get a clear look at you within about 45 m (28 m crouched,
13 m prone) raise the alert.

## Layout

| File | Role |
| --- | --- |
| `src/main.js` | Boot, game loop, kill-cam, win/lose |
| `src/terrain.js` | Height function, dense inner tile + coarse outer ring, splat shader, river |
| `src/sky.js` | Physical sky, IBL, clouds, sun and adaptive shadow frustum |
| `src/castle.js` | Château, bridge, legion camp, ruined wall (merged geometry, bullet colliders, walk-blocking footprints) |
| `src/vegetation.js` | Instanced grass in cells that follow the player, oak / stone pine / cypress, boulders |
| `src/soldier.js`, `src/legion.js` | Legionary model, animation, hit volumes; patrol routes and alert logic |
| `src/weapon.js`, `src/player.js` | Rifle view model, ballistics (drop, wind, flight time), movement, sway, breath |
| `src/heist.js` | Doors, loot bag and mission stages |
| `src/collision.js` | Player collision against circles, boxes and the palisade ring |
| `src/effects.js`, `src/audio.js`, `src/post.js`, `src/hud.js` | Particles and birds, synthesised audio, HDR post chain, HUD |
| `src/textures.js`, `src/noise.js`, `src/config.js` | Procedural textures, noise, shared constants |
