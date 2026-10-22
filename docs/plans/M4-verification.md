# M4 Handoff and Verification

Implementation branch: `feat/m4-graybox`. Scope and acceptance criteria remain in [the M4 plan](M4.md). The developer explicitly authorized creation and ownership of all three M4 level scenes. Interactive acceptance and Windows target-hardware checks remain pending.

## Open and Play

1. Open `Game/` with Unity **6000.6.7f1** and allow import/compilation to finish.
2. Open `Assets/Scenes/Level1_Pohorje.unity` and enter Play Mode.
3. Ride from the safe opening into A / Strmina or B / Gondola. Entering the gate selects the route immediately; alternatives close. C remains physically blocked.
4. Follow the chosen branch into the shared area and cross the marked Maribor exit. Forest A permits City A/B; Forest B permits only City A on a fresh run.
5. Cross the chosen Maribor branch and the shared `FERI →` exit. City A permits FERI A/B; City B permits only FERI B on a fresh run.
6. Ride through Glavni vhod or Dvorišče, around obstacles, and through the shared classroom doorway. The arrival marker shows `CLASSROOM REACHED — END OF M4 GRAYBOX`.
7. Damage carries through both scene loads. At zero HP, `ABSENT` appears and Retry returns to M4 Pohorje. Escape releases the cursor for Start New Run; clicking outside UI resumes camera control.

These are short grayboxes, not final-duration levels. Each scene contains four preplaced melee instances: three in A and one in B. Their encounter roots start inactive, and only the chosen branch activates, below every level's cap. A routes emphasize melee/obstacle corridors; B routes use service ramps, riverbank geometry, or a courtyard. The AI uses direct pursuit and does not navigate around obstacles. Unchosen enemies remain inactive and absent from laser targeting. Opaque entrance and exit barriers seal unchosen branches, preventing reentry or a view into alternatives from the shared ending. There is no new spawning or navigation system.

C entrances are visible but their interiors are unfinished and remain blocked. M4 neither earns nor saves unlocks. The arrival marker does not start a boss, announce victory, or reset the run. Persistence is M5; ranged enemies, battery behavior, Guilt, boss combat, and entrance modifiers remain M6. No main menu or intro is included.

## Delivered Assets and Inspector Wiring

- `Assets/Scenes/Level1_Pohorje.unity`: tree slalom, gondola service ramps and silhouettes, blocked Rave tunel entrance, and shared Maribor exit.
- `Assets/Scenes/Level2_Maribor.unity`: Tabor wrecks, Lent riverbank/bridge silhouettes, blocked Podzemni prehod entrance, and shared FERI exit.
- `Assets/Scenes/Level3_FERI.unity`: main corridor, courtyard/side corridor with ramps, blocked Servisni vhod entrance, and one shared classroom with desks and lectern.
- `Assets/Scripts/Core/RunSession.cs`: early bootstrap execution, serialized Forest/City/FERI destination paths, City-to-FERI loading, and Retry through the configured entry. Each M4 scene has a configured session bootstrap for direct launch; later duplicates are discarded without replacing the surviving session or state. Dynamically created sessions retain M3 defaults.
- `Assets/Scripts/Routes/RouteGate.cs`: optional rear collider/renderer and encounter-root assignments for M4. Rear seals stay closed until selection, then only the chosen branch opens and activates its preplaced enemies. Existing M3 gates retain their original setup.
- `Assets/Scripts/Routes/RouteManager.cs`: route status includes the FERI choice and current objective. Canonical route rules remain unchanged in `Rules/RunState.cs`.
- `Assets/Scripts/Routes/ClassroomArrival.cs`: scene-local one-time marker, assigned FERI manager and message Text. Requires the active living player and a selected FERI route. It has no save, boss, or scene-transition responsibility.
- `Assets/Editor/M4SceneBuilder.cs`: creates the three owned scenes using copies of the M2 setup through the Editor. Refuses existing M4 destination files, dirty scenes, Play Mode, or an open source combat scene. It does not overwrite delivered scenes. **Boki → Apply M4 Branch Isolation** upgrades existing owned M4 scenes through the Editor, adding missing rear seals and inactive encounter roots while preserving GUIDs; repeat application skips configured gates. Save scenes and exit Play Mode first.
- `Assets/Editor/M4SmokeChecks.cs`: isolated Play Mode probes of saved bindings, real CharacterController gate/exit/arrival contacts, route traversal, direct-entry handling, HP carry, and death/Retry.
- `Assets/Editor/M4PlayerBuild.cs`: explicit three-scene Linux/Windows development player build; leaves shared build settings unchanged.
- `Assets/Tests/EditMode/Routes/RunStateTests.cs`: additional City-to-FERI progression, carried-state, terminal FERI, dead/unselected advancement, and reset coverage.

Each scene binds the player and combat coordinator to session health, assigns its route identity and player/status view to `RouteManager`, and assigns every gate's entrance/rear blockers and renderers, inactive encounter root, trigger, and sign. Exits bind to the local manager. The FERI arrival trigger binds to its local manager and endpoint Text. Local combat coordinators include all four enemies for death shutdown.

Geometry uses primitive colliders on the existing environment layer; player/enemy filtering is unchanged. Gate and endpoint triggers use kinematic, gravity-free Rigidbodies as in M3. Each scene contains one camera/listener, one directional light, and one Input System EventSystem. No scene objects are retained by the persistent session.

Pre-M4 test scenes, existing prefabs, their GUIDs, input assets, packages, project settings, and build settings are preserved. The owned M4 scenes were updated through the Editor for branch isolation without changing their GUIDs. New scenes and script metadata are generated by the pinned Unity Editor in an isolated project copy, then copied back with their assets.

## Reproduce Checks and Builds

Run EditMode tests through Test Runner or the pinned Editor:

```text
-batchmode -nographics -job-worker-count 2 -projectPath <isolated-project> -runTests -testPlatform EditMode -testResults <xml-path> -logFile <log-path>
```

**Boki → Run M4 Smoke Checks** opens FERI to test invalid direct entry, then City, then starts at Pohorje. Save open scenes first; the runner refuses dirty scenes. It temporarily disables autonomous movement/combat and drives the CharacterController along explicit paths, including around the courtyard planter. It exercises three death/Retry cycles, one per level, and all five first-run route histories. Gate/rear-seal probes and the unchosen arrival probe use temporary teleports; branch traversal uses controller movement. It exits Play Mode and logs `M4_SMOKE_PASSED` or `M4_SMOKE_FAILED`.

For batch execution, use `-executeMethod Boki.Editor.M4SmokeChecks.Run` with the common arguments above, omitting test-run arguments and **omitting `-quit`**. The runner exits the Editor after leaving Play Mode. Existing M1/M2/M3 smoke menus and batch methods remain available for regressions.

**Boki → Create M4 Graybox Levels** is a first-creation utility, not a scene reset tool. Do not delete delivered scenes to regenerate them during integration.

**Boki → Build M4 Linux Test Player** and **Boki → Build M4 Windows Test Player** include Pohorje, Maribor, and FERI explicitly. Choose output outside tracked assets. For batch builds, set `BOKI_M4_BUILD_DIRECTORY` and invoke `Boki.Editor.M4PlayerBuild.Linux` or `.Windows`; install the matching build support first.

## Validation Results

Unity **6000.6.7f1**, Linux, isolated project copy:

- All **21 EditMode tests passed**: 12 run/route cases and the existing nine health tests. XML: `/tmp/boki-m4-editmode.xml`.
- M4 Play Mode smoke checks passed for direct City/FERI entry, all five first-run A/B histories, physical gate selection and C blocking, complete controller traversal of chosen branches/ramps/merges, shared classroom arrival, rejection of unchosen arrival, 73 HP carried through both scene loads, and one original session surviving throughout.
- Death shutdown and Retry passed in Forest, City, and FERI, including saved Retry button events, fresh state/HP, and rejection of a repeated request from the old coordinator.
- Existing M1, M2, and M3 smoke checks passed. M3 retains its original scene destinations, actual UI mouse-click/cursor handling, carried HP, and three death/Retry cycles.
- The Editor generated all three scenes and imported all new scripts. New assets have generated metadata; pre-M4 test scenes and existing prefab/metadata files remain unchanged. Asset metadata, duplicate GUID, new scene reference resolution (including package scripts), documentation link target, and whitespace checks passed.
- A Linux x86_64 development player built successfully with the explicit three-scene list. Output: `/tmp/boki-m4-player/Boki-M4.x86_64`. An eight-second headless startup logged no errors/exceptions and ended at the expected timeout (exit 124). This does not test rendered visuals, input, player transitions, or frame rate.

Logs are under `/tmp/boki-m4-*.log`; the final full-traversal smoke log is `/tmp/boki-m4-traverse-paths.log`. Branch isolation also checks rear collider/renderer state, physical reentry rejection, occlusion lines, initially empty encounter registries, and chosen-only enemy activation. Smoke probes temporarily disable autonomous gameplay and use controlled movement/damage; they do not replace an interactive combat and camera check.

## Remaining Acceptance

Manually check sign/HUD readability at 1280×720, movement feel, jump/boost gate bypass attempts, camera clipping under corridor roofs, obstacle/ramp traversal, encounter placement, laser occlusion, cursor/focus recovery, and repeated interactive full runs. Automated controller traversal establishes a physical path; it does not establish input feel or visual quality.

Run a Windows x86_64 player at 1280×720 Low on the Ryzen 5600G. Record scene loading, HP carry, deaths/Retry, the most populated Maribor section, corridor camera behavior, and observed FPS. Linux/headless checks do not establish Windows compatibility or target-hardware performance. Boss and final M7 performance acceptance remain pending.

## Branch Isolation Follow-up

The developer requested this correction during M4 playtesting: seal alternative branches at both ends and activate enemies only for the chosen path. The canonical behavior is now recorded in [Gameplay §4](../Gameplay.md#4-game-state-and-saving-rules).

All three owned M4 scenes now assign rear collider/renderer pairs and inactive encounter roots to their gates. Before selection, rear seals are closed and all branch enemies are inactive. Selection opens only that branch's rear seal and activates its encounter root; other entrances and exits remain opaque and blocked. Existing combat-coordinator references still include inactive enemies so death shutdown and scene recreation on Retry keep working.

The expanded M4 smoke check passed all five histories, including initially empty enemy registries, selected-only activation, rear barrier renderer/collider state, real controller rejection when approaching unchosen branches from the merge, and occlusion lines through rear seals. Full chosen-branch traversal, HP carry, and death/Retry checks still pass. Final follow-up smoke log: `/tmp/boki-m4-isolation-smoke.log`. Scene GUIDs are unchanged; the Editor upgrade helper also completed a repeat application without adding duplicate seals/groups.

M3 regression smoke checks passed with the original test-scene setup. The Linux development player rebuilt successfully with all three updated M4 scenes. Its eight-second headless startup logged no errors/exceptions and ended at the expected timeout (exit 124); log: `/tmp/boki-m4-isolation-player.log`. Logs: `/tmp/boki-m4-isolation-m3.log` and `/tmp/boki-m4-isolation-build.log`. After pulling these scene changes into an already open Editor, exit Play Mode and reopen the M4 scene before testing. Interactive visuals and jump/boost bypass checks remain part of manual acceptance.
