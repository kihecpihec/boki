"""One-time revision of the delivered source: short approaches, broad rolling arenas.

Run against the existing styled source, then finish_pohorje.py. Refuses a second pass.
The live Blender session and Unity assets are never touched.
"""
import bpy
import json
import math
from pathlib import Path
from mathutils import Vector, Matrix

OUT = Path(__file__).resolve().parent
scene = bpy.data.scenes['Pohorje_Large']
bpy.context.window.scene = scene
if scene.get('short_paths_rolling_arenas'):
    raise RuntimeError('This source already contains the arena revision.')
layout = json.loads((OUT/'layout.json').read_text())
old_pockets = layout['pockets']
KNOTS = [(-32,-16),(0,0),(90,45),(300,150),(360,246),(490,311),(544,338)]

def map_y(y):
    for (a,c),(b,d) in zip(KNOTS,KNOTS[1:]):
        if y <= b:
            return c+(y-a)*(d-c)/(b-a)
    return 338+(y-544)*.5

def transform(p):
    return Vector((p[0]*1.25, map_y(p[1]), p[2]))

pockets = {k:[x*1.25,map_y(y),w*1.25,map_y(y+d)-map_y(y)]
           for k,(x,y,w,d) in old_pockets.items()}

def elevation(k,x,y):
    cx,cy,w,d = pockets[k]
    u=(x-cx)/w+.5; v=(y-cy)/d
    if not (0<u<1 and 0<v<1):return 0.
    # Flat entrance/exit aprons and perimeter. Smooth slopes connect every shelf.
    edge=min(1.,u*w/7,(1-u)*w/7,v*d/9,(1-v)*d/9)
    edge=edge*edge*(3-2*edge)
    def mound(a,b,sx,sy):return math.exp(-((u-a)/sx)**2-((v-b)/sy)**2)
    if k=='A':
        h=3.8*mound(.25,.38,.21,.22)+2.8*mound(.75,.7,.22,.24)-1.6*mound(.53,.57,.18,.23)
    elif k=='B':
        h=3.4*mound(.24,.63,.2,.27)+2.4*mound(.75,.34,.19,.22)-1.4*mound(.55,.54,.16,.30)
    else:
        h=2.6*mound(.25,.4,.19,.26)+2.2*mound(.75,.66,.18,.22)-1.25*mound(.5,.58,.17,.22)
    return h*edge

collision = bpy.data.collections['PL_Collision_Guides']
# Transform existing geometry once. Shared module meshes retain their original size.
for o in list(scene.objects):
    if any(c.name.startswith('PL_Module_Library') for c in o.users_collection):continue
    if o.type=='MESH' and not o.name.startswith('PSX_PresentationFog'):
        mw=o.matrix_world.copy()
        verts=[mw@v.co for v in o.data.vertices]
        o.data=o.data.copy()
        # Keep each disconnected tree component round instead of stretching foliage.
        forest=any(c.name=='PL_Forest_Chunks' for c in o.users_collection)
        if forest:
            parent=list(range(len(verts)))
            def root(i):
                while parent[i]!=i:
                    parent[i]=parent[parent[i]];i=parent[i]
                return i
            for e in o.data.edges:
                a,b=map(root,e.vertices);parent[a]=b
            groups={}
            for i in range(len(verts)):groups.setdefault(root(i),[]).append(i)
            for ids in groups.values():
                cx=(min(verts[i].x for i in ids)+max(verts[i].x for i in ids))/2
                cy=(min(verts[i].y for i in ids)+max(verts[i].y for i in ids))/2
                delta=transform((cx,cy,0))-Vector((cx,cy,0))
                for i in ids:o.data.vertices[i].co=verts[i]+delta
        else:
            for v,p in zip(o.data.vertices,verts):v.co=transform(p)
        o.matrix_world=Matrix.Identity(4)
        o.data.update()
    else:
        o.location=transform(o.location)

# Replace each flat floor and its guide with the same continuous triangulated terrain.
arena_reports=[]
for k,(cx,cy,w,d) in pockets.items():
    floor=bpy.data.objects['PL_'+k+'_CombatSpace']
    material=floor.data.materials[0]
    bpy.data.objects.remove(floor,do_unlink=True)
    old=bpy.data.objects.get('PL_COL_'+k+'_CombatSpace')
    if old:bpy.data.objects.remove(old,do_unlink=True)
    nx=math.ceil(w/2.5);ny=math.ceil(d/2.5)
    verts=[];faces=[]
    for j in range(ny+1):
        for i in range(nx+1):
            x=cx-w/2+w*i/nx;y=cy+d*j/ny
            verts.append((x,y,8+elevation(k,x,y)))
    for j in range(ny):
        for i in range(nx):
            a=j*(nx+1)+i;b=a+1;c=a+nx+1;e=c+1
            faces.extend([(a,b,e),(a,e,c)])
    mesh=bpy.data.meshes.new('PL_'+k+'_RollingArena');mesh.from_pydata(verts,[],faces);mesh.update()
    mesh.materials.append(material)
    uv=mesh.uv_layers.new(name='UVMap')
    for loop in mesh.loops:
        p=mesh.vertices[loop.vertex_index].co;uv.data[loop.index].uv=(p.x/4,p.y/4)
    obj=bpy.data.objects.new('PL_'+k+'_CombatSpace',mesh)
    bpy.data.collections['PL_Route_'+k].objects.link(obj)
    guide=bpy.data.objects.new('PL_COL_'+k+'_CombatSpace',mesh.copy())
    collision.objects.link(guide);guide.display_type='WIRE'
    guide['primitive']='STATIC_SURFACE';guide['route']=k
    # Lift fixtures rigidly onto their new supporting ground; leave perimeter walls.
    for o in bpy.data.collections['PL_Route_'+k].objects:
        if o==obj or o.type!='MESH':continue
        if not any(s in o.name for s in ('Charge','Notice','Paper','Cart','Lamp')):continue
        center=sum((v.co for v in o.data.vertices),Vector())/max(1,len(o.data.vertices))
        dz=elevation(k,center.x,center.y)
        for v in o.data.vertices:v.co.z+=dz
    max_slope=0.
    for f in mesh.polygons:
        if f.normal.z>0:max_slope=max(max_slope,math.degrees(math.acos(min(1,f.normal.z))))
    arena_reports.append(dict(route=k,dimensions_m=[w,d],area_m2=round(w*d,1),
                              min_height_m=min(v[2] for v in verts),max_height_m=max(v[2] for v in verts),
                              maximum_triangle_slope_degrees=round(max_slope,2),triangles=len(faces)))

# Lower the scenic underlay beneath the rolling floors, including basin bottoms.
for o in bpy.data.collections['PL_Terrain_Chunks'].objects:
    if o.type!='MESH':continue
    for v in o.data.vertices:
        for k,(cx,cy,w,d) in pockets.items():
            if abs(v.co.x-cx)<=w/2+8 and cy-8<=v.co.y<=cy+d+8:
                v.co.z=min(v.co.z,5.5)

# Source markers and external semantic manifests follow their local ground.
markers=json.loads((OUT/'markers.json').read_text())
for m in markers:
    x,y,z=transform(m['xyz']);k=m['route']
    if k in pockets:z+=elevation(k,x,y)
    m['xyz']=[x,y,z]
    if m['kind']=='combat occupancy; free exit':
        m['width']=pockets[k][2];m['depth']=pockets[k][3]
        m['height_range_m']=[6,13]
    if 'clear_width' in m:
        a=m['angle'];m['clear_width']*=math.hypot(1.25*math.cos(a),.5*math.sin(a))
        m['angle']=math.atan2(.5*math.sin(a),1.25*math.cos(a))
    obj=next((o for o in bpy.data.collections['PL_Gameplay_Markers'].objects if o.name.split('.')[0]=='PL_'+m['name']),None)
    if obj:
        obj.location=m['xyz']
        for field in ('width','depth','clear_width','angle'):
            if field in m:obj[field]=m[field]
(OUT/'markers.json').write_text(json.dumps(markers,indent=2)+'\n')
instances=json.loads((OUT/'forest-instances.json').read_text())
for record in instances:record['xyz']=list(transform(record['xyz']))
(OUT/'forest-instances.json').write_text(json.dumps(instances,indent=2)+'\n')
for key,pts in layout['paths'].items():layout['paths'][key]=[[x*1.25,map_y(y)] for x,y in pts]
layout['pockets']=pockets
layout['revision']='short_paths_rolling_arenas'
layout['coordinate_mapping']={'x_scale':1.25,'y_knots':KNOTS}
# Width is now direction dependent. Record the minimum actual width per ribbon.
for key,pts in layout['paths'].items():
    oldpts=json.loads((OUT/'layout.json').read_text())['paths'][key]
    scales=[]
    for (ax,ay),(bx,by) in zip(oldpts,oldpts[1:]):
        dx=bx-ax;dy=by-ay;l=math.hypot(dx,dy)
        if l<1e-6:continue
        sy=(map_y(by)-map_y(ay))/(by-ay) if by!=ay else .5
        # Project the transformed cross-section onto the new path normal.
        scales.append(1.25*sy*l/math.hypot(1.25*dx,sy*dy))
    layout['widths'][key]=round(layout['widths'][key]*min(scales),2)
for k in 'ABC':
    keys=['Safe','Fork'+k,'Link'+k,k,'Exit'+k,'Merge'+k,'Final']
    length=sum(math.dist(a,b) for key in keys for a,b in zip(layout['paths'][key],layout['paths'][key][1:]))+pockets[k][3]
    old=layout['stats']['route_stats'][k]
    old['previous_centerline_m']=old['centerline_m']
    old['centerline_m']=round(length,1)
    old['lane_width_m']=layout['widths'][k]
    old['combat_dimensions_m']=pockets[k][2:]
layout['stats']['terrain_footprint_m']=[480,354]
(OUT/'layout.json').write_text(json.dumps(layout,indent=2)+'\n')
# Rebuild guide manifest directly from actual source geometry. Never label deformed
# boxes as Unity BoxColliders: the owner must fit primitives to the revised guide.
guides=[]
for o in collision.objects:
    if o.type!='MESH':continue
    is_floor='CombatSpace' in o.name or 'Travel_' in o.name
    o['primitive']='STATIC_SURFACE' if is_floor else 'REFIT_BOX'
    guides.append(dict(name=o.name,route=o.get('route','shared'),primitive=o['primitive'],
                       dynamic_gate=bool(o.get('dynamic_gate')),vertex_count=len(o.data.vertices),
                       bounds_min=[min(v.co[i] for v in o.data.vertices) for i in range(3)],
                       bounds_max=[max(v.co[i] for v in o.data.vertices) for i in range(3)]))
(OUT/'collision-guides.json').write_text(json.dumps(guides,indent=2)+'\n')
scene['short_paths_rolling_arenas']=True
(OUT/'arena-revision.json').write_text(json.dumps(dict(arenas=arena_reports,route_stats=layout['stats']['route_stats'],
    note='Geometry checks only. Refit primitive blockers; use static collision for graded floors. Unity/controller/performance checks pending.'),indent=2)+'\n')
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Source/Pohorje_Large.blend'))
print(json.dumps(arena_reports,indent=2))
