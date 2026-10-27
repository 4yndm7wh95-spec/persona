import bpy,json,math
from pathlib import Path
from mathutils import Vector,Matrix
r=Path(r'C:/Users/kkk/Documents/Codex/2026-10-12/claude-git-c-users-kkk-videos/work/persona-ref-upload')
p=r/'research/p5r/summon';out=p/'angle_options_frames';out.mkdir(exist_ok=True)
clips=json.loads((p/'source_clips.json').read_text(encoding='utf-8'))
probe=json.loads(Path('work/summon_camera_probe.json').read_text(encoding='utf-8'));center=probe['center_azimuth_degrees'];offsets=[-30,-15,0,15,30]
W,H=1536,1536
def setup(path):
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=path);sc=bpy.context.scene;sc.render.fps=30
 sc.render.engine='BLENDER_WORKBENCH';s=sc.display.shading;s.light='FLAT';s.color_type='TEXTURE';s.show_shadows=False;s.show_cavity=False;s.show_specular_highlight=False;s.show_object_outline=False
 sc.render.film_transparent=True;sc.render.image_settings.file_format='PNG';sc.render.image_settings.color_mode='RGBA';sc.render.resolution_x=W;sc.render.resolution_y=H;sc.render.resolution_percentage=100;sc.view_settings.view_transform='Standard'
 meshes=[]
 for o in sc.objects:
  if o.type=='MESH':
   if o.name=='Icosphere':o.hide_render=True
   else:meshes.append(o)
 return sc,meshes
points=[];sampling=[]
for clip in clips:
 sc,meshes=setup(clip['attached_glb']);frames=list(range(int(clip['frames_at_30fps_reference'])))
 for f in frames:
  sc.frame_set(f);bpy.context.view_layer.update();deps=bpy.context.evaluated_depsgraph_get()
  for o in meshes:
   ev=o.evaluated_get(deps);points.extend(ev.matrix_world@Vector(v) for v in ev.bound_box)
 sampling.append({'clip':clip['number'],'fit_frames':frames})
lo=Vector(tuple(min(p[i] for p in points) for i in range(3)));hi=Vector(tuple(max(p[i] for p in points) for i in range(3)));target=(lo+hi)/2;distance=(hi-lo).length*3
configs=[];scale=0
for offset in offsets:
 az=(center+offset)%360;direction=Vector((math.cos(math.radians(az)),math.sin(math.radians(az)),0));camloc=target+direction*distance;rot=(target-camloc).to_track_quat('-Z','Y');mat=Matrix.Translation(camloc)@rot.to_matrix().to_4x4();proj=[mat.inverted()@pt for pt in points]
 # One shared target, height, radius and scale across all five candidates; fit ALL frames from all three clips.
 scale=max(scale,2*max(abs(v.y) for v in proj)/.86,2*max(abs(v.x) for v in proj)/(W/H)/.86)
 configs.append({'offset_degrees':offset,'azimuth_degrees':az,'camera_location':list(camloc),'camera_rotation':list(rot.to_euler()),'direction':list(direction),'file':f'angle_{offset:+03d}.png'})
sc,meshes=setup(clips[0]['attached_glb']);sc.frame_set(0);bpy.context.view_layer.update()
cd=bpy.data.cameras.new('SummonAngleCamera');cam=bpy.data.objects.new('SummonAngleCamera',cd);sc.collection.objects.link(cam);sc.camera=cam;cd.type='ORTHO';cd.ortho_scale=scale;cd.clip_start=.01;cd.clip_end=100000
for c in configs:
 cam.location=c['camera_location'];cam.rotation_euler=c['camera_rotation'];sc.render.filepath=str(out/c['file']);bpy.ops.render.render(write_still=True)
metadata={'clip':15,'original_name':'BB0001_051.GAP#main[14]','frame':0,'variant':'D','model':'joker_thief_nomask fixed2','weapon':None,'center_azimuth_degrees':center,'center_method':'Match camera azimuth relative to shoulder-axis azimuth of gun clip52 frame0, preserving screen handedness after common horizontal mirror. Artistic closest-match remains for user selection.','shoulder_probe':probe,'options':configs,'camera_target':list(target),'camera_distance':distance,'elevation_degrees':0,'ortho_scale':scale,'resolution':[W,H],'horizontal_mirror':True,'azimuth_definition':'atan2(worldY,worldX) degrees before horizontal mirror; orbit around vertical world Z','fixed_all_options':True,'fit_padding_fraction':.07,'fit_sampling_all_clips':sampling,'world_union_min':list(lo),'world_union_max':list(hi)}
(out/'camera_options.json').write_text(json.dumps(metadata,indent=2),encoding='utf-8');print('SUMMON_ANGLE_OPTIONS_READY',center,scale,flush=True)
