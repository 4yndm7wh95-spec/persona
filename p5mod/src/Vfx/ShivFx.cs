using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 小刀流的关键牌（用户 2026-11-04：接下来做剩余其他牌的特效）。小刀流是 Joker 自己的怪盗手法（扯手套施法），不召唤人格面具。
/// 拿到小刀本身已经有反馈：能量球旁的弹巢一发发装弹（AmmoView）。
/// </summary>
public static class ShivFx
{
	public readonly record struct Target(Vector2 At, float Size);

	/// <summary>
	/// 刀扇（之后每一发都打所有敌人）。用户 2026-11-04：小刀在这套 mod 里就是开枪 → 转一下枪就好，不用像扇子、不用夸张。
	/// 选中这张牌 Joker 掏枪瞄准（和小刀牌一样），打出时把枪在指尖转一圈收起（aim_out）：转的时候指尖甩出一圈红色光环和几颗火星，
	/// 枪停下那一刻指尖一闪（用户 11-04：锁定框没必要）。finger = 转枪的指尖（GunGrip），spin = 转一圈要多久。
	/// </summary>
	public static void FanOfKnives(Control layer, Vector2 finger, IReadOnlyList<Target> enemies, float spin = 0.22f)
	{
		Sfx.Play(Sfx.GunDraw, 0.45f);
		Node2D root = Fx.Root(layer, finger, "P5GunTwirl", spin + 0.4);
		ColorRect ring = Glow.RingRect(root, 230f);
		ColorRect outer = Glow.RingRect(root, 300f);
		Glow.SetParam(outer, "tint", new Color(1f, 0.1f, 0.16f, 1f));
		Glow.SetParam(outer, "width", 0.02f);
		Glow.SetParam(ring, "tint", new Color(1f, 0.18f, 0.24f, 1f));
		Glow.SetParam(ring, "width", 0.035f);
		Fx.Animate(root, 0, spin + 0.25f, t =>
		{
			float s = t * (spin + 0.25f);
			float grow = Ease5.OutQuint(Mathf.Min(1f, s / spin));
			Glow.SetParam(ring, "radius", Mathf.Lerp(0.2f, 0.62f, grow));
			float a = s < spin ? 0.9f : 0.9f * (1f - (s - spin) / 0.25f);
			Glow.SetParam(ring, "alpha", a);
			Glow.SetParam(outer, "radius", Mathf.Lerp(0.3f, 0.7f, grow));
			Glow.SetParam(outer, "alpha", a * 0.5f);
			root.Rotation = s * 30f;
		});
		Vanilla.Burst(layer, Vanilla.Specks, finger, -Mathf.Pi / 2f, 0.6f, Lut5.Blood, amount: 10);
		Fx.After(layer, spin, () =>
		{
			Glow.Flash(layer, finger, new Color(1f, 0.4f, 0.42f), 160f, 0.2f);      // 用户 2026-11-04：不要锁定框
		});
	}
}
