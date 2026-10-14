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
	private static readonly Color Band = new(23 / 255f, 97 / 255f, 175 / 255f);       // 原版左下深一档的蓝

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
uniform float radii[12];
uniform vec4 cols[12];
uniform float rim_r[4];
uniform float rim_a[4];
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

	/// <summary>一滴水：t0 时刻从圆心落下，圈内是 col；水波先快后慢（粘滞衰减）再带一点匀速外漂，永不定住。</summary>
	private readonly record struct Drop(float T0, Color Col, float Reach, float Tau, bool Dark);

	/// <summary>
	/// 背景 = 湖面水波（用户：怪物死的位置像滴下一滴水，水波向外扩散、互相推挤；过一会儿再滴一滴，推着上一轮走）：
	///   蓄力 0.7s：四滴紧接着落下（深蓝 → 中蓝 → 浅青 → 底色浅蓝），一波推一波铺满画面；
	///   高潮之后：每隔 0.5–0.7s 再落一滴（深浅交替），新波纹追上旧波纹时把它往外推、挤窄旧的那一圈，旧圈出了画面就不再画。
	///   没有常驻的圆环 —— 画面上的深色区域都是正在经过的波。
	///   背景大字 MAJESTIC：整体是深蓝细线空心字；落在深色波里的那一段由跟着波走的遮罩换成深蓝实心（实时）。
	/// </summary>
	private static void Background(Node2D n, Vector2 focus, float W, float H, float life)
	{
		float cover = 0f;
		foreach (Vector2 corner in new[] { Vector2.Zero, new Vector2(W, 0f), new Vector2(0f, H), new Vector2(W, H) })
		{
			cover = Mathf.Max(cover, (corner - focus).Length());
		}
		cover *= 1.05f;
		// 落水表：Reach = 这一波最终能走多远（越大越快），Tau = 减速快慢
		var drops = new List<Drop>
		{
			new(0.00f, Deep, cover * 2.2f, 0.35f, true),
			new(0.12f, RingBlue, cover * 2.0f, 0.35f, true),
			new(0.24f, Cyan, cover * 1.9f, 0.35f, false),
			new(0.36f, Base, cover * 1.8f, 0.35f, false),
		};
		Color[] cycle = { RingBlue, Base, Band, Cyan };                 // 深浅交替：背景字总有一部分落在深色波里
		float at = Build + 0.12f;
		for (int k = 0; at < life; k++)
		{
			Color col = cycle[k % cycle.Length];
			drops.Add(new Drop(at, col, cover * (k % 2 == 0 ? 1.15f : 1.3f), 0.75f, col == RingBlue || col == Band));
			at += k % 2 == 0 ? 0.5f : 0.7f;
		}
		const int Slots = 12, MaskN = 4;
		float minGap = H * 0.045f;                                     // 被挤压时一圈最窄能有多宽

		ColorRect disc = new() { Size = new Vector2(W, H), MouseFilter = Control.MouseFilterEnum.Ignore, Material = new ShaderMaterial { Shader = Rings } };
		n.AddChild(disc);
		ShaderMaterial rm = (ShaderMaterial)disc.Material;
		rm.SetShaderParameter("size", new Vector2(W, H));
		rm.SetShaderParameter("center", focus);

		// 背景大字：一份深蓝细线空心字铺满；深色波各自一个遮罩（只当裁剪、不画），里面放一份深蓝实心字
		Vector2 wordAt = new(-W * 0.01f, H * 0.52f);
		Color clear = new(0f, 0f, 0f, 0f);
		var words = new List<Node2D> { WordCopy(n, wordAt, H, clear, new Color(Navy, 0.85f)) };
		var masks = new List<Polygon2D>();
		for (int m = 0; m < MaskN; m++)
		{
			Polygon2D mask = Annulus(n, Colors.White);
			mask.ClipChildren = CanvasItem.ClipChildrenMode.Only;
			masks.Add(mask);
			words.Add(WordCopy(mask, wordAt, H, new Color(Navy, 0.92f), new Color(Navy, 0.92f)));
		}

		Fx.Animate(n, life, t =>
		{
			float time = t * life;
			// 每一滴的"自由"半径（先快后慢 + 匀速外漂），再从新到旧推挤：旧的至少比新的大 minGap
			int live = 0;
			float[] r = new float[drops.Count];
			for (int k = drops.Count - 1; k >= 0; k--)
			{
				float dt = time - drops[k].T0;
				if (dt <= 0f)
				{
					r[k] = -1f;
					continue;
				}
				float free = drops[k].Reach * (1f - Mathf.Exp(-dt / drops[k].Tau)) + 40f * dt;
				float pushed = -1f;
				for (int q = k + 1; q < drops.Count; q++)
				{
					if (r[q] > 0f)
					{
						pushed = r[q] + minGap;                        // 紧挨着的下一滴（更新的那滴）
						break;
					}
				}
				r[k] = Mathf.Max(free, pushed);
			}
			// 画面上要画的：最老的那张"已经盖满全屏"的圆当底，往后所有落下的圆依次叠上去
			int first = 0;
			for (int k = 0; k < drops.Count; k++)
			{
				if (r[k] >= cover)
				{
					first = k;
				}
			}
			float[] radii = new float[Slots];
			Color[] cols = new Color[Slots];
			float[] rimR = new float[4], rimA = new float[4];
			int rim = 0;
			for (int k = first; k < drops.Count && live < Slots; k++)
			{
				if (r[k] <= 0f)
				{
					break;
				}
				radii[live] = Mathf.Min(r[k], cover * 1.5f);
				cols[live] = drops[k].Col;
				live++;
				// 波前细光边（我们自己的细节）：刚落下、还在快速外扩的那几圈带一道很淡的青白细光
				float dt = time - drops[k].T0;
				if (rim < 4 && dt < 0.6f)
				{
					rimR[rim] = r[k];
					rimA[rim] = 0.3f * (1f - dt / 0.6f) * Mathf.Clamp(dt / 0.05f, 0f, 1f);
					rim++;
				}
			}
			rm.SetShaderParameter("radii", radii);
			rm.SetShaderParameter("cols", cols);
			rm.SetShaderParameter("count", live);
			rm.SetShaderParameter("rim_r", rimR);
			rm.SetShaderParameter("rim_a", rimA);
			// 深色波的环形区域 = 这一滴的圆减去下一滴（更新的）圆
			int mi = 0;
			for (int k = first; k < drops.Count && mi < MaskN; k++)
			{
				if (r[k] <= 0f || !drops[k].Dark)
				{
					continue;
				}
				float inner = k + 1 < drops.Count && r[k + 1] > 0f ? r[k + 1] : 0f;
				SetAnnulus(masks[mi++], focus, Mathf.Min(r[k], cover * 1.5f), inner);
			}
			for (; mi < MaskN; mi++)
			{
				SetAnnulus(masks[mi], focus, 0f, 0f);
			}

			// 背景大字：高潮那一刻起从左侧缓缓滑入（0.7s），之后慢慢右移
			float since = time - Build;
			float wi = Ease.OutCubic(Mathf.Clamp(since / 0.7f, 0f, 1f));
			Vector2 wp = wordAt + new Vector2(-W * 0.3f * (1f - wi) + 18f * Mathf.Max(0f, since), 0f);
			foreach (Node2D w in words)
			{
				w.Position = wp;
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
