using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using P3Makoto.Core;

namespace P3Makoto.Vfx;

/// <summary>
/// 月面射击：目标头顶升起一轮新月（着色器月相）。每一击，月亮削下一道新月形的光刃劈向目标、留下一道月牙形斩痕，
/// 月相随之前进一格（新月 → 上弦 → 满月）；连击结束后月亮满盈一闪，八个月相小图标绕月一圈收拢，月光柱落下，随后淡去。
/// 命中次数 = 本回合打出过的技能牌数，所以月亮会一直待命到最后一击后 0.45 秒。
/// </summary>
public static class Lunar
{
	private sealed class Moon
	{
		public Node2D Root = null!;
		public ColorRect Quad = null!;
		public float Phase;
		public int Hits;
		public ulong Last;
		public Vector2 Target;
		public Vector2 Feet;
	}

	private static readonly Dictionary<ulong, Moon> Moons = new();

	private static Vector2 MoonPos(Vector2 target) => new(target.X - 40f, Mathf.Max(target.Y - 300f, 190f));

	/// <summary>出牌：目标上方升起新月。</summary>
	public static void Rise(NCreature target)
	{
		Control? back = Makoto.FxLayer(back: true);
		if (back == null)
		{
			return;
		}
		ulong id = target.GetInstanceId();
		if (Moons.TryGetValue(id, out Moon? old) && GodotObject.IsInstanceValid(old.Root))
		{
			return;
		}
		Vector2 at = target.VfxSpawnPosition;
		Moon m = new() { Target = at, Feet = target.GetBottomOfHitbox(), Phase = 0.18f, Last = Time.GetTicksMsec() };
		m.Root = Fx.Root(back, MoonPos(at) + new Vector2(0f, 60f), "P3LunarMoon");
		m.Quad = Fx.ShaderQuad(m.Root, Shaders.MoonPhase, 300f);
		Fx.SetParam(m.Quad, "phase", m.Phase);
		Fx.SetParam(m.Quad, "alpha", 0f);
		Moons[id] = m;
		Vector2 home = MoonPos(at);
		Fx.Animate(m.Root, 0.3f, t =>
		{
			m.Root.GlobalPosition = home + new Vector2(0f, 60f * (1f - Ease.OutCubic(t)));
			Fx.SetParam(m.Quad, "alpha", Ease.OutCubic(t));
		});
		Audio.Sfx.Play(Audio.Snd.TarotReveal, 0.5f, pitch: 1.2f);
		Watch(id, m);
	}

	/// <summary>每一击：新月光刃从月亮劈到目标，月相前进。</summary>
	public static void Hit(NCreature target, float scale)
	{
		Control? front = Makoto.FxLayer();
		if (front == null)
		{
			return;
		}
		ulong id = target.GetInstanceId();
		if (!Moons.TryGetValue(id, out Moon? m) || !GodotObject.IsInstanceValid(m.Root))
		{
			Rise(target);
			if (!Moons.TryGetValue(id, out m))
			{
				return;
			}
		}
		m.Hits++;
		m.Last = Time.GetTicksMsec();
		Vector2 from = m.Root.GlobalPosition;
		Vector2 to = target.VfxSpawnPosition + new Vector2(Fx.Rand(-30f, 30f), Fx.Rand(-30f, 20f));
		bool right = m.Hits % 2 == 1;
		// 光刃：新月形（两圆之差），从月亮侧边削下，带拖尾俯冲到目标
		Node2D blade = Fx.Root(front, from, "P3LunarBlade");
		Polygon2D glow = Fx.Poly(blade, Crescent(60f * scale, 0.62f), new Color(Pal.Cyan, 0.55f), additive: true);
		Polygon2D core = Fx.Poly(blade, Crescent(46f * scale, 0.62f), Pal.White, additive: true);
		glow.Scale = Vector2.One * 1.25f;
		Line2D trail = new() { Width = 26f * scale, Material = Fx.Additive, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round,
			Gradient = new Gradient { Colors = new[] { new Color(Pal.Cyan, 0f), new Color(Pal.Ice, 0.8f) } } };
		Node2D trailRoot = Fx.Root(front, Vector2.Zero, "P3LunarTrail");
		trailRoot.TopLevel = true;
		trailRoot.GlobalPosition = Vector2.Zero;
		trailRoot.AddChild(trail);
		Vector2 ctrl = (from + to) / 2f + new Vector2(right ? 140f : -140f, -40f);
		const float fly = 0.13f;
		Fx.Animate(blade, fly, t =>
		{
			float e = Ease.InCubic(t);
			Vector2 p = Bezier(from, ctrl, to, e);
			blade.GlobalPosition = p;
			blade.Rotation = (right ? 1f : -1f) * (t * 5f) + (to - from).Angle();
			trail.Points = new[] { Bezier(from, ctrl, to, Mathf.Max(0f, e - 0.35f)), Bezier(from, ctrl, to, Mathf.Max(0f, e - 0.15f)), p };
		});
		Fx.After(blade, fly, "lunar impact", () =>
		{
			blade.QueueFree();
			Fx.Animate(trail, 0.15f, t => trail.Modulate = new Color(1f, 1f, 1f, 1f - t));
			Fx.FreeAfter(trailRoot, 0.16);
			Slash(front, to, scale, right);
			P3Fx.Flash(front, to, Pal.Ice, 220f * scale, 0.18f);
			P3Fx.Shockwave(front, to, Pal.Cyan, 320f * scale, 0.35f);
			EvokerShot.HitStop(0.04);
			MegaCrit.Sts2.Core.Nodes.NGame.Instance?.ScreenShake(ShakeStrength.Weak, ShakeDuration.Short);
			// 月相前进
			float from_ = m.Phase, to_ = Mathf.Min(1f, m.Phase + 0.22f);
			m.Phase = to_;
			if (GodotObject.IsInstanceValid(m.Quad))
			{
				Fx.Animate(m.Quad, 0.18f, t =>
				{
					Fx.SetParam(m.Quad, "phase", Mathf.Lerp(from_, to_, Ease.OutCubic(t)));
					Fx.SetParam(m.Quad, "flash", 0.5f * (1f - t));
				});
			}
		});
		Audio.Sfx.Play(Audio.Snd.StarSpend, 0.55f, pitch: 0.9f + 0.05f * m.Hits);
	}

	/// <summary>月牙形斩痕：一道弯弧在目标身上横向张开再收细。</summary>
	private static void Slash(Control layer, Vector2 at, float s, bool right)
	{
		Node2D root = Fx.Root(layer, at, "P3LunarSlash");
		root.Rotation = (right ? -0.5f : 0.5f) + Fx.Rand(-0.2f, 0.2f);
		root.Scale = new Vector2(right ? 1f : -1f, 1f);
		Polygon2D outer = Fx.Poly(root, Crescent(150f * s, 0.8f), new Color(Pal.Cyan, 0.6f), additive: true);
		Polygon2D inner = Fx.Poly(root, Crescent(130f * s, 0.86f), Pal.White, additive: true);
		Fx.Animate(root, 0.32f, t =>
		{
			float open = Ease.OutExpo(Mathf.Clamp(t / 0.35f, 0f, 1f));
			outer.Scale = new Vector2(open, 1f - 0.5f * t);
			inner.Scale = new Vector2(open, 1f - 0.7f * t);
			root.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(t));
		});
		Fx.FreeAfter(root, 0.35);
	}

	/// <summary>连击结束（0.45 秒没有新的命中）：满月一闪、月相图标收拢、月光柱落下，然后淡去。</summary>
	private static void Watch(ulong id, Moon m)
	{
		Timer timer = new() { WaitTime = 0.1, Autostart = true };
		m.Root.AddChild(timer);
		timer.Timeout += () => ModEntry.Safe("lunar watch", () =>
		{
			if (Time.GetTicksMsec() - m.Last < (m.Hits == 0 ? 2500UL : 450UL))
			{
				return;
			}
			timer.Stop();
			Finale(m);
			Moons.Remove(id);
		});
	}

	private static void Finale(Moon m)
	{
		Control? back = Makoto.FxLayer(back: true);
		Control? front = Makoto.FxLayer();
		if (back == null || front == null || !GodotObject.IsInstanceValid(m.Root))
		{
			return;
		}
		Vector2 at = m.Root.GlobalPosition;
		float p0 = m.Phase;
		// 满盈：月相补满、发白一下
		Fx.Animate(m.Quad, 0.2f, t =>
		{
			Fx.SetParam(m.Quad, "phase", Mathf.Lerp(p0, 1f, Ease.OutCubic(t)));
			Fx.SetParam(m.Quad, "flash", 0.6f * Mathf.Sin(t * Mathf.Pi));
		});
		// 八个月相小图标从外圈收拢到月亮
		for (int i = 0; i < 8; i++)
		{
			Vector2 dir = Vector2.Up.Rotated(Mathf.Tau * i / 8f);
			Node2D icon = Fx.Root(front, at + dir * 190f, "P3LunarPhaseIcon");
			float ph = i / 8f;
			Fx.Poly(icon, Fx.Circle(11f, 20), new Color(0.04f, 0.09f, 0.26f, 0.9f));
			Fx.Poly(icon, PhaseShape(11f, ph), Pal.Ice);
			Fx.Line(icon, Fx.Circle(11f, 20), new Color(Pal.Ice, 0.8f), 1.5f, closed: true);
			Fx.Animate(icon, 0.35f, t =>
			{
				float e = Ease.InCubic(t);
				icon.GlobalPosition = at + dir.Rotated(e * 0.8f) * Mathf.Lerp(190f, 20f, e);
				icon.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(t * 6f, 0f, 1f) * (1f - Mathf.Clamp((t - 0.8f) / 0.2f, 0f, 1f)));
			});
			Fx.FreeAfter(icon, 0.38);
		}
		Fx.After(m.Root, 0.32, "lunar full", () =>
		{
			P3Fx.Flash(front, at, Pal.Ice, 380f, 0.3f);
			P3Fx.Shockwave(front, at, Pal.Ice, 520f, 0.45f);
			// 月光柱：从月亮落到目标脚下，硬边窄柱 + 两道细边
			Node2D beam = Fx.Root(back, at, "P3LunarBeam");
			float len = Mathf.Max(80f, m.Feet.Y - at.Y);
			Polygon2D col = Fx.Poly(beam, new[] { new Vector2(-34f, 0f), new Vector2(34f, 0f), new Vector2(46f, len), new Vector2(-46f, len) }, new Color(Pal.Ice, 0.55f), additive: true);
			Polygon2D coreB = Fx.Poly(beam, new[] { new Vector2(-10f, 0f), new Vector2(10f, 0f), new Vector2(14f, len), new Vector2(-14f, len) }, Pal.White, additive: true);
			Fx.Animate(beam, 0.45f, t =>
			{
				beam.Scale = new Vector2(1f - 0.8f * Ease.InCubic(t), Ease.OutExpo(Mathf.Clamp(t / 0.2f, 0f, 1f)));
				beam.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(t));
			});
			Fx.FreeAfter(beam, 0.5);
			P3Fx.Ripple(back, m.Feet, 500f, 0.7f, 0.02f, squashY: 0.3f);
			Audio.Sfx.Play(Audio.Snd.Weak, 0.6f, pitch: 0.8f);
			Fx.Animate(m.Root, 0.4f, t =>
			{
				Fx.SetParam(m.Quad, "alpha", 1f - Ease.InCubic(t));
				m.Root.Scale = Vector2.One * (1f + 0.15f * Ease.OutCubic(t));
			});
			Fx.FreeAfter(m.Root, 0.42);
		});
	}

	/// <summary>新月形：半径 r 的圆减去向右偏移的圆（cut 越大越细），开口朝右。</summary>
	private static Vector2[] Crescent(float r, float cut)
	{
		var pts = new List<Vector2>();
		const int n = 20;
		float off = r * cut;
		// 外弧：左半圆（从上到下）
		for (int i = 0; i <= n; i++)
		{
			float a = Mathf.Pi / 2f + Mathf.Pi * i / n;
			pts.Add(new Vector2(Mathf.Cos(a), -Mathf.Sin(a)) * r);
		}
		// 内弧：偏移圆的左半边（从下到上）
		for (int i = n; i >= 0; i--)
		{
			float a = Mathf.Pi / 2f + Mathf.Pi * i / n;
			Vector2 q = new Vector2(Mathf.Cos(a) * (r - off), -Mathf.Sin(a) * r);          // 内弧横向压扁：两端尖、中间最厚
			pts.Add(q);
		}
		return pts.ToArray();
	}

	/// <summary>月相图标的亮面：ph 0 新月 → 0.5 满月 → 1 新月（亮面从右往左扫过）。</summary>
	private static Vector2[] PhaseShape(float r, float ph)
	{
		var pts = new List<Vector2>();
		const int n = 16;
		bool waxing = ph <= 0.5f;
		float k = Mathf.Cos(ph * Mathf.Tau);              // 交界线的横向压缩（1 → -1 → 1）
		float side = waxing ? 1f : -1f;
		for (int i = 0; i <= n; i++)
		{
			float a = -Mathf.Pi / 2f + Mathf.Pi * i / n;
			pts.Add(new Vector2(Mathf.Cos(a) * r * side, Mathf.Sin(a) * r));
		}
		for (int i = n; i >= 0; i--)
		{
			float a = -Mathf.Pi / 2f + Mathf.Pi * i / n;
			pts.Add(new Vector2(Mathf.Cos(a) * r * k * side, Mathf.Sin(a) * r));
		}
		return pts.ToArray();
	}

	private static Vector2 Bezier(Vector2 a, Vector2 c, Vector2 b, float t)
	{
		float k = 1f - t;
		return a * (k * k) + c * (2f * k * t) + b * (t * t);
	}
}
