using Godot;

namespace P5Joker;

/// <summary>
/// 蛇咬（2026-11-16 第四版；前几版的光带、光尘、月牙用户都说贴图感太重、没有特效的感觉）：
/// 他挥手，手前一亮；群蛇形态那条咒烟蛇（SerpentMantle.Lunge，「没有明确轮廓、纯靠渲染」）在他身前聚出来，整段扑咬：
/// 慢（烟里显形、眼睛一亮）→ 快（窜出去、化成咒烟飞过去）→ 慢（敌人上方聚出蛇头、张嘴）→ 快（咬合）。
/// 敌人身上的上毒落点照旧（游戏的上毒等到咬合那一刻）。
/// </summary>
public static class SnakebiteFx
{
	/// <summary>出手到咬合的秒数。</summary>
	public const float BiteDelay = SerpentMantle.LungeDelay;

	public static float Play(Control front, Control back, Vector2 feet, float tall, float face, Vector2 at, Node2D? body)
	{
		float h = tall;
		Vector2 hand = feet + new Vector2(face * h * 0.3f, -h * 0.83f);   // 挥手伸直的手
		Sfx.Take("gx_whoosh", 0.12f, 0.05f);
		Sfx.Take("gx_whoosh", 0.18f, 0.3f);
		Glow.Flash(front, hand, new Color(1f, 0.92f, 0.9f), h * 0.25f, 0.1f);
		Glow.Flash(back, hand, new Color(0.85f, 0.04f, 0.14f), h * 0.7f, 0.25f);
		return SerpentMantle.Lunge(front, back, feet, tall, face, at, body);
	}
}
