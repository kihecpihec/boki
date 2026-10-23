# Pohorje first modeling pass

Open `ENV_Pohorje_FirstPass.blend` and select scene `Pohorje_FirstPass`. The original default scene is preserved. `preview.png` shows the initial layout and the separate module display.

This is a modular source kit and traversal study, not a complete Pohorje level. The study contains a safe start, rising calibration ramp, fork pocket, straight A approach, 12 × 12 m proposed clearing, and open shared-exit stub. Bellevue dressing, actual slalom, B/C branches, gates, rear seals, and mountain massing remain to build. Gameplay guides are empty transforms with proposed positions, without scripts or collision.

## Files and scale

- `Export/`: six individually exported render meshes and a one-meter calibration cube. Modules export at their local origin, separate from display placement.
- `module-report.json`: actual triangle counts, Blender XYZ dimensions, and object scales. Blender Z is vertical; FBX exports with Y up and -Z forward.
- `validation.json`: mesh checks and calibration FBX reimport check. Blender roundtrip does not certify Unity orientation or collision.
- `build.py`: reproducible creation script, run inside Blender. It refuses to replace an existing `Pohorje_FirstPass` scene. The script saves the source and exports; preview rendering is separate.

Path top is local Z=0; thickness extends downward. Ramp rises 0.5 m over 4 m, with 0.2 m below the lower finished plane (0.7 m overall bounding height). Library meshes use unit object scale. Materials are plain shared color materials for this first pass; texture/atlas work is pending. The path is deliberately a simple flat surface. Spruce uses 172 triangles, rock 52, log 28.

## Pending Unity verification

No files have been added under `Game/Assets`; no Unity scenes or prefabs have been edited. Import the calibration cube and modules using Unity **6000.6.7f1**, allowing Unity to generate `.meta` files. These output-folder exports do not yet have Unity metadata and are not ready to integrate without that import and validation.

The scene owner should create a disposable movement test and assign render meshes/materials. Add simple floor boxes, a smooth ramp surface, trunk capsules, and coarse rock/log blockers as needed. Collision hints on source objects are authoring notes, not imported working colliders. Test meter scale, orientation, ramp seams, stopping, boost, jump, camera clearance, and bank bypasses with the actual Boki controller. The layout is open at the sides and does not provide branch isolation or safe cliff boundaries.

Charge/spawn/Guilt empties are spatial proposals only; spawn candidates still need runtime offscreen, distance, and valid-ground filtering. The 52 m study does not establish the forest's 2–3 minute pacing. Target-hardware Windows performance remains unmeasured.

References: [world handoff](../world-modeling-v1/UNITY_WORLD_HANDOFF.md), [module catalog](../world-modeling-v1/MODULE_CATALOG.md), [combat-space overlay](../gameplay-mocks-v2/HANDOFF.md), and canonical [gameplay](../../docs/Gameplay.md).
