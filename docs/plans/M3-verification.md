# M3 Handoff and Verification

Implementation branch: `feat/m3-routes`. The developer confirmed manual gameplay acceptance after the cursor/UI fix; Windows checks remain pending. Scope and acceptance criteria remain in [the M3 plan](M3.md); canonical connection, lock, and state rules remain in [Gameplay §2–4](../Gameplay.md#2-branching-route-graph).

## Open and Play

1. Open `Game/` with Unity **6000.6.7f1** and allow import/compilation to finish.
2. Open `Assets/Scenes/Routes_Forest_Test.unity` and enter Play Mode.
3. Use WASD, mouse, Space, and Left Shift. Enter the left A lane or middle B lane to select it. C remains blocked. The alternatives close immediately, and the selected choice appears in the HUD.
4. Ride through the selected lane into the shared area and cross the exit at the far end to load City. Forest A permits City A/B; Forest B permits only City A in a fresh run. C signs explain whether the previous route or the permanent lock blocks entry.
5. Remaining HP carries into City. Melee enemies and the automatic laser use the existing M2 combat baseline. At zero HP, `ABSENT` appears; Retry starts at Forest with full HP and cleared choices.
6. Use **Start New Run** to restart during live play. Escape releases the cursor; click the button while the cursor is released. UI clicks keep the cursor free until the new scene starts; clicking outside UI resumes camera control. Opening City directly displays a prerequisite message and blocks selection until a Forest run is started.

The City endpoint marks the end of the M3 demonstration. There is no FERI scene or boss in this slice. FERI connections are implemented and tested as deterministic rules. C unlocks default to false and are not earned or saved in M3; M5 supplies persistence. There is no player-facing unlock switch.

The developer authorized ownership of the two new route test scenes. Existing scenes, prefabs, input actions, build settings, project settings, and package manifests are unchanged.

## Assets and Inspector Assignments

- `Assets/Scripts/Routes/Rules/RunState.cs`: level/choice/gate identities, copied unlock snapshot, run defaults/reset, canonical connection rules, guarded selection/advance, and state-change notification. The rules assembly references the existing combat rules assembly for its authoritative `HealthState`.
- `Assets/Scripts/Core/RunSession.cs`: one `DontDestroyOnLoad` session, validated Forest/City destinations, duplicate protection, transition guard, and fresh-run initialization. `NextRunUnlocks` is an in-memory integration point for M5. It never changes an active run's snapshot.
- `Assets/Scripts/Player/PlayerHealth.cs`: **Use Run Session** is enabled on the M3 scene instances. Their HP comes directly from the session's `HealthState`; there is no second synchronized HP store. Existing M2 instances default to local health.
- `Assets/Scripts/Core/CombatSession.cs`: **Use Run Session** is enabled on M3 scene instances. Death still shuts down scene-local controls/combat and shows the existing view; Retry delegates to the session. M2 keeps its original scene reload behavior.
- `Assets/Scripts/Routes/RouteManager.cs`: assigned level, scene player, and route-status Text. It binds to the surviving session, publishes gate refreshes, and rejects inactive-level, dead-player, and loading-time selection.
- `Assets/Scripts/Routes/RouteGate.cs`: each gate has its route identity, manager, primitive barrier collider/renderer, separate selection trigger, and TextMesh sign assigned. Locked gates disable selection and show the canonical message; selection closes alternatives synchronously.
- `Assets/Scripts/Routes/LevelExit.cs`: Forest-only test transition, assigned manager, with a shared exit trigger beyond the lane merge. It requires a living player with a selected Forest route. It does not save milestone unlocks.
- `Assets/Tests/EditMode/Routes/`: a separate EditMode assembly and deterministic run/route tests. Existing combat test assembly is unchanged.

Both scenes reuse the M1 player and M2 enemy prefabs through scene instances. They contain one camera/listener, one directional light, the existing combat UI/EventSystem, four melee instances, three primitive gate lanes, and a shared endpoint. The existing enemy AI uses direct pursuit and cannot plan around walls; this limitation is unchanged.

Gate roots and the Forest exit use kinematic, gravity-free Rigidbodies with trigger colliders, allowing the player's CharacterController to generate actual trigger contacts. Barrier colliders are separate children. Geometry uses `Default`, included in existing camera and weapon environment masks; player/enemy geometry retains `Ignore Raycast`. No shared layer edits are required.

The persistent session contains no scene-object references. Scene views/managers and health wrappers remove their subscriptions when disabled or destroyed. Retry resets run HP, battery, XP, upgrades, choices, kill count, start time, and unlock snapshot; scene reload recreates enemies, spawn, camera, and combat cooldowns.

## Reproduce Checks and Builds

Run EditMode tests in Test Runner, or invoke the pinned Editor with:

```text
-batchmode -nographics -job-worker-count 2 -projectPath <isolated-project> -runTests -testPlatform EditMode -testResults <xml-path> -logFile <log-path>
```

**Boki → Run M3 Smoke Checks** opens the owned City scene to check direct-entry handling, enters Play Mode, uses a simulated mouse press/release on the saved Start New Run button, then exercises physical gates, both Forest choices, the exit, carried HP, and three City deaths/UI retries. It exits Play Mode and logs `M3_SMOKE_PASSED` or `M3_SMOKE_FAILED`. Save open scenes first; the runner refuses dirty scenes. Test teleports, damage, and the duplicate-session probe are temporary Play Mode changes.

For batch execution, use `-executeMethod Boki.Editor.M3SmokeChecks.Run` with the arguments above, omitting the test-run arguments and **omitting `-quit`**. The smoke runner exits the Editor after leaving Play Mode.

**Boki → Create M3 Route Arenas** is a creation utility. It copies the existing combat setup through the Editor into new scenes, builds route geometry, and preserves the prior active scene. It refuses to overwrite existing M3 scenes or run with dirty scenes; close `Combat_Test` before using it. It never saves the source combat scene. Do not use it to reset delivered scenes.

**Boki → Build M3 Linux Test Player** and **Boki → Build M3 Windows Test Player** use an explicit two-scene list without changing shared build settings. Choose an output directory outside tracked assets. For batch builds, set `BOKI_M3_BUILD_DIRECTORY`, then invoke `Boki.Editor.M3PlayerBuild.Linux` or `.Windows`. Install the matching support module first. Player transitions use SceneManager; Editor transitions use the Editor scene-loading API.

## Validation Results

Unity **6000.6.7f1**, Linux, isolated project copy, batch mode with graphics disabled:

- All 20 EditMode tests passed: 11 run/route cases and the existing nine health tests. Coverage includes the complete matrix, all eight unlock combinations over reachable histories, lock precedence, first-run forced choices, invalid/dead/out-of-order requests, immediate one-time selection, snapshot isolation, carried HP, and new-run resets.
- The Editor generated both route scenes and imported the new scripts/assets. Metadata is generated by Unity; existing GUIDs are preserved.
- M3 Play Mode smoke checks passed for invalid direct City entry, a real CharacterController blocked by the C barrier, real selection/exit trigger callbacks, both Forest choices, City gate restrictions and lock precedence, 73 HP carried into City, one session surviving scene loads, duplicate-session rejection, and three City death/UI-button retries, including rejection of a repeated request from the old coordinator.
- M2 Play Mode regression checks passed for targeting, occlusion, automatic kills, contact damage, death shutdown, and three standalone retries.
- M1 Play Mode regression checks passed for saved references, grounding, wall collision, camera obstruction, and 120 Play Mode updates.
- A Linux x86_64 development player built successfully with both scenes supplied explicitly. An eight-second headless startup logged no errors and ended by the expected timeout; this does not test rendered visuals, player input, or performance. Temporary output: `/tmp/boki-m3-player/Boki-M3.x86_64`.
- Asset checks found no missing metadata or duplicate GUIDs. Existing scene/prefab/metadata files are unchanged; documentation link targets and whitespace checks passed.

## Cursor and UI Fix

The camera previously recaptured the cursor on any left press, including UI clicks. It now performs a UI raycast at the current mouse position before recapturing, avoiding dependence on whether EventSystem has already updated that frame. Press Escape before interacting with live-play UI; a click outside UI still resumes camera control.

The M3 smoke check now injects a mouse press/release through InputSystemUIInputModule and checks the actual saved button binding, rather than calling StartNewRun directly. Its temporary headless input-focus settings are restored after the probe; project settings are not saved. The updated smoke check passed in Unity 6000.6.7f1 on Linux, including a real UI mouse press/release that loads Forest. The temporary Linux player listed above predates this cursor fix; rebuild it before checking the updated player interaction.

## Remaining Acceptance

The developer accepted the slice after interactive testing and the cursor/UI fix. Individual visual, focus, and frame-rate checks were not separately reported.

For further integration, manually check the scene visuals, sign readability at 1280×720, gate closure while riding/jumping/boosting, camera obstruction, combat placement, cursor/focus changes, and repeated interactive runs. The batch checks establish state and physics integration; they do not establish input feel or visual quality.

Windows Editor/player behavior and Ryzen 5600G performance remain unverified. Run the Windows player with both scenes at 1280×720 Low on the target machine and record FPS, gate interaction, scene loading, HP carry, and Retry results. Linux results do not establish Windows compatibility or M7 performance acceptance.
