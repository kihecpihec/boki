# Maribor — lit diorama pass, horror and overlap revision

**Third pass.** This revision removes overlapping geometry and gives the city the dread the Story asks for. See [Overlap fixes](#overlap-fixes) and [Horror pass](#horror-pass). The sections below describe the layout, which is unchanged except where noted.

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

## Overlap fixes

The earlier pass let 8 m deep facade bays collide on the bend of Tabor street (1–4 m of penetration), sank the route C gate posts into the tunnel walls, put lamps on rails, stacked two containers into each other and left several coplanar faces that flicker (z-fight).

- **Tabor street** is a gentler bend (radius about 45 m or more). `bays_along` tests each bay against its neighbours and slides on when it would sink in more than 0.2 m. The south bays stop short of the merge-promenade rail. The row on each side now has fewer bays (38 `FacadeBay` guides against 44).
- **Props check the collision guides.** `lamp_at` and `tree_at` call `clear()`. A lamp steps aside up to 0.9 m or is dropped. A tree is dropped if its trunk touches a solid or its crown reaches a facade, container or lamp (a planter keeps a bush instead). The builder prints every skipped placement.
- **Coplanar faces.** `mb_kit.coplanar_overlaps` runs at build time and reports axis-aligned box pairs with different materials that share a plane and overlap. It found the clock face flush with its tower, the quay inlay layers 1 mm apart, the gate posts flush with the gallery walls, vents flush with the roof edge and containers overlapping by 0.6 m. All are fixed; the build now reports 0.
- **Route C gate.** The stone gate posts are gone; the pillars sit beside the gallery walls and the lintel is wider.
- **Lamp layout.** The merge-row lamps moved so none stands on a route end, and ledge lamps skip the Lent quay combat floor.
- `python3 -I audit_overlaps.py` re-checks `collision-guides.json` without Blender and writes [overlap-audit.json](overlap-audit.json): **0 building overlaps** and 0 other solid overlaps. Rails, same-material concrete joints and props on the terrace slab are treated as expected. It tests footprints only; it does not find mesh-level intersections inside a module.

## Horror pass

Palette and tone follow [Story §6](../../../../docs/Story.md): orange-red city, strong fog, short view distance. The city is now dim and decayed, not a warm dusk. Collision, routes and markers are unchanged.

- **Textures** are colder and dirtier: rain streaks, rising damp, peeled plaster, moss on roofs, cracked asphalt with old stains, dead grass with bare soil and near-black water with rare red streaks. A faded rave flyer texture (`Poster.png`) is new; 23 textures are exported.
- **Facades** have mostly dark windows (20% lit, down from 50%), nailed planks over about half of the dead ones, ivy on corners and between the window rows, and torn flyers. Planks, ivy and flyers sit clear of trims and frames.
- **Lamps.** About 45% of the unlit-by-design lamps are dead (`ENV_C_Streetlamp_Dead`); the lamps with point lights stay lit. Crows (`ENV_C_Crow`) perch on some dead lamp arms and the road near the stains.
- **Trees.** Most trees became dead ones, including a new tall `ENV_C_TreeDeadTall`.
- **Street dressing.** Leaf drifts, trash bags, dried stains and drag marks (flat, no collision) on Tabor, Lent, the plaza and underpass. The tower clock is stuck at 07:58.
- **Underpass.** Only every third ceiling tube is lit, the review light is dimmer and the Guilt end glows red.
- **Lighting (previews only, not exported).** A dim red low sun, a darker blood-red sky and a bounded ground-fog volume. An unbounded world volume renders black in Cycles because sky rays never leave it. In Unity use exponential-squared fog with a dark red tint and a short distance instead.

## Files

Open [the preview gallery](index.html) for nine views (**stale in part:** views 1–5 are re-rendered for this revision at 768 × 480; views 6–9, Lent quay, underpass, cutaway and FERI exit, are still the earlier lit-diorama renders and do not show the horror pass): overview, arrival, Tabor street and court, Lent lanes and quay, underpass, roofless cutaway and FERI exit.

- [Annotated meter-scaled map](Maribor_Map.svg).
- `Export/`: `MB_Shared_Arrival_FERI`, `MB_Route_A/B/C`, `MB_Scenery`, `MB_C_Removable_Roofs`, `MB_Gameplay_Markers`, `MB_Collision_Guides`, plus reusable modules (8 facades, 2 cars, streetlamp and dead streetlamp, crow, 4 m rail, jersey barrier, bin, 4 trees and a bush).
- `Textures/`: 23 opaque pixel textures (64–256 px), also packed in the Blender source and embedded in the FBXs. No downloaded or generated artwork is used as geometry texture.
- `layout.json`, `markers.json`, `collision-guides.json`: Blender XYZ meters, Z up; FBX exports use Y up / -Z forward. Collision guides now carry a `rotation_z` in radians (curved streets use rotated boxes).
- [validation.json](validation.json): measured triangles, zero-area faces, sampled floor and clearance results, charge placement checks and representative FBX reimports.
- `mb_kit.py` (textures, materials, primitives, path ribbons, coplanar-face audit), `build_maribor.py` (scene) and `audit_overlaps.py` (guide overlap audit).

## Verification

The source has **133,164 render triangles across 449 mesh objects** (Pohorje is 123,177 across 307). Everything is static, opaque and combined by material set. All **1,609 path samples** passed floor, proposed blocker and four-direction standing-clearance checks against actual render geometry, and all nine charge positions passed support and clearance (`validation.json`, status `passed`). Representative A and C FBX reimports matched triangle counts and meter bounds within 0.0002 m. No zero-area faces remain. The build reports 0 coplanar z-fighting pairs and `audit_overlaps.py` reports 0 building overlaps. These checks exclude dynamic gate blockers and do not establish controller behavior. Checks ran in Blender 5.2.0 (the builder was written for 4.4.3).

**Not verified:** preview renders 6–9 for this revision, Unity import, controller feel, enemy navigation, camera clearance and any Windows or Ryzen 5600G performance. Blender triangle counts do not establish the runtime target. About 65k of the triangles are scenery (hills, bank houses, spruces) that could be reduced or replaced with a skybox/impostors if the Windows 1280 × 720 Low test needs the headroom.

## Integration and remaining work

No `.meta` files exist and nothing is imported into Unity; import with the pinned **Unity 6000.6.7f1** and let the Editor generate GUIDs. Obtain scene ownership authorization before wiring production assets.

1. Assign URP materials, set nearest filtering on pixel textures, scale-check against Boki, and disable automatic mesh collider generation. Use the guides as references for BoxColliders. Curved street/lane surfaces, the court ramp and the causeway need static surface collision.
2. Bind City gate availability to the saved Forest choice and the run-start unlock snapshot, following the Gameplay matrix and lock priority. Completing City unlocks C only for the next run. Bind each entry gate, selection trigger and rear seal; selection immediately closes the alternatives and suppresses inactive branch rendering and encounters while keeping their physical blockers.
3. Bind C's closing door, far-end reveal and mandatory side-door escape. A zero-battery controller must still reach the escape and charges.
4. Bind combat occupancy, replenishment, the 12 regular-enemy cap and fixed +25 charges. Lent still needs extra recovery opportunities beyond three. No kill quota at arena exits.
5. Bind one-way `ExitCity` and the FERI event. Test sidewalk/curb edges, slalom clearance, ramp landing, narrow lanes, the side doorway, and jump or bypass resistance at every rail, planter and seal. Rails and hedges are boundary art backed by tall invisible box proposals that need controller testing.

## Rebuild and edit

Built with Blender **4.4.3**, and this revision also with **5.2.0**: `blender --background --factory-startup --threads 6 --python-exit-code 1 --python build_maribor.py`. The builder refuses to overwrite `Source/Maribor.blend`; preserve artist edits and use a fresh copy of the folder before deliberately rebuilding, or set `MB_FORCE=1` to replace it. `MB_FOG` and `MB_EXPOSURE` tune the preview fog and exposure; `MB_PREVIEWS=0` skips all renders. `MB_DEV=1` builds to `/tmp` without exports; `MB_PREVIEWS=1,3` renders only those cameras; `MB_SAMPLES` and `MB_RES` set quality. Run `blender --background Source/Maribor.blend --python verify_maribor.py` (it saves the file, so work on a copy) for the checks and `python3 draw_map.py` for the annotated map. Edit the builder and manifests together; do not treat validation or previews as current after manual Blender edits. The Pohorje `.blend` was saved by a newer Blender than 4.4.3, so only its FBX exports are used.
