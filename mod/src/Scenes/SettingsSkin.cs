using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using P3Makoto.Core;
using P3Makoto.Vfx;

namespace P3Makoto.Scenes;

/// <summary>
/// v0.14.67 设置界面整体重绘：仿 P3R 主菜单（ref_sheets/ref3_overview_8 的 51.67~53.5s，用户截图 art/p3menu_system/ref）。
/// 原版设置逻辑一行不改（分页、每一行设置、开关 / 滑条 / 下拉框 / 翻页器、手柄导航、LB/RB 换页、返回键都是原版的），只换外观：
/// · 背景：P3R 水下（亮青水面焦散 → 钴蓝 → 深蓝、光柱）+ 左边白色面板（右边缘就是人物轮廓）+ 倒挂入水的结城理（着色器让人物的白色在水那一侧透出水，
///   头发 / 飘带随水微动，定时眨眼）+ 最左边竖排黑色大字 SYSTEM（灰色错影）+ 上浮气泡；左上信息框、右下当前分页提示。
/// · 分页：原版顶部的分页按钮藏起来，换成画面中间斜排的 P3R 菜单大字 GENERAL / GRAPHICS / SOUND / INPUT / P3MAKOTO；
///   当前项黑字 + 红色斜切条 + 右端白色小旗（同 SKILL），其余浅青；点击时从那个字涨出深蓝圆形盖住内容区，换页后再退开（同 53.33s）。
///   P3MAKOTO = 常规页里只显示本 mod 的那一栏。
/// · 内容区：原版的滚动区整体缩放、移到右侧，背后一块斜切深蓝面板（青色上沿、红色下沿、页名标题和超大水印字）；
///   每一行设置垫一条斜切深蓝底，悬停 / 手柄选中时变白底 + 错开的红框（P3R 选中行）；文字换思源黑体 Bold（中文）/ Barlow（英文）白字；
///   勾选框换成 P3R 斜方框；其余控件贴图按亮度映射成深蓝 → 钴蓝 → 冰白。
/// · 打开时：水花从右往左泼过整屏（51.67s），白面板从左滑入、人物往下沉入、菜单字依次飞入、内容区淡入。
/// 设置里「设置界面（P3R 风格）」关掉后，下次打开恢复原版（所有改动都有记录、可还原）。
/// </summary>
public static class SettingsSkin
{
	private const string RootName = "P3SystemSkin";
	private static readonly Color Ink = new(0.03f, 0.04f, 0.10f);
	private static readonly Color Navy = new(0.03f, 0.08f, 0.28f, 0.86f);
	private static readonly Color Cyan = new(0.38f, 0.84f, 0.96f);
	private static readonly Color Pale = new(0.62f, 0.88f, 1f);
	private static readonly Color Red = new(0.90f, 0.10f, 0.20f);
	private static readonly Color White = new(0.97f, 0.98f, 1f);

	// 人物右侧轮廓（art/p3menu_system/meta.json 的 water_boundary_polyline，1920×1080 像素），白色面板和融水着色器共用
	private static readonly Vector2[] Edge =
	{
		new(1014f, 0f), new(947f, 108f), new(883f, 216f), new(828f, 324f), new(789f, 432f), new(852f, 540f),
		new(859f, 648f), new(906f, 756f), new(968f, 864f), new(977f, 972f), new(800f, 1080f),
	};

	private sealed class Tab
	{
		internal string En = "", Zh = "";
		internal NSettingsTab? Vanilla;                    // null = P3MAKOTO（常规页里只显示本 mod 那一栏）
		internal NSettingsPanel? Panel;
		internal Control Hit = null!;
		internal Node2D Node = null!, Sel = null!;
		internal Label Text = null!, Shadow = null!;
		internal float Hover, SelK;
		internal bool Mouse;
	}

	private sealed class RowDeco
	{
		internal Control Row = null!;
		internal Node2D Deco = null!, Normal = null!, Selected = null!;
		internal Vector2 Built = new(-1f, -1f);
		internal float K;
		internal List<CanvasItem> Labels = new();
	}

	private sealed class State
	{
		internal NSettingsScreen Screen = null!;
		internal NSettingsTabManager Manager = null!;
		internal NScrollableContainer Scroll = null!;
		internal Control Root = null!, Words = null!, Top = null!;
		internal TextureRect Portrait = null!;
		internal Texture2D? Open, Half, Closed;
		internal Node2D PanelDeco = null!, Splash = null!, Wipe = null!, Bubbles = null!;
		internal Label PanelTitle = null!, PanelWater = null!, HintZh = null!, HintEn = null!;
		internal Node2D WhitePanel = null!, SystemWord = null!;
		internal readonly List<Tab> Tabs = new();
		internal readonly List<RowDeco> Rows = new();
		internal readonly List<Action> Undo = new();
		internal readonly HashSet<ulong> Styled = new();
		internal bool On, P3Page;
		internal int Current = -1, Pending = -1;
		internal float T0, WipeT0 = -1f, NextBlink, BlinkT0 = -1f;
		internal Vector2 WipeAt;
		internal Vector2 OrigPos, OrigScale;
		internal Rect2 Src;
		internal bool SrcValid;
		internal Color OrigModulate;
		internal Action? Tick;
	}

	private static readonly Dictionary<ulong, State> States = new();

	private static bool Zh => TranslationServer.GetLocale().StartsWith("zh");
	private static float Now => Time.GetTicksMsec() / 1000f;
	private static Font LabelFont => Zh ? Typo.CjkBold : Typo.Barlow;

	/// <summary>NSettingsScreen._Ready 之后挂上（本 mod 的设置那一栏已经建好）。真正换皮在每次显示时按开关决定。</summary>
	public static void Attach(NSettingsScreen screen)
	{
		ulong id = screen.GetInstanceId();
		if (States.ContainsKey(id) || screen.GetNodeOrNull<NSettingsTabManager>("%SettingsTabManager") is not NSettingsTabManager manager
			|| manager.GetNodeOrNull<NScrollableContainer>("%ScrollContainer") is not NScrollableContainer scroll)
		{
			return;
		}
		State st = new() { Screen = screen, Manager = manager, Scroll = scroll };
		States[id] = st;
		screen.VisibilityChanged += () => ModEntry.Safe("settings skin visible", () => OnVisible(st));
		screen.TreeExiting += () =>
		{
			States.Remove(id);
			StopTick(st);
		};
		if (screen.IsVisibleInTree())
		{
			OnVisible(st);
		}
	}

	private static void OnVisible(State st)
	{
		if (!GodotObject.IsInstanceValid(st.Screen))
		{
			return;
		}
		if (!st.Screen.Visible)
		{
			StopTick(st);
			return;
		}
		bool want = P3Settings.SettingsSkin;
		if (want && !st.On)
		{
			try
			{
				Build(st);
			}
			catch (Exception e)
			{
				ModEntry.Report("settings skin build", e);
				Restore(st);
				return;
			}
		}
		else if (!want && st.On)
		{
			Restore(st);
		}
		if (st.On)
		{
			st.T0 = Now;                                         // 每次打开都重播入场
			StartTick(st);
		}
	}

	// ── 构建 / 还原 ──

	private static void Build(State st)
	{
		NSettingsScreen screen = st.Screen;
		st.On = true;
		Vector2 view = screen.GetViewportRect().Size;
		float k = view.Y / 1080f;

		// 1. 原版的装饰图层藏起来（只藏纯装饰；带按钮 / 滚动区 / 返回键的节点只把自身的贴图设透明）
		HideDecor(st, screen);

		// 2. 背景层（放在最底下）
		st.Root = new Control { Name = RootName, MouseFilter = Control.MouseFilterEnum.Ignore };
		st.Root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		screen.AddChild(st.Root);
		screen.MoveChild(st.Root, 0);
		st.Undo.Add(() => { if (GodotObject.IsInstanceValid(st.Root)) st.Root.QueueFree(); });

		ShaderMaterial water = new() { Shader = Shaders.CampWater };
		water.SetShaderParameter("aspect", view.X / view.Y);
		ColorRect waterRect = new() { Material = water, MouseFilter = Control.MouseFilterEnum.Ignore };
		waterRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		st.Root.AddChild(waterRect);
		st.Bubbles = new Node2D();
		st.Root.AddChild(st.Bubbles);
		for (int i = 0; i < 22; i++)
		{
			Sprite2D b = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Position = new Vector2(Fx.Rand(0.4f, 1f) * view.X, Fx.Rand(0f, 1f) * view.Y) };
			b.SetMeta("v", Fx.Rand(22f, 60f) * k);
			b.SetMeta("p", Fx.Rand(0f, 6.28f));
			b.SetMeta("s", Fx.Rand(0.04f, 0.11f) * k);
			st.Bubbles.AddChild(b);
		}

		// 白色面板：左边到人物右轮廓，边缘一圈很淡的白色渐隐（不是硬边）
		float pw = view.Y * 16f / 9f, sx = pw / 1920f, sy = view.Y / 1080f;
		st.WhitePanel = new Node2D();
		st.Root.AddChild(st.WhitePanel);
		List<Vector2> poly = new() { new Vector2(-40f, -10f) };
		poly.AddRange(Edge.Select(e => new Vector2(e.X * sx, e.Y * sy)));
		poly.Add(new Vector2(-40f, view.Y + 10f));
		st.WhitePanel.AddChild(new Polygon2D { Polygon = poly.ToArray(), Color = Colors.White });
		Vector2[] edgeLine = Edge.Select(e => new Vector2(e.X * sx, e.Y * sy)).ToArray();
		st.WhitePanel.AddChild(new Line2D { Points = edgeLine.Select(p => p + new Vector2(10f * k, 0f)).ToArray(), Width = 26f * k, DefaultColor = new Color(1f, 1f, 1f, 0.32f), JointMode = Line2D.LineJointMode.Round, Antialiased = true });
		st.WhitePanel.AddChild(new Line2D { Points = edgeLine.Select(p => p + new Vector2(28f * k, 0f)).ToArray(), Width = 18f * k, DefaultColor = new Color(0.85f, 0.97f, 1f, 0.18f), JointMode = Line2D.LineJointMode.Round, Antialiased = true });

		// 竖排大字 SYSTEM（灰色错影 + 黑字，从下往上读，被左边缘裁掉一点）
		st.SystemWord = new Node2D { Position = new Vector2(-34f * k, view.Y + 40f * k), Rotation = -Mathf.Pi / 2f };
		st.Root.AddChild(st.SystemWord);
		Typo.Text(st.SystemWord, "SYSTEM", Typo.Barlow, (int)(300f * k), new Color(0.58f, 0.61f, 0.66f), new Vector2(40f * k, -16f * k));
		Typo.Text(st.SystemWord, "SYSTEM", Typo.Barlow, (int)(300f * k), Ink, Vector2.Zero);

		// 倒挂立绘（融水着色器）
		st.Open = Load("res://p3makoto/ui/system_portrait.png");
		st.Half = Load("res://p3makoto/ui/system_portrait_half.png");
		st.Closed = Load("res://p3makoto/ui/system_portrait_closed.png");
		ShaderMaterial pm = new() { Shader = Shaders.CampPortrait };
		pm.SetShaderParameter("edge", Edge.Select(e => new Vector2(e.X / 1920f, e.Y / 1080f)).ToArray());
		st.Portrait = new TextureRect { Texture = st.Open, Material = pm, MouseFilter = Control.MouseFilterEnum.Ignore, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, Size = new Vector2(pw, view.Y) };
		st.Root.AddChild(st.Portrait);
		st.NextBlink = Now + 2.5f;

		// 左上信息框（仿金额框）、右下当前分页提示
		Node2D info = new() { Position = new Vector2(150f, 34f) * k };
		st.Root.AddChild(info);
		Fx.Poly(info, new[] { new Vector2(0f, 0f), new Vector2(250f, 0f) * k, new Vector2(250f, 74f) * k, new Vector2(0f, 74f) * k }, Colors.White);
		Fx.StrokeClosed(info, new[] { new Vector2(0f, 0f), new Vector2(250f, 0f) * k, new Vector2(250f, 74f) * k, new Vector2(0f, 74f) * k }, Ink, 2f * k, additive: false);
		Typo.Text(info, "P3MAKOTO", Typo.Barlow, (int)(34f * k), Ink, new Vector2(12f, 2f) * k);
		Typo.Text(info, "system settings · v" + ModVersion, Typo.Latin, (int)(14f * k), new Color(0.2f, 0.22f, 0.3f), new Vector2(14f, 46f) * k);
		Node2D hint = new() { Position = new Vector2(view.X - 330f * k, view.Y - 84f * k) };   // 内容区下沿之下
		st.Root.AddChild(hint);
		st.HintZh = Typo.Text(hint, "", Typo.CjkBold, (int)(26f * k), Colors.White, new Vector2(0f, 0f), outline: (int)(4f * k), outlineColor: new Color(0.02f, 0.08f, 0.3f));
		Fx.Poly(hint, new[] { new Vector2(0f, 40f) * k, new Vector2(300f, 40f) * k, new Vector2(300f, 42f) * k, new Vector2(0f, 42f) * k }, Colors.White);
		st.HintEn = Typo.Text(hint, "", Typo.Barlow, (int)(17f * k), new Color(0.85f, 0.95f, 1f), new Vector2(0f, 42f) * k);

		// 3. 内容区：原版滚动区整体缩放、移到右侧；背后垫 P3R 面板
		st.OrigPos = st.Manager.Position;
		st.OrigScale = st.Manager.Scale;
		st.OrigModulate = st.Manager.Modulate;
		st.Undo.Add(() =>
		{
			if (GodotObject.IsInstanceValid(st.Manager))
			{
				st.Manager.Scale = st.OrigScale;
				st.Manager.Position = st.OrigPos;
				st.Manager.Modulate = st.OrigModulate;
			}
		});
		st.PanelDeco = new Node2D { Name = "P3PanelDeco" };
		st.Manager.AddChild(st.PanelDeco);
		st.Manager.MoveChild(st.PanelDeco, 0);
		st.Undo.Add(() => { if (GodotObject.IsInstanceValid(st.PanelDeco)) st.PanelDeco.QueueFree(); });
		st.PanelTitle = Typo.Text(st.PanelDeco, "", Typo.Barlow, 64, Colors.White, Vector2.Zero, outline: 6, outlineColor: new Color(0.02f, 0.08f, 0.3f));
		st.PanelWater = Typo.Text(st.PanelDeco, "", Typo.Barlow, 230, new Color(0.7f, 0.9f, 1f, 0.07f), Vector2.Zero);

		// 4. 分页大字
		BuildTabs(st, view, k);

		// 5. 圆形转场、入场水花（最上层，但在返回键之下）
		st.Top = new Control { Name = "P3SystemTop", MouseFilter = Control.MouseFilterEnum.Ignore };
		st.Top.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		screen.AddChild(st.Top);
		if (screen.GetNodeOrNull<Control>("BackButton") is Control back)
		{
			screen.MoveChild(st.Top, back.GetIndex());
		}
		st.Undo.Add(() => { if (GodotObject.IsInstanceValid(st.Top)) st.Top.QueueFree(); });
		st.Wipe = new Node2D { Visible = false };
		st.Top.AddChild(st.Wipe);
		st.Wipe.AddChild(new Polygon2D { Polygon = Circle(1f, 64), Color = new Color(0.04f, 0.12f, 0.46f, 0.97f) });
		st.Wipe.AddChild(new Line2D { Points = Circle(1f, 64), Closed = true, Width = 0.012f, DefaultColor = new Color(0.55f, 0.9f, 1f, 0.9f), Antialiased = true });
		st.Splash = new Node2D { Visible = false };
		st.Top.AddChild(st.Splash);
		for (int i = 0; i < 30; i++)
		{
			float r = Fx.Rand(70f, 260f) * k;
			Polygon2D blob = new() { Polygon = Circle(r, 28), Color = i % 3 == 0 ? new Color(1f, 1f, 1f, 0.95f) : new Color(0.55f, 0.88f, 1f, 0.92f) };
			blob.SetMeta("y", Fx.Rand(-0.1f, 1.1f) * view.Y);
			blob.SetMeta("d", Fx.Rand(0f, 0.12f));
			blob.SetMeta("r", r);
			st.Splash.AddChild(blob);
		}

		// 6. 每一行设置的样式（四个分页一起处理；之后新出现的行由 Tick 补）
		StyleRows(st);
		// 滚动条贴图也换成 P3R 蓝
		if (st.Scroll.GetNodeOrNull<Control>("Scrollbar") is Control bar)
		{
			StyleTree(st, bar, new RowDeco { Row = bar });
		}
	}

	private static string ModVersion => ModEntry.Version;

	private static Texture2D? Load(string path) => ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;

	private static void Restore(State st)
	{
		StopTick(st);
		try { SetP3Page(st, false, force: true); } catch (Exception e) { ModEntry.Report("settings skin restore page", e); }
		for (int i = st.Undo.Count - 1; i >= 0; i--)
		{
			try { st.Undo[i](); } catch (Exception e) { ModEntry.Report("settings skin restore", e); }
		}
		st.Undo.Clear();
		st.Tabs.Clear();
		st.Rows.Clear();
		st.Styled.Clear();
		st.On = false;
		st.SrcValid = false;
		st.P3Page = false;
		st.Current = -1;
	}

	/// <summary>
	/// 原版设置界面的装饰（背景图、边框、遮罩等）：整棵子树里没有任何可交互 / 关键节点的 → 直接隐藏；
	/// 含有关键节点（分页管理器、返回键、提示条、按钮、滚动区）的 → 只把它自己的贴图 / 底色设透明，再往下找。
	/// </summary>
	private static void HideDecor(State st, Node parent)
	{
		foreach (Node c in parent.GetChildren())
		{
			if (c is not CanvasItem ci || c.Name.ToString().StartsWith("P3", StringComparison.Ordinal))
			{
				continue;
			}
			// 分页按钮单独处理；滚动区内部不碰（里面可能有用作裁切遮罩的贴图，设透明会把整个内容藏掉）
			if (c is NBackButton || c is NSettingsToast || c is NSettingsTab || c is NScrollableContainer)
			{
				continue;
			}
			if (c == st.Manager)
			{
				Neutralise(st, ci);
				HideDecor(st, c);                                   // 管理器自己的背景 / 边框
				continue;
			}
			if (Essential(c))
			{
				Neutralise(st, ci);
				HideDecor(st, c);
			}
			else if (ci.Visible)
			{
				ci.Visible = false;
				st.Undo.Add(() => { if (GodotObject.IsInstanceValid(ci)) ci.Visible = true; });
			}
		}
	}

	/// <summary>
	/// v0.14.68（实机：整屏持续压暗）：含关键节点的祖先只把"它自己画的东西"去掉 ——
	/// 自身贴图 / 底色设透明；**着色器材质去掉**（原版背景是读屏幕的模糊 + 压暗着色器，它不受透明度影响，会把画在它前面的水下背景 / 立绘模糊压暗后再铺一层）；
	/// 裁切遮罩关掉（原版顶部的渐隐遮罩，换位置后会切掉内容）。都记录、可还原。
	/// </summary>
	private static void Neutralise(State st, CanvasItem ci)
	{
		if (ci is TextureRect or NinePatchRect or ColorRect or Panel or PanelContainer)
		{
			Color old = ci.SelfModulate;
			ci.SelfModulate = new Color(1f, 1f, 1f, 0f);
			st.Undo.Add(() => { if (GodotObject.IsInstanceValid(ci)) ci.SelfModulate = old; });
		}
		if (ci.Material != null)
		{
			Material m = ci.Material;
			ci.Material = null;
			st.Undo.Add(() => { if (GodotObject.IsInstanceValid(ci)) ci.Material = m; });
		}
		if (ci.ClipChildren != CanvasItem.ClipChildrenMode.Disabled)
		{
			CanvasItem.ClipChildrenMode old = ci.ClipChildren;
			ci.ClipChildren = CanvasItem.ClipChildrenMode.Disabled;
			st.Undo.Add(() => { if (GodotObject.IsInstanceValid(ci)) ci.ClipChildren = old; });
		}
	}

	private static bool Essential(Node n)
	{
		if (n is NSettingsTabManager or NBackButton or NSettingsToast or NClickableControl or BaseButton or Godot.Range or ScrollContainer or NScrollableContainer or LineEdit)
		{
			return true;
		}
		foreach (Node c in n.GetChildren())
		{
			if (Essential(c))
			{
				return true;
			}
		}
		return false;
	}

	// ── 分页大字 ──

	private static void BuildTabs(State st, Vector2 view, float k)
	{
		(string Name, string En, string Zh, string Panel)[] defs =
		{
			("General", "GENERAL", "常规", "%GeneralSettings"), ("Graphics", "GRAPHICS", "画面", "%GraphicsSettings"),
			("Sound", "SOUND", "声音", "%SoundSettings"), ("Input", "INPUT", "按键", "%InputSettings"),
		};
		foreach ((string name, string en, string zh, string panel) in defs)
		{
			NSettingsTab? tab = st.Manager.GetNodeOrNull<NSettingsTab>(name);
			NSettingsPanel? p = st.Manager.GetNodeOrNull<NSettingsPanel>(panel);
			if (tab == null || p == null)
			{
				continue;
			}
			st.Tabs.Add(new Tab { En = en, Zh = zh, Vanilla = tab, Panel = p });
			// 原版分页按钮藏起来（手柄 LB/RB 仍由原版处理；我们的大字跟着当前页走）
			Color old = tab.Modulate;
			Control.MouseFilterEnum oldF = tab.MouseFilter;
			tab.Modulate = new Color(1f, 1f, 1f, 0f);
			tab.MouseFilter = Control.MouseFilterEnum.Ignore;
			SetMouseRecursive(tab, false);
			st.Undo.Add(() =>
			{
				if (GodotObject.IsInstanceValid(tab))
				{
					tab.Modulate = old;
					tab.MouseFilter = oldF;
					SetMouseRecursive(tab, true);
				}
			});
		}
		if (st.Tabs.Count > 0 && st.Tabs[0].Panel?.Content?.GetNodeOrNull("P3MakotoSettings") != null)
		{
			st.Tabs.Add(new Tab { En = "P3MAKOTO", Zh = "结城理演出", Vanilla = null, Panel = st.Tabs[0].Panel });
		}
		foreach (string icon in new[] { "LeftTabIcon", "RightTabIcon" })
		{
			if (st.Manager.GetNodeOrNull<CanvasItem>(icon) is CanvasItem ic)
			{
				Color old = ic.SelfModulate;
				ic.SelfModulate = new Color(1f, 1f, 1f, 0f);
				st.Undo.Add(() => { if (GodotObject.IsInstanceValid(ic)) ic.SelfModulate = old; });
			}
		}

		st.Words = new Control { Name = "P3SystemWords", MouseFilter = Control.MouseFilterEnum.Ignore };
		st.Words.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		st.Screen.AddChild(st.Words);
		st.Undo.Add(() => { if (GodotObject.IsInstanceValid(st.Words)) st.Words.QueueFree(); });
		int fs = (int)(70f * k);
		float[] zig = { 0f, 30f, 12f, -10f, 26f };
		for (int i = 0; i < st.Tabs.Count; i++)
		{
			Tab t = st.Tabs[i];
			int idx = i;
			t.Node = new Node2D { Rotation = -0.2f };
			st.Words.AddChild(t.Node);
			t.Sel = new Node2D { Modulate = new Color(1f, 1f, 1f, 0f) };
			t.Node.AddChild(t.Sel);
			float w = Typo.Barlow.GetStringSize(t.En, HorizontalAlignment.Left, -1, fs).X;
			float h = fs * 0.42f, y0 = fs * 0.36f, lean = fs * 0.18f;
			// 选中：红色斜切条横穿字的中下部 + 右端白色小旗（红边）
			Fx.Poly(t.Sel, new[] { new Vector2(-16f * k + lean, y0), new Vector2(w + 30f * k + lean, y0), new Vector2(w + 30f * k, y0 + h), new Vector2(-16f * k, y0 + h) }, Red);
			Vector2[] flag = { new Vector2(w + 34f * k, y0 + h), new Vector2(w + 84f * k, y0 - fs * 0.62f), new Vector2(w + 70f * k, y0 + h * 0.2f) };
			Fx.Poly(t.Sel, flag, White);
			Fx.StrokeClosed(t.Sel, flag, Red, 3f * k, additive: false);
			t.Shadow = Typo.Text(t.Node, t.En, Typo.Barlow, fs, new Color(0.02f, 0.10f, 0.40f, 0.45f), new Vector2(5f, 6f) * k);
			t.Text = Typo.Text(t.Node, t.En, Typo.Barlow, fs, Pale, Vector2.Zero);
			t.Hit = new Control { Size = new Vector2(w + 40f * k, fs * 1.0f), Position = new Vector2(-10f * k, fs * 0.05f), MouseFilter = Control.MouseFilterEnum.Stop, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
			t.Node.AddChild(t.Hit);
			t.Hit.MouseEntered += () =>
			{
				t.Mouse = true;
				Audio.Sfx.Clip("Sys_Cursor", 0.35f);
			};
			t.Hit.MouseExited += () => t.Mouse = false;
			t.Hit.GuiInput += ev => ModEntry.Safe("settings tab word", () =>
			{
				if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
				{
					t.Hit.AcceptEvent();
					Choose(st, idx);
				}
			});
			t.Node.SetMeta("x", (880f + zig[i % zig.Length]) * k);       // 人物轮廓右边的水里（同参考图，菜单字不压在白色上）
			t.Node.SetMeta("y", (330f + i * fs * 0.86f / k) * k);
		}
	}

	private static void SetMouseRecursive(Control c, bool restore)
	{
		foreach (Node n in c.GetChildren())
		{
			if (n is Control cc)
			{
				if (!restore)
				{
					cc.SetMeta("p3_mf", (int)cc.MouseFilter);
					cc.MouseFilter = Control.MouseFilterEnum.Ignore;
				}
				else if (cc.HasMeta("p3_mf"))
				{
					cc.MouseFilter = (Control.MouseFilterEnum)cc.GetMeta("p3_mf").AsInt32();
					cc.RemoveMeta("p3_mf");
				}
				SetMouseRecursive(cc, restore);
			}
		}
	}

	/// <summary>点分页大字：深蓝圆从这个字涨出盖住内容区，盖满时换页，再退开（同 P3R 53.33s 的进子菜单）。</summary>
	private static void Choose(State st, int idx)
	{
		if (idx == st.Current || st.WipeT0 >= 0f)
		{
			return;
		}
		st.Pending = idx;
		st.WipeT0 = Now;
		Tab t = st.Tabs[idx];
		st.WipeAt = t.Text.GetGlobalRect().GetCenter();
		Audio.Sfx.Clip("Btl_Ok", 0.5f);
	}

	private static void ApplyTab(State st, int idx)
	{
		Tab t = st.Tabs[idx];
		bool p3 = t.Vanilla == null;
		NSettingsTab? target = p3 ? st.Tabs[0].Vanilla : t.Vanilla;
		if (CurrentVanilla(st) != target)
		{
			target?.ForceTabPressed();
		}
		SetP3Page(st, p3);
		st.Current = idx;
	}

	/// <summary>
	/// P3MAKOTO 页：常规页里只显示本 mod 那一栏（其余行暂时藏起来，离开时恢复）。
	/// v0.14.68（实机）：语言下拉框不在"语言"那一行里（单独摆放），以前没藏到，压在本 mod 那一栏的标题上 —— 常规页里所有不属于本 mod 栏的下拉框也一起藏。
	/// 还原按每个节点上记的原可见性来做，不依赖 P3Page 标志（以前关掉换皮时标志被清掉，筛选就再也恢复不了）。
	/// </summary>
	private static void SetP3Page(State st, bool on, bool force = false)
	{
		if ((on == st.P3Page && !force) || st.Tabs.Count == 0 || st.Tabs[0].Panel is not NSettingsPanel panel || panel.Content is not Control content)
		{
			return;
		}
		st.P3Page = on;
		Node? section = content.GetNodeOrNull("P3MakotoSettings");
		List<CanvasItem> targets = new();
		foreach (Node n in content.GetChildren())
		{
			if (n is CanvasItem ci && n != section)
			{
				targets.Add(ci);
			}
		}
		foreach (Node n in panel.FindChildren("*", "", true, false))
		{
			if (n is NDropdown dd && (section == null || !section.IsAncestorOf(dd)) && !targets.Contains(dd))
			{
				targets.Add(dd);
			}
		}
		if (st.Screen.GetNodeOrNull<CanvasItem>("%LanguageDropdown") is CanvasItem lang && !targets.Contains(lang))
		{
			targets.Add(lang);                                    // 语言下拉框可能不在常规页面板里
		}
		foreach (CanvasItem ci in targets)
		{
			if (on)
			{
				if (!ci.HasMeta("p3_vis"))
				{
					ci.SetMeta("p3_vis", ci.Visible);
				}
				ci.Visible = false;
			}
			else if (ci.HasMeta("p3_vis"))
			{
				ci.Visible = ci.GetMeta("p3_vis").AsBool();
				ci.RemoveMeta("p3_vis");
			}
		}
		AccessTools.Method(typeof(NSettingsPanel), "RefreshSize")?.Invoke(panel, null);
		st.Scroll.InstantlyScrollToTop();
	}

	private static NSettingsTab? CurrentVanilla(State st)
	{
		foreach (Tab t in st.Tabs)
		{
			if (t.Vanilla != null && t.Panel != null && t.Panel.Visible)
			{
				return t.Vanilla;
			}
		}
		return null;
	}

	// ── 每一行设置 ──

	private static void StyleRows(State st)
	{
		foreach (Tab t in st.Tabs)
		{
			if (t.Vanilla == null || t.Panel?.Content is not Control content)
			{
				continue;
			}
			StyleContent(st, content);
		}
	}

	private static void StyleContent(State st, Control content)
	{
		foreach (Node n in content.GetChildren())
		{
			if (n is not Control row || st.Styled.Contains(row.GetInstanceId()))
			{
				continue;
			}
			st.Styled.Add(row.GetInstanceId());
			string name = row.Name.ToString();
			if (name.Contains("Divider", StringComparison.OrdinalIgnoreCase))
			{
				Color old = row.Modulate;
				row.Modulate = new Color(1f, 1f, 1f, 0f);
				st.Undo.Add(() => { if (GodotObject.IsInstanceValid(row)) row.Modulate = old; });
				continue;
			}
			if (name == "P3MakotoSettings" && row is Container section)
			{
				StyleContent(st, section);                        // 本 mod 那一栏：里面每行单独处理
				continue;
			}
			RowDeco d = new() { Row = row };
			d.Deco = new Node2D { Name = "P3RowDeco", ShowBehindParent = true };
			row.AddChild(d.Deco);
			row.MoveChild(d.Deco, 0);
			d.Normal = new Node2D();
			d.Selected = new Node2D { Modulate = new Color(1f, 1f, 1f, 0f) };
			d.Deco.AddChild(d.Normal);
			d.Deco.AddChild(d.Selected);
			st.Undo.Add(() => { if (GodotObject.IsInstanceValid(d.Deco)) d.Deco.QueueFree(); });
			StyleTree(st, row, d);
			st.Rows.Add(d);
		}
	}

	/// <summary>一行里面：文字换字体 / 白字；勾选框换 P3R 斜方框；其余贴图按亮度映射成 P3R 蓝。</summary>
	private static void StyleTree(State st, Node n, RowDeco d)
	{
		foreach (Node c in n.GetChildren())
		{
			if (c.Name.ToString().StartsWith("P3", StringComparison.Ordinal))
			{
				continue;
			}
			switch (c)
			{
				case NTickbox tb:
					Tickbox(st, tb);
					continue;
				case RichTextLabel rt:
				{
					rt.AddThemeFontOverride("normal_font", LabelFont);
					rt.AddThemeFontOverride("bold_font", LabelFont);
					rt.AddThemeColorOverride("default_color", Colors.White);
					rt.AddThemeColorOverride("font_outline_color", new Color(0.02f, 0.06f, 0.22f));
					rt.AddThemeConstantOverride("outline_size", 3);
					d.Labels.Add(rt);
					st.Undo.Add(() =>
					{
						if (!GodotObject.IsInstanceValid(rt)) return;
						rt.RemoveThemeFontOverride("normal_font");
						rt.RemoveThemeFontOverride("bold_font");
						rt.RemoveThemeColorOverride("default_color");
						rt.RemoveThemeColorOverride("font_outline_color");
						rt.RemoveThemeConstantOverride("outline_size");
					});
					break;
				}
				case Label lb:
				{
					Action undo = Typo.Restyle(lb, LabelFont, Colors.White, new Color(0.02f, 0.06f, 0.22f), 3);
					lb.AddThemeFontOverride("font", LabelFont);
					lb.AddThemeColorOverride("font_color", Colors.White);
					d.Labels.Add(lb);
					st.Undo.Add(() =>
					{
						if (!GodotObject.IsInstanceValid(lb)) return;
						undo();
						lb.RemoveThemeFontOverride("font");
						lb.RemoveThemeColorOverride("font_color");
					});
					break;
				}
				case TextureRect or NinePatchRect when ((CanvasItem)c).Material == null:
				{
					CanvasItem ci = (CanvasItem)c;
					ci.Material = new ShaderMaterial { Shader = Shaders.GradientMap };
					st.Undo.Add(() => { if (GodotObject.IsInstanceValid(ci)) ci.Material = null; });
					break;
				}
			}
			StyleTree(st, c, d);
		}
	}

	/// <summary>勾选框：原版的勾 / 空框贴图设透明，画 P3R 斜方框（勾上：青色填满 + 白勾）。</summary>
	private static void Tickbox(State st, NTickbox tb)
	{
		Control? ticked = tb.GetNodeOrNull<Control>("%TickboxVisuals/Ticked");
		Control? unticked = tb.GetNodeOrNull<Control>("%TickboxVisuals/NotTicked");
		Control? visuals = tb.GetNodeOrNull<Control>("%TickboxVisuals");
		if (ticked == null || unticked == null || visuals == null)
		{
			return;
		}
		foreach (Control c in new[] { ticked, unticked })
		{
			Color old = c.SelfModulate;
			c.SelfModulate = new Color(1f, 1f, 1f, 0f);
			foreach (Node ch in c.GetChildren())
			{
				if (ch is CanvasItem cc)
				{
					Color o2 = cc.Modulate;
					cc.Modulate = new Color(1f, 1f, 1f, 0f);
					st.Undo.Add(() => { if (GodotObject.IsInstanceValid(cc)) cc.Modulate = o2; });
				}
			}
			st.Undo.Add(() => { if (GodotObject.IsInstanceValid(c)) c.SelfModulate = old; });
		}
		Node2D box = new() { Name = "P3Tick" };
		visuals.AddChild(box);
		st.Undo.Add(() => { if (GodotObject.IsInstanceValid(box)) box.QueueFree(); });
		box.SetMeta("tb", tb);
	}

	// ── 每帧 ──

	private static void StartTick(State st)
	{
		if (st.Tick != null || Engine.GetMainLoop() is not SceneTree tree)
		{
			return;
		}
		ulong last = Time.GetTicksUsec();
		st.Tick = () =>
		{
			ulong now = Time.GetTicksUsec();
			float dt = Mathf.Min((now - last) / 1e6f, 0.1f);
			last = now;
			if (!GodotObject.IsInstanceValid(st.Screen) || !st.Screen.IsVisibleInTree() || !st.On)
			{
				StopTick(st);
				return;
			}
			try
			{
				Frame(st, dt);
			}
			catch (Exception e)
			{
				ModEntry.Report("settings skin", e);
				Restore(st);                                    // 出错就整套退回原版（不每帧重试）
			}
		};
		tree.ProcessFrame += st.Tick;
	}

	private static void StopTick(State st)
	{
		if (st.Tick != null && Engine.GetMainLoop() is SceneTree tree)
		{
			tree.ProcessFrame -= st.Tick;
		}
		st.Tick = null;
	}

	private static void Frame(State st, float dt)
	{
		float now = Now, t = now - st.T0;
		Vector2 view = st.Screen.GetViewportRect().Size;
		float k = view.Y / 1080f;

		// 当前页（原版 LB/RB 也能换页）：大字跟着走
		NSettingsTab? cur = CurrentVanilla(st);
		if (st.Tabs.Count > 0 && st.P3Page && cur != st.Tabs[0].Vanilla)
		{
			SetP3Page(st, false);
		}
		if (st.WipeT0 < 0f)
		{
			int idx = st.P3Page ? st.Tabs.Count - 1 : st.Tabs.FindIndex(x => x.Vanilla == cur);
			st.Current = idx;
		}

		// 入场
		float splash = Mathf.Clamp(t / 0.55f, 0f, 1f);
		st.Splash.Visible = splash < 1f;
		if (st.Splash.Visible)
		{
			foreach (Node n in st.Splash.GetChildren())
			{
				Polygon2D b = (Polygon2D)n;
				float dly = (float)b.GetMeta("d"), r = (float)b.GetMeta("r");
				float u = Mathf.Clamp((t - dly) / 0.42f, 0f, 1f);
				b.Position = new Vector2(Mathf.Lerp(view.X + r, -r - view.X * 0.1f, Ease.InOutCubic(u)), (float)b.GetMeta("y") + Mathf.Sin(u * 3f + r) * 30f * k);
				b.Scale = Vector2.One * (0.6f + 0.6f * Mathf.Sin(Mathf.Pi * u));
			}
		}
		float ePanel = Ease.OutCubic(Mathf.Clamp((t - 0.12f) / 0.4f, 0f, 1f));
		st.WhitePanel.Position = new Vector2(-260f * k * (1f - ePanel), 0f);
		st.SystemWord.Modulate = new Color(1f, 1f, 1f, ePanel);
		st.SystemWord.Position = new Vector2(-34f * k, view.Y + 40f * k + 140f * k * (1f - ePanel));
		float ePor = Ease.OutCubic(Mathf.Clamp((t - 0.2f) / 0.6f, 0f, 1f));
		st.Portrait.Position = new Vector2(0f, -120f * k * (1f - ePor) + Mathf.Sin(now * 0.6f) * 6f * k);
		st.Portrait.Modulate = new Color(1f, 1f, 1f, ePor);
		float eContent = Ease.OutCubic(Mathf.Clamp((t - 0.35f) / 0.35f, 0f, 1f));
		st.Manager.Modulate = new Color(st.OrigModulate, st.OrigModulate.A * eContent);

		// 眨眼：每 3~6 秒一次（半睁 0.05s → 闭 0.09s → 半睁 0.05s）
		if (st.Open != null && st.Half != null && st.Closed != null)
		{
			if (st.BlinkT0 < 0f && now >= st.NextBlink)
			{
				st.BlinkT0 = now;
			}
			if (st.BlinkT0 >= 0f)
			{
				float b = now - st.BlinkT0;
				st.Portrait.Texture = b < 0.05f ? st.Half : b < 0.14f ? st.Closed : b < 0.19f ? st.Half : st.Open;
				if (b >= 0.19f)
				{
					st.BlinkT0 = -1f;
					st.NextBlink = now + Fx.Rand(3f, 6f);
				}
			}
		}

		// 气泡上浮
		foreach (Node n in st.Bubbles.GetChildren())
		{
			Sprite2D b = (Sprite2D)n;
			float v = (float)b.GetMeta("v"), ph = (float)b.GetMeta("p"), s = (float)b.GetMeta("s");
			Vector2 p = b.Position + new Vector2(Mathf.Sin(now * 1.2f + ph) * 10f * dt, -v * dt);
			if (p.Y < -20f)
			{
				p = new Vector2(Fx.Rand(0.42f, 1f) * view.X, view.Y + 20f);
			}
			b.Position = p;
			b.Scale = Vector2.One * s;
			b.Modulate = new Color(0.7f, 0.95f, 1f, 0.45f * Mathf.Clamp(p.Y / view.Y * 2f, 0f, 1f));
		}

		LayoutContent(st, view, k);
		Words(st, view, k, t, dt, now);
		Wipe(st, view, k, now);
		Rows(st, k, dt);
		Ticks(st);

		// 右下提示：当前分页
		if (st.Current >= 0 && st.Current < st.Tabs.Count)
		{
			Tab c = st.Tabs[st.Current];
			st.HintZh.Text = Zh ? c.Zh + "设置" : c.En;
			st.HintEn.Text = (Zh ? c.En : "Settings") + "  ·  System";
		}
	}

	/// <summary>原版滚动区缩放到右侧目标框里（每帧对齐，原版容器重排后也会拉回来）；背后的面板按滚动区大小画。</summary>
	private static void LayoutContent(State st, Vector2 view, float k)
	{
		Control m = st.Manager;
		Transform2D inv = m.GetGlobalTransform().AffineInverse();
		// v0.14.68（实机：内容没移到右边）：滚动区自身可能是 0 尺寸（内容溢出摆放）→ 依次用 滚动区 / 当前分页面板（宽）+ 管理器（高）/ 管理器 的范围。
		// 只在第一次（缩放前）量一次原始范围，之后一直用它（避免缩放后量到的结果反过来影响自己）。
		if (!st.SrcValid)
		{
			Rect2 Local(Rect2 g)
			{
				Vector2 a = inv * g.Position, b = inv * g.End;
				return new Rect2(a, b - a);
			}
			Rect2 scroll = Local(st.Scroll.GetGlobalRect());
			Rect2 mgr = new(Vector2.Zero, m.Size);
			Rect2 src = scroll;
			string how = "scroll";
			if (src.Size.X < 100f || src.Size.Y < 100f)
			{
				NSettingsPanel? panel = st.Tabs.Count > 0 ? st.Tabs.FirstOrDefault(x => x.Panel != null && x.Panel.Visible)?.Panel : null;
				Rect2 pr = panel != null ? Local(panel.GetGlobalRect()) : default;
				if (pr.Size.X >= 100f && mgr.Size.Y >= 100f)
				{
					src = new Rect2(new Vector2(pr.Position.X, Mathf.Max(pr.Position.Y, mgr.Position.Y)), new Vector2(pr.Size.X, mgr.End.Y - Mathf.Max(pr.Position.Y, mgr.Position.Y)));
					how = "panel+manager";
				}
				else if (mgr.Size.X >= 100f && mgr.Size.Y >= 100f)
				{
					src = mgr;
					how = "manager";
				}
				else
				{
					return;                                         // 还没排版好，下一帧再量
				}
			}
			st.Src = src;
			st.SrcValid = true;
			GD.Print($"{ModEntry.Tag} settings layout: source={how} {src} (scroll {scroll}, manager {mgr}), view {view}, manager parent {m.GetParent()?.GetClass()}");
			DumpTree(st.Screen);
		}
		Rect2 local = st.Src;
		Rect2 target = new(new Vector2(view.X * 0.60f, view.Y * 0.17f), new Vector2(view.X * 0.375f, view.Y * 0.72f));
		float s = Mathf.Min(1f, Mathf.Min(target.Size.X / local.Size.X, target.Size.Y / local.Size.Y));
		Vector2 want = target.Position + new Vector2((target.Size.X - local.Size.X * s) / 2f, 0f) - local.Position * s;
		if (!m.Scale.IsEqualApprox(Vector2.One * s))
		{
			m.PivotOffset = Vector2.Zero;
			m.Scale = Vector2.One * s;
		}
		if (m.GlobalPosition.DistanceTo(want) > 0.5f)
		{
			m.GlobalPosition = want;
		}
		// 面板（管理器的局部坐标，跟着缩放）
		string key = $"{local.Position.X:0}|{local.Position.Y:0}|{local.Size.X:0}|{local.Size.Y:0}";
		if ((string)st.PanelDeco.GetMeta("key", "") != key)
		{
			st.PanelDeco.SetMeta("key", key);
			foreach (Node n in st.PanelDeco.GetChildren())
			{
				if (n is Polygon2D || n is Line2D)
				{
					n.QueueFree();
				}
			}
			float x0 = local.Position.X - 40f, x1 = local.End.X + 30f, y0 = local.Position.Y - 30f, y1 = local.End.Y + 26f, lean = 46f;
			Vector2[] Box(float a, float b, float c, float d) => new[] { new Vector2(a + lean, c), new Vector2(b + lean, c), new Vector2(b, d), new Vector2(a, d) };
			Polygon2D shadow = Fx.Poly(st.PanelDeco, Box(x0 + 18f, x1 + 18f, y0 + 18f, y1 + 18f), new Color(0f, 0.02f, 0.1f, 0.4f));
			Polygon2D body = Fx.Poly(st.PanelDeco, Box(x0, x1, y0, y1), Navy);
			Polygon2D topLine = Fx.Poly(st.PanelDeco, Box(x0 + 30f, x1 + 10f, y0, y0 + 5f), Cyan);
			Polygon2D bottom = Fx.Poly(st.PanelDeco, Box(x0 + (x1 - x0) * 0.4f, x1, y1 - 6f, y1), Red);
			foreach (Polygon2D p in new[] { shadow, body, topLine, bottom })
			{
				st.PanelDeco.MoveChild(p, 0);
			}
			st.PanelDeco.MoveChild(shadow, 0);
			st.PanelTitle.Position = new Vector2(x0 + lean + 10f, y0 - 82f);
			st.PanelWater.Position = new Vector2(x1 - 820f, y1 - 250f);
		}
		if (st.Current >= 0 && st.Current < st.Tabs.Count)
		{
			string title = st.Tabs[st.Current].En;
			if (st.PanelTitle.Text != title)
			{
				st.PanelTitle.Text = title;
				st.PanelWater.Text = title;
			}
		}
	}

	private static void Words(State st, Vector2 view, float k, float t, float dt, float now)
	{
		for (int i = 0; i < st.Tabs.Count; i++)
		{
			Tab w = st.Tabs[i];
			bool sel = i == (st.WipeT0 >= 0f && st.Pending >= 0 ? st.Pending : st.Current);
			w.SelK = Mathf.MoveToward(w.SelK, sel ? 1f : 0f, dt / 0.14f);
			w.Hover = Mathf.MoveToward(w.Hover, w.Mouse && !sel ? 1f : 0f, dt / 0.1f);
			float e = Ease.OutCubic(Mathf.Clamp((t - 0.22f - i * 0.05f) / 0.35f, 0f, 1f));
			float sk = Ease.OutBack(w.SelK) , hv = Ease.OutCubic(w.Hover);
			float x = (float)w.Node.GetMeta("x"), y = (float)w.Node.GetMeta("y");
			w.Node.Position = new Vector2(x + 180f * k * (1f - e) + 12f * k * hv + 10f * k * w.SelK, y);
			w.Node.Modulate = new Color(1f, 1f, 1f, e);
			w.Node.Scale = Vector2.One * (1f + 0.1f * w.SelK);
			w.Sel.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(sk, 0f, 1f));
			w.Sel.Scale = new Vector2(Mathf.Clamp(sk, 0.01f, 1.2f), 1f);
			w.Text.LabelSettings.FontColor = Pale.Lerp(Colors.White, hv).Lerp(Ink, w.SelK);
			w.Shadow.LabelSettings.FontColor = new Color(0.02f, 0.10f, 0.40f, 0.45f).Lerp(new Color(1f, 1f, 1f, 0.95f), w.SelK);
		}
	}

	private static void Wipe(State st, Vector2 view, float k, float now)
	{
		if (st.WipeT0 < 0f)
		{
			st.Wipe.Visible = false;
			return;
		}
		float u = now - st.WipeT0;
		float full = new Vector2(view.X, view.Y).Length();
		float r;
		if (u < 0.24f)
		{
			r = full * 0.75f * Ease.InCubic(u / 0.24f);
		}
		else
		{
			if (st.Pending >= 0)
			{
				ApplyTab(st, st.Pending);                        // 盖满的那一刻换页
				st.Pending = -1;
				Audio.Sfx.Clip("ShuffleTime_Whoosh", 0.3f);
			}
			float v = Mathf.Clamp((u - 0.24f) / 0.3f, 0f, 1f);
			r = full * 0.75f * (1f + 0.35f * Ease.OutCubic(v));
			st.Wipe.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(v));
			if (v >= 1f)
			{
				st.WipeT0 = -1f;
				st.Wipe.Visible = false;
				return;
			}
		}
		if (u < 0.24f)
		{
			st.Wipe.Modulate = Colors.White;
		}
		st.Wipe.Visible = true;
		st.Wipe.Position = st.WipeAt;
		st.Wipe.Scale = Vector2.One * Mathf.Max(r, 1f);
	}

	private static void Rows(State st, float k, float dt)
	{
		// 新出现的行（比如原版按键页重建）补上样式
		foreach (Tab t in st.Tabs)
		{
			if (t.Vanilla != null && t.Panel != null && t.Panel.Visible && t.Panel.Content is Control content)
			{
				StyleContent(st, content);
				if (content.GetNodeOrNull<Container>("P3MakotoSettings") is Container sec)
				{
					StyleContent(st, sec);
				}
			}
		}
		Control? focus = st.Screen.GetViewport().GuiGetFocusOwner();
		Vector2 mouse = st.Screen.GetViewport().GetMousePosition();
		Rect2 clip = st.Scroll.GetGlobalRect();
		foreach (RowDeco d in st.Rows)
		{
			if (!GodotObject.IsInstanceValid(d.Row) || !d.Row.IsVisibleInTree())
			{
				continue;
			}
			Vector2 size = d.Row.Size;
			if (!d.Built.IsEqualApprox(size) && size.X > 4f && size.Y > 4f)
			{
				BuildRow(d, size);
			}
			bool hot = (clip.HasPoint(mouse) && d.Row.GetGlobalRect().HasPoint(mouse)) || (focus != null && (focus == d.Row || d.Row.IsAncestorOf(focus)));
			d.K = Mathf.MoveToward(d.K, hot ? 1f : 0f, dt / 0.1f);
			float e = Ease.OutCubic(d.K);
			d.Selected.Modulate = new Color(1f, 1f, 1f, e);
			d.Deco.Position = new Vector2(8f * e, 0f);
			Color tint = Colors.White.Lerp(new Color(0.06f, 0.1f, 0.35f), e);
			foreach (CanvasItem l in d.Labels)
			{
				if (GodotObject.IsInstanceValid(l))
				{
					l.SelfModulate = tint;
				}
			}
		}
	}

	private static void BuildRow(RowDeco d, Vector2 size)
	{
		d.Built = size;
		foreach (Node n in d.Normal.GetChildren()) n.QueueFree();
		foreach (Node n in d.Selected.GetChildren()) n.QueueFree();
		float x0 = -14f, x1 = size.X + 10f, y0 = 3f, y1 = size.Y - 3f, lean = Mathf.Min(16f, (y1 - y0) * 0.3f);
		Vector2[] Box(float a, float b, float c, float e) => new[] { new Vector2(a + lean, c), new Vector2(b + lean, c), new Vector2(b, e), new Vector2(a, e) };
		Fx.Poly(d.Normal, Box(x0, x1, y0, y1), new Color(0.02f, 0.06f, 0.24f, 0.55f));
		Fx.Poly(d.Normal, new[] { new Vector2(x0 + lean, y0), new Vector2(x0 + lean + 7f, y0), new Vector2(x0 + 7f, y1), new Vector2(x0, y1) }, new Color(Cyan, 0.85f));
		Fx.StrokeClosed(d.Selected, Box(x0 + 9f, x1 + 9f, y0 - 6f, y1 - 6f), Red, 2.4f, additive: false);
		Fx.Poly(d.Selected, Box(x0, x1, y0, y1), White);
		Fx.Poly(d.Selected, new[] { new Vector2(x0 + lean, y0), new Vector2(x0 + lean + 7f, y0), new Vector2(x0 + 7f, y1), new Vector2(x0, y1) }, Cyan);
	}

	private static void Ticks(State st)
	{
		foreach (RowDeco d in st.Rows)
		{
			if (!GodotObject.IsInstanceValid(d.Row) || !d.Row.IsVisibleInTree())
			{
				continue;
			}
			foreach (Node n in d.Row.FindChildren("P3Tick", "Node2D", true, false))
			{
				Node2D box = (Node2D)n;
				if (!box.HasMeta("tb") || box.GetMeta("tb").AsGodotObject() is not NTickbox tb || box.GetParent() is not Control vis)
				{
					continue;
				}
				bool on = tb.IsTicked;
				float s = Mathf.Min(vis.Size.X, vis.Size.Y);
				if (s < 4f) s = 40f;
				string key = $"{on}|{s:0}";
				if ((string)box.GetMeta("key", "") == key)
				{
					continue;
				}
				box.SetMeta("key", key);
				foreach (Node c in box.GetChildren()) c.QueueFree();
				Vector2 c0 = vis.Size / 2f;
				float h = s * 0.36f, lean = h * 0.35f;
				Vector2[] sq = { c0 + new Vector2(-h + lean, -h), c0 + new Vector2(h + lean, -h), c0 + new Vector2(h - lean, h), c0 + new Vector2(-h - lean, h) };
				Fx.Poly(box, sq.Select(p => p + new Vector2(4f, 4f)).ToArray(), new Color(0f, 0.02f, 0.1f, 0.5f));
				Fx.Poly(box, sq, on ? Cyan : new Color(0.02f, 0.08f, 0.3f, 0.95f));
				Fx.StrokeClosed(box, sq, White, 2.5f, additive: false);
				if (on)
				{
					Fx.Stroke(box, new[] { c0 + new Vector2(-h * 0.55f, 0f), c0 + new Vector2(-h * 0.1f, h * 0.5f), c0 + new Vector2(h * 0.7f, -h * 0.6f) }, Colors.White, 4f, additive: false);
				}
			}
		}
	}

	private static bool _dumped;

	/// <summary>v0.14.68 诊断：第一次换皮时把设置界面的节点结构写进日志（类型、名字、可见、矩形、调制、材质、裁切），实机有问题时对照用。每次游戏启动只写一次。</summary>
	private static void DumpTree(Node root)
	{
		if (_dumped)
		{
			return;
		}
		_dumped = true;
		int lines = 0;
		void Walk(Node n, int depth)
		{
			if (lines > 260 || depth > 7 || n.Name.ToString().StartsWith("P3", StringComparison.Ordinal))
			{
				return;
			}
			lines++;
			string info = n.GetClass() + (n.GetScript().Obj is Script sc ? "(" + System.IO.Path.GetFileNameWithoutExtension(sc.ResourcePath) + ")" : "");
			if (n is Control c)
			{
				info += $" vis={c.Visible} rect={c.GetGlobalRect()} mod={c.Modulate.ToHtml()} self={c.SelfModulate.ToHtml()} clip={c.ClipChildren} mat={(c.Material is ShaderMaterial ? "shader" : c.Material != null ? "other" : "-")} mouse={c.MouseFilter}";
			}
			else if (n is CanvasItem ci)
			{
				info += $" vis={ci.Visible} mod={ci.Modulate.ToHtml()} mat={(ci.Material != null ? "yes" : "-")}";
			}
			GD.Print($"{ModEntry.Tag} settings tree {new string(' ', depth * 2)}{n.Name} : {info}");
			if (n is NSettingsPanel && n.Name.ToString() != "GeneralSettings")
			{
				return;                                                // 只展开常规页（语言下拉框等的位置），其他页的每一行不展开（太长）
			}
			foreach (Node ch in n.GetChildren())
			{
				Walk(ch, depth + 1);
			}
		}
		Walk(root, 0);
	}

	private static Vector2[] Circle(float r, int n)
	{
		Vector2[] p = new Vector2[n];
		for (int i = 0; i < n; i++)
		{
			float a = Mathf.Tau * i / n;
			p[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
		}
		return p;
	}
}
