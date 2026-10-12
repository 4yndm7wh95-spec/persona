using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Runs;

namespace P3TestMod;

/// <summary>
/// 测试面板（F8 开关，或点左上角的"测试"按钮）：
/// · 卡牌按原版卡面显示（原版牌组 / 图鉴用的同一个卡牌格子 NGridCardHolder），点一下加入手牌；
/// · 筛选：储君 / 无色 / 全部，按名称搜索，"升级版"开关（卡面预览也跟着变）；每页 12 张；
/// · 按钮：击杀假人（看胜利特效）、让我受击（看受击动画）、清空手牌。
/// </summary>
public static class Panel
{
	private const int Columns = 6, PerPage = 12;
	private const float CardScale = 0.6f;
	private static readonly Vector2 CardSize = new(300f, 422f);       // NCard.defaultSize

	private static CanvasLayer? _layer;
	private static Control? _window;
	private static GridContainer? _grid;
	private static Label? _status, _pageLabel;
	private static LineEdit? _search;
	private static string _filter = "regent";
	private static bool _upgraded;
	private static int _page;
	private static List<CardModel> _cards = new();

	public static void Toggle()
	{
		if (_layer == null || !GodotObject.IsInstanceValid(_layer))
		{
			Build();
		}
		if (_window == null) return;
		_window.Visible = !_window.Visible;
		if (_window.Visible)
		{
			Refresh();
		}
	}

	private static void Build()
	{
		if (Engine.GetMainLoop() is not SceneTree tree)
		{
			return;
		}
		_layer = new CanvasLayer { Layer = 95, Name = "P3TestPanel" };
		tree.Root.AddChild(_layer);
		// 面板自己的主题：字体用游戏自带的 Noto Sans CJK（Godot 默认字体没有中文）
		Theme theme = new() { DefaultFontSize = 20 };
		if (ResourceLoader.Load("res://fonts/jpn/NotoSansCJKjp-Medium.otf") is Font cjk)
		{
			theme.DefaultFont = cjk;
		}

		// 左上角常驻的小按钮（F8 也可以）
		Button open = new() { Text = "测试 (F8)", Position = new Vector2(8f, 140f), FocusMode = Control.FocusModeEnum.None, Theme = theme };
		open.Pressed += Toggle;
		_layer.AddChild(open);

		Vector2 view = tree.Root.GetVisibleRect().Size;
		float w = Columns * CardSize.X * CardScale + 80f, h = 2 * CardSize.Y * CardScale + 230f;
		PanelContainer win = new() { Position = new Vector2((view.X - w) * 0.5f, 40f), CustomMinimumSize = new Vector2(w, h), Visible = false, Theme = theme };
		win.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.02f, 0.05f, 0.16f, 0.94f), BorderColor = new Color(0.22f, 0.85f, 1f), BorderWidthTop = 2, BorderWidthBottom = 2, BorderWidthLeft = 2, BorderWidthRight = 2, ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 12, ContentMarginBottom = 12 });
		_layer.AddChild(win);
		_window = win;
		VBoxContainer col = new();
		col.AddThemeConstantOverride("separation", 8);
		win.AddChild(col);

		// 第一行：标题 + 筛选 + 搜索 + 升级开关 + 关闭
		HBoxContainer top = new();
		top.AddThemeConstantOverride("separation", 10);
		col.AddChild(top);
		top.AddChild(new Label { Text = "P3 测试面板", CustomMinimumSize = new Vector2(150f, 0f) });
		foreach ((string label, string key) in new[] { ("储君", "regent"), ("无色", "colorless"), ("全部", "all") })
		{
			Button b = new() { Text = label, FocusMode = Control.FocusModeEnum.None };
			b.Pressed += () => { _filter = key; _page = 0; Refresh(); };
			top.AddChild(b);
		}
		_search = new LineEdit { PlaceholderText = "搜索卡名…", CustomMinimumSize = new Vector2(220f, 0f) };
		_search.TextChanged += _ => { _page = 0; Refresh(); };
		top.AddChild(_search);
		CheckBox up = new() { Text = "升级版", FocusMode = Control.FocusModeEnum.None };
		up.Toggled += on => { _upgraded = on; Refresh(); };
		top.AddChild(up);
		Button close = new() { Text = "关闭", FocusMode = Control.FocusModeEnum.None };
		close.Pressed += Toggle;
		top.AddChild(close);

		// 第二行：操作按钮
		HBoxContainer actions = new();
		actions.AddThemeConstantOverride("separation", 10);
		col.AddChild(actions);
		void AddButton(string text, Func<string> act)
		{
			Button b = new() { Text = text, FocusMode = Control.FocusModeEnum.None };
			b.Pressed += () => Say(act());
			actions.AddChild(b);
		}
		AddButton("击杀假人", Runtime.KillDummy);
		AddButton("让我受击", () => Runtime.HurtMe(3));
		AddButton("清空手牌", ClearHand);
		actions.AddChild(new Label { Text = "  能量 / 辉星无限；假人打不死；所有房间都是假人战" });

		// 卡牌网格（每页 12 张）
		_grid = new GridContainer { Columns = Columns };
		_grid.AddThemeConstantOverride("h_separation", 8);
		_grid.AddThemeConstantOverride("v_separation", 8);
		col.AddChild(_grid);

		// 翻页 + 状态
		HBoxContainer bottom = new();
		bottom.AddThemeConstantOverride("separation", 10);
		col.AddChild(bottom);
		Button prev = new() { Text = "◀ 上一页", FocusMode = Control.FocusModeEnum.None };
		prev.Pressed += () => { _page = Math.Max(0, _page - 1); Refresh(); };
		bottom.AddChild(prev);
		_pageLabel = new Label { CustomMinimumSize = new Vector2(120f, 0f), HorizontalAlignment = HorizontalAlignment.Center };
		bottom.AddChild(_pageLabel);
		Button next = new() { Text = "下一页 ▶", FocusMode = Control.FocusModeEnum.None };
		next.Pressed += () => { _page++; Refresh(); };
		bottom.AddChild(next);
		_status = new Label { Text = "点卡牌加入手牌" };
		bottom.AddChild(_status);
	}

	private static void Say(string text)
	{
		if (_status != null && GodotObject.IsInstanceValid(_status))
		{
			_status.Text = text;
		}
	}

	private static IEnumerable<CardModel> Filtered()
	{
		string q = _search?.Text?.Trim() ?? "";
		foreach (CardModel c in ModelDb.AllCards)
		{
			if (!c.ShouldShowInCardLibrary) continue;
			string pool = c.Pool.Title.ToLowerInvariant();
			if (pool == "test") continue;                                          // 原版测试用卡池
			if (_filter != "all" && pool != _filter) continue;
			if (q.Length > 0)
			{
				string title = c.TitleLocString.GetRawText();
				if (!title.Contains(q, StringComparison.OrdinalIgnoreCase) && !c.Id.Entry.Contains(q, StringComparison.OrdinalIgnoreCase)) continue;
			}
			yield return c;
		}
	}

	private static void Refresh()
	{
		if (_grid == null || !GodotObject.IsInstanceValid(_grid))
		{
			return;
		}
		foreach (Node child in _grid.GetChildren())
		{
			_grid.RemoveChild(child);
			child.QueueFreeSafely();
		}
		_cards = Filtered().ToList();
		int pages = Math.Max(1, (_cards.Count + PerPage - 1) / PerPage);
		_page = Math.Clamp(_page, 0, pages - 1);
		if (_pageLabel != null) _pageLabel.Text = $"{_page + 1} / {pages}（{_cards.Count} 张）";
		foreach (CardModel model in _cards.Skip(_page * PerPage).Take(PerPage))
		{
			Control cell = new() { CustomMinimumSize = CardSize * CardScale, MouseFilter = Control.MouseFilterEnum.Pass };
			_grid.AddChild(cell);
			NCard? card = NCard.Create(model);
			if (card == null) continue;
			NGridCardHolder? holder = NGridCardHolder.Create(card);
			if (holder == null) continue;
			holder.Scale = Vector2.One * CardScale;
			holder.Position = CardSize * CardScale * 0.5f;
			holder.MouseFilter = Control.MouseFilterEnum.Pass;
			cell.AddChild(holder);
			card.UpdateVisuals(PileType.Deck, CardPreviewMode.Normal);
			if (model.IsUpgradable)
			{
				holder.SetIsPreviewingUpgrade(_upgraded);
			}
			CardModel picked = model;
			holder.Connect(NCardHolder.SignalName.Pressed, Callable.From<NCardHolder>(_ => Say(AddToHand(picked))));
		}
	}

	private static string AddToHand(CardModel model)
	{
		CombatState? state = Runtime.Combat;
		Player? me = Runtime.Me;
		if (state == null || me == null)
		{
			return "不在战斗中：先进一场战斗";
		}
		CardPile hand = PileType.Hand.GetPile(me);
		if (hand.Cards.Count >= CardPile.MaxCardsInHand)
		{
			return "手牌满了（10 张）：先出牌或点\"清空手牌\"";
		}
		CardModel card = ((ICardScope)state).CreateCard(model, me);           // 和原版 card 命令一样走 ICardScope
		if (_upgraded && card.IsUpgradable)
		{
			CardCmd.Upgrade(card);                // 牌还不在任何牌堆里：只改数值，不弹升级预览
		}
		TaskHelper.RunSafely(CardPileCmd.Add(card, PileType.Hand));
		return $"已加入手牌：{model.TitleLocString.GetRawText()}{(_upgraded && card.IsUpgraded ? "+" : "")}";
	}

	private static string ClearHand()
	{
		if (Runtime.Me is not Player me) return "不在战斗中";
		List<CardModel> cards = PileType.Hand.GetPile(me).Cards.ToList();
		if (cards.Count == 0) return "手牌已经是空的";
		foreach (CardModel c in cards)
		{
			TaskHelper.RunSafely(CardPileCmd.Add(c, PileType.Discard));
		}
		return $"已把 {cards.Count} 张手牌移到弃牌堆";
	}
}
