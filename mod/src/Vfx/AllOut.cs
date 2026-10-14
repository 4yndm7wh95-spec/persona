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
///         背景大字 MAJESTIC 和「DONE AND DUSTED.」的阴影做在背景里：落在哪一圈，就用哪一圈的配色 —— 用随圆环实时变化的裁剪实现，
///         不是预先画好的图（原版截图里深蓝环上的字是浅色、浅蓝底上的字是深色）
///   1.67  「DONE AND DUSTED.」从左侧甩入（0.12s）+ 白闪
///   停留到 3.25 → 0.4s 整体淡出
/// 结束立绘用 P3R 原版贴图（allout_finish / allout_finish_dark / allout_text），缺图时退回 victory_portrait。
/// </summary>
public static class AllOut
{
	private const float IrisAt = 0.3f, Build = 0.7f, FinishAt = IrisAt + Build, FinishLen = 2.65f;
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
		Fx.After(root, FinishAt, "allout finish", () => Portrait(fg, W, H));
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

	// ── 环带：普通 Polygon2D（外圈 N 点 + 内圈 N 点，每段一个四边形），每帧只改顶点 ──
	private const int Seg = 120;

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
	private static Node2D WordCopy(Node parent, Vector2 at, float H, Color color)
	{
		Node2D w = new() { Position = at, Rotation = -0.12f, Skew = -0.25f };
		parent.AddChild(w);
		Typo.Text(w, "MAJESTIC", Typo.LatinItalic, (int)(H * 0.36f), color, Vector2.Zero);
		return w;
	}

	private static Sprite2D? ShadowCopy(Node parent, Texture2D? txt, float ts, Color color)
	{
		if (txt == null)
		{
			return null;
		}
		Sprite2D s = new() { Texture = txt, Scale = Vector2.One * ts, Modulate = color };
		parent.AddChild(s);
		return s;
	}

	/// <summary>
	/// 背景（同心圆 → 结算页底）+ 做进背景里的字：
	///   四波整屏圆从敌人中心依次涌出（每波前沿带细光边），两道环带随后长出；圆心滑到圆环位置后一直缓慢漂移、呼吸。
	///   MAJESTIC / DONE AND DUSTED 的阴影：先画一份"浅蓝底上的配色"，环带本身开 ClipChildren，把同一段字的另一种配色画在环带里 ——
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
		(Color col, float start, float len)[] waves = { (Deep, 0f, 0.32f), (RingBlue, 0.1f, 0.34f), (Cyan, 0.2f, 0.36f), (Base, 0.3f, 0.38f) };
		var discs = new List<Polygon2D>();
		foreach (var w in waves)
		{
			discs.Add(Fx.Poly(n, Fx.Circle(10f, 160), w.col));
		}
		// 外圈浅青环（大）+ 内圈深蓝粗环（结算页的主圆环，原版截图量：内 0.55H、外 0.66H）
		float outIn = H * 1.02f, outOut = H * 1.2f, ringIn = H * 0.55f, ringOut = H * 0.66f;
		Polygon2D outer = Annulus(n, Cyan);
		Polygon2D ring = Annulus(n, RingBlue);
		outer.ClipChildren = CanvasItem.ClipChildrenMode.AndDraw;
		ring.ClipChildren = CanvasItem.ClipChildrenMode.AndDraw;
		var rims = new List<Polygon2D>();
		foreach (var _ in waves)
		{
			rims.Add(Annulus(n, new Color(0.85f, 0.98f, 1f, 0f)));
		}

		// 背景大字 MAJESTIC：底色区深蓝字，外圈浅青环里是中蓝字，深蓝粗环里是浅青字
		Vector2 wordAt = new(-W * 0.01f, H * 0.52f);
		Node2D wordBase = WordCopy(n, wordAt, H, new Color(Navy, 0.9f));
		n.MoveChild(wordBase, outer.GetIndex());                     // 在两道环带下面、底色上面
		Node2D wordOuter = WordCopy(outer, wordAt, H, new Color(RingBlue, 0.95f));
		Node2D wordRing = WordCopy(ring, wordAt, H, new Color(Cyan, 0.95f));

		// 「DONE AND DUSTED.」的错位阴影也做进背景：底色区深蓝、环带里换成环带的深一档
		Texture2D? txt = Tex("allout_text");
		float ts = H * 0.3f / (txt?.GetHeight() ?? 241);
		Vector2 shadowOff = new(8f, 8f);
		Sprite2D? shBase = ShadowCopy(n, txt, ts, new Color(Navy, 0.9f));
		if (shBase != null) n.MoveChild(shBase, outer.GetIndex());
		Sprite2D? shOuter = ShadowCopy(outer, txt, ts, new Color(RingBlue, 0.95f));
		Sprite2D? shRing = ShadowCopy(ring, txt, ts, new Color(Navy, 0.95f));

		Fx.Animate(n, life, t =>
		{
			float time = t * life;
			// 圆心：蓄力期间从敌人滑到圆环位置；之后一直缓慢漂移
			Vector2 c = focus.Lerp(ringAt, Ease.InOutCubic(Mathf.Clamp(time / Build, 0f, 1f))) + new Vector2(-10f, -3f) * Mathf.Max(0f, time - Build);
			float breathe = 1f + 0.015f * Mathf.Sin(time * 2.2f) + 0.012f * Mathf.Max(0f, time - Build);
			for (int i = 0; i < waves.Length; i++)
			{
				float u = Ease.InOutCubic(Mathf.Clamp((time - waves[i].start) / waves[i].len, 0f, 1f));
				float r = cover * u;
				discs[i].Position = c;
				discs[i].Scale = Vector2.One * (r / 10f);
				// 前沿光边：展开途中亮，到达后褪掉
				float rimA = u > 0f && u < 1f ? 0.75f * Mathf.Sin(Mathf.Pi * u) : 0f;
				rims[i].Color = new Color(0.85f, 0.98f, 1f, rimA);
				SetAnnulus(rims[i], c, r, r - 3f - 5f * u);
			}
			// 两道环带：外沿先长、内沿随后跟上（像一圈波纹定住），0.7s 落定
			float uo = Ease.OutCubic(Mathf.Clamp((time - 0.4f) / 0.3f, 0f, 1f));
			float ui = Ease.OutCubic(Mathf.Clamp((time - 0.47f) / 0.23f, 0f, 1f));
			SetAnnulus(outer, c, outOut * breathe * uo, outIn * breathe * ui);
			float ro = Ease.OutCubic(Mathf.Clamp((time - 0.45f) / 0.25f, 0f, 1f));
			float ri = Ease.OutCubic(Mathf.Clamp((time - 0.52f) / 0.18f, 0f, 1f));
			SetAnnulus(ring, c, ringOut * breathe * ro, ringIn * (breathe + 0.008f * Mathf.Sin(time * 3.1f)) * ri);

			// 背景大字：高潮那一刻从左侧滑入（0.3s），之后慢慢右移
			float since = time - Build;
			float wi = Ease.OutCubic(Mathf.Clamp(since / 0.3f, 0f, 1f));
			Vector2 wp = wordAt + new Vector2(-W * 0.35f * (1f - wi) + 18f * Mathf.Max(0f, since), 0f);
			foreach (Node2D w in new[] { wordBase, wordOuter, wordRing })
			{
				w.Position = wp;
				w.Visible = since >= 0f;
				w.Modulate = new Color(1f, 1f, 1f, wi);
			}
			// 阴影跟着字图一起甩入（字图本体在 Portrait 里，时刻一致）
			float tp = Mathf.Clamp((since - 0.67f) / 0.12f, 0f, 1f);
			Vector2 tpos = TextPos(W, H, since, tp) + shadowOff;
			foreach (Sprite2D? s in new[] { shBase, shOuter, shRing })
			{
				if (s != null)
				{
					s.Position = tpos;
					s.Visible = tp > 0f;
				}
			}
			n.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InOutSine(Mathf.Clamp((time - (life - 0.4f)) / 0.4f, 0f, 1f)));
		});
		Fx.FreeAfter(n, life + 0.05f);
	}

	/// <summary>「DONE AND DUSTED.」的位置：高潮后 0.67s 从左侧甩入（0.12s，OutBack 轻微过冲），之后慢慢右移。</summary>
	private static Vector2 TextPos(float W, float H, float since, float tp) =>
		new(W * 0.2f - W * 0.4f * (1f - Ease.OutBack(tp)) + 6f * Mathf.Max(0f, since - 0.79f), H * 0.21f);

	/// <summary>结算立绘 + 「DONE AND DUSTED.」本体（阴影和背景大字在 Background 里）。</summary>
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

		Texture2D? txt = Tex("allout_text");
		Node2D text = new();
		f.AddChild(text);
		float ts = H * 0.3f / (txt?.GetHeight() ?? 241);
		if (txt != null)
		{
			text.AddChild(new Sprite2D { Texture = txt, Scale = Vector2.One * ts });
		}
		else
		{
			int fs = (int)(H * 0.075f);
			Typo.Text(text, "DONE", Typo.LatinItalic, fs, Pal.White, new Vector2(0f, 0f), outline: 4, outlineColor: Navy);
			Typo.Text(text, "AND", Typo.LatinItalic, fs, Pal.White, new Vector2(fs * 0.4f, fs * 0.9f), outline: 4, outlineColor: Navy);
			Typo.Text(text, "DUSTED.", Typo.LatinItalic, fs, Pal.White, new Vector2(0f, fs * 1.8f), outline: 4, outlineColor: Navy);
		}
		Sprite2D textFlash = new() { Texture = txt, Scale = Vector2.One * ts, Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add } };
		if (txt != null)
			text.AddChild(textFlash);

		const float total = FinishLen;
		// 节奏照原版（video1 18.73s 切入，逐帧量）：立绘切入时 1.3 倍、带蓝色调，以脸为支点 0.7s 拉回原大，之后一直慢漂；
		// 0.25–0.65s 平滑亮起（不是一下闪白）；0.67s「DONE AND DUSTED.」从左侧快速甩入并白闪一下
		Fx.Animate(f, total, t =>
		{
			float time = t * total;
			float ease = Ease.OutCubic(Mathf.Clamp(time / 0.7f, 0f, 1f));
			float hsc = 1.3f - 0.3f * ease - 0.015f * time;
			heroRoot.Scale = Vector2.One * hsc;
			heroRoot.Position = faceAt - faceOff * hs * hsc + new Vector2(-12f * time, 3f * Mathf.Sin(time * 1.6f));
			float lift = Ease.InOutSine(Mathf.Clamp((time - 0.25f) / 0.4f, 0f, 1f));
			if (heroDark != null)
				heroDark.Modulate = new Color(1f, 1f, 1f, 1f - lift);
			flash.Modulate = new Color(1f, 1f, 1f, 0.15f * Mathf.Sin(Mathf.Pi * lift));
			float tp = Mathf.Clamp((time - 0.67f) / 0.12f, 0f, 1f);
			text.Visible = tp > 0f;
			text.Position = TextPos(W, H, time, tp);
			textFlash.Modulate = new Color(1f, 1f, 1f, time < 0.79f ? 0f : 0.8f * Mathf.Max(0f, 1f - (time - 0.79f) / 0.18f));
			f.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InOutSine(Mathf.Clamp((time - (total - 0.4f)) / 0.4f, 0f, 1f)));
		});
		Fx.FreeAfter(f, total + 0.05f);
	}
}
