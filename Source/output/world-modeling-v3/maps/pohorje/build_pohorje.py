"""Build Pohorje's production art and its explicit Blender/Unity collision contract.

Blender --background --factory-startup --python-exit-code 1 --python build_pohorje.py
Optional argument after --: --skip-previews.
Layout uses Unity metres. Mesh coordinates are baked to Blender (-x, -z, y).
No existing scenes, prefabs, GUIDs or previous map versions are changed.
"""
import collections
import json
import math
from pathlib import Path
import random
import shutil
import sys

import bpy
import bmesh
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[3]
ART = REPO / 'Game/Assets/Art/Pohorje_v3'
for directory in ('Source', 'Export', 'Textures', 'Previews'):
    (HERE / directory).mkdir(parents=True, exist_ok=True)
ART.mkdir(parents=True, exist_ok=True)
RNG = random.Random(751958)
P = lambda p: Vector((-p[0], -p[2], p[1]))
UP = Vector((0, 1, 0))

PADS = {
    'Bellevue': dict(c=(0, 0), half=(15, 13), r=5, h=40, mat='Trail', owner='Shared'),
    'Fork': dict(c=(0, 56), half=(24, 11), r=5, h=36, mat='Trail', owner='Shared'),
    'ArenaA': dict(c=(-66, 135), half=(24, 22), r=7, h=20, mat='Trail', owner='A'),
    'ArenaB': dict(c=(0, 135), half=(25, 22), r=6, h=18, mat='Snow', owner='B'),
    'ArenaC': dict(c=(64, 132), half=(22, 20), r=3, h=22, mat='Concrete', owner='C'),
    'Overlook': dict(c=(0, 198), half=(20, 10), r=5, h=12, mat='Trail', owner='Shared'),
    'AlcoveC': dict(c=(53, 87.5), half=(4, 4), r=0.5, h=27.2, mat='Concrete', owner='C'),
}
LANES = {
    'Safe': dict(w=12, mat='Trail', owner='Shared', pts=[(0, 8, 40), (0, 16, 40), (1, 28, 39.5), (-1, 42, 37), (0, 50, 36), (0, 56, 36)]),
    'A': dict(w=11, mat='Trail', owner='A', pts=[(-20, 58, 36), (-28, 61, 36), (-39, 71, 33), (-46, 85, 30), (-49, 96, 26), (-57, 106, 20), (-64, 113, 20), (-67, 122, 20)]),
    'B': dict(w=16, mat='Snow', owner='B', pts=[(0, 62, 36), (0, 70, 36), (0, 110, 19), (0, 118, 18), (0, 126, 18)]),
    'C': dict(w=10, mat='Concrete', owner='C', pts=[(20, 58, 36), (28, 61, 36), (36, 69, 34), (42, 82, 30), (47, 94, 26), (53, 108, 22), (61, 118, 22)]),
    'ExitA': dict(w=10, mat='Trail', owner='A', pts=[(-57, 148, 20), (-50, 160, 20), (-38, 172, 17), (-25, 184, 12), (-14, 195, 12)]),
    'ExitB': dict(w=11, mat='Snow', owner='B', pts=[(0, 151, 18), (0, 164, 18), (0, 181, 12), (0, 193, 12)]),
    'ExitC': dict(w=10, mat='Trail', owner='C', pts=[(65, 146, 22), (64, 159, 22), (50, 172, 18), (33, 184, 13), (15, 196, 12)]),
}
RAW_LANES = {name: list(lane['pts']) for name, lane in LANES.items()}


def round_bends(points):
    for _ in range(2):
        out = [points[0]]
        for i, (a, b) in enumerate(zip(points, points[1:])):
            if i:
                out.append(tuple(0.75 * a[j] + 0.25 * b[j] for j in range(3)))
            if i < len(points) - 2:
                out.append(tuple(0.25 * a[j] + 0.75 * b[j] for j in range(3)))
        points = out + [points[-1]]
    return points


for lane in LANES.values():
    lane['pts'] = round_bends(lane['pts'])


def pad_distance(x, z, pad):
    hx, hz = pad['half']; cx, cz = pad['c']; r = pad['r']
    qx = np.abs(x - cx) - hx + r; qz = np.abs(z - cz) - hz + r
    return np.hypot(np.maximum(qx, 0), np.maximum(qz, 0)) + np.minimum(np.maximum(qx, qz), 0) - r


def lane_distance(x, z, lane):
    ds, hs = [], []
    for (ax, az, ah), (bx, bz, bh) in zip(lane['pts'], lane['pts'][1:]):
        vx, vz = bx - ax, bz - az
        t = np.clip(((x - ax) * vx + (z - az) * vz) / (vx * vx + vz * vz), 0, 1)
        ds.append(np.hypot(x - ax - t * vx, z - az - t * vz))
        hs.append(ah + t * (bh - ah))
    ds, hs = np.stack(ds), np.stack(hs)
    nearest = np.min(ds, axis=0)
    weights = np.exp(-(ds - nearest) / 0.65)
    return nearest - lane['w'] / 2, np.sum(weights * hs, axis=0) / weights.sum(axis=0)


def pad_height(name, x, z):
    pad = PADS[name]; cx, cz = pad['c']
    h = np.full(np.shape(x), float(pad['h']))
    if name == 'ArenaA':
        h += 0.9 * np.exp(-((x - cx + 11) ** 2 + (z - cz - 7) ** 2) / 32)
        h -= 0.65 * np.exp(-((x - cx - 6) ** 2 + (z - cz - 8) ** 2) / 24)
    elif name == 'ArenaC':
        d = np.maximum(np.abs(x - cx + 2) - 9, np.abs(z - cz) - 8)
        h -= 0.8 * np.clip(-d / 3.5, 0, 1)
    return h


FEATURES = [('pad', n) for n in PADS] + [('lane', n) for n in LANES]


def fields(x, z):
    ds, hs = [], []
    for kind, name in FEATURES:
        if kind == 'pad':
            ds.append(pad_distance(x, z, PADS[name])); hs.append(pad_height(name, x, z))
        else:
            d, h = lane_distance(x, z, LANES[name]); ds.append(d); hs.append(h)
    return np.stack(ds), np.stack(hs)


def heightfield(x, z):
    ds, hs = fields(x, z)
    # The 1.6 m apron keeps coarse terrain triangles from pulling steep banks into a riding lane.
    d = np.maximum(ds - 1.6, 0)
    t = np.clip(d / 3.1, 0, 1)
    bay=FEATURES.index(('pad','AlcoveC'))
    d[bay]=np.maximum(ds[bay],0)
    t[bay]=np.clip(d[bay]/1.5,0,1)
    banks = 7.5 * t * t * (3 - 2 * t) + np.maximum(d - 3.1, 0) * 0.55
    natural = 54 - 0.12 * z + 2.0 * np.sin(x / 26) * np.cos(z / 36)
    h = np.minimum(natural, np.min(hs + banks, axis=0))
    return np.where(z > 209, np.minimum(h, 11.5 - (z - 209) * 0.9), h)


def feature_at(x, z):
    ds, hs = fields(np.array([x]), np.array([z]))
    idx = int(np.argmin(ds[:, 0]))
    kind, name = FEATURES[idx]
    data = PADS[name] if kind == 'pad' else LANES[name]
    return float(ds[idx, 0]), data['owner'], data['mat']


bpy.ops.wm.read_factory_settings(use_empty=True)
SCENE = bpy.context.scene
SCENE.name = 'Pohorje_Production'
SCENE.unit_settings.system = 'METRIC'
SCENE.unit_settings.scale_length = 1.0
COLLECTIONS = {}
for name in ('Shared', 'Route_A', 'Route_B', 'Route_C', 'Backdrop', 'CollisionGuides', 'Markers', 'Presentation'):
    col = bpy.data.collections.new(name); SCENE.collection.children.link(col); COLLECTIONS[name] = col

# All images are original, deliberately painted pixel patterns. No noise-only surfaces.
PALETTE = {
    'Ground': ('moss', (48, 66, 54)), 'Trail': ('earth', (138, 112, 79)),
    'Snow': ('snow', (169, 184, 187)), 'Stone': ('stone', (77, 91, 101)),
    'Concrete': ('concrete', (116, 126, 123)), 'Timber': ('wood', (121, 89, 57)),
    'Metal': ('metal', (92, 124, 118)), 'Rust': ('rust', (145, 78, 54)),
    'Foliage': ('needles', (43, 70, 55)), 'Dark': ('dark', (30, 40, 45)),
}
MATERIALS = {}


def save_pixels(name, rgb):
    height, width = rgb.shape[:2]
    image = bpy.data.images.new('T_Pohorje_' + name, width, height, alpha=False)
    rgba = np.ones((height, width, 4), dtype=np.float32)
    srgb = np.clip(rgb / 255.0, 0, 1)
    rgba[:, :, :3] = np.where(srgb <= 0.04045, srgb / 12.92, ((srgb + 0.055) / 1.055) ** 2.4)
    image.pixels.foreach_set(rgba[::-1].ravel())
    image.filepath_raw = str(HERE / 'Textures' / ('Pohorje_' + name + '.png'))
    image.file_format = 'PNG'; image.save()
    return image


def paint_texture(name, pattern, base, size=128):
    rng = random.Random(name)
    image = np.full((size, size, 3), base, dtype=float)
    y, x = np.mgrid[0:size, 0:size]
    bayer = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]])
    image += (bayer[y % 4, x % 4, None] - 7.5) * 0.6

    def rect(px, py, w, h, color):
        image[py:py+h, px:px+w] = color

    if pattern in ('moss', 'earth', 'snow', 'stone'):
        if pattern in ('earth','moss'):
            for _ in range(20):
                px,py=rng.randrange(size),rng.randrange(size)
                shade=rng.choice((-11,-7,5,8))
                radius=rng.randrange(7,18)
                mask=((x-px)**2/(radius*radius)+(y-py)**2/(radius*radius*0.4))<1
                image[mask]+=shade
        for _ in range(44 if pattern != 'snow' else 30):
            px, py = rng.randrange(size - 8), rng.randrange(size - 5)
            w, h = rng.randrange(3, 10), rng.randrange(2, 5)
            shade = rng.choice((-17, -11, 9, 15))
            rect(px, py, w, h, np.array(base) + shade)
            if pattern in ('earth', 'stone'):
                rect(px, py, w, 1, np.array(base) + 23)
        if pattern == 'snow':
            for row in (28, 71, 110):
                for xx in range(size):
                    yy = (row + int(3 * math.sin(xx / 21))) % size
                    rect(xx, yy, 1, 2, np.array(base) - 14)
        elif pattern == 'stone':
            for row in (22, 58, 92):
                for xx in range(size):
                    yy = (row + xx // 23) % size
                    rect(xx, yy, 1, 2, np.array(base) - 29)
    elif pattern in ('wood', 'needles'):
        for _ in range(150):
            px, py = rng.randrange(size - 2), rng.randrange(size - 15)
            rect(px, py, 1 if pattern == 'needles' else 2, rng.randrange(3, 15), np.array(base) + rng.choice((-24, -12, 10, 18)))
        if pattern == 'wood':
            for xx in (0, 32, 64, 96):
                rect(xx, 0, 2, size, np.array(base) - 35)
                rect(xx + 2, 0, 1, size, np.array(base) + 21)
    elif pattern in ('metal', 'rust'):
        for xx in range(0, size, 16):
            rect(xx, 0, 2, size, np.array(base) - 25)
            rect(xx + 2, 0, 2, size, np.array(base) + 17)
        for _ in range(35):
            px, py = rng.randrange(size - 6), rng.randrange(size - 7)
            rect(px, py, rng.randrange(2, 6), rng.randrange(2, 7), (105, 67, 49))
    elif pattern == 'concrete':
        for xx in (0, 64):
            rect(xx, 0, 1, size, np.array(base) - 28)
            rect(xx + 1, 0, 1, size, np.array(base) + 14)
        for yy in (0, 64):
            rect(0, yy, size, 1, np.array(base) - 25)
        for xx, yy in ((8, 8), (56, 8), (8, 56), (56, 56), (72, 72), (120, 120)):
            rect(xx, yy, 2, 2, np.array(base) - 22)
        for xx, yy in ((23, 40), (84, 91)):
            for k in range(13):
                rect(xx + k // 4, yy + k, 1, 1, np.array(base) - 31)
    return save_pixels(name, image)


def material(name, image=None, color=None, emission=False):
    mat = bpy.data.materials.new('Pohorje_' + name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Roughness'].default_value = 1
    bsdf.inputs['Specular IOR Level'].default_value = 0.1
    if image:
        node = mat.node_tree.nodes.new('ShaderNodeTexImage'); node.image = image; node.interpolation = 'Closest'
        mat.node_tree.links.new(node.outputs['Color'], bsdf.inputs['Base Color'])
    else:
        bsdf.inputs['Base Color'].default_value = (*color, 1)
    if emission:
        bsdf.inputs['Emission Color'].default_value = (*color, 1)
        bsdf.inputs['Emission Strength'].default_value = 2.2
    MATERIALS[name] = mat
    return mat


for name, (pattern, base) in PALETTE.items():
    material(name, paint_texture(name, pattern, base))
material('Warm', color=(1.0, 0.43, 0.13), emission=True)
material('Violet', color=(0.54, 0.15, 1.0), emission=True)

# Compact original bitmap lettering stays crisp at PS1 texture resolutions.
FONT = {
    'A': '01110/10001/10001/11111/10001/10001/10001', 'B': '11110/10001/10001/11110/10001/10001/11110',
    'C': '01111/10000/10000/10000/10000/10000/01111', 'D': '11110/10001/10001/10001/10001/10001/11110',
    'E': '11111/10000/10000/11110/10000/10000/11111', 'F': '11111/10000/10000/11110/10000/10000/10000',
    'G': '01111/10000/10000/10111/10001/10001/01111', 'H': '10001/10001/10001/11111/10001/10001/10001',
    'I': '11111/00100/00100/00100/00100/00100/11111', 'J': '00111/00010/00010/00010/10010/10010/01100',
    'K': '10001/10010/10100/11000/10100/10010/10001', 'L': '10000/10000/10000/10000/10000/10000/11111',
    'M': '10001/11011/10101/10101/10001/10001/10001', 'N': '10001/11001/10101/10011/10001/10001/10001',
    'O': '01110/10001/10001/10001/10001/10001/01110', 'P': '11110/10001/10001/11110/10000/10000/10000',
    'Q': '01110/10001/10001/10001/10101/10010/01101', 'R': '11110/10001/10001/11110/10100/10010/10001',
    'S': '01111/10000/10000/01110/00001/00001/11110', 'T': '11111/00100/00100/00100/00100/00100/00100',
    'U': '10001/10001/10001/10001/10001/10001/01110', 'V': '10001/10001/10001/10001/10001/01010/00100',
    'W': '10001/10001/10001/10101/10101/10101/01010', 'X': '10001/10001/01010/00100/01010/10001/10001',
    'Y': '10001/10001/01010/00100/00100/00100/00100', 'Z': '11111/00001/00010/00100/01000/10000/11111',
    '0': '01110/10001/10011/10101/11001/10001/01110', '1': '00100/01100/00100/00100/00100/00100/01110',
    '2': '01110/10001/00001/00010/00100/01000/11111', '3': '11110/00001/00001/01110/00001/00001/11110',
    '4': '00010/00110/01010/10010/11111/00010/00010', '5': '11111/10000/10000/11110/00001/00001/11110',
    '6': '01110/10000/10000/11110/10001/10001/01110', '7': '11111/00001/00010/00100/01000/01000/01000',
    '8': '01110/10001/10001/01110/10001/10001/01110', '9': '01110/10001/10001/01111/00001/00001/01110',
    ':': '00000/00100/00100/00000/00100/00100/00000', '-': '00000/00000/00000/11111/00000/00000/00000',
    '>': '10000/01000/00100/00010/00100/01000/10000', '/': '00001/00001/00010/00100/01000/10000/10000',
    '.': '00000/00000/00000/00000/00000/00110/00110', ' ': '00000/00000/00000/00000/00000/00000/00000',
}
SIGNS = [
    ('BELLEVUE', 'POHORJE / 07:58', (57, 84, 73)),
    ('A  GOZDNA STRMINA', 'FOREST TRAIL  >', (63, 86, 59)),
    ('B  GONDOLSKA', 'SERVISNA POT  >', (63, 86, 111)),
    ('C  RAVE TUNEL', 'AFTERPARTY  >', (91, 62, 109)),
    ('MARIBOR  >', 'FERI / 08:00', (45, 73, 78)),
    ('SERVICE 04', 'KEEP CLEAR', (134, 91, 53)),
    ('NO SIGNAL', '07:58', (46, 58, 65)),
    ('AFTERPARTY', 'DO NOT LOOK BACK', (89, 54, 100)),
    ('TRAIL CLOSED', 'DISCOVER ON ANOTHER RUN', (112, 74, 56)),
    ('MAINTENANCE', 'TERMINAL 02', (54, 85, 91)),
    ('WRONG WAY HOME', 'POHORJE', (72, 84, 73)),
    ('1988 / 07:58', 'LAST DEPARTURE', (93, 80, 60)),
    ('A', 'FOREST', (63, 86, 59)), ('B', 'GONDOLA', (63, 86, 111)),
    ('C', 'RAVE TUNEL', (91, 62, 109)), ('DANGER', 'STAY ON THE TRAIL', (129, 78, 51)),
]


def sign_atlas():
    image = np.zeros((512, 512, 3), dtype=float)
    def text(value, x, y, scale, color):
        for char in value:
            for row, line in enumerate(FONT[char].split('/')):
                for col, bit in enumerate(line):
                    if bit == '1':
                        image[y+row*scale:y+(row+1)*scale, x+col*scale:x+(col+1)*scale] = color
            x += 6 * scale
    for index, (title, subtitle, bg) in enumerate(SIGNS):
        x = (index % 2) * 256; y = (index // 2) * 64
        image[y:y+64, x:x+256] = bg
        image[y+3:y+61, x+3:x+253] = np.array(bg) + 14
        image[y+5:y+59, x+5:x+251] = bg
        scale = 2 if len(title) <= 19 else 1
        text(title, x + (256 - len(title)*6*scale)//2, y+13, scale, (225, 216, 174))
        text(subtitle, x + (256 - len(subtitle)*6)//2, y+42, 1, (201, 202, 177))
        for xx in (9, 245):
            image[y+8:y+10, x+xx:x+xx+2] = (32, 40, 40)
            image[y+54:y+56, x+xx:x+xx+2] = (32, 40, 40)
    return save_pixels('Signs', image)


material('Signs', sign_atlas())
GUIDE_MATERIAL = bpy.data.materials.new('Pohorje_CollisionGuide')
GUIDE_MATERIAL.diffuse_color = (1, 0.25, 0.05, 1)


class Geometry:
    def __init__(self):
        self.vertices, self.faces, self.materials, self.uvs = [], [], [], []

    def add(self, vertices, faces, material_name, uvs=None):
        start = len(self.vertices)
        self.vertices.extend(tuple(P(v)) for v in vertices)
        self.faces.extend(tuple(start + i for i in f) for f in faces)
        self.materials.extend([material_name] * len(faces))
        self.uvs.extend(uvs if uvs is not None else [None] * len(faces))

    def object(self, name, collection):
        mesh = bpy.data.meshes.new(name); mesh.from_pydata(self.vertices, [], self.faces); mesh.update()
        materials = list(dict.fromkeys(self.materials))
        for mat in materials:
            mesh.materials.append(MATERIALS[mat])
        uv = mesh.uv_layers.new(name='UVMap')
        for i, face in enumerate(mesh.polygons):
            face.material_index = materials.index(self.materials[i])
            normal = face.normal
            for j, loop_index in enumerate(face.loop_indices):
                vertex = mesh.vertices[mesh.loops[loop_index].vertex_index].co
                if self.uvs[i] is not None:
                    uv.data[loop_index].uv = self.uvs[i][j]
                elif abs(normal.z) >= max(abs(normal.x), abs(normal.y)):
                    uv.data[loop_index].uv = (vertex.x / 3.0, vertex.y / 3.0)
                elif abs(normal.x) >= abs(normal.y):
                    uv.data[loop_index].uv = (vertex.y / 2.0, vertex.z / 2.0)
                else:
                    uv.data[loop_index].uv = (vertex.x / 2.0, vertex.z / 2.0)
        bm = bmesh.new(); bm.from_mesh(mesh)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        if '_Ground_' in name:
            bmesh.ops.reverse_faces(bm, faces=[face for face in bm.faces if face.normal.z < 0])
        bmesh.ops.triangulate(bm, faces=bm.faces[:])
        bm.to_mesh(mesh); bm.free()
        obj = bpy.data.objects.new(name, mesh); COLLECTIONS[collection].objects.link(obj)
        return obj


BATCHES = {}
COLLIDERS, MARKERS = [], []
COLLIDER_NAMES = set()


def geo(owner, category, center):
    cell = (math.floor(center[0] / 24), math.floor(center[2] / 24))
    key = (owner, category, *cell)
    return BATCHES.setdefault(key, Geometry())


def basis(direction):
    forward = Vector(direction).normalized()
    right = UP.cross(forward).normalized() if abs(forward.y)<0.999 else Vector((1,0,0))
    up = forward.cross(right).normalized()
    return right, up, forward


BOX_FACES = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]


def box(g, center, size, mat='Timber', direction=(0, 0, 1)):
    right, up, forward = basis(direction)
    c = Vector(center); sx, sy, sz = (d / 2 for d in size)
    vertices = [c + right*x*sx + up*y*sy + forward*z*sz
                for x, y, z in [(-1,-1,-1), (1,-1,-1), (1,-1,1), (-1,-1,1), (-1,1,-1), (1,1,-1), (1,1,1), (-1,1,1)]]
    g.add(vertices, BOX_FACES, mat)


def cylinder(g, a, b, radius, mat='Timber', sides=7, end_radius=None):
    a, b = Vector(a), Vector(b); axis = (b - a).normalized()
    cross = axis.cross(Vector((0,0,1)) if abs(axis.z) < 0.9 else Vector((1,0,0))).normalized()
    other = axis.cross(cross)
    vertices = []
    for center, rad in ((a, radius), (b, radius if end_radius is None else end_radius)):
        vertices += [center + (cross*math.cos(i*math.tau/sides) + other*math.sin(i*math.tau/sides))*rad for i in range(sides)]
    faces = [tuple(reversed(range(sides))), tuple(range(sides, 2*sides))]
    faces += [(i, (i+1)%sides, (i+1)%sides+sides, i+sides) for i in range(sides)]
    g.add(vertices, faces, mat)


def beam(g, a, b, width, height, mat):
    a, b = Vector(a), Vector(b)
    box(g, (a+b)/2, (width, height, (b-a).length), mat, b-a)


def collider_box(name, owner, center, size, direction=(0, 0, 1), role='obstacle', trigger=False):
    name = 'COL_Box_' + name
    assert name not in COLLIDER_NAMES, name
    COLLIDER_NAMES.add(name)
    right, up, forward = basis(direction)
    rotation = Matrix((right, up, forward)).transposed().to_quaternion()
    COLLIDERS.append(dict(name=name, owner=owner, kind='Box', center=list(center), size=list(size),
                          rotation=[rotation.x, rotation.y, rotation.z, rotation.w], role=role, trigger=trigger))
    guide = Geometry(); box(guide, center, size, 'Dark', direction)
    ob = guide.object(name, 'CollisionGuides'); ob.data.materials.clear(); ob.data.materials.append(GUIDE_MATERIAL)
    ob.display_type = 'WIRE'; ob.hide_render = True


def collider_capsule(name, owner, a, b, radius):
    name = 'COL_Capsule_' + name
    assert name not in COLLIDER_NAMES, name
    COLLIDER_NAMES.add(name)
    a, b = Vector(a), Vector(b); length = (b-a).length
    rotation = Vector((0,1,0)).rotation_difference((b-a).normalized())
    COLLIDERS.append(dict(name=name, owner=owner, kind='Capsule', center=list((a+b)/2),
                          size=[0,0,0], rotation=[rotation.x, rotation.y, rotation.z, rotation.w],
                          radius=radius, height=max(length, radius*2), role='obstacle', trigger=False))
    guide = Geometry()
    axis=(b-a).normalized()
    tangent=axis.cross(Vector((0,0,1)) if abs(axis.z)<0.95 else Vector((1,0,0))).normalized()
    other=axis.cross(tangent)
    vertices=[a]
    for center,angles in ((a+axis*radius,(-math.pi/3,-math.pi/6,0)),(b-axis*radius,(0,math.pi/6,math.pi/3))):
        for angle in angles:
            vertices += [center+axis*math.sin(angle)*radius+(tangent*math.cos(k*math.tau/8)+other*math.sin(k*math.tau/8))*math.cos(angle)*radius for k in range(8)]
    vertices.append(b)
    faces=[(0,1+k,1+(k+1)%8) for k in range(8)]
    faces += [(1+ring*8+k,1+ring*8+(k+1)%8,1+(ring+1)*8+(k+1)%8,1+(ring+1)*8+k) for ring in range(5) for k in range(8)]
    faces += [(49,41+(k+1)%8,41+k) for k in range(8)]
    guide.add(vertices,faces,'Dark')
    ob = guide.object(name, 'CollisionGuides'); ob.data.materials.clear(); ob.data.materials.append(GUIDE_MATERIAL)
    ob.display_type = 'WIRE'; ob.hide_render = True


def marker(name, owner, position, kind, size=(0,0,0), forward=(0,0,1)):
    name = 'MK_' + name
    assert not any(m['name'] == name for m in MARKERS), name
    obj = bpy.data.objects.new(name, None); COLLECTIONS['Markers'].objects.link(obj)
    obj.location = P(position); obj.empty_display_size = 0.65
    obj['Owner'] = owner; obj['Kind'] = kind
    MARKERS.append(dict(name=name, owner=owner, position=list(position), kind=kind, size=list(size), forward=list(forward), blender_position=list(P(position))))


# One contiguous, triangulated surface: every route is carved into the same ground.
X = np.arange(-110, 105, 2.0); Z = np.arange(-28, 243, 2.0)
GX, GZ = np.meshgrid(X, Z)
GH = heightfield(GX.ravel(), GZ.ravel()).reshape(GX.shape)
terrain_vertices = [P((float(x), float(y), float(z))) for x, y, z in zip(GX.ravel(), GH.ravel(), GZ.ravel())]
terrain_faces = []
terrain_groups = {}
for iz in range(len(Z)-1):
    for ix in range(len(X)-1):
        a = iz*len(X)+ix; b = a+1; c = a+len(X); d = c+1
        faces = [(a,c,b), (b,c,d)] if (ix+iz)%2 else [(a,c,d), (a,d,b)]
        cx, cz = float(X[ix]+1), float(Z[iz]+1)
        ds, hs = fields(np.array([cx]), np.array([cz]))
        candidates = np.where(ds[:,0] <= 0.3, hs[:,0], np.inf)
        inside_pads=[i for i,(kind,name) in enumerate(FEATURES) if kind=='pad' and ds[i,0]<-0.8]
        idx = min(inside_pads,key=lambda i: hs[i,0]) if inside_pads else (int(np.argmin(candidates)) if np.any(np.isfinite(candidates)) else int(np.argmin(ds[:,0])))
        kind, name = FEATURES[idx]; feature = PADS[name] if kind == 'pad' else LANES[name]
        mat = feature['mat'] if ds[idx,0] <= -0.25 else ('Stone' if ds[idx,0] < 3.6 else 'Ground')
        owner = feature['owner'] if ds[idx,0] < 8 else 'Backdrop'
        key = (owner, math.floor(cx/48), math.floor(cz/48))
        terrain_groups.setdefault(key, []).extend((f, mat) for f in faces)
        terrain_faces.extend(faces)
TERRAIN_BVH = BVHTree.FromPolygons(terrain_vertices, terrain_faces, all_triangles=True)


def ground(x, z):
    hit, _, _, _ = TERRAIN_BVH.ray_cast(P((x,150,z)), Vector((0,0,-1)), 400)
    if hit is None:
        raise ValueError('Ground missing at ' + str((x,z)))
    return hit.z


TERRAIN_OBJECTS = []
for (owner, ix, iz), faces in terrain_groups.items():
    g = Geometry(); used = sorted({i for f, _ in faces for i in f}); remap = {v:i for i,v in enumerate(used)}
    g.vertices = [terrain_vertices[i] for i in used]
    for face, mat in faces:
        g.faces.append(tuple(remap[i] for i in face)); g.materials.append(mat); g.uvs.append(None)
    name = f'PL_{owner}_Ground_{ix+4:02}_{iz+1:02}'
    ob = g.object(name, 'Route_'+owner if owner in 'ABC' and len(owner)==1 else owner)
    TERRAIN_OBJECTS.append(name)


def lane_point(name, t):
    points = LANES[name]['pts']; lengths = [math.dist(a[:2], b[:2]) for a,b in zip(points, points[1:])]
    distance = t * sum(lengths)
    for (a,b), length in zip(zip(points, points[1:]), lengths):
        if distance <= length or b == points[-1]:
            u = min(1, distance/length)
            return (a[0]+u*(b[0]-a[0]), a[1]+u*(b[1]-a[1])), (b[0]-a[0], b[1]-a[1])
        distance -= length


def sign(g, center, width, index, direction=(0,0,1)):
    right, up, forward = basis(direction); c = Vector(center)
    box(g, center, (width+0.16, width/4+0.14, 0.14), 'Timber', direction)
    v = [c + right*x*width/2 + up*y*width/8 - forward*0.081 for x,y in [(-1,-1),(1,-1),(1,1),(-1,1)]]
    x0 = ((index%2)*256+1)/512; x1 = ((index%2+1)*256-1)/512
    y0 = 1-((index//2+1)*64-1)/512; y1 = 1-((index//2)*64+1)/512
    g.add(v, [(0,1,2,3)], 'Signs', [[(x0,y0),(x1,y0),(x1,y1),(x0,y1)]])


def pine(owner, x, z, scale, seed, hero=False):
    rng = random.Random(seed); y = ground(x,z)-0.15
    g = geo(owner, 'Forest' if not hero else 'Trees', (x,y,z))
    height = rng.uniform(10.0,15.5)*scale; radius = 0.25*scale
    lean = rng.uniform(-0.65,0.65)
    cylinder(g,(x,y,z),(x+lean,y+height*0.82,z+lean*0.6),radius,'Timber',6,radius*0.32)
    tiers = 3+seed%3; sides = 5+seed%2; phase = rng.random()*math.tau
    for tier in range(tiers):
        base = y+(5.0 if hero else 2.0)*scale+tier*height*(0.54/tiers)
        rad = (2.7-tier*(1.9/tiers))*scale
        apex = min(y+height,base+height*0.40)
        vertices = [(x+lean*0.6,apex,z+lean*0.3)]
        for ring in range(2):
            for k in range(sides):
                angle = phase+tier*0.28+k*math.tau/sides
                r = rad*(0.78+rng.random()*0.40)*(1 if ring==0 else 0.36)
                vertices.append((x+lean*0.5+math.cos(angle)*r,base+(rng.random()-0.5)*0.7*scale+(0 if ring==0 else 0.5*scale),z+math.sin(angle)*r))
        faces = [(0,1+k,1+(k+1)%sides) for k in range(sides)]
        faces += [(1+k,1+sides+k,1+sides+(k+1)%sides,1+(k+1)%sides) for k in range(sides)]
        faces.append(tuple(range(1+sides,1+2*sides)))
        g.add(vertices,faces,'Foliage')
    if hero:
        collider_capsule(f'Pine_{seed}',owner,(x,y,z),(x+lean,y+height*0.82,z+lean*0.6),radius+0.02)
        for angle in (0.2,2.3,4.4):
            px,pz=x+math.cos(angle)*0.8*scale,z+math.sin(angle)*0.8*scale
            beam(g,(x,y+0.3,z),(px,ground(px,pz)+0.03,pz),0.16,0.12,'Timber')


def boulder(owner,x,z,s,seed):
    rng = random.Random(seed); y=ground(x,z)
    g=geo(owner,'Rockwork',(x,y,z))
    vertices=[]
    for ring,(h,r) in enumerate(((-0.25,0.82),(0.8,1.0),(1.55,0.55))):
        for k in range(5):
            a=k*math.tau/5+0.28*ring; rr=r*s*(0.85+rng.random()*0.25)
            vertices.append((x+math.cos(a)*rr,y+h*s,z+math.sin(a)*rr))
    faces=[tuple(reversed(range(5))),tuple(range(10,15))]
    faces += [(ring*5+k,ring*5+(k+1)%5,(ring+1)*5+(k+1)%5,(ring+1)*5+k) for ring in range(2) for k in range(5)]
    g.add(vertices,faces,'Stone')


def lamp(owner,name,x,z,direction=(0,0,1),neon=False):
    y=ground(x,z); g=geo(owner,'Furniture',(x,y,z)); right,up,forward=basis(direction)
    c=Vector((x,y,z)); top=c+UP*4.6
    box(g,c+UP*2.1,(0.18,4.6,0.18),'Dark')
    box(g,c+UP*0.22,(0.5,0.6,0.5),'Concrete')
    beam(g,top,top-forward*0.9,0.13,0.13,'Metal')
    head=top-forward*0.75-UP*0.18
    box(g,head,(0.62,0.24,0.7),'Dark',direction)
    box(g,head-UP*0.135,(0.48,0.035,0.55),'Violet' if neon else 'Warm',direction)
    collider_box(name,owner,c+UP*2.0,(0.24,4.5,0.24))
    marker('Light_'+name,owner,head-UP*0.25,'light_violet' if neon else 'light_warm',size=(9,0,0))


def fence(owner,name,points):
    for segment,(a,b) in enumerate(zip(points,points[1:])):
        ax,az=a; bx,bz=b; length=math.dist(a,b); count=max(1,math.ceil(length/3.8))
        for k in range(count):
            t0,t1=k/count,(k+1)/count
            x0,z0=ax+(bx-ax)*t0,az+(bz-az)*t0; x1,z1=ax+(bx-ax)*t1,az+(bz-az)*t1
            y0,y1=ground(x0,z0),ground(x1,z1)
            g=geo(owner,'Furniture',(x0,y0,z0))
            box(g,(x0,y0+0.65,z0),(0.22,1.6,0.22),'Timber')
            for h in (0.5,1.1):
                beam(g,(x0,y0+h,z0),(x1,y1+h,z1),0.13,0.16,'Timber')
            collider_box(f'{name}_{segment}_{k}',owner,((x0+x1)/2,(y0+y1)/2+0.65,(z0+z1)/2),
                         (0.26,1.5,math.hypot(x1-x0,z1-z0)+0.06),(x1-x0,y1-y0,z1-z0))


def ramp(owner,name,a,b,width,rise,mat='Timber'):
    # The top is one inclined box, shared by visual and collision. No convex fitting.
    ax,az=a; bx,bz=b; ay=ground(ax,az)+0.04; by=ground(bx,bz)+rise
    av,bv=Vector((ax,ay,az)),Vector((bx,by,bz)); direction=(bv-av).normalized()
    _,up,_=basis(direction); center=(av+bv)/2-up*0.16
    g=geo(owner,'Ramps',center)
    box(g,center,(width,0.32,(bv-av).length),mat,direction)
    collider_box(name,owner,center,(width,0.32,(bv-av).length),direction,role='walkable')
    # Broad buried foundations make the ramp read as supported, not floating.
    for side in (-1,1):
        right,_,_=basis(direction)
        pos=bv+right*side*(width/2-0.16)
        gy=ground(pos.x,pos.z)
        if pos.y>gy+0.25:
            box(g,(pos.x,(pos.y+gy)/2,pos.z),(0.25,pos.y-gy+0.1,0.4),'Dark')


def gate(owner,which,x,z,dx,dz,width):
    name=f'{owner}_{which}'; direction=Vector((dx,0,dz)).normalized(); right,_,_=basis(direction)
    y=ground(x,z); g=geo('Shared' if which=='Front' else owner,'GateFrames',(x,y,z))
    for side in (-1,1):
        p=Vector((x,y,z))+right*side*(width/2+0.4)
        box(g,p+UP*2.6,(0.65,5.8,0.8),'Concrete',direction)
        box(g,p+UP*0.3,(1.0,0.8,1.1),'Stone',direction)
        collider_box('GatePillar_'+name+('_L' if side<0 else '_R'),owner,p+UP*2.6,(0.65,5.8,0.8),direction)
        # Visible wing walls close the gravel apron; a closed gate cannot be bypassed around its posts.
        wing=Vector((x,y+2.55,z))+right*side*(width/2+2.5)
        box(g,wing,(4.0,5.8,1.05),'Concrete',direction)
        box(g,wing+UP*2.98,(4.1,0.2,1.17),'Stone',direction)
        collider_box('GateWing_'+name+('_L' if side<0 else '_R'),owner,wing,(4.0,5.8,1.05),direction)
    box(g,(x,y+5.35,z),(width+1.5,0.7,0.85),'Timber',direction)
    sign(g,Vector((x,y+5.25,z))-direction*0.48,min(width-0.8,7.2),{'A':1,'B':2,'C':3}[owner] if which=='Front' else 4,direction)
    # Opaque seals are separate named objects; interior hiding cannot hide these.
    seal=Geometry(); box(seal,(x,y+2.45,z),(width+0.2,5.1,0.35),'Metal' if owner=='B' else 'Timber',direction)
    for off in (-width*0.32,width*0.32):
        p=Vector((x,y+2.4,z))+right*off-direction*0.2
        box(seal,p,(0.13,4.8,0.12),'Rust',direction)
    sign(seal,Vector((x,y+2.6,z))-direction*0.2,min(width*0.65,5.7),8,direction)
    seal.object(f'PL_Seal_{name}','Shared')
    collider_box('Seal_'+name,owner,(x,y+2.45,z),(width+0.2,5.1,0.42),direction,role='seal_'+which.lower())
    marker('Gate_'+name,owner,(x,y,z),'gate_'+which.lower(),(width,5.1,0.42),direction)
    if which=='Front':
        marker('Trigger_'+owner,owner,Vector((x,y+1.8,z))+direction*1.6,'route_selection',(width,3.6,2.4),direction)


def cabin(owner,name,x,z,yaw=(0,0,1),raised=0):
    y=ground(x,z)+raised; g=geo(owner,'Architecture',(x,y,z))
    right,up,forward=basis(yaw); c=Vector((x,y,z))
    def local(dx,dy,dz): return c+right*dx+UP*dy+forward*dz
    box(g,local(0,0.12,0),(7.4,0.45,5.4),'Stone',yaw)
    box(g,local(0,1.85,0),(6.6,3.3,4.6),'Timber',yaw)
    # Window and shutter relief stay outside the solid building envelope.
    for dx in (-1.9,1.9):
        box(g,local(dx,2.0,-2.33),(1.4,1.15,0.12),'Dark',yaw)
        for side in (-1,1):
            box(g,local(dx+side*0.82,2.0,-2.4),(0.24,1.3,0.12),'Metal',yaw)
        box(g,local(dx,1.37,-2.5),(1.8,0.12,0.45),'Timber',yaw)
        box(g,local(dx,2.0,-2.42),(0.07,1.12,0.08),'Timber',yaw)
    box(g,local(0,1.3,-2.33),(1.1,2.5,0.14),'Metal',yaw)
    box(g,local(0,1.6,-2.42),(0.65,0.55,0.08),'Dark',yaw)
    verts=[local(-3.8,3.5,-2.8),local(3.8,3.5,-2.8),local(-3.8,3.5,2.8),local(3.8,3.5,2.8),
           local(-3.8,5.3,0),local(3.8,5.3,0)]
    g.add(verts,[(0,1,5,4),(4,5,3,2),(0,4,2),(1,3,5),(0,2,3,1)],'Rust')
    for side in (-1,1):
        slope=UP*1.8-forward*side*2.8
        collider_box(name+('_RoofN' if side<0 else '_RoofS'),owner,local(0,4.4,side*1.4),
                     (7.6,0.16,slope.length),slope,role='roof')
    for dz in (-2.8,2.8):
        beam(g,local(-4,3.45,dz),local(4,3.45,dz),0.15,0.16,'Timber')
    box(g,local(2.0,4.5,0.9),(0.55,2.4,0.55),'Concrete',yaw)
    sign(g,local(0,3.0,-2.43),4.3,0 if owner=='Shared' else 9,yaw)
    collider_box(name,owner,local(0,1.7,0),(6.6,3.6,4.6),yaw)


def crate(owner,name,x,z,scale=1,direction=(0,0,1)):
    y=ground(x,z); g=geo(owner,'Equipment',(x,y,z)); c=Vector((x,y+0.65*scale,z))
    size=(1.35*scale,1.3*scale,1.35*scale)
    box(g,c,size,'Timber',direction)
    right,_,forward=basis(direction)
    for off in (-0.43,0.43):
        box(g,c+right*off*scale,(0.12*scale,1.36*scale,1.4*scale),'Dark',direction)
    collider_box(name,owner,c,size,direction)


def cart(owner,name,x,z,direction=(0,0,1)):
    y=ground(x,z); g=geo(owner,'Equipment',(x,y,z)); c=Vector((x,y,z)); right,_,forward=basis(direction)
    box(g,c+UP*0.58,(2.0,0.95,3.1),'Rust',direction)
    box(g,c+UP*1.08,(1.7,0.18,2.8),'Dark',direction)
    for end in (-1,1):
        beam(g,c+forward*end*1.15-right*1.12+UP*0.38,c+forward*end*1.15+right*1.12+UP*0.38,0.15,0.15,'Dark')
        for side in (-1,1):
            p=c+forward*end*1.15+right*side*1.1+UP*0.38
            cylinder(g,p-right*0.13,p+right*0.13,0.38,'Dark',8)
            cylinder(g,p-right*0.14,p+right*0.14,0.14,'Metal',6)
    collider_box(name,owner,c+UP*0.65,(2.24,1.3,3.1),direction)


# Bellevue: a recognizable abandoned mountain stop, with a sheltered porch and lookout rail.
cabin('Shared','BellevueCabin',9,-6,(1,0,0))
g=geo('Shared','BellevueFurniture',(-8,40,-2))
for zz in (-6.5,4):
    box(g,(-8,40.65,zz),(3.6,0.18,0.72),'Timber')
    box(g,(-8,41.15,zz+0.35),(3.6,0.7,0.14),'Timber')
    for xx in (-9.3,-6.7):
        box(g,(xx,40.3,zz),(0.18,0.8,0.65),'Dark')
    collider_box('Bench_'+str(int(zz*10)).replace('-','N'),'Shared',(-8,40.7,zz),(3.6,1.35,0.9))
box(g,(-12.8,42,-0.5),(0.22,4.4,0.22),'Timber')
sign(g,(-12.8,43.4,-0.5),6.2,10,(0,0,1))
fence('Shared','BellevueRail',[(-12,10),(-15,6),(-15,-9),(-11,-12),(13,-12),(15,-7),(15,8),(10,12)])
lamp('Shared','BellevueWest',-11,9)
lamp('Shared','BellevueEast',11,11)
marker('Start_Player','Shared',(0,40.06,-5),'player_start')
marker('Intro_LowCamera','Shared',(0,40.3,-10),'intro_camera')
marker('Intro_GuiltSilhouette','Shared',(-31,ground(-31,23),23),'narrative_event')
for x,z in [(-3.5,-5),(-3.2,-4.4),(4.2,5),(-5.2,6)]:
    y=ground(x,z); g=geo('Shared','SmallDetails',(x,y,z))
    cylinder(g,(x,y,z),(x+0.06,y+0.28,z+0.07),0.065,'Metal',6)

# Shared learning run: soft bends, broad sight lines, one optional supported kicker.
ramp('Shared','SafeKicker',(-3.2,26),(-3.2,31),3.5,0.65)
for k,t in enumerate((0.33,0.72)):
    (x,z),(dx,dz)=lane_point('Safe',t); length=math.hypot(dx,dz)
    lamp('Shared','SafeLamp_'+str(k),x+dz/length*7,z-dx/length*7)

# The fork announces three different silhouettes before the player chooses.
g=geo('Shared','Wayfinding',(-8,36,52))
box(g,(-8,38.5,51),(0.3,5.5,0.3),'Timber')
for index,yy in enumerate((40.8,39.65,38.5),1):
    sign(g,(-8,yy,50.8),5.0,index)
collider_capsule('ForkSign','Shared',(-8,35.8,51),(-8,41.1,51),0.19)
gate('A','Front',-26,60.8,-8,3,10.4)
gate('B','Front',0,67.5,0,1,15.4)
gate('C','Front',26,60.8,8,3,9.4)
gate('A','Rear',-43,168,12,12,9.8)
gate('B','Rear',0,172,0,1,10.8)
gate('C','Rear',48,173,-14,12,9.8)

# Route A: an asymmetrical spruce colonnade, road-side timber and a logging landing.
for k,t in enumerate((0.15,0.32,0.50,0.68,0.84)):
    (x,z),(dx,dz)=lane_point('A',t); length=math.hypot(dx,dz)
    off=3.7*(-1 if k%2 else 1)
    pine('A',x+dz/length*off,z-dx/length*off,1.1+(k%3)*0.1,100+k,True)
for k,t in enumerate((0.26,0.63)):
    (x,z),(dx,dz)=lane_point('A',t); length=math.hypot(dx,dz); rx,rz=dz/length,-dx/length
    side=1 if k==0 else -1
    ax,az=x+rx*side*4.0,z+rz*side*4.0; bx,bz=x+rx*side*7,z+rz*side*7
    y=ground(ax,az)+0.2
    g=geo('A','Timber',(x,y,z)); cylinder(g,(ax,y,az),(bx,y+0.2,bz),0.4,'Timber',8)
    collider_capsule('TrailLog_'+str(k),'A',(ax,y,az),(bx,y+0.2,bz),0.39)
for k,t in enumerate((0.24,0.56,0.82)):
    (x,z),(dx,dz)=lane_point('A',t); length=math.hypot(dx,dz); side=(-1 if k%2 else 1)
    lamp('A','ForestLamp_'+str(k),x+dz/length*6.4*side,z-dx/length*6.4*side)
for k,(dx,dz) in enumerate([(-15,-13),(13,-14),(-17,8),(14,9),(-5,15)]):
    pine('A',-66+dx,135+dz,1.2+(k%2)*0.15,150+k,True)
for pile,(x,z) in enumerate([(-82,129),(-52,145)]):
    y=ground(x,z); g=geo('A','TimberLanding',(x,y,z))
    for row,count in ((0,4),(1,3),(2,2)):
        for k in range(count):
            xx=x+(k-(count-1)/2)*0.72
            a=(xx,y+0.36+row*0.6,z-2.6); b=(xx,y+0.36+row*0.6,z+2.6)
            cylinder(g,a,b,0.36,'Timber',8)
    # A snug box covers the stack footprint; individual logs do not add physics cost.
    collider_box('LogStack_'+str(pile),'A',(x,y+0.9,z),(2.9,1.8,5.2))
for k,(x,z) in enumerate([(-86,141),(-50,123),(-82,117)]):
    boulder('A',x,z,1.9,200+k)
    y=ground(x,z); collider_box('ClearingRock_'+str(k),'A',(x,y+1.0,z),(2.5,2.3,2.5),(0.5,0,1))
lamp('A','LandingWest',-84,151); lamp('A','LandingEast',-49,119)
stump_x,stump_z=-75,133; stump_y=ground(stump_x,stump_z)
g=geo('A','TimberLanding',(stump_x,stump_y,stump_z))
cylinder(g,(stump_x,stump_y-0.15,stump_z),(stump_x,stump_y+2.0,stump_z),1.1,'Timber',7,0.85)
cylinder(g,(stump_x,stump_y+2.0,stump_z),(stump_x,stump_y+2.03,stump_z),0.81,'Trail',7)
for angle in (0.2,1.9,3.5,5.2):
    px,pz=stump_x+math.cos(angle)*1.9,stump_z+math.sin(angle)*1.9
    beam(g,(stump_x,stump_y+0.6,stump_z),(px,ground(px,pz)+0.02,pz),0.32,0.19,'Timber')
collider_box('LandingStump','A',(stump_x,stump_y+1.0,stump_z),(2.15,2.1,2.15))
boulder('A',-59,130,1.8,279)
rock_y=ground(-59,130)
collider_box('LandingCoverRock','A',(-59,rock_y+1.0,130),(2.8,2.4,2.8))

# Route B: gondola infrastructure has a coherent cable axis, solid pylons and a terminal.
cart('B','SlopeCart_N',5.3,79,(0.65,0,1))
cart('B','SlopeCart_S',-5.3,106,(-0.4,0,1))
ramp('B','PlankJump_N',(-4.2,83),(-4.2,88),3.7,1.0)
ramp('B','PlankJump_S',(4.2,98),(4.2,103),3.7,1.0)
cable=[]
for k,(x,z) in enumerate([(12,28),(13,72),(13,108),(17,140)]):
    y=ground(x,z); top=y+12.8; g=geo('B','GondolaLine',(x,y,z))
    for side in (-1,1):
        a=(x+side*1.6,y-0.2,z); b=(x+side*0.5,top-1.1,z)
        beam(g,a,b,0.45,0.5,'Metal')
        collider_box(f'Pylon_{k}_{side+1}', 'B', (Vector(a)+Vector(b))/2,(0.5,0.5,math.dist(a,b)),Vector(b)-Vector(a))
    beam(g,(x-4.2,top,z),(x+4.2,top,z),0.4,0.45,'Metal')
    box(g,(x,top-1.2,z),(1.6,0.5,1.1),'Rust')
    for side in (-1,1):
        cylinder(g,(x+side*3.0,top+0.1,z-0.3),(x+side*3.0,top+0.1,z+0.3),0.32,'Dark',8)
    cable.append((x-3.0,top+0.35,z))
for k,(a,b) in enumerate(zip(cable,cable[1:])):
    g=geo('B','GondolaCable',a)
    middle=(Vector(a)+Vector(b))/2-UP*1.6
    for p,q in ((a,middle),(middle,b)):
        cylinder(g,p,q,0.055,'Dark',4)
    if k in (1,2):
        p=Vector(a).lerp(Vector(b),0.47)-UP*1.3
        cylinder(g,p,p-UP*1.5,0.08,'Dark',5)
        box(g,p-UP*2.55,(2.0,2.15,2.3),'Rust')
        box(g,p-UP*1.75,(2.1,0.35,2.4),'Metal')
        for side in (-1,1):
            box(g,p+Vector((side*1.01,-2.25,0)),(0.025,0.75,1.65),'Dark')
        box(g,p+Vector((0,-2.25,-1.16)),(1.5,0.75,0.025),'Dark')
        if k==1:
            marker('Watcher_B','B',p-UP*1.7,'watcher_perch')
            box(g,p+Vector((0.4,-2.25,-1.19)),(0.12,0.12,0.03),'Warm')

# Terminal roof is removable and independent from the playable yard.
g=geo('B','Terminal',(16,18,143))
box(g,(16,19.25,143),(12,2.5,9),'Concrete')
collider_box('TerminalBase','B',(16,19.25,143),(12,2.5,9))
roof=Geometry(); box(roof,(16,25.7,143),(13.7,0.4,11.0),'Metal')
roof.object('PL_B_RemovableRoof_Terminal','Route_B')
collider_box('TerminalRoof','B',(16,25.7,143),(13.7,0.4,11.0),role='roof')
for xx in (10.3,21.7):
    for zz in (138.8,147.2):
        box(g,(xx,23,zz),(0.35,5.0,0.35),'Rust')
        collider_box(f'TerminalPost_{int(xx*10)}_{int(zz*10)}','B',(xx,23,zz),(0.35,5,0.35))
sign(g,(16,24.8,137.45),8.6,9)
sign(g,(16,21.2,138.44),2.5,6)
ramp('B','TerminalAccess',(16,127),(16,138.5),4.2,2.5,'Concrete')
for k,(x,z) in enumerate([(-16,123),(-17.5,126),(16,117),(20,125)]):
    crate('B','Crate_'+str(k),x,z,1.05,(0.3,0,1))
cart('B','YardCart',-11,146,(0.8,0,1))
lamp('B','YardNorth',-20,118); lamp('B','YardSouth',-20,152)

# Route C: square-ended modular walls meet at mitred bends. A flat ceiling clears camera/jump space.
tunnel_nodes=[RAW_LANES['C'][3],RAW_LANES['C'][4],RAW_LANES['C'][5]]
for k,(a,b) in enumerate(zip(tunnel_nodes,tunnel_nodes[1:])):
    ax,az,ah=a; bx,bz,bh=b
    av,bv=Vector((ax,ah,az)),Vector((bx,bh,bz)); direction=(bv-av).normalized()
    right,normal,forward=basis(direction); center=(av+bv)/2
    length=(bv-av).length+0.65; half=5.0; height=5.8
    g=geo('C','TunnelShell',center)
    for side in (-1,1):
        wall_center=center+right*side*(half+0.65)+normal*2.5
        if k==0 and side==1:
            for part,(t0,t1) in enumerate(((0,0.5),(0.9,1))):
                wall=av.lerp(bv,(t0+t1)/2)+right*(half+0.65)+normal*2.5
                span=(bv-av).length*(t1-t0)+0.3
                box(g,wall,(1.6,6.8,span),'Concrete',direction)
                collider_box(f'TunnelWindowPier_{part}','C',wall,(1.6,6.8,span),direction)
            lintel=av.lerp(bv,0.7)+right*(half+0.65)+normal*5.1
            box(g,lintel,(1.6,1.6,(bv-av).length*0.4+0.1),'Concrete',direction)
            collider_box('TunnelWindowLintel','C',lintel,(1.6,1.6,(bv-av).length*0.4+0.1),direction)
            window=av.lerp(bv,0.7)+right*4.92+normal*2.0
            for offset in np.arange(-2.4,2.41,0.45):
                p=window+forward*offset
                cylinder(g,p-normal*1.9,p+normal*1.9,0.055,'Dark',5)
            collider_box('RevealGrate','C',window,(0.12,4.0,5.0),direction)
        else:
            box(g,wall_center,(1.6,6.8,length),'Concrete',direction)
            collider_box(f'TunnelWall_{k}_{side+1}','C',wall_center,(1.6,6.8,length),direction)
        # Recessed strip is above the shoulder and outside the swept player capsule.
        if k==0 and side==1:
            for t0,t1 in ((0,0.5),(0.9,1)):
                strip=av.lerp(bv,(t0+t1)/2)+right*(half-0.19)+normal*1.35
                box(g,strip,(0.055,0.15,(bv-av).length*(t1-t0)-0.25),'Violet',direction)
        else:
            strip=center+right*side*(half-0.19)+normal*1.35
            box(g,strip,(0.055,0.15,length-0.6),'Violet',direction)
    roof=Geometry(); roof_center=center+normal*6.0
    box(roof,roof_center,(13.0,0.6,length),'Concrete',direction)
    roof.object(f'PL_C_RemovableRoof_Tunnel_{k}','Route_C')
    collider_box('TunnelCeiling_'+str(k),'C',roof_center,(13,0.6,length),direction,role='roof')
    for t in (0.1,0.5,0.9):
        pos=av.lerp(bv,t)
        for side in (-1,1):
            beam(g,pos+right*side*4.95+normal*0.2,pos+right*side*4.95+normal*5.7,0.18,0.18,'Dark')
        beam(g,pos-right*5+normal*5.7,pos+right*5+normal*5.7,0.2,0.2,'Dark')
    marker('Light_Tunnel_'+str(k),'C',center+normal*4.3,'light_violet',(11,0,0))
    # Posters are textured signs, never paper-thin collision walls.
    for side,t in ((-1,0.25),(1,0.22 if k==0 else 0.7)):
        pos=av.lerp(bv,t)+right*side*4.94+normal*2.7
        sign(g,pos,2.7,7 if side<0 else 11,right*side)

# Portal is a major silhouette; bevel-like layered masonry frames the full riding envelope.
for k,(x,z,h,dx,dz) in enumerate([(42,82,30,5,12),(53,108,22,6,10)]):
    direction=Vector((dx,0,dz)).normalized(); right,_,_=basis(direction); g=geo('C','Portal',(x,h,z))
    for side in (-1,1):
        p=Vector((x,h,z))+right*side*5.9
        box(g,p+UP*2.7,(1.2,6.4,1.5),'Stone',direction)
    box(g,(x,h+6.2,z),(13.2,1.2,1.6),'Stone',direction)
    sign(g,Vector((x,h+6.1,z))-direction*0.83,8.0,3,direction)

# Guilt reveal bay: a recessed room beside the tunnel, with a real grate and a separate trigger.
ax,az=53,87.5; ay=27.2; g=geo('C','RevealAlcove',(ax,ay,az))
direction=Vector((5,-4,12)).normalized(); right,normal,forward=basis(direction)
base=Vector((42,30,82)).lerp(Vector((47,26,94)),0.7)+right*8.0
for nm,offset,size in [('Back',right*2.7+normal*2.1,(0.7,5.5,7.0)),
                       ('North',-forward*3.2+normal*2.1,(5.9,5.5,0.7)),
                       ('South',forward*3.2+normal*2.1,(5.9,5.5,0.7))]:
    box(g,base+offset,size,'Concrete',direction)
    collider_box('Alcove_'+nm,'C',base+offset,size,direction)
roof=Geometry(); box(roof,base+normal*4.8,(6.3,0.45,7.1),'Concrete',direction)
roof.object('PL_C_RemovableRoof_Alcove','Route_C')
collider_box('AlcoveRoof','C',base+normal*4.8,(6.3,0.45,7.1),direction,role='roof')
marker('Event_GuiltReveal_C','C',base+normal*0.2,'guilt_grate_reveal')
box(g,base+right*2.28+normal*2.4,(0.07,0.16,0.32),'Warm',direction)
marker('Light_Alcove','C',base+normal*2.9,'light_warm',(6,0,0))

# Arena C: a former mountain reservoir reclaimed as an afterparty; wide continuous riding ring.
g=geo('C','Cistern',(64,22,132))
for name,center,size in [
    ('West',(42.8,25.1,132),(1.1,7.0,39)),('East',(85.2,25.1,132),(1.1,7.0,39)),
    ('NorthLeft',(47.9,25.1,112.8),(10.2,7.0,1.1)),('NorthRight',(74.0,25.1,112.8),(23,7.0,1.1)),
    ('SouthLeft',(50.8,25.1,151.2),(16,7.0,1.1)),('SouthRight',(77.5,25.1,151.2),(15.5,7.0,1.1))]:
    box(g,center,size,'Concrete'); collider_box('Cistern_'+name,'C',center,size)
    box(g,Vector(center)+UP*3.65,(size[0]+0.12,0.25,size[2]+0.12),'Stone')
for k,(x,z) in enumerate([(52,124),(67,124),(52,141),(67,141)]):
    y=ground(x,z); box(g,(x,y+3.0,z),(1.35,6.4,1.35),'Concrete')
    box(g,(x-0.7,y+2.1,z),(0.055,3.2,0.13),'Violet')
    box(g,(x,y+0.18,z),(1.7,0.45,1.7),'Stone')
    collider_box('CisternColumn_'+str(k),'C',(x,y+3.0,z),(1.7,6.4,1.7))
# Stage height and ramps agree exactly, including foundations and guard-free riding approaches.
box(g,(78.0,22.55,132),(7.0,1.1,13.0),'Dark')
box(g,(78.0,23.16,132),(7.2,0.12,13.2),'Timber')
collider_box('Stage','C',(78,22.58,132),(7.2,1.28,13.2),role='walkable')
ramp('C','StageAccess_N',(78,119.5),(78,125.4),3.4,1.22)
ramp('C','StageAccess_S',(78,144.5),(78,138.6),3.4,1.22)
for side in (-1,1):
    z=132+side*5.2
    for yy in (24.1,25.6):
        box(g,(80.0,yy,z),(1.7,1.45,1.5),'Dark')
        cylinder(g,(79.12,yy,z),(79.08,yy,z),0.52,'Metal',8)
        cylinder(g,(79.07,yy,z),(79.04,yy,z),0.18,'Dark',6)
    collider_box('Speaker_'+str(side+1),'C',(80,24.85,z),(1.8,3,1.5))
box(g,(76.6,23.9,132),(1.15,1.3,4.2),'Rust')
collider_box('DJBooth','C',(76.6,23.9,132),(1.15,1.3,4.2))
sign(g,(84.56,26.1,132),7.5,7,(1,0,0))
for z in (119,132,145):
    box(g,(43.39,26.5,z),(0.06,0.15,8.0),'Violet')
marker('Light_CisternStage','C',(75,26.5,132),'light_violet',(12,0,0))
stage_roof=Geometry()
box(stage_roof,(79.4,29.9,132),(11.6,0.45,18),'Dark')
stage_roof.object('PL_C_RemovableRoof_StageCanopy','Route_C')
collider_box('StageCanopy','C',(79.4,29.9,132),(11.6,0.45,18),role='roof')
for z in (123.5,140.5):
    beam(g,(73.9,29.55,z),(84.5,29.55,z),0.2,0.3,'Metal')
    box(g,(77.8,29.35,z),(7.0,0.08,0.16),'Violet')
for k,z in enumerate((117.5,146.5)):
    # Reservoir plumbing gives the shell a history and a readable silhouette.
    cylinder(g,(83,21.7,z),(83,27.8,z),0.8,'Metal',8)
    cylinder(g,(83,27.8,z),(81,27.8,z),0.8,'Metal',8)
    for y in (23.0,26.5):
        cylinder(g,(83,y-0.13,z),(83,y+0.13,z),0.94,'Rust',8)
    collider_box('ReservoirPipe_'+str(k),'C',(83,24.75,z),(1.7,6.2,1.7))
    cylinder(g,(81,27.8,z),(81,28.8,z),0.16,'Dark',6)
(px,pz),(dx,dz)=lane_point('C',0.72)
length=math.hypot(dx,dz); px,pz=px+dz/length*1.7,pz-dx/length*1.7
marker('GuaranteedPickup_C','C',(px,ground(px,pz)+0.08,pz),'guaranteed_pickup')
for k,t in enumerate((0.55,0.80)):
    (x,z),_=lane_point('C',t)
    marker('StrongerEnemy_C_'+str(k+1),'C',(x,ground(x,z)+0.05,z),'stronger_enemy')

# Shared ending: a framed city reveal and a clearly marked final approach to Maribor.
fence('Shared','OverlookRail',[(-19,195),(-19,204),(-14,207),(14,207),(19,204),(19,195)])
g=geo('Shared','OverlookFurniture',(0,12,200))
sign(g,(0,16.2,204),9.6,4)
beam(g,(-5.3,12,204),(-5.3,16.7,204),0.25,0.25,'Timber')
beam(g,(5.3,12,204),(5.3,16.7,204),0.25,0.25,'Timber')
for side in (-1,1):
    collider_box('ExitSignPost_'+str(side+1),'Shared',(side*5.3,14.3,204),(0.3,4.7,0.3))
lamp('Shared','OverlookWest',-16,201); lamp('Shared','OverlookEast',16,201)
marker('Exit_Forest','Shared',(0,14,203),'level_exit',(36,5,7))

# Collision planning: off-lane containment starts beyond the bank toe, not in the usable riding width.
for name,lane in LANES.items():
    points=lane['pts']
    for index,(a,b) in enumerate(zip(points,points[1:])):
        ax,az,ah=a; bx,bz,bh=b; length=math.hypot(bx-ax,bz-az)
        if length<0.9: continue
        right=Vector(((bz-az)/length,0,-(bx-ax)/length))
        for side in (-1,1):
            center=Vector(((ax+bx)/2,(ah+bh)/2+4,(az+bz)/2))+right*side*(lane['w']/2+3.7)
            ds,_=fields(np.array([center.x]),np.array([center.z]))
            # Do not wall off openings into any pad or another lane.
            own_idx=FEATURES.index(('lane',name)); other=np.delete(ds[:,0],own_idx)
            if min(other)<1.0: continue
            collider_box(f'Bank_{name}_{index}_{side+1}',lane['owner'],center,(1.0,10,length+1.8),
                         (bx-ax,bh-ah,bz-az),role='boundary')

# Outer safety boundary is outside all pads, aligned with visible uphill ridges and the overlook rail.
for name,c,s in [('North',(0,35,-23),(207,100,1)),('West',(-101,35,94),(1,100,235)),
                 ('East',(96,35,94),(1,100,235)),('South',(0,35,208),(207,100,1))]:
    collider_box('Outer_'+name,'Shared',c,s,role='boundary')

# Rockwork is clustered at the foot of banks, leaving every designed lane clear.
for name,lane in LANES.items():
    for k in range(max(4,int(sum(math.dist(a[:2],b[:2]) for a,b in zip(lane['pts'],lane['pts'][1:]))/7))):
        t=(k+0.5)/max(4,int(sum(math.dist(a[:2],b[:2]) for a,b in zip(lane['pts'],lane['pts'][1:]))/7))
        (x,z),(dx,dz)=lane_point(name,t); length=math.hypot(dx,dz)
        side=1 if k%2 else -1; off=lane['w']/2+3.8
        px,pz=x+dz/length*side*off,z-dx/length*side*off
        if feature_at(px,pz)[0]<1.2: continue
        boulder(lane['owner'],px,pz,1.1+RNG.random()*0.65,3000+k+list(LANES).index(name)*100)

# Deliberately spaced silhouettes. No alpha cards, no per-tree physics and no wall of identical cones.
forest_points=[]
for attempt in range(7000):
    x=RNG.uniform(-103,99); z=RNG.uniform(-21,211)
    d,owner,mat=feature_at(x,z)
    if d<5.0 or any(math.hypot(x-px,z-pz)<4.8 for px,pz in forest_points): continue
    if z>190 and abs(x)<30: continue
    scale=RNG.uniform(0.8,1.55)
    pine(owner if d<15 else 'Backdrop',x,z,scale,5000+attempt)
    forest_points.append((x,z))
    if len(forest_points)>=320: break

# Opaque, solid tufts break up the bank toes without filling the riding lane or adding colliders.
for name,lane in LANES.items():
    for k in range(24):
        (x,z),(dx,dz)=lane_point(name,(k+0.4)/24); length=math.hypot(dx,dz)
        side=1 if k%2 else -1; off=lane['w']/2+0.65+RNG.random()*0.9
        x,z=x+dz/length*side*off,z-dx/length*side*off
        if feature_at(x,z)[0]<0.2: continue
        y=ground(x,z); g=geo(lane['owner'],'Forest',(x,y,z))
        for blade in range(3):
            angle=blade*2.1+k; radius=0.18+RNG.random()*0.18; height=0.35+RNG.random()*0.4
            vx,vz=math.cos(angle)*radius,math.sin(angle)*radius
            vertices=[(x-vz,y-0.04,z+vx),(x+vz,y-0.04,z-vx),(x+vx,y+height,z+vz),(x,y+0.08,z)]
            g.add(vertices,[(0,1,2),(0,3,1),(0,2,3),(1,3,2)],'Foliage')

# The horizon is a solid low-poly valley. Buildings have foundations; there are no paper skyline cards.
g=geo('Backdrop','Maribor',(0,-5,300))
vertices=[]; nx,nz=19,12
for iz in range(nz):
    z=238+iz*20
    for ix in range(nx):
        x=-270+ix*30
        rise=max(0,abs(x)-125)*0.17+max(0,z-338)*0.28
        y=-10+rise+(math.sin(x/37+iz*0.6)*5 if rise>0 else 0)
        if 248<z<278 and abs(x)<135:y=-13
        vertices.append((x,y,z))
faces=[]
for iz in range(nz-1):
    for ix in range(nx-1):
        a=iz*nx+ix; faces.append((a,a+1,a+nx+1,a+nx))
bottom=len(vertices)
vertices.extend([(-270,-50,238),(270,-50,238),(270,-50,458),(-270,-50,458)])
faces += [tuple(range(nx))+ (bottom+1,bottom),
          tuple(range((nz-1)*nx,nz*nx))+ (bottom+2,bottom+3),
          tuple(iz*nx for iz in range(nz))+ (bottom+3,bottom),
          tuple(iz*nx+nx-1 for iz in range(nz))+ (bottom+2,bottom+1),
          (bottom,bottom+1,bottom+2,bottom+3)]
g.add(vertices,faces,'Ground')
for index in range(45):
    x=RNG.uniform(-105,105); z=RNG.uniform(280,325); y=-10.35
    w,h,d=RNG.uniform(4,11),RNG.uniform(5,14),RNG.uniform(5,10)
    box(g,(x,y+h/2,z),(w,h,d),'Concrete' if index%3 else 'Stone')
    vertices=[(x-w*.6,y+h,z-d*.6),(x+w*.6,y+h,z-d*.6),(x-w*.6,y+h,z+d*.6),(x+w*.6,y+h,z+d*.6),
              (x-w*.6,y+h+2.5,z),(x+w*.6,y+h+2.5,z)]
    g.add(vertices,[(0,1,5,4),(4,5,3,2),(0,4,2),(1,3,5)],'Rust')
for x,z,h in [(-26,286,28),(10,307,36)]:
    box(g,(x,-10.35+h/2,z),(4.0,h,4.0),'Stone')
    cylinder(g,(x,h-10.35,z),(x,h-2.35,z),3.1,'Rust',4,0.08)
box(g,(0,-11.5,266),(240,0.4,14),'Dark')
for x in (-62,55):
    box(g,(x,-9.6,266),(6,0.6,25),'Concrete')
    for side in (-1,1):
        beam(g,(x+side*2.8,-9.0,254),(x+side*2.8,-9.0,278),0.18,0.35,'Dark')

# Grounded gameplay hooks belong to spaces, not arbitrary mesh-name searches.
for route in ('A','B','C'):
    pad=PADS['Arena'+route]; cx,cz=pad['c']; hx,hz=pad['half']
    marker('Occupancy_'+route,route,(cx,pad['h']+3,cz),'combat_occupancy',(hx*2-1,8,hz*2-1))
    for k,(fx,fz) in enumerate([(-0.45,-0.1),(0.15,-0.5),(0.15,0.55)],1):
        x,z=cx+fx*hx,cz+fz*hz
        marker(f'Charge_{route}_{k}',route,(x,ground(x,z)+0.08,z),'battery_charge')
    for k,(fx,fz) in enumerate([(-0.55,-0.6),(0.5,-0.55),(-0.5,0.6),(0.45,0.55)],1):
        if route=='B' and k==4: fx,fz=0.15,0.7
        x,z=cx+fx*hx,cz+fz*hz
        marker(f'Spawn_{route}_{k}',route,(x,ground(x,z)+0.05,z),'enemy_spawn')
    for k,t in enumerate((0.23,0.58,0.86),1):
        (x,z),_=lane_point(route,t)
        marker(f'Guilt_Anchor_{route}_{k}',route,(x,ground(x,z),z),'guilt_anchor')
marker('Event_GuiltReveal_A','A',(-65,ground(-65,120),120),'guilt_behind_player')

# Realize only spatially coherent batches; route interiors, seals and removable roofs remain separate.
for (owner,category,ix,iz),g in BATCHES.items():
    collection='Route_'+owner if owner in ('A','B','C') else owner
    g.object(f'PL_{owner}_{category}_{ix+20:02}_{iz+20:02}',collection)

for obj in COLLECTIONS['CollisionGuides'].objects:
    obj.hide_render=True
COLLECTIONS['CollisionGuides'].hide_render=True


def validate_geometry():
    errors=[]
    meshes=[o for o in SCENE.objects if o.type=='MESH']
    render=[o for o in meshes if o.name.startswith('PL_')]
    for obj in meshes:
        if any(abs(s-1)>1e-6 for s in obj.scale): errors.append(obj.name+': unapplied scale')
        bm=bmesh.new(); bm.from_mesh(obj.data)
        if any(face.calc_area()<1e-8 for face in bm.faces): errors.append(obj.name+': degenerate face')
        if any(len(edge.link_faces)>2 for edge in bm.edges): errors.append(obj.name+': non-manifold edge')
        bm.free()
    edges=collections.Counter()
    for face in terrain_faces:
        for a,b in zip(face,face[1:]+face[:1]): edges[tuple(sorted((a,b)))]+=1
    interior_holes=0
    for (a,b),count in edges.items():
        if count!=1: continue
        pa,pb=terrain_vertices[a],terrain_vertices[b]
        outer=any(abs(pa[axis]-edge)<1e-5 and abs(pb[axis]-edge)<1e-5
                  for axis,edge in [(0,-X[0]),(0,-X[-1]),(1,-Z[0]),(1,-Z[-1])])
        if not outer: interior_holes+=1
    if interior_holes: errors.append(f'Terrain has {interior_holes} interior holes')
    floor_samples=[]
    for name,lane in LANES.items():
        for a,b in zip(lane['pts'],lane['pts'][1:]):
            length=math.dist(a[:2],b[:2]); dx,dz=b[0]-a[0],b[1]-a[1]
            if length<1e-4: continue
            for t in np.linspace(0,1,max(2,math.ceil(length/2))):
                for side in (-1,0,1):
                    x=a[0]+(b[0]-a[0])*t+dz/length*side*(lane['w']/2-1.2)
                    z=a[1]+(b[1]-a[1])*t-dx/length*side*(lane['w']/2-1.2)
                    want=a[2]+(b[2]-a[2])*t; got=ground(x,z)
                    floor_samples.append(dict(lane=name,position=[x,got,z],design_height=want))
                    if abs(got-want)>0.65: errors.append(f'Lane {name}: floor deviates {got-want:.2f} m at {x:.1f},{z:.1f}')
    # A marker's foot must not spawn inside a physical obstacle. Volume markers are intentionally excluded.
    marker_errors=[]
    for m in MARKERS:
        if m['kind'] not in ('player_start','enemy_spawn','stronger_enemy','battery_charge','guaranteed_pickup'): continue
        p=Vector(m['position'])+UP*0.8
        for c in COLLIDERS:
            if c['kind']!='Box' or c['role'] not in ('obstacle','boundary'): continue
            from mathutils import Quaternion
            x,y,z,w=c['rotation']; q=Quaternion((w,x,y,z))
            local=q.inverted()@(p-Vector(c['center']))
            if all(abs(local[i])<c['size'][i]/2+0.45 for i in range(3)):
                marker_errors.append(m['name']+' overlaps '+c['name'])
    errors.extend(marker_errors)
    materials=sorted({m.name for o in render for m in o.data.materials})
    triangles=sum(len(o.data.polygons) for o in render)
    if len(materials)>15: errors.append('Render material budget exceeded')
    if triangles>100000: errors.append('Whole-map render budget exceeds 100k triangles')
    report=dict(scene=SCENE.name,render_triangles=triangles,render_objects=len(render),materials=materials,
                terrain_triangles=len(terrain_faces),terrain_chunks=len(TERRAIN_OBJECTS),interior_holes=interior_holes,
                collision_shapes=dict(collections.Counter(c['kind'] for c in COLLIDERS)),
                collision_roles=dict(collections.Counter(c['role'] for c in COLLIDERS)),markers=len(MARKERS),
                scenic_trees=len(forest_points),floor_samples=len(floor_samples),errors=errors)
    (HERE/'validation.json').write_text(json.dumps(report,indent=2)+'\n')
    if errors: raise RuntimeError('POHORJE_VALIDATION_FAILED\n'+'\n'.join(errors[:30]))
    return report,floor_samples


REPORT,FLOOR_SAMPLES=validate_geometry()

EXPORTS=[('Pohorje_Shared.fbx','Shared'),('Pohorje_Route_A.fbx','A'),('Pohorje_Route_B.fbx','B'),
         ('Pohorje_Route_C.fbx','C'),('Pohorje_Backdrop.fbx','Backdrop'),('Pohorje_CollisionGuides.fbx','CollisionGuides'),
         ('Pohorje_Markers.fbx','Markers')]


def export_fbx(filename,owner):
    collection=COLLECTIONS['Route_'+owner if owner in ('A','B','C') else owner]
    bpy.ops.object.select_all(action='DESELECT')
    for obj in collection.objects:
        obj.hide_set(False); obj.select_set(True)
    path=HERE/'Export'/filename
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'EMPTY'} if owner=='Markers' else {'MESH'},
        axis_forward='-Z',axis_up='Y',bake_space_transform=True,apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',
        mesh_smooth_type='FACE',use_mesh_modifiers=True,use_triangles=True,bake_anim=False,add_leaf_bones=False,path_mode='STRIP')
    shutil.copy2(path,ART/filename)


def write_contract():
    mats=[]
    for name in MATERIALS:
        mats.append(dict(name='Pohorje_'+name,texture='Textures/Pohorje_'+name+'.png' if name in PALETTE or name=='Signs' else '',
                         emission=[1.0,0.43,0.13] if name=='Warm' else ([0.54,0.15,1.0] if name=='Violet' else []),
                         clamp=name=='Signs'))
    # Data drives import and verification; there is no dimension/axis inference in Unity.
    manifest=dict(version=3,name='Pohorje_Production',coordinates='Unity metres: X right, Y up, Z downhill',
        exports=[dict(file=file,owner=owner) for file,owner in EXPORTS],materials=mats,terrain=TERRAIN_OBJECTS,
        colliders=COLLIDERS,markers=MARKERS,
        lanes=[dict(name=n,owner=l['owner'],width=l['w'],points=[dict(x=x,y=h,z=z) for x,z,h in l['pts']]) for n,l in LANES.items()],
        pads=[dict(name=n,cx=p['c'][0],cz=p['c'][1],hx=p['half'][0],hz=p['half'][1],radius=p['r'],height=p['h']) for n,p in PADS.items()],
        floorSamples=[dict(x=s['position'][0],y=s['position'][1],z=s['position'][2]) for s in FLOOR_SAMPLES],
        reviewCameras=[dict(name=n,eye=e,target=t) for n,e,t in SHOTS],expected=REPORT)
    (HERE/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
    (ART/'Pohorje_Manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
    (HERE/'markers.json').write_text(json.dumps(dict(coordinates='Blender XYZ metres',markers=[dict(name=m['name'],owner=m['owner'],kind=m['kind'],position=m['blender_position']) for m in MARKERS]),indent=2)+'\n')
    (ART/'Textures').mkdir(exist_ok=True)
    for png in (HERE/'Textures').glob('*.png'): shutil.copy2(png,ART/'Textures'/png.name)


def presentation():
    world=bpy.data.worlds.new('Pohorje_MorningFog'); SCENE.world=world; world.use_nodes=True
    world.node_tree.nodes['Background'].inputs[0].default_value=(0.18,0.24,0.29,1)
    world.node_tree.nodes['Background'].inputs[1].default_value=0.45
    world.mist_settings.start=25; world.mist_settings.depth=230; world.mist_settings.falloff='QUADRATIC'
    sun_data=bpy.data.lights.new('Preview_Sun','SUN'); sun_data.energy=2.2; sun_data.angle=math.radians(12)
    sun_data.color=(0.77,0.85,1.0)
    sun=bpy.data.objects.new('Preview_Sun',sun_data); COLLECTIONS['Presentation'].objects.link(sun)
    sun.rotation_euler=(math.radians(32),math.radians(-24),math.radians(-38))
    for name,pos,power,color,size in [('SkyFill',(-30,110,90),170000,(0.48,0.64,0.73),100),
                                       ('WarmBounce',(60,80,-30),90000,(1.0,0.67,0.36),80)]:
        data=bpy.data.lights.new('Preview_'+name,'AREA'); data.energy=power; data.shape='DISK'; data.size=size; data.color=color
        light=bpy.data.objects.new('Preview_'+name,data); COLLECTIONS['Presentation'].objects.link(light)
        light.location=P(pos); light.rotation_euler=(P((0,20,100))-light.location).to_track_quat('-Z','Y').to_euler()
    # Only presentation lights; marker-driven Unity lights are independent of exported geometry.
    for m in MARKERS:
        if not m['kind'].startswith('light_') or not any(word in m['name'] for word in ('Tunnel','Cistern','Alcove')): continue
        data=bpy.data.lights.new('Preview_'+m['name'],'POINT'); data.energy=500; data.shadow_soft_size=2
        data.color=(0.6,0.18,1.0) if m['kind']=='light_violet' else (1.0,0.5,0.2)
        light=bpy.data.objects.new('Preview_'+m['name'],data); COLLECTIONS['Presentation'].objects.link(light); light.location=P(m['position'])
    camera_data=bpy.data.cameras.new('Pohorje_ReviewCamera'); camera_data.lens=26; camera_data.clip_end=800
    camera=bpy.data.objects.new('Pohorje_ReviewCamera',camera_data); COLLECTIONS['Presentation'].objects.link(camera); SCENE.camera=camera
    SCENE.render.engine='CYCLES'; SCENE.cycles.samples=24; SCENE.cycles.use_denoising=True
    SCENE.render.resolution_x=1280; SCENE.render.resolution_y=720; SCENE.render.resolution_percentage=100
    SCENE.view_settings.view_transform='Standard'; SCENE.view_settings.look='Medium High Contrast' if 'Medium High Contrast' in [i.name for i in SCENE.view_settings.bl_rna.properties['look'].enum_items] else 'None'
    SCENE.view_settings.exposure=0; SCENE.view_settings.gamma=1
    SCENE.render.image_settings.file_format='PNG'; SCENE.render.film_transparent=False
    return camera


SHOTS=[
    ('01-bellevue',(-2,43.2,-11),(4,41.5,3)),
    ('02-fork',(0,43,24),(0,36.5,73)),
    ('03-forest',(-45.8,33.6,85),(-50,25.8,101)),
    ('04-gondola',(-8,39,70),(5,23,115)),
    ('05-tunnel',(37,35,71),(46,29,91)),
    ('06-logging-clearing',(-61,25.5,114),(-67,21,136)),
    ('07-terminal',(-15,24.5,116),(12,22.5,141)),
    ('08-cistern',(50,28.5,118),(74,24,135)),
    ('09-overlook',(-11,17,192),(0,8,299)),
    ('10-overview',(-155,155,-57),(0,20,114)),
    ('11-tunnel-interior',(44,31,86),(52,28,88)),
]


def set_camera(camera,eye,target):
    camera.location=P(eye); camera.rotation_euler=(P(target)-camera.location).to_track_quat('-Z','Y').to_euler()


args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
for filename,owner in EXPORTS: export_fbx(filename,owner)
write_contract()
camera=presentation(); set_camera(camera,SHOTS[9][1],SHOTS[9][2])
for obj in COLLECTIONS['CollisionGuides'].objects: obj.hide_set(True)
for image in bpy.data.images:
    if image.filepath and image.name.startswith('T_Pohorje_'):
        image.filepath='//../Textures/'+Path(image.filepath).name
for area in bpy.context.screen.areas if bpy.context.screen else []:
    if area.type=='VIEW_3D':
        area.spaces.active.region_3d.view_perspective='CAMERA'
        area.spaces.active.shading.type='MATERIAL'
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'Source/Pohorje_Production.blend'))
if '--skip-previews' not in args:
    for obj in SCENE.objects:
        if obj.name.startswith('PL_Seal_'): obj.hide_render=True
    for name,eye,target in SHOTS:
        set_camera(camera,eye,target)
        SCENE.render.filepath=str(HERE/'Previews'/('blender-'+name+'.png'))
        bpy.ops.render.render(write_still=True)
print('POHORJE_PRODUCTION_DONE',json.dumps(REPORT))


