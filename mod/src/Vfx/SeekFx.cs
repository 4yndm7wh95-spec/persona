using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using P3Makoto.Core;

namespace P3Makoto.Vfx;

/// <summary>
/// 追踪之刃「千刃追踪」（v0.14.8，替换 v0.14.7 的虚线测量 —— 用户：虚线和空间杀法太像、左上面板草率，可以单纯华丽一些）。
/// 意思：之后的君王之刃会自己找上每一个敌人。全程约 2.6s：
///   0.00  凝剑：三十片冰蓝玻璃碎片从结城理周围一圈飞来，在他身后拼成一把竖直的巨大光剑（剑身、护手、剑柄），
///         0.35 剑的半透明本体亮起（白边 + 青色内光）；音：Persona 光环浮现
///   0.50  蓄势：一道白光从剑柄沿剑刃扫到剑尖，护手处一圈光环，剑身呼吸
///   0.75  碎裂：剑身白闪后炸开，碎片先向外飞散、再绕着结城理盘旋（带残光拖尾）；音：阿卡纳爆发；轻震
///   1.10  追踪：碎片分成几股，各自沿弧线追向一个敌人（每片拖一道青白光尾）；音：灵能上扬
///   1.45  落定：每股碎片在敌人头顶重新拼成一把剑尖朝下的小晶剑（亮光 + 细圈 + 锁定声，音高逐个升高），悬停上下浮动，剑刃有光点闪过；
///         左上角挂 P3R 技能名小横幅「空间杀法 VORPAL BLADE」
///   2.25  余韵：所有小晶剑同时一闪，然后散成光点淡出
/// 不画全屏线条（和空间杀法区分开）；敌人头顶的小剑在身体上方，不挡敌人。
/// </summary>
public static class SeekFx
{
	private sealed class Shard
	{
		public Polygon2D Poly = null!;
		public Line2D Trail = null!;
		public Vector2 Ring, Blade, Sword;
		public float Delay, Phase, Radius, Angle;
		public int Group;
	}

	public static void Play(Control front, NCreature caster, Vector2[] targets)
	{
		Control back = Makoto.FxLayer(back: true) ?? front;
		Vector2 size = front.GetViewportRect().Size;
		Vector2 center = Makoto.Center(caster);
		Vector2 feet = Makoto.Feet(caster);
		float body = Mathf.Max(260f, (feet.Y - center.Y) * 2.1f);
		float L = body * 1.25f;                                       // 光剑全长
		Vector2 hilt = center + new Vector2(-body * 0.12f, body * 0.05f);   // 剑柄底端（在结城理身后）
		int n = Mathf.Max(1, targets.Length);
		Vector2[] swordAt = new Vector2[n];
		for (int g = 0; g < n; g++)
		{
			swordAt[g] = targets.Length > 0 ? targets[g] + new Vector2(0f, -175f) : center + new Vector2(300f, -200f);
		}

		Node2D root = Fx.Root(front, Vector2.Zero, "P3SeekEdge");
		root.TopLevel = true;
		root.GlobalPosition = Vector2.Zero;
		Node2D bladeRoot = Fx.Root(back, Vector2.Zero, "P3SeekBlade");
		bladeRoot.TopLevel = true;
		bladeRoot.GlobalPosition = Vector2.Zero;

		// ── 剑的半透明本体（后层，在人物身后） ──
		Vector2 tip = hilt + new Vector2(0f, -L);
		Vector2 guardY = hilt + new Vector2(0f, -L * 0.2f);
		float bw = L * 0.055f;
		Vector2[] bladePoly = { tip, tip + new Vector2(bw, L * 0.12f), guardY + new Vector2(bw, 0f), guardY + new Vector2(-bw, 0f), tip + new Vector2(-bw, L * 0.12f) };
		Vector2[] guardPoly = { guardY + new Vector2(-L * 0.17f, -6f), guardY + new Vector2(L * 0.17f, -6f), guardY + new Vector2(L * 0.14f, 10f), guardY + new Vector2(-L * 0.14f, 10f) };
		Vector2[] gripPoly = { guardY + new Vector2(-bw * 0.45f, 10f), guardY + new Vector2(bw * 0.45f, 10f), hilt + new Vector2(bw * 0.45f, 0f), hilt + new Vector2(-bw * 0.45f, 0f) };
		Node2D ghost = new() { Modulate = new Color(1f, 1f, 1f, 0f) };
		bladeRoot.AddChild(ghost);
		ghost.AddChild(new Sprite2D { Texture = Fx.SoftDot, Material = Fx.Additive, Position = (tip + hilt) / 2f, Scale = new Vector2(L * 0.35f, L * 1.1f) / Fx.SoftDot.GetWidth(), Modulate = new Color(Pal.Cyan, 0.45f) });
		Fx.Poly(ghost, bladePoly, new Color(0.55f, 0.85f, 1f, 0.35f), additive: true);
		Fx.Poly(ghost, guardPoly, new Color(0.7f, 0.9f, 1f, 0.5f), additive: true);
		Fx.Poly(ghost, gripPoly, new Color(0.5f, 0.75f, 1f, 0.4f), additive: true);
		Fx.Line(ghost, new[] { bladePoly[0], bladePoly[1], bladePoly[2], bladePoly[3], bladePoly[4], bladePoly[0] }, new Color(1f, 1f, 1f, 0.85f), 2f);
		Fx.Line(ghost, new[] { tip + new Vector2(0f, 8f), guardY }, new Color(1f, 1f, 1f, 0.5f), 1.5f);   // 剑脊
		Polygon2D sweep = Fx.Poly(ghost, new[] { new Vector2(-bw * 1.4f, -8f), new Vector2(bw * 1.4f, -8f), new Vector2(bw * 1.4f, 8f), new Vector2(-bw * 1.4f, 8f) }, new Color(1f, 1f, 1f, 0.9f), additive: true);
		Node2D guardRing = new() { Position = guardY };
		ghost.AddChild(guardRing);
		Fx.StrokeClosed(guardRing, Fx.Circle(L * 0.12f, 48, 0.35f), new Color(Pal.Ice, 0.8f), 2f);

		// ── 碎片 ──
		const int count = 30;
		var shards = new List<Shard>();
		for (int i = 0; i < count; i++)
		{
			float sz = Fx.Rand(10f, 22f);
			Color col = i % 3 == 0 ? Pal.White : i % 3 == 1 ? Pal.Ice : new Color(0.55f, 0.85f, 1f);
			Polygon2D p = Fx.Poly(root, new[] { new Vector2(0f, -sz), new Vector2(sz * 0.55f, sz * 0.5f), new Vector2(-sz * 0.6f, sz * 0.35f) }, col, additive: true);
			p.Modulate = new Color(1f, 1f, 1f, 0f);
			Line2D trail = new() { Width = sz * 0.45f, Material = Fx.Additive, DefaultColor = new Color(Pal.Cyan, 0f), Points = new[] { Vector2.Zero, Vector2.Zero } };
			root.AddChild(trail);
			root.MoveChild(trail, 0);
			// 剑上的位置：80% 在剑身（越往剑尖越窄），20% 在护手 / 剑柄
			Vector2 onBlade;
			if (i < count * 0.8f)
			{
				float k = Fx.Rand(0f, 1f);
				Vector2 axis = guardY.Lerp(tip + new Vector2(0f, L * 0.06f), k);
				onBlade = axis + new Vector2(Fx.Rand(-1f, 1f) * bw * (1f - 0.7f * k), 0f);
			}
			else
			{
				onBlade = i % 2 == 0 ? guardY + new Vector2(Fx.Rand(-L * 0.15f, L * 0.15f), Fx.Rand(-4f, 8f)) : guardY.Lerp(hilt, Fx.Rand(0.15f, 0.9f));
			}
			int group = i % n;
			// 小晶剑上的位置：剑尖朝下的细长菱形
			float s = Fx.Rand(0f, 1f);
			Vector2 onSword = swordAt[group] + new Vector2(Fx.Rand(-1f, 1f) * 12f * (1f - Mathf.Abs(s - 0.35f)), -60f + 120f * s);
			shards.Add(new Shard
			{
				Poly = p, Trail = trail, Group = group,
				Ring = center + Vector2.Right.Rotated(Fx.Rand(0f, Mathf.Tau)) * Fx.Rand(260f, 360f),
				Blade = onBlade, Sword = onSword,
				Delay = Fx.Rand(0f, 0.15f), Phase = Fx.Rand(0f, Mathf.Tau), Radius = Fx.Rand(150f, 230f), Angle = Mathf.Tau * i / count,
			});
		}

		// 每片碎片在时刻 tt 的位置（解析式：残光拖尾直接取过去几个时刻的位置）
		const float formEnd = 0.5f, burstAt = 0.75f, seekAt = 1.1f, seekLen = 0.38f, groupGap = 0.06f;
		Vector2 Orbit(Shard sh, float tt)
		{
			// 碎片均匀分布在一圈椭圆轨道上（每片自己的角度），从剑上的位置甩出去、绕着结城理转
			float u = Mathf.Clamp((tt - burstAt) / (seekAt - burstAt), 0f, 1f);
			float a = sh.Angle + 2.4f * Ease.InOutCubic(u);
			Vector2 ringPos = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.6f) * (sh.Radius + 50f * Mathf.Sin(Mathf.Pi * u));
			return sh.Blade.Lerp(ringPos, Ease.OutCubic(Mathf.Clamp(u * 1.6f, 0f, 1f)));
		}
		Vector2 PosAt(Shard sh, float tt)
		{
			if (tt < formEnd + sh.Delay * 0.3f)
			{
				float u = Mathf.Clamp((tt - sh.Delay) / (formEnd - 0.15f), 0f, 1f);
				return sh.Ring.Lerp(sh.Blade, Ease.OutCubic(u));
			}
			if (tt < burstAt)
			{
				return sh.Blade;
			}
			if (tt < seekAt)
			{
				return Orbit(sh, tt);
			}
			float st = seekAt + groupGap * sh.Group;
			float v = Mathf.Clamp((tt - st) / seekLen, 0f, 1f);
			Vector2 from = Orbit(sh, seekAt);
			Vector2 ctrl = (from + sh.Sword) / 2f + new Vector2(0f, -220f - 60f * Mathf.Sin(sh.Phase));
			float e = Ease.InOutCubic(v);
			float w = 1f - e;
			Vector2 p = w * w * from + 2f * w * e * ctrl + e * e * sh.Sword;
			if (v >= 1f)
			{
				p += new Vector2(0f, 6f * Mathf.Sin((tt - st - seekLen) * 3f + sh.Group));   // 悬停浮动
			}
			return p;
		}

		Audio.Sfx.Clip("Snd_FX_Btl_PersonaAura_00", 0.7f);
		Audio.Sfx.Clip("ShuffleTime_ArcanaBurst", 0.8f, burstAt);
		Audio.Sfx.Clip("Theurgia_Up", 0.55f, seekAt);
		for (int g = 0; g < n; g++)
		{
			Audio.Sfx.Clip("lock_tick", 0.75f, seekAt + groupGap * g + seekLen, 1f + 0.08f * g);
		}

		const float total = 2.6f;
		const float fadeAt = 2.25f;
		bool burst = false;
		var landed = new bool[n];
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			// 剑的本体
			float show = Mathf.Clamp((time - 0.35f) / 0.15f, 0f, 1f);
			float gone = Mathf.Clamp((time - burstAt) / 0.08f, 0f, 1f);
			ghost.Modulate = new Color(1f, 1f, 1f, show * (1f - gone) * (0.85f + 0.15f * Mathf.Sin(time * 14f)));
			float sw = Mathf.Clamp((time - 0.5f) / 0.22f, 0f, 1f);
			sweep.Position = hilt.Lerp(tip, Ease.InOutCubic(sw));
			sweep.Modulate = new Color(1f, 1f, 1f, sw > 0f && sw < 1f ? 1f : 0f);
			guardRing.Scale = Vector2.One * (0.6f + 0.6f * Ease.OutExpo(Mathf.Clamp((time - 0.5f) / 0.25f, 0f, 1f)));
			guardRing.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp((time - 0.5f) / 0.05f, 0f, 1f) * (1f - gone));
			if (!burst && time >= burstAt)
			{
				burst = true;
				P3Fx.Flash(back, (tip + hilt) / 2f, Pal.Ice, L * 0.9f, 0.25f);
				P3Fx.Flash(front, center, Pal.White, body * 0.6f, 0.15f);
				NGame.Instance?.ScreenShake(ShakeStrength.Weak, ShakeDuration.Short);
			}
			float vanish = Mathf.Clamp((time - fadeAt - 0.12f) / 0.25f, 0f, 1f);
			foreach (Shard sh in shards)
			{
				Vector2 p = PosAt(sh, time);
				sh.Poly.Position = p;
				Vector2 prev = PosAt(sh, time - 0.03f);
				sh.Poly.Rotation = (p - prev).LengthSquared() > 1f ? (p - prev).Angle() + Mathf.Pi / 2f : sh.Phase + time * 0.5f;
				float vis = Mathf.Clamp((time - sh.Delay) / 0.08f, 0f, 1f);
				float flare = time >= fadeAt && time < fadeAt + 0.12f ? 1.6f : 1f;
				sh.Poly.Modulate = new Color(flare, flare, flare, vis * (1f - vanish));
				bool moving = time > burstAt && time < seekAt + groupGap * sh.Group + seekLen;
				if (moving)
				{
					sh.Trail.Points = new[] { PosAt(sh, time - 0.12f), PosAt(sh, time - 0.06f), p };
					sh.Trail.DefaultColor = new Color(0.6f, 0.9f, 1f, 0.55f);
				}
				else
				{
					sh.Trail.DefaultColor = new Color(Pal.Cyan, 0f);
				}
			}
			for (int g = 0; g < n; g++)
			{
				if (!landed[g] && time >= seekAt + groupGap * g + seekLen)
				{
					landed[g] = true;
					Landed(front, swordAt[g]);
				}
			}
		});
		Fx.After(root, seekAt + 0.1, "seek banner", () => Uniq.SkillBanner(front, "空间杀法", "VORPAL BLADE", 1.1f));
		Fx.After(root, fadeAt, "seek swords flash", () =>
		{
			foreach (Vector2 a in swordAt)
			{
				P3Fx.Flash(front, a, Pal.Ice, 150f, 0.2f);
				Node2D motes = Fx.Root(front, a, "P3SeekMotes");
				Fx.Sparks(motes, Pal.Ice, 18, 60f, 200f, 0.6f, gravity: -60f);
				Fx.FreeAfter(motes, 0.9);
			}
		});
		Fx.FreeAfter(root, total + 0.05f);
		Fx.FreeAfter(bladeRoot, total + 0.05f);
	}

	/// <summary>一股碎片在敌人头顶拼成小晶剑的那一刻：剑形的淡光底 + 闪光 + 收缩细圈。</summary>
	private static void Landed(Control front, Vector2 at)
	{
		P3Fx.Flash(front, at, Pal.Ice, 170f, 0.18f);
		Node2D root = Fx.Root(front, at, "P3SeekSword");
		Fx.Poly(root, new[] { new Vector2(0f, 72f), new Vector2(14f, -40f), new Vector2(0f, -62f), new Vector2(-14f, -40f) }, new Color(0.6f, 0.88f, 1f, 0.35f), additive: true);
		Fx.Poly(root, new[] { new Vector2(-34f, -44f), new Vector2(34f, -44f), new Vector2(28f, -36f), new Vector2(-28f, -36f) }, new Color(1f, 1f, 1f, 0.6f), additive: true);
		Node2D ring = new();
		root.AddChild(ring);
		Fx.StrokeClosed(ring, Fx.Circle(40f, 48, 0.4f), new Color(1f, 1f, 1f, 0.85f), 2f);
		ring.Position = new Vector2(0f, 76f);
		const float life = 0.95f;
		Fx.Animate(root, life, t =>
		{
			float time = t * life;
			ring.Scale = Vector2.One * Mathf.Lerp(2.2f, 1f, Ease.OutExpo(Mathf.Clamp(time / 0.3f, 0f, 1f)));
			ring.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp((time - 0.3f) / 0.3f, 0f, 1f));
			root.Position = at + new Vector2(0f, 6f * Mathf.Sin(time * 3f));
			root.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(time / 0.1f, 0f, 1f) * (1f - Mathf.Clamp((time - 0.7f) / 0.25f, 0f, 1f)));
		});
		Fx.FreeAfter(root, life + 0.05f);
	}
}
