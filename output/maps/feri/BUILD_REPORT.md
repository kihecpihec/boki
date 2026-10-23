# FERI build verification

Built and checked with Blender 4.4.3 on Linux.

- Render geometry including removable roofs: **33,092 triangles, 51 mesh objects, 10 materials**.
- Collision proposals: 386 box guides, separately exported; never render them.
- Semantic markers: 69.
- Standard source validator: **zero errors**. Warnings identify preview-only cameras and lights.
- Every one of the 10 FBX files reimported into a cleared scene and passed the standard mesh validator, triangle/object counts, bounds and marker-position comparisons.
- Largest FBX world-bounds error: 0.00000620 m.
- All three isolated route floor graphs reach the same classroom.
- 694 floor-center raycasts match expected elevations; spawn, Guilt, charge and start markers pass floor-support and proposed standing-box clearance checks.
- Exactly two floor-trap footprints and two classroom charge markers.

These checks do not exercise Unity gameplay, navigation, camera motion, gate opening, physical collision, jump bypasses, or target hardware. Windows Ryzen 5600G performance is **unverified**. Unity import and generated `.meta` files are pending; this first-pass art is not integration-ready.

The block construction intentionally retains internal adjoining slab/wall faces; it is inexpensive but not a fully optimized shell. The floor graph and sampled raycasts do not establish continuous controller clearance or eliminate all decorative intersections. Source roofs are removable presentation geometry; split their ownership by route before gameplay visibility wiring. No reusable standalone module library or complete exterior dressing is delivered yet.

See [handoff and rebuild instructions](README.md), [source hygiene](validation.json), and [round-trip and layout results](verification.json).
