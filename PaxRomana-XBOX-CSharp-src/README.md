# Pax Romana, Level Zero: The Keys (native C#)

A first-person sniper game for Xbox, written in C#. There is no web view and no JavaScript: the console runs compiled
C# on [MonoGame](https://monogame.net) (Direct3D 11, XAudio2, the Xbox controller API). It began as a rewrite of the
web game in `../PaxRomana-XBOX-src`, which needs the Edge WebView2 runtime that the console did not have.

**Level Zero.** Bavaria, summer 1944. Beyond the valley the Reich is enriching uranium-235 in a walled works. The keys
to that enrichment zone lie in a strongbox in the vault of Burg Adlerstein, a hill castle held by a garrison of 29.
You are an agent of the Research and Analysis Wing. From a ridge across the river, clear the defenders inside the
castle walls, get into the keep, and **take the keys**. Later levels use them to enter the enrichment zone and
subvert it from the inside.

The level opens with an intro: a "Level Zero" title card and three briefing cards over a flight across the valley,
the works and the castle, narrated by a recorded voice-over. It runs for about 40 seconds, plays once per session,
and **A** skips it.

Your rifle fires sniper rounds and **explosive charges** (8 per mission). A charge flies the same trajectory as a
bullet, kills everything within about 8 m of where it lands, and blows doors and the castle gate off their hinges.

## How this differs from the web game

The rules began as a direct port of the web game and are checked by a test (below): free roam with collision, wading,
swimming and the bridge, ballistics with drop, wind and sight zero, the kill-cam, explosive charges, doors, the HUD
and menus, the controller layout and every haptic cue.

The setting and the objective are new in this edition. The web game is a winter Kremlin held by Roman legionaries,
captured by raising a standard in the courtyard. Level Zero is a summer valley, a German castle with a wartime
garrison, and the keys in the vault. The garrison flies the historical flag; `Config.HISTORICAL_INSIGNIA` in
`Game/Core.cs` switches it to a plain black cross for places where showing that symbol is restricted.

The picture is simpler than the web game's. This edition uses only MonoGame's built-in shaders, so that nothing has
to be compiled or loaded from files:

- no real-time shadows (soldiers get a soft blob shadow), no bloom, tone mapping or film grain
- the scope has no lens distortion or colour fringing
- grass and leaves do not sway in the wind; water is a tinted, drifting sheet without reflections
- torches and explosions glow but do not light the walls around them
- sound is mono, without left/right panning, and there is no birdsong

## Controller

| Input | Action |
| --- | --- |
| Left stick | Move |
| LB (hold), or click the left stick | Run. Raising the scope ends the run; from a crouch or prone it stands you up |
| Right stick | Look and aim (slows near a target and through the scope) |
| LT (hold) | Scope |
| RT | Fire the rifle |
| RB | Fire an explosive charge |
| LB (hold, while scoped) | Hold breath |
| D-pad up / down, or Y | Zoom |
| D-pad left / right | Sight zero, 100 to 600 m |
| X | Reload |
| A | Open or close the nearest door, skip the intro or the kill-cam |
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
| Cracking the strongbox | A pulse that builds, then a long rumble when the keys are yours |

## Layout

| Path | What it is |
| --- | --- |
| `Game/` | The whole game, in C# 7.3 against .NET Standard 2.0. Shared by everything below. |
| `xbox/` | The Xbox (UWP) app: a project file, a manifest and a 50-line entry point around `Game/`. |
| `desktop/` | The same game in a window on macOS, Windows or Linux. |
| `tests/` | Headless test of the rules. |
| `assets/` | The only files the game loads at run time: the intro voice-over, `assets/voice/intro1.wav` to `intro4.wav`. |
| `tools/` | The font baker (`makefont.py`) and the two fonts it reads, the voice-over generator (`makevoice.py`), and a compile-only check (`apicheck`). |
| `build.ps1`, `deploy.sh` | Build the Xbox package on Windows; install it on the console from any machine. |

`Game/` at a glance: `GameSim.cs` is one mission (rules, input, haptics) with no drawing in it; `Renderer.cs` and
`HudRenderer.cs` draw it; `PaxGame.cs` is the window, the loading screen and the device input. Textures, geometry,
fonts and sound effects are all generated at start-up; the intro voice-over is the one thing loaded from files.

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
    dotnet run -c Release -- --voice-check    # load the intro voice-over as the game does, list it and quit (plays nothing)

## Test the rules

    cd tests
    dotnet run -c Release

Runs a mission without a window: starts with **A**, skips the intro (and later lets it play through), walks and turns
with the sticks, fires a round and checks the vibration it produces, skips one kill-cam and watches another, breaches
the gate with a charge, opens the keep and the vault, takes the keys, wins, restarts, opens a door by hand, and
synthesises the sound clips. It exits with a non-zero code if any check fails. The GitHub workflow runs it before every Xbox build.

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

**Run.** Launch **Pax Romana: Tertia Roma (Native)** from Dev Home (the app keeps that name for now). It is a separate app from the web edition, so both
can be installed at once.

For the smoothest frame rate, switch the app's type from *App* to *Game* in Dev Home (highlight the app, press View,
then *View details*): games get the whole GPU and more memory.

If something goes wrong while it runs, the game shows the error text on screen instead of closing.

## Voice-over

The intro is narrated by four clips in `assets/voice/`, one per card, generated with
[ElevenLabs](https://elevenlabs.io) text-to-speech (the premade voice "Brian"). They are ordinary 16-bit mono WAVE
files at 24 kHz, about 1.5 MB in all. Both the Xbox package and the desktop build include them; if a clip is missing,
that card plays without narration.

The narration text is in `tools/makevoice.py`. To change it, edit the lines there and run:

    ELEVENLABS_API_KEY=<your key> python3 tools/makevoice.py

The script rewrites the clips and `Game/IntroVoice.g.cs`, which holds each clip's length. Those lengths time the
intro: a card stays up for as long as its narration runs. Whether the clips may be distributed depends on the
ElevenLabs plan of the account that generated them.

## Fonts

The HUD uses [Cinzel](https://github.com/NDISCOVER/Cinzel) and [Barlow Condensed](https://github.com/jpt/barlow),
both under the SIL Open Font License (`tools/fonts/*-OFL.txt`). They are baked into `Game/FontData.g.cs` by
`tools/makefont.py`; run it again (needs Python with Pillow) only if you change the fonts or the character set.
