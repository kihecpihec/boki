# Pohorje — production art v3

A complete replacement art asset for the Bellevue → fork → forest / gondola / Rave tunel → combat spaces → Maribor overlook layout. The game rules remain in [Gameplay.md](../../../../../Documentation/Gameplay.md) and the atmosphere in [Story.md](../../../../../Documentation/Story.md). Previous assets and production scenes are preserved.

## Open the model

- [Blender source](Source/Pohorje_Production.blend), authored in Blender 5.2.3 LTS.
- Unity prefab: `Source/Game/Assets/Art/Pohorje_v3/Pohorje_Production.prefab`.
- Playable art-review scene: `Source/Game/Assets/Scenes/Pohorje_Production_Preview.unity`.
- Portable environment: `Pohorje_Production.unitypackage`. Its prefab contains geometry, materials, primitive collision, terrain collision, seals, and marker/trigger volumes. It has no gameplay scripts.
- [Render gallery and collision plan](Review.html): switch between matched Blender and Unity views without a server or JavaScript.

Import the package into a Unity 6000.6.7f1 URP project, then drag `Pohorje_Production.prefab` into a scene. The package contains only `Assets/Art/Pohorje_v3`; it does not copy URP package sources or other game assets.

The Blender file opens on the whole-map camera. Collections separate Shared, Route_A/B/C, Backdrop, CollisionGuides, Markers, and Presentation. Collision guides are hidden from rendering and initially hidden in the viewport; show the `COL_*` objects to inspect the exact volumes. Objects named `PL_*_RemovableRoof_*` can be hidden individually to edit interiors. `PL_Seal_*` meshes are independent opaque closures.

## Art direction

Flat-shaded, triangulated geometry, opaque spruce silhouettes, a muted pine/slate/rust palette, original pixel-painted textures, and bitmap wayfinding. Trail, snow, concrete and rock have distinct surfaces and consistent texel scale. No alpha-card forest, normal maps, baked reflection dependency, or post-process effect is required for the model to read.

- **Bellevue:** timber shelter, pitched metal roof, benches, viewing rail, abandoned bottles, mountain-stop signage and lamps.
- **A:** a descending spruce slalom, roots, side logs, clustered bank rock, a logging landing, stacked timber, and a clear riding loop around the combat space.
- **B:** a broad snow service slope, optional supported plank jumps, carts, A-frame gondola pylons, sagging cables, hanging cabins, and a maintenance terminal with a matching access ramp.
- **C:** framed concrete portals, a covered passage with posters and purple strips, a real opening onto the grated Guilt bay, a recessed reservoir dance floor, columns, plumbing, speakers, a DJ booth, two accessible stage ramps, and a separate stage canopy.
- **Ending:** a fenced overlook framing a modeled river valley, bridges, clustered roofs and distant towers. The skyline is geometry, not a single transparent billboard.

The source has 74,546 render triangles, 294 render objects, 13 render materials, 320 scenic trees, 74 terrain chunks, and 69 markers. Actual exported triangle counts, rather than polygon counts, are recorded in `validation.json` and checked against every FBX in `delivery-validation.json`.

## Collision and integration

See [COLLISION_PLAN.md](COLLISION_PLAN.md) for dimensions, ownership, seal behavior and Inspector mapping. `manifest.json` is the machine-readable contract. `markers.json` records Blender-space positions; Unity uses the explicit Unity-space entries in the manifest.

The single ground surface has no interior holes. Walkable geometry is separated from decoration. The environment uses 74 non-convex terrain MeshColliders, 330 explicitly authored boxes and 13 snug trunk/log capsules. No convex-mesh collider fitting or automatic collider generation is used. Collision roots remain active independently of route render roots.

The prefab defaults to closed front/rear seals and disabled selection triggers. The review scene opens every route for art inspection; it does not run route selection, encounters, unlocks or scene transitions. Production gameplay integration remains a scene-owner task. `Level1_Pohorje`, other production scenes, existing prefabs, project settings and build settings are unchanged.

## Rebuild and verify

From this directory:

```sh
/Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python-exit-code 1 --python build_pohorje.py -- --skip-previews
/Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python-exit-code 1 --python validate_delivery.py
/Applications/Blender.app/Contents/MacOS/Blender -b Source/Pohorje_Production.blend --python-exit-code 1 --python render_previews.py
```

In Unity **6000.6.7f1**, use **Boki → Pohorje Production → Import and Validate Delivery**. This creates/rebuilds only the owned v3 prefab and preview scene, refuses Play Mode and dirty scenes, preserves GUIDs, and restores the previous scene setup. Unity generates all `.meta` files. Re-running the command is safe for these generated v3 assets; edit the Blender builder and contract rather than manually patching generated prefab content.

Use **Open Preview** for riding, **Capture Review Views** for screenshots, and **Export Environment Package** to regenerate the portable package. Import sets up URP/Lit materials, opaque rendering, flat imported normals, point filtering, mipmaps, static batching without Contribute GI, and Read/Write off. Signs clamp; surface textures repeat. Only the two declared emissive materials need emission. The prefab does not contain cameras, preview lights, or presentation-only Blender objects.

The source images use relative paths to `../Textures`. FBXs, textures and the manifest are also copied into `Source/Game/Assets/Art/Pohorje_v3/`. The import helper remaps materials explicitly; importing a raw FBX alone does not create the full collision setup. Use the delivered prefab/package or run the helper.

## Recorded checks

- Canonical Blender map validator: no errors. The only source warnings identify presentation lights/camera, which are excluded from export.
- Every FBX is reimported into a fresh Blender scene and revalidated. Object lists, triangle counts, material assignments and world-space vertex positions match. Maximum vertex drift is under 0.04 mm.
- Unity import, material setup and prefab generation run in the pinned Editor. `.meta` pairing and GUID audit have no failures.
- `PohorjeProductionChecks`: floor samples, three-speed corridor/arena rides, standing marker capsules, closed/open seals including lateral jumping bypass attempts, and random jump rides. Exact current results are in `unity-validation.json`; failures throw rather than logging success.
- Latest Unity result: 843 floor samples at the controller's slope limit, 72 corridor/arena rides, six seal checks and 233 random rides; zero failures. The imported prefab matches all 74,546 triangles and 294 render objects, with every material assigned from the v3 asset folder.
- `Previews/blender-*.png` and `Previews/unity-*.png`: matched review cameras covering every space, the tunnel grate and the overview.

Hands-on movement/camera feel with real input, gameplay binding in the production scene, and Windows x86_64 performance on the Ryzen 5600G remain unverified. Editor traversal and screenshots do not establish those results. The new art preview intentionally remains outside the shared production build settings.

## Files

- `build_pohorje.py`: deterministic layout, geometry, painting, collider authoring, markers, export and source save.
- `validate_delivery.py`: source validator plus clean FBX round-trip and Unity-copy checks.
- `render_previews.py`: render the saved source without rebuilding it; accepts optional shot names after `--`.
- `make_review.py`, `Review.html`, `collision-plan.svg`: regenerate/view the render comparison and exact collision footprints.
- `Export/`: separate shared, A/B/C, backdrop, collision-guide and marker FBXs.
- `Textures/`: ten 128×128 opaque surface textures and one 512×512 sign atlas.
- `manifest.json`, `markers.json`, `validation.json`, `delivery-validation.json`, `unity-validation.json`: import contract and evidence.
- `Source/Game/Assets/Editor/PohorjeProductionTools.cs` and `PohorjeProductionChecks.cs`: import, preview, package, capture and physics tools.
