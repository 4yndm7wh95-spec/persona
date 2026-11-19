using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 战斗结束的选卡界面「夺宝」（2026-11-19，用户：参考 P3 mod 的 Shuffle Time，要有特色且不拖沓）。
/// 原则照 P3 的「精简」模式：点开就能看、能点 —— 牌一开始就是正面，0.3s 内全部到位，特色放在不挡操作的地方：
/// · 牌后面一道 P5 菜单的斜切色带（暗红底 + 错开的黑色厚边），从左边一下扫进来，之后静静停着、隔一会儿一道暗光扫过；
/// · 左上角黑底斜切标签「TREASURE」带两层红色残影滑进来（只在这次打开的第一次）；
/// · 三张牌从画面右边依次甩进来（P5 菜单的方向），带一点倾斜、到位回正，身后拖一道红色的斜切残影，落位一下红闪；
/// · 鼠标 / 手柄停在哪张牌上，牌后面就弹出一块错开的红色斜切底板（P5 选项被选中的样子），一声很轻的光标音；
/// · 选中：那张牌白闪 + 红光 + 红黑白纸屑迸开（「偷到手」），其余的牌往右一甩、变暗淡出。
/// 原版的卡牌格子是对象池复用的（P3 mod 踩过的坑）：所有补间都绑在选牌界面上，每一帧先确认格子还在这一排里才动它；
/// 0.8s 后兜底把牌摆回原版位置、恢复不透明。只动位置 / 旋转 / 透明度，不碰可点击、焦点这些状态。
/// </summary>
public static class RewardFx
{
	private static readonly Color Red = new(0.82f, 0.04f, 0.1f), Ink = new(0.03f, 0.02f, 0.03f);

	private static bool Still(Control c, Control row) => GodotObject.IsInstanceValid(c) && GodotObject.IsInstanceValid(row) && c.GetParent() == row && !c.IsQueuedForDeletion();

	private static Vector2[] Slant(Vector2 c, float w, float h, float skew) =>
		new[] { c + new Vector2(-w * 0.5f + skew, -h * 0.5f), c + new Vector2(w * 0.5f + skew, -h * 0.5f), c + new Vector2(w * 0.5f - skew, h * 0.5f), c + new Vector2(-w * 0.5f - skew, h * 0.5f) };

	/// <summary>
	/// 打开 / 重抽。cards = 这一排的卡牌格子，finals = 原版给它们的位置（相对这一排），first = 这次打开的第一次（铺背景、标题），
	/// rectOf = 一张牌在屏幕上占的矩形（游戏里是格子的点击框）。
	/// </summary>
	public static void Open(Control screen, Control row, IReadOnlyList<Control> cards, IReadOnlyList<Vector2> finals, bool first, Func<Control, Rect2> rectOf)
	{
		Vector2 view = screen.GetViewportRect().Size;
		float u = view.Y / 1080f;
		int run = (screen.HasMeta("p5_reward_run") ? screen.GetMeta("p5_reward_run").AsInt32() : 0) + 1;
		screen.SetMeta("p5_reward_run", run);
		bool Current() => GodotObject.IsInstanceValid(screen) && screen.GetMeta("p5_reward_run").AsInt32() == run;
		Node2D under = screen.GetNodeOrNull<Node2D>("P5RewardBackdrop") ?? Backdrop(screen, row, view, u, rectOf, cards);
		if (first) Title(screen, view, u);

		Sfx.Take("gx_whoosh", 0.16f);
		Sfx.Take("gx_card_slide", 0.18f, 0.04f);
		Transform2D rowXf = row.GetGlobalTransform();
		for (int i = 0; i < cards.Count; i++)
		{
			Control h = cards[i];
			Vector2 final = finals[i];
			float delay = 0.035f * i;
			Vector2 from = final + new Vector2(view.X * 0.55f / Mathf.Max(0.01f, rowXf.X.Length()), 0f);
			h.Position = from;
			h.Rotation = 0.14f;
			h.Modulate = new Color(1f, 1f, 1f, 0f);
			// 身后拖一道红色的斜切残影（画在牌下面）
			Polygon2D streak = Fx.Poly(under, new[] { Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero }, new Color(Red, 0f));
			const float slide = 0.24f;
			float total = delay + slide + 0.14f;
			Fx.Animate(screen, 0, total, t =>
			{
				if (!Current()) return;
				float s = t * total - delay;
				if (s < 0f || !Still(h, row)) return;
				float k = Ease5.OutExpo(Mathf.Min(1f, s / slide));
				h.Position = from.Lerp(final, k);
				h.Rotation = 0.14f * (1f - Ease5.OutBack(Mathf.Min(1f, s / (slide * 1.2f))));
				h.Modulate = new Color(1f, 1f, 1f, Mathf.Min(1f, s / 0.06f));
				if (GodotObject.IsInstanceValid(streak))
				{
					Rect2 r = rectOf(h);
					float len = (1f - k) * view.X * 0.35f + 4f;
					float a = Mathf.Max(0f, 1f - s / (slide + 0.1f));
					Vector2 c = r.GetCenter();
					float hh = r.Size.Y * 0.7f;
					streak.Polygon = new[] { c + new Vector2(0f, -hh * 0.5f), c + new Vector2(len, -hh * 0.5f + hh * 0.2f), c + new Vector2(len, hh * 0.5f - hh * 0.2f), c + new Vector2(0f, hh * 0.5f) };
					streak.Color = new Color(Red, 0.55f * a);
				}
			});
			Fx.FreeAfter(streak, total + 0.05);
			int ii = i;
			Fx.After(screen, delay + slide * 0.7f, () =>
			{
				if (!Current() || !Still(h, row)) return;
				Rect2 r = rectOf(h);
				Glow.Flash(screen, r.GetCenter(), new Color(0.95f, 0.12f, 0.18f), r.Size.Y * 0.9f, 0.18f);
				Sfx.Take("gx_tock", 0.12f - 0.02f * ii);
			});
		}
		// 兜底：0.8s 后还没摆好的牌（对象池、被打断）直接摆回原版位置
		Fx.After(screen, 0.8, () =>
		{
			if (!Current()) return;
			for (int i = 0; i < cards.Count; i++)
			{
				Control h = cards[i];
				if (!Still(h, row)) continue;
				h.Position = finals[i];
				h.Rotation = 0f;
				h.Modulate = Colors.White;
			}
		});
	}

	/// <summary>牌后面的色带 + 悬停底板（选牌界面的第一个子节点：画在牌下面、原版暗色遮罩上面）。</summary>
	private static Node2D Backdrop(Control screen, Control row, Vector2 view, float u, Func<Control, Rect2> rectOf, IReadOnlyList<Control> firstCards)
	{
		Node2D root = new() { Name = "P5RewardBackdrop" };
		screen.AddChild(root);
		screen.MoveChild(root, 0);
		root.GlobalPosition = Vector2.Zero;
		float cy = row.GlobalPosition.Y, bandH = view.Y * 0.5f, skew = bandH * 0.18f;
		// 整条色带斜一点（P5 菜单的斜切），上下各一道黑边、一道亮红的细条
		Node2D band = new() { Rotation = -0.045f };
		root.AddChild(band);
		Node2D bandPivot = new() { Position = new Vector2(view.X * 0.5f, cy) };
		band.AddChild(bandPivot);
		Fx.Poly(bandPivot, Slant(new Vector2(18f * u, 16f * u), view.X * 1.4f, bandH * 1.06f, skew), new Color(Ink, 0.92f));
		Fx.Poly(bandPivot, Slant(Vector2.Zero, view.X * 1.4f, bandH, skew), new Color(0.4f, 0.02f, 0.06f, 0.9f));
		Fx.Poly(bandPivot, Slant(new Vector2(0f, -bandH * 0.43f), view.X * 1.4f, bandH * 0.05f, 0f), new Color(Ink, 0.9f));
		Fx.Poly(bandPivot, Slant(new Vector2(0f, -bandH * 0.38f), view.X * 1.4f, bandH * 0.018f, 0f), new Color(Red, 0.95f));
		Fx.Poly(bandPivot, Slant(new Vector2(0f, bandH * 0.42f), view.X * 1.4f, bandH * 0.03f, 0f), new Color(Red, 0.9f));
		band.Position = Vector2.Zero;
		band.GlobalPosition = Vector2.Zero;
		Polygon2D shine = Fx.Poly(root, Slant(Vector2.Zero, view.Y * 0.16f, bandH, skew), new Color(1f, 0.3f, 0.32f, 0f), additive: true);
		// 悬停底板：错开的红色斜切块
		Polygon2D hoverInk = Fx.Poly(root, new[] { Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero }, new Color(Ink, 0f));
		Polygon2D hover = Fx.Poly(root, new[] { Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero }, new Color(Red, 0f));
		Vector2 hc = Vector2.Zero, hs = Vector2.Zero;
		float ha = 0f;
		Control? last = null;
		Sfx.Play(Sfx.MenuWhoosh, 0.35f);
		Fx.Animate(screen, 0, 600.0, t =>
		{
			float time = t * 600f;
			if (!GodotObject.IsInstanceValid(root)) return;
			float k = Ease5.OutExpo(Mathf.Min(1f, time / 0.22f));
			bandPivot.Position = new Vector2(view.X * 0.5f - view.X * 1.3f * (1f - k), cy);
			// 一道暗光隔一会儿扫过色带
			float sw = Mathf.PosMod(time - 0.4f, 3.2f) / 0.9f;
			shine.Position = new Vector2(Mathf.Lerp(-view.X * 0.2f, view.X * 1.2f, Mathf.Clamp(sw, 0f, 1f)), cy);
			shine.Color = new Color(1f, 0.3f, 0.32f, sw < 1f ? 0.1f * Mathf.Sin(Mathf.Pi * sw) : 0f);
			// 哪张牌被鼠标停着 / 手柄选着
			Control? hov = null;
			if (GodotObject.IsInstanceValid(row))
			{
				Vector2 mouse = screen.GetGlobalMousePosition();
				foreach (Node n in row.GetChildren())
				{
					if (n is not Control c || !Still(c, row) || !c.Visible) continue;
					if (rectOf(c).HasPoint(mouse) || c.HasFocus()) { hov = c; break; }
				}
			}
			if (hov != last && hov != null) Sfx.Play(Sfx.Cursor, 0.25f);
			last = hov;
			ha = Mathf.MoveToward(ha, hov != null ? 1f : 0f, 0.016f * 9f);
			if (hov != null)
			{
				Rect2 r = rectOf(hov);
				Vector2 want = r.GetCenter() + new Vector2(14f, 12f) * u, wantS = r.Size * 1.06f;
				hc = hs == Vector2.Zero ? want : hc.Lerp(want, 0.45f);
				hs = hs == Vector2.Zero ? wantS * 0.6f : hs.Lerp(wantS, 0.45f);
			}
			if (hs != Vector2.Zero)
			{
				hover.Polygon = Slant(hc, hs.X, hs.Y, hs.Y * 0.06f);
				hoverInk.Polygon = Slant(hc + new Vector2(10f, 10f) * u, hs.X, hs.Y, hs.Y * 0.06f);
				hover.Color = new Color(Red, 0.9f * ha);
				hoverInk.Color = new Color(Ink, 0.85f * ha);
			}
		});
		return root;
	}

	/// <summary>左上角黑底斜切标签「TREASURE」，带两层红色残影从左滑进来（不压暗、不挡点击）。</summary>
	private static void Title(Control screen, Vector2 view, float u)
	{
		screen.GetNodeOrNull("P5RewardTitle")?.QueueFree();
		Node2D root = new() { Name = "P5RewardTitle" };
		screen.AddChild(root);
		root.GlobalPosition = Vector2.Zero;
		Font font = P5Ui.Font(P5Ui.Barlow);
		int size = (int)(86f * u);
		Vector2 at = new(view.X * 0.06f, view.Y * 0.1f);
		var layers = new List<(Node2D N, float Lag, float A)>();
		foreach ((float lag, float a, bool ghost) in new[] { (0.06f, 0.3f, true), (0.03f, 0.55f, true), (0f, 1f, false) })
		{
			Node2D n = new() { Rotation = -0.06f };
			root.AddChild(n);
			Fx.Poly(n, Slant(new Vector2(size * 2.3f, size * 0.62f), size * 4.9f, size * 1.15f, size * 0.18f), ghost ? new Color(Red, 1f) : Ink);
			if (!ghost)
			{
				Fx.Poly(n, Slant(new Vector2(size * 2.15f, size * 1.24f), size * 4.2f, size * 0.12f, size * 0.03f), Red);
				P5Ui.Text(n, "TREASURE", font, size, Colors.White, new Vector2(size * 0.18f, -size * 0.06f), (int)(6f * u), Ink);
			}
			n.Modulate = new Color(1f, 1f, 1f, 0f);
			layers.Add((n, lag, a));
		}
		Fx.Animate(root, 0, 0.5, t =>
		{
			float time = t * 0.5f;
			foreach (var (n, lag, a) in layers)
			{
				float k = Mathf.Clamp((time - lag) / 0.24f, 0f, 1f);
				n.Position = at + new Vector2(-view.X * 0.4f * (1f - Ease5.OutExpo(k)), 0f);
				// 残影跟上之后淡掉，本体留着
				float fade = a < 1f ? Mathf.Clamp(k * 3f, 0f, 1f) * (1f - Ease5.Seg(time, 0.24f, 0.2f)) : Mathf.Clamp(k * 4f, 0f, 1f);
				n.Modulate = new Color(1f, 1f, 1f, a * fade);
			}
		});
		Sfx.Take("gx_whoosh", 0.18f, 0.02f);
	}

	/// <summary>选中一张：那张白闪 + 红光 + 纸屑迸开，其余的往右一甩淡出。</summary>
	public static void Pick(Control screen, Control row, IReadOnlyList<Control> cards, Control picked, Func<Control, Rect2> rectOf)
	{
		if (screen.GetNodeOrNull("P5RewardBackdrop") == null) return;
		float u = screen.GetViewportRect().Size.Y / 1080f;
		int run = screen.HasMeta("p5_reward_run") ? screen.GetMeta("p5_reward_run").AsInt32() : 0;
		Rect2 r = rectOf(picked);
		Vector2 c = r.GetCenter();
		Sfx.Play(Sfx.Select, 0.6f);
		Sfx.Take("gx_card_stack", 0.24f, 0.03f);
		Glow.Flash(screen, c, Phantom.Hot, r.Size.Y * 0.6f, 0.12f);
		Glow.Flash(screen, c, new Color(0.95f, 0.1f, 0.16f), r.Size.Y * 1.6f, 0.35f);
		Phantom.Confetti(screen, c, 26, 760f * u, Vector2.Up, 180f, 0.9f, 9f * u);
		foreach (Control other in cards)
		{
			if (other == picked || !Still(other, row)) continue;
			Vector2 p0 = other.Position;
			float dx = (other.GetGlobalRect().GetCenter().X > c.X ? 1f : -1f) * 120f;
			Fx.Animate(screen, 0, 0.22, t =>
			{
				if (!Still(other, row) || screen.GetMeta("p5_reward_run").AsInt32() != run) return;
				other.Position = p0 + new Vector2(dx * Ease5.OutExpo(t), 0f);
				other.Modulate = new Color(1f - 0.5f * t, 1f - 0.55f * t, 1f - 0.5f * t, 1f - t);
			});
		}
	}
}
