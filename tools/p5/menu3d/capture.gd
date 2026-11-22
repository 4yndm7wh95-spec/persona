extends SceneTree

func _initialize() -> void:
	call_deferred("capture")

func capture() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() != 3:
		push_error("Pass repository path, output path, and 'preview' or 'all'")
		quit(1)
		return
	var scratch := "res://scratch/"
	var specs: Array = JSON.parse_string(FileAccess.get_file_as_string(scratch + "meshes.json"))
	var clips: Array = JSON.parse_string(FileAccess.get_file_as_string(scratch + "clips.json"))
	var viewport := SubViewport.new()
	var glove_test := args[2] in ["051W", "051B"]
	var preview := args[2] == "preview" or glove_test
	viewport.size = Vector2i(640, 360) if preview else Vector2i(1920, 1080)
	viewport.transparent_bg = true
	viewport.msaa_3d = Viewport.MSAA_4X
	viewport.render_target_update_mode = SubViewport.UPDATE_DISABLED
	root.add_child(viewport)
	var camera := Camera3D.new()
	viewport.add_child(camera)
	camera.current = true
	camera.position = Vector3(-12, 158, 75)
	camera.rotation_degrees.y = -10.0
	camera.fov = 44.0
	camera.h_offset = -14.0
	camera.v_offset = 0.0
	camera.far = 500.0
	var instances: Array[MeshInstance3D] = []
	var layouts: Array[Array] = []
	for spec in specs:
		var instance := MeshInstance3D.new()
		viewport.add_child(instance)
		instances.append(instance)
		var glove := args[2] if glove_test else "051B"
		instance.visible = not (spec.name.begins_with("Blur") or spec.name.begins_with("b ") or spec.name in ["c0001_body_002_UI_main", "megane01_002_main", "kamen01_051_main", "line"])
		if spec.name.begins_with("c0001_hand_"):
			instance.visible = spec.name == "c0001_hand_" + glove + "_UI_main"
		var mat := StandardMaterial3D.new()
		mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
		mat.cull_mode = BaseMaterial3D.CULL_BACK if spec.cull else BaseMaterial3D.CULL_DISABLED
		mat.albedo_color = Color(spec.color[0], spec.color[1], spec.color[2], spec.color[3])
		if spec.texture != null:
			mat.albedo_color = Color.WHITE
			var path: String = args[0] + "/research/p5r/model/joker/joker_menu_Textures/" + spec.texture.get_basename() + ".png"
			var tex := Image.load_from_file(path)
			if tex == null:
				quit(1)
				return
			mat.albedo_texture = ImageTexture.create_from_image(tex)
			mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA_SCISSOR
			mat.alpha_scissor_threshold = maxf(0.001, float(spec.clip) / 255.0)
		instance.material_override = mat
		var layout := []
		layout.resize(Mesh.ARRAY_MAX)
		var indices := PackedInt32Array(spec.triangles)
		for offset in range(0, indices.size(), 3):
			var swap := indices[offset + 1]
			indices[offset + 1] = indices[offset + 2]
			indices[offset + 2] = swap
		layout[Mesh.ARRAY_INDEX] = indices
		if spec.uv != null:
			var uv := PackedVector2Array()
			for pair in spec.uv:
				uv.append(Vector2(pair[0], pair[1]))
			layout[Mesh.ARRAY_TEX_UV] = uv
		layouts.append(layout)
	for clip in clips:
		if glove_test and clip.name != "main_0000":
			continue
		var folder: String = args[1] + "/" + clip.name
		DirAccess.make_dir_recursive_absolute(folder)
		var picks: Array = clip.frames
		if preview and not glove_test:
			picks = [picks[0], picks[picks.size() / 2], picks[-1]]
		for frame in picks:
			var number := "%04d" % int(frame)
			var file := FileAccess.open(scratch + clip.name + "_" + number + ".bin", FileAccess.READ)
			if file == null:
				quit(1)
				return
			for i in specs.size():
				var vertices := PackedVector3Array()
				for j in int(specs[i].vertices):
					vertices.append(Vector3(file.get_float(), file.get_float(), file.get_float()))
				var layout := layouts[i]
				layout[Mesh.ARRAY_VERTEX] = vertices
				var mesh := ArrayMesh.new()
				mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, layout)
				instances[i].mesh = mesh
			viewport.render_target_update_mode = SubViewport.UPDATE_ONCE
			await RenderingServer.frame_post_draw
			var result := viewport.get_texture().get_image()
			if result == null or result.save_png(folder + "/f" + number + ".png") != OK:
				quit(1)
				return
		print(clip.name)
	quit()
