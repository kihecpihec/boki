"""Pohorje large environment build. Run inside Blender; preserves earlier scenes."""
import bpy, math, random, json, io, contextlib
from pathlib import Path
from collections import defaultdict
from mathutils import Vector, Matrix

OUT=Path(__file__).resolve().parent
for folder in ('Source','Export','Textures','Previews'):(OUT/folder).mkdir(parents=True,exist_ok=True)
if bpy.data.scenes.get('Pohorje_Large'):
    raise RuntimeError('Pohorje_Large already exists; refusing to replace edits.')
scene=bpy.data.scenes.new('Pohorje_Large');bpy.context.window.scene=scene
scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
rng=random.Random(758)
def coll(name):
    c=bpy.data.collections.new('PL_'+name);scene.collection.children.link(c);return c
shared=coll('Shared_Bellevue');fork=coll('Shared_Fork');ending=coll('Shared_Overlook')
routes={k:coll('Route_'+k) for k in 'ABC'}
terraincol=coll('Terrain_Chunks');forestcol=coll('Forest_Chunks');roofcol=coll('C_Removable_Roofs')
library=coll('Module_Library');library.hide_render=True
collision=coll('Collision_Guides');collision.hide_render=True;collision.hide_viewport=True
markers=coll('Gameplay_Markers');stage=coll('Presentation')

paths={
 'Safe':[(0,16),(4,36),(-4,56),(0,76),(0,80)],
 'ForkA':[(0,80),(-10,90),(-20,100)],
 'ForkB':[(0,80),(0,94),(0,108)],
 'ForkC':[(0,80),(10,90),(20,100)],
 'LinkA':[(-20,100),(-50,112),(-65,124)],
 'LinkB':[(0,108),(0,124)],
 'LinkC':[(20,100),(50,112),(65,124)],
 'A':[(-65,124),(-82,150),(-115,178),(-95,207),(-125,235),(-108,265),(-100,300)],
 'B':[(0,124),(36,151),(-36,180),(36,209),(-36,238),(36,267),(0,300)],
 'C':[(65,124),(88,150),(100,190),(100,250),(100,300)],
 'ExitA':[(-100,360),(-114,382),(-94,410),(-65,435),(-64,452)],
 'ExitB':[(0,356),(9,385),(0,420),(0,452)],
 'ExitC':[(100,344),(100,375),(80,408),(64,435),(64,452)],
 'MergeA':[(-64,452),(-32,470),(0,490)],
 'MergeB':[(0,452),(0,470),(0,490)],
 'MergeC':[(64,452),(32,470),(0,490)],
 'Final':[(0,490),(0,508)]}
# Round the bends without changing shared endpoints or the route topology.
for key in list(paths):
    if key in ('Final',):continue
    pts=paths[key];rounded=[pts[0]]
    for a,b in zip(pts,pts[1:]):
        rounded.extend([(a[0]*.75+b[0]*.25,a[1]*.75+b[1]*.25),
                        (a[0]*.25+b[0]*.75,a[1]*.25+b[1]*.75)])
    paths[key]=rounded+[pts[-1]]
def center_x(key,y):
    pts=paths[key]
    for a,b in zip(pts,pts[1:]):
        if a[1]<=y<=b[1]:
            t=(y-a[1])/max(1e-6,b[1]-a[1]);return a[0]*(1-t)+b[0]*t
    return pts[0][0] if y<pts[0][1] else pts[-1][0]
widths={k:(10 if k in ('B','ForkB','LinkB','ExitB','MergeB','Safe','Final') else 8) for k in paths}
pockets={'A':(-100,300,64,60),'B':(0,300,60,56),'C':(100,300,48,44)}
def zground(y):
    if y<=90:return 36.
    if y<300:return 36-(y-90)*28/210
    if y<=360:return 8.
    if y<490:return 8-(y-360)*8/130
    return 0.
segments=[]
for key,pts in paths.items():
    for aa,bb in zip(pts,pts[1:]):
        a,b=Vector(aa),Vector(bb);segments.append((key,a,b,b-a,(b-a).length_squared,widths[key]/2))
def nearest(x,y):
    p=Vector((x,y));best=(1e9,None,None)
    for key,a,b,v,l2,half in segments:
        q=a+max(0,min(1,(p-a).dot(v)/l2))*v
        d=(p-q).length-half
        if d<best[0]:best=(d,q,key)
    return best
def in_pocket(x,y,margin=0):
    return any(abs(x-cx)<w/2+margin and yy-margin<y<yy+d+margin for cx,yy,w,d in pockets.values())
def ground(x,y):
    d,_,_=nearest(x,y)
    safe=in_pocket(x,y,8) or (abs(x)<25 and y<20) or (abs(x)<26 and y>490)
    t=0 if safe else min(1,max(0,(d-10)/18))
    hills=(3+3*math.sin(x*.033+y*.018)**2+6*math.sin(x*.047-y*.013)**4)*t
    depression=.25 if 288<=y<=312 and (safe or d<20) else 0
    return zground(y)-.06+hills-depression

materials=[]
def mat(name,color,texture=None,emission=0):
    m=bpy.data.materials.new('PL_'+name);m.use_nodes=True;m.diffuse_color=(*color,1)
    p=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    p.inputs['Base Color'].default_value=(*color,1);p.inputs['Roughness'].default_value=.96
    if texture:
        n=m.node_tree.nodes.new('ShaderNodeTexImage');n.image=texture;n.interpolation='Closest'
        m.node_tree.links.new(n.outputs['Color'],p.inputs['Base Color'])
    if emission:
        p.inputs['Emission Color'].default_value=(*color,1);p.inputs['Emission Strength'].default_value=emission
    materials.append(m);return m
def tex(name,color,kind):
    size=128;rr=random.Random(name);pixels=[]
    coarse=[[rr.uniform(-.02,.02) for i in range(16)] for j in range(16)]
    for y in range(size):
        for x in range(size):
            n=rr.uniform(-.025,.025)+coarse[y//8][x//8]
            if kind=='wood':n+=.023*math.sin(x*.65+y*.03)
            if kind=='stone':n+=.015*math.sin(x*.1+y*.15)
            if kind=='dirt' and rr.random()<.02:n+=.065
            pixels.extend([max(.008,min(1,c+n)) for c in color]+[1])
    im=bpy.data.images.new('PL_'+name,width=size,height=size);im.pixels.foreach_set(pixels)
    im.filepath_raw=str(OUT/'Textures'/(name+'.png'));im.file_format='PNG';im.save();im.pack();return im
soil=mat('Soil',(.17,.18,.14),tex('ForestFloor',(.17,.18,.14),'dirt'))
dirt=mat('Path',(.31,.28,.235),tex('Dirt',(.31,.28,.235),'dirt'))
stone=mat('Rock',(.29,.31,.30),tex('Rock',(.29,.31,.30),'stone'))
bark=mat('Bark',(.21,.18,.14),tex('Bark',(.21,.18,.14),'wood'))
foliage=mat('Foliage',(.11,.145,.105),tex('Needles',(.11,.145,.105),'dirt'))
concrete=mat('Concrete',(.30,.315,.30),tex('Concrete',(.30,.315,.30),'stone'))
metal=mat('Metal',(.10,.12,.115));red=mat('ServiceRed',(.24,.095,.065))
paper=mat('OldPaper',(.49,.46,.34));purple=mat('RaveEmission',(.37,.075,.64),emission=2)
warm=mat('WarmEmission',(.8,.39,.13),emission=2);cyan=mat('ChargeEmission',(.18,.48,.37),emission=1)
black=mat('DistantSilhouette',(.055,.075,.085));glass=mat('BlackGlass',(.045,.065,.07))

def uv(o,scale=4):
    layer=o.data.uv_layers.new(name='UVMap')
    for p in o.data.polygons:
        axis=max(range(3),key=lambda a:abs(p.normal[a]));axes=[a for a in range(3) if a!=axis]
        for li in p.loop_indices:
            v=o.data.vertices[o.data.loops[li].vertex_index].co;layer.data[li].uv=(v[axes[0]]/scale,v[axes[1]]/scale)
def mesh(name,verts,faces,m,c,indices=None):
    d=bpy.data.meshes.new('PL_'+name);d.from_pydata(verts,[],faces);d.update()
    o=bpy.data.objects.new('PL_'+name,d);c.objects.link(o)
    for item in (m if isinstance(m,list) else [m]):d.materials.append(item)
    if indices:
        for p,i in zip(d.polygons,indices):p.material_index=i
    uv(o);return o
cubeverts=[(i,j,k) for k in (0,1) for j in (0,1) for i in (0,1)]
cubefaces=[(0,2,3,1),(4,5,7,6),(0,1,5,4),(2,6,7,3),(0,4,6,2),(1,3,7,5)]
def box(name,size,pos,m,c,angle=0):
    o=mesh(name,[(x*size[0],y*size[1],z*size[2]) for x,y,z in cubeverts],cubefaces,m,c)
    o.location=pos;o.rotation_euler.z=angle;return o
manifest=[];colliders=[]
def mark(name,pos,kind,route='shared',**extra):
    o=bpy.data.objects.new('PL_'+name,None);markers.objects.link(o);o.location=pos;o.empty_display_size=1
    o['kind']=kind;o['route']=route
    for key,value in extra.items():o[key]=value
    manifest.append(dict(name=name,xyz=list(pos),kind=kind,route=route,**extra));return o
def collider(name,size,pos,angle=0,route='shared',kind='BOX'):
    o=box('COL_'+name,size,pos,metal,collision,angle);o.display_type='WIRE';o['primitive']=kind;o['route']=route
    colliders.append(dict(name=name,size=list(size),xyz=list(pos),rotation_z=angle,route=route,primitive=kind))
    return o
def text(body,pos,c,size=.7,angle=0,m=paper):
    d=bpy.data.curves.new('PL_Text','FONT');d.body=body;d.size=size;d.extrude=0
    o=bpy.data.objects.new('PL_Sign_'+body,d);c.objects.link(o);o.location=pos;o.rotation_euler=(math.pi/2,0,angle);d.materials.append(m)
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
    bpy.ops.object.convert(target='MESH');return bpy.context.object
def link(template,name,pos,c,scale=(1,1,1),angle=0):
    o=bpy.data.objects.new('PL_'+name,template.data);c.objects.link(o);o.location=pos;o.scale=scale;o.rotation_euler.z=angle;return o
def join(parts,name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts:o.select_set(True)
    bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();o=parts[0];o.name='PL_'+name
    o.data.transform(Matrix.Translation(o.location));o.location=(0,0,0);return o

# Asset library: irregular opaque silhouettes, 64-triangle mature spruce variants.
templates={}
for variant in range(3):
    vv=[];ff=[];mi=[];rr=random.Random(variant+71)
    def rings(levels,n,slot,offset=(0,0)):
        begin=len(vv)
        for z,r in levels:
            for i in range(n):
                a=i*2*math.pi/n+variant*.3
                f=1+rr.uniform(-.12,.12)
                vv.append((offset[0]+math.cos(a)*r*f,offset[1]+math.sin(a)*r*f,z))
        ff.append(tuple(begin+i for i in reversed(range(n))));mi.append(slot)
        for j in range(len(levels)-1):
            for i in range(n):
                a=begin+j*n+i;b=begin+j*n+(i+1)%n;ff.append((a,b,b+n,a+n));mi.append(slot)
        ff.append(tuple(begin+(len(levels)-1)*n+i for i in range(n)));mi.append(slot)
    rings([(0,.23),(6,.16)],5,0)
    for z,r,top in [(2,2.4,8),(5,1.9,10.5),(8,1.25,13)]:rings([(z,r),(top,.035)],5,1,(rr.uniform(-.3,.3),rr.uniform(-.3,.3)))
    templates['Spruce'+str(variant)]=mesh('ENV_F_Spruce_'+str(variant),vv,ff,[bark,foliage],library,mi)
for variant in range(3):
    vv=[]
    for z,r in [(0,.9),(.75,1.2),(1.7,.5)]:
        for i in range(5):
            a=i*2*math.pi/5;vv.append((math.cos(a)*r,math.sin(a)*r*.7,z))
    ff=[(4,3,2,1,0)]+[(j*5+i,j*5+(i+1)%5,(j+1)*5+(i+1)%5,(j+1)*5+i) for j in range(2) for i in range(5)]+[(10,11,12,13,14)]
    for i,v in enumerate(vv):vv[i]=(v[0]*(1+variant*.12),v[1],v[2]*(1-variant*.1))
    templates['Rock'+str(variant)]=mesh('ENV_F_Rock_'+str(variant),vv,ff,stone,library)
templates['DeadTree']=join([box('DeadTrunk',(.3,.3,7),(-.15,-.15,0),bark,library),
    box('DeadBranch',(.18,.18,3.5),(-.09,-.09,3.5),bark,library)],'ENV_F_DeadTree')
# Tilt one branch in mesh geometry for a crooked, inexpensive dead silhouette.
dead=templates['DeadTree']
for v in dead.data.vertices:
    if v.index>=8:v.co.x+=(v.co.z-3.5)*.55
templates['Log']=box('ENV_F_Log',(5,.65,.55),(-2.5,-.325,0),bark,library)

# Chunk accumulation: source instances stay in a JSON manifest; render meshes batch by 32 m cell.
batch=defaultdict(lambda:[[],[],[]]);instances=[]
def scatter_asset(key,x,y,z,scale=1,angle=0):
    src=templates[key];cell=(int(x//32),int(y//32));v,f,slots=batch[cell]
    start=len(v);rot=Matrix.Rotation(angle,4,'Z')
    for p in src.data.vertices:
        p=rot@(p.co*scale);v.append((p.x+x,p.y+y,p.z+z))
    for p in src.data.polygons:
        f.append(tuple(start+i for i in p.vertices));slots.append(materials.index(src.data.materials[p.material_index]))
    instances.append(dict(asset=key,xyz=[x,y,z],scale=scale,angle=angle,chunk=list(cell)))

# Ground chunks are kept modest for culling and collision iteration.
for cx in range(-192,192,64):
    for cy in range(-32,544,64):
        vs=[(x,y,ground(x,y)) for y in range(cy,cy+65,8) for x in range(cx,cx+65,8)]
        fs=[]
        for j in range(8):
            for i in range(8):
                a=j*9+i;fs.extend([(a,a+1,a+10),(a,a+10,a+9)])
        mesh('Terrain_%s_%s'%(cx,cy),vs,fs,soil,terraincol)

def subdivide(points,step=4):
    result=[]
    for aa,bb in zip(points,points[1:]):
        a,b=Vector(aa),Vector(bb);count=max(1,math.ceil((b-a).length/step))
        result.extend(tuple(a+(b-a)*i/count) for i in range(count))
    return result+[points[-1]]
samples={}
def ribbon(key,c):
    pts=subdivide(paths[key]);samples[key]=pts;vs=[];half=widths[key]/2
    for i,(x,y) in enumerate(pts):
        tangent=(Vector(pts[min(i+1,len(pts)-1)])-Vector(pts[max(0,i-1)])).normalized();n=Vector((-tangent.y,tangent.x))
        for side in (-1,1):
            w=half+(.25*math.sin(i*.7) if key not in ('C',) else 0)
            px,py=x+n.x*w*side,y+n.y*w*side
            vs.append((px,py,zground(py)+.025))
    fs=[(2*i,2*i+2,2*i+3,2*i+1) for i in range(len(pts)-1)]
    o=mesh('Travel_'+key,vs,fs,concrete if key=='C' else dirt,c)
    # Exact render-matched surface guide: custom curved/graded surface cannot be one box.
    co=bpy.data.objects.new('PL_COL_Travel_'+key,o.data.copy());collision.objects.link(co);co.display_type='WIRE';co['collision']='STATIC_SURFACE';co['route']=key
    return o
for key in paths:
    c=shared if key=='Safe' else fork if key.startswith('Fork') else ending if key.startswith('Merge') or key=='Final' else routes[key[-1] if key.startswith(('Exit','Link')) else key]
    ribbon(key,c)

def floor(name,x,y,w,d,c,m=dirt,z=None):
    z=zground(y) if z is None else z
    o=box(name,(w,d,.3),(x-w/2,y,z-.3),m,c)
    collider(name,(w,d,.3),(x-w/2,y,z-.3));return o
floor('Bellevue_Overlook',0,-12,30,28,shared,z=36)
# One graded decision room puts all three physical choices in sight.
hub=[(-7,76),(7,76),(22.04265,94.89335),(17.95735,105.10665),(6.5,108),(-6.5,108),(-17.95735,105.10665),(-22.04265,94.89335)]
hubfloor=mesh('Travel_ForkHub',[(x,y,zground(y)+.025) for x,y in hub],[tuple(range(8))],dirt,fork)
hubco=bpy.data.objects.new('PL_COL_Travel_ForkHub',hubfloor.data.copy());collision.objects.link(hubco);hubco.display_type='WIRE'
for i in (1,3,5,7):
    a,b=Vector(hub[i]),Vector(hub[(i+1)%8]);t=(b-a).normalized();angle=math.atan2(-t.x,t.y)
    pos=a+Matrix.Rotation(angle,2)@Vector((-.6,0));base=min(zground(a.y),zground(b.y))-.2
    box('Fork_RockBoundary',(1.2,(b-a).length,4.5),(pos.x,pos.y,base),stone,fork,angle)
    collider('Fork_Perimeter',(1.2,(b-a).length,4.5),(pos.x,pos.y,base),angle)
floor('City_Overlook',0,508,44,24,ending,z=0)
for k,(x,y,w,d) in pockets.items():floor(k+'_CombatSpace',x,y,w,d,routes[k],concrete if k=='C' else dirt,z=8)

# Continuous visual banks and matching primitive wall guides define the route corridors.
def banks(key,c):
    pts=samples[key];half=widths[key]/2
    for side in (-1,1):
        vs=[]
        for i,(x,y) in enumerate(pts):
            t=(Vector(pts[min(i+1,len(pts)-1)])-Vector(pts[max(0,i-1)])).normalized();n=Vector((-t.y,t.x))*side
            crest=4.2+.65*math.sin(i*.63+side)
            for off,h in [(half+.4,0),(half+1.4,crest),(half+4,crest+.5),(half+6,0)]:
                px,py=x+n.x*off,y+n.y*off
                vs.append((px,py,zground(py)+h))
        fs=[]
        for i in range(len(pts)-1):
            for j in range(3):
                a=i*4+j;f=(a,a+4,a+5,a+1);fs.append(f if side==1 else tuple(reversed(f)))
        mesh('Bank_'+key+'_'+str(side),vs,fs,stone,c)
        # One box per centerline segment, overlapping at joins, taller than jump baseline.
        for i,(aa,bb) in enumerate(zip(pts,pts[1:])):
            a,b=Vector(aa),Vector(bb);t=(b-a).normalized();n=Vector((-t.y,t.x))*side
            p=(a+b)/2+n*(half+2.2);length=(b-a).length+1.2;angle=math.atan2(-t.x,t.y)
            local=Vector((-1,-length/2));r=Matrix.Rotation(angle,2)@local
            collider(key+'_Bank_'+str(side)+'_'+str(i),(2,length,4.2),(p.x+r.x,p.y+r.y,min(zground(a.y),zground(b.y))-.2),angle,key)
for k in ('Safe','LinkA','LinkB','LinkC','A','B','ExitA','ExitB','ExitC'):
    banks(k,shared if k=='Safe' else routes[k[-1] if k.startswith(('Exit','Link')) else k])

# Pocket cliff rims leave broad entrance and exit apertures.
for k,(x,y,w,d) in pockets.items():
    c=routes[k]
    if k!='C':
        for side in (-1,1):
            for j in range(0,int(d),8):
                xx=x+side*(w/2+1.5);h=4+rng.uniform(0,1.6)
                box(k+'_ClearingRockRim',(3,8,h),(xx-1.5,y+j,7.8),stone,c)
                collider(k+'_PocketSide',(3,8,4.8),(xx-1.5,y+j,7.8),route=k)
        for yy in (y,y+d):
            for side in (-1,1):
                left=x-w/2 if side<0 else x+6
                box(k+'_ClearingEndRim',(w/2-6,2,4.4),(left,yy-1,7.8),stone,c)
                collider(k+'_PocketEnd',(w/2-6,2,4.4),(left,yy-1,7.8),route=k)

# Signs, fences and benches keep ordinary details slightly wrong and repetitive.
def fence(a,b,c,height=1.15):
    a,b=Vector(a),Vector(b);t=(b-a).normalized();ang=math.atan2(-t.x,t.y);length=(b-a).length
    for d in (0,length):
        p=a+t*d;box('FencePost',(.14,.14,height),(p.x-.07,p.y-.07,zground(p.y)),bark,c)
    for zz in (.4,.9):
        p=a+Matrix.Rotation(ang,2)@Vector((-.06,0))
        box('FenceRail',(.12,length,.12),(p.x,p.y,zground(a.y)+zz),bark,c,ang)
def lamp(x,y,c,real=False):
    z=zground(y);box('LampPost',(.17,.17,4),(x,y,z),metal,c)
    box('LampHousing',(.65,.5,.45),(x-.24,y-.15,z+3.8),metal,c)
    box('LampFace',(.5,.04,.28),(x-.17,y-.18,z+3.9),warm,c)
    if real:
        d=bpy.data.lights.new('PL_WarmLight','POINT');d.energy=160;d.color=(1,.47,.2);d.shadow_soft_size=1.4
        o=bpy.data.objects.new('PL_WarmLight',d);stage.objects.link(o);o.location=(x,y-1,z+3.4)
    return z
for y in (-9,525):
    for x in (-20,12):fence((x,y),(x+8,y),shared if y<0 else ending)
for x in (-10,7):
    parts=[box('BenchSeat',(3.5,.65,.15),(x,-4,36.65),bark,shared),box('BenchBack',(3.5,.14,.7),(x,-3.45,36.7),bark,shared)]
    for xx in (x+.2,x+3.1):box('BenchLeg',(.17,.5,.65),(xx,-4,36),metal,shared)
box('Intro_Bottle',(.08,.08,.28),(1.7,3,36),paper,shared)
text('BELLEVUE',(7,-1,38),shared,.9)
text('MARIBOR',(8,516,1.4),ending,1)
for x,y,c,lit in [(-12,4,shared,True),(9,82,fork,True),(-118,322,routes['A'],True),(19,318,routes['B'],False),(15,516,ending,True)]:lamp(x,y,c,lit)
for name,pos in [('PlayerStart',(0,16,36)),('Intro_LowCamera',(0,2,36.25)),('Intro_Scooter',(1,4,36)),('Intro_Watch',(0,3,37.4)),('Intro_GuiltSilhouette',(-24,33,39))]:mark(name,pos,'intro')
mark('ExitForest',(0,522,0),'one-way scene transition; persist Forest C for next run')

# Gate assemblies close the complete canyon width. Independent art/blockers/triggers.
gate_records=[]
def gate(k,pos,tangent,which):
    c=routes[k];x,y=pos;z=zground(y);w=widths[k]+3
    t=Vector(tangent).normalized();angle=math.atan2(-t.x,t.y);rot=Matrix.Rotation(angle,2)
    def gbox(name,size,local,m):
        p=Vector((x,y))+rot@Vector(local[:2]);return box(k+'_'+which+'_'+name,size,(p.x,p.y,z+local[2]),m,c,angle)
    for side in (-1,1):gbox('Pillar',(.7,1,6),(side*w/2-.35,-.5,0),concrete)
    gbox('Lintel',(w+.7,1,.6),(-w/2-.35,-.5,5.5),concrete)
    leaf=gbox('OpaqueLeaf',(w,.28,5.5),(-w/2,-.14,0),metal)
    # Authoring view: doors parked open above lintel; source blockers remain available.
    leaf.location.z+=5.7;leaf['closed_z']=z;leaf['state']='AUTHORING_OPEN; runtime state required'
    off=rot@Vector((-w/2,-.2));co=collider(k+'_'+which+'_GateLeaf',(w,.4,5.5),(x+off.x,y+off.y,z),angle,k)
    co['dynamic_gate']=True
    mark(k+'_'+which+'_Gate',(x,y,z),'gate assembly',k,clear_width=w,clear_height=5.5,angle=angle)
    gate_records.append(dict(route=k,which=which,xy=list(pos),width=w))
    if which=='Front':
        mark('F_'+k+'_Selection',(x+t.x*3,y+t.y*3,zground(y+t.y*3)),'selection trigger beyond leaf',k)
        n=rot@Vector((w/2+1,-1))
        box(k+'_SignPost',(.2,.2,3),(x+n.x,y+n.y,z),bark,c)
        # Sign board faces the approach; text uses the same orientation.
        p=Vector((x,y))+rot@Vector((w/2+.3,-1))
        box(k+'_SignBoard',(5,.2,1.3),(p.x,p.y,z+2),metal,c,angle)
        text({'A':'A / GOZDNA STRMINA','B':'B / GONDOLA','C':'C / RAVE TUNEL'}[k],(p.x,p.y-.03,z+2.35),c,.32,angle)
for k in 'ABC':
    p=paths['Link'+k];gate(k,p[0],Vector(p[1])-Vector(p[0]),'Front')
    p=paths['Exit'+k];gate(k,p[-1],Vector(p[-1])-Vector(p[-2]),'Rear')

# Static service kit: 36-triangle pole/cabin, 48-triangle cart.
templates['GondolaPole']=join([box('PoleShaft',(.55,.55,13),(-.275,-.275,0),metal,library),
    box('PoleBeam',(6,.6,.5),(-3,-.3,12.1),metal,library),box('PoleFoot',(1.7,1.3,.5),(-.85,-.65,0),concrete,library)],'ENV_F_GondolaPole')
templates['Cabin']=join([box('CabinShell',(2.4,2.1,1.7),(-1.2,-1.05,-3),red,library),box('CabinWindow',(2.25,1.95,.9),(-1.125,-.975,-1.3),glass,library),box('CabinHanger',(.16,.16,.55),(-.08,-.08,-.5),metal,library)],'ENV_F_GondolaCabin')
templates['Cart']=join([box('CartBody',(2,3,.8),(-1,-1.5,.5),red,library),box('CartRim',(2,.16,.5),(-1,1.25,1.3),metal,library),box('CartAxle',(2.2,.4,.5),(-1.1,-1,0),metal,library),box('CartAxle',(2.2,.4,.5),(-1.1,.7,0),metal,library)],'ENV_F_MaintenanceCart')
def beam(name,a,b,r,m,c):
    a,b=Vector(a),Vector(b);o=box(name,(r,r,(b-a).length),a,m,c)
    o.data.transform(Matrix.Translation((-r/2,-r/2,0)));o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();return o
def pole_x(y):return center_x('B',y)+17 if y<300 else 35
for y in (145,205,265,328,392):
    link(templates['GondolaPole'],'Static_GondolaPole',(pole_x(y),y,zground(y)),routes['B'])
    if y<392:link(templates['Cabin'],'Static_GondolaCabin',(pole_x(y),y+18,zground(y+18)+12.3),routes['B'])
for a,b in zip((145,205,265,328),(205,265,328,392)):
    beam('GondolaCable',(pole_x(a),a,zground(a)+12.8),(pole_x(b),b,zground(b)+12.8),.055,metal,routes['B'])
for off,y in [(-3,172),(3,245),(-21,317)]:
    x=center_x('B',y)+off
    o=link(templates['Cart'],'Abandoned_Cart',(x,y,zground(y)+1.1),routes['B'],angle=.35)
    o.rotation_euler.y=math.pi/2
    collider('Cart',(2.2,3,1.7),(x-1.1,y-1.5,zground(y)),route='B')
# Broad 7 m board decks; one missing board is visual only until jump testing.
for center in (204,266):
    x=center_x('B',center+2.6)
    for i in range(12):
        if i in (5,6):continue
        yy=center+i*.48;z=zground(yy)+.06
        box('BrokenBoard',(7,.43,.16),(x-3.5,yy,z),bark,routes['B'])
    for side in (-1,1):box('DeckBeam',(.22,6,.28),(x+side*3.2,center,zground(center)-.15),bark,routes['B'])
    mark('B_JumpTest_'+str(center),(x,center+2.6,zground(center+2.6)),'jump-gap proposal; continuous safety surface retained','B')
mark('B_Watcher',(pole_x(265),283,zground(283)+10),'Watcher on static gondola; red cue','B')
box('Watcher_RedCue',(.1,.1,.1),(pole_x(265),283,zground(283)+10),warm,routes['B'])

# C tunnel: roof follows the actual slope, with widened side bays and a large rave room.
c=routes['C'];cpts=samples['C'];tunnelvs=[];roofvs=[]
for i,(x,y) in enumerate(cpts):
    t=(Vector(cpts[min(i+1,len(cpts)-1)])-Vector(cpts[max(i-1,0)])).normalized();n=Vector((-t.y,t.x))
    for side in (-1,1):
        p=Vector((x,y))+n*side*4.5
        tunnelvs.extend([(p.x,p.y,zground(p.y)-.2),(p.x,p.y,zground(p.y)+6)])
    roofvs.extend([(x+n.x*4.8,y+n.y*4.8,zground(y+n.y*4.8)+6),(x-n.x*4.8,y-n.y*4.8,zground(y-n.y*4.8)+6)])
wallfaces=[]
for i in range(len(cpts)-1):
    wallfaces.extend([(4*i,4*i+4,4*i+5,4*i+1),(4*i+2,4*i+3,4*i+7,4*i+6)])
def solid_shell(o,thickness):
    mod=o.modifiers.new('Solid shell for inside and outside visibility','SOLIDIFY');mod.thickness=thickness;mod.offset=1
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
    bpy.ops.object.modifier_apply(modifier=mod.name)
solid_shell(mesh('C_TunnelWalls',tunnelvs,wallfaces,concrete,c),.5)
solid_shell(mesh('C_TunnelRoof',roofvs,[(2*i,2*i+1,2*i+3,2*i+2) for i in range(len(cpts)-1)],concrete,roofcol),.35)
for i in range(1,len(cpts)-1,5):
    x,y=cpts[i];t=(Vector(cpts[i+1])-Vector(cpts[i-1])).normalized();n=Vector((-t.y,t.x));angle=math.atan2(-t.x,t.y)
    for side in (-1,1):
        p=Vector((x,y))+n*side*4.35
        box('C_ConcreteRib',(.25,.45,6),(p.x,p.y,zground(y)),concrete,c,angle)
        if side==1:box('C_EmissiveStrip',(.06,.1,2.7),(p.x-.08,p.y-.04,zground(y)+1.4),purple,c)
        else:
            box('C_RavePoster',(.04,1.2,1.7),(p.x+.08,p.y,zground(y)+1.4),paper,c)
    # Dedicated primitive wall guidance; retain ceiling independently.
    for side in (-1,1):
        p=Vector((x,y))+n*side*4.7
        collider('C_WallSegment',(.8,23,6),(p.x-.4,p.y-11,zground(y)-.5),angle,'C')
# Chamber walls and roof. Both entry and exit are 12 m wide.
cx,cy,w,d=pockets['C']
for side in (-1,1):
    # Leave a 7 m grate opening on the east wall.
    if side==-1:spans=[(cy,d)]
    else:spans=[(cy,14),(cy+21,d-21)]
    for yy,ll in spans:
        box('C_RoomWall',(1,ll,7),(cx+side*w/2-.5,yy,7.8),concrete,c)
        collider('C_RoomWall',(1,ll,7),(cx+side*w/2-.5,yy,7.8),route='C')
for yy in (cy,cy+d):
    for side in (-1,1):
        xx=cx-w/2 if side<0 else cx+6
        box('C_ChamberEnd',(w/2-6,1,7),(xx,yy-.5,7.8),concrete,c)
        collider('C_ChamberEnd',(w/2-6,1,7),(xx,yy-.5,7.8),route='C')
box('C_ChamberRoof',(w+1,d+1,.35),(cx-w/2-.5,cy-.5,14.8),concrete,roofcol)
for yy in (cy+6,cy+27,cy+38):
    for side in (-1,1):box('C_RoomPurpleStrip',(.07,.12,3),(cx+side*(w/2-.6),yy,10),purple,c)
# Grate is scenery with a real untraversable blocker and a shallow reveal alcove.
for yy in range(314,322):box('C_GrateBar',(.12,.08,5),(cx+w/2-.1,yy,8),metal,c)
for zz in (8.5,11.5,13):box('C_GrateCrossbar',(.12,7,.1),(cx+w/2-.1,314,zz),metal,c)
collider('C_GrateBlocker',(.8,7,6),(cx+w/2-.4,314,8),route='C')
box('C_GrateAlcoveFloor',(7,7,.3),(cx+w/2,314,7.7),concrete,c)
box('C_GrateAlcoveBack',(.5,7,7),(cx+w/2+7,314,7.8),concrete,c)
for yy in (314,321):box('C_AlcoveSide',(7,.4,7),(cx+w/2,yy,7.8),concrete,c)
mark('C_GrateReveal',(cx+w/2+3,317,8),'authored Guilt appearance behind grate','C')
mark('C_GateStrike',(cx+w/2,317,8),'authored grate strike; suspend ambient Guilt','C')
for i,pos in enumerate([(94,309,8),(106,315,8)],1):mark('C_StrongerRegular_'+str(i),pos,'authored stronger regular enemy','C')
for y in (175,260,322):
    data=bpy.data.lights.new('PL_RaveLight','POINT');data.energy=300;data.color=(.47,.10,.8);data.shadow_soft_size=2
    o=bpy.data.objects.new('PL_RaveLight',data);stage.objects.link(o);o.location=(100 if y>190 else 96,y,zground(y)+3)

# Fixed charges stay well inside the enlarged pockets; avoid central turning floor.
for k,(x,y,w,d) in pockets.items():
    placements=[(x-w*.34,y+8),(x+w*.34,y+8),(x,y+d-8)]
    for i,(xx,yy) in enumerate(placements,1):
        box(k+'_ChargeBase_'+str(i),(1,.85,.85),(xx-.5,yy-.425,8),metal,routes[k])
        box(k+'_ChargeFace_'+str(i),(.7,.06,.5),(xx-.35,yy-.46,8.2),cyan,routes[k])
        mark(k+'_Charge_'+str(i),(xx,yy,8),'stationary rechargeable contact charge',k,guaranteed=k=='C' and i==1)
    mark(k+'_CombatSpace',(x,y+d/2,8),'combat occupancy; free exit',k,width=w,depth=d)
    for i,(xx,yy) in enumerate([(x-w/2+4,y+4),(x+w/2-4,y+4),(x-w/2+4,y+d/2),(x+w/2-4,y+d/2),(x-w/2+4,y+d-4),(x+w/2-4,y+d-4)],1):
        mark(k+'_SpawnCandidate_'+str(i),(xx,yy,8),'offscreen >=8 m; wait if invalid',k)
    for i,(xx,yy) in enumerate([(x,y+4),(x+w*.3,y+d/2),(x,y+d-4)],1):mark(k+'_GuiltAnchor_'+str(i),(xx,yy,8),'offscreen behind Boki; candidate only',k)
mark('A_AuthoredGuilt',(-100,357,8),'authored behind-player reveal; suspend ambient pursuit','A')

# Sparse uncanny details. Repeated empty noticeboards, inward-facing benches,
# disconnected fencing and dead trees add discomfort without new game rules.
for k,x,y in [('A',-127,315),('A',-73,340),('B',-23,340),('C',80,337)]:
    c=routes[k];box('NoticePost',(.16,.16,2.5),(x,y,8),bark,c)
    box('EmptyNoticeBoard',(2.5,.15,1.4),(x-1.2,y,9.4),metal,c)
    box('BleachedPaper',(1.8,.02,.9),(x-.9,y-.025,9.6),paper,c)
for x,y in [(-121,246),(-83,203),(-107,279)]:
    key='A';d,point,_=nearest(x,y)
    if d<4:
        away=(Vector((x,y))-point).normalized();p=point+away*8;x,y=p
    link(templates['Log'],'Fallen_Log',(x,y,zground(y)),routes['A'],angle=.45)
    collider('Log',(5,.8,.7),(x-2.5,y-.4,zground(y)),route='A')

# Forest placement respects full lanes, pocket extents, gates and shared spaces.
def allowed(x,y,margin=0):
    if in_pocket(x,y,6+margin):return False
    if abs(x)<26+margin and (y<24 or y>486):return False
    d,q,key=nearest(x,y)
    return d>5+margin
for key,pts in samples.items():
    if key.startswith(('Fork','Merge')) or key in ('Final',):continue
    for i in range(1,len(pts)-1,3):
        x,y=pts[i];t=(Vector(pts[i+1])-Vector(pts[i-1])).normalized();n=Vector((-t.y,t.x))
        for side in (-1,1):
            for extra in (8,15):
                p=Vector((x,y))+n*side*(widths[key]/2+extra+rng.uniform(-1,1))
                if not allowed(p.x,p.y):continue
                s=rng.uniform(.85,1.45)
                scatter_asset('Spruce'+str(rng.randrange(3)),p.x,p.y,ground(p.x,p.y),s,rng.random()*6.28)
                if extra==8 and i%2:scatter_asset('Rock'+str(rng.randrange(3)),p.x+1,p.y+2,ground(p.x+1,p.y+2),rng.uniform(1.1,2),rng.random()*6.28)
placed=0
for attempt in range(7000):
    if placed>=1450:break
    x=rng.uniform(-180,180);y=rng.uniform(-20,535)
    if not allowed(x,y,2):continue
    # Keep a few irregular clear patches to avoid a uniform plantation.
    if math.sin(x*.035+y*.014)>.85 and rng.random()<.8:continue
    key='DeadTree' if rng.random()<.09 else 'Spruce'+str(rng.randrange(3))
    scatter_asset(key,x,y,ground(x,y),rng.uniform(.8,1.5),rng.random()*6.28);placed+=1
for k,(x,y,w,d) in pockets.items():
    if k=='C':continue
    for side in (-1,1):
        for yy in range(int(y)-8,int(y+d)+9,7):
            xx=x+side*(w/2+rng.uniform(7,12));scatter_asset('Spruce'+str(rng.randrange(3)),xx,yy,ground(xx,yy),rng.uniform(1.1,1.6),rng.random()*6.28)
for (cx,cy),(v,f,slots) in batch.items():
    # Only actually used materials per chunk, keeping renderer slots small.
    used=sorted(set(slots));remap={value:i for i,value in enumerate(used)}
    mesh('ForestChunk_%s_%s'%(cx,cy),v,f,[materials[i] for i in used],forestcol,[remap[i] for i in slots])

# Layered opaque distant silhouettes: city and mountain shapes, no expensive distant city.
for i in range(70):
    x=-205+i*6;h=rng.uniform(5,18)
    box('Maribor_Silhouette',(rng.uniform(3,6),.5,h),(x,645,-18),black,terraincol)
for side in (-1,1):
    vv=[]
    for i in range(18):
        y=-50+i*38;x=side*(215+12*math.sin(i*.6));vv.extend([(x,y,-20),(x,y,30+25*math.sin(i*.55)**2)])
    mesh('Distant_Ridge_'+str(side),vv,[(2*i,2*i+1,2*i+3,2*i+2) for i in range(17)],black,terraincol)

# One directional light, seven local lights total in this preview scene.
world=bpy.data.worlds.new('PL_World');world.use_nodes=True;scene.world=world
bg=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND')
bg.inputs['Color'].default_value=(.20,.25,.28,1);bg.inputs['Strength'].default_value=.65
ld=bpy.data.lights.new('PL_OvercastSun','SUN');ld.energy=1.6;ld.angle=.18;ld.color=(.73,.80,.87)
sun=bpy.data.objects.new('PL_OvercastSun',ld);stage.objects.link(sun);sun.rotation_euler=(.55,-.35,-.7)
cd=bpy.data.cameras.new('PL_Camera');cam=bpy.data.objects.new('PL_Camera',cd);stage.objects.link(cam);scene.camera=cam
scene.render.resolution_x=1600;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
cam.location=(480,-330,490);cam.rotation_euler=(Vector((0,250,8))-cam.location).to_track_quat('-Z','Y').to_euler();cd.type='ORTHO';cd.ortho_scale=650
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        area.spaces.active.region_3d.view_perspective='CAMERA';area.spaces.active.clip_end=2000
cam.data.clip_end=2000

route_stats={}
for k in 'ABC':
    keys=['Safe','Fork'+k,'Link'+k,k,'Exit'+k,'Merge'+k,'Final']
    length=sum(sum((Vector(b)-Vector(a)).length for a,b in zip(paths[key],paths[key][1:])) for key in keys)+pockets[k][3]
    route_stats[k]=dict(centerline_m=round(length,1),lane_width_m=widths[k],combat_dimensions_m=list(pockets[k][2:]))
report=dict(scene=scene.name,route_stats=route_stats,source_instances=len(instances),forest_chunks=len(batch),
    terrain_footprint_m=[384,576],markers=len(manifest),primitive_collider_guides=len(colliders),
    geometry_status='Authoring build; Unity import/controller/hardware validation pending')
(OUT/'layout.json').write_text(json.dumps(dict(paths=paths,widths=widths,pockets=pockets,stats=report),indent=2)+'\n')
(OUT/'markers.json').write_text(json.dumps(manifest,indent=2)+'\n')
(OUT/'collision-guides.json').write_text(json.dumps(colliders,indent=2)+'\n')
(OUT/'forest-instances.json').write_text(json.dumps(instances,indent=2)+'\n')
bpy.ops.object.select_all(action='DESELECT')
exec(compile((OUT/'grade_surfaces.py').read_text(),'grade_surfaces.py','exec'))
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Source/Pohorje_Large.blend'))
print(json.dumps(report,indent=2))
