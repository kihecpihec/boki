"""Read-only geometry and FBX checks. Run in Blender on Source/Maribor.blend."""
import bpy
import json
import math
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
OUT=Path(__file__).resolve().parent
layout=json.loads((OUT/'layout.json').read_text())
markers=json.loads((OUT/'markers.json').read_text())
collisions=json.loads((OUT/'collision-guides.json').read_text())
scene=bpy.data.scenes['Maribor']
rendercols=[c for c in scene.collection.children if c.name not in ('MB_Collision_Guides','MB_Gameplay_Markers','MB_Module_Library','MB_Presentation')]
floors=[o for c in rendercols for o in c.objects if o.name.startswith('Floor_')]

def tree(objects):
    verts=[];faces=[]
    for o in objects:
        start=len(verts)
        verts.extend(o.matrix_world@v.co for v in o.data.vertices)
        faces.extend([start+i for i in p.vertices] for p in o.data.polygons)
    return BVHTree.FromPolygons(verts,faces)

floor_tree=tree(floors)
static_meshes=[o for c in rendercols for o in c.objects if o.type=='MESH' and not o.name.startswith(('Floor_','EntryGate_','RearSeal_','Hills'))]
static_tree=tree(static_meshes)
blocks=[c for c in collisions if not c['dynamic'] and not c['name'].startswith('COL_Floor_')]

def blocker(p, radius=.35):
    found=[]
    for c in blocks:
        x,y,z=c['center'];w,d,h=c['size']
        rot=c.get('rotation_z',0)
        dx,dy=p[0]-x,p[1]-y
        lx=dx*math.cos(rot)+dy*math.sin(rot);ly=-dx*math.sin(rot)+dy*math.cos(rot)
        # Test controller footprint against (rotated) collision proposals over the standing height.
        if z+h/2>.25 and z-h/2<1.8 and abs(lx)<w/2+radius and abs(ly)<d/2+radius:
            found.append(c['name'])
    return found

issues=[];samples=0
for key,points in layout['paths'].items():
    for a,b in zip(points,points[1:]):
        a,b=Vector(a),Vector(b)
        for i in range(max(1,math.ceil((b-a).length/.5))+1):
            p=a+(b-a)*(i/max(1,math.ceil((b-a).length/.5)))
            hit,normal,idx,dist=floor_tree.ray_cast(Vector((p.x,p.y,2)),Vector((0,0,-1)),3)
            samples+=1
            if hit is None or abs(hit.z)>.02: issues.append(dict(path=key,xy=list(p),issue='missing floor support'))
            for direction in ((1,0,0),(-1,0,0),(0,1,0),(0,-1,0)):
                h,*_=static_tree.ray_cast(Vector((p.x,p.y,1.2)),Vector(direction),.35)
                if h is not None:
                    issues.append(dict(path=key,xy=list(p),issue='actual render geometry blocks standing clearance'));break
            hitblocks=blocker(p)
            if hitblocks: issues.append(dict(path=key,xy=list(p),issue='static guide obstruction',objects=hitblocks))
charge_issues=[]
for m in markers:
    if m['kind']!='StationaryCharge': continue
    p=m['position']
    hit,*_=floor_tree.ray_cast(Vector((p[0],p[1],2)),Vector((0,0,-1)),3)
    if hit is None or blocker(p,.55): charge_issues.append(dict(name=m['name'],blocked_by=blocker(p,.55),has_floor=hit is not None))
triangles=0;zero=[];counts=[]
for c in rendercols:
    tris=0
    for o in c.objects:
        if o.type!='MESH': continue
        o.data.calc_loop_triangles();tris+=len(o.data.loop_triangles)
        if any(p.area<1e-10 for p in o.data.polygons): zero.append(o.name)
    triangles+=tris;counts.append(dict(collection=c.name,triangles=tris,mesh_objects=sum(o.type=='MESH' for o in c.objects)))

def bounds(objects):
    pts=[o.matrix_world@Vector(corner) for o in objects if o.type=='MESH' for corner in o.bound_box]
    return [min(v[i] for v in pts) for i in range(3)]+[max(v[i] for v in pts) for i in range(3)]

roundtrips=[]
for cname in ('MB_Route_A','MB_Route_C'):
    source=list(bpy.data.collections[cname].objects)
    before=set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(OUT/'Export'/(cname+'.fbx')))
    added=list(set(bpy.data.objects)-before)
    expected=bounds(source);actual=bounds(added)
    error=max(abs(a-b) for a,b in zip(expected,actual))
    def tris(obs):
        total=0
        for o in obs:
            if o.type=='MESH':o.data.calc_loop_triangles();total+=len(o.data.loop_triangles)
        return total
    roundtrips.append(dict(file=cname+'.fbx',source_triangles=tris(source),imported_triangles=tris(added),bounds_error_m=error,passed=error<.001 and tris(source)==tris(added)))
    for o in added:bpy.data.objects.remove(o,do_unlink=True)
report=dict(status='passed' if not(issues or charge_issues or zero) and all(r['passed'] for r in roundtrips) else 'failed',render_triangles_all_routes=triangles,collections=counts,zero_area_meshes=zero,path_samples=samples,path_issues=issues,charge_issues=charge_issues,fbx_roundtrips=roundtrips,gate_semantics='Closed default gate meshes excluded from static traversal checks; Unity must bind selection and rear seals.',unity_import='pending',controller_and_navigation='unverified',windows_5600g_performance='unmeasured')
(OUT/'validation.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
if report['status']!='passed': raise RuntimeError('Geometry validation failed; see validation.json')
