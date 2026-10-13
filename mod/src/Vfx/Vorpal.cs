using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace P3Makoto.Vfx;

/// <summary>
/// 空间杀法（Vorpal Blade）—— 追踪之刃生效后的君王之刃（群体）。节奏逐帧对照 research/p3r_video/NOTES.md 第 2 节：
///   蓄力：Persona 周身光点外飘（由 PersonaFx 负责 Persona 本体）
///   反相（release - 1.0s）：全屏瞬间变成米白高调色，第一道粗黑毛笔斩痕横扫
///   割裂（约 0.85s）：黑色直线一条条刻满全屏（越来越快），黑色碎屑漂浮，一道弧形黑斩痕绕场
///   释放（release）：蓝白强闪 → 画面压成暗蓝，刚才的黑线全部变成发光的青白激光线 + 放射速度线
///   结算（release + 0.1 ~ 0.8s）：伤害数字（游戏自己出）、敌人倒下，线条淡出
/// release 对准伤害结算时刻。
/// </summary>
public static class Vorpal
{
	/// <summary>从"割裂开始"到"释放"的标准时长；可用时间不够时等比压缩。</summary>
	public const float CutDuration = 1.0f;

	/// <summary>
	/// 播放空间杀法。release：从现在起到伤害结算的秒数；targets：所有敌人的中心点。
	/// </summary>
	public static void Play(Control front, IReadOnlyList<Vector2> targets, float release)
	{
		Vector2 size = front.GetViewportRect().Size;
		float cut = Mathf.Min(CutDuration, Mathf.Max(release - 0.1f, 0.35f));
		float start = Mathf.Max(release - cut, 0f);
		float k = cut / CutDuration;                     // 时间压缩系数
		Vector2 focus = Center(targets, size);

		Node2D root = Fx.Root(front, Vector2.Zero, "P3Vorpal");
		root.TopLevel = true;
		root.GlobalPosition = Vector2.Zero;
		// v0.14.1：不再读屏幕调色（游戏里读不到画面，会变成一整块不透明灰蓝，挡住全部视野）→ 改用半透明色层，人物和敌人始终看得见
		ColorRect grade = new() { Size = size, MouseFilter = Control.MouseFilterEnum.Ignore, Color = new Color(0.97f, 0.93f, 0.86f, 0f) };
		root.AddChild(grade);
		Node2D lines = new();
		root.AddChild(lines);
		Node2D glow = new();
		root.AddChild(glow);

		// 割裂的线：12 条贯穿全屏的直线，穿过敌人附近的点；粗细不一；出现时间越来越密
		const int n = 12;
		var cuts = new List<(Vector2 A, Vector2 B, float W, float At)>();
		for (int i = 0; i < n; i++)
		{
			Vector2 p = targets.Count > 0 ? targets[i % targets.Count] + new Vector2(Fx.Rand(-150f, 150f), Fx.Rand(-140f, 120f)) : focus + Fx.RandDir() * Fx.Rand(0f, 300f);
			float ang = Fx.Rand(-0.9f, 0.9f) + (i % 3 == 0 ? Mathf.Pi * 0.5f : 0f);
			Vector2 d = Vector2.Right.Rotated(ang) * size.Length();
			float u = (i + 1f) / n;
			cuts.Add((p - d, p + d, Fx.Rand(2.5f, 9f), 0.12f + 0.7f * (1f - (1f - u) * (1f - u))));
		}

		// P3R 原声 skill_0117（空间杀法）：和视频对齐 —— 音频开始 0.2s 后画面反相，1.0s 前后是斩击密集段，1.4s 释放
		Audio.Sfx.Clip("vorpal_blade", 1.0f, Mathf.Max(start - 0.2f, 0f));
		Fx.After(root, start, "vorpal invert", () =>
		{
			Fx.Animate(grade, 0.06f, t => grade.Color = new Color(0.97f, 0.93f, 0.86f, 0.5f * t));       // 高调：米白色层 50%
			BrushStroke(lines, focus, size, k);
			ArcStroke(lines, focus, k);
			Flecks(root, focus, size, cut + 0.5f);
			foreach (var (a, b, w, at) in cuts)
			{
				Fx.After(root, at * k, "vorpal line", () =>
				{
					Polygon2D poly = Fx.Poly(lines, new Vector2[3], new Color(0.08f, 0.07f, 0.06f, 0.92f));
					Vector2 mid = (a + b) * 0.5f;
					Fx.Animate(poly, 0.06f, t => Fx.Reshape(poly, Fx.StrokePts(new[] { mid.Lerp(a, Ease.OutExpo(t)), mid.Lerp(b, Ease.OutExpo(t)) }, w, taper: true)));
				});
			}
		});

		// 释放：蓝白强闪 → 暗蓝 + 线条亮起
		Fx.After(root, release - 0.06f, "vorpal release", () =>
		{
			Flash(root, size);
			grade.Color = new Color(0.02f, 0.07f, 0.24f, 0.4f);                                            // 释放：暗蓝色层 40%
			lines.Visible = false;
			foreach (var (a, b, w, _) in cuts)
			{
				Fx.Stroke(glow, new[] { a, b }, new Color(0.35f, 0.75f, 1f, 0.18f), w * 2.6f + 4f);
				Fx.Stroke(glow, new[] { a, b }, new Color(0.85f, 0.97f, 1f, 0.95f), Mathf.Max(2f, w * 0.6f));
			}
			P3Fx.SpeedLines(front, focus, Pal.Ice, 26, 120f, size.X * 0.7f, 0.45f);
			EvokerShot.HitStop(0.08);
			NGame.Instance?.ScreenShake(ShakeStrength.Strong, ShakeDuration.Short);
			Fx.Animate(glow, 0.75f, t =>
			{
				glow.Modulate = new Color(1f, 1f, 1f, (1f - Ease.InCubic(Mathf.Clamp((t - 0.35f) / 0.65f, 0f, 1f))) * (0.85f + 0.15f * Mathf.Sin(t * 60f)));
				grade.Color = new Color(0.02f, 0.07f, 0.24f, 0.4f * (1f - Ease.InCubic(Mathf.Clamp((t - 0.3f) / 0.7f, 0f, 1f))));
			});
		});
		Fx.FreeAfter(root, release + 0.85f);
	}

	private static Vector2 Center(IReadOnlyList<Vector2> targets, Vector2 size)
	{
		if (targets.Count == 0) return size * new Vector2(0.68f, 0.45f);
		Vector2 c = Vector2.Zero;
		foreach (Vector2 t in targets) c += t;
		return c / targets.Count;
	}

	/// <summary>第一刀：一道横扫全屏的粗黑毛笔斩痕（中间粗、两端尖、略弯），0.08s 划过。</summary>
	private static void BrushStroke(Node2D parent, Vector2 focus, Vector2 size, float k)
	{
		Polygon2D poly = Fx.Poly(parent, new Vector2[3], new Color(0.05f, 0.04f, 0.04f, 0.95f));
		float ang = Fx.Rand(-0.45f, -0.2f);
		Vector2 dir = Vector2.Right.Rotated(ang), nrm = dir.Orthogonal();
		var pts = new Vector2[9];
		for (int i = 0; i < 9; i++)
		{
			float u = i / 8f - 0.5f;
			pts[i] = focus + dir * u * size.X * 1.3f + nrm * (u * u * 260f - 40f);
		}
		Fx.Animate(poly, 0.09f * Mathf.Max(k, 0.6f), t =>
		{
			int m = Mathf.Clamp(Mathf.CeilToInt(t * 8f) + 1, 2, 9);
			Fx.Reshape(poly, Fx.StrokePts(pts[..m], 26f, taper: true));
		});
	}

	/// <summary>一道绕着敌群的弧形黑斩痕（椭圆的一段），稍晚出现。</summary>
	private static void ArcStroke(Node2D parent, Vector2 focus, float k)
	{
		Fx.After(parent, 0.3f * k, "vorpal arc", () =>
		{
			Polygon2D poly = Fx.Poly(parent, new Vector2[3], new Color(0.06f, 0.05f, 0.05f, 0.85f));
			float a0 = Fx.Rand(0f, Mathf.Tau);
			Fx.Animate(poly, 0.12f, t =>
			{
				var pts = new List<Vector2>();
				int m = 4 + (int)(t * 20f);
				for (int i = 0; i <= m; i++)
				{
					float a = a0 + i * 0.12f;
					pts.Add(focus + new Vector2(Mathf.Cos(a) * 340f, Mathf.Sin(a) * 120f));
				}
				Fx.Reshape(poly, Fx.StrokePts(pts.ToArray(), 7f, taper: true));
			});
		});
	}

	/// <summary>漂浮的黑色碎屑（小三角，缓慢旋转、四散）。</summary>
	private static void Flecks(Node2D root, Vector2 focus, Vector2 size, float life)
	{
		for (int i = 0; i < 34; i++)
		{
			Node2D f = new() { Position = focus + new Vector2(Fx.Rand(-size.X * 0.45f, size.X * 0.45f), Fx.Rand(-size.Y * 0.35f, size.Y * 0.35f)) };
			root.AddChild(f);
			float r = Fx.Rand(3f, 9f);
			Fx.Poly(f, new[] { new Vector2(-r, -r * 0.4f), new Vector2(r, 0f), new Vector2(-r * 0.3f, r * 0.6f) }, new Color(0.05f, 0.04f, 0.04f, 0.9f));
			Vector2 v = Fx.RandDir() * Fx.Rand(10f, 50f);
			float spin = Fx.Rand(-4f, 4f);
			Vector2 from = f.Position;
			Fx.Animate(f, life, t =>
			{
				f.Position = from + v * t * life;
				f.Rotation = spin * t * life;
				f.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(t * 8f, 0f, 1f) * (1f - Mathf.Clamp((t - 0.8f) / 0.2f, 0f, 1f)));
			});
		}
	}

	private static void Flash(Node2D root, Vector2 size)
	{
		ColorRect r = new() { Size = size, Color = new Color(0.85f, 0.93f, 1f, 0.95f), MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(r);
		Fx.Animate(r, 0.16f, t => r.Color = new Color(0.85f, 0.93f, 1f, 0.95f * (1f - Ease.OutCubic(t))));
		Fx.FreeAfter(r, 0.18);
	}
}
