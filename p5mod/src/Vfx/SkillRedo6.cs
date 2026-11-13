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
					Vortex(c.Front, back, c.Feet, h, h * 0.45f, 4, 0f, 0.55f, f, 0.25f, 0.6f, 0.9f);
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
		for (int i = 0; i < 10; i++)
		{
			float x = c.Feet.X + h * Fx.Rand(-0.28f, 0.28f);
			Vector2 a = new(x, c.Feet.Y - h * Fx.Rand(0f, 0.15f)), b2 = new(x + Fx.Rand(-0.05f, 0.05f) * h, c.Feet.Y - h * Fx.Rand(0.95f, 1.35f));
			bool white = i % 3 == 1;
			DashFly(i % 2 == 0 ? c.Front : back, t => a.Lerp(b2, t), 0.1f + Fx.Rand(0f, 0.2f), Fx.Rand(0.22f, 0.32f), h * Fx.Rand(0.4f, 0.7f),
				h * (white ? 0.02f : 0.045f), white ? new Color(1f, 0.85f, 0.85f) : new Color(0.95f, 0.06f, 0.15f), white ? 1.4f : 1f);
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
		Glitter(c.Front, c.Feet, h, u, 0.35f, 0.5f, 4);
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
