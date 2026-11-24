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
/// 入场（用户 2026-11-24：原版截图抠出来的区域不规则、乱 → 自己设计干净的色块碰撞，PileField 一个着色器画完）：
/// 黑白星纹从右边扫进来（前沿红边）→ 一把扇形从撞色斜线的底端转上来（黑块里是黑、外面是红，黑的前沿一道红条），
/// 同时左边的红底从左边撞到斜线上，星纹被两边挤成头顶那条星纹条 → 右下浅灰斜带从下面撞进黑块；
/// 原版 3D Joker（177–185 逐帧拟合的动作 + 收尾「186」= 立绘的机位和姿势，MenuJoker3DKeys.Discard）从第 0 帧连续播，前快后收，
/// 最后一段机位压低、脸抬起来看玩家，正好在 CutAt 落到立绘的样子；在黑块里反色成浅灰（同立绘），别处是黑的；CutAt（0.40 秒）切成 2D 立绘 + 最终排版（色块位置不变），立绘压一下、左边面板滑进、六行弹出、
/// 标题牌 / COMMAND 压上、牌飞入。
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

	// ── 入场：色块碰撞（PileField）+ 原版 177–185 的 3D Joker ──
	/// <summary>原版立绘 = 缩放 0.5、左上 (0, 0)，我们的立绘 = 缩放 1500/3072、左上 (-80, 0)：3D 的取景按同一个变换放进来（3D → 2D 立绘的相对位置和原版一样）。</summary>
	private const float MapK = 1500f / 3072f / 0.5f;
	/// <summary>3D 关键帧数（原版 177–185 + 收尾「186」）；CutAt 切成 2D 立绘（= 原版第 186 帧）。</summary>
	private const int FrameCount = 10;
	private const float CutAt = 0.40f;   // 斜带 0.384 秒到位，紧接着切
	/// <summary>色块动画的节奏（用户 2026-11-24：切入别太拖沓）：Field 里的时间点按 0.52 秒的版本写，整体压到 0.8 倍。</summary>
	private const float Pace = 0.8f;
	/// <summary>
	/// 3D 动作放到切 2D 那一刻（MotionEnd = CutAt）：
	/// 用户 2026-11-24 第一次：手往外甩一下才切，像回弹、拖沓 → 改成 0.29 秒收进 185 的姿势定住；
	/// 第二次：动作早就到位了却迟迟不变成立绘，而且 185（俯拍、低头、眼睛被头发挡）和立绘（抬眼看玩家）对不上，切的时候跳。
	/// 现在 177–185 照旧前快后收（0.29 秒左右走到 185），剩下的时间是收尾这一段：机位压低、脸抬起来看玩家，CutAt 正好落在立绘的样子，没有定住干等。
	/// </summary>
	private const float MotionEnd = CutAt, MotionPower = 2.0f;
	// 最终排版的块面（mock_discard.py / export_discard.py 的黑块、星纹条、斜带，1080p；伸出屏幕的边延长到画面外）：
	// 撞色斜线 x = Lx(y) 是黑块的左边，也是星纹条的右边；黑块外面全是红底
	private static float Lx(float y) => 1002.66f - 0.419f * y;
	private static readonly Vector2[] Slab = { new(956.8f, 109.4f), new(1553.1f, -60f), new(1565.0f, -60f), new(1804.8f, 883.8f), new(1223.7f, 1049.8f), new(1133.6f, 1140f), new(524.9f, 1140f) };
	private static readonly Vector2[] Strip = { new(683.2f, -60f), new(1027.8f, -60f), new(818.3f, 440f), new(740f, 440f) };
	private static readonly Vector2[] Band = { new(1765.7f, 766.6f), new(1804.8f, 883.8f), new(1223.7f, 1049.8f) };
	/// <summary>斜带长边（黑块底边）的法线，朝右下：斜带顺着它从下面撞进来。</summary>
	private static readonly Vector2 BandN = new Vector2(166.1f, 581.1f).Normalized();
	/// <summary>扇形的轴心（斜线底端，屏幕下面）和转到的角度（= 斜线的方向）。</summary>
	private static readonly Vector2 FanPivot = new(Lx(1140f), 1140f);
	private static readonly float FanEnd = Mathf.Atan2(-1200f, Lx(-60f) - Lx(1140f));
	private static readonly Color StripeRed = new(0.86f, 0.06f, 0.1f);
	private static readonly string[] Hide3d = { "smartphone", "card", "school_bag", "gun_outline", "gun_main", "ef line_obj_01", "ef line_obj_02", "ef line_obj_03", "ef line_obj_04", "ef line_obj_05", "ef line_obj_06", "ef line_obj_07", "ef line_obj_08" };
	private readonly Node2D _entrance;
	private readonly PileField _field;
	/// <summary>原版 177–185 的 3D Joker：游戏里实时渲染（MenuJoker3D，关键帧 MenuJoker3DKeys.Discard），PileField 直接采样它（黑块里反色）。</summary>
	private readonly MenuJoker3D? _m3d;

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
		// 2b. 入场：色块合成 + 3D Joker（PileField；底是黑白星纹，盖子从右边扫进来）
		_entrance = new Node2D { Name = "PileDiscardEntrance" };
		root.AddChild(_entrance);
		_field = new PileField(_entrance, view, PileField.Kind.Star);
		_field.SetInvert(new Color(8 / 255f, 8 / 255f, 8 / 255f), new Color(222 / 255f, 222 / 255f, 222 / 255f), new Color(8 / 255f, 8 / 255f, 8 / 255f));
		if (MenuJoker3D.Available)
		{
			_m3d = new MenuJoker3D(_entrance, view, MapK, new Vector2(-80f, 0f), Hide3d) { UseSprite = false };
			_entrance.AddChild(_m3d.Sprite);
			_field.SetJoker(_m3d.Texture);
		}

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
		AaPoly underMask = new() { Polygon = under, Color = Colors.White, ClipChildren = CanvasItem.ClipChildrenMode.Only };
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

	/// <summary>
	/// 入场第 t 秒的色块（1080p 排版坐标；宽屏时黑块 / 星纹条 / 斜带跟右半边一起右移 sx）。
	/// 层从下往上：黑白星纹底（盖子）→ 左边红底 → 星纹条 → 扇形（红）→ 扇形 ∩ 黑块（黑）→ 黑的前沿红条 → 浅灰斜带。
	/// </summary>
	private void Field(float t)
	{
		t /= Pace;
		float W = _view.X / _k + 120f, sx = _rx / _k;
		Vector2 shift = new(sx, 0f);
		// 1. 盖子：黑白星纹从右边扫进来（边和撞色斜线平行），前沿红边
		float c = PileShapes.OutCubic(PileShapes.Seg(t, 0f, 0.15f));
		float xc = Mathf.Lerp(W + 300f, -300f, c);
		_field.Begin(PileShapes.RightOf(new Vector2(xc + 0.419f * 600f, -60f), new Vector2(xc - 0.419f * 600f, 1140f)), 18f * (1f - c));
		// 2. 左边红底从左边撞到斜线上（前沿黑边，到位收掉）
		float r = PileShapes.Seg(t, 0.13f, 0.2f);
		if (r > 0f)
		{
			float dx = 1300f * (1f - PileShapes.OutQuint(r));
			_field.Add(PileShapes.LeftOf(new Vector2(Lx(-60f) + sx - dx, -60f), new Vector2(Lx(1140f) + sx - dx, 1140f)), PileField.Kind.Red, 14f * (1f - PileShapes.OutCubic(r)), Ink);
		}
		// 3. 星纹条：压在红底上面（红底从它下面钻过去，最后只剩头顶这一条星纹）
		_field.Add(PileShapes.Shift(Strip, shift), PileField.Kind.Star);
		// 4. 扇形从斜线底端转上来，停在斜线上：黑块里是黑（Joker 反色），黑块外是红；黑的前沿一道红条，到位收掉
		float f = PileShapes.Seg(t, 0.05f, 0.24f);
		if (f > 0f)
		{
			float e = PileShapes.OutCubic(f);
			Vector2 pivot = FanPivot + shift;
			float th = FanEnd * e;
			Vector2 tip = pivot + new Vector2(Mathf.Cos(th), Mathf.Sin(th)) * 3000f;
			float bulge = 100f * (1f - e);
			Vector2[] fan = PileShapes.Side(pivot, tip, bulge);
			Vector2[] slab = PileShapes.Shift(Slab, shift);
			_field.Add(fan, PileField.Kind.Red);
			_field.Add(PileShapes.Intersect(slab, fan), PileField.Kind.Dark);
			if (PileShapes.EdgeStripe(pivot, tip, bulge, 18f * (1f - e)) is Vector2[] stripe)
				_field.Add(PileShapes.Intersect(slab, stripe), PileField.Kind.Solid, 0f, StripeRed);
		}
		// 5. 右下浅灰斜带从下面撞进黑块（带一点回弹）
		float g = PileShapes.Seg(t, 0.31f, 0.17f);
		if (g > 0f)
			_field.Add(PileShapes.Shift(Band, shift + BandN * (320f * (1f - PileShapes.OutBack(g, 1.0f)))), PileField.Kind.Gray, 12f * (1f - PileShapes.OutCubic(g)));
		_field.Commit();
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
		// 红星纹底：入场里由 PileField 画（同一套星星、同一个时间），切到最终排版时才露出来
		_bg.SetShaderParameter("wipe", t >= CutAt ? 1e7f : 0f);
		// 入场：色块碰撞 + 3D 从第 0 帧就开始连续播，一路连到收尾「186」（= 立绘）；CutAt 硬切成 2D 立绘 + 最终排版（同原版第 186 帧）
		bool cut = t >= CutAt;
		_entrance.Visible = !cut;
		if (cut) _m3d?.Hide();
		else
		{
			_m3d?.Show(MenuJoker3DKeys.Discard, PileReveal.Progress(t, MotionEnd, FrameCount - 1, MotionPower));
			_field.SetTime(t);
			Field(t);
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
		// 斜带入场里已经撞进来了，切过去原地不动
		_bandRoot.Position = Vector2.Zero;
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
