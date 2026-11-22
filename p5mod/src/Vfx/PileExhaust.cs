using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 消耗堆界面 = P5R 暂停菜单 SKILL 页（docs/p5/PILE_PLAN.md，排版图 docs/p5/preview/pile_exhaust_layout_v2.png）。
/// 层次：红星纹底（不透明）→ 黑色大斜块 + 左下浅灰楔形（原版 SKILL 页的构图，矢量多边形）→ 立绘后面的黑白星纹斜带（同一份原版星纹数据）
/// → 原版 Joker 咧嘴笑立绘（CAMPSKILL_2D01；放大到下切口在屏幕底边外、左切口在屏幕左边外，用户：不要露出立绘边缘）
/// → 右上标题牌 MeNu&amp;EXHAUST → COMMAND + 说明；牌格子在右边黑块里（原版技能列表的位置）。
/// 入场（同抽牌堆的节奏和力量感）：红底从右下角（消耗堆按钮）铺开，白色大斜板从右边扫进来，Joker 以黑剪影压在白板上滑入，
/// 砸中那一刻（PileDraw.SlamAt，和原版 camp_open 的「啪」同一刻）硬切成原版构图：白板变黑块 + 灰楔形，立绘从放大砸下、压扁回弹，界面一震。
/// </summary>
public sealed class PileExhaust : IPileDecor
{
	public float GridX => 1130f;
	public float GridTop => 196f;
	public float GridBottom => 1050f;
	public int Cols => 4;
	public float CardW => 158f;
	public float CellW => 192f;
	public float CellH => 252f;
	public float CardsStart => PileDraw.SlamAt - 0.02f;     // 砸中之后牌才从右边飞进来
	public float ExitTime => 0.32f;
	public Vector2 FocusAt => new(560f, 560f);
	public Vector2 InfoAt => new(980f, 300f);
	public Vector2 SwitchAt => new(1130f, 120f);
	public Vector2 EnterOffset => new(70f, 30f);
	public Vector2 ExitOffset => new(260f, 40f);

	private const float SlamAt = PileDraw.SlamAt, HandAt = SlamAt - 0.06f;
	private static readonly Color Ink = PileDraw.Ink, Paper = PileDraw.Paper, Gray = new(0.84f, 0.84f, 0.84f);
	private static readonly Vector2 PorAt = new(-80f, 40f);
	private const float PorW = 1500f;

	private readonly Control _root;
	private readonly Vector2 _view;
	private readonly float _k, _rx;
	private readonly ShaderMaterial _bg, _por;
	private readonly Polygon2D _slab, _wedge, _white;
	private readonly Node2D _band, _portrait;
	// 入场的原版 3D Joker（SKILL 页镜头，侧身插兜站着：main_0015 淡入 main_0113，共 11 帧）和它脚下扫过的弧形星纹带
	private readonly Texture2D?[] _sk3d = new Texture2D?[11];
	private readonly Sprite2D? _m3d;
	private readonly Node2D _arc;
	private readonly float _arcR0, _arcR1;
	private readonly Vector2 _arcC;
	private readonly Sprite2D _porSprite;
	private readonly float _porScale = 1f;
	private readonly List<(Node2D Node, Vector2 At, float Rot, float Delay)> _pops = new();
	private float _exitT0 = -1f;
	private bool _slammed;

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

		// 2. 黑色大斜块 + 左下灰楔形（原版构图），入场时先是同形状的白板
		Vector2[] slab = { R(560f, 0f), R(1650f, 0f), R(1920f, 300f), R(1920f, 760f), R(1350f, 1080f), R(560f, 1080f) };
		_slab = new Polygon2D { Polygon = slab, Color = new Color(0.03f, 0.03f, 0.03f), Antialiased = true, Visible = false };
		root.AddChild(_slab);
		_wedge = new Polygon2D { Polygon = new[] { R(560f, 1080f), R(560f, 620f), R(1350f, 1080f) }, Color = Gray, Antialiased = true, Visible = false };
		root.AddChild(_wedge);
		_white = new Polygon2D { Polygon = slab, Color = Paper, Antialiased = true };
		root.AddChild(_white);

		// 3. 立绘后面的黑白星纹斜带：裁在一个斜四边形里（clip_children）
		Polygon2D bandMask = new() { Polygon = new[] { R(600f, 0f), R(900f, 0f), R(700f, 520f), R(560f, 520f) }, Color = Colors.White, ClipChildren = CanvasItem.ClipChildrenMode.Only, Visible = false };
		root.AddChild(bandMask);
		ShaderMaterial bandMat = PileDraw.StarMaterial(118f, new Color(0.91f, 0.91f, 0.91f), new Color(0.03f, 0.03f, 0.03f), new Color(0.376f, 0.376f, 0.376f), Vector2.Zero);
		bandMat.SetShaderParameter("size", view);
		bandMask.AddChild(new ColorRect { Size = view, Material = bandMat, MouseFilter = Control.MouseFilterEnum.Ignore });
		_band = bandMask;

		// 3b. 入场时扫过画面的弧形星纹带（原版 SKILL 页 frame_0110：从左下弯到右上的一条粗弧），绕圆心转进来
		_arcC = new Vector2(1250f, 1550f) * k;
		_arcR0 = 1150f * k;
		_arcR1 = 1480f * k;
		Polygon2D arcMask = new() { Color = Colors.White, ClipChildren = CanvasItem.ClipChildrenMode.Only, Position = _arcC, Antialiased = true };
		List<Vector2> ring = new();
		for (int i = 0; i <= 48; i++) { float a = Mathf.DegToRad(-175f + 95f * i / 48f); ring.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * _arcR1); }
		for (int i = 48; i >= 0; i--) { float a = Mathf.DegToRad(-175f + 95f * i / 48f); ring.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * _arcR0); }
		arcMask.Polygon = ring.ToArray();
		root.AddChild(arcMask);
		ShaderMaterial arcMat = PileDraw.StarMaterial(118f, new Color(0.91f, 0.91f, 0.91f), new Color(0.03f, 0.03f, 0.03f), new Color(0.376f, 0.376f, 0.376f), Vector2.Zero);
		arcMat.SetShaderParameter("size", view);
		arcMask.AddChild(new ColorRect { Size = view, Position = -_arcC, Material = arcMat, MouseFilter = Control.MouseFilterEnum.Ignore });
		_arc = arcMask;

		// 3c. 原版 3D Joker（黑身白脸，1:1 贴屏幕左上角，渲染画面的上 / 左 / 下边缘就是屏幕边缘）
		for (int i = 0; i < 11; i++) _sk3d[i] = Fx.Tex($"pile_sk3d_{i:00}");
		if (_sk3d[0] != null)
		{
			_m3d = new Sprite2D { Texture = _sk3d[0], Centered = false, Scale = Vector2.One * k, TextureFilter = CanvasItem.TextureFilterEnum.Linear, Visible = false };
			root.AddChild(_m3d);
		}

		// 4. Joker（原版 CAMPSKILL_2D01）
		_portrait = new Node2D { Name = "PilePortrait", Position = (PorAt + new Vector2(PorW, PorW) * 0.5f) * k };
		root.AddChild(_portrait);
		_por = new ShaderMaterial { Shader = PileDraw.FillShader };
		_porSprite = new Sprite2D { Texture = Fx.Tex("pile_campskill"), Material = _por, TextureFilter = CanvasItem.TextureFilterEnum.Linear };
		if (_porSprite.Texture is Texture2D pt) _porScale = PorW * k / pt.GetWidth();
		_porSprite.Scale = Vector2.One * _porScale;
		_portrait.AddChild(_porSprite);

		// 5. 标题牌 MeNu&EXHAUST + COMMAND SELECT（原版字图 + P5 字图）
		Node2D plate = new() { Position = R(1680f, 90f), Rotation = Mathf.DegToRad(-4f) };
		root.AddChild(plate);
		Vector2 h = new Vector2(215f, 65f) * k;
		Fx.Poly(plate, new[] { -h, new Vector2(h.X, -h.Y), h, new Vector2(-h.X, h.Y) }, Paper);
		Vector2 hi = h - new Vector2(6f, 6f) * k;
		Fx.Poly(plate, new[] { -hi, new Vector2(hi.X, -hi.Y), hi, new Vector2(-hi.X, hi.Y) }, Ink);
		Glyph(plate, "pile_txt_menu", new Vector2(-128f, -15f), 120f, Paper);
		PileText.Exact(plate, "menu_exhaust", new Vector2(-58f, -46f) * k, 52f * k, Paper);
		Glyph(plate, "pile_txt_cmdselect", new Vector2(5f, 40f), 212f, Paper);
		_pops.Add((plate, plate.Position, plate.Rotation, SlamAt + 0.02f));

		// 6. COMMAND + 说明
		Node2D command = new() { Position = new Vector2(760f, 915f) * k, Rotation = Mathf.DegToRad(-4f) };
		root.AddChild(command);
		Glyph(command, "pile_txt_command_ol", Vector2.Zero, 551f, Ink);
		Glyph(command, "pile_txt_command", Vector2.Zero, 551f, Paper);
		PileDraw.Caption(command, "exhaust", count, new Vector2(-150f, 100f) * k, 38f * k);
		_pops.Add((command, command.Position, command.Rotation, SlamAt + 0.06f));

		Enter(0f);
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

	private void Enter(float t)
	{
		float k = _k;
		float reach = (_view + new Vector2(0f, _view.Y)).Length() * 1.05f;
		_bg.SetShaderParameter("wipe", t >= 0.3f ? 1e7f : Ease5.OutQuint(Mathf.Clamp(t / 0.3f, 0f, 1f)) * reach);
		// 白板从右边扫进来（0.04 → 0.22）
		float sweep = Ease5.OutExpo(Mathf.Clamp((t - 0.04f) / 0.18f, 0f, 1f));
		bool cut = t >= HandAt;
		_white.Position = new Vector2(1500f * k * (1f - sweep), 0f);
		_white.Visible = !cut;
		_slab.Visible = _wedge.Visible = _band.Visible = cut;
		Vector2 home = (PorAt + new Vector2(PorW, PorW) * 0.5f) * k;
		// 弧形星纹带转进来（0.05 → 0.24），切到最终构图时收掉
		float arcIn = Ease5.OutExpo(Mathf.Clamp((t - 0.05f) / 0.19f, 0f, 1f));
		_arc.Rotation = Mathf.DegToRad(-70f) * (1f - arcIn);
		_arc.Visible = !cut && t >= 0.05f;
		if (!cut)
		{
			_portrait.Modulate = new Color(1f, 1f, 1f, 0f);
			if (_m3d != null)
			{
				// 原版 frame_0111 → 0120：侧身插兜站着（近景 3 帧 → 中景 8 帧），0.08 → HandAt 播完
				int idx = Mathf.Clamp((int)((t - 0.08f) / (HandAt - 0.08f) * 11f), 0, 10);
				if (_sk3d[idx] is Texture2D f && _m3d.Texture != f) _m3d.Texture = f;
				float slide = Ease5.OutExpo(Mathf.Clamp((t - 0.08f) / 0.16f, 0f, 1f));
				_m3d.Position = new Vector2(-160f * k * (1f - slide), 0f);
				_m3d.Visible = t >= 0.08f;
			}
		}
		else
		{
			// 硬切成原版构图，立绘从放大砸下（HandAt → SlamAt，加速），压扁 0.965 再回弹（同抽牌堆的手掌）
			float scale, drop;
			if (t < SlamAt)
			{
				float e = Mathf.Pow(Mathf.Clamp((t - HandAt) / (SlamAt - HandAt), 0f, 1f), 3f);
				scale = Mathf.Lerp(1.12f, 0.965f, e);
				drop = Mathf.Lerp(-40f, 8f, e);
			}
			else
			{
				float b = Ease5.OutBack(Mathf.Clamp((t - SlamAt) / 0.14f, 0f, 1f));
				scale = Mathf.Lerp(0.965f, 1f, b);
				drop = Mathf.Lerp(8f, 0f, b);
			}
			_portrait.Position = home + new Vector2(0f, drop) * k;
			_porSprite.Scale = Vector2.One * _porScale * scale;
			_por.SetShaderParameter("fill_amt", 0f);
			_portrait.Modulate = Colors.White;
			if (_m3d != null) _m3d.Visible = false;
			Shake(t);
		}
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

	private void Shake(float t)
	{
		if (t >= SlamAt) _slammed = true;
		float a = t < SlamAt ? 0f : Mathf.Max(0f, 1f - (t - SlamAt) / 0.24f);
		a *= a;
		float u = (t - SlamAt) * 34f;
		_root.Position = a <= 0f ? Vector2.Zero : new Vector2(Mathf.Sin(u * 1.3f) * 6f, Mathf.Cos(u) * 14f) * _k * a;
	}

	private void Exit(float e)
	{
		float k = _k;
		_root.Position = Vector2.Zero;
		if (_m3d != null) _m3d.Visible = false;
		_arc.Visible = false;
		float ei = Ease5Ext.InCubic(e);
		float reach = (_view + new Vector2(0f, _view.Y)).Length() * 1.05f;
		_bg.SetShaderParameter("wipe", reach * (1f - Ease5Ext.InCubic(Mathf.Clamp((e - 0.15f) / 0.85f, 0f, 1f))));
		// 黑块和星纹带往右退；立绘先变黑剪影再往右上滑走
		float pe = Ease5Ext.InOutCubic(Mathf.Clamp(e / 0.75f, 0f, 1f));
		Vector2 off = new(1500f * k * pe, 0f);
		_slab.Position = _wedge.Position = _band.Position = off;
		// 黑块 / 灰楔形 / 星纹带边退边淡掉（不然灰楔形滑到牌后面露出一块白三角）
		Color fade = new(1f, 1f, 1f, 1f - Mathf.Clamp(e / 0.45f, 0f, 1f));
		_wedge.Modulate = fade;
		_band.Modulate = fade;
		_slab.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp((e - 0.25f) / 0.5f, 0f, 1f));
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
		parent.AddChild(new Sprite2D { Texture = t, Position = at * _k, Scale = Vector2.One * w * _k / t.GetWidth(), Modulate = tint, TextureFilter = CanvasItem.TextureFilterEnum.Linear });
	}

	public static readonly string[] Textures = { "pile_sk3d_00", "pile_sk3d_01", "pile_sk3d_02", "pile_sk3d_03", "pile_sk3d_04", "pile_sk3d_05", "pile_sk3d_06", "pile_sk3d_07", "pile_sk3d_08", "pile_sk3d_09", "pile_sk3d_10", "pile_campskill", "pile_txt_menu", "pile_txt_cmdselect", "pile_txt_command", "pile_txt_command_ol" };
}
