"""Save a non-camera, eye-level viewport at Bellevue for easy Walk Navigation."""
import bpy
from pathlib import Path
from mathutils import Vector

scene = bpy.data.scenes['Pohorje_Large']
bpy.context.window.scene = scene
eye = Vector((0,20,37.8))
rotation = Vector((0,1,-.025)).to_track_quat('-Z','Y')
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type != 'VIEW_3D':
            continue
        space = area.spaces.active
        space.clip_start = .05
        space.clip_end = 1500
        space.lens = 28
        space.region_3d.view_perspective = 'PERSP'
        space.region_3d.view_distance = .1
        space.region_3d.view_rotation = rotation
        space.region_3d.view_location = eye-rotation@Vector((0,0,.1))
        space.overlay.show_overlays = False
        space.shading.type = 'SOLID'
        space.shading.color_type = 'MATERIAL'
        space.shading.show_shadows = True
        space.lock_camera = False
text = bpy.data.texts.get('START_HERE_WALKTHROUGH') or bpy.data.texts.new('START_HERE_WALKTHROUGH')
text.clear()
text.write('''POHORJE WALKTHROUGH

The saved viewport starts at eye height at Bellevue, looking toward the fork.
Hover over the 3D viewport. Press F3, type Walk Navigation, and press Enter.
Alternatively use View > Navigation > Walk Navigation, or Shift + grave accent.

Mouse: look. W/A/S/D: move. Wheel: change speed. Shift: temporarily faster.
Q/E: down/up with gravity off. Tab: toggle gravity. Left click: keep view and exit.
Esc: cancel and return to where navigation started.

Start with gravity off to inspect the full environment freely. Gravity can make
the view stand on rendered floors; this is not Boki's controller or Unity physics.
Use Q/E to move past parked gate art or through the tunnel roof when inspecting.

Z then M: Material Preview for textures. Z then S: fast Solid display.
The saved viewport uses Solid for responsive exploration. Rendered mood, fog,
and lighting are shown in the Previews folder. Home frames the entire map;
Numpad 7 gives a top-down view. F3 > View Camera exits a camera view if needed.

See WALKTHROUGH.md beside the source scripts for detailed instructions.
''')
path = Path(__file__).resolve().parent/'Source/Pohorje_Large.blend'
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(path))
print('Saved eye-level Bellevue viewport; walkthrough instructions included as a Blender text block.')
