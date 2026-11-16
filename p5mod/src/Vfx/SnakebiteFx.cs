using Godot;

namespace P5Joker;

/// <summary>
/// 蛇咬（2026-11-16 用户：原来亚森甩光带 + 敌人那里烟里冒小蛇，两段配合不好 → 推倒重做；不召唤亚森、不扶面具，直接挥手）。
/// 他一挥手，手前一亮，几道红黑疾光沿 S 形的路从手里窜向敌人（像蛇扑出去，2026-11-16 第二版：贴地的蛇形光带太贴图、反馈不明显），
/// 在敌人身上两道月牙毒牙上下合拢「咔」地咬住 —— 顿帧、白闪、空间一扭、火星；
/// 敌人身上的上毒落点照旧（游戏的上毒等到咬合那一刻）。返回从现在到咬合的秒数。
/// </summary>
public static class SnakebiteFx
{
	private const float Lead = 0.06f, Travel = 0.22f, Rear = 0.03f;

	/// <summary>咬合在出手后多少秒。</summary>
	public const float BiteDelay = Lead + Travel + Rear;

	public static float Play(Control front, Control back, Vector2 feet, float tall, float face, Vector2 at, Vector2 enemyFeet)
	{
		float h = tall, f = face, u = front.GetCanvasTransform().Scale.Y;
		Vector2 hand = feet + new Vector2(f * h * 0.28f, -h * 0.83f);   // 挥手伸直的手（同触媒、迷雾）
		Color red = new(0.92f, 0.06f, 0.16f), hot = new(1f, 0.95f, 0.94f);
		Color pink = new(1f, 0.32f, 0.38f), deep = new(0.4f, 0.01f, 0.06f);
		Sfx.Take("gx_whoosh", 0.18f);
		Sfx.Take("gx_slice", 0.26f, BiteDelay);                       // 咬合声（用户：这个不错，保留）；毒的音效走原来的落点（PoisonFx），这里不再加

		// 出手：手前一亮
		Glow.Flash(front, hand, hot, h * 0.32f, 0.1f);
		Glow.Flash(back, hand, red, h * 0.9f, 0.24f);
		Glow.Glint(front, hand, 0.7f);
		// 蛇扑：几道疾光沿 S 形的路从手里窜向敌人（2026-11-16：贴地的蛇形光带太贴图、反馈不明显 → 换成空中的疾光扑过去）
		Vector2 dv = at - hand, nrm = dv.Orthogonal().Normalized();
		float amp = Mathf.Min(h * 0.32f, dv.Length() * 0.16f);
		Vector2 Path(float t, float ph) => hand.Lerp(at, t) + nrm * Mathf.Sin(t * Mathf.Pi * 2f + ph) * amp * (1f - t * 0.85f);
		SkillRedo5.Streak(back, t => Path(t, 0.6f), Lead - 0.01f, Travel, h * 1.2f, h * 0.16f, deep, 0.9f);
		SkillRedo5.Streak(front, t => Path(t, 0f), Lead, Travel, h * 1.3f, h * 0.13f, red, 1.35f);
		SkillRedo5.Streak(front, t => Path(t, 0f), Lead + 0.01f, Travel, h * 0.9f, h * 0.045f, hot, 1.6f);
		SkillRedo5.Streak(front, t => Path(t, -0.7f), Lead + 0.02f, Travel, h * 0.6f, h * 0.035f, pink, 1.3f);
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
			Vanilla.Burst(front, Vanilla.Specks, at, 0f, 0.9f, Lut5.Blood, amount: 16);
		});
		return BiteDelay;
	}
}
