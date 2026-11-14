using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 重做批（2026-11-13，用户：刀刃陷阱太简单；肾上腺素改过一次还是不满意；暗影步要更好看；幽魂形态重做；计划妥当太简单）。
/// 和影刃旋风同一套语言：圆润的柔光、疾光、刀光旋涡、网点光晕；没有细线圈、没有尖碎片。
/// </summary>
public static partial class SkillRedo5
{
	/// <summary>
	/// 肾上腺素（获得能量 + 抽 2），「心跳涌动」约 1.1s：
	/// ① 0 / 0.26 两下心跳：每一拍他身上往外扩一层红剪影脉冲、身后漾开网点光晕、人亮一下；第二拍更强、轻震；
	/// ② 两拍之间红白疾光从脚下沿身体往上冲，四周的柔光粒往胸口汇聚（能量），汇到时胸口一闪、一圈刀光绕腰转起来往上卷；
	/// ③ 0.5 起两张牌从胸口弹出、飞进手牌区。
	/// 拍子：咚（轻）→ 咚（主拍）→ 抽牌。
	/// </summary>
	public static void Adrenaline(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		Control back = c.Back ?? c.Front;
		S("gx_pop", 0.14f, 0.02f);
		S("gx_pop", 0.3f, 0.28f);
		for (int b = 0; b < 2; b++)
		{
			int bb = b;
			float d = 0.02f + 0.26f * b;
			Fx.After(c.Front, d, () =>
			{
				float k = bb == 0 ? 0.7f : 1f;
				Glow.Flash(c.Front, c.Chest, new Color(1f, 0.92f, 0.9f), h * 0.35f * k, 0.13f);
				Glow.Flash(back, c.Chest, new Color(1f, 0.12f, 0.2f), h * 1.4f * k, 0.3f);
				HalftoneBurst(back, c.Chest, h * 1.7f * k, 0.42f);
				if (bb == 1)
				{
					Fx.Shake(1);
					Cross(c.Front, c.Chest, h * 0.9f);
					Vortex(c.Front, back, c.Feet, h, h * 0.55f, 6, 0f, 0.65f, f, 0.15f, 0.75f, 1.3f, 0.25f);
				}
			});
			// 剪影脉冲：一层红剪影从身上往外扩、淡掉
			Fx.After(c.Front, d, () =>
			{
				if (RedEcho(c, 0.4) is not { } echo) return;
				var (sp, m) = echo;
				Vector2 pivot = c.Chest - c.Region.Position;
				float k = bb == 0 ? 0.06f : 0.1f;
				Fx.Animate(sp, 0, 0.36, t =>
				{
					float e = Fx.OutCubic(t);
					float sc = 1f + k * e;
					sp.Scale = Vector2.One * sc;
					sp.Position = pivot * (1f - sc);
					m.SetShaderParameter("fill", 0.35f);
					m.SetShaderParameter("smear", 0f);
					m.SetShaderParameter("alpha", 0.8f * (1f - t));
				});
			});
			SkillRedo.Pulse(c, new Color(1.6f, 0.62f, 0.64f), d, 0.28f);
		}
		// 疾光往上冲
		for (int i = 0; i < 16; i++)
		{
			float x = c.Feet.X + h * Fx.Rand(-0.34f, 0.34f);
			Vector2 a = new(x, c.Feet.Y - h * Fx.Rand(0f, 0.15f)), b2 = new(x + Fx.Rand(-0.05f, 0.05f) * h, c.Feet.Y - h * Fx.Rand(0.95f, 1.35f));
			bool white = i % 3 == 1;
			DashFly(i % 2 == 0 ? c.Front : back, t => a.Lerp(b2, t), 0.1f + Fx.Rand(0f, 0.2f), Fx.Rand(0.22f, 0.32f), h * Fx.Rand(0.4f, 0.7f),
				h * (white ? 0.04f : 0.09f), white ? new Color(1f, 0.85f, 0.85f) : new Color(0.95f, 0.06f, 0.15f), white ? 1.4f : 1f);
		}
		// 柔光粒往胸口汇聚（能量）
		for (int i = 0; i < 9; i++)
		{
			Vector2 from = c.Chest + Vector2.FromAngle(Mathf.Tau * i / 9f + Fx.Rand(-0.2f, 0.2f)) * h * Fx.Rand(0.5f, 0.75f);
			float d0 = 0.08f + 0.015f * i;
			Node2D pr = Fx.Root(i % 2 == 0 ? c.Front : back, from, "P5AdrenalineMote", d0 + 0.3);
			Sprite2D g = Glow.SoftSprite(pr, Vector2.Zero, new Color(1f, 0.2f, 0.28f, 0f), h * 0.14f / 32f);
			Sprite2D cc = Glow.SoftSprite(pr, Vector2.Zero, new Color(1f, 0.92f, 0.9f, 0f), h * 0.05f / 32f);
			pr.Visible = false;
			Fx.Animate(pr, d0, 0.22, t =>
			{
				pr.Visible = true;
				float e = Fx.InCubic(t);
				pr.GlobalPosition = from.Lerp(c.Chest, e);
				float a = Mathf.Sin(Mathf.Pi * Mathf.Min(1f, t * 1.1f));
				g.Modulate = new Color(1f, 0.2f, 0.28f, 0.85f * a);
				cc.Modulate = new Color(1f, 0.92f, 0.9f, a);
			});
		}
		Glitter(c.Front, c.Feet, h, u, 0.3f, 0.7f, 8, 0.45f);
		// 身后一根柔光气柱：随两拍心跳鼓起来，然后往上散掉
		Node2D col = Fx.Root(back, c.Feet, "P5AdrenalineAura", 1.2);
		Sprite2D aw = Glow.SoftSprite(col, new Vector2(0f, -h * 0.6f), new Color(1f, 0.1f, 0.18f, 0f), 1f);
		Sprite2D ac = Glow.SoftSprite(col, new Vector2(0f, -h * 0.55f), new Color(1f, 0.75f, 0.75f, 0f), 1f);
		Fx.Animate(col, 0, 1.1, t =>
		{
			float s = t * 1.1f;
			float beat = Mathf.Exp(-Mathf.Max(0f, s - 0.02f) * 9f) * 0.6f * (s >= 0.02f ? 1f : 0f) + Mathf.Exp(-Mathf.Max(0f, s - 0.28f) * 6f) * (s >= 0.28f ? 1f : 0f);
			float a = Mathf.Min(1f, s / 0.06f) * (1f - Ease5.Seg(s, 0.6f, 0.5f));
			float rise = Fx.OutCubic(Ease5.Seg(s, 0.28f, 0.8f));
			aw.Scale = new Vector2(h * (0.75f + 0.35f * beat), h * (1.5f + 0.5f * beat + 0.4f * rise)) / 32f;
			aw.Position = new Vector2(0f, -h * (0.6f + 0.2f * rise));
			aw.Modulate = new Color(1f, 0.1f, 0.18f, (0.35f + 0.45f * beat) * a);
			ac.Scale = new Vector2(h * (0.3f + 0.15f * beat), h * (1.1f + 0.3f * beat)) / 32f;
			ac.Modulate = new Color(1f, 0.75f, 0.75f, 0.35f * beat * a);
		});
		// 抽 2
		for (int i = 0; i < 2; i++)
		{
			int k = i;
			float d = 0.5f + 0.07f * k;
			Vector2 apex = c.Chest + new Vector2(f * h * (0.3f + 0.15f * k), -h * (0.4f - 0.1f * k));
			Phantom.P5Card card = Phantom.Card(c.Front, c.Chest, SkillRedo.CardW(c), d + 0.2, 0.7f);
			card.Root.Visible = false;
			Fx.Animate(card.Root, d, 0.16, t =>
			{
				card.Root.Visible = true;
				card.Place(c.Chest.Lerp(apex, Ease5.OutQuint(t)), f * 0.3f, t * 6f, 0.7f + 0.3f * t);
			});
			Fx.After(c.Front, d + 0.16f, () => { card.Trail?.Kill(); card.Root.Visible = false; });
			SkillRedo.ToHand(c, apex, d + 0.16f, 0.42f, f * 0.3f, h * 0.15f);
		}
		S("gx_card_stack", 0.16f, 0.57f + 0.16f + 0.42f);
	}
}

public static partial class SkillRedo5
{
	/// <summary>
	/// 暗影步（弃掉手牌，下回合攻击伤害翻倍），「赤影换步」约 1.3s（2026-11-14 重做：去掉细圈、尖碎片）：
	/// ① 0 三张牌从手里甩出去、在半空烧成纸屑；
	/// ② 0.2 主拍：周围一下压暗，他「唰」地往旁边错一步 —— 原地留下一层红残影被拽成一道往后拖的影子，
	///    两道交叉的大弧光绕身扫过、身后网点光晕炸开、一排横向疾光往后掠、他身上暗一下再回来、轻震；
	/// ③ 0.6 蓄力（下回合翻倍）：四周光粒往握刀的手上汇，手上一团红光越聚越亮，一小圈刀光绕着前臂转起来收紧，
	///    最后手上一闪、十字光、面具一亮（叮）。
	/// </summary>
	public static void ShadowStepV2(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face, w = SkillRedo.CardW(c);
		Control back = c.Back ?? c.Front;
		const float step = 0.2f, charge = 0.45f;
		S("gx_card_slide", 0.16f);
		S("gx_slice", 0.3f, step);
		S("gx_ping", 0.14f, charge + 0.3f);
		// ① 甩牌
		for (int i = 0; i < 3; i++)
		{
			int k = i;
			float ang = Mathf.Lerp(-0.8f, 0.15f, k / 2f);
			Vector2 to = c.Hand + new Vector2(Mathf.Cos(ang) * f, Mathf.Sin(ang)) * h * 0.6f;
			Phantom.P5Card card = Phantom.Card(c.Front, c.Hand, w, 0.9, 0.6f);
			float d = 0.03f * k;
			card.Root.Visible = false;
			Fx.Animate(card.Root, d, 0.22, t =>
			{
				card.Root.Visible = true;
				card.Place(c.Hand.Lerp(to, Ease5.OutQuint(t)), f * t * 3f, Mathf.Pi + t * 12f, 1f);
			});
			Fx.After(c.Front, d + 0.22f, () => SkillRedo.BurnAway(c, card, to, f * 0.66f, new Vector2(f * h * 0.08f, -h * 0.06f)));
		}
		// ② 错步
		Dim(back, c.Chest, new Vector2(h * 2.4f, h * 1.9f), 0.9, s => Fx.OutCubic(Ease5.Seg(s, step - 0.05f, 0.1f)) * (1f - Ease5.Seg(s, 0.55f, 0.35f)) * 0.8f);
		Fx.After(c.Front, step, () =>
		{
			Glow.Flash(c.Front, c.Chest, new Color(1f, 0.93f, 0.92f), h * 0.6f, 0.11f);
			Glow.Flash(back, c.Chest, new Color(0.9f, 0.08f, 0.15f), h * 1.8f, 0.3f);
			HalftoneBurst(back, c.Chest - new Vector2(f * h * 0.15f, 0f), h * 2f, 0.45f);
			HitStop.Request(0.04);
			Fx.Shake(1);
			Glow.Sparks(c.Front, c.Chest, 18, 120f * u, 520f * u, 0.4f, new[] { new Color(1f, 0.96f, 0.94f), new Color(1f, 0.22f, 0.3f) }, 80f * u,
				0.015f * u, 0.04f * u, new Vector2(-f, -0.2f), 50f);
			if (RedEcho(c, 0.6) is { } echo)
			{
				var (sp, m) = echo;
				Fx.Animate(sp, 0, 0.6, t =>
				{
					float e = Ease5.OutQuint(t);
					sp.Position = new Vector2(-f * h * 0.14f * e, 0f);
					m.SetShaderParameter("smear", f * 0.22f * (1f - e * 0.5f));
					m.SetShaderParameter("flow", t * 5f);
					m.SetShaderParameter("fill", 0.55f);
					m.SetShaderParameter("alpha", 0.85f * Mathf.Min(1f, t / 0.05f) * (1f - Ease5.Seg(t, 0.4f, 0.6f)));
				});
			}
		});
		Sweep(c.Front, back, c.Chest, h * 0.55f, 0.5f * f, f, step, 0.42f, 0.08f, new Color(0.95f, 0.06f, 0.15f), 1.2f, 0.85f);
		Sweep(c.Front, back, c.Chest + new Vector2(0f, h * 0.06f), h * 0.5f, -0.45f * f, -f, step + 0.04f, 0.4f, 0.035f, new Color(1f, 0.85f, 0.85f), 1.6f, 0.8f);
		for (int i = 0; i < 7; i++)
		{
			float y = c.Feet.Y - h * (0.12f + 0.13f * i + Fx.Rand(-0.04f, 0.04f));
			Vector2 a = new(c.Feet.X + f * h * 0.1f, y), b = new(c.Feet.X - f * h * Fx.Rand(0.7f, 1.1f), y);
			bool white = i % 3 == 1;
			DashFly(i % 2 == 0 ? back : c.Front, t => a.Lerp(b, t), step + Fx.Rand(0f, 0.08f), Fx.Rand(0.22f, 0.3f), h * Fx.Rand(0.4f, 0.65f),
				h * (white ? 0.035f : 0.08f), white ? new Color(1f, 0.85f, 0.85f) : new Color(0.95f, 0.06f, 0.15f), white ? 1.4f : 1f);
		}
		SkillRedo.Pulse(c, new Color(0.3f, 0.2f, 0.28f), step, 0.4f);
		// ③ 手上蓄力
		Vector2 hand = c.Hand;
		for (int i = 0; i < 10; i++)
		{
			Vector2 from = hand + Vector2.FromAngle(Mathf.Tau * i / 10f + Fx.Rand(-0.2f, 0.2f)) * h * Fx.Rand(0.4f, 0.6f);
			float d0 = charge - 0.08f + 0.015f * i;
			Node2D pr = Fx.Root(i % 2 == 0 ? c.Front : back, from, "P5StepMote", d0 + 0.3);
			Sprite2D g = Glow.SoftSprite(pr, Vector2.Zero, new Color(1f, 0.2f, 0.28f, 0f), h * 0.12f / 32f);
			Sprite2D cc = Glow.SoftSprite(pr, Vector2.Zero, new Color(1f, 0.92f, 0.9f, 0f), h * 0.045f / 32f);
			pr.Visible = false;
			Fx.Animate(pr, d0, 0.24, t =>
			{
				pr.Visible = true;
				pr.GlobalPosition = from.Lerp(hand, Fx.InCubic(t));
				float a = Mathf.Sin(Mathf.Pi * Mathf.Min(1f, t * 1.1f));
				g.Modulate = new Color(1f, 0.2f, 0.28f, 0.85f * a);
				cc.Modulate = new Color(1f, 0.92f, 0.9f, a);
			});
		}
		Node2D orb = Fx.Root(c.Front, hand, "P5StepOrb", charge + 0.9);
		Sprite2D og = Glow.SoftSprite(orb, Vector2.Zero, new Color(1f, 0.12f, 0.2f, 0f), 1f);
		Sprite2D oc = Glow.SoftSprite(orb, Vector2.Zero, new Color(1f, 0.9f, 0.88f, 0f), 1f);
		Fx.Animate(orb, charge, 0.85, t =>
		{
			float s = t * 0.85f;
			float gather = Fx.OutCubic(Mathf.Min(1f, s / 0.3f));
			float pop = s > 0.3f ? Mathf.Exp(-(s - 0.3f) * 7f) : 0f;
			float a = 1f - Ease5.Seg(s, 0.36f, 0.22f);
			og.Scale = Vector2.One * h * (0.15f + 0.25f * gather + 0.3f * pop) / 32f;
			og.Modulate = new Color(1f, 0.12f, 0.2f, (0.4f + 0.4f * gather) * a);
			oc.Scale = Vector2.One * h * (0.05f + 0.08f * gather + 0.12f * pop) / 32f;
			oc.Modulate = new Color(1f, 0.9f, 0.88f, (0.3f + 0.7f * gather) * a);
		});
		Vortex(c.Front, back, hand + new Vector2(0f, h * 0.12f), h * 0.4f, h * 0.17f, 4, charge + 0.02f, 0.38f, f, 0.05f, 0.55f, 0.8f, -0.1f);
		Fx.After(c.Front, charge + 0.3f, () =>
		{
			Glow.Flash(c.Front, hand, new Color(1f, 0.95f, 0.92f), h * 0.4f, 0.13f);
			Glow.Flash(back, hand, new Color(0.95f, 0.1f, 0.16f), h * 1.1f, 0.28f);
			HalftoneBurst(back, hand, h * 1.1f, 0.38f);
			Cross(c.Front, hand, h * 0.8f);
			Glow.Glint(c.Front, c.Mask + new Vector2(f * h * 0.03f, 0f), 0.9f);
			Glow.Sparks(c.Front, hand, 14, 80f * u, 300f * u, 0.45f, new[] { new Color(1f, 0.96f, 0.94f), new Color(1f, 0.22f, 0.3f) }, -60f * u,
				0.015f * u, 0.04f * u, Vector2.Up, 180f);
		});
		Glitter(c.Front, c.Feet, h, u, charge + 0.4f, 0.4f, 4, 0.35f);
	}
}

public static partial class SkillRedo5
{
	/// <summary>
	/// 幽魂形态（获得无实体），「怪盗幻影」约 1.6s（2026-11-14 重做：去掉紫色漩涡、细圈和亡魂贴图）：
	/// ① 0 四周压暗，他整个人褪成半透明的淡红白（像幻影），身后一根柔光气柱慢慢升起；
	/// ② 0.1 起三层红残影从他身上剥离、往左 / 右 / 后上方飘开（拖着横向影子），一大圈刀光绕着他往上卷、光粒往上飘；
	/// ③ 0.75 主拍：三层残影「唰」地合回他身上 —— 白红闪光、超大网点光晕、十字光芒、两道交叉大弧光、碎光四散、重震；
	/// ④ 之后人慢慢恢复颜色，身上还闪两下幻影的抖动。
	/// </summary>
	public static void WraithFormV2(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		Control back = c.Back ?? c.Front;
		const float snap = 0.75f, life = 1.6f;
		Sfx.Play(Sfx.WraithForm, 0.6f);
		S("gx_slice", 0.22f, snap - 0.04f);
		Dim(back, c.Chest, new Vector2(h * 3.2f, h * 2.4f), life, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.25f)) * (1f - Ease5.Seg(s, 1.1f, 0.5f)));
		// 人褪成幻影
		if (c.SelfTint is { } tint)
		{
			Node2D tick = Fx.Root(c.Front, c.Feet, "P5WraithTint", life + 0.05);
			Color ghost = new(1f, 0.72f, 0.76f, 0.45f);
			Fx.Animate(tick, 0, life, t =>
			{
				float s = t * life;
				float k = Ease5.Seg(s, 0f, 0.25f) * (1f - 0.6f * Ease5.Seg(s, snap, 0.08f)) * (1f - Ease5.Seg(s, snap + 0.2f, 0.6f));
				float jit = s > snap ? 0.25f * Mathf.Max(0f, Mathf.Sin(s * 40f)) * (1f - Ease5.Seg(s, snap, 0.5f)) : 0.1f * Mathf.Sin(s * 30f);
				tint(Colors.White.Lerp(ghost, Mathf.Clamp(k + jit * k, 0f, 1f)));
			});
			tick.TreeExiting += () => tint(Colors.White);
		}
		// 身后柔光气柱
		Node2D col = Fx.Root(back, c.Feet, "P5WraithAura", life + 0.05);
		Sprite2D aw = Glow.SoftSprite(col, new Vector2(0f, -h * 0.6f), new Color(1f, 0.1f, 0.18f, 0f), 1f);
		Fx.Animate(col, 0, life, t =>
		{
			float s = t * life;
			float pop = s > snap ? Mathf.Exp(-(s - snap) * 5f) : 0f;
			float a = Fx.OutCubic(Ease5.Seg(s, 0f, 0.4f)) * (1f - Ease5.Seg(s, 1.0f, 0.6f));
			aw.Scale = new Vector2(h * (0.8f + 0.5f * pop), h * (1.4f + 0.4f * Ease5.Seg(s, 0f, snap) + 0.5f * pop)) / 32f;
			aw.Position = new Vector2(0f, -h * (0.6f + 0.15f * Ease5.Seg(s, 0f, snap)));
			aw.Modulate = new Color(1f, 0.1f, 0.18f, (0.4f + 0.4f * pop) * a);
		});
		// 三层残影剥离、飘开、合回
		Vector2[] dirs = { new(-f, -0.05f), new(f, -0.1f), new(-0.25f * f, -0.6f) };
		for (int i = 0; i < 3; i++)
		{
			Vector2 dv = dirs[i].Normalized() * h * (i == 2 ? 0.22f : 0.3f);
			float d0 = 0.08f + 0.06f * i;
			Fx.After(c.Front, d0, () =>
			{
				if (RedEcho(c, life) is not { } echo) return;
				var (sp, m) = echo;
				float dur = life - d0;
				Fx.Animate(sp, 0, dur, t =>
				{
					float s = d0 + t * dur;
					float out_ = Ease5.OutQuint(Ease5.Seg(s, d0, 0.4f));
					float back_ = Fx.InCubic(Ease5.Seg(s, snap - 0.12f, 0.12f));
					float k = out_ * (1f - back_);
					sp.Position = dv * k + new Vector2(0f, Mathf.Sin(s * 5f + d0 * 9f) * h * 0.01f * k);
					m.SetShaderParameter("smear", -Mathf.Sign(dv.X == 0f ? 1f : dv.X) * 0.07f * k + (s > snap - 0.12f && s < snap ? Mathf.Sign(dv.X) * 0.1f * back_ : 0f));
					m.SetShaderParameter("flow", s * 5f);
					m.SetShaderParameter("fill", 0.35f);
					m.SetShaderParameter("alpha", 0.7f * Mathf.Min(1f, (s - d0) / 0.06f) * (s < snap ? 1f : 0f));
				});
			});
		}
		Vortex(c.Front, back, c.Feet, h, h * 0.6f, 8, 0.1f, snap - 0.05f, f, 0.05f, 0.9f, 1.2f, 0.3f);
		Glitter(c.Front, c.Feet, h, u, 0.1f, 1.2f, 10, 0.5f);
		// 合回：主拍
		Fx.After(c.Front, snap, () =>
		{
			Glow.Flash(c.Front, c.Chest, new Color(1f, 0.95f, 0.93f), h * 0.9f, 0.16f);
			Glow.Flash(back, c.Chest, new Color(0.9f, 0.08f, 0.15f), h * 2.6f, 0.4f);
			HalftoneBurst(back, c.Chest, h * 2.8f, 0.6f);
			HalftoneBurst(back, c.Chest, h * 1.6f, 0.4f, 0.05f, new Color(1f, 0.3f, 0.36f));
			Cross(c.Front, c.Chest, h * 1.3f);
			HitStop.Request(0.05);
			Fx.Shake(2);
			Glow.Sparks(c.Front, c.Chest, 40, 160f * u, 700f * u, 0.55f, new[] { new Color(1f, 0.96f, 0.94f), new Color(1f, 0.22f, 0.3f), new Color(0.5f, 0.02f, 0.08f) },
				120f * u, 0.02f * u, 0.05f * u, Vector2.Up, 180f);
		});
		Sweep(c.Front, back, c.Chest, h * 0.75f, 0.4f, f, snap, 0.5f, 0.09f, new Color(0.95f, 0.06f, 0.15f), 1.2f, 1f);
		Sweep(c.Front, back, c.Chest, h * 0.7f, -0.4f, -f, snap + 0.04f, 0.48f, 0.04f, new Color(1f, 0.85f, 0.85f), 1.6f, 0.95f);
	}
}

public static partial class SkillRedo5
{
	/// <summary>
	/// 计划妥当（回合结束保留手牌），「作战图」约 1.3s（2026-11-14 重做：原来太简单）：
	/// ① 0 五张牌从手里迸出、转着排成他身前上方一道大扇面，每张落位时一闪、网点光晕、碎光；扇面后面一层柔光；
	/// ② 0.45 一道大弧光从扇面里扫过，牌从左到右依次亮一下（叮）—— 计划定了；
	/// ③ 0.78 五张牌一齐收拢叠成一摞回到手上：一摞牌一闪、十字光、网点光晕、一小圈刀光绕着它转，然后缩进手里。
	/// </summary>
	public static void WellLaidPlans(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face, w = SkillRedo.CardW(c) * 1.15f;
		Control back = c.Back ?? c.Front;
		const int n = 5;
		const float lit = 0.36f, fold = 0.6f, life = 1.1f;
		S("gx_card_slide", 0.16f);
		S("gx_ping", 0.14f, lit + 0.1f);
		S("gx_card_stack", 0.2f, fold + 0.14f);
		Vector2 hub = c.Chest + new Vector2(f * h * 0.12f, h * 0.12f);
		Vector2 stack = c.Hand + new Vector2(f * h * 0.08f, -h * 0.05f);
		// 扇面后的柔光
		Node2D glow = Fx.Root(back, hub + new Vector2(0f, -h * 0.5f), "P5PlanGlow", life);
		Sprite2D gs = Glow.SoftSprite(glow, Vector2.Zero, new Color(1f, 0.1f, 0.18f, 0f), 1f);
		gs.Scale = new Vector2(h * 1.5f, h * 0.8f) / 32f;
		Fx.Animate(glow, 0, life - 0.05, t =>
		{
			float s = t * (life - 0.05f);
			float pulse = s > lit ? Mathf.Exp(-(s - lit) * 6f) : 0f;
			float a = Fx.OutCubic(Ease5.Seg(s, 0.05f, 0.3f)) * (1f - Ease5.Seg(s, fold, 0.3f));
			gs.Modulate = new Color(1f, 0.1f, 0.18f, (0.35f + 0.35f * pulse) * a);
		});
		for (int i = 0; i < n; i++)
		{
			int k = i;
			float ang = -Mathf.Pi / 2f + f * Mathf.Lerp(-0.85f, 0.85f, k / (n - 1f));
			Vector2 slot = hub + Vector2.FromAngle(ang) * h * 0.62f;
			float tilt = ang + Mathf.Pi / 2f;
			float d = 0.03f + 0.05f * k;
			float flyIn = 0.24f;
			Phantom.P5Card card = Phantom.Card(c.Front, c.Hand, w, life, 0.6f);
			card.Root.Visible = false;
			Vector2 ctl = (c.Hand + slot) / 2f + new Vector2(-f * h * 0.15f, -h * 0.2f);
			float litAt = lit + 0.05f * k;
			Fx.Animate(card.Root, d, life - d - 0.02f, t =>
			{
				float s = d + t * (life - d - 0.02f);
				card.Root.Visible = true;
				float a = s - d;
				Vector2 pos;
				float rot, flip, sc;
				if (s < fold)
				{
					float e = Ease5.OutQuint(Mathf.Min(1f, a / flyIn));
					pos = c.Hand.Lerp(ctl, e).Lerp(ctl.Lerp(slot, e), e) + new Vector2(0f, Mathf.Sin(s * 3f + k) * h * 0.012f);
					rot = Mathf.Lerp(0f, tilt, e);
					flip = (1f - e) * Mathf.Tau * 1.5f;
					sc = 0.5f + 0.5f * e;
				}
				else
				{
					float e = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, (s - fold) / 0.16f));
					pos = slot.Lerp(stack + new Vector2(0f, -k * h * 0.008f), e);
					rot = Mathf.Lerp(tilt, f * 0.2f, e);
					flip = 0f;
					float sh = Ease5.Seg(s, fold + 0.24f, 0.12f);
					sc = Mathf.Lerp(1f, 0.85f, e) * (1f - 0.9f * Fx.InCubic(sh));
				}
				card.Place(pos, rot, flip, sc);
				float glowK = (s > litAt ? Mathf.Exp(-(s - litAt) * 9f) : 0f) + Mathf.Exp(-a * 10f) * 0.6f;
				card.Set("glow", glowK);
				card.Set("sheen", s > litAt ? Mathf.Min(1f, (s - litAt) * 4f) * 2f - 1f : -1f);
				card.Set("alpha", 1f - Ease5.Seg(s, fold + 0.33f, 0.05f));
			});
			Fx.After(c.Front, d + flyIn * 0.7f, () =>
			{
				Glow.Flash(c.Front, slot, new Color(1f, 0.93f, 0.92f), w * 1.6f, 0.11f);
				HalftoneBurst(back, slot, w * 3.2f, 0.32f);
				Glow.Sparks(c.Front, slot, 8, 60f * u, 220f * u, 0.35f, new[] { new Color(1f, 0.96f, 0.94f), new Color(1f, 0.22f, 0.3f) }, 40f * u,
					0.012f * u, 0.03f * u, Vector2.Up, 180f);
			});
			Fx.After(c.Front, litAt, () => Glow.Flash(back, slot, new Color(0.95f, 0.1f, 0.16f), w * 2.4f, 0.22f));
			Fx.After(c.Front, d + flyIn + 0.04f, () => card.Trail?.Kill());
		}
		// 一道疾光沿着扇面扫过去（跟牌一样的弧）
		float a0 = -Mathf.Pi / 2f - f * 1.05f, a1 = -Mathf.Pi / 2f + f * 1.05f;
		DashFly(c.Front, t => hub + Vector2.FromAngle(Mathf.Lerp(a0, a1, t)) * h * 0.62f, lit - 0.02f, 0.3f, h * 0.8f, h * 0.15f,
			new Color(0.95f, 0.06f, 0.15f), 1.3f, Ease5.OutQuint, 0.7f);
		DashFly(c.Front, t => hub + Vector2.FromAngle(Mathf.Lerp(a0, a1, t)) * h * 0.62f, lit, 0.3f, h * 0.65f, h * 0.06f,
			new Color(1f, 0.88f, 0.88f), 1.8f, Ease5.OutQuint, 0.7f);
		DashFly(back, t => hub + Vector2.FromAngle(Mathf.Lerp(a0, a1, t)) * h * 0.78f, lit + 0.03f, 0.34f, h * 0.6f, h * 0.1f,
			new Color(0.95f, 0.06f, 0.15f), 1f, Ease5.OutQuint, 0.7f);
		// 收拢成一摞
		Fx.After(c.Front, fold + 0.16f, () =>
		{
			Glow.Flash(c.Front, stack, new Color(1f, 0.95f, 0.92f), h * 0.45f, 0.13f);
			Glow.Flash(back, stack, new Color(0.9f, 0.08f, 0.15f), h * 1.3f, 0.3f);
			HalftoneBurst(back, stack, h * 1.4f, 0.42f);
			Cross(c.Front, stack, h * 0.75f);
			Fx.Shake(1);
			Glow.Sparks(c.Front, stack, 16, 100f * u, 380f * u, 0.45f, new[] { new Color(1f, 0.96f, 0.94f), new Color(1f, 0.22f, 0.3f) }, -40f * u,
				0.015f * u, 0.04f * u, Vector2.Up, 180f);
		});
		Vortex(c.Front, back, stack + new Vector2(0f, h * 0.1f), h * 0.4f, h * 0.18f, 4, fold + 0.12f, 0.4f, f, 0.1f, 0.4f, 0.8f, 0.05f);
		Glitter(c.Front, c.Feet, h, u, 0.2f, 0.9f, 6, 0.45f);
	}
}

public static partial class SkillRedo5
{
	/// <summary>刀刃陷阱的笼子：几道细刀光绕着敌人一直转（不往上卷），t0 起 dur 秒。</summary>
	public static void TrapCage(Control front, Control back, Vector2 feet, float h, float radius, float t0, float dur) =>
		Vortex(front, back, feet, h, radius, 6, t0, dur, 1f, 0.15f, 0.9f, 1.3f, 0f);
}

public static partial class SkillRedo5
{
	/// <summary>一根悬停的疾光（Dash 着色器：UV.x 0 尾 → 1 头）。返回节点（父：layer）和材质；位置 / 方向 / 长度每帧自己设（Scale = (长, 粗)）。</summary>
	public static (Node2D Root, ShaderMaterial M) Needle(Control layer, Color tint, float hot, double life)
	{
		Node2D root = Fx.Root(layer, Vector2.Zero, "P5Needle", life);
		ShaderMaterial m = new() { Shader = Dash };
		m.SetShaderParameter("tint", tint);
		m.SetShaderParameter("hot", hot);
		m.SetShaderParameter("seed", Fx.Rand(0f, 9f));
		m.SetShaderParameter("alpha", 0f);
		root.AddChild(new ColorRect { Size = new Vector2(1f, 1f), Position = new Vector2(-1f, -0.5f), Material = m, MouseFilter = Control.MouseFilterEnum.Ignore });
		return (root, m);
	}

	/// <summary>一块压暗透红（乘法，椭圆软边）：k(t) 0 → 1 控制强度，size 直径。</summary>
	public static void Dim(Control layer, Vector2 center, Vector2 size, double life, Func<float, float> k)
	{
		Node2D gr = Fx.Root(layer, center, "P5Dim", life + 0.1);
		ShaderMaterial gm = new() { Shader = Grade };
		gr.AddChild(new ColorRect { Size = size, Position = -size / 2f, Material = gm, MouseFilter = Control.MouseFilterEnum.Ignore });
		Fx.Animate(gr, 0, life, t => gm.SetShaderParameter("k", k(t * (float)life)));
	}
}
