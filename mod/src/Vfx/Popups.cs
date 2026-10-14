using System.Collections.Generic;
using Godot;

namespace P3Makoto.Vfx;

/// <summary>
/// P3R 原版风格的战斗弹字：伤害数字 和 "WEAK!"。
/// 样式（参照原版截图）：粗体斜体（字体 Bahnschrift 加粗窄体 + 斜切），近白 / 浅青色字面，蓝色描边，
/// 右下方一层深蓝"立体阴影"（多层偏移叠出厚度），周围点缀十字星光。
/// 动画：入场 —— 从 1.7 倍猛地缩回并略过冲、同时从左侧斜滑进来；停留 —— 轻微上浮；
///       出场 —— 横向压扁 + 向右上方加速飞出淡去。
/// </summary>
public static class Popups
{
	private static readonly Color Face = new("f2fbff");
	private static readonly Color WeakFace = new("9fe9ff");
	private static readonly Color Outline = new("1f5cff");
	private static readonly Color Extrude = new("0b2a8a");

	/// <summary>立体大字：深蓝阴影层 ×depth + 描边字面。返回字的节点（用于动画）。</summary>
	private static Node2D BlockText(Node2D parent, string text, int size, Color face, int depth)
	{
		Node2D node = new();
		parent.AddChild(node);
		for (int i = depth; i >= 1; i--)
		{
			Typo.Text(node, text, Typo.Latin, size, Extrude, new Vector2(i * 1.6f, i * 1.6f), outline: 6, outlineColor: Extrude);
		}
		Typo.Text(node, text, Typo.Latin, size, face, Vector2.Zero, outline: 5, outlineColor: Outline);
		return node;
	}

	private static void Sparkles(Control layer, Vector2 at, int count, float radius)
	{
		for (int i = 0; i < count; i++)
		{
			Vector2 p = at + new Vector2(Fx.Rand(-radius, radius), Fx.Rand(-radius * 0.6f, radius * 0.6f));
			Node2D st = Fx.Root(layer, p, "P3Sparkle");
			Fx.Poly(st, Fx.Star4(Fx.Rand(14f, 30f), 0.12f), Pal.White, additive: true);
			Sprite2D g = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(Pal.Cyan, 0.7f), Scale = Vector2.One * 0.9f };
			st.AddChild(g);
			float d = Fx.Rand(0f, 0.2f);
			Fx.Animate(st, 0.7f, t =>
			{
				float u = Mathf.Clamp((t * 0.7f - d) / 0.5f, 0f, 1f);
				st.Scale = Vector2.One * Mathf.Sin(u * Mathf.Pi);
				st.Rotation = u * 0.8f;
			});
			Fx.FreeAfter(st, 0.75);
		}
	}

	/// <summary>
	/// 入场 / 停留 / 出场 的通用动画：
	/// 入 —— 从 2.4 倍猛砸下来（0.1s），落地瞬间横向压扁再弹回（像盖章），同时抖动 0.14s；
	/// 停 —— 轻微上浮；
	/// 出 —— 先微微放大蓄一下，再纵向压扁、加速飞向右上方淡出。
	/// </summary>
	private static void Animate(Node2D root, Vector2 home, float hold, float tilt)
	{
		root.Rotation = tilt;
		root.Skew = -0.28f;                                  // 斜体
		const float tIn = 0.1f, tLand = 0.14f, tOut = 0.24f;
		float total = tIn + tLand + hold + tOut;
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			float slam = Mathf.Clamp(time / tIn, 0f, 1f);
			float land = Mathf.Clamp((time - tIn) / tLand, 0f, 1f);
			float exit = Mathf.Clamp((time - tIn - tLand - hold) / tOut, 0f, 1f);
			float s = Mathf.Lerp(2.4f, 1f, Ease.InCubic(slam));
			// 落地压扁：x 1.3 / y 0.72 → 弹回（阻尼振荡）
			float squash = land <= 0f ? 0f : Mathf.Exp(-6f * land) * Mathf.Cos(land * 14f);
			float sx = s * (1f + 0.3f * squash), sy = s * (1f - 0.28f * squash);
			float pre = Mathf.Sin(Mathf.Clamp(exit / 0.3f, 0f, 1f) * Mathf.Pi) * 0.12f;          // 出场前蓄一下
			float go = Ease.InCubic(Mathf.Clamp((exit - 0.2f) / 0.8f, 0f, 1f));
			root.Scale = new Vector2(sx * (1f + pre + 0.5f * go), sy * (1f + pre - 0.85f * go));
			Vector2 jitter = land > 0f && land < 1f ? new Vector2(Fx.Rand(-1f, 1f), Fx.Rand(-1f, 1f)) * 9f * (1f - land) : Vector2.Zero;
			root.GlobalPosition = home + jitter + new Vector2(-80f * (1f - Ease.OutExpo(slam)) + 220f * go,
				-60f * (1f - Ease.OutExpo(slam)) - 16f * Mathf.Clamp((time - tIn) / (tLand + hold), 0f, 1f) - 90f * go);
			root.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(time / 0.03f, 0f, 1f) * (1f - go));
		});
		Fx.FreeAfter(root, total + 0.05);
	}

	/// <summary>落地冲击：白色闪字（盖在字上 0.1s 淡掉）+ 细光环 + 十字星爆 + 三道向后拖的残影。</summary>
	private static void LandImpact(Control layer, Node2D root, Node2D text, Vector2 home, string label, int size, float strength)
	{
		Node2D white = new() { Position = text.Position };
		root.AddChild(white);
		Typo.Text(white, label, Typo.Latin, size, Pal.White, Vector2.Zero, outline: 8, outlineColor: Pal.White);
		Fx.Animate(white, 0.2f, t => white.Modulate = new Color(1f, 1f, 1f, t < 0.45f ? 1f : 1f - Ease.OutCubic((t - 0.45f) / 0.55f)));
		Fx.FreeAfter(white, 0.22);
		// 残影：砸下来的轨迹上留三道淡蓝描边字
		for (int i = 1; i <= 3; i++)
		{
			Node2D ghost = Fx.Root(layer, home + new Vector2(-26f * i, -20f * i), "P3DmgGhost");
			ghost.Rotation = root.Rotation;
			ghost.Skew = -0.28f;
			ghost.Scale = Vector2.One * (1f + 0.35f * i);
			Typo.Text(ghost, label, Typo.Latin, size, new Color(Pal.Cyan, 0.0f), text.Position, outline: 4, outlineColor: new Color(Pal.Cyan, 0.5f - 0.12f * i));
			Fx.Animate(ghost, 0.16f, t => ghost.Modulate = new Color(1f, 1f, 1f, 1f - t));
			Fx.FreeAfter(ghost, 0.18);
		}
		Fx.After(layer, 0.1, "dmg land", () =>
		{
			P3Fx.Shockwave(layer, home, Pal.Cyan, 320f * strength, 0.35f);
			P3Fx.Flash(layer, home, Pal.Ice, 260f * strength, 0.14f);
			Node2D star = Fx.Root(layer, home, "P3DmgStar");
			Fx.Poly(star, Fx.Star4(150f * strength, 0.06f), Pal.White, additive: true);
			Fx.Animate(star, 0.2f, t => { star.Scale = new Vector2(1f + t, 1f - 0.9f * t); star.Modulate = new Color(1f, 1f, 1f, 1f - t); });
			Fx.FreeAfter(star, 0.22);
			Sparkles(layer, home, 4, 110f * strength);
		});
	}

	// 结城理的牌刚刚打中的目标（伤害数字由 NDamageNumVfx.Create 生成时据此替换成 P3R 样式）
	private static readonly Dictionary<MegaCrit.Sts2.Core.Entities.Creatures.Creature, ulong> Marked = new();

	public static void MarkHits(IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> targets)
	{
		ulong now = Time.GetTicksMsec();
		foreach (var c in targets)
		{
			Marked[c] = now;
		}
	}

	/// <summary>这个目标最近 1.5 秒内是否被结城理的牌打中（多段 / 延迟结算都在窗口内）。</summary>
	public static bool IsMakotoHit(MegaCrit.Sts2.Core.Entities.Creatures.Creature target) =>
		Marked.TryGetValue(target, out ulong t) && Time.GetTicksMsec() - t < 1500;

	// 同一目标短时间内多段伤害：依次落在目标周围的几个固定位置上（循环使用），不叠在一起，也不会越错越远
	private static readonly Dictionary<ulong, (ulong Time, int Count)> Stack = new();
	private static readonly Vector2[] Slots = { new(0f, 0f), new(-95f, 55f), new(85f, 70f), new(-40f, -75f), new(110f, -40f), new(10f, 120f) };

	/// <summary>伤害数字（在目标旁边）。字号随伤害增大：&lt;10 → 112，≥30 → 150。</summary>
	public static void Damage(Control layer, ulong targetId, Vector2 at, int amount)
	{
		ulong now = Time.GetTicksMsec();
		int k = Stack.TryGetValue(targetId, out var st) && now - st.Time < 600 ? st.Count + 1 : 0;
		Stack[targetId] = (now, k);
		Vector2 home = at + new Vector2(80f, -80f) + Slots[k % Slots.Length];
		Node2D root = Fx.Root(layer, home, "P3DamageNum");
		int size = amount >= 30 ? 150 : amount >= 10 ? 128 : 112;
		string label = amount.ToString();
		Node2D text = BlockText(root, label, size, Face, 8);
		text.Position = new Vector2(-size * 0.3f * label.Length, -size * 0.7f);
		Animate(root, home, 0.5f, Fx.Rand(-0.12f, -0.04f));
		LandImpact(layer, root, text, home, label, size, amount >= 30 ? 1.3f : 1f);
	}

	/// <summary>"WEAK!"（施加减益时）。</summary>
	public static void Weak(Control layer, Vector2 at)
	{
		Vector2 home = at + new Vector2(-90f, -150f);
		Node2D root = Fx.Root(layer, home, "P3Weak");
		Audio.Sfx.Play(Audio.Snd.Down, 1.0f, delay: 0.09f);       // 对准落地那一刻
		Node2D text = BlockText(root, "WEAK!", 140, WeakFace, 9);
		text.Position = new Vector2(-180f, -84f);
		Animate(root, home, 0.55f, -0.17f);
		LandImpact(layer, root, text, home, "WEAK!", 140, 1.4f);
		Sparkles(layer, home, 5, 170f);
		P3Fx.Flash(layer, home, Pal.Cyan, 260f, 0.2f);
	}

	private static readonly Dictionary<ulong, ulong> LastCrit = new();

	/// <summary>
	/// 「CRITICAL!」（v0.14.12，原版贴图 T_UI_Battle_06：白 → 青渐变斜体 + 深蓝立体边，预合成为 ui/critical.png）。
	/// 结城理一击造成的实际伤害 ≥ 20 且 ≥ 目标最大生命 30% 时弹出（杀戮尖塔没有暴击，用"打得很重"代替）；同一目标 0.8s 内只弹一次。
	/// 原版节奏：字从大到小砸进来（0.12s，白闪），同时一圈放射速度线；停留约 0.6s 缓慢向右上漂，淡出。
	/// </summary>
	public static void Critical(Control layer, ulong targetId, Vector2 at)
	{
		ulong now = Time.GetTicksMsec();
		if (LastCrit.TryGetValue(targetId, out ulong last) && now - last < 800)
		{
			return;
		}
		LastCrit[targetId] = now;
		if (!ResourceLoader.Exists("res://p3makoto/ui/critical.png"))
		{
			return;
		}
		Texture2D tex = ResourceLoader.Load<Texture2D>("res://p3makoto/ui/critical.png");
		Vector2 home = at + new Vector2(40f, -215f);
		Node2D root = Fx.Root(layer, home, "P3Critical");
		root.Rotation = -0.12f;
		float sc = 0.62f;
		Sprite2D word = new() { Texture = tex, Scale = Vector2.One * sc };
		Sprite2D flash = new() { Texture = tex, Scale = Vector2.One * sc, Material = Fx.Additive };
		root.AddChild(word);
		root.AddChild(flash);
		Audio.Sfx.Play(Audio.Snd.Down, 1.0f, delay: 0.05f);
		P3Fx.SpeedLines(layer, home, Pal.Ice, 18, 120f, 420f, 0.35f);
		P3Fx.Flash(layer, home, Pal.Cyan, 320f, 0.2f);
		const float total = 1.0f;
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			float slam = Ease.OutBack(Mathf.Clamp(time / 0.12f, 0f, 1f));
			root.Scale = Vector2.One * Mathf.Lerp(1.8f, 1f, slam);
			flash.Modulate = new Color(1f, 1f, 1f, Mathf.Max(0f, 1f - time / 0.2f));
			root.Position = home + new Vector2(30f, -18f) * Mathf.Clamp((time - 0.12f) / 0.8f, 0f, 1f);
			root.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(time / 0.04f, 0f, 1f) * (1f - Ease.InCubic(Mathf.Clamp((time - 0.78f) / 0.22f, 0f, 1f))));
		});
		Fx.FreeAfter(root, total + 0.05f);
	}

	/// <summary>
	/// P3R「BLOCK」（v0.14.0，逐帧对照 research/p3r_video/NOTES.md 第 4 节）：
	/// · 字先出现（冲击前一拍），白色粗斜体、每个字母各自轻微歪斜 / 错位，带细黑描边；字后面一条黑色斜长条做底，
	///   但长条比字矮，字母上下都露出长条之外（不是把字框在里面）；
	/// · 冲击：半透明白色护盾泡猛地胀开（带几道弧形流光）→ 一圈细长的放射火花（白芯 → 黄 → 橙粉，越往外越细越暗）+ 中心黄光；
	/// · 余韵：人物身后一团红橙色柔光慢慢褪去，地上几片青色碎片，几片黄绿色小纸屑飘落；字停留约 0.7s 缓慢漂移后淡出。
	/// </summary>
	public static void Block(Control layer, Vector2 center, float s = 1f)
	{
		Vector2 impact = center + new Vector2(50f, -10f) * s;              // 冲击点：身前（敌人在右边）
		Vector2 home = center + new Vector2(62f, -62f) * s;                // 字：胸口偏上、稍微靠前
		BlockWord(layer, home, s);
		Fx.After(layer, 0.05, "block impact", () => BlockImpact(layer, impact, s));
	}

	private static readonly Color BlockInk = new(0.04f, 0.04f, 0.07f);

	private static void BlockWord(Control layer, Vector2 home, float s)
	{
		Node2D root = Fx.Root(layer, home, "P3Block");
		root.Rotation = -0.21f;
		const string word = "BLOCK";
		int fs = (int)(52 * s);
		float adv = fs * 0.5f, x0 = -adv * word.Length / 2f;
		// 黑色长条底：比字矮（约字高的 6 成）、稍微偏下偏左，字母的上沿和下沿都露出长条之外 —— 字没有被"框"在里面
		float barW = adv * word.Length + fs * 0.9f, barH = fs * 0.6f, lean = fs * 0.12f;      // 右端比 K 多伸出一截（原版如此）
		Vector2 bc = new(fs * 0.18f, fs * 0.12f);
		Fx.Poly(root, new[]
		{
			bc + new Vector2(-barW / 2f + lean, -barH / 2f), bc + new Vector2(barW / 2f + lean, -barH / 2f),
			bc + new Vector2(barW / 2f - lean, barH / 2f), bc + new Vector2(-barW / 2f - lean, barH / 2f),
		}, new Color(BlockInk, 0.95f));
		// 字面：白色，细黑描边，每个字母轻微歪斜、上下错位
		for (int i = 0; i < word.Length; i++)
		{
			Vector2 p = new(x0 + adv * i, -fs * 0.62f + Fx.Rand(-3f, 3f) * s);
			Label l = Typo.Text(root, word[i].ToString(), Typo.LatinItalic, fs, new Color(0.97f, 0.97f, 1f), p, outline: (int)(2 * s), outlineColor: BlockInk);
			l.PivotOffset = new Vector2(adv * 0.5f, fs * 0.6f);
			l.Rotation = Fx.Rand(-0.07f, 0.07f);
		}
		const float total = 1.0f;
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			float pop = Ease.OutBack(Mathf.Clamp(time / 0.1f, 0f, 1f));
			float kick = time > 0.05f ? Mathf.Exp(-(time - 0.05f) * 18f) * 0.12f : 0f;   // 冲击那一下被顶得一颤
			root.Scale = Vector2.One * (Mathf.Lerp(1.5f, 1f, pop) + kick);
			root.Position = home + new Vector2(10f * Ease.OutCubic(Mathf.Clamp(time / total, 0f, 1f)), -16f * Ease.OutCubic(Mathf.Clamp((time - 0.1f) / 0.9f, 0f, 1f))) * s;
			root.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(time / 0.04f, 0f, 1f) * (1f - Ease.InCubic(Mathf.Clamp((time - 0.78f) / 0.22f, 0f, 1f))));
		});
		Fx.FreeAfter(root, total + 0.05);
	}

	private static void BlockImpact(Control layer, Vector2 at, float s)
	{
		// 护盾泡：柔光圆 + 亮边 + 三道弧形流光，0.12s 胀开后淡出
		Node2D bubble = Fx.Root(layer, at, "P3BlockBubble");
		Sprite2D fill = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(0.85f, 0.92f, 1f, 0.55f), Scale = Vector2.One * 2.4f * s };
		bubble.AddChild(fill);
		Fx.StrokeClosed(bubble, Fx.Circle(78f * s, 48), new Color(1f, 1f, 1f, 0.85f), 4f * s);
		Fx.StrokeClosed(bubble, Fx.Circle(70f * s, 48), new Color(0.8f, 0.9f, 1f, 0.35f), 7f * s);
		for (int k = 0; k < 3; k++)
		{
			float a0 = Fx.Rand(0f, Mathf.Tau), r = Fx.Rand(48f, 66f) * s;
			var arc = new Vector2[10];
			for (int i = 0; i < 10; i++) arc[i] = Vector2.Right.Rotated(a0 + i * 0.13f) * r;
			Fx.Stroke(bubble, arc, new Color(1f, 1f, 1f, 0.8f), 3f * s);
		}
		Fx.Animate(bubble, 0.26f, t =>
		{
			bubble.Scale = Vector2.One * (0.55f + 0.6f * Ease.OutExpo(Mathf.Clamp(t / 0.45f, 0f, 1f)));
			bubble.Rotation = t * 0.6f;
			bubble.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(t));
		});
		Fx.FreeAfter(bubble, 0.28);

		// 放射火花：每道由三段拼成（白芯 → 黄 → 橙粉），外围再叠一层宽而淡的光晕；长短不一、稍有先后
		Fx.After(layer, 0.06, "block sparks", () =>
		{
			Node2D rays = Fx.Root(layer, at, "P3BlockSparks");
			var sparks = new System.Collections.Generic.List<(Polygon2D[] P, Vector2 D, float L, float Delay)>();
			Color[] cols = { new(1f, 1f, 0.95f, 0.95f), new(1f, 0.85f, 0.35f, 0.9f), new(1f, 0.45f, 0.45f, 0.75f) };
			for (int i = 0; i < 16; i++)
			{
				Vector2 d = Vector2.Right.Rotated(Mathf.Tau * i / 16f + Fx.Rand(-0.15f, 0.15f));
				float len = (i % 4 == 0 ? Fx.Rand(220f, 320f) : Fx.Rand(90f, 190f)) * s;
				var polys = new Polygon2D[4];
				polys[0] = Fx.Poly(rays, new Vector2[3], new Color(1f, 0.6f, 0.3f, 0.25f), additive: true);   // 光晕
				for (int k = 0; k < 3; k++) polys[k + 1] = Fx.Poly(rays, new Vector2[3], cols[k], additive: true);
				sparks.Add((polys, d, len, Fx.Rand(0f, 0.05f)));
			}
			Fx.Animate(rays, 0.34f, t =>
			{
				float time = t * 0.34f;
				foreach (var (polys, d, len, delay) in sparks)
				{
					float u = Mathf.Clamp((time - delay) / 0.26f, 0f, 1f);
					float head = 18f * s + len * Ease.OutExpo(u), tail = 18f * s + len * Ease.InCubic(u) * 0.95f;
					float span = Mathf.Max(head - tail, 0.5f), w = 3.4f * s * (1f - 0.6f * u);
					Vector2 a = d * tail, b = d * (tail + span * 0.45f), c = d * (tail + span * 0.8f), e = d * head;
					Fx.Reshape(polys[0], Fx.StrokePts(new[] { a, e }, w * 4f, taper: true));
					Fx.Reshape(polys[3], Fx.StrokePts(new[] { a, b }, w * 0.7f, taper: true));
					Fx.Reshape(polys[2], Fx.StrokePts(new[] { b, c }, w, taper: false));
					Fx.Reshape(polys[1], Fx.StrokePts(new[] { c, e }, w * 1.2f, taper: true));
				}
			});
			Fx.FreeAfter(rays, 0.36);
			Sprite2D core = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(1f, 0.85f, 0.35f, 0.9f), Scale = Vector2.One * 2.6f * s };
			Node2D coreRoot = Fx.Root(layer, at, "P3BlockCore");
			coreRoot.AddChild(core);
			Fx.Animate(coreRoot, 0.25f, t => coreRoot.Modulate = new Color(1f, 1f, 1f, 1f - t));
			Fx.FreeAfter(coreRoot, 0.27);
		});

		// 余韵：身后红橙柔光（0.7s 褪去）、地上青色碎片、黄绿小纸屑飘落
		Node2D after = Fx.Root(layer, at + new Vector2(-30f, 10f) * s, "P3BlockAfterglow");
		Sprite2D warm = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(1f, 0.32f, 0.22f, 0f), Scale = new Vector2(3.2f, 4.2f) * s };
		after.AddChild(warm);
		Fx.Animate(after, 0.8f, t => warm.Modulate = new Color(1f, 0.32f, 0.22f, 0.5f * Mathf.Sin(Mathf.Min(t * 4f, 1f) * Mathf.Pi * 0.5f) * (1f - t)));
		Fx.FreeAfter(after, 0.82);
		Fx.After(layer, 0.08, "block shards", () => P3Fx.GlassShatter(layer, at + new Vector2(0f, 60f) * s, 8, 0.45f, new Color(0.45f, 0.95f, 0.85f)));
		for (int i = 0; i < 7; i++)
		{
			Node2D leaf = Fx.Root(layer, at + new Vector2(Fx.Rand(-110f, 90f), Fx.Rand(-120f, -40f)) * s, "P3BlockConfetti");
			Fx.Poly(leaf, new[] { new Vector2(-5f, 0f), new Vector2(0f, -2.5f), new Vector2(5f, 0f), new Vector2(0f, 2.5f) }, i % 2 == 0 ? new Color(0.85f, 0.95f, 0.35f) : new Color(1f, 0.85f, 0.3f));
			float spin = Fx.Rand(4f, 9f), sway = Fx.Rand(0.5f, 1.2f), delay = Fx.Rand(0.05f, 0.2f);
			Vector2 from = leaf.Position;
			leaf.Modulate = new Color(1f, 1f, 1f, 0f);
			Fx.Animate(leaf, 0.9f, t =>
			{
				float time = t * 0.9f - delay;
				if (time < 0f) return;
				leaf.Position = from + new Vector2(18f * Mathf.Sin(time * 6f * sway), 70f * time) * s;
				leaf.Rotation = time * spin;
				leaf.Scale = new Vector2(Mathf.Cos(time * spin * 1.3f), 1f) * s;
				leaf.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(time / 0.05f, 0f, 1f) * (1f - Mathf.Clamp((time - 0.5f) / 0.2f, 0f, 1f)));
			});
			Fx.FreeAfter(leaf, 0.95);
		}
	}
}
