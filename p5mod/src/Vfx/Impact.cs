using Godot;

namespace P5Joker;

/// <summary>命中的分量：决定顿帧、震屏、全屏冲击帧有多重。</summary>
public enum Weight
{
	Light,      // 普通一刀 / 一枪
	Medium,     // 打掉一截血
	Heavy,      // 重击、大招
	Kill,       // 击杀
}

/// <summary>
/// 打击确认感（第四版，用户 2026-11-02：要有视觉冲击力、有确认感）。不画任何形状，全是引擎 / 着色器实时处理：
/// · 顿帧：原版 NGame.DoHitStop（Stage.HitStop），全场时间骤停后缓回；
/// · 定向震屏：沿攻击方向推一下（Stage.ShakeDir），比随机抖更像"打进去"；
/// · 受击白闪 + 击退：目标本体 HDR 提亮成白剪影，转红，再回原色；身体被打得往后一顿再弹回（击杀不做，交给死亡动画）；
/// · 全屏冲击帧（Heavy / Kill）：把画面瞬间变成 P5 式红 / 黑 / 白三色漫画帧（亮部白、中间调红带网点、暗部黑），
///   持续 2～5 帧，接着色散 + 指向命中点的径向模糊衰减掉。
/// 所有计时用真实时间（不受顿帧影响），所以全场停住的那一刻冲击帧照样在闪。
/// </summary>
public static class Impact
{
	private record struct Spec(int Stop, int Shake, float Flash, float Knock, float Ink, float InkHold, float Chroma, float Radial, float Life);

	private static Spec For(Weight w) => w switch
	{
		Weight.Light => new Spec(1, 1, 2.2f, 16f, 0f, 0f, 5f, 0.10f, 0.14f),
		Weight.Medium => new Spec(2, 2, 2.6f, 28f, 0f, 0f, 9f, 0.25f, 0.2f),
		Weight.Heavy => new Spec(3, 3, 3.0f, 42f, 1f, 0.07f, 14f, 0.55f, 0.32f),
		_ => new Spec(4, 4, 0f, 0f, 1f, 0.16f, 20f, 0.85f, 0.5f),
	};

	/// <summary>伤害占目标最大生命的比例 → 分量。</summary>
	public static Weight Of(int damage, int maxHp, bool killed)
	{
		if (killed)
		{
			return Weight.Kill;
		}
		float r = maxHp > 0 ? (float)damage / maxHp : 0f;
		return r >= 0.22f ? Weight.Heavy : r >= 0.09f ? Weight.Medium : Weight.Light;
	}

	/// <summary>
	/// 命中确认。layer = 特效层；body = 被打的人物（敌人的 Visuals，可为空）；at = 命中点（全局）；dir = 攻击方向（从攻击者指向目标）。
	/// </summary>
	public static void Confirm(Control layer, Node2D? body, Vector2 at, Vector2 dir, Weight w)
	{
		Spec s = For(w);
		dir = dir.LengthSquared() > 0.001f ? dir.Normalized() : Vector2.Right;
		Stage.HitStop?.Invoke(s.Stop);
		if (Stage.ShakeDir != null)
		{
			Stage.ShakeDir(s.Shake, Mathf.RadToDeg(dir.Angle()));
		}
		else
		{
			Fx.Shake(s.Shake);
		}
		if (body != null && GodotObject.IsInstanceValid(body) && w != Weight.Kill)
		{
			FlashBody(body, s.Flash);
			Knock(body, dir * s.Knock);
		}
		ScreenFrame(layer, at, dir, s);
	}

	// ───────────────────────── 受击白闪 / 击退 ─────────────────────────

	private static void FlashBody(Node2D body, float peak)
	{
		Color baseColor = body.Modulate;
		Tween tw = body.CreateTween().SetIgnoreTimeScale(true);
		body.Modulate = new Color(peak, peak, peak, baseColor.A);
		tw.TweenProperty(body, "modulate", new Color(1.9f, 0.55f, 0.6f, baseColor.A), 0.06);
		tw.TweenProperty(body, "modulate", baseColor, 0.16).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
	}

	private static void Knock(Node2D body, Vector2 offset)
	{
		if (offset.LengthSquared() < 1f)
		{
			return;
		}
		Vector2 home = body.Position;
		Tween tw = body.CreateTween().SetIgnoreTimeScale(true);
		body.Position = home + offset;
		tw.TweenInterval(0.05);
		tw.TweenProperty(body, "position", home, 0.32).SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
	}

	// ───────────────────────── 全屏冲击帧 ─────────────────────────

	private static Shader? _frame;

	/// <summary>
	/// 读屏幕：ink = P5 三色漫画帧的强度；chroma = 沿 dir 的色散像素；radial = 指向 center 的径向模糊；flash = 整体提白。
	/// </summary>
	public static Shader FrameShader => _frame ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform float ink = 0.0;
uniform float chroma = 0.0;
uniform vec2 dir = vec2(1.0, 0.0);
uniform float radial = 0.0;
uniform vec2 center = vec2(0.5);
uniform float flash = 0.0;
uniform vec3 red = vec3(0.92, 0.05, 0.09);

vec3 tap(vec2 uv, vec2 off) {
	return vec3(texture(screen_tex, uv + off).r, texture(screen_tex, uv).g, texture(screen_tex, uv - off).b);
}

void fragment() {
	vec2 uv = SCREEN_UV;
	vec2 off = dir * chroma * SCREEN_PIXEL_SIZE;
	vec3 col = vec3(0.0);
	float wsum = 0.0;
	for (int i = 0; i < 12; i++) {
		float t = float(i) / 11.0;
		float w = 1.0 - 0.6 * t;
		vec2 u = center + (uv - center) * (1.0 - radial * 0.09 * t);
		col += tap(u, off * (1.0 + t)) * w;
		wsum += w;
	}
	col /= wsum;

	// P5 三色帧：亮部白、中间调红（45° 网点）、暗部黑；命中点附近更亮，像被闪光打穿
	float lum = dot(col, vec3(0.299, 0.587, 0.114));
	float near = 1.0 - smoothstep(0.0, 0.07, distance(uv * vec2(1.7778, 1.0), center * vec2(1.7778, 1.0)));
	lum = lum + near * 0.6;
	vec2 g = FRAGCOORD.xy * mat2(vec2(0.7071, -0.7071), vec2(0.7071, 0.7071)) / 9.0;
	float dotr = length(fract(g) - 0.5);
	float dots = step(dotr, mix(0.12, 0.62, smoothstep(0.13, 0.5, lum)));
	vec3 inkCol = lum > 0.55 ? vec3(1.0, 0.97, 0.95) : lum > 0.13 ? mix(red * 0.62, red, dots) : vec3(0.02, 0.0, 0.01);
	col = mix(col, inkCol, ink);
	COLOR = vec4(col + flash, 1.0);
}" };

	private static void ScreenFrame(Control layer, Vector2 at, Vector2 dir, Spec s)
	{
		if (s.Chroma <= 0f && s.Ink <= 0f)
		{
			return;
		}
		Vector2 screen = Fx.Screen(layer);
		ShaderMaterial mat = new() { Shader = FrameShader };
		ColorRect rect = new()
		{
			Material = mat,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Size = screen,
			Name = "P5ImpactFrame",
		};
		layer.AddChild(rect);
		rect.GlobalPosition = Vector2.Zero;
		mat.SetShaderParameter("center", new Vector2(at.X / screen.X, at.Y / screen.Y));
		mat.SetShaderParameter("dir", dir);

		void Set(float ink, float chroma, float radial, float flash)
		{
			mat.SetShaderParameter("ink", ink);
			mat.SetShaderParameter("chroma", chroma);
			mat.SetShaderParameter("radial", radial);
			mat.SetShaderParameter("flash", flash);
		}

		// 第 1 帧：白闪；然后三色帧停 InkHold；然后色散 + 径向模糊在 Life 内衰减
		Set(s.Ink, s.Chroma, s.Radial, s.Ink > 0f ? 0.3f : 0f);
		Tween tw = rect.CreateTween().SetIgnoreTimeScale(true);
		tw.TweenInterval(1.0 / 30.0);
		tw.TweenCallback(Callable.From(() => Set(s.Ink, s.Chroma, s.Radial, 0f)));
		if (s.InkHold > 0f)
		{
			tw.TweenInterval(s.InkHold);
		}
		tw.TweenMethod(Callable.From<float>(t =>
		{
			float k = 1f - t;
			Set(s.Ink * Mathf.Max(0f, 1f - t * 4f), s.Chroma * k * k, s.Radial * k * k, 0f);
		}), 0f, 1f, s.Life);
		tw.TweenCallback(Callable.From(rect.QueueFree));
	}
}
