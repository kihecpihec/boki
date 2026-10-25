# Boki

The Windows x86-64 distribution lives directly in this folder. Run `Boki.exe`.
Keep `Boki_Data/`, `UnityPlayer.dll`, `MonoBleedingEdge/`, and the other runtime
files beside the executable when copying the game.

Source snapshot: pushed `origin/dev` commit `3f23b82`, fetched on 2026-10-25.
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
to menu, and quit. Measure performance at 1280×720 Low on the target hardware.
