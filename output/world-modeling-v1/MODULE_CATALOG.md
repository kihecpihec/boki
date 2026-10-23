# Environment module catalog

All dimensions and triangle counts are proposed authoring targets. X/Y/Z extents are meters; floor origins sit on the finished ride plane. These are reference specifications, not meshes.

| Module | Family | Size X × Y × Z (m) | Pivot | Collision | Triangle target | Purpose |
| --- | --- | --- | --- | --- | --- | --- |
| ENV_SH_Floor_4m | shared | 4 × 0.2 × 4 | floor corner at finished Y=0 | box | 12–24 | Concrete floor; shared by underpass and FERI |
| ENV_SH_Wall_4m | shared | 4 × 3.5 × 0.2 | floor/base corner | box | 12–40 | Plain wall; paint texture instead of deep surface cuts |
| ENV_SH_Ceiling_4m | shared | 4 × 0.2 × 4 | bay corner at ceiling underside | box only if required | 12–24 | Separate from walls for cutaway/camera setup |
| ENV_SH_Ramp_4m | shared | 4 × 0.5 × 4 | lower floor corner | one ramp plane/primitive-compatible shape | 12–30 | 0.5m rise over 4m run; proposal |
| ENV_SH_Corner_4m | shared | 4 × 3.5 × 4 | floor bay corner | two wall boxes + floor | 36–100 | 90-degree corner; enlarge turning pocket where needed |
| ENV_SH_DoorFrame | shared | 3 × 3.5 × 0.2 | floor hinge-side corner | frame boxes | 48–100 | 2.4m × 3m clear opening proposal |
| ENV_SH_DoorLeaf | shared | 2.4 × 3 × 0.12 | hinge at floor | box | 12–40 | Separate leaf, no player collider while passable |
| ENV_SH_Tunnel_Straight_4m | shared | 4 × 4 × 4 | finished-floor corner | floor/wall/ceiling boxes | 60–140 | 3m × 3.5m clear cross section proposal |
| ENV_SH_GateFrame | shared | 4 × 4 × 0.3 | floor corner | post/header boxes | 48–120 | 3m × 3.5m clear gate opening proposal |
| ENV_SH_GateLeaf | shared | 3 × 3.5 × 0.12 | hinge at floor | separate blocking box | 40–120 | States own blocker and trigger independently of art |
| ENV_SH_RouteSign | shared | 1.2 × 2.5 × 0.12 | post ground center | one box if blocking | 30–80 | A/B/C sign plates and lock indication |
| ENV_SH_PosterPlane | shared | 0.6 × 0.9 × 0.01 | lower-left wall attachment | none | 2–4 | Low-res shared poster texture, no collision |
| ENV_F_PathTile_4m | forest | 4 × 0.3 × 4 | finished-floor corner | simple ground surface | 20–60 | Faceted dirt tile with continuous ride plane |
| ENV_F_BankWedge | forest | 4 × 2 × 2 | lower terrain corner | box/ramp primitive | 12–40 | Blocks branch shortcuts; not a kill pit |
| ENV_F_Spruce | forest | 2.5 × 6 × 2.5 | trunk ground center | trunk capsule | 80–180 | Three height variants via authoring scale; opaque clusters |
| ENV_F_Rock | forest | 2 × 1.5 × 1.5 | ground base center | one or two boxes | 20–60 | Three silhouette variants |
| ENV_F_Log | forest | 3 × 0.7 × 0.7 | base center | capsule/box | 24–64 | Slalom prop; test clearance before placement |
| ENV_F_Fence_4m | forest | 4 × 1.1 × 0.2 | post floor corner | continuous box | 40–100 | Avoid one collider per rail |
| ENV_F_GondolaPole | forest | 3 × 8 × 1 | pole ground center | base box only if needed | 80–180 | Static silhouette, no operating gondola |
| ENV_F_GondolaCabin | forest | 1.5 × 2 × 1.5 | suspension attachment | none when unreachable | 80–180 | Static prop, no animation system |
| ENV_F_MaintenanceCart | forest | 1.4 × 1.2 × 2 | ground base center | 2–3 boxes | 100–240 | Upright/overturned placements reuse mesh |
| ENV_F_BrokenPlanks | forest | 3 × 0.2 × 4 | near-edge floor corner | test-defined surfaces | 40–120 | Gap width is a movement-test result, not fixed by artwork |
| ENV_F_Grate | forest | 3 × 3.5 × 0.12 | frame floor corner | box blocker | 40–100 | Guilt view is scenic set piece |
| ENV_F_Bottle | forest | 0.08 × 0.28 × 0.08 | base center | none | 24–48 | Bellevue prologue prop |
| ENV_F_SkylineCard | forest | 24 × 6 × 0.02 | lower card center | none | 2–10 | Distant city silhouette; no full city rendering |
| ENV_C_FacadeBay_4m | city | 4 × 8 × 0.4 | street-level corner | coarse boundary box | 80–240 | Three reusable facade texture/shape variants |
| ENV_C_FacadeCorner | city | 4 × 8 × 4 | street corner | two boundary boxes | 80–240 | No modeled inaccessible interiors |
| ENV_C_RoofCap | city | 4 × 2 × 4 | facade top corner | none when inaccessible | 20–80 | Roof silhouette only |
| ENV_C_Road_4m | city | 4 × 0.2 × 4 | finished-road corner | box | 12–24 | Repeat across 6–8m proposed clear street |
| ENV_C_CurbRamp | city | 4 × 0.15 × 1 | low street-level corner | smooth wedge | 12–30 | No raised lip across travel lane |
| ENV_C_CompactCar | city | 1.7 × 1.5 × 3.8 | ground midpoint | 2–3 boxes | 180–360 | Same car reused overturned |
| ENV_C_CarRamp | city | 2.5 × 0.8 × 4 | low landing corner | smooth ramp surface | 20–50 | Separate ride surface from wreck art |
| ENV_C_Streetlamp | city | 0.5 × 4 × 0.5 | ground center | pole primitive | 40–100 | Emissive face; real-time light only where budget allows |
| ENV_C_Bin | city | 0.7 × 1 × 0.7 | ground center | box | 24–60 | Place outside required clear width |
| ENV_C_Riverbank_4m | city | 4 × 0.3 × 3 | finished-walkway corner | floor + edge boxes | 24–60 | Continuous boundary against water |
| ENV_C_Railing_4m | city | 4 × 1.2 × 0.15 | end post floor corner | one continuous box | 40–100 | Prevents unintended water access |
| ENV_C_WaterPlane | city | 8 × 0.01 × 8 | tile corner | none; perimeter handles boundary | 2–8 | Flat surface; no swimming/fall rule added |
| ENV_C_BridgeSilhouette | city | 24 × 8 × 2 | base corner | none when scenic | 80–200 | Background arch; not a fourth route |
| ENV_C_Fence_4m | city | 4 × 2 × 0.15 | post floor corner | continuous box | 40–100 | Shared gate connection |
| ENV_E_CourtyardTile | feri | 4 × 0.2 × 4 | finished-floor corner | box | 12–30 | Same structural grid as corridors |
| ENV_E_Planter | feri | 2 × 0.8 × 1 | base corner | box | 40–100 | Keep outside ride lane; simple overgrowth |
| ENV_E_DeskChair | feri | 1.4 × 1.1 × 1.4 | desk floor center | 2–3 boxes | 100–220 | Push to arena perimeter |
| ENV_E_Lectern | feri | 0.75 × 1.1 × 0.5 | floor center | box | 150–300 | Matches character handoff prop proposal |
| ENV_E_Blackboard | feri | 4 × 1.5 × 0.05 | lower-left wall attachment | none | 12–24 | PRISOTNOST: 0/1 is preserved narrative content |
| ENV_E_Fluorescent | feri | 1.2 × 0.1 × 0.2 | mounting center | none | 12–40 | Emissive mesh, no light per instance requirement |
| ENV_E_PipeRun | feri | 4 × 0.25 × 0.25 | wall/base attachment end | none if out of reach | 40–100 | Keep off walkable floor and camera lane |
| ENV_E_PowerOffPanel | feri | 0.6 × 1 × 0.15 | lower-left wall attachment | box if physically necessary | 60–140 | Separate switch, interaction marker stays reachable |
| ENV_E_FloorTrap | feri | 1.2 × 0.03 × 1.2 | floor center | separate gameplay trigger | 12–40 | Instance twice on FERI C; match visual and hit footprints |
| ENV_E_WallShadow | feri | 6 × 6 × 0.01 | bottom wall center | none | 2–40 | Decorative Guilt only; no chase logic |
| ENV_E_WarningRing | feri | 1 × 0.01 × 1 | floor center | none; combat handles area hit | 32–64 | Scales to actual damage radius, not artist-selected radius |
