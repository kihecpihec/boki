"""blender --background --python build_guilt.py
Guilt: 4.2 m elongated pursuer on the shared humanoid rig layout. Featureless head, fused fingers.
"""
import sys, math
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
from enemylib import *

c = begin('Guilt_Studio', world=(.16, .18, .22))
P = dict(H=4.2, hip_z=2.05, neck_z=3.55, chin_z=3.70, sh_x=.34, shoulder_drop=.07, hip_x=.12, ankle_z=.14,
         upper=1.35, lower=1.45, hand=.68, arm_deg=11, elbow_fwd=.12, foot_len=.62, stoop=.75, knee_fwd=.08)
J = joints(P)
yo = J['yo']

at = Atlas('T_Guilt_BaseColor', 256, 2, 21)
BODY, ARM, HEAD, HAND = range(4)
r = at.rng
for t, stops in ((BODY, [(0, '070B10'), (.5, '10171E'), (1, '1B2833')]), (ARM, [(0, '0B1117'), (.5, '17222B'), (1, '23323E')]),
                 (HEAD, [(0, '0C1218'), (.5, '18242E'), (1, '2C3F4C')]), (HAND, [(0, '8C9DA6'), (.45, '2A3944'), (1, '0F161C')])):
    a = at.view(t)
    p_base(a, r, stops, mott=.05, cells=4)
    p_folds(a, r, '04070A', 7, .4, wpx=4)                       # long tension creases
    for _ in range(6): p_line(a, r.uniform(0, 1), 0, r.uniform(0, 1), 1, '4F6B80', 1, .4)   # blue edge planes
    p_speckle(a, r, '4B6678', 25, .35)
    pixelate(a, 2)
img = at.save('T_Guilt_BaseColor.png')
mat = material('MAT_Guilt', img, .95)

g = Geo(at)
torso = [(1.92, .19, .12), (2.05, .245, .13), (2.35, .19, .115), (2.75, .18, .11), (3.15, .27, .135), (3.40, .33, .14), (3.50, .24, .11), (3.56, .12, .08)]
torso_rings(g, J, torso, BODY)
g.tube([V((0, yo(3.50), 3.50)), V((0, yo(3.76), 3.76))], [(.075, .075)] * 2, {'neck': 1}, HEAD)
head = [(3.70, .07, .09), (3.78, .12, .14), (3.90, .14, .16), (4.06, .13, .15), (4.16, .08, .09), (4.20, .02, .03)]
g.tube([V((0, yo(z), z)) for z, _, _ in head], [(rx, ry) for _, rx, ry in head], {'head': 1}, HEAD)
ARM_R = [(0, .09, .095), (.55, .11, .115), (1, .115, .12), (1.5, .092, .098), (1.9, .076, .08), (2, .08, .084), (2.1, .076, .08), (2.5, .064, .068), (3, .046, .05)]
LEG_R = [(0, .13, .12), (1, .13, .13), (1.5, .105, .11), (1.9, .095, .1), (2, .095, .1), (2.1, .092, .097), (2.5, .085, .09), (2.9, .105, .11), (3, .11, .115)]
for suf in 'LR':
    pts, bones = arm_chain(J, suf)
    limb_rings(g, pts, ARM_R, bones, ARM)
    pts, bones = leg_chain(J, suf, .15)
    limb_rings(g, pts, LEG_R, bones, BODY)
    hand(g, J, suf, HAND, palm=(.15, .05), finger_len=.78, fw=.026, claw=.09, curl=32, spread=.22)
    s = 1 if suf == 'L' else -1
    shoe(g, s * P['hip_x'], .62, .23, .17, .08, .12, BODY, HAND, 'foot.' + suf, sole=.05)

# Menace: vertebrae ridge, shoulder-blade spurs and tattered shadow rags (all rigid to the nearest bone).
st = [(J['hips'].z + .02, 'hips'), (J['z1'], 'spine'), (J['z2'], 'chest')]
for z in np.linspace(2.35, 3.45, 9):
    g.prism(V((0, yo(z) + .10, z)), V((0, yo(z) + .21 + .015 * (z - 2.3), z + .035)), (.05, .05), (.008, .008), BODY, z_weights(z, st))
for s_ in (1, -1):
    g.prism(V((s_ * .13, yo(3.2) + .12, 3.25)), V((s_ * .30, yo(3.1) + .27, 3.0)), (.06, .05), (.008, .008), BODY, {'chest': 1})
    for i in range(3):
        sx = J['elbow' + ('L' if s_ > 0 else 'R')]
        a0 = sx + V((s_ * (i - 1) * .035, (i - 1) * .02, 0))
        g.prism(a0, a0 + V((s_ * .03, .05 * (i - 1), -.55 - .12 * i)), (.07, .012), (.008, .008), ARM, {'lower_arm.' + ('L' if s_ > 0 else 'R'): 1})
for i, x in enumerate(np.linspace(-.19, .19, 6)):
    y = yo(1.95) + (.13 if i % 2 else -.13)
    g.prism(V((x, y, 1.97)), V((x * 1.15, y + (.06 if i % 2 else -.06), 1.05 + .12 * (i % 3))), (.10, .012), (.01, .008), BODY, {'hips': 1})

rig = make_rig(c, 'CHR_Guilt', humanoid_bones(J))
body = g.build('M_Guilt', mat, c.export, jitter=.012)
finalize_mesh(body, rig)

def spec(*pairs):
    d = {}
    for bone, axis, deg in pairs: d.setdefault(bone, []).append((axis, deg))
    return d

warn_a = spec(('spine', 'X', 3), ('head', 'X', 4), ('upper_arm.L', 'X', -4), ('upper_arm.R', 'X', -4))
warn_b = spec(('spine', 'X', 5), ('chest', 'X', 2), ('head', 'X', 8), ('upper_arm.L', 'X', -7), ('upper_arm.R', 'X', -2), ('lower_arm.L', 'X', -6))
make_action(rig, 'Guilt_Warning', {1: (warn_a,), 30: (warn_b,), 60: (warn_a,)}, 60)

def chase(phase):
    f = {}
    for suf, off in (('L', 0), ('R', math.pi)):
        th = phase + off
        fwd, flex = 24 * math.sin(th), 34 * max(0, math.cos(th))
        f['thigh.' + suf] = [('X', -fwd)]
        f['shin.' + suf] = [('X', flex)]
        f['foot.' + suf] = [('X', (fwd - flex) * .7)]
        f['upper_arm.' + suf] = [('X', -14 + 14 * math.sin(th))]
        f['lower_arm.' + suf] = [('X', -16)]
    f.update({'spine': [('X', 16)], 'chest': [('X', 10)], 'neck': [('X', 10)], 'head': [('X', -4)]})
    return (f,)
make_action(rig, 'Guilt_Chase', {1 + i * 7: chase(i * math.pi / 2) for i in range(5)}, 29)
shadow = spec(('spine', 'X', 4), ('chest', 'X', 4), ('head', 'X', 14),
              ('upper_arm.L', 'Y', -48), ('upper_arm.R', 'Y', 48), ('upper_arm.L', 'X', -18), ('upper_arm.R', 'X', -18),
              ('lower_arm.L', 'Y', -20), ('lower_arm.R', 'Y', 20), ('lower_arm.L', 'X', -34), ('lower_arm.R', 'X', -34))
make_action(rig, 'Guilt_BossShadow', {1: (shadow,), 2: (shadow,)}, 2)
reset_pose(rig)

export_fbx('CHR_Guilt.fbx', [rig, body], anim=True)
report = {'guilt': {'vertices': len(body.data.vertices), 'triangles': tri_count(body), 'bones': len(rig.data.bones),
                    'actions': [a.name for a in bpy.data.actions], 'standing_height_m': P['H'],
                    'max_influences': max(len(v.groups) for v in body.data.vertices),
                    'unweighted_vertices': sum(not v.groups for v in body.data.vertices)}}
write_report('validation.json', report)

studio(c, (0, 0, 2.1), 4.6, 650)
tgt = (0, 0, 2.1)
render(c, 'guilt-front', (0, -9, 2.6), tgt, 4.6, (900, 1100))
render(c, 'guilt-three-quarter', (6, -8, 3.2), tgt)
render(c, 'guilt-side', (9, 0, 2.6), tgt)
for label, frame, name, loc in (('chase', 8, 'Guilt_Chase', (7, -7, 2.4)), ('boss-shadow-pose', 1, 'Guilt_BossShadow', (0, -9, 2.6))):
    rig.animation_data.action = bpy.data.actions[name]
    c.scene.frame_set(frame)
    render(c, 'guilt-' + label if label == 'chase' else 'guilt-boss-shadow-pose', loc, tgt, 4.6, (900, 1100))
rig.animation_data.action = None; reset_pose(rig)
save_blend(c, 'CHR_Guilt.blend')

# Scale comparison against the player (imported after the save so the .blend stays clean).
boki = OUT.parent / 'player-model-v1' / 'Export' / 'CHR_Boki_BindPose.fbx'
if boki.exists():
    bpy.context.view_layer.active_layer_collection = bpy.context.view_layer.layer_collection.children['STUDIO_NO_EXPORT']
    bpy.ops.import_scene.fbx(filepath=str(boki), axis_forward='-Z', axis_up='Y')
    imported = [o for o in bpy.context.selected_objects]
    for o in imported:
        if o.parent is None: o.location = (1.6, -.3, 0)
    c.scene.render.resolution_x, c.scene.render.resolution_y = 1100, 1100
    render(c, 'guilt-vs-boki', (0, -12, 2.2), (.6, 0, 2.1), 4.9, (1100, 1100))
print(json.dumps(report, indent=2))
