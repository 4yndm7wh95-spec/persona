using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using P3Makoto.Core;

namespace P3Makoto.Vfx;

/// <summary>
/// v0.12：把原来共用模板（塔罗 / Shuffle Time / 锻造火花 / 切入大字 / 法阵）的蓝色、金色品质卡，各换成一个自己的演出。
/// 每个函数对应一张牌（个别两张共用同一主题时参数和构图不同）。都只用程序化图形 + 已有着色器，不依赖新贴图。
/// 约定：会盖住人物的大面积光放在身后层（back），轻量的线条、火花、文字放在前景（front）。
/// </summary>
public static class Uniq
{
	private static void Shake(ShakeStrength s) => MegaCrit.Sts2.Core.Nodes.NGame.Instance?.ScreenShake(s, ShakeDuration.Short);

	private static Vector2[] RectPts(Rect2 r) => new[] { r.Position, new Vector2(r.End.X, r.Position.Y), r.End, new Vector2(r.Position.X, r.End.Y) };

	private static void Bang(Control layer, Vector2 at, Color tint, float s = 1f)
	{
		P3Fx.Flash(layer, at, tint, 260f * s, 0.22f);
		P3Fx.Shockwave(layer, at, tint, 420f * s, 0.4f);
	}

	private static Vector2 Bezier(Vector2 a, Vector2 c, Vector2 b, float t)
	{
		float k = 1f - t;
		return a * (k * k) + c * (2f * k * t) + b * (t * t);
	}

	/// <summary>剑形：剑尖朝 +X。</summary>
	private static Vector2[] Blade(float len, float w) => new[]
	{
		new Vector2(-len * 0.18f, -w * 0.3f), new Vector2(len * 0.82f, -w * 0.5f), new Vector2(len, 0f), new Vector2(len * 0.82f, w * 0.5f), new Vector2(-len * 0.18f, w * 0.3f)
	};

	/// <summary>一把光剑节点（外层青色辉光 + 钢身 + 白刃 + 护手）。</summary>
	private static Node2D Sword(Node parent, float len, Color tint)
	{
		Node2D s = new();
		parent.AddChild(s);
		Fx.Poly(s, Blade(len * 1.08f, len * 0.16f), new Color(tint, 0.45f), additive: true);
		Fx.Poly(s, Blade(len, len * 0.1f), new Color(0.75f, 0.85f, 1f));
		Fx.Poly(s, Blade(len * 0.98f, len * 0.03f), Pal.White, additive: true);
		Fx.Poly(s, RectPts(new Rect2(-len * 0.2f, -len * 0.13f, len * 0.05f, len * 0.26f)), new Color(0.72f, 0.76f, 0.9f));
		return s;
	}

	/// <summary>锤头剪影（锤柄朝 -Y）。</summary>
	private static Node2D Hammer(Node parent, float s, Color tint)
	{
		Node2D h = new();
		parent.AddChild(h);
		Fx.Poly(h, RectPts(new Rect2(-7f * s, -150f * s, 14f * s, 150f * s)), new Color(0.55f, 0.6f, 0.75f));
		Fx.Poly(h, RectPts(new Rect2(-62f * s, -200f * s, 124f * s, 58f * s)), new Color(tint, 0.35f), additive: true);
		Fx.Poly(h, RectPts(new Rect2(-55f * s, -195f * s, 110f * s, 48f * s)), new Color(0.82f, 0.88f, 1f));
		Fx.Line(h, RectPts(new Rect2(-55f * s, -195f * s, 110f * s, 48f * s)), Pal.White, 3f, closed: true);
		return h;
	}

	// ══════════════════ 锻造系 ══════════════════

	/// <summary>铸剑者：身旁凭空出现一块烧红的剑胚，光之锤连砸三下（每下火花喷泉 + 冲击），剑胚由红变白再淬成青。</summary>
	public static void TheSmith(Control back, Control front, Vector2 center)
	{
		Vector2 at = center + new Vector2(150f, 30f);
		Node2D billet = Fx.Root(front, at, "P3SmithBillet");
		Node2D sw = Sword(billet, 170f, new Color(1f, 0.5f, 0.1f));
		billet.Rotation = 0.05f;
		Node2D hamRoot = Fx.Root(front, at + new Vector2(10f, -10f), "P3SmithHammer");
		Hammer(hamRoot, 0.9f, Pal.Ice);
		for (int i = 0; i < 3; i++)
		{
			float t0 = 0.12f + 0.3f * i;
			int k = i;
			Fx.After(hamRoot, t0 + 0.12f, "smith blow", () =>
			{
				Vector2 hit = at + new Vector2(60f, 0f);
				Node2D sp = Fx.Root(front, hit, "P3SmithSparks");
				Fx.Sparks(sp, new Color(1f, 0.6f, 0.15f), 40, 200f, 520f, 0.5f, gravity: 900f, scaleMin: 0.1f, scaleMax: 0.3f,
					ramp: new[] { new Color(1f, 0.95f, 0.7f), new Color(1f, 0.55f, 0.1f), new Color(0.6f, 0.1f, 0.05f) });
				Fx.FreeAfter(sp, 0.6);
				Bang(front, hit, k == 2 ? Pal.Ice : new Color(1f, 0.6f, 0.2f), 0.6f + 0.2f * k);
				Shake(k == 2 ? ShakeStrength.Medium : ShakeStrength.Weak);
				Audio.Sfx.Play(Audio.Snd.StarSpend, 0.6f, pitch: 0.6f + 0.1f * k);
			});
		}
		Fx.Animate(hamRoot, 1.15f, t =>
		{
			float time = t * 1.15f;
			float ph = Mathf.PosMod(time - 0.12f, 0.3f) / 0.3f;
			float swing = time < 0.12f || time > 1.02f ? -1.2f : (ph < 0.4f ? Mathf.Lerp(0.2f, -1.2f, Ease.OutCubic(ph / 0.4f)) : Mathf.Lerp(-1.2f, 0.2f, Ease.InCubic((ph - 0.4f) / 0.6f)));
			hamRoot.Rotation = swing;
			hamRoot.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(time / 0.08f, 0f, 1f) * (1f - Mathf.Clamp((time - 1.0f) / 0.15f, 0f, 1f)));
		});
		Fx.Animate(billet, 1.5f, t =>
		{
			float time = t * 1.5f;
			float heat = Mathf.Clamp(time / 0.9f, 0f, 1f), q = Mathf.Clamp((time - 1.0f) / 0.2f, 0f, 1f);
			Color c = new Color(1f, 0.35f, 0.1f).Lerp(new Color(1f, 0.95f, 0.8f), heat).Lerp(new Color(0.6f, 0.85f, 1f), q);
			sw.Modulate = c;
			billet.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(time / 0.1f, 0f, 1f) * (1f - Mathf.Clamp((time - 1.3f) / 0.2f, 0f, 1f)));
		});
		Fx.After(billet, 1.0, "smith quench", () =>
		{
			Node2D steam = Fx.Root(front, at + new Vector2(70f, 0f), "P3SmithSteam");
			Fx.Sparks(steam, Pal.White, 26, 60f, 200f, 0.7f, gravity: -220f, scaleMin: 0.3f, scaleMax: 0.8f, ramp: new[] { new Color(1f, 1f, 1f, 0.8f), new Color(0.8f, 0.9f, 1f, 0f) });
			Fx.FreeAfter(steam, 0.9);
			Audio.Sfx.Play(Audio.Snd.Weak, 0.5f, pitch: 0.9f);
		});
		Fx.FreeAfter(billet, 1.55);
		Fx.FreeAfter(hamRoot, 1.2);
	}

	/// <summary>锻打成型：巨大的光之锤从敌人头顶砸下，地面开裂，火花四溅。</summary>
	public static void BeatIntoShape(Control front, Vector2 target, Vector2 feet)
	{
		Node2D root = Fx.Root(front, target + new Vector2(-40f, -40f), "P3BeatHammer");
		Node2D h = Hammer(root, 1.6f, Pal.Ice);
		h.Rotation = Mathf.Pi;                                   // 锤头朝下
		Fx.Animate(root, 0.22f, t =>
		{
			root.Rotation = Mathf.Lerp(-2.2f, 0f, Ease.InCubic(t));
			root.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(t * 4f, 0f, 1f));
		});
		Fx.After(root, 0.22, "beat hit", () =>
		{
			Motifs.GroundCrack(front, feet, 1.3f);
			Node2D sp = Fx.Root(front, target, "P3BeatSparks");
			Fx.Sparks(sp, new Color(1f, 0.6f, 0.15f), 50, 260f, 640f, 0.55f, gravity: 1000f, scaleMin: 0.1f, scaleMax: 0.3f,
				ramp: new[] { Pal.White, new Color(1f, 0.6f, 0.15f), new Color(0.5f, 0.1f, 0.05f) });
			Fx.FreeAfter(sp, 0.7);
			Bang(front, target, Pal.Ice, 1.1f);
			Fx.Animate(root, 0.25f, t => root.Modulate = new Color(1f, 1f, 1f, 1f - t));
		});
		Fx.FreeAfter(root, 0.5);
	}

	/// <summary>武器库：人物身后浮现一排扇形排开的光剑（武器架），依次亮起，最后一齐插进他脚下的地面。</summary>
	public static void Arsenal(Control back, Control front, Vector2 feet, Vector2 center)
	{
		const int n = 7;
		var swords = new List<Node2D>();
		Node2D root = Fx.Root(back, center + new Vector2(-10f, -40f), "P3Arsenal");
		for (int i = 0; i < n; i++)
		{
			float a = Mathf.Lerp(-2.4f, -0.74f, i / (float)(n - 1));          // 扇形：从左上到右上
			Node2D s = Sword(root, 150f, Pal.Cyan);
			s.Rotation = a;
			s.Position = Vector2.Right.Rotated(a) * 60f;
			s.Modulate = new Color(1f, 1f, 1f, 0f);
			swords.Add(s);
		}
		Fx.Animate(root, 1.3f, t =>
		{
			float time = t * 1.3f;
			for (int i = 0; i < n; i++)
			{
				float on = Mathf.Clamp((time - 0.05f - i * 0.06f) / 0.1f, 0f, 1f);
				float a = Mathf.Lerp(-2.4f, -0.74f, i / (float)(n - 1));
				float dive = Ease.InCubic(Mathf.Clamp((time - 0.8f) / 0.2f, 0f, 1f));
				Vector2 home = Vector2.Right.Rotated(a) * (60f + 30f * on);
				Vector2 ground = feet - root.GlobalPosition + new Vector2((i - n / 2) * 46f, 0f);
				swords[i].Position = home.Lerp(ground, dive);
				swords[i].Rotation = Mathf.LerpAngle(a, Mathf.Pi / 2f, dive);
				swords[i].Modulate = new Color(1f, 1f, 1f, on * (1f - Mathf.Clamp((time - 1.1f) / 0.2f, 0f, 1f)));
			}
		});
		Fx.After(root, 1.0, "arsenal land", () =>
		{
			P3Fx.Shockwave(front, feet, Pal.Ice, 700f, 0.45f, squashY: 0.3f);
			P3Fx.GlassShatter(front, feet + new Vector2(0f, -20f), 18, 0.6f, Pal.Ice);
			Shake(ShakeStrength.Medium);
			Audio.Sfx.Play(Audio.Snd.StarSpend, 0.8f, pitch: 0.7f);
		});
		Fx.FreeAfter(root, 1.35);
	}

	/// <summary>剑圣：三把剑的残影绕着人物旋转一圈（越转越快），最后合成一把，向前一指，剑尖一闪。</summary>
	public static void SwordSage(Control back, Control front, Vector2 center)
	{
		Node2D root = Fx.Root(front, center, "P3SwordSage");
		var blades = new List<Node2D>();
		for (int i = 0; i < 3; i++)
		{
			Node2D s = Sword(root, 140f, Pal.Cyan);
			blades.Add(s);
		}
		Fx.Animate(root, 1.2f, t =>
		{
			float time = t * 1.2f;
			float spin = time * time * 9f;
			float merge = Ease.InOutCubic(Mathf.Clamp((time - 0.75f) / 0.25f, 0f, 1f));
			for (int i = 0; i < 3; i++)
			{
				float a = spin + i * Mathf.Tau / 3f;
				Vector2 orbit = new(Mathf.Cos(a) * 150f, Mathf.Sin(a) * 50f);
				blades[i].Position = orbit.Lerp(new Vector2(60f, -30f), merge);
				blades[i].Rotation = Mathf.LerpAngle(a + Mathf.Pi / 2f, -0.15f, merge);
				blades[i].Modulate = new Color(1f, 1f, 1f, (i == 0 ? 1f : 0.5f * (1f - merge)) * Mathf.Clamp(time / 0.1f, 0f, 1f) * (1f - Mathf.Clamp((time - 1.05f) / 0.15f, 0f, 1f)));
			}
		});
		Fx.After(root, 1.0, "sage point", () =>
		{
			Vector2 tip = center + new Vector2(200f, -50f);
			P3Fx.Flash(front, tip, Pal.White, 180f, 0.2f);
			P3Fx.SpeedLines(front, tip, Pal.Ice, 12, 30f, 260f, 0.3f);
			Audio.Sfx.Play(Audio.Snd.Weak, 0.6f, pitch: 1.2f);
		});
		Fx.FreeAfter(root, 1.25);
	}

	/// <summary>铸墙：身前的火花聚拢成一块块六边形钢板，咔哒咔哒拼成一面墙，最后整面一亮。</summary>
	public static void Bulwark(Control front, Vector2 center)
	{
		Vector2 wall = center + new Vector2(130f, 0f);
		var cells = new List<Vector2>();
		for (int row = -2; row <= 2; row++)
		{
			for (int col = 0; col < 2; col++)
			{
				cells.Add(new Vector2(col * 52f + (row % 2 == 0 ? 0f : 26f), row * 45f));
			}
		}
		Node2D root = Fx.Root(front, wall, "P3Bulwark");
		for (int i = 0; i < cells.Count; i++)
		{
			Node2D tile = new() { Position = cells[i] + new Vector2(Fx.Rand(-200f, -80f), Fx.Rand(-160f, 160f)) };
			root.AddChild(tile);
			Fx.Poly(tile, Fx.Hexagon(25f), new Color(0.35f, 0.5f, 0.85f, 0.85f));
			Fx.Line(tile, Fx.Hexagon(25f), Pal.Ice, 2.5f, closed: true);
			Vector2 from = tile.Position, to = cells[i];
			float d = i * 0.05f;
			tile.Modulate = new Color(1f, 1f, 1f, 0f);
			Fx.Animate(tile, 0.35f + d, t =>
			{
				float u = Mathf.Clamp((t * (0.35f + d) - d) / 0.35f, 0f, 1f);
				tile.Position = from.Lerp(to, Ease.OutBack(u));
				tile.Rotation = (1f - u) * 3f;
				tile.Modulate = new Color(1f, 1f, 1f, u > 0f ? 1f : 0f);
			});
		}
		Fx.After(root, 0.35 + cells.Count * 0.05, "bulwark lock", () =>
		{
			Bang(front, wall + new Vector2(26f, 0f), Pal.Ice, 0.8f);
			Audio.Sfx.Play(Audio.Snd.StarSpend, 0.7f, pitch: 0.75f);
			Fx.Animate(root, 0.45f, t => root.Modulate = new Color(1f + 0.6f * (1f - t), 1f + 0.6f * (1f - t), 1f + 0.6f * (1f - t), 1f - Ease.InCubic(t)));
		});
		Fx.FreeAfter(root, 0.35 + cells.Count * 0.05 + 0.5);
	}

	/// <summary>征召上前：脚下亮起一圈锻造法阵，君王之剑从法阵中心缓缓升起、翻转，化成一道光飞向手牌区。</summary>
	public static void SummonForth(Control back, Control front, Vector2 feet)
	{
		Node2D ring = Fx.Root(back, feet, "P3SummonRing");
		ring.Scale = new Vector2(1f, 0.3f);
		Fx.Line(ring, Fx.Circle(120f, 48), new Color(Pal.Cyan, 0.9f), 4f, closed: true);
		Fx.Line(ring, Fx.Circle(92f, 6), new Color(Pal.Ice, 0.8f), 2f, closed: true);
		Fx.Animate(ring, 1.2f, t => { ring.Rotation = t * 3f; ring.Modulate = new Color(1f, 1f, 1f, Ease.Envelope(t, 0.1f, 0.7f)); });
		Fx.FreeAfter(ring, 1.25);
		Node2D root = Fx.Root(front, feet, "P3SummonBlade");
		Node2D sw = Sword(root, 200f, Pal.Royal);
		sw.Rotation = -Mathf.Pi / 2f;
		Vector2 size = front.GetViewportRect().Size;
		Vector2 hand = new(size.X * 0.5f, size.Y - 100f);
		Fx.Animate(root, 1.1f, t =>
		{
			float time = t * 1.1f;
			float rise = Ease.OutCubic(Mathf.Clamp(time / 0.45f, 0f, 1f));
			float fly = Ease.InCubic(Mathf.Clamp((time - 0.7f) / 0.3f, 0f, 1f));
			Vector2 up = feet + new Vector2(0f, -40f - 220f * rise);
			root.GlobalPosition = up.Lerp(hand, fly);
			sw.Rotation = -Mathf.Pi / 2f + Mathf.Clamp((time - 0.45f) / 0.25f, 0f, 1f) * Mathf.Tau;
			root.Scale = Vector2.One * (1f - 0.6f * fly);
			root.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(time / 0.1f, 0f, 1f));
		});
		Fx.After(root, 0.45, "summon top", () => { P3Fx.Flash(front, feet + new Vector2(0f, -260f), Pal.Ice, 220f, 0.2f); Audio.Sfx.Play(Audio.Snd.TarotReveal, 0.6f); });
		Fx.FreeAfter(root, 1.12);
	}

	/// <summary>锤子时间：天空盖下一枚巨大的锤形纹章，重重一砸，冲击环一圈圈扩散到全场。</summary>
	public static void HammerTime(Control back, Control front, Vector2 center)
	{
		Vector2 size = front.GetViewportRect().Size;
		Vector2 at = new(center.X + 220f, size.Y * 0.32f);
		Node2D root = Fx.Root(front, at, "P3HammerCrest");
		Fx.Poly(root, Fx.Circle(110f, 48), new Color(0.05f, 0.12f, 0.4f, 0.85f));
		Fx.Line(root, Fx.Circle(110f, 48), Pal.Ice, 4f, closed: true);
		Fx.Line(root, Fx.Circle(94f, 48), new Color(Pal.Ice, 0.5f), 1.5f, closed: true);
		Node2D h = Hammer(root, 0.6f, Pal.Cyan);
		h.Position = new Vector2(0f, 75f);
		h.Rotation = 0.6f;
		Node2D h2 = Hammer(root, 0.6f, Pal.Cyan);
		h2.Position = new Vector2(0f, 75f);
		h2.Rotation = -0.6f;
		Fx.Animate(root, 1.1f, t =>
		{
			float time = t * 1.1f;
			float drop = Ease.InCubic(Mathf.Clamp(time / 0.25f, 0f, 1f));
			root.Scale = Vector2.One * Mathf.Lerp(2.4f, 1f, drop);
			root.Modulate = new Color(1f, 1f, 1f, drop * (1f - Mathf.Clamp((time - 0.85f) / 0.25f, 0f, 1f)));
		});
		Fx.After(root, 0.25, "crest slam", () =>
		{
			Shake(ShakeStrength.Strong);
			for (int i = 0; i < 3; i++)
			{
				P3Fx.Shockwave(front, at, Pal.Ice, 900f + 400f * i, 0.6f, delay: 0.1f * i);
			}
			P3Fx.Flash(front, at, Pal.White, 300f, 0.25f);
			Audio.Sfx.Play(Audio.Snd.CutIn, 0.6f, pitch: 0.8f);
		});
		Fx.FreeAfter(root, 1.15);
	}

	/// <summary>征服者：一把剑从天而降插在敌人面前的地上，剑上展开一面光之旗帜，猎猎飘动。</summary>
	public static void Conqueror(Control back, Control front, Vector2 target, Vector2 feet)
	{
		Vector2 ground = new(target.X - 140f, feet.Y);
		Node2D root = Fx.Root(front, ground, "P3Conqueror");
		Node2D sw = Sword(root, 220f, Pal.Royal);
		sw.Rotation = Mathf.Pi / 2f;
		sw.Position = new Vector2(0f, -220f);
		Polygon2D flag = Fx.Poly(root, new Vector2[8], new Color(Pal.Royal, 0.85f));
		Line2D flagEdge = Fx.Line(root, new Vector2[8], Pal.Ice, 2f, closed: true);
		Fx.Animate(root, 1.4f, t =>
		{
			float time = t * 1.4f;
			float drop = Ease.InCubic(Mathf.Clamp(time / 0.18f, 0f, 1f));
			sw.Position = new Vector2(0f, Mathf.Lerp(-900f, -200f, drop));
			float unfurl = Ease.OutBack(Mathf.Clamp((time - 0.3f) / 0.3f, 0f, 1f));
			var pts = new Vector2[8];
			for (int i = 0; i < 4; i++)
			{
				float x = 110f * unfurl * i / 3f;
				float wave = 8f * Mathf.Sin(time * 9f - i * 1.3f) * (i / 3f);
				pts[i] = new Vector2(x, -190f + wave);
				pts[7 - i] = new Vector2(x, -120f + wave + 10f * (i / 3f));
			}
			flag.Polygon = pts;
			Fx.Reshape(flag, pts);
			flagEdge.Points = pts;
			root.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp((time - 1.15f) / 0.25f, 0f, 1f));
		});
		Fx.After(root, 0.18, "conqueror plant", () =>
		{
			Motifs.GroundCrack(front, ground, 0.8f);
			Bang(front, ground + new Vector2(0f, -20f), Pal.Ice, 0.8f);
			Shake(ShakeStrength.Medium);
			Audio.Sfx.Play(Audio.Snd.StarSpend, 0.7f, pitch: 0.7f);
		});
		Fx.FreeAfter(root, 1.45);
	}

	/// <summary>追踪之刃：头顶一把剑分裂成数道光刃，各自划着弧线锁定每个敌人，敌人身上亮起准星。</summary>
	public static void SeekingEdge(Control front, Vector2 head, Vector2[] targets)
	{
		Vector2 at = head + new Vector2(30f, -110f);
		Node2D origin = Fx.Root(front, at, "P3SeekOrigin");
		Node2D sw = Sword(origin, 150f, Pal.Cyan);
		sw.Rotation = -Mathf.Pi / 2f;
		Fx.Animate(origin, 0.4f, t => origin.Modulate = new Color(1f, 1f, 1f, Ease.Envelope(t, 0.2f, 0.7f)));
		Fx.FreeAfter(origin, 0.42);
		for (int i = 0; i < targets.Length; i++)
		{
			Vector2 to = targets[i];
			float side = i % 2 == 0 ? -1f : 1f;
			Fx.After(origin, 0.3 + 0.08 * i, "seek blade", () =>
			{
				Vector2 ctrl = (at + to) / 2f + new Vector2(0f, -260f * side);
				Node2D b = Fx.Root(front, at, "P3SeekBlade");
				Sword(b, 90f, Pal.Cyan);
				Fx.Animate(b, 0.35f, t =>
				{
					float e = Ease.InOutCubic(t);
					Vector2 p = Bezier(at, ctrl, to, e), q = Bezier(at, ctrl, to, Mathf.Min(1f, e + 0.02f));
					b.GlobalPosition = p;
					b.Rotation = (q - p).Angle();
				});
				Fx.After(b, 0.35, "seek lock", () =>
				{
					b.QueueFree();
					P3Fx.Reticle(front, to, 90f, Pal.Ice);
					P3Fx.Flash(front, to, Pal.Ice, 140f, 0.15f);
					Audio.Sfx.Play(Audio.Snd.Weak, 0.4f, pitch: 1.3f);
				});
			});
		}
	}

	// ══════════════════ 抽牌 / 生成系 ══════════════════

	/// <summary>微光：他身边浮起一圈细小的光点，像萤火一样慢慢升起、明灭，其中一颗忽然变亮、眨一下。</summary>
	public static void Glimmer(Control back, Control front, Vector2 center)
	{
		Node2D root = Fx.Root(front, center, "P3Glimmer");
		GpuParticles2D motes = Fx.Sparks(root, Pal.Ice, 40, 10f, 40f, 1.4f, gravity: -60f, scaleMin: 0.08f, scaleMax: 0.22f, oneShot: false);
		ParticleProcessMaterial pm = (ParticleProcessMaterial)motes.ProcessMaterial;
		pm.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
		pm.EmissionSphereRadius = 160f;
		Fx.After(root, 0.7, "glimmer off", () => motes.Emitting = false);
		Fx.After(root, 0.45, "glimmer wink", () =>
		{
			Vector2 at = center + new Vector2(90f, -120f);
			Node2D star = Fx.Root(front, at, "P3GlimmerWink");
			Fx.Poly(star, Fx.Star4(40f, 0.12f), Pal.White, additive: true);
			Fx.Animate(star, 0.4f, t => { star.Scale = Vector2.One * Mathf.Sin(t * Mathf.Pi); star.Rotation = t * 1.5f; });
			Fx.FreeAfter(star, 0.42);
			Audio.Sfx.Play(Audio.Snd.Weak, 0.35f, pitch: 1.6f);
		});
		P3Fx.Flash(back, center, Pal.Ice, 260f, 0.4f);
		Fx.FreeAfter(root, 2.3);
	}

	/// <summary>指导：他抬手一指，一道细光从指尖射向抽牌堆，抽牌堆上亮起准星并"叮"一下。</summary>
	public static void Tutor(Control front, Vector2 hand)
	{
		Vector2 size = front.GetViewportRect().Size;
		Vector2 pile = new(70f, size.Y - 60f);
		Node2D root = Fx.Root(front, Vector2.Zero, "P3TutorRay");
		root.TopLevel = true;
		root.GlobalPosition = Vector2.Zero;
		Line2D ray = Fx.Line(root, new[] { hand, hand }, new Color(Pal.Ice, 0.9f), 4f);
		Fx.Animate(root, 0.5f, t =>
		{
			float e = Ease.OutExpo(Mathf.Clamp(t / 0.35f, 0f, 1f));
			ray.Points = new[] { hand, hand.Lerp(pile, e) };
			ray.DefaultColor = new Color(Pal.Ice, 0.9f * (1f - Mathf.Clamp((t - 0.6f) / 0.4f, 0f, 1f)));
		});
		Fx.After(root, 0.18, "tutor lock", () =>
		{
			P3Fx.Reticle(front, pile, 70f, Pal.Ice);
			P3Fx.Flash(front, pile, Pal.Ice, 120f, 0.2f);
			Audio.Sfx.Play(Audio.Snd.Weak, 0.45f, pitch: 1.4f);
		});
		Fx.FreeAfter(root, 0.55);
	}

	/// <summary>筹划：脚下投出一块棋盘格，一枚光之骑士沿"日"字走两步，每落一格那格亮起。</summary>
	public static void Plot(Control back, Control front, Vector2 feet)
	{
		Node2D board = Fx.Root(back, feet + new Vector2(30f, -10f), "P3PlotBoard");
		board.Scale = new Vector2(1f, 0.38f);
		const float cell = 52f;
		var cells = new Dictionary<(int, int), Polygon2D>();
		for (int x = -3; x <= 3; x++)
		{
			for (int y = -3; y <= 3; y++)
			{
				bool dark = (x + y) % 2 == 0;
				Polygon2D p = Fx.Poly(board, RectPts(new Rect2(x * cell - cell / 2f, y * cell - cell / 2f, cell, cell)), dark ? new Color(0.05f, 0.12f, 0.35f, 0.7f) : new Color(0.75f, 0.85f, 1f, 0.25f));
				cells[(x, y)] = p;
			}
		}
		(int, int)[] path = { (-2, 2), (-1, 0), (1, -1) };          // 骑士的两步
		Node2D knight = Fx.Root(front, Vector2.Zero, "P3PlotKnight");
		Fx.Poly(knight, new[] { new Vector2(-14f, 0f), new Vector2(14f, 0f), new Vector2(12f, -20f), new Vector2(16f, -44f), new Vector2(-2f, -60f), new Vector2(-16f, -40f), new Vector2(-6f, -32f), new Vector2(-12f, -16f) }, new Color(0.85f, 0.92f, 1f));
		Fx.Line(knight, new[] { new Vector2(-14f, 0f), new Vector2(14f, 0f), new Vector2(12f, -20f), new Vector2(16f, -44f), new Vector2(-2f, -60f), new Vector2(-16f, -40f), new Vector2(-6f, -32f), new Vector2(-12f, -16f) }, Pal.Royal, 2f, closed: true, additive: false);
		Vector2 Cell((int, int) c) => board.GlobalPosition + new Vector2(c.Item1 * cell, c.Item2 * cell * 0.38f);
		Fx.Animate(board, 1.5f, t =>
		{
			float time = t * 1.5f;
			board.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(time / 0.15f, 0f, 1f) * (1f - Mathf.Clamp((time - 1.2f) / 0.3f, 0f, 1f)));
			int step = time < 0.45f ? 0 : time < 0.85f ? 1 : 2;
			float u = step == 0 ? 0f : Mathf.Clamp((time - (step == 1 ? 0.3f : 0.7f)) / 0.15f, 0f, 1f);
			Vector2 a = Cell(path[Mathf.Max(0, step - 1)]), b = Cell(path[step]);
			Vector2 p = a.Lerp(b, Ease.InOutCubic(u)) + new Vector2(0f, -50f * Mathf.Sin(u * Mathf.Pi));
			knight.GlobalPosition = step == 0 ? Cell(path[0]) : p;
			knight.Modulate = board.Modulate;
		});
		for (int i = 0; i < path.Length; i++)
		{
			var c = path[i];
			Fx.After(board, i == 0 ? 0.15 : (i == 1 ? 0.45 : 0.85), "plot step", () =>
			{
				if (cells.TryGetValue(c, out Polygon2D? p) && GodotObject.IsInstanceValid(p))
				{
					p.Color = new Color(Pal.Cyan, 0.9f);
				}
				P3Fx.Flash(front, Cell(c), Pal.Ice, 90f, 0.15f);
				Audio.Sfx.Play(Audio.Snd.Weak, 0.35f, pitch: 1.1f + 0.1f * i);
			});
		}
		Fx.FreeAfter(board, 1.55);
		Fx.FreeAfter(knight, 1.55);
	}

	/// <summary>慷慨捐助：头顶散开八颗金色光球，沿弧线飘向四周（分给所有人），落地化成金色涟漪。</summary>
	public static void Largesse(Control back, Control front, Vector2 head, Vector2 feet)
	{
		Vector2 at = head + new Vector2(0f, -80f);
		Color gold = new(1f, 0.85f, 0.45f);
		P3Fx.Flash(front, at, gold, 200f, 0.25f);
		for (int i = 0; i < 8; i++)
		{
			float a = -Mathf.Pi / 2f + (i - 3.5f) * 0.38f;
			Vector2 to = feet + new Vector2((i - 3.5f) * 110f, Fx.Rand(-10f, 10f));
			Vector2 ctrl = at + Vector2.Right.Rotated(a) * 220f;
			Node2D orb = Fx.Root(front, at, "P3LargesseOrb");
			Fx.Poly(orb, Fx.Circle(10f, 16), gold, additive: true);
			Sprite2D g = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Scale = Vector2.One * 1.4f, Modulate = new Color(gold, 0.7f) };
			orb.AddChild(g);
			float d = 0.05f * i;
			Fx.Animate(orb, 0.7f + d, t =>
			{
				float u = Mathf.Clamp((t * (0.7f + d) - d) / 0.7f, 0f, 1f);
				orb.GlobalPosition = Bezier(at, ctrl, to, Ease.InOutCubic(u));
			});
			Fx.After(orb, 0.7 + d, "largesse land", () =>
			{
				P3Fx.Shockwave(back, to, gold, 160f, 0.35f, squashY: 0.3f);
				orb.QueueFree();
			});
		}
		Audio.Sfx.Play(Audio.Snd.StarGain, 0.7f);
	}

	/// <summary>新生之喜：头顶一颗光蛋裂开，涌出一大群小蓝蝶和金色光屑，像烟花一样散开。</summary>
	public static void BundleOfJoy(Control back, Control front, Vector2 head)
	{
		Vector2 at = head + new Vector2(-20f, -110f);
		Node2D egg = Fx.Root(front, at, "P3JoyEgg");
		Fx.Poly(egg, Fx.Circle(34f, 32, 1.3f), new Color(1f, 0.95f, 0.85f), additive: true);
		Sprite2D g = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Scale = Vector2.One * 4f, Modulate = new Color(1f, 0.85f, 0.5f, 0.6f) };
		egg.AddChild(g);
		Fx.Animate(egg, 0.45f, t =>
		{
			egg.Scale = Vector2.One * Ease.OutBack(Mathf.Clamp(t / 0.4f, 0f, 1f)) * (1f + 0.08f * Mathf.Sin(t * 40f) * t);
		});
		Fx.After(egg, 0.45, "joy burst", () =>
		{
			egg.QueueFree();
			P3Fx.GlassShatter(front, at, 16, 0.5f, new Color(1f, 0.92f, 0.75f));
			Bang(front, at, new Color(1f, 0.85f, 0.5f), 0.8f);
			for (int i = 0; i < 9; i++)
			{
				Vector2 to = at + Vector2.Up.Rotated(Fx.Rand(-1.6f, 1.6f)) * Fx.Rand(220f, 420f);
				P3Fx.Butterfly(front, at, to, Fx.Rand(0.9f, 1.4f), Fx.Rand(0.6f, 0.9f), i % 3 == 0 ? new Color(1f, 0.85f, 0.5f) : null);
			}
			Node2D sp = Fx.Root(front, at, "P3JoySparks");
			Fx.Sparks(sp, new Color(1f, 0.85f, 0.5f), 60, 200f, 500f, 1.0f, gravity: 260f, scaleMin: 0.1f, scaleMax: 0.25f, ramp: new[] { Pal.White, new Color(1f, 0.85f, 0.5f), new Color(Pal.Cyan, 0f) });
			Fx.FreeAfter(sp, 1.2);
			Audio.Sfx.Play(Audio.Snd.TarotShatter, 0.6f, pitch: 1.3f);
		});
	}

	/// <summary>光谱偏移：一道白光从左上射进头顶的三棱镜，从另一侧散成一扇柔和的光谱（青 → 蓝 → 紫 → 金）。</summary>
	public static void SpectrumShift(Control back, Control front, Vector2 head)
	{
		Vector2 at = head + new Vector2(60f, -150f);
		Node2D root = Fx.Root(back, at, "P3Prism");
		Vector2[] tri = { new(0f, -60f), new(52f, 30f), new(-52f, 30f) };
		Fx.Poly(root, tri, new Color(0.8f, 0.9f, 1f, 0.25f), additive: true);
		Fx.Line(root, tri, Pal.White, 3f, closed: true);
		Line2D inBeam = Fx.Line(root, new[] { new Vector2(-420f, -160f), new Vector2(-24f, -10f) }, new Color(Pal.White, 0f), 8f);
		Color[] spec = { Pal.Cyan, Pal.Royal, new(0.6f, 0.4f, 1f), new(1f, 0.85f, 0.45f), Pal.Ice };
		var fans = new List<Polygon2D>();
		for (int i = 0; i < spec.Length; i++)
		{
			fans.Add(Fx.Poly(root, new Vector2[3], new Color(spec[i], 0f), additive: true));
		}
		Node2D prism = new();
		root.AddChild(prism);
		foreach (Node n in root.GetChildren())
		{
			if (n != prism && n is Polygon2D or Line2D && fans.TrueForAll(f => f != n) && n != inBeam)
			{
				n.Reparent(prism, keepGlobalTransform: false);
			}
		}
		const float total = 1.6f;
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			// 退场分三步、互相错开：入射光先从左往右收回（0.9–1.1）→ 光谱从远端往棱镜收拢（1.0–1.35）→ 棱镜转着缩小、闪一下消失（1.3–1.6）
			float beamOut = Ease.InCubic(Mathf.Clamp((time - 0.9f) / 0.2f, 0f, 1f));
			float fanOut = Ease.InOutCubic(Mathf.Clamp((time - 1.0f) / 0.35f, 0f, 1f));
			float prismOut = Ease.InBackLike(Mathf.Clamp((time - 1.3f) / 0.3f, 0f, 1f));
			Vector2 b0 = new(-420f, -160f), b1 = new(-24f, -10f);
			inBeam.Points = new[] { b0.Lerp(b1, beamOut), b1 };
			inBeam.DefaultColor = new Color(Pal.White, 0.9f * Mathf.Clamp(time / 0.15f, 0f, 1f) * (1f - beamOut));
			float spread = Ease.OutExpo(Mathf.Clamp((time - 0.2f) / 0.4f, 0f, 1f));
			for (int i = 0; i < fans.Count; i++)
			{
				float a0 = 0.15f + i * 0.13f, a1 = a0 + 0.12f;
				float far = 520f * spread * (1f - fanOut * (1f - i * 0.04f));
				float near = 520f * spread * fanOut * 0.0f;
				Vector2 o = new(24f, 0f);
				Fx.Reshape(fans[i], new[] { o + Vector2.Right.Rotated((a0 + a1) / 2f) * near, o + Vector2.Right.Rotated(a0) * far, o + Vector2.Right.Rotated(a1) * far });
				fans[i].Color = new Color(spec[i], 0.35f * Mathf.Clamp(time / 0.2f, 0f, 1f) * (1f - fanOut * 0.6f));
			}
			prism.Scale = Vector2.One * (1f - prismOut);
			prism.Rotation = prismOut * 2.5f;
			root.Rotation = 0.05f * Mathf.Sin(time * 2f);
		});
		Fx.After(root, 0.2, "prism hit", () => { P3Fx.Flash(back, at, Pal.White, 160f, 0.2f); Audio.Sfx.Play(Audio.Snd.TarotReveal, 0.5f, pitch: 1.3f); });
		Fx.After(root, total - 0.08, "prism out", () =>
		{
			P3Fx.Flash(front, at, Pal.White, 120f, 0.18f);
			Node2D star = Fx.Root(front, at, "P3PrismStar");
			Fx.Poly(star, Fx.Star4(28f, 0.12f), Pal.White, additive: true);
			Fx.Animate(star, 0.3f, t => { star.Scale = Vector2.One * Mathf.Sin(t * Mathf.Pi); star.Rotation = t * 2f; });
			Fx.FreeAfter(star, 0.32);
		});
		Fx.FreeAfter(root, total + 0.02);
	}

	/// <summary>类星体：头顶一个极亮的点，上下喷出两道细长的喷流，绕着一圈压扁的吸积盘旋转。</summary>
	public static void Quasar(Control back, Control front, Vector2 head)
	{
		Vector2 at = head + new Vector2(-30f, -160f);
		Node2D root = Fx.Root(back, at, "P3Quasar");
		root.Rotation = 0.35f;
		Node2D disk = new() { Scale = new Vector2(1f, 0.25f) };
		root.AddChild(disk);
		Fx.Line(disk, Fx.Circle(120f, 64), new Color(Pal.Cyan, 0.7f), 6f, closed: true);
		Fx.Line(disk, Fx.Circle(90f, 64), new Color(1f, 0.85f, 0.5f, 0.6f), 3f, closed: true);
		Polygon2D jetUp = Fx.Poly(root, new Vector2[3], new Color(Pal.Ice, 0.7f), additive: true);
		Polygon2D jetDn = Fx.Poly(root, new Vector2[3], new Color(Pal.Ice, 0.7f), additive: true);
		Fx.Poly(root, Fx.Circle(16f, 24), Pal.White, additive: true);
		Fx.Animate(root, 1.3f, t =>
		{
			float time = t * 1.3f;
			float L = 480f * Ease.OutExpo(Mathf.Clamp((time - 0.15f) / 0.3f, 0f, 1f));
			Fx.Reshape(jetUp, new[] { new Vector2(-10f, 0f), new Vector2(10f, 0f), new Vector2(0f, -L) });
			Fx.Reshape(jetDn, new[] { new Vector2(-10f, 0f), new Vector2(10f, 0f), new Vector2(0f, L) });
			disk.Rotation = time * 4f;
			root.Scale = Vector2.One * Ease.OutBack(Mathf.Clamp(time / 0.2f, 0f, 1f));
			root.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp((time - 1.0f) / 0.3f, 0f, 1f));
		});
		Fx.After(root, 0.15, "quasar ignite", () => { P3Fx.Flash(back, at, Pal.White, 300f, 0.3f); Shake(ShakeStrength.Weak); Audio.Sfx.Play(Audio.Snd.TarotReveal, 0.6f, pitch: 0.8f); });
		Fx.FreeAfter(root, 1.35);
	}

	/// <summary>大爆炸：画面瞬间压黑，只剩一个白点；白点炸开成一圈圈彩色的膨胀环和向外飞散的星尘。</summary>
	public static void BigBang(Control back, Control front, Vector2 at)
	{
		Vector2 size = front.GetViewportRect().Size;
		Node2D dim = Fx.Root(back, Vector2.Zero, "P3BigBangDim");
		ColorRect black = new() { Position = -size, Size = size * 3f, Color = new Color(0f, 0f, 0.02f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		dim.AddChild(black);
		Fx.Animate(dim, 1.4f, t => black.Color = new Color(0f, 0f, 0.02f, 0.8f * (t < 0.15f ? t / 0.15f : 1f - Ease.OutCubic(Mathf.Clamp((t - 0.3f) / 0.7f, 0f, 1f)))));
		Fx.FreeAfter(dim, 1.45);
		Node2D dot = Fx.Root(front, at, "P3BigBangDot");
		Fx.Poly(dot, Fx.Circle(8f, 16), Pal.White, additive: true);
		Fx.Animate(dot, 0.3f, t => dot.Scale = Vector2.One * (1f - 0.6f * t + 0.2f * Mathf.Sin(t * 60f)));
		Fx.After(dot, 0.3, "big bang", () =>
		{
			dot.QueueFree();
			Shake(ShakeStrength.Strong);
			EvokerShot.HitStop(0.06);
			Color[] rings = { Pal.White, new(1f, 0.85f, 0.45f), Pal.Cyan, Pal.Royal, new(0.6f, 0.4f, 1f) };
			for (int i = 0; i < rings.Length; i++)
			{
				P3Fx.Shockwave(front, at, rings[i], 600f + 500f * i, 0.7f + 0.1f * i, delay: 0.04f * i);
			}
			P3Fx.Flash(front, at, Pal.White, 700f, 0.35f);
			Node2D dust = Fx.Root(front, at, "P3BigBangDust");
			Fx.Sparks(dust, Pal.Ice, 120, 400f, 1200f, 1.0f, scaleMin: 0.1f, scaleMax: 0.35f, ramp: new[] { Pal.White, new Color(1f, 0.85f, 0.45f), Pal.Cyan, new Color(Pal.Royal, 0f) });
			Fx.FreeAfter(dust, 1.2);
			Audio.Sfx.Play(Audio.Snd.TarotShatter, 1f, pitch: 0.6f);
		});
	}

	// ══════════════════ 增益 / 王权系 ══════════════════

	/// <summary>君权自授：脚下盖下一枚巨大的王冠纹章（圆环 + 王冠剪影），砸地的一瞬冲击扩散，纹章慢慢褪去。</summary>
	public static void ManifestAuthority(Control back, Control front, Vector2 feet)
	{
		Node2D root = Fx.Root(back, feet, "P3AuthoritySeal");
		Node2D seal = new() { Scale = new Vector2(1f, 0.32f) };
		root.AddChild(seal);
		Color gold = new(1f, 0.85f, 0.45f);
		Fx.Line(seal, Fx.Circle(190f, 72), gold, 6f, closed: true);
		Fx.Line(seal, Fx.Circle(165f, 72), new Color(gold, 0.6f), 2f, closed: true);
		Vector2[] crown = { new(-90f, 40f), new(90f, 40f), new(100f, -50f), new(50f, -10f), new(0f, -80f), new(-50f, -10f), new(-100f, -50f) };
		Fx.Poly(seal, crown, new Color(gold, 0.45f), additive: true);
		Fx.Line(seal, crown, gold, 4f, closed: true);
		Fx.Animate(root, 1.3f, t =>
		{
			float time = t * 1.3f;
			float drop = Ease.InCubic(Mathf.Clamp(time / 0.2f, 0f, 1f));
			root.Scale = Vector2.One * Mathf.Lerp(1.8f, 1f, drop);
			root.Modulate = new Color(1f, 1f, 1f, drop * (1f - Mathf.Clamp((time - 0.9f) / 0.4f, 0f, 1f)));
		});
		Fx.After(root, 0.2, "authority stamp", () =>
		{
			P3Fx.Shockwave(front, feet, gold, 800f, 0.45f, squashY: 0.3f);
			P3Fx.LightPillar(back, feet, gold, 160f, 700f, 0.4f);
			Shake(ShakeStrength.Medium);
			Audio.Sfx.Play(Audio.Snd.CutIn, 0.5f, pitch: 0.9f);
		});
		Fx.FreeAfter(root, 1.35);
	}

	/// <summary>独白：舞台熄灯，一束硬边的聚光灯从正上方打在他身上，光里飘着灰尘，片刻后灯光收拢熄灭。</summary>
	public static void Monologue(Control back, Control front, Vector2 head, Vector2 feet)
	{
		Vector2 size = front.GetViewportRect().Size;
		Node2D root = Fx.Root(back, Vector2.Zero, "P3Spotlight");
		ColorRect dark = new() { Position = -size, Size = size * 3f, Color = new Color(0f, 0f, 0.03f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(dark);
		Polygon2D cone = Fx.Poly(root, new Vector2[4], new Color(1f, 0.97f, 0.88f, 0f), additive: true);
		Node2D pool = Fx.Root(back, feet, "P3SpotPool");
		Polygon2D floor = Fx.Poly(pool, Fx.Circle(150f, 48, 0.22f), new Color(1f, 0.97f, 0.88f, 0f), additive: true);
		Node2D dustAt = Fx.Root(front, (head + feet) / 2f, "P3SpotDust");
		GpuParticles2D dust = Fx.Sparks(dustAt, new Color(1f, 0.97f, 0.88f), 30, 5f, 20f, 1.6f, gravity: 10f, scaleMin: 0.06f, scaleMax: 0.15f, oneShot: false);
		((ParticleProcessMaterial)dust.ProcessMaterial).EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
		((ParticleProcessMaterial)dust.ProcessMaterial).EmissionBoxExtents = new Vector3(110f, 220f, 0f);
		Fx.Animate(root, 1.6f, t =>
		{
			float time = t * 1.6f;
			float on = time < 0.12f ? 0f : 1f - Mathf.Clamp((time - 1.25f) / 0.3f, 0f, 1f);
			float width = 1f - 0.7f * Mathf.Clamp((time - 1.25f) / 0.3f, 0f, 1f);
			dark.Color = new Color(0f, 0f, 0.03f, 0.55f * Mathf.Clamp(time / 0.1f, 0f, 1f) * (1f - Mathf.Clamp((time - 1.3f) / 0.3f, 0f, 1f)));
			Fx.Reshape(cone, new[] { new Vector2(head.X - 40f * width, -40f), new Vector2(head.X + 40f * width, -40f), new Vector2(feet.X + 150f * width, feet.Y), new Vector2(feet.X - 150f * width, feet.Y) });
			cone.Color = new Color(1f, 0.97f, 0.88f, 0.22f * on);
			floor.Color = new Color(1f, 0.97f, 0.88f, 0.35f * on);
			pool.Scale = new Vector2(width, 1f);
		});
		Fx.After(root, 0.12, "spot on", () => Audio.Sfx.Play(Audio.Snd.Gunshot, 0.25f, pitch: 0.5f));
		Fx.After(root, 1.0, "dust off", () => dust.Emitting = false);
		Fx.FreeAfter(root, 1.65);
		Fx.FreeAfter(pool, 1.65);
		Fx.FreeAfter(dustAt, 2.6);
	}

	/// <summary>共鸣：他身前荡出一组声波弧线（音叉般一层层），扫过敌人时敌人身上震一下、泛起涟漪。</summary>
	public static void Resonance(Control back, Control front, Vector2 center, Vector2[] targets)
	{
		for (int i = 0; i < 5; i++)
		{
			float d = 0.09f * i;
			Fx.After(front, d, "resonance wave", () =>
			{
				Node2D arc = Fx.Root(front, center + new Vector2(40f, 0f), "P3ResonanceArc");
				var pts = new Vector2[17];
				for (int k = 0; k <= 16; k++)
				{
					pts[k] = Vector2.Right.Rotated(-0.7f + 1.4f * k / 16f) * 60f;
				}
				Line2D l = Fx.Line(arc, pts, new Color(Pal.Ice, 0.9f), 5f);
				Fx.Animate(arc, 0.7f, t =>
				{
					arc.Scale = Vector2.One * (1f + 14f * Ease.OutCubic(t));
					l.Width = 5f / arc.Scale.X;
					arc.Modulate = new Color(1f, 1f, 1f, 1f - t);
				});
				Fx.FreeAfter(arc, 0.72);
			});
		}
		foreach (Vector2 t in targets)
		{
			Fx.After(front, 0.4, "resonance hit", () => { P3Fx.Ripple(back, t, 300f, 0.5f, 0.04f); P3Fx.Flash(front, t, Pal.Ice, 120f, 0.15f); });
		}
		Audio.Sfx.Play(Audio.Snd.Weak, 0.55f, pitch: 0.65f);
	}

	/// <summary>冲锋！！：前方地面卷起一道冲击波向敌人推进，速度线从身后掠过，扬尘一路翻起。</summary>
	public static void Charge(Control back, Control front, Vector2 feet, Vector2 target)
	{
		Vector2 size = front.GetViewportRect().Size;
		P3Fx.SpeedLines(front, feet + new Vector2(-60f, -160f), Pal.Ice, 18, 80f, 700f, 0.4f);
		Node2D wave = Fx.Root(front, feet + new Vector2(60f, 0f), "P3ChargeWave");
		Polygon2D crest = Fx.Poly(wave, new[] { new Vector2(-30f, 0f), new Vector2(0f, -90f), new Vector2(40f, -40f), new Vector2(60f, 0f) }, new Color(Pal.Ice, 0.8f), additive: true);
		float endX = Mathf.Min(target.X + 120f, size.X - 40f);
		Fx.Animate(wave, 0.5f, t =>
		{
			float e = Ease.OutCubic(t);
			wave.GlobalPosition = new Vector2(Mathf.Lerp(feet.X + 60f, endX, e), feet.Y);
			wave.Scale = new Vector2(1f + t, 1f - 0.5f * t);
			wave.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(t));
		});
		for (int i = 0; i < 6; i++)
		{
			float d = 0.06f * i;
			Fx.After(wave, d, "charge dust", () => P3Fx.Shockwave(back, wave.GlobalPosition, Pal.Ice, 200f, 0.35f, squashY: 0.25f));
		}
		Shake(ShakeStrength.Medium);
		Audio.Sfx.Play(Audio.Snd.CutIn, 0.5f, pitch: 1.1f);
		Fx.FreeAfter(wave, 0.52);
	}

	/// <summary>地形改造：他脚下一圈六边形石板依次从地里顶起、落回，地面裂开一圈。</summary>
	public static void Terraforming(Control back, Control front, Vector2 feet)
	{
		Motifs.GroundCrack(front, feet, 1.3f);
		for (int i = 0; i < 10; i++)
		{
			float a = Mathf.Tau * i / 10f;
			Vector2 at = feet + new Vector2(Mathf.Cos(a) * 190f, Mathf.Sin(a) * 50f);
			Node2D slab = Fx.Root(back, at, "P3TerraSlab");
			Node2D body = new() { Scale = new Vector2(1f, 0.55f) };
			slab.AddChild(body);
			Fx.Poly(body, Fx.Hexagon(30f), new Color(0.35f, 0.42f, 0.55f));
			Fx.Line(body, Fx.Hexagon(30f), new Color(1f, 0.8f, 0.45f, 0.9f), 3f, closed: true);
			Polygon2D side = Fx.Poly(slab, RectPts(new Rect2(-30f, 0f, 60f, 0f)), new Color(0.2f, 0.25f, 0.35f));
			slab.MoveChild(side, 0);
			float d = 0.05f * i;
			Fx.Animate(slab, 0.8f + d, t =>
			{
				float u = Mathf.Clamp((t * (0.8f + d) - d) / 0.8f, 0f, 1f);
				float h = 60f * Mathf.Sin(Mathf.Clamp(u / 0.5f, 0f, 1f) * Mathf.Pi * 0.5f) * (1f - Ease.InCubic(Mathf.Clamp((u - 0.55f) / 0.45f, 0f, 1f)));
				body.Position = new Vector2(0f, -h);
				Fx.Reshape(side, RectPts(new Rect2(-26f, -h, 52f, h)));
				slab.Modulate = new Color(1f, 1f, 1f, u > 0f ? 1f - Mathf.Clamp((u - 0.85f) / 0.15f, 0f, 1f) : 0f);
			});
			Fx.FreeAfter(slab, 0.85 + d);
		}
		Shake(ShakeStrength.Medium);
		Audio.Sfx.Play(Audio.Snd.StarSpend, 0.7f, pitch: 0.55f);
	}

	/// <summary>星位序列：头顶一条弧形轨道上，四颗小行星从散乱的位置滑进同一条直线，连成一线时一道光贯穿。</summary>
	public static void Alignment(Control back, Control front, Vector2 above)
	{
		Node2D root = Fx.Root(front, above, "P3Alignment");
		Node2D orbit = new() { Scale = new Vector2(1f, 0.35f) };
		root.AddChild(orbit);
		Fx.Line(orbit, Fx.Circle(220f, 64), new Color(Pal.Ice, 0.35f), 2f, closed: true);
		Color[] cols = { Pal.Cyan, new(1f, 0.85f, 0.45f), Pal.Ice, new(0.6f, 0.5f, 1f) };
		float[] start = { -2.4f, -0.4f, 1.6f, 2.8f };
		var planets = new List<Node2D>();
		for (int i = 0; i < 4; i++)
		{
			Node2D p = new();
			root.AddChild(p);
			Fx.Poly(p, Fx.Circle(9f + 3f * (i % 2), 20), cols[i], additive: true);
			Sprite2D g = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Scale = Vector2.One * 1.2f, Modulate = new Color(cols[i], 0.6f) };
			p.AddChild(g);
			planets.Add(p);
		}
		Line2D beam = Fx.Line(root, new[] { new Vector2(-300f, 0f), new Vector2(300f, 0f) }, new Color(Pal.White, 0f), 5f);
		Fx.Animate(root, 1.3f, t =>
		{
			float time = t * 1.3f;
			float align = Ease.InOutCubic(Mathf.Clamp((time - 0.1f) / 0.5f, 0f, 1f));
			for (int i = 0; i < 4; i++)
			{
				Vector2 free = new Vector2(Mathf.Cos(start[i] + time), Mathf.Sin(start[i] + time) * 0.35f) * 220f;
				Vector2 lined = new((i - 1.5f) * 120f, 0f);
				planets[i].Position = free.Lerp(lined, align);
			}
			beam.DefaultColor = new Color(Pal.White, 0.9f * Mathf.Clamp((time - 0.6f) / 0.05f, 0f, 1f) * (1f - Mathf.Clamp((time - 0.75f) / 0.3f, 0f, 1f)));
			root.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(time / 0.1f, 0f, 1f) * (1f - Mathf.Clamp((time - 1.05f) / 0.25f, 0f, 1f)));
		});
		Fx.After(root, 0.6, "aligned", () => { P3Fx.Flash(front, above, Pal.White, 200f, 0.2f); Audio.Sfx.Play(Audio.Snd.Weak, 0.5f, pitch: 1.2f); });
		Fx.FreeAfter(root, 1.35);
	}

	/// <summary>群星之子：他身周升起一圈星星组成的光环（像星之摇篮），缓缓旋转、收紧，最后每颗星闪一下融进身体。</summary>
	public static void ChildOfTheStars(Control back, Control front, Vector2 center)
	{
		Node2D root = Fx.Root(front, center, "P3StarChild");
		var stars = new List<Node2D>();
		for (int i = 0; i < 12; i++)
		{
			Node2D s = new();
			root.AddChild(s);
			Fx.Poly(s, Fx.Star4(i % 3 == 0 ? 14f : 8f, 0.25f), Pal.White, additive: true);
			stars.Add(s);
		}
		Fx.Animate(root, 1.5f, t =>
		{
			float time = t * 1.5f;
			float R = Mathf.Lerp(230f, 40f, Ease.InCubic(Mathf.Clamp((time - 0.8f) / 0.5f, 0f, 1f)));
			for (int i = 0; i < stars.Count; i++)
			{
				float a = Mathf.Tau * i / stars.Count + time * 1.2f;
				float rise = Ease.OutCubic(Mathf.Clamp((time - i * 0.03f) / 0.3f, 0f, 1f));
				stars[i].Position = new Vector2(Mathf.Cos(a) * R, Mathf.Sin(a) * R * 0.4f + 120f * (1f - rise));
				stars[i].Rotation = time * 3f;
				stars[i].Modulate = new Color(1f, 1f, 1f, rise * (1f - Mathf.Clamp((time - 1.25f) / 0.2f, 0f, 1f)));
			}
		});
		Fx.After(root, 1.3, "star child absorb", () => { P3Fx.Flash(front, center, Pal.Ice, 260f, 0.25f); Audio.Sfx.Play(Audio.Snd.StarGain, 0.8f); });
		Fx.FreeAfter(root, 1.55);
	}

	/// <summary>环绕轨道：三条不同倾角的轨道环（像陀螺仪）绕着他展开、各自旋转，环上的光点追着跑。</summary>
	public static void Orbit(Control back, Control front, Vector2 center)
	{
		Node2D root = Fx.Root(front, center, "P3Gyro");
		float[] tilt = { 0.2f, -0.9f, 1.1f };
		var rings = new List<Node2D>();
		var dots = new List<Node2D>();
		for (int i = 0; i < 3; i++)
		{
			Node2D r = new() { Rotation = tilt[i] };
			root.AddChild(r);
			Node2D squash = new() { Scale = new Vector2(1f, 0.3f) };
			r.AddChild(squash);
			Fx.Line(squash, Fx.Circle(170f + 20f * i, 64), new Color(Pal.Ice, 0.7f), 2.5f, closed: true);
			Node2D dot = new();
			squash.AddChild(dot);
			Fx.Poly(dot, Fx.Circle(9f, 16), Pal.White, additive: true);
			rings.Add(r);
			dots.Add(dot);
		}
		Fx.Animate(root, 1.5f, t =>
		{
			float time = t * 1.5f;
			root.Scale = Vector2.One * Ease.OutBack(Mathf.Clamp(time / 0.3f, 0f, 1f));
			for (int i = 0; i < 3; i++)
			{
				float a = time * (3f + i);
				dots[i].Position = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (170f + 20f * i);
				rings[i].Rotation = tilt[i] + time * 0.3f * (i % 2 == 0 ? 1f : -1f);
			}
			root.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp((time - 1.2f) / 0.3f, 0f, 1f));
		});
		Audio.Sfx.Play(Audio.Snd.TarotReveal, 0.5f, pitch: 1.1f);
		Fx.FreeAfter(root, 1.55);
	}

	/// <summary>暗淡蓝点：画面暗下来，远处一颗极小的蓝色光点，一道斜穿画面的阳光光带恰好从它身上掠过。</summary>
	public static void PaleBlueDot(Control back, Control front, Vector2 above)
	{
		Vector2 size = front.GetViewportRect().Size;
		Node2D root = Fx.Root(back, Vector2.Zero, "P3PaleBlueDot");
		ColorRect dark = new() { Position = -size, Size = size * 3f, Color = new Color(0f, 0f, 0.02f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(dark);
		Vector2 dotAt = above + new Vector2(200f, -20f);
		Polygon2D band = Fx.Poly(root, new Vector2[4], new Color(1f, 0.9f, 0.7f, 0f), additive: true);
		Polygon2D dot = Fx.Poly(root, Fx.Circle(4f, 12), new Color(0.5f, 0.8f, 1f, 0f), additive: true);
		dot.Position = dotAt;
		Fx.Animate(root, 1.8f, t =>
		{
			float time = t * 1.8f;
			float a = Mathf.Clamp(time / 0.25f, 0f, 1f) * (1f - Mathf.Clamp((time - 1.45f) / 0.35f, 0f, 1f));
			dark.Color = new Color(0f, 0f, 0.02f, 0.6f * a);
			Vector2 dir = new Vector2(1f, 0.55f).Normalized(), n = dir.Orthogonal();
			float w = 26f;
			Fx.Reshape(band, new[] { dotAt - dir * 1400f - n * w, dotAt + dir * 1400f - n * w, dotAt + dir * 1400f + n * w, dotAt - dir * 1400f + n * w });
			band.Color = new Color(1f, 0.9f, 0.7f, 0.18f * a);
			dot.Color = new Color(0.5f, 0.8f, 1f, a);
			dot.Scale = Vector2.One * (1f + 0.3f * Mathf.Sin(time * 5f));
		});
		Fx.After(root, 0.6, "pale dot", () => Audio.Sfx.Play(Audio.Snd.Weak, 0.3f, pitch: 1.8f));
		Fx.FreeAfter(root, 1.85);
	}

	// ══════════════════ 防御系 ══════════════════

	/// <summary>招架：身前两把光剑交叉成 X 格挡，"铛"地一声火花四溅，随后剑身化光散去。</summary>
	public static void Parry(Control front, Vector2 center)
	{
		Vector2 at = center + new Vector2(110f, -20f);
		Node2D root = Fx.Root(front, at, "P3Parry");
		Node2D a = Sword(root, 170f, Pal.Cyan);
		Node2D b = Sword(root, 170f, Pal.Cyan);
		Fx.Animate(root, 0.8f, t =>
		{
			float time = t * 0.8f;
			float cross = Ease.OutBack(Mathf.Clamp(time / 0.15f, 0f, 1f));
			a.Rotation = Mathf.Lerp(-2.4f, -0.8f - Mathf.Pi / 2f + 0.8f, cross) ;
			b.Rotation = Mathf.Lerp(-0.7f, -2.35f, cross);
			a.Position = new Vector2(-70f, 70f).Rotated(0f) * (1f - 0.1f * cross);
			b.Position = new Vector2(70f, 70f) * (1f - 0.1f * cross);
			root.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp((time - 0.55f) / 0.25f, 0f, 1f));
		});
		Fx.After(root, 0.15, "parry clang", () =>
		{
			Node2D sp = Fx.Root(front, at, "P3ParrySparks");
			Fx.Sparks(sp, Pal.Ice, 36, 240f, 600f, 0.4f, gravity: 800f, scaleMin: 0.08f, scaleMax: 0.22f, ramp: new[] { Pal.White, Pal.Ice, new Color(Pal.Cyan, 0f) });
			Fx.FreeAfter(sp, 0.5);
			Bang(front, at, Pal.White, 0.7f);
			Shake(ShakeStrength.Weak);
			Audio.Sfx.Play(Audio.Snd.Weak, 0.7f, pitch: 0.9f);
		});
		Fx.FreeAfter(root, 0.82);
	}

	/// <summary>中子护盾：一圈中子小球高速绕身旋转、越压越近，最后"咔"地压成一层贴身的六角装甲，闪一下。</summary>
	public static void NeutronAegis(Control back, Control front, Vector2 center)
	{
		Node2D root = Fx.Root(front, center, "P3Neutron");
		var dots = new List<Node2D>();
		for (int i = 0; i < 16; i++)
		{
			Node2D d = new();
			root.AddChild(d);
			Fx.Poly(d, Fx.Circle(6f, 12), Pal.Ice, additive: true);
			Sprite2D g = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Scale = Vector2.One * 0.8f, Modulate = new Color(Pal.Cyan, 0.7f) };
			d.AddChild(g);
			dots.Add(d);
		}
		Fx.Animate(root, 0.9f, t =>
		{
			float time = t * 0.9f;
			float R = Mathf.Lerp(260f, 110f, Ease.InCubic(t));
			for (int i = 0; i < dots.Count; i++)
			{
				float a = Mathf.Tau * i / dots.Count + time * (6f + 10f * t) * (i % 2 == 0 ? 1f : -1f);
				dots[i].Position = new Vector2(Mathf.Cos(a) * R, Mathf.Sin(a) * R * (i % 2 == 0 ? 0.45f : 1f));
			}
		});
		Fx.After(root, 0.9, "neutron lock", () =>
		{
			root.QueueFree();
			Extras.HexShield(front, center, 1.4f, false, 0.8f);
			Bang(front, center, Pal.Ice, 0.9f);
			Shake(ShakeStrength.Medium);
			Audio.Sfx.Play(Audio.Snd.StarSpend, 0.8f, pitch: 0.6f);
		});
	}

	/// <summary>所向无敌（物理反射 Tetrakarn）：身前竖起一面由大块晶体拼成的护壁，晶面一道高光扫过，最后整面一亮。</summary>
	public static void Tetrakarn(Control front, Vector2 center)
	{
		Vector2 at = center + new Vector2(130f, -10f);
		Node2D root = Fx.Root(front, at, "P3Tetrakarn");
		var shards = new List<Polygon2D>();
		Vector2[][] facets =
		{
			new[] { new Vector2(-40f, -160f), new Vector2(20f, -180f), new Vector2(10f, -60f), new Vector2(-50f, -70f) },
			new[] { new Vector2(20f, -180f), new Vector2(60f, -120f), new Vector2(50f, -20f), new Vector2(10f, -60f) },
			new[] { new Vector2(-50f, -70f), new Vector2(10f, -60f), new Vector2(0f, 60f), new Vector2(-60f, 40f) },
			new[] { new Vector2(10f, -60f), new Vector2(50f, -20f), new Vector2(45f, 90f), new Vector2(0f, 60f) },
			new[] { new Vector2(-60f, 40f), new Vector2(0f, 60f), new Vector2(-10f, 170f), new Vector2(-50f, 150f) },
			new[] { new Vector2(0f, 60f), new Vector2(45f, 90f), new Vector2(30f, 180f), new Vector2(-10f, 170f) },
		};
		for (int i = 0; i < facets.Length; i++)
		{
			Polygon2D p = Fx.Poly(root, facets[i], new Color(0.55f, 0.75f, 1f, 0.45f), additive: true);
			Fx.Line(root, facets[i], new Color(Pal.Ice, 0.9f), 2f, closed: true);
			shards.Add(p);
		}
		Fx.Animate(root, 1.2f, t =>
		{
			float time = t * 1.2f;
			root.Scale = new Vector2(Ease.OutBack(Mathf.Clamp(time / 0.2f, 0f, 1f)), 1f);
			for (int i = 0; i < shards.Count; i++)
			{
				float sweep = Mathf.Clamp(1f - Mathf.Abs(time - 0.35f - i * 0.05f) / 0.08f, 0f, 1f);
				shards[i].Color = new Color(0.55f + 0.45f * sweep, 0.75f + 0.25f * sweep, 1f, 0.45f + 0.4f * sweep);
			}
			root.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp((time - 0.95f) / 0.25f, 0f, 1f));
		});
		Fx.After(root, 0.2, "tetrakarn up", () => { Bang(front, at, Pal.Ice, 0.8f); Audio.Sfx.Play(Audio.Snd.TarotReveal, 0.6f, pitch: 1.2f); });
		Fx.FreeAfter(root, 1.25);
	}

	/// <summary>倒映（魔法反射 Makarakarn）：身前立起一面细长的镜子，镜中倒映出一道紫青色的光，镜面高光从下扫到上。</summary>
	public static void Makarakarn(Control front, Vector2 center)
	{
		Vector2 at = center + new Vector2(130f, -10f);
		Node2D root = Fx.Root(front, at, "P3Makarakarn");
		Vector2[] frame = { new(-38f, -170f), new(38f, -170f), new(38f, 170f), new(-38f, 170f) };
		Fx.Poly(root, frame, new Color(0.35f, 0.25f, 0.7f, 0.4f), additive: true);
		Fx.Line(root, frame, new Color(0.85f, 0.8f, 1f), 3f, closed: true);
		Polygon2D gl = Fx.Poly(root, new Vector2[4], new Color(1f, 1f, 1f, 0f), additive: true);
		Fx.Animate(root, 1.2f, t =>
		{
			float time = t * 1.2f;
			root.Scale = new Vector2(1f, Ease.OutBack(Mathf.Clamp(time / 0.2f, 0f, 1f)));
			float y = Mathf.Lerp(170f, -170f, Mathf.Clamp((time - 0.25f) / 0.4f, 0f, 1f));
			Fx.Reshape(gl, new[] { new Vector2(-38f, y), new Vector2(38f, y - 40f), new Vector2(38f, y - 10f), new Vector2(-38f, y + 30f) });
			gl.Color = new Color(1f, 1f, 1f, time > 0.25f && time < 0.65f ? 0.6f : 0f);
			root.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp((time - 0.95f) / 0.25f, 0f, 1f));
		});
		Fx.After(root, 0.2, "mirror up", () => { Bang(front, at, new Color(0.7f, 0.6f, 1f), 0.8f); Audio.Sfx.Play(Audio.Snd.TarotReveal, 0.6f, pitch: 1.4f); });
		Fx.FreeAfter(root, 1.25);
	}

	/// <summary>护驾！！！：他两侧各砸下一柄高大的光之长戟（卫兵），戟尖交叉在他头顶，地面一震。</summary>
	public static void Guards(Control back, Control front, Vector2 head, Vector2 feet)
	{
		for (int side = -1; side <= 1; side += 2)
		{
			int s = side;
			Vector2 ground = feet + new Vector2(s * 150f, 0f);
			Node2D root = Fx.Root(back, ground, "P3Halberd");
			Fx.Poly(root, RectPts(new Rect2(-5f, -420f, 10f, 420f)), new Color(0.7f, 0.78f, 0.95f));
			Fx.Poly(root, new[] { new Vector2(0f, -500f), new Vector2(14f, -420f), new Vector2(-14f, -420f) }, Pal.Ice, additive: true);
			Fx.Poly(root, new[] { new Vector2(5f, -440f), new Vector2(60f, -470f), new Vector2(50f, -400f), new Vector2(5f, -410f) }, new Color(Pal.Ice, 0.85f), additive: true);
			root.Scale = new Vector2(-s, 1f);
			Fx.Animate(root, 1.3f, t =>
			{
				float time = t * 1.3f;
				float drop = Ease.InCubic(Mathf.Clamp(time / 0.2f, 0f, 1f));
				root.Position = ground + new Vector2(0f, -600f * (1f - drop));
				float lean = Ease.OutBack(Mathf.Clamp((time - 0.3f) / 0.2f, 0f, 1f));
				root.Rotation = s * -0.28f * lean;
				root.Modulate = new Color(1f, 1f, 1f, drop * (1f - Mathf.Clamp((time - 1.0f) / 0.3f, 0f, 1f)));
			});
			Fx.FreeAfter(root, 1.35);
		}
		Fx.After(front, 0.2, "guards land", () =>
		{
			P3Fx.Shockwave(front, feet, Pal.Ice, 700f, 0.4f, squashY: 0.3f);
			Shake(ShakeStrength.Medium);
			Audio.Sfx.Play(Audio.Snd.StarSpend, 0.8f, pitch: 0.6f);
		});
		Fx.After(front, 0.5, "guards cross", () => P3Fx.Flash(front, head + new Vector2(0f, -200f), Pal.White, 160f, 0.2f));
	}

	// ══════════════════ 攻击补充 ══════════════════

	/// <summary>传承之锤：巨锤带着残影从高处抡下砸在敌人身上，地面炸开一圈。</summary>
	public static void HeirloomHammer(Control front, Vector2 target, Vector2 feet)
	{
		for (int i = 0; i < 3; i++)
		{
			float d = 0.03f * i;
			Fx.After(front, d, "heirloom ghost", () =>
			{
				Node2D root = Fx.Root(front, target + new Vector2(-60f, 0f), "P3Heirloom");
				Node2D h = Hammer(root, 1.9f, Pal.Cyan);
				h.Rotation = Mathf.Pi;
				h.Modulate = new Color(1f, 1f, 1f, i == 0 ? 1f : 0.35f);
				Fx.Animate(root, 0.24f, t => { root.Rotation = Mathf.Lerp(-2.6f, 0.1f, Ease.InCubic(t)); });
				Fx.After(root, 0.24, "heirloom fade", () => Fx.Animate(root, 0.2f, t => root.Modulate = new Color(1f, 1f, 1f, 1f - t)));
				Fx.FreeAfter(root, 0.46);
			});
		}
		Fx.After(front, 0.24, "heirloom hit", () =>
		{
			Motifs.GroundCrack(front, feet, 1.5f);
			Bang(front, target, Pal.Ice, 1.3f);
			Shake(ShakeStrength.Strong);
		});
	}

	/// <summary>王者之踢：一道新月形的脚踢弧光从下往上划过敌人，弧尾拖出残影。</summary>
	public static void KinglyKick(Control front, Vector2 target)
	{
		Node2D root = Fx.Root(front, target, "P3KickArc");
		Polygon2D arc = Fx.Poly(root, new Vector2[3], new Color(Pal.Ice, 0.8f), additive: true);
		Fx.Animate(root, 0.3f, t =>
		{
			float sweep = Ease.OutCubic(t);
			var pts = new List<Vector2>();
			int n = 16;
			float a0 = 2.4f, a1 = Mathf.Lerp(2.4f, 5.4f, sweep);
			for (int i = 0; i <= n; i++) { float a = Mathf.Lerp(a0, a1, i / (float)n); pts.Add(Vector2.Right.Rotated(a) * 170f); }
			for (int i = n; i >= 0; i--) { float a = Mathf.Lerp(a0, a1, i / (float)n); float w = 30f * Mathf.Sin(i / (float)n * Mathf.Pi); pts.Add(Vector2.Right.Rotated(a) * (170f - w)); }
			Fx.Reshape(arc, pts.ToArray());
			root.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(Mathf.Clamp((t - 0.6f) / 0.4f, 0f, 1f)));
		});
		Fx.FreeAfter(root, 0.32);
	}
}
