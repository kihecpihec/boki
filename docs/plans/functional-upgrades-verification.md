# Functional upgrades verification

Scope: functional progression in the existing `Player_Model_Test` scene, using the generated upgrade mockup. Production maps, route connections, shared player/enemy/UI prefabs and project settings are untouched. [Story §5](../Story.md#5-upgrades-and-replay-motivation) owns the rules, including the approved two-card exception when only two rewards are eligible.

## Implementation

- `RunState.Progression` owns cumulative XP, typed accepted upgrades, capped additive bonuses and stable distinct eligible offers. Invalid, stale or repeated selections do not consume another reward. Progression shares the run's health and battery instances and survives level advance; New Run resets it.
- `EnemyHealth` counts regular kills and creates one `XpPickup` until max level. Contact awards XP; leftover packs cannot be collected at max level. Kills never charge the battery.
- `UpgradeManager` supplies the real cards to `UpgradeScreen`, applies accepted selections and resolves pending choices without releasing upgrade pause. The unused card is hidden for a two-card offer and cannot be selected by keyboard or mouse.
- `RunSession` owns time scale and named pause reasons. Releasing upgrade pause preserves menu/loading/scripted pause. Movement, camera, laser, physics, battery and ambient Guilt timing freeze during selection.
- `AutoLaser` consumes Rave Overdrive and Double Trouble. The second hit prefers another visible living target in range, otherwise revalidates the original; cadence is unchanged.
- `ScooterMotion` consumes ordinary riding bonuses separately from boost speed. Panic Mode snapshots its speed/duration at boost activation. `ScooterController.guilt` supplies real chase proximity; the HUD shows `PANIC READY` while eligible. The Guilt riding advantage remains separate from that upgrade.
- `HealthState.TryHeal` clamps recovery without reviving a dead player. `LightBatteryState.SetCapacity` preserves charge percentage.

## Owned scene and Inspector wiring

Open `Assets/Scenes/Player_Model_Test.unity`. The scene has a run-bound player, existing `UpgradeScreen` prefab instance, `UpgradeManager`, one EventSystem and the preserved eight-station enemy arena. The active Guilt station supplies its clone and existing spawn markers. Its original movement area, arena layout, eight enemy stations and 27 battery charges are retained. It contains no production map integration. **Boki → Upgrades → Wire Player Model Test** reapplies the setup idempotently, refuses Play Mode/dirty scenes and restores the previous scene setup. It reports a missing model scene instead of creating a replacement.

Assignments made by the helper:

- Player `PlayerHealth.useRunSession = true`.
- Player `UpgradeManager.player` → that player; `screen` → the scene's upgrade screen instance.
- Screen `navigate`, `submit`, `quickSelect[0..2]` → the existing `UpgradeInput.asset` subasset references. The reusable mockup prefab has empty slots; the helper fills only this scene instance.
- `PlayerTestArena` binds `ScooterController.guilt` and `PlayerHud.guilt` to the active station clone at runtime, then clears them on exit. Guilt uses the run session and its existing station-owned spawn markers; no extra Guilt is created.
- The upgrade canvas uses order 50, above the arena panel at 30. Arena controls cannot switch stations or toggle the weapon while an upgrade is pending.

For a future map-owner integration, assign the same components/references on that owner's scene, keep exactly one active upgrade manager and one player driving run health/battery, and provide an EventSystem with Input System UI input. Connect menus and scripted sequences through `RunSession.SetPaused(PauseReason, bool)`; only `RunSession` writes time scale. Guilt still needs the owner's valid spawn markers and combat-space occupancy. No route changes are required by this upgrade system.

## Manual Play Mode checks

1. Play `Player_Model_Test`. Select a regular-enemy station, let the laser kill its enemy, then ride over its XP pack. The preplaced gold samples use the same collector. Repeat to reach the first threshold. Confirm the mockup opens, charge/movement/combat freeze, and the cards describe eligible real rewards.
2. Choose with 1/2/3, arrows plus Enter, or mouse. Confirm exactly one effect applies and gameplay resumes. Repeat through later thresholds; no seventh selection or further collectible XP is available after `MAX LEVEL`.
3. Use H to take damage and B to drain battery. Confirm Emergency Snack is only offered while injured and heals at most maximum HP. Bigger Battery keeps the same percentage (for example 50/100 → 70/140); Turbo Skiro increases riding speed without raising ordinary boost speed.
4. Select Double Trouble. Place two visible targets within range: both take one full hit per cycle. With only one surviving valid target, it can take both hits. A wall, dead target or out-of-range target must not receive the second hit.
5. Select the Guilt station in the arena panel. After its grace/warning, let Guilt approach. With Panic Mode selected, confirm `PANIC READY` within 10 m during a chase. Start a grounded boost there; its extra speed and duration persist if Guilt moves away. A boost outside the condition stays ordinary, and cooldown is unchanged. G only previews the warning visuals and does not control pursuit.
6. With multiple pending choices, resolve them one at a time: combat never resumes between cards. A held/double submit must not consume the next choice. A two-card offer ignores key 3 and wraps navigation over the visible cards only.
7. Verify death/New Run, level carry and overlapping pause reasons in integration. No new Console errors or missing references should appear. Run duration/balance and Windows player performance remain playtest work.

## Automated checks

- Engine-free EditMode tests cover cumulative thresholds, six-selection ceiling, queue/version validation, randomized distinct eligible offers, caps, recovery eligibility/clamps, charge preservation, additive bonuses, run carry/reset, overlapping pause reasons and Panic Mode snapshot behavior.
- **Boki → Upgrades → Run Gameplay Checks** exercises the authored model scene in Play Mode: threshold pause, frozen movement/battery, sequential choices, stale input, independent menu pause, two-target Double Trouble, real XP contact and death cleanup. It restores the previous scene setup. It does not establish production map wiring or Windows performance.

Recorded checks (Unity `6000.6.7f1`, Linux):

- EditMode: **131 passed, 0 failed, 0 skipped**, including 2,000 randomized full runs and the explicit two-card/recovery edge cases. Results: `/tmp/boki-upgrades-editmode-final.xml`.
- Authored-scene batch Play Mode: **UPGRADE_GAMEPLAY_PASSED**. Log: `/tmp/boki-upgrades-gameplay.log`.
- Scene setup helper: succeeded. New scripts/assemblies were imported by the pinned Editor and all `.meta` files were generated by Unity.
- Architecture audit: **no FAILs**. Existing backlog warnings remain; it also reports time-scale writes in `RunSession`, which is now the documented pause authority.
- Interactive visual/input playtest and Windows x86_64 player/FPS validation: **not run**. Follow the manual checklist above; batch Play Mode does not establish either.

Production map wiring remains intentionally with the other developer. No commits or pushes were made.


## Arena reconciliation

The newer eight-station scene from commit `05772e2` was used as the baseline, preserving the other developer's arena instead of combining conflicting scene YAML. Shared camera/HUD additions were retained alongside upgrade behavior. No Git merge, commit or push was performed.

The scene's preplaced XP components were migrated in Unity to `XpPickup`, preserving gold visuals, positions and trigger shapes. The obsolete collector/script metadata were removed together. Stations no longer subscribe to death or award rewards; `EnemyHealth` counts kills and drops one regular-enemy pack, with no XP from professor deaths. The arena binds movement and HUD to its active Guilt clone and clears bindings on exit. Its station controls reject gameplay changes during upgrade pause, and the upgrade screen draws above the test panel.

Combined-scene checks passed in Unity `6000.6.7f1`:

- `/tmp/boki-combined-upgrades-gameplay.log`: `UPGRADE_GAMEPLAY_PASSED`, including eight retained stations, single manager/HUD/EventSystem, overlay order, paused station rejection, queued selections, real XP collection and Double Trouble.
- `/tmp/boki-combined-arena-gameplay.log`: `PLAYER_TEST_ARENA_PASSED`, including eight enemy spawns, 27 charges, exactly one regular kill/drop, boss XP exclusion, actual pickup contacts, Guilt's sole live driver and player binding/cleanup, death and sandbox restart.
- Final combined EditMode suite: **131 passed, 0 failed, 0 skipped** (`/tmp/boki-combined-editmode.xml`).
- Static scene preservation check: all 446 incoming arena GameObjects remain.
- Reapplying **Wire Player Model Test** succeeded without adding another manager/HUD/EventSystem or removing arena content.

Interactive keyboard/mouse feel, rendered visual review and Windows player validation remain manual. Production maps and shared prefabs/settings remain untouched.
