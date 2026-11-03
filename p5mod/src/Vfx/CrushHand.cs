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
	float t = enter(rd, 0.09 * hs, m);
	vec3 col = vec3(0.0);
	float T = 1.0;                                            // 透过率（从前往后累积）
	if (t > 0.0) {
		// 手形的咒烟：沿视线一步步走进去。密度 = 手的距离场被 3D 噪声撕毛（边缘往上飘）；
		// 越靠近表面越透红光（薄的地方：手指边缘、指缝 → 叠在掌心前面的手指自己勾出一道红边），越深越黑
		float dt = 0.022 * hs;
		bool shaded = false;
		t += fract(sin(dot(UV * 1000.0, vec2(12.9898, 78.233)) + clock) * 43758.5453) * dt;     // 起点抖一下：不然固定步长切出一圈圈木纹
		for (int i = 0; i < 40; i++) {
			vec3 pw = rd * t;
			if (pw.z > band_z || T < 0.02) break;
			vec3 lp = to_local(pw);
			float s = map(pw) / hs;
			if (s > 0.12) { t += (s - 0.08) * hs; continue; }
			vec3 fp = lp * 5.0 + vec3(0.0, -clock * 1.4, clock * 0.3);
			float nz = fbm(fp);
			float sd = s + (nz - 0.5) * 0.12;
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
				vec3 light = vec3(0.26, 0.022, 0.042) * key * (0.6 + 0.4 * nz) + vec3(0.9, 0.07, 0.1) * smoothstep(0.6, 0.9, fres) * 0.5;
				light *= ao * mix(1.0, 0.3, smoothstep(band_z - 2.5, band_z - 0.4, pw.z));
				col += T * light * 0.9;
				T *= 1.0 - 0.6 * dens;
			}
			if (dens > 0.001) {
				float inner = max(-sd, 0.0);
				// 只有最外面一层薄壳发光：正对镜头时视线只穿过薄薄一层（暗），擦着边走时穿过很长一段（亮）→ 每条轮廓、每根叠着的手指都自己勾出红边
				float shellGlow = exp(-inner * 75.0);
				float flick = fbm(lp * 4.0 + vec3(0.0, -clock * 2.2, 0.0));
				vec3 emit = mix(vec3(0.45, 0.015, 0.045), vec3(1.0, 0.15, 0.11), smoothstep(0.5, 0.8, flick)) * shellGlow * 0.5;
				// 表面的咒纹：细细的暗红纹路顺着手流动
				float vn = fbm(lp * 2.6 + vec3(0.0, -clock * 0.35, 0.0));
				emit += vec3(0.7, 0.03, 0.07) * (1.0 - smoothstep(0.0, 0.035, abs(vn - 0.5))) * exp(-inner * 28.0) * 0.6;
				// 攥紧：拳心里有一团红光，从指缝往外透
				emit += vec3(1.0, 0.3, 0.22) * grip * exp(-length(lp - vec3(0.0, 0.75, -0.3)) * 3.5) * 2.5;
				// 越靠近横幅（越深）越暗，穿出的地方烧红
				emit *= mix(1.0, 0.35, smoothstep(band_z - 2.5, band_z - 0.4, pw.z));
				emit += vec3(1.0, 0.2, 0.15) * smoothstep(band_z - 0.3, band_z, pw.z) * 3.0;
				vec3 smoke = vec3(0.012, 0.0, 0.004);
				// 发光按「视线在发光层里走了多远」累积（不乘不透明度）：正面只穿过一层 → 暗，擦边穿过好几层 → 亮
				float a = 1.0 - exp(-dens * dt / hs * 130.0);
				col += T * (a * smoke + emit * dens * dt / hs * 13.0);
				T *= 1.0 - a;
			}
			t += dt;
		}
	}
	float handA = 1.0 - T;
	// 剪影外的咒火：贴着剪影的一圈，被往上卷的噪声撕成火舌
	float sn = texture(noise, UV * vec2(2.5, 1.6) + vec2(0.0, clock * 0.3)).r;
	float tn = texture(noise, vec2(UV.x * 7.0 + (sn - 0.5) * 0.4, UV.y * 1.4 + clock * 1.5)).r;
	float tn2 = texture(noise, vec2(UV.x * 15.0 + 0.3, UV.y * 2.5 + clock * 2.3)).r;
	float halo = 1.0 - smoothstep(0.0, 0.045, m);
	float heat = halo * (0.55 + 0.9 * tn) + (tn2 - 0.5) * 0.3 - 0.3;
	float flame = smoothstep(0.05, 0.45, heat) * T;
	vec3 fc = texture(lut, vec2(clamp(0.25 + heat * 0.7, 0.0, 1.0), 0.5)).rgb;
	// 横幅上手臂穿出来的那一圈：红色的裂口 + 往外的裂纹
	vec3 q = rd * (band_z / rd.z);
	float da = arm(q) / band_z;
	float ring = exp(-max(da, 0.0) * 70.0) * step(-0.004, da);
	vec2 rel = q.xy - (hand_pos.xy + (elbow.xy - hand_pos.xy) * (band_z - hand_pos.z) / (elbow.z - hand_pos.z));
	float cr = texture(noise, vec2(atan(rel.y, rel.x) * 1.3, 0.37)).r;
	float cracks = (1.0 - smoothstep(0.0, 0.03, abs(fract(cr * 7.0) - 0.5))) * exp(-max(da, 0.0) * 16.0) * step(0.0, da);
	float pa = clamp(ring * 0.9 + cracks * 0.7, 0.0, 1.0) * T;
	vec3 rgb = col + fc * flame * 0.9 + vec3(1.0, 0.2, 0.16) * (0.6 + 0.6 * ring) * pa;
	float a = clamp(handA + flame * 0.9 + pa, 0.0, 1.0);
	rgb = mix(rgb, vec3(1.0, 0.85, 0.8) * a, flash * 0.7);
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
}
