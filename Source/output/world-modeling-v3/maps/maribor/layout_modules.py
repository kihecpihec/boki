"""Construct the unbatched Maribor module layout for the v3 production builder.

Run with Blender 4.4.3 or newer:  blender --background --factory-startup --python build_maribor.py
Never opens or edits Pohorje or Unity scenes. Refuses to overwrite an artist's source unless MB_FORCE=1.
Third pass: overlap-free placement (props and facades test the collision guides) and a horror grade.
MB_DEV=1 builds to /tmp, skips exports and renders (fast iteration). MB_PREVIEWS=1,3 renders only those cameras.
"""
import bpy
import bmesh
import math
import json
import os
import random
import sys
from pathlib import Path
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import maribor_kit as K  # noqa: E402

OUT = K.OUT
DEV = os.environ.get('MB_DEV') == '1'
SOURCE = Path('/tmp/maribor_dev.blend') if DEV else OUT / 'Source/Maribor.blend'
if SOURCE.exists() and not DEV and os.environ.get('MB_FORCE') != '1':
    raise RuntimeError('Maribor.blend exists. Preserve artist edits (or set MB_FORCE=1 to replace it from the builder).')
for directory in ('Source', 'Export', 'Textures', 'Previews'):
    (OUT / directory).mkdir(exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for pool in (bpy.data.meshes, bpy.data.materials, bpy.data.images):
    for block in list(pool):
        pool.remove(block)
scene = bpy.context.scene
scene.name = 'Maribor'
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
rng = random.Random(759)
hrng = random.Random(1313)   # horror dressing decisions; kept apart from `rng` so layout stays stable

WZ = -4.0           # Drava water level
GZ = -0.08          # ground top around the paved routes
X0, X1, Y0, Y1 = -68., 86., -52., 154.   # island plinth
BX0, BX1, BY0, BY1 = 12., 44., 22., 118.  # basin


def collection(name):
    c = bpy.data.collections.new('MB_' + name)
    scene.collection.children.link(c)
    return c


shared = collection('Shared_Arrival_FERI')
routes = {k: collection('Route_' + k) for k in 'ABC'}
scenic = collection('Scenery')
roofs = collection('C_Removable_Roofs')
library = collection('Module_Library')
guides = collection('Collision_Guides')
markers = collection('Gameplay_Markers')
presentation = collection('Presentation')
kit = K.Kit(scene, guides, markers)
kit.library_c = library
box, mesh, guide, mark = kit.box, kit.mesh, kit.guide, kit.mark

# ---------------------------------------------------------------------------------------------
# Materials
# ---------------------------------------------------------------------------------------------
T = K.build_textures()
M = {}
for name, img in T.items():
    M[name] = K.material(name, img)
M['Stucco'] = [M['Stucco_%d' % i] for i in range(4)]
M['Iron'] = K.material('Iron', color=(.07, .08, .1), rough=.6)
M['Ivory'] = K.material('Ivory', color=(.5, .45, .35))
M['Glass'] = K.material('DarkGlass', color=(.05, .08, .11), rough=.25)
M['WindowLit'] = K.material('WindowLit', T['WarmWindow'], emit=2.0, emit_from_image=True)
M['LampGlow'] = K.material('Lamp', color=(1, .5, .18), emit=6, emit_color=(1, .45, .15))
M['LampDead'] = K.material('LampDead', color=(.1, .1, .11), rough=.4)
M['Blood'] = K.material('DriedBlood', color=(.16, .025, .02), rough=.35)
M['Poster'] = K.material('Poster', T['Poster'])
M['Plank'] = K.material('Plank', T['Wood'])
M['Leafpile'] = K.material('LeafPile', color=(.2, .12, .06))
M['Bag'] = K.material('TrashBag', color=(.03, .035, .04), rough=.35)
M['Crow'] = K.material('Crow', color=(.015, .015, .02), rough=.5)
M['Secret'] = K.material('Secret', T['SecretLattice'], emit=2.4, emit_from_image=True)
M['SecretGlow'] = K.material('SecretGlow', color=(.55, .2, .9), emit=4, emit_color=(.6, .25, .95))
M['Charge'] = K.material('Charge', color=(.48, .85, .18), emit=3, emit_color=(.55, .95, .2))
M['Fluoro'] = K.material('Fluorescent', color=(.8, .9, 1), emit=6, emit_color=(.8, .9, 1))
M['Ember'] = K.material('EmberGlow', color=(1, .2, .06), emit=4, emit_color=(1, .2, .06))
M['Red'] = K.material('RedLight', color=(.9, .1, .05), emit=6, emit_color=(1, .1, .05))
M['Sign'] = K.material('SignWhite', color=(.9, .88, .78), emit=.6, emit_color=(.9, .85, .7))
M['Hedge'] = K.material('Hedge', T['Leaf'])
M['Ivy'] = K.material('Ivy', T['Leaf'])
M['Shutter'] = K.material('Shutter', color=(.1, .17, .14))
M['Door'] = K.material('DoorWood', T['Wood'])
M['Trunk'] = K.material('Trunk', T['Bark'])
M['Bin'] = K.material('BinGreen', color=(.12, .27, .2))
M['Water'] = K.material('DravaWater', T['Drava'], emit=.45, emit_from_image=True, rough=.4)
kit.iron = M['Iron']


def relocate(o, c):
    for old in list(o.users_collection):
        old.objects.unlink(o)
    c.objects.link(o)
    return o


def text(name, label, pos, size, mat, c, rotation=(math.pi / 2, 0, 0)):
    data = bpy.data.curves.new(name, 'FONT')
    data.body = label
    data.align_x = 'CENTER'
    data.size = size
    data.extrude = .02
    o = bpy.data.objects.new(name, data)
    c.objects.link(o)
    o.location = pos
    o.rotation_euler = rotation
    o.data.materials.append(mat)
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    bpy.ops.object.convert(target='MESH')
    o.select_set(False)
    return o


def instance(proto, name, pos, c, angle=0., scale=1.):
    o = bpy.data.objects.new(name, proto.data)
    c.objects.link(o)
    o.location = pos
    o.rotation_euler.z = angle
    o.scale = (scale,) * 3
    return o


def yaw_facing(f):
    """Rotation about Z so a module's local -Y (front) points along f."""
    return math.atan2(f.x, -f.y)


def yaw_along(d):
    """Rotation about Z so a box's local +Y (length) runs along d."""
    return math.atan2(-d.x, d.y)


def local_yaw(d):
    """Rotation about Z so a module's local +X runs along d."""
    return math.atan2(d.y, d.x)


# ---------------------------------------------------------------------------------------------
# Module library (linked instances share mesh data)
# ---------------------------------------------------------------------------------------------
def window(parts, x, z, w, h, mat, sill=False, shutters=False, boards=0):
    parts.append(box('Trim', (x, -.09, z), (w + .28, .14, h + .28), M['Ivory'], library))
    parts.append(box('Glass', (x, -.14, z), (w, .06, h), mat, library))
    for k in range(boards):  # nailed-up planks across a dead window
        pz = z - h / 2 + (k + .6) * h / (boards + .2)
        parts.append(box('Plank', (x + (k % 2 - .5) * .12, -.24 - k * .01, pz), (w + .5, .05, .17), M['Plank'], library))
    if sill:
        parts.append(box('Sill', (x, -.2, z - h / 2 - .14), (w + .5, .32, .12), M['Ashlar'], library))
    if shutters:
        for s in (-1, 1):
            parts.append(box('Shutter', (x + s * (w / 2 + .3), -.16, z), (.3, .05, h + .15), M['Shutter'], library))


def make_facade(i, stucco, lit_seed, garage=False, shutters=False):
    r = random.Random(lit_seed)
    D = 8.
    parts = [box('Wall', (0, D / 2, 3.3), (4, D, 6.6), M['Stucco'][stucco], library),
             box('Plinth', (0, -.04, .42), (4.06, .3, .84), M['Ashlar'], library),
             box('Cornice', (0, -.16, 6.5), (4.06, .5, .24), M['Ivory'], library)]
    glass = lambda: M['WindowLit'] if r.random() < .2 else M['Glass']

    def pane():
        g = glass()
        return g, (r.choice((2, 3)) if g is M['Glass'] and r.random() < .5 else 0)
    if garage:
        parts.append(box('Garage', (0, -.14, 1.35), (3.0, .12, 2.5), M['Door'], library))
        parts.append(box('GarageFrame', (0, -.06, 1.45), (3.3, .1, 2.7), M['Ivory'], library))
    else:
        parts.append(box('Door', (0, -.08, 1.2), (1.0, .14, 2.4), M['Door'], library))
        parts.append(box('DoorStep', (0, -.35, .12), (1.5, .55, .24), M['Ashlar'], library))
        for x in (-1.3, 1.3):
            g, nb = pane()
            window(parts, x, 2.4, .8, 1.15, g, sill=True, shutters=shutters, boards=nb)
    for x in (-1.3, 0, 1.3):
        g, nb = pane()
        window(parts, x, 5.0, .7, 1.1, g, shutters=shutters and x != 0, boards=nb)
    # Dressing only goes where no trim, sill or frame exists: corner strips and the band between window rows.
    for sx in (-1, 1):
        if r.random() < .55:
            parts.append(box('Ivy', (sx * 1.93, -.04, 2.9 + r.uniform(0, .6)), (.14, .08, r.uniform(3.4, 4.4)), M['Ivy'], library))
    if r.random() < .5:
        parts.append(box('Ivy', (r.uniform(-1, 1), -.04, 3.75), (r.uniform(1., 2.), .06, .6), M['Ivy'], library))
    elif r.random() < .8:
        parts.append(box('Poster', (r.choice((-1.3, 1.3)), -.05, 3.75), (.5, .03, .7), M['Poster'], library))
    parts.append(kit.gable('Roof', (0, D / 2, 6.6), (4, D, 0), (0, .5), 2.6, M['Terracotta'], library))
    parts.append(box('Chimney', (1.2 if i % 2 else -1.2, 5.6, 8.6), (.55, .55, 1.4), M['Ashlar'], library))
    parts.append(box('ChimneyCap', (1.2 if i % 2 else -1.2, 5.6, 9.36), (.7, .7, .12), M['Iron'], library))
    return kit.join(parts, 'ENV_C_Facade_%d' % i)


facade_specs = [(0, 11, False, False), (1, 12, False, True), (2, 13, True, False), (3, 14, False, False),
                (0, 15, False, True), (1, 16, False, False), (2, 17, False, True), (3, 18, True, False)]
facades = [make_facade(i, *spec) for i, spec in enumerate(facade_specs)]


def make_car(name, body, flipped=False):
    p = [box('Lower', (0, 0, .62), (1.8, 3.9, .62), body, library),
         box('Hood', (0, -1.3, .98), (1.7, 1.1, .12), body, library),
         box('RoofPanel', (0, -.1, 1.55), (1.5, 1.4, .1), body, library),
         box('Cabin', (0, -.1, 1.25), (1.62, 2.1, .5), M['Glass'], library),
         box('Bumper_F', (0, -2.0, .45), (1.84, .16, .3), M['Iron'], library),
         box('Bumper_B', (0, 2.0, .45), (1.84, .16, .3), M['Iron'], library)]
    for x in (-.65, .65):
        p.append(box('Headlamp', (x, -1.98, .85), (.4, .08, .2), M['Ivory'], library))
        p.append(box('Taillamp', (x, 1.98, .85), (.4, .08, .18), M['Red'], library))
    for x in (-.9, .9):
        for y in (-1.25, 1.25):
            p.append(kit.cylinder('Wheel', (x, y, .36), .36, .24, M['Iron'], library, rot=(0, math.pi / 2, 0)))
    return kit.join(p, name)


car_blue = make_car('ENV_C_CompactCar', M['CarBlue'])
car_red = make_car('ENV_C_CompactCar_Red', M['CarRed'])
lamp_proto = kit.join([box('Foot', (0, 0, .35), (.55, .55, .7), M['Ashlar'], library),
                       kit.cylinder('Post', (0, 0, 2.1), .08, 2.8, M['Iron'], library, verts=8),
                       box('Arm', (.2, 0, 3.45), (.5, .08, .08), M['Iron'], library),
                       box('Lantern', (0, 0, 3.75), (.44, .44, .6), M['LampGlow'], library),
                       kit.cone('Cap', (0, 0, 4.05), .4, .38, M['Iron'], library, verts=4)], 'ENV_C_Streetlamp')
lamp_dead_proto = kit.join([box('Foot', (0, 0, .35), (.55, .55, .7), M['Ashlar'], library),
                            kit.cylinder('Post', (0, 0, 2.1), .08, 2.8, M['Iron'], library, verts=8),
                            box('Arm', (.2, 0, 3.45), (.5, .08, .08), M['Iron'], library),
                            box('Lantern', (0, 0, 3.75), (.44, .44, .6), M['LampDead'], library),
                            kit.cone('Cap', (0, 0, 4.05), .4, .38, M['Iron'], library, verts=4)], 'ENV_C_Streetlamp_Dead')
crow_proto = kit.join([box('Body', (0, 0, .16), (.15, .32, .15), M['Crow'], library),
                       box('Head', (0, -.2, .27), (.1, .11, .1), M['Crow'], library),
                       box('Beak', (0, -.28, .26), (.035, .08, .035), M['Iron'], library),
                       box('Tail', (0, .22, .15), (.12, .16, .03), M['Crow'], library),
                       box('WingL', (-.09, .02, .18), (.03, .28, .1), M['Crow'], library),
                       box('WingR', (.09, .02, .18), (.03, .28, .1), M['Crow'], library),
                       box('LegL', (-.03, 0, .04), (.015, .015, .08), M['Iron'], library),
                       box('LegR', (.03, 0, .04), (.015, .015, .08), M['Iron'], library)], 'ENV_C_Crow')
rail_parts = [box('PostL', (-2, 0, .6), (.28, .32, 1.2), M['Ashlar'], library),
              box('PostR', (2, 0, .6), (.28, .32, 1.2), M['Ashlar'], library)]
for z in (.45, 1.0):
    rail_parts.append(box('Rail', (0, 0, z), (4, .1, .1), M['Iron'], library))
for x in (-1.5, -1, -.5, 0, .5, 1, 1.5):
    rail_parts.append(box('Bar', (x, 0, .72), (.05, .06, .62), M['Iron'], library))
rail_proto = kit.join(rail_parts, 'ENV_C_RiverRail_4m')
jersey = kit.join([box('Base', (0, 0, .2), (2.4, .8, .4), M['Concrete'], library),
                   box('Body', (0, 0, .62), (2.3, .5, .45), M['Hazard'], library),
                   box('Cap', (0, 0, .95), (2.2, .3, .2), M['Concrete'], library)], 'ENV_C_JerseyBarrier')
bin_proto = kit.join([box('Bin', (0, 0, .45), (.62, .62, .9), M['Bin'], library),
                      box('Lid', (0, 0, .94), (.7, .7, .1), M['Iron'], library)], 'ENV_C_Bin')


def blob(name, pos, radius, mat, c, squash=1., seed=0):
    r = random.Random(seed)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=1, radius=radius)
    for v in bm.verts:
        v.co *= Vector((1, 1, squash)) * r.uniform(.85, 1.1)
    data = bpy.data.meshes.new(name)
    bm.to_mesh(data)
    bm.free()
    o = bpy.data.objects.new(name, data)
    c.objects.link(o)
    o.location = pos
    data.materials.append(mat)
    uv = data.uv_layers.new()
    for f in data.polygons:
        for li in f.loop_indices:
            q = data.vertices[data.loops[li].vertex_index].co
            uv.data[li].uv = (q.x / 2, q.y / 2 + q.z / 2)
    return o


def make_trees():
    t = {}
    parts = [kit.cylinder('Trunk', (0, 0, 1.6), .22, 3.2, M['Trunk'], library, verts=6, top_radius=.14)]
    parts += [blob('Crown', (0, 0, 5.4), 2.1, M['Leaf'], library, 1.25, 1), blob('Crown', (.6, .3, 7.2), 1.5, M['Leaf'], library, 1.2, 2)]
    t['round'] = kit.join(parts, 'ENV_C_TreeRound')
    parts = [kit.cylinder('Trunk', (0, 0, 1.5), .2, 3., M['Trunk'], library, verts=6)]
    for k, (z, r_) in enumerate([(3.0, 1.4), (5.0, 1.15), (7.0, .85), (8.8, .5)]):
        parts.append(kit.cone('Tier', (0, 0, z), r_, 2.6 - k * .2, M['Leaf'], library, verts=7))
    t['poplar'] = kit.join(parts, 'ENV_C_TreeCypress')
    parts = [kit.cylinder('Trunk', (0, 0, 1.8), .24, 3.6, M['Trunk'], library, verts=6, top_radius=.1)]
    for k, (ang, tilt, ln) in enumerate([(0, .8, 2.2), (1.5, .9, 2.0), (3, .75, 2.4), (4.4, .95, 1.8), (5.5, .6, 1.5)]):
        parts.append(kit.cylinder('Branch', (math.cos(ang) * .7, math.sin(ang) * .7, 3.7 + k * .35), .06, ln, M['Trunk'], library, verts=5,
                                  rot=(math.sin(ang) * tilt, -math.cos(ang) * tilt, 0), top_radius=.02))
    t['dead'] = kit.join(parts, 'ENV_C_TreeDead')
    # A second, taller dead tree: forked trunk and long reaching limbs.
    parts = [kit.cylinder('Trunk', (0, 0, 2.6), .26, 5.2, M['Trunk'], library, verts=6, top_radius=.12)]
    for k, (ang, tilt, ln, z) in enumerate([(.4, .9, 3.0, 4.2), (2.1, 1.05, 2.6, 4.6), (3.7, .8, 3.2, 3.8), (5.2, 1.0, 2.2, 5.0), (1.2, .55, 1.8, 5.4)]):
        parts.append(kit.cylinder('Limb', (math.cos(ang) * ln * .35, math.sin(ang) * ln * .35, z + ln * .3), .07, ln, M['Trunk'], library, verts=5,
                                  rot=(math.sin(ang) * tilt, -math.cos(ang) * tilt, 0), top_radius=.015))
    t['dead2'] = kit.join(parts, 'ENV_C_TreeDeadTall')
    t['bush'] = blob('ENV_C_Bush', (0, 0, .5), .9, M['Leaf'], library, .75, 4)
    return t


trees = make_trees()

# Optional: reuse Pohorje's baked spruce modules for the foothills behind the city.
spruces = []
for index in range(3):
    parts=[kit.cylinder('SpruceTrunk',(0,0,2.0),.17,4.0,M['Trunk'],library,verts=6)]
    for tier in range(3):
        parts.append(kit.cone('SpruceNeedles',(0,0,1.5+tier*1.4),1.6-tier*.35,3.2,M['Leaf'],library,verts=6))
    spruces.append(kit.join(parts,'ENV_C_HillSpruce_'+str(index)))

# ---------------------------------------------------------------------------------------------
# Placement helpers
# ---------------------------------------------------------------------------------------------
lamp_spots = []
tree_spots = []
skipped = []
GUIDE_SKIP = ('TerraceWall',)   # the terrace slab is modelled as a full-height wall, but props stand on its top


def clear(x, y, r, z=0., names=None):
    """True when a r-metre circle at (x, y) touches no solid collision guide taller than the prop's footing."""
    for g in kit.collision:
        if g['dynamic'] or g['name'].startswith('COL_Floor_'):
            continue
        base = g['name'][4:].split('.')[0]
        if base in GUIDE_SKIP or (names and base not in names):
            continue
        cx, cy, cz = g['center']
        w, d, h = g['size']
        if cz + h / 2 < z + .3:
            continue
        rot = g.get('rotation_z', 0)
        dx, dy = x - cx, y - cy
        lx = dx * math.cos(rot) + dy * math.sin(rot)
        ly = -dx * math.sin(rot) + dy * math.cos(rot)
        qx, qy = max(abs(lx) - w / 2, 0), max(abs(ly) - d / 2, 0)
        if qx * qx + qy * qy < r * r:
            return False
    return True


CROWN = {'round': 2.2, 'poplar': 1.5, 'dead': 2.0, 'dead2': 2.6}


def lamp_at(x, y, c, z=0., light=False):
    for dx, dy in ((0, 0), (-.9, 0), (.9, 0), (0, -.9), (0, .9)):   # a lamp that lands on a rail or wall steps aside
        if clear(x + dx, y + dy, .45, z) and not any(math.hypot(x + dx - tx, y + dy - ty) < cr * .7 for tx, ty, cr in tree_spots):
            x, y = x + dx, y + dy
            break
    else:
        skipped.append(('Lamp', round(x, 1), round(y, 1)))
        return
    dead = not light and hrng.random() < .45
    instance(lamp_dead_proto if dead else lamp_proto, 'Streetlamp_Dead' if dead else 'Streetlamp', (x, y, z), c)
    if dead and hrng.random() < .4:
        instance(crow_proto, 'Crow', (x + .38, y, z + 3.49), c, hrng.random() * 6.28)
    guide('Lamp', (x, y, z + 1.4), (.6, .6, 2.8), c.name)
    if light:
        lamp_spots.append((x, y, z + 3.7))


def tree_at(kind, x, y, c, z=0., scale=1., solid=True):
    angle = rng.random() * 6.28
    if kind != 'dead' and hrng.random() < (.6 if kind == 'round' else .5):
        kind = hrng.choice(('dead', 'dead2'))
    crown = CROWN[kind] * scale
    # Trunk must clear every solid; the crown must also clear walls, containers and lamps.
    if not clear(x, y, .5 * scale, z) or not clear(x, y, crown, z, names=('FacadeBay', 'ParkHouse', 'Container', 'Lamp')) \
            or any(math.hypot(x - tx, y - ty) < .9 for tx, ty, _ in tree_spots):
        skipped.append(('Tree', round(x, 1), round(y, 1)))
        if z > 0 and clear(x, y, .9 * scale, z):   # a planter keeps a low scrub bush instead of an empty bed
            return instance(trees['bush'], 'Bush', (x, y, z), c, angle, scale)
        return None
    tree_spots.append((x, y, crown))
    o = instance(trees[kind], 'Tree', (x, y, z), c, angle, scale)
    if solid:
        guide('TreeTrunk', (x, y, z + 1.5), (.7, .7, 3.), c.name)
    return o


def rail_seg(p0, p1, c, tall=5., rail=True, name='Boundary'):
    d = p1 - p0
    n = max(1, int(round(d.length / 4.)))
    u = d / n
    ang = local_yaw(u)
    if rail:
        for i in range(n):
            m = p0 + u * (i + .5)
            instance(rail_proto, 'RiverRail', (m.x, m.y, 0), c, ang)
    mid = (p0 + p1) / 2
    guide(name, (mid.x, mid.y, tall / 2), (d.length, .45, tall), c.name, rot=ang)


def rail_pts(pts, c, **kw):
    for a, b in zip(pts, pts[1:]):
        rail_seg(Vector((a[0], a[1], 0)), Vector((b[0], b[1], 0)), c, **kw)


def rail_path(path, lateral, c, i0=0, i1=None, step=4, **kw):
    idx = list(range(i0, (len(path.pts) if i1 is None else i1), step))
    pts = [path.pts[i] + path.normal(i) * lateral for i in idx]
    for a, b in zip(pts, pts[1:]):
        rail_seg(a, b, c, **kw)


def _rect_overlap(a, b):
    """Penetration depth (m) of two rotated rectangles (cx, cy, w, d, rot) by the separating-axis test; 0 when apart."""
    def corners(r):
        cx, cy, w, d, rot = r
        c, s_ = math.cos(rot), math.sin(rot)
        return [(cx + px * c - py * s_, cy + px * s_ + py * c) for px, py in ((-w / 2, -d / 2), (w / 2, -d / 2), (w / 2, d / 2), (-w / 2, d / 2))]
    ca, cb = corners(a), corners(b)
    depth = 1e9
    for rot in (a[4], b[4]):
        for ax in ((math.cos(rot), math.sin(rot)), (-math.sin(rot), math.cos(rot))):
            pa = [p[0] * ax[0] + p[1] * ax[1] for p in ca]
            pb = [p[0] * ax[0] + p[1] * ax[1] for p in cb]
            o = min(max(pa), max(pb)) - max(min(pa), min(pb))
            if o <= 0:
                return 0.
            depth = min(depth, o)
    return depth


def building_fits(ctr, ang, tol=.2):
    """A 4 x 8 m bay may not sink more than `tol` metres into another building."""
    me = (ctr.x, ctr.y, 4.05, 8., ang)
    for g in kit.collision:
        if g['name'][4:].split('.')[0] in ('FacadeBay', 'ParkHouse'):
            other = (g['center'][0], g['center'][1], g['size'][0], g['size'][1], g.get('rotation_z', 0))
            if _rect_overlap(me, other) > tol:
                return False
    return True


def bays_along(path, side, off, i_ranges, c, rows_seed):
    """Row of 4 m facade bays facing the path; side +1 = left of travel. A bay that would sink into a neighbour slides on."""
    r = random.Random(rows_seed)
    placed = 0
    for i0, i1 in i_ranges:
        i = i0
        while i + 4 <= i1:
            p, t, n = path.at((i + 2) * path.ds)
            base = p + n * side * off
            f = -n * side
            ang = yaw_facing(f)
            ctr = base - f * 4
            if not building_fits(ctr, ang):
                i += 1
                continue
            instance(r.choice(facades), 'FacadeBay', (base.x, base.y, 0), c, ang)
            guide('FacadeBay', (ctr.x, ctr.y, 3.9), (4.05, 8., 7.8), c.name, rot=ang)
            placed += 1
            i += 4
    return placed


def facade_straight(x, y0, y1, f, c, seed):
    r = random.Random(seed)
    f = Vector((f[0], f[1], 0))
    ang = yaw_facing(f)
    for y in range(int(y0), int(y1), 4):
        base = Vector((x, y + 2, 0))
        ctr = base - f * 4
        proto = r.choice(facades)
        if not building_fits(ctr, ang):
            skipped.append(('FacadeBay', round(base.x, 1), round(base.y, 1)))
            continue
        instance(proto, 'FacadeBay', base, c, ang)
        guide('FacadeBay', (ctr.x, ctr.y, 3.9), (4.05, 8., 7.8), c.name, rot=ang)


# ---------------------------------------------------------------------------------------------
# Island plinth, rock skirt, water, quay walls
# ---------------------------------------------------------------------------------------------
def skirt(p0, p1, outward, mat, c, ztop=GZ, zbot=-14., jag=1.4, seg=4., seed=0):
    r = random.Random(seed)
    p0, p1 = Vector((p0[0], p0[1], 0)), Vector((p1[0], p1[1], 0))
    d = p1 - p0
    if Vector((-d.y, d.x, 0)).dot(Vector((outward[0], outward[1], 0))) < 0:
        p0, p1 = p1, p0
        d = -d
    n = max(1, int(round(d.length / seg)))
    out = Vector((outward[0], outward[1], 0)).normalized()
    rows = [ztop, ztop * .4 + zbot * .6 if False else ztop - 3.0, ztop - 7.5, zbot]
    verts = []
    for i in range(n + 1):
        base = p0 + d * (i / n)
        for k, z in enumerate(rows):
            amt = 0 if (k == 0 or jag == 0) else r.uniform(-jag, jag) * (1 + k * .4)
            verts.append((base.x + out.x * amt, base.y + out.y * amt, z))
    faces = []
    m = len(rows)
    for i in range(n):
        for k in range(m - 1):
            a = i * m + k
            faces.append((a, a + m, a + m + 1, a + 1))
    return mesh('Skirt', verts, faces, mat, c, tile=6.)


def plinth():
    for name, (x0, y0, x1, y1) in dict(W=(X0, Y0, BX0, Y1), N=(BX0, Y0, X1, BY0), E=(BX1, BY0, X1, Y1)).items():
        box('Ground_' + name, ((x0 + x1) / 2, (y0 + y1) / 2, GZ - 1), (x1 - x0, y1 - y0, 2), M['Pave'], scenic, tile=5.)
    rock, quay = M['Rock'], M['QuayStone']
    skirt((X0, Y0), (X1, Y0), (0, -1), rock, scenic, seed=1)
    skirt((X0, Y0), (X0, Y1), (-1, 0), rock, scenic, seed=2)
    skirt((X1, Y0), (X1, Y1), (1, 0), rock, scenic, seed=3)
    skirt((X0, Y1), (BX0, Y1), (0, 1), rock, scenic, seed=4)
    skirt((BX1, Y1), (X1, Y1), (0, 1), rock, scenic, seed=5)
    skirt((BX0, BY1), (BX0, Y1), (1, 0), rock, scenic, seed=6)
    skirt((BX1, BY1), (BX1, Y1), (-1, 0), rock, scenic, seed=7)
    # Basin quay walls (clean ashlar) and the open water strip under the causeway.
    skirt((BX0, BY0), (BX0, BY1), (1, 0), quay, scenic, jag=0, seed=8)
    skirt((BX1, BY0), (BX1, BY1), (-1, 0), quay, scenic, jag=0, seed=9)
    skirt((BX0, BY0), (BX1, BY0), (0, 1), quay, scenic, jag=0, seed=10)
    # River plane, wide enough to reach every bank.
    box('Water', (8, 40, WZ - .5), (900, 900, 1), M['Water'], scenic, tile=10.)
    for name, (lx0, ly0, lx1, ly1) in dict(Park=(-27, 26, -7.5, 118), NorthW=(-66, -12, -18, 11), NorthE=(18, -12, 62, 11), SouthW=(-66, 128, -9, 153.97), SouthE=(50, 128, 85.97, 153.97)).items():
        box('Lawn_' + name, ((lx0 + lx1) / 2, (ly0 + ly1) / 2, GZ + .005), (lx1 - lx0, ly1 - ly0, .06), M['Grass'], scenic, tile=5.)
    box('BasinBed', (28, 70, -15), (34, 100, .5), M['Rock'], scenic)


plinth()

# ---------------------------------------------------------------------------------------------
# Shared arrival: plaza, landmark building, fork promenade
# ---------------------------------------------------------------------------------------------
sh = shared
kit.floor_box('Arrival', 0, 2, 34, 30, M['Cobble'], sh)
kit.floor_box('Fork', 6, 18, 112, 12, M['Cobble'], sh, z=.002)
kit.floor_box('Merge', 8, 121, 128, 12, M['Cobble'], sh, thick=.7)
kit.floor_box('FERI', 0, 138, 16, 24, M['Cobble'], sh, z=.003)
mark('CityArrival', (0, 0, 0), 'PlayerStart')
mark('ExitCity', (0, 144, 0), 'SceneTransition', target='FERI', persist_city_c='next_run')

# Plaza inlay and fountain (decorative; fountain is a solid obstacle off the start line).
box('PlazaInlay', (0, 2, .006), (14, 14, .01), M['Slab'], sh, tile=3.)
kit.cylinder('FountainBasin', (0, -7, .45), 3.4, .9, M['Ashlar'], sh, verts=12)
kit.cylinder('FountainWater', (0, -7, .91), 3.0, .06, M['Water'], sh, verts=12)
kit.cylinder('FountainColumn', (0, -7, 1.6), .35, 2.2, M['Ashlar'], sh, verts=8)
kit.cylinder('FountainBowl', (0, -7, 2.7), 1.2, .3, M['Ashlar'], sh, verts=10)
guide('Fountain', (0, -7, 1.5), (6.8, 6.8, 3.), sh.name)
for x in (-12, 12):
    lamp_at(x, -8, sh, light=True)
    lamp_at(x, 12, sh, light=True)
for x in (-14, 14):
    box('Planter', (x, -2, .35), (2.4, 2.4, .7), M['Ashlar'], sh, solid=True)
    tree_at('round', x, -2, sh, z=.7, solid=False)

# Raised terrace and landmark hall behind the plaza (scenery only).
TZ = 2.4
box('Terrace', (0, -32, TZ / 2 - .04), (70, 34, TZ), M['Ashlar'], sh)
guide('TerraceWall', (0, -30, 1.8), (70, 36, 3.6), sh.name)
for i in range(8):  # steps up to the terrace (decorative; the wall guide keeps them closed)
    box('Step', (0, -14.4 - i * .62, .15 * (i + 1) + .1), (14, .62, .3 * (i + 1) + .2), M['Ashlar'], sh)
hall = [box('HallWing', (0, -39, TZ + 5.5), (52, 14, 11), M['Stucco'][1], sh),
        box('HallBase', (0, -39, TZ + .5), (52.6, 14.6, 1.1), M['Ashlar'], sh),
        box('Pavilion', (0, -34, TZ + 7.5), (16, 16, 15), M['Stucco'][2], sh),
        box('PavilionBase', (0, -34, TZ + .6), (16.6, 16.6, 1.2), M['Ashlar'], sh),
        kit.gable('Pediment', (0, -34, TZ + 15), (16, 16, 0), (.5, .8), 3.4, M['Terracotta'], sh, rot=math.pi / 2),
        kit.gable('WingRoof', (0, -39, TZ + 11), (52, 14, 0), (.6, .9), 3.4, M['Terracotta'], sh),
        box('Tower', (0, -37, TZ + 18), (5.2, 5.2, 10), M['Stucco'][2], sh),
        kit.gable('TowerRoof', (0, -37, TZ + 23), (5.2, 5.2, 0), (.3, .3), 3., M['Terracotta'], sh, rot=0),
        box('ClockFace', (0, -34.3, TZ + 19.5), (2.6, .2, 2.6), M['Sign'], sh)]
def clock_hand(name, deg, length, width, mat):
    """Flat hand on the tower clock; deg is clockwise from 12 as seen from the plaza (+Y side)."""
    a = math.radians(deg)
    u, v = math.sin(a), math.cos(a)
    pts = [(-v * width, u * width), (v * width, -u * width), (v * width + u * length, -u * width + v * length), (-v * width + u * length, u * width + v * length)]
    # (u, v) are viewer right/up; viewer right is world -X.
    return mesh(name, [(-px, -34.18, TZ + 19.5 + py) for px, py in pts], [(0, 1, 2, 3)], mat, sh)


clock_hand('ClockHour', 239, .75, .07, M['Iron'])      # the watch never moved past 07:58
clock_hand('ClockMinute', 348, 1.1, .05, M['Iron'])
box('PortalGlow', (0, -25.76, TZ + 2.6), (3.2, .1, 4.4), M['WindowLit'], sh)
box('PortalFrame', (0, -25.9, TZ + 2.7), (4.2, .16, 5.2), M['Ivory'], sh)
for x in (-6.5, -2.4, 2.4, 6.5):
    box('Pilaster', (x, -25.7, TZ + 7.4), (.9, .6, 12.4), M['Ivory'], sh)
for x in range(-23, 24, 4):
    if abs(x) < 10:
        continue
    for k, z in enumerate((TZ + 2.8, TZ + 6.2, TZ + 9.2)):
        lit = rng.random() < .5
        box('HallWindowTrim', (x, -31.95, z), (1.5, .14, 1.9), M['Ivory'], sh)
        box('HallWindow', (x, -31.9, z), (1.1, .1, 1.5), M['WindowLit'] if lit else M['Glass'], sh)
for k, x in enumerate((-4.5, 0, 4.5)):
    box('PavilionWindowTrim', (x, -25.95, TZ + 8.2), (1.6, .14, 2.2), M['Ivory'], sh)
    box('PavilionWindow', (x, -25.9, TZ + 8.2), (1.2, .1, 1.8), M['WindowLit'], sh)
    box('PavilionWindowTrim', (x, -25.95, TZ + 12.2), (1.6, .14, 2.0), M['Ivory'], sh)
    box('PavilionWindow', (x, -25.9, TZ + 12.2), (1.2, .1, 1.6), M['Glass'], sh)
for x in (-30, 30):
    tree_at('poplar', x, -16, sh, solid=False, scale=1.2)
    tree_at('round', x * 1.4, -22, sh, z=TZ, solid=False, scale=1.1)
for x in range(-32, 33, 8):
    lamp_at(x, -17.5, sh, z=TZ - .1) if abs(x) > 8 else None
# Balustrade line along the front of the terrace.
for x in range(-34, 35, 4):
    if abs(x) > 8:
        instance(rail_proto, 'RiverRail', (x + 2, -15.6, TZ), sh, 0)

# Plaza and promenade boundaries.
rail_pts([(-17, 12), (-50, 12), (-50, 24)], sh)
rail_pts([(17, 12), (62, 12), (62, 24)], sh)
rail_pts([(-17, -13), (-17, 12)], sh)
rail_pts([(17, -13), (17, 12)], sh)
guide('PlazaNorthWallW', (-26, -14, 2), (18, .6, 4), sh.name)
guide('PlazaNorthWallE', (26, -14, 2), (18, .6, 4), sh.name)
guide('PlazaNorthMid', (0, -15.7, 1.8), (16, 3, 3.6), sh.name)
for x in range(-46, 62, 10):
    if abs(x) > 18 or True:
        lamp_at(x, 14, sh, light=(x % 20 == 4 or abs(x) < 6))
# Hedges and lawns north of the promenade.
for x0, x1 in ((-50, -18), (18, 62)):
    box('PromenadeHedge', ((x0 + x1) / 2, 10.6, .55), (x1 - x0, 1.2, 1.1), M['Hedge'], sh)
    for x in range(int(x0) + 6, int(x1) - 4, 12):
        tree_at('round' if (x // 12) % 2 else 'poplar', x, 5 + rng.uniform(-1, 1), scenic, scale=rng.uniform(.9, 1.3), solid=False)

# Promenade south wall between route gates.
wall_gaps = [(-50, -46), (-38, -6.5), (6.5, 49), (55, 62)]
for a, b in wall_gaps:
    rail_pts([(a, 24.4), (b, 24.4)], sh, tall=5.)
    box('GateWall', ((a + b) / 2, 25.1, .7), (b - a, 1.2, 1.4), M['Ashlar'], sh)

# ---------------------------------------------------------------------------------------------
# A / Tabor: curving street, facade rows, slalom, court
# ---------------------------------------------------------------------------------------------
a = routes['A']
pathA = K.Path([(-42, 24), (-42, 34), (-40.8, 46), (-41.6, 56), (-42, 63), (-42, 78), (-42, 93), (-42.4, 104), (-41.4, 114), (-42, 123)])
K.ribbon(kit, 'Floor_A_Street', pathA, 9, M['Asphalt'], a)
kit.floors.append(bpy.data.objects['Floor_A_Street'])
kit.floor_box('A_Court', -42, 78, 24, 28, M['Asphalt'], a, z=.004)
court_idx = [i for i, p in enumerate(pathA.pts) if 62 <= p.y <= 94]
ci0, ci1 = court_idx[0], court_idx[-1]
for side in (1, -1):
    o = K.ribbon(kit, 'Floor_A_Sidewalk', pathA, 2.2, M['Cobble'], a, z=.003, lateral=side * 5.6, end=ci0 * pathA.ds)
    kit.floors.append(o)
    o = K.ribbon(kit, 'Floor_A_Sidewalk', pathA, 2.2, M['Cobble'], a, z=.003, lateral=side * 5.6, start=ci1 * pathA.ds)
    kit.floors.append(o)
end_idx = next(i for i, p in enumerate(pathA.pts) if p.y >= 113.2)   # bays stop short of the merge-promenade rail at y 115.4
rows_ranges = [(2, ci0 - 2), (ci1 + 2, end_idx)]
nA = sum(bays_along(pathA, s, 6.95, rows_ranges, a, 70 + k) for k, s in enumerate((1, -1)))
facade_straight(-54.6, 64, 92, (1, 0), a, 81)
facade_straight(-29.4, 64, 92, (-1, 0), a, 82)
# lane dashes
for i in range(4, len(pathA.pts) - 4, 6):
    if ci0 - 2 <= i <= ci1 + 2:
        continue
    p = pathA.pts[i]
    box('RoadDash', (p.x, p.y, .015), (.14, 2.2, .02), M['Ivory'], a, rot=yaw_along(pathA.tangent(i)))
for y in range(66, 92, 6):
    box('CourtDash', (-42, y + 1, .015), (.14, 2.4, .02), M['Ivory'], a)
# slalom: parked cars and barriers alternate across the 8 m street
for s, lat, kind in [(7, 3.2, 'b'), (16, -3.2, 'r'), (24, 3.2, 'j'), (31, -3.2, 'b'), (38, 3.2, 'j')]:
    p, t, n = pathA.at(s)
    pos = p + n * lat
    if kind == 'j':
        ang = local_yaw(t) + math.pi / 2
        instance(jersey, 'JerseyBarrier', (pos.x, pos.y, 0), a, ang)
        guide('JerseyBarrier', (pos.x, pos.y, .5), (2.4, .8, 1.0), a.name, rot=ang)
    else:
        ang = yaw_along(t) + (0 if lat > 0 else math.pi)
        instance(car_blue if kind == 'b' else car_red, 'ParkedCar', (pos.x, pos.y, 0), a, ang)
        guide('Car', (pos.x, pos.y, .85), (2, 4, 1.7), a.name, rot=yaw_along(t))
for x, y, k in [(-50, 70, 'b'), (-34, 87, 'r'), (-50.5, 87, 'b')]:
    instance(car_blue if k == 'b' else car_red, 'ParkedCar', (x, y, 0), a, 0)
    guide('Car', (x, y, .85), (2, 4, 1.7), a.name)
o = instance(car_red, 'OverturnedCar', (-33.6, 76, 1.55), a, 0)
o.rotation_euler.y = math.pi
guide('OverturnedCar', (-33.6, 76, .8), (2, 4, 1.6), a.name)
ramp = mesh('CarRamp', [(-35.6, 69, 0), (-31.6, 69, 0), (-35.6, 73.6, 1.4), (-31.6, 73.6, 1.4), (-35.6, 73.6, 0), (-31.6, 73.6, 0)],
            [(0, 1, 3, 2), (2, 3, 5, 4), (0, 2, 4), (1, 5, 3), (0, 4, 5, 1)], M['Concrete'], a)
ramp['collision'] = 'Static convex wedge or surface mesh; separate from car'
mark('A_Ramp', (-33.6, 69, 0), 'OptionalRamp', 'A', rise_m=1.4, run_m=4.6)
# sidewalk props
for s, lat in [(10, 5.3), (22, -5.3), (34, 5.3), (46, -5.3)]:
    p, t, n = pathA.at(s)
    lamp_at(p.x + n.x * lat, p.y + n.y * lat, a, light=(s in (10, 34)))
for s, lat in [(14, 5.2), (40, -5.2)]:
    p, t, n = pathA.at(s)
    tree_at('dead', p.x + n.x * lat, p.y + n.y * lat, a, scale=1.1)
for s in range(8, int(pathA.length) - 8, 18):
    p, t, n = pathA.at(s)
    if 62 <= p.y <= 94:
        continue
    for lat in (-5.4, 5.4):
        if p.y > 94 and lat < 0 and s % 36 == 8:
            continue
        q = p + n * lat
        if lat > 0 and p.y < 62 and s % 36 == 8:
            continue
        lamp_at(q.x, q.y, a, light=(s % 36 == 26 and lat > 0))
for x, y in [(-52.6, 65.3), (-31.4, 90.4), (-52.6, 90.4)]:
    box('CourtPlanter', (x, y, .35), (2.2, 2.2, .7), M['Ashlar'], a, solid=True)
    tree_at('round', x, y, a, z=.7, solid=False, scale=.9)
for x, y in [(-53.2, 80), (-30.8, 66.5)]:
    instance(bin_proto, 'Bin', (x, y, 0), a)
    guide('Bin', (x, y, .45), (.7, .7, .9), a.name)
for s, lat in [(12, 5.3), (30, -5.3)]:
    p, t, n = pathA.at(s)
    instance(bin_proto, 'Bin', (p.x + n.x * lat, p.y + n.y * lat, 0), a)
# A closing alleys / garden: houses between A and B and park trees
for y in (42, 100):
    for k, yy in enumerate((y - 2, y + 2)):
        instance(facades[(k + y // 10) % 8], 'FacadeBay', (-12, yy, 0), a, yaw_facing(Vector((1, 0, 0))))
    guide('ParkHouse', (-16, y, 3.9), (8, 8.1, 7.8), a.name)
for x, y, k in [(-24, 36, 'round'), (-22, 52, 'poplar'), (-24, 60, 'round'), (-20, 72, 'poplar'), (-14, 78, 'round'),
                (-20, 86, 'poplar'), (-24, 110, 'round'), (-16, 112, 'poplar'), (-16, 58, 'round'), (-12, 92, 'poplar')]:
    tree_at(k, x, y, scenic, scale=rng.uniform(.9, 1.25), solid=False)
mark('A_MeleeGroup_1', (-42, 61, 0), 'AuthoredEncounter', 'A')
mark('A_MeleeGroup_2', (-42, 88, 0), 'AuthoredEncounter', 'A')
mark('A_Guilt_Roof', (-56, 80, 8), 'AuthoredGuiltReveal', 'A')

# ---------------------------------------------------------------------------------------------
# B / Lent: planter-divided quay lanes above the basin
# ---------------------------------------------------------------------------------------------
b = routes['B']
pathB = K.Path([(0, 24), (0, 36), (2.5, 48), (2, 58), (0, 66), (0, 76), (0, 86), (-2, 96), (-2.5, 106), (0, 116)])
o = K.ribbon(kit, 'Floor_B_Lanes', pathB, 13, M['Slab'], b)
kit.floors.append(o)
kit.floor_box('B_Quay', 0, 76, 22, 24, M['Cobble'], b, z=.003)
box('QuayInlay', (0, 76, .0115), (12, 12, .01), M['Slab'], b, tile=3.)       # tops sit 7 mm apart: floor .003, ring .010, inlay .017
box('QuayInlayRing', (0, 76, .005), (16, 16, .01), M['Ashlar'], b, tile=3.)
q0 = next(i for i, p in enumerate(pathB.pts) if p.y >= 64)
q1 = next(i for i, p in enumerate(pathB.pts) if p.y >= 88)
planter_ranges = [(next(i for i, p in enumerate(pathB.pts) if p.y >= 34), q0 - 2), (q1 + 2, next(i for i, p in enumerate(pathB.pts) if p.y >= 110))]
for i0, i1 in planter_ranges:
    rim = K.ribbon(kit, 'PlanterRim', pathB, 5, M['Ashlar'], b, z=.7, thick=.7, start=i0 * pathB.ds, end=i1 * pathB.ds)
    K.ribbon(kit, 'PlanterSoil', pathB, 4.2, M['Grass'], b, z=.74, thick=.1, start=i0 * pathB.ds, end=i1 * pathB.ds)
    for i in range(i0, i1 - 2, 3):
        p = pathB.pts[i + 1]
        t = pathB.tangent(i + 1)
        guide('Planter', (p.x, p.y, .35), (5.1, 3.1, .7), b.name, rot=yaw_along(t))
    for k, i in enumerate(range(i0 + 3, i1 - 2, 8)):
        p = pathB.pts[i]
        lamp_at(p.x, p.y, b, z=.7, light=(k % 2 == 0))
    for k, i in enumerate(range(i0 + 7, i1 - 4, 8)):
        p = pathB.pts[i]
        tree_at('poplar', p.x, p.y, b, z=.74, scale=rng.uniform(.9, 1.15), solid=False)
# lane edges (rails + hedges) except across the quay; the quay gets its own boundary.
for lat in (6.5, -6.5):
    rail_path(pathB, lat, b, 0, q0 + 1)
    rail_path(pathB, lat, b, q1 - 1, len(pathB.pts))
rail_pts([(-11, 64), (-11, 88)], b)
rail_pts([(11, 64), (11, 88)], b)
rail_pts([(6.5, 64), (11, 64)], b)
rail_pts([(6.5, 88), (11, 88)], b)
rail_pts([(-6.5, 64), (-11, 64)], b)
rail_pts([(-6.5, 88), (-11, 88)], b)
for s in (0, 1):
    K.ribbon(kit, 'LaneHedge', pathB, 1.6, M['Hedge'], b, z=1.05, thick=1.05, lateral=-8.0,
             start=0 if s == 0 else q1 * pathB.ds, end=q0 * pathB.ds if s == 0 else None)
for x, y in [(-8, 34), (10.2, 52), (-7.5, 66), (7.5, 86), (-8, 108)]:
    lamp_at(x, y, b, light=(y in (52, 86)))
# riverside ledge: east of the lane rail, a decorative promenade with lamps and trees before the basin wall.
ledge = K.Path([(0, 26), (2.5, 48), (2, 58), (0, 66), (0, 86), (-2, 96), (-2.5, 106), (0, 116)])
for y in range(30, 112, 10):
    p, t, n = pathB.at(y - 24)
    if 62 <= y <= 90:
        continue   # the quay is a combat space; charges and spawns need that floor open
    q = p - n * 7.6   # centre of the ledge strip, clear of the basin-edge rail at x 11.5
    lamp_at(q.x, q.y, b, light=(y in (50, 90)))
for y in range(34, 112, 14):
    p, t, n = pathB.at(y - 24)
    q = p - n * 9.6
    tree_at('poplar', q.x, q.y, b, scale=1.0, solid=False)
# basin quay edge rail along the ledge (keeps water edge readable).
rail_pts([(BX0 - .5, BY0 + 1), (BX0 - .5, BY1 - 1)], b, tall=1.2, name='QuayEdge')
# northern basin edge strip, a wooden jetty and a moored dinghy for life on the water.
box('Jetty', (22, 24.2, -.3), (6, 4, .25), M['Wood'], b)
for k in range(2):
    box('JettyPost', (19.4 + k * 5.2, 26, -1.2), (.3, .3, 2.4), M['Trunk'], b)
mark('B_Watcher_1', (8, 69, 2), 'AuthoredWatcher', 'B')
mark('B_Watcher_2', (-8, 83, 2), 'AuthoredWatcher', 'B')

# ---------------------------------------------------------------------------------------------
# C / Podzemni prehod: concrete galleries, sealed structural joints and dressing
# ---------------------------------------------------------------------------------------------
c = routes['C']
cc, ccon = M['Concrete'], M['Concrete']
kit.floor_box('C_Main', 52, 58, 6, 76, cc, c)
kit.floor_box('C_SideOpening', 60, 62, 16, 4, cc, c)
kit.floor_box('C_EscapeGallery', 68, 89, 6, 54, cc, c)
kit.floor_box('C_CombatPocket', 68, 90, 18, 20, cc, c)
for y in range(24, 96, 4):
    box('TunnelWest', (48.7, y + 2, 2.2), (.6, 4, 4.4), cc, c, solid=True)
    if not 60 <= y < 64:
        box('TunnelEast', (55.3, y + 2, 2.2), (.6, 4, 4.4), cc, c, solid=True)
    box('RoofBay', (52, y + 2, 4.6), (7, 4, .4), cc, roofs)
    if y % 8 == 0:   # every third tube is alive; the rest hang dark
        box('CeilingStrip', (52, y + 2, 4.34), (1.2, .3, .08), M['Fluoro'] if y % 24 == 0 else M['LampDead'], c)
    if y % 12 == 0:
        box('Pipe', (49.2, y + 2, 3.8), (.14, 4, .14), M['Iron'], c)
        box('Pipe', (49.2, y + 2, 3.45), (.1, 4, .1), M['Red'] if y % 24 == 0 else M['Iron'], c)
box('GuiltEndWall', (52, 96, 2.2), (7, .6, 4.4), cc, c, solid=True)
box('GuiltEndGlow', (52, 95.6, 1.7), (2.2, .1, 3.2), M['Ember'], c)
for y in (59.7, 64.3):
    box('EscapeConnectorWall', (60, y, 2.2), (10, .6, 4.4), cc, c, solid=True)
box('SideDoorLintel', (55.3, 62, 4.05), (.6, 4, .9), cc, c)
mark('C_SideDoor', (55.3, 62, 0), 'MandatorySideEscape', 'C', clear_width_m=4., clear_height_m=3.45)
mark('C_Guilt_Reveal', (52, 91, 0), 'AuthoredGuiltReveal', 'C')
mark('C_Retreat', (52, 67, 0), 'RetreatWaypoint', 'C')
for x in (64.7, 71.3):
    for y0, y1 in ((64 if x < 68 else 60, 80), (100, 116)):
        box('EscapeWall', (x, (y0 + y1) / 2, 2.2), (.6, y1 - y0, 4.4), cc, c, solid=True)
box('EscapeNorthCap', (68, 59.7, 2.2), (7, .6, 4.4), cc, c, solid=True)
for x in (58.7, 77.3):
    box('PocketWall', (x, 90, 2.2), (.6, 20, 4.4), cc, c, solid=True)
for y in (80, 100):
    for x in (62, 74):
        box('PocketReturn', (x, y, 2.2), (6, .6, 4.4), cc, c, solid=True)
box('EscapeRoof', (68, 89, 4.6), (7, 54, .4), cc, roofs)
box('PocketRoof', (68, 90, 4.6), (19, 20, .4), cc, roofs)
box('SideConnectorRoof', (60, 62, 4.6), (10, 5, .4), cc, roofs)
# non-removable caps over the solid voids so the structure reads as one block from outside
for name, (x0, y0, x1, y1) in dict(V1=(55.6, 64.3, 64.4, 80), V2=(55.6, 80, 58.4, 100), V3=(48.4, 96.6, 64.4, 116)).items():
    box('Cap_' + name, ((x0 + x1) / 2, (y0 + y1) / 2, 4.6), (x1 - x0, y1 - y0, .4), cc, c)
for y in (32, 56, 84):
    box('SecretStripe', (49.05, y, 1.4), (.08, 1, .3), M['SecretGlow'], c)
for y in range(26, 94, 8):
    box('HazardBase', (49.04, y + 2, .22), (.08, 3.8, .3), M['Hazard'], c)
box('RollerDoor', (49.0, 44, 1.5), (.1, 3.2, 2.8), M['Iron'], c)
box('RollerLight', (49.0, 44, 3.1), (.12, .5, .16), M['Red'], c)
for k, (x, y, s) in enumerate([(49.6, 30, 1.0), (49.8, 31.2, .8), (49.6, 76, 1.1)]):
    if y < 96 and 60 <= y < 64:
        continue
    box('Crate', (x + .2, y, s / 2 if False else .55 * s), (1.0 * s, 1.0 * s, 1.1 * s), M['Wood'], c, solid=False)
guide('CratesNorth', (49.7, 30.6, .6), (1.2, 2.2, 1.2), c.name)
for k, (x, y) in enumerate([(59.9, 91), (76.1, 89), (60.4, 95.5)]):
    box('PocketCrate', (x, y, .5), (1.4, 1.4, 1.0), M['Wood'], c, rot=k * .3, solid=True)
for y in (98, 110):
    box('SecretStripe', (71.0, y, 1.6), (.08, 1.4, .3), M['SecretGlow'], c)
# exterior: purple-lit entrance frame, deck dressing, vents
for x in (47.85, 56.15):   # beside the gallery walls (faces at x 48.4 / 55.6), not inside them
    box('GatePillar', (x, 23.8, 2.7), (1.1, 1.1, 5.4), M['Ashlar'], c, solid=True)
box('GateLintel', (52, 23.8, 5.4), (9.6, 1.3, .8), M['Ashlar'], c)
box('NeonStripe', (52, 23.2, 4.9), (6.4, .1, .2), M['SecretGlow'], c)
for k in range(3):
    box('Vent', (49.8 + k * 2.2, 44 + k * 14, 5.1), (1.4, 1.4, .8), M['Iron'], roofs)
box('RoofHazard', (52, 98, 4.9), (7, .3, .2), M['Hazard'], roofs)
# exterior face of the block, lit by a few sodium lamps.
lamp_at(46.2, 30, c, light=True)
lamp_at(46.2, 64, c)
lamp_at(46.2, 100, c)
# C approach promenade (north of the gate) is part of the shared fork; add a lit banner for the secret route.
text('C_Banner', 'PODZEMNI PREHOD', (52, 23.1, 4.2), .34, M['SecretGlow'], c)


# Service yard east of the underpass block: containers, crates and parked cars keep the east plinth from reading as empty.
M['ContainerRed'] = K.material('ContainerRed', T['CarRed'])
M['ContainerTeal'] = K.material('ContainerTeal', color=(.12, .32, .36))
for k, (y, mat) in enumerate([(30, 'ContainerRed'), (36, 'ContainerTeal'), (52, 'ContainerTeal'), (58, 'ContainerRed'), (101, 'ContainerRed')]):
    box('Container', (82.3, y, 1.3), (2.5, 5.6, 2.6), M[mat], c, solid=True)
    box('ContainerRidge', (82.3, y, 2.65), (2.3, 5.4, .12), M['Iron'], c)
    if k % 2 == 0:
        box('ContainerStack', (82.3, y, 3.9), (2.5, 5.6, 2.4), M['ContainerTeal' if mat == 'ContainerRed' else 'ContainerRed'], c)
for k, (x, y) in enumerate([(80.5, 80), (80.5, 84), (80.5, 120)]):
    instance(car_blue if k % 2 else car_red, 'ParkedCar', (x, y, 0), c, math.pi / 2)
for y in range(30, 120, 14):
    lamp_at(79.6, y + 3, c, light=(y % 42 == 2))
for y in range(40, 130, 12):
    tree_at('poplar', 84.5, y, scenic, scale=1.1, solid=False)
for x, y in [(46.6, 120), (47, 20)]:
    tree_at('round', x, y, scenic, scale=1.0, solid=False)

# ---------------------------------------------------------------------------------------------
# Gates, route signs, charges, FERI sign
# ---------------------------------------------------------------------------------------------
def gate(route, x, y, w, rear=False):
    cl = routes[route]
    name = ('RearSeal_' if rear else 'EntryGate_') + route
    mat = M['Secret'] if route == 'C' else M['Iron']
    box(name, (x, y, 2), (w, .35, 4), mat, cl)
    guide(name, (x, y, 2), (w, .35, 4), cl.name, dynamic=True)
    mark(name, (x, y, 0), 'RearSeal' if rear else 'EntryGate', route, default_closed=True)
    if not rear:
        mark('Select_' + route, (x, y + 2, 0), 'SelectionTrigger', route)
        for s in ((-1, 1) if route != 'C' else ()):   # C's gallery walls and pillars already frame its gate
            box('GatePost', (x + s * (w / 2 + .5), y, 2.4), (1, 1, 4.8), M['Ashlar'], cl, solid=True)
        box('SignBoard', (x, y - .45, 5.2), (w + 1, .3, 1.5), M['Iron'], cl)
        label = {'A': 'A / TABOR', 'B': 'B / LENT', 'C': 'C / PODZEMNI PREHOD'}[route]
        text('RouteSign', label, (x, y - .62, 4.95), .5 if route != 'C' else .3, M['SecretGlow'] if route == 'C' else M['Sign'], cl)


for k, x, w in [('A', -42, 8), ('B', 0, 13), ('C', 52, 6)]:
    gate(k, x, 25, w)
for k, x, w in [('A', -42, 8), ('B', 0, 13), ('C', 68, 6)]:
    gate(k, x, 113.5 if k != 'C' else 113, w, True)

pockets = {'A': dict(center=[-42, 78], size=[24, 28]), 'B': dict(center=[0, 76], size=[22, 24]), 'C': dict(center=[68, 90], size=[18, 20])}
charges = {'A': [(-48, 76), (-36, 87), (-42, 69)], 'B': None, 'C': None}
for k, p in pockets.items():
    x, y = p['center']
    w, d = p['size']
    cl = routes[k]
    mark('CombatSpace_' + k, (x, y, 0), 'CombatSpace', k, width_m=w, depth_m=d, exit_has_kill_quota=False)
    spots = charges[k] or [(x + dx, y + dy) for dx, dy in [(-w * .32, -d * .28), (w * .32, -d * .24), (0, d * .30)]]
    for i, (px, py) in enumerate(spots, 1):
        mark('Charge_%s_%d' % (k, i), (px, py, 0), 'StationaryCharge', k)
        box('ChargeBase', (px, py, .12), (.7, .7, .24), M['Iron'], cl)
        box('ChargeCell', (px, py, .7), (.28, .28, .9), M['Charge'], cl)
    for i, (dx, dy) in enumerate([(-.4, -.4), (.4, -.4), (-.4, 0), (.4, 0), (-.4, .4), (.4, .4)], 1):
        mark('Spawn_%s_%d' % (k, i), (x + dx * w, y + dy * d, 0), 'OffscreenSpawnCandidate', k)
    for i, dy in enumerate((-.35, 0, .35), 1):
        mark('Guilt_%s_%d' % (k, i), (x, y + dy * d, 0), 'OffscreenGuiltCandidate', k)

# FERI signpost on the pier end.
arrow = [(-3.2, -.5), (2.2, -.5), (3.6, 0), (2.2, .5), (-3.2, .5)]
verts = [(px, -.12, 3.7 + py * 1.7) for px, py in arrow] + [(px, .12, 3.7 + py * 1.7) for px, py in arrow]
faces = [(0, 1, 2, 3, 4), (9, 8, 7, 6, 5)] + [(i, (i + 1) % 5, 5 + (i + 1) % 5, 5 + i) for i in range(5)]
sign = mesh('FERI_Arrow', verts, faces, M['Sign'], sh)
sign.location = (0, 146, 0)
text('FERI_Sign', 'FERI', (-.4, 145.86, 3.35), 1.05, M['Iron'], sh)
for s in (-2.2, 2.2):
    box('SignPost', (s, 146, 1.9), (.22, .22, 3.8), M['Iron'], sh, solid=True)
for s in (-6.5, 6.5):
    lamp_at(s, 142, sh, light=True)
    lamp_at(s, 130, sh)
rail_pts([(-8, 126.5), (-8, 150), (-3.2, 150)], sh)
rail_pts([(3.2, 150), (8, 150), (8, 126.5)], sh)
box('FERI_Step', (0, 150.3, -.3), (6.4, 1, .7), M['Ashlar'], sh)
mark('FERI_Pier_End', (0, 149, 0), 'Cosmetic', 'Shared')

# Merge row: enclosure along its long edges.
rail_pts([(-56, 126.8), (-8, 126.8)], sh)
rail_pts([(8, 126.8), (44, 126.8)], sh)
rail_pts([(44, 126.8), (72, 126.8)], sh)
for a_, b_ in [(-56, -46), (-38, -6.5), (6.5, 12), (12, 44)]:
    rail_pts([(a_, 115.4), (b_, 115.4)], sh)
rail_pts([(44, 115.4), (65, 115.4)], sh)
rail_pts([(71, 115.4), (72, 115.4)], sh)
rail_pts([(-56, 115.4), (-56, 126.8)], sh)
rail_pts([(72, 115.4), (72, 126.8)], sh)
for x in range(-47, 72, 11):   # offset so no lamp stands on a route end at x -42 / 0 / 68
    if not (-10 < x < 12):
        lamp_at(x, 118, sh, light=(x % 22 == 4))
# causeway arches over the basin outlet
for k in range(3):
    xa = 12 + k * (32 / 3)
    cx = xa + 32 / 6
    K.arch_span(kit, 'CausewayArch', cx, 115.0, 127.0, 4.4, -1.4, -.7, M['QuayStone'], shared)
for x in (12, 12 + 32 / 3, 12 + 64 / 3, 44):
    box('CausewayPier', (x, 121, -5), (1.1, 12, 8), M['QuayStone'], shared)
box('CausewayFascia', (28, 114.9, -.45), (32, .3, .5), M['Ashlar'], shared)
box('CausewayFascia', (28, 127.1, -.45), (32, .3, .5), M['Ashlar'], shared)
# hedges + trees south of the merge promenade
box('MergeHedgeW', (-32, 128.3, .6), (48, 1.4, 1.2), M['Hedge'], scenic)
box('MergeHedgeE', (58, 128.3, .6), (28, 1.4, 1.2), M['Hedge'], scenic)
for x in range(-66, -10, 9):
    tree_at('round' if x % 2 else 'poplar', x, 138 + rng.uniform(-4, 6), scenic, scale=rng.uniform(.9, 1.3), solid=False)
for x in range(60, 90, 9):
    tree_at('poplar', x, 136 + rng.uniform(-4, 8), scenic, scale=rng.uniform(.9, 1.2), solid=False)
for y in range(40, 140, 18):
    tree_at('round', -66, y + rng.uniform(-3, 3), scenic, scale=1.2, solid=False)
    tree_at('poplar', 86, y + rng.uniform(-3, 3), scenic, scale=1.1, solid=False)

# ---------------------------------------------------------------------------------------------
# Backdrop: south-bank city, viaduct, hills with Pohorje spruces
# ---------------------------------------------------------------------------------------------
def terrain():
    n = 48
    step = 13.
    x_lo, y_lo = -300., -300.

    def height(x, y):
        # distance outside of the river rectangle
        dx = max(-108 - x, 0, x - 124)
        dy = max(-96 - y, 0, y - 172)
        d = math.hypot(dx, dy)
        if d == 0:
            return -12.
        if d < 8:
            return -12 + d * 1.5
        if d < 40:
            return 0. + math.sin(x * .1 + y * .07) * .3
        h = (d - 40) ** 1.25 * .35
        return min(h, 70) * (.7 + .5 * (math.sin(x * .031 + 1) * math.cos(y * .027 + 2) * .5 + .5)) + math.sin(x * .13) * 1.6

    verts = []
    for j in range(n + 1):
        for i in range(n + 1):
            x = x_lo + i * step
            y = y_lo + j * step
            verts.append((x, y, height(x, y)))
    faces = [(j * (n + 1) + i, j * (n + 1) + i + 1, (j + 1) * (n + 1) + i + 1, (j + 1) * (n + 1) + i) for j in range(n) for i in range(n)]
    o = mesh('Hills', verts, faces, [M['Rock'], M['Grass']], scenic, tile=9.)
    for f in o.data.polygons:
        z = sum(o.data.vertices[v].co.z for v in f.vertices) / 4
        f.material_index = 1 if z < 14 else 0
    return o, height


hills, terrain_height = terrain()
# south-bank city: two staggered rows of facade bays plus landmark spires, scaled up for depth.
bank_rows = []
for row, (y, sc, seed) in enumerate([(184, 1.3, 5), (204, 1.7, 6)]):
    r = random.Random(seed)
    x = -104
    while x < 122:
        s = sc * r.uniform(.9, 1.35)
        instance(r.choice(facades), 'BankHouse', (x, y, terrain_height(x, y)), scenic, math.pi if False else yaw_facing(Vector((0, -1, 0))), scale=s)
        x += 4 * s
for x in (-30, 60, 112):
    h = 24 + rng.uniform(0, 8)
    box('ChurchTower', (x, 196, h / 2), (6, 6, h), M['Stucco'][2], scenic)
    kit.cone('Spire', (x, 196, h), 4.8, 14, M['Terracotta'], scenic, verts=4)
    for z in (h - 5,):
        box('BellLight', (x, 192.9, z), (1.6, .1, 2.4), M['WindowLit'], scenic)
# north/east/west bank villages (low, distant)
for (x0, x1, y, fy) in [(-104, 120, -110, 1)]:
    r = random.Random(31)
    xx = x0
    while xx < x1:
        s = 1.7 * r.uniform(.9, 1.3)
        instance(r.choice(facades), 'BankHouse', (xx, y, terrain_height(xx, y)), scenic, yaw_facing(Vector((0, 1, 0))), scale=s)
        xx += 4 * s
for y in range(-60, 150, 14):
    s = 1.3 * rng.uniform(.9, 1.25)
    instance(rng.choice(facades), 'BankHouse', (-120, y, terrain_height(-120, y)), scenic, yaw_facing(Vector((1, 0, 0))), scale=s)
    instance(rng.choice(facades), 'BankHouse', (136, y, terrain_height(136, y)), scenic, yaw_facing(Vector((-1, 0, 0))), scale=s)
# viaduct in the north-east water (stone arches + deck + lamps), the bridge silhouette of the mockup
for k in range(10):
    xa = -10 + k * 14.5
    K.arch_span(kit, 'ViaductArch', xa + 7.25, -82, -78, 5.6, 6.2, 7.2, M['Ashlar'], scenic)
for k in range(11):
    box('ViaductPier', (-10 + k * 14.5, -80, 1.6), (1.6, 4, 11.2), M['Ashlar'], scenic)
box('ViaductDeck', (62, -80, 7.4), (150, 5.2, .6), M['Slab'], scenic)
box('ViaductParapetN', (62, -82.5, 8.2), (150, .4, 1.0), M['Ashlar'], scenic)
box('ViaductParapetS', (62, -77.5, 8.2), (150, .4, 1.0), M['Ashlar'], scenic)
for k in range(0, 150, 10):
    instance(lamp_proto if k % 50 == 20 else lamp_dead_proto, 'ViaductLamp', (-12 + k, -77.2, 7.7), scenic)
M['Reflect'] = K.material('ReflectGlow', color=(.9, .18, .08), emit=1.5, emit_color=(.9, .16, .07))
for k in range(0, 150, 10):
    for j, (dy, ln) in enumerate([(2, 2.2), (5, 1.6), (8.5, 1.1), (12, .7)]):
        box('Reflection', (-12 + k + rng.uniform(-.3, .3), -75 + dy, WZ + .03), (.35, ln, .02), M['Reflect'], scenic)
for y in range(30, 112, 10):
    for j, (dx, ln) in enumerate([(3.5, 2.2), (7.5, 1.6), (12, 1.1)]):
        box('Reflection', (BX0 + dx, y + rng.uniform(-.5, .5), WZ + .03), (ln, .3, .02), M['Reflect'], scenic)
for x in range(-100, 120, 11):
    for j, (dy, ln) in enumerate([(-12, 2.6), (-18, 1.8), (-24, 1.2)]):
        box('Reflection', (x + rng.uniform(-1, 1), 172 + dy + 30 - 26, WZ + .03), (.4, ln, .02), M['Reflect'], scenic)
# hills: Pohorje spruces on slopes
if spruces:
    pr = random.Random(11)
    for _ in range(90):
        x, y = pr.uniform(-290, 290), pr.uniform(-290, 290)
        h = terrain_height(x, y)
        if h < 6 or (-108 < x < 124 and -96 < y < 172):
            continue
        proto = pr.choice(spruces)
        o = instance(proto, 'HillSpruce', (x, y, h - .3), scenic, pr.random() * 6.28, pr.uniform(1.4, 2.6))

# ---------------------------------------------------------------------------------------------
# Horror dressing: leaf drifts, trash, dried stains and drag marks (all flat or tiny; no collision)
# ---------------------------------------------------------------------------------------------
def stain(x, y, w, d, c, rot=0.):
    box('Stain', (x, y, .012), (w, d, .006), M['Blood'], c, rot=rot, tile=1.)


def drag(p0, p1, c, n=6, w0=.6, w1=.25):
    """A dried smear leading away from a pool: boxes shrinking along p0 -> p1."""
    d = Vector(p1) - Vector(p0)
    ang = yaw_along(d.normalized())
    for k in range(n):
        q = Vector(p0) + d * (k / (n - 1))
        stain(q.x + hrng.uniform(-.15, .15), q.y, w0 + (w1 - w0) * k / (n - 1), d.length / n * .85, c, rot=ang)


def litter(path, c, seed, step=4, spread=5.4):
    r = random.Random(seed)
    for s_ in range(6, int(path.length) - 6, step):
        p, t, n = path.at(s_)
        q = p + n * r.uniform(-spread, spread)
        roll = r.random()
        if roll < .55 and clear(q.x, q.y, 1.0):
            box('LeafPile', (q.x, q.y, .025), (r.uniform(.8, 2.2), r.uniform(.8, 2.2), .05), M['Leafpile'], c, rot=r.random() * 3, tile=1.)
        elif roll < .8 and clear(q.x, q.y, .7):
            for k in range(r.choice((2, 3))):
                bx, by = q.x + r.uniform(-.4, .4), q.y + r.uniform(-.4, .4)
                box('TrashBag', (bx, by, .22 + k * .02), (r.uniform(.4, .6), r.uniform(.4, .6), .44 - k * .06), M['Bag'], c, rot=r.random() * 3, tile=1.)


litter(pathA, a, 91)
litter(pathB, b, 92, spread=4.6)
for x in range(-46, 60, 9):   # fork promenade
    if clear(x, 19, .8):
        box('LeafPile', (x + hrng.uniform(-1, 1), hrng.uniform(15, 22), .025), (hrng.uniform(1, 2.4), hrng.uniform(1, 2), .05), M['Leafpile'], sh, rot=hrng.random() * 3, tile=1.)
for x, y in [(-9, 4), (7, -3), (-3, 9)]:
    box('LeafPile', (x, y, .035), (2.2, 1.8, .05), M['Leafpile'], sh, rot=x, tile=1.)
stain(-46.5, 72, 1.6, 2.2, a, .4)
drag((-40, 66), (-44, 90), a, 7)
stain(2.5, 70, 1.8, 1.4, b, .3)
drag((5.5, 74), (1.5, 88), b, 6)
stain(52, 48, 1.2, 2.4, c)
drag((52, 66), (52, 90), c, 9, .8, .3)
stain(52, 92.6, 2.8, 1.6, c)
stain(3.2, -4.4, 1.4, 1.1, sh, .6)
for cx, cy, ch, cs in [(-46.5, 72, 4, 3), (-41, 80, 3, 2), (2.5, 70, 3, 3), (4, 80, 2, 2), (52, 49, 3, 2), (3.2, -4.4, 3, 2)]:
    cr_ = random.Random(cx * 7 + cy)
    cr_pos = [(cx + cr_.uniform(-ch, ch), cy + cr_.uniform(-ch, ch)) for _ in range(cs)]
    for qx, qy in cr_pos:
        if clear(qx, qy, .5):
            instance(crow_proto, 'Crow', (qx, qy, .01), c if cx > 40 else (a if cx < -20 else (b if cy > 20 else sh)), cr_.random() * 6.28)
drag((-3, 6), (0, 15), sh, 4, .4, .2)

# ---------------------------------------------------------------------------------------------
# Overlap audit (printed; written to overlap-audit.json by the full build)
# ---------------------------------------------------------------------------------------------
zfight = K.coplanar_overlaps(kit.boxlog)
print('MB_ZFIGHT', len(zfight))
seen = set()
for hit in zfight:
    key = (hit[0], hit[1], hit[3])
    if key not in seen:
        seen.add(key)
        print('  zfight', hit)
print('MB_SKIPPED_PLACEMENTS', len(skipped), skipped[:40])

# ---------------------------------------------------------------------------------------------
# Manifests
# ---------------------------------------------------------------------------------------------
def poly(path, step=3):
    return [[round(p.x, 2), round(p.y, 2)] for p in path.pts[::step]] + [[round(path.pts[-1].x, 2), round(path.pts[-1].y, 2)]]


paths = {
    'Arrival': [[0, 0], [0, 18]],
    'ForkA': [[0, 18], [-42, 18], [-42, 28]],
    'ForkB': [[0, 18], [0, 28]],
    'ForkC': [[0, 18], [52, 18], [52, 28]],
    'A': poly(pathA)[3:],
    'B': [[round(p.x - 0, 2), round(p.y, 2)] for p in pathB.pts[::3] if p.y >= 28],
    'B_Alternate': [[round(p.x + 4, 2), round(p.y, 2)] for p in pathB.pts[::3] if 36 <= p.y <= 62] if False else [],
    'C_Reveal': [[52, 28], [52, 91]],
    'C_Escape': [[52, 91], [52, 62], [68, 62], [68, 90], [68, 108]],
    'MergeA': [[-42, 118], [-42, 121], [0, 121]],
    'MergeB': [[0, 116], [0, 121]],
    'MergeC': [[68, 108], [68, 121], [0, 121]],
    'FERI': [[0, 121], [0, 144]]}
# B lanes either side of the planter (lateral +-4)
lane_l = [[round((p + pathB.normal(i) * 4).x, 2), round((p + pathB.normal(i) * 4).y, 2)] for i, p in enumerate(pathB.pts) if i % 3 == 0 and 28 <= p.y <= 63]
lane_r = [[round((p - pathB.normal(i) * 4).x, 2), round((p - pathB.normal(i) * 4).y, 2)] for i, p in enumerate(pathB.pts) if i % 3 == 0 and 28 <= p.y <= 63]
lane2_l = [[round((p + pathB.normal(i) * 4).x, 2), round((p + pathB.normal(i) * 4).y, 2)] for i, p in enumerate(pathB.pts) if i % 3 == 0 and 89 <= p.y <= 114]
lane2_r = [[round((p - pathB.normal(i) * 4).x, 2), round((p - pathB.normal(i) * 4).y, 2)] for i, p in enumerate(pathB.pts) if i % 3 == 0 and 89 <= p.y <= 114]
paths['B'] = [[0, 28]] + lane_l + [[0, 66], [0, 86]] + lane2_l + [[0, 116]]
paths['B_Alternate'] = [[0, 28]] + lane_r + [[0, 66], [0, 86]] + lane2_r + [[0, 116]]
layout = dict(scene='Maribor', revision='lit_diorama_pass', coordinates='Blender XYZ meters, Z up; FBX Y up / -Z forward', paths=paths, pockets=pockets,
              route_names={'A': 'Tabor', 'B': 'Lent', 'C': 'Podzemni prehod'},
              references=['../../world-modeling-v1/figma/04-maribor-layout.svg', '../../world-modeling-v1/figma/05-maribor-modules.svg',
                          '../../world-modeling-v1/maps/pohorje/Source/Pohorje_Large.blend'],
              status='Environment proposal. No gameplay scripts or Unity scene wiring.', authored_side_escape=True,
              ribbons=[dict(name='A_Street', width_m=8, points=poly(pathA, 2)), dict(name='B_Lanes', width_m=11, points=poly(pathB, 2))],
              landmarks=dict(hall=[[-26, -46], [26, -32]], pavilion=[[-8, -42], [8, -26]], fountain=[0, -7, 3.4], terrace=[[-35, -49], [35, -15]]),
              island=dict(x=[X0, X1], y=[Y0, Y1], water_level_m=WZ, basin=dict(x=[BX0, BX1], y=[BY0, BY1])))




