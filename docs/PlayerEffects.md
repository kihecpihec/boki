# Boki model and effects test

Open `Game/` in Unity **6000.6.7f1**, open `Assets/Scenes/Player_Model_Test.unity` alone, and press Play. This is the existing player model scene (the name is `Player_Model_Test`, not `Plaxer_Model_Test`). Use it for all further Boki model, animation, behavior, and effect demonstrations.

The scene now starts in an isolated enemy-testing arena with the same player. See [enemy sandbox controls and checks](plans/player-test-arena-verification.md). Press Escape to use its panel; **Original movement area** returns to the original ramp and obstacles. The separate stations include contact-collected sandbox XP packs and battery charges. Production XP progression and upgrade selection remain separate work.

The scene combines the rigged Boki and scooter, riding animation, movement and camera, wheel rotation and visual lean, speed FOV, boost HUD, battery headlight and darkness overlay, recharge pickups, cyan/magenta boost trails, red damage flash, automatic laser with an impact burst, takeoff lean, landing squash, Guilt warning flicker/camera pulse, and pickup glows. The shared player prefabs and other developers' map scenes are not modified by the consolidation tool.

## Controls and manual verification

Focus the Game view; click to capture the mouse if needed.

- **WASD / mouse:** ride and steer the camera. **Space:** jump. Test the ramp, airborne steering, and landing hops. The rig should lean back slightly at takeoff, squash briefly on contact, then return to its authored scale. The CharacterController and collision root must keep their original size.
- **Left Shift:** boost. Both short trails should emit only while the controller is actually boosting, then fade. Lean, wheels, FOV, and the HUD should follow movement.
- **H:** take 5 actual damage. Boki and the scooter should flash red briefly and return to their original materials; the health HUD should decrease. Repeated presses restart the flash. At zero health the laser, headlight, and trails stop. Restart Play Mode for a fresh health/battery state.
- **T:** create one stationary laser target five metres ahead, replacing any previous test target. The existing `AutoLaser` should fire at it automatically while it is in range and unobstructed. Each hit shows a 0.08-second beam and an eight-particle cyan impact burst; the target also flashes with existing enemy feedback. The lethal beam remains visible for its full short duration. Press T to repeat. Use an open area so it spawns clear of obstacles.
- **B:** drain 25 battery for testing. Headlight and nearby fill should progressively weaken with the charge fraction; at zero they switch off. Ride over the three original battery charges or a station's three charges to refill the headlight and emit a brief green pickup glow. A full battery must leave a charge available; standing on a depleted charge must not retrigger the glow.
- **G:** preview headlight flicker and the restrained camera FOV pulse for three active seconds. This does not spawn Guilt or change pressure. The same effects read the shared run's active Warning/Chase phases, excluding pending placement and suspended pressure. Disable `ScooterPresentation.cameraPulseEnabled` in the Inspector to turn off the screen pulse.
- **X:** preview the gold XP glow without awarding XP. Actual increases to `RunSession.State.XP` also trigger it, including contact collection of the enemy sandbox's gold packs. Gold packs now use the shared progression collector and open the real upgrade mockup at the thresholds in [Story §5](Story.md#5-upgrades-and-replay-motivation).
- **Esc:** release the mouse. Test keys require a focused Game view and captured mouse.

Check that trails, laser, headlight, and model render without pink materials or Console errors. Check damage restoration, disable/re-enable cleanup, and that other objects retain their original colors. Windows x86_64 / Ryzen 5600G visual and performance validation remains separate from Linux Editor checks.

## Scene wiring

**Boki → Player Model → Consolidate Effects in Test Scene** refreshes the scene-local setup. Save any dirty scenes and exit Play Mode first. The command opens and saves only `Player_Model_Test`, preserves its existing arena and GUID, and reuses named objects/components on repeated runs. It does not modify build settings, map scenes, or shared prefabs.

`BokiPlayerEffects` owns two short rear `TrailRenderer`s and the model's mesh renderers. Trails reuse `Assets/Effects/Boki/BoostTrail.mat`, last 0.25 s, and cast no shadows. Damage uses per-material property blocks, without cloning shared materials. `BokiModelTestEffects` connects the controller's actual boost timer and accepted health decreases to those effects and provides the scene's H/T/B/G/X test controls. It subscribes to the three `BatteryCharge.Collected` events, which fire only after an accepted recharge, and observes XP gains from the shared run. It does not drive the player around automatically.

`AutoLaser` uses a scene-local origin and unshadowed line renderer with the existing laser material; `impactBurst` references the scene's `Laser Impact` particle system. `BokiPlayerEffects.pickupGlow` references `Visuals/Pickup Glow`. Both use `Assets/Effects/Boki/FeedbackParticles.mat` with the existing vertex-color particle shader. The bursts are capped at 16 impact / 32 pickup particles, add no lights, and advance through explicit simulation so shared pause freezes them. Disabling the owning component clears them.

`ScooterPresentation.visualRoot` is the existing non-colliding `Visuals` transform; all lean/squash stays there. `LightBattery` scales its existing spot and fill intensities by charge fraction, retaining the zero-charge gameplay penalty. The warning bridge sets their cosmetic flicker strength and the camera pulse without changing pressure, movement or damage rules. `PlayerHealth`, `LightBattery`, the camera, movement presentation, and HUD retain their existing scene assignments. No additional Inspector setup is required after running the command.

The earlier `BokiEffectsPreview` is a standalone placeholder demonstration. Its effects have been wired onto the real model here; the preview and other scenes were left intact to avoid deleting work or breaking references.

## Validation recorded for this consolidation

Unity 6000.6.7f1 on Linux imported and compiled the assets and generated metadata for the new scripts. Running the consolidation twice retained one effects component, one pair of trails, and the existing player/camera. A batch Play Mode smoke check passed active controller/camera and headlight, accepted-damage flash and timed restoration, trail start/disable cleanup, unchanged shared materials, and automatic laser acquisition/destruction of a spawned target. Unity logged its internal `UnityEditor.Search.SearchDatabase` indexing exception during startup; the smoke check filtered that specific Editor exception and passed. Rendered appearance, physical keyboard interaction, and Windows/Ryzen performance remain manual checks.

## Action feedback verification

Run **Boki → Player Model → Run Feedback Checks** with clean scenes outside Play Mode. This opens the authored scene, exercises live references and pickup contacts, then restores the previous scene setup. Unity 6000.6.7f1 batch Play Mode checks passed battery dimming/empty light, accepted contact recharge and glow, particle freeze during shared pause, XP-gain glow, shared Guilt warning wiring, lethal laser beam/impact, landing squash with unchanged collision-root scale, scale recovery, and disable cleanup. The Editor logged the same internal `UnityEditor.Search.SearchDatabase` startup indexing exception recorded above; no feedback script errors were logged.

Asset import/compilation and metadata generation completed in the pinned Editor. Physical keyboard takeoff/boost feel, rendered appearance (including Low settings), camera comfort/toggle, and Windows x86_64 FPS on the Ryzen 5600G remain manual checks. This feedback pass changes no deterministic gameplay rules; no new EditMode rules tests are needed.
