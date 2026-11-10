using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 技能 / 能力牌的出牌特效（用户 2026-11-10：把还只有人物动作、没有特效的牌一次做完，音效也加上）。
/// 全部着色器实时渲染，不用贴图；材质照 docs/p5/FX_RULES.md：光 = 白芯 + 红晕，人格面具 / 防御相关 = 蓝焰色，毒 = 咒怨的暗红。
/// 按效果分成几族，每族一个积木，各张牌在 SkillPlay（游戏侧）里拼：
///  · Buff / Debuff —— 螺旋光带：自己身上往上盘（增益），敌人身上往下压（减益）。前后两层各画半圈，绕着身体有前后遮挡；
///  · Draw / Discard / Plan —— 光牌：从扯手套的手里弹出来，飞进手牌 / 烧掉 / 绕身一圈；
///  · Load —— 装弹：小刀牌 = 子弹，几发黄铜子弹从手里抛起、翻转、依次落回；
///  · Surge —— 能量：四周的光丝吸进胸口，一圈光环弹开；
///  · Wail —— 尖啸：从面具朝敌人推过去的一串弧形声波；
///  · TimeSlow —— 子弹时间：钟面刻度绕着 Joker 减速、停住，四周压暗；
///  · Ghosts / Veil —— 潜行：身后拉出残像；一身暗影裹上来；
///  · Shatter / Mark —— 暴露（护甲碎裂）、夹击（两侧合拢的标记）。
/// 普通牌一个特效不超过 0.6s、一声短音；同一时刻不叠第二个声音。
/// </summary>
public static class SkillFx
{
	public enum Tone
	{
		Blue,     // 人格面具蓝焰：防御 / 敏捷 / 潜行
		Red,      // 白芯红晕：进攻类增益、能量
		Curse,    // 咒怨暗红：毒、压在敌人身上的减益
		Shadow,   // 暗影：深蓝黑
	}

	private static (Color Hot, Color Mid, Color Deep, float Dark) Pal(Tone t) => t switch
	{
		Tone.Blue => (new Color(0.94f, 0.98f, 1f), new Color(0.4f, 0.8f, 1f), new Color(0.08f, 0.32f, 0.95f), 0f),
		Tone.Curse => (new Color(1f, 0.55f, 0.6f), new Color(0.6f, 0.02f, 0.12f), new Color(0.22f, 0f, 0.06f), 1f),
		Tone.Shadow => (new Color(0.62f, 0.6f, 1f), new Color(0.26f, 0.18f, 0.72f), new Color(0.06f, 0.03f, 0.22f), 1f),
		_ => (new Color(1f, 0.96f, 0.94f), new Color(1f, 0.32f, 0.36f), new Color(0.8f, 0.05f, 0.14f), 0f),
	};

	private const string Noise = @"
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
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

	private static float Unit(Control layer) => Fx.Screen(layer).Y / 1080f;

	// ───────────────────────── 螺旋光带（增益 / 减益） ─────────────────────────

	private static Shader? _helix;

	/// <summary>
	/// 绕身体盘旋的光带。一张竖着的四边形（x：-1～1，y：0 脚 → 1 顶），里面两三股光带按 3D 螺旋投影：
	/// 转到近处的半圈粗而亮、转到远处的细而暗；side = +1 只画近的半圈（盖在人前面），-1 只画远的半圈（放在人后面）——人被光带绕在中间。
	/// 光带的头沿 dirn 方向走（+1 往上 = 增益，-1 往下 = 减益），头最亮，后面拖一段渐隐的尾巴，沿途掉细碎的光点。
	/// dark > 0（咒怨 / 暗影）：光带外面裹一层压暗的烟。
	/// </summary>
	private static Shader Helix => _helix ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform float prog = 0.0;
uniform float side = 1.0;
uniform float dirn = 1.0;
uniform float turns = 2.0;
uniform float strands = 2.0;
uniform vec3 hot = vec3(1.0);
uniform vec3 mid = vec3(0.4, 0.8, 1.0);
uniform vec3 deep = vec3(0.1, 0.3, 0.9);
uniform float dark = 0.0;
uniform float seed = 0.0;
uniform float clock = 0.0;
" + Noise + @"
void fragment() {
	float x = (UV.x - 0.5) * 2.0;
	float y = 1.0 - UV.y;
	float yy = dirn > 0.0 ? y : 1.0 - y;                       // 沿行进方向的坐标
	float head = prog * 1.7;                                    // 头：0.6 的进度走到头，之后尾巴跟上去
	float tail = head - 0.75;
	float body = smoothstep(tail, tail + 0.3, yy) * (1.0 - smoothstep(head - 0.015, head + 0.01, yy));
	float lead = exp(-max(head - yy, 0.0) / 0.09);            // 头部加亮
	float R = mix(0.8, 0.52, y) * (1.0 + 0.05 * sin(clock * 3.0 + y * 6.0));
	float core = 0.0, glow = 0.0, smoke = 0.0;
	for (int i = 0; i < 3; i++) {
		if (float(i) >= strands) break;
		float ph = y * turns * 6.2832 - clock * 6.5 * dirn + float(i) * 6.2832 / strands + seed;
		float sx = R * sin(ph), dz = cos(ph);
		float vis = smoothstep(-0.12, 0.12, dz * side);
		float near = dz * 0.5 + 0.5;
		float w = mix(0.022, 0.06, near) * mix(1.0, 0.5, 1.0 - abs(dz));   // 远处细；转到侧面是侧着看的带子，更窄
		float d = abs(x - sx);
		float c = exp(-pow(d / w, 2.0)) * vis;
		core += c * (0.5 + 0.5 * near);
		glow += exp(-d / (w * 4.0)) * vis * (0.25 + 0.2 * near);
		smoke += exp(-d / (w * 7.0)) * vis;
	}
	// 沿途掉的细碎光点：按格子随机，跟着行进方向漂
	vec2 g = vec2(x * 9.0, y * 22.0 - clock * 3.0 * dirn);
	vec2 gi = floor(g), gf = fract(g) - 0.5;
	float mote = step(0.86, hash(gi + seed)) * exp(-dot(gf, gf) * 26.0) * smoothstep(1.0, 0.3, abs(x)) * (0.5 + 0.5 * sin(clock * 20.0 + hash(gi) * 40.0));
	float edge = (1.0 - smoothstep(0.86, 1.0, abs(x))) * smoothstep(0.0, 0.05, y) * (1.0 - smoothstep(0.93, 1.0, y));
	vec3 col = hot * core * (0.55 + 1.3 * lead) + mid * (core * 0.7 + glow * (1.0 + lead)) + deep * glow * 0.9 + mix(mid, hot, 0.5) * mote * 0.9 * side;
	float fade = body * edge * (1.0 - smoothstep(0.8, 1.0, prog));
	float shade = dark * clamp(smoke * 0.55, 0.0, 0.7) * fade;
	COLOR = vec4(col * fade, shade);
}" };

	private static void HelixPair(Control front, Control? back, Vector2 feet, float w, float h, Tone tone, bool up, float life, float turns, int strands)
	{
		(Color hot, Color mid, Color deep, float dark) = Pal(tone);
		float seed = Fx.Rand(0f, 6.28f);
		foreach ((Control? layer, float side) in new[] { (back, -1f), (front, 1f) })
		{
			if (layer == null || !GodotObject.IsInstanceValid(layer))
			{
				continue;
			}
			Node2D root = Fx.Root(layer, feet, "P5Helix", life + 0.1);
			ColorRect q = Quad(root, Helix, new Vector2(w, h), new Vector2(w / 2f, h * 0.97f));
			Set(q, "side", side);
			Set(q, "dirn", up ? 1f : -1f);
			Set(q, "turns", turns);
			Set(q, "strands", (float)strands);
			Set(q, "hot", new Vector3(hot.R, hot.G, hot.B));
			Set(q, "mid", new Vector3(mid.R, mid.G, mid.B));
			Set(q, "deep", new Vector3(deep.R, deep.G, deep.B));
			Set(q, "dark", dark);
			Set(q, "seed", seed);
			Fx.Clock(q);
			Fx.Animate(q, 0, life, t => Set(q, "prog", t));
		}
	}

	/// <summary>
	/// 增益（自己身上）：脚下一圈光环弹开，两股光带绕着身体盘上去，到头顶一下亮、甩出几点火星。
	/// feet / height = Joker 脚底和身高（屏幕像素）；big = 形态类大牌（三股、更高、更久）。
	/// </summary>
	public static void Buff(Control front, Control? back, Vector2 feet, float height, Tone tone = Tone.Blue, bool big = false, bool sound = true)
	{
		(Color hot, Color mid, _, _) = Pal(tone);
		float unit = Unit(front), life = big ? 0.95f : 0.62f;
		if (sound)
		{
			Sfx.Play(tone is Tone.Curse or Tone.Shadow ? Sfx.BuffDark : Sfx.BuffUp, big ? 0.8f : 0.6f);
		}
		Glow.Shockwave(front, feet, mid, height * (big ? 1.5f : 1.1f), 0.4f, 0.24f);
		HelixPair(front, back, feet, height * (big ? 0.95f : 0.78f), height * (big ? 1.45f : 1.22f), tone, true, life, big ? 2.6f : 2f, big ? 3 : 2);
		Vector2 top = feet + new Vector2(0f, -height * (big ? 1.3f : 1.1f));
		Fx.After(front, life * 0.58f, () =>
		{
			Glow.Flash(front, top, mid, height * (big ? 0.55f : 0.36f), 0.18f);
			Glow.Sparks(front, top, big ? 16 : 9, 80f, 320f, 0.4f, new[] { hot, mid }, 260f, 0.03f * unit, 0.09f * unit, Vector2.Up, 70f);
		});
		if (big)
		{
			Glow.Shockwave(front, feet, hot, height * 2.1f, 0.55f, 0.24f, 0.12f);
		}
	}

	/// <summary>
	/// 减益（敌人身上）：两股暗红的光带从头顶压下来、绕着身体拧到脚下，落地一圈暗环；身体跟着暗一下（tint）。
	/// top / feet = 敌人头顶和脚底；width = 身体宽。
	/// </summary>
	public static void Debuff(Control front, Control? back, Vector2 feet, Vector2 top, float width, Action<Color>? tint = null, bool sound = true, float delay = 0f)
	{
		Fx.After(front, delay, () =>
		{
			(Color hot, Color mid, Color deep, _) = Pal(Tone.Curse);
			float h = Mathf.Max(120f, (feet - top).Length()), unit = Unit(front);
			const float life = 0.55f;
			if (sound)
			{
				Sfx.Play(Sfx.DebuffDown, 0.6f);
			}
			HelixPair(front, back, feet, Mathf.Max(width * 1.25f, h * 0.7f), h * 1.3f, Tone.Curse, false, life, 2f, 2);
			Fx.After(front, life * 0.5f, () =>
			{
				Glow.Shockwave(front, feet, mid, h * 1.25f, 0.38f, 0.22f);
				Glow.Sparks(front, feet, 8, 60f, 240f, 0.35f, new[] { hot, mid, deep }, 0f, 0.03f * unit, 0.08f * unit, Vector2.Up, 85f);
			});
			if (tint != null)
			{
				Node2D ticker = Fx.Root(front, feet, "P5DebuffTint", life + 0.25);
				Fx.Animate(ticker, 0, life + 0.2f, t =>
				{
					float k = Mathf.Sin(Mathf.Clamp(t * 1.25f, 0f, 1f) * Mathf.Pi);
					tint(Colors.White.Lerp(new Color(0.62f, 0.42f, 0.5f), k * 0.8f));
				});
				ticker.TreeExiting += () => tint(Colors.White);
			}
		});
	}

	// ───────────────────────── 光牌（抽牌 / 弃牌 / 谋划） ─────────────────────────

	private static Shader? _card;

	/// <summary>
	/// 一张发光的牌（怪盗的预告信那样的红牌，但全是光）：圆角矩形，牌面暗红到近黑的斜向渐变、一道扫过的高光、细白边、中间一枚白色四角星；
	/// 四周一圈红晕。face：1 正面、0 背面（翻面时由代码切）。burn 0→1：从一角被噪声烧掉，烧口一圈白热。
	/// 四边形比牌大（牌占中间 0.5 × 0.62），外面留给光晕。
	/// </summary>
	private static Shader Card => _card ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform float face = 1.0;
uniform float burn = 0.0;
uniform float sheen = 0.0;
uniform float alpha = 1.0;
uniform float seed = 0.0;
" + Noise + @"
float box(vec2 p, vec2 b, float r) { vec2 q = abs(p) - b + r; return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r; }
void fragment() {
	vec2 p = (UV - 0.5) * vec2(1.0, 1.24);                      // 牌 5:7
	float d = box(p, vec2(0.25, 0.385), 0.035);
	float inside = 1.0 - smoothstep(-0.006, 0.006, d);
	float rim = exp(-abs(d) / 0.011);
	float halo = exp(-max(d, 0.0) / 0.075) * (1.0 - inside);
	// 牌面
	float diag = clamp((p.x - p.y) * 1.1 + 0.5, 0.0, 1.0);
	vec3 front = mix(vec3(0.16, 0.0, 0.03), vec3(0.86, 0.06, 0.13), diag);
	float inner = smoothstep(0.012, 0.0, abs(box(p, vec2(0.2, 0.33), 0.02)));       // 内框细线
	float a = atan(p.y, p.x), r = length(p * vec2(1.0, 0.9));
	float star = 1.0 - smoothstep(0.0, 0.012, r - 0.115 * pow(abs(cos(a * 2.0)), 5.0) - 0.022);   // 四角星
	front = mix(front, vec3(1.0, 0.95, 0.93), max(inner * 0.75, star));
	vec3 backc = mix(vec3(0.05, 0.0, 0.01), vec3(0.3, 0.02, 0.05), diag) + vec3(0.5, 0.06, 0.1) * step(0.5, fract((p.x + p.y) * 14.0)) * 0.25;
	vec3 body = mix(backc, front, face);
	float band = exp(-pow((p.x + p.y * 0.6 - mix(-0.7, 0.7, sheen)) / 0.07, 2.0));  // 扫过的高光
	body += vec3(1.0, 0.9, 0.88) * band * 0.75;
	// 烧掉：从左下角烧上来
	float n = noise(p * 9.0 + seed) * 0.5 + noise(p * 21.0 + seed * 2.0) * 0.25;
	float m = (p.x - p.y) * 0.55 + 0.5 + n * 0.45 - burn * 1.5;
	float alive = smoothstep(0.0, 0.03, m);
	float hotline = exp(-abs(m) / 0.03) * step(-0.05, m) * step(0.001, burn);
	vec3 col = body * inside * alive + vec3(1.0, 0.96, 0.94) * rim * 0.9 * alive + vec3(1.0, 0.22, 0.28) * halo * 0.55 * alive;
	col += vec3(1.0, 0.85, 0.8) * hotline * (inside + rim) * 1.6;
	float aa = inside * alive * 0.94;
	COLOR = vec4(col * alpha, aa * alpha);
}" };

	private sealed class CardNode
	{
		public Node2D Root = null!;
		public ColorRect Rect = null!;
		public Line2D Trail = null!;
		public readonly List<Vector2> Path = new();
	}

	private static CardNode MakeCard(Control layer, Vector2 at, float size, double life)
	{
		CardNode c = new() { Root = Fx.Root(layer, at, "P5LightCard", life) };
		c.Trail = new Line2D
		{
			Width = size * 0.16f, Material = Fx.Additive, TopLevel = true, Antialiased = true,
			Gradient = new Gradient { Offsets = new[] { 0f, 1f }, Colors = new[] { new Color(1f, 0.25f, 0.3f, 0f), new Color(1f, 0.75f, 0.72f, 0.75f) } },
			WidthCurve = new Curve(),
		};
		c.Trail.WidthCurve.AddPoint(new Vector2(0f, 0f));
		c.Trail.WidthCurve.AddPoint(new Vector2(1f, 1f));
		c.Root.AddChild(c.Trail);
		c.Rect = Quad(c.Root, Card, new Vector2(size * 2f, size * 2f));
		Set(c.Rect, "seed", Fx.Rand(0f, 30f));
		return c;
	}

	/// <summary>牌的位置、翻转（绕竖轴：横向缩放 = cos，过了 90° 换面）、拖尾。</summary>
	private static void Pose(CardNode c, Vector2 pos, float tilt, float flip, float scale = 1f)
	{
		c.Root.GlobalPosition = pos;
		c.Root.Rotation = tilt;
		float cs = Mathf.Cos(flip);
		c.Root.Scale = new Vector2(Mathf.Max(0.06f, Mathf.Abs(cs)), 1f) * scale;
		Set(c.Rect, "face", cs >= 0f ? 1f : 0f);
		c.Path.Add(pos);
		if (c.Path.Count > 7)
		{
			c.Path.RemoveAt(0);
		}
		c.Trail.GlobalTransform = Transform2D.Identity;
		c.Trail.Points = c.Path.ToArray();
	}

	/// <summary>手牌区在特效层坐标里的位置（屏幕底边中间偏下）。</summary>
	private static Vector2 HandUi(Control layer)
	{
		Vector2 screen = Fx.Screen(layer);
		return layer.GetCanvasTransform().AffineInverse() * new Vector2(screen.X * 0.5f, screen.Y * 1.04f);
	}

	/// <summary>
	/// 抽牌：n 张光牌从手里呈扇形弹出去（翻着面），在半空顿一下、亮一下，然后一张接一张拉着光尾飞进手牌区。
	/// hand = 扯手套的那只手；facing = Joker 朝向。最多画 5 张。
	/// </summary>
	public static void Draw(Control layer, Vector2 hand, int n, float facing = 1f, bool sound = true)
	{
		n = Mathf.Clamp(n, 1, 5);
		float unit = Unit(layer), size = 46f * unit;
		Vector2 dest = HandUi(layer);
		Glow.Flash(layer, hand, new Color(1f, 0.3f, 0.34f), 150f * unit, 0.14f);
		for (int i = 0; i < n; i++)
		{
			int k = i;
			float delay = 0.035f * k;
			float fan = n == 1 ? 0f : Mathf.Lerp(-0.5f, 0.5f, k / (n - 1f));
			Vector2 apex = hand + new Vector2((70f + 120f * fan) * facing, -150f - 46f * (1f - fan * fan)) * unit * 1.1f;
			float tilt = fan * 0.5f * facing, spin = Fx.Rand(0.8f, 1.2f);
			const float outT = 0.16f, holdT = 0.1f, inT = 0.24f;
			Fx.After(layer, delay, () =>
			{
				if (sound)
				{
					Sfx.Play(Sfx.CardFlick, 0.55f - 0.05f * k);
				}
				CardNode c = MakeCard(layer, hand, size, outT + holdT + inT + 0.1);
				Fx.Animate(c.Root, 0, outT + holdT + inT, t =>
				{
					float s = t * (outT + holdT + inT);
					if (s < outT)
					{
						float e = Ease5.OutExpo(s / outT);
						Pose(c, hand.Lerp(apex, e), tilt * e, Mathf.Pi * (1f + spin) * (1f - e), 0.5f + 0.5f * e);
						Set(c.Rect, "sheen", 0f);
					}
					else if (s < outT + holdT)
					{
						float h = (s - outT) / holdT;
						Pose(c, apex + new Vector2(0f, -6f * unit * Mathf.Sin(h * Mathf.Pi)), tilt, 0f);
						Set(c.Rect, "sheen", h);                         // 悬停时一道高光扫过
					}
					else
					{
						float e = Fx.InCubic((s - outT - holdT) / inT);
						Vector2 ctrl = new(Mathf.Lerp(apex.X, dest.X, 0.35f), apex.Y - 40f * unit);
						Vector2 p = apex.Lerp(ctrl, e).Lerp(ctrl.Lerp(dest, e), e);
						Pose(c, p, tilt + e * 0.9f * facing, e * Mathf.Pi * 0.5f * 0.9f, 1f - 0.45f * e);
						Set(c.Rect, "alpha", 1f - Mathf.Clamp((e - 0.8f) / 0.2f, 0f, 1f));
					}
				});
			});
		}
	}

	/// <summary>
	/// 弃牌：n 张光牌被甩到身前，边飞边从一角烧掉，烧口白热，留下几点往上飘的余烬。最多画 4 张。
	/// </summary>
	public static void Discard(Control layer, Vector2 hand, int n, float facing = 1f, bool sound = true)
	{
		n = Mathf.Clamp(n, 1, 4);
		float unit = Unit(layer), size = 42f * unit;
		for (int i = 0; i < n; i++)
		{
			int k = i;
			float ang = Mathf.Lerp(-0.55f, 0.2f, n == 1 ? 0.5f : k / (n - 1f));
			Vector2 to = hand + new Vector2(Mathf.Cos(ang) * 230f * facing, Mathf.Sin(ang) * 230f) * unit;
			float spin = Fx.Rand(2.5f, 4f) * (k % 2 == 0 ? 1f : -1f);
			const float life = 0.46f;
			Fx.After(layer, 0.05f * k, () =>
			{
				if (sound && k == 0)
				{
					Sfx.Play(Sfx.CardBurn, 0.55f);
				}
				CardNode c = MakeCard(layer, hand, size, life + 0.1);
				bool ember = false;
				Fx.Animate(c.Root, 0, life, t =>
				{
					float e = Ease5.OutQuint(t);
					Vector2 p = hand.Lerp(to, e) + new Vector2(0f, 50f * unit * t * t);
					Pose(c, p, spin * e, t * 2.2f, 0.6f + 0.4f * Mathf.Min(1f, t * 5f));
					Set(c.Rect, "burn", Mathf.Clamp((t - 0.22f) / 0.7f, 0f, 1f));
					if (!ember && t > 0.5f)
					{
						ember = true;
						Glow.Sparks(layer, p, 7, 30f, 140f, 0.45f, new[] { new Color(1f, 0.9f, 0.86f), new Color(1f, 0.25f, 0.3f), new Color(0.5f, 0.02f, 0.08f) },
							-160f, 0.03f * unit, 0.08f * unit, Vector2.Up, 60f);
					}
				});
			});
		}
	}

	/// <summary>
	/// 谋划 / 保留（计划妥当、谋划专家、必备工具、夜魇……）：n 张光牌绕着 Joker 转一圈（转到身后的那半圈画在后景层、被人挡住），然后收进胸口、一闪。
	/// dark = 夜魇：牌是背面朝外的暗色，收的时候不闪白而是一团暗影。
	/// </summary>
	public static void Plan(Control front, Control? back, Vector2 chest, float height, int n = 3, bool dark = false, bool sound = true)
	{
		n = Mathf.Clamp(n, 1, 5);
		float unit = Unit(front), size = 48f * unit, rx = height * 0.42f, ry = height * 0.1f;
		const float life = 0.7f;
		if (sound)
		{
			Sfx.Play(Sfx.CardFlick, 0.5f);
			Sfx.Play(dark ? Sfx.BuffDark : Sfx.BuffUp, 0.4f, 0.32f);
		}
		for (int i = 0; i < n; i++)
		{
			float phase = Mathf.Tau * i / n;
			// 每张牌两份：前景一份、后景一份，按转到前 / 后切换显示
			CardNode f = MakeCard(front, chest, size, life + 0.1);
			CardNode? b = back != null && GodotObject.IsInstanceValid(back) ? MakeCard(back, chest, size, life + 0.1) : null;
			f.Trail.Visible = false;                                  // 绕圈时不拖尾（轨迹弯，直线段的尾巴会折成多边形）
			if (b != null) b.Trail.Visible = false;
			Fx.Animate(f.Root, 0, life, t =>
			{
				float a = phase + Mathf.Tau * 1.1f * Ease5.OutQuint(Mathf.Min(1f, t / 0.75f));
				float pull = Fx.InCubic(Mathf.Clamp((t - 0.62f) / 0.38f, 0f, 1f));
				float appear = Mathf.Min(1f, t * 7f);
				Vector2 p = chest + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry - height * 0.04f) * (1f - pull) * appear;
				bool nearSide = Mathf.Sin(a) >= 0f;
				float sc = (0.8f + 0.2f * Mathf.Sin(a)) * (1f - 0.6f * pull);
				float flip = dark ? Mathf.Pi : a + Mathf.Pi * 0.5f;
				Pose(f, p, 0.12f * Mathf.Cos(a), flip, sc);
				f.Root.Visible = nearSide || b == null;
				Set(f.Rect, "alpha", appear * (1f - pull * 0.5f));
				if (b != null && GodotObject.IsInstanceValid(b.Root))
				{
					Pose(b, p, 0.12f * Mathf.Cos(a), flip, sc);
					b.Root.Visible = !nearSide;
					Set(b.Rect, "alpha", appear * 0.8f * (1f - pull * 0.5f));
				}
			});
		}
		Fx.After(front, life - 0.04f, () =>
		{
			Glow.Flash(front, chest, dark ? new Color(0.3f, 0.2f, 0.55f) : new Color(1f, 0.35f, 0.38f), height * 0.5f, 0.2f);
			Glow.Shockwave(front, chest, dark ? new Color(0.4f, 0.3f, 0.8f) : new Color(1f, 0.5f, 0.5f), height * 0.9f, 0.35f);
		});
	}

	// ───────────────────────── 装弹（生成小刀牌） ─────────────────────────

	private static Shader? _bullet;

	/// <summary>
	/// 一发子弹（小刀牌在这个 mod 里是子弹）：横着的一枚——黄铜弹壳 + 铜色弹头，上缘一道随翻转明灭的高光，底缘压暗，外面一点暖色的光。
	/// glint 0～1：高光的亮度（由代码按转角给）。
	/// </summary>
	private static Shader Bullet => _bullet ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform float glint = 0.0;
uniform float alpha = 1.0;
void fragment() {
	vec2 p = (UV - 0.5) * vec2(2.0, 2.0);                       // x：-1 弹底 → 1 弹尖
	float halfw = p.x < 0.25 ? 0.2 : 0.2 * sqrt(max(1.0 - pow((p.x - 0.25) / 0.6, 2.0), 0.0));   // 弹壳直筒 + 弹头卵形
	float inx = step(-0.78, p.x) * step(p.x, 0.85);
	float d = abs(p.y) - halfw;
	float inside = (1.0 - smoothstep(-0.02, 0.02, d)) * inx;
	float v = clamp(p.y / max(halfw, 0.001), -1.0, 1.0);        // -1 上缘 → 1 下缘
	vec3 brass = mix(vec3(0.95, 0.74, 0.34), vec3(0.42, 0.24, 0.08), smoothstep(-0.4, 1.0, v));
	vec3 copper = mix(vec3(0.98, 0.56, 0.36), vec3(0.4, 0.14, 0.08), smoothstep(-0.4, 1.0, v));
	vec3 col = mix(brass, copper, smoothstep(0.2, 0.28, p.x));
	col *= 1.0 - 0.45 * (1.0 - smoothstep(0.0, 0.03, abs(p.x + 0.66)));   // 弹底的槽
	float spec = exp(-pow((v + 0.55) / 0.22, 2.0)) * (0.35 + 1.4 * glint);
	col += vec3(1.0, 0.96, 0.88) * spec;
	float halo = exp(-max(d, 0.0) / 0.22) * exp(-pow(p.x / 1.1, 2.0)) * (1.0 - inside) * (0.25 + 0.75 * glint);
	COLOR = vec4((col * inside + vec3(1.0, 0.6, 0.35) * halo * 0.6) * alpha, inside * alpha);
}" };

	/// <summary>
	/// 装弹：n 发子弹从手里抛起来（一小把，各自翻转、转到某个角度闪一下光），到顶以后一发接一发落回手里，每落一发手上一点白闪、一声金属轻响。
	/// hand = 扯手套的那只手。最多画 5 发。
	/// </summary>
	public static void Load(Control layer, Vector2 hand, int n, float facing = 1f, bool sound = true)
	{
		n = Mathf.Clamp(n, 1, 5);
		float unit = Unit(layer), len = 40f * unit;
		Glow.Flash(layer, hand, new Color(1f, 0.7f, 0.45f), 120f * unit, 0.12f);
		for (int i = 0; i < n; i++)
		{
			int k = i;
			float fan = n == 1 ? 0f : Mathf.Lerp(-1f, 1f, k / (n - 1f));
			Vector2 apex = hand + new Vector2((18f + 40f * fan) * facing, -(150f + 26f * (1f - Mathf.Abs(fan)) + Fx.Rand(-10f, 10f))) * unit;
			float up = 0.2f, hang = 0.05f + 0.055f * k, down = 0.13f, life = up + hang + down;
			float turns = Fx.Rand(1.3f, 2.1f) * (k % 2 == 0 ? 1f : -1f), rot0 = Fx.Rand(0f, 6f);
			Node2D root = Fx.Root(layer, hand, "P5Bullet", life + 0.1);
			ColorRect q = Quad(root, Bullet, new Vector2(len * 1.6f, len * 1.6f));
			Fx.Animate(root, 0, life, t =>
			{
				float s = t * life;
				Vector2 p;
				if (s < up)
				{
					p = hand.Lerp(apex, Ease5.OutQuint(s / up));
				}
				else if (s < up + hang)
				{
					float h = (s - up) / hang;
					p = apex + new Vector2(0f, -5f * unit * Mathf.Sin(h * Mathf.Pi));
				}
				else
				{
					p = apex.Lerp(hand, Fx.InCubic((s - up - hang) / down));
				}
				root.GlobalPosition = p;
				float spinK = s < up + hang ? Ease5.OutQuint(Mathf.Min(1f, s / (up + hang))) : 1f;
				root.Rotation = rot0 + turns * Mathf.Tau * spinK + (s >= up + hang ? -Mathf.Pi * 0.5f * Fx.InCubic((s - up - hang) / down) : 0f);
				Set(q, "glint", Mathf.Pow(Mathf.Abs(Mathf.Sin(root.Rotation * 1.5f + k)), 6f));
				Set(q, "alpha", Mathf.Min(1f, s / 0.03f));
			});
			Fx.After(layer, life, () =>
			{
				if (sound)
				{
					Sfx.Play(Sfx.BulletLoad, 0.6f);
				}
				Glow.Flash(layer, hand, new Color(1f, 0.9f, 0.8f), 70f * unit, 0.08f);
				Glow.Sparks(layer, hand, 3, 80f, 200f, 0.18f, new[] { new Color(1f, 0.95f, 0.85f), new Color(1f, 0.6f, 0.3f) }, 300f, 0.02f * unit, 0.05f * unit,
					Vector2.Up, 80f);
			});
		}
	}

	// ───────────────────────── 能量涌动 ─────────────────────────

	private static Shader? _surge;

	/// <summary>
	/// 往中心吸的光丝：极坐标下按角度分道，每道一段亮线从外圈冲向中心（越近越细越亮），道与道错开出发；中心一团越来越亮的白热。
	/// prog 0→1：吸的过程；burst 0→1：吸满以后炸开（光丝消失，中心一闪）。
	/// </summary>
	private static Shader SurgeShader => _surge ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform float prog = 0.0;
uniform vec3 mid = vec3(1.0, 0.32, 0.36);
uniform float seed = 0.0;
float h(float n) { return fract(sin(n * 127.1 + seed * 17.3) * 43758.5453); }
void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	float r = length(p), a = atan(p.y, p.x);
	const float N = 46.0;
	float k = (a + 3.14159) / 6.28318 * N, cell = floor(k), f = abs(k - cell - 0.5);
	float on = step(0.45, h(cell));
	float start = h(cell + 5.0) * 0.45;                           // 各道错开出发
	float t = clamp((prog - start) / 0.55, 0.0, 1.0);
	float headr = mix(1.0, 0.04, t * t * (3.0 - 2.0 * t));       // 线头的半径：从外往里
	float lenr = mix(0.3, 0.1, t);
	float along = smoothstep(headr - 0.01, headr + 0.02, r) * (1.0 - smoothstep(headr + lenr * 0.4, headr + lenr, r));
	float w = mix(0.06, 0.3, r);                                  // 以「道宽」为单位的线宽：越靠外越宽
	float line = on * along * (1.0 - smoothstep(w * 0.5, w, f * 2.0)) * step(0.001, t) * step(t, 0.999);
	float core = exp(-r * 14.0) * prog * prog * 0.9 + exp(-r * 5.0) * prog * 0.16;
	vec3 col = mix(mid, vec3(1.0, 0.96, 0.94), clamp(1.0 - r * 1.6, 0.0, 1.0)) * line * 1.3 + vec3(1.0, 0.95, 0.92) * core;
	COLOR = vec4(col * (1.0 - smoothstep(0.9, 1.0, r)), 1.0);
}" };

	/// <summary>
	/// 能量涌动（战术大师、肾上腺素）：四周的光丝吸进胸口（0.28s），吸满一闪、一圈光环弹开、几点火星往上蹿。
	/// </summary>
	public static void Surge(Control layer, Vector2 chest, float height, Tone tone = Tone.Red, bool sound = true)
	{
		(Color hot, Color mid, _, _) = Pal(tone);
		float unit = Unit(layer);
		const float pull = 0.28f;
		if (sound)
		{
			Sfx.Play(Sfx.EnergySurge, 0.65f);
		}
		Node2D root = Fx.Root(layer, chest, "P5Surge", pull + 0.1);
		ColorRect q = Quad(root, SurgeShader, Vector2.One * height * 1.7f);
		Set(q, "mid", new Vector3(mid.R, mid.G, mid.B));
		Set(q, "seed", Fx.Rand(0f, 20f));
		Fx.Animate(q, 0, pull, t => Set(q, "prog", t));
		Fx.After(layer, pull, () =>
		{
			Glow.Flash(layer, chest, mid, height * 0.55f, 0.16f);
			Glow.Shockwave(layer, chest, hot, height * 1.5f, 0.4f);
			Glow.Shockwave(layer, chest, mid, height * 1.0f, 0.32f, 1f, 0.05f);
			Glow.Sparks(layer, chest, 14, 160f, 520f, 0.4f, new[] { hot, mid }, -120f, 0.03f * unit, 0.09f * unit, Vector2.Up, 110f);
		});
	}

	// ───────────────────────── 尖啸 ─────────────────────────

	private static Shader? _wave;

	/// <summary>
	/// 声波：从四边形左边中点朝右推出去的一串弧形波前（锥形，越远越宽越淡）。每道波前是一条断续的亮边，后面跟一条压暗的带子（空气被挤开），
	/// 锥里有顺着声波方向的细丝。prog 0→1：波前往外走。
	/// </summary>
	private static Shader Wave => _wave ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform float prog = 0.0;
uniform float cone = 0.42;
uniform float seed = 0.0;
" + Noise + @"
void fragment() {
	vec2 p = vec2(UV.x, (UV.y - 0.5) * 2.0);                    // 原点在左边中点，x 朝右 0～1
	float r = length(p), a = atan(p.y, p.x);
	float inCone = 1.0 - smoothstep(cone * 0.75, cone, abs(a));
	float light = 0.0, shade = 0.0;
	for (int k = 0; k < 5; k++) {
		float fk = float(k);
		float R = prog * 1.45 - fk * 0.14;
		if (R <= 0.02) continue;
		float w = 0.008 + 0.022 * R;
		float seg = smoothstep(0.3, 0.45, noise(vec2(a * 9.0 + fk * 3.7 + seed, fk)));
		float fall = (1.0 - smoothstep(0.55, 1.0, R)) * (1.0 - fk * 0.13);
		light += exp(-pow((r - R) / w, 2.0)) * mix(0.3, 1.0, seg) * fall;
		shade += smoothstep(R - 0.07, R - 0.015, r) * (1.0 - smoothstep(R - 0.015, R, r)) * fall;
	}
	float lane = pow(noise(vec2(a * 46.0 + seed, 1.0)), 7.0) * 3.0;
	float streak = lane * smoothstep(0.05, 0.2, r) * (1.0 - smoothstep(prog * 1.2, prog * 1.45, r)) * (1.0 - prog) * 0.5;
	float fade = inCone * (1.0 - smoothstep(0.9, 1.0, r)) * (1.0 - smoothstep(0.75, 1.0, prog));
	vec3 col = vec3(1.0, 0.95, 0.93) * light + vec3(1.0, 0.25, 0.3) * (light * 0.6 + streak);
	COLOR = vec4(col * fade, clamp(shade * 0.3, 0.0, 0.4) * fade);
}" };

	public readonly record struct Victim(Vector2 Feet, Vector2 Top, float Width, Action<Color>? Tint = null);

	/// <summary>
	/// 尖啸（所有敌人失去力量）：从面具朝敌人推出去一串弧形声波；声波扫到谁，谁身上压下一道减益（不再单独出声）。
	/// mask = Joker 的面具；targets = 每个敌人的脚底 / 头顶 / 宽度。
	/// </summary>
	public static void Wail(Control front, Control? back, Vector2 mask, IReadOnlyList<Victim> targets, bool sound = true)
	{
		if (targets.Count == 0)
		{
			return;
		}
		Vector2 mid = Vector2.Zero;
		float reach = 0f;
		foreach (Victim v in targets)
		{
			Vector2 c = (v.Feet + v.Top) * 0.5f;
			mid += c / targets.Count;
			reach = Mathf.Max(reach, c.DistanceTo(mask));
		}
		reach = reach * 1.12f + 120f;
		const float life = 0.62f;
		if (sound)
		{
			Sfx.Play(Sfx.Wail, 0.7f);
		}
		Fx.Shake(2);
		Glow.Flash(front, mask, new Color(1f, 0.3f, 0.34f), reach * 0.22f, 0.16f);
		Node2D root = Fx.Root(front, mask, "P5Wail", life + 0.1);
		root.Rotation = (mid - mask).Angle();
		ColorRect q = Quad(root, Wave, new Vector2(reach, reach), new Vector2(0f, reach / 2f));
		Set(q, "seed", Fx.Rand(0f, 30f));
		Fx.Animate(q, 0, life, t => Set(q, "prog", t));
		foreach (Victim v in targets)
		{
			float d = ((v.Feet + v.Top) * 0.5f).DistanceTo(mask);
			Debuff(front, back, v.Feet, v.Top, v.Width, v.Tint, sound: false, delay: life * Mathf.Clamp(d / reach / 1.45f, 0f, 1f) * 1.0f);
		}
	}

	// ───────────────────────── 子弹时间 ─────────────────────────

	private static Shader? _dial;

	/// <summary>
	/// 子弹时间的钟面（全屏一层，不读屏幕）：以 center 为圆心，一圈 12 个刻度（每三格一个长的）和两根指针——开始转得飞快，越来越慢，停住；
	/// 外面几圈细环往里收；四周压一层深蓝黑的暗。spin = 钟面此刻的转角，amount 0～1 = 整体强度，stop 0～1 = 停住的那一下（刻度一亮）。
	/// </summary>
	private static Shader Dial => _dial ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform vec2 sres = vec2(1920.0, 1080.0);
uniform vec2 center = vec2(500.0, 600.0);
uniform float radius = 240.0;
uniform float spin = 0.0;
uniform float amount = 0.0;
uniform float stop = 0.0;
uniform float rings = 0.0;
float seg(vec2 p, vec2 a, vec2 b) { vec2 pa = p - a, ba = b - a; return length(pa - ba * clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0)); }
void fragment() {
	vec2 p = UV * sres - center;
	float r = length(p) / radius, a = atan(p.y, p.x) - spin;
	float px = 1.0 / radius;
	// 刻度
	float k = a / 6.2831853 * 12.0, cell = floor(k + 0.5), fa = abs(k - cell) * 6.2831853 / 12.0 * r * radius;   // 到最近一根刻度的像素距离
	float longTick = step(0.5, 1.0 - abs(mod(cell, 3.0)));
	float tick = (1.0 - smoothstep(1.2, 2.6, fa)) * smoothstep(mix(0.9, 0.82, longTick), mix(0.9, 0.82, longTick) + px * 2.0, r) * (1.0 - smoothstep(1.0, 1.0 + px * 2.0, r));
	float rim = exp(-pow((r - 1.04) / (px * 1.6), 2.0)) * 0.6;
	// 指针：分针跟着钟面转，时针慢 12 倍
	vec2 q = p / radius;
	float ha = spin, hb = spin / 12.0 + 1.1;
	float hand = (1.0 - smoothstep(px * 1.2, px * 2.6, seg(q, vec2(0.0), vec2(cos(ha), sin(ha)) * 0.78)))
		+ (1.0 - smoothstep(px * 1.8, px * 3.4, seg(q, vec2(0.0), vec2(cos(hb), sin(hb)) * 0.5)));
	float hub = 1.0 - smoothstep(px * 4.0, px * 6.0, length(q));
	// 往里收的细环
	float ring = 0.0;
	for (int i = 0; i < 3; i++) {
		float R = 1.2 + (1.0 - rings) * (0.9 + float(i) * 0.55);
		ring += exp(-pow((r - R) / (px * 1.8), 2.0)) * (1.0 - float(i) * 0.25) * rings * (1.0 - rings) * 4.0;
	}
	float light = clamp(tick + rim + hand + hub, 0.0, 1.5) * (0.7 + 0.9 * stop) + ring * 0.5;
	float glow = exp(-abs(r - 1.0) * 5.0) * 0.12 * (1.0 + 2.0 * stop);
	float vig = smoothstep(0.6, 3.6, r) * 0.62;
	vec3 col = vec3(1.0, 0.95, 0.93) * light + vec3(1.0, 0.2, 0.26) * (light * 0.5 + glow);
	COLOR = vec4(col * amount, vig * amount);
}" };

	/// <summary>
	/// 子弹时间（本回合手牌全部免费、不能再抽）：Joker 身后一面光的钟——指针和刻度转得飞快、越来越慢，0.5s 时「咔」地停住（刻度一亮、一圈波纹），
	/// 四周压暗；停住的样子保持一小会儿再淡掉。chest = Joker 胸口，height = 身高。
	/// </summary>
	public static void TimeSlow(Control layer, Vector2 chest, float height, bool sound = true)
	{
		const float life = 1.25f, stopAt = 0.5f;
		if (sound)
		{
			Sfx.Play(Sfx.TimeSlow, 0.7f);
		}
		Vector2 screen = Fx.Screen(layer);
		Node2D root = Fx.ScreenRoot(layer, "P5TimeSlow");
		Fx.FreeAfter(root, life + 0.1);
		ShaderMaterial m = new() { Shader = Dial };
		ColorRect r = new() { Size = screen, Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(r);
		m.SetShaderParameter("sres", screen);
		m.SetShaderParameter("center", Fx.ScreenPoint(layer, chest));
		m.SetShaderParameter("radius", height * 0.62f * (layer.GetCanvasTransform().Scale.X));
		float spin0 = Fx.Rand(0f, 6f);
		Fx.Animate(r, 0, life, t =>
		{
			float s = t * life;
			float k = Mathf.Clamp(s / stopAt, 0f, 1f);
			m.SetShaderParameter("spin", spin0 + 15f * (1f - Mathf.Pow(1f - k, 3f)));           // 先快后慢，转到停
			m.SetShaderParameter("rings", k);
			m.SetShaderParameter("stop", s < stopAt ? 0f : Mathf.Exp(-(s - stopAt) / 0.12f));
			m.SetShaderParameter("amount", Mathf.Min(1f, s / 0.1f) * (1f - Mathf.Clamp((s - 0.95f) / 0.3f, 0f, 1f)));
		});
		Fx.After(layer, stopAt, () =>
		{
			Glow.Shockwave(layer, chest, new Color(1f, 0.9f, 0.88f), height * 2.4f, 0.5f);
			HitStop.Request(0.05);
		});
	}

	// ───────────────────────── 潜行：残像 / 暗影 ─────────────────────────

	/// <summary>
	/// 残像（残影、余像、侧步、暗影步……）：Joker 身上分出几道同姿势的残像，往身后依次滑开、拉长一点、淡掉。
	/// clone = 复制一份此刻的 Joker（游戏里是骨骼的复制品，Aim.CloneBody），返回复制品和本体的全局变换。
	/// </summary>
	public static void Ghosts(Control back, Func<Node, (Node2D Clone, Transform2D Body)?>? clone, float facing, Tone tone = Tone.Blue, int count = 3, bool sound = true)
	{
		if (sound)
		{
			Sfx.Play(Sfx.StealthVeil, 0.55f);
		}
		if (clone == null)
		{
			return;
		}
		(_, Color mid, Color deep, _) = Pal(tone);
		Color tint = mid.Lerp(deep, 0.25f);
		for (int i = 0; i < count; i++)
		{
			int k = i;
			Fx.After(back, 0.05f * k, () =>
			{
				if (clone(back) is not { } c)
				{
					return;
				}
				c.Clone.GlobalTransform = c.Body;
				Vector2 from = c.Clone.Position, sc = c.Clone.Scale;
				float dist = (70f + 60f * k) * -facing, a0 = 0.62f - 0.12f * k;
				c.Clone.Modulate = new Color(tint, a0);
				Fx.Animate(c.Clone, 0, 0.5, t =>
				{
					float e = Ease5.OutQuint(t);
					c.Clone.Position = from + new Vector2(dist * e, 0f);
					c.Clone.Scale = new Vector2(sc.X * (1f + 0.06f * e), sc.Y);
					c.Clone.Modulate = new Color(tint, a0 * (1f - t * t));
				});
				Fx.FreeAfter(c.Clone, 0.55);
			});
		}
	}

	/// <summary>
	/// 暗影裹身（融入暗影、幽魂形态）：暗影的光带盘上来，Joker 自己暗下去再慢慢恢复（tint），脚下一圈暗环。big = 形态牌。
	/// </summary>
	public static void Veil(Control front, Control? back, Vector2 feet, float height, Action<Color>? tint, bool big = false, bool sound = true)
	{
		if (sound)
		{
			Sfx.Play(Sfx.StealthVeil, 0.6f);
			Sfx.Play(Sfx.BuffDark, 0.45f, 0.1f);
		}
		Buff(front, back, feet, height, Tone.Shadow, big, sound: false);
		if (tint == null)
		{
			return;
		}
		float life = big ? 1.3f : 0.9f;
		Node2D ticker = Fx.Root(front, feet, "P5VeilTint", life + 0.1);
		Fx.Animate(ticker, 0, life, t =>
		{
			float k = Mathf.Min(1f, t / 0.2f) * (1f - Mathf.Clamp((t - 0.55f) / 0.45f, 0f, 1f));
			tint(Colors.White.Lerp(new Color(0.3f, 0.32f, 0.55f), k * 0.85f));
		});
		ticker.TreeExiting += () => tint(Colors.White);
	}

	// ───────────────────────── 暴露 / 夹击 ─────────────────────────

	/// <summary>
	/// 暴露（去掉格挡和人工制品、给易伤）：目标身前像一层护甲被击碎——一下白闪、几道裂纹、朝 dir 迸开的锐利碎片，然后压下一道减益。
	/// </summary>
	public static void Shatter(Control front, Control? back, Vector2 center, Vector2 feet, Vector2 top, float width, Vector2 dir, Action<Color>? tint = null)
	{
		float h = Mathf.Max(120f, (feet - top).Length());
		Sfx.Play(Sfx.Glass, 0.5f);
		Glow.Flash(front, center, new Color(1f, 0.85f, 0.85f), h * 0.9f, 0.14f);
		Cut.Cracks(front, center, 5, h * 0.45f, 0.28f);
		Glow.Shards(front, center, 16, 1.5f, dir, 1.9f);
		Glow.Shockwave(front, center, new Color(1f, 0.4f, 0.42f), h * 1.3f, 0.35f);
		Debuff(front, back, feet, top, width, tint, sound: false, delay: 0.12f);
	}

	/// <summary>
	/// 夹击（目标本回合受到队友的攻击伤害翻倍）：两道尖角从左右两侧合拢、扣在目标两边，一亮，停半秒，淡掉。
	/// </summary>
	public static void Mark(Control layer, Vector2 center, float width, float height, bool sound = true)
	{
		const float close = 0.16f, hold = 0.55f, fade = 0.2f, life = close + hold + fade;
		if (sound)
		{
			Sfx.Play(Sfx.ShadowMark, 0.6f);
		}
		float unit = Unit(layer), half = width * 0.5f + 34f * unit, arm = Mathf.Clamp(height * 0.34f, 60f * unit, 150f * unit);
		Node2D root = Fx.Root(layer, center, "P5Mark", life + 0.1);
		var parts = new List<(Node2D Node, float Side)>();
		foreach (float side in new[] { -1f, 1f })
		{
			Node2D n = new();
			root.AddChild(n);
			Vector2[] pts = { new(side * arm * 0.55f, -arm), new(0f, 0f), new(side * arm * 0.55f, arm) };
			Line2D glow = new() { Points = pts, Width = 13f * unit, DefaultColor = new Color(1f, 0.15f, 0.22f, 0.5f), Material = Fx.Additive, JointMode = Line2D.LineJointMode.Sharp, Antialiased = true };
			Line2D core = new() { Points = pts, Width = 3.2f * unit, DefaultColor = new Color(1f, 0.95f, 0.93f), Material = Fx.Additive, JointMode = Line2D.LineJointMode.Sharp, Antialiased = true };
			n.AddChild(glow);
			n.AddChild(core);
			parts.Add((n, side));
		}
		Fx.Animate(root, 0, life, t =>
		{
			float s = t * life;
			float e = Ease5.OutBack(Mathf.Clamp(s / close, 0f, 1f));
			float a = Mathf.Min(1f, s / 0.05f) * (1f - Mathf.Clamp((s - close - hold) / fade, 0f, 1f));
			float pulse = s > close ? 1f + 0.05f * Mathf.Sin((s - close) * 16f) * Mathf.Exp(-(s - close) / 0.25f) : 1f;
			foreach ((Node2D n, float side) in parts)
			{
				n.Position = new Vector2(-side * Mathf.Lerp(half + 320f * unit, half, e) * pulse, 0f);
				n.Modulate = new Color(1f, 1f, 1f, a);
			}
		});
		Fx.After(layer, close, () =>
		{
			Glow.Flash(layer, center, new Color(1f, 0.3f, 0.34f), height * 0.9f, 0.14f);
			Glow.Shockwave(layer, center, new Color(1f, 0.5f, 0.5f), height * 1.4f, 0.3f, 0.5f);
		});
	}
}
