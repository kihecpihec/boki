# Project structure: current state, target, migration rules

Canonical layout lives in `Documentation/Architecture.md` §2–3. This file adds the engineering view: assemblies, dependency direction, and what is debt versus intended. Update Architecture.md for any structural decision; update this file only to keep the "current" column true.

## Assembly graph

```text
Boki.Combat.Rules  <-  Boki.Run.Rules  <-  (future Rules assemblies compose here via RunState)
        ^                    ^
        |                    |
Boki.*.Tests (Editor-only, EditMode)           Assembly-CSharp (runtime adapters, currently predefined)
                                                      ^
                                               Assembly-CSharp-Editor (Assets/Editor)
```

Rules assemblies can never see Assembly-CSharp, so rules-never-depend-on-adapters is enforced by the compiler. Adapters depending on rules is the only allowed direction. Everything else is convention, enforced by `scripts/audit.sh` and review.

### Rules for assemblies

- Name `Boki.<Domain>.Rules`, folder `Assets/Scripts/<Domain>/Rules/`, namespace `Boki.<Domain>` unless the domain already uses another (`Boki.Run.Rules` holds namespace `Boki.Routes`; keep, do not rename incidentally).
- Every Rules asmdef sets `"noEngineReferences": true`, so the compiler rejects any `UnityEngine` use. Copy that line into each new one; `audit.sh` warns when it is missing.
- Each Rules assembly gets `Boki.<Domain>.Tests` under `Assets/Tests/EditMode/<Domain>/`, referencing the Rules assembly, `includePlatforms: ["Editor"]`, matching the existing test asmdefs.
- Create a new Rules assembly when the logic is cohesive, route-agnostic, and has its own tests (battery, Guilt pressure, XP/upgrades, pause reasons). Add to an existing one when the data is inseparable from it (route gate state belongs with `RunState`).
- `RunState` composes rule objects and is the only place that resets them on New Run. Adapters never reset a rule object themselves.

### Runtime adapters stay in Assembly-CSharp for now

PlayMode tests and Editor code that wants assembly isolation need a runtime asmdef (test assemblies cannot reference the predefined Assembly-CSharp). Introduce `Boki.Runtime` (and an Editor asmdef `Boki.Editor` referencing it) once, in one task, when the first PlayMode test is wanted or compile time hurts. Procedure: move nothing between folders; add one asmdef at `Assets/Scripts/`, referencing every Rules assembly plus `Unity.InputSystem`, `Unity.TextMeshPro`/`UnityEngine.UI` as needed; add one at `Assets/Editor/`; import in Unity; confirm scene script references survived (script GUIDs do not change). Do not do this as a drive-by.

## Current versus target

| Area | Current | Target / rule |
|---|---|---|
| Rules code | `Combat/Rules`, `Routes/Rules`, both `noEngineReferences` | Add Rules for battery, pressure, progression, pause as those systems land |
| Run-wide state | `RunState` with public setters `Battery`, `XP`, `KillCount`, `List<string> Upgrades` | Replace raw setters with intention-revealing methods (`TryDrain`, `AddCharge`, `AddXp`, `TryApplyUpgrade`) when the revised loop lands; typed upgrade identifiers, not strings; fractional battery per Architecture §6 |
| Runtime assemblies | Predefined (Assembly-CSharp) | `Boki.Runtime` when needed (above) |
| Scene path constants | `const string` on `RunSession` | One place. Do not add path strings in other classes. Consider a small `SceneIds` static in Core when a third consumer appears |
| Editor tools | Per-milestone builders/checks/builds; `M2`–`M5PlayerBuild.cs` are identical apart from names | One shared helper; no new per-milestone clones |
| Assets by milestone | `Art/M2/`, `Input/M1/` | New assets go by type/domain per Architecture §2 (`Art/Materials`, `Prefabs/Enemies`, ...). Do not relocate old ones without Unity and an owner |
| Build settings | Lists `Level1_Pohorje`, `Level2_Maribor`, `Level3_FERI` (in that order; `SampleScene` disabled). Test scenes are intentionally absent | Add `MainMenu` first when it exists. Edit through the Editor or copy GUIDs from the scene's `.meta`; never invent one |
| Version control mode | `Visible Meta Files`, `Force Text` | Keep. `audit.sh` fails if Force Text changes |

## Planned systems and their homes (proposals; Architecture §3 says names can change)

| System | Rules (deterministic, tested) | Adapter (scene) |
|---|---|---|
| Battery / light | Charge, capacity, drain, empty state, clamp, arena charge pickup timing | `LightBattery` (spot light, speed modifier hook), pickup behaviors |
| Guilt pressure | Per-level pressure accumulation, tier snapshots/caps, reentry and next-level reset, warning/chase timing | `GuiltChaser` (state machine adapter driving movement), pressure spawner |
| XP and upgrades | Thresholds, queued selections, six-selection ceiling, additive caps, eligibility, random offers (injected RNG) | `UpgradeManager` (pause + UI hand-off), pickups, upgrade effect appliers |
| Pause | Set of pause reasons (upgrade selection, loading, menu, scripted sequence); `IsPaused` | `PauseAuthority` in `Core`: the only writer of `Time.timeScale`; UI uses unscaled time, gameplay uses scaled `deltaTime` so pause freezes it with no per-system checks |
| Enemies | Replenishment budget and caps if non-trivial | `MeleeEnemy`, `RangedEnemy`, `EnemyProjectile`, spawners; per-level looks are prefab variants and materials, not subclasses |
| Boss | Phase/attack scheduling and route modifier values | `ProfessorBoss` adapter |
| HUD / menus | None | `Scripts/UI/*`, read-only against `RunSession` state and events |

Check the "Revised Loop Integration Requirements" in Architecture §6 before touching any of these. Per-level accumulated pressure and per-space pickup timers must survive reentry; XP, upgrades, capacity, and charge survive scene changes; pressure resets at the next level.

## Scenes and prefabs

- Ownership comes from Architecture §8 and the user, never from this file. Production scenes `Level1_Pohorje`, `Level2_Maribor`, `Level3_FERI` need explicit authorization per task.
- Build gameplay scenes as: persistent player prefab + scene-local adapters bound to `RunSession`. Scene-local code binds to `RunSession.Ensure()` in `OnEnable` and must tolerate standalone test scenes (`PlayerHealth.useRunSession` is the pattern).
- Prefer prefab variants over scene overrides. Shared prefabs (player, `RouteGate`, boss) have a single editor at a time.
- Gates, exits, and boss triggers use trigger colliders plus a one-shot latch. See `code-patterns.md`.
- Layers and tags: define once in `TagManager` through the Editor, mirror as `const int`/`LayerMask` fields on the adapter, not literals scattered in code. (`MeleeEnemy` passes layer mask `1`; replace with a serialized `LayerMask` when touching it.)

## Project settings checklist (verify, change only with agreement)

- Asset Serialization `Force Text` and `Visible Meta Files`.
- Active input handling: Input System package only.
- URP Forward, one main light, minimal shadow-casting lights, simple fog, render scale 0.75–0.9 when needed. VHS/glitch effects must be switchable.
- Player: Windows x86_64, 1280×720, Low quality. Record FPS on the 5600G in a Windows player build; Linux Editor numbers do not count (Architecture §7).
- Uncapped regular arena populations per Gameplay §5; simple colliders; no ragdolls.

## Adding a script, asmdef, or asset without the Editor

1. Create the file in the right folder.
2. Do not create the `.meta` yourself. Report: "pending Unity import; no `.meta`".
3. If Unity can run, import with the pinned Editor (see `verification.md`) and confirm the new `.meta` appears and is paired. Only then call it ready.
4. Moving or deleting: move the asset and its `.meta` together, preferably inside the Editor.
