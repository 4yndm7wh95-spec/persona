using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 每张牌的技能特效，第二批（用户 2026-11-10）：潜行 / 控场——融入暗影、残影、余像、暗影步、子弹时间、夹击、萎靡、扫腿。
/// 登记进 CardSkillFx.Cards（CardSkillFx 的静态构造里调 Register）。音效：tools/p5/sfx/synth_card_fx2.py。
/// </summary>
public static class CardSkillFx2
{
	public static void Register(Dictionary<string, Action<SkillCtx>> cards)
	{
		cards["Shadowmeld"] = Shadowmeld;
		cards["Blur"] = Blur;
		cards["Afterimage"] = Afterimage;
		cards["ShadowStep"] = ShadowStep;
		cards["BulletTime"] = BulletTime;
		cards["Flanking"] = Flanking;
		cards["Malaise"] = Malaise;
		cards["LegSweep"] = LegSweep;
	}

	private static readonly Color Hot = new(1f, 0.96f, 0.94f), Red = new(1f, 0.2f, 0.27f), Crimson = new(0.85f, 0.04f, 0.13f),
		Deep = new(0.3f, 0f, 0.07f), Ice = new(0.94f, 0.98f, 1f), Sky = new(0.4f, 0.8f, 1f), Blue = new(0.1f, 0.38f, 0.98f),
		Lilac = new(0.78f, 0.62f, 1f), Violet = new(0.42f, 0.2f, 0.9f), Night = new(0.05f, 0.01f, 0.12f), Brass = new(1f, 0.82f, 0.5f);

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
	private static bool Ok(Control? c) => c != null && GodotObject.IsInstanceValid(c);

	private static void TintPulse(Control layer, Vector2 at, Action<Color>? tint, Color to, float delay, float life, float hold = 0.3f)
	{
		if (tint == null) return;
		Fx.After(layer, delay, () =>
		{
			Node2D tick = Fx.Root(layer, at, "P5Tint", life + 0.1);
			Fx.Animate(tick, 0, life, t =>
			{
				float k = Ease5.Seg(t, 0f, 0.15f) * (1f - Ease5.Seg(t, hold + 0.15f, Mathf.Max(0.05f, 0.85f - hold)));
				tint(Colors.White.Lerp(to, k));
			});
			tick.TreeExiting += () => tint(Colors.White);
		});
	}

	// ═══════════════════════════ 融入暗影：沉进影子里 ═══════════════════════════

	private static Shader? _pool;

	/// <summary>
	/// 贴地的一滩影子（正方形四边形，代码里压扁）：边缘被噪声啃成墨迹、往外爬的触须，里面近黑带一点暗紫，边上一圈细的紫光。open 张开，alpha 淡出。预乘。
	/// </summary>
	private static Shader Pool => _pool ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform float open = 1.0;
uniform float alpha = 1.0;
uniform float seed = 0.0;
uniform float clock = 0.0;
" + Noise + @"
void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	float r = length(p), a = atan(p.y, p.x);
	float tendril = pow(noise(vec2(a * 3.5 + seed, clock * 0.4)), 3.0) * 0.55;
	float edge = open * (0.62 + tendril) + (fbm(p * 3.0 + seed + clock * 0.3) - 0.5) * 0.25;
	float d = r - edge;
	float inside = 1.0 - smoothstep(-0.02, 0.02, d);
	float rim = exp(-abs(d) / 0.025) * (0.5 + 0.5 * noise(vec2(a * 12.0, clock * 3.0)));
	vec3 col = vec3(0.1, 0.03, 0.22) * inside * 0.5 + vec3(0.55, 0.35, 1.0) * rim * 0.7;
	float aa = clamp(inside * 0.88 + rim * 0.3, 0.0, 0.95) * alpha * (1.0 - smoothstep(0.92, 1.0, r));
	COLOR = vec4(col * alpha, aa);
}" };

	private static void ShadowPool(Control layer, Vector2 feet, float size, float life, float closeAt)
	{
		Node2D root = Fx.Root(layer, feet, "P5ShadowPool", life + 0.1);
		ColorRect q = Quad(root, Pool, Vector2.One * size);
		q.Scale = new Vector2(1f, 0.3f);
		Set(q, "seed", Fx.Rand(0f, 40f));
		Fx.Clock(q);
		Fx.Animate(q, 0, life, t =>
		{
			float s = t * life;
			Set(q, "open", Ease5.OutExpo(Mathf.Min(1f, s / 0.25f)) * (1f - Fx.InCubic(Mathf.Clamp((s - closeAt) / (life - closeAt), 0f, 1f))));
			Set(q, "alpha", 1f - Mathf.Clamp((s - life + 0.12f) / 0.12f, 0f, 1f));
		});
	}

	/// <summary>
	/// 黑色的影手 / 触须：从 root 往上长出来、扭着、再缩回去。身子近黑，边上一层暗紫的光。
	/// </summary>
	private static void Tendril(Control layer, Vector2 root, float len, float lean, float delay, float life, float w)
	{
		Node2D n = new() { Name = "P5Tendril" };
		layer.AddChild(n);
		n.GlobalPosition = root;
		Fx.FreeAfter(n, delay + life + 0.1);
		Polygon2D glow = Fx.Poly(n, new[] { Vector2.Zero, Vector2.Right, Vector2.Down }, new Color(0.5f, 0.3f, 1f, 0.35f), additive: true);
		Polygon2D body = Fx.Poly(n, new[] { Vector2.Zero, Vector2.Right, Vector2.Down }, new Color(0.03f, 0f, 0.06f, 0.95f));
		n.Visible = false;
		float ph = Fx.Rand(0f, 6f);
		Fx.Animate(n, delay, life, t =>
		{
			n.Visible = true;
			float grow = Ease5.OutQuint(Mathf.Min(1f, t / 0.35f)) * (1f - Fx.InCubic(Mathf.Clamp((t - 0.65f) / 0.35f, 0f, 1f)));
			var pts = new List<Vector2>();
			const int N = 14;
			for (int i = 0; i < N; i++)
			{
				float b = 1f - (float)i / (N - 1);          // i = 0 是根（b = 1），最后是尖
				float y = -len * grow * (1f - b);
				float x = lean * len * grow * (1f - b) * (1f - b) + Mathf.Sin(ph + (1f - b) * 5f + t * 9f) * w * 1.6f * (1f - b);
				pts.Add(new Vector2(x, y));
			}
			pts.Reverse();                                   // 从尖到根：Ribbon 最后一点最宽 = 根部最粗
			glow.Polygon = Glow.Ribbon(pts, w * 2.4f);
			body.Polygon = Glow.Ribbon(pts, w);
		});
	}

	/// <summary>
	/// 融入暗影（本回合格挡翻倍）：脚下漫开一滩墨一样的影子，七八根黑色影手从影子里长出来、扭着裹住他的腿和腰，
	/// 人沉成近黑的剪影、只剩一圈暗紫的轮廓光；随后影手缩回、影子收拢，人回来，身前一圈紫光弹开两次（翻倍）。1.3s。
	/// </summary>
	public static void Shadowmeld(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit;
		Sfx.Play(Sfx.ShadowmeldSink, 0.45f);
		Control back = Ok(c.Back) ? c.Back! : c.Front;
		ShadowPool(back, c.Feet, h * 1.9f, 1.25f, 0.85f);
		for (int i = 0; i < 8; i++)
		{
			float x = (i - 3.5f) / 3.5f;
			Vector2 at = c.Feet + new Vector2(x * h * 0.45f, h * Fx.Rand(-0.02f, 0.04f));
			Control layer = i % 2 == 0 ? back : c.Front;
			Tendril(layer, at, h * Fx.Rand(0.45f, 0.8f) * (1f - 0.35f * Mathf.Abs(x)), -x * 0.35f, 0.05f + 0.04f * i, 0.95f, h * 0.04f);
		}
		TintPulse(c.Front, c.Feet, c.SelfTint, new Color(0.16f, 0.12f, 0.3f), 0.12f, 1.0f, 0.4f);
		Glow.Sparks(c.Front, c.Feet, 20, 30f, 140f, 1.0f, new[] { Lilac, Violet, Night }, -60f, 0.03f * u, 0.07f * u, Vector2.Up, 70f);
		for (int i = 0; i < 2; i++)
		{
			float d = 0.85f + 0.13f * i;
			Fx.After(c.Front, d, () =>
			{
				Glow.Shockwave(c.Front, c.Chest, Lilac, h * (1.3f + 0.4f * i), 0.4f, 0.5f);
				Glow.Flash(c.Front, c.Chest, Violet, h * 0.6f, 0.15f);
			});
		}
	}

	// ═══════════════════════════ 残影：一闪而过的残像 ═══════════════════════════

	/// <summary>
	/// 残影（格挡下回合不消失）：他往旁边一闪——身后留下三道纯红的人形剪影依次拉开，地上贴着划出几道横的速度线，
	/// 剪影定住的那一下身前一层细碎的红光「定」住（格挡被留住），再慢慢褪。0.9s。
	/// </summary>
	public static void Blur(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit;
		// 用户 2026-11-12：残影只要一层红色剪影，贴身、不用很明显（格挡照旧是蜂巢护盾，走 GainBlockPatch）；速度线、闪光、冲击波、火星都去掉
		if (Ok(c.Back) && c.CopyBody != null)
		{
			SkillFx.Ghosts(c.Back!, c.CopyBody, c.Region, c.Face, 1, sound: false, subtle: true);
		}
	}

	private static void Streakline(Control layer, Vector2 at, Vector2 dir, float len, float life, Color core, Color glow, float width, float delay = 0f)
	{
		Node2D root = Fx.Root(layer, at, "P5Streak", delay + life + 0.1);
		Line2D g = Glow.Line(root, new[] { Vector2.Zero, dir }, new Color(glow, 0.8f), width * 3f, false);
		Line2D k = Glow.Line(root, new[] { Vector2.Zero, dir }, core, width, false);
		root.Visible = false;
		Fx.Animate(root, delay, life, t =>
		{
			root.Visible = true;
			Vector2 a = dir * len * Ease5.OutQuint(t), b = dir * len * Fx.InCubic(t);
			if (a.DistanceTo(b) < 1f) b = a - dir;
			g.Points = k.Points = new[] { a, b };
			root.Modulate = new Color(1f, 1f, 1f, 1f - Ease5.Seg(t, 0.6f, 0.4f));
		});
	}

	// ═══════════════════════════ 余像：绕身的光之回声 ═══════════════════════════

	/// <summary>
	/// 余像（每打出一张牌获得格挡）：六点蓝白的光绕着他转（椭圆轨道，转到身后变暗被挡住），拖出越来越长的光尾，
	/// 转两圈后一齐收进胸口、一亮，身上叠起两层淡蓝的人形剪影一闪即散，一圈细环弹开。1.1s。
	/// </summary>
	public static void Afterimage(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit;
		Sfx.Play(Sfx.AfterimageOrbit, 0.4f);
		const int n = 6;
		const float spin = 0.8f;
		Control back = Ok(c.Back) ? c.Back! : c.Front;
		for (int i = 0; i < n; i++)
		{
			float ph = Mathf.Tau * i / n, yOff = Mathf.Lerp(-0.15f, -0.85f, i / (n - 1f));
			Node2D fr = Fx.Root(c.Front, c.Chest, "P5Orbit", spin + 0.3);
			Node2D bk = Fx.Root(back, c.Chest, "P5OrbitBack", spin + 0.3);
			Sprite2D sf = Glow.SoftSprite(fr, Vector2.Zero, Ice, 0.5f * u);
			Sprite2D sb = Glow.SoftSprite(bk, Vector2.Zero, Sky, 0.4f * u);
			Line2D tf = Glow.Line(fr, new[] { Vector2.Zero, Vector2.Right }, new Color(Sky, 0.8f), 4f * u, false);
			Line2D tb = Glow.Line(bk, new[] { Vector2.Zero, Vector2.Right }, new Color(Blue, 0.6f), 3f * u, false);
			var trail = new List<Vector2>();
			Vector2 center = c.Feet + new Vector2(0f, h * yOff);
			Fx.Animate(fr, 0.02f * i, spin, t =>
			{
				float a = ph + t * Mathf.Tau * 2f;
				float rad = h * 0.42f * (1f - Ease5.Seg(t, 0.78f, 0.22f));
				Vector2 pos = center.Lerp(c.Chest, Ease5.Seg(t, 0.78f, 0.22f)) + new Vector2(Mathf.Cos(a) * rad, Mathf.Sin(a) * rad * 0.22f);
				bool front = Mathf.Sin(a) > 0f;
				trail.Add(pos - c.Chest);
				int keep = 4 + (int)(10 * t);
				while (trail.Count > keep) trail.RemoveAt(0);
				sf.Position = sb.Position = pos - c.Chest;
				sf.Visible = front; sb.Visible = !front;
				Vector2[] tp = trail.ToArray();
				tf.Points = tp; tb.Points = tp;
				tf.Visible = front; tb.Visible = !front;
			});
		}
		Fx.After(c.Front, spin, () =>
		{
			Glow.Flash(c.Front, c.Chest, Sky, h * 0.8f, 0.2f);
			Glow.Shockwave(c.Front, c.Chest, Ice, h * 1.6f, 0.45f, 0.5f);
			if (Ok(c.Back) && c.CopyBody != null)
			{
				SkillFx.Ghosts(c.Back!, c.CopyBody, c.Region, -c.Face, 2, sound: false);
			}
			TintPulse(c.Front, c.Feet, c.SelfTint, new Color(0.7f, 0.95f, 1.3f), 0f, 0.4f, 0.05f);
		});
	}

	// ═══════════════════════════ 暗影步：消失在影子里，带着杀意出来 ═══════════════════════════

	/// <summary>
	/// 暗影步（弃掉手牌，下回合攻击伤害翻倍）：手里的牌一甩烧掉，他整个人「唰」地被一道黑红的斜斩切开、沉进影子里不见，
	/// 片刻后从影子里升回来，眼睛（面具）一点红光，手上缠着一圈暗红的杀意（下回合翻倍）。1.3s。
	/// </summary>
	public static void ShadowStep(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit;
		Sfx.Play(Sfx.ShadowStepCut, 0.45f);
		SkillFx.Discard(c.Front, c.Hand, 3, c.Face, sound: false);
		Control back = Ok(c.Back) ? c.Back! : c.Front;
		Fx.After(c.Front, 0.22f, () =>
		{
			Cut.Razor(c.Front, c.Chest, c.Face > 0f ? -0.95f : Mathf.Pi + 0.95f, h * 1.3f, 5f * u, 0.14f);
			Glow.Flash(c.Front, c.Chest, Red, h * 0.8f, 0.14f);
			ShadowPool(back, c.Feet, h * 1.4f, 0.9f, 0.6f);
		});
		if (c.SelfTint is { } tint)
		{
			Node2D tick = Fx.Root(c.Front, c.Feet, "P5StepTint", 1.4);
			Fx.Animate(tick, 0, 1.3, t =>
			{
				float s = t * 1.3f;
				float gone = Mathf.Clamp((s - 0.24f) / 0.08f, 0f, 1f) * (1f - Mathf.Clamp((s - 0.75f) / 0.25f, 0f, 1f));
				Color col = new Color(0.1f, 0.05f, 0.15f, 1f - gone).Lerp(Colors.White, Mathf.Clamp((s - 0.85f) / 0.3f, 0f, 1f));
				tint(s < 0.24f ? Colors.White : col);
			});
			tick.TreeExiting += () => tint(Colors.White);
		}
		Fx.After(c.Front, 0.28f, () =>
			Glow.Sparks(c.Front, c.Chest, 26, 60f, 260f, 0.6f, new[] { Red, Crimson, Night }, 120f, 0.03f * u, 0.08f * u, Vector2.Down, 70f));
		Fx.After(c.Front, 0.8f, () =>
		{
			Glow.Glint(c.Front, c.Mask + new Vector2(c.Face * h * 0.03f, 0f), 1.0f);
			Glow.Flash(c.Front, c.Mask, Red, h * 0.35f, 0.18f);
			Glow.Shockwave(c.Front, c.Feet, Crimson, h * 1.4f, 0.4f, 0.22f);
			// 手上的杀意：暗红的光环绕着手转两圈、往里收
			Node2D rr = Fx.Root(c.Front, c.Hand, "P5Intent", 0.7);
			for (int i = 0; i < 2; i++)
			{
				ColorRect ring = Glow.RingRect(rr, h * 0.5f, 0.45f);
				Glow.SetParam(ring, "tint", i == 0 ? Red : Hot);
				int ii = i;
				Fx.Animate(ring, 0.06f * i, 0.55, t =>
				{
					Glow.SetParam(ring, "radius", Mathf.Lerp(0.9f, 0.25f, Ease5.OutQuint(t)));
					Glow.SetParam(ring, "width", 0.02f + 0.02f * ii);
					Glow.SetParam(ring, "alpha", Mathf.Min(1f, t * 6f) * (1f - Ease5.Seg(t, 0.6f, 0.4f)));
					ring.Rotation = t * 4f;
				});
			}
			Glow.Sparks(c.Front, c.Hand, 12, 40f, 140f, 0.5f, new[] { Hot, Red, Deep }, -100f, 0.02f * u, 0.05f * u, Vector2.Up, 100f);
		});
	}

	// ═══════════════════════════ 子弹时间：只有他在动的世界 ═══════════════════════════

	/// <summary>子弹时间：时停到回合结束（Vfx/TimeStop.cs；回合结束的接法在 BulletTimePlay.cs）。</summary>
	public static void BulletTime(SkillCtx c) => TimeStop.Start(c.Front, c.Feet, c.Tall, c.Face);

	// ═══════════════════════════ 夹击：左右两刃交叉钉住 ═══════════════════════════

	/// <summary>
	/// 夹击（本回合其他玩家对它造成双倍攻击伤害）：两道红白的光刃从目标左右两边同时斩进来、在它身上交叉成一个 X，交叉处一亮、
	/// 碎光往两边迸开；X 留着烧一会儿，四个角括号扣上锁住目标，红光一闪。0.9s。
	/// </summary>
	public static void Flanking(SkillCtx c)
	{
		if (c.Target is not SkillFx.Victim v) return;
		float h = Mathf.Max(120f, (v.Feet - v.Top).Length()), u = c.Unit;
		Vector2 at = v.Feet.Lerp(v.Top, 0.5f);
		Sfx.Play(Sfx.FlankCross, 0.45f);
		float half = Mathf.Max(v.Width, h * 0.6f) * 0.9f;
		for (int i = 0; i < 2; i++)
		{
			float side = i == 0 ? -1f : 1f;
			Vector2 from = at + new Vector2(side * half * 1.6f, -h * 0.55f), to = at + new Vector2(-side * half * 0.9f, h * 0.45f);
			float ang = (to - from).Angle();
			Glow.Blade(c.Front, from.Lerp(to, 0.5f), ang, from.DistanceTo(to), 26f * u, 0.4f);
		}
		Fx.After(c.Front, 0.09f, () =>
		{
			Glow.Flash(c.Front, at, Hot, h * 0.7f, 0.16f);
			Glow.Flash(c.Front, at, Red, h * 1.3f, 0.3f);
			Glow.Shards(c.Front, at, 10, 0.7f, Vector2.Left, 0.6f);
			Glow.Shards(c.Front, at, 10, 0.7f, Vector2.Right, 0.6f);
			HitStop.Request(0.04);
			Fx.Shake(1);
		});
		Fx.After(c.Front, 0.2f, () =>
		{
			SkillFx.Mark(c.Front, at, half * 1.4f, h, sound: false);
			if (v.Tint is { } tint)
			{
				Node2D tick = Fx.Root(c.Front, at, "P5FlankTint", 0.6);
				Fx.Animate(tick, 0, 0.5, t => tint(Colors.White.Lerp(new Color(1.6f, 0.5f, 0.55f), Mathf.Exp(-t * 5f) * 0.9f)));
				tick.TreeExiting += () => tint(Colors.White);
			}
		});
	}

	// ═══════════════════════════ 萎靡：压垮 ═══════════════════════════

	/// <summary>
	/// 萎靡（敌人失去 X 点力量、获得 X 层虚弱）：目标头顶压下一团翻滚的暗红咒云，云里往下滴暗红的液丝，
	/// 接着中和的锁环箍住、三道 V 砸下；敌人被压得一矮（往下一沉）、颜色被抽干变灰暗，红色的力量光点从它身上被抽出来、飘向 Joker 散掉。1.4s。
	/// </summary>
	public static void Malaise(SkillCtx c)
	{
		if (c.Target is not SkillFx.Victim v) return;
		float h = Mathf.Max(120f, (v.Feet - v.Top).Length()), u = c.Unit;
		Vector2 center = v.Feet.Lerp(v.Top, 0.5f), head = v.Top + new Vector2(0f, -h * 0.25f);
		Sfx.Play(Sfx.MalaisePress, 0.45f);
		// 咒云
		Node2D cloud = Fx.Root(c.Front, head, "P5MalaiseCloud", 1.3);
		ColorRect q = Quad(cloud, CloudShader, new Vector2(Mathf.Max(v.Width * 1.6f, h * 1.1f), h * 0.7f));
		Set(q, "seed", Fx.Rand(0f, 30f));
		Fx.Clock(q);
		Fx.Animate(q, 0, 1.25, t =>
		{
			Set(q, "grow", Ease5.OutExpo(Ease5.Seg(t, 0f, 0.2f)));
			Set(q, "alpha", 1f - Ease5.Seg(t, 0.7f, 0.3f));
			cloud.GlobalPosition = head + new Vector2(0f, h * 0.18f * Ease5.OutQuint(Ease5.Seg(t, 0.15f, 0.3f)));
		});
		for (int i = 0; i < 6; i++)
		{
			float x = Fx.Rand(-0.4f, 0.4f) * Mathf.Max(v.Width, h * 0.6f);
			DripLine(c.Front, head + new Vector2(x, h * 0.15f), h * Fx.Rand(0.3f, 0.6f), 0.15f + 0.07f * i, u);
		}
		CardFx.Weaken(c.Front, center, v.Top, 0.3f);
		TintPulse(c.Front, center, v.Tint, new Color(0.45f, 0.38f, 0.42f), 0.4f, 1.0f, 0.5f);
		// 力量被抽走
		Fx.After(c.Front, 0.55f, () =>
		{
			for (int i = 0; i < 7; i++)
			{
				Vector2 from = center + new Vector2(Fx.Rand(-0.3f, 0.3f) * v.Width, Fx.Rand(-0.3f, 0.3f) * h);
				Wisp(c.Front, from, c.Chest, 0.05f * i, u, h);
			}
		});
	}

	private static Shader? _cloud;

	/// <summary>一团翻滚的暗红咒云（四边形内的椭圆，fbm 起伏的边，下沿更亮、往下鼓）。grow 张开，alpha 淡出。预乘。</summary>
	private static Shader CloudShader => _cloud ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform float grow = 1.0;
uniform float alpha = 1.0;
uniform float seed = 0.0;
uniform float clock = 0.0;
" + Noise + @"
void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	float n = fbm(p * 2.2 + vec2(clock * 0.5, -clock * 0.3) + seed);
	float d = length(p * vec2(1.0, 1.5)) - grow * (0.55 + 0.4 * n);
	float inside = 1.0 - smoothstep(-0.1, 0.05, d);
	float lower = smoothstep(-0.2, 0.6, p.y);
	float glow = fbm(p * 5.0 - clock * 0.8 + seed * 2.0);
	vec3 col = vec3(0.12, 0.0, 0.03) * inside + vec3(0.8, 0.05, 0.15) * inside * lower * glow * 0.9;
	float a = clamp(inside * (0.6 + 0.35 * n), 0.0, 0.9) * alpha * (1.0 - smoothstep(0.85, 1.0, max(abs(p.x), abs(p.y))));
	COLOR = vec4(col * alpha, a);
}" };

	/// <summary>一根往下拉长的暗红液丝：从 from 往下长 len，断开，下面那滴落下去。</summary>
	private static void DripLine(Control layer, Vector2 from, float len, float delay, float u)
	{
		Node2D root = Fx.Root(layer, from, "P5DripLine", delay + 0.8);
		Line2D glow = Glow.Line(root, new[] { Vector2.Zero, Vector2.Down }, new Color(0.9f, 0.08f, 0.18f, 0.6f), 6f * u, false);
		Line2D core = Glow.Line(root, new[] { Vector2.Zero, Vector2.Down }, new Color(0.25f, 0f, 0.05f), 2.5f * u, false);
		core.Material = null;
		root.Visible = false;
		Fx.Animate(root, delay, 0.7, t =>
		{
			root.Visible = true;
			float head = len * Ease5.OutQuint(Mathf.Min(1f, t / 0.5f)), tail = len * Fx.InCubic(Ease5.Seg(t, 0.45f, 0.55f)) * 1.3f;
			Vector2[] pts = { new(0f, Mathf.Min(tail, head - 1f)), new(0f, head + len * 0.6f * Ease5.Seg(t, 0.5f, 0.5f)) };
			glow.Points = core.Points = pts;
			root.Modulate = new Color(1f, 1f, 1f, 1f - Ease5.Seg(t, 0.75f, 0.25f));
		});
	}

	/// <summary>一缕被抽走的红光：从 from 弯着飘向 to（贝塞尔），越飘越淡。</summary>
	private static void Wisp(Control layer, Vector2 from, Vector2 to, float delay, float u, float h)
	{
		Node2D root = Fx.Root(layer, from, "P5Wisp", delay + 0.75);
		Sprite2D dot = Glow.SoftSprite(root, Vector2.Zero, new Color(1f, 0.25f, 0.3f, 0.95f), 0.45f * u);
		Line2D trail = Glow.Line(root, new[] { Vector2.Zero, Vector2.Right }, new Color(1f, 0.2f, 0.27f, 0.6f), 3f * u, false);
		var pts = new List<Vector2>();
		Vector2 ctrl = from.Lerp(to, 0.5f) + new Vector2(0f, -h * Fx.Rand(0.4f, 0.8f));
		root.Visible = false;
		Fx.Animate(root, delay, 0.7, t =>
		{
			root.Visible = true;
			float k = Fx.OutCubic(t);
			Vector2 p = from.Lerp(ctrl, k).Lerp(ctrl.Lerp(to, k), k);
			dot.GlobalPosition = p;
			pts.Add(p - root.GlobalPosition);
			while (pts.Count > 8) pts.RemoveAt(0);
			trail.Points = pts.ToArray();
			root.Modulate = new Color(1f, 1f, 1f, 1f - Ease5.Seg(t, 0.6f, 0.4f));
		});
	}

	// ═══════════════════════════ 扫腿 ═══════════════════════════

	/// <summary>
	/// 扫腿（虚弱 + 格挡）：一道贴地的弧形光刃从目标脚下横扫过去（白芯红晕），扫过的地面扬起一溜尘土和碎光、地上留一道发光的弧痕；
	/// 目标被扫得一晃（往后一倾），然后中和的锁环 + V 压下（虚弱）。1s。格挡的蜂巢盾由格挡钩子另放。
	/// </summary>
	public static void LegSweep(SkillCtx c)
	{
		if (c.Target is not SkillFx.Victim v) return;
		float h = Mathf.Max(120f, (v.Feet - v.Top).Length()), u = c.Unit;
		Sfx.Play(Sfx.SweepLow, 0.45f);
		float wdt = Mathf.Max(v.Width, h * 0.7f);
		Vector2 ground = v.Feet + new Vector2(0f, -h * 0.06f);
		Paint.Sweep(c.Front, "slash_thin", ground, c.Face > 0f ? 0f : Mathf.Pi, new Vector2(0.9f, 0.22f) * u * wdt / 300f, Lut5.Blood, 0.06f, 0.04f, 0.25f, 0f, 0f, 1.1f);
		Glow.Blade(c.Front, ground, c.Face > 0f ? 0f : Mathf.Pi, wdt * 1.6f, 18f * u, 0.3f);
		for (int i = 0; i < 6; i++)
		{
			float k = i / 5f;
			Vector2 at = ground + new Vector2((k - 0.5f) * wdt * 1.4f * c.Face, 0f);
			Fx.After(c.Front, 0.03f * i, () =>
			{
				Glow.Sparks(c.Front, at, 6, 80f, 260f, 0.4f, new[] { Hot, Red, new Color(0.35f, 0.3f, 0.28f) }, 600f, 0.02f * u, 0.06f * u, new Vector2(c.Face * 0.4f, -1f), 40f);
			});
		}
		Fx.After(c.Front, 0.1f, () =>
		{
			Glow.Shockwave(c.Front, v.Feet, Red, wdt * 1.6f, 0.35f, 0.22f);
			HitStop.Request(0.035);
			Fx.Shake(1);
		});
		CardFx.Weaken(c.Front, v.Feet.Lerp(v.Top, 0.5f), v.Top, 0.3f);
		TintPulse(c.Front, v.Feet, v.Tint, new Color(0.6f, 0.45f, 0.5f), 0.35f, 0.7f, 0.2f);
	}
}
