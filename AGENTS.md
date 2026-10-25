# Core Priorities

1. Deliver a working vertical slice.
2. Keep it performant on the Ryzen 5600G.
3. Protect the other developer's scenes and work.

When a tradeoff is needed, choose a playable slice over architecture and simpler implementation over extra scope. Scene ownership is a hard boundary, not a tradeoff.

## Scope and Git

- Work on a feature branch. Reserve `main` for finished, integrated work; never implement unfinished work directly on it.
- Do not commit, push, merge, or rewrite history unless the user explicitly requests it.
- Change only files needed for the task. Preserve existing user changes.
- Do not edit another developer's scenes or prefabs without explicit authorization. If ownership is unclear, ask before editing those assets; continue independent code work.

## Read When Needed

Read only the sections relevant to the task. Keep game rules in their canonical document, not in agent instructions.

| Task | Reference to read |
| --- | --- |
| Scooter movement, boost, jump, or camera feel | [Movement.md](Documentation/Movement.md); base values in [Story.md §4](Documentation/Story.md#4-enemy-design-and-escalation) |
| Routes, locks, unlocks, or run state | [Gameplay.md §2–4](Documentation/Gameplay.md#2-branching-route-graph); [Architecture.md §4](Documentation/Architecture.md#4-runtime-state) for implementation |
| Dialogue, character prompts, or narrative | [Story.md §3](Documentation/Story.md#3-story-beats-and-script); §1 for tone |
| Systems or scene wiring | [Architecture.md §3–6](Documentation/Architecture.md#3-proposed-modules-and-ownership) |
| Linux/Windows behavior or performance | [Architecture.md §1 and §7](Documentation/Architecture.md#1-technical-goal-and-boundaries); [Gameplay.md §8](Documentation/Gameplay.md#8-performance-target-and-unity-settings) |
| Editor setup and hardware | [README.md](README.md#development-setup) |
| Writing, structuring, auditing, or verifying C#, asmdefs, tests, or Editor tools | Skill `boki-unity-architecture` ([SKILL.md](.agents/skills/boki-unity-architecture/SKILL.md)) |

Do not invent missing design decisions. Check the relevant canonical document first.

## Gameplay Traps

- C unlocks apply on the next run, never the current run.
- `ROUTE_LOCK` takes priority over `PERSISTENT_LOCK`.
- Keep the route matrix and remaining gameplay constraints in [Gameplay.md](Documentation/Gameplay.md); reference them instead of copying them into agent files.

## Unity Changes

- Use the existing `Player_Model_Test` scene as the shared demonstration scene for Boki's model. Wire every Boki-related model change, behavior, animation, and effect into that scene so its result can be seen and tested there. Preserve the scene name. If the scene is missing from the current checkout, report it instead of creating a replacement or modifying another scene.
- Unity Editor's asset import pipeline generates `.meta` files. Preserve existing GUIDs and keep each asset paired with its `.meta` when moving or deleting it.
- After adding a script or asset outside Unity, import it in the project's pinned Editor version so Unity generates the `.meta`. Never invent a GUID. If Unity cannot run, report the pending import and missing `.meta` explicitly; do not claim the asset is ready to integrate.
- Do not edit generated `Library`, `Temp`, or package-cache files.
- Use simple colliders. Use mesh colliders only when the required collision shape cannot be represented with primitive colliders.
- Write readable C#. Add abstractions only for a concrete requirement. Keep gameplay rules separate from UI and scene wiring.
- Describe required Inspector assignments, scene setup, and manual verification steps when a change needs Editor wiring.
- Use the Editor version in `Source/Game/ProjectSettings/ProjectVersion.txt`. Verify Windows-specific Editor or build behavior on Windows; a Linux check does not establish Windows compatibility. Follow [Architecture.md §1 and §7](Documentation/Architecture.md#1-technical-goal-and-boundaries): Linux Editor FPS does not replace a Windows player build test.

## Validation and Documentation

- Add or update EditMode tests for changes to deterministic gameplay rules, especially RouteManager lock priority and next-run unlock timing. Cover behavior and edge cases, not implementation details. Use PlayMode or manual checks for scene wiring and interactions.
- For documentation-only edits, check references and consistency; do not add tests.
- Run the checks relevant to the change. State which checks passed, which could not run, and any remaining Editor steps.
- When a document is demonstrably wrong, correct its canonical source in the same task. If the intended behavior is ambiguous, report the conflict and ask for a decision before changing that behavior. Never silently copy a conflicting rule into another file.

## Naming

- Write code identifiers, comments, documentation, and explanatory prose in English. Preserve established Slovenian names, quoted text, character prompts, and references verbatim, including diacritics.
- Write new UI text in English except for the preserved Slovenian content above.
- Preserve established terms: `boost`, `Izgubljeni meščan`, `Podzemni prehod`, and `POWER OFF` panel. Do not rename them incidentally.
