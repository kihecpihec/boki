# BOKI — Entire-world modeling handoff v1

Open the [visual gallery](index.html). Keep the folder intact so its reference images and sheet links work.

This pack covers **Bellevue → Pohorje → Maribor → FERI → the professor's classroom**, including all nine A/B/C variants, shared scene starts/endings, modular environment families, route gates and secret passages. It is a modeling and scene-planning reference for Blender and Unity. No `.blend`, FBX, built Unity scene or environment prefab is delivered.

The Figma Starter MCP allowance is still exhausted. The existing [Figma atlas](https://www.figma.com/design/VkUNlXhesVqCd2H8T9cOWZ) has **not** received this world pack.

## Deliverables

- `references/*.png`: nine generated environment and modular-kit reference images.
- `figma/*.svg`: a complete world-route sheet, nine illustrated construction sheets and a modular-grid/collision reference. Raster artwork is embedded; notes and diagrams are editable vectors.
- `floorplans/*.svg`: independent editable topology plans for each of the three gameplay scenes and a dimensioned classroom proposal.
- `module-catalog.json` and `MODULE_CATALOG.md`: 50 reusable model specifications with proposed extents, pivots, collision representations and triangle targets.
- `world-specs.json`: proposed construction dimensions, sheet inventory and observed player clearance context.
- `canonical-route-links.json`: a generated snapshot of Gameplay.md §3, used to draw the world-route sheet.
- `UNITY_WORLD_HANDOFF.md`: modeling, assembly, route wiring, set-piece and manual verification guidance.
- `source-prompts.json`: the image-generation prompt set.
- `index.html`: an offline gallery and resource index.

Use reference PNGs as Blender image guides. Import SVG sheets into Figma manually when file editing is available. If Figma's SVG import omits an embedded bitmap or substitutes a font, place the paired PNG in the art rectangle and use an available typeface. Figma import behavior has not been tested because the MCP allowance blocks file work.

## Sheet order

| Sheet | Purpose |
| --- | --- |
| 00 | Complete world flow and exact allowed route connections |
| 01 | Bellevue prologue, overlook and safe shared start |
| 02–03 | Pohorje layout and forest/gondola/rave-tunnel model kit |
| 04–05 | Maribor layout and street/river/underpass model kit |
| 06–07 | FERI layout and corridor/courtyard/service/classroom model kit |
| 08 | One shared professor arena, dodge space and collision planning |
| 09 | Route-gate states, secret passages and interaction separation |
| 10 | Shared construction grid, ramp and gate collision sections |

## Construction proposals versus game rules

The game rules remain in [Gameplay.md](../../docs/Gameplay.md), narrative in [Story.md](../../docs/Story.md), implementation ownership in [Architecture.md](../../docs/Architecture.md), and movement tuning in the existing project. This pack does not change them.

**New proposals:** a 1 m placement unit, 4 m structural bays, 3 m clear corridors, 3.5 m clear height, 2.4 × 3 m clear doors, a 4 m ramp with 0.5 m rise, module pivots/material sharing, marker names, and a 24 × 20 m classroom with a 14 × 12 m central clear floor.

**Observed integration context:** the current player capsule is 1.8 m high with radius 0.35 m, slope limit 45°, step offset 0.25 m. These are inspected prefab values, not promises that every proposed module is comfortable. Test them using the real player/camera.

The scene floorplans are **topology schematics**, not geographic maps or meter-scaled route lengths. Only the explicitly dimensioned module/arena drawings are to scale. Concept art may exaggerate terrain, draw imperfect arrows or omit a doorway connection. The editable route plans and game documents govern connectivity. Reconcile image details with the module dimensions before modeling.

## Scope that must survive art production

- Three gameplay scenes. Bellevue is included in Pohorje; the classroom is included in FERI.
- Each scene has one shared start, a fork into short variants and one shared ending.
- The canonical connection table still filters later gates. Do not turn the plan into nine independent environments.
- C availability uses the run-start unlock snapshot. Forest exit, city exit and boss-arena entry persist the relevant discovery for the **next** run.
- ROUTE_LOCK overrides PERSISTENT_LOCK. Locked art needs a real blocker and disabled selection trigger.
- An available fork is selected by physical crossing; alternatives close immediately.
- A single professor arena uses two attacks. B adds two melee at half HP; C reduces starting boss HP only after POWER OFF activation.
- No new swimming, falling-death, checkpoints, puzzles, operating gondolas or geographic-accuracy requirement is introduced.

## Practical build order

1. Make the shared floor, wall, ramp, corner, gate and door modules first.
2. Assemble only a shared start → A/B fork → shared ending in each graybox scene. Wire a complete playable run before dressing it.
3. Test turns, jump landings, camera clearance and collision with Boki's actual controller. Adjust dimensions as art proposals.
4. Add short C variants from the shared concrete kit, with proper snapshot/lock behavior.
5. Dress Pohorje with forest silhouettes, Maribor with three facade models and one car family, and FERI with corridor repeats and perimeter furniture.
6. Place encounter/pickup/set-piece markers and hook them to the planned gameplay systems.
7. Add sparse lighting, fog and scene transitions; run the full route checklist.
8. Measure Windows 1280 × 720 Low on the Ryzen 5600G. Reduce dressing/effects before cutting route decisions.

The level timing targets remain approximately 2–3 min Pohorje, 3–4 min Maribor, 1–2 min FERI approach and 1–2 min boss, plus the skippable prologue. These diagrams do **not** establish those durations. Tune travel length, encounter spacing and pauses through playtesting; do not infer route distance from the picture.

## Verification status

Reference art, XML/SVG structure, local links, ZIP integrity, module IDs and the generated route snapshot are checked during packaging. No Unity scene/prefab or existing character-pack file was changed. Blender mesh construction, Figma SVG import, collision/trigger behavior, run pacing, scene integration and target-hardware performance remain for the modeling/integration phase.
