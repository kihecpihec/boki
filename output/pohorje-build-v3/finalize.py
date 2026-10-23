"""Export and render the current PH03 source without rebuilding user edits."""
import bpy, math, json, io, contextlib
from pathlib import Path
from mathutils import Vector

OUT=Path('/home/ismarm/GAMEJAM/boki/output/pohorje-build-v3')
scene=bpy.data.scenes['Pohorje_World'];bpy.context.window.scene=scene
warnings=[]
for c in scene.collection.children:
    if c.name.endswith('NOT_EXPORT') or 'ModuleLibrary' in c.name:continue
    bpy.ops.object.select_all(action='DESELECT')
    hidden=[]
    for o in c.objects:
        if o.type=='MESH':
            hidden.append((o,o.hide_viewport));o.hide_viewport=False;o.select_set(True)
    if not bpy.context.selected_objects:continue
    # Export linked instances using temporary copies; preserve the editable source.
    saved=[];capture=io.StringIO()
    try:
        for o in list(bpy.context.selected_objects):
            old=o.data;o.data=old.copy();saved.append((o,old,o.data))
        with contextlib.redirect_stdout(capture):
            bpy.ops.export_scene.fbx(filepath=str(OUT/'Export'/(c.name+'.fbx')),use_selection=True,
                object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,
                add_leaf_bones=False,path_mode='COPY',embed_textures=True)
    finally:
        for o,old,temp in saved:o.data=old;bpy.data.meshes.remove(temp)
        for o,state in hidden:o.hide_viewport=state
    warnings.extend(line for line in capture.getvalue().splitlines() if 'WARNING' in line)
cam=scene.camera;cd=cam.data
def shot(name,pos,target,ortho=None):
    cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
    cd.type='ORTHO' if ortho else 'PERSP'
    if ortho:cd.ortho_scale=ortho
    else:cd.lens=25
    scene.render.filepath=str(OUT/'Previews'/(name+'.png'));bpy.ops.render.render(write_still=True)
def h(y):return 8 if y<=22 else 8-(y-22)*6/42 if y<64 else 2 if y<=82 else max(0,2-(y-82)*2/22)
shot('01-world-overview',(134,-125,142),(0,55,2),165)
shot('02-bellevue',(1,-7,9.65),(0,24,8.2))
shot('03-forest-A',(-32,33,h(33)+1.65),(-33,53,h(53)+1.4))
shot('04-service-B',(0,34,h(34)+1.65),(1,56,h(56)+2))
shot('05-rave-C',(32,35,h(35)+1.65),(32,59,h(59)+1.5))
roofs=bpy.data.collections['PH03_C_RemovableRoofs'];roofs.hide_render=True
shot('06-cutaway-overview',(134,-125,142),(0,55,2),165);roofs.hide_render=False
cam.location=(134,-125,142);cam.rotation_euler=(Vector((0,55,2))-cam.location).to_track_quat('-Z','Y').to_euler();cd.type='ORTHO';cd.ortho_scale=165
bpy.ops.object.select_all(action='DESELECT')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'ENV_Pohorje_World.blend'))
(OUT/'export-validation.json').write_text(json.dumps({'fbx_material_warnings':warnings,'unity_import':'pending'},indent=2)+'\n')
print('FBX warnings:',len(warnings))
