# V1 pause menu verification

The authorized `V1_Pohorje`, `V1_Maribor`, and `V1_FERI` scenes contain a copy of the actual `Menu_Preview` canvas, including its artwork, screen effects, music and button sounds. `Menu_Preview` itself is unchanged. The existing V1 panel remains responsible for death and victory.

`V1GameFlow.menu` references the scene's `V1 Pause Menu` / `MenuScreen`. `MenuScreen.startHidden` is enabled. Background, readability and effect layers belong to `Menu Panel`, so hiding the menu reveals gameplay. No Inspector assignments remain. `Boki/Version 1/Upgrade Pause Menus` installs this wiring in scenes that lack it; it preserves existing menu assignments and refuses Play Mode or dirty scenes.

Escape opens pause after the intro and outside upgrade selection. Resume or Escape releases only `PauseReason.Menu` through `RunSession`. Settings toggles the menu screen effects; Back returns to pause. Restart Run uses `RunSession.Retry`, returning to Pohorje with a new run and retained permanent discoveries. Quit to Menu unloads the current level and loads `MainMenu` with gameplay suspended; New Run configures the V1 destinations and starts again at `V1_Pohorje`. Quit exits a player build. The menu displays current HP, battery, XP, upgrade selection count and kills.

The menu temporarily disables EventSystem navigation events while visible because its dedicated Input System actions already handle keyboard navigation and submission. Mouse clicks still use the existing EventSystem. Hiding the menu restores the previous navigation setting for upgrades and terminal screens.

## Automated smoke check

Run the pinned Editor with `-batchmode -nographics -projectPath Source/Game -executeMethod Boki.Editor.V1PauseMenuSetup.SmokeChecks -logFile /tmp/boki-pause-smoke.log` (omit `-quit`; the check exits itself). This is a disposable Play Mode run: it temporarily releases the intro's scripted pause to exercise menu events. It first checks launch from `MainMenu`. For each map it exercises pause, settings toggle/back, resume, Restart Run, Quit to Menu and New Run. It checks time scale and the restart destination, and logs `V1_PAUSE_SMOKE_PASSED` for each map.

## Manual verification

Open each V1 scene and enter Play Mode. Finish or skip the intro in Pohorje. Press Escape: the preview artwork and PAUSED heading should appear with live run stats, gameplay frozen and the cursor available. Use W/S or arrow keys and Enter, then repeat with mouse clicks. Confirm each button activates once. Settings must toggle effects and return with Back or Escape; Escape from pause must restore riding and camera input. Repeat pause/resume several times.

Restart from each map: expect Pohorje, reset run stats and retained permanent unlocks. Quit to Menu must load `MainMenu` with an unlocked visible cursor; New Run must restart at Pohorje without a leftover menu pause. Confirm upgrade selection and death/victory screens still work and Escape cannot bypass either an upgrade offer or the intro. Confirm there is only one RunSession and no new Console errors. Test Quit in a standalone player; it does not close the Editor.

Visual layout, physical keyboard/mouse interaction, audio and Windows player behavior require manual verification; headless smoke checks do not establish these.

## Recorded checks (2026-10-24)

- Unity 6000.6.7f1 import and compilation passed; the new Editor script has its Unity-generated `.meta`.
- Headless Play Mode smoke checks passed for all three maps (exit code 0). This exercises menu events directly; it does not simulate hardware input.
- Architecture audit passed with its existing backlog warnings. `git diff --check` passed.
- Scene comparison confirmed that only the existing V1GameFlow block and SceneRoots list changed, alongside new menu objects. Existing AudioListener counts are preserved.
- Unity's internal QuickSearch startup indexing logged an `ArgumentOutOfRangeException` in `UnityEditor.Search.SearchDatabase.EnumerateAll`; menu checks completed successfully afterward. No menu runtime exceptions or duplicate-listener warnings occurred in the corrected run.
- Manual visual/input/audio checks and Windows player verification have not run.
