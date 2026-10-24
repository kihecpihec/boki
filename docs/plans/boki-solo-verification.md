# Boki solo scene and scooter emitters

`Assets/Scenes/Boki_Solo_Test.unity` is a separate copy of `Player_Model_Test`, reduced to the rigged Boki, existing movement/camera, HUD, flat floor and boundary walls, and three battery charges. Enemy stations, their panel, obstacles, and enemy templates are removed from the copy. The original model scene retains its arena and receives the same emitter fixes. Shared player prefabs and production scenes are not modified.

The laser origin is attached to the imported `Socket_LaserMuzzle`, offset to the small emitter on its housing. The slimmer tapered beam updates its start after scooter presentation so lean, jumps and turns do not leave it floating. Target selection, range, damage and cadence retain the [M2 rules](../Gameplay.md#m2-combat-baseline).

`LightBattery.Headlight Mount` references a child of `Socket_Headlight`. The existing light is positioned at that mount in Edit Mode and attached to it in Play Mode, with a narrower downward spot and a small shadowless fill. Existing scenes without a mount retain their root-relative light placement. Charge dimming and empty battery remain unchanged. Both player scenes opt into a stronger red double-pulse warning using the existing spot and fill.

## Editor setup

The saved scenes contain all assignments. To create the solo copy when missing or refresh the emitters, use **Boki → Player Model → Create Solo Scene and Fix Scooter Emitters**, outside Play Mode with clean scenes. The command preserves an existing solo scene's layout, refuses a missing original, and restores the previous scene setup. It does not add the test scene to shared build settings.

Open `Boki_Solo_Test` and enter Play Mode:

- WASD and mouse: ride and orbit; Space: jump; Left Shift: boost. Wheels, lean, trails, camera and boost HUD should remain functional. Rear-wheel dirt should increase with speed/boost, stop emitting when stationary or airborne, and a separate dust burst should appear on landing.
- T: spawn a temporary laser target. Inspect the beam from the front and side while steering and jumping; it must begin at the scooter emitter.
- H: take damage; X: preview XP glow; G: preview the red double-pulse warning.
- B: drain 25 battery. Verify the lamp dims and goes out at zero. Ride over a green charge to recover light and show pickup feedback.
- Esc: release the mouse; click the game to capture it again. Stop/start Play Mode to reset the standalone test.
- Check that HP, battery, XP and boost remain readable and the Console has no new script errors.

Run **Boki → Player Model → Run Solo Feedback Checks** for live checks of socket assignments, battery dimming/empty state, contact recharge/glow, pause, XP glow, warning wiring, laser hits/impact, landing squash and cleanup. **Run Feedback Checks** covers the original scene.

## Validation

Unity 6000.6.7f1 imported and compiled the changes and generated metadata for the new helper and scene. Reapplying the setup succeeded with one headlight mount per scene and retained the existing solo layout. The solo scene's serialized-reference checks passed, including one player/HUD/camera, no enemy stations and no preplaced enemies.

Both `RunSolo` and the original scene's `Run` reported `BOKI_MODEL_FEEDBACK_PASSED`. These exercised runtime socket attachment, a nearby fill within 0.5 m of its mount despite FBX conversion scale, battery dimming/empty state, accepted contact recharge and glow, shared pause, XP glow, warning wiring, laser hit/impact and beam origin, collision-root scale, landing squash recovery and disable cleanup. The existing landing check now waits for active simulation to recover to the saved scene's authored scale; a fixed wall-clock delay was unreliable during batch startup.

The architecture audit passed metadata pairing and unique GUIDs with no FAILs; its existing warnings remain. `git diff --check` passed. No deterministic gameplay rules changed, so EditMode rules tests were not run. Unity logged the previously documented internal `UnityEditor.Search.SearchDatabase` startup exception; the feedback checks completed without new gameplay-script errors.

Rendered appearance, physical keyboard/camera feel and Windows x86_64 performance on the Ryzen 5600G require manual verification; Linux batch checks do not establish them.

## Scooter dirt and stronger warning pass

The saved solo and model-test scenes now include rear-wheel dirt (64-particle cap) and landing dust (24-particle cap), referencing the existing feedback material. The setup helper updates named children without rebuilding either scene. `LightBattery.strongGuiltWarning` is enabled in these scenes for a brighter red double-pulse and expanded nearby fill; the other scenes retain the previous presentation. See [riding feedback](../PlayerEffects.md#scooter-riding-feedback) for assignments and manual checks.

Unity 6000.6.7f1 on Linux compiled the changes and the updated solo and original model-scene Play Mode feedback checks passed, including particle budgets, particle pause, landing dust emission, red warning color and disable cleanup alongside the existing battery/laser checks. The check's manually seeded particles are explicitly paused, matching runtime emission. The Editor still reports its previously recorded Search indexing exception. No deterministic rules changed. Rendered dirt placement, warning readability, keyboard feel and Windows/5600G performance remain manual checks.
