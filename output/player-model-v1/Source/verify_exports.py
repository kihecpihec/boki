"""Validate actual FBX files by importing them into clean Blender scenes."""
import bpy, json, math
from pathlib import Path
from mathutils import Vector
OUT=Path(__file__).resolve().parents[1]
report={}
for filename in ('CHR_Boki.fbx','CHR_Boki_BindPose.fbx','PROP_Scooter.fbx'):
    scene=bpy.data.scenes.new('VERIFY_'+filename); bpy.context.window.scene=scene
    bpy.ops.import_scene.fbx(filepath=str(OUT/'Export'/filename))
    meshes=[o for o in scene.objects if o.type=='MESH']
    rigs=[o for o in scene.objects if o.type=='ARMATURE']
    triangles=0
    for o in meshes:
        o.data.calc_loop_triangles(); triangles+=len(o.data.loop_triangles)
        assert all(len(p.vertices)==3 for p in o.data.polygons), 'Export contains non-triangles'
        assert all(math.isfinite(c) for v in o.data.vertices for c in v.co)
    entry={'meshes':len(meshes),'triangles':triangles,'rigs':len(rigs)}
    if rigs:
        rig=rigs[0]; body=meshes[0]
        entry['bones']=len(rig.data.bones)
        entry['has_skin']=any(m.type=='ARMATURE' and m.object==rig for m in body.modifiers)
        assert entry['has_skin'] and entry['bones']==22
        assert all(v.groups for v in body.data.vertices)
        assert all(abs(sum(g.weight for g in v.groups)-1)<1e-4 for v in body.data.vertices)
        entry['action']=rig.animation_data.action.name if rig.animation_data and rig.animation_data.action else None
        scene.frame_set(1)
        dg=bpy.context.evaluated_depsgraph_get(); ev=body.evaluated_get(dg)
        points=[body.matrix_world@v.co for v in ev.data.vertices]
        entry['world_bounds_m']={axis:[min(p[i] for p in points),max(p[i] for p in points)] for i,axis in enumerate('XYZ')}
        assert triangles==1273
        if filename=='CHR_Boki.fbx':
            assert entry['action']
            source=json.loads((OUT/'validation.json').read_text())['ride_bounds_m']
            assert max(abs(entry['world_bounds_m'][a][j]-source[a][j]) for a in 'XYZ' for j in (0,1)) < .0001, 'Ride pose shifted during FBX export'
            entry['ride_bounds_match_source']=True
        else: assert 1.70 < entry['world_bounds_m']['Z'][1] < 1.74
    else:
        assert triangles==628
        for label,y in [('M_Wheel_F',-.52),('M_Wheel_R',.52)]:
            wheel=next(o for o in meshes if o.name.startswith(label))
            assert (wheel.matrix_world.translation-Vector((0,y,.10))).length < .001
        assert any(o.name.startswith('Steering') for o in scene.objects)
        entry['axle_pivots_verified']=True
    report[filename]=entry
(OUT/'export-validation.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
