# FERI — first environment pass

Blender map built in this folder from the repository's Figma SVG handoff. This is an authored art/layout delivery. Its FBX, PNG and JSON files are imported into `Game/Assets/Art/FERI/` (see the Unity handoff below); no scene or prefab has been assembled and movement testing is pending. No Maribor, Pohorje or other developer's scene was changed.

Open `Source/FERI.blend` or browse the [preview gallery](index.html). The saved scene shows a roofless cutaway; enable `Removable_Roofs` for an interior view. Presentation cameras and lights are preview-only. Gates and the classroom entry seal are closed by default and must be bound to gameplay before playing.

## Design sources

- [FERI layout](../../world-modeling-v1/figma/06-feri-layout.svg), [module kit](../../world-modeling-v1/figma/07-feri-modules.svg), and [classroom](../../world-modeling-v1/figma/08-classroom-arena.svg).
- [Connected topology](../../world-modeling-v1/floorplans/feri-topology.svg), [classroom dimensions](../../world-modeling-v1/floorplans/classroom-dimensioned.svg), [updated approach combat pockets](../../gameplay-mocks-v2/sheets/feri-combat-plan.svg), and [boss recovery](../../gameplay-mocks-v2/sheets/boss-recovery-plan.svg).
- [Maribor construction and horror pass](../../world-modeling-v1/maps/maribor/README.md): low-poly silhouettes, distressed opaque pixel textures, material/chunk batching, separately authored collision and markers. FERI uses its own canonical green/red palette instead of the city's orange-red lighting.
- [Gameplay](../../../docs/Gameplay.md), [Story](../../../docs/Story.md), and [Movement](../../../docs/Movement.md) govern runtime behavior. The model supplies no gameplay logic.

## Built spaces

Shared campus arrival feeds three separately exportable routes. A has repeated doors and a seminar combat pocket; B has an open overgrown courtyard, dead tree, benches, and a continuous ramp; C has utility pipes/cabinets, exactly two trap plates, and a separate `POWER OFF` switch. Three rear seals lead to one landing and one classroom with peripheral desks, lectern, attendance board and two recovery charge positions. Professor and Guilt are semantic placement markers, not character models.

Each approach pocket is 20 × 18 m, following the newer combat overlays. The classroom is 24 × 20 m, with a clear central 14 × 12 m floor. Corridors have approximately 3.78 m clear width and ceilings at 4.55 m; tubes hang at 4.05 m or higher. The SVG's 3 m corridors, 3.5 m ceiling and 2.4 m doors are proposals; this pass uses 4 m nominal door apertures and wider corridors for scooter/camera clearance. These are not the skill's outdoor 9 m main-lane target; the FERI-specific interior designs control here. B rises 0.5 m over 4 m and descends over another 4 m so the shared merge remains at zero height. Floor slabs meet without steps.

Layout is intentionally schematic, like the SVG topology: A left, courtyard center, service right, classroom behind. It is not a reconstruction of the real university or a pixel trace of the illustrative perspective image. All coordinates are Blender XYZ meters, Z up; +Y runs from arrival toward classroom. `layout.json` records the exact occupied floor cells.

## Files and rebuilding

- `Source/FERI.blend`: editable map, named collections, materials, cameras.
- `Export/FERI_*.fbx`: shared arrival, each approach, shared landing, classroom, seals, removable roofs, collision guides and markers.
- `Textures/`: ten 128² opaque pixel textures, external to the blend.
- `Previews/`: overview, classroom, courtyard, service approach.
- `markers.json`, `collision-guides.json`, `layout.json`, `export-manifest.json`: authoring/integration data.
- `validation.json`, `validation-FERI_*.json`, `verification.json`: source hygiene, every FBX reimport, connectivity/support checks.

Build with Blender 4.4.3:

```sh
blender --background --factory-startup --threads 6 --python-exit-code 1 --python output/maps/feri/build_feri.py
blender --background output/maps/feri/Source/FERI.blend --threads 6 --python-exit-code 1 --python output/maps/feri/verify_feri.py
```

The builder refuses to overwrite the source. Preserve manual artist edits before explicitly using `FERI_FORCE=1`. `FERI_PREVIEWS=0` skips rendering. The verification script never saves over the source. Textures, exports and reports are regenerated alongside the source; keep the whole folder together. Source chunks have world-space origins; imported FBX chunks assemble at zero with scale one, Y up / −Z forward export settings.

## Unity handoff and remaining work

Imported into `Game/Assets/Art/FERI/` with the pinned Unity 6000.6.7f1 Editor, which generated the `.meta` files. Never hand-author a GUID. The map is **not integration-ready** until the steps below are wired and tested in a scene. Do not modify another developer's scene: `Level3_FERI` already holds a graybox over the same area (x ±18, z −20…94), so replace it rather than adding this map beside it.

`Assets/Scenes/FERI_Map_Test.unity` (`Boki ▸ FERI ▸ Build Map Test Scene`, `FeriMapTestBuilder.cs`) places the map around the Boki test player with colliders from the guides, every gate and seal open, roofs off, and no RouteGate logic. `Boki ▸ FERI ▸ Run Walk Check` walks A, B and C from the start into the classroom. It never edits `Level3_FERI`.

After replacing an FBX or PNG, run `Boki ▸ FERI ▸ Configure Import`, then `Boki ▸ FERI ▸ Run Import Checks` (`Assets/Editor/FeriImportSetup.cs`, `FeriImportChecks.cs`). The checks passed on import: triangle and object counts equal `export-manifest.json`, all 386 guides and 69 markers match their JSON, no inside-out solids, and every material and texture resolves inside `Art/FERI`. The only coplanar overlaps between chunks are down-facing wall-base and wall-cap faces buried in floors and wall tops at the 0.19 m seams, so z-fighting is not expected to be visible (geometry check only; not confirmed in a rendered view).

1. Done by `FeriImportSetup`: scale one, Convert Units, imported normals, no generated colliders, no animation, mesh compression off; opaque URP materials in `Art/FERI/Materials`; textures Repeat, Point, uncompressed; `Tube` and `Danger` emission recreated. `FERI_CollisionGuides.fbx` has no material, so its boxes show Unity's default until their renderers are removed. All surfaces have thickness; no double-sided materials or vertex-color shader required.

   **Axes:** Unity imports Blender (x, y, z) as (−x, z, −y), a 180° turn about Y. Parent all FERI chunks under one root rotated (0, 180, 0), as `MariborMapTestBuilder` does, so +Y runs toward +Z and `markers.json` / `collision-guides.json` (Blender XYZ) convert as (x, z, y).
2. Use `COL_*` boxes as BoxCollider proposals; remove their renderers. Merge neighboring floor guides where convenient. Only B's sloped floor portions need special handling: use aligned primitive boxes or isolate the ramp faces for collision; do not add a MeshCollider to an entire decorative route chunk. The source ramp floor has no flat collider hiding its slope.
3. Bind the front/rear seals and selection trigger volumes to route logic using the canonical connection matrix, lock priority and run-start unlock snapshot. Renderers are grouped by route. Roof geometry is in a shared collection in this first pass; split roof groups per route before runtime visibility wiring. Keep seals visible when hiding interiors.
4. Markers identify three charges, six regular spawn candidates, three Guilt candidates and an occupancy volume per approach pocket. Spawn systems must still enforce offscreen/8 m distance and the total FERI regular-enemy cap. Bind the two C traps to their 1.8 m square footprints and the optional panel interaction; no damage behavior or puzzle is implemented in the art.
5. On classroom entry, close its seal, deactivate approach encounters/rendering, save the next-run unlock, and disable Guilt chase. Bind the professor, two classroom recovery charges, B adds and optional C panel modifier. `Exit_FERI` is a victory-event placement, not a walk-through exit or kill-quota gate.
6. Test actual scooter movement, jump/boost, all corners, ramps, trap bypasses, camera clearance and seals. Collision guides are proposals, not tested Unity colliders. Preview lights are intentionally numerous for art review; use one main light and only a few local lights in view in URP. Preview lighting is not a runtime light budget.
7. Measure a Windows 1280 × 720 Low player build on the Ryzen 5600G. Performance remains unverified.

This first pass does not include runtime fog/VHS, animated light failure, professor/Guilt character art, fully dressed exterior/backdrop, or reusable standalone prop FBXs. The static geometry is batched by owner/material rather than shipped as a complete reusable module library. Geometry validation is not a proof of full mesh overlap, navigation, collision behavior or gameplay balance. See the generated reports and `BUILD_REPORT.md` for measured checks.
