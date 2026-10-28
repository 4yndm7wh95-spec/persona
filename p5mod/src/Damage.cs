using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace P5Joker;

/// <summary>
/// P5R 的伤害数字：原版 DAMAGE_DATA.SPD 的切图（白描边黑字 0~9、身后一块白色漫画爆点「ハート素材」，枪击用带裂纹的那块）。
/// 每个数字左右交错一点角度和高度（P5 那种拼贴感），弹出后停一会儿往上飘走。
/// </summary>
public static class Damage
{
	// Joker 打中的敌人：Damage 前缀里记下（实例 id → 时间、是不是枪击），原版的伤害数字创建时据此替换
	private static readonly Dictionary<ulong, (double time, bool gun)> Hits = new();

	public static void Mark(NCreature target, bool gun) => Hits[target.GetInstanceId()] = (Time.GetTicksMsec() / 1000.0, gun);

	public static bool TakeMark(NCreature target, out bool gun)
	{
		gun = false;
		if (!Hits.TryGetValue(target.GetInstanceId(), out var m) || Time.GetTicksMsec() / 1000.0 - m.time > 1.5)
		{
			return false;
		}
		gun = m.gun;
		return true;
	}

	public static void Show(Control layer, Vector2 at, int amount, bool gun)
	{
		string text = amount.ToString();
		Node2D root = Fx.Root(layer, at + new Vector2(Fx.Rand(-12f, 12f), -110f), "P5DamageNum", 1.2);
		root.Rotation = Fx.Rand(-0.12f, 0.06f);
		const float digitScale = 0.26f, step = 44f;               // 数字贴图 188x371（原版的一半）→ 约 49x96，相邻稍微叠一点
		float width = step * (text.Length - 1);
		if (Fx.Tex(gun ? "dmg_splash_gun" : "dmg_splash") is Texture2D splash)
		{
			Sprite2D sp = Fx.Sprite(root, splash, additive: false);
			sp.Scale = Vector2.One * (0.55f + 0.12f * (text.Length - 1));
			sp.Rotation = Fx.Rand(-0.4f, 0.4f);
			sp.FlipH = Fx.Rng.Next(2) == 0;
		}
		for (int i = 0; i < text.Length; i++)
		{
			if (Fx.Tex("dmg_" + text[i]) is not Texture2D tex)
			{
				continue;
			}
			Sprite2D d = Fx.Sprite(root, tex, additive: false);
			d.Position = new Vector2(-width / 2f + step * i, i % 2 == 0 ? -5f : 6f);
			d.Rotation = i % 2 == 0 ? -0.07f : 0.06f;
			d.Scale = Vector2.One * digitScale;
		}
		Vector2 basePos = root.Position;
		Fx.Animate(root, 0, 1.0, t =>
		{
			// 0~0.08 s 从 1.7 倍弹到 1 倍，停住，最后 0.25 s 往上飘走淡出
			float pop = Mathf.Clamp(t / 0.08f, 0f, 1f);
			float leave = Mathf.Clamp((t - 0.75f) / 0.25f, 0f, 1f);
			root.Scale = Vector2.One * (1f + 0.7f * (1f - Fx.OutCubic(pop)));
			root.Position = basePos + new Vector2(0f, -40f * Fx.InCubic(leave));
			root.Modulate = new Color(1, 1, 1, 1f - leave);
		});
	}
}
