using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using P3Makoto.Core;

namespace P3Makoto.Vfx;

/// <summary>
/// v0.13.10：王者之拳 / 王者之踢 / 决胜一击 的专属命中（原来三张都用同一个打击星爆，而且实心星爆会把敌人整个盖住）。
/// 共同原则：冲击点放在敌人朝向玩家的一侧（身体左缘），画面元素只用细线、空心环和向外飞的碎片，敌人本体始终看得见。
/// · 拳：直拳 —— 从左侧射来的三道平行拳风，压出一道竖向的空气激波（")" 形），火花向左反弹。
/// · 踢：回旋踢 —— 一道带残影的粗月牙弧从下往上扫过冲击点，斜向的扁平冲击环，脚下扬起尘土。
/// · 决胜：终结一击 —— 全屏一道细横线"定格"，冲击点放射出玻璃裂纹，重顿帧；敌人倒下时 5 颗辉星从它身上飞回结城理（卡面：击杀获得 5 辉星）。
/// </summary>
public static class Kingly
{
	private static void Shake(ShakeStrength s) => MegaCrit.Sts2.Core.Nodes.NGame.Instance?.ScreenShake(s, ShakeDuration.Short);

	/// <summary>冲击点：敌人身体朝向玩家的一侧（左缘），略高于中心。</summary>
	private static Vector2 Contact(NCreature n, float facing)
	{
		Vector2 c = n.VfxSpawnPosition;
		float halfW = Mathf.Clamp((P3Makoto.Core.Makoto.Feet(n).Y - c.Y) * 0.55f, 50f, 140f);
		return c + new Vector2(-halfW * facing, -10f);
	}

	public static bool Hit(MegaCrit.Sts2.Core.Models.CardModel card, Control front, NCreature node, Creature target, float scale)
	{
		float facing = Makoto.Node(card.Owner) is NCreature caster ? Makoto.Facing(caster) : 1f;
		switch (card.GetType().Name)
		{
			case "KinglyPunch": Punch(front, Contact(node, facing), scale, facing); return true;
			case "KinglyKick": Kick(front, Contact(node, facing), P3Makoto.Core.Makoto.Feet(node), scale, facing); return true;
			case "KnockoutBlow": Knockout(front, node, target, card, scale, facing); return true;
			case "SolarStrike": SolarHit(front, node, scale); return true;
		}
		return false;
	}

	// ───────── 太阳打击：命中只留轻量的火花（主体是天上的太阳 + 光束，见 Motifs.SolarFlare），不再喷整根火柱盖住敌人 ─────────
	private static void SolarHit(Control front, NCreature node, float s)
	{
		Vector2 at = node.VfxSpawnPosition;
		Node2D e = Fx.Root(front, at + new Vector2(0f, -20f), "P3SolarEmbers");
		GpuParticles2D p = Fx.Sparks(e, ElementPal.FireMid, 36, 140f, 380f, 0.7f, gravity: 260f, scaleMin: 0.08f, scaleMax: 0.22f,
			ramp: new[] { new Color(1f, 0.95f, 0.7f), ElementPal.FireMid, new Color(0.5f, 0.08f, 0.04f, 0f) });
		Fx.FreeAfter(e, 0.9);
		Node2D ring = Fx.Root(front, P3Makoto.Core.Makoto.Feet(node), "P3SolarRing");
		Fx.StrokeClosed(ring, Fx.Circle(120f * s, 40, 0.3f), new Color(ElementPal.FireMid, 0.8f), 3f * s);
		Fx.Animate(ring, 0.4f, t =>
		{
			ring.Scale = Vector2.One * (0.4f + 1.2f * Ease.OutExpo(t));
			ring.Modulate = new Color(1f, 1f, 1f, 1f - t);
		});
		Fx.FreeAfter(ring, 0.42);
	}

	// ───────── 王者之拳 ─────────
	private static void Punch(Control front, Vector2 at, float s, float facing)
	{
		// 拳风：三道平行细线从左边射入（0.07s），尾部渐细
		for (int i = 0; i < 3; i++)
		{
			float y = (i - 1) * 26f * s;
			Node2D r = Fx.Root(front, at + new Vector2(0f, y), "P3PunchStreak");
			r.Scale = new Vector2(facing, 1f);
			Polygon2D streak = Fx.Stroke(r, new[] { new Vector2(-10f, 0f), new Vector2(-11f, 0f) }, new Color(i == 1 ? Pal.White : Pal.Ice, 0.9f), (i == 1 ? 7f : 4f) * s);
			float len = (i == 1 ? 300f : 220f) * s;
			Fx.Animate(r, 0.22f, t =>
			{
				float head = Mathf.Lerp(-len - 40f, -6f, Ease.OutExpo(Mathf.Clamp(t / 0.32f, 0f, 1f)));
				float tail = Mathf.Lerp(-len - 160f, -8f, Ease.OutCubic(t));
				Fx.Reshape(streak, Fx.StrokePts(new[] { new Vector2(tail, 0f), new Vector2(head, 0f) }, (i == 1 ? 7f : 4f) * s, taper: true));
				r.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(t));
			});
			Fx.FreeAfter(r, 0.24);
		}
		// 竖向空气激波：两道压扁的空心椭圆（")" 形，只画右半边），一前一后
		for (int k = 0; k < 2; k++)
		{
			float delay = 0.06f + 0.06f * k;
			Node2D w = Fx.Root(front, at, "P3PunchWave");
			var arc = new List<Vector2>();
			for (int i = 0; i <= 18; i++)
			{
				float a = Mathf.Lerp(-1.25f, 1.25f, i / 18f);
				arc.Add(new Vector2(Mathf.Cos(a) * 34f, Mathf.Sin(a) * 120f) * s);
			}
			Fx.Stroke(w, arc.ToArray(), new Color(k == 0 ? Pal.White : Pal.Cyan, 0.9f), (k == 0 ? 6f : 4f) * s);
			w.Modulate = new Color(1f, 1f, 1f, 0f);
			Fx.Animate(w, 0.3f + delay, t =>
			{
				float time = t * (0.3f + delay) - delay;
				float u = Mathf.Clamp(time / 0.3f, 0f, 1f);
				w.GlobalPosition = at + new Vector2(facing * 70f * Ease.OutCubic(u) * s, 0f);
				w.Scale = new Vector2(facing * (1f + 0.6f * u), 0.6f + 0.6f * Ease.OutExpo(u));
				w.Modulate = new Color(1f, 1f, 1f, time < 0f ? 0f : 1f - Ease.InCubic(u));
			});
			Fx.FreeAfter(w, 0.32 + delay);
		}
		// 火花向左反弹（后坐力），小而亮，不盖住敌人
		Node2D sp = Fx.Root(front, at, "P3PunchSparks");
		sp.Scale = new Vector2(facing, 1f);
		for (int i = 0; i < 7; i++)
		{
			Node2D d = new();
			sp.AddChild(d);
			Fx.Poly(d, Fx.Star4(9f * s, 0.25f), i % 2 == 0 ? Pal.White : Pal.Cyan, additive: true);
			Vector2 v = Vector2.Left.Rotated(Fx.Rand(-0.8f, 0.8f)) * Fx.Rand(180f, 360f) * s;
			Fx.Animate(d, 0.35f, t =>
			{
				d.Position = v * Ease.OutCubic(t) * 0.45f + new Vector2(0f, 80f * t * t);
				d.Rotation = t * 5f;
				d.Modulate = new Color(1f, 1f, 1f, 1f - t);
			});
		}
		Fx.FreeAfter(sp, 0.37);
		P3Fx.Flash(front, at, Pal.Ice, 120f * s, 0.12f);
		EvokerShot.HitStop(0.05);
		Shake(ShakeStrength.Weak);
	}

	// ───────── 王者之踢 ─────────
	private static void Kick(Control front, Vector2 at, Vector2 feet, float s, float facing)
	{
		// 月牙弧：圆心在冲击点左下，弧从下往上扫过冲击点；主弧 + 两道滞后的残影
		Vector2 pivot = at + new Vector2(-150f * facing, 110f) * s;
		float R = 200f * s;
		for (int k = 0; k < 3; k++)
		{
			float lag = 0.035f * k;
			Node2D root = Fx.Root(front, pivot, "P3KickArc");
			root.Scale = new Vector2(facing, 1f);
			Polygon2D arc = Fx.Poly(root, new Vector2[3], new Color(k == 0 ? Pal.White : Pal.Cyan, k == 0 ? 0.95f : 0.45f - 0.12f * k), additive: true);
			Fx.Animate(root, 0.34f + lag, t =>
			{
				float time = t * (0.34f + lag) - lag;
				float sweep = Ease.OutCubic(Mathf.Clamp(time / 0.2f, 0f, 1f));
				float a1 = Mathf.Lerp(1.7f, -0.9f, sweep);              // 弧头：从正下方扫到右上
				float a0 = Mathf.Lerp(1.7f, a1 + 1.4f, Mathf.Clamp(time / 0.3f, 0f, 1f) * 0.9f + 0.1f);
				var pts = new List<Vector2>();
				const int n = 18;
				for (int i = 0; i <= n; i++) { float a = Mathf.Lerp(a0, a1, i / (float)n); pts.Add(Vector2.Right.Rotated(a) * R); }
				for (int i = n; i >= 0; i--)
				{
					float a = Mathf.Lerp(a0, a1, i / (float)n);
					float w = (k == 0 ? 34f : 22f) * s * Mathf.Sin(i / (float)n * Mathf.Pi * 0.9f + 0.1f);
					pts.Add(Vector2.Right.Rotated(a) * (R - w));
				}
				Fx.Reshape(arc, pts.ToArray());
				root.Modulate = new Color(1f, 1f, 1f, time < 0f ? 0f : 1f - Ease.InCubic(Mathf.Clamp((time - 0.18f) / 0.16f, 0f, 1f)));
			});
			Fx.FreeAfter(root, 0.36 + lag);
		}
		// 冲击：弧头扫过冲击点的那一刻（约 0.1s），斜向扁平空心环 + 一圈小碎光
		Fx.After(front, 0.1, "kick impact", () =>
		{
			Node2D ring = Fx.Root(front, at, "P3KickRing");
			ring.Rotation = -0.7f * facing;
			Fx.StrokeClosed(ring, Fx.Circle(90f * s, 40, 0.35f), new Color(Pal.White, 0.9f), 5f * s);
			Fx.StrokeClosed(ring, Fx.Circle(70f * s, 40, 0.35f), new Color(Pal.Cyan, 0.7f), 3f * s);
			Fx.Animate(ring, 0.3f, t =>
			{
				ring.Scale = Vector2.One * (0.4f + 1.3f * Ease.OutExpo(t));
				ring.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(t));
			});
			Fx.FreeAfter(ring, 0.32);
			P3Fx.SpeedLines(front, at, Pal.Ice, 10, 70f * s, 260f * s, 0.25f);
			EvokerShot.HitStop(0.06);
			Shake(ShakeStrength.Medium);
		});
		// 扬尘：脚下一道扁平的尘环 + 向两侧飞起的尘粒
		Node2D dust = Fx.Root(front, feet + new Vector2(-40f * s * facing, 0f), "P3KickDust");
		Fx.StrokeClosed(dust, Fx.Circle(110f * s, 36, 0.22f), new Color(0.75f, 0.85f, 1f, 0.5f), 3f * s);
		Fx.Animate(dust, 0.45f, t =>
		{
			dust.Scale = Vector2.One * (0.3f + 1.1f * Ease.OutExpo(t));
			dust.Modulate = new Color(1f, 1f, 1f, 1f - t);
		});
		GpuParticles2D p = Fx.Sparks(dust, new Color(0.8f, 0.88f, 1f, 0.7f), 26, 120f, 300f, 0.6f, gravity: 500f, scaleMin: 0.1f, scaleMax: 0.25f);
		((ParticleProcessMaterial)p.ProcessMaterial).Direction = new Vector3(0f, -1f, 0f);
		((ParticleProcessMaterial)p.ProcessMaterial).Spread = 70f;
		Fx.FreeAfter(dust, 0.8);
	}

	// ───────── 决胜一击 ─────────
	private static void Knockout(Control front, NCreature node, Creature target, MegaCrit.Sts2.Core.Models.CardModel card, float s, float facing)
	{
		Vector2 at = Contact(node, facing);
		Vector2 size = front.GetViewportRect().Size;
		// 定格横线：一道极细的白线横贯全屏（P3R 暴击定格），中间最亮
		Node2D line = Fx.Root(front, new Vector2(0f, at.Y), "P3KOLine");
		Polygon2D bar = Fx.Poly(line, new[] { new Vector2(0f, -1.5f), new Vector2(size.X, -1.5f), new Vector2(size.X, 1.5f), new Vector2(0f, 1.5f) }, Pal.White, additive: true);
		Fx.Animate(line, 0.32f, t =>
		{
			float w = 1f + 5f * Mathf.Clamp(1f - Mathf.Abs(t - 0.15f) / 0.15f, 0f, 1f);
			line.Scale = new Vector2(1f, w);
			line.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(t));
		});
		Fx.FreeAfter(line, 0.34);
		// 玻璃裂纹：从冲击点放射 11 道折线（细、白），向外逐段长出，然后碎片掉落
		Node2D cracks = Fx.Root(front, at, "P3KOCracks");
		var lines = new List<(Polygon2D Poly, Vector2[] Pts)>();
		for (int i = 0; i < 11; i++)
		{
			float a = Mathf.Tau * i / 11f + Fx.Rand(-0.2f, 0.2f);
			float len = Fx.Rand(150f, 320f) * s;
			var pts = new Vector2[5];
			Vector2 dir = Vector2.Right.Rotated(a);
			for (int k = 0; k < 5; k++)
			{
				pts[k] = dir * len * k / 4f + dir.Orthogonal() * (k == 0 ? 0f : Fx.Rand(-18f, 18f) * s);
			}
			lines.Add((Fx.Poly(cracks, new Vector2[3], new Color(Pal.White, 0.9f), additive: true), pts));
		}
		Fx.Animate(cracks, 0.55f, t =>
		{
			float grow = Ease.OutExpo(Mathf.Clamp(t / 0.25f, 0f, 1f));
			foreach (var (poly, pts) in lines)
			{
				// 沿折线长出：完整的前几段 + 最后一段按比例插值
				float g = grow * 4f;
				int full = Mathf.Min(4, Mathf.FloorToInt(g));
				var cut = new List<Vector2>();
				for (int k = 0; k <= full; k++) cut.Add(pts[k]);
				if (full < 4) cut.Add(pts[full].Lerp(pts[full + 1], g - full));
				if (cut.Count < 2) cut.Add(pts[0] + (pts[1] - pts[0]) * 0.05f);
				Fx.Reshape(poly, Fx.StrokePts(cut.ToArray(), 2.5f * s, taper: true));
			}
			cracks.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(Mathf.Clamp((t - 0.55f) / 0.45f, 0f, 1f)));
		});
		Fx.FreeAfter(cracks, 0.57);
		Fx.After(front, 0.2, "ko shards", () => P3Fx.GlassShatter(front, at, 16, 0.7f, Pal.Ice));
		// 竖向光柱一闪（细），重顿帧 + 强震
		Node2D pillar = Fx.Root(front, at, "P3KOPillar");
		Fx.Poly(pillar, new[] { new Vector2(-3f, -260f), new Vector2(3f, -260f), new Vector2(3f, 260f), new Vector2(-3f, 260f) }, Pal.White, additive: true);
		Fx.Animate(pillar, 0.25f, t =>
		{
			pillar.Scale = new Vector2(1f + 4f * (1f - t), 0.4f + 0.6f * Ease.OutExpo(t));
			pillar.Modulate = new Color(1f, 1f, 1f, 1f - t);
		});
		Fx.FreeAfter(pillar, 0.27);
		EvokerShot.HitStop(0.1);
		Shake(ShakeStrength.Strong);
		// 击杀奖励：伤害结算后敌人倒下 → 5 颗辉星从它身上飞回胸口
		Fx.After(front, 0.25, "ko reward", () =>
		{
			if (target.IsAlive || Makoto.FxLayer() is not Control f || Makoto.Node(card.Owner) is not NCreature me)
			{
				return;
			}
			Vector2 chest = Makoto.Center(me);
			for (int i = 0; i < 5; i++)
			{
				int idx = i;
				Fx.After(f, 0.07 * i, "ko star", () => Rares.ThrowStar(f, node.VfxSpawnPosition, chest, idx));
			}
		});
	}
}
