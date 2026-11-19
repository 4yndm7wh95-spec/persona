using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 战斗结束的选卡界面「夺宝」（2026-11-19，用户：参考 P3 mod 的 Shuffle Time，要有特色且不拖沓）。
/// 原则照 P3 的「精简」模式：点开就能看、能点 —— 牌一开始就是正面，0.3s 内全部到位，特色放在不挡操作的地方：
/// · 牌后面一条剪报纸带（红底黑边，Codex 画的 art/p5/ui/reward/band.png），从左边一下扫进来；
/// · 左上角预告函剪报拼贴字「TREASURE」一片一片拍上来，下面「TAKE YOUR PICK」滑进来（只在这次打开的第一次）；
/// · 三张牌从画面右边依次甩进来（P5 菜单的方向），带一点倾斜、到位回正，身后拖一道红色的斜切残影，落位一下红闪；
/// · 鼠标 / 手柄停在哪张牌上，牌后面就弹出一块剪出来的红纸板（黑色投影错开垫着，P5 选项被选中的样子），一声很轻的光标音；
/// · 选中：「STOLEN!」印章重重盖在那张牌上，白闪 + 红光 + 红黑白纸屑迸开，其余的牌往两边一甩、变暗淡出。
/// 原版的卡牌格子是对象池复用的（P3 mod 踩过的坑）：所有补间都绑在选牌界面上，每一帧先确认格子还在这一排里才动它；
/// 0.8s 后兜底把牌摆回原版位置、恢复不透明。只动位置 / 旋转 / 透明度，不碰可点击、焦点这些状态。
/// </summary>
public static class RewardFx
{
	private static readonly Color Red = new(0.82f, 0.04f, 0.1f), Ink = new(0.03f, 0.02f, 0.03f);

	/// <summary>选中之后等多久再把结果交给游戏（界面关掉、牌进牌组）：印章压下 0.08s + 抖一下 + 停一眼。</summary>
	public const float PickHold = 0.45f;

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
		// 用户 2026-11-19：TREASURE 放到「选择一张牌」横幅下面（横幅和牌之间居中）
		if (first && cards.Count > 0) Title(screen, view, u, new Vector2(view.X * 0.5f, rectOf(cards[0]).Position.Y - 64f * u));

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

	private static Sprite2D? Spr(Node parent, string name, Vector2 at, float scale, float rot = 0f)
	{
		if (Fx.Tex("reward_" + name) is not Texture2D tex) return null;
		Sprite2D sp = new() { Texture = tex, Position = at, Scale = Vector2.One * scale, Rotation = rot };
		parent.AddChild(sp);
		return sp;
	}

	/// <summary>
	/// 牌后面的色带 + 悬停底板（选牌界面的第一个子节点：画在牌下面、原版暗色遮罩上面）。素材是 Codex 画的剪报纸带 / 剪出来的红纸板（art/p5/ui/reward）。
	/// </summary>
	private static Node2D Backdrop(Control screen, Control row, Vector2 view, float u, Func<Control, Rect2> rectOf, IReadOnlyList<Control> firstCards)
	{
		Node2D root = new() { Name = "P5RewardBackdrop" };
		screen.AddChild(root);
		screen.MoveChild(root, 0);
		root.GlobalPosition = Vector2.Zero;
		float cy = row.GlobalPosition.Y;
		// 色带：纸带（贴图半尺寸 2048 宽，纸带本体约 1780 宽）拉到比屏幕宽一点，斜一点
		Node2D band = new() { Rotation = -0.035f };
		root.AddChild(band);
		Sprite2D? strip = Spr(band, "band", Vector2.Zero, 1f);
		float bandK = strip?.Texture is Texture2D bt ? view.X * 1.25f / (bt.GetWidth() * 0.87f) : 1f;
		if (strip != null) strip.Scale = new Vector2(bandK, bandK * 1.35f);
		// 悬停底板：黑色投影错开垫在红纸板下面
		Node2D plate = new();
		root.AddChild(plate);
		Sprite2D? plateInk = Spr(plate, "hover_plate_shadow", new Vector2(12f, 12f) * u, 1f);
		Sprite2D? plateRed = Spr(plate, "hover_plate", Vector2.Zero, 1f);
		plate.Modulate = new Color(1f, 1f, 1f, 0f);
		Vector2 pc = Vector2.Zero, psz = Vector2.Zero;
		float pa = 0f;
		Control? last = null;
		Sfx.Play(Sfx.MenuWhoosh, 0.35f);
		Fx.Animate(screen, 0, 600.0, t =>
		{
			float time = t * 600f;
			if (!GodotObject.IsInstanceValid(root)) return;
			float k = Ease5.OutExpo(Mathf.Min(1f, time / 0.22f));
			band.Position = new Vector2(view.X * 0.5f - view.X * 1.3f * (1f - k), cy);
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
			if (hov != last && hov != null)
			{
				Sfx.Play(Sfx.Cursor, 0.25f);
				plate.Rotation = Fx.Rand(-0.07f, 0.07f);           // 每换一张，纸板歪的角度换一下（剪报的随手感）
				psz = Vector2.Zero;
			}
			last = hov;
			pa = Mathf.MoveToward(pa, hov != null ? 1f : 0f, 0.16f);
			if (hov != null && plateRed?.Texture is Texture2D pt)
			{
				Rect2 r = rectOf(hov);
				Vector2 want = r.GetCenter() + new Vector2(10f, 8f) * u;
				Vector2 wantS = new(r.Size.X * 1.14f / (pt.GetWidth() * 0.87f), r.Size.Y * 1.1f / (pt.GetHeight() * 0.84f));
				pc = psz == Vector2.Zero ? want : pc.Lerp(want, 0.5f);
				psz = psz == Vector2.Zero ? wantS * 0.75f : psz.Lerp(wantS, 0.45f);    // 弹出来：小一点 → 撑满
			}
			plate.Position = pc;
			plate.Scale = psz;
			plate.Modulate = new Color(1f, 1f, 1f, pa);
			_ = plateInk;
		});
		return root;
	}

	/// <summary>剪报拼贴字「TREASURE」：8 张字母纸片一片一片拍上来（每片从大到正常、带一点旋转），居中在横幅和牌之间。</summary>
	private static void Title(Control screen, Vector2 view, float u, Vector2 center)
	{
		screen.GetNodeOrNull("P5RewardTitle")?.QueueFree();
		Node2D root = new() { Name = "P5RewardTitle" };
		screen.AddChild(root);
		root.GlobalPosition = Vector2.Zero;
		string[] letters = { "T", "R", "E1", "A", "S", "U", "R2", "E2" };
		float k = 0.34f * u, step = 205f * k;                   // 字母贴图 512，纸片约 200 宽
		Vector2 at = center - new Vector2(step * 3.5f, -14f * u);
		for (int i = 0; i < letters.Length; i++)
		{
			float rot = Fx.Rand(-0.12f, 0.12f), dy = Fx.Rand(-14f, 14f) * u;
			Vector2 p = at + new Vector2(step * i, dy - i * 4f * u);
			Sprite2D? sp = Spr(root, "letter_" + letters[i], p, k, rot);
			if (sp == null) continue;
			sp.Modulate = new Color(1f, 1f, 1f, 0f);
			float delay = 0.03f * i;
			Fx.Animate(sp, 0, delay + 0.16, t =>
			{
				float s = t * (delay + 0.16f) - delay;
				if (s < 0f) return;
				float e = Ease5.OutBack(Mathf.Min(1f, s / 0.12f));
				sp.Scale = Vector2.One * k * Mathf.Lerp(1.8f, 1f, e);
				sp.Rotation = rot + 0.5f * (1f - Mathf.Min(1f, e));
				sp.Modulate = new Color(1f, 1f, 1f, Mathf.Min(1f, s / 0.03f));
			});
		}
		Sfx.Take("gx_card_stack", 0.16f, 0.02f);
		Sfx.Take("gx_card_stack", 0.12f, 0.14f);
	}

	/// <summary>选中一张：「STOLEN!」印章重重盖在那张牌上 + 白闪红光、纸屑迸开，其余的牌往两边一甩淡出。</summary>
	public static void Pick(Control screen, Control row, IReadOnlyList<Control> cards, Control picked, Func<Control, Rect2> rectOf)
	{
		if (screen.GetNodeOrNull("P5RewardBackdrop") == null) return;
		float u = screen.GetViewportRect().Size.Y / 1080f;
		int run = screen.HasMeta("p5_reward_run") ? screen.GetMeta("p5_reward_run").AsInt32() : 0;
		Rect2 r = rectOf(picked);
		Vector2 c = r.GetCenter();
		Sfx.Play(Sfx.Select, 0.6f);
		Sfx.Play(Sfx.SubBoom, 0.25f, 0.06f);
		Sfx.Take("gx_card_stack", 0.28f, 0.06f);
		Glow.Flash(screen, c, Phantom.Hot, r.Size.Y * 0.6f, 0.12f);
		Glow.Flash(screen, c, new Color(0.95f, 0.1f, 0.16f), r.Size.Y * 1.6f, 0.35f);
		Phantom.Confetti(screen, c, 26, 760f * u, Vector2.Up, 180f, 0.9f, 9f * u);
		// 印章：画在最上面，从 2 倍大重重压下来（0.08s），压住时一抖
		Node2D top = new() { Name = "P5RewardStamp", ZIndex = 10, TopLevel = true };
		screen.AddChild(top);
		top.GlobalPosition = Vector2.Zero;
		if (Spr(top, "stamp_stolen", c + new Vector2(0f, r.Size.Y * 0.05f), 1f, -0.18f) is { } stamp && stamp.Texture is Texture2D st)
		{
			float sk = r.Size.X * 1.25f / (st.GetWidth() * 0.87f);
			stamp.Modulate = new Color(1f, 1f, 1f, 0f);
			Fx.Animate(stamp, 0, 0.3, t =>
			{
				float s = t * 0.3f;
				float e = Mathf.Min(1f, s / 0.08f);
				float shake = s > 0.08f ? Mathf.Sin((s - 0.08f) * 90f) * Mathf.Exp(-(s - 0.08f) * 25f) * 0.04f : 0f;
				stamp.Scale = Vector2.One * sk * (Mathf.Lerp(2f, 1f, Fx.InCubic(e)) + shake);
				stamp.Modulate = new Color(1f, 1f, 1f, Mathf.Min(1f, s / 0.04f));
			});
		}
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

	// ───────────────── 原版界面元件换皮（Codex 的剪报素材，art/p5/ui/reward：header_* / skip_*） ─────────────────

	private static bool Zh => TranslationServer.GetLocale().StartsWith("zh", StringComparison.OrdinalIgnoreCase);

	private static void HideArt(Node n, Node keep)
	{
		foreach (Node c in n.GetChildren())
		{
			if (c == keep) continue;
			if (c is TextureRect or NinePatchRect or Sprite2D) ((CanvasItem)c).SelfModulate = new Color(1f, 1f, 1f, 0f);
			if (!c.IsAncestorOf(keep) || c != keep) HideArt(c, keep);
		}
	}

	/// <summary>「选择一张牌」横幅：原版米色卷轴的图藏掉（文字节点留着），换成黑底红边的剪报纸条；中文时文字换成拼贴字图。</summary>
	public static void SkinBanner(Control banner, Label label)
	{
		if (banner.HasMeta("p5_skin") || Fx.Tex("reward_header_plate") is not Texture2D plate) return;
		banner.SetMeta("p5_skin", true);
		if (banner is TextureRect tr) tr.SelfModulate = new Color(1f, 1f, 1f, 0f);
		HideArt(banner, label);
		Node2D root = new() { Name = "P5Header", Position = label.Position + label.Size * 0.5f, ShowBehindParent = false };
		Node parent = label.GetParent() ?? banner;
		parent.AddChild(root);
		parent.MoveChild(root, label.GetIndex());
		float k = 680f / (plate.GetWidth() * 0.88f);
		root.AddChild(new Sprite2D { Texture = plate, Scale = Vector2.One * k, Rotation = -0.02f });
		if (Zh && Fx.Tex("reward_header_text_zh") is Texture2D txt)
		{
			label.SelfModulate = new Color(1f, 1f, 1f, 0f);
			root.AddChild(new Sprite2D { Texture = txt, Scale = Vector2.One * k * 0.92f, Rotation = -0.02f, Position = new Vector2(0f, -4f) });
		}
		else
		{
			label.SelfModulate = Colors.White;
		}
	}

	/// <summary>跳过（及其他备选）按钮：底板换成剪报纸板（悬停换红色版），「跳过 / SKIP」换成拼贴字图，其他按钮留原文字。</summary>
	public static void SkinButton(Control button, TextureRect image, Label label)
	{
		if (button.HasMeta("p5_skin") || Fx.Tex("reward_skip_plate") is not Texture2D plate) return;
		button.SetMeta("p5_skin", true);
		image.Texture = plate;
		image.Material = null;                                   // 原版的 HSV 悬停着色器会把红黑纸板染掉
		image.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		image.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		string t = label.Text.Trim();
		bool skip = t is "跳过" or "跳過" || t.Equals("skip", StringComparison.OrdinalIgnoreCase);
		if (skip && Fx.Tex(Zh ? "reward_skip_text_zh" : "reward_skip_text_en") is Texture2D txt)
		{
			label.SelfModulate = new Color(1f, 1f, 1f, 0f);
			TextureRect tx = new()
			{
				Texture = txt, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				MouseFilter = Control.MouseFilterEnum.Ignore, AnchorRight = 1f, AnchorBottom = 1f, Scale = Vector2.One * 0.8f,
			};
			image.AddChild(tx);
			tx.Size = image.Size;
			tx.PivotOffset = image.Size * 0.5f;
		}
		else
		{
			label.SelfModulate = Colors.White;
		}
	}

	public static void SkinButtonHover(TextureRect image, bool hover)
	{
		if (image.GetParent() is not Control b || !b.HasMeta("p5_skin")) return;
		if (Fx.Tex(hover ? "reward_skip_plate_hover" : "reward_skip_plate") is Texture2D tex) image.Texture = tex;
	}
}
