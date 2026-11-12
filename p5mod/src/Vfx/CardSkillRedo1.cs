using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 技能牌重做，第一批（2026-11-12，用户：之前的技能特效好难看，第二、三块全部重做，一批五张，做完给看视频）：
/// 融入暗影、暗影步、萎靡、扫腿（残影在 CardSkillFx2.Blur 里改成一层贴身的淡红剪影）。
/// 统一用群蛇那种暗红咒烟（CurseSmoke），只用红黑白，不用细线圆圈、图标、预告信。登记在 CardSkillFx 里，盖掉第二批的同名牌。
/// 音效走 Sfx 的对照表（游戏自带的 FMOD 事件）。
/// </summary>
public static class CardSkillRedo1
{
	public static void Register(Dictionary<string, Action<SkillCtx>> cards)
	{
		cards["Shadowmeld"] = Shadowmeld;
		cards["ShadowStep"] = ShadowStep;
		cards["Malaise"] = Malaise;
		cards["LegSweep"] = LegSweep;
	}

	private static bool Ok(Control? c) => c != null && GodotObject.IsInstanceValid(c);

	/// <summary>融入暗影：脚下铺开一摊贴地翻滚的咒烟，几缕烟往上卷到腰，Joker 整个暗下去、再慢慢恢复。</summary>
	public static void Shadowmeld(SkillCtx c)
	{
		float h = c.Tall;
		Sfx.Play(Sfx.ShadowmeldSink, 0.5f);
		Control under = Ok(c.Back) ? c.Back! : c.Front;
		CurseSmoke.Puff(under, c.Feet + new Vector2(0f, -h * 0.02f), h * 1.7f, 1.7, squash: 0.22f, grow: 0.2f, hold: 0.55f);
		CurseSmoke.Puff(c.Front, c.Feet + new Vector2(0f, h * 0.01f), h * 1.3f, 1.5, delay: 0.05f, squash: 0.16f, grow: 0.25f, hold: 0.5f, dens: 0.8f);
		for (int i = 0; i < 6; i++)
		{
			float x = (i - 2.5f) / 2.5f * h * 0.32f;
			CurseSmoke.Puff(i % 2 == 0 ? c.Front : under, c.Feet + new Vector2(x, -h * 0.08f), h * Fx.Rand(0.36f, 0.5f), Fx.Rand(0.9f, 1.2f),
				delay: 0.06f + 0.05f * i, rise: h * Fx.Rand(0.35f, 0.55f), grow: 0.3f, hold: 0.2f, dens: 0.75f, drift: new Vector2(-x * 0.3f, 0f));
		}
		CurseSmoke.Darken(c.Front, c.SelfTint, new Color(0.3f, 0.24f, 0.28f), 0.22f, 0.7f, 0.6f, 0.08f);
	}

	/// <summary>暗影步：身上「噗」地炸开一团烟、人瞬间暗下去，一串烟往身后窜出去，在后方聚成一团再散掉；人慢慢亮回来。</summary>
	public static void ShadowStep(SkillCtx c)
	{
		float h = c.Tall;
		Sfx.Play(Sfx.ShadowStepCut, 0.5f);
		Control under = Ok(c.Back) ? c.Back! : c.Front;
		CurseSmoke.Puff(c.Front, c.Chest, h * 0.95f, 0.65, grow: 0.25f, hold: 0.1f);
		CurseSmoke.Darken(c.Front, c.SelfTint, new Color(0.12f, 0.08f, 0.1f), 0.06f, 0.35f, 0.55f);
		Vector2 back = c.Chest + new Vector2(-c.Face * h * 0.95f, -h * 0.05f);
		for (int i = 0; i < 6; i++)
		{
			float k = (i + 1) / 6f;
			Vector2 at = c.Chest.Lerp(back, k) + new Vector2(0f, -Mathf.Sin(k * Mathf.Pi) * h * 0.12f);
			CurseSmoke.Puff(i < 3 ? c.Front : under, at, h * Mathf.Lerp(0.5f, 0.32f, k), 0.45, delay: 0.03f * i, grow: 0.3f, hold: 0.05f, dens: 0.8f);
		}
		CurseSmoke.Puff(under, back, h * 0.75f, 0.8, delay: 0.2f, grow: 0.3f, hold: 0.3f);
	}

	/// <summary>萎靡：敌人头顶聚起一团咒烟，往下一压罩住它，敌人发暗一会儿，烟从脚边散开。</summary>
	public static void Malaise(SkillCtx c)
	{
		Sfx.Play(Sfx.MalaisePress, 0.5f);
		foreach (SkillFx.Victim v in c.Target is { } t ? new[] { t } : c.Enemies)
		{
			float w = v.Width;
			Vector2 above = v.Top + new Vector2(0f, -w * 0.32f), onto = v.Top + new Vector2(0f, w * 0.18f);
			Node2D cloud = CurseSmoke.Puff(c.Front, above, w * 1.6f, 1.3, squash: 0.62f, grow: 0.22f, hold: 0.45f, rim: 1.6f, dens: 0.95f);
			// 压下去：0.28～0.45s 从头顶压到身上（Puff 自己每帧设位置，这里在它后面再改）
			Fx.Animate(cloud, 0, 1.3, t =>
			{
				float s = t * 1.3f;
				float k = Mathf.Clamp((s - 0.28f) / 0.17f, 0f, 1f);
				cloud.GlobalPosition = above.Lerp(onto, k * k);
			});
			Fx.After(c.Front, 0.45, () =>
			{
				Fx.Shake(1);
				for (int i = 0; i < 3; i++)
				{
					float x = (i - 1f) * w * 0.35f;
					CurseSmoke.Puff(c.Front, v.Feet + new Vector2(x, -w * 0.04f), w * 0.55f, 0.8, squash: 0.45f, grow: 0.3f, hold: 0.2f, dens: 0.8f,
						drift: new Vector2(x * 0.8f, -w * 0.05f));
				}
			});
			CurseSmoke.Darken(c.Front, v.Tint, new Color(0.45f, 0.32f, 0.38f), 0.12f, 0.8f, 0.5f, 0.42f);
		}
	}

	/// <summary>扫腿：贴地一道红黑的弧形烟带从敌人脚下扫过去，扬起几团尘烟，轻轻一震（格挡照旧是蜂巢护盾）。</summary>
	public static void LegSweep(SkillCtx c)
	{
		Sfx.Play(Sfx.SweepLow, 0.5f);
		foreach (SkillFx.Victim v in c.Target is { } t ? new[] { t } : c.Enemies)
		{
			float w = v.Width;
			// 一串压扁的咒烟沿地面从 Joker 这侧很快扫过敌人脚下（前头那团红边最重），像一阵贴地的风
			for (int i = 0; i < 9; i++)
			{
				float k = i / 8f;
				Vector2 at = v.Feet + new Vector2(c.Face * w * Mathf.Lerp(-0.75f, 0.7f, k), -w * 0.03f);
				CurseSmoke.Puff(c.Front, at, w * Mathf.Lerp(0.5f, 0.62f, Mathf.Sin(k * Mathf.Pi)), 0.55, delay: 0.022f * i, squash: 0.32f, grow: 0.2f,
					hold: 0.1f, rim: 2.2f, dens: 0.9f, drift: new Vector2(c.Face * w * 0.25f, -w * 0.04f));
			}
			Fx.After(c.Front, 0.14, () =>
			{
				Fx.Shake(1);
				for (int i = 0; i < 4; i++)
				{
					float x = (i - 1.5f) * w * 0.28f;
					CurseSmoke.Puff(c.Front, v.Feet + new Vector2(x, -w * 0.02f), w * 0.42f, 0.7, delay: 0.03f * i, squash: 0.5f, grow: 0.3f, hold: 0.15f,
						dens: 0.75f, drift: new Vector2(c.Face * w * 0.18f, -w * 0.08f));
				}
			});
			CurseSmoke.Darken(c.Front, v.Tint, new Color(1.6f, 1.4f, 1.4f), 0.03f, 0.04f, 0.15f, 0.14f);   // 被扫中闪一下
		}
	}
}
