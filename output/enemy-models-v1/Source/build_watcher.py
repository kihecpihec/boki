"""blender --background --python build_watcher.py
Watcher (forest + city skins), exam paper prop and watcher projectile orb.
Forward / eye direction is Blender -Y.
"""
import sys, math, bmesh
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
from enemylib import *

c = begin('Watcher_Studio')

# ----------------------------------------------------------------------------- textures
def shell_atlas(skin, stops_top, stops_bot, rim, seed, streak=None):
    at = Atlas('T_Watcher_' + skin, 256, 2, seed)
    r = at.rng
    for t, stops in ((0, stops_top), (1, stops_bot)):
        a = at.view(t)
        p_base(a, r, stops, mott=.09, cells=4)
        for _ in range(7): p_line(a, r.uniform(0, 1), r.uniform(0, 1), r.uniform(0, 1), r.uniform(0, 1), rim, 1, .35)   # scratches
        p_folds(a, r, '05080B' if not streak else '2A100D', 4, .3, wpx=4)
        if streak: p_drips(a, r, streak, 8, .9, .55, 3, .15, .5)
        p_speckle(a, r, rim, 60, .4)
        for _ in range(5):                                                       # glowing cracks leaking red
            x0, y0 = r.uniform(.1, .9), r.uniform(.1, .9)
            for seg in range(4):
                x1, y1 = x0 + r.uniform(-.16, .16), y0 + r.uniform(-.16, .16)
                p_line(a, x0, y0, x1, y1, 'E02A1A', 2, .9); x0, y0 = x1, y1
        pixelate(a, 2)
    a = at.view(2); p_base(a, r, [(0, '6E6658'), (1, 'B5AC98')], mott=.06); p_stain(a, r, '7A2018', .5, .1, .5, .15, .6); pixelate(a, 2)   # teeth: stained bone
    return at

shell_forest = shell_atlas('Forest', [(0, '0E161C'), (.6, '1C2A36'), (1, '2E4352')], [(0, '080C10'), (1, '141D25')], '6F90A3', 3)
shell_city = shell_atlas('City', [(0, '3A1612'), (.6, '6E2E27'), (1, '8C4332')], [(0, '210D0B'), (1, '4A1D18')], 'D08A5E', 4, '2A0E0B')
img_forest = shell_forest.save('T_Watcher_Forest_BaseColor.png')
img_city = shell_city.save('T_Watcher_City_BaseColor.png')
eye_at = Atlas('T_Watcher_Eye', 64, 2, 5)
ea = eye_at.view(0); p_base(ea, eye_at.rng, [(0, '8A1810'), (1, 'B82418')], mott=.02, fine=0)
p_ellipse(ea, .5, .5, .45, .45, 'E8402C'); p_ellipse(ea, .5, .5, .34, .34, 'FF6A2A'); p_ellipse(ea, .5, .5, .07, .36, '140202')
eb = eye_at.view(1); p_base(eb, eye_at.rng, [(0, 'FFC090'), (1, 'FFE2C0')], mott=.02, fine=0)
img_eye = eye_at.save('T_Watcher_Eye.png')
mat_forest = material('MAT_Watcher_Forest', img_forest, .9)
mat_city = material('MAT_Watcher_City', img_city, .9)
mat_eye = material('MAT_Watcher_Eye', img_eye, .5, emit_image=True)

# ----------------------------------------------------------------------------- shell (two halves + bezel)
shell = Geo(Atlas('layout', 256, 2), .4)
ring = [(-.275, 0), (-.16, -.095), (0, -.12), (.16, -.095), (.275, 0), (.16, .09), (0, .12), (-.16, .09)]
front_lift = [0, .035, .05, .035, 0, 0, 0, 0]  # opens a slit in the front for the eye
for sgn, tile in ((1, 0), (-1, 1)):
    base = [V((x, y, sgn * lift)) for (x, y), lift in zip(ring, front_lift)]
    mid = [V((x * .6, y * .6, sgn * .105)) for x, y in ring]
    apex = V((0, .005, sgn * .20))
    verts = base + mid + [apex]
    faces = []
    for k in range(8):
        k2 = (k + 1) % 8
        faces += [(k, k2, 8 + k2, 8 + k), (8 + k, 8 + k2, 16)]
    faces.append(tuple(range(8)))  # underside, closes the half
    # Orient every face away from the half's centre.
    centre = V((0, 0, sgn * .07))
    faces = [face_toward(verts, f, sum((verts[i] for i in f), V()) / len(f) - centre) for f in faces]
    # The flat underside faces the other half; point it into the body instead.
    faces[-1] = face_toward(verts, faces[-1], V((0, 0, -sgn)))
    shell.add(verts, {}, faces, tile)

# Menace: apex horns, side spikes, a toothed maw along the slit and angry brow lids over the eye.
for sgn in (1, -1):
    shell.prism(V((0, .005, sgn * .18)), V((0, .03, sgn * .31)), (.07, .07), (.006, .006), 1, {})
for sx in (1, -1):
    shell.prism(V((sx * .25, 0, 0)), V((sx * .36, .02, 0.025)), (.07, .07), (.006, .006), 1, {})
    shell.prism(V((sx * .12, .06, 0)), V((sx * .20, .17, 0.01)), (.05, .05), (.006, .006), 1, {})   # rear barbs
fx, fy, fl = [-.275, -.16, 0, .16, .275], [0, -.095, -.12, -.095, 0], [0, .035, .05, .035, 0]
for sgn in (1, -1):
    for x in (-.23, -.17, -.12, .12, .17, .23):
        y, lift = np.interp(x, fx, fy), np.interp(x, fx, fl)
        shell.prism(V((x, y, sgn * (lift + .004))), V((x * .97, y - .012, sgn * (lift - .052))), (.026, .022), (.004, .004), 2, {}, ref=(1, 0, 0))
for sx in (1, -1):
    shell.prism(V((sx * .12, -.134, .098)), V((sx * .02, -.134, .045)), (.024, .022), (.018, .02), 1, {})   # angry brow lids

# Eye bezel: front annulus + inward-facing inner wall, centred in the slit.
def circle(y, r, n=8):
    return [V((r * math.cos(2 * math.pi * k / n + math.pi / 8), y, r * math.sin(2 * math.pi * k / n + math.pi / 8))) for k in range(n)]

yo_, yi_, yb_ = -.128, -.128, -.075
outer, inner_f, inner_b = circle(yo_, .09), circle(yi_ - .002, .056), circle(yb_, .056)
bv = outer + inner_f + inner_b
bf = []
for k in range(8):
    k2 = (k + 1) % 8
    bf.append(face_toward(bv, (k, k2, 8 + k2, 8 + k), (0, -1, 0)))
    mid = (bv[8 + k] + bv[16 + k2]) / 2
    bf.append(face_toward(bv, (8 + k, 8 + k2, 16 + k2, 16 + k), -V((mid.x, 0, mid.z))))
shell.add(bv, {}, bf, 1)

# ----------------------------------------------------------------------------- eye disc (separate material) + projectile orb
eye = Geo(Atlas('eye', 32, 2), None)
def disc(geo, y_front, y_back, r, tile):
    f, b = circle(y_front, r), circle(y_back, r)
    vv = f + b
    faces = [tuple(range(8)), tuple(range(8, 16))] + [(k, (k + 1) % 8, 8 + (k + 1) % 8, 8 + k) for k in range(8)]
    geo.add(vv, {}, orient(vv, faces), tile)
disc(eye, -.100, -.078, .050, 0)

# ----------------------------------------------------------------------------- build objects
root = bpy.data.objects.new('CHR_Watcher', None); c.export.objects.link(root)
o_shell = shell.build('M_WatcherShell', mat_forest, c.export, jitter=.003); o_shell.parent = root
o_eye = eye.build('M_WatcherEye', mat_eye, c.export); o_eye.parent = root
sock = bpy.data.objects.new('Socket_ProjectileOrigin', None); c.export.objects.link(sock)
sock.parent = root; sock.location = (0, -.13, 0); sock.empty_display_size = .03
for o in (o_shell, o_eye): finalize_mesh(o)

# Projectile orb (own file): 20-face icosphere on the eye material.
bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=1, radius=.05)
me = bpy.data.meshes.new('PROJ_WatcherOrb'); bm.to_mesh(me); bm.free()
orb = bpy.data.objects.new('PROJ_WatcherOrb', me); c.export.objects.link(orb)
orb.location = (.6, 0, .6)
me.materials.append(mat_eye)
lay = me.uv_layers.new(name='Atlas')
for p in me.polygons:
    p.use_smooth = False
    for li in p.loop_indices: lay.data[li].uv = (.08, .25)
orb.modifiers.new('Stable_Export_Triangles', 'TRIANGULATE')

# ----------------------------------------------------------------------------- exam paper (own file): shallow closed bent sheet
paper_at = Atlas('T_ExamPaper', 256, 2, 9)
pr = paper_at.rng
for t in (0, 1):
    a_ = paper_at.view(t)
    p_base(a_, pr, [(0, 'C9C5B2'), (1, 'E2DFCD')], mott=.04, cells=4)
    p_stain(a_, pr, 'A8946A', .22 + .5 * t, .2, .16, .12, .35)              # coffee ring / age stain
    p_line(a_, 0, .5, 1, .5, 'B4B09C', 1, .6)                                 # fold crease
    p_line(a_, .5, 0, .5, 1, 'D8D5C4', 1, .4)
    for row in range(2, 13):                                                  # printed lines, ragged right edge
        y = row / 14
        x1 = .88 - (.05 * ((row * 37) % 7)) / 7 - (.25 if row % 4 == 0 else 0)
        p_rect(a_, .12, y, x1, y + .028, '55534A', .85)
    p_rect(a_, .12, .93, .55, .975, '3E3C36', .9)                            # title bar
a0, a1 = paper_at.view(0), paper_at.view(1)
for k in (-1, 1):
    p_line(a0, .60, .66 + k * .08, .86, .66 - k * .08, 'C8402F', 3, .95)     # red X
p_ellipse(a1, .72, .22, .16, .11, 'C8402F', .9); p_ellipse(a1, .72, .22, .1, .066, 'D9D5C3', 1); p_ellipse(a1, .72, .22, .03, .02, 'C8402F', .95)
for t in (0, 1): pixelate(paper_at.view(t), 2)
img_paper = paper_at.save('T_ExamPaper_BaseColor.png')
mat_paper = material('MAT_ExamPaper', img_paper, .95)
W, Hh, th = .21, .30, .003
xs, ys = (-W / 2, 0, W / 2), (0, -.012, 0)
verts, wts = [], {}
for sgn in (-1, 1):  # front sheet (toward -Y) then back sheet
    for xi, yi in zip(xs, ys):
        for z in (-Hh / 2, Hh / 2): verts.append(V((xi, yi + sgn * th / 2, z)))
# indices: front i*2+j (0..5), back 6 + i*2+j
def idx(side, i, j): return side * 6 + i * 2 + j
P = verts
quads = []
fuv = []
for i in range(2):
    quads.append(face_toward(P, (idx(0, i, 0), idx(0, i + 1, 0), idx(0, i + 1, 1), idx(0, i, 1)), (0, -1, 0)))
    quads.append(face_toward(P, (idx(1, i, 0), idx(1, i + 1, 0), idx(1, i + 1, 1), idx(1, i, 1)), (0, 1, 0)))
for i, d in ((0, (-1, 0, 0)), (2, (1, 0, 0))):
    quads.append(face_toward(P, (idx(0, i, 0), idx(1, i, 0), idx(1, i, 1), idx(0, i, 1)), d))
for j, d in ((0, (0, 0, -1)), (1, (0, 0, 1))):
    for i in range(2):
        quads.append(face_toward(P, (idx(0, i, j), idx(0, i + 1, j), idx(1, i + 1, j), idx(1, i, j)), d))
pg = Geo(paper_at)
def paper_uv(face):
    out = []
    for vi in face:
        v = P[vi]
        u = (v.x + W / 2) / W
        out.append((u if vi < 6 else 1 - u, (v.z + Hh / 2) / Hh))
    return out
tiles = [0 if all(vi < 6 for vi in f) else 1 for f in quads]
uvs = [paper_uv(f) if t == 0 or all(vi >= 6 for vi in f) else [(.02, .02)] * len(f) for f, t in zip(quads, tiles)]
pg.add(P, {}, quads, tiles, uvs)
o_paper = pg.build('PROP_ExamPaper', mat_paper, c.export)
o_paper.location = (-.6, 0, .6)
o_paper.modifiers.new('Stable_Export_Triangles', 'TRIANGULATE')

# ----------------------------------------------------------------------------- export
for o in (orb, o_paper): o.location = (0, 0, 0)
export_fbx('CHR_Watcher.fbx', [root, o_shell, o_eye, sock])
export_fbx('PROJ_WatcherOrb.fbx', [orb])
export_fbx('PROP_ExamPaper.fbx', [o_paper])
report = {'watcher': {'shell': tri_count(o_shell), 'eye': tri_count(o_eye), 'total': tri_count(o_shell) + tri_count(o_eye),
                      'size_m': [.55, .24, .40], 'orb_triangles': tri_count(orb), 'paper_triangles': tri_count(o_paper)}}
bb = [o_shell.matrix_world @ V(v) for v in o_shell.bound_box]
report['watcher']['bounds_m'] = [[min(p[i] for p in bb), max(p[i] for p in bb)] for i in range(3)]
write_report('validation.json', report)

# ----------------------------------------------------------------------------- previews
orb.location = (.50, -.05, .12); o_paper.location = (-.55, 0, .12)
root.location = (0, 0, .35)
studio(c, (0, 0, .35), 1.4)
for label, mat, loc in (('watcher-forest-front', mat_forest, (0, -4, .4)), ('watcher-forest-three-quarter', mat_forest, (2.2, -3.2, 1.2)),
                        ('watcher-city-three-quarter', mat_city, (2.2, -3.2, 1.2)), ('watcher-back', mat_forest, (-2.2, 3.2, 1.2))):
    o_shell.data.materials[0] = mat
    render(c, label, loc, (0, 0, .35), 1.2, (1000, 800))
# paper + orb close-up
root.hide_render = True; o_shell.hide_render = True; o_eye.hide_render = True
orb.location = (.12, 0, .3); o_paper.location = (-.10, -.4, .3)
render(c, 'paper-and-orb', (.7, -3.2, .75), (0, 0, .3), .6, (1000, 700))
root.location = (0, 0, .35)
orb.location = (.55, 0, .35); o_paper.location = (-.55, 0, .35)
root.hide_render = o_shell.hide_render = o_eye.hide_render = False
save_blend(c, 'CHR_Watcher.blend')
print(json.dumps(report, indent=2))
