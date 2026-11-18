# Pax Romana Xbox (C#, MonoGame): developer guide

How to run, test, build and install the native C# edition of Pax Romana, which lives in
`PaxRomana-XBOX-CSharp-src/`. What is there today is **Level Zero: The Keys** (Bavaria, summer 1944: take the keys
to the U-235 enrichment zone from a castle vault). Development happens on the `develop` branch. This is the task-by-task guide. For the reference material, see:

- [`PRD-PaxRomana-XBOX-CSharp-MonoGame.md`](PRD-PaxRomana-XBOX-CSharp-MonoGame.md): what the game must do, and from
  section 10 the architecture, file-by-file contents, conventions and pitfalls.
- [`PaxRomana-XBOX-CSharp-src/README.md`](PaxRomana-XBOX-CSharp-src/README.md): the game, its controls and haptics.

## Quick reference

Commands run from `PaxRomana-XBOX-CSharp-src/` unless a `cd` says otherwise.

| I want to | Command |
| --- | --- |
| Play on this Mac | `cd desktop && dotnet run -c Release` |
| Run the rules test | `cd tests && dotnet run -c Release` |
| Check the code will compile for Xbox | `dotnet build tools/apicheck` |
| Build the Xbox package | `git push`, then `gh run watch` |
| Install on the console | `XBOX_IP=… XBOX_USER=… XBOX_PASS=… ./deploy.sh` |
| Install a newer build | `rm -rf package`, then `deploy.sh` again |

## What you need

| For | You need |
| --- | --- |
| Playing on the Mac, the tests, `apicheck` | The .NET SDK 8 or newer: `brew install dotnet` |
| Building the Xbox package | Nothing locally. GitHub Actions builds it on a Windows runner. The package cannot be built on macOS. |
| Installing on the console | The GitHub CLI, signed in (`gh auth status`), and `curl`. The console in Developer Mode with the Xbox Device Portal enabled, on the same network as the Mac. |
| Rebuilding the fonts (rare) | Python 3 with Pillow |

## Running on Xbox

### 1. Prepare the console (once)

The console must be in Developer Mode with the Xbox Device Portal switched on. The full steps are in
[`WSBX-src/README.md`](WSBX-src/README.md), section 1. In short:

1. Install **Xbox Dev Mode** from the Store, activate the console in Partner Center, then **Switch and restart**.
2. In Dev Home, open **Remote Access Settings**, enable **Xbox Device Portal** and set a user name and password.

Dev Home shows the console's IP address at the top. It can change between sessions, so check it before installing.

### 2. Build the package

Commit and push. The workflow **Pax Romana native Xbox package**
(`.github/workflows/paxromana-native-xbox.yml`) runs on any push that changes a file under
`PaxRomana-XBOX-CSharp-src/` or the workflow itself.

    git push
    gh run watch

It runs the rules test, then builds and signs the package, and uploads it as the artifact
`paxromana-native-xbox-package` (about 40 MB). The first run took about three and a half minutes.

- A push that only changes files outside that folder, such as this document, does not start a build. Start one by
  hand with `gh workflow run paxromana-native-xbox.yml`.
- If the build fails, `gh run view --log-failed` prints the failing step's log.

### 3. Install it

    cd PaxRomana-XBOX-CSharp-src
    XBOX_IP=<console address> XBOX_USER=<portal user> XBOX_PASS=<portal password> ./deploy.sh

The script downloads the newest build from GitHub into `./package`, uploads it and its dependencies to the console
through the Device Portal, and waits for the install to finish (up to two minutes).

- **It only downloads when `./package` is missing.** After a new build, delete the folder first:
  `rm -rf package`. Otherwise it installs the build you already have.
- The user name and password are the ones set in Remote Access Settings, not a Microsoft account.
- To keep the password out of your shell history, put a space before the command (zsh skips such lines when
  `HIST_IGNORE_SPACE` is set) or export the three variables in a file you `source`.

Without a terminal, the same package can be installed from a browser: open `https://<console address>:11443`, sign
in, choose **Add** under *My games & apps*, pick the `PaxRomanaNative_*` package file (`.msix` or `.appx`) from `package/`, then add every
file in its `Dependencies/x64` folder.

### 4. Launch it

In Dev Home, open **Pax Romana: Tertia Roma (Native)** from *Games & apps*. It is a separate app from the web edition,
so both can be installed.

For the best frame rate, change its type from *App* to *Game*: highlight the app, press View, choose *View details*.

### First run on the console

Nothing has run on the console yet, so the first launch is the test. Things to look at:

1. It reaches the title panel with no WebView2 message, the loading bar completes, **A** starts the Level Zero intro,
   and **A** again (or waiting 27 seconds) starts the mission.
2. The picture fills the screen and no HUD text is cut off at the edges.
3. Sound plays, and the controller vibrates on a shot, including the right trigger.
4. Turning in the courtyard and looking across the valley through the scope are smooth.

The full list is in the PRD, section 9. If the game stops with an error, it shows the error text on screen instead of
closing; that text is the bug report.

## Running on the Mac

    cd PaxRomana-XBOX-CSharp-src/desktop
    dotnet run -c Release

It is the same game code in a window. Pair an Xbox controller and press **A**, or click to play with keyboard and
mouse. Options go after `--`:

| Option | Effect |
| --- | --- |
| `--fullscreen` | Full screen |
| `--console` | Behave as on the Xbox: controller prompts only, no mouse |
| `--mute` | No sound |
| `--shots <folder>` | Render 26 fixed views to PNG files, print frame timings and quit. The quick way to check a rendering change. |

Example: `dotnet run -c Release -- --console --mute`

## Testing

    cd PaxRomana-XBOX-CSharp-src/tests
    dotnet run -c Release

This plays a mission with no window, sound or controller: it starts with **A**, skips the intro (and later lets it
play through), walks and turns, fires a round and checks the vibration, skips one kill-cam and watches another,
breaches the gate, opens the keep and the vault, takes the keys, wins, restarts, opens a door and synthesises the
sound. It prints one line per check (58 in all) and exits non-zero if any fails. It
takes a few seconds.

    cd PaxRomana-XBOX-CSharp-src
    dotnet build tools/apicheck

This compiles `Game/` under the Xbox build's limits: C# 7.3 and .NET Standard 2.0. Newer syntax or APIs compile fine
for the Mac and then fail in the Windows build, minutes later; this catches them in seconds.

## Making a change

1. Edit under `PaxRomana-XBOX-CSharp-src/Game/`. Every project includes that folder by wildcard, so new files need no
   project edits.
2. Run the rules test and `dotnet build tools/apicheck`.
3. Look at it: play the desktop build, or use `--shots` for rendering changes.
4. Commit and push, then `gh run watch`.
5. `rm -rf package`, then `deploy.sh`, and try it on the console.

Where to make common changes (tuning a rule, adding a haptic cue, a sound, a soldier, a door, a HUD element, a
control) is tabulated in the PRD, section 10.13.

## Where things are

| Path | Contents |
| --- | --- |
| `Game/` | The whole game. `GameSim.cs` is one mission (rules, input, haptics); `Renderer.cs` and `HudRenderer.cs` draw it; `PaxGame.cs` is the window, loader and device input. |
| `xbox/` | The UWP app: project, manifest, tiles and a 49-line entry point around `Game/`. |
| `desktop/` | The desktop launcher and the `--shots` mode. |
| `tests/` | The rules test. |
| `tools/` | `makefont.py` and the two fonts it bakes, and `apicheck`. |
| `build.ps1` | Builds and signs the Xbox package. Runs on Windows only; the workflow calls it. |
| `deploy.sh` | Installs the package on the console. |

## Troubleshooting

| Symptom | What to do |
| --- | --- |
| `dotnet: command not found` | `brew install dotnet` |
| The workflow fails in "Rules test" | Run the rules test locally; the failing line names the check. |
| The workflow fails in "Build package" | `gh run view --log-failed`. Usually syntax or an API the Xbox toolchain lacks; `dotnet build tools/apicheck` reproduces most of those. |
| `gh run watch` says there are no runs in progress | The push did not touch `PaxRomana-XBOX-CSharp-src/`. Start a build with `gh workflow run paxromana-native-xbox.yml`. |
| `deploy.sh` stops with "Set XBOX_IP…" | One of the three variables is missing. |
| `deploy.sh` cannot connect | Check the address in Dev Home, that the Device Portal is enabled, and that the Mac is on the same network. |
| `deploy.sh` reports 401 or asks for credentials | Wrong portal user name or password. |
| `deploy.sh` says no package found | Delete `./package` and run it again. If the download itself fails, the artifact may have expired; start a new build. |
| The console still runs the old build | `./package` held the old one. `rm -rf package` and install again. |
| The install is refused over an existing version | Each build is signed with a newly generated certificate. Uninstall the app in Dev Home, then install again. |
| The game shows an error screen | Copy the text; it names the failing file and method. |
| Low frame rate on the console | Set the app type to *Game* in Dev Home. |

More symptoms and causes are in the PRD, section 10.15.
