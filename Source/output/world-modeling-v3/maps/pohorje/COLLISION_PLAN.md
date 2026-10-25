# Pohorje v3 collision and scene handoff

The Blender model and `manifest.json` jointly define collision. Unity instantiates exact parameters; it does not infer a box from a rotated bounding box or guess a capsule axis from vertices. The game rules remain in [Gameplay §2–5](../../../../../Documentation/Gameplay.md#2-branching-route-graph).

Inspect the [top-down collision drawing](collision-plan.svg), or use the visibility controls in [Review.html](Review.html) to isolate boundaries, roofs and markers.

## Coordinate and ownership contract

All layout, collider and Unity-marker values are metres in Unity XYZ: X right, Y up, Z downhill. Baked Blender mesh positions are `(-x, -z, y)`. Source meshes have unit object scale and a common world origin. FBX export converts axes/units; the Unity importer uses scale 1. Reimport checks compare world-space coordinates.

Each primitive has a stable `COL_Box_*` or `COL_Capsule_*` name, owner, role, center, quaternion and dimensions in the manifest. The Blender guide is the same oriented box or rounded capsule. Guide renderers never enter the assembled prefab. Every mesh, guide and marker is exported exactly once.

```text
Pohorje_Production
  Shared / InteriorRender
  Route_A / InteriorRender
  Route_B / InteriorRender
  Route_C / InteriorRender
  Backdrop / InteriorRender
  Seals / PL_Seal_<A|B|C>_<Front|Rear>
  Collision / Terrain / COL_Mesh_PL_<owner>_Ground_*
  Collision / AuthoredShapes / COL_*
  Markers / MK_*
```

Disabling an `InteriorRender` root suppresses only art. Ground/obstacle collision, marker volumes and opaque front/rear seals remain outside it. Roof render meshes are individually named for editing; their colliders remain enabled when only their rendering is hidden.

## Physical surfaces

| Role | Representation | Authoring decision |
|---|---|---|
| Ground | 74 static non-convex MeshColliders | One continuous heightfield, split for ownership/culling. Shared border positions are identical. No stacked walkable floor sheets. |
| Riding lanes | Ground surface | Safe 12 m, A 11 m, B 16 m, C 10 m; exits 10/11/10 m before the bank apron. Obstacles create deliberate slalom/jump lines while the central corridor remains clear. |
| Bank toe | Ground surface | A 1.6 m apron keeps a coarse triangle from pulling a steep bank into the declared riding width. |
| Containment | Oriented boxes on the visible bank rise | Located beyond the apron, 3.7 m outside lane half-width. Segments are omitted at openings into other lanes/pads. Outer safety boxes lie outside the playable spaces. |
| Ramps | Thin oriented BoxColliders | Visual top and collision share one plane. A 4 cm entry lip is below the controller step offset. Stage and terminal ramp tops meet their platforms exactly. |
| Stage / terminal platforms | BoxColliders | Separate from speakers, posts, decorative trim and the underlying terrain. |
| Logs / playable trees | Capsules | Radius matches the visible log/trunk; trunk capsules follow the visible lean and full trunk height. There is no oversized whole-tree blocker. |
| Crates / carts / log stacks / columns | BoxColliders | One simple shape per solid obstacle or stack. Low relief and bolts do not add collision. |
| Roofs / tunnel shell | Oriented boxes | Sloped tunnel pieces preserve standing/jump/camera clearance. Cabin roof slopes, terminal roof, tunnel/alcove roofs and stage canopy have explicit shapes. |
| Scenic forest / bank decoration / city | None | Outside containment or nonblocking dressing. No alpha-card collider forest. |

All 343 authored primitive shapes are boxes/capsules. Terrain is the only MeshCollider geometry. There are no Rigidbody components on the map and no convex MeshCollider proxies.

Only intentional decorative intersections remain: buried foundations, trunk roots, fitted wall seams and rock-to-bank transitions. They do not create overlapping coplanar riding surfaces. Do not replace the ground with a single enclosing box or enable Generate Colliders on imported scenery.

## Gates and seals

Each route has a front and rear opaque seal, an independent blocker, visible frame/wing walls, a gate marker, and a selection-volume marker at the front. Wing walls close the flat apron beyond the posts so a locked route cannot be bypassed around its gate. Seals remain outside the branch render roots.

The delivered prefab has all six seals closed and all three selection triggers disabled. The art preview disables the six blockers and hides their visuals, opening all three routes for inspection. This is an explicit preview override, not route/unlock logic.

For each route `X`, the scene owner can bind the existing `RouteGate` fields:

| Inspector field | Asset reference |
|---|---|
| `manager` / `choice` | The scene's existing RouteManager and X |
| `blocker` | `Collision/AuthoredShapes/COL_Box_Seal_X_Front` |
| `selectionTrigger` | `Markers/MK_Trigger_X` BoxCollider |
| `barrierVisual` | `Seals/PL_Seal_X_Front` MeshRenderer |
| `exitBlocker` | `Collision/AuthoredShapes/COL_Box_Seal_X_Rear` |
| `exitVisual` | `Seals/PL_Seal_X_Rear` MeshRenderer |
| `sign` | A scene-owned dynamic TextMesh for lock/status messages; the baked route labels remain art |
| `encounterRoot` | A separate scene-owned encounter root, not the asset's `InteriorRender` root |

Place the RouteGate adapter on the selection volume so its trigger callback receives the player. Bind branch visibility separately to `Route_X/InteriorRender`; keep collision and seals active. Preserve lock priority and next-run unlock timing from the canonical docs. This delivery changes no route rules or production scene wiring.

## Gameplay markers

- `MK_Start_Player`: grounded Bellevue spawn, facing downhill.
- `MK_Gate_X_Front/Rear` and `MK_Trigger_X`: gate anchors and sized selection volumes.
- `MK_Occupancy_X`: sized arena occupancy volumes.
- `MK_Charge_X_1..3`: three grounded, capsule-clear charge locations per combat space.
- `MK_Spawn_X_1..4`: grounded, capsule-clear candidate spawns; runtime still enforces offscreen placement and the minimum player distance.
- `MK_Guilt_Anchor_X_1..3`: route-local placement candidates.
- `MK_Event_GuiltReveal_A/C`: logging-landing and grated-bay reveal anchors.
- `MK_Watcher_B`: gondola perch.
- `MK_StrongerEnemy_C_1..2` and `MK_GuaranteedPickup_C`: inside the tunnel route on valid floor.
- `MK_Exit_Forest`: the overlook transition volume.
- `MK_Light_*`: optional scene-light positions, not lights embedded in the FBX.

No marker spawns an actor, changes run state or opens a route by itself. The Unity prefab includes correctly sized trigger colliders for selection, occupancy and the exit. Production gameplay adapters remain scene-owner assignments.

## Verification and manual review

`PohorjeProductionChecks` samples the actual imported terrain at 843 reference positions, sweeps the existing player CharacterController along center and offset corridors at 6/10.5/18 m/s, crosses all three combat spaces, checks marker standing capsules, probes every seal closed/open including lateral jumping attempts, and runs random jump rides. `unity-validation.json` records exact results and errors; the command throws if any fail.

Open `Pohorje_Production_Preview` in Unity 6000.6.7f1. Check jump/boost entry onto both stage ramps and the terminal ramp, brush past trunks/logs, orbit the camera under the tunnel and stage canopy, inspect the grate from both ends, and ride to the city overlook. Review at 1280×720 Low with normal input. No hands-on camera-feel result or Windows Ryzen 5600G FPS result is claimed by the automated checks.
