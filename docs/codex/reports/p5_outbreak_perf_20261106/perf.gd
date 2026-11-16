extends "res://check.gd"
var hand_back: Node2D
var hand_front: Node2D
var rows: Array = []
func percentile(values: Array, f: float) -> float:
	values.sort()
	return values[mini(values.size()-1,int(values.size()*f))]
func measure(label: String):
	for i in 24:
		await RenderingServer.frame_post_draw
	var gpu: Array = []
	var cpu: Array = []
	var elapsed: Array = []
	var last := Time.get_ticks_usec()
	for i in 72:
		await RenderingServer.frame_post_draw
		var now := Time.get_ticks_usec()
		elapsed.append((now-last)/1000.0)
		last = now
		gpu.append(RenderingServer.viewport_get_measured_render_time_gpu(view.get_viewport_rid()))
		cpu.append(RenderingServer.viewport_get_measured_render_time_cpu(view.get_viewport_rid()))
	var row := {"case":label,"gpu_ms_median":percentile(gpu,.5),"gpu_ms_p95":percentile(gpu,.95),"cpu_ms_median":percentile(cpu,.5),"frame_ms_median":percentile(elapsed,.5),"frame_ms_p95":percentile(elapsed,.95)}
	rows.append(row)
	print(JSON.stringify(row))
func pose(curl_v: float, dissolve_v: float, alpha_v: float):
	set_pose(curl_v)
	for m in [rear,front]:
		m.set_shader_parameter("dissolve",dissolve_v)
		m.set_shader_parameter("alpha",alpha_v)
	if dissolve_v > 0.0:
		for m in [rear,front]:
			m.set_shader_parameter("hand_pos",Vector3(.05,.03,1.75))
			m.set_shader_parameter("elbow",Vector3(-.25,-.57,7.75))
func run():
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	Engine.max_fps = 0
	screen = Vector2(1920,1080)
	view = SubViewport.new()
	view.size = Vector2i(screen)
	view.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(view)
	RenderingServer.viewport_set_measure_render_time(view.get_viewport_rid(),true)
	var bg := ColorRect.new()
	bg.size=screen
	bg.color=Color("202028")
	view.add_child(bg)
	hand_back=Node2D.new()
	hand_front=Node2D.new()
	view.add_child(hand_back)
	view.add_child(hand_front)
	rear=make_hand(hand_back,1.0)
	front=make_hand(hand_front,2.0)
	hand_back.hide();hand_front.hide()
	await measure("empty")
	hand_back.show();hand_front.show()
	for pass_v in [2,1]:
		hand_front.visible = pass_v == 2
		rear.set_shader_parameter("hand_pass",1.0 if pass_v==2 else 0.0)
		pose(1,0,1)
		await measure("fist_" + str(pass_v) + "pass")
		for d in [.25,.5,.75,1.0]:
			pose(1,d,1-d)
			await measure("dissolve"+str(d)+"_"+str(pass_v)+"pass")
	hand_back.hide();hand_front.hide()
	var rings := Node2D.new()
	view.add_child(rings)
	var sh := Shader.new()
	sh.code=FileAccess.get_file_as_string("res://wave.gdshader")
	for i in 5:
		var r := ColorRect.new()
		var size_v := 600+i*500
		r.size=Vector2.ONE*size_v
		r.position=(screen-r.size)*.5
		var mat := ShaderMaterial.new()
		mat.shader=sh
		mat.set_shader_parameter("radius",.8)
		mat.set_shader_parameter("width",.015)
		mat.set_shader_parameter("alpha",.7)
		r.material=mat
		rings.add_child(r)
	await measure("five_rings_only")
	var out:=FileAccess.open("res://hand_perf.json",FileAccess.WRITE)
	out.store_string(JSON.stringify(rows,"  "))
	quit()
