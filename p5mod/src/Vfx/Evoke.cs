using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 「P5 的召唤器」（2026-11-02，标杆 1）：照结城理 mod 的 EvokerShot 结构原样移植（参数、层次、时序都按那边被验证过的），只换材质：
/// 青蓝光 → 红白光 + 人格面具的蓝焰；蓝蝶 → 黑羽。用在召唤高潮那一拍（人格面具出手 = 眼神特写碎开 = 结算），从人格面具射向敌人：
///   0    贯穿光束（一帧穿到远处、尾巴往前收，留一道余辉）+ 出口玻璃龟裂 + 锥形玻璃碎片（强阻尼悬停、转到某个角度闪棱光）
///        + 喷流火星 + 斜向出口环 + 大折射波 + 闪光 + 速度线；
///   0.05 蓝焰从出口升腾、黑羽散开。
/// 顿帧 / 震屏 / 声音由调用方（SummonStage.Beat）放。
/// </summary>
public static class Evoke
{
	private static readonly Color Ice = new(1f, 0.82f, 0.82f), Red = new(1f, 0.08f, 0.14f), White = new(1f, 0.97f, 0.96f);

	public static void Fire(Control front, Control back, Vector2 muzzle, Vector2 dir, float power = 1f)
	{
		dir = dir.LengthSquared() < 0.01f ? Vector2.Right : dir.Normalized();
		Vector2 exit = muzzle + dir * 105f * power;
		Beam(front, muzzle, dir, power);
		CrackPane(front, exit, dir, power);
		ConeShatter(front, exit, dir, (int)(25 * power), power);   // 55 → 25（用户 2026-11-03：太花）
		Mist(front, exit, dir, power);
		GpuParticles2D jet = Glow.Sparks(front, exit, 30, 400f * power, 1100f * power, 0.45f, new[] { White, Red, new Color(0.4f, 0f, 0.05f) }, 0f,
			0.4f * 0.35f, 1.1f * 0.35f, dir, 22f);
		ExitRing(front, exit, dir, power);
		Glow.Ripple(back, exit, 560f * power, 0.6f, 0.04f);
		Glow.Flash(front, exit, Ice, 300f * power, 0.2f);
		Fx.After(front, 0.05, () =>
		{
			SpiritFlare(front, exit + new Vector2(0f, 40f), 360f * power, 0.9f);
			Feathers(front, exit, (int)(4 * power), 240f * power);
		});
	}

	/// <summary>贯穿光束：一帧内贯穿到远处，然后从尾部向前收束消失；一条细余辉停留更久。</summary>
	private static void Beam(Control layer, Vector2 muzzle, Vector2 dir, float power)
	{
		Node2D root = Fx.Root(layer, Vector2.Zero, "P5EvokeBeam", 0.6);
		root.GlobalPosition = Vector2.Zero;
		float len = 620f * power;
		Line2D glow = Glow.Line(root, new[] { muzzle, muzzle + dir * len }, new Color(Red, 0.7f), 34f * power);
		Line2D core = Glow.Line(root, new[] { muzzle, muzzle + dir * len }, White, 9f * power);
		Line2D after = Glow.Line(root, new[] { muzzle - dir * 20f, muzzle + dir * len * 0.85f }, new Color(Ice, 0.55f), 3f * power);
		Fx.Animate(after, 0, 0.55, t => after.Modulate = new Color(1f, 1f, 1f, 1f - Fx.OutCubic(t)));
		Fx.Animate(root, 0, 0.32, t =>
		{
			float tail = Fx.InCubic(t);
			Vector2 a = muzzle + dir * len * tail, b = muzzle + dir * len * (1f + 0.3f * t);
			glow.Points = core.Points = new[] { a, b };
			float w = 1f - t * 0.7f;
			glow.Width = 34f * power * w;
			core.Width = 9f * power * w;
		});
	}

	/// <summary>出口处的玻璃面龟裂：放射锯齿裂纹 + 两圈断续环纹，瞬间出现，随碎片飞出而淡去。</summary>
	private static void CrackPane(Control layer, Vector2 at, Vector2 dir, float power)
	{
		Node2D root = Fx.Root(layer, at, "P5EvokeCrack", 0.35);
		root.Rotation = dir.Angle();
		root.Scale = new Vector2(0.55f, 1f);
		for (int i = 0; i < 9; i++)
		{
			Vector2 d = Vector2.Right.Rotated(Mathf.Tau * i / 9 + Fx.Rand(-0.2f, 0.2f));
			float len = Fx.Rand(70f, 130f) * power;
			var pts = new Vector2[5];
			for (int k = 0; k < 5; k++) pts[k] = d * len * k / 4f + d.Orthogonal() * (k == 0 ? 0f : Fx.Rand(-8f, 8f) * power);
			root.AddChild(new Line2D { Points = pts, Width = 2.2f, DefaultColor = White, JointMode = Line2D.LineJointMode.Sharp });
		}
		for (int r = 1; r <= 2; r++)
		{
			float radius = 38f * r * power;
			for (int seg = 0; seg < 6; seg++)
			{
				float a0 = Mathf.Tau * seg / 6f + Fx.Rand(0f, 0.4f);
				var arc = new Vector2[5];
				for (int k = 0; k < 5; k++)
				{
					float a = a0 + 0.6f * k / 4f;
					arc[k] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (radius + Fx.Rand(-4f, 4f));
				}
				root.AddChild(new Line2D { Points = arc, Width = 1.6f, DefaultColor = new Color(Ice, 0.85f) });
			}
		}
		Fx.Poly(root, Fx.Circle(26f * power, 20), new Color(Red, 0.6f), additive: true);
		Fx.Animate(root, 0, 0.3, t =>
		{
			root.Modulate = new Color(2f - t, 2f - t, 2f - t, 1f - Fx.InCubic(t));
			root.Scale = new Vector2(0.55f, 1f) * Mathf.Lerp(1f, 1.25f, Ease5.OutExpo(t));
		});
	}

	/// <summary>出口的红雾：大颗柔光粒子沿出射方向缓慢扩散，低透明度。</summary>
	private static void Mist(Control layer, Vector2 at, Vector2 dir, float power)
	{
		GpuParticles2D p = Glow.Sparks(layer, at, 26, 120f * power, 380f * power, 1.2f,
			new[] { new Color(Red, 0.3f), new Color(0.5f, 0f, 0.06f, 0.2f), new Color(0.2f, 0f, 0.03f, 0f) }, -40f, 2.5f * power * 0.35f,
			5f * power * 0.35f, dir, 30f);
		if (p.ProcessMaterial is ParticleProcessMaterial pm)
		{
			pm.DampingMin = 3f;
			pm.DampingMax = 5f;
		}
	}

	/// <summary>沿 dir 锥形喷出的玻璃碎片：每 5 片 1 片大玻璃，强阻尼迅速减速后悬停、缓慢翻转，转到某个角度闪棱光，最后缩小淡出；之后留下光点。</summary>
	private static void ConeShatter(Control layer, Vector2 at, Vector2 dir, int count, float power)
	{
		Node2D root = Fx.Root(layer, at, "P5EvokeShards", 1.7);
		const float duration = 1.5f;
		for (int i = 0; i < count; i++)
		{
			bool pane = i % 5 == 0;
			float len = (pane ? Fx.Rand(34f, 60f) : Fx.Rand(8f, 24f)) * power;
			float wid = len * Fx.Rand(0.35f, 0.7f);
			Vector2[] shape = pane
				? new Vector2[] { new(-len * 0.5f, -wid * 0.2f), new(-len * 0.05f, -wid * 0.5f), new(len * 0.5f, -wid * 0.1f), new(len * 0.2f, wid * 0.5f), new(-len * 0.35f, wid * 0.35f) }
				: new Vector2[] { new(-len * 0.5f, 0f), new(-len * 0.1f, -wid * 0.5f), new(len * 0.5f, 0f), new(len * 0.05f, wid * 0.5f) };
			Node2D shard = new();
			root.AddChild(shard);
			Polygon2D body = Fx.Poly(shard, shape, new Color(Ice, pane ? 0.28f : 0.4f), additive: true);
			var loop = new Vector2[shape.Length + 1];
			shape.CopyTo(loop, 0);
			loop[^1] = shape[0];
			Line2D edge = new() { Points = loop, Width = pane ? 2f : 1.4f, DefaultColor = White, JointMode = Line2D.LineJointMode.Sharp };
			shard.AddChild(edge);
			Vector2 d = dir.Rotated(Fx.Rand(-0.75f, 0.75f));
			float speed = Fx.Rand(900f, 2000f) * power * (pane ? 0.6f : 1f);
			const float drag = 7f;
			float life = duration * Fx.Rand(0.6f, 1f);
			float spin = Fx.Rand(-10f, 10f), drift = Fx.Rand(-1f, 1f) * 0.6f, phase = Fx.Rand(0f, Mathf.Tau);
			shard.Rotation = d.Angle();
			Fx.Animate(shard, 0, life, t =>
			{
				float s = t * life;
				shard.Position = d * speed * (1f - Mathf.Exp(-drag * s)) / drag + new Vector2(0f, 35f * s * s) + d.Orthogonal() * drift * 30f * s;
				shard.Rotation = d.Angle() + spin * (1f - Mathf.Exp(-3f * s)) / 3f + drift * s;
				float glint = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(shard.Rotation * 3f + phase + s * 4f)), 10f);
				edge.Modulate = new Color(1f + glint * 1.5f, 1f + glint * 1.5f, 1f + glint * 1.5f, 1f);
				body.Modulate = new Color(1f, 1f, 1f, 0.6f + glint);
				float end = Ease5.Seg(t, 0.65f, 0.35f);
				shard.Scale = Vector2.One * Mathf.Lerp(1f, 0.15f, Fx.InCubic(end));
				shard.Modulate = new Color(1f, 1f, 1f, 1f - Fx.InCubic(end));
			});
		}
		Fx.After(layer, duration * 0.6, () =>
		{
			GpuParticles2D motes = Glow.Sparks(layer, at + dir * 140f * power, 40, 20f, 90f, 1f, new[] { White, Red, new Color(Red, 0f) }, -60f, 0.08f, 0.22f);
			if (motes.ProcessMaterial is ParticleProcessMaterial pm)
			{
				pm.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
				pm.EmissionSphereRadius = 110f * power;
			}
		});
	}

	/// <summary>斜向出口环：侧着看的一圈环，沿出射方向推出去、变细淡掉。</summary>
	private static void ExitRing(Control layer, Vector2 exit, Vector2 dir, float power)
	{
		Node2D ringRoot = Fx.Root(layer, exit + dir * 30f, "P5EvokeRing", 0.5);
		ringRoot.Rotation = dir.Angle();
		ColorRect ring = Glow.RingRect(ringRoot, 300f * power, 1f);
		ring.Scale = new Vector2(0.35f, 1f);
		Glow.SetParam(ring, "tint", Ice);
		Fx.Animate(ring, 0, 0.4, t =>
		{
			float e = Ease5.OutExpo(t);
			Glow.SetParam(ring, "radius", Mathf.Lerp(0.1f, 0.95f, e));
			Glow.SetParam(ring, "width", Mathf.Lerp(0.08f, 0.01f, e));
			Glow.SetParam(ring, "alpha", 1f - Fx.InCubic(t));
			ringRoot.GlobalPosition = exit + dir * Mathf.Lerp(20f, 140f, e);
		});
	}

	/// <summary>人格面具的蓝焰从出口升腾（同 P3 SpiritFlare：大颗柔光往上飘）。</summary>
	private static void SpiritFlare(Control layer, Vector2 at, float height, float duration)
	{
		GpuParticles2D flame = Glow.Sparks(layer, at, 70, 80f, 260f, duration * 0.8f,
			new[] { PersonaMask.Hot, PersonaMask.Hi, PersonaMask.Mid, new Color(PersonaMask.Dark, 0f) }, -height * 1.4f, 0.6f * 0.35f, 1.6f * 0.35f);
		if (flame.ProcessMaterial is ParticleProcessMaterial pm)
		{
			pm.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
			pm.EmissionBoxExtents = new Vector3(70f, 12f, 0f);
			pm.Spread = 25f;
		}
		flame.Explosiveness = 0.6f;
	}

	/// <summary>黑羽（亚森的）：从中心向外飘开，翻转着慢慢落下，红色描边，错开出现。</summary>
	private static void Feathers(Control layer, Vector2 at, int count, float radius)
	{
		for (int i = 0; i < count; i++)
		{
			float delay = i * 0.04f;
			Fx.After(layer, delay, () =>
			{
				Node2D f = Fx.Root(layer, at, "P5Feather", 1.7);
				float l = Fx.Rand(26f, 44f), w = l * 0.28f;
				Vector2[] shape = { new(-l * 0.5f, 0f), new(-l * 0.2f, -w * 0.5f), new(l * 0.3f, -w * 0.45f), new(l * 0.5f, 0f), new(l * 0.3f, w * 0.4f), new(-l * 0.2f, w * 0.45f) };
				Fx.Poly(f, Ink.Scale(shape, 1f, 2.5f), new Color(0.9f, 0.08f, 0.14f, 0.9f));
				Fx.Poly(f, shape, new Color(0.04f, 0.01f, 0.03f));
				f.AddChild(new Line2D { Points = new[] { new Vector2(-l * 0.55f, 0f), new Vector2(l * 0.45f, 0f) }, Width = 1.2f, DefaultColor = new Color(0.6f, 0.08f, 0.12f) });
				Vector2 target = at + Vector2.Right.Rotated(Fx.Rand(0f, Mathf.Tau)) * radius * Fx.Rand(0.5f, 1f);
				float spin = Fx.Rand(-6f, 6f), sway = Fx.Rand(0f, Mathf.Tau);
				Fx.Animate(f, 0, 1.6, t =>
				{
					float e = Fx.OutCubic(Mathf.Min(1f, t * 2f));
					f.GlobalPosition = at.Lerp(target, e) + new Vector2(Mathf.Sin(t * 9f + sway) * 14f, 120f * t * t);
					f.Rotation = spin * t + Mathf.Sin(t * 6f + sway) * 0.6f;
					f.Scale = new Vector2(Mathf.Abs(Mathf.Cos(t * 7f + sway)) * 0.7f + 0.3f, 1f);
					f.Modulate = new Color(1f, 1f, 1f, Mathf.Min(1f, t * 8f) * (1f - Fx.InCubic(Ease5.Seg(t, 0.6f, 0.4f))));
				});
			});
		}
	}
}
