# Player HUD mockup implementation

Scope: `Source/output/hud-mockup-v4/hud-forest.png`, applied only to the owned
`Source/Game/Assets/Scenes/Player_Model_Test.unity`. No map scenes, shared prefabs,
lighting, input assets, packages or project settings are intentionally changed.

`PlayerHud` reads the existing player health, light battery, movement and run XP.
HP and battery sit at bottom left, interval XP progress at bottom center, boost
status at bottom right and location at top left. The top-right watch has been
removed at the user's request.
Bars and text use restrained colors; the built-in Unity font is a fallback,
not the exact pixel font in the concept image.

The XP milestones are display configuration from [Story §5](../Story.md#5-upgrades-and-replay-motivation).
This UI does not collect XP, award upgrades or pause gameplay. Those systems are
still absent on this branch. Story watch beats remain separate narrative
presentation work; this HUD does not display a clock.

## Wiring

Run **Boki > Player Model > Apply Mockup HUD to Test Scene** outside Play Mode
with clean scenes. It updates only `Player_Model_Test`, restores the previously
open scene setup, and can be run again. It creates a `Boki HUD` canvas and assigns
all `PlayerHud` display references plus `Health`, `Battery`, and `Movement` from
the test player. It disables the old movement HUD and battery label without
disabling the darkness overlay. No Inspector assignment is needed to try it.

`Guilt` is optional: assign the scene's `GuiltChaser` to that field when an owned
test encounter exists. The warning uses actual distance during active warning
or chase phases, never changes Panic Mode eligibility, and remains absent with
no Guilt actor. `Screen Effects Enabled` switches the red edge tint off. The
texture is generated once on enable and released on disable; there are no
additional lights or post-processing passes.

## Manual checks

1. Open `Player_Model_Test` (player-owned) and enter Play Mode. Check all corners
   and centered XP at 1280×720 and 1920×1080. The central view must remain clear.
2. Boost: observe READY → ACTIVE → RECHARGING → READY with the existing timings.
3. Ride through a battery pickup after charge drains. BAT text and fill must
   agree, including at empty charge and changed capacity.
4. Damage/heal the test player's `PlayerHealth`; HP text and bar must agree.
5. With an owned Guilt encounter assigned, approach during warning/chase and
   move away. Only the outer edges should tint; disable Screen Effects Enabled
   to remove the tint immediately. Disable/re-enable the HUD without errors.
6. When XP collection is available, check each displayed milestone and max
   progress. This branch does not yet provide interactive XP progression.
7. Check the Console for new errors. Verify the other developer's map scenes
   and prefabs remain unchanged in the Git diff.

## Checks performed

- Unity 6000.6.7f1 Linux batchmode: compiled, generated both script `.meta`
  files, reapplied the setup to an existing HUD, validated required references
  and canvas ordering, saved the scene and exited successfully.
- Architecture audit: no FAILs; pre-existing build-list and duplicated-tooling
  warnings remain outside this task.
- Source/document whitespace check passed. Unity-generated scene YAML retains
  its normal empty-field whitespace.
- Scene object comparison: no original objects removed; original object changes
  are limited to the old HUD/label activation, battery-view disabling, prefab
  reference serialization and the root list.
- No gameplay rules changed; no new rule tests added. Interactive Play Mode
  appearance and Windows target-hardware FPS remain manual checks; Linux
  batchmode compilation does not establish either.

## Why other maps differ

The map test builders and production preview builder open `Player_Model_Test`
and save a separate scene. This is a snapshot, not a shared player/HUD prefab or
an additive gameplay scene. Existing map scenes therefore retain the setup from
when they were generated; later player-test changes do not propagate. The HUD
setup tool intentionally updates only the owned player test scene. Do not rebuild
or overwrite another developer's maps to refresh their gameplay setup. Integrate
a reusable player/presentation setup with the map owner when those scenes are
ready for wiring.
