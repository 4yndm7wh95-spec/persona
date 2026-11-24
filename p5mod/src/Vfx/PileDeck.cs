using System.Collections.Generic;
using System.Linq;
using Godot;

namespace P5Joker;

/// <summary>
/// 牌组界面 = P5R 暂停菜单 EQUIP 页（排版 docs/p5/preview/pile_deck_layout_v2.png，用户 2026-11-24 认可）。
/// 静态部分是 tools/p5/pile/export_deck.py 从排版图分层导出的 1080p 图层（art/p5/ui/pile_deck → pile_dk_*）：
/// 背景块面（左上星纹拼贴 / 中间黑块 / 右边浅灰板，分界线是立绘自带白斜条两条边的延长线）、立绘 CAMPEQUIP_2D01、标题牌、COMMAND。
/// 运行时做：红星纹底（右下）、中间类型列表（原版「更换谁的装备？」名单的位置：类型名 + 原版斜体大数字，第一个有牌的类型垫青 / 红斜条）、入场 / 退场。
/// 牌格子在原版装备栏的位置，4 列，画在立绘上面（枪在格子下面露出来）。
/// 入场照原版 EQUIP 页序号 233–239 逐帧还原（tools/p5/menu3d/equip_entrance.py：逐帧拟合的机位 + 从原版截图取的区域形状，PileDeckEntrance.cs）：
/// 原版 3D Joker 低头 → 手举过头顶 → 拔枪，镜头从他正前方绕到左前方推近；主菜单的星纹弧带扫过、黑区（P 系列撞色：Joker 进去变纯白）、
/// 右上的红底 / 浅灰板逐帧变形状；然后硬切成 2D 立绘 + 最终排版（同原版第 240 帧），立绘压一下、列表一行行弹出、标题牌 / COMMAND 压上、牌飞入。
/// </summary>
public sealed class PileDeck : IPileDecor
{
	public float GridX => 1090f;
	public float GridTop => 140f;
	public float GridBottom => 880f;   // 格子下面露出原版那把枪（排版 v2：第三排只露半截）
	public int Cols => 4;
	public float CardW => 158f;
	public float CellW => 190f;
	public float CellH => 252f;
	public float CardsStart => PortraitAt + 0.06f;
	public float ExitTime => 0.32f;
	public Vector2 FocusAt => new(560f, 540f);
	public Vector2 InfoAt => new(980f, 300f);
	public Vector2 SwitchAt => new(1580f, 1004f);
	public Vector2 EnterOffset => new(70f, 30f);
	public Vector2 ExitOffset => new(260f, 40f);

	private static readonly Color Ink = PileDraw.Ink, Paper = PileDraw.Paper;
	/// <summary>原版一帧（录像 27.5fps）；第 k 帧在 FrameStart + k·FrameDt 显示，七帧放完硬切 2D 立绘（= 原版第 240 帧）。</summary>
	private const float FrameStart = 0.086f, FrameDt = 0.036f;
	private const int FrameCount = 7;
	private const float PortraitAt = FrameStart + FrameCount * FrameDt;
	/// <summary>原版 1920×1080 → 我们的排版（同弃牌堆：立绘配准对齐，缩放 0.977、左移 80）。</summary>
	private const float MapK = 1500f / 3072f / 0.5f;
	private static Vector2 Map(Vector2 p) => p * MapK + new Vector2(-80f, 0f);
	/// <summary>export_deck.py 的 layout.json：各图层左上角、红底多边形（1080p）。</summary>
	private static readonly Vector2 BgAt = Vector2.Zero, PortraitPos = Vector2.Zero, TitleAt = new(1443f, 79f), CommandAt = new(59f, 734f);
	/// <summary>中间类型列表：第一行左上角、行距、每行往左挪多少（跟着浅灰板的斜边）。</summary>
	private const float ListX = 880f, ListY = 128f, ListStep = 84f, ListSlant = 44f;   // 第一行在游戏顶栏（约 80px）下面
	private static readonly string[] Keys = { "deck", "attack", "skill", "power", "status", "curse" };

	private readonly Control _root;
	private readonly Vector2 _view;
	private readonly float _k, _rx;
	private readonly ShaderMaterial _bg;
	private readonly Node2D _back, _portrait, _list, _title, _command;
	private readonly List<(Node2D Node, float Delay, float Alpha)> _rows = new();
	private float _exitT0 = -1f;
	// 入场（原版 233–239）
	private readonly Node2D _entrance;
	/// <summary>原版 233–239 的 3D Joker：游戏里实时渲染（MenuJoker3D，关键帧 MenuJoker3DKeys.Deck；这一页拿着枪）。</summary>
	private readonly MenuJoker3D? _m3d;
	private static readonly string[] Hide3d = { "smartphone", "card", "school_bag", "ef line_obj_01", "ef line_obj_02", "ef line_obj_03", "ef line_obj_04", "ef line_obj_05", "ef line_obj_06", "ef line_obj_07", "ef line_obj_08" };
	private readonly List<Polygon2D> _star = new(), _gray = new(), _red = new(), _darkPaper = new(), _darkInvert = new();
	private int _shownFrame = -1;

	/// <summary>stats：总张数和各类型张数（PileScreen.DiscardStats 同一份统计）。</summary>
	public PileDeck(Control root, Vector2 view, (int Total, int Up, Dictionary<string, (int N, int Up)> ByType) stats)
	{
		var (total, _, counts) = stats;
		_root = root;
		_view = view;
		_k = view.Y / 1080f;
		_rx = view.X - 1920f * _k;
		float k = _k;

		// 1. 红星纹（同另外三页），只在右下那块（多边形裁剪），从右下角铺开
		_bg = PileDraw.StarMaterial(160f, new Color(0.466f, 0.034f, 0.06f), new Color(0.27f, 0.02f, 0.035f), new Color(0.27f, 0.02f, 0.035f), new Vector2(0.32f, 0.22f));
		_bg.SetShaderParameter("size", view);
		_bg.SetShaderParameter("wipe_origin", view);
		_bg.SetShaderParameter("wipe_dir", new Vector2(-1f, -0.62f).Normalized());
		_bg.SetShaderParameter("wipe", 0f);
		root.AddChild(new ColorRect { Name = "PileBg", Size = view, Material = _bg, MouseFilter = Control.MouseFilterEnum.Ignore });
		// 背景块面（黑 / 灰 / 星纹拼贴）：图层本身在红色那块是透明的，下面露出红星纹
		_back = Layer(root, "bg", BgAt * k);
		_portrait = Layer(root, "portrait", PortraitPos * k);

		// 1b. 入场（原版 233–239）：黑底 → 星纹 / 浅灰板 / 红底 → 黑区先画成白纸 → 3D Joker → 读屏把黑区反色（白纸 → 黑、黑 Joker → 白）
		_entrance = new Node2D { Name = "PileDeckEntrance" };
		root.AddChild(_entrance);
		_entrance.AddChild(new ColorRect { Size = view, Color = Ink, MouseFilter = Control.MouseFilterEnum.Ignore });
		ShaderMaterial starMat = PileDraw.StarMaterial(118f, new Color(0.91f, 0.91f, 0.91f), new Color(0.03f, 0.03f, 0.03f), new Color(0.376f, 0.376f, 0.376f), Vector2.Zero);
		starMat.SetShaderParameter("size", view);
		ShaderMaterial redMat = (ShaderMaterial)_bg.Duplicate();
		redMat.SetShaderParameter("wipe", 1e7f);
		Clipped(_star, PileDeckEntrance.Star, starMat);
		for (int i = 0; i < PileDeckEntrance.Gray.Max(f => f.Length); i++)
		{
			_gray.Add(new Polygon2D { Color = new Color(0.84f, 0.84f, 0.84f), Antialiased = true });
			_entrance.AddChild(_gray[^1]);
		}
		Clipped(_red, PileDeckEntrance.Red, redMat);
		Node2D paper = new();
		_entrance.AddChild(paper);
		if (MenuJoker3D.Available)
		{
			_m3d = new MenuJoker3D(_entrance, view, MapK, new Vector2(-80f, 0f), Hide3d);
			_entrance.AddChild(_m3d.Sprite);
		}
		_entrance.AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });
		Node2D inverted = new();
		_entrance.AddChild(inverted);
		ShaderMaterial invertMat = new() { Shader = PileDiscard.InvertShader };
		invertMat.SetShaderParameter("sum", Paper.R + Ink.R);
		for (int i = 0; i < PileDeckEntrance.Dark.Max(f => f.Length); i++)
		{
			_darkPaper.Add(new Polygon2D { Color = Paper, Antialiased = true });
			paper.AddChild(_darkPaper[^1]);
			_darkInvert.Add(new Polygon2D { Color = Colors.White, Antialiased = true, Material = invertMat });
			inverted.AddChild(_darkInvert[^1]);
		}

		// 2. 中间类型列表
		_list = new Node2D { Name = "PileDeckList" };
		root.AddChild(_list);
		int sel = -1;
		for (int i = 1; i < Keys.Length && sel < 0; i++)
			if (counts.TryGetValue(Keys[i], out var c0) && c0.N > 0) sel = i;
		for (int i = 0; i < Keys.Length; i++)
		{
			int n = i == 0 ? total : counts.TryGetValue(Keys[i], out var v) ? v.N : 0;
			float a = i == 0 || n > 0 ? 1f : 0.42f;
			Node2D row = new() { Position = new Vector2(ListX - i * ListSlant, ListY + i * ListStep) * k, Modulate = new Color(1f, 1f, 1f, a) };
			_list.AddChild(row);
			bool hl = i == sel;
			if (hl)
			{
				// 原版名单选中行：青 / 红斜条垫白底
				Vector2 o = new(-36f, -18f);
				Fx.Poly(row, Scale(new[] { new Vector2(0f, 54f), new(300f, 0f), new(334f, 78f), new(30f, 84f) }, o, 280f / 340f), new Color(0.9f, 0.1f, 0.11f));
				Fx.Poly(row, Scale(new[] { new Vector2(0f, 54f), new(150f, 27f), new(170f, 80f), new(30f, 84f) }, o, 280f / 340f), new Color(0.31f, 0.9f, 0.94f));
				Fx.Poly(row, Scale(new[] { new Vector2(24f, 10f), new(286f, 4f), new(300f, 70f), new(40f, 76f) }, o, 280f / 340f), Paper);
			}
			Color tc = hl ? Ink : Paper;
			float w = PileText.Word(row, Keys[i], Vector2.Zero, 44f * k, tc, !hl);
			Digits(row, n.ToString(), new Vector2(w + 24f * k, -6f * k), 52f * k, hl ? Ink : Paper);
			_rows.Add((row, PortraitAt + 0.02f + 0.035f * i, a));
		}

		// 3. 标题牌、COMMAND + 说明
		_title = Layer(root, "title", new Vector2(TitleAt.X * k + _rx, TitleAt.Y * k));
		_command = new Node2D();
		root.AddChild(_command);
		Layer(_command, "command", CommandAt * k);
		PileDraw.Caption(_command, "deck", total, new Vector2(270f, 960f) * k, 34f * k);

		Enter(0f);
	}

	/// <summary>一组按多边形裁剪的全屏材质（星纹 / 红底）。</summary>
	private void Clipped(List<Polygon2D> list, Vector2[][][] frames, ShaderMaterial mat)
	{
		for (int i = 0; i < frames.Max(f => f.Length); i++)
		{
			Polygon2D m = new() { Color = Colors.White, ClipChildren = CanvasItem.ClipChildrenMode.Only, Antialiased = true };
			m.AddChild(new ColorRect { Size = _view, Material = mat, MouseFilter = Control.MouseFilterEnum.Ignore });
			_entrance.AddChild(m);
			list.Add(m);
		}
	}

	/// <summary>原版坐标的区域点 → 屏幕：贴原版画面右 / 下边（生成时推到边外的点）推到屏幕边外（我们的排版把原版缩小了，原版右 / 下边缘落在屏幕里面）。</summary>
	private Vector2 MapEdge(Vector2 p)
	{
		Vector2 q = Map(p) * _k;
		if (p.X >= 1950f) q.X = _view.X + 40f;
		if (p.Y >= 1110f) q.Y = _view.Y + 40f;
		return q;
	}

	private void SetPolys(List<Polygon2D> nodes, Vector2[][] polys)
	{
		for (int i = 0; i < nodes.Count; i++)
		{
			bool on = i < polys.Length;
			nodes[i].Visible = on;
			if (on) nodes[i].Polygon = polys[i].Select(MapEdge).ToArray();
		}
	}

	/// <summary>入场第 u 帧（原版 233 + u，小数）：3D Joker 连续插值；四种区域取最近的补间形状（每帧 Sub 步）。</summary>
	private void ShowFrame(float u)
	{
		u = Mathf.Clamp(u, 0f, FrameCount - 1);
		_m3d?.Show(MenuJoker3DKeys.Deck, u);
		int f = Mathf.Clamp(Mathf.RoundToInt(u * PileDeckEntrance.Sub), 0, PileDeckEntrance.Dark.Length - 1);
		if (f == _shownFrame) return;
		_shownFrame = f;
		SetPolys(_star, PileDeckEntrance.Star[f]);
		SetPolys(_gray, PileDeckEntrance.Gray[f]);
		SetPolys(_red, PileDeckEntrance.Red[f]);
		SetPolys(_darkPaper, PileDeckEntrance.Dark[f]);
		SetPolys(_darkInvert, PileDeckEntrance.Dark[f]);
	}

	private Vector2[] Scale(Vector2[] pts, Vector2 offset, float s)
	{
		var o = new Vector2[pts.Length];
		for (int i = 0; i < pts.Length; i++) o[i] = (pts[i] * s + offset) * _k;
		return o;
	}

	private Node2D Layer(Node parent, string name, Vector2 at)
	{
		Node2D n = new() { Position = at };
		parent.AddChild(n);
		if (Fx.Tex("pile_dk_" + name) is Texture2D t)
			n.AddChild(new Sprite2D { Texture = t, Centered = false, Scale = Vector2.One * _k, TextureFilter = CanvasItem.TextureFilterEnum.Linear });
		return n;
	}

	/// <summary>原版斜体大数字（P5CAMP 00235–00244 → pile_dk_big0..9）。</summary>
	private static void Digits(Node2D parent, string text, Vector2 at, float h, Color tint)
	{
		float x = 0f;
		foreach (char c in text)
		{
			if (c < '0' || c > '9' || Fx.Tex($"pile_dk_big{c}") is not Texture2D t) continue;
			float w = t.GetWidth() * h / t.GetHeight();
			parent.AddChild(new Sprite2D { Texture = t, Centered = false, Position = at + new Vector2(x, 0f), Scale = Vector2.One * h / t.GetHeight(), Modulate = tint, TextureFilter = CanvasItem.TextureFilterEnum.Linear });
			x += w * 0.82f;
		}
	}

	public void Kick() { }

	public void BeginExit(float now)
	{
		if (_exitT0 >= 0f) return;
		_exitT0 = now;
		Sfx.Play("camp_cancel", 0.55f);
	}

	public void Update(float now, float dt)
	{
		_bg.SetShaderParameter("time", now);
		if (_exitT0 < 0f) Enter(now);
		else Exit(Mathf.Clamp((now - _exitT0) / ExitTime, 0f, 1f));
	}

	private static float Seg(float t, float a, float len) => Mathf.Clamp((t - a) / len, 0f, 1f);

	private void Enter(float t)
	{
		float k = _k;
		float reach = (_view + new Vector2(0f, _view.Y)).Length() * 1.05f;
		_bg.SetShaderParameter("wipe", t >= PortraitAt ? 1e7f : 0f);
		// 入场：七帧原版画面（星纹带 + 黑区 + 3D Joker + 红底 / 浅灰板），开头从右边甩进来；PortraitAt 硬切成 2D 立绘 + 最终排版（同原版第 240 帧）
		bool cut = t >= PortraitAt;
		_entrance.Visible = !cut;
		if (cut) _m3d?.Hide();
		if (!cut)
		{
			ShowFrame((t - FrameStart) / FrameDt - 0.5f);
			_entrance.Position = new Vector2(_view.X * 0.7f * (1f - Ease5.OutExpo(Seg(t, 0f, 0.1f))), 0f);
		}
		_back.Visible = _portrait.Visible = _list.Visible = cut;
		_back.Position = Vector2.Zero;
		_back.Modulate = Colors.White;
		// 切的那一下：立绘压一下再回弹 + 界面一震（同另外几页）
		float bump = cut ? Mathf.Lerp(0.965f, 1f, Ease5.OutBack(Seg(t, PortraitAt, 0.14f))) : 1f;
		_portrait.Scale = Vector2.One * bump;
		_portrait.Position = new Vector2(560f, 540f) * k * (1f - bump);
		_portrait.Modulate = Colors.White;
		_root.Position = cut ? new Vector2(Mathf.Sin((t - PortraitAt) * 44f) * 5f, Mathf.Cos((t - PortraitAt) * 34f) * 10f) * k * Mathf.Pow(Mathf.Max(0f, 1f - (t - PortraitAt) / 0.22f), 2f) : Vector2.Zero;
		// 列表一行行弹出（原版第 240 帧名单在一行行出来）
		foreach (var (node, delay, alpha) in _rows)
		{
			float r = Seg(t, delay, 0.2f);
			node.Scale = Vector2.One * Mathf.Lerp(0.8f, 1f, Ease5.OutBack(r));
			node.Modulate = new Color(1f, 1f, 1f, Mathf.Min(1f, r * 4f) * alpha);
		}
		Pop(_title, t, PortraitAt + 0.02f, new Vector2(TitleAt.X * k + _rx, TitleAt.Y * k));
		Pop(_command, t, PortraitAt + 0.05f, Vector2.Zero);
		_title.Visible = _command.Visible = cut;
	}

	private void Pop(Node2D node, float t, float delay, Vector2 at)
	{
		float p = Seg(t, delay, 0.2f);
		node.Position = at + new Vector2(60f * _k * (1f - Ease5.OutExpo(p)), 0f);
		node.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(p * 3f, 0f, 1f));
	}

	private void Exit(float e)
	{
		float k = _k;
		_root.Position = Vector2.Zero;
		float reach = (_view + new Vector2(0f, _view.Y)).Length() * 1.05f;
		_bg.SetShaderParameter("wipe", reach * (1f - Ease5Ext.InCubic(Seg(e, 0.15f, 0.85f))));
		float o = Ease5Ext.InOutCubic(Seg(e, 0f, 0.75f));
		_back.Position = new Vector2(1400f * k * o, 0f);
		_portrait.Position = new Vector2(-500f * k * o, 0f);
		_portrait.Scale = Vector2.One;
		Color fade = new(1f, 1f, 1f, 1f - Seg(e, 0.35f, 0.5f));
		_portrait.Modulate = _back.Modulate = fade;
		Color quick = new(1f, 1f, 1f, 1f - Mathf.Clamp(e * 1.8f, 0f, 1f));
		_title.Modulate = _command.Modulate = _list.Modulate = quick;
	}

	public static readonly string[] Textures =
	{
		"pile_dk_bg", "pile_dk_portrait", "pile_dk_title", "pile_dk_command",
		"pile_dk_big0", "pile_dk_big1", "pile_dk_big2", "pile_dk_big3", "pile_dk_big4", "pile_dk_big5", "pile_dk_big6", "pile_dk_big7", "pile_dk_big8", "pile_dk_big9",
	};
}
