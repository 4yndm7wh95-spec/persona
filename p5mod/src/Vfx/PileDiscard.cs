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
/// 入场（约 0.5s）：红底从右下铺开 → 黑块从右边甩进来、立绘从左边推进来 → 左边面板滑进、六行依次弹出 → 标题牌 / COMMAND 压上 → 斜带从右下切进来 → 牌飞入。
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
	public float CardsStart => 0.4f;
	public float ExitTime => 0.32f;
	public Vector2 FocusAt => new(560f, 540f);
	public Vector2 InfoAt => new(980f, 300f);
	public Vector2 SwitchAt => new(1440f, 836f);
	public Vector2 EnterOffset => new(70f, 30f);
	public Vector2 ExitOffset => new(260f, 40f);

	private static readonly Color Ink = PileDraw.Ink, Paper = PileDraw.Paper;
	private static readonly Color Teal = new(92 / 255f, 230 / 255f, 200 / 255f), Pink = new(226 / 255f, 70 / 255f, 220 / 255f);
	private const float ListK = 0.84f;
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

		// 4. 左边面板：斜边（白边 + 黑 + 内侧红条）+ 六行 + 两个标签牌
		_panel = new Node2D { Name = "PileDiscardList" };
		root.AddChild(_panel);
		Fx.Poly(_panel, new[] { new Vector2(-40f, -20f), EdgeA + new Vector2(14f, 0f), EdgeB + new Vector2(14f, 0f), new Vector2(-40f, 1100f) }.Select(p => p * k).ToArray(), new Color(0.94f, 0.94f, 0.94f));
		Fx.Poly(_panel, new[] { new Vector2(-40f, -20f), EdgeA, EdgeB, new Vector2(-40f, 1100f) }.Select(p => p * k).ToArray(), new Color(0.03f, 0.03f, 0.03f));
		Fx.Poly(_panel, new[] { EdgeA + new Vector2(-22f, 0f), EdgeA + new Vector2(-15f, 0f), EdgeB + new Vector2(-15f, 0f), EdgeB + new Vector2(-22f, 0f) }.Select(p => p * k).ToArray(), new Color(0.78f, 0.08f, 0.12f));
		for (int i = 0; i < Rows.Length; i++)
		{
			var row = Rows[i];
			(int n, int up) = i == 0 ? (total, totalUp) : counts.TryGetValue(row.Key, out var v) ? v : (0, 0);
			Node2D node = new() { Modulate = new Color(1f, 1f, 1f, n > 0 ? 1f : 0.42f) };
			_panel.AddChild(node);
			Layer(node, $"row{i}", row.Img * k);
			RowText(node, row.Key, row.Hx, row.Yc, row.Tilt, n, up, i > 0);
			_rows.Add((node, 0.16f + 0.045f * i, n > 0 ? 1f : 0.42f));
		}
		_tags.Add(Layer(_panel, "tag_discard", new Vector2(0f, 20f) * k));
		_tags.Add(Layer(_panel, "tag_bytype", new Vector2(0f, 282f) * k));

		// 5. 标题牌、COMMAND + 说明
		_title = Layer(root, "title", R(1458f, 7f));
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
		Fx.Poly(_bandRoot, new[] { R(1765.7f, 766.6f), R(1804.8f, 883.8f), R(1223.7f, 1049.8f) }, new Color(0.84f, 0.84f, 0.84f));
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
			n.AddChild(new Sprite2D { Texture = t, Centered = false, Scale = Vector2.One * _k, TextureFilter = CanvasItem.TextureFilterEnum.Linear });
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
			parent.AddChild(new Sprite2D { Texture = t, Centered = false, Position = at + new Vector2(x, 0f), Scale = Vector2.One * h / t.GetHeight(), Modulate = tint, TextureFilter = CanvasItem.TextureFilterEnum.Linear });
			x += w * advance;
			lastW = w;
		}
		return x + lastW * (1f - advance) + 4f;
	}

	private static void Underline(Node2D parent, Vector2 at, float w, Color tint)
	{
		if (Fx.Tex("pile_dc_underline") is not Texture2D t) return;
		parent.AddChild(new Sprite2D { Texture = t, Centered = false, Position = at, Scale = new Vector2(w / t.GetWidth(), 11f / t.GetHeight()), Modulate = tint, TextureFilter = CanvasItem.TextureFilterEnum.Linear });
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
		_bg.SetShaderParameter("wipe", t >= 0.3f ? 1e7f : Ease5.OutQuint(Seg(t, 0f, 0.3f)) * reach);
		// 黑块从右边甩进来、立绘从左边推进来（两块在撞色斜线那里对上）
		float a = Ease5.OutExpo(Seg(t, 0.03f, 0.3f));
		_slab.Position = new Vector2(548f * k + _rx + 900f * k * (1f - a), 0f);
		_slab.Modulate = new Color(1f, 1f, 1f, Seg(t, 0.03f, 0.06f));
		float p = Ease5.OutExpo(Seg(t, 0.07f, 0.32f));
		_portrait.Position = new Vector2(-260f * k * (1f - p), 0f);
		_portrait.Modulate = new Color(1f, 1f, 1f, Seg(t, 0.07f, 0.08f));
		// 左边面板滑进来，六行依次弹出
		float l = Ease5.OutExpo(Seg(t, 0.1f, 0.28f));
		_panel.Position = new Vector2(-520f * k * (1f - l), 0f);
		foreach (var (node, delay, alpha) in _rows)
		{
			float r = Seg(t, delay, 0.22f);
			node.Position = new Vector2(-140f * k * (1f - Ease5.OutBack(r)), 0f);
			node.Modulate = new Color(1f, 1f, 1f, Mathf.Min(1f, r * 4f) * alpha);
		}
		for (int i = 0; i < _tags.Count; i++)
		{
			float q = Ease5.OutBack(Seg(t, 0.34f + 0.05f * i, 0.16f));
			_tags[i].Scale = Vector2.One * Mathf.Lerp(1.5f, 1f, q);
			_tags[i].Modulate = new Color(1f, 1f, 1f, Mathf.Min(1f, Seg(t, 0.34f + 0.05f * i, 0.05f)));
		}
		Pop(_title, t, 0.3f, R0(1458f, 7f));
		Pop(_command, t, 0.34f, Vector2.Zero);
		// 斜带从右下切进来
		float b = Ease5.OutExpo(Seg(t, 0.28f, 0.26f));
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
