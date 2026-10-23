"""Shared helpers for the Maribor build: pixel textures, materials, mesh primitives, path ribbons.

Imported by build_maribor.py inside Blender. Textures follow the Pohorje delivery: small opaque
128-256 px sheets made of 2-4 px clumps and no external artwork.
"""
import bpy
import math
import random
from pathlib import Path
from mathutils import Vector

OUT = Path(__file__).resolve().parent


# ---------------------------------------------------------------------------------------------
# Pixel textures (sRGB values, opaque)
# ---------------------------------------------------------------------------------------------
def rgb(h):
    return tuple(int(h[i:i + 2], 16) / 255 for i in (1, 3, 5))


def pick(palette, v):
    return palette[min(len(palette) - 1, int(v * len(palette)))]


def shade(c, k):
    return tuple(max(0, min(1, x * k)) for x in c)


def make_texture(name, size, painter, seed=1):
    rng = random.Random(seed)
    grid = [[rng.random() for _ in range(64)] for _ in range(64)]
    hashes = {}

    def cell(x, y, b):
        return grid[(y // b) % 64][(x // b) % 64]

    def h2(a, b):
        key = (a, b)
        if key not in hashes:
            hashes[key] = random.Random(a * 7919 + b * 104729 + seed).random()
        return hashes[key]

    pixels = []
    for y in range(size):
        for x in range(size):
            c = painter(x, y, rng, cell, h2)
            j = rng.uniform(.93, 1.07)
            pixels.extend([max(0, min(1, v * j)) for v in c] + [1])
    im = bpy.data.images.new('MB_' + name, size, size)
    im.pixels.foreach_set(pixels)
    im.filepath_raw = str(OUT / 'Textures' / (name + '.png'))
    im.file_format = 'PNG'
    im.save()
    im.pack()
    return im


def build_textures():
    T = {}
    P = {k: [rgb(h) for h in v] for k, v in dict(
        cobble=['#4c4a48', '#585450', '#645e55', '#3f3e3d', '#6b6252'],
        paveblue=['#3f444c', '#484d55', '#393d44', '#50545b'],
        slab=['#625f58', '#6d6a60', '#54524b', '#797366'],
        asphalt=['#1c2229', '#222931', '#171c22', '#262d35'],
        roof=['#6e2d22', '#7d3526', '#5e271d', '#8b4130', '#4f2018'],
        quay=['#4b4e4d', '#585b58', '#3f4242', '#62645e', '#34463c'],
        concrete=['#464b4d', '#51575a', '#3d4244', '#5b6163'],
        rock=['#1f2228', '#2a2d34', '#33373f', '#1a1d22', '#3e4249'],
        grass=['#3a4630', '#444f33', '#2f3a29', '#555a38', '#48503a'],
        wood=['#3f2c20', '#4b3526', '#34241a', '#58402d'],
        stucco0=['#8a5240', '#7c483a', '#935d46', '#6f4034'],
        stucco1=['#9b8d76', '#8d806b', '#a39680', '#83775f'],
        stucco2=['#85765c', '#786a52', '#8d7e63', '#6d6049'],
        stucco3=['#72806f', '#667362', '#7d8b79', '#5d6a5a'],
        water=['#0d1822', '#101f2b', '#0a131b', '#14263a'],
        carred=['#5e1c18', '#6b231e', '#521815'],
        carblue=['#46535f', '#505d69', '#3d4953'],
        bark=['#2c231c', '#352a22', '#241c16'],
        leaf=['#2b3d27', '#324830', '#243420', '#3d4f2c'],
        ashlar=['#625f55', '#6f6b5f', '#55524a', '#797466']).items()}

    def grime(c, x, y, h2, size=64, streak=.7, damp=True):
        """Rain streaks running down from the top of each tile and rising damp at the bottom."""
        v = h2(x // 5, 1234)
        if v > streak:
            k = .62 + .38 * ((y % size) / size) ** .6
            c = shade(c, k)
        if damp and y % size > size * .86:
            c = shade(c, .72)
        return c

    def cobble(x, y, rng, cell, h2):
        row = y // 10
        off = 6 if row % 2 else 0
        col = (x + off) // 12
        if y % 10 == 0 or (x + off) % 12 == 0:
            return shade(P['cobble'][3], .72)
        c = pick(P['cobble'], h2(row, col))
        return (.14, .06, .05) if cell(x + 5, y + 21, 12) > .955 else c

    def slab(x, y, rng, cell, h2):
        if x % 32 == 0 or y % 32 == 0:
            return shade(P['slab'][2], .7)
        return pick(P['slab'], cell(x, y, 4) * .6 + h2(x // 32, y // 32) * .4)

    def asphalt(x, y, rng, cell, h2):
        c = pick(P['asphalt'], cell(x, y, 2))
        if ((x * 3 + y * 5) % 53 < 2 or (x * 5 - y * 2) % 71 < 2) and cell(x, y, 16) > .62:
            return (.05, .06, .07)             # cracks
        if cell(x + 11, y + 40, 10) > .94:
            return (.16, .06, .05)             # old stain
        return shade(c, 1.25) if rng.random() < .008 else c

    def roof(x, y, rng, cell, h2):
        row = y // 8
        col = (x + (4 if row % 2 else 0)) // 8
        if y % 8 in (0, 1) or (x + (4 if row % 2 else 0)) % 8 == 0:
            return shade(P['roof'][4], .6)
        c = pick(P['roof'], h2(row, col))
        if cell(x, y, 16) > .86:
            return shade((.17, .24, .15), .8 + h2(row, col) * .5)
        return shade(c, .75) if h2(row + 9, col + 3) > .85 else c

    def stucco(i):
        def f(x, y, rng, cell, h2):
            c = pick(P['stucco%d' % i], cell(x, y, 8) * .5 + cell(x, y, 2) * .5)
            c = grime(c, x, y, h2, 64, .66)
            if cell(x + 17, y + 5, 16) > .9:       # peeled plaster showing brick
                c = (.30, .16, .13) if cell(x, y, 2) > .35 else (.22, .12, .1)
            elif cell(x + 3, y + 29, 12) > .93:    # mould bloom
                c = shade((.2, .27, .18), .8 + cell(x, y, 2) * .4)
            return c
        return f

    def quay(x, y, rng, cell, h2):
        row = y // 16
        col = (x + (16 if row % 2 else 0)) // 32
        if y % 16 == 0 or (x + (16 if row % 2 else 0)) % 32 == 0:
            return shade(P['quay'][2], .6)
        c = pick(P['quay'], h2(row, col) * .7 + cell(x, y, 4) * .3)
        return P['quay'][4] if cell(x + 40, y + 9, 6) > .93 else c

    def ashlar(x, y, rng, cell, h2):
        row = y // 12
        col = (x + (12 if row % 2 else 0)) // 24
        if y % 12 == 0 or (x + (12 if row % 2 else 0)) % 24 == 0:
            return shade(P['ashlar'][2], .65)
        return pick(P['ashlar'], h2(row, col))

    def concrete(x, y, rng, cell, h2):
        if x % 64 in (0, 1) or y % 64 == 0:
            return shade(P['concrete'][2], .62)
        c = pick(P['concrete'], cell(x, y, 4))
        return shade(c, .8) if y % 64 > 50 else c

    def rock(x, y, rng, cell, h2):
        return pick(P['rock'], cell(x, y, 4) * .65 + cell(x, y, 12) * .35)

    def grass(x, y, rng, cell, h2):
        c = pick(P['grass'], cell(x, y, 4))
        if cell(x + 7, y + 3, 8) > .82:
            return (.2, .15, .11)              # bare soil
        return (.45, .4, .22) if rng.random() < .02 else c

    def wood(x, y, rng, cell, h2):
        if x % 16 == 0:
            return shade(P['wood'][2], .6)
        return pick(P['wood'], h2(x // 16, y // 48) * .6 + cell(x, y, 2) * .4)

    def water(x, y, rng, cell, h2):
        c = pick(P['water'], cell(x, y, 4) * .5 + .25 * math.sin(x * .35 + y * .15) + .25)
        streak = cell(x // 6 * 6, y // 14 * 14, 6)
        if streak > .93 and (y % 14) < 3:
            return (.62, .14, .08)
        if streak > .86 and (y % 14) < 2:
            return (.3, .09, .09)
        return c

    def pave(x, y, rng, cell, h2):
        row = y // 12
        col = (x + (8 if row % 2 else 0)) // 16
        if y % 12 == 0 or (x + (8 if row % 2 else 0)) % 16 == 0:
            return (.16, .15, .15)
        return shade(pick(P['paveblue'], h2(row, col)), 1.0)

    def solid(key):
        def f(x, y, rng, cell, h2):
            return pick(P[key], cell(x, y, 4))
        return f

    def lattice(x, y, rng, cell, h2):
        d = (x + y) % 16 < 2 or (x - y) % 16 < 2
        return (.62, .34, .96) if d else (.10, .05, .16)

    def lit(x, y, rng, cell, h2):
        return (.92, .5, .2) if (x // 8 + y // 8) % 2 else (.7, .32, .12)

    def poster(x, y, rng, cell, h2):
        # Faded rave flyer: paper, a purple sun, a headline bar, a torn corner.
        if (x - 32) ** 2 + (y - 38) ** 2 < 140:
            return (.5, .18, .5)
        if 10 < y < 18 and 6 < x < 58:
            return (.82, .78, .6)
        if x + y > 112 or (y < 4 and x > 50):
            return (.35, .3, .26)
        return (.66, .6, .46) if cell(x, y, 4) > .3 else (.58, .52, .4)

    def rail(x, y, rng, cell, h2):
        return (.5, .22, .17) if x % 32 < 6 else (.18, .2, .24)

    spec = [('Cobble', 128, cobble), ('Slab', 128, slab), ('Asphalt', 128, asphalt), ('Terracotta', 128, roof),
            ('Stucco_0', 256, stucco(0)), ('Stucco_1', 256, stucco(1)), ('Stucco_2', 256, stucco(2)),
            ('Stucco_3', 256, stucco(3)), ('QuayStone', 128, quay), ('Ashlar', 128, ashlar),
            ('Concrete', 128, concrete), ('Rock', 128, rock), ('Grass', 128, grass), ('Wood', 128, wood),
            ('Drava', 128, water), ('Pave', 128, pave), ('CarRed', 64, solid('carred')), ('CarBlue', 64, solid('carblue')),
            ('Bark', 64, solid('bark')), ('Leaf', 64, solid('leaf')), ('SecretLattice', 64, lattice),
            ('WarmWindow', 32, lit), ('Hazard', 64, rail), ('Poster', 64, poster)]
    for i, (name, size, painter) in enumerate(spec):
        T[name] = make_texture(name, size, painter, seed=759 + i)
    return T


# ---------------------------------------------------------------------------------------------
# Materials
# ---------------------------------------------------------------------------------------------
def material(name, image=None, color=(.5, .5, .5), emit=0., emit_from_image=False, emit_color=None, rough=.92):
    m = bpy.data.materials.new('MB_' + name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    p = m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Roughness'].default_value = rough
    p.inputs['Base Color'].default_value = (*color, 1)
    tex = None
    if image is not None:
        tex = m.node_tree.nodes.new('ShaderNodeTexImage')
        tex.image = image
        tex.interpolation = 'Closest'
        m.node_tree.links.new(tex.outputs['Color'], p.inputs['Base Color'])
    if emit:
        p.inputs['Emission Strength'].default_value = emit
        if emit_from_image and tex is not None:
            m.node_tree.links.new(tex.outputs['Color'], p.inputs['Emission Color'])
        else:
            p.inputs['Emission Color'].default_value = (*(emit_color or color), 1)
    return m


# ---------------------------------------------------------------------------------------------
# Mesh primitives (all UVs are box-projected, one texture tile per `tile` meters)
# ---------------------------------------------------------------------------------------------
class Kit:
    def __init__(self, scene, guides_collection, marker_collection):
        self.scene = scene
        self.collision = []
        self.markers = []
        self.floors = []
        self.boxlog = []
        self.group = 0
        self.library_c = None
        self.guides_c = guides_collection
        self.markers_c = marker_collection
        self.iron = None

    def link(self, o, c):
        c.objects.link(o)
        return o

    def mesh(self, name, verts, faces, mat, c, tile=4.):
        data = bpy.data.meshes.new(name)
        data.from_pydata(verts, [], faces)
        data.update()
        o = bpy.data.objects.new(name, data)
        c.objects.link(o)
        mats = mat if isinstance(mat, (list, tuple)) else [mat]
        for m in mats:
            data.materials.append(m)
        uv = data.uv_layers.new()
        for face in data.polygons:
            axis = max(range(3), key=lambda i: abs(face.normal[i]))
            axes = [i for i in range(3) if i != axis]
            for li in face.loop_indices:
                p = data.vertices[data.loops[li].vertex_index].co
                uv.data[li].uv = (p[axes[0]] / tile, p[axes[1]] / tile)
        return o

    def box(self, name, pos, size, mat, c, rot=0., tile=4., solid=False):
        x, y, z = pos
        if not name.startswith('COL_'):
            # Library modules are modelled at the origin and joined one by one; each join closes a group.
            owner = 'module_%d' % self.group if c is self.library_c else c.name
            self.boxlog.append((name, owner, tuple(pos), tuple(size), rot, getattr(mat, 'name', 'multi')))
        a, b, d = [v / 2 for v in size]
        cs, sn = math.cos(rot), math.sin(rot)
        verts = []
        for i, j, k in [(-1, -1, -1), (-1, -1, 1), (-1, 1, -1), (-1, 1, 1), (1, -1, -1), (1, -1, 1), (1, 1, -1), (1, 1, 1)]:
            lx, ly = i * a, j * b
            verts.append((x + lx * cs - ly * sn, y + lx * sn + ly * cs, z + k * d))
        o = self.mesh(name, verts, [(0, 4, 6, 2), (1, 3, 7, 5), (0, 1, 5, 4), (2, 6, 7, 3), (0, 2, 3, 1), (4, 5, 7, 6)], mat, c, tile)
        if solid:
            self.guide(name, pos, size, c.name, rot=rot)
        return o

    def guide(self, name, pos, size, owner, rot=0., dynamic=False):
        o = self.box('COL_' + name, pos, size, self.iron, self.guides_c, rot=rot)
        o.display_type = 'WIRE'
        o.hide_render = True
        o['suggested_collider'] = 'BoxCollider'
        o['owner_collection'] = owner
        o['dynamic_gate'] = dynamic
        self.collision.append(dict(name=o.name, center=[round(v, 3) for v in pos], size=[round(v, 3) for v in size],
                                   rotation_z=round(rot, 4), owner=owner, dynamic=dynamic))
        return o

    def floor_box(self, name, x, y, w, d, mat, c, z=0., thick=.36, rot=0.):
        o = self.box('Floor_' + name, (x, y, z - thick / 2), (w, d, thick), mat, c, rot=rot)
        o['collision'] = 'BoxCollider; finished surface Z=%.3f' % z
        self.floors.append(o)
        self.guide('Floor_' + name, (x, y, z - thick / 2), (w, d, thick), c.name, rot=rot)
        return o

    def mark(self, name, pos, kind, route='Shared', **extra):
        o = bpy.data.objects.new(name, None)
        self.markers_c.objects.link(o)
        o.location = pos
        o.empty_display_type = 'ARROWS'
        o.empty_display_size = .7
        o['kind'] = kind
        o['route'] = route
        for k, v in extra.items():
            o[k] = v
        self.markers.append(dict(name=name, position=[round(v, 3) for v in pos], kind=kind, route=route, **extra))
        return o

    def gable(self, name, pos, size, overhang, rise, mat, c, rot=0.):
        """Gable roof, ridge along local X, with overhang. pos is the eave-level center."""
        x, y, z = pos
        w, d, _ = size
        ox, oy = overhang if isinstance(overhang, (tuple, list)) else (overhang, overhang)
        w += ox * 2
        d += oy * 2
        cs, sn = math.cos(rot), math.sin(rot)

        def tr(lx, ly, lz):
            return (x + lx * cs - ly * sn, y + lx * sn + ly * cs, z + lz)

        v = [tr(-w / 2, -d / 2, 0), tr(w / 2, -d / 2, 0), tr(-w / 2, d / 2, 0), tr(w / 2, d / 2, 0),
             tr(-w / 2, 0, rise), tr(w / 2, 0, rise)]
        f = [(0, 1, 5, 4), (4, 5, 3, 2), (0, 4, 2), (1, 3, 5), (0, 2, 3, 1)]
        return self.mesh(name, v, f, mat, c, tile=3.)

    def cylinder(self, name, pos, radius, depth, mat, c, verts=10, rot=(0, 0, 0), top_radius=None):
        bm_verts = []
        faces = []
        tr = radius if top_radius is None else top_radius
        for i in range(verts):
            a = math.tau * i / verts
            bm_verts.append((radius * math.cos(a), radius * math.sin(a), -depth / 2))
            bm_verts.append((tr * math.cos(a), tr * math.sin(a), depth / 2))
        for i in range(verts):
            j = (i + 1) % verts
            faces.append((2 * i, 2 * j, 2 * j + 1, 2 * i + 1))
        faces.append(tuple(2 * i + 1 for i in range(verts)))
        faces.append(tuple(2 * i for i in reversed(range(verts))))
        o = self.mesh(name, bm_verts, faces, mat, c, tile=2.)
        o.location = pos
        o.rotation_euler = rot
        return o

    def cone(self, name, pos, radius, height, mat, c, verts=7):
        v = [(radius * math.cos(math.tau * i / verts), radius * math.sin(math.tau * i / verts), 0) for i in range(verts)] + [(0, 0, height)]
        f = [(i, (i + 1) % verts, verts) for i in range(verts)] + [tuple(reversed(range(verts)))]
        o = self.mesh(name, v, f, mat, c, tile=2.)
        o.location = pos
        return o

    def join(self, parts, name, origin=(0, 0, 0)):
        bpy.ops.object.select_all(action='DESELECT')
        for o in parts:
            if o.data.users > 1:
                o.data = o.data.copy()
            o.select_set(True)
        bpy.context.view_layer.objects.active = parts[0]
        bpy.ops.object.join()
        o = bpy.context.object
        o.name = name
        o.data.name = name
        self.group += 1
        # Bake any object transform into the mesh, then keep origin at the requested meter origin.
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        bpy.context.scene.cursor.location = origin
        bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
        o.select_set(False)
        return o


# ---------------------------------------------------------------------------------------------
# Paths
# ---------------------------------------------------------------------------------------------
def smooth(points, step=1.0):
    pts = [Vector((p[0], p[1], 0)) for p in points]
    P = [pts[0]] + pts + [pts[-1]]
    out = []
    for i in range(1, len(P) - 2):
        p0, p1, p2, p3 = P[i - 1], P[i], P[i + 1], P[i + 2]
        n = max(2, int((p2 - p1).length / step))
        for k in range(n):
            t = k / n
            out.append(.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    out.append(pts[-1])
    return out


class Path:
    def __init__(self, control, ds=1.0):
        raw = smooth(control, .5)
        pts = [raw[0]]
        acc = 0.
        for a, b in zip(raw, raw[1:]):
            seg = (b - a).length
            while acc + seg >= ds:
                t = (ds - acc) / seg
                a = a + (b - a) * t
                pts.append(a.copy())
                seg = (b - a).length
                acc = 0.
            acc += seg
        if (pts[-1] - raw[-1]).length > .05:
            pts.append(raw[-1])
        self.pts = pts
        self.ds = ds
        self.length = sum((b - a).length for a, b in zip(pts, pts[1:]))

    def tangent(self, i):
        a = self.pts[max(0, i - 1)]
        b = self.pts[min(len(self.pts) - 1, i + 1)]
        t = b - a
        return t.normalized() if t.length else Vector((0, 1, 0))

    def normal(self, i):
        t = self.tangent(i)
        return Vector((-t.y, t.x, 0))

    def at(self, s):
        i = max(0, min(len(self.pts) - 1, int(round(s / self.ds))))
        return self.pts[i], self.tangent(i), self.normal(i)

    def offset(self, d):
        return [p + self.normal(i) * d for i, p in enumerate(self.pts)]

    def control_list(self, step=.5):
        return [[p.x, p.y] for p in self.pts[::max(1, int(step / self.ds))]]


def ribbon(kit, name, path, width, mat, c, z=0., thick=.3, lateral=0., tile=4., rng=None, start=None, end=None):
    pts = path.pts
    i0 = 0 if start is None else int(start / path.ds)
    i1 = len(pts) if end is None else int(end / path.ds) + 1
    left = []
    right = []
    for i in range(i0, min(i1, len(pts))):
        n = path.normal(i)
        left.append(pts[i] + n * (lateral + width / 2))
        right.append(pts[i] + n * (lateral - width / 2))
    verts = []
    for l, r in zip(left, right):
        verts += [(l.x, l.y, z), (r.x, r.y, z), (l.x, l.y, z - thick), (r.x, r.y, z - thick)]
    faces = []
    for k in range(len(left) - 1):
        a, b = 4 * k, 4 * k + 4
        faces += [(a, a + 1, b + 1, b), (a, b, b + 2, a + 2), (a + 1, a + 3, b + 3, b + 1)]
    return kit.mesh(name, verts, faces, mat, c, tile)


def arch_span(kit, name, cx, y0, y1, r, z_crown, z_top, mat, c):
    """Spandrel wall with a semicircular opening, front and back faces plus the intrados."""
    n = 14
    zc = z_crown - r
    verts = []
    for y in (y0, y1):
        for i in range(n + 1):
            t = math.pi * i / n
            verts.append((cx + r * math.cos(t), y, zc + r * math.sin(t)))
            verts.append((cx + r * math.cos(t), y, z_top))
    faces = []
    m = 2 * (n + 1)
    for i in range(n):
        j = 2 * i
        faces.append((j, j + 2, j + 3, j + 1))
        faces.append((m + j + 1, m + j + 3, m + j + 2, m + j))
        faces.append((j, m + j, m + j + 2, j + 2))
    return kit.mesh(name, verts, faces, mat, c)


def coplanar_overlaps(boxlog, eps=.004, min_area=.0004):
    """Axis-aligned box pairs whose faces share a plane, point the same way and overlap with different materials.

    Those are the pairs a renderer shows as flickering z-fighting. Rotated boxes are ignored.
    """
    items = []
    for name, col, pos, size, rot, mat in boxlog:
        n = round(rot / (math.pi / 2))
        if abs(rot - n * math.pi / 2) > 1e-3:
            continue
        w, d, h = size if n % 2 == 0 else (size[1], size[0], size[2])
        lo = (pos[0] - w / 2, pos[1] - d / 2, pos[2] - h / 2)
        hi = (pos[0] + w / 2, pos[1] + d / 2, pos[2] + h / 2)
        items.append((lo, hi, name, col, mat))
    items.sort(key=lambda t: t[0][0])
    hits = []
    for i, (la, ha, na, ca, ma) in enumerate(items):
        for lb, hb, nb, cb, mb in items[i + 1:]:
            if lb[0] > ha[0]:
                break
            if ca != cb or ma == mb:
                continue
            for k in range(3):
                o, p_ = [j for j in range(3) if j != k]
                ov = (min(ha[o], hb[o]) - max(la[o], lb[o])) * (min(ha[p_], hb[p_]) - max(la[p_], lb[p_]))
                if min(ha[o], hb[o]) - max(la[o], lb[o]) <= 0 or min(ha[p_], hb[p_]) - max(la[p_], lb[p_]) <= 0 or ov < min_area:
                    continue
                # Coplanar downward faces (equal bottoms) sit on the ground or inside a base and are never seen.
                if abs(ha[k] - hb[k]) < eps or (abs(la[k] - lb[k]) < eps and k != 2):
                    hits.append((na, nb, ca, 'xyz'[k], round(ov, 3), [round((a + b) / 2, 1) for a, b in zip(la, ha)]))
                    break
    return hits
