"""Replace the delivered environment with a compact, action-first arena layout.

Run inside a background Blender opened on the previous delivery. Preserves the
module library, packed materials and other Blender scenes. No Unity edits.
"""
import bpy, math, random, json
from pathlib import Path
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
OUT=Path(__file__).resolve().parent
scene=bpy.data.scenes['Pohorje_Large'];bpy.context.window.scene=scene
if scene.get('action_layout'):raise RuntimeError('Action layout already built; refusing to overwrite edits.')
rng=random.Random(7582)
M={key:next(m for m in bpy.data.materials if 'PL_'+key in m.name) for key in ('Bark','Rock','Soil','Path','Concrete','Metal','ServiceRed','WarmEmission','ChargeEmission','RaveEmission','Foliage')}
library=bpy.data.collections['PL_Module_Library']
templates={o.name.removeprefix('PL_ENV_F_'):o for o in library.objects if o.type=='MESH'}
for o in list(scene.objects):
    if o not in list(library.objects):bpy.data.objects.remove(o,do_unlink=True)
cols={c.name:c for c in scene.collection.children}
for name in ('Shared_Bellevue','Shared_Fork','Shared_Overlook','Route_A','Route_B','Route_C','Terrain_Chunks','Forest_Chunks','C_Removable_Roofs','Collision_Guides','Gameplay_Markers','Presentation'):
    if 'PL_'+name not in cols:
        c=bpy.data.collections.new('PL_'+name);scene.collection.children.link(c);cols[c.name]=c
    cols['PL_'+name].hide_viewport=False
collision=cols['PL_Collision_Guides'];markerscol=cols['PL_Gameplay_Markers'];stage=cols['PL_Presentation']
collision.hide_render=True;markerscol.hide_render=True;library.hide_render=True
paths={
 'Safe':[(0,-14),(0,-6)],
 'ForkA':[(0,-6),(-10,-1)],'ForkB':[(0,-6),(10,-1)],'ForkC':[(0,-6),(0,4)],
 'A':[(-10,-1),(-25,5),(-41,12),(-52,22)],
 'B':[(10,-1),(25,5),(41,12),(52,22)],
 'C':[(0,4),(0,16),(0,32)],
 'ExitA':[(-52,86),(-44,95),(-22,102),(0,106)],
 'ExitB':[(52,82),(44,95),(22,102),(0,106)],
 'ExitC':[(0,84),(0,96),(0,106)],'Final':[(0,106),(0,116)]}
pockets={'A':[-64,22,56,64],'B':[64,22,52,60],'C':[0,32,48,52]}
widths={k:10 if k in ('Safe','Final') else 9 for k in paths}
manifest=[];guides=[];instances=[];features=[]

def mesh(name,verts,faces,col,mat):
    data=bpy.data.meshes.new(name);data.from_pydata(verts,[],faces);data.update()
    for m in (mat if isinstance(mat,list) else [mat]):data.materials.append(m)
    uv=data.uv_layers.new(name='UVMap')
    for loop in data.loops:
        v=data.vertices[loop.vertex_index].co;uv.data[loop.index].uv=(v.x/4,v.y/4+v.z/5)
    obj=bpy.data.objects.new(name,data);col.objects.link(obj);return obj

def guide(obj,k,kind='BOX',dynamic=False):
    data=obj.data.copy();data.transform(obj.matrix_world)
    co=bpy.data.objects.new('PL_COL_'+obj.name.removeprefix('PL_'),data);collision.objects.link(co)
    co.display_type='WIRE';co['route']=k;co['primitive']=kind
    if dynamic:co['dynamic_gate']=True
    return co

def box(name,pos,size,col,mat,k=None):
    x,y,z=pos;w,d,h=size
    verts=[(x+i*w/2,y+j*d/2,z+t*h) for t in (0,1) for j in (-1,1) for i in (-1,1)]
    faces=[(0,2,3,1),(4,5,7,6),(0,1,5,4),(2,6,7,3),(0,4,6,2),(1,3,7,5)]
    o=mesh('PL_'+name,verts,faces,col,mat)
    if k is not None:guide(o,k)
    if col==collision:o['primitive']='BOX';o['route']=name[0] if name[0] in 'ABC' else 'shared'
    return o

def beam(name,a,b,r,col,mat,k=None,sides=6):
    a,b=Vector(a),Vector(b);axis=(b-a).normalized();u=axis.cross(Vector((0,0,1)))
    if u.length<.1:u=axis.cross(Vector((0,1,0)))
    u.normalize();v=axis.cross(u);verts=[]
    for center in (a,b):
        for i in range(sides):verts.append(tuple(center+r*(u*math.cos(i*math.tau/sides)+v*math.sin(i*math.tau/sides))))
    faces=[tuple(reversed(range(sides))),tuple(range(sides,sides*2))]
    faces.extend((i,(i+1)%sides,(i+1)%sides+sides,i+sides) for i in range(sides))
    o=mesh('PL_'+name,verts,faces,col,mat)
    if k is not None:guide(o,k,'FIT_CAPSULE')
    return o

def sign_text(body,pos,col,mat):
    data=bpy.data.curves.new('RouteSign','FONT');data.body=body;data.size=.35
    obj=bpy.data.objects.new('PL_RouteSign_'+body,data);col.objects.link(obj);data.materials.append(mat)
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    bpy.ops.object.convert(target='MESH')
    obj.data.transform(Matrix.Translation(pos)@Matrix.Rotation(math.pi/2,4,'X'))
    return obj

def mark(name,pos,kind,k='shared',**props):
    o=bpy.data.objects.new('PL_'+name,None);markerscol.objects.link(o);o.location=pos
    o.empty_display_size=.7;o['kind']=kind;o['route']=k
    for key,value in props.items():o[key]=value
    manifest.append(dict(name=name,xyz=list(pos),kind=kind,route=k,**props));return o

def asset(name,key,pos,scale,col,angle=0):
    o=bpy.data.objects.new('PL_'+name,templates[key].data);col.objects.link(o)
    o.location=pos;o.scale=scale;o.rotation_euler.z=angle
    return o

def smooth(t):t=max(0,min(1,t));return t*t*(3-2*t)
def patch(x,y,cx,cy,w,d,feather):
    return smooth(min((w/2-abs(x-cx))/feather,(d/2-abs(y-cy))/feather))
def ground(k,x,y):
    cx,cy,w,d=pockets[k];xx=x-cx;yy=y-cy
    edge=smooth(min((w/2-abs(xx))/5,yy/6,(d-yy)/6))
    if k=='A':h=3.8*patch(xx,yy,-14,36,32,44,14)-1.7*patch(xx,yy,4,32,20,40,10)
    elif k=='B':h=3.4*patch(xx,yy,-13,42,27,34,12)-1.6*patch(xx,yy,10,25,19,34,10)
    else:h=-2.5*patch(xx,yy,0,27,34,40,10)+2.4*patch(xx,yy,0,43,34,17,7)
    return 8+h*edge

pit_mat=bpy.data.materials.get('Action_PitConcrete') or bpy.data.materials.new('Action_PitConcrete')
pit_mat.diffuse_color=(.065,.075,.095,1);pit_mat.use_nodes=True
principled=next(n for n in pit_mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
principled.inputs['Base Color'].default_value=pit_mat.diffuse_color
principled.inputs['Roughness'].default_value=.85
floor_trees={}
for k,(cx,cy,w,d) in pockets.items():
    col=cols['PL_Route_'+k];nx=math.ceil(w/1.5);ny=math.ceil(d/1.5)
    verts=[(cx-w/2+w*i/nx,cy+d*j/ny,ground(k,cx-w/2+w*i/nx,cy+d*j/ny)) for j in range(ny+1) for i in range(nx+1)]
    faces=[]
    for j in range(ny):
        for i in range(nx):
            a=j*(nx+1)+i;b=a+1;c=a+nx+1;e=c+1
            faces.extend([(a,b,e),(a,e,c)])
    mats=[M['Concrete'],M['Concrete'],pit_mat] if k=='C' else [M['Path'],M['Rock'],M['Soil']]
    o=mesh('PL_'+k+'_CombatSpace',verts,faces,col,mats)
    for f in o.data.polygons:
        h=sum(o.data.vertices[i].co.z for i in f.vertices)/len(f.vertices)
        if k=='C':
            cell=f.index//2;i=cell%nx;j=cell//nx
            x=cx-w/2+w*(i+.5)/nx;y=d*(j+.5)/ny
            f.material_index=2 if abs(x-cx)<12 and 16<y<36 else 0
        else:f.material_index=1 if h>9.2 else 2 if h<7 else 0
    guide(o,k,'STATIC_SURFACE')
    floor_trees[k]=BVHTree.FromPolygons(verts,faces)

def floor_z(k,x,y):
    hit=floor_trees[k].ray_cast(Vector((x,y,40)),Vector((0,0,-1)),80)[0]
    return hit.z if hit else 8

def ramp(name,lower_a,lower_b,upper_a,upper_b,k,col,mat):
    verts=[lower_a,lower_b,upper_b,upper_a]
    faces=[(0,1,2),(0,2,3)]
    if (Vector(lower_b)-Vector(lower_a)).cross(Vector(upper_b)-Vector(lower_a)).z<0:faces=[tuple(reversed(f)) for f in faces]
    o=mesh('PL_'+name,verts,faces,col,mat);guide(o,k,'STATIC_SURFACE');return o

# Broad short lanes. Overlapping segment rectangles and round joins prevent seams.
for key,pts in paths.items():
    k=key[-1] if key[-1] in 'ABC' else 'shared'
    col=cols['PL_Route_'+k] if k!='shared' and not key.startswith('Fork') else cols['PL_Shared_Fork' if key.startswith('Fork') else 'PL_Shared_Overlook' if key=='Final' else 'PL_Shared_Bellevue']
    verts=[];faces=[];half=widths[key]/2
    for aa,bb in zip(pts,pts[1:]):
        a,b=Vector(aa),Vector(bb);t=(b-a).normalized();n=Vector((-t.y,t.x))*half
        q=len(verts);verts.extend([(p.x,p.y,8.025) for p in (a-n,b-n,b+n,a+n)]);faces.append(tuple(range(q,q+4)))
    for x,y in pts:
        q=len(verts);verts.append((x,y,8.025))
        for i in range(16):verts.append((x+half*math.cos(i*math.tau/16),y+half*math.sin(i*math.tau/16),8.025))
        faces.extend((q,q+i+1,q+(i+1)%16+1) for i in range(16))
    o=mesh('PL_Travel_'+key,verts,faces,col,M['Concrete'] if k=='C' else M['Path']);guide(o,k,'STATIC_SURFACE')
# Compact shared start, decision pad and end.
for name,x,y,w,d,c in [('Bellevue',0,-17,20,12,'Shared_Bellevue'),('Decision',0,-2,31,17,'Shared_Fork'),('Overlook',0,116,24,12,'Shared_Overlook')]:
    o=box(name,(x,y,7.7),(w,d,.3),cols['PL_'+c],M['Path'],'shared')

# Room rims: primitive walls with irregular low-poly rock skin. Two full-width exits.
for k,(cx,cy,w,d) in pockets.items():
    col=cols['PL_Route_'+k];entry=cx+12 if k=='A' else cx-12 if k=='B' else cx
    for side in (-1,1):
        if k=='C':
            spans=[(cy,d)] if side<0 else [(cy,22),(cy+29,d-29)]
            for start,length in spans:box(k+'_RoomSide',(cx+side*w/2,start+length/2,7.7),(1.4,length,11.3),col,M['Concrete'],k)
        else:
            box(k+'_Boundary',(cx+side*(w/2+1),cy+d/2,7),(2,d,5),col,M['Rock'],k)
            for j in range(0,int(d),6):
                x=cx+side*(w/2+rng.uniform(.5,2));y=cy+j+3
                asset(k+'_RimRock','Rock_'+str(j%3),(x,y,7.5),(2.8,3.5,rng.uniform(3,5)),col,rng.uniform(-.3,.3))
    for yy in (cy,cy+d):
        for lo,hi in [(cx-w/2,entry-6),(entry+6,cx+w/2)]:
            if hi-lo>0:box(k+'_EndWall',((lo+hi)/2,yy,7.7),(hi-lo,1.6,9.5 if k=='C' else 4.8),col,M['Concrete'] if k=='C' else M['Rock'],k)

# A: a wooded quarry hollow, rock island, bridge and a towering fallen spruce.
k='A';cx,cy,w,d=pockets[k];col=cols['PL_Route_A']
for i,(xx,yy,s) in enumerate([(3,38,3),(-16,43,2.8),(19,46,2.0)]):
    z=floor_z(k,cx+xx,cy+yy)
    asset('A_RockIsland_'+str(i),'Rock_'+str(i%3),(cx+xx,cy+yy,z-.2),(s,s*1.35,s*1.8),col,.3*i)
    box('A_IslandGuide_'+str(i),(cx+xx,cy+yy,z),(s*1.6,s*1.8,s*1.8),collision,M['Rock'])['primitive']='BOX'
    # The independently fitted rock blockers stay simple boxes.
    o=list(collision.objects)[-1];o['route']='A'
# Six-meter bridge crosses the shallow channel; open riding loops run around it.
left=cx-7;right=cx+15;bridge_y=cy+28
hl=max(floor_z(k,left,bridge_y+off) for off in (-3,0,3))+.08
hr=max(floor_z(k,right,bridge_y+off) for off in (-3,0,3))+.08
def bridge_z(x):return hl+(hr-hl)*(x-left)/(right-left)
for i in range(16):
    a=left+(right-left)*i/16;b=left+(right-left)*(i+.94)/16
    verts=[(a,bridge_y-3,bridge_z(a)),(b,bridge_y-3,bridge_z(b)),(b,bridge_y+3,bridge_z(b)),(a,bridge_y+3,bridge_z(a))]
    mesh('PL_A_BridgePlank_'+str(i),verts,[(0,1,2,3)],col,M['Bark'])
o=mesh('PL_A_BridgeSurface',[(left,bridge_y-3,hl),(right,bridge_y-3,hr),(right,bridge_y+3,hr),(left,bridge_y+3,hl)],[(0,1,2),(0,2,3)],col,M['Bark'])
o.hide_render=True;guide(o,'A','STATIC_SURFACE')
for side in (-1,1):
    beam('A_BridgeRail',(left,bridge_y+side*2.8,hl+1),(right,bridge_y+side*2.8,hr+1),.1,col,M['Bark'])
    for x in (left,right):beam('A_BridgePost',(x,bridge_y+side*2.8,bridge_z(x)),(x,bridge_y+side*2.8,bridge_z(x)+1.1),.12,col,M['Bark'])
ramp('A_BridgeEntryRamp',(left-4,bridge_y-3,floor_z('A',left-4,bridge_y-3)),(left-4,bridge_y+3,floor_z('A',left-4,bridge_y+3)),(left,bridge_y-3,hl),(left,bridge_y+3,hl),'A',col,M['Bark'])
ramp('A_BridgeExitRamp',(right+4,bridge_y-3,floor_z('A',right+4,bridge_y-3)),(right+4,bridge_y+3,floor_z('A',right+4,bridge_y+3)),(right,bridge_y-3,hr),(right,bridge_y+3,hr),'A',col,M['Bark'])
# Fallen landmark remains high enough to ride under, supported by snapped trunks.
for xx in (-18,17):beam('A_SnappedTrunk',(cx+xx,cy+47,floor_z(k,cx+xx,cy+47)),(cx+xx,cy+47,17),1.0,col,M['Bark'],'A')
beam('A_FallenSpruce',(cx-19,cy+47,17),(cx+18,cy+49,16.3),1.1,col,M['Bark'])
for i in range(9):
    x=cx-14+i*3.5;beam('A_DeadBranch_'+str(i),(x,cy+47.5,17),(x-2,cy+52,19+i%2),.18,col,M['Bark'])
for xx,yy in [(-19,14),(17,18),(-18,54)]:
    for j in range(3):
        z=floor_z(k,cx+xx,cy+yy)+j*.48
        beam('A_LogPile',(cx+xx-3,cy+yy+j*.6,z+.3),(cx+xx+3,cy+yy+j*.6,z+.3),.3,col,M['Bark'],'A')
features.extend([dict(route='A',name='Rock island / split riding loop',xy=[cx+3,cy+38]),dict(route='A',name='Timber bridge / low channel',xy=[cx,cy+28]),dict(route='A',name='Fallen spruce arch',xy=[cx,cy+48])])

# B: raised gondola terminal, two accessible ramps and a loop through its legs.
k='B';cx,cy,w,d=pockets[k];col=cols['PL_Route_B']
deck_z=14
box('B_TerminalDeck',(cx-9,cy+36,deck_z-.5),(14,14,.5),col,M['Concrete'],'B')
for xx in (-15,-3):
    for yy in (30,42):
        z=floor_z(k,cx+xx,cy+yy)
        box('B_TerminalLeg',(cx+xx,cy+yy,z),(1,1,deck_z-z),col,M['ServiceRed'],'B')
ramp('B_FrontRamp',(cx-12,cy+13,floor_z(k,cx-12,cy+13)),(cx-6,cy+13,floor_z(k,cx-6,cy+13)),(cx-12,cy+29,deck_z),(cx-6,cy+29,deck_z),'B',col,M['Metal'])
ramp('B_SideRamp',(cx+12,cy+33,floor_z(k,cx+12,cy+33)),(cx+12,cy+39,floor_z(k,cx+12,cy+39)),(cx-2.25,cy+33,deck_z),(cx-2.25,cy+39,deck_z),'B',col,M['Metal'])
for side in (-1,1):
    for j in range(4):box('B_HazardStripe',(cx-12+side*.25+j*1.7,cy+29,14.04),(.8,.7,.05),col,M['WarmEmission'])
for yy in (13,47):
    for xx in (-20,18):box('B_GantryFoot',(cx+xx,cy+yy,8),(.6,.8,14),col,M['Metal'],'B')
    beam('B_GantryBeam',(cx-20,cy+yy,21.5),(cx+18,cy+yy,21.5),.45,col,M['ServiceRed'])
    beam('B_GantryBrace',(cx-20,cy+yy,18),(cx-15,cy+yy,21.5),.2,col,M['Metal'])
for xx in (-5,8):
    beam('B_StaticCable',(cx+xx,cy-3,22),(cx+xx,cy+d+8,22),.045,col,M['Metal'])
    for yy in (18,46):asset('B_HangingCabin','GondolaCabin',(cx+xx,cy+yy,22),(1.6,1.6,1.6),col)
for xx,yy in [(15,18),(14,45),(-17,16)]:
    z=floor_z('B',cx+xx,cy+yy)
    asset('B_Cart','MaintenanceCart',(cx+xx,cy+yy,z),(1.5,1.5,1.5),col,.6)
    box('B_CartGuide',(cx+xx,cy+yy,z),(3.2,4.5,2),collision,M['Metal'])['primitive']='BOX'
features.extend([dict(route='B',name='Elevated terminal / two ramps',xy=[cx-9,cy+36]),dict(route='B',name='Gantry and suspended gondolas',xy=[cx,cy+20])])

# C: compact cistern rave, a sunken dance floor, back stage and concrete pillars.
k='C';cx,cy,w,d=pockets[k];col=cols['PL_Route_C'];roofcol=cols['PL_C_Removable_Roofs']
box('C_RemovableRoomRoof',(cx,cy+d/2,19),(w+2,d+2,.5),roofcol,M['Concrete'])
# Short entry tunnel frames the reveal rather than consuming a long corridor.
for side in (-1,1):box('C_EntryTunnelWall',(side*5.5,21,7.8),(1,22,9),col,M['Concrete'],'C')
box('C_EntryTunnelRoof',(0,21,16.8),(12,22,.5),roofcol,M['Concrete'])
for xx in (-17,17):
    for yy in (13,36):
        z=floor_z(k,xx,cy+yy)
        box('C_CisternColumn',(xx,cy+yy,z),(2.2,2.2,19-z),col,M['Concrete'],'C')
        box('C_ColumnNeon',(xx,cy+yy-1.13,z+1),(.18,.08,6),col,M['RaveEmission'])
# Raised DJ stage is reached by two broad ramps; the exit remains independently open.
box('C_Stage',(0,cy+42,10.5),(18,8,.5),col,M['Metal'],'C')
for side in (-1,1):
    ramp('C_StageRamp',(side*15,cy+39,floor_z('C',side*15,cy+39)),(side*15,cy+45,floor_z('C',side*15,cy+45)),(side*8.75,cy+39,11),(side*8.75,cy+45,11),'C',col,M['Concrete'])
    box('C_SpeakerTower',(side*10,cy+43,11),(2.6,2.4,4),col,M['Metal'],'C')
    for zz in (12,13.5):
        beam('C_SpeakerCone',(side*10,cy+41.75,zz),(side*10,cy+41.6,zz),.65,col,M['Concrete'],sides=8)
box('C_DJBooth',(0,cy+44,11),(5,1.4,1.2),col,M['ServiceRed'],'C')
for i in range(9):
    x=-12+i*3;y=cy+20+(i%2)*5;z=floor_z('C',x,y)+.04
    beam('C_DanceFloorStripe',(x,y,z),(x+1.5,y+2,floor_z('C',x+1.5,y+2)+.04),.045,col,M['RaveEmission'])
# Grate reveal kept on the east wall, with a separate shallow alcove.
for yy in range(int(cy+22),int(cy+29)):
    beam('C_GrateBar',(w/2-.85,yy,8),(w/2-.85,yy,14),.065,col,M['Metal'])
box('C_GrateAlcove',(w/2+3,cy+25,7.7),(6,7,.3),col,M['Concrete'])
box('C_AlcoveBack',(w/2+6,cy+25,8),(1,7,9),col,M['Concrete'],'C')
for yy in (cy+21.5,cy+28.5):box('C_AlcoveSide',(w/2+3,yy,8),(6,1,9),col,M['Concrete'],'C')
box('C_GrateBlocker',(w/2,cy+25,8),(.5,7,7),collision,M['Metal'])
mark('C_GrateReveal',(w/2+3,cy+25,8),'authored Guilt appearance behind grate','C')
mark('C_GateStrike',(w/2,cy+25,8),'authored grate strike; suspend ambient Guilt','C')
features.extend([dict(route='C',name='Sunken dance floor',xy=[0,cy+25]),dict(route='C',name='Raised DJ stage / speaker towers',xy=[0,cy+43])])

# Gates and markers preserve selection/rear sealing semantics. Early enemies are
# candidate positions only, to be bound to the existing encounter system in Unity.
for k,(cx,cy,w,d) in pockets.items():
    col=cols['PL_Route_'+k];front=paths['Fork'+k][-1];rear=paths['Exit'+k][1]
    for which,xy in [('Front',front),('Rear',rear)]:
        before=set(scene.objects)
        x,y=xy;leaf=box(k+'_'+which+'_GateLeaf',(x,y,14),(10,.35,5.5),col,M['Metal'])
        leaf['closed_z']=8;leaf['state']='AUTHORING_OPEN; runtime required'
        blocker=box(k+'_'+which+'_GateBlocker',(x,y,8),(10,.4,5.5),collision,M['Metal'])
        blocker['dynamic_gate']=True;blocker['primitive']='BOX';blocker['route']=k
        for side in (-1,1):box(k+'_GatePillar',(x+side*5.5,y,8),(.8,1,6.5),col,M['Concrete'],k)
        if which=='Front':
            box(k+'_RouteSignBoard',(x,y-.5,13.5),(9,.2,1.1),col,M['Metal'])
            sign_text({'A':'A / GOZDNA STRMINA','B':'B / GONDOLA','C':'C / RAVE TUNEL'}[k],(x-3.7,y-.62,13.8),col,M['RaveEmission'] if k=='C' else M['WarmEmission'])
        p=paths[k] if which=='Front' else paths['Exit'+k]
        tangent=(Vector(p[1])-Vector(p[0])).normalized() if which=='Front' else (Vector(p[2])-Vector(p[0])).normalized()
        angle=math.atan2(-tangent.x,tangent.y);rot=Matrix.Rotation(angle,2)
        for o in set(scene.objects)-before:
            for v in o.data.vertices:
                q=rot@Vector((v.co.x-x,v.co.y-y));v.co.x=x+q.x;v.co.y=y+q.y
            o.data.update()
        mark(k+'_'+which+'_Gate',(x,y,8),'gate assembly',k,clear_width=10,clear_height=5.5,angle=angle)
    x,y=front;mark('F_'+k+'_Selection',(x,y+2,8),'selection trigger beyond leaf',k)
    t=(Vector(paths[k][1])-Vector(paths[k][0])).normalized();x,y=Vector(paths[k][0])+t*5;mark(k+'_FirstEncounter',(x,y,8),'early regular encounter candidate; activate only after route selection',k)
    charges=[(cx-w*.32,cy+10),(cx+w*.32,cy+d*.65),(cx,cy+d-7)]
    for i,(x,y) in enumerate(charges,1):
        z=floor_z(k,x,y);box(k+'_ChargeBase_'+str(i),(x,y,z),(1,.9,.8),col,M['Metal'])
        box(k+'_ChargeFace_'+str(i),(x,y-.47,z+.2),(.7,.06,.5),col,M['ChargeEmission'])
        mark(k+'_Charge_'+str(i),(x,y,z),'stationary rechargeable contact charge',k,guaranteed=k=='C' and i==1)
    mark(k+'_CombatSpace',(cx,cy+d/2,8),'combat occupancy; free exit',k,width=w,depth=d,height_range_m=[5,16])
    for i,(x,y) in enumerate([(cx-w/2+6,cy+8),(cx+w/2-6,cy+8),(cx-w/2+6,cy+d-8),(cx+w/2-6,cy+d-8)],1):
        mark(k+'_SpawnCandidate_'+str(i),(x,y,floor_z(k,x,y)),'offscreen >=8 m; wait if invalid',k)
    for i,(x,y) in enumerate([(cx-w*.25,cy+18),(cx+w*.25,cy+d-14)],1):mark(k+'_GuiltAnchor_'+str(i),(x,y,floor_z(k,x,y)),'offscreen behind Boki; candidate only',k)
mark('A_AuthoredGuilt',(-52,83,8),'authored behind-player reveal; suspend ambient pursuit','A')
for i,(x,y) in enumerate([(-7,49),(7,58)],1):mark('C_StrongerRegular_'+str(i),(x,y,floor_z('C',x,y)),'authored stronger regular enemy','C')
mark('B_Watcher',(59,68,21),'Watcher on static gondola; red cue','B')
for name,pos in [('PlayerStart',(0,-14,8)),('Intro_LowCamera',(0,-18,8.25)),('Intro_Scooter',(2,-18,8)),('Intro_Watch',(0,-18,9.4)),('Intro_GuiltSilhouette',(-18,-14,12))]:mark(name,pos,'intro')
mark('ExitForest',(0,118,8),'one-way scene transition; persist Forest C for next run')

# Dense, irregular scenic forest outside arenas/lanes. Bake into 24 m cells.
def near_lane(x,y,extra=0):
    p=Vector((x,y))
    for key,pts in paths.items():
        for aa,bb in zip(pts,pts[1:]):
            a,b=Vector(aa),Vector(bb);v=b-a;q=a+max(0,min(1,(p-a).dot(v)/v.length_squared))*v
            if (p-q).length<widths[key]/2+extra:return True
    return False
def arena_near(x,y,margin):return any(abs(x-cx)<w/2+margin and cy-margin<y<cy+d+margin for cx,cy,w,d in pockets.values())
batch={}
for attempt in range(5000):
    if len(instances)>=620:break
    x=rng.uniform(-109,109);y=rng.uniform(-19,129)
    if arena_near(x,y,3) or near_lane(x,y,4) or (abs(x)<18 and (y<8 or y>104)):continue
    key='Spruce_'+str(rng.randrange(3));s=rng.uniform(.8,1.55);angle=rng.random()*math.tau
    data=templates[key].data;rot=Matrix.Rotation(angle,4,'Z');trans=Matrix.Translation((x,y,7.5));scale=Matrix.Diagonal((s,s,s,1));mat=trans@rot@scale
    cell=(math.floor(x/24),math.floor(y/24));verts,faces,slots=batch.setdefault(cell,([],[],[]));start=len(verts)
    verts.extend(tuple(mat@v.co) for v in data.vertices)
    for f in data.polygons:faces.append(tuple(start+i for i in f.vertices));slots.append(f.material_index)
    instances.append(dict(asset=key,xyz=[x,y,7.5],scale=s,angle=angle))
materials=list(templates['Spruce_0'].data.materials)
for cell,(verts,faces,slots) in batch.items():
    o=mesh('PL_ForestChunk_'+str(cell),verts,faces,cols['PL_Forest_Chunks'],materials)
    for f,slot in zip(o.data.polygons,slots):f.material_index=slot
# Low terrain underlay and raised distant ridges. Never intersects playable ground.
box('ForestUnderlay',(0,54,5),(230,162,.5),cols['PL_Terrain_Chunks'],M['Soil'])
for side in (-1,1):
    verts=[]
    for i in range(18):
        y=-25+i*10;x=side*(118+5*math.sin(i));verts.extend([(x,y,3),(x,y,17+10*math.sin(i*.7)**2)])
    mesh('PL_DistantRidge_'+str(side),verts,[(i*2,i*2+1,i*2+3,i*2+2) for i in range(17)],cols['PL_Terrain_Chunks'],M['Rock'])
# Clear, readable presentation; no volume obscures the revised spatial layout.
scene.world.color=(.12,.16,.2)
if scene.world.use_nodes:
    bg=next(n for n in scene.world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs['Color'].default_value=(.19,.25,.32,1);bg.inputs['Strength'].default_value=.8
sun_data=bpy.data.lights.new('Action_Overcast','SUN');sun_data.energy=2.3;sun_data.angle=.15
sun=bpy.data.objects.new('Action_Overcast',sun_data);stage.objects.link(sun);sun.rotation_euler=(.45,-.6,-.7)
for k,x,y,z,color,energy in [('A',-64,40,14,(1,.66,.3),500),('B',55,61,20,(1,.55,.24),650),('C',-15,51,16,(.3,.35,1),1200),('C',15,68,16,(.8,.14,.6),1500),('C',0,76,16,(.4,.18,1),1500)]:
    data=bpy.data.lights.new('Arena_'+k,'POINT');data.energy=energy;data.color=color;data.shadow_soft_size=2
    obj=bpy.data.objects.new('Arena_'+k,data);stage.objects.link(obj);obj.location=(x,y,z)
cd=bpy.data.cameras.new('PL_Camera');cam=bpy.data.objects.new('PL_Camera',cd);stage.objects.link(cam);scene.camera=cam;cd.clip_end=1000
scene.render.engine='BLENDER_EEVEE'
scene.view_settings.exposure=.6
scene['action_layout']=True
stats={}
for k in 'ABC':
    approach=sum(math.dist(a,b) for key in ('Safe','Fork'+k,k) for a,b in zip(paths[key],paths[key][1:]))
    full=approach+pockets[k][3]+sum(math.dist(a,b) for key in ('Exit'+k,'Final') for a,b in zip(paths[key],paths[key][1:]))
    encounter=sum(math.dist(a,b) for key in ('Safe','Fork'+k) for a,b in zip(paths[key],paths[key][1:]))+5
    stats[k]=dict(centerline_m=round(full,1),approach_m=round(approach,1),approach_seconds_at_6mps=round(approach/6,1),first_encounter_candidate_m=round(encounter,1),lane_width_m=widths[k],combat_dimensions_m=pockets[k][2:])
for o in collision.objects:
    if o.type!='MESH':continue
    verts=[o.matrix_world@v.co for v in o.data.vertices]
    guides.append(dict(name=o.name,route=o.get('route','shared'),primitive=o.get('primitive','BOX'),dynamic_gate=bool(o.get('dynamic_gate')),bounds_min=[min(v[i] for v in verts) for i in range(3)],bounds_max=[max(v[i] for v in verts) for i in range(3)]))
layout=dict(revision='action_layout',paths=paths,widths=widths,pockets=pockets,ground_height_m=8,
 terrain_bounds_m=[-115,-27,115,135],features=features,stats=dict(scene=scene.name,route_stats=stats,terrain_footprint_m=[230,162],source_instances=len(instances),forest_chunks=len(batch),markers=len(manifest),collider_guides=len(guides)))
for name,data in [('layout.json',layout),('markers.json',manifest),('forest-instances.json',instances),('collision-guides.json',guides)]:
    (OUT/name).write_text(json.dumps(data,indent=2)+'\n')
collision.hide_viewport=True
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Source/Pohorje_Large.blend'))
print('ACTION LAYOUT',json.dumps(stats))
