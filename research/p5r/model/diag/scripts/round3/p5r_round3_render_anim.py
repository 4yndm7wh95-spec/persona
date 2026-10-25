import bpy,sys,json,math,struct
from pathlib import Path
from mathutils import Vector
REPO=Path(r'C:\Users\kkk\Documents\Codex\2026-10-12\claude-git-c-users-kkk-videos\work\persona-ref-upload')
LOCAL=Path(r'C:\Users\kkk\sts2_tools\p5r_rip\model\round3_work');LOCAL.mkdir(exist_ok=True)
OUT=REPO/'research/p5r/model/diag';OUT.mkdir(exist_ok=True)
mode=sys.argv[sys.argv.index('--')+1]
def setup(path):
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(path))
    blob=path.read_bytes();j=json.loads(blob[20:20+struct.unpack_from('<I',blob,12)[0]])
    source_names={n['name'] for n in j['nodes'] if 'mesh' in n}
    for obj in bpy.context.scene.objects:
        if obj.type=='MESH':
            obj['__diag_source_mesh']=obj.name in source_names
            if obj.name not in source_names:obj.hide_render=True
    sc=bpy.context.scene;sc.render.engine='BLENDER_WORKBENCH';sc.display.shading.light='FLAT';sc.display.shading.color_type='TEXTURE'
    sc.display.shading.show_shadows=False;sc.display.shading.show_cavity=False;sc.display.shading.show_specular_highlight=False
    sc.render.film_transparent=True;sc.render.image_settings.file_format='PNG';sc.render.image_settings.color_mode='RGBA'
    sc.render.resolution_x=1024;sc.render.resolution_y=1536;sc.render.resolution_percentage=100;sc.view_settings.view_transform='Standard';sc.render.fps=30
    camdata=bpy.data.cameras.new('DiagnosticCamera');cam=bpy.data.objects.new('DiagnosticCamera',camdata);sc.collection.objects.link(cam);sc.camera=cam;camdata.type='ORTHO';camdata.clip_start=0.01;camdata.clip_end=100000
    return sc,cam
def fit(cam):
    bpy.context.view_layer.update();objs=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.get('__diag_source_mesh')];pts=[o.matrix_world@Vector(c) for o in objs for c in o.bound_box]
    lo=Vector(tuple(min(p[i] for p in pts) for i in range(3)));hi=Vector(tuple(max(p[i] for p in pts) for i in range(3)));center=(lo+hi)/2;h=hi.z-lo.z
    cam.location=center+Vector((-0.8,-1,0)).normalized()*h*3;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();bpy.context.view_layer.update()
    cp=[cam.matrix_world.inverted()@p for p in pts];xmin=min(p.x for p in cp);xmax=max(p.x for p in cp);ymin=min(p.y for p in cp);ymax=max(p.y for p in cp)
    scale=max((ymax-ymin)/0.90,(xmax-xmin)/(1024/1536)/0.92);cam.data.ortho_scale=scale
    cam.location+=cam.rotation_euler.to_matrix()@Vector(((xmin+xmax)/2,ymin+scale/2-scale*60/1536,0))
    return {'location':list(cam.location),'rotation_euler':list(cam.rotation_euler),'ortho_scale':scale,'bbox_min':list(lo),'bbox_max':list(hi)}
def applycam(cam,cfg):
    cam.location=cfg['location'];cam.rotation_euler=cfg['rotation_euler'];cam.data.ortho_scale=cfg['ortho_scale']
def render(sc,path):
    path.parent.mkdir(exist_ok=True,parents=True);sc.render.filepath=str(path);bpy.ops.render.render(write_still=True)
if mode=='meshes':
    result={}
    for label,stem in [('thief','joker_thief_nomask'),('student','joker_student')]:
        path=REPO/'research/p5r/model/joker/fixed'/f'{stem}.glb';sc,cam=setup(path);cfg=fit(cam);(LOCAL/f'camera_{label}.json').write_text(json.dumps(cfg),encoding='utf-8')
        meshobjs=[o for o in sc.objects if o.type=='MESH' and o.get('__diag_source_mesh')];rows=[]
        for i,o in enumerate(meshobjs):
            for other in meshobjs:other.hide_render=other!=o
            name=f'{label}_mesh_{i:02}.png';render(sc,LOCAL/name)
            row={'index':i,'object_name':o.name,'mesh_name':o.data.name,'materials':[m.name for m in o.data.materials if m],'vertex_count':len(o.data.vertices),'render':name,'world_bbox':[[float(v) for v in o.matrix_world@Vector(c)] for c in o.bound_box]}
            rows.append(row);print('MESH_DONE',label,i,o.name,flush=True)
        result[label]=rows
    (LOCAL/'mesh_renders.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
elif mode=='animation':
    sc,cam=setup(Path('C:/Users/kkk/sts2_tools/p5r_rip/model/joker/fixed2/joker_thief_nomask.glb'));cfg=fit(cam);(LOCAL/'camera_fixed2.json').write_text(json.dumps(cfg));frames=[0,8,16,24,32,40];summary={}
    for variant in ['A','C','D']:
        sc,cam=setup(LOCAL/f'anim_{variant}.glb');applycam(cam,cfg);sc.frame_start=0;sc.frame_end=40
        print('ANIMATION_STATE',variant,'objects with animation',[(o.name,o.animation_data.action.name if o.animation_data.action else None,len(o.animation_data.nla_tracks)) for o in sc.objects if o.animation_data],flush=True)
        summary[variant]=[]
        for frame in frames:
            sc.frame_set(frame);bpy.context.view_layer.update();render(sc,LOCAL/f'anim_{variant}_{frame:02}.png')
            deps=bpy.context.evaluated_depsgraph_get();pts=[]
            for o in sc.objects:
                if o.type=='MESH' and o.get('__diag_source_mesh'):
                    ev=o.evaluated_get(deps);pts.extend(ev.matrix_world@Vector(c) for c in ev.bound_box)
            summary[variant].append({'frame_at_30fps':frame,'time_seconds':frame/30,'bbox_min':[min(p[i] for p in pts) for i in range(3)],'bbox_max':[max(p[i] for p in pts) for i in range(3)]})
        print('ANIM_DONE',variant,flush=True)
    (LOCAL/'animation_render_info.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
