using Godot;

namespace P5Joker;

/// <summary>
/// 蛇咬（2026-11-16 用户：原来亚森甩光带 + 敌人那里烟里冒小蛇，两段配合不好 → 推倒重做；不召唤亚森、不扶面具，直接挥手）。
/// 全程是同一条蛇：他一挥手，手前一亮、一道光落到脚前地面；一条黑红的蛇影贴着地面蜿蜒游向敌人（咒怨光带的蛇形模式，带地面火星）；
/// 游到敌人脚前猛地昂头扑起（一道弧形疾光从地面窜上去），在敌人身上两道月牙毒牙上下合拢「咔」地咬住 —— 顿帧、白闪、空间一扭、火星；
/// 敌人身上的上毒落点照旧（游戏的上毒等到咬合那一刻）。返回从现在到咬合的秒数。
/// </summary>
public static class SnakebiteFx
{
	private const float Lead = 0.1f, Travel = 0.3f, Rear = 0.09f;

	/// <summary>咬合在出手后多少秒。</summary>
	public const float BiteDelay = Lead + Travel + Rear;

	public static float Play(Control front, Control back, Vector2 feet, float tall, float face, Vector2 at, Vector2 enemyFeet)
	{
		float h = tall, f = face, u = front.GetCanvasTransform().Scale.Y;
		Vector2 hand = feet + new Vector2(f * h * 0.28f, -h * 0.83f);   // 挥手伸直的手（同触媒、迷雾）
		Vector2 start = feet + new Vector2(f * h * 0.4f, -h * 0.03f);
		Vector2 end = new(enemyFeet.X - f * h * 0.32f, enemyFeet.Y - h * 0.03f);
		Color red = new(0.92f, 0.06f, 0.16f), hot = new(1f, 0.95f, 0.94f);
		Color pink = new(1f, 0.32f, 0.38f), deep = new(0.4f, 0.01f, 0.06f);
		Sfx.Take("gx_whoosh", 0.16f);
		Sfx.Take("gx_slice", 0.26f, BiteDelay);
		Sfx.Take("gx_pop", 0.18f, BiteDelay + 0.01f);

		// 出手：手前一亮，一道光从手落到脚前地面（蛇从这里钻出去）
		Glow.Flash(front, hand, hot, h * 0.3f, 0.1f);
		Glow.Flash(back, hand, red, h * 0.8f, 0.24f);
		Glow.Glint(front, hand, 0.6f);
		SkillRedo5.Streak(front, t => hand.Lerp(start, t), 0.02f, Lead, h * 0.4f, h * 0.035f, red, 1.3f);
		Fx.After(front, Lead, () =>
		{
			Glow.Flash(back, start, red, h * 0.7f, 0.25f);
			Glow.Sparks(front, start, 10, 60f * u, 260f * u, 0.35f, new[] { hot, pink, deep }, 200f * u, 0.006f * u, 0.016f * u, Vector2.Up, 70f);
		});
		// 贴地游过去：咒怨光带的蛇形模式（几道弯、波往前游、靠手那头细），真实宽度
		PoisonFx.Beam(back, start, end, 0.045f, Travel, h * 0.085f, 0.06f, 0.2f, 4f, true);
		// 地面一路溅起的火星
		for (int i = 1; i <= 4; i++)
		{
			float k = i / 5f;
			Vector2 p = start.Lerp(end, k);
			Vanilla.Burst(back, Vanilla.Specks, p, -Mathf.Pi * 0.5f, 0.35f, Lut5.Curse, amount: 3, delay: Lead + Travel * (1f - Mathf.Pow(1f - k, 5f)) * 0.9f);
		}
		// 昂头扑起：从地面窜到咬合点的一道弧形疾光
		Vector2 ctrl = new(end.X - f * h * 0.1f, at.Y + h * 0.05f);
		SkillRedo5.Streak(front, t => end.Lerp(ctrl, t).Lerp(ctrl.Lerp(at, t), t), Lead + Travel - 0.01f, Rear + 0.02f, h * 0.55f, h * 0.06f, red, 1.4f);
		// 毒牙：上下两道月牙合拢
		float ang = f > 0f ? 0f : Mathf.Pi;
		float jaw = h * 0.85f;
		Blade.Arc(front, at + new Vector2(0f, -h * 0.1f), ang, jaw, h * 0.2f, h * 0.15f, f < 0f, 0.05f, 0.04f, 0.08f, 0.2f, true, BiteDelay - 0.04f);
		Blade.Arc(front, at + new Vector2(0f, h * 0.1f), ang, jaw * 0.9f, h * 0.18f, h * 0.13f, f > 0f, 0.05f, 0.04f, 0.08f, 0.2f, true, BiteDelay - 0.03f);
		Fx.After(front, BiteDelay, () =>
		{
			HitStop.Request(0.05);
			Fx.Shake(1);
			Glow.Flash(front, at, hot, h * 0.45f, 0.1f);
			Glow.Flash(back, at, red, h * 1.4f, 0.32f);
			Glow.Ripple(front, at, h * 1.2f, 0.38f, 0.03f);
			Glow.Sparks(front, at, 22, 140f * u, 560f * u, 0.45f, new[] { hot, pink, deep }, 0f, 0.008f * u, 0.024f * u, new Vector2(f, -0.2f), 120f);
			Vanilla.Burst(front, Vanilla.Specks, at, 0f, 0.9f, Lut5.Blood, amount: 20);
		});
		return BiteDelay;
	}
}
