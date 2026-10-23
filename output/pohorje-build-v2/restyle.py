"""Run inside the v1 Blender file; creates an independent second-pass scene."""
import bpy
import bmesh
import math
import random
import json
from pathlib import Path
from mathutils import Vector

OUT=Path('/home/ismarm/GAMEJAM/boki/output/pohorje-build-v2')
OUT.mkdir(parents=True,exist_ok=True)
for folder in ('Export','Textures'):(OUT/folder).mkdir(exist_ok=True)
if bpy.data.scenes.get('Pohorje_Restrained'):
    raise RuntimeError('Second-pass scene already exists; refusing to overwrite live work.')
source=bpy.data.scenes['Pohorje_FirstPass']
scene=bpy.data.scenes.new('Pohorje_Restrained')
bpy.context.window.scene=scene
scene.unit_settings.system='METRIC'
scene.unit_settings.scale_length=1
copies={}; meshes={}; collections={}
for oldc in source.collection.children:
    c=bpy.data.collections.new(oldc.name.replace('PH01','PH02'))
    scene.collection.children.link(c);collections[oldc.name]=c
    for old in oldc.objects:
        o=old.copy()
        if old.data:
            if old.type=='MESH':
                if old.data not in meshes:meshes[old.data]=old.data.copy()
                o.data=meshes[old.data]
            else:o.data=old.data.copy()
        c.objects.link(o);copies[old.name]=o
        o.name='PH02_'+old.name

def replace_geometry(o,verts,faces):
    o.data.clear_geometry();o.data.from_pydata(verts,[],faces);o.data.update()

verts=[];faces=[];slots=[]
def rings(levels,n,slot,phase=0):
    start=len(verts)
    for z,r in levels:
        for i in range(n):
            a=i*2*math.pi/n+phase
            verts.append((math.cos(a)*r,math.sin(a)*r,z))
    faces.append(tuple(start+i for i in reversed(range(n))));slots.append(slot)
    for j in range(len(levels)-1):
        for i in range(n):
            a=start+j*n+i;b=start+j*n+(i+1)%n
            faces.append((a,b,b+n,a+n));slots.append(slot)
    faces.append(tuple(start+(len(levels)-1)*n+i for i in range(n)));slots.append(slot)
rings([(0,.14),(3.1,.09)],5,0)
for levels,phase in [([(1.0,1.22),(3.9,.025)],.1), ([(2.65,.94),(5.1,.02)],.45), ([(4.1,.58),(6,.01)],.2)]:
    rings(levels,5,1,phase)
tree=copies['ENV_F_Spruce'];replace_geometry(tree,verts,faces)
rock=copies['ENV_F_Rock']
rv=[]
for z,rx,ry in [(0,.85,.61),(.65,1,.75),(1.5,.48,.4)]:
    for i in range(5):
        a=i*2*math.pi/5+.25;rv.append((rx*math.cos(a),ry*math.sin(a),z))
rf=[(4,3,2,1,0)]
for j in range(2):
    for i in range(5):rf.append((j*5+i,j*5+(i+1)%5,(j+1)*5+(i+1)%5,(j+1)*5+i))
rf.append((10,11,12,13,14));replace_geometry(rock,rv,rf)

def texture_material(name,base,kind):
    rng=random.Random(name);size=128
    image=bpy.data.images.new(name,width=size,height=size)
    pixels=[]
    coarse=[[rng.uniform(-1,1) for x in range(16)] for y in range(16)]
    for y in range(size):
        for x in range(size):
            grain=rng.uniform(-.075,.075)+coarse[y//8][x//8]*.055
            if kind=='bark':grain+=.07*math.sin(x*.7)+.035*math.sin(x*1.7+y*.09)
            if kind=='needles':grain+=.035*math.sin(x*.6+y*.8)
            if kind=='dirt' and rng.random()<.035:grain+=.13
            pixels.extend([max(.005,min(1,c+grain)) for c in base]+[1])
    image.pixels.foreach_set(pixels)
    image.filepath_raw=str(OUT/'Textures'/(name+'.png'));image.file_format='PNG';image.save()
    m=bpy.data.materials.new(name);m.use_nodes=True;m.diffuse_color=(*base,1)
    bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    bs.inputs['Roughness'].default_value=.97
    tex=m.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image;tex.interpolation='Closest'
    m.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
    return m

dirt=texture_material('PH02_Dirt',(.27,.245,.20),'dirt')
stone=texture_material('PH02_Stone',(.255,.27,.26),'stone')
bark=texture_material('PH02_Bark',(.21,.175,.135),'bark')
needles=texture_material('PH02_Needles',(.105,.15,.105),'needles')
soil=texture_material('PH02_ForestFloor',(.145,.16,.12),'dirt')
seen=set()
for name,o in copies.items():
    if o.type!='MESH' or o.data in seen:continue
    seen.add(o.data)
    o.data.materials.clear()
    mats=([bark,needles] if name=='ENV_F_Spruce' else [bark] if name=='ENV_F_Log'
        else [stone] if name in ('ENV_F_Rock','ENV_F_BankWedge') else [dirt])
    for m in mats:o.data.materials.append(m)
    if name=='ENV_F_Spruce':
        for p,slot in zip(o.data.polygons,slots):p.material_index=slot
    else:
        for p in o.data.polygons:p.material_index=0
    uv=o.data.uv_layers.new(name='UVMap')
    for p in o.data.polygons:
        axis=max(range(3),key=lambda a:abs(p.normal[a]))
        axes=[a for a in range(3) if a!=axis]
        for li in p.loop_indices:
            v=o.data.vertices[o.data.loops[li].vertex_index].co
            uv.data[li].uv=(v[axes[0]]/2,v[axes[1]]/2)

# Broad forest floor makes the study read as a place rather than floating tiles.
# Side strips stay outside the original 4 m riding lane and all widened pockets.
layout=collections['PH01_Route_A_Study']
rng=random.Random(91)
for side in (-1,1):
    vs=[]
    for y in range(-4,57,4):
        half=6.05 if 32<=y<=44 else 4.05 if y<=8 or 12<=y<=20 else 2.05
        z=-.035 if y<=8 else .465
        if y==12:z=.465
        for x,dz in [(half,0),(half+3,.1),(half+9,rng.uniform(.2,1))]:
            vs.append((side*x,y,z+dz))
    fs=[]
    for j in range(len(vs)//3-1):
        for i in range(2):
            a=j*3+i
            face=(a,a+1,a+4,a+3)
            fs.append(face if side==1 else tuple(reversed(face)))
    data=bpy.data.meshes.new('PH02_TerrainStrip');data.from_pydata(vs,[],fs);data.update()
    o=bpy.data.objects.new('PH02_Scenic_Terrain_'+str(side),data);layout.objects.link(o)
    data.materials.append(soil)
    uv=data.uv_layers.new(name='UVMap')
    for loop in data.loops:
        v=data.vertices[loop.vertex_index].co;uv.data[loop.index].uv=(v.x/4,v.y/4)
    o['collision']='Scenic terrain only; riding surfaces remain separate; boundaries pending'

# A continuous scenic underlay closes visual holes at changing path widths.
data=bpy.data.meshes.new('PH02_ContinuousForestFloor')
data.from_pydata([(-18,-4,-.06),(18,-4,-.06),(18,57,-.06),(-18,57,-.06)],[],[(0,1,2,3)])
data.update();data.materials.append(soil)
underlay=bpy.data.objects.new('PH02_ContinuousForestFloor',data);layout.objects.link(underlay)
uv=data.uv_layers.new(name='UVMap')
for loop in data.loops:
    v=data.vertices[loop.vertex_index].co;uv.data[loop.index].uv=(v.x/4,v.y/4)
underlay['collision']='Scenic underlay; no gameplay collider'

# Small placement changes break the regimented silhouette without narrowing travel.
for name,o in copies.items():
    if name.startswith('Spruce_Dressing'):
        o.location.y+=rng.uniform(-.9,.9)
        o.rotation_euler.z+=rng.uniform(-.6,.6)
        o.scale=(1,1,rng.uniform(.8,1.13))

sun=copies['PH01_Sun'];sun.data.energy=1.8
sun.data.color=(.74,.80,.92)
sun.rotation_euler=(.7,-.4,-.6)
scene.world=bpy.data.worlds.new('PH02_OvercastWorld');scene.world.use_nodes=True
bg=next(n for n in scene.world.node_tree.nodes if n.type=='BACKGROUND')
bg.inputs['Color'].default_value=(.19,.23,.27,1);bg.inputs['Strength'].default_value=.65
camera=copies['PH01_Camera'];scene.camera=camera
scene.render.resolution_x=1200;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'

modules=['ENV_F_PathTile_4m','ENV_SH_Ramp_4m','ENV_F_BankWedge','ENV_F_Spruce','ENV_F_Rock','ENV_F_Log','Calibration_1m']
report=[]
for name in modules:
    o=copies[name];o.data.calc_loop_triangles()
    bm=bmesh.new();bm.from_mesh(o.data)
    report.append({'module':name,'triangles':len(o.data.loop_triangles),
        'nonmanifold_edges':sum(not e.is_manifold for e in bm.edges),
        'zero_area_faces':sum(f.calc_area()<1e-8 for f in bm.faces),
        'unit_scale':all(abs(s-1)<1e-6 for s in o.scale)})
    bm.free()
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
    pos=o.location.copy();o.location=(0,0,0)
    try:
        bpy.ops.export_scene.fbx(filepath=str(OUT/'Export'/(name+'.fbx')),use_selection=True,
            object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,
            add_leaf_bones=False,path_mode='COPY',embed_textures=True)
    finally:o.location=pos
(OUT/'validation.json').write_text(json.dumps(report,indent=2)+'\n')
for m in [dirt,stone,bark,needles,soil]:
    for n in m.node_tree.nodes:
        if n.type=='TEX_IMAGE':n.image.pack()

bpy.ops.object.select_all(action='DESELECT')
scene.render.filepath=str(OUT/'overview.png');bpy.ops.render.render(write_still=True)
# Eye-level image is the primary style check.
camera.data.type='PERSP';camera.data.lens=28
camera.location=(.1,20,2.25)
camera.rotation_euler=(Vector((0,38,2.0))-camera.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(OUT/'riding-view.png');bpy.ops.render.render(write_still=True)
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':area.spaces.active.region_3d.view_perspective='CAMERA'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'ENV_Pohorje_Restrained.blend'))
print(json.dumps(report,indent=2))
