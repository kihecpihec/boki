"""Run in Blender's Python console to rebuild the isolated Pohorje first-pass source."""
import bpy
import math
import random
from pathlib import Path
from mathutils import Vector

OUT = Path('/home/ismarm/GAMEJAM/boki/output/pohorje-build-v1')
OUT.mkdir(parents=True, exist_ok=True)
if bpy.data.scenes.get('Pohorje_FirstPass'):
    raise RuntimeError('Pohorje_FirstPass already exists; preserve it before rebuilding.')
scene = bpy.data.scenes.new('Pohorje_FirstPass')
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
random.seed(24)

def collection(name):
    c = bpy.data.collections.new(name)
    scene.collection.children.link(c)
    return c

kit = collection('PH01_Module_Library')
layout = collection('PH01_Route_A_Study')
markers = collection('PH01_Gameplay_Guides_NOT_EXPORT')
stage = collection('PH01_Presentation_NOT_EXPORT')

def material(name, color):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    n = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    n.inputs['Base Color'].default_value = (*color, 1)
    n.inputs['Roughness'].default_value = .95
    return m

earth = material('PH01_Earth', (.18,.25,.27))
stone = material('PH01_Stone', (.27,.35,.40))
needles = material('PH01_Needles', (.045,.12,.13))
needle_light = material('PH01_Needles_Light', (.075,.18,.18))
wood = material('PH01_Wood', (.26,.19,.13))
pathmat = material('PH01_Path', (.38,.46,.46))
signmat = material('PH01_Sign', (.66,.79,.35))

def mesh(name, verts, faces, mat, coll=kit):
    data = bpy.data.meshes.new(name)
    data.from_pydata(verts, [], faces)
    data.update()
    o = bpy.data.objects.new(name, data)
    coll.objects.link(o)
    o.data.materials.append(mat)
    return o

def box(name, size, mat, coll=kit, offset=(0,0,0)):
    x,y,z = size
    a,b,c = offset
    v=[(a+i*x,b+j*y,c+k*z) for k in (0,1) for j in (0,1) for i in (0,1)]
    return mesh(name,v,[(0,2,3,1),(4,5,7,6),(0,1,5,4),(2,6,7,3),(0,4,6,2),(1,3,7,5)],mat,coll)

def wedge(name, width, run, rise, thickness, mat):
    v=[(0,0,-thickness),(width,0,-thickness),(width,run,-thickness),(0,run,-thickness),
       (0,0,0),(width,0,0),(width,run,rise),(0,run,rise)]
    return mesh(name,v,[(0,3,2,1),(4,5,6),(4,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],mat)

tile = box('ENV_F_PathTile_4m',(4,4,.3),pathmat,offset=(0,0,-.3))
ramp = wedge('ENV_SH_Ramp_4m',4,4,.5,.2,pathmat)
bank = mesh('ENV_F_BankWedge',[(0,0,0),(4,0,0),(0,2,0),(4,2,0),(0,2,2),(4,2,2)],
    [(0,2,3,1),(0,1,5,4),(2,4,5,3),(0,4,2),(1,3,5)],stone)

def ringmesh(name, rings, segments, mats, phase=0):
    verts=[]
    for z,rx,ry in rings:
        for i in range(segments):
            t=2*math.pi*i/segments+phase
            verts.append((rx*math.cos(t),ry*math.sin(t),z))
    faces=[tuple(reversed(range(segments)))]
    for j in range(len(rings)-1):
        for i in range(segments):
            a=j*segments+i;b=j*segments+(i+1)%segments
            faces.append((a,b,b+segments,a+segments))
    faces.append(tuple((len(rings)-1)*segments+i for i in range(segments)))
    o=mesh(name,verts,faces,mats[0])
    for mat in mats[1:]:o.data.materials.append(mat)
    for p in o.data.polygons:p.material_index=p.index%len(mats)
    return o

trunk=ringmesh('Spruce_Trunk',[(0,.17,.17),(2,.12,.12)],6,[wood])
parts=[trunk]
for z,r,h in [(1,1.25,2.7),(2.1,1.05,2.6),(3.3,.8,2),(4.35,.5,1.65)]:
    parts.append(ringmesh('Spruce_Crown',[(z,r,r),(z+.18,r*.9,r*.9),(z+h,.015,.015)],7,[needles,needle_light],.2))
bpy.ops.object.select_all(action='DESELECT')
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=trunk
bpy.ops.object.join()
tree=trunk;tree.name='ENV_F_Spruce'
rock=ringmesh('ENV_F_Rock',[(0,.8,.55),(.45,1,.75),(1.18,.65,.5),(1.5,.2,.18)],7,[stone,earth],.3)
log=ringmesh('ENV_F_Log',[(0,.35,.35),(3,.35,.35)],8,[wood])
# Rotate mesh data so unit object scale and ground-base origin survive export.
from mathutils import Matrix
log.data.transform(Matrix.Translation((0,0,.35)) @ Matrix.Rotation(math.pi/2,4,'Y'))

modules=[tile,ramp,bank,tree,rock,log]
for o in modules:
    o['authoring_status']='First pass; movement and Unity import validation pending'
    o['collision_hint']={'ENV_F_Spruce':'trunk capsule only','ENV_F_Rock':'one or two boxes',
        'ENV_F_Log':'capsule or box','ENV_F_BankWedge':'simple boundary volume; test bypass',
        'ENV_SH_Ramp_4m':'smooth wedge surface','ENV_F_PathTile_4m':'box with top on finished floor'}[o.name]
    o['origin_contract']='ground base' if o in [tree,rock,log] else 'finished floor corner'

def instance(src,name,pos,angle=0):
    o=bpy.data.objects.new(name,src.data)
    layout.objects.link(o)
    o.location=pos;o.rotation_euler.z=angle
    return o

# Seam-aligned traversal study: safe 8 m start, 4 m rise, 8 m fork,
# short A approach, 12 x 12 m clearing, and an open exit stub.
for y in (0,4):
    for x in (-4,0):instance(tile,'Start_Path',(x,y,0))
instance(ramp,'Ramp_Test',(-2,8,0))
for y in (12,16):
    for x in (-4,0):instance(tile,'Fork_Pocket',(x,y,.5))
for y in (20,24,28):instance(tile,'A_Approach',(-2,y,.5))
for y in (32,36,40):
    for x in (-6,-2,2):instance(tile,'A_Combat_Clearing',(x,y,.5))
for y in (44,48):instance(tile,'Shared_Exit_Stub',(-2,y,.5))

for y in range(0,52,4):
    width=7.8 if 32<=y<=40 else (5.7 if y<8 or 12<=y<20 else 4)
    level=0 if y<8 else .5
    for side in (-1,1):
        for j in range(2):
            x=side*(width+1.3+j*2.1+random.uniform(0,.8))
            instance(tree,'Spruce_Dressing',(x,y+random.uniform(-.8,.8),level),random.random()*6.28)
        instance(rock,'Rock_Dressing',(side*(width+.5),y+.4,level),random.random()*6.28)
# Banks remain outside the riding envelope; this open study is not branch isolation.
for y in (20,24,28):
    instance(bank,'Bank_Right',(2.3,y,.5),-math.pi/2)
    instance(bank,'Bank_Left',(-2.3,y+4,.5),math.pi/2)

def marker(name, pos, radius=.6):
    o=bpy.data.objects.new(name,None);markers.objects.link(o)
    o.location=pos;o.empty_display_size=radius
    o['status']='Placement proposal only; no runtime bindings'
    return o

marker('Bellevue_SafeStart',(0,2,0))
marker('Fork_A_Selection_Proposal',(0,20,.5))
marker('Future_B_Blocked_Stub',(4,16,.5))
marker('Future_C_Blocked_Stub',(-4,16,.5))
marker('A_CombatSpace_Center',(0,38,.5),6)
for i,p in enumerate([(-4,34,.5),(4,38,.5),(-4,42,.5)],1):marker('A_Charge_B'+str(i),p)
for i,p in enumerate([(-5,33,.5),(5,41,.5),(-5,41,.5)],1):
    marker('A_Spawn_Candidate_'+str(i),p)
    marker('A_Guilt_Candidate_'+str(i),(p[0],p[1]+.5,p[2]))
marker('Shared_Exit_Proposal',(0,50,.5))
marker('Camera_Clearance_Test',(0,15,2.3),1.8)

# Display library to the side, separate from the layout and export at local origin.
for i,o in enumerate(modules):o.location=(20+(i%3)*7,7+(i//3)*10,0)
cal=box('Calibration_1m',(1,1,1),signmat,kit)
cal.location=(20,0,0);modules.append(cal)

camera_data=bpy.data.cameras.new('PH01_Camera')
cam=bpy.data.objects.new('PH01_Camera',camera_data);stage.objects.link(cam)
cam.location=(43,-32,40)
cam.rotation_euler=(Vector((2,24,0))-cam.location).to_track_quat('-Z','Y').to_euler()
camera_data.type='ORTHO';camera_data.ortho_scale=69
scene.camera=cam
ld=bpy.data.lights.new('PH01_Sun','SUN');ld.energy=2.5;ld.angle=.25
sun=bpy.data.objects.new('PH01_Sun',ld);stage.objects.link(sun)
sun.rotation_euler=(.5,-.4,-.5)
scene.world=bpy.data.worlds.new('PH01_World');scene.world.color=(.13,.18,.23)
scene.render.resolution_x=1200;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        area.spaces.active.region_3d.view_perspective='CAMERA'
        area.spaces.active.clip_end=500

# Only library render meshes export; guides/camera/layout stay in the .blend.
export=OUT/'Export';export.mkdir(exist_ok=True)
report=[]
for o in modules:
    bpy.ops.object.select_all(action='DESELECT')
    o.select_set(True);bpy.context.view_layer.objects.active=o
    pos=o.location.copy();o.location=(0,0,0)
    try:
        bpy.ops.export_scene.fbx(filepath=str(export/(o.name+'.fbx')),use_selection=True,
            object_types={'MESH'},global_scale=1,apply_unit_scale=True,
            axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False)
    finally:o.location=pos
    o.data.calc_loop_triangles()
    report.append({'name':o.name,'triangles':len(o.data.loop_triangles),
        'blender_dimensions_m':list(o.dimensions),'scale':list(o.scale)})
import json
(OUT/'module-report.json').write_text(json.dumps(report,indent=2)+'\n')
bpy.ops.object.select_all(action='DESELECT')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'ENV_Pohorje_FirstPass.blend'))
print(json.dumps(report,indent=2))
