extends SceneTree
var vp:SubViewport
var cam:Camera3D
var probes=[Vector3(-.025,-.02,-.5),Vector3(.01,.035,-.8),Vector3(.055,-.045,-.65)]
var checks=0
var worst=0.0
var legacy_worst=0.0
func _initialize():
	call_deferred("run")
func run():
	vp=SubViewport.new();vp.own_world_3d=true;vp.render_target_update_mode=SubViewport.UPDATE_ALWAYS
	root.add_child(vp)
	cam=Camera3D.new();cam.keep_aspect=Camera3D.KEEP_WIDTH;cam.current=true;cam.projection=Camera3D.PROJECTION_FRUSTUM
	cam.size=2*cam.near*tan(deg_to_rad(37.35)/2)
	vp.add_child(cam)
	var env=WorldEnvironment.new();env.environment=Environment.new();env.environment.background_mode=Environment.BG_COLOR;env.environment.background_color=Color.BLACK
	vp.add_child(env)
	for i in probes.size():
		var mesh=MeshInstance3D.new();var q=QuadMesh.new();q.size=Vector2(.003,.003);mesh.mesh=q
		var m=StandardMaterial3D.new();m.shading_mode=BaseMaterial3D.SHADING_MODE_UNSHADED;m.albedo_color=[Color.RED,Color.GREEN,Color.BLUE][i]
		mesh.material_override=m;mesh.position=probes[i];vp.add_child(mesh)
	var results=[]
	for display in [Vector2(1920,1080),Vector2(1280,720),Vector2(1920,1200),Vector2(2560,1080),Vector2(1600,1200)]:
		var scale=min(1.0,1920.0/display.x)
		vp.size=Vector2i(display*scale)
		for phase in [0.0,.15,.35,.5,.7,.9,1.0]:
			var pan=(Vector2(160,0)+Vector2(-90,-110)*phase)*display.x/1920.0
			cam.frustum_offset=Vector2(-pan.x/display.x*cam.size,pan.y/display.x*cam.size)
			await process_frame
			await process_frame
			await RenderingServer.frame_post_draw
			var im=vp.get_texture().get_image()
			var projection=Projection.create_frustum_aspect(cam.size,float(vp.size.x)/vp.size.y,cam.frustum_offset,cam.near,cam.far,true)
			for i in probes.size():
				var p=probes[i];var clip=projection*Vector4(p.x,p.y,p.z,1)
				var expected=Vector2(clip.x/clip.w*.5+.5,-clip.y/clip.w*.5+.5)*Vector2(vp.size)
				var legacy=cam.unproject_position(p)
				legacy_worst=max(legacy_worst,expected.distance_to(legacy))
				var sum=Vector2.ZERO;var count=0
				for y in range(int(expected.y)-14,int(expected.y)+15):
					for x in range(int(expected.x)-14,int(expected.x)+15):
						if x<0 or y<0 or x>=im.get_width() or y>=im.get_height():continue
						var c=im.get_pixel(x,y)
						if c[i]>.8 and c[(i+1)%3]<.1 and c[(i+2)%3]<.1:
							sum+=Vector2(x+.5,y+.5);count+=1
				if count==0:
					push_error("No GPU probe at "+str(expected));quit(1);return
				var actual=sum/count;var error=actual.distance_to(expected);worst=max(worst,error)
				if error>1:
					push_error("GPU mismatch: "+str([display,phase,i,expected,actual,error]));quit(1);return
				checks+=1
			results.append({"display":str(display),"render":str(vp.size),"entrance":phase})
	var report={"pass":true,"checks":checks,"max_gpu_error_px":worst,"legacy_max_error_px":legacy_worst,"cases":results}
	FileAccess.open(OS.get_cmdline_user_args()[0],FileAccess.WRITE).store_string(JSON.stringify(report,"\t"))
	print("PASS ",checks," GPU anchors; max error ",worst," px; legacy max ",legacy_worst," px")
	quit()
