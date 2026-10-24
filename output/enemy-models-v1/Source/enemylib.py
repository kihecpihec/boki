"""Shared helpers for the enemy builders. Import from a Blender --background script.

Conventions match output/player-model-v1: meters, Blender -Y is forward, +Z is up,
flat-shaded faces, one nearest-filtered tile atlas per variant, at most two bone
influences per vertex. Characters use anatomical sides: *.L is +X, *.R is -X.
"""
import bpy, math, json
from pathlib import Path
import numpy as np
from mathutils import Vector, Matrix

OUT = Path(__file__).resolve().parents[1]
CROSS = [(-.7, -1), (.7, -1), (1, -.55), (1, .55), (.7, 1), (-.7, 1), (-1, .55), (-1, -.55)]
V = Vector


def hexrgb(h):
    return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))


# ----------------------------------------------------------------------------- painting
def vnoise(rng, h, w, cells):
    g = rng.random((cells + 2, cells + 2)).astype(np.float32)
    ys = np.linspace(0, cells, h, endpoint=False); xs = np.linspace(0, cells, w, endpoint=False)
    yi, xi = ys.astype(int), xs.astype(int)
    fy, fx = ys - yi, xs - xi
    fy, fx = fy * fy * (3 - 2 * fy), fx * fx * (3 - 2 * fx)
    a, b = g[np.ix_(yi, xi)], g[np.ix_(yi, xi + 1)]
    c, d = g[np.ix_(yi + 1, xi)], g[np.ix_(yi + 1, xi + 1)]
    top, bot = a + (b - a) * fx[None, :], c + (d - c) * fx[None, :]
    return top + (bot - top) * fy[:, None]


def fbm(rng, h, w, base=3, octaves=3):
    out, amp, tot = np.zeros((h, w), np.float32), 1.0, 0.0
    for o in range(octaves):
        out += vnoise(rng, h, w, base * 2 ** o) * amp
        tot += amp
        amp *= .5
    return out / tot


def p_base(a, rng, stops, mott=.07, fine=.015, cells=3):
    """Vertical gradient (stops = [(pos 0=bottom..1=top, hex)]) with soft mottling and fine grain."""
    h, w, _ = a.shape
    t = (np.arange(h) + .5) / h
    pos = [p for p, _ in stops]
    col = np.stack([np.interp(t, pos, [hexrgb(c)[i] for _, c in stops]) for i in range(3)], -1)[:, None, :]
    n = (fbm(rng, h, w, cells, 3) - .5) * 2 * mott + rng.uniform(-fine, fine, (h, w))
    a[...] = np.clip(col + n[:, :, None], 0, 1)


def p_stain(a, rng, hexc, cx, cy, rx, ry, alpha=.8, rough=.4):
    h, w, _ = a.shape
    y, x = ((np.arange(h) + .5) / h)[:, None], ((np.arange(w) + .5) / w)[None, :]
    d = ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 + (fbm(rng, h, w, 4, 3) - .5) * rough * 2
    m = (np.clip(1 - d, 0, 1) ** .55 * alpha)[:, :, None]
    a[...] = a * (1 - m) + np.array(hexrgb(hexc)) * m


def p_drips(a, rng, hexc, n, top, alpha=.6, wpx=2, lo=.08, hi=.35):
    h, w, _ = a.shape
    col = np.array(hexrgb(hexc))
    for _ in range(n):
        x = int(rng.integers(0, w - wpx)); y0 = int(top * h); L = int(rng.uniform(lo, hi) * h)
        for i in range(L):
            y = y0 - i
            if 0 <= y < h:
                f = alpha * (1 - i / max(L, 1) * .6)
                a[y, x:x + wpx] = a[y, x:x + wpx] * (1 - f) + col * f


def p_folds(a, rng, hexc, n, alpha=.22, wpx=3, bend=.04):
    """Vertical cloth folds: soft dark creases with a little sideways wander."""
    h, w, _ = a.shape
    col = np.array(hexrgb(hexc))
    for _ in range(n):
        x0 = rng.uniform(0, w); ph = rng.uniform(0, 6.28); am = rng.uniform(.4, 1) * bend * w
        for y in range(h):
            x = int(x0 + math.sin(y / h * 5 + ph) * am) % w
            for dx in range(wpx):
                f = alpha * (1 - abs(dx - wpx // 2) / (wpx / 2 + .5))
                a[y, (x + dx) % w] = a[y, (x + dx) % w] * (1 - f) + col * f


def p_rect(a, x0, y0, x1, y1, hexc, alpha=1.0):
    h, w, _ = a.shape
    sl = (slice(int(y0 * h), int(y1 * h)), slice(int(x0 * w), int(x1 * w)))
    a[sl] = a[sl] * (1 - alpha) + np.array(hexrgb(hexc)) * alpha


def p_line(a, x0, y0, x1, y1, hexc, th=1, alpha=1.0):
    h, w, _ = a.shape
    col = np.array(hexrgb(hexc))
    for t in np.linspace(0, 1, int(max(h, w) * 1.2)):
        x, y = int((x0 + (x1 - x0) * t) * w), int((y0 + (y1 - y0) * t) * h)
        for dy in range(th):
            for dx in range(th):
                if 0 <= y + dy < h and 0 <= x + dx < w:
                    a[y + dy, x + dx] = a[y + dy, x + dx] * (1 - alpha) + col * alpha


def p_ellipse(a, cx, cy, rx, ry, hexc, alpha=1.0):
    h, w, _ = a.shape
    y, x = ((np.arange(h) + .5) / h)[:, None], ((np.arange(w) + .5) / w)[None, :]
    m = ((((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2) <= 1)[:, :, None] * alpha
    a[...] = a * (1 - m) + np.array(hexrgb(hexc)) * m


def p_speckle(a, rng, hexc, n, alpha=.5):
    h, w, _ = a.shape
    col = np.array(hexrgb(hexc))
    for _ in range(n):
        y, x = int(rng.integers(0, h)), int(rng.integers(0, w))
        a[y, x] = a[y, x] * (1 - alpha) + col * alpha


def pixelate(a, block=2):
    h, w, c = a.shape
    a[...] = np.kron(a.reshape(h // block, block, w // block, block, c).mean((1, 3)), np.ones((block, block, 1)))


def torso_uv(front, back, side, xh, z0, z1):
    """uvfn for 8-sided torso tubes: planar front/back projection, side tile on the flanks."""
    def f(j, k, vs):
        v = lambda z: (z - z0) / (z1 - z0)
        if k in (0, 1, 7): return front, [(.5 + p.x / (2 * xh), v(p.z)) for p in vs]
        if k in (3, 4, 5): return back, [(.5 - p.x / (2 * xh), v(p.z)) for p in vs]
        return side, [(.5 + p.y / .3, v(p.z)) for p in vs]
    return f


def head_uv(face, skin, chin_z, top_z, half_w, min_face_z):
    """uvfn for head tubes: painted face on forward-facing polygons, skin tile elsewhere."""
    def f(j, k, vs):
        n = (vs[1] - vs[0]).cross(vs[3] - vs[0])
        if n.y < -.3 * n.length and vs[0].z > min_face_z:
            return face, [((p.x / (2 * half_w) + .5), (p.z - chin_z) / (top_z - chin_z)) for p in vs]
        return skin, [(k / 8, 1 - j / 6), ((k + 1) / 8, 1 - j / 6), ((k + 1) / 8, 1 - (j + 1) / 6), (k / 8, 1 - (j + 1) / 6)]
    return f


def arm_chain(J, suf):
    """Polyline clavicle→shoulder→elbow→wrist so the arm tube grows out of the torso (no boxy shoulder)."""
    return ([J['clav' + suf] + V((0, 0, -.01)), J['shoulder' + suf], J['elbow' + suf], J['wrist' + suf]],
            ['clavicle.' + suf, 'upper_arm.' + suf, 'lower_arm.' + suf])


def leg_chain(J, suf, rise):
    """Polyline pelvis→hip→knee→ankle so the thigh merges into the pelvis (no shelf at the hips)."""
    return ([J['hip' + suf] + V((0, 0, rise)), J['hip' + suf], J['knee' + suf], J['ankle' + suf]],
            ['hips', 'thigh.' + suf, 'shin.' + suf])


def shoe(geo, x, length, width, heel_h, toe_h, back, tile, sole_tile, bone, sole=.028):
    geo.foot(x, 0, length, width, heel_h, toe_h, back, tile, {bone: 1})
    geo.foot(x, 0, length + .02, width + .012, sole, sole * .8, back + .005, sole_tile, {bone: 1})


# ----------------------------------------------------------------------------- scene
class Ctx:
    pass


def begin(title, world=(.10, .12, .17)):
    if not bpy.app.background:
        raise RuntimeError('Run this builder in a separate Blender --background process.')
    for name in ('Source', 'Export', 'Textures', 'Previews'):
        (OUT / name).mkdir(exist_ok=True)
    c = Ctx()
    c.scene = bpy.data.scenes.new(title)
    bpy.context.window.scene = c.scene
    s = c.scene
    s.unit_settings.system = 'METRIC'
    s.unit_settings.scale_length = 1.0
    s.render.engine = 'CYCLES'
    s.cycles.samples = 20
    s.render.resolution_x = 900
    s.render.resolution_y = 1100
    s.world = bpy.data.worlds.new(title + '_World')
    s.world.use_nodes = True
    next(n for n in s.world.node_tree.nodes if n.type == 'BACKGROUND').inputs[0].default_value = (*world, 1)
    s.view_settings.view_transform = 'Standard'
    c.export = bpy.data.collections.new('EXPORT')
    s.collection.children.link(c.export)
    c.studio = bpy.data.collections.new('STUDIO_NO_EXPORT')
    s.collection.children.link(c.studio)
    return c


# ----------------------------------------------------------------------------- atlas
class Atlas:
    """Grid of flat painted tiles. Row 0 is the bottom of the image (UV v = 0)."""

    def __init__(self, name, size=256, grid=4, seed=1):
        self.name, self.size, self.grid, self.tile = name, size, grid, size // grid
        self.px = np.zeros((size, size, 4), np.float32)
        self.px[..., 3] = 1
        self.rng = np.random.default_rng(seed)

    def _o(self, tile):
        return (tile % self.grid) * self.tile, (tile // self.grid) * self.tile

    def fill(self, tile, hexc, var=.04, block=4):
        x0, y0 = self._o(tile)
        n = self.tile // block
        base = np.array(hexrgb(hexc), np.float32)
        blocks = np.clip(base + self.rng.uniform(-var, var, (n, n, 1)), 0, 1)
        self.px[y0:y0 + self.tile, x0:x0 + self.tile, :3] = np.kron(blocks, np.ones((block, block, 1)))

    def rect(self, tile, x0, y0, x1, y1, hexc):
        ox, oy = self._o(tile)
        T = self.tile
        self.px[oy + int(y0 * T):oy + int(y1 * T), ox + int(x0 * T):ox + int(x1 * T), :3] = hexrgb(hexc)

    def view(self, tile):
        ox, oy = self._o(tile)
        return self.px[oy:oy + self.tile, ox:ox + self.tile, :3]

    def paint(self, tile, fn):
        """fn(u, v) -> hex string or None, sampled at each pixel centre of the tile (v up)."""
        ox, oy = self._o(tile)
        T = self.tile
        for y in range(T):
            for x in range(T):
                col = fn((x + .5) / T, (y + .5) / T)
                if col: self.px[oy + y, ox + x, :3] = hexrgb(col)

    def save(self, filename):
        img = bpy.data.images.new(self.name, width=self.size, height=self.size)
        img.pixels.foreach_set(self.px.ravel())
        img.filepath_raw = str(OUT / 'Textures' / filename)
        img.file_format = 'PNG'
        img.save()
        img.pack()
        return img


def material(name, image=None, rough=.9, color=None, emission=None, emit_image=False):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nodes = m.node_tree.nodes
    sh = next(n for n in nodes if n.type == 'BSDF_PRINCIPLED')
    sh.inputs['Roughness'].default_value = rough
    if image:
        tex = nodes.new('ShaderNodeTexImage')
        tex.image = image
        tex.interpolation = 'Closest'
        m.node_tree.links.new(tex.outputs['Color'], sh.inputs['Base Color'])
    if image and emit_image:
        m.node_tree.links.new(tex.outputs['Color'], sh.inputs['Emission Color'])
        sh.inputs['Emission Strength'].default_value = 3.0
    if color:
        sh.inputs['Base Color'].default_value = (*color, 1)
        m.diffuse_color = (*color, 1)
    if emission:
        sh.inputs['Emission Color'].default_value = (*emission, 1)
        sh.inputs['Emission Strength'].default_value = 4.0
    return m


# ----------------------------------------------------------------------------- geometry
def orient(verts, faces):
    """Flip faces of a convex solid so every normal points away from its centroid."""
    c = sum(verts, V()) / len(verts)
    out = []
    for f in faces:
        p = [verts[i] for i in f]
        n = (p[1] - p[0]).cross(p[2] - p[0])
        out.append(tuple(f) if n.dot(sum(p, V()) / len(p) - c) >= 0 else tuple(reversed(f)))
    return out


def face_toward(verts, face, hint):
    """Wind a (possibly non-convex) face so its normal points along hint."""
    p = [V(verts[i]) for i in face]
    n = (p[1] - p[0]).cross(p[2] - p[0])
    return tuple(face) if n.dot(V(hint)) >= 0 else tuple(reversed(face))


def mix(a, b, t):
    t = min(max(t, 0.0), 1.0)
    d = {}
    if t < 1: d[a] = 1 - t
    if t > 0: d[b] = t
    return d


def z_weights(z, stations):
    """Linear blend between (z, bone) stations sorted ascending by z."""
    if z <= stations[0][0]: return {stations[0][1]: 1}
    if z >= stations[-1][0]: return {stations[-1][1]: 1}
    for (z0, b0), (z1, b1) in zip(stations, stations[1:]):
        if z0 <= z <= z1:
            return mix(b0, b1, (z - z0) / (z1 - z0))


def chain_weights(s, bones, blend=.22):
    n = len(bones)
    j = round(s)
    if 0 < j < n and abs(s - j) < blend:
        return mix(bones[j - 1], bones[j], (s - j + blend) / (2 * blend))
    return {bones[min(max(int(s), 0), n - 1)]: 1}


class Geo:
    """Accumulates one skinned mesh: vertices, per-vertex weights, tiled UV faces."""

    def __init__(self, atlas, world_uv=None):
        self.a = atlas
        self.world_uv = world_uv  # meters per tile repeat; planar-projects UVs for even texel density
        self.v, self.w, self.f, self.t, self.uv = [], [], [], [], []

    def _uv(self, tile, u, v):
        a = self.a
        ox, oy = a._o(tile)
        return ((ox + 1 + u * (a.tile - 2)) / a.size, (oy + 1 + v * (a.tile - 2)) / a.size)

    def add(self, verts, wts, faces, tile=0, uvs=None):
        base = len(self.v)
        self.v += [V(p) for p in verts]
        self.w += wts if isinstance(wts, list) else [wts] * len(verts)
        for i, f in enumerate(faces):
            self.f.append(tuple(base + k for k in f))
            tl = tile[i] if isinstance(tile, (list, tuple)) else tile
            self.t.append(tl)
            if uvs is None and self.world_uv:
                p = [self.v[base + k] for k in f]
                n = (p[1] - p[0]).cross(p[2] - p[0])
                ax = max(range(3), key=lambda a: abs(n[a]))
                cu, cv = {0: (1, 2), 1: (0, 2), 2: (0, 1)}[ax]
                raw = [(q[cu] / self.world_uv, q[cv] / self.world_uv) for q in p]
                mu = math.floor(sum(r[0] for r in raw) / len(raw))
                mv = math.floor(sum(r[1] for r in raw) / len(raw))
                coords = [(min(.97, max(.03, r[0] - mu)), min(.97, max(.03, r[1] - mv))) for r in raw]
            elif uvs:
                coords = uvs[i]
            elif len(f) == 4:
                coords = [(.06, .06), (.94, .06), (.94, .94), (.06, .94)]
            else:
                coords = [(.5 + .45 * math.cos(2 * math.pi * k / len(f)), .5 + .45 * math.sin(2 * math.pi * k / len(f))) for k in range(len(f))]
            self.uv.append([self._uv(tl, *c) for c in coords])

    def prism(self, a, b, sa, sb, tile, w, ref=(0, 1, 0)):
        a, b = V(a), V(b)
        t = (b - a).normalized()
        r = V(ref)
        if abs(t.dot(r)) > .95: r = V((1, 0, 0))
        u = r.cross(t).normalized()
        v = t.cross(u)
        verts = []
        for c, s in ((a, sa), (b, sb)):
            for du, dv in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                verts.append(c + u * (du * s[0] / 2) + v * (dv * s[1] / 2))
        faces = [(0, 1, 2, 3), (4, 5, 6, 7)] + [(k, (k + 1) % 4, 4 + (k + 1) % 4, 4 + k) for k in range(4)]
        wts = [w[0]] * 4 + [w[1]] * 4 if isinstance(w, tuple) else w
        self.add(verts, wts, orient(verts, faces), tile)

    def box(self, center, size, tile, w):
        x, y, z = center
        self.prism((x, y, z - size[2] / 2), (x, y, z + size[2] / 2), size[:2], size[:2], tile, w)

    def foot(self, x, ankle_y, length, width, heel_h, toe_h, back, tile, w):
        """Shoe wedge: sole at z=0, toe narrower and lower than the heel."""
        hw, tw = width / 2, width * .36
        yh, yt = ankle_y + back, ankle_y - length + back
        verts = [(x - hw, yh, 0), (x + hw, yh, 0), (x + tw, yt, 0), (x - tw, yt, 0),
                 (x - hw, yh, heel_h), (x + hw, yh, heel_h), (x + tw, yt, toe_h), (x - tw, yt, toe_h)]
        vv = [V(p) for p in verts]
        faces = [(0, 1, 2, 3), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
        self.add(vv, w, orient(vv, faces), tile)

    def tube(self, pts, radii, wts, tile, uvfn=None, skip=(), cap=(True, True), ref=(0, 1, 0)):
        n = 8
        pts = [V(p) for p in pts]
        verts, wv = [], []
        for i, c in enumerate(pts):
            t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
            r = V(ref)
            if abs(t.dot(r)) > .95: r = V((1, 0, 0))
            u = r.cross(t).normalized()
            v = t.cross(u)
            rx, ry = radii[i]
            for a, b in CROSS:
                verts.append(c + u * (a * rx) + v * (b * ry))
                wv.append(wts[i] if isinstance(wts, list) else wts)
        m = len(pts)
        cum = [0.0]
        for i in range(1, m): cum.append(cum[-1] + (pts[i] - pts[i - 1]).length)
        total = max(cum[-1], 1e-6)
        faces, tiles, uvs = [], [], []
        if cap[0]:
            faces.append(tuple(reversed(range(n)))); tiles.append(tile); uvs.append([(.5, .5)] * n)
        for j in range(m - 1):
            for k in range(n):
                if k in skip: continue
                ids = (j * n + k, j * n + (k + 1) % n, (j + 1) * n + (k + 1) % n, (j + 1) * n + k)
                faces.append(ids)
                if uvfn:
                    tl, co = uvfn(j, k, [verts[i] for i in ids])
                else:
                    tl = tile
                    v0, v1 = 1 - cum[j] / total, 1 - cum[j + 1] / total  # tile top = start of the path
                    co = [(k / n, v0), ((k + 1) / n, v0), ((k + 1) / n, v1), (k / n, v1)]
                tiles.append(tl); uvs.append([(min(.97, max(.03, a)), min(.97, max(.03, b))) for a, b in co])
        if cap[1]:
            faces.append(tuple(range((m - 1) * n, m * n))); tiles.append(tile); uvs.append([(.5, .5)] * n)
        self.add(verts, wv, faces, tiles, None if (self.world_uv and not uvfn) else uvs)

    def build(self, name, mat, coll, jitter=0.0, seed=7):
        if jitter:
            rnd = np.random.default_rng(seed)
            self.v = [p + V(rnd.uniform(-jitter, jitter, 3)) for p in self.v]
        me = bpy.data.meshes.new(name)
        me.from_pydata([tuple(p) for p in self.v], [], self.f)
        me.update()
        ob = bpy.data.objects.new(name, me)
        coll.objects.link(ob)
        me.materials.append(mat)
        layer = me.uv_layers.new(name='Atlas')
        for poly in me.polygons:
            poly.use_smooth = False
            for k, li in enumerate(poly.loop_indices):
                layer.data[li].uv = self.uv[poly.index][k]
        groups = {}
        for i, wd in enumerate(self.w):
            if not wd: continue
            for bone, wt in wd.items():
                g = groups.get(bone) or groups.setdefault(bone, ob.vertex_groups.new(name=bone))
                g.add([i], wt, 'REPLACE')
        return ob


# ----------------------------------------------------------------------------- humanoid
def joints(p):
    """Joint positions for the shared humanoid skeleton from a handful of proportions.

    Keys: H, hip_z, neck_z, chin_z, sh_x, shoulder_drop, hip_x, ankle_z, upper, lower, hand,
    arm_deg, elbow_fwd, foot_len, stoop, knee_fwd. Forward is -Y; L is +X.
    """
    H, hz, nz = p['H'], p['hip_z'], p['neck_z']

    def yo(z):
        return -p['stoop'] * max(0.0, (z - hz) / (H - hz)) ** 1.6

    z1, z2 = hz + .36 * (nz - hz), hz + .70 * (nz - hz)
    J = {'yo': yo, 'root': V((0, 0, 0)), 'hips': V((0, yo(hz), hz)), 'spine': V((0, yo(z1), z1)),
         'chest': V((0, yo(z2), z2)), 'neck': V((0, yo(nz), nz)), 'head': V((0, yo(p['chin_z']), p['chin_z'])),
         'top': V((0, yo(H), H)), 'z1': z1, 'z2': z2}
    a = math.radians(p['arm_deg'])
    for s, suf in ((1, 'L'), (-1, 'R')):
        sz = nz - p['shoulder_drop']
        J['clav' + suf] = V((s * .035, yo(sz), sz + .01))
        J['shoulder' + suf] = V((s * p['sh_x'], yo(sz), sz))
        d1 = V((s * math.sin(a), -p['elbow_fwd'] * .6, -math.cos(a))).normalized()
        J['elbow' + suf] = J['shoulder' + suf] + d1 * p['upper']
        d2 = V((s * math.sin(a * .8), -p['elbow_fwd'], -math.cos(a * .8))).normalized()
        J['wrist' + suf] = J['elbow' + suf] + d2 * p['lower']
        d3 = V((s * math.sin(a * .6), -p['elbow_fwd'] * .8, -math.cos(a * .6))).normalized()
        J['handend' + suf] = J['wrist' + suf] + d3 * p['hand']
        J['hip' + suf] = V((s * p['hip_x'], yo(hz), hz))
        kz = p['ankle_z'] + (hz - p['ankle_z']) * .5
        J['knee' + suf] = V((s * p['hip_x'] * 1.03, -p['knee_fwd'], kz))
        J['ankle' + suf] = V((s * p['hip_x'], 0, p['ankle_z']))
        J['ball' + suf] = V((s * p['hip_x'], -p['foot_len'] * .62, p['ankle_z'] * .55))
        J['toe' + suf] = V((s * p['hip_x'], -p['foot_len'] * .90, p['ankle_z'] * .40))
    return J


def humanoid_bones(J):
    b = [('root', J['root'], V((0, 0, .15)), None), ('hips', J['hips'], J['spine'], 'root'),
         ('spine', J['spine'], J['chest'], 'hips'), ('chest', J['chest'], J['neck'], 'spine'),
         ('neck', J['neck'], J['head'], 'chest'), ('head', J['head'], J['top'], 'neck')]
    for suf in 'LR':
        b += [('clavicle.' + suf, J['clav' + suf], J['shoulder' + suf], 'chest'),
              ('upper_arm.' + suf, J['shoulder' + suf], J['elbow' + suf], 'clavicle.' + suf),
              ('lower_arm.' + suf, J['elbow' + suf], J['wrist' + suf], 'upper_arm.' + suf),
              ('hand.' + suf, J['wrist' + suf], J['handend' + suf], 'lower_arm.' + suf),
              ('thigh.' + suf, J['hip' + suf], J['knee' + suf], 'hips'),
              ('shin.' + suf, J['knee' + suf], J['ankle' + suf], 'thigh.' + suf),
              ('foot.' + suf, J['ankle' + suf], J['ball' + suf], 'shin.' + suf),
              ('toe.' + suf, J['ball' + suf], J['toe' + suf], 'foot.' + suf)]
    return b


def make_rig(c, name, bones):
    data = bpy.data.armatures.new(name)
    rig = bpy.data.objects.new(name, data)
    c.export.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    made = {}
    for n, h, t, par in bones:
        e = data.edit_bones.new(n)
        e.head, e.tail = h, t
        if par: e.parent = made[par]
        made[n] = e
    bpy.ops.object.mode_set(mode='OBJECT')
    rig.show_in_front = True
    return rig


def torso_rings(geo, J, rings, tile, name_bones=None, uvfn=None, skip=(), cap=(True, True)):
    """rings: (z, rx, ry); centered on the leaning spine, skinned hips→spine→chest."""
    st = [(J['hips'].z + .02, 'hips'), (J['z1'], 'spine'), (J['z2'], 'chest')]
    pts = [V((0, J['yo'](z), z)) for z, _, _ in rings]
    geo.tube(pts, [(rx, ry) for _, rx, ry in rings], [z_weights(z, st) for z, _, _ in rings], tile, uvfn, skip, cap)


def limb_rings(geo, pts, rings, bones, tile, blend=.22, uvfn=None, cap=(True, True), root_bone=None):
    """rings: (s, rx, ry) where s counts joints along the polyline pts (0 = first joint)."""
    P = [V(q) for q in pts]

    def pos(s):
        i = min(int(s), len(P) - 2)
        return P[i].lerp(P[i + 1], s - i)

    centers, radii, wts = [], [], []
    for s, rx, ry in rings:
        centers.append(pos(s))
        radii.append((rx, ry))
        w = chain_weights(s, bones, blend)
        if root_bone and s < .2:
            w = mix(root_bone, bones[0], .5 + s * 2.5)
        wts.append(w)
    geo.tube(centers, radii, wts, tile, uvfn, (), cap)


def hand(geo, J, suf, tile, palm=(.08, .035), finger_len=.55, fingers=4, fw=.017, thumb=True, claw=0.0, curl=0.0, spread=0.0):
    """Palm block, two-segment fingers (optional curl toward -Y) and a thumb, rigid to hand.<suf>."""
    w = {'hand.' + suf: 1}
    a, b = J['wrist' + suf], J['handend' + suf]
    d = b - a
    L = d.length
    n = d.normalized()
    s = 1 if suf == 'L' else -1
    p1 = a + n * L * (1 - finger_len)
    geo.prism(a, p1, (palm[0] * .85, palm[1]), palm, tile, w)
    side = V((1, 0, 0))
    c, sn = math.cos(math.radians(curl)), math.sin(math.radians(curl))
    n2 = (n * c + V((0, -1, 0)) * sn).normalized()
    for i in range(fingers):
        off = (i - (fingers - 1) / 2) * (palm[0] / fingers) * .95
        base = p1 + side * off
        mid = base + n * L * finger_len * .55 + side * (off * spread)
        tip = mid + n2 * L * finger_len * .45 + V((0, -claw, 0))
        geo.prism(base, mid, (fw * 1.1, fw * 1.15), (fw, fw), tile, w)
        geo.prism(mid, tip, (fw, fw), (fw * .6, fw * .6), tile, w)
    if thumb:
        o = s * palm[0] * .55
        t0 = a + n * L * .12 + side * o
        geo.prism(t0, a + n * L * .42 + side * (o * 1.5) + V((0, -.025, 0)), (fw * 1.4, fw * 1.4), (fw * 1.1, fw * 1.1), tile, w)


ORDER = ['hips', 'spine', 'chest', 'neck', 'head', 'clavicle.L', 'upper_arm.L', 'lower_arm.L', 'hand.L',
         'clavicle.R', 'upper_arm.R', 'lower_arm.R', 'hand.R', 'thigh.L', 'shin.L', 'foot.L', 'thigh.R',
         'shin.R', 'foot.R']


def reset_pose(rig):
    for p in rig.pose.bones:
        p.rotation_mode = 'QUATERNION'
        p.rotation_quaternion = (1, 0, 0, 0)
        p.location = (0, 0, 0)
        p.scale = (1, 1, 1)
    bpy.context.view_layer.update()


def swing(rig, name, axis, deg, pivot=None):
    p = rig.pose.bones[name]
    piv = V(pivot) if pivot is not None else p.head.copy()
    T = Matrix.Translation(piv)
    p.matrix = T @ Matrix.Rotation(math.radians(deg), 4, axis) @ T.inverted() @ p.matrix
    bpy.context.view_layer.update()


def set_pose(rig, spec, pivot=None):
    """spec maps bone -> list of (axis, degrees); applied parents-first about each bone's head.

    +X on a hanging limb swings it backward; on an upright bone it leans forward (-Y).
    """
    reset_pose(rig)
    for name in ORDER:
        for axis, deg in spec.get(name, []):
            swing(rig, name, axis, deg, pivot if name == 'hips' else None)


def key_pose(rig, frame):
    for p in rig.pose.bones:
        p.keyframe_insert('rotation_quaternion', frame=frame, group=p.name)
        p.keyframe_insert('location', frame=frame, group=p.name)


def make_action(rig, name, keys, fps_end):
    """keys: {frame: spec}. Creates a named action and parks it on the rig as fake-user data."""
    rig.animation_data_create()
    rig.animation_data.action = None
    for frame, spec in sorted(keys.items()):
        set_pose(rig, spec[0], spec[1] if len(spec) > 1 else None)
        key_pose(rig, frame)
    act = rig.animation_data.action
    act.name = name
    act.use_fake_user = True
    rig.animation_data.action = None
    reset_pose(rig)
    return act


def aim(rig, name, target):
    p = rig.pose.bones[name]
    q = (p.tail - p.head).rotation_difference(V(target) - p.head)
    m = q.to_matrix().to_4x4() @ p.matrix.copy()
    m.translation = p.head
    p.matrix = m
    bpy.context.view_layer.update()


def two_bone(rig, a, b, target, pole):
    up, lo = rig.pose.bones[a], rig.pose.bones[b]
    start, end = up.head.copy(), V(target)
    v = end - start
    la, lb = up.bone.length, lo.bone.length
    dist = min(v.length, la + lb - 1e-5)
    d = v.normalized()
    side = V(pole) - start
    side = (side - d * side.dot(d)).normalized()
    along = (la * la - lb * lb + dist * dist) / (2 * dist)
    bend = math.sqrt(max(0, la * la - along * along))
    aim(rig, a, start + d * along + side * bend)
    aim(rig, b, end)


# ----------------------------------------------------------------------------- output
def tri_count(ob):
    ob.data.calc_loop_triangles()
    return len(ob.data.loop_triangles)


def finalize_mesh(ob, rig=None, armature_name='Deform'):
    if rig:
        ob.parent = rig
        ob.modifiers.new(armature_name, 'ARMATURE').object = rig
    ob.modifiers.new('Stable_Export_Triangles', 'TRIANGULATE')


def select(objs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]


def export_fbx(name, objs, anim=False):
    select(objs)
    bpy.ops.export_scene.fbx(filepath=str(OUT / 'Export' / name), use_selection=True,
                             object_types={'ARMATURE', 'MESH', 'EMPTY'}, axis_forward='-Z', axis_up='Y',
                             apply_unit_scale=True, global_scale=1.0, add_leaf_bones=False,
                             bake_anim=anim, bake_anim_use_all_actions=anim, bake_anim_use_nla_strips=False,
                             bake_anim_simplify_factor=0.0, path_mode='RELATIVE', use_mesh_modifiers=True)


def studio(c, target, ortho, key_power=430):
    c.scene.world.node_tree.nodes['Background'].inputs[1].default_value = .6
    def smat(n, col):
        m = bpy.data.materials.new(n)
        m.diffuse_color = (*col, 1)
        m.use_nodes = True
        next(x for x in m.node_tree.nodes if x.type == 'BSDF_PRINCIPLED').inputs['Base Color'].default_value = (*col, 1)
        return m
    fm = bpy.data.meshes.new('StudioFloor')
    fm.from_pydata([(-200, -200, -.015), (200, -200, -.015), (200, 200, -.015), (-200, 200, -.015)], [], [(0, 1, 2, 3)])
    floor = bpy.data.objects.new('StudioFloor', fm)
    c.studio.objects.link(floor)
    fm.materials.append(smat('Studio_Charcoal', (.055, .065, .085)))
    k = ortho / 2.15
    key_power *= .55  # moodier than the first pass
    for n, loc, pw, wd in [('Key', (-3, -4, 5), key_power * k * k, 4 * k), ('Fill', (3, -2, 3), key_power * .5 * k * k, 3 * k),
                           ('Rim', (1, 3, 4), key_power * 1.15 * k * k, 3 * k)]:
        d = bpy.data.lights.new(n, 'AREA')
        d.energy, d.shape, d.size = pw, 'DISK', wd
        o = bpy.data.objects.new(n, d)
        c.studio.objects.link(o)
        o.location = tuple(V(loc) * k + V(target) * (1 - k) * 0)
        track(o, target)
    cd = bpy.data.cameras.new('Review_Camera')
    cam = bpy.data.objects.new('Review_Camera', cd)
    c.studio.objects.link(cam)
    c.scene.camera = cam
    cd.type, cd.ortho_scale = 'ORTHO', ortho
    c.cam = cam
    return cam


def track(o, point):
    o.rotation_euler = (V(point) - o.location).to_track_quat('-Z', 'Y').to_euler()


def render(c, label, loc, target, ortho=None, size=None):
    cam = c.cam
    if ortho: cam.data.ortho_scale = ortho
    if size: c.scene.render.resolution_x, c.scene.render.resolution_y = size
    cam.location = loc
    track(cam, target)
    c.scene.render.filepath = str(OUT / 'Previews' / f'{label}.png')
    bpy.ops.render.render(write_still=True)


def save_blend(c, filename):
    for s in list(bpy.data.scenes):
        if s != c.scene: bpy.data.scenes.remove(s)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'Source' / filename), compress=True)


def write_report(filename, report):
    path = OUT / filename
    existing = json.loads(path.read_text()) if path.exists() else {}
    existing.update(report)
    path.write_text(json.dumps(existing, indent=2) + '\n')
