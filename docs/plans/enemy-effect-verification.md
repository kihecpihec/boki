# Enemy effect verification

Implements initial enemy hit/death presentation from [Gameplay §5](../Gameplay.md#battery-charges-and-action-feedback), with the readability and performance limits in [Story §6](../Story.md#6-visual-and-audio-direction). Enemy model and effect demonstrations belong in `Enemy_Test`; the scene upgrade changes only this authorized scene, leaving shared prefabs, maps, project settings and packages intact.

## Enemy_Test scene integration

**Boki → Enemy Models → Upgrade Test Models and Effects** opens and saves the existing `Assets/Scenes/Enemy_Test.unity`, refuses Play Mode and dirty scenes, and restores the previous scene setup. Repeating the command reuses the visual hierarchy and feedback components. It refuses a missing scene rather than creating a replacement.

- Boki's placeholder body, deck and forward marker are disabled by scene overrides. `Visuals` copies the imported character and scooter from `Boki_Rigged_Test`, including the atlas, Generic Animator and `Boki_RideIdle` pose. Existing player health, controller, collision, camera, HUD and combat references remain on their original objects. `AutoLaser.Beam Origin` points at `Visuals`' scooter `Socket_LaserMuzzle`.
- All three melee variants, both Watchers and the professor have an explicit enabled `EnemyFeedback` alongside `EnemyHealth`. Their existing animation, hover, eye, orb, paper and slam-ring assets remain connected. Death particles are emitted on demand; no continuously running particle systems are needed.
- Guilt retains its warning/chase animation and visibility lifecycle, with no damageable health or hit/death feedback. This upgrade adds no new audio or light-flicker system.
- No manual Inspector assignments are required. Boki's dedicated movement/effect demonstrations remain in `Player_Model_Test`, which already uses the same imported rigged model.

## Behavior and setup

- `EnemyHealth.Show Combat Feedback` defaults to enabled and adds `EnemyFeedback` at runtime if no component exists. No Inspector assignments are required. Only accepted positive damage flashes; death produces one burst and the target still leaves the target registry immediately.
- For custom tuning, the asset owner can add `EnemyFeedback` to an enemy root alongside `EnemyHealth`, then adjust `Hit Color` and `Flash Seconds`. Renderers are cached from the hierarchy in Awake. The default flash lasts 0.1 s, preserves material alpha and restores the previous property blocks on expiry, disable or death. Supported mesh materials expose `_BaseColor` or `_Color`.
- To opt out, the owner disables `Show Combat Feedback` on `EnemyHealth` and removes/disables any explicitly authored `EnemyFeedback`. No such asset edits are included here.
- Death emits ten small amber particles for 0.35 s. Six bursts can exist simultaneously; excess bursts are skipped. No colliders, lights, shadows, camera shake, audio voices or persistent scene objects are added. The burst belongs to the dying enemy's scene and survives its root destruction, including when `MeleeView` detaches the death animation.
- Flash and detached-particle time stop when scaled time stops or the existing run reports gameplay inactive. Guilt has no `EnemyHealth`, so these effects do not imply it can be damaged.
- `Assets/Resources/EnemyFeedbackParticles.shader` is loaded by resource name to preserve shader inclusion in builds. Bursts release their generated materials on destruction.

## Automated verification

Use Unity `6000.6.7f1`.

1. Run the architecture audit and EditMode suite.
2. Run **Boki → Enemy Models → Run Feedback Checks** outside Play Mode with clean open scenes. The helper uses a temporary empty scene and restores the previous saved scene setup; it never saves maps or prefabs. Batch invocation: `-batchmode -nographics -projectPath Game -executeMethod Boki.Editor.EnemyFeedbackChecks.Run -logFile /tmp/boki-enemy-feedback-checks.log` (omit `-quit`).
3. Require `ENEMY_FEEDBACK_PASSED` and no new code warnings/errors. Checks cover automatic attachment, rejected damage, accepted hit flash, shared-material/property-block preservation, shared pause, restoration on disable, repeated lethal hits, simultaneous burst cap, resource shader resolution, detached-particle pause and cleanup after resuming.

## Manual verification still required

Open `Enemy_Test` and enter Play Mode. Confirm the rigged Boki sits on the scooter and the laser fires from its muzzle. Select a melee enemy or Watcher and let the existing auto-laser hit it. Confirm the short warm flash is visible in the level lighting, fades smoothly, and leaves no changed material after stopping Play Mode. Kill both types and confirm a small burst at their aim position; the melee death animation should finish independently. Check the professor too: its ground warnings must remain more prominent than these effects. Verify Guilt's warning/chase cycle, pause/resume mid-flash and mid-burst, rapid kills, scene transition during a burst, and no new Console messages. There is no sound layer in this first effect pass.

Graphical review on Low and a Windows x86_64 player run on the Ryzen 5600G at 1280×720 remain required. Headless Linux checks do not establish visual quality, shader rendering, Windows compatibility or target frame rate.

## Recorded scene integration checks (2026-10-24)

- Unity `6000.6.7f1` imported and compiled the checkout and saved the upgrade to `Enemy_Test`. Running the upgrade twice produced the same scene SHA-256 before whitespace cleanup; no duplicate visuals or feedback components were added.
- `EnemySmokeChecks.Run` passed the rigged-model, disabled-placeholder, Animator, muzzle and feedback wiring checks, then exercised melee contact, Watcher shots, professor barrage/telegraph/slam, and Guilt placement/lifecycle/strikes (`/tmp/boki-enemy-scene-smoke.log`, `ENEMY_SMOKE_PASSED`).
- `EnemyFeedbackChecks.Run` passed accepted/rejected hit, property-block restoration, burst cap, pause and cleanup checks (`/tmp/boki-enemy-scene-feedback.log`, `ENEMY_FEEDBACK_PASSED`).
- Architecture audit: no failures; existing warnings remain. `git diff --check` passes after removing whitespace introduced by Unity serialization. Only `Enemy_Test` is changed among scenes; no prefab, model, package or project-settings changes.
- Deterministic rules were unchanged, so the EditMode suite was not rerun for this scene integration. Graphical Play Mode review and Windows/5600G checks remain pending; no additional import or Inspector wiring is needed.

## Recorded initial feedback checks (2026-10-24)

- Pinned Linux Editor imported all new assets and generated their `.meta` files.
- EditMode: 117/117 passed, none skipped (`/tmp/boki-enemy-effect-editmode.xml`).
- Isolated Play Mode feedback checks passed after fixing particle initialization to start, emit and then pause before manual simulation (`/tmp/boki-enemy-feedback-checks.log`).
- Architecture audit: no failures; asset pairing and GUID uniqueness pass. It warns about `Resources.Load`; this single shader resource keeps the default runtime effect available without scene/prefab wiring or changes to shared graphics settings. Existing build-settings and milestone-helper warnings remain outside this task.
- `git diff --check` passes. No scene, prefab, map, package or project-settings changes.
- Unity reports an Editor Search startup `ArgumentOutOfRangeException` in `UnityEditor.Search.SearchDatabase.EnumerateAll`, independent of the effect check. Existing project compiler deprecation warnings also remain; no fixes to that unrelated code are included.
