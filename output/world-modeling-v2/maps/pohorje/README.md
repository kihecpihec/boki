# Pohorje v2

A from-scratch rebuild of the Pohorje level (Bellevue → fork → routes A/B/C → three combat spaces → overlook). It replaces the v1 map in `output/world-modeling-v1/maps/pohorje`, which stacked floating floor sheets over a ground slab 2.5 m lower. That caused players to fall under the floor and made the pieces look disconnected. Game rules are unchanged; see [Gameplay.md §2–7](../../../../docs/Gameplay.md) and [Story.md Level 1](../../../../docs/Story.md).

## Rebuild

```sh
cd output/world-modeling-v2/maps/pohorje
/Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python build_pohorje.py
```

Then, in Unity 6000.6.7f1, run `Boki/Pohorje/Build Map Test Scene` and `Boki/Pohorje/Run Map Collision Checks`.

`build_pohorje.py` is the only source. Every run starts from an empty Blender scene, so edit the layout tables at the top of the script and rebuild. Don't hand-edit the exported FBX files or layer patch scripts on top of the output. It writes:

- `Source/Pohorje_v2.blend`: for inspection, regenerated each run.
- `layout.json` and `validation.json`.
- `Previews/`: Blender previews, plus `unity-*` screenshots.
- `Game/Assets/Art/Pohorje_v2/`: FBX files and textures. Unity creates the `.meta` files and the materials.

## Structural rules (why the v1 problems can't come back)

1. **One continuous ground.** The ground is a single heightfield. Lanes, the fork, arenas and the overlook are carved into it (`height = min(forest ground, every carved floor)`), never laid on top. Validation fails the build if the ground has interior holes or non-manifold edges.
2. **The ground contains the player.** Every lane and arena is walled by a bank that rises 5.5 m over 2.5 m (about 66°), steeper than the 45° controller slope limit. Invisible walls only mark the outer edge of the play area.
3. **Lanes meet floors at the same height.** A lane arriving at a lower area is level for longer than the bank width before it reaches it. Bends are rounded, and floor heights are blended so there are no steps where segments meet.
4. **Solid things are solid.** Ramps, the stage, the gondola terminal, tunnel walls and roofs are closed meshes sunk into the ground. Tunnel walls run into the hillside, so there's no gap behind them.
5. **Collision is authored, not guessed.** The Unity builder reads names only:

   | Prefix | Collider |
   |---|---|
   | `TERRAIN_*` | Mesh collider (the only mesh colliders) |
   | `UBX_` | Oriented box |
   | `UCP_` | Capsule |
   | `USP_` | Sphere |
   | `UCX_` | Convex mesh |
   | `UBX_GATE_<route>_<Front\|Rear>` | Gate blocker, toggled by gameplay (disabled in the test scene) |

   Trees in the forest have no colliders because they stand on ground the player can't reach. Slalom trees in route A and arena A have capsules.
6. **Gameplay positions are markers**, never geometry names: `MK_PlayerStart`, `MK_Gate_*`, `MK_CombatSpace_*`, `MK_Charge_*` (three per arena), `MK_Spawn_*`, `MK_Guilt_*`, `MK_GuiltReveal_A/C`, `MK_Watcher_B`, `MK_GuaranteedPickup_C`, `MK_StrongerEnemy_C_*`, `MK_ExitForest`, and `MK_Light_*` (the builder turns these into point lights). `layout.json` lists every marker in Unity coordinates.

Coordinates in the script are Unity metres (x right, y up, z downhill). Blender stores (−x, −z, y), and the FBX export brings that back with identity transforms.

## Layout

| Area | What it is | Floor height |
|---|---|---|
| Bellevue | Fenced start platform: bench, hut, lamps | 40 m |
| Safe ride | 10 m wide, with one small boost bump | 40 → 37 m |
| Fork | Signpost and three route gates | 37 m |
| A — Gozdna strmina | Winding 9 m forest lane: tree slalom, side logs, orange lamps | → 22 m |
| Arena A | Logging clearing: mounds, a shallow pit, spruces, log stacks, boulders | 22 m |
| B — Gondolska servisna pot | 14 m snow slope (24°): two plank jumps, overturned carts, gondola line, Watcher cabin | → 20 m |
| Arena B | Snowy maintenance yard: solid terminal with a ramp, carts, crates | 20 m |
| C — Rave tunel | 8 m lane into a 37 m concrete tunnel: purple neon, grate window onto the Guilt reveal alcove | → 24 m |
| Arena C | Cistern rave: walls with entry and exit openings, sunken dance floor, columns, stage with two ramps, speakers | 24 m |
| Exits → Overlook | Three exit lanes merge on a fenced overlook; the ground drops away toward the Maribor skyline | 14 m |

## Current validation

- **Blender:** 0 interior holes and 0 non-manifold edges in the terrain. Floors are within 18 cm of design height (arena mounds included). 821 of 840 bank samples are steeper than 45°. About 70.6k render triangles in total (terrain 28.2k in 31 chunks).
- **Unity (`PohorjeMapChecks`):**
  - 0 drop-throughs, 0 lane falls, 0 blocked lanes, and 0 blocked arena crossings.
  - 0 failures in 500 random rides at bunny-hop speed with jumps.
  - The only floor flags are intended obstacles on lanes (logs, carts, a crate) and pad corners, which the checker samples but which are rounded off.

## Not done

- **Production scene:** `Level1_Pohorje` is untouched. Gates, triggers, encounters and charges still have to be bound to the markers in the scene owner's production level.
- **Testing:** no Play Mode playtest with real input, and no FPS measurement on the Windows Ryzen 5600G.
- **Old assets:** `Game/Assets/Art/Pohorje` (v1) and `PohorjeImportPostprocessor` are no longer used by the test scene. They can be removed once the team agrees.
