using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 消耗堆界面 = P5R 暂停菜单 SKILL 页（docs/p5/PILE_PLAN.md，排版图 docs/p5/preview/pile_exhaust_layout_v2.png）。
/// 层次：红星纹底（不透明）→ 黑色大斜块 + 左下浅灰楔形（原版 SKILL 页的构图，矢量多边形）→ 立绘后面的黑白星纹斜带（同一份原版星纹数据）
/// → 原版 Joker 咧嘴笑立绘（CAMPSKILL_2D01；放大到下切口在屏幕底边外、左切口在屏幕左边外，用户：不要露出立绘边缘）
/// → 右上标题牌 MeNu&amp;EXHAUST → COMMAND + 说明；牌格子在右边黑块里（原版技能列表的位置）。
/// 入场照原版 SKILL 页 frame 112–123 逐帧还原（tools/p5/menu3d/skill_entrance.py 拟合的机位和动作，research/p5r/menu3d_skill）：
/// 红底从右下角铺开、白板扫进来；原版 3D Joker（main_0015 抬膝）低头站着，镜头慢慢右摇；然后镜头一仰、他一脚踩进来，
/// 混合进定格姿势 main_0016（最后一帧 = 2D 立绘的姿势和机位）；砸中那一刻（PileDraw.SlamAt，camp_open 的「啪」）硬切成 2D 立绘，界面一震。
/// P 系列的「区域反色」（用户 2026-11-22）：白板区域里 Joker 是白的（白底上看不见），黑色区域从直线 L 往右扫开，
/// 区域里白板变黑、伸进来的那条腿就成了黑底上的白腿；L 正好是 2D 立绘里白腿左边那条直边，切到立绘时严丝合缝。
/// </summary>
public sealed class PileExhaust : IPileDecor
{
	public float GridX => 1130f;
	public float GridTop => 214f;
	public float GridBottom => 1050f;
	public int Cols => 3;
	public float CardW => 210f;   // 用户 2026-11-24：卡太小 → 放大约 30%、3 列
	public float CellW => 238f;
	public float CellH => 318f;
	public float CardsStart => PileDraw.SlamAt - 0.02f;     // 砸中之后牌才从右边飞进来
	public float ExitTime => 0.32f;
	public Vector2 FocusAt => new(560f, 560f);
	public Vector2 InfoAt => new(980f, 300f);
	public Vector2 SwitchAt => new(1130f, 152f);   // 遗物栏（到 y≈148）下面
	public Vector2 EnterOffset => new(70f, 30f);
	public Vector2 ExitOffset => new(260f, 40f);

	private const float SlamAt = PileDraw.SlamAt, HandAt = SlamAt - 0.06f;
	private static readonly Color Ink = PileDraw.Ink, Paper = PileDraw.Paper, Gray = new(0.84f, 0.84f, 0.84f);
	private static readonly Vector2 PorAt = new(-80f, 40f);
	private const float PorW = 1500f;

	// ── 原版屏幕坐标 → 我们的排版：原版 2D 立绘（缩放 0.50795、左上 (0.47, 18.05)，拿 frame 123/124 配准的）对齐到我们的 PorAt / PorW，
	//    3D 帧、白板边、反色区都用这同一个变换，所以 3D → 2D 的相对位置和原版一模一样 ──
	private const float OrigArtScale = 0.50795f;
	private static readonly Vector2 OrigArtAt = new(0.47f, 18.05f);
	private const float MapK = PorW / 3072f / OrigArtScale;
	private static Vector2 Map(Vector2 p) => (p - OrigArtAt) * MapK + PorAt;

	/// <summary>原版一帧的时长（录像约 27.5fps = 0.0364s，稍微压快一点）；第 i 帧（原版 112+i）在 FrameT(i) 显示，原版 123（2D 立绘）= HandAt。</summary>
	private const float FrameDt = 0.0335f;
	private static float FrameT(float i) => HandAt - (11f - i) * FrameDt;
	/// <summary>白板左边缘（原版屏幕坐标，y=500 / y=900 两点）：原版 112–117 量出来的，118 起 = 反色区左边的直线 L（= 立绘白腿的直边）。</summary>
	private static readonly (Vector2 A, Vector2 B)[] EdgeKeys =
	{
		(new(905f, 500f), new(855f, 900f)), (new(980f, 500f), new(783f, 900f)), (new(1028f, 500f), new(750f, 900f)),
		(new(1054f, 500f), new(731f, 900f)), (new(1056f, 500f), new(727f, 900f)), (new(1056f, 500f), new(727f, 900f)),
		(new(834f, 500f), new(584f, 900f)),
	};
	/// <summary>
	/// 黑色反色区 = 圆内 ∩ 白板（白板此时左边就是 L）。原版 118–120 的弧线拟合成同心圆、半径逐帧变大（均方根误差 15px）：
	/// 117 半径刚好擦到 L（还没有黑区），118 一下子扫开一大片，之后继续往右，121 盖满。
	/// </summary>
	private static readonly Vector2 RCenter = new(185f, -397f);
	private static readonly float[] RRadius = { 1026f, 1518f, 1621f, 1719f, 2000f, 2300f };

	private readonly Control _root;
	private readonly Vector2 _view;
	private readonly float _k, _rx;
	private readonly ShaderMaterial _bg, _por, _coverMat, _invertMat;
	private readonly AaPoly _slab, _wedge, _plate, _cover, _invert;
	/// <summary>黑板底边那条直线（屏幕坐标）：线上一点、朝下的法线；反色圆只在线上面起作用，线下面一直是白板。</summary>
	private readonly Vector2 _lowA, _lowN;
	/// <summary>右侧分区的折线（我们排版的 1080p 坐标，未加 _rx）：顶边 _top0 → 拐点 _vtx → 底边 _redB；黑板底边直线 _lp → _lq。</summary>
	private readonly Vector2 _top0, _vtx, _redB, _lp, _lq;
	private readonly Vector2[] _slabPts, _platePts;
	private readonly Node2D _band, _portrait;
	private readonly AaPoly _bandEdge;
	private readonly Vector2[] _bandPts;
	/// <summary>原版 112–122 的 3D Joker：游戏里实时渲染（MenuJoker3D，关键帧 MenuJoker3DKeys.Exhaust）。</summary>
	private readonly MenuJoker3D? _m3d;
	private readonly PileReveal? _reveal;
	private static readonly string[] Hide3d = { "smartphone", "card", "school_bag", "gun_outline", "gun_main", "ef line_obj_01", "ef line_obj_02", "ef line_obj_03", "ef line_obj_04", "ef line_obj_05", "ef line_obj_06", "ef line_obj_07", "ef line_obj_08" };
	private readonly Sprite2D _porSprite;
	private readonly float _porScale = 1f;
	private readonly List<(Node2D Node, Vector2 At, float Rot, float Delay)> _pops = new();
	/// <summary>
	/// 黑板落下时的两条细白线（用户 2026-11-24：原版 SKILL 页 118–122，「把画面分成三部分」）：
	/// · 下面那条 = 白板（灰）和黑区分界的延长线（用户：线是白色那块的延长线，要接上）：跟着反色圆的弧从右上顺时针转到右下，
	///   122 落定成黑板底边直线的延长；画在黑板 / 白板下面、红底上面，所以只在红底上露出来（LoLine 按我们自己的分界算，不套原版坐标）。
	/// · 上面那条 = 黑板左边缘直线 L 上的白描边（L 同时是星纹条的右边缘、立绘白腿的直边），118 跟着黑板出现、略偏右，121 收到 L 上，之后一直在；
	///   画在立绘上面（原版压过头发和眼睛旁边）。
	/// </summary>
	private readonly AaPoly _lineLo, _lineHi;
	/// <summary>黑板底边直线（原版 122 起定格的那条，原版屏幕坐标：红板左边缘上的一点 → 屏幕右边）；下面那条细白线落定后就是它的延长。</summary>
	private static readonly Vector2 LoA = new(1561f, 858f), LoB = new(1919f, 745f);
	/// <summary>上面那条：L（x = 834 − 0.625·(y − 500)，原版坐标）往右的偏移，u = 7…9（原版 119 在 L 右边约 34px、120 起贴在 L 上，118 还没有）；
	/// 从屏幕顶一直画到底（用户 2026-11-24：要接到左下那块白色区域上，它的左边就是 L）。宽约 7px（原版量的亮边 7–8px），下面那条细一些（4–5px）。</summary>
	private static readonly float[] HiOffset = { 34f, 1f, 0f };
	private static float Lx(float y) => 834f - 0.625f * (y - 500f);
	private const float LoW = 4.5f, HiW = 7f;
	/// <summary>原版定格的右侧分区（原版坐标）：黑板右上边 (1539,0)→(1920,304)、红板左边缘 (1920,435)→(1391,1080)，两条交成「&lt;」。</summary>
	private static readonly Vector2 TopA = new(1539f, 0f), TopB = new(1920f, 304f), RedA = new(1920f, 435f), RedB = new(1391f, 1080f);
	private float _exitT0 = -1f;
	private float _tNow;

	public PileExhaust(Control root, Vector2 view, int count)
	{
		_root = root;
		_view = view;
		_k = view.Y / 1080f;
		_rx = view.X - 1920f * _k;
		float k = _k;
		Vector2 R(float x, float y) => new(x * k + (x > 960f ? _rx : 0f), y * k);

		// 1. 红底：从右下角铺开
		_bg = PileDraw.StarMaterial(160f, new Color(0.466f, 0.034f, 0.06f), new Color(0.27f, 0.02f, 0.035f), new Color(0.27f, 0.02f, 0.035f), new Vector2(0.32f, 0.22f));
		_bg.SetShaderParameter("size", view);
		_bg.SetShaderParameter("wipe_origin", view);
		_bg.SetShaderParameter("wipe_dir", new Vector2(-1f, -0.62f).Normalized());
		_bg.SetShaderParameter("wipe", 0f);
		root.AddChild(new ColorRect { Name = "PileBg", Size = view, Material = _bg, MouseFilter = Control.MouseFilterEnum.Ignore });
		_lineLo = new AaPoly { Color = LineColor, Visible = false };
		root.AddChild(_lineLo);

		// 2. 最终构图：黑色大斜块 + 左下灰楔形（砸中时才出现）
		// 右侧分区照原版定格（用户 2026-11-24：之前右边只留了一个小红三角，细白线画不出「三分画面」）：黑板右边界 = 右上边 + 红板左边缘交成的「<」
		Vector2 top0 = AtY(Map(TopA), Map(TopB), 0f), redB = AtY(Map(RedA), Map(RedB), 1080f), vtx = Cross(Map(TopA), Map(TopB), Map(RedA), Map(RedB));
		// 黑板的下边 = 下面那条细白线往左的延长线（原版 122 起黑板底边是直的，白线就是它越过红板的延长；线下面是灰板，留着不反色）
		Vector2 lp = Map(LoA), lq = AtY(lp, Map(LoB), 1080f);
		lq.X = Mathf.Max(lq.X, 560f);
		_slabPts = new[] { R(560f, 0f), R(top0.X, 0f), R(vtx.X, vtx.Y), R(lp.X, lp.Y), R(lq.X, 1080f), R(560f, 1080f) };
		// 入场白板的外轮廓 = 黑块往左延伸到 x=300（左边由白板边 / 直线 L 裁）：原版反色区一直贴着 L 到屏幕底，立绘白腿也越过 x=560
		_platePts = new[] { R(300f, 0f), R(top0.X, 0f), R(vtx.X, vtx.Y), R(redB.X, 1080f), R(300f, 1080f) };
		_slab = new AaPoly { Polygon = _slabPts, Color = new Color(0.03f, 0.03f, 0.03f), Visible = false };
		root.AddChild(_slab);
		// 线下面那块灰板（原版入场白板剩下的部分，同色；切立绘以后接着留着）
		_wedge = new AaPoly { Polygon = new[] { R(lq.X, 1080f), R(lp.X, lp.Y), R(redB.X, 1080f) }, Color = Paper, Visible = false };
		_lowA = R(lp.X, lp.Y);
		_top0 = top0; _vtx = vtx; _redB = redB; _lp = lp; _lq = lq;
		Vector2 ld = (R(lq.X, 1080f) - _lowA).Normalized();
		_lowN = new Vector2(-ld.Y, ld.X);
		if (_lowN.Y < 0f) _lowN = -_lowN;                          // 法线朝下（线下面 = 正）
		root.AddChild(_wedge);

		// 3. 立绘 / 3D Joker 后面的黑白星纹斜带：裁在一个斜四边形里（clip_children）；入场时就在 Joker 身后（原版 112–117 头后面的星纹）
		// 星纹斜带的右边缘 = 直线 L（原版星纹条的右边缘就是黑板左边缘，上面那条细白线画在这条边上）
		_bandPts = new[] { R(600f, 0f), R(OurL(0f), 0f), R(OurL(520f), 520f), R(560f, 520f) };
		AaPoly bandMask = new() { Polygon = _bandPts, Color = Colors.White, ClipChildren = CanvasItem.ClipChildrenMode.Only, Visible = false };
		root.AddChild(bandMask);
		ShaderMaterial bandMat = PileDraw.StarMaterial(118f, new Color(0.91f, 0.91f, 0.91f), new Color(0.03f, 0.03f, 0.03f), new Color(0.376f, 0.376f, 0.376f), Vector2.Zero);
		bandMat.SetShaderParameter("size", view);
		bandMask.AddChild(new ColorRect { Size = view, Material = bandMat, MouseFilter = Control.MouseFilterEnum.Ignore });
		_band = bandMask;
		// 切入的前沿：一道黑边跟着划开的下沿走（同红底铺开的前沿）
		_bandEdge = new AaPoly { Color = new Color(0.02f, 0.02f, 0.02f), Visible = false };
		root.AddChild(_bandEdge);

		// 4. 入场白板（Joker 下面）→ 3D Joker → 白板遮罩（Joker 上面，挖掉黑色区域：白板里的 Joker 是白的，白底上看不见）
		_plate = new AaPoly { Color = Paper, Visible = false };
		root.AddChild(_plate);
		if (MenuJoker3D.Available)
		{
			_m3d = new MenuJoker3D(root, view, MapK, PorAt - OrigArtAt * MapK, Hide3d);
			root.AddChild(_m3d.Sprite);
			_reveal = new PileReveal(root, view);
			_m3d.Sprite.Material = _reveal.Track();
		}
		_coverMat = new ShaderMaterial { Shader = CoverShader };
		_cover = new AaPoly { Color = Paper, Visible = false, Material = _coverMat };
		root.AddChild(_cover);

		// 5. Joker（原版 CAMPSKILL_2D01），砸中时出现
		_portrait = new Node2D { Name = "PilePortrait", Position = (PorAt + new Vector2(PorW, PorW) * 0.5f) * k, Visible = false };
		root.AddChild(_portrait);
		_por = new ShaderMaterial { Shader = PileDraw.FillShader };
		_porSprite = new Sprite2D { Texture = Fx.Tex("pile_campskill"), Material = _por, TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps };
		if (_porSprite.Texture is Texture2D pt) _porScale = PorW * k / pt.GetWidth();
		_porSprite.Scale = Vector2.One * _porScale;
		_portrait.AddChild(_porSprite);
		// 线下面那块白板盖在立绘上面（原版定格：线下面整块是灰的，黑大衣不越过这条线；不然白板的边和细白线接不上）
		root.MoveChild(_wedge, root.GetChildCount() - 1);

		// 6. 黑色反色区：读屏幕，白板 ↔ 黑、黑 Joker ↔ 白（P 系列的撞色）；形状 = 白板多边形，着色器只保留圆内
		root.AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });
		_invertMat = new ShaderMaterial { Shader = InvertShader };
		_invertMat.SetShaderParameter("sum", Paper.R + 0.03f);   // 白板反过来 = 黑块的颜色（0.03），两块黑接在一起看不出边
		_invertMat.SetShaderParameter("paper", Paper.R);
		_invert = new AaPoly { Color = Colors.White, Visible = false, Material = _invertMat };
		root.AddChild(_invert);

		// 7. 标题牌 MeNu&EXHAUST + COMMAND SELECT（原版字图 + P5 字图）
		Node2D plate = new() { Position = R(1690f, 142f), Rotation = Mathf.DegToRad(-4f) };   // 在游戏顶栏（约 80px）下面
		root.AddChild(plate);
		Node2D plateIn = new() { Scale = Vector2.One * 0.86f };
		plate.AddChild(plateIn);
		Vector2 h = new Vector2(215f, 65f) * k;
		PileDraw.Poly(plateIn, new[] { -h, new Vector2(h.X, -h.Y), h, new Vector2(-h.X, h.Y) }, Paper);
		Vector2 hi = h - new Vector2(6f, 6f) * k;
		PileDraw.Poly(plateIn, new[] { -hi, new Vector2(hi.X, -hi.Y), hi, new Vector2(-hi.X, hi.Y) }, Ink);
		Glyph(plateIn, "pile_txt_menu", new Vector2(-128f, -15f), 120f, Paper);
		PileText.Exact(plateIn, "menu_exhaust", new Vector2(-58f, -46f) * k, 52f * k, Paper);
		Glyph(plateIn, "pile_txt_cmdselect", new Vector2(5f, 40f), 212f, Paper);
		_pops.Add((plate, plate.Position, plate.Rotation, SlamAt + 0.02f));

		// 8. COMMAND + 说明
		Node2D command = new() { Position = new Vector2(760f, 915f) * k, Rotation = Mathf.DegToRad(-4f) };
		root.AddChild(command);
		Glyph(command, "pile_txt_command_ol", Vector2.Zero, 551f, Ink);
		Glyph(command, "pile_txt_command", Vector2.Zero, 551f, Paper);
		PileDraw.Caption(command, "exhaust", count, new Vector2(-150f, 100f) * k, 38f * k);
		_pops.Add((command, command.Position, command.Rotation, SlamAt + 0.06f));

		_lineHi = new AaPoly { Color = LineColor, Visible = false };
		root.AddChild(_lineHi);

		Enter(0f);
	}

	public void Kick() { }

	/// <summary>
	/// 星纹斜带切入（用户 2026-11-24：之前直接放进来太僵硬）：0.05 → 0.25s 从顶部沿斜带往下划开，同时从中线向两边张开（三次缓出，前快后慢），
	/// 下沿跟着一道黑边；到位后黑边收掉。原版这条带是上一页的星纹拼贴收成的一窄条，这里用同一个方向的划入。
	/// </summary>
	private void Band(float t)
	{
		const float t0 = 0.05f, len = 0.2f;
		float p = Mathf.Clamp((t - t0) / len, 0f, 1f);
		var mask = (AaPoly)_band;
		mask.Visible = t >= t0;
		_bandEdge.Visible = t >= t0 && p < 1f;
		if (p >= 1f) { mask.Polygon = _bandPts; return; }
		float d = 1f - Mathf.Pow(1f - p, 3f), w = 1f - Mathf.Pow(1f - Mathf.Clamp(p * 1.3f, 0f, 1f), 3f);
		Vector2 a = _bandPts[0], b = _bandPts[1], c = _bandPts[2], e = _bandPts[3];
		Vector2 midTop = (a + b) * 0.5f, midBot = (e + c) * 0.5f;
		Vector2 Spread(Vector2 q, Vector2 mid) => mid + (q - mid) * Mathf.Lerp(0.15f, 1f, w);
		// 下沿在左右两条边上按 d 往下走
		Vector2 l = a.Lerp(e, d), r = b.Lerp(c, d), midL = midTop.Lerp(midBot, d);
		Vector2 tl = Spread(a, midTop), tr = Spread(b, midTop), bl = Spread(l, midL), br = Spread(r, midL);
		mask.Polygon = new[] { tl, tr, br, bl };
		float th = 14f * _k * (1f - p);
		Vector2 n = new Vector2(0f, th);
		_bandEdge.Polygon = new[] { bl, br, br + n, bl + n };
	}

	private static Shader? _inv, _cov;
	/// <summary>
	/// 黑色反色区：圆内读屏幕、两种色调互换（sum = 纸色 + 墨色：白板 → 墨黑，黑 Joker → 纸白），圆外丢弃；圆边 2px 抗锯齿。
	/// 多边形边上的羽化条（AaPoly，顶点 alpha = 白板在这个像素的覆盖率 a）不能整像素反色：半白半黑的边反过来还是灰，会留一根灰线
	///（用户 2026-11-24：黑区里那根线）→ 只把白板那一份换成反色：s − a·纸 + a·(sum − 纸)。
	/// </summary>
	internal static Shader InvertShader => _inv ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
uniform vec2 c = vec2(0.0);
uniform float r = 0.0;
uniform float sum = 0.98;
uniform float paper = 0.94;
uniform vec2 la = vec2(0.0);
uniform vec2 ln = vec2(0.0);
varying vec2 lp;
void vertex() { lp = VERTEX; }
void fragment() {
	float ca = 1.0 - smoothstep(r - 1.0, r + 1.0, distance(lp, c));
	ca *= 1.0 - smoothstep(-1.0, 1.0, dot(lp - la, ln));   // 线下面不反色（ln = 0 时不起作用）
	if (ca <= 0.0) discard;
	vec3 s = texture(screen_tex, SCREEN_UV).rgb;
	float a = COLOR.a;
	vec3 inv = a >= 0.999 ? vec3(sum) - s : s + a * (sum - 2.0 * paper);
	COLOR = vec4(clamp(inv, 0.0, 1.0), ca);
}
" };
	/// <summary>白板遮罩：盖在 3D Joker 上面的白板，圆内（黑色反色区）挖空，让下面的 Joker 被反色成白腿。</summary>
	private static Shader CoverShader => _cov ??= new Shader { Code = @"
shader_type canvas_item;
uniform vec2 c = vec2(0.0);
uniform float r = 0.0;
uniform vec2 la = vec2(0.0);
uniform vec2 ln = vec2(0.0);
varying vec2 lp;
void vertex() { lp = VERTEX; }
void fragment() {
	float a = smoothstep(r - 1.0, r + 1.0, distance(lp, c));
	a = max(a, smoothstep(-1.0, 1.0, dot(lp - la, ln)));     // 线下面白板不挖空
	if (a <= 0.0) discard;
	COLOR.a *= a;
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

	/// <summary>入场进度 = 原版第几帧（112 + u）：从 0 开始就在动，HandAt 正好到第 10 帧（3D、白板边、反色圆共用）。</summary>
	private static float U(float t) => PileReveal.Progress(t, HandAt, 10f);

	/// <summary>白板左边缘在 t 时刻的两点（原版屏幕坐标）：先从右边扫进来，112–117 按原版逐帧插值，117→118 跳到直线 L。</summary>
	private static (Vector2 A, Vector2 B) Edge(float t)
	{
		// 关键帧之间 Catmull-Rom（用户 2026-11-24：不够丝滑——之前每段 InOutCubic，每到一个原版帧都停一下）
		float u = U(t);
		int i = Mathf.Min((int)u, EdgeKeys.Length - 1);
		if (i >= EdgeKeys.Length - 1) return EdgeKeys[^1];
		float f = u - i;
		int i0 = Mathf.Max(i - 1, 0), i3 = Mathf.Min(i + 2, EdgeKeys.Length - 1);
		return (Cr(EdgeKeys[i0].A, EdgeKeys[i].A, EdgeKeys[i + 1].A, EdgeKeys[i3].A, f), Cr(EdgeKeys[i0].B, EdgeKeys[i].B, EdgeKeys[i + 1].B, EdgeKeys[i3].B, f));
	}

	private static Vector2 Cr(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t) =>
		0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (3f * p1 - p0 - 3f * p2 + p3) * t * t * t);

	/// <summary>
	/// 黑色反色区半径（原版屏幕坐标）：117 之前 0，之后过原版逐帧的值。单调三次插值（Fritsch–Carlson）：
	/// 之前逐段直线，速度在每个原版帧突变（117→118 一大跳、119–120 很慢、121 又快），看着一顿一顿的；现在速度连续、也不会往回缩。
	/// </summary>
	private static float Radius(float t)
	{
		float u = U(t) - 5f;
		if (u <= 0f) return 0f;
		int n = RRadius.Length;
		if (u >= n - 1) return RRadius[^1];
		int i = (int)u;
		float s = u - i;
		float M(int j)
		{
			if (j <= 0) return RRadius[1] - RRadius[0];
			if (j >= n - 1) return RRadius[n - 1] - RRadius[n - 2];
			float d0 = RRadius[j] - RRadius[j - 1], d1 = RRadius[j + 1] - RRadius[j];
			return d0 * d1 <= 0f ? 0f : 2f / (1f / d0 + 1f / d1);   // 调和平均：单调、不过冲
		}
		float h00 = 2 * s * s * s - 3 * s * s + 1, h10 = s * s * s - 2 * s * s + s, h01 = -2 * s * s * s + 3 * s * s, h11 = s * s * s - s * s;
		return h00 * RRadius[i] + h10 * M(i) + h01 * RRadius[i + 1] + h11 * M(i + 1);
	}

	/// <summary>凸多边形按半平面裁剪（Sutherland–Hodgman）：保留直线 a→b 右侧（屏幕 x 更大的一侧）。</summary>
	private static Vector2[] ClipRight(Vector2[] poly, Vector2 a, Vector2 b)
	{
		Vector2 d = b - a;
		Vector2 n = new(d.Y, -d.X);
		if (n.X < 0f) n = -n;
		float Side(Vector2 p) => (p - a).Dot(n);
		List<Vector2> o = new();
		for (int i = 0; i < poly.Length; i++)
		{
			Vector2 p = poly[i], q = poly[(i + 1) % poly.Length];
			float sp = Side(p), sq = Side(q);
			if (sp >= 0f) o.Add(p);
			if ((sp >= 0f) != (sq >= 0f)) o.Add(p + (q - p) * (sp / (sp - sq)));
		}
		return o.ToArray();
	}

	private void Enter(float t)
	{
		float k = _k;
		float reach = (_view + new Vector2(0f, _view.Y)).Length() * 1.05f;
		_bg.SetShaderParameter("wipe", t >= 0.3f ? 1e7f : Ease5.OutQuint(Mathf.Clamp(t / 0.3f, 0f, 1f)) * reach);
		bool cut = t >= HandAt;
		_tNow = t;
		_reveal?.Set(t);
		// 黑块从白板到位起就垫在 Joker 下面（原版 Joker 身后是黑底：黑色的身体 / 腿和背景连成一片），不然黑色区域和身体之间会露出一条红缝
		_slab.Visible = true;
		_slab.Modulate = Colors.White;
		Vector2[] slab = _reveal != null && !cut ? _reveal.Clip(_slabPts) : _slabPts;
		_slab.Visible = slab.Length >= 3;
		if (_slab.Visible) _slab.Polygon = slab;
		// 灰楔形是我们排版自己加的，原版切立绘那一刻是黑的：砸中之后再淡进来，切换那一下不冒出一块灰
		_wedge.Visible = cut;
		_wedge.Modulate = Colors.White;
		Band(t);
		_portrait.Visible = cut;
		if (!cut)
		{
			// 白板（Joker 下面 + 上面的遮罩）和黑色反色区共用同一个多边形：最终黑块的形状裁掉白板左边缘左侧
			var (ea, eb) = Edge(t);
			Vector2[] poly = ClipRight(_platePts, Map(ea) * k, Map(eb) * k);
			if (_reveal != null) poly = _reveal.Clip(poly);   // 白板也跟着揭幕的斜线露出来（原来是从右边整块滑进来）
			bool show = poly.Length >= 3;
			_plate.Visible = _cover.Visible = show;
			if (show) _plate.Polygon = _cover.Polygon = _invert.Polygon = poly;
			float r = Radius(t);
			_invert.Visible = show && r > 0f;
			Vector2 c = Map(RCenter) * k;
			float rr = r * MapK * k;
			_coverMat.SetShaderParameter("la", _lowA);
			_coverMat.SetShaderParameter("ln", _lowN);
			_invertMat.SetShaderParameter("la", _lowA);
			_invertMat.SetShaderParameter("ln", _lowN);
			_coverMat.SetShaderParameter("c", c);
			_coverMat.SetShaderParameter("r", rr);
			_invertMat.SetShaderParameter("c", c);
			_invertMat.SetShaderParameter("r", rr);
			if (_m3d != null)
			{
				// 原版 112–122 的 3D Joker：从第 0 帧就开始连续播（u = 原版第几帧，小数在两帧之间插值）、HandAt 正好放到最后一帧；
				// 斜切揭幕（PileReveal）露出来，不再定格平移（用户 2026-11-24）
				_m3d.Show(MenuJoker3DKeys.Exhaust, U(t));
				_m3d.Sprite.Position = Vector2.Zero;
			}
		}
		else
		{
			_plate.Visible = _cover.Visible = _invert.Visible = false;
			_m3d?.Hide();
			// 换成立绘，从 1.02 倍砸下（HandAt → SlamAt，加速），压扁 0.965 再回弹（同抽牌堆的手掌）
			float scale, drop;
			if (t < SlamAt)
			{
				float e = Mathf.Pow(Mathf.Clamp((t - HandAt) / (SlamAt - HandAt), 0f, 1f), 3f);
				scale = Mathf.Lerp(1.02f, 0.965f, e);
				drop = Mathf.Lerp(-10f, 8f, e);
			}
			else
			{
				float b = Ease5.OutBack(Mathf.Clamp((t - SlamAt) / 0.14f, 0f, 1f));
				scale = Mathf.Lerp(0.965f, 1f, b);
				drop = Mathf.Lerp(8f, 0f, b);
			}
			Vector2 home = (PorAt + new Vector2(PorW, PorW) * 0.5f) * k;
			_portrait.Position = home + new Vector2(0f, drop) * k;
			_porSprite.Scale = Vector2.One * _porScale * scale;
			_por.SetShaderParameter("fill_amt", 0f);
			_portrait.Modulate = Colors.White;
			Shake(t);
		}
		Lines(t);
		foreach (var (node, at, rot, delay) in _pops)
		{
			float p = Mathf.Clamp((t - delay) / 0.2f, 0f, 1f);
			float b = Ease5.OutBack(p);
			node.Position = at + new Vector2(60f * k * (1f - Ease5.OutExpo(p)), 0f);
			node.Rotation = rot + Mathf.DegToRad(8f) * (1f - b);
			node.Scale = Vector2.One * Mathf.Lerp(0.7f, 1f, b);
			node.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(p * 3f, 0f, 1f));
		}
	}

	private static readonly Color LineColor = new(0.86f, 0.86f, 0.85f);

	/// <summary>直线 a→b 上 y 处的点。</summary>
	private static Vector2 AtY(Vector2 a, Vector2 b, float y) => new(a.X + (b.X - a.X) * (y - a.Y) / (b.Y - a.Y), y);
	/// <summary>两条直线的交点。</summary>
	private static Vector2 Cross(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
	{
		Vector2 d1 = a2 - a1, d2 = b2 - b1;
		float s = ((b1 - a1).X * d2.Y - (b1 - a1).Y * d2.X) / (d1.X * d2.Y - d1.Y * d2.X);
		return a1 + d1 * s;
	}
	/// <summary>直线 L 换到我们排版后，在我们的 y 处的 x。</summary>
	private static float OurL(float y)
	{
		float yo = (y - PorAt.Y) / MapK + OrigArtAt.Y;
		return Map(new Vector2(Lx(yo), yo)).X;
	}

	/// <summary>一条细线：我们排版的 1080p 坐标 a → b，LineW 宽。</summary>
	private void SetLine(AaPoly line, Vector2 a, Vector2 b, float width)
	{
		Vector2 R(Vector2 q) => new(q.X * _k + (q.X > 960f ? _rx : 0f), q.Y * _k);
		Vector2 pa = R(a), pb = R(b);
		Vector2 n = (pb - pa).Normalized().Orthogonal() * (width * 0.5f * _k);
		line.Polygon = new[] { pa - n, pb - n, pb + n, pa + n };
	}

	/// <summary>
	/// 两条细白线（用户 2026-11-24：线是白色那块的延长线，要接上）。
	/// 下面那条 = 白板（灰）和黑色反色区的分界在右边出界的那一点，顺着分界的方向伸进红底：
	/// 扫开的时候分界是反色圆的弧（线 = 弧和右侧分区折线交点处的切线），弧越过黑板底边直线以后分界就是那条直线（线 = 它的延长，原版定格那条）。
	/// 上面那条 = 直线 L 上的白边（星纹条右边缘），原版 119 起。
	/// </summary>
	private void Lines(float t)
	{
		float u = U(t) - 6f;
		_lineLo.Modulate = _lineHi.Modulate = Colors.White;
		_lineLo.Visible = LoLine(out Vector2 e, out Vector2 dir);
		if (_lineLo.Visible) SetLine(_lineLo, e - dir * 6f, e + dir * 3000f, LoW);
		_lineHi.Visible = u >= 1f;
		if (u < 1f) return;
		float uh = u - 1f;
		int j = Mathf.Min((int)uh, HiOffset.Length - 2);
		float off = uh >= HiOffset.Length - 1 ? HiOffset[^1] : Mathf.Lerp(HiOffset[j], HiOffset[j + 1], Mathf.Clamp(uh - j, 0f, 1f));
		// 线的中心在亮边中间（L 往右半个线宽）
		// 一直画到屏幕底外：下半截压在白板 / 白腿上（白上白看不出），上半截就是白色区域左边那条斜边往上的延长，中间不断开
		Vector2 h0 = Map(new Vector2(Lx(-20f) + off + HiW * 0.5f, -20f)), h1 = Map(new Vector2(Lx(1120f) + off + HiW * 0.5f, 1120f));
		SetLine(_lineHi, h0, h1, HiW);
	}

	/// <summary>
	/// 下面那条线的起点和朝外的方向；还没有黑区时返回 false。
	/// A：弧扫过顶边时，起点 = 弧和顶边的交点，方向 = 弧的切线（线是白板弧边的延长）；
	/// B：弧快到拐点时（离拐点 60px 起，半径再长 LoBlend），起点沿红板边滑到黑板底边那一点、方向转到底边直线的方向（原版 120→121 是两帧之间直接跳，
	///    用户要丝滑，这里摊开三四帧）；C：之后就是黑板底边直线的延长（原版定格）。
	/// </summary>
	private bool LoLine(out Vector2 e, out Vector2 dir)
	{
		e = dir = Vector2.Zero;
		float r = Radius(_tNow) * MapK;
		if (r <= 0f) return false;
		Vector2 c = Map(RCenter);
		Vector2 fin = (_lp - _lq).Normalized();
		float ra = (_vtx - c).Length() - 60f, rb = ra + LoBlend * MapK;
		if (r >= rb) { e = _lp; dir = fin; return true; }
		float rr = Mathf.Min(r, ra);
		if (!ArcOnTop(c, rr, out Vector2 ea)) return false;
		Vector2 rad = (ea - c).Normalized();
		Vector2 tan = new(-rad.Y, rad.X);
		if (tan.X < 0f) tan = -tan;                                   // 朝右（伸进红底）
		if (r <= ra) { e = ea; dir = tan; return true; }
		float w = (r - ra) / (rb - ra);
		w = w * w * (3f - 2f * w);
		// 起点先到拐点再顺着红板边下到底边那一点
		Vector2 mid = _vtx;
		e = w < 0.35f ? ea.Lerp(mid, w / 0.35f) : mid.Lerp(_lp, (w - 0.35f) / 0.65f);
		dir = Vector2.FromAngle(Mathf.LerpAngle(tan.Angle(), fin.Angle(), w));
		return true;
	}

	private const float LoBlend = 360f;

	/// <summary>半径 r 的圆和顶边（_top0 → _vtx）的交点。</summary>
	private bool ArcOnTop(Vector2 c, float r, out Vector2 q)
	{
		q = Vector2.Zero;
		Vector2 a = _top0, d = _vtx - _top0, f = a - c;
		float A = d.Dot(d), B = 2f * f.Dot(d), C = f.Dot(f) - r * r, disc = B * B - 4f * A * C;
		if (disc < 0f) return false;
		float sq = Mathf.Sqrt(disc);
		bool ok = false;
		foreach (float s in new[] { (-B - sq) / (2f * A), (-B + sq) / (2f * A) })
		{
			if (s < 0f || s > 1f) continue;
			Vector2 p = a + d * s;
			if (!ok || p.Y > q.Y) { q = p; ok = true; }
		}
		return ok;
	}

	private void Shake(float t)
	{
		float a = t < SlamAt ? 0f : Mathf.Max(0f, 1f - (t - SlamAt) / 0.24f);
		a *= a;
		float u = (t - SlamAt) * 34f;
		_root.Position = a <= 0f ? Vector2.Zero : new Vector2(Mathf.Sin(u * 1.3f) * 6f, Mathf.Cos(u) * 14f) * _k * a;
	}

	private void Exit(float e)
	{
		float k = _k;
		_root.Position = Vector2.Zero;
		_m3d?.Hide();
		_plate.Visible = _cover.Visible = _invert.Visible = false;
		_portrait.Visible = true;
		float ei = Ease5Ext.InCubic(e);
		float reach = (_view + new Vector2(0f, _view.Y)).Length() * 1.05f;
		_bg.SetShaderParameter("wipe", reach * (1f - Ease5Ext.InCubic(Mathf.Clamp((e - 0.15f) / 0.85f, 0f, 1f))));
		// 黑块和星纹带往右退；立绘先变黑剪影再往右上滑走
		float pe = Ease5Ext.InOutCubic(Mathf.Clamp(e / 0.75f, 0f, 1f));
		Vector2 off = new(1500f * k * pe, 0f);
		_slab.Position = _wedge.Position = _band.Position = off;
		_bandEdge.Visible = false;
		// 黑块 / 灰楔形 / 星纹带边退边淡掉（不然灰楔形滑到牌后面露出一块白三角）
		Color fade = new(1f, 1f, 1f, 1f - Mathf.Clamp(e / 0.45f, 0f, 1f));
		_wedge.Modulate = fade;
		_band.Modulate = fade;
		_slab.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp((e - 0.25f) / 0.5f, 0f, 1f));
		_lineLo.Position = _lineHi.Position = off;
		_lineLo.Modulate = _lineHi.Modulate = fade;
		_por.SetShaderParameter("fill_color", Ink);
		_por.SetShaderParameter("fill_amt", Fx.OutCubic(Mathf.Clamp(e / 0.3f, 0f, 1f)));
		Vector2 home = (PorAt + new Vector2(PorW, PorW) * 0.5f) * k;
		_portrait.Position = home + new Vector2(420f, -120f) * k * Ease5Ext.InCubic(Mathf.Clamp((e - 0.15f) / 0.75f, 0f, 1f));
		_portrait.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp((e - 0.7f) / 0.3f, 0f, 1f));
		foreach (var (node, at, _, _) in _pops)
		{
			node.Position = at + new Vector2(200f * k * ei, 0f);
			node.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp(e * 1.8f, 0f, 1f));
		}
	}

	private void Glyph(Node2D parent, string tex, Vector2 at, float width, Color tint)
	{
		if (Fx.Tex(tex) is not Texture2D t) return;
		float w = tex.EndsWith("_ol") && Fx.Tex(tex[..^3]) is Texture2D body ? width * t.GetWidth() / body.GetWidth() : width;
		parent.AddChild(new Sprite2D { Texture = t, Position = at * _k, Scale = Vector2.One * w * _k / t.GetWidth(), Modulate = tint, TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps });
	}

	public static readonly string[] Textures = { "pile_campskill", "pile_txt_menu", "pile_txt_cmdselect", "pile_txt_command", "pile_txt_command_ol" };
}
