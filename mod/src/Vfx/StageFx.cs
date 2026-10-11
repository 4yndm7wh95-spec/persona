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
		Vector2 size = layer.GetViewportRect().Size;
		float cy = size.Y * 0.42f, h = size.Y * 0.13f;
		Node2D root = Fx.Root(layer, new Vector2(size.X * 0.5f, cy), "P3TurnBanner");
		root.TopLevel = true;
		root.GlobalPosition = new Vector2(size.X * 0.5f, cy);
		root.Rotation = -0.105f;
		Color slabB = player ? new Color(0.95f, 0.97f, 1f, 0.97f) : new Color(0.32f, 0.03f, 0.1f, 0.96f);
		Color word = player ? new Color(0.03f, 0.08f, 0.3f) : Pal.White;
		Color accent = player ? new Color(Pal.Cyan, 0.9f) : new Color(0.95f, 0.15f, 0.3f, 0.9f);

		Node2D navy = new();
		root.AddChild(navy);
		P3Ui.Slab(navy, -size.X, size.X, -h * 0.5f, h * 0.5f, h * 0.5f, new Color(0.02f, 0.06f, 0.24f, 0.94f));
		P3Ui.Slab(navy, -size.X, size.X, h * 0.5f, h * 0.5f + 6f, 3f, accent);
		Node2D slab = new();
		root.AddChild(slab);
		P3Ui.Slab(slab, -size.X * 0.32f, size.X * 0.42f, -h * 0.36f, h * 0.36f, h * 0.36f, slabB);
		Node2D red = new() { Position = new Vector2(-size.X * 0.34f, h * 0.36f) };
		root.AddChild(red);
		P3Ui.Slab(red, 0f, h * 0.12f, -h * 0.72f, 0f, h * 0.36f, new Color(0.9f, 0.08f, 0.22f, 0.95f));

		Node2D text = new() { Skew = -0.25f, Position = new Vector2(-size.X * 0.22f, 0f) };
		root.AddChild(text);
		int fs = (int)(h * 0.62f);
		Typo.Text(text, player ? "PLAYER TURN" : "ENEMY TURN", Typo.Latin, fs, word, new Vector2(0f, -fs * 0.62f),
			shadow: new Color(0.9f, 0.08f, 0.22f, player ? 0.85f : 0.0f), shadowOffset: new Vector2(5f, 4f));

		Vector2 discAt = new(-size.X * 0.4f, -h * 0.1f);
		float discR = h * 1.05f;
		ColorRect disc = P3Ui.WaterDisc(root, discAt, discR);
		Node2D arcs = new() { Position = discAt };
		root.AddChild(arcs);
		P3Ui.Arc(arcs, Vector2.Zero, discR * 1.12f, 0f, Mathf.Pi * 1.3f, accent, 3f);
		Node2D num = new() { Position = discAt, Skew = -0.2f };
		root.AddChild(num);
		if (player)
		{
			int ns = (int)(discR * 1.05f);
			Typo.Text(num, "TURN", Typo.Latin, (int)(discR * 0.26f), new Color(Pal.Ice, 0.9f), new Vector2(-discR * 0.5f, -discR * 0.82f));
			Typo.Text(num, round.ToString(), Typo.Latin, ns, Pal.White, new Vector2(-ns * 0.3f * round.ToString().Length, -ns * 0.6f),
				shadow: new Color(0.02f, 0.06f, 0.24f, 0.9f), shadowOffset: new Vector2(5f, 5f));
		}

		const float tIn = 0.28f, hold = 0.55f, tOut = 0.26f, total = tIn + hold + tOut;
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			float a = Ease.OutExpo(Seg(time, 0f, 0.24f));
			float b = Ease.OutExpo(Seg(time, 0.03f, 0.24f));
			float d = Seg(time, tIn, hold);
			float o = Ease.InExpo(Seg(time, tIn + hold, tOut));
			navy.Position = new Vector2(-size.X * 1.1f * (1f - a) - 20f * d + size.X * 1.2f * o, 0f);
			slab.Position = new Vector2(size.X * 1.1f * (1f - b) + 25f * d - size.X * 1.2f * o, 0f);
			navy.Scale = slab.Scale = new Vector2(1f, 1f - 0.8f * o);
			red.Scale = new Vector2(1f, Ease.OutBack(Seg(time, 0.12f, 0.14f)) * (1f - o));
			float u = Ease.OutExpo(Seg(time, 0.07f, 0.3f));
			text.Position = new Vector2(-size.X * 0.22f + 300f * (1f - u) + 30f * d - size.X * 0.9f * o, 0f);
			text.Scale = new Vector2(Mathf.Lerp(1.5f, 1f, u), 1f);
			text.Modulate = new Color(1f, 1f, 1f, u);
			Fx.SetParam(disc, "reveal", Ease.OutExpo(Seg(time, 0.05f, 0.28f)) * (1f - Ease.InCubic(Seg(time, tIn + hold, tOut))));
			arcs.Rotation = 0.6f * time;
			arcs.Modulate = new Color(1f, 1f, 1f, a * (1f - o));
			float n = Ease.OutBack(Seg(time, 0.12f, 0.22f));
			num.Scale = Vector2.One * Mathf.Lerp(1.8f, 1f, n) * (1f - o);
			num.Modulate = new Color(1f, 1f, 1f, Seg(time, 0.12f, 0.08f) * (1f - o));
			root.Modulate = new Color(1f, 1f, 1f, 1f - Seg(time, tIn + hold + tOut * 0.6f, tOut * 0.4f));
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
	/// 战斗胜利（P3R 结算界面风格）：左侧白色斜切大色块 + 红边从左切入，右侧巨大水之圆张开（两道圆弧），
	/// 白块上旋转的超大深蓝 "VICTORY" 从下方滑上来，右上红字标签 "BATTLE CLEAR"，蓝蝶从圆里飞出。
	/// 入 0.4s → 停 1.0s → 出 0.4s（白块向左、圆收拢、字继续上滑淡出）。
	/// </summary>
	public static void Victory(Control layer)
	{
		Vector2 size = layer.GetViewportRect().Size;
		Node2D root = Fx.Root(layer, Vector2.Zero, "P3Victory");
		root.TopLevel = true;
		root.GlobalPosition = Vector2.Zero;
		ColorRect dim = new() { Size = size, Color = new Color(0.01f, 0.03f, 0.1f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(dim);

		Vector2 discAt = new(size.X * 0.72f, size.Y * 0.5f);
		float discR = size.Y * 0.46f;
		ColorRect disc = P3Ui.WaterDisc(root, discAt, discR);
		Node2D arcs = new() { Position = discAt };
		root.AddChild(arcs);
		P3Ui.Arc(arcs, Vector2.Zero, discR * 1.07f, 0f, Mathf.Pi * 1.4f, new Color(Pal.Cyan, 0.6f), 3f);
		P3Ui.Arc(arcs, Vector2.Zero, discR * 1.15f, Mathf.Pi * 1.1f, Mathf.Pi * 1.7f, new Color(1f, 1f, 1f, 0.5f), 2f);

		Node2D white = new();
		root.AddChild(white);
		P3Ui.Slab(white, -300f, size.X * 0.42f, -40f, size.Y + 40f, size.Y * 0.28f, new Color(0.95f, 0.97f, 1f, 0.95f));
		P3Ui.Slab(white, size.X * 0.42f - 4f, size.X * 0.42f + 12f, -40f, size.Y + 40f, size.Y * 0.28f, new Color(0.9f, 0.08f, 0.22f, 0.95f));
		Node2D word = P3Ui.GiantWord(white, "VICTORY", new Vector2(size.X * 0.05f, size.Y * 0.95f), (int)(size.Y * 0.26f), -1.05f, new Color(0.03f, 0.08f, 0.3f, 0.95f));
		Node2D tag = P3Ui.RedTag(root, "BATTLE CLEAR", new Vector2(size.X * 0.58f, size.Y * 0.16f), (int)(size.Y * 0.05f), cjk: false);

		const float tIn = 0.4f, hold = 1.0f, tOut = 0.4f, total = tIn + hold + tOut;
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			float a = Ease.OutExpo(Seg(time, 0f, 0.32f));
			float d = Seg(time, tIn, hold);
			float o = Seg(time, tIn + hold, tOut);
			float oe = Ease.InExpo(o);
			dim.Color = new Color(0.01f, 0.03f, 0.1f, 0.45f * a * (1f - o));
			white.Position = new Vector2(-size.X * 0.6f * (1f - a) - 20f * d - size.X * 0.7f * oe, 0f);
			word.Position = new Vector2(size.X * 0.05f, size.Y * 0.95f + size.Y * 0.5f * (1f - Ease.OutExpo(Seg(time, 0.08f, 0.4f))) - 40f * d - size.Y * 0.4f * oe);
			Fx.SetParam(disc, "reveal", Ease.OutExpo(Seg(time, 0.06f, 0.36f)) * (1f - Ease.InCubic(o)));
			arcs.Rotation = 0.35f * time;
			arcs.Modulate = new Color(1f, 1f, 1f, a * (1f - o));
			float k = Seg(time, 0.2f, 0.18f);
			tag.Scale = Vector2.One * Mathf.Lerp(1.8f, 1f, Ease.OutBack(k));
			tag.Modulate = new Color(1f, 1f, 1f, k * (1f - o));
			root.Modulate = new Color(1f, 1f, 1f, 1f - Seg(time, tIn + hold + tOut * 0.5f, tOut * 0.5f));
		});
		Fx.After(root, 0.3, "victory butterflies", () => P3Fx.ButterflyBurst(layer, discAt, 9, size.Y * 0.5f));
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
		Node2D root = new() { Name = "P3RewardBackdrop" };
		screen.AddChild(root);
		screen.MoveChild(root, 0);
		root.TopLevel = true;
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
					Node2D g = new() { TopLevel = true };
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
