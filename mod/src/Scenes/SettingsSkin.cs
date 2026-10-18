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
///   当前项黑字 + 红色斜切条 + 右端白色小旗（同 SKILL），其余浅青；点击时从那个字涌出边缘起伏的半透明深蓝水团盖满整屏，
///   换页后中间撑开一个白边的洞露出新的一页（同 P3R 进子菜单）。v0.14.70：整列字放在内容面板左边，不再和面板重叠，行距加大。
///   P3MAKOTO = 常规页里只显示本 mod 的那一栏。
/// · 内容区：原版的滚动区整体缩放、移到右侧，背后一块斜切深蓝面板（青色上沿、红色下沿、页名标题和超大水印字）；
///   每一行设置垫一条斜切深蓝底，悬停 / 手柄选中时变白底 + 错开的红框（P3R 选中行）；文字换思源黑体 Bold（中文）/ Barlow（英文）白字；
///   勾选框换成 P3R 斜方框；其余控件贴图按亮度映射成深蓝 → 钴蓝 → 冰白。
/// · 打开时（v0.14.72 按用户推送的 60fps 分镜逐帧复刻，时间轴见 Splash 的注释和 docs/ART_SPEC.md）：泡沫往下泼 → V 形水团又窄又深地冲下、
///   越来越宽直到盖满（设置界面只在水团里显示）→ 宽水柱、白色水花云、两侧水花、成串气泡 → 人物闭着眼、偏着头从上方沉下来、转正、睁眼；
///   菜单字依次浮现，红色选中条最后展开，内容区从右淡入。
///   待机时人物身边稀疏地冒气泡串。音效（程序合成的入水声 + 气泡声）和画面对齐。
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

	// v0.14.75 人物和水的分界线（1920×1080 像素，y 递增）：衬衫右沿 → 绕开袖口那一截（原版没有）→ 耳机 → 绕开右下伸进水里的头发 → 脸下边。
	// 线左边是人物 + 白色面板，线右边是水（立绘着色器、白色面板、待机气泡共用）
	private static readonly Vector2[] Edge =
	{
		new(1000f, 0f), new(905f, 200f), new(812f, 385f), new(795f, 470f), new(775f, 560f), new(760f, 650f), new(745f, 700f),
		new(748f, 745f), new(752f, 860f), new(740f, 925f), new(700f, 935f), new(650f, 965f), new(630f, 1080f),   // v0.14.77：贴紧耳机右边（以前和头发之间留了一块白）
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
		internal Texture2D? Open, Q1, Half, Q3, Closed;
		internal Texture2D?[] DivePoses = new Texture2D?[3];
		internal ShaderMaterial PortraitMat = null!;
		internal Node2D PanelDeco = null!, Bubbles = null!;
		internal ColorRect SplashRect = null!;
		internal ShaderMaterial SplashMat = null!, MaskMat = null!;
		internal readonly List<(int Frame, Texture2D Tex)> DiveFrames = new();
		internal Texture2D? DiveShown;
		internal ColorRect Wipe = null!;
		internal ShaderMaterial WipeMat = null!;
		internal Label PanelTitle = null!, PanelWater = null!, HintZh = null!, HintEn = null!;
		internal Node2D SystemWord = null!;
		internal ColorRect SystemStrip = null!;
		internal TextureRect ZoneLayer = null!;
		internal readonly List<Tab> Tabs = new();
		internal readonly List<RowDeco> Rows = new();
		internal readonly List<Action> Undo = new();
		internal readonly HashSet<ulong> Styled = new();
		internal bool On, P3Page;
		internal int Current = -1, Pending = -1;
		internal float T0, WipeT0 = -1f, NextBlink, BlinkT0 = -1f;
		internal Vector2 WipeAt;
		internal Vector2 OrigPos, OrigScale, OrigSize, OrigPivot;
		internal Rect2 Src;
		internal Control? Bar;
		internal Vector2 BarOrig;
		internal bool SrcValid;
		internal Color OrigModulate;
		internal Action? Tick;
	}

	private static readonly Dictionary<ulong, State> States = new();

	/// <summary>
	/// v0.14.76（用户：入场动画还没做完，先发一个只修 bug 的版本）：P3R 风格设置界面整体先关掉 —— 不挂、设置里的开关不显示、空闲预热不加载它的资源。
	/// 做完后改回 true。
	/// </summary>
	public static readonly bool Enabled = false;

	private static bool Zh => TranslationServer.GetLocale().StartsWith("zh");
	private static float Now => Time.GetTicksMsec() / 1000f;
	private static Font LabelFont => Zh ? Typo.CjkBold : Typo.Barlow;

	/// <summary>NSettingsScreen._Ready 之后挂上（本 mod 的设置那一栏已经建好）。真正换皮在每次显示时按开关决定。</summary>
	public static void Attach(NSettingsScreen screen)
	{
		ulong id = screen.GetInstanceId();
		if (!Enabled || States.ContainsKey(id) || screen.GetNodeOrNull<NSettingsTabManager>("%SettingsTabManager") is not NSettingsTabManager manager
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
			Audio.Sfx.Clip("sys_dive", 0.55f);
			st.NextBlink = st.T0 + 2.5f;
			st.Root.Material = st.MaskMat;
			st.Root.ClipChildren = CanvasItem.ClipChildrenMode.Only;                   // 入水声：0.00s 的"噗"对齐水花炸开，气泡声对齐上浮的气泡
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
		// v0.14.72：Root 本身是入水遮罩（只在 V 形水团里面显示设置界面），入场结束后关掉
		st.MaskMat = new ShaderMaterial { Shader = Shaders.DiveMask };
		st.Root = new ColorRect { Name = RootName, Color = new Color(1f, 1f, 1f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore, Material = st.MaskMat, ClipChildren = CanvasItem.ClipChildrenMode.Only };
		st.Root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		screen.AddChild(st.Root);
		screen.MoveChild(st.Root, 0);
		st.Undo.Add(() => { if (GodotObject.IsInstanceValid(st.Root)) st.Root.QueueFree(); });

		ShaderMaterial water = new() { Shader = Shaders.CampWater };
		water.SetShaderParameter("aspect", view.X / view.Y);
		ColorRect waterRect = new() { Material = water, MouseFilter = Control.MouseFilterEnum.Ignore };
		waterRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		st.Root.AddChild(waterRect);
		// 待机气泡（P3R 的空心圈 + 高光点，少数实心）：从人物轮廓右边 / 画面下方稀疏地冒出来往上浮（画在内容面板后面）
		st.Bubbles = new Node2D();
		st.Root.AddChild(st.Bubbles);
		for (int i = 0; i < 16; i++)
		{
			Node2D b = Ring(st.Bubbles, Fx.Rand(5f, 15f) * k, i % 4 == 0);
			IdleRespawn(b, view, k, Fx.Rand(0f, 1f) * view.Y);
		}

		// v0.14.77（用户对照原版分镜：一开始左边也是水；白色区域是跟着人物一起沉下来的）：白色面板画在立绘着色器里（随人物移动），不再是屏幕上固定的一层
		float pw = view.Y * 16f / 9f;

		// 竖排大字 SYSTEM（灰色错影 + 黑字，从下往上读，被左边缘裁掉一点）
		// v0.14.77（用户对照原版 #70：背景里那一长串大字没有完全放出来）：顺时针竖排、从上往下读、超大，
		// 字的下半截被画面左边裁掉，只露出字头那一半，整个词也比画面长（下面一截出画）
		st.SystemWord = new Node2D { Position = new Vector2(0.165f * view.X, -0.03f * view.Y), Rotation = Mathf.Pi / 2f };
		st.Root.AddChild(st.SystemWord);
		Typo.Text(st.SystemWord, "SYSTEM", Typo.Barlow, (int)(560f * k), new Color(0.58f, 0.61f, 0.66f), new Vector2(-40f * k, -22f * k));
		Typo.Text(st.SystemWord, "SYSTEM", Typo.Barlow, (int)(560f * k), Ink, Vector2.Zero);

		// 倒挂立绘（融水着色器）
		st.Open = Load("res://p3makoto/ui/system_portrait.png");
		st.Q1 = Load("res://p3makoto/ui/system_portrait_q1.png");
		st.Half = Load("res://p3makoto/ui/system_portrait_half.png");
		st.Q3 = Load("res://p3makoto/ui/system_portrait_q3.png");
		st.Closed = Load("res://p3makoto/ui/system_portrait_closed.png");
		// v0.14.75：入水时"低头 → 抬头"的几张姿势帧（Codex imagegen，docs/codex/codex_settings_dive_frames.md）；还没有就用旋转近似
		for (int i = 0; i < 3; i++)
		{
			st.DivePoses[i] = Load($"res://p3makoto/ui/system_dive_{(char)('a' + i)}.png");
		}
		ShaderMaterial pm = new() { Shader = Shaders.CampPortrait };
		pm.SetShaderParameter("edge", Edge.Select(e => new Vector2(e.X / 1920f, e.Y / 1080f)).ToArray());
		pm.SetShaderParameter("aspect", 16f / 9f);
		pm.SetShaderParameter("panel", 1f);
		pm.SetShaderParameter("zone_tex", Load("res://p3makoto/ui/system_zone.png"));
		pm.SetShaderParameter("screen_aspect", view.X / view.Y);
		st.PortraitMat = pm;
		// v0.14.77（用户对照原版 #70：头发压在竖排大字上面）：层次 = 水 → 白色区域（跟人物走）→ 左边白条 → 竖排大字 → 人物（头发画成水）→ 气泡。
		// 白色区域单独一层（同一个着色器 layer = 1 只画白区），人物那层 layer = 2 不画白区
		ShaderMaterial zm = (ShaderMaterial)pm.Duplicate();
		zm.SetShaderParameter("layer", 1);
		pm.SetShaderParameter("layer", 2);
		st.ZoneLayer = new TextureRect { Texture = st.Open, Material = zm, MouseFilter = Control.MouseFilterEnum.Ignore, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, Size = new Vector2(pw, view.Y) };
		st.Root.AddChild(st.ZoneLayer);
		st.SystemStrip = new ColorRect { Color = Colors.White, MouseFilter = Control.MouseFilterEnum.Ignore, Size = new Vector2(0.15f * view.X, view.Y) };
		st.Root.AddChild(st.SystemStrip);
		st.Root.MoveChild(st.SystemWord, -1);
		st.Portrait = new TextureRect { Texture = st.Open, Material = pm, MouseFilter = Control.MouseFilterEnum.Ignore, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, Size = new Vector2(pw, view.Y) };
		st.Root.AddChild(st.Portrait);
		st.Root.MoveChild(st.Bubbles, -1);                                   // 待机气泡在人物上面
		st.NextBlink = Now + 2.5f;

		// 左上信息框（仿金额框）、右下当前分页提示
		Node2D info = new() { Position = new Vector2(150f, 34f) * k };
		st.Root.AddChild(info);
		Fx.Poly(info, new[] { new Vector2(0f, 0f), new Vector2(250f, 0f) * k, new Vector2(250f, 74f) * k, new Vector2(0f, 74f) * k }, Colors.White);
		Fx.StrokeClosed(info, new[] { new Vector2(0f, 0f), new Vector2(250f, 0f) * k, new Vector2(250f, 74f) * k, new Vector2(0f, 74f) * k }, Ink, 2f * k, additive: false);
		Typo.Text(info, "P3MAKOTO", Typo.Barlow, (int)(34f * k), Ink, new Vector2(12f, 2f) * k);
		Typo.Text(info, "system settings · v" + ModVersion, Typo.Latin, (int)(14f * k), new Color(0.2f, 0.22f, 0.3f), new Vector2(14f, 46f) * k);
		Node2D hint = new() { Position = new Vector2(view.X - 330f * k, view.Y - 72f * k) };   // 内容区下沿之下
		st.Root.AddChild(hint);
		st.HintZh = Typo.Text(hint, "", Typo.CjkBold, (int)(26f * k), Colors.White, new Vector2(0f, 0f), outline: (int)(4f * k), outlineColor: new Color(0.02f, 0.08f, 0.3f));
		Fx.Poly(hint, new[] { new Vector2(0f, 40f) * k, new Vector2(300f, 40f) * k, new Vector2(300f, 42f) * k, new Vector2(0f, 42f) * k }, Colors.White);
		st.HintEn = Typo.Text(hint, "", Typo.Barlow, (int)(17f * k), new Color(0.85f, 0.95f, 1f), new Vector2(0f, 42f) * k);

		// 3. 内容区：原版滚动区整体缩放、移到右侧；背后垫 P3R 面板
		// v0.14.69（实机节点结构 docs/qa/v0.14.68/settings_tree.log）：设置内容不在分页管理器（那只是顶部的分页按钮栏）下面，
		// 而在设置界面直属的全屏滚动区 ScrollContainer（Mask → Clipper → 四个分页面板，x 454~1466；滚动条 x 1604）里。
		// 移动 / 缩放的对象是这个滚动区；面板背景挂在滚动区的第一个子节点（画在内容后面、跟着内容一起缩放）。
		Control sc = st.Scroll;
		st.OrigPos = sc.Position;
		st.OrigScale = sc.Scale;
		st.OrigSize = sc.Size;
		st.OrigPivot = sc.PivotOffset;
		st.OrigModulate = sc.Modulate;
		st.Undo.Add(() =>
		{
			if (GodotObject.IsInstanceValid(sc))
			{
				sc.Scale = st.OrigScale;
				sc.PivotOffset = st.OrigPivot;
				sc.Position = st.OrigPos;
				sc.Size = st.OrigSize;
				sc.Modulate = st.OrigModulate;
			}
		});
		st.PanelDeco = new Node2D { Name = "P3PanelDeco" };
		sc.AddChild(st.PanelDeco);
		sc.MoveChild(st.PanelDeco, 0);
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
		st.WipeMat = new ShaderMaterial { Shader = Shaders.BlobWipe };
		st.Wipe = new ColorRect { Material = st.WipeMat, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
		st.Wipe.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		st.Top.AddChild(st.Wipe);
		BuildSplash(st, view, k);

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
		st.DiveFrames.Clear();
		st.DiveShown = null;
		st.WipeT0 = -1f;
		st.On = false;
		st.SrcValid = false;
		st.Bar = null;
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
		// v0.14.70（用户实机：挤在一起、压到右边面板）：P3R 菜单字大小不一、左右错开；整列放在人物和面板之间（x 0.355W 起），
		// 选中放大 + 右端小旗也不超过面板左沿（面板 0.6W 起）；行距 = 字号 × 1.08（以前 0.86）
		float[] sizes = { 70f, 58f, 64f, 56f, 60f };
		float[] zig = { 0f, 34f, 14f, -8f, 30f };
		float yCursor = 318f * k;
		for (int i = 0; i < st.Tabs.Count; i++)
		{
			Tab t = st.Tabs[i];
			int idx = i;
			int fs = (int)(sizes[i % sizes.Length] * k);
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
			t.Shadow = Typo.Text(t.Node, t.En, Typo.Barlow, fs, new Color(0.02f, 0.10f, 0.40f, 0.7f), new Vector2(5f, 6f) * k);
			t.Text = Typo.Text(t.Node, t.En, Typo.Barlow, fs, Pale, Vector2.Zero);
			t.Hit = new Control { Size = new Vector2(w + 12f * k, fs * 0.82f), Position = new Vector2(-4f * k, fs * 0.12f), MouseFilter = Control.MouseFilterEnum.Stop, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
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
			t.Node.SetMeta("x", view.X * 0.355f + zig[i % zig.Length] * k);
			t.Node.SetMeta("y", yCursor);
			yCursor += fs * 1.08f;
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

	/// <summary>点分页大字：深蓝水团从这个字涌出盖满整屏，盖满时换页，再从中间撑开白边的洞露出新的一页（同 P3R 进子菜单，视频 1.57~1.83s）。</summary>
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
		st.WipeMat.SetShaderParameter("seed", Fx.Rand(0f, 6.28f));
		Audio.Sfx.Clip("Btl_Ok", 0.5f);
		Audio.Sfx.Clip("sys_blob", 0.6f);                         // 水团涌出的闷响，0.16~0.33s 的气泡声对齐洞撑开
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
					Color rtSelf = rt.SelfModulate;
					st.Undo.Add(() =>
					{
						if (!GodotObject.IsInstanceValid(rt)) return;
						rt.SelfModulate = rtSelf;                           // 悬停时给的深蓝色调（v0.14.69 实机：关掉换皮后残留）
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
					Color lbSelf = lb.SelfModulate;
					st.Undo.Add(() =>
					{
						if (!GodotObject.IsInstanceValid(lb)) return;
						lb.SelfModulate = lbSelf;
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

		// 入场（时间轴见类注释；视频第 12 帧 = 0s）
		bool entering = t < 1.2f;
		st.SplashRect.Visible = entering;
		if (entering)
		{
			Splash(st, view, k, t);
		}
		else if (st.Root.ClipChildren != CanvasItem.ClipChildrenMode.Disabled)
		{
			st.Root.ClipChildren = CanvasItem.ClipChildrenMode.Disabled;     // 入场结束：遮罩关掉（ColorRect 本身透明）
			st.Root.Material = null;
		}
		float show = Mathf.Clamp((t - 0.24f) / 0.06f, 0f, 1f);
		st.Words.Modulate = new Color(1f, 1f, 1f, show);
		// v0.14.74（用户：切入僵硬）：按原版逐帧量出来的位置 / 角度（分镜 #40~#72 和最终帧做模板匹配，见 DiveKeys），
		// 两段式：0.30~0.45s 快速沉入、转正大半，之后 0.35s 慢慢下沉到位、角度归零；中间平滑插值（三次 Hermite），不是一条缓动曲线。
		// 待机只往下轻轻浮动（往上浮会在画面底边露出一条白缝 —— v0.14.69 实机）
		(float dy, float rot, float dx) = DivePose(t);
		st.PortraitMat.SetShaderParameter("bottom_fade", Mathf.Clamp(-dy * 10f, 0f, 1f));
		st.Portrait.PivotOffset = new Vector2(650f, 977f) * k;                 // 原版模板匹配得到的旋转中心
		st.Portrait.Rotation = Mathf.DegToRad(rot);
		st.Portrait.Position = new Vector2(dx * view.X, dy * view.Y) + new Vector2(0f, (1f - Mathf.Cos(now * 0.6f)) * 3f * k);
		st.ZoneLayer.PivotOffset = st.Portrait.PivotOffset;
		st.ZoneLayer.Rotation = st.Portrait.Rotation;
		st.ZoneLayer.Position = st.Portrait.Position;
		bool poses = st.DivePoses.All(x => x != null);
		st.PortraitMat.SetShaderParameter("head_angle", poses ? 0f : Mathf.DegToRad(rot) * 0.35f);
		float eWord = Ease.OutCubic(Mathf.Clamp((t - 0.34f) / 0.3f, 0f, 1f));
		st.SystemWord.Position = new Vector2(0.165f * view.X - 320f * k * (1f - eWord), -0.03f * view.Y);
		st.SystemStrip.Position = new Vector2(-st.SystemStrip.Size.X * (1f - eWord), 0f);
		float eContent = Ease.OutCubic(Mathf.Clamp((t - 0.5f) / 0.3f, 0f, 1f));
		st.Scroll.Modulate = new Color(st.OrigModulate, st.OrigModulate.A * eContent);

		// 眼睛（v0.14.72：5 档连续切，按真实时间算，60fps 下闭眼 4 帧、停 2~3 帧、睁眼 5 帧）：入场下沉时闭着，0.52s 起睁开；之后每 3~6 秒眨一次
		if (st.Open != null && st.Q1 != null && st.Half != null && st.Q3 != null && st.Closed != null)
		{
			float lid = t < 0.52f ? 1f : 1f - Mathf.Clamp((t - 0.52f) / 0.08f, 0f, 1f);
			if (lid <= 0f)
			{
				if (st.BlinkT0 < 0f && now >= st.NextBlink)
				{
					st.BlinkT0 = now;
				}
				if (st.BlinkT0 >= 0f)
				{
					float b = now - st.BlinkT0;
					lid = b < 0.06f ? b / 0.06f : b < 0.1f ? 1f : 1f - Mathf.Clamp((b - 0.1f) / 0.08f, 0f, 1f);
					if (b >= 0.18f)
					{
						st.BlinkT0 = -1f;
						st.NextBlink = now + Fx.Rand(3f, 6f);
					}
				}
			}
			Texture2D eye = lid < 0.125f ? st.Open : lid < 0.375f ? st.Q1 : lid < 0.625f ? st.Half : lid < 0.875f ? st.Q3 : st.Closed;
			// 入水姿势帧（原版 #40~#52：低着头闭眼 → 抬头 → 睁眼），按原版的"两帧一换"节奏切
			if (st.DivePoses.All(x => x != null) && t < 0.52f)
			{
				eye = t < 0.40f ? st.DivePoses[0]! : t < 0.46f ? st.DivePoses[1]! : st.DivePoses[2]!;
			}
			if (st.Portrait.Texture != eye)
			{
				st.Portrait.Texture = eye;
				st.ZoneLayer.Texture = eye;
			}
		}

		// 待机气泡上浮（左右晃、轻微变形）；入场水花退掉以后才出现
		float idle = Mathf.Clamp((t - 0.6f) / 0.5f, 0f, 1f);
		foreach (Node n in st.Bubbles.GetChildren())
		{
			Node2D b = (Node2D)n;
			float v = (float)b.GetMeta("v"), ph = (float)b.GetMeta("p");
			Vector2 p = b.Position + new Vector2(Mathf.Sin(now * 2.1f + ph) * 16f * k * dt, -v * dt);
			if (p.Y < -30f * k)
			{
				IdleRespawn(b, view, k, view.Y + 30f * k);
				continue;
			}
			b.Position = p;
			float w = Mathf.Sin(now * 5f + ph) * 0.07f;
			b.Scale = new Vector2(1f + w, 1f - w);
			b.Modulate = new Color(1f, 1f, 1f, 0.6f * idle * Mathf.Clamp(p.Y / (view.Y * 0.25f), 0f, 1f));
		}

		LayoutContent(st, view, k, 80f * k * (1f - eContent));
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

	/// <summary>
	/// 设置内容（全屏滚动区）缩放、平移到右侧目标框：按实测的内容列（各分页面板最左 → 滚动条最右）算缩放；
	/// 同时把滚动区加高，让缩放后可见区域一直到目标框底边（否则缩小后下面一截空着）。每帧对齐（游戏改了它也拉回来）。
	/// 面板背景（滚动区局部坐标，跟着缩放）：斜切深蓝底、青色上沿、红色下沿、页名标题、超大水印字。
	/// </summary>
	private static void LayoutContent(State st, Vector2 view, float k, float slide)
	{
		Control sc = st.Scroll;
		if (!st.SrcValid)
		{
			// 第一次（还没缩放）量内容列：滚动区局部坐标 = 全局 - 滚动区全局位置（此时缩放为 1）
			Vector2 o = sc.GetGlobalRect().Position;
			float left = float.MaxValue, right = float.MinValue, top = float.MaxValue;
			foreach (Tab t in st.Tabs)
			{
				if (t.Panel != null)
				{
					Rect2 r = t.Panel.GetGlobalRect();
					if (r.Size.X > 50f)
					{
						left = Mathf.Min(left, r.Position.X - o.X);
						right = Mathf.Max(right, r.End.X - o.X);
						top = Mathf.Min(top, r.Position.Y - o.Y);
					}
				}
			}
			if (left == float.MaxValue || right - left < 100f)
			{
				return;                                             // 还没排版好，下一帧再量
			}
			// 滚动条（原版在 x 1604，离面板很远）挪到紧贴面板右边，缩放只按面板宽算（字更大）；记录原位置可还原
			if (sc.GetNodeOrNull<Control>("Scrollbar") is Control bar && bar.Size.X > 1f)
			{
				st.Bar = bar;
				st.BarOrig = bar.Position;
				st.Undo.Add(() => { if (GodotObject.IsInstanceValid(bar)) bar.Position = st.BarOrig; });
				right += 14f + bar.Size.X;
			}
			st.Src = new Rect2(left, top, right - left, Mathf.Max(100f, sc.Size.Y - top));
			st.SrcValid = true;
			GD.Print($"{ModEntry.Tag} settings layout v2: content column {st.Src} in scroll {sc.GetGlobalRect()} scale {sc.Scale}, view {view}");
			DumpTree(st.Screen);
		}
		Rect2 src = st.Src;
		// v0.14.70：面板往右收（0.565W → 0.6W），左边整列菜单字不再压到面板上
		Rect2 target = new(new Vector2(view.X * 0.6f, view.Y * 0.165f), new Vector2(view.X * 0.38f, view.Y * 0.745f));
		float s = Mathf.Clamp(target.Size.X / src.Size.X, 0.5f, 0.85f);
		Vector2 pos = target.Position - src.Position * s + new Vector2(slide, 0f);
		Vector2 size = new(st.OrigSize.X, Mathf.Max(st.OrigSize.Y, (target.End.Y - pos.Y) / s));
		if (!sc.PivotOffset.IsEqualApprox(Vector2.Zero)) sc.PivotOffset = Vector2.Zero;
		if (!sc.Scale.IsEqualApprox(Vector2.One * s)) sc.Scale = Vector2.One * s;
		if (!sc.Size.IsEqualApprox(size)) sc.Size = size;
		if (sc.Position.DistanceTo(pos) > 0.5f) sc.Position = pos;
		if (st.Bar != null && GodotObject.IsInstanceValid(st.Bar))
		{
			float bx = src.End.X - st.Bar.Size.X;
			if (Mathf.Abs(st.Bar.Position.X - bx) > 0.5f)
			{
				st.Bar.Position = new Vector2(bx, st.Bar.Position.Y);
			}
		}

		// 面板背景（滚动区局部坐标）
		float x0 = src.Position.X - 50f, x1 = src.End.X + 24f, y0 = src.Position.Y - 34f, y1 = (target.End.Y - pos.Y) / s + 10f, lean = 60f;
		string key = $"{x0:0}|{x1:0}|{y0:0}|{y1:0}";
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
			Vector2[] Box(float a, float b, float c, float d) => new[] { new Vector2(a + lean, c), new Vector2(b + lean, c), new Vector2(b, d), new Vector2(a, d) };
			Polygon2D bottom = Fx.Poly(st.PanelDeco, Box(x0 + (x1 - x0) * 0.4f, x1, y1 - 8f, y1), Red);
			Polygon2D topLine = Fx.Poly(st.PanelDeco, Box(x0 + 40f, x1 + 14f, y0, y0 + 7f), Cyan);
			Polygon2D body = Fx.Poly(st.PanelDeco, Box(x0, x1, y0, y1), Navy);
			Polygon2D shadow = Fx.Poly(st.PanelDeco, Box(x0 + 24f, x1 + 24f, y0 + 24f, y1 + 24f), new Color(0f, 0.02f, 0.1f, 0.4f));
			foreach (Polygon2D p in new[] { bottom, topLine, body, shadow })
			{
				st.PanelDeco.MoveChild(p, 0);                          // 结果顺序：投影 → 底 → 青上沿 → 红下沿 → 文字
			}
			st.PanelTitle.Position = new Vector2(x0 + lean + 16f, y0 - 104f);
			st.PanelTitle.LabelSettings.FontSize = 86;
			st.PanelWater.Position = new Vector2(x1 - 900f, y1 - 300f);
			st.PanelWater.LabelSettings.FontSize = 300;
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
			// 0.46s 起依次浮现（先是半透明的浅色残影，再变实）；红色选中条 0.72s 才从左往右展开（同视频 1.10s 那几帧）
			float e = Ease.OutCubic(Mathf.Clamp((t - 0.46f - i * 0.04f) / 0.3f, 0f, 1f));
			float gate = Ease.OutCubic(Mathf.Clamp((t - 0.72f) / 0.14f, 0f, 1f));
			float sk = Ease.OutBack(w.SelK) * gate, hv = Ease.OutCubic(w.Hover);
			float x = (float)w.Node.GetMeta("x"), y = (float)w.Node.GetMeta("y");
			w.Node.Position = new Vector2(x + 50f * k * (1f - e) + 12f * k * hv + 10f * k * w.SelK * gate, y - 16f * k * (1f - e));
			w.Node.Modulate = new Color(1f, 1f, 1f, e * e);
			w.Node.Scale = Vector2.One * (1f + 0.1f * w.SelK * gate);
			w.Sel.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(sk, 0f, 1f));
			w.Sel.Scale = new Vector2(Mathf.Clamp(sk, 0.01f, 1.2f), 1f);
			w.Text.LabelSettings.FontColor = Pale.Lerp(Colors.White, hv).Lerp(Ink, w.SelK * gate);
			w.Shadow.LabelSettings.FontColor = new Color(0.02f, 0.10f, 0.40f, 0.7f).Lerp(new Color(1f, 1f, 1f, 0.95f), w.SelK * gate);
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
		// 盖满需要的半径：到最远的角，再除以边缘起伏的最小值（0.82）
		float far = 0f;
		foreach (Vector2 c in new[] { Vector2.Zero, new Vector2(view.X, 0f), new Vector2(0f, view.Y), view })
		{
			far = Mathf.Max(far, c.DistanceTo(st.WipeAt));
		}
		float full = far / 0.82f;
		const float Grow = 0.2f, Hold = 0.04f, Open = 0.3f;
		float rOut, rIn = 0f;
		if (u < Grow)
		{
			rOut = full * Ease.InOutCubic(u / Grow);
		}
		else
		{
			if (st.Pending >= 0)
			{
				ApplyTab(st, st.Pending);                        // 盖满的那一刻换页
				st.Pending = -1;
			}
			rOut = full * 1.05f;
			float v = Mathf.Clamp((u - Grow - Hold) / Open, 0f, 1f);
			rIn = full * 1.1f * Ease.InOutCubic(v);
			if (v >= 1f)
			{
				st.WipeT0 = -1f;
				st.Wipe.Visible = false;
				return;
			}
		}
		st.Wipe.Visible = true;
		st.WipeMat.SetShaderParameter("center", st.WipeAt);
		st.WipeMat.SetShaderParameter("size", view);
		st.WipeMat.SetShaderParameter("r_out", rOut);
		st.WipeMat.SetShaderParameter("r_in", rIn);
		st.WipeMat.SetShaderParameter("rim", 7f * k);
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

	// ── 气泡 / 入场水花 ──

	/// <summary>P3R 的气泡：空心圈（细描边）+ 左上一个高光点；filled = 实心浅青团。点按真实半径建（不用缩放，描边才不会糊）。</summary>
	private static Node2D Ring(Node parent, float r, bool filled)
	{
		Node2D b = new();
		if (filled)
		{
			b.AddChild(new Polygon2D { Polygon = Circle(r, 16), Color = new Color(0.62f, 0.9f, 1f, 0.75f), Antialiased = true });
		}
		b.AddChild(new Line2D { Points = Circle(r, 18), Closed = true, Width = Mathf.Max(1.5f, r * 0.16f), DefaultColor = new Color(0.72f, 0.95f, 1f, 0.95f), Antialiased = true });
		b.AddChild(new Polygon2D { Polygon = Circle(Mathf.Max(1.2f, r * 0.22f), 8), Position = new Vector2(-0.4f, -0.4f) * r, Color = new Color(1f, 1f, 1f, 0.95f), Antialiased = true });
		parent.AddChild(b);
		return b;
	}

	/// <summary>待机气泡重生：六成从人物轮廓右边一点（人物吐出来的气泡串），四成在右半屏随机。</summary>
	private static void IdleRespawn(Node2D b, Vector2 view, float k, float y)
	{
		float sx = view.Y * 16f / 9f / 1920f;
		float x;
		if (Fx.Rand(0f, 1f) < 0.6f)
		{
			Vector2 e = Edge[(int)Fx.Rand(3f, 8.99f)];
			x = e.X * sx + Fx.Rand(10f, 70f) * k;
		}
		else
		{
			x = Fx.Rand(0.45f, 0.98f) * view.X;
		}
		b.Position = new Vector2(x, y);
		b.SetMeta("v", Fx.Rand(40f, 95f) * k);
		b.SetMeta("p", Fx.Rand(0f, 6.28f));
	}

	private static void BuildSplash(State st, Vector2 view, float k)
	{
		st.SplashMat = new ShaderMaterial { Shader = Shaders.DiveSplash };
		st.SplashRect = new ColorRect { Material = st.SplashMat, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
		st.SplashRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		st.Top.AddChild(st.SplashRect);
		// v0.14.73：原版水花逐帧图（tools/ui/build_dive_flipbook.py；录像里重复的帧没导出，存在哪几帧就按哪几帧切）
		for (int f = DiveFirst; f <= DiveLast; f++)
		{
			if (Load($"res://p3makoto/ui/dive/dive_{f:000}.png") is Texture2D tex)
			{
				st.DiveFrames.Add((f, tex));
			}
		}
	}

	private const int DiveFirst = 20, DiveLast = 52;

	// (t, 下沉 dy / 屏高, 顺时针角度°, 横移 dx / 屏宽)：t = 原视频时间 − 0.30s。0.367~0.80 是量出来的，0.30 往前外推（人物刚从画面上沿进来）
	private static readonly (float T, float Dy, float Rot, float Dx)[] DiveKeys =
	{
		(0.30f, -0.78f, 36f, -0.10f), (0.367f, -0.48f, 28f, -0.085f), (0.40f, -0.36f, 20f, -0.08f), (0.433f, -0.24f, 17f, -0.07f),
		(0.467f, -0.19f, 12f, -0.04f), (0.533f, -0.14f, 9f, -0.02f), (0.60f, -0.06f, 2.5f, 0f), (0.70f, -0.03f, 1.5f, 0f), (0.80f, 0f, 0f, 0f),
	};

	private static (float, float, float) DivePose(float t)
	{
		var K = DiveKeys;
		if (t <= K[0].T) return (K[0].Dy, K[0].Rot, K[0].Dx);
		if (t >= K[^1].T) return (0f, 0f, 0f);
		int i = 0;
		while (t > K[i + 1].T) i++;
		float u = (t - K[i].T) / (K[i + 1].T - K[i].T);
		float H(Func<(float T, float Dy, float Rot, float Dx), float> f)
		{
			// 单调三次 Hermite：切线取两侧斜率的调和平均，不会过冲
			float Slope(int a) => (f(K[a + 1]) - f(K[a])) / (K[a + 1].T - K[a].T);
			float Tan(int j)
			{
				if (j == 0 || j == K.Length - 1) return 0f;
				float s0 = Slope(j - 1), s1 = Slope(j);
				return s0 * s1 <= 0f ? 0f : 2f * s0 * s1 / (s0 + s1);
			}
			float h = K[i + 1].T - K[i].T, u2 = u * u, u3 = u2 * u;
			return (2f * u3 - 3f * u2 + 1f) * f(K[i]) + (u3 - 2f * u2 + u) * h * Tan(i) + (-2f * u3 + 3f * u2) * f(K[i + 1]) + (u3 - u2) * h * Tan(i + 1);
		}
		return (H(k => k.Dy), H(k => k.Rot), H(k => k.Dx));
	}

	/// <summary>
	/// 入水转场每帧：按原视频时间（t + 0.30s）找到当前该显示的原版帧（原版水花本身就是每 2~4 帧换一张图的作画，照原样切，不插值）；
	/// 开头 0.03s 淡入，最后一帧之后 0.13s 淡出；水团的蓝色 0.30~0.42s 褪掉（下面的设置界面露出来）；遮罩 0.34s 起整屏。
	/// </summary>
	private static void Splash(State st, Vector2 view, float k, float t)
	{
		if (st.DiveFrames.Count == 0)
		{
			st.MaskMat.SetShaderParameter("full", 1f);
			st.SplashRect.Visible = false;
			return;
		}
		float src = (t + 0.30f) * 60f;
		Texture2D tex = st.DiveFrames[0].Tex;
		foreach ((int f, Texture2D tx) in st.DiveFrames)
		{
			if (f <= src)
			{
				tex = tx;
			}
		}
		if (st.DiveShown != tex)
		{
			st.DiveShown = tex;
			st.SplashMat.SetShaderParameter("frame_tex", tex);
			st.MaskMat.SetShaderParameter("frame_tex", tex);
		}
		float end = DiveLast / 60f - 0.30f;
		st.SplashMat.SetShaderParameter("alpha", Mathf.Clamp(t / 0.03f, 0f, 1f) * (1f - Mathf.Clamp((t - end) / 0.13f, 0f, 1f)));
		st.SplashMat.SetShaderParameter("tint", 1f - Ease.InOutCubic(Mathf.Clamp((t - 0.30f) / 0.12f, 0f, 1f)));
		st.MaskMat.SetShaderParameter("full", t >= 0.34f ? 1f : 0f);
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
