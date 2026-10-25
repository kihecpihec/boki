# Blender → Unity entire-world handoff

Target Editor: **6000.6.7f1**, URP. The scene names, module/prefab names, snap markers and integration bindings below are proposals. Built environments are delivered separately: [Pohorje](../world-modeling-v3/maps/pohorje/README.md) and [Maribor](../world-modeling-v3/maps/maribor/README.md) (v3) and [FERI](maps/feri/README.md). Remaining locations are documentation and reference art.

## 1. Inspected project and ownership boundary

At packaging against dev, the project contains Level1_Pohorje, Level2_Maribor and Level3_FERI, along with movement, combat and route test scenes. Treat this pack as reference for the existing scene owners, not instructions to replace their work. No existing scene, prefab, script, GUID or `.meta` was edited for this pack.

A scene owner must assemble and wire a scene in Unity before it is ready for integration. Agree ownership before changing another developer's scenes or prefabs. Import new assets in the pinned Editor and let Unity generate metadata; never fabricate GUIDs. Keep source `.blend` files and exported FBX files separate from generated Unity folders.

The earlier [character handoff](../character-modeling-v2/UNITY_HANDOFF.md) covers Boki's model and controller wiring.

## 2. World structure

Use the [gameplay overlay handoff](../gameplay-mocks-v2/HANDOFF.md) and [updated plan gallery](../gameplay-mocks-v2/index.html) alongside the topology below. Each branch now has a proposed widened combat pocket after its approach, stationary rechargeable charges, candidate replenishment/Guilt anchors, and an exit with no kill quota. The original fixed encounter groups alone do not satisfy the updated combat loop. The classroom needs two equivalent charges inside its closed entrance seal.

Build **three scenes**, using shared concrete and prop families:

```text
Level1_Pohorje
  Bellevue intro / safe start
  Shared fork → F_A or F_B or F_C → shared city overlook
  ExitForest → Level2_Maribor

Level2_Maribor
  Shared arrival / filtered fork
  C_A or C_B or C_C → shared FERI sign
  ExitCity → Level3_FERI

Level3_FERI
  Shared campus arrival / filtered fork
  E_A or E_B or E_C → shared classroom landing
  ArenaEntry → ONE professor arena in this same scene
```

Scene transitions preserve health, light, upgrades and recorded route choices. Do not reset the chosen route before loading the next scene. There is no return to a prior scene during the same run.

The exact inter-scene connections come from [Gameplay.md §3](../../../Documentation/Gameplay.md#3-allowed-route-matrix). The packaged route JSON is a generated drawing snapshot, not a replacement rules file. Regenerate it when Gameplay.md changes.

## 3. Coordinate and module contract

Use meters; the Unity result has +Y up. Placement uses a proposed 1 m base unit and 4 m structural bay. Standard floor origins are at a lower-left bay corner on the **finished floor plane Y = 0**. Floor thickness extends below that plane.

A proposed straight bay uses:
- X/Z footprint 4 × 4 m.
- Interior clear width 3 m and height 3.5 m.
- Wall thickness 0.2 m, with the remaining envelope available for structural margins.
- Door clear aperture 2.4 m wide × 3 m high.
- Gentle ramp module: 4 m horizontal run and 0.5 m rise.

These dimensions are not signed-off collision limits. The existing player capsule is 1.8 m high, radius 0.35 m, with 45° slope limit and 0.25 m step offset. Camera position and jump apex require clearance beyond the standing capsule. Use a test bay with the real player before finalizing modules. [Unity's CharacterController reference](https://docs.unity3d.com/6000.6/Documentation/Manual/class-CharacterController.html) explains its slope, step and capsule constraints.

Snap proposal:
```text
Snap_S = (2, 0, 0)
Snap_N = (2, 0, 4)
Snap_W = (0, 0, 2)
Snap_E = (4, 0, 2)
```

Ramp north snap is at Y = 0.5 m. Align finished floor planes at seams; do not introduce a tiny raised lip. Door leaves pivot at hinges, trees at trunk bases, cars at ground midpoints and lamps at ground bases. Use unit object scale for approved exported modules.

The drawings of whole routes use **topology units** and do not prescribe exact world dimensions or travel length. The module/collision and arena sheets explicitly show meter-based measurements. Do not scale an entire generated picture to infer meters.

## 4. Blender delivery and material plan

Use the module catalog IDs for source object and export names. Example families:
```text
Source/ENV_SharedConcrete.blend
Source/ENV_Pohorje.blend
Source/ENV_Maribor.blend
Source/ENV_FERI.blend
Export/ENV_SH_Floor_4m.fbx
Export/ENV_SH_Wall_4m.fbx
Export/ENV_SH_Ramp_4m.fbx
Textures/T_SharedConcrete_BaseColor.png
Textures/T_Forest_BaseColor.png
Textures/T_City_BaseColor.png
Textures/T_FERI_BaseColor.png
```

Model each reusable module individually; assemble the final levels in Unity. Avoid one combined mesh for the entire level: it makes ownership, collision and visibility iteration harder. Keep render meshes, collision shapes and semantic markers independent.

Use broad faceted silhouettes and painted low-resolution surfaces. Starting texture proposals are 256² repeating textures or 512² family atlases, not a unique texture per object. Prioritize silhouettes and legible obstacles over modeled cracks. Flat water, facade fronts and skyline/bridge silhouette cards are sufficient.

Keep collision primitive whenever it represents the needed surface. Use mesh collision only for a required shape primitives cannot represent; a smooth custom ramp surface is preferable to colliding with each decorative plank.

Export a meter cube and one wall/floor/ramp test before exporting the whole kit. Confirm scale, facing, normals, UV seams and material assignment in the pinned Unity Editor. Model import guidance and coordinate testing from the character pack also apply. Export presets must be proven with a round trip, not inferred from the concept art.

## 5. Scene hierarchy and Inspector binding plan

Proposed scene organization:
```text
LevelRoot
├─ RuntimeBindings                 # link to existing run/session services later
├─ SharedStart
│  ├─ PlayerStart                  # position + facing, not a new player duplicate
│  └─ IntroMarkers                 # Bellevue only
├─ RouteFork
│  ├─ Gate_A
│  ├─ Gate_B
│  └─ Gate_C
├─ Branches
│  ├─ Route_A                      # approach + combat pocket + rear seal
│  ├─ Route_B
│  └─ Route_C
├─ SharedEnd
│  └─ LevelExit / ArenaEntry
├─ Scenery                         # silhouettes, flat water, unreachable roofs
├─ LightingAndAudio                # few real lights + emitter meshes
└─ GuiltAnchors                    # 2–3 offscreen candidate points per scene
```

Runtime component classes are described in Architecture.md and may not exist yet. Required later semantic assignments:

| Object | Bindings to supply in the later implementation |
| --- | --- |
| RouteManager | Correct level and access to preserved RunState + unlock snapshot |
| RouteGate | A/B/C route ID, render state objects, blocker, selection trigger, lock message |
| LevelExit | Correct completed level, next scene, persisted milestone and idempotent transition guard |
| ArenaEntry | Persist FERI C, disable Guilt chase, start the professor once |
| Spawn marker | Shared melee/ranged prefab family, cap-aware encounter controller |
| Pickup marker | Existing/planned pickup prefab and allowed reward; guaranteed pickup on F_C |
| POWER OFF panel | Interaction point, range/layer policy, E action and E_C boss-modifier record |
| Trap marker | Same visible footprint as the damage area; two on E_C |
| Guilt anchor | Offscreen candidate with sufficient route/camera clearance |

Each branch additionally needs a combat-space occupancy volume, replenishment candidates, fixed charge objects with available/depleted presentation, a rear seal, and a visibility root. Assign the selected-route owner to all of them. See the companion JSON for proposed coordinates and the handoff for timer/activation rules. Lent's fourth charge is a recorded authoring proposal, pending playtest approval.

Do not add a duplicate DontDestroyOnLoad player/session while testing scene transitions. Do not assume importing a model assigns gameplay references automatically.

## 6. Gate construction and state behavior

Proposed prefab:
```text
GateRoot
├─ Frame_Render
├─ Leaf_Render                     # hinge pivot if it moves
├─ Sign_Render
├─ LockVisual_Persistent
├─ LockVisual_Route
├─ BlockingCollider                # real blocker spans the opening
├─ SelectionTrigger                # separate nonblocking sensor beyond gate
└─ ApproachMessagePoint
```

State requirements are canonical:
- AVAILABLE: bright sign, passable entrance, active selection trigger.
- PERSISTENT_LOCK: padlock, DISCOVER ON ANOTHER RUN, blocker enabled and selection trigger disabled.
- ROUTE_LOCK: dark roots/shadow barrier, SEALED BY YOUR PREVIOUS PATH, blocker enabled and selection trigger disabled.
- SELECTED: after actual trigger crossing; record the route and close alternatives immediately.

Evaluate route connection first, then persistent availability. If both fail, show ROUTE_LOCK only. A remembered C path can remain sealed by the previous route. Newly earned unlocks do not modify the current run's snapshot.

Never reuse a trigger as the physical blocker: a trigger is passable. [Unity's trigger-collider documentation](https://docs.unity3d.com/6000.6/Documentation/Manual/collider-interactions-create-trigger.html) covers Is Trigger and physics-body requirements for ordinary collider pairs. CharacterController interaction is specialized: verify enter detection with the actual controller in a disposable test scene before finalizing the route-gate prefab. Do not add a competing Rigidbody to Boki as an untested workaround. Keep triggers separated so one movement frame cannot accidentally select two routes.

Place gates at a readable fork with enough turning room, and blockers tall/wide enough to prevent jump or side bypass. Once one route is selected, all alternative entries require physical closure.

Unchosen branches also require opaque physical rear seals at their merge into the shared ending. Open only the selected branch's rear seal; suppress unchosen interior rendering while retaining blockers and visible seals. Keep unchosen enemies and charges inactive. Arena entry closes a solid entrance seal, suppresses approach rendering, deactivates approach encounters and replenishment, and ends Guilt pursuit. Both boss charges stay inside. New Run restores scene visibility through reload.

## 7. Scene set pieces

### Bellevue and Pohorje

Place the prologue within Level1_Pohorje, using camera/prop markers. Intro skip must leave Boki at the same safe approach without stale held input. The scene starts with A/B available and C locked on a first run.

A emphasizes tree slalom and melee XP. B uses jumps/maintenance obstacles, less combat and a static gondola; no operating gondola system is needed. C is a shorter dark tunnel with the grate/Guilt set piece and one guaranteed battery or XP pickup. Author two stronger regular enemies through balancing data rather than inventing a new AI species.

Leaving the shared forest exit persists Forest C for the next run, once. The watch remains 07:58.

### Maribor

Keep actual riding geometry limited to the shared start, short branch variants and common exit. Three facade models, a car family, a flat water plane and a bridge silhouette supply the setting.

A has two larger melee groups and a car-ramp escape. B has narrow riverside lanes and ranged Watchers; rail/bank collision prevents unintended water access. C has a closing entry door, a far-end Guilt reveal and a **short retreat to a side escape opening**. That retreat is an in-route set piece, not a second A/B/C selection and not a return to the previous scene. Make its escape path reconnect to the shared end.

Leaving the city persists City C for the next run. End with FERI signage and the watch transition toward 07:59.

### FERI and the classroom

Use repeating doors, sparse fluorescent emitters and courtyard/service variants. Use ramps where the illustration suggests steps. Keep two simple floor traps and a reachable optional POWER OFF panel on E_C.

Every variant reaches the same classroom landing. ArenaEntry persists FERI C for the next run and starts the same boss once. Guilt dissolves/deactivates as a pursuer and becomes decorative shadow art.

Professor setup: A is standard; B adds two melee enemies once at half HP; C starts at 85% HP only if the panel was used. The arena uses only Exam Barrage (three slow sheets) and Deadline Slam (0.8 s red warning). The ring's rendered size and actual hit area must agree.

## 8. Camera, collision and readability pass

- Test the actual Boki model envelope in both standing reference and riding poses; preserve movement tuning during the art pass.
- Clear all route lanes of decorative roots, planter edges, pipe supports and door leaves.
- Use smooth collision on ramps/curbs, even when the render mesh is faceted.
- Check turns and fork selection while moving, boosting and jumping. Do not infer jumpable gap widths from artwork.
- Test the existing follow camera at corridor corners, door headers and the arena entry. Separate cutaway illustration roofs from actual camera and collision policy.
- Confirm every physical lock cannot be bypassed through an adjacent scenic surface.
- A 1.1 m fence or 1.2 m railing is not a certified route barrier: the player may jump it. Use visually justified high banks, walls or overgrowth at bypass points and test with the actual jump/boost controls.
- Do not create new invisible-death rules for scenic cliffs or water; use boundaries consistent with the intended gameplay.
- Show danger on Low settings without depending on bloom, grain or shadow quality.

## 9. Performance and pacing

Honor the source targets: URP Forward, Windows x86_64 at 1280 × 720 Low, at least 30 FPS measured on Ryzen 5600G/Radeon. Enemy caps are 8 / 12 / 8; only the FERI boss is additional. These are targets, not verified results from this design pack.

Reuse assets and limit visible distance. Use one main directional light and only a few active local lights; emissive lamp meshes are not a request for a dynamic light on every fixture. No volumetric fog, ragdolls or dense transparent vegetation are required. Keep flat water and facade interiors simple. Test the city horde, boss and most heavily lit area in a Windows player build.

Pacing targets stay 2–3 min forest, 3–4 min city, 1–2 min FERI approach and 1–2 min boss, plus skippable prologue. Start with a compact graybox and tune encounter spacing and route travel length. Whole-level image proportions and bay counts do not validate total run time. First-run forced continuations are intentional and must not be disguised as extra open choices.

## 10. Manual acceptance checks for the later assembly

- [ ] Every selected route reaches a usable combat pocket; leaving needs no kill quota, while City C's side-door event remains required.
- [ ] Replenishment obeys desired counts, level caps, cadence, offscreen distance, occupancy, and pause rules from Gameplay §5.
- [ ] Three charges per regular pocket (Lent B4 recorded as a proposal), two equivalent boss charges; full/empty/pause/reentry behavior works.
- [ ] Lingering escalates Guilt by time; authored events suspend ambient pursuit, and boss entry ends it.
- [ ] Unchosen interiors are hidden with front/rear blockers retained; arena seals and approach deactivation work.

- [ ] Correct Editor; one-meter calibration and unit-scale modules.
- [ ] Seamless floor/ramp joins; no unintended raised lip or visible mesh/collision mismatch.
- [ ] One shared start/fork/end per scene; no nine-scene split or extra boss arena.
- [ ] Canonical route matrix is reflected by physical gates in every run-start unlock combination.
- [ ] First-run F_B only permits City A; first-run C_B only permits FERI B.
- [ ] Lock precedence and messages are correct; locked triggers never select.
- [ ] Entering a chosen fork immediately records it and closes alternatives.
- [ ] Unlock persistence occurs at forest exit, city exit and arena entry, applies next run only, and survives death.
- [ ] C underpass retreat/side escape is connected and never a dead end.
- [ ] POWER OFF is reachable and only its actual activation applies E_C's modifier.
- [ ] Camera, boosts, jumps and collision work across ramps, doors, cars and tunnel corners.
- [ ] Only two regular enemy AI families, capped per scene; Guilt chase is off during boss.
- [ ] One professor arena; correct two attacks and route modifiers.
- [ ] Full successful run and death/new-run cycle preserve/reset correct data.
- [ ] Windows player measured on target hardware; pacing checked across first and later runs.

These checks remain pending until assets and gameplay are assembled in Unity. No existing scene was edited or run for this reference-pack task.
