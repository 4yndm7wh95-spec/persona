extends "res://check.gd"

var legacy := "--legacy" in OS.get_cmdline_user_args()
var anchors: Array[Node2D] = []
var markers: Array[ColorRect] = []
var baseline: Array[Color] = []
var back: Control
var failures := 0

func panel(parent: Node, at: Vector2, extent: Vector2, tint: Color) -> ColorRect:
	var p := ColorRect.new()
	p.position = at
	p.size = extent
	p.color = tint
	parent.add_child(p)
	return p

func anchored(label: String) -> Node2D:
	var n := Node2D.new()
	n.name = label
	n.top_level = legacy
	back.add_child(n)
	anchors.append(n)
	place(n)
	return n

func place(n: Node2D):
	if legacy:
		n.global_transform = back.get_canvas_transform().affine_inverse()
	else:
		n.transform = back.get_global_transform_with_canvas().affine_inverse()

func sample_marker(im: Image, p: ColorRect) -> Color:
	return im.get_pixelv(Vector2i(p.get_global_transform_with_canvas() * (p.size * 0.5)))

func snapshot() -> Image:
	await process_frame
	await RenderingServer.frame_post_draw
	await process_frame
	await RenderingServer.frame_post_draw
	return view.get_texture().get_image()

func check_phase(label: String):
	var im := await snapshot()
	var hidden := 0
	for i in markers.size():
		if sample_marker(im, markers[i]) != baseline[i]:
			hidden += 1
	if hidden > 0:
		failures += 1
	print(label, ": foreground markers altered = ", hidden, "/", markers.size())
	im.save_png("res://" + ("old_" if legacy else "new_") + label + ".png")
	return im

func run():
	view = SubViewport.new()
	view.size = Vector2i(screen)
	view.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(view)
	# A preceding screen effect might have captured an empty viewport. The stage
	# must sample the actual room, not this unrelated earlier copy.
	panel(view,Vector2.ZERO,screen,Color.BLACK)
	var early := BackBufferCopy.new()
	early.copy_mode = BackBufferCopy.COPY_MODE_VIEWPORT
	view.add_child(early)
	panel(view, Vector2(-2000,-2000), Vector2(5000,5000), Color("516379"))
	var room := Control.new()
	view.add_child(room)
	back = Control.new()
	room.add_child(back)
	# Existing actor, enemy, hand of cards and HUD; no artificial high z-index.
	markers.append(panel(room, Vector2(175,225), Vector2(48,76), Color("42e6b3")))
	markers.append(panel(room, Vector2(700,220), Vector2(70,88), Color("e7bb31")))
	markers.append(panel(view, Vector2(375,465), Vector2(120,50), Color("407fa9")))
	markers.append(panel(view, Vector2(300,5), Vector2(290,32), Color("308f74")))
	var original := await snapshot()
	for m in markers:
		baseline.append(sample_marker(original, m))
	var stage_root := anchored("Stage")
	if not legacy:
		var capture := BackBufferCopy.new()
		capture.copy_mode = BackBufferCopy.COPY_MODE_VIEWPORT
		stage_root.add_child(capture)
	var stage := panel(stage_root,Vector2.ZERO,screen,Color.WHITE)
	var shader := Shader.new()
	shader.code = FileAccess.get_file_as_string("res://stage.gdshader")
	var mat := ShaderMaterial.new()
	mat.shader = shader
	stage.material = mat
	mat.set_shader_parameter("res",screen)
	mat.set_shader_parameter("sres",screen)
	mat.set_shader_parameter("intro",0.0)
	var eye := anchored("Paper")
	make_paper(eye)
	var entering: Image = await check_phase("intro")
	var intro_changed := entering.get_pixel(80,480) != original.get_pixel(80,480)
	print("Uncovered intro background (original / actual): ",original.get_pixel(80,480).to_html(), " / ", entering.get_pixel(80,480).to_html())
	if intro_changed:
		failures += 1
	var palm := anchored("Rear")
	back.move_child(palm,eye.get_index())
	rear = make_hand(palm,1)
	front = make_hand(anchored("Fingers"),2)
	set_pose(0.8)
	# Persona is spawned later in the same back container.
	var persona := panel(back,Vector2(110,100),Vector2(60,95),Color("bc83cf"))
	markers.append(persona)
	baseline.append(original.get_pixel(0,0)) # replaced below with an unfiltered reference sample
	for n in anchors: n.hide()
	var clean := await snapshot()
	baseline[-1] = sample_marker(clean,persona)
	for n in anchors: n.show()
	mat.set_shader_parameter("intro",0.45)
	var grappling: Image = await check_phase("grip")
	if grappling.get_pixel(80,480) == original.get_pixel(80,480):
		printerr("Background treatment missing")
		failures += 1
	room.scale = Vector2(.7,.7)
	room.position = Vector2(120,45)
	view.canvas_transform = Transform2D(0,Vector2(.65,.65),0,Vector2(90,60))
	for n in anchors:
		place(n)
		if (n.get_global_transform_with_canvas()*screen).distance_to(screen) > .001:
			failures += 1
	await check_phase("zoom")
	if legacy:
		print("Legacy black intro and foreground regression reproduced: ",intro_changed and failures > 1)
		quit(0 if intro_changed and failures > 1 else 1)
	else:
		print("Foreground and room coverage regression count: ",failures)
		quit(0 if failures == 0 else 1)
