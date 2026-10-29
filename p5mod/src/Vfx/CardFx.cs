using Godot;

namespace P5Joker;

/// <summary>
/// 出牌特效（2026-10-29 重做，docs/p5/FX_PLAN.md：主体是光、粒子、空间扭曲，实时渲染）。
/// 第一批：防御（Guard）、中和的虚弱（Weaken）、致命毒药（Eiha：亚森的咒怨）。
/// 点由调用方换算好传进来（屏幕全局坐标）；facing = 1 朝右（敌人那边）。
/// </summary>
public static class CardFx
{
	/// <summary>咒怨的颜色：近黑的紫 + 亮紫红的边（P5 的 Eiha）。</summary>
	public static readonly Color CurseRim = new("d43bff"), CurseDark = new("12001c"), CurseHot = new("ffd6ff");

	// ───────────────────────── 防御：前臂一竖，身前张开一面弧形光盾 ─────────────────────────

	private static Shader? _barrier;

	/// <summary>
	/// 光盾：侧面看过去的一面弧形盾（往敌人那边鼓），前沿一道白热的亮边；盾面是红色的漫画网点（越靠前沿点越大越密），
	/// 一道光从上往下扫过；dissolve 0→1 时按噪声一块块碎掉。grow：从中间往上下张开。
	/// </summary>
	private static Shader Barrier => _barrier ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform vec4 tint : source_color = vec4(1.0, 0.08, 0.15, 1.0);
uniform vec4 hot : source_color = vec4(1.0, 0.95, 0.94, 1.0);
uniform float grow = 1.0;
uniform float scan = -2.0;
uniform float dissolve = 0.0;
uniform float alpha = 1.0;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
void fragment() {
	vec2 p = UV * 2.0 - 1.0;
	float ay = abs(p.y);
	float open = smoothstep(grow + 0.02, grow - 0.08, ay);
	float xf = 0.62 - 0.85 * p.y * p.y;
	float th = mix(0.75, 0.16, ay * ay);
	float d = xf - p.x;
	float inside = step(0.0, d) * smoothstep(th, th * 0.35, d);
	float rim = exp(-pow(d / 0.045, 2.0)) * smoothstep(-0.12, 0.0, d);
	vec2 g = vec2(p.x * 7.0, p.y * 15.0);
	g.x += step(1.0, mod(floor(g.y), 2.0)) * 0.5;
	vec2 cell = fract(g) - 0.5;
	float k = clamp(d / th, 0.0, 1.0);
	float r = mix(0.4, 0.06, k);
	float dots = smoothstep(r, r - 0.1, length(cell));
	float fill = inside * (0.12 + 0.6 * dots * (1.0 - k * 0.6));
	float stripe = exp(-pow((p.y - scan) / 0.07, 2.0)) * (inside + rim) * 1.3;
	float taper = smoothstep(1.0, 0.8, ay);
	float keep = smoothstep(dissolve - 0.05, dissolve + 0.05, noise(UV * vec2(9.0, 22.0)) * 0.95 + 0.04);
	vec3 col = tint.rgb * (fill + rim * 0.7) + hot.rgb * (rim * 0.85 + stripe);
	COLOR = vec4(col, open * taper * keep * alpha);
}" };

	/// <summary>
	/// 防御（格挡）：前臂竖起那一刻（cast_guard 的 guard_a2），拳头上白闪 + 红光爆开、空间一扭，身前"唰"地张开一面弧形光盾，
	/// 一道光从上扫到下，停一下，盾碎成光片散掉。fist = 拳头，shield = 盾的中心。~0.9s。
	/// </summary>
	public static void Guard(Control layer, Vector2 fist, Vector2 shield, float height, float facing = 1f)
	{
		Glow.Flash(layer, fist, Pal5.Hot, 150f, 0.14f);
		Glow.Flash(layer, fist, Pal5.Bright, 340f, 0.3f);
		Glow.Ripple(layer, shield, height * 1.3f, 0.5f, 0.022f, 1.6f);
		Glow.Sparks(layer, fist, 26, 160f, 520f, 0.5f, new[] { Pal5.Hot, Pal5.Bright, Pal5.Deep }, 300f, 0.1f, 0.3f,
			new Vector2(facing, -0.3f), 70f);
		Fx.Shake(1);

		float w = height * 0.5f;
		Node2D root = Fx.Root(layer, shield, "P5Guard", 1.1);
		ColorRect r = new()
		{
			Size = new Vector2(w, height), Position = new Vector2(-w / 2f, -height / 2f), PivotOffset = new Vector2(w / 2f, height / 2f),
			Scale = new Vector2(facing, 1f), MouseFilter = Control.MouseFilterEnum.Ignore,
			Material = new ShaderMaterial { Shader = Barrier },
		};
		root.AddChild(r);
		void Set(string n, Variant v) => ((ShaderMaterial)r.Material).SetShaderParameter(n, v);
		Set("grow", 0f);
		bool shattered = false;
		Fx.Animate(root, 0, 0.9, t =>
		{
			Set("grow", Ease5.OutBack(Ease5.Seg(t, 0f, 0.14f)) * 1.0f);
			Set("scan", Mathf.Lerp(-1.2f, 1.2f, Fx.OutCubic(Ease5.Seg(t, 0.12f, 0.4f))));
			Set("dissolve", Fx.InCubic(Ease5.Seg(t, 0.62f, 0.38f)));
			// 刚张开时亮一下、往前顶一点（过冲）
			float pop = 1f - Ease5.Seg(t, 0f, 0.2f);
			Set("alpha", 1f + 0.8f * pop);
			root.Position = shield + new Vector2(facing * 14f * Mathf.Sin(Ease5.Seg(t, 0f, 0.22f) * Mathf.Pi), 0f);
			if (!shattered && t >= 0.64f)
			{
				shattered = true;
				Glow.Shards(layer, shield + new Vector2(facing * w * 0.2f, 0f), 10, 0.7f, new Vector2(facing, 0f), 1.2f);
				for (int i = 0; i < 3; i++)
				{
					Glow.Glint(layer, shield + new Vector2(facing * w * 0.28f, height * Fx.Rand(-0.35f, 0.35f)), Fx.Rand(0.7f, 1.1f), i * 0.05f);
				}
			}
		});
	}

	// ───────────────────────── 中和（虚弱）：一道红光锁环把敌人"箍"住、往下压 ─────────────────────────

	/// <summary>
	/// 虚弱：斩击落下后，一圈红光环从外面猛地收紧箍在敌人身上（白闪），三道"V"形光从头顶一道道砸下来，
	/// 红色的光尘往下沉，脚下压出一圈扁平的暗红波。center / top = 敌人中心 / 头顶。~1s。
	/// </summary>
	public static void Weaken(Control layer, Vector2 center, Vector2 top, float delay = 0f)
	{
		float h = Mathf.Max(80f, (center - top).Length());
		Vector2 waist = center + new Vector2(0f, h * 0.1f), feet = center + new Vector2(0f, h * 0.95f);
		Fx.After(layer, delay, () =>
		{
			// 锁环：两圈压扁的光环从 1.8 倍收到 1 倍，过冲，然后慢慢转、变暗
			Node2D root = Fx.Root(layer, waist, "P5Weaken", 1.3);
			for (int i = 0; i < 2; i++)
			{
				ColorRect ring = Glow.RingRect(root, h * 2.6f, 0.26f);
				Glow.SetParam(ring, "tint", i == 0 ? Pal5.Bright : Pal5.Hot);
				Glow.SetParam(ring, "width", i == 0 ? 0.022f : 0.01f);
				Glow.SetParam(ring, "radius", 0.9f);
				float lag = i * 0.04f;
				Fx.Animate(ring, lag, 1.1, t =>
				{
					float k = Ease5.OutBack(Ease5.Seg(t, 0f, 0.2f));
					Glow.SetParam(ring, "radius", Mathf.Lerp(0.95f, 0.55f, k));
					Glow.SetParam(ring, "alpha", (i == 0 ? 1f : 0.8f) * (1f - Fx.InCubic(Ease5.Seg(t, 0.55f, 0.45f))));
				});
			}
			Fx.After(layer, 0.18, () =>
			{
				Glow.Flash(layer, waist, Pal5.Hot, h * 1.2f, 0.16f);
				Glow.Flash(layer, waist, Pal5.Bright, h * 2.4f, 0.35f);
				Fx.Shake(1);
			});
			// 三道 V 从头顶一道道往下砸（越往下越大），落到腰上消失
			for (int i = 0; i < 3; i++)
			{
				Chevron(layer, top + new Vector2(0f, -h * 0.5f), waist, h * (0.55f + 0.18f * i), 0.22f + i * 0.11f);
			}
			// 往下沉的光尘 + 脚下一圈扁的暗红波
			Fx.After(layer, 0.3, () =>
			{
				Glow.Sparks(layer, waist, 30, 40f, 160f, 0.9f, new[] { Pal5.Bright, Pal5.Crimson, Pal5.Deep }, 420f, 0.1f, 0.3f,
					Vector2.Down, 80f);
				Glow.Shockwave(layer, feet, Pal5.Bright, h * 3.2f, 0.6f, 0.22f);
				Glow.Ripple(layer, feet, h * 2.6f, 0.5f, 0.018f, 0.3f);
			});
		});
	}

	/// <summary>一道往下指的发光"V"：从 from 砸到 to，越来越快，到了亮一下消失。</summary>
	private static void Chevron(Control layer, Vector2 from, Vector2 to, float width, float delay)
	{
		Node2D root = Fx.Root(layer, from, "P5Chevron", delay + 0.4);
		float w = width / 2f, hh = width * 0.32f, th = width * 0.09f;
		Vector2[] shape = { new(-w, -hh), new(0f, hh - th * 2f), new(w, -hh), new(w, -hh + th), new(0f, hh), new(-w, -hh + th) };
		Polygon2D glow = Fx.Poly(root, shape, new Color(Pal5.Bright, 0.85f), additive: true);
		Polygon2D core = Fx.Poly(root, shape, Pal5.Hot, additive: true);
		core.Scale = new Vector2(0.94f, 0.5f);
		glow.Scale = new Vector2(1.08f, 1.2f);
		root.Visible = false;
		Fx.Animate(root, delay, 0.3, t =>
		{
			root.Visible = true;
			float k = Fx.InCubic(Mathf.Min(1f, t / 0.75f));
			root.GlobalPosition = from.Lerp(to, k);
			root.Modulate = new Color(1f, 1f, 1f, t < 0.75f ? Mathf.Min(1f, t * 6f) : 1f - (t - 0.75f) / 0.25f);
			root.Scale = new Vector2(1f + 0.3f * Ease5.Seg(t, 0.75f, 0.25f), 1f);
		});
	}

	// ───────────────────────── 致命毒药：Persona 的咒怨（Eiha）从敌人脚下烧起来 ─────────────────────────

	/// <summary>
	/// 咒怨：Joker 手扣面具那一刻面具上蓝焰一爆（mask），敌人脚下张开一圈紫色的咒印（压扁的光环 + 往里收的光点），
	/// 一根黑紫色的咒焰柱从脚下蹿起把敌人吞掉、旁边几簇小火错开冒，紫红火星往上飞，空间扭一下。~1.3s。
	/// </summary>
	public static void Eiha(Control layer, Vector2 mask, Vector2 center, Vector2 top, float delay = 0.12f)
	{
		Glow.Flash(layer, mask, ReadyFx.BlueHot, 120f, 0.12f);
		Glow.Flash(layer, mask, ReadyFx.Blue, 300f, 0.35f);
		Glow.Sparks(layer, mask, 18, 120f, 380f, 0.5f, new[] { ReadyFx.BlueHot, ReadyFx.Blue, ReadyFx.BlueDeep }, -120f, 0.1f, 0.3f,
			Vector2.Up, 120f);
		float h = Mathf.Max(80f, (center - top).Length());
		Vector2 feet = center + new Vector2(0f, h * 0.95f);
		Fx.After(layer, delay, () =>
		{
			// 咒印：脚下一圈压扁的紫光环张开、慢慢转暗；四周的紫光点往中间收
			Node2D sigil = Fx.Root(layer, feet, "P5EihaSigil", 1.5);
			for (int i = 0; i < 2; i++)
			{
				ColorRect ring = Glow.RingRect(sigil, h * (3.2f - i * 0.9f), 0.24f);
				Glow.SetParam(ring, "tint", i == 0 ? CurseRim : CurseHot);
				Glow.SetParam(ring, "width", 0.018f);
				Fx.Animate(ring, i * 0.05, 1.3, t =>
				{
					Glow.SetParam(ring, "radius", Mathf.Lerp(0.1f, 0.85f, Ease5.OutExpo(Ease5.Seg(t, 0f, 0.25f))));
					Glow.SetParam(ring, "alpha", 0.9f * (1f - Fx.InCubic(Ease5.Seg(t, 0.5f, 0.5f))));
				});
			}
			Glow.Gather(layer, feet, CurseRim, h * 2.2f, 0.16f, 0.24f);
			// 主火柱 + 错开的小火
			Fx.After(layer, 0.14, () =>
			{
				// 火身很透（看得见被烧的敌人），烧得快；后面一团紫光托着
				Glow.Flash(layer, center, CurseRim, h * 3.6f, 0.9f);
				Glow.CurseFire(layer, feet + new Vector2(0f, 6f), h * 1.7f, h * 3.4f, 0f, 1.05f, 1f, CurseRim, CurseDark, 1.7f, 0.28f);
				for (int i = 0; i < 4; i++)
				{
					float x = (i % 2 == 0 ? -1f : 1f) * h * Fx.Rand(0.45f, 0.85f);
					Glow.CurseFire(layer, feet + new Vector2(x, 4f), h * 0.8f, h * Fx.Rand(1.3f, 1.9f), 0.05f + i * 0.07f, 0.8f, 1f, CurseRim, CurseDark,
						1.9f, 0.25f);
				}
				Glow.Ripple(layer, center, h * 3.2f, 0.6f, 0.025f);
				Glow.Sparks(layer, feet, 40, 120f, 480f, 1.0f, new[] { CurseHot, CurseRim, CurseDark }, -260f, 0.1f, 0.35f, Vector2.Up, 35f);
				Fx.Shake(2);
			});
		});
	}
}
