# PRD: Pax Romana: Tertia Roma, native C# edition for Xbox (MonoGame)

| | |
| --- | --- |
| Product | Pax Romana: Tertia Roma (Native) |
| Source | `PaxRomana-XBOX-CSharp-src/` |
| Platform | Xbox in Developer Mode (UWP, x64); also runs on macOS, Windows and Linux as a desktop build |
| Technology | C# 7.3, MonoGame 3.8.1.303, no web view, no JavaScript, no asset files |
| Document date | 2026-11-18 |
| Status | Implemented and committed (3575a97). Rules tested and rendering checked on a Mac. The Xbox package builds on GitHub Actions. **Nothing has been installed or run on the console yet** (see [section 9](#9-status-and-acceptance)). |

This document has two parts. Sections 1 to 9 are the product requirements: what the game is and how it must behave.
Section 10 onwards is the developer documentation: how the code is organised and how to build, test, install and
change it.

---

## 1. Summary

Pax Romana: Tertia Roma is a single-player, first-person sniper game. Rome never fell; in 2187 the legions of the
Third Rome hold the Kremlin above a frozen, neon-lit Moscow. The player starts on a ridge across the Moskva river,
clears the defenders inside the Kremlin walls and raises a standard at the flagpole in the courtyard.

This edition is a rewrite in C# of the Xbox game in `PaxRomana-XBOX-src/`, which is a Three.js game inside a UWP
WebView2 wrapper. It keeps the same rules, controls and haptics and draws a simpler picture.

## 2. Background and problem

The web edition does not start on the target console. It reports:

> A suitable version of Microsoft Edge WebView 2 Runtime was not detected. Could not start the game view. Object
> reference not set to an instance of an object.

The WebView2 runtime is not present on the console, and the game cannot install it. Any design that hosts the game in
a web view has the same dependency.

The native edition removes that dependency. The console runs compiled C# on MonoGame, which talks to Direct3D 11,
XAudio2 and the Xbox controller API directly.

## 3. Goals and non-goals

### Goals

1. **Runs on the console without WebView2 or any other runtime that must be installed separately.**
2. **Feature parity with the web edition** in rules, mission flow, controls, HUD content and haptics.
3. **Playable entirely with an Xbox controller**, with text readable on a television and inside the title-safe area.
4. **Buildable without a Windows machine**: GitHub Actions builds the package; a script installs it from any computer.
5. **Testable without a console**: the rules run headless, and the same game runs in a desktop window.
6. **No asset files**: textures, geometry, fonts and sound are generated in code, so the package has nothing to load
   from disk except the app tiles.

### Non-goals

- Visual parity with the web edition. Real-time shadows, bloom, tone mapping, film grain, scope lens distortion, wind
  sway, water reflections and dynamic point lights are out of scope (see [section 7](#7-parity-with-the-web-edition)).
- Store publication. The package is self-signed for sideloading in Developer Mode.
- Multiplayer, saved games, settings menus, difficulty levels, localisation.
- Enemies that shoot back or a way to lose the mission. Neither edition has one (see [section 8](#8-known-gaps)).
- Replacing or changing the web edition. `PaxRomana-XBOX-src/` is untouched and installs as a separate app.

## 4. Users and platforms

| Platform | Purpose | Input |
| --- | --- | --- |
| Xbox in Developer Mode (the development console is an Xbox Series S) | The product | Xbox controller |
| Windows PC, same UWP package | Side effect of the UWP build; behaves like the desktop build | Controller, keyboard and mouse |
| macOS, Windows, Linux desktop build | Trying the game and checking rendering without a console | Controller, keyboard and mouse |

The player is someone with a console in Developer Mode who sideloads the game. There is no account, network access or
data collection: the app manifest declares no capabilities.

## 5. Functional requirements

### 5.1 Mission flow

| Stage | Objective text | Condition |
| --- | --- | --- |
| Title | Menu panel over a slow camera drift above the valley | Until **A** (or Enter / click) once loading has finished |
| 0 | Breach the Kremlin: open the Spasskaya gate, or blow it in | Defenders alive inside the walls, player outside |
| 1 | Clear the defenders inside the walls: *n* left | Defenders alive inside the walls, player inside |
| 2 | Raise your standard at the flagpole in the courtyard | No defenders inside the walls, player away from the flagpole |
| 3 | Hold the flagpole | No defenders inside the walls, player at the flagpole |
| 4 | The Kremlin is yours | Standard raised |
| End | Statistics and rating, 3 s after the capture | **A** / Menu (or Enter / click) starts a new mission |

- "Inside the walls" means |x| < 48.5 m and |z| < 41.5 m around the Kremlin's centre. Only those defenders block the
  capture; the camp, road, bridge and watchtower garrison do not have to be killed.
- The flagpole is at (0, 15). The player must stand within 7 m of it on the plateau for **8 seconds**. Progress drains
  over 14 s when the player steps away. While a defender is alive inside the walls the bar shows "Contested: *n*
  defenders inside the walls" and does not fill.
- When two or fewer defenders remain inside, a hint says where they are (in the courtyard, on the wall-walk, on the
  gate tower, inside the Senate, on the Senate roof).
- Pausing (Menu / Esc) freezes the mission and shows the same panel with a Resume button. Losing focus pauses too.
- A new mission restores the doors, the flag, the garrison and the ammunition. The world itself is built once.

### 5.2 World

All coordinates are metres; +Y is up; the Kremlin is at the origin; the water surface is y = 0.

| Feature | Requirement |
| --- | --- |
| Valley | 1,200 m square playable heightfield (3 m cells) with a coarse outer ring to the horizon. Snow, slush on the roads and bare rock on slopes. |
| Kremlin | Plateau at 30 m. Red brick curtain walls 13 m high with wall-walks, corner and mid-wall towers with red stars, the Spasskaya gatehouse, the Senate (with a command vault), the Arsenal, a cathedral with gold onion domes, courtyard furnishings, banners lettered SPQR. |
| Doors | Five, each openable by hand or destroyed by a charge: the Spasskaya gate (two leaves), the Arsenal door, the cathedral door, the Senate door, the command vault door (iron). |
| River and bridge | The Moskva runs east to west at about z = 210. A stone bridge crosses it at x = 55, z = 180 to 260, deck at 3.4 m. |
| Camp | The legion's forward base at (170, 10): palisade ring of radius 52 m with a south gateway, tents, fires, a watchtower. |
| Sniper's nest | Spawn at (120, 412) beside a ruined wall, about 390 m from the gate. |
| Skyline | 85 towers of a future Moscow with lit windows and red beacons, and an orbital tether. Scenery only. |
| Vegetation | Three tree species (about 1,150 in the valley, 1,500 beyond it), 420 boulders, frosted grass around the player. Trees and boulders block movement. |
| Sky and weather | Low winter sun, sky dome, drifting cloud deck, violet distance haze, falling snow around the camera, two flocks of birds, chimney and campfire smoke. |

The world is laid out by a seeded generator, so it is identical on every run and on every platform.

### 5.3 Player

| Property | Standing | Crouched | Prone |
| --- | --- | --- | --- |
| Eye height | 1.70 m | 1.08 m | 0.42 m |
| Speed | 4.2 m/s | 2.1 m/s | 0.8 m/s |
| Scope sway (relative) | 1 | 0.52 | 0.2 |
| Distance at which a sentry notices the player | 45 m | 28 m | 13 m |

- **Free roam** over the whole valley, with collision against walls, towers, closed doors, trees, boulders, tents and
  living soldiers. The playable area stops 14 m inside the edge of the valley, with a hint when the player reaches it.
- **Sprint** (standing, moving forward, not scoped): 1.6 times walking speed. Sprinting lowers the rifle.
- **Scoped movement** is 35% of normal speed.
- **Water**: wading slows the player by up to 58%. Where the river bed is deeper than 1.35 m the player swims, cannot
  crouch and cannot shoot ("You cannot shoot while swimming"). Sentries notice a swimmer at half the usual distance.
- **Bridge**: walk on from either bank; parapets keep the player on the deck; the piers block a swimmer.
- **Breath**: holding breath while scoped cuts sway to 12% for up to 6.5 s, with a heartbeat every 0.85 s. Running out
  leaves the player exhausted for 3.2 s with sway 2.6 times normal. Breath recovers in 5 s.

### 5.4 Rifle and ballistics

| Property | Value |
| --- | --- |
| Magazine / reserve | 5 rounds / 40 rounds |
| Explosive charges | 8 per mission |
| Time between shots | 1.25 s (rifle, bolt cycles), 1.5 s (charge) |
| Reload | 2.6 s, lowers the scope |
| Scope zoom | 4×, 6×, **8×** (default), 12×, 16× |
| Sight zero | 100, 200, 300, **400** (default), 500, 600 m |
| Muzzle velocity | 880 m/s |
| Gravity | 9.81 m/s² |
| Wind | Constant lateral acceleration of (0.5, 0, 0.12) m/s² on every projectile |

- Rounds are simulated projectiles, not instant hits: they drop, drift with the wind and take time to arrive (about
  0.45 s to the gate from the nest). The sight zero launches the round slightly high so that it crosses the line of
  sight at the chosen range.
- Fired from the hip, a round scatters by up to 0.022 rad. Through the scope it goes where the reticle points, subject
  to sway.
- A round stops at the first thing it meets: a soldier, masonry or timber, a door leaf, the ground or the water. A
  round that hits nothing is retired after 4 s.
- Soldiers have three hit volumes: head (headshot), torso and legs. Any hit kills. A headshot knocks the helmet off.
- Impact sounds and the explosion's sound and rumble are delayed by the distance divided by the speed of sound.
- **Scope view**: circular glass with black surround, a mil-dot reticle, and readouts for RANGE (laser rangefinder
  along the line of sight), ZERO, ZOOM, WIND and a BREATH bar.
- **Explosive charges** fly the same trajectory as a round and detonate on impact. A charge kills every defender within
  8.5 m, destroys any door within 5.5 m for the rest of the mission, shakes the camera when close, and raises the
  garrison's alert sharply.
- An empty magazine gives a dry click and starts a reload if rounds remain. No charges left gives the hint "No
  explosive charges left".

### 5.5 Garrison

29 soldiers: 28 legionaries and one centurion (red cloak, transverse crest).

| Post | Count | Behaviour |
| --- | --- | --- |
| South and north wall-walks | 5 | Patrol back and forth |
| Foot of the south wall, camp road | 2 | Patrol |
| Gatehouse roof and gate, ground level | 4 | Stand still until alerted |
| Senate roof | 2 | Walk a loop |
| Road patrol | 2 | Walk as a pair |
| Bridge | 1 | Patrol the deck |
| Drill squad around the camp | 7 | Centurion leading six legionaries in a loop |
| Watchtower | 1 | Lookout, stands still |
| Camp | 3 | Two by the fire, one walking |
| Courtyard and Senate vault | 2 | One crossing the courtyard, one guarding the vault |

**Alert.** The garrison has an alert level from 0 to 100, shown as a bar under the objective. It falls by 1.6 per
second when nothing happens.

| Event | Alert raised | Soldiers disturbed |
| --- | --- | --- |
| Rifle shot | 3 | None directly |
| Charge fired | 6 | None directly |
| Round lands near soldiers | 7 | Within 9 m, for 9 s |
| Kill | 14 (9 for a headshot) | Within 16 m, for 14 s |
| Explosion | 40 | Within 70 m, for 25 s |
| Player seen by a sentry (checked twice a second, needs a clear line of sight) | 14 | Around the player, for 12 s |

At 100 the **alarm** sounds: a klaxon, a "ALARM" feed entry, and every soldier runs their route for 45 s. Afterwards
the level drops to 55. Disturbed soldiers move at about 3.3 to 4.1 m/s instead of 1.0 to 1.35 m/s and scan with their
heads; soldiers on hold posts start to move.

Soldiers do not attack. See [section 8](#8-known-gaps).

### 5.6 Kill-cam

- A shot is eligible when its predicted hit is more than 90 m away and not blocked by masonry. It is shown always for
  the last defender in the garrison, with 65% probability for a headshot, and with 22% probability otherwise.
- The camera follows the round in slow motion (10% speed, 1.8% for the last 28 m), then holds on the fall for 2.1 s at
  22% speed. The HUD fades out and letterbox bars slide in.
- **A** or **RT** skips it after 0.4 s. Skipping must not cancel the kill: the round flies on at full speed.
- At least 1 s passes before another kill-cam can start.

### 5.7 HUD

All HUD elements stay inside the title-safe area: 5% in from the sides, 6% from the top and bottom. Sizes are defined
at 720 lines and scale with the screen height.

| Element | Position | Content |
| --- | --- | --- |
| Objective | Top left | Objective text, kills / total "LEGIONARIES DOWN", alert bar (flashes "ALARM" during an alarm) |
| Capture bar | Top centre | Progress and "Raising your standard *n*%", "Contested: …" or "Kremlin captured" |
| Feed | Top right | Last five events, each for about 5 s: kills with range, HEADSHOT, Blast *n* DOWN, BREACH, SPOTTED, ALARM, WALLS CLEAR |
| Door prompt | Lower centre | Key cap (A or O) and "Open / Close the Spasskaya gate" when a door is in reach |
| Hint | Lower centre | Timed messages, upper case |
| Crosshair, hit marker | Centre | Crosshair at the hip; a four-tick marker on a kill, red for a headshot |
| Ammunition | Bottom right | Magazine and reserve, five round pips, eight charge pips, stance, status (RELOADING, PRESS X TO RELOAD, NO AMMUNITION) |
| Controls overlay | Centre | Table of every control for controller and keyboard (View / H) |
| Menu | Centre | Title, premise, mission, controls for the active input device, button, loading bar |
| End screen | Centre | Kills, accuracy, headshots, longest shot, time, alarms raised, charges used, rating, "DEPLOY AGAIN" |

- Prompts follow the last input device used. On the console they are controller prompts from the first frame.
- Ratings on a win: **Umbra · Ghost of the Moskva** (accuracy at least 85% and no alarm), **Sagittarius · Marksman**
  (accuracy at least 65%), otherwise **Tiro · Recruit**.
- The first hint, 1.2 s after the mission starts, lists the main controls.

### 5.8 Controls

| Action | Controller | Keyboard and mouse (desktop) |
| --- | --- | --- |
| Move / sprint | Left stick / click it | W A S D / Shift |
| Look and aim | Right stick | Mouse or arrow keys |
| Scope | LT (hold) | Right button (hold) or F (toggle) |
| Fire the rifle | RT | Left button, Space or Enter |
| Fire an explosive charge | RB | Middle button or G |
| Hold breath (scoped) | LB | Shift |
| Zoom | D-pad up / down, or Y to cycle | Wheel (scoped) or + / − |
| Sight zero | D-pad left / right | Q / E or [ / ] |
| Reload | X | R |
| Open or close the nearest door | A | O |
| Skip the kill-cam | A or RT | Left button, Space or Enter |
| Crouch / prone | B / click right stick | C / Z |
| Show all controls | View | H |
| Pause | Menu | Esc |
| Start, resume, play again | A or Menu | Enter, Space or click |
| Quit (desktop only) | | Esc on the menu |

**Aiming with a thumbstick** must make a 400 m shot fair:

- Radial dead zone of 0.16, rescaled so movement starts from zero at its edge.
- Squared response curve for fine aim; vertical sensitivity is 72% of horizontal.
- At the hip, holding the stick hard over for 0.45 s adds up to 70% turn speed.
- Through the scope, turn speed scales down with magnification.
- **Aim friction**: with a controller, turn speed drops to 62% when the line of sight passes within 1.1 m of a living
  defender and to 40% within 0.5 m.

### 5.9 Haptics

All four motors of the Xbox controller are used: the heavy and light motors in the grips and the two impulse motors in
the triggers. Strengths are 0 to 1.

| Event | Grip, heavy | Grip, light | Left trigger | Right trigger | Length | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| Rifle shot: trigger breaks | | | | 1.0 | 70 ms | |
| Rifle shot: recoil | 0.95 | 0.75 | | | 210 ms | Fades out |
| Rifle shot: bolt back, bolt home | | 0.28, 0.34 | | | 35 ms each | 520 ms and 930 ms after the shot |
| Charge launched | 1.0 | 0.5 | | 0.9 | 300 ms | Fades out |
| Explosion | *k* | 0.7 *k* | 0.4 *k* | 0.4 *k* | 380 + 520 *k* ms | *k* = 1.1 − distance / 420, between 0.22 and 1. Arrives with the sound. Fades out |
| Kill | | 0.4 | | | 60 ms | |
| Headshot | | 0.7 | 0.5 | | 110 ms | |
| Dry fire | | | | 0.3 | 35 ms | |
| Scope raised | | | 0.25 | | 30 ms | |
| Holding breath | 0.22, then 0.15 | | | | 70 ms each | Two-beat heartbeat every 0.85 s |
| Reload | | 0.3 | 0.15 | | 40 ms × 4 | At 250, 1,050, 1,850 and 2,450 ms |
| Door opened or closed | 0.35 | | | | 160 ms | Fades out |
| Spotted by a sentry | 0.4 | 0.2 | | | 180 ms | Fades out |
| Alarm | 0.35 | | | | 500 ms × 2 | 700 ms apart, fading out |
| Walls clear | 0.5 | 0.5 | | | 260 ms | Fades out |
| Raising the standard | | 0.12 rising to 0.42 | | | 45 ms | Every 0.5 s |
| Kremlin captured | 0.9 | 0.9 | 0.5 | 0.5 | 900 ms | Fades out |

Rumble stops when the game is paused, loses focus, ends or exits.

### 5.10 Sound

19 clips, synthesised at start-up: rifle report with a valley echo, bolt, reload, dry click, three impact sounds
(flesh, stone, dirt), kill tick and headshot tick, klaxon horn, heartbeat, charge launch, explosion, door, capture
chime, three footsteps and a looping wind bed. Impacts and explosions are quieter with distance and arrive late by the
time sound takes to travel. Sound is mono. If the device has no audio output the game plays silently.

### 5.11 Start-up, loading and errors

- The title panel appears at once with a loading bar and six progress messages ("Firing Kremlin brick…", "Laying snow
  over the Moskva valley…", "Raising the Kremlin and the skyline…", "Frosting the birch and pine…", "Mustering the
  legion…", "Polishing the lens…"). The world and the sound are built on a background thread.
- The button reads LOADING… until the world is ready, then TAKE THE SHOT.
- **Error screen.** If loading, a game frame or a draw call throws, the game shows "The game hit an error and had to
  stop. Please pass on the text below." followed by the first 1,100 characters of the error. On a console an unhandled
  error would otherwise close the app with nothing to report.

## 6. Non-functional requirements

| Area | Requirement |
| --- | --- |
| Dependencies | No WebView2, no JavaScript engine, no content pipeline, no shader compiler, no files loaded at run time. The package contains the app, MonoGame, the .NET Native runtime packages and five tile images. |
| Resolution | Draw edge to edge at the display's real resolution on the console. The window is resizable on desktop. |
| Anti-aliasing | 4× multisampling of the 3D scene, with a fallback to none if the device refuses it. The HUD is drawn at full sharpness on top. |
| Frame pacing | Variable time step, synchronised to the display refresh. A frame longer than 50 ms is treated as 50 ms. |
| Performance | Smooth on an Xbox Series S. Not yet measured on the console. On the development Mac the scripted check averaged about 0.07 ms per update and 0.73 ms per draw at 1280 × 720. |
| Loading | World geometry builds in under half a second on the development Mac; textures and sound add to that. Not yet measured on the console. |
| Determinism | The world is identical on every run. Mission events that use chance (hip-fire scatter, which shots get a kill-cam, patrol pauses) are not seeded. |
| Privacy | No network access, no capabilities declared, nothing stored. |
| Identity | Package `PaxRomana.TertiaRoma.Native`, publisher `CN=PaxRomana`, display name "Pax Romana: Tertia Roma (Native)". Different from the web edition, so both can be installed. |
| OS versions | Minimum Windows 10.0.17763, tested target 10.0.22621. |
| Robustness | Controller disconnects are handled (the first connected controller is used). Missing audio device and missing haptics are not errors. |

## 7. Parity with the web edition

### The same

The valley and the Kremlin and its rooms; the 29-soldier garrison, its routes and its alert rules; free roam with
collision, wading, swimming and the bridge; ballistics with drop, wind and sight zero; the rangefinder; the kill-cam;
explosive charges and breaching; the five doors; the capture; every HUD element, menu and end-screen statistic; the
controller layout; aim friction; every haptic cue; the keyboard and mouse controls.

### Different, by design

This edition uses only MonoGame's two built-in shaders so that nothing has to be compiled or loaded from files and the
first console build has the best chance of working.

| Web edition | Native edition |
| --- | --- |
| Real-time shadows | None; soldiers get a soft blob shadow |
| Bloom, tone mapping, film grain, colour grading | None; a vignette and a muzzle / blast glare overlay only |
| Scope lens distortion and colour fringing | A flat scope mask |
| Grass and leaves sway in the wind | Static; banners and flags do wave |
| Water with reflections | A tinted, drifting sheet |
| Torches and explosions light the walls around them | They glow but cast no light |
| Stereo sound with panning, birdsong | Mono, no birdsong |
| HUD in HTML and CSS | HUD drawn with sprites and baked fonts, sized for a television |
| Any error leaves a blank view | On-screen error report |

### Added

A desktop build, a headless rules test, a compile-only check against the console's API limits, and a GitHub Actions
workflow that runs the test before it builds the package.

## 8. Known gaps

| Gap | Detail |
| --- | --- |
| **No way to lose** | Soldiers patrol, run and raise the alarm but never attack, and the player has no health. The end screen has "MISSION FAILED" text that nothing can reach. This matches the web edition. |
| Alarm has no consequence beyond movement | It makes targets harder to hit and costs the top rating. |
| Wind readout is cosmetic | The scope shows a fixed speed with an arrow for left or right; the wind itself never changes. |
| No settings | No sensitivity, inverted look, volume or vibration options. |
| MonoGame's UWP package is frozen | 3.8.1.303 is the last release that includes the UWP / Xbox target, so it will not receive fixes. |

## 9. Status and acceptance

### Verified

| Check | Result |
| --- | --- |
| Headless rules test (`tests/`), 46 checks | Passes. Last run 2026-11-18. Earlier in development it passed 52 consecutive runs after the last rule fix. |
| Shared code compiles against .NET Standard 2.0 and C# 7.3 (`tools/apicheck`) | Passes |
| Shared code compiles against the real `MonoGame.Framework.WindowsUniversal` 3.8.1.303 assembly | Passed, in a scratch project on the Mac |
| GitHub Actions workflow: rules test on Windows, UWP project and `Program.cs` compile with .NET Native, package signed and uploaded | Passed on the first run, 2026-11-18 (run 44402866842, commit 3575a97) |
| Rendering on the desktop build | 18 scripted views inspected on a Mac: title, nest, low sun, scope, 16× on a guard, kill-cam in flight and on impact, gate breach, courtyard, explosion, smoke, camp and skyline, controls overlay, end screen; and the error screen |

### Not verified

| Item | Why |
| --- | --- |
| The game on the console: start-up, frame rate, resolution, safe area | Nothing has been installed yet |
| Vibration on a real controller, including the trigger motors | Tested only as numbers sent to the controller layer |
| How the sound actually sounds | The test checks only that every clip is generated, audible and not clipped |
| `deploy.sh` for this package | Same script shape as the web edition's, with different names |

### Acceptance criteria for the console

1. The workflow completes and produces the `paxromana-native-xbox-package` artifact. **Met on 2026-11-18.**
2. `deploy.sh` installs it and "Pax Romana: Tertia Roma (Native)" appears in Dev Home.
3. The game starts to the title panel with no WebView2 message, finishes loading, and **A** starts the mission.
4. The picture fills the screen and no HUD text is cut off at the edges.
5. The mission can be completed with the controller alone: scope, kill at range, breach the gate with a charge, clear
   the walls, hold the flagpole, reach the end screen, deploy again.
6. Vibration is felt for a shot (including the right trigger), an explosion and the capture.
7. Sound plays: shot, explosion, klaxon, wind.
8. The frame rate is smooth while turning in the courtyard and looking across the valley through the scope.
9. Pressing the Xbox button and returning resumes on the pause panel with no vibration left running.

If the game shows its error screen instead, the text on it is the bug report.

---

## 10. Developer documentation

### 10.1 Technology

| Piece | Choice | Why |
| --- | --- | --- |
| Language | C# 7.3 | The highest version the UWP toolchain supports |
| Framework | [MonoGame](https://monogame.net) 3.8.1.303 ([source](https://github.com/MonoGame/MonoGame), [docs](https://docs.monogame.net), Ms-PL licence) | The lightest C# option that runs on Xbox in Developer Mode |
| Xbox target | `MonoGame.Framework.WindowsUniversal` (Direct3D 11 through SharpDX, XAudio2, `Windows.Gaming.Input`), UWP, .NET Native in Release | |
| Desktop and tests | `MonoGame.Framework.DesktopGL` on .NET 8 or newer | Same game code, runs anywhere |
| Shaders | `BasicEffect` and `AlphaTestEffect` only | Built into MonoGame: nothing to compile |
| Assets | None | Everything is generated in code |

MonoGame supplies the window, the game loop, triangle drawing, sound playback and controller access. It has no scene
graph, physics, editor or asset format; all of that is in `Game/`.

### 10.2 Repository layout

```
.github/workflows/paxromana-native-xbox.yml   CI: rules test, then the Xbox package
PaxRomana-XBOX-CSharp-src/
  Game/            the whole game, shared by everything below (about 5,700 lines)
  xbox/            UWP app: solution, project, manifest, tiles, 49-line entry point
  desktop/         the same game in a window (macOS, Windows, Linux)
  tests/           headless rules test
  tools/
    makefont.py    bakes the fonts into Game/FontData.g.cs
    fonts/         Cinzel and Barlow Condensed, with their OFL licence texts
    apicheck/      compile-only project: Game/ against .NET Standard 2.0 and C# 7.3
  build.ps1        builds the signed Xbox package on Windows
  deploy.sh        installs the package on the console from any machine
  README.md        player and quick-start documentation
```

The three projects (`xbox`, `desktop`, `tests`) and `apicheck` do not reference each other. Each compiles every file
under `Game/` directly (`<Compile Include="..\Game\**\*.cs" />`), so a new file in `Game/` is picked up by all of
them with no project edits.

### 10.3 Source files in `Game/`

| File | Lines | Contents |
| --- | --- | --- |
| `Core.cs` | 183 | `Config` (world constants), `M` (float maths, colour helpers), `Noise` (hash, value noise, fBm), `Mulberry` (seeded generator), `Solid` and `Collision` (2D footprints the player cannot walk through) |
| `Geo.cs` | 328 | `Geo`: a triangle soup with positions, normals, texture coordinates and colours; primitives (box, cylinder, cone, sphere, plane, torus, capsule and others); transforms. `IndexedMesh`. |
| `Textures.cs` | 510 | `Tex` (CPU image), `Canvas` (2D drawing), `FontFace` (decodes the baked fonts), `Textures.Make` (every texture) |
| `FontData.g.cs` | 40 | Generated. Three font atlases, gzipped and base64-encoded |
| `Terrain.cs` | 177 | `HeightAt`, `MacroAt`, `GroundAt`; builds 16 inner tiles and the outer ring |
| `Castle.cs` | 498 | `Builder` (geometry per material), `Castle` (Kremlin, camp, bridge, ruined wall, doors, banners, flagpole), `City` (skyline, tether) |
| `Vegetation.cs` | 369 | `Lighting` (the light rig), trees and boulders baked into chunks, `Grass` |
| `Colliders.cs` | 155 | Bounding-volume hierarchy over the masonry triangles, plus door leaves; `Raycast`, `Blocked` |
| `Input.cs` | 121 | `Btn`, `Pad` (controller state and the haptics mixer), `K`, `KeyMouse`, `IAudio`, `NullAudio` |
| `Garrison.cs` | 386 | `Route`, `Soldier` (patrol, alert, animation, hit volumes, geometry), `Legion` (the 29 posts, alert level, alarm) |
| `Player.cs` | 215 | `Camera`, `Player` (movement, collision, water, bridge, breath, sway) |
| `Weapon.cs` | 276 | `Bullet`, `Weapon` (firing, ballistics, prediction, reload, view-model pose and geometry) |
| `Effects.cs` | 175 | `Fx`: particles, snow, birds, shockwaves, explosion flash |
| `Hud.cs` | 180 | `Hud` (state only) and `Capture` (doors, defenders inside, the flag, objective stages) |
| `GameSim.cs` | 430 | `World` (built once) and `GameSim` (one mission: input, rules, haptics, kill-cam) |
| `Renderer.cs` | 654 | Uploads meshes and textures; draws the world, the view model and sprites |
| `HudRenderer.cs` | 427 | Draws the HUD, scope, menus, end screen and error screen |
| `Audio.cs` | 256 | `Synth` (offline synthesiser) and `SynthAudio` (playback) |
| `PaxGame.cs` | 280 | The `Game` subclass: window, loader, device input, rumble output, draw |

### 10.4 Architecture

```
                 platform (MonoGame)
                        |
   XboxGame / DesktopGame : PaxGame          window, loading, device input, rumble out, draw
        |              |             |
        |          GameSim        Renderer + HudRenderer
        |        (one mission)        (read-only view of GameSim and Hud)
        |              |
        +--------->  World  (built once, shared by every mission)
```

Three rules hold the design together:

1. **`World` is static, `GameSim` is one mission.** `World.Build` creates terrain, castle, city, vegetation,
   colliders and part geometry once. Starting again constructs a new `GameSim` over the same `World`; `Castle.Reset()`
   restores the doors and the flag.
2. **`GameSim` touches no device.** It reads `Pad` and `KeyMouse`, which the shell fills each frame, and writes `Hud`,
   `Pad.Out` and calls on `IAudio`. It has no `GraphicsDevice`, so the tests construct and drive it directly.
3. **The renderers only read.** `Renderer` and `HudRenderer` draw from `GameSim`, `World` and `Hud` and change
   nothing in them, apart from flags on grass cells that mark vertex data as uploaded.

**One frame** (`PaxGame.Step`, then `PaxGame.Paint`):

1. `ReadPad` and `ReadKeysAndMouse` copy device state into `Pad` and `KeyMouse`.
2. `GameSim.Frame(dt)`:
   - controller input, haptics mixer, keyboard input;
   - kill-cam camera and time scale;
   - player, capture, grass window, sentry watch, weapon and projectiles, garrison;
   - effects, rangefinder, HUD fields, end-of-mission timer.
3. `SynthAudio.Update` plays delayed sounds; `SendRumble` sends `Pad.Out` to the controller if it changed.
4. `Renderer.DrawWorld` and `DrawViewModel` into the multisampled target, which is copied to the screen.
5. `HudRenderer.Draw` on top.

Two clocks run in `GameSim.Frame`: `dtReal` drives the player, the view model, the HUD and haptics; `dt` (real time
multiplied by the kill-cam time scale, zero when paused) drives projectiles, soldiers and effects.

### 10.5 Conventions

| Topic | Convention |
| --- | --- |
| Coordinates | Right-handed, metres, +Y up, −Z forward. The same as the web edition, so positions port unchanged. |
| Winding | Front faces are counter-clockwise; the renderer culls clockwise faces. |
| Rotation order | Three.js Euler XYZ becomes `RotZ * RotY * RotX` here. The player camera is `RotZ(roll) * RotX(pitch) * RotY(yaw) * Translation`. |
| Texture coordinates | In `Geo`, v = 0 is the bottom of the texture. The renderer flips it (`1 − v`) on upload. |
| Colour | `M.Hex(0xRRGGBB)` gives a `Vector3`, `M.Rgb` a `Color`. Geometry is painted with `Geo.Paint`. |
| Maths | Use `M.Sin`, `M.Clamp`, `M.Lerp` and friends. `MathF` and `Math.Clamp` do not exist in .NET Standard 2.0. |
| Language | C# 7.3 only: no switch expressions, no `using` declarations, no nullable reference types, no records, no ranges. |
| Randomness | `Mulberry` (seeded) for anything that shapes the world; `M.Rand()` (unseeded) for mission events. |
| Text | HUD strings may carry `<b>bold</b>`, `<em>dim</em>` and `<k>key cap</k>`. Only characters baked into the fonts can be drawn. |
| Comments | Each file opens with a comment saying what it holds; comments explain why, not what. |

### 10.6 World generation

`World.Build(progress, textures = true, grassCount = 40000)` runs on a background thread. The tests pass
`textures: false` to skip the slowest step. On the development Mac the test's world reports 46,348 collision
triangles, 1,668 solids, 17 terrain tiles, 77 near and 38 far vegetation chunks and 5 doors.

- **Textures** (`Textures.Make`): brick 512, slate, snow, rock, timber, leather, water and cloud 256, bark 128, a
  1024 × 256 leaf atlas, the SPQR banner, a window facade for the skyline, soft and glow discs, a 1024 scope mask, a
  vignette. Images are mip-mapped with an alpha-weighted box filter so cut-out foliage does not get dark fringes.
- **Terrain**: `Terrain.GroundAt(x, z)` is the single source of ground height for everything (player, soldiers,
  bullets, trees). The inner 1,200 m square is 400 × 400 cells split into 16 tiles so each fits 16-bit indices.
  Normals come from the shared grid, so tiles meet without seams.
- **Castle and city**: `Builder` collects geometry under material keys ("stone", "slate", "gold", "timber", "neon"
  and so on) with world-scale texture coordinates so brick courses line up across pieces. The renderer maps each key
  to a material in `Renderer.SetWorld`. Keys listed in `Castle.ColliderKeys` also become collision triangles.
- **Solids**: every wall, tower, building, tent, tree and boulder adds a `Solid` (box, circle, or a ring with a
  gateway) to `World.Solids`. `Collision.Resolve` pushes the player out of them. A door owns a `Solid` that is
  switched off while the door is open or broken.
- **Vegetation**: trees and boulders are merged into chunk meshes on a 150 m grid near the Kremlin and a 700 m grid
  beyond, where trees use lighter variants. Leaf lighting is baked into vertex colours with `Lighting.Bake`.
- **Grass**: a 7 × 7 window of 24 m cells follows the player. When the player crosses a cell boundary, at most one
  stale cell is refilled per frame, nearest first, so there is no hitch.

### 10.7 Rendering

`Renderer` owns every GPU resource. Draw order in `DrawWorld`:

1. Sky dome, then the cloud deck with its own projection (the deck is wider than the far plane).
2. Terrain tiles, castle and city parts that take lighting, doors, tree trunks, boulders.
3. Soldiers: nine rigid parts each, from `Soldier.Matrices`; distant soldiers skip arms, head and pilum.
4. Banners and flags, waved on the CPU each frame.
5. Grass, then foliage with `AlphaTestEffect`.
6. Unlit parts: neon, stars, embers, lit windows, beacons.
7. The capture ring, birds, water, the orbital tether.
8. Sprites: blob shadows, shockwaves, tracers, the kill-cam round, particles, snow, then additive fire, flash, glows and the sun.

`DrawViewModel` draws the rifle and arms afterwards with a cleared depth buffer and its own projection (near plane
0.02 m), so they never poke through walls.

| Setting | Value |
| --- | --- |
| Lights | Three directional lights on `BasicEffect`: a warm low sun, a blue sky light from above, a dim bounce off the snow; plus ambient. Per-pixel lighting. |
| Fog | Linear, violet-grey (`0xb7b4c8`), from 60 m to 3,300 m |
| Far plane | 9,000 m |
| Near plane | 0.25 m at the hip, 1.4 m through the scope |
| Field of view | 68° at the hip; through the scope, the field of view that gives the chosen magnification |
| Vertex format | `Vtx`: position, normal, colour, texture coordinate |

**Anti-aliasing.** The UWP swap chain cannot be multisampled, so `Graphics.PreferMultiSampling` is false. `PaxGame`
draws the scene into a 4× multisampled `RenderTarget2D` the size of the output and copies it across. If creating that
target throws, the scene is drawn straight to the screen for the rest of the session.

**HUD.** `HudRenderer` works in design pixels: `U = screen height / 720`. Text uses three baked faces: `Ui` (Barlow
Condensed Medium), `UiBold` (Barlow Condensed SemiBold) and `Display` (Cinzel). Font textures are mip-mapped so small
labels stay clean. `Rich` lays out tagged text with wrapping.

### 10.8 Input and haptics

`PaxGame.ReadPad` reads the controller with no dead zone applied (`GamePadDeadZone.None`) and passes four axes and
sixteen button values (0 to 1) to `Pad.SetState`. The game applies its own dead zone in `Pad.Stick`.

```csharp
// Queue a pulse. low / high are the grip motors, lt / rt the trigger motors, all 0..1.
pad.Rumble(low: 0.95f, high: 0.75f, ms: 210, decay: true);        // recoil
pad.Rumble(high: 0.28f, ms: 35, delay: 520);                       // bolt, half a second later
```

`Pad.Update` sums the active pulses into `Pad.Out[4]`, clamps each to 1, and `PaxGame.SendRumble` forwards them with
`GamePad.SetVibration(index, low, high, leftTrigger, rightTrigger)`. At most 25 pulses are queued. `Pad.Stop()` clears
them.

Keyboard and mouse arrive through `KeyMouse`. While a mouse player is in the game (`GameSim.WantMouseCapture`) the
shell hides the cursor and holds it at the centre of the window. On the console the mouse is ignored.

`Pad.Active` records whether the controller was the last device used, and drives the prompts.

### 10.9 Sound

`Synth.RenderAll()` returns a dictionary of clip name to 16-bit PCM at 32 kHz mono, about 42 s in total. Each clip is
built from filtered noise bursts and swept tones with a reverb send, then normalised by `Finish(master, reverb)`.
`SynthAudio` wraps each in a `SoundEffect`, keeps a queue for delayed plays, and loops the wind.

`GameSim` knows only the `IAudio` interface. `NullAudio` is used when muted, in tests, and when no audio device exists.

### 10.10 Build, run and test

**Requirements.** The [.NET SDK](https://dotnet.microsoft.com/download) 8 or newer for the desktop build, the tests
and `apicheck` (`brew install dotnet` on a Mac). Windows with Visual Studio 2022 and the "Universal Windows Platform
development" workload for the Xbox package, or GitHub Actions.

All commands below run from `PaxRomana-XBOX-CSharp-src/`.

| Task | Command |
| --- | --- |
| Play on the desktop | `cd desktop && dotnet run -c Release` |
| … full screen | `dotnet run -c Release -- --fullscreen` |
| … as on the console (controller prompts, no mouse) | `dotnet run -c Release -- --console` |
| … without sound | `dotnet run -c Release -- --mute` |
| Render the 18 check views to PNG and quit | `cd desktop && dotnet run -c Release -- --shots out` |
| Rules test | `cd tests && dotnet run -c Release` |
| Check the code against the console's limits | `dotnet build tools/apicheck` |
| Rebuild the fonts | `python3 tools/makefont.py` (needs Pillow) |
| Xbox package on Windows | `powershell -ExecutionPolicy Bypass -File .\build.ps1` |

**The rules test** (`tests/Tests.cs`) builds the world without textures and drives a `GameSim` at 60 steps a second
through the same `Pad` and `KeyMouse` the shell fills. It prints one line per check and exits non-zero if any fails.

| Group | What it checks |
| --- | --- |
| World | Collision mesh size, plateau height, title screen |
| 1. Controller | A starts; left stick walks about 4.4 m in a second; right stick turns; B crouches; Menu pauses; A resumes |
| 2. Rifle shot | LT scopes, LB holds breath; RT fires once and does not repeat while held; trigger and grip motors kick; the bolt ticks the light motor; kill-cam starts; A skips it; the guard still dies at 393 m; the kill is counted |
| 3. Explosive charge | A closed gate stops a bullet; RB fires a charge; it detonates after its flight time; the gate is breached; a defender dies; the rumble arrives with the sound; a bullet then passes through the gateway |
| 4. Capture | Objective text outside; walking in through the breach; the flag is contested; the last-defender hint; walls clear; half way after 4 s; captured after 8 s; victory with statistics |
| 5. New mission and doors | A asks for a restart; gate and flag restored; a kill-cam left to run ends by itself with the guard down and no stray rounds; the door prompt; a closed gate blocks walking; A opens it; G fires a charge from the keyboard and prompts switch |
| 6. Sound | 19 clips, none silent, none clipped, under 60 s in total |

Two hooks exist for tests and the screenshot mode: `Player.AlwaysSteady` removes scope sway and
`GameSim.AlwaysKillCam` forces the kill-cam on every eligible shot.

**The screenshot mode** (`--shots`) poses the camera in 18 places on a timed script, saves 1280 × 720 PNG files,
prints average update and draw times, and exits by itself. It is the way to check rendering changes without playing.

### 10.11 Building for Xbox

`build.ps1` does three things:

1. Finds MSBuild (on the path, or through `vswhere`).
2. Creates a self-signed certificate `paxromana-sideload.pfx` with subject `CN=PaxRomana` if it is missing. The
   subject must match `Publisher` in `Package.appxmanifest`. The certificate is throwaway, its password is in the
   script, and `*.pfx` is git-ignored.
3. Runs MSBuild on `xbox\PaxRomanaNative\PaxRomanaNative.csproj`: Release, x64, .NET Native, sideload package, no
   bundle. Output goes to `package\PaxRomanaNative_<version>_x64_Test\` with a `Dependencies\x64` folder.

The UWP project is an old-style `.csproj` with `PackageReference` items for
`Microsoft.NETCore.UniversalWindowsPlatform` 6.2.14 and `MonoGame.Framework.WindowsUniversal` 3.8.1.303.

The entry point (`xbox/PaxRomanaNative/Program.cs`) starts MonoGame without XAML:

```csharp
static void Main()
{
    var factory = new MonoGame.Framework.GameFrameworkViewSource<XboxGame>();
    CoreApplication.Run(factory);
}
```

`XboxGame` subclasses `PaxGame` for two console specifics: it sets `IsConsole` when the device family is
`Windows.Xbox`, and it asks for the full core window at the display's raw pixel size so the picture is not letterboxed
or scaled.

**Continuous integration.** `.github/workflows/paxromana-native-xbox.yml` runs on `windows-2022` for any push that
touches `PaxRomana-XBOX-CSharp-src/**` or the workflow file, and on manual dispatch:

1. Rules test: `dotnet run -c Release --project PaxRomana-XBOX-CSharp-src\tests\PaxRomana.Tests.csproj`
2. Build package: `.\PaxRomana-XBOX-CSharp-src\build.ps1`
3. Upload `PaxRomana-XBOX-CSharp-src/package` as the artifact `paxromana-native-xbox-package`

```
git push
gh run watch
gh run view --log-failed      # if it fails
```

### 10.12 Installing on the console

The console must be in Developer Mode with the Xbox Device Portal enabled (set-up steps are in
`WSBX-src/README.md`).

```
cd PaxRomana-XBOX-CSharp-src
XBOX_IP=<console address> XBOX_USER=<portal user> XBOX_PASS=<portal password> ./deploy.sh
```

`deploy.sh`:

- downloads the newest artifact with `gh run download` into `./package` if that folder is missing (delete the folder
  to fetch a newer build);
- finds the `PaxRomanaNative_*` package and every dependency under `Dependencies/x64`;
- uploads them to `https://<console>:11443/api/app/packagemanager/package`. The `auto-` prefix on the user name tells
  Device Portal the caller is a script, so no CSRF token is needed; `-k` accepts the console's self-signed certificate;
- polls the install state for up to two minutes and prints the result.

Launch **Pax Romana: Tertia Roma (Native)** from Dev Home. Switching the app's type from *App* to *Game* in Dev Home
(highlight the app, press View, then *View details*) gives it the whole GPU and more memory.

### 10.13 How to change things

| To | Do this |
| --- | --- |
| Tune a rule | Constants sit at the top of the class that uses them: `Config` (world, ballistics), `Player` (speeds, eye heights, sway), `Weapon` (zoom, zero, magazine), `Capture.HOLD`, `GameSim.BLAST`, `Legion.AlertAround` call sites (alert amounts). Add or adjust a check in `tests/Tests.cs`. |
| Add a haptic cue | Call `Pad.Rumble(...)` where the event happens. Rifle and charge cues are in the `GameSim` constructor; capture cues in `Capture.Update`. |
| Add a sound | Synthesise a clip in `Synth.RenderAll`, add a method to `IAudio`, implement it in `SynthAudio` and `NullAudio`, and update the expected clip count in the test. |
| Add a soldier or a patrol | Add an `Add(new Route(...), ...)` line in the `Legion` constructor. `Total` follows automatically. |
| Add a door | Call `AddDoor` in `Castle` with its footprint and leaves. Collision, the prompt, breaching and drawing follow. |
| Add a building or prop | Use `Builder.Box`, `Cyl`, `Prism` or `Add` in `Castle` under an existing material key, and add a `Solid` if it should block the player. A new material key needs an entry in the `mats` dictionary in `Renderer.SetWorld`, and in `Castle.ColliderKeys` if bullets should hit it. |
| Add a texture | Add a `Make…` method and a field in `Textures`, upload it in `Renderer.SetWorld`. |
| Add a HUD element | Add state to `Hud`, set it from `GameSim` or `Capture`, draw it in `HudRenderer.InGame`. Keep it inside the safe margins (`sx`, `sy`). |
| Use a character the fonts lack | Add it to `CHARS` in `tools/makefont.py` and run the script. Characters missing from the atlas are skipped silently. |
| Add a control | Read it in `GameSim.PadInput` and `GameSim.KeyInput`; add a row to `KeyRows` in `HudRenderer` and to the README table. A new key needs an entry in the `K` enum and, in the same position, in `PaxGame.KeyMap`. |
| Add a file | Put it in `Game/`. Every project includes the folder by wildcard. |

**Before pushing**, run the rules test and `dotnet build tools/apicheck`. The second catches language features and
APIs that compile on .NET 8 but not for the console, which would otherwise surface only in the Windows build.

### 10.14 Pitfalls

| Pitfall | Detail |
| --- | --- |
| C# newer than 7.3, or APIs newer than .NET Standard 2.0 | They build on the desktop and fail in the UWP build. `tools/apicheck` catches them. |
| Back-buffer multisampling | Never set `PreferMultiSampling = true`: MonoGame's UWP swap chain creation fails with it. |
| `--` inside XML comments | Invalid XML; MSBuild reports MSB4025. It is easy to write when quoting `dotnet run -- …` in a project file. |
| Reflection under .NET Native | Release builds strip metadata. The game uses no reflection or serialisation; keep it that way, or add directives to `Properties/Default.rd.xml`. |
| Device access from the loader thread | `World.Build` and `Synth.RenderAll` run on a background thread and must not touch `GraphicsDevice` or create `SoundEffect` objects. Uploads happen in `Renderer.SetWorld` and `SynthAudio`'s constructor on the main thread. |
| Removing a kill-cam round early | Ending the kill-cam must only clear `Bullet.Cam`. Removing the bullet cancels the kill. The test covers this. |
| Unseeded chance in tests | The kill-cam is random; tests set `AlwaysKillCam` to make it deterministic. |
| Exceptions | `PaxGame.Update` and `Draw` catch everything and route it to `Fail`, which shows the error screen. Do not add code that swallows exceptions silently in the game loop. |
| Sixteen-bit index buffers | A single mesh is limited to 65,535 vertices. Large geometry is split (terrain tiles, vegetation chunks). |

### 10.15 Troubleshooting

| Symptom | Likely cause and action |
| --- | --- |
| The workflow fails in "Rules test" | A rule regression. Run `cd tests && dotnet run -c Release` locally; the failing line says which check. |
| The workflow fails in "Build package" | `gh run view --log-failed` shows the MSBuild error. The usual cause is C# or an API the UWP toolchain lacks; `dotnet build tools/apicheck` reproduces most of those locally. |
| `deploy.sh` says no package found | The artifact did not download or `./package` holds an old build. Delete `./package` and run it again. |
| Installing over an existing version is refused | Each CI build is signed with a newly generated certificate. Uninstall the app in Dev Home and install again. |
| The game shows its error screen | The text is the exception. It usually names the file and method. |
| Black borders or a scaled picture on the console | The size request in `XboxGame`'s constructor did not apply. Check the try block there. |
| Low frame rate on the console | Set the app type to *Game* in Dev Home. If still low, lower `grassCount` in `World.Build` or remove the 4× multisampling in `PaxGame.SceneTarget`. |
| No vibration | Vibration is sent only to the controller that is being read. Check the controller's own vibration setting on the console. |
| Text shows gaps | A character is not in the baked fonts. See "Use a character the fonts lack" above. |

### 10.16 Possible next steps

In rough order of value once the console build is confirmed:

1. Measure frame rate and loading time on the console and tune grass density and multisampling.
2. Decide whether the garrison should fight back and whether the mission can be lost (applies to both editions).
3. Settings: look sensitivity, inverted look, vibration strength, volume.
4. Stereo panning for impacts and explosions.
5. Real-time shadows or a baked shadow term, which needs a custom shader and so a content pipeline step.

## 11. Licences

| Component | Licence |
| --- | --- |
| MonoGame | Microsoft Public License (Ms-PL) |
| Cinzel, Barlow Condensed (baked into `Game/FontData.g.cs`) | SIL Open Font License 1.1; texts in `PaxRomana-XBOX-CSharp-src/tools/fonts/` |
