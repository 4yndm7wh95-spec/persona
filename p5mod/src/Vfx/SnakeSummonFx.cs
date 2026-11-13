using Godot;

namespace P5Joker;

/// <summary>
/// 蛇咬（用户 2026-11-13 B 方案）：召唤亚森，亚森出手时一挥手 —— 爪尖一亮、迸开一圈碎光和一团柔的红光晕，
/// 一道像蛇一样弯几道的咒怨光带（PoisonFx.Beam，waves）游到敌人斜上方，那里的咒烟鼓起来（闪光、网点光晕、四周的柔烟往里收），
/// 然后蛇从烟里窜出来咬（SerpentMantle.Bite，原来的扑咬和毒的落点都不变）。全部圆润的形状，没有尖角。
/// </summary>
public static class SnakeSummonFx
{
	/// <summary>挥手到蛇从烟里窜出来的秒数。</summary>
	public const float Lag = 0.3f;   // 爪尖收光 0.1s + 光带游过去 0.18s

	public static void Play(Control front, Control back, Vector2 claw, Vector2 at, float tall, float face)
	{
		float u = Fx.Screen(front).Y / 1080f;
		// 蛇从这团咒烟里窜出来（SerpentMantle.Bite 的烟团：按 0.6 倍身高算，在敌人斜上方）
		Vector2 cloud = at + new Vector2(-0.6f * face, -1.25f) * tall * 0.6f;
		// 挥手：几团红光先往爪尖收拢（看得出是从他手里放出来的），爪尖一亮、迸开碎光和一团柔的红光晕
		for (int i = 0; i < 6; i++)
		{
			Vector2 from = claw + Vector2.FromAngle(Mathf.Tau * i / 6f + Fx.Rand(-0.3f, 0.3f)) * tall * 0.28f;
			Node2D pr = Fx.Root(front, from, "P5ClawGather", 0.16);
			Sprite2D sp = Glow.SoftSprite(pr, Vector2.Zero, new Color(1f, 0.2f, 0.28f, 0f), tall * 0.12f / 32f);
			Fx.Animate(pr, 0, 0.12, t =>
			{
				float e = Fx.InCubic(t);
				pr.GlobalPosition = from.Lerp(claw, e);
				sp.Modulate = new Color(1f, 0.2f, 0.28f, 0.8f * Mathf.Sin(Mathf.Pi * Mathf.Min(1f, t * 1.1f)));
			});
		}
		Fx.After(front, 0.1, () =>
		{
			Glow.Flash(front, claw, new Color(1f, 0.9f, 0.88f), tall * 0.35f, 0.16f);
			Glow.Flash(back, claw, new Color(0.9f, 0.1f, 0.16f), tall * 1.0f, 0.32f);
			SkillRedo5.SoftBurst(back, claw, tall * 0.8f, 0.38f, new Color(0.85f, 0.05f, 0.12f));
			Glow.Sparks(front, claw, 18, 80f * u, 320f * u, 0.45f, new[] { new Color(1f, 0.9f, 0.88f), new Color(0.9f, 0.12f, 0.18f), new Color(0.3f, 0.02f, 0.05f) },
				40f * u, 0.015f * u, 0.04f * u, (cloud - claw).Normalized(), 60f);
			// 蛇形的咒怨光带游过去（波沿着光带往前游、靠手那头细、前面一团亮的蛇头）
			PoisonFx.Beam(front, claw, cloud, 0.13f, Lag - 0.12f, 24f * u, 0.1f, 0.25f, 2.5f);
		});
		// 烟团鼓起来
		Fx.After(front, Lag - 0.04f, () =>
		{
			Glow.Flash(front, cloud, new Color(1f, 0.85f, 0.85f), tall * 0.3f, 0.12f);
			Glow.Flash(back, cloud, new Color(0.9f, 0.1f, 0.15f), tall * 0.85f, 0.3f);
			SkillRedo5.SoftBurst(back, cloud, tall * 0.9f, 0.4f, new Color(0.85f, 0.05f, 0.12f));
			for (int i = 0; i < 8; i++)
			{
				Vector2 from = cloud + Vector2.FromAngle(Mathf.Tau * i / 8f + Fx.Rand(-0.2f, 0.2f)) * tall * 0.42f;
				Node2D pr = Fx.Root(back, from, "P5SnakeGather", 0.38);
				Sprite2D sp = new() { Texture = Glow.Soft };
				pr.AddChild(sp);
				float sz = tall * Fx.Rand(0.2f, 0.3f);
				Fx.Animate(pr, 0, 0.32, t =>
				{
					float e = Fx.OutCubic(t);
					pr.GlobalPosition = from.Lerp(cloud, e);
					sp.Scale = Vector2.One * sz * (1f - 0.5f * e) / 32f;
					sp.Modulate = new Color(0.38f, 0.03f, 0.08f, 0.6f * Mathf.Sin(Mathf.Pi * Mathf.Min(1f, t * 1.15f)));
				});
			}
		});
	}
}
