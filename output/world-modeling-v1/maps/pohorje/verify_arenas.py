"""Check revised floor/guide alignment, underlay clearance, aprons and charges."""
import bpy,json,math
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
OUT=Path(__file__).resolve().parent
layout=json.loads((OUT/'layout.json').read_text())
markers=json.loads((OUT/'markers.json').read_text())
scene=bpy.data.scenes['Pohorje_Large']
def tree(o):
    return BVHTree.FromPolygons([o.matrix_world@v.co for v in o.data.vertices],[list(p.vertices) for p in o.data.polygons])
def hit(t,x,y):return t.ray_cast(Vector((x,y,40)),Vector((0,0,-1)),80)[0]
terrain=[tree(o) for o in bpy.data.collections['PL_Terrain_Chunks'].objects if o.type=='MESH']
issues=[];samples=0;charges=0;maximum_slope=0;arena_reports=[]
for k,(cx,cy,w,d) in layout['pockets'].items():
    obj=bpy.data.objects['PL_'+k+'_CombatSpace'];floor=tree(obj)
    guide=tree(bpy.data.objects['PL_COL_'+k+'_CombatSpace'])
    for j in range(1,21):
        for i in range(1,21):
            x=cx-w/2+w*i/21;y=cy+d*j/21
            a=hit(floor,x,y);b=hit(guide,x,y);samples+=1
            if a is None or b is None or (a-b).length>.001:issues.append([k,'floor/guide mismatch',x,y]);continue
            if any((h:=hit(t,x,y)) is not None and h.z>a.z+.05 for t in terrain):issues.append([k,'terrain above floor',x,y])
    for end in (cy,cy+d):
        for dx in (-3,0,3):
            entry=cx+12 if layout.get('revision')=='action_layout' and k=='A' else cx-12 if layout.get('revision')=='action_layout' and k=='B' else cx
            h=hit(floor,entry+dx,end+.01 if end==cy else end-.01)
            if h is None or abs(h.z-8)>.01:issues.append([k,'entrance/exit apron mismatch'])
    slope=max(math.degrees(math.acos(min(1,f.normal.z))) for f in obj.data.polygons)
    maximum_slope=max(maximum_slope,slope)
    arena_reports.append(dict(route=k,dimensions_m=[w,d],area_m2=w*d,min_height_m=min(v.co.z for v in obj.data.vertices),max_height_m=max(v.co.z for v in obj.data.vertices),maximum_triangle_slope_degrees=round(slope,2)))
    for m in markers:
        if m['route']==k and m['kind'].startswith('stationary'):
            x,y,z=m['xyz'];h=hit(floor,x,y);charges+=1
            if h is None or abs(h.z-z)>.12:issues.append([k,'charge ground mismatch',m['name']])
ramp_checks=[]
for k in 'ABC':
    col=bpy.data.collections['PL_Route_'+k]
    floors=[tree(bpy.data.objects['PL_'+k+'_CombatSpace'])]
    landing_names={'A':['PL_A_BridgeSurface'],'B':['PL_B_TerminalDeck'],'C':['PL_C_Stage']}[k]
    floors.extend(tree(bpy.data.objects[name]) for name in landing_names)
    for o in col.objects:
        if o.type!='MESH' or 'Ramp' not in o.name:continue
        max_gap=0
        for v in o.data.vertices:
            p=o.matrix_world@v.co
            heights=[h.z for t in floors if (h:=t.ray_cast(p+Vector((0,0,.15)),Vector((0,0,-1)),3)[0]) is not None]
            gap=min((abs(p.z-z) for z in heights),default=99);max_gap=max(max_gap,gap)
        slope=max(math.degrees(math.acos(min(1,f.normal.z))) for f in o.data.polygons)
        ramp_checks.append(dict(name=o.name,maximum_landing_gap_m=round(max_gap,3),maximum_slope_degrees=round(slope,2)))
        if max_gap>.15 or slope>40:issues.append([k,'ramp landing or slope',o.name,max_gap,slope])
report=dict(ramp_checks=ramp_checks,floor_samples=samples,charge_locations_checked=charges,maximum_slope_degrees=round(maximum_slope,2),issues=issues,
            note='Source geometry only; Unity collision, navigation, controller and Windows performance remain pending.')
(OUT/'arena-revision.json').write_text(json.dumps(dict(revision=layout.get('revision'),arenas=arena_reports,route_stats=layout['stats']['route_stats']),indent=2)+'\n')
(OUT/'arena-sampling.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report))
assert not issues
