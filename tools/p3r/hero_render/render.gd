extends SceneTree
## P3R 营地菜单主角离线渲染：原版模型（GLB）+ 原版动作（tools/p3r/psa_to_bin.py 的输出）+ 原版材质参数。
## 色键：纯绿 (0,1,0) 表示"透出水"。透明底上的抗锯齿边是预乘的（如 (0,128,0,128)），合成时先 rgb /= a 反预乘，再算溢色 s = g − max(r,b)，alpha = 1 − s，颜色 = (rgb − s·绿) / (1 − s)。
## 透水的衣服 / 头发 = 完全透明（用户：不要自己填色，衣服的样子交给原版水背景）；贴图材质暗部 50% 透（原版 RTShadowColor_Blend 0.5）。
## 每帧输出两张：color_NNN.png（画出来的部分：脸、手、衬衫、描边，透明底）和 mask_NNN.png（原版 CaRtOp 材质 = "这里显示水"的区域，白）。
## 用法：godot --path tools/p3r/hero_render -s render.gd -- <config.json>
## config: { "models": [...glb 路径], "textures_dir": "...", "anim": "<prefix>", "frames": [起, 止, 步长], "out": "<dir>",
##           "fov": 30, "cam_basis": [[x],[y],[z]], "cam_origin": [x,y,z] }（或 "look_from"/"look_at"/"up"）

const RAMP = """
shader_type spatial;
render_mode unshaded, cull_disabled;
uniform sampler2D tex : source_color, filter_linear;
uniform vec3 c0 = vec3(0.03, 0.08, 0.28); uniform vec3 c1 = vec3(0.25, 0.62, 0.92); uniform vec3 c2 = vec3(0.80, 0.95, 1.0); uniform vec3 c3 = vec3(1.0);
uniform vec3 shade = vec3(0.55, 0.85, 1.0);
uniform bool mask_pass = false;
varying vec3 wn;
void vertex() { wn = (MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz; }
vec3 hero_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(vec3(0.04045), c));
}
void fragment() {
	vec4 t = texture(tex, UV);
	float l = dot(t.rgb, vec3(0.333));
	vec3 c = l < 0.25 ? mix(c0, c1, l / 0.25) : l < 0.6 ? mix(c1, c2, (l - 0.25) / 0.35) : mix(c2, c3, (l - 0.6) / 0.4);
	// Preserve the original dark straps and face strokes before applying the menu's bright effects.
	float ink = smoothstep(0.12, 0.60, dot(OUTPUT_IS_SRGB ? hero_linear(t.rgb) : t.rgb, vec3(1.0 / 3.0)));
	vec3 dark_base = OUTPUT_IS_SRGB ? vec3(0.065, 0.075, 0.090) : hero_linear(vec3(0.065, 0.075, 0.090));
	c = mix(max(t.rgb, dark_base), c, ink);
	float side = FRONT_FACING ? 1.0 : -1.0;
	float nl = dot(normalize(wn) * side, normalize(vec3(-0.4, 0.6, 0.7)));
	float lit = smoothstep(-0.2 - 0.1, -0.2 + 0.1, nl);
	c *= mix(shade, vec3(1.0), lit);
	c = mix(c, vec3(0.0, 1.0, 0.0), 0.5 * (1.0 - lit) * ink);   // 亮色材质的阴影透水；深色笔画保持不透明
	float rim = pow(1.0 - abs(dot(normalize(NORMAL), VIEW)), 4.0);
	vec3 dark_rim = OUTPUT_IS_SRGB ? vec3(0.20, 0.23, 0.27) : hero_linear(vec3(0.20, 0.23, 0.27));
	c = mix(c, dark_rim, rim * (1.0 - ink) * 0.65);
	c = mix(c, vec3(0.80, 0.94, 1.0), rim * 0.6 * ink);
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
uniform float width = 0.0006;
uniform bool mask_pass = false;
void vertex() { VERTEX += NORMAL * width; }
void fragment() { ALBEDO = mask_pass ? vec3(0.0) : col; }
"""
# v0.14.90 对照原版 COMMU 截图调过：贴图材质加卡通明暗（原版 ToonThreshold -0.2 / Softness 0.1）+ 边缘光 CDEFFF，描边用原版浅色 B6C4D4 极细（用户：原版手上没有重描边），透水部分不描边。
# 原版材质（research/p3r_camp/hero3d/materials/*.json）：CaFi = 平涂色，CaBa = 贴图（灰度 → 蓝色调），CaRt = 显示水，OlOpCamp = 描边
const FILLS = {"MI_PC0051_C002_02_CaFiOp": "5BBCFF", "MI_PC0051_C002_03_CaFiOp": "9DACC7", "MI_PC0051_C002_04_CaFiOp": "444444", "MI_PC0051_C002_05_CaFiOp": "0961B5", "MI_PC0051_C002_06_CaFiOp": "BCC4D2", "MI_PC0051_C002_01_CaFiOpTxRs": "000000", "MI_PC0051_H000_00_CaBaOp": "00E7F8"}
const TEXMAP = {"MI_PC0051_C002_00_CaBaOp": "T_PC0051_C002_00_Col.png", "MI_PC0051_F000_00_CaBaOp": "T_PC0051_F000_00_Col.png"}

var cfg; var skels = []; var mats = []; var anim; var data; var nb
var sc_names = []; var sc_data = PackedFloat32Array()   # 可选：原版缩放轨（道具骨骼缩到 0.001 = 隐藏）
var props = []   # 可选：独立道具（原版 COMMU 的塔罗牌 SK_Pp2001_000），各自用自己的动作
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
		var m = sm(FLAT); m.set_shader_parameter("col", Color(0, 1, 0)); m.set_shader_parameter("water", true); m.set_meta("no_outline", true); return m
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
		# 原版 DT_CampCameraTable 的页级角色偏移（UE cm → glTF m：x→x、z→y、y→−z）和偏航
		if cfg.has("chara_pos"):
			var cp = cfg["chara_pos"]; s.position = Vector3(cp[0], cp[2], -cp[1]) / 100.0
		if cfg.has("chara_yaw"): s.rotation_degrees.y = cfg["chara_yaw"]
		walk(s, func(n):
			if n is Skeleton3D: skels.append(n)
			if n is MeshInstance3D:
				for si in n.mesh.get_surface_count():
					var src = n.mesh.surface_get_material(si)
					var m = make_mat(src.resource_name if src else "")
					if m == null:
						var h = StandardMaterial3D.new(); h.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA; h.albedo_color = Color(0, 0, 0, 0); m = h
					else:
						# v0.14.90：不描边（用户：原版没有描边；对照截图去掉后更接近）
						mats.append(m)
					n.set_surface_override_material(si, m))
	if cfg.has("scale"):
		sc_names = JSON.parse_string(FileAccess.get_file_as_string(cfg["scale"] + ".json"))["bones"]
		sc_data = FileAccess.get_file_as_bytes(cfg["scale"] + ".bin").to_float32_array()
	for pr in cfg.get("props", []):
		var s = load_glb(pr["model"]); w.add_child(s)
		var ps = []
		walk(s, func(n):
			if n is Skeleton3D: ps.append(n)
			if n is MeshInstance3D:
				for si in n.mesh.get_surface_count():
					var src = n.mesh.surface_get_material(si); var nm = src.resource_name if src else ""
					var m = StandardMaterial3D.new(); m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED; m.cull_mode = BaseMaterial3D.CULL_DISABLED
					m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA_SCISSOR
					m.albedo_texture = ImageTexture.create_from_image(Image.load_from_file(pr["tex_back"] if nm.contains("Backside") else pr["tex_front"]))
					n.set_surface_override_material(si, m))
		var pa = JSON.parse_string(FileAccess.get_file_as_string(pr["anim"] + ".json"))
		props.append({"skels": ps, "anim": pa, "data": FileAccess.get_file_as_bytes(pr["anim"] + ".bin").to_float32_array()})
	anim = JSON.parse_string(FileAccess.get_file_as_string(cfg["anim"] + ".json"))
	data = FileAccess.get_file_as_bytes(cfg["anim"] + ".bin").to_float32_array()
	nb = anim["bones"].size()
	var e = Environment.new(); e.background_mode = Environment.BG_CLEAR_COLOR
	var we = WorldEnvironment.new(); we.environment = e; w.add_child(we)
	cam = Camera3D.new(); w.add_child(cam); cam.fov = cfg.get("fov", 30.0)
	if cfg.get("fov_horizontal", false): cam.keep_aspect = Camera3D.KEEP_WIDTH   # UE 的 FOVAngle 是水平视角
	if cfg.has("cam_basis"):
		var b = cfg["cam_basis"]; var o = cfg["cam_origin"]
		cam.global_transform = Transform3D(Basis(Vector3(b[0][0], b[0][1], b[0][2]), Vector3(b[1][0], b[1][1], b[1][2]), Vector3(b[2][0], b[2][1], b[2][2])), Vector3(o[0], o[1], o[2]))
	else:
		var f = cfg["look_from"]; var t = cfg["look_at"]; var u = cfg.get("up", [0, 1, 0])
		cam.look_at_from_position(Vector3(f[0], f[1], f[2]), Vector3(t[0], t[1], t[2]), Vector3(u[0], u[1], u[2]))
	if cfg.has("frame_list"):
		frames_todo = cfg["frame_list"]
	else:
		var fr = cfg["frames"]
		for f in range(fr[0], min(fr[1], anim["frames"] - 1) + 1, fr[2]): frames_todo.append(f)
	DirAccess.make_dir_recursive_absolute(cfg["out"])

func pose_skels(ss, an, dt, f):
	# f 可以是小数：相邻两帧插值（位置 lerp、旋转 slerp），和游戏里 HeroStage 一样
	var n = an["bones"].size(); f = clamp(float(f), 0.0, an["frames"] - 1.0)
	var fa = int(floor(f)); var fb = min(fa + 1, an["frames"] - 1); var t = f - fa
	for s in ss:
		for b in n:
			var i = s.find_bone(an["bones"][b])
			if i < 0: continue
			var oa = (fa * n + b) * 7; var ob = (fb * n + b) * 7
			s.set_bone_pose_position(i, Vector3(dt[oa], dt[oa + 1], dt[oa + 2]).lerp(Vector3(dt[ob], dt[ob + 1], dt[ob + 2]), t))
			s.set_bone_pose_rotation(i, Quaternion(dt[oa + 3], dt[oa + 4], dt[oa + 5], dt[oa + 6]).normalized().slerp(Quaternion(dt[ob + 3], dt[ob + 4], dt[ob + 5], dt[ob + 6]).normalized(), t))

func pose(f):
	pose_skels(skels, anim, data, f)
	if sc_names.size() > 0:
		var n = sc_names.size(); var g = min(int(f), sc_data.size() / (n * 3) - 1)
		for s in skels:
			for b in n:
				var o = (g * n + b) * 3
				if abs(sc_data[o] - 1.0) < 0.0005 and abs(sc_data[o + 1] - 1.0) < 0.0005 and abs(sc_data[o + 2] - 1.0) < 0.0005: continue
				var i = s.find_bone(sc_names[b])
				if i >= 0: s.set_bone_pose_scale(i, Vector3(sc_data[o], sc_data[o + 2], sc_data[o + 1]))   # UE (x,y,z) → glTF (x,z,y)
	for pr in props: pose_skels(pr["skels"], pr["anim"], pr["data"], f)

func set_mask(on):
	for m in mats:
		if m is ShaderMaterial: m.set_shader_parameter("mask_pass", on)

func _process(_d):
	# 每帧：设姿势 → 等 2 帧 → 存彩色 → 切遮罩 → 等 2 帧 → 存遮罩
	if wait > 0: wait -= 1; return
	if fi >= 0:
		var img = root.get_texture().get_image()
		if phase == 0:
			img.save_png(("%s/color_%04d.png" % [cfg["out"], fi]) if cfg.has("frame_list") else ("%s/color_%03d.png" % [cfg["out"], frames_todo[fi]])); set_mask(true); phase = 1; wait = 2; return
		img.save_png(("%s/mask_%04d.png" % [cfg["out"], fi]) if cfg.has("frame_list") else ("%s/mask_%03d.png" % [cfg["out"], frames_todo[fi]])); set_mask(false); phase = 0
	fi += 1
	if fi >= frames_todo.size(): print("rendered ", frames_todo.size()); quit(); return
	pose(frames_todo[fi]); wait = 2
