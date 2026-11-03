using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 毒性爆发的「巨手捏碎眼神特写」（用户 2026-11-03 创意：手本身就是特效，不用贴图）。
/// 手的形状在着色器里用有向距离场（SDF）实时算，掌心朝镜头：手掌（圆角矩形）+ 四指（三节胶囊）+ 大拇指 + 往下收细的手腕。
/// 透视不靠画：手指往镜头弯时，正面看每一节都按 cos(弯曲角) 变短，弯过 90° 就折回来盖在掌心前面（攥拳）；
/// 整只手从远处小小的一团猛地放大到半个屏幕 = 朝镜头伸过来；手腕往下变细、化成烟 = 手臂伸进画面深处。
/// · 剪影里面：翻滚的黑色咒烟 + 暗红筋络；
/// · 剪影外面：往上窜的黑红咒火（往下找剪影 + 噪声，同亚森蓝焰的做法），几何边缘全被火吃掉；
/// · grip：指缝里透出红光；flash：捏碎那一下爆亮。
/// </summary>
public sealed class CrushHand
{
	private static Shader? _shader;

	private static Shader HandShader => _shader ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D noise : repeat_enable, filter_linear;
uniform sampler2D lut : repeat_disable, filter_linear;
uniform float clock = 0.0;
uniform float curl = 0.0;      // 0 张开 → 1 攥拳
uniform float spread = 1.0;    // 手指张开的角度
uniform float thumb = 0.0;     // 0 大拇指张开 → 1 横压在四指前面
uniform float grip = 0.0;      // 指缝透出的红光
uniform float flash = 0.0;
uniform float alpha = 1.0;
uniform float aspect = 0.857;
uniform float zoom = 1.0;

float sd_capsule(vec2 p, vec2 a, vec2 b, float r) {
	vec2 pa = p - a, ba = b - a;
	float h = clamp(dot(pa, ba) / max(dot(ba, ba), 1e-5), 0.0, 1.0);
	return length(pa - ba * h) - r;
}
float sd_rbox(vec2 p, vec2 b, float r) {
	vec2 q = abs(p) - b + r;
	return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
}
float smin(float a, float b, float k) {
	float h = clamp(0.5 + 0.5 * (b - a) / k, 0.0, 1.0);
	return mix(b, a, h) - k * h * (1.0 - h);
}
vec2 rot(vec2 v, float a) { float c = cos(a), s = sin(a); return vec2(c * v.x - s * v.y, s * v.x + c * v.y); }

// 一根手指（正面看）：三节沿手指方向排开，每节投影长度 = 节长 × cos(累计弯曲角)；弯过 90° 往回折。
// 朝镜头弯的那几节看起来更粗（离镜头近）。
float finger(vec2 p, vec2 base, float ang, float L, float r, float c, inout float ln) {
	vec2 dir = rot(vec2(0.0, 1.0), ang);
	float a1 = 1.25 * c, a2 = a1 + 1.45 * c, a3 = a2 + 1.1 * c;
	vec2 j1 = base + dir * L * 0.46 * cos(a1);
	vec2 j2 = j1 + dir * L * 0.30 * cos(a2);
	vec2 tip = j2 + dir * L * 0.24 * cos(a3);
	float rr = r * (1.0 + 0.22 * c);
	float s1 = sd_capsule(p, base, j1, rr);
	float s2 = sd_capsule(p, j1, j2, rr * 0.95);
	float s3 = sd_capsule(p, j2, tip, rr * 0.88);
	// 弯起来以后各节互相压着：每节的轮廓都是一条线（伸直时不画，免得像一节节的虫子）
	float joint = (1.0 - smoothstep(0.25, 0.6, c)) * 0.08;
	ln = min(ln, abs(s2) + joint);
	ln = min(ln, abs(s3) + joint);
	return smin(smin(s1, s2, 0.03), s3, 0.03);
}

// 记一个部件：并进剪影，同时记下每个部件自己的轮廓（ln = 离最近的部件边缘多远）→ 剪影里面画出指缝、折回来的指节、拇指的线
void part(inout float d, inout float ln, float pd, float k) { ln = min(ln, abs(pd)); d = smin(d, pd, k); }

float hand_ln(vec2 p, out float ln) {
	ln = 1e3;
	// 手掌：攥拳时稍微往前倾，正面看矮一点
	float palm = sd_rbox(p, vec2(0.47, 0.5 - 0.06 * curl), 0.22);
	float d = palm;
	ln = abs(palm) + 0.025;                                   // 掌缘那条线弱一点（只在手指根部若隐若现）
	// 手腕 / 前臂：往下收细（伸进画面深处）
	d = smin(d, sd_capsule(p, vec2(0.0, -0.35), vec2(0.0, -1.35), 0.29), 0.2);
	float sp = spread * (1.0 - curl * 0.8);
	float c = curl;
	float top = 0.42 - 0.06 * curl;
	part(d, ln, finger(p, vec2(-0.33, top - 0.03), 0.26 * sp, 0.86, 0.105, c, ln), 0.04);
	part(d, ln, finger(p, vec2(-0.11, top + 0.02), 0.08 * sp, 0.98, 0.112, c, ln), 0.04);
	part(d, ln, finger(p, vec2(0.12, top), -0.07 * sp, 0.92, 0.108, c, ln), 0.04);
	part(d, ln, finger(p, vec2(0.33, top - 0.07), -0.25 * sp, 0.72, 0.094, c, ln), 0.04);
	// 大拇指：张开时往外上方斜伸，攥拳时横压在四指前面
	vec2 tb = vec2(0.36, -0.22);
	vec2 tj = mix(tb + vec2(0.3, 0.2), tb + vec2(0.06, 0.26), thumb);
	vec2 tt = mix(tb + vec2(0.5, 0.48), tb + vec2(-0.44, 0.42), thumb);
	part(d, ln, smin(sd_capsule(p, tb, tj, 0.14), sd_capsule(p, tj, tt, 0.12), 0.04), 0.06);
	return d;
}
float hand(vec2 p) { float ln; return hand_ln(p, ln); }

void fragment() {
	vec2 p = (UV - vec2(0.5, 0.55)) * vec2(aspect, -1.0) * 3.2 / zoom;
	float n1 = texture(noise, UV * vec2(1.6, 1.3) + vec2(0.0, clock * 0.22)).r;
	float n2 = texture(noise, UV * vec2(3.0, 2.4) + vec2(clock * 0.05, clock * 0.4)).r;
	float ln;
	float d = hand_ln(p + (vec2(n1, n2) - 0.5) * 0.07, ln);
	// 往上窜的咒火：往下找剪影（离剪影越近越亮），火舌左右摆
	float src = 0.0;
	for (int i = 1; i <= 12; i++) {
		float f = float(i) / 12.0;
		float sway = (texture(noise, vec2(UV.x * 2.2 + f * 0.3, UV.y * 0.8 + clock * 0.5)).r - 0.5) * 0.35 * f;
		float dd = hand(p - vec2(sway, f * 0.6));
		src = max(src, step(dd, 0.0) * (1.0 - f));
	}
	// 四周也贴着一层薄火（侧面、下面不光秃）
	src = max(src, (1.0 - smoothstep(0.0, 0.12, d)) * 0.7);
	float tn = texture(noise, vec2(UV.x * 6.0 + (n1 - 0.5) * 0.5, UV.y * 1.1 - clock * 1.5)).r;
	float tn2 = texture(noise, vec2(UV.x * 13.0 + 0.3, UV.y * 2.2 - clock * 2.4)).r;
	float heat = src + (tn - 0.5) * 0.85 + (tn2 - 0.5) * 0.35;
	float flame = step(0.0, d) * smoothstep(0.1, 0.42, heat);
	float inside = 1.0 - smoothstep(-0.012, 0.012, d);
	// 里面：大尺度流动的黑色咒烟（域扭曲），越靠边缘越红（体积感）
	vec2 wuv = UV * 1.4 + (vec2(n1, n2) - 0.5) * 0.35 + vec2(0.0, -clock * 0.12);
	float smoke = texture(noise, wuv).r;
	float veins = 1.0 - smoothstep(0.0, 0.05, abs(fract(smoke * 3.0) - 0.5));
	float edge = 1.0 - smoothstep(0.0, 0.2, -d);
	vec3 body = mix(vec3(0.01, 0.0, 0.006), vec3(0.16, 0.01, 0.035), smoothstep(0.3, 0.8, smoke) * 0.7 + edge * 0.45);
	body += vec3(0.7, 0.04, 0.09) * veins * 0.22 * (0.5 + n2);
	// 指缝 / 指节 / 拇指的线：发红光的裂缝（拳头靠它才有结构）
	float crease = (1.0 - smoothstep(0.0, 0.022, ln)) * inside * smoothstep(-0.02, -0.06, d);
	float creaseGlow = (1.0 - smoothstep(0.0, 0.07, ln)) * inside * smoothstep(-0.02, -0.08, d);
	body = mix(body, vec3(0.0), creaseGlow * 0.5);
	body += vec3(1.0, 0.12, 0.14) * crease * (0.75 + 0.5 * tn2);
	// 握紧：掌心前方（指缝）透出红光
	float core = exp(-length((p - vec2(0.0, 0.28)) * vec2(1.1, 1.6)) * 2.6) * grip;
	body += vec3(1.0, 0.25, 0.22) * core * creaseGlow * 2.5 + vec3(0.6, 0.05, 0.08) * core * 0.6;
	// 火：贴着手的地方亮红，往上变暗红，火舌尖化成黑烟（黑芯红边的咒火）
	float hot = clamp(heat, 0.0, 1.0);
	vec3 fl = texture(lut, vec2(clamp(0.25 + hot * 0.75, 0.0, 1.0), 0.5)).rgb;
	float rim = (1.0 - smoothstep(0.0, 0.03, abs(d))) * (0.5 + 0.5 * tn);
	vec3 col = mix(fl, body, inside) + vec3(1.0, 0.15, 0.16) * rim * 0.7;
	col = mix(col, vec3(1.0, 0.82, 0.78), flash * 0.75);
	float fa = flame * smoothstep(0.1, 0.35, heat) * (0.6 + 0.4 * hot);
	float a = max(inside, fa) + core * 0.4 * (1.0 - inside);
	// 手腕往下化成烟：越往下越透明
	a *= smoothstep(-1.35, -0.55, p.y + (n1 - 0.5) * 0.4);
	COLOR = vec4(col, clamp(a, 0.0, 1.0) * alpha);
}" };

	public ColorRect Rect { get; }
	private readonly ShaderMaterial _mat;
	private readonly Vector2 _center;

	public CrushHand(Control layer, Vector2 center, float height)
	{
		_mat = new ShaderMaterial { Shader = HandShader };
		_mat.SetShaderParameter("noise", Paint.Noise);
		_mat.SetShaderParameter("lut", Lut5.Curse);
		Vector2 size = new(height * 0.857f, height);
		_mat.SetShaderParameter("aspect", size.X / size.Y);
		_center = center;
		Rect = new ColorRect { Material = _mat, Size = size, MouseFilter = Control.MouseFilterEnum.Ignore, Name = "P5CrushHand", PivotOffset = size * new Vector2(0.5f, 0.55f) };
		layer.AddChild(Rect);
		Rect.GlobalPosition = center - Rect.PivotOffset;
		Fx.Clock(Rect);
	}

	public void Set(string key, float v) => _mat.SetShaderParameter(key, v);

	/// <summary>
	/// 一次完整的「伸出 → 抓住 → 攥紧颤抖 → 捏碎 → 散掉」。crushAt：从现在起多少秒捏碎（onCrush 在那一刻调用）。
	/// 伸出 0.3s、抓住 0.18s，剩下到 crushAt 的时间都在攥紧颤抖（蓄力）。
	/// </summary>
	public static CrushHand Play(Control layer, Vector2 center, float height, float crushAt, Action? onCrush = null)
	{
		CrushHand h = new(layer, center, height);
		const float reach = 0.3f, grab = 0.18f, fade = 0.45f;
		float squeeze = Mathf.Max(0.1f, crushAt - reach - grab);
		float life = crushAt + fade;
		bool crushed = false;
		h.Set("alpha", 0f);
		Fx.Animate(h.Rect, 0, life, t =>
		{
			float s = t * life;
			float zoom, curl, thumb, grip = 0f, flash = 0f, alpha = 1f, spread = 1.15f;
			Vector2 jitter = Vector2.Zero;
			if (s < reach)
			{
				float k = Fx.OutCubic(s / reach);
				zoom = Mathf.Lerp(0.3f, 1.04f, k);
				alpha = Mathf.Clamp(s / 0.12f, 0f, 1f);
				curl = 0f; thumb = 0f;
			}
			else if (s < reach + grab)
			{
				float k = Fx.OutCubic((s - reach) / grab);
				zoom = Mathf.Lerp(1.04f, 1f, k);
				curl = Mathf.Lerp(0f, 0.62f, k);
				thumb = Mathf.Lerp(0f, 0.55f, k);
				spread = Mathf.Lerp(1.15f, 0.9f, k);
			}
			else if (s < crushAt)
			{
				float k = (s - reach - grab) / squeeze;
				zoom = 1f - 0.02f * k;
				curl = Mathf.Lerp(0.62f, 0.8f, k);
				thumb = Mathf.Lerp(0.55f, 0.75f, k);
				spread = 0.9f;
				grip = 0.35f + 0.5f * k;
				float amp = 3f + 6f * k;                                 // 越攥越抖
				jitter = new Vector2(Mathf.Sin(s * 71f), Mathf.Cos(s * 57f)) * amp;
			}
			else
			{
				float k = (s - crushAt) / fade;
				zoom = 1f + 0.06f * Fx.OutCubic(Mathf.Min(1f, k * 4f)) - 0.03f * k;
				curl = 1f; thumb = 1f; spread = 0.9f;
				flash = Mathf.Max(0f, 1f - k * 5f);
				grip = Mathf.Max(0f, 1.4f - k * 2.5f);
				alpha = 1f - Fx.InCubic(k);
				if (!crushed)
				{
					crushed = true;
					onCrush?.Invoke();
				}
			}
			h.Rect.Scale = Vector2.One * zoom;
			h.Rect.GlobalPosition = h._center - h.Rect.PivotOffset + jitter;
			h.Set("curl", curl);
			h.Set("thumb", thumb);
			h.Set("spread", spread);
			h.Set("grip", grip);
			h.Set("flash", flash);
			h.Set("alpha", alpha);
		});
		Fx.FreeAfter(h.Rect, life + 0.05);
		return h;
	}
}
