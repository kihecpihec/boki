---
name: software-architect
description: Review the Boki Unity project as a software architect and write an evidence-based report on architecture, code quality, performance, testing, Unity asset hygiene, and drift from the design docs. Use when the user asks for an architecture review, a code audit, or a health check of the Unity project. Read-only for game code; writes one report under docs/reviews/.
---

# Software Architect Review

Act as a pragmatic software architect for a two-person game jam. Review `Game/` against what the project says it wants, then write one report. Findings must be specific, verifiable, and ranked by impact on the core priorities in `AGENTS.md`: a playable vertical slice, 30+ FPS on a Ryzen 5600G, and protecting the other developer's scenes.

Do not apply fixes, refactor, commit, or push. The only file you create is the report.

This skill works in any coding agent (Claude Code, Codex, and others). It names no agent-specific tools: use whatever file-reading, search, and shell tools you have. The user may pass an optional focus, such as `architecture`, `quality`, `performance`, `tests`, `assets`, or a path.

## Ground rules

- **Evidence over opinion.** Every finding cites `path:line` (or an asset path and the YAML field) and says what actually happens. If you did not read it, do not claim it. Mark anything inferred as "Likely" and say what would confirm it.
- **Judge against this project's goals, not textbook purity.** `Architecture.md` §1 asks for simple MonoBehaviours, no framework, no new packages. Do not recommend DI containers, ECS, event buses, or layers of abstraction unless a concrete problem in the code needs one. Simpler beats more architecture here.
- **Respect documented design.** Game rules live in `docs/Gameplay.md`, `docs/Movement.md`, `docs/Story.md`. If code and docs disagree, report the conflict and which side looks wrong. Do not decide design questions.
- **Calibrate to milestone.** `Architecture.md` §9 and `docs/plans/` say what is in scope now. Flag missing features only if the current milestone claims them; list the rest as "not yet built" at most.
- **Unity cannot run during the review.** Use static analysis only. State plainly that no profiling, Play Mode, or Windows build was done, and list performance claims as predictions with a suggested profiler check.
- **Keep it proportionate.** A 1,000-line codebase does not need a 20-page report. Prefer a short list of findings that matter over exhaustive nitpicks. Say what is good too, briefly, so the next review knows what to preserve.

## Process

Scope: if an argument names a focus or path, review only that, but still read enough context to judge it. Otherwise review everything below.

### 1. Orient (read, don't skim)

- `AGENTS.md`, `README.md`, and `docs/Architecture.md` (§1–7 at minimum). Skim `docs/Gameplay.md` §2–4 and §8 for route rules and the performance target.
- `docs/plans/` for the latest milestone and its `*-verification.md`, to know what is claimed done.
- `git log --oneline -20` and `git status` to see recent direction and any uncommitted work.
- `Game/ProjectSettings/ProjectVersion.txt` and `Game/Packages/manifest.json` for the Editor version and packages. Note unused or unagreed packages.
- Previous reports in `docs/reviews/`, if any. Check which earlier findings were fixed or ignored and say so in the report.

### 2. Inventory

List every `.cs` under `Game/Assets`, every `.asmdef`, and every scene and prefab. Get sizes with `wc -l`. Build a picture of: assemblies and their references, which scripts are pure C# rules versus MonoBehaviours, and what the Editor-only scripts do.

### 3. Review lenses

Read the code for each lens. Search for patterns, then read the hits in context so you do not report false positives.

**Architecture and boundaries**
- Are gameplay rules (route locks, unlock timing, health) separated from MonoBehaviours and scene wiring, and testable without Unity? Check the `*.Rules` assemblies stay free of `UnityEngine` dependencies where they should.
- Dependency direction between `Core`, `Player`, `Combat`, `Enemies`, `Routes`, `UI`. Look for cycles, UI reaching into rules, or rules reaching into scenes.
- Compare actual modules against `Architecture.md` §3–6. Note drift in either direction.
- Global state: static fields, singletons, `DontDestroyOnLoad`. Can a scene reload create a duplicate `RunSession`, duplicate listeners, or a duplicate HUD (`Architecture.md` §10)? Are events unsubscribed in `OnDisable`/`OnDestroy`?
- Persistence: `PlayerPrefs` keys, defaults, versioning, and whether unlocks can leak into the current run.
- Hidden coupling: `FindObjectOfType`/`FindFirstObjectByType`, `GameObject.Find`, tag or name string lookups, `SendMessage`, serialized references that must be wired by hand in scenes.

**Gameplay-rule correctness traps** (from `AGENTS.md` and `Gameplay.md`)
- C unlocks apply on the next run, never the current run.
- `ROUTE_LOCK` takes priority over `PERSISTENT_LOCK`.
- `RouteGate` and `LevelExit` cannot fire twice. `New Run` restores the documented starting state and keeps permanent unlocks.
- Every FERI route reaches the same boss arena.
Check these in code, not only in tests.

**Code quality**
- Readability, naming (English identifiers; preserved terms such as `boost`, `Izgubljeni meščan`, `Podzemni prehod`, `POWER OFF` are not renamed), dead code, duplicated logic, magic numbers that belong in serialized fields or in the values from `Story.md` §4 / `Movement.md`.
- Null-safety on serialized references, missing `[RequireComponent]` / `[SerializeField]` validation, silent failure paths, and `Debug.Log` left in hot paths.
- Public mutable fields where a property or `private` + `[SerializeField]` fits.
- Time handling: `Time.deltaTime` vs `fixedDeltaTime`, frame-rate-dependent movement or damage, `CharacterController.Move` called more than once per frame.
- Unnecessary abstraction or over-engineering, as well as under-engineering that is already causing duplicate code.

**Performance (Ryzen 5600G, Windows, 1280×720, Low, target ≥30 FPS)**
- Per-frame cost in `Update`/`FixedUpdate`/`LateUpdate`: `GetComponent`, `Find*`, `Camera.main` on older Unity versions, LINQ, string building, `new` allocations, `foreach` over non-array collections that allocate, closures, `Physics.OverlapSphere` (allocating) instead of `*NonAlloc`, repeated `Physics.Raycast` per enemy, `SendMessage`.
- Targeting scans: does `AutoLaser` or enemy code iterate all enemies every frame? Does it scale to the cap of 8 / 12 / 8 active enemies (`Architecture.md` §7)?
- Instantiate/Destroy churn for enemies, projectiles, VFX. Is pooling needed at the planned counts, or is it premature?
- Physics: mesh colliders where primitives suffice (`AGENTS.md` forbids them without need), non-convex mesh colliders on moving objects, collision matrix, layer use, ragdolls.
- Rendering from assets: grep scenes, prefabs, and URP assets for shadow-casting light count, realtime vs baked lights, shadow distance and resolution, render scale, MSAA, post-processing volumes, SRP batcher compatibility, huge textures, high-poly meshes. Check `Game/Assets/Settings/PC_RPAsset.asset` and `QualitySettings.asset` against `Architecture.md` §7.
- Memory and GC: Editor-time or runtime allocations in loops, large `Resources` folders, unbounded lists.
- Anything that only the Linux Editor would hide. State that Windows needs a player build test.

**Input and camera**
- Only the Input System, no legacy `Input.*` (`Architecture.md` §7). Check action asset usage, rebinding hazards, and cursor lock behavior.
- Camera: collision handling, jitter from update order (`LateUpdate`, interpolation), sensitivity configurability.

**Testing**
- What do the EditMode tests in `Game/Assets/Tests/` cover, and do they check behavior and edge cases (lock priority, next-run unlock timing, reset behavior) rather than implementation details?
- Gaps against the rules in `AGENTS.md` ("Validation and Documentation"). Rules that are not tested but should be.
- Check that test assemblies reference the right `*.Rules` assemblies and are Editor-only.
- Review the Editor scripts (`SceneBuilder`, `SmokeChecks`, `PlayerBuild`): are they deterministic, idempotent, and safe? Do they overwrite scenes or prefabs the other developer owns? Are they duplicated across milestones (M1…M5) in a way that now needs consolidation?
- Can the tests and smoke checks run headless? Find the exact command if one is documented; do not invent one.

**Unity asset hygiene and scene ownership**
- Every asset has its `.meta`, and no `.meta` is orphaned: compare `git ls-files` for `Game/Assets`.
- No duplicate GUIDs: `grep -h '^guid:' ... | sort | uniq -d`.
- Scripts in scenes and prefabs resolve: grep scene/prefab YAML for `m_Script` GUIDs with no matching `.meta` (missing script references).
- `EditorBuildSettings.asset` lists every gameplay scene (`Architecture.md` §10).
- Asset serialization is `Force Text` and meta files are visible (`EditorSettings.asset`).
- Nothing generated is committed: `Library`, `Temp`, `Logs`, `Obj`, builds, `output/` contents. Check `git ls-files` and `.gitignore`.
- Scene ownership: from `git log --format='%an %s' -- <scene>`, note which scenes have been touched by more than one author or by an agent. Prefabs and scenes edited by hand-written YAML are a merge-conflict and corruption risk, so say so when you see it.
- Scene size and merge risk: very large single scenes, nested prefab overrides that should be prefab variants.
- Naming and folder layout versus `Architecture.md` §2.

**Platform and build**
- Windows-only or Linux-only assumptions: path separators, case-sensitive asset names (Linux is case sensitive, Windows is not, so a case-only mismatch breaks one of them), `#if UNITY_EDITOR` leaks into runtime code, Editor APIs outside `Editor/` folders or `UNITY_EDITOR` guards (build breakers).
- Build scripts: output paths, scene lists, target platform.

### 4. Verify before reporting

Re-open the code for each High/Critical finding and confirm it. Drop any you cannot confirm or downgrade them to "Likely". Check whether a finding is already tracked in `docs/plans/` or in a verification file, and if so, say it is a known item.

### 5. Write the report

Create `docs/reviews/architecture-review-YYYY-MM-DD.md` using today's date (create the directory if needed; if the file exists, append `-2`, `-3`, and so on, instead of overwriting). Use English prose. Preserve Slovenian terms verbatim.

Use this structure:

```markdown
# Architecture Review — YYYY-MM-DD

**Scope:** what was reviewed (and what was not)
**Commit:** short SHA and branch
**Method:** static review only; no Unity Editor, Play Mode, profiler, or Windows build was run
**Milestone context:** what the plans say is current

## Summary
Three to six sentences: overall health, the most important risks, and the single best next step.

## Scorecard
| Area | Rating | One-line reason |
|---|---|---|
| Architecture & boundaries | Good / Fair / Poor | |
| Code quality | | |
| Performance (predicted) | | |
| Testing | | |
| Unity asset hygiene | | |
| Docs vs code alignment | | |

## What is working well
Short bullets; things to preserve.

## Findings
Ordered by severity. Each finding:

### [ID] Title — Severity (Critical | High | Medium | Low) · Confidence (Confirmed | Likely)
- **Where:** `path:line`
- **What:** what the code does
- **Why it matters:** impact on the slice, the 30 FPS target, or scene ownership
- **Suggested fix:** the smallest change that works; note if it needs Unity Editor wiring or touches another developer's scene
- **Effort:** S / M / L

Use IDs like ARC-1, QUA-1, PERF-1, TEST-1, ASSET-1, DOC-1.

## Docs and code conflicts
Table: document and section, what it says, what the code does, which looks wrong. Do not resolve design ambiguity; flag it for a decision.

## Test coverage gaps
Rules from AGENTS.md / Gameplay.md that have no EditMode test, ranked.

## Performance checks to run on the Ryzen 5600G
A short, concrete list: scene, scenario (for example "12 enemies in the city"), what to measure in the Profiler, and the threshold.

## Recommended next steps
At most seven items, in order, each linked to finding IDs. Separate "do before integration" from "do later".

## Not reviewed
Anything skipped or impossible without the Editor (for example, binary assets, runtime behavior, Windows FPS).
```

### 6. Finish

Reply with a short summary, not the report: the report path, the scorecard, the top three findings (ID, title, severity), and anything you could not verify. Remind the user that the report is uncommitted and that you did not change game code.

## Anti-patterns to avoid

- Padding the report with generic Unity advice that is not tied to a line in this repository.
- Reporting an issue you only saw in a grep result without reading the context.
- Recommending packages, frameworks, or a rewrite.
- Editing scenes, prefabs, `.meta` files, or anything under `Library/`, `Temp/`, or the package cache.
- Treating Linux Editor results or a clean static review as proof of Windows performance.
- Copying game rules into the report as if they were new decisions. Link to the canonical document instead.
