using System.Collections.Generic;
using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 出牌特效（2026-10-29 重做，docs/p5/FX_PLAN.md：主体是光、粒子、空间扭曲，实时渲染；2026-11-01 防御改成 P5 平面风，Ink.cs）。
/// 第一批：防御（Guard）、中和的虚弱（Weaken）、致命毒药（Eiha：亚森的咒怨）。
/// 点由调用方换算好传进来（屏幕全局坐标）；facing = 1 朝右（敌人那边）。
/// </summary>
public static class CardFx
{
	// ───────────────────────── 防御：前臂一竖，身前「唰唰」叠起一面斜切面板墙 ─────────────────────────

	/// <summary>
	/// 防御（格挡）：菱形光格（设计说明 docs/p5/FX_DESIGN_HITS.md 方案 A，做法照结城理 mod 的 Extras.HexShield，换成 P5 的斜切几何）。
	/// 前臂竖起那一刻（cast_guard 的 guard_a2），身前一片竖长椭圆区域里，细线勾出的红白菱形格子从前臂的高度往外波浪式点亮
	/// （每格 OutBack 弹出、出现时闪白），整片往敌人那侧斜一点、两边往后收（像弧面的正投影）；停一会儿，再从外往里一格格熄灭。
	/// 点亮时一圈空间扭曲往外推。是光，不是物件。fist = 拳头，shield = 光格的中心，height = 光格高度参考。~0.95s。
	/// </summary>
	public static void Guard(Control layer, Vector2 fist, Vector2 shield, float height, float facing = 1f) =>
		GuardFx.Shield(layer, fist, shield + new Vector2(height * 0.12f * facing, 0f), height * 0.72f, facing);   // 2026-11-04 重做：碎玻璃墙（Vfx/GuardFx.cs）

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
	/// <summary>咒焰柱蹿起的时刻（出牌后几秒）：游戏在施法后 0.25s 加上中毒、跳数字，火要和它一起到。</summary>
	public const float Burst = 0.2f;

	/// <summary>咒焰往中间一收的时刻（Eiha 开始后几秒）：和眼神特写被劈开、亚森离场同一刻。</summary>
	public static float Collapse => Burst + 0.55f;

	public static void Eiha(Control layer, Vector2 mask, Vector2 center, Vector2 top, Action<Color>? tint = null)
	{
		float h = Mathf.Max(80f, (center - top).Length());
		Vector2 feet = center + new Vector2(0f, h * 0.95f);


		// 咒怨之手：咒印张开时钻出来，咒焰喷发那一刻拍下去，之后被火烧掉
		CurseHands(layer, feet, center, h, 0.08f, Burst, Burst + 0.22f, false, true);   // 完整咒怨跟召唤一起出：冲击声并进召唤那一拍

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
		Fx.Animate(ring, 0, 1.3, t =>
		{
			SetS("open", Ease5.OutExpo(Ease5.Seg(t, 0f, 0.14f)) * (1f - 0.6f * Fx.InCubic(Ease5.Seg(t, 0.82f, 0.18f))));
			SetS("spin", t * 1.6f);
			float flare = Mathf.Exp(-Mathf.Pow((t - 0.154f) / 0.035f, 2f));              // 爆的那一刻（0.2s）亮一下
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
			Fx.Animate(streak, 0.02 + i * 0.008, 0.17, t =>
			{
				streak.Visible = true;
				float head = Fx.InCubic(t), tail = Fx.InCubic(Mathf.Max(0f, t - 0.25f) / 0.75f);
				Vector2 p0 = dir * r0 * (1f - tail), p1 = dir * r0 * (1f - head);
				glow.Polygon = Glow.TaperPts(p0, p1, 9f);
				core.Polygon = Glow.TaperPts(p0, p1, 3f);
			});
		}

		// ② 爆：0.2s，和游戏加上中毒（施法后 0.25s）几乎同时
		Sfx.Play(Sfx.Curse, 0.45f, Burst + 0.25f);                    // 余韵层：咒焰烧起来（召唤那一拍之后，压低）
		Fx.After(layer, Burst, () =>
		{
			Flame.Column(layer, feet + new Vector2(0f, 8f), h * 1.9f, h * 3.3f, 0f, Collapse - Burst + 0.2f, 1.4f);
			for (int i = 0; i < 4; i++)
			{
				float x = (i % 2 == 0 ? -1f : 1f) * h * (0.55f + 0.25f * (i / 2));
				Flame.Column(layer, feet + new Vector2(x, 6f), h * 0.9f, h * Fx.Rand(1.4f, 2.0f), 0.04f + i * 0.06f, 0.75f, 1.7f);
			}
			Glow.Flash(layer, center, Pal5.Hot, h * 1.4f, 0.14f);
			Glow.Flash(layer, center, Pal5.Bright, h * 3.4f, 0.45f);
			// 冲击只贴着地面走（用户：悬在半空的那一圈波纹看着不同步、也不贴地）
			Glow.Shockwave(layer, feet, Pal5.Bright, h * 4f, 0.55f, 0.26f);
			Glow.Ripple(layer, feet, h * 3.6f, 0.5f, 0.02f, 0.3f);
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
		Fx.After(layer, Collapse, () =>
		{
			Glow.Gather(layer, feet, Pal5.Bright, h * 3f, 0.16f, 0.28f);
			Glow.Flash(layer, feet + new Vector2(0f, -h * 0.6f), Pal5.Hot, h * 0.9f, 0.2f, 0.14f);
		});
	}

	/// <summary>
	/// 精简咒怨（普通白卡，Tiers.Common，用户 2026-11-01：致命毒药是很普通的白卡，不需要完整演出）：
	/// 地里钻出手（Burst 那一刻拍下去、随后烧掉）+ 一根咒焰柱 + 一点火星和红光，敌人染一下暗红。≈0.6s，不出咒印 / 黑烟 / 热浪 / 小火柱。
	/// </summary>
	public static void CurseLight(Control layer, Vector2 center, Vector2 top, Action<Color>? tint = null, float bodyW = 0f)
	{
		float h = Mathf.Max(80f, (center - top).Length());
		float bw = bodyW > 0f ? bodyW : h * 1.3f;
		Vector2 feet = center + new Vector2(0f, h * 0.95f);
		CurseHands(layer, feet, center, h, 0.0f, Burst, Burst + 0.16f);
		Sfx.Play(Sfx.Curse, 0.7f, Burst - 0.05f);
		Fx.After(layer, Burst, () =>
		{
			Flame.Wrap(layer, Stage.Back, feet, bw, h * 2.3f, 0f, 0.45f);
			Glow.Flash(layer, center, Pal5.Bright, h * 2.2f, 0.3f);
			Glow.Shockwave(layer, feet, Pal5.Bright, h * 2.8f, 0.4f, 0.26f);
			Embers(layer, feet, h, 16, 0.7f);
			if (tint != null)
			{
				Fx.Animate(layer, 0, 0.5, t =>
				{
					float k = Mathf.Min(1f, t * 6f) * (1f - Fx.InCubic(Ease5.Seg(t, 0.5f, 0.5f)));
					tint(new Color(1f, 1f, 1f).Lerp(new Color(0.6f, 0.18f, 0.24f), k));
				});
				Fx.After(layer, 0.55, () => tint(Colors.White));
			}
		});
	}

	// ───────────────────────── 咒怨之手（用户 2026-11-01：咒怨的主题上多一些东西——地里钻出手来拍怪，和伤害、火往上钻同步） ─────────────────────────

	private static Shader? _hand;

	/// <summary>
	/// 一只手（Codex 画的 P5 风格黑红手，wrist 在图底边）：rise 0～1 从地里钻出来的比例（地面以下的部分丢掉），
	/// hot 命中时红色高光过曝，burn 0～1 从指尖往下烧掉（烧的边是亮红 → 橙白），dim 后排压暗。
	/// </summary>
	private static Shader HandShader => _hand ??= new Shader { Code = @"
shader_type canvas_item;
uniform float rise = 1.0;
uniform float hot = 0.0;
uniform float burn = 0.0;
uniform float dim = 0.0;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
void fragment() {
	if (UV.y > rise) { discard; }
	vec4 c = texture(TEXTURE, UV);
	float red = clamp(c.r - max(c.g, c.b) * 1.2, 0.0, 1.0);          // 红色高光的地方
	vec3 col = c.rgb + vec3(1.0, 0.35, 0.3) * red * hot * 1.4 + vec3(0.25, 0.0, 0.02) * hot * (1.0 - red);
	col *= 1.0 - dim * 0.45;
	float n = noise(UV * vec2(14.0, 20.0)) * 0.25 + noise(UV * vec2(40.0, 55.0)) * 0.08;
	float d = (UV.y + n) - (burn * 1.4 - 0.15);                      // < 0：已经烧掉（从上往下）
	if (burn > 0.0 && d < 0.0) { discard; }
	float edge = burn > 0.0 ? smoothstep(0.07, 0.0, d) : 0.0;
	col = mix(col, mix(vec3(1.0, 0.15, 0.1), vec3(1.0, 0.85, 0.6), smoothstep(0.03, 0.0, d)), edge * step(0.02, c.a));
	COLOR = vec4(col, c.a);
}" };

	private sealed class Hand
	{
		public Node2D Root = null!;
		public Sprite2D Sprite = null!;
		public ShaderMaterial Mat = null!;
		public float H;
	}

	private static Hand? MakeHand(Control layer, string tex, Vector2 ground, float height, bool mirror, float dim)
	{
		if (Fx.Tex(tex) is not Texture2D t)
		{
			return null;
		}
		Node2D root = Fx.Root(layer, ground, "P5CurseHand", 2.0);
		float k = height / t.GetHeight();
		Sprite2D sp = new()
		{
			Texture = t, Centered = false, Offset = new Vector2(-t.GetWidth() / 2f, -t.GetHeight()),
			Scale = new Vector2(mirror ? -k : k, k), Material = new ShaderMaterial { Shader = HandShader },
		};
		root.AddChild(sp);
		ShaderMaterial m = (ShaderMaterial)sp.Material;
		m.SetShaderParameter("rise", 0f);
		m.SetShaderParameter("dim", dim);
		return new Hand { Root = root, Sprite = sp, Mat = m, H = height };
	}

	private static readonly string[] Poses = { "chand_bud", "chand_unfurl", "chand_open", "chand_slap", "chand_clench" };

	/// <summary>
	/// 咒怨之手（用户 2026-11-01：三只手完整地从地里长出来、伤害打出时同时拍向目标、结束时钻回去；注意每个阶段的手型）。
	/// 贴图是 Codex 画的同一只手的五个姿势（curse_hands_v2，打包时对齐了手腕，逐帧切换），掌心朝右（敌人那边），右边的手镜像：
	///  · 长（emergeAt 起）：地面一裂，手从地里往上钻 —— 花苞（指头蜷着）→ 舒展 → 张开；边长边轻轻扭；
	///  · 蓄（到 hitAt 前）：张开的手往后仰，越仰越多；
	///  · 拍（hitAt）：前 0.05s 换成拍击手型、手腕一折猛地扑向敌人，撞上过冲一点、弹回；红光过曝；
	///  · 停（到 burnAt）：拍着不动，轻轻颤；
	///  · 缩（burnAt 起 0.3s）：握成拳、回正、往地里缩回去，地面一点尘。
	/// 三只手：左右各一（前排），左后方一只更大的（画在前排后面，压暗一点）。few = 群攻，手小一点。
	/// </summary>
	public static void CurseHands(Control layer, Vector2 feet, Vector2 center, float h, float emergeAt, float hitAt, float burnAt, bool few = false, bool quiet = false)
	{
		if (Fx.Tex(Poses[0]) is not Texture2D t0)
		{
			return;
		}
		Texture2D?[] tex = new Texture2D?[Poses.Length];
		for (int i = 0; i < Poses.Length; i++) tex[i] = Fx.Tex(Poses[i]) ?? t0;
		float k0 = few ? 0.8f : 1f;
		// (层, 离脚底的横向偏移 × h, 高 × h, 掌心朝左（镜像）, 压暗, 拍下去转多少, 错开)
		(Control L, float X, float Height, bool Mirror, float Dim, float Slap, float Lag)[] spec =
		{
			// 拍下去转的角度要让指尖真的落在敌人身上（用户 2026-11-01：像捏了一把，要砸上去）
			// 手臂够长、从上方把掌心砸到敌人身上（拍击手型本身手腕折了 60°，再转太多会平趴在地上）
			(layer, -1.35f, 2.9f, false, 0.35f, 0.62f, 0.08f),          // 先建：画在两只前排手后面
			(layer, -0.85f, 2.3f, false, 0f, 0.52f, 0f),
			(layer, 0.85f, 2.3f, true, 0f, 0.52f, 0.04f),
		};
		float grow = Mathf.Clamp((hitAt - emergeAt) * 0.75f, 0.12f, 0.3f);
		const float retract = 0.3f;
		float total = burnAt + retract + 0.05f;
		foreach (var (L, x, hh, mirror, dim, slapAng, lag) in spec)
		{
			Vector2 ground = feet + new Vector2(x * h * k0, 6f);
			float height = hh * h * k0;
			Node2D root = Fx.Root(L, ground, "P5CurseHand", total + 0.1);
			float k = height / t0.GetHeight();
			ShaderMaterial m = new() { Shader = HandShader };
			Sprite2D sp = new()
			{
				Texture = tex[0], Centered = false, Offset = new Vector2(-t0.GetWidth() / 2f, -t0.GetHeight()),
				Scale = new Vector2(mirror ? -k : k, k), Material = m,
			};
			root.AddChild(sp);
			m.SetShaderParameter("rise", 0f);
			m.SetShaderParameter("dim", dim);
			float toward = mirror ? -1f : 1f;                              // 往敌人那边转的方向
			float e0 = emergeAt + lag;
			// 地面裂开（第一只手一声）
			if (lag <= 0f) Sfx.Play(Sfx.Crack, quiet ? 0.2f : 0.35f);
			Fx.After(layer, e0, () =>
			{
				Glow.Flash(L, ground, Pal5.Bright, height * 0.5f, 0.2f);
				Glow.Shockwave(L, ground, Pal5.Bright, height * 0.8f, 0.3f, 0.25f);
				Glow.Sparks(L, ground, 10, 160f, 480f, 0.4f, new[] { Pal5.Hot, Pal5.Bright, Pal5.Deep }, 700f, 0.08f, 0.2f, Vector2.Up, 40f);
			});
			Fx.After(layer, burnAt + retract * 0.8f, () =>
				Glow.Sparks(L, ground, 8, 80f, 260f, 0.35f, new[] { Pal5.Bright, Pal5.Deep }, 500f, 0.06f, 0.16f, Vector2.Up, 50f));
			int last = -1;
			Fx.Animate(root, 0, total, t =>
			{
				float s = t * total;
				float g = Mathf.Clamp((s - e0) / grow, 0f, 1f);
				float back01 = Mathf.Clamp((s - burnAt) / retract, 0f, 1f);
				// 手型
				int f = s < hitAt - 0.05f ? (g < 0.4f ? 0 : g < 0.75f ? 1 : 2) : s < burnAt ? 3 : 4;
				if (f != last)
				{
					last = f;
					sp.Texture = tex[f];
				}
				// 长出来 / 缩回去：地面以下的部分丢掉
				float rise = Fx.OutCubic(g) * (1f - Fx.InCubic(back01));
				m.SetShaderParameter("rise", rise);
				sp.Position = new Vector2(0f, (1f - rise) * height);
				// 转：长的时候轻轻扭 → 蓄力：往后仰到底、手臂拉长 → 最后 0.07s 猛地砸下去（指尖落在敌人身上）→
				// 撞上：压扁、微微弹起再落回 → 缩的时候回正
				const float strikeT = 0.07f;
				float wob = 0.12f * Mathf.Sin(g * Mathf.Pi * 2.2f) * (1f - g * 0.6f);
				float wind = Mathf.Clamp((s - e0 - grow) / Mathf.Max(0.01f, hitAt - strikeT - e0 - grow), 0f, 1f);
				float lean = -0.5f * Fx.OutCubic(wind);
				float stretch = 0.15f * Fx.OutCubic(wind);
				float strike = Mathf.Clamp((s - (hitAt - strikeT)) / strikeT, 0f, 1f);
				float after = Mathf.Max(0f, s - hitAt);
				float ang, sx = 1f, sy = 1f + stretch;
				if (s < hitAt - strikeT)
				{
					ang = wob + lean;
				}
				else if (s < hitAt)
				{
					float e = strike * strike * strike;                        // 越砸越快
					ang = Mathf.Lerp(lean, slapAng * 1.06f, e);
					sy = 1f + stretch * (1f - e) + 0.12f * e;
				}
				else
				{
					// 撞上：0.08s 压扁，之后弹起一点再慢慢落回
					float squash = Mathf.Exp(-after / 0.06f);
					ang = slapAng * (1.06f - 0.1f * Mathf.Sin(Mathf.Min(1f, after / 0.25f) * Mathf.Pi) * (1f - squash));
					sy = 1f - 0.16f * squash;
					sx = 1f + 0.1f * squash;
					if (s < burnAt) ang += 0.015f * Mathf.Sin(s * 45f);
				}
				ang *= 1f - Fx.OutCubic(back01);
				root.Rotation = ang * toward;
				root.Scale = new Vector2(sx, Mathf.Lerp(sy, 1f, back01));
				m.SetShaderParameter("hot", s >= hitAt ? 0.55f * Mathf.Exp(-(s - hitAt) / 0.05f) : 0f);   // 一闪就回黑，剪影要看得清
				m.SetShaderParameter("burn", 0f);
			});
			// 落点：这只手的指尖砸在哪（地面支点 + 转过去的手臂），炸一下
			float reachLen = height * 0.82f;
			Vector2 tip = ground + new Vector2(0f, -reachLen).Rotated(slapAng * 1.06f * toward);
			Fx.After(layer, hitAt, () =>
			{
				Glow.Flash(L, tip, Pal5.Hot, h * 0.7f, 0.1f);
				Glow.Flash(L, tip, Pal5.Bright, h * 1.4f, 0.24f);
				Glow.Shockwave(L, tip, Pal5.Bright, h * 1.5f, 0.3f, 0.6f);
				Glow.Sparks(L, tip, 14, 300f, 900f, 0.4f, new[] { Pal5.Hot, Pal5.Bright, Pal5.Deep }, 900f, 0.08f, 0.24f,
					new Vector2(-toward, -0.6f).Normalized(), 55f);
			});
		}
		// 声音：砸下去的重击 + 低频（冲击层）；缩回去时一声低沉的挥动（余韵）
		if (!quiet)
		{
			Sfx.PlayHit(Sfx.Slam, 1f, hitAt);
			Sfx.Play(Sfx.SubBoom, 0.75f, hitAt);
		}
		// 命中：三只手拍在敌人身上的那一下
		Fx.After(layer, hitAt, () =>
		{
			Glow.Flash(layer, center, Pal5.Hot, h * 1.1f, 0.12f);
			Glow.Flash(layer, center, Pal5.Bright, h * 2.2f, 0.28f);
			Glow.Shockwave(layer, center, Pal5.Hot, h * 2.4f, 0.36f);
			Glow.SpeedLines(layer, center, Pal5.Bright, 14, h * 0.4f, h * 2f, 0.3f);
			Glow.Ripple(layer, center, h * 3f, 0.4f, 0.03f);
			HitStop.Request(0.06);                                       // 砸上去那一下顿一顿
			Fx.Shake(2);
		});
	}

	/// <summary>一块热浪（扭动后面的画面），中心在 center（parent 的本地坐标），一直扭到被释放；alpha 参数控制强弱。</summary>
	public static ColorRect HazeRect(Node parent, Vector2 center, float w, float h, float strength = 0.006f)
	{
		ColorRect hz = new()
		{
			Size = new Vector2(w, h), Position = center - new Vector2(w / 2f, h / 2f), MouseFilter = Control.MouseFilterEnum.Ignore,
			Material = new ShaderMaterial { Shader = Haze },
		};
		parent.AddChild(hz);
		Fx.Clock(hz);
		((ShaderMaterial)hz.Material).SetShaderParameter("strength", strength);
		return hz;
	}

	/// <summary>打着旋往上飘的火星（GPU 粒子，湍流）。</summary>
	internal static void Embers(Control layer, Vector2 feet, float h, int amount, float life)
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
	internal static void Smoke(Control layer, Vector2 at, float h)
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
