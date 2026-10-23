"""Sample static collision guides across each lane; excludes intentional gate leaves."""
import bpy, math, json
from mathutils import Vector
from pathlib import Path

OUT=Path(__file__).resolve().parent if '__file__' in globals() else Path('/home/ismarm/GAMEJAM/boki/output/world-modeling-v1/maps/pohorje')
scene=bpy.data.scenes['Pohorje_Large'];bpy.context.window.scene=scene;bpy.context.view_layer.update()
c=next(c for c in scene.collection.children if c.name.startswith('PL_Collision_Guides'))
boxes=[]
for o in c.objects:
    if o.type!='MESH' or o.get('dynamic_gate') or o.name.startswith(('PL_COL_Travel','PL_COL_Bellevue','PL_COL_City_','PL_COL_A_Combat','PL_COL_B_Combat','PL_COL_C_Combat')):continue
    lo=Vector(tuple(min(v.co[i] for v in o.data.vertices) for i in range(3)))
    hi=Vector(tuple(max(v.co[i] for v in o.data.vertices) for i in range(3)))
    boxes.append((o.name,o.matrix_world.inverted(),lo,hi))
p=json.loads((OUT/'layout.json').read_text());problems=[];sample_count=0
def z(y):return 36 if y<=90 else 36-(y-90)*28/210 if y<300 else 8 if y<=360 else 8-(y-360)*8/130 if y<490 else 0
for key,points in p['paths'].items():
    for aa,bb in zip(points,points[1:]):
        a,b=Vector(aa),Vector(bb);n=Vector((-(b-a).y,(b-a).x)).normalized()
        count=max(1,math.ceil((b-a).length/4))
        for i in range(count):
            q=a+(b-a)*(i+.5)/count;blocked=[];sample_count+=1
            for off in (-.3*p['widths'][key],0,.3*p['widths'][key]):
                v=q+n*off;point=Vector((v.x,v.y,z(v.y)+1));hits=[]
                for name,inv,lo,hi in boxes:
                    local=inv@point
                    if all(lo[j]-.35<local[j]<hi[j]+.35 for j in range(3)):hits.append(name)
                blocked.append(hits)
            if all(blocked):problems.append({'route':key,'xy':list(q),'blockers':blocked})
report={'station_count':sample_count,'all_three_lanes_blocked':problems,'note':'Static guide point samples at center and +/-30% lane width, 0.35 m capsule margin. Dynamic gate leaves excluded. This is not a Unity physics or camera test.'}
(OUT/'collision-sampling.json').write_text(json.dumps(report,indent=2)+'\n')
print('Static collision stations:',sample_count,'full-width obstructions:',len(problems))
