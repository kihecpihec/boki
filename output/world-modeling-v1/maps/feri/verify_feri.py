"""Validate saved FERI source and every delivered FBX without modifying source."""
import bpy, json, runpy, sys, math
from pathlib import Path
from mathutils import Vector
from collections import deque
OUT=Path(__file__).resolve().parent; ROOT=OUT.parents[3]
validator=ROOT/'.agents/skills/boki-map-model-maker/scripts/validate_map.py'
sys.argv=['validate_map.py','--','--json',str(OUT/'validation.json'),'--max-tris','150000']
runpy.run_path(str(validator),run_name='__main__')
layout=json.loads((OUT/'layout.json').read_text()); cells={(c['x'],c['y']):c['owner'] for c in layout['cells']}
ramp=layout['ramp']
def ramp_height(x,y):
    return max(0,ramp['rise']-abs(y-ramp['center_y'])*ramp['rise']/ramp['run']) if abs(x-ramp['x'])<=2 and abs(y-ramp['center_y'])<=ramp['run'] else 0
checks={};errors=[]
for route in 'ABC':
    allowed={'Shared_Arrival','Shared_Landing','Arena_Classroom','Route_'+route}; seen={(0,4)};q=deque(seen)
    while q:
        x,y=q.popleft()
        for p in [(x+2,y),(x-2,y),(x,y+2),(x,y-2)]:
            if p not in seen and cells.get(p) in allowed:seen.add(p);q.append(p)
    checks['route_'+route+'_reaches_classroom']=tuple(layout['classroom_probe']) in seen
    if not checks['route_'+route+'_reaches_classroom']:errors.append('Disconnected '+route)
# Raycast actual floor geometry and check candidate marker supports.
floors=[o for o in bpy.context.scene.objects if o.name.startswith('PL_') and o.name.endswith('_Floor')]
def floor_at(x,y):
    for o in floors:
        inv=o.matrix_world.inverted();hit,p,n,i=o.ray_cast(inv@Vector((x,y,10)),Vector((0,0,-1)))
        if hit:return (o.matrix_world@p).z
    return None
sampled=0
for x,y in cells:
    z=floor_at(x+1,y+1);sampled+=1
    expected=ramp_height(x+1,y+1)
    if z is None or abs(z-expected)>.001:errors.append(f'Floor support {(x,y)}: {z}')
markers=json.loads((OUT/'markers.json').read_text())['markers']
for m in markers:
    if m['type'] not in ['Charge','Spawn','Guilt','Start']:continue
    z=floor_at(*m['position'][:2])
    if z is None or abs(z-m['position'][2])>.001:errors.append('Marker support '+m['name'])
checks['floor_samples']=sampled
checks['trap_count']=len([m for m in markers if m['name'].startswith('Event_FloorTrap_')]);assert checks['trap_count']==2
checks['boss_charge_count']=len([m for m in markers if m['name'].startswith('Charge_Classroom_')]);assert checks['boss_charge_count']==2
# Exclude dynamic seals and floor guide slabs from standing clearance.
guides=json.loads((OUT/'collision-guides.json').read_text())
for m in markers:
    if m['type'] not in ['Charge','Spawn','Guilt','Start']:continue
    x,y,z=m['position']
    for g in guides:
        if g['owner']=='Seals' or 'Floor' in g['name']:continue
        p=g['position'];s=g['size']
        if abs(x-p[0])<s[0]/2+.35 and abs(y-p[1])<s[1]/2+.35 and p[2]+s[2]/2>z+.05 and p[2]-s[2]/2<z+1.8:
            errors.append(f"Marker clearance {m['name']} / {g['name']}")
# All chunk imports must retain triangle totals, world bounds, and empty positions.
def metrics(obs):
    mesh=[o for o in obs if o.type=='MESH'];vs=[o.matrix_world@Vector(v) for o in mesh for v in o.bound_box]
    return {'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in mesh),'meshes':len(mesh),'bounds':[[min(v[i] for v in vs) for i in range(3)],[max(v[i] for v in vs) for i in range(3)]] if vs else None,'markers':{o.name:list(o.matrix_world.translation) for o in obs if o.type=='EMPTY'}}
source={c.name:metrics(list(c.objects)) for c in bpy.context.scene.collection.children if c.name!='Presentation'}
roundtrips={}
for file in sorted((OUT/'Export').glob('*.fbx')):
    bpy.ops.object.select_all(action='SELECT')
    for o in bpy.context.scene.objects:o.hide_set(False);o.select_set(True)
    bpy.ops.object.delete(use_global=False)
    for pool in [bpy.data.meshes,bpy.data.materials,bpy.data.images]:
        for block in list(pool):
            if block.users==0:pool.remove(block)
    bpy.ops.import_scene.fbx(filepath=str(file),use_anim=False)
    actual=metrics(list(bpy.context.scene.objects));expected=source[file.stem.removeprefix('FERI_')]
    good=actual['triangles']==expected['triangles'] and actual['meshes']==expected['meshes']
    delta=0
    if expected['bounds']:
        delta=max(abs(a-b) for rowa,rowb in zip(actual['bounds'],expected['bounds']) for a,b in zip(rowa,rowb));good &= delta<.001
    for name,pos in expected['markers'].items():
        actual_pos=actual['markers'].get(name)
        good &= actual_pos is not None and max(abs(a-b) for a,b in zip(pos,actual_pos))<.001
    roundtrips[file.name]={'passed':bool(good),'triangles':actual['triangles'],'bounds_error_m':delta}
    if not good:errors.append('Roundtrip '+file.name)
    # Run standard mesh hygiene validator on every clean import.
    bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
    sys.argv=['validate_map.py','--','--json',str(OUT/('validation-'+file.stem+'.json'))]
    runpy.run_path(str(validator),run_name='__main__')
report={'checks':checks,'roundtrip':roundtrips,'errors':errors,'status':'passed' if not errors else 'failed','limitations':['No Unity controller/camera or gameplay tests','No Windows target-hardware measurement','Grid and support checks do not prove full mesh-overlap or navigation clearance']}
(OUT/'verification.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
if errors:raise SystemExit(1)
