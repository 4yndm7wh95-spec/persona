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

	// 同一目标短时间内多段伤害：后一个数字往右下错开，不叠在一起
	private static readonly Dictionary<ulong, (ulong Time, int Count)> Stack = new();

	/// <summary>伤害数字（在目标旁边）。字号随伤害增大：&lt;10 → 112，≥30 → 150。</summary>
	public static void Damage(Control layer, ulong targetId, Vector2 at, int amount)
	{
		ulong now = Time.GetTicksMsec();
		int k = Stack.TryGetValue(targetId, out var st) && now - st.Time < 600 ? st.Count + 1 : 0;
		Stack[targetId] = (now, k);
		Vector2 home = at + new Vector2(80f + 40f * k, -80f + 36f * k);
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
		Audio.Sfx.Play(Audio.Snd.Weak, 1.0f, delay: 0.09f);       // 对准落地那一刻
		Node2D text = BlockText(root, "WEAK!", 140, WeakFace, 9);
		text.Position = new Vector2(-180f, -84f);
		Animate(root, home, 0.55f, -0.17f);
		LandImpact(layer, root, text, home, "WEAK!", 140, 1.4f);
		Sparkles(layer, home, 5, 170f);
		P3Fx.Flash(layer, home, Pal.Cyan, 260f, 0.2f);
	}
}
