using System.Collections.Generic;
using System.Linq;
using Godot;

namespace P5Joker;

/// <summary>
/// 弃牌堆界面 = P5R 暂停菜单 ITEM 页（用户 2026-11-23 定稿的排版 docs/p5/preview/pile_discard_layout_v4.png）。
/// 静态部分是 tools/p5/pile/export_discard.py 从排版图分层导出的 1080p 图层（art/p5/ui/pile_discard → pile_dc_*）：
/// 黑块 + 黑白星纹条、撞色立绘（原版 CAMPITEM_2D01，斜线右边黑 → 浅灰）、标题牌、COMMAND、左边列表每行的头像底 / 图标、两个标签牌。
/// 运行时做：红星纹底、左边面板（斜边 + 白边 + 内侧红条）、每行的张数 / 已升级数（原版数字字图）和文字、
/// 右下浅灰斜带和它下面的红底（盖在牌上面，牌从下面钻过去）、入场 / 退场。
/// 入场照原版 ITEM 页序号 177–185 逐帧还原（tools/p5/menu3d/item_entrance.py：逐帧拟合的机位 + 从原版截图取的区域形状，PileDiscardEntrance.cs）：
/// 主菜单的黑白星纹底上，原版 3D Joker 转身、手扫过脸、手举到头边，镜头一路推近；黑区（P 系列撞色：Joker 进去变白）和右下的红底逐帧变形状；
/// 然后硬切成 2D 立绘 + 最终排版（同原版第 186 帧），左边面板滑进、六行弹出、标题牌 / COMMAND 压上、斜带切进来、牌飞入。
/// </summary>
public sealed class PileDiscard : IPileDecor
{
	public float GridX => 1100f;   // 牌区右移一点：牌在立绘上面一层，第一列别压住浅灰身体和 COMMAND 的 D
	public float GridTop => 170f;
	public float GridBottom => 1060f;
	public int Cols => 3;
	public float CardW => 158f;
	public float CellW => 182f;
	public float CellH => 252f;
	public float CardsStart => CutAt + 0.02f;
	public float ExitTime => 0.32f;
	public Vector2 FocusAt => new(560f, 540f);
	public Vector2 InfoAt => new(980f, 300f);
	public Vector2 SwitchAt => new(1440f, 836f);
	public Vector2 EnterOffset => new(70f, 30f);
	public Vector2 ExitOffset => new(260f, 40f);

	private static readonly Color Ink = PileDraw.Ink, Paper = PileDraw.Paper;
	private static readonly Color Teal = new(92 / 255f, 230 / 255f, 200 / 255f), Pink = new(226 / 255f, 70 / 255f, 220 / 255f);
	private const float ListK = 0.84f;
	/// <summary>游戏顶栏的高度（1080p）：顶部的东西都放到它下面。</summary>
	private const float TopInset = 142f, ListSquash = 0.87f, TitleDrop = 62f;   // 列表第一行让开顶栏下面那排遗物（到 y≈148）   // 标题牌只让出顶栏露出来的那截，不压第一排牌
	private static readonly Vector2 EdgeA = new(474f, -20f), EdgeB = new(322f, 1100f);
	/// <summary>每行（export_discard.py 的 layout.json）：头像底图层左上角、排版用的 hx / yc（列表画布单位，再乘 ListK）、倾斜。</summary>
	private static readonly (string Key, Vector2 Img, float Hx, float Yc, float Tilt)[] Rows =
	{
		("discard", new(66f, 7f), 169.61f, 107.14f, -4f),
		("attack", new(102f, 185f), 203.53f, 321.43f, -2f),
		("skill", new(73f, 367f), 168.98f, 535.71f, -4f),
		("power", new(51f, 553f), 134.44f, 750f, -2f),
		("status", new(23f, 733f), 99.89f, 964.29f, -4f),
		("curse", new(3f, 896f), 65.35f, 1178.57f, -2f),
	};

	// ── 入场（原版序号 177–185，九帧）：帧图 / 区域都是原版屏幕坐标；原版立绘 = 缩放 0.5、左上 (0, 0)，我们的立绘 = 缩放 1500/3072、左上 (-80, 0)
	//    → 同一个变换 Map 把原版坐标换到我们的排版（3D → 2D 立绘的相对位置和原版一样）──
	private const float MapK = 1500f / 3072f / 0.5f;
	private static Vector2 Map(Vector2 p) => p * MapK + new Vector2(-80f, 0f);
	/// <summary>原版一帧（录像 27.5fps）；第 k 帧在 FrameStart + k·FrameDt 显示，九帧放完硬切 2D 立绘。</summary>
	private const float FrameDt = 0.036f;
	private const int FrameCount = 9;
	/// <summary>九帧放完正好是 PileDraw.SlamAt（camp_open 自带的那一声「啪」，用户 2026-11-24：音效没对齐）。</summary>
	private const float FrameStart = PileDraw.SlamAt - FrameCount * FrameDt;
	/// <summary>切成 2D 立绘的时刻（= 原版第 186 帧）。</summary>
	private const float CutAt = FrameStart + FrameCount * FrameDt;
	private static readonly string[] Hide3d = { "smartphone", "card", "school_bag", "gun_outline", "gun_main", "ef line_obj_01", "ef line_obj_02", "ef line_obj_03", "ef line_obj_04", "ef line_obj_05", "ef line_obj_06", "ef line_obj_07", "ef line_obj_08" };
	private readonly Node2D _entrance;
	/// <summary>原版 177–185 的 3D Joker：游戏里实时渲染（MenuJoker3D，关键帧 MenuJoker3DKeys.Discard）。</summary>
	private readonly MenuJoker3D? _m3d;
	private readonly Polygon2D _base;
	private readonly PileReveal _reveal;
	private readonly ShaderMaterial? _revealMat;
	private Vector2[][] _curDark = System.Array.Empty<Vector2[]>(), _curRed = System.Array.Empty<Vector2[]>();
	private readonly ShaderMaterial _invertMat;
	private readonly List<Polygon2D> _darkPaper = new(), _darkInvert = new(), _redMasks = new();
	private int _shownFrame = -1;

	private readonly Control _root;
	private readonly Vector2 _view;
	private readonly float _k, _rx;
	private readonly ShaderMaterial _bg;
	private readonly Node2D _slab, _portrait, _panel, _title, _command, _bandRoot;
	private readonly List<(Node2D Node, float Delay, float Alpha)> _rows = new();
	private readonly List<Node2D> _tags = new();
	private float _exitT0 = -1f;

	/// <summary>stats：总张数 / 总已升级 / 各类型（attack / skill / power / status / curse）的张数和已升级数（游戏里 PileScreen.DiscardStats 算）。</summary>
	public PileDiscard(Control root, Vector2 view, (int Total, int Up, Dictionary<string, (int N, int Up)> ByType) stats)
	{
		var (total, totalUp, counts) = stats;
		_root = root;
		_view = view;
		_k = view.Y / 1080f;
		_rx = view.X - 1920f * _k;
		float k = _k;
		Vector2 R(float x, float y) => new(x * k + (x > 960f ? _rx : 0f), y * k);

		// 1. 红星纹底（同另外两页），从右下角铺开
		_bg = PileDraw.StarMaterial(160f, new Color(0.466f, 0.034f, 0.06f), new Color(0.27f, 0.02f, 0.035f), new Color(0.27f, 0.02f, 0.035f), new Vector2(0.32f, 0.22f));
		_bg.SetShaderParameter("size", view);
		_bg.SetShaderParameter("wipe_origin", view);
		_bg.SetShaderParameter("wipe_dir", new Vector2(-1f, -0.62f).Normalized());
		_bg.SetShaderParameter("wipe", 0f);
		root.AddChild(new ColorRect { Name = "PileBg", Size = view, Material = _bg, MouseFilter = Control.MouseFilterEnum.Ignore });

		// 2. 黑块 + 星纹条、3. 撞色立绘
		_slab = Layer(root, "slab", R(548f, 0f));
		_portrait = Layer(root, "portrait", Vector2.Zero);
		// 2b. 入场（原版 177–185）：主菜单的黑白星纹底 → 黑区先画成白纸 → 3D Joker → 读屏把黑区反色（白纸 → 黑、黑 Joker → 白，同消耗堆的撞色）→ 红底
		_entrance = new Node2D { Name = "PileDiscardEntrance" };
		root.AddChild(_entrance);
		ShaderMaterial plateMat = PileDraw.StarMaterial(118f, new Color(0.91f, 0.91f, 0.91f), new Color(0.03f, 0.03f, 0.03f), new Color(0.376f, 0.376f, 0.376f), Vector2.Zero);
		plateMat.SetShaderParameter("size", view);
		// 揭幕（PileReveal）：整屏星纹底裁在一个跟着斜线走的多边形里
		_base = new Polygon2D { Color = Colors.White, ClipChildren = CanvasItem.ClipChildrenMode.Only };
		_base.AddChild(new ColorRect { Size = view, Material = plateMat, MouseFilter = Control.MouseFilterEnum.Ignore });
		_entrance.AddChild(_base);
		Node2D paper = new();
		_entrance.AddChild(paper);
		if (MenuJoker3D.Available)
		{
			_m3d = new MenuJoker3D(_entrance, view, MapK, new Vector2(-80f, 0f), Hide3d);
			_entrance.AddChild(_m3d.Sprite);
			_revealMat = new ShaderMaterial { Shader = PileReveal.Shader };
			_m3d.Sprite.Material = _revealMat;
		}
		_entrance.AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });
		Node2D inverted = new();
		_entrance.AddChild(inverted);
		_invertMat = new ShaderMaterial { Shader = InvertShader };
		_invertMat.SetShaderParameter("sum", Paper.R + Ink.R);
		int maxDark = PileDiscardEntrance.Dark.Max(f => f.Length), maxRed = PileDiscardEntrance.Red.Max(f => f.Length);
		for (int i = 0; i < maxDark; i++)
		{
			_darkPaper.Add(new Polygon2D { Color = Paper, Antialiased = true });
			paper.AddChild(_darkPaper[^1]);
			_darkInvert.Add(new Polygon2D { Color = Colors.White, Antialiased = true, Material = _invertMat });
			inverted.AddChild(_darkInvert[^1]);
		}
		ShaderMaterial redMat = (ShaderMaterial)_bg.Duplicate();
		redMat.SetShaderParameter("wipe", 1e7f);
		for (int i = 0; i < maxRed; i++)
		{
			Polygon2D m = new() { Color = Colors.White, ClipChildren = CanvasItem.ClipChildrenMode.Only, Antialiased = true };
			m.AddChild(new ColorRect { Size = view, Material = redMat, MouseFilter = Control.MouseFilterEnum.Ignore });
			_entrance.AddChild(m);
			_redMasks.Add(m);
		}
		_reveal = new PileReveal(_entrance, view);
		if (_revealMat != null) _reveal.Track(_revealMat);

		// 4. 左边面板：斜边（白边 + 黑 + 内侧红条）+ 六行 + 两个标签牌
		_panel = new Node2D { Name = "PileDiscardList" };
		root.AddChild(_panel);
		PileDraw.Poly(_panel, new[] { new Vector2(-40f, -20f), EdgeA + new Vector2(14f, 0f), EdgeB + new Vector2(14f, 0f), new Vector2(-40f, 1100f) }.Select(p => p * k).ToArray(), new Color(0.94f, 0.94f, 0.94f));
		PileDraw.Poly(_panel, new[] { new Vector2(-40f, -20f), EdgeA, EdgeB, new Vector2(-40f, 1100f) }.Select(p => p * k).ToArray(), new Color(0.03f, 0.03f, 0.03f));
		PileDraw.Poly(_panel, new[] { EdgeA + new Vector2(-22f, 0f), EdgeA + new Vector2(-15f, 0f), EdgeB + new Vector2(-15f, 0f), EdgeB + new Vector2(-22f, 0f) }.Select(p => p * k).ToArray(), new Color(0.78f, 0.08f, 0.12f));
		// 游戏顶栏（1080p 下约 80px）盖在界面最上面：列表整体下移 TopInset、纵向压到 ListSquash，六行都露出来（用户 2026-11-24 实机截图）
		Node2D list = new() { Position = new Vector2(0f, TopInset * k), Scale = new Vector2(1f, ListSquash) };
		_panel.AddChild(list);
		for (int i = 0; i < Rows.Length; i++)
		{
			var row = Rows[i];
			(int n, int up) = i == 0 ? (total, totalUp) : counts.TryGetValue(row.Key, out var v) ? v : (0, 0);
			Node2D node = new() { Modulate = new Color(1f, 1f, 1f, n > 0 ? 1f : 0.42f) };
			list.AddChild(node);
			Layer(node, $"row{i}", row.Img * k);
			RowText(node, row.Key, row.Hx, row.Yc, row.Tilt, n, up, i > 0);
			_rows.Add((node, 0.03f + 0.03f * i, n > 0 ? 1f : 0.42f));     // 切到最终排版之后的延迟
		}
		_tags.Add(Layer(list, "tag_discard", new Vector2(0f, 20f) * k));
		_tags.Add(Layer(list, "tag_bytype", new Vector2(0f, 282f) * k));

		// 5. 标题牌、COMMAND + 说明
		_title = Layer(root, "title", R(1458f, 7f + TitleDrop));
		_command = new Node2D();
		root.AddChild(_command);
		Layer(_command, "command", new Vector2(569f, 834f) * k);
		PileDraw.Caption(_command, "discard", total, new Vector2(690f, 1020f) * k, 34f * k);

		// 6. 右下浅灰斜带和它下面的红底：盖在牌上面（牌从下面钻过去，同原版道具列表只在黑块里露出来）——
		//    PileScreen 在装饰之后才加牌，所以下一帧把它挪到最上面，排列切换（PileHead）再挪到它上面
		_bandRoot = new Node2D { Name = "PileDiscardBand" };
		root.AddChild(_bandRoot);
		Vector2[] under = { R(1804.8f, 883.8f), R(1960f, 891.8f), R(1960f, 1120f), R(1193.5f, 1120f), R(1179.8f, 1093.8f), R(1223.7f, 1049.8f) };
		Polygon2D underMask = new() { Polygon = under, Color = Colors.White, ClipChildren = CanvasItem.ClipChildrenMode.Only };
		_bandRoot.AddChild(underMask);
		ShaderMaterial underMat = (ShaderMaterial)_bg.Duplicate();
		underMat.SetShaderParameter("wipe", 1e7f);
		underMask.AddChild(new ColorRect { Size = view, Material = underMat, MouseFilter = Control.MouseFilterEnum.Ignore });
		PileDraw.Poly(_bandRoot, new[] { R(1765.7f, 766.6f), R(1804.8f, 883.8f), R(1223.7f, 1049.8f) }, new Color(0.84f, 0.84f, 0.84f));
		Callable.From(() =>
		{
			if (!GodotObject.IsInstanceValid(_bandRoot) || !GodotObject.IsInstanceValid(root)) return;
			root.MoveChild(_bandRoot, root.GetChildCount() - 1);
			if (root.GetNodeOrNull("PileHead") is Node head) root.MoveChild(head, root.GetChildCount() - 1);
		}).CallDeferred();

		Enter(0f);
	}

	/// <summary>一张导出的图层（左上角放在 at，1080p 原尺寸 × k）。</summary>
	private Node2D Layer(Node parent, string name, Vector2 at)
	{
		Node2D n = new() { Position = at };
		parent.AddChild(n);
		if (Fx.Tex("pile_dc_" + name) is Texture2D t)
			n.AddChild(new Sprite2D { Texture = t, Centered = false, Scale = Vector2.One * _k, TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps });
		return n;
	}

	/// <summary>
	/// 一行的数字和文字（同 mock_discard.py 的 _list_body，列表画布单位再乘 ListK）：
	/// 斜体大张数（原版数字字图）→ 类型名；下面青色「已升级 n」+ 下划线；类型行再下面粉色英文 + 下划线。整行按 tilt 歪一点。
	/// </summary>
	private void RowText(Node2D parent, string key, float hx, float yc, float tilt, int n, int up, bool typeRow)
	{
		float s = ListK * _k;
		float x = hx + 74f;
		Node2D g = new() { Position = new Vector2(x, yc) * s, Rotation = Mathf.DegToRad(-tilt), Scale = Vector2.One * s };
		parent.AddChild(g);
		// 以下是相对 (x, yc) 的列表画布单位
		float dgw = Digits(g, "big", n.ToString(), new Vector2(0f, -60f), 90f, 0.82f, Colors.White);
		float nx = dgw + 6f;
		PileText.Word(g, key, new Vector2(nx, -66f), 38f, Paper);
		float lx0 = nx - 14f;
		float labW = PileText.Word(g, "upgraded", new Vector2(lx0, 14f), 24f, Teal);
		float upW = Digits(g, "small", up.ToString(), new Vector2(lx0 + labW + 8f, 8f), 30f, 0.86f, Colors.White);
		Underline(g, new Vector2(lx0 - 6f, 44f), labW + upW + 14f + 12f, Teal);
		if (typeRow)
		{
			float enW = PileText.Exact(g, key, new Vector2(lx0 + 4f, 56f), 26f, Pink);
			Underline(g, new Vector2(lx0 - 2f, 88f), enW + 14f, Pink);
		}
	}

	/// <summary>原版数字字图拼成的数（big = 斜体大数字，small = 青色小数字），返回宽度（画布单位）。</summary>
	private static float Digits(Node2D parent, string set, string text, Vector2 at, float h, float advance, Color tint)
	{
		float x = 0f, lastW = 0f;
		foreach (char c in text)
		{
			if (c < '0' || c > '9' || Fx.Tex($"pile_dc_{set}{c}") is not Texture2D t) continue;
			float w = t.GetWidth() * h / t.GetHeight();
			parent.AddChild(new Sprite2D { Texture = t, Centered = false, Position = at + new Vector2(x, 0f), Scale = Vector2.One * h / t.GetHeight(), Modulate = tint, TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps });
			x += w * advance;
			lastW = w;
		}
		return x + lastW * (1f - advance) + 4f;
	}

	private static void Underline(Node2D parent, Vector2 at, float w, Color tint)
	{
		if (Fx.Tex("pile_dc_underline") is not Texture2D t) return;
		parent.AddChild(new Sprite2D { Texture = t, Centered = false, Position = at, Scale = new Vector2(w / t.GetWidth(), 11f / t.GetHeight()), Modulate = tint, TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps });
	}

	public void Kick() { }

	private static Shader? _inv;
	/// <summary>入场的黑区：读屏把两种色调互换（sum = 纸色 + 墨色：白纸 → 墨黑，黑 Joker → 纸白，脸 → 暗），多边形边缘按覆盖率混合。</summary>
	internal static Shader InvertShader => _inv ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
uniform float sum = 0.98;
void fragment() {
	vec3 s = texture(screen_tex, SCREEN_UV).rgb;
	COLOR = vec4(clamp(vec3(sum) - s, 0.0, 1.0), COLOR.a);
}
" };

	/// <summary>原版坐标的区域点 → 屏幕：贴原版画面右 / 下边（生成时推到边外的点）推到屏幕边外（我们的排版把原版缩小了，原版右 / 下边缘落在屏幕里面）。</summary>
	private Vector2 MapEdge(Vector2 p)
	{
		Vector2 q = Map(p) * _k;
		if (p.X >= 1950f) q.X = _view.X + 40f;
		if (p.Y >= 1110f) q.Y = _view.Y + 40f;
		return q;
	}

	/// <summary>入场第 u 帧（原版 177 + u，小数）：3D Joker 连续插值；黑区、红底取最近的补间形状（每帧 Sub 步），再按揭幕的斜线裁。</summary>
	private void ShowFrame(float u)
	{
		u = Mathf.Clamp(u, 0f, FrameCount - 1);
		_m3d?.Show(MenuJoker3DKeys.Discard, u);
		int f = Mathf.Clamp(Mathf.RoundToInt(u * PileDiscardEntrance.Sub), 0, PileDiscardEntrance.Dark.Length - 1);
		if (f != _shownFrame)
		{
			_shownFrame = f;
			_curDark = PileDiscardEntrance.Dark[f].Select(p => p.Select(MapEdge).ToArray()).ToArray();
			_curRed = PileDiscardEntrance.Red[f].Select(p => p.Select(MapEdge).ToArray()).ToArray();
		}
		bool clip = !_reveal.Done;
		Vector2 big = _view + new Vector2(80f, 80f);
		_base.Polygon = _reveal.Clip(new[] { new Vector2(-40f, -40f), new Vector2(big.X, -40f), big, new Vector2(-40f, big.Y) });
		for (int i = 0; i < _darkPaper.Count; i++)
		{
			Vector2[] pts = i < _curDark.Length ? (clip ? _reveal.Clip(_curDark[i]) : _curDark[i]) : System.Array.Empty<Vector2>();
			bool on = pts.Length >= 3;
			_darkPaper[i].Visible = _darkInvert[i].Visible = on;
			if (on) _darkPaper[i].Polygon = _darkInvert[i].Polygon = pts;
		}
		for (int i = 0; i < _redMasks.Count; i++)
		{
			Vector2[] pts = i < _curRed.Length ? (clip ? _reveal.Clip(_curRed[i]) : _curRed[i]) : System.Array.Empty<Vector2>();
			bool on = pts.Length >= 3;
			_redMasks[i].Visible = on;
			if (on) _redMasks[i].Polygon = pts;
		}
	}

	public void BeginExit(float now)
	{
		if (_exitT0 >= 0f) return;
		_exitT0 = now;
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
		_bg.SetShaderParameter("wipe", t >= 0.3f ? 1e7f : Ease5.OutQuint(Seg(t, 0f, 0.3f)) * reach);
		// 入场：九帧原版画面（星纹底 + 黑区 + 3D Joker + 红底），开头从右边甩进来；CutAt 硬切成 2D 立绘 + 最终排版（同原版第 186 帧）
		bool cut = t >= CutAt;
		_entrance.Visible = !cut;
		if (cut) _m3d?.Hide();
		if (!cut)
		{
			// 斜切揭幕 + 3D 从第 0 帧就开始连续播，CutAt（= camp_open 的「啪」）正好放到最后一帧
			_reveal.Set(t);
			ShowFrame(PileReveal.Progress(t, CutAt, FrameCount - 1));
		}
		_slab.Visible = _portrait.Visible = _panel.Visible = _bandRoot.Visible = cut;
		_slab.Position = new Vector2(548f * k + _rx, 0f);
		_slab.Modulate = Colors.White;
		// 切的那一下：立绘压一下再回弹 + 界面一震（同另外两页砸下来的那一下）
		float bump = cut ? Mathf.Lerp(0.965f, 1f, Ease5.OutBack(Seg(t, CutAt, 0.14f))) : 1f;
		_portrait.Scale = Vector2.One * bump;
		_portrait.Position = new Vector2(760f, 540f) * k * (1f - bump);
		_portrait.Modulate = Colors.White;
		_root.Position = cut ? new Vector2(Mathf.Sin((t - CutAt) * 44f) * 5f, Mathf.Cos((t - CutAt) * 34f) * 10f) * k * Mathf.Pow(Mathf.Max(0f, 1f - (t - CutAt) / 0.22f), 2f) : Vector2.Zero;
		// 左边面板滑进来，六行依次弹出（原版第 186 帧队伍列表已经在了，这里快一点）
		float l = Ease5.OutExpo(Seg(t, CutAt, 0.16f));
		_panel.Position = new Vector2(-520f * k * (1f - l), 0f);
		foreach (var (node, delay, alpha) in _rows)
		{
			float r = Seg(t, CutAt + delay, 0.2f);
			node.Position = new Vector2(-140f * k * (1f - Ease5.OutBack(r)), 0f);
			node.Modulate = new Color(1f, 1f, 1f, Mathf.Min(1f, r * 4f) * alpha);
		}
		for (int i = 0; i < _tags.Count; i++)
		{
			float q = Ease5.OutBack(Seg(t, CutAt + 0.12f + 0.05f * i, 0.16f));
			_tags[i].Scale = Vector2.One * Mathf.Lerp(1.5f, 1f, q);
			_tags[i].Modulate = new Color(1f, 1f, 1f, Mathf.Min(1f, Seg(t, CutAt + 0.12f + 0.05f * i, 0.05f)));
		}
		Pop(_title, t, CutAt + 0.02f, R0(1458f, 7f + TitleDrop));
		Pop(_command, t, CutAt + 0.05f, Vector2.Zero);
		// 斜带从右下切进来
		float b = Ease5.OutExpo(Seg(t, CutAt + 0.02f, 0.22f));
		_bandRoot.Position = new Vector2(700f, 260f) * k * (1f - b);
	}

	private Vector2 R0(float x, float y) => new(x * _k + (x > 960f ? _rx : 0f), y * _k);

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
		_entrance.Visible = false;
		_slab.Visible = _portrait.Visible = _panel.Visible = _bandRoot.Visible = true;
		_portrait.Scale = Vector2.One;
		float reach = (_view + new Vector2(0f, _view.Y)).Length() * 1.05f;
		_bg.SetShaderParameter("wipe", reach * (1f - Ease5Ext.InCubic(Seg(e, 0.15f, 0.85f))));
		float o = Ease5Ext.InOutCubic(Seg(e, 0f, 0.75f));
		_slab.Position = new Vector2(548f * k + _rx + 1400f * k * o, 0f);
		_bandRoot.Position = new Vector2(900f, 300f) * k * o;
		_panel.Position = new Vector2(-600f * k * o, 0f);
		_portrait.Position = new Vector2(-400f * k * o, 0f);
		Color fade = new(1f, 1f, 1f, 1f - Seg(e, 0.35f, 0.5f));
		_portrait.Modulate = _slab.Modulate = fade;
		_title.Modulate = _command.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp(e * 1.8f, 0f, 1f));
		_title.Position = R0(1458f, 7f) + new Vector2(200f * k * Ease5Ext.InCubic(e), 0f);
	}

	public static readonly string[] Textures =
	{
		"pile_dc_slab", "pile_dc_portrait", "pile_dc_title", "pile_dc_command", "pile_dc_underline", "pile_dc_tag_discard", "pile_dc_tag_bytype",
		"pile_dc_row0", "pile_dc_row1", "pile_dc_row2", "pile_dc_row3", "pile_dc_row4", "pile_dc_row5",
	};
}
