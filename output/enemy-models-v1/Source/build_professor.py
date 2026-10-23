"""blender --background --python build_professor.py
Professor boss (1.78 m), separate glasses mesh, lectern and floor ring. Forward is Blender -Y.
Lectern origin is floor-centre; place it at professor-local (0, -0.45 forward, 0).
"""
import sys, math
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
from enemylib import *

c = begin('Professor_Studio', world=(.13, .14, .17))
P = dict(H=1.78, hip_z=.88, neck_z=1.47, chin_z=1.53, sh_x=.205, shoulder_drop=.045, hip_x=.095, ankle_z=.075,
         upper=.30, lower=.27, hand=.17, arm_deg=20, elbow_fwd=.05, foot_len=.29, stoop=.03, knee_fwd=.01)
J = joints(P)
yo = J['yo']

# ----------------------------------------------------------------------------- painted atlas (512px, 4x4 tiles of 128px)
at = Atlas('T_Professor_BaseColor', 512, 4, 31)
JACKET, SHIRT, JACKET_B, TROUSER, SKIN, SHOE, HAIR, FACE, FRAME, BELT, LENS, TRIM, SOLE, HAND = range(14)
r = at.rng
def tweed(a, base, dark, light):
    p_base(a, r, [(0, dark), (.5, base), (1, light)], mott=.06, cells=4)
    h, w, _ = a.shape
    chk = ((np.arange(h)[:, None] // 14 + np.arange(w)[None, :] // 14) % 2)[:, :, None]      # check pattern like the mock
    a[...] = np.clip(a * (1 - .22 * chk) + .01 * chk, 0, 1)
a = at.view(JACKET); tweed(a, '3F5A47', '2B3F32', '50705A'); p_folds(a, r, '1C2A21', 9, .35, wpx=4); p_stain(a, r, '2B3F32', .5, .1, .4, .1, .6)
a = at.view(JACKET_B); tweed(a, '3A5443', '27392D', '4B6954'); p_folds(a, r, '1C2A21', 7, .3)
a = at.view(SHIRT); p_base(a, r, [(0, '8C8A7C'), (1, 'B4B2A2')], mott=.05); p_folds(a, r, '6E6C60', 4, .25)
p_rect(a, .48, 0, .52, 1, '7A786C', .6)
for y in (.12, .32, .52, .72, .88): p_ellipse(a, .5, y, .018, .018, '58564C')
p_stain(a, r, '8A7E62', .3, .12, .18, .1, .4)
a = at.view(TROUSER); p_base(a, r, [(0, '1F2122'), (.15, '2E3031'), (.6, '3C3E3F'), (1, '34363A')], mott=.05); p_folds(a, r, '151617', 7, .35, wpx=4); p_line(a, .5, 0, .5, 1, '4A4C4D', 1, .35)
p_stain(a, r, '2A2B2C', .5, .55, .3, .07, .5)
a = at.view(SKIN); p_base(a, r, [(0, 'A8896C'), (.5, 'C9A98A'), (1, 'D6B898')], mott=.04)
a = at.view(HAND); p_base(a, r, [(0, 'B89878'), (1, 'D2B393')], mott=.03)
a = at.view(SHOE); p_base(a, r, [(0, '1E1511'), (.5, '3A2C27'), (1, '55433A')], mott=.05); p_speckle(a, r, '7A665A', 50, .5)
a = at.view(SOLE); p_base(a, r, [(0, '0F0B09'), (1, '221A16')])
a = at.view(HAIR); p_base(a, r, [(0, '2E221A'), (.5, '4A382B'), (1, '5A4636')], mott=.07)
for _ in range(14): p_line(a, r.uniform(0, 1), 0, r.uniform(0, 1), 1, '6B5642', 1, .35)
for _ in range(6): p_line(a, r.uniform(0, 1), 0, r.uniform(0, 1), 1, 'A8A294', 1, .45)         # greying strands
a = at.view(FRAME); p_base(a, r, [(0, '0C0E10'), (1, '1E2226')], mott=.02)
a = at.view(BELT); p_base(a, r, [(0, '0A0B0C'), (1, '1A1C1E')], mott=.02)
a = at.view(LENS); p_base(a, r, [(0, '9AA6A8'), (1, 'C4CCCD')])
a = at.view(TRIM); p_base(a, r, [(0, '9A988A'), (1, 'C2C0B0')], mott=.04)
a = at.view(FACE); p_base(a, r, [(0, 'B08F72'), (.4, 'CCAA8A'), (1, 'D8BA9A')], mott=.04)
p_stain(a, r, 'A8846A', .5, .88, .45, .1, .6)                                      # receding hairline shade
p_stain(a, r, 'B38B72', .16, .3, .1, .2, .6); p_stain(a, r, 'B38B72', .84, .3, .1, .2, .6)   # sunken cheeks
p_stain(a, r, '9E7A60', .3, .41, .1, .04, .75); p_stain(a, r, '9E7A60', .7, .41, .1, .04, .75)   # eye bags
p_rect(a, .23, .535, .41, .575, '3E2E22'); p_rect(a, .59, .535, .77, .575, '3E2E22')                 # heavy brows
p_rect(a, .26, .44, .39, .50, 'E8DECB'); p_rect(a, .61, .44, .74, .50, 'E8DECB')
p_rect(a, .31, .44, .35, .50, '2A2320'); p_rect(a, .65, .44, .69, .50, '2A2320')
p_rect(a, .26, .495, .39, .52, '8A6A52'); p_rect(a, .61, .495, .74, .52, '8A6A52')
p_stain(a, r, 'A8846A', .5, .35, .06, .12, .6); p_rect(a, .43, .27, .57, .295, '9A7660')
p_rect(a, .39, .17, .61, .195, '7E5C4C'); p_stain(a, r, 'A8846A', .5, .13, .2, .06, .5)
for yy in (.62, .66): p_line(a, .22, yy, .78, yy, 'B79678', 1, .35)                  # forehead lines
p_stain(a, r, 'A48A70', .5, .1, .25, .1, .5); p_speckle(a, r, '8A7058', 60, .4)    # stubble hint
for t in range(14): pixelate(at.view(t), 2)
img = at.save('T_Professor_BaseColor.png')
mat = material('MAT_Professor', img, .92)

# ----------------------------------------------------------------------------- body
g = Geo(at)
torso_rings(g, J, [(.80, .125, .09), (.88, .14, .095), (.95, .15, .10), (1.10, .145, .095), (1.28, .15, .095), (1.40, .17, .10), (1.46, .10, .07)], SHIRT)
torso_rings(g, J, [(.90, .158, .108), (.95, .158, .108)], BELT)
g.box((0, yo(.925) - .108, .925), (.035, .012, .03), LENS, {'hips': 1})
torso_rings(g, J, [(.74, .16, .108), (.95, .168, .112), (1.10, .162, .106), (1.28, .17, .108), (1.40, .19, .114), (1.47, .12, .085)], JACKET, skip=(0,), cap=(False, False))
for s in (1, -1):
    g.prism(V((s * .05, yo(1.46) - .03, 1.455)), V((s * .078, yo(1.49) - .02, 1.52)), (.05, .022), (.05, .022), TRIM, {'chest': 1})
g.tube([V((0, yo(1.46), 1.46)), V((0, yo(1.58), 1.58))], [(.048, .05)] * 2, {'neck': 1}, SKIN)
hz = [(1.53, .04, .05), (1.57, .062, .07), (1.64, .083, .09), (1.71, .088, .095), (1.76, .08, .088), (1.78, .05, .058)]
g.tube([V((0, yo(z), z)) for z, _, _ in hz], [(rx, ry) for _, rx, ry in hz], {'head': 1}, SKIN, uvfn=head_uv(FACE, SKIN, 1.53, 1.78, .1, 1.55))
g.prism(V((0, yo(1.625) - .084, 1.625)), V((0, yo(1.59) - .118, 1.585)), (.026, .03), (.034, .02), SKIN, {'head': 1})
for s in (1, -1):
    g.tube([V((s * .088, .004, 1.62)), V((s * .098, .004, 1.65)), V((s * .09, .004, 1.675))], [(.012, .022), (.016, .025), (.011, .019)], {'head': 1}, SKIN)
# Hair: horseshoe cap covering the sides and back, bare crown, hairline lifted only at the front.
h0 = len(g.v)
g.tube([V((0, .02, 1.60)), V((0, .014, 1.665)), V((0, .01, 1.725)), V((0, .01, 1.775))], [(.094, .097), (.099, .103), (.092, .098), (.06, .07)], {'head': 1}, HAIR, cap=(False, False))
for ring, lift in ((0, .09), (1, .06)):
    for k in range(8):
        i = h0 + ring * 8 + k
        if g.v[i].y < -.05: g.v[i] = V((g.v[i].x, g.v[i].y, g.v[i].z + lift))
ARM_R = [(0, .05, .055), (.55, .062, .066), (1, .066, .07), (1.45, .058, .062), (1.9, .054, .058), (2, .055, .059), (2.1, .053, .057), (2.55, .049, .053), (3, .046, .05)]
LEG_R = [(0, .092, .09), (1, .088, .09), (1.4, .08, .085), (1.8, .072, .078), (2, .07, .076), (2.2, .068, .074), (2.6, .063, .07), (3, .062, .07)]
for suf in 'LR':
    pts, bones = arm_chain(J, suf)
    limb_rings(g, pts, ARM_R, bones, JACKET)
    limb_rings(g, [J['wrist' + suf] + V((0, 0, .03)), J['wrist' + suf] + V((0, 0, -.005))], [(0, .042, .042), (1, .04, .04)], ['lower_arm.' + suf], TRIM, cap=(False, False))
    pts, bones = leg_chain(J, suf, .06)
    limb_rings(g, pts, LEG_R, bones, TROUSER)
    hand(g, J, suf, HAND, palm=(.08, .03), finger_len=.6, fw=.017, curl=14, spread=.06)
    s = 1 if suf == 'L' else -1
    shoe(g, s * P['hip_x'], .29, .095, .08, .05, .06, SHOE, SOLE, 'foot.' + suf, sole=.022)

# Glasses: separate rigid mesh on the head bone.
gl = Geo(at, None)
hw = {'head': 1}
gz = 1.648
gy = yo(gz) - .090 - .009  # just in front of the face plane
for s in (1, -1):
    cx = s * .043  # frames only: lenses are omitted so the painted eyes read through (no transparency in the budget)
    gl.box((cx, gy, gz + .0225), (.066, .012, .008), FRAME, hw)
    gl.box((cx, gy, gz - .0225), (.066, .012, .008), FRAME, hw)
    gl.box((cx - .029, gy, gz), (.008, .012, .045), FRAME, hw)
    gl.box((cx + .029, gy, gz), (.008, .012, .045), FRAME, hw)
    gl.prism(V((s * .076, gy + .004, gz + .01)), V((s * .092, yo(gz) + .01, gz + .012)), (.006, .006), (.006, .006), FRAME, hw)
gl.box((0, gy, gz + .012), (.02, .012, .008), FRAME, hw)

rig = make_rig(c, 'CHR_Professor', humanoid_bones(J))
body = g.build('M_Professor', mat, c.export, jitter=.0015)
glasses = gl.build('M_Glasses', mat, c.export)
finalize_mesh(body, rig); finalize_mesh(glasses, rig)

# ----------------------------------------------------------------------------- idle: hands planted on the lectern
LECTERN_Y = -.45
def lectern_pose():
    reset_pose(rig)
    aim(rig, 'spine', (0, J['spine'].y - .03, J['spine'].z)); aim(rig, 'chest', (0, J['chest'].y - .08, J['chest'].z))
    aim(rig, 'neck', (0, J['neck'].y - .10, J['neck'].z + .03)); aim(rig, 'head', (0, J['head'].y - .10, 1.77))
    for s, suf in ((1, 'L'), (-1, 'R')):
        two_bone(rig, 'upper_arm.' + suf, 'lower_arm.' + suf, (s * .22, -.30, 1.10), (s * .5, .05, 1.2))
        aim(rig, 'hand.' + suf, (s * .22, -.46, 1.085))
rig.animation_data_create()
lectern_pose(); key_pose(rig, 1)
aim(rig, 'head', (0, J['head'].y - .10, 1.765)); key_pose(rig, 30)
lectern_pose(); key_pose(rig, 60)
act = rig.animation_data.action; act.name = 'Professor_Idle'; act.use_fake_user = True
rig.animation_data.action = None
reset_pose(rig)

# ----------------------------------------------------------------------------- props
pat = Atlas('T_Professor_Props', 128, 2, 41)
pr_ = pat.rng
a = pat.view(0); p_base(a, pr_, [(0, '4E331E'), (.5, '6E4A2C'), (1, '7E5936')], mott=.07, cells=4)
for _ in range(16): p_line(a, 0, pr_.uniform(0, 1), 1, pr_.uniform(0, 1), '3A2414', 1, .3)    # wood grain
a = pat.view(1); p_base(a, pr_, [(0, '3A2414'), (1, '5A3C22')], mott=.05)
for _ in range(10): p_line(a, 0, pr_.uniform(0, 1), 1, pr_.uniform(0, 1), '24160B', 1, .3)
p_speckle(pat.view(0), pr_, 'A07A50', 60, .4)
pat.fill(2, 'E0463B', var=.0, block=8); pat.fill(3, 'F27A63', var=.0, block=8)
for t in (0, 1): pixelate(pat.view(t), 2)
pimg = pat.save('T_Professor_Props.png')
mat_props = material('MAT_Professor_Props', pimg, .9)
mat_ring = material('MAT_FloorRing', pimg, .5, emit_image=True)
lg = Geo(pat, .4)
lw = {}
lg.box((0, 0, .045), (.70, .56, .09), 1, lw)
lg.box((0, 0, .55), (.60, .44, .92), 0, lw)
lg.box((0, -.225, .55), (.44, .03, .62), 1, lw)
tv = [V(p) for p in [(-.35, .27, 1.00), (.35, .27, 1.00), (.35, -.30, 1.00), (-.35, -.30, 1.00),
                     (-.35, .27, 1.10), (.35, .27, 1.10), (.35, -.30, 1.02), (-.35, -.30, 1.02)]]
tf = [(0, 1, 2, 3), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
lg.add(tv, lw, orient(tv, tf), 1)
o_lectern = lg.build('PROP_Lectern', mat_props, c.export)
o_lectern.modifiers.new('Stable_Export_Triangles', 'TRIANGULATE')

rg = Geo(pat, None)
n, ro, ri, hh = 16, 1.0, .92, .02
def rp(r, z): return [V((r * math.cos(2 * math.pi * k / n), r * math.sin(2 * math.pi * k / n), z)) for k in range(n)]
rv = rp(ro, hh) + rp(ri, hh) + rp(ro, 0) + rp(ri, 0)
rf = []
for k in range(n):
    k2 = (k + 1) % n
    rf += [(k, k2, n + k2, n + k), (2 * n + k, 3 * n + k, 3 * n + k2, 2 * n + k2), (k, 2 * n + k, 2 * n + k2, k2), (n + k, n + k2, 3 * n + k2, 3 * n + k)]
rf = [face_toward(rv, f, sum((rv[i] for i in f), V()) / 4 - V((0, 0, hh / 2)) if i % 4 in (2,) else
                  {0: V((0, 0, 1)), 1: V((0, 0, -1)), 3: -(sum((rv[j] for j in f), V()) / 4 * V((1, 1, 0)))}[i % 4]) for i, f in enumerate(rf)]
rg.add(rv, {}, rf, 2)
o_ring = rg.build('PROP_FloorRing', mat_ring, c.export)
o_ring.modifiers.new('Stable_Export_Triangles', 'TRIANGULATE')

# ----------------------------------------------------------------------------- export + report
o_lectern.location = (0, 0, 0)
export_fbx('CHR_Professor.fbx', [rig, body, glasses], anim=True)
export_fbx('PROP_Lectern.fbx', [o_lectern])
export_fbx('PROP_FloorRing.fbx', [o_ring])
report = {'professor': {'body_triangles': tri_count(body), 'glasses_triangles': tri_count(glasses),
                        'total_triangles': tri_count(body) + tri_count(glasses), 'bones': len(rig.data.bones),
                        'actions': ['Professor_Idle'], 'standing_height_m': P['H'], 'lectern_triangles': tri_count(o_lectern),
                        'floor_ring_triangles': tri_count(o_ring), 'lectern_offset_m': [0, LECTERN_Y, 0],
                        'max_influences': max(len(v.groups) for v in body.data.vertices),
                        'unweighted_vertices': sum(not v.groups for v in body.data.vertices)}}
write_report('validation.json', report)

# ----------------------------------------------------------------------------- previews
o_ring.hide_render = True
o_lectern.location = (0, LECTERN_Y, 0)
o_lectern.hide_render = True
studio(c, (0, 0, .9), 2.1)
for label, loc in (('professor-front', (0, -5, 1.0)), ('professor-three-quarter', (2.6, -3.8, 1.5)), ('professor-side', (5, 0, 1.0)), ('professor-back', (-2.4, 4.0, 1.5))):
    render(c, label, loc, (0, 0, .9), 2.1)
render(c, 'professor-head', (1.1, -1.6, 1.8), (0, 0, 1.64), .55, (900, 900))
rig.animation_data.action = bpy.data.actions['Professor_Idle']; c.scene.frame_set(1)
o_lectern.hide_render = False
render(c, 'professor-at-lectern', (2.4, -3.6, 1.5), (0, -.2, .9), 1.9)
rig.animation_data.action = None; reset_pose(rig)
o_lectern.hide_render = True
o_ring.hide_render = False; o_ring.location = (0, 0, 0)
render(c, 'floor-ring', (0, -6, 6), (0, 0, 0), 2.4, (900, 900))
o_ring.location = (0, 0, 0); o_lectern.location = (0, LECTERN_Y, 0)
o_ring.hide_render = o_lectern.hide_render = False
save_blend(c, 'CHR_Professor.blend')
print(json.dumps(report, indent=2))
