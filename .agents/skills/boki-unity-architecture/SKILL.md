---
name: boki-unity-architecture
description: Use before writing or changing any C#, asmdef, prefab wiring, Editor tool, or test in the Boki Unity project (Game/). Defines where code goes, which patterns to follow, what never to do, and how to verify and hand off, so new work stays consistent with the existing rules/adapter split. Also use to audit or bootstrap project structure, add a new gameplay system (battery, Guilt pressure, upgrades, pause, enemies, HUD), or review a Unity change.
---

# Boki Unity architecture

This skill is the engineering rulebook for `Game/`. It does not restate game rules. Those live in `docs/` and `AGENTS.md`; read the section your task needs (AGENTS.md has the lookup table) and never copy a rule into code comments or other docs. Link to it.

Pinned stack: Unity `6000.6.7f1`, URP, Input System, no extra packages. Two developers, jam deadline, 5600G target. Playable slice beats architecture; simple beats clever; scene ownership is a hard boundary.

## Workflow

1. **Orient.** Run `.agents/skills/boki-unity-architecture/scripts/audit.sh` from the repo root. Read the canonical doc section for the task. Read the two or three existing files nearest to what you will touch and match them.
2. **Classify each piece of the change** with the placement table below. If a decision is a game rule and no doc states it, stop and ask; do not invent it.
3. **Rules first.** Put deterministic logic in a `Rules` assembly with a test before writing the MonoBehaviour that uses it.
4. **Thin adapter second.** The MonoBehaviour reads Unity input/physics/time, calls the rules, and applies results to the scene.
5. **Wire.** List the Inspector assignments and scene edits. Make them only in scenes you are authorized to edit.
6. **Verify and hand off** per `references/verification.md`. Say what ran, what could not, and what Editor steps remain.

## Placement table

| Piece | Where | Notes |
|---|---|---|
| Deterministic rule (damage, battery, thresholds, locks, pressure timers, upgrade eligibility) | `Assets/Scripts/<Domain>/Rules/`, own `.asmdef`, plain C#, no `UnityEngine` | Time comes in as a `double`/`float` argument. Randomness comes in as an injected value or `System.Random`. |
| Persistence or other I/O | Interface in the Rules assembly, implementation in `Scripts/Core/` | The `IUnlockStore` / `PlayerPrefsUnlockStore` pair is the model. |
| Run-wide mutable state | `RunState` (rules) owned by `RunSession` (adapter) | One authority. Scene objects bind to it; they never keep a second copy. |
| Scene behavior (movement, AI, triggers, spawners, pickups, gates) | `Assets/Scripts/<Domain>/` MonoBehaviour | Sealed, one per file, thin. |
| HUD and menus | `Assets/Scripts/UI/` | Reads state and events; contains no gameplay rules. |
| Tuning numbers | `[SerializeField]` on the adapter with the doc value as default, or a constructor argument to a rules class | Canonical values stay in `docs/`. Add a `// Gameplay §x` pointer, not a copy of the prose. |
| Scene/prefab setup helpers, smoke checks, builds | `Assets/Editor/`, namespace `Boki.Editor` | See "Editor tooling" below. |
| EditMode tests | `Assets/Tests/EditMode/<Domain>/`, one test asmdef per Rules asmdef | Behavior and edge cases, not implementation. |
| Anything else | Ask, or write the smallest thing that works and note it | |

Domains today: `Player`, `Combat`, `Enemies`, `Routes`, `Core`, `UI` (Architecture §2 also reserves `Boss`). New rule areas named in Architecture §6 (battery/light, Guilt pressure, XP and upgrades, shared pause) get their own `Rules` folder and assembly rather than growing `RunState` into a catch-all. `RunState` composes them and remains the single save point for carry-over and reset.

Read `references/project-structure.md` for the target tree, assembly graph, and migration rules, and `references/code-patterns.md` for copyable templates.

## Non-negotiables

Gameplay traps (details in `AGENTS.md` and `docs/Gameplay.md`, so link rather than restate):
- C unlocks apply on the next run. `ROUTE_LOCK` beats `PERSISTENT_LOCK`. Persist immediately, snapshot only on New Run.
- Milestone, gate, exit, and boss-start handlers are idempotent.
- One shared pause authority; there is no per-system pause.

Code:
- Dependencies point one way: `Editor/Tests → Runtime adapters → Rules`. Rules never reference Unity, adapters, or other adapters' internals.
- Subscribe in `OnEnable`, unsubscribe in `OnDisable`/`OnDestroy`. Every `+=` has a matching `-=`.
- Static state (singletons, registries) has a `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` reset so Enter Play Mode without domain reload stays correct.
- Prefer serialized references over `Find*`/`GetComponent` in `Update`. Cache in `Awake`. Validate required references in `OnEnable`, `Debug.LogError(..., this)`, then `enabled = false` (see `RouteGate`).
- Gameplay reads `Time.deltaTime` only inside adapters, and passes the delta into rules. No `Time.timeScale` writes outside the pause authority.
- Input System only, through serialized `InputActionReference`s. Whoever enables an action disables it. No legacy `Input.*`.
- Uncapped regular arena spawns per Gameplay §5, simple colliders, no ragdolls, no per-frame allocation (no LINQ, no string building, no `new` collections in `Update`; use `*NonAlloc` physics). No pooling or abstraction layers without a profile or concrete requirement.
- No new packages, no ScriptableObject event buses, no service locators, no DI framework, no generic `Manager` or `Singleton<T>` base class. Existing `RunSession.Ensure()` is the one sanctioned global; do not copy that pattern for a second one without agreement.
- Use identifiers, comments and new UI strings in English. Preserve Slovenian names and quoted text verbatim (`Izgubljeni meščan`, `Podzemni prehod`, `POWER OFF`) and keep `boost`.

Process:
- Never commit, push, merge, or rewrite history unless asked. Stay on a feature branch, not `main`.
- Never edit another developer's scene or prefab without explicit authorization. Ownership unclear: ask, and continue code-only work. Do not rebuild production scenes with a scene builder; write an idempotent upgrade helper that refuses Play Mode and dirty scenes, as `docs/plans/M6.md` requires.
- A script or asset created outside Unity has no `.meta`. Never invent a GUID. Import it in `6000.6.7f1`, or report the pending import and do not call the work ready to integrate. Move or delete assets together with their `.meta`.
- Edit `Game/ProjectSettings/`, `Packages/`, input assets, and shared prefabs only when the task needs it and only after agreement. Never touch `Library/`, `Temp/`, or package caches.

## Code style (match what exists)

- Namespaces `Boki.<Domain>` (`Boki.Core`, `Boki.Routes`, `Boki.Editor`). File name equals type name. `sealed` by default; `[DisallowMultipleComponent]` for singletons-per-object components.
- `[SerializeField] private` for Inspector fields, with `[Min]`/`[Range]` where a bad value is possible. Public surface is read-only properties, methods, and C# `event Action`.
- Comments explain why or cite a doc section. Do not narrate what the code does. Keep density near the surrounding files, which is sparse.
- Return `bool` from commands that can be rejected (`TrySelect`, `TryAdvance`) instead of throwing for expected game states. Throw for programmer errors (`ArgumentOutOfRangeException` in constructors).
- No dead code, no speculative parameters, no `TODO` without an owner and doc reference.

## Editor tooling

Scene builders, smoke checks, and player builds exist per milestone (`M1`..`M5`). `M2`–`M5PlayerBuild.cs` differ only by name. Do not add an `M6PlayerBuild.cs` clone. When you need another build or check, first generalize one shared `Boki.Editor` helper (parameterized by scene list and output name) and migrate callers in the same change, or ask before leaving the duplication. Editor code must refuse Play Mode and dirty scenes before modifying assets, be idempotent, and restore prior scene setup.

## Bootstrap or audit pass

Use this when the request is to set up or tidy the project rather than build a feature.

1. Run `audit.sh`. FAIL lines are blockers. WARN lines are a backlog, not an instruction to fix everything now.
2. Compare against `references/project-structure.md` ("current" vs "target" columns). Apply, in order: (a) pure-code fixes needing no new assets, (b) tests for untested rules, (c) items needing Unity import, listed as pending with exact steps.
3. Changes to shared files (`EditorBuildSettings.asset`, `ProjectSettings/*`, `Packages/*`, asmdef moves) are proposals. Present them and wait, even when the fix is obvious.
4. Record structural decisions in `docs/Architecture.md` §2/§3 (the canonical source), not in this skill. Update this skill only for engineering rules.

## When this skill is wrong

If following it would contradict `AGENTS.md` or a canonical doc, those win. Report the conflict and fix the skill in the same task.
