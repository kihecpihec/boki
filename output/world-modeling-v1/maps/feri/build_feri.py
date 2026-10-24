"""Deterministic FERI art build; run with Blender --background --factory-startup --python.
Refuses to replace artist source unless FERI_FORCE=1. No Unity scene mutations.
"""
import bpy, bmesh, math, random, json, os, sys
from pathlib import Path
from mathutils import Vector
OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[2]
if (OUT/'Source/FERI.blend').exists() and os.getenv('FERI_FORCE') != '1':
    raise RuntimeError('Preserve artist edits: use a fresh output folder or explicitly set FERI_FORCE=1.')
for d in ['Source','Export','Textures','Previews']:(OUT/d).mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
scene=bpy.context.scene; scene.name='FERI'; scene.unit_settings.system='METRIC'; scene.unit_settings.scale_length=1
rng=random.Random(758)
cols={}
for n in ['Shared_Arrival','Route_A','Route_B','Route_C','Shared_Landing','Arena_Classroom','Seals','Removable_Roofs','CollisionGuides','Markers','Presentation']:
    c=bpy.data.collections.new(n); scene.collection.children.link(c); cols[n]=c
M={}
palette={'Wall':'647165','Floor':'45514E','Wood':'6E5741','Iron':'263230','Dark':'101B1B','Leaf':'45532C','Paper':'D9D6C5','Concrete':'58675E','Tube':'C3D4A8','Danger':'EF624A'}
for i,(n,h) in enumerate(palette.items()):
    color=tuple(int(h[j:j+2],16)/255 for j in (0,2,4))
    m=bpy.data.materials.new('M_FERI_'+n); m.use_nodes=True; p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Roughness'].default_value=.88
    im=bpy.data.images.new('FERI_'+n,128,128); pixels=[]
    noise=[[rng.uniform(.65,1.12) for x in range(32)] for y in range(32)]
    for y in range(128):
        for x in range(128):
            f=noise[y//4][x//4]
            if n=='Floor' and (x%32<2 or y%32<2): f=.4
            if n=='Wall':
                if y<36:f*=.5
                if x%19<3:f*=.7
            if n=='Wood' and x%13<2:f*=.55
            pixels.extend([min(1,c*f) for c in color]+[1])
    im.pixels.foreach_set(pixels); im.filepath_raw=str(OUT/'Textures'/f'{n}.png'); im.file_format='PNG'; im.save()
    tex=m.node_tree.nodes.new('ShaderNodeTexImage'); tex.image=im; tex.interpolation='Closest'; m.node_tree.links.new(tex.outputs['Color'],p.inputs['Base Color'])
    if n in ['Tube','Danger']:
        m.node_tree.links.new(tex.outputs['Color'],p.inputs['Emission Color']); p.inputs['Emission Strength'].default_value=2.5
    M[n]=m
counts={}; guides=[]; markers=[]
def move(o,c):
    for old in list(o.users_collection):old.objects.unlink(o)
    cols[c].objects.link(o)
def box(n,pos,size,mat,c,collision=False):
    counts[n]=counts.get(n,0)+1; name=f'{n}_{counts[n]:03d}'
    verts=[(a*size[0]/2,b*size[1]/2,d*size[2]/2) for a,b,d in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
    me=bpy.data.meshes.new(name); me.from_pydata(verts,[],[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]); me.update()
    o=bpy.data.objects.new(name,me); cols[c].objects.link(o); o.location=pos; me.uv_layers.new(name='UVMap')
    if mat:o.data.materials.append(M[mat])
    # World-sized planar UVs, same density across all boxes.
    uv=o.data.uv_layers.active
    for poly in o.data.polygons:
        axis=max(range(3),key=lambda a:abs(poly.normal[a])); axes=[a for a in range(3) if a!=axis]
        for li in poly.loop_indices:
            v=o.data.vertices[o.data.loops[li].vertex_index].co+o.location
            uv.data[li].uv=(v[axes[0]]/4,v[axes[1]]/4)
    if collision:guide(name,pos,size,c)
    return o
def guide(n,p,s,owner):
    o=box('COL_'+n,p,s,None,'CollisionGuides'); o.hide_render=True; o.hide_set(True)
    guides.append(dict(name=o.name,position=p,size=s,rotation=[0,0,0],owner=owner,shape='box'))
    return o
def mark(n,p,owner,kind=None,size=None):
    o=bpy.data.objects.new(n,None); cols['Markers'].objects.link(o); o.location=p; o.empty_display_size=.4
    markers.append(dict(name=n,type=kind or n.split('_')[0],position=p,rotation=[0,0,0],owner=owner,**({'size':size} if size else {})))
    return o
def text(n,body,p,size,c,mat='Paper'):
    cu=bpy.data.curves.new(n,'FONT'); cu.body=body; cu.resolution_u=2; cu.size=size; cu.align_x='CENTER'; cu.extrude=.005
    o=bpy.data.objects.new(n,cu); cols[c].objects.link(o); o.location=p; o.rotation_euler=(math.pi/2,0,0); cu.materials.append(M[mat])
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active=o; bpy.ops.object.convert(target='MESH')
    # Text has generated UVs via smart projection.
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.uv.smart_project(); bpy.ops.object.mode_set(mode='OBJECT')
    return o
def desk(x,y,c):
    box('ENV_Desk',(x,y,.82),(1.5,.8,.12),'Wood',c,True)
    for dx in [-.62,.62]:
        for dy in [-.3,.3]:box('ENV_DeskLeg',(x+dx,y+dy,.38),(.07,.07,.76),'Iron',c)
    box('ENV_Chair',(x,y-.85,.45),(.55,.55,.1),'Wood',c)
    box('ENV_ChairBack',(x,y-1.1,.83),(.55,.08,.7),'Wood',c)
    box('ENV_Paper',(x+.2,y,.888),(.3,.4,.012),'Paper',c)
def light(p,energy=80,color=(.64,.9,.42),size=3):
    d=bpy.data.lights.new('Preview_Fluorescent','AREA'); d.energy=energy; d.color=color; d.shape='DISK'; d.size=size
    o=bpy.data.objects.new(d.name,d); cols['Presentation'].objects.link(o); o.location=p
# Approach combat pockets. The gameplay overlay proposes 20 x 18 m, but Maribor (24x28 / 22x24 / 18x20 m) and Pohorje
# (56x64 / 52x60 / 48x52 m) were built larger and 20 x 18 m cannot hold an offscreen spawn 8 m from Boki, so FERI's
# three pockets are enlarged. Marker and prop layouts keep the overlay's pocket-local proportions via pk().
PW,PD=36,32                # pocket width (X) and depth (Y), even meters on the 2 m grid
GAP=6                      # wall-to-wall void between neighbouring pockets
PY0=20; PY1=PY0+PD         # pocket floor spans Y PY0..PY1; the front corridor spans 8..PY0
REAR=PY1+14                # rear seals / shared landing start; rear corridor is 14 m
ARENA=REAR+10              # classroom start; landing is 8 m deep plus a 2 m neck
RX={'A':-(PW+GAP),'B':0,'C':PW+GAP}
EDGE=PW+GAP+4              # shared arrival / landing half-width
RAMP_C=PY1+6               # B ramp crest; 4 m up, 4 m down
CEIL=4.55                  # ceiling slab centre; 0.2 m thick, underside 4.45 m sits flush on the 4.46 m wall caps
def pk(cx,lx,lz):
    """Pocket-local overlay coordinates (20 x 18 m, X right, Z toward exit) to Blender XY."""
    return (cx-PW/2+lx*PW/20, PY0+lz*PD/18)
# Occupied 2 m cells. Each branch remains independent; no cross-branch openings.
cells={}
def rect(x0,x1,y0,y1,owner):
    for x in range(x0,x1,2):
        for y in range(y0,y1,2):
            assert (x,y) not in cells,(x,y)
            cells[x,y]=owner
rect(-EDGE,EDGE,0,8,'Shared_Arrival')
for route,cx in RX.items():
    c='Route_'+route
    rect(cx-2,cx+2,8,PY0,c);rect(cx-PW//2,cx+PW//2,PY0,PY1,c);rect(cx-2,cx+2,PY1,REAR,c)
rect(-EDGE,EDGE,REAR,REAR+8,'Shared_Landing');rect(-2,2,REAR+8,ARENA,'Shared_Landing');rect(-12,12,ARENA,ARENA+20,'Arena_Classroom')
def height(x,y):
    return max(0,.5-abs(y-RAMP_C)*.125) if -2<=x<=2 and RAMP_C-4<=y<=RAMP_C+4 else 0
def is_flat(p):
    return all(height(p[0]+dx,p[1]+dy)==0 for dx,dy in [(0,0),(2,0),(2,2),(0,2)])
def merge(pool,make):
    """Greedy maximal rectangles per owner over a {(x,y):owner} cell pool; calls make(owner,x0,x1,y0,y1)."""
    pool=dict(pool)
    while pool:
        x,y=min(pool,key=lambda p:(p[1],p[0])); owner=pool[x,y]; x1=x+2
        while pool.get((x1,y))==owner:x1+=2
        y1=y+2
        while all(pool.get((xx,y1))==owner for xx in range(x,x1,2)):y1+=2
        make(owner,x,x1,y,y1)
        for xx in range(x,x1,2):
            for yy in range(y,y1,2):del pool[xx,yy]
# Floors: the B ramp cells stay single boxes with a custom closed wedge top; flat cells merge into maximal slabs.
for (x,y),c in cells.items():
    if is_flat((x,y)):continue
    o=box('PL_Floor',(x+1,y+1,-.2),(2,2,.4),'Floor',c)
    for v in o.data.vertices:
        if v.co.z>0:
            wx=v.co.x+o.location.x; wy=v.co.y+o.location.y; v.co.z+=height(wx,wy)
def flat_floor(owner,x0,x1,y0,y1):
    pos=((x0+x1)/2,(y0+y1)/2,-.2); size=(x1-x0,y1-y0,.4)
    box('PL_Floor',pos,size,'Floor',owner)
    guide('Floor_'+owner,pos,size,owner)
merge({p:c for p,c in cells.items() if is_flat(p)},flat_floor)
# Walls: contiguous boundary edges of one owner become one wall, base and cap, with one collider proposal.
runs={}
for (x,y),c in cells.items():
    for dx,dy in [(0,-2),(0,2),(-2,0),(2,0)]:
        if (x+dx,y+dy) in cells:continue
        key=('x',y+1+dy/2,c) if dy else ('y',x+1+dx/2,c)
        runs.setdefault(key,[]).append(x if dy else y)
for (axis,fixed,c),at in runs.items():
    at.sort(); i=0
    while i<len(at):
        j=i
        while j+1<len(at) and at[j+1]==at[j]+2:j+=1
        a0,a1=at[i],at[j]+2; mid=(a0+a1)/2; length=a1-a0; i=j+1
        pos=(lambda z:(mid,fixed,z)) if axis=='x' else (lambda z:(fixed,mid,z))
        size=(length,.22,4.4) if axis=='x' else (.22,length,4.4)
        box('PL_Wall',pos(2.2),size,'Wall',c,True)
        box('PL_WallBase',pos(.45),(size[0]+.04,size[1]+.04,.9),'Concrete',c)
        box('PL_WallCap',pos(4.38),(size[0]+.08,size[1]+.08,.16),'Iron',c)
# Ceilings close every indoor path and room. The campus arrival and B's courtyard stay open to the sky.
def roof(owner,x0,x1,y0,y1):
    pos=((x0+x1)/2,(y0+y1)/2,CEIL); size=(x1-x0,y1-y0,.2)
    box('PL_Roof_'+owner,pos,size,'Concrete','Removable_Roofs')
    guide('Roof_'+owner,pos,size,owner)
merge({p:c for p,c in cells.items() if c!='Shared_Arrival' and not(c=='Route_B' and PY0<=p[1]<PY1)},roof)
# Repeating corridors: doors, tubes, utility pipes; wall thickness remains physical.
for route,cx in RX.items():
    c='Route_'+route
    for y in [10,14,18,PY1+2,PY1+10,PY1+12]:
        box('ENV_Tube',(cx,y,4.1),(2,.16,.1),'Tube',c)
        if y in [14,PY1+10]:light((cx,y,3.9),110)
        if route!='C':
            box('ENV_Door',(cx-1.85,y,1.5),(.12,1.4,3),'Wood',c)
            box('ENV_DoorWindow',(cx-1.77,y,2.1),(.03,.6,.5),'Dark',c)
        else:
            for z in [1.1,2.7]:box('ENV_ServicePipe',(cx+1.8,y,z),(.12,3.8,.12),'Iron',c)
    for y,which in [(8,'Front'),(REAR,'Rear')]:
        o=box('PL_Seal_'+route+'_'+which,(cx,y,2),(4,.25,4),'Iron','Seals')
        guide(o.name,(cx,y,2),(4,.25,4),'Seals')
        mark('Seal_'+route+'_'+which,(cx,y,0),c,size=[4,.25,4])
        if which=='Front':
            mark('Gate_'+route,(cx,y,0),c,size=[4,.25,4]);mark('Trigger_'+route,(cx,y+1,0),c,size=[4,1,3.5])
    title={'A':'A / GLAVNI VHOD','B':'B / DVORIŠČE','C':'C / SERVISNI VHOD'}[route]
    text('ENV_Sign_'+route,title,(cx,7.8,3.1),.32,'Seals','Tube')
    # Combat pocket furniture sits in the side strips, outside the entry-to-exit lane and the overlay's marker spots.
    for lx in [3,17]:
        for lz in [3,9,15]:
            x,y=pk(cx,lx,lz)
            if route=='B':
                box('ENV_Planter',(x,y,.4),(2.8,2,.8),'Concrete',c,True)
                for k in range(7):
                    bx=x+rng.uniform(-1,1);by=y+rng.uniform(-.7,.7)
                    box('ENV_Shrub',(bx,by,rng.uniform(.9,1.4)),(.7,.7,.8),'Leaf',c)
            elif route=='A':desk(x,y,c)
            else:
                box('ENV_Cabinet',(x,y,1.2),(1.2,1,2.4),'Concrete',c,True)
                for z in [.5,.7,.9,1.1]:box('ENV_Vent',(x,y-.51,z),(.9,.025,.035),'Dark',c)
    # Overlay markers: B charges, S spawn candidates, G Guilt anchors, in pocket-local meters.
    for j,(lx,lz) in enumerate([(4,4),(16,4),(10,14)],1):
        x,y=pk(cx,lx,lz)
        mark(f'Charge_{route}_{j}',(x,y,0),c)
        box('ENV_ChargeDock',(x,y,.22),(.6,.6,.44),'Iron',c)
        box('ENV_ChargeGlow',(x,y,.48),(.42,.42,.09),'Tube',c)
    for j,(lx,lz) in enumerate([(1,1),(19,1),(1,9),(19,9),(1,17),(19,17)],1):
        x,y=pk(cx,lx,lz);mark(f'Spawn_{route}_{j}',(x,y,0),c)
    for j,(lx,lz) in enumerate([(10,2),(13,9),(10,16)],1):
        x,y=pk(cx,lx,lz);mark(f'Guilt_Anchor_{route}_{j}',(x,y,0),c)
    mark('Occupancy_'+route,(cx,PY0+PD/2,2),c,size=[PW,PD,4])
    for lx in [4,10,16]:
        for lz in [5,13]:
            x,y=pk(cx,lx,lz);box('ENV_Tube',(x,y,4.05),(1.8,.2,.12),'Tube',c);light((x,y,3.8),130)
# Exactly two trap plates; red geometric cross signals danger without inventing damage radius.
CX=RX['C']
for j,y in enumerate([12,17],1):
    box('ENV_TrapPlate',(CX,y,.015),(1.8,1.8,.03),'Dark','Route_C')
    for dx in [-.85,.85]:box('ENV_TrapEdge',(CX+dx,y,.045),(.06,1.8,.03),'Danger','Route_C')
    for dy in [-.85,.85]:box('ENV_TrapEdge',(CX,y+dy,.045),(1.8,.06,.03),'Danger','Route_C')
    mark(f'Event_FloorTrap_{j}',(CX,y,0),'Route_C',size=[1.8,1.8,.1])
PO_X,PO_Y=CX+PW/2-1.1,pk(CX,0,15)[1]   # against the service room's east wall, past the pocket's midline
box('ENV_PowerOffCabinet',(PO_X,PO_Y,1.3),(1,1,2.6),'Concrete','Route_C',True)
box('ENV_PowerOffSwitch',(PO_X,PO_Y-.54,1.25),(.2,.1,.35),'Danger','Route_C')
text('ENV_PowerOffLabel','POWER\nOFF',(PO_X,PO_Y-.57,1.9),.22,'Route_C','Danger')
mark('Event_PowerOffInteract',(PO_X,PO_Y-1.5,0),'Route_C')
mark('Event_A_MeleeGroup_1',(RX['A'],16,0),'Route_A');mark('Event_A_MeleeGroup_2',(*pk(RX['A'],10,10),0),'Route_A')
mark('Event_B_Ambush',(RX['B'],PY0+PD/2,0),'Route_B')
# Courtyard dead tree: tapered low polygon branches and overgrown beds.
def branch(a,b,r):
    d=Vector(b)-Vector(a);bpy.ops.mesh.primitive_cone_add(vertices=6,radius1=r,radius2=r*.45,depth=d.length,location=(Vector(a)+Vector(b))/2)
    o=bpy.context.object;o.name='ENV_DeadTree_'+str(len(bpy.data.objects));o.rotation_euler=d.to_track_quat('Z','Y').to_euler();move(o,'Route_B');o.data.materials.append(M['Wood'])
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
TX,TY=pk(RX['B'],4,11)
branch((TX,TY,0),(TX,TY,5),.28)
for j in range(7):
    a=(TX,TY,2+j*.4);b=(TX+rng.uniform(-2,2),TY+rng.uniform(-1.5,1.5),4+j*.35);branch(a,b,.12)
guide('Tree',(TX,TY,2.5),(.6,.6,5),'Route_B')
for lx in [5,15]:
    x,y=pk(RX['B'],lx,9)
    box('ENV_Bench',(x,y,.55),(2,.6,.15),'Wood','Route_B',True)
    for dx in [-.8,.8]:box('ENV_BenchLeg',(x+dx,y,.25),(.15,.5,.5),'Iron','Route_B')
# Shared arrival facade and classroom.
text('ENV_FERI_Name','FERI',(0,.35,3.2),1,'Shared_Arrival','Tube')
box('ENV_EntryLintel',(0,.5,4),(12,.8,.6),'Concrete','Shared_Arrival')
for x in [-5.7,5.7]:box('ENV_EntryPillar',(x,.5,1.8),(.6,.8,3.6),'Concrete','Shared_Arrival',True)
# The classroom keeps the 24 x 20 m size of classroom-dimensioned.svg; its offsets are measured from the room's near wall.
mark('Start_Player',(0,4,0),'Shared_Arrival');mark('Event_FinalGuilt',(0,REAR+5,0),'Shared_Landing')
mark('Event_ArenaEntry',(0,ARENA+1,0),'Arena_Classroom',size=[4,1,3.5])
box('PL_ArenaEntrySeal',(0,ARENA-1,2),(4,.25,4),'Wood','Seals');guide('ArenaEntrySeal',(0,ARENA-1,2),(4,.25,4),'Seals')
mark('Seal_Classroom',(0,ARENA-1,0),'Arena_Classroom',size=[4,.25,4])
for x in [-9.5,9.5]:
    for dy in [4,8,12,16]:desk(x,ARENA+dy,'Arena_Classroom')
box('ENV_Blackboard',(0,ARENA+19.72,2.3),(10,.12,2),'Dark','Arena_Classroom')
text('ENV_Attendance','PRISOTNOST: 0/1',(0,ARENA+19.62,2.3),.52,'Arena_Classroom')
box('ENV_Lectern',(0,ARENA+17,.7),(2,1.1,1.4),'Wood','Arena_Classroom',True)
box('ENV_LecternTop',(0,ARENA+17,1.46),(2.2,1.3,.12),'Wood','Arena_Classroom')
mark('Spawn_Professor',(0,ARENA+18,0),'Arena_Classroom');mark('Event_GuiltBackdrop',(0,ARENA+19.5,0),'Arena_Classroom')
mark('Occupancy_Classroom',(0,ARENA+10,2),'Arena_Classroom',size=[24,20,4]);mark('Exit_FERI',(0,ARENA+17,0),'Arena_Classroom','VictoryEvent')
for j,x in enumerate([-8,8],1):
    mark('Charge_Classroom_'+str(j),(x,ARENA+8,0),'Arena_Classroom')
    box('ENV_BossCharge',(x,ARENA+8,.3),(.6,.6,.6),'Tube','Arena_Classroom')
    mark('Spawn_Classroom_Add_'+str(j),(x,ARENA+16,0),'Arena_Classroom')
for x in [-8,0,8]:
    for dy in [4,13,18]:box('ENV_Tube',(x,ARENA+dy,4.1),(2,.2,.12),'Tube','Arena_Classroom');light((x,ARENA+dy,3.9),150)
# Reference proxies, preview only.
box('MK_MeterCube',(-20,3,.5),(1,1,1),'Paper','Presentation')
box('MK_PlayerEnvelope',(-18,3,.9),(.7,.7,1.8),'Danger','Presentation')
# Merge static geometry by owner/material to keep draw object count small, preserve seals.
for cname in ['Shared_Arrival','Route_A','Route_B','Route_C','Shared_Landing','Arena_Classroom']:
    for mat in M.values():
        obs=[o for o in cols[cname].objects if o.type=='MESH' and o.data.materials and o.data.materials[0]==mat]
        if not obs:continue
        bpy.ops.object.select_all(action='DESELECT')
        for o in obs:o.hide_set(False);o.select_set(True)
        bpy.context.view_layer.objects.active=obs[0];bpy.ops.object.join();o=bpy.context.object;o.name='PL_'+cname+'_'+mat.name.removeprefix('M_FERI_')
        scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
# Roofs stay one object per owner so each space can be shown or hidden separately; all start hidden in the source.
roofs={}
for o in list(cols['Removable_Roofs'].objects):
    if o.type=='MESH':roofs.setdefault(o.name.rsplit('_',1)[0],[]).append(o)
for name,obs in roofs.items():
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs:o.hide_set(False);o.select_set(True)
    bpy.context.view_layer.objects.active=obs[0];bpy.ops.object.join();o=bpy.context.object;o.name=name
    scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    o.hide_render=True;o.hide_set(True)
# Plain UV-unwrapped closed geometry; triangulate delivered meshes.
for o in scene.objects:
    if o.type=='MESH':
        bm=bmesh.new(); bm.from_mesh(o.data); bmesh.ops.triangulate(bm,faces=list(bm.faces))
        bad=[f for f in bm.faces if f.calc_area()<1e-5]  # float32 FBX positions near Y 96 m collapse thinner slivers
        if bad:bmesh.ops.delete(bm,geom=bad,context='FACES')
        loose=[v for v in bm.verts if not v.link_faces]
        if loose:bmesh.ops.delete(bm,geom=loose,context='VERTS')
        bm.to_mesh(o.data); bm.free(); o.data.update()
(OUT/'markers.json').write_text(json.dumps({'coordinates':'Blender XYZ meters; Z up','markers':markers},indent=2))
(OUT/'collision-guides.json').write_text(json.dumps(guides,indent=2))
(OUT/'layout.json').write_text(json.dumps({'cell_size':2,'cells':[{'x':x,'y':y,'owner':c} for (x,y),c in cells.items()], 'classroom':[24,20], 'central_clear_floor':[14,12], 'route_clear_width':3.78,'ramp':{'x':RX['B'],'center_y':RAMP_C,'run':4,'rise':.5,'return_run':4},'door_width':4,'pocket':[PW,PD],'route_x':RX,'classroom_probe':[0,ARENA+10]},indent=2))
# Preview-only lighting, quiet black environment and cool green fluorescents.
scene.world.color=(.055,.065,.05)
d=bpy.data.lights.new('Preview_Moon','AREA');d.energy=3500;d.shape='DISK';d.size=110
ob=bpy.data.objects.new(d.name,d);cols['Presentation'].objects.link(ob);ob.location=(0,ARENA/2,70)
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
scene.render.resolution_x=1400;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX';scene.view_settings.exposure=.6
def rig(target,offset,scale):
    return ((target[0]+offset[0],target[1]+offset[1],target[2]+offset[2]),target,scale)
cameras=[('01-overview',*rig((0,(ARENA+20)/2,0),(90,-129,115),150)),('02-classroom',*rig((0,ARENA+10,1),(25,-31,25),36)),('03-courtyard',*rig((RX['B'],PY0+PD/2,1),(40,-34,38),56)),('04-service',*rig((RX['C'],PY0+PD/2,0),(18,-30,70),62))]
for name,pos,target,scale in cameras:
    d=bpy.data.cameras.new(name);o=bpy.data.objects.new(name,d);cols['Presentation'].objects.link(o);o.location=pos;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler();d.type='ORTHO';d.ortho_scale=scale
scene.camera=bpy.data.objects['01-overview']
# Export chunks; hidden source roofs and guides are explicitly selected and restored.
export_stats={}
for cname,c in cols.items():
    if cname=='Presentation':continue
    obs=[o for o in c.objects if o.type in {'MESH','EMPTY'}]
    if not obs:continue
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs:o.hide_set(False);o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(OUT/'Export'/('FERI_'+cname+'.fbx')),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,use_mesh_modifiers=True,use_triangles=True,mesh_smooth_type='FACE',add_leaf_bones=False,bake_anim=False,path_mode='RELATIVE')
    export_stats[cname]={'objects':len(obs),'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in obs if o.type=='MESH')}
    if cname in ['CollisionGuides','Removable_Roofs']:
        for o in obs:o.hide_set(True)
(OUT/'export-manifest.json').write_text(json.dumps(export_stats,indent=2))
bpy.ops.object.select_all(action='DESELECT')
for im in bpy.data.images:
    if im.source=='FILE':im.filepath='//../Textures/'+Path(im.filepath).name
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Source/FERI.blend'))
if os.getenv('FERI_PREVIEWS','1')!='0':
    for name,_,_,_ in cameras:
        scene.camera=bpy.data.objects[name];scene.render.filepath=str(OUT/'Previews'/f'{name}.png');bpy.ops.render.render(write_still=True)
print('FERI BUILD COMPLETE',json.dumps(export_stats))
