"""blender --background --python build_melee.py
One shared melee body/rig, jacket + backpack add-ons, three painted skins (Forest / Izgubljeni meščan / Student).
"""
import sys, math
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
from enemylib import *

c = begin('Melee_Studio')
P = dict(H=1.85, hip_z=.93, neck_z=1.52, chin_z=1.60, sh_x=.19, shoulder_drop=.05, hip_x=.085, ankle_z=.07,
         upper=.31, lower=.31, hand=.20, arm_deg=8, elbow_fwd=.10, foot_len=.28, stoop=.20, knee_fwd=.05)
J = joints(P)
yo = J['yo']

# ----------------------------------------------------------------------------- painted atlases (512px, 4x4 tiles of 128px)
TF, TB, TS, ARM, LEG, HEAD, FACE, JACKET, HAND, SHOE, SOLE, PACK, PTRIM = range(13)

def paint_forest(at, r):
    dark = [(0, '0D151A'), (.5, '1A2933'), (1, '2A3F4B')]
    for t in (TF, TB, TS, ARM, LEG, HEAD, HAND, JACKET, PACK, PTRIM):
        a = at.view(t); p_base(a, r, dark, mott=.06)
        for _ in range(5): p_line(a, r.uniform(0, 1), r.uniform(.2, 1), r.uniform(0, 1), r.uniform(0, .8), '557685', 1, .28)
        p_folds(a, r, '0A1217', 4, .25)
    p_line(at.view(TB), .5, 0, .5, 1, '0A1217', 2, .5)              # spine seam
    p_line(at.view(TF), .5, .1, .5, .85, '0A1217', 1, .45)
    a = at.view(FACE); p_base(a, r, [(0, '17232B'), (1, '2E4350')], mott=.05)
    p_stain(a, r, '04070A', .31, .53, .14, .1, .9); p_stain(a, r, '04070A', .69, .53, .14, .1, .9)   # hollow sockets
    p_rect(a, .30, .545, .32, .56, 'A9C3CF'); p_rect(a, .68, .545, .70, .56, 'A9C3CF')           # eye glints
    p_rect(a, .38, .21, .62, .235, '04070A')                                                    # mouth slit
    p_stain(a, r, '0A1217', .5, .38, .09, .1, .5)                                               # nose hollow
    a = at.view(SHOE); p_base(a, r, [(0, '070C10'), (1, '151F26')])
    a = at.view(SOLE); p_base(a, r, [(0, '05080B'), (1, '0E161B')])

def paint_rust(at, r):
    skin = [(0, '3E3C3A'), (.5, '6B6862'), (1, '7B7871')]
    a = at.view(TF); p_base(a, r, [(0, '1D1A19'), (1, '34302D')]); p_folds(a, r, '100E0D', 4)
    p_rect(a, .36, .12, .64, 1, '77736A'); p_rect(a, .36, .12, .64, 1, '000000', 0)       # shirt placket under the open jacket
    for y in (.8, .6, .4, .22): p_ellipse(a, .5, y, .02, .02, '2A2723')
    p_stain(a, r, '6E1E18', .6, .35, .16, .22, .85); p_stain(a, r, '3A1210', .45, .6, .08, .12, .6)
    p_rect(a, .2, 0, .8, .1, '15110F', .5)
    a = at.view(TB); p_base(a, r, [(0, '1A1716'), (1, '2D2926')]); p_folds(a, r, '0E0C0B', 5); p_stain(a, r, '541812', .35, .5, .12, .2, .6)
    a = at.view(TS); p_base(a, r, [(0, '181514'), (1, '2A2724')]); p_folds(a, r, '0E0C0B', 3)
    a = at.view(ARM); p_line(a, .3, .02, .35, .3, '2A2623', 1, .0); p_base(a, r, [(0, '777269'), (.3, '6F6A62'), (.45, '3A3532'), (1, '2D2926')]); p_stain(a, r, '6E1E18', .4, .2, .25, .12, .7); p_drips(a, r, '5A1914', 5, .3); p_line(a, .25, .02, .3, .32, '4A3E3A', 1, .5); p_line(a, .62, .02, .58, .3, '4A3E3A', 1, .5); p_stain(a, r, '5A3A3A', .5, .12, .3, .1, .6)
    a = at.view(LEG); p_base(a, r, [(0, '221D19'), (.12, '3C352F'), (.55, '574F47'), (1, '4A443D')], mott=.09)
    p_folds(a, r, '2A241F', 6); p_stain(a, r, '3A332D', .5, .5, .3, .08, .6); p_stain(a, r, '2E2824', .5, .88, .35, .1, .5)
    p_stain(a, r, '6B6862', .3, .6, .08, .09, .8); p_stain(a, r, '6E1E18', .66, .3, .12, .16, .75); p_drips(a, r, '5A1914', 4, .45)
    a = at.view(HEAD); p_base(a, r, [(0, '4A4844'), (.6, '6D6A64'), (1, '3C3A38')], mott=.06); p_stain(a, r, '6B3B36', .3, .45, .2, .18, .5)
    a = at.view(FACE); p_base(a, r, [(0, '5A5752'), (.45, '767269'), (1, '4A4844')], mott=.05)
    p_stain(a, r, '1A1210', .31, .53, .15, .1, .8); p_stain(a, r, '1A1210', .69, .53, .15, .1, .8)      # sunken sockets + shadow
    p_ellipse(a, .31, .55, .075, .045, '0A0606'); p_ellipse(a, .69, .55, .075, .045, '0A0606')
    p_rect(a, .30, .545, .325, .565, 'C8C4B4'); p_rect(a, .68, .545, .705, .565, 'C8C4B4')
    p_stain(a, r, '2A1C18', .15, .3, .1, .2, .5); p_stain(a, r, '2A1C18', .85, .3, .1, .2, .5)         # cheek hollows
    p_rect(a, .36, .19, .64, .26, '150C0B'); p_rect(a, .38, .235, .62, .26, 'BDB7A4', .75)
    for x in (.42, .48, .54, .6): p_rect(a, x, .19, x + .008, .26, '150C0B')
    p_stain(a, r, '6E1E18', .55, .1, .18, .12, .85); p_stain(a, r, '34302D', .5, .38, .08, .1, .5)
    a = at.view(JACKET); p_base(a, r, [(0, '3A1E12'), (.25, '56301F'), (1, '75442F')], mott=.09)
    p_folds(a, r, '2A130C', 12, .5, wpx=4); p_stain(a, r, '9A6A4E', .62, .6, .14, .16, .55); p_stain(a, r, '2E1710', .3, .2, .16, .14, .6)
    p_stain(a, r, '6E1E18', .3, .62, .1, .14, .7); p_drips(a, r, '2E1710', 6, .25); p_speckle(a, r, 'B07856', 40, .5)
    a = at.view(HAND); p_base(a, r, [(0, '4E4B46'), (.5, '6B6862'), (1, '77746D')], mott=.05)
    a = at.view(SHOE); p_base(a, r, [(0, '1D1815'), (1, '3B332D')]); p_speckle(a, r, '6A5E52', 30, .5)
    a = at.view(SOLE); p_base(a, r, [(0, '0F0C0A'), (1, '231D19')])
    for t in (PACK, PTRIM): p_base(at.view(t), r, [(0, '17242C'), (1, '2A3F4B')])

def paint_student(at, r):
    skin = [(0, '56604F'), (.5, '6C7564'), (1, '7C8571')]
    a = at.view(TF); p_base(a, r, [(0, '3E4A52'), (1, '66747D')], mott=.07); p_folds(a, r, '2A343A', 4)
    p_ellipse(a, .5, .58, .16, .17, '9B9684', .5); p_rect(a, .43, .56, .57, .6, '3E4A52', .7); p_rect(a, .48, .45, .52, .7, '3E4A52', .7)  # faded print
    p_stain(a, r, '6E1E18', .5, .92, .2, .12, .9); p_stain(a, r, '2A343A', .5, .06, .45, .12, .6)
    a = at.view(TB); p_base(a, r, [(0, '36424A'), (1, '5A6870')], mott=.07); p_folds(a, r, '26303A', 5); p_stain(a, r, '6E1E18', .7, .4, .1, .15, .5)
    a = at.view(TS); p_base(a, r, [(0, '2E3A42'), (1, '56646C')]); p_folds(a, r, '26303A', 3)
    a = at.view(ARM); p_base(a, r, [(0, '6F7867'), (.3, '66705F'), (.42, '4E5C64'), (1, '5A6870')]); p_stain(a, r, '6E1E18', .6, .2, .22, .12, .7); p_drips(a, r, '5A1914', 4, .3); p_line(a, .25, .02, .3, .32, '4A3E3A', 1, .5); p_line(a, .62, .02, .58, .3, '4A3E3A', 1, .5); p_stain(a, r, '5A3A3A', .5, .12, .3, .1, .6)
    a = at.view(LEG); p_base(a, r, [(0, '1C222E'), (.12, '222A39'), (.6, '2B344A'), (1, '252D3E')], mott=.08)
    p_folds(a, r, '1C2230', 6); p_stain(a, r, '4C5C7A', .5, .52, .28, .1, .65); p_stain(a, r, '4C5C7A', .5, .78, .3, .1, .4)
    p_stain(a, r, '1B1A18', .5, .06, .45, .1, .6); p_stain(a, r, '6E1E18', .3, .35, .1, .12, .6); p_line(a, .5, .55, .5, .98, '5A6B8E', 1, .5)
    a = at.view(HEAD); p_base(a, r, [(0, '4B5445'), (.6, '6A7362'), (1, '3A4236')], mott=.06); p_stain(a, r, '6B3B36', .7, .5, .18, .16, .45)
    a = at.view(FACE); p_base(a, r, [(0, '566050'), (.45, '737C69'), (1, '454D40')], mott=.05)
    p_stain(a, r, '161310', .31, .53, .15, .1, .8); p_stain(a, r, '161310', .69, .53, .15, .1, .8)
    p_ellipse(a, .31, .55, .075, .045, '0A0806'); p_ellipse(a, .69, .55, .075, .045, '0A0806')
    p_rect(a, .30, .545, .325, .565, 'D9DCC4'); p_rect(a, .68, .545, .705, .565, 'D9DCC4')
    p_stain(a, r, '2A2A20', .15, .3, .1, .2, .5); p_stain(a, r, '2A2A20', .85, .3, .1, .2, .5)
    p_rect(a, .36, .19, .64, .26, '120E0C'); p_rect(a, .38, .235, .62, .26, 'BDB7A4', .75)
    for x in (.42, .48, .54, .6): p_rect(a, x, .19, x + .008, .26, '120E0C')
    p_stain(a, r, '6E1E18', .45, .1, .16, .1, .8)
    a = at.view(JACKET); p_base(a, r, [(0, '27382D'), (.3, '3C5144'), (1, '50685A')], mott=.08)
    p_folds(a, r, '16241B', 12, .5, wpx=4); p_stain(a, r, '2E4034', .3, .15, .2, .15, .6); p_stain(a, r, '6E4A2E', .65, .5, .12, .14, .5)
    p_drips(a, r, '26382D', 6, .3); p_speckle(a, r, '8FAE98', 36, .45); p_line(a, .02, 0, .02, 1, '26382D', 2, .6)
    a = at.view(HAND); p_base(a, r, [(0, '444C3F'), (.5, '626B59'), (1, '6D765F')], mott=.05)
    a = at.view(SHOE); p_base(a, r, [(0, '5A5A52'), (.5, '8B8A80'), (1, '9C9B90')]); p_stain(a, r, '3E3C36', .5, .15, .5, .15, .8); p_speckle(a, r, '2A2926', 30, .5)
    a = at.view(SOLE); p_base(a, r, [(0, '7A7A70'), (1, 'A8A79B')])
    a = at.view(PACK); p_base(a, r, [(0, '262F32'), (.4, '39454A'), (1, '46565C')], mott=.07); p_folds(a, r, '1C2326', 5)
    p_stain(a, r, '1E2426', .5, .05, .5, .15, .7); p_line(a, .06, .78, .94, .78, '8E8A7A', 1, .8); p_speckle(a, r, '6D7B80', 30, .5)
    a = at.view(PTRIM); p_base(a, r, [(0, '1B2123'), (1, '2C3438')]); p_line(a, .1, .5, .9, .5, '5E6A6E', 1, .6)
    for t in (TS,): pass

PAINT = {'Forest': paint_forest, 'Rust': paint_rust, 'Student': paint_student}
images, mats = {}, {}
for seed, (skin, fn) in enumerate(PAINT.items()):
    at = Atlas('T_Melee_' + skin, 512, 4, seed + 11)
    r = at.rng
    fn(at, r)
    for t in range(13): pixelate(at.view(t), 2)  # chunky low-res look
    images[skin] = at.save(f'T_Melee_{skin}_BaseColor.png')
    mats[skin] = material('MAT_Melee_' + skin, images[skin], .95)
atlas = Atlas('layout', 512, 4)

# ----------------------------------------------------------------------------- body
body = Geo(atlas)
torso = [(.80, .105, .078), (.88, .13, .086), (.95, .14, .09), (1.07, .134, .082), (1.22, .126, .078), (1.38, .155, .092), (1.46, .178, .092), (1.52, .105, .07), (1.545, .06, .05)]
torso_rings(body, J, torso, TF, uvfn=torso_uv(TF, TB, TS, .2, .78, 1.55))
body.tube([V((0, yo(1.49), 1.49)), V((0, yo(1.63), 1.63))], [(.046, .046)] * 2, {'neck': 1}, TS)
head = [(1.60, .04, .055), (1.64, .06, .075), (1.69, .083, .092), (1.75, .09, .10), (1.81, .085, .095), (1.85, .05, .06), (1.875, .016, .022)]
body.tube([V((0, yo(z), z)) for z, _, _ in head], [(rx, ry) for _, rx, ry in head], {'head': 1}, HEAD, uvfn=head_uv(FACE, HEAD, 1.60, 1.875, .1, 1.62))
zf = lambda z: yo(z) - .094
body.prism(V((0, zf(1.74), 1.74)), V((0, zf(1.705) - .016, 1.70)), (.018, .022), (.028, .016), HEAD, {'head': 1})
body.prism(V((-.05, zf(1.775) + .004, 1.775)), V((.05, zf(1.775) + .004, 1.775)), (.014, .016), (.014, .016), HEAD, {'head': 1})
for s_ in (1, -1):
    body.tube([V((s_ * .09, yo(1.72) + .005, 1.715)), V((s_ * .1, yo(1.74) + .005, 1.745)), V((s_ * .092, yo(1.76) + .005, 1.77))], [(.012, .022), (.016, .026), (.011, .02)], {'head': 1}, HEAD)
ARM_R = [(0, .05, .055), (.55, .06, .062), (1, .06, .062), (1.45, .052, .054), (1.9, .046, .048), (2, .049, .051), (2.15, .046, .048), (2.55, .04, .042), (3, .03, .032)]
LEG_R = [(0, .09, .085), (1, .08, .084), (1.5, .069, .075), (1.9, .06, .066), (2, .06, .066), (2.1, .058, .064), (2.6, .046, .052), (3, .038, .044)]
for suf in 'LR':
    pts, bones = arm_chain(J, suf)
    limb_rings(body, pts, ARM_R, bones, ARM)
    pts, bones = leg_chain(J, suf, .07)
    limb_rings(body, pts, LEG_R, bones, LEG)
    hand(body, J, suf, HAND, palm=(.08, .04), finger_len=.56, fw=.018, curl=28, spread=.12)
    s = 1 if suf == 'L' else -1
    shoe(body, s * P['hip_x'], .28, .094, .09, .048, .07, SHOE, SOLE, 'foot.' + suf)

# ----------------------------------------------------------------------------- jacket add-on (open front, ragged hem)
jacket = Geo(atlas)
jt = [(.80, .152, .102), (1.04, .16, .106), (1.22, .15, .1), (1.38, .182, .114), (1.485, .2, .106), (1.54, .10, .075)]
start = len(jacket.v)
torso_rings(jacket, J, jt, JACKET, skip=(0,), cap=(False, False))
for k, dz in enumerate((0, -.07, -.02, -.10, -.03, -.08, -.01, -.06)): jacket.v[start + k] += V((0, 0, dz))
for suf in 'LR':
    pts, bones = arm_chain(J, suf)
    limb_rings(jacket, pts, [(.7, .066, .07), (1, .072, .075), (1.5, .068, .07), (2, .066, .068), (2.5, .06, .062)], bones, JACKET, cap=(False, False))
    for k, dz in enumerate((0, -.04, .01, -.06, 0, -.03, .01, -.05)):
        i = len(jacket.v) - 8 + k
        jacket.v[i] += V((0, 0, dz))

# ----------------------------------------------------------------------------- backpack add-on (rigid to chest)
pack = Geo(atlas)
cw = {'chest': 1}
zc = 1.30
py = yo(zc) + .06 + .08
pack.box((0, py, zc), (.30, .16, .36), PACK, cw)
pack.box((0, py + .005, 1.50), (.30, .17, .08), PACK, cw)
pack.box((0, py + .11, 1.16), (.22, .06, .14), PACK, cw)
for s in (1, -1):
    top = V((s * .11, py - .02, 1.545)); sf = V((s * .11, yo(1.50) - .12, 1.50)); cl = V((s * .11, yo(1.30) - .105, 1.30))
    pack.prism(top, sf, (.045, .012), (.045, .012), PTRIM, cw, ref=(1, 0, 0))
    pack.prism(sf, cl, (.045, .012), (.045, .012), PTRIM, cw, ref=(1, 0, 0))

# ----------------------------------------------------------------------------- rig, meshes
rig = make_rig(c, 'CHR_Melee', humanoid_bones(J))
objs = {}
for name, g in (('M_MeleeBody', body), ('M_Melee_Jacket', jacket), ('M_Melee_Backpack', pack)):
    o = g.build(name, mats['Forest'], c.export, jitter=.004)
    finalize_mesh(o, rig)
    objs[name] = o

# ----------------------------------------------------------------------------- clips (in place, 30 fps)
def spec(*pairs):
    d = {}
    for bone, axis, deg in pairs: d.setdefault(bone, []).append((axis, deg))
    return d

idle_a = spec(('spine', 'X', 5), ('chest', 'X', 3), ('head', 'X', -3), ('upper_arm.L', 'X', -8), ('upper_arm.R', 'X', -8), ('lower_arm.L', 'X', -12), ('lower_arm.R', 'X', -12))
idle_b = spec(('spine', 'X', 8), ('chest', 'X', 5), ('neck', 'X', 3), ('head', 'X', -5), ('upper_arm.L', 'X', -12), ('upper_arm.R', 'X', -10), ('lower_arm.L', 'X', -16), ('lower_arm.R', 'X', -14))
make_action(rig, 'Melee_Idle', {1: (idle_a,), 15: (idle_b,), 30: (idle_a,)}, 30)

def pursuit(phase):
    f = {}
    for suf, off in (('L', 0), ('R', math.pi)):
        th = phase + off
        fwd, flex = 30 * math.sin(th), 42 * max(0, math.cos(th))
        f['thigh.' + suf] = [('X', -fwd)]
        f['shin.' + suf] = [('X', flex)]
        f['foot.' + suf] = [('X', (fwd - flex) * .7)]
        f['upper_arm.' + suf] = [('X', -26 + 20 * math.sin(th))]
        f['lower_arm.' + suf] = [('X', -38)]
    f.update({'spine': [('X', 14)], 'chest': [('X', 10)], 'neck': [('X', 8)], 'head': [('X', -8)]})
    return (f,)

make_action(rig, 'Melee_Pursuit', {1 + i * 6: pursuit(i * math.pi / 2) for i in range(5)}, 25)
reach = spec(('spine', 'X', 30), ('chest', 'X', 12), ('neck', 'X', 10), ('head', 'X', -10), ('upper_arm.L', 'X', -85), ('upper_arm.R', 'X', -80),
             ('lower_arm.L', 'X', -18), ('lower_arm.R', 'X', -22), ('hand.L', 'X', -12), ('hand.R', 'X', -12),
             ('thigh.L', 'X', -28), ('shin.L', 'X', 32), ('thigh.R', 'X', 16), ('shin.R', 'X', 22))
make_action(rig, 'Melee_Contact', {1: (idle_a,), 6: (reach,), 12: (idle_a,)}, 12)
hit = spec(('spine', 'X', -14), ('chest', 'X', -8), ('neck', 'X', -8), ('head', 'X', -16), ('upper_arm.L', 'X', 18), ('upper_arm.R', 'X', 14), ('lower_arm.L', 'X', -20), ('lower_arm.R', 'X', -20))
make_action(rig, 'Melee_Hit', {1: (idle_a,), 4: (hit,), 10: (idle_a,)}, 10)
stagger = spec(('spine', 'X', -18), ('chest', 'X', -10), ('head', 'X', -12), ('thigh.L', 'X', -22), ('thigh.R', 'X', -22), ('shin.L', 'X', 46), ('shin.R', 'X', 46), ('upper_arm.L', 'X', 25), ('upper_arm.R', 'X', 25))
down = spec(('thigh.L', 'X', -20), ('thigh.R', 'X', -8), ('shin.L', 'X', 24), ('shin.R', 'X', 14), ('upper_arm.L', 'Y', -40), ('upper_arm.R', 'Y', 40), ('head', 'X', 14))
make_action(rig, 'Melee_Death', {1: (idle_a,), 8: (stagger,), 24: (down, (0, .10, 0))}, 24)
rig.animation_data.action = None
reset_pose(rig)

# ----------------------------------------------------------------------------- export (all add-ons ride in one FBX; toggle per variant in Unity)
export_fbx('CHR_Melee.fbx', [rig, *objs.values()], anim=True)
report = {'melee': {n: {'vertices': len(o.data.vertices), 'triangles': tri_count(o)} for n, o in objs.items()}}
report['melee'].update({'bones': len(rig.data.bones), 'actions': [a.name for a in bpy.data.actions], 'standing_height_m': P['H'],
                        'max_influences': max(len(v.groups) for o in objs.values() for v in o.data.vertices),
                        'unweighted_vertices': sum(not v.groups for o in objs.values() for v in o.data.vertices)})
write_report('validation.json', report)

# ----------------------------------------------------------------------------- previews
studio(c, (0, 0, .93), 2.3)
def show(skin, jk, bp):
    for o in objs.values(): o.data.materials[0] = mats[skin]
    objs['M_Melee_Jacket'].hide_render = not jk
    objs['M_Melee_Backpack'].hide_render = not bp

view = (2.8, -3.6, 1.7)
show('Forest', False, False); render(c, 'melee-forest-three-quarter', view, (0, 0, .95))
show('Rust', True, False); render(c, 'melee-rust-three-quarter', view, (0, 0, .95))
show('Student', True, True); render(c, 'melee-student-three-quarter', view, (0, 0, .95))
render(c, 'melee-student-back', (-2.2, 4.0, 1.7), (0, 0, .95))
show('Rust', True, False); render(c, 'melee-rust-front', (0, -4.6, 1.2), (0, 0, .95))
render(c, 'melee-rust-head', (1.0, -1.7, 1.9), (0, -.1, 1.7), .6, (900, 900)); c.scene.render.resolution_x, c.scene.render.resolution_y = 900, 1100
show('Forest', False, False); render(c, 'melee-forest-front', (0, -4.6, 1.2), (0, 0, .95), 2.3)
render(c, 'melee-forest-side', (4.6, 0, 1.2), (0, 0, .95))
show('Rust', True, False)
act = bpy.data.actions
for label, frame, name in (('pursuit', 7, 'Melee_Pursuit'), ('contact', 6, 'Melee_Contact'), ('death', 24, 'Melee_Death')):
    rig.animation_data.action = act[name]
    c.scene.frame_set(frame)
    render(c, f'melee-{label}', (3.6, -3.6, 1.4) if label != 'death' else (3.2, -3.4, 1.6), (0, 0, .75 if label != 'death' else .3), size=(900, 900) if label == 'death' else (900, 1100))
rig.animation_data.action = None
reset_pose(rig)
save_blend(c, 'CHR_Melee.blend')
print(json.dumps(report, indent=2))
