# Enemy effect verification

Implements initial enemy hit/death presentation from [Gameplay §5](../Gameplay.md#battery-charges-and-action-feedback), with the readability and performance limits in [Story §6](../Story.md#6-visual-and-audio-direction). No maps, scenes, prefabs, model imports, project settings or packages are edited.

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

Use the existing `Enemy_Test` scene read-only in Play Mode, coordinated with its owner; no setup changes or saves are needed. Select a melee enemy or Watcher and let the existing auto-laser hit it. Confirm the short warm flash is visible in the level lighting, fades smoothly, and leaves no changed material after stopping Play Mode. Kill both types and confirm a small burst at their aim position; the melee death animation should finish independently. Check the professor too: its ground warnings must remain more prominent than these effects. Verify pause/resume mid-flash and mid-burst, rapid kills, scene transition during a burst, and no new Console messages. There is no sound layer in this first effect pass.

Graphical review on Low and a Windows x86_64 player run on the Ryzen 5600G at 1280×720 remain required. Headless Linux checks do not establish visual quality, shader rendering, Windows compatibility or target frame rate.

## Recorded checks (2026-10-24)

- Pinned Linux Editor imported all new assets and generated their `.meta` files.
- EditMode: 117/117 passed, none skipped (`/tmp/boki-enemy-effect-editmode.xml`).
- Isolated Play Mode feedback checks passed after fixing particle initialization to start, emit and then pause before manual simulation (`/tmp/boki-enemy-feedback-checks.log`).
- Architecture audit: no failures; asset pairing and GUID uniqueness pass. It warns about `Resources.Load`; this single shader resource keeps the default runtime effect available without scene/prefab wiring or changes to shared graphics settings. Existing build-settings and milestone-helper warnings remain outside this task.
- `git diff --check` passes. No scene, prefab, map, package or project-settings changes.
- Unity reports an Editor Search startup `ArgumentOutOfRangeException` in `UnityEditor.Search.SearchDatabase.EnumerateAll`, independent of the effect check. Existing project compiler deprecation warnings also remain; no fixes to that unrelated code are included.
