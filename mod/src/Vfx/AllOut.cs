using System.Collections.Generic;
using Godot;

namespace P3Makoto.Vfx;

/// <summary>
/// 总攻击（All-Out Attack）：精英 / Boss 战的最后一击之后、胜利结算之前播放（AllOutPatch 把战斗结束流程推迟 Duration 秒；
/// 播了总攻击就不再播普通胜利演出）。
/// v0.14.16（用户：只要「怪物倒下处展开同心圆 → 顺势切到结算立绘」；同心圆层数、节奏、音效都照原版；字幕要做进背景）：约 3.65s
///   音效：只有 Snd_FX_Btl_Allout_Zoom（把原版结算片段和全部 P3R 音效库做互相关得出），前 0.7s 是蓄力、0.7s 到达高潮。
///   0.30  蓄力 = 同心圆展开（原版 video1 9.4s：浅青 → 中蓝 → 深蓝一波接一波从画面外扫过、每波都铺满）：
///         从倒下的敌人中心依次涌出四波整屏圆（深蓝 → 中蓝 → 浅青 → 底色浅蓝），圆心一边涌一边滑到结算页圆环的位置；
///         随后两道环带（外圈浅青、内圈深蓝粗环）从圆心长出来。每一波的前沿有一道细细的青白光边（我们自己的细节，原版没有），落定后褪掉
///   1.00  高潮 = 同心圆落定成结算页背景（浅蓝底 + 深蓝粗环 + 外圈浅青环），结算立绘同一帧切入（1.3 倍以脸为支点拉回、暗蓝平滑亮起）；
///         背景之后一直呼吸、扩大、漂移，不静止
///         背景大字 MAJESTIC 做在背景里（主行 + 错开的一行回声）：浅色区是深蓝细线空心字、深蓝环里是深蓝实心字 ——
///         同一条细轮廓线跨过区域边界是连续的；分区用随圆环实时变化的裁剪实现，不是预先画好的图
///   去掉了左上角「DONE AND DUSTED.」（用户：太突兀）。停留到 3.25 → 0.4s 整体淡出
/// 结束立绘用 P3R 原版贴图（allout_finish / allout_finish_dark / allout_text），缺图时退回 victory_portrait。
/// </summary>
public static class AllOut
{
	private const float IrisAt = 0.3f, Build = 0.7f, FinishAt = IrisAt + Build, FinishLen = 2.65f;
	private const float PortraitLead = 0.1f;                         // 立绘比高潮早 0.1s 开始淡入，高潮时已基本到位（用户：人物出现太突兀）
	public const float Duration = FinishAt + FinishLen;

	// 原版结算画面的配色（从原版截图取色）
	private static readonly Color Base = new(53 / 255f, 172 / 255f, 239 / 255f);
	private static readonly Color RingBlue = new(23 / 255f, 105 / 255f, 216 / 255f);
	private static readonly Color Cyan = new(118 / 255f, 212 / 255f, 248 / 255f);
	private static readonly Color Deep = new(10 / 255f, 30 / 255f, 120 / 255f);
	private static readonly Color Navy = new(8 / 255f, 26 / 255f, 82 / 255f);

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
		Audio.Sfx.Clip("Snd_FX_Btl_Allout_Zoom", 0.8f, FinishAt - 0.7f);

		Node2D bg = new();
		root.AddChild(bg);
		Node2D fg = new();
		root.AddChild(fg);
		Fx.After(root, IrisAt, "allout iris", () => Background(bg, focus, W, H, Duration - IrisAt));
		Fx.After(root, FinishAt - PortraitLead, "allout finish", () => Portrait(fg, W, H));
		Fx.FreeAfter(root, Duration + 0.1f);
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
uniform float radii[8];
uniform vec4 cols[8];
uniform float rim_r[4];
uniform float rim_a[4];
void fragment() {
	vec2 p = UV * size;
	float d = distance(p, center);
	float aa = max(fwidth(d), 0.75);
	vec4 c = vec4(0.0);
	for (int i = 0; i < 8; i++) {
		if (i >= count) break;
		float cov = clamp((radii[i] - d) / aa + 0.5, 0.0, 1.0);
		c.rgb = mix(c.rgb, cols[i].rgb, c.a > 0.0 ? cov : 1.0);     // 第一层：直接取色（透明度单独给），边缘不发黑
		c.a = max(c.a, cov);
	}
	for (int j = 0; j < 4; j++) {
		float k = rim_a[j] * (1.0 - smoothstep(0.0, 2.5, abs(d - rim_r[j])));
		c.rgb = mix(c.rgb, vec3(0.88, 0.98, 1.0), k);
	}
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

	/// <summary>同一段背景大字的一份拷贝：位置 / 变形相同，颜色不同。</summary>
	/// <remarks>原版细节（放大原版画面看）：浅色区的字是"深蓝极细线勾出的空心字"，深色区的字是"深蓝实心字"，
	/// 每一处都是同一条细轮廓线 —— 跨过区域边界时轮廓连续，只是里面填不填色变了；下方还错开叠着一行同样的空心字（回声）。
	/// 所以每份拷贝都用同样粗细的细描边，只换填色。</remarks>
	private static Node2D WordCopy(Node parent, Vector2 at, float H, Color fill, Color outline)
	{
		Node2D w = new() { Position = at, Rotation = -0.12f, Skew = -0.25f };
		parent.AddChild(w);
		Typo.Text(w, "MAJESTIC", Typo.LatinItalic, (int)(H * 0.36f), fill, Vector2.Zero, outline: Mathf.Max(2, (int)(H * 0.0035f)), outlineColor: outline);
		return w;
	}

	/// <summary>
	/// 背景（同心圆 → 结算页底）+ 做进背景里的字：
	///   四波整屏圆从敌人中心依次涌出（每波前沿带细光边），两道环带随后长出；圆心滑到圆环位置后一直缓慢漂移、呼吸。
	///   MAJESTIC：先画一份"浅蓝底上的样式"，环带本身开 ClipChildren，把同一段字的另一种样式画在环带里 ——
	///   环带怎么动，字的分区就怎么变（实时）。
	/// </summary>
	private static void Background(Node2D n, Vector2 focus, float W, float H, float life)
	{
		Vector2 ringAt = new(W * 0.66f, H * 0.48f);
		float cover = 0f;
		foreach (Vector2 corner in new[] { Vector2.Zero, new Vector2(W, 0f), new Vector2(0f, H), new Vector2(W, H) })
		{
			cover = Mathf.Max(cover, Mathf.Max((corner - focus).Length(), (corner - ringAt).Length()));
		}
		cover *= 1.1f;

		// 四波整屏圆（后一波盖住前一波；最后一波 = 结算页底色）
		(Color col, float start, float len)[] waves = { (Deep, 0f, 0.36f), (RingBlue, 0.11f, 0.38f), (Cyan, 0.22f, 0.4f), (Base, 0.33f, 0.42f) };
		// 画面上的圆全部由一个全屏着色器逐像素算（v0.14.16，用户：圆边有锯齿）：多边形放大到整屏、顶点又超过抗锯齿上限时边缘发毛；
		// 着色器按到圆心的距离 + fwidth 做 1px 软边，任意大小都是平滑的圆
		ColorRect disc = new() { Size = new Vector2(W, H), MouseFilter = Control.MouseFilterEnum.Ignore, Material = new ShaderMaterial { Shader = Rings } };
		n.AddChild(disc);
		ShaderMaterial rm = (ShaderMaterial)disc.Material;
		rm.SetShaderParameter("size", new Vector2(W, H));
		// 外圈浅青环（大）+ 内圈深蓝粗环（结算页的主圆环，原版截图量：内 0.55H、外 0.66H）
		float outIn = H * 1.02f, outOut = H * 1.2f, ringIn = H * 0.55f, ringOut = H * 0.66f;
		// 两道环带的多边形只当字的裁剪遮罩（不画出来）
		Polygon2D outer = Annulus(n, Colors.White);
		Polygon2D ring = Annulus(n, Colors.White);
		outer.ClipChildren = CanvasItem.ClipChildrenMode.Only;
		ring.ClipChildren = CanvasItem.ClipChildrenMode.Only;
		Color[] cols = { Deep, RingBlue, Cyan, Base, Cyan, Base, RingBlue, Base };
		rm.SetShaderParameter("cols", cols);
		rm.SetShaderParameter("count", cols.Length);

		// 背景大字 MAJESTIC（主行 + 下方错开的一行回声）：浅蓝底色区 = 深蓝细线空心字，外圈浅青环 = 中蓝细线空心字，
		// 深蓝粗环 = 深蓝实心字（同一条细轮廓，只是填满）
		Vector2 wordAt = new(-W * 0.01f, H * 0.52f);
		Vector2 echoOff = new(W * 0.035f, H * 0.2f);
		Color clear = new(0f, 0f, 0f, 0f);
		var words = new List<(Node2D node, Vector2 off, float alpha)>();
		foreach ((Vector2 off, float a) in new[] { (Vector2.Zero, 1f), (echoOff, 0.55f) })
		{
			Node2D wb = WordCopy(n, wordAt + off, H, clear, new Color(Navy, 0.85f * a));
			n.MoveChild(wb, outer.GetIndex());                         // 在两道环带下面、底色上面
			words.Add((wb, off, a));
			words.Add((WordCopy(outer, wordAt + off, H, clear, new Color(RingBlue, 0.95f * a)), off, a));
			words.Add((WordCopy(ring, wordAt + off, H, new Color(Navy, 0.92f * a), new Color(Navy, 0.92f * a)), off, a));
		}

		Fx.Animate(n, life, t =>
		{
			float time = t * life;
			// 圆心：蓄力期间从敌人滑到圆环位置；之后一直缓慢漂移
			Vector2 c = focus.Lerp(ringAt, Ease.InOutCubic(Mathf.Clamp(time / Build, 0f, 1f))) + new Vector2(-10f, -3f) * Mathf.Max(0f, time - Build);
			float breathe = 1f + 0.015f * Mathf.Sin(time * 2.2f) + 0.012f * Mathf.Max(0f, time - Build);
			float[] radii = new float[8];
			float[] rimR = new float[4], rimA = new float[4];
			for (int i = 0; i < waves.Length; i++)
			{
				float u = Ease.InOutCubic(Mathf.Clamp((time - waves[i].start) / waves[i].len, 0f, 1f));
				radii[i] = cover * u;
				// 前沿光边（我们自己的细节）：展开途中一道很淡的青白细光，到达后褪掉
				rimR[i] = radii[i];
				rimA[i] = u > 0f && u < 1f ? 0.35f * Mathf.Sin(Mathf.Pi * u) : 0f;
			}
			// 两道环带：外沿先长、内沿随后跟上（像一圈波纹），高潮前后到位；之后带着惯性继续慢慢外扩、速度逐渐衰减（用户：不是马上定格）
			float drift = 0.07f * (1f - Mathf.Exp(-Mathf.Max(0f, time - 0.8f) / 0.7f));
			float uo = Ease.OutCubic(Mathf.Clamp((time - 0.36f) / 0.5f, 0f, 1f));
			float ui = Ease.OutCubic(Mathf.Clamp((time - 0.44f) / 0.46f, 0f, 1f));
			SetAnnulus(outer, c, outOut * (breathe + drift) * uo, outIn * (breathe + drift) * ui);
			radii[4] = outOut * (breathe + drift) * uo;
			radii[5] = Mathf.Min(outIn * (breathe + drift) * ui, radii[4]);
			float ro = Ease.OutCubic(Mathf.Clamp((time - 0.42f) / 0.48f, 0f, 1f));
			float ri = Ease.OutCubic(Mathf.Clamp((time - 0.5f) / 0.42f, 0f, 1f));
			SetAnnulus(ring, c, ringOut * (breathe + drift) * ro, ringIn * (breathe + drift + 0.008f * Mathf.Sin(time * 3.1f)) * ri);
			radii[6] = ringOut * (breathe + drift) * ro;
			radii[7] = Mathf.Min(ringIn * (breathe + drift + 0.008f * Mathf.Sin(time * 3.1f)) * ri, radii[6]);
			rm.SetShaderParameter("center", c);
			rm.SetShaderParameter("radii", radii);
			rm.SetShaderParameter("rim_r", rimR);
			rm.SetShaderParameter("rim_a", rimA);

			// 背景大字：高潮那一刻起从左侧缓缓滑入（0.7s，用户：切入太快），之后慢慢右移
			float since = time - Build;
			float wi = Ease.OutCubic(Mathf.Clamp(since / 0.7f, 0f, 1f));
			Vector2 wp = wordAt + new Vector2(-W * 0.3f * (1f - wi) + 18f * Mathf.Max(0f, since), 0f);
			foreach ((Node2D w, Vector2 off, float _) in words)
			{
				w.Position = wp + off + new Vector2(off.X * 0.4f * (1f - wi), 0f);    // 回声那行比主行稍慢一点到位
				w.Visible = since >= 0f;
				w.Modulate = new Color(1f, 1f, 1f, wi);
			}
			n.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InOutSine(Mathf.Clamp((time - (life - 0.4f)) / 0.4f, 0f, 1f)));
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
			heroRoot.Scale = Vector2.One * hsc;
			heroRoot.Position = faceAt - faceOff * hs * hsc + new Vector2(W * 0.06f * (1f - enter) - 12f * Mathf.Max(0f, time), 3f * Mathf.Sin(raw * 1.6f));
			heroRoot.Modulate = new Color(1f, 1f, 1f, Ease.OutCubic(Mathf.Clamp(raw / 0.3f, 0f, 1f)));
			float lift = Ease.InOutSine(Mathf.Clamp((time - 0.25f) / 0.4f, 0f, 1f));
			if (heroDark != null)
				heroDark.Modulate = new Color(1f, 1f, 1f, 1f - lift);
			flash.Modulate = new Color(1f, 1f, 1f, 0.15f * Mathf.Sin(Mathf.Pi * lift));
			f.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InOutSine(Mathf.Clamp((raw - (total - 0.4f)) / 0.4f, 0f, 1f)));
		});
		Fx.FreeAfter(f, total + 0.05f);
	}
}
