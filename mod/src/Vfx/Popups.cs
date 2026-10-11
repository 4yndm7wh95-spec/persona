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

	/// <summary>入场 / 停留 / 出场 的通用动画。</summary>
	private static void Animate(Node2D root, Vector2 home, float hold, float tilt)
	{
		root.Rotation = tilt;
		root.Skew = -0.28f;                                  // 斜体
		float total = 0.14f + hold + 0.22f;
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			float slam = Mathf.Clamp(time / 0.14f, 0f, 1f);
			float exit = Mathf.Clamp((time - 0.14f - hold) / 0.22f, 0f, 1f);
			float s = Mathf.Lerp(1.7f, 1f, Ease.OutBack(slam));
			root.Scale = new Vector2(s * (1f + 0.4f * Ease.InCubic(exit)), s * (1f - 0.75f * Ease.InCubic(exit)));
			root.GlobalPosition = home + new Vector2(-60f * (1f - Ease.OutExpo(slam)) + 180f * Ease.InCubic(exit),
				-14f * Mathf.Clamp((time - 0.14f) / hold, 0f, 1f) - 60f * Ease.InCubic(exit));
			root.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(time / 0.04f, 0f, 1f) * (1f - exit));
		});
		Fx.FreeAfter(root, total + 0.05);
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

	/// <summary>伤害数字（在目标旁边）。</summary>
	public static void Damage(Control layer, ulong targetId, Vector2 at, int amount)
	{
		ulong now = Time.GetTicksMsec();
		int k = Stack.TryGetValue(targetId, out var st) && now - st.Time < 600 ? st.Count + 1 : 0;
		Stack[targetId] = (now, k);
		Vector2 home = at + new Vector2(70f + 34f * k, -70f + 30f * k);
		Node2D root = Fx.Root(layer, home, "P3DamageNum");
		BlockText(root, amount.ToString(), amount >= 30 ? 84 : 72, Face, 6).Position = new Vector2(-40f, -50f);
		Animate(root, home, 0.5f, Fx.Rand(-0.12f, -0.04f));
		Sparkles(layer, home, 2, 70f);
	}

	/// <summary>"WEAK!"（施加减益时）。</summary>
	public static void Weak(Control layer, Vector2 at)
	{
		Vector2 home = at + new Vector2(-90f, -150f);
		Node2D root = Fx.Root(layer, home, "P3Weak");
		BlockText(root, "WEAK!", 120, WeakFace, 8).Position = new Vector2(-150f, -70f);
		Animate(root, home, 0.55f, -0.17f);
		Sparkles(layer, home, 5, 170f);
		P3Fx.Flash(layer, home, Pal.Cyan, 260f, 0.2f);
	}
}
