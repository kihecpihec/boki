"""Pohorje v2: one deterministic build of the whole forest level.

Run:  Blender -b --factory-startup --python build_pohorje.py
Builds from an empty scene every time (no layered patch scripts), then writes:
  Source/Pohorje_v2.blend, layout.json, validation.json, Previews/*.png
  Game/Assets/Art/Pohorje_v2/*.fbx and Textures/*.png  (Unity import; .meta files come from Unity)

Structural rules this script enforces (see README.md):
  * The ground is ONE continuous heightfield. Lanes, pads and arenas are carved into it, never laid on top.
    Banks rise steeply (> 45 deg, the controller slope limit) so the ground itself contains the player.
  * Every raised or solid object is a closed mesh standing on (and sunk into) the ground.
  * Collision is authored, not guessed: UBX_ box, UCP_ capsule, USP_ sphere, UCX_ convex mesh.
    The terrain chunks (TERRAIN_*) are the only mesh colliders.
  * Gameplay positions are MK_ empties. Gate blockers are UBX_GATE_* and are toggled by gameplay code.

Coordinates: all layout numbers are Unity metres (x right, y up, z forward/downhill).
Blender (bx, by, bz) = (-x, -z, y); Unity imports it back as (x, y, z) with identity transforms.
"""
import bpy, bmesh, json, math, os, random, zlib
import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..', '..', '..'))
UNITY = os.path.join(REPO, 'Game', 'Assets', 'Art', 'Pohorje_v2')
TEX = os.path.join(UNITY, 'Textures')
for d in (UNITY, TEX, os.path.join(HERE, 'Source'), os.path.join(HERE, 'Previews')):
    os.makedirs(d, exist_ok=True)
rng = random.Random(20261024)

def P(x, y, z):
    """Unity (x, y-up, z) -> Blender vector."""
    return Vector((-x, -z, y))

# --------------------------------------------------------------------------------------------
# Layout (Unity metres). Heights drop from Bellevue (40 m) to the overlook (14 m).
# --------------------------------------------------------------------------------------------
BANK_RISE, BANK_WIDTH = 5.5, 2.5          # carved walls: 5.5 m over 2.5 m (~66 deg), steeper than 45 deg
PADS = {   # centre, half size, corner radius, floor height, floor material, route
    'Bellevue': dict(c=(0, 0),     half=(13, 11), r=4, h=40, mat='Path',     route='shared'),
    'Fork':     dict(c=(0, 48),    half=(19, 9),  r=5, h=37, mat='Path',     route='shared'),
    'ArenaA':   dict(c=(-66, 126), half=(22, 20), r=8, h=22, mat='Dirt',     route='A'),
    'ArenaB':   dict(c=(0, 126),   half=(22, 20), r=6, h=20, mat='Snow',     route='B'),
    'ArenaC':   dict(c=(60, 124),  half=(20, 18), r=1, h=24, mat='Concrete', route='C'),
    'Overlook': dict(c=(0, 186),   half=(16, 8),  r=4, h=14, mat='Path',     route='shared'),
    'AlcoveC':  dict(c=(53.3, 82.9), half=(3, 2.6), r=0.5, h=28.0, mat='Concrete', route='C'),
}
# Lane nodes (x, z, floor height). A lane arriving at a lower pad is level for > BANK_WIDTH before it.
LANES = {
    'Safe':  dict(w=10, mat='Path', route='shared', pts=[(0, 6, 40), (0, 11, 40), (0, 39, 37), (0, 44, 37)]),
    'A':     dict(w=9, mat='Path', route='A', pts=[(-14, 50, 37), (-21, 52, 37), (-30, 60, 35), (-35, 72, 32),
              (-30, 84, 29), (-38, 96, 25), (-50, 100, 22), (-58, 102, 22), (-62, 112, 22)]),
    'B':     dict(w=14, mat='Snow', route='B', pts=[(0, 52, 37), (0, 60, 37), (0, 96, 21), (0, 101, 20), (0, 112, 20)]),
    'C':     dict(w=8, mat='Concrete', route='C', pts=[(14, 50, 37), (21, 52, 37), (30, 58, 35.5), (38, 66, 33.5),
              (44, 80, 30), (50, 92, 26), (54, 99, 24), (58, 110, 24)]),
    'ExitA': dict(w=9, mat='Path', route='A', pts=[(-56, 140, 22), (-50, 150, 22), (-44, 156, 20), (-30, 166, 17),
              (-22, 172, 14), (-12, 182, 14)]),
    'ExitB': dict(w=10, mat='Snow', route='B', pts=[(0, 140, 20), (0, 150, 20), (0, 164, 15), (0, 170, 14), (0, 182, 14)]),
    'ExitC': dict(w=8, mat='Path', route='C', pts=[(62, 138, 24), (60, 150, 24), (50, 160, 20), (36, 168, 16),
              (24, 174, 14), (12, 182, 14)]),
}
def chaikin(pts, iterations=3):
    """Round lane bends (keeps both ends). Heights follow the original arc-length profile."""
    for _ in range(iterations):
        out = [pts[0]]
        for k, (a, b) in enumerate(zip(pts, pts[1:])):
            if k > 0: out.append(tuple(0.75 * a[i] + 0.25 * b[i] for i in range(3)))
            if k < len(pts) - 2: out.append(tuple(0.25 * a[i] + 0.75 * b[i] for i in range(3)))
        out.append(pts[-1]); pts = out
    return pts

LANES_RAW = {n: list(L['pts']) for n, L in LANES.items()}
for L in LANES.values():
    L['pts'] = chaikin(L['pts'])

GRID = dict(x0=-105, x1=100, z0=-30, z1=232, step=1.0)
PLAY_BOUNDS = dict(x0=-96, x1=92, z0=-20, z1=194.3)   # invisible walls (the banks already contain the routes)
CLIFF_Z = 194.4                                      # south rim: terrain drops toward the city beyond the overlook

# --------------------------------------------------------------------------------------------
# Signed distances (vectorised over numpy arrays of Unity x, z)
# --------------------------------------------------------------------------------------------
def sd_pad(x, z, p):
    cx, cz = p['c']; hx, hz = p['half']; r = p['r']
    qx = np.abs(x - cx) - (hx - r); qz = np.abs(z - cz) - (hz - r)
    out = np.hypot(np.maximum(qx, 0), np.maximum(qz, 0))
    return out + np.minimum(np.maximum(qx, qz), 0) - r

def lane_eval(x, z, lane):
    """Signed distance to the lane edge and a continuous floor height.
    Heights of nearby segments are blended by distance so the floor never steps where the nearest segment switches."""
    pts = lane['pts']; ds, hs = [], []
    for (ax, az, ah), (bx, bz, bh) in zip(pts, pts[1:]):
        vx, vz = bx - ax, bz - az; L2 = vx * vx + vz * vz
        t = np.clip(((x - ax) * vx + (z - az) * vz) / L2, 0, 1)
        ds.append(np.hypot(x - (ax + t * vx), z - (az + t * vz))); hs.append(ah + t * (bh - ah))
    ds, hs = np.stack(ds), np.stack(hs); best = ds.min(axis=0)
    w = np.exp(-(ds - best) / 0.8)
    return best - lane['w'] / 2, (w * hs).sum(axis=0) / w.sum(axis=0)

def pad_floor(name, x, z):
    p = PADS[name]; h = np.full(x.shape, float(p['h']))
    cx, cz = p['c']
    def bump(px, pz, rad, amp):
        return amp * np.exp(-(((x - cx - px) ** 2 + (z - cz - pz) ** 2) / (rad * rad)))
    if name == 'ArenaA':   # two riding mounds and a shallow pit (Gameplay §6: visual ground pits)
        h = h + bump(-9, 4, 5.5, 1.6) + bump(10, -6, 4.5, 1.2) - bump(4, 9, 4.0, 1.1)
    elif name == 'ArenaB':  # gentle snow drifts
        h = h + bump(-12, 8, 6, 0.9) + bump(13, 10, 5, 0.7)
    elif name == 'ArenaC':  # sunken dance floor, 1.2 m deep with ~22 deg sides
        qx = np.abs(x - (cx - 3)) - 8; qz = np.abs(z - cz) - 7
        d = np.maximum(qx, qz)
        h = h - 1.2 * np.clip(-d / 3.0, 0, 1)
    return h

def smoothstep(t):
    t = np.clip(t, 0, 1); return t * t * (3 - 2 * t)

def bank(d):
    d = np.maximum(d, 0)
    return BANK_RISE * smoothstep(d / BANK_WIDTH) + np.maximum(d - BANK_WIDTH, 0) * 1.0

FEATURES = [('pad', n) for n in PADS] + [('lane', n) for n in LANES]

def feature_fields(x, z):
    """Per feature: signed distance, floor height. Stacked arrays [features, ...]."""
    sds, hs = [], []
    for kind, n in FEATURES:
        if kind == 'pad':
            sds.append(sd_pad(x, z, PADS[n])); hs.append(pad_floor(n, x, z))
        else:
            d, h = lane_eval(x, z, LANES[n]); sds.append(d); hs.append(h)
    return np.stack(sds), np.stack(hs)

def control_points():
    pts = []
    for n, p in PADS.items():
        cx, cz = p['c']; hx, hz = p['half']
        for i in range(-2, 3):
            for j in range(-2, 3):
                pts.append((cx + hx * i / 2, cz + hz * j / 2, p['h']))
    for lane in LANES.values():
        for (ax, az, ah), (bx, bz, bh) in zip(lane['pts'], lane['pts'][1:]):
            n = max(1, int(math.hypot(bx - ax, bz - az) / 4))
            for k in range(n + 1):
                t = k / n; pts.append((ax + t * (bx - ax), az + t * (bz - az), ah + t * (bh - ah)))
    # Mountain shape: high flanks, falling away south toward Maribor.
    for x, z, h in [(-105, -30, 58), (100, -30, 58), (0, -30, 52), (-105, 60, 48), (100, 60, 48),
                    (-105, 150, 36), (100, 150, 36), (-105, 232, 0), (100, 232, 0), (0, 232, -12),
                    (-50, 232, -6), (50, 232, -6), (-60, 205, 4), (60, 205, 4), (0, 210, 0)]:
        pts.append((x, z, h))
    return np.array(pts)

def noise(x, z):
    return (0.9 * np.sin(0.083 * x + 1.3) * np.sin(0.071 * z + 0.4)
            + 0.5 * np.sin(0.19 * x - 0.7 + 0.11 * z) + 0.35 * np.sin(0.27 * z + 2.1 - 0.05 * x))

CP = control_points()

def height_field(x, z):
    """Final ground height: min(natural forest ground, every carved feature)."""
    num = np.zeros(x.shape); den = np.zeros(x.shape)
    for cx, cz, ch in CP:
        w = 1.0 / ((x - cx) ** 2 + (z - cz) ** 2 + 4.0) ** 1.5
        num += w * ch; den += w
    natural = num / den + BANK_RISE + 0.6 + noise(x, z)
    sds, hs = feature_fields(x, z)
    carved = np.min(hs + bank(sds), axis=0)
    h = np.minimum(natural, carved)
    # South rim: a steep drop beyond the overlook so the city view opens up.
    cliff = 13.8 - 1.0 * (z - CLIFF_Z) + noise(x, z) * 0.6
    h = np.where(z > CLIFF_Z, np.minimum(h, cliff), h)
    return h

def inside_feature(x, z):
    """Index into FEATURES of the floor a point belongs to, or -1."""
    sds, hs = feature_fields(x, z)
    idx = np.argmin(np.where(sds <= 0.05, hs, np.inf), axis=0)
    return np.where(np.min(sds, axis=0) <= 0.05, idx, -1), np.min(sds, axis=0)

# --------------------------------------------------------------------------------------------
# Scene, collections, materials, textures
# --------------------------------------------------------------------------------------------
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene; scene.name = 'Pohorje_v2'
scene.unit_settings.system = 'METRIC'; scene.unit_settings.scale_length = 1.0
COLS = {}
for c in ('Terrain', 'Props', 'Forest', 'Backdrop', 'Collision', 'Markers'):
    col = bpy.data.collections.new(c); scene.collection.children.link(col); COLS[c] = col

PALETTE = {  # base sRGB colour, noise amplitude, pattern
    'Path': ((0.60, 0.50, 0.38), 0.06, 'pebble'), 'Dirt': ((0.42, 0.33, 0.25), 0.07, 'pebble'),
    'Snow': ((0.78, 0.82, 0.90), 0.04, 'soft'), 'Concrete': ((0.46, 0.46, 0.50), 0.05, 'panel'),
    'Ground': ((0.13, 0.19, 0.17), 0.06, 'moss'), 'Cliff': ((0.33, 0.34, 0.37), 0.08, 'strata'),
    'Bark': ((0.33, 0.22, 0.15), 0.07, 'grain'), 'Needles': ((0.10, 0.22, 0.17), 0.06, 'needles'),
    'Wood': ((0.50, 0.33, 0.19), 0.06, 'planks'), 'Metal': ((0.25, 0.27, 0.31), 0.04, 'soft'),
    'RedPaint': ((0.62, 0.15, 0.10), 0.05, 'soft'), 'Dark': ((0.06, 0.06, 0.08), 0.03, 'soft'),
}
EMISSIVE = {'NeonPurple': (0.62, 0.20, 1.0), 'LampWarm': (1.0, 0.62, 0.28), 'ChargeGreen': (0.30, 1.0, 0.55),
            'SignA': (0.35, 0.85, 0.45), 'SignB': (0.35, 0.60, 1.0), 'SignC': (0.70, 0.40, 1.0), 'WarnRed': (1.0, 0.15, 0.1)}

def make_texture(name, base, amp, pattern, size=64):
    r = np.random.default_rng(zlib.crc32(name.encode()))
    yy, xx = np.mgrid[0:size, 0:size]
    v = r.normal(0, 1, (size, size))
    # Blur a little so the pattern reads as material, not camo noise.
    v = (v + np.roll(v, 1, 0) + np.roll(v, 1, 1) + np.roll(v, -1, 0) + np.roll(v, -1, 1)) / 5
    if pattern == 'pebble':
        v += (r.random((size, size)) > 0.985) * 2.2
    elif pattern == 'panel':
        v -= ((xx % 32 == 0) | (yy % 32 == 0)) * 2.0
    elif pattern == 'strata':
        v += np.sin(yy * 0.55 + np.sin(xx * 0.2) * 1.5) * 0.8
    elif pattern == 'grain':
        v += np.sin(xx * 0.9 + r.random() * 3) * 0.6
    elif pattern == 'planks':
        v -= (yy % 16 == 0) * 2.2; v += np.sin(xx * 0.5) * 0.3
    elif pattern == 'needles':
        v += ((xx + yy * 2) % 7 == 0) * 0.8
    elif pattern == 'moss':
        v += (r.random((size, size)) > 0.97) * 1.2
    img = np.clip(np.array(base)[None, None, :] * (1 + amp * v[..., None]), 0, 1)
    rgba = np.concatenate([img, np.ones((size, size, 1))], axis=2)
    im = bpy.data.images.new('T_' + name, size, size, alpha=False)
    im.pixels = rgba[::-1].ravel().tolist()
    im.filepath_raw = os.path.join(TEX, 'Pohorje_' + name + '.png'); im.file_format = 'PNG'; im.save()
    return im

def make_skyline(w=512, h=128):
    r = np.random.default_rng(7); img = np.zeros((h, w, 4))
    x = 0
    while x < w:
        bw = int(r.integers(10, 34)); bh = int(r.integers(18, 100)); col = 0.04 + r.random() * 0.03
        img[h - bh:, x:x + bw, :3] = col; img[h - bh:, x:x + bw, 3] = 1
        for wy in range(h - bh + 4, h - 4, 6):
            for wx in range(x + 2, x + bw - 2, 5):
                if r.random() < 0.28: img[wy:wy + 2, wx:wx + 2, :3] = (1.0, 0.55, 0.25); img[wy:wy + 2, wx:wx + 2, 3] = 1
        x += bw + int(r.integers(0, 4))
    im = bpy.data.images.new('T_Skyline', w, h, alpha=True)
    im.pixels = img[::-1].ravel().tolist()
    im.filepath_raw = os.path.join(TEX, 'Pohorje_Skyline.png'); im.file_format = 'PNG'; im.save()
    return im

MATS = {}
def material(name, image=None, emission=None):
    m = bpy.data.materials.new('Pohorje_' + name); m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Roughness'].default_value = 1.0
    if image is not None:
        tn = m.node_tree.nodes.new('ShaderNodeTexImage'); tn.image = image; tn.interpolation = 'Closest'
        m.node_tree.links.new(tn.outputs['Color'], bsdf.inputs['Base Color'])
    if emission is not None:
        bsdf.inputs['Base Color'].default_value = (*emission, 1)
        bsdf.inputs['Emission Color'].default_value = (*emission, 1); bsdf.inputs['Emission Strength'].default_value = 3
    MATS[name] = m; return m

for n, (base, amp, pat) in PALETTE.items():
    material(n, make_texture(n, base, amp, pat))
for n, c in EMISSIVE.items():
    material(n, emission=c)
material('Skyline', make_skyline())
material('Collision')

# --------------------------------------------------------------------------------------------
# Terrain
# --------------------------------------------------------------------------------------------
xs = np.arange(GRID['x0'], GRID['x1'] + 1e-6, GRID['step']); zs = np.arange(GRID['z0'], GRID['z1'] + 1e-6, GRID['step'])
gx, gz = np.meshgrid(xs, zs)            # [nz, nx]
gh = height_field(gx, gz)
nz, nx = gx.shape

FLOOR_MAT = {}
for i, (kind, n) in enumerate(FEATURES):
    FLOOR_MAT[i] = (PADS[n] if kind == 'pad' else LANES[n])['mat']

# Face classification at quad centres.
cx = (gx[:-1, :-1] + gx[1:, 1:]) / 2; cz = (gz[:-1, :-1] + gz[1:, 1:]) / 2
feat, dist = inside_feature(cx, cz)
dhx = (gh[:-1, 1:] + gh[1:, 1:] - gh[:-1, :-1] - gh[1:, :-1]) / (2 * GRID['step'])
dhz = (gh[1:, :-1] + gh[1:, 1:] - gh[:-1, :-1] - gh[:-1, 1:]) / (2 * GRID['step'])
normal_y = 1 / np.sqrt(1 + dhx ** 2 + dhz ** 2)

slot_names = ['Ground', 'Cliff'] + sorted({m for m in FLOOR_MAT.values()})
slot = {n: i for i, n in enumerate(slot_names)}
face_slot = np.where(normal_y < 0.74, slot['Cliff'], slot['Ground'])
for i, m in FLOOR_MAT.items():
    face_slot = np.where(feat == i, slot[m], face_slot)

bm = bmesh.new()
verts = [bm.verts.new(P(gx[j, i], gh[j, i], gz[j, i])) for j in range(nz) for i in range(nx)]
for j in range(nz - 1):
    for i in range(nx - 1):
        a = j * nx + i
        f = bm.faces.new((verts[a], verts[a + 1], verts[a + nx + 1], verts[a + nx]))
        f.material_index = int(face_slot[j, i])
bm.normal_update()
if sum(f.normal.z for f in bm.faces[:50]) < 0:
    for f in bm.faces: f.normal_flip()
outer_boundary_before = sum(1 for e in bm.edges if e.is_boundary)

# Merge flat floors (exactly planar per lane segment / pad) without moving any vertex.
floor_slots = {slot[m] for m in FLOOR_MAT.values()}
floor_edges = [e for e in bm.edges if len(e.link_faces) == 2 and all(f.material_index in floor_slots for f in e.link_faces)]
bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(0.05), verts=[], edges=floor_edges, delimit={'MATERIAL'})
terrain = bpy.data.meshes.new('TERRAIN_Pohorje'); bm.to_mesh(terrain); bm.free()
for n in slot_names: terrain.materials.append(MATS[n])
tobj = bpy.data.objects.new('TERRAIN_Pohorje', terrain); COLS['Terrain'].objects.link(tobj)

# Coarsen the unreachable forest top only (vertex group); banks and floors keep full detail.
vx = np.array([-v.co.x for v in terrain.vertices]); vz = np.array([-v.co.y for v in terrain.vertices])
_, vdist = inside_feature(vx, vz)
border = (np.abs(vx - GRID['x0']) < 1e-3) | (np.abs(vx - GRID['x1']) < 1e-3) | (np.abs(vz - GRID['z0']) < 1e-3) | (np.abs(vz - GRID['z1']) < 1e-3)
far = (vdist > BANK_WIDTH + 2.5) & ~border
vg = tobj.vertex_groups.new(name='forest_top')
vg.add([i for i in np.nonzero(far)[0].tolist()], 1.0, 'REPLACE')
bpy.context.view_layer.objects.active = tobj; tobj.select_set(True)
mod = tobj.modifiers.new('coarsen', 'DECIMATE'); mod.decimate_type = 'COLLAPSE'; mod.ratio = 0.22
mod.vertex_group = 'forest_top'; mod.use_collapse_triangulate = True
bpy.ops.object.modifier_apply(modifier=mod.name)
tobj.vertex_groups.clear()


bm = bmesh.new(); bm.from_mesh(terrain)
bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method='BEAUTY', ngon_method='BEAUTY')
uv = bm.loops.layers.uv.new('UVMap')
for f in bm.faces:
    f.smooth = False
    n = f.normal
    for loop in f.loops:
        co = loop.vert.co
        if abs(n.z) > 0.6: loop[uv].uv = (co.x / 4, co.y / 4)
        elif abs(n.x) > abs(n.y): loop[uv].uv = (co.y / 4, co.z / 4)
        else: loop[uv].uv = (co.x / 4, co.z / 4)
holes = sum(1 for e in bm.edges if e.is_boundary and not (
    abs(-e.verts[0].co.x - GRID['x0']) < 1e-3 and abs(-e.verts[1].co.x - GRID['x0']) < 1e-3 or
    abs(-e.verts[0].co.x - GRID['x1']) < 1e-3 and abs(-e.verts[1].co.x - GRID['x1']) < 1e-3 or
    abs(-e.verts[0].co.y - GRID['z0']) < 1e-3 and abs(-e.verts[1].co.y - GRID['z0']) < 1e-3 or
    abs(-e.verts[0].co.y - GRID['z1']) < 1e-3 and abs(-e.verts[1].co.y - GRID['z1']) < 1e-3))
nonmanifold = sum(1 for e in bm.edges if len(e.link_faces) > 2)
bm.to_mesh(terrain); bm.free()
terrain_tris = len(terrain.polygons)

# BVH of the whole terrain for grounding props and validation.
TBVH = BVHTree.FromObject(tobj, bpy.context.evaluated_depsgraph_get())
def ground(x, z):
    hit = TBVH.ray_cast(P(x, 200, z), Vector((0, 0, -1)), 500)
    return hit[0].z if hit[0] is not None else float(height_field(np.array([x]), np.array([z]))[0])

# Split into 48 m chunks for culling. Chunk borders share identical vertex positions, so no cracks.
def split_terrain():
    CH = 48.0
    bm = bmesh.new(); bm.from_mesh(terrain)
    groups = {}
    for f in bm.faces:
        c = f.calc_center_median(); key = (math.floor(-c.x / CH), math.floor(-c.y / CH))
        groups.setdefault(key, []).append(f.index)
    bm.free()
    names = []
    for key, idx in sorted(groups.items()):
        bm = bmesh.new(); bm.from_mesh(terrain)
        bm.faces.ensure_lookup_table(); keep = set(idx)
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.index not in keep], context='FACES')
        me = bpy.data.meshes.new('TERRAIN_%d_%d' % key); bm.to_mesh(me); bm.free()
        for m in terrain.materials: me.materials.append(m)
        o = bpy.data.objects.new(me.name, me); COLS['Terrain'].objects.link(o); names.append(me.name)
    bpy.data.objects.remove(tobj)
    return names
TERRAIN_CHUNKS = split_terrain()

# --------------------------------------------------------------------------------------------
# Mesh helpers (all inputs in Unity coordinates)
# --------------------------------------------------------------------------------------------
PROPS = []
def link(me, name, col):
    o = bpy.data.objects.new(name, me); COLS[col].objects.link(o); return o

def box_mesh(name, center, size, yaw_dir=(0, 1), mats=('Concrete',)):
    """Oriented box. yaw_dir is the Unity (x, z) direction of the box's local +z (its length)."""
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    fx, fz = yaw_dir; L = math.hypot(fx, fz); fx, fz = fx / L, fz / L
    fwd = P(fx, 0, fz); upv = Vector((0, 0, 1)); right = fwd.cross(upv)
    rot = Matrix((right, fwd, upv)).transposed().to_4x4()
    sx, sy, sz = size  # width, height, length
    bmesh.ops.transform(bm, matrix=Matrix.Translation(P(*center)) @ rot @ Matrix.Diagonal((sx, sz, sy, 1)), verts=bm.verts)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for m in mats: me.materials.append(MATS[m])
    return me

def prism_mesh(name, a, b, radius, sides, mats, taper=1.0):
    """Cylinder (or cone when taper < 1) from Unity point a to b."""
    A, B = P(*a), P(*b); axis = (B - A)
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=sides, radius1=radius, radius2=radius * taper, depth=axis.length)
    rot = axis.normalized().to_track_quat('Z', 'Y').to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=Matrix.Translation((A + B) / 2) @ rot, verts=bm.verts)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for m in mats: me.materials.append(MATS[m])
    return me

def hull_mesh(name, pts_unity, mats):
    bm = bmesh.new()
    for p in pts_unity: bm.verts.new(P(*p))
    bmesh.ops.convex_hull(bm, input=bm.verts[:])
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for m in mats: me.materials.append(MATS[m])
    return me

def rock_mesh(name, center, scale, seed):
    r = random.Random(seed); bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=1, radius=1.0)
    for v in bm.verts:
        v.co *= 0.8 + r.random() * 0.4
    bmesh.ops.convex_hull(bm, input=bm.verts[:])
    sx, sy, sz = scale
    bmesh.ops.transform(bm, matrix=Matrix.Translation(P(*center)) @ Matrix.Rotation(r.random() * 6.28, 4, 'Z')
                        @ Matrix.Diagonal((sx, sz, sy, 1)), verts=bm.verts)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free(); me.materials.append(MATS['Cliff'])
    return me

def join(name, meshes, col):
    """Join meshes into one object, merging material slots by name."""
    bm = bmesh.new(); slots = []
    for me in meshes:
        remap = []
        for m in me.materials:
            if m.name not in slots: slots.append(m.name)
            remap.append(slots.index(m.name))
        tmp = bmesh.new(); tmp.from_mesh(me)
        for f in tmp.faces: f.material_index = remap[f.material_index] if remap else 0
        tmp_me = bpy.data.meshes.new('tmp'); tmp.to_mesh(tmp_me); tmp.free()
        bm.from_mesh(tmp_me); bpy.data.meshes.remove(tmp_me)
    for f in bm.faces: f.smooth = False
    out = bpy.data.meshes.new(name); bm.to_mesh(out); bm.free()
    for s in slots: out.materials.append(bpy.data.materials[s])
    for me in meshes:
        if me.users == 0: bpy.data.meshes.remove(me)
    o = link(out, name, col); box_uv(o.data); return o

def box_uv(me, tile=2.0):
    bm = bmesh.new(); bm.from_mesh(me)
    uv = bm.loops.layers.uv.verify()
    for f in bm.faces:
        n = f.normal
        for l in f.loops:
            c = l.vert.co
            if abs(n.z) >= max(abs(n.x), abs(n.y)): l[uv].uv = (c.x / tile, c.y / tile)
            elif abs(n.x) > abs(n.y): l[uv].uv = (c.y / tile, c.z / tile)
            else: l[uv].uv = (c.x / tile, c.z / tile)
    bm.to_mesh(me); bm.free()

COLLIDERS = []
def collider(kind, name, me):
    o = link(me, 'U%s_%s' % (kind, name), 'Collision'); o.display_type = 'WIRE'
    o.data.materials.clear(); o.data.materials.append(MATS['Collision'])
    COLLIDERS.append(o.name); return o

MARKERS = []
def marker(name, x, y, z, **props):
    e = bpy.data.objects.new('MK_' + name, None); e.location = P(x, y, z); e.empty_display_size = 0.6
    COLS['Markers'].objects.link(e)
    MARKERS.append(dict(name='MK_' + name, position=[round(x, 3), round(y, 3), round(z, 3)], **props))
    return e

# --------------------------------------------------------------------------------------------
# Props
# --------------------------------------------------------------------------------------------
def spruce(x, z, s, seed, sink=0.35):
    r = random.Random(seed); y = ground(x, z) - sink
    parts = [prism_mesh('trunk', (x, y, z), (x, y + 2.2 * s, z), 0.22 * s, 6, ('Bark',))]
    tiers = 3 + (seed % 2); base = y + 1.4 * s
    for t in range(tiers):
        rad = (2.3 - t * 0.5) * s * (0.9 + r.random() * 0.2); hgt = (3.0 - t * 0.35) * s
        parts.append(prism_mesh('cone', (x, base, z), (x, base + hgt, z), rad, 7, ('Needles',), taper=0.0))
        base += hgt * 0.55
    return parts

def log(name, a, b, radius, col=True):
    parts = [prism_mesh(name, a, b, radius, 8, ('Bark',))]
    if col: collider('CP', name, prism_mesh(name, a, b, radius, 8, ('Collision',)))
    return parts

def lamp(name, x, z, light=True):
    y = ground(x, z)
    parts = [prism_mesh('post', (x, y - 0.3, z), (x, y + 3.2, z), 0.08, 6, ('Dark',)),
             box_mesh('head', (x, y + 3.35, z), (0.45, 0.4, 0.45), mats=('LampWarm',))]
    collider('CP', name, prism_mesh(name, (x, y - 0.3, z), (x, y + 3.0, z), 0.15, 6, ('Collision',)))
    if light: marker('Light_Lamp_' + name, x, y + 3.1, z, kind='lamp')
    return parts

def edge_lamp(name, lane, t, side):
    """Lamp on the bank foot of a lane, never inside the riding width."""
    (x, z), (dx, dz) = lane_point(lane, t); L = math.hypot(dx, dz); rx, rz = dz / L, -dx / L
    o = LANES[lane]['w'] / 2 + 0.5
    return lamp(name, x + rx * o * side, z + rz * o * side)

def fence(name, pts, height=1.1):
    """Fence of 4 m modules along a Unity (x, z) polyline. One box collider per module."""
    parts = []
    for k, ((ax, az), (bx, bz)) in enumerate(zip(pts, pts[1:])):
        L = math.hypot(bx - ax, bz - az); n = max(1, round(L / 4))
        for m in range(n):
            t0, t1 = m / n, (m + 1) / n
            x0, z0 = ax + (bx - ax) * t0, az + (bz - az) * t0; x1, z1 = ax + (bx - ax) * t1, az + (bz - az) * t1
            y0, y1 = ground(x0, z0), ground(x1, z1)
            parts.append(prism_mesh('post', (x0, y0 - 0.3, z0), (x0, y0 + height + 0.1, z0), 0.09, 4, ('Wood',)))
            for hr in (0.45, 0.95):
                parts.append(box_mesh('rail', ((x0 + x1) / 2, (y0 + y1) / 2 + hr * height, (z0 + z1) / 2),
                                      (0.08, 0.12, math.hypot(x1 - x0, z1 - z0) + 0.1), (x1 - x0, z1 - z0), ('Wood',)))
            collider('BX', '%s_%d_%d' % (name, k, m), box_mesh('c', ((x0 + x1) / 2, (y0 + y1) / 2 + height / 2, (z0 + z1) / 2),
                     (0.25, height + 0.6, math.hypot(x1 - x0, z1 - z0)), (x1 - x0, z1 - z0), ('Collision',)))
        parts.append(prism_mesh('post', (bx, ground(bx, bz) - 0.3, bz), (bx, ground(bx, bz) + height + 0.1, bz), 0.09, 4, ('Wood',)))
    return parts

def lane_point(name, t):
    """Point and direction at fraction t of a lane's length."""
    pts = LANES[name]['pts']; segs = list(zip(pts, pts[1:]))
    lens = [math.hypot(b[0] - a[0], b[1] - a[1]) for a, b in segs]; s = t * sum(lens)
    for (a, b), L in zip(segs, lens):
        if s <= L or (a, b) == segs[-1]:
            u = min(1, s / L); return (a[0] + (b[0] - a[0]) * u, a[1] + (b[1] - a[1]) * u), (b[0] - a[0], b[1] - a[1])
        s -= L

def gate(route, which, x, z, dx, dz, width):
    """Pillars, lintel, sign, barrier visual and the gameplay blocker for one route gate."""
    L = math.hypot(dx, dz); fx, fz = dx / L, dz / L; rx, rz = fz, -fx
    y = ground(x, z); parts = []
    for side in (-1, 1):
        px, pz = x + rx * side * (width / 2 + 0.4), z + rz * side * (width / 2 + 0.4)
        py = ground(px, pz)
        parts.append(box_mesh('pillar', (px, py + 2.2, pz), (0.8, 5.2, 0.8), (dx, dz), ('Wood',)))
        collider('BX', 'GatePillar_%s_%s_%d' % (route, which, side), box_mesh('c', (px, py + 2.2, pz), (0.8, 5.2, 0.8), (dx, dz), ('Collision',)))
    parts.append(box_mesh('lintel', (x, y + 4.7, z), (width + 1.6, 0.5, 0.6), (dx, dz), ('Wood',)))
    parts.append(box_mesh('sign', (x, y + 4.0, z - fz * 0.3), (min(width, 5), 0.8, 0.12), (dx, dz), ('Sign' + route,)))
    bars = [box_mesh('bar', (x + rx * o, y + 1.4, z + rz * o), (0.1, 2.8, 0.1), (dx, dz), ('Metal',))
            for o in np.arange(-width / 2 + 0.5, width / 2, 0.9)]
    bars.append(box_mesh('bar', (x, y + 2.7, z), (width, 0.15, 0.12), (dx, dz), ('Metal',)))
    join('GATEVIS_%s_%s' % (route, which), bars, 'Props')
    collider('BX', 'GATE_%s_%s' % (route, which), box_mesh('c', (x, y + 2.0, z), (width + 0.2, 4.5, 0.5), (dx, dz), ('Collision',)))
    marker('Gate_%s_%s' % (route, which), x, y, z, route=route, which=which, forward=[round(fx, 3), 0, round(fz, 3)], width=width)
    return parts

def wedge(name, a, b, half_w, rise, mats=('Wood',), collide=True):
    """Solid ramp from Unity point a (low end, on the ground) to b, rising `rise` at b. Base follows the ground."""
    (ax, az), (bx, bz) = a, b; L = math.hypot(bx - ax, bz - az); rx, rz = (bz - az) / L, -(bx - ax) / L
    pts = []
    for (x, z), top in (((ax, az), 0.02), ((bx, bz), rise)):
        for s in (-1, 1):
            px, pz = x + rx * half_w * s, z + rz * half_w * s; g = ground(px, pz)
            pts += [(px, g - 0.3, pz), (px, g + top, pz)]
    me = hull_mesh(name, pts, mats); o = link(me, name, 'Props'); box_uv(o.data, 1.5)
    if collide: collider('CX', name, hull_mesh(name, pts, ('Collision',)))
    return o

# --- Bellevue: start platform, fence with the view, bench, hut, lamps
bp = PADS['Bellevue']
props = fence('FenceBellevue', [(-9, 11), (-13, 7), (-13, -11), (13, -11), (13, 7), (9, 11)])
props += lamp('Bellevue_L', -10, 8) + lamp('Bellevue_R', 10, 8)
props.append(box_mesh('bench_seat', (-6, 40.45, -6), (2.2, 0.12, 0.6), (1, 0), ('Wood',)))
for o in (-0.9, 0.9): props.append(box_mesh('bench_leg', (-6 + o, 40.2, -6), (0.12, 0.5, 0.5), (1, 0), ('Wood',)))
collider('BX', 'Bench', box_mesh('c', (-6, 40.3, -6), (2.2, 0.7, 0.6), (1, 0), ('Collision',)))
props.append(prism_mesh('bottle', (-4.6, 40, -5.2), (-4.6, 40.3, -5.2), 0.05, 6, ('SignB',)))
props.append(box_mesh('hut', (8, 41.6, -7), (4.5, 3.2, 3.5), (1, 0), ('Wood',)))
props.append(hull_mesh('hut_roof', [(5.4, 43.2, -9), (10.6, 43.2, -9), (5.4, 43.2, -5), (10.6, 43.2, -5),
                                    (5.4, 44.6, -7), (10.6, 44.6, -7)], ('RedPaint',)))
collider('BX', 'Hut', box_mesh('c', (8, 41.6, -7), (4.5, 3.2, 3.5), (1, 0), ('Collision',)))
join('PR_Bellevue', props, 'Props')
marker('PlayerStart', 0, 40.05, -3, forward=[0, 0, 1])
for n, (x, y, z) in {'Intro_LowCamera': (0, 40.25, -7), 'Intro_Scooter': (2, 40, -6), 'Intro_GuiltSilhouette': (-30, 50, 40)}.items():
    marker(n, x, y, z, kind='intro')

# --- Safe ride: one small boost/jump bump, lamps
wedge('PR_SafeBump', (-2.5, 21), (-2.5, 25), 2.0, 0.6)
join('PR_Safe', lamp('Safe_1', 6.5, 24) + lamp('Safe_2', -6.5, 34), 'Props')

# --- Fork: sign post, three gates, lamps
fp = PADS['Fork']; props = []
SX, SZ = -7.5, 44.5
props.append(prism_mesh('signpost', (SX, 36.7, SZ), (SX, 40.2, SZ), 0.12, 6, ('Wood',)))
for k, (route, dx) in enumerate((('A', -1), ('B', 0), ('C', 1))):
    props.append(box_mesh('arrow', (SX + dx * 0.6, 39.6 - k * 0.55, SZ), (1.4, 0.4, 0.1), (1, 0), ('Sign' + route,)))
collider('CP', 'SignPost', prism_mesh('c', (SX, 36.7, SZ), (SX, 40.2, SZ), 0.25, 6, ('Collision',)))
props += gate('A', 'Front', -19.5, 51.8, -7, 2, 8.6)
props += gate('B', 'Front', 0, 56.5, 0, 1, 13.4)
props += gate('C', 'Front', 19.5, 51.8, 7, 2, 7.6)
props += lamp('Fork_L', -14, 42) + lamp('Fork_R', 14, 42)
join('PR_Fork', props, 'Props')

# --- Route A: Gozdna strmina. Tree slalom, logs, orange lamps.
props = []; slalom = []
for k, t in enumerate(np.linspace(0.12, 0.86, 7)):
    (x, z), (dx, dz) = lane_point('A', t); L = math.hypot(dx, dz); rx, rz = dz / L, -dx / L
    off = (2.0 if k % 2 == 0 else -2.0)
    slalom.append((x + rx * off, z + rz * off))
for k, t in enumerate((0.3, 0.62)):
    (x, z), (dx, dz) = lane_point('A', t); L = math.hypot(dx, dz); rx, rz = dz / L, -dx / L
    side = 1 if k == 0 else -1
    a = (x + rx * side * 4.6, ground(x, z) + 0.28, z + rz * side * 4.6); b = (x + rx * side * 1.3, ground(x, z) + 0.28, z + rz * side * 1.3)
    props += log('LogA_%d' % k, a, b, 0.32)
for k, t in enumerate((0.2, 0.5, 0.8)):
    props += edge_lamp('A_%d' % k, 'A', t, 1 if k % 2 else -1)
join('PR_RouteA', props, 'Props')

# --- Arena A: logging clearing. Slalom spruces, log stacks, boulders.
cxA, czA = PADS['ArenaA']['c']; props = []
slalom += [(cxA + dx, czA + dz) for dx, dz in [(-14, -10), (-4, -14), (8, -12), (15, 2), (-15, 10), (2, 2), (12, 14), (-6, 15)]]
for k, (x, z) in enumerate(slalom):
    s = 0.85 + rng.random() * 0.3
    trunk_y = ground(x, z)
    collider('CP', 'Spruce_%d' % k, prism_mesh('c', (x, trunk_y - 0.3, z), (x, trunk_y + 2.6 * s, z), 0.42 * s, 6, ('Collision',)))
    props += spruce(x, z, s, 100 + k)
for k, (dx, dz, yaw) in enumerate([(-3, -4, (1, 0.3)), (16, 12, (0.2, 1))]):
    x, z = cxA + dx, czA + dz; fx, fz = yaw; L = math.hypot(fx, fz); fx, fz = fx / L, fz / L
    for lvl, offs in ((0, (-0.65, 0, 0.65)), (1, (-0.33, 0.33))):
        for o in offs:
            px, pz = x + fz * o, z - fx * o; g = ground(x, z)
            props += log('Stack_%d_%d_%d' % (k, lvl, int(o * 10)), (px - fx * 2.5, g + 0.3 + lvl * 0.55, pz - fz * 2.5),
                         (px + fx * 2.5, g + 0.3 + lvl * 0.55, pz + fz * 2.5), 0.3)
for k, (dx, dz, s) in enumerate([(-17, -2, 1.6), (6, 10, 1.3), (-8, 4, 1.0), (17, -12, 1.4)]):
    x, z = cxA + dx, czA + dz; y = ground(x, z)
    props.append(rock_mesh('boulder', (x, y + s * 0.4, z), (s * 1.3, s, s * 1.2), 500 + k))
    collider('SP', 'Boulder_%d' % k, rock_mesh('c', (x, y + s * 0.4, z), (s * 1.1, s * 0.9, s * 1.1), 500 + k))
props += lamp('ArenaA_1', cxA - 18, czA - 16) + lamp('ArenaA_2', cxA + 18, czA + 16) + lamp('ArenaA_3', cxA - 18, czA + 16)
join('PR_ArenaA', props, 'Props')

# --- Route B: Gondolska servisna pot. Snow slope, broken-plank jumps, overturned carts, gondola line.
props = []
wedge('PR_PlankJump_1', (-4, 70), (-4, 74), 2.6, 1.0)
wedge('PR_PlankJump_2', (3.5, 84), (3.5, 88), 2.6, 1.0)
def cart(name, x, z, yaw):
    y = ground(x, z); parts = [box_mesh('cart', (x, y + 0.6, z), (1.8, 1.2, 2.8), yaw, ('RedPaint',))]
    for o in (-0.9, 0.9):
        parts.append(prism_mesh('wheel', (x - 0.95, y + 1.25, z + o), (x + 0.95, y + 1.25, z + o), 0.3, 8, ('Dark',)))
    collider('BX', name, box_mesh('c', (x, y + 0.75, z), (1.9, 1.5, 2.9), yaw, ('Collision',)))
    return parts
props += cart('CartB_1', 4.5, 66, (1, 0.4)) + cart('CartB_2', -4.8, 92, (0.6, 1))
cable_pts = []
for k, (x, z) in enumerate([(11, 30), (11.5, 64), (11.5, 94), (16, 128)]):
    y = ground(x, z); top = y + (12 if k < 3 else 9)
    props.append(box_mesh('pylon', (x, (y + top) / 2 - 0.2, z), (0.9, top - y + 0.6, 0.9), (0, 1), ('Metal',)))
    props.append(box_mesh('arm', (x - 2, top, z), (5, 0.35, 0.35), (1, 0), ('Metal',)))
    collider('BX', 'Pylon_%d' % k, box_mesh('c', (x, (y + top) / 2, z), (1.0, top - y, 1.0), (0, 1), ('Collision',)))
    cable_pts.append((x - 3.5, top - 0.1, z))
for (a, b) in zip(cable_pts, cable_pts[1:]):
    props.append(prism_mesh('cable', a, b, 0.04, 4, ('Dark',)))
for k, t in enumerate((0.45, 0.75)):
    a, b = cable_pts[k + 1], cable_pts[k + 2] if k + 2 < len(cable_pts) else cable_pts[-1]
    x, y, z = [a[i] + (b[i] - a[i]) * t for i in range(3)]
    props.append(prism_mesh('hanger', (x, y - 2.0, z), (x, y, z), 0.05, 4, ('Metal',)))
    props.append(box_mesh('cabin', (x, y - 3.2, z), (1.8, 2.0, 2.2), (0, 1), ('RedPaint',)))
    props.append(box_mesh('window', (x, y - 2.9, z - 1.12), (1.4, 0.7, 0.05), (0, 1), ('Dark',)))
    if k == 0: marker('Watcher_B', x, y - 1.9, z, route='B', kind='watcher on gondola, red cue'); marker('Light_Red_Watcher', x, y - 2.0, z, kind='red')
join('PR_RouteB', props, 'Props')

# --- Arena B: maintenance yard with a solid terminal, carts and crates.
cxB, czB = PADS['ArenaB']['c']; props = []
tx, tz = cxB + 12, czB + 8; ty = PADS['ArenaB']['h']
props.append(box_mesh('terminal', (tx, ty + 1.2, tz), (10, 3.0, 9), (0, 1), ('Concrete',)))
props.append(box_mesh('terminal_roof', (tx, ty + 6.2, tz), (11, 0.5, 10), (0, 1), ('Metal',)))
for ox in (-4.8, 4.8):
    for oz in (-4.3, 4.3):
        props.append(box_mesh('roof_post', (tx + ox, ty + 4.4, tz + oz), (0.35, 3.4, 0.35), (0, 1), ('RedPaint',)))
        collider('BX', 'RoofPost_%d_%d' % (ox, oz), box_mesh('c', (tx + ox, ty + 4.4, tz + oz), (0.4, 3.4, 0.4), (0, 1), ('Collision',)))
collider('BX', 'Terminal', box_mesh('c', (tx, ty + 1.2, tz), (10, 3.0, 9), (0, 1), ('Collision',)))
wedge('PR_TerminalRamp', (tx, tz - 16), (tx, tz - 4.5), 2.5, 2.7, ('Concrete',))
props += cart('CartYard_1', cxB - 10, czB - 6, (1, 0.2)) + cart('CartYard_2', cxB - 4, czB + 12, (0.3, 1))
for k, (dx, dz) in enumerate([(-16, 10), (-15, 12.2), (7, -14), (17, -10)]):
    x, z = cxB + dx, czB + dz; y = ground(x, z)
    props.append(box_mesh('crate', (x, y + 0.6, z), (1.2, 1.2, 1.2), (1, 0.2 * k), ('Wood',)))
    collider('BX', 'Crate_%d' % k, box_mesh('c', (x, y + 0.6, z), (1.2, 1.2, 1.2), (1, 0.2 * k), ('Collision',)))
props += lamp('ArenaB_1', cxB - 19, czB - 17) + lamp('ArenaB_2', cxB + 19, czB - 17) + lamp('ArenaB_3', cxB - 19, czB + 17)
join('PR_ArenaB', props, 'Props')

# --- Route C: Rave tunel. Cut-and-cover concrete tunnel along lane segments, grate window, neon.
props = []; neon = []
C = LANES_RAW['C']; tunnel = [C[3], C[4], C[5], C[6]]
WALL_T, CLEAR_H = 0.8, 4.6
half = LANES['C']['w'] / 2
def sloped_slab(name, a, b, off0, off1, bottom, top, mats, coll=True, ext=0.0):
    """Closed slab between lane nodes a, b; lateral offsets off0..off1; heights relative to the lane floor."""
    (ax, az, ah), (bx, bz, bh) = a, b; L = math.hypot(bx - ax, bz - az); fx, fz = (bx - ax) / L, (bz - az) / L
    rx, rz = fz, -fx; pts = []
    for (x, z, h), e in (((ax, az, ah), -ext), ((bx, bz, bh), ext)):
        x, z = x + fx * e, z + fz * e
        for o in (off0, off1):
            for y in (bottom, top): pts.append((x + rx * o, h + y, z + rz * o))
    me = hull_mesh(name, pts, mats); props.append(me)
    if coll: collider('CX', name, hull_mesh(name, pts, ('Collision',)))
for k, (a, b) in enumerate(zip(tunnel, tunnel[1:])):
    for side in (-1, 1):
        o0, o1 = side * (half - WALL_T), side * (half + 2.6)   # buried into the bank: no gap behind the wall
        if k == 1 and side == 1:      # grate window into the reveal alcove (Story: Guilt seen through a grate)
            (ax, az, ah), (bx, bz, bh) = a, b
            m1 = [a[i] + (b[i] - a[i]) * 0.38 for i in range(3)]; m2 = [a[i] + (b[i] - a[i]) * 0.62 for i in range(3)]
            sloped_slab('TunnelWallR_%d_a' % k, a, m1, o0, o1, -0.6, CLEAR_H, ('Concrete',), ext=0.4)
            sloped_slab('TunnelWallR_%d_b' % k, m2, b, o0, o1, -0.6, CLEAR_H, ('Concrete',), ext=0.4)
            sloped_slab('TunnelWallR_%d_top' % k, m1, m2, o0, o1, 2.8, CLEAR_H, ('Concrete',))
            Lg = math.hypot(b[0] - a[0], b[1] - a[1]); rxg, rzg = (b[1] - a[1]) / Lg, -(b[0] - a[0]) / Lg
            for t in np.linspace(0.40, 0.60, 7):
                px, pz, ph = (a[i] + (b[i] - a[i]) * t for i in range(3))
                gx_, gz_ = px + rxg * (half - 0.4), pz + rzg * (half - 0.4)
                props.append(prism_mesh('grate_bar', (gx_, ph - 0.2, gz_), (gx_, ph + 2.8, gz_), 0.05, 4, ('Metal',)))
            (ax, az, ah) = m1; (bx, bz, bh) = m2
            L = math.hypot(bx - ax, bz - az); rx, rz = (bz - az) / L, -(bx - ax) / L
            collider('BX', 'Grate_C', box_mesh('c', ((ax + bx) / 2 + rx * (half - 0.4), (ah + bh) / 2 + 1.4, (az + bz) / 2 + rz * (half - 0.4)),
                     (0.3, 2.9, L + 0.2), (bx - ax, bz - az), ('Collision',)))
            marker('GuiltReveal_C', PADS['AlcoveC']['c'][0], PADS['AlcoveC']['h'], PADS['AlcoveC']['c'][1], route='C', kind='seen through grate')
        else:
            sloped_slab('TunnelWall%s_%d' % ('LR'[side > 0], k), a, b, o0, o1, -0.6, CLEAR_H, ('Concrete',), ext=0.4)
    sloped_slab('TunnelRoof_%d' % k, a, b, -half - 2.6, half + 2.6, CLEAR_H, CLEAR_H + 0.7, ('Concrete',), ext=0.4)
    for side in (-1, 1):
        sloped_slab('neon', a, b, side * (half - WALL_T - 0.06), side * (half - WALL_T), 1.0, 1.15, ('NeonPurple',), coll=False, ext=-0.5)
    for t in (0.25, 0.75):
        px, pz, ph = (a[i] + (b[i] - a[i]) * t for i in range(3))
        marker('Light_Neon_Tunnel_%d_%d' % (k, int(t * 100)), px, ph + 3.5, pz, kind='neon')
# Reveal alcove behind the grate: closed concrete box room on its carved pad.
ax_, az_ = PADS['AlcoveC']['c']; ah_ = PADS['AlcoveC']['h']
for nm, (dx, dz, sx, sz) in {'back': (2.6, 0, 0.8, 5.6), 'n': (0, -2.4, 6, 0.8), 's': (0, 2.4, 6, 0.8)}.items():
    props.append(box_mesh('alcove_' + nm, (ax_ + dx, ah_ + 1.8, az_ + dz), (sx, 4.2, sz), (0, 1), ('Concrete',)))
    collider('BX', 'Alcove_' + nm, box_mesh('c', (ax_ + dx, ah_ + 1.8, az_ + dz), (sx, 4.2, sz), (0, 1), ('Collision',)))
props.append(box_mesh('alcove_roof', (ax_, ah_ + 4.2, az_), (6.2, 0.6, 5.6), (0, 1), ('Concrete',)))
marker('Light_Red_Alcove', ax_, ah_ + 2.5, az_, kind='red')
# Portal frames and posters
for k, (p, q) in enumerate(((tunnel[0], tunnel[1]), (tunnel[-1], tunnel[-2]))):
    sloped_slab('PortalFrame_%d' % k, p, [p[0] + (q[0] - p[0]) * 0.08, p[1] + (q[1] - p[1]) * 0.08, p[2] + (q[2] - p[2]) * 0.08],
                -half - 3.0, half + 3.0, CLEAR_H - 0.2, CLEAR_H + 1.6, ('Concrete',), ext=0.3)
join('PR_RouteC', props, 'Props')

# --- Arena C: cistern rave. Concrete walls with two openings, columns, solid stage with ramps.
cxC, czC = PADS['ArenaC']['c']; hC = PADS['ArenaC']['h']; hxC, hzC = PADS['ArenaC']['half']; props = []
def wall_run(name, x0, z0, x1, z1):
    L = math.hypot(x1 - x0, z1 - z0)
    if L < 0.5: return
    props.append(box_mesh(name, ((x0 + x1) / 2, hC + 2.6, (z0 + z1) / 2), (1.0, 6.4, L), (x1 - x0, z1 - z0), ('Concrete',)))
    collider('BX', name, box_mesh('c', ((x0 + x1) / 2, hC + 2.6, (z0 + z1) / 2), (1.0, 6.4, L), (x1 - x0, z1 - z0), ('Collision',)))
W0, W1 = cxC - hxC + 0.5, cxC + hxC - 0.5; N, S = czC - hzC + 0.5, czC + hzC - 0.5
entry_x, exit_x = 56.5, 61.5
wall_run('CWall_N1', W0, N, entry_x - 5, N); wall_run('CWall_N2', entry_x + 5, N, W1, N)
wall_run('CWall_S1', W0, S, exit_x - 5, S); wall_run('CWall_S2', exit_x + 5, S, W1, S)
wall_run('CWall_W', W0, N, W0, S); wall_run('CWall_E', W1, N, W1, S)
for k, (dx, dz) in enumerate([(-9, -8), (3, -8), (-9, 8), (3, 8)]):
    x, z = cxC + dx, czC + dz
    props.append(box_mesh('column', (x, hC + 2.5, z), (1.6, 7.0, 1.6), (0, 1), ('Concrete',)))
    props.append(box_mesh('column_neon', (x, hC + 2.0, z - 0.82), (0.2, 4.0, 0.05), (0, 1), ('NeonPurple',)))
    collider('BX', 'Column_%d' % k, box_mesh('c', (x, hC + 2.5, z), (1.6, 7.0, 1.6), (0, 1), ('Collision',)))
    marker('Light_Neon_Column_%d' % k, x, hC + 3, z - 1.5, kind='neon')
sx, sz = cxC + 14, czC; props.append(box_mesh('stage', (sx, hC + 0.3, sz), (6, 1.8, 14), (0, 1), ('Metal',)))
collider('BX', 'Stage', box_mesh('c', (sx, hC + 0.3, sz), (6, 1.8, 14), (0, 1), ('Collision',)))
wedge('PR_StageRamp_N', (sx, sz - 13), (sx, sz - 7), 2.2, 1.2, ('Metal',))
wedge('PR_StageRamp_S', (sx, sz + 13), (sx, sz + 7), 2.2, 1.2, ('Metal',))
for side in (-1, 1):
    x, z = sx + 1.5, sz + side * 5.2
    props.append(box_mesh('speaker', (x, hC + 2.8, z), (2.0, 3.2, 1.8), (0, 1), ('Dark',)))
    props.append(box_mesh('speaker_glow', (x - 1.02, hC + 2.8, z), (0.05, 2.4, 1.2), (0, 1), ('NeonPurple',)))
    collider('BX', 'Speaker_%d' % side, box_mesh('c', (x, hC + 2.8, z), (2.0, 3.2, 1.8), (0, 1), ('Collision',)))
props.append(box_mesh('dj_booth', (sx - 1.5, hC + 1.7, sz), (1.2, 1.0, 4.5), (0, 1), ('RedPaint',)))
collider('BX', 'DJBooth', box_mesh('c', (sx - 1.5, hC + 1.7, sz), (1.2, 1.0, 4.5), (0, 1), ('Collision',)))
for k, z in enumerate(np.linspace(N + 4, S - 4, 4)):
    props.append(box_mesh('wall_neon', (W0 + 0.52, hC + 3.0, z), (0.05, 0.25, 5), (0, 1), ('NeonPurple',)))
marker('Light_Neon_Stage', sx - 3, hC + 4, sz, kind='neon')
join('PR_ArenaC', props, 'Props')

# --- Exit lanes and the overlook: lamps, fence on the cliff edge, invisible wall above it.
props = edge_lamp('ExitA', 'ExitA', 0.55, 1) + edge_lamp('ExitB', 'ExitB', 0.5, 1) + edge_lamp('ExitC', 'ExitC', 0.55, -1)
props += fence('FenceOverlook', [(-16, 187), (-16, 193.6), (16, 193.6), (16, 187)])
props += lamp('Overlook_L', -12, 190) + lamp('Overlook_R', 12, 190)
join('PR_Overlook', props, 'Props')
marker('ExitForest', 0, 14, 189, kind='one-way scene transition; persist Forest C for next run', size=[30, 6, 8])

# --- Rocks hugging every bank so the carved walls read as rock, not smooth ramps.
rocks = []
for name, lane in LANES.items():
    pts = lane['pts']
    for (ax, az, ah), (bx, bz, bh) in zip(pts, pts[1:]):
        L = math.hypot(bx - ax, bz - az); n = int(L / 5)
        for k in range(n):
            t = (k + rng.random()) / max(n, 1); x, z = ax + (bx - ax) * t, az + (bz - az) * t
            rx, rz = (bz - az) / L, -(bx - ax) / L; side = rng.choice((-1, 1)); s = 0.7 + rng.random() * 0.8
            off = lane['w'] / 2 + BANK_WIDTH * 0.6 + s * 1.2
            px, pz = x + rx * side * off, z + rz * side * off
            if inside_feature(np.array([px]), np.array([pz]))[1][0] < s * 1.3 + 0.3: continue   # keep clear of every floor
            rocks.append(rock_mesh('rock', (px, ground(px, pz) + s * 0.1, pz), (s * 1.2, s * 0.9, s * 1.1), rng.randrange(10 ** 6)))
join('PR_BankRocks', rocks, 'Props')

# --- Scenic forest on the unreachable ground, baked into 40 m chunks.
forest = {}; placed = []
for attempt in range(9000):
    x = rng.uniform(GRID['x0'] + 3, GRID['x1'] - 3); z = rng.uniform(GRID['z0'] + 3, GRID['z1'] - 6)
    d = inside_feature(np.array([x]), np.array([z]))[1][0]
    if d < BANK_WIDTH + 1.2: continue
    if any((x - px) ** 2 + (z - pz) ** 2 < 16 for px, pz in placed[-400:]): continue
    placed.append((x, z))
    s = 0.8 + rng.random() * 0.8
    forest.setdefault((math.floor(x / 40), math.floor(z / 40)), []).extend(spruce(x, z, s, rng.randrange(10 ** 6), sink=0.6))
    if len(placed) >= 1100: break
for key, parts in forest.items():
    join('FOREST_%d_%d' % key, parts, 'Forest')

# --- Backdrop: distant mountain ring and the Maribor skyline (no collision, behind fog).
bm = bmesh.new(); ring = []
for k in range(40):
    a = k / 40 * math.tau; r = 210 + 25 * math.sin(k * 1.7)
    cx_, cz_ = 0 + math.sin(a) * r, 100 + math.cos(a) * r
    lowh = -60; peak = 22 + 32 * abs(math.sin(k * 0.9)) - (45 if math.cos(a) > 0.6 else 0)
    ring.append((bm.verts.new(P(cx_, lowh, cz_)), bm.verts.new(P(cx_, peak, cz_))))
for (a0, a1), (b0, b1) in zip(ring, ring[1:] + ring[:1]):
    bm.faces.new((a0, b0, b1, a1))
bm.normal_update()
centre = P(0, 0, 100)
for f in bm.faces:   # face the player (inside of the ring)
    if f.normal.dot(centre - f.calc_center_median()) < 0: f.normal_flip()
me = bpy.data.meshes.new('BACKDROP_Mountains'); bm.to_mesh(me); bm.free(); me.materials.append(MATS['Dark'])
for p in me.polygons: p.use_smooth = False
link(me, 'BACKDROP_Mountains', 'Backdrop')
bm = bmesh.new()
v = [bm.verts.new(P(x, y, 275)) for x, y in ((-230, -45), (230, -45), (230, 85), (-230, 85))]
f = bm.faces.new(v); uvl = bm.loops.layers.uv.new('UVMap')
for l, uvv in zip(f.loops, ((0, 0), (1, 0), (1, 1), (0, 1))): l[uvl].uv = uvv
me = bpy.data.meshes.new('BACKDROP_Skyline'); bm.to_mesh(me); bm.free(); me.materials.append(MATS['Skyline'])
link(me, 'BACKDROP_Skyline', 'Backdrop')

# --- Gameplay markers: combat spaces, charges, spawns, Guilt anchors, authored events.
for k, n in (('A', 'ArenaA'), ('B', 'ArenaB'), ('C', 'ArenaC')):
    p = PADS[n]; cx_, cz_ = p['c']; hx_, hz_ = p['half']
    marker('CombatSpace_' + k, cx_, p['h'], cz_, route=k, size=[hx_ * 2, 12, hz_ * 2])
    charges = [(-0.6, -0.3), (0.2, 0.6), (0.2, -0.65)] if k == 'C' else [(-0.6, -0.3), (0.55, 0.1), (-0.1, 0.65)]
    for i, (fx, fz) in enumerate(charges, 1):
        x, z = cx_ + fx * hx_, cz_ + fz * hz_; marker('Charge_%s_%d' % (k, i), x, ground(x, z) + 0.5, z, route=k)
    for i, (fx, fz) in enumerate([(-0.75, -0.75), (0.75, -0.75), (-0.75, 0.75), (0.75, 0.75)], 1):
        x, z = cx_ + fx * hx_, cz_ + fz * hz_; marker('Spawn_%s_%d' % (k, i), x, ground(x, z), z, route=k)
for k in ('A', 'B', 'C'):
    for i, t in enumerate((0.25, 0.55, 0.85), 1):
        (x, z), _ = lane_point(k, t); marker('Guilt_%s_%d' % (k, i), x, ground(x, z), z, route=k)
(x, z), _ = lane_point('A', 0.97); marker('GuiltReveal_A', x, ground(x, z), z, route='A', kind='appears behind player at clearing end')
marker('GuaranteedPickup_C', cxC + 8, hC + 0.5, czC - 12, route='C')
for i, (dx, dz) in enumerate([(-6, 4), (6, -10)], 1):
    marker('StrongerEnemy_C_%d' % i, cxC + dx, ground(cxC + dx, czC + dz), czC + dz, route='C')

# --- Invisible play-area walls (the carved banks are the primary containment).
b = PLAY_BOUNDS
for nm, c, s in [('N', ((b['x0'] + b['x1']) / 2, 40, b['z0']), (b['x1'] - b['x0'], 120, 1)),
                 ('S', ((b['x0'] + b['x1']) / 2, 40, b['z1']), (b['x1'] - b['x0'], 120, 1)),
                 ('W', (b['x0'], 40, (b['z0'] + b['z1']) / 2), (1, 120, b['z1'] - b['z0'])),
                 ('E', (b['x1'], 40, (b['z0'] + b['z1']) / 2), (1, 120, b['z1'] - b['z0']))]:
    collider('BX', 'INV_Edge_' + nm, box_mesh('c', c, s, (0, 1), ('Collision',)))

# Flat shading + box UVs on every prop.
for o in COLS['Props'].objects:
    for p in o.data.polygons: p.use_smooth = False
    if o.name.startswith('PR_'): box_uv(o.data)
for o in list(COLS['Forest'].objects) + list(COLS['Terrain'].objects):
    for p in o.data.polygons: p.use_smooth = False

# --------------------------------------------------------------------------------------------
# Validation
# --------------------------------------------------------------------------------------------
report = dict(scene=scene.name, terrain=dict(triangles=terrain_tris, chunks=len(TERRAIN_CHUNKS),
              interior_holes=holes, nonmanifold_edges=nonmanifold), features={}, lanes={}, props={})
for kind, n in FEATURES:
    if kind == 'pad':
        p = PADS[n]; cx_, cz_ = p['c']; hx_, hz_ = p['half']
        sx_, sz_ = np.meshgrid(np.linspace(cx_ - hx_ + 1.5, cx_ + hx_ - 1.5, 15), np.linspace(cz_ - hz_ + 1.5, cz_ + hz_ - 1.5, 15))
        sx_, sz_ = sx_.ravel(), sz_.ravel(); inside = sd_pad(sx_, sz_, p) < -1.0
        sx_, sz_ = sx_[inside], sz_[inside]; want = pad_floor(n, sx_, sz_)
    else:
        L = LANES[n]; samples = []
        for (ax, az, ah), (bx, bz, bh) in zip(L['pts'], L['pts'][1:]):
            for t in np.linspace(0, 1, 12):
                samples.append((ax + (bx - ax) * t, az + (bz - az) * t, ah + (bh - ah) * t))
        sx_, sz_, want = (np.array(c) for c in zip(*samples))
    got = np.array([ground(x, z) for x, z in zip(sx_, sz_)])
    # Points shared with another floor at the same height are fine; report the worst mismatch.
    dev = float(np.max(np.abs(got - want))) if len(got) else 0.0
    report['features'][n] = dict(max_floor_deviation_m=round(dev, 3), samples=int(len(got)))
for n, L in LANES.items():
    grades = [abs(bh - ah) / math.hypot(bx - ax, bz - az) for (ax, az, ah), (bx, bz, bh) in zip(L['pts'], L['pts'][1:])]
    report['lanes'][n] = dict(width_m=L['w'], max_grade_deg=round(math.degrees(math.atan(max(grades))), 1),
                              length_m=round(sum(math.hypot(b[0] - a[0], b[1] - a[1]) for a, b in zip(L['pts'], L['pts'][1:])), 1))
# Bank steepness: sample just outside every lane edge; it must exceed the 45 deg slope limit somewhere in the rise.
steep_ok = 0; steep_total = 0
for n, L in LANES.items():
    for (ax, az, ah), (bx, bz, bh) in zip(L['pts'], L['pts'][1:]):
        Ls = math.hypot(bx - ax, bz - az); rx, rz = (bz - az) / Ls, -(bx - ax) / Ls
        for t in (0.3, 0.7):
            x, z = ax + (bx - ax) * t, az + (bz - az) * t
            for side in (-1, 1):
                e = L['w'] / 2
                p0 = (x + rx * side * (e + 0.6), z + rz * side * (e + 0.6)); p1 = (x + rx * side * (e + 1.6), z + rz * side * (e + 1.6))
                if inside_feature(np.array([p1[0]]), np.array([p1[1]]))[1][0] < 0.5: continue   # opens into another floor
                steep_total += 1
                if math.degrees(math.atan((ground(*p1) - ground(*p0)) / 1.0)) > 45: steep_ok += 1
report['banks'] = dict(samples=steep_total, steeper_than_slope_limit=steep_ok)
report['props'] = dict(colliders=len(COLLIDERS), markers=len(MARKERS), forest_trees=len(placed),
                       render_triangles=sum(len(o.data.polygons) for c in ('Props', 'Forest', 'Terrain') for o in COLS[c].objects
                                            if o.type == 'MESH'))
with open(os.path.join(HERE, 'validation.json'), 'w') as fh: json.dump(report, fh, indent=1)
with open(os.path.join(HERE, 'layout.json'), 'w') as fh:
    json.dump(dict(coordinates='Unity metres: x right, y up, z forward', bank=dict(rise=BANK_RISE, width=BANK_WIDTH),
                   pads=PADS, lanes=LANES, play_bounds=PLAY_BOUNDS, markers=MARKERS,
                   # Flat lists for Unity's JsonUtility (PohorjeMapChecks): lane pts are x, z, h triples.
                   unity=dict(lanes=[dict(name=n, width=L['w'], pts=[c for p in L['pts'] for c in p]) for n, L in LANES.items()],
                              pads=[dict(name=n, cx=p['c'][0], cz=p['c'][1], hx=p['half'][0], hz=p['half'][1], h=p['h'])
                                    for n, p in PADS.items()])), fh, indent=1)
print('POHORJE_VALIDATION', json.dumps(report))

# --------------------------------------------------------------------------------------------
# Export
# --------------------------------------------------------------------------------------------
def export(fname, cols, types={'MESH'}):
    bpy.ops.object.select_all(action='DESELECT')
    for c in cols:
        for o in COLS[c].objects: o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(UNITY, fname), use_selection=True, object_types=types,
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True, apply_unit_scale=True,
                             mesh_smooth_type='FACE', use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False,
                             path_mode='STRIP', use_triangles=True)
export('Pohorje_Terrain.fbx', ['Terrain'])
export('Pohorje_Props.fbx', ['Props', 'Forest', 'Backdrop'])
export('Pohorje_Collision.fbx', ['Collision'])
export('Pohorje_Markers.fbx', ['Markers'], {'EMPTY'})
bpy.context.preferences.filepaths.save_version = 0   # no .blend1 backups next to the source
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, 'Source', 'Pohorje_v2.blend'))
print('POHORJE_DONE')

# --------------------------------------------------------------------------------------------
# Previews (Workbench, textured) for review; Unity captures are the source of truth for the final look.
# --------------------------------------------------------------------------------------------
def preview(name, eye, target, lens=24):
    cam_data = bpy.data.cameras.new('cam'); cam_data.lens = lens; cam_data.clip_end = 1200
    cam = bpy.data.objects.new('PreviewCam', cam_data); scene.collection.objects.link(cam)
    cam.location = P(*eye); d = P(*target) - P(*eye); cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    scene.camera = cam
    scene.render.engine = 'BLENDER_WORKBENCH'; scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'TEXTURE'; scene.display.shading.show_shadows = True
    scene.render.resolution_x, scene.render.resolution_y = 1280, 720
    scene.render.filepath = os.path.join(HERE, 'Previews', name + '.png')
    COLS['Collision'].hide_render = True; COLS['Markers'].hide_render = True
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam)
try:
    preview('01-overview', (-150, 150, -60), (0, 20, 110))
    preview('02-top', (0, 330, 95), (0, 0, 96), lens=18)
    preview('03-bellevue', (0, 43, -9), (0, 37, 40), lens=20)
    preview('04-fork', (0, 41, 40), (0, 35, 60), lens=18)
except Exception as exc:   # previews are optional in headless environments
    print('POHORJE_PREVIEW_SKIPPED', exc)
