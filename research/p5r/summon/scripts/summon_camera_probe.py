import bpy,json,math
from pathlib import Path
from mathutils import Vector
lib=Path(r'C:/Users/kkk/sts2_tools/p5r_rip')
data={}
for label,path in [('gun',lib/'model/gun_ref/clip_52_D.glb'),('summon',lib/'model/summon_ref/clip_15_D.glb')]:
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(path));bpy.context.scene.render.fps=30;bpy.context.scene.frame_set(0);bpy.context.view_layer.update()
 def norm(n):return ''.join(c for c in n.lower() if c.isalnum())
 candidates={norm(o.name):o.matrix_world.translation.copy() for o in bpy.data.objects}
 for o in bpy.data.objects:
  if o.type=='ARMATURE':
   for b in o.pose.bones:candidates[norm(b.name)]=(o.matrix_world@b.matrix).translation
 pos={n:list(v) for n,v in candidates.items() if 'upperarm' in n or 'spine' in n or 'head' in n}
 print('BONE_PROBE',label,json.dumps(pos),flush=True)
 right=candidates['bip01rupperarm'];left=candidates['bip01lupperarm']
 v=right-left;data[label]={'positions':pos,'shoulder_axis_degrees':math.degrees(math.atan2(v.y,v.x))}
center=(25.670095872954956+data['summon']['shoulder_axis_degrees']-data['gun']['shoulder_axis_degrees'])%360
data['center_azimuth_degrees']=center
Path('work/summon_camera_probe.json').write_text(json.dumps(data,indent=2),encoding='utf-8');print('CAMERA_PROBE',json.dumps(data),flush=True)
