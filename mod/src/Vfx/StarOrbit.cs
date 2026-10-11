using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using P3Makoto.Core;

namespace P3Makoto.Vfx;

/// <summary>
/// 辉星实体化：结城理持有的每一颗辉星 = 一张环绕在他身边的蓝色塔罗牌（大阿卡纳；他是"愚者"——Wild Card）。
/// - 获得：一张牌从他胸口的蓝光里凝出，翻转着飞进环绕轨道。
/// - 预备打出要花 N 颗辉星的牌：N 张牌离开轨道，在他身前排开待命（发着光）；取消则飞回轨道。
/// - 消耗：这 N 张牌向前飞出、翻面，碎成玻璃和蓝蝶。
/// 每帧对照真实的辉星数量（PlayerCombatState.Stars）对账，所以任何来源的增减都会有对应动画。
/// 轨道是压扁的椭圆：后半圈的牌放在人物身后的特效层，前半圈放在身前，读起来像真的绕着他转。
/// </summary>
public static class StarOrbit
{
	private const int MaxShown = 12;
	private const float SpinSpeed = 0.55f;               // 轨道角速度（弧度 / 秒）

	private sealed class Card
	{
		public Node2D Node = null!;
		public float Born;                               // 出生时刻（用于凝出动画）
		public Vector2 Pos;
		public bool Front;
	}

	private sealed class Orbit
	{
		public NCreature Creature = null!;
		public readonly List<Card> Cards = new();
		public int Preview;                              // 当前在身前待命的张数
		public float Phase;
	}

	private static readonly Dictionary<ulong, Orbit> Orbits = new();
	private static NCombatRoom? _room;
	private static float _time;
	private static bool _hooked;

	public static void Install()
	{
		if (_hooked || Engine.GetMainLoop() is not SceneTree tree)
		{
			return;
		}
		_hooked = true;
		tree.ProcessFrame += () => ModEntry.Safe("star orbit", Tick);
	}

	/// <summary>拖牌时：这张牌要花 n 颗辉星（-1 = X 费，全部）。</summary>
	public static void Preview(NCreature creature, int n)
	{
		if (Orbits.TryGetValue(creature.GetInstanceId(), out Orbit? o))
		{
			o.Preview = n < 0 ? o.Cards.Count : Math.Min(n, o.Cards.Count);
		}
	}

	public static void ClearPreview()
	{
		foreach (Orbit o in Orbits.Values)
		{
			o.Preview = 0;
		}
	}

	private static void Tick()
	{
		NCombatRoom? room = NCombatRoom.Instance;
		if (room == null || !GodotObject.IsInstanceValid(room))
		{
			Orbits.Clear();
			_room = null;
			return;
		}
		if (room != _room)
		{
			Orbits.Clear();
			_room = room;
		}
		float dt = (float)room.GetProcessDeltaTime();
		_time += dt;
		Control? front = Makoto.FxLayer();
		Control? back = Makoto.FxLayer(back: true);
		if (front == null || back == null)
		{
			return;
		}
		foreach (NCreature node in room.CreatureNodes)
		{
			if (node.Entity.Player is not Player player || !Makoto.Is(player) || !GodotObject.IsInstanceValid(node))
			{
				continue;
			}
			ulong id = node.GetInstanceId();
			if (!Orbits.TryGetValue(id, out Orbit? o))
			{
				o = new Orbit { Creature = node };
				Orbits[id] = o;
			}
			int stars = node.Entity.IsDead ? 0 : Math.Min(player.PlayerCombatState?.Stars ?? 0, MaxShown);
			Reconcile(o, stars, front, back);
			Layout(o, dt, front, back);
		}
	}

	private static void Reconcile(Orbit o, int stars, Control front, Control back)
	{
		Vector2 chest = Makoto.Center(o.Creature) + new Vector2(0f, -20f);
		while (o.Cards.Count < stars)
		{
			Card c = new() { Node = MakeCard(front), Born = _time, Pos = chest, Front = true };
			c.Node.GlobalPosition = chest;
			o.Cards.Add(c);
			P3Fx.Flash(front, chest, Pal.Cyan, 160f, 0.25f);
			Node2D sparks = Fx.Root(front, chest, "P3StarBirth");
			Fx.Sparks(sparks, Pal.Ice, 10, 80f, 220f, 0.5f);
			Fx.FreeAfter(sparks, 0.8);
		}
		while (o.Cards.Count > stars)
		{
			// 优先消耗正在身前待命的牌；没有就取轨道最前面的一张
			int idx = o.Preview > 0 ? 0 : FrontMost(o);
			Card c = o.Cards[idx];
			o.Cards.RemoveAt(idx);
			if (o.Preview > 0)
			{
				o.Preview--;
			}
			Spend(c, front);
		}
		o.Preview = Math.Min(o.Preview, o.Cards.Count);
	}

	private static int FrontMost(Orbit o)
	{
		int best = o.Cards.Count - 1;
		float by = float.MinValue;
		for (int i = 0; i < o.Cards.Count; i++)
		{
			if (o.Cards[i].Pos.Y > by)
			{
				by = o.Cards[i].Pos.Y;
				best = i;
			}
		}
		return best;
	}

	private static void Layout(Orbit o, float dt, Control front, Control back)
	{
		o.Phase += SpinSpeed * dt;
		Vector2 center = Makoto.Center(o.Creature) + new Vector2(0f, -10f);
		int orbiting = o.Cards.Count - o.Preview;
		float rx = 118f + 4f * orbiting, ry = 30f;
		float k = 1f - Mathf.Exp(-10f * dt);
		for (int i = 0; i < o.Cards.Count; i++)
		{
			Card c = o.Cards[i];
			Vector2 target;
			float depth;
			float scale;
			if (i < o.Preview)
			{
				// 身前待命：在胸前排成一道向上扬的弧，朝向敌人一侧
				float u = o.Preview == 1 ? 0.5f : i / (float)(o.Preview - 1);
				target = center + new Vector2(70f + 150f * u, -70f - 40f * Mathf.Sin(u * Mathf.Pi) - 10f * i);
				depth = 1f;
				scale = 1.15f + 0.05f * Mathf.Sin(_time * 6f + i);
			}
			else
			{
				int j = i - o.Preview;
				float a = o.Phase + Mathf.Tau * j / Math.Max(orbiting, 1);
				target = center + new Vector2(rx * Mathf.Cos(a), ry * Mathf.Sin(a) + 6f * Mathf.Sin(_time * 1.3f + j));
				depth = Mathf.Sin(a);
				scale = 0.82f + 0.16f * depth;
			}
			c.Pos = c.Pos.Lerp(target, k);
			float born = Mathf.Clamp((_time - c.Born) / 0.4f, 0f, 1f);
			bool wantFront = depth >= 0f;
			if (wantFront != c.Front)
			{
				c.Node.Reparent(wantFront ? front : back);
				c.Front = wantFront;
			}
			c.Node.GlobalPosition = c.Pos;
			float s = scale * Ease.OutBack(born);
			// 牌面缓慢翻转（宽度随 cos 变化），出生时快速旋入
			float flip = Mathf.Cos(_time * 1.6f + i * 0.9f) * 0.25f + 0.75f;
			c.Node.Scale = new Vector2(s * flip, s);
			c.Node.Rotation = (1f - born) * 3f + 0.08f * Mathf.Sin(_time * 2f + i);
			c.Node.Modulate = new Color(1f, 1f, 1f, (0.55f + 0.45f * (depth * 0.5f + 0.5f)) * born + (1f - born) * born);
		}
	}

	private static void Spend(Card c, Control front)
	{
		Node2D n = c.Node;
		if (!GodotObject.IsInstanceValid(n))
		{
			return;
		}
		if (!c.Front)
		{
			n.Reparent(front);
		}
		Vector2 from = n.GlobalPosition;
		Vector2 to = from + new Vector2(Fx.Rand(60f, 140f), Fx.Rand(-140f, -90f));
		Fx.Animate(n, 0.28f, t =>
		{
			float e = Ease.OutCubic(t);
			n.GlobalPosition = from.Lerp(to, e);
			n.Scale = new Vector2(1.3f * Mathf.Cos(t * Mathf.Pi * 1.5f), 1.3f);
			n.Rotation = -0.4f * e;
		});
		Fx.After(n, 0.28, "star card shatter", () =>
		{
			Vector2 at = n.GlobalPosition;
			P3Fx.GlassShatter(front, at, 10, 0.5f, Pal.Ice);
			P3Fx.Flash(front, at, Pal.Cyan, 140f, 0.18f);
			P3Fx.Butterfly(front, at, at + new Vector2(Fx.Rand(-60f, 80f), Fx.Rand(-220f, -140f)), 1.2f, 0.55f);
			n.QueueFree();
		});
	}

	/// <summary>一张小塔罗牌：深蓝牌面 + 青色边框 + 白色四芒星纹章 + 柔光。</summary>
	private static Node2D MakeCard(Control layer)
	{
		Node2D root = new() { Name = "P3StarCard" };
		layer.AddChild(root);
		Sprite2D glow = new() { Texture = Fx.SoftDot, Scale = new Vector2(2.4f, 3.2f), Modulate = new Color(Pal.Cyan, 0.45f), Material = Fx.Additive };
		root.AddChild(glow);
		const float w = 15f, h = 23f;
		Vector2[] rect = { new(-w, -h), new(w, -h), new(w, h), new(-w, h) };
		Fx.Poly(root, rect, new Color(Pal.Abyss, 0.95f));
		Fx.Line(root, rect, Pal.Cyan, 2f, closed: true, additive: false);
		Fx.Line(root, new[] { new Vector2(-w + 4f, -h + 4f), new Vector2(w - 4f, -h + 4f), new Vector2(w - 4f, h - 4f), new Vector2(-w + 4f, h - 4f) },
			new Color(Pal.Royal, 0.9f), 1f, closed: true, additive: false);
		Fx.Poly(root, Fx.Star4(9f, 0.3f), Pal.Ice, additive: true);
		return root;
	}
}
