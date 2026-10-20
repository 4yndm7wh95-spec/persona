using System;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using P3Makoto.Core;

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
	/// 击倒敌人：两道交叉的长色块（白 / 深蓝，各带一道红边）像两刀先后切过敌人（细光线划过 → 色块从一端划到另一端 → 切口张开），
	/// 定格一拍后沿各自法线方向错开分离、变细淡出；中心一个小水之圆张开又收拢，加闪光与玻璃碎片。总长约 0.66s。
	/// </summary>
	public static void KnockDown(Control layer, Control? back, Vector2 at)
	{
		Node2D root = Fx.Root(layer, at, "P3KnockDown");
		// v0.14.28（用户：击杀特效没有音效，加一个不太大声、能听出反馈的）：P3R 敌人消散声两种随机，压低音量（01 开头更响，再低一点）
		if (Fx.Rng.Next(2) == 0)
		{
			Audio.Sfx.Clip("Snd_FX_Btl_EnemyDead_00", 0.65f);
		}
		else
		{
			Audio.Sfx.Clip("Snd_FX_Btl_EnemyDead_01", 0.52f);
		}
		// v0.14.8（用户：击败特效把怪物挡住了）：水之圆改到人物后层、半透明、无焦散，只在敌人身后亮一下
		Node2D discRoot = Fx.Root(back ?? layer, at, "P3KnockDownDisc");
		float len = 900f;
		// v0.14.43（用户：两条斜杠像是突然出现的，有违和感，要有切入的动效）：
		// 以前色块从中间向两头一下子展开（0.07s），像凭空冒出来。现在是"刀切进来"：
		//   先一道细亮光线沿刀路飞速划过（带拖尾），色块紧跟着从一端"划"到另一端（此时只有 25% 粗），
		//   划完之后切口张开到全宽（带一点回弹）；白条从左往右切，深蓝条晚 0.04s 从右往左切，两刀交叉时中心一闪；
		//   定格一拍后照旧沿各自法线错开分离、变细淡出。
		// fromStart = true：从轴的负端划向正端；false：从正端划向负端。
		(Node2D bar, Node2D sweep, Line2D streak) MakeBar(float angle, Color color, bool fromStart)
		{
			Node2D bar = new() { Rotation = angle };
			root.AddChild(bar);
			Node2D sweep = new() { Position = new Vector2(fromStart ? -len * 0.5f : len * 0.5f, 0f), Scale = new Vector2(0f, 1f) };
			bar.AddChild(sweep);
			float a0 = fromStart ? 0f : -len, a1 = fromStart ? len : 0f;
			P3Ui.Slab(sweep, a0, a1, -16f, 16f, 22f, color);
			P3Ui.Slab(sweep, a0, a1, 16f, 21f, 4f, new Color(0.9f, 0.08f, 0.22f, 0.95f));
			Line2D streak = Fx.Line(bar, new[] { Vector2.Zero, Vector2.Zero }, new Color(1f, 1f, 1f, 0f), 4f);
			return (bar, sweep, streak);
		}
		var (w, ws, wl) = MakeBar(-0.45f, new Color(0.96f, 0.98f, 1f, 0.95f), true);
		var (n, ns, nl) = MakeBar(0.42f, new Color(0.03f, 0.08f, 0.3f, 0.95f), false);
		ColorRect disc = P3Ui.WaterDisc(discRoot, Vector2.Zero, 150f);
		Fx.SetParam(disc, "caustics", 0f);
		Fx.SetParam(disc, "alpha", back != null ? 0.6f : 0.35f);
		Fx.FreeAfter(discRoot, 0.71);
		Vector2 nw = new Vector2(Mathf.Cos(-0.45f + Mathf.Pi / 2f), Mathf.Sin(-0.45f + Mathf.Pi / 2f));
		Vector2 nn = new Vector2(Mathf.Cos(0.42f + Mathf.Pi / 2f), Mathf.Sin(0.42f + Mathf.Pi / 2f));
		const float total = 0.66f;

		// 一刀：streakAt 起光线划过（0.06s，拖尾晚 0.035s），cutAt 起色块划过（0.07s），openAt 起切口张开（0.07s，回弹）
		void Cut(Node2D bar, Node2D sweep, Line2D streak, bool fromStart, float time, float t0)
		{
			float dir = fromStart ? 1f : -1f;
			float head = Ease.OutCubic(Seg(time, t0, 0.06f));
			float tail = Ease.InCubic(Seg(time, t0 + 0.035f, 0.06f));
			float start = -len * 0.55f * dir;
			streak.Points = new[] { new Vector2(start + dir * len * 1.1f * tail, 0f), new Vector2(start + dir * len * 1.1f * head, 0f) };
			streak.DefaultColor = new Color(1f, 1f, 1f, time < t0 ? 0f : 0.95f * (1f - tail));
			streak.Width = 3f + 3f * (1f - head);
			float cut = Ease.OutExpo(Seg(time, t0 + 0.015f, 0.07f));
			float open = Seg(time, t0 + 0.07f, 0.07f);
			float thick = time < t0 + 0.07f ? 0.25f : 0.25f + 0.75f * Ease.OutBack(open);
			sweep.Scale = new Vector2(cut, 1f);
			bar.Scale = new Vector2(1f, thick);
		}

		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			Cut(w, ws, wl, true, time, 0f);
			Cut(n, ns, nl, false, time, 0.04f);
			float split = Ease.InCubic(Seg(time, 0.22f, 0.4f));
			if (split > 0f)
			{
				w.Scale = new Vector2(1f, 1f - 0.85f * split);
				n.Scale = new Vector2(1f, 1f - 0.85f * split);
			}
			w.Position = nw * 70f * split;
			n.Position = -nn * 70f * split;
			w.Modulate = n.Modulate = new Color(1f, 1f, 1f, 1f - split);
			Fx.SetParam(disc, "reveal", Ease.OutExpo(Seg(time, 0.08f, 0.14f)) * (1f - Ease.InCubic(Seg(time, 0.26f, 0.3f))));
		});
		// 两刀交叉的那一刻（深蓝那刀划过中心，约 0.1s）中心一闪，再碎玻璃
		P3Fx.Flash(layer, at, Pal.Ice, 320f, 0.16f, delay: 0.09f);
		Fx.After(root, 0.2, "knockdown shatter", () => P3Fx.GlassShatter(layer, at, 22, 1.0f, Pal.Cyan));
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

	/// <summary>
	/// v0.14.38（用户：问号事件选牌 / 看牌组时牌乱飞，有两张半透明的牌停在左上角）：
	/// 原版的卡牌格子（NGridCardHolder）是对象池复用的 —— 选牌界面关掉后，同一个格子会被下一个界面（事件选牌、牌组）拿去用。
	/// 以前我们的动画补间绑在格子上（Fx.Animate(格子, …)），界面关掉时还没跑完的补间跟着格子进了对象池（不在场景树里就暂停），
	/// 格子被复用后补间接着跑，把牌拖回选牌界面的旧坐标、压暗 —— 就是"乱飞"和左上角的半透明牌。
	/// 现在所有对原版格子的改动都：补间绑在选牌界面上（界面释放补间就没了），每一帧先确认格子还在这一排里（没被回收复用）才动它。
	/// </summary>
	private static bool StillInRow(Control h, Control row) =>
		GodotObject.IsInstanceValid(h) && GodotObject.IsInstanceValid(row) && h.GetParent() == row;

	// CardRow 可能还有翻页按钮等 Control；它们不是奖励牌，也不能参与发牌/翻牌。
	private static NGridCardHolder[] RewardHolders(Control row) => row.GetChildren()
		.OfType<NGridCardHolder>().Where(h => !h.IsQueuedForDeletion()).ToArray();

	/// <summary>first = 这次打开选牌界面的第一次（先播独立的 SHUFFLE TIME 标题动画再发牌）；重抽时直接发牌。</summary>
	public static void ShuffleIntro(Control screen, Control row, bool first = true)
	{
		var holders = RewardHolders(row);
		if (holders.Length == 0)
		{
			return;
		}
		if (!P3Settings.ShuffleFull)
		{
			QuickDeal(screen, row, holders, first);
			return;
		}
		foreach (Control h in holders)
		{
			h.Visible = false;                           // 发牌 / 翻牌之前先藏起来（含标题动画期间）
		}
		screen.GetNodeOrNull("P3ShuffleDeck")?.QueueFree();
		// 每次进来都换一个序号：标题动画还没播完就重抽时，旧的"延迟发牌"作废，不会发两次牌
		int token = (screen.HasMeta("p3_deal") ? screen.GetMeta("p3_deal").AsInt32() : 0) + 1;
		screen.SetMeta("p3_deal", token);
		if (first)
		{
			TitleIntro(screen);
			Fx.After(screen, TitleIntroLen, "shuffle deal", () =>
			{
				if (GodotObject.IsInstanceValid(row) && screen.GetMeta("p3_deal").AsInt32() == token)
				{
					Deal(screen, row);
				}
			});
		}
		// 兜底（v0.14.45：重抽也有 —— 以前只有首次打开才有，重抽时 Deal 出错牌就一直藏着、点不了）：无论如何最晚这时把牌显示出来
		Fx.After(screen, (first ? TitleIntroLen : 0f) + 3.0, "shuffle failsafe 2", () =>
		{
			if (!GodotObject.IsInstanceValid(screen) || screen.GetMeta("p3_deal").AsInt32() != token)
			{
				return;
			}
			foreach (Control h in holders)
			{
				if (StillInRow(h, row)) h.Visible = true;
			}
		});
		if (!first)
		{
			Deal(screen, row);
		}
	}

	/// <summary>
	/// v0.14.101 选卡「精简」（默认；用户：结算和选卡太拖沓，保留 Shuffle Time 特色但不耽误看牌选牌）：
	/// 点开就能看到、能点 —— 三张牌一开始就是正面，从中间同时向两边展开（左边那张往左、右边那张往右，同原版），0.3s 到位；
	/// 特色保留在不挡操作的地方：顶部 SHUFFLE TIME 字图带残影滑过（只在这次打开的第一次）、落位时的十字闪光、Shuffle Time 音效。
	/// </summary>
	private static void QuickDeal(Control screen, Control row, NGridCardHolder[] holders, bool first)
	{
		int n = holders.Length;
		screen.GetNodeOrNull("P3ShuffleTitle")?.QueueFree();
		screen.GetNodeOrNull("P3ShuffleDeck")?.QueueFree();
		int run = (screen.HasMeta("p3_deal_run") ? screen.GetMeta("p3_deal_run").AsInt32() : 0) + 1;
		screen.SetMeta("p3_deal_run", run);
		bool Current() => GodotObject.IsInstanceValid(screen) && screen.GetMeta("p3_deal_run").AsInt32() == run;
		Vector2 view = screen.GetViewportRect().Size;
		// 排版同 Deal：每次从原始整行缩放重新算，只缩整行
		if (!row.HasMeta("p3_reward_row_scale")) row.SetMeta("p3_reward_row_scale", row.Scale);
		Vector2 originalRowScale = row.GetMeta("p3_reward_row_scale").AsVector2();
		row.Scale = originalRowScale;
		float rowScaleX = row.GetGlobalTransform().X.Length();
		float cardWidth = holders.Max(h => Mathf.Max(300f, h.Hitbox.Size.X)) * 1.05f;
		float fit = ShuffleLayout.RowScale(n, view.X, rowScaleX, cardWidth, view.X * 0.06f);
		row.Scale = originalRowScale * fit;
		row.GlobalPosition = new Vector2(view.X * 0.5f, row.GlobalPosition.Y);
		float spacing = ShuffleLayout.Spacing;
		Vector2 origin = Vector2.Left * (n - 1) * spacing * 0.5f;
		Transform2D rowXf = row.GetGlobalTransform();
		float cardH = holders.Max(h => Mathf.Max(422f, h.Hitbox.Size.Y) * h.SmallScale.Y) * rowXf.Y.Length();
		const float spread = 0.3f;
		for (int i = 0; i < n; i++)
		{
			NGridCardHolder h = holders[i];
			Vector2 final = origin + Vector2.Right * spacing * i;
			Vector2 bs = h.SmallScale;
			h.Visible = true;
			h.Rotation = 0f;
			h.Position = Vector2.Zero;                   // 全部从中间出发（一直是正面）
			h.Scale = bs * 0.9f;
			h.Modulate = new Color(1f, 1f, 1f, 0f);
			// 补间绑在选牌界面上、每帧确认格子还在这一排里（对象池复用的教训，见 StillInRow）
			Fx.Animate(screen, spread, t =>
			{
				if (!Current() || !StillInRow(h, row)) return;
				h.Position = final * Ease.OutCubic(t);
				h.Scale = bs * (0.9f + 0.1f * Ease.OutBack(t));
				h.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(t * 4f, 0f, 1f));
			});
			Vector2 at = rowXf * final;
			Fx.After(screen, spread * 0.75, "shuffle quick glint", () => { if (Current()) Glint(screen, at, cardH); });
		}
		Audio.Sfx.Clip("ShuffleTime_Whoosh", 0.45f);
		Audio.Sfx.Clip("ShuffleTime_CardSet", 0.55f, spread * 0.8f);
		if (first) QuickTitle(screen);
	}

	/// <summary>精简模式的标题：原版字图 SHUFFLE TIME 在画面上方带两层残影从左滑到中间、停一下、横向化开淡出（共约 1s），不压暗、不挡点击。</summary>
	private static void QuickTitle(Control screen)
	{
		Texture2D? logo = ResourceLoader.Exists("res://p3makoto/ui/shuffle_title.png") ? ResourceLoader.Load<Texture2D>("res://p3makoto/ui/shuffle_title.png") : null;
		if (logo == null) return;
		Vector2 size = screen.GetViewportRect().Size;
		Node2D root = new() { Name = "P3ShuffleTitle", ZIndex = 50 };
		screen.AddChild(root);
		root.TopLevel = true;
		root.GlobalPosition = Vector2.Zero;
		Audio.Sfx.Clip("ShuffleTime_Start", 0.6f);
		const float tilt = -0.12f;
		float ls = size.X * 0.34f / logo.GetWidth();
		Vector2 at = new(size.X * 0.5f, size.Y * 0.14f);
		var layers = new System.Collections.Generic.List<(Sprite2D S, float Lag, float A)>();
		foreach ((float lag, float a, bool glow) in new[] { (0.06f, 0.25f, true), (0.03f, 0.45f, true), (0f, 1f, false) })
		{
			Sprite2D sp = new() { Texture = logo, Scale = Vector2.One * ls, Rotation = tilt, Skew = -0.05f, Modulate = glow ? new Color(0.3f, 0.6f, 1f, 0f) : new Color(1f, 1f, 1f, 0f) };
			if (glow) sp.Material = Fx.Additive;
			root.AddChild(sp);
			layers.Add((sp, lag, a));
		}
		Fx.Animate(root, 1.0, t =>
		{
			float time = t * 1.0f;
			foreach (var (sp, lag, a) in layers)
			{
				float u = Mathf.Clamp((time - lag) / 0.25f, 0f, 1f);
				float x = Mathf.Lerp(-size.X * 0.35f, 0f, Ease.OutExpo(u));
				float fade = time < 0.7f ? Mathf.Clamp(u * 3f, 0f, 1f) : 1f - Mathf.Clamp((time - 0.7f) / 0.3f, 0f, 1f);
				sp.Position = at + new Vector2(x, 0f).Rotated(tilt);
				sp.Scale = new Vector2(ls * (1f + (time > 0.7f ? (time - 0.7f) * 1.2f : 0f)), ls);
				sp.Modulate = new Color(sp.Modulate.R, sp.Modulate.G, sp.Modulate.B, a * fade);
			}
		});
		Fx.FreeAfter(root, 1.05);
	}

	private static void Deal(Control screen, Control row)
	{
		var holders = RewardHolders(row);
		int n = holders.Length;
		if (n == 0)
		{
			return;
		}
		screen.GetNodeOrNull("P3ShuffleTitle")?.QueueFree();   // v0.14.13：原版在发牌时没有常驻标题条
		screen.GetNodeOrNull("P3ShuffleDeck")?.QueueFree();
		// 这一轮发牌的编号：被新一轮（重抽）取代后，本轮还没触发的翻牌 / 兜底回调全部作废，不会提前把新一轮的牌翻开
		int run = (screen.HasMeta("p3_deal_run") ? screen.GetMeta("p3_deal_run").AsInt32() : 0) + 1;
		screen.SetMeta("p3_deal_run", run);
		int introToken = screen.GetMeta("p3_deal").AsInt32();
		bool Current() => GodotObject.IsInstanceValid(screen)
			&& screen.GetMeta("p3_deal_run").AsInt32() == run
			&& screen.GetMeta("p3_deal").AsInt32() == introToken;
		Vector2 view = screen.GetViewportRect().Size;
		Texture2D? back = ResourceLoader.Exists("res://p3makoto/fx/tarot_back.png") ? ResourceLoader.Load<Texture2D>("res://p3makoto/fx/tarot_back.png") : null;
		// 每次重抽从原始整行缩放重新算，避免 3→4→3 张时越缩越小。
		if (!row.HasMeta("p3_reward_row_scale")) row.SetMeta("p3_reward_row_scale", row.Scale);
		Vector2 originalRowScale = row.GetMeta("p3_reward_row_scale").AsVector2();
		row.Scale = originalRowScale;
		float rowScaleX = row.GetGlobalTransform().X.Length();
		// 按悬停时的完整牌宽预留余量；只缩整行，原版的悬停放大仍然有效。
		float cardWidth = holders.Max(h => Mathf.Max(300f, h.Hitbox.Size.X)) * 1.05f;
		float fit = ShuffleLayout.RowScale(n, view.X, rowScaleX, cardWidth, view.X * 0.06f);
		row.Scale = originalRowScale * fit;
		row.GlobalPosition = new Vector2(view.X * 0.5f, row.GlobalPosition.Y);
		float spacing = ShuffleLayout.Spacing;
		Vector2 origin = Vector2.Left * (n - 1) * spacing * 0.5f;
		Transform2D rowXf = row.GetGlobalTransform();
		float cardH = holders.Max(h => Mathf.Max(422f, h.Hitbox.Size.Y) * h.SmallScale.Y) * rowXf.Y.Length();
		var baseScales = new Vector2[n];
		var targets = new Vector2[n];
		for (int i = 0; i < n; i++)
		{
			Control h = holders[i];
			Vector2 final = origin + Vector2.Right * spacing * i;
			baseScales[i] = holders[i].SmallScale;
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
			Node2D c = new() { Position = targets[i], Scale = Vector2.Zero };
			deck.AddChild(c);
			Sprite2D glow = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(Pal.Cyan, 0.35f), Scale = new Vector2(cardH * 0.9f, cardH * 1.25f) / Fx.SoftDot.GetWidth() };
			c.AddChild(glow);
			Sprite2D face = new() { Texture = back, Scale = Vector2.One * sc };
			c.AddChild(face);
			cards.Add((c, face, glow));
		}
		// v0.14.13（对照 P3R 分镜 art/p3r_tex/shuffle/shuffle_time_storyboard_detail_65frames.png 第 23~27 帧）：
		// 牌从画面上方、各自位置的正上方斜着落下来 —— 入画时倾斜约 45°，边落边转正，越落越慢（约 0.45s），身后拖两道淡蓝残影；
		// 几张牌依次落下（间隔 0.12s），落定时轻轻一顿（"嗒"）。全部落定后再一起翻面。
		const float startAt = ShuffleLayout.StartAt, fall = ShuffleLayout.Fall;
		float gap = ShuffleLayout.Gap(n);
		float flipAt = ShuffleLayout.FlipAt(n);
		var starts = new Vector2[n];
		var spins = new float[n];
		for (int i = 0; i < n; i++)
		{
			float sgn = i % 2 == 0 ? -1f : 1f;
			starts[i] = targets[i] + new Vector2(sgn * 70f, -view.Y * 0.75f);
			spins[i] = sgn * 0.8f;
			Audio.Sfx.Clip("ShuffleTime_CardSet", 0.55f, startAt + gap * i + fall * 0.95f);
		}
		Audio.Sfx.Clip("ShuffleTime_Whoosh", 0.5f, startAt);
		// 残影：每张牌两道，沿同一条轨迹落后 0.03 / 0.06s
		var ghosts = new System.Collections.Generic.List<(Sprite2D S, int I, float Lag)>();
		for (int i = 0; i < n; i++)
		{
			for (int k = 1; k <= 2; k++)
			{
				Sprite2D g = new() { Texture = back, Scale = Vector2.One * sc, Material = Fx.Additive, Modulate = new Color(0.4f, 0.7f, 1f, 0f) };
				deck.AddChild(g);
				deck.MoveChild(g, 0);
				ghosts.Add((g, i, 0.035f * k));
			}
		}
		(Vector2 Pos, float Rot) At(int i, float time)
		{
			float u = Mathf.Clamp((time - startAt - gap * i) / fall, 0f, 1f);
			float e = Ease.OutCubic(u);
			return (starts[i].Lerp(targets[i], e), spins[i] * (1f - Ease.OutCubic(Mathf.Min(1f, u * 1.15f))));
		}
		Fx.Animate(deck, flipAt + 0.05f, t =>
		{
			float time = t * (flipAt + 0.05f);
			for (int i = 0; i < n; i++)
			{
				(Node2D c, Sprite2D face, Sprite2D glow) = cards[i];
				float u = Mathf.Clamp((time - startAt - gap * i) / fall, 0f, 1f);
				var (pos, rot) = At(i, time);
				float land = u >= 1f ? Mathf.Max(0f, 1f - (time - startAt - gap * i - fall) / 0.18f) : 0f;
				c.Position = pos;
				c.Rotation = rot;
				c.Scale = u <= 0f ? Vector2.Zero : new Vector2(1f + 0.03f * Mathf.Sin(Mathf.Pi * land), 1f - 0.03f * Mathf.Sin(Mathf.Pi * land));
				c.ZIndex = 10 + i;
				glow.Modulate = new Color(Pal.Cyan, 0.2f + 0.3f * (1f - u) * (u > 0f ? 1f : 0f) + 0.35f * land);
			}
			foreach (var (g, i, lag) in ghosts)
			{
				float u = Mathf.Clamp((time - lag - startAt - gap * i) / fall, 0f, 1f);
				var (pos, rot) = At(i, time - lag);
				g.Position = pos;
				g.Rotation = rot;
				g.Modulate = new Color(0.4f, 0.7f, 1f, u > 0f && u < 1f ? 0.35f - lag * 3f : 0f);
			}
		});
		// 一起翻面：牌背收窄成线（0.1s）→ 真卡展开（0.2s, OutBack）
		Fx.After(deck, flipAt, "shuffle flip", () =>
		{
			if (!Current() || !GodotObject.IsInstanceValid(deck))
			{
				return;
			}
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
				if (!Current() || !GodotObject.IsInstanceValid(deck))
				{
					return;
				}
				deck.QueueFree();
				for (int i = 0; i < n; i++)
				{
					Control h = holders[i];
					if (!StillInRow(h, row))
					{
						continue;
					}
					Vector2 bs = baseScales[i];
					h.Visible = true;
					h.Scale = new Vector2(0.01f, bs.Y);
					Fx.Animate(screen, 0.2f, t => { if (Current() && StillInRow(h, row)) h.Scale = new Vector2(bs.X * Mathf.Max(0.01f, Ease.OutBack(t)), bs.Y); });
					Glint(screen, targets[i], cardH);
				}
			});
		});
		// 兜底：无论如何 2.2s 后把牌显示出来
		Fx.After(screen, flipAt + 0.6f, "shuffle failsafe", () =>
		{
			if (!Current())
			{
				return;
			}
			for (int i = 0; i < n; i++)
			{
				Control h = holders[i];
				if (StillInRow(h, row) && !h.Visible)
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
	/// P3R：选牌前先单独播一段「SHUFFLE TIME」标题（v0.14.13，对照原版分镜第 9~22 帧，约 1.15s）：
	///   0.00  画面压暗成深蓝，一道斜向的蓝色光带亮起
	///   0.05  原版字图「SHUFFLE TIME」（白字 + 深蓝描边，向右上倾斜约 7°）从左下沿斜线滑入，身后拖着三层越来越大、越来越淡的蓝色残影
	///   0.40  主字到位后缓慢漂移，残影继续往右上散开、变淡
	///   0.75  主字也化开：横向拉长、变成蓝色、淡出；只剩一团斜向的蓝色柔光抹过画面（原版第 18~22 帧）
	///   1.15  发牌
	/// </summary>
	private static void TitleIntro(Control screen)
	{
		Vector2 size = screen.GetViewportRect().Size;
		Node2D root = new() { Name = "P3ShuffleTitleIntro", ZIndex = 50 };
		screen.AddChild(root);
		root.TopLevel = true;
		root.GlobalPosition = Vector2.Zero;
		Audio.Sfx.Clip("ShuffleTime_Start", 0.85f);
		ColorRect dim = new() { Size = size, Color = new Color(0.01f, 0.03f, 0.14f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(dim);
		Vector2 c = size / 2f;
		const float tilt = -0.12f;
		Vector2 dir = Vector2.Right.Rotated(tilt);           // 字的走向（向右上）
		// 斜向蓝色光带（柔光，压在字下面）
		Sprite2D band = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Position = c, Rotation = tilt, Modulate = new Color(0.15f, 0.35f, 1f, 0f) };
		root.AddChild(band);
		float bandScale = 64f / Fx.SoftDot.GetWidth();
		Texture2D? logo = ResourceLoader.Exists("res://p3makoto/ui/shuffle_title.png") ? ResourceLoader.Load<Texture2D>("res://p3makoto/ui/shuffle_title.png") : null;
		float ls = logo != null ? size.X * 0.62f / logo.GetWidth() : 1f;
		Node2D MakeWord(Color fill, Color line, bool outline)
		{
			Node2D w = new() { Rotation = tilt, Skew = -0.05f };
			root.AddChild(w);
			if (logo != null)
			{
				if (outline)
				{
					foreach (Vector2 o in new[] { new Vector2(-5, 0), new Vector2(5, 0), new Vector2(0, -5), new Vector2(0, 5), new Vector2(-4, -4), new Vector2(4, 4), new Vector2(-4, 4), new Vector2(4, -4) })
					{
						w.AddChild(new Sprite2D { Texture = logo, Scale = Vector2.One * ls, Position = o, Modulate = line });
					}
				}
				w.AddChild(new Sprite2D { Texture = logo, Scale = Vector2.One * ls, Modulate = fill });
			}
			else
			{
				int fs = (int)(size.Y * 0.14f);
				Typo.Text(w, "SHUFFLE TIME", Typo.LatinItalic, fs, fill, new Vector2(-fs * 3.1f, -fs * 0.62f), outline: outline ? 6 : 0, outlineColor: line);
			}
			return w;
		}
		// 残影（原版第 11~17 帧：主字上下各叠着几层更大、更淡的蓝色同款字，往右上散开）
		var echoes = new System.Collections.Generic.List<(Node2D W, float Lag, float Scale, float Side)>();
		for (int k = 3; k >= 1; k--)
		{
			Node2D e = MakeWord(new Color(0.22f, 0.5f, 1f, 0.6f - 0.12f * k), new Color(0.05f, 0.15f, 0.6f, 0.4f - 0.08f * k), false);
			echoes.Add((e, 0.05f * k, 1f + 0.1f * k, k % 2 == 0 ? -1f : 1f));
		}
		Node2D main = MakeWord(Pal.White, new Color(0.04f, 0.1f, 0.42f), true);
		Vector2 from = c - dir * size.X * 0.7f + new Vector2(0f, size.Y * 0.08f);
		Fx.Animate(root, TitleIntroLen, t =>
		{
			float time = t * TitleIntroLen;
			float outT = Ease.InCubic(Mathf.Clamp((time - 0.75f) / 0.35f, 0f, 1f));
			dim.Color = new Color(0.01f, 0.03f, 0.14f, 0.65f * Mathf.Clamp(time / 0.1f, 0f, 1f) * (1f - Mathf.Clamp((time - 1.0f) / 0.15f, 0f, 1f)));
			float bandOn = Mathf.Clamp(time / 0.15f, 0f, 1f);
			band.Scale = new Vector2(size.X * 0.9f * (0.6f + 0.6f * outT), size.Y * 0.28f * (1f - 0.5f * outT)) / 64f * bandScale;
			band.Position = c + dir * size.X * 0.25f * outT;
			band.Modulate = new Color(0.15f, 0.35f, 1f, (0.55f + 0.35f * outT) * bandOn * (1f - Mathf.Clamp((time - 1.0f) / 0.15f, 0f, 1f)));
			// 主字：0.05 → 0.4 从左下滑到中心（OutExpo），之后缓慢漂移；0.75 起横向拉长、变蓝、淡出
			float slide = Ease.OutExpo(Mathf.Clamp((time - 0.05f) / 0.35f, 0f, 1f));
			main.Position = from.Lerp(c, slide) + dir * 30f * Mathf.Max(0f, time - 0.4f) + dir * size.X * 0.15f * outT;
			main.Scale = new Vector2(1f + 0.5f * outT, 1f - 0.2f * outT);
			main.Modulate = new Color(1f - 0.7f * outT, 1f - 0.5f * outT, 1f, Mathf.Clamp((time - 0.05f) / 0.05f, 0f, 1f) * (1f - outT));
			foreach (var (e, lag, scale, side) in echoes)
			{
				float es = Ease.OutExpo(Mathf.Clamp((time - 0.05f - lag) / 0.35f, 0f, 1f));
				float spread = Ease.OutCubic(Mathf.Clamp((time - 0.3f) / 0.6f, 0f, 1f));
				float k = (scale - 1f) * 10f;   // 1..3
				e.Position = from.Lerp(c, es) + dir * 90f * k * spread + dir.Orthogonal() * side * 70f * k * spread;
				e.Scale = Vector2.One * Mathf.Lerp(1f, scale, spread);
				e.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp((time - 0.05f - lag) / 0.05f, 0f, 1f) * (1f - Mathf.Clamp((time - 0.6f) / 0.4f, 0f, 1f)));
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
			foreach (Control other in RewardHolders(row))
			{
				if (other == picked) continue;
				Vector2 p0 = other.Position;
				Fx.Animate(screen, 0.3f, t =>
				{
					if (!StillInRow(other, row)) return;
					other.Position = p0 + new Vector2(0f, 40f * Ease.InCubic(t));
					other.Modulate = new Color(1f - 0.5f * t, 1f - 0.5f * t, 1f - 0.4f * t, 1f - 0.6f * t);
				});
			}
		}
	}
}
