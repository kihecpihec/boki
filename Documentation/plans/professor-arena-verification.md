# V1 professor arena movement

The user-authorized `V1_FERI` change gives the professor a slow patrol through the clear central classroom, turns his starting orientation toward the entrance, and removes the lectern immediately in front of him. The wall blackboard, desks, arena floor, perimeter colliders, and entrance seal remain in place. The shared professor prefab and other scenes are unchanged.

`ProfessorBoss.movementFloor` references `COL_Floor_Arena_Classroom_001`. Movement runs only during active combat with a living target, follows shared gameplay suspension, and uses capsule sweeps, floor support checks, and an inset from the floor bounds. The professor faces Boki while moving. Attack timing and damage remain unchanged.

The scene-specific `Assets/Art/ProfessorArenaWood.asset` mesh removes only the lectern body/top triangles. The matching lectern collider is inactive; the imported map mesh is unchanged. All Inspector references are already assigned.

`Boki → Version 1 → Run Integration Checks` exercises professor movement, arena containment, shared suspension, accepted-hit material feedback, and a detached death particle burst, alongside the existing route and victory checks. Batch entry point: `Boki.Editor.V1SceneChecks.Run`, without `-quit`. The focused `Boki → Version 1 → Run Professor Checks` (`Boki.Editor.V1SceneChecks.RunProfessor`) starts the saved FERI professor directly and checks the same movement/feedback/victory behavior without requiring earlier route triggers.

Manual verification: open `Assets/Scenes/V1_FERI.unity`, reach the boss through a selected route, and confirm the professor starts facing the entrance and the lectern is absent. Ride along every perimeter and back to the entrance seal; confirm neither participant escapes the arena. Observe a full patrol and both attack types, pause/resume, hit the professor, then defeat him. Check flash readability, death particles, victory, Retry and no new Console errors. Linux headless checks do not establish rendered appearance or Windows/5600G performance.

Validation on Unity 6000.6.7f1, Linux headless:

- Focused professor check passed: movement, 58 interior-position samples, shared suspension, hit material feedback, emitted death particles, and terminal victory.
- Unity imported the new mesh and generated its `.meta`; no Inspector assignments remain.
- Full V1 integration stopped at stage 1 (`Physical Forest selection trigger`) before reaching FERI. Full route entry remains a manual/integration follow-up.
- Architecture audit has no FAILs; existing warnings remain. The Editor also logged its existing QuickSearch indexing exception.
- Rendered effects, hands-on perimeter riding and Windows player performance remain unverified.
