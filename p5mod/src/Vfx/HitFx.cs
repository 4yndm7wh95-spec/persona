using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 打击 / 开枪的代码特效（用户 2026-10-28：加一些特效，掩盖帧动画的不足）。都很短、很干脆，不挡敌人：
///  · Swing —— 挥刀那一下沿刀尖的真实轨迹扫出一道白色刀光弧（红边），补上准备帧和挥出帧之间缺的动作；
///  · Slash —— 命中：敌人身上一道细长白色斜斩痕 + 红色错位重影 + 小白闪 + 几片碎片；
///  · Muzzle —— 开火：枪口白色十字枪焰 + 红环 + 一小股烟；
///  · Shot —— 一道细白弹道线打到敌人，命中处小白闪 + 扩散红环 + 碎片。
/// 只用全局坐标，游戏里由 JokerFx 按姿势图换算好位置传进来，预览工具直接传。
/// </summary>
public static class HitFx
{
	private static float Wrap(float a) => Mathf.PosMod(a + Mathf.Pi, Mathf.Tau) - Mathf.Pi;

	/// <summary>刀光：以 center 为圆心，从刀尖起点 from 扫到终点 to（走较短的那一侧），0.2 秒。</summary>
	public static void Swing(Control layer, Vector2 center, Vector2 from, Vector2 to)
	{
		float r = (from.DistanceTo(center) + to.DistanceTo(center)) / 2f;
		float a0 = (from - center).Angle(), sweep = Wrap((to - center).Angle() - a0);
		Node2D root = Fx.Root(layer, center, "P5Swing", 0.25);
		// 发光的刀光（加法）：红晕在外、白芯在里
		Polygon2D red = Fx.Poly(root, new Vector2[3], new Color(Pal5.Bright, 0.9f), additive: true);
		Polygon2D white = Fx.Poly(root, new Vector2[3], Pal5.Hot, additive: true);
		const float total = 0.2f;
		Fx.Animate(root, 0, total, t =>
		{
			// 刀光头部飞快扫到终点（前 20%），尾巴随后追上收掉
			float head = Fx.OutCubic(Mathf.Clamp(t / 0.2f, 0f, 1f));
			float tail = Fx.InCubic(Mathf.Clamp((t - 0.1f) / 0.9f, 0f, 1f));
			red.Polygon = Band(r + 6f, a0, sweep, tail, head, 26f);
			white.Polygon = Band(r, a0, sweep, tail, head, 9f);
			root.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp((t - 0.7f) / 0.3f, 0f, 1f));
		});
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
	/// 打击命中（2026-10-29 用发光积木重做，Glow.cs）：
	/// 爆发 —— 径向白闪 + 屏幕折射波，一道沿上撩方向的长光刃划过，60ms 后第二道短光刃交叉；
	/// 余韵 —— 扁的红色光环往外扩、几十颗红色火花往刀的去向喷、几片带棱光的碎片、两颗星光；轻震屏。
	/// </summary>
	public static void Slash(Control layer, Vector2 at, float facing = 1f)
	{
		float up = -0.55f;                                           // 上撩：从左下到右上
		float a1 = facing > 0f ? up : Mathf.Pi - up;
		Glow.Flash(layer, at, new Color(1f, 0.55f, 0.5f), 260f, 0.2f);
		Glow.Ripple(layer, at, 440f, 0.5f, 0.03f);
		Glow.Blade(layer, at, a1, 300f, 34f, 0.34f);
		Glow.Blade(layer, at + new Vector2(10f * facing, 6f), a1 + 1.35f * facing, 190f, 22f, 0.3f, 0.06f);
		Glow.Shockwave(layer, at, new Color(0.9f, 0.05f, 0.08f), 300f, 0.42f, 0.5f, 0.03f);
		Glow.Sparks(layer, at, 34, 260f, 720f, 0.5f, new[] { Pal5.Hot, Pal5.Bright, Pal5.Deep }, 260f, 0.12f, 0.4f,
			new Vector2(facing, -0.6f).Normalized(), 70f);
		Glow.Shards(layer, at, 7, 0.9f, new Vector2(facing, -0.3f).Normalized(), 1.0f);
		Glow.Glint(layer, at + new Vector2(70f * facing, -60f), 0.9f, 0.06f);
		Glow.Glint(layer, at + new Vector2(-40f * facing, 50f), 0.6f, 0.12f);
		Fx.Shake(2);
	}

	/// <summary>
	/// 开火（发光积木）：枪口一团白热闪光 + 红晕；向前一束锥形的短光刺（主刺 + 两根斜的细刺）；
	/// 一小圈光环；火花往前喷。muzzle = 枪口，facing = 朝向。
	/// </summary>
	public static void Muzzle(Control layer, Vector2 muzzle, float facing = 1f)
	{
		Glow.Flash(layer, muzzle, new Color(1f, 0.75f, 0.6f), 150f, 0.12f);
		Glow.Flash(layer, muzzle + new Vector2(30f * facing, 0f), new Color(1f, 0.1f, 0.12f), 220f, 0.16f);
		Node2D root = Fx.Root(layer, muzzle, "P5Muzzle", 0.2);
		root.Scale = new Vector2(facing, 1f);
		var spikes = new (float Ang, float Len, float W)[] { (0f, 120f, 26f), (-0.42f, 60f, 12f), (0.42f, 60f, 12f), (Mathf.Pi, 26f, 10f) };
		var polys = new System.Collections.Generic.List<(Polygon2D G, Polygon2D C, float Ang, float Len, float W)>();
		foreach (var (ang, len, w) in spikes)
		{
			polys.Add((Glow.TaperPoly(root, new Color(Pal5.Bright, 0.95f)), Glow.TaperPoly(root, Pal5.Hot), ang, len, w));
		}
		Fx.Animate(root, 0, 0.1, t =>
		{
			float k = Ease5.OutExpo(Mathf.Min(1f, t * 4f)), fade = 1f - Fx.InCubic(t);
			foreach (var (g, c, ang, len, w) in polys)
			{
				Vector2 d = Vector2.Right.Rotated(ang);
				g.Polygon = Glow.TaperPts(d * 4f, d * len * k, w * fade);
				c.Polygon = Glow.TaperPts(d * 4f, d * len * 0.8f * k, w * 0.35f * fade);
			}
		});
		Glow.Shockwave(layer, muzzle + new Vector2(22f * facing, 0f), new Color(1f, 0.1f, 0.14f), 100f, 0.14f, 1.25f);
		Glow.Sparks(layer, muzzle, 14, 300f, 700f, 0.3f, new[] { Pal5.Hot, Pal5.Gold, Pal5.Bright }, 0f, 0.08f, 0.22f,
			new Vector2(facing, 0f), 22f);
		Fx.Shake(1);
	}

	/// <summary>
	/// 弹道 + 命中：一道极细的白热光从枪口射到 hit（尾巴马上追上），命中处白闪 + 小折射波 + 红色光环 + 一圈短速度线 + 火花 + 星光。
	/// </summary>
	public static void Shot(Control layer, Vector2 muzzle, Vector2 hit, float facing = 1f)
	{
		Node2D tr = Fx.Root(layer, Vector2.Zero, "P5Tracer", 0.15);
		tr.GlobalPosition = Vector2.Zero;
		Polygon2D glow = Glow.TaperPoly(tr, new Color(Pal5.Bright, 0.8f)), core = Glow.TaperPoly(tr, Pal5.Hot);
		Fx.Animate(tr, 0, 0.1, t =>
		{
			Vector2 a = muzzle.Lerp(hit, Fx.InCubic(t)), b = muzzle.Lerp(hit, Ease5.OutExpo(Mathf.Min(1f, t * 3f)));
			glow.Polygon = Glow.TaperPts(a, b, 10f * (1f - t) + 2f);
			core.Polygon = Glow.TaperPts(a, b, 3f * (1f - t) + 1f);
		});
		Glow.Flash(layer, hit, new Color(1f, 0.6f, 0.55f), 200f, 0.16f, 0.04f);
		Glow.Ripple(layer, hit, 300f, 0.4f, 0.025f, 1f, 0.04f);
		Glow.Shockwave(layer, hit, new Color(0.95f, 0.06f, 0.1f), 240f, 0.36f, 1f, 0.04f);
		Glow.SpeedLines(layer, hit, Pal5.Bright, 10, 30f, 150f, 0.24f, 0.04f);
		Glow.Sparks(layer, hit, 22, 200f, 560f, 0.42f, new[] { Pal5.Hot, Pal5.Bright, Pal5.Deep }, 220f, 0.1f, 0.32f,
			new Vector2(facing, -0.3f).Normalized(), 75f);
		Glow.Glint(layer, hit + new Vector2(40f * facing, -40f), 0.7f, 0.08f);
		Fx.Shake(1);
	}
}
