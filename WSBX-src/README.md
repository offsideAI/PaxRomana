# WSBX

A two-player Pong for Xbox controllers, built as a UWP app so it can be sideloaded onto an Xbox Series S/X in
Developer Mode. Left stick (or D-pad) moves your paddle, **A** starts, first to 7 wins. With one controller the
console plays the right paddle. Controllers rumble on hits and lost points.

UWP apps can only be built on Windows. This repo builds the package on a GitHub Actions Windows runner
(`.github/workflows/wsbx-xbox.yml`), so a Mac is enough. With a Windows machine you can also build locally.

## 1. Put the console in Developer Mode (once)

1. On the Xbox, install **Xbox Dev Mode** from the Microsoft Store and open it. It shows an activation code.
2. In Partner Center, open **Xbox services → Xbox development consoles**, press **+**, and enter the code.
3. Back on the Xbox, choose **Switch and restart**. It reboots into **Dev Home**.
4. Connect the console to your network (**Settings → Launch Settings → General → Network settings**).
5. In Dev Home, open **Remote Access Settings**, enable **Xbox Device Portal**, and set a user name and password.
   The console IP address is shown at the top of Dev Home.

## 2. Build the package

**On GitHub (no Windows needed).** Push. The **WSBX Xbox package** workflow runs on every push that touches
`WSBX-src/`, or start it by hand with `gh workflow run "WSBX Xbox package"`. Follow it with `gh run watch`.
The result is an artifact named `wsbx-xbox-package`.

**On Windows.** Install Visual Studio 2022 with the **Universal Windows Platform development** workload, open
PowerShell in this folder and run:

    powershell -ExecutionPolicy Bypass -File .\build.ps1

Either way the output is a `package` folder holding the signed app package and, under `Dependencies\x64`, the
packages it needs.

## 3. Install it on the Xbox

Either of these, with the console on the same network.

**From a browser (any computer).** Open `https://<console ip>:11443`, accept the certificate warning and sign in.
Under *My games & apps* choose **Add**, pick the `WSBX_*.appx` (or `.msix`) file from the `package` folder (`gh run download --name wsbx-xbox-package --dir package` fetches it),
press **Next**, add every file in its `Dependencies\x64` folder, then **Start**.

**From a Mac or Linux terminal.** This downloads the newest GitHub build into `package/` if that folder is missing
(delete it to fetch a newer build), then uploads it:

    XBOX_IP=<console ip> XBOX_USER=<portal user> XBOX_PASS=<portal password> ./deploy.sh

**Straight from Visual Studio.** Open `WSBX.sln`, pick `Release` / `x64`, set the debug target to **Remote Machine**
with the console IP (Universal authentication), and press F5. Visual Studio asks for a PIN: get it from
**Show Visual Studio pin** in Dev Home. This builds, installs and launches in one step, without `build.ps1`.

## 4. Play

WSBX appears in the *Games & apps* list in Dev Home. Select it and press A. In Device Portal you can switch its type
from *App* to *Game*, which gives it the larger game resource allowance.

## Trying it on the PC

The same project runs on Windows itself: in Visual Studio choose **Local Machine** and press F5.
Keyboard: W/S or Up/Down, Space or Enter to start.
