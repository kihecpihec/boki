# Maribor v3 — production environment delivery

Open [Source/Maribor_Production.blend](Source/Maribor_Production.blend) for the editable model, [Review.html](Review.html) for matched Unity/Blender views, or [Maribor_Production.unitypackage](Maribor_Production.unitypackage) to import the environment into another Unity 6 URP project. The owned playable art scene is [`Game/Assets/Scenes/Maribor_Production_Preview.unity`](../../../../Game/Assets/Scenes/Maribor_Production_Preview.unity).

This upgrades the existing Tabor, Lent and Podzemni prehod layout. The archived v1 model and its imported scene remain available. `Level2_Maribor`, shared gameplay prefabs and build settings are unchanged. The preview includes the existing Boki controller and camera; encounters, route selection, battery collection and the scripted Guilt escape are marker contracts, not implemented gameplay in this scene.

## What changed

- Correct outward winding in the Blender source. The old source contained 8,092 inward-facing closed components; the legacy Unity postprocessor repaired them during import. This delivery requires no winding repair.
- One continuous, tiled floor surface replaces stacked ribbons, court slabs and millimetre-separated inlays. A one metre support apron sits behind enclosing geometry. All riding collision uses explicit primitives, including the supported ascent/deck/descent car ramp.
- Continuous route boundaries terminate before the shared merge. Visible gate wing walls close both ends of every branch. Front/rear seals remain independent of interior rendering.
- Tunnel roofs are delivered, imported, removable for editing, and backed by ceiling collision. Pocket wall returns, the connector north cap, and the escape gallery wall meet without passable cracks.
- Wider Tabor carriageway and Lent riding lines; lamps and slalom obstacles leave the tested lines clear. The two narrow Lent lanes remain distinct around the central planters.
- Eleven opaque pixel-textured materials replace 49 legacy materials. Padded detail/emission atlases consolidate small surfaces. Large ground/wall/roof/water families retain tiling UVs. Distant houses use solid low-poly silhouettes rather than the full facade kit.
- Small surface stains/leaves have irregular silhouettes. Distant trees snap to the actual simplified hillside mesh; water uses its texture instead of floating reflection stamps. The terracotta city, boarded windows, dead trees, sodium lamps, Drava, bridge silhouette and violet service-gallery wayfinding retain the established horror direction.

## Asset contract

Measured delivery: **86,854 render triangles, 266 render objects, 11 materials, 549 box colliders, 87 capsules and 64 markers**. There are no mesh colliders.

Eight FBXs share one coordinate space: `Maribor_Shared`, `Maribor_Route_A/B/C`, `Maribor_Backdrop`, `Maribor_Roofs`, `Maribor_CollisionGuides`, and `Maribor_Markers`. Instantiate the delivered prefab at position/rotation zero, scale one; no manual axis correction is needed. Source textures use relative paths and are not packed. No animation, runtime scripts, cameras, lights or rigidbodies are included in the environment prefab/package.

`manifest.json` is the Unity-space authority: metres, X right, Y up, Z forward. Every collider has its type, dimensions and quaternion explicitly recorded. `markers.json` also records Blender coordinates. See [COLLISION_PLAN.md](COLLISION_PLAN.md) and [collision-plan.svg](collision-plan.svg).

Prefab roots:

```
Maribor_Production
  Shared / InteriorRender
  Route_A / InteriorRender
  Route_B / InteriorRender
  Route_C / InteriorRender
  Route_C / Removable_Roofs
  Backdrop / InteriorRender
  Seals / PL_Seal_A_Front ... PL_Seal_C_Rear
  Collision / AuthoredShapes / COL_*
  Markers / MK_*
```

The prefab's six seals are closed and route-selection triggers disabled. The review scene opens all six seals for inspection. Render suppression must never disable `Collision` or `Seals`. Bind route availability, selection, rear seals and the mandatory side escape to existing gameplay adapters following [Gameplay §§2–5](../../../../docs/Gameplay.md). No route logic belongs in the model.

`MK_Start_Player` supplies the spawn. `MK_Trigger_A/B/C` are selection volumes. `MK_Occupancy_A/B/C` are combat volumes. `MK_Charge_*`, `MK_Spawn_*`, `MK_Guilt_*`, `MK_C_Guilt_Reveal`, `MK_C_Retreat`, `MK_C_SideDoor`, and `MK_Exit_City` are Inspector binding targets. Five charge locations are provided in Lent, versus three in the other arenas, reflecting the route-specific recovery direction in Gameplay §7; the preview props do not collect/recharge. Confirm that variation when wiring the encounter tuning.

## Unity import and tools

Use the pinned **6000.6.7f1** Editor. `Boki / Maribor Production / Import and Build Preview` configures URP Lit materials, Point filtering with mipmaps, no automatic colliders, imported flat normals, read/write off, and static batching/occlusion flags with Contribute GI off. Large textures repeat; atlases clamp. The detail emission atlas reproduces the per-surface colors; the water retains its faint textured emission, for two emission material families. The review lighting uses one sun and a few nonshadowed local lights. Import/build tools refuse Play Mode and dirty scenes and restore the previous scene setup.

Other menu actions: **Open Preview**, **Validate Collision and Traversal**, **Capture Review Views**, **Export Environment Package**, **Import, Validate and Package**. The same implementation serves Pohorje, whose existing menu commands remain available.

## Rebuild and verification

Run from the repository root:

```bash
/Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --threads 6 \
  --python-exit-code 1 --python output/world-modeling-v3/maps/maribor/build_maribor.py -- --rebuild
/Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --threads 6 \
  --python-exit-code 1 --python output/world-modeling-v3/maps/maribor/validate_delivery.py
/Applications/Blender.app/Contents/MacOS/Blender -b \
  output/world-modeling-v3/maps/maribor/Source/Maribor_Production.blend --threads 6 \
  --python output/world-modeling-v3/maps/maribor/render_previews.py
```

`--rebuild` explicitly replaces the generated source: preserve artist edits first. `layout_modules.py` and `maribor_kit.py` are independent adaptations of the original modular city builder; rebuilding does not load or change the v1 Blender file. The builder recreates the exports, textures, manifest and Unity copies. Render after rebuilding so previews describe the saved source.

Run `Boki.Editor.MariborProductionTools.BuildDelivery` in a graphics-enabled Editor batch to import, validate, capture and package. `Boki.Editor.PohorjeProductionChecks.Run` verifies the shared tooling against Pohorje. Checks write [validation.json](validation.json), [delivery-validation.json](delivery-validation.json), and [unity-validation.json](unity-validation.json). Read the current measured counts there.

Validation covers canonical source and every clean FBX reimport, matching vertices/material slots/objects, relative textures, byte equality with Unity assets, outward closed components, actual visible floor heights, collision-guide orientation/dimensions, imported geometry/material counts, standing marker capsules, 6/10.5/18 m/s rides, jumping around closed seals, open passage and random arena jump/ride runs. It does not establish enemy navigation, hands-on camera feel, production gameplay integration or the Windows Ryzen 5600G performance target. Measure those on the target Windows player at 1280×720 Low.
