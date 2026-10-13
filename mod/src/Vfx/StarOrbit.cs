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
/// - 预备打出要花 N 颗辉星的牌：N 张牌离开轨道，升到他后脑勺偏上的位置排成拱形待命（发着光）；取消则飞回轨道。
/// - 消耗：这 N 张牌向前飞出、翻面，碎成玻璃和蓝蝶。
/// 每帧对照真实的辉星数量（PlayerCombatState.Stars）对账，所以任何来源的增减都会有对应动画。
/// - 星尘（ArmVolley）：花掉的牌不碎，留在头顶排成一排待命；每一击 Throw 掷出一颗砸向目标。
/// 轨道是压扁的椭圆：后半圈的牌放在人物身后的特效层，前半圈放在身前，读起来像真的绕着他转。
/// </summary>
public static class StarOrbit
{
	private const int MaxShown = 24;                     // v0.14.0：两圈轨道（内圈 12 + 外圈 12），再多显示"×N"计数，不再出现"加了星却没动画"
	private const int RingSize = 12;
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
		public readonly List<Card> Quiver = new();       // 星尘：已花掉、等待掷出的牌
		public float VolleyUntil;
		public Label? Overflow;                          // 超过 MaxShown 时的"×N"
	}

	/// <summary>封印王座等：下一颗（几颗）辉星从这里凝出，而不是胸口（until 之前有效）。</summary>
	private static Vector2? _birthFrom;
	private static float _birthUntil;

	public static void BirthFrom(Vector2 at, float seconds)
	{
		_birthFrom = at;
		_birthUntil = _time + seconds;
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

	/// <summary>星尘：接下来花掉的辉星留在头顶，等每一击掷出（几秒内没掷完的照常碎掉）。</summary>
	public static void ArmVolley(NCreature creature)
	{
		ulong id = creature.GetInstanceId();
		if (!Orbits.TryGetValue(id, out Orbit? o))
		{
			o = new Orbit { Creature = creature };
			Orbits[id] = o;
		}
		o.VolleyUntil = _time + 4f;
	}

	public static void DisarmVolley()
	{
		foreach (Orbit o in Orbits.Values)
		{
			o.VolleyUntil = 0f;
		}
	}

	/// <summary>掷出一颗待命的辉星砸向 target，落地时调用 onImpact。没有待命的牌就从头顶凝出一颗。</summary>
	public static void Throw(NCreature creature, Vector2 target, Action onImpact)
	{
		Control? front = Makoto.FxLayer();
		if (front == null)
		{
			onImpact();
			return;
		}
		Node2D n;
		if (Orbits.TryGetValue(creature.GetInstanceId(), out Orbit? oo))
		{
			oo.VolleyUntil = Math.Max(oo.VolleyUntil, _time + 2f);    // 还在连续掷出：待命状态续期
		}
		if (Orbits.TryGetValue(creature.GetInstanceId(), out Orbit? o) && o.Quiver.Count > 0)
		{
			Card c = o.Quiver[0];
			o.Quiver.RemoveAt(0);
			n = c.Node;
			if (!GodotObject.IsInstanceValid(n))
			{
				onImpact();
				return;
			}
			if (!c.Front)
			{
				n.Reparent(front);
			}
		}
		else
		{
			n = MakeCard(front);
			n.GlobalPosition = QuiverSlot(creature, 0, 1);
		}
		Vector2 from = n.GlobalPosition;
		Vector2 dir = (target - from).Normalized();
		// 先往后一缩（蓄力），再直线砸下：拖一道青白色的光尾
		Vector2 wind = from - dir * 26f;
		Line2D trail = new() { Width = 16f, Material = Fx.Additive, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round,
			Gradient = new Gradient { Colors = new[] { new Color(Pal.Cyan, 0f), new Color(Pal.Cyan, 0.55f), new Color(Pal.White, 0.95f) }, Offsets = new[] { 0f, 0.6f, 1f } } };
		Node2D trailRoot = Fx.Root(front, Vector2.Zero, "P3StarThrowTrail");
		trailRoot.TopLevel = true;
		trailRoot.GlobalPosition = Vector2.Zero;
		trailRoot.AddChild(trail);
		const float windT = 0.06f, flyT = 0.11f;
		Audio.Sfx.Play(Audio.Snd.StarSpend, 0.45f);
		Fx.Animate(n, windT + flyT, t =>
		{
			float time = t * (windT + flyT);
			if (time < windT)
			{
				n.GlobalPosition = from.Lerp(wind, Ease.OutCubic(time / windT));
				n.Scale = Vector2.One * (1.25f + 0.25f * time / windT);
			}
			else
			{
				float u = Ease.InCubic((time - windT) / flyT);
				Vector2 p = wind.Lerp(target, u);
				n.GlobalPosition = p;
				n.Rotation = dir.Angle() + Mathf.Pi / 2f;
				n.Scale = new Vector2(1.1f, 1.5f + 1.2f * u);
				trail.Points = new[] { wind.Lerp(p, Mathf.Max(0f, u - 0.55f) / 1f), p };
			}
		});
		Fx.After(n, windT + flyT, "star throw hit", () =>
		{
			n.QueueFree();
			onImpact();
			P3Fx.GlassShatter(front, target, 8, 0.4f, Pal.Ice);
			Fx.Animate(trail, 0.18f, t => trail.Modulate = new Color(1f, 1f, 1f, 1f - t));
			Fx.FreeAfter(trailRoot, 0.2);
		});
	}

	/// <summary>星尘待命位：头顶后上方一排（人物身后），中间最高。</summary>
	private static Vector2 QuiverSlot(NCreature creature, int i, int count)
	{
		float u = count <= 1 ? 0.5f : i / (float)(count - 1);
		float span = Mathf.Min(56f * (count - 1), 420f);
		return Makoto.Head(creature) + new Vector2(-120f + span * (u - 0.5f), -170f - 40f * Mathf.Sin(u * Mathf.Pi) + 5f * Mathf.Sin(_time * 3f + i));
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
			int total = node.Entity.IsDead ? 0 : player.PlayerCombatState?.Stars ?? 0;
			int stars = Math.Min(total, MaxShown);
			Reconcile(o, stars, front, back);
			Layout(o, dt, front, back);
			UpdateOverflow(o, total, front);
			ThroneSeal.Tick(node, back, front, _time);
		}
	}

	private static void Reconcile(Orbit o, int stars, Control front, Control back)
	{
		Vector2 chest = Makoto.Center(o.Creature) + new Vector2(0f, -20f);
		if (_birthFrom is Vector2 from && _time <= _birthUntil)
		{
			chest = from;
		}
		while (o.Cards.Count < stars)
		{
			Card c = new() { Node = MakeCard(front), Born = _time, Pos = chest, Front = true };
			c.Node.GlobalPosition = chest;
			o.Cards.Add(c);
			P3Fx.Flash(front, chest, Pal.Cyan, 160f, 0.25f);
			Audio.Sfx.Play(Audio.Snd.StarGain, 0.5f);
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
			if (_time < o.VolleyUntil)
			{
				o.Quiver.Add(c);
			}
			else
			{
				Spend(c, front);
			}
		}
		o.Preview = Math.Min(o.Preview, o.Cards.Count);
		// 星尘待命超时（敌人提前死光等）：剩下的照常碎掉
		if (o.Quiver.Count > 0 && _time > o.VolleyUntil)
		{
			foreach (Card c in o.Quiver)
			{
				Spend(c, front);
			}
			o.Quiver.Clear();
		}
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
				// 待命：升到后脑勺后上方（人物身后图层，不挡人），排成一道拱形（中间最高），轻轻上下浮动
				float u = o.Preview == 1 ? 0.5f : i / (float)(o.Preview - 1);
				Vector2 above = Makoto.Head(o.Creature) + new Vector2(-165f, -150f);
				float span = Mathf.Min(48f * (o.Preview - 1), 300f);
				target = above + new Vector2(span * (u - 0.5f), -34f * Mathf.Sin(u * Mathf.Pi) + 5f * Mathf.Sin(_time * 3f + i));
				depth = -1f;
				scale = 1.2f + 0.05f * Mathf.Sin(_time * 6f + i);
			}
			else
			{
				int j = i - o.Preview;
				// 内圈最多 12 张；更多的进外圈（更大、更扁、反向慢转、牌小一号），两圈错开不打架
				int ring = j < RingSize ? 0 : 1;
				int inRing = ring == 0 ? Math.Min(orbiting, RingSize) : orbiting - RingSize;
				int jr = ring == 0 ? j : j - RingSize;
				float rx0 = 118f + 4f * Math.Min(orbiting, RingSize);
				float a = (ring == 0 ? o.Phase : -o.Phase * 0.7f + 0.3f) + Mathf.Tau * jr / Math.Max(inRing, 1);
				float rxr = ring == 0 ? rx0 : rx0 + 64f, ryr = ring == 0 ? ry : ry + 16f;
				target = center + new Vector2(rxr * Mathf.Cos(a), ryr * Mathf.Sin(a) + 6f * Mathf.Sin(_time * 1.3f + j) + (ring == 1 ? 18f : 0f));
				depth = Mathf.Sin(a);
				scale = (0.82f + 0.16f * depth) * (ring == 0 ? 1f : 0.8f);
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
			float shade = i < o.Preview ? 1f : 0.55f + 0.45f * (depth * 0.5f + 0.5f);
			c.Node.Modulate = new Color(1f, 1f, 1f, shade * born + (1f - born) * born);
		}
		// 星尘待命：在头顶排开，发亮、轻轻浮动，牌面朝前
		for (int i = 0; i < o.Quiver.Count; i++)
		{
			Card c = o.Quiver[i];
			if (!GodotObject.IsInstanceValid(c.Node))
			{
				continue;
			}
			if (c.Front)
			{
				c.Node.Reparent(back);
				c.Front = false;
			}
			c.Pos = c.Pos.Lerp(QuiverSlot(o.Creature, i, o.Quiver.Count), 1f - Mathf.Exp(-14f * dt));
			c.Node.GlobalPosition = c.Pos;
			c.Node.Rotation = Mathf.LerpAngle(c.Node.Rotation, 0f, 0.3f);
			c.Node.Scale = Vector2.One * (1.25f + 0.06f * Mathf.Sin(_time * 7f + i));
			c.Node.Modulate = Colors.White;
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
			Audio.Sfx.Play(Audio.Snd.StarSpend, 0.65f);
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

	/// <summary>超过 MaxShown 颗时，在轨道右侧显示"×N"（总数），每次变化轻轻弹一下。</summary>
	private static void UpdateOverflow(Orbit o, int total, Control front)
	{
		bool show = total > MaxShown;
		if (!show)
		{
			if (o.Overflow != null && GodotObject.IsInstanceValid(o.Overflow))
			{
				o.Overflow.QueueFree();
			}
			o.Overflow = null;
			return;
		}
		if (o.Overflow == null || !GodotObject.IsInstanceValid(o.Overflow))
		{
			o.Overflow = Typo.Text(front, "", Typo.LatinItalic, 34, Pal.Ice, Vector2.Zero, outline: 4, outlineColor: Pal.Abyss);
			o.Overflow.TopLevel = true;
		}
		string text = "×" + total;
		if (o.Overflow.Text != text)
		{
			o.Overflow.Text = text;
			o.Overflow.Scale = Vector2.One * 1.35f;
		}
		o.Overflow.Scale = o.Overflow.Scale.Lerp(Vector2.One, 0.15f);
		o.Overflow.GlobalPosition = Makoto.Center(o.Creature) + new Vector2(200f, -60f);
	}
}
