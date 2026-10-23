# Pohorje restrained style pass

Open `ENV_Pohorje_Restrained.blend`, scene `Pohorje_Restrained`. The independent first-pass scene and its data remain in the file for comparison; the original v1 file is unchanged. The active camera shows the approach at riding height. Preview images are `riding-view.png` and `overview.png`.

Changes: spruce reduced from 172 to 64 triangles, rock from 52 to 26; five original 128 × 128 repeating color textures; muted earth/foliage palette; cooler, subdued directional lighting; varied tree heights and spacing; broad scenic forest-floor strips and a continuous underlay. Tree foliage stays opaque. This is an initial interpretation of the requested restrained horror direction, not a recreation of another game's assets.

The six module exports plus calibration cube are in `Export/`. Texture PNGs are in `Textures/`, packed into the Blender source, and embedded in FBX. All module meshes have UVs and unit scale. Blender uses Z up; exports specify Y up/-Z forward. `validation.json` records triangle counts, zero-area face checks, manifold edge checks, and unit scales for module render meshes. Spruce foliage/trunk are separate closed components in one mesh. Scenic ground is intentionally an open render surface and excluded from individual module exports.

Run `restyle.py` inside the v1 Blender file to reproduce the source, exports, and renders. It refuses to overwrite an existing second-pass scene. Layout instances share mesh data; individual tree instances vary height while source modules retain unit scale. This pass uses flat silhouettes and simple generated textures; natural branch silhouettes, irregular path shoulders, authored wear, lamps, mountain massing, and the winding slalom remain further art work.

## Pending integration

No Unity assets, scenes, or prefabs were changed. Unity import and `.meta` generation are pending in **6000.6.7f1**. Check scale/orientation and map imported color textures to URP materials; Blender lighting does not transfer as a finished Unity lighting setup. No Unity FBX/material roundtrip has been verified for this pass.

Follow [v1's integration notes](../pohorje-build-v1/README.md) for primitive collider setup and actual Boki movement/camera checks. Scenic strips/underlay are not approved riding collision or route barriers. The existing geometry guides have no runtime bindings. Branches B/C, route seals, pacing, and Windows Ryzen 5600G performance remain unverified. Keep combat/charge/spawn placements subject to the [gameplay overlay](../gameplay-mocks-v2/HANDOFF.md) and [canonical rules](../../docs/Gameplay.md).
