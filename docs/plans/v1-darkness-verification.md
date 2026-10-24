# V1 darkness lighting pass

Changed `V1_Pohorje`, `V1_Maribor`, `V1_FERI`, and `Player_Model_Test` lighting in place. Black fog, nearly black neutral ambient, zero environment reflections and zero directional intensity replace broad colored illumination. Local landmark lights are capped at 3 m and 0.35 intensity. The battery beam and nearby fill use warm neutral light; the darkness overlay now draws black instead of navy. See [Story §6](../Story.md#6-visual-and-audio-direction).

No Inspector assignments are needed. The existing LightBattery references remain intact. `Boki/Light Battery/Apply Darkness to V1 and Model Test` reapplies the lighting without rebuilding maps or changing the shared player prefab; it refuses Play Mode and dirty scenes and restores the prior scene setup. The V1 builder uses the same settings.

Validation:
- Unity 6000.6.7f1 compiled and ran the lighting upgrade successfully.
- The existing Player_Model_Test Play Mode capture completed at 100 and 0 battery. Inspected both frames after changing the overlay shader: the navy screen tint is gone and the lamp switches off at empty charge. Capture files: `/tmp/boki-light-charged.png` and `/tmp/boki-light-empty.png`.
- Unity logged a SearchDatabase startup indexing ArgumentOutOfRangeException in the capture runs; the capture completed. This was an Editor indexing exception, not a gameplay stack trace.
- Architecture audit: no FAILs; existing warnings remain. `git diff --check` passed.
- Deterministic rules did not change; EditMode tests were not run.

Manual checks still required: ride each V1 route with full, low and empty battery; check enemies become legible in the beam, recovery charges and attack warnings remain visible, and empty-battery riding is recoverable. Refill and confirm the beam returns; pause during a warning and confirm no advancing flicker. Check the Console for gameplay errors. The model-test capture does not establish production-map readability or fear/pacing. Windows player performance on the Ryzen 5600G remains untested.
