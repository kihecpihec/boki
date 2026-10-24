"""Split travel polygons at slope changes so wide cross-sections stay continuous."""
import bpy

def grade(y):
    return 36 if y<=90 else 36-(y-90)*28/210 if y<300 else 8 if y<=360 else 8-(y-360)*8/130 if y<490 else 0

def clip(poly,bound,keep_above):
    out=[]
    for a,b in zip(poly,poly[1:]+poly[:1]):
        ia=a[1]>=bound-1e-7 if keep_above else a[1]<=bound+1e-7
        ib=b[1]>=bound-1e-7 if keep_above else b[1]<=bound+1e-7
        if ia:out.append(a)
        if ia!=ib:
            t=(bound-a[1])/(b[1]-a[1]);out.append((a[0]+t*(b[0]-a[0]),bound,grade(bound)+.025))
    return out

for o in bpy.data.scenes['Pohorje_Large'].objects:
    if o.type!='MESH' or not o.name.startswith(('PL_Travel_','PL_COL_Travel_')):continue
    verts=[];faces=[];index={}
    for face in o.data.polygons:
        poly=[tuple(o.data.vertices[i].co) for i in face.vertices]
        for lo,hi in [(-1000,90),(90,300),(300,360),(360,490),(490,1000)]:
            piece=clip(clip(poly,lo,True),hi,False)
            ids=[]
            for x,y,z in piece:
                key=(round(x,6),round(y,6),round(grade(y)+.025,6))
                if key not in index:index[key]=len(verts);verts.append(key)
                if not ids or ids[-1]!=index[key]:ids.append(index[key])
            if len(ids)>1 and ids[0]==ids[-1]:ids.pop()
            if len(set(ids))>=3:faces.append(ids)
    o.data.clear_geometry();o.data.from_pydata(verts,[],faces);o.data.update()
    if o.name.startswith('PL_Travel_'):
        uv=o.data.uv_layers.get('UVMap') or o.data.uv_layers.new(name='UVMap')
        for loop in o.data.loops:
            v=o.data.vertices[loop.vertex_index].co;uv.data[loop.index].uv=(v.x/4,v.y/4)
print('Wide travel surfaces split at grade transitions')
