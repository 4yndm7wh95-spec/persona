import bpy,json
from pathlib import Path
r=Path(r'C:/Users/kkk/Documents/Codex/2026-10-12/claude-git-c-users-kkk-videos/work/persona-ref-upload');p=r/'research/p5r/summon'
clips=json.loads((p/'source_clips.json').read_text(encoding='utf-8'));m=json.loads((p/'angle_options_frames/camera_options.json').read_text(encoding='utf-8'));chosen=next(c for c in m['options'] if c['offset_degrees']==30)
records=[]
for clip in clips:
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=clip['attached_glb']);sc=bpy.context.scene;sc.render.fps=30
 sc.render.engine='BLENDER_WORKBENCH';s=sc.display.shading;s.light='FLAT';s.color_type='TEXTURE';s.show_shadows=False;s.show_cavity=False;s.show_specular_highlight=False;s.show_object_outline=False
 sc.render.film_transparent=True;sc.render.image_settings.file_format='PNG';sc.render.image_settings.color_mode='RGBA';sc.render.resolution_x=1536;sc.render.resolution_y=1536;sc.render.resolution_percentage=100;sc.view_settings.view_transform='Standard'
 for obj in sc.objects:
  if obj.type=='MESH' and obj.name=='Icosphere':obj.hide_render=True
 cd=bpy.data.cameras.new('SummonSelectedCamera');cam=bpy.data.objects.new('SummonSelectedCamera',cd);sc.collection.objects.link(cam);sc.camera=cam;cd.type='ORTHO';cd.ortho_scale=m['ortho_scale'];cd.clip_start=.01;cd.clip_end=100000;cam.location=chosen['camera_location'];cam.rotation_euler=chosen['camera_rotation']
 folder=p/f"{clip['number']}_frames";folder.mkdir(exist_ok=True);frames=[]
 for f in clip['frames']:
  sc.frame_set(f);bpy.context.view_layer.update();path=folder/f'frame_{f:03}.png';sc.render.filepath=str(path);bpy.ops.render.render(write_still=True)
  hands={n:list(bpy.data.objects[n].matrix_world.translation) for n in ['h_Motion_hand_R','h_Motion_hand_L'] if n in bpy.data.objects}
  frames.append({'frame':f,'time_seconds':f/30,'path':path.relative_to(r).as_posix(),'hands_world':hands})
 records.append({'number':clip['number'],'original_name':clip['original_name'],'variant':'D','duration_seconds':clip['duration_exact_seconds'],'frames':frames})
 data={'camera_source':'research/p5r/summon/angle_options_frames/camera_options.json','selected_azimuth_degrees':chosen['azimuth_degrees'],'selected_offset_degrees':30,'camera_location':chosen['camera_location'],'camera_rotation':chosen['camera_rotation'],'ortho_scale':m['ortho_scale'],'camera_target':m['camera_target'],'camera_direction':chosen['direction'],'camera_fixed_for_all_clips':True,'output_horizontal_mirror':True,'horizontal_mirror_applied':False,'model':'C:/Users/kkk/sts2_tools/p5r_rip/model/joker/fixed2/joker_thief_nomask.glb','weapon':None,'resolution':[1536,1536],'fps_reference':30,'frame_step':2,'clips':records}
 (p/'render_records.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8');print('SELECTED_SUMMON_CLIP_DONE',clip['number'],flush=True)
