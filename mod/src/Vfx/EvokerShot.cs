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
	private static bool _hitStopActive;

	public static void Fire(Control layer, Vector2 muzzle, Vector2 dir, float power = 1f, Color? tint = null)
	{
		Color c = tint ?? Pal.Cyan;
		dir = dir.LengthSquared() < 0.01f ? Vector2.Left : dir.Normalized();
		Vector2 exit = muzzle + dir * 105f * power;

		HitStop(0.07);
		ScreenFlash(layer, 0.12f, 0.55f);
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

		// 出口：锥形碎裂 + 喷流 + 斜向冲击环 + 折射
		ConeShatter(layer, exit, dir, (int)(30 * power), power);
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
		Fx.Animate(root, 0.2f, t =>
		{
			float tail = Ease.InCubic(t);
			Vector2 a = muzzle + dir * len * tail;
			Vector2 b = muzzle + dir * len * (1f + 0.3f * t);
			glow.Points = core.Points = new[] { a, b };
			float w = 1f - t * 0.7f;
			glow.Width = 34f * power * w;
			core.Width = 9f * power * w;
		});
		Fx.FreeAfter(root, 0.25);
	}

	/// <summary>沿 dir 方向锥形喷出的玻璃碎片（贯穿后从头的另一侧飞出）。</summary>
	private static void ConeShatter(Control layer, Vector2 at, Vector2 dir, int count, float power)
	{
		Node2D root = Fx.Root(layer, at, "P3ConeShatter");
		const float duration = 0.9f;
		for (int i = 0; i < count; i++)
		{
			float len = Fx.Rand(10f, 28f) * power;
			float wid = len * Fx.Rand(0.25f, 0.5f);
			Vector2[] shape = { new(-len * 0.5f, 0f), new(-len * 0.1f, -wid * 0.5f), new(len * 0.5f, 0f), new(len * 0.05f, wid * 0.5f) };
			Node2D shard = new Node2D();
			root.AddChild(shard);
			Polygon2D body = Fx.Poly(shard, shape, new Color(Pal.Ice, 0.4f), additive: true);
			Line2D edge = Fx.Line(shard, shape, Pal.White, 1.4f, closed: true);
			Vector2 d = dir.Rotated(Fx.Rand(-0.6f, 0.6f));
			float speed = Fx.Rand(700f, 1600f) * power;
			float life = duration * Fx.Rand(0.5f, 1f);
			float spin = Fx.Rand(-14f, 14f);
			float phase = Fx.Rand(0f, Mathf.Tau);
			shard.Rotation = d.Angle();
			Fx.Animate(shard, life, t =>
			{
				float s = t * life;
				shard.Position = d * speed * (1f - Mathf.Exp(-5f * s)) / 5f + new Vector2(0f, 220f * s * s);
				shard.Rotation = d.Angle() + spin * (1f - Mathf.Exp(-2.5f * s)) / 2.5f;
				float glint = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(shard.Rotation * 3f + phase)), 8f);
				edge.Modulate = new Color(1f + glint, 1f + glint, 1f + glint, 1f);
				body.Modulate = new Color(1f, 1f, 1f, 0.6f + glint);
				shard.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(t));
			});
		}
		Fx.FreeAfter(root, duration + 0.2);
	}

	/// <summary>顿帧：短暂放慢全局时间，给扣扳机一个"定格"的确认感。用真实时间计时恢复。</summary>
	public static void HitStop(double realSeconds)
	{
		if (_hitStopActive || Engine.GetMainLoop() is not SceneTree tree)
		{
			return;
		}
		_hitStopActive = true;
		double before = Engine.TimeScale;
		Engine.TimeScale = 0.08;
		tree.CreateTimer(realSeconds, processAlways: true, processInPhysics: false, ignoreTimeScale: true).Timeout += () =>
		{
			Engine.TimeScale = before;
			_hitStopActive = false;
		};
	}

	/// <summary>全屏白闪。</summary>
	public static void ScreenFlash(Control layer, float duration, float peak)
	{
		Vector2 size = layer.GetViewportRect().Size;
		Node2D root = Fx.Root(layer, Vector2.Zero, "P3ScreenFlash");
		root.TopLevel = true;
		root.GlobalPosition = Vector2.Zero;
		ColorRect rect = new ColorRect { Size = size, Color = new Color(1f, 1f, 1f, peak), MouseFilter = Control.MouseFilterEnum.Ignore, Material = Fx.Additive };
		root.AddChild(rect);
		Fx.Animate(rect, duration, t => rect.Color = new Color(0.85f, 0.95f, 1f, peak * (1f - Ease.OutCubic(t))));
		Fx.FreeAfter(root, duration + 0.05);
	}
}
