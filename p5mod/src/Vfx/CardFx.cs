using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 出牌特效（2026-10-29 重做，docs/p5/FX_PLAN.md：主体是光、粒子、空间扭曲，实时渲染）。
/// 第一批：防御（Guard）、中和的虚弱（Weaken）、致命毒药（Eiha：亚森的咒怨）。
/// 点由调用方换算好传进来（屏幕全局坐标）；facing = 1 朝右（敌人那边）。
/// </summary>
public static class CardFx
{
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
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
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

	// ───────────────────────── 致命毒药：亚森的咒怨（Eiha，P5 原作的黑红） ─────────────────────────

	private static Shader? _sigil, _haze;

	/// <summary>
	/// 咒印（地上的法阵，压扁了贴地）：三圈细环，外两圈之间一圈刻度，中圈一圈断续的"符文"短划，里面一个五角星；spin 转、open 张开。加法。
	/// </summary>
	private static Shader Sigil => _sigil ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform vec4 tint : source_color = vec4(1.0, 0.1, 0.18, 1.0);
uniform vec4 hot : source_color = vec4(1.0, 0.85, 0.8, 1.0);
uniform float open = 1.0;
uniform float spin = 0.0;
uniform float alpha = 1.0;
float seg(vec2 p, vec2 a, vec2 b) { vec2 pa = p - a, ba = b - a; float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0); return length(pa - ba * h); }
float ring(float r, float R, float w) { return exp(-pow((r - R) / w, 2.0)); }
void fragment() {
	vec2 p = UV * 2.0 - 1.0;
	float r = length(p) / max(open, 0.001);
	float a = atan(p.y, p.x);
	float v = ring(r, 0.93, 0.014) + ring(r, 0.8, 0.009) * 0.8 + ring(r, 0.42, 0.01) * 0.7;
	float band = step(0.8, r) * step(r, 0.93);
	float ticks = band * pow(abs(sin((a + spin) * 36.0)), 40.0);
	float runes = ring(r, 0.62, 0.025) * step(0.25, sin((a - spin * 1.5) * 9.0)) * step(0.0, sin((a - spin * 1.5) * 27.0 + 1.0));
	float star = 0.0;
	for (int i = 0; i < 5; i++) {
		float t0 = spin + 6.2832 * float(i) / 5.0, t1 = spin + 6.2832 * float(i + 2) / 5.0;
		star += exp(-pow(seg(p / max(open, 0.001), 0.8 * vec2(cos(t0), sin(t0)), 0.8 * vec2(cos(t1), sin(t1))) / 0.012, 2.0));
	}
	float m = v + ticks * 0.9 + runes * 0.75 + star * 0.6;
	vec3 col = tint.rgb * m + hot.rgb * (ring(r, 0.93, 0.005) + ring(r, 0.8, 0.004)) * 0.8;
	COLOR = vec4(col, clamp(m, 0.0, 1.0) * alpha * smoothstep(1.05, 0.95, r));
}" };

	/// <summary>热浪：把后面的画面按往上流的噪声轻轻扭动，边缘柔和淡出。</summary>
	private static Shader Haze => _haze ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
uniform float strength = 0.006;
uniform float clock = 0.0;
uniform float alpha = 1.0;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
void fragment() {
	vec2 q = UV * vec2(6.0, 10.0) + vec2(0.0, clock * 3.0);
	vec2 off = vec2(noise(q) - 0.5, noise(q + 7.3) - 0.5) * strength * alpha;
	vec2 c = UV * 2.0 - 1.0;
	float m = smoothstep(1.0, 0.4, length(c));
	COLOR = vec4(texture(screen_tex, SCREEN_UV + off * m).rgb, m * alpha);
}" };

	/// <summary>
	/// 咒怨（Eiha，P5 原作的黑红）：
	/// ① 蓄（0～0.3s）：面具上蓝焰一爆（Persona 出手）；敌人脚下张开一圈红色咒印（刻度环、符文、五角星，慢慢转），四周八道红光沿地面往中心收；
	/// ② 爆（0.3s）：一根黑底绯红火丝的咒焰柱从脚下蹿起吞掉敌人（细丝火，Flame.Column），四根小火柱错开冒，红光闪、扁的冲击环、空间一扭、震屏；
	/// ③ 烧（0.3～1.1s）：火星打着旋往上飘，火上方黑烟翻滚，热浪扭动后面的画面，敌人被染成暗红；
	/// ④ 收（1.1s）：火往中心一缩，红环往里收，一点热白闪掉，留余烟。
	/// tint：给敌人上色（游戏里改敌人节点的 Modulate，结束时调用方传入的会被设回白色）。
	/// </summary>
	public static void Eiha(Control layer, Vector2 mask, Vector2 center, Vector2 top, Action<Color>? tint = null)
	{
		Glow.Flash(layer, mask, ReadyFx.BlueHot, 110f, 0.12f);
		Glow.Sparks(layer, mask, 14, 120f, 360f, 0.45f, new[] { ReadyFx.BlueHot, ReadyFx.Blue, ReadyFx.BlueDeep }, -120f, 0.1f, 0.28f, Vector2.Up, 120f);
		float h = Mathf.Max(80f, (center - top).Length());
		Vector2 feet = center + new Vector2(0f, h * 0.95f);

		// ① 咒印 + 往中心收的红光
		Node2D sigil = Fx.Root(layer, feet, "P5EihaSigil", 1.6);
		float sw = h * 3.4f;
		ColorRect ring = new()
		{
			Size = new Vector2(sw, sw), Position = new Vector2(-sw / 2f, -sw / 2f), PivotOffset = new Vector2(sw / 2f, sw / 2f),
			Scale = new Vector2(1f, 0.28f), MouseFilter = Control.MouseFilterEnum.Ignore, Material = new ShaderMaterial { Shader = Sigil },
		};
		sigil.AddChild(ring);
		void SetS(string n, Variant v) => ((ShaderMaterial)ring.Material).SetShaderParameter(n, v);
		SetS("open", 0f);
		Fx.Animate(ring, 0.04, 1.4, t =>
		{
			SetS("open", Ease5.OutExpo(Ease5.Seg(t, 0f, 0.2f)) * (1f - 0.6f * Fx.InCubic(Ease5.Seg(t, 0.82f, 0.18f))));
			SetS("spin", t * 1.6f);
			float flare = Mathf.Exp(-Mathf.Pow((t - 0.19f) / 0.04f, 2f));                // 爆的那一刻亮一下
			SetS("alpha", (0.85f + 1.2f * flare) * (1f - Ease5.Seg(t, 0.85f, 0.15f)));
		});
		for (int i = 0; i < 8; i++)
		{
			float ang = Mathf.Tau * i / 8f + Fx.Rand(-0.2f, 0.2f);
			Vector2 dir = new(Mathf.Cos(ang), Mathf.Sin(ang) * 0.28f);
			Node2D streak = new();
			sigil.AddChild(streak);
			Polygon2D glow = Glow.TaperPoly(streak, new Color(Pal5.Bright, 0.9f));
			Polygon2D core = Glow.TaperPoly(streak, Pal5.Hot);
			float r0 = sw * 0.55f;
			streak.Visible = false;
			Fx.Animate(streak, 0.05 + i * 0.012, 0.24, t =>
			{
				streak.Visible = true;
				float head = Fx.InCubic(t), tail = Fx.InCubic(Mathf.Max(0f, t - 0.25f) / 0.75f);
				Vector2 p0 = dir * r0 * (1f - tail), p1 = dir * r0 * (1f - head);
				glow.Polygon = Glow.TaperPts(p0, p1, 9f);
				core.Polygon = Glow.TaperPts(p0, p1, 3f);
			});
		}

		// ② 爆
		Fx.After(layer, 0.3, () =>
		{
			Flame.Column(layer, feet + new Vector2(0f, 8f), h * 1.9f, h * 3.3f, 0f, 0.95f, 1.4f);
			for (int i = 0; i < 4; i++)
			{
				float x = (i % 2 == 0 ? -1f : 1f) * h * (0.55f + 0.25f * (i / 2));
				Flame.Column(layer, feet + new Vector2(x, 6f), h * 0.9f, h * Fx.Rand(1.4f, 2.0f), 0.04f + i * 0.06f, 0.75f, 1.7f);
			}
			Glow.Flash(layer, center, Pal5.Hot, h * 1.4f, 0.14f);
			Glow.Flash(layer, center, Pal5.Bright, h * 3.4f, 0.45f);
			Glow.Shockwave(layer, feet, Pal5.Bright, h * 4f, 0.55f, 0.26f);
			Glow.Ripple(layer, center, h * 3.6f, 0.6f, 0.03f);
			Fx.Shake(2);
			// 火星：打着旋往上飘
			Embers(layer, feet, h, 46, 1.1f);
			// 黑烟：火上方翻滚（普通混合，压暗后面，衬出火）
			Smoke(layer, center + new Vector2(0f, -h * 1.1f), h);
			// 热浪
			Node2D haze = Fx.Root(layer, center + new Vector2(0f, -h * 0.4f), "P5EihaHaze", 1.0);
			float hw = h * 2.4f, hh = h * 3.6f;
			ColorRect hz = new()
			{
				Size = new Vector2(hw, hh), Position = new Vector2(-hw / 2f, -hh / 2f), MouseFilter = Control.MouseFilterEnum.Ignore,
				Material = new ShaderMaterial { Shader = Haze },
			};
			haze.AddChild(hz);
			Fx.Clock(hz);
			Fx.Animate(hz, 0, 0.9, t => ((ShaderMaterial)hz.Material).SetShaderParameter("alpha", Mathf.Sin(Mathf.Min(1f, t * 1.2f) * Mathf.Pi)));
			// 敌人被烧成暗红
			if (tint != null)
			{
				Fx.Animate(sigil, 0, 0.9, t =>
				{
					float k = Mathf.Min(1f, t * 6f) * (1f - Fx.InCubic(Ease5.Seg(t, 0.6f, 0.4f)));
					tint(new Color(1f, 1f, 1f).Lerp(new Color(0.55f, 0.16f, 0.22f), k));
				});
				Fx.After(layer, 0.95, () => tint(Colors.White));
			}
		});

		// ④ 收
		Fx.After(layer, 1.12, () =>
		{
			Glow.Gather(layer, feet, Pal5.Bright, h * 3f, 0.16f, 0.28f);
			Glow.Flash(layer, feet + new Vector2(0f, -h * 0.6f), Pal5.Hot, h * 0.9f, 0.2f, 0.14f);
		});
	}

	/// <summary>打着旋往上飘的火星（GPU 粒子，湍流）。</summary>
	private static void Embers(Control layer, Vector2 feet, float h, int amount, float life)
	{
		Node2D root = Fx.Root(layer, feet, "P5Embers", life + 0.5);
		Gradient g = new()
		{
			Offsets = new[] { 0f, 0.3f, 0.75f, 1f },
			Colors = new[] { Flame.CurseHot, Flame.CurseHi, Flame.CurseMid, new Color(Flame.CurseMid, 0f) },
		};
		ParticleProcessMaterial pm = new()
		{
			Direction = new Vector3(0f, -1f, 0f), Spread = 28f, InitialVelocityMin = h * 1.2f, InitialVelocityMax = h * 3.2f,
			Gravity = new Vector3(0f, -h * 0.4f, 0f), DampingMin = 1.5f, DampingMax = 3f, ScaleMin = 0.08f, ScaleMax = 0.3f,
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(h * 0.7f, 6f, 0f),
			TurbulenceEnabled = true, TurbulenceNoiseStrength = 6f, TurbulenceNoiseScale = 3f, TurbulenceInfluenceMin = 0.08f, TurbulenceInfluenceMax = 0.2f,
			ColorRamp = new GradientTexture1D { Gradient = g },
		};
		GpuParticles2D p = new()
		{
			Amount = amount, Lifetime = life, OneShot = true, Explosiveness = 0.55f, ProcessMaterial = pm, Texture = Glow.Soft,
			Material = Fx.Additive, LocalCoords = false, Emitting = true,
		};
		root.AddChild(p);
	}

	/// <summary>翻滚的黑烟（普通混合的暗色柔团，慢慢往上飘、胀大、淡掉）。</summary>
	private static void Smoke(Control layer, Vector2 at, float h)
	{
		Node2D root = Fx.Root(layer, at, "P5Smoke", 2.0);
		Gradient g = new()
		{
			Offsets = new[] { 0f, 0.25f, 1f },
			Colors = new[] { new Color(0.06f, 0f, 0.02f, 0f), new Color(0.06f, 0f, 0.02f, 0.55f), new Color(0.03f, 0f, 0.01f, 0f) },
		};
		ParticleProcessMaterial pm = new()
		{
			Direction = new Vector3(0f, -1f, 0f), Spread = 35f, InitialVelocityMin = h * 0.3f, InitialVelocityMax = h * 0.8f,
			Gravity = Vector3.Zero, DampingMin = 0.5f, DampingMax = 1f, ScaleMin = h / 32f * 0.6f, ScaleMax = h / 32f * 1.1f,
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(h * 0.5f, h * 0.4f, 0f),
			AngleMin = 0f, AngleMax = 360f,
			ScaleCurve = new CurveTexture { Curve = Grow() },
			ColorRamp = new GradientTexture1D { Gradient = g },
		};
		GpuParticles2D p = new()
		{
			Amount = 16, Lifetime = 1.4, OneShot = true, Explosiveness = 0.7f, ProcessMaterial = pm, Texture = Glow.Soft,
			LocalCoords = false, Emitting = true,
		};
		root.AddChild(p);
	}

	private static Curve Grow()
	{
		Curve c = new();
		c.AddPoint(new Vector2(0f, 0.5f));
		c.AddPoint(new Vector2(1f, 1.4f));
		return c;
	}
}
