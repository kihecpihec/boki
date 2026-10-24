"""Apply the October 23 forest reference to the existing build, inside Blender.

Run once after opening Source/Pohorje_Large.blend. Does not modify Unity assets.
"""
import bpy
import json
import math
import random
from collections import defaultdict
from pathlib import Path
from mathutils import Matrix, Vector

OUT = Path(__file__).resolve().parent
scene = bpy.data.scenes['Pohorje_Large']
bpy.context.window.scene = scene
if scene.get('reference_style'):
    raise RuntimeError('Reference style already applied; refusing to replace subsequent edits.')

def collection(key):
    return next(c for c in scene.collection.children if c.name.startswith('PL_' + key))

def ground(y):
    return 36 if y <= 90 else 36-(y-90)*28/210 if y < 300 else 8 if y <= 360 else 8-(y-360)*8/130 if y < 490 else 0

# Make scene-local material and mesh copies so earlier comparison scenes survive.
materials = {}
for obj in scene.objects:
    if obj.type != 'MESH':
        continue
    obj.data = obj.data.copy()
    for slot in obj.material_slots:
        if slot.material:
            original = slot.material
            if original not in materials:
                materials[original] = original.copy()
                materials[original].name = 'PSX_' + original.name
            slot.material = materials[original]

def material(key):
    return next(m for old, m in materials.items() if old.name.split('.')[0] == 'PL_' + key)

def texture(key, colors, kind):
    rng = random.Random(758 + len(key))
    size = 128
    grid = [[rng.random() for x in range(32)] for y in range(32)]
    pixels = []
    for y in range(size):
        for x in range(size):
            value = grid[y//4][x//4]
            if kind == 'stone':
                value = grid[(y//8)%32][(x//12 + (y//8)%2)%32]
                if y%8 == 0 or (x + (y//8)%2*6)%12 == 0:
                    value *= .35
            elif kind == 'wood':
                value = .7*grid[y//16][(x//2)%32] + .3*value
            color = colors[min(len(colors)-1, int(value*len(colors)))]
            noise = rng.uniform(.85, 1.12)
            pixels.extend([min(1, c*noise) for c in color] + [1])
    image = bpy.data.images.new('PSX_' + key, width=size, height=size)
    image.pixels.foreach_set(pixels)
    image.filepath_raw = str(OUT/'Textures'/('PSX_'+key+'.png'))
    image.file_format = 'PNG'
    image.save()
    image.pack()
    mat = material(key)
    nodes = mat.node_tree.nodes
    image_nodes = [n for n in nodes if n.type == 'TEX_IMAGE']
    node = image_nodes[0] if image_nodes else nodes.new('ShaderNodeTexImage')
    node.image = image
    node.interpolation = 'Closest'
    bsdf = next(n for n in nodes if n.type == 'BSDF_PRINCIPLED')
    mat.node_tree.links.new(node.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = .95

texture('Path', [(.075,.065,.052),(.15,.126,.095),(.23,.20,.15),(.32,.29,.23),(.42,.39,.32)], 'soil')
texture('Soil', [(.045,.043,.032),(.085,.08,.051),(.14,.135,.075),(.21,.19,.105)], 'soil')
texture('Rock', [(.07,.075,.08),(.16,.17,.175),(.25,.26,.26),(.35,.35,.32)], 'stone')
texture('Bark', [(.055,.043,.034),(.12,.095,.065),(.22,.18,.12),(.30,.25,.17)], 'wood')
texture('Foliage', [(.025,.035,.032),(.045,.062,.048),(.075,.09,.06),(.12,.13,.075)], 'soil')

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
    for tier in range(11):
        z = 2.3 + tier*.94
        radius = (13.7-z)*.22
        for branch in range(5):
            a = branch*math.tau/5 + tier*1.79 + variant*.5
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

# Keep bank footprints and collision guides; break their broad planar surfaces.
for obj in scene.objects:
    if obj.type == 'MESH' and obj.name.startswith('PL_Bank_'):
        for v in obj.data.vertices:
            height = v.co.z-ground(v.co.y)
            if height > 1:
                v.co.z += .27*math.sin(v.co.x*3.7+v.co.y*2.1)

# Sparse opaque undergrowth at the outside of travel surfaces, batched per route.
layout = json.loads((OUT/'layout.json').read_text())
for route in ('A','B'):
    verts, faces = [], []
    rng = random.Random(71+ord(route))
    points = layout['paths'][route]
    for aa, bb in zip(points, points[1:]):
        a,b = Vector(aa),Vector(bb)
        tangent = (b-a).normalized()
        normal = Vector((-tangent.y,tangent.x))
        for j in range(max(1,int((b-a).length*3))):
            p = a.lerp(b,rng.random()) + normal*rng.choice((-1,1))*(layout['widths'][route]/2+rng.uniform(.05,.38))
            z = ground(p.y)+.03
            for angle in (rng.random()*math.tau, rng.random()*math.tau):
                w,h = rng.uniform(.12,.28),rng.uniform(.18,.6)
                dx,dy = math.cos(angle)*w,math.sin(angle)*w
                n = len(verts)
                verts.extend([(p.x-dx,p.y-dy,z),(p.x+dx,p.y+dy,z),(p.x+dx*.3,p.y+dy*.3,z+h)])
                faces.extend([(n,n+1,n+2),(n+2,n+1,n)])
    mesh('PSX_EdgeUndergrowth_'+route,verts,faces,[material('Soil')],target=collection('Route_'+route))

world = scene.world.copy()
world.name = 'PSX_ColdForestWorld'
scene.world = world
bg = next(n for n in world.node_tree.nodes if n.type == 'BACKGROUND')
bg.inputs['Color'].default_value = (.075,.09,.13,1)
bg.inputs['Strength'].default_value = .45
# Presentation-only atmospheric volume. Unity should use cheap distance fog.
fogmat = bpy.data.materials.new('PSX_PresentationFog')
fogmat.use_nodes = True
fogmat.node_tree.nodes.clear()
volume = fogmat.node_tree.nodes.new('ShaderNodeVolumeScatter')
volume.inputs['Color'].default_value = (.30,.35,.44,1)
volume.inputs['Density'].default_value = .009
volume.inputs['Anisotropy'].default_value = .15
output = fogmat.node_tree.nodes.new('ShaderNodeOutputMaterial')
fogmat.node_tree.links.new(volume.outputs[0],output.inputs['Volume'])
fog = mesh('PSX_PresentationFog', [(-240,-80,-30),(240,-80,-30),(240,680,-30),(-240,680,-30),(-240,-80,100),(240,-80,100),(240,680,100),(-240,680,100)], [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)], [fogmat], target=collection('Presentation'))
fog.display_type = 'WIRE'
for obj in collection('Presentation').objects:
    if obj.type == 'LIGHT':
        obj.data = obj.data.copy()
        if obj.data.type == 'SUN':
            obj.data.energy = .85
            obj.data.color = (.58,.67,.86)
            obj.data.angle = .3
scene.view_settings.view_transform = 'AgX'
scene.view_settings.exposure = .7
scene.render.resolution_x = 1280
scene.render.resolution_y = 720
scene.render.resolution_percentage = 100
scene.camera.data = scene.camera.data.copy()
scene.camera.data.type = 'PERSP'
scene.camera.data.lens = 28
scene.camera.location = (-82,150,ground(150)+2.1)
scene.camera.rotation_euler = (Vector((-108,177,ground(177)+2.7))-scene.camera.location).to_track_quat('-Z','Y').to_euler()
scene['reference_style'] = '2026-10-23: cold PS1 forest, sparse spruce branches, pixel ground, blue-gray haze'
scene['unity_fog_note'] = 'Use URP distance fog; Blender world volume is presentation only.'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Source/Pohorje_Large.blend'))
scene.render.filepath = str(OUT/'Previews/12-reference-forest.png')
bpy.ops.render.render(write_still=True)
print('Reference style saved and rendered.')
