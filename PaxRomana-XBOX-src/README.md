# Pax Romana: Tertia Roma (Xbox)

An adaptation of the Pax Romana sniper game for Xbox. Rome never fell; in 2187 the legions of the Third Rome hold the
Kremlin above a frozen, neon-lit Moscow. From a ridge across the Moskva, clear the defenders inside the walls and raise
your standard at the flagpole in the courtyard to **capture the Kremlin**.

Your rifle fires sniper rounds and **explosive charges** (8 per mission). A charge flies the same trajectory as a
bullet, kills everything within about 8 m of where it lands, and blows doors and the Spasskaya gate off their hinges.

The game is the Three.js web game in this folder. On Xbox it runs inside a small app (`xbox/`) that shows it full
screen, feeds it the controller, and drives the controller's four vibration motors.

## Controller

| Input | Action |
| --- | --- |
| Left stick / click it | Move / sprint |
| Right stick | Look and aim (slows near a target and through the scope) |
| LT (hold) | Scope |
| RT | Fire the rifle |
| RB | Fire an explosive charge |
| LB | Hold breath (scoped) |
| D-pad up / down | Zoom |
| D-pad left / right | Sight zero, 100 to 600 m |
| X | Reload |
| A | Open or close the nearest door, skip the kill-cam |
| B / click right stick | Crouch / prone |
| View | Show all controls |
| Menu | Pause |

Keyboard and mouse still work in a browser: the same keys as the original game, plus **G** or the middle mouse button
for an explosive charge and **O** for doors.

## Haptics

| Event | What you feel |
| --- | --- |
| Rifle shot | A sharp kick in the right trigger, heavy recoil in the grips, then two light ticks as the bolt cycles |
| Explosive launch | A longer, heavier thump with the right trigger |
| Explosion | A rumble that arrives with the sound, stronger and longer the closer the blast |
| Kill / headshot | A short tick; a headshot also taps the left trigger |
| Holding breath | A two-beat heartbeat |
| Reload | Four light ticks |
| Scope up, doors, being spotted, the alarm | Light cues |
| Capturing the flag | A pulse that builds, then a long rumble when the Kremlin is yours |

Inside the Xbox app all four motors are used, including the impulse motors in the triggers. In a browser the game uses
the standard rumble, with trigger rumble where the browser supports it.

## Run it in a browser

    npm install
    npm run dev

Plug in or pair an Xbox controller and press **A** on the title screen. Add `?xbox` to the URL to use the console
quality settings, or `?low` for the lightest ones.

## Build and install on Xbox

The console must be in Developer Mode with the Xbox Device Portal enabled (see `../WSBX-src/README.md` for that setup).

**Build on GitHub (no Windows needed).** Push. The **Pax Romana Xbox package** workflow runs on every push that touches
this folder; follow it with `gh run watch`. The result is an artifact named `paxromana-xbox-package`.

**Build on Windows.** With Node.js and Visual Studio 2022 (Universal Windows Platform development workload):

    powershell -ExecutionPolicy Bypass -File .\build.ps1

**Install**, from this folder on a Mac or Linux machine on the same network as the console:

    XBOX_IP=<console ip> XBOX_USER=<portal user> XBOX_PASS=<portal password> ./deploy.sh

It downloads the newest GitHub build into `package/` if that folder is missing (delete it to fetch a newer build) and
uploads it through the Device Portal. Or use **Add** on the Device Portal page in a browser: pick the
`PaxRomanaXbox_*.msix` file, then every file in its `Dependencies/x64` folder.

Launch **Pax Romana: Tertia Roma** from the games and apps list in Dev Home. In Device Portal, switching its type from
*App* to *Game* gives it the larger game resource allowance.

## Layout

| Path | Role |
| --- | --- |
| `src/main.js` | Boot, game loop, controller mapping, haptic cues, explosions, kill-cam |
| `src/gamepad.js` | Controller input and the haptics mixer (native host or browser Gamepad API) |
| `src/weapon.js` | Rifle, sniper rounds and explosive charges, ballistics |
| `src/capture.js` | Doors, breaching, defenders inside the walls, the flag |
| `src/castle.js`, `src/city.js` | The Kremlin, forward base and bridge; the Moscow skyline |
| `src/effects.js`, `src/audio.js` | Particles, explosions, snow; synthesised sound |
| other `src/*.js` | Terrain, sky, vegetation, soldiers, player, HUD, post-processing (as in the original game) |
| `xbox/` | The Xbox app: a UWP project hosting the web build in WebView2, with the controller bridge |
| `build.ps1`, `deploy.sh` | Build the Xbox package; install it on a console |
