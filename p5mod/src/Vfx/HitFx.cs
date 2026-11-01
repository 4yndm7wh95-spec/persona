using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 打击 / 开枪的代码特效。2026-11-01 用户：之前的太朴素 → 全部换成 P5 的平面漫画语言（Ink.cs：硬边、黑描边、红白黑、爆点、网点、碎片），
/// 都很短、很干脆：
///  · Swing —— 挥刀那一下沿刀尖的真实轨迹扫出一道黑描边的白色月牙（内侧红线、两道速度弧）；
///  · Slash —— 命中：两道交叉斩线 + 锯齿爆点 + 网点环 + 速度线 + 平面碎片 + 原版星光；
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
		// 2026-11-01：刀光改成一笔墨笔（Brush.Slash），沿弦挥过去、往外拱（拱的方向背离圆心）
		Vector2 mid = (from + to) / 2f;
		float ang = (to - from).Angle();
		bool flip = (mid - center).Dot(Vector2.Up.Rotated(ang)) < 0f;
		Brush.Slash(layer, mid, ang, from.DistanceTo(to) * 1.12f, 30f, 0.24f, 0f, flip, 0.32f);
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
		float up = -0.55f;                                           // 上撩：从左下到右上
		float a1 = facing > 0f ? up : Mathf.Pi - up;
		Impact(layer, at, new Vector2(facing, -0.6f).Normalized(), facing);      // 爆点在下面，斩线画在最上面（斩线是主角）
		// 实时墨笔斩痕：一道长的上撩、60ms 后一道短的交叉（Brush.Slash）
		Brush.Slash(layer, at, a1, 560f, 44f, 0.42f, 0f, facing < 0f);
		Brush.Slash(layer, at + new Vector2(10f * facing, 6f), a1 + 1.35f * facing, 360f, 28f, 0.36f, 0.06f, facing > 0f);
	}

	/// <summary>斩击打到身上的那一下（不含斩线）：爆点 + 网点环 + 速度线 + 碎片 + 星光 + 轻震。dir = 刀的去向。</summary>
	public static void Impact(Control layer, Vector2 at, Vector2 dir, float facing, float delay = 0f)
	{
		Fx.After(layer, delay, () =>
		{
			Brush.Splat(layer, at, dir.Angle(), 230f, 0.5f, 0.01f);
			Brush.Drops(layer, at, dir, 14, 0.7f, 1300f, 9f, 0.45f, 0.01f);
			Ink.Needles(layer, at, 6, 200f, 10f, 0.14f);
			Ink.Halftone(layer, at, 240f, 0.3f, 0.02f, null, 1f, 10f);
			Ink.Streaks(layer, at, 12, 110f, 320f, 0.2f, 0.01f, 4f);
			Ink.Shards(layer, at, 10, 40f, dir, 0.8f, 900f, 0.45f);
			Fx.Shake(1);
		});
	}

	/// <summary>
	/// 开火（P5 平面风）：枪口一个往前拉长的红白黑锯齿焰 + 原版枪焰（firetip）往前喷 + 一圈细黑红环；
	/// 弹壳往后上方弹出去、转着掉；一小团平面灰烟往上飘、从细处蚀掉。muzzle = 枪口，facing = 朝向。
	/// </summary>
	public static void Muzzle(Control layer, Vector2 muzzle, float facing = 1f)
	{
		float ang = facing > 0f ? 0f : Mathf.Pi;
		Ink.Needles(layer, muzzle + new Vector2(14f * facing, 0f), 5, 150f, 11f, 0.1f, 0f, new Vector2(facing, 0f), 0.4f, Ink.Gold);
		Ink.Streaks(layer, muzzle + new Vector2(20f * facing, 0f), 7, 30f, 210f, 0.14f, 0f, 6f, new Vector2(facing, 0f), 0.35f);
		Node2D root = Fx.Root(layer, muzzle, "P5Muzzle", 0.6);
		if (Fx.Tex("firetip") is Texture2D tip)
		{
			Sprite2D jet = Fx.Sprite(root, tip);
			jet.Rotation = facing > 0f ? -Mathf.Pi / 2f : Mathf.Pi / 2f;
			jet.Offset = new Vector2(0f, -100f);
			Fx.Animate(jet, 0, 0.1, t => jet.Scale = new Vector2(1.6f * (1f - t), 1.5f + 0.8f * t));
		}
		// 黑红细环
		Polygon2D ringB = Fx.Poly(root, new Vector2[3], Ink.Black), ringR = Fx.Poly(root, new Vector2[3], Ink.Red);
		ringB.Position = ringR.Position = new Vector2(30f * facing, 0f);
		ringB.Scale = ringR.Scale = new Vector2(0.45f, 1f);
		Fx.Animate(ringB, 0, 0.16, t =>
		{
			float r = 20f + 70f * Ease5.OutExpo(t), w = 9f * (1f - t);
			ringB.Polygon = w < 0.3f ? new Vector2[3] : Ink.Arc(r, w + 5f, 0f, Mathf.Tau, 28);
			ringR.Polygon = w < 0.3f ? new Vector2[3] : Ink.Arc(r, w, 0f, Mathf.Tau, 28);
		});
		// 弹壳
		Node2D shell = new() { Position = new Vector2(-30f * facing, -8f) };
		root.AddChild(shell);
		Fx.Poly(shell, new[] { new Vector2(-10f, -6f), new Vector2(10f, -6f), new Vector2(10f, 6f), new Vector2(-10f, 6f) }, Ink.Black);
		Fx.Poly(shell, new[] { new Vector2(-7f, -3f), new Vector2(7f, -3f), new Vector2(7f, 3f), new Vector2(-7f, 3f) }, Ink.Gold);
		Vector2 v = new(-220f * facing, -420f);
		Fx.Animate(shell, 0, 0.55, t =>
		{
			float s = t * 0.55f;
			shell.Position = new Vector2(-30f * facing, -8f) + v * s + new Vector2(0f, 1500f * s * s);
			shell.Rotation = -18f * facing * s;
			shell.Modulate = new Color(1f, 1f, 1f, 1f - Ease5.Seg(t, 0.8f, 0.2f));
		});
		Fx.Shake(1);
	}

	/// <summary>
	/// 弹道 + 命中（P5 平面风）：一道黑描边的细白弹道射到 hit（尾巴马上追上），命中处原版枪击爆点序列帧 + 小锯齿爆点 + 红网点环
	/// + 速度线 + 碎片 + 星光。
	/// </summary>
	public static void Shot(Control layer, Vector2 muzzle, Vector2 hit, float facing = 1f)
	{
		Node2D tr = Fx.Root(layer, Vector2.Zero, "P5Tracer", 0.15);
		tr.GlobalPosition = Vector2.Zero;
		Polygon2D black = Fx.Poly(tr, new Vector2[3], Ink.Black), core = Fx.Poly(tr, new Vector2[3], Ink.White);
		Fx.Animate(tr, 0, 0.09, t =>
		{
			Vector2 a = muzzle.Lerp(hit, Fx.InCubic(t)), b = muzzle.Lerp(hit, Ease5.OutExpo(Mathf.Min(1f, t * 3f)));
			black.Polygon = Glow.TaperPts(a, b, 12f * (1f - t) + 3f);
			core.Polygon = Glow.TaperPts(a, b, 5f * (1f - t) + 1f);
		});
		Vector2 dir = new Vector2(facing, -0.3f).Normalized();
		Ink.Needles(layer, hit, 6, 170f, 9f, 0.14f, 0.02f);
		Brush.Splat(layer, hit, dir.Angle(), 160f, 0.45f, 0.02f);
		Brush.Drops(layer, hit, dir, 10, 0.6f, 1100f, 7f, 0.4f, 0.02f);
		Ink.GunHit(layer, hit, 230f, 0.02f);
		Ink.Halftone(layer, hit, 220f, 0.32f, 0.04f);
		Ink.Streaks(layer, hit, 12, 60f, 230f, 0.2f, 0.03f, 6f);
		Ink.Shards(layer, hit, 7, 32f, dir, 1.0f, 800f, 0.4f, 0.02f);
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
				Ink.Needles(layer, b, 6, last ? 170f : 130f, 9f, 0.14f);         // 弹到的每个敌人：和开枪命中同一套
				Ink.GunHit(layer, b, last ? 220f : 170f);
				Ink.Halftone(layer, b, last ? 210f : 160f, 0.3f, 0.02f);
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
