# FERI build verification

Built and checked with Blender 4.4.3 on Linux.

- Render geometry including removable roofs: **9,528 triangles, 55 mesh objects, 10 materials**.
- Collision proposals: 117 box guides, separately exported; never render them.
- Semantic markers: 69.
- Standard source validator: **zero errors**. Warnings identify preview-only cameras and lights.
- Every one of the 10 FBX files reimported into a cleared scene and passed the standard mesh validator, triangle/object counts, bounds and marker-position comparisons.
- Largest FBX world-bounds error: 0.00000724 m.
- All three isolated route floor graphs reach the same classroom.
- 1,432 floor-center raycasts match expected elevations; spawn, Guilt, charge and start markers pass floor-support and proposed standing-box clearance checks.
- Exactly two floor-trap footprints and two classroom charge markers.

These checks do not exercise Unity gameplay, navigation, camera motion, gate opening, physical collision, jump bypasses, or target hardware. Windows Ryzen 5600G performance is **unverified**. The Unity import is done (see the README); scene wiring, collision binding and movement tests are pending, so this first-pass art is not integration-ready.

Flat floors, ceilings and straight walls are merged into long slabs per owner, so the larger pockets cost fewer triangles than the first pass; B's ramp cells stay single boxes, and faces buried between adjoining boxes remain, so it is not a fully optimized shell. The floor graph and sampled raycasts do not establish continuous controller clearance or eliminate all decorative intersections. Source roofs are removable ceilings, one object per space with a matching collision guide; wire their visibility per route. No reusable standalone module library or complete exterior dressing is delivered yet.

See [handoff and rebuild instructions](README.md), [source hygiene](validation.json), and [round-trip and layout results](verification.json).
