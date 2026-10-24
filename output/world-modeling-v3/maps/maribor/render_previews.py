"""Render the saved model without rebuilding or modifying it.

Blender -b Source/Maribor_Production.blend --python render_previews.py -- 01-bellevue 08-cistern
With no shot names, renders every review camera. Camera coordinates are in manifest.json.
"""
import json
from pathlib import Path
import sys

import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
manifest = json.loads((HERE/'manifest.json').read_text())
selected = sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
P = lambda p: Vector((-p[0], -p[2], p[1]))
scene = bpy.context.scene
camera = scene.camera
for obj in scene.objects:
    if obj.name.startswith('PL_Seal_'): obj.hide_render = True
for shot in manifest['reviewCameras']:
    if selected and shot['name'] not in selected: continue
    camera.location = P(shot['eye'])
    camera.rotation_euler = (P(shot['target'])-camera.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath = str(HERE/'Previews'/('blender-'+shot['name']+'.png'))
    bpy.ops.render.render(write_still=True)
print('MARIBOR_PREVIEWS_DONE')
