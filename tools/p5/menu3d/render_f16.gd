extends SceneTree
## 云端渲染菜单 3D Joker（Claude 用来逐帧对机位）：读 research/p5r/menu3d_data 的 meshes.json + <片段>.f16（Codex 从 GMD/GAP 蒙皮导出的逐帧顶点，
## 小端 float16，[帧][顶点][xyz]），按任务 JSON 渲成透明底 PNG。着色同 capture.gd：无光照、贴图原色、alpha 裁切、原剔除。
## 用法：godot --headless? 不行（要渲染）→ xvfb-run godot --path tools/p5/menu3d --script render_f16.gd -- <repo> <jobs.json>
## jobs.json：{"size":[1920,1080],"ss":2,"jobs":[{"clip":"main_0015","frame":5.0,"camera":{"position":[..],"rotation_degrees":[..],"fov":40,
##             "h_offset":0,"v_offset":0},"mode":"color|silhouette","out":"/abs/path.png"}]}
## frame 可以是小数（相邻两帧线性插值）；mode=silhouette 时全部画成纯黑（叠图对轮廓用）。
## 也可以给 "verts": "/abs/x.bin"（小端 float32 [顶点][xyz]），这时 clip / frame 只作标注。

func _initialize() -> void:
	call_deferred("run")

var specs: Array = []
var instances: Array[MeshInstance3D] = []
var layouts: Array[Array] = []
var color_mats: Array[Material] = []
var black_mat: StandardMaterial3D
var total_vertices := 0
var clip_cache: Dictionary = {}
var clips_info: Dictionary = {}

func run() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() != 2:
		push_error("Pass REPO JOBS.json")
		quit(1)
		return
	var repo: String = args[0]
	var data_dir := repo + "/research/p5r/menu3d_data/"
	var plan: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(args[1]))
	specs = JSON.parse_string(FileAccess.get_file_as_string(data_dir + "meshes.json"))
	for c in JSON.parse_string(FileAccess.get_file_as_string(data_dir + "clips.json")):
		clips_info[c.name] = c
	var size: Array = plan.get("size", [1920, 1080])
	var ss: int = int(plan.get("ss", 2))
	var viewport := SubViewport.new()
	viewport.size = Vector2i(int(size[0]) * ss, int(size[1]) * ss)
	viewport.transparent_bg = true
	viewport.msaa_3d = Viewport.MSAA_4X
	viewport.render_target_update_mode = SubViewport.UPDATE_DISABLED
	root.add_child(viewport)
	var camera := Camera3D.new()
	viewport.add_child(camera)
	camera.current = true
	camera.far = 2000.0
	camera.near = 1.0
	black_mat = StandardMaterial3D.new()
	black_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	black_mat.albedo_color = Color.BLACK
	black_mat.cull_mode = BaseMaterial3D.CULL_DISABLED
	for spec in specs:
		var instance := MeshInstance3D.new()
		viewport.add_child(instance)
		instances.append(instance)
		var mat := StandardMaterial3D.new()
		mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
		mat.cull_mode = BaseMaterial3D.CULL_BACK if spec.get("cull", true) else BaseMaterial3D.CULL_DISABLED
		var col: Array = spec.get("color", [1, 1, 1, 1])
		mat.albedo_color = Color(col[0], col[1], col[2], col[3])
		if spec.get("texture") != null:
			mat.albedo_color = Color.WHITE
			var tex := Image.load_from_file(repo + "/research/p5r/model/joker/joker_menu_Textures/" + String(spec.texture).get_basename() + ".png")
			if tex != null:
				mat.albedo_texture = ImageTexture.create_from_image(tex)
				mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA_SCISSOR
				mat.alpha_scissor_threshold = maxf(0.001, float(spec.get("clip", 0)) / 255.0)
		color_mats.append(mat)
		var layout := []
		layout.resize(Mesh.ARRAY_MAX)
		var indices := PackedInt32Array(spec.triangles)
		for o in range(0, indices.size(), 3):
			var swap := indices[o + 1]
			indices[o + 1] = indices[o + 2]
			indices[o + 2] = swap
		layout[Mesh.ARRAY_INDEX] = indices
		if spec.get("uv") != null:
			var uv := PackedVector2Array()
			for pair in spec.uv:
				uv.append(Vector2(pair[0], pair[1]))
			layout[Mesh.ARRAY_TEX_UV] = uv
		layouts.append(layout)
		total_vertices += int(spec.vertices)
	for job in plan.jobs:
		var cam: Dictionary = job.camera
		camera.position = Vector3(cam.position[0], cam.position[1], cam.position[2])
		camera.rotation_degrees = Vector3(cam.rotation_degrees[0], cam.rotation_degrees[1], cam.rotation_degrees[2])
		camera.fov = float(cam.get("fov", 40.0))
		camera.h_offset = float(cam.get("h_offset", 0.0))
		camera.v_offset = float(cam.get("v_offset", 0.0))
		var verts: PackedVector3Array
		if job.has("verts"):
			verts = raw_vertices(String(job.verts))
		else:
			verts = frame_vertices(data_dir, String(job.clip), float(job.frame))
		var silhouette: bool = job.get("mode", "color") == "silhouette"
		var hidden: Array = job.get("hide", [])
		var cursor := 0
		for i in specs.size():
			var n := int(specs[i].vertices)
			var layout := layouts[i]
			layout[Mesh.ARRAY_VERTEX] = verts.slice(cursor, cursor + n)
			cursor += n
			var mesh := ArrayMesh.new()
			mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, layout)
			instances[i].mesh = mesh
			instances[i].material_override = black_mat if silhouette else color_mats[i]
			instances[i].visible = not (String(specs[i].name) in hidden)
		viewport.render_target_update_mode = SubViewport.UPDATE_ONCE
		await RenderingServer.frame_post_draw
		var img := viewport.get_texture().get_image()
		if ss > 1:
			img.resize(int(size[0]), int(size[1]), Image.INTERPOLATE_BILINEAR)
		img = to_straight_alpha(img)
		img.save_png(String(job.out))
		print("rendered ", job.out)
	quit()

func clip_bytes(data_dir: String, clip: String) -> PackedByteArray:
	if not clip_cache.has(clip):
		clip_cache[clip] = FileAccess.get_file_as_bytes(data_dir + clip + ".f16")
	return clip_cache[clip]

func frame_vertices(data_dir: String, clip: String, frame: float) -> PackedVector3Array:
	var bytes := clip_bytes(data_dir, clip)
	var frames := int(clips_info[clip].frames)
	var f0 := clampi(int(floor(frame)), 0, frames - 1)
	var f1 := clampi(f0 + 1, 0, frames - 1)
	var t := clampf(frame - floor(frame), 0.0, 1.0)
	var stride := total_vertices * 6
	var a := f0 * stride
	var b := f1 * stride
	var out := PackedVector3Array()
	out.resize(total_vertices)
	for v in total_vertices:
		var o := v * 6
		var p0 := Vector3(bytes.decode_half(a + o), bytes.decode_half(a + o + 2), bytes.decode_half(a + o + 4))
		if t > 0.0001 and f1 != f0:
			var p1 := Vector3(bytes.decode_half(b + o), bytes.decode_half(b + o + 2), bytes.decode_half(b + o + 4))
			p0 = p0.lerp(p1, t)
		out[v] = p0
	return out

## 任务里给了 "verts"（小端 float32，[顶点][xyz]，顶点数 = meshes.json 总数）就直接用它：Python 侧算好的混合帧 / 转头后的顶点
func raw_vertices(path: String) -> PackedVector3Array:
	var bytes := FileAccess.get_file_as_bytes(path)
	var out := PackedVector3Array()
	out.resize(total_vertices)
	for v in total_vertices:
		var o := v * 12
		out[v] = Vector3(bytes.decode_float(o), bytes.decode_float(o + 4), bytes.decode_float(o + 8))
	return out

func to_straight_alpha(result: Image) -> Image:
	result.convert(Image.FORMAT_RGBA8)
	var pixels := result.get_data()
	for o in range(0, pixels.size(), 4):
		var alpha := pixels[o + 3]
		if alpha == 0 or alpha == 255:
			continue
		for c in 3:
			pixels[o + c] = mini(255, roundi(pixels[o + c] * 255.0 / alpha))
	return Image.create_from_data(result.get_width(), result.get_height(), false, Image.FORMAT_RGBA8, pixels)
