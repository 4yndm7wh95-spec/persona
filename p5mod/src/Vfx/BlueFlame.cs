using Godot;

namespace P5Joker;

/// <summary>
/// P5R 召唤 Persona 的蓝焰（用户 2026-11-03：亚森出现和消失要伴随 P5R 那种蓝焰，这是 Persona 的特色）。
/// 用的就是 P5R 原版召唤循环蓝焰 BE_SYOLOOP 的火舌序列帧（research/p5r/ghost_blueflame/textures_png/bes_syo_fkomaB12kf2.dds.png，
/// 6 列 × 2 行共 12 帧：上一行火舌冒出 → 最旺 → 收，下一行散成细碎余烬）。原版贴图是白色遮罩、蓝色来自材质，这里按「离边缘多深」查 LUT
/// （Lut5.SummonFlame：边缘天蓝 → 芯青白）上色、加法发光。每缕火舌：12 帧播一遍、往上窜、轻微摆动。
/// </summary>
public static class BlueFlame
{
	private static Shader? _shader;
	private static ShaderMaterial? _mat;

	private static ShaderMaterial Material
	{
		get
		{
			if (_mat == null)
			{
				_shader ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform sampler2D lut : repeat_disable, filter_linear;
uniform float glow = 1.15;                                   // 火舌重叠处加法会叠白，别太亮
// 原版贴图是纯白遮罩（不透明处 RGB 全白），按亮度查表只会查到白色：改按「离火舌边缘多深」上色——周围一圈采样取平均，
// 边缘 → 蓝，越往里越亮，芯 → 青白（P5R 召唤蓝焰：亮白的芯、外面一圈蓝）
void fragment() {
	vec4 t = texture(TEXTURE, UV);
	vec2 px = TEXTURE_PIXEL_SIZE * 3.0;
	float b = 0.0;
	b += texture(TEXTURE, UV + vec2(px.x, 0.0)).a;
	b += texture(TEXTURE, UV - vec2(px.x, 0.0)).a;
	b += texture(TEXTURE, UV + vec2(0.0, px.y)).a;
	b += texture(TEXTURE, UV - vec2(0.0, px.y)).a;
	b += texture(TEXTURE, UV + px).a;
	b += texture(TEXTURE, UV - px).a;
	b += texture(TEXTURE, UV + vec2(px.x, -px.y)).a;
	b += texture(TEXTURE, UV + vec2(-px.x, px.y)).a;
	b /= 8.0;
	float v = t.a * mix(0.3, 1.0, b * b * b);
	vec3 c = texture(lut, vec2(v, 0.5)).rgb * glow;
	COLOR = vec4(c, t.a * COLOR.a);
}" };
				_mat = new ShaderMaterial { Shader = _shader };
				_mat.SetShaderParameter("lut", Lut5.SummonFlame);
			}
			return _mat;
		}
	}

	/// <summary>一缕火舌：at 处冒出，life 秒内播完 12 帧、往上窜 rise 像素。scale 1 ≈ 85×256 像素一格的原尺寸。</summary>
	public static void Tongue(Control layer, Vector2 at, float scale, float life = 0.5f, float rise = 60f, float delay = 0f, float alpha = 1f)
	{
		if (Fx.Tex("persona_flame_strip") is not Texture2D tex)
		{
			return;
		}
		if (delay > 0f)
		{
			Fx.After(layer, delay, () => Tongue(layer, at, scale, life, rise, 0f, alpha));
			return;
		}
		Sprite2D s = new()
		{
			Texture = tex, Hframes = 6, Vframes = 2, Frame = 0, Material = Material, FlipH = Fx.Rng.Next(2) == 0,
			Scale = Vector2.One * scale, Offset = new Vector2(0f, -100f), Name = "P5BlueFlame",
			Modulate = new Color(1f, 1f, 1f, alpha),
		};
		layer.AddChild(s);
		s.GlobalPosition = at;
		float sway = Fx.Rand(-0.18f, 0.18f);
		Fx.Animate(s, 0, life, t =>
		{
			s.Frame = Mathf.Min(11, (int)(t * 12f));
			s.GlobalPosition = at + new Vector2(Mathf.Sin(t * 6f) * 6f * scale, -rise * Ease5.OutQuint(t));
			s.Rotation = sway * t;
		});
		Fx.FreeAfter(s, life + 0.05);
	}

	/// <summary>
	/// 把一块区域包在蓝焰里：duration 秒内冒 count 缕火舌；fromBottom = 先从底下冒、往上爬（现身），否则整块随机（退场 / 爆开）。
	/// center / size = 区域（人物的包围盒），scale = 火舌大小。
	/// </summary>
	public static void Engulf(Control layer, Vector2 center, Vector2 size, int count, float duration, float scale, bool fromBottom)
	{
		for (int i = 0; i < count; i++)
		{
			float u = (float)i / Mathf.Max(1, count - 1);
			float delay = u * duration * Fx.Rand(0.7f, 1f);
			float x = center.X + Fx.Rand(-0.5f, 0.5f) * size.X * (0.55f + 0.45f * Fx.Rand(0f, 1f));
			float yu = fromBottom ? Mathf.Clamp(1f - u + Fx.Rand(-0.15f, 0.15f), 0f, 1f) : Fx.Rand(0f, 1f);
			float y = center.Y + (yu - 0.5f) * size.Y;
			Tongue(layer, new Vector2(x, y), scale * Fx.Rand(0.75f, 1.25f), Fx.Rand(0.4f, 0.6f), Fx.Rand(40f, 110f) * scale, delay);
		}
	}
}
