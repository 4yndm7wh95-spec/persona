using System;
using Godot;

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

		Vector2 discAt = new(size.X * 0.035f, 0f);
		float discR = h * 0.95f;
		ColorRect disc = P3Ui.WaterDisc(root, discAt, discR);
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
		Typo.Text(num, glyph, Typo.Latin, ns, player ? Pal.White : new Color(1f, 0.25f, 0.35f), new Vector2(-ns * 0.3f * glyph.Length, -ns * 0.6f),
			shadow: new Color(0.02f, 0.06f, 0.24f, 0.9f), shadowOffset: new Vector2(4f, 4f));

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
			Fx.SetParam(disc, "reveal", Ease.OutQuint(Seg(time, 0.05f, 0.28f)) * (1f - Ease.InCubic(Seg(time, tIn + hold, tOut))));
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
	public static void KnockDown(Control layer, Vector2 at)
	{
		Node2D root = Fx.Root(layer, at, "P3KnockDown");
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
		ColorRect disc = P3Ui.WaterDisc(root, Vector2.Zero, 150f);
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
		Node2D arcs = new() { Position = rowCenter };
		root.AddChild(arcs);
		P3Ui.Arc(arcs, Vector2.Zero, discR * 1.06f, 0f, Mathf.Pi * 1.3f, new Color(Pal.Cyan, 0.5f), 3f);
		P3Ui.Arc(arcs, Vector2.Zero, discR * 1.14f, Mathf.Pi, Mathf.Pi * 1.6f, new Color(1f, 1f, 1f, 0.4f), 2f);
		Node2D tag = P3Ui.RedTag(root, "SELECT ARCANA", new Vector2(size.X * 0.08f, size.Y * 0.12f), (int)(size.Y * 0.04f), cjk: false);
		Fx.Animate(root, 120.0, t =>
		{
			float time = t * 120f;
			float a = Ease.OutExpo(Seg(time, 0f, 0.4f));
			Fx.SetParam(disc, "reveal", a);
			slab.Position = new Vector2(-size.X * (1f - a), 0f);
			arcs.Rotation = 0.25f * time;
			arcs.Modulate = new Color(1f, 1f, 1f, a);
			float k = Seg(time, 0.15f, 0.18f);
			tag.Scale = Vector2.One * Mathf.Lerp(1.7f, 1f, Ease.OutBack(k));
			tag.Modulate = new Color(1f, 1f, 1f, k);
		});
		Fx.After(root, 0.55, "reward glints", () =>
		{
			Vector2[] cs = cardCenters();
			for (int i = 0; i < cs.Length; i++)
			{
				Vector2 p = cs[i];
				Fx.After(root, 0.07 * i, "reward glint", () =>
				{
					Node2D g = new();
					root.AddChild(g);
					g.GlobalPosition = p;
					Fx.Poly(g, Fx.Star4(140f, 0.05f), Pal.White, additive: true);
					g.AddChild(new Sprite2D { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(Pal.Cyan, 0.7f), Scale = Vector2.One * 3f });
					Fx.Animate(g, 0.35, t =>
					{
						g.Scale = Vector2.One * Mathf.Sin(t * Mathf.Pi);
						g.Rotation = t * 0.8f;
					});
					Fx.FreeAfter(g, 0.4);
				});
			}
		});
	}
}
