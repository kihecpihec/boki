"""blender --background --python verify_exports.py
Reimport every exported FBX into a clean scene and assert triangulation, skinning, clips and size."""
import bpy, json, math
from pathlib import Path
from mathutils import Vector
OUT = Path(__file__).resolve().parents[1]
EXPECT = {  # file: (max triangles, rig bones, clips, height/extent axis range)
    'CHR_Melee.fbx': dict(bones=22, clips={'Melee_Idle', 'Melee_Pursuit', 'Melee_Contact', 'Melee_Hit', 'Melee_Death'}, height=(1.80, 1.90)),
    'CHR_Guilt.fbx': dict(bones=22, clips={'Guilt_Warning', 'Guilt_Chase', 'Guilt_BossShadow'}, height=(4.1, 4.3)),
    'CHR_Professor.fbx': dict(bones=22, clips={'Professor_Idle'}, height=(1.74, 1.82)),
    'CHR_Watcher.fbx': dict(width=(.6, .75), sockets={'Socket_ProjectileOrigin'}),
    'PROJ_WatcherOrb.fbx': dict(), 'PROP_ExamPaper.fbx': dict(), 'PROP_Lectern.fbx': dict(), 'PROP_FloorRing.fbx': dict(),
}
report = {}
for name, exp in EXPECT.items():
    scene = bpy.data.scenes.new('VERIFY_' + name); bpy.context.window.scene = scene
    before = set(bpy.data.actions)
    bpy.ops.import_scene.fbx(filepath=str(OUT / 'Export' / name))
    meshes = [o for o in scene.objects if o.type == 'MESH']
    rigs = [o for o in scene.objects if o.type == 'ARMATURE']
    tris = 0; entry = {'meshes': {}}
    pts = []
    for o in meshes:
        o.data.calc_loop_triangles(); t = len(o.data.loop_triangles); tris += t
        assert all(len(p.vertices) == 3 for p in o.data.polygons), name + ': non-triangles'
        assert all(math.isfinite(c) for v in o.data.vertices for c in v.co)
        entry['meshes'][o.name] = t
        pts += [o.matrix_world @ v.co for v in o.data.vertices]
    entry['triangles'] = tris
    if rigs:
        rig = rigs[0]
        assert len(rig.data.bones) == exp['bones'], 'bone count'
        for o in meshes:
            assert any(m.type == 'ARMATURE' and m.object == rig for m in o.modifiers), o.name + ' not skinned'
            assert all(v.groups for v in o.data.vertices), o.name + ' unweighted'
            assert all(len(v.groups) <= 2 for v in o.data.vertices)
        clips = {a.name.split('|')[-1] for a in bpy.data.actions if a not in before}
        entry['clips'] = sorted(clips)
        assert exp['clips'] <= clips, f"missing clips {exp['clips'] - clips}"
        # evaluate in bind pose (no action): height check
        for r in rigs:
            r.animation_data.action = None
            for pb in r.pose.bones:
                pb.rotation_mode = 'QUATERNION'; pb.rotation_quaternion = (1, 0, 0, 0); pb.location = (0, 0, 0); pb.scale = (1, 1, 1)
        bpy.context.view_layer.update()
        dg = bpy.context.evaluated_depsgraph_get()
        zs = [(o.matrix_world @ v.co).z for o in meshes for v in o.evaluated_get(dg).data.vertices]
        entry['height_m'] = [round(min(zs), 3), round(max(zs), 3)]
        assert exp['height'][0] < max(zs) < exp['height'][1], f'{name} height out of range {min(zs):.3f}..{max(zs):.3f}'
    if 'width' in exp:
        xs = [p.x for p in pts]; entry['width_m'] = round(max(xs) - min(xs), 3)
        assert exp['width'][0] < max(xs) - min(xs) < exp['width'][1]
        names = {o.name for o in scene.objects}
        assert exp['sockets'] <= names, 'missing socket'
        entry['sockets'] = sorted(exp['sockets'])
    report[name] = entry
(OUT / 'export-validation.json').write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report, indent=2))
