"""Validate, export and render the current large Pohorje scene. No Unity mutation."""
import bpy, math, json, io, contextlib, bmesh
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree

OUT=Path(__file__).resolve().parent
scene=bpy.data.scenes['Pohorje_Large'];bpy.context.window.scene=scene
layout=json.loads((OUT/'layout.json').read_text());paths=layout['paths'];widths=layout['widths'];pockets=layout['pockets']
def z(y):
    return 36 if y<=90 else 36-(y-90)*28/210 if y<300 else 8 if y<=360 else 8-(y-360)*8/130 if y<490 else 0
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
scene.eevee.taa_render_samples=32
def shot(name,pos,target,ortho=None):
    if fog: fog.hide_render=bool(ortho)
    cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
    cd.type='ORTHO' if ortho else 'PERSP'
    if ortho:cd.ortho_scale=ortho
    else:cd.lens=24
    scene.render.filepath=str(OUT/'Previews'/(name+'.png'));bpy.ops.render.render(write_still=True)
shot('01-whole-pohorje',(480,-340,500),(0,250,12),690)
shot('02-bellevue',(0,-5,37.7),(0,45,36.8))
shot('03-forest-traversal',(-70,132,z(132)+2.1),(-96,163,z(163)+2.4))
shot('04-forest-combat',(-100,303,9.8),(-100,343,9.5))
shot('05-gondola',(0,125,z(125)+1.8),(20,148,z(148)+3))
shot('06-service-combat',(0,305,9.8),(0,342,9.5))
shot('07-rave-tunnel',(100,218,z(218)+1.8),(100,250,z(250)+1.6))
shot('08-rave-combat',(100,304,9.8),(100,333,10.4))
shot('09-city-overlook',(0,502,1.8),(0,650,-4))
roof=collection('C_Removable_Roofs');roof.hide_render=True
shot('10-cutaway-map',(480,-340,500),(0,250,12),690);roof.hide_render=False
shot('11-top-down',(0,250,900),(0,250,0),650)
shot('12-reference-forest',(-70,132,z(132)+2.1),(-96,163,z(163)+2.4))
shot('13-forest-canopy',(-100,205,z(205)+2.1),(-114,224,z(224)+3.0))
shot('14-maintenance-approach',(8,130,z(130)+2.1),(27,144,z(144)+3.1))
shot('15-tunnel-approach',(100,225,z(225)+2.1),(100,250,z(250)+2.5))
if fog: fog.hide_render=False
bpy.ops.object.select_all(action='DESELECT')
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Source/Pohorje_Large.blend'))
print(json.dumps({'render_triangles':total,'render_objects':renderobjects,'sample_count':surface_samples,'surface_issues':len(issues),'terrain_occlusions':len(terrain_occlusion),'export_warnings':sum(len(r['warnings']) for r in export_results)},indent=2))
