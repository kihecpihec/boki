# Scooter light and battery handoff

Implemented on `t3/boki-scooter-light`. Rules follow [Gameplay §5](../Gameplay.md#battery-charges-and-action-feedback) and [Story §4](../Story.md#4-enemy-design-and-escalation). Charged light uses ordinary enemy movement speed; the documented penalty is ×1.3 at empty charge, without a separate proximity slowdown.

## Delivered

- `RunState.LightBattery` owns fractional charge, capacity and per-space charge timers in the engine-free `Boki.Battery.Rules` assembly. Charge/capacity carry between levels and reset on New Run. Kills do not recharge it.
- `LightBattery` registers one active player with `RunSession`. Drain/recharge stop during loading, death, `Guilt.GameplaySuspended` or zero scaled delta time. Standalone scenes without this adapter do not start battery ticking.
- A raised, unshadowed 125° white spotlight covers a broad area ahead with an 8 m maximum range. One reusable unshadowed point fill reveals Boki, the scooter and nearby ground. Both follow the player; socket orientation does not determine beam direction. `LightBattery` configures the existing serialized light and creates the fill at runtime, so no new Inspector references are required. Brightness/reach fade with charge, and the darkness overlay contracts toward the rider. At zero charge the beam switches off, while the fill retains intensity 0.12 and range 2.2 m for faint rider visibility. Both lights remain pure white, including during Guilt warning brightness modulation. `MeleeEnemy`, `RangedEnemy` and `GuiltChaser` retain the empty-battery movement modifier; damage and projectile speed are unchanged.
- Contact charges have available/depleted visuals. Full charge leaves them available. Their timers run only in the occupied active space and survive leaving/reentry, including controller disable/teleport. Availability returns visually when the timer expires.
- Three authored locations in each selected route lane, four in Lent, two inside the FERI classroom, and three in the model test arena. Forest C includes its guaranteed charge. Unchosen route encounter roots remain inactive.
- Battery HUD and switchable `ScooterDarkness.Darkness Enabled` Inspector setting. This is not a new pause/settings menu.

## Wiring and review

Already imported and saved using Unity `6000.6.7f1`, with Unity-generated `.meta` files:

- `Assets/Prefabs/Player/Boki_Rigged_Test.prefab`
- `Assets/Scenes/Player_Model_Test.unity`
- `Assets/Scenes/Level1_Pohorje.unity`
- `Assets/Scenes/Level2_Maribor.unity`
- `Assets/Scenes/Level3_FERI.unity`

The user explicitly authorized these scene/prefab changes. Existing scene objects and GUIDs are retained. Environment/map preview scenes were not changed.

No Inspector assignments remain for these assets. `LightBattery.Player` and `Surrounding Light`, each charge's `Space`, `Player`, `Slot`, `Charged Visual` and `Depleted Visual`, and the HUD/camera/shader references are assigned. `BatteryCombatSpace.Space Id` must stay unique and stable per scene when extending the layout. Slots must stay unique per space.

`Boki > Light Battery > Apply to Player and Gameplay Scenes` reapplies the setup in place, refuses Play Mode/dirty scenes and restores the prior scene setup. It also reapplies its presentation tuning; use it deliberately after hand-tuning lighting. `Run Gameplay Checks` runs disposable Play Mode checks, and `Capture Player Preview` writes charged/empty PNGs to the operating system's temporary folder. Neither check saves runtime fixture changes.

Manual review: open `Player_Model_Test` to ride the rigged Boki, then start a run in `Level1_Pohorje`. Ride through charges, wait for depletion/recharge, leave/reenter the area, compare light at zero and after collection, and check branch gates and floor hazards remain readable. Review all three route layouts and the classroom on the target display. The production grayboxes still use their existing player visuals; replacing them with the rigged character is separate work.

## Verification

- Headlight presentation follow-up (`t3/scooter-headlight-lighting`): Unity `6000.6.7f1` compiled the changes; charged/empty Linux previews rendered and were inspected. Updated Play Mode gameplay checks passed with both headlight and fill extinguishing at empty and returning after collection, alongside the existing carry/retry checks. Architecture audit and `git diff --check` passed; existing audit warnings and the unrelated QuickSearch startup exception remain. No scene/prefab edits or new assets were needed. Windows/5600G validation and manual riding remain pending.
- Architecture audit and diff checks (ignoring Unity serializer empty-field trailing spaces): passed; no missing `.meta` files or duplicate GUIDs. Reapplying setup left all five scene/prefab file hashes unchanged. No FAILs; existing build-scene and milestone-helper warnings remain.
- EditMode: all 117 tests passed, including seven new battery tests covering fractional/frame-independent drain, clamping, invalid inputs, empty/recovery speed, full-charge retention, recharge timing, occupancy, capacity percentage and run carry/reset.
- Play Mode gameplay check: passed contact collection, once-only recharge, full-charge retention, selected-only activation, suspension, leaving/reentry timer preservation, empty/recovery presentation and multiplier, Forest-to-City carry, death freeze and Retry reset.
- Charged/empty rigged-player previews rendered and inspected on Linux/OpenGL. No new game-code exceptions; Unity's QuickSearch startup indexer emitted an unrelated `ArgumentOutOfRangeException` in the Editor logs, alongside existing obsolete-API warnings.
- Windows player build, target Ryzen 5600G FPS, and full manual route riding: not run.

## Next work

Connect the authored environment maps to the playable route/arena flow and relocate these charges to their approved markers. Add XP drops/collection and upgrade selection, wire Bigger Battery into the existing percentage-preserving capacity command, and complete the shared pause/menu flow. Professor attacks exist as components, but the current production classroom still only records arrival; boss startup, victory and full encounter integration remain separate work. Add pickup/low-battery audio and final battery meshes, then tune visibility and recovery pacing in a Windows player on the target hardware.

## Nearby visibility tuning verification

Open the shared `Player_Model_Test` scene and enter Play Mode. No new Inspector assignments or scene upgrade are needed: the existing `LightBattery` and `ScooterDarkness` references use the updated presentation. In the runtime Inspector, `Headlight Brightness Multiplier`, `Nearby Brightness Multiplier`, and `Nearby Range Multiplier` tune the healthy light; `Charged Radius Multiplier` and `Charged Opacity Multiplier` tune the overlay.

Compare full, 75%, 50%, 25%, and empty charge using the existing model-test B key (subtracts 25 charge; active drain continues). Full charge should reveal ground and obstacles beside and ahead of Boki. Each lower charge should smoothly dim the lights and contract the visible area; near empty should be barely readable, with only a faint rider fill at zero. Ride over an available charge to verify visibility recovers. Check while turning, pausing and triggering Guilt warnings; the restrained warning brightness modulation should still work and the Console should have no new errors. Visual tuning and Windows player performance on the Ryzen 5600G still require a manual check.

## Forward scooter beam follow-up

The nearby fill now uses a wide, downward-facing spotlight instead of an omnidirectional point light. Both lamps follow the riding frame while retaining their authored scooter socket position, avoiding imported socket rotation pointing the beam at the rider. The warm-white color remains constant during Guilt warnings; restrained brightness modulation preserves the warning cue. Charge changes ease over approximately half a second, with the darkness overlay reading the same eased visibility. Empty battery still extinguishes both lamps immediately. `Player_Model_Test` retains its existing wiring and now explicitly overrides full-charge brightness to ×4 for the main beam and ×12 for the ground fill. No prefab changes are required.

Manual review: compare 100%, 75%, 50%, 25% and empty charge using B, collect a charge, turn and jump, then trigger the Guilt preview. Look for a broad pool ahead of the scooter, a soft beam edge, consistent color and gradual dimming. Confirm pause freezes presentation and empty charge disables both beams.

The shared test-floor material (`Assets/Effects/Boki/Floor.mat`) now uses URP Lit with a matte surface, allowing the headlight pool to appear on the arena ground. Its preview setup helper preserves the lit shader. No render-pipeline settings changed.

Validation for this follow-up: Unity 6000.6.7f1 compiled the changes and the scooter-light Play Mode checks passed, including forward beam direction, empty shutdown and pickup recovery. Charged/empty Linux previews were rendered and inspected. Architecture audit and diff whitespace checks passed. The pre-existing QuickSearch startup exception remains; Windows/5600G performance and manual riding checks remain pending.


## Wide, short white light follow-up

The current presentation supersedes the historical forward-only, warm-white and fully extinguished checks above. `Player_Model_Test` overrides the cone to 125° and the downward angle to 24°; `BokiSoloSetup` preserves this tuning when reapplied. The light origin is raised 1.2 m above its socket, with an 8 m beam range. The second light is a rider-centered point fill, avoiding an additional runtime light. The overlay shifts back onto Boki as charge falls so its remaining clear area follows the faint rider fill.

No new Inspector assignments or assets are required. In `Player_Model_Test`, compare 100%, 75%, 50%, 25% and empty using B; inspect Boki's back, scooter, elevated obstacles and both sides of the immediate riding area. Turn the scooter, trigger Guilt warnings and collect a charge: illumination must remain white and recover smoothly. At empty only Boki, the scooter and very little adjacent ground should remain faintly visible. Check the Console for new errors. Windows player performance on the Ryzen 5600G and manual riding remain pending.

Validation for this follow-up: Unity `6000.6.7f1` compiled the changes, rendered full/half/empty previews on Linux, and passed `ScooterLightChecks.Run` (including the raised wide white beam, faint empty rider fill, pickup recovery, suspension, scene carry and Retry checks). The first full-charge preview prompted reducing the rider fill multiplier from 12 to 3; the final previews were inspected after that adjustment. Architecture audit and `git diff --check` passed with existing audit warnings. The existing Unity QuickSearch startup exception remains. No deterministic battery rules changed, so EditMode rule tests were not rerun. Windows/5600G and manual riding checks remain open.

## Wide, long-reaching white light follow-up

The current tuning supersedes the short-range tuning above: the forward spotlight uses a 140° cone, 24 m maximum range, 18° downward aim and a 0.65 m offset above the scooter socket. Full-charge intensity is 120; the separate rider fill is restrained to 0.715 with a 3.5 m range. White color is applied on enable and color-temperature tinting is disabled. The existing charge smoothing dims and shortens the beam toward 2.4 m before it switches off at zero; the faint empty rider fill remains. The charged darkness opening is enlarged to avoid masking the wider illuminated area. Both lights remain unshadowed, with no additional runtime lights.

`Player_Model_Test` retains its existing references and explicitly overrides cone angle, downward aim and brightness multipliers. `BokiSoloSetup` preserves the updated tuning on reapplication. No new Inspector assignments or assets are needed. Only the shared model demonstration scene was edited; production scenes and shared prefabs were preserved.

Manual review: open `Player_Model_Test`, enter Play Mode and confirm a white beam immediately illuminates a wide area well ahead of the scooter. Boki should remain readable without a glowing or overexposed appearance. Press B repeatedly to compare full, 75%, 50%, 25% and empty charge, then collect an available charge. Both brightness and reach should fall smoothly and recover after collection; empty charge should leave only a faint rider fill. Turn, jump and pause; verify the beam follows the scooter and the Console has no new errors. Windows/5600G performance remains unverified.

Validation: Unity `6000.6.7f1` compiled the changes and `ScooterLightChecks.Run` passed the updated wide/distant white beam check, collection/recovery, suspension, charge carry and Retry checks. Full/half/empty model-scene previews were rendered on Linux and inspected; the rider fill was subsequently moved behind and above the backpack (local position 0, 1.6, -0.9) to soften its hotspot. The scene explicitly stores this offset so cached prefab defaults cannot retain the old close fill position. Architecture audit and `git diff --check` passed. Existing obsolete-API warnings and the unrelated Unity QuickSearch startup exception remain. Battery rules did not change, so EditMode rule tests were not rerun. Manual riding and Windows/5600G checks remain open.

## Surrounding scooter light follow-up

This supersedes the forward cone tuning above. The scooter lamp is an unshadowed white point light with a 28 m maximum radius and full-charge intensity 60. Its origin remains at the scooter headlight socket with a 0.1 m lift. It illuminates front, sides and rear, while rendering layer mask 1 excludes the player model. A second point light on rendering layer mask 2 gently reveals Boki and the scooter with the existing 0.715 full-charge fill. The adapter temporarily puts cached player renderers on mask 2 and restores their authored masks on disable; layer separation already works in both existing URP quality assets. No added light, pipeline-setting change or shared-prefab edit is required.

`Player_Model_Test`, `V1_Pohorje`, `V1_Maribor` and `V1_FERI` retain their references and store the new tuning. The darkness opening is now centred on Boki (forward offset 0) with a larger charged radius multiplier of 3. Both reach and brightness still fade with battery charge, and empty charge leaves only the faint rider fill. No Inspector assignments remain. `Boki/Light Battery/Apply Surrounding Scooter Light to V1 and Model Test` reapplies these scene settings in place. Manual review: ride and turn at full charge, inspect ground ahead, to both sides and behind, and confirm Boki's face/backpack stay softly visible rather than washed out. Press B in the model scene to compare decreasing charge, collect a charge to recover, and compare V1 scenes after the Pohorje intro's authored lamp reveal. Check pause, death and scene transitions for new Console errors. Windows/5600G performance and full manual route riding remain pending.

Validation: Unity 6000.6.7f1 compiled and saved all four scenes. ScooterLightChecks.Run passed, including layer-separated point lighting, empty/recovery presentation, collection, suspension, scene carry and Retry. Full/half/empty model previews rendered on Linux and were inspected; capture checks also passed brightness/reach reduction, rider exclusion from the strong lamp, empty shutdown and rendering-layer restoration after disable/re-enable. Saved settings were verified in all four scene files. Architecture audit and whitespace checks passed with existing warnings; the unrelated QuickSearch startup exception remains. Battery rules did not change, so EditMode rule tests were not rerun. The earlier V1 full-run check remains blocked at its physical Forest selection trigger; Windows/5600G performance and manual route riding remain unverified.

## Front visibility and red Guilt warning follow-up

The existing rider-only fill now stays on the camera-facing side of Boki instead of rotating with his back. `LightBattery` caches the main camera once in `Awake` and updates the fill after the camera in `LateUpdate`; its intensity, range, layer isolation and battery fading remain restrained. The existing fill offset is interpreted in the horizontal camera frame. No additional lights, assets or Inspector assignments are required. This shared adapter applies to `Player_Model_Test` and all V1 scenes using their existing MainCamera tag and light references.

The existing Guilt warning/chase signal now pulses the surrounding scooter lamp between white and red twice per active second. Boki's fill stays white. Pause freezes the warning phase; clearing the signal restores white, and empty battery still keeps the surrounding lamp off. This supersedes earlier instructions to keep the lamp white during Guilt warnings. `BokiModelTestEffects` G preview and the production `PlayerGameplayFeedback` bridge already supply this signal; their activation timing is unchanged.

Manual review: turn toward the camera and orbit it around Boki, comparing face, chest, sides and backpack at full, half and empty battery. In the model scene press G; in V1 enter a Guilt encounter. Confirm the surrounding light flashes red, Boki stays gently readable, pause freezes the flash and the light returns to white when the warning ends. No scene or prefab changes were needed. Windows/5600G performance and manual V1 riding remain unverified.

Validation: Unity 6000.6.7f1 compiled the adapter and passed ScooterLightChecks.Run. The capture checks passed front-facing fill placement, rider isolation, battery fading, layer restoration, red warning, preserved white rider fill, pause freeze, active red/white pulse progression and white recovery. Rear/front, half/empty and red-warning previews were rendered; front and warning frames were inspected. MainCamera tags were verified in the model scene and all three V1 scenes. Architecture audit and whitespace checks passed with existing warnings. The unrelated QuickSearch startup exception remains. Battery rules were unchanged, so EditMode rule tests were not rerun.
