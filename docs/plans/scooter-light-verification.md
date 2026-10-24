# Scooter light and battery handoff

Implemented on `t3/boki-scooter-light`. Rules follow [Gameplay §5](../Gameplay.md#battery-charges-and-action-feedback) and [Story §4](../Story.md#4-enemy-design-and-escalation). Charged light uses ordinary enemy movement speed; the documented penalty is ×1.3 at empty charge, without a separate proximity slowdown.

## Delivered

- `RunState.LightBattery` owns fractional charge, capacity and per-space charge timers in the engine-free `Boki.Battery.Rules` assembly. Charge/capacity carry between levels and reset on New Run. Kills do not recharge it.
- `LightBattery` registers one active player with `RunSession`. Drain/recharge stop during loading, death, `Guilt.GameplaySuspended` or zero scaled delta time. Standalone scenes without this adapter do not start battery ticking.
- An unshadowed forward spotlight shines from scooter height, with a dim, short-range point fill for nearby ground. `LightBattery` configures the existing serialized light as the headlight and creates one reusable fill at runtime, so existing scenes and prefabs need no rewiring. The headlight position, downward angle, range/cone and fill intensity/range are Inspector tuning fields. Dim ambient/main lighting, simple fog and a soft darkness overlay surround it; the clear area is smaller and shifted ahead, with darker edges. Empty charge extinguishes both lights and reduces the clear area. `MeleeEnemy`, `RangedEnemy` and `GuiltChaser` read the empty-battery movement multiplier; damage and projectile speed are unchanged.
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
