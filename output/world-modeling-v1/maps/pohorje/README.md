# Pohorje — large environment build

Open [Source/Pohorje_Large.blend](Source/Pohorje_Large.blend), scene **Pohorje_Large**. Earlier modeling scenes remain available for comparison. The existing Unity scenes and prefabs are untouched.

This build expands the approved uncanny low-poly direction into the complete Pohorje route layout. The original SVGs govern topology and beats; the larger dimensions below are construction proposals responding to the request for fast movement and roomy fighting. Canonical combat, gate and persistence rules remain unchanged.

## Map and views

- [Meter-scaled SVG map](Pohorje_Large_Map.svg)
- [Whole environment](Previews/01-whole-pohorje.png) · [Roof cutaway](Previews/10-cutaway-map.png) · [Top-down render](Previews/11-top-down.png)
- [Bellevue](Previews/02-bellevue.png) · [Forest traversal](Previews/03-forest-traversal.png) · [Forest fighting space](Previews/04-forest-combat.png)
- [Gondola service route](Previews/05-gondola.png) · [Service fighting space](Previews/06-service-combat.png)
- [Rave tunnel](Previews/07-rave-tunnel.png) · [Rave fighting space](Previews/08-rave-combat.png) · [City overlook](Previews/09-city-overlook.png)

## Space and route design

The terrain covers **384 × 576 m**, with scenic silhouettes beyond its edges. Bellevue leads through a safe approach into a graded decision room with three nearby gate assemblies. Separate branch links lead to traversal, a combat pocket, rear seal, shared merge, and city overlook. Rounded centerlines reduce sharp scooter turns. Rock banks protect the lanes and pocket perimeters; geometry guides describe the required collision.

| Selected route | Approximate full route centerline | Main clear lane | Fighting area |
| --- | --- | --- | --- |
| A / Gozdna strmina | 616.8 m | 8 m | 64 × 60 m |
| B / Gondolska servisna pot | 622.9 m | 10 m | 60 × 56 m |
| C / Rave tunel | 584.3 m | 8 m | 48 × 44 m |

Centerline lengths include shared sections and travel through the pocket; they do not measure actual player paths or establish pacing. At the documented M1 6 m/s baseline, uninterrupted centerline travel would be roughly 97–104 seconds, before fighting and decisions. Confirm the 2–3 minute forest target with the actual controller and encounters; boost and shortcuts within the rooms change this considerably.

A emphasizes spruce slalom, logs, and the largest fighting floor. B has broad switchbacks, static gondola silhouettes, abandoned carts, broken-board tests, and a Watcher anchor. C remains shorter, with a roofed descent, large rave room, two authored regular-enemy anchors, and a side grate/reveal alcove. All three reach one overlook. Pockets have three fixed charge locations each, candidate spawn/Guilt anchors, open exits, and no kill-quota mechanism. C Charge 1 remains guaranteed. Central fighting floors are intentionally clear; clutter sits at the edges.

## Low-poly and performance choices

The build has **138,686 render triangles across all three routes**, excluding collision guides, the unused module library, and any gameplay characters/effects. There are **2,014 source forest placements**, baked into **206 spatial chunks** rather than thousands of separate tree objects. Terrain uses 64 m chunks; forest uses 32 m cells. There are 639 render mesh objects before Unity batching/culling.

Spruces use 64 triangles each; rocks 26; dead trees 24; poles/cabins 36; carts 48. Foliage is opaque. Six original 128² textures repeat across shared materials. Tunnel wall and roof shells have thickness so they render from inside and outside with ordinary backface culling. There is no volumetric fog, transparent grass field, operating gondola, unique texture per prop, or distant full city. Blender presentation uses one sun plus seven local lights; Unity should activate only the lights needed near the selected route.

These are geometry budgets, **not measured target-hardware performance**. Apply sensible view distances, chunk culling, selected-branch visibility, and shadow limits in Unity. Measure a Windows 1280 × 720 Low player build on the Ryzen 5600G. Blender renders do not establish that result.

## Source, export and collision structure

- `Source/Pohorje_Large.blend`: editable source, active world camera, separate route/shared collections, removable C roofs, module library, marker collection, and hidden collision guides.
- `Export/`: individual reusable module FBXs, separate shared/route/terrain/forest/roof assembly FBXs, and separate collision-guide and gameplay-marker FBXs. Export preserves separate objects instead of welding the entire level.
- `Textures/`: original texture PNGs; they are also packed into the source and embedded in FBX.
- `layout.json`, `markers.json`, `forest-instances.json`: route geometry, semantic markers, and original instance placements. Coordinates are Blender XYZ meters, Z up; FBX exports use Y up/-Z forward.
- `collision-guides.json`: proposed primitive boxes with dimensions, positions, rotation and route ownership. Hidden wire meshes are guides, not automatically installed Unity colliders. Dynamic gate blockers remain closed in this guide collection until runtime state is assigned.

Travel-surface guides follow the actual graded ribbon geometry. Curved downhill surfaces need a suitable static collision surface because one flat box cannot represent them accurately. Pocket floors, walls, banks, carts, logs and gates have primitive guidance. Chunked forest is scenic outside the banks and has no collider per decorative branch/tree. Gate leaves are parked above their frames in the authoring view; `closed_z` records the required closed position. All three branch interiors are visible for editing, rather than pretending this is a runtime selection state.

## Verification

`validation.json` records actual geometry and export checks:

- **1,683 lane samples:** no unsupported travel surfaces, height mismatches over the checked tolerance, or terrain intrusions. Wide polygons were split at grade transitions to remove a landing seam.
- **428 static collision stations:** no point at which all three sampled lane tracks were obstructed, using a 0.35 m capsule margin. Dynamic gate blockers were intentionally excluded. See `collision-sampling.json`.
- No zero-area render faces or FBX material warnings. Source modules have unit scale and closed manifold components.
- Blender FBX reimports of A, C, and the shared fork preserve their respective 59, 101, and 11 mesh objects with no empty material slots. See `roundtrip-validation.json`.
- Overview, riding-height, fighting-space and tunnel renders were inspected.

These samples do not prove complete collision, route isolation, camera comfort, enemy navigation, jump feasibility, or balance. The broken-board art retains a continuous safety surface until jump testing defines a proper gap and landing. The scenic shell is not a certified play boundary. Bellevue/overlook edges, gate bypasses and narrow bank joins still need explicit controller tests. No death pits, swimming, checkpoint, puzzle, new AI species or kill-locked pocket was added.

## Pending Unity owner work

Import through **Unity 6000.6.7f1** so the Editor generates `.meta` files. All delivered assets remain outside `Game/Assets`; **Unity import and metadata generation are pending**. Map embedded color textures to URP materials and verify scale/orientation against Boki before assembly. Agree ownership before modifying the production scene.

Create a disposable movement test, assign primitive colliders/static travel surfaces from the guides, and test seams, bank joins, gates, zero-battery access, boost, jump, camera and pocket turns. Bind the front blockers, separate selection triggers, opaque rear seals, and route visibility roots to the existing route systems. Only the selected branch opens at its rear; unchosen enemies/charges stay inactive and their interiors hidden. C uses the run-start unlock snapshot, and ExitForest discovers it for the next run. Keep ambient Guilt separate from authored events. Bind the three pocket charges, occupancy volumes and offscreen/valid-ground spawn candidates; markers alone do not implement these systems.

Windows performance, Unity material import, full controller traversal, interaction wiring and pacing remain unverified. Lighting and sky in these renders are presentation settings, not a finished Unity lighting/postprocess setup.

## Rebuild

Run `build_pohorje.py` in Blender; it creates a separate scene and refuses to replace an existing `Pohorje_Large`. It calls `grade_surfaces.py` to preserve graded seams. Run `finish_pohorje.py` to validate, export, render and save the current source; `verify_collision.py` samples the static guides. Run `python3 draw_map.py` outside Blender to regenerate the meter-scaled SVG from the manifests. Later user edits should be preserved before any rebuild.

References: [original topology](../../floorplans/forest-topology.svg), [Pohorje sheet](../../figma/02-pohorje-layout.svg), [module sheet](../../figma/03-pohorje-modules.svg), [combat overlay](../../../gameplay-mocks-v2/HANDOFF.md), [canonical gameplay](../../../../docs/Gameplay.md), [story](../../../../docs/Story.md).
