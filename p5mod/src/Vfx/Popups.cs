using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// P5R 风格的战斗弹字：伤害数字、BLOCK。结构照搬 P3 mod 的 Popups（mod/src/Vfx/Popups.cs）：
/// 猛砸入场 → 落地压扁回弹 + 抖动 → 停留微漂 → 干脆地出场；多段伤害落在目标周围几个固定槽位上。
/// 样式用 P5R 原版 DAMAGE_DATA.SPD 的切图：
///   伤害 —— 白描边黑字一个个盖上去，带红色错位阴影（枪击是黑色），沿右上斜线排开、每个字歪一点（不要心形对话泡，用户 2026-10-28）；
///   BLOCK —— 五张白底黑字的字母卡片排在一条线上，一张接一张拍在红色斜条上，只在 Joker 被完全格挡时出现，小小一个。
/// </summary>
public static class Popups
{
	public static readonly Color Red = new("e5191c");
	public static readonly Color Ink = new("0d0d0f");

	private static readonly Dictionary<ulong, (ulong Time, int Count)> Stack = new();
	// 槽位只往上 / 往右排（往左下就压到敌人身上了）
	private static readonly Vector2[] Slots = { new(0f, 0f), new(145f, 75f), new(-70f, -125f), new(215f, -55f), new(70f, -220f), new(285f, 105f) };

	/// <summary>伤害数字。at = 目标的 VfxSpawnPosition；字高随伤害增大：&lt;10 → 130，≥30 → 170。</summary>
	public static void Damage(Control layer, ulong targetId, Vector2 at, int amount, bool gun)
	{
		ulong now = Stage.NowMs();
		int k = Stack.TryGetValue(targetId, out var st) && now - st.Time < 600 ? st.Count + 1 : 0;
		Stack[targetId] = (now, k);
		Vector2 home = at + new Vector2(110f, -200f) + Slots[k % Slots.Length];       // 敌人头顶偏右：别挡住敌人
		string label = amount.ToString();
		float h = amount >= 30 ? 170f : amount >= 10 ? 150f : 130f;
		const float total = 1.0f;
		Node2D root = Fx.Root(layer, home, "P5DamageNum", total + 0.05);

		// 数字排布：沿右上斜线，相邻叠一点，单双交替歪向两边、大小交替
		float step = h * 0.4f;
		var digits = new List<(Node2D Node, Vector2 Pos, float Rot, float Scale)>();
		Vector2 center = new(step * (label.Length - 1) * 0.5f, -h * 0.07f * (label.Length - 1));
		for (int i = 0; i < label.Length; i++)
		{
			Vector2 pos = new Vector2(step * i, -h * 0.14f * i + (i % 2 == 0 ? 4f : -6f)) - center;
			float rot = (i % 2 == 0 ? -0.13f : 0.07f) + Fx.Rand(-0.04f, 0.04f);
			float sc = (i % 2 == 0 ? 1f : 0.9f) * Fx.Rand(0.96f, 1.04f);
			digits.Add((new Node2D(), pos, rot, sc));
		}

		// 每个数字：错位阴影（红，枪击是黑）→ 字 → 白闪（落地那一下盖一层，迅速褪掉）
		var flashes = new List<Sprite2D>();
		for (int i = 0; i < digits.Count; i++)
		{
			root.AddChild(digits[i].Node);
			if (Fx.Tex("dmg_" + label[i]) is not Texture2D tex)
			{
				continue;
			}
			float ks = h / 371f;
			Sprite2D shadow = Fx.Sprite(digits[i].Node, tex, additive: false);
			shadow.Scale = Vector2.One * ks;
			shadow.Position = new Vector2(7f, 6f);
			shadow.Modulate = gun ? Ink : Red;
			Fx.Sprite(digits[i].Node, tex, additive: false).Scale = Vector2.One * ks;
			Sprite2D flash = Fx.Sprite(digits[i].Node, tex, additive: true);
			flash.Scale = Vector2.One * ks;
			flashes.Add(flash);
		}

		float tilt = Fx.Rand(-0.08f, 0.0f);
		Fx.Animate(root, 0, total, t =>
		{
			float time = t * total;
			// 数字：一个接一个盖上去（每个晚 0.035s），1.5 倍 → 1，落地压扁回弹
			for (int i = 0; i < digits.Count; i++)
			{
				var d = digits[i];
				float t0 = 0.02f + 0.035f * i;
				float u = Mathf.Clamp((time - t0) / 0.07f, 0f, 1f);
				float v = Mathf.Clamp((time - t0 - 0.07f) / 0.18f, 0f, 1f);
				float sq = v <= 0f ? 0f : Mathf.Exp(-6f * v) * Mathf.Cos(v * 15f);
				float s = d.Scale * Mathf.Lerp(1.5f, 1f, Fx.InCubic(u));
				d.Node.Visible = time >= t0;
				d.Node.Scale = new Vector2(s * (1f + 0.25f * sq), s * (1f - 0.22f * sq));
				d.Node.Rotation = d.Rot - 0.35f * (1f - Fx.OutCubic(u)) * (i % 2 == 0 ? 1f : -1f);
				Vector2 jitter = v > 0f && v < 0.6f ? new Vector2(Fx.Rand(-1f, 1f), Fx.Rand(-1f, 1f)) * 5f * (1f - v / 0.6f) : Vector2.Zero;
				d.Node.Position = d.Pos + jitter;
				if (i < flashes.Count)
				{
					flashes[i].Modulate = new Color(1f, 1f, 1f, u < 1f ? 0f : Mathf.Clamp(1f - v / 0.5f, 0f, 1f));
				}
			}
			// 整体：停留时往右上漂一点；最后 0.16s 先一挺、再横向拉长纵向压扁甩出去
			float exit = Mathf.Clamp((time - 0.8f) / 0.16f, 0f, 1f);
			float pre = Mathf.Sin(Mathf.Clamp(exit / 0.35f, 0f, 1f) * Mathf.Pi) * 0.08f;
			float go = Fx.InCubic(Mathf.Clamp((exit - 0.25f) / 0.75f, 0f, 1f));
			float drift = Fx.OutCubic(Mathf.Clamp((time - 0.15f) / 0.65f, 0f, 1f));
			root.Rotation = tilt;
			root.Scale = new Vector2(1f + pre + 0.6f * go, 1f + pre - 0.95f * go);
			root.GlobalPosition = home + new Vector2(10f * drift + 60f * go, -12f * drift - 10f * go);
			root.Modulate = new Color(1f, 1f, 1f, 1f - go);
		});
	}

	private static ulong _lastBlock;
	private static Node2D? _lastWord;

	/// <summary>
	/// 「BLOCK」：Joker 的格挡完全挡住一次攻击时（原版 NDamageBlockedVfx 那一刻）。chest = Joker 胸口，facing = 朝向（敌人在哪边）。
	/// 和 P3 一样：字在胸口偏上、稍微靠前，冲击点在身前；连续格挡时旧字立刻淡掉，冲击用轻量版。
	/// </summary>
	public static void Block(Control layer, Vector2 chest, float facing = 1f)
	{
		ulong now = Stage.NowMs();
		bool chain = now - _lastBlock < 700;
		_lastBlock = now;
		if (_lastWord is Node2D old && GodotObject.IsInstanceValid(old))
		{
			Fx.Animate(old, 0, 0.08, t => old.Modulate = new Color(1f, 1f, 1f, old.Modulate.A * (1f - t)));
			Fx.FreeAfter(old, 0.1);
		}
		_lastWord = BlockWord(layer, chest + new Vector2(120f * facing, -70f));       // 胸口偏上、身前（同 P3），不挡脸也不盖住胸口
		Vector2 impact = chest + new Vector2(70f * facing, 10f);
		Fx.After(layer, 0.05, () => BlockImpact(layer, impact, facing, chain));
	}

	private static Node2D BlockWord(Control layer, Vector2 home)
	{
		const float total = 1.0f;
		Node2D root = Fx.Root(layer, home, "P5Block", total + 0.05);
		root.Rotation = -0.16f;
		const float cardH = 54f, step = 30f;
		float scale = cardH / 354f;
		string[] names = { "block_b", "block_l", "block_o", "block_c", "block_k" };
		float x0 = -step * (names.Length - 1) / 2f;

		// 红色斜条：和卡片同一条中线，左右各多出一点，比卡片矮（卡片上下都露在条外面）
		Node2D bar = new();
		root.AddChild(bar);
		float barW = step * names.Length + 14f, barH = cardH * 0.5f;
		Fx.Poly(bar, Fx.Slant(barW, barH, 10f), Red).Position = new Vector2(-barW / 2f - 5f, barH / 2f);

		var cards = new List<(Node2D Node, Vector2 Pos, float Rot, float Scale)>();
		for (int i = 0; i < names.Length; i++)
		{
			Node2D n = new();
			root.AddChild(n);
			if (Fx.Tex(names[i]) is Texture2D tex)
			{
				Fx.Sprite(n, tex, additive: false).Scale = Vector2.One * scale;
			}
			cards.Add((n, new Vector2(x0 + step * i, 0f), i % 2 == 0 ? -0.05f : 0.05f, 1f));      // 排齐：同一基线、同样大小，只交替微歪
		}

		Fx.Animate(root, 0, total, t =>
		{
			float time = t * total;
			// 条跟着卡片一起往右长：K 落下时刚好长满（不会先空出一大截）
			float barIn = Fx.OutCubic(Mathf.Clamp(time / (0.02f + 0.03f * (cards.Count - 1) + 0.06f), 0f, 1f));
			bar.Scale = new Vector2(barIn, 1f);
			bar.Position = new Vector2(-barW * 0.5f * (1f - barIn), 0f);
			for (int i = 0; i < cards.Count; i++)
			{
				var c = cards[i];
				float t0 = 0.02f + 0.03f * i;
				float u = Mathf.Clamp((time - t0) / 0.06f, 0f, 1f);
				float v = Mathf.Clamp((time - t0 - 0.06f) / 0.16f, 0f, 1f);
				float sq = v <= 0f ? 0f : Mathf.Exp(-6f * v) * Mathf.Cos(v * 15f);
				float s = c.Scale * Mathf.Lerp(1.5f, 1f, Fx.InCubic(u));
				c.Node.Visible = time >= t0;
				c.Node.Scale = new Vector2(s * (1f + 0.2f * sq), s * (1f - 0.18f * sq));
				c.Node.Rotation = c.Rot + 0.25f * (1f - Fx.OutCubic(u)) * (i % 2 == 0 ? -1f : 1f);
				c.Node.Position = c.Pos;
			}
			// 冲击那一下被顶得一颤，然后慢慢往右上漂；最后 0.2s 压扁淡出
			float kick = time > 0.06f ? Mathf.Exp(-(time - 0.06f) * 18f) * 0.1f : 0f;
			float exit = Mathf.Clamp((time - 0.8f) / 0.2f, 0f, 1f);
			float go = Fx.InCubic(exit);
			root.Scale = new Vector2(1f + kick + 0.3f * go, 1f + kick - 0.9f * go);
			float drift = Fx.OutCubic(Mathf.Clamp((time - 0.1f) / 0.9f, 0f, 1f));
			root.GlobalPosition = home + new Vector2(8f * drift, -14f * drift);
			root.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(time / 0.03f, 0f, 1f) * (1f - go));
		});
		return root;
	}

	/// <summary>
	/// 格挡冲击：身前一道朝敌人的白色月牙（像刀背格开一击），后面晚一拍跟一道红月牙，第一下再崩出三片碎块。
	/// 都很小，不挡人；不用放射火花（不要刺猬球）。
	/// </summary>
	private static void BlockImpact(Control layer, Vector2 at, float facing, bool light)
	{
		Node2D root = Fx.Root(layer, at, "P5BlockImpact", 0.4);
		root.Scale = new Vector2(facing, 1f);
		Polygon2D red = Fx.Poly(root, Crescent(62f, 1f), Red);
		Polygon2D white = Fx.Poly(root, Crescent(58f, 1f), Colors.White);
		Fx.Animate(root, 0, 0.26, t =>
		{
			// 白：0.05s 张开到最厚，然后越来越薄、往前推一点；红：晚 0.04s，稍大，跟着褪
			float open = Fx.OutCubic(Mathf.Clamp(t / 0.2f, 0f, 1f));
			float thin = Mathf.Clamp((t - 0.2f) / 0.8f, 0f, 1f);
			white.Polygon = Crescent(58f * (0.7f + 0.3f * open), (1f - thin) * open);
			white.Position = new Vector2(14f * Fx.OutCubic(t), 0f);
			float r = Mathf.Clamp((t - 0.15f) / 0.85f, 0f, 1f);
			red.Visible = r > 0f;
			red.Polygon = Crescent(66f * (0.8f + 0.25f * Fx.OutCubic(r)), 0.8f * (1f - r));
			red.Position = new Vector2(-4f + 22f * Fx.OutCubic(r), 0f);
		});
		if (light)
		{
			return;
		}
		for (int i = 0; i < 3; i++)
		{
			Node2D shard = Fx.Root(layer, at, "P5BlockShard", 0.4);
			float sz = Fx.Rand(8f, 13f);
			Fx.Poly(shard, new[] { new Vector2(-sz, -sz * 0.4f), new Vector2(sz, 0f), new Vector2(-sz * 0.5f, sz * 0.6f) }, i == 1 ? Red : Colors.White);
			Vector2 dir = new Vector2(facing, 0f).Rotated((i - 1) * 0.7f + Fx.Rand(-0.25f, 0.25f));
			float dist = Fx.Rand(55f, 85f), spin = Fx.Rand(-9f, 9f);
			Vector2 from = at + new Vector2(50f * facing, 0f);
			Fx.Animate(shard, 0.03, 0.3, t =>
			{
				shard.GlobalPosition = from + dir * dist * Fx.OutCubic(t) + new Vector2(0f, 40f * t * t);
				shard.Rotation = spin * t;
				shard.Modulate = new Color(1f, 1f, 1f, 1f - Fx.InCubic(t));
			});
		}
	}

	/// <summary>朝右的月牙：外弧半径 r、上下张角约 ±65°，中间最厚 = 0.28r × thick，两头收尖。</summary>
	private static Vector2[] Crescent(float r, float thick)
	{
		const int n = 14;
		var pts = new Vector2[n * 2];
		for (int i = 0; i < n; i++)
		{
			float u = (float)i / (n - 1), a = Mathf.Lerp(-1.15f, 1.15f, u);
			float w = r * 0.28f * Mathf.Max(thick, 0.01f) * Mathf.Sin(u * Mathf.Pi);
			pts[i] = Vector2.Right.Rotated(a) * r;
			pts[n * 2 - 1 - i] = Vector2.Right.Rotated(a) * (r - w) - new Vector2(w * 0.4f, 0f);
		}
		return pts;
	}

}
