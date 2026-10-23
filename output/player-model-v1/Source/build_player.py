"""Rebuild with Blender 5.2: blender --background --python build_player.py.
Creates a separate scene. Never edits the game's Unity scenes or prefabs.
"""
import bpy, bmesh, math, random, json, sys
from pathlib import Path
from mathutils import Vector, Quaternion

if not bpy.app.background:
    raise RuntimeError('Run this builder in a separate Blender --background process.')

OUT = Path(__file__).resolve().parents[1]
for name in ('Source', 'Export', 'Textures', 'Previews'):
    (OUT / name).mkdir(exist_ok=True)
scene = bpy.data.scenes.new('Boki_Lowpoly_Studio')
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0
scene.render.engine = 'CYCLES'
scene.cycles.samples = 24
scene.render.resolution_x = 1000
scene.render.resolution_y = 1000
scene.render.resolution_percentage = 100
scene.world = bpy.data.worlds.new('Boki_Studio_World')
scene.world.use_nodes = True
next(n for n in scene.world.node_tree.nodes if n.type == 'BACKGROUND').inputs[0].default_value = (.10,.12,.17,1)
scene.view_settings.view_transform = 'Standard'
collection = bpy.data.collections.new('PLAYER_EXPORT')
scene.collection.children.link(collection)
studio = bpy.data.collections.new('STUDIO_NO_EXPORT')
scene.collection.children.link(studio)

# A single 512px palette/paint atlas. Each 128px tile has a distinct surface.
colors = ['272C33','754CA5','754CA5','CFEF75','BEAA92','D8D5C2','17171D','342820',
          '30363D','11171D','E9E5CD','EF624A','BEAA92','6D477E','8B7770','AAB89C']
rand = random.Random(758)
size = 512
pixels = [0.0]*(size*size*4)
def rgb(h): return tuple(int(h[i:i+2],16)/255 for i in (0,2,4))
def put(x,y,c):
    if 0 <= x < size and 0 <= y < size:
        at = (y*size+x)*4
        pixels[at:at+4] = (*c[:3],1)
def rect(tile,x0,y0,x1,y1,c):
    tx,ty=(tile%4)*128,(tile//4)*128
    c=rgb(c) if isinstance(c,str) else c
    for y in range(int(y0*128),int(y1*128)):
        for x in range(int(x0*128),int(x1*128)): put(tx+x,ty+y,c)
for tile,h in enumerate(colors):
    base=rgb(h)
    for y in range(0,128,4):
        for x in range(0,128,4):
            variation=rand.uniform(-.045,.045)
            if tile in (1,2,7,0): variation *= 1.5
            c=tuple(max(0,min(1,k+variation)) for k in base)
            for yy in range(y,y+4):
                for xx in range(x,x+4): put(tile%4*128+xx,tile//4*128+yy,c)
# Lime V on back/front: large readable shape, no fine modeled cloth wrinkles.
for tile in (1,2):
    for x in range(128):
        line=int(49+abs(x-64)*.64)
        for y in range(line,line+15): put(tile%4*128+x,tile//4*128+y,rgb('CFEF75'))
    for x in (3,4,123,124):
        for y in range(12,105): put(tile%4*128+x,tile//4*128+y,rgb('503471'))
# Front shirt with a rough rave symbol and zipper edges.
rect(1,.39,0,.61,1,'272C33')
rect(1,.375,0,.39,1,'B7A6BA'); rect(1,.61,0,.625,1,'B7A6BA')
for a in [(.45,.48,.49,.55),(.49,.55,.53,.62),(.47,.62,.51,.69),(.51,.69,.56,.76)]: rect(1,*a,'78924E')
# Face painting: tired eyes, eyebrows, stubble and mouth in one pixel tile.
rect(12,.03,.20,.16,.80,'A48D7B'); rect(12,.84,.20,.97,.80,'A48D7B')
rect(12,.15,.48,.43,.62,'93737B'); rect(12,.57,.48,.85,.62,'93737B')
rect(12,.18,.54,.42,.61,'E2D8C2'); rect(12,.58,.54,.82,.61,'E2D8C2')
rect(12,.28,.54,.35,.61,'4C5758'); rect(12,.65,.54,.72,.61,'4C5758')
rect(12,.30,.55,.34,.60,'171A20'); rect(12,.66,.55,.70,.60,'171A20')
rect(12,.17,.67,.42,.72,'342820'); rect(12,.58,.67,.83,.72,'342820')
rect(12,.22,.65,.43,.68,'342820'); rect(12,.57,.65,.78,.68,'342820')
rect(12,.45,.39,.55,.47,'A18A78')
rect(12,.30,.23,.70,.27,'705B57'); rect(12,.36,.27,.64,.29,'AD8E7C')
rect(12,.39,.14,.61,.17,'514940')
for _ in range(180):
    x=rand.randrange(22,106); y=rand.randrange(8,47)
    if y < 18 or x < 35 or x > 93: put(12%4*128+x,12//4*128+y,rgb('8E8170'))
# Watch tile, LCD readout 07:58, seven-segment pixel numerals.
rect(15,.04,.12,.96,.88,'30363D'); rect(15,.13,.25,.87,.75,'AAB89C')
digits={'0':'ab cdef'.replace(' ',''),'7':'abc','5':'afgcd','8':'abcdefg'}
segments={'a':(0,12,6,14),'b':(5,7,7,13),'c':(5,1,7,7),'d':(0,0,6,2),'e':(-1,1,1,7),'f':(-1,7,1,13),'g':(0,6,6,8)}
for char,ox in [('0',24),('7',43),('5',70),('8',89)]:
    for seg in digits[char]:
        a,b,c,d=segments[seg]
        rect(15,(ox+a*1.8)/128,(49+b*1.8)/128,(ox+c*1.8)/128,(49+d*1.8)/128,'25312C')
rect(15,.485,.43,.51,.46,'25312C'); rect(15,.485,.57,.51,.60,'25312C')
image=bpy.data.images.new('T_Boki_BaseColor',width=size,height=size)
image.pixels.foreach_set(pixels)
image.filepath_raw=str(OUT/'Textures/T_Boki_BaseColor.png')
image.file_format='PNG'; image.save(); image.pack()
material=bpy.data.materials.new('MAT_Boki_Atlas'); material.use_nodes=True
nodes=material.node_tree.nodes
shader=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
shader.inputs['Roughness'].default_value=.88
tex=nodes.new('ShaderNodeTexImage'); tex.image=image; tex.interpolation='Closest'
material.node_tree.links.new(tex.outputs['Color'],shader.inputs['Base Color'])

parts=[]; scooter=[]
def uv(tile,u,v): return ((tile%4*128+4+u*120)/512,(tile//4*128+4+v*120)/512)
def mesh(name,verts,faces,tile=0,bone=None,tiles=None,uvs=None,group=None):
    data=bpy.data.meshes.new(name); data.from_pydata(verts,[],faces); data.update()
    obj=bpy.data.objects.new(name,data); collection.objects.link(obj)
    data.materials.append(material)
    layer=data.uv_layers.new(name='Atlas')
    for poly in data.polygons:
        t=tiles[poly.index] if tiles else tile
        for k,li in enumerate(poly.loop_indices):
            u,v=uvs[poly.index][k] if uvs else ((.07,.07),(.93,.07),(.93,.93),(.07,.93))[k%4]
            layer.data[li].uv=uv(t,u,v)
        poly.use_smooth=False
    if bone: obj.vertex_groups.new(name=bone).add(list(range(len(verts))),1,'REPLACE')
    (scooter if group=='scooter' else parts).append(obj)
    return obj
cross=[(-.7,-1),(.7,-1),(1,-.55),(1,.55),(.7,1),(-.7,1),(-1,.55),(-1,-.55)]
def tube(name,rings,tile=0,bone=None,ring_bones=None,torso=False,group=None):
    verts=[]
    for center,rx,ry in rings:
        verts += [(center[0]+a*rx,center[1]+b*ry,center[2]) for a,b in cross]
    faces=[tuple(reversed(range(8)))]; tiles=[tile]; coords=[[(.1,.1)]*8]
    z0,z1=rings[0][0][2],rings[-1][0][2]
    for j in range(len(rings)-1):
        for k in range(8):
            ids=(j*8+k,j*8+(k+1)%8,(j+1)*8+(k+1)%8,(j+1)*8+k)
            faces.append(ids)
            t=(1 if k in (0,1,7) else 2) if torso else tile
            tiles.append(t)
            coords.append([((verts[i][0]/.48+.5), (verts[i][2]-z0)/(z1-z0)) for i in ids] if torso else [(k/8,j/(len(rings)-1)),((k+1)/8,j/(len(rings)-1)),((k+1)/8,(j+1)/(len(rings)-1)),(k/8,(j+1)/(len(rings)-1))])
    faces.append(tuple(range((len(rings)-1)*8,len(rings)*8))); tiles.append(tile); coords.append([(.5,.5)]*8)
    o=mesh(name,verts,faces,tile,bone,tiles,coords,group)
    if ring_bones:
        for i,weights in enumerate(ring_bones):
            for b,w in weights.items():
                g=o.vertex_groups.get(b) or o.vertex_groups.new(name=b)
                g.add(list(range(i*8,i*8+8)),w,'REPLACE')
    return o

def box(name,center,scale,tile=0,bone=None,group=None):
    x,y,z=center; a,b,c=[v/2 for v in scale]
    return mesh(name,[(x+sx*a,y+sy*b,z+sz*c) for sx,sy,sz in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]],[(0,3,2,1),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,6,7)],tile,bone,group=group)
def beam(name,a,b,width,depth,tile,bone=None,group=None):
    mid=(Vector(a)+Vector(b))*.5
    o=box(name,(0,0,0),(width,depth,(Vector(b)-Vector(a)).length),tile,bone,group)
    q=Vector((0,0,1)).rotation_difference(Vector(b)-Vector(a))
    for v in o.data.vertices: v.co=q@v.co+mid
    return o

# Body shell; no redundant torso underneath closed jacket.
tube('M_Boki_Jacket',[((0,0,.86),.17,.105),((0,0,.90),.19,.12),((0,0,1.08),.21,.13),((0,0,1.27),.24,.125),((0,0,1.36),.20,.10),((0,0,1.40),.085,.07)],torso=True,ring_bones=[{'hips':1},{'hips':.6,'spine':.4},{'spine':1},{'chest':1},{'chest':1},{'chest':1}])
tube('Jacket_Ribbed_Hem',[((0,0,.85),.17,.105),((0,0,.89),.18,.11)],13,'hips')
tube('Neck',[((0,0,1.38),.055,.053),((0,0,1.51),.052,.05)],4,'neck')
# Raised collar, separated at the front.
for s in (-1,1):
    o=beam('Collar', (s*.065,0,1.37),(s*.073,.008,1.455),.048,.145,13,'chest')

# Legs with extra joint rings and baggy ankle silhouette.
for s,suffix in [(-1,'L'),(1,'R')]:
    leg=[((s*.095,0,.10),.065,.073),((s*.097,0,.16),.078,.08),((s*.102,.005,.27),.082,.083),((s*.104,-.012,.46),.071,.078),((s*.101,-.014,.50),.072,.08),((s*.093,0,.69),.089,.093),((s*.085,0,.88),.095,.10)]
    tube('Trouser.'+suffix,leg,0,ring_bones=[{'shin.'+suffix:1},{'shin.'+suffix:1},{'shin.'+suffix:1},{'shin.'+suffix:.7,'thigh.'+suffix:.3},{'shin.'+suffix:.3,'thigh.'+suffix:.7},{'thigh.'+suffix:1},{'thigh.'+suffix:1}])
    tube('Trainer.'+suffix,[((s*.097,-.062,.022),.079,.139),((s*.097,-.062,.055),.079,.139),((s*.097,-.056,.105),.072,.126),((s*.097,-.012,.14),.057,.073)],5,'foot.'+suffix)
    box('Sole.'+suffix,(s*.097,-.062,.0125),(.154,.274,.025),14,'foot.'+suffix)
    for i in range(3): box('Lace.'+suffix,(s*.097,-.098+i*.023,.112+i*.004),(.080,.009,.008),10,'foot.'+suffix)
    for i in (-1,1): box('ShoeStripe.'+suffix,(s*.097+i*.075,-.075,.067),(.003,.055,.032),3,'foot.'+suffix)
    arm=[((s*.228,0,1.325),.073,.083),((s*.272,0,1.29),.086,.087),((s*.302,0,1.25),.079,.085),((s*.320,0,1.225),.079,.083),((s*.37,0,1.15),.074,.077),((s*.39,0,1.12),.072,.075),((s*.45,-.004,1.025),.065,.066),((s*.475,-.008,.985),.047,.05)]
    o=tube('Sleeve.'+suffix,arm,13,ring_bones=[{'upper_arm.'+suffix:1}]*4+[{'upper_arm.'+suffix:.6,'lower_arm.'+suffix:.4},{'upper_arm.'+suffix:.3,'lower_arm.'+suffix:.7},{'lower_arm.'+suffix:1},{'lower_arm.'+suffix:1}])
    # Wide lime sleeve stripe follows the shoulder band.
    for p in o.data.polygons:
        if 9 <= p.index <= 24:
            for li in p.loop_indices: o.data.uv_layers.active.data[li].uv=uv(3,.5,.5)
    beam('Cuff.'+suffix,(s*.465,-.008,1.006),(s*.486,-.009,.969),.085,.088,13,'lower_arm.'+suffix)
    beam('Palm.'+suffix,(s*.488,-.01,.97),(s*.519,-.015,.875),.057,.055,4,'hand.'+suffix)
    beam('Thumb.'+suffix,(s*.475,-.039,.942),(s*.48,-.055,.895),.023,.023,4,'hand.'+suffix)
# Watch with visible LCD paint tile, rigidly weighted to left wrist.
box('M_Watch',(-.486,-.005,.967),(.082,.085,.043),6,'hand.L')
# Top face in bind pose; tilt follows the wrist after posing.
mesh('Watch_LCD',[(-.521,-.038,.990),(-.452,-.038,.990),(-.452,.028,.990),(-.521,.028,.990)],[(0,1,2,3)],15,'hand.L')

# Angular head: jaw, cheeks, brow and cranial rings.
head=tube('M_Boki_Head',[((0,0,1.475),.043,.045),((0,-.004,1.50),.065,.064),((0,0,1.555),.086,.077),((0,0,1.62),.089,.081),((0,.004,1.675),.078,.077),((0,.008,1.695),.06,.057)],4,'head')
# Project all forward head polygons into one painted face island.
for p in head.data.polygons:
    if p.normal.y < -.3:
        for li in p.loop_indices:
            co=head.data.vertices[head.data.loops[li].vertex_index].co
            head.data.uv_layers.active.data[li].uv=uv(12,max(.02,min(.98,co.x/.152+.5)),max(.02,min(.98,(co.z-1.477)/.205)))
mesh('Nose',[(-.018,-.077,1.597),(.018,-.077,1.597),(-.015,-.105,1.568),(.015,-.105,1.568),(0,-.078,1.55)],[(0,2,3,1),(2,4,3),(0,4,2),(1,3,4)],4,'head')
for s in (-1,1):
    tube('Ear',[((s*.087,.004,1.55),.014,.025),((s*.092,.004,1.58),.018,.026),((s*.085,.004,1.603),.012,.020)],4,'head')
# Uneven hair cap plus chunky locks. Broad planes rather than strands.
hair=tube('Hair_Cap',[((0,.025,1.59),.083,.066),((0,.014,1.665),.096,.086),((0,.009,1.704),.078,.071),((0,.008,1.72),.037,.039)],7,'head')
# Remove cap's front low panels so painted forehead remains readable.
# Raise forward vertices at bottom ring, leaving sideburns at the temples.
for v in hair.data.vertices:
    if v.co.y < -.02 and v.co.z < 1.64: v.co.z=1.655
for i in range(7):
    x=-.079+i*.025
    z=1.687+rand.uniform(-.007,.014)
    verts=[(x-.018,-.061,z),(x+.018,-.061,z+.006),(x+.012,-.093,z-.045-rand.uniform(0,.015)),(x-.012,-.096,z-.035),(x,-.025,z+.020)]
    mesh('Hair_Lock',verts,[(0,1,2,3),(0,4,1),(1,4,2),(2,4,3),(3,4,0)],7,'head')

# Compact Generic deformation rig, with explicit deterministic skin weights.
armdata=bpy.data.armatures.new('Boki_Generic')
rig=bpy.data.objects.new('CHR_Boki',armdata); collection.objects.link(rig)
bpy.context.view_layer.objects.active=rig; rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
bones={}
def bone(name,a,b,parent=None):
    e=armdata.edit_bones.new(name); e.head=a; e.tail=b
    if parent: e.parent=bones[parent]
    bones[name]=e
bone('root',(0,0,0),(0,0,.15))
bone('hips',(0,0,.87),(0,0,.99),'root')
bone('spine',(0,0,.99),(0,0,1.18),'hips')
bone('chest',(0,0,1.18),(0,0,1.39),'spine')
bone('neck',(0,0,1.39),(0,0,1.50),'chest')
bone('head',(0,0,1.50),(0,0,1.72),'neck')
for s,suffix in [(-1,'L'),(1,'R')]:
    bone('clavicle.'+suffix,(s*.045,0,1.33),(s*.228,0,1.325),'chest')
    bone('upper_arm.'+suffix,(s*.228,0,1.325),(s*.38,0,1.135),'clavicle.'+suffix)
    bone('lower_arm.'+suffix,(s*.38,0,1.135),(s*.486,-.009,.97),'upper_arm.'+suffix)
    bone('hand.'+suffix,(s*.486,-.009,.97),(s*.519,-.015,.875),'lower_arm.'+suffix)
    bone('thigh.'+suffix,(s*.085,0,.88),(s*.104,-.014,.48),'hips')
    bone('shin.'+suffix,(s*.104,-.014,.48),(s*.097,0,.13),'thigh.'+suffix)
    bone('foot.'+suffix,(s*.097,0,.13),(s*.097,-.17,.065),'shin.'+suffix)
    bone('toe.'+suffix,(s*.097,-.17,.065),(s*.097,-.20,.05),'foot.'+suffix)
bpy.ops.object.mode_set(mode='OBJECT')
# Consolidate to one skinned mesh and one material/draw submission.
bpy.ops.object.select_all(action='DESELECT')
for o in parts: o.select_set(True)
bpy.context.view_layer.objects.active=parts[0]
bpy.ops.object.join()
body=bpy.context.object; body.name='M_Boki'
# Collapse duplicate material slots produced by joining.
body.data.materials.clear(); body.data.materials.append(material)
for p in body.data.polygons: p.material_index=0
body.parent=rig
mod=body.modifiers.new('Boki_Deform','ARMATURE'); mod.object=rig
rig.show_in_front=True

# Scooter, front is Blender -Y. Axle pivots are preserved on wheel meshes.
root=bpy.data.objects.new('PROP_Scooter',None); collection.objects.link(root)
steer=bpy.data.objects.new('Steering',None); collection.objects.link(steer); steer.parent=root; steer.location=(0,-.40,.18)
def propbox(n,c,s,t=8): return box(n,c,s,t,group='scooter')
propbox('M_Deck',(0,0,.12),(.18,.87,.08),8)
propbox('Deck_Grip',(0,0,.164),(.165,.79,.009),9)
for s in (-1,1):
    propbox('Deck_PurpleTrim',(s*.092,0,.133),(.009,.85,.031),13)
    propbox('Reflector',(s*.097,.28,.13),(.008,.12,.018),3)
beam('Deck_Neck',(0,-.40,.12),(0,-.50,.26),.09,.07,13,group='scooter')
beam('Stem',(0,-.50,.18),(0,-.40,1.07),.042,.042,8,group='scooter')
beam('Stem_LimeStripe',(0,-.517,.26),(0,-.468,.68),.025,.007,3,group='scooter')
for z in (.45,.84): propbox('Stem_Clamp',(0,-.50+(z-.18)/.89*.1,z),(.065,.064,.032),13)
beam('Handlebar',(-.24,-.40,1.07),(.24,-.40,1.07),.029,.029,8,group='scooter')
for s in (-1,1):
    beam('Grip',(s*.15,-.40,1.07),(s*.27,-.40,1.07),.041,.042,9,group='scooter')
    for x in (.14,.16): propbox('Grip_Collar',(s*x,-.40,1.07),(.016,.052,.052),13)
propbox('M_Headlight',(0,-.445,1.01),(.10,.085,.10),9)
propbox('Headlight_Lens',(0,-.492,1.01),(.075,.008,.074),10)
propbox('M_LaserBox',(0,-.46,.90),(.085,.065,.082),13)
propbox('Laser_Front',(0,-.497,.90),(.067,.009,.063),9)
propbox('Laser_Emitter',(-.017,-.504,.91),(.018,.007,.018),3)
propbox('Laser_LED',(.019,-.504,.883),(.010,.007,.010),11)
propbox('Rear_Reflector',(0,.53,.18),(.086,.032,.045),11)
# 12-sided stepped tire + metal hub on each end, flat-shaded.
for front,y in [('F',-.52),('R',.52)]:
    verts=[]
    rings=[(-.035,.062),(-.035,.083),(-.025,.10),(.025,.10),(.035,.083),(.035,.062)]
    for x,r in rings:
        for j in range(12):
            a=j*2*math.pi/12
            verts.append((x,y+math.sin(a)*r,.10+math.cos(a)*r))
    faces=[tuple(reversed(range(12)))]
    tiles=[8]
    for k in range(5):
        for j in range(12):
            faces.append((k*12+j,k*12+(j+1)%12,(k+1)*12+(j+1)%12,(k+1)*12+j)); tiles.append(8 if k in (0,4) else 9)
    faces.append(tuple(range(60,72))); tiles.append(8)
    wheel=mesh('M_Wheel_'+front,verts,faces,tiles=tiles,group='scooter')
    # Geometry relative to axle, object transform carries its center.
    for v in wheel.data.vertices: v.co-=Vector((0,y,.10))
    wheel.location=(0,y,.10)
    for s in (-1,1):
        beam('Fork_'+front,(s*.047,y,.10),(s*.047,y+(.06 if front=='F' else -.12),.22),.022,.03,8,group='scooter')
# Separate steering geometry before consolidation; preserve the explicit hinge pivot.
steering_prefixes=('Stem','Handlebar','Grip','M_Headlight','Headlight_Lens','M_LaserBox','Laser_Front','Laser_Emitter','Laser_LED','Fork_F')
steering_parts=[o for o in scooter if o.name.startswith(steering_prefixes)]
bpy.ops.object.select_all(action='DESELECT')
for o in steering_parts: o.select_set(True)
bpy.context.view_layer.objects.active=steering_parts[0]; bpy.ops.object.join()
steering_mesh=bpy.context.object; steering_mesh.name='M_Steering'
steering_mesh.data.materials.clear(); steering_mesh.data.materials.append(material)
for p in steering_mesh.data.polygons: p.material_index=0
steering_mesh.parent=steer
steering_mesh.matrix_parent_inverse=steer.matrix_world.inverted()
# Consolidate static scooter meshes while retaining wheels.
bpy.ops.object.select_all(action='DESELECT')
wheels=[o for o in collection.objects if o.name.startswith('M_Wheel')]
static=[o for o in collection.objects if o.type=='MESH' and o.name not in {body.name,steering_mesh.name} and o not in wheels]
for o in static: o.select_set(True)
bpy.context.view_layer.objects.active=static[0]; bpy.ops.object.join()
deck=bpy.context.object; deck.name='M_Scooter_Frame'
deck.data.materials.clear(); deck.data.materials.append(material)
for p in deck.data.polygons: p.material_index=0
for o in [deck]+wheels: o.parent=root
wheels[0].parent=steer; wheels[0].matrix_parent_inverse=steer.matrix_world.inverted()
# Authoring markers with the agreed contact positions.
for name,loc in {'Socket_HandGrip_L':(-.24,-.40,1.07),'Socket_HandGrip_R':(.24,-.40,1.07),'Foot_L':(-.06,.28,.16),'Foot_R':(.06,-.08,.16),'Socket_LaserMuzzle':(0,-.51,.90),'Socket_Headlight':(0,-.50,1.01)}.items():
    o=bpy.data.objects.new(name,None); collection.objects.link(o); o.parent=root; o.location=loc; o.empty_display_size=.035
    if name.startswith('Socket_'):
        o.parent=steer; o.matrix_parent_inverse=steer.matrix_world.inverted()

# Pose by aiming bone chains at explicit points, keeping limb lengths intact.
def reset_pose():
    for p in rig.pose.bones:
        p.rotation_mode='QUATERNION'; p.rotation_quaternion=(1,0,0,0); p.location=(0,0,0); p.scale=(1,1,1)
    bpy.context.view_layer.update()
def aim(name,target):
    p=rig.pose.bones[name]
    delta=Vector(target)-p.head
    q=(p.tail-p.head).rotation_difference(delta)
    m=p.matrix.copy(); m=q.to_matrix().to_4x4()@m; m.translation=p.head
    p.matrix=m; bpy.context.view_layer.update()
def two_bone(a,b,target,pole):
    upper=rig.pose.bones[a]; lower=rig.pose.bones[b]
    start=upper.head.copy(); end=Vector(target); v=end-start
    la=upper.bone.length; lb=lower.bone.length
    distance=min(v.length,la+lb-.00001); direction=v.normalized()
    side=Vector(pole)-start; side=(side-direction*side.dot(direction)).normalized()
    along=(la*la-lb*lb+distance*distance)/(2*distance)
    bend=math.sqrt(max(0,la*la-along*along))
    elbow=start+direction*along+side*bend
    aim(a,elbow); aim(b,end)
def ride_pose():
    reset_pose()
    hips=rig.pose.bones['hips']; hips.location=(0,0,0)
    # Keep the hip translation neutral for a stable FBX bind-pose round-trip.
    m=hips.matrix.copy(); m.translation=Vector((0,0,.87)); hips.matrix=m
    bpy.context.view_layer.update()
    aim('spine',(0,-.06,1.13)); aim('chest',(0,-.20,1.31))
    aim('neck',(0,-.20,1.42)); aim('head',(0,-.20,1.64))
    for s,suffix,fy in [(-1,'L',.28),(1,'R',-.08)]:
        ankle=(s*.06,fy+.06,.29)
        two_bone('thigh.'+suffix,'shin.'+suffix,ankle,(s*.10,-.6,.55))
        aim('foot.'+suffix,(s*.06,fy-.11,.225))
        # Grip targets correspond to the hand centers, wrist is one palm length behind.
        wrist=(s*.24,-.34,1.08)
        two_bone('upper_arm.'+suffix,'lower_arm.'+suffix,wrist,(s*.6,-.12,1.04))
        aim('hand.'+suffix,(s*.24,-.43,1.065))

scene.render.fps=30; scene.frame_start=1; scene.frame_end=30
ride_pose()
# Deliberately restrained two-key held loop: no sliding contacts or root motion.
for frame in (1,30):
    for p in rig.pose.bones:
        p.keyframe_insert(data_path='rotation_quaternion',frame=frame,group=p.name)
        p.keyframe_insert(data_path='location',frame=frame,group=p.name)
rig.animation_data.action.name='Boki_RideIdle'
rig.animation_data.action.use_fake_user=True
scene.frame_set(1)

# Ensure every closed component has outward normals, including descending sleeve rings.
for obj in (body,deck,steering_mesh,*wheels):
    bm=bmesh.new(); bm.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(obj.data); bm.free()
    obj.modifiers.new('Stable_Export_Triangles','TRIANGULATE')

# Export selected assets only. The external atlas is deliberately easy to remap in URP.
def select(objects):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects: o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
def export(name,objects,anim=False):
    select(objects)
    bpy.ops.export_scene.fbx(filepath=str(OUT/'Export'/name),use_selection=True,
        object_types={'ARMATURE','MESH','EMPTY'},axis_forward='-Z',axis_up='Y',
        apply_unit_scale=True,global_scale=1.0,add_leaf_bones=False,
        bake_anim=anim,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,
        bake_anim_simplify_factor=0.0,path_mode='RELATIVE',use_mesh_modifiers=True)
export('CHR_Boki.fbx',[rig,body],True)
prop_objects=list(root.children_recursive)+[root]
export('PROP_Scooter.fbx',prop_objects)
# Bind-pose static alternative is useful for quick visual inspection and external viewers.
rig.animation_data.action=None; reset_pose()
export('CHR_Boki_BindPose.fbx',[rig,body])
ride_pose()
rig.animation_data.action=bpy.data.actions['Boki_RideIdle']; scene.frame_set(1)

# Studio is excluded from every FBX.
def studio_material(name,color):
    m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.use_nodes=True
    next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED').inputs['Base Color'].default_value=(*color,1)
    return m
floor_mesh=bpy.data.meshes.new('StudioFloor'); floor_mesh.from_pydata([(-200,-200,-.015),(200,-200,-.015),(200,200,-.015),(-200,200,-.015)],[],[(0,1,2,3)])
floor=bpy.data.objects.new('StudioFloor',floor_mesh); studio.objects.link(floor); floor.data.materials.append(studio_material('Studio_Charcoal',(.055,.065,.085)))
def track(o,point): o.rotation_euler=(Vector(point)-o.location).to_track_quat('-Z','Y').to_euler()
for name,loc,power,width in [('Key',(-3,-4,5),430,4),('Fill',(3,-2,3),210,3),('Rim',(1,3,4),500,3)]:
    data=bpy.data.lights.new(name,'AREA'); data.energy=power; data.shape='DISK'; data.size=width
    o=bpy.data.objects.new(name,data); studio.objects.link(o); o.location=loc; track(o,(0,0,.8))
camera_data=bpy.data.cameras.new('Boki_Review_Camera'); camera=bpy.data.objects.new('Boki_Review_Camera',camera_data); studio.objects.link(camera)
scene.camera=camera; camera_data.type='ORTHO'; camera_data.ortho_scale=2.15
camera.location=(2.7,-4,2.1); track(camera,(0,0,.84))
# Set a useful live viewport without touching other scenes.
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_distance=3.1
            area.spaces.active.region_3d.view_location=(0,0,.85)
            area.spaces.active.region_3d.view_rotation=camera.rotation_euler.to_quaternion()
            area.spaces.active.shading.type='MATERIAL'
            area.spaces.active.overlay.show_extras=False
select([rig,body])
# Health report counts evaluated triangles and normalized weights.
def stats(o):
    o.data.calc_loop_triangles()
    return {'vertices':len(o.data.vertices),'triangles':len(o.data.loop_triangles),'materials':len(o.data.materials)}
report={'blender':bpy.app.version_string,'units':'meters','authoring_forward':'-Y','authoring_up':'+Z',
 'character':stats(body),'scooter':{o.name:stats(o) for o in prop_objects if o.type=='MESH'},
 'bones':len(armdata.bones),'atlas':[512,512],'actions':['Boki_RideIdle'],
 'standing_height_m':1.72,'max_influences':max(len(v.groups) for v in body.data.vertices),
 'unweighted_vertices':sum(not v.groups for v in body.data.vertices),
 'non_normalized_vertices':sum(abs(sum(g.weight for g in v.groups)-1)>1e-5 for v in body.data.vertices)}
evalbody=body.evaluated_get(bpy.context.evaluated_depsgraph_get())
report['ride_bounds_m']={axis:[min(v.co[i] for v in evalbody.data.vertices),max(v.co[i] for v in evalbody.data.vertices)] for i,axis in enumerate('XYZ')}
report['unity_import_verified']=False
(OUT/'validation.json').write_text(json.dumps(report,indent=2)+'\n')
# This builder runs in its own background process; save a normal standalone project.
for other_scene in list(bpy.data.scenes):
    if other_scene != scene: bpy.data.scenes.remove(other_scene)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Source/CHR_Boki.blend'),compress=True)
for label,pos in [('ride-three-quarter',(2.7,-4,2.1)),('ride-side',(4,0,1.5)),('ride-back',(-2.5,4,2.0))]:
    camera.location=pos; track(camera,(0,0,.84)); scene.render.filepath=str(OUT/'Previews'/f'{label}.png'); bpy.ops.render.render(write_still=True)
rig.animation_data.action=None; reset_pose(); root.hide_render=True
for o in root.children_recursive: o.hide_render=True
camera.location=(2.7,-4,2.05); track(camera,(0,0,.86)); scene.render.filepath=str(OUT/'Previews/bind-three-quarter.png'); bpy.ops.render.render(write_still=True)
root.hide_render=False
for o in root.children_recursive: o.hide_render=False
rig.animation_data.action=bpy.data.actions['Boki_RideIdle']; scene.frame_set(1)
camera.location=(2.7,-4,2.1); track(camera,(0,0,.84))
print(json.dumps(report,indent=2))
