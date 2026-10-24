# Architecture Review — 2026-10-23

> **Superseded in part (cleanup 2026-10-24):** this is a point-in-time snapshot of commit `43e1840`. Since then `RangedEnemy`, `EnemyProjectile`, `GuiltChaser`, `ProfessorBoss`, Guilt rules and tests, and the scooter-light battery rules landed on `dev`, so DOC-1, QUA-2, TEST-1 and test-coverage gap 1 need re-verifying before acting on them. `Pohorje_Map_Test`, `Maribor_Map_Test` and their builders were removed; PERF-2 applied to the retired Pohorje v2 map and no longer applies.

**Scope:** all of `Game/Assets` (runtime scripts, Rules asmdefs, EditMode tests, Editor tools, scenes/prefabs, URP and quality settings) plus `AGENTS.md`, `docs/Architecture.md`, and `docs/plans/M1–M6`. `TutorialInfo` and binary art were not reviewed.
**Commit:** `43e1840` on `t3/unity-architecture-review` (same tree as `dev`; clean working copy)
**Method:** static review only; no Unity Editor, Play Mode, profiler, or Windows build was run. The `boki-unity-architecture` `audit.sh` was run (no FAILs, 3 WARNs).
**Milestone context:** `Architecture.md` §9 makes revised M6 (winnable loop with battery, Guilt, XP/upgrades, shared pause, professor) the current target. `docs/plans/M6-verification.md` records an original M6 implementation on `feat/m6-full-run`.

## Summary

The code that exists here is in good shape: rules sit in engine-free assemblies with behavior tests, adapters are thin, sealed, and validate their references, and route lock priority and next-run unlock timing are correct in code and tested. The main risk is not code quality but **integration state**: the branch everyone builds on does not contain M6. Every M6 file listed in the handoff (`M6State.cs`, `M6Gameplay.cs`, `RangedEnemy`, `GuiltChaser`, `ProfessorBoss`, and others) exists only on `origin/feat/m6-full-run`, which has not been merged, yet `Architecture.md` §3 and the M6 handoff describe it as delivered. As a result this tree cannot be won, and docs overstate progress. The best next step is to decide how to merge `feat/m6-full-run` (it diverges by 43 script files) before more feature work lands on `dev`.

## Scorecard

| Area | Rating | One-line reason |
|---|---|---|
| Architecture & boundaries | Good | Clean Rules/adapter split, one sanctioned global, events paired; two hidden couplings. |
| Code quality | Good | Readable, sealed, validated; a few magic numbers and speculative `RunState` fields. |
| Performance (predicted) | Fair | Runtime code is allocation-free; render settings exceed the §7 Low target and one test scene has 246 mesh colliders. |
| Testing | Fair | 40 focused EditMode tests on the right rules; no tests for M6 rules in this tree; Editor tooling duplicated. |
| Unity asset hygiene | Good | Metas paired, GUIDs unique, Force Text, build list correct. |
| Docs vs code alignment | Poor | Architecture §3 and M6 handoff describe code absent from this branch. |

## What is working well

- `Boki.Combat.Rules`, `Boki.Player.Rules`, `Boki.Run.Rules` all set `noEngineReferences: true`; test asmdefs are Editor-only and reference only Rules. Keep this.
- `RunState.Gate` (`Game/Assets/Scripts/Routes/Rules/RunState.cs:53-62`) returns `RouteLock` before checking `PersistentLock`, matching `AGENTS.md`; `UnlockSnapshot` is a value type so earned flags cannot touch the current run (`RunState.cs:11-19`, `RunSession.cs:94-100`).
- `PersistentUnlocks.Earn` is idempotent and saves immediately (`PersistentUnlocks.cs:24-31`); `IUnlockStore`/`PlayerPrefsUnlockStore` is the right seam.
- Statics have `SubsystemRegistration` resets (`EnemyHealth.cs:33-34`, `RunSession.cs:29-30`). Every `+=` found has a matching `-=`.
- Adapters validate in `OnEnable`, log with context, and disable themselves (`AutoLaser.cs:25-30`, `RouteGate.cs:19-24`).
- Input actions are enabled and disabled by their owner (`ScooterController.cs:81-95`, `ThirdPersonCamera.cs:45-57`). No legacy `Input.*`.
- `ScooterMotion` substeps at 1/240 s, so movement is frame-rate independent and fully testable.
- Production scenes have zero mesh colliders.

## Findings

### DOC-1 M6 is documented as delivered but is not in this branch — High · Confirmed
- **Where:** `docs/Architecture.md` §3 (last paragraph: "M6 adds regular kill/battery rewards, shared ranged projectiles, …"); `docs/plans/M6-verification.md` "Files and Editor Setup". Code: `git branch -a --contains fabb526` → only `remotes/origin/feat/m6-full-run`.
- **What:** None of `M6State.cs`, `M6Gameplay.cs`, `RangedEnemy.cs`, `EnemyProjectile.cs`, `GuiltChaser.cs`, `PowerPanel.cs`, `BatteryPickup.cs`, `FloorHazard.cs`, `ProfessorBoss.cs`, `M6StateTests.cs`, or `M6SceneBuilder/SmokeChecks/PlayerBuild.cs` exist here. `ClassroomArrival.cs:7,21` still shows the M5 placeholder message. The handoff reports 47 tests; this tree has 40.
- **Why it matters:** the slice cannot be won on `dev`. Anyone reading Architecture §3 will assume M6 systems exist and build on them. Meanwhile `dev` gained movement and asset work (`git diff --stat HEAD origin/feat/m6-full-run -- Game/Assets/Scripts`: 43 files differ), so the merge cost grows with every PR.
- **Suggested fix:** decide whether to merge `feat/m6-full-run` into `dev` now (expect conflicts in `ScooterController`, the three production scenes, and Editor scripts) or to port it. Until then, add one line to Architecture §3 stating M6 lives on `feat/m6-full-run`, unmerged. Merging touches the three production scenes owned by Ismar M; coordinate.
- **Effort:** M–L (merge), S (doc note)

### ARC-1 Persistence switches on a scene-path string comparison — Medium · Confirmed
- **Where:** `Game/Assets/Scripts/Core/RunSession.cs:57` `UsesPersistentUnlocks => forestDestination == PohorjeScene`
- **What:** whether unlocks are saved depends on the serialized entry scene path equalling a hard-coded constant. Renaming or moving `Level1_Pohorje.unity`, or using the new `Pohorje_Map_Test` as the entry, silently disables saving with no error.
- **Why it matters:** permanent C discoveries are a core release requirement (Architecture §9 M5/M7). This would fail quietly in a Windows build.
- **Suggested fix:** keep it, but log a warning in `Awake` when a scene in the build list is used as entry and persistence is off, or replace with an explicit serialized mode once the M3 test scenes are retired. Comment at line 55 already explains the reason; the risk is just silent failure.
- **Effort:** S

### ARC-2 `RunSession.Ensure()` called from a property getter and from triggers that assume `Instance` — Low · Confirmed
- **Where:** `PlayerHealth.cs:26,36-37`, `RouteManager.cs:21`, `ClassroomArrival.cs:18-19`
- **What:** `PlayerHealth.State` lazily creates a session in a getter; `OnEnable` uses `RunSession.Instance` right after `Ensure()` (safe). `ClassroomArrival` dereferences `RunSession.Instance` directly; it is guarded indirectly by `manager.IsReady` (which requires a session), so it is safe today but fragile if the guard order changes.
- **Why it matters:** low; noted so the M6 merge does not copy the pattern into more scripts.
- **Suggested fix:** in `ClassroomArrival`, go through `manager` (add a `RouteManager.Session` getter) instead of the static.
- **Effort:** S

### PERF-1 PC quality settings exceed the Architecture §7 "Low" target — Medium · Confirmed
- **Where:** `Game/Assets/Settings/PC_RPAsset.asset:28,29,46,57,66` (`m_MSAA: 1`, `m_RenderScale: 1`, `m_MainLightShadowmapResolution: 2048`, `m_ShadowDistance: 50`, `m_SoftShadowsSupported: 1`, `m_SupportsHDR: 1`); `QualitySettings.asset:7` current quality = PC.
- **What:** the active tier is the template's PC preset. There is no Low tier configured for 1280×720.
- **Why it matters:** on the 5600G's integrated Vega 7, soft 2048 shadows and HDR at full render scale are the most likely FPS risk. Prediction, not measured.
- **Suggested fix:** propose (shared file; needs agreement) a Low preset: render scale 0.8, shadow res 1024, shadow distance ~30, soft shadows off, HDR off unless post needs it. Measure before/after on Windows.
- **Effort:** S (needs Editor)

### PERF-2 246 non-convex mesh colliders in `Pohorje_Map_Test` — Medium · Confirmed
- **Where:** `Game/Assets/Editor/PohorjeMapTestBuilder.cs:46-48`; `Game/Assets/Scenes/Pohorje_Map_Test.unity` (246 `MeshCollider:` blocks)
- **What:** every MeshFilter in eight imported FBX groups (including `PL_C_Removable_Roofs` and building/route chunks) gets a mesh collider. The comment justifies it for walkable surfaces only.
- **Why it matters:** `AGENTS.md` asks for primitives unless the shape needs a mesh. Static mesh colliders are cheap-ish, but `CharacterController` sweeps, the camera `SphereCast`, laser and Linecast occlusion all hit them every frame. If this map replaces `Level1_Pohorje`, it becomes the production cost.
- **Suggested fix:** keep mesh colliders on terrain/path slabs only; give buildings, roofs, and props box colliders. Profile physics time first.
- **Effort:** M

### QUA-1 Hard-coded layer mask and gravity in `MeleeEnemy` — Low · Confirmed
- **Where:** `Game/Assets/Scripts/Enemies/MeleeEnemy.cs:46` (`25f` gravity), `:58` (`Physics.Linecast(..., 1, ...)`)
- **What:** the occlusion mask is the literal Default layer; `AutoLaser` exposes the same concept as `obstructionMask`.
- **Why it matters:** any wall on a non-Default layer lets melee damage pass through it.
- **Suggested fix:** serialize `obstructionMask` (default 1) and `gravity`, matching `AutoLaser`/`ScooterController`.
- **Effort:** S

### QUA-2 Speculative mutable fields on `RunState` — Low · Confirmed
- **Where:** `Game/Assets/Scripts/Routes/Rules/RunState.cs:25-28` (`Battery`, `XP`, `Upgrades` as `List<string>`, `KillCount`, all public setters)
- **What:** unused in this tree; any caller can set them without raising `Changed`. The skill says new rule areas get their own Rules type, composed by `RunState`.
- **Why it matters:** the M6 branch already replaced battery with fractional state; these will conflict or be bypassed.
- **Suggested fix:** resolve during the M6 merge; do not build on these fields.
- **Effort:** S

### TEST-1 Milestone Editor tooling is cloned per milestone — Low · Confirmed (audit WARN)
- **Where:** `Game/Assets/Editor/M2–M5PlayerBuild.cs` (identical except name/menu), `M1–M5SceneBuilder.cs`, `M1–M5SmokeChecks.cs` (~2,000 lines); `feat/m6-full-run` adds a sixth set.
- **Why it matters:** the M6 merge will add more clones; old scene builders can overwrite production scenes if run (M6 handoff warns against it).
- **Suggested fix:** one `PlayerBuild` helper taking scene list + output name; delete or menu-hide M1–M4 scene builders once production scenes no longer depend on them.
- **Effort:** M

### TEST-2 No automated test for `RunSession` orchestration — Low · Likely
- **Where:** `Game/Assets/Scripts/Core/RunSession.cs:60-110`
- **What:** `Load` decides save-before-advance order and the `IsLoading` guard (the "LevelExit cannot fire twice" rule in Architecture §10). Covered only by Editor smoke checks, which run in a separate validation project.
- **Suggested fix:** a small PlayMode test that calls `Advance` twice in one frame and asserts one load and one save, using a fake `IUnlockStore`.
- **Effort:** S–M

## Docs and code conflicts

| Document and section | What it says | What the code does | Which looks wrong |
|---|---|---|---|
| Architecture.md §3 (last paragraph) | M6 adds battery, ranged enemies, Guilt, professor, victory | None exist on this branch | Docs, or the branch is missing a merge (DOC-1) |
| M6-verification.md "Validation" | 47 EditMode tests pass | 40 `[Test]` methods here | Same cause |
| Architecture.md §3 table, `EnemyHealth` | "HP, death, XP drop and kill count" | HP and death only; comment at `EnemyHealth.cs:41` says "M2 intentionally grants none" | Expected; revised M6 not built. Flag only. |
| Architecture.md §7.5 | 1280×720 Low, render scale ≈0.75–0.9 | Active PC tier at 1.0 with soft 2048 shadows | Settings (PERF-1) |

## Test coverage gaps

1. All revised-M6 rules (battery drain/charges, Guilt pressure timing, XP thresholds, upgrade eligibility, pause): no code here, no tests. Required by Architecture §9 M6.
2. `RunSession` double-advance and save-then-load ordering (TEST-2).
3. `HealthState` boundary of exact-lethal damage is covered; no test for zero/negative damage being ignored (`HealthState.cs:22`). Minor.

Covered well: lock priority, next-run snapshot timing, idempotent saving, reachable route histories, motion/boost/bunny-hop behavior.

## Performance checks to run on the Ryzen 5600G

1. Windows x86_64 player, 1280×720, current PC tier vs proposed Low: `Level2_Maribor` with 12 active enemies. Measure CPU main thread and GPU frame time; target ≤33 ms.
2. Same scene: Profiler → Physics and `Rendering.Shadows` share. If shadows are >25% of GPU time, apply PERF-1.
3. `Pohorje_Map_Test` traversal at full speed: `Physics.Processing` and `CharacterController.Move` time; compare against `Level1_Pohorje` (PERF-2).
4. FERI boss (after the M6 merge): GC Alloc per frame should be 0 B outside spawns.

## Recommended next steps

Do before integration:
1. Decide and schedule the `feat/m6-full-run` merge with the scene owner; add the "unmerged" note to Architecture §3 now (DOC-1).
2. Make persistence failure loud (ARC-1).
3. During the merge, retire the speculative `RunState` fields (QUA-2) and avoid adding a sixth cloned build script (TEST-1).
4. Agree and apply a Low quality tier, then measure on Windows (PERF-1).

Do later:
5. Replace non-walkable mesh colliders in the Pohorje map (PERF-2).
6. Serialize `MeleeEnemy` mask and gravity (QUA-1); route `ClassroomArrival` through `RouteManager` (ARC-2).
7. Add the `RunSession` double-advance test (TEST-2).

## Not reviewed

- Runtime behavior, FPS, and Windows builds (no Editor available).
- The contents of `origin/feat/m6-full-run` beyond file presence and diff size.
- Binary assets (FBX, textures) and `TutorialInfo`.
- Smoke-check correctness; only their structure and duplication were reviewed.
