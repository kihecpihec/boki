# Upgrade test scene

Open `Game/Assets/Scenes/Upgrade_Test.unity` in Unity 6000.6.7f1 and enter Play Mode. This dedicated upgrade sandbox copies the shared model demonstration, including its movement course, eight enemy stations, HUD, XP pickups and battery charges. `Player_Model_Test`, production scenes and shared prefabs are unchanged.

Open **Boki → Upgrades → Open Test Controls** for Editor-only run diagnostics. No Inspector assignments are needed. **Create Separate Test Scene** creates the scene only when missing and preserves later scene edits. The scene is intentionally not added to the shared build list; this is an Editor sandbox.

## Manual checks

- Add one XP or reach the next threshold. Confirm the real card screen opens, gameplay pauses, and mouse, 1/2/3 and arrows/Enter select exactly one reward. Queue all selections to exercise consecutive offers and the six-selection ceiling.
- Observe stack counts, eligibility, laser multiplier, riding bonus and battery capacity in Test Controls. Choose different builds across sandbox resets; offers remain random and retain production eligibility. The lab does not bypass caps or grant otherwise unavailable rewards.
- Rave Overdrive: compare hits against reset regular enemies. Turbo Skiro: compare riding along the movement course; boost speed stays unchanged. Double Trouble: use a station with multiple visible enemies and confirm two distinct targets take damage.
- Set battery to 50%, choose Bigger Battery, and confirm the charge percentage is preserved. Empty the battery to check movement/light feedback and collect an arena charge. Damage the player before a threshold to make Emergency Snack eligible; check healing clamps at maximum. Battery Charge is a fallback reward when fewer than three ordinary rewards remain, rather than an always-offered upgrade. Follow [Story §5](../Story.md#5-upgrades-and-replay-motivation) for eligibility and caps.
- Choose Panic Mode, visit a Guilt station from the arena panel (Esc), and boost while Guilt chases within 10 m. Check PANIC READY and the boosted speed/duration, then compare outside that condition. Use the existing stations and normal gameplay to test the effect.
- Enable independent menu pause, finish queued choices, and confirm gameplay stays paused until the menu pause is disabled. Kill the player during a choice: cards must close. Restart the sandbox: HP/battery reset, XP/stacks clear, and controls work again.
- Use physical XP pickups and enemy kills as well as diagnostic XP. Confirm kills never restore battery. Check that the Console has no new errors and that restarting creates no duplicate HUD, EventSystem, or upgrade manager.

**Boki → Upgrades → Run Separate Test Scene Checks** runs the existing integration sequence against this scene: required wiring, eight preserved stations, queued selections, independent pause, stale selection rejection, Double Trouble hits, XP contact and death cleanup. It restores the previous scene setup. EditMode upgrade rules tests cover deterministic progression behavior separately.

Windows player performance remains a separate target-machine check; this Editor sandbox does not establish Windows compatibility or FPS.

## Validation in this change

- Unity 6000.6.7f1 imported the new Editor script and generated the scene and both `.meta` files.
- The separate-scene integration sequence completed with `UPGRADE_GAMEPLAY_PASSED` and exit code 0.
- The architecture asset audit passed with no failures; existing warnings remain. `git diff --check` passed.
- Unity's Quick Search startup indexer logged an `ArgumentOutOfRangeException` in `UnityEditor.Search.SearchDatabase`; it did not stop the integration sequence. No game-code exception was logged.
- Interactive visual checks, the Editor diagnostics window, and Windows player performance have not been manually verified. Deterministic rules were unchanged; EditMode tests were not rerun.
