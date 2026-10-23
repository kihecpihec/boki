# Pohorje world art blockout — approved restrained style

Open **ENV_Pohorje_World.blend**, scene **Pohorje_World**. Earlier first-pass and restrained-style scenes remain available for comparison. Existing Unity scenes/prefabs were not edited. This is an expanded environment art blockout with all three routes, not a wired or playtested Unity level.

## Included

- Bellevue start: bench, bottle, fence, safe approach, intro/player/scooter placement markers.
- Shared fork: three signs, individual entrance frames/leaves and selection markers.
- A: winding dirt path, spruce/rock perimeter, logs, combat clearing, and authored Guilt marker.
- B: wider service path, static gondola poles/cabins/cable, overturned maintenance cart, broken-plank art, Watcher marker, and combat clearing.
- C: concrete descent with low-resolution texture, rave posters and purple strips, widened room, side grate opening, stronger-enemy/Guilt markers, and removable roofs.
- Shared end: separate branch rear seals, merging paths, overlook/fence, skyline silhouettes, and ExitForest marker.
- Three **20 × 18 m** combat pockets using the companion spatial plan; each has three charge housings and charge/spawn/Guilt markers. These dimensions and positions remain playtest proposals.
- Eight individual module FBXs plus separate assembly FBXs for the shared sections, each route, scenery, and roofs. Meshes remain separate objects, not a welded whole-level export.
- Original 128² texture PNGs, six rendered views, marker manifest, and validation reports.

The edit view illustrates **A selected**: A front/rear leaves are hidden, B/C leaves are visible. All branch interiors stay visible for authoring. This is a static presentation state; it does not implement branch selection, runtime visibility suppression, or lock logic. Exported A contains its hidden leaves so the scene owner can bind them.

## Viewing

1. [Whole world](Previews/01-world-overview.png)
2. [Bellevue](Previews/02-bellevue.png)
3. [Forest A at riding height](Previews/03-forest-A.png)
4. [Service B at riding height](Previews/04-service-B.png)
5. [Rave C at riding height](Previews/05-rave-C.png)
6. [Roof cutaway](Previews/06-cutaway-overview.png)

Hide `PH03_C_RemovableRoofs` to inspect the tunnel/room. Module library is excluded from renders. `PH03_GameplayGuides_NOT_EXPORT` contains **65 empty transforms**, listed with Blender XYZ positions in `markers.json`. Blender Z is vertical; FBX specifies Y up/-Z forward. Guide transforms are separate from render exports; transfer them intentionally and verify orientation in Unity.

## Validation and limitations

`module-validation.json`: all eight source modules have unit scale, no zero-area faces, and no nonmanifold edges. Spruce is 64 triangles; rock 26. Ground/ribbon meshes intentionally have open edges and are excluded from closed-module checks.

`export-validation.json`: final assembly export produced zero material warnings. `roundtrip-validation.json`: Blender FBX reimport preserved Route A's 62 and Route C's 74 mesh objects with no empty material slots. This verifies those Blender roundtrips, not a Unity roundtrip or all target-platform behavior. Renders were inspected and tunnel roof gaps corrected. A/B decorative trees and rocks were moved outside their proposed lane envelopes; this does not replace actual collision/camera tests.

Geometry is deliberately simple: bench/cart/cabins are first-pass props, concrete reuses the original stone texture, and posters are minimal lettering/planes. Path shoulders, skyline composition, terrain boundaries, and environmental storytelling can receive further art polish. The broken-plank art currently sits over a continuous travel surface: a real jump gap and its landing require controller testing before removing support. There are no invented lethal pits or fall rules.

Scenic terrain, ridge banks, gate leaves, charge housings, and encounter markers have **no working Unity colliders/scripts**. Open ground around gates is not a certified route barrier. Route isolation requires collision boundaries spanning the surrounding terrain plus opaque rear seals. Roof camera clearance and the C grate sightline require in-engine testing. No enemy/Guilt animation, prologue sequence, fog/postprocess, runtime lighting, or audio is implemented here.

## Unity owner handoff

Import into **6000.6.7f1**, let Unity generate `.meta` files, and verify calibration/axis/material mapping before integration. Files remain outside `Game/Assets`, so Unity metadata/import are pending. Use the individual kit assets for prefabs; assembly exports are optional placement references. Embedded texture materials may need mapping to URP materials. Blender's directional light/two tunnel lights are presentation only and are not part of the FBX mesh assemblies.

- Agree scene ownership before editing the production scene. Set up a disposable movement test first.
- Add primitive trunk/rock/cart/gate/terrain-boundary colliders and continuous smooth riding surfaces. Use custom mesh collision only where primitives cannot represent required traversal. Check every ground/pocket/merge seam.
- Bind A/B/C entrance leaves, rear seals, separate selection triggers, and branch visibility roots. C availability uses the run-start snapshot; ExitForest persists discovery for the next run. Do not infer gameplay state from Blender visibility.
- Bind charge objects, combat occupancy, authored enemies/events, and valid offscreen spawn/Guilt candidates. C Charge 1 is its guaranteed charge. Pocket exits remain available without a kill quota.
- Test at zero battery; test turns, boost, jumps, camera, gate bypasses, grate reveal, transitions, and first/later-run choices. Tune path lengths and encounter timing for 2–3 minutes; the compact geometry alone does not establish that duration or the safe-section timing.
- Measure Windows 1280 × 720 Low performance on Ryzen 5600G. No FPS or compatibility claim is established by the Blender renders.

## Reproduction

Run `build_world.py` inside the v2 `.blend` to create a new world scene; it refuses to overwrite an existing `Pohorje_World`. `finalize.py` reexports current assembly collections and renders six views without rebuilding geometry. Finalize preserves source linked meshes/visibility and temporarily separates export mesh data to avoid material remapping warnings. Individual module exports are created by the build script.

Sources: [world handoff](../world-modeling-v1/UNITY_WORLD_HANDOFF.md), [module catalog](../world-modeling-v1/MODULE_CATALOG.md), [combat-space proposals](../gameplay-mocks-v2/combat-space-plans.json), canonical [Gameplay](../../docs/Gameplay.md) and [Story](../../docs/Story.md).
