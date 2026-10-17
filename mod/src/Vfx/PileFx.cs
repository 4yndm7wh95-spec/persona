using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens;
using P3Makoto.Core;

namespace P3Makoto.Vfx;

/// <summary>
/// v0.14.49 抽牌堆 / 弃牌堆 / 消耗堆界面 → P3R 游戏内菜单风格：
/// 从牌堆按钮所在的角张开一个圆孔，露出深蓝水下背景（焦散、光柱、气泡）；
/// 右侧白色大圆盘（同心细圆、外圈刻度弧）+ 被圆盘裁掉一截的灰色立体大字 + 结城理举塔罗牌的会动立绘（Spine：呼吸、发梢、举牌的手、
/// 外套下摆、随身听晃动、眨眼、轮廓光、牌面扫光）+ 牌边的光粒子；
/// 左侧按类型分段的网格（抽牌堆：攻击 / 技能 / 能力 / 状态 / 诅咒，段内按费用 → 名字排，不透露抽牌顺序；弃牌堆 / 消耗堆：最新在前），
/// 每段一块 P3R 斜切标题牌，整片网格沿屏幕向右下微斜；卡牌逐张弹入，指到的那张放大、套白框、下方出名牌，其余压暗。
/// 原版的卡牌网格只藏起来不动；卡牌用游戏自己的 NGridCardHolder（悬停的关键词提示是原版的），位置和缩放由这里排。
/// 只在本机玩家是结城理、且设置里「牌堆界面」开着时生效；用手柄 / 键盘方向键操作时保留原版界面（这里没有方向键导航）。
/// </summary>
public static class PileFx
{
	private const string RootName = "P3PileScreen";
	private const string PortraitPath = "res://p3makoto/ui/pile_portrait.png";
	private const string PortraitSpine = "res://p3makoto/pile/pile_skel_data.tres";

	// 布局都按 1920×1080 写，运行时乘 k = 视口高 / 1080；右侧的东西贴右边缘
	private const float Cols = 5;
	private const float CardW = 160f;                 // 网格里一张牌的宽（参考分辨率像素）
	private const float CellW = 178f, CellH = 252f;
	private const float GridX = 190f, GridTop = 262f, GridBottom = 1010f;
	private const float HeaderH = 58f;
	private const float Slant = 0.05f;                // 网格沿屏幕向右下斜：每往下 1px 右移 0.05px
	private const float PorScale = 0.74f;             // 立绘（1024×1536 画布）缩放
	private static readonly Vector2 PorTopLeft = new(1150f, -20f);   // 1920 宽下的左上角

	private static readonly Color Navy = new(0.03f, 0.07f, 0.24f, 0.94f);
	private static readonly Color Ink = new(0.04f, 0.09f, 0.26f);
	private static readonly Color Cyan = new(0.38f, 0.84f, 0.96f);
	private static readonly Color Ice = new(0.6f, 0.88f, 1f);
	private static readonly Color Red = new(0.89f, 0.13f, 0.23f);

	private sealed class Item
	{
		public Control? Slot;
		public NGridCardHolder? Holder;
		public Node2D? Frame;
		public CardModel Card = null!;
		public int Index;
		public float Born = -1f;
		public float Lift;
		public float Dim = 1f;
		public Vector2 At;                     // 网格里的位置（未滚动、未斜切，参考像素）
	}

	private sealed class Section
	{
		public Node2D Head = null!;
		public float Y;
	}

	public static bool Enabled(NCardPileScreen screen)
	{
		if (!P3Settings.PileScreen || !Patches.Stage.MakotoRun)
		{
			return false;
		}
		return !Compat.UsingDirectionalNav() && screen.Pile != null;
	}

	public static void Rollback(NCardPileScreen screen)
	{
		screen.GetNodeOrNull(RootName)?.QueueFree();
		if (screen.GetNodeOrNull<CanvasItem>("CardGrid") is CanvasItem grid)
		{
			grid.Visible = true;
		}
		if (screen.GetNodeOrNull<CanvasItem>("%BottomLabel") is CanvasItem bottom)
		{
			bottom.Visible = true;
		}
	}

	public static void Build(NCardPileScreen screen)
	{
		if (screen.GetNodeOrNull(RootName) != null || screen.GetNodeOrNull<Control>("CardGrid") is not Control grid)
		{
			return;
		}
		Vector2 view = screen.GetViewportRect().Size;
		float k = view.Y / 1080f;
		float rx = view.X - 1920f * k;                    // 右侧元素的水平偏移（宽屏时贴右边缘）
		CardPile pile = screen.Pile;
		Font font = Typo.Barlow;
		float t0 = Time.GetTicksMsec() / 1000f;
		float Now() => Time.GetTicksMsec() / 1000f - t0;

		Control root = new() { Name = RootName, MouseFilter = Control.MouseFilterEnum.Stop };
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		screen.AddChild(root);
		screen.MoveChild(root, grid.GetIndex());             // 在原版网格的位置：返回按钮仍在上面
		grid.Visible = false;
		if (screen.GetNodeOrNull<CanvasItem>("%BottomLabel") is CanvasItem bottom)
		{
			bottom.Visible = false;                          // 原版说明写的是"按稀有度排序"，这里不是
		}

		// ── 背景：圆孔从牌堆按钮那个角张开 ──
		Vector2 from = pile.Type switch
		{
			PileType.Draw => new Vector2(0.04f, 0.95f),
			PileType.Discard => new Vector2(0.96f, 0.95f),
			_ => new Vector2(0.96f, 0.82f),
		};
		ShaderMaterial water = new() { Shader = Shaders.Underwater };
		water.SetShaderParameter("aspect", view.X / view.Y);
		water.SetShaderParameter("center", from);
		water.SetShaderParameter("radius", 0f);
		ColorRect bg = new() { Size = view, Material = water, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(bg);
		float diag = Mathf.Sqrt(view.X * view.X / (view.Y * view.Y) + 1f) + 0.1f;
		Fx.Animate(bg, 0.55, t => water.SetShaderParameter("radius", diag * Ease.OutCubic(t)));
		Bubbles(root, view, k);

		// ── 右侧：白色大圆盘 + 立体大字 + 立绘 ──
		Node2D right = new() { Position = new Vector2(700f * k, 0f), Modulate = new Color(1f, 1f, 1f, 0f) };
		root.AddChild(right);
		Disc(right, pile.Type, font, k, rx);
		Portrait(right, k, rx);
		Fx.After(right, 0.1, "pile right in", () => Fx.Animate(right, 0.5, t =>
		{
			right.Position = new Vector2(700f * k * (1f - Ease.OutExpo(t)), 0f);
			right.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(t * 3f, 0f, 1f));
		}));

		// ── 左上：牌堆名 + 张数 + 一行说明 ──
		Node2D head = new() { Position = new Vector2(-640f * k, 0f) };
		root.AddChild(head);
		string title = pile.Type switch { PileType.Draw => "DRAW PILE", PileType.Discard => "DISCARD PILE", PileType.Exhaust => "EXHAUST PILE", _ => "PILE" };
		// 标题：Barlow 粗斜体，白字深蓝描边，右下错开一层宝蓝投影（P3R 菜单标题的叠影）
		Text(head, title, font, (int)(92f * k), new Color(0.12f, 0.36f, 1f), new Vector2(66f * k, 98f * k), (int)(5f * k), new Color(0.12f, 0.36f, 1f));
		Label titleL = Text(head, title, font, (int)(92f * k), Colors.White, new Vector2(58f * k, 90f * k), (int)(5f * k), Ink);
		Label countL = Text(head, "0", font, (int)(112f * k), Cyan, new Vector2((58f + 26f) * k + titleL.GetMinimumSize().X, 74f * k), (int)(5f * k), Ink);
		Fx.Poly(head, new[] { new Vector2(56f, 200f) * k, new Vector2(560f, 200f) * k, new Vector2(552f, 228f) * k, new Vector2(48f, 228f) * k }, Navy);
		Fx.Poly(head, new[] { new Vector2(56f, 200f) * k, new Vector2(70f, 200f) * k, new Vector2(62f, 228f) * k, new Vector2(48f, 228f) * k }, Cyan);
		string note = pile.Type == PileType.Draw
			? (Zh ? "抽牌顺序随机 · 按类型 → 费用排列" : "DRAW ORDER IS HIDDEN · SORTED BY TYPE → COST")
			: (Zh ? "按进入牌堆的先后排列 · 最新在前" : "MOST RECENT FIRST");
		Text(head, note, Zh ? Typo.CjkBold : font, (int)(19f * k), new Color(0.8f, 0.93f, 1f), new Vector2(84f * k, 202f * k), 0, Ink);
		Fx.After(head, 0.16, "pile head in", () => Fx.Animate(head, 0.45, t => head.Position = new Vector2(-640f * k * (1f - Ease.OutExpo(t)), 0f)));

		// ── 网格 ──
		Control cards = new() { MouseFilter = Control.MouseFilterEnum.Ignore };
		cards.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.AddChild(cards);
		Node2D heads = new();
		root.AddChild(heads);
		root.MoveChild(heads, cards.GetIndex());              // 段标题在卡牌下面（放大的牌可以盖住标题）
		Node2D plate = NamePlate(root, k);                     // 名牌在最上面
		Label empty = Text(root, "EMPTY", font, (int)(90f * k), new Color(1f, 1f, 1f, 0.45f), new Vector2(GridX, GridTop + 60f) * k, 0, Ink);
		empty.Visible = false;

		// ── 点击特写：压暗层 + 白色斜带 + 旋转光芒 + 左侧说明；挡点击的透明层；放特写卡牌的最上层 ──
		Focus fx = FocusLayer(root, view, k, font);
		Control catcher = new() { MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };
		catcher.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.AddChild(catcher);
		Control front = new() { MouseFilter = Control.MouseFilterEnum.Ignore };
		front.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.AddChild(front);
		Item? focused = null;
		float focusP = 0f;
		bool focusOn = false;
		float lastNow = 0f;

		List<Item> items = new();
		List<Section> sections = new();
		List<CardModel> pending = new();
		Item? hovered = null;
		float scroll = 0f, target = 0f, contentH = 0f;
		bool dragging = false;
		float dragFrom = 0f, dragScroll = 0f;
		float nextBorn = 0.22f;

		void Refill()
		{
			foreach (Item it in items)
			{
				// 卡牌 holder 是对象池里的：先还回池子，再删外面的 slot
				if (it.Holder != null && GodotObject.IsInstanceValid(it.Holder))
				{
					it.Holder.QueueFreeSafely();
				}
				if (it.Slot != null && GodotObject.IsInstanceValid(it.Slot))
				{
					it.Slot.QueueFree();
				}
			}
			foreach (Section s in sections)
			{
				if (GodotObject.IsInstanceValid(s.Head))
				{
					s.Head.QueueFree();
				}
			}
			items.Clear();
			sections.Clear();
			pending.Clear();
			hovered = null;
			focused = null;
			focusOn = false;
			focusP = 0f;
			catcher.Visible = false;

			List<CardModel> all = pile.Cards.ToList();
			List<(string En, string Zh, List<CardModel> Cards)> groups = new();
			if (pile.Type == PileType.Draw)
			{
				foreach (IGrouping<int, CardModel> g in all.GroupBy(c => TypeOrder(c.Type)).OrderBy(g => g.Key))
				{
					List<CardModel> list = g.OrderBy(Cost).ThenBy(c => c.Title, StringComparer.Ordinal).ThenBy(c => c.IsUpgraded ? 1 : 0).ToList();
					groups.Add((TypeEn(list[0].Type), TypeZh(list[0].Type), list));
				}
			}
			else if (all.Count > 0)
			{
				all.Reverse();
				groups.Add(("RECENT", Zh ? "最新在前" : "", all));
			}
			float y = 0f;
			int index = 0;
			foreach ((string en, string zh, List<CardModel> list) in groups)
			{
				sections.Add(new Section { Head = SectionHead(heads, en, zh, list.Count, font, k), Y = y });
				y += HeaderH;
				for (int i = 0; i < list.Count; i++)
				{
					pending.Add(list[i]);
					items.Add(new Item { Card = list[i], Index = index++, At = new Vector2((i % (int)Cols) * CellW + CellW / 2f, y + (i / (int)Cols) * CellH + CellH / 2f) });
				}
				y += Mathf.Ceil(list.Count / Cols) * CellH + 16f;
			}
			contentH = y;
			countL.Text = all.Count.ToString();
			target = Mathf.Clamp(target, 0f, MaxScroll());
		}

		float MaxScroll() => Mathf.Max(0f, contentH - (GridBottom - GridTop));

		void Create(Item it)
		{
			NCard? card = NCard.Create(it.Card);
			NGridCardHolder? h = card != null ? NGridCardHolder.Create(card) : null;
			if (h == null)
			{
				card?.QueueFreeSafely();
				return;
			}
			Control slot = new() { MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = new Color(1f, 1f, 1f, 0f) };
			cards.AddChild(slot);
			h.Visible = true;
			h.MouseFilter = Control.MouseFilterEnum.Pass;
			slot.AddChild(h);
			card!.UpdateVisuals(pile.Type, CardPreviewMode.Normal);
			Node2D frame = CardFrame(slot);
			it.Slot = slot;
			it.Holder = h;
			it.Frame = frame;
			it.Born = Mathf.Max(Now(), nextBorn);
			nextBorn = it.Born + 0.022f;
			h.Hitbox.MouseEntered += () =>
			{
				if (hovered != it)
				{
					hovered = it;
					Audio.Sfx.Clip("Sys_Cursor", 0.35f);
				}
			};
			h.Hitbox.MouseExited += () => { if (hovered == it) hovered = null; };
			h.Hitbox.GuiInput += ev => ModEntry.Safe("pile card input", () =>
			{
				if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left or MouseButton.Right })
				{
					h.Hitbox.AcceptEvent();                 // 不传给底层（否则会当成拖动开始）
					Toggle(it);
				}
				else if (focused == null)
				{
					Wheel(ev);
				}
			});
		}

		// 左 / 右键点牌：这张飞到中间特写；再点它或点任意空白处：飞回原位
		void Toggle(Item it)
		{
			if (focused == null)
			{
				if (it.Slot == null)
				{
					return;
				}
				focused = it;
				focusOn = true;
				it.Slot.Reparent(front, true);
				catcher.Visible = true;
				Fill(fx, it.Card, pile, k);
				Audio.Sfx.Clip("ShuffleTime_Select", 0.5f);
			}
			else if (focusOn)
			{
				focusOn = false;
				Audio.Sfx.Clip("ShuffleTime_Whoosh", 0.3f);
			}
		}
		catcher.GuiInput += ev => ModEntry.Safe("pile focus input", () =>
		{
			if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left or MouseButton.Right } && focused != null)
			{
				catcher.AcceptEvent();
				Toggle(focused);
			}
		});

		void Wheel(InputEvent ev)
		{
			if (ev is InputEventMouseButton { Pressed: true } mb)
			{
				if (mb.ButtonIndex == MouseButton.WheelUp) target -= CellH * 0.6f;
				else if (mb.ButtonIndex == MouseButton.WheelDown) target += CellH * 0.6f;
			}
		}

		root.GuiInput += ev => ModEntry.Safe("pile input", () =>
		{
			if (focused != null)
			{
				return;
			}
			Wheel(ev);
			if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left } b)
			{
				dragging = b.Pressed;
				dragFrom = b.Position.Y;
				dragScroll = target;
			}
			else if (dragging && ev is InputEventMouseMotion m)
			{
				target = dragScroll - (m.Position.Y - dragFrom) / k;
			}
		});

		Refill();
		Action onChanged = () => ModEntry.Safe("pile changed", () =>
		{
			nextBorn = Now();
			Refill();
		});
		pile.ContentsChanged += onChanged;

		SceneTree tree = screen.GetTree();
		void Tick()
		{
			if (!GodotObject.IsInstanceValid(root) || !root.IsVisibleInTree())
			{
				return;
			}
			ModEntry.Safe("pile tick", () =>
			{
				float now = Now();
				float dt = Mathf.Clamp(now - lastNow, 0f, 0.1f);
				lastNow = now;
				// 特写进度：进 0.5s、退 0.42s
				focusP = Mathf.MoveToward(focusP, focusOn ? 1f : 0f, dt / (focusOn ? 0.5f : 0.42f));
				if (focused != null && !focusOn && focusP <= 0f)
				{
					if (focused.Slot != null && GodotObject.IsInstanceValid(focused.Slot))
					{
						focused.Slot.Reparent(cards, true);
					}
					focused = null;
					catcher.Visible = false;
				}
				float fe = Ease.InOutCubic(focusP);
				Vector2 focusCenter = FocusCenter * k;
				Animate(fx, fe, focusP, focusOn, now, focusCenter, k);
				// 圆孔张开一小会儿之后开始建卡牌节点（每帧 4 张，避免一帧卡顿）；入场按序号错开
				if (now > 0.12f)
				{
					for (int n = 0; n < 4 && pending.Count > 0; n++)
					{
						CardModel model = pending[0];
						pending.RemoveAt(0);
						if (items.FirstOrDefault(i => i.Card == model && i.Slot == null) is Item it)
						{
							Create(it);
						}
					}
				}
				empty.Visible = items.Count == 0 && now > 0.4f;

				// 滚动：带回弹的平滑跟随
				float max = MaxScroll();
				if (!dragging)
				{
					if (target < 0f) target = Mathf.Lerp(target, 0f, 0.2f);
					if (target > max) target = Mathf.Lerp(target, max, 0.2f);
				}
				target = Mathf.Clamp(target, -120f, max + 120f);
				scroll = Mathf.Lerp(scroll, target, 0.2f);

				Vector2 Screen(Vector2 at)
				{
					float sy = GridTop + at.Y - scroll;
					return new Vector2(GridX + at.X + (sy - GridTop) * Slant, sy) * k;
				}
				float Fade(float top, float bottom) => Mathf.Clamp((top - (GridTop - 22f) * k) / (36f * k), 0f, 1f) * Mathf.Clamp((GridBottom * k - bottom) / (50f * k), 0f, 1f);

				foreach (Section s in sections)
				{
					Vector2 p = Screen(new Vector2(0f, s.Y + HeaderH * 0.5f));
					s.Head.Position = p + new Vector2(-14f * k, 0f);
					s.Head.Modulate = new Color(1f, 1f, 1f, Fade(p.Y - 20f * k, p.Y + 20f * k) * Mathf.Clamp((now - 0.2f) / 0.25f, 0f, 1f) * (1f - fe));
					s.Head.Position += new Vector2(-60f * k * fe, 0f);
				}
				head.Modulate = new Color(1f, 1f, 1f, 1f - 0.85f * fe);
				if (focused != null)
				{
					hovered = null;
				}

				bool any = hovered != null && hovered.Slot != null && GodotObject.IsInstanceValid(hovered.Holder) && hovered.Slot.Visible;
				if (!any)
				{
					hovered = null;
				}
				foreach (Item it in items)
				{
					if (it.Slot == null || !GodotObject.IsInstanceValid(it.Holder))
					{
						continue;
					}
					NGridCardHolder h = it.Holder;
					Vector2 cs = h.Hitbox.Size.X > 1f ? h.Hitbox.Size : new Vector2(300f, 422f);
					float baseScale = CardW * k / cs.X;
					float half = cs.Y * baseScale * 0.5f;
					Vector2 p = Screen(it.At);
					float fade = Fade(p.Y - half, p.Y + half);
					// 入场：从左下方弹入（OutBack），带淡入
					float born = Mathf.Clamp((now - it.Born) / 0.38f, 0f, 1f);
					float pop = Ease.OutBack(born);
					it.Lift = Mathf.MoveToward(it.Lift, it == hovered ? 1f : 0f, 0.12f);
					float lift = Ease.OutCubic(it.Lift);
					it.Dim = Mathf.MoveToward(it.Dim, any && it != hovered ? 0.55f : 1f, 0.07f);
					p += new Vector2(-40f, 46f) * k * (1f - pop) + new Vector2(0f, -14f) * k * lift;
					bool on = fade > 0.01f || it == hovered;
					it.Slot.Visible = on;
					if (!on)
					{
						continue;
					}
					float rot = Mathf.DegToRad(-2f) * (1f - lift);
					// 原版悬停会把 holder 自己的 Scale 从 0.8 跳到 1.0；这里按它当前的 Scale 反算，让总缩放平滑
					float want = baseScale * (0.85f + 0.15f * pop) * (1f + 0.26f * lift);
					float flip = 1f, alpha = Mathf.Clamp(born * 3f, 0f, 1f) * (it == hovered ? 1f : fade);
					float frameA = lift;
					if (it == focused)
					{
						// 特写：沿上拱的弧线飞到中间、放大，途中像翻牌一样横向收窄再展开（正好一次翻面），落定后轻轻浮动
						Vector2 to = focusCenter + new Vector2(0f, Mathf.Sin(now * 1.6f) * 4f * k * fe);
						p = p.Lerp(to, fe) + new Vector2(0f, -170f * k) * Mathf.Sin(Mathf.Pi * fe);
						want = Mathf.Lerp(want, FocusW * k / cs.X, fe);
						flip = Mathf.Max(0.03f, Mathf.Abs(Mathf.Cos(Mathf.Pi * fe)));
						rot = Mathf.Lerp(rot, 0f, fe) + Mathf.DegToRad(-12f) * Mathf.Sin(Mathf.Pi * fe);
						alpha = 1f;
						frameA = Mathf.Max(lift, fe);
						it.Slot.Visible = true;
					}
					else if (focused != null)
					{
						// 其他牌"退下"：离特写那张越远越晚，往下沉、缩小、转一点、淡出
						float dist = (it.At - focused.At).Length();
						float r = Ease.OutCubic(Mathf.Clamp(focusP * 1.6f - Mathf.Min(0.5f, dist / 1600f), 0f, 1f));
						p += new Vector2(-24f, 80f) * k * r;
						want *= 1f - 0.16f * r;
						rot += Mathf.DegToRad(-5f) * r;
						alpha *= 1f - 0.9f * r;
					}
					it.Slot.Position = p;
					it.Slot.Rotation = rot;
					float own = Mathf.Max(0.01f, h.Scale.X);
					it.Slot.Scale = new Vector2(want / own * flip, want / own);
					it.Frame.Scale = Vector2.One * own;
					it.Frame.Modulate = new Color(1f, 1f, 1f, frameA);
					float d = it == focused ? 1f : it.Dim;
					it.Slot.Modulate = new Color(d * 0.9f + 0.1f, d * 0.94f + 0.06f, Mathf.Min(1f, d + 0.1f), alpha);
				}
				// 指到的那张移到最上层（绘制和鼠标判定都按节点顺序）
				if (any && hovered!.Slot!.GetParent() == cards && hovered.Slot.GetIndex() != cards.GetChildCount() - 1)
				{
					cards.MoveChild(hovered.Slot, cards.GetChildCount() - 1);
				}
				// 名牌：放大那张牌的正下方
				plate.Visible = any;
				if (any)
				{
					Item it = hovered!;
					Vector2 cs = it.Holder.Hitbox.Size.X > 1f ? it.Holder.Hitbox.Size : new Vector2(300f, 422f);
					float half = cs.Y * it.Slot.Scale.Y * it.Holder.Scale.Y * 0.5f;
					plate.Position = it.Slot.Position + new Vector2(-20f * k, half + 8f * k);
					((Label)plate.GetChild(plate.GetChildCount() - 1)).Text = it.Card.Title;
					float a = Ease.OutCubic(it.Lift);
					plate.Modulate = new Color(1f, 1f, 1f, a);
					plate.Scale = new Vector2(Mathf.Lerp(0.7f, 1f, a), 1f);
				}
			});
		}

		tree.ProcessFrame += Tick;
		root.TreeExiting += () =>
		{
			tree.ProcessFrame -= Tick;
			pile.ContentsChanged -= onChanged;
		};
		Audio.Sfx.Clip("ShuffleTime_Whoosh", 0.45f);
	}

	// ── 点击特写 ──
	private static readonly Vector2 FocusCenter = new(700f, 560f);   // 特写卡牌中心（参考像素）
	private const float FocusW = 450f;                               // 特写卡牌宽

	private sealed class Focus
	{
		public Node2D Layer = null!;
		public ColorRect Dim = null!;
		public Node2D Band = null!;
		public Node2D Rays = null!;
		public Node2D Info = null!;
		public Label Name = null!;
		public Label Kind = null!;
		public Label Copies = null!;
	}

	/// <summary>特写的背景层：压暗、穿过卡牌的 P3R 白色斜带（带红 / 青细条）、卡牌后面慢转的光芒和两道细圆、左侧说明（牌名 / 类型费用 / 同名张数）。</summary>
	private static Focus FocusLayer(Control root, Vector2 view, float k, Font font)
	{
		Focus f = new() { Layer = new Node2D { Visible = false } };
		root.AddChild(f.Layer);
		f.Dim = new ColorRect { Size = view, Color = new Color(0.01f, 0.03f, 0.13f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		f.Layer.AddChild(f.Dim);
		Vector2 c = FocusCenter * k;
		// 斜带：以特写中心为轴转 -16°，很长；从左下方滑进来
		f.Band = new Node2D { Position = c, Rotation = Mathf.DegToRad(-16f) };
		f.Layer.AddChild(f.Band);
		float len = 2600f * k, h = 240f * k;
		Fx.Poly(f.Band, new[] { new Vector2(-len, -h / 2), new Vector2(len, -h / 2), new Vector2(len, h / 2), new Vector2(-len, h / 2) }, new Color(0.96f, 0.98f, 1f, 0.9f));
		Fx.Poly(f.Band, new[] { new Vector2(-len, h / 2 + 10f * k), new Vector2(len, h / 2 + 10f * k), new Vector2(len, h / 2 + 22f * k), new Vector2(-len, h / 2 + 22f * k) }, Red);
		Fx.Poly(f.Band, new[] { new Vector2(-len, -h / 2 - 16f * k), new Vector2(len, -h / 2 - 16f * k), new Vector2(len, -h / 2 - 10f * k), new Vector2(-len, -h / 2 - 10f * k) }, Cyan);
		// 光芒：16 道细楔形（加色），两道细圆
		f.Rays = new Node2D { Position = c };
		f.Layer.AddChild(f.Rays);
		for (int i = 0; i < 16; i++)
		{
			float a = Mathf.Tau * i / 16f, w = Mathf.DegToRad(i % 2 == 0 ? 3.2f : 1.6f), r = 900f * k;
			Fx.Poly(f.Rays, new[] { Vector2.Zero, new Vector2(Mathf.Cos(a - w), Mathf.Sin(a - w)) * r, new Vector2(Mathf.Cos(a + w), Mathf.Sin(a + w)) * r }, new Color(0.45f, 0.85f, 1f, 0.16f), true);
		}
		f.Rays.AddChild(new Line2D { Points = Fx.Circle(400f * k, 96).Append(Fx.Circle(400f * k, 96)[0]).ToArray(), Width = 2f * k, DefaultColor = new Color(0.8f, 0.95f, 1f, 0.7f), Antialiased = true });
		f.Rays.AddChild(new Line2D { Points = Fx.Circle(430f * k, 96).Append(Fx.Circle(430f * k, 96)[0]).ToArray(), Width = 1f * k, DefaultColor = new Color(0.6f, 0.9f, 1f, 0.45f), Antialiased = true });
		// 左侧说明
		f.Info = new Node2D();
		f.Layer.AddChild(f.Info);
		f.Name = Text(f.Info, "", Typo.CjkBold, (int)(54f * k), Colors.White, new Vector2(60f, 300f) * k, (int)(6f * k), Ink);
		Fx.Poly(f.Info, new[] { new Vector2(60f, 382f) * k, new Vector2(400f, 382f) * k, new Vector2(394f, 390f) * k, new Vector2(54f, 390f) * k }, Red);
		f.Kind = Text(f.Info, "", font, (int)(40f * k), Cyan, new Vector2(60f, 398f) * k, (int)(4f * k), Ink);
		f.Copies = Text(f.Info, "", font, (int)(34f * k), Colors.White, new Vector2(60f, 448f) * k, (int)(4f * k), Ink);
		Text(f.Info, Zh ? "点击任意处返回" : "CLICK ANYWHERE TO RETURN", Zh ? Typo.CjkBold : font, (int)(20f * k), new Color(0.75f, 0.88f, 1f, 0.8f), new Vector2(62f, 508f) * k, (int)(3f * k), Ink);
		return f;
	}

	private static void Fill(Focus f, CardModel card, CardPile pile, float k)
	{
		f.Name.Text = card.Title;
		int cost = Cost(card);
		f.Kind.Text = $"{TypeEn(card.Type)}  ·  COST {(cost < 0 ? "X" : cost.ToString())}";
		int copies = pile.Cards.Count(c => c.Id.Entry == card.Id.Entry && c.IsUpgraded == card.IsUpgraded);
		f.Copies.Text = Zh ? $"牌堆中 ×{copies}" : $"IN PILE ×{copies}";
	}

	private static void Animate(Focus f, float e, float p, bool on, float now, Vector2 center, float k)
	{
		f.Layer.Visible = e > 0.001f;
		if (!f.Layer.Visible)
		{
			return;
		}
		f.Dim.Color = new Color(0.01f, 0.03f, 0.13f, 0.62f * e);
		f.Band.Position = center + new Vector2(-1f, 0.29f).Normalized() * 2400f * k * (1f - Ease.OutExpo(Mathf.Clamp(p * 1.4f - 0.1f, 0f, 1f)));
		f.Band.Modulate = new Color(1f, 1f, 1f, e);
		f.Rays.Rotation = now * 0.12f;
		f.Rays.Scale = Vector2.One * (0.6f + 0.4f * e);
		f.Rays.Modulate = new Color(1f, 1f, 1f, e);
		float ia = on ? Mathf.Clamp((p - 0.45f) / 0.4f, 0f, 1f) : e;
		f.Info.Modulate = new Color(1f, 1f, 1f, ia);
		f.Info.Position = new Vector2(-90f * k * (1f - Ease.OutCubic(ia)), 0f);
	}

	/// <summary>白色大圆盘：中心纯白、边缘淡青（顶点色渐变），三道同心细圆、外圈青 / 白刻度弧；立体大字只画在圆盘里面。</summary>
	private static void Disc(Node2D parent, PileType type, Font? font, float k, float rx)
	{
		Vector2 c = new Vector2(1760f, 500f) * k + new Vector2(rx, 0f);
		float r = 640f * k;
		Vector2[] ring = Fx.Circle(r, 160);
		Vector2[] pts = new Vector2[ring.Length + 1];
		Color[] cols = new Color[pts.Length];
		pts[0] = Vector2.Zero;
		cols[0] = new Color(0.98f, 0.99f, 1f, 0.97f);
		for (int i = 0; i < ring.Length; i++)
		{
			pts[i + 1] = ring[i];
			cols[i + 1] = new Color(0.8f, 0.9f, 0.97f, 0.97f);
		}
		Godot.Collections.Array polys = new();
		for (int i = 0; i < ring.Length; i++)
		{
			polys.Add(new[] { 0, i + 1, (i + 1) % ring.Length + 1 });
		}
		Polygon2D disc = new() { Position = c, Polygon = pts, Polygons = polys, VertexColors = cols, ClipChildren = CanvasItem.ClipChildrenMode.AndDraw };
		parent.AddChild(disc);
		// 细斜纹
		for (int i = -14; i <= 14; i++)
		{
			float o = i * 46f * k;
			disc.AddChild(new Line2D { Points = new[] { new Vector2(o - r, -r), new Vector2(o + r, r) }, Width = 1.5f * k, DefaultColor = new Color(0.55f, 0.68f, 0.82f, 0.12f) });
		}
		// 立体大字：深灰投影 + 浅灰字身，沿圆盘左下边缘往上走，下半截被圆盘裁掉
		string word = type switch { PileType.Draw => "DRAW", PileType.Discard => "DISCARD", PileType.Exhaust => "EXHAUST", _ => "PILE" };
		Vector2 origin = new Vector2(1190f, 1150f) * k + new Vector2(rx, 0f) - c;
		float rot = Mathf.DegToRad(-76f);
		int size = (int)(250f * k);
		Label shadow = Text(disc, word, font, size, new Color(0.46f, 0.5f, 0.56f), origin + new Vector2(22f, 18f) * k, 0, Ink);
		shadow.Rotation = rot;
		Label body = Text(disc, word, font, size, new Color(0.84f, 0.86f, 0.9f), origin, (int)(3f * k), new Color(0.59f, 0.63f, 0.69f));
		body.Rotation = rot;
		foreach ((float rr, float w, Color col) in new[] { (r - 26f * k, 2f, new Color(0.74f, 0.81f, 0.88f)), (r - 70f * k, 1.2f, new Color(0.8f, 0.86f, 0.91f)), (r - 140f * k, 1.2f, new Color(0.84f, 0.88f, 0.93f)) })
		{
			disc.AddChild(new Line2D { Points = Arc(rr, 95f, 265f, 80), Width = w * k, DefaultColor = col, Antialiased = true });
		}
		// 外圈：青色细弧 + 白色短弧 + 一串刻度点，慢慢转
		Node2D outer = new() { Position = c };
		parent.AddChild(outer);
		outer.AddChild(new Line2D { Points = Arc(r + 18f * k, 120f, 230f, 90), Width = 3f * k, DefaultColor = new Color(0.6f, 0.9f, 1f, 0.8f), Antialiased = true });
		outer.AddChild(new Line2D { Points = Arc(r + 34f * k, 150f, 185f, 40), Width = 6f * k, DefaultColor = new Color(1f, 1f, 1f, 0.86f), Antialiased = true });
		outer.AddChild(new Line2D { Points = Arc(r + 46f * k, 196f, 214f, 24), Width = 2f * k, DefaultColor = new Color(0.6f, 0.9f, 1f, 0.6f), Antialiased = true });
		for (int i = 0; i < 14; i++)
		{
			float a = Mathf.DegToRad(128f + i * 7f);
			Fx.Poly(outer, Fx.Circle(3f * k, 8).Select(p => p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (r + 18f * k)).ToArray(), new Color(0.86f, 0.96f, 1f, 0.86f));
		}
		Tween spin = outer.CreateTween().SetLoops();
		spin.TweenProperty(outer, "rotation", Mathf.DegToRad(-6f), 9.0).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		spin.TweenProperty(outer, "rotation", 0f, 9.0).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
	}

	private static Vector2[] Arc(float r, float a0, float a1, int n)
	{
		Vector2[] pts = new Vector2[n + 1];
		for (int i = 0; i <= n; i++)
		{
			float a = Mathf.DegToRad(Mathf.Lerp(a0, a1, (float)i / n));
			pts[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
		}
		return pts;
	}

	/// <summary>立绘：Spine 待机动画（读不到就用静态图），牌边一圈慢慢上浮的光粒子 + 两颗闪烁的十字星。</summary>
	private static void Portrait(Node2D parent, float k, float rx)
	{
		float s = PorScale * k;
		Vector2 topLeft = PorTopLeft * k + new Vector2(rx, 0f);
		Node2D holder = new() { Position = topLeft + new Vector2(512f, 768f) * s, Scale = Vector2.One * s };
		parent.AddChild(holder);
		Texture2D? tex = ResourceLoader.Load(PortraitPath) as Texture2D;
		Resource? data = ResourceLoader.Exists(PortraitSpine) ? ResourceLoader.Load(PortraitSpine) : null;
		bool dataOk = data != null && (!data.HasMethod("is_skeleton_data_loaded") || data.Call("is_skeleton_data_loaded").AsBool());
		Node2D? spine = dataOk ? ClassDB.Instantiate("SpineSprite").AsGodotObject() as Node2D : null;
		if (spine != null)
		{
			spine.Position = new Vector2(0f, 768f);
			holder.AddChild(spine);
			spine.Set("skeleton_data_res", data!);
			Scenes.CharSelectBg.PlayIdle(spine, 30, () =>
			{
				if (GodotObject.IsInstanceValid(holder) && tex != null)
				{
					spine.QueueFree();
					holder.AddChild(new Sprite2D { Texture = tex });
				}
			});
		}
		else if (tex != null)
		{
			holder.AddChild(new Sprite2D { Texture = tex });
		}
		// 牌的位置（立绘画布坐标，见 art/p3pile/meta.json）
		Vector2 card = topLeft + new Vector2(211f, 425f) * s;
		CpuParticles2D motes = new()
		{
			Position = card,
			Amount = 18,
			Lifetime = 3.2,
			Preprocess = 2.0,
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
			EmissionRectExtents = new Vector2(110f, 150f) * k,
			Direction = new Vector2(0f, -1f),
			Spread = 30f,
			Gravity = new Vector2(0f, -6f) * k,
			InitialVelocityMin = 8f * k,
			InitialVelocityMax = 22f * k,
			ScaleAmountMin = 1.2f * k,
			ScaleAmountMax = 3.2f * k,
			Color = new Color(0.85f, 0.97f, 1f, 0.9f),
			ColorRamp = new Gradient { Offsets = new[] { 0f, 0.2f, 0.75f, 1f }, Colors = new[] { new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 0.7f), new Color(1f, 1f, 1f, 0f) } },
			Material = Fx.Additive,
		};
		parent.AddChild(motes);
		foreach ((Vector2 at, float len, float delay) in new[] { (topLeft + new Vector2(132f, 318f) * s, 20f, 0.0f), (topLeft + new Vector2(290f, 530f) * s, 14f, 1.7f) })
		{
			Node2D star = new() { Position = at, Scale = Vector2.Zero };
			parent.AddChild(star);
			star.AddChild(new Line2D { Points = new[] { new Vector2(-len, 0f) * k, new Vector2(len, 0f) * k }, Width = 2f * k, DefaultColor = Colors.White });
			star.AddChild(new Line2D { Points = new[] { new Vector2(0f, -len) * k, new Vector2(0f, len) * k }, Width = 2f * k, DefaultColor = Colors.White });
			Tween tw = star.CreateTween().SetLoops();
			tw.TweenInterval(1.2 + delay);
			tw.TweenProperty(star, "scale", Vector2.One, 0.25).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			tw.Parallel().TweenProperty(star, "rotation", Mathf.Pi / 4f, 0.6);
			tw.TweenProperty(star, "scale", Vector2.Zero, 0.4).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
			tw.TweenProperty(star, "rotation", 0f, 0.01);
			tw.TweenInterval(2.2 - delay * 0.5);
		}
	}

	/// <summary>段标题：深色斜切牌（左边一道青色）+ 英文类型 + 中文 + 青色张数，后面一条细线和箭头延伸到网格右边。</summary>
	private static Node2D SectionHead(Node2D parent, string en, string zh, int count, Font? font, float k)
	{
		Node2D n = new() { Modulate = new Color(1f, 1f, 1f, 0f) };
		parent.AddChild(n);
		float h = 44f;
		Fx.Poly(n, new[] { new Vector2(12f, -h / 2), new Vector2(330f, -h / 2), new Vector2(320f, h / 2), new Vector2(0f, h / 2) }.Select(p => p * k).ToArray(), new Color(0.02f, 0.03f, 0.09f, 0.94f));
		Fx.Poly(n, new[] { new Vector2(12f, -h / 2), new Vector2(24f, -h / 2), new Vector2(14f, h / 2), new Vector2(2f, h / 2) }.Select(p => p * k).ToArray(), Cyan);
		Label enL = Text(n, en, font, (int)(36f * k), Colors.White, new Vector2(34f, -24f) * k, 0, Ink);
		if (zh.Length > 0)
		{
			Text(n, zh, Typo.CjkBold, (int)(20f * k), Ice, new Vector2(44f * k + enL.GetMinimumSize().X, -14f * k), 0, Ink);
		}
		Label c = Text(n, "×" + count, font, (int)(38f * k), Cyan, new Vector2(250f, -25f) * k, 0, Ink);
		c.HorizontalAlignment = HorizontalAlignment.Right;
		c.Size = new Vector2(60f * k, c.Size.Y);
		n.AddChild(new Line2D { Points = new[] { new Vector2(338f, 0f) * k, new Vector2(Cols * CellW - 6f, 0f) * k }, Width = 2f * k, DefaultColor = new Color(0.6f, 0.86f, 1f, 0.55f) });
		Fx.Poly(n, new[] { new Vector2(Cols * CellW - 6f, -6f) * k, new Vector2(Cols * CellW + 6f, 0f) * k, new Vector2(Cols * CellW - 6f, 6f) * k }, new Color(0.6f, 0.86f, 1f, 0.8f));
		return n;
	}

	/// <summary>悬停白框：外圈一层青色柔光 + 白色细框（画在卡牌外沿，按卡牌原始大小，跟着 slot 缩放）。</summary>
	private static Node2D CardFrame(Control slot)
	{
		Node2D f = new() { Modulate = new Color(1f, 1f, 1f, 0f) };
		slot.AddChild(f);
		Vector2 half = new(150f + 10f, 211f + 10f);
		Vector2[] rect = { new(-half.X, -half.Y), new(half.X, -half.Y), new(half.X, half.Y), new(-half.X, half.Y), new(-half.X, -half.Y) };
		f.AddChild(new Line2D { Points = rect, Width = 22f, DefaultColor = new Color(0.55f, 0.9f, 1f, 0.28f), JointMode = Line2D.LineJointMode.Round, Material = Fx.Additive });
		f.AddChild(new Line2D { Points = rect, Width = 6f, DefaultColor = Colors.White, JointMode = Line2D.LineJointMode.Round });
		// 左上角一块红色三角（P3R 选中标记）
		Fx.Poly(f, new[] { new Vector2(-half.X - 4f, -half.Y + 56f), new Vector2(-half.X - 4f, -half.Y - 4f), new Vector2(-half.X + 56f, -half.Y - 4f) }, Red);
		return f;
	}

	/// <summary>名牌：白边黑底斜切条 + 红色尾边，牌名用粗斜体中文。最后一个子节点是文字。</summary>
	private static Node2D NamePlate(Control root, float k)
	{
		Node2D n = new() { Visible = false };
		root.AddChild(n);
		float w = 300f, h = 54f;
		Fx.Poly(n, new[] { new Vector2(14f, 0f), new Vector2(w + 30f, 0f), new Vector2(w + 18f, h), new Vector2(0f, h) }.Select(p => p * k).ToArray(), new Color(0.98f, 0.99f, 1f));
		Fx.Poly(n, new[] { new Vector2(18f, 4f), new Vector2(w + 6f, 4f), new Vector2(w - 6f, h - 4f), new Vector2(6f, h - 4f) }.Select(p => p * k).ToArray(), new Color(0.02f, 0.03f, 0.09f));
		Fx.Poly(n, new[] { new Vector2(w + 10f, 4f), new Vector2(w + 26f, 4f), new Vector2(w + 14f, h - 4f), new Vector2(w - 2f, h - 4f) }.Select(p => p * k).ToArray(), Red);
		Label l = Text(n, "", Typo.CjkBold, (int)(32f * k), Colors.White, new Vector2(32f, 5f) * k, 0, Ink);
		l.ClipText = true;
		l.Size = new Vector2((w - 40f) * k, (h - 8f) * k);
		return n;
	}

	private static void Bubbles(Control root, Vector2 view, float k)
	{
		Node2D layer = new();
		root.AddChild(layer);
		var rng = new RandomNumberGenerator();
		for (int i = 0; i < 26; i++)
		{
			float r = rng.RandfRange(3f, 12f) * k;
			Line2D ring = new() { Points = Fx.Circle(r, 16).Append(Fx.Circle(r, 16)[0]).ToArray(), Width = 1.6f * k, DefaultColor = new Color(0.75f, 0.93f, 1f, rng.RandfRange(0.25f, 0.6f)), Antialiased = true };
			layer.AddChild(ring);
			float x = rng.RandfRange(0f, view.X * 0.6f);
			float dur = rng.RandfRange(6f, 12f);
			float delay = rng.RandfRange(0f, dur);
			ring.Position = new Vector2(x, view.Y + 20f);
			Tween tw = ring.CreateTween().SetLoops();
			tw.TweenInterval(delay * 0.3);
			tw.TweenProperty(ring, "position", new Vector2(x + rng.RandfRange(-40f, 40f) * k, -30f), dur).From(new Vector2(x, view.Y + 20f));
		}
	}

	private static Label Text(Node parent, string text, Font? font, int size, Color color, Vector2 at, int outline, Color outlineColor)
	{
		Label l = new() { Text = text, Position = at, MouseFilter = Control.MouseFilterEnum.Ignore };
		if (font != null) l.AddThemeFontOverride("font", font);
		l.AddThemeFontSizeOverride("font_size", size);
		l.AddThemeColorOverride("font_color", color);
		if (outline > 0)
		{
			l.AddThemeColorOverride("font_outline_color", outlineColor);
			l.AddThemeConstantOverride("outline_size", outline);
		}
		parent.AddChild(l);
		return l;
	}

	private static bool Zh => TranslationServer.GetLocale().StartsWith("zh");

	private static int Cost(CardModel c)
	{
		try
		{
			return c.EnergyCost.Canonical;
		}
		catch (Exception)
		{
			return 99;
		}
	}

	private static int TypeOrder(CardType t) => t switch
	{
		CardType.Attack => 0,
		CardType.Skill => 1,
		CardType.Power => 2,
		CardType.Status => 3,
		CardType.Curse => 4,
		_ => 5,
	};

	private static string TypeEn(CardType t) => t switch
	{
		CardType.Attack => "ATTACK",
		CardType.Skill => "SKILL",
		CardType.Power => "POWER",
		CardType.Status => "STATUS",
		CardType.Curse => "CURSE",
		_ => "OTHER",
	};

	private static string TypeZh(CardType t) => !Zh ? "" : t switch
	{
		CardType.Attack => "攻击",
		CardType.Skill => "技能",
		CardType.Power => "能力",
		CardType.Status => "状态",
		CardType.Curse => "诅咒",
		_ => "其他",
	};
}
