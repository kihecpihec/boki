# Pohorje — compact action layout

Open [Source/Pohorje_Large.blend](Source/Pohorje_Large.blend), scene **Pohorje_Large**. This replaces the long traversal layout with short branch entrances and three denser, distinct arenas. Existing Unity scenes/prefabs and the user's live Blender session are untouched. The superseded `output/pohorje-build-*` directories remain deleted.

## Get to the action

The safe ride is **8 m**. Three nearby, signed gates lead toward encounter candidates **5 m beyond selection**. From the player start, these candidates are about **23–24 m away**, approximately **4 seconds** of uninterrupted baseline travel. Full arena entrances are **46–68 m away**, approximately **8–11 seconds**. These are authoring distances; encounters still need binding in Unity.

| Route | Full centerline, previous → current | Start to arena | Arena footprint |
| --- | --- | --- | --- |
| A / Gozdna strmina | 552.9 → 199.2 m | 67.7 m / 11.3 s | 56 × 64 m |
| B / Gondolska servisna pot | 534.8 → 198.4 m | 67.7 m / 11.3 s | 52 × 60 m |
| C / Rave tunel | 486.4 → 130.0 m | 46 m / 7.7 s | 48 × 52 m |

Full routes are **64–73% shorter than the previous delivery** and include crossing the arena and reaching the shared exit. The main lanes have a consistent **9 m** clear surface. The arenas were deliberately tightened from the preceding 80 × 96 / 75 × 89.6 / 60 × 70.4 m rectangles: useful space, landmarks and crossing choices replace empty perimeter acreage. C remains the shorter secret branch. The scenic footprint is **230 × 162 m**.

The canonical 2–3 minute forest target depends on fighting, decisions and lingering; uninterrupted travel time does not establish encounter pacing. The gameplay rules, lock priority, next-run C unlocks and open arena exits remain unchanged.

## Three distinct spaces

**A — logging hollow.** A raised rocky shoulder and a low channel create different riding lines around a central stone island. A broad timber bridge crosses the channel, with approach ramps at both ends. Snapped trunks support a large fallen spruce overhead, making a recognizable landmark without blocking the riding loop. Log stacks and irregular rock rims break up sightlines and edges.

**B — gondola maintenance yard.** A raised concrete terminal has two broad ramps and a route through its supporting legs. Two red gantries, suspended static cabins and cables make the service space recognizable from its entrance. Carts and uneven ground divide the perimeter into smaller pockets while leaving room to circle the terminal.

**C — cistern rave.** A short roofed entrance reveals a sunken dance floor, four concrete columns, a raised DJ stage reached by side ramps, large speaker towers and floor/column emission. The east wall has an actual grate opening with a blocked reveal alcove behind it. Removable roofs allow editing and inspection. The stage leaves an independent route to the rear exit.

All floors are connected terrain, with flat entrance/exit aprons and optional elevated landmarks. Arena terrain slopes peak around **35.2°**. The bridge, terminal and stage ramps have separate static surface guidance. Geometry checks do not prove controller or enemy navigation behavior.

Three charge positions per arena are grounded to the current floor; C Charge 1 remains guaranteed. Offscreen spawn/Guilt candidates, authored A/C reveals, the B Watcher and early selected-route encounter candidates are semantic markers. No marker spawns enemies or runs gameplay on its own.

## Pictures and exploration

- [Whole layout](Previews/01-whole-pohorje.png) · [Roof cutaway](Previews/10-cutaway-map.png) · [Top-down](Previews/11-top-down.png)
- [Logging hollow layout](Previews/16-forest-arena-layout.png) · [Gondola yard layout](Previews/17-service-arena-layout.png) · [Cistern rave layout](Previews/18-rave-arena-layout.png)
- [Forest entrance](Previews/03-forest-traversal.png) · [Forest arena](Previews/04-forest-combat.png) · [Service arena](Previews/06-service-combat.png) · [Rave arena](Previews/08-rave-combat.png)
- [Meter-scaled annotated map](Pohorje_Large_Map.svg) · [Blender walk controls](WALKTHROUGH.md)

The saved viewport starts at eye level at Bellevue. Hover over it, press F3, search **Walk Navigation**, then use the mouse and W/A/S/D. The overview views use clearer lighting for spatial review; the roofed rave view retains its local lighting. Blender navigation is not Boki's controller.

## Delivery and verification

`Source/` contains the editable Blender environment. `Export/` contains current route/shared/terrain/forest/roof FBXs, unchanged-size reusable modules, collision guides and gameplay markers. Existing packed pixel textures are reused. JSON uses Blender XYZ meters with Z up; FBXs use Y up/-Z forward.

Current reports:

- `layout.json`: paths, dimensions, timing estimates and landmark positions.
- `validation.json`: actual render budgets, zero-area faces, sampled travel support/terrain clearance and export warnings.
- `arena-revision.json` and `arena-sampling.json`: floor dimensions/heights/slopes, render/guide agreement, underlay clearance, entry/exit aprons, nine charge locations and six ramp landing/slope checks.
- `collision-sampling.json`: three-track static obstruction samples, excluding dynamic gates and walkable surface meshes.
- `roundtrip-validation.json`: representative A/C/shared-fork FBX reimports and material slots.

The source has **123,177 render triangles across 307 mesh objects**, across all routes. Foliage is opaque and baked into **61 spatial chunks from 620 placements**. The environment uses one sun and five presentation point lights; no animated gondola, volume fog or per-tree collider is required. The previous style/approach preservation reports are historical and do not describe the rebuilt map.

## Pending Unity owner work

All assets remain outside `Game/Assets`. Import using **Unity 6000.6.7f1** so the Editor generates `.meta` files; import and metadata generation are pending. Assign URP materials, check scale against Boki, and obtain the scene owner's agreement before production scene edits.

Use primitive boxes/capsules for walls, gates, rock islands, logs, carts and other simple obstacles. `collision-guides.json` describes suggested shapes; it does not install colliders. Curved terrain, ramps and bridge surfaces need static surface collision. Opaque room rims and branch gates still require a controller test for bypasses and jumps. Decorative foliage is scenic.

Bind front gates, independent selection triggers, rear seals and selected-route visibility. Keep unchosen branches, enemies and charges inactive. Place the first regular encounters at the early candidate positions, within the existing enemy caps. Bind the three charges, combat occupancy covering the floor height (proposed 5–16 m range), valid-ground/offscreen spawn candidates, and authored events. C discoveries still apply on the next run. Do not add a kill quota.

Test the bridge and six ramps, camera clearance under the terminal/fallen tree, pits, charges, zero-battery access, boost, jumps, spawn validity, navigation, route isolation and encounter pacing. **Unity/controller tests and Windows Ryzen 5600G performance are unverified.** Measure a Windows 1280 × 720 Low player; Blender geometry checks do not establish runtime performance.

## Rebuild and edit

`build_action_layout.py` constructs this revision from the delivered module library and packed materials. It refuses to overwrite a source already marked `action_layout`. Preserve later artist edits before any deliberate rebuild. The older base/style/approach/reshape/dressing scripts target superseded layouts and must not be applied to this delivery.

After source edits, run `finish_pohorje.py`, `verify_arenas.py`, `verify_collision.py`, `verify_roundtrip.py`, and `setup_walkthrough.py` in Blender. Use `--python-exit-code 1` for batch verification. Run `python3 draw_map.py` outside Blender and update the ZIP. Keep the layout and semantic marker manifests consistent with geometry edits.

References: [canonical gameplay](../../../../docs/Gameplay.md), [story](../../../../docs/Story.md), [original topology](../../floorplans/forest-topology.svg).
