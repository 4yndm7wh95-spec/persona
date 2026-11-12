using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 技能牌特效重做第三版（2026-11-12，用户：按你的审美来，好看、帅、细节足够不粗糙）。积木在 Vfx/Phantom.cs（怪盗风：精细的预告信卡、
/// 红白拖尾、网点斜切色带、拉长的火花、红黑白纸屑）。这里登记的牌覆盖 CardSkillFx2 / 3 里的旧版本。
/// </summary>
public static class SkillRedo
{
	public static void Register(Dictionary<string, Action<SkillCtx>> cards)
	{
		cards["Acrobatics"] = Acrobatics;
	}

	/// <summary>
	/// 杂技（抽 3 弃 1）：手腕一抖，三张预告信卡从指间弹出，绕着他甩出一个大翻身弧（身后一道网点斜切色带同时扫过），
	/// 在头顶扇形一顿、闪一下；两张拉着红白拖尾俯冲进手牌区，第三张被一刀斩成两半，切口一道白，两半翻着飞开、碎成纸屑和余烬。约 1.1s。
	/// </summary>
	public static void Acrobatics(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		float w = h * 0.16f;                                    // 牌宽
		Vector2 hub = c.Chest + new Vector2(-f * h * 0.05f, -h * 0.12f);
		float R = h * 0.62f;
		Vector2 handUi = SkillFx.HandArea(c.Front);

		// 出手：指尖一点白闪 + 几根往外弹的火花；身后的斜切色带
		Glow.Flash(c.Front, c.Hand, Phantom.Hot, h * 0.25f, 0.1f);
		Phantom.Shards(c.Front, c.Hand, 8, 300f * u, 700f * u, new Vector2(f, -0.6f), 40f, 0.3f, 600f * u, 22f * u);
		// （试过身后的网点斜切色带：在这张牌上像一根红棍，不加）
		Sfx.Play(Sfx.CardFlick, 0.5f);

		const float arc = 0.36f, hold = 0.08f;
		for (int i = 0; i < 3; i++)
		{
			int k = i;
			float lag = 0.05f * k;
			// 翻身弧：从手（右下）往后甩过头顶，落在头顶上方的扇形位置
			float a0 = Mathf.Atan2(c.Hand.Y - hub.Y, (c.Hand.X - hub.X)), a1 = -Mathf.Pi / 2f + f * (k - 1) * 0.42f;
			float sweep = -f * (Mathf.Tau * 0.9f);                  // 反着绕一大圈（像翻跟头）
			float aEnd = a0 + sweep;
			aEnd += Mathf.Wrap(a1 - aEnd, -Mathf.Pi, Mathf.Pi);
			Vector2 apex = hub + new Vector2(Mathf.Cos(aEnd), Mathf.Sin(aEnd)) * R * 1.05f;
			bool cut = k == 1;
			float total = arc + hold + (cut ? 0.05f : 0.42f);
			Phantom.P5Card card = Phantom.Card(c.Front, c.Hand, w, lag + total + 0.15, 0.85f);
			card.Root.Visible = false;
			Sfx.Play(Sfx.CardDraw, 0.3f, lag);
			Fx.Animate(card.Root, lag, total, t =>
			{
				card.Root.Visible = true;
				float s = t * total;
				if (s < arc)
				{
					float e = Ease5.OutQuint(s / arc);
					float a = Mathf.Lerp(a0, aEnd, e);
					float rr = Mathf.Lerp(c.Hand.DistanceTo(hub), R * 1.05f, Mathf.Min(1f, e * 1.6f));
					card.Place(hub + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr, a + Mathf.Pi / 2f, e * 12f, 0.7f + 0.3f * e);
					card.Set("sheen", Mathf.PosMod(e * 3f, 1f) * 3f - 1f);
				}
				else if (s < arc + hold)
				{
					float k2 = (s - arc) / hold;
					card.Place(apex, f * (k - 1) * 0.42f, 0f, 1f + 0.12f * Mathf.Sin(k2 * Mathf.Pi));
					card.Set("glow", Mathf.Sin(k2 * Mathf.Pi));
					card.Set("sheen", k2 * 3f - 1f);
					if (card.Trail != null) card.Trail.Fade = 1f - k2;
				}
				else if (!cut)
				{
					float k3 = (s - arc - hold) / 0.42f;
					float e = Fx.InCubic(k3) * 0.75f + k3 * 0.25f;
					Vector2 ctrl = apex + new Vector2(f * h * 0.35f, -h * 0.15f);
					Vector2 p = apex.Lerp(ctrl, e).Lerp(ctrl.Lerp(handUi, e), e);
					if (card.Trail != null) card.Trail.Fade = Mathf.Min(1f, k3 * 3f);
					card.Place(p, f * (k - 1) * 0.42f + e * f * 1.2f, e * 9f, 1f - 0.45f * e);
					card.Set("glow", 0f);
				}
			});
			Fx.After(c.Front, lag + arc, () =>
			{
				Glow.Glint(c.Front, apex + new Vector2(w * 0.4f, -w * 0.6f), 0.55f);
				if (k == 0) Glow.Flash(c.Front, hub + new Vector2(0f, -R), Phantom.Red, h * 0.9f, 0.18f);
			});
			if (!cut)
			{
				Fx.After(c.Front, lag + total, () =>
				{
					card.Trail?.Kill();
					card.Root.Visible = false;
					Phantom.Shards(c.Front, handUi, 6, 200f * u, 500f * u, Vector2.Up, 50f, 0.3f, 500f * u, 16f * u);
				});
				continue;
			}
			// 被斩的那张：一刀横斩、短顿帧，两半翻开烧掉、碎成纸屑
			float cutAt = lag + arc + hold + 0.03f;
			Fx.After(c.Front, cutAt, () =>
			{
				card.Trail?.Kill();
				card.Root.Visible = false;
				Sfx.Play(Sfx.CardCut, 0.45f);
				float ang = f > 0f ? -0.5f : Mathf.Pi + 0.5f;
				Cut.Razor(c.Front, apex, ang, w * 4.2f, 3.5f * u, 0.1f);
				Glow.Flash(c.Front, apex, Phantom.Hot, w * 2.6f, 0.1f);
				HitStop.Request(0.035);
				Fx.Shake(1);
				Phantom.Shards(c.Front, apex, 14, 300f * u, 900f * u, Vector2.Right.Rotated(ang), 25f, 0.35f, 300f * u, 26f * u);
				Phantom.Shards(c.Front, apex, 8, 200f * u, 600f * u, Vector2.Right.Rotated(ang + Mathf.Pi), 25f, 0.3f, 300f * u, 20f * u);
				Vector2 cn = Vector2.Right.Rotated(ang + Mathf.Pi / 2f);   // 切线的法向（屏幕）
				for (int side = -1; side <= 1; side += 2)
				{
					int sd = side;
					Phantom.P5Card half = Phantom.Card(c.Front, apex, w, 0.75);
					float tilt0 = f * (k - 1) * 0.42f;
					// 牌内坐标里的切线法向：把屏幕法向转进牌的坐标（牌旋转 tilt0，宽高比 5:7）
					Vector2 ln = cn.Rotated(-tilt0);
					half.Set("clip", new Vector3(ln.X * sd, ln.Y * sd * 1.4f, 0f));
					Vector2 away = apex + cn * sd * w * 1.6f + new Vector2(f * w * 0.6f, w * 0.8f);
					Fx.Animate(half.Root, 0, 0.7, t =>
					{
						float e = Ease5.OutQuint(t);
						half.Place(apex.Lerp(away, e) + new Vector2(0f, h * 0.35f * t * t), tilt0 + sd * e * 1.4f, sd * e * 1.2f, 1f);
						half.Set("burn", Mathf.Clamp((t - 0.1f) / 0.7f, 0f, 1f));
					});
					Fx.After(c.Front, 0.32f, () => Phantom.Confetti(c.Front, away, 10, 260f * u, cn * sd, 60f, 0.9f, 7f * u));
				}
				Glow.Sparks(c.Front, apex, 12, 40f, 160f, 0.6f, new[] { Phantom.Hot, Phantom.Red, new Color(0.3f, 0f, 0.05f) }, -80f, 0.02f * u, 0.05f * u, Vector2.Up, 120f);
			});
		}
	}
}
