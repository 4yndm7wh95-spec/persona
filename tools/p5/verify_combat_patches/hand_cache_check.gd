extends "res://check.gd"

var cache: SubViewport
var cached: ShaderMaterial
var direct_back: Node2D
var direct_front: Node2D
var cached_back: Node2D
var cached_front: Node2D
var rows: Array = []

func make_cached(owner: Node, back_parent: Node, front_parent: Node, height: int):
	cache = SubViewport.new()
	cache.size = Vector2i(screen * minf(1.0,float(height)/screen.y))
	cache.transparent_bg = true
	cache.disable_3d = true
	cache.use_hdr_2d = true
	cache.render_target_update_mode = SubViewport.UPDATE_DISABLED
	owner.add_child(cache)
	var input := Control.new()
	input.scale = Vector2(cache.size)/screen
	cache.add_child(input)
	cached = make_hand(input,0)
	var packed := Shader.new()
	packed.code = FileAccess.get_file_as_string("res://hand_packed.gdshader")
	cached.shader = packed
	for key in ["noise","lut","aspect","band_z","clock"]:
		cached.set_shader_parameter(key,rear.get_shader_parameter(key))
	var composite := Shader.new()
	composite.code = FileAccess.get_file_as_string("res://hand_composite.gdshader")
	for i in 2:
		var mat := ShaderMaterial.new()
		mat.shader = composite
		mat.set_shader_parameter("frame",cache.get_texture())
		mat.set_shader_parameter("foreground",i==1)
		var rect := ColorRect.new()
		rect.size = screen
		rect.material = mat
		(back_parent if i==0 else front_parent).add_child(rect)
	RenderingServer.viewport_set_measure_render_time(cache.get_viewport_rid(),true)

func mode(use_cache: bool):
	direct_back.visible = not use_cache
	direct_front.visible = not use_cache
	cached_back.visible = use_cache
	cached_front.visible = use_cache
	cache.render_target_update_mode = SubViewport.UPDATE_ALWAYS if use_cache else SubViewport.UPDATE_DISABLED

func pose(k: float, tail: float = -1.0):
	set_pose(k)
	for m in [rear,front]:
		m.set_shader_parameter("dissolve",0.0)
		m.set_shader_parameter("alpha",1.0)
		m.set_shader_parameter("flash",0.0)
	if tail >= 0.0:
		var push := 1.0-pow(1.0-minf(tail*.45/.06,1.0),3)
		var position := Vector3(.05,.03,2.1-.35*push)
		for m in [rear,front]:
			m.set_shader_parameter("hand_pos",position)
			m.set_shader_parameter("elbow",position+Vector3(-.3,-.6,6))
			m.set_shader_parameter("grip",maxf(0,1.4-tail*3))
			m.set_shader_parameter("flash",maxf(0,1-tail*6))
			m.set_shader_parameter("dissolve",1.0-pow(1-tail,3))
			m.set_shader_parameter("alpha",1.0-pow(tail,3))
	for key in ["hand_pos","tilt","roll","curl","thumb","spread","elbow","grip","front_mix","dissolve","alpha","flash"]:
		cached.set_shader_parameter(key,rear.get_shader_parameter(key))

func percentile(a: Array, k: float) -> float:
	a.sort()
	return a[mini(a.size()-1,int(a.size()*k))]

func measure(label: String, use_cache: bool):
	mode(use_cache)
	for i in 24: await RenderingServer.frame_post_draw
	var gpu: Array = []
	var cache_gpu: Array = []
	var main_gpu: Array = []
	var elapsed: Array = []
	var last := Time.get_ticks_usec()
	for i in 72:
		await RenderingServer.frame_post_draw
		var now := Time.get_ticks_usec()
		elapsed.append((now-last)/1000.0)
		last = now
		var main_ms := RenderingServer.viewport_get_measured_render_time_gpu(view.get_viewport_rid())
		var cache_ms := RenderingServer.viewport_get_measured_render_time_gpu(cache.get_viewport_rid()) if use_cache else 0.0
		gpu.append(main_ms+cache_ms)
		cache_gpu.append(cache_ms)
		main_gpu.append(main_ms)
	var row := {"case":label,"cached":use_cache,"gpu_total_ms_median":percentile(gpu,.5),"gpu_total_ms_p95":percentile(gpu,.95),"cache_gpu_ms_median":percentile(cache_gpu,.5),"main_gpu_ms_median":percentile(main_gpu,.5),"frame_ms_median":percentile(elapsed,.5)}
	rows.append(row)
	print(JSON.stringify(row))

func run():
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	Engine.max_fps=0
	screen=Vector2(1920,1080)
	view=SubViewport.new()
	view.size=Vector2i(screen)
	view.render_target_update_mode=SubViewport.UPDATE_ALWAYS
	view.transparent_bg=true
	root.add_child(view)
	RenderingServer.viewport_set_measure_render_time(view.get_viewport_rid(),true)
	var bg:=ColorRect.new()
	bg.size=screen
	bg.color=Color("202028")
	view.add_child(bg)
	direct_back=Node2D.new(); view.add_child(direct_back)
	cached_back=Node2D.new(); view.add_child(cached_back)
	make_paper(view)
	direct_front=Node2D.new(); view.add_child(direct_front)
	cached_front=Node2D.new(); view.add_child(cached_front)
	rear=make_hand(direct_back,1)
	front=make_hand(direct_front,2)
	# A 1:1 comparison isolates packing/color errors from the resolution change.
	make_cached(view,cached_back,cached_front,1080)
	for i in 20: await RenderingServer.frame_post_draw
	for k in [0.0,.45,1.0]:
		pose(k)
		mode(false); await save_frame("direct_pose_"+str(k))
		mode(true); await save_frame("cache_full_pose_"+str(k))
	cache.size=Vector2i(1280,720)
	(cache.get_child(0) as Control).scale=Vector2(cache.size)/screen
	for k in [0.0,.45,1.0]:
		pose(k)
		mode(true); await save_frame("cache_720_pose_"+str(k))
	paper.hide()
	for k in [0.0,.1,.25,.5,.75,.95]:
		pose(1.0,k)
		for use_cache in [false,true]:
			await measure("tail_"+str(k),use_cache)
			await save_frame(("cache_720_tail_" if use_cache else "direct_tail_")+str(k))
	# Transparent gaps remain transparent, not opaque black after decoding.
	bg.hide()
	pose(1.0,.5)
	mode(true); await save_frame("transparent_tail")
	mode(false); await save_frame("transparent_direct")
	var out:=FileAccess.open("res://hand_cache_perf.json",FileAccess.WRITE)
	out.store_string(JSON.stringify(rows,"  "))
	quit()
