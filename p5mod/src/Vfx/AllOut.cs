using System;
using System.Collections.Generic;
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
public static class AllOut
{
	/// <summary>一只怪物的剪影：截下来的贴图（透明底）+ 它在屏幕上的位置（贴图按 Rect 大小摆放）。</summary>
	public sealed class Foe
	{
		public Texture2D Tex = null!;
		public Image Img = null!;
		public Rect2 Rect;
		public List<Vector2> Wounds = new();      // 贴图 UV（0–1）里的伤口
		public Rect2 Tight = new(0f, 0f, 1f, 1f);  // 不透明部分在贴图 UV 里的范围
	}

	// ── 时间轴（秒） ──
	// 节奏照 P3 mod（最后一击 → 约 0.6s 蓄势 → 立绘）：定格顿 0.15s，裂纹 0.3s 内看得见地长满，停一拍、往里推；0.55 碎开
	private const float CrackAt = 0.15f, CrackGrow = 0.3f, ShatterAt = 0.55f, ShardLife = 0.45f, SlamLen = 0.12f;
	// 碎开那一刻原版收尾已经在玻璃后面（从源时间 ArtAt 前一点开始放，碎星的血花照原版炸开），三层各自切入：
	// 背景从放大缩回、Joker 从右边快速滑进、竖排字从左边进、怪物剪影从左边砸进；剪影落定（ArtT）时喷血
	// 碎开那一刻立绘卡硬切进来（在玻璃片后面，没有空红底），白闪 + 撞击点一颗巨大的白色四角星，整张卡从放大压回来、震屏；
	// 怪物剪影同一拍砸进左下，紧接着喷血
	private const float SrcStart = AllOutFinale.ArtAt - 0.03f, Settle = 0.32f;
	// 入场分拍（相对碎开）：0 碎开 + 红底压回（冲击）→ 0.02–0.4 碎星转进来（丝滑，带一点回弹）→ 0.14–0.38 Joker 冲进来、落定震屏（冲击）
	// → 0.42 怪物剪影砸进 + 喷血 + 震屏（第二下冲击）→ 0.5–0.8 竖排字划上来（丝滑）
	private const float JokerIn = 0.14f, JokerLen = 0.24f, TextIn = 0.5f, TextLen = 0.3f;
	private const float ArtT = ShatterAt + 0.42f;
	private const float ExitAt = ShatterAt + 2.2f, CoverLen = 0.28f, HoleAt = ExitAt + CoverLen + 0.02f, HoleLen = 0.4f;
	public const float Total = HoleAt + HoleLen + 0.05f;
	/// <summary>结算推迟多久：洞开到一半左右时结算页出现在洞里。</summary>
	public const float Hold = HoleAt + 0.14f;

	private static readonly Color Red = new(0.86f, 0.02f, 0.09f);
	private static readonly Color Ink = new(0.015f, 0.008f, 0.012f);

	/// <summary>本场已播总攻击（同一场不重复）。</summary>
	public static bool Played;

	/// <summary>开始播的时刻（毫秒）：战斗结束钩子据此只推迟剩下的时间。</summary>
	public static ulong StartedMs;

	// ═══════════════════════════ 剪影截取 ═══════════════════════════

	/// <summary>
	/// 截一只怪物：在 host 下面开离屏画面（region = 屏幕坐标，1.5 倍分辨率），fill 往给的节点里放复制品（变换 = 它在屏幕上的变换），
	/// 渲染两三帧后取图，回调 done（截不到 / 全透明时 null）。
	/// </summary>
	public static void Snap(Node host, Rect2 region, Func<Node2D, bool> fill, Action<Foe?> done)
	{
		const float K = 1.5f;
		float k = Mathf.Min(K, 2048f / Mathf.Max(region.Size.X, region.Size.Y));
		Vector2I size = new(Mathf.Max(8, Mathf.CeilToInt(region.Size.X * k)), Mathf.Max(8, Mathf.CeilToInt(region.Size.Y * k)));
		SubViewport vp = new() { Size = size, TransparentBg = true, Disable3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always, Name = "P5AllOutSnap" };
		host.AddChild(vp);
		Node2D off = new() { Scale = Vector2.One * k, Position = -region.Position * k };
		vp.AddChild(off);
		bool ok = false;
		Stage.Safe("allout snap fill", () => ok = fill(off));
		if (!ok)
		{
			vp.QueueFree();
			done(null);
			return;
		}
		SceneTree tree = host.GetTree();
		int frames = 0;
		void Step()
		{
			if (++frames < 3) return;
			tree.ProcessFrame -= Step;
			Foe? foe = null;
			Stage.Safe("allout snap grab", () =>
			{
				if (!GodotObject.IsInstanceValid(vp)) return;
				Image img = vp.GetTexture().GetImage();
				if (img == null || img.IsEmpty()) return;
				img.Convert(Image.Format.Rgba8);
				foe = new Foe { Img = img, Tex = ImageTexture.CreateFromImage(img), Rect = region };
				if (!FindWounds(foe)) foe = null;
			});
			if (GodotObject.IsInstanceValid(vp)) vp.QueueFree();
			done(foe);
		}
		tree.ProcessFrame += Step;
	}

	/// <summary>在剪影上身挑 2～3 个伤口（不透明处，彼此隔开）；整张透明时 false。</summary>
	private static bool FindWounds(Foe f)
	{
		Image img = f.Img;
		int w = img.GetWidth(), h = img.GetHeight();
		int x0 = w, x1 = -1, y0 = h, y1 = -1, step = Mathf.Max(1, Mathf.Min(w, h) / 96);
		for (int y = 0; y < h; y += step)
		{
			for (int x = 0; x < w; x += step)
			{
				if (img.GetPixel(x, y).A > 0.5f)
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
			if (img.GetPixel((int)u, (int)v).A < 0.8f) continue;
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
		public float Speed, Spin, Delay, Grow;
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
	public static void Play(Node host, IReadOnlyList<Foe> foes)
	{
		Played = true;
		StartedMs = Time.GetTicksMsec();
		SceneTree tree = host.GetTree();
		Viewport main = host.GetViewport();
		Vector2 screen = main.GetVisibleRect().Size;
		float W = screen.X, H = screen.Y;
		RandomNumberGenerator rng = new() { Seed = 5u + (ulong)foes.Count * 131u };

		// 冻住此刻的画面
		Image shot = main.GetTexture().GetImage();
		ImageTexture frozen = ImageTexture.CreateFromImage(shot);

		CanvasLayer layer = new() { Layer = 110, Name = "P5AllOut" };
		tree.Root.AddChild(layer);
		Control rootC = new() { Size = screen, MouseFilter = Control.MouseFilterEnum.Stop, Name = "P5AllOutRoot" };   // 挡住输入
		layer.AddChild(rootC);

		// ── 定格：最后一击那一帧（原样，不调色）画进离屏画面，裂开时整张拿去当玻璃片的贴图 ──
		SubViewport stage = new() { Size = new Vector2I((int)W, (int)H), Disable3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always, Name = "P5AllOutStage" };
		rootC.AddChild(stage);
		ShaderMaterial still = new() { Shader = StillShader };
		stage.AddChild(new TextureRect { Texture = frozen, Size = screen, StretchMode = TextureRect.StretchModeEnum.Scale, Material = still });
		Node2D lines = new();
		stage.AddChild(lines);
		Node2D crackLayer = new();
		stage.AddChild(crackLayer);
		TextureRect stageView = new() { Texture = stage.GetTexture(), Size = screen, PivotOffset = screen * 0.5f, MouseFilter = Control.MouseFilterEnum.Ignore };

		// ── 原版收尾 + 夹在中间的怪物剪影和血 ──
		AllOutFinale? fin = AllOutFinale.Create(rootC, screen);
		Control finRoot = new() { Size = screen, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
		rootC.AddChild(finRoot);
		finRoot.AddChild(new ColorRect { Color = new Color(0.996f, 0f, 0f), Size = screen, MouseFilter = Control.MouseFilterEnum.Ignore });   // 原版红底（离屏画面第一帧到之前也是红的）
		if (fin != null)
		{
			finRoot.AddChild(fin.Bg);
			finRoot.AddChild(fin.Star);
			fin.Bg.PivotOffset = fin.Front.PivotOffset = fin.Text.PivotOffset = screen * 0.5f;
			fin.Star.PivotOffset = screen * new Vector2(0.28f, 0.4f);                 // 碎星的中心
		}
		Node2D mid = new();
		finRoot.AddChild(mid);
		if (fin != null)
		{
			finRoot.AddChild(fin.Front);
			finRoot.AddChild(fin.Text);
		}
		rootC.AddChild(stageView);
		Node2D shardLayer = new();
		rootC.AddChild(shardLayer);
		ColorRect cover = new() { Size = screen, MouseFilter = Control.MouseFilterEnum.Ignore, Material = new ShaderMaterial { Shader = CoverShader }, Visible = false };
		ShaderMaterial coverM = (ShaderMaterial)cover.Material;
		coverM.SetShaderParameter("size", screen);
		if (Stage.LoadTexture("aoa_noise") is Texture2D noise) coverM.SetShaderParameter("noise", noise);
		// 碎开那一下的白闪 + 撞击点的巨大白色四角星（盖在玻璃片上面）
		ColorRect flash = new() { Color = Colors.White, Size = screen, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
		rootC.AddChild(flash);
		Polygon2D burst = new() { Color = Colors.White, Antialiased = true, Polygon = StarPoly(1f), Visible = false };
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
		float gk = H * 0.56f / maxH;
		foreach (Foe f in foes) sumW += f.Tight.Size.X * f.Rect.Size.X * gk * 0.7f;
		if (sumW > W * 0.56f) { gk *= W * 0.56f / sumW; sumW = W * 0.56f; }
		var finRects = new Rect2[foes.Count];
		float cx = W * 0.31f - sumW * 0.5f;
		foreach (int i in order)
		{
			Foe f = foes[i];
			Vector2 full = f.Rect.Size * gk;
			Vector2 tight = f.Tight.Size * full;
			float left = cx - (tight.X * 0.22f) * 0.5f;
			Vector2 pos = new(left - f.Tight.Position.X * full.X, H * 1.02f - (f.Tight.Position.Y + f.Tight.Size.Y) * full.Y);
			finRects[i] = new Rect2(pos, full);
			cx += tight.X * 0.7f;
		}
		// 剪影：保留怪物身上的明暗和线条（压成炭黑的几档 + 亮部的暗红网点），朝 Joker 那边（右上）一道红色轮廓光、最外缘一线白；
		// 身后错开一层暗红的投影，和原版背景分开层次
		var finSils = new List<(Sprite2D Sp, Rect2 A, Rect2 B, float Rot)>();
		var shadows = new List<Sprite2D>();
		for (int i = 0; i < foes.Count; i++)
		{
			ShaderMaterial sm = new() { Shader = SilShader };
			sm.SetShaderParameter("fill", new Color(0.3f, 0f, 0.035f));
			Sprite2D sh = new() { Texture = foes[i].Tex, Centered = false, Material = sm, Visible = false };
			mid.AddChild(sh);
			shadows.Add(sh);
		}
		for (int i = 0; i < foes.Count; i++)
		{
			ShaderMaterial m = new() { Shader = ShadeShader };
			m.SetShaderParameter("rim_px", 7f);
			Sprite2D sp = new() { Texture = foes[i].Tex, Centered = false, Material = m, Visible = false };
			mid.AddChild(sp);
			finSils.Add((sp, foes[i].Rect, finRects[i], rng.RandfRange(-0.05f, 0.05f)));
		}
		Node2D bloodLayer = new();
		mid.AddChild(bloodLayer);

		// 血：每个伤口 6～8 道血柱 + 末端血点 + P5R splatterBlood 的血团
		var jets = new List<Jet>();
		var splats = new List<Splat>();
		for (int fi = 0; fi < foes.Count; fi++)
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
		var cracks = new List<Crack>();
		still.SetShaderParameter("focus", focus / screen);
		still.SetShaderParameter("aspect", W / H);
		// 顿帧的集中线：从画面四周朝撞击点收进去的细白楔形（不到撞击点，留一圈空）
		var speed = new List<(Polygon2D P, float Ang, float Len, float W0, float Ph)>();
		for (int i = 0; i < 44; i++)
		{
			float ang = Mathf.Tau * i / 44f + rng.RandfRange(-0.05f, 0.05f);
			Polygon2D pl = new() { Color = new Color(1f, 1f, 1f, 0f), Antialiased = true };
			lines.AddChild(pl);
			speed.Add((pl, ang, rng.RandfRange(0.25f, 0.55f), rng.RandfRange(4f, 12f), rng.RandfRange(0f, 9f)));
		}

		// ── 声音 ──
		Sfx.Play(Sfx.AllOut, 0.9f);                          // 最后一击定格：原版总攻击斩击
		Sfx.Play(Sfx.Crack, 0.45f, CrackAt);                 // 裂开
		Sfx.Play(Sfx.Shatter, 0.85f, ShatterAt);
		Sfx.Play(Sfx.SwishLow, 0.5f, ShatterAt + 0.03f);         // 碎星转进来
		Sfx.Play(Sfx.CutIn, 0.8f, ShatterAt + JokerIn);          // Joker 冲进来
		Sfx.Play(Sfx.Swish, 0.35f, ShatterAt + TextIn);           // 竖排字
		Sfx.Play(Sfx.AoaSlash3, 0.6f, ArtT);
		Sfx.Play(Sfx.SubBoom, 0.7f, ArtT);
		Sfx.Play(Sfx.SwishHeavy, 0.55f, ExitAt);
		Sfx.Play(Sfx.MenuWhoosh, 0.5f, HoleAt);

		bool broke = false, cracked = false;
		Fx.Animate(layer, 0, Total, u =>
		{
			float T = u * Total;
			// 定格：裂纹从敌人那里炸开，画面往里推一点
			// 顿帧：最开头两帧是黑白反转的冲击帧，之后画面压暗、去色、四周收暗，撞击点附近色散，集中线一闪一闪
			float imp = T < 0.07f ? 1f : 0f;
			float hold = Mathf.Clamp((T - 0.05f) / 0.12f, 0f, 1f);
			still.SetShaderParameter("impact", imp);
			still.SetShaderParameter("hold", hold);
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
			// 裂开的过程中慢慢往撞击点推近，碎开前一拍微微发抖
			float push = Mathf.Clamp((T - CrackAt) / (ShatterAt - CrackAt), 0f, 1f);
			stageView.PivotOffset = focus;
			stageView.Scale = Vector2.One * (1f + 0.06f * push * push);
			float tremble = T > ShatterAt - 0.12f && T < ShatterAt ? 3f : 0f;
			stageView.Position = new Vector2(Mathf.Sin(T * 97f), Mathf.Cos(T * 83f)) * tremble;
			if (T >= CrackAt && !cracked)
			{
				cracked = true;
				var built = BuildShards(shardLayer, stage, focus, screen, rng, crackLayer);
				shards.AddRange(built.Item1);
				cracks.AddRange(built.Item2);
			}
			if (cracked && !broke)
			{
				// 裂纹从撞击点往外窜：0.08 秒到画面外（先快后慢）
				// 先一下炸开一圈（撞击点附近），再越长越快地爬满全屏
				float c = Mathf.Clamp((T - CrackAt) / CrackGrow, 0f, 1f);
				float reach = 160f * Quint(Mathf.Clamp(c * 4f, 0f, 1f)) + 2300f * Mathf.Pow(c, 2.2f);
				foreach (Crack ck in cracks) GrowCrack(ck, reach);
			}
			// 碎开：离屏画面定住，玻璃片飞散，下面是原版收尾
			if (T >= ShatterAt && !broke)
			{
				broke = true;
				Fx.After(layer, 0.04, () => { if (GodotObject.IsInstanceValid(stage)) stage.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled; });
				stageView.Visible = false;
				finRoot.Visible = true;
				foreach (Shard sh in shards) sh.Face.Visible = sh.Back.Visible = sh.Edge.Visible = true;
			}
			if (broke)
			{
				float d = T - ShatterAt;
				float fa = 1f - Mathf.Clamp(d / 0.08f, 0f, 1f);
				flash.Visible = fa > 0f;
				flash.Color = new Color(1f, 1f, 1f, 0.5f * fa * fa);
				float bt = Mathf.Clamp(d / 0.16f, 0f, 1f);
				burst.Visible = bt < 1f;
				burst.Position = focus;
				burst.Rotation = 0.3f + 0.25f * bt;
				burst.Scale = Vector2.One * Mathf.Lerp(H * 0.2f, H * 1.1f, Quint(bt));
				burst.Modulate = new Color(1f, 1f, 1f, 0.75f * (1f - bt) * (1f - bt));
				foreach (Shard sh in shards)
				{
					// 炸开：一出手就是最快（四次方缓出），越转越慢；后段带一点下坠，最后 30% 很快淡掉
					float e = Mathf.Clamp((d - sh.Delay) / ShardLife, 0f, 1f);
					float fly = 1f - Mathf.Pow(1f - e, 4f);
					Vector2 off = sh.Dir * sh.Speed * fly + new Vector2(0f, 700f) * e * e;
					float sc = 1f + sh.Grow * fly;
					float a = 1f - Mathf.Clamp((e - 0.7f) / 0.3f, 0f, 1f);
					foreach (CanvasItem ci in new CanvasItem[] { sh.Face, sh.Back, sh.Edge })
					{
						Node2D n = (Node2D)ci;
						n.Position = sh.Center + off;
						n.Rotation = sh.Spin * fly;
						n.Scale = Vector2.One * sc;
						ci.Modulate = new Color(1f, 1f, 1f, a);
					}
					// 玻璃的厚度：分开以后才看得到（侧边一层暗红随飞行错开），白边随之亮起
					sh.Back.Position += new Vector2(7f, 9f) * sc * Mathf.Clamp(fly * 3f, 0f, 1f);
					sh.Edge.Modulate = new Color(1f, 1f, 1f, a * Mathf.Clamp(fly * 4f, 0f, 1f));
				}
				fin?.Seek(Mathf.Min(SrcStart + d, AllOutFinale.End - 0.02f));
				if (fin != null)
				{
					// 拆开入场（一层接一层）：红底从放大压回 → 碎星转着砸进来 → Joker 从右边冲进来（带一点回弹）→ 怪物剪影从左边砸进（ArtT）
					// → 竖排字从下往上划上来；Joker 落定那一下震屏
					float qb = Quint(Mathf.Clamp(d / 0.3f, 0f, 1f));
					fin.Bg.Scale = Vector2.One * Mathf.Lerp(1.15f, 1f, qb);
					float ks = Mathf.Clamp((d - 0.02f) / 0.34f, 0f, 1f);
					fin.Star.Visible = d >= 0.02f;
					fin.Star.Rotation = Mathf.Lerp(-0.9f, 0f, Back(ks, 1.2f));
					fin.Star.Scale = Vector2.One * Mathf.Lerp(1.9f, 1f, Back(ks, 0.8f));
					fin.Star.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(ks * 5f, 0f, 1f));
					float kj = Mathf.Clamp((d - JokerIn) / JokerLen, 0f, 1f);
					fin.Front.Visible = d >= JokerIn;
					fin.Front.Position = new Vector2(W * 0.55f * (1f - Back(kj, 0.9f)), 0f);
					fin.Front.Scale = Vector2.One * Mathf.Lerp(1.08f, 1f, Quint(kj));
					float kt = Mathf.Clamp((d - TextIn) / TextLen, 0f, 1f);
					fin.Text.Visible = d >= TextIn;
					fin.Text.Position = new Vector2(0f, H * 0.9f * (1f - Ease(kt) * (2f - Ease(kt)) * 0.5f - Ease(kt) * 0.5f));
					float landed = d - (JokerIn + JokerLen * 0.75f), hitT = T - ArtT;
					float sh = 0f;
					if (landed > 0f && landed < 0.16f) sh = (1f - landed / 0.16f) * 9f;
					if (hitT > 0f && hitT < 0.2f) sh = Mathf.Max(sh, (1f - hitT / 0.2f) * 13f);
					finRoot.PivotOffset = screen * 0.5f;
					finRoot.Scale = Vector2.One * (1f + sh * 0.003f);                  // 震的时候略放大，边缘不露底
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
					float sc = Mathf.Lerp(1.25f, 1f, le) * (1f + 0.035f * recoil);
					Vector2 piv = B.Position + B.Size * new Vector2(0.5f, 1f);
					sp.Scale = B.Size / sp.Texture.GetSize() * sc;
					sp.Rotation = rot * (1f - le) - 0.025f * recoil;
					sp.Position = piv + new Vector2(-B.Size.X * 0.5f * sc, -B.Size.Y * sc).Rotated(sp.Rotation) + new Vector2(-16f * recoil - W * 0.22f * (1f - le), 0f);
					sp.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(lt * 2.5f, 0f, 1f));
					Sprite2D sh = shadows[i];
					sh.Visible = sp.Visible;
					sh.Scale = sp.Scale;
					sh.Rotation = sp.Rotation;
					sh.Position = sp.Position + new Vector2(-B.Size.Y * 0.035f, B.Size.Y * 0.012f);
					sh.Modulate = sp.Modulate;
				}
				// 血
				foreach (Jet jt in jets) DrawJet(jt, T, finSils[jt.Foe].Sp);
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
			// 收尾：黑墨卷过盖满 → 中间挖开红边的洞
			if (T >= ExitAt)
			{
				cover.Visible = true;
				coverM.SetShaderParameter("sweep", Ease(Mathf.Clamp((T - ExitAt) / CoverLen, 0f, 1f)));
				float h = Mathf.Clamp((T - HoleAt) / HoleLen, 0f, 1f);
				coverM.SetShaderParameter("hole", h * h * (3f - 2f * h));
				if (T >= HoleAt) finRoot.Visible = false;
			}
		});
		Fx.After(layer, Total + 0.05, () =>
		{
			fin?.Free();
			layer.QueueFree();
		});
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

	/// <summary>一道裂纹：折线 + 起止粗细，Dist = 每个点离撞击点多远（裂纹从撞击点往外长）。</summary>
	private sealed class Crack
	{
		public Vector2[] Pts = Array.Empty<Vector2>();
		public float[] Dist = Array.Empty<float>();
		public float W0, W1;
		public Polygon2D Core = null!, Glow = null!;
	}

	/// <summary>
	/// 以 focus 为中心的玻璃片（环 × 扇区，顶点抖动；大片再沿对角切开），边是有走向的折线（裂纹和玻璃片共用同一条折线，碎开时严丝合缝）。
	/// 裂纹：放射线从撞击点一路到画面外、越远越细；环线只在近处三圈、更细；撞击点一圈细碎的短裂纹。贴图 = 离屏画面。
	/// </summary>
	private static (List<Shard>, List<Crack>) BuildShards(Node2D layer, SubViewport stage, Vector2 focus, Vector2 screen, RandomNumberGenerator rng, Node2D crackLayer)
	{
		float[] rings = { 0f, 46f, 120f, 240f, 420f, 680f, 1020f, 1500f, 2300f };
		const int A = 12;
		var v = new Vector2[rings.Length, A];
		float a0 = rng.RandfRange(0f, Mathf.Tau);
		float[] angs = new float[A];
		for (int a = 0; a < A; a++) angs[a] = a0 + Mathf.Tau * (a + rng.RandfRange(-0.32f, 0.32f)) / A;
		for (int r = 0; r < rings.Length; r++)
		{
			for (int a = 0; a < A; a++)
			{
				float rr = rings[r] * (r == 0 ? 0f : rng.RandfRange(0.82f, 1.18f));
				float aa = angs[a] + (r == 0 ? 0f : rng.RandfRange(-0.07f, 0.07f) * (1f + r * 0.15f));
				v[r, a] = focus + Vector2.FromAngle(aa) * rr;
			}
		}
		// 折线边：同一条边（不管从哪头取）只生成一次
		var jag = new Dictionary<long, Vector2[]>();
		Vector2[] Edge(int r1, int a1, int r2, int a2)
		{
			long k1 = r1 * 64 + a1, k2 = r2 * 64 + a2;
			bool flip = k1 > k2;
			long key = flip ? k2 * 4096 + k1 : k1 * 4096 + k2;
			if (!jag.TryGetValue(key, out Vector2[]? pts))
			{
				Vector2 p = flip ? v[r2, a2] : v[r1, a1], q = flip ? v[r1, a1] : v[r2, a2];
				float len = p.DistanceTo(q);
				int n = Mathf.Clamp(Mathf.RoundToInt(len / 26f), 1, 16);
				pts = new Vector2[n + 1];
				Vector2 nrm = (q - p).Orthogonal().Normalized();
				RandomNumberGenerator jr = new() { Seed = (ulong)(key + 17) * 2654435761ul };
				float drift = 0f;
				for (int i = 0; i <= n; i++)
				{
					float t = i / (float)n;
					drift = drift * 0.55f + jr.RandfRange(-1f, 1f) * Mathf.Min(len * 0.04f, 14f);   // 有走向的抖动，不是锯齿
					pts[i] = p.Lerp(q, t) + nrm * drift * Mathf.Sin(t * Mathf.Pi);
				}
				jag[key] = pts;
			}
			if (!flip) return pts;
			var rev = new Vector2[pts.Length];
			for (int i = 0; i < pts.Length; i++) rev[i] = pts[pts.Length - 1 - i];
			return rev;
		}
		Vector2[] Loop((int R, int A)[] corners)
		{
			var outp = new List<Vector2>();
			for (int i = 0; i < corners.Length; i++)
			{
				var c1 = corners[i];
				var c2 = corners[(i + 1) % corners.Length];
				Vector2[] e = Edge(c1.R, c1.A, c2.R, c2.A);
				for (int k = 0; k < e.Length - 1; k++) outp.Add(e[k]);
			}
			return outp.ToArray();
		}
		Texture2D tex = stage.GetTexture();
		var list = new List<Shard>();
		var cracks = new List<Crack>();
		int[] reachRing = new int[A];
		for (int a = 0; a < A; a++) reachRing[a] = a % 3 == 0 ? rings.Length - 2 : rng.RandiRange(3, rings.Length - 2);
		Rect2 view = new Rect2(Vector2.Zero, screen).Grow(60f);
		for (int r = 0; r < rings.Length - 1; r++)
		{
			for (int a = 0; a < A; a++)
			{
				int b = (a + 1) % A;
				var pieces = new List<(int R, int A)[]>();
				if (r == 0) pieces.Add(new[] { (0, a), (1, a), (1, b) });
				else if (r >= 2 && rng.Randf() < 0.6f)
				{
					if (rng.Randf() < 0.5f) { pieces.Add(new[] { (r, a), (r + 1, a), (r + 1, b) }); pieces.Add(new[] { (r, a), (r + 1, b), (r, b) }); }
					else { pieces.Add(new[] { (r, a), (r + 1, a), (r, b) }); pieces.Add(new[] { (r + 1, a), (r + 1, b), (r, b) }); }
				}
				else pieces.Add(new[] { (r, a), (r + 1, a), (r + 1, b), (r, b) });
				foreach (var pc in pieces)
				{
					Vector2[] poly = Loop(pc);
					Vector2 c = Vector2.Zero;
					foreach (var k in pc) c += v[k.R, k.A];
					c /= pc.Length;
					Rect2 bb = new(poly[0], Vector2.Zero);
					foreach (Vector2 p in poly) bb = bb.Expand(p);
					if (!bb.Intersects(view)) continue;
					var local = new Vector2[poly.Length];
					for (int k = 0; k < poly.Length; k++) local[k] = poly[k] - c;
					float dist = (c - focus).Length();
					Shard sh = new()
					{
						Center = c, Dir = (c - focus).LengthSquared() > 1f ? (c - focus).Normalized() : Vector2.Up,
						Speed = (700f + 1900f * Mathf.Clamp(1f - dist / 1800f, 0.2f, 1f)) * rng.RandfRange(0.8f, 1.25f),
						Spin = rng.RandfRange(-3.2f, 3.2f), Delay = Mathf.Clamp(dist / 9000f, 0f, 0.04f),
						Grow = rng.Randf() < 0.3f ? rng.RandfRange(0.6f, 1.1f) : rng.RandfRange(0.05f, 0.35f),     // 少数朝镜头飞（变大）
						Back = new Polygon2D { Polygon = local, Color = new Color(0.22f, 0f, 0.03f), Antialiased = true, Visible = false },
						Face = new Polygon2D { Polygon = local, Texture = tex, UV = poly, Antialiased = true, Visible = false },
						Edge = new Line2D { Points = local, Closed = true, Width = 1.2f, DefaultColor = new Color(1f, 0.95f, 0.95f, 0.7f), Antialiased = true, Visible = false },
					};
					layer.AddChild(sh.Back);
					layer.AddChild(sh.Face);
					layer.AddChild(sh.Edge);
					list.Add(sh);
				}
				// 裂纹：主裂纹沿放射边，长短不一（有的到画面外、有的半路停）；环线只在最里面一圈零星几段 —— 不要整齐的蛛网
				if (r < reachRing[a]) cracks.Add(MakeCrack(Edge(r, a, r + 1, a), focus, crackLayer, 1f));
				if (r == 1 && rng.Randf() < 0.55f) cracks.Add(MakeCrack(Edge(r, a, r, b), focus, crackLayer, 0.55f));
				if (r == 2 && rng.Randf() < 0.25f) cracks.Add(MakeCrack(Edge(r, a, r, b), focus, crackLayer, 0.4f));
				// 分叉：从主裂纹中段斜着岔出去一小段
				if (r >= 1 && r < reachRing[a] && rng.Randf() < 0.45f)
				{
					Vector2[] e = Edge(r, a, r + 1, a);
					Vector2 p0 = e[e.Length / 2];
					Vector2 dir = (e[^1] - e[0]).Normalized().Rotated(rng.RandfRange(0.35f, 0.7f) * (rng.Randf() < 0.5f ? -1f : 1f));
					float len = (e[^1] - e[0]).Length() * rng.RandfRange(0.35f, 0.7f);
					Vector2 p2 = p0 + dir * len, p1 = p0.Lerp(p2, 0.5f) + dir.Orthogonal() * len * rng.RandfRange(-0.12f, 0.12f);
					cracks.Add(MakeCrack(new[] { p0, p1, p2 }, focus, crackLayer, 0.45f));
				}
			}
		}
		// 撞击点：一圈细碎的短裂纹
		for (int i = 0; i < 16; i++)
		{
			float ang = rng.RandfRange(0f, Mathf.Tau), len = rng.RandfRange(18f, 60f);
			Vector2 p = focus + Vector2.FromAngle(ang) * rng.RandfRange(4f, 16f);
			Vector2 q = p + Vector2.FromAngle(ang + rng.RandfRange(-0.4f, 0.4f)) * len;
			cracks.Add(MakeCrack(new[] { p, p.Lerp(q, 0.5f) + (q - p).Orthogonal() * rng.RandfRange(-0.12f, 0.12f), q }, focus, crackLayer, 0.45f));
		}
		return (list, cracks);
	}

	private static Crack MakeCrack(Vector2[] pts, Vector2 focus, Node2D layer, float weight)
	{
		Crack c = new() { Pts = pts, Dist = new float[pts.Length] };
		for (int i = 0; i < pts.Length; i++) c.Dist[i] = pts[i].DistanceTo(focus);
		float W(float d) => Mathf.Lerp(4.5f, 0.8f, Mathf.Clamp(d / 900f, 0f, 1f)) * weight;   // 越远越细
		c.W0 = W(c.Dist[0]);
		c.W1 = W(c.Dist[^1]);
		c.Glow = new Polygon2D { Color = new Color(1f, 1f, 1f, 0.16f), Antialiased = true, Visible = false };
		c.Core = new Polygon2D { Color = new Color(1f, 1f, 1f, 0.95f), Antialiased = true, Visible = false };
		layer.AddChild(c.Glow);
		layer.AddChild(c.Core);
		return c;
	}

	/// <summary>裂纹长到离撞击点 reach 为止（折线截断，正在长的前端是尖的）。</summary>
	private static void GrowCrack(Crack c, float reach)
	{
		var pts = new List<Vector2>();
		var ws = new List<float>();
		int n = c.Pts.Length;
		for (int i = 0; i < n; i++)
		{
			float wt = n == 1 ? 0f : i / (float)(n - 1);
			if (c.Dist[i] <= reach)
			{
				pts.Add(c.Pts[i]);
				ws.Add(Mathf.Lerp(c.W0, c.W1, wt));
				continue;
			}
			if (i > 0 && c.Dist[i - 1] <= reach)
			{
				float f = (reach - c.Dist[i - 1]) / Mathf.Max(0.001f, c.Dist[i] - c.Dist[i - 1]);
				pts.Add(c.Pts[i - 1].Lerp(c.Pts[i], f));
				ws.Add(0f);
			}
			break;
		}
		if (pts.Count < 2)
		{
			c.Core.Visible = c.Glow.Visible = false;
			return;
		}
		c.Core.Visible = c.Glow.Visible = true;
		c.Core.Polygon = Strip(pts, ws, 1f);
		c.Glow.Polygon = Strip(pts, ws, 3.4f);
	}

	private static Vector2[] Strip(List<Vector2> pts, List<float> ws, float k)
	{
		int n = pts.Count;
		var o = new Vector2[n * 2];
		for (int i = 0; i < n; i++)
		{
			Vector2 t = (pts[Mathf.Min(i + 1, n - 1)] - pts[Mathf.Max(i - 1, 0)]).Normalized();
			Vector2 nr = t.Orthogonal() * Mathf.Max(ws[i], 0.05f) * 0.5f * k;
			o[i] = pts[i] + nr;
			o[n * 2 - 1 - i] = pts[i] - nr;
		}
		return o;
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

	private static Vector2[] StarPoly(float r)
	{
		var p = new Vector2[8];
		for (int i = 0; i < 8; i++) p[i] = Vector2.FromAngle(Mathf.Pi * i / 4f) * (i % 2 == 0 ? r : r * 0.22f);
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

	private static Shader? _sil, _shade, _blob, _cover, _still;

	/// <summary>
	/// 顿帧的定格画面：impact = 冲击帧（黑白反转、高反差），hold = 顿住（压暗、去色 60%、四周收暗、撞击点附近一点红青色散）。
	/// </summary>
	private static Shader StillShader => _still ??= new Shader { Code = @"
shader_type canvas_item;
uniform float impact = 0.0;
uniform float hold = 0.0;
uniform vec2 focus = vec2(0.6, 0.5);
uniform float aspect = 1.7778;
void fragment() {
	vec2 d = (UV - focus) * vec2(aspect, 1.0);
	float r = length(d);
	vec2 ca = (UV - focus) * 0.006 * hold * smoothstep(0.0, 0.5, r);
	vec3 c = vec3(texture(TEXTURE, UV + ca).r, texture(TEXTURE, UV).g, texture(TEXTURE, UV - ca).b);
	float l = dot(c, vec3(0.299, 0.587, 0.114));
	vec3 held = mix(c, vec3(l), 0.6) * mix(1.0, 0.62, hold);
	held *= 1.0 - hold * 0.55 * smoothstep(0.35, 1.2, r);
	vec3 o = mix(c, held, hold);
	float hi = smoothstep(0.32, 0.38, l);
	o = mix(o, vec3(1.0 - hi), impact);
	COLOR = vec4(o, 1.0);
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
