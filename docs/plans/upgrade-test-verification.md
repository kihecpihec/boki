# Upgrade test scene

Open `Game/Assets/Scenes/Upgrade_Test.unity` in Unity 6000.6.7f1 and enter Play Mode. The compact sandbox contains Boki, a riding floor, HUD, upgrade screen and 40 contact XP packs. Copied enemy stations and movement obstacles have been removed. `Player_Model_Test` retains its full sandbox and also demonstrates the upgrade hardware. Production scenes and shared prefabs are unchanged.

No Inspector assignments are needed. `UpgradeManager.batteryModules`, `laserModules` and `secondEmitter` are assigned to scene-local scooter parts. **Boki → Upgrades → Simplify Test Scene** rebuilds this dedicated sandbox and wires hardware in both model scenes; it refuses Play Mode and dirty scenes. **Open Test Controls** offers real XP, recovery, independent pause and reset diagnostics. This Editor sandbox is intentionally absent from the shared build list.

## Manual checks

- Ride through gold XP packs. At thresholds, gameplay pauses. Hover each card: the red selection bar follows it, and Enter selects that card. Alternate hover with arrows, mouse clicks and 1/2/3. When only two rewards remain, the hidden third slot cannot be selected.
- Choose Rave Overdrive: an amplifier appears at the laser for each stack (up to three). Double Trouble adds a second emitter. Bigger Battery adds a deck-mounted battery module for each stack (up to two); current charge percentage remains unchanged. These parts have no colliders and do not modify gameplay stats.
- Turbo Skiro, Panic Mode and recovery rewards add no permanent model parts. Their existing movement, boost, HP and battery feedback remains. Use `Player_Model_Test` enemy stations to verify laser hits and Panic Mode near Guilt; the upgrade sandbox has no enemies.
- Queue remaining selections in Test Controls. Finish them while independently paused and confirm gameplay stays paused until that pause is released. Kill the player during an offer: cards close. Restart the sandbox: XP/stacks clear, hardware disappears, all 40 packs return, and controls remain functional.
- Check the Console for new errors and confirm there is one HUD, EventSystem and upgrade manager. Existing deterministic behavior follows [Story §5](../Story.md#5-upgrades-and-replay-motivation).

**Run Separate Test Scene Checks** verifies the compact scene, 40 packs, pointer selection, queued choices, hardware counts, independent pause, stale selection rejection, Double Trouble hits, contact XP and death cleanup. **Run Gameplay Checks** exercises the shared model scene while verifying its eight enemy stations are preserved. Both restore the prior scene setup.

Interactive appearance and Windows player performance need manual verification; Linux batch checks do not establish Windows FPS or compatibility.

## Validation

- Unity 6000.6.7f1 imported and saved both scenes successfully.
- Compact sandbox and shared model scene integration checks both exited 0 with `UPGRADE_GAMEPLAY_PASSED`, including hover, hardware, XP contact and pause checks. The model scene retained its eight stations.
- Architecture audit: no failures; existing warnings remain. `git diff --check` passed.
- Gameplay rules were unchanged; EditMode tests were not rerun. Interactive visual review and Windows player testing remain manual.
