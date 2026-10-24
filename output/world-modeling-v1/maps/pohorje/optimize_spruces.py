"""Rebuild sparse spruce chunks within the 180 triangle module target."""
import bpy, json, math, random
from collections import defaultdict
from pathlib import Path
from mathutils import Matrix, Vector
OUT=Path(__file__).resolve().parent
scene=bpy.data.scenes['Pohorje_Large']
def collection(key):
    return next(c for c in scene.collection.children if c.name.startswith('PL_'+key))
def material(key):
    return next(m for m in bpy.data.materials if m.name.startswith('PSX_PL_'+key+'.'))
def mesh(name, verts, faces, mats, indices=None, target=None):
    data = bpy.data.meshes.new(name)
    data.from_pydata(verts, [], faces)
    data.update()
    for mat in mats:
        data.materials.append(mat)
    for face, index in zip(data.polygons, indices or []):
        face.material_index = index
    uv = data.uv_layers.new(name='UVMap')
    for face in data.polygons:
        axis = max(range(3), key=lambda a: abs(face.normal[a]))
        axes = [a for a in range(3) if a != axis]
        for loop in face.loop_indices:
            v = data.vertices[data.loops[loop].vertex_index].co
            uv.data[loop].uv = (v[axes[0]]/2, v[axes[1]]/2)
    obj = bpy.data.objects.new(name, data)
    (target or collection('Route_A')).objects.link(obj)
    return obj

# Opaque branch wedges leave real gaps in the silhouette; no alpha overdraw.
library = collection('Module_Library')
templates = {}
for variant in range(3):
    verts, faces, slots = [], [], []
    rng = random.Random(131 + variant)
    for z, radius in ((0,.23),(13,.055)):
        for i in range(5):
            a = i*math.tau/5
            verts.append((math.cos(a)*radius,math.sin(a)*radius,z))
    faces.extend([(i,(i+1)%5,(i+1)%5+5,i+5) for i in range(5)])
    faces.extend([(4,3,2,1,0),(5,6,7,8,9)])
    slots.extend([0]*7)
    for tier in range(10):
        z = 2.3 + tier*1.04
        radius = (13.7-z)*.22
        for branch in range(2):
            a = branch*math.tau/2 + tier*1.79 + variant*.5
            length = radius*rng.uniform(.72,1.15)
            along = Vector((math.cos(a),math.sin(a),0))
            side = Vector((-math.sin(a),math.cos(a),0))
            start = len(verts)
            center = Vector((0,0,z))
            points = [center+side*.12, center-side*.12,
                      center+along*length+Vector((0,0,-.8)),
                      center+along*(length*.65)+side*(length*.24)+Vector((0,0,-.37)),
                      center+along*(length*.65)-side*(length*.24)+Vector((0,0,-.37)),
                      center+along*(length*.45)+Vector((0,0,.15))]
            verts.extend(tuple(p) for p in points)
            faces.extend(tuple(start+i for i in f) for f in [(0,3,2,4,1),(0,5,3),(3,5,2),(2,5,4),(4,5,1),(1,5,0)])
            slots.extend([1]*6)
    obj = next(o for o in library.objects if o.name.startswith('PL_ENV_F_Spruce_'+str(variant)))
    temporary = mesh('PSX_Spruce_'+str(variant), verts, faces, [material('Bark'),material('Foliage')], slots, library)
    obj.data = temporary.data
    bpy.data.objects.remove(temporary, do_unlink=True)
    templates['Spruce'+str(variant)] = obj
for key in ('Rock0','Rock1','Rock2','DeadTree'):
    prefix = 'PL_ENV_F_' + (key[:-1]+'_'+key[-1] if key.startswith('Rock') else key)
    templates[key] = next(o for o in library.objects if o.name.startswith(prefix))

batches = defaultdict(lambda: [[],[],[]])
all_mats = list(dict.fromkeys(m for o in templates.values() for m in o.data.materials))
for instance in json.loads((OUT/'forest-instances.json').read_text()):
    obj = templates[instance['asset']]
    verts, faces, slots = batches[tuple(instance['chunk'])]
    start = len(verts)
    transform = Matrix.Translation(Vector(instance['xyz'])) @ Matrix.Rotation(instance['angle'],4,'Z') @ Matrix.Scale(instance['scale'],4)
    verts.extend(tuple(transform @ v.co) for v in obj.data.vertices)
    faces.extend(tuple(start+i for i in f.vertices) for f in obj.data.polygons)
    slots.extend(all_mats.index(obj.data.materials[f.material_index]) for f in obj.data.polygons)
forest = collection('Forest_Chunks')
for obj in list(forest.objects):
    bpy.data.objects.remove(obj, do_unlink=True)
for (x,y),(verts,faces,slots) in batches.items():
    used = sorted(set(slots))
    mesh('PL_ForestChunk_'+str(x)+'_'+str(y), verts, faces, [all_mats[i] for i in used], [used.index(i) for i in slots], forest)

