import bpy,json,math
from pathlib import Path
from mathutils import Vector
lib=Path(r'C:/Users/kkk/sts2_tools/p5r_rip');repo=Path(r'C:/Users/kkk/Documents/Codex/2026-10-12/claude-git-c-users-kkk-videos/work/persona-ref-upload');out=repo/'research/p5r/gun/angle_options_frames';out.mkdir(exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(lib/'model/gun_ref/clip_52_D.glb'));sc=bpy.context.scene;sc.render.fps=30;sc.frame_set(0);bpy.context.view_layer.update()
before=set(sc.objects);bpy.ops.import_scene.gltf(filepath=str(lib/'model/gun_ref/gun0001_fixed.glb'));gun=[o for o in sc.objects if o not in before];roots=[o for o in gun if o.parent not in gun]
mount=bpy.data.objects['h_Motion_hand_R'].matrix_world
for o in roots:o.matrix_world=mount@o.matrix_world
bpy.context.view_layer.update()
sc.render.engine='BLENDER_WORKBENCH';s=sc.display.shading;s.light='FLAT';s.color_type='TEXTURE';s.show_shadows=False;s.show_cavity=False;s.show_specular_highlight=False;s.show_object_outline=False
sc.render.film_transparent=True;sc.render.image_settings.file_format='PNG';sc.render.image_settings.color_mode='RGBA';sc.render.resolution_x=1024;sc.render.resolution_y=1536;sc.render.resolution_percentage=100;sc.view_settings.view_transform='Standard'
meshes=[o for o in sc.objects if o.type=='MESH' and o.name!='Icosphere']
for o in sc.objects:
 if o.type=='MESH' and o not in meshes:o.hide_render=True
deps=bpy.context.evaluated_depsgraph_get();pts=[]
for o in meshes:
 ev=o.evaluated_get(deps);pts.extend(ev.matrix_world@Vector(c) for c in ev.bound_box)
lo=Vector(tuple(min(v[i] for v in pts) for i in range(3)));hi=Vector(tuple(max(v[i] for v in pts) for i in range(3)));target=(lo+hi)/2;distance=(hi-lo).length*3
cd=bpy.data.cameras.new('AngleOptionsCamera');cam=bpy.data.objects.new('AngleOptionsCamera',cd);sc.collection.objects.link(cam);sc.camera=cam;cd.type='ORTHO';cd.clip_start=.01;cd.clip_end=100000
# Azimuth is atan2(worldY,worldX), same elevation, target and radius throughout.
start=math.atan2(1,.8);end=0.;options=[0,20,35,50,65,100];configs=[];scale=0
for percent in options:
 az=start+(end-start)*percent/100;direction=Vector((math.cos(az),math.sin(az),0));cam.location=target+direction*distance;cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();bpy.context.view_layer.update();cp=[cam.matrix_world.inverted()@v for v in pts]
 # Keep the SAME target rather than separately recentering each view. Fit all options once.
 scale=max(scale,2*max(abs(v.y) for v in cp)/.88,2*max(abs(v.x) for v in cp)/(1024/1536)/.88)
 configs.append({'percent':percent,'azimuth_degrees':math.degrees(az),'rotation_from_original_degrees':math.degrees(az-start),'camera_location':list(cam.location),'camera_rotation':list(cam.rotation_euler),'file':f'angle_{percent:03}.png'})
cd.ortho_scale=scale
for cfg in configs:
 cam.location=cfg['camera_location'];cam.rotation_euler=cfg['camera_rotation'];sc.render.filepath=str(out/cfg['file']);bpy.ops.render.render(write_still=True)
metadata={'clip':52,'original_name':'BB0001_051.GAP#main[54]','frame':0,'fps_reference':30,'variant':'D','weapon':'MODEL/WEAPON/GUN0001.GMD','mount':'h_Motion_hand_R','target':list(target),'camera_distance':distance,'ortho_scale':scale,'elevation_degrees':0,'azimuth_definition':'atan2(worldY,worldX), before common output horizontal mirror','horizontal_mirror':True,'interpolation':'linear azimuth / orbit around world Z','options':configs}
(out/'camera_options.json').write_text(json.dumps(metadata,indent=2),encoding='utf-8')
print('ANGLE_OPTIONS_READY',flush=True)
