using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using P3Makoto.Core;

namespace P3Makoto.Vfx;

/// <summary>
/// 封印王座（v0.14.0）：能力生效期间，结城理脑后常驻一枚「审判 XX」光环印记（弥赛亚的阿卡纳），缓慢旋转、微微呼吸。
/// 每打出一张牌（TheSealedThronePower 获得 1 颗辉星）：一道金白色光流从手牌区划进印记 → 印记亮起、外扩一圈波纹 →
/// 那颗辉星（蓝色塔罗牌）直接从印记里凝出，飞进环绕轨道（StarOrbit.BirthFrom）。
/// 连续出牌时每次都是同一条"手牌 → 印记 → 轨道"的路径，印记亮度叠加后慢慢回落，星再多也是同一套连贯的动作。
/// </summary>
public static class ThroneSeal
{
	private sealed class Seal
	{
		public Node2D Root = null!;
		public Node2D Spin = null!;
		public float Heat;                               // 最近的触发热度（叠加，慢慢回落）
	}

	private static readonly Dictionary<ulong, Seal> Seals = new();
	private static readonly Color Gold = new(1f, 0.9f, 0.6f);

	private static Vector2 Anchor(NCreature node) => Makoto.Head(node) + new Vector2(-28f, -36f);

	private static readonly Dictionary<ulong, ulong> DeferUntil = new();

	/// <summary>封印王座打出时：先压住印记，等弥赛亚的法阵落进脑后（Release）再出现；最多压 seconds 秒。</summary>
	public static void Defer(NCreature node, float seconds) => DeferUntil[node.GetInstanceId()] = Time.GetTicksMsec() + (ulong)(seconds * 1000f);

	public static void Release(NCreature node) => DeferUntil.Remove(node.GetInstanceId());

	public static void Tick(NCreature node, Control back, Control front, float time)
	{
		ulong id = node.GetInstanceId();
		if (DeferUntil.TryGetValue(id, out ulong until))
		{
			if (Time.GetTicksMsec() < until)
			{
				return;
			}
			DeferUntil.Remove(id);
		}
		bool active = !node.Entity.IsDead && node.Entity.HasPower<MegaCrit.Sts2.Core.Models.Powers.TheSealedThronePower>();
		Seals.TryGetValue(id, out Seal? s);
		if (s != null && !GodotObject.IsInstanceValid(s.Root))
		{
			Seals.Remove(id);
			s = null;
		}
		if (!active)
		{
			if (s != null)
			{
				Seal dying = s;
				Fx.Animate(dying.Root, 0.4f, t => dying.Root.Modulate = new Color(1f, 1f, 1f, 1f - t));
				Fx.FreeAfter(dying.Root, 0.42);
				Seals.Remove(id);
			}
			return;
		}
		if (s == null)
		{
			s = Create(back, Anchor(node));
			Seals[id] = s;
		}
		float dt = (float)node.GetProcessDeltaTime();
		s.Heat = Mathf.Max(0f, s.Heat - dt * 1.4f);
		s.Root.GlobalPosition = s.Root.GlobalPosition.Lerp(Anchor(node), 0.2f);
		s.Spin.Rotation += dt * (0.25f + 1.2f * s.Heat);
		float breath = 0.5f + 0.5f * Mathf.Sin(time * 1.6f);
		float born = Mathf.Clamp(s.Root.GetMeta("age", 0f).AsSingle() + dt, 0f, 10f);
		s.Root.SetMeta("age", born);
		float appear = Ease.OutBack(Mathf.Clamp(born / 0.5f, 0f, 1f));
		s.Root.Scale = Vector2.One * (appear * (1f + 0.04f * breath + 0.12f * Mathf.Min(s.Heat, 1f)));
		s.Root.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(born / 0.3f, 0f, 1f) * (0.55f + 0.1f * breath + 0.45f * Mathf.Min(s.Heat, 1f)));
	}

	private static Seal Create(Control back, Vector2 at)
	{
		Node2D root = Fx.Root(back, at, "P3ThroneSeal");
		root.AddChild(new Sprite2D { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(Gold, 0.35f), Scale = Vector2.One * 3.4f });
		Node2D spin = new();
		root.AddChild(spin);
		Fx.StrokeClosed(spin, Fx.Circle(66f, 64), new Color(Gold, 0.9f), 2.5f);
		Fx.StrokeClosed(spin, Fx.Circle(56f, 64), new Color(Pal.Ice, 0.45f), 1.2f);
		for (int i = 0; i < 12; i++)
		{
			Vector2 d = Vector2.Up.Rotated(Mathf.Tau * i / 12f);
			Fx.Poly(spin, Fx.Star4(i % 3 == 0 ? 7f : 4f, 0.3f), i % 3 == 0 ? Pal.White : new Color(Gold, 0.9f), additive: true).Position = d * 61f;
		}
		// 四道细长的十字光芒（光环的"冠"）
		for (int i = 0; i < 4; i++)
		{
			Vector2 d = Vector2.Up.Rotated(Mathf.Tau * i / 4f + Mathf.Pi / 4f);
			Fx.Stroke(root, new[] { d * 74f, d * 118f }, new Color(Gold, 0.5f), 2f);
		}
		Typo.Text(root, "XX", Typo.LatinItalic, 26, new Color(Gold, 0.95f), new Vector2(-14f, -18f), outline: 2, outlineColor: new Color(0.25f, 0.18f, 0.05f, 0.8f));
		root.SetMeta("age", 0f);
		return new Seal { Root = root, Spin = spin };
	}

	/// <summary>封印王座触发（打出一张牌 → 获得辉星）：光流从手牌区划进印记，印记亮起外扩，下一颗星从印记里凝出。</summary>
	public static void Pulse(NCreature node)
	{
		if (Makoto.FxLayer() is not Control front || Makoto.FxLayer(back: true) is not Control back)
		{
			return;
		}
		Vector2 seal = Anchor(node);
		if (Seals.TryGetValue(node.GetInstanceId(), out Seal? s) && GodotObject.IsInstanceValid(s.Root))
		{
			s.Heat = Mathf.Min(s.Heat + 0.8f, 2.5f);
			seal = s.Root.GlobalPosition;
		}
		StarOrbit.BirthFrom(seal, 0.4f);
		// 光流：手牌区 → 印记（弧线、带尾巴，0.2s）
		Vector2 size = front.GetViewportRect().Size;
		Vector2 from = new(size.X * 0.5f + Fx.Rand(-120f, 120f), size.Y * 0.86f);
		Vector2 ctrl = (from + seal) * 0.5f + new Vector2(-160f, -60f);
		Node2D tr = Fx.Root(front, Vector2.Zero, "P3ThroneFeed");
		tr.TopLevel = true;
		tr.GlobalPosition = Vector2.Zero;
		Polygon2D tail = Fx.Poly(tr, new Vector2[3], new Color(Gold, 0.85f), additive: true);
		Node2D head = new();
		tr.AddChild(head);
		Fx.Poly(head, Fx.Star4(12f, 0.25f), Pal.White, additive: true);
		Fx.Animate(tr, 0.22f, t =>
		{
			float e = Ease.InOutCubic(t);
			var pts = new Vector2[6];
			for (int k = 0; k < 6; k++)
			{
				float u = Mathf.Max(0f, e - 0.3f * (5 - k) / 5f);
				float q = 1f - u;
				pts[k] = from * (q * q) + ctrl * (2f * q * u) + seal * (u * u);
			}
			head.Position = pts[5];
			Fx.Reshape(tail, Fx.StrokePts(pts, 5f, taper: true));
		});
		Fx.After(tr, 0.22, "throne feed land", () =>
		{
			tr.QueueFree();
			P3Fx.Flash(back, seal, Gold, 200f, 0.2f);
			Node2D wave = Fx.Root(back, seal, "P3ThroneWave");
			Fx.StrokeClosed(wave, Fx.Circle(66f, 48), new Color(Gold, 0.9f), 3f);
			Fx.Animate(wave, 0.4f, t =>
			{
				wave.Scale = Vector2.One * (1f + 0.8f * Ease.OutExpo(t));
				wave.Modulate = new Color(1f, 1f, 1f, 1f - t);
			});
			Fx.FreeAfter(wave, 0.42);
		});
	}
}
