using Godot;

namespace P5Joker;

/// <summary>
/// 蛇咬（2026-11-16 第八版，用户画的线路）：他挥手，手心透出暗红的光；群蛇形态那条咒烟蛇（SerpentMantle.Lunge，「没有明确轮廓、纯靠渲染」）
/// 从他伸出的手里冲出来，往前猛窜、往上兜一个大圈（快）→ 翻过圈顶时慢下来、倒挂着锁定敌人（顿一下，嘴前聚光）→ 顺着后半圈加速俯冲、
/// 斜着扎到敌人身上咬合（快）；身后拖着一道慢慢散开的咒烟。出手到咬合 0.48s。
/// 敌人身上保留前几版的咬合特效（用户点名）：上下两道月牙毒牙合拢「咔」一下 + 空间一扭 + 火星；声音只有咬合那一下（群蛇形态的原版咬合，Lunge 里放）。
/// 敌人身上的上毒落点照旧（游戏的上毒等到咬合那一刻）。
/// </summary>
public static class SnakebiteFx
{
	/// <summary>出手到咬合的秒数。</summary>
	public const float BiteDelay = SerpentMantle.LungeDelay;

	public static float Play(Control front, Control back, Vector2 feet, float tall, float face, Vector2 at, Node2D? body)
	{
		float h = tall, f = face;
		Vector2 hand = feet + new Vector2(f * h * 0.3f, -h * 0.83f);   // 挥手伸直的手
		// 出手（2026-11-16 用户：音效别加那么多；打出时的特效要更合适）：不放白闪、星光和出手的风声 ——
		// 手心先透出一点暗红的光，蛇冲出来的那一刻手边炸开一团咒烟（SerpentMantle 里画）
		Glow.Flash(back, hand, new Color(0.7f, 0.02f, 0.12f), h * 0.8f, 0.3f);
		float bite = SerpentMantle.Lunge(front, back, feet, tall, face, at, body);

		// 毒牙：上下两道月牙合拢（前几版的咬合特效）
		float ang = f > 0f ? 0f : Mathf.Pi, jaw = h * 0.85f;
		Blade.Arc(front, at + new Vector2(0f, -h * 0.1f), ang, jaw, h * 0.2f, h * 0.15f, f < 0f, 0.05f, 0.04f, 0.08f, 0.2f, true, bite - 0.02f);
		Blade.Arc(front, at + new Vector2(0f, h * 0.1f), ang, jaw * 0.9f, h * 0.18f, h * 0.13f, f > 0f, 0.05f, 0.04f, 0.08f, 0.2f, true, bite - 0.01f);
		Fx.After(front, bite, () =>
		{
			Glow.Ripple(front, at, h * 1.2f, 0.38f, 0.03f);
			Vanilla.Burst(front, Vanilla.Specks, at, 0f, 0.9f, Lut5.Blood, amount: 16);
		});
		return bite;
	}
}
