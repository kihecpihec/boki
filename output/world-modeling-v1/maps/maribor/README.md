# Maribor — lit diorama pass

Open [Source/Maribor.blend](Source/Maribor.blend), scene **Maribor**. This replaces the first, flat box-and-ring pass. It follows the Figma Maribor mockup (red dusk, terracotta roofs, lit lamps, the Drava, a basin with a bridge behind it, a concrete underpass block, the FERI sign at the end) and the Pohorje delivery's construction style (2–4 px clump textures, flat-shaded low poly, a framing ridge of hills with Pohorje's own spruces, packed materials, separate route collections). Nothing in `Game/Assets`, Pohorje or any Unity scene was modified.

## References

- [Figma Maribor layout](../../world-modeling-v1/figma/04-maribor-layout.svg) and [module kit](../../world-modeling-v1/figma/05-maribor-modules.svg) — facade, car, jersey barrier, lamp, rail, riverbank, bridge arch and underpass modules; palette (`#B86742` sky, `#263B49` asphalt, `#795849` brick, `#172734` water, `#BA86EA` secret).
- [City topology](../../world-modeling-v1/floorplans/city-topology.svg) and [combat overlay](../../gameplay-mocks-v2/sheets/city-combat-plan.svg) — shared arrival, filtered fork, three combat spaces, rear seals, shared FERI exit.
- [Pohorje delivery](../../world-modeling-v1/maps/pohorje/README.md) — build style, collections, markers, FBX conventions. The three `ENV_F_Spruce` FBXs from its `Export/` folder are imported for the foothills; nothing in the Pohorje folder is changed.
- [Gameplay §§2–5](../../../docs/Gameplay.md) and [Story §3](../../../docs/Story.md) — authoritative route and narrative rules.

## What is built

The whole level is one island in the Drava, about **154 × 204 m** plus backdrop. A stone-and-rock plinth stands in the water, with an inlet basin cut into it that opens to the river under a three-arch causeway.

**Shared arrival and FERI.** A cobbled plaza with a fountain and planters sits in front of a terraced landmark hall (pavilion, pediment, clock tower, lit windows) after the Figma's institutional building. A lamp-lined promenade carries the three route gates, each with a sign, two stone posts and an opaque closed gate mesh. All routes merge on a causeway and end on the FERI pier with an arrow sign.

**A / Tabor.** A curving 8 m street with cobbled sidewalks and facade rows on both sides. Facades are 4 m bays, **8 m deep**, with gabled terracotta roofs, plinths, sills, door steps, shutters and chimneys. Windows are a mix of lit and dark. A car slalom, jersey barriers, bins and a bare tree lead to a 24 × 28 m street court with planters, an overturned car and a separate 1.4 m car ramp. Between A and B are a park and two double-bay houses.

**B / Lent.** Two 3 m lanes run around raised planters (poplars and lamps on top) and meet in a 22 × 24 m cobbled quay with an inlay. Continuous rails, a hedge and a riverside ledge with lamps run down the basin side. The water carries lamp reflections. Watchers are semantic markers only; no enemy models.

**C / Podzemni prehod.** The concrete gallery, closing entrance, far-end Guilt reveal, 4 m side aperture, escape gallery and 18 × 20 m pocket are the same validated geometry as the first pass. New: purple-lit gate frame and banner, hazard stripes, fluorescent strips, pipes, a red-lit roller door, an ember glow at the Guilt reveal, crates in the pocket, solid caps over the structural voids so it reads as one block from outside, and a service yard (containers, parked cars, lamps) on its east side. Roofs stay in `MB_C_Removable_Roofs`.

**Backdrop.** A south-bank city of 8 facade variants scaled up in two rows with church spires, a 10-arch stone viaduct, foothills with Pohorje spruces, lit windows and red lamp reflections on the water. The sky is a dusk gradient with one low warm sun, 24 lamp point lights and the underpass review lights. None of the lights are exported.

Each combat pocket has three fixed charge props, six regular spawn candidates and three offscreen Guilt candidates. These are authoring transforms; nothing spawns or enforces visibility. Spawn positions must still be filtered for valid ground, sightlines and controller clearance.

Entry and rear seal meshes are **opaque and closed by default** and remain separately named for gameplay binding. They carry no gate logic.

## Files

Open [the preview gallery](index.html) for nine views: overview, arrival, Tabor street and court, Lent lanes and quay, underpass, roofless cutaway and FERI exit.

- [Annotated meter-scaled map](Maribor_Map.svg).
- `Export/`: `MB_Shared_Arrival_FERI`, `MB_Route_A/B/C`, `MB_Scenery`, `MB_C_Removable_Roofs`, `MB_Gameplay_Markers`, `MB_Collision_Guides`, plus reusable modules (8 facades, 2 cars, streetlamp, 4 m rail, jersey barrier, bin, 3 trees and a bush).
- `Textures/`: 22 opaque pixel textures (64–256 px), also packed in the Blender source and embedded in the FBXs. No downloaded or generated artwork is used as geometry texture.
- `layout.json`, `markers.json`, `collision-guides.json`: Blender XYZ meters, Z up; FBX exports use Y up / -Z forward. Collision guides now carry a `rotation_z` in radians (curved streets use rotated boxes).
- [validation.json](validation.json): measured triangles, zero-area faces, sampled floor and clearance results, charge placement checks and representative FBX reimports.
- `mb_kit.py` (textures, materials, primitives, path ribbons) and `build_maribor.py` (scene).

## Verification

The source has **151,378 render triangles across 532 mesh objects** (Pohorje is 123,177 across 307). Everything is static, opaque and combined by material set. All **1,609 path samples** passed floor, proposed blocker and four-direction standing-clearance checks against actual render geometry. All nine charge positions passed support and clearance. Representative A and C FBX reimports matched triangle counts and meter bounds within 0.00004 m. No zero-area faces remain. These checks exclude dynamic gate blockers and do not establish controller behavior.

**Not verified:** Unity import, controller feel, enemy navigation, camera clearance and any Windows or Ryzen 5600G performance. Blender triangle counts do not establish the runtime target. About 85k of the triangles are scenery (hills, bank houses, spruces) that could be reduced or replaced with a skybox/impostors if the Windows 1280 × 720 Low test needs the headroom.

## Integration and remaining work

No `.meta` files exist and nothing is imported into Unity; import with the pinned **Unity 6000.6.7f1** and let the Editor generate GUIDs. Obtain scene ownership authorization before wiring production assets.

1. Assign URP materials, set nearest filtering on pixel textures, scale-check against Boki, and disable automatic mesh collider generation. Use the guides as references for BoxColliders. Curved street/lane surfaces, the court ramp and the causeway need static surface collision.
2. Bind City gate availability to the saved Forest choice and the run-start unlock snapshot, following the Gameplay matrix and lock priority. Completing City unlocks C only for the next run. Bind each entry gate, selection trigger and rear seal; selection immediately closes the alternatives and suppresses inactive branch rendering and encounters while keeping their physical blockers.
3. Bind C's closing door, far-end reveal and mandatory side-door escape. A zero-battery controller must still reach the escape and charges.
4. Bind combat occupancy, replenishment, the 12 regular-enemy cap and fixed +25 charges. Lent still needs extra recovery opportunities beyond three. No kill quota at arena exits.
5. Bind one-way `ExitCity` and the FERI event. Test sidewalk/curb edges, slalom clearance, ramp landing, narrow lanes, the side doorway, and jump or bypass resistance at every rail, planter and seal. Rails and hedges are boundary art backed by tall invisible box proposals that need controller testing.

## Rebuild and edit

Built with Blender **4.4.3**: `blender --background --factory-startup --threads 6 --python-exit-code 1 --python build_maribor.py`. The builder refuses to overwrite `Source/Maribor.blend`; preserve artist edits and use a fresh copy of the folder before deliberately rebuilding. `MB_DEV=1` builds to `/tmp` without exports; `MB_PREVIEWS=1,3` renders only those cameras; `MB_SAMPLES` and `MB_RES` set quality. Run `blender --background Source/Maribor.blend --python verify_maribor.py` for the checks and `python3 draw_map.py` for the annotated map. Edit the builder and manifests together; do not treat validation or previews as current after manual Blender edits. The Pohorje `.blend` was saved by a newer Blender than 4.4.3, so only its FBX exports are used.
