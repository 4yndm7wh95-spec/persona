using System.Collections.Generic;
using Godot;

namespace P3Makoto.Vfx;

/// <summary>
/// 总攻击（All-Out Attack）：精英 / Boss 战的最后一击之后、胜利结算之前播放（AllOutPatch 把战斗结束流程推迟 Duration 秒；
/// 播了总攻击就不再播普通胜利演出）。
/// v0.14.16（用户：只要「怪物倒下处展开同心圆 → 顺势切到结算立绘」；同心圆层数、节奏、音效都照原版；字幕要做进背景）：约 3.65s
///   音效：主音是 Snd_FX_Btl_Allout_Zoom（把原版结算片段和全部 P3R 音效库做互相关得出），前 0.7s 是蓄力、0.7s 到达高潮；
///   v0.14.20 另加：第一滴 Allout_Wipe（压低）、第二滴 swish、背景字滑入 swish、收尾 swish（全部走 Master 总线）。
///   收尾 0.75s（v0.14.20，用户：不要草草淡出）：立绘一顿后甩出右侧、背景字滑向左，水波从圆心挖开一个带亮边的洞、扩到全屏，露出战场，接原版结算。
///   0.30  蓄力 = 湖面水波（圆心在画面右上角外）：第一滴落下，三圈边界先后冲出 —— 深一档蓝前沿扫满全屏、浅蓝、中蓝，
///         从左到右是「深 · 浅 · 深」；0.36s 后圆心再落一滴，新的浅蓝冲出来把上一轮推开、挤窄，压力一圈圈往外传（环带弹簧模型）
///   1.00  高潮：结算立绘切入（1.3 倍以脸为支点拉回、暗蓝平滑亮起）；不再滴水，余波回弹、慢慢外扩
///         背景大字 MAJESTIC：深蓝细线空心字，落在深色带里的那段换成深蓝实心（遮罩跟着水波实时走）
///   去掉了左上角「DONE AND DUSTED.」（用户：太突兀）。停留到 3.25 → 0.4s 整体淡出
/// 结束立绘用 P3R 原版贴图（allout_finish / allout_finish_dark / allout_text），缺图时退回 victory_portrait。
/// </summary>
public static partial class AllOut
{
	private const float IrisAt = 0.3f, Build = 0.7f, FinishAt = IrisAt + Build, FinishLen = 2.95f;
	// v0.14.24 收尾（用户：之前的结束动画不好 —— 在同一个圆心再滴一滴水，这滴的水波把整层总攻击（背景字、立绘）盖掉，
	// 盖满后顺势露出后面的结算页面，要丝滑）：Duration - OutroLen 滴下，三圈实心水波依次扫满全屏（最后一圈是原版结算的浅蓝），
	// 盖满后停一拍等结算页在下面出现，再柔和淡出。覆盖层放在独立的最上层 CanvasLayer，结算页弹出时也被盖着。
	// v0.14.25（用户：第三圈浅色水波直接换成全透明，动画不变，更自然地切出结算页；浅色纯色停留太久）：
	// 深一档蓝 → 中蓝两圈照旧扫满，中蓝盖满的那一刻第三圈"透明水波"紧接着从圆心扩开（边缘一道亮边），直接露出下面的结算页；
	// 同一刻藏掉总攻击层（已被两圈深色完全盖住，看不出来）。
	// 用户：透明区足够大时结算页再顺势出来 —— 滴下时刻往前挪：透明波开始扩 0.17s 后（半径约 65% 满屏）正好到 Duration、结算页出现。
	private const float OutroLen = 0.56f;
	private const float CoverSweep = 0.42f, ClearSweep = 0.55f;
	private const float PortraitLead = 0.1f;                         // 立绘比高潮早 0.1s 开始淡入，高潮时已基本到位（用户：人物出现太突兀）
	public const float Duration = FinishAt + FinishLen;

	// 原版结算画面的配色（从原版截图取色）
	private static readonly Color Base = new(53 / 255f, 172 / 255f, 239 / 255f);
	private static readonly Color RingBlue = new(23 / 255f, 105 / 255f, 216 / 255f);
	private static readonly Color Cyan = new(118 / 255f, 212 / 255f, 248 / 255f);
	private static readonly Color Deep = new(10 / 255f, 30 / 255f, 120 / 255f);
	private static readonly Color Navy = new(8 / 255f, 26 / 255f, 82 / 255f);
	private static readonly Color Band = new(23 / 255f, 97 / 255f, 175 / 255f);       // 原版左下深一档的蓝
	private static readonly Color Ink = new(6 / 255f, 20 / 255f, 66 / 255f);          // 深色波里实心背景字的颜色

	/// <summary>本场已播总攻击：VictoryPatch 读到后跳过普通胜利演出并清掉。</summary>
	public static bool Played;

	public static void Play(Control front, IReadOnlyList<Vector2> enemies)
	{
		Played = true;
		Vector2 size = front.GetViewportRect().Size;
		float W = size.X, H = size.Y;
		Vector2 focus = enemies.Count > 0 ? Avg(enemies) : size * new Vector2(0.68f, 0.5f);
		Node2D root = Fx.Root(front, Vector2.Zero, "P3AllOut");
		root.TopLevel = true;
		root.GlobalPosition = Vector2.Zero;

		// 蓄力 0.7s = 同心圆展开，峰值 = 同心圆落定、结算页出现
		// v0.14.16：实测总攻击时没声音 —— 战斗结束时 SFX 总线可能被游戏压低 / 静音，直接走 Master 总线；日志里记一行方便排查
		Audio.Sfx.Clip("Snd_FX_Btl_Allout_Zoom", 0.8f, FinishAt - 0.7f, master: true);
		// v0.14.20（用户：音效还不够丰富）：两滴水各一声（第一滴用原版转场音、压低），背景字滑入一声，收尾滑出一声
		Audio.Sfx.Clip("Btl_Allout_Wipe", 0.3f, IrisAt, master: true);
		Audio.Sfx.Clip("swish_large_1", 0.45f, IrisAt + 0.36f, master: true);
		Audio.Sfx.Clip("swish_small_2", 0.4f, FinishAt + 0.05f, master: true);
		Audio.Sfx.Clip("Btl_Allout_Wipe", 0.35f, Duration - OutroLen, master: true);   // 第三滴（和第一滴同一个水滴声）
		Audio.Sfx.Clip("swish_heavy_2", 0.5f, Duration - OutroLen + 0.08f, master: true);   // 水波扫满全屏

		Node2D bg = new();
		root.AddChild(bg);
		Node2D fg = new();
		root.AddChild(fg);
		Fx.After(root, IrisAt, "allout iris", () => Background(bg, focus, W, H, Duration - IrisAt));
		Fx.After(root, FinishAt - PortraitLead, "allout finish", () => Portrait(fg, W, H));
		Fx.After(root, Duration - OutroLen, "allout cover", () => Cover(front, root, W, H));
		Fx.FreeAfter(root, Duration + 0.1f);
	}

	/// <summary>
	/// 收尾的最后一滴：圆心同前（右上角外）。两圈实心水波（深一档蓝 → 中蓝）依次扫满，前沿带一道青白细光；
	/// 中蓝盖满的那一刻第三圈透明水波紧接着扩开，露出下面的结算页（总攻击层同时藏掉）。
	/// </summary>
	private static void Cover(Control anchor, Node2D allOut, float W, float H)
	{
		if (anchor.GetTree()?.Root is not Window win)
		{
			return;
		}
		CanvasLayer layer = new() { Layer = 120, Name = "P3AllOutCover" };
		win.AddChild(layer);
		ColorRect disc = new() { Size = new Vector2(W, H), MouseFilter = Control.MouseFilterEnum.Ignore, Material = new ShaderMaterial { Shader = Rings } };
		layer.AddChild(disc);
		ShaderMaterial rm = (ShaderMaterial)disc.Material;
		Vector2 center = new(W * 0.95f, -H * 0.45f);
		float cover = 0f;
		foreach (Vector2 corner in new[] { Vector2.Zero, new Vector2(W, 0f), new Vector2(0f, H), new Vector2(W, H) })
		{
			cover = Mathf.Max(cover, (corner - center).Length());
		}
		rm.SetShaderParameter("size", new Vector2(W, H));
		rm.SetShaderParameter("center", center);
		rm.SetShaderParameter("hole_center", center);
		Color[] cols = new Color[12];
		cols[0] = Band;
		cols[1] = RingBlue;
		rm.SetShaderParameter("cols", cols);
		float[] born = { 0f, 0.08f };
		// 半径 = cover·1.04·OutCubic(u)：u≈0.75 时已盖满（前快后慢，接近盖满时"铺"开）
		float clearAt = born[1] + 0.75f * CoverSweep;
		bool hidden = false;
		float total = clearAt + ClearSweep;
		Fx.Animate(layer, total, t =>
		{
			float time = t * total;
			float[] radii = new float[12];
			float[] rimR = new float[4], rimA = new float[4];
			int live = 0;
			for (int i = 0; i < 2; i++)
			{
				float u = (time - born[i]) / CoverSweep;
				if (u <= 0f)
				{
					break;
				}
				radii[i] = cover * 1.04f * Ease.OutCubic(Mathf.Min(u, 1f));
				live = i + 1;
				rimR[i] = radii[i];
				rimA[i] = 0.45f * (1f - Mathf.Clamp(u / 0.75f, 0f, 1f));
			}
			rm.SetShaderParameter("radii", radii);
			rm.SetShaderParameter("count", live);
			rm.SetShaderParameter("rim_r", rimR);
			rm.SetShaderParameter("rim_a", rimA);
			// 第三圈：透明水波（着色器的 hole：洞里透明，边缘一道亮边）
			float c = (time - clearAt) / ClearSweep;
			if (c > 0f && !hidden)
			{
				hidden = true;
				if (GodotObject.IsInstanceValid(allOut))
				{
					allOut.Visible = false;                              // 已被两圈深色完全盖住
				}
			}
			rm.SetShaderParameter("hole", c > 0f ? cover * 1.06f * Ease.OutCubic(Mathf.Min(c, 1f)) + 1f : 0f);
		});
		Fx.FreeAfter(layer, total + 0.05f);
	}

	private static Vector2 Avg(IReadOnlyList<Vector2> pts)
	{
		Vector2 c = Vector2.Zero;
		foreach (Vector2 p in pts) c += p;
		return c / pts.Count;
	}

	private static Texture2D? Tex(string name) =>
		ResourceLoader.Exists($"res://p3makoto/ui/{name}.png") ? ResourceLoader.Load<Texture2D>($"res://p3makoto/ui/{name}.png") : null;

	private static Shader? _rings;

	/// <summary>同心圆：按顺序一层层画实心圆（后画的盖前面的），环带 = 外径实心圆 + 内径底色圆；fwidth 做 1px 抗锯齿软边。
	/// rim_*：展开中的波前细光边。</summary>
	private static Shader Rings => _rings ??= new Shader
	{
		Code = @"
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
		c.rgb = mix(c.rgb, cols[i].rgb, c.a > 0.0 ? cov : 1.0);     // 第一层：直接取色（透明度单独给），边缘不发黑
		c.a = max(c.a, cov);
	}
	for (int j = 0; j < 4; j++) {
		float k = rim_a[j] * (1.0 - smoothstep(0.0, 2.5, abs(d - rim_r[j])));
		c.rgb = mix(c.rgb, vec3(0.88, 0.98, 1.0), k);
	}
	// 收尾：从圆心挖开的洞（洞里透明、露出战场），洞的边缘一道亮边
	float dh = distance(p, hole_center);
	float edge = hole > 0.0 ? (1.0 - smoothstep(0.0, 7.0, abs(dh - hole))) : 0.0;
	c.rgb = mix(c.rgb, vec3(0.92, 0.99, 1.0), edge * 0.8);
	c.a *= hole > 0.0 ? clamp((dh - hole) / aa + 0.5, 0.0, 1.0) : 1.0;
	COLOR = c;
}",
	};

	// ── 环带：普通 Polygon2D（外圈 N 点 + 内圈 N 点，每段一个四边形），每帧只改顶点 ──
	private const int Seg = 240;

	private static Polygon2D Annulus(Node parent, Color color)
	{
		Polygon2D p = new() { Color = color, Polygon = new Vector2[Seg * 2] };
		var quads = new Godot.Collections.Array();
		for (int i = 0; i < Seg; i++)
		{
			int j = (i + 1) % Seg;
			quads.Add(new[] { i, j, Seg + j, Seg + i });
		}
		p.Polygons = quads;
		parent.AddChild(p);
		return p;
	}

	private static void SetAnnulus(Polygon2D p, Vector2 c, float rOut, float rIn)
	{
		Vector2[] pts = new Vector2[Seg * 2];
		for (int i = 0; i < Seg; i++)
		{
			Vector2 d = Vector2.Right.Rotated(i * Mathf.Tau / Seg);
			pts[i] = c + d * Mathf.Max(rOut, 0.5f);
			pts[Seg + i] = c + d * Mathf.Clamp(rIn, 0f, Mathf.Max(rOut - 0.5f, 0f));
		}
		p.Polygon = pts;
	}

	/// <summary>
	/// 背景大字的一份拷贝：hollow = 深蓝细线空心字，否则 = fill 色实心字（位置 / 变形相同）。
	/// 原版细节：浅色区是极细线勾出的空心字、深色区是实心字，轮廓跨区连续。
	/// 空心字不能用字体自带的描边（v0.14.16 实测：Bahnschrift 的字形由多个重叠轮廓拼成，Label 描边把每个轮廓都描出来，字里出现一堆格线）——
	/// 改成：字先实心画进一个 CanvasGroup（离屏合成后重叠轮廓并成一个形状），再用着色器只描这个形状的边。
	/// </summary>
	private static Node2D WordCopy(Node parent, Vector2 at, float H, Color color, bool hollow)
	{
		Node2D w = new() { Position = at, Rotation = -0.12f, Skew = -0.25f };
		parent.AddChild(w);
		int size = (int)(H * 0.36f);
		if (hollow)
		{
			ShaderMaterial m = new() { Shader = HollowShader };
			m.SetShaderParameter("line_color", color);
			m.SetShaderParameter("width", Mathf.Max(1.6f, H * 0.0028f));
			CanvasGroup g = new() { Material = m, FitMargin = 8f, ClearMargin = 8f };
			w.AddChild(g);
			Typo.Text(g, "MAJESTIC", Typo.LatinItalic, size, Colors.White, Vector2.Zero);
		}
		else
		{
			Typo.Text(w, "MAJESTIC", Typo.LatinItalic, size, color, Vector2.Zero);
		}
		return w;
	}

	private static Shader? _hollow;

	/// <summary>CanvasGroup 着色器：读组内合成好的字形，只保留离边缘 width 像素以内的一圈（内描边），并做软边。</summary>
	private static Shader HollowShader => _hollow ??= new Shader
	{
		Code = @"
shader_type canvas_item;
uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_linear;
uniform vec4 line_color : source_color;
uniform float width = 2.0;
void fragment() {
	float a = textureLod(screen_texture, SCREEN_UV, 0.0).a;
	float mn = 1.0;
	for (int i = 0; i < 12; i++) {
		float ang = 6.2831853 * float(i) / 12.0;
		vec2 d = vec2(cos(ang), sin(ang)) * width * SCREEN_PIXEL_SIZE;
		mn = min(mn, textureLod(screen_texture, SCREEN_UV + d, 0.0).a);
	}
	float edge = clamp(a * (1.0 - mn) * 1.6, 0.0, 1.0);
	COLOR = vec4(line_color.rgb, line_color.a * edge * COLOR.a);
}",
	};

	/// <summary>一圈水波：圈内颜色、是不是深色带（深色带里的背景字换成实心）。半径来自 RippleTab。</summary>
	private readonly record struct Wave(Color Col, bool Dark);

	/// <summary>
	/// 第 time 秒各圈半径（720p 像素，外 → 内，未出生为 -1）。RippleTab 由 tools/vfx/allout_ripple.py 离线算好：
	/// 环带弹簧模型（新水滴冲出、追上里层把环带压窄、压力外传、回弹）+ σ=0.09s 双向平滑（用户：展开速率偏僵硬）。
	/// 表按 60Hz 存，这里用 Catmull-Rom 在相邻帧之间插值，速度连续。
	/// </summary>
	private static float[] Ripple(float time)
	{
		int rows = RippleTab.GetLength(0), n = RippleTab.GetLength(1);
		float f = Mathf.Clamp(time * RippleFps, 0f, rows - 1.001f);
		int i1 = (int)f;
		float u = f - i1;
		int i0 = Mathf.Max(i1 - 1, 0), i2 = Mathf.Min(i1 + 1, rows - 1), i3 = Mathf.Min(i1 + 2, rows - 1);
		float[] r = new float[n];
		for (int k = 0; k < n; k++)
		{
			float p0 = RippleTab[i0, k], p1 = RippleTab[i1, k], p2 = RippleTab[i2, k], p3 = RippleTab[i3, k];
			float v = 0.5f * (2f * p1 + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u * u + (-p0 + 3f * p1 - 3f * p2 + p3) * u * u * u);
			r[k] = v > 1f ? v : -1f;
		}
		return r;
	}

	/// <summary>
	/// 背景 = 湖面水波（用户第九轮：圆心在画面右上角外；第一滴落下后从左到右是「深 · 浅 · 深」三层；约 0.36s 后圆心再落一滴，
	/// 把上一轮的同心圆往外推，要看得出"推动""挤压"）：
	///   第一滴（怪物刚死）：三圈边界先后冲出 —— 深一档蓝前沿扫满全屏、浅蓝、中蓝；
	///   第二滴（0.36s）：同样冲出「浅 · 深 · 浅」一组新同心圆，追上上一轮里层的深色带把它压窄，压力往外传，浅色带随后也被挤窄，然后回弹、慢慢散开。
	///   高潮之后不再滴水，余波一直在缓慢外扩。
	///   背景大字 MAJESTIC：整体是深蓝细线空心字；落在深色带里的那一段由跟着水波走的遮罩换成深蓝实心（实时）。
	/// </summary>
	private static void Background(Node2D n, Vector2 focus, float W, float H, float life)
	{
		Vector2 center = new(W * 0.95f, -H * 0.45f);                   // 圆心在画面右上角外（用户指定）
		float cover = 0f;
		foreach (Vector2 corner in new[] { Vector2.Zero, new Vector2(W, 0f), new Vector2(0f, H), new Vector2(W, H) })
		{
			cover = Mathf.Max(cover, (corner - center).Length());
		}
		float k = H / 720f;                                              // 参数按 720p 调，其他分辨率等比
		// 和 RippleTab 的列一一对应（出生时刻、初速、环带宽度见 tools/vfx/allout_ripple.py）
		var waves = new List<Wave>
		{
			new(Band, true),          // 第一滴 0s：深色前沿（最快，扫满全屏）
			new(Base, false),         //       0.06s：浅色
			new(RingBlue, true),      //       0.12s：深色（离圆心最近）
			// 第二滴（用户：第二滴也要有自己的深色同心圆）：0.36 / 0.42 / 0.48s 冲出「浅 · 深 · 浅」，推挤上一轮
			new(Base, false),
			new(RingBlue, true),
			new(Base, false),
		};
		const int MaskN = 3;

		ColorRect disc = new() { Size = new Vector2(W, H), MouseFilter = Control.MouseFilterEnum.Ignore, Material = new ShaderMaterial { Shader = Rings } };
		n.AddChild(disc);
		ShaderMaterial rm = (ShaderMaterial)disc.Material;
		rm.SetShaderParameter("size", new Vector2(W, H));
		rm.SetShaderParameter("center", center);
		rm.SetShaderParameter("hole_center", center);
		Color[] colsAll = new Color[12];
		for (int i = 0; i < waves.Count; i++)
		{
			colsAll[i] = waves[i].Col;
		}
		rm.SetShaderParameter("cols", colsAll);

		// 背景大字：一份深蓝细线空心字铺满；每条深色带一个遮罩（只当裁剪、不画），里面放一份深色实心字
		Vector2 wordAt = new(-W * 0.01f, H * 0.52f);
		var words = new List<Node2D> { WordCopy(n, wordAt, H, new Color(Navy, 0.85f), hollow: true) };
		var masks = new List<Polygon2D>();
		for (int m = 0; m < MaskN; m++)
		{
			Polygon2D mask = Annulus(n, Colors.White);
			mask.ClipChildren = CanvasItem.ClipChildrenMode.Only;
			masks.Add(mask);
			words.Add(WordCopy(mask, wordAt, H, Ink, hollow: false));       // 深色带里的实心字：不透明、比描边再深一点
		}

		float prevT = 0f;
		float[] prevR = new float[waves.Count];
		Fx.Animate(n, life, t =>
		{
			float time = t * life;
			float[] r = Ripple(time);
			for (int i = 0; i < r.Length; i++)
			{
				if (r[i] > 0f) r[i] *= k;
			}
			float[] radii = new float[12];
			float[] rimR = new float[4], rimA = new float[4];
			int live = 0;
			for (int i = 0; i < waves.Count && r[i] > 0f; i++)
			{
				radii[i] = r[i];
				live = i + 1;
				// 波前细光边（我们自己的细节）：跑得快的那几圈前沿带一道很淡的青白细光，减速后褪掉
				float speed = time > prevT ? (r[i] - prevR[i]) / (time - prevT) : 0f;
				if (i < 4)
				{
					rimR[i] = r[i];
					rimA[i] = 0.3f * Mathf.Clamp((speed - 300f * k) / (2500f * k), 0f, 1f);
				}
			}
			System.Array.Copy(r, prevR, r.Length);
			prevT = time;
			rm.SetShaderParameter("radii", radii);
			rm.SetShaderParameter("count", live);
			rm.SetShaderParameter("rim_r", rimR);
			rm.SetShaderParameter("rim_a", rimA);
			// 深色带 = 这一圈减去里面那一圈
			int mi = 0;
			for (int i = 0; i < waves.Count && mi < MaskN; i++)
			{
				if (r[i] <= 0f || !waves[i].Dark)
				{
					continue;
				}
				float inner = i + 1 < waves.Count && r[i + 1] > 0f ? r[i + 1] : 0f;
				SetAnnulus(masks[mi++], center, Mathf.Min(r[i], cover * 1.2f), inner);
			}
			for (; mi < MaskN; mi++)
			{
				SetAnnulus(masks[mi], center, 0f, 0f);
			}

			// 背景大字：高潮那一刻起从左侧缓缓滑入（0.7s），透明度 0.25s 到满，之后慢慢右移
			float since = time - Build;
			float outK = Mathf.Clamp((time - (life - OutroLen)) / OutroLen, 0f, 1f);
			float wi = Ease.OutCubic(Mathf.Clamp(since / 0.7f, 0f, 1f));
			Vector2 wp = wordAt + new Vector2(-W * 0.3f * (1f - wi) + 18f * Mathf.Max(0f, since) - W * 0.06f * Ease.InCubic(outK), 0f);
			float wa = Ease.OutCubic(Mathf.Clamp(since / 0.25f, 0f, 1f));
			foreach (Node2D w in words)
			{
				w.Position = wp;
				w.Visible = since >= 0f;
				w.Modulate = new Color(1f, 1f, 1f, wa);
			}
			// 收尾（v0.14.24）：不再挖洞 / 滑走，最后一滴的水波从上层盖过来（Cover）；背景字只被水波推得往左一让
		});
		Fx.FreeAfter(n, life + 0.05f);
	}

	/// <summary>结算立绘（背景大字在 Background 里）。用户：左上角「DONE AND DUSTED.」太突兀 —— 去掉。</summary>
	private static void Portrait(Node2D root, float W, float H)
	{
		Node2D f = new();
		root.AddChild(f);
		Texture2D? lit = Tex("allout_finish") ?? Tex("victory_portrait");
		Texture2D? dark = Tex("allout_finish_dark");
		Node2D heroRoot = new();
		f.AddChild(heroRoot);
		float hs = lit != null ? H * 1.0f / lit.GetHeight() : 1f;
		heroRoot.AddChild(new Sprite2D { Texture = lit, Scale = Vector2.One * hs });
		Sprite2D? heroDark = dark != null ? new Sprite2D { Texture = dark, Scale = Vector2.One * hs } : null;
		if (heroDark != null)
			heroRoot.AddChild(heroDark);
		Sprite2D flash = new() { Texture = lit, Scale = Vector2.One * hs, Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add }, Modulate = new Color(1f, 1f, 1f, 0f) };
		heroRoot.AddChild(flash);
		Vector2 heroAt = new(W * 0.65f, H * 0.56f);
		// 脸（两眼中点）相对立绘中心的偏移（原图像素）；缩放时让它停在最终位置上
		Vector2 faceOff = lit != null ? new Vector2(1075f, 262f) - new Vector2(lit.GetWidth(), lit.GetHeight()) / 2f : Vector2.Zero;
		Vector2 faceAt = heroAt + faceOff * hs;

		const float total = FinishLen + PortraitLead;
		// 节奏照原版（video1 18.73s 切入，逐帧量）：立绘切入时 1.3 倍、带蓝色调，以脸为支点 0.7s 拉回原大，之后一直慢漂；
		// 0.25–0.65s 平滑亮起（不是一下闪白）
		Fx.Animate(f, total, t =>
		{
			float raw = t * total;
			float time = raw - PortraitLead;                            // 0 = 高潮那一刻
			float enter = Ease.OutCubic(Mathf.Clamp(raw / 0.5f, 0f, 1f));
			float ease = Ease.OutCubic(Mathf.Clamp(raw / 1.0f, 0f, 1f));
			float hsc = 1.3f - 0.3f * ease - 0.015f * Mathf.Max(0f, time);
			// 收尾：立绘先往左一顿（蓄势），再加速甩出右侧、略放大
			float outK = Mathf.Clamp((raw - (total - OutroLen)) / OutroLen, 0f, 1f);
			// 收尾（v0.14.24）：立绘不再甩出，被最后一滴的水波推得往左下轻轻一沉，随后被盖住
			float exitX = -W * 0.03f * Ease.OutCubic(outK);
			heroRoot.Scale = Vector2.One * hsc;
			heroRoot.Position = faceAt - faceOff * hs * hsc + new Vector2(W * 0.06f * (1f - enter) - 12f * Mathf.Max(0f, time) + exitX, 3f * Mathf.Sin(raw * 1.6f));
			heroRoot.Modulate = new Color(1f, 1f, 1f, Ease.OutCubic(Mathf.Clamp(raw / 0.3f, 0f, 1f)));
			float lift = Ease.InOutSine(Mathf.Clamp((time - 0.25f) / 0.4f, 0f, 1f));
			if (heroDark != null)
				heroDark.Modulate = new Color(1f, 1f, 1f, 1f - lift);
			flash.Modulate = new Color(1f, 1f, 1f, 0.15f * Mathf.Sin(Mathf.Pi * lift));
		});
		Fx.FreeAfter(f, total + 0.05f);
	}
}
