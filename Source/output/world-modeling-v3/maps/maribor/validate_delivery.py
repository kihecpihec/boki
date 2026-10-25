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
import bmesh
from mathutils import Vector, Quaternion
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

HERE=Path(__file__).resolve().parent
REPO=HERE.parents[3]
manifest=json.loads((HERE/'manifest.json').read_text())
bpy.ops.wm.open_mainfile(filepath=str(HERE/'Source/Maribor_Production.blend'))


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
    if not image.name.startswith('T_Maribor_'):continue
    if not image.filepath.startswith('//') or not Path(bpy.path.abspath(image.filepath)).is_file():
        raise RuntimeError('Source image must resolve through a relative path: '+image.name)
    texture_paths.append(image.filepath)
# Check the actual saved render geometry independently of collision dimensions.
source_errors=[];inside_out=0;closed_components=0
render=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.name.startswith('PL_')]
for obj in render:
    bm=bmesh.new();bm.from_mesh(obj.data);remaining=set(bm.faces)
    while remaining:
        stack=[remaining.pop()];component=[]
        while stack:
            face=stack.pop();component.append(face)
            for edge in face.edges:
                for adjacent in edge.link_faces:
                    if adjacent in remaining:remaining.remove(adjacent);stack.append(adjacent)
        if not all(len(e.link_faces)==2 for f in component for e in f.edges):continue
        closed_components+=1
        origin=component[0].verts[0].co.copy()
        volume=sum((f.verts[0].co-origin).dot((f.verts[i].co-origin).cross(f.verts[i+1].co-origin))/6 for f in component for i in range(1,len(f.verts)-1))
        if volume < -1e-5:inside_out+=1
    bm.free()
if inside_out:source_errors.append(str(inside_out)+' inside-out closed render components')
guide_failures=[]
for shape in manifest['colliders']:
    obj=bpy.data.objects.get(shape['name'])
    q=shape['rotation'];q=Quaternion((q[3],q[0],q[1],q[2]));center=Vector(shape['center'])
    points=[q.inverted()@(Vector((-v.x,v.z,-v.y))-center) for v in [obj.matrix_world@v.co for v in obj.data.vertices]]
    size=shape['size'] if shape['kind']=='Box' else [shape['radius']*2,shape['height'],shape['radius']*2]
    worst=max(abs(abs(p[axis])-size[axis]/2) for axis in range(3) for p in points) if shape['kind']=='Box' else 0
    if worst>.001:guide_failures.append(shape['name'])
if guide_failures:source_errors.append(str(len(guide_failures))+' Blender collision guides differ from Unity dimensions or orientation')
trees=[BVHTree.FromObject(o,bpy.context.evaluated_depsgraph_get()) for o in render if not o.name.startswith('PL_Seal_')]
visual_floor_failures=[]
for point in manifest['floorSamples']:
    origin=Vector((-point['x'],-point['z'],point['y']+.12))
    hits=[]
    for tree in trees:
        p,n,_,_=tree.ray_cast(origin,Vector((0,0,-1)),.30)
        if p is not None and n.z>.65:hits.append(p.z)
    if not hits or abs(max(hits)-point['y'])>.065:
        visual_floor_failures.append(dict(position=point,visible_height=max(hits) if hits else None))
if visual_floor_failures:source_errors.append(str(len(visual_floor_failures))+' visual floor / collision height mismatches')
validator=REPO/'.agents/skills/boki-map-model-maker/scripts/validate_map.py'
canonical=runpy.run_path(str(validator))['report']
roundtrip=[]; errors=list(source_errors)
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
        if export['owner']=='Roofs':required = [name for name in source if name.startswith('PL_C_Roof_')]
        if export['owner']=='C':required = [name for name in required if not name.startswith('PL_C_Roof_')]
        if export['owner']=='Shared':required += [name for name in source if name.startswith('PL_Seal_')]
        if sorted(names)!=sorted(required):errors.append(export['file']+': render objects missing or duplicated')
    if hashlib.sha256(path.read_bytes()).hexdigest()!=hashlib.sha256((REPO/'Game/Assets/Art/Maribor_v3'/export['file']).read_bytes()).hexdigest():
        errors.append(export['file']+': Unity asset differs from delivered FBX')
    roundtrip.append(dict(file=export['file'],objects=count,triangles=triangles,max_vertex_delta_m=worst,
                         validator_errors=imported_validation['errors'],sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
for texture in (HERE/'Textures').glob('*.png'):
    unity_texture=REPO/'Game/Assets/Art/Maribor_v3/Textures'/texture.name
    if not unity_texture.is_file() or texture.read_bytes()!=unity_texture.read_bytes():
        errors.append(texture.name+': Unity texture differs from delivery')
report=dict(source_geometry=dict(closed_components=closed_components,inside_out_components=inside_out,visual_floor_samples=len(manifest['floorSamples']),visual_floor_failures=visual_floor_failures,collision_guide_failures=guide_failures),relative_source_textures=texture_paths,canonical_validator=dict(errors=canonical['errors'],warnings=canonical['warnings']),roundtrip=roundtrip,errors=errors)
(HERE/'delivery-validation.json').write_text(json.dumps(report,indent=2)+'\n')
print('MARIBOR_DELIVERY_VALIDATION',json.dumps(dict(files=len(roundtrip),errors=errors,max_vertex_delta_m=max(r['max_vertex_delta_m'] for r in roundtrip))))
if errors:raise RuntimeError('FBX delivery validation failed')
