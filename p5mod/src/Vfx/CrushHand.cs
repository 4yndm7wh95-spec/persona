using Godot;

namespace P5Joker;

/// <summary>
/// 毒性爆发的「巨手捏碎眼神特写」（用户 2026-11-03 创意）。手本身就是特效，不用贴图：着色器里光线步进一只 3D 的手
/// （手掌、三节四指、拇指、伸进画面深处的前臂），透视镜头渲染 —— 从画面深处朝镜头冲出来、手指朝镜头弯，近大远小都是真的。
/// 用户：霸气、帅、立体感一定要强，不要霓虹描边 → 主体近乎纯黑，硬的三档明暗（暗部 / 中间 / 受光面暗红）+ 指节上的硬高光
/// + 锐利的红色轮廓光 + 凹处压暗（攥紧时凹处透红光）+ 前臂越远越暗化进黑烟；剪影外一圈往上窜的黑红咒火。
/// 世界坐标：镜头在原点朝 +z 看，y 朝上。手的局部坐标：手腕在原点，手指朝 +y，掌心朝 -z（朝镜头）。
/// </summary>
public sealed class CrushHand
{
	private static Shader? _shader;

	private static Shader HandShader => _shader ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D noise : repeat_enable, filter_linear;
uniform sampler2D lut : repeat_disable, filter_linear;
uniform float clock = 0.0;
uniform vec3 hand_pos = vec3(0.0, -0.8, 2.6);   // 手腕在世界里的位置
uniform float hand_size = 1.0;
uniform float tilt = -0.25;     // 绕 x 轴：负 = 指尖朝镜头倒过来（压迫感）
uniform float roll = 0.0;       // 绕 z 轴
uniform float yaw = 0.0;        // 绕 y 轴
uniform float curl = 0.0;       // 0 张开 → 1 攥拳
uniform float spread = 1.0;
uniform float thumb = 0.0;      // 0 张开 → 1 横压在四指前面
uniform float grip = 0.0;       // 指缝透出的红光
uniform float flash = 0.0;
uniform float dissolve = 0.0;   // 捏碎之后：手散成一团团咒烟
uniform float alpha = 1.0;
uniform float aspect = 1.7778;
uniform float focal = 1.25;
uniform vec3 elbow = vec3(-2.2, 0.3, 8.0);       // 前臂另一头（世界坐标，在横幅后面）：手臂从横幅里穿出来
uniform float band_z = 5.0;                      // 眼神横幅所在的深度：比它远的部分不画，穿出的地方一圈红色裂口

float sd_capsule(vec3 p, vec3 a, vec3 b, float r) {
	vec3 pa = p - a, ba = b - a;
	float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
	return length(pa - ba * h) - r;
}
float sd_rbox(vec3 p, vec3 b, float r) {
	vec3 q = abs(p) - b + r;
	return length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0) - r;
}
float smin(float a, float b, float k) {
	float h = clamp(0.5 + 0.5 * (b - a) / k, 0.0, 1.0);
	return mix(b, a, h) - k * h * (1.0 - h);
}
vec3 fdir(float a, float t) { return vec3(-sin(a) * cos(t), cos(a) * cos(t), -sin(t)); }
// 手指往掌心那边（-z，朝镜头）弯：三节，每节在上一节的基础上再弯
float finger(vec3 p, vec3 base, float a, float L, float r) {
	float c = curl;
	float t1 = 0.12 + 1.35 * c, t2 = t1 + 0.1 + 1.55 * c, t3 = t2 + 0.08 + 1.15 * c;
	vec3 j1 = base + fdir(a, t1) * L * 0.45;
	vec3 j2 = j1 + fdir(a, t2) * L * 0.31;
	vec3 tip = j2 + fdir(a, t3) * L * 0.24;
	float d = sd_capsule(p, base, j1, r);
	d = smin(d, sd_capsule(p, j1, j2, r * 0.92), 0.025);
	return smin(d, sd_capsule(p, j2, tip, r * 0.84), 0.025);
}
float hand(vec3 p) {
	// 手掌：上宽下窄，大鱼际（拇指根的肉）鼓出来
	float w = mix(0.36, 0.45, clamp(p.y, 0.0, 1.0));
	float d = sd_rbox(p - vec3(0.0, 0.52, 0.0), vec3(w, 0.48, 0.1), 0.1);
	d = smin(d, length((p - vec3(0.24, 0.3, -0.06)) / vec3(0.24, 0.3, 0.13)) * 0.13 - 0.13, 0.08);
	// 手腕
	d = smin(d, sd_capsule(p, vec3(0.0, 0.1, 0.03), vec3(0.0, -0.25, 0.08), 0.3), 0.1);
	float sp = spread * (1.0 - 0.75 * curl);
	d = smin(d, finger(p, vec3(-0.33, 0.96, 0.0), 0.24 * sp, 0.86, 0.1), 0.04);
	d = smin(d, finger(p, vec3(-0.11, 1.0, 0.0), 0.08 * sp, 1.0, 0.108), 0.04);
	d = smin(d, finger(p, vec3(0.12, 0.99, 0.0), -0.07 * sp, 0.94, 0.104), 0.04);
	d = smin(d, finger(p, vec3(0.33, 0.93, 0.0), -0.24 * sp, 0.72, 0.09), 0.04);
	// 拇指（对照用户拍的照片）：张开时从掌根右下往右、略朝镜头伸出去；攥拳时横压在弯起来的四指前面（右下 → 左）
	vec3 tb = vec3(0.32, 0.14, -0.05);
	vec3 tj = mix(tb + vec3(0.3, 0.0, -0.14), tb + vec3(0.08, 0.3, -0.42), thumb);
	vec3 tt = mix(tb + vec3(0.6, -0.02, -0.28), tb + vec3(-0.4, 0.46, -0.62), thumb);
	d = smin(d, smin(sd_capsule(p, tb, tj, 0.14), sd_capsule(p, tj, tt, 0.115), 0.025), 0.08);
	return d;
}
vec3 to_local(vec3 pw) {
	vec3 p = (pw - hand_pos) / hand_size;
	float c = cos(-yaw), s = sin(-yaw);
	p.xz = vec2(c * p.x + s * p.z, -s * p.x + c * p.z);
	c = cos(-roll); s = sin(-roll);
	p.xy = vec2(c * p.x - s * p.y, s * p.x + c * p.y);
	c = cos(-tilt); s = sin(-tilt);
	p.yz = vec2(c * p.y - s * p.z, s * p.y + c * p.z);
	return p;
}
// 前臂（世界坐标）：手腕到肘，往肘那头稍粗（小臂肌肉）
float arm(vec3 pw) {
	vec3 a = hand_pos, b = elbow;
	vec3 pa = pw - a, ba = b - a;
	float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
	return length(pa - ba * h) - hand_size * mix(0.3, 0.42, smoothstep(0.0, 0.5, h));
}
float map(vec3 pw) { return smin(hand(to_local(pw)) * hand_size, arm(pw), 0.12 * hand_size); }
// 3D 值噪声（烟的翻滚）
float hash3(vec3 p) { p = fract(p * 0.3183099 + 0.1); p *= 17.0; return fract(p.x * p.y * p.z * (p.x + p.y + p.z)); }
float vnoise(vec3 x) {
	vec3 i = floor(x), f = fract(x); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(hash3(i), hash3(i + vec3(1, 0, 0)), f.x), mix(hash3(i + vec3(0, 1, 0)), hash3(i + vec3(1, 1, 0)), f.x), f.y),
		mix(mix(hash3(i + vec3(0, 0, 1)), hash3(i + vec3(1, 0, 1)), f.x), mix(hash3(i + vec3(0, 1, 1)), hash3(i + vec3(1, 1, 1)), f.x), f.y), f.z);
}
float fbm(vec3 x) { return vnoise(x) * 0.62 + vnoise(x * 2.3 + 7.1) * 0.38; }
vec3 normal_at(vec3 p, float e) {
	vec2 k = vec2(1.0, -1.0);
	return normalize(k.xyy * map(p + k.xyy * e) + k.yyx * map(p + k.yyx * e) + k.yxy * map(p + k.yxy * e) + k.xxx * map(p + k.xxx * e));
}
// 先球面追踪到烟的外壳（离表面 shell 以内），返回进入距离（没中 = -1）；m = 一路上离表面最近的视角距离（剪影外的火用）
float enter(vec3 rd, float shell, out float m) {
	float t = 0.3; m = 1e3;
	for (int i = 0; i < 90; i++) {
		float d = map(rd * t);
		m = min(m, d / t);
		if (d < shell) return t;
		t += d * 0.9;
		if (t > 30.0) break;
	}
	return -1.0;
}

void fragment() {
	vec2 uv = (UV * 2.0 - 1.0) * vec2(aspect, -1.0);
	vec3 rd = normalize(vec3(uv, focal));
	float hs = hand_size;
	float m;
	float t = enter(rd, 0.3 * hs, m);
	vec3 col = vec3(0.0);
	float T = 1.0;                                            // 透过率（从前往后累积）
	if (t > 0.0) {
		// 手形的咒烟：沿视线一步步走进去。密度 = 手的距离场被 3D 噪声撕毛（边缘往上飘）；
		// 越靠近表面越透红光（薄的地方：手指边缘、指缝 → 叠在掌心前面的手指自己勾出一道红边），越深越黑
		float dt = 0.022 * hs;
		bool shaded = false;
		t += fract(sin(dot(UV * 1000.0, vec2(12.9898, 78.233)) + clock) * 43758.5453) * dt;     // 起点抖一下：不然固定步长切出一圈圈木纹
		for (int i = 0; i < 64; i++) {
			vec3 pw = rd * t;
			if (pw.z > band_z || T < 0.02) break;
			vec3 lp = to_local(pw);
			float s = map(pw) / hs;
			if (s > 0.32) { t += (s - 0.28) * hs; continue; }
			float stepv = s > 0.06 ? 0.045 * hs : dt;                  // 手外面的烟丝走大步，进到手里走小步
			// 手四周的咒烟丝：离手 0.3 以内，被往上飘的噪声撕成一缕一缕（手不再「形单影只」）
			float wn = fbm(lp * vec3(2.4, 1.3, 2.4) + vec3(0.0, -clock * 1.3, clock * 0.2));
			float aura = smoothstep(0.3, 0.02, s) * smoothstep(0.5, 0.72, wn) * step(0.0, s) * smoothstep(0.0, 0.5, lp.y);   // 手腕以下不出烟（不然像一摊血）
			if (aura > 0.001) {
				// 以暗烟为主（一缕缕黑烟），只在烟丝最浓的地方带一点暗红
				float aa = 1.0 - exp(-aura * stepv / hs * 9.0);
				col += T * (aa * vec3(0.025, 0.0, 0.008) + vec3(0.5, 0.025, 0.055) * aura * smoothstep(0.62, 0.8, wn) * stepv / hs * 1.2);
				T *= 1.0 - aa;
			}
			vec3 fp = lp * 5.0 + vec3(0.0, -clock * 1.4, clock * 0.3);
			float nz = fbm(fp);
			float sd = s + (nz - 0.5) * 0.12 + dissolve * (0.22 + (nz - 0.5) * 0.7);
			float dens = smoothstep(0.035, -0.045, sd);
			// 第一次进到烟里：按手本身的形状打一次光（柔和的主光从左上照下来 + 轮廓光 + 指缝压暗）→ 指节、拇指、拳面的结构读得出来；
			// 不做高光（用户：高光很怪、像橡胶手），边缘仍然是烟
			if (!shaded && dens > 0.25) {
				shaded = true;
				vec3 n = normal_at(pw, 0.004 * t);
				float ao = clamp(map(pw + n * 0.06 * hs) / (0.06 * hs), 0.0, 1.0);
				ao *= clamp(0.4 + map(pw + n * 0.15 * hs) / (0.15 * hs) * 0.6, 0.0, 1.0);
				float key = smoothstep(0.05, 0.75, dot(n, normalize(vec3(-0.45, 0.75, -0.5))));
				float fres = 1.0 - abs(dot(n, rd));
				vec3 light = vec3(0.13, 0.01, 0.022) * key * (0.6 + 0.4 * nz) + vec3(1.0, 0.34, 0.28) * smoothstep(0.6, 0.86, fres) * 1.2;   // 舞台是红黑的：主体压黑，轮廓光更亮更暖，才从背景里跳出来
				light *= ao * mix(1.0, 0.75, smoothstep(band_z - 2.5, band_z - 0.4, pw.z));
				col += T * light * 0.9;
				T *= 1.0 - 0.6 * dens;
			}
			if (dens > 0.001) {
				float inner = max(-sd, 0.0);
				// 只有最外面一层薄壳发光：正对镜头时视线只穿过薄薄一层（暗），擦着边走时穿过很长一段（亮）→ 每条轮廓、每根叠着的手指都自己勾出红边
				float shellGlow = exp(-inner * 75.0);
				float flick = fbm(lp * 4.0 + vec3(0.0, -clock * 2.2, 0.0));
				vec3 emit = mix(vec3(0.5, 0.03, 0.05), vec3(1.0, 0.32, 0.24), smoothstep(0.5, 0.8, flick)) * shellGlow * 0.5;
				// 表面的咒纹：细细的暗红纹路顺着手流动
				float vn = fbm(lp * 2.6 + vec3(0.0, -clock * 0.35, 0.0));
				emit += vec3(0.7, 0.03, 0.07) * (1.0 - smoothstep(0.0, 0.035, abs(vn - 0.5))) * exp(-inner * 28.0) * 0.6;
				// 攥紧：拳心里有一团红光，从指缝往外透
				emit += vec3(1.0, 0.3, 0.22) * grip * exp(-length(lp - vec3(0.0, 0.75, -0.3)) * 3.5) * 2.5;
				// 越靠近横幅（越深）越暗，穿出的地方烧红
				emit *= mix(1.0, 0.75, smoothstep(band_z - 2.5, band_z - 0.4, pw.z));
				emit += vec3(1.0, 0.2, 0.15) * smoothstep(band_z - 0.3, band_z, pw.z) * 3.0;
				vec3 smoke = vec3(0.004, 0.0, 0.002);
				// 发光按「视线在发光层里走了多远」累积（不乘不透明度）：正面只穿过一层 → 暗，擦边穿过好几层 → 亮
				float a = 1.0 - exp(-dens * dt / hs * 130.0);
				col += T * (a * smoke + emit * dens * dt / hs * 17.0);      // 用户 2026-11-03：手再看清一点 → 薄处透光加一档
				T *= 1.0 - a;
			}
			t += stepv;
		}
	}
	vec4 acc = vec4(col, 1.0 - T);                            // 预乘颜色，下面一层层垫在手后面
	float sz = hs * focal / max(hand_pos.z, 0.5);              // 手的 1 个单位在屏幕上多大（uv 单位）
	float mu = m * focal;                                      // 离剪影多远（uv 单位）
	float sn = texture(noise, UV * vec2(2.5, 1.6) + vec2(0.0, clock * 0.3)).r;
	float tn = texture(noise, vec2(UV.x * 9.0 + (sn - 0.5) * 0.5, UV.y * 0.7 + clock * 1.3)).r;
	float tn2 = texture(noise, vec2(UV.x * 14.0 + 0.3, UV.y * 2.2 + clock * 2.4)).r;
	// 往上窜的咒火：这个像素往下 lift 的地方是不是手 → 火舌从轮廓往上烧（lift 随噪声起伏 = 一条条火舌，近景也看得见）
	if (mu < sz * 0.8 && acc.a < 0.98) {
		float lift = sz * (0.03 + 0.55 * tn * tn);
		vec2 uv2 = uv - vec2((tn2 - 0.5) * 0.6 * lift, lift);
		float m2;
		float t2 = enter(normalize(vec3(uv2, focal)), 0.002, m2);
		float cover = t2 > 0.0 ? 1.0 : 1.0 - smoothstep(0.0, sz * 0.04, m2 * focal);
		float fh = clamp(lift / (sz * 0.58), 0.0, 1.0);
		float fa = cover * smoothstep(0.15, 0.55, tn2 * 0.6 + (1.0 - fh) * 0.7) * (0.5 + 0.5 * (1.0 - fh));
		vec3 fc = texture(lut, vec2(clamp(0.92 - fh * 0.75 + (tn2 - 0.5) * 0.2, 0.0, 1.0), 0.5)).rgb;
		acc.rgb += (1.0 - acc.a) * fc * fa * 1.15;
		acc.a += (1.0 - acc.a) * fa * 0.95;
	}
	// 背光：手后面一圈暗红辉光，把手从横幅上托出来
	float glow = exp(-mu / (sz * 0.1)) * 0.26 * (0.8 + 0.4 * sn);
	acc.rgb += (1.0 - acc.a) * vec3(0.75, 0.04, 0.07) * glow;
	acc.a += (1.0 - acc.a) * glow;
	// 横幅上手臂穿出来的那一圈：红色的裂口 + 往外的裂纹
	vec3 q = rd * (band_z / rd.z);
	float da = arm(q) / band_z;
	float ring = exp(-max(da, 0.0) * 70.0) * step(-0.004, da);
	vec2 rel = q.xy - (hand_pos.xy + (elbow.xy - hand_pos.xy) * (band_z - hand_pos.z) / (elbow.z - hand_pos.z));
	float cr = texture(noise, vec2(atan(rel.y, rel.x) * 1.3, 0.37)).r;
	float cracks = (1.0 - smoothstep(0.0, 0.03, abs(fract(cr * 7.0) - 0.5))) * exp(-max(da, 0.0) * 16.0) * step(0.0, da);
	float pa = clamp(ring * 0.9 + cracks * 0.7, 0.0, 1.0);
	acc.rgb += (1.0 - acc.a) * vec3(1.0, 0.2, 0.16) * (0.6 + 0.6 * ring) * pa;
	acc.a += (1.0 - acc.a) * pa;
	float a = clamp(acc.a, 0.0, 1.0);
	vec3 rgb = acc.rgb + vec3(0.9, 0.12, 0.1) * flash * acc.a * 0.6;      // 捏碎：烧红一下（不提白：白闪打在半透明的烟上会变成一团灰雾）
	COLOR = vec4(rgb / max(a, 0.001), a * alpha);
}" };

	public ColorRect Rect { get; }
	private readonly ShaderMaterial _mat;

	/// <summary>铺满 rect 的一块画布（镜头视野按 rect 的宽高比）。</summary>
	public CrushHand(Control layer, Vector2 pos, Vector2 size)
	{
		_mat = new ShaderMaterial { Shader = HandShader };
		_mat.SetShaderParameter("noise", Paint.Noise);
		_mat.SetShaderParameter("lut", Lut5.Curse);
		_mat.SetShaderParameter("aspect", size.X / size.Y);
		Rect = new ColorRect { Material = _mat, Size = size, MouseFilter = Control.MouseFilterEnum.Ignore, Name = "P5CrushHand" };
		layer.AddChild(Rect);
		Rect.GlobalPosition = pos;
		Fx.Clock(Rect);
	}

	public void Set(string key, float v) => _mat.SetShaderParameter(key, v);

	public void Set(string key, Vector3 v) => _mat.SetShaderParameter(key, v);

	// ───────────────────────── 毒性爆发：巨手捏碎眼神特写 ─────────────────────────
	// 用户 2026-11-03：亚森还是要出来 → 亚森照常召唤现身，他往前出手的那一下（emergeAt）巨手从横幅里冲出来（这只手就是他的咒怨）。
	// 下面的时刻都从手冲出来那一刻算；CrushAfter = 冲出来到捏碎 = 眼神特写碎开 = 游戏结算毒伤 = 所有敌人咒火引爆。

	public const float ArriveAfter = 0.14f, GrabAfter = 0.2f, FistAfter = 0.34f, CrushAfter = 0.62f, FadeTime = 0.45f;   // 用户 2026-11-03：太拖沓 → 压紧
	private const float Focal = 1.25f;

	/// <summary>手的一个姿态（着色器参数）。</summary>
	private readonly record struct Pose(Vector3 Pos, float Tilt, float Roll, float Curl, float Thumb, float Spread);

	// 对照用户拍的照片：张开 = 掌心正对镜头、手指张开、拇指往右下伸；攥拳 = 手腕往前扣，拳面朝镜头，拇指从右下横压过来
	private static readonly Pose Deep = new(new Vector3(0f, 0.15f, 4.95f), -0.15f, 0.1f, 0.2f, 0.1f, 1.0f);
	private static readonly Pose Open = new(new Vector3(0.05f, -0.37f, 1.6f),     // 巨大：手指从横幅上沿伸出来、手腕从下沿露出来
		 -0.15f, 0.1f, 0f, 0f, 1.4f);
	private static readonly Pose Fist = new(new Vector3(0.05f, 0.03f, 2.2f), -1.1f, 0.3f, 1f, 1f, 0.9f);

	private static Pose Lerp(Pose a, Pose b, float k) => new(a.Pos.Lerp(b.Pos, k), Mathf.Lerp(a.Tilt, b.Tilt, k), Mathf.Lerp(a.Roll, b.Roll, k),
		Mathf.Lerp(a.Curl, b.Curl, k), Mathf.Lerp(a.Thumb, b.Thumb, k), Mathf.Lerp(a.Spread, b.Spread, k));

	private void Apply(Pose p)
	{
		Set("hand_pos", p.Pos);
		Set("tilt", p.Tilt);
		Set("roll", p.Roll);
		Set("curl", p.Curl);
		Set("thumb", p.Thumb);
		Set("spread", p.Spread);
		Set("elbow", p.Pos + new Vector3(-0.3f, -0.6f, 6f));     // 手臂笔直伸进画面（藏在手后面），从横幅后面穿出来
	}

	/// <summary>手的局部坐标点 → 屏幕像素（和着色器的 to_local 互逆，用来找拳心在屏幕上的位置）。</summary>
	private Vector2 ToScreen(Pose p, Vector3 local)
	{
		Vector3 v = local;
		float c = Mathf.Cos(p.Tilt), s = Mathf.Sin(p.Tilt);
		v = new Vector3(v.X, c * v.Y - s * v.Z, s * v.Y + c * v.Z);
		c = Mathf.Cos(p.Roll); s = Mathf.Sin(p.Roll);
		v = new Vector3(c * v.X - s * v.Y, s * v.X + c * v.Y, v.Z);
		Vector3 w = p.Pos + v;
		Vector2 size = Rect.Size;
		Vector2 uv = new(w.X / w.Z * Focal, w.Y / w.Z * Focal);
		return Rect.GlobalPosition + new Vector2((uv.X / (size.X / size.Y) + 1f) * 0.5f * size.X, (1f - uv.Y) * 0.5f * size.Y);
	}

	/// <summary>
	/// 毒性爆发的巨手（和 CutIn.Eyes 同一刻开始，放在背景层、眼神特写上面、人物后面）：
	/// 从横幅深处穿出来 → 冲到眼前张开（照片里那个姿势，停一下）→ 手腕往前扣成拳、把横幅攥住（横幅往拳心皱进去）→ 越攥越抖、指缝漏红光 →
	/// CrushAt 捏碎（横幅碎开由 CutIn 自己在 SplitAt 做，Aim 把 SplitAt 设成 CrushAt）→ 拳头往前一顶、散成咒烟。
	/// onFist：攥住横幅那一刻（敌人身上被标记）；onCrush：捏碎那一刻。
	/// </summary>
	public static CrushHand Outbreak(Control back, float emergeAt, System.Action? onFist = null, System.Action? onCrush = null)
	{
		Vector2 screen = Fx.Screen(back);
		CrushHand h = new(back, Vector2.Zero, screen);
		// 层次（用户 2026-11-03：手在眼神横幅下面才有捏爆的感觉）：手从横幅后面伸上来攥住它——横幅中间挡着手，
		// 上下露出手指和手腕；横幅往拳心皱进去，捏碎、碎片飞开才露出后面的拳头。亚森之后才现身，自然盖在最上面
		foreach (Node c in back.GetChildren())
		{
			if (c.Name.ToString().StartsWith("P5CutIn"))
			{
				back.MoveChild(h.Rect, c.GetIndex());
				break;
			}
		}
		h.Set("band_z", 50f);                                         // 手整个在横幅后面：不用再切掉「穿出横幅」的那段
		float EmergeAt = emergeAt, ArriveAt = emergeAt + ArriveAfter, GrabAt = emergeAt + GrabAfter, FistAt = emergeAt + FistAfter, CrushAt = emergeAt + CrushAfter;
		float life = CrushAt + FadeTime;
		bool emerged = false, fisted = false, crushed = false;
		CutIn.Pinch = 0f;
		h.Set("alpha", 0f);
		Fx.Animate(h.Rect, 0, life, t =>
		{
			float s = t * life;
			Pose pose;
			float grip = 0f, flash = 0f, dissolve = 0f, alpha = 1f, pinch = 0f;
			Vector2 shake = Vector2.Zero;
			if (s < EmergeAt)
			{
				pose = Deep;
				alpha = 0f;
			}
			else if (s < ArriveAt)
			{
				// 冲出来：先快后慢，冲过头一点再回来（OutBack）
				float k = (s - EmergeAt) / (ArriveAt - EmergeAt);
				float e = 1f + 2.2f * Mathf.Pow(k - 1f, 3f) + 1.2f * Mathf.Pow(k - 1f, 2f);
				pose = Lerp(Deep, Open, e);
				alpha = Mathf.Clamp(k / 0.25f, 0f, 1f);
				if (!emerged)
				{
					emerged = true;
					Sfx.Play(Sfx.Crack, 0.5f);                         // 从横幅里破出来
					Sfx.Play(Sfx.Curse, 0.45f);
				}
			}
			else if (s < GrabAt)
			{
				// 张开停一下：手指微微往里扣，像要抓过来
				float k = (s - ArriveAt) / (GrabAt - ArriveAt);
				pose = Lerp(Open, Open with { Pos = Open.Pos + new Vector3(0f, 0f, -0.08f), Curl = 0.1f, Spread = 1.45f }, k);
			}
			else if (s < FistAt)
			{
				float k = (s - GrabAt) / (FistAt - GrabAt);
				float e = k < 0.5f ? 4f * k * k * k : 1f - Mathf.Pow(-2f * k + 2f, 3f) / 2f;     // 先蓄后猛
				Pose from = Open with { Pos = Open.Pos + new Vector3(0f, 0f, -0.08f), Curl = 0.1f, Spread = 1.45f };
				pose = Lerp(from, Fist, e);
				grip = 0.3f * e;
				pinch = 0.45f * e;
			}
			else if (s < CrushAt)
			{
				// 攥紧：越攥越抖，往前压一点，指缝的红光越来越亮
				float k = (s - FistAt) / (CrushAt - FistAt);
				pose = Fist with { Pos = Fist.Pos + new Vector3(0f, 0f, -0.1f * k) };
				grip = Mathf.Lerp(0.3f, 1f, k);
				pinch = Mathf.Lerp(0.45f, 0.75f, k);
				float amp = (0.004f + 0.014f * k * k);
				shake = new Vector2(Mathf.Sin(s * 83f), Mathf.Cos(s * 67f)) * amp * screen.Y;
				if (!fisted)
				{
					fisted = true;
					Sfx.Play(Sfx.Slam, 0.35f);
					Sfx.Play(Sfx.SubBoom, 0.3f);
					onFist?.Invoke();
				}
			}
			else
			{
				// 捏碎：拳头往前一顶（0.06s），同时散成咒烟
				float k = (s - CrushAt) / FadeTime;
				float push = Fx.OutCubic(Mathf.Clamp((s - CrushAt) / 0.06f, 0f, 1f));
				pose = Fist with { Pos = Fist.Pos + new Vector3(0f, 0f, -0.1f - 0.35f * push) };
				grip = Mathf.Max(0f, 1.4f - k * 3f);
				flash = Mathf.Max(0f, 1f - k * 6f);
				dissolve = Fx.OutCubic(k);
				alpha = 1f - Fx.InCubic(k);
				if (!crushed)
				{
					crushed = true;
					CutIn.Pinch = 0f;
					Sfx.Play(Sfx.Crush, 0.6f);
					onCrush?.Invoke();
				}
			}
			h.Apply(pose);
			h.Rect.GlobalPosition = shake;
			h.Set("grip", grip);
			h.Set("flash", flash);
			h.Set("dissolve", dissolve);
			h.Set("alpha", alpha);
			if (s >= GrabAt && s < CrushAt)
			{
				CutIn.Pinch = pinch;
				CutIn.PinchAt = h.ToScreen(pose, new Vector3(0f, 0.85f, -0.35f));
				CutIn.ShatterFrom = CutIn.PinchAt;
			}
		});
		Fx.FreeAfter(h.Rect, life + 0.05);
		return h;
	}
}
