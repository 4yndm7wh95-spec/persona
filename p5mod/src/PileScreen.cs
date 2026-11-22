using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace P5Joker;

/// <summary>
/// 牌堆界面换成 P5R 暂停菜单风格（docs/p5/PILE_PLAN.md）。交互部分照搬 P3 mod 的 PileFx（分支 claude/admiring-knuth-lqj1x7，mod/src/Vfx/PileFx.cs）：
/// 原版卡牌网格只藏起来不动，卡牌用游戏自己的 NGridCardHolder（悬停关键词提示是原版的），位置和缩放由这里排；
/// 按类型分段（或原版顺序一整段，能看出抽牌顺序）、指到的那张放大 + 名牌、其余压暗、点击飞到中间翻转特写（可查看升级）、手柄方向键导航、滚轮 / 拖动滚动、退场动画。
/// 装饰和入场 / 退场动画在 Vfx/PileDraw.cs。
/// 目前只做了抽牌堆（= P5R 主菜单）；消耗堆 / 弃牌堆 / 牌组以后一个个做，没做之前用原版界面。
/// 只在本机玩家是 Joker、设置里「牌堆界面」开着时生效；搭到一半出错就回滚到原版网格，显示中出错也退回原版。
/// </summary>
public static class PileScreen
{
	private const string RootName = "P5PileScreen";
	private const float HeaderH = 70f;
	private const float FocusW = 450f;

	private static readonly Color Ink = new(0.04f, 0.04f, 0.04f), Paper = new(0.94f, 0.94f, 0.94f), Red = new(0.9f, 0.1f, 0.11f);

	private static Font Barlow => P5Ui.Font(P5Ui.Barlow);
	private static Font Cjk => P5Ui.Font(P5Ui.CjkBold);

	public static bool Enabled(CardPile? pile) =>
		pile != null && (pile.Type == PileType.Draw || pile.Type == PileType.Exhaust) && P5Settings.Enabled(P5Settings.Option.Pile) && Joker.LocalRun;

	private sealed class Item
	{
		public Control? Clip;
		public Control? Slot;
		public NGridCardHolder? Holder;
		public CardModel Card = null!;
		public int Index;
		public bool AnimateEntrance;
		public float Born = -1f;
		public float Lift;
		public float Dim = 1f;
		public Vector2 At;
		public Vector2 From;
		public float MoveT0 = -1f, MoveDelay;
	}

	private sealed class Section
	{
		public Node2D Head = null!;
		public float Y;
		public float Born = -1f, Die = -1f;
	}

	private static readonly AccessTools.FieldRef<NCardGrid, List<CardModel>>? GridCards = Field();
	private static AccessTools.FieldRef<NCardGrid, List<CardModel>>? Field()
	{
		try { return AccessTools.FieldRefAccess<NCardGrid, List<CardModel>>("_cards"); }
		catch { return null; }
	}

	/// <summary>原版抽牌堆的顺序（P3 v0.14.98：SL 要看）：优先直接读原版网格，读不到按同一段排序现算。</summary>
	private static List<CardModel> VanillaOrder(Control grid, PileType type, List<CardModel> cards)
	{
		if (grid is NCardGrid g && GridCards != null && GridCards(g) is List<CardModel> shown && shown.Count == cards.Count && shown.All(c => cards.Contains(c)))
			return shown.ToList();
		if (type == PileType.Draw)
		{
			cards = cards.ToList();
			cards.Sort((c1, c2) => c1.Rarity != c2.Rarity ? c1.Rarity.CompareTo(c2.Rarity) : string.Compare(c1.Id.Entry, c2.Id.Entry, StringComparison.Ordinal));
		}
		return cards;
	}

	private static readonly string[] Hidden = { "CardGrid", "BottomText", "%SortingBg", "%ObtainedSorter", "%CardTypeSorter", "%CostSorter", "%AlphabeticalSorter" };
	private static readonly Dictionary<ulong, Action> Exits = new();

	/// <summary>关闭时先播退场（接管原版的「藏起来 + 删掉」），返回 false = 没有我们的界面，走原版。</summary>
	public static bool TryExit(Control screen)
	{
		if (!Exits.Remove(screen.GetInstanceId(), out Action? exit)) return false;
		exit();
		return true;
	}

	private static void Finalize(Control screen)
	{
		if (GodotObject.IsInstanceValid(screen))
		{
			screen.Visible = false;
			screen.QueueFreeSafely();
		}
	}

	public static void Rollback(Control screen)
	{
		Exits.Remove(screen.GetInstanceId());
		if (screen.GetNodeOrNull<Control>(RootName) is not Control root) return;
		root.Hide();
		foreach (string path in Hidden)
		{
			if (screen.GetNodeOrNull<CanvasItem>(path) is CanvasItem ci) ci.Visible = true;
		}
		root.QueueFree();
	}

	public static void Build(Control screen, CardPile pile)
	{
		if (screen.GetNodeOrNull(RootName) != null || screen.GetNodeOrNull<Control>("CardGrid") is not Control grid) return;
		Vector2 view = screen.GetViewportRect().Size;
		if (view.X <= 0f || view.Y <= 0f) return;
		float k = view.Y / 1080f;
		bool exhaust = pile.Type == PileType.Exhaust;
		Font font = Barlow;
		float t0 = Time.GetTicksMsec() / 1000f;
		float Now() => Time.GetTicksMsec() / 1000f - t0;
		bool exiting = false, finalized = false;
		float exitT0 = 0f;
		bool orderMode = exhaust ? P5Settings.ExhaustOrder : P5Settings.DrawOrder;

		Control root = new() { Name = RootName, MouseFilter = Control.MouseFilterEnum.Stop };
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		screen.AddChild(root);
		screen.MoveChild(root, grid.GetIndex());             // 原版网格的位置：返回按钮仍在上面
		foreach (string path in Hidden)
		{
			if (screen.GetNodeOrNull<CanvasItem>(path) is CanvasItem ci) ci.Visible = false;
		}

		// ── 装饰（红底、星纹面板、标题牌、立绘、COMMAND、预告信）──
		// 抽牌堆 = P5R 主菜单（PileDraw），消耗堆 = SKILL 页（PileExhaust）
		IPileDecor draw = exhaust ? new PileExhaust(root, view, pile.Cards.Count) : new PileDraw(root, view, pile.Cards.Count);
		IPileDecor L = draw;
		float gx = L.GridX;
		Node2D head = new() { Name = "PileHead" };
		root.AddChild(head);
		ModeSwitch modeSwitch = ModeSwitch.Build(head, L.SwitchAt * k, k, orderMode);

		// ── 网格 ──
		Control cards = new() { MouseFilter = Control.MouseFilterEnum.Ignore };
		cards.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.AddChild(cards);                                // 在装饰上面：特写的压暗层 / 说明 / 翻转的牌都要盖住立绘
		Node2D heads = new();
		Vector2 clipAt = new(0f, L.GridTop * k);
		Vector2 clipSize = new(view.X, (L.GridBottom - L.GridTop) * k);
		Control headClip = new() { Name = "PileHeadingClip", Position = clipAt, Size = clipSize, ClipContents = true, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(headClip);
		headClip.AddChild(heads);
		heads.Position = -clipAt;
		root.MoveChild(headClip, cards.GetIndex());
		Node2D? plate = null;
		Node2D empty = new() { Modulate = new Color(1f, 1f, 1f, 0.5f) };
		root.AddChild(empty);
		PileText.Word(empty, "empty", new Vector2(gx, L.GridTop + 60f) * k, 90f * k, Paper);
		empty.Visible = false;

		// ── 点击特写 ──
		Focus? fx = null;
		Control catcher = new() { MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };
		catcher.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		cards.AddChild(catcher);
		Control navFocus = new() { Name = "P5PileNavFocus", FocusMode = Control.FocusModeEnum.All, MouseFilter = Control.MouseFilterEnum.Ignore, Size = Vector2.One };
		NodePath self = new(".");
		navFocus.FocusNeighborLeft = navFocus.FocusNeighborRight = navFocus.FocusNeighborTop = navFocus.FocusNeighborBottom = self;
		navFocus.FocusNext = navFocus.FocusPrevious = self;
		root.AddChild(navFocus);
		Item? focused = null;
		Flip? flip = null;
		float focusP = 0f;
		bool focusOn = false;
		float lastNow = 0f;
		List<Item> items = new();
		UpgradeButton? upBtn = null;
		bool focusUp = false;
		float upT0 = -1f;
		bool upSwapped = false;
		CardModel? upModel = null;
		const float UpFlipTime = 0.6f;

		void ShowFace(Item it, bool up)
		{
			if (up && upModel == null)
			{
				upModel = (CardModel)it.Card.MutableClone();
				upModel.UpgradeInternal();
			}
			if (flip?.Card is NCard fc && GodotObject.IsInstanceValid(fc))
			{
				if (up && upModel != null)
				{
					fc.Model = upModel;
					fc.ShowUpgradePreview();
				}
				else
				{
					fc.Model = it.Card;
					fc.UpdateVisuals(pile.Type, CardPreviewMode.Normal);
				}
			}
			if (it.Holder != null && GodotObject.IsInstanceValid(it.Holder) && it.Card.IsUpgradable)
				it.Holder.SetIsPreviewingUpgrade(up);
			Fill(fx!, up && upModel != null ? upModel : it.Card, pile);
		}

		void FlipUpgrade()
		{
			if (focused == null || !focusOn || upT0 >= 0f || !focused.Card.IsUpgradable || flip == null) return;
			upT0 = Now();
			upSwapped = false;
			Sfx.Play(Sfx.MenuWhoosh, 0.3f);
		}
		void EnsureFocus()
		{
			if (fx != null) return;
			fx = FocusLayer(cards, view, k, L.FocusAt, L.InfoAt);
			UpgradeButton button = UpgradeButton.Build(cards, k);
			upBtn = button;
			button.Hit.GuiInput += ev => ModEntry.Safe("pile upgrade flip", () =>
			{
				if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && focused != null)
				{
					button.Hit.AcceptEvent();
					FlipUpgrade();
				}
			});
		}
		List<Section> sections = new();
		List<Section> dying = new();
		Queue<Item> pending = new();
		List<CardModel>? displayed = null;
		Item? hovered = null;
		float scroll = 0f, target = 0f, contentH = 0f;
		bool dragging = false;
		float dragFrom = 0f, dragScroll = 0f;
		float nextBorn = L.CardsStart;

		void ReleaseCard(Item it)
		{
			if (it.Holder is { } holder && GodotObject.IsInstanceValid(holder) && holder.GetParent() == it.Slot) holder.QueueFreeSafely();
			if (it.Clip is { } clip && GodotObject.IsInstanceValid(clip)) clip.QueueFree();
			it.Holder = null;
			it.Slot = null;
			it.Clip = null;
		}

		void Refresh(List<CardModel> all, bool animateSort)
		{
			bool initial = displayed == null;
			if (!animateSort && displayed != null && all.SequenceEqual(displayed, ReferenceEqualityComparer.Instance)) return;
			var groups = Groups(all);
			var reuse = new Dictionary<CardModel, Item>(ReferenceEqualityComparer.Instance);
			foreach (Item it in items) reuse[it.Card] = it;
			foreach (Section section in sections)
			{
				if (animateSort)
				{
					section.Die = Now();
					dying.Add(section);
				}
				else if (GodotObject.IsInstanceValid(section.Head)) section.Head.QueueFree();
			}
			sections.Clear();
			pending.Clear();
			contentH = Place(groups, reuse, animateSort, initial);
			foreach (Item gone in reuse.Values) ReleaseCard(gone);
			if (hovered != null && !items.Contains(hovered)) hovered = null;
			if (focused != null && !items.Contains(focused))
			{
				focused = null;
				focusOn = false;
				focusP = 0f;
				catcher.Visible = false;
				upBtn?.Hide();
				FreeFlip(flip);
				flip = null;
			}
			displayed = all;
			target = Mathf.Clamp(target, 0f, MaxScroll());
		}

		List<(string En, string Zh, List<CardModel> Cards)> Groups(List<CardModel> all)
		{
			List<(string En, string Zh, List<CardModel> Cards)> groups = new();
			if (orderMode)
			{
				if (all.Count > 0) groups.Add((exhaust ? "exhaust" : "draw", "", VanillaOrder(grid, pile.Type, all)));
			}
			else
			{
				foreach (IGrouping<int, CardModel> g in all.GroupBy(c => TypeOrder(c.Type)).OrderBy(g => g.Key))
				{
					List<CardModel> list = g.OrderBy(Cost).ThenBy(c => c.Title, StringComparer.Ordinal).ThenBy(c => c.IsUpgraded ? 1 : 0).ToList();
					groups.Add((TypeKey(list[0].Type), Zh ? TypeKey(list[0].Type) + "_zh" : "", list));
				}
			}
			return groups;
		}

		float Place(List<(string En, string Zh, List<CardModel> Cards)> groups, Dictionary<CardModel, Item> reuse, bool animateSort, bool initial)
		{
			float y = 0f;
			int index = 0;
			float now = Now();
			List<Item> order = new();
			foreach ((string en, string zh, List<CardModel> list) in groups)
			{
				sections.Add(new Section { Head = SectionHead(heads, en, zh, list.Count, k), Y = y, Born = animateSort ? now + 0.18f : -1f });
				y += HeaderH;
				for (int i = 0; i < list.Count; i++)
				{
					Vector2 at = new((i % L.Cols) * L.CellW + L.CellW / 2f, y + (i / L.Cols) * L.CellH + L.CellH / 2f);
					if (!reuse.Remove(list[i], out Item? it)) it = new Item { Card = list[i], At = at, AnimateEntrance = initial };
					it.From = it.At;
					it.At = at;
					it.Index = index++;
					it.MoveT0 = animateSort ? now : -1f;
					it.MoveDelay = Mathf.Min(0.3f, it.Index * 0.014f);
					if (it.Slot == null) pending.Enqueue(it);
					order.Add(it);
				}
				y += Mathf.Ceil(list.Count / (float)L.Cols) * L.CellH + 16f;
			}
			items.Clear();
			items.AddRange(order);
			return y;
		}

		void Switch(bool toOrder)
		{
			if (orderMode == toOrder || exiting || focused != null) return;
			orderMode = toOrder;
			if (exhaust) P5Settings.SetExhaustOrder(toOrder); else P5Settings.SetDrawOrder(toOrder);
			Refresh(pile.Cards.ToList(), animateSort: true);
			modeSwitch.Set(toOrder);
			Sfx.Take("gx_card_stack", 0.2f);
		}

		float MaxScroll() => Mathf.Max(0f, contentH - (L.GridBottom - L.GridTop));

		void Create(Item it)
		{
			NCard card = NCard.Create(it.Card) ?? throw new InvalidOperationException($"Could not create pile card {it.Card.Id}");
			NGridCardHolder h;
			try
			{
				h = NGridCardHolder.Create(card) ?? throw new InvalidOperationException($"Could not create pile card holder {it.Card.Id}");
			}
			catch
			{
				if (card.GetParent() == null) card.QueueFreeSafely();
				throw;
			}
			Control clip = new() { Name = "PileCardClip", Position = clipAt, Size = clipSize, ClipContents = true, MouseFilter = Control.MouseFilterEnum.Ignore };
			Control slot = new() { MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = new Color(1f, 1f, 1f, 0f) };
			clip.AddChild(slot);
			it.Clip = clip;
			it.Slot = slot;
			it.Holder = h;
			h.Visible = true;
			slot.AddChild(h);
			cards.AddChild(clip);
			if (!card.IsInsideTree() || h.CardNode != card)
				throw new InvalidOperationException($"Pile card {it.Card.Id} did not attach to its holder");
			card.UpdateVisuals(pile.Type, CardPreviewMode.Normal);
			PooledCardInput.Track(h);
			if (it.AnimateEntrance)
			{
				it.Born = Mathf.Max(Now(), nextBorn);
				nextBorn = it.Born + 0.022f;
			}
			bool OwnsHolder() => !exiting && GodotObject.IsInstanceValid(root) && root.IsInsideTree()
				&& GodotObject.IsInstanceValid(h) && h.GetParent() == slot && it.Holder == h;
			void Enter()
			{
				if (!OwnsHolder()) return;
				if (hovered != it)
				{
					hovered = it;
					Sfx.Play(Sfx.Cursor, 0.35f);
					draw.Kick();
				}
			}
			void Leave() { if (hovered == it) hovered = null; }
			void Input(InputEvent ev) => ModEntry.Safe("pile card input", () =>
			{
				if (!OwnsHolder()) return;
				if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left or MouseButton.Right })
				{
					h.Hitbox.AcceptEvent();
					Toggle(it);
				}
				else if (focused == null)
				{
					Wheel(ev);
				}
			});
			void Detach()
			{
				h.Hitbox.MouseEntered -= Enter;
				h.Hitbox.MouseExited -= Leave;
				h.Hitbox.GuiInput -= Input;
				h.TreeExiting -= Detach;
			}
			h.Hitbox.MouseEntered += Enter;
			h.Hitbox.MouseExited += Leave;
			h.Hitbox.GuiInput += Input;
			h.TreeExiting += Detach;
		}

		void Toggle(Item it)
		{
			if (focused == null)
			{
				if (it.Slot == null) return;
				EnsureFocus();
				focused = it;
				focusOn = true;
				cards.MoveChild(fx!.Layer, cards.GetChildCount() - 1);
				cards.MoveChild(catcher, cards.GetChildCount() - 1);
				cards.MoveChild(it.Clip!, cards.GetChildCount() - 1);
				cards.MoveChild(upBtn!.Root, cards.GetChildCount() - 1);
				flip = MakeFlip(root, cards, it.Card, pile.Type, it.Holder);
				catcher.Visible = true;
				upModel = null;
				upT0 = -1f;
				focusUp = false;
				ShowFace(it, focusUp);
				upBtn.Show(it.Card.IsUpgradable, it.Card.IsUpgraded, focusUp, it.Card.CurrentUpgradeLevel + 1, it.Card.MaxUpgradeLevel > 1);
				Sfx.Play(Sfx.Select, 0.5f);
			}
			else if (focusOn)
			{
				focusOn = false;
				Sfx.Play(Sfx.MenuWhoosh, 0.25f);
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

		// ── 手柄：方向键在网格里移动选中，确认键特写 / 收回；特写时返回键只收回特写 ──
		Item? sel = null;
		string? heldDir = null;
		float repeatAt = 0f;
		bool backBound = false;
		Action unzoom = () => ModEntry.Safe("pile pad back", () => { if (focused != null && focusOn) Toggle(focused); });
		Action swallow = () => { };
		string[] backKeys = { "ui_cancel", "mega_pause_and_back", "mega_back" };
		(string Action, int X, int Y)[] dirs = { ("ui_left", -1, 0), ("ui_right", 1, 0), ("ui_up", 0, -1), ("ui_down", 0, 1) };

		void BindBack(bool on)
		{
			if (on == backBound || NHotkeyManager.Instance is not NHotkeyManager hk) return;
			backBound = on;
			foreach (string key in backKeys)
			{
				if (on && !InputMap.HasAction(key)) continue;
				HotkeyBindings.Set(hk, key, unzoom, swallow, on);
			}
		}

		bool Pressed(string action) => InputMap.HasAction(action) && Input.IsActionJustPressed(action);

		void Move(int dx, int dy)
		{
			Item? cur = sel != null && items.Contains(sel) ? sel : null;
			if (cur == null)
			{
				sel = items.FirstOrDefault();
				return;
			}
			Item? best = null;
			float bestD = float.MaxValue;
			if (dy == 0)
			{
				foreach (Item it in items)
				{
					float d = (it.At.X - cur.At.X) * dx;
					if (Mathf.IsEqualApprox(it.At.Y, cur.At.Y) && d > 1f && d < bestD) { best = it; bestD = d; }
				}
			}
			else
			{
				float rowY = float.NaN;
				foreach (Item it in items)
				{
					float d = (it.At.Y - cur.At.Y) * dy;
					if (d > 1f && (float.IsNaN(rowY) || d < (rowY - cur.At.Y) * dy)) rowY = it.At.Y;
				}
				foreach (Item it in items)
				{
					float d = Mathf.Abs(it.At.X - cur.At.X);
					if (!float.IsNaN(rowY) && Mathf.IsEqualApprox(it.At.Y, rowY) && d < bestD) { best = it; bestD = d; }
				}
			}
			if (best != null)
			{
				sel = best;
				Sfx.Play(Sfx.Cursor, 0.35f);
				draw.Kick();
			}
		}

		void Nav(float now)
		{
			if (exiting) return;
			Viewport vp = root.GetViewport();
			Control? owner = vp.GuiGetFocusOwner();
			bool usingPad = GameCompat.DirectionalNavigation();
			if (usingPad && owner != navFocus && (owner == null || !owner.IsVisibleInTree() || grid.IsAncestorOf(owner)))
			{
				navFocus.GrabFocus();
				owner = navFocus;
				sel ??= items.FirstOrDefault();
			}
			bool pad = owner == navFocus && usingPad;
			BindBack(pad && focused != null && focusOn);
			if (!pad) return;
			if (focused != null)
			{
				if (focusOn && (Pressed("ui_select") || Pressed("ui_confirm")))
				{
					if (focused.Card.IsUpgradable) FlipUpgrade();
					else Toggle(focused);
				}
				return;
			}
			if (sel != null && !items.Contains(sel)) sel = items.FirstOrDefault();
			string? dir = null;
			int dx = 0, dy = 0;
			foreach ((string a, int x, int y) in dirs)
			{
				if (InputMap.HasAction(a) && Input.IsActionPressed(a)) { (dir, dx, dy) = (a, x, y); break; }
			}
			if (dir == null) heldDir = null;
			else if (dir != heldDir) { heldDir = dir; repeatAt = now + 0.38f; Move(dx, dy); }
			else if (now >= repeatAt) { repeatAt = now + 0.09f; Move(dx, dy); }
			if (sel != null)
			{
				float visH = L.GridBottom - L.GridTop;
				float topY = sel.At.Y - L.CellH * 0.5f - HeaderH;
				if (topY < target) target = Mathf.Max(0f, topY);
				if (sel.At.Y + L.CellH * 0.5f > target + visH) target = Mathf.Min(MaxScroll(), sel.At.Y + L.CellH * 0.5f - visH);
				if (sel.Slot != null) hovered = sel;
				if (Pressed("ui_select") || Pressed("ui_confirm")) Toggle(sel);
			}
		}

		void Wheel(InputEvent ev)
		{
			if (ev is InputEventMouseButton { Pressed: true } mb)
			{
				if (mb.ButtonIndex == MouseButton.WheelUp) target -= L.CellH * 0.6f;
				else if (mb.ButtonIndex == MouseButton.WheelDown) target += L.CellH * 0.6f;
			}
		}

		root.GuiInput += ev => ModEntry.Safe("pile input", () =>
		{
			if (focused != null) return;
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

		SceneTree tree = screen.GetTree();
		bool needsRefill = false;
		void OnChanged() => needsRefill = true;

		void FallBack(string where, Exception error)
		{
			tree.ProcessFrame -= Tick;
			pile.ContentsChanged -= OnChanged;
			ModEntry.Safe("pile input cleanup", () => BindBack(false));
			Rollback(screen);
			if (exiting) Finalize(screen);
			Stage.Report($"pile {where} ({pile.Cards.Count} cards in pile, {items.Count} slots, {pending.Count} pending); using vanilla grid", error);
			ModEntry.Safe("pile card cleanup", () => { foreach (Item it in items) ReleaseCard(it); });
		}

		modeSwitch.Changed = order =>
		{
			try { Switch(order); }
			catch (Exception e) { FallBack("sort", e); }
		};

		void Tick()
		{
			if (!GodotObject.IsInstanceValid(root) || !root.IsVisibleInTree()) return;
			try
			{
				if (!exiting && (needsRefill || items.Count != pile.Cards.Count))
				{
					needsRefill = false;
					Refresh(pile.Cards.ToList(), animateSort: false);
				}
				float now = Now();
				float dt = Mathf.Clamp(now - lastNow, 0f, 0.1f);
				lastNow = now;
				draw.Update(now, dt);
				focusP = Mathf.MoveToward(focusP, focusOn ? 1f : 0f, dt / (focusOn ? 0.5f : 0.42f));
				if (focused != null && !focusOn && focusP <= 0f)
				{
					BindBack(false);
					upBtn?.Hide();
					focused = null;
					catcher.Visible = false;
					FreeFlip(flip);
					flip = null;
				}
				float fe = Ease5Ext.InOutCubic(focusP);
				Vector2 focusCenter = L.FocusAt * k;
				if (fx != null) Animate(fx, fe, focusP, focusOn, k, focusCenter);
				float ex = exiting ? Mathf.Clamp((now - exitT0) / L.ExitTime, 0f, 1f) : 0f;
				if (exiting)
				{
					if (screen.GetNodeOrNull<CanvasItem>("BackButton") is CanvasItem backBtn)
						backBtn.Modulate = new Color(1f, 1f, 1f, 1f - Fx.OutCubic(ex));
					if (fx != null) fx.Layer.Modulate = new Color(1f, 1f, 1f, 1f - ex);
					if (flip != null) flip.Node.Modulate = new Color(1f, 1f, 1f, 1f - ex);
					if (ex >= 1f && !finalized)
					{
						finalized = true;
						Finalize(screen);
					}
				}
				// 红底铺开一小会儿之后开始建卡牌节点（每帧 4 张，避免一帧卡顿）
				if (!exiting && now > 0.1f)
				{
					for (int n = 0; n < 4 && pending.TryDequeue(out Item? it); n++) Create(it);
				}
				empty.Visible = items.Count == 0 && now > 0.4f;

				float max = MaxScroll();
				if (!dragging)
				{
					if (target < 0f) target = Mathf.Lerp(target, 0f, 0.2f);
					if (target > max) target = Mathf.Lerp(target, max, 0.2f);
				}
				target = Mathf.Clamp(target, -120f, max + 120f);
				scroll = Mathf.Lerp(scroll, target, 0.2f);

				Vector2 Screen(Vector2 at) => new Vector2(gx + at.X, L.GridTop + at.Y - scroll) * k;
				foreach (Section s in sections)
				{
					Vector2 p = Screen(new Vector2(0f, s.Y + HeaderH * 0.5f));
					float sIn = s.Born < 0f ? 1f : Fx.OutCubic(Mathf.Clamp((now - s.Born) / 0.3f, 0f, 1f));
					float hIn = Ease5.OutExpo(Mathf.Clamp((now - 0.2f) / 0.3f, 0f, 1f));
					s.Head.Position = p + new Vector2(-6f * k - 80f * k * (1f - sIn) - 80f * k * (1f - hIn) - 260f * k * Ease5Ext.InCubic(ex) - 60f * k * fe, 0f);
					s.Head.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp((now - 0.2f) / 0.25f, 0f, 1f) * sIn * (1f - fe) * (1f - ex));
				}
				for (int i = dying.Count - 1; i >= 0; i--)
				{
					Section s = dying[i];
					float d = Mathf.Clamp((now - s.Die) / 0.25f, 0f, 1f);
					if (d >= 1f || !GodotObject.IsInstanceValid(s.Head))
					{
						if (GodotObject.IsInstanceValid(s.Head)) s.Head.QueueFree();
						dying.RemoveAt(i);
						continue;
					}
					Vector2 p = Screen(new Vector2(0f, s.Y + HeaderH * 0.5f));
					s.Head.Position = p + new Vector2(-6f * k + 120f * k * Ease5Ext.InCubic(d), 0f);
					s.Head.Modulate = new Color(1f, 1f, 1f, 1f - d);
				}
				float headIn = Ease5.OutExpo(Mathf.Clamp((now - 0.24f) / 0.3f, 0f, 1f));
				head.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp((now - 0.24f) / 0.2f, 0f, 1f) * (1f - 0.85f * fe) * (1f - ex));
				head.Position = new Vector2(-120f * k * (1f - headIn) - 260f * k * Ease5Ext.InCubic(ex), 0f);
				Nav(now);
				if (focused != null) hovered = null;

				bool any = hovered != null && hovered.Slot != null && GodotObject.IsInstanceValid(hovered.Holder) && hovered.Slot.Visible;
				if (!any) hovered = null;
				foreach (Item it in items)
				{
					if (it.Slot == null) continue;
					if (it.Holder is not { } h || !GodotObject.IsInstanceValid(h) || !GodotObject.IsInstanceValid(h.CardNode))
						throw new InvalidOperationException($"Pile card {it.Card.Id} lost its display node");
					Vector2 cs = h.Hitbox.Size.X > 1f ? h.Hitbox.Size : new Vector2(300f, 422f);
					float baseScale = L.CardW * k / cs.X;
					float move = 0f;
					Vector2 at = it.At;
					if (it.MoveT0 >= 0f)
					{
						float me = Mathf.Clamp((now - it.MoveT0 - it.MoveDelay) / 0.46f, 0f, 1f);
						float mc = Ease5Ext.InOutCubic(me);
						at = it.From.Lerp(it.At, mc);
						move = Mathf.Sin(Mathf.Pi * mc);
						if (me >= 1f) it.MoveT0 = -1f;
					}
					Vector2 p = Screen(at) + new Vector2(0f, -42f * k * move);
					// 入场：从左下（抽牌堆按钮那边）弹进来
					float born = it.Born < 0f ? 1f : Mathf.Clamp((now - it.Born) / 0.38f, 0f, 1f);
					float pop = Ease5.OutBack(born);
					it.Lift = Mathf.MoveToward(it.Lift, it == hovered ? 1f : 0f, 0.12f);
					float lift = Fx.OutCubic(it.Lift);
					it.Dim = Mathf.MoveToward(it.Dim, any && it != hovered ? 0.55f : 1f, 0.07f);
					p += L.EnterOffset * k * (1f - pop);
					p += new Vector2(0f, -14f) * k * lift;
					it.Slot.Visible = true;
					it.Clip!.ClipContents = it != focused;
					float rot = Mathf.DegToRad(-4f) * move;
					float want = baseScale * (0.85f + 0.15f * pop) * (1f + 0.26f * lift) * (1f + 0.07f * move);
					float alpha = Mathf.Clamp(born * 3f, 0f, 1f);
					if (it == focused)
					{
						Vector2 to = focusCenter + new Vector2(0f, Mathf.Sin(now * 1.6f) * 4f * k * fe);
						p = p.Lerp(to, fe) + new Vector2(0f, -170f * k) * Mathf.Sin(Mathf.Pi * fe);
						want = Mathf.Lerp(want, FocusW * k / cs.X, fe);
						rot = Mathf.Lerp(rot, 0f, fe) + Mathf.DegToRad(-8f) * Mathf.Sin(Mathf.Pi * fe);
						alpha = 1f;
						if (!focusOn && fe < 0.5f && focusUp)
						{
							focusUp = false;
							ShowFace(it, focusUp);
						}
						float upE = upT0 >= 0f ? Mathf.Clamp((now - upT0) / UpFlipTime, 0f, 1f) : 0f;
						if (upT0 >= 0f && (upE >= 1f || !focusOn))
						{
							if (!upSwapped)
							{
								focusUp = !focusUp;
								ShowFace(it, focusUp);
							}
							upT0 = -1f;
							upE = 0f;
							upBtn!.Set(focusUp);
						}
						else if (upT0 >= 0f && !upSwapped && upE >= 0.5f)
						{
							upSwapped = true;
							focusUp = !focusUp;
							ShowFace(it, focusUp);
							Sfx.Play(Sfx.Select, 0.35f);
						}
						bool upFlipping = upT0 >= 0f;
						float halfW = FocusW * k * 0.5f, halfH = halfW * cs.Y / cs.X;
						upBtn!.Place(to + new Vector2(halfW + 18f * k, halfH - 34f * k), focusOn && fe > 0.95f ? Mathf.Clamp((fe - 0.95f) / 0.05f, 0f, 1f) : 0f);
						bool flying = flip != null && ((fe > 0.004f && fe < 0.996f) || upFlipping);
						it.Slot.Visible = !flying;
						if (flip != null)
						{
							flip.Node.Visible = flying;
							if (flying)
							{
								flip.Node.Position = p;
								flip.Node.Rotation = rot;
								flip.Node.Scale = Vector2.One * want * (1f + 0.06f * Mathf.Sin(Mathf.Pi * upE));
								Pose(flip, upFlipping ? Mathf.Tau * Ease5Ext.InOutCubic(upE) : Mathf.Tau * fe);
							}
						}
					}
					else if (focused != null)
					{
						float dist = (it.At - focused.At).Length();
						float r = Fx.OutCubic(Mathf.Clamp(focusP * 1.6f - Mathf.Min(0.5f, dist / 1600f), 0f, 1f));
						p += new Vector2(-24f, 80f) * k * r;
						want *= 1f - 0.16f * r;
						rot += Mathf.DegToRad(-5f) * r;
						alpha *= 1f - 0.9f * r;
					}
					if (exiting)
					{
						float r = Mathf.Clamp((ex - Mathf.Min(0.35f, it.Index * 0.012f)) / 0.65f, 0f, 1f);
						float ri = Ease5Ext.InCubic(r);
						p += L.ExitOffset * k * ri;
						want *= 1f - 0.12f * ri;
						rot += Mathf.DegToRad(4f) * ri;
						alpha *= 1f - Fx.OutCubic(r);
					}
					it.Slot.Position = p - clipAt;
					it.Slot.Rotation = rot;
					float own = Mathf.Max(0.01f, h.Scale.X);
					it.Slot.Scale = Vector2.One * (want / own);
					float dd = it == focused ? 1f : it.Dim;
					it.Slot.Modulate = new Color(dd, dd, dd, alpha);
				}
				if (any && hovered?.Clip is Control top && top.GetParent() == cards && top.GetIndex() != cards.GetChildCount() - 1)
					cards.MoveChild(top, cards.GetChildCount() - 1);
				if (any && hovered is { Slot: Control ps, Holder: NGridCardHolder ph } hv && GodotObject.IsInstanceValid(ps) && GodotObject.IsInstanceValid(ph))
				{
					plate ??= NamePlate(root, k);
					plate.Visible = true;
					Vector2 cs = ph.Hitbox.Size.X > 1f ? ph.Hitbox.Size : new Vector2(300f, 422f);
					float half = cs.Y * ps.Scale.Y * ph.Scale.Y * 0.5f;
					plate.Position = ps.Position + clipAt + new Vector2(-20f * k, half + 6f * k);
					((Label)plate.GetChild(plate.GetChildCount() - 1)).Text = hv.Card.Title;
					float a = Fx.OutCubic(hv.Lift);
					plate.Modulate = new Color(1f, 1f, 1f, a);
					plate.Scale = new Vector2(Mathf.Lerp(0.7f, 1f, a), 1f);
				}
				else if (plate != null)
				{
					plate.Visible = false;
				}
			}
			catch (Exception e)
			{
				FallBack("display", e);
			}
		}

		Refresh(pile.Cards.ToList(), animateSort: false);
		pile.ContentsChanged += OnChanged;
		tree.ProcessFrame += Tick;
		ulong sid = screen.GetInstanceId();
		Exits[sid] = () =>
		{
			if (exiting) return;
			exiting = true;
			exitT0 = Now();
			draw.BeginExit(exitT0);
			// 退场期间继续动画，只在这个即将销毁的界面上禁用鼠标（holder 会回对象池，不改它们的 MouseFilter）
			screen.ProcessMode = Node.ProcessModeEnum.Always;
			screen.MouseBehaviorRecursive = Control.MouseBehaviorRecursiveEnum.Disabled;
			catcher.Visible = false;
			if (plate != null) plate.Visible = false;
			hovered = null;
			BindBack(false);
			upBtn?.Hide();
			// 保险：真实时间 1s 后无论如何关掉
			tree.CreateTimer(1.0, processAlways: true, processInPhysics: false, ignoreTimeScale: true).Timeout += () =>
			{
				if (!finalized)
				{
					finalized = true;
					Finalize(screen);
				}
			};
		};
		root.TreeExiting += () =>
		{
			Exits.Remove(sid);
			tree.ProcessFrame -= Tick;
			pile.ContentsChanged -= OnChanged;
			ModEntry.Safe("pile input cleanup", () => BindBack(false));
			ModEntry.Safe("pile flip cleanup", () => FreeFlip(flip));
			flip = null;
		};
		// 建界面这一帧要几十毫秒：延到下一帧再播，声音不抢在画面前面
		Sfx.Play("camp_open", 0.5f, 0.001f);
		PileDraw.ScheduleSlamSounds();               // camp_open 的「啪」、重击、拳击声都落在手掌砸中那一刻（PileDraw.SlamAt）
	}


	// ── 点击特写：压暗 + 穿过卡牌的红色斜带（黑细条）+ 左侧说明 ──
	private sealed class Focus
	{
		public Node2D Layer = null!;
		public ColorRect Dim = null!;
		public Node2D Band = null!;
		public Node2D Info = null!;
		public Label Name = null!;
		public Node2D Kind = null!;
		public Node2D Copies = null!;
		public float K;
		public Vector2 At;
	}

	/// <summary>特写：压暗 + 穿过卡牌的红色斜带 + 说明（牌名用游戏字体——是游戏实时给的；类型、费用、张数、提示都是 P5 字图）。</summary>
	private static Focus FocusLayer(Control parent, Vector2 view, float k, Vector2 center, Vector2 infoAt)
	{
		Focus f = new() { Layer = new Node2D { Visible = false }, K = k, At = infoAt };
		parent.AddChild(f.Layer);
		f.Dim = new ColorRect { Size = view, Color = new Color(0f, 0f, 0f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		f.Layer.AddChild(f.Dim);
		Vector2 c = center * k;
		f.Band = new Node2D { Position = c, Rotation = Mathf.DegToRad(-14f) };
		f.Layer.AddChild(f.Band);
		float len = 2600f * k, h = 250f * k;
		Fx.Poly(f.Band, new[] { new Vector2(-len, -h / 2), new Vector2(len, -h / 2), new Vector2(len, h / 2), new Vector2(-len, h / 2) }, new Color(0.78f, 0.05f, 0.08f, 0.95f));
		Fx.Poly(f.Band, new[] { new Vector2(-len, h / 2 + 10f * k), new Vector2(len, h / 2 + 10f * k), new Vector2(len, h / 2 + 24f * k), new Vector2(-len, h / 2 + 24f * k) }, Ink);
		Fx.Poly(f.Band, new[] { new Vector2(-len, -h / 2 - 18f * k), new Vector2(len, -h / 2 - 18f * k), new Vector2(len, -h / 2 - 10f * k), new Vector2(-len, -h / 2 - 10f * k) }, Paper);
		f.Info = new Node2D();
		f.Layer.AddChild(f.Info);
		float ix = infoAt.X, iy = infoAt.Y;
		f.Name = Text(f.Info, "", Cjk, (int)(54f * k), Paper, new Vector2(ix, iy) * k, (int)(7f * k), Ink);
		Fx.Poly(f.Info, new[] { new Vector2(ix, iy + 84f) * k, new Vector2(ix + 360f, iy + 78f) * k, new Vector2(ix + 356f, iy + 90f) * k, new Vector2(ix - 4f, iy + 94f) * k }, Red);
		f.Kind = new Node2D();
		f.Info.AddChild(f.Kind);
		f.Copies = new Node2D();
		f.Info.AddChild(f.Copies);
		Node2D back = new();
		f.Info.AddChild(back);
		PileText.Word(back, "back", new Vector2(ix + 2f, iy + 214f) * k, 26f * k, new Color(1f, 1f, 1f, 0.85f), true);
		return f;
	}

	private static void Fill(Focus f, CardModel card, CardPile pile)
	{
		f.Name.Text = card.Title;
		float k = f.K;
		int cost = Cost(card);
		foreach (Node n in f.Kind.GetChildren()) n.QueueFree();
		foreach (Node n in f.Copies.GetChildren()) n.QueueFree();
		Vector2 at = f.At;
		// 类型（中文版用中文字图）· COST n
		float w = PileText.Word(f.Kind, TypeKey(card.Type), new Vector2(at.X, at.Y + 104f) * k, 44f * k, Paper, true);
		float x = at.X + w / k + 26f;
		x += PileText.Word(f.Kind, "cost", new Vector2(x, at.Y + 104f) * k, 44f * k, Red, true) / k + 12f;
		if (cost >= 0) PileText.Number(f.Kind, cost.ToString(), new Vector2(x, at.Y + 108f) * k, 40f * k, Paper, true);
		int copies = pile.Cards.Count(c => c.Id.Entry == card.Id.Entry && c.IsUpgraded == card.IsUpgraded);
		PileText.Row(f.Copies, new Vector2(at.X, at.Y + 160f) * k, 38f * k, Paper, true, 12f * k, "inpile", "×" + copies);
	}

	private static void Animate(Focus f, float e, float p, bool on, float k, Vector2 center)
	{
		f.Layer.Visible = e > 0.001f;
		if (!f.Layer.Visible) return;
		f.Dim.Color = new Color(0f, 0f, 0f, 0.62f * e);
		f.Band.Position = center + new Vector2(-1f, 0.25f).Normalized() * 2400f * k * (1f - Ease5.OutExpo(Mathf.Clamp(p * 1.4f - 0.1f, 0f, 1f)));
		f.Band.Modulate = new Color(1f, 1f, 1f, e);
		float ia = on ? Mathf.Clamp((p - 0.45f) / 0.4f, 0f, 1f) : e;
		f.Info.Modulate = new Color(1f, 1f, 1f, ia);
		f.Info.Position = new Vector2(90f * k * (1f - Fx.OutCubic(ia)), 0f);
	}

	/// <summary>排列切换（左上）：白 / 黑两半剪报纸片，红色滑块滑到选中的一侧；点一下切换。</summary>
	private sealed class ModeSwitch
	{
		private const float W = 300f, H = 46f;
		public Action<bool>? Changed;
		private Node2D root = null!;
		private Polygon2D knob = null!;
		private Node2D left = null!, right = null!;
		private float k;
		private bool order;

		public static ModeSwitch Build(Node2D parent, Vector2 at, float k, bool order)
		{
			ModeSwitch m = new() { k = k, order = order };
			m.root = new Node2D { Position = at, Rotation = Mathf.DegToRad(-2f) };
			parent.AddChild(m.root);
			Fx.Poly(m.root, new[] { new Vector2(0f, 4f), new Vector2(W, 0f), new Vector2(W + 4f, H), new Vector2(2f, H + 4f) }.Select(p => p * k).ToArray(), Ink);
			m.knob = new Polygon2D { Color = Red, Polygon = new[] { new Vector2(4f, 6f), new Vector2(W / 2f, 3f), new Vector2(W / 2f + 2f, H - 2f), new Vector2(6f, H) }.Select(p => p * k).ToArray() };
			m.root.AddChild(m.knob);
			// 文字：P5 字图（原版顺序 / 按类型），各自在半边里居中
			m.left = new Node2D();
			m.right = new Node2D();
			m.root.AddChild(m.left);
			m.root.AddChild(m.right);
			float th = 30f * k;
			foreach ((Node2D n, string key, float cx) in new[] { (m.left, "order", W * 0.25f), (m.right, "bytype", W * 0.75f) })
			{
				float tw = PileText.RowWidth(th, 0f, PileText.Pick(key));
				PileText.Word(n, key, new Vector2(cx * k - tw * 0.5f, (H * k - th) * 0.5f + 2f * k), th, Colors.White);
			}
			Control hit = new() { Size = new Vector2(W, H) * k, MouseFilter = Control.MouseFilterEnum.Stop, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
			m.root.AddChild(hit);
			hit.MouseEntered += () => m.root.Modulate = new Color(1.12f, 1.12f, 1.12f);
			hit.MouseExited += () => m.root.Modulate = Colors.White;
			hit.GuiInput += ev => ModEntry.Safe("pile mode switch", () =>
			{
				if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
				{
					hit.AcceptEvent();
					bool want = mb.Position.X < W * k / 2f;
					m.Changed?.Invoke(want == m.order ? !m.order : want);
				}
			});
			m.Paint(1f);
			return m;
		}

		public void Set(bool toOrder)
		{
			order = toOrder;
			Fx.Animate(knob, 0, 0.32, t => Paint(Ease5.OutExpo(t)));
			Sfx.Play(Sfx.Cursor, 0.35f);
		}

		private void Paint(float t)
		{
			float from = order ? W / 2f : 0f, to = order ? 0f : W / 2f;
			knob.Position = new Vector2(Mathf.Lerp(from, to, t) * k, 0f);
			left.Modulate = order ? Paper : new Color(0.55f, 0.55f, 0.55f);
			right.Modulate = order ? new Color(0.55f, 0.55f, 0.55f) : Paper;
		}
	}

	/// <summary>段标题：黑色斜剪报纸片 + 白色英文类型字图 + 红色中文字图 + 原版数字 ×张数。</summary>
	private static Node2D SectionHead(Node2D parent, string en, string zh, int count, float k)
	{
		Node2D n = new() { Modulate = new Color(1f, 1f, 1f, 0f), Rotation = Mathf.DegToRad(-1.5f) };
		parent.AddChild(n);
		float h = 54f;
		Fx.Poly(n, new[] { new Vector2(0f, -h / 2 + 4f), new Vector2(330f, -h / 2), new Vector2(338f, h / 2 - 2f), new Vector2(6f, h / 2 + 2f) }.Select(p => p * k).ToArray(), Ink);
		float w = PileText.Word(n, en.Length > 0 && Fx.Tex("pile_t_" + en) != null ? en : "draw", new Vector2(16f, -21f) * k, 40f * k, Paper) / k;
		float x = 16f + w + 10f;
		if (zh.Length > 0) x += PileText.Word(n, zh, new Vector2(x, -15f) * k, 28f * k, Red) / k + 10f;
		PileText.Number(n, "×" + count, new Vector2(Mathf.Max(x, 236f), -17f) * k, 34f * k, Paper);
		return n;
	}

	// ── 特写的真实翻转（P3 同款）：离屏渲染这张牌（1.5 倍）作正面，预告信卡背作背面，贴在透视竖条网格上 ──
	private const int FlipStrips = 14;
	private const float FlipRes = 1.5f;
	private const float FlipPadding = 48f;

	private sealed class Flip
	{
		public SubViewport Vp = null!;
		public NCard? Card;
		public Node2D Node = null!;
		public Polygon2D Mesh = null!;
		public Texture2D? Back;
		public Vector2 Size;
	}

	private static Flip? MakeFlip(Control root, Control front, CardModel model, PileType pileType, NGridCardHolder? holder)
	{
		Vector2 size = holder != null && holder.Hitbox.Size.X > 1f ? holder.Hitbox.Size : new Vector2(300f, 422f);
		NCard? card = NCard.Create(model);
		if (card == null) return null;
		Vector2 captureSize = size + Vector2.One * (FlipPadding * 2f);
		SubViewport vp = new() { Size = (Vector2I)(captureSize * FlipRes).Ceil(), TransparentBg = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
		root.AddChild(vp);
		vp.AddChild(card);
		card.Position = captureSize * FlipRes / 2f;
		card.Scale = Vector2.One * FlipRes;
		card.UpdateVisuals(pileType, CardPreviewMode.Normal);
		Flip f = new() { Vp = vp, Card = card, Size = size, Back = Fx.Tex("calling_card_back") };
		f.Node = new Node2D { Visible = false };
		front.AddChild(f.Node);
		f.Mesh = new Polygon2D { TextureFilter = CanvasItem.TextureFilterEnum.Linear };
		f.Node.AddChild(f.Mesh);
		Godot.Collections.Array polys = new();
		for (int i = 0; i < FlipStrips; i++) polys.Add(new[] { i, i + 1, FlipStrips + 1 + i + 1, FlipStrips + 1 + i });
		f.Mesh.Polygons = polys;
		return f;
	}

	private static void Pose(Flip f, float theta)
	{
		float cam = f.Size.X * 2.6f;
		float c = Mathf.Cos(theta), sn = Mathf.Sin(theta);
		bool frontFace = c >= 0f;
		float pad = frontFace ? FlipPadding * 2f : 0f;
		float w = f.Size.X + pad, h = f.Size.Y + pad;
		Texture2D tex = (frontFace ? f.Vp.GetTexture() : f.Back) ?? f.Vp.GetTexture();
		Vector2 ts = tex.GetSize();
		int n = FlipStrips + 1;
		Vector2[] pts = new Vector2[n * 2];
		Vector2[] uv = new Vector2[n * 2];
		for (int i = 0; i < n; i++)
		{
			float u = (float)i / FlipStrips;
			float x = (u - 0.5f) * w;
			float persp = cam / (cam + x * sn);
			float px = x * c * persp;
			pts[i] = new Vector2(px, -h / 2f * persp);
			pts[n + i] = new Vector2(px, h / 2f * persp);
			float tu = (frontFace ? u : 1f - u) * ts.X;
			uv[i] = new Vector2(tu, 0f);
			uv[n + i] = new Vector2(tu, ts.Y);
		}
		f.Mesh.Texture = tex;
		f.Mesh.Polygon = pts;
		f.Mesh.UV = uv;
		float light = 0.55f + 0.45f * Mathf.Abs(c);
		f.Mesh.Color = new Color(light, light, light);
	}

	private static void FreeFlip(Flip? f)
	{
		if (f == null) return;
		if (f.Card != null && GodotObject.IsInstanceValid(f.Card)) f.Card.QueueFreeSafely();
		if (GodotObject.IsInstanceValid(f.Node)) f.Node.QueueFree();
		if (GodotObject.IsInstanceValid(f.Vp)) f.Vp.QueueFree();
	}

	/// <summary>名牌：红色斜剪报纸片 + 黑色投影纸 + 白色粗体牌名（黑描边）。最后一个子节点是文字。</summary>
	private static Node2D NamePlate(Control root, float k)
	{
		Node2D n = new() { Visible = false, Rotation = Mathf.DegToRad(-3f) };
		root.AddChild(n);
		float w = 280f, h = 52f;
		Fx.Poly(n, new[] { new Vector2(8f, 10f), new Vector2(w + 12f, 4f), new Vector2(w + 18f, h + 6f), new Vector2(12f, h + 10f) }.Select(p => p * k).ToArray(), Ink);
		Fx.Poly(n, new[] { new Vector2(0f, 6f), new Vector2(w, 0f), new Vector2(w + 6f, h), new Vector2(4f, h + 4f) }.Select(p => p * k).ToArray(), Red);
		Label l = Text(n, "", Cjk, (int)(30f * k), Paper, new Vector2(18f, 6f) * k, (int)(4f * k), Ink);
		l.ClipText = true;
		l.Size = new Vector2((w - 30f) * k, (h - 8f) * k);
		return n;
	}

	private static Label Text(Node parent, string text, Font font, int size, Color color, Vector2 at, int outline, Color outlineColor)
	{
		Label l = new() { Text = text, Position = at, MouseFilter = Control.MouseFilterEnum.Ignore };
		l.AddThemeFontOverride("font", font);
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

	/// <summary>特写里的「查看升级」按钮：黑色斜剪报条 + 红色底边；悬停变白底黑字。</summary>
	private sealed class UpgradeButton
	{
		internal Node2D Root = null!;
		internal Control Hit = null!;
		private Polygon2D _sel = null!;
		private Node2D _text = null!;
		private float _k;
		private bool _mouse, _enabled, _up, _multi;
		private int _level;

		internal static UpgradeButton Build(Control parent, float k)
		{
			UpgradeButton b = new();
			float w = 214f * k, h = 38f * k, lean = 9f * k;
			b.Root = new Node2D { Name = "P5UpgradeButton", Visible = false };
			parent.AddChild(b.Root);
			Fx.Poly(b.Root, new[] { new Vector2(lean + 5f * k, 5f * k), new Vector2(w + lean + 5f * k, 5f * k), new Vector2(w + 5f * k, h + 5f * k), new Vector2(5f * k, h + 5f * k) }, new Color(0f, 0f, 0f, 0.5f));
			Fx.Poly(b.Root, new[] { new Vector2(lean, 0f), new Vector2(w + lean, 0f), new Vector2(w, h), new Vector2(0f, h) }, Ink);
			b._sel = Fx.Poly(b.Root, new[] { new Vector2(lean, 0f), new Vector2(w + lean, 0f), new Vector2(w, h), new Vector2(0f, h) }, Paper);
			Fx.Poly(b.Root, new[] { new Vector2(0f, h - 4f * k), new Vector2(w, h - 4f * k), new Vector2(w, h), new Vector2(0f, h) }, Red);
			b._k = k;
			b._text = new Node2D();
			b.Root.AddChild(b._text);
			b.Hit = new Control { Size = new Vector2(w + lean, h), MouseFilter = Control.MouseFilterEnum.Stop, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
			b.Root.AddChild(b.Hit);
			b.Hit.MouseEntered += () => { b._mouse = true; b.Refresh(); };
			b.Hit.MouseExited += () => { b._mouse = false; b.Refresh(); };
			return b;
		}

		internal void Show(bool upgradable, bool upgraded, bool showingUp, int nextLevel, bool multi)
		{
			if (!upgradable && !upgraded)
			{
				Hide();
				return;
			}
			_enabled = upgradable;
			_up = showingUp;
			_level = nextLevel;
			_multi = multi;
			Root.Visible = true;
			Root.Modulate = new Color(1f, 1f, 1f, 0f);
			Refresh();
		}

		internal void Hide()
		{
			if (!GodotObject.IsInstanceValid(Root)) return;
			Root.Visible = false;
			_enabled = false;
			_mouse = false;
		}

		internal void Set(bool showingUp)
		{
			_up = showingUp;
			Refresh();
		}

		internal void Place(Vector2 at, float alpha)
		{
			if (!Root.Visible) return;
			Root.Position = at;
			Root.Modulate = new Color(1f, 1f, 1f, alpha * (_enabled ? 1f : 0.55f));
			Hit.MouseFilter = alpha > 0.9f && _enabled ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore;
		}

		private void Refresh()
		{
			if (!GodotObject.IsInstanceValid(Root)) return;
			bool lit = _mouse && _enabled;
			_sel.Visible = lit;
			foreach (Node c in _text.GetChildren()) c.QueueFree();
			string key = !_enabled ? "upgraded" : _up ? "upbase" : "upgrade";
			float th = 26f * _k;
			float x = PileText.Word(_text, key, new Vector2(18f * _k, 6f * _k), th, Colors.White);
			if (_enabled && !_up && _multi) PileText.Number(_text, _level.ToString(), new Vector2(18f * _k + x + 8f * _k, 8f * _k), th * 0.9f, Colors.White);
			_text.Modulate = lit ? Ink : Paper;
		}
	}

	private static int Cost(CardModel c)
	{
		try { return c.EnergyCost.Canonical; }
		catch (Exception) { return 99; }
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

	private static string TypeKey(CardType t) => t switch
	{
		CardType.Attack => "attack",
		CardType.Skill => "skill",
		CardType.Power => "power",
		CardType.Status => "status",
		CardType.Curse => "curse",
		_ => "skill",
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

	/// <summary>只清理经过这个界面的对象池卡牌控件的按下 / 悬停 / 焦点状态（P3 PooledCardInput 同款）。</summary>
	private static class PooledCardInput
	{
		private const string Tracked = "p5_input_cleanup";

		public static void Track(NGridCardHolder holder)
		{
			if (holder.HasMeta(Tracked)) return;
			holder.SetMeta(Tracked, true);
			void Release()
			{
				holder.TreeExiting -= Release;
				holder.RemoveMeta(Tracked);
				ModEntry.Safe("pooled card input cleanup", () =>
				{
					var tweenField = AccessTools.Field(typeof(NCardHolder), "_hoverTween");
					if (tweenField?.GetValue(holder) is Tween tween && GodotObject.IsInstanceValid(tween)) tween.Kill();
					tweenField?.SetValue(holder, null);
					AccessTools.Field(typeof(NCardHolder), "_currentPressedAction")?.SetValue(holder, null);
					ClearFlags(holder, "_isHovered", "_isFocused");
					if (!holder.IsNodeReady()) return;
					var hitbox = holder.Hitbox;
					if (hitbox == null || !GodotObject.IsInstanceValid(hitbox)) return;
					ClearFlags(hitbox, "_isHovered", "_isControllerFocused", "_isPressed");
					var focusedProp = AccessTools.Property(hitbox.GetType(), "IsFocused");
					if (focusedProp?.SetMethod != null) focusedProp.SetValue(hitbox, false);
				});
			}
			holder.TreeExiting += Release;
		}

		private static void ClearFlags(object obj, params string[] names)
		{
			foreach (string name in names)
			{
				var field = AccessTools.Field(obj.GetType(), name);
				if (field?.FieldType == typeof(bool)) field.SetValue(obj, false);
			}
		}
	}

	/// <summary>NHotkeyManager 的 Push / Remove 在 0.113 多了优先级参数：运行时选签名（P3 Compat.HotkeyBindings 同款）。</summary>
	private static class HotkeyBindings
	{
		private static readonly Type[] Binding = { typeof(string), typeof(Action) };
		private static readonly Type[] SequencedBinding = { typeof(string), typeof(Action), typeof(long) };
		private static readonly MethodInfo? PushPressed = FindPush("PushHotkeyPressedBinding");
		private static readonly MethodInfo? PushReleased = FindPush("PushHotkeyReleasedBinding");
		private static readonly MethodInfo? RemovePressed = typeof(NHotkeyManager).GetMethod("RemoveHotkeyPressedBinding", Binding);
		private static readonly MethodInfo? RemoveReleased = typeof(NHotkeyManager).GetMethod("RemoveHotkeyReleasedBinding", Binding);

		private static MethodInfo? FindPush(string name) =>
			typeof(NHotkeyManager).GetMethod(name, SequencedBinding) ?? typeof(NHotkeyManager).GetMethod(name, Binding);

		private static object[] Args(MethodInfo method, string key, Action action) =>
			method.GetParameters().Length == SequencedBinding.Length ? new object[] { key, action, 0L } : new object[] { key, action };

		internal static void Set(NHotkeyManager manager, string key, Action pressed, Action released, bool bind)
		{
			if (PushPressed == null || PushReleased == null || RemovePressed == null || RemoveReleased == null) return;
			if (bind)
			{
				PushPressed.Invoke(manager, Args(PushPressed, key, pressed));
				PushReleased.Invoke(manager, Args(PushReleased, key, released));
			}
			else
			{
				RemovePressed.Invoke(manager, new object[] { key, pressed });
				RemoveReleased.Invoke(manager, new object[] { key, released });
			}
		}
	}
}
