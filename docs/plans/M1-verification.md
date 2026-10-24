# M1 Handoff and Verification

Implementation branch: `feat/m1-movement`. Manual acceptance remains pending. Movement behavior and approved tuning are defined in [Gameplay](../Gameplay.md#m1-movement-baseline); scope and acceptance criteria remain in [the M1 plan](M1.md).

## Open and Play

1. Open `Game/` in Unity **6000.6.7f1** and allow asset refresh and compilation to finish.
2. Open `Assets/Scenes/Gameplay_Test.unity` and enter Play Mode.
3. Use WASD to move relative to the camera, mouse to orbit, Space to jump, and Left Shift for a short boost. Escape releases the cursor; left click recaptures it. Release held controls before resuming after focus loss or cursor release.
4. Try the ramp on the left, low jump obstacle in front, overhead obstacle on the right, and camera wall/corner toward the back. The arena has boundary walls.

The scene already contains the player, camera, light, and primitive collision geometry. It does not require changes to `SampleScene.unity`, level scenes, build profiles, or packages.

## Assets and Inspector Assignments

- `Assets/Scripts/Player/ScooterController.cs`: camera-relative movement, gravity, grounded jump, timed boost, and input lifetime handling.
- `Assets/Scripts/Player/ThirdPersonCamera.cs`: pivot follow, mouse orbit, pitch limits, sphere-cast obstruction handling, and cursor capture.
- `Assets/Prefabs/Player/Boki_Player.prefab`: controller capsule, primitive body/deck/forward marker, and `CameraPivot` at local height 1.4. Visuals have no colliders. The player uses built-in layer 2 (`Ignore Raycast`); arena geometry uses layer 0 (`Default`).
- `Assets/Input/M1/`: serialized references to `Player/Move`, `Player/Jump`, `Player/Boost`, and `Player/Look` in the existing input actions asset. `Sprint` was renamed to `Boost`, retaining its action ID and bindings, including Left Shift. Unity's serializer also added version/default priority fields.
- `Assets/Scenes/Gameplay_Test.unity`: the player instance has its movement camera assigned; the follow camera has the player's pivot and Look action assigned. Its obstruction mask includes only `Default`, excluding the player. Near clip is 0.1 m, follow distance 5 m, and initial pitch 18°.

The standalone player prefab has no reference to a scene camera. On enable, its controller resolves `Camera.main` if the movement camera is unassigned; assign a scene camera explicitly when integrating elsewhere. A follow camera still needs its pivot assigned to that instance's `CameraPivot`.

Controller capsule: height 1.8 m, radius 0.35 m, center Y 0.9 m, step offset 0.25 m, slope limit 45°, skin width 0.035 m. Movement and boost settings are serialized on the component. Mouse sensitivity, pitch range, obstruction radius/padding, and distance recovery speed are serialized on the camera.

## Automated Verification

Unity **6000.6.7f1**, Linux, batch mode with graphics disabled, using an isolated project copy:

- Scripts compiled and assets imported through Unity; every new asset/folder has an Editor-generated `.meta` file.
- Scene/prefab generation succeeded. The builder preserved the previously active scene and refused to overwrite existing M1 scene/prefab assets.
- Saved scene references, one camera and Audio Listener, and the Boost action rename passed checks.
- Play Mode smoke checks passed for grounding, physical wall blocking, camera distance shortening near a wall, and 120 Editor Play Mode updates without scene errors.
- Final input JSON comparison confirmed that action IDs and unrelated settings/bindings were preserved, apart from the expected rename and serializer defaults.
- Whitespace checks passed. No existing scene, package, or project setting was changed.

To rerun the smoke check, save any open scenes, then choose **Boki → Run M1 Smoke Checks**. This opens the test scene, enters Play Mode, checks it, and exits Play Mode. It logs `M1_SMOKE_PASSED` or `M1_SMOKE_FAILED`. It temporarily relocates the player during Play Mode; those moves are not saved. The menu refuses to run with dirty scenes.

For a terminal run against an isolated copy, invoke the pinned Unity executable with:

```text
-batchmode -nographics -projectPath <isolated-project-path> -executeMethod Boki.Editor.M1SmokeChecks.Run -logFile <log-path>
```

Do not add `-quit`; the smoke runner exits after leaving Play Mode. **Boki → Create M1 Test Arena** is the original creation utility, not a reset command; it refuses to overwrite the delivered scene/prefab.

Early batch smoke runs encountered an exception in Unity's Search indexing startup callback. The runner now waits for startup callbacks before entering Play Mode; subsequent checks passed without that exception. No Search package or generated cache was edited.

## Remaining Acceptance

The batch smoke run does not simulate keyboard/mouse input or establish movement feel. Complete [the M1 acceptance checklist](M1.md#verification-and-completion) manually, including diagonal speed, jump/ceiling behavior, boost timing and recovery, ramp traversal, cursor/focus recovery, repeated Play Mode sessions, and comparison at different frame rates. Check camera clipping visually at corners and overhead geometry.

Windows Editor/player behavior and the Ryzen 5600G performance check have not been run. Test a Windows x86_64 player at 1280×720 Low on the target machine and record FPS and failures. Linux batch results do not establish Windows compatibility or final performance acceptance.
