# Pax Romana: Tertia Roma (native C#)

The Xbox game from `../PaxRomana-XBOX-src`, rewritten in C#. There is no web view and no JavaScript: the console runs
compiled C# on [MonoGame](https://monogame.net) (Direct3D 11, XAudio2, the Xbox controller API). It was written because
the web edition needs the Edge WebView2 runtime, which the console did not have.

Rome never fell; in 2187 the legions of the Third Rome hold the Kremlin above a frozen, neon-lit Moscow. From a ridge
across the Moskva, clear the defenders inside the walls and raise your standard at the flagpole in the courtyard to
**capture the Kremlin**. Your rifle fires sniper rounds and **explosive charges** (8 per mission). A charge flies the
same trajectory as a bullet, kills everything within about 8 m of where it lands, and blows doors and the Spasskaya
gate off their hinges.

## What is the same, and what is not

The rules are a direct port and are checked by a test (below): the valley, the Kremlin and its rooms, the
garrison's patrols and alert, free roam with collision, wading, swimming and the bridge, ballistics with drop, wind and
sight zero, the kill-cam, explosive charges, doors, the capture, the HUD and menus, the controller layout and every
haptic cue.

The picture is simpler. This edition uses only MonoGame's built-in shaders, so that nothing has to be compiled or
loaded from files:

- no real-time shadows (soldiers get a soft blob shadow), no bloom, tone mapping or film grain
- the scope has no lens distortion or colour fringing
- grass and leaves do not sway in the wind; water is a tinted, drifting sheet without reflections
- torches and explosions glow but do not light the walls around them
- sound is mono, without left/right panning, and there is no birdsong

## Controller

| Input | Action |
| --- | --- |
| Left stick / click it | Move / sprint |
| Right stick | Look and aim (slows near a target and through the scope) |
| LT (hold) | Scope |
| RT | Fire the rifle |
| RB | Fire an explosive charge |
| LB | Hold breath (scoped) |
| D-pad up / down, or Y | Zoom |
| D-pad left / right | Sight zero, 100 to 600 m |
| X | Reload |
| A | Open or close the nearest door, skip the kill-cam |
| B / click right stick | Crouch / prone |
| View | Show all controls |
| Menu | Pause |

Keyboard and mouse work in the desktop build: W A S D, mouse or arrows to aim, left button or Space to fire, right
button or F for the scope, G or the middle button for a charge, O for doors, R to reload, Q / E for zero, the wheel or
+ / − to zoom, C / Z to crouch or go prone, Shift to sprint or hold breath, H for the controls, Esc to pause.

## Haptics

All four motors of the Xbox controller are used, including the impulse motors in the triggers.

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

## Layout

| Path | What it is |
| --- | --- |
| `Game/` | The whole game, in C# 7.3 against .NET Standard 2.0. Shared by everything below. |
| `xbox/` | The Xbox (UWP) app: a project file, a manifest and a 50-line entry point around `Game/`. |
| `desktop/` | The same game in a window on macOS, Windows or Linux. |
| `tests/` | Headless test of the rules. |
| `tools/` | The font baker (`makefont.py`), the two fonts it reads, and a compile-only check (`apicheck`). |
| `build.ps1`, `deploy.sh` | Build the Xbox package on Windows; install it on the console from any machine. |

`Game/` at a glance: `GameSim.cs` is one mission (rules, input, haptics) with no drawing in it; `Renderer.cs` and
`HudRenderer.cs` draw it; `PaxGame.cs` is the window, the loading screen and the device input. Textures, geometry,
fonts and sound are all generated at start-up: there are no asset files.

## Try it on a Mac or PC

Needs the [.NET SDK](https://dotnet.microsoft.com/download) 8 or newer (`brew install dotnet` on a Mac).

    cd desktop
    dotnet run -c Release

Plug in or pair an Xbox controller and press **A** on the title screen, or click to play with keyboard and mouse.
Options go after `--`:

    dotnet run -c Release -- --fullscreen
    dotnet run -c Release -- --console        # behave as on the Xbox: controller prompts only
    dotnet run -c Release -- --mute
    dotnet run -c Release -- --shots out      # render a fixed set of views to out/*.png and quit

## Test the rules

    cd tests
    dotnet run -c Release

Runs a mission without a window: starts with **A**, walks and turns with the sticks, fires a round and checks the
vibration it produces, skips one kill-cam and watches another, breaches the gate with a charge, captures the flag, wins,
restarts, opens a door by hand, and synthesises the sound clips. It exits with a non-zero code if any check fails. The GitHub workflow runs it before every Xbox build.

`dotnet build tools/apicheck` compiles `Game/` against .NET Standard 2.0 and C# 7.3, the limits of the Xbox build, so
code the console cannot compile is caught without Windows.

## Build and install on Xbox

The console must be in Developer Mode with the Xbox Device Portal enabled (see `../WSBX-src/README.md` for that setup).

**Build.** The package can only be built on Windows. Pushing a change under this folder runs the workflow
`.github/workflows/paxromana-native-xbox.yml`, which builds it on a GitHub Windows runner:

    git push
    gh run watch

On a Windows PC with Visual Studio 2022 and the "Universal Windows Platform development" workload you can build it
yourself instead: `powershell -ExecutionPolicy Bypass -File .\build.ps1`.

**Install.** From this folder, on any machine on the same network as the console:

    XBOX_IP=<console address> XBOX_USER=<portal user> XBOX_PASS=<portal password> ./deploy.sh

It downloads the newest build from GitHub (needs `gh`) into `./package` if that folder is missing, then uploads it
through the Device Portal. Delete `./package` to fetch a newer build.

**Run.** Launch **Pax Romana: Tertia Roma (Native)** from Dev Home. It is a separate app from the web edition, so both
can be installed at once.

For the smoothest frame rate, switch the app's type from *App* to *Game* in Dev Home (highlight the app, press View,
then *View details*): games get the whole GPU and more memory.

If something goes wrong while it runs, the game shows the error text on screen instead of closing.

## Fonts

The HUD uses [Cinzel](https://github.com/NDISCOVER/Cinzel) and [Barlow Condensed](https://github.com/jpt/barlow),
both under the SIL Open Font License (`tools/fonts/*-OFL.txt`). They are baked into `Game/FontData.g.cs` by
`tools/makefont.py`; run it again (needs Python with Pillow) only if you change the fonts or the character set.
