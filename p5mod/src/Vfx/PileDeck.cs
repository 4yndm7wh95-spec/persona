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
/// 入场（用户 2026-11-24：原版截图抠出来的区域不规则、乱 → 自己设计干净的色块碰撞，PileField 一个着色器画完）：
/// 黑底从左边斜扫进来 → 左边星纹块、右边灰板、红底先后撞进来，停在最终排版的位置（L1 / L2 / 折线）；
/// 原版 3D Joker（233–241 逐帧拟合的动作和机位，MenuJoker3DKeys.Deck）从头到尾连续播，
/// 在黑区里反色（白）、在星纹 / 灰 / 红上是黑的；PortraitAt（0.42 秒）硬切 2D 立绘 + 最终排版（背景色块不变）+ 枪口闪光（同原版 242），
/// 立绘压一下、列表一行行弹出、标题牌 / COMMAND 压上、牌飞入。
/// </summary>
public sealed class PileDeck : IPileDecor
{
	public float GridX => 1090f;
	public float GridTop => 250f;   // 用户 2026-11-24：牌挡住右上角 MENU DECK 标题牌 → 往下挪（下面空间大）
	public float GridBottom => 990f;   // 格子下面露出原版那把枪（排版 v2：第三排只露半截）
	public int Cols => 3;
	public float CardW => 210f;   // 用户 2026-11-24：卡太小 → 放大约 30%、3 列
	public float CellW => 238f;
	public float CellH => 318f;
	public float CardsStart => PortraitAt + 0.06f;
	public float ExitTime => 0.32f;
	public Vector2 FocusAt => new(560f, 540f);
	public Vector2 InfoAt => new(980f, 300f);
	public Vector2 SwitchAt => new(1580f, 1004f);
	public Vector2 EnterOffset => new(70f, 30f);
	public Vector2 ExitOffset => new(260f, 40f);

	private static readonly Color Ink = PileDraw.Ink, Paper = PileDraw.Paper;
	/// <summary>
	/// 3D 入场的关键帧数：原版 233–241（用户 2026-11-24 第五轮：原版 240、241 还是 3D——侧身、手臂水平伸出、枪口朝右下、左臂全藏在身后，
	/// 姿势已经和立绘几乎一样；242 才硬切立绘、枪口闪一下）。239–241 的机位对原版逐帧拟合（fit/equip_example/fit241.py、fit241b.py：耳 / 眼 / 枪口 / 握把 + 大衣左边缘）。
	/// 之前几版把立绘当成 240 去拟合、机位往立绘的角度搬，角度不对（左臂、手肘露出来）。
	/// </summary>
	private const int FrameCount = 9;
	private const float PortraitAt = 0.42f;
	/// <summary>色块动画的节奏（用户 2026-11-24：切入别太拖沓）：下面 Field 里的时间点按 0.52 秒的版本写，整体压到 0.8 倍。</summary>
	private const float Pace = 0.8f;
	/// <summary>
	/// 3D 播到 241 就硬切立绘（同原版 241→242；之前的红边斜扫是为了盖住对不上的姿势，现在对上了就不用了），切的同时枪口闪光（MuzzleFlash）。
	/// 240→241 只差一点机位：LastAt 之前播到 240，最后 0.035 秒（原版一帧）带过 241 就切，别在最后停住。
	/// </summary>
	private const float LastAt = PortraitAt - 0.035f;
	/// <summary>枪口闪光的位置（立绘的枪口，原版 (1730, 830) 换到我们的排版）和大小（1080p 像素）。原版 242：浅灰圆盘 + 往左上的一道白线；243–245 圆盘；246 炸成碎片；250 没了。</summary>
	private static readonly Vector2 MuzzleAt = new(1730f * MapK - 80f, 830f * MapK);
	private const float FlashR = 72f;
	/// <summary>
	/// 立绘的手臂和枪（L2 右边那块）在扫过去的同时往外送：立绘的枪是夸张透视（比模型的枪长得多），按行往 L2 压到 ArmFrom，ArmTime 弹开。
	/// 收尾「240」第二版 3D 已经把手臂和枪伸到立绘的位置（fit240b.py），再压回去反而跳：ArmFrom = 1（不压）；以后要再用就改回 0.5。
	/// </summary>
	private const float ArmFrom = 1f, ArmTime = 0.14f;
	// 最终排版的块面分界（export_deck.py，原版坐标换到我们的排版）：星纹 | L1 | 黑（Joker 白）| L2 | 灰 | 折线 | 红
	private static float X1(float y) => 624.1f - 0.3275f * y;
	private static float X2(float y) => 1155.4f - 0.628f * y;
	private static readonly Vector2[] Zig = { new(1292.6f, -60f), new(1482.5f, 78.1f), new(1609.4f, 293f), new(1189.5f, 664.1f), new(1013.8f, 742.2f), new(759.8f, 1054.7f), new(690.5f, 1140f) };
	private static readonly Vector2 N1 = new Vector2(1f, 0.3275f).Normalized(), N2 = new Vector2(1f, 0.628f).Normalized();
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
	private readonly Node2D _flash, _flashDisc, _flashStreak;
	private readonly List<Node2D> _shards = new();
	private readonly ShaderMaterial? _armMat;
	private readonly List<(Node2D Node, float Delay, float Alpha)> _rows = new();
	private float _exitT0 = -1f;
	// 入场（原版 233–239）
	private readonly Node2D _entrance;
	private readonly PileField _field;
	/// <summary>原版 233–239 的 3D Joker：游戏里实时渲染（MenuJoker3D，关键帧 MenuJoker3DKeys.Deck；这一页拿着枪），PileField 直接采样它（黑区里反色）。</summary>
	private readonly MenuJoker3D? _m3d;
	private static readonly string[] Hide3d = { "smartphone", "card", "school_bag", "ef line_obj_01", "ef line_obj_02", "ef line_obj_03", "ef line_obj_04", "ef line_obj_05", "ef line_obj_06", "ef line_obj_07", "ef line_obj_08" };

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
		if (_portrait.GetChildCount() > 0 && _portrait.GetChild(0) is Sprite2D ps && ps.Texture is Texture2D pt)
		{
			_armMat = new ShaderMaterial { Shader = ArmShader };
			_armMat.SetShaderParameter("size", new Vector2(pt.GetWidth(), pt.GetHeight()));
			ps.Material = _armMat;
		}

		// 1b. 入场：色块合成 + 3D Joker（PileField）
		_entrance = new Node2D { Name = "PileDeckEntrance" };
		root.AddChild(_entrance);
		_field = new PileField(_entrance, view, PileField.Kind.Dark);
		if (MenuJoker3D.Available)
		{
			_m3d = new MenuJoker3D(_entrance, view, MapK, new Vector2(-80f, 0f), Hide3d) { UseSprite = false };
			_entrance.AddChild(_m3d.Sprite);
			_field.SetJoker(_m3d.Texture);
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
				PileDraw.Poly(row, Scale(new[] { new Vector2(0f, 54f), new(300f, 0f), new(334f, 78f), new(30f, 84f) }, o, 280f / 340f), new Color(0.9f, 0.1f, 0.11f));
				PileDraw.Poly(row, Scale(new[] { new Vector2(0f, 54f), new(150f, 27f), new(170f, 80f), new(30f, 84f) }, o, 280f / 340f), new Color(0.31f, 0.9f, 0.94f));
				PileDraw.Poly(row, Scale(new[] { new Vector2(24f, 10f), new(286f, 4f), new(300f, 70f), new(40f, 76f) }, o, 280f / 340f), Paper);
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

		// 4. 枪口闪光（切立绘那一刻）
		Color flashC = new(0.85f, 0.85f, 0.84f);
		_flash = new Node2D { Name = "PileDeckMuzzleFlash", Position = new Vector2(MuzzleAt.X * k + _rx, MuzzleAt.Y * k), Visible = false };
		root.AddChild(_flash);
		_flashStreak = new Node2D();
		_flash.AddChild(_flashStreak);
		PileDraw.Poly(_flashStreak, new[] { new Vector2(-14f, -40f), new Vector2(12f, -46f), new Vector2(-70f, -400f), new Vector2(-84f, -396f) }.Select(v => v * k).ToArray(), flashC);
		_flashDisc = new Node2D();
		_flash.AddChild(_flashDisc);
		var disc = new Vector2[11];
		for (int i = 0; i < disc.Length; i++)
		{
			float a = Mathf.Tau * i / disc.Length, r = FlashR * (i % 3 == 0 ? 1.06f : 0.96f);
			disc[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * k;
		}
		PileDraw.Poly(_flashDisc, disc, flashC);
		for (int i = 0; i < 9; i++)
		{
			var s = new Node2D { Rotation = Mathf.Tau * i / 9f + 0.3f * (i % 2) };
			_flash.AddChild(s);
			float len = (i % 3 == 0 ? 120f : 80f) * k, w = (i % 2 == 0 ? 9f : 6f) * k;
			PileDraw.Poly(s, new[] { new Vector2(0f, -w), new Vector2(len, 0f), new Vector2(0f, w) }, Colors.White);
			_shards.Add(s);
		}

		Enter(0f);
	}

	/// <summary>
	/// 入场第 t 秒的色块（1080p 排版坐标）。层从下往上：黑底（盖子）→ 星纹块 → 灰板 → 红底；运动中的前沿带一道描边，到位时收掉。
	/// </summary>
	private void Field(float t, float wipe = -1f)
	{
		t /= Pace;
		float W = _view.X / _k + 120f;
		if (wipe >= 0f)
		{
			// 切 2D：盖子换成斜边右边（左边露出下面的 2D 立绘 / 最终排版），斜边从左往右扫，红边跟在后面
			float xw = Mathf.Lerp(-460f, W + 300f, wipe);
			_field.Begin(PileShapes.RightOf(new Vector2(xw + 0.3275f * 600f, -60f), new Vector2(xw - 0.3275f * 600f, 1140f)), 14f);
		}
		else
		{
			// 1. 黑底从左边扫进来（边和 L1 平行），前沿红边
			float c = PileShapes.OutCubic(PileShapes.Seg(t, 0f, 0.15f));
			float xc = Mathf.Lerp(-260f, W + 300f, c);
			_field.Begin(PileShapes.LeftOf(new Vector2(xc + 0.3275f * 600f, -60f), new Vector2(xc - 0.3275f * 600f, 1140f)), 18f * (1f - c));
		}
		// 2. 星纹块从左边撞进来，停在 L1（带一点回弹）
		float s = PileShapes.Seg(t, 0.12f, 0.2f);   // 黑底一扫完就撞进来（中间那道横条去掉了，不留空档）
		if (s > 0f)
		{
			Vector2 off = N1 * (-900f * (1f - PileShapes.OutBack(s, 1.0f)));
			_field.Add(PileShapes.LeftOf(new Vector2(X1(-60f), -60f) + off, new Vector2(X1(1140f), 1140f) + off, 140f * (1f - s)), PileField.Kind.Star, 16f * (1f - PileShapes.OutCubic(s)));
		}
		// 3. 灰板从右下撞进来（用户 2026-11-25：之前中间还有一道斜着劈过去的横条，星纹 / 红色都试过，横条本身和黑白块面不搭，去掉），停在 L2
		float g = PileShapes.Seg(t, 0.23f, 0.2f);
		if (g > 0f)
		{
			Vector2 off = N2 * (1000f * (1f - PileShapes.OutBack(g, 1.0f)));
			_field.Add(PileShapes.RightOf(new Vector2(X2(-60f), -60f) + off, new Vector2(X2(1140f), 1140f) + off, 120f * (1f - g)), PileField.Kind.Gray, 16f * (1f - PileShapes.OutCubic(g)));
		}
		// 4. 红底从右边滑进来，停在折线
		float r = PileShapes.Seg(t, 0.3f, 0.16f);
		if (r > 0f)
		{
			float dx = 800f * (1f - PileShapes.OutQuint(r));
			var red = new Vector2[Zig.Length + 2];
			for (int i = 0; i < Zig.Length; i++) red[i] = Zig[i] + new Vector2(dx, 0f);
			red[Zig.Length] = new Vector2(W + PileShapes.Far, 1140f);
			red[Zig.Length + 1] = new Vector2(W + PileShapes.Far, -60f);
			_field.Add(red, PileField.Kind.Red, 12f * (1f - PileShapes.OutCubic(r)), Ink);
		}
		_field.Commit();
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
			n.AddChild(new Sprite2D { Texture = t, Centered = false, Scale = Vector2.One * _k, TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps });
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
			parent.AddChild(new Sprite2D { Texture = t, Centered = false, Position = at + new Vector2(x, 0f), Scale = Vector2.One * h / t.GetHeight(), Modulate = tint, TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps });
			x += w * 0.82f;
		}
	}

	public void Kick() { }

	private static Shader? _armShader;
	/// <summary>立绘的手臂 / 枪（L2 右边）按行往 L2 压扁：arm = 1 原样，arm &lt; 1 每一行 L2 右边的内容缩到 arm 倍长（袖子根部贴着 L2 不动）。</summary>
	internal static Shader ArmShader => _armShader ??= new Shader { Code = @"
shader_type canvas_item;
uniform vec2 size = vec2(1737.0, 1080.0);
uniform float arm = 1.0;
varying vec4 vcol;
void vertex() { vcol = COLOR; }
void fragment() {
	vec2 p = UV * size;
	float l2 = 1155.4 - 0.628 * p.y;
	vec4 c;
	if (arm < 0.999 && p.x > l2) {
		float qx = l2 + (p.x - l2) / max(arm, 0.05);
		c = qx < size.x ? texture(TEXTURE, vec2(qx / size.x, UV.y)) : vec4(0.0);
	} else {
		c = texture(TEXTURE, UV);
	}
	COLOR = c * vcol;
}
" };

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
		_bg.SetShaderParameter("wipe", t >= PortraitAt ? 1e7f : 0f);
		// 入场：色块碰撞 + 3D（原版 233–241）；PortraitAt 硬切 2D 立绘 + 最终排版 + 枪口闪光（同原版 242）
		bool cut = t >= PortraitAt;
		_entrance.Visible = !cut;
		if (cut) _m3d?.Hide();
		else
		{
			// 色块碰撞 + 3D 从第 0 帧就开始连续播：LastAt 到 240，再 0.035 秒带过 241，PortraitAt 切
			float u = t < LastAt ? PileReveal.Progress(t, LastAt, FrameCount - 2) : FrameCount - 2 + Seg(t, LastAt, PortraitAt - LastAt);
			_m3d?.Show(MenuJoker3DKeys.Deck, u);
			_field.SetTime(t);
			Field(t, -1f);
		}
		Flash(t - PortraitAt);
		_back.Visible = _portrait.Visible = _list.Visible = cut;
		_back.Position = Vector2.Zero;
		_back.Modulate = Colors.White;
		// 切的那一下：立绘压一下再回弹 + 界面一震（同另外几页）
		float bump = cut ? Mathf.Lerp(0.965f, 1f, Ease5.OutBack(Seg(t, PortraitAt, 0.14f))) : 1f;
		_portrait.Scale = Vector2.One * bump;
		_portrait.Position = new Vector2(560f, 540f) * k * (1f - bump);
		_armMat?.SetShaderParameter("arm", cut ? Mathf.Lerp(ArmFrom, 1f, PileShapes.OutCubic(Seg(t, PortraitAt + 0.03f, ArmTime))) : ArmFrom);
		_portrait.Modulate = Colors.White;
		_root.Position = Vector2.Zero;   // 切立绘不震（用户 2026-11-24：头晕；枪口闪光已经够有冲击感）
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

	/// <summary>枪口闪光（e = 切立绘后的秒数）：一开始圆盘 + 往左上的白线（一帧），圆盘留到 0.12 秒，然后炸成一圈白碎片往外飞、0.24 秒散完。</summary>
	private void Flash(float e)
	{
		_flash.Visible = e >= 0f && e < 0.24f;
		if (!_flash.Visible) return;
		_flashStreak.Visible = e < 0.04f;
		_flashDisc.Visible = e < 0.12f;
		_flashDisc.Scale = Vector2.One * Mathf.Lerp(0.8f, 1f, Ease5.OutExpo(Seg(e, 0f, 0.05f)));
		float b = Seg(e, 0.11f, 0.13f);
		for (int i = 0; i < _shards.Count; i++)
		{
			var s = _shards[i];
			s.Visible = e >= 0.11f;
			s.Position = Vector2.FromAngle(s.Rotation) * (40f + 150f * Ease5.OutExpo(b)) * _k;
			s.Scale = new Vector2(Mathf.Lerp(1f, 0.3f, b), Mathf.Lerp(1f, 0.5f, b));
			s.Modulate = new Color(1f, 1f, 1f, 1f - b * b);
		}
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
		_armMat?.SetShaderParameter("arm", 1f);
		_flash.Visible = false;
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
