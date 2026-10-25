"""Build Maribor v3 from the preserved modular city layout.

Blender -b --factory-startup --threads 6 --python-exit-code 1 --python build_maribor.py
The v1 source and production gameplay scenes are never modified. Unity metres are
(old Blender X, old Blender Z, old Blender Y); exported meshes bake the axis change.
"""
import collections
import json
import math
import os
from pathlib import Path
import random
import runpy
import shutil
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector
from mathutils.bvhtree import BVHTree

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[3]
ART = REPO / 'Game/Assets/Art/Maribor_v3'
if (HERE/'Source/Maribor_Production.blend').exists() and '--rebuild' not in sys.argv:
    raise RuntimeError('Preserve artist edits first; --rebuild explicitly replaces the generated production source.')
os.environ['MB_FORCE'] = '1'
os.environ.pop('MB_DEV', None)
layout = runpy.run_path(str(HERE / 'layout_modules.py'))
K, kit, M = layout['K'], layout['kit'], layout['M']
scene = bpy.context.scene
scene.name = 'Maribor_Production'
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
owners = {'Shared': layout['shared'], **layout['routes'], 'Backdrop': layout['scenic']}
roofs = layout['roofs']
for owner, collection in owners.items():
    collection.name = 'Shared' if owner == 'Shared' else 'Backdrop' if owner == 'Backdrop' else 'Route_' + owner
roofs.name = 'Removable_Roofs'
collision_collection = layout['guides']; collision_collection.name = 'CollisionGuides'
marker_collection = layout['markers']; marker_collection.name = 'Markers'
presentation_collection = layout['presentation']; presentation_collection.name = 'Presentation'
for collection in (layout['library'], collision_collection, marker_collection):
    for obj in list(collection.objects): bpy.data.objects.remove(obj, do_unlink=True)
    collection.hide_render = False
    collection.hide_viewport = False

COLLIDERS, MARKERS, FLOOR_OBJECTS = [], [], []
def unity(p): return [float(p[0]), float(p[2]), float(p[1])]
def delete(obj): bpy.data.objects.remove(obj, do_unlink=True)
def base(obj): return obj.name.split('.')[0]
def owner_of(collection):
    return next((key for key, value in owners.items() if value == collection), 'C' if collection == roofs else 'Shared')

def box_shape(name, owner, pos, size, angle=0, role='obstacle'):
    q = Quaternion((0, 1, 0), -angle)
    COLLIDERS.append(dict(name='COL_' + name.replace('-', 'N'), owner=owner, kind='Box', center=unity(pos),
        size=unity(size), rotation=[q.x, q.y, q.z, q.w], radius=0, height=0, role=role, trigger=False))

def capsule_shape(name, owner, pos, radius, height):
    COLLIDERS.append(dict(name='COL_' + name, owner=owner, kind='Capsule', center=unity(pos),
        size=[0, 0, 0], rotation=[0, 0, 0, 1], radius=radius, height=height, role='obstacle', trigger=False))

def marker(name, owner, p, kind, size=(0, 0, 0), forward=(0, 0, 1)):
    MARKERS.append(dict(name='MK_' + name, owner=owner, position=unity(p), kind=kind,
        size=list(size), forward=list(forward), blender_position=[-p[0], -p[1], p[2]]))

def solid_box(name, owner, pos, size, material, angle=0, role='obstacle', collision=True):
    obj = kit.box(name, pos, size, material, owners[owner], rot=angle)
    if collision: box_shape(name, owner, pos, size, angle, role)
    return obj

# A single floor tessellation replaces the old stacked ribbons, court slabs,
# inlays and foundation tops. No coplanar riding surfaces survive this pass.
STEP = 0.5
original_floors = [obj for col in owners.values() for obj in col.objects if base(obj).startswith('Floor_')]
floor_bvhs = [(BVHTree.FromObject(obj, bpy.context.evaluated_depsgraph_get()), owner_of(obj.users_collection[0]), obj)
              for obj in original_floors]
cells = {}
for iz in range(-27, 303):
    for ix in range(-117, 157):
        x, z = (ix + .5) * STEP, (iz + .5) * STEP
        hits = []
        for tree, owner, obj in floor_bvhs:
            hit, normal, _, _ = tree.ray_cast(Vector((x, z, 1)), Vector((0, 0, -1)), 2)
            if hit is not None: hits.append((owner, obj))
        if not hits: continue
        # Shared merge/fork surfaces own their seam, with no second surface beneath them.
        owner, obj = next(((o, b) for o, b in hits if o == 'Shared'), hits[0])
        mat = 'Asphalt' if owner == 'A' and abs(x + 42) < (12 if 64 <= z <= 92 else 4.5) else 'Cobble'
        if owner == 'C': mat = 'Concrete'
        if owner == 'B':
            mat = 'Cobble' if 64 <= z <= 88 else 'Slab'
            if abs(x) < 6 and abs(z - 76) < 6: mat = 'Slab'
            elif abs(x) < 8 and abs(z - 76) < 8: mat = 'Ashlar'
        cells[ix, iz] = owner, mat
# The quay's widening corners and the service block have continuous base slabs
# behind their enclosing walls. They cannot expose unsupported inside corners.
for ix in range(-25,25):
    for iz in range(48,233):
        cells.setdefault((ix,iz),('B','Cobble' if 64<=(iz+.5)*STEP<=88 else 'Slab'))
for x0,z0,x1,z1 in [(48,20,56,97),(52,59,72,65),(58,60,78,117)]:
    for ix in range(int(x0/STEP),int(x1/STEP)):
        for iz in range(int(z0/STEP),int(z1/STEP)):cells.setdefault((ix,iz),('C','Concrete'))
for obj in original_floors: delete(obj)
for col in owners.values():
    for obj in list(col.objects):
        if base(obj) in ('PlazaInlay', 'QuayInlay', 'QuayInlayRing'): delete(obj)

def rectangles(values):
    remaining = dict(values)
    result = []
    while remaining:
        x, z = min(remaining, key=lambda p: (p[1], p[0])); value = remaining[x, z]
        w = 1
        while remaining.get((x + w, z)) == value: w += 1
        d = 1
        while all(remaining.get((xx, z + d)) == value for xx in range(x, x + w)): d += 1
        for zz in range(z, z + d):
            for xx in range(x, x + w): del remaining[xx, zz]
        result.append((x, z, w, d, value))
    return result

# A one metre support apron lies behind boundary walls and under facade bases.
# The controller can still be supported when its centre reaches a wall corner.
for _ in range(2):
    expanded=dict(cells)
    for (ix,iz),value in cells.items():
        for dx,dz in ((-1,0),(1,0),(0,-1),(0,1)):
            expanded.setdefault((ix+dx,iz+dz),value)
    cells=expanded
floor_rectangles = rectangles(cells)
for index, (ix, iz, w, d, (owner, mat)) in enumerate(floor_rectangles):
    name = f'Ground_{index:03d}'
    obj = solid_box(name, owner, ((ix + w/2)*STEP, (iz + d/2)*STEP, -.20),
        (w*STEP, d*STEP, .4), M[mat], role='walkable')
    FLOOR_OBJECTS.append(obj)

# The concrete roof is one union of covered footprints. Overlapping pocket /
# escape-gallery slabs previously produced coincident top and bottom faces.
roof_cells = {}
for obj in list(roofs.objects) + [o for o in owners['C'].objects if base(o).startswith('Cap_')]:
    if base(obj) not in ('RoofBay', 'EscapeRoof', 'PocketRoof', 'SideConnectorRoof') and not base(obj).startswith('Cap_'): continue
    points = [obj.matrix_world @ Vector(v) for v in obj.bound_box]
    lo, hi = [min(v[i] for v in points) for i in range(3)], [max(v[i] for v in points) for i in range(3)]
    for iz in range(math.floor(lo[1]/STEP), math.ceil(hi[1]/STEP)):
        for ix in range(math.floor(lo[0]/STEP), math.ceil(hi[0]/STEP)): roof_cells[ix, iz] = 'Roof'
    delete(obj)
for index, (ix, iz, w, d, _) in enumerate(rectangles(roof_cells)):
    pos = ((ix+w/2)*STEP, (iz+d/2)*STEP, 4.6)
    kit.box(f'TunnelRoof_{index:02d}', pos, (w*STEP, d*STEP, .4), M['Concrete'], roofs)
    box_shape(f'TunnelRoof_{index:02d}', 'C', pos, (w*STEP, d*STEP, .4), role='ceiling')

# Keep the dense facade kit near the player; distant bank buildings need solid
# silhouettes, roof volumes and a handful of windows rather than full door trim.
for obj in list(owners['Backdrop'].objects):
    if base(obj) != 'BankHouse': continue
    matrix = obj.matrix_world.copy(); x, z, h = obj.location; scale = obj.scale.x
    name = obj.name.replace('.', '_'); angle = obj.rotation_euler.z
    delete(obj)
    parts = [kit.box(name+'_Body', (0, 4, 3.3), (4, 8, 6.6), M['Stucco'][len(name)%4], owners['Backdrop']),
             kit.gable(name+'_Roof', (0, 4, 6.6), (4, 8, 0), (.15, .25), 2.6, M['Terracotta'], owners['Backdrop'])]
    for side in (-1, 1):
        parts.append(kit.box(name+'_Window', (side*1.1, -.045, 4.8), (.7, .10, 1.2),
            M['WindowLit'] if (int(abs(x))//6)%5 == 0 else M['Glass'], owners['Backdrop']))
    merged = kit.join(parts, name)
    merged.matrix_world = matrix

# Reflections belong to the water texture, not floating emissive rectangles.
# Trees sample the final coarse hillside mesh instead of the analytic scaffold.
hill_tree=BVHTree.FromObject(layout['hills'],bpy.context.evaluated_depsgraph_get())
for obj in list(owners['Backdrop'].objects):
    if base(obj)=='Reflection':delete(obj)
    elif base(obj)=='HillSpruce':
        p=obj.location
        hit,_,_,_=hill_tree.ray_cast(Vector((p.x,p.y,200)),Vector((0,0,-1)),400)
        if hit is not None:obj.location.z=hit.z-.12

# Replace the unsafe one-ended car wedge with supported ascent, deck and descent.
for obj in list(owners['A'].objects):
    if base(obj) == 'CarRamp': delete(obj)
def inclined(name, a, b, width):
    a, b = Vector(a), Vector(b); direction = (b-a).normalized()
    right = Vector((1, 0, 0)); normal = right.cross(direction).normalized()
    if normal.z < 0: normal = -normal
    mid = (a+b)/2 - normal*.12
    bpy.ops.mesh.primitive_cube_add(size=1)
    obj = bpy.context.object; obj.name=name
    obj.dimensions = (width, (b-a).length, .24)
    obj.rotation_euler.x = math.atan2(direction.z, direction.y)
    obj.location=mid
    for col in list(obj.users_collection): col.objects.unlink(obj)
    owners['A'].objects.link(obj); obj.data.materials.append(M['Concrete'])
    q = Quaternion((1, 0, 0), obj.rotation_euler.x)
    uq = Quaternion((q.w, -q.x, q.z, -q.y))
    COLLIDERS.append(dict(name='COL_Ramp_'+name,owner='A',kind='Box',center=unity(mid),size=[width,.24,(b-a).length],
        rotation=[uq.x,uq.y,uq.z,uq.w],radius=0,height=0,role='walkable',trigger=False))
    return obj
inclined('CarRamp_Ascent', (-33.6,68,.04), (-33.6,73.5,1.4), 3.5)
inclined('CarRamp_Descent', (-33.6,78.5,1.4), (-33.6,84,.04), 3.5)
solid_box('Ramp_Deck','A',(-33.6,76,1.28),(3.5,5,.24),M['Concrete'],role='walkable')
for z in (73.5,78.5):
    for x in (-35.1,-32.1): solid_box(f'Ramp_Support_{x}_{z}'.replace('.','_'),'A',(x,z,.60),(.2,.28,1.2),M['Iron'])

# Convert only authored obstacles, never the decorative mesh bounds. Floors,
# gates, thin lamp posts and boundary strips have their own explicit treatment.
skip = ('COL_Floor_', 'COL_EntryGate_', 'COL_RearSeal_', 'COL_Boundary', 'COL_QuayEdge', 'COL_Fountain', 'COL_Lamp', 'COL_TreeTrunk', 'COL_GatePost')
for guide in kit.collision:
    if guide['name'].startswith(skip): continue
    owner = next((key for key, col in owners.items() if guide['owner'] in (col.name,'MB_'+('Route_'+key if key in 'ABC' else 'Shared_Arrival_FERI' if key=='Shared' else 'Scenery'))), 'C')
    pos, size = guide['center'], guide['size']
    # The old facade proposals included a full phantom storey above their eaves.
    if guide['name'].startswith(('COL_FacadeBay','COL_ParkHouse')): pos=[pos[0],pos[1],3.3];size=[size[0],size[1],6.6]
    if guide['name'].startswith('COL_Planter') and owner=='B': continue
    box_shape(guide['name'][4:].replace('.','_'),owner,pos,size,guide.get('rotation_z',0))
for owner,col in owners.items():
    if owner=='Backdrop':continue
    for obj in list(col.objects):
        if base(obj) in ('Streetlamp','Streetlamp_Dead'):
            x,z,h=obj.location
            box_shape(obj.name.replace('.','_')+'_Foot',owner,(x,z,h+.35),(.55,.55,.7))
            capsule_shape(obj.name.replace('.','_')+'_Post',owner,(x,z,h+2),.10,3.8)
        elif base(obj)=='Tree':
            x,z,h=obj.location;scale=obj.scale.x
            capsule_shape(obj.name.replace('.','_'),owner,(x,z,h+1.6*scale),.25*scale,3.6*scale)

# Match the curved planter footprint with short oriented boxes, with no gaps.
pathB=layout['pathB']; pathA=layout['pathA']
for i0,i1 in layout['planter_ranges']:
    for i in range(i0,i1):
        a,b=pathB.pts[i],pathB.pts[i+1];mid=(a+b)/2;delta=b-a
        box_shape(f'LentPlanter_{i:03d}','B',(mid.x,mid.y,.35),(5.0,delta.length+.04,.7),layout['yaw_along'](delta))

# New continuous rail/boundary footprints include the final segment. The tall
# containment is behind visible rails/hedges, and separate from the riding lane.
def boundary(name, owner, points, height=5.5, thickness=.35):
    for index,(a,b) in enumerate(zip(points,points[1:])):
        a,b=Vector(a),Vector(b);delta=b-a;mid=(a+b)/2
        box_shape(f'Boundary_{name}_{index:03d}',owner,(mid.x,mid.y,height/2),
            (delta.length+.10,thickness,height),math.atan2(delta.y,delta.x),role='boundary')
def offset_path(path,side,width):
    ids=list(range(0,len(path.pts),3))
    if ids[-1]!=len(path.pts)-1:ids.append(len(path.pts)-1)
    return [path.pts[i]+path.normal(i)*side*width for i in ids]
for side in (-1,1):
    apts=[p for p in offset_path(pathA,side,7.0) if p.y<=114.5]
    # The street court expands between z64 and z92, so its boundary moves out.
    apts=[Vector((-42+(-1 if side>0 else 1)*12.7,p.y,0)) if 64<=p.y<=92 else p for p in apts]
    boundary('Tabor_'+str(side),'A',apts)
    bpts=[p for p in offset_path(pathB,side,6.5) if p.y<=114.5]
    bpts=[Vector(((-1 if side>0 else 1)*11,p.y,0)) if 64<=p.y<=88 else p for p in bpts]
    boundary('Lent_'+str(side),'B',bpts)
# Keep original authored shared rail footprints, including the causeway and pier.
for guide in kit.collision:
    if not guide['name'].startswith('COL_Boundary') or guide['owner'] not in ('MB_Shared_Arrival_FERI','Shared'):continue
    pos=list(guide['center']);size=list(guide['size']);pos[2]=2.75;size[2]=5.5
    # Move the fork/merge end barriers clear of the widened A and B openings.
    if abs(pos[1]-24.4)<.1 or abs(pos[1]-115.4)<.1:continue
    box_shape('Boundary_Shared_'+guide['name'].replace('.','_'),'Shared',pos,size,guide.get('rotation_z',0),role='boundary')
for z in (24.4,115.4):
    segments=[(-56,-49.2),(-34.8,-7),(7,48.4),(55.6,65)] if z>100 else [(-56,-49.2),(-34.8,-7),(7,48.4),(55.6,62)]
    for index,(a,b) in enumerate(segments):
        if b<=a:continue
        solid_box(f'ForkBarrier_{int(z)}_{index}','Shared',((a+b)/2,z,1.0),(b-a,.65,2),M['Ashlar'],role='boundary')
        box_shape(f'Boundary_Fork_{int(z)}_{index}','Shared',((a+b)/2,z,2.75),(b-a,.65,5.5),role='boundary')
# Fountain collision follows its ring; standing on the basin remains supported.
for i in range(12):
    angle=i*math.tau/12
    box_shape(f'FountainRing_{i:02d}','Shared',(math.cos(angle)*3.12,-7+math.sin(angle)*3.12,.45),
        (.58,1.75,.9),angle)
capsule_shape('FountainColumn','Shared',(0,-7,1.8),.4,2.5)
box_shape('Ground_Fountain','Shared',(0,-7,.74),(5.0,5.0,.30),role='walkable')

# Remove old narrow gates, lettering and gate posts before placing new seals.
for col in owners.values():
    for obj in list(col.objects):
        if base(obj).startswith(('EntryGate_','RearSeal_')) or base(obj) in ('GatePost','SignBoard','RouteSign','C_Banner','GateWall'):delete(obj)
def gate(owner,which,x,z,width):
    col=owners['Shared'];name=owner+'_'+which
    rear=which=='Rear'
    for side in (-1,1):
        xx=x+side*(width/2+.45)
        solid_box('GatePillar_'+name+str(side),'Shared',(xx,z,2.7),(.8,1.0,5.4),M['Ashlar'])
        # Wing walls connect the aperture to the outer support slab; no route
        # can be entered around either end of a closed front or rear seal.
        wing=x+side*(width/2+3.8)
        solid_box('GateWing_'+name+str(side),'Shared',(wing,z,2.7),(6.8,.65,5.4),M['Concrete'],role='boundary')
    kit.box('GateLintel_'+name,(x,z,5.2),(width+1.6,.7,.6),M['Iron'],col)
    label={'A':'A / TABOR','B':'B / LENT','C':'C / PODZEMNI PREHOD'}[owner] if not rear else 'FERI  >'
    kit.box('GateSign_'+name,(x,z-.57,4.6),(min(width,10),.16,1.0),M['Iron'],col)
    layout['text']('GateLabel_'+name,label,(x,z-.68,4.4),.46 if width>8 else .29,M['Sign'],col)
    seal=kit.box('PL_Seal_'+name,(x,z,2.5),(width+.12,.35,5),M['ContainerTeal'] if owner=='B' else M['Iron'],col)
    seal['seal_name']=name
    for side in (-1,1):
        brace=kit.box('GateBrace_'+name,(x+side*width*.3,z-.20,2.4),(.12,.12,4.6),M['Hazard'],col)
        brace['seal_name']=name
    box_shape('Seal_'+name,owner,(x,z,2.5),(width+.12,.42,5),role='seal_rear' if rear else 'seal_front')
    marker('Gate_'+name,owner,(x,z,0),'gate_rear' if rear else 'gate_front',(width,5,.42))
    if not rear:marker('Trigger_'+owner,owner,(x,z+1.8,1.6),'route_selection',(width,3.2,2.0))
for owner,x,width in [('A',-42,14.4),('B',0,14),('C',52,6.4)]:gate(owner,'Front',x,25,width)
for owner,x,width in [('A',-42,14.4),('B',0,14),('C',68,6.4)]:gate(owner,'Rear',x,113 if owner=='C' else 113.5,width)

# Clear, grounded marker positions replace the old proposed spawn points, some
# of which overlapped parked cars, planters or the underpass pocket wall.
marker('Start_Player','Shared',(0,0,.03),'player_start')
marker('Exit_City','Shared',(0,146,1.5),'level_exit',(5,3,3))
for name,p,kind in [('C_Guilt_Reveal',(52,91,0),'event_guilt_reveal'),('C_Retreat',(52,67,0),'event_retreat'),
                    ('C_SideDoor',(55.3,62,0),'event_side_escape'),('A_Guilt_Roof',(-56,80,8),'event_guilt_reveal')]:
    marker(name,'A' if name.startswith('A') else 'C',p,kind)
SPOTS={
 'A':dict(c=(-42,78),half=(12,14),charges=[(-48,76),(-40,87),(-42,69)],spawns=[(-51,73),(-45,66),(-37,72),(-38,82),(-47,89),(-32,92)]),
 'B':dict(c=(0,76),half=(11,12),charges=[(-6,70),(6,73),(0,83),(-7,80),(7,84)],spawns=[(-8,68),(8,68),(-8,77),(8,80),(-6,85),(5,86)]),
 'C':dict(c=(68,90),half=(9,10),charges=[(63,85),(73,86),(68,96)],spawns=[(62,82),(74,82),(62,88),(74,93),(63,98),(73,98)])}
for owner,data in SPOTS.items():
    x,z=data['c'];hx,hz=data['half']
    marker('Occupancy_'+owner,owner,(x,z,2.0),'combat_occupancy',(hx*2,4,hz*2))
    for index,(x,z) in enumerate(data['charges'],1):marker(f'Charge_{owner}_{index}',owner,(x,z,0),'battery_charge')
    for index,(x,z) in enumerate(data['spawns'],1):marker(f'Spawn_{owner}_{index}',owner,(x,z,0),'enemy_spawn')
    for index,dz in enumerate((-.3,0,.3),1):marker(f'Guilt_{owner}_{index}',owner,(data['c'][0],data['c'][1]+dz*hz*2,0),'guilt_anchor')
# Remove old charge illustrations so new markers are the one placement authority.
for owner in 'ABC':
    for obj in list(owners[owner].objects):
        if base(obj) in ('ChargeBase','ChargeCell'):delete(obj)
    for index,(x,z) in enumerate(SPOTS[owner]['charges'],1):
        kit.box(f'ChargeFoot_{owner}_{index}',(x,z,.08),(.6,.6,.16),M['Iron'],owners[owner])
        kit.box(f'ChargeCell_{owner}_{index}',(x,z,.65),(.25,.25,.9),M['Charge'],owners[owner])
for name,p,kind,radius in [('C_Entry',(52,30,3.6),'light_violet',10),('C_Main',(52,52,3.9),'light_warm',13),
                         ('C_Reveal',(52,92,3.2),'light_violet',10),('C_Escape',(62,62,3.7),'light_warm',12),
                         ('C_Pocket',(68,90,3.8),'light_warm',16)]:marker('Light_'+name,'C',p,kind,(radius,0,0))
for index,p in enumerate(layout['lamp_spots'][::8]):marker('Light_Lamp_'+str(index),'Shared',p,'light_warm',(10,0,0))

for col in owners.values():
    for obj in list(col.objects):
        if base(obj) not in ('LeafPile','Stain'):continue
        points=[obj.matrix_world@Vector(v) for v in obj.bound_box]
        cx=sum(v.x for v in points)/8;cz=sum(v.y for v in points)/8
        rx=(max(v.x for v in points)-min(v.x for v in points))/2
        rz=(max(v.y for v in points)-min(v.y for v in points))/2
        rng=random.Random(obj.name+str(cx));vertices=[]
        for index in range(8):
            angle=index*math.tau/8;r=rng.uniform(.70,1)
            vertices.append((cx+math.cos(angle)*rx*r,cz+math.sin(angle)*rz*r,.013))
        mat=obj.data.materials[0];name=obj.name;delete(obj)
        kit.mesh(name,vertices,[tuple(range(8))],mat,col,tile=2)

# Consolidate small surface families into padded, opaque atlases. Large surfaces
# retain their tiling UVs; small props/facades use one local tile per face.
TILING=['Asphalt','Cobble','Slab','Ashlar','Concrete','Rock','Grass','Terracotta','Water']
tiling_mats={M[name]:'Maribor_'+name for name in TILING}
render=[o for col in list(owners.values())+[roofs] for o in col.objects if o.type=='MESH']
used=sorted({m for o in render for m in o.data.materials},key=lambda m:m.name)
other=[m for m in used if m not in tiling_mats]
emissive=[m for m in other if m.node_tree.nodes.get('Principled BSDF') and m.node_tree.nodes['Principled BSDF'].inputs['Emission Strength'].default_value>0]
opaque=[m for m in other if m not in emissive]
atlas_lookup={}; materials=[]
def texture_image(mat):
    return next((n.image for n in mat.node_tree.nodes if n.type=='TEX_IMAGE' and n.image),None)
def atlas(name,old_mats):
    count=8 if len(old_mats)>16 else 4;tile=64;size=count*tile
    pixels=np.zeros((size,size,4),dtype=np.float32);pixels[:,:,3]=1
    for index,mat in enumerate(old_mats):
        image=texture_image(mat);bsdf=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
        if image:
            data=np.array(image.pixels[:]).reshape(image.size[1],image.size[0],4)
            ids=np.linspace(0,image.size[0]-1,tile-4).astype(int);rows=np.linspace(0,image.size[1]-1,tile-4).astype(int)
            sample=data[rows[:,None],ids[None,:]]
        else:
            color=np.array(bsdf.inputs['Base Color'].default_value)
            sample=np.broadcast_to(color,(tile-4,tile-4,4))
        tx,ty=(index%count)*tile,(index//count)*tile
        pixels[ty:ty+tile,tx:tx+tile]=np.pad(sample,((2,2),(2,2),(0,0)),mode='edge')
        atlas_lookup[mat]=(name,(tx+2.5)/size,(ty+2.5)/size,(tile-5)/size)
    image=bpy.data.images.new('T_'+name,width=size,height=size,alpha=False)
    image.pixels.foreach_set(pixels.ravel());image.file_format='PNG';image.filepath_raw=str(HERE/'Textures'/f'{name}.png');image.save()
    material=K.material(name,image,emit=.8 if name.endswith('Emission') else 0,emit_from_image=name.endswith('Emission'))
    material.name=name
    materials.append(dict(name=name,texture='Textures/'+name+'.png',emission=[.8,.8,.8] if name.endswith('Emission') else [],clamp=True))
    return material
atlases={'Maribor_Details':atlas('Maribor_Details',opaque),'Maribor_Emission':atlas('Maribor_Emission',emissive)}
for mat,name in tiling_mats.items():
    image=texture_image(mat);filename=name+'.png'
    image.filepath_raw=str(HERE/'Textures'/filename);image.file_format='PNG';image.save()
    mat.name=name;image.name='T_'+name
    materials.append(dict(name=name,texture='Textures/'+filename,emission=[.45,.45,.45] if name=='Maribor_Water' else [],clamp=False))
for obj in render:
    if obj.data.users>1:obj.data=obj.data.copy()
    mesh=obj.data
    if not mesh.uv_layers:mesh.uv_layers.new(name='UVMap')
    uv=mesh.uv_layers.active.data;old=list(mesh.materials)
    new=[];indices=[]
    for poly in mesh.polygons:
        mat=old[poly.material_index]
        if mat in atlas_lookup:
            name,u,v,span=atlas_lookup[mat];target=atlases[name]
            coords=[uv[i].uv.copy() for i in poly.loop_indices]
            lo=[min(q[j] for q in coords) for j in range(2)];hi=[max(q[j] for q in coords) for j in range(2)]
            # Source primitives retain quads here, so each small prop face maps
            # to one complete atlas tile before triangulation.
            for li,q in zip(poly.loop_indices,coords):
                uv[li].uv=(u+((q.x-lo[0])/max(hi[0]-lo[0],1e-5))*span,v+((q.y-lo[1])/max(hi[1]-lo[1],1e-5))*span)
        else:target=mat
        if target not in new:new.append(target)
        indices.append(new.index(target))
    mesh.materials.clear()
    for mat in new:mesh.materials.append(mat)
    for poly,index in zip(mesh.polygons,indices):poly.material_index=index

# Correct winding in the source, including disconnected library pieces. Text
# and open scenery surfaces retain their authored front. Export needs no repair.
normal_repairs=0
for obj in render:
    bpy.context.view_layer.update()
    matrix=obj.matrix_world.copy();mesh=obj.data
    mesh.transform(matrix);obj.matrix_world=Matrix.Identity(4)
    bm=bmesh.new();bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    todo=set(bm.faces)
    while todo:
        stack=[todo.pop()];component=[]
        while stack:
            face=stack.pop();component.append(face)
            for edge in face.edges:
                for adjacent in edge.link_faces:
                    if adjacent in todo:todo.remove(adjacent);stack.append(adjacent)
        if all(len(e.link_faces)==2 for f in component for e in f.edges):
            origin=component[0].verts[0].co.copy()
            volume=sum((f.verts[0].co-origin).dot((f.verts[i].co-origin).cross(f.verts[i+1].co-origin))/6 for f in component for i in range(1,len(f.verts)-1))
            if volume<0:bmesh.ops.reverse_faces(bm,faces=component);normal_repairs+=1
    bmesh.ops.triangulate(bm,faces=list(bm.faces))
    bad=[f for f in bm.faces if f.calc_area()<1e-8]
    if bad:bmesh.ops.delete(bm,geom=bad,context='FACES')
    loose=[v for v in bm.verts if not v.link_faces]
    if loose:bmesh.ops.delete(bm,geom=loose,context='VERTS')
    bm.to_mesh(mesh);bm.free();mesh.update()
    for poly in mesh.polygons:poly.use_smooth=False

# Spatially batch small pieces, but retain roofs, floor surfaces and seals.
groups=collections.defaultdict(list)
for obj in render:
    owner=owner_of(obj.users_collection[0])
    if obj.get('seal_name'):
        key=('seal',obj['seal_name'])
    elif obj in FLOOR_OBJECTS:key=('floor',owner,int(obj.bound_box[0][1]//20))
    elif obj.users_collection[0]==roofs:key=('roof',int(obj.bound_box[0][1]//20))
    else:
        center=sum((Vector(v) for v in obj.bound_box),Vector())/8
        key=('render',owner,int(center.x//20),int(center.y//20))
    groups[key].append(obj)
for index,(key,objects) in enumerate(groups.items()):
    owner=owner_of(objects[0].users_collection[0])
    name='PL_Seal_'+key[1] if key[0]=='seal' else f'PL_{owner}_'+('Roof_' if key[0]=='roof' else 'Ground_' if key[0]=='floor' else 'City_')+f'{index:03d}'
    obj=kit.join(objects,name) if len(objects)>1 else objects[0]
    obj.name=name;obj.data.name=name
    # The FBX conversion produces identity roots in Unity.
    obj.data.transform(Matrix.Diagonal((-1,-1,1,1)))
    obj.matrix_world=Matrix.Identity(4)

for index,shape in enumerate(COLLIDERS):
    pos=shape['center'];size=shape['size']
    q=shape['rotation'];uq=Quaternion((q[3],q[0],q[1],q[2]))
    # Guide geometry is an inspectable duplicate of the numerical contract.
    bpy.ops.mesh.primitive_cube_add(size=1)
    obj=bpy.context.object;obj.name=shape['name']
    if shape['kind']=='Capsule':
        bpy.data.objects.remove(obj,do_unlink=True)
        obj=kit.cylinder(shape['name'],(0,0,0),shape['radius'],shape['height'],M['Iron'],collision_collection,verts=8)
    else:
        for col in list(obj.users_collection):col.objects.unlink(obj)
        collision_collection.objects.link(obj)
        obj.scale=(size[0],size[2],size[1])
    obj.location=(-pos[0],-pos[2],pos[1])
    obj.rotation_euler=Quaternion((uq.w,uq.x,uq.z,-uq.y)).to_euler()
    bpy.context.view_layer.objects.active=obj;obj.select_set(True)
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);obj.select_set(False)
    obj.data.materials.clear()
    obj.display_type='WIRE';obj.hide_render=True
for item in MARKERS:
    obj=bpy.data.objects.new(item['name'],None);marker_collection.objects.link(obj)
    obj.location=item['blender_position'];obj.empty_display_type='ARROWS';obj.empty_display_size=.6
    obj['kind']=item['kind'];obj['owner']=item['owner']

def lane(name,owner,points,width,offsets=None):
    return dict(name=name,owner=owner,width=width,offsets=offsets or [-1.1,0,1.1],
        points=[dict(x=float(p[0]),y=0,z=float(p[1])) for p in points])
paths=layout['paths']
lanes=[lane(name,'Shared',paths[name],12) for name in ('Arrival','ForkA','ForkB','ForkC','MergeA','MergeB','MergeC','FERI')]
lanes += [lane('A','A',[[p.x,p.y] for p in pathA.pts[::3] if 28<=p.y<=113],9)]
for side in (-1,1):
    points=[]
    for i in range(4,len(pathB.pts)-1,3):
        p=pathB.pts[i];offset=side*4.8 if 33<=p.y<=63 or 89<=p.y<=111 else 0
        q=p+pathB.normal(i)*offset;points.append([q.x,q.y])
    lanes.append(lane('B' if side==1 else 'B_Alternate','B',[[0,28]]+points+[[0,115]],3.7,[-.65,0,.65]))
lanes += [lane('C','C',paths['C_Reveal'],6),lane('C_Escape','C',paths['C_Escape'],4,[-.65,0,.65])]
lanes += [lane('Exit'+r,r,([[-42,94],[-42,112]] if r=='A' else [[0,89],[0,112]] if r=='B' else [[68,101],[68,111]]),6) for r in 'AC']
lanes += [dict(name='Optional_car_ramp',owner='A',width=3.5,offsets=[-.7,0,.7],points=[dict(x=-33.6,y=y,z=z) for z,y in [(67,0),(73.5,1.4),(78.5,1.4),(85,0)]])]
shots=[('01-arrival',(3,7,18),(0,9,-29)),('02-fork',(-9,4,16),(-36,3,28)),
 ('03-tabor',(-42,2.8,31),(-42,2,61)),('04-tabor-court',(-48,5.2,65),(-36,1.5,84)),
 ('05-lent',(-4.6,3.4,32),(0,2.5,63)),('06-lent-quay',(-7,4.4,66),(8,1.8,84)),
 ('07-underpass',(52,2.3,31),(52,2,90)),('08-side-escape',(52,2,65),(64,2,61)),
 ('09-service-pocket',(68,2.7,82),(72,1.3,97)),('10-feri',(0,3.6,126),(0,3,151)),
 ('11-overview',(-140,160,-118),(6,0,65)),('12-drava',(6,7,119),(57,4,154))]
render=[o for col in list(owners.values())+[roofs] for o in col.objects if o.type=='MESH']
report=dict(scene=scene.name,render_triangles=sum(len(o.data.polygons) for o in render),render_objects=len(render),
    materials=sorted({m.name for o in render for m in o.data.materials}),floor_rectangles=len(floor_rectangles),
    collision_shapes=dict(collections.Counter(c['kind'] for c in COLLIDERS)),normal_repairs=normal_repairs,markers=len(MARKERS),errors=[])
if len(report['materials'])>15:raise RuntimeError('Material budget exceeded')
if report['render_triangles']>100000:raise RuntimeError('Triangle budget exceeded: '+str(report['render_triangles']))
def walkable_height(x,z):
    top=0.0
    for shape in COLLIDERS:
        if shape['role']!='walkable':continue
        q=shape['rotation'];q=Quaternion((q[3],q[0],q[1],q[2]))
        c=Vector(shape['center']);size=shape['size'];normal=q@Vector((0,1,0))
        if normal.y<.7:continue
        y=c.y+size[1]/(2*normal.y)-(normal.x*(x-c.x)+normal.z*(z-c.z))/normal.y
        local=q.inverted()@(Vector((x,y,z))-c)
        if abs(local.x)<=size[0]/2+1e-5 and abs(local.z)<=size[2]/2+1e-5:top=max(top,y)
    return top
floor_samples=[]
for (ix,iz),(owner,mat) in cells.items():
    if ix%4 or iz%4:continue
    x,z=(ix+.5)*STEP,(iz+.5)*STEP
    floor_samples.append(dict(x=x,y=walkable_height(x,z),z=z))
exports=[dict(file='Maribor_'+name+'.fbx',owner=owner) for name,owner in
    [('Shared','Shared'),('Route_A','A'),('Route_B','B'),('Route_C','C'),('Backdrop','Backdrop'),('Roofs','Roofs'),('CollisionGuides','CollisionGuides'),('Markers','Markers')]]
manifest=dict(version=3,name='Maribor_Production',coordinates='Unity metres: X right, Y up, Z forward',materials=materials,exports=exports,
    terrain=[],colliders=COLLIDERS,markers=MARKERS,lanes=lanes,floorSamples=floor_samples,
    pads=[dict(name='Arena'+r,cx=d['c'][0],cz=d['c'][1],hx=d['half'][0],hz=d['half'][1],height=0,radius=0) for r,d in SPOTS.items()]+
         [dict(name='Arrival',cx=0,cz=2,hx=17,hz=15,height=0,radius=0)],
    reviewCameras=[dict(name=n,eye=e,target=t) for n,e,t in shots],expected=report)
ART.mkdir(parents=True,exist_ok=True);(ART/'Textures').mkdir(exist_ok=True)
(HERE/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
(HERE/'validation.json').write_text(json.dumps(report,indent=2)+'\n')
(ART/'Maribor_Manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
(HERE/'markers.json').write_text(json.dumps(dict(coordinates='Blender XYZ metres',markers=MARKERS),indent=2)+'\n')
keep={Path(m['texture']).name for m in materials}
for png in (HERE/'Textures').glob('*.png'):
    if png.name not in keep:png.unlink()
    else:shutil.copy2(png,ART/'Textures'/png.name)
export_collections={**owners,'Roofs':roofs,'CollisionGuides':collision_collection,'Markers':marker_collection}
for export in exports:
    bpy.ops.object.select_all(action='DESELECT')
    for obj in export_collections[export['owner']].objects:obj.hide_set(False);obj.select_set(True)
    path=HERE/'Export'/export['file']
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'EMPTY'} if export['owner']=='Markers' else {'MESH'},
        axis_forward='-Z',axis_up='Y',bake_space_transform=True,apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',
        mesh_smooth_type='FACE',use_mesh_modifiers=True,use_triangles=True,bake_anim=False,add_leaf_bones=False,path_mode='STRIP')
    shutil.copy2(path,ART/path.name)
# Only the material images actually used by the production model remain.
for mat in list(bpy.data.materials):
    if mat.name not in report['materials']:bpy.data.materials.remove(mat)
for image in list(bpy.data.images):
    if not image.name.startswith('T_Maribor_'):bpy.data.images.remove(image)
    else:
        if image.packed_file:image.unpack(method='REMOVE')
        image.filepath='//../Textures/'+Path(image.filepath).name
for obj in collision_collection.objects:obj.hide_set(True)
world=bpy.data.worlds.new('Maribor_Dusk');scene.world=world;world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.22,.12,.12,1)
world.node_tree.nodes['Background'].inputs[1].default_value=.65
sun_data=bpy.data.lights.new('Preview_Sun','SUN');sun_data.energy=2.1;sun_data.color=(1,.59,.40);sun_data.angle=.10
sun=bpy.data.objects.new('Preview_Sun',sun_data);presentation_collection.objects.link(sun)
sun.rotation_euler=(math.radians(28),math.radians(-25),math.radians(-40))
for item in MARKERS:
    if not item['kind'].startswith('light_'):continue
    light_data=bpy.data.lights.new('Preview_'+item['name'],'POINT');light_data.energy=200;light_data.shadow_soft_size=1
    light_data.color=(.56,.25,1) if item['kind']=='light_violet' else (1,.62,.38)
    light=bpy.data.objects.new(light_data.name,light_data);presentation_collection.objects.link(light);light.location=item['blender_position']
camera_data=bpy.data.cameras.new('Maribor_ReviewCamera');camera_data.lens=26;camera_data.clip_end=1000
camera=bpy.data.objects.new('Maribor_ReviewCamera',camera_data);presentation_collection.objects.link(camera);scene.camera=camera
scene.render.engine='CYCLES';scene.cycles.samples=20;scene.cycles.use_denoising=True
scene.render.resolution_x=1280;scene.render.resolution_y=720;scene.render.resolution_percentage=100
scene.view_settings.view_transform='Standard';scene.view_settings.look='None';scene.view_settings.exposure=0
scene.render.image_settings.file_format='PNG'
eye,target=Vector(shots[10][1]),Vector(shots[10][2])
camera.location=(-eye.x,-eye.z,eye.y);look=Vector((-target.x,-target.z,target.y))
camera.rotation_euler=(look-camera.location).to_track_quat('-Z','Y').to_euler()
for area in bpy.context.screen.areas if bpy.context.screen else []:
    if area.type=='VIEW_3D':area.spaces.active.region_3d.view_perspective='CAMERA';area.spaces.active.shading.type='MATERIAL'
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'Source/Maribor_Production.blend'),compress=True)
print('MARIBOR_PRODUCTION_DONE',json.dumps(report))
