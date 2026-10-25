# Retry screen verification

The approved retry layout is wired into `V1_Pohorje`, `V1_Maribor`, and
`V1_FERI`. It uses the existing forest artwork and fonts, a centered `ABSENT`
heading, the current location, three live run totals (kills, XP, upgrades), a
selected `TRY AGAIN` button, and one short discovery reminder. The previous
slogans, repeated retry prompt, paragraphs, and control footer are removed.

`V1GameFlow` reads the totals from the existing `RunSession` when Boki dies.
Retry still uses `RunSession.Retry`. Victory retains its Slovenian heading
and existing message, with death-only location and stats hidden. No gameplay
rules, input assets, shared prefabs, or player demonstration scenes changed.

## Scene wiring

All references are assigned on each scene's `V1 Run UI` / `V1GameFlow`:
`Retry Location` points to `Try Again Styling/Location`, `Retry Summary` to
`Try Again Styling/Run Summary`, and `Retry Kills`, `Retry Xp`, and
`Retry Upgrades` to that summary's value texts. Existing `Panel`, `Title`,
`Details`, `Resume`, and `Retry` references are retained. No Inspector work
remains.

`Boki/Version 1/Upgrade Try Again Screens` reapplies only the terminal-panel
presentation. It refuses Play Mode and dirty scenes and restores the prior
scene setup. The creation-only V1 builder also wires the new presentation
when creating a missing scene; it continues to preserve existing scenes.

## Verification

Run the pinned Unity `6000.6.7f1` Editor with `-batchmode -projectPath Game
-executeMethod Boki.Editor.V1TryAgainSetup.VerifyWithPreview -logFile
/tmp/boki-retry.log` (omit `-quit`; the smoke check exits itself).

The preview renders at 1280×720 and 1920×1080 to
`Path.GetTempPath()/boki-try-again.png` and `boki-try-again-1080.png`. Sample
values are preview data only. The Play Mode smoke check prepares real kills,
XP and accepted upgrades, kills Boki in each V1 map, verifies the displayed
totals and location, checks pause, focus and text bounds, dispatches submit
or pointer-click events, and verifies the new Pohorje run resets health,
kills, XP, upgrades, battery capacity, and route choice. It also exercises
the shared victory panel after retry.

Manual checks: open each V1 scene, enter Play Mode, finish or skip any intro,
collect XP/upgrades and die. Check the layout at 1280×720 and 1920×1080,
including readable stats, the location, and the selected retry button.
Confirm Enter and a physical mouse click each start one fresh Pohorje run.
Repeat with zero kills/upgrades, and complete the ending to check its text.
Check the Console for new errors. Physical input and Windows player checks
remain manual; event dispatch and macOS rendering do not establish them.

No deterministic rules changed, so no EditMode tests were added.

## Recorded checks (2026-10-25)

- Unity `6000.6.7f1` on macOS imported and compiled the changes. No new
  assets or `.meta` files were needed.
- The final 1280×720 and 1920×1080 Unity captures were visually inspected.
- Play Mode smoke checks passed in all three maps, including live stats,
  location, text bounds, selected focus, submit/pointer-click retry, and run
  resets. The victory regression check passed. Logs:
  `/tmp/boki-retry-preserved-scenes.log` and `/tmp/boki-retry-capture.log`.
- Scene object comparison confirmed that every object outside the terminal
  panel and its `V1GameFlow` reference block is unchanged, with no duplicate
  object IDs. Unrelated Unity serialization changes were discarded.
- Architecture audit passed with its existing backlog warnings;
  `git diff --check` passed. No EditMode tests were run for this UI change.
- Unity logged its existing QuickSearch `SearchDatabase.EnumerateAll` startup
  exception during the smoke run; all screen checks completed afterward.
  Physical input, full-run playtesting, and Windows validation remain manual.
