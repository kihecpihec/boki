# <Map name> — Blender → Unity handoff

**Target:** Unity 6000.6.7f1, URP, Windows x86_64. Status: import pending / imported (pick one).

## Contents
| File | Type | Tris | Objects | Notes |
|---|---|---:|---:|---|

## Scale and axes
Meters, scale 1, Y up / +Z forward after import. Origin convention:

## Spaces and routes
Shared / A / B / C chunks, entrances, exits, arenas (footprint in m).

## Collision
- Walkable mesh colliders:
- Primitive guides (`COL_*`):
- Boundaries:

## Markers (`markers.json`)
Start, gates, triggers, seals, exits, charges, spawns, Guilt anchors, occupancy volumes, events.

## Materials and textures
| Material | Texture | Size | Filter | Emission | Double-sided |
|---|---|---|---|---|---|

## Unity steps remaining
1. Import in pinned Editor so `.meta` files generate.
2. Create URP materials, emission setups.
3. Add colliders from guides; delete guide renderers.
4. Bind gates/triggers/seals to `RouteGate`; markers to spawn/charge/Guilt adapters (scene owner's scene only).
5. Player test: ramps, jumps, boost, camera clearance, bypasses.
6. Windows 1280×720 Low performance check on the Ryzen 5600G.

## Checks run
Validator output, round-trip reimport, anything skipped.
