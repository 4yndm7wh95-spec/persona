import bpy,json,math
from pathlib import Path
from mathutils import Vector,Matrix
lib=Path(r'C:/Users/kkk/sts2_tools/p5r_rip');repo=Path(r'C:/Users/kkk/Documents/Codex/2026-10-12/claude-git-c-users-kkk-videos/work/persona-ref-upload');out=repo/'research/p5r/gun';out.mkdir(parents=True,exist_ok=True)
clips=json.loads((lib/'model/gun_ref/gun_clips.json').read_text(encoding='utf-8'));allpts=[];scene_data=[];records=[]
def setup(clip):
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=clip['attached_glb']);sc=bpy.context.scene;sc.render.fps=30;sc.frame_set(0);bpy.context.view_layer.update()
 before=set(sc.objects);bpy.ops.import_scene.gltf(filepath=str(lib/'model/gun_ref/gun0001_fixed.glb'));gun=[o for o in sc.objects if o not in before];roots=[o for o in gun if o.parent not in gun];local={o.name:o.matrix_world.copy() for o in roots}
 sc.render.engine='BLENDER_WORKBENCH';s=sc.display.shading;s.light='FLAT';s.color_type='TEXTURE';s.show_shadows=False;s.show_cavity=False;s.show_specular_highlight=False;s.show_object_outline=False
 sc.render.film_transparent=True;sc.render.image_settings.file_format='PNG';sc.render.image_settings.color_mode='RGBA';sc.render.resolution_x=1024;sc.render.resolution_y=1536;sc.render.resolution_percentage=100;sc.view_settings.view_transform='Standard'
 meshes=[o for o in sc.objects if o.type=='MESH' and o.name!='Icosphere']
 for o in sc.objects:
  if o.type=='MESH' and o not in meshes:o.hide_render=True
 return sc,meshes,roots,local
def frame(sc,f,roots,local):
 sc.frame_set(f);bpy.context.view_layer.update();mount=bpy.data.objects['h_Motion_hand_R'].matrix_world
 for o in roots:o.matrix_world=mount@local[o.name]
 bpy.context.view_layer.update();deps=bpy.context.evaluated_depsgraph_get()
 return deps
for clip in clips:
 sc,meshes,roots,local=setup(clip)
 for f in clip['frames']:
  deps=frame(sc,f,roots,local)
  for o in meshes:
   ev=o.evaluated_get(deps);allpts.extend(ev.matrix_world@Vector(c) for c in ev.bound_box)
# One shared camera position/scale across ALL four clips. Mirrored output is documented in README.
lo=Vector(tuple(min(p[i] for p in allpts) for i in range(3)));hi=Vector(tuple(max(p[i] for p in allpts) for i in range(3)));center=(lo+hi)/2
direction=Vector((.8,1,0)).normalized();camloc=center+direction*(hi-lo).length*3;rot=(center-camloc).to_track_quat('-Z','Y');mat=Matrix.Translation(camloc)@rot.to_matrix().to_4x4();cp=[mat.inverted()@p for p in allpts]
xmin,xmax=min(p.x for p in cp),max(p.x for p in cp);ymin,ymax=min(p.y for p in cp),max(p.y for p in cp);scale=max((ymax-ymin)/.88,(xmax-xmin)/(1024/1536)/.88);camloc+=rot.to_matrix()@Vector(((xmin+xmax)/2,(ymin+ymax)/2,0))
for clip in clips:
 sc,meshes,roots,local=setup(clip);cd=bpy.data.cameras.new('GunCamera');cam=bpy.data.objects.new('GunCamera',cd);sc.collection.objects.link(cam);sc.camera=cam;cd.type='ORTHO';cd.ortho_scale=scale;cd.clip_start=.01;cd.clip_end=100000;cam.location=camloc;cam.rotation_euler=rot.to_euler()
 folder=out/f"{clip['number']}_frames";folder.mkdir(exist_ok=True);fs=[]
 for f in clip['frames']:
  deps=frame(sc,f,roots,local);path=folder/f'frame_{f:03}.png';sc.render.filepath=str(path);bpy.ops.render.render(write_still=True)
  hand=bpy.data.objects['h_Motion_hand_R'].matrix_world.translation;upper=bpy.data.objects.get('Bip01_R_UpperArm')
  fs.append({'frame':f,'time_seconds':f/30,'path':str(path),'hand_world':list(hand)})
 records.append({'number':clip['number'],'original_name':clip['original_name'],'variant':'D','frames':fs})
 (out/'render_records.json').write_text(json.dumps({'camera_location':list(camloc),'camera_rotation':list(rot.to_euler()),'ortho_scale':scale,'camera_direction':[.8,1,0],'output_horizontal_mirror':True,'weapon':'MODEL/WEAPON/GUN0001.GMD','attachment':'h_Motion_hand_R','mount_local_adjustment':'identity in Blender imported gun coordinates; native GMD Y-up is imported as Z-up','clips':records},indent=2),encoding='utf-8')
 print('GUN_CLIP_DONE',clip['number'],flush=True)
