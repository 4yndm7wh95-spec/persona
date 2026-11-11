using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>出技能 / 能力牌时特效要用到的位置（SkillPlay 从游戏里取，预览工具自己填）。</summary>
public sealed record SkillCtx(
	Control Front, Control? Back, Vector2 Feet, Vector2 Chest, Vector2 Hand, Vector2 Mask, float Tall, float Face,
	Action<Color>? SelfTint, IReadOnlyList<SkillFx.Victim> Enemies, SkillFx.Victim? Target)
{
	public float Unit => Fx.Screen(Front).Y / 1080f;
	/// <summary>复制此刻的 Joker（剪影残像用，SkillFx.Ghosts）；Region = 剪影离屏画面覆盖的屏幕区域。</summary>
	public Func<Node, (Node2D Clone, Transform2D Body)?>? CopyBody { get; init; }
	public Rect2 Region { get; init; }
	public Vector2 Head => Feet + new Vector2(0f, -Tall * 1.02f);
}

/// <summary>
/// 具体到每张牌的技能 / 能力牌特效（用户 2026-11-10：每张牌都要有自己的特效，再丰富一些，一批一批做）。
/// 第一批：增益 / 能力牌——灵动步法、精准、涂毒、毒雾、跟踪、爆发、群蛇形态、幽魂形态。
/// 全部着色器 / 多边形实时渲染，不用生图；音效克制（tools/p5/sfx/synth_card_fx1.py），形态类大牌才有张力。
/// </summary>
public static class CardSkillFx
{
	/// <summary>牌名 → 特效。SkillPlay 先查这里，查不到再走通用积木（SkillFx）。</summary>
	public static readonly Dictionary<string, Action<SkillCtx>> Cards = new()
	{
		["Footwork"] = Footwork,
		["Accuracy"] = Accuracy,
		["Envenom"] = Envenom,
		["NoxiousFumes"] = NoxiousFumes,
		["Tracking"] = Tracking,
		["Burst"] = Burst,
		["SerpentForm"] = SerpentForm,
		["WraithForm"] = WraithForm,
	};

	static CardSkillFx()
	{
		CardSkillFx2.Register(Cards);
		CardSkillFx3.Register(Cards);
	}

	private static readonly Color Hot = new(1f, 0.96f, 0.94f), Red = new(1f, 0.2f, 0.27f), Crimson = new(0.85f, 0.04f, 0.13f),
		Deep = new(0.3f, 0f, 0.07f), Ice = new(0.94f, 0.98f, 1f), Sky = new(0.4f, 0.8f, 1f), Blue = new(0.1f, 0.38f, 0.98f),
		Lilac = new(0.78f, 0.62f, 1f), Violet = new(0.42f, 0.2f, 0.9f), Night = new(0.08f, 0.02f, 0.2f);

	private const string Noise = @"
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
float fbm(vec2 p) { float v = 0.0, a = 0.5; for (int i = 0; i < 4; i++) { v += a * noise(p); p = p * 2.03 + vec2(1.7, 9.2); a *= 0.5; } return v; }
";

	private static ColorRect Quad(Node parent, Shader shader, Vector2 size, Vector2? pivot = null)
	{
		ColorRect r = new()
		{
			Size = size, Position = -(pivot ?? size / 2f), PivotOffset = pivot ?? size / 2f,
			MouseFilter = Control.MouseFilterEnum.Ignore, Material = new ShaderMaterial { Shader = shader },
		};
		parent.AddChild(r);
		return r;
	}

	private static void Set(ColorRect r, string name, Variant v) => ((ShaderMaterial)r.Material).SetShaderParameter(name, v);
	private static Vector3 V(Color c) => new(c.R, c.G, c.B);
	private static bool Ok(Control? c) => c != null && GodotObject.IsInstanceValid(c);

	// ═══════════════════════════ 灵动步法：脚下一步一步亮起的步法符文 ═══════════════════════════

	private static Shader? _rune;

	/// <summary>
	/// 贴地的步法符文（四边形是正方形，代码里压扁成地面透视）：外面一个菱形框、里面一个小菱形、四角往外伸的刻度、一圈细环；
	/// open 0→1 从中心张开，flare 亮一下，alpha 淡出。加法。
	/// </summary>
	private static Shader Rune => _rune ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform vec3 tint = vec3(0.4, 0.8, 1.0);
uniform vec3 hot = vec3(0.94, 0.98, 1.0);
uniform float open = 1.0;
uniform float flare = 0.0;
uniform float alpha = 1.0;
uniform float spin = 0.0;
float line(float d, float w) { return exp(-pow(d / w, 2.0)); }
void fragment() {
	vec2 p = (UV - 0.5) * 2.0 / max(open, 0.001);
	float c = cos(spin), s = sin(spin);
	vec2 q = vec2(c * p.x - s * p.y, s * p.x + c * p.y);
	float dia = abs(q.x) + abs(q.y);
	float v = line(dia - 0.78, 0.022) + line(dia - 0.5, 0.016) * 0.8 + line(length(p) - 0.92, 0.012) * 0.55;
	float axis = min(abs(q.x), abs(q.y));
	v += line(axis, 0.018) * step(0.8, dia) * step(dia, 1.02) * 1.2;
	float core = exp(-dot(p, p) * 9.0) * (0.3 + flare);
	float edge = 1.0 - smoothstep(0.95, 1.0, length(p));
	vec3 col = tint * (v * 0.9 + core * 0.8) + hot * (v * (0.35 + flare) + core * flare);
	COLOR = vec4(col * edge * alpha, 1.0);
}" };

	private static void StepRune(Control layer, Vector2 at, float size, float delay, Color tint, float spinDir)
	{
		Node2D root = Fx.Root(layer, at, "P5StepRune", delay + 0.75);
		ColorRect q = Quad(root, Rune, Vector2.One * size);
		q.Scale = new Vector2(1f, 0.3f);
		Set(q, "tint", V(tint));
		Set(q, "alpha", 0f);
		Fx.Animate(q, delay, 0.62, t =>
		{
			Set(q, "open", Mathf.Lerp(0.35f, 1f, Ease5.OutExpo(Ease5.Seg(t, 0f, 0.25f))));
			Set(q, "flare", Mathf.Exp(-t * 9f) * 1.6f);
			Set(q, "spin", spinDir * (0.6f * (1f - Ease5.OutExpo(t))));
			Set(q, "alpha", 1f - Fx.InCubic(Ease5.Seg(t, 0.45f, 0.55f)));
		});
	}

	/// <summary>
	/// 灵动步法（敏捷）：左、右、正中三下踩出步法符文（每一下一圈扁波、往两边贴地甩出风线、溅起几点光），
	/// 最后正中那一下亮透：一小道蓝光柱窜上来、身边掠过几道风线，两道「^」冲上去。轻快，0.75s。
	/// </summary>
	public static void Footwork(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit;
		Sfx.Play(Sfx.FootworkSteps, 0.42f);
		Vector2[] steps = { c.Feet + new Vector2(-h * 0.24f, h * 0.02f), c.Feet + new Vector2(h * 0.22f, -h * 0.01f), c.Feet };
		for (int i = 0; i < 3; i++)
		{
			Vector2 at = steps[i];
			float d = 0.1f * i, size = h * (i == 2 ? 1.05f : 0.62f);
			StepRune(c.Front, at, size, d, Sky, i % 2 == 0 ? 1f : -1f);
			Fx.After(c.Front, d, () =>
			{
				Glow.Shockwave(c.Front, at, Sky, size * 1.3f, 0.34f, 0.26f);
				Glow.Sparks(c.Front, at, 8, 120f, 360f, 0.35f, new[] { Ice, Sky, Blue }, 500f, 0.02f * u, 0.05f * u, Vector2.Up, 70f);
				for (int k = 0; k < 2; k++)
				{
					float side = k == 0 ? -1f : 1f;
					Streak(c.Front, at + new Vector2(side * h * 0.08f, -h * Fx.Rand(0.01f, 0.06f)), new Vector2(side, 0f), h * Fx.Rand(0.35f, 0.55f), 0.24f, Ice, Sky, 3f * u);
				}
			});
		}
		Fx.After(c.Front, 0.22f, () =>
		{
			Glow.Flash(c.Front, c.Feet, Sky, h * 0.8f, 0.18f);
			SkillFx.ColumnFx(c.Front, c.Back, c.Feet, h * 0.85f, SkillFx.Tone.Blue, 0.5f);
			for (int i = 0; i < 6; i++)
			{
				float side = i % 2 == 0 ? -1f : 1f;
				Streak(c.Front, c.Feet + new Vector2(side * h * Fx.Rand(0.15f, 0.32f), -h * Fx.Rand(0.05f, 0.35f)), Vector2.Up, h * Fx.Rand(0.5f, 0.8f),
					0.3f, Ice, Sky, 3.5f * u, 0.03f * i);
			}
			for (int i = 0; i < 2; i++)
			{
				SkillFx.RiseChevron(c.Front, c.Feet + new Vector2(0f, -h * 0.3f), c.Head + new Vector2(0f, -h * 0.1f * i), h * (0.42f - 0.08f * i), 0.06f + 0.08f * i, Sky, Ice);
			}
		});
	}

	/// <summary>一道直的光痕：从 at 沿 dir 掠出去 len，头快尾慢，0.25～0.4s。</summary>
	private static void Streak(Control layer, Vector2 at, Vector2 dir, float len, float life, Color core, Color glow, float width, float delay = 0f)
	{
		Node2D root = Fx.Root(layer, at, "P5Streak", delay + life + 0.1);
		Line2D g = Glow.Line(root, new[] { Vector2.Zero, dir }, new Color(glow, 0.85f), width * 3f, false);
		Line2D k = Glow.Line(root, new[] { Vector2.Zero, dir }, core, width, false);
		root.Visible = false;
		Fx.Animate(root, delay, life, t =>
		{
			root.Visible = true;
			Vector2 a = dir * len * Ease5.OutQuint(t), b = dir * len * Fx.InCubic(t);
			if (a.DistanceTo(b) < 1f) b = a - dir;
			g.Points = k.Points = new[] { a, b };
			root.Modulate = new Color(1f, 1f, 1f, 1f - Ease5.Seg(t, 0.65f, 0.35f));
		});
	}

	// ═══════════════════════════ 精准 / 跟踪：准星 ═══════════════════════════

	private static Shader? _reticle;

	/// <summary>
	/// 准星：外圈一道有四个缺口的环、一圈刻度、内圈虚线环、中间十字（中心留空），lock 时中心一点亮起。spin 让外圈 / 刻度 / 内圈各自按不同速度转。加法。
	/// </summary>
	private static Shader Reticle => _reticle ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform vec3 tint = vec3(1.0, 0.2, 0.27);
uniform vec3 hot = vec3(1.0, 0.96, 0.94);
uniform float spin = 0.0;
uniform float lock = 0.0;
uniform float alpha = 1.0;
float ring(float r, float R, float w) { return exp(-pow((r - R) / w, 2.0)); }
void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	float r = length(p), a = atan(p.y, p.x);
	float gaps = smoothstep(0.12, 0.3, abs(sin((a + spin) * 2.0)));
	float v = ring(r, 0.86, 0.013) * gaps;
	float band = step(0.66, r) * step(r, 0.76);
	v += band * pow(abs(cos((a - spin * 1.6) * 12.0)), 60.0) * 0.9;
	v += ring(r, 0.42, 0.009) * step(0.0, sin((a + spin * 2.5) * 10.0)) * 0.8;
	float cx = exp(-pow(p.y / 0.009, 2.0)) * step(0.14, abs(p.x)) * step(abs(p.x), 0.6);
	float cy = exp(-pow(p.x / 0.009, 2.0)) * step(0.14, abs(p.y)) * step(abs(p.y), 0.6);
	v += (cx + cy) * 0.9;
	float dotc = exp(-r * r / 0.002) * lock;
	float glow = ring(r, 0.86, 0.07) * 0.22 * gaps;
	vec3 col = tint * (v + glow) + hot * (v * (0.3 + 0.7 * lock) + dotc * 2.0);
	COLOR = vec4(col * alpha * (1.0 - smoothstep(0.95, 1.0, r)), 1.0);
}" };

	/// <summary>准星从 from 倍大一边转一边收到 at，lock 的那一刻四个角括号扣上来、中心一亮，停一下，缩小淡出。返回锁住的时刻。</summary>
	private static float LockOn(Control layer, Vector2 at, float size, float delay, Color tint, float from = 2.2f, float hold = 0.28f, bool sound = true)
	{
		const float close = 0.24f;
		float u = Fx.Screen(layer).Y / 1080f;
		Node2D root = Fx.Root(layer, at, "P5Reticle", delay + close + hold + 0.35);
		ColorRect q = Quad(root, Reticle, Vector2.One * size);
		Set(q, "tint", V(tint));
		Set(q, "alpha", 0f);
		float spin0 = Fx.Rand(0f, 3f);
		Fx.Animate(q, delay, close + hold + 0.25, t =>
		{
			float s = t * (close + hold + 0.25f);
			float k = Ease5.OutExpo(Mathf.Min(1f, s / close));
			float outT = Mathf.Clamp((s - close - hold) / 0.25f, 0f, 1f);
			q.Scale = Vector2.One * (Mathf.Lerp(from, 1f, k) * (1f - 0.4f * Fx.InCubic(outT)));
			Set(q, "spin", spin0 + (1f - k) * 2.6f + s * 0.4f);
			Set(q, "lock", s >= close ? Mathf.Exp(-(s - close) * 6f) * 1.4f + 0.4f : 0f);
			Set(q, "alpha", Mathf.Min(1f, s / 0.06f) * (1f - outT));
		});
		Fx.After(layer, delay + close, () =>
		{
			if (sound) Sfx.Play(Sfx.ReticleLock, 0.36f);
			Glow.Flash(layer, at, tint, size * 0.7f, 0.14f);
			Glow.Glint(layer, at, 0.7f);
			Brackets(layer, at, size * 0.62f, tint, hold + 0.15f, u);
		});
		return delay + close;
	}

	/// <summary>四个角括号从外面「咔」地扣到 half 的位置，停 life 秒淡掉。</summary>
	private static void Brackets(Control layer, Vector2 at, float half, Color tint, float life, float u)
	{
		Node2D root = Fx.Root(layer, at, "P5Brackets", life + 0.2);
		float arm = half * 0.32f, th = Mathf.Max(2f, 3.5f * u);
		var corners = new List<Node2D>();
		for (int i = 0; i < 4; i++)
		{
			Vector2 sgn = new(i % 2 == 0 ? -1f : 1f, i < 2 ? -1f : 1f);
			Node2D n = new();
			root.AddChild(n);
			Vector2[] L = { Vector2.Zero, new(-sgn.X * arm, 0f), new(-sgn.X * arm, -sgn.Y * th), new(-sgn.X * th, -sgn.Y * th), new(-sgn.X * th, -sgn.Y * arm), new(0f, -sgn.Y * arm) };
			Fx.Poly(n, L, new Color(tint, 0.9f), additive: true).Scale = Vector2.One * 1.25f;
			Fx.Poly(n, L, Hot, additive: true);
			n.SetMeta("sgn", sgn);
			corners.Add(n);
		}
		Fx.Animate(root, 0, life, t =>
		{
			float k = Ease5.OutBack(Ease5.Seg(t, 0f, 0.25f));
			foreach (Node2D n in corners)
			{
				Vector2 sgn = (Vector2)n.GetMeta("sgn");
				n.Position = sgn * half * Mathf.Lerp(1.6f, 1f, k);
			}
			root.Modulate = new Color(1f, 1f, 1f, 1f - Ease5.Seg(t, 0.7f, 0.3f));
		});
	}

	/// <summary>
	/// 精准（小刀伤害提高）：Joker 手边，一个红色准星从两倍大转着收拢、扣上四个角括号、中心一亮；锁住的那一刻
	/// 一道细长的瞄准线从准星朝敌人方向「嗖」地拉出去，沿线掠过几点火星；身上一圈红环弹开，头顶一点白星。0.8s。
	/// </summary>
	public static void Accuracy(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit;
		Vector2 at = c.Chest + new Vector2(c.Face * h * 0.42f, -h * 0.05f);
		float lockAt = LockOn(c.Front, at, h * 0.62f, 0f, Red);
		Fx.After(c.Front, lockAt, () =>
		{
			Vector2 dir = new(c.Face, 0f);
			float reach = Fx.Screen(c.Front).X * 0.55f;
			Streak(c.Front, at + dir * h * 0.25f, dir, reach, 0.32f, Hot, Red, 2.2f * u);
			Streak(c.Front, at + dir * h * 0.25f + new Vector2(0f, h * 0.02f), dir, reach * 0.8f, 0.28f, Hot, Crimson, 1.2f * u, 0.03f);
			Glow.Sparks(c.Front, at + dir * h * 0.3f, 10, 300f, 900f, 0.3f, new[] { Hot, Red }, 0f, 0.02f * u, 0.05f * u, dir, 8f);
			Node2D rr = Fx.Root(c.Front, c.Chest, "P5AccRing", 0.6);
			ColorRect ring = Glow.RingRect(rr, h * 1.4f, 0.32f);
			Glow.SetParam(ring, "tint", Red);
			Fx.Animate(ring, 0, 0.45, t =>
			{
				float e = Ease5.OutExpo(t);
				Glow.SetParam(ring, "radius", Mathf.Lerp(0.2f, 0.92f, e));
				Glow.SetParam(ring, "width", Mathf.Lerp(0.03f, 0.006f, e));
				Glow.SetParam(ring, "alpha", 1f - Fx.InCubic(t));
			});
			Glow.Glint(c.Front, c.Head + new Vector2(h * 0.1f, -h * 0.05f), 0.8f, 0.08f);
		});
	}

	/// <summary>
	/// 跟踪（虚弱的敌人受到的攻击伤害更高）：面具上一点寒光，几道细红线从面具「嗖」地连到每个敌人，线头上准星收拢锁住，
	/// 被锁住的敌人身上红光一闪。一个一个依次锁，节奏快。
	/// </summary>
	public static void Tracking(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit;
		Glow.Glint(c.Front, c.Mask + new Vector2(c.Face * h * 0.03f, 0f), 0.9f);
		Glow.Flash(c.Front, c.Mask, Red, h * 0.35f, 0.16f);
		var targets = new List<SkillFx.Victim>(c.Enemies);
		if (targets.Count == 0 && c.Target is SkillFx.Victim only) targets.Add(only);
		Sfx.Play(Sfx.TrackScan, 0.36f);
		for (int i = 0; i < targets.Count; i++)
		{
			SkillFx.Victim v = targets[i];
			Vector2 at = v.Feet.Lerp(v.Top, 0.55f);
			float d = 0.07f + 0.11f * i, size = Mathf.Clamp(v.Width * 0.9f, h * 0.35f, h * 0.7f);
			Fx.After(c.Front, d, () =>
			{
				Vector2 from = c.Mask, dir = (at - from).Normalized();
				float len = from.DistanceTo(at);
				Streak(c.Front, from, dir, len, 0.22f, Hot, Red, 1.6f * u);
			});
			float lockAt = LockOn(c.Front, at, size, d + 0.12f, Red, 1.8f, 0.3f, i == 0);
			Fx.After(c.Front, lockAt, () =>
			{
				if (v.Tint is { } tint)
				{
					Node2D tick = Fx.Root(c.Front, at, "P5TrackTint", 0.5);
					Fx.Animate(tick, 0, 0.4, t => tint(Colors.White.Lerp(new Color(1.6f, 0.55f, 0.6f), Mathf.Exp(-t * 5f) * 0.9f)));
					tick.TreeExiting += () => tint(Colors.White);
				}
			});
		}
	}

	// ═══════════════════════════ 涂毒：手上裹一层咒毒 ═══════════════════════════

	private static Shader? _venom;

	/// <summary>
	/// 裹在手上的咒毒：一团不停翻滚的液面（噪声起伏的边界），里面近黑的暗红、边上一圈绯红到粉白的高光，
	/// 下半边往下拉长、拖出几条要滴下去的液柱。grow 0→1 裹上来，alpha 淡出。预乘混合（暗的部分真的压暗）。
	/// </summary>
	private static Shader Venom => _venom ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform float grow = 1.0;
uniform float alpha = 1.0;
uniform float seed = 0.0;
uniform float clock = 0.0;
" + Noise + @"
void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	float down = max(p.y, 0.0);
	float col_id = floor((p.x + 1.0) * 5.0);
	float drip = step(0.55, hash(vec2(col_id, seed))) * smoothstep(0.0, 1.0, grow) * 0.6;
	vec2 q = vec2(p.x, p.y - down * down * 0.9 * drip * (0.6 + 0.4 * sin(clock * 3.0 + col_id)));
	float n = fbm(q * 2.6 + vec2(seed, -clock * 1.3)) * 0.55 + noise(q * 8.0 + vec2(clock * 2.0, seed)) * 0.15;
	float R = 0.16 + 0.2 * grow;
	float d = length(q * vec2(1.0, 1.1)) - R - (n - 0.35) * 0.28 * grow;
	float inside = 1.0 - smoothstep(-0.015, 0.015, d);
	float rim = exp(-abs(d) / 0.035);
	float hi = pow(rim, 4.0) * smoothstep(0.2, 0.8, noise(q * 5.0 - clock * 1.5 + seed));
	vec3 col = vec3(0.16, 0.0, 0.04) * inside + vec3(0.85, 0.05, 0.16) * rim * 0.9 + vec3(1.0, 0.75, 0.72) * hi * 0.9;
	float keep = 1.0 - smoothstep(0.6, 0.95, length(p));
	float a = clamp(inside * 0.7 + rim * 0.4, 0.0, 1.0) * alpha * keep;
	COLOR = vec4(col * alpha * keep, a);
}" };

	/// <summary>
	/// 涂毒（攻击造成未格挡伤害时施毒）：一团翻滚的咒毒从手心涌出、把手裹住（下面拉出要滴的液柱），毒液一滴滴落到地上溅开小花，
	/// 手里冒起一缕魂火；最后「滋」地一亮收进手里，脚下一圈暗红的扁波。1s。
	/// </summary>
	public static void Envenom(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit;
		Sfx.Play(Sfx.VenomCoat, 0.4f);
		Node2D root = Fx.Root(c.Front, c.Hand, "P5Venom", 1.1);
		ColorRect q = Quad(root, Venom, Vector2.One * h * 0.55f);
		Set(q, "seed", Fx.Rand(0f, 40f));
		Fx.Clock(q);
		Fx.Animate(q, 0, 1.0, t =>
		{
			Set(q, "grow", Ease5.OutBack(Ease5.Seg(t, 0f, 0.3f)) * (1f - 0.85f * Fx.InCubic(Ease5.Seg(t, 0.75f, 0.25f))));
			Set(q, "alpha", 1f - Ease5.Seg(t, 0.8f, 0.2f));
		});
		Glow.Flash(c.Front, c.Hand, Crimson, h * 0.45f, 0.2f);
		// 滴落
		float ground = c.Feet.Y;
		for (int i = 0; i < 5; i++)
		{
			float d = 0.18f + 0.12f * i + Fx.Rand(-0.03f, 0.03f);
			Vector2 from = c.Hand + new Vector2(Fx.Rand(-0.08f, 0.08f) * h, h * 0.1f);
			Drop(c.Front, from, ground + Fx.Rand(-0.02f, 0.03f) * h, d, h, u);
		}
		Paint.Rise(c.Front, "curse_soul", c.Hand + new Vector2(0f, -h * 0.05f), h * 0.4f, 0.45f, Lut5.Curse, 0.75f, 0.12f);
		Fx.After(c.Front, 0.78f, () =>
		{
			Glow.Flash(c.Front, c.Hand, Red, h * 0.3f, 0.12f);
			Glow.Sparks(c.Front, c.Hand, 10, 80f, 240f, 0.35f, new[] { Hot, Red, Deep }, -60f, 0.02f * u, 0.05f * u, Vector2.Up, 120f);
			Glow.Shockwave(c.Front, c.Feet, Crimson, h * 1.2f, 0.4f, 0.24f);
		});
	}

	/// <summary>一滴毒液：从 from 落到 groundY（加速），落地溅起一小圈和几点飞沫。</summary>
	private static void Drop(Control layer, Vector2 from, float groundY, float delay, float h, float u)
	{
		float fall = Mathf.Max(0.12f, Mathf.Sqrt(Mathf.Max(1f, groundY - from.Y) / (h * 4.5f)));
		Node2D root = Fx.Root(layer, from, "P5Drop", delay + fall + 0.1);
		Sprite2D glow = Glow.SoftSprite(root, Vector2.Zero, new Color(0.9f, 0.08f, 0.18f, 0.9f), 0.35f * u);
		Polygon2D body = Fx.Poly(root, new[] { new Vector2(0f, -7f), new Vector2(3.2f, 1f), new Vector2(0f, 4f), new Vector2(-3.2f, 1f) }, new Color(0.22f, 0f, 0.05f));
		body.Scale = Vector2.One * u * 1.3f;
		root.Visible = false;
		Fx.Animate(root, delay, fall, t =>
		{
			root.Visible = true;
			root.GlobalPosition = new Vector2(from.X, Mathf.Lerp(from.Y, groundY, t * t));
			body.Scale = new Vector2(1f, 1f + t * 0.8f) * u * 1.3f;
		});
		Fx.After(layer, delay + fall, () =>
		{
			Vector2 at = new(from.X, groundY);
			Glow.Shockwave(layer, at, Crimson, h * 0.22f, 0.25f, 0.3f);
			Glow.Sparks(layer, at, 5, 60f, 160f, 0.25f, new[] { Red, Deep }, 600f, 0.015f * u, 0.035f * u, Vector2.Up, 60f);
		});
	}

	// ═══════════════════════════ 毒雾：贴地往敌人那边涌的咒怨雾 ═══════════════════════════

	private static Shader? _fog;

	/// <summary>
	/// 贴地的雾带（四边形：x 0 = Joker 脚边 → 1 = 最远，y 0 地面 → 1 雾能到的最高处）。fbm 翻滚的雾体，雾顶起伏，
	/// 前沿 front 0→1 往外涌（前沿带噪声、卷起来），雾里暗红、雾顶一圈稍亮的边；整体 alpha 淡出。预乘混合：雾主要是压暗，带一点红光。
	/// </summary>
	private static Shader Fog => _fog ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform float front = 1.0;
uniform float alpha = 1.0;
uniform float seed = 0.0;
uniform float clock = 0.0;
uniform vec3 deep = vec3(0.12, 0.0, 0.03);
uniform vec3 mid = vec3(0.75, 0.04, 0.14);
" + Noise + @"
void fragment() {
	float x = UV.x, y = 1.0 - UV.y;
	float n = fbm(vec2(x * 5.0 - clock * 0.45, y * 2.4 + clock * 0.12) + seed);
	float n2 = fbm(vec2(x * 12.0 + clock * 0.3, y * 5.0 - clock * 0.4) + seed * 1.7);
	float top = 0.25 + 0.5 * n * (0.7 + 0.3 * smoothstep(front - 0.25, front, x));
	float body = (1.0 - smoothstep(top - 0.45, top, y)) * mix(1.0, 0.45, y / max(top, 0.01));
	float edge = x + (n2 - 0.5) * 0.12;
	float lead = 1.0 - smoothstep(front - 0.1, front + 0.02, edge);
	float base = smoothstep(0.0, 0.05, x) * (1.0 - smoothstep(0.9, 1.0, x));
	float holes = smoothstep(0.25, 0.65, fbm(vec2(x * 9.0 - clock * 0.6, y * 4.0) + seed * 3.1));
	float d = body * lead * base * (0.25 + 0.75 * n2) * mix(0.35, 1.0, holes) * (1.0 - smoothstep(0.8, 1.0, y));
	float rim = exp(-abs(y - top + 0.06) / 0.05) * lead * base;
	vec3 col = mix(deep, mid, clamp(pow(n2 * n * 2.0, 2.0), 0.0, 1.0)) * d + mid * rim * 0.4 * (0.4 + n2) * (1.0 - smoothstep(0.8, 1.0, y));
	float a = clamp(d * 0.85, 0.0, 0.85) * alpha;
	COLOR = vec4(col * alpha, a);
}" };

	/// <summary>
	/// 毒雾（每回合开始给所有敌人施毒）：脚下一圈暗红咒印似的扁波，一层咒怨雾贴着地从 Joker 脚边朝敌人那边涌过去（0.6s 涌到头），
	/// 雾里一路冒起魂火；雾漫过哪个敌人，那个敌人暗红一闪；雾停一会儿慢慢散掉。1.6s。
	/// </summary>
	public static void NoxiousFumes(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit;
		Sfx.Play(Sfx.FumesRoll, 0.42f);
		Vector2 scr = Fx.Screen(c.Front);
		float far = c.Face > 0f ? scr.X * 0.98f : scr.X * 0.02f;
		Vector2 start = c.Feet + new Vector2(-c.Face * h * 0.35f, h * 0.06f);
		float len = Mathf.Abs(far - start.X), fh = h * 1.3f;
		const float roll = 0.6f, life = 1.6f;
		foreach ((Control? layer, float a) in new[] { (c.Back, 0.85f), ((Control?)c.Front, 0.5f) })
		{
			if (!Ok(layer)) continue;
			Node2D root = Fx.Root(layer!, start, "P5Fumes", life + 0.1);
			ColorRect q = Quad(root, Fog, new Vector2(len, fh), new Vector2(0f, fh));
			if (c.Face < 0f) q.Scale = new Vector2(-1f, 1f);
			Set(q, "seed", Fx.Rand(0f, 50f));
			Fx.Clock(q);
			Fx.Animate(q, 0, life, t =>
			{
				float s = t * life;
				Set(q, "front", 0.04f + 0.98f * Fx.OutCubic(Mathf.Min(1f, s / roll)));
				Set(q, "alpha", a * Mathf.Min(1f, s / 0.1f) * (1f - Ease5.Seg(s, 0.9f, 0.7f)));
			});
		}
		Glow.Shockwave(c.Front, c.Feet, Crimson, h * 1.5f, 0.45f, 0.22f);
		Glow.Flash(c.Front, c.Feet, Crimson, h * 0.6f, 0.2f);
		for (int i = 0; i < 6; i++)
		{
			float k = (i + 0.5f) / 6f;
			Vector2 at = new(Mathf.Lerp(start.X, far, k * 0.9f), c.Feet.Y - h * Fx.Rand(0.0f, 0.12f));
			Paint.Rise(c.Front, i % 3 == 2 ? "curse_ghost" : "curse_soul", at, h * Fx.Rand(0.45f, 0.7f), Fx.Rand(0.6f, 0.85f), Lut5.Curse, 0.9f, roll * k + 0.05f);
		}
		foreach (SkillFx.Victim v in c.Enemies)
		{
			float k = Mathf.Clamp(Mathf.Abs(v.Feet.X - start.X) / len, 0f, 1f);
			float when = roll * (1f - Mathf.Pow(1f - k, 1f / 3f)) + 0.04f;
			Fx.After(c.Front, when, () =>
			{
				Glow.Sparks(c.Front, v.Feet, 10, 40f, 160f, 0.6f, new[] { Red, Crimson, Deep }, -80f, 0.03f * u, 0.07f * u, Vector2.Up, 40f);
				if (v.Tint is { } tint)
				{
					Node2D tick = Fx.Root(c.Front, v.Feet, "P5FumeTint", 0.6);
					Fx.Animate(tick, 0, 0.5, t => tint(Colors.White.Lerp(new Color(0.75f, 0.3f, 0.4f), Mathf.Sin(t * Mathf.Pi) * 0.7f)));
					tick.TreeExiting += () => tint(Colors.White);
				}
			});
		}
	}

	// ═══════════════════════════ 爆发：一声两响的回声 ═══════════════════════════

	/// <summary>
	/// 爆发（下一张技能牌打两次）：一切都来两遍——胸口炸开一圈红光，紧跟着错开一点的第二圈「回声」；脚下光柱冲上去两次；
	/// 两列「^」左右并排冲上头顶；最后身后一个大的红色光爆和一下短震。1s。
	/// </summary>
	public static void Burst(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit;
		Sfx.Play(Sfx.BurstEcho, 0.45f);
		for (int e = 0; e < 2; e++)
		{
			float d = 0.16f * e;
			Vector2 off = new(c.Face * h * 0.06f * e, -h * 0.03f * e);
			Fx.After(c.Front, d, () =>
			{
				Glow.Flash(c.Front, c.Chest + off, e == 0 ? Red : Hot, h * (0.7f + 0.2f * e), 0.16f);
				Glow.Shockwave(c.Front, c.Chest + off, e == 0 ? Red : Hot, h * (1.6f + 0.4f * e), 0.4f, 0.55f);
				Glow.Shockwave(c.Front, c.Feet, Red, h * (1.4f + 0.5f * e), 0.42f, 0.22f);
				SkillFx.ColumnFx(c.Front, c.Back, c.Feet, h * (0.95f + 0.15f * e), SkillFx.Tone.Red, 0.45f);
				Glow.Sparks(c.Front, c.Chest + off, 12, 200f, 600f, 0.35f, new[] { Hot, Red, Crimson }, 300f, 0.02f * u, 0.06f * u, Vector2.Up, 160f);
			});
		}
		for (int col = 0; col < 2; col++)
		{
			float x = (col == 0 ? -1f : 1f) * h * 0.2f;
			for (int i = 0; i < 3; i++)
			{
				SkillFx.RiseChevron(c.Front, c.Feet + new Vector2(x, -h * 0.3f), c.Head + new Vector2(x, -h * (0.05f + 0.1f * i)), h * (0.34f - 0.05f * i),
					0.05f + 0.07f * i + 0.12f * col, Red, Hot);
			}
		}
		Fx.After(c.Front, 0.36f, () =>
		{
			if (Ok(c.Back)) Glow.Flash(c.Back!, c.Chest, Crimson, h * 2.0f, 0.3f);
			Glow.SpeedLines(c.Front, c.Chest, Hot, 14, h * 0.35f, h * 0.9f, 0.3f);
			Fx.Shake(1);
		});
	}

	// ═══════════════════════════ 群蛇形态：光蛇升龙（Vfx/SerpentAscend.cs）+ 常驻的蛇（Vfx/SerpentMantle.cs） ═══════════════════════════

	/// <summary>
	/// 群蛇形态（2026-11-11 第三版，用户：要霸气）：光做的巨蛇螺旋冲上去、昂首吼、俯冲扎进 Joker 胸口；之后常驻的蛇从他身上浮现
	/// （能力挂上时由 SerpentFormVfxPatch 开始，这里只放起手）。原来的咒怨之蛇（三条带子蛇）撤掉。
	/// </summary>
	public static void SerpentForm(SkillCtx c)
	{
		Sfx.Play(Sfx.SerpentRise, 0.62f);
		SerpentAscend.Play(c.Front, c.Back ?? c.Front, c.Feet, c.Tall, c.Face, c.SelfTint);
	}

	// ═══════════════════════════ 幽魂形态：化作幽魂 ═══════════════════════════

	private static Shader? _vortex;

	/// <summary>
	/// 脚下的幽冥漩涡（正方形四边形，代码里压扁）：四条往里卷的旋臂，暗紫近黑，臂的边缘一道淡紫的光；中心更黑。open 张开、spin 转、alpha 淡出。预乘。
	/// </summary>
	private static Shader Vortex => _vortex ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform float open = 1.0;
uniform float spin = 0.0;
uniform float alpha = 1.0;
uniform float seed = 0.0;
" + Noise + @"
void fragment() {
	vec2 p = (UV - 0.5) * 2.0 / max(open, 0.001);
	float r = length(p), a = atan(p.y, p.x);
	float arm = sin(a * 4.0 + log(max(r, 0.02)) * 6.0 - spin * 5.0);
	float n = fbm(vec2(a * 2.0 + spin, r * 4.0 - spin * 2.0) + seed);
	float band = smoothstep(0.1, 0.7, arm + (n - 0.5) * 0.8);
	float rim = exp(-abs(arm - 0.35) / 0.12) * (0.5 + n);
	float disk = 1.0 - smoothstep(0.75, 1.0, r);
	float hole = exp(-r * r * 6.0);
	vec3 col = vec3(0.52, 0.36, 1.0) * rim * 0.75 * disk + vec3(0.85, 0.75, 1.0) * pow(rim, 3.0) * 0.4 * disk;
	float a2 = clamp((band * 0.6 + hole * 0.8) * disk, 0.0, 0.9) * alpha;
	COLOR = vec4(col * alpha, a2);
}" };

	/// <summary>
	/// 幽魂形态（获得无实体）：脚下张开一个暗紫的幽冥漩涡，Joker 被抽走颜色、变成半透明的暗紫（像魂），身边升起一缕缕亡魂、
	/// 一圈圈淡紫的光往里收；漩涡合上时一记反白的闷闪、短震，人慢慢恢复。1.5s。
	/// </summary>
	public static void WraithForm(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit;
		Sfx.Play(Sfx.WraithForm, 0.6f);
		Control layer = Ok(c.Back) ? c.Back! : c.Front;
		Node2D root = Fx.Root(layer, c.Feet, "P5Vortex", 1.6);
		ColorRect q = Quad(root, Vortex, Vector2.One * h * 2.2f);
		q.Scale = new Vector2(1f, 0.32f);
		Set(q, "seed", Fx.Rand(0f, 30f));
		Fx.Animate(q, 0, 1.5, t =>
		{
			Set(q, "open", Ease5.OutExpo(Ease5.Seg(t, 0f, 0.25f)) * (1f - 0.7f * Fx.InCubic(Ease5.Seg(t, 0.7f, 0.3f))));
			Set(q, "spin", t * 2.4f);
			Set(q, "alpha", 1f - Ease5.Seg(t, 0.8f, 0.2f));
		});
		// 往里收的环
		for (int i = 0; i < 3; i++)
		{
			Node2D rr = Fx.Root(c.Front, c.Chest, "P5WraithRing", 1.0);
			ColorRect ring = Glow.RingRect(rr, h * 2.4f, 0.45f);
			Glow.SetParam(ring, "tint", Lilac);
			float d = 0.1f + 0.14f * i;
			Glow.SetParam(ring, "alpha", 0f);
			Fx.Animate(ring, d, 0.45, t =>
			{
				float e = Fx.InCubic(t);
				Glow.SetParam(ring, "radius", Mathf.Lerp(0.95f, 0.1f, e));
				Glow.SetParam(ring, "width", Mathf.Lerp(0.008f, 0.03f, e));
				Glow.SetParam(ring, "alpha", Mathf.Min(1f, t * 5f) * (1f - Ease5.Seg(t, 0.8f, 0.2f)) * 0.8f);
			});
		}
		// 亡魂
		for (int i = 0; i < 7; i++)
		{
			Vector2 at = c.Feet + new Vector2(Fx.Rand(-0.45f, 0.45f) * h, -h * Fx.Rand(0f, 0.3f));
			Paint.Rise(c.Front, i % 2 == 0 ? "curse_ghost" : "curse_soul", at, h * Fx.Rand(0.5f, 0.9f), Fx.Rand(0.5f, 0.75f), Lut5.PersonaAura, 0.9f, 0.05f + 0.09f * i);
		}
		Glow.Sparks(c.Front, c.Feet, 30, 50f, 220f, 1.2f, new[] { Hot, Lilac, Violet, Night }, -110f, 0.03f * u, 0.08f * u, Vector2.Up, 70f);
		// 人变成魂
		if (c.SelfTint is { } tint)
		{
			Node2D tick = Fx.Root(c.Front, c.Feet, "P5WraithTint", 1.6);
			Color ghost = new(0.55f, 0.5f, 0.95f, 0.5f);
			Fx.Animate(tick, 0, 1.5, t =>
			{
				float k = Ease5.Seg(t, 0f, 0.2f) * (1f - Ease5.Seg(t, 0.65f, 0.35f));
				float flicker = 1f - 0.12f * Mathf.Sin(t * 60f) * k;
				tint(Colors.White.Lerp(ghost, k * flicker));
			});
			tick.TreeExiting += () => tint(Colors.White);
		}
		Fx.After(c.Front, 0.55f, () =>
		{
			Glow.Flash(c.Front, c.Chest, Lilac, h * 1.1f, 0.22f);
			Glow.Shockwave(c.Front, c.Feet, Lilac, h * 2.0f, 0.5f, 0.3f);
			Fx.Shake(2);
		});
	}
}
