"""Add route-specific scenic beats to the existing map without changing gameplay geometry.

Run once in Blender on the current source. New meshes share existing materials and
are batched by landmark. Lights and sound remain Unity integration work.
"""
import bpy
import hashlib
import json
import math
import random
from pathlib import Path
from mathutils import Vector

OUT = Path(__file__).resolve().parent
scene = bpy.data.scenes['Pohorje_Large']
bpy.context.window.scene = scene
if scene.get('approaches_dressed'):
    raise RuntimeError('Approach dressing already exists; preserve subsequent edits.')
layout = json.loads((OUT/'layout.json').read_text())

def ground(y):
    return 36 if y <= 90 else 36-(y-90)*28/210 if y < 300 else 8 if y <= 360 else 8-(y-360)*8/130 if y < 490 else 0

def protected():
    # Capture all pre-existing meshes, transforms and semantic properties.
    result = {}
    for obj in scene.objects:
        if obj.type not in ('MESH','EMPTY'):
            continue
        data = {'matrix': [list(r) for r in obj.matrix_world], 'props': dict(obj.items())}
        if obj.type == 'MESH':
            data['verts'] = [list(v.co) for v in obj.data.vertices]
            data['faces'] = [list(f.vertices) for f in obj.data.polygons]
        result[obj.name] = hashlib.sha256(json.dumps(data,sort_keys=True).encode()).hexdigest()
    return result

before = protected()
def material(key):
    return next(m for m in bpy.data.materials if m.name.startswith('PSX_PL_'+key+'.'))
mats = [material(k) for k in ('Bark','Rock','Metal','Concrete','OldPaper','ServiceRed','WarmEmission','RaveEmission','Soil')]
BARK, ROCK, METAL, CONCRETE, PAPER, RED, WARM, PURPLE, SOIL = range(len(mats))
cube = [(x,y,z) for z in (-.5,.5) for y in (-.5,.5) for x in (-.5,.5)]
cube_faces = [(0,2,3,1),(4,5,7,6),(0,1,5,4),(2,6,7,3),(0,4,6,2),(1,3,7,5)]
verts, faces, indices = [], [], []
created = []
beats = []
rng = random.Random(758)

def frame(route,y):
    points = layout['paths'][route]
    a,b = next((Vector(a),Vector(b)) for a,b in zip(points,points[1:]) if a[1] <= y <= b[1])
    t = (y-a.y)/(b.y-a.y)
    return a.lerp(b,t),(b-a).normalized()

def begin(route,y,title):
    global verts,faces,indices,center,tangent,normal,route_key,beat_title
    verts,faces,indices = [],[],[]
    center,tangent = frame(route,y)
    normal = Vector((-tangent.y,tangent.x))
    route_key,beat_title = route,title

def position(x,y,z):
    p = center+normal*x+tangent*y
    return Vector((p.x,p.y,ground(p.y)+z))

def append(points,polygons,slot):
    offset = len(verts)
    verts.extend(tuple(p) for p in points)
    faces.extend(tuple(offset+i for i in f) for f in polygons)
    indices.extend([slot]*len(polygons))

def box(x,y,z,sx,sy,sz,slot):
    origin = position(x,y,z)
    # Rigid props keep square corners; only their base placement follows the grade.
    append([origin + Vector((normal.x*vx*sx+tangent.x*vy*sy,
                             normal.y*vx*sx+tangent.y*vy*sy,vz*sz))
            for vx,vy,vz in cube],cube_faces,slot)

def beam(a,b,radius,slot,sides=6):
    aa,bb = position(*a),position(*b)
    axis = (bb-aa).normalized()
    u = axis.cross(Vector((0,0,1)))
    if u.length < .01:
        u = axis.cross(Vector((1,0,0)))
    u.normalize()
    v = axis.cross(u)
    points = [p+radius*(math.cos(i*math.tau/sides)*u+math.sin(i*math.tau/sides)*v) for p in (aa,bb) for i in range(sides)]
    polygons = [tuple(reversed(range(sides))),tuple(range(sides,2*sides))]
    polygons += [(i,(i+1)%sides,(i+1)%sides+sides,i+sides) for i in range(sides)]
    append(points,polygons,slot)

def lantern(side,y,slot=WARM):
    # Exterior fixtures: no new light objects or gameplay triggers.
    x = side*(layout['widths'][route_key]/2+.65)
    box(x,y,1.75,.18,.2,3.5,BARK if route_key=='A' else METAL)
    box(x,y,3.5,.5,.48,.65,METAL)
    box(x,y-.25,3.5,.32,.035,.38,slot)
    box(x,y,3.88,.68,.65,.1,METAL)

def finish():
    name = 'PL_Approach_'+route_key+'_'+beat_title
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts,[],faces)
    mesh.update()
    used = sorted(set(indices))
    for i in used:
        mesh.materials.append(mats[i])
    for poly,slot in zip(mesh.polygons,indices):
        poly.material_index = used.index(slot)
    uv = mesh.uv_layers.new(name='UVMap')
    for face in mesh.polygons:
        axis = max(range(3),key=lambda i:abs(face.normal[i]))
        axes = [i for i in range(3) if i!=axis]
        for loop in face.loop_indices:
            p = mesh.vertices[mesh.loops[loop].vertex_index].co
            uv.data[loop].uv = (p[axes[0]]/2,p[axes[1]]/2)
    obj = bpy.data.objects.new(name,mesh)
    bpy.data.collections['PL_Route_'+route_key].objects.link(obj)
    obj['purpose'] = 'scenic approach landmark; no interaction or hazard'
    obj['route'] = route_key
    created.append(obj)
    beats.append({'route':route_key,'landmark':beat_title,'center_xy':list(center),'object':name})

# Forest: alternate close silhouettes, canopy frames and recognizable trail details.
for y,title in [(135,'TrailLanterns'),(179,'RootOutcrop'),(217,'FallenCanopy'),(266,'LoggingRemains')]:
    begin('A',y,title)
    lantern(-1,-3)
    for side in (-1,1):
        # Faceted rock clusters stay beyond the full 8 m lane.
        for j in range(4):
            x = side*(5.4+rng.uniform(.1,.7))
            box(x,j*1.9-3,1.2+j*.35,1.4,1.6,2.4+j*.7,ROCK)
    if title=='FallenCanopy':
        beam((-6,-1,7.3),(6,2,6.9),.37,BARK)
        # Snapped trunks support both ends instead of leaving a floating beam.
        beam((-7.4,-3,3.8),(-6,-1,7.3),.46,BARK)
        beam((7.5,4,3.8),(6,2,6.9),.44,BARK)
        for side,yy in ((-1,-1),(1,2)):
            beam((side*6,yy,4.7),(side*7.1,yy-1.4,3.7),.18,BARK)
        beam((-4,-.5,7.233),(-2,1,8.4),.12,BARK)
        beam((3,1.25,7.0),(4,3,8.0),.11,BARK)
    elif title=='RootOutcrop':
        for side in (-1,1):
            beam((side*5.2,-3,5),(side*5.4,3,6.8),.22,BARK)
            for j in range(3):
                beam((side*5.4,j,5.8),(side*(6.3+j*.3),j-2,4.4),.13,BARK)
    elif title=='LoggingRemains':
        for j in range(3):
            beam((5.1,-4,4.7+j*.6),(5.5,4,4.7+j*.6),.25,BARK)
        for yy in (-3,3):
            box(5.3,yy,4.35,1.0,.5,.7,BARK)
        for j in range(6):
            box(-4.8,j*1.8-4.5,1.0,.17,.17,2,BARK)
        beam((-4.8,-4.5,1.6),(-4.8,4.5,1.6),.075,BARK)
    else:
        box(5.4,2,2.5,1.0,.16,.55,PAPER)
        box(5.4,1.9,2.5,.5,.03,.08,RED)
    finish()

# Gondola: overhead maintenance frames, hardware and hazard-painted edge posts.
for y,title in [(137,'MaintenanceGantry'),(166,'RepairZone'),(223,'CableSupports'),(286,'ArenaApproach')]:
    begin('B',y,title)
    for side in (-1,1):
        # Both uprights meet a level crossbar despite different ground heights.
        foot = position(side*5.7,0,0).z
        top = ground(center.y)+7.2
        height = top-foot
        box(side*5.7,0,height/2,.45,.55,height,METAL)
        box(side*5.7,0,.45,1.0,1.0,.9,CONCRETE)
        for j in range(5):
            box(side*5.6,j*2-4,1.0,.3,.3,2,METAL)
            box(side*5.6,j*2-4,1.55,.33,.34,.22,PAPER if j%2 else RED)
        beam((side*5.6,-4,1.8),(side*5.6,4,1.8),.06,METAL)
    box(0,0,7.2,12,.45,.45,METAL)
    # Braces remain above the riding envelope and join the actual upright tops.
    for side in (-1,1):
        endpoint = position(side*5.7,0,0)
        upper_z = ground(center.y)+7.2-ground(endpoint.y)
        inner = position(side*4.3,0,0)
        inner_z = ground(center.y)+7.2-ground(inner.y)
        beam((side*5.7,0,upper_z-.65),(side*4.3,0,inner_z),.11,METAL)
    box(0,-.3,7.2,3,.18,.8,RED)
    # Painted white bars on the overhead warning board.
    for i in (-1,0,1):
        box(i*.75,-.4,7.2,.35,.035,.55,PAPER)
    lantern(1,3)
    if title=='RepairZone':
        box(-6.8,1,4.9,1.6,2.4,1.8,METAL)
        box(-6.8,-.25,5.4,1.2,.06,.5,RED)
        for j in range(3):
            beam((-6.6,-4,.5+j*.4),(-6.6,-1,.5+j*.4),.16,BARK)
    if title=='CableSupports':
        beam((-5.7,0,7.9),(5.7,0,7.9),.045,METAL)
    finish()

# Tunnel: distinct poster bays and utility conduits; keep the full 8 m floor clear.
for y,title in [(171,'PosterThreshold'),(192,'UtilityRun'),(237,'GrateApproach'),(279,'PurpleArrival')]:
    begin('C',y,title)
    for side in (-1,1):
        x = side*4.24
        for j in range(3):
            beam((x,-6,4.6+j*.32),(x,6,4.6+j*.32),.065,METAL)
        for j in (-4,4):
            box(x,j,3.0,.13,.26,5.8,CONCRETE)
            box(x-side*.09,j,2.9,.055,.35,1.0,PURPLE)
        # Simple graphic poster layers face into the tunnel, without invented copy.
        box(x,0,2.4,.08,2.8,2.0,PAPER)
        box(x-side*.05,0,2.65,.035,2.1,.36,PURPLE)
        for j in range(4):
            box(x-side*.05,j*.48-.75,1.95,.035,.28,.38,RED if j%2 else METAL)
    if title in ('UtilityRun','GrateApproach'):
        for j in range(5):
            box(4.18,-2+j*.5,1.0,.12,.12,1.4,METAL)
        box(4.18,-1,1.75,.13,2.2,.12,METAL)
    finish()

after = protected()
assert all(after.get(k)==v for k,v in before.items()), 'Pre-existing geometry changed'

# Inspect every new vertex against every lane, keeping 5.8 m overhead clearance.
# All floor-level scenery must be outside the full authored lane envelope.
violations = []
for obj in created:
    for vertex in obj.data.vertices:
        v = vertex.co
        if v.z-ground(v.y) >= 5.8:
            continue
        for key,points in layout['paths'].items():
            p = Vector((v.x,v.y))
            for aa,bb in zip(points,points[1:]):
                a,b = Vector(aa),Vector(bb)
                delta = b-a
                q = a+delta*max(0,min(1,(p-a).dot(delta)/delta.length_squared))
                if (p-q).length < layout['widths'][key]/2+.01:
                    violations.append({'object':obj.name,'path':key,'vertex':list(v)})
                    break
assert not violations, json.dumps(violations[:5])
scene['approaches_dressed'] = True
report = {'original_objects_unchanged':len(before),'all_original_geometry_hashes_unchanged':True,
          'new_mesh_objects':len(created),'beats':beats,'lane_envelope_violations':violations,
          'minimum_overhead_clearance_m':5.8,
          'unity_work':'Final lights, flicker, fog, audio, interactions, controller and hardware checks pending'}
(OUT/'approach-validation.json').write_text(json.dumps(report,indent=2)+'\n')
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Source/Pohorje_Large.blend'))
print(json.dumps(report,indent=2))
