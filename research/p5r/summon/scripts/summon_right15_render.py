import bpy,json,math
from pathlib import Path
from mathutils import Vector
r=Path(r'C:/Users/kkk/Documents/Codex/2026-10-12/claude-git-c-users-kkk-videos/work/persona-ref-upload');p=r/'research/p5r/summon';out=p/'right15_review';out.mkdir(exist_ok=True)
clips=json.loads((p/'source_clips.json').read_text(encoding='utf-8'));m=json.loads((p/'angle_options_frames/camera_options.json').read_text(encoding='utf-8'));base=next(c for c in m['options'] if c['offset_degrees']==30);records=[]
def norm(n):return ''.join(c for c in n.lower() if c.isalnum())
for name,num,frame in [('key_rip_open',16,14),('key_stance',17,0)]:
 clip=next(c for c in clips if c['number']==num);bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=clip['attached_glb']);sc=bpy.context.scene;sc.render.fps=30;sc.frame_set(frame);bpy.context.view_layer.update()
 pos={norm(o.name):o.matrix_world.translation.copy() for o in bpy.data.objects}
 for o in bpy.data.objects:
  if o.type=='ARMATURE':
   for b in o.pose.bones:pos[norm(b.name)]=(o.matrix_world@b.matrix).translation
 right=pos['bip01rupperarm']-pos['bip01lupperarm'];right.z=0;right.normalize()
 choices=[]
 for delta in [-15,15]:
  az=(base['azimuth_degrees']+delta)%360;direction=Vector((math.cos(math.radians(az)),math.sin(math.radians(az)),0));choices.append((direction.dot(right),delta,az,direction))
 score,delta,az,direction=max(choices,key=lambda c:c[0])
 target=Vector(m['camera_target']);loc=target+direction*m['camera_distance'];rot=(target-loc).to_track_quat('-Z','Y').to_euler()
 sc.render.engine='BLENDER_WORKBENCH';s=sc.display.shading;s.light='FLAT';s.color_type='TEXTURE';s.show_shadows=False;s.show_cavity=False;s.show_specular_highlight=False;s.show_object_outline=False
 sc.render.film_transparent=True;sc.render.image_settings.file_format='PNG';sc.render.image_settings.color_mode='RGBA';sc.render.resolution_x=1536;sc.render.resolution_y=1536;sc.render.resolution_percentage=100;sc.view_settings.view_transform='Standard'
 for o in sc.objects:
  if o.type=='MESH' and o.name=='Icosphere':o.hide_render=True
 cd=bpy.data.cameras.new('Right15Camera');cam=bpy.data.objects.new('Right15Camera',cd);sc.collection.objects.link(cam);sc.camera=cam;cd.type='ORTHO';cd.ortho_scale=m['ortho_scale'];cd.clip_start=.01;cd.clip_end=100000;cam.location=loc;cam.rotation_euler=rot
 path=out/(name+'.png');sc.render.filepath=str(path);bpy.ops.render.render(write_still=True)
 records.append({'name':name+'.png','clip':num,'frame':frame,'previous_azimuth_degrees':base['azimuth_degrees'],'delta_degrees':delta,'azimuth_degrees':az,'camera_location':list(loc),'camera_rotation':list(rot),'character_right_axis_world':list(right),'selection_rule':'choose orbit sign that moves camera toward anatomical right shoulder side','ortho_scale':m['ortho_scale'],'camera_target':list(target),'resolution':[1536,1536],'horizontal_mirror':True})
(out/'camera_records.json').write_text(json.dumps(records,indent=2),encoding='utf-8');print('RIGHT15_READY',json.dumps(records),flush=True)
