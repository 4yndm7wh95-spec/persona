using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Saves;
using P3Makoto.Vfx;

namespace P3Makoto.Scenes;

/// <summary>
/// v0.14.66（玩家：状态 / 能力说明太多时原版挤成好几列，把画面占满）：结城理的对局里，血条右边加一个「状态」按钮（≡ ×N），
/// 点开是 P3R 风格的滚动列表，再点关闭；悬停角色不再弹出原版那一大片（引用卡牌的预览照常）。
/// · 一列竖排，屏幕上只显示固定高度（约 62% 屏高）；中间的条目最实、最靠外，往上 / 往下越来越透明、略缩小、往回收（弧形排布，同 P3R 道具菜单）；
/// · 鼠标滚轮在列表或按钮上滚动（平滑跟随）；内侧一条细滚动条 + 上下箭头提示还有多少；顶部 "STATUS ×N" 标题；
/// · 每一条仍用原版的说明控件（关键词颜色、图标、[img] 都正确），只把底板换成 P3R 斜切深蓝条（减益是深红），标题换正体粗黑体；
/// · 打开期间每 0.25s 读一次角色当前的说明：没变不重建，变了原地重排、保留滚动位置（不重播入场动画）。
/// </summary>
public static class TipList
{
	private sealed class Row
	{
		internal Control Node = null!;
		internal Control Tip = null!;
		internal Node2D Plate = null!;
		internal Vector2 Built = new(-1f, -1f);
		internal bool Debuff, Right;
		internal float Y, H, W;
		// 选中效果（v0.14.66）：身后白底 + 右上错开的红框、外侧青色指示箭头、扫过的一道高光
		internal Node2D? Sel, Pointer, Shine;
		internal float SelK, ShineT = -1f;
	}

	private sealed class State
	{
		internal Control Root = null!;
		internal Control Owner = null!;
		internal List<Row> Rows = new();
		internal string Sig = "";
		internal float Scroll, Target, ContentH, WindowH, T0, CloseT0 = -1f;
		internal bool Right;
		internal Node2D Track = null!;
		internal Polygon2D Thumb = null!;
		internal Node2D Up = null!, Down = null!;
		internal Node2D Head = null!;
		internal Label Count = null!;
		internal Control Catcher = null!;
		internal NCreature Creature = null!;
		internal float NextRefresh;
		internal Node2D Back = null!;                           // 背景：斜切深蓝条带 + 青边 + 斜纹 + 竖排水印 + 气泡 + 打开时的白色斜光
		internal string BackKey = "";
		internal readonly List<(Sprite2D S, float X, float Y, float V, float Ph)> Bubbles = new();
		internal Vector2 Mouse;
		internal bool MouseIn;
		internal Action? Tick;
	}

	private const string TipScene = "res://scenes/ui/hover_tip.tscn";
	private static readonly Dictionary<ulong, State> Lists = new();
	private static readonly Color Navy = new(0.03f, 0.07f, 0.24f, 0.94f);
	private static readonly Color Wine = new(0.24f, 0.03f, 0.09f, 0.94f);
	private static readonly Color Cyan = new(0.38f, 0.84f, 0.96f);
	private static readonly Color Red = new(0.89f, 0.13f, 0.23f);
	private static readonly Color Ink = new(0.02f, 0.04f, 0.14f);
	private static PackedScene? _scene;

	private static float Now => Time.GetTicksMsec() / 1000f;

	/// <summary>角色当前的文字说明（去重后，只要 HoverTip；卡牌预览不算）。</summary>
	internal static List<HoverTip> TextTips(NCreature creature) =>
		IHoverTip.RemoveDupes(creature.Entity.HoverTips).OfType<HoverTip>().ToList();

	private static string Sig(List<HoverTip> text) =>
		string.Join("\u0002", text.Select(t => (t.IsDebuff ? "-" : "+") + t.Title + "\u0001" + t.Description));

	internal static bool IsOpen(NCreature creature) =>
		Lists.TryGetValue(creature.GetInstanceId(), out State? st) && st.CloseT0 < 0f;

	/// <summary>悬停角色时（替代 NCreature.ShowHoverTips）：文字说明不弹出（看「状态」按钮），只把引用卡牌的预览交给原版。返回 true = 已处理。</summary>
	public static bool Hover(NCreature creature, IEnumerable<IHoverTip> hoverTips)
	{
		if (NCombatRoom.Instance?.Ui.Hand.InCardPlay == true)
		{
			return true;
		}
		List<IHoverTip> tips = IHoverTip.RemoveDupes(hoverTips).ToList();
		foreach (IHoverTip t in tips)
		{
			MarkSeen(t.CanonicalModel);                        // 原版 NHoverTipSet.Init 里顺带做的"已见过"记录
		}
		NHoverTipSet.Remove(creature.Hitbox);
		List<IHoverTip> cards = tips.Where(t => t is not HoverTip).ToList();
		if (cards.Count > 0)
		{
			NHoverTipSet.CreateAndShow(creature.Hitbox, cards, HoverTip.GetHoverTipAlignment(creature, 0.5f));
		}
		StatusButton.Nudge(creature);
		return true;
	}

	public static void Toggle(NCreature creature)
	{
		ulong id = creature.GetInstanceId();
		if (Lists.TryGetValue(id, out State? old) && old.CloseT0 < 0f)
		{
			Close(id);
			return;
		}
		if (old != null)
		{
			Free(id, old);
		}
		_scene ??= ResourceLoader.Load<PackedScene>(TipScene);
		if (_scene == null || NGame.Instance?.HoverTipsContainer is not Node container)
		{
			return;
		}
		List<HoverTip> text = TextTips(creature);
		if (text.Count == 0)
		{
			return;
		}
		NHoverTipSet.Remove(creature.Hitbox);
		State st = new() { Owner = creature.Hitbox, Creature = creature, Sig = Sig(text), Right = HoverTip.GetHoverTipAlignment(creature, 0.5f) != HoverTipAlignment.Left, T0 = Now };
		st.Root = new Control { Name = "P3TipList", MouseFilter = Control.MouseFilterEnum.Ignore };
		container.AddChild(st.Root);
		// 列表区域接滚轮（鼠标在列表上时滚动；点击穿不过去，关闭用按钮）
		st.Back = new Node2D { Name = "Back" };
		st.Root.AddChild(st.Back);
		st.Catcher = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
		st.Root.AddChild(st.Catcher);
		st.Catcher.GuiInput += ev => ModEntry.Safe("tip list wheel", () =>
		{
			if (ev is InputEventMouseMotion)
			{
				st.Mouse = st.Catcher.GetGlobalMousePosition();
				st.MouseIn = true;
			}
			Wheel(creature, ev);
		});
		st.Catcher.MouseExited += () => st.MouseIn = false;
		BuildChrome(st);
		Fill(st, text);
		Lists[id] = st;
		creature.TreeExiting += () => Free(id, st);
		Audio.Sfx.Clip("Sys_Cursor", 0.4f);
		Drive(id, st);
	}

	public static void Close(NCreature creature) => Close(creature.GetInstanceId());

	/// <summary>滚轮（列表上或按钮上）。返回是否用掉了这个事件。</summary>
	internal static bool Wheel(NCreature creature, InputEvent ev)
	{
		if (!Lists.TryGetValue(creature.GetInstanceId(), out State? st) || st.CloseT0 >= 0f || ev is not InputEventMouseButton { Pressed: true } mb || st.ContentH <= st.WindowH + 1f)
		{
			return false;
		}
		float step = Mathf.Clamp(st.WindowH * 0.22f, 70f, 140f);
		if (mb.ButtonIndex == MouseButton.WheelUp) st.Target -= step;
		else if (mb.ButtonIndex == MouseButton.WheelDown) st.Target += step;
		else return false;
		st.Target = Mathf.Clamp(st.Target, 0f, st.ContentH - st.WindowH);
		if (GodotObject.IsInstanceValid(st.Catcher))
		{
			st.Catcher.AcceptEvent();
		}
		return true;
	}

	private static void MarkSeen(AbstractModel? model)
	{
		switch (model)
		{
			case CardModel card: SaveManager.Instance.MarkCardAsSeen(card); break;
			case RelicModel relic: SaveManager.Instance.MarkRelicAsSeen(relic); break;
			case PotionModel potion: SaveManager.Instance.MarkPotionAsSeen(potion); break;
		}
	}

	private static void Close(ulong id)
	{
		if (Lists.TryGetValue(id, out State? st) && st.CloseT0 < 0f)
		{
			st.CloseT0 = Now;                                   // 0.14s 淡出后由 Tick 释放
			if (GodotObject.IsInstanceValid(st.Catcher))
			{
				st.Catcher.MouseFilter = Control.MouseFilterEnum.Ignore;
			}
		}
	}

	private static void Free(ulong id, State st)
	{
		if (Lists.TryGetValue(id, out State? cur) && cur == st)
		{
			Lists.Remove(id);
		}
		if (st.Tick != null && Engine.GetMainLoop() is SceneTree tree)
		{
			tree.ProcessFrame -= st.Tick;
			st.Tick = null;
		}
		if (GodotObject.IsInstanceValid(st.Root))
		{
			st.Root.QueueFree();
		}
	}

	// ── 构建 ──

	private static void BuildChrome(State st)
	{
		// 内侧细滚动条（只在内容超出时显示）+ 上下箭头
		st.Track = new Node2D { Name = "Track", Visible = false };
		st.Root.AddChild(st.Track);
		st.Up = Chevron(st.Root, -1f);
		st.Down = Chevron(st.Root, 1f);
		// 顶部标题：STATUS ×N（Barlow 粗斜体，白字深蓝描边 + 青色数量），下面一道斜切深蓝条 + 青色尖头
		st.Head = new Node2D { Name = "Head" };
		st.Root.AddChild(st.Head);
		Fx.Poly(st.Head, new[] { new Vector2(-4f, 30f), new Vector2(190f, 30f), new Vector2(184f, 36f), new Vector2(-10f, 36f) }, new Color(Navy, 0.9f));
		Fx.Poly(st.Head, new[] { new Vector2(-4f, 30f), new Vector2(8f, 30f), new Vector2(2f, 36f), new Vector2(-10f, 36f) }, Cyan);
		Label title = Typo.Text(st.Head, "STATUS", Typo.Barlow, 34, Colors.White, new Vector2(0f, -10f), outline: 5, outlineColor: Ink);
		st.Count = Typo.Text(st.Head, "", Typo.Barlow, 30, Cyan, new Vector2(title.GetMinimumSize().X + 8f, -6f), outline: 5, outlineColor: Ink);
	}

	private static Node2D Chevron(Control parent, float dir)
	{
		Node2D n = new() { Visible = false };
		parent.AddChild(n);
		for (int i = 0; i < 2; i++)
		{
			float y = i * 7f * dir;
			Fx.Stroke(n, new[] { new Vector2(-9f, y - 4f * dir), new Vector2(0f, y + 4f * dir), new Vector2(9f, y - 4f * dir) }, new Color(Cyan, i == 0 ? 0.95f : 0.5f), 2.6f, additive: false);
		}
		return n;
	}

	private static void Fill(State st, List<HoverTip> text)
	{
		foreach (Row r in st.Rows)
		{
			if (GodotObject.IsInstanceValid(r.Node))
			{
				r.Node.QueueFree();
			}
		}
		st.Rows.Clear();
		foreach (HoverTip tip in text)
		{
			Control node = new() { MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = new Color(1f, 1f, 1f, 0f) };
			st.Root.AddChild(node);
			st.Root.MoveChild(node, st.Root.GetChildCount() - 1);
			Node2D plate = new();
			node.AddChild(plate);
			Control ctl = _scene!.Instantiate<Control>();
			node.AddChild(ctl);
			ctl.MouseFilter = Control.MouseFilterEnum.Ignore;
			if (ctl.GetNodeOrNull<MegaLabel>("%Title") is MegaLabel title)
			{
				if (tip.Title == null)
				{
					title.Visible = false;
				}
				else
				{
					title.SetTextAutoSize(tip.Title);
					// 标题：正体粗黑体（游戏自带的思源黑体 Bold），白字（减益偏粉红），细深色描边
					Typo.Restyle(title, Typo.CjkBold, tip.IsDebuff ? new Color(1f, 0.62f, 0.68f) : Colors.White, Ink, 4);
				}
			}
			if (ctl.GetNodeOrNull<MegaRichTextLabel>("%Description") is MegaRichTextLabel desc)
			{
				desc.Text = tip.Description;
				desc.AutowrapMode = tip.ShouldOverrideTextOverflow ? TextServer.AutowrapMode.Off : TextServer.AutowrapMode.WordSmart;
			}
			if (ctl.GetNodeOrNull<TextureRect>("%Icon") is TextureRect icon)
			{
				icon.Texture = tip.Icon;
			}
			if (ctl.GetNodeOrNull<CanvasItem>("%Bg") is CanvasItem bg)
			{
				bg.SelfModulate = new Color(1f, 1f, 1f, 0f);   // 原版底板藏起来（子节点照常显示）
			}
			ctl.ResetSize();
			st.Rows.Add(new Row { Node = node, Tip = ctl, Plate = plate, Debuff = tip.IsDebuff, Right = st.Right });
		}
		if (st.Count != null && GodotObject.IsInstanceValid(st.Count))
		{
			st.Count.Text = "×" + st.Rows.Count;
		}
	}

	/// <summary>P3R 斜切底板：投影 → 深蓝（减益深红）斜切条 → 顶部细高光 → 左端青（减益红）斜块 → 底部右侧红色细线 → 半透明白描边。</summary>
	private static void Plate(Row r, Vector2 size)
	{
		foreach (Node c in r.Plate.GetChildren())
		{
			c.QueueFree();
		}
		float x0 = -16f, x1 = size.X + 14f, y0 = -6f, y1 = size.Y + 6f, lean = 12f;
		Color body = r.Debuff ? Wine : Navy, edge = r.Debuff ? Red : Cyan;
		Vector2[] Box(float a, float b, float c, float d) => new[] { new Vector2(a + lean, c), new Vector2(b + lean, c), new Vector2(b, d), new Vector2(a, d) };
		Fx.Poly(r.Plate, Box(x0 + 8f, x1 + 8f, y0 + 8f, y1 + 8f), new Color(0f, 0f, 0.03f, 0.45f));
		Fx.Poly(r.Plate, Box(x0, x1, y0, y1), body);
		Fx.Poly(r.Plate, new[] { new Vector2(x0 + lean + 14f, y0 + 3f), new Vector2(x1 + lean - 2f, y0 + 3f), new Vector2(x1 + lean - 2.5f, y0 + 4.5f), new Vector2(x0 + lean + 13.5f, y0 + 4.5f) }, new Color(1f, 1f, 1f, 0.22f));
		float wedge = 9f, k = lean / (y1 - y0);
		Fx.Poly(r.Plate, new[] { new Vector2(x0 + lean, y0), new Vector2(x0 + lean + wedge, y0), new Vector2(x0 + wedge, y1), new Vector2(x0, y1) }, edge);
		float rx = x0 + (x1 - x0) * 0.45f;
		Fx.Poly(r.Plate, new[] { new Vector2(rx + k * 4f, y1 - 4f), new Vector2(x1 + k * 4f, y1 - 4f), new Vector2(x1, y1), new Vector2(rx, y1) }, Red);
		Fx.StrokeClosed(r.Plate, Box(x0, x1, y0, y1), new Color(1f, 1f, 1f, 0.28f), 1.5f, additive: false);
		// 选中层（在底板后面）：白底错开 + 红色细框再错开一点（P3R 菜单的选中行）
		r.Sel?.QueueFree();
		r.Sel = new Node2D { Modulate = new Color(1f, 1f, 1f, 0f) };
		r.Node.AddChild(r.Sel);
		r.Node.MoveChild(r.Sel, 0);
		float sx = r.Right ? 1f : -1f;
		Fx.StrokeClosed(r.Sel, Box(x0 + 11f * sx, x1 + 11f * sx, y0 - 10f, y1 - 10f), Red, 2.2f, additive: false);
		Fx.Poly(r.Sel, Box(x0 + 6f * sx, x1 + 6f * sx, y0 - 5f, y1 - 5f), new Color(0.97f, 0.98f, 1f, 0.95f));
		// 外侧指示箭头（青色三角 + 白芯）
		r.Pointer?.QueueFree();
		float px = r.Right ? x1 + lean + 16f : x0 - 16f;
		r.Pointer = new Node2D { Position = new Vector2(px, (y0 + y1) / 2f), Modulate = new Color(1f, 1f, 1f, 0f), Scale = new Vector2(-sx, 1f) };
		r.Node.AddChild(r.Pointer);
		Fx.Poly(r.Pointer, new[] { new Vector2(-12f, 0f), new Vector2(4f, -10f), new Vector2(4f, 10f) }, Cyan);
		Fx.Poly(r.Pointer, new[] { new Vector2(-6f, 0f), new Vector2(1f, -4.5f), new Vector2(1f, 4.5f) }, Colors.White);
		// 高光：一道斜的白色亮片，选中时从左扫到右（在底板上、文字下）
		r.Shine?.QueueFree();
		r.Shine = new Node2D { Visible = false };
		r.Node.AddChild(r.Shine);
		r.Node.MoveChild(r.Shine, r.Plate.GetIndex() + 1);
		Fx.Poly(r.Shine, new[] { new Vector2(lean + 10f, y0), new Vector2(lean + 34f, y0), new Vector2(24f, y1), new Vector2(0f, y1) }, new Color(1f, 1f, 1f, 0.22f), additive: true);
		Fx.Poly(r.Shine, new[] { new Vector2(lean + 40f, y0), new Vector2(lean + 46f, y0), new Vector2(36f, y1), new Vector2(30f, y1) }, new Color(0.7f, 0.95f, 1f, 0.3f), additive: true);
		r.Built = size;
	}

	// ── 每帧 ──

	private static void Drive(ulong id, State st)
	{
		if (Engine.GetMainLoop() is not SceneTree tree)
		{
			return;
		}
		ulong last = Time.GetTicksUsec();
		st.Tick = () =>
		{
			ulong nowUs = Time.GetTicksUsec();
			float dt = Mathf.Min((nowUs - last) / 1e6f, 0.1f);
			last = nowUs;
			try
			{
				if (!GodotObject.IsInstanceValid(st.Root) || !GodotObject.IsInstanceValid(st.Owner) || !GodotObject.IsInstanceValid(st.Creature))
				{
					Free(id, st);
					return;
				}
				if (st.CloseT0 < 0f && (st.Creature.Entity.IsDead || !Patches.Stage.MakotoCombat()))
				{
					Close(id);
				}
				if (st.CloseT0 < 0f && Now >= st.NextRefresh)
				{
					st.NextRefresh = Now + 0.25f;
					List<HoverTip> text = TextTips(st.Creature);
					string sig = Sig(text);
					if (text.Count == 0)
					{
						Close(id);
					}
					else if (sig != st.Sig)
					{
						st.Sig = sig;
						Fill(st, text);                             // 原地重排，保留滚动位置，不重播入场
						foreach (Row r in st.Rows) r.Node.Modulate = Colors.White;
					}
				}
				Layout(st, dt);
				if (st.CloseT0 >= 0f && Now - st.CloseT0 > 0.15f)
				{
					Free(id, st);
				}
			}
			catch (Exception e)
			{
				ModEntry.Report("tip list", e);
				Free(id, st);
			}
		};
		tree.ProcessFrame += st.Tick;
	}

	private static void Layout(State st, float dt)
	{
		float now = Now;
		Vector2 view = st.Root.GetViewportRect().Size;
		// 量尺寸（说明文字排版可能晚一帧才定）：变了就重画底板
		float y = 0f, w = 0f;
		foreach (Row r in st.Rows)
		{
			Vector2 size = r.Tip.Size;
			if (!r.Built.IsEqualApprox(size) && size.X > 1f)
			{
				Plate(r, size);
			}
			r.Y = y;
			r.H = size.Y;
			r.W = size.X;
			w = Mathf.Max(w, size.X);
			y += size.Y + 18f;
		}
		st.ContentH = Mathf.Max(0f, y - 18f);
		st.WindowH = Mathf.Min(st.ContentH, view.Y * 0.62f);
		bool scrollable = st.ContentH > st.WindowH + 1f;
		st.Target = Mathf.Clamp(st.Target, 0f, Mathf.Max(0f, st.ContentH - st.WindowH));
		st.Scroll = Mathf.Lerp(st.Scroll, st.Target, 1f - Mathf.Exp(-14f * dt));

		Rect2 hit = st.Owner.GetGlobalRect();
		float gap = 34f, bulge = 64f * Mathf.Clamp((st.Rows.Count - 1) / 4f, 0f, 1f);
		float baseX = st.Right ? hit.End.X + gap : hit.Position.X - gap - w;
		float centerY = Mathf.Clamp(hit.GetCenter().Y - 30f, st.WindowH / 2f + 120f, view.Y - st.WindowH / 2f - 40f);
		float top = centerY - st.WindowH / 2f;
		float half = Mathf.Max(st.WindowH / 2f, 1f);
		float closing = st.CloseT0 >= 0f ? Mathf.Clamp((now - st.CloseT0) / 0.14f, 0f, 1f) : 0f;
		float side = st.Right ? 1f : -1f;

		// 选中哪一条：鼠标指着的；没有就（可滚动时）是离窗口中心最近的
		int selIdx = -1;
		float best = float.MaxValue;
		for (int i = 0; i < st.Rows.Count; i++)
		{
			Row r = st.Rows[i];
			if (st.MouseIn && r.Node.Visible && r.Node.GetGlobalRect().Grow(10f).HasPoint(st.Mouse))
			{
				selIdx = i;
				break;
			}
			float dc = Mathf.Abs(r.Y + r.H / 2f - st.Scroll - half);
			if (scrollable && !st.MouseIn && dc < best)
			{
				best = dc;
				selIdx = i;
			}
		}
		// 滚动手感：滚得越快条目越往运动方向微微倾斜，停下回正
		float vel = st.Target - st.Scroll;
		float tilt = Mathf.Clamp(vel * 0.00045f, -0.05f, 0.05f) * side;

		for (int i = 0; i < st.Rows.Count; i++)
		{
			Row r = st.Rows[i];
			r.Node.Size = new Vector2(r.W, r.H);
			bool sel = i == selIdx && closing <= 0f;
			if (sel && r.SelK <= 0.01f)
			{
				r.ShineT = now;                                    // 刚被选中：扫一道高光
				Audio.Sfx.Clip("Sys_Cursor", 0.18f);
			}
			r.SelK = Mathf.MoveToward(r.SelK, sel ? 1f : 0f, dt / 0.12f);
			float sk = Ease.OutCubic(r.SelK);
			if (r.Sel != null) r.Sel.Modulate = new Color(1f, 1f, 1f, sk);
			if (r.Pointer != null)
			{
				r.Pointer.Modulate = new Color(1f, 1f, 1f, sk);
				r.Pointer.Position = new Vector2(r.Pointer.Position.X, r.H / 2f + Mathf.Sin(now * 6f) * 1.5f);
			}
			if (r.Shine != null)
			{
				float u = r.ShineT >= 0f ? (now - r.ShineT) / 0.45f : 2f;
				r.Shine.Visible = u < 1f;
				if (u < 1f)
				{
					r.Shine.Position = new Vector2(-30f + (r.W + 60f) * Ease.InOutCubic(u), 0f);
					r.Shine.Modulate = new Color(1f, 1f, 1f, Mathf.Sin(Mathf.Pi * u));
				}
			}
			float c = r.Y + r.H / 2f - st.Scroll;                // 在窗口里的中心（0 = 窗口顶）
			float d = (c - half) / half;                          // -1 顶 … 0 中 … 1 底
			float ad = Mathf.Abs(d);
			float alpha = scrollable ? 1f - Mathf.SmoothStep(0.62f, 1.08f, ad) : 1f;
			float scale = scrollable ? 1f - 0.08f * Mathf.SmoothStep(0.3f, 1f, ad) : 1f;
			float arc = bulge * (1f - Mathf.Min(ad * ad, 1.6f));   // 弧形：中间最靠外
			float e = Ease.OutCubic(Mathf.Clamp((now - st.T0 - i * 0.035f) / 0.3f, 0f, 1f));
			alpha *= e * (1f - closing);
			float x = baseX + side * (arc - 70f * (1f - e) - 24f * closing + 14f * sk);
			scale *= 1f + 0.03f * sk;
			float rowX = st.Right ? x : x + (w - r.W);              // 左侧时右对齐
			r.Node.Position = new Vector2(rowX, top + c - r.H / 2f);
			r.Node.PivotOffset = new Vector2(st.Right ? 0f : r.W, r.H / 2f);
			r.Node.Scale = Vector2.One * scale;
			r.Node.Rotation = tilt;
			r.Node.Modulate = new Color(1f, 1f, 1f, alpha);
			r.Node.Visible = alpha > 0.01f;
		}

		Backdrop(st, baseX, w, bulge, top, side, now, dt, closing);
		// 接滚轮的区域：整个列表窗口（含弧形外扩）
		st.Catcher.Position = new Vector2(st.Right ? baseX - 30f : baseX - bulge - 30f, top - 70f);
		st.Catcher.Size = new Vector2(w + bulge + 60f, st.WindowH + 80f);
		// 标题
		float he = Ease.OutCubic(Mathf.Clamp((now - st.T0) / 0.3f, 0f, 1f)) * (1f - closing);
		st.Head.Position = new Vector2(st.Right ? baseX + 6f : baseX + w - 190f, top - 58f - (scrollable ? 10f : 0f)) + new Vector2(-side * 40f * (1f - he), 0f);
		st.Head.Modulate = new Color(1f, 1f, 1f, he);

		// 滚动条 + 上下箭头
		st.Track.Visible = scrollable;
		float more = scrollable ? he : 0f;
		if (scrollable)
		{
			float tx = st.Right ? baseX - 16f : baseX + w + 16f;
			if (st.Thumb == null || !GodotObject.IsInstanceValid(st.Thumb))
			{
				foreach (Node n in st.Track.GetChildren()) n.QueueFree();
				Fx.Poly(st.Track, new[] { new Vector2(-1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-1f, 1f) }, new Color(1f, 1f, 1f, 0.18f));
				st.Thumb = Fx.Poly(st.Track, new[] { new Vector2(-2.5f, 0f), new Vector2(2.5f, 0f), new Vector2(2.5f, 1f), new Vector2(-2.5f, 1f) }, Cyan);
			}
			float len = st.WindowH - 40f;
			((Polygon2D)st.Track.GetChild(0)).Scale = new Vector2(1f, len);
			float thumb = Mathf.Max(24f, len * st.WindowH / st.ContentH);
			float u = st.Scroll / Mathf.Max(1f, st.ContentH - st.WindowH);
			st.Thumb.Position = new Vector2(0f, (len - thumb) * Mathf.Clamp(u, 0f, 1f));
			st.Thumb.Scale = new Vector2(1f, thumb);
			st.Track.Position = new Vector2(tx, top + 20f);
			st.Track.Modulate = new Color(1f, 1f, 1f, more);
			st.Up.Position = new Vector2(tx, top + 2f);
			st.Down.Position = new Vector2(tx, top + st.WindowH - 2f);
		}
		float bob = Mathf.Sin(now * 5f) * 2f;
		st.Up.Visible = scrollable;
		st.Down.Visible = scrollable;
		st.Up.Modulate = new Color(1f, 1f, 1f, more * Mathf.SmoothStep(0f, 1f, st.Scroll / 30f));
		st.Down.Modulate = new Color(1f, 1f, 1f, more * Mathf.SmoothStep(0f, 1f, (st.ContentH - st.WindowH - st.Scroll) / 30f));
		st.Up.Position += new Vector2(0f, -bob);
		st.Down.Position += new Vector2(0f, bob);
	}

	/// <summary>
	/// 列表背景（P3R 菜单的水下质感）：斜切深蓝条带（上下渐隐）+ 内侧青色细边 + 几道很淡的斜纹 + 外侧竖排半透明大字 STATUS
	/// + 慢慢上浮、左右摆的小气泡；打开时一道白色斜光从内侧扫到外侧。
	/// </summary>
	private static void Backdrop(State st, float baseX, float w, float bulge, float top, float side, float now, float dt, float closing)
	{
		float pad = 46f, slant = 60f;
		float inner = st.Right ? baseX - pad : baseX + w + pad;
		float outer = st.Right ? baseX + w + bulge + pad : baseX - bulge - pad;
		float T = top - 96f, B = top + st.WindowH + 60f;
		string key = $"{inner:0}|{outer:0}|{T:0}|{B:0}";
		if (key != st.BackKey)
		{
			st.BackKey = key;
			foreach (Node n in st.Back.GetChildren())
			{
				if (n is not Sprite2D)
				{
					n.QueueFree();
				}
			}
			float l = Mathf.Min(inner, outer), r = Mathf.Max(inner, outer), mid1 = T + (B - T) * 0.2f, mid2 = T + (B - T) * 0.8f;
			Vector2 S(float x, float y) => new(x + slant * (B - y) / (B - T), y);   // 整条往上斜
			Polygon2D band = new()
			{
				Polygon = new[] { S(l, T), S(r, T), S(r, mid1), S(r, mid2), S(r, B), S(l, B), S(l, mid2), S(l, mid1) },
				VertexColors = new[] { new Color(Navy, 0f), new Color(Navy, 0f), new Color(Navy, 0.55f), new Color(Navy, 0.55f), new Color(Navy, 0f), new Color(Navy, 0f), new Color(Navy, 0.55f), new Color(Navy, 0.55f) },
			};
			st.Back.AddChild(band);
			Gradient fade = new() { Offsets = new[] { 0f, 0.2f, 0.8f, 1f }, Colors = new[] { new Color(1f, 1f, 1f, 0f), Colors.White, Colors.White, new Color(1f, 1f, 1f, 0f) } };
			st.Back.AddChild(new Line2D { Points = new[] { S(inner, T), S(inner, B) }, Width = 2.5f, DefaultColor = Cyan, Gradient = fade, Antialiased = true });
			st.Back.AddChild(new Line2D { Points = new[] { S(inner + 8f * side, T), S(inner + 8f * side, B) }, Width = 1f, DefaultColor = new Color(1f, 1f, 1f, 0.4f), Gradient = fade, Antialiased = true });
			for (float x = l + 30f; x < r; x += 46f)
			{
				st.Back.AddChild(new Line2D { Points = new[] { S(x, T), S(x - 140f, B) }, Width = 1f, DefaultColor = new Color(0.6f, 0.85f, 1f, 0.07f), Gradient = fade, Antialiased = true });
			}
			Label word = Typo.Text(st.Back, "STATUS", Typo.Barlow, 150, new Color(0.75f, 0.9f, 1f, 0.07f), Vector2.Zero);
			word.Rotation = -Mathf.Pi / 2f;
			word.Position = new Vector2(st.Right ? r - 120f : l - 30f, B - 30f);
			// 打开时的白色斜光
			Node2D wipe = new() { Name = "Wipe" };
			st.Back.AddChild(wipe);
			Fx.Poly(wipe, new[] { S(0f, T), S(60f, T), S(60f, B), S(0f, B) }, new Color(1f, 1f, 1f, 0.5f), additive: true);
			Fx.Poly(wipe, new[] { S(70f, T), S(78f, T), S(78f, B), S(70f, B) }, new Color(0.6f, 0.95f, 1f, 0.6f), additive: true);
			if (st.Bubbles.Count == 0)
			{
				for (int i = 0; i < 12; i++)
				{
					Sprite2D b = new() { Texture = Fx.SoftDot, Material = Fx.Additive };
					st.Back.AddChild(b);
					st.Bubbles.Add((b, Fx.Rand(0f, 1f), Fx.Rand(0f, 1f), Fx.Rand(0.035f, 0.08f), Fx.Rand(0f, Mathf.Tau)));
				}
			}
		}
		float open = Ease.OutCubic(Mathf.Clamp((now - st.T0) / 0.25f, 0f, 1f));
		st.Back.Modulate = new Color(1f, 1f, 1f, open * (1f - closing));
		if (st.Back.GetNodeOrNull<Node2D>("Wipe") is Node2D wp)
		{
			float u = Mathf.Clamp((now - st.T0) / 0.4f, 0f, 1f);
			wp.Visible = u < 1f;
			wp.Position = new Vector2(Mathf.Lerp(inner, outer, Ease.InOutCubic(u)) - (st.Right ? 0f : 60f), 0f);
			wp.Modulate = new Color(1f, 1f, 1f, Mathf.Sin(Mathf.Pi * u));
		}
		// 气泡：在条带里上浮（一圈约 10~25s），左右轻摆，靠近上下边淡出
		float lo = Mathf.Min(inner, outer), hi = Mathf.Max(inner, outer), T2 = top - 60f, B2 = top + st.WindowH + 40f;
		for (int i = 0; i < st.Bubbles.Count; i++)
		{
			(Sprite2D sp, float bx, float by, float v, float ph) = st.Bubbles[i];
			by -= v * dt;
			if (by < 0f)
			{
				by += 1f;
				bx = Fx.Rand(0f, 1f);
			}
			st.Bubbles[i] = (sp, bx, by, v, ph);
			float yy = Mathf.Lerp(T2, B2, by);
			sp.Position = new Vector2(Mathf.Lerp(lo + 20f, hi - 20f, bx) + Mathf.Sin(now * 1.3f + ph) * 8f + 60f * (B2 - yy) / (B2 - T2), yy);
			float size = 0.05f + 0.05f * Mathf.Abs(Mathf.Sin(ph));
			sp.Scale = Vector2.One * size;
			sp.Modulate = new Color(0.6f, 0.9f, 1f, 0.35f * Mathf.Sin(Mathf.Pi * by));
		}
	}
}

/// <summary>
/// v0.14.66 血条右边的「状态」按钮（只在结城理的对局、有文字说明时显示，跟着血条一起出现 / 隐藏）：
/// 深蓝斜切小牌 + 左端青色斜块 + 三道斜线图标 + "×N"（Barlow 粗斜体）。悬停略放大提亮；打开列表时变成白底 + 右上错开的红色细框（P3R 选中行）；
/// 悬停角色时轻轻弹一下提示"说明在这里"。点击开 / 关列表，滚轮在按钮上也能滚列表。
/// </summary>
public static class StatusButton
{
	private sealed class Btn
	{
		internal Control Hit = null!;
		internal Node2D Deco = null!, Normal = null!, Open = null!;
		internal Label Count = null!;
		internal bool Mouse;
		internal float Hover, OpenK, Pulse, NextCount;
		internal int Shown = -1;
	}

	private static readonly Dictionary<ulong, Btn> Buttons = new();
	private static readonly Color Navy = new(0.03f, 0.07f, 0.24f, 0.95f);
	private static readonly Color Ink = new(0.02f, 0.04f, 0.14f);
	private static readonly Color Cyan = new(0.38f, 0.84f, 0.96f);
	private static readonly Color Red = new(0.89f, 0.13f, 0.23f);
	private const float W = 74f, H = 28f, Lean = 8f;

	public static void Attach(NCreature creature)
	{
		ulong id = creature.GetInstanceId();
		if (Buttons.ContainsKey(id) || creature.GetNodeOrNull<Control>("%HealthBar") is not Control display || display.GetNodeOrNull<Control>("%HealthBar") is not Control bar)
		{
			return;
		}
		Btn b = new();
		b.Hit = new Control { Name = "P3StatusButton", Size = new Vector2(W + Lean, H), MouseFilter = Control.MouseFilterEnum.Stop, MouseDefaultCursorShape = Control.CursorShape.PointingHand, Visible = false, PivotOffset = new Vector2((W + Lean) / 2f, H / 2f) };
		display.AddChild(b.Hit);
		b.Deco = new Node2D();
		b.Hit.AddChild(b.Deco);
		Vector2[] Slab(float x0, float x1, float y0, float y1) => new[] { new Vector2(x0 + Lean, y0), new Vector2(x1 + Lean, y0), new Vector2(x1, y1), new Vector2(x0, y1) };
		Fx.Poly(b.Deco, Slab(4f, W + 4f, 4f, H + 4f), new Color(0f, 0f, 0.03f, 0.5f));
		b.Normal = new Node2D();
		b.Deco.AddChild(b.Normal);
		Fx.Poly(b.Normal, Slab(0f, W, 0f, H), Navy);
		Fx.StrokeClosed(b.Normal, Slab(0f, W, 0f, H), new Color(1f, 1f, 1f, 0.35f), 1.4f, additive: false);
		b.Open = new Node2D { Modulate = new Color(1f, 1f, 1f, 0f) };
		b.Deco.AddChild(b.Open);
		Fx.StrokeClosed(b.Open, Slab(5f, W + 5f, -5f, H - 5f), Red, 2f, additive: false);
		Fx.Poly(b.Open, Slab(0f, W, 0f, H), new Color(0.97f, 0.98f, 1f));
		Fx.Poly(b.Deco, new[] { new Vector2(Lean, 0f), new Vector2(Lean + 7f, 0f), new Vector2(7f, H), new Vector2(0f, H) }, Cyan);
		// 图标：三道长短不一的斜线（列表）
		for (int i = 0; i < 3; i++)
		{
			float y = 8f + i * 6f, len = i == 1 ? 12f : 16f;
			Fx.Stroke(b.Deco, new[] { new Vector2(17f + (2 - i) * 1.5f, y), new Vector2(17f + (2 - i) * 1.5f + len, y) }, Colors.White, 2.4f, additive: false).Name = "Icon";
		}
		b.Count = Typo.Text(b.Deco, "×0", Typo.Barlow, 24, Cyan, new Vector2(38f, -1f), outline: 4, outlineColor: Ink);
		b.Hit.MouseEntered += () => b.Mouse = true;
		b.Hit.MouseExited += () => b.Mouse = false;
		b.Hit.GuiInput += ev => ModEntry.Safe("status button", () =>
		{
			if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
			{
				b.Hit.AcceptEvent();
				b.Pulse = 1f;
				TipList.Toggle(creature);
			}
			else if (TipList.Wheel(creature, ev))
			{
				b.Hit.AcceptEvent();
			}
		});
		Buttons[id] = b;
		creature.TreeExiting += () => Buttons.Remove(id);
		if (Engine.GetMainLoop() is not SceneTree tree)
		{
			return;
		}
		ulong last = Time.GetTicksUsec();
		Action? tick = null;
		tick = () =>
		{
			if (!GodotObject.IsInstanceValid(creature) || !GodotObject.IsInstanceValid(b.Hit) || !GodotObject.IsInstanceValid(bar))
			{
				tree.ProcessFrame -= tick;
				return;
			}
			ulong now = Time.GetTicksUsec();
			float dt = Mathf.Min((now - last) / 1e6f, 0.1f);
			last = now;
			ModEntry.Safe("status button tick", () => Tick(creature, b, bar, dt));
		};
		tree.ProcessFrame += tick;
	}

	/// <summary>悬停角色时轻轻弹一下（告诉玩家说明在这里）。</summary>
	public static void Nudge(NCreature creature)
	{
		if (Buttons.TryGetValue(creature.GetInstanceId(), out Btn? b) && b.Pulse < 0.3f)
		{
			b.Pulse = 0.7f;
		}
	}

	private static void Tick(NCreature creature, Btn b, Control bar, float dt)
	{
		float t = Time.GetTicksMsec() / 1000f;
		if (t >= b.NextCount)
		{
			b.NextCount = t + 0.25f;
			int n = creature.Entity.IsDead ? 0 : TipList.TextTips(creature).Count;
			if (n != b.Shown)
			{
				b.Shown = n;
				b.Count.Text = "×" + n;
			}
		}
		bool show = b.Shown > 0 && Core.P3Settings.TipList && Patches.Stage.MakotoCombat() && bar.IsVisibleInTree();
		b.Hit.Visible = show;
		if (!show)
		{
			if (TipList.IsOpen(creature))
			{
				TipList.Close(creature);
			}
			return;
		}
		// 血条右边一点，竖直居中对齐血条
		Vector2 scale = bar.GetGlobalTransform().Scale;
		Vector2 at = bar.GlobalPosition + new Vector2(bar.Size.X * scale.X + 12f, (bar.Size.Y * scale.Y - H) / 2f);
		b.Hit.GlobalPosition = at;
		bool open = TipList.IsOpen(creature);
		b.Hover = Mathf.MoveToward(b.Hover, b.Mouse ? 1f : 0f, dt / 0.12f);
		b.OpenK = Mathf.MoveToward(b.OpenK, open ? 1f : 0f, dt / 0.15f);
		b.Pulse = Mathf.Max(0f, b.Pulse - dt * 2.5f);
		float hv = Ease.OutCubic(b.Hover), ok = Ease.OutCubic(b.OpenK);
		b.Hit.Scale = Vector2.One * (1f + 0.08f * hv + 0.12f * Mathf.Sin(Mathf.Pi * Mathf.Min(1f, b.Pulse)) * b.Pulse);
		b.Open.Modulate = new Color(1f, 1f, 1f, ok);
		float lit = 1f + 0.18f * hv;
		b.Normal.Modulate = new Color(lit, lit, lit, 1f);
		// 打开时字和图标变深蓝（在白底上）
		Color fg = Colors.White.Lerp(Ink, ok);
		foreach (Node c in b.Deco.GetChildren())
		{
			if (c is Polygon2D p && p.Name == "Icon")
			{
				p.Color = fg;
			}
		}
		b.Count.LabelSettings.FontColor = Cyan.Lerp(Ink, ok);
		b.Count.LabelSettings.OutlineColor = Ink.Lerp(new Color(1f, 1f, 1f, 0f), ok);
	}
}
