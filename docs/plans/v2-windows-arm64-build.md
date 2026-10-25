# V2 Windows ARM64 test build

V2 packages the integrated `origin/dev` snapshot at `938fe96`, including the latest
laser muzzle/impact effects, compact retry summary, story-phone fixes, subtitles,
level music, arena intros, and existing three-level run flow. It retains the
existing scene names, authored assets, and save identity. V2 is a test package,
not full-design release acceptance.

## Reproduce

Use Unity `6000.6.7f1` with Windows Build Support (Mono). From the repository root:

```sh
mkdir -p Game/Builds/V2-Windows-ARM64
BOKI_V2_BUILD_DIRECTORY="$PWD/Game/Builds/V2-Windows-ARM64" \
/Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit -projectPath "$PWD/Game" -buildTarget Win64 \
  -executeMethod Boki.Editor.PlayerBuild.V2WindowsArm64 \
  -logFile "$PWD/Game/Builds/v2-windows-arm64.log"
```

The shared player builder explicitly selects native ARM64 and version `2.0.0`,
then restores the previous Editor architecture and project version. It builds
MainMenu, V1_Pohorje, V1_Maribor, and V1_FERI without changing scenes or the shared
build scene list. Older milestone build commands reuse the same build helper.
No Inspector assignments are needed.

## Windows test

Extract the complete `Boki-V2-Windows-ARM64.zip` and run `Launch-Boki-V2.cmd`.
Keep the executable, data directory, DLLs, and Mono runtime together. The launcher
uses Direct3D 11 at 1280×720 in a window for testing in a Windows ARM64 VM.

Check New Run, intro/skip, movement, jump, boost, laser muzzle/impact effects,
story-phone visibility, music in all three levels, pause/resume, transitions,
death/retry summary, victory, return to menu, and quit. Existing persistent
unlocks are retained; use a fresh Windows account for a fresh-save test.
Collect `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Game\Player.log` if it fails.

Windows launch/gameplay and target Ryzen 5600G performance still require testing.
VM performance does not establish target-hardware acceptance.

## Validation and packaging

- Pinned macOS Editor build: `Build Finished, Result: Success.` and
  `V2_PLAYER_BUILD_PASSED`. Unity generated the new helper's `.meta`.
- Architecture audit: no FAILs; existing warnings remain.
- `git diff --check`: passed. No deterministic gameplay rules changed.
- Executable, UnityPlayer, Mono runtime, Burst and remaining native DLLs:
  PE ARM64 verified.
- ZIP integrity and required player files: verified.
- No scenes, prefabs, project settings, or rendering settings retained changes.
- Windows launch, gameplay and FPS: not run.

Packaging adds `Launch-Boki-V2.cmd` and `README.txt`, excludes Unity's
`BackUpThisFolder_ButDontShipItWithYourGame` directory, and removes
`Boki-V2_Data/Plugins/x86_64/libonigwrap.dll`. This last file is an unused
TextMate library leaked by the Unity Version Control Editor package's Win64
import settings; its managed Editor assemblies are absent from the player.
The package cache was not modified.

## Windows x86-64 variant

The same V2 content is also packaged as `Boki-V2-Windows-x64.zip`. Reproduce
with the command above using `Boki.Editor.PlayerBuild.V2WindowsX64`, output
directory `Game/Builds/V2-Windows-x64`, and log `Game/Builds/v2-windows-x64.log`.
The shared helper selects x64 and restores the previous architecture/version.
Build succeeded; native PE files verified x86-64; ZIP integrity and required
player files passed. Windows gameplay and FPS remain untested. Extract all
files and run `Launch-Boki-V2.cmd`. No scene or Inspector changes required.
