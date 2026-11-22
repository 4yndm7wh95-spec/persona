using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 抽牌堆界面 = P5R 暂停菜单主页（docs/p5/PILE_PLAN.md）。这里只放装饰和动画（只用 Godot，预览工具也能编译）；
/// 牌格子、悬停、特写、排序在 PileScreen.cs（照搬 P3 mod 的 PileFx）。
///
/// 层次（从下到上）：
///   1. 左侧不透明红底：同一份原版星纹数据，暗红压在红上（看不到局内）；
///   2. 右侧黑白星纹面板（原版 INIT/CAMPBGSTARANIME.DAT 的 49 颗同心五角星，shader 实时画 = 矢量，任何分辨率都锐利），左边是斜边；
///   3. 原版标题牌（Don't Look at ME / LIKE THAT / MeNu&Main COMMAND SELECT）、牌数 = 原版页码大数字；
///   4. 原版 Joker + 白手 + 红手套（CAMPTOP_2D）；
///   5. COMMAND 大字 + 说明；
///   6. 白手掌心上方悬浮旋转的真 3D 预告信（SubViewport：透视、厚度边、淡反光；正面礼帽，背面每次随机一封原版信）。
///
/// 入场（约 0.6s，慢快慢）：红底从抽牌堆按钮那个角斜着铺开（前沿一道黑边），Joker 先以白剪影、再黑剪影顺斜向滑进来（原版是 2D 剪影硬切，
/// 这里用交叉渐变 + 位移代替），星纹面板从右边扫进来，白手带一点回弹落定，标题牌和 COMMAND 依次弹入，最后预告信起转。
/// 退场（0.32s）大致倒放。坐标全按 1920×1080 写，运行时乘 k = 视口高 / 1080；右侧元素跟着宽屏右移 rx。
/// </summary>
public sealed class PileDraw : IPileDecor
{
	// ── 版式（1920×1080）──
	public const float GridX = 70f, GridTop = 168f, GridBottom = 1046f;
	public const int Cols = 4;
	public const float CardW = 168f, CellW = 182f, CellH = 250f;
	// 立绘按原版比例（实机截图量的：比第一版大 1.258 倍，手指尖顶到画面上缘）；牌格子改 4 列，手掌左边缘正好在牌格子右边
	private static readonly Vector2 PortraitAt = new(580f, -6f);
	private const float PortraitW = 1510f;
	private const float CardScale = 1.258f;
	private static readonly Vector2 PalmAt = new(1063f, 537f);           // 预告信中心（用户 2026-11-22 定的白手掌心偏左一点，随立绘等比换算）
	private const float PanelEdgeTop = 1150f, PanelEdgeBottom = 1000f;  // 星纹面板左边的斜边
	private static readonly Vector2 WipeFrom = new(0f, 1080f);          // 抽牌堆按钮在左下角

	public const float EnterTime = 0.62f, ExitTime = 0.32f;
	public const float CardsStart = 0.2f;                               // 牌从这时开始依次飞进来

	private readonly Control _root;
	private readonly Vector2 _view;
	private readonly float _k, _rx;
	private readonly ShaderMaterial _bg, _panel, _por;
	private readonly Node2D _portrait, _plates, _command;
	private readonly Sprite2D _porSprite;
	private readonly List<(Node2D Node, Vector2 At, float Rot, float Delay)> _pops = new();
	private readonly CallingCard? _card;
	private float _exitT0 = -1f;
	private float _porA = 1f, _porScale = 1f;
	// 原版菜单 3D Joker 的入场动作（research/p5r/menu3d/main_0000，12 帧，Codex 从 MENU_C0001 GMD/GAP 渲的）
	private readonly Texture2D?[] _m3dFrames = new Texture2D?[12];
	private readonly Sprite2D? _m3d;
	private readonly ShaderMaterial? _split;
	private readonly bool _has3d;
	private static readonly Vector2 M3dAt = new(1117f, 117f), M3dSize = new(921f, 1007f);   // 高度含往下延伸的 240px（×0.843）   // 1080p 下的左上角和大小（最后一帧的头对齐 2D 定格图的头）

	public PileDraw(Control root, Vector2 view, int count)
	{
		_root = root;
		_view = view;
		_k = view.Y / 1080f;
		_rx = view.X - 1920f * _k;
		float k = _k;

		// 1. 红底（不透明，铺满）
		_bg = StarMaterial(160f, new Color(0.466f, 0.034f, 0.06f), new Color(0.27f, 0.02f, 0.035f), new Color(0.27f, 0.02f, 0.035f), new Vector2(0.32f, 0.22f));
		_bg.SetShaderParameter("wipe_origin", WipeFrom * k);
		_bg.SetShaderParameter("wipe_dir", new Vector2(1f, -0.62f).Normalized());
		_bg.SetShaderParameter("wipe", 0f);
		_bg.SetShaderParameter("size", view);
		root.AddChild(new ColorRect { Name = "PileBg", Size = view, Material = _bg, MouseFilter = Control.MouseFilterEnum.Ignore });

		// 2. 星纹面板
		_panel = StarMaterial(118f, new Color(0.91f, 0.91f, 0.91f), new Color(0.03f, 0.03f, 0.03f), new Color(0.376f, 0.376f, 0.376f), Vector2.Zero);
		_panel.SetShaderParameter("edge_top", PanelEdgeTop * k + _rx);
		_panel.SetShaderParameter("edge_bottom", PanelEdgeBottom * k + _rx);
		_panel.SetShaderParameter("edge_shift", view.X);
		_panel.SetShaderParameter("size", view);
		root.AddChild(new ColorRect { Name = "PilePanel", Size = view, Material = _panel, MouseFilter = Control.MouseFilterEnum.Ignore });

		// 3. 标题牌 + 牌数
		_plates = new Node2D { Name = "PilePlates" };
		root.AddChild(_plates);
		Plate(new Vector2(1520f, 72f), new Vector2(470f, 92f), -6f, Ink, Paper, "pile_txt_dontlook", 413f, Paper, 0.30f);
		Plate(new Vector2(1700f, 150f), new Vector2(330f, 96f), -2f, Paper, Ink, "pile_txt_likethat", 292f, Ink, 0.34f);
		Node2D menu = Plate(new Vector2(1760f, 300f), new Vector2(300f, 130f), 5f, Ink, Paper, null, 0f, Paper, 0.38f);
		Glyph(menu, "pile_txt_menu", new Vector2(-60f, -12f), 131f, 0f, Paper);
		Glyph(menu, "pile_txt_main", new Vector2(70f, -20f), 106f, 0f, Paper);
		Glyph(menu, "pile_txt_cmdselect", new Vector2(5f, 40f), 212f, 0f, Paper);

		// 4. Joker + 白手 + 红手套（剪影 → 原画用 fill 着色器）
		_portrait = new Node2D { Name = "PilePortrait", Position = (PortraitAt + new Vector2(PortraitW, PortraitW) * 0.5f) * k + new Vector2(_rx, 0f) };
		root.AddChild(_portrait);
		_por = new ShaderMaterial { Shader = FillShader };
		_porSprite = new Sprite2D { Texture = Fx.Tex("pile_camptop"), Material = _por, TextureFilter = CanvasItem.TextureFilterEnum.Linear };
		_porScale = _porSprite.Texture is { } pt ? PortraitW * k / pt.GetWidth() : 1f;
		_porSprite.Scale = Vector2.One * _porScale;
		_portrait.AddChild(_porSprite);

		// 4b. 原版菜单 3D Joker：播原版入场动作，一条斜线从右往左扫过，人物由白剪影变成黑（原版就是同一段动作 + 移动的分界线）
		for (int i = 0; i < 12; i++) _m3dFrames[i] = Fx.Tex($"pile_m3d_{i:00}");
		_has3d = _m3dFrames[0] != null && _m3dFrames[11] != null;
		if (_has3d)
		{
			_split = new ShaderMaterial { Shader = SplitShader };
			_m3d = new Sprite2D { Name = "PileMenu3d", Texture = _m3dFrames[0], Material = _split, TextureFilter = CanvasItem.TextureFilterEnum.Linear, Visible = false };
			_m3d.Scale = Vector2.One * M3dSize.X * k / _m3dFrames[0]!.GetWidth();
			root.AddChild(_m3d);
		}

		// 牌数（原版页码大数字，白字黑描边，叠在 Joker 右边）
		Node2D digits = new() { Position = new Vector2(1790f * k + _rx, 690f * k) };
		root.AddChild(digits);
		string n = Math.Clamp(count, 0, 999).ToString();
		float dw = n.Length >= 3 ? 104f : 140f;
		for (int i = 0; i < n.Length; i++)
		{
			float x = (i - (n.Length - 1) * 0.5f) * dw;
			float rot = i % 2 == 0 ? 4f : -3f;
			Glyph(digits, $"pile_digit_{n[i]}_ol", new Vector2(x, i % 2 * 12f), n.Length >= 3 ? 104f : 123f, rot, Ink);
			Glyph(digits, $"pile_digit_{n[i]}", new Vector2(x, i % 2 * 12f), n.Length >= 3 ? 104f : 123f, rot, Paper);
		}
		_pops.Add((digits, digits.Position, 0f, 0.42f));

		// 5. COMMAND + 说明
		_command = new Node2D { Position = new Vector2(1430f * k + _rx, 955f * k), Rotation = Mathf.DegToRad(-4f) };
		root.AddChild(_command);
		Glyph(_command, "pile_txt_command_ol", Vector2.Zero, 643f, 0f, Ink);
		Glyph(_command, "pile_txt_command", Vector2.Zero, 643f, 0f, Paper);
		Caption(_command, "draw", count, new Vector2(150f, 62f) * k, 36f * k);
		_pops.Add((_command, _command.Position, _command.Rotation, 0.46f));

		// 6. 预告信
		_card = CallingCard.Create(root, PalmAt * k + new Vector2(_rx, 0f), k, CardScale);

		Enter(0f);
	}

	internal static readonly Color Ink = new(0.04f, 0.04f, 0.04f), Paper = new(0.94f, 0.94f, 0.94f);

	/// <summary>手指碰到一张牌：预告信额外踢转 120°（P3 同款）。</summary>
	public void Kick() => _card?.Kick();

	float IPileDecor.GridX => GridX;
	float IPileDecor.GridTop => GridTop;
	float IPileDecor.GridBottom => GridBottom;
	int IPileDecor.Cols => Cols;
	float IPileDecor.CardW => CardW;
	float IPileDecor.CellW => CellW;
	float IPileDecor.CellH => CellH;
	float IPileDecor.CardsStart => CardsStart;
	float IPileDecor.ExitTime => ExitTime;
	public Vector2 FocusAt => new(640f, 560f);
	public Vector2 InfoAt => new(1000f, 300f);
	public Vector2 SwitchAt => new(70f, 44f);
	public Vector2 EnterOffset => new(-40f, 46f);
	public Vector2 ExitOffset => new(-20f, 130f);

	/// <summary>说明行：「抽牌堆 · 23 张」/「DRAW · 23」，全部是 P5 字图（PileText），白字黑描边。</summary>
	internal static void Caption(Node2D parent, string pile, int count, Vector2 at, float h)
	{
		bool zh = TranslationServer.GetLocale().StartsWith("zh");
		if (zh) PileText.Row(parent, at, h, Paper, true, 8f * h / 36f, pile + "_zh", "#" + count, "cards_zh");
		else PileText.Row(parent, at, h, Paper, true, 10f * h / 36f, pile, "#" + count);
	}

	/// <summary>牌从哪里飞进来（屏幕像素）：抽牌堆按钮那个角。</summary>
	public Vector2 CardSource => new(60f * _k, _view.Y - 40f * _k);

	public void BeginExit(float now)
	{
		if (_exitT0 >= 0f) return;
		_exitT0 = now;
		_porA = _portrait.Modulate.A;
		Sfx.Play("camp_cancel", 0.55f);
	}

	/// <summary>每帧：now = 打开以来的秒数（真实时间）。</summary>
	public void Update(float now, float dt)
	{
		float time = now;
		_bg.SetShaderParameter("time", time);
		_panel.SetShaderParameter("time", time);
		if (_exitT0 < 0f) Enter(now);
		else Exit(Mathf.Clamp((now - _exitT0) / ExitTime, 0f, 1f));
		_card?.Update(now, dt, _exitT0 < 0f);
	}

	private void Enter(float t)
	{
		float k = _k;
		// 红底斜着铺开：0 → 0.3s
		float wipe = Ease5.OutQuint(Mathf.Clamp(t / 0.3f, 0f, 1f));
		float reach = (_view + new Vector2(0f, _view.Y)).Length() * 1.05f;
		_bg.SetShaderParameter("wipe", t >= 0.3f ? 1e7f : wipe * reach);
		// 星纹面板从右边扫进来：0.06 → 0.42s
		float pe = Ease5.OutExpo(Mathf.Clamp((t - 0.06f) / 0.36f, 0f, 1f));
		_panel.SetShaderParameter("edge_shift", (1f - pe) * (_view.X - PanelEdgeBottom * k - _rx + 40f * k));
		Vector2 home = (PortraitAt + new Vector2(PortraitW, PortraitW) * 0.5f) * k + new Vector2(_rx, 0f);
		if (_has3d && _m3d != null && _split != null)
		{
			// 原版入场：3D Joker 带着白剪影斜着滑进来（0.03 → 0.23），播原版转身抬头（0.06 → HandAt），
			// 同时分界线从右往左扫过（0.08 → 0.34）由白变黑；0.36 起交叉切到 2D 定格（白手 + 红手套），手带一点回弹
			float slide = Ease5.OutExpo(Mathf.Clamp((t - 0.05f) / 0.2f, 0f, 1f));
			Vector2 at = (M3dAt + M3dSize * 0.5f) * k + new Vector2(_rx, 0f);
			_m3d.Position = at + new Vector2(-220f, 120f) * k * (1f - slide);
			int idx = Mathf.RoundToInt(Mathf.Clamp((t - 0.06f) / (HandAt - 0.07f), 0f, 1f) * 11f);
			if (_m3dFrames[idx] is Texture2D f && _m3d.Texture != f) _m3d.Texture = f;
			_split.SetShaderParameter("edge", 1.25f - 1.6f * Ease5Ext.InOutCubic(Mathf.Clamp((t - 0.08f) / 0.26f, 0f, 1f)));
			float m3dA = t >= HandAt ? 0f : Mathf.Clamp((t - 0.05f) / 0.02f, 0f, 1f);   // 手掌砸进来那一帧硬切，不留半透明重影
			_m3d.Visible = m3dA > 0.001f;
			_m3d.Modulate = new Color(1f, 1f, 1f, m3dA);
			// 手掌拍下来（用户：要有力量感）：从放大 1.22 倍猛地砸下去（0.36 → 0.42，加速），砸到底压扁到 0.965，
			// 再弹回 1.0（0.42 → 0.56）；砸中那一下整个界面短促一震（衰减 0.24s）+ 低沉的重击声
			float scale, drop;
			if (t < SlamAt)
			{
				float d = Mathf.Clamp((t - HandAt) / (SlamAt - HandAt), 0f, 1f);
				float e = d * d * d;
				scale = Mathf.Lerp(1.22f, 0.965f, e);
				drop = Mathf.Lerp(-70f, 8f, e);
			}
			else
			{
				float r = Mathf.Clamp((t - SlamAt) / 0.14f, 0f, 1f);
				float b = Ease5.OutBack(r);
				scale = Mathf.Lerp(0.965f, 1f, b);
				drop = Mathf.Lerp(8f, 0f, b);
			}
			_portrait.Position = home + new Vector2(0f, drop) * k;
			_porSprite.Scale = Vector2.One * _porScale * scale;
			_por.SetShaderParameter("fill_amt", 0f);
			_portrait.Modulate = new Color(1f, 1f, 1f, t >= HandAt ? 1f : 0f);
			Shake(t);
		}
		else
		{
			// 没有 3D 帧时的退路：2D 定格图本身做白剪影 → 黑剪影 → 原画
			float move = Ease5.OutExpo(Mathf.Clamp((t - 0.04f) / 0.34f, 0f, 1f));
			_portrait.Position = home + new Vector2(-300f, 180f) * k * (1f - move);
			float toBlack = Mathf.Clamp((t - 0.12f) / 0.06f, 0f, 1f);
			float toArt = Mathf.Clamp((t - 0.24f) / 0.16f, 0f, 1f);
			_por.SetShaderParameter("fill_color", Paper.Lerp(Ink, toBlack));
			_por.SetShaderParameter("fill_amt", 1f - Ease5.OutQuint(toArt));
			float pop = Mathf.Clamp((t - 0.28f) / 0.26f, 0f, 1f);
			_porSprite.Scale = Vector2.One * _porScale * Mathf.Lerp(0.96f, 1f, Ease5.OutBack(pop));
			_portrait.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp((t - 0.04f) / 0.03f, 0f, 1f));
		}
		// 标题牌、牌数、COMMAND 依次弹入
		foreach (var (node, at, rot, delay) in _pops)
		{
			float p = Mathf.Clamp((t - delay) / 0.22f, 0f, 1f);
			float b = Ease5.OutBack(p);
			node.Position = at + new Vector2(60f * k * (1f - Ease5.OutExpo(p)), 0f);
			node.Rotation = rot + Mathf.DegToRad(8f) * (1f - b);
			node.Scale = Vector2.One * Mathf.Lerp(0.7f, 1f, b);
			node.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(p * 3f, 0f, 1f));
		}
	}

	/// <summary>
	/// 手掌砸中的那一刻（用户 2026-11-22：手掌、音效的「啪」、预告信出现要在同一帧）。
	/// 原版打开菜单的音效 camp_open 自己带一声「啪」，峰值在文件第 0.488s；打开时（t=0）就开始播，所以砸中定在 0.488s，
	/// 低频重击 sub_boom（起音 0.001s）和拳击声 punch_taiman（起音 0.087s）在打开时就按偏移预约好，三声的冲击落在同一刻。
	/// </summary>
	public const float SlamAt = 0.488f;
	private const float HandAt = SlamAt - 0.06f;                       // 手掌出现、开始往下砸（砸 0.06s，加速）
	private bool _slammed;

	/// <summary>打开界面时调用一次（和 camp_open 同时）：把砸中那一下的两声按起音提前量预约好。</summary>
	public static void ScheduleSlamSounds()
	{
		Sfx.Play(Sfx.SubBoom, 0.7f, SlamAt);
		Sfx.Play(Sfx.Slam, 0.4f, SlamAt - 0.087f);
	}

	/// <summary>手掌砸中：整个界面（root）短促一震，衰减 0.24s；第一次到这一刻时放重击声。</summary>
	private void Shake(float t)
	{
		if (t >= SlamAt && !_slammed)
		{
			_slammed = true;
			_card?.Slam();                                             // 预告信同一帧出现，带一记踢转
		}
		float a = t < SlamAt ? 0f : Mathf.Max(0f, 1f - (t - SlamAt) / 0.24f);
		a *= a;
		float u = (t - SlamAt) * 34f;
		_root.Position = a <= 0f ? Vector2.Zero : new Vector2(Mathf.Sin(u * 1.3f) * 6f, Mathf.Cos(u) * 14f) * _k * a;
	}

	private void Exit(float e)
	{
		float k = _k;
		if (_m3d != null) _m3d.Visible = false;
		_root.Position = Vector2.Zero;
		// 面板先往右退（0 → 0.7），红底晚一点从后往按钮那个角收（0.15 → 1，先慢后快）
		float pe = Ease5Ext.InOutCubic(Mathf.Clamp(e / 0.7f, 0f, 1f));
		_panel.SetShaderParameter("edge_shift", pe * (_view.X - PanelEdgeBottom * k - _rx + 40f * k));
		float reach = (_view + new Vector2(0f, _view.Y)).Length() * 1.05f;
		_bg.SetShaderParameter("wipe", reach * (1f - Ease5Ext.InCubic(Mathf.Clamp((e - 0.15f) / 0.85f, 0f, 1f))));
		// Joker：原画先变黑剪影（不半透明，免得透出后面的星纹），再整个滑出去，最后才淡掉
		_por.SetShaderParameter("fill_color", Ink);
		_por.SetShaderParameter("fill_amt", Fx.OutCubic(Mathf.Clamp(e / 0.3f, 0f, 1f)));
		float slide = Ease5Ext.InCubic(Mathf.Clamp((e - 0.15f) / 0.75f, 0f, 1f));
		Vector2 home = (PortraitAt + new Vector2(PortraitW, PortraitW) * 0.5f) * k + new Vector2(_rx, 0f);
		_portrait.Position = home + new Vector2(-420f, 260f) * k * slide;
		_portrait.Modulate = new Color(1f, 1f, 1f, _porA * (1f - Mathf.Clamp((e - 0.7f) / 0.3f, 0f, 1f)));
		foreach (var (node, at, rot, _) in _pops)
		{
			node.Position = at + new Vector2(200f * k * Ease5Ext.InCubic(Mathf.Clamp(e / 0.7f, 0f, 1f)), 0f);
			node.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp(e * 1.8f, 0f, 1f));
		}
	}

	// ── 标题牌：黑 / 白斜纸片（描边是另一种颜色）+ 原版白字图 ──
	private Node2D Plate(Vector2 at, Vector2 size, float rotDeg, Color fill, Color edge, string? glyph, float glyphW, Color tint, float delay)
	{
		float k = _k;
		Node2D n = new() { Position = at * k + new Vector2(_rx, 0f), Rotation = Mathf.DegToRad(rotDeg) };
		_plates.AddChild(n);
		Vector2 h = size * 0.5f * k;
		float e = 6f * k;
		Fx.Poly(n, new[] { new Vector2(-h.X, -h.Y), new Vector2(h.X, -h.Y), new Vector2(h.X, h.Y), new Vector2(-h.X, h.Y) }, edge);
		Fx.Poly(n, new[] { new Vector2(-h.X + e, -h.Y + e), new Vector2(h.X - e, -h.Y + e), new Vector2(h.X - e, h.Y - e), new Vector2(-h.X + e, h.Y - e) }, fill);
		if (glyph != null) Glyph(n, glyph, Vector2.Zero, glyphW, 0f, tint);
		_pops.Add((n, n.Position, n.Rotation, delay));
		return n;
	}

	private Sprite2D? Glyph(Node2D parent, string tex, Vector2 at, float width, float rotDeg, Color tint)
	{
		if (Fx.Tex(tex) is not Texture2D t) return null;
		Sprite2D s = new() { Texture = t, Position = at * _k, Rotation = Mathf.DegToRad(rotDeg), Modulate = tint, TextureFilter = CanvasItem.TextureFilterEnum.Linear };
		float w = tex.EndsWith("_ol") && Fx.Tex(tex[..^3]) is Texture2D body ? width * t.GetWidth() / body.GetWidth() : width;
		s.Scale = Vector2.One * w * _k / t.GetWidth();
		parent.AddChild(s);
		return s;
	}

	// ── 星纹着色器 ──
	internal static ShaderMaterial StarMaterial(float baseSize, Color paper, Color ink, Color grayInk, Vector2 shade)
	{
		ShaderMaterial m = new() { Shader = StarShader };
		float[] data = new float[Stars.Length * 5];
		for (int i = 0; i < Stars.Length; i++)
		{
			data[i * 5] = Stars[i].X;
			data[i * 5 + 1] = Stars[i].Y;
			data[i * 5 + 2] = Stars[i].Z;
			data[i * 5 + 3] = Stars[i].W;
			data[i * 5 + 4] = StarGray[i];
		}
		m.SetShaderParameter("sdata", data);
		m.SetShaderParameter("base_size", baseSize);
		m.SetShaderParameter("paper", paper);
		m.SetShaderParameter("ink", ink);
		m.SetShaderParameter("gray_ink", grayInk);
		m.SetShaderParameter("shade", shade);
		return m;
	}

	private static Shader? _star, _fill;

	/// <summary>
	/// 同心五角星（外半径 1、内半径 inner 的正五角星的「多边形距离」按 rings 等分，黑 / 白交替，最外圈是墨色）。
	/// 原版数据在 1280×720 里，这里按 cover 方式铺满任意宽高。抗锯齿按每颗星在屏幕上的像素尺寸解析计算（循环里不能用 fwidth）。
	/// 遮罩：edge_* = 左边的斜边（面板）；wipe = 从 wipe_origin 沿 wipe_dir 铺开的距离，前沿一道黑边（红底入场）。
	/// </summary>
	private static Shader StarShader => _star ??= new Shader { Code = @"
shader_type canvas_item;
uniform float sdata[245];
uniform float base_size = 118.0;
uniform float rings = 8.0;
uniform float inner = 0.46;
uniform vec4 paper : source_color;
uniform vec4 ink : source_color;
uniform vec4 gray_ink : source_color;
uniform vec2 shade = vec2(0.0);
uniform float time = 0.0;
uniform float spin = 1.2;
uniform float edge_top = -1.0;
uniform float edge_bottom = -1.0;
uniform float edge_shift = 0.0;
uniform vec2 wipe_origin = vec2(0.0);
uniform vec2 wipe_dir = vec2(1.0, 0.0);
uniform float wipe = 1e7;
uniform vec2 size = vec2(1920.0, 1080.0);

void fragment() {
	vec2 px = UV * size;
	float kk = max(size.x / 1280.0, size.y / 720.0);
	vec2 off = (size - vec2(1280.0, 720.0) * kk) * 0.5;
	vec2 p0 = (px - off) / kk;
	const float SEG = 0.6283185;
	vec2 p2 = vec2(sin(SEG), cos(SEG)) * inner;
	vec2 nn = normalize(vec2(p2.y - 1.0, -p2.x));
	float c0 = nn.y;
	float grad = length(nn) / abs(c0);
	vec3 col = paper.rgb;
	for (int i = 0; i < 49; i++) {
		float s = sdata[i * 5 + 2] * base_size;
		vec2 d = (p0 - vec2(sdata[i * 5], sdata[i * 5 + 1])) / s;
		float r = length(d);
		if (r > 1.02) continue;
		float sgn = (i % 2 == 0) ? 1.0 : -1.0;
		float th = radians(sdata[i * 5 + 3] + time * spin * sgn);
		vec2 q = vec2(d.x * cos(th) + d.y * sin(th), -d.x * sin(th) + d.y * cos(th));
		float a = atan(q.x, -q.y);
		float t = abs(mod(a, 2.0 * SEG) - SEG);
		float dist = (r * sin(t) * nn.x + r * cos(t) * nn.y) / c0;
		if (dist >= 1.0 + 4.0 / (kk * s)) continue;
		float w = grad / (kk * s);              // 一个屏幕像素在 dist 里有多大
		float cov = clamp((1.0 - dist) / w + 0.5, 0.0, 1.0);
		float sr = dist * rings;
		float phase = mod(rings - 1.0, 2.0);
		float f = fract((sr - phase) * 0.5);
		float h = w * rings * 0.25 + 1e-4;
		float inkAmt = smoothstep(-h, h, f) - smoothstep(0.5 - h, 0.5 + h, f) + smoothstep(1.0 - h, 1.0 + h, f);
		vec3 ic = sdata[i * 5 + 4] > 0.5 ? gray_ink.rgb : ink.rgb;
		col = mix(col, mix(paper.rgb, ic, clamp(inkAmt, 0.0, 1.0)), cov);
	}
	vec2 uvs = px / size;
	col *= 1.0 - shade.x * uvs.x - shade.y * uvs.y;
	float alpha = 1.0;
	if (edge_top >= 0.0) {
		float ex = mix(edge_top, edge_bottom, px.y / size.y) + edge_shift;
		alpha *= clamp(px.x - ex + 0.5, 0.0, 1.0);
	}
	float proj = dot(px - wipe_origin, wipe_dir);
	alpha *= clamp(wipe - proj + 0.5, 0.0, 1.0);
	float front = clamp(proj - (wipe - 16.0) + 0.5, 0.0, 1.0);
	col = mix(col, vec3(0.02), front * step(wipe, 1e6));
	COLOR = vec4(col, alpha);
}
" };

	/// <summary>菜单 3D Joker：分界线左边整个变成白剪影，右边是原样（黑底白细节）；分界线上斜（上端偏右），抗锯齿。</summary>
	private static Shader? _splitShader;
	private static Shader SplitShader => _splitShader ??= new Shader { Code = @"
shader_type canvas_item;
uniform float edge = 1.25;
uniform float slope = 0.28;
void fragment() {
	// Godot 4：COLOR 进来时已经是 贴图 × modulate
	float line = edge + slope * (0.5 - UV.y);
	float w = fwidth(UV.x) * 1.5;
	float white = 1.0 - smoothstep(line - w, line + w, UV.x);
	COLOR.rgb = mix(COLOR.rgb, vec3(0.94), white);
}
" };

	/// <summary>立绘：按 fill_amt 把颜色换成 fill_color（剪影），保留透明度和 modulate。</summary>
	internal static Shader FillShader => _fill ??= new Shader { Code = @"
shader_type canvas_item;
uniform vec4 fill_color : source_color = vec4(1.0);
uniform float fill_amt = 0.0;
uniform float clip_top = -1.0;   // UV.y 小于它的部分不画（软边 0.04）；-1 = 不裁
void fragment() {
	if (clip_top > 0.0) COLOR.a *= smoothstep(clip_top - 0.02, clip_top + 0.02, UV.y);
	// Godot 4：COLOR 进来时已经是 贴图 × modulate
	COLOR.rgb = mix(COLOR.rgb, fill_color.rgb, fill_amt);
}
" };

	// 原版 INIT/CAMPBGSTARANIME.DAT 的 49 颗星：x、y（1280×720）、缩放、旋转角（度）
	private static readonly Vector4[] Stars = {
		new(614f, 509f, 2.055f, 10f), new(481f, 411f, 1.5f, 356f), new(1241f, 541f, 1.777f, 324f), new(906f, 55f, 2.055f, 21f),
		new(1194f, 649f, 1.777f, 30f), new(387f, 521f, 1.777f, 0f), new(696f, 27f, 1.777f, 350f), new(582f, 286f, 2.055f, 17f),
		new(815f, 358f, 2.055f, 293f), new(796f, 497f, 1.5f, 352f), new(1074f, 677f, 1.777f, 341f), new(486f, 644f, 2.055f, 18f),
		new(288f, 365f, 2.055f, 353f), new(757f, 164f, 2.055f, 27f), new(715f, 316f, 1.055f, -9f), new(921f, 644f, 1.777f, 302f),
		new(675f, 649f, 1.777f, 17f), new(185f, 214f, 1.777f, 13f), new(1231f, 360f, 2.055f, 352f), new(1111f, 508f, 1.055f, 326f),
		new(1044f, 48f, 1.055f, 324f), new(829f, 676f, 1.5f, 347f), new(1301f, 149f, 1.777f, 8f), new(1070f, 319f, 1.777f, 343f),
		new(712f, 451f, 1.777f, 51f), new(79f, 504f, 1.777f, 20f), new(79f, 51f, 2.055f, 312f), new(15f, 417f, 1.055f, 130f),
		new(212f, 43f, 1.055f, 323f), new(840f, 582f, 1.055f, 326f), new(1011f, 542f, 1.5f, 2f), new(254f, 547f, 1.5f, 346f),
		new(57f, 672f, 2.055f, 18f), new(1179f, 88f, 2.055f, 34f), new(468f, 95f, 2.055f, 340f), new(339f, 700f, 1.5f, 9f),
		new(332f, 55f, 1.777f, 55f), new(18f, 227f, 2.055f, 24f), new(962f, 446f, 2.055f, 347f), new(1194f, 241f, 1.055f, 42f),
		new(228f, 657f, 1.055f, 43f), new(299f, 185f, 1.055f, 330f), new(569f, 152f, 1.5f, 13f), new(935f, 241f, 1.777f, 62f),
		new(527f, 16f, 1.055f, 101f), new(1051f, 182f, 1.5f, 11f), new(146f, 739f, 1.5f, 202.999f), new(136f, 382f, 1.5f, 346f),
		new(408f, 264f, 1.9f, 351f),
	};
	/// <summary>1 = 灰色星（0x606060），0 = 黑色星。</summary>
	private static readonly float[] StarGray = { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 1f, 1f, 1f, 1f, 0f, 0f, 0f, 0f, 0f };

	/// <summary>原版暂停菜单素材 + 预告信（预热用）。</summary>
	public static readonly string[] Textures = BuildTextureList();
	private static string[] BuildTextureList()
	{
		List<string> l = new() { "pile_m3d_00", "pile_m3d_01", "pile_m3d_02", "pile_m3d_03", "pile_m3d_04", "pile_m3d_05", "pile_m3d_06", "pile_m3d_07", "pile_m3d_08", "pile_m3d_09", "pile_m3d_10", "pile_m3d_11", "pile_camptop", "pile_txt_command", "pile_txt_command_ol", "pile_txt_dontlook", "pile_txt_likethat", "pile_txt_menu", "pile_txt_main", "pile_txt_cmdselect", "pile_card_front" };
		for (int i = 0; i < 10; i++) { l.Add($"pile_digit_{i}"); l.Add($"pile_digit_{i}_ol"); }
		foreach (string s in CallingCard.Letters) l.Add("pile_letter_" + s);
		return l.ToArray();
	}
}

/// <summary>InCubic / InOutCubic（Ease5 里没有）。</summary>
public static class Ease5Ext
{
	public static float InCubic(float t) => t * t * t;
	public static float InOutCubic(float t) => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
}

/// <summary>
/// 白手掌心上方悬浮旋转的真 3D 预告信：SubViewport（独立 3D 世界、透明底、4×MSAA，按显示尺寸 2 倍渲染再缩小）里
/// 一张有厚度的卡（正面礼帽 / 背面原版信的正文 / 暗红侧边），透视相机；越侧越暗，一道很淡的斜向反光随角度扫过。
/// 转速和碰牌踢转照抄 P3 mod 的 HeroStage：底速 36°/s，打开后 0.6s smoothstep 起转；碰牌 Target += 120°，弹簧 ω = 9（临界阻尼）。
/// 贴图运行时生成 mipmap（ctex 里没有），缩小时不闪。
/// </summary>
public sealed class CallingCard
{
	public static readonly string[] Letters = { "kamoshida", "madarame", "kaneshiro", "futaba", "okumura", "sae", "maruki" };
	private const float Focal = 1100f;
	private float W = 276f, H = 198f, Thick = 6f;
	private Vector2 Box = new(400f, 320f);                          // 显示框（1080p 像素），转到斜着也装得下
	private const float BaseSpin = 36f, KickDeg = 120f, KickOmega = 9f;
	private static readonly Dictionary<string, Texture2D> Mipped = new();

	private SubViewport _vp = null!;
	private TextureRect _view = null!;
	private Node3D _spin = null!;
	private ShaderMaterial _front = null!, _back = null!;
	private Polygon2D _shadow = null!;
	private Vector2 _center;
	private float _k;
	private float _base, _extra, _vel, _target, _born = -1f, _alpha;

	public static CallingCard? Create(Control parent, Vector2 center, float k, float size = 1f)
	{
		Texture2D? front = Mip("pile_card_front");
		Texture2D? back = Mip("pile_letter_" + Letters[(int)(GD.Randi() % (uint)Letters.Length)]);
		if (front == null || back == null) return null;
		CallingCard c = new() { _center = center, _k = k };
		c.W *= size; c.H *= size; c.Thick *= size; c.Box *= size;
		Vector2I px = (Vector2I)(c.Box * k * 2f).Ceil();
		c._vp = new SubViewport { Size = px, TransparentBg = true, OwnWorld3D = true, Msaa3D = Viewport.Msaa.Msaa4X, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
		parent.AddChild(c._vp);
		Camera3D cam = new() { Position = new Vector3(0f, 0f, Focal), Fov = Mathf.RadToDeg(2f * Mathf.Atan(c.Box.Y * 0.5f / Focal)), Near = 10f, Far = 4000f };
		c._vp.AddChild(cam);
		// 歪一点（绕 Z -8°）再后仰一点（绕 X 8°），里面那层绕 Y 转
		Node3D tilt = new() { RotationDegrees = new Vector3(8f, 0f, -8f) };
		c._vp.AddChild(tilt);
		c._spin = new Node3D();
		tilt.AddChild(c._spin);
		c._front = Face(front);
		c._back = Face(back);
		c._spin.AddChild(new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(c.W, c.H) }, MaterialOverride = c._front, Position = new Vector3(0f, 0f, c.Thick * 0.5f + 0.05f) });
		c._spin.AddChild(new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(c.W, c.H) }, MaterialOverride = c._back, Position = new Vector3(0f, 0f, -c.Thick * 0.5f - 0.05f), RotationDegrees = new Vector3(0f, 180f, 0f) });
		c._spin.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(c.W - 0.5f, c.H - 0.5f, c.Thick) }, MaterialOverride = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(0.27f, 0.024f, 0.04f) } });
		// 落在手上的投影（跟着卡的宽度变化）
		c._shadow = new Polygon2D { Color = new Color(0.12f, 0f, 0f, 0.3f), Polygon = Fx.Circle(1f, 48), Material = Soft };
		parent.AddChild(c._shadow);
		c._view = new TextureRect { Texture = c._vp.GetTexture(), Size = c.Box * k, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, TextureFilter = CanvasItem.TextureFilterEnum.Linear, MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = new Color(1f, 1f, 1f, 0f) };
		parent.AddChild(c._view);
		return c;
	}

	private static Texture2D? Mip(string name)
	{
		if (Mipped.TryGetValue(name, out Texture2D? t)) return t;
		if (Fx.Tex(name) is not Texture2D src || src.GetImage() is not Image img) return null;
		img = (Image)img.Duplicate();
		if (img.IsCompressed()) img.Decompress();
		img.GenerateMipmaps();
		t = ImageTexture.CreateFromImage(img);
		Mipped[name] = t;
		return t;
	}

	private static Shader? _face, _soft;
	private static ShaderMaterial Face(Texture2D tex)
	{
		_face ??= new Shader { Code = @"
shader_type spatial;
render_mode unshaded, cull_back;
uniform sampler2D tex : source_color, filter_linear_mipmap_anisotropic, repeat_disable;
uniform vec2 pad = vec2(0.00617, 0.00858);
uniform float lam = 1.0;
uniform float band_pos = 0.0;
uniform float band_amt = 0.0;
void fragment() {
	vec2 uv = mix(pad, vec2(1.0) - pad, UV);
	vec3 c = texture(tex, uv).rgb;
	float b = exp(-pow((UV.x * 2.0 - 1.0) * 0.8 + (UV.y * 2.0 - 1.0) * 0.45 - band_pos, 2.0) / 0.012) * band_amt;
	ALBEDO = c * lam + vec3(b);
}
" };
		ShaderMaterial m = new() { Shader = _face };
		m.SetShaderParameter("tex", tex);
		m.SetShaderParameter("pad", new Vector2(4f / tex.GetWidth(), 4f / tex.GetHeight()));
		return m;
	}

	/// <summary>投影：圆盘中心实、边缘按半径软掉。</summary>
	private static ShaderMaterial Soft => new() { Shader = _soft ??= new Shader { Code = @"
shader_type canvas_item;
varying vec2 local;
void vertex() { local = VERTEX; }
void fragment() { float r = length(local); COLOR.a *= 1.0 - smoothstep(0.0, 1.0, r); }
" } };

	public void Kick() => _target += KickDeg;

	/// <summary>手掌砸中：这一帧出现，起转就带一记踢转（被拍出来的劲），之后落回底速。</summary>
	public void Slam()
	{
		if (_born >= 0f) return;
		_born = _now;
		_target += 200f;
		_vel = 900f;
	}
	private float _now;

	public void Update(float now, float dt, bool alive)
	{
		if (!GodotObject.IsInstanceValid(_view)) return;
		dt = Mathf.Clamp(dt, 0f, 1f / 30f);
		_now = now;
		float life = _born < 0f ? 0f : now - _born;
		float ramp = Mathf.Clamp(life / 0.6f, 0f, 1f);
		_base = (_base + BaseSpin * ramp * ramp * (3f - 2f * ramp) * dt) % 360f;
		float a = KickOmega * KickOmega * (_target - _extra) - 2f * KickOmega * _vel;
		_vel += a * dt;
		_extra += _vel * dt;
		float turns = Mathf.Floor(_extra / 360f) * 360f;
		_extra -= turns;
		_target -= turns;
		float theta = Mathf.DegToRad(_base + _extra);
		_spin.Rotation = new Vector3(0f, theta, 0f);
		float c = Mathf.Cos(theta), s = Mathf.Sin(theta);
		float lam = 0.62f + 0.38f * Mathf.Abs(c);
		foreach (ShaderMaterial m in new[] { _front, _back })
		{
			m.SetShaderParameter("lam", lam);
			m.SetShaderParameter("band_pos", s * 1.6f * (m == _front ? 1f : -1f));
			m.SetShaderParameter("band_amt", 0.22f * Mathf.Sqrt(Mathf.Abs(c)));
		}
		// 出现：0.4s 起 0.3s 弹出；退场时跟着淡掉
		// 砸中那一帧直接出现（不弹跳，用户：太软）：满尺寸、不透明，带一下短促的白闪 + 随手掌一起往下一顿
		_alpha = alive ? (_born < 0f ? 0f : 1f) : Mathf.MoveToward(_alpha, 0f, dt / 0.2f);
		float flash = _born < 0f ? 0f : Mathf.Max(0f, 1f - life / 0.12f);
		float scale = 1f;
		float bob = 6f * Mathf.Sin(now * 1.7f) * _k;
		Vector2 size = Box * _k * scale;
		_view.Size = size;
		_view.Position = _center - size * 0.5f + new Vector2(0f, bob);
		float fl = 1f + 1.2f * flash * flash;
		_view.Modulate = new Color(fl, fl, fl, _alpha);
		float sw = (Mathf.Abs(c) * W * 0.5f + 10f) * _k * scale;
		_shadow.Position = _center + new Vector2(30f, 128f) * _k * (H / 198f);
		_shadow.Scale = new Vector2(sw * 1.15f, 30f * _k * scale * (H / 198f));
		_shadow.Modulate = new Color(1f, 1f, 1f, _alpha);
	}
}
