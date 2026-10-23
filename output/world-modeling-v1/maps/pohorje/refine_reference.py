"""Finish the reference style on the existing styled source, without changing layout."""
import bpy
import hashlib
import json
import math
import random
from pathlib import Path

OUT = Path(__file__).resolve().parent
scene = bpy.data.scenes['Pohorje_Large']
bpy.context.window.scene = scene

def protected_geometry():
    result = {}
    for obj in scene.objects:
        if obj.name.startswith(('PL_Travel_', 'PL_COL_')) or obj.type == 'EMPTY':
            values = [list(row) for row in obj.matrix_world]
            if obj.type == 'MESH':
                values += [list(v.co) for v in obj.data.vertices]
            values.append(dict(obj.items()))
            result[obj.name] = hashlib.sha256(json.dumps(values, sort_keys=True).encode()).hexdigest()
    return result

before = protected_geometry()
# Irregular pixel stone replaces the brick pattern. Shared 128-square images,
# nearest sampling, and flat rough surfaces retain the low-resolution direction.
for key, palette in {
    'Rock': [(0.065,.069,.073),(.12,.126,.13),(.20,.205,.20),(.29,.285,.26)],
    'Path': [(.055,.046,.036),(.115,.096,.068),(.20,.17,.12),(.30,.27,.205),(.40,.37,.30)],
}.items():
    rng = random.Random(758 + len(key))
    cells = [(rng.randrange(128),rng.randrange(128),rng.uniform(2,7),rng.randrange(len(palette))) for _ in range(330)]
    pixels = []
    for y in range(128):
        for x in range(128):
            px,py = x//2*2,y//2*2
            cell = min(cells, key=lambda c: (min(abs(px-c[0]),128-abs(px-c[0]))**2 + min(abs(py-c[1]),128-abs(py-c[1]))**2)/c[2])
            color = palette[cell[3]]
            noise = rng.uniform(.84,1.12)
            pixels.extend([v*noise for v in color]+[1])
    image = bpy.data.images.get('PSX_'+key)
    image.pixels.foreach_set(pixels)
    image.filepath_raw = str(OUT/'Textures'/('PSX_'+key+'.png'))
    image.save()
    image.pack()
    fresh = bpy.data.images.load(str(OUT/'Textures'/('PSX_'+key+'.png')), check_existing=False)
    for mat in bpy.data.materials:
        if mat.use_nodes:
            for node in mat.node_tree.nodes:
                if node.type == 'TEX_IMAGE' and node.image == image:
                    node.image = fresh
    image.name = 'Unused_' + key
    fresh.name = 'PSX_' + key
    fresh.pack()

bg = next(n for n in scene.world.node_tree.nodes if n.type == 'BACKGROUND')
bg.inputs['Color'].default_value = (.032,.040,.061,1)
bg.inputs['Strength'].default_value = .35
scene.view_settings.exposure = .35
fog = bpy.data.materials['PSX_PresentationFog']
volume = next(n for n in fog.node_tree.nodes if n.type == 'VOLUME_SCATTER')
volume.inputs['Density'].default_value = .013
volume.inputs['Color'].default_value = (.22,.265,.35,1)

# Remove only superseded Pohorje scene variants, including their unused objects.
removed = []
for old in list(bpy.data.scenes):
    if old.name in ('Pohorje_FirstPass','Pohorje_Restrained','Pohorje_World'):
        removed.append(old.name)
        objects = list(old.objects)
        collections = list(old.collection.children_recursive)
        bpy.data.scenes.remove(old)
        for obj in objects:
            if not obj.users_scene:
                bpy.data.objects.remove(obj, do_unlink=True)
        for col in reversed(collections):
            if col.users == 0:
                bpy.data.collections.remove(col)

after = protected_geometry()
assert before == after, 'Protected travel, collision, or marker data changed'
report = {
    'protected_objects': len(before),
    'travel_collision_marker_hashes_unchanged': before == after,
    'removed_scene_versions': removed,
    'layout_dimensions': json.loads((OUT/'layout.json').read_text())['stats'],
    'unity_import': 'pending; source assets are outside Game/Assets',
}
(OUT/'style-validation.json').write_text(json.dumps(report,indent=2)+'\n')
scene['reference_style'] = 'Cold blue-gray forest haze, sparse spruce silhouettes, irregular pixel earth and stone, restrained warm lights'
for image in bpy.data.images:
    if image.packed_file and (OUT/'Textures'/Path(image.filepath).name).exists():
        image.filepath = str(OUT/'Textures'/Path(image.filepath).name)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Source/Pohorje_Large.blend'))
print(json.dumps(report,indent=2))
