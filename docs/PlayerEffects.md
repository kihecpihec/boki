# Boki model and effects test

Open `Game/` in Unity **6000.6.7f1**, open `Assets/Scenes/Player_Model_Test.unity` alone, and press Play. This is the existing player model scene (the name is `Player_Model_Test`, not `Plaxer_Model_Test`). Use it for all further Boki model, animation, behavior, and effect demonstrations.

The scene combines the rigged Boki and scooter, riding animation, movement and camera, wheel rotation and visual lean, speed FOV, boost HUD, battery headlight and darkness overlay, recharge pickups, cyan/magenta boost trails, red damage flash, and automatic laser. The shared player prefabs and other developers' map scenes are not modified by the consolidation tool.

## Controls and manual verification

Focus the Game view; click to capture the mouse if needed.

- **WASD / mouse:** ride and steer the camera. **Space:** jump. Test the ramp, airborne steering, and landing hops.
- **Left Shift:** boost. Both short trails should emit only while the controller is actually boosting, then fade. Lean, wheels, FOV, and the HUD should follow movement.
- **H:** take 5 actual damage. Boki and the scooter should flash red briefly and return to their original materials; the health HUD should decrease. Repeated presses restart the flash. At zero health the laser, headlight, and trails stop. Restart Play Mode for a fresh health/battery state.
- **T:** create one stationary laser target five metres ahead, replacing any previous test target. The existing `AutoLaser` should fire at it automatically while it is in range and unobstructed. The target can be destroyed; press T to repeat. Use an open area so it spawns clear of obstacles.
- Ride over the three battery charges to refill the headlight. Check its beam follows the scooter, the darkness overlay responds, and the battery HUD updates.
- **Esc:** release the mouse. Test keys require a focused Game view and captured mouse.

Check that trails, laser, headlight, and model render without pink materials or Console errors. Check damage restoration, disable/re-enable cleanup, and that other objects retain their original colors. Windows x86_64 / Ryzen 5600G visual and performance validation remains separate from Linux Editor checks.

## Scene wiring

**Boki → Player Model → Consolidate Effects in Test Scene** refreshes the scene-local setup. Save any dirty scenes and exit Play Mode first. The command opens and saves only `Player_Model_Test`, preserves its existing arena and GUID, and reuses named objects/components on repeated runs. It does not modify build settings, map scenes, or shared prefabs.

`BokiPlayerEffects` owns two short rear `TrailRenderer`s and the model's mesh renderers. Trails reuse `Assets/Effects/Boki/BoostTrail.mat`, last 0.25 s, and cast no shadows. Damage uses per-material property blocks, without cloning shared materials. `BokiModelTestEffects` connects the controller's actual boost timer and accepted health decreases to those effects and provides the scene's H/T test controls. It does not drive the player around automatically.

`AutoLaser` uses a scene-local origin and unshadowed line renderer with the existing laser material. `PlayerHealth`, `LightBattery`, the camera, movement presentation, and HUD retain their existing scene assignments. No additional Inspector setup is required after running the command.

The earlier `BokiEffectsPreview` is a standalone placeholder demonstration. Its effects have been wired onto the real model here; the preview and other scenes were left intact to avoid deleting work or breaking references.

## Validation recorded for this consolidation

Unity 6000.6.7f1 on Linux imported and compiled the assets and generated metadata for the new scripts. Running the consolidation twice retained one effects component, one pair of trails, and the existing player/camera. A batch Play Mode smoke check passed active controller/camera and headlight, accepted-damage flash and timed restoration, trail start/disable cleanup, unchanged shared materials, and automatic laser acquisition/destruction of a spawned target. Unity logged its internal `UnityEditor.Search.SearchDatabase` indexing exception during startup; the smoke check filtered that specific Editor exception and passed. Rendered appearance, physical keyboard interaction, and Windows/Ryzen performance remain manual checks.
