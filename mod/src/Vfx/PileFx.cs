using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
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
	private const float HeaderH = 58f;
	public const float HeroShiftX = 0.22f;

	/// <summary>v0.14.56：四个牌堆界面各有不同的版式和动画（用户：每个牌堆都要有不同的表现）。</summary>
	public enum Kind { Draw, Discard, Exhaust, Deck }

	/// <summary>
	/// 每个界面的版式（都按 1920×1080 写，运行时乘 k = 视口高 / 1080）。RightSide = 网格贴屏幕右边（宽屏时跟着右移）。
	/// Slant：网格沿屏幕斜（每往下 1px 横移多少）；Side：装饰 + 立绘那一组从哪边滑进来（+1 右、-1 左）。
	/// </summary>
	private sealed class Layout
	{
		public Kind Kind;
		public int Cols = 5;
		public float CardW = 160f, CellW = 178f, CellH = 252f;
		public float GridX = 190f, GridTop = 262f, GridBottom = 1010f, Slant = 0.05f;
		public bool RightSide;
		public Vector2 Focus = new(700f, 560f);
		public float InfoX = 60f, TitleX = 58f, Side = 1f;
		public Vector2 Iris;
		public string Title = "", Word = "";
	}

	private static Layout For(Kind kind) => kind switch
	{
		Kind.Deck => new Layout
		{
			Kind = kind, CardW = 166f, CellW = 186f, GridX = 1920f - 70f - 5 * 186f, Slant = -0.05f, RightSide = true,
			Focus = new Vector2(1260f, 560f), InfoX = 1520f, TitleX = 930f, Side = -1f, Iris = new Vector2(0.93f, 0.04f), Title = "DECK", Word = "DECK",
		},
		Kind.Discard => new Layout
		{
			Kind = kind, GridX = 200f, GridTop = 270f, Slant = -0.04f, Focus = new Vector2(720f, 560f),
			Iris = new Vector2(0.96f, 0.95f), Title = "DISCARD PILE", Word = "DISCARD",
		},
		Kind.Exhaust => new Layout
		{
			Kind = kind, Slant = 0f, Iris = new Vector2(0.96f, 0.82f), Title = "EXHAUST PILE", Word = "EXHAUST",
		},
		_ => new Layout { Kind = Kind.Draw, Iris = new Vector2(0.04f, 0.95f), Title = "DRAW PILE", Word = "DRAW" },
	};

	/// <summary>v0.14.101 弃牌堆 = 原版 ITEM 页（用户：牌堆和抽牌堆 / 牌组一样，放右边；人物往左挪，不必完整露出）：版式同牌组，圆孔仍从弃牌堆按钮张开。</summary>
	private static Layout ItemLayout() => new()
	{
		Kind = Kind.Discard, CardW = 166f, CellW = 186f, GridX = 1920f - 70f - 5 * 186f, Slant = -0.05f, RightSide = true,
		Focus = new Vector2(1260f, 560f), InfoX = 1520f, TitleX = 930f, Side = -1f, Iris = new Vector2(0.96f, 0.95f), Title = "DISCARD PILE", Word = "DISCARD",
	};

	// 原版 DT_CampParamItem / DT_CampParamCommon（30fps 帧）：白板 10 帧 CurveUp1；人物 ItemSilhouette 等 10 帧、20 帧 CurveUp2 移到 (-90, -110)（游戏里改成 0.05s 起步、0.7s CurveUp3，见入场处）；InDist -100
	private const float ItemShiftX = -520f;                 // 人物整体左移（1920 宽下的像素），给右边的牌堆让位
	private static readonly Vector2 ItemSilhouette = new(-90f, -110f);

	/// <summary>原版 EOneAnimType::CurveUpN 的近似（曲线写在原版程序里，资源里没有）：1 - (1 - t)^(N+1)。</summary>
	private static float CurveUp(int n, float t) => 1f - Mathf.Pow(1f - Mathf.Clamp(t, 0f, 1f), n + 1);

	public static Kind KindOf(PileType type) => type switch
	{
		PileType.Discard => Kind.Discard,
		PileType.Exhaust => Kind.Exhaust,
		PileType.Deck => Kind.Deck,
		_ => Kind.Draw,
	};
	private const float PorScale = 0.74f;             // 立绘（1024×1536 画布）缩放
	private const float DeckPorScale = 0.78f, DiscardPorScale = 0.72f, ExhaustPorScale = 0.8f;
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
		public CardModel Card = null!;
		public int Index;
		public float Born = -1f;
		public float Lift;
		public float Dim = 1f;
		public Vector2 At;                     // 网格里的位置（未滚动、未斜切，参考像素）
		public Vector2 From;                   // v0.14.98 切换排列模式：从 From 飞到 At
		public float MoveT0 = -1f, MoveDelay;
	}

	private sealed class Section
	{
		public Node2D Head = null!;
		public float Y;
		public float Born = -1f, Die = -1f;    // v0.14.98 切换排列模式时标题淡入 / 淡出
	}

	private static readonly AccessTools.FieldRef<NCardGrid, List<CardModel>>? GridCards = Field();
	private static AccessTools.FieldRef<NCardGrid, List<CardModel>>? Field()
	{
		try { return AccessTools.FieldRefAccess<NCardGrid, List<CardModel>>("_cards"); }
		catch { return null; }
	}

	/// <summary>
	/// v0.14.98（用户：按类型分区看不习惯，也看不出抽牌顺序，影响 SL）：原版抽牌堆界面的顺序。
	/// 原版是 Pile.Cards 再按 稀有度 → Id 用 List.Sort（不稳定排序）排一次 —— 同名牌谁在前取决于牌堆里的实际位置，SL 靠的就是这个。
	/// 优先直接读原版网格（我们只是把它藏起来，它照样排好了），游戏更新了排法也跟着变；读不到时按同一段代码现算（同输入同算法 = 同结果）。
	/// </summary>
	private static List<CardModel> VanillaOrder(Control grid, CardPile pile)
	{
		List<CardModel> cards = pile.Cards.ToList();
		if (grid is NCardGrid g && GridCards != null && GridCards(g) is List<CardModel> shown && shown.Count == cards.Count && shown.All(c => cards.Contains(c)))
		{
			return shown.ToList();
		}
		if (pile.Type == PileType.Draw)
		{
			cards.Sort((c1, c2) => c1.Rarity != c2.Rarity ? c1.Rarity.CompareTo(c2.Rarity) : string.Compare(c1.Id.Entry, c2.Id.Entry, StringComparison.Ordinal));
		}
		return cards;
	}

	public static bool Enabled(CardPile? pile)
	{
		if (!P3Settings.PileScreen || !Patches.Stage.MakotoRun)
		{
			return false;
		}
		return pile != null;                               // v0.14.61：手柄也用我们的界面（方向键选牌、确认键特写，见 Build 里的 Nav）
	}

	/// <summary>原版界面里要藏起来的节点（牌组界面另有排序按钮条）。回滚时恢复显示。</summary>
	// BottomLabel 的黑色底板是兄弟节点：隐藏整个 BottomText 才不会留下 124×40 的空黑块。
	private static readonly string[] Hidden = { "CardGrid", "BottomText", "%SortingBg", "%ObtainedSorter", "%CardTypeSorter", "%CostSorter", "%AlphabeticalSorter" };

	private const float ExitTime = 0.42f;
	private const float WhooshSkip = 0.3f;   // ShuffleTime_Whoosh 前 0.36s 是静音引子
	private static readonly Dictionary<ulong, Action> Exits = new();

	/// <summary>
	/// v0.14.55 退场动画：游戏关闭界面时调 AfterCapstoneClosed（原版：藏起来 + 删掉）。这里接管：先播 0.42s 退场，播完再按原版的方式藏起来、删掉。
	/// 关闭流程的其他部分（战斗恢复、遮罩淡出、当前界面清空）照常立即执行，只是这个界面晚一点消失。返回 false = 没有我们的界面，走原版。
	/// </summary>
	public static bool TryExit(Control screen)
	{
		if (!Exits.Remove(screen.GetInstanceId(), out Action? exit))
		{
			return false;
		}
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
		if (screen.GetNodeOrNull(RootName) is Node root) HeroStage.Close(root);
		screen.GetNodeOrNull(RootName)?.QueueFree();
		foreach (string path in Hidden)
		{
			if (screen.GetNodeOrNull<CanvasItem>(path) is CanvasItem ci)
			{
				ci.Visible = true;
			}
		}
	}

	public static void Build(Control screen, CardPile pile, Kind kind)
	{
		if (screen.GetNodeOrNull(RootName) != null || screen.GetNodeOrNull<Control>("CardGrid") is not Control grid)
		{
			return;
		}
		Vector2 view = screen.GetViewportRect().Size;
		ulong buildStart = Time.GetTicksUsec();
		float k = view.Y / 1080f;
		float rx = view.X - 1920f * k;                    // 右侧元素的水平偏移（宽屏时贴右边缘）
		bool item = kind == Kind.Discard && HeroStage.Available(screen.GetTree()) && HeroStage.ItemReady;
		Layout L = item ? ItemLayout() : For(kind);
		float gx = L.GridX + (L.RightSide ? rx / k : 0f);   // 网格左边（参考像素）
		Font font = Typo.PileBarlow;
		float t0 = Time.GetTicksMsec() / 1000f;
		float Now() => Time.GetTicksMsec() / 1000f - t0;

		Control root = new() { Name = RootName, MouseFilter = Control.MouseFilterEnum.Stop };
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		screen.AddChild(root);
		screen.MoveChild(root, grid.GetIndex());             // 在原版网格的位置：返回按钮仍在上面
		foreach (string path in Hidden)
		{
			if (screen.GetNodeOrNull<CanvasItem>(path) is CanvasItem ci)
			{
				ci.Visible = false;                          // 原版网格、说明（写的是"按稀有度排序"）、牌组的排序按钮条
			}
		}

		// v0.14.89：分段计时（日志 perf pile 一行里列出），定位第一次打开时卡在哪一块
		var laps = new System.Text.StringBuilder();
		ulong lapAt = Time.GetTicksUsec();
		void lap(string what)
		{
			ulong n = Time.GetTicksUsec();
			laps.Append($" {what}={(n - lapAt) / 1000f:0.0}");
			lapAt = n;
		}
		lap("hide");
		// ── 背景：圆孔从牌堆按钮那个角张开 ──
		Vector2 from = L.Iris;
		// v0.14.90：抽牌堆 = 原版营地 COMMU 页（原版 3D 主角 + 原版背景上色 + 白色斜块）；舞台加载失败就退回原来的水下背景和立绘。
		bool commu = kind == Kind.Draw && HeroStage.Available(screen.GetTree());
		// v0.14.95：牌组 = 原版营地主菜单的"倒入水"（DiveFx：原版水花 + 水下露出 + 原版波浪；HeroStage.Act.Top 倒入水 → 水下待机）
		bool dive = kind == Kind.Deck && HeroStage.Available(screen.GetTree()) && DiveFx.Ready;
		ShaderMaterial water = new() { Shader = commu || dive || item ? Shaders.CommuBg : Shaders.Underwater };
		if (commu || dive || item) water.SetShaderParameter("caustics", HeroStage.Tex("T_UI_Camp_Caustics_00.png")!);
		water.SetShaderParameter("aspect", view.X / view.Y);
		water.SetShaderParameter("center", from);
		water.SetShaderParameter("radius", 0f);
		ColorRect bg = new() { Size = view, Material = water, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(bg);
		float diag = Mathf.Sqrt(view.X * view.X / (view.Y * view.Y) + 1f) + 0.1f;
		if (dive) water.SetShaderParameter("radius", diag);   // 入水转场自己负责露出（水花轮廓），不用圆孔
		else Fx.Animate(bg, 0.55, t => water.SetShaderParameter("radius", diag * Ease.OutCubic(t)));
		lap("water");
		Node2D bubbles = Bubbles(root, view, k);
		lap("bubbles");

		// ── 装饰 + 立绘（每个界面不同），整组从一侧滑进来 ──
		Node2D right = new() { Position = new Vector2(700f * k * L.Side, 0f), Modulate = new Color(1f, 1f, 1f, 0f) };
		root.AddChild(right);
		PileButterfly butterfly;
		switch (kind)
		{
			case Kind.Deck:
				if (dive && HeroStage.Open(right, view, 0f, HeroStage.Act.Top))
				{
					butterfly = new PileButterfly(right, Kind.Deck, k, 0f, DeckPorScale);   // 原版镜头：倒着的结城理在左边，右边留给牌
					lap("hero3d");
				}
				else
				{
					dive = false;
					butterfly = DeckDecor(right, view, font, k);
				}
				break;
			case Kind.Discard:
				if (item && ItemDecor(right, view, k))
				{
					butterfly = new PileButterfly(right, Kind.Deck, k, 0f, DeckPorScale);   // 人物在左边，蓝蝶位置同牌组
					lap("hero3d");
				}
				else
				{
					item = false;
					butterfly = DiscardDecor(right, view, font, k, rx);
				}
				break;
			case Kind.Exhaust:
				butterfly = ExhaustDecor(root, right, view, font, k, rx);
				break;
			default:
				if (!commu)
				{
					Disc(right, L.Word, font, k, rx);
					lap("disc");
				}
				butterfly = new PileButterfly(right, kind, k, rx, PorScale);
				lap("butterfly");
				Node2D? panel = commu ? CommuPanel(right, view) : null;
				bool hero = commu && HeroStage.Open(right, view, HeroShiftX * view.X * L.Side);
				lap("hero3d");
				if (hero)
				{
					float u = view.X / 1920f;
					HeroStage.CutOut(panel!);                                   // 白块和灰影在人物剪影处挖空：外套透出的是水
					HeroStage.CutOut((CanvasItem)panel!.GetMeta("shadow").AsGodotObject());
					HeroStage.AddShadow(panel!, new Vector2(24f, 10f) * u);   // 人物投在白块上的小阴影（被白块裁剪）
					CommuWord(panel!, view);
				}
				else
				{
					if (panel != null)
					{
						((Node)panel.GetMeta("shadow").AsGodotObject()).QueueFree();
						panel.QueueFree();
					}
					Portrait(right, k, rx);
					lap("portrait");
				}
				break;
		}
		lap("decor");
		butterfly.AttachForeground(right);                 // 装饰底板 → 后景蓝蝶 → 立绘 → 前景蓝蝶；卡牌仍在整组之上。
		float decorMs = (Time.GetTicksUsec() - buildStart) / 1000f;
		if (dive)
		{
			right.Position = Vector2.Zero;                   // 人物自己从画面外沉下来，整组不滑入
			right.Modulate = Colors.White;
		}
		else if (item)
		{
			// ITEM 页：整组不滑入；随圆孔张开淡入，白板按原版参数从左滑进，人物按原版 ItemSilhouette 落位
			right.Position = Vector2.Zero;
			float u = view.X / 1920f;
			Node2D board = right.GetNode<Node2D>("ItemBoard");
			Fx.Animate(right, 0.3, t => right.Modulate = new Color(1f, 1f, 1f, Ease.OutCubic((float)t)));
			Fx.After(right, 0.1, "item board in", () => Fx.Animate(board, 10.0 / 30.0, t =>
			{
				float e = CurveUp(1, (float)t);
				board.Position = new Vector2(-100f * k * (1f - e), 0f);
				board.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp((float)t * 1.6f, 0f, 1f));
			}));
			// 人物落位：原版等 10 帧再动，那 10 帧在原版被切页遮罩盖着；这里圆孔一张开人物就看得见，停着不动再突然起步显得僵（v0.14.103 用户反馈）。
			// 改成和淡入几乎同时起步（只让开打开牌堆那一两帧的卡顿，免得曲线开头被吞掉跳一下），一路减速到原版落点，时长 0.7s，CurveUp3 收尾更软
			Fx.After(right, 0.05, "item silhouette", () => Fx.Animate(right, 0.7, t => HeroStage.Nudge(ItemSilhouette * u * CurveUp(3, (float)t))));
		}
		else Fx.After(right, 0.1, "pile right in", () => Fx.Animate(right, 0.5, t =>
		{
			right.Position = new Vector2(700f * k * L.Side * (1f - Ease.OutExpo(t)), 0f);
			right.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(t * 3f, 0f, 1f));
		}));

		// ── 左上：牌堆名 + 张数 + 一行说明 ──
		float headFrom = (L.RightSide ? 900f : -640f) * k;    // 标题从网格那一侧滑进来
		Node2D head = new() { Position = new Vector2(headFrom, 0f) };
		root.AddChild(head);
		string title = L.Title;
		float tx = L.TitleX + (L.RightSide ? rx / k : 0f);
		// 标题：Barlow 粗斜体，白字深蓝描边，右下错开一层宝蓝投影（P3R 菜单标题的叠影）
		Text(head, title, font, (int)(92f * k), new Color(0.12f, 0.36f, 1f), new Vector2((tx + 8f) * k, 98f * k), (int)(5f * k), new Color(0.12f, 0.36f, 1f));
		Label titleL = Text(head, title, font, (int)(92f * k), Colors.White, new Vector2(tx * k, 90f * k), (int)(5f * k), Ink);
		Label countL = Text(head, "0", font, (int)(112f * k), Cyan, new Vector2((tx + 26f) * k + titleL.GetMinimumSize().X, 74f * k), (int)(5f * k), Ink);
		// v0.14.100（用户：切换条已经起到说明作用，说明文字没用）：抽牌堆不画说明条，切换条放在说明条的位置
		bool hasSwitch = kind == Kind.Draw;
		if (!hasSwitch)
		{
			Fx.Poly(head, new[] { new Vector2(tx - 2f, 200f) * k, new Vector2(tx + 502f, 200f) * k, new Vector2(tx + 494f, 228f) * k, new Vector2(tx - 10f, 228f) * k }, Navy);
			Fx.Poly(head, new[] { new Vector2(tx - 2f, 200f) * k, new Vector2(tx + 12f, 200f) * k, new Vector2(tx + 4f, 228f) * k, new Vector2(tx - 10f, 228f) * k }, Cyan);
		}
		bool orderMode = kind == Kind.Draw && P3Settings.Current.DrawPileOrder;
		string note = kind switch
		{
			Kind.Draw => "",
			Kind.Deck => Zh ? "整副牌组 · 按类型 → 费用排列" : "WHOLE DECK · SORTED BY TYPE → COST",
			Kind.Exhaust => Zh ? "本场战斗中消耗的牌 · 最新在前" : "EXHAUSTED THIS COMBAT · MOST RECENT FIRST",
			_ => Zh ? "按进入牌堆的先后排列 · 最新在前" : "MOST RECENT FIRST",
		};
		if (note.Length > 0) Text(head, note, Zh ? Typo.PileCjk : font, (int)(19f * k), new Color(0.8f, 0.93f, 1f), new Vector2((tx + 26f) * k, 202f * k), 0, Ink);
		ModeSwitch modeSwitch = ModeSwitch.Build(head, new Vector2(tx - 10f, 200f) * k, k, font, orderMode, hasSwitch);
		Fx.After(head, 0.16, "pile head in", () => Fx.Animate(head, 0.45, t => head.Position = new Vector2(headFrom * (1f - Ease.OutExpo(t)), 0f)));

		// ── 网格 ──
		Control cards = new() { MouseFilter = Control.MouseFilterEnum.Ignore };
		cards.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.AddChild(cards);
		Node2D heads = new();
		root.AddChild(heads);
		root.MoveChild(heads, cards.GetIndex());              // 段标题在卡牌下面（放大的牌可以盖住标题）
		Node2D? plate = null;                                // 真正悬停时才创建名牌。
		Label empty = Text(root, "EMPTY", font, (int)(90f * k), new Color(1f, 1f, 1f, 0.45f), new Vector2(gx, L.GridTop + 60f) * k, 0, Ink);
		empty.Visible = false;

		// ── 点击特写：压暗层 + 白色斜带 + 旋转光芒 + 左侧说明；挡点击的透明层 ──
		// v0.14.54：这几层都和卡牌放在同一个父节点（cards）里，特写时只调前后顺序（压暗层 → 挡点击层 → 特写那张牌）。
		// 之前把特写的牌 Reparent 到另一层：holder 离开场景树时游戏会回收它的牌面（NGridCardHolder._ExitTree），牌就消失了
		Vector2 focusAt = L.Focus + new Vector2(L.RightSide ? rx / k : 0f, 0f);
		lap("head");
		Focus? fx = null;                                    // 首开不构建不可见的特写层。
		lap("focus");
		Control catcher = new() { MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };
		catcher.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		cards.AddChild(catcher);
		// v0.14.61 手柄：焦点停在这个看不见的控件上（四个方向的邻居都是它自己），不让游戏把焦点交给藏起来的原版网格；
		// 选牌由 Nav 自己按网格位置移动。不改对象池里 holder 的焦点邻居（不会被池子复位）。
		Control navFocus = new() { Name = "P3PileNavFocus", FocusMode = Control.FocusModeEnum.All, MouseFilter = Control.MouseFilterEnum.Ignore, Size = Vector2.One };
		NodePath self = new(".");
		navFocus.FocusNeighborLeft = navFocus.FocusNeighborRight = navFocus.FocusNeighborTop = navFocus.FocusNeighborBottom = self;
		navFocus.FocusNext = navFocus.FocusPrevious = self;
		root.AddChild(navFocus);
		Control front = cards;
		Item? focused = null;
		Flip? flip = null;
		float focusP = 0f;
		bool focusOn = false;
		float lastNow = 0f;
		bool exiting = false, finalized = false;
		float exitT0 = 0f;

		List<Item> items = new();
		// 牌组界面原版的「查看升级」勾选框：勾上时网格里能升级的牌都显示升级后的样子
		bool upgradeOn = false;
		NTickbox? upgrades = kind == Kind.Deck ? screen.GetNodeOrNull<NTickbox>("%Upgrades") : null;
		void SyncGridUpgradePreview()
		{
			// _Ready 时 NTickbox 的默认值是 true；AfterCapstoneOpened 随后直接写 false，不发 Toggled。
			// 每帧只比较实际值，发生变化才刷新已建卡牌，兼容原版开屏初始化和手柄切换。
			if (upgrades == null || !GodotObject.IsInstanceValid(upgrades) || upgradeOn == upgrades.IsTicked) return;
			upgradeOn = upgrades.IsTicked;
			foreach (Item it in items)
			{
				if (it.Holder != null && GodotObject.IsInstanceValid(it.Holder) && it != focused)
				{
					it.Holder.SetIsPreviewingUpgrade(upgradeOn && it.Card.IsUpgradable);
				}
			}
		}
		// v0.14.65 特写里的「查看升级」按钮（四个界面都有，在特写卡牌右下角外侧）：点一下卡牌翻一圈（正面 → 牌背 → 升级后的正面），再点翻回来。
		// 已经满级的牌：按钮变灰写「已升级」；本来就不能升级的（诅咒 / 状态等）：不显示。可多次升级的牌照常翻到下一级。
		UpgradeButton? upBtn = null;
		bool focusUp = false;                                   // 特写这张现在显示的是不是升级后
		float upT0 = -1f;                                       // 翻转开始的时间（< 0 = 没在翻）
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
			{
				it.Holder.SetIsPreviewingUpgrade(up);
			}
			Fill(fx!, up && upModel != null ? upModel : it.Card, pile, k);
		}

		void FlipUpgrade()
		{
			if (focused == null || !focusOn || upT0 >= 0f || !focused.Card.IsUpgradable || flip == null)
			{
				return;
			}
			upT0 = Now();
			upSwapped = false;
			Audio.Sfx.Clip("ShuffleTime_Whoosh", 0.35f);
		}
		void EnsureFocus()
		{
			if (fx != null) return;
			ulong start = Time.GetTicksUsec();
			fx = FocusLayer(cards, view, k, font, focusAt, L.InfoX + (L.RightSide ? rx / k : 0f));
			UpgradeButton button = UpgradeButton.Build(cards, k, font);
			upBtn = button;
			button.Hit.GuiInput += ev => ModEntry.Safe("pile upgrade flip", () =>
			{
				if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && focused != null)
				{
					button.Hit.AcceptEvent();
					FlipUpgrade();
				}
			});
			GD.Print($"{ModEntry.Tag} perf pile focus prepare {(Time.GetTicksUsec() - start) / 1000f:0.0}ms");
		}
		List<Section> sections = new();
		List<Section> dying = new();
		float sweepT0 = -10f;
		List<CardModel> pending = new();
		Item? hovered = null;
		float scroll = 0f, target = 0f, contentH = 0f;
		bool dragging = false;
		float dragFrom = 0f, dragScroll = 0f;
		float nextBorn = dive ? 0.35f : 0.22f;              // 入水：等水花散开再出牌

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
			FreeFlip(flip);
			flip = null;

			List<CardModel> all = pile.Cards.ToList();
			float y = Place(Groups(all), null);
			contentH = y;
			countL.Text = all.Count.ToString();
			target = Mathf.Clamp(target, 0f, MaxScroll());
		}

		List<(string En, string Zh, List<CardModel> Cards)> Groups(List<CardModel> all)
		{
			List<(string En, string Zh, List<CardModel> Cards)> groups = new();
			if (kind == Kind.Draw && orderMode)
			{
				// 原版顺序：一整段，从左到右、从上往下（行宽按我们的版式，不照抄原版一行几张）
				if (all.Count > 0) groups.Add(("DRAW ORDER", "", VanillaOrder(grid, pile)));   // v0.14.100：不加中文副标题（和 ×张数 叠在一起，也没用）
			}
			else if (kind == Kind.Draw || kind == Kind.Deck)
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
				groups.Add(("RECENT", "", all));   // 说明条已经写了"最新在前"
			}
			return groups;
		}

		// 按分组摆位置。reuse == null：新建 Item（之后分批建牌）；否则沿用已有的牌，只给新位置（切换排列模式，牌飞过去）
		float Place(List<(string En, string Zh, List<CardModel> Cards)> groups, Dictionary<CardModel, Item>? reuse)
		{
			float y = 0f;
			int index = 0;
			float now = Now();
			List<Item> order = new();
			foreach ((string en, string zh, List<CardModel> list) in groups)
			{
				sections.Add(new Section { Head = SectionHead(heads, en, zh, list.Count, font, k, L.Cols * L.CellW), Y = y, Born = reuse != null ? now + 0.18f : -1f });
				y += HeaderH;
				for (int i = 0; i < list.Count; i++)
				{
					Vector2 at = new((i % L.Cols) * L.CellW + L.CellW / 2f, y + (i / L.Cols) * L.CellH + L.CellH / 2f);
					if (reuse != null && reuse.Remove(list[i], out Item? it))
					{
						it.From = it.At;
						it.At = at;
						it.Index = index;
						it.MoveT0 = now;
						it.MoveDelay = Mathf.Min(0.3f, index * 0.014f);
						order.Add(it);
						index++;
						continue;
					}
					pending.Add(list[i]);
					Item n = new() { Card = list[i], Index = index++, At = at };
					items.Add(n);
					order.Add(n);
				}
				y += Mathf.Ceil(list.Count / (float)L.Cols) * L.CellH + 16f;
			}
			if (reuse != null)
			{
				items.Clear();
				items.AddRange(order);
			}
			return y;
		}

		// v0.14.98 抽牌堆：原版顺序 ⇄ 按类型。牌不重建，每张从旧位置沿弧线飞到新位置（按新顺序依次出发），段标题淡出 / 淡入，一道青色扫光掠过网格
		void Switch(bool toOrder)
		{
			if (kind != Kind.Draw || orderMode == toOrder || exiting || focused != null)
			{
				return;
			}
			orderMode = toOrder;
			P3Settings.Current.DrawPileOrder = toOrder;
			P3Settings.Save();
			float now = Now();
			foreach (Section s in sections)
			{
				s.Die = now;
			}
			dying.AddRange(sections);
			sections.Clear();
			Dictionary<CardModel, Item> reuse = new(ReferenceEqualityComparer.Instance);
			foreach (Item it in items)
			{
				reuse[it.Card] = it;
			}
			List<Item> before = items.ToList();
			contentH = Place(Groups(pile.Cards.ToList()), reuse);
			foreach (Item gone in reuse.Values)
			{
				// 牌堆在切换的同一帧变了（极少见）：多出来的直接还回去
				if (gone.Holder != null && GodotObject.IsInstanceValid(gone.Holder)) gone.Holder.QueueFreeSafely();
				if (gone.Slot != null && GodotObject.IsInstanceValid(gone.Slot)) gone.Slot.QueueFree();
			}
			target = Mathf.Clamp(target, 0f, MaxScroll());
			sweepT0 = now;
			modeSwitch.Set(toOrder);
			Audio.Sfx.Clip("ShuffleTime_CardSet", 0.4f);
		}
		modeSwitch.Changed = Switch;
		// 扫光：一道青色细光带从网格顶扫到底（切换排列模式时）
		Polygon2D sweep = new() { Color = new Color(0.55f, 0.9f, 1f, 0f), Polygon = new[] { new Vector2(-20f, -3f), new Vector2(L.Cols * L.CellW + 20f, -3f), new Vector2(L.Cols * L.CellW + 20f, 3f), new Vector2(-20f, 3f) }.Select(v => v * k).ToArray() };
		heads.AddChild(sweep);

		float MaxScroll() => Mathf.Max(0f, contentH - (L.GridBottom - L.GridTop));

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
			slot.AddChild(h);
			card!.UpdateVisuals(pile.Type, CardPreviewMode.Normal);
			h.SetIsPreviewingUpgrade(upgradeOn && it.Card.IsUpgradable);
			it.Slot = slot;
			it.Holder = h;
			it.Born = Mathf.Max(Now(), nextBorn);
			nextBorn = it.Born + 0.022f;
			h.Hitbox.MouseEntered += () =>
			{
				if (hovered != it)
				{
					hovered = it;
					Audio.Sfx.Clip("Sys_Cursor", 0.35f);
					if (kind == Kind.Draw) HeroStage.Kick();
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
				EnsureFocus();
				focused = it;
				focusOn = true;
				cards.MoveChild(fx!.Layer, cards.GetChildCount() - 1);
				cards.MoveChild(catcher, cards.GetChildCount() - 1);
				cards.MoveChild(it.Slot, cards.GetChildCount() - 1);
				cards.MoveChild(upBtn!.Root, cards.GetChildCount() - 1);     // 按钮在挡点击层上面才点得到
				flip = MakeFlip(root, front, it.Card, pile.Type, it.Holder);
				catcher.Visible = true;
				upModel = null;
				upT0 = -1f;
				focusUp = upgradeOn && it.Card.IsUpgradable;               // 牌组界面勾了「查看升级」：特写从升级后开始
				ShowFace(it, focusUp);
				upBtn.Show(it.Card.IsUpgradable, it.Card.IsUpgraded, focusUp, it.Card.CurrentUpgradeLevel + 1, it.Card.MaxUpgradeLevel > 1);
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

		// 动作名用字面量（MegaInput 的同名字段在 0.107 不全，例如没有 confirm）；不存在的动作由 Pressed 里的 HasAction 跳过
		// ── 手柄（v0.14.61）：方向键在网格里移动选中（按住连发），确认键特写 / 收回；特写时返回键只收回特写，不关界面 ──
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
			if (on == backBound || NHotkeyManager.Instance is not NHotkeyManager hk)
			{
				return;
			}
			backBound = on;
			foreach (string key in backKeys)
			{
				if (!InputMap.HasAction(key))
				{
					continue;                                    // 旧版本没有的动作不挂（否则每次按键都会报"动作不存在"）
				}
				if (on)
				{
					hk.PushHotkeyPressedBinding(key, unzoom);
					hk.PushHotkeyReleasedBinding(key, swallow);   // 原版返回按钮可能挂在"松开"上：特写期间一并挡住
				}
				else
				{
					hk.RemoveHotkeyPressedBinding(key, unzoom);
					hk.RemoveHotkeyReleasedBinding(key, swallow);
				}
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
				// 同一行里左右相邻的一张（不跨行）
				foreach (Item it in items)
				{
					float d = (it.At.X - cur.At.X) * dx;
					if (Mathf.IsEqualApprox(it.At.Y, cur.At.Y) && d > 1f && d < bestD)
					{
						best = it;
						bestD = d;
					}
				}
			}
			else
			{
				// 上 / 下最近的一行里横向最近的一张（跨段标题也行）
				float rowY = float.NaN;
				foreach (Item it in items)
				{
					float d = (it.At.Y - cur.At.Y) * dy;
					if (d > 1f && (float.IsNaN(rowY) || d < (rowY - cur.At.Y) * dy))
					{
						rowY = it.At.Y;
					}
				}
				foreach (Item it in items)
				{
					float d = Mathf.Abs(it.At.X - cur.At.X);
					if (!float.IsNaN(rowY) && Mathf.IsEqualApprox(it.At.Y, rowY) && d < bestD)
					{
						best = it;
						bestD = d;
					}
				}
			}
			if (best != null)
			{
				sel = best;
				Audio.Sfx.Clip("Sys_Cursor", 0.35f);
			}
		}

		void Nav(float now)
		{
			if (exiting)
			{
				return;
			}
			Viewport vp = root.GetViewport();
			Control? owner = vp.GuiGetFocusOwner();
			bool usingPad = Compat.UsingDirectionalNav();
			if (usingPad && owner != navFocus && (owner == null || !owner.IsVisibleInTree() || grid.IsAncestorOf(owner)))
			{
				navFocus.GrabFocus();                            // 游戏默认把焦点给原版网格的第一张牌（已藏起来）
				owner = navFocus;
				sel ??= items.FirstOrDefault();
			}
			bool pad = owner == navFocus && usingPad;          // 中途换回鼠标：交还给鼠标悬停
			BindBack(pad && focused != null && focusOn);
			if (!pad)
			{
				return;
			}
			if (focused != null)
			{
				if (focusOn && (Pressed("ui_select") || Pressed("ui_confirm")))
				{
					if (focused.Card.IsUpgradable)
					{
						FlipUpgrade();                           // 手柄：特写时确认 = 查看升级 / 翻回；返回键收回特写
					}
					else
					{
						Toggle(focused);
					}
				}
				return;
			}
			if (sel != null && !items.Contains(sel))
			{
				sel = items.FirstOrDefault();                    // 牌堆内容变了（Refill）
			}
			string? dir = null;
			int dx = 0, dy = 0;
			foreach ((string a, int x, int y) in dirs)
			{
				if (InputMap.HasAction(a) && Input.IsActionPressed(a))
				{
					(dir, dx, dy) = (a, x, y);
					break;
				}
			}
			if (dir == null)
			{
				heldDir = null;
			}
			else if (dir != heldDir)
			{
				heldDir = dir;
				repeatAt = now + 0.38f;
				Move(dx, dy);
			}
			else if (now >= repeatAt)
			{
				repeatAt = now + 0.09f;
				Move(dx, dy);
			}
			if (sel != null)
			{
				// 选中的牌保持在可见区里
				float visH = L.GridBottom - L.GridTop;
				float topY = sel.At.Y - L.CellH * 0.5f - HeaderH;   // 上面留一个段标题的高度
				if (topY < target) target = Mathf.Max(0f, topY);
				if (sel.At.Y + L.CellH * 0.5f > target + visH) target = Mathf.Min(MaxScroll(), sel.At.Y + L.CellH * 0.5f - visH);
				if (sel.Slot != null)
				{
					hovered = sel;
				}
				if (Pressed("ui_select") || Pressed("ui_confirm"))
				{
					Toggle(sel);
				}
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
		// v0.14.53 兜底：每帧的排版连续出错 3 次 → 拆掉这个界面、退回原版网格（不能留一堆没排好的牌）
		int tickFails = 0;
		void RunTick(Action body)
		{
			try
			{
				body();
				tickFails = 0;
			}
			catch (Exception e)
			{
				ModEntry.Report("pile tick", e);
				if (++tickFails >= 3)
				{
					tree.ProcessFrame -= Tick;
					BindBack(false);
					ModEntry.Warn("pile screen: layout keeps failing -> back to the vanilla grid");
					FreeFlip(flip);
					flip = null;
					Rollback(screen);
				}
			}
		}

		void Tick()
		{
			if (!GodotObject.IsInstanceValid(root) || !root.IsVisibleInTree())
			{
				return;
			}
			RunTick(() =>
			{
				SyncGridUpgradePreview();                      // 必须早于本帧分批建牌，不能沿用 _Ready 的默认选中值。
				float now = Now();
				float dt = Mathf.Clamp(now - lastNow, 0f, 0.1f);
				lastNow = now;
				// 特写进度：进 0.5s、退 0.42s
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
				float fe = Ease.InOutCubic(focusP);
				Vector2 focusCenter = focusAt * k;
				if (fx != null) Animate(fx, fe, focusP, focusOn, now, focusCenter, k);
				// 退场（v0.14.55）：0.42s —— 卡牌按顺序往下沉、淡出；标题往左退；圆盘和立绘往右滑出；水下圆孔缩回牌堆按钮；最后才真正关掉界面
				float ex = exiting ? Mathf.Clamp((now - exitT0) / ExitTime, 0f, 1f) : 0f;
				butterfly.Update(now, dt);
				if (exiting)
				{
					float ei = Ease.InCubic(ex);
					right.Position = new Vector2(820f * k * L.Side * ei, 0f);
					right.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(Mathf.Clamp((ex - 0.3f) / 0.7f, 0f, 1f)));
					water.SetShaderParameter("radius", diag * (1f - Ease.InOutCubic(Mathf.Clamp((ex - 0.15f) / 0.85f, 0f, 1f))));
					bubbles.Modulate = new Color(1f, 1f, 1f, 1f - ex);
					if (screen.GetNodeOrNull<CanvasItem>("BackButton") is CanvasItem backBtn)
					{
						backBtn.Modulate = new Color(1f, 1f, 1f, 1f - Ease.OutCubic(ex));
					}
					if (fx != null) fx.Layer.Modulate = new Color(1f, 1f, 1f, 1f - ex);
					if (flip != null)
					{
						flip.Node.Modulate = new Color(1f, 1f, 1f, 1f - ex);
					}
					if (ex >= 1f && !finalized)
					{
						finalized = true;
						Finalize(screen);
					}
				}
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
					float sy = L.GridTop + at.Y - scroll;
					return new Vector2(gx + at.X + (sy - L.GridTop) * L.Slant, sy) * k;
				}
				// v0.14.66（用户：稍微往下滚一点，上面那行牌就直接消失了）：按"露在网格外的比例"平滑淡出 ——
				// 整张在网格里 = 不透明；越过边界 65% 的高度（中心已出界 15%）才完全透明，中间用 smoothstep。
				// 区域最短 60px（段标题这种矮的也有足够的过渡）。
				float Fade(float top, float bottom)
				{
					float zone = Mathf.Max((bottom - top) * 0.65f, 60f * k);
					float a = Mathf.SmoothStep(0f, 1f, (top - (L.GridTop * k - zone)) / zone);
					float b = Mathf.SmoothStep(0f, 1f, ((L.GridBottom * k + zone) - bottom) / zone);
					return a * b;
				}

				foreach (Section s in sections)
				{
					Vector2 p = Screen(new Vector2(0f, s.Y + HeaderH * 0.5f));
					float sIn = s.Born < 0f ? 1f : Ease.OutCubic(Mathf.Clamp((now - s.Born) / 0.3f, 0f, 1f));   // 切换后新标题从左滑入
					s.Head.Position = p + new Vector2(-14f * k - 80f * k * (1f - sIn), 0f);
					s.Head.Modulate = new Color(1f, 1f, 1f, Fade(p.Y - 20f * k, p.Y + 20f * k) * Mathf.Clamp((now - 0.2f) / 0.25f, 0f, 1f) * sIn * (1f - fe) * (1f - ex));
					s.Head.Position += new Vector2(-60f * k * fe, 0f);
				}
				for (int i = dying.Count - 1; i >= 0; i--)
				{
					// 旧标题往右滑出、淡掉，然后删
					Section s = dying[i];
					float d = Mathf.Clamp((now - s.Die) / 0.25f, 0f, 1f);
					if (d >= 1f || !GodotObject.IsInstanceValid(s.Head))
					{
						if (GodotObject.IsInstanceValid(s.Head)) s.Head.QueueFree();
						dying.RemoveAt(i);
						continue;
					}
					Vector2 p = Screen(new Vector2(0f, s.Y + HeaderH * 0.5f));
					s.Head.Position = p + new Vector2(-14f * k + 120f * k * Ease.InCubic(d), 0f);
					s.Head.Modulate = new Color(1f, 1f, 1f, (1f - d) * Fade(p.Y - 20f * k, p.Y + 20f * k));
				}
				{
					float se = Mathf.Clamp((now - sweepT0) / 0.6f, 0f, 1f);
					sweep.Visible = se < 1f;
					if (se < 1f)
					{
						Vector2 sp = Screen(new Vector2(0f, scroll + (L.GridBottom - L.GridTop) * Ease.InOutCubic(se)));
						sweep.Position = sp;
						sweep.Color = new Color(0.55f, 0.9f, 1f, 0.7f * Mathf.Sin(Mathf.Pi * se));
						sweep.Rotation = Mathf.Atan(L.Slant);
					}
				}
				head.Modulate = new Color(1f, 1f, 1f, (1f - 0.85f * fe) * (1f - ex));
				if (exiting)
				{
					head.Position = new Vector2((L.RightSide ? 320f : -260f) * k * Ease.InCubic(ex), 0f);
				}
				Nav(now);
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
					float baseScale = L.CardW * k / cs.X;
					float half = cs.Y * baseScale * 0.5f;
					// v0.14.98 切换排列模式：从旧位置沿弧线飞到新位置（先慢后快再慢），途中略放大、转一点
					float move = 0f;
					Vector2 at = it.At;
					if (it.MoveT0 >= 0f)
					{
						float me = Mathf.Clamp((now - it.MoveT0 - it.MoveDelay) / 0.46f, 0f, 1f);
						float mc = Ease.InOutCubic(me);
						at = it.From.Lerp(it.At, mc);
						move = Mathf.Sin(Mathf.Pi * mc);
						if (me >= 1f) it.MoveT0 = -1f;
					}
					Vector2 p = Screen(at) + new Vector2(0f, -42f * k * move);
					float fade = Mathf.Max(Fade(p.Y - half, p.Y + half), move * 0.85f);
					// 入场（每个界面不同）：抽牌堆从左下弹入；牌组一列列从右侧滑入；弃牌堆从上方飘落（左右摆着转）；消耗堆由光点聚拢显形（从大到小、淡入）
					float born = Mathf.Clamp((now - it.Born) / (kind == Kind.Discard ? 0.6f : 0.38f), 0f, 1f);
					float pop = Ease.OutBack(born);
					float enterRot = 0f, enterScale = 1f;
					it.Lift = Mathf.MoveToward(it.Lift, it == hovered ? 1f : 0f, 0.12f);
					float lift = Ease.OutCubic(it.Lift);
					it.Dim = Mathf.MoveToward(it.Dim, any && it != hovered ? 0.55f : 1f, 0.07f);
					switch (kind)
					{
						case Kind.Deck:
							p += new Vector2(160f, 0f) * k * (1f - Ease.OutExpo(born));
							pop = 1f;
							break;
						case Kind.Discard:
							float fall = 1f - Ease.OutCubic(born);
							p += new Vector2(Mathf.Sin(born * Mathf.Pi * 2.2f) * 26f * fall, -190f * fall) * k;
							enterRot = Mathf.DegToRad(16f) * Mathf.Sin(born * Mathf.Pi * 2.2f) * fall;
							pop = 1f;
							break;
						case Kind.Exhaust:
							enterScale = 1f + 0.3f * (1f - Ease.OutCubic(born));
							pop = 1f;
							break;
						default:
							p += new Vector2(-40f, 46f) * k * (1f - pop);
							break;
					}
					p += new Vector2(0f, -14f) * k * lift;
					bool on = fade > 0.01f || it == hovered;
					it.Slot.Visible = on;
					if (!on)
					{
						continue;
					}
					float rot = Mathf.DegToRad(L.Slant < 0f ? 2f : L.Slant > 0f ? -2f : 0f) * (1f - lift) + enterRot;
					// 原版悬停会把 holder 自己的 Scale 从 0.8 跳到 1.0；这里按它当前的 Scale 反算，让总缩放平滑
					// 淡出中的牌稍微缩小一点（"退到远处"的虚化感），整张在网格里时不变
					float want = baseScale * (0.85f + 0.15f * pop) * enterScale * (1f + 0.26f * lift) * (0.93f + 0.07f * Mathf.Max(fade, lift)) * (1f + 0.07f * move);
					rot += Mathf.DegToRad(-4f) * move;
					float alpha = Mathf.Clamp(born * (kind == Kind.Exhaust ? 1.4f : 3f), 0f, 1f) * (it == hovered ? 1f : fade);
					if (it == focused)
					{
						// 特写：沿上拱的弧线飞到中间、放大，途中绕竖轴真正翻一圈（带透视：正面 → 塔罗牌背 → 正面），落定后轻轻浮动。
						// 飞行中显示的是翻转用的替身（这张牌的离屏渲染 + 牌背），落定 / 飞回原位时换回真正的卡牌
						Vector2 to = focusCenter + new Vector2(0f, Mathf.Sin(now * 1.6f) * 4f * k * fe);
						p = p.Lerp(to, fe) + new Vector2(0f, -170f * k) * Mathf.Sin(Mathf.Pi * fe);
						want = Mathf.Lerp(want, FocusW * k / cs.X, fe);
						rot = Mathf.Lerp(rot, 0f, fe) + Mathf.DegToRad(-8f) * Mathf.Sin(Mathf.Pi * fe);
						alpha = 1f;
						// 飞回原位的半路（背面朝前时）换回网格的状态（牌组勾了「查看升级」= 升级后，否则原版）
						bool gridUp = upgradeOn && it.Card.IsUpgradable;
						if (!focusOn && fe < 0.5f && focusUp != gridUp)
						{
							focusUp = gridUp;
							ShowFace(it, focusUp);
						}
						// 查看升级：在原地绕竖轴翻一整圈（0.6s），转到背面时换成升级后 / 升级前
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
							Audio.Sfx.Clip("ShuffleTime_Select", 0.35f);
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
								Pose(flip, upFlipping ? Mathf.Tau * Ease.InOutCubic(upE) : Mathf.Tau * fe);
							}
						}
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
					if (exiting)
					{
						// 按排列顺序依次下沉（每张晚 0.012 的进度，最多晚 0.35），边沉边缩小、转一点、淡出
						float r = Mathf.Clamp((ex - Mathf.Min(0.35f, it.Index * 0.012f)) / 0.65f, 0f, 1f);
						float ri = Ease.InCubic(r);
						// 退场也各不相同：牌组往右滑走；弃牌堆继续往下掉（转着）；消耗堆放大散去；抽牌堆往下沉
						switch (kind)
						{
							case Kind.Deck:
								p += new Vector2(300f, 0f) * k * ri;
								break;
							case Kind.Discard:
								p += new Vector2(30f, 280f) * k * ri;
								rot += Mathf.DegToRad(12f) * ri;
								break;
							case Kind.Exhaust:
								want *= 1f + 0.35f * ri;
								break;
							default:
								p += new Vector2(-20f, 130f) * k * ri;
								want *= 1f - 0.12f * ri;
								rot += Mathf.DegToRad(4f) * ri;
								break;
						}
						alpha *= 1f - Ease.OutCubic(r);
					}
					it.Slot.Position = p;
					it.Slot.Rotation = rot;
					float own = Mathf.Max(0.01f, h.Scale.X);
					it.Slot.Scale = Vector2.One * (want / own);
					float d = it == focused ? 1f : it.Dim;
					// 消耗堆的牌偏冷灰（已经被"用掉"的感觉）
					float cool = kind == Kind.Exhaust && it != focused ? 0.78f : 1f;
					it.Slot.Modulate = new Color((d * 0.9f + 0.1f) * cool, (d * 0.94f + 0.06f) * (cool + (1f - cool) * 0.4f), Mathf.Min(1f, d + 0.1f), alpha);
				}
				// 指到的那张移到最上层（绘制和鼠标判定都按节点顺序）
				if (any && hovered?.Slot is Control top && top.GetParent() == cards && top.GetIndex() != cards.GetChildCount() - 1)
				{
					cards.MoveChild(top, cards.GetChildCount() - 1);
				}
				// 名牌：放大那张牌的正下方
				// v0.14.53（Codex 编译警告 CS8602）：用显式的非空局部变量，节点失效就藏起名牌
				if (any && hovered is { Slot: Control ps, Holder: NGridCardHolder ph } hv && GodotObject.IsInstanceValid(ps) && GodotObject.IsInstanceValid(ph))
				{
					plate ??= NamePlate(root, k);
					plate.Visible = true;
					Vector2 cs = ph.Hitbox.Size.X > 1f ? ph.Hitbox.Size : new Vector2(300f, 422f);
					float half = cs.Y * ps.Scale.Y * ph.Scale.Y * 0.5f;
					plate.Position = ps.Position + new Vector2(-20f * k, half + 8f * k);
					SetPileTitle((Label)plate.GetChild(plate.GetChildCount() - 1), hv.Card.Title);
					float a = Ease.OutCubic(hv.Lift);
					plate.Modulate = new Color(1f, 1f, 1f, a);
					plate.Scale = new Vector2(Mathf.Lerp(0.7f, 1f, a), 1f);
				}
				else
				{
					if (plate != null) plate.Visible = false;
				}
			});
		}

		tree.ProcessFrame += Tick;
		ulong sid = screen.GetInstanceId();
		Exits[sid] = () =>
		{
			if (exiting)
			{
				return;
			}
			exiting = true;
			exitT0 = Now();
			butterfly.BeginExit(exitT0);
			// 退场期间继续动画，只在这个即将销毁的界面上禁用鼠标。
			// NGridCardHolder / Hitbox 会回对象池；改它们的 MouseFilter 会污染奖励、升级等后续界面。
			screen.ProcessMode = Node.ProcessModeEnum.Always;
			screen.MouseBehaviorRecursive = Control.MouseBehaviorRecursiveEnum.Disabled;
			catcher.Visible = false;
			if (plate != null) plate.Visible = false;
			hovered = null;
			BindBack(false);
			upBtn?.Hide();
			// v0.14.91（用户：进出音效没跟上动画）：这个嗖声前 0.36s 几乎静音、0.67s 最响，而退场只有 0.42s —— 从 0.3s 处开始播，最响处落在退场后段
			Audio.Sfx.Clip("ShuffleTime_Whoosh", 0.3f, from: WhooshSkip);
			// 保险：真实时间 1s 后无论如何关掉（每帧逻辑出问题也不会留着不关）
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
			if (kind == Kind.Draw || item) HeroStage.Close(right);
			Exits.Remove(sid);
			tree.ProcessFrame -= Tick;
			BindBack(false);
			pile.ContentsChanged -= onChanged;
			FreeFlip(flip);                                  // 特写中途关掉界面：翻转替身借的卡牌还回对象池
			flip = null;
		};
		// 同上跳过静音引子；再延到下一帧才播：建界面这一帧要几十毫秒，立刻播的话声音会抢在画面前面
		Audio.Sfx.Clip("ShuffleTime_Whoosh", 0.45f, delay: 0.001f, from: WhooshSkip);
		if (dive) DiveFx.Start(root, water, view);       // 水花在最上层，最后加
		lap("tail");
		GD.Print($"{ModEntry.Tag} perf pile {kind} build {(Time.GetTicksUsec() - buildStart) / 1000f:0.0}ms (decor {decorMs:0.0}ms;{laps}); idle warmup complete={IdleWarmup.Complete}");
	}

	// ── 点击特写 ──
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
		public float InfoBase;                    // 左侧说明整体的横向位置（牌组界面的说明在特写卡牌右边）
	}

	/// <summary>特写的背景层：压暗、穿过卡牌的 P3R 白色斜带（带红 / 青细条）、卡牌后面慢转的光芒和两道细圆、左侧说明（牌名 / 类型费用 / 同名张数）。</summary>
	private static Focus FocusLayer(Control parent, Vector2 view, float k, Font font, Vector2 center, float infoX)
	{
		Focus f = new() { Layer = new Node2D { Visible = false } };
		parent.AddChild(f.Layer);
		f.Dim = new ColorRect { Size = view, Color = new Color(0.01f, 0.03f, 0.13f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		f.Layer.AddChild(f.Dim);
		Vector2 c = center * k;
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
		f.InfoBase = (infoX - 60f) * k;
		f.Layer.AddChild(f.Info);
		f.Name = Text(f.Info, "", Typo.PileCjk, (int)(54f * k), Colors.White, new Vector2(60f, 300f) * k, (int)(6f * k), Ink);
		Fx.Poly(f.Info, new[] { new Vector2(60f, 382f) * k, new Vector2(400f, 382f) * k, new Vector2(394f, 390f) * k, new Vector2(54f, 390f) * k }, Red);
		f.Kind = Text(f.Info, "", font, (int)(40f * k), Cyan, new Vector2(60f, 398f) * k, (int)(4f * k), Ink);
		f.Copies = Text(f.Info, "", font, (int)(34f * k), Colors.White, new Vector2(60f, 448f) * k, (int)(4f * k), Ink);
		Text(f.Info, Zh ? "点击任意处返回" : "CLICK ANYWHERE TO RETURN", Zh ? Typo.PileCjk : font, (int)(20f * k), new Color(0.75f, 0.88f, 1f, 0.8f), new Vector2(62f, 508f) * k, (int)(3f * k), Ink);
		return f;
	}

	private static void Fill(Focus f, CardModel card, CardPile pile, float k)
	{
		SetPileTitle(f.Name, card.Title);
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
		f.Info.Position = new Vector2(f.InfoBase - 90f * k * (1f - Ease.OutCubic(ia)), 0f);
	}

	/// <summary>白色大圆盘：中心纯白、边缘淡青（顶点色渐变），三道同心细圆、外圈青 / 白刻度弧；立体大字只画在圆盘里面。</summary>
	private static void Disc(Node2D parent, string word, Font? font, float k, float rx)
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

	/// <summary>会动的立绘：Spine 待机动画（骨骼数据读不到就用静态图）。center = 画布中心的位置，scale = 画布缩放；返回装立绘的节点。</summary>
	private static Node2D SpinePortrait(Node2D parent, string spinePath, string texPath, Vector2 center, float scale, float canvasH)
	{
		Node2D holder = new() { Position = center, Scale = Vector2.One * scale };
		parent.AddChild(holder);
		Texture2D? tex = ResourceLoader.Exists(texPath) ? ResourceLoader.Load(texPath) as Texture2D : null;
		Resource? data = ResourceLoader.Exists(spinePath) ? ResourceLoader.Load(spinePath) : null;
		bool dataOk = data != null && (!data.HasMethod("is_skeleton_data_loaded") || data.Call("is_skeleton_data_loaded").AsBool());
		Node2D? spine = dataOk ? ClassDB.Instantiate("SpineSprite").AsGodotObject() as Node2D : null;
		if (spine != null)
		{
			spine.Position = new Vector2(0f, canvasH / 2f);            // 骨骼原点在画布底边中点
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
		return holder;
	}

	/// <summary>灰色立体大字（深灰投影 + 浅灰字身），旋转后放在 at（参考像素，左上角是字的起点）。</summary>
	private static void BigWord(Node parent, string word, Font? font, float k, Vector2 at, float rotDeg, float size, float alpha = 1f)
	{
		Label shadow = Text(parent, word, font, (int)(size * k), new Color(0.46f, 0.5f, 0.56f, alpha), at + new Vector2(22f, 18f) * k, 0, Ink);
		shadow.Rotation = Mathf.DegToRad(rotDeg);
		Label body = Text(parent, word, font, (int)(size * k), new Color(0.84f, 0.86f, 0.9f, alpha), at, (int)(3f * k), new Color(0.59f, 0.63f, 0.69f, alpha));
		body.Rotation = Mathf.DegToRad(rotDeg);
	}

	/// <summary>
	/// 牌组（仿 P3R STATUS）：左边一大块白色斜切板（右边是一道斜切口），横躺的脸部特写裁在板里；切口外贴深蓝斜条 + 青色细线 + 红色细线；
	/// 卡牌网格在右边。
	/// </summary>
	private static PileButterfly DeckDecor(Node2D parent, Vector2 view, Font? font, float k)
	{
		Vector2[] slabPts = { new(-80f, 70f), new(820f, 70f), new(600f, 1100f), new(-80f, 1100f) };
		// 切口外：深蓝宽斜条、青色细线、红色细线
		Vector2 top = new(820f, 70f), bottom = new(600f, 1100f);
		Vector2 n = new Vector2(1030f, 220f).Normalized();             // 切口的外法向（大致朝右）
		void Strip(float a, float b, Color c) => Fx.Poly(parent, new[] { (top + n * a) * k, (top + n * b) * k, (bottom + n * b) * k, (bottom + n * a) * k }, c);
		Strip(0f, 46f, new Color(0.03f, 0.07f, 0.24f, 0.92f));
		Strip(52f, 56f, Cyan);
		Strip(70f, 73f, Red);
		Polygon2D slab = new() { Polygon = slabPts.Select(p => p * k).ToArray(), Color = new Color(0.95f, 0.97f, 1f, 0.97f), ClipChildren = CanvasItem.ClipChildrenMode.AndDraw };
		parent.AddChild(slab);
		for (int i = -12; i <= 24; i++)
		{
			float o = i * 52f * k;
			slab.AddChild(new Line2D { Points = new[] { new Vector2(o, 0f), new Vector2(o - 1100f * k, 1100f * k) }, Width = 1.5f * k, DefaultColor = new Color(0.55f, 0.68f, 0.82f, 0.1f) });
		}
		PileButterfly butterfly = new(parent, Kind.Deck, k, 0f, DeckPorScale);
		// 白色底板 → 蓝蝶/亮粉 → 单独裁切的立绘；蓝蝶可越过斜边，人物仍按原版板形裁切。
		Polygon2D portraitClip = new() { Polygon = slab.Polygon, ClipChildren = CanvasItem.ClipChildrenMode.Only };
		parent.AddChild(portraitClip);
		SpinePortrait(portraitClip, "res://p3makoto/pile_deck/pile_deck_skel_data.tres", "res://p3makoto/ui/pile_deck.png", new Vector2(350f, 590f) * k, DeckPorScale * k, 1254f);
		return butterfly;
	}

	/// <summary>
	/// v0.14.101 弃牌堆 = 原版营地 ITEM 页（预览 v3 用户认可，tools/p3r/item_preview.py）：左上白板 + 淡灰大字 ITEM（原版图集原样裁出），
	/// 人物（原版 CampItemStart → Loop，填原版深海军蓝、不透水）压在白板上、整体左移给牌堆让位。打不开就返回 false，退回旧样式。
	/// </summary>
	private static bool ItemDecor(Node2D parent, Vector2 view, float k)
	{
		float u = view.X / 1920f;
		Node2D board = new() { Name = "ItemBoard", Modulate = new Color(1f, 1f, 1f, 0f) };
		parent.AddChild(board);
		Fx.Poly(board, new[] { new Vector2(0f, 0f) * k, new Vector2(960f, 0f) * k, new Vector2(930f, 150f) * k, new Vector2(0f, 150f) * k }, Colors.White);
		Fx.Poly(board, new[] { new Vector2(0f, 158f) * k, new Vector2(926f, 158f) * k, new Vector2(918f, 196f) * k, new Vector2(0f, 196f) * k }, new Color(18f / 255f, 70f / 255f, 190f / 255f, 200f / 255f));
		if (HeroStage.Tex("item_word.png") is Texture2D word)
		{
			board.AddChild(new Sprite2D { Texture = word, Centered = false, Position = new Vector2(40f, -60f) * k, Scale = Vector2.One * 2f * k, Modulate = new Color(214f / 255f, 219f / 255f, 228f / 255f) });
		}
		if (HeroStage.Open(parent, view, ItemShiftX * u, HeroStage.Act.Item))
		{
			// 原版 ITEM 页水光（Nami A / B）：屏幕上方的水面光斑，加亮叠在人物和水上（白板在它下面，白色加亮后不变；牌在它上面），人物和背景融在一起，不像贴上去的 3D 模型
			if (HeroStage.Tex(HeroStage.ItemWaterLight) is Texture2D light)
			{
				ShaderMaterial m = new() { Shader = Shaders.WaterLight };
				m.SetShaderParameter("atlas", light);
				parent.AddChild(new ColorRect { Size = new Vector2(view.X, view.Y * 768f / 1080f), Material = m, MouseFilter = Control.MouseFilterEnum.Ignore });
			}
			return true;
		}
		board.QueueFree();
		return false;
	}

	/// <summary>弃牌堆（仿 P3R ITEM）：右上、右下两块白色大三角 + 一道青色斜光片 + 一道红色细斜线；后仰下沉的立绘贴右边（大字会被立绘挡住，不放）。</summary>
	private static PileButterfly DiscardDecor(Node2D parent, Vector2 view, Font? font, float k, float rx)
	{
		Vector2 o = new(rx, 0f);
		Fx.Poly(parent, new[] { new Vector2(1300f, 1080f) * k + o, new Vector2(1360f, 1080f) * k + o, new Vector2(1760f, 330f) * k + o, new Vector2(1720f, 330f) * k + o }, new Color(0.55f, 0.88f, 1f, 0.35f));
		Fx.Poly(parent, new[] { new Vector2(1240f, 70f) * k + o, new Vector2(1920f, 70f) * k + o, new Vector2(1920f, 660f) * k + o }, new Color(0.95f, 0.97f, 1f, 0.95f));
		Fx.Poly(parent, new[] { new Vector2(1500f, 1080f) * k + o, new Vector2(1920f, 560f) * k + o, new Vector2(1920f, 1080f) * k + o }, new Color(0.95f, 0.97f, 1f, 0.93f));
		Fx.Poly(parent, new[] { new Vector2(1232f, 70f) * k + o, new Vector2(1240f, 70f) * k + o, new Vector2(1920f, 668f) * k + o, new Vector2(1920f, 676f) * k + o }, Red);
		PileButterfly butterfly = new(parent, Kind.Discard, k, rx, DiscardPorScale);
		SpinePortrait(parent, "res://p3makoto/pile_discard/pile_discard_skel_data.tres", "res://p3makoto/ui/pile_discard.png", new Vector2(1552f, 537f) * k + o, DiscardPorScale * k, 1536f);
		return butterfly;
	}

	/// <summary>
	/// 消耗堆（仿 P3R QUEST）：整屏再压暗一层深海蓝 + 细网格线（像深海里的坐标格），右边远处背对镜头漂走的小小身影，身边慢慢上升的青色光点；
	/// 竖排的半透明大字 EXHAUST 贴右边缘。
	/// </summary>
	private static PileButterfly ExhaustDecor(Control root, Node2D parent, Vector2 view, Font? font, float k, float rx)
	{
		ColorRect deep = new() { Size = view, Color = new Color(0.0f, 0.02f, 0.1f, 0.5f), MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(deep);
		root.MoveChild(deep, parent.GetIndex());
		Node2D gridLines = new();
		root.AddChild(gridLines);
		root.MoveChild(gridLines, parent.GetIndex());
		for (float x = 0f; x < view.X; x += 96f * k)
		{
			gridLines.AddChild(new Line2D { Points = new[] { new Vector2(x, 0f), new Vector2(x, view.Y) }, Width = 1f, DefaultColor = new Color(0.45f, 0.8f, 1f, 0.07f) });
		}
		for (float y = 0f; y < view.Y; y += 96f * k)
		{
			gridLines.AddChild(new Line2D { Points = new[] { new Vector2(0f, y), new Vector2(view.X, y) }, Width = 1f, DefaultColor = new Color(0.45f, 0.8f, 1f, 0.07f) });
		}
		Vector2 o = new(rx, 0f);
		BigWord(parent, "EXHAUST", font, k, new Vector2(1700f, 1100f) * k + o, -90f, 230f, 0.22f);
		PileButterfly butterfly = new(parent, Kind.Exhaust, k, rx, ExhaustPorScale);
		SpinePortrait(parent, "res://p3makoto/pile_exhaust/pile_exhaust_skel_data.tres", "res://p3makoto/ui/pile_exhaust.png", new Vector2(1500f, 560f) * k + o, ExhaustPorScale * k, 1536f);
		CpuParticles2D motes = new()
		{
			Position = new Vector2(1500f, 700f) * k + o,
			Amount = 22,
			Lifetime = 4.0,
			Preprocess = 3.0,
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
			EmissionRectExtents = new Vector2(200f, 260f) * k,
			Direction = new Vector2(0f, -1f),
			Spread = 20f,
			Gravity = new Vector2(0f, -4f) * k,
			InitialVelocityMin = 10f * k,
			InitialVelocityMax = 26f * k,
			ScaleAmountMin = 1.2f * k,
			ScaleAmountMax = 3f * k,
			Color = new Color(0.6f, 0.92f, 1f, 0.85f),
			ColorRamp = new Gradient { Offsets = new[] { 0f, 0.2f, 0.75f, 1f }, Colors = new[] { new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 0.6f), new Color(1f, 1f, 1f, 0f) } },
			Material = Fx.Additive,
		};
		parent.AddChild(motes);
		return butterfly;
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
	/// <summary>
	/// v0.14.90 原版 COMMU 页的白色斜块（左缘撕裂碎片 + 左侧灰影）。原版资源里没有它在屏幕上的摆放数据，
	/// 坐标是对着用户的原版截图（半尺寸 2000×920 截图里游戏画面 x 125~865、y 0~460，人物中心 x≈560）描的，
	/// 换算到我们的版式：人物中心对齐 HeroStage 的人物（屏幕中心 + HeroShiftX），按视口宽等比缩放。
	/// 返回白块本身（ClipChildren：人物阴影和 COMMU 字只出现在白块上）。
	/// </summary>
	private static Node2D CommuPanel(Node2D parent, Vector2 view)
	{
		float u = view.X / 1920f, s = 1080f / 460f;
		float ox = 960f + HeroShiftX * 1920f - (560f - 125f) * s;
		Vector2 P(float x, float y) => new(((x - 125f) * s + ox - 960f) * u + view.X / 2f, (y * s - 540f) * u + view.Y / 2f);
		Vector2[] pts = { P(705, 0), P(900, 0), P(900, 300), P(800, 460), P(262, 460), P(300, 425), P(275, 418), P(330, 370) };
		Vector2[] shadow = new Vector2[pts.Length];
		for (int i = 0; i < pts.Length; i++) shadow[i] = pts[i] + new Vector2(-30f, 6f) * u;
		Polygon2D shade = new() { Polygon = shadow, Color = new Color(150 / 255f, 158 / 255f, 175 / 255f) };
		parent.AddChild(shade);
		Polygon2D panel = new() { Polygon = pts, Color = Colors.White, ClipChildren = CanvasItem.ClipChildrenMode.AndDraw };
		panel.SetMeta("shadow", shade);
		parent.AddChild(panel);
		return panel;
	}

	/// <summary>原版 PLG 的 COMMU 字形（灰色，斜 57°），压在人物阴影上面。</summary>
	private static void CommuWord(Node2D panel, Vector2 view)
	{
		if (HeroStage.Tex("commu_word.png") is not Texture2D tex) return;
		float u = view.X / 1920f, s = 1080f / 460f;
		float ox = 960f + HeroShiftX * 1920f - (560f - 125f) * s;
		Vector2 at = new(((760f - 125f) * s + ox - 960f) * u + view.X / 2f, (300f * s - 540f) * u + view.Y / 2f);
		Sprite2D word = new() { Texture = tex, Position = at, Rotation = Mathf.DegToRad(-57f), Scale = Vector2.One * 0.7f * u };
		panel.AddChild(word);
		HeroStage.CutOut(word);   // 字在人物下面：人物剪影处不画（白块的裁剪不认白块材质里挖的空）
	}

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
	/// <summary>
	/// v0.14.98 抽牌堆排列模式切换条（标题说明条右边）：深蓝斜切底板，左「原版顺序」右「按类型」，
	/// 青色斜切高亮块滑到选中的一侧（OutExpo 0.32s），选中的字变深色；悬停整体提亮一点。点一下切换。
	/// </summary>
	private sealed class ModeSwitch
	{
		private const float W = 330f, H = 28f;
		public Action<bool>? Changed;
		private Node2D root = null!;
		private Polygon2D knob = null!;
		private Label left = null!, right = null!;
		private float k;
		private bool order;

		public static ModeSwitch Build(Node2D parent, Vector2 at, float k, Font? font, bool order, bool visible)
		{
			ModeSwitch m = new() { k = k, order = order };
			m.root = new Node2D { Position = at, Visible = visible };
			parent.AddChild(m.root);
			Fx.Poly(m.root, new[] { new Vector2(8f, 0f), new Vector2(W, 0f), new Vector2(W - 8f, H), new Vector2(0f, H) }.Select(p => p * k).ToArray(), Navy);
			m.knob = new Polygon2D { Color = Cyan, Polygon = new[] { new Vector2(8f, 0f), new Vector2(W / 2f, 0f), new Vector2(W / 2f - 8f, H), new Vector2(0f, H) }.Select(p => p * k).ToArray() };
			m.root.AddChild(m.knob);
			Font cjk = Zh ? Typo.PileCjk : font ?? Typo.PileCjk;
			m.left = Text(m.root, Zh ? "原版顺序" : "ORDER", cjk, (int)(17f * k), Colors.White, Vector2.Zero, 0, Ink);
			m.right = Text(m.root, Zh ? "按类型" : "BY TYPE", cjk, (int)(17f * k), Colors.White, Vector2.Zero, 0, Ink);
			foreach ((Label l, float cx) in new[] { (m.left, W * 0.25f), (m.right, W * 0.75f) })
			{
				l.HorizontalAlignment = HorizontalAlignment.Center;
				l.VerticalAlignment = VerticalAlignment.Center;
				l.Size = new Vector2(W / 2f * k, H * k);
				l.Position = new Vector2((cx - W / 4f) * k, 0f);
			}
			Control hit = new() { Size = new Vector2(W, H) * k, MouseFilter = Control.MouseFilterEnum.Stop, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
			m.root.AddChild(hit);
			hit.MouseEntered += () => m.root.Modulate = new Color(1.15f, 1.15f, 1.15f);
			hit.MouseExited += () => m.root.Modulate = Colors.White;
			hit.GuiInput += ev => ModEntry.Safe("pile mode switch", () =>
			{
				if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
				{
					hit.AcceptEvent();
					bool want = mb.Position.X < W * k / 2f;            // 点左半 = 原版顺序，点右半 = 按类型；点当前那一侧 = 切到另一侧
					m.Changed?.Invoke(want == m.order ? !m.order : want);
				}
			});
			m.Paint(1f);
			return m;
		}

		public void Set(bool toOrder)
		{
			order = toOrder;
			Fx.Animate(knob, 0.32, t => Paint(Ease.OutExpo(t)));
			Audio.Sfx.Clip("Sys_Cursor", 0.35f);
		}

		private void Paint(float t)
		{
			float from = order ? W / 2f : 0f, to = order ? 0f : W / 2f;
			knob.Position = new Vector2(Mathf.Lerp(from, to, t) * k, 0f);
			left.AddThemeColorOverride("font_color", order ? Ink : Ice);
			right.AddThemeColorOverride("font_color", order ? Ice : Ink);
		}
	}

	private static Node2D SectionHead(Node2D parent, string en, string zh, int count, Font? font, float k, float width)
	{
		Node2D n = new() { Modulate = new Color(1f, 1f, 1f, 0f) };
		parent.AddChild(n);
		float h = 44f;
		Fx.Poly(n, new[] { new Vector2(12f, -h / 2), new Vector2(330f, -h / 2), new Vector2(320f, h / 2), new Vector2(0f, h / 2) }.Select(p => p * k).ToArray(), new Color(0.02f, 0.03f, 0.09f, 0.94f));
		Fx.Poly(n, new[] { new Vector2(12f, -h / 2), new Vector2(24f, -h / 2), new Vector2(14f, h / 2), new Vector2(2f, h / 2) }.Select(p => p * k).ToArray(), Cyan);
		Label enL = Text(n, en, font, (int)(36f * k), Colors.White, new Vector2(34f, -24f) * k, 0, Ink);
		if (zh.Length > 0)
		{
			Text(n, zh, Typo.PileCjk, (int)(20f * k), Ice, new Vector2(44f * k + enL.GetMinimumSize().X, -14f * k), 0, Ink);
		}
		Label c = Text(n, "×" + count, font, (int)(38f * k), Cyan, new Vector2(250f, -25f) * k, 0, Ink);
		c.HorizontalAlignment = HorizontalAlignment.Right;
		c.Size = new Vector2(60f * k, c.Size.Y);
		n.AddChild(new Line2D { Points = new[] { new Vector2(338f, 0f) * k, new Vector2(width - 6f, 0f) * k }, Width = 2f * k, DefaultColor = new Color(0.6f, 0.86f, 1f, 0.55f) });
		Fx.Poly(n, new[] { new Vector2(width - 6f, -6f) * k, new Vector2(width + 6f, 0f) * k, new Vector2(width - 6f, 6f) * k }, new Color(0.6f, 0.86f, 1f, 0.8f));
		return n;
	}

	// ── 真实翻转：离屏渲染这张牌（SubViewport，1.5 倍分辨率）作正面，P3R 塔罗牌背作背面，贴在一条按透视投影的竖条网格上 ──
	private const string BackPath = "res://p3makoto/fx/pile_card_back.png";
	private const int FlipStrips = 14;
	private const float FlipRes = 1.5f;
	private const float FlipPadding = 48f;                 // 命中框不含费用圆标、标题飘带和阴影，四边留透明空间。

	private sealed class Flip
	{
		public SubViewport Vp = null!;
		public NCard? Card;
		public Node2D Node = null!;
		public Polygon2D Mesh = null!;
		public Texture2D? Back;
		public Vector2 Size;                      // 卡牌原始像素大小（holder 坐标）
	}

	private static Flip? MakeFlip(Control root, Control front, CardModel model, PileType pileType, NGridCardHolder? holder)
	{
		Vector2 size = holder != null && holder.Hitbox.Size.X > 1f ? holder.Hitbox.Size : new Vector2(300f, 422f);
		NCard? card = NCard.Create(model);
		if (card == null)
		{
			return null;
		}
		Vector2 captureSize = size + Vector2.One * (FlipPadding * 2f);
		SubViewport vp = new() { Size = (Vector2I)(captureSize * FlipRes).Ceil(), TransparentBg = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
		root.AddChild(vp);
		vp.AddChild(card);
		card.Position = captureSize * FlipRes / 2f;
		card.Scale = Vector2.One * FlipRes;
		card.UpdateVisuals(pileType, CardPreviewMode.Normal);
		Flip f = new() { Vp = vp, Card = card, Size = size, Back = ResourceLoader.Load(BackPath) as Texture2D };
		f.Node = new Node2D { Visible = false };
		front.AddChild(f.Node);
		f.Mesh = new Polygon2D { TextureFilter = CanvasItem.TextureFilterEnum.Linear };
		f.Node.AddChild(f.Mesh);
		Godot.Collections.Array polys = new();
		for (int i = 0; i < FlipStrips; i++)
		{
			polys.Add(new[] { i, i + 1, FlipStrips + 1 + i + 1, FlipStrips + 1 + i });
		}
		f.Mesh.Polygons = polys;
		return f;
	}

	/// <summary>绕竖轴转 theta（弧度）：每条竖线按 z = x·sinθ 做透视缩放；背面朝前时贴牌背（u 反过来，不是镜像），越接近侧面越暗。</summary>
	private static void Pose(Flip f, float theta)
	{
		float cam = f.Size.X * 2.6f;
		float c = Mathf.Cos(theta), sn = Mathf.Sin(theta);
		bool frontFace = c >= 0f;
		// 正面网格也扩到透明留边，否则即使画布放大，伸出命中框的装饰仍会被映射回主体矩形。
		// 背面继续使用原卡牌尺寸；透视距离、外层缩放和转轴均按主体尺寸，不把整张牌变大。
		float pad = frontFace ? FlipPadding * 2f : 0f;
		float w = f.Size.X + pad, h = f.Size.Y + pad;
		Texture2D? tex = frontFace ? f.Vp.GetTexture() : f.Back;
		if (tex == null)
		{
			tex = f.Vp.GetTexture();
		}
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
		f.Mesh.Color = new Color(light, light, Mathf.Min(1f, light + 0.05f));
	}

	private static void FreeFlip(Flip? f)
	{
		if (f == null)
		{
			return;
		}
		if (f.Card != null && GodotObject.IsInstanceValid(f.Card))
		{
			f.Card.QueueFreeSafely();                  // NCard 是对象池里的：还回池子
		}
		if (GodotObject.IsInstanceValid(f.Node))
		{
			f.Node.QueueFree();
		}
		if (GodotObject.IsInstanceValid(f.Vp))
		{
			f.Vp.QueueFree();
		}
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
		Label l = Text(n, "", Typo.PileCjk, (int)(32f * k), Colors.White, new Vector2(32f, 5f) * k, 0, Ink);
		l.ClipText = true;
		l.Size = new Vector2((w - 40f) * k, (h - 8f) * k);
		return n;
	}

	private static Node2D Bubbles(Control root, Vector2 view, float k)
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
		return layer;
	}

	internal const string CjkFontPath = "res://p3makoto/fonts/P3PileUI-Bold.otf";

	/// <summary>
	/// v0.14.91（实机日志：首开抽牌堆 head=70ms / focus=31ms，复开 1ms）：牌堆界面实际用到的每一组 字体 × 字号 × 描边 先栅格化，
	/// 和 Build / FocusLayer 里的写法一一对应（改那边的字号时这里一起改）。
	/// 小子集普通字体按需栅格化；只预热固定文案，不再遍历全卡名生成 MSDF。
	/// </summary>
	internal static List<(Font Font, int Size, int Outline, string Text, bool Cjk)> WarmTextJobs(float k)
	{
		const string latin = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 ·→×";
		var jobs = new List<(Font, int, int, string, bool)>
		{
			(Typo.PileBarlow, (int)(92f * k), (int)(5f * k), latin, false),           // 标题
			(Typo.PileBarlow, (int)(112f * k), (int)(5f * k), "0123456789", false),   // 张数
			(Typo.PileBarlow, (int)(36f * k), 0, latin, false),                 // 分段标题
			(Typo.PileBarlow, (int)(38f * k), 0, "0123456789×", false),         // 分段张数
			(Typo.PileBarlow, (int)(90f * k), 0, "EMPTY", false),
			(Typo.PileBarlow, (int)(40f * k), (int)(4f * k), latin, false),           // 特写：类型
			(Typo.PileBarlow, (int)(34f * k), (int)(4f * k), latin, false),           // 特写：张数
			(Typo.PileBarlow, (int)(19f * k), 0, latin, false),
			(Typo.PileBarlow, (int)(20f * k), (int)(3f * k), latin, false),
		};
		void Cjk(string chars, int size, int outline)
		{
			string uniq = new(chars.Distinct().Where(c => c > 0x2000).ToArray());
			int n = size >= 40 ? 1 : 2;   // 缩小不可中断的主线程调用；实际耗时继续由日志验证。
			for (int i = 0; i < uniq.Length; i += n)
				jobs.Add((Typo.PileCjk, size, outline, uniq.Substring(i, Math.Min(n, uniq.Length - i)), true));
		}
		Cjk("和原版抽牌堆同一顺序从左到右上往下按类型费用排列整副牌组本场战斗中消耗的最新在前弃堆", (int)(19f * k), 0); // 说明
		Cjk("原版顺序按类型", (int)(17f * k), 0);       // 排列开关：同样使用小型位图字体。
		Cjk("点击任意处返回", (int)(20f * k), (int)(3f * k));                                             // 特写：提示
		Cjk("攻击技能能力诅咒状态最新在前", (int)(20f * k), 0); // 段标题
		Cjk("查看升级已前不能", (int)(18f * k), 0);             // 升级按钮
		return jobs;
	}

	internal static void WarmText(Node parent, (Font Font, int Size, int Outline, string Text, bool Cjk) job)
	{
		Text(parent, job.Text, job.Font, job.Size, Colors.White, Vector2.Zero, job.Outline, Ink);
	}

	private static void SetPileTitle(Label label, string title)
	{
		if (label.Text == title) return;
		label.AddThemeFontOverride("font", Typo.PileCjkFor(title));
		label.Text = title;
	}

	private static Label Text(Node parent, string text, Font? font, int size, Color color, Vector2 at, int outline, Color outlineColor)
	{
		Label l = new() { Text = text, Position = at, MouseFilter = Control.MouseFilterEnum.Ignore };
		if (font == Typo.PileCjk) font = Typo.PileCjkFor(text);
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

	/// <summary>
	/// v0.14.65 特写里的「查看升级」按钮：深蓝斜切条 + 左边青色翻转箭头图标 + 文字；悬停白底深蓝字（P3R 选中行）。
	/// 文字：查看升级（可多次升级的牌写「查看升级 +N」）/ 查看升级前；满级的牌灰色「已升级」不能点。
	/// </summary>
	private sealed class UpgradeButton
	{
		internal Node2D Root = null!;
		internal Control Hit = null!;
		private Polygon2D _sel = null!;
		private Node2D _icon = null!;
		private Label _text = null!;
		private bool _mouse, _enabled, _up, _multi;
		private int _level;
		private float _k;

		internal static UpgradeButton Build(Control parent, float k, Font font)
		{
			UpgradeButton b = new() { _k = k };
			float w = 214f * k, h = 34f * k, lean = 9f * k;
			b.Root = new Node2D { Name = "P3UpgradeButton", Visible = false };
			parent.AddChild(b.Root);
			Fx.Poly(b.Root, new[] { new Vector2(lean + 5f * k, 5f * k), new Vector2(w + lean + 5f * k, 5f * k), new Vector2(w + 5f * k, h + 5f * k), new Vector2(5f * k, h + 5f * k) }, new Color(0f, 0f, 0.03f, 0.5f));
			Fx.Poly(b.Root, new[] { new Vector2(lean, 0f), new Vector2(w + lean, 0f), new Vector2(w, h), new Vector2(0f, h) }, Navy);
			b._sel = Fx.Poly(b.Root, new[] { new Vector2(lean, 0f), new Vector2(w + lean, 0f), new Vector2(w, h), new Vector2(0f, h) }, new Color(0.96f, 0.98f, 1f));
			Fx.Poly(b.Root, new[] { new Vector2(0f, h - 3f * k), new Vector2(w, h - 3f * k), new Vector2(w, h), new Vector2(0f, h) }, Red);
			// 图标：一个青色斜方块里两道弧形箭头（"翻转"）
			b._icon = new Node2D { Position = new Vector2(24f * k, h * 0.5f) };
			b.Root.AddChild(b._icon);
			float r = 9f * k;
			Vector2[] Arc(float a0, float a1)
			{
				Vector2[] pts = new Vector2[9];
				for (int i = 0; i < 9; i++)
				{
					float a = Mathf.Lerp(a0, a1, i / 8f);
					pts[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
				}
				return pts;
			}
			Fx.Stroke(b._icon, Arc(-2.6f, 0.2f), Cyan, 2.4f * k, additive: false);
			Fx.Stroke(b._icon, Arc(0.54f, 3.34f), Cyan, 2.4f * k, additive: false);
			Fx.Poly(b._icon, new[] { new Vector2(r + 4f * k, 0f), new Vector2(r - 4f * k, 0f), new Vector2(r, 5f * k) }.Select(v => v.Rotated(0.2f)).ToArray(), Cyan);
			Fx.Poly(b._icon, new[] { new Vector2(-r - 4f * k, 0f), new Vector2(-r + 4f * k, 0f), new Vector2(-r, -5f * k) }.Select(v => v.Rotated(0.2f)).ToArray(), Cyan);
			b._text = Text(b.Root, "", Zh ? Typo.PileCjk : font, (int)(18f * k), Colors.White, new Vector2(42f * k, h * 0.5f - 13f * k), 0, Ink);
			b.Hit = new Control { Size = new Vector2(w + lean, h), MouseFilter = Control.MouseFilterEnum.Stop, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
			b.Root.AddChild(b.Hit);
			b.Hit.MouseEntered += () => { b._mouse = true; b.Refresh(); };
			b.Hit.MouseExited += () => { b._mouse = false; b.Refresh(); };
			return b;
		}

		/// <summary>特写开始时：这张能不能升级、是不是已经升级过、现在显示的是哪一面。</summary>
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
			if (GodotObject.IsInstanceValid(Root))
			{
				Root.Visible = false;
				_enabled = false;
				_mouse = false;
			}
		}

		internal void Set(bool showingUp)
		{
			_up = showingUp;
			Refresh();
		}

		/// <summary>每帧：放到特写卡牌右下角外侧，按特写进度淡入。没落定时不接收点击。</summary>
		internal void Place(Vector2 at, float alpha)
		{
			if (!Root.Visible)
			{
				return;
			}
			Root.Position = at;
			Root.Modulate = new Color(1f, 1f, 1f, alpha * (_enabled ? 1f : 0.55f));
			Hit.MouseFilter = alpha > 0.9f && _enabled ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore;
		}

		private void Refresh()
		{
			if (!GodotObject.IsInstanceValid(Root))
			{
				return;
			}
			bool lit = _mouse && _enabled;
			_sel.Visible = lit;
			_icon.Visible = _enabled;
			string text = !_enabled ? (Zh ? "已升级" : "UPGRADED")
				: _up ? (Zh ? "查看升级前" : "VIEW BASE")
				: _multi ? (Zh ? $"查看升级 +{_level}" : $"VIEW UPGRADE +{_level}")
				: (Zh ? "查看升级" : "VIEW UPGRADE");
			_text.Text = text;
			_text.Position = new Vector2((_enabled ? 42f : 18f) * _k, _text.Position.Y);
			_text.AddThemeColorOverride("font_color", lit ? new Color(0.03f, 0.07f, 0.24f) : Colors.White);
		}
	}

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
