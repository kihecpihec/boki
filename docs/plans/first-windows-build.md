# First Windows test build

Built on 2026-10-25 from commit `ae0055f` using Unity `6000.6.7f1` on macOS.
The Windows x86_64 Mono build finished with `Build Finished, Result: Success.`
The existing enabled scene list starts at MainMenu and includes the three V1 levels and retained level scenes. No scenes or project settings were changed.

## Reproduce on macOS

Install Windows Build Support (Mono) for the pinned Editor through Unity Hub.
Close any Editor holding this project, then run from the repository root:

```sh
mkdir -p Game/Builds/Windows-x64
/Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit -projectPath "$PWD/Game" \
  -buildTarget Win64 \
  -buildWindows64Player "$PWD/Game/Builds/Windows-x64/Boki.exe" \
  -logFile "$PWD/Game/Builds/windows-build.log"
```

Keep the complete player output together. Exclude Unity's
`Boki_BackUpThisFolder_ButDontShipItWithYourGame` folder when packaging.
Build output is intentionally ignored by Git and is not included in this PR.
The first local ZIP was 54.1 MiB, contained 215 files, and passed ZIP integrity checks.
Its SHA256 is `6166157247185ccccdadaa4173c0dc7fe9a75ec08f00fb80cd05f7d0fad8e0bf`.

## Windows 11 ARM64 VM test

Extract the entire ZIP inside Windows. The packaged `Launch-Boki.cmd` runs:

```bat
Boki.exe -force-d3d11 -screen-fullscreen 0 -screen-width 1280 -screen-height 720
```

Enable the VM's 3D acceleration and install its guest tools. This x64 player
uses Windows 11 ARM's built-in emulation; it is not a native ARM64 player.
See [Microsoft's emulation documentation](https://learn.microsoft.com/en-us/windows/arm/apps-on-arm-x86-emulation).

Select NEW RUN. Check intro/skip, WASD movement, mouse camera, Space jump,
Left Shift boost, E interaction, Esc pause/resume, level transitions,
death/retry, return to menu, and quit.
If startup fails, collect `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Game\Player.log`
and record VM software/version and Windows version.

## Validation limits

- Repository architecture audit: no FAILs; existing warnings remain.
- Windows player build: successful; executable and UnityPlayer.dll verified as x86-64 PE files.
- ZIP integrity and required player files: verified.
- Windows/VM launch, gameplay and FPS: not tested.
- No Inspector assignments or scene edits required.
- VM performance does not establish the Ryzen 5600G acceptance target in
  [Architecture](../Architecture.md#1-technical-goal-and-boundaries).
