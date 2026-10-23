"""Add sparse arena landmarks and close two approach ribbon joins. Idempotent."""
import bpy, math, json
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree

OUT=Path(__file__).resolve().parent
scene=bpy.data.scenes['Pohorje_Large']
bpy.context.window.scene=scene
layout=json.loads((OUT/'layout.json').read_text())
collision=bpy.data.collections['PL_Collision_Guides']

def material(prefix):return next(m for m in bpy.data.materials if prefix in m.name)
def mesh_object(name,verts,faces,col,mat):
    data=bpy.data.meshes.new(name);data.from_pydata(verts,[],faces);data.update()
    data.materials.append(mat)
    uv=data.uv_layers.new(name='UVMap')
    for l in data.loops:
        v=data.vertices[l.vertex_index].co;uv.data[l.index].uv=(v.x/2,v.y/2+v.z/3)
    obj=bpy.data.objects.new(name,data);col.objects.link(obj);return obj

def guide(obj,route,kind):
    co=bpy.data.objects.new('PL_COL_'+obj.name.removeprefix('PL_'),obj.data.copy())
    collision.objects.link(co);co.display_type='WIRE';co['route']=route;co['primitive']=kind
    return co

def box(name,pos,size,col,mat,route):
    x,y,z=pos;w,d,h=size
    verts=[(x+i*w/2,y+j*d/2,z+k*h) for k in (0,1) for j in (-1,1) for i in (-1,1)]
    faces=[(0,2,3,1),(4,5,7,6),(0,1,5,4),(2,6,7,3),(0,4,6,2),(1,3,7,5)]
    o=mesh_object(name,verts,faces,col,mat);guide(o,route,'BOX');return o

if not scene.get('arena_landmarks'):
    for k,(cx,cy,w,d) in layout['pockets'].items():
        col=bpy.data.collections['PL_Route_'+k]
        floor=bpy.data.objects['PL_'+k+'_CombatSpace']
        tree=BVHTree.FromPolygons([v.co for v in floor.data.vertices],[list(f.vertices) for f in floor.data.polygons])
        def floor_z(x,y):return tree.ray_cast(Vector((x,y,30)),Vector((0,0,-1)))[0].z
        for side in (-1,1):
            x=cx+side*w*.32;y=cy+d*(.36 if side<0 else .7)
            z=floor_z(x,y)
            if k=='A':
                # Broad broken stone and a low timber stack leave room to circle.
                box(f'PL_A_ArenaStone_{side}',(x,y,z-.15),(3.8,2.8,1.8),col,material('PL_Rock'),k)
                box(f'PL_A_ArenaStoneStep_{side}',(x+side*2.5,y+1,z-.12),(2,2,1),col,material('PL_Rock'),k)
                box(f'PL_A_ArenaTimber_{side}',(x-side*2,y+4,floor_z(x-side*2,y+4)),(4,.8,.7),col,material('PL_Bark'),k)
            elif k=='B':
                box(f'PL_B_ServicePlinth_{side}',(x,y,z-.1),(4.5,3,1),col,material('PL_Concrete'),k)
                box(f'PL_B_CableMachinery_{side}',(x,y,z+.9),(2.4,1.8,1.7),col,material('PL_ServiceRed'),k)
                box(f'PL_B_CableUpright_{side}',(x+side*2,y,z-.1),(.4,.5,5.5),col,material('PL_Metal'),k)
            else:
                box(f'PL_C_SpeakerBase_{side}',(x,y,z-.1),(2.2,1.8,.5),col,material('PL_Concrete'),k)
                box(f'PL_C_SpeakerStack_{side}',(x,y,z+.4),(1.5,1.2,2.6),col,material('PL_Metal'),k)
                # Emissive geometry reuses the rave material; no additional light.
                box(f'PL_C_RaveColumn_{side}',(x+side*1.8,y,z),(.25,.3,3.8),col,material('PL_RaveEmission'),k)
    scene['arena_landmarks']=True

# Pads support skewed ribbon endpoints, including lateral lane samples.
for key in ('Safe','A'):
    name='PL_Travel_Join_'+key
    if bpy.data.objects.get(name):continue
    x,y=layout['paths'][key][0];radius=layout['widths'][key]*.5+1.5
    def height(y):
        old_y=0
        for (a,c),(b,d) in zip(layout['coordinate_mapping']['y_knots'],layout['coordinate_mapping']['y_knots'][1:]):
            if y<=d:old_y=a+(y-c)*(b-a)/(d-c);break
        return (36 if old_y<=90 else 36-(old_y-90)*28/210)+.025
    verts=[(x,y,height(y))]
    for i in range(16):
        a=i*math.tau/16;xx=x+radius*math.cos(a);yy=y+radius*math.sin(a)
        verts.append((xx,yy,height(yy)))
    col=bpy.data.collections['PL_Shared_Bellevue' if key=='Safe' else 'PL_Route_A']
    o=mesh_object(name,verts,[(0,i+1,(i+1)%16+1) for i in range(16)],col,material('PL_Path'))
    guide(o,'shared' if key=='Safe' else key,'STATIC_SURFACE')

# Keep the coarse scenic underlay below even the lowest arena triangles.
for o in bpy.data.collections['PL_Terrain_Chunks'].objects:
    if o.type!='MESH':continue
    for v in o.data.vertices:
        for cx,cy,w,d in layout['pockets'].values():
            if abs(v.co.x-cx)<=w/2+8 and cy-8<=v.co.y<=cy+d+8:
                v.co.z=min(v.co.z,5.5)

# Refresh bounds after adding the new source guides.
guides=[]
for o in collision.objects:
    if o.type!='MESH':continue
    verts=[o.matrix_world@v.co for v in o.data.vertices]
    guides.append(dict(name=o.name,route=o.get('route','shared'),primitive=o.get('primitive','REFIT_BOX'),
        dynamic_gate=bool(o.get('dynamic_gate')),vertex_count=len(verts),
        bounds_min=[min(v[i] for v in verts) for i in range(3)],bounds_max=[max(v[i] for v in verts) for i in range(3)]))
(OUT/'collision-guides.json').write_text(json.dumps(guides,indent=2)+'\n')
layout['stats']['collider_guides']=len(guides)
layout['stats'].pop('primitive_collider_guides',None)
(OUT/'layout.json').write_text(json.dumps(layout,indent=2)+'\n')
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Source/Pohorje_Large.blend'))
