using System.Collections.Generic;
using Godot;

namespace P3Makoto.Vfx;

/// <summary>
/// 总攻击（All-Out Attack）：精英 / Boss 战的最后一击之后、胜利结算之前播放（AllOutPatch 把战斗结束流程推迟 Duration 秒；
/// 播了总攻击就不再播普通胜利演出）。
/// v0.14.16（用户：太长了，只要「怪物倒下的位置展开同心圆 → 顺势切到结算立绘」，配好音效，其他都不要）：约 3.7s
///   0.30  从怪物中心展开浅蓝 / 中蓝 / 深蓝同心大圆铺满画面，两道细圆弧转过                ♪ Btl_Allout_Wipe @0.20
///   1.05  同心圆的蓝底直接接成结算画面：青底从右上扫入、深蓝色带、刻度圆环、速度线、三角碎片，
///         立绘暗蓝滑入 → 0.22s 白闪亮起（原版 A2→A1），背后 MAJESTIC；0.5s「DONE AND DUSTED.」从左滑入   ♪ Kime01 高潮对准亮起
///   停留到 3.35 → 0.35s 淡出
/// 结束立绘用 P3R 原版贴图（allout_finish / allout_finish_dark / allout_text），缺图时退回 victory_portrait。
/// </summary>
public static class AllOut
{
	private const float IrisAt = 0.3f, FinishAt = 1.05f, FinishLen = 2.65f;
	public const float Duration = FinishAt + FinishLen;

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

		Audio.Sfx.Clip("Btl_Allout_Wipe", 0.85f, IrisAt - 0.1f);
		Audio.Sfx.Clip("Snd_FX_Btl_Allout_Kime01", 1.0f, FinishAt - 0.85f);           // Kime01 前 0.85s 是蓄势长音，0.85s 起爆 = 原版切入结算立绘那一帧

		Fx.After(root, IrisAt, "allout iris", () => Iris(root, focus, W, H));
		Fx.After(root, FinishAt, "allout finish", () => Finish(root, W, H));
		Fx.FreeAfter(root, Duration + 0.1f);
	}

	private static Vector2 Avg(IReadOnlyList<Vector2> pts)
	{
		Vector2 c = Vector2.Zero;
		foreach (Vector2 p in pts) c += p;
		return c / pts.Count;
	}

	// 从怪物中心展开的同心圆（盖住整个画面，接结算画面）
	private static void Iris(Node2D root, Vector2 focus, float W, float H)
	{
		Node2D n = new();
		root.AddChild(n);
		Color[] cols = { new(0.35f, 0.88f, 1f), new(0.12f, 0.45f, 0.95f), new(0.04f, 0.1f, 0.45f) };
		var discs = new List<Polygon2D>();
		for (int i = 0; i < 3; i++)
		{
			Polygon2D d = Fx.Poly(n, Fx.Circle(10f, 128), cols[i]);
			d.Position = focus;
			discs.Add(d);
		}
		Node2D rim = new() { Position = focus };
		n.AddChild(rim);
		Fx.StrokeClosed(rim, Fx.Circle(10f, 128), new Color(0.6f, 0.95f, 1f, 0.95f), 0.6f);
		Node2D arcs = new() { Position = focus };
		n.AddChild(arcs);
		P3Ui.Arc(arcs, Vector2.Zero, H * 0.7f, 0f, Mathf.Pi * 0.9f, new Color(0.45f, 0.9f, 1f, 0.8f), 4f);
		P3Ui.Arc(arcs, Vector2.Zero, H * 1.05f, Mathf.Pi, Mathf.Pi * 1.7f, new Color(1f, 1f, 1f, 0.45f), 2f);
		// 半径要盖住离圆心最远的屏幕角
		float R = 0f;
		foreach (Vector2 corner in new[] { Vector2.Zero, new Vector2(W, 0f), new Vector2(0f, H), new Vector2(W, H) })
		{
			R = Mathf.Max(R, (corner - focus).Length());
		}
		R *= 1.05f;
		const float total = 1.25f;
		Fx.Animate(n, total, t =>
		{
			float time = t * total;
			for (int i = 0; i < 3; i++)
			{
				float u = Ease.InOutCubic(Mathf.Clamp((time - 0.13f * i) / 0.55f, 0f, 1f));
				discs[i].Scale = Vector2.One * (R * u / 10f);
			}
			float ru = Ease.OutCubic(Mathf.Clamp(time / 0.5f, 0f, 1f));
			rim.Scale = Vector2.One * (R * 1.02f * ru / 10f);
			rim.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp((time - 0.45f) / 0.15f, 0f, 1f));
			arcs.Rotation = 0.6f * time;
			arcs.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp((time - 0.7f) / 0.15f, 0f, 1f));
			n.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp((time - 1.15f) / 0.1f, 0f, 1f));
		});
		Fx.FreeAfter(n, total + 0.05f);
	}

	private static Texture2D? Tex(string name) =>
		ResourceLoader.Exists($"res://p3makoto/ui/{name}.png") ? ResourceLoader.Load<Texture2D>($"res://p3makoto/ui/{name}.png") : null;

	private static void Finish(Node2D root, float W, float H)
	{
		Node2D f = new();
		root.AddChild(f);
		ColorRect bg = new() { Size = new Vector2(W, H), Color = new Color(0.25f, 0.72f, 0.95f, 0.97f), MouseFilter = Control.MouseFilterEnum.Ignore };
		f.AddChild(bg);
		// 斜切的深一档色带（P3R 结束画面左下的斜块）
		Polygon2D band = Fx.Poly(f, new[] { new Vector2(-40f, H * 0.78f), new Vector2(W * 0.62f, H * 0.52f), new Vector2(W * 0.7f, H + 40f), new Vector2(-40f, H + 40f) }, new Color(0.14f, 0.52f, 0.88f, 0.85f));

		Node2D ring = new() { Position = new Vector2(W * 0.66f, H * 0.5f) };
		f.AddChild(ring);
		Fx.StrokeClosed(ring, Fx.Circle(H * 0.56f, 128), new Color(0.12f, 0.45f, 0.9f, 0.9f), H * 0.1f, additive: false);
		Fx.StrokeClosed(ring, Fx.Circle(H * 0.43f, 128), new Color(1f, 1f, 1f, 0.55f), 3f);
		Fx.StrokeClosed(ring, Fx.Circle(H * 0.66f, 128), new Color(1f, 1f, 1f, 0.3f), 2f);
		// 圆环上的刻度
		for (int k = 0; k < 48; k++)
		{
			Vector2 d = Vector2.Right.Rotated(k * Mathf.Tau / 48f);
			float len = k % 4 == 0 ? 26f : 12f;
			Fx.Line(ring, new[] { d * (H * 0.62f), d * (H * 0.62f + len) }, new Color(1f, 1f, 1f, k % 4 == 0 ? 0.6f : 0.3f), k % 4 == 0 ? 3f : 2f);
		}

		Node2D word = new() { Position = new Vector2(W * 0.02f, H * 0.62f), Rotation = -0.12f, Skew = -0.25f };
		f.AddChild(word);
		Typo.Text(word, "MAJESTIC", Typo.LatinItalic, (int)(H * 0.3f), new Color(0.03f, 0.08f, 0.3f, 0.92f), Vector2.Zero);

		Texture2D? lit = Tex("allout_finish") ?? Tex("victory_portrait");
		Texture2D? dark = Tex("allout_finish_dark");
		Node2D heroRoot = new();
		f.AddChild(heroRoot);
		float hs = lit != null ? H * 1.0f / lit.GetHeight() : 1f;
		Sprite2D hero = new() { Texture = lit, Scale = Vector2.One * hs };
		heroRoot.AddChild(hero);
		Sprite2D? heroDark = dark != null ? new Sprite2D { Texture = dark, Scale = Vector2.One * hs } : null;
		if (heroDark != null)
			heroRoot.AddChild(heroDark);
		// 立绘的白色剪影闪（亮起那一下）
		Sprite2D flash = new() { Texture = lit, Scale = Vector2.One * hs, Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add }, Modulate = new Color(1f, 1f, 1f, 0f) };
		heroRoot.AddChild(flash);
		Vector2 heroAt = new(W * 0.65f, H * 0.56f);
		// 脸（两眼中点）相对立绘中心的偏移（原图像素）；缩放时让它停在最终位置上
		Vector2 faceOff = lit != null ? new Vector2(1075f, 262f) - new Vector2(lit.GetWidth(), lit.GetHeight()) / 2f : Vector2.Zero;
		Vector2 faceAt = heroAt + faceOff * hs;

		Texture2D? txt = Tex("allout_text");
		Node2D text = new() { Position = new Vector2(W * 0.2f, H * 0.21f) };
		f.AddChild(text);
		float ts = H * 0.3f / (txt?.GetHeight() ?? 241);
		if (txt != null)
		{
			// 深蓝错位阴影 + 原版字图
			text.AddChild(new Sprite2D { Texture = txt, Scale = Vector2.One * ts, Position = new Vector2(8f, 8f), Modulate = new Color(0.03f, 0.08f, 0.3f, 0.85f) });
			text.AddChild(new Sprite2D { Texture = txt, Scale = Vector2.One * ts });
		}
		else
		{
			int fs = (int)(H * 0.075f);
			Typo.Text(text, "DONE", Typo.LatinItalic, fs, Pal.White, new Vector2(0f, 0f), outline: 4, outlineColor: new Color(0.03f, 0.08f, 0.3f));
			Typo.Text(text, "AND", Typo.LatinItalic, fs, Pal.White, new Vector2(fs * 0.4f, fs * 0.9f), outline: 4, outlineColor: new Color(0.03f, 0.08f, 0.3f));
			Typo.Text(text, "DUSTED.", Typo.LatinItalic, fs, Pal.White, new Vector2(0f, fs * 1.8f), outline: 4, outlineColor: new Color(0.03f, 0.08f, 0.3f));
		}
		Sprite2D textFlash = new() { Texture = txt, Scale = Vector2.One * ts, Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add } };
		if (txt != null)
			text.AddChild(textFlash);


		const float total = FinishLen;
		// 节奏照原版（video1 18.73s 切入，逐帧量）：同心圆铺满后硬切进结算画面（无白闪）；立绘切入时 1.3 倍、带蓝色调，
		// 之后整段都在缓慢后拉 + 漂移（一刻不停 = 丝滑）；0.25–0.65s 平滑亮起（不是一下闪白）；MAJESTIC 0.3s 内从左滑入；
		// 0.67s「DONE AND DUSTED.」从左侧快速甩入（0.12s）并白闪一下；圆环、速度线、碎片持续慢动
		Fx.Animate(f, total, t =>
		{
			float time = t * total;
			float ease = Ease.OutCubic(Mathf.Clamp(time / 0.7f, 0f, 1f));            // 镜头后拉：切入时 1.3 倍，0.7s 内拉到原大，之后一直慢漂
			bg.Position = Vector2.Zero;
			band.Position = new Vector2(-W * 0.12f * (1f - Ease.OutCubic(Mathf.Clamp(time / 0.4f, 0f, 1f))), 0f);
			// 以脸为支点缩放（原版：拉镜头时脸基本不动，身体往画面外收）
			float hsc = 1.3f - 0.3f * ease - 0.015f * time;
			heroRoot.Scale = Vector2.One * hsc;
			heroRoot.Position = faceAt - faceOff * hs * hsc + new Vector2(-12f * time, 3f * Mathf.Sin(time * 1.6f));
			// 暗蓝 → 亮起：平滑过渡 + 很淡的一层白
			float lift = Ease.InOutSine(Mathf.Clamp((time - 0.25f) / 0.4f, 0f, 1f));
			if (heroDark != null)
				heroDark.Modulate = new Color(1f, 1f, 1f, 1f - lift);
			flash.Modulate = new Color(1f, 1f, 1f, 0.15f * Mathf.Sin(Mathf.Pi * lift));
			ring.Rotation = time * 0.12f;
			ring.Scale = Vector2.One * (1.1f - 0.1f * ease);
			float wi = Ease.OutCubic(Mathf.Clamp(time / 0.3f, 0f, 1f));
			word.Position = new Vector2(W * 0.02f - W * 0.35f * (1f - wi) + 18f * time, H * 0.62f);
			word.Modulate = new Color(1f, 1f, 1f, wi);
			// 字图（原版）：0.67s 从左侧甩入，0.12s 到位（OutBack 轻微过冲）+ 白闪
			float tp = Mathf.Clamp((time - 0.67f) / 0.12f, 0f, 1f);
			text.Modulate = new Color(1f, 1f, 1f, tp > 0f ? 1f : 0f);
			text.Position = new Vector2(W * 0.2f - W * 0.4f * (1f - Ease.OutBack(tp)) + 6f * Mathf.Max(0f, time - 0.79f), H * 0.21f);
			textFlash.Modulate = new Color(1f, 1f, 1f, time < 0.79f ? 0f : 0.8f * Mathf.Max(0f, 1f - (time - 0.79f) / 0.18f));
			f.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InOutSine(Mathf.Clamp((time - (total - 0.4f)) / 0.4f, 0f, 1f)));
		});
		Fx.FreeAfter(f, total + 0.05f);
	}
}
