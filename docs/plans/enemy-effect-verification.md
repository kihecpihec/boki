# Enemy effect verification

Implements initial enemy hit/death presentation from [Gameplay §5](../Gameplay.md#battery-charges-and-action-feedback), with the readability and performance limits in [Story §6](../Story.md#6-visual-and-audio-direction). Enemy model and effect demonstrations belong in `Enemy_Test`; the scene upgrade changes only this authorized scene, leaving shared prefabs, maps, project settings and packages intact.

## Enemy_Test scene integration

**Boki → Enemy Models → Upgrade Test Models and Effects** opens and saves the existing `Assets/Scenes/Enemy_Test.unity`, refuses Play Mode and dirty scenes, and restores the previous scene setup. Repeating the command reuses the visual hierarchy and feedback components. It refuses a missing scene rather than creating a replacement.

- Boki's placeholder body, deck and forward marker are disabled by scene overrides. `Visuals` copies the imported character and scooter from `Boki_Rigged_Test`, including the atlas, Generic Animator and `Boki_RideIdle` pose. Existing player health, controller, collision, camera, HUD and combat references remain on their original objects. `AutoLaser.Beam Origin` points at `Visuals`' scooter `Socket_LaserMuzzle`.
- All three melee variants, both Watchers and the professor have an explicit enabled `EnemyFeedback` alongside `EnemyHealth`. Their existing animation, hover, eye, orb and paper assets remain connected. The professor uses `ProfessorAttackView` for casting recoil, slam windup and recovery, an exact-radius red boundary, a shrinking amber countdown, and an expanding impact ring. Death particles are emitted on demand; no continuously running particle systems are needed.
- Guilt retains its warning/chase animation and visibility lifecycle, with no damageable health or hit/death feedback. This upgrade adds no new audio or light-flicker system.
- No manual Inspector assignments are required. Boki's dedicated movement/effect demonstrations remain in `Player_Model_Test`, which already uses the same imported rigged model.

## Attack presentation

- The same upgrade command configures scene-local paper/orb templates beneath the inactive `Enemy Attack Templates` root. The professor's `Sheet Prefab` and both Watchers' `Projectile Prefab` reference those templates. Shared prefabs remain untouched. `Launch` activates each spawned copy and aims its forward axis along the shot.
- `M_EnemyAttackFX` uses the existing vertex-color particle shader for transparent lines and trails. Papers have amber trails, Watcher orbs red trails, and impacts use the existing bounded burst system. No lights or additional packages are required.
- `ProfessorAttackView.Visual` points to the professor's `Visual`; `Warning`, `Countdown`, `Shockwave`, and `Cast Pulse` point to the four named line children. The old `SlamRing` marker remains assigned to `ProfessorBoss`, with its model renderers disabled by scene overrides. The red line stays at `Slam Radius`; the shrinking amber line counts down the canonical warning time. The expanding ring after impact is decorative.
- Professor posing, warnings and projectiles stop advancing while the run is inactive. Death, target death and disabling the professor clear its presentation and restore its visual transform. Attack counts, damage, route modifiers and warning duration retain their canonical values.
- Projectile hit detection checks the travelled segment against Boki's existing hit radius so a shot cannot skip across the target between frames. Walls still block projectiles. No additional Inspector assignments are needed in `Enemy_Test`.

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

Open `Enemy_Test` and enter Play Mode. Confirm the rigged Boki sits on the scooter and the laser fires from its muzzle. Select a melee enemy or Watcher and let the existing auto-laser hit it. Confirm the short warm flash is visible in the level lighting, fades smoothly, and leaves no changed material after stopping Play Mode. Kill both types and confirm a small burst at their aim position; the melee death animation should finish independently. Check the professor too: three amber paper sheets should launch with recoil and trails, followed by a red boundary and shrinking countdown at Boki's previous position. Boost out before impact; verify the warning stays in place and the shockwave dissipates. Repeat while standing inside and jumping over the slam. Its ground warnings must remain more prominent than hit/death bursts. Pause during the countdown, then kill or disable the boss during a warning and verify all lines disappear. Verify Guilt's warning/chase cycle, pause/resume mid-flash and mid-burst, rapid kills, scene transition during a burst, and no new Console messages. There is no sound layer in this first effect pass.

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

## Recorded attack presentation checks (2026-10-24)

- Pinned Unity `6000.6.7f1` imported the new view and effect material, generated their `.meta` files, and saved `Enemy_Test`. Repeating the upgrade produced the same scene SHA-256 before whitespace cleanup; templates, view and line objects were reused.
- EditMode suite: 117/117 passed, none skipped (`/tmp/boki-attack-editmode.xml`).
- Extended `EnemySmokeChecks.Run` passed (`/tmp/boki-attack-smoke-final.log`, `ENEMY_SMOKE_PASSED`): melee/Watcher attacks, three professor sheets, telegraph and impact, exact damage-radius boundary, projectile trails, effect cleanup on disable, a projectile crossing the player between frames, and the existing Guilt placement/lifecycle checks.
- Architecture audit has no failures; existing warnings remain. `git diff --check` passes. No shared prefabs, other scenes, packages or project settings were changed.
- These were headless Linux checks. Interactive visual review, pause/death during warnings, boost/jump avoidance, and Windows/5600G performance remain manual checks; they are not established by the smoke run.

## V1 scene integration

**Boki → Version 1 → Upgrade Enemy Effects** upgrades only the three existing V1 scenes, refuses Play Mode and dirty scenes, and restores the previous scene setup. It does not rebuild maps or encounters or edit shared prefabs. All scenes are checked for existence before editing begins.

- Pohorje and Maribor now reference scene-local Watcher orb templates with the existing red trails and bounded impact bursts. FERI retains its amber exam papers and professor presentation. Hit/death feedback already attaches through `EnemyHealth`; Guilt remains undamageable.
- The shared attack upgrade accepts scenes without a professor and reuses nested template roots, including FERI's existing root under `V1 Gameplay`. The V1 scene builder also applies attack effects to every level on future builds.
- The upgrade validates every ranged template's scene-local reference, inactive template state, visual type, trail material and width; it checks hit/death feedback configuration and professor presentation references. These checks also run with the V1 integration checks.
- No Inspector assignments are needed. For manual review, open each `V1_*` scene, select a route, let a ranged enemy fire and confirm the projectile trail and impact burst. Hit and kill regular enemies, pause during an effect, and verify it resumes correctly. In FERI, check the professor's barrage, warning countdown and slam; Guilt should retain its existing warning/chase presentation. Require no new Console errors. Windows/5600G visual and performance verification remains required.

### Recorded V1 checks (2026-10-24)

- Unity `6000.6.7f1` compiled, upgraded and validated all three V1 scenes (`/tmp/boki-v1-enemy-effects.log`). Repeating the command validated them again and produced identical scene SHA-256 hashes (`/tmp/boki-v1-enemy-effects-repeat.log`).
- `EnemyFeedbackChecks.Run` passed (`/tmp/boki-v1-enemy-feedback.log`, `ENEMY_FEEDBACK_PASSED`). Unity also reported the previously recorded Editor Search startup `ArgumentOutOfRangeException`; its stack is in `UnityEditor.Search.SearchDatabase.EnumerateAll`.
- Removed unrelated Unity object ordering, prefab override ordering, whitespace and HUD font serialization changes. FERI has no remaining scene diff; only the two missing scene integrations remain. Existing object blocks outside projectile overrides and scene roots are preserved. Scene object IDs are unique.
- Architecture audit: no failures, existing warnings remain. `git diff --check` passes. No new assets or missing `.meta` files; no shared prefab, package or project-setting changes. Deterministic rules were unchanged, so EditMode rule tests were not rerun. Interactive Play Mode review and Windows/5600G verification remain pending.
