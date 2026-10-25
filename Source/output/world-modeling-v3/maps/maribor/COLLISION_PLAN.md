# Maribor collision plan

The map uses authored BoxColliders and narrow CapsuleColliders only. No MeshCollider, convex hull inference, auto-generated collision or Rigidbody is required. The numerical source of truth is [manifest.json](manifest.json); [collision-plan.svg](collision-plan.svg) shows its exact footprints.

| Geometry | Shape / policy |
|---|---|
| Flat floors | Greedily merged rectangles from one 0.5 m tessellation; top at Y=0; 0.4 m thick. The continuous support apron lies behind walls. |
| Car ramp | Matching inclined render slabs/boxes; ascent 5.5 m, rise 1.36 m, 3.5 m wide; supported 5 m deck; matching descent. |
| Facades / concrete walls / crates / cars | Explicit oriented boxes. Facade wall collision stops at its actual 6.6 m eaves, rather than a phantom extra storey. |
| Roofs | Box union of covered footprints, underside at 4.4 m; separate render collection. Collision remains enabled when the editor roof render root is hidden. |
| Lamps | Separate 0.55 m base box and 0.10 m radius post capsule. The entire lamp is not an invisible broad box. |
| Accessible hero trees | Snug trunk capsules; scenic trees/foliage have no collision. |
| Lent central planters | Short oriented boxes following the actual curved planter rim. |
| Route edges | Continuous tall boundary boxes behind visible rails, buildings or hedges, including corner transitions. They end before the shared merge. |
| Front / rear seals | Six independently switchable blocking boxes and opaque render objects. Fixed wing walls join each opening to its enclosing boundary. |
| Selection / occupancy / exit | Separate trigger boxes on marker objects, never part of render geometry. |
| Leaves / stains / posters / birds / distant scenery / water | No collision. |

Tabor's carriageway is 9 m plus sidewalks. Lent's two planter-side riding lines are approximately 4 m each before the rail/capsule margins. The service gallery is 6 m wide; its side opening is 4 m wide and clears the riding capsule. Its pocket returns and north cap close the former outside-corner gaps. The canonical player envelope and movement values remain in [Movement.md](../../../../../Documentation/Movement.md).

The scene supplies geometry and binding targets. Production wiring must enable/disable only the selected gate's blocking box and seal renderer, retain unchosen front/rear seals, and keep all physical collision independent of route-interior visibility. Bind the side-door sequence before shipping the production level; no event is triggered merely by the supplied marker.
