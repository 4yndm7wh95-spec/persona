using System;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace P3Makoto.Vfx;

/// <summary>
/// 战斗"舞台"演出，全部用 P3R 菜单的视觉语言（P3Ui：色块拼接、水之圆、超大斜体字、红字标签、细圆弧）。
/// 每个演出都有完整的入 / 停 / 出：入场 OutExpo（快进慢停）、停留时缓慢漂移、出场 InExpo（慢起快走）+ 最后淡出，
/// 不出现"一下出现 / 一下消失"。
/// </summary>
public static class StageFx
{
	private static float Seg(float time, float start, float len) => Mathf.Clamp((time - start) / len, 0f, 1f);

	// ───────────────────────── 回合横幅 ─────────────────────────
	/// <summary>
	/// 回合开始横幅（替换原版 "Your Turn / Enemy Turn"）：整组向左上倾斜 6°。
	/// 玩家回合：深蓝长色块从左切入、白色色块从右切入，白块上深蓝粗斜体 "PLAYER TURN"；左侧水之圆里是回合数。
	/// 敌人回合：深蓝 + 暗红色块，白字 "ENEMY TURN"，圆弧变红。
	/// 时间：入 0.28s → 停 0.55s → 出 0.26s（色块沿各自方向继续切出、纵向压扁）。
	/// </summary>
	public static void TurnBanner(Control layer, bool player, int round)
	{
		// v0.9.5：不再横贯屏幕中央 —— 收成左上角的一条短色带（顶栏下方），画在背景特效层（人物后面），淡一些、停得短一些
		Vector2 size = layer.GetViewportRect().Size;
		float cy = size.Y * 0.2f, h = size.Y * 0.075f;
		Node2D root = Fx.Root(layer, new Vector2(0f, cy), "P3TurnBanner");
		root.Rotation = -0.05f;
		Color slabB = player ? new Color(0.95f, 0.97f, 1f, 0.9f) : new Color(0.32f, 0.03f, 0.1f, 0.9f);
		Color word = player ? new Color(0.03f, 0.08f, 0.3f) : Pal.White;
		Color accent = player ? new Color(Pal.Cyan, 0.9f) : new Color(0.95f, 0.15f, 0.3f, 0.9f);
		float w = size.X * 0.36f;

		Node2D navy = new();
		root.AddChild(navy);
		P3Ui.Slab(navy, -200f, w, -h * 0.5f, h * 0.5f, h * 0.5f, new Color(0.02f, 0.06f, 0.24f, 0.82f));
		P3Ui.Slab(navy, -200f, w, h * 0.5f, h * 0.5f + 4f, 2f, accent);
		Node2D slab = new();
		root.AddChild(slab);
		P3Ui.Slab(slab, size.X * 0.06f, w - size.X * 0.03f, -h * 0.36f, h * 0.36f, h * 0.36f, slabB);
		Node2D red = new() { Position = new Vector2(size.X * 0.06f - 8f, h * 0.36f) };
		root.AddChild(red);
		P3Ui.Slab(red, 0f, h * 0.12f, -h * 0.72f, 0f, h * 0.36f, new Color(0.9f, 0.08f, 0.22f, 0.95f));

		Node2D text = new() { Skew = -0.25f, Position = new Vector2(size.X * 0.09f, 0f) };
		root.AddChild(text);
		int fs = (int)(h * 0.56f);
		Typo.Text(text, player ? "PLAYER TURN" : "ENEMY TURN", Typo.Latin, fs, word, new Vector2(0f, -fs * 0.62f),
			shadow: new Color(0.9f, 0.08f, 0.22f, player ? 0.85f : 0.0f), shadowOffset: new Vector2(4f, 3f));

		float discR = h * 0.95f;
		Vector2 discAt = new(Mathf.Max(size.X * 0.035f, discR * 1.2f + 10f), 0f);   // 整个圆（含外圈弧线）都在屏幕内
		// 平涂双色圆（小尺寸下水之圆的焦散会变成一团团暗斑，这里不用）：底色 + 偏右上的亮面 + 白色细边
		Node2D disc = new() { Position = discAt };
		root.AddChild(disc);
		Color baseC = player ? new Color(0.03f, 0.1f, 0.38f) : new Color(0.3f, 0.02f, 0.09f);
		Color litC = player ? new Color(0.12f, 0.42f, 0.95f) : new Color(0.62f, 0.06f, 0.16f);
		Fx.Poly(disc, Fx.Circle(discR, 64), baseC);
		Polygon2D lit = Fx.Poly(disc, Fx.Circle(discR * 0.8f, 64), litC);
		lit.Position = new Vector2(discR * 0.14f, -discR * 0.14f);
		Fx.StrokeClosed(disc, Fx.Circle(discR, 64), Pal.White, 3f, additive: false);
		Node2D arcs = new() { Position = discAt };
		root.AddChild(arcs);
		P3Ui.Arc(arcs, Vector2.Zero, discR * 1.12f, 0f, Mathf.Pi * 1.3f, accent, 2.5f);
		Node2D num = new() { Position = discAt, Skew = -0.2f };
		root.AddChild(num);
		// 圆里总有内容：我方回合 = 回合数，敌方回合 = 红色感叹号（之前敌方回合这里是空的）
		string glyph = player ? round.ToString() : "!";
		int ns = (int)(discR * (player ? 1.0f : 1.2f));
		if (player)
		{
			Typo.Text(num, "TURN", Typo.Latin, (int)(discR * 0.26f), new Color(Pal.Ice, 0.9f), new Vector2(-discR * 0.5f, -discR * 0.82f));
		}
		Typo.Text(num, glyph, Typo.Latin, ns, Pal.White, new Vector2(-ns * 0.3f * glyph.Length, -ns * 0.6f),
			shadow: player ? new Color(0.02f, 0.06f, 0.24f, 0.9f) : new Color(0.12f, 0f, 0.03f, 0.9f), shadowOffset: new Vector2(4f, 4f));

		const float tIn = 0.26f, hold = 0.45f, tOut = 0.26f, total = tIn + hold + tOut;
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			float a = Ease.OutQuint(Seg(time, 0f, 0.26f));
			float b = Ease.OutQuint(Seg(time, 0.03f, 0.26f));
			float d = Seg(time, tIn, hold);
			float o = Ease.InCubic(Seg(time, tIn + hold, tOut));
			navy.Position = new Vector2(-(w + 200f) * (1f - a) + 8f * d - (w + 200f) * o, 0f);
			slab.Position = new Vector2(-(w + 200f) * (1f - b) + 14f * d - (w + 200f) * o, 0f);
			red.Scale = new Vector2(1f, Ease.OutBack(Seg(time, 0.12f, 0.14f)) * (1f - o));
			float u = Ease.OutQuint(Seg(time, 0.07f, 0.28f));
			text.Position = new Vector2(size.X * 0.09f - 120f * (1f - u) + 16f * d - (w + 200f) * o, 0f);
			text.Modulate = new Color(1f, 1f, 1f, u * (1f - o));
			disc.Scale = Vector2.One * Ease.OutBack(Seg(time, 0.05f, 0.22f)) * (1f - Ease.InCubic(Seg(time, tIn + hold, tOut)));
			arcs.Rotation = 0.6f * time;
			arcs.Modulate = new Color(1f, 1f, 1f, a * (1f - o));
			float n = Ease.OutBack(Seg(time, 0.12f, 0.22f));
			num.Scale = Vector2.One * Mathf.Lerp(1.6f, 1f, n) * (1f - o);
			num.Modulate = new Color(1f, 1f, 1f, Seg(time, 0.12f, 0.08f) * (1f - o));
			root.Modulate = new Color(1f, 1f, 1f, 0.92f * (1f - Seg(time, tIn + hold + tOut * 0.6f, tOut * 0.4f)));
		});
		Fx.FreeAfter(root, total + 0.05);
	}

	// ───────────────────────── 击倒 ─────────────────────────
	/// <summary>
	/// 击倒敌人：两道交叉的长色块（白 / 深蓝，各带一道红边）0.07s 切过敌人，定格一拍后沿各自法线方向错开分离、变细淡出；
	/// 中心一个小水之圆张开又收拢，加玻璃碎片与闪光。总长约 0.6s。
	/// </summary>
	public static void KnockDown(Control layer, Control? back, Vector2 at)
	{
		Node2D root = Fx.Root(layer, at, "P3KnockDown");
		// v0.14.8（用户：击败特效把怪物挡住了）：水之圆改到人物后层、半透明、无焦散，只在敌人身后亮一下
		Node2D discRoot = Fx.Root(back ?? layer, at, "P3KnockDownDisc");
		float len = 900f;
		Node2D MakeBar(float angle, Color color)
		{
			Node2D bar = new() { Rotation = angle };
			root.AddChild(bar);
			P3Ui.Slab(bar, -len * 0.5f, len * 0.5f, -16f, 16f, 22f, color);
			P3Ui.Slab(bar, -len * 0.5f, len * 0.5f, 16f, 21f, 4f, new Color(0.9f, 0.08f, 0.22f, 0.95f));
			return bar;
		}
		Node2D w = MakeBar(-0.45f, new Color(0.96f, 0.98f, 1f, 0.95f));
		Node2D n = MakeBar(0.42f, new Color(0.03f, 0.08f, 0.3f, 0.95f));
		ColorRect disc = P3Ui.WaterDisc(discRoot, Vector2.Zero, 150f);
		Fx.SetParam(disc, "caustics", 0f);
		Fx.SetParam(disc, "alpha", back != null ? 0.6f : 0.35f);
		Fx.FreeAfter(discRoot, 0.67);
		Vector2 nw = new Vector2(Mathf.Cos(-0.45f + Mathf.Pi / 2f), Mathf.Sin(-0.45f + Mathf.Pi / 2f));
		Vector2 nn = new Vector2(Mathf.Cos(0.42f + Mathf.Pi / 2f), Mathf.Sin(0.42f + Mathf.Pi / 2f));
		const float total = 0.62f;
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			float cut = Ease.OutExpo(Seg(time, 0f, 0.07f));
			float split = Ease.InCubic(Seg(time, 0.16f, 0.4f));
			w.Scale = new Vector2(cut, 1f - 0.85f * split);
			n.Scale = new Vector2(Ease.OutExpo(Seg(time, 0.03f, 0.07f)), 1f - 0.85f * split);
			w.Position = nw * 70f * split;
			n.Position = -nn * 70f * split;
			w.Modulate = n.Modulate = new Color(1f, 1f, 1f, 1f - split);
			Fx.SetParam(disc, "reveal", Ease.OutExpo(Seg(time, 0.02f, 0.14f)) * (1f - Ease.InCubic(Seg(time, 0.2f, 0.3f))));
		});
		P3Fx.Flash(layer, at, Pal.Ice, 320f, 0.16f);
		Fx.After(root, 0.16, "knockdown shatter", () => P3Fx.GlassShatter(layer, at, 22, 1.0f, Pal.Cyan));
		Fx.FreeAfter(root, total + 0.05);
	}

	// ───────────────────────── 胜利 ─────────────────────────
	/// <summary>
	/// 战斗胜利（P3R 结算界面风格，不用圆框）：左侧白色斜切大色块 + 红边从左切入，"VICTORY" 大字从下滑上；
	/// 右侧一整块斜切的水面面板从右边扫进来（左边是一道 "/" 斜切口，上 / 右 / 下都出血到屏幕外），
	/// 结城理的立绘贴着屏幕右下角放大出血，身后两层错开的蓝色剪影先到、本人随后滑入；斜切口上青白两道细线，
	/// 入场时眼前那块玻璃碎开，蓝蝶从面板里飞出。入 0.4s → 停 1.0s → 出 0.4s（白块向左、面板向右收走）。
	/// </summary>
	public static void Victory(Control layer)
	{
		Vector2 size = layer.GetViewportRect().Size;
		float W = size.X, H = size.Y;
		Node2D root = Fx.Root(layer, Vector2.Zero, "P3Victory");
		root.TopLevel = true;
		root.GlobalPosition = Vector2.Zero;
		ColorRect dim = new() { Size = size, Color = new Color(0.01f, 0.03f, 0.1f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(dim);

		// 立绘：画布右下角贴住屏幕右下角（身体在画布下 / 右边被截断，截断处正好在屏幕外）
		Texture2D? heroTex = ResourceLoader.Load("res://p3makoto/ui/victory_portrait.png") as Texture2D;
		float heroH = H * 1.25f;
		float heroScale = heroTex != null ? heroH / heroTex.GetHeight() : 1f;
		float heroW = heroTex != null ? heroTex.GetWidth() * heroScale : heroH * 0.667f;
		Vector2 heroAt = new(W + W * 0.02f - heroW * 0.5f, -H * 0.055f + heroH * 0.5f);
		// 面板的 "/" 斜切口：下端在立绘画布左边线右侧（画布左边的袖子截断处永远被切掉），上端再向右倾
		float lean = H * 0.16f;
		float cutB = Mathf.Max(heroAt.X - heroW * 0.5f + 24f, W * 0.54f);
		float far = W + lean + 260f;
		Vector2[] PanelPts(float x0) => new[] { new Vector2(x0 + lean, -60f), new Vector2(far + 400f, -60f), new Vector2(far + 400f, H + 60f), new Vector2(x0, H + 60f) };

		// 白块与面板之间的深蓝底带
		Node2D band = new();
		root.AddChild(band);
		P3Ui.Slab(band, W * 0.38f, W + 300f, -60f, H + 60f, H * 0.28f, new Color(0.02f, 0.06f, 0.24f, 0.92f));

		Polygon2D panel = new() { Polygon = PanelPts(far), ClipChildren = CanvasItem.ClipChildrenMode.AndDraw, Color = new Color(0.02f, 0.08f, 0.4f, 1f) };
		root.AddChild(panel);
		ColorRect water = P3Ui.WaterDisc(panel, new Vector2(W * 0.8f, H * 0.5f), size.Length() * 0.62f);
		Fx.SetParam(water, "reveal", 1f);
		// 面板里的斜向光带（水面反光，和斜切口平行）
		Node2D sheen = new();
		panel.AddChild(sheen);
		P3Ui.Slab(sheen, cutB + W * 0.05f, cutB + W * 0.09f, -60f, H + 60f, lean * 1.4f, new Color(0.6f, 0.9f, 1f, 0.12f));
		P3Ui.Slab(sheen, cutB + W * 0.11f, cutB + W * 0.125f, -60f, H + 60f, lean * 1.4f, new Color(0.6f, 0.9f, 1f, 0.08f));
		Typo.Text(panel, "THE FOOL  0", Typo.Latin, (int)(H * 0.03f), new Color(Pal.Ice, 0.35f), new Vector2(W - W * 0.2f, H * 0.93f));

		Sprite2D MakeHero(Color? tint)
		{
			Sprite2D sp = new() { Texture = heroTex, Scale = Vector2.One * heroScale, Position = heroAt };
			if (tint is Color c)
			{
				ShaderMaterial m = new() { Shader = Shaders.FlatTint };
				m.SetShaderParameter("tint", new Color(c, 0f));
				sp.Material = m;
			}
			panel.AddChild(sp);
			return sp;
		}
		Sprite2D shadeDeep = MakeHero(new Color(0.12f, 0.3f, 1f));
		Sprite2D shadeCyan = MakeHero(Pal.Cyan);
		Sprite2D hero = MakeHero(null);
		hero.Modulate = new Color(1f, 1f, 1f, 0f);

		// 斜切口上的两道细线（随面板一起扫进来）
		Node2D edge = new();
		root.AddChild(edge);
		float EdgeX(float y) => lean * (H + 60f - y) / (H + 120f);
		Fx.Stroke(edge, new[] { new Vector2(0f, H + 60f), new Vector2(lean, -60f) }, Pal.Cyan, 7f, additive: false);
		Fx.Stroke(edge, new[] { new Vector2(-22f, H + 60f), new Vector2(lean - 22f, -60f) }, new Color(1f, 1f, 1f, 0.85f), 2.5f, additive: false);
		Fx.Stroke(edge, new[] { new Vector2(EdgeX(H * 0.8f) - 44f, H * 0.8f), new Vector2(EdgeX(H * 0.6f) - 44f, H * 0.6f) }, new Color(0.9f, 0.08f, 0.22f, 0.9f), 5f, additive: false);

		Node2D white = new();
		root.AddChild(white);
		P3Ui.Slab(white, -300f, W * 0.42f, -40f, H + 40f, H * 0.28f, new Color(0.95f, 0.97f, 1f, 0.95f));
		P3Ui.Slab(white, W * 0.42f - 4f, W * 0.42f + 12f, -40f, H + 40f, H * 0.28f, new Color(0.9f, 0.08f, 0.22f, 0.95f));
		// 左侧色块上方：深蓝斜切色块 + "RESULT" 小标题 + 两道细线 + 半透明大字（P3R 菜单的层叠排版）
		P3Ui.Slab(white, -300f, W * 0.3f, -40f, H * 0.2f, H * 0.06f, new Color(0.03f, 0.08f, 0.3f, 0.95f));
		Typo.Text(white, "RESULT", Typo.Latin, (int)(H * 0.07f), new Color(0.95f, 0.97f, 1f), new Vector2(W * 0.04f, H * 0.045f));
		Typo.Text(white, "MEMENTO MORI  /  THE FOOL", Typo.Latin, (int)(H * 0.022f), new Color(Pal.Cyan, 0.9f), new Vector2(W * 0.045f, H * 0.135f));
		Fx.Line(white, new[] { new Vector2(-50f, H * 0.235f), new Vector2(W * 0.36f, H * 0.235f) }, new Color(0.9f, 0.08f, 0.22f, 0.9f), 4f);
		Fx.Line(white, new[] { new Vector2(-50f, H * 0.255f), new Vector2(W * 0.33f, H * 0.255f) }, new Color(0.03f, 0.08f, 0.3f, 0.6f), 2f);
		Node2D ghostWord = P3Ui.GiantWord(white, "CLEAR", new Vector2(W * 0.2f, H * 0.62f), (int)(H * 0.2f), -1.05f, new Color(0.03f, 0.08f, 0.3f, 0.08f));
		Node2D word = P3Ui.GiantWord(white, "VICTORY", new Vector2(W * 0.09f, H * 0.9f), (int)(H * 0.28f), -1.05f, new Color(0.03f, 0.08f, 0.3f, 0.95f));
		Node2D tag = P3Ui.RedTag(root, "BATTLE CLEAR", new Vector2(W * 0.5f, H * 0.14f), (int)(H * 0.05f), cjk: false);

		// 画布上眼睛 / 玻璃片的位置（相对立绘画布，0~1）
		Vector2 glassAt = heroAt + new Vector2((0.56f - 0.5f) * heroW, (0.24f - 0.5f) * heroH);

		const float tIn = 0.4f, hold = 1.0f, tOut = 0.4f, total = tIn + hold + tOut;
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			float a = Ease.OutExpo(Seg(time, 0f, 0.32f));
			float d = Seg(time, tIn, hold);
			float o = Seg(time, tIn + hold, tOut);
			float oe = Ease.InExpo(o);
			dim.Color = new Color(0.01f, 0.03f, 0.1f, 0.45f * a * (1f - o));
			white.Position = new Vector2(-W * 0.6f * (1f - a) - 20f * d - W * 0.7f * oe, 0f);
			band.Position = new Vector2(W * 0.5f * (1f - a) + W * 0.7f * oe, 0f);
			word.Position = new Vector2(W * 0.09f, H * 0.9f + H * 0.5f * (1f - Ease.OutExpo(Seg(time, 0.08f, 0.4f))) - 40f * d - H * 0.4f * oe);
			ghostWord.Position = new Vector2(W * 0.2f + 60f * time, H * 0.62f);
			// 面板：斜切口从屏幕右外扫到 cutB，停留时轻微左移，退场再扫回右边
			float p = Ease.OutExpo(Seg(time, 0.04f, 0.3f));
			float x0 = Mathf.Lerp(far, cutB, p) - 10f * d + (far - cutB) * oe;
			panel.Polygon = PanelPts(x0);
			edge.Position = new Vector2(x0, 0f);
			// 剪影先到、本人随后：三层同向滑入，错开 (-34,18) / (-18,9) 停住
			float s1 = Ease.OutExpo(Seg(time, 0.06f, 0.3f));
			float s2 = Ease.OutExpo(Seg(time, 0.1f, 0.3f));
			float h = Ease.OutQuint(Seg(time, 0.14f, 0.34f));
			Vector2 drift = new(-14f * d, 0f);
			Vector2 slideOut = new(W * 0.5f * oe, 0f);
			shadeDeep.Position = heroAt + new Vector2(-34f, 18f) + new Vector2(W * 0.35f * (1f - s1), 0f) + drift * 1.4f + slideOut;
			shadeCyan.Position = heroAt + new Vector2(-18f, 9f) + new Vector2(W * 0.35f * (1f - s2), 0f) + drift * 1.2f + slideOut;
			hero.Position = heroAt + new Vector2(W * 0.3f * (1f - h), 0f) + drift + slideOut;
			hero.Scale = Vector2.One * heroScale * (1.06f - 0.06f * h);
			hero.Modulate = new Color(1f, 1f, 1f, h * (1f - o));
			((ShaderMaterial)shadeDeep.Material).SetShaderParameter("tint", new Color(0.12f, 0.3f, 1f, 0.9f * s1 * (1f - o)));
			((ShaderMaterial)shadeCyan.Material).SetShaderParameter("tint", new Color(Pal.Cyan, 0.95f * s2 * (1f - o)));
			float k = Seg(time, 0.2f, 0.18f);
			tag.Scale = Vector2.One * Mathf.Lerp(1.8f, 1f, Ease.OutBack(k));
			tag.Modulate = new Color(1f, 1f, 1f, k * (1f - o));
			root.Modulate = new Color(1f, 1f, 1f, 1f - Seg(time, tIn + hold + tOut * 0.5f, tOut * 0.5f));
		});
		Fx.After(root, 0.36, "victory glass", () => P3Fx.GlassShatter(layer, glassAt, 16, 0.8f, Pal.Ice));
		Fx.After(root, 0.3, "victory butterflies", () =>
		{
			for (int i = 0; i < 5; i++)
			{
				Vector2 from = new(cutB + Fx.Rand(-20f, 120f), H * Fx.Rand(0.45f, 0.9f));
				P3Fx.Butterfly(layer, from, from + new Vector2(Fx.Rand(-W * 0.3f, -W * 0.1f), -H * Fx.Rand(0.3f, 0.6f)), Fx.Rand(1.1f, 1.5f), Fx.Rand(0.7f, 1.1f));
			}
		});
		Fx.FreeAfter(root, total + 0.05);
	}

	// ───────────────────────── 卡牌奖励 ─────────────────────────
	/// <summary>
	/// 卡牌奖励（选牌）界面的背景：牌后面张开一个水之圆（两道圆弧缓慢转动）+ 一条斜切白色色块 + 左上红字标签 "SELECT ARCANA"；
	/// 每张牌落位时（原版 0.5s 滑入之后）依次闪一道白光十字星。随界面一起关闭（是界面的子节点）。
	/// </summary>
	public static void RewardBackdrop(Control screen, Vector2 rowCenter, Func<Vector2[]> cardCenters)
	{
		Vector2 size = screen.GetViewportRect().Size;
		// 作为选牌界面的第一个子节点：画在牌的下面，但在原版共享的暗色遮罩（选牌界面的同级节点）上面。
		// 不能设 ZIndex = -1（会掉到遮罩下面被一起压暗），也不用 TopLevel（会画到牌的上面）。
		Node2D root = new() { Name = "P3RewardBackdrop" };
		screen.AddChild(root);
		screen.MoveChild(root, 0);
		root.GlobalPosition = Vector2.Zero;
		Node2D slab = new();
		root.AddChild(slab);
		P3Ui.Slab(slab, -200f, size.X + 200f, rowCenter.Y + size.Y * 0.16f, rowCenter.Y + size.Y * 0.24f, -60f, new Color(0.95f, 0.97f, 1f, 0.22f));
		float discR = size.Y * 0.4f;
		ColorRect disc = P3Ui.WaterDisc(root, rowCenter, discR);
		Fx.SetParam(disc, "alpha", 0.55f);
		Fx.SetParam(disc, "caustics", 0f);   // v0.14.8：焦散的暗部在大圆盘上形成不规则深蓝斑块（和能力牌月盘同一个问题）→ 干净水面
		Node2D arcs = new() { Position = rowCenter };
		root.AddChild(arcs);
		P3Ui.Arc(arcs, Vector2.Zero, discR * 1.06f, 0f, Mathf.Pi * 1.3f, new Color(Pal.Cyan, 0.5f), 3f);
		P3Ui.Arc(arcs, Vector2.Zero, discR * 1.14f, Mathf.Pi, Mathf.Pi * 1.6f, new Color(1f, 1f, 1f, 0.4f), 2f);
		Fx.Animate(root, 120.0, t =>
		{
			float time = t * 120f;
			float a = Ease.OutExpo(Seg(time, 0f, 0.4f));
			Fx.SetParam(disc, "reveal", a);
			slab.Position = new Vector2(-size.X * (1f - a), 0f);
			arcs.Rotation = 0.25f * time;
			arcs.Modulate = new Color(1f, 1f, 1f, a);
		});
		// 每张牌的十字闪光改在 ShuffleIntro 翻牌那一刻（v0.14.8）
		_ = cardCenters;
	}

	/// <summary>
	/// P3R Shuffle Time（v0.14.8，用户：卡牌飞出草率 → 三张塔罗牌落定后再一起翻开）：
	///   0.00  中间叠起一摞塔罗牌背（天鹅绒蓝蝶牌背），轻轻弹出；顶部「SHUFFLE TIME」斜带滑入
	///   0.15  洗牌：几张牌绕着中心沿椭圆轨道转一圈半（平滑加减速，牌身随运动方向倾斜，拖着淡淡的残光）
	///   0.85  发牌：依次滑到各自的位置（OutCubic + 轻微回弹），每落一张"嗒"一声
	///   1.40  三张都落定后，一起翻面：牌背收窄成一条线 → 真正的卡牌从线里展开（OutBack），每张一道白色十字闪光 + 翻牌声
	/// 翻开之前真正的卡牌是隐藏的（不能提前点到看不见的牌）；任何异常 2.2s 后兜底显示。
	/// </summary>
	public const float TitleIntroLen = 1.15f;

	/// <summary>first = 这次打开选牌界面的第一次（先播独立的 SHUFFLE TIME 标题动画再发牌）；重抽时直接发牌。</summary>
	public static void ShuffleIntro(Control screen, Control row, bool first = true)
	{
		var holders = row.GetChildren().OfType<Control>().ToList();
		if (holders.Count == 0)
		{
			return;
		}
		foreach (Control h in holders)
		{
			h.Visible = false;                           // 发牌 / 翻牌之前先藏起来（含标题动画期间）
		}
		screen.GetNodeOrNull("P3ShuffleDeck")?.QueueFree();
		if (first)
		{
			TitleIntro(screen);
			Fx.After(screen, TitleIntroLen, "shuffle deal", () => { if (GodotObject.IsInstanceValid(row)) Deal(screen, row); });
			Fx.After(screen, TitleIntroLen + 3.0, "shuffle failsafe 2", () =>
			{
				foreach (Control h in holders)
				{
					if (GodotObject.IsInstanceValid(h)) h.Visible = true;
				}
			});
		}
		else
		{
			Deal(screen, row);
		}
	}

	private static void Deal(Control screen, Control row)
	{
		var holders = row.GetChildren().OfType<Control>().ToList();
		int n = holders.Count;
		if (n == 0)
		{
			return;
		}
		Audio.Sfx.Clip("ShuffleTime_Whoosh", 0.6f, 0.15f);
		ShuffleTitle(screen);
		Vector2 view = screen.GetViewportRect().Size;
		float cardH = 330f * view.Y / 1080f;
		Texture2D? back = ResourceLoader.Exists("res://p3makoto/fx/tarot_back.png") ? ResourceLoader.Load<Texture2D>("res://p3makoto/fx/tarot_back.png") : null;
		float spacing = 350f;
		Vector2 origin = Vector2.Left * (n - 1) * spacing * 0.5f;
		Transform2D rowXf = row.GetGlobalTransform();
		Vector2 center = rowXf * Vector2.Zero;
		var baseScales = new Vector2[n];
		var targets = new Vector2[n];
		for (int i = 0; i < n; i++)
		{
			Control h = holders[i];
			Vector2 final = origin + Vector2.Right * spacing * i;
			baseScales[i] = h.Scale;
			h.Position = final;
			h.Rotation = 0f;
			h.Scale = baseScales[i];
			h.Modulate = Colors.White;
			h.Visible = back == null;                    // 没有牌背贴图就直接显示原牌
			targets[i] = rowXf * final;
		}
		if (back == null)
		{
			return;
		}

		Node2D deck = new() { Name = "P3ShuffleDeck" };
		screen.AddChild(deck);
		deck.TopLevel = true;
		deck.GlobalPosition = Vector2.Zero;
		float sc = cardH / back.GetHeight();
		var cards = new System.Collections.Generic.List<(Node2D Node, Sprite2D Face, Sprite2D Glow)>();
		for (int i = 0; i < n; i++)
		{
			Node2D c = new() { Position = center, Scale = Vector2.Zero };
			deck.AddChild(c);
			Sprite2D glow = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(Pal.Cyan, 0.35f), Scale = new Vector2(cardH * 0.9f, cardH * 1.25f) / Fx.SoftDot.GetWidth() };
			c.AddChild(glow);
			Sprite2D face = new() { Texture = back, Scale = Vector2.One * sc };
			c.AddChild(face);
			cards.Add((c, face, glow));
		}
		// v0.14.8：每张牌一条连续的三次贝塞尔曲线（中间牌堆 → 绕向外侧 → 从上方落进自己的位置），整段一个平滑缓动，
		// 不再分"绕圈"和"发牌"两段拼接（拼接处速度突变，看起来不丝滑）。
		const float startAt = 0.12f, gap = 0.09f, flight = 0.85f;
		float flipAt = startAt + gap * (n - 1) + flight + 0.25f;
		var paths = new (Vector2 P0, Vector2 P1, Vector2 P2, Vector2 P3, float Side)[n];
		for (int i = 0; i < n; i++)
		{
			float side = n == 1 ? 1f : (i - (n - 1) / 2f) / ((n - 1) / 2f);   // -1 左 … 1 右
			float sgn = side == 0f ? (i % 2 == 0 ? 1f : -1f) : Mathf.Sign(side);
			Vector2 p0 = center + new Vector2((i - (n - 1) / 2f) * 4f, -i * 3f);
			Vector2 p1 = center + new Vector2(sgn * (260f + 120f * Mathf.Abs(side)), 150f);        // 先向外下方甩出
			Vector2 p2 = targets[i] + new Vector2(sgn * 60f, -220f);                               // 再从斜上方落下
			paths[i] = (p0, p1, p2, targets[i], sgn);
			Audio.Sfx.Clip("ShuffleTime_CardSet", 0.5f, startAt + gap * i + flight * 0.96f);
		}
		static Vector2 Bez(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
		{
			float u = 1f - t;
			return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
		}
		static float Smoother(float x) => x * x * x * (x * (x * 6f - 15f) + 10f);
		Fx.Animate(deck, flipAt + 0.05f, t =>
		{
			float time = t * (flipAt + 0.05f);
			for (int i = 0; i < n; i++)
			{
				(Node2D c, Sprite2D face, Sprite2D glow) = cards[i];
				var (p0, p1, p2, p3, sgn) = paths[i];
				float pop = Ease.OutBack(Mathf.Clamp(time / 0.12f, 0f, 1f));
				float u = Mathf.Clamp((time - startAt - gap * i) / flight, 0f, 1f);
				float e = Smoother(u);
				Vector2 pos = Bez(p0, p1, p2, p3, e);
				Vector2 ahead = Bez(p0, p1, p2, p3, Mathf.Min(1f, e + 0.02f));
				float tilt = u >= 1f ? 0f : Mathf.Clamp((ahead - pos).X * 0.004f, -0.35f, 0.35f) * Mathf.Sin(Mathf.Pi * e);
				float land = u >= 1f ? Mathf.Max(0f, 1f - (time - startAt - gap * i - flight) / 0.18f) : 0f;
				c.Position = pos;
				c.Rotation = tilt;
				c.Scale = Vector2.One * pop * (0.82f + 0.18f * e) * (1f + 0.04f * Mathf.Sin(Mathf.Pi * land));
				c.ZIndex = u > 0f ? 10 + i : i;
				glow.Modulate = new Color(Pal.Cyan, 0.25f + 0.25f * Mathf.Sin(Mathf.Pi * e) + 0.35f * land);
			}
		});
		// 一起翻面：牌背收窄成线（0.1s）→ 真卡展开（0.2s, OutBack）
		Fx.After(deck, flipAt, "shuffle flip", () =>
		{
			Audio.Sfx.Clip("card_draw", 0.8f);
			Audio.Sfx.Clip("ShuffleTime_Result", 0.55f, 0.08f);
			Fx.Animate(deck, 0.1f, t =>
			{
				foreach (var card in cards)
				{
					card.Node.Scale = new Vector2(1f - t, 1f + 0.04f * t);
				}
			});
			Fx.After(deck, 0.1, "shuffle reveal", () =>
			{
				deck.QueueFree();
				for (int i = 0; i < n; i++)
				{
					Control h = holders[i];
					if (!GodotObject.IsInstanceValid(h))
					{
						continue;
					}
					Vector2 bs = baseScales[i];
					h.Visible = true;
					h.Scale = new Vector2(0.01f, bs.Y);
					Fx.Animate(h, 0.2f, t => { if (GodotObject.IsInstanceValid(h)) h.Scale = new Vector2(bs.X * Mathf.Max(0.01f, Ease.OutBack(t)), bs.Y); });
					Glint(screen, targets[i], cardH);
				}
			});
		});
		// 兜底：无论如何 2.2s 后把牌显示出来
		Fx.After(screen, flipAt + 0.6f, "shuffle failsafe", () =>
		{
			for (int i = 0; i < n; i++)
			{
				Control h = holders[i];
				if (GodotObject.IsInstanceValid(h) && !h.Visible)
				{
					h.Visible = true;
					h.Scale = baseScales[i];
				}
			}
		});
	}

	/// <summary>翻牌那一刻每张牌上的白色十字闪光 + 青色光晕。</summary>
	private static void Glint(Control screen, Vector2 at, float cardH)
	{
		Node2D g = new();
		screen.AddChild(g);
		g.TopLevel = true;
		g.GlobalPosition = at;
		Fx.Poly(g, Fx.Star4(cardH * 0.42f, 0.05f), Pal.White, additive: true);
		g.AddChild(new Sprite2D { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(Pal.Cyan, 0.7f), Scale = Vector2.One * (cardH * 0.9f / Fx.SoftDot.GetWidth()) });
		Fx.Animate(g, 0.35, t =>
		{
			g.Scale = Vector2.One * Mathf.Sin(t * Mathf.Pi);
			g.Rotation = t * 0.8f;
		});
		Fx.FreeAfter(g, 0.4);
	}

	/// <summary>
	/// P3R：选牌前先单独播一段「SHUFFLE TIME」标题（v0.14.8，用户：P3R 里是标题动画播完才出牌）：
	///   0.00  画面压暗成深蓝；一道青白光刃斜着划过画面中央，留下一条倾斜的蓝色宽带
	///   0.10  「SHUFFLE」从左、「TIME」从右高速滑入，在中间对上（0.35）：白闪 + 轻震 + 八张小阿卡纳牌从字后旋转着炸开
	///   0.35–0.85  停留：字缓慢放大，一道扫光掠过字面
	///   0.85–1.15  退场：字和宽带横向拉长、淡出，暗色退去 → 左上角小标题滑入、开始发牌
	/// 有原版「SHUFFLE TIME」字图（ui/shuffle_title.png）时用原版字图代替文字。
	/// </summary>
	private static void TitleIntro(Control screen)
	{
		Vector2 size = screen.GetViewportRect().Size;
		Node2D root = new() { Name = "P3ShuffleTitleIntro", ZIndex = 50 };
		screen.AddChild(root);
		root.TopLevel = true;
		root.GlobalPosition = Vector2.Zero;
		Audio.Sfx.Clip("ShuffleTime_Start", 0.85f);
		Audio.Sfx.Clip("flash_cutin", 0.7f, 0.33f);
		ColorRect dim = new() { Size = size, Color = new Color(0.01f, 0.03f, 0.12f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(dim);
		Vector2 c = size / 2f;
		Node2D band = new() { Position = c, Rotation = -0.14f };
		root.AddChild(band);
		float bh = size.Y * 0.24f;
		Polygon2D slab = Fx.Poly(band, new[] { new Vector2(-size.X, -bh / 2f), new Vector2(size.X, -bh / 2f), new Vector2(size.X, bh / 2f), new Vector2(-size.X, bh / 2f) }, new Color(0.04f, 0.2f, 0.75f, 0.9f));
		Polygon2D edgeA = Fx.Poly(band, new[] { new Vector2(-size.X, -bh / 2f - 6f), new Vector2(size.X, -bh / 2f - 6f), new Vector2(size.X, -bh / 2f), new Vector2(-size.X, -bh / 2f) }, Pal.Cyan, additive: true);
		Polygon2D edgeB = Fx.Poly(band, new[] { new Vector2(-size.X, bh / 2f), new Vector2(size.X, bh / 2f), new Vector2(size.X, bh / 2f + 4f), new Vector2(-size.X, bh / 2f + 4f) }, new Color(1f, 1f, 1f, 0.8f), additive: true);
		Polygon2D blade = Fx.Poly(band, new[] { new Vector2(-60f, -bh * 0.7f), new Vector2(60f, -bh * 0.7f), new Vector2(20f, bh * 0.7f), new Vector2(-100f, bh * 0.7f) }, new Color(0.85f, 0.97f, 1f, 0.95f), additive: true);
		// 字：原版字图优先，没有就用斜体字
		Node2D words = new() { Position = c, Rotation = -0.14f };
		root.AddChild(words);
		Node2D left, right;
		Texture2D? logo = ResourceLoader.Exists("res://p3makoto/ui/shuffle_title.png") ? ResourceLoader.Load<Texture2D>("res://p3makoto/ui/shuffle_title.png") : null;
		int fs = (int)(size.Y * 0.17f);
		if (logo != null)
		{
			float ls = bh * 1.1f / logo.GetHeight();
			left = new Node2D();
			words.AddChild(left);
			left.AddChild(new Sprite2D { Texture = logo, Scale = Vector2.One * ls });
			right = new Node2D();
			words.AddChild(right);
		}
		else
		{
			left = new Node2D();
			words.AddChild(left);
			Typo.Text(left, "SHUFFLE", Typo.LatinItalic, fs, Pal.White, new Vector2(-fs * 3.35f + 10f, -fs * 0.62f), outline: 6, outlineColor: new Color(0.02f, 0.06f, 0.3f), shadow: new Color(Pal.Cyan, 0.8f), shadowOffset: new Vector2(8f, 8f));
			right = new Node2D();
			words.AddChild(right);
			Typo.Text(right, "TIME", Typo.LatinItalic, fs, Pal.White, new Vector2(fs * 0.25f, -fs * 0.62f), outline: 6, outlineColor: new Color(0.02f, 0.06f, 0.3f), shadow: new Color(Pal.Cyan, 0.8f), shadowOffset: new Vector2(8f, 8f));
		}
		Polygon2D shine = Fx.Poly(words, new[] { new Vector2(0f, -fs * 0.7f), new Vector2(50f, -fs * 0.7f), new Vector2(20f, fs * 0.7f), new Vector2(-30f, fs * 0.7f) }, new Color(1f, 1f, 1f, 0.55f), additive: true);
		// 炸开的小阿卡纳牌
		Texture2D? faces = ResourceLoader.Exists("res://p3makoto/fx/arcana_faces.png") ? ResourceLoader.Load<Texture2D>("res://p3makoto/fx/arcana_faces.png") : null;
		var minis = new System.Collections.Generic.List<(Sprite2D s, Vector2 v, float spin)>();
		if (faces != null)
		{
			for (int k = 0; k < 8; k++)
			{
				int cell = (k * 5 + 2) % 16 == 14 ? 15 : (k * 5 + 2) % 16;
				Sprite2D m = new() { Texture = new AtlasTexture { Atlas = faces, Region = new Rect2(cell % 4 * 256, cell / 4 * 256, 256, 256) }, Scale = Vector2.One * (size.Y * 0.09f / 256f), Position = c, Modulate = new Color(1f, 1f, 1f, 0f) };
				root.AddChild(m);
				minis.Add((m, Vector2.Right.Rotated(k * Mathf.Tau / 8f + 0.3f) * new Vector2(size.X * 0.42f, size.Y * 0.36f), Fx.Rand(-5f, 5f)));
			}
		}
		bool met = false;
		Fx.Animate(root, TitleIntroLen, t =>
		{
			float time = t * TitleIntroLen;
			float outT = Ease.InCubic(Mathf.Clamp((time - 0.85f) / 0.3f, 0f, 1f));
			dim.Color = new Color(0.01f, 0.03f, 0.12f, 0.6f * Mathf.Clamp(time / 0.1f, 0f, 1f) * (1f - outT));
			// 光刃划过 → 宽带展开
			float sweep = Ease.OutExpo(Mathf.Clamp(time / 0.2f, 0f, 1f));
			blade.Position = new Vector2(Mathf.Lerp(-size.X * 0.8f, size.X * 0.8f, sweep), 0f);
			blade.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp((time - 0.15f) / 0.1f, 0f, 1f));
			float open = Ease.OutExpo(Mathf.Clamp((time - 0.05f) / 0.2f, 0f, 1f));
			band.Scale = new Vector2(1f + 0.3f * outT, open * (1f - 0.7f * outT));
			band.Modulate = new Color(1f, 1f, 1f, 1f - outT);
			// 字对撞
			float slide = Ease.OutExpo(Mathf.Clamp((time - 0.1f) / 0.25f, 0f, 1f));
			left.Position = new Vector2(-size.X * (1f - slide), 0f);
			right.Position = new Vector2(size.X * (1f - slide), 0f);
			words.Scale = new Vector2((1f + 0.04f * Mathf.Clamp((time - 0.35f) / 0.5f, 0f, 1f)) * (1f + 0.35f * outT), 1f + 0.04f * Mathf.Clamp((time - 0.35f) / 0.5f, 0f, 1f));
			words.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp((time - 0.1f) / 0.05f, 0f, 1f) * (1f - outT));
			float sh = Mathf.Clamp((time - 0.45f) / 0.35f, 0f, 1f);
			shine.Position = new Vector2(Mathf.Lerp(-fs * 3.4f, fs * 2.6f, Ease.InOutCubic(sh)), 0f);
			shine.Modulate = new Color(1f, 1f, 1f, sh > 0f && sh < 1f ? 1f : 0f);
			if (!met && time >= 0.35f)
			{
				met = true;
				P3Fx.Flash(screen, c, Pal.Ice, size.Y * 0.9f, 0.25f);
				NGame.Instance?.ScreenShake(ShakeStrength.Weak, ShakeDuration.Short);
			}
			float b = Mathf.Clamp((time - 0.35f) / 0.6f, 0f, 1f);
			foreach ((Sprite2D m, Vector2 v, float spin) in minis)
			{
				m.Position = c + v * Ease.OutCubic(b);
				m.Rotation = spin * b;
				m.Modulate = new Color(1f, 1f, 1f, time < 0.35f ? 0f : (1f - Ease.InCubic(b)) * (1f - outT));
			}
		});
		Fx.FreeAfter(root, TitleIntroLen + 0.05f);
	}

	private static void ShuffleTitle(Control screen)
	{
		screen.GetNodeOrNull("P3ShuffleTitle")?.QueueFree();
		Vector2 size = screen.GetViewportRect().Size;
		Node2D root = new() { Name = "P3ShuffleTitle" };
		screen.AddChild(root);
		// v0.14.8：放到顶部资源栏下面（原来压在血量 / 遗物栏上）、收窄到左侧，不碰中间的「选择一张牌」卷轴
		float y0 = size.Y * 0.105f, bh = size.Y * 0.085f, bw = size.X * 0.3f;
		root.GlobalPosition = new Vector2(0f, y0);
		Node2D band = new() { Rotation = -0.04f };
		root.AddChild(band);
		P3Ui.Slab(band, -100f, bw, 0f, bh, 22f, new Color(0.04f, 0.2f, 0.75f, 0.92f));
		P3Ui.Slab(band, -100f, bw, bh - 4f, bh, 22f, Pal.Cyan);
		Typo.Text(band, "SHUFFLE TIME", Typo.LatinItalic, (int)(size.Y * 0.068f), Pal.White, new Vector2(size.X * 0.025f, -size.Y * 0.002f), outline: 4, outlineColor: new Color(0.02f, 0.06f, 0.25f));
		Fx.Animate(root, 0.4f, t =>
		{
			root.GlobalPosition = new Vector2(-size.X * 0.5f * (1f - Ease.OutExpo(t)), y0);
		});
	}

	/// <summary>Shuffle Time 选中：阿卡纳爆发（光柱 + 玻璃碎片 + 闪光），其余的牌下沉变暗。</summary>
	public static void ShufflePick(Control screen, Control picked)
	{
		Audio.Sfx.Clip("ShuffleTime_ArcanaBurst", 0.9f);
		Vector2 at = picked.GetGlobalRect().GetCenter();
		Node2D root = new() { Name = "P3ShufflePick" };
		screen.AddChild(root);
		root.TopLevel = true;
		root.GlobalPosition = at;
		root.AddChild(new Sprite2D { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(Pal.Ice, 0.9f), Scale = new Vector2(4f, 9f) });
		for (int i = 0; i < 8; i++)
		{
			Vector2 d = Vector2.Up.Rotated(Mathf.Tau * i / 8f);
			Fx.Stroke(root, new[] { d * 60f, d * (i % 2 == 0 ? 320f : 200f) }, new Color(Pal.White, 0.9f), i % 2 == 0 ? 6f : 3f);
		}
		Fx.Animate(root, 0.45f, t =>
		{
			root.Scale = Vector2.One * (0.6f + 0.6f * Ease.OutExpo(t));
			root.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(t));
		});
		Fx.FreeAfter(root, 0.47);
		if (picked.GetParent() is Control row)
		{
			foreach (Control other in row.GetChildren().OfType<Control>())
			{
				if (other == picked) continue;
				Vector2 p0 = other.Position;
				Fx.Animate(other, 0.3f, t =>
				{
					if (!GodotObject.IsInstanceValid(other)) return;
					other.Position = p0 + new Vector2(0f, 40f * Ease.InCubic(t));
					other.Modulate = new Color(1f - 0.5f * t, 1f - 0.5f * t, 1f - 0.4f * t, 1f - 0.6f * t);
				});
			}
		}
	}
}
