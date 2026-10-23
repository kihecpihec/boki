# BOKI — Character modeling handoff v2

Open [the visual reference pack](index.html). Download or copy the whole folder so the image links remain intact.

This pack expands the established Boki, enemy and professor appearances into seven production reference sheets. It is a design proposal for artists to build in Blender and integrate in Unity. No `.blend`, FBX, rig, animation clip, texture atlas or working Unity prefab is delivered here.

Figma's Starter MCP allowance was exhausted while beginning this task. The existing [Figma atlas](https://www.figma.com/design/VkUNlXhesVqCd2H8T9cOWZ) has **not** received these v2 additions.

## Contents and use

- `references/*.png`: seven generated shape, part and pose references. Load these as Blender image references.
- `figma/*.svg`: self-contained sheets with embedded artwork plus editable annotations, palettes and numeric guides. Drag these into Figma once you can work in the file. Figma may substitute unavailable fonts; if its SVG importer drops a raster image, place the paired PNG in the artwork rectangle. Import behavior has not been tested because the MCP limit blocks file edits.
- `index.html`: local visual gallery with expandable modeling notes, rig hierarchy and Unity checklist. No network, package installation or web hosting is needed.
- `model-specs.json`: dimensions, object names, proposed budgets, sockets, clip inventory and the inspected player envelope.
- `source-prompts.json`: the prompt set used with the built-in image-generation tool.
- `UNITY_HANDOFF.md`: export, assembly, wiring and validation steps.

Use **01** for Boki's body/head, **02** for the scooter and riding contacts, **03** for all melee variants, **04** for Watchers and exam paper, **05** for Guilt, **06** for the professor's body/head, and **07** for his gestures, lectern and attack props.

## What controls the model

The source of gameplay rules remains [Story.md](../../docs/Story.md) and [Gameplay.md](../../docs/Gameplay.md). [Architecture.md](../../docs/Architecture.md) describes the runtime responsibilities. Numeric dimensions, triangle budgets, rig layout, object names, socket names and clip names in this pack are **new art/implementation proposals**. They do not alter movement tuning, attack damage, route rules or character statistics.

Generated turnarounds are shape references, not perfectly registered orthographic blueprints or proven topology. The numeric guides govern construction. Start with a dimensioned Blender blockout, compare front/side/back, reconcile disagreements, and approve the silhouette before retopology and rigging. The pale reference background makes dark enemy edges easier to trace.

## Modeling sequence

1. Build a one-meter reference cube and confirm scene units before modeling.
2. Block out Boki and the scooter together using the contact coordinates. Test the riding silhouette inside the current capsule envelope.
3. Model one melee base, UV it once, and derive the three appearances through textures and small rigid add-ons.
4. Build Watcher and exam-paper geometry as separate cheap props. Keep eye emission and projectile objects separate.
5. Model Guilt from silhouette first. Test his height and arm clearance in corridors before investing in detail.
6. Build the professor and lectern separately. Test the paper-release and glasses-adjust gestures before finalizing sleeves and hands.
7. Create the agreed skeleton, remove internal hidden surfaces, finalize UVs, triangulate an export copy and inspect shading.
8. Export a tiny scale/forward test before exporting the whole cast. Complete the Unity checks in the handoff guide.

## Surface and mesh decisions

- Preserve broad low-poly planes and low-resolution painted texture detail. Model the silhouette, not each jacket wrinkle.
- Share a base-color atlas per family; use at most one additional emission material where the design needs it.
- Use explicit back faces or a shallow solid sheet for exam paper so it stays visible from behind.
- Make headlight, Watcher eye and floor warning effects visually clear without treating every emissive object as a real-time light.
- Bind rigid glasses, watch and backpack to their intended bones. A simple grouped-finger hand is sufficient unless the gesture test proves otherwise.
- Use in-place animation. The gameplay controller owns displacement and jump motion.
- Keep all performance figures as authoring targets. Only a Windows player test on the Ryzen 5600G establishes the frame-rate result.

## Verification status

The package is checked for readable sheets, local links, valid SVG/XML and consistent asset metadata. Blender export, mesh deformation, skin weights, clip playback, Unity imports, actual Inspector assignments, target-hardware performance and Figma SVG import still require the later modeling/integration pass.
