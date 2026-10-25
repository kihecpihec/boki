# Production main menu flow

`Assets/Scenes/MainMenu.unity` is the build entry. It copies the existing menu presentation and audio without changing `Menu_Preview`. Its `MainMenuFlow.menu` references the local `MenuScreen`; the sandbox `MenuPreview` and its notice are removed. No additional Inspector assignments are needed.

`Boki/Menus/Set Up Production Main Menu` creates the scene only if absent and places it first in the build list, retaining the remaining entries. It refuses Play Mode and dirty scenes and restores the previous scene setup. Existing production menu edits are preserved on subsequent runs.

NEW RUN configures the shared session for `V1_Pohorje`, `V1_Maribor` and `V1_FERI`, then uses the existing new-run reset and saved unlock snapshot. Pohorje's integrated intro plays as before. Pause-menu RESTART RUN uses the same session reset. QUIT TO MENU unloads gameplay and loads the production menu. Its menu pause is cleared by the next new run; the intro retains its own scripted pause until finished or skipped.

## Verification

Run the pinned Unity Editor with `-batchmode -nographics -projectPath Source/Game -executeMethod Boki.Editor.V1PauseMenuSetup.SmokeChecks -logFile /tmp/boki-menu-flow-smoke.log`, without `-quit`. The check exits itself. It checks menu launch, NEW RUN, pause/settings/resume, restart, return to the production menu, new-run destination/reset, one session instance and release of menu pause, covering all three V1 maps. It invokes UI events directly and temporarily clears the intro pause; it does not verify physical input or the visual handoff.

Manual checks in `MainMenu`:

1. Enter Play Mode; expect the menu, menu music and a visible unlocked cursor. Test keyboard and mouse selection.
2. Select NEW RUN, then finish or skip the Bellevue intro. Expect control of Boki in `V1_Pohorje`, with the gameplay camera and cursor captured for riding.
3. Pause, visit settings, return, and resume. Expect gameplay to freeze and resume normally.
4. Restart; expect Pohorje with reset run stats and the intro again.
5. Quit to menu from each V1 map; expect the gameplay scene unloaded, menu/music restored and cursor unlocked. NEW RUN must return to Pohorje without leftover pause or duplicate HUD/input/session objects.
6. Complete a level to earn a permanent unlock, return to menu, then start a new run. Expect saved discoveries available in the new snapshot. Confirm route gates, transitions, death/retry, upgrade selection and victory still work.
7. Test QUIT in a standalone player and check the Console for new errors. Windows player behavior remains a Windows verification step.

## Implementation checks (2026-10-25)

- Unity `6000.6.7f1` imported the new scripts and scene and generated their `.meta` files.
- Production setup rerun passed, preserving the generated menu and existing build entries.
- Headless Play Mode flow smoke passed for `V1_Pohorje`, `V1_Maribor` and `V1_FERI` (exit code 0). This includes initial launch from `MainMenu` and the reset/transition checks described above.
- Unity logged the already documented internal QuickSearch startup indexing `ArgumentOutOfRangeException`; no game-flow runtime exceptions occurred.
- All 178 EditMode tests passed (0 failed, 0 skipped).
- Architecture audit passed with existing warnings; whitespace checks passed.
- Physical keyboard/mouse input, visual/audio presentation, intro handoff and standalone Windows QUIT behavior still require the manual checks above.
