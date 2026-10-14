using System.Collections.Generic;
using Godot;

namespace P3Makoto.Vfx;

/// <summary>
/// 总攻击（All-Out Attack）：精英 / Boss 战的最后一击之后、胜利结算之前播放（AllOutPatch 把战斗结束流程推迟 Duration 秒；
/// 播了总攻击就不再播普通胜利演出）。
/// v0.14.16（用户：只要「怪物倒下处展开同心圆 → 顺势切到结算立绘」；同心圆层数、节奏、音效都照原版；字幕要做进背景）：约 3.65s
///   音效：只有 Snd_FX_Btl_Allout_Zoom（把原版结算片段和全部 P3R 音效库做互相关得出），前 0.7s 是蓄力、0.7s 到达高潮。
///   0.30  蓄力 = 湖面水波（圆心在画面右上角外）：第一滴落下，三圈边界先后冲出 —— 深一档蓝前沿扫满全屏、浅蓝、中蓝，
///         从左到右是「深 · 浅 · 深」；0.36s 后圆心再落一滴，新的浅蓝冲出来把上一轮推开、挤窄，压力一圈圈往外传（环带弹簧模型）
///   1.00  高潮：结算立绘切入（1.3 倍以脸为支点拉回、暗蓝平滑亮起）；不再滴水，余波回弹、慢慢外扩
///         背景大字 MAJESTIC：深蓝细线空心字，落在深色带里的那段换成深蓝实心（遮罩跟着水波实时走）
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

	/// <summary>一圈水波的边界：Spawn 时刻从圆心冲出（初速 V0）；Rest = 它和外面那圈之间的环带"想要"的宽度；Col = 圈内的颜色。</summary>
	private readonly record struct Wave(float Spawn, float V0, float Rest, Color Col, bool Dark);

	/// <summary>
	/// 环带弹簧模型：每圈边界是一个质点（半径 + 速度），相邻两圈之间的环带是弹簧（抗压缩，几乎不抗拉伸），带阻尼，速度不低于匀速外漂。
	/// 新水滴 = 圆心冲出新的边界 —— 它追上里面那圈时把环带压窄，压力一圈圈往外传（推动、挤压），之后回弹、慢慢散开。
	/// 每帧从 0 开始以 1/240s 步长重算（确定性，几百步、开销很小）。返回各圈半径（外 → 内，未出生的为 -1）。
	/// </summary>
	private static float[] Ripple(IReadOnlyList<Wave> waves, float time)
	{
		const float K = 60f, Damp = 2.4f, Drift = 25f, Dt = 1f / 240f;
		int n = waves.Count;
		float[] r = new float[n], v = new float[n];
		bool[] alive = new bool[n];
		for (float t = 0f; t < time; t += Dt)
		{
			for (int i = 0; i < n; i++)
			{
				if (!alive[i] && waves[i].Spawn <= t)
				{
					alive[i] = true;
					v[i] = waves[i].V0;
				}
			}
			float[] a = new float[n];
			for (int i = 0; i < n; i++)
			{
				a[i] = -Damp * v[i];
			}
			for (int i = 0; i + 1 < n; i++)
			{
				if (!alive[i + 1])
				{
					break;
				}
				float f = K * (waves[i + 1].Rest - (r[i] - r[i + 1]));
				if (f < 0f)
				{
					f *= 0.08f;                                         // 拉伸只给很弱的回拉
				}
				a[i] += f;
				a[i + 1] -= f;
			}
			for (int i = 0; i < n; i++)
			{
				if (alive[i])
				{
					v[i] = Mathf.Max(v[i] + a[i] * Dt, Drift);
					r[i] += v[i] * Dt;
				}
			}
			for (int i = 0; i + 1 < n; i++)
			{
				if (alive[i + 1] && r[i + 1] > r[i] - 2f)
				{
					r[i + 1] = r[i] - 2f;
				}
			}
		}
		for (int i = 0; i < n; i++)
		{
			if (!alive[i])
			{
				r[i] = -1f;
			}
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
		var waves = new List<Wave>
		{
			new(0.00f, 5200f * k, 0f, Band, true),                       // 第一滴：深色前沿（最快，扫满全屏）
			new(0.06f, 3600f * k, 430f * k, Base, false),               //         浅色
			new(0.12f, 2700f * k, 400f * k, RingBlue, true),            //         深色（离圆心最近）
			// 第二滴（用户：第二滴也要有自己的深色同心圆，不能只有浅色）：同样冲出"浅 · 深 · 浅"一组，推挤上一轮
			new(0.36f, 3800f * k, 340f * k, Base, false),
			new(0.42f, 3000f * k, 320f * k, RingBlue, true),
			new(0.48f, 2300f * k, 300f * k, Base, false),
		};
		const int MaskN = 3;

		ColorRect disc = new() { Size = new Vector2(W, H), MouseFilter = Control.MouseFilterEnum.Ignore, Material = new ShaderMaterial { Shader = Rings } };
		n.AddChild(disc);
		ShaderMaterial rm = (ShaderMaterial)disc.Material;
		rm.SetShaderParameter("size", new Vector2(W, H));
		rm.SetShaderParameter("center", center);
		Color[] colsAll = new Color[12];
		for (int i = 0; i < waves.Count; i++)
		{
			colsAll[i] = waves[i].Col;
		}
		rm.SetShaderParameter("cols", colsAll);

		// 背景大字：一份深蓝细线空心字铺满；每条深色带一个遮罩（只当裁剪、不画），里面放一份深色实心字
		Vector2 wordAt = new(-W * 0.01f, H * 0.52f);
		Color clear = new(0f, 0f, 0f, 0f);
		var words = new List<Node2D> { WordCopy(n, wordAt, H, clear, new Color(Navy, 0.85f)) };
		var masks = new List<Polygon2D>();
		for (int m = 0; m < MaskN; m++)
		{
			Polygon2D mask = Annulus(n, Colors.White);
			mask.ClipChildren = CanvasItem.ClipChildrenMode.Only;
			masks.Add(mask);
			words.Add(WordCopy(mask, wordAt, H, Ink, Ink));                 // 深色带里的实心字：不透明、比描边再深一点
		}

		float prevT = 0f;
		float[] prevR = new float[waves.Count];
		Fx.Animate(n, life, t =>
		{
			float time = t * life;
			float[] r = Ripple(waves, time);
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
			float wi = Ease.OutCubic(Mathf.Clamp(since / 0.7f, 0f, 1f));
			Vector2 wp = wordAt + new Vector2(-W * 0.3f * (1f - wi) + 18f * Mathf.Max(0f, since), 0f);
			float wa = Ease.OutCubic(Mathf.Clamp(since / 0.25f, 0f, 1f));
			foreach (Node2D w in words)
			{
				w.Position = wp;
				w.Visible = since >= 0f;
				w.Modulate = new Color(1f, 1f, 1f, wa);
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
