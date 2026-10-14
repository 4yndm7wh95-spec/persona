using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace P3Makoto.Vfx;

/// <summary>
/// 空间杀法（Vorpal Blade）—— 追踪之刃生效后的君王之刃（群体）。节奏逐帧对照 research/p3r_video/NOTES.md 第 2 节：
///   蓄力：Persona 周身光点外飘（由 PersonaFx 负责 Persona 本体）
///   时停（v0.14.1）：伤害结算前 0.1s 冻结战斗时间，以下"反相 → 割裂"在真实时间里进行 1.2s，然后恢复时间、释放、结算
///   反相：全屏瞬间蒙上米白色层，第一道粗黑毛笔斩痕横扫
///   割裂（约 1.1s）：黑色直线一条条刻满全屏（越来越快），黑色碎屑漂浮，一道弧形黑斩痕绕场
///   释放（release）：蓝白强闪 → 画面压成暗蓝，刚才的黑线全部变成发光的青白激光线 + 放射速度线
///   结算（release + 0.1 ~ 0.8s）：伤害数字（游戏自己出）、敌人倒下，线条淡出
/// release 对准伤害结算时刻。
/// </summary>
public static class Vorpal
{
	// ── 伤害闸门（v0.14.5）：君王之刃的伤害结算等空间杀法释放（斩击结束、线条亮起）才放行 ──
	private static System.Threading.Tasks.TaskCompletionSource<bool>? _gate;
	private static ulong _openedAt;

	/// <summary>游戏等待伤害结算时调用：返回在空间杀法释放时完成的 Task（最多等 timeout 秒真实时间）。刚释放过就立即完成。</summary>
	public static System.Threading.Tasks.Task Gate(float timeout)
	{
		if (_openedAt != 0 && Time.GetTicksMsec() - _openedAt < 600)
		{
			_openedAt = 0;
			return System.Threading.Tasks.Task.CompletedTask;
		}
		var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
		_gate = tcs;
		if (Engine.GetMainLoop() is SceneTree tree)
		{
			tree.CreateTimer(timeout, processAlways: true, processInPhysics: false, ignoreTimeScale: true).Timeout += () => tcs.TrySetResult(false);
		}
		else
		{
			tcs.TrySetResult(false);
		}
		return tcs.Task;
	}

	/// <summary>v0.14.16（用户：空间杀法释放时那两声"打击"命中音不锋利，去掉）：从起手到释放后 1s 内静音物理系命中音（Sfx.Element 读这个）。</summary>
	private static ulong _quietUntil;
	public static bool Quiet => Time.GetTicksMsec() < _quietUntil;

	private static void OpenGate()
	{
		if (_gate != null && _gate.TrySetResult(true))
		{
			_gate = null;
			_openedAt = 0;
			return;
		}
		_openedAt = Time.GetTicksMsec();
	}

	/// <summary>从"割裂开始"到"释放"的标准时长；可用时间不够时等比压缩。</summary>
	public const float CutDuration = 1.2f;

	/// <summary>
	/// 播放空间杀法。release：从现在起到伤害结算的秒数；targets：所有敌人的中心点。
	/// </summary>
	public static void Play(Control front, IReadOnlyList<Vector2> targets, float release)
	{
		Vector2 size = front.GetViewportRect().Size;
		// v0.14.1 时停：伤害结算前 0.1s 冻结战斗时间（Engine.TimeScale≈0，敌人 / Persona / 游戏流程全部停住），
		// 斩线在真实时间里刻 1.2s；斩击结束、线条亮起时恢复时间，0.1s 后游戏照常结算伤害。本类里的动画 / 计时都用真实时间（RT）。
		float start = Mathf.Max(release - 0.1f, 0.05f);
		// v0.14.22（用户：两阶段之间要有一道明显的斩击来切换，配原声里那声大斩击；之前的弧形斩太小、比声音早）：
		// 原声 vorpal_blade 在 1.0s 处有一声大斩击（0~0.35 第一刀、0.7~0.97 蓄势、1.0 斩下）。
		// 大弧斩在原声 0.98s 挥出（0.09s 划完），划完即释放（时停结束、画面转暗蓝），刻线节奏按比例压缩
		float audioAt = Mathf.Max(start - 0.2f, 0f);
		float slashAt = audioAt + 0.98f;
		float releaseAt = Mathf.Max(slashAt + 0.11f, start + 0.5f);   // 真实时间：时停结束 = 释放
		float cut = releaseAt - start;
		float k = cut / CutDuration;
		_quietUntil = Time.GetTicksMsec() + (ulong)((releaseAt + 1.0f) * 1000f);
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
		Audio.Sfx.Clip("vorpal_blade", 1.0f, audioAt);
		AfterRT(root, start, "vorpal invert", () =>
		{
			AnimRT(grade, 0.06f, t => grade.Color = new Color(0.97f, 0.93f, 0.86f, 0.5f * t));       // 高调：米白色层 50%
			BrushStroke(lines, focus, size, k);
			Flecks(root, focus, size, cut + 0.5f);
			foreach (var (a, b, w, at) in cuts)
			{
				AfterRT(root, at * k, "vorpal line", () =>
				{
					Polygon2D poly = Fx.Poly(lines, new Vector2[3], new Color(0.08f, 0.07f, 0.06f, 0.92f));
					Vector2 mid = (a + b) * 0.5f;
					AnimRT(poly, 0.06f, t => Fx.Reshape(poly, Fx.StrokePts(new[] { mid.Lerp(a, Ease.OutExpo(t)), mid.Lerp(b, Ease.OutExpo(t)) }, w, taper: true)));
				});
			}
		});

		// 切换阶段的大弧斩（墨黑，比第一刀还粗），释放时变成发光的光刃
		Vector2[] arc = ArcPoints(focus, size);
		AfterRT(root, slashAt, "vorpal arc", () => ArcStroke(lines, arc));

		// 释放：蓝白强闪 → 暗蓝 + 线条亮起
		AfterRT(root, start, "vorpal freeze", () => Freeze(true));
		AfterRT(root, releaseAt - 0.02f, "vorpal release", () =>
		{
			Freeze(false);
			OpenGate();                                  // 斩击结束 → 放行伤害结算（数字、WEAK! 都在这之后出现）
			Flash(root, size);
			grade.Color = new Color(0.02f, 0.07f, 0.24f, 0.4f);                                            // 释放：暗蓝色层 40%
			lines.Visible = false;
			foreach (var (a, b, w, _) in cuts)
			{
				Fx.Stroke(glow, new[] { a, b }, new Color(0.35f, 0.75f, 1f, 0.18f), w * 2.6f + 4f);
				Fx.Stroke(glow, new[] { a, b }, new Color(0.85f, 0.97f, 1f, 0.95f), Mathf.Max(2f, w * 0.6f));
			}
			// 大弧斩亮起：最宽最亮的一道光刃；弧内的白色层被"劈开"——闪白、向外撑开后散去
			Fx.Stroke(glow, arc, new Color(0.35f, 0.75f, 1f, 0.3f), 46f);
			Fx.Stroke(glow, arc, new Color(0.9f, 0.98f, 1f, 1f), 9f);
			OpenUp(root, arc, focus);
			P3Fx.SpeedLines(front, focus, Pal.Ice, 26, 120f, size.X * 0.7f, 0.45f);
			NGame.Instance?.ScreenShake(ShakeStrength.Strong, ShakeDuration.Short);
			AnimRT(glow, 0.75f, t =>
			{
				glow.Modulate = new Color(1f, 1f, 1f, (1f - Ease.InCubic(Mathf.Clamp((t - 0.35f) / 0.65f, 0f, 1f))) * (0.85f + 0.15f * Mathf.Sin(t * 60f)));
				grade.Color = new Color(0.02f, 0.07f, 0.24f, 0.4f * (1f - Ease.InCubic(Mathf.Clamp((t - 0.3f) / 0.7f, 0f, 1f))));
			});
		});
		FreeRT(root, releaseAt + 0.85f);
	}

	// ── 真实时间（不受时停影响）的动画 / 计时 ──
	private static Tween AnimRT(Node owner, double seconds, System.Action<float> step)
	{
		Tween tw = Fx.Animate(owner, seconds, step);
		tw.SetIgnoreTimeScale(true);
		return tw;
	}

	private static void AfterRT(Node anchor, double seconds, string what, System.Action action)
	{
		if (seconds <= 0)
		{
			ModEntry.Safe(what, action);
			return;
		}
		anchor.GetTree().CreateTimer(seconds, processAlways: true, processInPhysics: false, ignoreTimeScale: true).Timeout += () => ModEntry.Safe(what, action);
	}

	private static void FreeRT(Node node, double seconds)
	{
		node.GetTree()?.CreateTimer(seconds, processAlways: true, processInPhysics: false, ignoreTimeScale: true).Connect("timeout", Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(node)) node.QueueFree();
		}));
	}

	private static bool _frozen;

	/// <summary>时停开关。冻结时保险：最多 2.5s 后无论如何恢复。</summary>
	private static void Freeze(bool on)
	{
		if (on == _frozen) return;
		_frozen = on;
		P3Makoto.Core.TimeScale.Freeze(on ? 0.02 : 1.0);
		if (on && Engine.GetMainLoop() is SceneTree tree)
		{
			tree.CreateTimer(2.5, processAlways: true, processInPhysics: false, ignoreTimeScale: true).Timeout += () => { if (_frozen) { _frozen = false; P3Makoto.Core.TimeScale.Freeze(1.0); } };
		}
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
		AnimRT(poly, 0.09f * Mathf.Max(k, 0.6f), t =>
		{
			int m = Mathf.Clamp(Mathf.CeilToInt(t * 8f) + 1, 2, 9);
			Fx.Reshape(poly, Fx.StrokePts(pts[..m], 26f, taper: true));
		});
	}

	/// <summary>大弧斩的轨迹：从敌群左上方切入，经右侧绕到下方、收向左下（椭圆的大半圈，略倾斜）。</summary>
	private static Vector2[] ArcPoints(Vector2 focus, Vector2 size)
	{
		const int m = 40;
		var pts = new Vector2[m + 1];
		float rx = size.X * 0.24f, ry = size.Y * 0.3f, tilt = -0.25f;
		for (int i = 0; i <= m; i++)
		{
			float a = Mathf.Lerp(-2.5f, 2.35f, i / (float)m);
			pts[i] = focus + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry).Rotated(tilt);
		}
		return pts;
	}

	/// <summary>切换阶段的大弧斩（和原声 1.0s 的斩击同步）：粗墨黑毛笔弧 0.09s 划完，刀锋处带一点白，划过处溅出黑色碎屑。</summary>
	private static void ArcStroke(Node2D parent, Vector2[] arc)
	{
		Polygon2D ink = Fx.Poly(parent, new Vector2[3], new Color(0.05f, 0.04f, 0.04f, 0.96f));
		Polygon2D edge = Fx.Poly(parent, new Vector2[3], new Color(1f, 1f, 1f, 0.85f));
		AnimRT(ink, 0.09f, t =>
		{
			int m = Mathf.Clamp(Mathf.CeilToInt(Ease.OutCubic(t) * (arc.Length - 1)) + 1, 2, arc.Length);
			Fx.Reshape(ink, Fx.StrokePts(arc[..m], 34f, taper: true));
			int h = Mathf.Max(0, m - 6);
			Fx.Reshape(edge, Fx.StrokePts(arc[h..m], 5f, taper: true));
			edge.Modulate = new Color(1f, 1f, 1f, 1f - t * 0.5f);
		});
		for (int i = 0; i < 14; i++)
		{
			Vector2 p = arc[Fx.Rng.Next(arc.Length)];
			float delay = 0.09f * Fx.Rand(0f, 1f);
			Polygon2D chip = Fx.Poly(parent, new[] { new Vector2(-6f, 0f), new Vector2(0f, -11f), new Vector2(6f, 2f) }, new Color(0.06f, 0.05f, 0.05f, 0.9f));
			chip.Position = p;
			chip.Visible = false;
			Vector2 v = Fx.RandDir() * Fx.Rand(40f, 120f);
			AnimRT(chip, 0.5f, t =>
			{
				float u = Mathf.Clamp((t * 0.5f - delay) / (0.5f - delay), 0f, 1f);
				chip.Visible = u > 0f;
				chip.Position = p + v * Ease.OutCubic(u);
				chip.Rotation = u * 4f;
			});
		}
	}

	/// <summary>释放瞬间：大弧斩围住的区域闪白、向外撑开后散去（像画面从刀口处被劈开）。</summary>
	private static void OpenUp(Node2D root, Vector2[] arc, Vector2 focus)
	{
		Node2D hole = new() { Position = focus };
		root.AddChild(hole);
		var local = new Vector2[arc.Length];
		for (int i = 0; i < arc.Length; i++)
		{
			local[i] = arc[i] - focus;
		}
		Polygon2D fill = Fx.Poly(hole, local, new Color(0.97f, 0.97f, 1f, 0.8f), additive: true);
		Line2D rim = Fx.Line(hole, local, new Color(0.8f, 0.95f, 1f, 1f), 6f, closed: true);
		AnimRT(hole, 0.28f, t =>
		{
			hole.Scale = Vector2.One * (1f + 0.35f * Ease.OutExpo(t));
			fill.Modulate = new Color(1f, 1f, 1f, 1f - Ease.OutCubic(Mathf.Min(1f, t * 1.6f)));
			rim.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(t));
		});
		FreeRT(hole, 0.3f);
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
			AnimRT(f, life, t =>
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
		AnimRT(r, 0.16f, t => r.Color = new Color(0.85f, 0.93f, 1f, 0.95f * (1f - Ease.OutCubic(t))));
		FreeRT(r, 0.18);
	}
}
