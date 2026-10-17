using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace P3Makoto.Vfx;

/// <summary>
/// 召唤器扣扳机：强确认感 + "子弹贯穿"。
/// 时间轴（游戏时间）：
///   0     枪口闪光 + 贯穿光束（从枪口沿枪管方向穿过头部，射向远处）+ 全屏白闪 + 顿帧 70ms（真实时间）+ 震屏
///   0     出口（头部另一侧）：玻璃碎片沿枪管方向锥形喷出、蓝色灵焰喷流、斜向冲击环、折射波
///   0.05  灵焰从出口升腾，蓝蝶散出
/// </summary>
public static class EvokerShot
{
	private static ulong _hitStopUntil;

	public static void Fire(Control layer, Vector2 muzzle, Vector2 dir, float power = 1f, Color? tint = null)
	{
		Color c = tint ?? Pal.Cyan;
		dir = dir.LengthSquared() < 0.01f ? Vector2.Left : dir.Normalized();
		Vector2 exit = muzzle + dir * 105f * power;

		HitStop(0.09);
		ScreenFlash(layer, 0.2f, 0.12f);          // 只是一层很淡的冷色提亮，避免刺眼
		NGame.Instance?.ScreenShake(power >= 1.2f ? ShakeStrength.Strong : ShakeStrength.Medium, ShakeDuration.Short, Mathf.RadToDeg(dir.Angle()));

		// 枪口闪光：四角星 + 白点
		Node2D flash = Fx.Root(layer, muzzle, "P3MuzzleFlash");
		Fx.Poly(flash, Fx.Star4(46f * power, 0.18f), Pal.White, additive: true);
		flash.Rotation = dir.Angle();
		Fx.Animate(flash, 0.14f, t =>
		{
			flash.Scale = Vector2.One * Mathf.Lerp(1.2f, 0.4f, t);
			flash.Modulate = new Color(1f, 1f, 1f, 1f - t);
		});
		Fx.FreeAfter(flash, 0.2);

		Beam(layer, muzzle, dir, c, power);

		// 出口：玻璃面瞬间龟裂 → 扇形碎片喷出并悬停闪光 → 蓝雾 + 喷流 + 斜向冲击环 + 折射
		CrackPane(layer, exit, dir, power);
		ConeShatter(layer, exit, dir, (int)(55 * power), power);
		Mist(layer, exit, dir, c, power);
		Node2D jetRoot = Fx.Root(layer, exit, "P3SpiritJet");
		GpuParticles2D jet = Fx.Sparks(jetRoot, c, 60, 400f * power, 1100f * power, 0.45f, gravity: 0f, scaleMin: 0.4f, scaleMax: 1.1f);
		ParticleProcessMaterial pm = (ParticleProcessMaterial)jet.ProcessMaterial;
		pm.Direction = new Vector3(dir.X, dir.Y, 0f);
		pm.Spread = 22f;
		pm.DampingMin = 6f;
		pm.DampingMax = 10f;
		Fx.FreeAfter(jetRoot, 0.8);

		Node2D ringRoot = Fx.Root(layer, exit + dir * 30f, "P3ExitRing");
		ringRoot.Rotation = dir.Angle();
		ColorRect ring = Fx.ShaderQuad(ringRoot, Shaders.Ring, 300f * power, 1f);
		ring.Scale = new Vector2(0.35f, 1f);
		Fx.SetParam(ring, "tint", Pal.Ice);
		Fx.Animate(ring, 0.4f, t =>
		{
			float e = Ease.OutExpo(t);
			Fx.SetParam(ring, "radius", Mathf.Lerp(0.1f, 0.95f, e));
			Fx.SetParam(ring, "width", Mathf.Lerp(0.08f, 0.01f, e));
			Fx.SetParam(ring, "alpha", 1f - Ease.InCubic(t));
			ringRoot.GlobalPosition = exit + dir * Mathf.Lerp(20f, 140f, e);
		});
		Fx.FreeAfter(ringRoot, 0.5);

		P3Fx.Ripple(layer, exit, 560f * power, 0.6f, 0.04f);
		P3Fx.Flash(layer, exit, Pal.Ice, 300f * power, 0.2f);
		P3Fx.SpeedLines(layer, exit, c, 10, 40f, 300f * power, 0.3f);

		Fx.After(layer, 0.05, "spirit rise", () =>
		{
			P3Fx.SpiritFlare(layer, exit + new Vector2(0f, 40f), Pal.Royal, 360f * power, 0.9f);
			P3Fx.ButterflyBurst(layer, exit, Mathf.Max(2, (int)(3 * power)), 240f * power, c);
		});
	}

	/// <summary>贯穿光束：一帧内贯穿到远处，然后从尾部向前收束消失。</summary>
	private static void Beam(Control layer, Vector2 muzzle, Vector2 dir, Color c, float power)
	{
		Node2D root = Fx.Root(layer, Vector2.Zero, "P3Beam");
		root.TopLevel = true;
		root.GlobalPosition = Vector2.Zero;
		float len = 620f * power;
		Curve taper = new Curve();
		taper.AddPoint(new Vector2(0f, 1f));
		taper.AddPoint(new Vector2(1f, 0f));
		Line2D glow = Fx.Line(root, new[] { muzzle, muzzle + dir * len }, new Color(c, 0.7f), 34f * power);
		Line2D core = Fx.Line(root, new[] { muzzle, muzzle + dir * len }, Pal.White, 9f * power);
		glow.WidthCurve = core.WidthCurve = taper;
		// 余辉：一条细线在原地停留更久，让"穿过去的轨迹"留在画面上
		Line2D after = Fx.Line(root, new[] { muzzle - dir * 20f, muzzle + dir * len * 0.85f }, new Color(Pal.Ice, 0.55f), 3f * power);
		after.WidthCurve = taper;
		Fx.Animate(after, 0.55f, t => after.Modulate = new Color(1f, 1f, 1f, 1f - Ease.OutCubic(t)));
		Fx.Animate(root, 0.32f, t =>
		{
			float tail = Ease.InCubic(t);
			Vector2 a = muzzle + dir * len * tail;
			Vector2 b = muzzle + dir * len * (1f + 0.3f * t);
			glow.Points = core.Points = new[] { a, b };
			float w = 1f - t * 0.7f;
			glow.Width = 34f * power * w;
			core.Width = 9f * power * w;
		});
		Fx.FreeAfter(root, 0.6);
	}

	/// <summary>出口处的玻璃面龟裂：放射状锯齿裂纹 + 两圈断续环纹，瞬间出现，随碎片飞出而淡去。</summary>
	private static void CrackPane(Control layer, Vector2 at, Vector2 dir, float power)
	{
		Node2D root = Fx.Root(layer, at, "P3CrackPane");
		root.Rotation = dir.Angle();
		root.Scale = new Vector2(0.55f, 1f);                 // 面朝出射方向的玻璃面，透视压扁
		int rays = 9;
		for (int i = 0; i < rays; i++)
		{
			Vector2 d = Vector2.Right.Rotated(Mathf.Tau * i / rays + Fx.Rand(-0.2f, 0.2f));
			float len = Fx.Rand(70f, 130f) * power;
			Vector2[] pts = new Vector2[5];
			for (int k = 0; k < 5; k++)
			{
				pts[k] = d * len * k / 4f + d.Orthogonal() * (k == 0 ? 0f : Fx.Rand(-8f, 8f) * power);
			}
			Fx.Line(root, pts, Pal.White, 2.2f);
		}
		for (int r = 1; r <= 2; r++)
		{
			float radius = 38f * r * power;
			for (int seg = 0; seg < 6; seg++)
			{
				float a0 = Mathf.Tau * seg / 6f + Fx.Rand(0f, 0.4f);
				Vector2[] arc = new Vector2[5];
				for (int k = 0; k < 5; k++)
				{
					float a = a0 + 0.6f * k / 4f;
					arc[k] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (radius + Fx.Rand(-4f, 4f));
				}
				Fx.Line(root, arc, new Color(Pal.Ice, 0.85f), 1.6f);
			}
		}
		Fx.Poly(root, Fx.Circle(26f * power, 20), new Color(Pal.Ice, 0.6f), additive: true);
		Fx.Animate(root, 0.3f, t =>
		{
			root.Modulate = new Color(1f + (1f - t), 1f + (1f - t), 1f + (1f - t), 1f - Ease.InCubic(t));
			root.Scale = new Vector2(0.55f, 1f) * Mathf.Lerp(1f, 1.25f, Ease.OutExpo(t));
		});
		Fx.FreeAfter(root, 0.35);
	}

	/// <summary>出口蓝雾：大颗柔光粒子沿出射方向缓慢扩散，低透明度。</summary>
	private static void Mist(Control layer, Vector2 at, Vector2 dir, Color c, float power)
	{
		Node2D root = Fx.Root(layer, at, "P3ExitMist");
		GpuParticles2D mist = Fx.Sparks(root, new Color(c, 0.35f), 26, 120f * power, 380f * power, 1.2f, gravity: -40f, scaleMin: 2.5f * power, scaleMax: 5f * power);
		ParticleProcessMaterial pm = (ParticleProcessMaterial)mist.ProcessMaterial;
		pm.Direction = new Vector3(dir.X, dir.Y, 0f);
		pm.Spread = 30f;
		pm.DampingMin = 3f;
		pm.DampingMax = 5f;
		Fx.FreeAfter(root, 1.5);
	}

	/// <summary>沿 dir 方向锥形喷出的玻璃碎片（贯穿后从头的另一侧飞出）。</summary>
	private static void ConeShatter(Control layer, Vector2 at, Vector2 dir, int count, float power)
	{
		Node2D root = Fx.Root(layer, at, "P3ConeShatter");
		const float duration = 1.5f;
		for (int i = 0; i < count; i++)
		{
			bool pane = i % 5 == 0;                            // 每 5 片有 1 片大玻璃
			float len = (pane ? Fx.Rand(34f, 60f) : Fx.Rand(8f, 24f)) * power;
			float wid = len * Fx.Rand(0.35f, 0.7f);
			Vector2[] shape = pane
				? new Vector2[] { new(-len * 0.5f, -wid * 0.2f), new(-len * 0.05f, -wid * 0.5f), new(len * 0.5f, -wid * 0.1f), new(len * 0.2f, wid * 0.5f), new(-len * 0.35f, wid * 0.35f) }
				: new Vector2[] { new(-len * 0.5f, 0f), new(-len * 0.1f, -wid * 0.5f), new(len * 0.5f, 0f), new(len * 0.05f, wid * 0.5f) };
			Node2D shard = new Node2D();
			root.AddChild(shard);
			Polygon2D body = Fx.Poly(shard, shape, new Color(Pal.Ice, pane ? 0.28f : 0.4f), additive: true);
			Line2D edge = Fx.Line(shard, shape, Pal.White, pane ? 2f : 1.4f, closed: true);
			Vector2 d = dir.Rotated(Fx.Rand(-0.75f, 0.75f));
			float speed = Fx.Rand(900f, 2000f) * power * (pane ? 0.6f : 1f);
			const float drag = 7f;                             // 强阻尼：迅速减速后悬停
			float life = duration * Fx.Rand(0.6f, 1f);
			float spin = Fx.Rand(-10f, 10f);
			float drift = Fx.Rand(-1f, 1f) * 0.6f;              // 悬停期间的缓慢翻转
			float phase = Fx.Rand(0f, Mathf.Tau);
			shard.Rotation = d.Angle();
			Fx.Animate(shard, life, t =>
			{
				float s = t * life;
				shard.Position = d * speed * (1f - Mathf.Exp(-drag * s)) / drag + new Vector2(0f, 35f * s * s) + d.Orthogonal() * drift * 30f * s;
				shard.Rotation = d.Angle() + spin * (1f - Mathf.Exp(-3f * s)) / 3f + drift * s;
				float glint = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(shard.Rotation * 3f + phase + s * 4f)), 10f);
				edge.Modulate = new Color(1f + glint * 1.5f, 1f + glint * 1.5f, 1f + glint * 1.5f, 1f);
				body.Modulate = new Color(1f, 1f, 1f, 0.6f + glint);
				float end = Ease.Segment(t, 0.65f, 0.35f);
				shard.Scale = Vector2.One * Mathf.Lerp(1f, 0.15f, Ease.InCubic(end));
				shard.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(end));
			});
		}
		// 碎片消散后留下的光点
		Fx.After(layer, duration * 0.6, "shard motes", () =>
		{
			Node2D motes = Fx.Root(layer, at + dir * 140f * power, "P3ShardMotes");
			GpuParticles2D p = Fx.Sparks(motes, Pal.Cyan, 40, 20f, 90f, 1.0f, gravity: -60f, scaleMin: 0.08f, scaleMax: 0.22f);
			ParticleProcessMaterial pm = (ParticleProcessMaterial)p.ProcessMaterial;
			pm.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
			pm.EmissionSphereRadius = 110f * power;
			Fx.FreeAfter(motes, 1.3);
		});
		Fx.FreeAfter(root, duration + 0.2);
	}

	/// <summary>顿帧：短暂放慢全局时间，给扣扳机一个"定格"的确认感。用真实时间计时恢复。</summary>
	public static void HitStop(double realSeconds)
	{
		// v0.14.51：用截止时间代替"进行中"标记 —— 恢复回调万一没执行，标记不会永远挡住之后的顿帧；速度本身由 TimeScale 的看门狗兜底
		ulong now = Time.GetTicksMsec();
		if (now < _hitStopUntil || Engine.GetMainLoop() is not SceneTree tree)
		{
			return;
		}
		_hitStopUntil = now + (ulong)(realSeconds * 1000.0);
		P3Makoto.Core.TimeScale.HitStop(0.08, realSeconds + 0.3);
		tree.CreateTimer(realSeconds, processAlways: true, processInPhysics: false, ignoreTimeScale: true).Timeout += () =>
			P3Makoto.Core.TimeScale.HitStop(1.0);
	}

	/// <summary>全屏冷色提亮（很淡，不刺眼；强度靠局部闪光和震屏表现）。</summary>
	public static void ScreenFlash(Control layer, float duration, float peak)
	{
		Vector2 size = layer.GetViewportRect().Size;
		Node2D root = Fx.Root(layer, Vector2.Zero, "P3ScreenFlash");
		root.TopLevel = true;
		root.GlobalPosition = Vector2.Zero;
		ColorRect rect = new ColorRect { Size = size, Color = new Color(0.55f, 0.75f, 1f, peak), MouseFilter = Control.MouseFilterEnum.Ignore, Material = Fx.Additive };
		root.AddChild(rect);
		Fx.Animate(rect, duration, t => rect.Color = new Color(0.55f, 0.75f, 1f, peak * (1f - Ease.OutCubic(t))));
		Fx.FreeAfter(root, duration + 0.05);
	}
}
