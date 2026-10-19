extends SceneTree
## P3R 营地菜单主角离线渲染：原版模型（GLB）+ 原版动作（tools/p3r/psa_to_bin.py 的输出）+ 原版材质参数。
## 每帧输出两张：color_NNN.png（画出来的部分：脸、手、衬衫、描边，透明底）和 mask_NNN.png（原版 CaRtOp 材质 = "这里显示水"的区域，白）。
## 用法：godot --path tools/p3r/hero_render -s render.gd -- <config.json>
## config: { "models": [...glb 路径], "textures_dir": "...", "anim": "<prefix>", "frames": [起, 止, 步长], "out": "<dir>",
##           "fov": 30, "cam_basis": [[x],[y],[z]], "cam_origin": [x,y,z] }（或 "look_from"/"look_at"/"up"）

const RAMP = """
shader_type spatial;
render_mode unshaded, cull_disabled;
uniform sampler2D tex : source_color, filter_linear;
uniform vec3 c0 = vec3(0.02, 0.05, 0.30); uniform vec3 c1 = vec3(0.05, 0.30, 0.85); uniform vec3 c2 = vec3(0.50, 0.78, 1.0); uniform vec3 c3 = vec3(1.0);
uniform bool mask_pass = false;
void fragment() {
	vec4 t = texture(tex, UV);
	float l = dot(t.rgb, vec3(0.333));
	vec3 c = l < 0.33 ? mix(c0, c1, l / 0.33) : l < 0.66 ? mix(c1, c2, (l - 0.33) / 0.33) : mix(c2, c3, (l - 0.66) / 0.34);
	ALBEDO = mask_pass ? vec3(0.0) : c;
}
"""
const FLAT = """
shader_type spatial;
render_mode unshaded, cull_disabled;
uniform vec3 col = vec3(1.0);
uniform bool water = false;
uniform bool mask_pass = false;
void fragment() { ALBEDO = mask_pass ? vec3(water ? 1.0 : 0.0) : col; }
"""
const OUTLINE = """
shader_type spatial;
render_mode unshaded, cull_front;
uniform vec3 col = vec3(0.714, 0.769, 0.831);
uniform float width = 0.004;
uniform bool mask_pass = false;
void vertex() { VERTEX += NORMAL * width; }
void fragment() { ALBEDO = mask_pass ? vec3(0.0) : col; }
"""
# 原版材质（research/p3r_camp/hero3d/materials/*.json）：CaFi = 平涂色，CaBa = 贴图（灰度 → 蓝色调），CaRt = 显示水，OlOpCamp = 描边
const FILLS = {"MI_PC0051_C002_02_CaFiOp": "5BBCFF", "MI_PC0051_C002_03_CaFiOp": "9DACC7", "MI_PC0051_C002_04_CaFiOp": "444444", "MI_PC0051_C002_05_CaFiOp": "0961B5", "MI_PC0051_C002_06_CaFiOp": "BCC4D2", "MI_PC0051_C002_01_CaFiOpTxRs": "000000", "MI_PC0051_H000_00_CaBaOp": "00E7F8"}
const TEXMAP = {"MI_PC0051_C002_00_CaBaOp": "T_PC0051_C002_00_Col.png", "MI_PC0051_F000_00_CaBaOp": "T_PC0051_F000_00_Col.png"}

var cfg; var skels = []; var mats = []; var anim; var data; var nb
var frames_todo = []; var fi = -1; var phase = 0; var wait = 0
var cam

func load_glb(p):
	var doc = GLTFDocument.new(); var st = GLTFState.new()
	if doc.append_from_file(p, st) != OK: push_error("glb " + p); return null
	return doc.generate_scene(st)

func walk(n, f):
	f.call(n)
	for c in n.get_children(): walk(c, f)

func sm(code):
	var m = ShaderMaterial.new(); m.shader = Shader.new(); m.shader.code = code; return m

func make_mat(n):
	if FILLS.has(n):
		var m = sm(FLAT); m.set_shader_parameter("col", Color(FILLS[n])); return m
	if n.ends_with("CaRtOp"):
		var m = sm(FLAT); m.set_shader_parameter("col", Color("2050C0")); m.set_shader_parameter("water", true); return m
	if TEXMAP.has(n):
		var m = sm(RAMP); m.set_shader_parameter("tex", ImageTexture.create_from_image(Image.load_from_file(cfg["textures_dir"] + "/" + TEXMAP[n]))); return m
	if n == "MI_PC0051_F000_00_CaEyOp":
		var m = StandardMaterial3D.new(); m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
		m.albedo_texture = ImageTexture.create_from_image(Image.load_from_file(cfg["textures_dir"] + "/T_PC0051_E000_00_Col.png")); m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA_SCISSOR
		return m
	return null

func _initialize():
	cfg = JSON.parse_string(FileAccess.get_file_as_string(OS.get_cmdline_user_args()[0]))
	var w = Node3D.new(); root.add_child(w)
	var outline = sm(OUTLINE); mats.append(outline)
	for p in cfg["models"]:
		var s = load_glb(p)
		if s == null: continue
		w.add_child(s)
		walk(s, func(n):
			if n is Skeleton3D: skels.append(n)
			if n is MeshInstance3D:
				for si in n.mesh.get_surface_count():
					var src = n.mesh.surface_get_material(si)
					var m = make_mat(src.resource_name if src else "")
					if m == null:
						var h = StandardMaterial3D.new(); h.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA; h.albedo_color = Color(0, 0, 0, 0); m = h
					else:
						m.next_pass = outline; mats.append(m)
					n.set_surface_override_material(si, m))
	anim = JSON.parse_string(FileAccess.get_file_as_string(cfg["anim"] + ".json"))
	data = FileAccess.get_file_as_bytes(cfg["anim"] + ".bin").to_float32_array()
	nb = anim["bones"].size()
	var e = Environment.new(); e.background_mode = Environment.BG_CLEAR_COLOR
	var we = WorldEnvironment.new(); we.environment = e; w.add_child(we)
	cam = Camera3D.new(); w.add_child(cam); cam.fov = cfg.get("fov", 30.0)
	if cfg.has("cam_basis"):
		var b = cfg["cam_basis"]; var o = cfg["cam_origin"]
		cam.global_transform = Transform3D(Basis(Vector3(b[0][0], b[0][1], b[0][2]), Vector3(b[1][0], b[1][1], b[1][2]), Vector3(b[2][0], b[2][1], b[2][2])), Vector3(o[0], o[1], o[2]))
	else:
		var f = cfg["look_from"]; var t = cfg["look_at"]; var u = cfg.get("up", [0, 1, 0])
		cam.look_at_from_position(Vector3(f[0], f[1], f[2]), Vector3(t[0], t[1], t[2]), Vector3(u[0], u[1], u[2]))
	var fr = cfg["frames"]
	for f in range(fr[0], min(fr[1], anim["frames"] - 1) + 1, fr[2]): frames_todo.append(f)
	DirAccess.make_dir_recursive_absolute(cfg["out"])

func pose(f):
	for s in skels:
		for b in nb:
			var i = s.find_bone(anim["bones"][b])
			if i < 0: continue
			var o = (f * nb + b) * 7
			s.set_bone_pose_position(i, Vector3(data[o], data[o + 1], data[o + 2]))
			s.set_bone_pose_rotation(i, Quaternion(data[o + 3], data[o + 4], data[o + 5], data[o + 6]).normalized())

func set_mask(on):
	for m in mats:
		if m is ShaderMaterial: m.set_shader_parameter("mask_pass", on)

func _process(_d):
	# 每帧：设姿势 → 等 2 帧 → 存彩色 → 切遮罩 → 等 2 帧 → 存遮罩
	if wait > 0: wait -= 1; return
	if fi >= 0:
		var img = root.get_texture().get_image()
		if phase == 0:
			img.save_png("%s/color_%03d.png" % [cfg["out"], frames_todo[fi]]); set_mask(true); phase = 1; wait = 2; return
		img.save_png("%s/mask_%03d.png" % [cfg["out"], frames_todo[fi]]); set_mask(false); phase = 0
	fi += 1
	if fi >= frames_todo.size(): print("rendered ", frames_todo.size()); quit(); return
	pose(frames_todo[fi]); wait = 2
