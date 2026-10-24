---
name: boki-map-model-maker
description: Use when building, polishing, exporting, or reviewing a Blender map/level/environment model (terrain, route chunks, arenas, modules, props, collision guides, gameplay markers) for the Boki Unity project. Defines what a finished map must contain so it imports into Unity 6 URP and can be wired (colliders, gates, spawns, charges, visibility) without rework, and gives a pre-export checklist and validation script.
---

# Boki map model maker

Rulebook for delivering a **finished Blender map** that drops into `Game/` and can be wired in Unity without touching the model again. Game rules live in `docs/`; this skill only covers the art-to-engine contract. Link to docs, never copy rules.

Maps are static, so "rig" here means **engine-ready setup**: collision, gates, triggers, spawn/charge markers, visibility groups and LODs-by-chunk, not skeletons. (Skinned characters follow `output/character-modeling-v2/UNITY_HANDOFF.md`.)

Read first, as needed:
- Route structure and what each space must contain: `docs/Gameplay.md` §2–5, §8 and `docs/Architecture.md` §3–7.
- Player envelope: `docs/Movement.md` (capsule 1.8 m high, radius 0.35 m, 45° slope limit, 0.25 m step offset, boost/jump speeds).
- Existing worked example: `output/world-modeling-v1/maps/pohorje/README.md` and `Game/Assets/Editor/PohorjeImportPostprocessor.cs`.
- Ownership: do not edit another developer's scenes/prefabs. Importing FBX/textures into `Game/Assets/Art/<Map>/` is fine; assembling a production scene needs the scene owner's agreement.

## Workflow

1. **Brief.** Identify the level (Pohorje, Maribor, FERI), the route variants (A/B/C, shared start/fork/end), and which spaces are shared vs branch-specific. Reuse modules; do not build nine environments for nine variants (Gameplay §8).
2. **Blockout in meters.** Scene units Metric, scale 1.0. Add a 1 m cube and a 1.8 m × 0.7 m capsule proxy. Greybox every lane, ramp, gate, arena and exit and test sizes against the player before detailing.
3. **Model** to the budgets and rules below. Split into the object groups in "Structure".
4. **Add engine markers** (empties/collision guides) per "Gameplay markers".
5. **Finish**: clean mesh, UVs, materials, atlas, then run `scripts/validate_map.py` in Blender (`--background --python-exit-code 1`).
6. **Export** per "Export". Re-import the FBX into a clean Blender scene and re-run the validator.
7. **Hand off** with the template in `references/handoff-template.md`. Import in Unity 6000.6.7f1 so it generates `.meta` files. Never write GUIDs. If Unity cannot run, say the import is pending and the asset is not integration-ready.

## Finished-model checklist

### Scale, axes, origins
- [ ] Meters, unit scale 1.0, no object-level scale ≠ 1 on exported meshes (Apply All Transforms; no negative scale, no mirrored leftovers).
- [ ] Blender +Z up / −Y forward → FBX export with Y up, −Z forward ("Apply Transform" + Forward −Z, Up Y). Result in Unity: +Y up, +Z forward, no 90° root rotation.
- [ ] Walkable floor plane of each shared/entry module at Y = 0 where modules snap; seams align with no lip.
- [ ] Origins: modules at a documented snap corner/center on the floor plane; props (trees, rocks, poles, carts) at ground base center; world-placed chunks keep world coordinates with origin at (0,0,0) so they line up when imported together.
- [ ] Positions are in one shared map space across all chunk files so FBX pieces assemble without hand placement.

### Player-fit dimensions (verify against `docs/Movement.md`)
- [ ] Main lanes ≥ 9 m clear (Pohorje delivery value); tight spots still ≥ 3 m wide, ≥ 3.5 m clear height. Ceilings over riding paths clear the capsule, camera pivot (1.4 m) and jump apex.
- [ ] Walkable slopes ≤ 45° and readable ones ≤ ~35°; no steps taller than 0.25 m unless intended as a jump/ramp; ramps are real inclined surfaces, not stairs.
- [ ] Gaps/pits are either clearly jumpable at boost speed or clearly blocked. No accidental 0.1–0.4 m ledges.
- [ ] Edges players can ride off have a visible rim; kill planes/pits are marked, not implied.
- [ ] Camera clearance: no thin walls the third-person camera can clip through; no low overhangs that trap the spring arm.

### Geometry hygiene
- [ ] No non-manifold edges, loose verts/edges, zero-area faces, duplicate faces, or interior hidden faces. Remove faces nobody can see (under floors, inside walls).
- [ ] Normals consistent and outward; walls visible from both sides where players can see them from behind, otherwise single-sided with correct winding.
- [ ] Flat shading (PS1 look) or deliberate smoothing; no custom split normals surprises. Shading checked on a triangulated copy.
- [ ] Export copy triangulated; source may keep quads.
- [ ] No Boolean/modifier left unapplied, no hidden objects exported, no stray cameras/lights/empties except named markers.
- [ ] Intentional overlaps only (decorative); walkable surfaces never z-fight or overlap coplanar.

### Budgets (authoring targets, verify on Windows Ryzen 5600G, 1280×720 Low; Gameplay §8)
- [ ] Whole-map render triangles recorded. Reference: Pohorje ≈ 123k tris / 307 objects across all routes.
- [ ] Visible-at-once cost matters, not total: split into chunks so Unity can cull and so unchosen branches can be disabled (Gameplay §4).
- [ ] Foliage/props: opaque, merged into spatial chunks (≈ 10–20 m cells) or reused FBX prefabs; no per-tree collider; no alpha-cutout cards unless measured.
- [ ] Few objects, few materials: target ≤ 15 materials per map, one atlas per surface family, no more than 1–2 emission materials.
- [ ] No real-time lights baked into the FBX. Lighting is Unity's: 1 sun + few local lights, no extra shadow casters (Gameplay §8). Presentation lights in the .blend are preview-only.

### UVs and textures (PS1 / low-poly look)
- [ ] Every textured mesh has UV0, no overlapping UVs unless tile-repeating on purpose, padding ≥ 2 px at final size.
- [ ] Texel density consistent across the map (e.g. 1 tile = 4 m for ground/walls, matching the Pohorje importer). Large surfaces (ground, concrete, rock) use tiling textures; unique hero props use a small atlas.
- [ ] Textures: PNG, power-of-two (64–512 for props, ≤ 1024 for tiling surfaces), sRGB base color, painted detail, no normal maps, no transparency except explicitly justified.
- [ ] Textures live in `Textures/` next to `Export/`; materials reference them by file name; no packed .blend-only images.
- [ ] Unity filtering plan stated: Point for hard pixel art, Bilinear + aniso for tiling surfaces. Compression decision stated.

### Materials
- [ ] Materials named `M_<Map>_<Surface>` (or the project's established `Pohorje_<Surface>` set); no `.001` duplicates; no unused slots; one material slot per surface type per mesh.
- [ ] Simple Principled setup only: base color texture + optional emission. Emission is declared in the handoff because Unity must recreate it (Blender emission links do not survive as URP materials).
- [ ] Faces needing double-sided rendering are listed (leaves, fences, paper-thin sheets) or modeled with thickness.
- [ ] Vertex colors, if used for baked shading, are named and listed; the Unity material must read them or they are dropped.

### Structure and naming
- [ ] Prefixes: `PL_` placed level chunks, `ENV_` reusable modules/props, `COL_`/`*_CollisionGuides` guides, `MK_`/`*_GameplayMarkers` markers. Names are unique, ASCII, no spaces, no `.001`.
- [ ] Collections mirror Unity needs: `Shared_*`, `Route_A/B/C`, `Arena_*`, `Props`, `Foliage_Chunks`, `Removable_Roofs`, `CollisionGuides`, `Markers`. Each branch is its own exportable file/collection so it can be activated, sealed and hidden independently.
- [ ] Interiors that get hidden after route selection (Gameplay §4) are separate objects from their rear seals and entrance seals, which stay visible.
- [ ] Roofs/ceilings that block editing view are separate objects (`Removable_Roofs`).

## Collision

Use simple colliders (AGENTS.md). Mesh colliders only for walkable curved surfaces.

- [ ] **Walkable surfaces** (terrain, ramps, bridge deck, stage, arena floors): clean, continuous mesh that can take a MeshCollider. Separate from decorative geometry so only this gets a collider.
- [ ] **Obstacles** (walls, gates, rocks, logs, carts, columns, speaker towers, pillars): provide `COL_` guides as boxes, capsules/cylinders or low-poly convex hulls with unit scale, named after the visual object, placed in the same space. Boxes/cylinders are preferred; a guide is a box mesh whose bounds Unity can read.
- [ ] Foliage and scenic clutter: no collision. A single trunk capsule at most for hero trees.
- [ ] Boundaries: invisible boundary guides closing every edge a rider can reach, including arena rims and above-floor gaps, so no bypass or jump leaves the map.
- [ ] Gates/seals: each route gate has (a) a blocking box guide, (b) a separate selection trigger volume, (c) a rear/entrance seal. A locked gate blocks with its collider, an open gate does not (Gameplay §2–4). The map only supplies geometry; lock logic is `RouteGate`.
- [ ] Triggers are not part of the render mesh.

## Gameplay markers

Markers are empties (or tiny meshes) named and parented per room; they never spawn or run gameplay on their own. Unity binds them. Keep a `markers.json` in Blender XYZ meters listing each marker (name, type, position, rotation, owner space).

| Marker | Per | Purpose |
|---|---|---|
| `Start_Player` | level | Spawn point + yaw |
| `Gate_<Route>` + `Trigger_<Route>` + `Seal_<Route>_Front/Rear` | branch | Selection trigger, lock collider, seals |
| `Exit_<Level>` | level | Scene transition trigger volume |
| `Charge_<Space>_1..3` | arena | Arena battery charge locations; grounded to floor, accessible (Gameplay §5) |
| `Spawn_<Space>_n` | arena | Valid ground spawn candidates, offscreen-capable, ≥ 8 m from Boki |
| `Guilt_Anchor_<Space>_n` | arena | Guilt appearance candidates |
| `Occupancy_<Space>` | arena | Volume (floor height to ~5–16 m) defining "player is in this space" |
| `Event_<Name>` | as needed | Authored reveals/events |

All markers sit on or snap to the final floor surface (raycast down to verify).

## Gameplay-readability

- [ ] Route signage/landmarks make A/B/C recognizable from the entrance without UI.
- [ ] Silhouette contrast between walkable ground and hazards (pits, fall lines) under fog and Low settings.
- [ ] Dead ends are intentional. Every allowed route reaches its exit; sealed branches are sealed at both ends.
- [ ] Visibility: geometry beyond ~100 m can be dropped; fog masks the rest (Gameplay §8). Do not model whole streets behind fog.
- [ ] Combat spaces have more than one riding line and landmarks to dodge around; no empty perimeter acreage.

## Export

- Source: `Source/<Map>.blend` (outside `Game/Assets`). Delivered: `Export/*.fbx` + `Textures/*.png` + `markers.json` + `validation.json`.
- FBX settings: Selected Objects only; Mesh only (+ Empty for markers file); Scale 1.0; Apply Scalings = FBX All; Forward −Z, Up Y; Apply Unit; Apply Modifiers on; Triangulate Faces on; Smoothing = Face for flat-shaded; no animation, no armature, no leaf bones; Path Mode = Copy off (textures are referenced, not embedded).
- One FBX per chunk family (shared, each route, terrain chunks, foliage chunks, roofs, collision guides, markers). Keep individual files small enough to reimport quickly.
- Reimport every FBX into a clean Blender scene and verify triangle count, bounds, material slots and marker positions against `validation.json`.
- Do not hand-edit `.meta` files. Do not export `Library`/`Temp` content.

## Unity import notes (for the handoff, performed in the Editor)

- ModelImporter: Scale Factor 1, Convert Units on, Generate Colliders **off** (colliders are authored), Read/Write only if a postprocessor rewrites UVs, Normals Import, Mesh Compression off for now, Weld Vertices on, Optimize Mesh on.
- Materials: create/assign URP/Lit (opaque, low smoothness) per surface; emission materials recreated; texture Wrap Repeat for tiling surfaces, Clamp for atlases.
- Static flags: Contribute GI off unless baked; Batching Static + Occluder/Occludee for world chunks.
- Guides: attach colliders by reading guide bounds, then hide/delete the guide renderers.
- Match the repo convention in `PohorjeImportPostprocessor` (world-space tiling UVs) if the map relies on it.

## Done means

Report: what was built, triangle/object/material counts, validator result, round-trip result, which Unity steps are pending, and that Windows 5600G performance is **unverified** until measured on target hardware. Say explicitly if any item above was skipped.
