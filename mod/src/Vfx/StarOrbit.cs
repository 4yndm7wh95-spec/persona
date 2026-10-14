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
	private const int MaxShown = 12;                     // 显示上限（用户确认可以设上限）：超出的仍有获得动画（飞进轨道后并入），不显示计数；外圈布局代码保留，上限调到 >12 即启用
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
		public float NextBirth;                          // 一次获得多颗时逐张凝出（间隔 BirthGap），不叠在同一帧
		public int LastTotal = -1;                       // 上一帧的真实辉星数（用于超过上限时的获得动画和"+N"）
	}

	private const float BirthGap = 0.13f;

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
		bool glinted = false;
		Vector2 last = from;
		Vector2 side = new(-dir.Y, dir.X);
		Fx.Animate(n, windT + flyT, t =>
		{
			float time = t * (windT + flyT);
			// 细节（v0.14.22）：蓄力到底的一瞬，牌上闪一下十字星光；飞行中沿途洒落星屑，向两侧散开、减速、熄灭
			if (!glinted && time >= windT * 0.8f)
			{
				glinted = true;
				Twinkle(front, n.GlobalPosition, 30f, 0.16f);
			}
			if (time > windT)
			{
				Vector2 now = n.GlobalPosition;
				int k = Mathf.Clamp((int)(now.DistanceTo(last) / 22f), 0, 4);
				for (int m = 0; m < k; m++)
				{
					Vector2 at = last.Lerp(now, (m + Fx.Rand(0f, 1f)) / k);
					Mote(front, at + side * Fx.Rand(-8f, 8f), side * Fx.Rand(-70f, 70f) - dir * Fx.Rand(10f, 40f), Fx.Rand(3.5f, 7f), Fx.Rand(0.25f, 0.45f), 0.9f);
				}
				last = now;
			}
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
			// 细节（v0.14.22）：命中后几粒星屑在目标身上缓缓飘落，闪两下再熄（余韵）
			for (int m = 0; m < 6; m++)
			{
				Vector2 at = target + new Vector2(Fx.Rand(-60f, 60f), Fx.Rand(-50f, 30f));
				Fx.After(front, Fx.Rand(0.04f, 0.2f), "star dust settle", () =>
					Mote(front, at, new Vector2(Fx.Rand(-14f, 14f), Fx.Rand(18f, 40f)), Fx.Rand(4f, 7f), Fx.Rand(0.6f, 0.95f), 1f, blink: true));
			}
			Fx.Animate(trail, 0.18f, t => trail.Modulate = new Color(1f, 1f, 1f, 1f - t));
			Fx.FreeAfter(trailRoot, 0.2);
		});
	}

	/// <summary>一粒星屑：小四芒星，沿 vel 漂移并减速，慢转、淡出；blink 时中途闪一下。</summary>
	private static void Mote(Control layer, Vector2 at, Vector2 vel, float size, float life, float alpha, bool blink = false)
	{
		Node2D root = Fx.Root(layer, at, "P3StarMote");
		Color tint = Fx.Rng.Next(3) == 0 ? Pal.White : Pal.Ice;
		Fx.Poly(root, Fx.Star4(size, 0.22f), tint, additive: true);
		float spin = Fx.Rand(-4f, 4f);
		Fx.Animate(root, life, t =>
		{
			root.GlobalPosition = at + vel * life * Ease.OutCubic(t);
			root.Rotation = spin * t;
			float a = alpha * (1f - Ease.InCubic(t));
			if (blink)
			{
				a *= 0.55f + 0.45f * Mathf.Abs(Mathf.Cos(t * Mathf.Pi * 2f));
			}
			root.Modulate = new Color(1f, 1f, 1f, a);
			root.Scale = Vector2.One * (1f - 0.4f * t);
		});
		Fx.FreeAfter(root, life + 0.05);
	}

	/// <summary>十字星光一闪（pop 后收拢）。</summary>
	private static void Twinkle(Control layer, Vector2 at, float radius, float life)
	{
		Node2D root = Fx.Root(layer, at, "P3StarTwinkle");
		Fx.Poly(root, Fx.Star4(radius, 0.08f), Pal.White, additive: true);
		Polygon2D halo = Fx.Poly(root, Fx.Star4(radius * 1.7f, 0.05f), new Color(Pal.Cyan, 0.55f), additive: true);
		halo.Rotation = Mathf.Pi / 4f;
		Fx.Animate(root, life, t =>
		{
			root.Scale = Vector2.One * (t < 0.3f ? Ease.OutExpo(t / 0.3f) : 1f - Ease.InCubic((t - 0.3f) / 0.7f));
			root.Rotation = t * 0.5f;
		});
		Fx.FreeAfter(root, life + 0.05);
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
			TrackGains(o, total, front);
			Reconcile(o, stars, front, back);
			Layout(o, dt, front, back);
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
		// 一次获得多颗：逐张凝出（每 0.13s 一张），每张都有自己的闪光、火花和音效 —— 不管辉星是从卡牌、能力、回合开始还是别的来源来的
		while (o.Cards.Count < stars && _time >= o.NextBirth)
		{
			o.NextBirth = _time + BirthGap;
			Card c = new() { Node = MakeCard(front), Born = _time, Pos = chest, Front = true };
			c.Node.GlobalPosition = chest;
			o.Cards.Add(c);
			P3Fx.Flash(front, chest, Pal.Cyan, 90f, 0.2f);                // 克制：小闪光、少量火花、音量偏低
			Audio.Sfx.Play(Audio.Snd.StarGain, 0.35f);
			Node2D sparks = Fx.Root(front, chest, "P3StarBirth");
			Fx.Sparks(sparks, Pal.Ice, 5, 60f, 150f, 0.4f);
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
				Twinkle(front, c.Pos, 18f, 0.2f);          // 细节（v0.14.22）：这颗辉星"上膛"留在头顶的一瞬亮一下
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
			// 细节（v0.14.22）：待命的牌底边不断洒下细小的星屑（人物身后图层，很淡，不抢主体）
			if (Fx.Rng.NextDouble() < dt * 5f)
			{
				Mote(back, c.Pos + new Vector2(Fx.Rand(-12f, 12f), 22f), new Vector2(Fx.Rand(-8f, 8f), Fx.Rand(26f, 48f)), Fx.Rand(3f, 5.5f), Fx.Rand(0.5f, 0.8f), 0.7f);
			}
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

	/// <summary>
	/// 任何来源的辉星增加都要看得见：
	/// · 在显示上限以内 —— Reconcile 逐张凝出塔罗牌；
	/// · 超过上限（轨道已满）—— 每多一颗，仍然从胸口凝出一张牌飞到轨道旁，到达后化成光点并入（不显示计数）；
	/// 整体克制：不弹数字，只是一张张小牌安静地凝出、加入轨道。
	/// </summary>
	private static void TrackGains(Orbit o, int total, Control front)
	{
		if (o.LastTotal < 0)
		{
			o.LastTotal = total;
			return;
		}
		int gained = total - o.LastTotal;
		if (gained > 0)
		{
			int over = Math.Max(0, total - Math.Max(o.LastTotal, MaxShown));       // 超出上限、轨道里放不下的那几颗
			for (int i = 0; i < over; i++)
			{
				float delay = BirthGap * i;
				Fx.After(front, delay, "star overflow birth", () => OverflowBirth(o, front));
			}
		}
		o.LastTotal = total;
	}

	/// <summary>轨道已满时的获得：一张牌从胸口凝出，飞到外圈某处，化成光点并入。</summary>
	private static void OverflowBirth(Orbit o, Control front)
	{
		if (!GodotObject.IsInstanceValid(o.Creature))
		{
			return;
		}
		Vector2 chest = Makoto.Center(o.Creature) + new Vector2(0f, -20f);
		float a = Fx.Rand(0f, Mathf.Tau);
		Vector2 to = Makoto.Center(o.Creature) + new Vector2(0f, -10f) + new Vector2(Mathf.Cos(a) * 230f, Mathf.Sin(a) * 46f + 18f);
		Node2D card = MakeCard(front);
		card.GlobalPosition = chest;
		P3Fx.Flash(front, chest, Pal.Cyan, 80f, 0.18f);
		Audio.Sfx.Play(Audio.Snd.StarGain, 0.3f);
		Fx.Animate(card, 0.45f, t =>
		{
			float e = Ease.OutCubic(t);
			card.GlobalPosition = chest.Lerp(to, e) + new Vector2(0f, -40f * Mathf.Sin(t * Mathf.Pi));
			card.Rotation = (1f - e) * 3f;
			card.Scale = Vector2.One * (0.8f * Ease.OutBack(Mathf.Min(t * 2f, 1f)) * (1f - 0.6f * Mathf.Clamp((t - 0.7f) / 0.3f, 0f, 1f)));
			card.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp((t - 0.75f) / 0.25f, 0f, 1f));
		});
		Fx.After(card, 0.45, "star overflow merge", () =>
		{
			P3Fx.Flash(front, to, Pal.Ice, 50f, 0.12f);
			card.QueueFree();
		});
	}
}
