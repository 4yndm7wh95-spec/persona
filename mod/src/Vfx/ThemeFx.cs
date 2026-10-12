using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using P3Makoto.Core;

namespace P3Makoto.Vfx;

/// <summary>
/// P3 主题物件的特效（贴图来自 art/fx，打包到 res://p3makoto/fx/，见 tools/ui/build_fx_assets.py）：
/// - VelvetGate：创世之柱 —— 天鹅绒房间的门在人物左后方从"水面"下升起并常驻；每次生成卡牌，门顶的楼层指针扫到底"叮"一声，
///   门打开（门后是一直在上升的笼式电梯），放出光和蓝蝶，再砰地关上。
/// - Contract：既定事项 —— 伊戈尔的契约：卷轴从掌心飞到头顶左上方展开，签名、盖封蜡，卷起后化成两只蓝蝶飞进胸口。
/// - Compendium：预言 —— 全书从掌心飞出、在头顶左上方翻开，书页一张接一张快速翻过，书上升起光柱和旋转的法阵，合上。
/// - ShuffleTime：抉择，抉择 —— 四张塔罗牌扇开洗牌、越转越快，停下逐张翻成小阿卡纳，选中的剑牌放大一闪，其余碎掉。
/// 物件都放在人物头顶左上方（和命运之轮同一位置），不挡人。
/// </summary>
public static class ThemeFx
{
	private const string Dir = "res://p3makoto/fx/";
	private static readonly Dictionary<string, Texture2D?> Cache = new();

	private static Texture2D? Tex(string name)
	{
		if (!Cache.TryGetValue(name, out Texture2D? t))
		{
			string p = Dir + name + ".png";
			t = ResourceLoader.Exists(p) ? ResourceLoader.Load<Texture2D>(p) : null;
			Cache[name] = t;
		}
		return t;
	}

	private static Sprite2D Sprite(Node parent, string tex, Vector2 pos, bool centered = true)
	{
		Sprite2D s = new() { Texture = Tex(tex), Position = pos, Centered = centered };
		parent.AddChild(s);
		return s;
	}

	/// <summary>一个矩形裁切容器（子节点只在矩形内显示）。</summary>
	private static Polygon2D Clip(Node parent, Rect2 r)
	{
		Polygon2D clip = new() { Polygon = RectPts(r), ClipChildren = CanvasItem.ClipChildrenMode.Only };
		parent.AddChild(clip);
		return clip;
	}

	private static Vector2[] RectPts(Rect2 r) => new[] { r.Position, new Vector2(r.End.X, r.Position.Y), r.End, new Vector2(r.Position.X, r.End.Y) };

	private static Vector2 Bezier(Vector2 a, Vector2 c, Vector2 b, float t)
	{
		float k = 1f - t;
		return a * (k * k) + c * (2f * k * t) + b * (t * t);
	}

	/// <summary>物件的标准位置：头顶左上方（命运之轮同一位置），不压到顶栏。</summary>
	public static Vector2 Spot(NCreature caster, float minY = 150f)
	{
		Vector2 at = Makoto.Head(caster) + new Vector2(-235f, -125f);
		at.Y = Mathf.Max(at.Y, minY);
		at.X = Mathf.Max(at.X, 150f);
		return at;
	}

	private static Vector2 Hand(NCreature caster) => WeaponFx.Bone(caster, "hand") ?? Makoto.Center(caster) + new Vector2(60f, -40f);

	/// <summary>从 from 沿弧线飞到 to 的一小段光尾（物件飞入时用）。</summary>
	private static void Trail(Control layer, Vector2 from, Vector2 ctrl, Vector2 to, float duration, Color tint)
	{
		Node2D root = Fx.Root(layer, Vector2.Zero, "P3ThemeTrail");
		root.TopLevel = true;
		root.GlobalPosition = Vector2.Zero;
		Line2D line = new()
		{
			Width = 6f, Material = Fx.Additive, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round,
			Gradient = new Gradient { Colors = new[] { new Color(tint, 0f), new Color(Pal.White, 0.9f) } }
		};
		root.AddChild(line);
		Fx.Animate(root, duration + 0.15f, t =>
		{
			float time = t * (duration + 0.15f);
			float head = Ease.OutCubic(Mathf.Clamp(time / duration, 0f, 1f));
			float tail = Ease.OutCubic(Mathf.Clamp((time - 0.08f) / duration, 0f, 1f));
			var pts = new Vector2[8];
			for (int i = 0; i < 8; i++)
			{
				pts[i] = Bezier(from, ctrl, to, Mathf.Lerp(tail, head, i / 7f));
			}
			line.Points = pts;
			line.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp((time - duration) / 0.15f, 0f, 1f));
		});
		Fx.FreeAfter(root, duration + 0.2f);
	}

	// ══════════════════════ 天鹅绒门（创世之柱）══════════════════════

	// 门框贴图 512×768（原点 = 门底中心）；开口 x 138–374、y 181–750；楼层指示盘中心 (256,158)
	private static readonly Rect2 Opening = new(138f - 256f, 181f - 768f, 236f, 569f);
	private static readonly Vector2 Dial = new(0f, 158f - 768f);
	private const float DoorScale = 0.64f;

	private sealed class Gate
	{
		public Node2D Root = null!;
		public Node2D Body = null!;
		public Sprite2D Leaf = null!;
		public Sprite2D FrameFlash = null!;
		public Node2D Interior = null!;
		public Polygon2D[] Bars = Array.Empty<Polygon2D>();
		public Polygon2D Needle = null!;
		public Line2D Crack = null!;
		public float Open;                  // 0 关 → 1 开
		public float NeedleT;               // 0 最左 → 1 最右
		public bool Busy;
		public float Time;
	}

	private static Gate? _gate;

	private static bool GateAlive => _gate != null && GodotObject.IsInstanceValid(_gate.Root);

	/// <summary>打出创世之柱：门从水面下升起并常驻（已经在场就只闪一下）。</summary>
	public static void SummonGate(NCreature caster)
	{
		Control? back = Makoto.FxLayer(back: true);
		Control? front = Makoto.FxLayer();
		if (back == null || front == null)
		{
			return;
		}
		if (GateAlive)
		{
			Fx.Animate(_gate!.FrameFlash, 0.3f, t => _gate.FrameFlash.Modulate = new Color(1f, 1f, 1f, 0.6f * (1f - t)));
			return;
		}
		Vector2 floor = Makoto.Feet(caster) + new Vector2(-250f, 0f);
		Gate g = new();
		g.Root = Fx.Root(back, floor, "P3VelvetGate");
		g.Root.Scale = Vector2.One * DoorScale;

		// 水面之上才显示（门从地面下升起 / 沉下去）
		Polygon2D ground = Clip(g.Root, new Rect2(-900f, -2400f, 1800f, 2400f));
		g.Body = new Node2D();
		ground.AddChild(g.Body);

		// 门后：笼式电梯 —— 深蓝底 + 一直向下滑过的井道光带 + 铁艺格栅。
		// 注意：不能再套一层 ClipChildren（嵌套裁切在游戏里会把裁切多边形画成一整块白色），光带每帧手动截到门洞范围内。
		g.Interior = new Node2D();
		g.Body.AddChild(g.Interior);
		Fx.Poly(g.Interior, RectPts(Opening), new Color(0.02f, 0.05f, 0.24f));
		float[] heights = { 30f, 12f, 48f, 18f, 9f, 26f };
		g.Bars = new Polygon2D[heights.Length];
		for (int i = 0; i < heights.Length; i++)
		{
			g.Bars[i] = Fx.Poly(g.Interior, RectPts(new Rect2(Opening.Position.X, 0f, Opening.Size.X, heights[i])), new Color(i % 2 == 0 ? Pal.White : Pal.Ice, 0.9f), additive: true);
			g.Bars[i].SetMeta("h", heights[i]);
		}
		Sprite(g.Interior, "door_grille", Opening.Position, centered: false);

		// 门板：铰链在左，打开时宽度按 cos 收窄、越开越暗
		g.Leaf = Sprite(g.Body, "door_leaf", Opening.Position, centered: false);
		// 门框 + 闪白用的加色副本
		Sprite(g.Body, "door_frame", new Vector2(-256f, -768f), centered: false);
		g.FrameFlash = Sprite(g.Body, "door_frame", new Vector2(-256f, -768f), centered: false);
		g.FrameFlash.Material = Fx.Additive;
		g.FrameFlash.Modulate = new Color(1f, 1f, 1f, 0f);
		// 楼层指示：盖掉画上的指针，换一根会转的
		Fx.Poly(g.Body, RectPts(new Rect2(Dial + new Vector2(-5f, -66f), new Vector2(10f, 54f))), new Color(0.035f, 0.12f, 0.28f));
		Node2D needleRoot = new() { Position = Dial };
		g.Body.AddChild(needleRoot);
		g.Needle = Fx.Poly(needleRoot, new[] { new Vector2(0f, -61f), new Vector2(3f, 0f), new Vector2(0f, 6f), new Vector2(-3f, 0f) }, new Color(0.93f, 0.93f, 0.96f));
		Fx.Poly(needleRoot, Fx.Circle(4.5f, 12), new Color(0.8f, 0.8f, 0.86f));
		// 门缝透出的光（常驻时呼吸）
		g.Crack = Fx.Line(g.Body, new[] { new Vector2(Opening.End.X - 3f, Opening.Position.Y + 6f), new Vector2(Opening.End.X - 3f, Opening.End.Y) }, new Color(Pal.Cyan, 0f), 6f);
		_gate = g;
		Apply(g);

		// 1) 蓝蝶从右上飞来、落在门的位置，落点泛开波纹
		Vector2 land = floor + new Vector2(0f, -12f);
		P3Fx.Butterfly(front, land + new Vector2(320f, -300f), land, 0.32f, 0.9f);
		Fx.After(g.Root, 0.3, "gate land", () =>
		{
			P3Fx.Flash(back, land, Pal.Ice, 150f, 0.15f);
			for (int i = 0; i < 3; i++)
			{
				WaterRing(back, floor, 0.12f * i);
			}
			P3Fx.Ripple(back, floor, 700f, 0.9f, 0.025f, squashY: 0.3f);
		});
		// 2) 门从水下升起（过冲后落定），底部溅起水花；3) 锁定：门框闪白 + 门形回声
		g.Body.Position = new Vector2(0f, 800f);
		Fx.After(g.Root, 0.3, "gate rise", () =>
		{
			Splash(back, floor, 1f);
			Audio.Sfx.Play(Audio.Snd.TarotReveal, 0.8f);
			Fx.Animate(g.Body, 0.34f, t => g.Body.Position = new Vector2(0f, 800f * (1f - Ease.OutBack(t))));
		});
		Fx.After(g.Root, 0.64, "gate lock", () =>
		{
			Fx.Animate(g.FrameFlash, 0.25f, t => g.FrameFlash.Modulate = new Color(1f, 1f, 1f, 0.7f * (1f - t)));
			Echo(back, floor);
		});
		// 常驻：门缝光呼吸、井道光带滑动；隔几秒从门缝飘出一只蓝蝶
		Timer tick = new() { WaitTime = 1.0 / 30.0, Autostart = true };
		g.Root.AddChild(tick);
		tick.Timeout += () => ModEntry.Safe("gate tick", () => Tick(g, (float)tick.WaitTime));
		Timer moth = new() { WaitTime = 4.5, Autostart = true };
		g.Root.AddChild(moth);
		moth.Timeout += () => ModEntry.Safe("gate moth", () =>
		{
			if (!g.Busy && Makoto.FxLayer() is Control f)
			{
				Vector2 crack = g.Root.ToGlobal(new Vector2(Opening.End.X, Opening.GetCenter().Y));
				P3Fx.Butterfly(f, crack, crack + new Vector2(Fx.Rand(-80f, 40f), Fx.Rand(-280f, -200f)), 1.6f, 0.7f);
			}
		});
	}

	/// <summary>生成卡牌时（创世之柱给格挡的那一刻）：指针扫到底"叮" → 开门放光 → 砰地关上。</summary>
	public static void GatePulse()
	{
		if (!GateAlive || _gate!.Busy)
		{
			return;
		}
		Gate g = _gate;
		g.Busy = true;
		Control? back = Makoto.FxLayer(back: true);
		Control? front = Makoto.FxLayer();
		// 指针一格一格抖着扫到最右（0.3s）
		Fx.Animate(g.Needle, 0.3f, t =>
		{
			g.NeedleT = Ease.OutCubic(t) + 0.04f * Mathf.Sin(t * Mathf.Pi * 6f) * (1f - t);
			Apply(g);
		});
		Fx.After(g.Root, 0.3, "gate ding", () =>
		{
			Vector2 tip = g.Root.ToGlobal(Dial + Vector2.Up.Rotated(Mathf.DegToRad(74f)) * 61f);
			if (front != null)
			{
				P3Fx.Flash(front, tip, Pal.White, 70f, 0.2f);
			}
			Audio.Sfx.Play(Audio.Snd.Weak, 0.45f, pitch: 1.3f);
			Fx.Animate(g.Leaf, 0.16f, t => { g.Open = Ease.OutCubic(t); Apply(g); });
			if (back != null && front != null)
			{
				Vector2 c = g.Root.ToGlobal(Opening.GetCenter());
				Rays(back, c);
				for (int i = 0; i < 3; i++)
				{
					P3Fx.Butterfly(front, c, c + new Vector2(Fx.Rand(80f, 380f), Fx.Rand(-360f, -160f)), 0.9f, 0.8f);
				}
			}
		});
		Fx.After(g.Root, 0.92, "gate slam", () =>
		{
			Fx.Animate(g.Leaf, 0.08f, t => { g.Open = 1f - Ease.InCubic(t); Apply(g); });
		});
		Fx.After(g.Root, 1.0, "gate shut", () =>
		{
			Fx.Animate(g.FrameFlash, 0.15f, t => g.FrameFlash.Modulate = new Color(1f, 1f, 1f, 0.45f * (1f - t)));
			MegaCrit.Sts2.Core.Nodes.NGame.Instance?.ScreenShake(MegaCrit.Sts2.Core.Nodes.Vfx.Utilities.ShakeStrength.Weak, MegaCrit.Sts2.Core.Nodes.Vfx.Utilities.ShakeDuration.Short);
			Fx.Animate(g.Needle, 0.3f, t => { g.NeedleT = 1f - Ease.OutCubic(t); Apply(g); });
		});
		Fx.After(g.Root, 1.32, "gate idle", () => g.Busy = false);
	}

	private static void Apply(Gate g)
	{
		float ang = g.Open * Mathf.DegToRad(78f);
		float w = Mathf.Cos(ang);
		g.Leaf.Scale = new Vector2(Mathf.Max(0.02f, w), 1f);
		float shade = 1f - 0.55f * g.Open;
		g.Leaf.Modulate = new Color(shade, shade, shade);
		g.Interior.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(g.Open * 3f, 0f, 1f));
		g.Needle.GetParent<Node2D>().Rotation = Mathf.DegToRad(-74f + 148f * g.NeedleT);
	}

	private static void Tick(Gate g, float dt)
	{
		g.Time += dt;
		// 井道光带：一条条向下滑过（电梯在上升）
		float h = Opening.Size.Y + 80f;
		float[] phase = { 0f, 0.27f, 0.45f, 0.71f, 0.86f, 0.6f };
		for (int i = 0; i < g.Bars.Length; i++)
		{
			float y = Opening.Position.Y - 60f + ((phase[i] + g.Time * 1.1f) % 1f) * h;
			float bh = (float)g.Bars[i].GetMeta("h");
			float y0 = Mathf.Max(y, Opening.Position.Y), y1 = Mathf.Min(y + bh, Opening.End.Y);
			g.Bars[i].Visible = y1 > y0;
			if (y1 > y0)
			{
				Fx.Reshape(g.Bars[i], RectPts(new Rect2(Opening.Position.X, y0, Opening.Size.X, y1 - y0)));
			}
		}
		// 门缝光：关着时呼吸，开着时隐去
		float a = (0.35f + 0.25f * Mathf.Sin(g.Time * 4f)) * (1f - g.Open);
		g.Crack.DefaultColor = new Color(Pal.Cyan, a);
	}

	private static void WaterRing(Control layer, Vector2 at, float delay)
	{
		Node2D root = Fx.Root(layer, at, "P3GateRing");
		Line2D ring = Fx.Line(root, Fx.Circle(1f, 64, 0.22f), new Color(Pal.Ice, 0f), 3f, closed: true);
		Fx.Animate(root, 0.9f + delay, t =>
		{
			float u = Mathf.Clamp((t * (0.9f + delay) - delay) / 0.9f, 0f, 1f);
			root.Scale = Vector2.One * (40f + 300f * Ease.OutCubic(u));
			ring.Width = (3f * (1f - u) + 1f) / root.Scale.X;
			ring.DefaultColor = new Color(Pal.Ice, u <= 0f ? 0f : 0.8f * (1f - u));
		});
		Fx.FreeAfter(root, 1.0 + delay);
	}

	/// <summary>平涂水花：门底一圈水冠 + 抛物线飞起的硬边水滴。</summary>
	private static void Splash(Control layer, Vector2 at, float s)
	{
		Node2D root = Fx.Root(layer, at, "P3GateSplash");
		Polygon2D crown = Fx.Poly(root, new Vector2[3], new Color(Pal.Ice, 0.95f));
		Polygon2D crownCore = Fx.Poly(root, new Vector2[3], Pal.White);
		float half = 160f * s;
		Fx.Animate(root, 0.32f, t =>
		{
			float hgt = 46f * s * Mathf.Sin(t * Mathf.Pi);
			var pts = new List<Vector2> { new(-half - 24f, 0f) };
			for (int k = 0; k <= 12; k++)
			{
				float x = -half - 24f + (2f * half + 48f) * k / 12f;
				pts.Add(new Vector2(x, -hgt * (k % 2 == 0 ? 1f : 0.45f) * (1f - Mathf.Abs(k - 6) / 9f)));
			}
			pts.Add(new Vector2(half + 24f, 0f));
			Fx.Reshape(crown, pts.ToArray());
			Fx.Reshape(crownCore, pts.ConvertAll(p => new Vector2(p.X, p.Y * 0.5f)).ToArray());
		});
		for (int i = 0; i < 24; i++)
		{
			Vector2 v = Vector2.Up.Rotated(Fx.Rand(-1.1f, 1.1f)) * Fx.Rand(240f, 520f) * s;
			Vector2 start = new(Fx.Rand(-half, half), 0f);
			float r = Fx.Rand(4f, 12f) * s;
			Polygon2D drop = Fx.Poly(root, Fx.Circle(r, 10, 1.25f), i % 3 == 0 ? Pal.White : (i % 3 == 1 ? Pal.Ice : Pal.Cyan));
			float delay = Fx.Rand(0f, 0.08f);
			Fx.Animate(drop, 0.55f, t =>
			{
				float time = t * 0.55f - delay;
				Vector2 p = start + v * time + new Vector2(0f, 1300f * time * time);
				drop.Position = p;
				drop.Visible = time > 0f && p.Y <= 2f;
				drop.Scale = Vector2.One * (1f - 0.5f * t);
			});
		}
		Fx.FreeAfter(root, 0.6);
	}

	/// <summary>锁定：门框形状的光影以门底为中心放大、淡出。</summary>
	private static void Echo(Control layer, Vector2 floor)
	{
		Node2D root = Fx.Root(layer, floor, "P3GateEcho");
		root.Scale = Vector2.One * DoorScale;
		Sprite2D e = Sprite(root, "door_frame", new Vector2(-256f, -768f), centered: false);
		e.Material = Fx.Additive;
		Fx.Animate(root, 0.38f, t =>
		{
			root.Scale = Vector2.One * DoorScale * (1f + 0.1f * Ease.OutExpo(t));
			e.Modulate = new Color(Pal.Ice, 0.55f * (1f - t));
		});
		Fx.FreeAfter(root, 0.4);
	}

	/// <summary>开门瞬间：门口射出几道硬边光束（扇形），很快收掉。</summary>
	private static void Rays(Control layer, Vector2 at)
	{
		Node2D root = Fx.Root(layer, at, "P3GateRays");
		(float ang, float len, float w)[] rays = { (-1.1f, 520f, 0.05f), (-0.55f, 640f, 0.035f), (-0.15f, 700f, 0.05f), (0.3f, 600f, 0.03f), (0.8f, 480f, 0.045f) };
		var polys = new List<Polygon2D>();
		foreach (var _ in rays)
		{
			polys.Add(Fx.Poly(root, new Vector2[3], new Color(Pal.Ice, 0.35f), additive: true));
		}
		Fx.Animate(root, 0.45f, t =>
		{
			for (int i = 0; i < rays.Length; i++)
			{
				var (ang, len, w) = rays[i];
				float L = len * Ease.OutExpo(t);
				Fx.Reshape(polys[i], new[] { Vector2.Zero, Vector2.Right.Rotated(ang - w) * L, Vector2.Right.Rotated(ang + w) * L });
				polys[i].Color = new Color(Pal.Ice, 0.35f * (1f - t));
			}
		});
		Fx.FreeAfter(root, 0.5);
	}

	// ══════════════════════ 伊戈尔的契约（既定事项）══════════════════════

	public static void Contract(NCreature caster)
	{
		Control? front = Makoto.FxLayer();
		Texture2D? tex = Tex("contract");
		if (front == null || tex == null)
		{
			return;
		}
		const float S = 0.5f;                                   // 贴图 311×420 → 显示约 155×210
		float w = tex.GetWidth(), h = tex.GetHeight();
		Vector2 spot = Spot(caster, 170f);
		Vector2 hand = Hand(caster);
		Vector2 chest = Makoto.Center(caster) + new Vector2(0f, -20f);
		Node2D root = Fx.Root(front, spot, "P3Contract");
		root.Scale = Vector2.One * S;
		root.Rotation = -0.06f;
		Node2D paperRoot = new() { Position = new Vector2(0f, -h / 2f) };       // 纸的顶边
		root.AddChild(paperRoot);
		Polygon2D clip = Clip(paperRoot, new Rect2(-w / 2f - 4f, 0f, w + 8f, 1f));
		Sprite(clip, "contract", new Vector2(-w / 2f, 0f), centered: false);
		Polygon2D glint = Fx.Poly(clip, new Vector2[4], new Color(Pal.White, 0f), additive: true);
		Line2D sign = Fx.Line(paperRoot, Array.Empty<Vector2>(), new Color(0.06f, 0.14f, 0.55f), 5f, additive: false);
		Line2D signGlow = Fx.Line(paperRoot, Array.Empty<Vector2>(), new Color(Pal.Ice, 0.35f), 12f);
		Node2D topRod = Rod(paperRoot, w);
		Node2D botRod = Rod(paperRoot, w);
		Node2D seal = new() { Position = new Vector2(w * 0.17f, h * 0.84f), Visible = false };
		paperRoot.AddChild(seal);
		Fx.Poly(seal, Fx.Circle(34f, 24), new Color(0.07f, 0.17f, 0.7f));
		Fx.Line(seal, Fx.Circle(34f, 24), Pal.Ice, 3f, closed: true, additive: false);
		Fx.Poly(seal, Fx.Star4(24f, 0.3f), Pal.White, additive: true);
		paperRoot.Visible = false;

		// 1) 卷着的卷轴（一根轴）从掌心旋转着飞到头顶左上方
		Node2D flyer = Fx.Root(front, hand, "P3ContractRod");
		flyer.Scale = Vector2.One * S;
		Rod(flyer, w);
		Vector2 top = spot + new Vector2(0f, -h / 2f * S);
		Vector2 ctrl = new(hand.X - 60f, Mathf.Min(hand.Y, top.Y) - 160f);
		Trail(front, hand, ctrl, top, 0.27f, Pal.Ice);
		Fx.Animate(flyer, 0.27f, t =>
		{
			float e = Ease.OutCubic(t);
			flyer.GlobalPosition = Bezier(hand, ctrl, top, e);
			flyer.Rotation = (1f - e) * 6.3f - 0.06f;
		});
		Fx.After(flyer, 0.27, "contract arrive", () =>
		{
			flyer.QueueFree();
			paperRoot.Visible = true;
			Audio.Sfx.Play(Audio.Snd.TarotReveal, 0.7f);
		});
		float sealAt = 0.85f;
		float total = 1.75f;
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			root.Position = spot + new Vector2(0f, 4f * Mathf.Sin(time * 3.2f));
			// 2) 0.27–0.53 下轴往下滚，纸从上往下展开；1.07–1.29 卷回
			float open = time < 0.27f ? 0f : time < 0.53f ? Ease.OutCubic((time - 0.27f) / 0.26f) : time < 1.07f ? 1f : 1f - Ease.InOutCubic(Mathf.Clamp((time - 1.07f) / 0.22f, 0f, 1f));
			float bottom = h * open;
			Fx.Reshape(clip, RectPts(new Rect2(-w / 2f - 4f, 0f, w + 8f, Mathf.Max(1f, bottom))));
			botRod.Position = new Vector2(0f, bottom);
			paperRoot.Scale = new Vector2(1f, 1f + 0.03f * Mathf.Sin(time * 18f) * (1f - Mathf.Clamp((time - 0.53f) / 0.2f, 0f, 1f)));
			// 纸面一道斜向高光扫过
			float g = Mathf.Clamp((time - 0.55f) / 0.4f, 0f, 1f);
			float gx = -w * 0.8f + g * w * 1.6f;
			Fx.Reshape(glint, new[] { new Vector2(gx - 40f, 0f), new Vector2(gx + 40f, 0f), new Vector2(gx - 60f, h), new Vector2(gx - 140f, h) });
			glint.Color = new Color(Pal.White, g > 0f && g < 1f ? 0.35f * Mathf.Sin(g * Mathf.Pi) : 0f);
			// 3) 0.57–0.81 签名：发光的墨迹沿签名栏写出
			float u = Mathf.Clamp((time - 0.57f) / 0.24f, 0f, 1f);
			if (time < 1.07f)
			{
				int n = Mathf.Max(2, (int)(41 * u));
				var pts = new Vector2[n];
				for (int i = 0; i < n; i++)
				{
					pts[i] = new Vector2(-w * 0.4f + w * 0.8f * i / 40f, h * 0.74f + 14f * Mathf.Sin(i * 0.55f) - 16f * Mathf.Sin(i / 40f * Mathf.Pi));
				}
				sign.Points = u > 0f ? pts : Array.Empty<Vector2>();
				signGlow.Points = sign.Points;
			}
			else
			{
				sign.Points = signGlow.Points = Array.Empty<Vector2>();
			}
			// 4) 封蜡：从上方砸下（1.8 倍 → 1 倍）
			float v = (time - sealAt) / 0.1f;
			seal.Visible = v >= 0f && time < 1.07f;
			seal.Scale = Vector2.One * (1f + 1.8f * (1f - Ease.OutCubic(Mathf.Clamp(v, 0f, 1f))));
			seal.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(v * 4f, 0f, 1f));
			root.Modulate = new Color(1f, 1f, 1f, time < 1.29f ? 1f : 0f);
		});
		Fx.After(root, sealAt + 0.1, "contract seal", () =>
		{
			Vector2 at = seal.GlobalPosition;
			P3Fx.Shockwave(front, at, Pal.Ice, 200f, 0.32f);
			P3Fx.Flash(front, at, Pal.Ice, 110f, 0.18f);
			P3Fx.GlassShatter(front, at, 10, 0.35f, Pal.Ice);
			Audio.Sfx.Play(Audio.Snd.StarSpend, 0.7f);
			MegaCrit.Sts2.Core.Nodes.NGame.Instance?.ScreenShake(MegaCrit.Sts2.Core.Nodes.Vfx.Utilities.ShakeStrength.Weak, MegaCrit.Sts2.Core.Nodes.Vfx.Utilities.ShakeDuration.Short);
		});
		// 5) 卷起后一闪，化成两只蓝蝶飞进胸口
		Fx.After(root, 1.29, "contract butterflies", () =>
		{
			Vector2 at = topRod.GlobalPosition;
			P3Fx.Flash(front, at, Pal.White, 130f, 0.15f);
			P3Fx.Butterfly(front, at + new Vector2(-30f, 0f), chest, 0.42f, 0.8f);
			P3Fx.Butterfly(front, at + new Vector2(30f, 0f), chest, 0.48f, 0.8f);
		});
		Fx.After(root, 1.78, "contract absorb", () =>
		{
			P3Fx.Flash(front, chest, Pal.Ice, 170f, 0.22f);
			P3Fx.Shockwave(front, chest, Pal.Cyan, 220f, 0.3f);
			Audio.Sfx.Play(Audio.Snd.StarGain, 0.7f);
		});
		Fx.FreeAfter(root, total + 0.1);
	}

	/// <summary>卷轴的金属轴：一根横杆 + 两端的四芒星宝石。</summary>
	private static Node2D Rod(Node parent, float w)
	{
		Node2D rod = new();
		parent.AddChild(rod);
		Fx.Poly(rod, RectPts(new Rect2(-w / 2f - 18f, -9f, w + 36f, 18f)), new Color(0.72f, 0.76f, 0.9f));
		Fx.Line(rod, new[] { new Vector2(-w / 2f - 18f, -8f), new Vector2(w / 2f + 18f, -8f) }, new Color(1f, 1f, 1f, 0.9f), 3f, additive: false);
		foreach (float sgn in new[] { -1f, 1f })
		{
			Fx.Poly(rod, Fx.Star4(16f, 0.3f), Pal.Ice, additive: true).Position = new Vector2(sgn * (w / 2f + 26f), 0f);
		}
		return rod;
	}

	// ══════════════════════ 全书（预言）══════════════════════

	public static void Compendium(NCreature caster)
	{
		Control? front = Makoto.FxLayer();
		Texture2D? left = Tex("book_left"), right = Tex("book_right");
		if (front == null || left == null || right == null)
		{
			return;
		}
		const float S = 0.5f;                                   // 贴图 380 宽 → 显示约 190
		float half = right.GetWidth(), bh = right.GetHeight();
		Vector2 spot = Spot(caster, 190f) + new Vector2(10f, 20f);
		Vector2 hand = Hand(caster);
		Node2D root = Fx.Root(front, hand, "P3Compendium");
		root.Scale = Vector2.One * S * 0.55f;
		Sprite2D glowSpr = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Position = new Vector2(0f, -20f), Modulate = new Color(Pal.Ice, 0f), Scale = Vector2.One * 6f };
		root.AddChild(glowSpr);
		Sprite2D r = Sprite(root, "book_right", new Vector2(0f, -bh / 2f), centered: false);
		Sprite2D l = Sprite(root, "book_left", new Vector2(0f, -bh / 2f), centered: false);
		l.Offset = new Vector2(-half, 0f);
		Polygon2D cover = Fx.Poly(root, new Vector2[4], new Color(0.08f, 0.16f, 0.5f));
		// 翻页（9 张）
		var pages = new Polygon2D[9];
		for (int i = 0; i < pages.Length; i++)
		{
			pages[i] = Fx.Poly(root, new Vector2[18], Pal.White);
			pages[i].Visible = false;
		}
		// 光：三条硬边光带 + 旋转法阵
		var beams = new Polygon2D[3];
		(float dx, float bw, float a)[] beamDef = { (-52f, 28f, 0.3f), (0f, 60f, 0.55f), (44f, 20f, 0.3f) };
		for (int i = 0; i < 3; i++)
		{
			beams[i] = Fx.Poly(root, new Vector2[4], new Color(Pal.Ice, 0f), additive: true);
		}
		Node2D circle = new() { Position = new Vector2(0f, -bh * 0.45f), Scale = new Vector2(1f, 0.32f) };
		root.AddChild(circle);
		Node2D spinner = new();
		circle.AddChild(spinner);
		Fx.Line(spinner, Fx.Circle(140f, 64), new Color(Pal.Ice, 0.8f), 3f, closed: true);
		Fx.Line(spinner, Fx.Circle(112f, 64), new Color(Pal.Ice, 0.4f), 1.5f, closed: true);
		for (int i = 0; i < 8; i++)
		{
			Fx.Poly(spinner, Fx.Star4(12f, 0.3f), Pal.White, additive: true).Position = Vector2.Right.Rotated(Mathf.Tau * i / 8f) * 140f;
		}
		circle.Modulate = new Color(1f, 1f, 1f, 0f);
		Node2D motesAt = new() { Position = new Vector2(0f, -bh * 0.3f) };
		root.AddChild(motesAt);
		Vector2 ctrl = new(hand.X - 40f, Mathf.Min(hand.Y, spot.Y) - 200f);
		Trail(front, hand, ctrl, spot, 0.24f, Pal.Ice);
		Audio.Sfx.Play(Audio.Snd.TarotReveal, 0.7f);
		const float total = 1.6f;
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			// 飞来：合着的书旋转着从掌心飞到头顶左上方
			if (time < 0.24f)
			{
				float e = Ease.OutCubic(time / 0.24f);
				root.GlobalPosition = Bezier(hand, ctrl, spot, e);
				root.Scale = Vector2.One * S * (0.55f + 0.45f * e);
				root.Rotation = (1f - e) * 2.4f;
			}
			else
			{
				root.GlobalPosition = spot + new Vector2(0f, 4f * Mathf.Sin(time * 3f));
				root.Scale = Vector2.One * S;
				root.Rotation = -0.04f;
			}
			// 打开：左半封面绕书脊从右翻到左
			float op = time < 0.24f ? 0f : time < 1.3f ? Ease.OutBack(Mathf.Clamp((time - 0.24f) / 0.22f, 0f, 1f)) : 1f - Ease.InCubic(Mathf.Clamp((time - 1.3f) / 0.18f, 0f, 1f));
			float ang = Mathf.Clamp(op, 0f, 1.08f) * Mathf.Pi;
			float ca = Mathf.Cos(ang);
			l.Visible = ca < 0f;
			l.Scale = new Vector2(Mathf.Max(0.01f, -ca), 1f);
			cover.Visible = ca >= 0f;
			Fx.Reshape(cover, new[] { new Vector2(0f, -bh / 2f), new Vector2(half * ca, -bh / 2f - 16f * Mathf.Sin(ang)), new Vector2(half * ca, bh / 2f - 16f * Mathf.Sin(ang)), new Vector2(0f, bh / 2f) });
			glowSpr.Modulate = new Color(Pal.Ice, 0.4f * Mathf.Clamp(op, 0f, 1f));
			// 翻页：一张接一张从右翻到左，越翻越快
			for (int i = 0; i < pages.Length; i++)
			{
				float t0 = 0.4f + 0.62f * (1f - Mathf.Pow(1f - i / (float)pages.Length, 1.6f));
				float u = (time - t0) / 0.16f;
				pages[i].Visible = u > 0f && u < 1f && time < 1.3f;
				if (pages[i].Visible)
				{
					Fx.Reshape(pages[i], Page(u * Mathf.Pi, bh * 0.78f, half * 0.86f, 44f));
					float sh = 0.82f + 0.18f * Mathf.Abs(Mathf.Cos(u * Mathf.Pi));
					pages[i].Color = new Color(sh, sh, Mathf.Min(1f, sh + 0.04f));
				}
			}
			// 书上的光柱和法阵
			float la = Mathf.Clamp((time - 0.36f) / 0.12f, 0f, 1f) * (1f - Mathf.Clamp((time - 1.15f) / 0.15f, 0f, 1f));
			float flick = 0.8f + 0.2f * Mathf.Sin(time * 30f);
			for (int i = 0; i < 3; i++)
			{
				var (dx, bw, a) = beamDef[i];
				float y0 = -bh * 0.35f;
				Fx.Reshape(beams[i], new[] { new Vector2(dx - bw / 2f, y0), new Vector2(dx + bw / 2f, y0), new Vector2(dx + bw * 0.15f, y0 - 300f), new Vector2(dx - bw * 0.15f, y0 - 300f) });
				beams[i].Color = new Color(Pal.Ice, a * la * flick);
			}
			spinner.Rotation = time * 2f;
			circle.Modulate = new Color(1f, 1f, 1f, la);
		});
		Fx.After(root, 0.4, "book motes", () =>
		{
			GpuParticles2D motes = Fx.Sparks(motesAt, Pal.Ice, 26, 20f, 60f, 1.1f, gravity: -160f, scaleMin: 0.1f, scaleMax: 0.3f, oneShot: false);
			Fx.After(root, 0.8, "book motes off", () => motes.Emitting = false);
		});
		Fx.After(root, 1.3, "book close", () =>
		{
			Vector2 at = root.GlobalPosition;
			P3Fx.GlassShatter(front, at + new Vector2(0f, -20f), 12, 0.35f, Pal.White);
			P3Fx.Flash(front, at, Pal.Ice, 140f, 0.18f);
		});
		Fx.After(root, 1.48, "book gone", () =>
		{
			Fx.Animate(root, 0.12f, t => root.Modulate = new Color(1f, 1f, 1f, 1f - t));
		});
		Fx.FreeAfter(root, total + 0.1);
	}

	/// <summary>从书脊翻起的书页：ang 0（盖在右边）→ π（翻到左边），中间拱起。</summary>
	private static Vector2[] Page(float ang, float hh, float ww, float lift)
	{
		float c = Mathf.Cos(ang), s = Mathf.Sin(ang);
		var pts = new Vector2[18];
		for (int i = 0; i < 9; i++)
		{
			float t = i / 8f, bend = Mathf.Sin(t * Mathf.Pi) * lift * s;
			pts[i] = new Vector2(c * ww * t, -hh / 2f - bend - 12f * s * t);
		}
		for (int i = 0; i < 9; i++)
		{
			float t = (8 - i) / 8f, bend = Mathf.Sin(t * Mathf.Pi) * lift * s;
			pts[9 + i] = new Vector2(c * ww * t, hh / 2f - bend * 0.6f - 12f * s * t);
		}
		return pts;
	}

	// ══════════════════════ Shuffle Time（抉择，抉择）══════════════════════

	public static void ShuffleTime(NCreature caster)
	{
		Control? front = Makoto.FxLayer();
		if (front == null || Tex("tarot_back") == null)
		{
			return;
		}
		const float S = 0.57f;
		Vector2 c = Makoto.Head(caster) + new Vector2(260f, 40f);
		Node2D root = Fx.Root(front, c, "P3ShuffleTime");
		string[] minors = { "minor_sword", "minor_cup", "minor_wand", "minor_coin" };
		var cards = new Sprite2D[4];
		var faces = new Texture2D?[4];
		for (int i = 0; i < 4; i++)
		{
			cards[i] = Sprite(root, "tarot_back", Vector2.Zero);
			faces[i] = Tex(minors[i]);
		}
		Label title = Typo.Text(root, "SHUFFLE TIME", Typo.LatinItalic, 44, Pal.White, new Vector2(-170f, -150f), outline: 6, outlineColor: new Color(0.02f, 0.06f, 0.24f));
		title.Modulate = new Color(1f, 1f, 1f, 0f);
		const int pick = 0;
		Audio.Sfx.Play(Audio.Snd.TarotReveal, 0.8f);
		const float total = 1.65f;
		bool flipped = false;
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			float spread = Ease.OutBack(Mathf.Clamp((time - 0.05f) / 0.22f, 0f, 1f));
			float spin = time < 0.25f ? 0f : Mathf.Min(time, 0.8f) - 0.25f;
			spin = spin * spin * 9f;
			title.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp((time - 0.8f) / 0.06f, 0f, 1f) * (1f - Mathf.Clamp((time - 1.1f) / 0.1f, 0f, 1f)));
			for (int i = 0; i < 4; i++)
			{
				Sprite2D card = cards[i];
				if (!GodotObject.IsInstanceValid(card))
				{
					continue;
				}
				float a = spin + i * Mathf.Pi / 2f - Mathf.Pi / 2f;
				float R = 120f * spread;
				Vector2 p = new(R * Mathf.Cos(a) * 1.25f, R * Mathf.Sin(a) * 0.55f);
				float depth = 0.8f + 0.2f * Mathf.Sin(a);
				// 0.8 起逐张翻面（宽度过零时换成正面贴图）
				float flip = Mathf.Clamp((time - (0.8f + i * 0.06f)) / 0.1f, 0f, 1f);
				if (flip >= 0.5f && card.Texture != faces[i] && faces[i] != null)
				{
					card.Texture = faces[i];
				}
				float fx = time >= 0.8f ? Mathf.Abs(Mathf.Cos(flip * Mathf.Pi)) : 1f;
				if (i == pick && time >= 1.1f)
				{
					float u = Ease.OutBack(Mathf.Clamp((time - 1.1f) / 0.2f, 0f, 1f));
					p = p.Lerp(Vector2.Zero, u);
					depth = 1f + 0.45f * u;
					if (time >= 1.4f)
					{
						depth *= 1f - Ease.InCubic(Mathf.Clamp((time - 1.4f) / 0.2f, 0f, 1f));
					}
				}
				card.Position = p;
				card.Scale = new Vector2(Mathf.Max(0.02f, fx) * depth, depth) * S;
				card.ZIndex = (int)(depth * 10f);
			}
			if (time >= 0.8f && !flipped)
			{
				flipped = true;
				Audio.Sfx.Play(Audio.Snd.Weak, 0.5f);
			}
		});
		// 1.1：选中剑牌，其余三张碎掉
		Fx.After(root, 1.1, "shuffle pick", () =>
		{
			for (int i = 0; i < 4; i++)
			{
				if (i != pick && GodotObject.IsInstanceValid(cards[i]))
				{
					P3Fx.GlassShatter(front, cards[i].GlobalPosition, 14, 0.5f, Pal.Ice);
					cards[i].QueueFree();
				}
			}
			P3Fx.Flash(front, c, Pal.White, 240f, 0.25f);
			P3Fx.Shockwave(front, c, Pal.Ice, 460f, 0.4f);
			Audio.Sfx.Play(Audio.Snd.TarotShatter, 0.8f);
		});
		Fx.FreeAfter(root, total + 0.1);
	}
}
