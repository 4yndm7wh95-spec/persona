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
	/// 刀扇（之后小刀打所有敌人）。两拍：
	/// · 展扇：Joker 手边打开一把真正的扇子——七片宽底尖头的刀光（slash_thrust）围成半圆，从上往下一片片展开，扇面朝着敌人；
	/// · 甩出：扇子往前一甩，一道竖起来的大月牙斩（slash_heavy）横扫过所有敌人的位置，敌人依次亮起红色锁定框（"以后全都打得到"）。
	/// hand = 出手点（扯手套的手），facing = Joker 朝向。
	/// </summary>
	public static void FanOfKnives(Control layer, Vector2 hand, IReadOnlyList<Target> enemies, float facing = 1f)
	{
		if (Fx.Tex("slash_thrust") is not Texture2D thrust || Fx.Tex("slash_heavy") is not Texture2D heavy)
		{
			return;
		}
		// 扇面朝向：敌人群的中心；扇子张开 110°
		Vector2 aim = new(facing, 0f);
		float top = float.MaxValue, bottom = float.MinValue, midX = hand.X + 700f * facing;
		if (enemies.Count > 0)
		{
			Vector2 sum = Vector2.Zero;
			float ex0 = float.MaxValue, ex1 = float.MinValue;
			foreach (Target e in enemies)
			{
				sum += e.At;
				top = Mathf.Min(top, e.At.Y - e.Size * 0.5f);
				bottom = Mathf.Max(bottom, e.At.Y + e.Size * 0.5f);
				ex0 = Mathf.Min(ex0, e.At.X);
				ex1 = Mathf.Max(ex1, e.At.X);
			}
			aim = (sum / enemies.Count - hand).Normalized();
			midX = (ex0 + ex1) * 0.5f;
		}
		else
		{
			top = hand.Y - 260f; bottom = hand.Y + 160f;
		}
		const int blades = 7;
		const float spread = 1.9f, radius = 360f;
		float baseAng = aim.Angle();
		float sx = radius / thrust.GetWidth() * 1.15f;
		Sfx.Play(Sfx.Swish, 0.4f);
		for (int i = 0; i < blades; i++)
		{
			// 从上往下一片片打开（折扇），每片晚 0.028s
			float k = i / (float)(blades - 1);
			float ang = baseAng + (k - 0.5f) * spread;
			Vector2 dir = Vector2.FromAngle(ang);
			float delay = 0.028f * i;
			Paint.Sweep(layer, "slash_thrust", hand + dir * radius * 0.55f, ang, new Vector2(sx, 0.3f), Lut5.Blood,
				0.06f, 0.2f + 0.028f * (blades - 1 - i), 0.18f, 10f, delay, 1.3f);
		}
		Glow.Flash(layer, hand, new Color(1f, 0.35f, 0.38f), 220f, 0.3f);
		// 甩出：扇子张满后 0.06s，一道竖起来的大月牙斩从扇面那边横扫过所有敌人
		float open = 0.028f * (blades - 1) + 0.06f;
		float span = Mathf.Max(260f, bottom - top) * 1.25f;
		float midY = (top + bottom) * 0.5f;
		// 刀浪：一道竖起来的月牙，从扇面出发横着飞过所有敌人（0.26s），飞到谁身上谁亮锁定框
		float x0 = hand.X + radius * 0.6f * facing, x1 = midX;
		foreach (Target e in enemies)
		{
			x1 = facing > 0f ? Mathf.Max(x1, e.At.X + e.Size * 0.6f) : Mathf.Min(x1, e.At.X - e.Size * 0.6f);
		}
		Fx.After(layer, open + 0.04f, () =>
		{
			Sfx.Play(Sfx.SwishHeavy, 0.5f);
			float sx2 = span / heavy.GetWidth() * 1.35f;
			float rot = facing > 0f ? Mathf.Pi / 2f : -Mathf.Pi / 2f;
			if (Paint.Make(layer, "slash_heavy", new Vector2(x0, midY), rot, new Vector2(sx2, sx2 * 0.9f), Lut5.Blood) is not Paint.Stroke wave)
			{
				return;
			}
			wave.Set("reveal", 1.12f);
			wave.Set("glow", 1.3f);
			const float travel = 0.26f, fade = 0.12f;
			var locked = new bool[enemies.Count];
			Fx.Animate(wave.Root, 0, travel + fade, t =>
			{
				float s = t * (travel + fade);
				float u = Mathf.Min(1f, s / travel);
				float x = Mathf.Lerp(x0, x1, 1f - (1f - u) * (1f - u));
				wave.Root.GlobalPosition = new Vector2(x, midY);
				wave.Set("erode", s < travel ? 0f : Mathf.Lerp(0.05f, 1.15f, (s - travel) / fade));
				for (int i = 0; i < enemies.Count; i++)
				{
					if (!locked[i] && (x - enemies[i].At.X) * facing >= -enemies[i].Size * 0.3f)
					{
						locked[i] = true;
						Target e = enemies[i];
						LockOn lk = LockOn.Start(layer, e.At, e.Size);
						Sfx.Play(Sfx.Cursor, 0.35f);
						Fx.After(layer, 0.5, lk.Release);
					}
				}
			});
			Fx.FreeAfter(wave.Root, travel + fade + 0.05);
		});
	}
}
