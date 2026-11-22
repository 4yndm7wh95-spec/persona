extends SceneTree

func _initialize() -> void:
	call_deferred("capture")

func capture() -> void:
	var args := OS.get_cmdline_user_args()
	var pages := args.size() == 4 and args[2] == "pages"
	if not pages and (args.size() != 3 or args[2] == "pages"):
		push_error("Pass REPO OUTPUT MODE, or REPO OUTPUT pages PLAN.json")
		quit(1)
		return
	var plan: Array = []
	if pages:
		var data: Variant = JSON.parse_string(FileAccess.get_file_as_string(args[3]))
		if not data is Array or data.is_empty():
			push_error("Expected a nonempty array of camera jobs")
			quit(1)
			return
		plan = data
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
	var jobs: Array = []
	if pages:
		var clips_by_name := {}
		for clip in clips:
			clips_by_name[clip.name] = clip.frames
		for view in plan:
			for name in view.clips:
				if not clips_by_name.has(name):
					push_error("Unknown clip: " + name)
					quit(1)
					return
				jobs.append({
					"name": name,
					"folder": view.folder + "/" + name,
					"frames": view.get("frames", {}).get(name, clips_by_name[name]),
					"view": view
				})
	else:
		for clip in clips:
			if glove_test and clip.name != "main_0000":
				continue
			var picks: Array = clip.frames
			if preview and not glove_test:
				picks = [picks[0], picks[picks.size() / 2], picks[-1]]
			jobs.append({"name": clip.name, "folder": clip.name, "frames": picks})

	for clip in jobs:
		var folder: String = args[1] + "/" + clip.folder
		if DirAccess.make_dir_recursive_absolute(folder) != OK:
			push_error("Cannot create output directory: " + folder)
			quit(1)
			return
		var draft := false
		if pages:
			var view: Dictionary = clip.view
			draft = view.get("draft", false)
			viewport.size = Vector2i(384, 216) if draft else Vector2i(3840, 2160)
			camera.position = vector3(view.position)
			camera.rotation_degrees = vector3(view.rotation_degrees)
			camera.fov = view.fov
			camera.h_offset = view.get("h_offset", 0.0)
			camera.v_offset = view.get("v_offset", 0.0)
			camera.far = view.get("far", 500.0)
		var picks: Array = clip.frames
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
			if result == null:
				push_error("Cannot read viewport pixels")
				quit(1)
				return
			if pages and not draft:
				# A 2:1 bilinear resize averages each 2x2 block of resolved samples.
				result.resize(1920, 1080, Image.INTERPOLATE_BILINEAR)
				result = to_straight_alpha(result)
			if result.save_png(folder + "/f" + number + ".png") != OK:
				quit(1)
				return
		print(clip.name)
	quit()

func vector3(values: Array) -> Vector3:
	return Vector3(values[0], values[1], values[2])

# Compatibility MSAA resolves against transparent black. Convert the averaged
# premultiplied edge colors to straight alpha before writing PNG.
func to_straight_alpha(result: Image) -> Image:
	result.convert(Image.FORMAT_RGBA8)
	var pixels := result.get_data()
	for offset in range(0, pixels.size(), 4):
		var alpha := pixels[offset + 3]
		if alpha == 0 or alpha == 255:
			continue
		for channel in 3:
			var index := offset + channel
			pixels[index] = mini(255, roundi(pixels[index] * 255.0 / alpha))
	return Image.create_from_data(result.get_width(), result.get_height(), false, Image.FORMAT_RGBA8, pixels)
