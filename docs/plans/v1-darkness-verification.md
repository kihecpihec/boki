# V1 darkness lighting pass

Changed `V1_Pohorje`, `V1_Maribor`, `V1_FERI`, and `Player_Model_Test` lighting in place. Black fog, nearly black neutral ambient, zero environment reflections and zero directional intensity replace broad colored illumination. Local landmark lights are capped at 3 m and 0.35 intensity. The battery beam and nearby fill use pure white light; the darkness overlay now draws black instead of navy. See [Story §6](../Story.md#6-visual-and-audio-direction).

No Inspector assignments are needed. The existing LightBattery references remain intact. `Boki/Light Battery/Apply Darkness to V1 and Model Test` reapplies the lighting without rebuilding maps or changing the shared player prefab; it refuses Play Mode and dirty scenes and restores the prior scene setup. The V1 builder uses the same settings.

Validation:
- Unity 6000.6.7f1 compiled and ran the lighting upgrade successfully.
- The existing Player_Model_Test Play Mode capture completed at 100 and 0 battery. Inspected both frames after changing the overlay shader: the navy screen tint is gone and the lamp switches off at empty charge. Capture files: `/tmp/boki-light-charged.png` and `/tmp/boki-light-empty.png`.
- Unity logged a SearchDatabase startup indexing ArgumentOutOfRangeException in the capture runs; the capture completed. This was an Editor indexing exception, not a gameplay stack trace.
- Architecture audit: no FAILs; existing warnings remain. `git diff --check` passed.
- Deterministic rules did not change; EditMode tests were not run.

Manual checks still required: ride each V1 route with full, low and empty battery; check enemies become legible in the beam, recovery charges and attack warnings remain visible, and empty-battery riding is recoverable. Refill and confirm the beam returns; pause during a warning and confirm no advancing flicker. Check the Console for gameplay errors. The model-test capture does not establish production-map readability or fear/pacing. Windows player performance on the Ryzen 5600G remains untested.

## Wide white scooter beam in V1

`V1_Pohorje`, `V1_Maribor` and `V1_FERI` now store the 140°/24 m beam tuning, brightness multiplier 24, 18° downward aim and 0.65 m raised socket offset. The restrained rider fill uses multiplier 1.3 and local position (0, 1.6, -0.9); the empty fill remains faint. The lamp itself is saved white with a 120 intensity and no shadows. The overlay matches the model demonstration: opacity 0.3, extra darkness 0.03 and charged radius multiplier 2.4. Existing battery-driven dimming and reach contraction apply in each scene.

Pohorje keeps its existing headlight mount. Maribor and FERI bind to their existing `Socket_Headlight`; no replacement scene, shared prefab edit or new asset is needed. No Inspector assignments remain. Use `Boki/Light Battery/Apply Wide White Light to V1` to update these scenes in place; it refuses Play Mode and dirty scenes and restores the prior setup. The V1 builder also applies this tuning. Presentation float overrides are pinned even when equal to the current script defaults, avoiding reliance on cached inherited defaults. The Pohorje intro retains its authored lamp reveal before riding begins.

Validation: Unity 6000.6.7f1 compiled and saved all three updated scenes. The saved beam, rider fill, mount references and darkness settings were checked in all three files. Unity serializer changes unrelated to lighting were excluded. The broader V1 integration run passed Pohorje's new lighting configuration checks but failed at stage 1, `Physical Forest selection trigger`; this run does not establish end-to-end gameplay correctness. Existing QuickSearch indexing exceptions remain. Architecture audit and whitespace checks passed; battery rules were unchanged, so EditMode rule tests were not rerun. Manual riding in each V1 scene and Windows/5600G performance remain pending.
