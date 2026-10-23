"""Build the Pohorje art blockout inside the v2 Blender file. No Unity edits."""
import bpy, math, random, json, shutil, bmesh
from pathlib import Path
from mathutils import Vector

ROOT=Path('/home/ismarm/GAMEJAM/boki')
OUT=ROOT/'output/pohorje-build-v3'
for folder in ('Export','Textures','Previews'):(OUT/folder).mkdir(parents=True,exist_ok=True)
if bpy.data.scenes.get('Pohorje_World'):
    raise RuntimeError('World scene already exists; preserve live edits before rebuilding.')
scene=bpy.data.scenes.new('Pohorje_World');bpy.context.window.scene=scene
scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
rng=random.Random(307)
def collection(name):
    c=bpy.data.collections.new(name);scene.collection.children.link(c);return c
start=collection('PH03_SharedStart_Bellevue')
fork=collection('PH03_SharedFork')
routes={k:collection('PH03_Route_'+k) for k in 'ABC'}
end=collection('PH03_SharedEnd_Overlook')
scenery=collection('PH03_Mountain_and_Forest')
guides=collection('PH03_GameplayGuides_NOT_EXPORT')
library=collection('PH03_ModuleLibrary_NOT_RENDERED')
presentation=collection('PH03_Presentation_NOT_EXPORT')
library.hide_render=True
mats={k:bpy.data.materials['PH02_'+v] for k,v in
    [('dirt','Dirt'),('rock','Stone'),('wood','Bark'),('tree','Needles'),('soil','ForestFloor')]}
def plain(name,color,emission=0):
    m=bpy.data.materials.new('PH03_'+name);m.use_nodes=True;m.diffuse_color=(*color,1)
    p=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    p.inputs['Base Color'].default_value=(*color,1);p.inputs['Roughness'].default_value=.95
    if emission:
        p.inputs['Emission Color'].default_value=(*color,1);p.inputs['Emission Strength'].default_value=emission
    return m
mats.update(concrete=plain('WeatheredConcrete',(.19,.21,.20)),metal=plain('OldMetal',(.085,.10,.095)),
    red=plain('FadedRed',(.25,.095,.065)),purple=plain('RaveStrip',(.32,.065,.60),2),
    warm=plain('LampFace',(.8,.43,.16),2),sign=plain('SignPaint',(.52,.52,.36)),
    paper=plain('OldPoster',(.33,.30,.23)),charge=plain('ChargeCue',(.24,.50,.42),.7),
    skyline=plain('DistantCity',(.09,.12,.14)))
concrete_bs=next(n for n in mats['concrete'].node_tree.nodes if n.type=='BSDF_PRINCIPLED')
concrete_tex=mats['concrete'].node_tree.nodes.new('ShaderNodeTexImage')
concrete_tex.image=bpy.data.images['PH02_Stone'];concrete_tex.interpolation='Closest'
mats['concrete'].node_tree.links.new(concrete_tex.outputs['Color'],concrete_bs.inputs['Base Color'])

def uv(o):
    layer=o.data.uv_layers.new(name='UVMap')
    for p in o.data.polygons:
        axis=max(range(3),key=lambda a:abs(p.normal[a]));axes=[a for a in range(3) if a!=axis]
        for li in p.loop_indices:
            v=o.data.vertices[o.data.loops[li].vertex_index].co
            layer.data[li].uv=(v[axes[0]]/2,v[axes[1]]/2)
def mesh(name,verts,faces,mat,c):
    d=bpy.data.meshes.new(name);d.from_pydata(verts,[],faces);d.update()
    o=bpy.data.objects.new(name,d);c.objects.link(o);d.materials.append(mat);uv(o);return o
def box(name,size,pos,mat,c):
    x,y,z=size
    v=[(i*x,j*y,k*z) for k in (0,1) for j in (0,1) for i in (0,1)]
    o=mesh(name,v,[(0,2,3,1),(4,5,7,6),(0,1,5,4),(2,6,7,3),(0,4,6,2),(1,3,7,5)],mat,c)
    o.location=pos;return o
def marker(name,pos,kind,route='shared'):
    o=bpy.data.objects.new(name,None);guides.objects.link(o);o.location=pos;o.empty_display_size=.65
    o['kind']=kind;o['route']=route;o['status']='Authoring marker; runtime binding pending'
    return o
def height(y):
    if y<=22:return 8
    if y<64:return 8-(y-22)*6/42
    if y<=82:return 2
    return max(0,2-(y-82)*2/22)
def ground(x,y):return height(y)-.09
def beam(name,a,b,width,mat,c):
    a,b=Vector(a),Vector(b);o=box(name,(width,width,(b-a).length),(-width/2,-width/2,0),mat,c)
    # Geometry centered around its longitudinal axis; position is at first endpoint.
    o.data.transform(__import__('mathutils').Matrix.Translation((-width/2,-width/2,0)))
    o.location=a;o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();return o

sources={}
for key in ('Spruce','Rock','Log','PathTile_4m'):
    old=bpy.data.objects['PH02_ENV_F_'+key]
    o=bpy.data.objects.new('PH03_ENV_F_'+key,old.data.copy());library.objects.link(o);sources[key]=o
def instance(key,name,pos,c,scale=(1,1,1),angle=0):
    o=bpy.data.objects.new(name,sources[key].data);c.objects.link(o);o.location=pos
    o.scale=scale;o.rotation_euler.z=angle;return o

# Continuous terrain shell, gentle grade with raised nonplayable route dividers.
xs=[-65,-54,-44,-32,-21,-16,-12,0,12,16,21,32,44,54,65]
ys=list(range(-12,125,4));vs=[]
for y in ys:
    for x in xs:
        ridge=3.7 if abs(x)==16 and 28<=y<=88 else 0
        outer=2 if abs(x)>=54 else 0
        vs.append((x,y,ground(x,y)+ridge+outer))
fs=[]
for j in range(len(ys)-1):
    for i in range(len(xs)-1):
        a=j*len(xs)+i;fs.append((a,a+1,a+1+len(xs),a+len(xs)))
terrain=mesh('PH03_Scenic_MountainShell',vs,fs,mats['soil'],scenery)
terrain['collision']='Scenic only; primitive boundary volumes required in Unity'

def ribbon(name,points,width,c,material=None):
    verts=[]
    for i,(x,y) in enumerate(points):
        before=Vector(points[max(0,i-1)]);after=Vector(points[min(len(points)-1,i+1)])
        tangent=(after-before).normalized();normal=Vector((-tangent.y,tangent.x))
        for sign in (-1,1):verts.append((x+normal.x*width/2*sign,y+normal.y*width/2*sign,height(y)))
    faces=[(2*i,2*i+1,2*i+3,2*i+2) for i in range(len(points)-1)]
    # Reverse winding: ribbon vertices run right-to-left across forward travel.
    faces=[tuple(reversed(f)) for f in faces]
    o=mesh(name,verts,faces,material or mats['dirt'],c)
    o['collision']='Smooth travel surface; validate seams and use only necessary mesh collision'
    return o
def pad(name,x,y,w,d,c,mat=None):
    return box(name,(w,d,.22),(x-w/2,y,height(y)-.22),mat or mats['dirt'],c)

pad('Bellevue_IntroPlatform',0,-8,12,12,start)
ribbon('Shared_SafeApproach',[(0,4),(0,10),(0,16)],6,start)
pad('Fork_TurningPocket',0,12,12,10,fork)
# Fan out across a broad shared fork; entries sit beyond the shared platform.
for k,x in [('A',-32),('B',0),('C',32)]:
    ribbon(k+'_SharedForkApproach',[(0,22),(x/2,24),(x,28)],4 if k!='B' else 5,fork)
points={
 'A':[(-32,28),(-32,34),(-36,40),(-29,46),(-35,52),(-32,58),(-32,64)],
 'B':[(0,28),(0,36),(2,44),(-1,52),(0,58),(0,64)],
 'C':[(32,28),(32,40),(32,52),(32,64)]}
for k in 'ABC':
    ribbon(k+'_TraversalSurface',points[k],5 if k=='B' else 4,routes[k],mats['concrete'] if k=='C' else None)
    x={'A':-32,'B':0,'C':32}[k]
    pad(k+'_CombatPocket_20x18',x,64,20,18,routes[k],mats['concrete'] if k=='C' else None)
    ribbon(k+'_ExitApproach',[(x,82),(x,90)],4,routes[k])
    ribbon(k+'_SharedMerge',[(x,90),(x/2,99),(0,106)],4,end)
pad('Shared_CityOverlook',0,104,14,12,end)

def label(text,pos,c,size=.55,mat=None):
    d=bpy.data.curves.new('PH03_Label','FONT');d.body=text;d.size=size;d.extrude=.002
    o=bpy.data.objects.new('Sign_'+text,d);c.objects.link(o);o.location=pos;o.rotation_euler=(math.pi/2,0,0)
    d.materials.append(mat or mats['sign'])
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
    bpy.ops.object.convert(target='MESH')
    return bpy.context.object
def fence(x,y,z,length,c):
    for dx in (0,length):box('FencePost',(.12,.15,1.1),(x+dx,y,z),mats['wood'],c)
    for h in (.4,.8):box('FenceRail',(length,.12,.12),(x,y,z+h),mats['wood'],c)
for x in (-6,2):fence(x,-8,8,4,start)
for x in (-7,3):fence(x,115,0,4,end)
box('Bellevue_BenchSeat',(3,.5,.18),(-4,-3,8.5),mats['wood'],start)
for x in (-3.8,-1.4):box('BenchLeg',(.15,.4,.5),(x,-3,8),mats['wood'],start)
box('BenchBack',(3,.12,.5),(-4,-2.55,8.7),mats['wood'],start)
box('Bellevue_Bottle',(.08,.08,.28),(-1,-1,8),mats['paper'],start)
for name,pos in [('Intro_LowCamera',(0,-4,8.2)),('Intro_Watch',(0,-3,9.5)),('PlayerStart',(0,2,8)),('Intro_Scooter',(.7,-1,8)),('Intro_GuiltSilhouette',(-9,10,9))]:marker(name,pos,'intro')
marker('ExitForest',(0,112,0),'scene transition / next-run discovery')
label('MARIBOR',(3,110,.8),end,.7)

# Front gates and opaque rear leaves are independent geometry, never triggers.
for k,x in [('A',-32),('B',0),('C',32)]:
    for where,y in [('Front',30),('Rear',89)]:
        z=height(y);c=routes[k]
        for dx in (-2.45,2.15):box(k+'_'+where+'_GatePost',(.3,.4,4.2),(x+dx,y,z),mats['metal'],c)
        box(k+'_'+where+'_GateHeader',(4.9,.4,.3),(x-2.45,y,z+4.2),mats['metal'],c)
        leaf=box(k+'_'+where+'_OpaqueSeal',(4.3,.18,4.2),(x-2.15,y+.1,z),mats['metal'],c)
        leaf['role']='Separate blocker art; bind collision and render state in Unity'
        # Presentation depicts A selected; source retains all branch interiors for editing.
        leaf.hide_render=k=='A';leaf.hide_viewport=k=='A'
        marker(k+'_'+where+'_BlockerBinding',(x,y,z),'gate blocker',k)
    marker('F_'+k+'_Selection',(x,31,height(31)),'selection trigger',k)
    box(k+'_SignBoard',(3,.15,1),(x+3,28,height(28)+1.4),mats['wood'],fork)
    label({'A':'A  FOREST','B':'B  SERVICE','C':'C  RAVE TUNNEL'}[k],(x+3.1,27.98,height(28)+1.7),fork,.32)

# Shared reusable gondola/cart/plank kit; geometry is static.
poleparts=[]
poleparts.append(box('PoleShaft',(.38,.5,8),(-.19,-.25,0),mats['metal'],library))
poleparts.append(box('PoleCrossbar',(3,.5,.35),(-1.5,-.25,7.6),mats['metal'],library))
poleparts.append(box('PoleBase',(1,.8,.4),(-.5,-.4,0),mats['concrete'],library))
def join_parts(parts,name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts:o.select_set(True)
    bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();parts[0].name=name
    o=parts[0]
    o.data.transform(__import__('mathutils').Matrix.Translation(o.location));o.location=(0,0,0)
    return o
sources['GondolaPole']=join_parts(poleparts,'PH03_ENV_F_GondolaPole')
sources['GondolaCabin']=join_parts([
    box('CabinBody',(1.5,1.5,1.4),(-.75,-.75,-2),mats['red'],library),
    box('CabinWindow',(1.4,1.4,.5),(-.7,-.7,-.6),mats['metal'],library),
    box('CabinHanger',(.12,.12,.6),(-.06,-.06,-.1),mats['metal'],library)],'PH03_ENV_F_GondolaCabin')
sources['MaintenanceCart']=join_parts([
    box('CartBody',(1.4,2,.65),(-.7,-1,.35),mats['red'],library),
    box('CartRail',(1.4,.1,.3),(-.7,.85,1),mats['metal'],library),
    box('CartAxle',(1.5,.25,.3),(-.75,-.7,0),mats['metal'],library),
    box('CartAxle',(1.5,.25,.3),(-.75,.45,0),mats['metal'],library)],'PH03_ENV_F_MaintenanceCart')
planks=[]
for i in range(10):
    if i==5:continue
    planks.append(box('Plank',(3,.34,.12),(-1.5,i*.4,0),mats['wood'],library))
sources['BrokenPlanks']=join_parts(planks,'PH03_ENV_F_BrokenPlanks')
for y in (36,52,76):
    instance('GondolaPole','Static_GondolaPole',(5,y,height(y)),routes['B'])
    if y<76:instance('GondolaCabin','Static_GondolaCabin',(5,y+5,height(y)+7),routes['B'])
beam('Static_GondolaCable',(5,30,height(30)+8),(5,84,height(84)+8),.035,mats['metal'],routes['B'])
cart=instance('MaintenanceCart','Service_Cart_Overturned',(-1.4,40,height(40)+.7),routes['B'],angle=.6)
cart.rotation_euler.y=math.pi/2
for y in (46,54):
    o=instance('BrokenPlanks','BrokenPlanks_Test',(0,y,height(y)+.015),routes['B'])
    o.rotation_euler.x=math.atan(-6/42)
    marker('B_Jump_Test_'+str(y),(0,y+2,height(y+2)),'Gap dimensions pending real-controller testing','B')
marker('B_Watcher_Gondola',(5,57,height(52)+6),'authored ranged enemy anchor','B')

# C tunnel: straight 3.6 m clear lane, removable roof, widened room.
roofs=collection('PH03_C_RemovableRoofs')
for y in range(32,64,4):
    z=height(y)
    for x in (29.8,33.8):
        wall=box('C_TunnelWall',(.4,4,4.7),(x,y,z-1),mats['concrete'],routes['C'])
    roof=box('C_TunnelRoof',(4.4,4,.22),(29.8,y,z+3.7),mats['concrete'],roofs)
    roof.rotation_euler.x=math.atan(-6/42)
    strip=box('C_PurpleStrip',(.05,.08,1.7),(33.77,y+1,z+.8),mats['purple'],routes['C'])
    box('C_OldRavePoster',(.025,.65,.9),(30.21,y+1,z+1.1),mats['paper'],routes['C'])
    if y in (36,52):label('RAVE',(30.24,y+1.02,z+1.3),routes['C'],.16,mats['paper'])
box('C_RoomWall',(.3,18,4.2),(21.7,64,1.8),mats['concrete'],routes['C'])
for y,length in [(64,6),(73.5,8.5)]:box('C_RoomWall_GrateSide',(.3,length,4.2),(42,y,1.8),mats['concrete'],routes['C'])
box('C_GrateHeader',(.3,3.5,.4),(42,70,5.6),mats['concrete'],routes['C'])
for y in (64,81.7):
    for x in (22,34):box('C_RoomEndWall',(8,.3,4.2),(x,y,1.8),mats['concrete'],routes['C'])
box('C_RoomRoof',(20.6,18,.2),(21.7,64,6),mats['concrete'],roofs)
for y in (67,73,79):
    box('C_RoomEmissive',(.04,.08,2),(41.96,y,3),mats['purple'],routes['C'])
for i in range(12):box('C_GrateBar',(.055,3.5,.055),(41.7,70,2.2+i*.28),mats['metal'],routes['C'])
marker('C_Grate_GuiltReveal',(44,72,2),'authored grate event','C')
marker('C_GateStrike',(42,74,2),'authored metal gate strike','C')
for i,x in enumerate((29,35)):marker('C_StrongerEnemy_'+str(i+1),(x,69,2),'authored regular enemy','C')

# Combat pocket placements come directly from the companion spatial proposal.
spec=json.loads((ROOT/'output/gameplay-mocks-v2/combat-space-plans.json').read_text())['levels'][0]
for r in spec['routes']:
    k=r['id'][-1];cx={'A':-32,'B':0,'C':32}[k]
    marker(k+'_CombatSpace_Center',(cx,73,2),'20 x 18 m proposed occupancy',k)
    for category,key in [('Charge','chargesXZ'),('SpawnCandidate','spawnCandidatesXZ'),('GuiltCandidate','guiltCandidatesXZ')]:
        for i,(x,y) in enumerate(r[key],1):
            pos=(cx-10+x,64+y,2)
            marker(k+'_'+category+'_'+str(i),pos,category,k)
            if category=='Charge':
                box(k+'_ChargeHousing_'+str(i),(.45,.45,.7),(pos[0]-.225,pos[1]-.225,2),mats['metal'],routes[k])
                box(k+'_ChargeCue_'+str(i),(.3,.04,.35),(pos[0]-.15,pos[1]-.25,2.2),mats['charge'],routes[k])
    marker(k+'_RearSealControl',(cx,89,height(89)),'Open only for selected branch',k)
marker('A_AuthoredGuiltBehind',(-32,79,2),'authored event; no ambient overlap','A')

# Sparse forest grouped around routes; reserve the whole combat pockets.
for k in 'AB':
    for x,y in points[k][1:-1]:
        for side in (-1,1):
            for j in range(3):
                tx=x+side*(4.2+j*2.2+rng.random());ty=y+rng.uniform(-2,2)
                instance('Spruce','Forest_Spruce',(tx,ty,ground(tx,ty)),routes[k],(1,1,rng.uniform(.85,1.3)),rng.random()*6.28)
            instance('Rock','Forest_Rock',(x+side*3.7,y,ground(x,y)),routes[k],angle=rng.random()*6.28)
for cx in (-32,0):
    for y in (65,70,76,81):
        for side in (-1,1):
            x=cx+side*(12+rng.random()*2)
            instance('Spruce','Clearing_PerimeterSpruce',(x,y,ground(x,y)),scenery,(1,1,rng.uniform(.9,1.4)),rng.random()*6.28)
for y in range(-4,117,5):
    for x in (-54,-48,-17,17,49,55):
        tx=x+rng.uniform(-1,1);ty=y+rng.uniform(-1,1)
        if 20<y<29 or 91<y<105:continue
        z=ground(tx,ty)+(3.6 if abs(x)==17 and 28<=y<=88 else 0)
        instance('Spruce','Mountain_BackdropSpruce',(tx,ty,z),scenery,(1,1,rng.uniform(.9,1.6)),rng.random()*6.28)
for x,y in [(-37,36),(-25,49),(-38,58)]:instance('Log','A_Slalom_Log',(x,y,ground(x,y)),routes['A'],angle=.4)

# Keep decorative tree/rock envelopes outside the whole polyline, including bends.
for k in 'AB':
    for o in routes[k].objects:
        if not o.name.startswith(('Forest_Spruce','Forest_Rock')):continue
        p=Vector(o.location[:2]);best=None
        for aa,bb in zip(points[k],points[k][1:]):
            a,b=Vector(aa),Vector(bb);v=b-a
            q=a+max(0,min(1,(p-a).dot(v)/v.length_squared))*v
            if best is None or (p-q).length<best[0]:best=((p-q).length,q)
        minimum=(2.5 if k=='B' else 2)+1.35
        if best[0]<minimum:
            away=p-best[1]
            if away.length<.001:away=Vector((1,0))
            p=best[1]+away.normalized()*minimum
            o.location=(p.x,p.y,ground(p.x,p.y))

# Distant skyline only: no city interiors or additional playable route.
for i in range(36):
    x=-48+i*2.6;h=rng.uniform(1,5)
    box('Maribor_Skyline',(rng.uniform(1.4,2.5),.15,h),(x,143,-7),mats['skyline'],scenery)
for c,x,y in [(start,-4,1),(routes['A'],-35,58),(end,5,110)]:
    z=height(y);box('LampPost',(.13,.13,3),(x,y,z),mats['metal'],c)
    box('LampFace',(.32,.3,.3),(x-.1,y-.1,z+2.8),mats['warm'],c)

# Present A selected for riding view; all source routes remain available to edit.
world=bpy.data.worlds.new('PH03_OvercastWorld');world.use_nodes=True;scene.world=world
bg=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND')
bg.inputs['Color'].default_value=(.19,.23,.27,1);bg.inputs['Strength'].default_value=.65
ld=bpy.data.lights.new('PH03_Sun','SUN');ld.energy=1.8;ld.color=(.74,.80,.92)
sun=bpy.data.objects.new('PH03_Sun',ld);presentation.objects.link(sun);sun.rotation_euler=(.7,-.4,-.6)
for y in (40,74):
    ld=bpy.data.lights.new('PH03_TunnelLight','POINT');ld.energy=65;ld.color=(.45,.12,.8);ld.shadow_soft_size=1
    o=bpy.data.objects.new('PH03_TunnelLight',ld);presentation.objects.link(o);o.location=(32,y,height(y)+2.5)
cd=bpy.data.cameras.new('PH03_Camera');cam=bpy.data.objects.new('PH03_Camera',cd);presentation.objects.link(cam);scene.camera=cam
scene.render.resolution_x=1400;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
def shot(name,pos,target,ortho=None):
    cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
    cd.type='ORTHO' if ortho else 'PERSP'
    if ortho:cd.ortho_scale=ortho
    else:cd.lens=25
    scene.render.filepath=str(OUT/'Previews'/(name+'.png'));bpy.ops.render.render(write_still=True)
shot('01-world-overview',(134,-125,142),(0,55,2),165)
shot('02-bellevue',(1,-7,9.65),(0,24,8.2))
shot('03-forest-A',(-32,33,height(33)+1.65),(-33,53,height(53)+1.4))
shot('04-service-B',(0,34,height(34)+1.65),(1,56,height(56)+2))
shot('05-rave-C',(32,35,height(35)+1.65),(32,59,height(59)+1.5))
# Cutaway is explicitly a presentation view; delivered roof geometry stays separate.
roofs.hide_render=True
shot('06-cutaway-overview',(134,-125,142),(0,55,2),165)
roofs.hide_render=False

report=[]
for key,o in sources.items():
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
    o.data.calc_loop_triangles();bm=bmesh.new();bm.from_mesh(o.data)
    report.append({'module':key,'triangles':len(o.data.loop_triangles),'unit_scale':all(abs(s-1)<1e-6 for s in o.scale),
        'zero_area_faces':sum(f.calc_area()<1e-8 for f in bm.faces),'nonmanifold_edges':sum(not e.is_manifold for e in bm.edges)})
    bm.free()
    bpy.ops.export_scene.fbx(filepath=str(OUT/'Export'/('ENV_F_'+key+'.fbx')),use_selection=True,object_types={'MESH'},
        axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,path_mode='COPY',embed_textures=True)
# Layout delivery is per collection, keeping source branches separate, not one welded mesh.
for c in [start,fork,*routes.values(),end,scenery,roofs]:
    bpy.ops.object.select_all(action='DESELECT')
    hidden=[]
    for o in c.objects:
        if o.type=='MESH':
            hidden.append((o,o.hide_viewport));o.hide_viewport=False;o.select_set(True)
    # Give the exporter temporary unique data to avoid material remap warnings
    # for differently scaled instances. Restore linked source meshes afterward.
    saved=[]
    try:
        for o in list(bpy.context.selected_objects):
            old=o.data;o.data=old.copy();saved.append((o,old,o.data))
        bpy.ops.export_scene.fbx(filepath=str(OUT/'Export'/(c.name+'.fbx')),use_selection=True,object_types={'MESH'},
            axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,path_mode='COPY',embed_textures=True)
    finally:
        for o,old,temp in saved:o.data=old;bpy.data.meshes.remove(temp)
        for o,state in hidden:o.hide_viewport=state
manifest=[{'name':o.name,'position_blender_xyz':list(o.location),'kind':o.get('kind'),'route':o.get('route')} for o in guides.objects]
(OUT/'markers.json').write_text(json.dumps(manifest,indent=2)+'\n')
(OUT/'module-validation.json').write_text(json.dumps(report,indent=2)+'\n')
for p in (ROOT/'output/pohorje-build-v2/Textures').glob('*.png'):shutil.copy2(p,OUT/'Textures'/p.name)
# Restore the useful editing overview without another render.
cam.location=(134,-125,142);cam.rotation_euler=(Vector((0,55,2))-cam.location).to_track_quat('-Z','Y').to_euler();cd.type='ORTHO';cd.ortho_scale=165
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':area.spaces.active.region_3d.view_perspective='CAMERA'
bpy.ops.object.select_all(action='DESELECT')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'ENV_Pohorje_World.blend'))
print(json.dumps({'objects':len(scene.objects),'markers':len(manifest),'modules':report},indent=2))
