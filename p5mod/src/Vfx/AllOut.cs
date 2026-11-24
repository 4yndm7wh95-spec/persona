using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace P5Joker;

/// <summary>
/// 总攻击（精英 / Boss 战最后一击之后、胜利结算之前；AllOutPatches 把结算推迟 Hold 秒）。思路照 P3 mod（触发 → 切入 → 立绘定格 → 盖住再挖开露出结算），
/// 切入换成 P5R 自己的语言（用户 2026-11-18）：
///   0.00  定格：最后一击那一帧冻住、压成红黑双色，这一局的怪物变成黑剪影（白描边）；原版总攻击斩击声
///   0.16  乱斗：五六道白刃红边的刀光轮流划过剪影，每刀剪影白闪一帧、一震，带闪光星（原版总攻击的打击 / 斩击声 SYSTEM #24–29）
///   0.74  画面裂开（白色裂纹）→ 0.80 以怪物为中心碎成带厚度的玻璃片飞散，露出 P5R 原版收尾的红底（AllOutFinale，BKSK_HERO_CTE 实时重放）
///         怪物剪影被最后一击打飞，从战场位置甩到画面左下（夹在原版背景和人物之间）
///   1.00  原版收尾：Joker 和碎星出现（源时间 2.2s）的同一拍，怪物剪影身上「喷血」—— 黑色剪影的血柱（拖成弧线、末端甩出血点、溅成 P5R 原版
///         splatterBlood 的血团形状），粘在红底上留到最后；竖排 THE SHOW'S OVER 照原版
///   2.85  收尾：黑墨从右往左卷过盖满（边缘用 P5R 原版烟雾噪声撕开、带一道红边），再从中间挖开一个红边的洞，露出下面的结算
/// 全部挂在最上层 CanvasLayer（盖住手牌和 UI）；一个时钟驱动所有元素（预览录像逐帧确定）。
/// </summary>
public static partial class AllOut
{
	/// <summary>一只怪物的剪影：截下来的贴图（透明底）+ 它在屏幕上的位置（贴图按 Rect 大小摆放）。</summary>
	public sealed class Foe
	{
		public Texture2D Tex = null!;
		public Rect2 Rect;
		public List<Vector2> Wounds = new();      // 贴图 UV（0–1）里的伤口
		public Rect2 Tight = new(0f, 0f, 1f, 1f);  // 不透明部分在贴图 UV 里的范围
	}

	// ── 时间轴（秒） ──
	// 节奏照 P3 mod（最后一击 → 约 0.6s 蓄势 → 立绘）：定格顿 0.15s，裂纹 0.3s 内看得见地长满，停一拍、往里推；0.55 碎开
	private const float CrackAt = 0.22f, ShatterAt = 1.0f, ShardLife = 0.7f, SlamLen = 0.16f;
	// 碎开那一刻原版收尾已经在玻璃后面（从源时间 ArtAt 前一点开始放，碎星的血花照原版炸开），三层各自切入：
	// 背景从放大缩回、Joker 从右边快速滑进、竖排字从左边进、怪物剪影从左边砸进；剪影落定（ArtT）时喷血
	// 碎开那一刻立绘卡硬切进来（在玻璃片后面，没有空红底），白闪 + 撞击点一颗巨大的白色四角星，整张卡从放大压回来、震屏；
	// 怪物剪影同一拍砸进左下，紧接着喷血
	private const float SrcStart = AllOutFinale.ArtAt - 0.03f, Settle = 0.32f;
	// 入场分拍（相对碎开）：0 碎开 + 红底压回（冲击）→ 0.02–0.4 碎星转进来（丝滑，带一点回弹）→ 0.14–0.38 Joker 冲进来、落定震屏（冲击）
	// → 0.42 怪物剪影砸进 + 喷血 + 震屏（第二下冲击）→ 0.5–0.8 竖排字划上来（丝滑）
	// 立绘弹出后各层一直保持运动直到退场（用户 2026-11-18）：每秒放大 Drift（各层倍数不同，有纵深），加上缓慢的平移
	private const float Drift = 0.022f;
	private const float TextIn = 0.75f, TextLen = 0.42f;
	private const float ArtT = ShatterAt + 0.5f;
	// 收尾照 P3 mod 的速率：红色的波 CoverSweep 秒扫满（u≈0.75 时已盖满），停一拍，透明波 ClearSweep 秒扩开
	private const float CoverSweep = 0.42f, ClearSweep = 0.55f;
	private const float ExitAt = ShatterAt + 2.6f, CoverLen = 0.08f + 0.75f * CoverSweep, HoleAt = ExitAt + CoverLen, HoleLen = ClearSweep;
	public const float Total = HoleAt + HoleLen + 0.05f;
	/// <summary>结算推迟多久：洞开到一半左右时结算页出现在洞里。</summary>
	public const float Hold = HoleAt + 0.17f;                    // 照 P3：透明波扩开 0.17s 后（约 65% 屏）结算页出现

	private static readonly Color Red = new(0.86f, 0.02f, 0.09f);
	private static readonly Color Ink = new(0.015f, 0.008f, 0.012f);

	// ── 慢放（0.9.58）：最后一击之后到碎开之前，游戏画面慢放（只改本地流速，不改结算） ──
	private const float SlowScale = 0.12f;
	private static bool _slowed;
	private static ulong _playRun;
	private static double _savedScale = 1.0;

	private static void SlowDown(float scale)
	{
		if (!_slowed)
		{
			HitStop.Finish();
			_savedScale = Engine.TimeScale;
		}
		_slowed = true;
		Engine.TimeScale = scale;
	}

	/// <summary>总攻击正在播（碎开之前的慢放段）。</summary>
	public static bool Playing => _slowed;

	/// <summary>恢复正常速度（只在我们改过时恢复；可以重复调用）。AllOutPatches 在战斗结束 / 新战斗开始时也会调。</summary>
	public static void RestoreTime()
	{
		if (!_slowed) return;
		_slowed = false;
		Engine.TimeScale = _savedScale > 0.01 ? _savedScale : 1.0;
	}

	/// <summary>本场已播总攻击（同一场不重复）。</summary>
	public static bool Played;

	// ═══════════════════════════ 剪影截取 ═══════════════════════════

	internal const int SnapFrameLimit = 8;

	/// <summary>
	/// 截一只怪物：在 host 下面开离屏画面（region = 屏幕坐标，最长边 768 像素），fill 往给的节点里放复制品（变换 = 它在屏幕上的变换），
	/// 渲染两三帧后取图，回调 done（截不到 / 全透明时 null）。
	/// </summary>
	public static void Snap(Node host, Rect2 region, Func<Node2D, bool> fill, Action<Foe?> done)
	{
		// A flat silhouette does not need a supersampled readback.
		float k = Mathf.Min(1f, 768f / Mathf.Max(region.Size.X, region.Size.Y));
		Vector2I size = new(Mathf.Max(8, Mathf.CeilToInt(region.Size.X * k)), Mathf.Max(8, Mathf.CeilToInt(region.Size.Y * k)));
		SubViewport vp = new() { Size = size, TransparentBg = true, Disable3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled, Name = "P5AllOutSnap" };
		host.AddChild(vp);
		Node2D off = new() { Scale = Vector2.One * k, Position = -region.Position * k };
		vp.AddChild(off);
		bool ok = false;
		Stage.Safe("allout snap fill", () => ok = fill(off));
		if (!ok)
		{
			vp.QueueFree();
			GD.Print("[P5Joker] allout snap: body unavailable");
			done(null);
			return;
		}

		// Wait for the rig to process and for the viewport to actually render.
		// Once could consume its only draw before the Spine mesh was ready.
		SceneTree tree = host.GetTree();
		int frames = 0, draws = 0, readbacks = 0;
		void Drawn() => draws++;
		void Step()
		{
			if (!GodotObject.IsInstanceValid(vp))
			{
				Finish(null);
				return;
			}
			if (++frames == 1)
			{
				vp.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
				return;
			}
			if (draws < 2 && frames < SnapFrameLimit) return;
			Foe? foe = null;
			if (draws >= 2)
			{
				readbacks++;
				draws = 0;
				Stage.Safe("allout snap grab", () =>
				{
					using Image img = vp.GetTexture().GetImage();
					if (img == null || img.IsEmpty()) return;
					img.Convert(Image.Format.Rgba8);
					var candidate = new Foe { Rect = region };
					if (!FindWounds(candidate, img)) return;
					candidate.Tex = ImageTexture.CreateFromImage(img);
					foe = candidate;
				});
			}
			if (foe != null || readbacks >= 2 || frames >= SnapFrameLimit) Finish(foe);
		}
		void Finish(Foe? foe)
		{
			tree.ProcessFrame -= Step;
			RenderingServer.FramePostDraw -= Drawn;
			if (GodotObject.IsInstanceValid(vp)) vp.QueueFree();
			GD.Print($"[P5Joker] allout snap: {(foe != null ? "captured" : "empty")} {size.X}x{size.Y}, {readbacks} readback(s)");
			done(foe);
		}
		RenderingServer.FramePostDraw += Drawn;
		tree.ProcessFrame += Step;
	}

	/// <summary>在剪影上身挑 2～3 个伤口（不透明处，彼此隔开）；整张透明时 false。</summary>
	private static bool FindWounds(Foe f, Image img)
	{
		int w = img.GetWidth(), h = img.GetHeight();
		int x0 = w, x1 = -1, y0 = h, y1 = -1, step = Mathf.Max(1, Mathf.Min(w, h) / 96);
		byte[] rgba = img.GetData();
		byte AlphaAt(int x, int y) => rgba[(y * w + x) * 4 + 3];
		for (int y = 0; y < h; y += step)
		{
			for (int x = 0; x < w; x += step)
			{
				if (AlphaAt(x, y) > 127)
				{
					x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y);
				}
			}
		}
		if (x1 < 0 || (x1 - x0) * (y1 - y0) < 64) return false;
		f.Tight = new Rect2((float)x0 / w, (float)y0 / h, (float)(x1 - x0 + step) / w, (float)(y1 - y0 + step) / h);
		RandomNumberGenerator rng = new() { Seed = (ulong)(w * 7919 + h) };
		int want = (x1 - x0) * (y1 - y0) > w * h * 0.25f ? 2 : 1;
		float minGap = 0.22f * Mathf.Max(x1 - x0, y1 - y0);
		for (int tries = 0; tries < 200 && f.Wounds.Count < want; tries++)
		{
			float u = Mathf.Lerp(x0, x1, rng.RandfRange(0.25f, 0.75f));
			float v = Mathf.Lerp(y0, y1, rng.RandfRange(0.18f, 0.55f));
			if (AlphaAt((int)u, (int)v) < 204) continue;
			Vector2 p = new(u, v);
			bool far = true;
			foreach (Vector2 q in f.Wounds) far &= (q * new Vector2(w, h)).DistanceTo(p) > minGap;
			if (far) f.Wounds.Add(new Vector2(u / w, v / h));
		}
		if (f.Wounds.Count == 0) f.Wounds.Add(new Vector2((x0 + x1) * 0.5f / w, Mathf.Lerp(y0, y1, 0.35f) / h));
		return true;
	}

	// ═══════════════════════════ 播放 ═══════════════════════════

	private sealed class Shard
	{
		public Polygon2D Face = null!, Back = null!;
		public Line2D Edge = null!;
		public Vector2 Center, Dir;
		public float Speed, SpeedFactor, Spin, Delay, Grow;
		public Pane Pane = null!;
		public Vector2[] Local = Array.Empty<Vector2>();
	}

	private sealed class Jet
	{
		public int Foe;
		public Vector2 Wound, Dir;
		public float Speed, Width, Emit, Stick, Born;
		public Polygon2D Poly = null!;
		public readonly List<(Polygon2D Dot, Vector2 Dir, float Speed, float R, float Delay)> Drops = new();
	}

	private sealed class Splat
	{
		public Polygon2D Sp = null!;
		public Jet Jet = null!;
		public float Born, Size;
	}

	/// <summary>播总攻击。foes = 这一局的怪物剪影（可以为空）。host = 场景树里任意节点。</summary>
	public static Task Play(Node host, IReadOnlyList<Foe> foes)
	{
		Played = true;
		SceneTree tree = host.GetTree();
		Viewport main = host.GetViewport();
		Vector2 screen = main.GetVisibleRect().Size;
		float W = screen.X, H = screen.Y;
		RandomNumberGenerator rng = new() { Seed = 5u + (ulong)foes.Count * 131u };

		CanvasLayer layer = new() { Layer = 110, Name = "P5AllOut" };
		host.AddChild(layer);
		Control rootC = new() { Size = screen, MouseFilter = Control.MouseFilterEnum.Stop, Name = "P5AllOutRoot" };   // 挡住输入（揭幕时放开）
		layer.AddChild(rootC);
		AllOutFinale? fin = null;
		// Arrange cleanup before building the layers so partial setup cannot leave a full-screen overlay.
		tree.CreateTimer(Total + 0.05, true, false, true).Timeout += () =>
		{
			if (GodotObject.IsInstanceValid(layer)) layer.QueueFree();
			fin?.Free();
		};
		// 前半段不定格：游戏画面慢放（Engine.TimeScale），碎开那一刻恢复；万一中途被打断，节点离开场景树时 / 真实时间计时器到点时也恢复。
		// 上一场的延迟回调不能改下一场的速度。
		ulong playRun = ++_playRun;
		void RestoreThisRun()
		{
			if (playRun == _playRun) RestoreTime();
		}
		layer.TreeExiting += RestoreThisRun;
		tree.CreateTimer(ShatterAt + 0.6, true, false, true).Timeout += RestoreThisRun;
		SlowDown(SlowScale);

		// ── 前半段：直接盖在游戏画面上（读屏着色器压暗去色）+ 集中线 + 裂痕；碎开那一刻把整屏截下来当玻璃片的贴图 ──
		Control stageRoot = new() { Size = screen, MouseFilter = Control.MouseFilterEnum.Ignore };
		rootC.AddChild(stageRoot);
		Node2D paneLayer = new();                                               // 裂开的各块玻璃（读屏，AllOutGlass.cs）
		stageRoot.AddChild(paneLayer);
		Node2D lines = new();
		stageRoot.AddChild(lines);
		Node2D crackLayer = new();
		stageRoot.AddChild(crackLayer);
		Texture2D? coreTex = Stage.LoadTexture("aoa_crackp5_core");
		Sprite2D? coreSp = null;

		// ── 原版收尾 + 夹在中间的怪物剪影和血 ──
		Control finRoot = new() { Size = screen, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
		rootC.AddChild(finRoot);
		finRoot.AddChild(new ColorRect { Color = new Color(0.8f, 0.03f, 0.08f), Position = new Vector2(-160f, -160f), Size = screen + new Vector2(320f, 320f), MouseFilter = Control.MouseFilterEnum.Ignore });   // 红底（比屏幕大一圈：震的时候不露边）   // 原版红底（离屏画面第一帧到之前也是红的）
		Node2D mid = new();
		finRoot.AddChild(mid);
		void PrepareFinale()
		{
			fin = AllOutFinale.Create(rootC, screen);
			if (fin == null) return;
			finRoot.AddChild(fin.Bg);
			finRoot.MoveChild(fin.Bg, 1);
			finRoot.AddChild(fin.Star);
			finRoot.MoveChild(fin.Star, 2);
			finRoot.AddChild(fin.Front);
			finRoot.AddChild(fin.Text);
			fin.Bg.PivotOffset = fin.Text.PivotOffset = screen * 0.5f;
			fin.Front.PivotOffset = screen * new Vector2(0.62f, 0.2f);
			fin.Star.PivotOffset = screen * new Vector2(0.28f, 0.4f);
		}
		void PrepareFinaleViews(bool immediate)
		{
			if (fin == null) return;
			try
			{
				if (immediate) fin.CompletePreparation();
				else
				{
					fin.PrepareNextView();
					fin.WarmNextView();
				}
			}
			catch (Exception e)
			{
				Stage.Report("allout finale build", e);
				fin.Free();
				fin = null;
			}
		}
		Node2D shardLayer = new();
		rootC.AddChild(shardLayer);
		// 收尾：照搬 P3 mod 的收尾（用户 2026-11-18）—— 圆心在画面右上角外，两圈实心的波依次扫满全屏（前沿一道细亮边），
		// 盖满的那一刻第三圈「透明的波」从同一个圆心扩开（边缘一道亮边），露出下面的战场 / 结算；颜色换成 P5 的黑 → 红
		ColorRect cover = new() { Size = screen, MouseFilter = Control.MouseFilterEnum.Ignore, Material = new ShaderMaterial { Shader = RingsShader }, Visible = false };
		ShaderMaterial ringM = (ShaderMaterial)cover.Material;
		Vector2 ringC = new(W * 0.95f, -H * 0.45f);
		float ringCover = 0f;
		foreach (Vector2 corner in new[] { Vector2.Zero, new Vector2(W, 0f), new Vector2(0f, H), screen }) ringCover = Mathf.Max(ringCover, (corner - ringC).Length());
		ringM.SetShaderParameter("size", screen);
		ringM.SetShaderParameter("center", ringC);
		ringM.SetShaderParameter("hole_center", ringC);
		Color[] ringCols = new Color[12];
		ringCols[0] = new Color(0.8f, 0.03f, 0.08f);                               // 一层红色的波盖过来，再一层透明的波（用户 2026-11-18）
		ringM.SetShaderParameter("cols", ringCols);
		// 碎开那一下的白闪 + 撞击点的巨大白色四角星（盖在玻璃片上面）
		ColorRect flash = new() { Color = Colors.White, Size = screen, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
		rootC.AddChild(flash);
		Polygon2D burst = new() { Color = Colors.White, Antialiased = true, Polygon = StarPoly(1f, 0.08f), Visible = false };
		rootC.AddChild(burst);
		rootC.AddChild(cover);

		Rect2 union = foes.Count > 0 ? foes[0].Rect : new Rect2(W * 0.6f, H * 0.4f, 300f, 300f);
		foreach (Foe f in foes) union = union.Merge(f.Rect);
		// 怪物剪影在收尾里的位置：按战场上从左到右排成一排（按不透明部分算，相对大小不变、彼此略叠），最高的一只占画面高 0.42，
		// 脚底略出画面下沿，整排水平中心在 0.29 宽处（左边是竖排字、右边是 Joker）
		var order = new List<int>();
		for (int i = 0; i < foes.Count; i++) order.Add(i);
		order.Sort((a, b) => foes[a].Rect.GetCenter().X.CompareTo(foes[b].Rect.GetCenter().X));
		float maxH = 1f, sumW = 0f;
		foreach (Foe f in foes) maxH = Mathf.Max(maxH, f.Tight.Size.Y * f.Rect.Size.Y);
		float gk = H * 0.5f / maxH;
		foreach (Foe f in foes) sumW += f.Tight.Size.X * f.Rect.Size.X * gk * 0.92f;
		if (sumW > W * 0.6f) { gk *= W * 0.6f / sumW; sumW = W * 0.6f; }
		var finRects = new Rect2[foes.Count];
		float cx = W * 0.31f - sumW * 0.5f;
		foreach (int i in order)
		{
			Foe f = foes[i];
			Vector2 full = f.Rect.Size * gk;
			Vector2 tight = f.Tight.Size * full;
			float left = cx - (tight.X * 0.08f) * 0.5f;
			Vector2 pos = new(left - f.Tight.Position.X * full.X, H * 1.02f - (f.Tight.Position.Y + f.Tight.Size.Y) * full.Y);
			finRects[i] = new Rect2(pos, full);
			cx += tight.X * 0.92f;
		}
		// 剪影：照原版 —— 纯黑平涂，没有描边、没有明暗、没有投影（用户 2026-11-18）
		var finSils = new List<(Sprite2D Sp, Rect2 A, Rect2 B, float Rot)>();
		for (int i = 0; i < foes.Count; i++)
		{
			ShaderMaterial m = new() { Shader = SilShader };
			m.SetShaderParameter("fill", Ink);
			Sprite2D sp = new() { Texture = foes[i].Tex, Centered = false, Material = m, Visible = false };
			mid.AddChild(sp);
			finSils.Add((sp, foes[i].Rect, finRects[i], rng.RandfRange(-0.05f, 0.05f)));
		}
		Node2D bloodLayer = new();
		mid.AddChild(bloodLayer);
		// 喷血（照原版 P5R splatterBlood）：从剪影头上朝右上方陡陡地喷出一道持续的黑色血柱 —— 拉长的面片贴原版的滴落遮罩
		// （BKSK_SPLATTER 里的 be0cmn_upaura_uv_d），遮罩沿柱子不停往上滚，所以柱子是一道道软的、半透明的、一直在流动闪烁的黑色血痕；
		// 第一个伤口一道主柱，第二个伤口一道细一点、角度不同的副柱
		Texture2D? drip = Stage.LoadTexture("aoa_drip");
		var columns = new List<(Polygon2D P, ShaderMaterial M, int Foe, Vector2 Wound, float Fh, float Ang, float LenK, float WidK, float Speed, float Phase)>();
		for (int fi = 0; fi < foes.Count && drip != null; fi++)
		{
			float fh = finRects[fi].Size.Y;
			for (int wi = 0; wi < foes[fi].Wounds.Count && wi < 2; wi++)
			{
				bool primary = wi == 0;
				ShaderMaterial cm = new() { Shader = ColumnShader };
				cm.SetShaderParameter("drip", drip);
				cm.SetShaderParameter("ink", Ink);
				Polygon2D col = new() { Texture = drip, Material = cm, Visible = false, UV = new[] { Vector2.Zero, new Vector2(drip.GetWidth(), 0f), drip.GetSize(), new Vector2(0f, drip.GetHeight()) } };   // Polygon2D 的 UV 是贴图像素
				bloodLayer.AddChild(col);
				columns.Add((col, cm, fi, foes[fi].Wounds[wi], fh, primary ? rng.RandfRange(-74f, -62f) : rng.RandfRange(-110f, -92f),
					primary ? rng.RandfRange(1.15f, 1.4f) : rng.RandfRange(0.7f, 0.9f), primary ? 1f : 0.6f, rng.RandfRange(1.6f, 2.2f), rng.RandfRange(0f, 9f)));
			}
		}

		// 血：每个伤口 6～8 道血柱 + 末端血点 + P5R splatterBlood 的血团
		var jets = new List<Jet>();
		var splats = new List<Splat>();
		for (int fi = 0; fi < 0; fi++)                                         // （尖刺状的血柱 / 血花不要了，用户 2026-11-18）
		{
			Foe f = foes[fi];
			float fh = finSils[fi].B.Size.Y;
			foreach (Vector2 wd in f.Wounds)
			{
				int n = rng.RandiRange(3, 4);
				for (int j = 0; j < n; j++)
				{
					// 朝左上、正上为主（Joker 从右边打过来），少数往右上
					float ang = j < n - 1 ? rng.RandfRange(-172f, -62f) : rng.RandfRange(-60f, -28f);
					Jet jt = new()
					{
						Foe = fi, Wound = wd, Dir = Vector2.FromAngle(Mathf.DegToRad(ang)),
						Speed = fh * rng.RandfRange(1.4f, 2.4f), Width = fh * rng.RandfRange(0.03f, 0.05f),
						Emit = rng.RandfRange(0.12f, 0.2f), Stick = rng.RandfRange(0.2f, 0.3f), Born = ArtT + rng.RandfRange(0f, 0.04f),
						Poly = new Polygon2D { Color = Ink, Antialiased = true },
					};
					bloodLayer.AddChild(jt.Poly);
					int drops = rng.RandiRange(0, 2);
					for (int d = 0; d < drops; d++)
					{
						Polygon2D dot = new() { Color = Ink, Antialiased = true, Visible = false };
						bloodLayer.AddChild(dot);
						float da = ang + rng.RandfRange(-14f, 14f);
						jt.Drops.Add((dot, Vector2.FromAngle(Mathf.DegToRad(da)), jt.Speed * rng.RandfRange(1.05f, 1.35f),
							jt.Width * rng.RandfRange(0.18f, 0.4f), rng.RandfRange(0f, 0.05f)));
					}
					jets.Add(jt);
				}
				// 两道血柱落点溅开（带尖刺的血花，形状照原版碎星上的血花；落点 = 最先喷出那一份粘住的位置）
				{
					Jet jt = jets[jets.Count - 1];
					Polygon2D sp = new() { Color = Ink, Antialiased = true, Visible = false, Polygon = SplatPoly(rng), Rotation = rng.RandfRange(2.4f, 4.0f) };
					bloodLayer.AddChild(sp);
					splats.Add(new Splat { Sp = sp, Jet = jt, Born = ArtT, Size = fh * rng.RandfRange(0.1f, 0.14f) });
				}
			}
		}

		Vector2 focus = foes.Count > 0 ? union.GetCenter() : new Vector2(W * 0.66f, H * 0.45f);
		var shards = new List<Shard>();
		Glass gl = BuildGlass(paneLayer, crackLayer, screen, focus, rng);
		// 顿帧的集中线：从画面四周朝撞击点收进去的细白楔形（不到撞击点，留一圈空）
		var speed = new List<(Polygon2D P, float Ang, float Len, float W0, float Ph)>();
		for (int i = 0; i < 44; i++)
		{
			float ang = Mathf.Tau * i / 44f + rng.RandfRange(-0.05f, 0.05f);
			Polygon2D pl = new() { Color = new Color(1f, 1f, 1f, 0f), Antialiased = true };
			lines.AddChild(pl);
			speed.Add((pl, ang, rng.RandfRange(0.25f, 0.55f), rng.RandfRange(4f, 12f), rng.RandfRange(0f, 9f)));
		}

		// ── 声音（用户 2026-11-18：只要三样）：蓄势（P3 mod 同款，高潮正好在碎开）、碎开、怪物喷血（P5R 原版总攻击斩击） ──
		// 在时间轴里按真实时间触发（不用 Sfx 的延时：慢放会把延时也拖长）
		var cues = new List<(float At, string Name, float Vol)> { (ShatterAt - 0.65f, Sfx.AoaZoom, 1.9f), (CrackAt, Sfx.AoaCrack, 1.5f), (ShatterAt, Sfx.Shatter, 1.9f), (ArtT - 0.05f, Sfx.AoaSplatter, 2.0f) };
		var fired = new bool[cues.Count];

		bool broke = false, finRequested = false, prepDone = false, shardsVisible = false;
		// 撞击点的粉碎团：打中那一刻（冲击帧）就出现，0.1s 从小炸到原大，之后随裂纹蔓延再慢慢扩一点
		if (coreTex != null)
		{
			coreSp = new Sprite2D { Texture = coreTex, Position = focus, Rotation = rng.RandfRange(0f, Mathf.Tau), Scale = Vector2.Zero };
			crackLayer.AddChild(coreSp);
		}
		IEnumerator<Shard> prep = PrepareShards(gl, shardLayer, screen, rng).GetEnumerator();
		layer.TreeExiting += prep.Dispose;
		Tween clock = layer.CreateTween();
		clock.SetIgnoreTimeScale(true);
		clock.TweenMethod(Callable.From<float>(T => Stage.Safe("allout tick", () =>
		{
			if (!GodotObject.IsInstanceValid(layer)) return;
			for (int i = 0; i < cues.Count; i++)
			{
				if (!fired[i] && T >= cues[i].At) { fired[i] = true; Sfx.PlayLoud(cues[i].Name, cues[i].Vol); }
			}
			// 定格：裂纹从敌人那里炸开，画面往里推一点
			// 顿帧：最开头两帧是黑白反转的冲击帧，之后画面压暗、去色、四周收暗，撞击点附近色散，集中线一闪一闪
			if (!broke)
			{
				float imp = T < 0.08f ? 1f : 0f;
				float hold = Mathf.Clamp((T - 0.06f) / 0.2f, 0f, 1f);
				gl.Param("impact", imp);
				gl.Param("hold", hold);
				float rad = Mathf.Sqrt(W * W + H * H);
				foreach (var sp in speed)
				{
					float flick = 0.5f + 0.5f * Mathf.Sin(T * 40f + sp.Ph * 3f);
					float a = hold * (0.18f + 0.3f * flick) * (broke ? 0f : 1f);
					Vector2 dir = Vector2.FromAngle(sp.Ang), nrm = dir.Orthogonal();
					Vector2 outer = focus + dir * rad, inner = focus + dir * rad * (1f - sp.Len * (0.85f + 0.15f * flick));
					sp.P.Polygon = new[] { outer + nrm * sp.W0, outer - nrm * sp.W0, inner };
					sp.P.Color = new Color(1f, 1f, 1f, a);
				}
				if (coreSp != null)
				{
					float ck = Quint(Mathf.Clamp(T / 0.1f, 0f, 1f)) * (1f + 0.25f * Mathf.Clamp((T - CrackAt) / 0.6f, 0f, 1f));
					foreach (float beat in new[] { ShatterAt - 0.17f, ShatterAt - 0.07f })   // 碎开前两下心跳似的鼓胀
					{
						float bt2 = (T - beat) / 0.07f;
						if (bt2 > 0f && bt2 < 1f) ck *= 1f + 0.22f * Mathf.Sin(bt2 * Mathf.Pi);
					}
					coreSp.Scale = Vector2.One * 0.62f * ck;
					coreSp.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(T / 0.04f, 0f, 1f));
				}
				// 裂开的过程中慢慢往撞击点推近，碎开前一拍微微发抖
				// （用户 2026-11-18：前半段不要放大游戏画面）碎开前 0.18s 微微发抖
				float tremble = T < ShatterAt ? 6f * Mathf.Pow(Mathf.Clamp((T - (ShatterAt - 0.3f)) / 0.3f, 0f, 1f), 1.5f) : 0f;
				UpdateGlass(gl, T, screen, tremble);
				if (T < ShatterAt)
				{
					if (!finRequested)
					{
						finRequested = true;
						PrepareFinale();
					}
					else PrepareFinaleViews(immediate: false);
					for (int i = 0; i < 9 && !prepDone; i++)
					{
						prepDone = !prep.MoveNext();
						if (!prepDone) shards.Add(prep.Current);
					}
				}
			}
			// 碎开：离屏画面定住，玻璃片飞散，下面是原版收尾
			if (T >= ShatterAt && !broke)
			{
				broke = true;
				if (!finRequested) PrepareFinale();
				PrepareFinaleViews(immediate: true);
				// 截下此刻的整屏（含压暗、裂痕）当玻璃片的贴图
				using Image? shot = DisplayServer.GetName() == "headless" ? null : main.GetTexture().GetImage();
				while (!prepDone)
				{
					prepDone = !prep.MoveNext();
					if (!prepDone) shards.Add(prep.Current);
				}
				shardsVisible = shot != null && !shot.IsEmpty();
				if (shardsVisible) ApplyShatterTexture(gl, shards, shot!, screen);
				stageRoot.Visible = false;
				RestoreThisRun();
				finRoot.Visible = true;
				if (shardsVisible)
					foreach (Shard sh in shards) sh.Face.Visible = sh.Back.Visible = sh.Edge.Visible = true;
			}
			if (broke && T < HoleAt)
			{
				float d = T - ShatterAt;
				float fa = 1f - Mathf.Clamp(d / 0.08f, 0f, 1f);
				flash.Visible = fa > 0f;
				flash.Color = new Color(1f, 0.82f, 0.82f, 0.55f * fa * fa);
				float bt = Mathf.Clamp(d / 0.11f, 0f, 1f);
				burst.Visible = bt < 1f;
				burst.Position = focus;
				burst.Rotation = 0.3f + 0.25f * bt;
				burst.Scale = new Vector2(1f, 0.55f) * Mathf.Lerp(H * 0.3f, H * 1.3f, Quint(bt));
				burst.Modulate = new Color(1f, 1f, 1f, 0.9f * (1f - bt));
				if (shardsVisible)
				{
					if (d >= ShardLife + 0.04f)
					{
						shardLayer.Visible = false;
						shardsVisible = false;
					}
					else foreach (Shard sh in shards)
					{
						float e = Mathf.Clamp((d - sh.Delay) / ShardLife, 0f, 1f);
						float fly = 1f - Mathf.Pow(1f - e, 4f);
						Vector2 off = sh.Dir * sh.Speed * fly + new Vector2(0f, 700f) * e * e;
						float sc = 1f + sh.Grow * fly;
						float a = 1f - Mathf.Clamp((e - 0.7f) / 0.3f, 0f, 1f);
						sh.Face.Position = sh.Edge.Position = sh.Center + off;
						sh.Back.Position = sh.Face.Position + new Vector2(7f, 9f) * sc * Mathf.Clamp(fly * 3f, 0f, 1f);
						sh.Face.Rotation = sh.Back.Rotation = sh.Edge.Rotation = sh.Pane.Rot + sh.Spin * fly;
						sh.Face.Scale = sh.Back.Scale = sh.Edge.Scale = Vector2.One * sc;
						sh.Face.Modulate = sh.Back.Modulate = new Color(1f, 1f, 1f, a);
						sh.Edge.Modulate = new Color(1f, 1f, 1f, a * Mathf.Clamp(fly * 4f, 0f, 1f));
					}
				}
				// 原版收尾的时间：入场 0.4s 按原速，之后放慢、正好在透明波扩开时走到原版的最后一帧 —— 一直在动，不会中途停住
				float stretch = (AllOutFinale.End - 0.02f - SrcStart - 0.4f) / (HoleAt + ClearSweep - ShatterAt - 0.4f);
				fin?.Seek(Mathf.Min(d < 0.4f ? SrcStart + d : SrcStart + 0.4f + (d - 0.4f) * stretch, AllOutFinale.End - 0.02f), textVisible: d >= TextIn);
				if (fin != null)
				{
					// 拆开入场（一层接一层）：红底从放大压回 → 碎星转着砸进来 → Joker 从右边冲进来（带一点回弹）→ 怪物剪影从左边砸进（ArtT）
					// → 竖排字从下往上划上来；Joker 落定那一下震屏
					float qb = Quint(Mathf.Clamp(d / 0.3f, 0f, 1f));
					fin.Bg.Scale = Vector2.One * (Mathf.Lerp(1.15f, 1f, qb) + Drift * 0.6f * d);   // 之后一直慢慢推近（只放大，不露边）
					// 碎星：不转（整层是全屏的，一转就露出四角）、不回弹，0.24s 利落地从 1.35 倍压到原大
					float ks = Mathf.Clamp((d - 0.02f) / 0.24f, 0f, 1f);
					fin.Star.Visible = d >= 0.02f;
					fin.Star.Rotation = 0f;
					fin.Star.Scale = Vector2.One * (Mathf.Lerp(1.35f, 1f, Quint(ks)) + Drift * 1.3f * d);   // 碎星推得比底板快，有纵深
					fin.Star.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(ks * 6f, 0f, 1f));
					// Joker：碎开那一刻就在玻璃后面，从 1.4 倍、以脸为支点 0.12s 砸到略小于原大（0.97），再 0.3s 弹回原大 —— 砸进来的那一下要狠；
					// 同时从右边 0.1 屏宽冲到位；第一帧整个人白闪一下（加色），压暗只留 0.08s 就亮起；之后一直极慢地漂
					// 不缩到原大以下（人物这层一直铺到屏幕右边和下边，缩了就露边）：1.4 倍 0.16s 砸到原大，之后极慢地往前推（放大）
					float slam = Mathf.Clamp(d / 0.16f, 0f, 1f);
					float js = Mathf.Lerp(1.4f, 1f, Quint(slam)) + Drift * d;                    // 人物一直往前推
					fin.Front.Scale = Vector2.One * js;
					fin.Front.Position = new Vector2(W * 0.1f * (1f - Quint(slam)) - 9f * d, 0f);      // 同时慢慢往左漂（放大量盖得住，不露右边）
					float lit = Mathf.Clamp((d - 0.04f) / 0.1f, 0f, 1f);
					float white = d < 0.05f ? 1f - d / 0.05f : 0f;
					fin.Front.Modulate = new Color(Mathf.Lerp(0.35f, 1f, lit) + white, Mathf.Lerp(0.15f, 1f, lit) + white, Mathf.Lerp(0.2f, 1f, lit) + white, Mathf.Clamp(d / 0.04f, 0f, 1f));
					float kt = Mathf.Clamp((d - TextIn) / TextLen, 0f, 1f);
					fin.Text.Visible = d >= TextIn;
					fin.Text.Position = new Vector2(0f, H * 0.9f * (1f - Ease(kt) * (2f - Ease(kt)) * 0.5f - Ease(kt) * 0.5f));
					fin.Text.Scale = Vector2.One * (1f + Drift * 0.5f * Mathf.Max(0f, d - TextIn));
					float hitT = T - ArtT;
					float jl = d - 0.13f;                                                     // Joker 砸到位那一下震得最狠
					float sh = jl > 0f && jl < 0.28f ? (1f - jl / 0.28f) * 16f : (d < 0.13f ? 6f : 0f);
					if (hitT > 0f && hitT < 0.26f) sh = Mathf.Max(sh, (1f - hitT / 0.26f) * 12f);
					finRoot.PivotOffset = screen * 0.5f;
					// 穿过玻璃：整张卡从 1.12 倍压回（和玻璃片朝镜头飞散同时），震的时候再略放大一点，边缘不露底
					float through = Mathf.Lerp(1.12f, 1f, Quint(Mathf.Clamp(d / 0.5f, 0f, 1f)));
					finRoot.Scale = Vector2.One * (through + sh * 0.003f);
					finRoot.Position = new Vector2(Mathf.Sin(d * 120f), Mathf.Cos(d * 95f)) * sh;
				}
				// 剪影：人物出现前一拍砸进收尾位置（从 1.25 倍缩回、带一点倾斜）；喷血时往后一顿
				float lt = Mathf.Clamp((T - (ArtT - SlamLen)) / SlamLen, 0f, 1f), le = Quint(lt);
				for (int i = 0; i < finSils.Count; i++)
				{
					var (sp, A, B, rot) = finSils[i];
					sp.Visible = lt > 0f;
					float rc = Mathf.Clamp((T - ArtT) / 0.2f, 0f, 1f);
					float recoil = T >= ArtT ? Mathf.Sin(rc * Mathf.Pi) * (1f - rc * 0.5f) : 0f;
					float sc = Mathf.Lerp(1.25f, 1f, le) * (1f + 0.035f * recoil) * (1f + Drift * 0.8f * Mathf.Max(0f, T - ArtT));   // 剪影也一直慢慢放大
					Vector2 piv = B.Position + B.Size * new Vector2(0.5f, 1f);
					sp.Scale = B.Size / sp.Texture.GetSize() * sc;
					sp.Rotation = rot * (1f - le) - 0.025f * recoil;
					sp.Position = piv + new Vector2(-B.Size.X * 0.5f * sc, -B.Size.Y * sc).Rotated(sp.Rotation) + new Vector2(-16f * recoil - W * 0.22f * (1f - le) + 7f * Mathf.Max(0f, T - ArtT), 0f);   // 往右慢慢逼近（和 Joker 相向）
					sp.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(lt * 2.5f, 0f, 1f));
				}
				// 血
				foreach (Jet jt in jets) DrawJet(jt, T, finSils[jt.Foe].Sp);
				foreach (var c in columns)
				{
					// 0.1s 冲到全长，之后长短、角度轻轻起伏；退场前一直在喷
					float since = T - ArtT;
					c.P.Visible = since > 0f && T < HoleAt;
					if (!c.P.Visible) continue;
					Sprite2D owner = finSils[c.Foe].Sp;
					Vector2 o = owner.Transform * (c.Wound * owner.Texture.GetSize());
					float grow = Quint(Mathf.Clamp(since / 0.1f, 0f, 1f));
					float L = c.Fh * c.LenK * grow * (1f + 0.08f * Mathf.Sin(since * 9f + c.Phase) + 0.05f * Mathf.Sin(since * 23f + c.Phase * 2f));
					float ang = Mathf.DegToRad(c.Ang + 3f * Mathf.Sin(since * 5f + c.Phase));
					Vector2 dir = Vector2.FromAngle(ang), nrm = dir.Orthogonal();
					float w0 = c.Fh * 0.1f * c.WidK, w1 = c.Fh * 0.28f * c.WidK;      // 根部窄、往上散开
					Vector2 tip = o + dir * L;
					c.P.Polygon = new[] { o - nrm * w0 * 0.5f, o + nrm * w0 * 0.5f, tip + nrm * w1 * 0.5f, tip - nrm * w1 * 0.5f };
					c.M.SetShaderParameter("scroll", since * c.Speed);
					c.M.SetShaderParameter("alpha", Mathf.Clamp(since / 0.05f, 0f, 1f) * (since < 0.25f ? 1f : 0.85f + 0.15f * Mathf.Sin(since * 17f + c.Phase)));
				}
				foreach (Splat sl in splats)
				{
					float a2 = (T - sl.Born) / 0.08f;
					sl.Sp.Visible = a2 >= 0f;
					if (a2 < 0f) continue;
					Jet jt = sl.Jet;
					Sprite2D owner = finSils[jt.Foe].Sp;
					Vector2 o = owner.Transform * (jt.Wound * owner.Texture.GetSize());
					sl.Sp.Position = o;                                      // 伤口处炸开的血花
					float pop = a2 < 1f ? Quint(a2) * 1.2f : 1.2f - 0.2f * Mathf.Clamp((a2 - 1f) * 1.2f, 0f, 1f);
					sl.Sp.Scale = Vector2.One * sl.Size * pop;
				}
			}
			// 收尾（P3 mod 同款）：两圈实心波（黑 → 红）依次扫满 → 第三圈透明波露出结算
			if (T >= ExitAt)
			{
				cover.Visible = true;
				float time = T - ExitAt;
				float[] radii = new float[12];
				float[] rimR = new float[4], rimA = new float[4];
				int live = 0;
				float[] born = { 0f };
				for (int i = 0; i < 1; i++)
				{
					float u = (time - born[i]) / CoverSweep;
					if (u <= 0f) break;
					radii[i] = ringCover * 1.04f * Ease(Mathf.Min(u, 1f));
					live = i + 1;
					rimR[i] = radii[i];
					rimA[i] = 0.45f * (1f - Mathf.Clamp(u / 0.75f, 0f, 1f));
				}
				ringM.SetShaderParameter("radii", radii);
				ringM.SetShaderParameter("count", live);
				ringM.SetShaderParameter("rim_r", rimR);
				ringM.SetShaderParameter("rim_a", rimA);
				float c = (T - HoleAt) / ClearSweep;
				ringM.SetShaderParameter("hole", c > 0f ? ringCover * 1.06f * Ease(Mathf.Min(c, 1f)) + 1f : 0f);
				if (T >= HoleAt && finRoot.Visible)
				{
					finRoot.Visible = false;
					fin?.Free();
				}
			}
		})), 0f, Total, Total);
		var revealed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		void Reveal()
		{
			if (revealed.Task.IsCompleted) return;
			if (GodotObject.IsInstanceValid(rootC))
				rootC.MouseBehaviorRecursive = Control.MouseBehaviorRecursiveEnum.Disabled;
			revealed.TrySetResult();
		}
		layer.TreeExiting += Reveal;
		tree.CreateTimer(Hold, ignoreTimeScale: true).Timeout += Reveal;
		return revealed.Task;
	}

	private const int SprayBundles = 5, SprayStrands = 6, SpraySlots = SprayBundles * SprayStrands;
	private const float SprayCycle = 0.32f;

	/// <summary>
	/// 持续喷血（照原版：一束束细长的黑色血丝朝右上方猛地射出去，像喷枪喷出的一道道斜线）：
	/// 每个伤口 SprayBundles 束、每束 SprayStrands 根血丝，同一束方向一致（±4°）、长短粗细不一、有深有浅；
	/// 一束 0.06s 射到最长，停一下，尾巴脱离伤口往前追、越来越细地散掉（共约 0.26s），每束每 SprayCycle 秒重新喷一次（每轮方向都变）。
	/// </summary>
	private static void DrawSpray(Polygon2D pl, Sprite2D owner, Vector2 wound, int slot, float fh, float T)
	{
		int bundle = (slot / SprayStrands) % SprayBundles, strand = slot % SprayStrands;
		int seedBase = slot / SprayStrands;                                // 同一束共用
		float start = ArtT + 0.12f + bundle * SprayCycle / SprayBundles;
		float since = T - start;
		if (since < 0f || T > HoleAt) { pl.Visible = false; return; }
		int k = (int)(since / SprayCycle);
		float age = since - k * SprayCycle;
		RandomNumberGenerator rb = new() { Seed = (ulong)(seedBase * 7919 + k * 104729 + 31) };
		RandomNumberGenerator rs = new() { Seed = (ulong)(slot * 6151 + k * 92821 + 7) };
		float density = Mathf.Lerp(1f, 0.6f, Mathf.Clamp((T - ArtT) / 2.4f, 0f, 1f));
		if (rb.Randf() > density) { pl.Visible = false; return; }
		const float Grow = 0.06f, HoldT = 0.06f, Fade = 0.14f;
		if (age > Grow + HoldT + Fade) { pl.Visible = false; return; }
		float baseAng = rb.RandfRange(-80f, -25f);                       // 朝右上（Joker 那边）为主
		float L0 = fh * rb.RandfRange(0.5f, 0.95f);
		float ang = Mathf.DegToRad(baseAng + rs.RandfRange(-4f, 4f));
		float L = L0 * rs.RandfRange(0.55f, 1.15f);
		float w = fh * rs.RandfRange(0.004f, 0.016f);
		float alpha = rs.RandfRange(0.55f, 1f);
		Vector2 o = owner.Transform * (wound * owner.Texture.GetSize()) + Vector2.FromAngle(ang).Orthogonal() * (strand - SprayStrands * 0.5f) * fh * 0.006f;
		Vector2 dir = Vector2.FromAngle(ang), n = dir.Orthogonal();
		float head = L * Quint(Mathf.Clamp(age / Grow, 0f, 1f));
		float tailK = Mathf.Clamp((age - Grow - HoldT) / Fade, 0f, 1f);
		float tail = L * 0.95f * tailK * tailK;
		head += L * 0.25f * tailK;                                          // 散掉时整根还在往前飞
		float thin = 1f - tailK;
		if (head - tail < 2f) { pl.Visible = false; return; }
		pl.Visible = true;
		pl.Color = new Color(Ink.R, Ink.G, Ink.B, alpha * Mathf.Lerp(1f, 0.4f, tailK));
		Vector2 a = o + dir * tail, b = o + dir * head;
		float wb = w * thin + 0.6f;
		pl.Polygon = new[] { a + n * wb * 0.5f, a + dir * (head - tail) * 0.35f + n * wb * 0.6f, b, a + dir * (head - tail) * 0.35f - n * wb * 0.6f, a - n * wb * 0.5f };
	}

	/// <summary>
	/// 一道血柱：连续喷出的血，第 e 秒喷出的那一份初速随压力降低，飞行 Stick 秒后粘在红底上不动 —— 整条最后定成一道从伤口甩出去的弧；
	/// 头上几颗血点飞得更远、粘住。
	/// </summary>
	private static void DrawJet(Jet jt, float T, Sprite2D owner)
	{
		float age = T - jt.Born;
		if (age <= 0f)
		{
			jt.Poly.Visible = false;
			return;
		}
		jt.Poly.Visible = true;
		Vector2 o = owner.Transform * (jt.Wound * owner.Texture.GetSize());
		Vector2 g = new(0f, jt.Speed * 2.2f);
		const int N = 18;
		float emitted = Mathf.Min(age, jt.Emit);
		var left = new Vector2[N];
		var right = new Vector2[N];
		for (int k = 0; k < N; k++)
		{
			float s = k / (float)(N - 1);
			float e = emitted * s;                                   // 0 = 最先喷出（头），emitted = 刚喷出（伤口处）
			float speed = jt.Speed * (1f - 0.55f * e / jt.Emit);
			float fl = Mathf.Min(age - e, jt.Stick);
			Vector2 p = o + jt.Dir * speed * fl + 0.5f * g * fl * fl;
			float fl2 = Mathf.Max(0f, fl - 0.012f);
			Vector2 p2 = o + jt.Dir * speed * fl2 + 0.5f * g * fl2 * fl2;
			Vector2 tan = (p - p2).LengthSquared() > 0.01f ? (p - p2).Normalized() : jt.Dir;
			Vector2 nrm = new(-tan.Y, tan.X);
			float wv = jt.Width * Mathf.Pow(s, 0.7f);                // 尖刺：头上是尖的，越靠伤口越粗
			left[k] = p + nrm * wv * 0.5f;
			right[N - 1 - k] = p - nrm * wv * 0.5f;
		}
		var poly = new Vector2[N * 2 - 1];
		int idx = 0;
		for (int k = 0; k < N; k++) poly[idx++] = left[N - 1 - k];         // 伤口 → 头（左边）
		for (int k = 1; k < N; k++) poly[idx++] = right[N - 1 - k];        // 头 → 伤口（右边）
		jt.Poly.Polygon = poly;
		foreach (var d in jt.Drops)
		{
			float fl = Mathf.Min(age - d.Delay, jt.Stick * 1.15f);
			d.Dot.Visible = fl > 0f;
			if (fl <= 0f) continue;
			Vector2 p = o + d.Dir * d.Speed * fl + 0.5f * g * fl * fl;
			Vector2 v = d.Dir * d.Speed + g * fl;
			float stretch = fl < jt.Stick * 1.15f ? 2.2f : 1.25f;          // 飞行中拉长，粘住后变圆一点
			d.Dot.Position = p;
			d.Dot.Rotation = v.Angle();
			d.Dot.Polygon = Ellipse(d.R * stretch, d.R, 12);
		}
	}

	private static Vector2[] Offset(Vector2[] pts, Vector2 by)
	{
		var o = new Vector2[pts.Length];
		for (int i = 0; i < pts.Length; i++) o[i] = pts[i] + by;
		return o;
	}


	/// <summary>血花：单位半径的不规则团 + 几根长短不一的尖刺（顺着 +x 方向的更长 = 血飞来的方向往前溅）。</summary>
	private static Vector2[] SplatPoly(RandomNumberGenerator rng)
	{
		const int N = 40;
		var p = new Vector2[N];
		float[] spikes = new float[N];
		for (int k = 0; k < 7; k++)
		{
			int at = rng.RandiRange(0, N - 1);
			float a = Mathf.Tau * at / N;
			spikes[at] = Mathf.Max(spikes[at], rng.RandfRange(0.5f, 1.3f) * (0.6f + 0.6f * Mathf.Max(0f, Mathf.Cos(a))));
		}
		float ph = rng.RandfRange(0f, 9f);
		for (int i = 0; i < N; i++)
		{
			float a = Mathf.Tau * i / N;
			float r = 0.62f + 0.12f * Mathf.Sin(a * 3f + ph) + 0.08f * Mathf.Sin(a * 7f + ph * 2f);
			// 尖刺：本身最长，两边各一个点收窄
			float sp = spikes[i] + 0.35f * Mathf.Max(spikes[(i + 1) % N], spikes[(i + N - 1) % N]) * 0f;
			if (spikes[i] > 0f) r += spikes[i];
			else if (spikes[(i + 1) % N] > 0f || spikes[(i + N - 1) % N] > 0f) r *= 0.8f;
			p[i] = Vector2.FromAngle(a) * r * new Vector2(1.25f, 1f);
		}
		return p;
	}

	private static Vector2[] StarPoly(float r, float inner = 0.22f)
	{
		var p = new Vector2[8];
		for (int i = 0; i < 8; i++) p[i] = Vector2.FromAngle(Mathf.Pi * i / 4f) * (i % 2 == 0 ? r : r * inner);
		return p;
	}

	private static Vector2[] Ellipse(float rx, float ry, int n)
	{
		var p = new Vector2[n];
		for (int i = 0; i < n; i++)
		{
			float a = Mathf.Tau * i / n;
			p[i] = new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
		}
		return p;
	}

	private static float Ease(float t) => 1f - Mathf.Pow(1f - t, 3f);

	private static float Quint(float t) => 1f - Mathf.Pow(1f - t, 5f);

	/// <summary>带回弹的缓出（超过终点一点再回来），k = 回弹量。</summary>
	private static float Back(float t, float k)
	{
		float c = 1.70158f * k, u = t - 1f;
		return 1f + (c + 1f) * u * u * u + c * u * u;
	}

	// ═══════════════════════════ 着色器 ═══════════════════════════

	private static Shader? _sil, _shade, _blob, _cover, _still, _crack, _crackGlow, _rings, _column;

	/// <summary>
	/// 顿帧的定格画面：impact = 冲击帧（黑白反转、高反差），hold = 顿住（压暗、去色 60%、四周收暗、撞击点附近一点红青色散）。
	/// </summary>
	private static Shader StillShader => _still ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D scr : hint_screen_texture, filter_linear;
uniform float impact = 0.0;
uniform float hold = 0.0;
uniform vec2 focus = vec2(0.6, 0.5);
uniform float aspect = 1.7778;
uniform vec2 shift = vec2(0.0);
void fragment() {
	vec2 uv = SCREEN_UV + shift;
	vec2 d = (uv - focus) * vec2(aspect, 1.0);
	float r = length(d);
	vec2 ca = (uv - focus) * 0.006 * hold * smoothstep(0.0, 0.5, r);
	vec3 c = vec3(texture(scr, uv + ca).r, texture(scr, uv).g, texture(scr, uv - ca).b);
	float l = dot(c, vec3(0.299, 0.587, 0.114));
	vec3 held = mix(c, vec3(l), 0.6) * mix(1.0, 0.62, hold);
	held *= 1.0 - hold * 0.55 * smoothstep(0.35, 1.2, r);
	vec3 o = mix(c, held, hold);
	float hi = smoothstep(0.32, 0.38, l);
	o = mix(o, vec3(1.0 - hi), impact);
	COLOR = vec4(o, 1.0);
}" };

	/// <summary>裂痕贴图逐步显出：UV 离中心的距离（0–0.5 = 贴图半宽）小于 reach 的部分显示，前沿软一点、带一点亮。</summary>
	private static Shader CrackShader => _crack ??= new Shader { Code = @"
shader_type canvas_item;
uniform float reach = 0.0;
uniform float glow = 0.0;
void fragment() {
	vec4 t = texture(TEXTURE, UV);
	float d = length(UV - vec2(0.5));
	float m = 1.0 - smoothstep(reach - 0.015, reach, d);
	COLOR = vec4(mix(t.rgb, vec3(1.0), 0.0), t.a * m);
}" };

	/// <summary>裂纹后面渗出来的红光：同一张裂痕贴图，周围 8 个方向取样做模糊，加色叠上去。</summary>
	private static Shader CrackGlowShader => _crackGlow ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform float reach = 0.0;
uniform float amount = 0.0;
void fragment() {
	float a = 0.0;
	for (int i = 0; i < 8; i++) {
		float t = 6.2831853 * float(i) / 8.0;
		a += texture(TEXTURE, UV + vec2(cos(t), sin(t)) * TEXTURE_PIXEL_SIZE * 5.0).a;
		a += texture(TEXTURE, UV + vec2(cos(t), sin(t)) * TEXTURE_PIXEL_SIZE * 11.0).a * 0.6;
	}
	a /= 12.8;
	float d = length(UV - vec2(0.5));
	float m = 1.0 - smoothstep(reach - 0.02, reach, d);
	float core = 1.0 - smoothstep(0.0, 0.08, d);
	COLOR = vec4(vec3(1.0, 0.08, 0.12) * (a * 2.2 + core * 0.6) * m * amount, 1.0);
}" };

	/// <summary>P3 mod 收尾的同心圆着色器（mod/src/Vfx/AllOut.cs 的 Rings，亮边颜色换成偏暖的白）。</summary>
	private static Shader RingsShader => _rings ??= new Shader { Code = @"
shader_type canvas_item;
uniform vec2 size;
uniform vec2 center;
uniform int count = 0;
uniform float radii[12];
uniform vec4 cols[12];
uniform float rim_r[4];
uniform float rim_a[4];
uniform vec2 hole_center;
uniform float hole = 0.0;
void fragment() {
	vec2 p = UV * size;
	float d = distance(p, center);
	float aa = max(fwidth(d), 0.75);
	vec4 c = vec4(0.0);
	for (int i = 0; i < 12; i++) {
		if (i >= count) break;
		float cov = clamp((radii[i] - d) / aa + 0.5, 0.0, 1.0);
		c.rgb = mix(c.rgb, cols[i].rgb, c.a > 0.0 ? cov : 1.0);
		c.a = max(c.a, cov);
	}
	for (int j = 0; j < 4; j++) {
		float k = rim_a[j] * (1.0 - smoothstep(0.0, 2.5, abs(d - rim_r[j])));
		c.rgb = mix(c.rgb, vec3(1.0, 0.92, 0.92), k);
	}
	float dh = distance(p, hole_center);
	float edge = hole > 0.0 ? (1.0 - smoothstep(0.0, 7.0, abs(dh - hole))) : 0.0;
	c.rgb = mix(c.rgb, vec3(1.0, 0.94, 0.94), edge * 0.8);
	c.a *= hole > 0.0 ? clamp((dh - hole) / aa + 0.5, 0.0, 1.0) : 1.0;
	COLOR = c;
}" };

	/// <summary>
	/// 血柱：UV.x 横穿柱子（0–1）、UV.y 从根部（0）到顶端（1）。原版滴落遮罩（alpha）两层不同速度沿柱子往上滚，叠出一道道流动的血痕；
	/// 两侧和顶端软收、根部实，整体黑色半透明。
	/// </summary>
	private static Shader ColumnShader => _column ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D drip : repeat_enable, filter_linear;
uniform vec4 ink : source_color = vec4(0.015, 0.008, 0.012, 1.0);
uniform float scroll = 0.0;
uniform float alpha = 1.0;
void fragment() {
	float x = UV.x, y = UV.y;
	float a1 = texture(drip, vec2(x * 0.9 + 0.1, y * 1.3 - scroll)).a;
	float a2 = texture(drip, vec2(x * 0.6 + 0.47, y * 0.9 - scroll * 0.63)).a;
	float a3 = texture(drip, vec2(x * 1.4 + 0.2, y * 2.1 - scroll * 1.4)).a;
	float m = clamp(a1 + a2 * 0.8 + a3 * 0.6, 0.0, 1.0);
	float core = 1.0 - smoothstep(0.45, 1.0, abs(x - 0.5) * 2.0);                       // 中间实、两侧软
	float along = smoothstep(1.0, 0.55, y);
	float root = (1.0 - smoothstep(0.05, 0.3, y)) * (1.0 - smoothstep(0.3, 0.9, abs(x - 0.5) * 2.0));   // 根部一团实的
	float a = max(m * core * along, root);
	COLOR = vec4(ink.rgb, clamp(a, 0.0, 1.0) * alpha);
}" };

	/// <summary>纯色剪影（投影用）。</summary>
	private static Shader SilShader => _sil ??= new Shader { Code = @"
shader_type canvas_item;
uniform vec4 fill : source_color = vec4(0.0, 0.0, 0.0, 1.0);
void fragment() {
	float a = smoothstep(0.3, 0.6, texture(TEXTURE, UV).a);
	COLOR = vec4(fill.rgb, a * fill.a);
}" };

	/// <summary>
	/// 有明暗的剪影：怪物原来的亮度压成炭黑的几档（线条、暗部最黑，亮部深灰），亮部叠暗红网点；
	/// 朝 light（右上，Joker 那边）的边缘一道红色轮廓光，最外一线粉白。
	/// </summary>
	private static Shader ShadeShader => _shade ??= new Shader { Code = @"
shader_type canvas_item;
uniform float rim_px = 7.0;
uniform vec2 light = vec2(0.8, -0.6);
void fragment() {
	vec4 t = texture(TEXTURE, UV);
	float body = smoothstep(0.3, 0.6, t.a);
	float l = dot(t.rgb, vec3(0.299, 0.587, 0.114));
	float tone = floor(smoothstep(0.08, 0.85, l) * 3.0 + 0.5) / 3.0;          // 三档
	vec3 col = mix(vec3(0.012, 0.008, 0.012), vec3(0.2, 0.17, 0.19), tone);
	vec2 px = UV / TEXTURE_PIXEL_SIZE;
	vec2 g = fract(px / 9.0) - 0.5;
	float r = 0.45 * smoothstep(0.3, 0.95, l);
	float dots = 1.0 - smoothstep(r - 0.06, r + 0.06, length(g));
	col = mix(col, vec3(0.42, 0.04, 0.08), dots * 0.75);
	vec2 dir = normalize(light) * TEXTURE_PIXEL_SIZE;
	float rim = body * (1.0 - smoothstep(0.3, 0.6, texture(TEXTURE, UV + dir * rim_px).a));
	float edge = body * (1.0 - smoothstep(0.3, 0.6, texture(TEXTURE, UV + dir * rim_px * 0.3).a));
	col = mix(col, vec3(0.9, 0.04, 0.1), rim);
	col = mix(col, vec3(1.0, 0.88, 0.88), edge * 0.9);
	COLOR = vec4(col, body);
}" };

	/// <summary>P5R splatterBlood 的血团（smoke.dds 的 alpha）：阈值收边成干净的墨团。</summary>
	private static Shader BlobShader => _blob ??= new Shader { Code = @"
shader_type canvas_item;
uniform vec4 tint : source_color = vec4(0.0, 0.0, 0.0, 1.0);
void fragment() {
	float a = texture(TEXTURE, UV).a;
	float e = fwidth(a) * 1.2 + 0.02;
	COLOR = vec4(tint.rgb, smoothstep(0.5 - e, 0.5 + e, a) * tint.a);
}" };

	/// <summary>收尾：sweep 0→1 黑墨从右往左盖满（噪声撕边 + 红边）；hole 0→1 中间挖开红边的洞。</summary>
	private static Shader CoverShader => _cover ??= new Shader { Code = @"
shader_type canvas_item;
uniform vec2 size = vec2(1920.0, 1080.0);
uniform sampler2D noise : repeat_enable, filter_linear;
uniform float sweep = 0.0;
uniform float hole = 0.0;
void fragment() {
	vec2 p = UV * size;
	float n = texture(noise, p / 520.0).r - 0.5;
	float n2 = texture(noise, p / 170.0 + vec2(0.37, 0.11)).r - 0.5;
	float nn = n * 0.8 + n2 * 0.35;
	// 前沿：从右边进来（略斜）
	float front = mix(size.x * 1.25, -size.x * 0.3, sweep) + (p.y - size.y * 0.5) * 0.25 + nn * 260.0;
	float ink = smoothstep(front - 2.0, front + 2.0, p.x);
	float rim = smoothstep(front - 34.0, front - 30.0, p.x) * (1.0 - ink);
	vec3 col = vec3(0.015, 0.008, 0.012);
	float a = ink;
	col = mix(col, vec3(0.86, 0.02, 0.09), rim * (1.0 - ink));
	a = max(a, rim);
	// 洞
	float d = length((p - size * 0.5) / size.y) + nn * 0.12;
	float r = hole * 1.25 - 0.05;
	float inside = 1.0 - smoothstep(r - 0.004, r + 0.004, d);
	float hr = smoothstep(r - 0.004, r, d) * (1.0 - smoothstep(r + 0.03, r + 0.034, d));
	col = mix(col, vec3(0.86, 0.02, 0.09), hr * step(0.001, hole));
	a *= 1.0 - inside;
	COLOR = vec4(col, a);
}" };
}
