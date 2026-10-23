"""Validate, export and render the current large Pohorje scene. No Unity mutation."""
import bpy, math, json, io, contextlib, bmesh
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree

OUT=Path(__file__).resolve().parent
scene=bpy.data.scenes['Pohorje_Large'];bpy.context.window.scene=scene
layout=json.loads((OUT/'layout.json').read_text());paths=layout['paths'];widths=layout['widths'];pockets=layout['pockets']
def original_y(y):
    knots=layout.get('coordinate_mapping',{}).get('y_knots')
    if not knots:return y
    for (a,c),(b,d) in zip(knots,knots[1:]):
        if y<=d:return a+(y-c)*(b-a)/(d-c)
    return 544+(y-338)*2

def z(y):
    if 'ground_height_m' in layout:return layout['ground_height_m']
    y=original_y(y)
    return 36 if y<=90 else 36-(y-90)*28/210 if y<300 else 8 if y<=360 else 8-(y-360)*8/130 if y<490 else 0

def old_z(y):
    return 36 if y<=90 else 36-(y-90)*28/210 if y<300 else 8 if y<=360 else 8-(y-360)*8/130 if y<490 else 0

def view_position(pos):
    if not layout.get('coordinate_mapping'):return pos
    x,y,h=pos
    knots=layout['coordinate_mapping']['y_knots']
    for (a,c),(b,d) in zip(knots,knots[1:]):
        if y<=b:return (x*1.25,c+(y-a)*(d-c)/(b-a),h)
    return (x*1.25,338+(y-544)*.5,h)
def collection(key):return next(c for c in scene.collection.children if c.name.startswith('PL_'+key))

# Real mesh checks, not estimates from source asset budgets.
report={'scene':scene.name,'unity_import':'pending','windows_5600g_performance':'unmeasured'}
rendercols=[c for c in scene.collection.children if not any(k in c.name for k in ('Module_Library','Collision_Guides','Gameplay_Markers','Presentation'))]
total=0;renderobjects=0;bad=[];budget=[]
for c in rendercols:
    count=0
    for o in c.objects:
        if o.type!='MESH':continue
        renderobjects+=1;o.data.calc_loop_triangles();count+=len(o.data.loop_triangles)
        zero=sum(p.area<1e-10 for p in o.data.polygons)
        if zero:bad.append({'object':o.name,'zero_area_faces':zero})
    budget.append({'collection':c.name,'triangles':count});total+=count
report.update(render_triangles_all_routes=total,render_mesh_objects=renderobjects,collections=budget,zero_area_meshes=bad)
modules=[]
for o in collection('Module_Library').objects:
    if o.type!='MESH':continue
    o.data.calc_loop_triangles();bm=bmesh.new();bm.from_mesh(o.data)
    modules.append({'name':o.name,'triangles':len(o.data.loop_triangles),'nonmanifold_edges':sum(not e.is_manifold for e in bm.edges),'zero_area_faces':sum(f.calc_area()<1e-10 for f in bm.faces),'unit_scale':all(abs(v-1)<1e-6 for v in o.scale)})
    bm.free()
report['modules']=modules

# Ray-test the actual travel render surfaces and nearby scenery at sampled lane positions.
def bvhs(objects):
    result=[]
    for o in objects:
        if o.type!='MESH':continue
        verts=[o.matrix_world@v.co for v in o.data.vertices]
        faces=[list(p.vertices) for p in o.data.polygons]
        if faces:result.append((o.name,BVHTree.FromPolygons(verts,faces)))
    return result
travel=[o for c in rendercols for o in c.objects if o.name.startswith('PL_Travel_')]
floor_trees=bvhs(travel)
terrain_trees=bvhs(collection('Terrain_Chunks').objects)
issues=[];surface_samples=0;terrain_occlusion=[]
for key,points in paths.items():
    for a,b in zip(points,points[1:]):
        a,b=Vector(a),Vector(b);t=(b-a).normalized();n=Vector((-t.y,t.x));length=(b-a).length
        for i in range(max(2,math.ceil(length/3))):
            p=a+(b-a)*(i+.5)/max(2,math.ceil(length/3))
            for off in (0,-widths[key]*.3,widths[key]*.3):
                q=p+n*off;expected=z(q.y)+.025;origin=Vector((q.x,q.y,expected+2));hits=[]
                for name,tree in floor_trees:
                    point,normal,idx,distance=tree.ray_cast(origin,Vector((0,0,-1)),4)
                    if point is not None:hits.append((point.z,normal.z))
                surface_samples+=1
                if not hits or min(abs(h[0]-expected) for h in hits)>.2:issues.append({'route':key,'xy':list(q),'reason':'unsupported or seam height mismatch'})
                for name,tree in terrain_trees:
                    point,normal,idx,distance=tree.ray_cast(origin,Vector((0,0,-1)),2)
                    if point is not None and point.z>expected+.1:
                        terrain_occlusion.append({'route':key,'xy':list(q),'terrain':name,'height_above_m':round(point.z-expected,3)});break
report['travel_checks']={'sample_count':surface_samples,'surface_issues':issues,'terrain_above_lane':terrain_occlusion}

# Source geometry/data remain linked; only temporary export meshes become unique.
export_results=[]
def export(name,objects):
    bpy.ops.object.select_all(action='DESELECT');saved=[];hidden=[];capture=io.StringIO()
    viewstates=[]
    for c in scene.collection.children:
        viewstates.append((c,c.hide_viewport));c.hide_viewport=False
    try:
        for o in objects:
            if o.type not in ('MESH','EMPTY'):continue
            hidden.append((o,o.hide_viewport));o.hide_viewport=False;o.hide_set(False);o.select_set(True)
            if o.type=='MESH':old=o.data;o.data=old.copy();saved.append((o,old,o.data))
        with contextlib.redirect_stdout(capture):
            bpy.ops.export_scene.fbx(filepath=str(OUT/'Export'/(name+'.fbx')),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,path_mode='COPY',embed_textures=True)
    finally:
        for o,old,temp in saved:o.data=old;bpy.data.meshes.remove(temp)
        for o,state in hidden:o.hide_viewport=state
        for c,state in viewstates:c.hide_viewport=state
    warnings=[line for line in capture.getvalue().splitlines() if 'WARNING' in line]
    export_results.append({'file':name+'.fbx','mesh_count':len(saved),'warnings':warnings})
for c in rendercols:export(c.name.split('.')[0],list(c.objects))
export('Pohorje_CollisionGuides',list(collection('Collision_Guides').objects))
export('Pohorje_GameplayMarkers',list(collection('Gameplay_Markers').objects))
for o in collection('Module_Library').objects:
    export(o.name.split('.')[0].removeprefix('PL_'),[o])
report['exports']=export_results
(OUT/'validation.json').write_text(json.dumps(report,indent=2)+'\n')

# Establish delivery views from the actual scene, with human-height views on each route.
cam=scene.camera;cd=cam.data
fog=scene.objects.get('PSX_PresentationFog')
scene.eevee.taa_render_samples=16
scene.render.resolution_x=1280
scene.render.resolution_y=800
def shot(name,pos,target,ortho=None):
    if fog: fog.hide_render=bool(ortho)
    pos=view_position(pos);target=view_position(target)
    cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
    cd.type='ORTHO' if ortho else 'PERSP'
    if ortho:cd.ortho_scale=ortho if layout.get('revision')=='action_layout' else ortho*.78
    else:cd.lens=24
    scene.render.filepath=str(OUT/'Previews'/(name+'.png'));bpy.ops.render.render(write_still=True)
if layout.get('revision')=='action_layout':
    shot('01-whole-pohorje',(190,-175,220),(0,48,6),270)
    shot('02-bellevue',(0,-23,12),(0,15,10))
    shot('03-forest-traversal',(-21,0,11),(-56,30,12))
    shot('04-forest-combat',(-93,13,35),(-61,54,9))
    shot('05-gondola',(25,5,12),(57,48,16))
    shot('06-service-combat',(101,10,39),(61,53,12))
    shot('07-rave-tunnel',(0,10,11),(0,42,10))
    shot('08-rave-combat',(-18,36,16),(2,68,10))
    shot('09-city-overlook',(0,103,11),(0,124,9))
    roof=collection('C_Removable_Roofs');roof.hide_render=True
    shot('10-cutaway-map',(185,-155,210),(0,48,7),260)
    shot('11-top-down',(0,50,300),(0,50,0),245)
    shot('16-forest-arena-layout',(-100,11,65),(-64,56,8),88)
    shot('17-service-arena-layout',(105,12,72),(64,53,9),88)
    shot('18-rave-arena-layout',(-43,3,70),(0,59,8),84)
    roof.hide_render=False
    shot('12-reference-forest',(-78,27,11),(-60,67,12))
    shot('13-forest-canopy',(-84,44,12),(-58,74,15))
    shot('14-maintenance-approach',(44,22,11),(59,54,17))
    shot('15-tunnel-approach',(0,26,11),(0,63,10))
else:
    shot('01-whole-pohorje',(480,-340,500),(0,250,12),690)
    shot('02-bellevue',(0,-5,37.7),(0,45,36.8))
    shot('03-forest-traversal',(-70,132,old_z(132)+2.1),(-96,163,old_z(163)+2.4))
    shot('04-forest-combat',(-126,302,15.5),(-94,338,9.5))
    shot('05-gondola',(0,125,old_z(125)+1.8),(20,148,old_z(148)+3))
    shot('06-service-combat',(-23,302,15),(4,336,9.5))
    shot('07-rave-tunnel',(100,218,old_z(218)+1.8),(100,250,old_z(250)+1.6))
    shot('08-rave-combat',(82,302,12.8),(103,330,9.5))
    shot('09-city-overlook',(0,502,1.8),(0,650,-4))
    roof=collection('C_Removable_Roofs');roof.hide_render=True
    shot('10-cutaway-map',(480,-340,500),(0,250,12),690);roof.hide_render=False
    shot('11-top-down',(0,250,900),(0,250,0),650)
    shot('12-reference-forest',(-70,132,old_z(132)+2.1),(-96,163,old_z(163)+2.4))
    shot('13-forest-canopy',(-100,205,old_z(205)+2.1),(-114,224,old_z(224)+3.0))
    shot('14-maintenance-approach',(8,130,old_z(130)+2.1),(27,144,old_z(144)+3.1))
    shot('15-tunnel-approach',(100,225,old_z(225)+2.1),(100,250,old_z(250)+2.5))
if fog: fog.hide_render=False
bpy.ops.object.select_all(action='DESELECT')
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Source/Pohorje_Large.blend'))
print(json.dumps({'render_triangles':total,'render_objects':renderobjects,'sample_count':surface_samples,'surface_issues':len(issues),'terrain_occlusions':len(terrain_occlusion),'export_warnings':sum(len(r['warnings']) for r in export_results)},indent=2))
