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
- Combat-space occupancy enables replenishment at the canonical cadence and caps. Safe spawn checks require offscreen points at least 8 m away, ground and capsule clearance. Only active regular enemies count against replenishment; the professor has its own arena.
- Regular kills count once and drop one contact-collected XP pack until maximum level. Kills never recharge the battery. Arena charges recharge only while their space is occupied during active gameplay.
- XP thresholds offer eligible upgrades, apply exactly one choice, and queue additional selections without resuming combat. Late offers show fewer cards when fewer are eligible, as approved for V1 and recorded in [Story §5](../Story.md#5-upgrades-and-replay-motivation).
- All six upgrade types are connected, including the second laser hit and Panic Mode's activation-time boost snapshot. Recovery cards obey injury/charge eligibility. Turbo changes ordinary riding speed rather than the base boost speed.
- Ambient Guilt uses the shared per-level pressure state and authored anchors. City C's tunnel reveal suspends ambient pursuit and requires returning through the side door before its exit portal accepts travel. The reveal currently uses a static Guilt silhouette and physical tunnel blocker.
- FERI C's `POWER OFF` panel uses E inside its interaction volume. Arena entry seals the classroom, stops approach encounters and ambient Guilt, applies the 85% starting-HP modifier when earned, and starts the professor. FERI B adds two melee enemies at half boss HP. The professor uses the existing attack presentation and scene-local projectile effects.
- Death shows `ABSENT`; Retry/New Run returns to Pohorje with fresh temporary state and retained discoveries. Victory shows `Danes je sobota.` and New Run. Escape opens a working pause panel. `RunSession` owns suspension and `Time.timeScale`; death, upgrades and pause share that authority.

## Controls and Inspector setup

WASD ride, mouse look, Space jump, Left Shift boost, Escape pause, E interact. Upgrade selection supports mouse, A/D or arrow keys, Enter, and available number keys 1–3. Click the Game view to capture input.

The generated V1 scenes require no further Inspector assignments. **Boki → Version 1 → Create or Upgrade Game Scenes** regenerates their generated integration objects and rewires references. Exit Play Mode and save dirty scenes first. Preserve manual edits in a scene copy before regenerating generated gameplay content. The command leaves the original scenes, shared player/enemy/UI prefabs, packages and input assets unchanged; V1 controls, materials and XP prefab live under `Assets/V1/`.

## Verification commands

From the repository root, use the pinned Editor binary (macOS path shown):

```sh
bash .agents/skills/boki-unity-architecture/scripts/audit.sh
/Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath Game -runTests -testPlatform EditMode -testResults /tmp/boki-v1-editmode.xml -logFile /tmp/boki-v1-editmode.log
/Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath Game -executeMethod Boki.Editor.V1SceneChecks.Run -logFile /tmp/boki-v1-smoke.log
```

Close the Editor holding this checkout before batch verification. Integration checks temporarily isolate the three permanent-unlock keys and restore their original values, input settings and scene setup afterward. They do not save fixture edits into the scenes. Require `V1_INTEGRATION_PASSED`; read the XML rather than relying on the process exit code alone.

## Recorded checks

Unity **6000.6.7f1 on macOS** generated all new asset metadata and the three V1 scenes. EditMode results: **126/126 passed**, none skipped (`/tmp/boki-v1-editmode.xml`). The integration check passed (`/tmp/boki-v1-smoke.log`, `V1_INTEGRATION_PASSED`), exercising physical route triggers, gate/branch isolation, actual combat-space spawning and clocks, kill/XP contact, upgrade pause, state carry through both portals, boss victory, next-run C unlocks, City C side escape, E interaction at `POWER OFF`, the 85% boss modifier, death/retry, and a two-card late offer with hidden-card rejection. Permanent unlock values and input settings were restored after the fixtures.

The asset audit has no failures; existing warnings remain. Unity's existing internal `UnityEditor.Search.SearchDatabase` indexing exception was excluded by its exact stack namespace from gameplay-error collection. A subsequent graphical macOS Editor check confirmed that V1 Pohorje renders with the rigged player and real HUD, that Escape opens the pause panel, and that its Retry/New Run button reloads a fresh starting state without Console errors. Unity was left in paused Play Mode at the V1 start. Full-route riding and movement feel remain manual checks.

## Manual playtest still required

Ride every allowed route from start to end without teleporting. Confirm collider traversal, gate/sign readability, branch isolation, camera comfort indoors, enemy and charge accessibility, safe replenishment, Guilt warnings, and both visible portals. Pause during a chase, projectile flight and battery recharge; verify all resume correctly. Try each upgrade through the actual UI, including a shortened late offer, repeated thresholds, Double Trouble with one/two/occluded targets, and Panic Mode boosts near Guilt. Test death while riding, during boss attacks and after a portal transition, then Retry. Verify the City C turn-back and side door, both professor entrance modifiers, victory and a second run.

A Windows x86_64 player build on the Ryzen 5600G at 1280×720 Low remains required. macOS/headless Editor checks do not establish Windows compatibility, final difficulty, the intended 7–12 minute duration or target frame rate.
