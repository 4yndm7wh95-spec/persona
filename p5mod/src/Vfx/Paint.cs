using Godot;

namespace P5Joker;

/// <summary>
/// 手绘贴图（Codex 生图，art/p5/fx/v4）当"材料"用的材质，照原版特效的做法（research/sts2_vfx/recovered/shaders/vfx/common/）：
/// · 颜色：贴图只取亮度（白芯 → 1、红边 → 0.3、墨点 → 0），查 LUT（Lut5）上色；
/// · 出现：沿贴图横向（刀光的走向）扫出来，前沿最亮，不是整张"啪"地贴上；
/// · 消失：按噪声溶解，暗的边先烧掉、白芯最后走，溶解的边缘亮一圈；
/// · 可选的噪声扭曲（火焰 / 能量抖动）。
/// 每个元素两层：正常混合的主体 + 稍大一点的加法发光层。
/// </summary>
public static class Paint
{
	private const string Body = @"
uniform sampler2D lut : repeat_disable, filter_linear;
uniform sampler2D noise : repeat_enable, filter_linear;
uniform float use_lut = 1.0;
uniform float reveal = 1.0;
uniform float feather = 0.1;
uniform float erode = 0.0;
uniform float glow = 1.0;
uniform float wobble = 0.0;
uniform float scroll = 0.0;
uniform vec2 noise_scale = vec2(2.5, 1.2);

void fragment() {
	float n = texture(noise, UV * noise_scale + vec2(scroll, scroll * 0.37)).r;
	vec2 uv = UV + (n - 0.5) * wobble;
	vec4 t = texture(TEXTURE, uv);
	float v = clamp(dot(t.rgb, vec3(0.3, 0.38, 0.32)) * 1.12, 0.0, 1.0);
	float front = 1.0 - smoothstep(reveal - feather, reveal + feather * 0.3, uv.x);
	float lead = (1.0 - smoothstep(0.0, feather * 1.6, abs(uv.x - reveal))) * step(reveal, 0.999);
	float e = n * 0.62 + v * 0.5;
	float er = erode > 0.0 ? smoothstep(erode, erode + 0.1, e) : 1.0;
	float rim = erode > 0.0 ? 1.0 - smoothstep(0.0, 0.05, abs(e - erode)) : 0.0;
	float vv = clamp(v + lead * 0.7 + rim * 0.8, 0.0, 1.0);
	vec3 col = mix(t.rgb, texture(lut, vec2(vv, 0.5)).rgb, use_lut) * glow;
	COLOR = vec4(col, t.a * front * er * COLOR.a);
}";

	private static Shader? _mix, _add;
	private static Texture2D? _noise;

	public static Shader MixShader => _mix ??= new Shader { Code = "shader_type canvas_item;\nrender_mode blend_mix;\n" + Body };
	public static Shader AddShader => _add ??= new Shader { Code = "shader_type canvas_item;\nrender_mode blend_add;\n" + Body };

	/// <summary>无缝噪声（引擎生成，同步）：溶解和扭曲用。</summary>
	public static Texture2D Noise
	{
		get
		{
			if (_noise == null)
			{
				FastNoiseLite fn = new() { NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, Frequency = 0.02f, FractalOctaves = 4, Seed = 5 };
				_noise = ImageTexture.CreateFromImage(fn.GetSeamlessImage(256, 256));
			}
			return _noise;
		}
	}

	/// <summary>一个手绘元素：主体 + 发光层，返回根节点和两份材质（动画里一起改参数）。</summary>
	public sealed class Stroke
	{
		public Node2D Root = null!;
		public ShaderMaterial Main = null!, Glow = null!;

		public void Set(string key, Variant v)
		{
			Main.SetShaderParameter(key, v);
			Glow.SetShaderParameter(key, v);
		}
	}

	public static Stroke? Make(Control layer, string tex, Vector2 at, float rot, Vector2 scale, Texture2D? lut, float glowAlpha = 0.5f,
		float glowScale = 1.06f)
	{
		if (Fx.Tex(tex) is not Texture2D t)
		{
			return null;
		}
		Node2D root = new() { Name = "P5Paint_" + tex };
		layer.AddChild(root);
		root.GlobalPosition = at;
		root.Rotation = rot;
		root.Scale = scale;
		ShaderMaterial Mat(Shader s)
		{
			ShaderMaterial m = new() { Shader = s };
			m.SetShaderParameter("noise", Noise);
			m.SetShaderParameter("use_lut", lut != null ? 1f : 0f);
			if (lut != null)
			{
				m.SetShaderParameter("lut", lut);
			}
			return m;
		}
		Stroke st = new() { Root = root, Main = Mat(MixShader), Glow = Mat(AddShader) };
		root.AddChild(new Sprite2D { Texture = t, Material = st.Glow, Scale = Vector2.One * glowScale, Modulate = new Color(1f, 1f, 1f, glowAlpha) });
		root.AddChild(new Sprite2D { Texture = t, Material = st.Main });
		return st;
	}

	/// <summary>
	/// 扫出来的一笔（刀光）：revealTime 内沿横向扫满，停 hold，erodeTime 内溶解掉；整个过程沿走向微微往前滑（drift，像素）。
	/// </summary>
	public static Stroke? Sweep(Control layer, string tex, Vector2 at, float rot, Vector2 scale, Texture2D? lut,
		float revealTime = 0.06f, float hold = 0.05f, float erodeTime = 0.24f, float drift = 30f, float delay = 0f, float glow = 1.15f)
	{
		Stroke? s = Make(layer, tex, at, rot, scale, lut);
		if (s == null)
		{
			return null;
		}
		s.Set("reveal", 0f);
		s.Set("glow", glow);
		s.Root.Visible = delay <= 0f;
		Vector2 dir = Vector2.FromAngle(rot);
		float total = delay + revealTime + hold + erodeTime;
		Fx.Animate(s.Root, 0, total, t =>
		{
			float time = t * total - delay;
			if (time < 0f)
			{
				return;
			}
			s.Root.Visible = true;
			s.Set("reveal", Mathf.Clamp(Ease5.OutQuint(time / revealTime), 0f, 1f) * 1.12f);
			float e = Mathf.Clamp((time - revealTime - hold) / erodeTime, 0f, 1f);
			s.Set("erode", e <= 0f ? 0f : Mathf.Lerp(0.05f, 1.15f, Mathf.Pow(e, 0.8f)));
			s.Root.GlobalPosition = at + dir * drift * Ease5.OutQuint(Mathf.Min(1f, time / (revealTime + hold + erodeTime)));
		});
		Fx.FreeAfter(s.Root, total + 0.05);
		return s;
	}

	/// <summary>弹出来的一张（漫画爆框、枪口火焰）：2 帧内从 pop 倍缩到 1，停 hold，溶解掉。</summary>
	public static Stroke? Pop(Control layer, string tex, Vector2 at, float rot, float scale, Texture2D? lut, float hold = 0.05f,
		float erodeTime = 0.18f, float pop = 1.35f, float delay = 0f, float glow = 1f)
	{
		Stroke? s = Make(layer, tex, at, rot, Vector2.One * scale * pop, lut, 0.35f);
		if (s == null)
		{
			return null;
		}
		s.Set("glow", glow);
		s.Root.Visible = delay <= 0f;
		float total = delay + 0.07f + hold + erodeTime;
		Fx.Animate(s.Root, 0, total, t =>
		{
			float time = t * total - delay;
			if (time < 0f)
			{
				return;
			}
			s.Root.Visible = true;
			float k = Mathf.Lerp(pop, 1f, Ease5.OutQuint(Mathf.Min(1f, time / 0.07f)));
			s.Root.Scale = Vector2.One * scale * k * (1f + 0.06f * Mathf.Max(0f, time - 0.07f));
			float e = Mathf.Clamp((time - 0.07f - hold) / erodeTime, 0f, 1f);
			s.Set("erode", e <= 0f ? 0f : Mathf.Lerp(0.05f, 1.15f, e));
		});
		Fx.FreeAfter(s.Root, total + 0.05);
		return s;
	}
}
