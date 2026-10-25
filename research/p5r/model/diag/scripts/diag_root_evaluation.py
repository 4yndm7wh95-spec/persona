import bpy,json
from pathlib import Path
base=Path(r'C:\Users\kkk\sts2_tools\p5r_rip\model\diag_work');out={}
for variant in ['original','skip_root_rotation']:
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(base/f'anim_{variant}.glb'));bpy.context.scene.frame_set(0);bpy.context.view_layer.update()
 r=bpy.data.objects.get('root');ad=r.animation_data
 out[variant]={'root_type':r.type,'root_rotation_mode':r.rotation_mode,'root_rotation_quaternion':list(r.rotation_quaternion),'root_matrix_world':[list(row) for row in r.matrix_world],'action':ad.action.name if ad and ad.action else None,'nla_tracks':[(t.name,t.mute) for t in ad.nla_tracks] if ad else [],'actions':[(a.name,len(a.slots)) for a in bpy.data.actions]}
 print('ROOT_EVALUATED',variant,out[variant],flush=True)
(base/'root_evaluation.json').write_text(json.dumps(out,indent=2))
