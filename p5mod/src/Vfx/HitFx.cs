using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 打击 / 开枪的代码特效。2026-11-01 用户：之前的太朴素 → 全部换成 P5 的平面漫画语言（Ink.cs：硬边、黑描边、红白黑、爆点、网点、碎片），
/// 都很短、很干脆：
///  · Swing / Slash —— 打击（2026-11-01 设计说明 docs/p5/FX_DESIGN_HITS.md，Blade.cs）：三层月牙刀光，入 / 满 / 停 / 出四段，
///    命中顿帧 50ms + 第二刀交叉 + 细红冲击环 + 空间扭曲 + 余韵速度线；
///  · Muzzle —— 开火：拉长的锯齿枪焰 + 原版枪焰 + 黑红环 + 弹壳 + 平面烟；
///  · Shot —— 黑描边弹道，命中处原版枪击爆点序列帧 + 爆点 + 网点 + 碎片。
/// 只用全局坐标，游戏里由 JokerFx 按姿势图换算好位置传进来，预览工具直接传。
/// </summary>
public static class HitFx
{
	private static float Wrap(float a) => Mathf.PosMod(a + Mathf.Pi, Mathf.Tau) - Mathf.Pi;

	/// <summary>
	/// 刀光（2026-11-01 重做成 P5 平面风）：以 center 为圆心从 from 扫到 to 的硬边月牙 —— 黑描边、白刃、内侧一道红；
	/// 外侧和内侧各跟一道细的黑色速度弧。头 0.06s 扫到底（缓出），尾巴随后追上收掉，0.2s。
	/// </summary>
	public static void Swing(Control layer, Vector2 center, Vector2 from, Vector2 to)
	{
		// 刀光（Blade.Arc）：沿刀尖真实轨迹的弦画月牙，往背离圆心的方向拱；比命中斩痕窄、快，不炸光点
		Vector2 mid = (from + to) / 2f;
		float ang = (to - from).Angle(), chord = from.DistanceTo(to);
		float r = (from.DistanceTo(center) + to.DistanceTo(center)) / 2f;
		float bow = r - Mathf.Sqrt(Mathf.Max(0f, r * r - chord * chord / 4f));
		bool flip = (mid - center).Dot(Vector2.Up.Rotated(ang)) < 0f;
		Blade.Arc(layer, mid, ang, chord * 1.08f, Mathf.Clamp(bow, 20f, 160f), 46f, flip, 0.05f, 0.03f, 0.03f, 0.13f, false);
	}

	/// <summary>弧形带：沿圆弧从 tail 到 head（0~1 的扫过比例），头部最厚 width，往尾巴收成尖。</summary>
	private static Vector2[] Band(float r, float a0, float sweep, float tail, float head, float width)
	{
		const int n = 20;
		if (head - tail < 0.01f)
		{
			return new Vector2[3];
		}
		var pts = new Vector2[n * 2];
		for (int i = 0; i < n; i++)
		{
			float u = (float)i / (n - 1);
			float a = a0 + sweep * Mathf.Lerp(tail, head, u);
			float w = width * Mathf.Pow(u, 1.4f);
			Vector2 d = Vector2.Right.Rotated(a);
			pts[i] = d * r;
			pts[n * 2 - 1 - i] = d * (r - w);
		}
		return pts;
	}

	/// <summary>
	/// 打击命中（2026-11-01 重做成 P5 平面风）：
	/// 一道长斩线沿上撩方向划过（黑描边 + 白芯 + 错开的红线），60ms 后第二道短斩线交叉；
	/// 斩点炸开红白黑锯齿爆点、一圈红网点往外扩、黑白速度线、平面三角碎片顺刀路喷出去、两颗原版星光；轻震一下。
	/// </summary>
	public static void Slash(Control layer, Vector2 at, float facing = 1f)
	{
		// 命中 0ms：顿帧 50ms + 轻震；一道斜向斩痕（三层月牙，入 / 满 / 停 / 出）+ 小白闪 + 细红冲击环 + 轻微空间扭曲
		HitStop.Request(0.05);
		Fx.Shake(1);
		float a1 = facing > 0f ? -0.55f : Mathf.Pi + 0.55f;
		// 月牙往外拱 60px：弦往拱的反方向挪，让弧线正好穿过命中点
		Vector2 down = Vector2.Down.Rotated(a1) * (facing < 0f ? -1f : 1f);
		Blade.Arc(layer, at + down * 52f, a1, 640f, 60f, 64f, facing < 0f);
		Blade.Ring(layer, at, 24f, 140f, 6f, 0.22f, 0f, 0.5f);
		Glow.Ripple(layer, at, 380f, 0.4f, 0.022f);
		// +60ms：第二道短斩痕交叉成 X
		float a2 = a1 + 1.35f * facing;
		Vector2 down2 = Vector2.Down.Rotated(a2) * (facing < 0f ? -1f : 1f);
		Blade.Arc(layer, at + new Vector2(8f * facing, 4f) + down2 * 30f, a2, 400f, 34f, 42f, facing < 0f, 0.05f, 0.04f, 0.04f, 0.14f, true, 0.06f);
		// +150ms：余韵，沿刀路的细速度线
		Ink.Streaks(layer, at, 10, 100f, 360f, 0.24f, 0.15f, 4f);                // 余韵：实色的红 / 白 / 黑细速度线（不发粉）
	}

	/// <summary>
	/// 开火（设计说明 docs/p5/FX_DESIGN_HITS.md 第 2 项，和打击同一套材质：薄红辉光 + 实色红 + 白芯）：
	/// 枪口一个往前拉长的十字闪（往前长、往后短）+ 三根往前的细光刺，0.07s 炸开收掉；一圈侧着看的细红环往前推出去；
	/// 火星往前喷；一枚金色弹壳往后上方弹出去。muzzle = 枪口，facing = 朝向。
	/// </summary>
	public static void Muzzle(Control layer, Vector2 muzzle, float facing = 1f)
	{
		Node2D root = Fx.Root(layer, muzzle, "P5Muzzle", 0.6);
		Node2D flash = new() { Scale = new Vector2(facing, 1f) };
		root.AddChild(flash);
		Polygon2D glow = Fx.Poly(flash, Blade.Cross(150f, 44f, 15f), new Color(1f, 0.02f, 0.07f, 0.8f), additive: true);
		Polygon2D red = Fx.Poly(flash, Blade.Cross(112f, 30f, 10f), new Color(1f, 0.04f, 0.08f));
		Polygon2D core = Fx.Poly(flash, Blade.Cross(82f, 18f, 5f), Ink.White);
		var spikes = new (Polygon2D G, Polygon2D C, float A, float L)[3];
		for (int i = 0; i < 3; i++)
		{
			spikes[i] = (Glow.TaperPoly(flash, new Color(1f, 0.05f, 0.1f, 0.9f)), Fx.Poly(flash, new Vector2[3], Ink.White), (i - 1) * 0.32f, i == 1 ? 190f : 110f);
		}
		Fx.Animate(flash, 0, 0.09, t =>
		{
			float pop = t < 0.3f ? Ease5.OutExpo(t / 0.3f) : 1f - Fx.InCubic((t - 0.3f) / 0.7f);
			glow.Scale = red.Scale = core.Scale = new Vector2(pop * (1f + 0.4f * t), pop);
			foreach (var (g, c, ang, len) in spikes)
			{
				Vector2 d = Vector2.Right.Rotated(ang);
				float l = len * Ease5.OutExpo(Mathf.Min(1f, t * 3f)), w = 9f * (1f - t);
				g.Polygon = Glow.TaperPts(d * 10f, d * l, w);
				c.Polygon = Glow.TaperPts(d * 10f, d * l * 0.85f, w * 0.35f);
			}
		});
		Glow.Flash(layer, muzzle + new Vector2(20f * facing, 0f), new Color(1f, 0.1f, 0.12f), 160f, 0.12f);
		// 细红环：侧着看（压扁成竖的椭圆），往前推
		Node2D ringN = new() { Position = new Vector2(24f * facing, 0f), Scale = new Vector2(0.38f, 1f) };
		root.AddChild(ringN);
		Line2D ringR = new() { Points = Fx.Circle(1f, 40), Closed = true, DefaultColor = new Color(1f, 0.04f, 0.08f) };
		Line2D ringW = new() { Points = Fx.Circle(1f, 40), Closed = true, DefaultColor = Ink.White };
		ringN.AddChild(ringR);
		ringN.AddChild(ringW);
		Fx.Animate(ringN, 0, 0.18, t =>
		{
			float r = 14f + 60f * Ease5.OutExpo(t), w = 7f * (1f - Fx.OutCubic(t));
			ringN.Position = new Vector2((24f + 70f * Ease5.OutExpo(t)) * facing, 0f);
			ringR.Scale = ringW.Scale = Vector2.One * r;
			ringR.Width = w / r;
			ringW.Width = w * 0.35f / r;
		});
		Glow.Sparks(layer, muzzle, 14, 400f, 1000f, 0.25f, new[] { Pal5.Hot, Pal5.Gold, Pal5.Bright }, 300f, 0.07f, 0.2f, new Vector2(facing, 0f), 18f);
		// 弹壳
		Node2D shell = new() { Position = new Vector2(-24f * facing, -6f) };
		root.AddChild(shell);
		Fx.Poly(shell, new[] { new Vector2(-8f, -3.5f), new Vector2(8f, -3.5f), new Vector2(8f, 3.5f), new Vector2(-8f, 3.5f) }, Ink.Gold);
		Vector2 v = new(-240f * facing, -440f);
		Fx.Animate(shell, 0, 0.5, t =>
		{
			float st = t * 0.5f;
			shell.Position = new Vector2(-24f * facing, -6f) + v * st + new Vector2(0f, 1600f * st * st);
			shell.Rotation = -20f * facing * st;
			shell.Modulate = new Color(1f, 1f, 1f, 1f - Ease5.Seg(t, 0.75f, 0.25f));
		});
		Fx.Shake(1);
	}

	/// <summary>
	/// 弹道 + 命中：一道极细的白热弹道（外面薄红辉光）0.08s 射到 hit、尾巴随后收掉；
	/// 命中：顿帧 30ms，白色小闪 + 细红冲击环 + 放射状的细光刺（像子弹打进去的星形裂光）+ 顺着弹道溅出去的火星。
	/// </summary>
	public static void Shot(Control layer, Vector2 muzzle, Vector2 hit, float facing = 1f)
	{
		Tracer(layer, muzzle, hit);
		HitStop.Request(0.03);
		GunImpact(layer, hit, (hit - muzzle).Normalized(), 1f);
	}

	private static void Tracer(Control layer, Vector2 from, Vector2 to)
	{
		Node2D tr = Fx.Root(layer, Vector2.Zero, "P5Tracer", 0.2);
		tr.GlobalPosition = Vector2.Zero;
		Polygon2D glow = Glow.TaperPoly(tr, new Color(1f, 0.03f, 0.08f, 0.85f));
		Polygon2D core = Fx.Poly(tr, new Vector2[3], Ink.White);
		Polygon2D red = Fx.Poly(tr, new Vector2[3], new Color(1f, 0.03f, 0.08f));
		tr.MoveChild(red, 1);
		Fx.Animate(tr, 0, 0.16, t =>
		{
			Vector2 a = from.Lerp(to, Fx.InCubic(Mathf.Clamp((t - 0.2f) / 0.8f, 0f, 1f))), b = from.Lerp(to, Ease5.OutExpo(Mathf.Min(1f, t * 4f)));
			glow.Polygon = Glow.TaperPts(a, b, 22f * (1f - t) + 3f);
			red.Polygon = Glow.TaperPts(a, b, 9f * (1f - t) + 2f);
			core.Polygon = Glow.TaperPts(a, b, 4f * (1f - t) + 1f);
		});
	}

	/// <summary>枪击命中（不含顿帧）：白闪 + 细红冲击环 + 放射细光刺 + 火星；k = 大小。</summary>
	private static void GunImpact(Control layer, Vector2 at, Vector2 dir, float k)
	{
		// 实色的红白十字闪（和枪口同一套，斜一点），0.1s 炸开收掉
		Node2D star = Fx.Root(layer, at, "P5GunStar", 0.15);
		star.Rotation = Fx.Rand(-0.5f, 0.5f);
		Polygon2D sg = Fx.Poly(star, Blade.Cross(110f * k, 110f * k, 14f * k), new Color(1f, 0.02f, 0.07f, 0.75f), additive: true);
		Polygon2D sr = Fx.Poly(star, Blade.Cross(80f * k, 80f * k, 9f * k), new Color(1f, 0.04f, 0.08f));
		Polygon2D sw = Fx.Poly(star, Blade.Cross(55f * k, 55f * k, 5f * k), Ink.White);
		Fx.Animate(star, 0, 0.12, t =>
		{
			float pop = t < 0.25f ? Ease5.OutExpo(t / 0.25f) : 1f - Fx.InCubic((t - 0.25f) / 0.75f);
			star.Scale = Vector2.One * pop;
		});
		Glow.Flash(layer, at, new Color(0.9f, 0.04f, 0.08f), 200f * k, 0.18f);
		Blade.Ring(layer, at, 16f * k, 150f * k, 9f, 0.26f);
		Ink.Needles(layer, at, 8, 190f * k, 9f, 0.15f);
		Glow.Sparks(layer, at, 16, 300f, 900f, 0.32f, new[] { Pal5.Hot, Pal5.Bright, Pal5.Deep }, 400f, 0.07f, 0.22f, dir, 40f);
		Fx.Shake(1);
	}

	/// <summary>
	/// 刀扇（小刀打全体）：一发子弹先打最近的敌人，命中后直线弹起、在空中折点"叮"地硬折一下，再直线打到下一个、再下一个……
	/// （用户：要直勾勾的弹，不要弯的）
	/// 每段 0.07 秒，快但看得出在弹；每个弹点一个小闪光 + 火花 + 星光，最后一个命中稍大。hits 按距离 Joker 由近到远排好。
	/// </summary>
	/// <summary>弹射每一跳飞多久（用户 2026-10-29：0.07s 太快看不清）。</summary>
	public const float RicochetSeg = 0.2f;

	/// <summary>第 i 个被弹到的敌人在开火后多久被打中（伤害数字等到这一刻再跳）。</summary>
	public static float RicochetArrival(int i) => i == 0 ? 0f : 0.04f + RicochetSeg * i;

	public static void Ricochet(Control layer, Vector2 muzzle, IReadOnlyList<Vector2> hits, float facing = 1f)
	{
		if (hits.Count == 0)
		{
			return;
		}
		Shot(layer, muzzle, hits[0], facing);
		const float seg = RicochetSeg;
		for (int i = 1; i < hits.Count; i++)
		{
			Vector2 a = hits[i - 1], b = hits[i];
			// 空中的折点：两个敌人之间偏上（高低交替，免得几段叠在一起），弹道是两段直线
			Vector2 ctrl = (a + b) / 2f + new Vector2(Fx.Rand(-20f, 20f), -110f - 40f * (i % 2));
			float delay = 0.04f + seg * (i - 1);
			bool last = i == hits.Count - 1;
			Fx.After(layer, delay, () =>
			{
				// 弹点："叮"一下的星光和往弹出方向喷的火花
				Glow.Glint(layer, a + new Vector2(0f, -10f), 0.7f);
				Glow.Sparks(layer, a, 8, 200f, 520f, 0.25f, new[] { Pal5.Hot, Pal5.Gold, Pal5.Bright }, 300f, 0.08f, 0.2f, (ctrl - a).Normalized(), 30f);
				KinkTracer(layer, a, ctrl, b, seg);
			});
			Fx.After(layer, delay + seg * 0.45f, () =>
			{
				// 折点：硬折的那一下
				Glow.Glint(layer, ctrl, 0.55f);
				Glow.Flash(layer, ctrl, new Color(1f, 0.7f, 0.6f), 90f, 0.1f);
				Glow.Sparks(layer, ctrl, 6, 200f, 480f, 0.2f, new[] { Pal5.Hot, Pal5.Gold }, 300f, 0.06f, 0.16f, (ctrl - a).Normalized(), 35f);
			});
			Fx.After(layer, delay + seg, () =>
			{
				GunImpact(layer, b, (b - ctrl).Normalized(), last ? 1.1f : 0.85f);   // 弹到的每个敌人：和开枪命中同一套（不顿帧）
				Glow.Sparks(layer, b, last ? 18 : 12, 200f, 520f, 0.35f, new[] { Pal5.Hot, Pal5.Bright, Pal5.Deep }, 220f, 0.1f, 0.3f,
					(b - ctrl).Normalized(), 70f);
				if (last)
				{
					Glow.Ripple(layer, b, 280f, 0.36f, 0.025f);
					Fx.Shake(1);
				}
			});
		}
	}

	/// <summary>折线弹道：头沿 a → k → b 两段直线飞快走完（seg 秒），尾巴随后收掉；折点是硬角。</summary>
	private static void KinkTracer(Control layer, Vector2 a, Vector2 k, Vector2 b, float seg)
	{
		Node2D tr = Fx.Root(layer, Vector2.Zero, "P5Ricochet", seg + 0.35);
		tr.GlobalPosition = Vector2.Zero;
		Polygon2D glowA = Glow.TaperPoly(tr, new Color(Pal5.Bright, 0.85f)), coreA = Glow.TaperPoly(tr, Pal5.Hot);
		Polygon2D glowB = Glow.TaperPoly(tr, new Color(Pal5.Bright, 0.85f)), coreB = Glow.TaperPoly(tr, Pal5.Hot);
		float la = a.DistanceTo(k), total = la + k.DistanceTo(b);
		Vector2 At(float u)
		{
			float d = u * total;
			return d <= la ? a.Lerp(k, d / la) : k.Lerp(b, (d - la) / (total - la));
		}
		// 子弹头：一颗白热的亮点（带红晕和星光），拖尾在后面慢慢收
		Node2D bullet = new();
		tr.AddChild(bullet);
		Glow.SoftSprite(bullet, Vector2.Zero, Pal5.Bright, 1.4f);
		Glow.SoftSprite(bullet, Vector2.Zero, Pal5.Hot, 0.45f);
		Polygon2D star = Fx.Poly(bullet, Glow.Star4(14f, 0.18f), Pal5.Hot, additive: true);
		float life = seg + 0.25f;
		Fx.Animate(tr, 0, life, t =>
		{
			float s = t * life;
			float head = Mathf.Min(1f, s / seg), tail = Fx.InCubic(Mathf.Clamp((s - seg * 0.45f) / (life - seg * 0.45f), 0f, 1f));
			bullet.Visible = s < seg;
			bullet.Position = At(head);
			star.Rotation = s * 18f;
			float w = 1f - tail, split = la / total;
			// 两段各画一条直的锥形光：尾巴那段在后、头那段在前，折角处是尖角
			bool aOn = tail < split && head > tail, bOn = head > split;
			glowA.Visible = coreA.Visible = aOn;
			glowB.Visible = coreB.Visible = bOn;
			if (aOn)
			{
				Vector2 p0 = At(tail), p1 = At(Mathf.Min(head, split));
				glowA.Polygon = Glow.TaperPts(p0, p1, 10f * w + 2f);
				coreA.Polygon = Glow.TaperPts(p0, p1, 3.5f * w + 1f);
			}
			if (bOn)
			{
				Vector2 p0 = At(Mathf.Max(tail, split)), p1 = At(head);
				glowB.Polygon = Glow.TaperPts(p0, p1, 10f * w + 2f);
				coreB.Polygon = Glow.TaperPts(p0, p1, 3.5f * w + 1f);
			}
		});
	}
}
