extends SceneTree

var view: SubViewport
var rear: ShaderMaterial
var front: ShaderMaterial
var paper: Node2D
var screen_root: Node2D
var screen := Vector2(960, 540)

func _initialize():
	call_deferred("run")

func load_texture(path: String) -> Texture2D:
	return ImageTexture.create_from_image(Image.load_from_file(path))

func make_hand(parent: Node, mode: float) -> ShaderMaterial:
	var shader := Shader.new()
	shader.code = FileAccess.get_file_as_string("res://hand.gdshader")
	var mat := ShaderMaterial.new()
	mat.shader = shader
	var noise := NoiseTexture2D.new()
	noise.width = 256
	noise.height = 256
	var source := FastNoiseLite.new()
	source.seed = 7419
	source.frequency = 0.025
	noise.noise = source
	mat.set_shader_parameter("noise", noise)
	var ramp := GradientTexture2D.new()
	ramp.width = 256
	ramp.height = 1
	var gradient := Gradient.new()
	gradient.colors = PackedColorArray([Color(0.01,0,0.005),Color(0.4,0.005,0.02),Color(1,0.05,0.12),Color(1,0.7,0.6)])
	gradient.offsets = PackedFloat32Array([0,0.4,0.75,1])
	ramp.gradient = gradient
	mat.set_shader_parameter("lut", ramp)
	mat.set_shader_parameter("aspect", screen.x / screen.y)
	mat.set_shader_parameter("band_z", 50.0)
	mat.set_shader_parameter("hand_pass", mode)
	mat.set_shader_parameter("clock", 1.0)
	var rect := ColorRect.new()
	rect.size = screen
	rect.material = mat
	parent.add_child(rect)
	return mat

func make_paper(parent: Node):
	var data = JSON.parse_string(FileAccess.get_file_as_string("res://eye_cutin.json"))
	var tex := load_texture("res://eyes.png")
	paper = Node2D.new()
	parent.add_child(paper)
	var canvas := Vector2(data.canvas[0], data.canvas[1])
	var k: float = max(screen.x / canvas.x, screen.y / canvas.y) * 0.82
	paper.scale = Vector2.ONE * k
	paper.position = (screen - canvas * k) / 2 + Vector2(0, -screen.y * 0.06)
	var opaque := Shader.new()
	opaque.code = "shader_type canvas_item; void fragment(){ COLOR=vec4(texture(TEXTURE,UV).rgb,1.0); }"
	for part in data.meshes:
		if part.linear: continue
		var opacity: float = 0.0
		for key in part.keys:
			if key[0] <= 1.45: opacity = key[1]
		if opacity < 0.001: continue
		var arrays := []
		arrays.resize(Mesh.ARRAY_MAX)
		var vertices := PackedVector3Array()
		var colors := PackedColorArray()
		var uvs := PackedVector2Array()
		for i in part.vertices.size():
			vertices.append(Vector3(part.vertices[i][0], part.vertices[i][1], 0))
			colors.append(Color(part.color[0], part.color[1], part.color[2], opacity))
			if part.textured: uvs.append(Vector2(part.uv[i][0], part.uv[i][1]))
		arrays[Mesh.ARRAY_VERTEX] = vertices
		arrays[Mesh.ARRAY_COLOR] = colors
		arrays[Mesh.ARRAY_INDEX] = PackedInt32Array(part.indices)
		if part.textured: arrays[Mesh.ARRAY_TEX_UV] = uvs
		var mesh := ArrayMesh.new()
		mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
		var node := MeshInstance2D.new()
		node.mesh = mesh
		if part.textured:
			node.texture = tex
			var mat := ShaderMaterial.new()
			mat.shader = opaque
			node.material = mat
		paper.add_child(node)

func set_pose(k: float):
	var position := Vector3(0.05,-0.37,1.6).lerp(Vector3(0.05,0.03,2.2),k)
	for mat in [rear,front]:
		mat.set_shader_parameter("hand_pos",position)
		mat.set_shader_parameter("tilt",lerpf(-0.15,-1.1,k))
		mat.set_shader_parameter("roll",lerpf(0.1,0.3,k))
		mat.set_shader_parameter("curl",k)
		mat.set_shader_parameter("thumb",k)
		mat.set_shader_parameter("spread",lerpf(1.4,0.9,k))
		mat.set_shader_parameter("elbow", position+Vector3(-0.3,-0.6,6))
		mat.set_shader_parameter("grip", k*0.7)
		mat.set_shader_parameter("front_mix", k)

func save_frame(name: String):
	await process_frame
	await RenderingServer.frame_post_draw
	await process_frame
	await RenderingServer.frame_post_draw
	var image := view.get_texture().get_image()
	assert(not image.is_empty())
	assert(image.save_png("res://" + name + ".png") == OK)
	print("PASS rendered ", name, " ", image.get_size())

func run():
	view = SubViewport.new()
	view.size = Vector2i(screen)
	view.transparent_bg = true
	view.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(view)
	var bg := ColorRect.new()
	bg.color = Color("202028")
	bg.size = screen
	view.add_child(bg)
	var room := Control.new()
	room.position = Vector2(170,90)
	room.scale = Vector2(0.7,0.7)
	view.add_child(room)
	screen_root = Node2D.new()
	room.add_child(screen_root)
	screen_root.transform = room.get_global_transform_with_canvas().affine_inverse()
	rear = make_hand(screen_root,1.0)
	make_paper(screen_root)
	front = make_hand(screen_root,2.0)
	for pose in [0.0,0.45,1.0]:
		set_pose(pose)
		await save_frame("grip_" + str(roundi(pose*100)))
	# Same viewport-space coverage despite both a transformed room parent and Camera2D-equivalent canvas zoom.
	view.canvas_transform = Transform2D(0,Vector2(0.55,0.55),0,Vector2(120,75))
	screen_root.transform = room.get_global_transform_with_canvas().affine_inverse()
	var result := screen_root.get_global_transform_with_canvas()
	assert((result * Vector2.ZERO).distance_to(Vector2.ZERO)<0.001)
	assert((result * screen).distance_to(screen)<0.001)
	print("PASS viewport transform cancels room scale/translation and canvas zoom/pan")
	await save_frame("grip_boss_zoom")
	var wave_shader := Shader.new()
	wave_shader.code = FileAccess.get_file_as_string("res://wave.gdshader")
	for i in 5:
		var mat := ShaderMaterial.new()
		mat.shader = wave_shader
		mat.set_shader_parameter("radius",0.65 + i*0.04)
		mat.set_shader_parameter("width",0.018)
		mat.set_shader_parameter("alpha",0.75)
		mat.set_shader_parameter("tint",Color(1,0.08+i*0.12,0.12+i*0.12))
		var rect := ColorRect.new()
		rect.size = Vector2.ONE * (300+i*250)
		rect.position = screen/2 - rect.size/2
		rect.material = mat
		screen_root.add_child(rect)
	await save_frame("crush_wave")
	quit()
