# Version 1 playable integration

Open `Game/` from the `t3/assemble-game-scene` worktree with Unity **6000.6.7f1**. The older `t3-02a8dd7b` preview checkout does not contain these scenes or the current gameplay systems.

## Entry and scene flow

Use **Boki → Version 1 → Open Pohorje** and press Play:

- `Assets/Scenes/V1_Pohorje.unity`: polished Pohorje v3 map.
- `Assets/Scenes/V1_Maribor.unity`: polished Maribor v3 map.
- `Assets/Scenes/V1_FERI.unity`: the authored FERI map and professor arena.

Ride through each level's shared ending portal to advance. Choosing a route is required; clearing enemies is not. Health, battery capacity/charge, XP and upgrades carry forward. There is no backwards portal. New C-route discoveries save immediately but only become available in the next run, following [Gameplay §2–4](../Gameplay.md#2-branching-route-graph).

The three V1 scenes are first in the build scene list. The original production grayboxes and map/model preview scenes are preserved. This version starts directly in Pohorje; the standalone main-menu preview is not an entry scene.

## Integrated systems

- The rigged player, riding presentation, laser feedback and Ismar's real `PlayerHud` come from `Player_Model_Test`. The old movement HUD and all boost text are hidden. Production feedback has no H/T/B/G/X test shortcuts.
- Authored marker positions wire all A/B/C front gates, selection triggers, rear seals, branch visuals and selected-only combat roots. Route availability and lock precedence use the existing run rules.
- Combat-space occupancy enables replenishment at the canonical cadence and caps. Regular spawn intervals now decrease linearly using the shared per-level combat clock, reaching half their starting interval after 120 active combat seconds (Pohorje/FERI: 4 → 2 s; Maribor: 3 → 1.5 s). Existing RouteEncounter components pick this up without scene edits or Inspector assignments. Safe spawn checks require offscreen points at least 8 m away, ground and capsule clearance. Only active regular enemies count against replenishment; the professor has its own arena.
- Regular kills count once and drop one contact-collected XP pack until maximum level. Kills never recharge the battery. Arena charges recharge only while their space is occupied during active gameplay.
- XP thresholds offer eligible upgrades, apply exactly one choice, and queue additional selections without resuming combat. Late offers show fewer cards when fewer are eligible, as approved for V1 and recorded in [Story §5](../Story.md#5-upgrades-and-replay-motivation).
- All six upgrade types are connected, including the second laser hit and Panic Mode's activation-time boost snapshot. Recovery cards obey injury/charge eligibility. Turbo changes ordinary riding speed rather than the base boost speed.
- Ambient Guilt uses the shared per-level pressure state and authored anchors. City C's tunnel reveal suspends ambient pursuit and requires returning through the side door before its exit portal accepts travel. The reveal currently uses a static Guilt silhouette and physical tunnel blocker.
- FERI C's `POWER OFF` panel uses E inside its interaction volume. Arena entry seals the classroom, stops approach encounters and ambient Guilt, applies the 85% starting-HP modifier when earned, and starts the professor. FERI B adds two melee enemies at half boss HP. The professor uses the existing attack presentation and scene-local projectile effects.
- Death shows `ABSENT`; Retry/New Run returns to Pohorje with fresh temporary state and retained discoveries. Victory shows `Danes je sobota.` and New Run. Escape opens a working pause panel. `RunSession` owns suspension and `Time.timeScale`; death, upgrades and pause share that authority.

## Controls and Inspector setup

In the Editor and Development Builds, `PlayerHud` shows horizontal scooter speed in m/s at the top right, refreshed ten times per second with two decimal places. This reads the same `ScooterController.HorizontalSpeed` used by the portal shortcut; vertical jump/fall speed is excluded. It appears automatically in existing V1 scenes and the shared `Player_Model_Test` HUD, requires no Inspector assignments and is compiled out of regular player builds. For a manual check, ride/boost in either scene, confirm the readout rises and returns to zero when stopped, then disable/re-enable the HUD and check that only one readout exists and the Console has no new errors. Actual rendered placement and Windows behavior remain manual checks.

Speed readout validation: Unity 6000.6.7f1 macOS batch import/compilation passed (`/tmp/boki-dev-speed-compile.log`), architecture audit reported no failures (existing warnings remain), and `git diff --check` passed. No gameplay rules changed, so no new rule tests were added.

Visibility fix: V1 deliberately deactivates the old boost text, so cloning it also copied its inactive state. `PlayerHud` now explicitly enables/activates the development readout. **Boki → Version 1 → Run Development HUD Checks** passed in Unity 6000.6.7f1 (`/tmp/boki-dev-speed-smoke.log`, `DEVELOPMENT_HUD_CHECKS_PASSED`): one active readout, text geometry within the Game view, updates from `0.00` to `14.00 m/s`, and no duplicate after HUD disable/re-enable. The check restores preferences/input/scene setup without saving fixture changes. This verifies runtime visibility/geometry rather than a screenshot; graphical appearance and Windows behavior remain manual.

WASD ride, mouse look, Space jump, Left Shift boost, Escape pause, E interact. Upgrade selection supports mouse, A/D or arrow keys, Enter, and available number keys 1–3. Click the Game view to capture input.

The generated V1 scenes require no further Inspector assignments. **Boki → Version 1 → Create Missing Game Scenes** creates only absent V1 scene files from `Player_Model_Test`. Existing scenes are skipped before opening or cleaning them, including files not yet imported into the AssetDatabase. When all three exist, the command returns without changing scenes, V1 assets or build settings. When creating missing scenes, it retains existing build entries and appends newly created scenes only if absent from the list. Exit Play Mode and save dirty scenes first. Existing scene updates require a targeted, owner-approved upgrade helper; this command no longer regenerates their gameplay objects or maps. Template cleanup runs only on newly copied scenes. Shared player/enemy/UI prefabs and packages remain unchanged; V1 controls and materials live under `Assets/V1/`; regular XP drops use the shared `Boki.Upgrades.XpPickup` adapter.

### Scene preservation regression check

Run **Boki → Version 1 → Run Scene Preservation Checks**, or use the pinned Editor with `-batchmode -nographics -quit -projectPath Game -executeMethod Boki.Editor.V1SceneSetup.RunPreservationChecks -logFile /tmp/boki-scene-preservation.log`. Require `V1_SCENE_PRESERVATION_PASSED` and no compilation/check errors. All three V1 scenes must already exist. The check reruns setup twice and checks existing scene, V1 asset and build-settings contents, then uses a disposable scene to verify that a custom collider root, audio root, `FERI_Map` root and authored child under a `V1 Gameplay` root survive repeated creation attempts. It also checks rejection of dirty scenes, deletes the disposable scene through Unity and restores the prior scene setup. Production scenes are not saved by this check. No Inspector assignments are needed.

Validation on 2026-10-25: Unity 6000.6.7f1 macOS batch import/compilation and the preservation check passed (`/tmp/boki-scene-preservation.log`, `V1_SCENE_PRESERVATION_PASSED`, exit 0). The architecture audit passed with existing warnings, and `git diff --check` passed. No production scene, prefab, V1 asset or project-settings changes resulted. The fresh-scene generation path and Windows Editor behavior were not exercised; gameplay rules were unchanged, so the EditMode rule suite was not rerun.

## Verification commands

From the repository root, use the pinned Editor binary (macOS path shown):

```sh
bash .agents/skills/boki-unity-architecture/scripts/audit.sh
/Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath Game -runTests -testPlatform EditMode -testResults /tmp/boki-v1-editmode.xml -logFile /tmp/boki-v1-editmode.log
/Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath Game -executeMethod Boki.Editor.V1SceneChecks.Run -logFile /tmp/boki-v1-smoke.log
```

Close the Editor holding this checkout before batch verification. Integration checks temporarily isolate the three permanent-unlock keys and restore their original values, input settings and scene setup afterward. They do not save fixture edits into the scenes. Require `V1_INTEGRATION_PASSED`; read the XML rather than relying on the process exit code alone.

## Recorded checks

Unity **6000.6.7f1 on macOS** generated all new asset metadata and the three V1 scenes. After reconciling current `dev`, EditMode results: **143/143 passed**, none skipped (`/tmp/boki-v1-merge-editmode.xml`). The reconciled V1 integration check passed (`/tmp/boki-v1-merge-smoke.log`, `V1_INTEGRATION_PASSED`), exercising physical route triggers, gate/branch isolation, actual combat-space spawning and clocks, kill/XP contact, upgrade pause, state carry through both portals, boss victory, next-run C unlocks, City C side escape, E interaction at `POWER OFF`, the 85% boss modifier, death/retry, and a two-card late offer with hidden-card rejection. Permanent unlock values and input settings were restored after the fixtures.

The asset audit has no failures; existing warnings remain. Unity's existing internal `UnityEditor.Search.SearchDatabase` indexing exception was excluded by its exact stack namespace from gameplay-error collection. A subsequent graphical macOS Editor check confirmed that V1 Pohorje renders with the rigged player and real HUD, that Escape opens the pause panel, and that its Retry/New Run button reloads a fresh starting state without Console errors. Unity was left in paused Play Mode at the V1 start. Full-route riding and movement feel remain manual checks.

### Portal shortcut verification

The threshold is now 18 m/s. Unity 6000.6.7f1 macOS validation passed all **215 EditMode tests** (`/tmp/boki-portal-18-editmode.xml`) and the full V1 integration run (`/tmp/boki-portal-18-smoke.log`, `V1_INTEGRATION_PASSED`), including physical skip entry at exactly 18 m/s through both portals and normal progression at 17.999 m/s. The architecture audit and diff checks passed; existing warnings and manual platform/presentation checks remain as described below.

Unity **6000.6.7f1 on macOS** compiled/imported the changes. All **214 EditMode tests passed**, none skipped (`/tmp/boki-fast-portal-editmode.xml`). The full V1 integration run passed (`/tmp/boki-fast-portal-smoke.log`, `V1_INTEGRATION_PASSED`), including physical Forest/City portal entry at qualifying horizontal speeds, normal 10.5 m/s entry into Maribor, safe classroom arrival, the sealed/inactive FERI approach, resource/upgrade carry, default boss startup, duplicate boss-entry rejection, victory/ending, death/retry, and next-run discoveries without skipped-level rewards. The existing route, upgrade, City C escape and `POWER OFF` checks also passed. Permanent unlock preferences, input settings and scene setup were restored; scene/prefab assets were not edited.

The integration fixture now uses current XP thresholds and allows 180 seconds for the additional scene/retry cycles. It disables focus-dependent recorded dialogue during gameplay checks; spoken opening lines and camera presentation remain manual. A pre-existing V1 startup error was fixed in `ComboView`: `CanvasScaler` already requires/creates its Canvas, so the view now retrieves it instead of adding a duplicate. Architecture audit and `git diff --check` passed (existing audit warnings remain). No new assets or Inspector assignments are required.

## Manual playtest still required

Air-strafe/timed-hop headroom: `ScooterController` now defaults **Momentum Speed Headroom** to 0.1 m/s, added to the existing base-speed multiplier, for an 18.1 m/s gain ceiling with the 6 m/s player. This applies automatically in V1 and `Player_Model_Test` without scene/prefab edits or Inspector assignments. The portal still requires 18 m/s. Unity 6000.6.7f1 macOS passed all **217 EditMode tests** (`/tmp/boki-momentum-181-editmode.xml`), including air-strafe ceilings with and without headroom, repeated hops above 18 m/s but below the new ceiling, and preservation of faster downhill speed. Audit and diff checks passed with existing warnings. In Play Mode, build speed in `Player_Model_Test` or V1, confirm the development meter can exceed 18 m/s through air strafing/timed hops, then test the portal; actual riding feel and Windows checks remain manual.

For the [V1 speed shortcut](../Gameplay.md#11-v1-portal-speed-shortcut), use the existing `V1_Pohorje` and `V1_Maribor` shared exit portals. No scene edits, regeneration or new Inspector references are needed; `LevelPortal`'s new **Boss Skip Speed** defaults to 18 m/s on existing components. Build horizontal momentum through downhill riding, air strafing or timed hops and cross a portal at or above that speed after choosing a route. Expect to arrive inside the sealed classroom with the professor active and current HP, battery, XP and upgrades retained. No hints or special portal feedback should appear. Standard boost speed must still load the next level normally. City C must reject travel until its side escape is complete, at any speed. Win and die after the skip, then retry: each returns to a fresh Pohorje run, and discoveries for skipped levels remain locked. Check the Console for new errors. Real riding feel and Windows validation remain manual.

Ride every allowed route from start to end without teleporting. Confirm collider traversal, gate/sign readability, branch isolation, camera comfort indoors, enemy and charge accessibility, safe replenishment, Guilt warnings, and both visible portals. Pause during a chase, projectile flight and battery recharge; verify all resume correctly. Try each upgrade through the actual UI, including a shortened late offer, repeated thresholds, Double Trouble with one/two/occluded targets, and Panic Mode boosts near Guilt. Test death while riding, during boss attacks and after a portal transition, then Retry. Verify the City C turn-back and side door, both professor entrance modifiers, victory and a second run.

A Windows x86_64 player build on the Ryzen 5600G at 1280×720 Low remains required. macOS/headless Editor checks do not establish Windows compatibility, final difficulty, the intended 7–12 minute duration or target frame rate.

## Integration with current dev

V1 uses the shared `Boki.Upgrades.Rules.UpgradeState`, automatic `EnemyHealth` XP drops, existing scooter modifiers and reason-based `PauseState`. The earlier V1-only progression/reward implementation was removed during reconciliation so upgrades and kills cannot be counted twice. Existing hover selection, scooter effects, isolated player/upgrade/enemy test scenes and cleanup changes from dev are preserved.

The existing shared upgrade gameplay check also passed after reconciliation (`/tmp/boki-v1-merge-upgrades.log`, `UPGRADE_GAMEPLAY_PASSED`). This verifies that the preserved player-model upgrade sandbox remains compatible alongside V1.

### Spawn ramp verification

Open each existing V1 scene (`V1_Pohorje`, `V1_Maribor`, `V1_FERI`) and select a route. In its combat space, keep regular enemy counts below the desired count and allow an offscreen, collision-free spawn point at least 8 m away. Observe attempts starting at the level's initial cadence and shortening toward half that interval over 120 active combat seconds; invalid points can delay actual spawns. After another minute the interval must remain at the minimum. Pause and open an upgrade offer mid-ramp, then resume: neither advances combat time or produces a catch-up burst. Leave and reenter: progress remains. Advance to the next level or Retry: the starting interval returns. Boss entry stops replenishment. Check the Console for new errors. These Play Mode checks and Windows target-hardware performance checks remain manual.

Automated validation: Unity 6000.6.7f1 imported the new scripts and generated their `.meta` files; all 166 EditMode tests passed, including 14 spawn cadence cases covering ramp values, clamping, retained progress, suspension and reset. Architecture audit reported no FAILs (existing warnings remain); `git diff --check` passed.
