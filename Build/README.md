# Boki

The Windows x86-64 distribution lives directly in this folder. Run `Boki.exe`.
The game selects the display's native resolution in borderless fullscreen at
startup, overriding any lower resolution saved by an earlier launch.
Keep `Boki_Data/`, `UnityPlayer.dll`, `MonoBleedingEdge/`, and the other runtime
files beside the executable when copying the game.

Source baseline: `origin/dev` commit `3f23b82`, plus the native-resolution
startup change in this checkout, rebuilt on 2026-10-25.
The build includes every enabled scene in `EditorBuildSettings`, starting at
MainMenu, followed by OpeningCrawl and the three V1 levels, plus the retained
graybox levels. The product and executable are named `Boki`, without a version
suffix. This packages the current dev content; Windows gameplay and target
Ryzen 5600G performance still require testing.

## Rebuild

Install Unity **6000.6.7f1** and Windows Build Support (Mono). Close any Editor
holding `Source/Game`, then run from the repository root on macOS:

```sh
BOKI_BUILD_DIRECTORY="$PWD/Build" \
/Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit -projectPath "$PWD/Source/Game" \
  -buildTarget Win64 -executeMethod Boki.Editor.PlayerBuild.WindowsX64 \
  -logFile /tmp/boki-windows-build.log
```

The command explicitly selects x64 and restores the Editor's previous architecture
and product name afterward. It does not edit scenes or the shared build scene list.
Exclude Unity's `Boki_BackUpThisFolder_ButDontShipItWithYourGame` directory and
build logs from the distribution. No Inspector assignments are required.

On Windows, check New Run, crawl/intro skip, movement, jump, boost, combat,
upgrades, route and level transitions, pause, death/retry, boss victory, return
to menu, and quit. Confirm native resolution on the target display, including
after a previous lower-resolution/windowed launch, and check HUD scaling.
Measure native-resolution performance separately from the 1280×720 Low baseline.
