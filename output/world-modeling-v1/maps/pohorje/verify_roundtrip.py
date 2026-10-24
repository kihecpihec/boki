"""Reimport representative FBXs in a disposable Blender scene."""
import bpy
import contextlib
import io
import json
from pathlib import Path

OUT = Path(__file__).resolve().parent
original = bpy.context.window.scene
results = []
for name in ('PL_Route_A', 'PL_Route_C', 'PL_Shared_Fork'):
    expected = len([o for o in bpy.data.collections[name].objects if o.type == 'MESH'])
    test = bpy.data.scenes.new('Pohorje_ExportVerification')
    bpy.context.window.scene = test
    try:
        with contextlib.redirect_stdout(io.StringIO()):
            bpy.ops.import_scene.fbx(filepath=str(OUT/'Export'/(name+'.fbx')))
        meshes = [o for o in test.objects if o.type == 'MESH']
        results.append({'export': name, 'source_meshes': expected,
                        'reimported_meshes': len(meshes),
                        'mesh_count_passed': expected == len(meshes),
                        'empty_material_slots': sum(s.material is None for o in meshes for s in o.material_slots)})
    finally:
        objects = list(test.objects)
        collections = list(test.collection.children_recursive)
        bpy.context.window.scene = original
        bpy.data.scenes.remove(test)
        for obj in objects:
            bpy.data.objects.remove(obj, do_unlink=True)
        for col in reversed(collections):
            if not col.users:
                bpy.data.collections.remove(col)
(OUT/'roundtrip-validation.json').write_text(json.dumps(results,indent=2)+'\n')
assert all(r['mesh_count_passed'] and not r['empty_material_slots'] for r in results)
print(json.dumps(results))
