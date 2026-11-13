using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 召唤亚森出手的两张普通毒牌（用户 2026-11-13）：
/// · 致命毒药：亚森隔空一爪 —— 三道弧形爪光（暗红芯、白边）从爪尖飞向敌人，在它身上划出三道斜的爪痕，毒从爪痕里渗开：
///   敌人身后一团红网点光晕、闪光，暗红的毒滴往下淌、毒的光粒往上冒，敌人一阵阵发暗红。不用光束、不用墨块。
/// · 蛇咬：亚森出手时爪尖一亮，一道疾光射到敌人斜上方（蛇从那团咒烟里窜出来咬，SerpentMantle.Bite）。
/// 音效只用游戏样本。
/// </summary>
public static class PoisonCardsFx
{
	/// <summary>爪光从爪尖飞到敌人的秒数（游戏结算推到爪光到达那一刻）。</summary>
	public const float ClawFly = 0.13f;

	public static void ArseneClaw(Control front, Control back, Vector2 claw, Vector2 center, float th, float tw, Node2D? body, float tall, float face)
	{
		float u = Fx.Screen(front).Y / 1080f;
		th = Mathf.Max(th, 120f);
		tw = Mathf.Max(tw, th * 0.6f);
		Sfx.Take("gx_slice", 0.26f * 0.8f, ClawFly);
		Glow.Flash(front, claw, new Color(1f, 0.85f, 0.82f), tall * 0.35f, 0.14f);
		// 三道弧形爪光：从爪尖飞过去，各自弧度不同，到敌人身上错开排成三道
		Vector2 dir = (center - claw).Normalized();
		Vector2 perp = new(-dir.Y, dir.X);
		for (int i = 0; i < 3; i++)
		{
			float k = i - 1f;
			Vector2 a = claw + perp * k * tall * 0.06f;
			Vector2 b = center + perp * k * th * 0.2f;
			Vector2 m = (a + b) * 0.5f + perp * (k * 0.2f + 0.35f) * a.DistanceTo(b) * 0.25f;
			Func<float, Vector2> path = t => a.Lerp(m, t).Lerp(m.Lerp(b, t), t);
			SkillRedo5.Streak(front, path, 0.015f * i, ClawFly, tall * 0.65f, tall * 0.12f, new Color(0.9f, 0.06f, 0.14f), 1.1f);
			SkillRedo5.Streak(front, path, 0.015f * i + 0.005f, ClawFly, tall * 0.5f, tall * 0.04f, new Color(1f, 0.88f, 0.86f), 1.5f);
		}
		// 爪痕：三道斜着划下去的痕（左上 → 右下，朝 Joker 那边倾），划完停一下再淡掉
		Fx.After(front, ClawFly, () =>
		{
			float slant = -face * 0.5f;   // 斜角
			Vector2 down = new Vector2(Mathf.Sin(slant), 1f).Normalized();
			Vector2 across = new(-down.Y, down.X);
			for (int i = 0; i < 3; i++)
			{
				float k = i - 1f;
				Vector2 c0 = center + across * k * tw * 0.22f + new Vector2(0f, -th * 0.04f * k);
				Vector2 p0 = c0 - down * th * 0.5f, p1 = c0 + down * th * 0.5f;
				ClawMark(front, p0, p1, tall, 0.03f * i);
			}
			Fx.Shake(1);
			Glow.Flash(front, center, new Color(1f, 0.9f, 0.88f), tw * 0.6f, 0.14f);
			Glow.Flash(back, center, new Color(0.9f, 0.08f, 0.14f), tw * 1.9f, 0.32f);
			SkillRedo5.SoftBurst(back, center, Mathf.Max(tw, th) * 1.7f, 0.45f, new Color(0.85f, 0.05f, 0.12f));
			// 毒从爪痕里渗开：暗红毒滴往下淌，毒的光粒往上冒
			Glow.Sparks(front, center, 22, 60f * u, 220f * u, 0.8f, new[] { new Color(1f, 0.4f, 0.38f), new Color(0.75f, 0.04f, 0.1f), new Color(0.3f, 0.01f, 0.04f) },
				520f * u, 0.025f * u, 0.055f * u, Vector2.Down, 70f);
			Glow.Sparks(front, center, 16, 30f * u, 110f * u, 0.9f, new[] { new Color(1f, 0.6f, 0.55f), new Color(0.85f, 0.08f, 0.12f), new Color(0.4f, 0.02f, 0.05f) },
				-130f * u, 0.02f * u, 0.045f * u, Vector2.Up, 60f);
			if (body != null && GodotObject.IsInstanceValid(body))
			{
				Color rest = body.Modulate;
				Node2D tk = Fx.Root(front, center, "P5DeadlyTint", 1.0);
				Fx.Animate(tk, 0, 0.9, t =>
				{
					if (!GodotObject.IsInstanceValid(body)) return;
					float flash = Mathf.Exp(-t * 0.9f * 16f) * 0.8f;
					float pulse = 0.35f * (0.5f + 0.5f * Mathf.Cos(t * 0.9f * 14f)) * (1f - t);
					body.Modulate = rest.Lerp(new Color(0.5f, 0.2f, 0.24f), pulse + 0.3f * (1f - t)) + new Color(flash, flash * 0.85f, flash * 0.85f, 0f);
				});
				tk.TreeExiting += () => { if (GodotObject.IsInstanceValid(body)) body.Modulate = rest; };
			}
		});
	}

	public static void ArseneSummonsSnake(Control front, Control back, Vector2 claw, Vector2 at, float tall, float face)
	{
		// 蛇从敌人斜上方的咒烟里窜出来（SerpentMantle.Bite 的烟团位置）：亚森的爪尖一亮，一道疾光先射到那里
		Vector2 cloud = at + new Vector2(-0.6f * face, -1.25f) * tall * 0.6f;
		Glow.Flash(front, claw, new Color(1f, 0.85f, 0.82f), tall * 0.35f, 0.14f);
		Vector2 m = (claw + cloud) * 0.5f + new Vector2(0f, -tall * 0.3f);
		SkillRedo5.Streak(front, t => claw.Lerp(m, t).Lerp(m.Lerp(cloud, t), t), 0f, 0.12f, tall * 0.7f, tall * 0.13f, new Color(0.9f, 0.06f, 0.14f), 1.1f);
		SkillRedo5.Streak(front, t => claw.Lerp(m, t).Lerp(m.Lerp(cloud, t), t), 0.01f, 0.12f, tall * 0.55f, tall * 0.045f, new Color(1f, 0.88f, 0.86f), 1.5f);
		Fx.After(front, 0.11f, () =>
		{
			Glow.Flash(back, cloud, new Color(0.9f, 0.1f, 0.15f), tall * 0.7f, 0.25f);
			SkillRedo5.SoftBurst(back, cloud, tall * 0.8f, 0.35f, new Color(0.85f, 0.05f, 0.12f));
		});
	}

	/// <summary>
	/// 一道爪痕：0.07s 从 p0 划到 p1（两头尖、中间粗），停 0.3s，再 0.35s 淡掉。三层：普通混合的暗红底（压在敌人身上看得清）、
	/// 加法的红芯、白亮芯；刚划出来那一下最亮。
	/// </summary>
	private static void ClawMark(Control layer, Vector2 p0, Vector2 p1, float tall, float delay)
	{
		const float draw = 0.07f, hold = 0.3f, fade = 0.35f;
		Node2D root = Fx.Root(layer, Vector2.Zero, "P5ClawMark", delay + draw + hold + fade + 0.1);
		root.GlobalPosition = Vector2.Zero;
		Polygon2D bed = Fx.Poly(root, new[] { Vector2.Zero, Vector2.Right, Vector2.Down }, new Color(0.35f, 0.01f, 0.05f, 0.9f));
		Polygon2D mid = Glow.TaperPoly(root, new Color(1f, 0.1f, 0.18f, 0.95f));
		Polygon2D core = Glow.TaperPoly(root, new Color(1f, 0.95f, 0.92f, 1f));
		root.Visible = false;
		Fx.Animate(root, delay, draw + hold + fade, t =>
		{
			root.Visible = true;
			float s = t * (draw + hold + fade);
			float g = Ease5.OutQuint(Mathf.Min(1f, s / draw));
			Vector2 end = p0.Lerp(p1, g);
			float a = 1f - Ease5.Seg(s, draw + hold, fade);
			float hot = Mathf.Exp(-Mathf.Max(0f, s - draw) * 7f);
			bed.Polygon = Glow.TaperPts(p0, end, tall * 0.16f);
			mid.Polygon = Glow.TaperPts(p0, end, tall * (0.09f + 0.03f * hot));
			core.Polygon = Glow.TaperPts(p0, end, tall * (0.025f + 0.02f * hot));
			bed.Modulate = new Color(1f, 1f, 1f, a);
			mid.Modulate = new Color(1f, 1f, 1f, a * (0.6f + 0.4f * hot));
			core.Modulate = new Color(1f, 1f, 1f, a * (0.35f + 0.65f * hot));
		});
	}
}
