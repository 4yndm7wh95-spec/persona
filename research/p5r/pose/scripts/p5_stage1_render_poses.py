import bpy,json,math
from pathlib import Path
from mathutils import Vector
LOCAL=Path(r'C:\Users\kkk\sts2_tools\p5r_rip\model\pose_stage1');clips=json.loads((LOCAL/'clips.json').read_text());records=[]
for r in clips:
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=r['attached_glb']);sc=bpy.context.scene
    sc.render.engine='BLENDER_WORKBENCH';sc.display.shading.light='FLAT';sc.display.shading.color_type='TEXTURE';sc.display.shading.show_shadows=False;sc.display.shading.show_cavity=False;sc.display.shading.show_specular_highlight=False;sc.display.shading.show_object_outline=False
    sc.render.film_transparent=True;sc.render.image_settings.file_format='PNG';sc.render.image_settings.color_mode='RGBA';sc.render.resolution_x=256;sc.render.resolution_y=384;sc.render.resolution_percentage=100;sc.render.fps=30;sc.view_settings.view_transform='Standard'
    meshes=[o for o in sc.objects if o.type=='MESH' and o.name!='Icosphere']
    for o in sc.objects:
        if o.type=='MESH' and o not in meshes:o.hide_render=True
    cd=bpy.data.cameras.new('PoseCamera');cam=bpy.data.objects.new('PoseCamera',cd);sc.collection.objects.link(cam);sc.camera=cam;cd.type='ORTHO';cd.clip_start=0.01;cd.clip_end=100000
    duration=float(r['duration_seconds']);frames=[]
    for k in range(6):
        t=duration*k/5;frame=t*30;sc.frame_set(int(math.floor(frame)),subframe=frame-math.floor(frame));bpy.context.view_layer.update();deps=bpy.context.evaluated_depsgraph_get()
        points=[]
        for obj in meshes:
            ev=obj.evaluated_get(deps);points.extend(ev.matrix_world@Vector(c) for c in ev.bound_box)
        lo=Vector(tuple(min(p[i] for p in points) for i in range(3)));hi=Vector(tuple(max(p[i] for p in points) for i in range(3)));center=(lo+hi)/2;dist=max((hi-lo).length,1)*3
        cam.location=center+Vector((-0.8,-1,0)).normalized()*dist;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();bpy.context.view_layer.update()
        cp=[cam.matrix_world.inverted()@p for p in points];xmin=min(p.x for p in cp);xmax=max(p.x for p in cp);ymin=min(p.y for p in cp);ymax=max(p.y for p in cp)
        scale=max((ymax-ymin)/0.9,(xmax-xmin)/(256/384)/0.9);cd.ortho_scale=scale
        cam.location+=cam.rotation_euler.to_matrix()@Vector(((xmin+xmax)/2,(ymin+ymax)/2,0))
        imagepath=LOCAL/f'pose_{r["number"]:02}_{k}.png';sc.render.filepath=str(imagepath);bpy.ops.render.render(write_still=True)
        frames.append({'sample':k,'time_seconds':t,'frame_at_30fps':frame,'path':str(imagepath),'bbox_min':list(lo),'bbox_max':list(hi),'camera_location':list(cam.location),'camera_ortho_scale':scale})
    records.append({'number':r['number'],'file':r['file'],'frames':frames});(LOCAL/'render_records.json').write_text(json.dumps(records,indent=2),encoding='utf-8');print('POSE_CLIP_DONE',r['number'],r['original_name'],flush=True)
print('POSE_RENDER_FINISHED',len(records),flush=True)
