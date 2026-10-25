"""Validate the actual saved Blender model and all exported FBXs in a fresh scene.

Blender -b --factory-startup --python-exit-code 1 --python validate_delivery.py
Writes delivery-validation.json, including the canonical map validator's result.
"""
import collections
import hashlib
import json
from pathlib import Path
import runpy

import bpy
from mathutils.kdtree import KDTree

HERE=Path(__file__).resolve().parent
REPO=HERE.parents[3]
manifest=json.loads((HERE/'manifest.json').read_text())
bpy.ops.wm.open_mainfile(filepath=str(HERE/'Source/Pohorje_Production.blend'))


def snapshot(obj):
    if obj.type!='MESH': return dict(position=obj.matrix_world.translation.copy())
    obj.data.calc_loop_triangles()
    vertices=[obj.matrix_world@v.co for v in obj.data.vertices]
    tree=KDTree(len(vertices))
    for index,vertex in enumerate(vertices): tree.insert(vertex,index)
    tree.balance()
    return dict(vertices=vertices,tree=tree,triangles=len(obj.data.loop_triangles),materials=sorted(m.name for m in obj.data.materials))


source={obj.name:snapshot(obj) for obj in bpy.context.scene.objects if obj.type in ('MESH','EMPTY')}
texture_paths=[]
for image in bpy.data.images:
    if not image.name.startswith('T_Pohorje_'):continue
    if not image.filepath.startswith('//') or not Path(bpy.path.abspath(image.filepath)).is_file():
        raise RuntimeError('Source image must resolve through a relative path: '+image.name)
    texture_paths.append(image.filepath)
validator=REPO/'.agents/skills/boki-map-model-maker/scripts/validate_map.py'
canonical=runpy.run_path(str(validator))['report']
roundtrip=[]; errors=[]
for export in manifest['exports']:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system='METRIC'
    bpy.context.scene.unit_settings.scale_length=1
    path=HERE/'Export'/export['file']
    bpy.ops.import_scene.fbx(filepath=str(path))
    imported_validation=runpy.run_path(str(validator))['report']
    errors.extend(export['file']+': '+error for error in imported_validation['errors'])
    worst=0; count=0; triangles=0; names=[]
    for obj in bpy.context.scene.objects:
        if obj.type not in ('MESH','EMPTY'):continue
        names.append(obj.name); count+=1
        if obj.name not in source:
            errors.append(export['file']+': unexpected '+obj.name); continue
        original=source[obj.name]; imported=snapshot(obj)
        if obj.type=='EMPTY':
            worst=max(worst,(original['position']-imported['position']).length)
            continue
        triangles+=imported['triangles']
        if original['triangles']!=imported['triangles']:
            errors.append(obj.name+': triangle count differs')
        if sorted(n.split('.')[0] for n in imported['materials'])!=original['materials']:
            errors.append(obj.name+': material slots differ')
        delta=max(original['tree'].find(v)[2] for v in imported['vertices'])
        delta=max(delta,max(imported['tree'].find(v)[2] for v in original['vertices']))
        worst=max(worst,delta)
        if delta>0.001: errors.append(obj.name+': round-trip vertex drift exceeds 1 mm')
    expected=len(manifest['markers']) if export['owner']=='Markers' else len(manifest['colliders']) if export['owner']=='CollisionGuides' else None
    if expected is not None and count!=expected: errors.append(export['file']+': object count differs')
    if export['owner'] not in ('Markers','CollisionGuides'):
        required=[name for name in source if name.startswith('PL_'+export['owner']+'_')]
        if export['owner']=='Shared':required += [name for name in source if name.startswith('PL_Seal_')]
        if sorted(names)!=sorted(required):errors.append(export['file']+': render objects missing or duplicated')
    if hashlib.sha256(path.read_bytes()).hexdigest()!=hashlib.sha256((REPO/'Game/Assets/Art/Pohorje_v3'/export['file']).read_bytes()).hexdigest():
        errors.append(export['file']+': Unity asset differs from delivered FBX')
    roundtrip.append(dict(file=export['file'],objects=count,triangles=triangles,max_vertex_delta_m=worst,
                         validator_errors=imported_validation['errors'],sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
for texture in (HERE/'Textures').glob('*.png'):
    unity_texture=REPO/'Game/Assets/Art/Pohorje_v3/Textures'/texture.name
    if not unity_texture.is_file() or texture.read_bytes()!=unity_texture.read_bytes():
        errors.append(texture.name+': Unity texture differs from delivery')
report=dict(relative_source_textures=texture_paths,canonical_validator=dict(errors=canonical['errors'],warnings=canonical['warnings']),roundtrip=roundtrip,errors=errors)
(HERE/'delivery-validation.json').write_text(json.dumps(report,indent=2)+'\n')
print('POHORJE_DELIVERY_VALIDATION',json.dumps(dict(files=len(roundtrip),errors=errors,max_vertex_delta_m=max(r['max_vertex_delta_m'] for r in roundtrip))))
if errors:raise RuntimeError('FBX delivery validation failed')
