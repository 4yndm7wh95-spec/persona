import bpy,sys,math,json
from pathlib import Path
from mathutils import Vector
args=sys.argv[sys.argv.index('--')+1:]
model=Path(args[0]).resolve(); out=Path(args[1]).resolve(); view=args[2] if len(args)>2 else 'threequarter'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(model))
scene=bpy.context.scene
scene.render.engine='BLENDER_WORKBENCH'
scene.display.shading.light='FLAT'
scene.display.shading.studiolight_rotate_z=0.3
scene.display.shading.color_type='TEXTURE'
scene.display.shading.show_shadows=False
scene.display.shading.show_cavity=False
scene.display.shading.show_specular_highlight=False
scene.render.film_transparent=True
scene.render.image_settings.file_format='PNG'
scene.render.image_settings.color_mode='RGBA'
scene.render.resolution_x=1024;scene.render.resolution_y=1536;scene.render.resolution_percentage=100
scene.view_settings.view_transform='Standard'
meshes=[o for o in scene.objects if o.type=='MESH']
bpy.context.view_layer.update()
pts=[o.matrix_world@Vector(c) for o in meshes for c in o.bound_box]
lo=Vector(tuple(min(p[i] for p in pts) for i in range(3))); hi=Vector(tuple(max(p[i] for p in pts) for i in range(3)))
center=(lo+hi)/2; height=hi.z-lo.z
camdata=bpy.data.cameras.new('PreviewCamera');cam=bpy.data.objects.new('PreviewCamera',camdata);scene.collection.objects.link(cam);scene.camera=cam
camdata.clip_end=max(1000,height*30);camdata.clip_start=0.01;camdata.type='ORTHO';direction=Vector((0,-1,0)) if view=='front' else Vector((-0.8,-1,0))
cam.location=center+direction.normalized()*height*3;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
bpy.context.view_layer.update()
invcam=cam.matrix_world.inverted();cp=[invcam@p for p in pts]
xmin=min(p.x for p in cp);xmax=max(p.x for p in cp);ymin=min(p.y for p in cp);ymax=max(p.y for p in cp)
scale=max((ymax-ymin)/0.90,(xmax-xmin)/(1024/1536)/0.92);camdata.ortho_scale=scale
# Bottom 60px, with remaining headroom above.
local_shift=Vector(((xmin+xmax)/2, ymin+scale/2-scale*60/1536,0));cam.location+=cam.rotation_euler.to_matrix()@local_shift
out.parent.mkdir(parents=True,exist_ok=True);scene.render.filepath=str(out)
print('BOUNDS',model.name,list(lo),list(hi),flush=True)
bpy.ops.render.render(write_still=True)
