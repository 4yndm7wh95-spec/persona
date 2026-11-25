using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 战斗开始 / 回合横幅（2026-11-19，用户：替换原版的每回合数、战斗开始的 UI，参考 P3 mod）。
/// 照 P3 mod 的做法：原版「战斗开始」横幅只藏画面、节点照常跑（它播完负责生成第 1 回合的横幅）；回合横幅不再建原版的，换成这里的。
/// 风格接选卡界面「夺宝」的剪报拼贴（Codex 素材 art/p5/ui/battle，任务单 docs/codex/codex_p5_battle_ui_art.md）：
/// · 战斗开始：左上角顶栏下方一条红色剪报纸带从左边扫进来，「TAKE YOUR HEART!」字母纸片一片片拍上去，停一眼往左收走（约 1.3s）；
/// · 回合横幅：P3 的教训（横贯屏幕中央太挡）→ 收成左上角顶栏下方一条短纸条、画在人物后面，红色小纸牌上是拼贴数字，
///   「YOUR TURN」/「ENEMY TURN」/ 额外回合「ONE MORE!」，入 0.2s → 停 → 往左收走，约 1.1s。
/// 素材缺哪张（Fx.Tex 拿不到）就用代码画的纸片 + 粗斜体字顶上，动画一样。
/// </summary>
public static class BattleUiFx
{
	private static readonly Color Red = new(0.9f, 0.08f, 0.1f), Ink = new(0.04f, 0.03f, 0.03f), Paper = new(0.96f, 0.94f, 0.92f);

	// ───────────────── 字母纸片 ─────────────────

	/// <summary>一片字母纸片（居中在 at，纸片宽约 size）。tex = 素材名（battle_TAKE_1 之类），ch = 素材缺时画的字。</summary>
	private static Node2D Piece(Node2D parent, string tex, string ch, Vector2 at, float size, int index, bool hot)
	{
		Node2D p = new() { Position = at };
		parent.AddChild(p);
		if (Fx.Tex(tex) is Texture2D t)
		{
			// 字母 / 数字图 512（半尺寸 256）里纸片约占宽 60%
			p.AddChild(new Sprite2D { Texture = t, Scale = Vector2.One * size / (t.GetWidth() * 0.6f) });
			return p;
		}
		// 占位：歪一点的纸片 + 投影 + 粗斜体字，红黑白轮换（hot = 偏红，敌人回合）
		int k = (index + (hot ? 1 : 0)) % 3;
		Color bg = k == 0 ? Paper : k == 1 ? Ink : Red;
		Color fg = k == 0 ? Ink : k == 1 ? Paper : Paper;
		if (hot && k == 0) (bg, fg) = (Red, Ink);
		float w = size * Fx.Rand(0.78f, 0.95f), h = size * Fx.Rand(0.95f, 1.12f);
		Vector2[] quad =
		{
			new(-w * 0.5f + Fx.Rand(-6f, 6f), -h * 0.5f + Fx.Rand(-5f, 5f)), new(w * 0.5f + Fx.Rand(-6f, 6f), -h * 0.5f + Fx.Rand(-5f, 5f)),
			new(w * 0.5f + Fx.Rand(-6f, 6f), h * 0.5f + Fx.Rand(-5f, 5f)), new(-w * 0.5f + Fx.Rand(-6f, 6f), h * 0.5f + Fx.Rand(-5f, 5f)),
		};
		Vector2[] shadow = Array.ConvertAll(quad, v => v + new Vector2(5f, 6f) * size / 100f);
		Fx.Poly(p, shadow, new Color(0f, 0f, 0f, 0.55f));
		Fx.Poly(p, quad, bg);
		int fs = (int)(size * Fx.Rand(0.82f, 1.0f));
		Font font = P5Ui.Font(index % 2 == 0 ? P5Ui.Barlow : P5Ui.BarlowBold);
		float tw = P5Ui.Width(font, ch, fs);
		P5Ui.Text(p, ch, font, fs, fg, new Vector2(-tw * 0.5f, -fs * 0.66f));
		return p;
	}

	private sealed record Word(string Name, string Text);

	/// <summary>一行单词排开（居中在 center），返回每片纸片和它最终的位置 / 角度。</summary>
	private static List<(Node2D node, Vector2 at, float rot)> Line(Node2D parent, IReadOnlyList<Word> words, Vector2 center, float size, bool hot, float u)
	{
		float step = size * 0.92f, gap = size * 0.42f;
		float total = -gap;
		foreach (Word w in words) total += w.Text.Length * step + gap;
		float x = center.X - total * 0.5f + step * 0.5f;
		var list = new List<(Node2D, Vector2, float)>();
		int n = 0;
		foreach (Word w in words)
		{
			for (int i = 0; i < w.Text.Length; i++)
			{
				string ch = w.Text[i].ToString();
				float s = ch == "!" ? size * 1.1f : size * Fx.Rand(0.9f, 1.1f);
				Vector2 at = new(x, center.Y + Fx.Rand(-10f, 10f) * u);
				float rot = Fx.Rand(-0.13f, 0.13f);
				Node2D piece = Piece(parent, $"battle_{w.Name}_{i + 1}", ch, at, s, n++, hot);
				piece.Rotation = rot;
				piece.Modulate = new Color(1f, 1f, 1f, 0f);
				list.Add((piece, at, rot));
				x += step;
			}
			x += gap;
		}
		return list;
	}

	/// <summary>纸片一片一片拍上来：从 1.8 倍、多转一点，OutBack 落定。</summary>
	private static void Slap(Node2D piece, float rot, float scale, float delay, float dur = 0.11f)
	{
		Fx.Animate(piece, 0, delay + dur, t =>
		{
			float s = t * (delay + dur) - delay;
			if (s < 0f) return;
			float e = Ease5.OutBack(Mathf.Min(1f, s / dur));
			piece.Scale = Vector2.One * scale * Mathf.Lerp(1.8f, 1f, e);
			piece.Rotation = rot + 0.45f * (1f - Mathf.Min(1f, e));
			piece.Modulate = new Color(1f, 1f, 1f, Mathf.Min(1f, s / 0.03f));
		});
	}

	// ───────────────── 战斗开始 ─────────────────

	public const float StartLength = 1.3f;

	/// <summary>
	/// 战斗开始（用户 2026-11-19：不要放屏幕中间，一样放左上角）：顶栏下方一条红色剪报纸带（选卡界面那条，黑边红底）从左边扫进来，
	/// 「TAKE YOUR HEART!」字母纸片一片片拍上去，停一眼往左收走；之后原版接着出第 1 回合的横幅（同一个位置）。
	/// layer = 人物后面那层特效层（没有就用原版横幅节点），自己换算成屏幕坐标。
	/// </summary>
	public static void BattleStart(Control layer)
	{
		Vector2 view = Fx.Screen(layer);
		float u = view.Y / 1080f;
		Node2D root = Fx.ScreenRoot(layer, "P5BattleStart");
		Fx.FreeAfter(root, StartLength + 0.1f);
		float cy = view.Y * 0.2f;

		var text = new[] { new Word("TAKE", "TAKE"), new Word("YOUR", "YOUR"), new Word("HEART", "HEART!") };
		float size = 44f * u;                       // 用户 2026-11-19 实机：太大、压到遗物栏 → 缩到约 2/3
		int letters = 0;
		foreach (Word wd in text) letters += wd.Text.Length;
		float lineW = letters * size * 0.92f + (text.Length - 1) * size * 0.42f;
		float lineX = view.X * 0.04f + lineW * 0.5f;
		float w = (lineX + lineW * 0.5f + 50f * u) / 0.9f;

		// 红色剪报纸带，左端出血到屏幕外
		Node2D band = new() { Position = new Vector2(w * 0.42f, cy), Rotation = -0.04f };
		root.AddChild(band);
		if (Fx.Tex("reward_band") is Texture2D bt)
		{
			band.AddChild(new Sprite2D { Texture = bt, Scale = new Vector2(w / (bt.GetWidth() * 0.9f), 100f * u / (bt.GetHeight() * 0.62f)) });
		}
		else
		{
			P5Ui.Slab(band, -w * 0.5f, w * 0.5f, -42f * u, 42f * u, 14f * u, Ink);
			P5Ui.Slab(band, -w * 0.5f, w * 0.5f, -32f * u, 32f * u, 14f * u, Red);
		}

		Node2D words = new();
		root.AddChild(words);
		var pieces = Line(words, text, new Vector2(lineX, cy - 4f * u), size, false, u);
		for (int i = 0; i < pieces.Count; i++)
		{
			Slap(pieces[i].node, pieces[i].rot, 1f, 0.08f + 0.024f * i, 0.1f);
		}

		Sfx.Take(Sfx.CutIn, 0.42f);
		Sfx.Take("gx_slice", 0.22f, 0.02f);
		Sfx.Take("gx_card_stack", 0.18f, 0.12f);
		Sfx.Take("gx_card_stack", 0.16f, 0.24f);
		Sfx.Take("gx_card_stack", 0.2f, 0.36f);

		const float outAt = 1.02f, outLen = 0.24f;
		Fx.Animate(root, 0, StartLength, t =>
		{
			float time = t * StartLength;
			float a = Ease5.OutExpo(Ease5.Seg(time, 0f, 0.2f));
			float o = Ease5.Seg(time, outAt, outLen);
			float oe = o * o * o;
			float drift = 12f * u * Ease5.Seg(time, 0.2f, 0.82f);
			band.Position = new Vector2(w * 0.42f - (w + 120f) * (1f - a) + drift - (w + 120f) * oe, cy);
			words.Position = new Vector2(drift * 1.4f - (w + 160f) * oe, 0f);
			root.Modulate = new Color(1f, 1f, 1f, 1f - Ease5.Seg(time, outAt + outLen * 0.5f, outLen * 0.5f));
		});
	}

	// ───────────────── 回合横幅 ─────────────────

	public const float TurnLength = 1.15f;

	/// <summary>回合横幅：layer = 人物后面那层特效层。player = 我方回合（round = 第几回合，extra = 额外回合），否则敌人回合。</summary>
	public static void TurnBanner(Control layer, bool player, int round, bool extra)
	{
		Vector2 view = Fx.Screen(layer);
		float u = view.Y / 1080f;
		Node2D root = Fx.ScreenRoot(layer, "P5TurnBanner");
		Fx.FreeAfter(root, TurnLength + 0.1f);
		float cy = view.Y * 0.2f;

		// 字：YOUR TURN / ONE MORE! / ENEMY TURN（先算长度，纸条按字的长度伸长）
		Word[] text = !player ? new[] { new Word("ENEMY", "ENEMY"), new Word("TURN", "TURN") }
			: extra ? new[] { new Word("ONE", "ONE"), new Word("MORE", "MORE!") }
			: new[] { new Word("YOUR", "YOUR"), new Word("TURN", "TURN") };
		float size = 38f * u;                       // 用户 2026-11-19 实机：太大 → 缩到约 2/3
		int letters = 0;
		foreach (Word wd in text) letters += wd.Text.Length;
		float lineW = letters * size * 0.92f + (text.Length - 1) * size * 0.42f;
		float lineX = view.X * 0.045f + 62f * u + lineW * 0.5f;

		// 纸条：我方 = 黑纸条垫红（选卡界面横幅那块），敌方 = 红纸条垫黑
		float w = (lineX + lineW * 0.5f + 36f * u) / 0.9f;
		Node2D strip = new() { Position = new Vector2(w * 0.42f, cy), Rotation = -0.04f };
		root.AddChild(strip);
		Texture2D? st = player ? Fx.Tex("reward_header_plate") : Fx.Tex("battle_strip_enemy");
		if (st != null)
		{
			strip.AddChild(new Sprite2D { Texture = st, Scale = Vector2.One * w / (st.GetWidth() * 0.9f) });
		}
		else
		{
			Color top = player ? Ink : Red, under = player ? Red : Ink;
			P5Ui.Slab(strip, -w * 0.5f + 8f, w * 0.5f + 8f, -26f * u, 40f * u, 18f * u, under);
			P5Ui.Slab(strip, -w * 0.5f, w * 0.5f, -40f * u, 30f * u, 18f * u, top);
		}

		// 第几回合的小纸牌（敌人回合是「!」）
		Vector2 tagAt = new(view.X * 0.045f, cy - 3f * u);
		Node2D tag = new() { Position = tagAt, Rotation = -0.1f };
		root.AddChild(tag);
		float tagH = 66f * u;
		if ((player ? Fx.Tex("battle_turn_tag") : Fx.Tex("battle_turn_tag_enemy")) is Texture2D tt)
		{
			tag.AddChild(new Sprite2D { Texture = tt, Scale = Vector2.One * tagH / (tt.GetHeight() * 0.7f) });   // 纸牌约占高 70%
		}
		else
		{
			Color bg = player ? Red : Ink;
			Fx.Poly(tag, new[] { new Vector2(-70f, -52f) * u + new Vector2(6f, 7f) * u, new Vector2(72f, -58f) * u + new Vector2(6f, 7f) * u, new Vector2(66f, 56f) * u + new Vector2(6f, 7f) * u, new Vector2(-74f, 52f) * u + new Vector2(6f, 7f) * u }, player ? Ink : Red);
			Fx.Poly(tag, new[] { new Vector2(-70f, -52f) * u, new Vector2(72f, -58f) * u, new Vector2(66f, 56f) * u, new Vector2(-74f, 52f) * u }, bg);
			Font f = P5Ui.Font(P5Ui.BarlowBold);
			P5Ui.Text(tag, player ? "TURN" : "ENEMY", f, (int)(18f * u), player ? Paper : Red, new Vector2(-62f, -54f) * u);
		}
		string num = player ? Math.Clamp(round, 0, 99).ToString() : "!";
		var digits = new List<(Node2D node, float rot)>();
		float ds = (num.Length > 1 ? 33f : 41f) * u;
		for (int i = 0; i < num.Length; i++)
		{
			string ch = num[i].ToString();
			string tex = ch == "!" ? "battle_HEART_6" : "battle_digit_" + ch;
			Vector2 at = new((i - (num.Length - 1) * 0.5f) * ds * 0.8f, 8f * u);
			float rot = Fx.Rand(-0.12f, 0.12f);
			Node2D d = Piece(tag, tex, ch, at, ds, i + 1, !player);
			d.Rotation = rot;
			d.Modulate = new Color(1f, 1f, 1f, 0f);
			digits.Add((d, rot));
		}

		Node2D words = new();
		root.AddChild(words);
		var pieces = Line(words, text, new Vector2(lineX, cy - 6f * u), size, !player, u);

		for (int i = 0; i < pieces.Count; i++) Slap(pieces[i].node, pieces[i].rot, 1f, 0.08f + 0.02f * i, 0.09f);
		for (int i = 0; i < digits.Count; i++) Slap(digits[i].node, digits[i].rot, 1f, 0.1f + 0.04f * i, 0.12f);

		if (extra && player)
		{
			Sfx.Take(Sfx.OneMore, 0.42f);
		}
		else
		{
			Sfx.Take("gx_whoosh", player ? 0.14f : 0.12f);
		}
		Sfx.Take("gx_card_stack", player ? 0.13f : 0.11f, 0.1f);
		if (!player) Sfx.Take("gx_slice", 0.1f, 0.04f);

		const float outAt = 0.86f, outLen = 0.24f;
		Fx.Animate(root, 0, TurnLength, t =>
		{
			float time = t * TurnLength;
			float a = Ease5.OutExpo(Ease5.Seg(time, 0f, 0.2f));
			float o = Ease5.Seg(time, outAt, outLen);
			float oe = o * o * o;
			float drift = 10f * u * Ease5.Seg(time, 0.2f, 0.66f);
			strip.Position = new Vector2(w * 0.42f - (w + 120f) * (1f - a) + drift - (w + 120f) * oe, cy);
			float tg = Ease5.OutBack(Ease5.Seg(time, 0.04f, 0.16f));
			tag.Scale = Vector2.One * Mathf.Max(0.001f, tg) * (1f - 0.4f * oe);
			tag.Rotation = -0.1f + 0.5f * (1f - Mathf.Min(1f, tg));
			tag.Position = tagAt + new Vector2(drift * 0.5f - (w + 120f) * oe, 0f);
			words.Position = new Vector2(drift * 1.4f - (w + 160f) * oe, 0f);
			root.Modulate = new Color(1f, 1f, 1f, 0.95f * (1f - Ease5.Seg(time, outAt + outLen * 0.5f, outLen * 0.5f)));
		});
	}
}
