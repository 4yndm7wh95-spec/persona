using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Debug;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;
using Fail = P3TestMod.Bridge.Fail;

namespace P3TestMod;

/// <summary>
/// 导演接口的全部命令（都在主线程执行）。接口一览见 testmod/README.md。
/// 出牌默认 style=drag：真实鼠标悬停 → 按下 → 拖到目标 → 松手，和玩家手动出牌走同一套流程（抬牌、瞄准箭头、准星全都有）；
/// 拖拽失败时自动退回 instant（直接打出），回包里 style_used 会写明。
/// </summary>
public static class Director
{
	public static async Task<object?> Dispatch(string path, JsonObject a)
	{
		switch (path)
		{
			case "":
			case "/ping": return new { ok = true, mod = "P3TestMod", version = TestEntry.Version };
			case "/state": return State();
			case "/cards": return Cards(a);
			case "/hand/add": return await HandAdd(a);
			case "/hand/set": return await HandSet(a);
			case "/hand/clear": return await HandClear();
			case "/hand/exhaust": return await HandExhaust();
			case "/play": return await Play(a);
			case "/end_turn": return await EndTurn(a);
			case "/wait": return await Wait(a);
			case "/kill": return new { ok = true, message = Runtime.KillDummy() };
			case "/hurt": return new { ok = true, message = Runtime.HurtMe(Bridge.Int(a, "amount", 3)) };
			case "/dummy": return new { ok = true, message = Runtime.ReplaceWithDummy() };
			case "/mouse/move": return await MouseMove(a);
			case "/mouse/click": return await MouseClick(a);
			case "/mouse/park": return await MousePark(a);
			case "/mouse/hover_card": return await HoverCard(a);
			case "/ui": return Chrome(a);
			case "/ui/find": return new { ok = true, nodes = Find(a).Select(Describe).ToList() };
			case "/ui/click": return await ClickNode(a);
			case "/map/points": return new { ok = true, points = MapPoints().Select(Describe).ToList() };
			case "/console": return await Console(a);
			default: throw new Fail($"未知接口 {path}");
		}
	}

	// ── 状态 ──

	private static object State()
	{
		CombatState? s = Runtime.Combat;
		Player? me = Runtime.Me;
		Vector2 view = Mouse.ViewSize;
		object? combat = null;
		if (s != null && me != null)
		{
			combat = new
			{
				round = s.RoundNumber,
				ready = Ready(),
				energy = me.PlayerCombatState?.Energy,
				stars = me.PlayerCombatState?.Stars,
				hp = me.Creature.CurrentHp,
				max_hp = me.Creature.MaxHp,
				hand = Holders().Select((h, i) => new
				{
					index = i,
					id = h.CardModel!.Id.Entry,
					title = h.CardModel!.TitleLocString.GetRawText(),
					target = h.CardModel!.TargetType.ToString(),
					x = Mouse.Center(h.Hitbox).X,
					y = Mouse.Center(h.Hitbox).Y,
				}).ToList(),
				enemies = s.Enemies.Select((e, i) => new
				{
					index = i,
					name = e.Name,
					hp = e.CurrentHp,
					max_hp = e.MaxHp,
					alive = e.IsAlive,
					x = EnemyPoint(e)?.X,
					y = EnemyPoint(e)?.Y,
				}).ToList(),
			};
		}
		return new
		{
			ok = true,
			screen = combat != null ? "combat" : MapPoints().Any() ? "map" : Find(new JsonObject { ["type"] = nameof(NCharacterSelectButton) }).Any() ? "char_select" : "other",
			viewport = new { w = view.X, h = view.Y },
			mouse = new { x = Mouse.Pos.X, y = Mouse.Pos.Y },
			combat,
		};
	}

	private static object Cards(JsonObject a)
	{
		string pool = (Bridge.Str(a, "pool") ?? "regent").ToLowerInvariant();
		string q = Bridge.Str(a, "q") ?? "";
		List<object> list = new();
		foreach (CardModel c in ModelDb.AllCards)
		{
			string p = c.Pool.Title.ToLowerInvariant();
			if (p == "test" || (pool != "all" && p != pool)) continue;
			string title = c.TitleLocString.GetRawText();
			if (q.Length > 0 && !title.Contains(q, StringComparison.OrdinalIgnoreCase) && !c.Id.Entry.Contains(q, StringComparison.OrdinalIgnoreCase)) continue;
			list.Add(new { id = c.Id.Entry, title, pool = p, target = c.TargetType.ToString(), cls = c.GetType().Name });
		}
		return new { ok = true, count = list.Count, cards = list };
	}

	// ── 手牌 ──

	private static CardModel FindModel(string key)
	{
		CardModel? m = ModelDb.AllCards.FirstOrDefault(c => c.Id.Entry.Equals(key, StringComparison.OrdinalIgnoreCase) || c.GetType().Name.Equals(key, StringComparison.OrdinalIgnoreCase))
			?? ModelDb.AllCards.FirstOrDefault(c => c.TitleLocString.GetRawText() == key);
		return m ?? throw new Fail($"找不到卡牌 {key}（用 /cards 查 id）");
	}

	private static async Task<object> HandAdd(JsonObject a)
	{
		CombatState s = Runtime.Combat ?? throw new Fail("不在战斗中");
		Player me = Runtime.Me ?? throw new Fail("不在战斗中");
		List<string> ids = Bridge.List(a, "ids");
		if (Bridge.Str(a, "id") is string one) ids.Add(one);
		int count = Math.Max(1, Bridge.Int(a, "count", 1));
		List<string> added = new();
		foreach (string id in ids)
		{
			CardModel model = FindModel(id);
			for (int i = 0; i < count; i++)
			{
				if (PileType.Hand.GetPile(me).Cards.Count >= CardPile.MaxCardsInHand) break;
				CardModel card = ((ICardScope)s).CreateCard(model, me);
				await CardPileCmd.Add(card, PileType.Hand);
				added.Add(model.Id.Entry);
			}
		}
		await Mouse.Sleep(Bridge.Int(a, "settle_ms", 350));
		return new { ok = true, added };
	}

	private static async Task<object> HandSet(JsonObject a)
	{
		await HandClear();
		return await HandAdd(a);
	}

	private static async Task<object> HandClear()
	{
		Player me = Runtime.Me ?? throw new Fail("不在战斗中");
		List<CardModel> cards = PileType.Hand.GetPile(me).Cards.ToList();
		foreach (CardModel c in cards)
		{
			await CardPileCmd.Add(c, PileType.Discard);
		}
		return new { ok = true, cleared = cards.Count };
	}

	private static async Task<object> HandExhaust()
	{
		Player me = Runtime.Me ?? throw new Fail("不在战斗中");
		List<CardModel> cards = PileType.Hand.GetPile(me).Cards.ToList();
		await Runtime.ExhaustAll(cards);
		return new { ok = true, exhausted = cards.Count };
	}

	// ── 出牌 ──

	private static IReadOnlyList<NHandCardHolder> Holders() => (NPlayerHand.Instance?.ActiveHolders ?? Array.Empty<NHandCardHolder>()).Where(h => h.CardModel != null).ToList();

	private static NHandCardHolder FindHolder(JsonObject a)
	{
		IReadOnlyList<NHandCardHolder> hs = Holders();
		if (hs.Count == 0) throw new Fail("手牌是空的");
		string key = Bridge.Str(a, "card") ?? "0";
		if (int.TryParse(key, out int index))
		{
			if (index < 0) index += hs.Count;
			if (index < 0 || index >= hs.Count) throw new Fail($"手牌只有 {hs.Count} 张，没有第 {index} 张");
			return hs[index];
		}
		return hs.FirstOrDefault(h => h.CardModel!.Id.Entry.Equals(key, StringComparison.OrdinalIgnoreCase) || h.CardModel!.GetType().Name.Equals(key, StringComparison.OrdinalIgnoreCase))
			?? hs.FirstOrDefault(h => h.CardModel!.TitleLocString.GetRawText() == key)
			?? throw new Fail($"手牌里没有 {key}");
	}

	private static Vector2? EnemyPoint(Creature e)
	{
		NCreature? node = NCombatRoom.Instance?.GetCreatureNode(e);
		return node != null && GodotObject.IsInstanceValid(node) ? Mouse.Center(node.Hitbox) : null;
	}

	private static bool Ready()
	{
		CombatManager cm = CombatManager.Instance;
		if (!cm.IsInProgress) return false;
		NPlayerHand? hand = NPlayerHand.Instance;
		return !cm.PlayerActionsDisabled && !RunManager.Instance.ActionExecutor.IsRunning && (hand == null || !hand.InCardPlay);
	}

	/// <summary>等到玩家可以操作（连续 3 帧满足才算，避免动作之间的空隙误判）。</summary>
	private static async Task<bool> WaitReady(int timeoutMs)
	{
		ulong end = Time.GetTicksMsec() + (ulong)timeoutMs;
		int stable = 0;
		while (Time.GetTicksMsec() < end)
		{
			stable = Ready() ? stable + 1 : 0;
			if (stable >= 3) return true;
			await Mouse.Frame();
		}
		return false;
	}

	private static async Task<object> Play(JsonObject a)
	{
		if (Runtime.Combat is not CombatState s) throw new Fail("不在战斗中");
		Panel.Hide();
		if (!await WaitReady(Bridge.Int(a, "timeout_ms", 15000))) throw new Fail("等了很久还不能出牌（敌方回合 / 动画没播完）");
		double speed = Bridge.Num(a, "speed", 1.0);
		NHandCardHolder holder = FindHolder(a);
		CardModel card = holder.CardModel!;
		string title = card.TitleLocString.GetRawText();
		Creature? target = null;
		if (card.TargetType.IsSingleTarget())
		{
			List<Creature> foes = s.HittableEnemies.ToList();
			int ti = Bridge.Int(a, "target", 0);
			target = card.TargetType == TargetType.AnyEnemy ? (foes.Count > 0 ? foes[Math.Clamp(ti, 0, foes.Count - 1)] : null) : null;
		}
		string style = (Bridge.Str(a, "style") ?? "drag").ToLowerInvariant();
		string used = style;
		if (style == "drag" && !await Drag(holder, target, speed, a))
		{
			used = "instant (drag failed)";
			style = "instant";
		}
		if (style == "instant" && !card.TryManualPlay(target))
		{
			throw new Fail($"{title} 打不出去（不满足出牌条件？）");
		}
		if (Bridge.Bool(a, "wait", true))
		{
			await Mouse.Sleep(150);
			await WaitReady(Bridge.Int(a, "timeout_ms", 15000));
			await Mouse.Sleep(Bridge.Int(a, "settle_ms", 200));
		}
		return new { ok = true, card = card.Id.Entry, title, target = target?.Name, style_used = used };
	}

	/// <summary>真实拖牌：悬停 → 按下 → 往上拖进出牌区 → 拖到目标（单体牌）/ 屏幕中上（非指向牌）→ 停一下 → 松手。</summary>
	private static async Task<bool> Drag(NHandCardHolder holder, Creature? target, double speed, JsonObject a)
	{
		NPlayerHand? hand = NPlayerHand.Instance;
		if (hand == null) return false;
		Vector2 view = Mouse.ViewSize;
		int Ms(string key, int def) => (int)(Bridge.Int(a, key, def) / Math.Max(0.1, speed));

		// 1) 光标移到牌上，停一下让牌抬起来
		Vector2 cardAt = Mouse.Center(holder.Hitbox);
		await Mouse.MoveTo(() => GodotObject.IsInstanceValid(holder) ? Mouse.Center(holder.Hitbox) : cardAt, Mouse.Duration(cardAt, speed));
		await Mouse.Sleep(Ms("hover_ms", 420));
		if (!GodotObject.IsInstanceValid(holder)) return false;
		await Mouse.MoveTo(Mouse.Center(holder.Hitbox), 90, 0f);       // 牌抬起后对准它的新位置

		// 2) 按下，确认游戏进入了出牌流程
		Mouse.Button(true);
		bool started = false;
		for (int i = 0; i < 8 && !started; i++)
		{
			await Mouse.Frame();
			started = hand.InCardPlay;
		}
		if (!started)
		{
			Mouse.Button(false);
			GD.PrintErr($"{TestEntry.Tag} drag: card play did not start, falling back to instant");
			return false;
		}

		// 3) 往上拖进出牌区（牌会跟着光标走）
		Vector2 lift = new(Mathf.Lerp(Mouse.Pos.X, view.X * 0.5f, 0.35f), view.Y * 0.56f);
		await Mouse.MoveTo(lift, Ms("lift_ms", 300), 0.05f);

		// 4) 单体牌拖到敌人身上（瞄准箭头 + 准星），其他牌拖到屏幕中上
		Vector2? aim = target != null ? EnemyPoint(target) : null;
		if (aim is Vector2 enemy)
		{
			await Mouse.MoveTo(() => target != null && EnemyPoint(target) is Vector2 p ? p : enemy, Ms("aim_move_ms", 520), 0.12f);
			await Mouse.Sleep(Ms("aim_ms", 380));
		}
		else
		{
			await Mouse.MoveTo(new Vector2(view.X * 0.5f, view.Y * 0.42f), Ms("aim_move_ms", 360), 0.06f);
			await Mouse.Sleep(Ms("aim_ms", 260));
		}

		// 5) 松手打出
		Mouse.Button(false);
		for (int i = 0; i < 120 && hand.InCardPlay; i++)
		{
			await Mouse.Frame();
		}
		return true;
	}

	private static async Task<object> EndTurn(JsonObject a)
	{
		CombatState s = Runtime.Combat ?? throw new Fail("不在战斗中");
		Player me = Runtime.Me ?? throw new Fail("不在战斗中");
		Panel.Hide();
		await WaitReady(Bridge.Int(a, "timeout_ms", 15000));
		int round = s.RoundNumber;
		string style = (Bridge.Str(a, "style") ?? "click").ToLowerInvariant();
		string used = style;
		NEndTurnButton? button = NCombatRoom.Instance?.Ui.EndTurnButton;
		if (style == "click" && button != null)
		{
			Vector2 at = Mouse.Center(button);
			await Mouse.MoveTo(at, Mouse.Duration(at, Bridge.Num(a, "speed", 1.0)));
			await Mouse.Sleep(Bridge.Int(a, "hover_ms", 350));
			await Mouse.Click();
			await Mouse.Sleep(400);
		}
		if (!CombatManager.Instance.IsPlayerReadyToEndTurn(me) && s.RoundNumber == round)
		{
			used = style == "click" ? "instant (click missed)" : "instant";
			RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(new EndPlayerTurnAction(me, me.PlayerCombatState!.TurnNumber));
		}
		if (Bridge.Bool(a, "wait", true))
		{
			ulong end = Time.GetTicksMsec() + (ulong)Bridge.Int(a, "timeout_ms", 30000);
			while (Time.GetTicksMsec() < end && Runtime.Combat == s && s.RoundNumber == round)
			{
				await Mouse.Frame();
			}
			await WaitReady(15000);
		}
		return new { ok = true, round = Runtime.Combat?.RoundNumber, style_used = used };
	}

	private static async Task<object> Wait(JsonObject a)
	{
		int ms = Bridge.Int(a, "ms", 0);
		if (ms > 0) await Mouse.Sleep(ms);
		bool ready = true;
		if (Bridge.Str(a, "until") == "ready")
		{
			ready = await WaitReady(Bridge.Int(a, "timeout_ms", 15000));
		}
		return new { ok = true, ready };
	}

	// ── 鼠标 ──

	private static Vector2 Point(JsonObject a, Vector2 fallback)
	{
		Vector2 view = Mouse.ViewSize;
		float x = (float)Bridge.Num(a, "x", fallback.X), y = (float)Bridge.Num(a, "y", fallback.Y);
		if (Bridge.Bool(a, "relative", false)) { x *= view.X; y *= view.Y; }   // 0~1 的比例坐标
		return new Vector2(x, y);
	}

	private static async Task<object> MouseMove(JsonObject a)
	{
		Vector2 to = Point(a, Mouse.Pos);
		await Mouse.MoveTo(to, Bridge.Int(a, "ms", Mouse.Duration(to, Bridge.Num(a, "speed", 1.0))), (float)Bridge.Num(a, "arc", 0.08));
		return new { ok = true, x = to.X, y = to.Y };
	}

	private static async Task<object> MouseClick(JsonObject a)
	{
		Vector2 to = Point(a, Mouse.Pos);
		await Mouse.MoveTo(to, Bridge.Int(a, "ms", Mouse.Duration(to, Bridge.Num(a, "speed", 1.0))));
		await Mouse.Sleep(Bridge.Int(a, "hover_ms", 200));
		await Mouse.Click();
		return new { ok = true, x = to.X, y = to.Y };
	}

	/// <summary>光标停到不碍事的地方（默认屏幕上方正中，不压到人物和卡牌）。</summary>
	private static async Task<object> MousePark(JsonObject a)
	{
		Vector2 view = Mouse.ViewSize;
		Vector2 to = Point(a, new Vector2(view.X * 0.5f, view.Y * 0.16f));
		await Mouse.MoveTo(to, Bridge.Int(a, "ms", Mouse.Duration(to, Bridge.Num(a, "speed", 1.0))));
		return new { ok = true, x = to.X, y = to.Y };
	}

	private static async Task<object> HoverCard(JsonObject a)
	{
		NHandCardHolder h = FindHolder(a);
		Vector2 at = Mouse.Center(h.Hitbox);
		await Mouse.MoveTo(() => GodotObject.IsInstanceValid(h) ? Mouse.Center(h.Hitbox) : at, Mouse.Duration(at, Bridge.Num(a, "speed", 1.0)));
		await Mouse.Sleep(Bridge.Int(a, "hover_ms", 900));
		return new { ok = true, card = h.CardModel!.Id.Entry };
	}

	// ── 界面 ──

	private static object Chrome(JsonObject a)
	{
		Panel.SetChrome(Bridge.Bool(a, "test_button", Panel.ButtonVisible));
		if (Bridge.Bool(a, "panel", false) == false) Panel.Hide();
		return new { ok = true, test_button = Panel.ButtonVisible };
	}

	/// <summary>找可见的界面节点：type=类名（含父类名），text=节点或其子标签的文字包含；返回可点击的那一层。</summary>
	private static List<Control> Find(JsonObject a)
	{
		string? type = Bridge.Str(a, "type");
		string? text = Bridge.Str(a, "text");
		string? character = Bridge.Str(a, "character");
		List<Control> found = new();
		Stack<Node> stack = new();
		stack.Push(((SceneTree)Engine.GetMainLoop()).Root);
		while (stack.Count > 0 && found.Count < 40)
		{
			Node n = stack.Pop();
			foreach (Node child in n.GetChildren()) stack.Push(child);
			if (n is not Control c || !c.IsVisibleInTree()) continue;
			if (type != null && !IsType(c, type)) continue;
			if (text != null && !TextOf(c).Contains(text, StringComparison.OrdinalIgnoreCase)) continue;
			if (character != null && !(c is NCharacterSelectButton b && b.Character != null && (b.Character.Id.Entry.Equals(character, StringComparison.OrdinalIgnoreCase) || b.Character.GetType().Name.Equals(character, StringComparison.OrdinalIgnoreCase)))) continue;
			if (type == null && text == null && character == null) continue;
			Control hit = text != null && type == null ? Clickable(c) : c;
			if (!found.Contains(hit)) found.Add(hit);
		}
		return found;
	}

	private static bool IsType(object o, string name)
	{
		for (Type? t = o.GetType(); t != null; t = t.BaseType)
		{
			if (t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return true;
		}
		return false;
	}

	private static string TextOf(Control c) => c switch
	{
		Label l => l.Text,
		RichTextLabel r => r.GetParsedText(),
		Button b => b.Text,
		_ => "",
	};

	private static Control Clickable(Control c)
	{
		for (Node? n = c; n != null; n = n.GetParent())
		{
			if (n is BaseButton || (n is Control ctl && IsType(ctl, "NClickableControl"))) return (Control)n;
		}
		return c;
	}

	private static object Describe(Control c)
	{
		Vector2 at = Mouse.Center(c);
		return new
		{
			type = c.GetType().Name,
			name = c.Name.ToString(),
			text = TextOf(c),
			x = at.X,
			y = at.Y,
			character = c is NCharacterSelectButton b ? b.Character?.Id.Entry : null,
			map_state = c is NMapPoint p ? p.State.ToString() : null,
			map_col = c is NMapPoint p2 ? p2.Point?.coord.col : null,
			map_row = c is NMapPoint p3 ? p3.Point?.coord.row : null,
		};
	}

	private static async Task<object> ClickNode(JsonObject a)
	{
		List<Control> nodes = Find(a);
		if (Bridge.Bool(a, "travelable", false)) nodes = nodes.Where(n => n is NMapPoint p && p.State == MapPointState.Travelable).ToList();
		int index = Bridge.Int(a, "index", 0);
		if (nodes.Count == 0 || index >= nodes.Count) throw new Fail("界面上找不到要点的节点（先用 /ui/find 看看）");
		Control target = nodes[index];
		Vector2 at = Mouse.Center(target);
		await Mouse.MoveTo(at, Bridge.Int(a, "ms", Mouse.Duration(at, Bridge.Num(a, "speed", 1.0))));
		await Mouse.Sleep(Bridge.Int(a, "hover_ms", 300));
		await Mouse.Click();
		await Mouse.Sleep(Bridge.Int(a, "settle_ms", 300));
		return new { ok = true, clicked = Describe(target) };
	}

	private static IEnumerable<Control> MapPoints() => Find(new JsonObject { ["type"] = nameof(NMapPoint) });

	// ── 开发者控制台 ──

	private static async Task<object> Console(JsonObject a)
	{
		string command = Bridge.Str(a, "command") ?? throw new Fail("要 command");
		Traverse statics = Traverse.Create(typeof(NDevConsole));
		if (statics.Field("_instance").GetValue() == null)
		{
			if (NDevConsole.Create() is not Node layer) throw new Fail("创建开发者控制台失败");
			((SceneTree)Engine.GetMainLoop()).Root.AddChild(layer);
			await Mouse.Frame();
		}
		NDevConsole console = NDevConsole.Instance;
		Traverse t = Traverse.Create(console);
		if (t.Field("_inputBuffer").GetValue() is not LineEdit input) throw new Fail("找不到控制台输入框");
		input.Text = command;
		t.Method("ProcessCommand").GetValue();
		string output = t.Field("_outputBuffer").GetValue() is RichTextLabel o ? o.GetParsedText() : "";
		return new { ok = true, command, output_tail = output.Length > 600 ? output[^600..] : output };
	}
}
