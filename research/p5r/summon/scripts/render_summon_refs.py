import bpy,json,math
from pathlib import Path
from mathutils import Vector
repo=Path(r'C:/Users/kkk/Documents/Codex/2026-10-12/claude-git-c-users-kkk-videos/work/persona-ref-upload')
lib=Path(r'C:/Users/kkk/sts2_tools/p5r_rip');out=repo/'research/p5r/summon'
clips=json.loads((lib/'model/summon_ref/summon_clips.json').read_text(encoding='utf-8'))
camdata=json.loads((repo/'research/p5r/gun/pick/render_records.json').read_text(encoding='utf-8'))
records=[];allbounds=[]
for clip in clips:
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=clip['attached_glb'])
 sc=bpy.context.scene;sc.render.fps=30;sc.frame_set(0);bpy.context.view_layer.update()
 sc.render.engine='BLENDER_WORKBENCH';s=sc.display.shading;s.light='FLAT';s.color_type='TEXTURE';s.show_shadows=False;s.show_cavity=False;s.show_specular_highlight=False;s.show_object_outline=False
 sc.render.film_transparent=True;sc.render.image_settings.file_format='PNG';sc.render.image_settings.color_mode='RGBA';sc.render.resolution_x=1024;sc.render.resolution_y=1536;sc.render.resolution_percentage=100;sc.view_settings.view_transform='Standard'
 for obj in sc.objects:
  if obj.type=='MESH' and obj.name=='Icosphere':obj.hide_render=True
 cd=bpy.data.cameras.new('SummonCamera');cam=bpy.data.objects.new('SummonCamera',cd);sc.collection.objects.link(cam);sc.camera=cam
 cd.type='ORTHO';cd.ortho_scale=camdata['ortho_scale'];cd.clip_start=.01;cd.clip_end=100000
 cam.location=camdata['camera_location'];cam.rotation_euler=camdata['camera_rotation']
 folder=out/f"{clip['number']}_frames";folder.mkdir(exist_ok=True);fs=[]
 for f in clip['frames']:
  sc.frame_set(f);bpy.context.view_layer.update();path=folder/f'frame_{f:03}.png';sc.render.filepath=str(path);bpy.ops.render.render(write_still=True)
  hands={n:list(bpy.data.objects[n].matrix_world.translation) for n in ['h_Motion_hand_R','h_Motion_hand_L'] if n in bpy.data.objects}
  fs.append({'frame':f,'time_seconds':f/30,'path':path.relative_to(repo).as_posix(),'hands_world':hands})
 records.append({'number':clip['number'],'original_name':clip['original_name'],'variant':'D','duration_seconds':clip['duration_exact_seconds'],'frames':fs})
 data={k:camdata[k] for k in ['camera_location','camera_rotation','ortho_scale','camera_direction','selected_percent','azimuth_degrees','output_horizontal_mirror']}
 data.update({'camera_source':'research/p5r/gun/pick/render_records.json','camera_fixed_for_all_clips':True,'model':str(lib/'model/joker/fixed2/joker_thief_nomask.glb'),'weapon':None,'resolution':[1024,1536],'fps_reference':30,'frame_step':2,'clips':records})
 (out/'render_records.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
 print('SUMMON_CLIP_DONE',clip['number'],flush=True)
