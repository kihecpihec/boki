"""Pre-export validator for Boki map .blend files.

Run: blender --background <map>.blend --python validate_map.py --python-exit-code 1 -- [--json out.json] [--max-tris N]
Checks the current file's mesh objects. Exits non-zero on any ERROR.
"""
import json
import re
import sys

import bmesh
import bpy

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
out_path = argv[argv.index("--json") + 1] if "--json" in argv else None
max_tris = int(argv[argv.index("--max-tris") + 1]) if "--max-tris" in argv else 0

errors, warnings = [], []
err = lambda m: errors.append(m)
warn = lambda m: warnings.append(m)

NAME_OK = re.compile(r"^[A-Za-z0-9_]+(\.\d{3})?$")
PREFIXES = ("PL_", "ENV_", "COL_", "MK_")
MARKER_WORDS = ("Start_", "Gate_", "Trigger_", "Seal_", "Exit_", "Charge_", "Spawn_", "Guilt_", "Occupancy_", "Event_")

us = bpy.context.scene.unit_settings
if us.system != "METRIC" or abs(us.scale_length - 1.0) > 1e-6:
    err(f"Scene units must be Metric with scale 1.0 (got {us.system}, {us.scale_length})")

total_tris, meshes, mats_used = 0, 0, set()
for ob in bpy.context.scene.objects:
    if ob.type == "EMPTY":
        if not ob.name.startswith(MARKER_WORDS) and not ob.name.startswith(PREFIXES):
            warn(f"Empty '{ob.name}' is not a recognised marker name")
        continue
    if ob.type in {"CAMERA", "LIGHT"}:
        warn(f"{ob.type.title()} '{ob.name}' will not export (preview only)")
        continue
    if ob.type != "MESH":
        continue
    meshes += 1
    if not NAME_OK.match(ob.name):
        warn(f"'{ob.name}': use ASCII letters/digits/underscore only (no spaces or punctuation)")
    elif re.search(r"\.\d{3}$", ob.name):
        warn(f"'{ob.name}': .001 suffix; rename if this object is exported as its own file/prefab")
    if not ob.name.startswith(PREFIXES):
        warn(f"'{ob.name}': missing PL_/ENV_/COL_/MK_ prefix")
    if any(s < 0 for s in ob.scale):
        err(f"'{ob.name}': negative scale {tuple(round(s, 3) for s in ob.scale)}")
    if any(abs(s - 1.0) > 1e-4 for s in ob.scale):
        warn(f"'{ob.name}': scale {tuple(round(s, 3) for s in ob.scale)} is not applied")
    if any(m.type in {"BOOLEAN", "MIRROR", "ARRAY"} for m in ob.modifiers):
        warn(f"'{ob.name}': unapplied Boolean/Mirror/Array modifier")
    me = ob.data
    if not me.uv_layers and not ob.name.startswith(("COL_", "MK_")) and "Guides" not in ob.name and "Markers" not in ob.name:
        err(f"'{ob.name}': no UV map")
    if not me.materials and not ob.name.startswith("COL_"):
        warn(f"'{ob.name}': no material")
    for i, m in enumerate(me.materials):
        if m is None:
            err(f"'{ob.name}': empty material slot {i}")
        else:
            if m.name in mats_used:
                continue
            mats_used.add(m.name)
            if re.search(r"\.\d{3}$", m.name):
                warn(f"Material '{m.name}' has a .001 suffix (duplicate?); merge it or confirm the importer strips it")
            if m.use_nodes and any(n.type == "TEX_IMAGE" and n.image and n.image.packed_file for n in m.node_tree.nodes):
                warn(f"Material '{m.name}' uses a packed image; export it to Textures/")

    bm = bmesh.new()
    bm.from_mesh(me)
    bm.faces.ensure_lookup_table()
    tris = sum(len(f.verts) - 2 for f in bm.faces)
    total_tris += tris
    loose = sum(1 for v in bm.verts if not v.link_edges)
    nonman = sum(1 for e in bm.edges if len(e.link_faces) > 2)
    zero = sum(1 for f in bm.faces if f.calc_area() < 1e-8)
    if loose:
        err(f"'{ob.name}': {loose} loose vertices")
    if nonman:
        err(f"'{ob.name}': {nonman} non-manifold edges (3+ faces)")
    if zero:
        err(f"'{ob.name}': {zero} zero-area faces")
    seen, dup = set(), 0
    for f in bm.faces:
        k = tuple(sorted(v.index for v in f.verts))
        dup += k in seen
        seen.add(k)
    if dup:
        err(f"'{ob.name}': {dup} duplicate faces")
    if not (ob.name.startswith("COL_") or "Guides" in ob.name or "Markers" in ob.name):
        for f in bm.faces:
            if any(abs(c) > 5000 for v in f.verts for c in (ob.matrix_world @ v.co)):
                err(f"'{ob.name}': coordinates beyond 5 km; check units")
                break
    bm.free()

if len(mats_used) > 15:
    warn(f"{len(mats_used)} materials in use (target <= 15)")
if max_tris and total_tris > max_tris:
    err(f"{total_tris} triangles exceeds budget {max_tris}")

report = {"meshes": meshes, "triangles": total_tris, "materials": sorted(mats_used),
          "errors": errors, "warnings": warnings}
print(json.dumps(report, indent=2))
if out_path:
    with open(out_path, "w") as fh:
        json.dump(report, fh, indent=2)
if errors:
    raise SystemExit(1)
