using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using P3Makoto.Core;

namespace P3Makoto.Vfx;

/// <summary>
/// 稀有牌的独属演出（都不挥剑，改为轻施法 cast_light；伤害时机见 CardFx.OnHit 里的分支）：
/// - 迫降：月亮坠落 —— 满月从右上方的天空砸向敌群，落地时月面碎成月牙碎片、地面裂开、强震屏。
/// - 天际钻头：敌人头顶降下一支旋转的螺旋光锥，每一击向下钻一下迸出火花，最后一击后碎成玻璃。
/// - 王之凝视：敌群上方的天空睁开一只巨大的眼睛，凝视光落到每个敌人身上留下眼形印记；
///   之后每次攻击敌人，被打的敌人身上闪一下这只眼睛（力量下降）。
/// </summary>
public static class Rares
{
	private static void Shake(ShakeStrength s) => MegaCrit.Sts2.Core.Nodes.NGame.Instance?.ScreenShake(s, ShakeDuration.Short);

	private static Vector2[] RectPts(Rect2 r) => new[] { r.Position, new Vector2(r.End.X, r.Position.Y), r.End, new Vector2(r.Position.X, r.End.Y) };

	// ══════════════════════ 迫降：月亮坠落 ══════════════════════

	public const float MoonfallImpact = 0.4f;

	public static void Moonfall(List<NCreature> targets)
	{
		Control? back = Makoto.FxLayer(back: true);
		Control? front = Makoto.FxLayer();
		if (back == null || front == null || targets.Count == 0)
		{
			return;
		}
		Vector2 size = front.GetViewportRect().Size;
		float gx = targets.Average(t => t.VfxSpawnPosition.X);
		float gy = targets.Max(t => t.GetBottomOfHitbox().Y);
		Vector2 ground = new(gx, gy);
		Vector2 start = new(Mathf.Min(gx + 700f, size.X + 250f), -260f);
		// 天色先压暗一下，月亮出现在右上方
		Node2D dim = Fx.Root(back, Vector2.Zero, "P3MoonfallDim");
		ColorRect shade = new() { Position = -size, Size = size * 3f, Color = new Color(0.01f, 0.03f, 0.12f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		dim.AddChild(shade);
		Fx.Animate(dim, 1.2f, t => shade.Color = new Color(0.01f, 0.03f, 0.12f, 0.45f * Ease.Envelope(t, 0.12f, 0.55f)));
		Fx.FreeAfter(dim, 1.25);

		Node2D moon = Fx.Root(front, start, "P3Moonfall");
		ColorRect quad = Fx.ShaderQuad(moon, Shaders.MoonPhase, 560f);
		Fx.SetParam(quad, "phase", 1f);
		// 月亮身后拖一道宽光尾
		Line2D tail = new()
		{
			Width = 150f, Material = Fx.Additive,
			Gradient = new Gradient { Colors = new[] { new Color(Pal.Cyan, 0f), new Color(Pal.Ice, 0.55f) } }
		};
		Node2D tailRoot = Fx.Root(front, Vector2.Zero, "P3MoonfallTail");
		tailRoot.TopLevel = true;
		tailRoot.GlobalPosition = Vector2.Zero;
		tailRoot.AddChild(tail);
		Audio.Sfx.Play(Audio.Snd.CutIn, 0.6f, pitch: 0.7f);
		Fx.Animate(moon, MoonfallImpact, t =>
		{
			float e = Ease.InCubic(t);
			Vector2 p = start.Lerp(ground + new Vector2(0f, -60f), e);
			moon.GlobalPosition = p;
			moon.Scale = Vector2.One * Mathf.Lerp(0.55f, 1.15f, e);
			moon.Rotation = -0.4f * e;
			Fx.SetParam(quad, "flash", 0.6f * e);
			tail.Points = new[] { start.Lerp(p, Mathf.Max(0f, e - 0.45f)), p };
			tail.Width = 150f * moon.Scale.X;
		});
		Fx.After(moon, MoonfallImpact, "moonfall impact", () =>
		{
			moon.QueueFree();
			Fx.Animate(tail, 0.2f, t => tail.Modulate = new Color(1f, 1f, 1f, 1f - t));
			Fx.FreeAfter(tailRoot, 0.22);
			EvokerShot.HitStop(0.08);
			Shake(ShakeStrength.Strong);
			P3Fx.Flash(front, ground + new Vector2(0f, -80f), Pal.White, 900f, 0.35f);
			P3Fx.Shockwave(front, ground, Pal.Ice, 1500f, 0.6f, squashY: 0.35f);
			P3Fx.Ripple(back, ground, 1400f, 0.9f, 0.03f, squashY: 0.4f);
			Motifs.GroundCrack(front, ground, 2.2f);
			// 月面碎成月牙碎片：大块弧形碎片向四周抛出、翻滚、落地
			for (int i = 0; i < 14; i++)
			{
				Node2D shard = Fx.Root(front, ground + new Vector2(Fx.Rand(-60f, 60f), -80f), "P3MoonShard");
				float r = Fx.Rand(18f, 46f);
				Fx.Poly(shard, Shard(r), new Color(0.86f, 0.94f, 1f));
				Fx.Poly(shard, Shard(r * 0.6f), new Color(Pal.Ice, 0.8f), additive: true);
				Vector2 v = Vector2.Up.Rotated(Fx.Rand(-1.3f, 1.3f)) * Fx.Rand(380f, 900f);
				float spin = Fx.Rand(-9f, 9f);
				Vector2 at0 = shard.GlobalPosition;
				Fx.Animate(shard, 0.75f, t =>
				{
					float time = t * 0.75f;
					shard.GlobalPosition = at0 + v * time + new Vector2(0f, 1500f * time * time);
					shard.Rotation = spin * time;
					shard.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(t));
				});
				Fx.FreeAfter(shard, 0.78);
			}
			P3Fx.GlassShatter(front, ground + new Vector2(0f, -80f), 40, 1.4f, Pal.Ice);
			// 落点留下一圈月光，慢慢收掉
			P3Fx.LightPillar(back, ground, Pal.Ice, 380f, 900f, 0.6f);
			Audio.Sfx.Play(Audio.Snd.TarotShatter, 1f, pitch: 0.75f);
		});
	}

	/// <summary>月牙形碎片：不规则的弧形块。</summary>
	private static Vector2[] Shard(float r)
	{
		var pts = new List<Vector2>();
		for (int i = 0; i <= 6; i++)
		{
			float a = -1.2f + 2.4f * i / 6f;
			pts.Add(Vector2.Right.Rotated(a) * r * Fx.Rand(0.9f, 1.1f));
		}
		for (int i = 6; i >= 0; i--)
		{
			float a = -1.0f + 2.0f * i / 6f;
			pts.Add(Vector2.Right.Rotated(a) * r * 0.55f + new Vector2(r * 0.15f, 0f));
		}
		return pts.ToArray();
	}

	// ══════════════════════ 天际钻头 ══════════════════════

	private sealed class Drill
	{
		public Node2D Root = null!;
		public Node2D Body = null!;
		public ColorRect Quad = null!;
		public Node2D Vortex = null!;
		public float Spin;
		public int Hits;
		public ulong Last;
	}

	private static readonly Dictionary<ulong, Drill> Drills = new();
	private const float DrillLen = 420f, DrillW = 120f;

	private static float DrillHalf(float y) => DrillW / 2f * (1f - y / DrillLen);          // y：从锥底（顶端）往下到锥尖

	/// <summary>出牌：螺旋光锥从天而降，停在敌人头顶。</summary>
	public static void DrillDescend(NCreature target)
	{
		Control? front = Makoto.FxLayer();
		if (front == null)
		{
			return;
		}
		ulong id = target.GetInstanceId();
		if (Drills.TryGetValue(id, out Drill? old) && GodotObject.IsInstanceValid(old.Root))
		{
			return;
		}
		Vector2 tip = target.VfxSpawnPosition + new Vector2(0f, -40f);
		Drill d = new() { Last = Time.GetTicksMsec() };
		d.Root = Fx.Root(front, tip, "P3HeavenlyDrill");
		d.Body = new Node2D();
		d.Root.AddChild(d.Body);
		// 锥体（原点在锥尖，向上伸出）：着色器画的螺旋钻头（金属明暗、螺旋棱、轮廓光、锥尖白热）
		d.Quad = new ColorRect { Size = new Vector2(DrillW * 1.5f, DrillLen), Position = new Vector2(-DrillW * 0.75f, -DrillLen), MouseFilter = Control.MouseFilterEnum.Ignore,
			Material = new ShaderMaterial { Shader = Shaders.Drill } };
		d.Body.AddChild(d.Quad);
		// 锥尖附近两圈压扁的气旋环，反向旋转（转起来的感觉）
		d.Vortex = new Node2D { Position = new Vector2(0f, -DrillLen * 0.28f), Scale = new Vector2(1f, 0.28f) };
		d.Body.AddChild(d.Vortex);
		Fx.Line(d.Vortex, Fx.Circle(70f, 40), new Color(Pal.Ice, 0.55f), 3f, closed: true);
		Fx.Line(d.Vortex, Fx.Circle(98f, 40), new Color(Pal.Cyan, 0.3f), 2f, closed: true);
		d.Vortex.Modulate = new Color(1f, 1f, 1f, 0f);
		Drills[id] = d;
		// 从屏幕上方落下
		float drop = tip.Y + DrillLen + 80f;
		d.Body.Position = new Vector2(0f, -drop);
		Fx.Animate(d.Body, 0.22f, t => d.Body.Position = new Vector2(0f, -drop * (1f - Ease.InCubic(t))));
		Fx.After(d.Root, 0.22, "drill land", () =>
		{
			P3Fx.Flash(front, tip, Pal.Ice, 260f, 0.2f);
			P3Fx.Shockwave(front, tip, Pal.Cyan, 360f, 0.35f);
			Shake(ShakeStrength.Weak);
		});
		Audio.Sfx.Play(Audio.Snd.CutIn, 0.45f, pitch: 1.3f);
		Timer tick = new() { WaitTime = 1.0 / 30.0, Autostart = true };
		d.Root.AddChild(tick);
		tick.Timeout += () => ModEntry.Safe("drill tick", () =>
		{
			float speed = 6f + 3f * Mathf.Min(d.Hits, 6);
			d.Spin += (float)tick.WaitTime * speed;
			if (GodotObject.IsInstanceValid(d.Quad))
			{
				((ShaderMaterial)d.Quad.Material).SetShaderParameter("spin", d.Spin);
				((ShaderMaterial)d.Quad.Material).SetShaderParameter("heat", Mathf.Min(d.Hits / 6f, 1f));
				d.Vortex.Rotation += (float)tick.WaitTime * speed * 0.6f;
				d.Vortex.Modulate = new Color(1f, 1f, 1f, d.Hits > 0 ? Mathf.Min(1f, 0.4f + d.Hits * 0.1f) : 0f);
			}
			// 连击之间留足间隔（命中间隔可能接近 0.5 秒），1.1 秒没有新的命中才算打完，不会中途碎掉再重新落下
			ulong idle = Time.GetTicksMsec() - d.Last;
			if (idle > (d.Hits == 0 ? 2500UL : 1100UL))
			{
				tick.Stop();
				Drills.Remove(id);
				Shatter(front, d);
			}
		});
	}

	/// <summary>每一击：光锥往下钻一下，锥尖迸出火花和一圈冲击。</summary>
	public static void DrillHit(NCreature target)
	{
		Control? front = Makoto.FxLayer();
		if (front == null)
		{
			return;
		}
		ulong id = target.GetInstanceId();
		if (!Drills.TryGetValue(id, out Drill? d) || !GodotObject.IsInstanceValid(d.Root))
		{
			DrillDescend(target);
			if (!Drills.TryGetValue(id, out d))
			{
				return;
			}
		}
		d.Hits++;
		d.Last = Time.GetTicksMsec();
		Vector2 tip = d.Root.GlobalPosition;
		Fx.Animate(d.Root, 0.12f, t => d.Root.Scale = new Vector2(1f - 0.08f * Mathf.Sin(t * Mathf.Pi), 1f + 0.12f * Mathf.Sin(t * Mathf.Pi)));
		Node2D sparks = Fx.Root(front, tip, "P3DrillSparks");
		Fx.Sparks(sparks, Pal.Ice, 22, 260f, 620f, 0.45f, gravity: 900f, scaleMin: 0.12f, scaleMax: 0.3f, ramp: new[] { Pal.White, Pal.Ice, Pal.Cyan });
		Fx.FreeAfter(sparks, 0.6);
		P3Fx.Shockwave(front, tip, Pal.Ice, 240f + 20f * Mathf.Min(d.Hits, 8), 0.25f, squashY: 0.45f);
		P3Fx.Flash(front, tip, Pal.White, 140f, 0.12f);
		EvokerShot.HitStop(0.03);
		Shake(ShakeStrength.Weak);
		Audio.Sfx.Play(Audio.Snd.StarSpend, 0.45f, pitch: 0.8f + 0.04f * Mathf.Min(d.Hits, 10));
	}

	private static void Shatter(Control front, Drill d)
	{
		if (!GodotObject.IsInstanceValid(d.Root))
		{
			return;
		}
		Vector2 at = d.Root.GlobalPosition + new Vector2(0f, -DrillLen * 0.45f);
		P3Fx.GlassShatter(front, at, 34, 1.1f, Pal.Ice);
		P3Fx.Flash(front, at, Pal.White, 320f, 0.25f);
		Audio.Sfx.Play(Audio.Snd.TarotShatter, 0.7f, pitch: 1.1f);
		d.Root.QueueFree();
	}

	// ══════════════════════ 王之凝视 ══════════════════════

	public static void Gaze()
	{
		var enemies = new List<NCreature>();
		if (MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom.Instance is { } room)
		{
			enemies = room.CreatureNodes.Where(n => GodotObject.IsInstanceValid(n) && !n.Entity.IsPlayer && n.Entity.IsAlive && n.Entity.Monster is not MegaCrit.Sts2.Core.Models.Monsters.Osty).ToList();
		}
		Control? front = Makoto.FxLayer();
		Control? back = Makoto.FxLayer(back: true);
		if (front == null || back == null || enemies.Count == 0)
		{
			return;
		}
		float cx = enemies.Average(e => e.VfxSpawnPosition.X);
		Vector2 at = new(cx, 210f);
		Node2D eye = Fx.Root(back, at, "P3MonarchEye");
		const float W = 300f, H = 120f;
		// 眼白（杏仁形）→ 虹膜（青色两圈 + 旋转刻度）→ 竖瞳；上下眼睑是两片深色弧形，张开 = 收缩
		Vector2[] almond = Almond(W, H, 1f);
		Polygon2D white = Fx.Poly(eye, almond, new Color(0.9f, 0.96f, 1f, 0.95f));
		Polygon2D clip = new() { Polygon = almond, ClipChildren = CanvasItem.ClipChildrenMode.Only };
		eye.AddChild(clip);
		Node2D iris = new();
		clip.AddChild(iris);
		Fx.Poly(iris, Fx.Circle(56f, 48), new Color(0.05f, 0.2f, 0.7f));
		Fx.Poly(iris, Fx.Circle(44f, 48), new Color(Pal.Cyan, 0.9f));
		Node2D ticks = new();
		iris.AddChild(ticks);
		for (int i = 0; i < 24; i++)
		{
			Vector2 dir = Vector2.Up.Rotated(Mathf.Tau * i / 24f);
			Fx.Line(ticks, new[] { dir * 30f, dir * (i % 3 == 0 ? 52f : 44f) }, new Color(Pal.White, 0.7f), i % 3 == 0 ? 3f : 1.5f);
		}
		Polygon2D pupil = Fx.Poly(iris, new[] { new Vector2(0f, -40f), new Vector2(9f, 0f), new Vector2(0f, 40f), new Vector2(-9f, 0f) }, new Color(0.01f, 0.03f, 0.12f));
		Polygon2D lidTop = Fx.Poly(clip, new Vector2[3], new Color(0.02f, 0.05f, 0.2f));
		Polygon2D lidBot = Fx.Poly(clip, new Vector2[3], new Color(0.02f, 0.05f, 0.2f));
		Line2D rim = Fx.Line(eye, almond, new Color(Pal.Ice, 0.9f), 3f, closed: true);
		Sprite2D glow = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Scale = new Vector2(14f, 7f), Modulate = new Color(Pal.Cyan, 0f) };
		eye.AddChild(glow);
		eye.MoveChild(glow, 0);
		const float total = 1.9f;
		Fx.Animate(eye, total, t =>
		{
			float time = t * total;
			// 0.1–0.35 睁开；1.5–1.75 闭上
			float open = time < 0.1f ? 0f : time < 1.5f ? Ease.OutBack(Mathf.Clamp((time - 0.1f) / 0.25f, 0f, 1f)) : 1f - Ease.InCubic(Mathf.Clamp((time - 1.5f) / 0.25f, 0f, 1f));
			float gap = H / 2f * Mathf.Clamp(open, 0f, 1.1f);
			Fx.Reshape(lidTop, Lid(W, H, gap, top: true));
			Fx.Reshape(lidBot, Lid(W, H, gap, top: false));
			ticks.Rotation = time * 1.2f;
			// 环视一下再盯住正下方
			iris.Position = new Vector2(time < 0.9f ? 40f * Mathf.Sin(time * 7f) * (1f - time / 0.9f) : 0f, 8f);
			pupil.Scale = new Vector2(time > 0.9f ? Mathf.Lerp(1f, 0.55f, Mathf.Clamp((time - 0.9f) / 0.1f, 0f, 1f)) : 1f, 1f);
			glow.Modulate = new Color(Pal.Cyan, 0.4f * Mathf.Clamp(open, 0f, 1f));
			eye.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(time / 0.1f, 0f, 1f) * (1f - Mathf.Clamp((time - 1.72f) / 0.18f, 0f, 1f)));
			white.Color = new Color(0.9f, 0.96f, 1f, 0.95f);
			rim.DefaultColor = new Color(Pal.Ice, 0.9f);
		});
		// 0.95：瞳孔收紧，凝视光落到每个敌人身上，留下眼形印记
		Fx.After(eye, 0.95, "gaze beams", () =>
		{
			P3Fx.Flash(front, at, Pal.Ice, 260f, 0.2f);
			Audio.Sfx.Play(Audio.Snd.Weak, 0.7f, pitch: 0.7f);
			foreach (NCreature e in enemies)
			{
				if (!GodotObject.IsInstanceValid(e))
				{
					continue;
				}
				Vector2 to = e.VfxSpawnPosition;
				Node2D beam = Fx.Root(back, at, "P3GazeBeam");
				Vector2 d = to - at;
				float len = d.Length();
				beam.Rotation = d.Angle() - Mathf.Pi / 2f;
				Fx.Poly(beam, new[] { new Vector2(-6f, 0f), new Vector2(6f, 0f), new Vector2(60f, len), new Vector2(-60f, len) }, new Color(Pal.Cyan, 0.3f), additive: true);
				Fx.Poly(beam, new[] { new Vector2(-2f, 0f), new Vector2(2f, 0f), new Vector2(14f, len), new Vector2(-14f, len) }, new Color(Pal.White, 0.7f), additive: true);
				Fx.Animate(beam, 0.45f, t => beam.Modulate = new Color(1f, 1f, 1f, Ease.Envelope(t, 0.15f, 0.4f)));
				Fx.FreeAfter(beam, 0.5);
				Fx.After(eye, 0.12, "gaze mark", () => Mark(front, to, 1f));
			}
		});
		Fx.FreeAfter(eye, total + 0.05);
	}

	/// <summary>敌人身上的眼形印记：杏仁形轮廓一闪睁开、瞳孔收紧，然后淡去（攻击时也会闪一下）。</summary>
	public static void Mark(Control layer, Vector2 at, float s)
	{
		Node2D m = Fx.Root(layer, at + new Vector2(0f, -10f), "P3GazeMark");
		m.Scale = Vector2.One * 0.45f * s;
		Line2D outline = Fx.Line(m, Almond(160f, 64f, 1f), new Color(Pal.Ice, 0.95f), 6f, closed: true);
		Polygon2D iris = Fx.Poly(m, Fx.Circle(22f, 24), new Color(Pal.Cyan, 0.9f), additive: true);
		Fx.Poly(m, new[] { new Vector2(0f, -20f), new Vector2(5f, 0f), new Vector2(0f, 20f), new Vector2(-5f, 0f) }, new Color(0.01f, 0.03f, 0.12f));
		Fx.Animate(m, 0.55f, t =>
		{
			float open = Ease.OutBack(Mathf.Clamp(t / 0.25f, 0f, 1f));
			m.Scale = new Vector2(0.45f * s * (1f + 0.15f * t), 0.45f * s * Mathf.Max(0.05f, open));
			m.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(Mathf.Clamp((t - 0.5f) / 0.5f, 0f, 1f)));
		});
		Fx.FreeAfter(m, 0.6);
	}

	/// <summary>杏仁形眼睛轮廓（两条弧）。</summary>
	private static Vector2[] Almond(float w, float h, float k)
	{
		var pts = new List<Vector2>();
		const int n = 18;
		for (int i = 0; i <= n; i++)
		{
			float x = -w / 2f + w * i / n;
			pts.Add(new Vector2(x, -h / 2f * k * (1f - Mathf.Pow(2f * x / w, 2f))));
		}
		for (int i = n - 1; i > 0; i--)
		{
			float x = -w / 2f + w * i / n;
			pts.Add(new Vector2(x, h / 2f * k * (1f - Mathf.Pow(2f * x / w, 2f))));
		}
		return pts.ToArray();
	}

	/// <summary>眼睑：从眼角到眼角的一片，gap = 眼缝半高（0 全闭）。</summary>
	private static Vector2[] Lid(float w, float h, float gap, bool top)
	{
		var pts = new List<Vector2>();
		const int n = 18;
		float sign = top ? -1f : 1f;
		for (int i = 0; i <= n; i++)
		{
			float x = -w / 2f + w * i / n;
			pts.Add(new Vector2(x, sign * (h / 2f + 10f)));
		}
		for (int i = n; i >= 0; i--)
		{
			float x = -w / 2f + w * i / n;
			pts.Add(new Vector2(x, sign * gap * (1f - Mathf.Pow(2f * x / w, 2f))));
		}
		return pts.ToArray();
	}

	// ══════════════════════ 熔炉 ══════════════════════

	/// <summary>
	/// 熔炉：脚下的锻造法阵亮起 → 人物身后升起一道着色器火柱（白热芯、橙色外焰、尖端泛青），热浪扭曲空气、火星上飘 →
	/// 火柱顶端锻出一把烧红的剑，淬火（白汽 + 由橙变青）后化作光点飞进人物身体。
	/// </summary>
	public static void Furnace(NCreature caster)
	{
		Control? back = Makoto.FxLayer(back: true);
		Control? front = Makoto.FxLayer();
		if (back == null || front == null)
		{
			return;
		}
		Vector2 feet = Makoto.Feet(caster);
		Vector2 chest = Makoto.Center(caster);
		Color ember = new(1f, 0.5f, 0.12f);
		// 1) 锻造法阵：压扁的双圈 + 八个刻度，从中心张开、亮起
		Node2D sigil = Fx.Root(back, feet, "P3FurnaceSigil");
		sigil.Scale = new Vector2(1f, 0.28f);
		Fx.Line(sigil, Fx.Circle(170f, 64), new Color(ember, 0.9f), 4f, closed: true);
		Fx.Line(sigil, Fx.Circle(140f, 64), new Color(1f, 0.8f, 0.4f, 0.6f), 2f, closed: true);
		for (int i = 0; i < 8; i++)
		{
			Vector2 dir = Vector2.Right.Rotated(Mathf.Tau * i / 8f);
			Fx.Poly(sigil, new[] { dir * 140f + dir.Orthogonal() * 8f, dir * 186f, dir * 140f - dir.Orthogonal() * 8f }, new Color(ember, 0.9f), additive: true);
		}
		Fx.Animate(sigil, 1.5f, t =>
		{
			float time = t * 1.5f;
			float k = Ease.OutBack(Mathf.Clamp(time / 0.2f, 0f, 1f));
			sigil.Scale = new Vector2(k, 0.28f * k);
			sigil.Rotation = time * 0.6f;
			sigil.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(Mathf.Clamp((time - 1.1f) / 0.4f, 0f, 1f)));
		});
		Fx.FreeAfter(sigil, 1.55);

		// 2) 火柱（人物身后）：底边对齐脚下
		Node2D fireRoot = Fx.Root(back, feet, "P3FurnaceFire");
		ColorRect fire = new() { Size = new Vector2(440f, 620f), Position = new Vector2(-220f, -600f), MouseFilter = Control.MouseFilterEnum.Ignore, Material = new ShaderMaterial { Shader = Shaders.ForgeFire } };
		fireRoot.AddChild(fire);
		Fx.SetParam(fire, "height", 0f);
		Fx.Animate(fireRoot, 1.45f, t =>
		{
			float time = t * 1.45f;
			float h = time < 0.18f ? 0f : time < 1.05f ? Ease.OutCubic(Mathf.Clamp((time - 0.18f) / 0.25f, 0f, 1f)) : 1f - Ease.InCubic(Mathf.Clamp((time - 1.05f) / 0.4f, 0f, 1f));
			Fx.SetParam(fire, "height", 0.15f + 0.85f * h);
			Fx.SetParam(fire, "alpha", Mathf.Clamp(h * 1.5f, 0f, 1f));
		});
		Fx.FreeAfter(fireRoot, 1.5);
		Fx.After(fireRoot, 0.18, "furnace ignite", () =>
		{
			P3Fx.Flash(back, feet + new Vector2(0f, -40f), ember, 360f, 0.3f);
			P3Fx.Ripple(back, feet + new Vector2(0f, -200f), 600f, 1.0f, 0.03f);
			Node2D sparks = Fx.Root(front, feet + new Vector2(0f, -20f), "P3FurnaceEmbers");
			GpuParticles2D e = Fx.Sparks(sparks, ember, 60, 60f, 220f, 1.1f, gravity: -380f, scaleMin: 0.1f, scaleMax: 0.3f,
				ramp: new[] { new Color(1f, 0.95f, 0.7f), ember, new Color(0.6f, 0.1f, 0.05f) }, oneShot: false);
			ParticleProcessMaterial pm = (ParticleProcessMaterial)e.ProcessMaterial;
			pm.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
			pm.EmissionBoxExtents = new Vector3(150f, 10f, 0f);
			Fx.After(sparks, 0.8, "embers off", () => e.Emitting = false);
			Fx.FreeAfter(sparks, 2.0);
			MegaCrit.Sts2.Core.Nodes.NGame.Instance?.ScreenShake(ShakeStrength.Weak, ShakeDuration.Short);
		});

		// 3) 火柱顶端锻出一把剑：烧红 → 淬火（白汽、由橙变青）→ 化作光点飞进胸口
		Vector2 bladeAt = feet + new Vector2(0f, -560f);
		Node2D blade = Fx.Root(front, bladeAt, "P3FurnaceBlade");
		blade.Rotation = Mathf.Pi / 2f;                      // 剑尖朝下
		blade.Visible = false;
		Polygon2D glowB = Fx.Poly(blade, Blade(230f, 34f), new Color(ember, 0.5f), additive: true);
		Polygon2D steel = Fx.Poly(blade, Blade(200f, 20f), new Color(1f, 0.55f, 0.15f));
		Polygon2D edge = Fx.Poly(blade, Blade(196f, 6f), new Color(1f, 0.95f, 0.75f), additive: true);
		Fx.Poly(blade, RectPts(new Rect2(-48f, -26f, 12f, 52f)), new Color(0.75f, 0.78f, 0.9f));
		Fx.After(blade, 0.45, "blade forge", () =>
		{
			blade.Visible = true;
			P3Fx.Flash(front, bladeAt, ember, 200f, 0.2f);
			Audio.Sfx.Play(Audio.Snd.StarSpend, 0.6f, pitch: 0.7f);
		});
		Fx.Animate(blade, 1.4f, t =>
		{
			float time = t * 1.4f;
			float form = Ease.OutBack(Mathf.Clamp((time - 0.45f) / 0.18f, 0f, 1f));
			blade.Scale = new Vector2(form, 1f) * 0.9f;
			blade.Position = bladeAt + new Vector2(0f, 6f * Mathf.Sin(time * 5f));
			// 0.85 起淬火：由橙变青
			float q = Mathf.Clamp((time - 0.85f) / 0.2f, 0f, 1f);
			steel.Color = new Color(1f, 0.55f, 0.15f).Lerp(new Color(0.55f, 0.75f, 0.95f), q);
			glowB.Color = new Color(ember, 0.5f).Lerp(new Color(Pal.Cyan, 0.5f), q);
			edge.Color = new Color(1f, 0.95f, 0.75f).Lerp(Pal.White, q);
		});
		Fx.After(blade, 0.85, "blade quench", () =>
		{
			Node2D steam = Fx.Root(front, bladeAt, "P3FurnaceSteam");
			Fx.Sparks(steam, Pal.White, 30, 80f, 260f, 0.7f, gravity: -200f, scaleMin: 0.3f, scaleMax: 0.8f, ramp: new[] { new Color(1f, 1f, 1f, 0.8f), new Color(0.8f, 0.9f, 1f, 0f) });
			Fx.FreeAfter(steam, 0.9);
			P3Fx.Shockwave(front, bladeAt, Pal.Ice, 300f, 0.35f);
			Audio.Sfx.Play(Audio.Snd.Weak, 0.5f, pitch: 0.9f);
		});
		Fx.After(blade, 1.12, "blade absorb", () =>
		{
			Vector2 from = blade.GlobalPosition;
			Fx.Animate(blade, 0.22f, t =>
			{
				float e = Ease.InCubic(t);
				blade.GlobalPosition = from.Lerp(chest, e);
				blade.Scale = Vector2.One * 0.9f * (1f - 0.7f * e);
			});
			Fx.After(blade, 0.22, "blade in", () =>
			{
				P3Fx.Flash(front, chest, Pal.Cyan, 220f, 0.25f);
				P3Fx.Shockwave(front, chest, Pal.Cyan, 280f, 0.3f);
				blade.QueueFree();
			});
		});
	}

	/// <summary>剑形：剑尖朝 +X，从护手（原点）到剑尖 len。</summary>
	private static Vector2[] Blade(float len, float w) => new[]
	{
		new Vector2(-40f, -w * 0.35f), new Vector2(len * 0.82f, -w * 0.5f), new Vector2(len, 0f), new Vector2(len * 0.82f, w * 0.5f), new Vector2(-40f, w * 0.35f)
	};

	// ══════════════════════ 创世纪 ══════════════════════

	/// <summary>
	/// 创世纪（召唤 Messiah 之后）：头顶上方的夜空里一点光炸开，长成一个旋转的星系（着色器），星系越转越快，
	/// 旋臂上甩出的星星划着弧线落进人物身边（辉星）；最后星系收缩成一颗星，闪一下消失。
	/// </summary>
	public static void Genesis(NCreature caster, float s = 1f, float duration = 1.9f)
	{
		Control? back = Makoto.FxLayer(back: true);
		Control? front = Makoto.FxLayer();
		if (back == null || front == null)
		{
			return;
		}
		Vector2 at = Makoto.Head(caster) + new Vector2(-40f, -190f * s);
		at.Y = Mathf.Max(at.Y, 170f);
		Vector2 chest = Makoto.Center(caster) + new Vector2(0f, -20f);
		Node2D root = Fx.Root(back, at, "P3Genesis");
		root.Scale = Vector2.One * s;
		ColorRect gal = Fx.ShaderQuad(root, Shaders.Galaxy, 620f);
		P3Fx.Flash(back, at, Pal.White, 140f * s, 0.2f);
		float spin = 0f;
		float last = 0f;
		Fx.Animate(root, duration, t =>
		{
			float time = t * duration;
			float dt = time - last;
			last = time;
			float speed = 0.8f + 3.5f * Ease.InCubic(t);
			spin += speed * dt;
			float birth = Ease.OutCubic(Mathf.Clamp(time / 0.5f, 0f, 1f));
			float collapse = Ease.InCubic(Mathf.Clamp((time - (duration - 0.35f)) / 0.3f, 0f, 1f));
			Fx.SetParam(gal, "spin", spin);
			Fx.SetParam(gal, "birth", birth * (1f - collapse) + 0.02f);
			Fx.SetParam(gal, "alpha", 1f - collapse * 0.5f);
		});
		// 0.5 起：旋臂上甩出星星，弧线落到人物身边
		for (int i = 0; i < 7; i++)
		{
			float delay = 0.5f + 0.13f * i;
			Fx.After(root, delay, "genesis star", () =>
			{
				float a = Fx.Rand(0f, Mathf.Tau);
				Vector2 from = at + new Vector2(Mathf.Cos(a) * 150f, Mathf.Sin(a) * 70f) * s;
				Vector2 to = chest + new Vector2(Fx.Rand(-130f, 130f), Fx.Rand(-40f, 30f));
				Vector2 ctrl = from + new Vector2(Mathf.Cos(a) * 160f, -80f);
				Node2D star = Fx.Root(front, from, "P3GenesisStar");
				Fx.Poly(star, Fx.Star4(16f, 0.25f), Pal.White, additive: true);
				Sprite2D g = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Scale = Vector2.One * 1.6f, Modulate = new Color(Pal.Cyan, 0.8f) };
				star.AddChild(g);
				Line2D tail = new() { Width = 5f, Material = Fx.Additive, Gradient = new Gradient { Colors = new[] { new Color(Pal.Cyan, 0f), new Color(Pal.Ice, 0.9f) } } };
				Node2D tr = Fx.Root(front, Vector2.Zero, "P3GenesisTail");
				tr.TopLevel = true;
				tr.GlobalPosition = Vector2.Zero;
				tr.AddChild(tail);
				Fx.Animate(star, 0.4f, t =>
				{
					float e = Ease.InOutCubic(t);
					Vector2 p = Bezier(from, ctrl, to, e);
					star.GlobalPosition = p;
					star.Rotation = t * 6f;
					tail.Points = new[] { Bezier(from, ctrl, to, Mathf.Max(0f, e - 0.25f)), p };
				});
				Fx.After(star, 0.4, "genesis star land", () =>
				{
					P3Fx.Flash(front, to, Pal.Ice, 90f, 0.15f);
					star.QueueFree();
					Fx.Animate(tail, 0.12f, t => tail.Modulate = new Color(1f, 1f, 1f, 1f - t));
					Fx.FreeAfter(tr, 0.14);
				});
				Audio.Sfx.Play(Audio.Snd.StarGain, 0.35f, pitch: 1f + 0.05f * i);
			});
		}
		Fx.After(root, duration - 0.05f, "genesis end", () =>
		{
			P3Fx.Flash(front, at, Pal.White, 260f * s, 0.25f);
			P3Fx.Shockwave(front, at, Pal.Ice, 520f * s, 0.4f, squashY: 0.45f);
		});
		Fx.FreeAfter(root, duration + 0.05f);
	}

	private static Vector2 Bezier(Vector2 a, Vector2 c, Vector2 b, float t)
	{
		float k = 1f - t;
		return a * (k * k) + c * (2f * k * t) + b * (t * t);
	}

	// ══════════════════════ 暴政 ══════════════════════

	private static Texture2D? _coffin;
	private static Texture2D? Coffin => _coffin ??= ResourceLoader.Exists("res://p3makoto/fx/coffin.png") ? ResourceLoader.Load<Texture2D>("res://p3makoto/fx/coffin.png") : null;

	/// <summary>棺材的位置：人物左后方空地（和人物不重叠）；天鹅绒门在场时改到右侧。</summary>
	private static Vector2 CoffinSpot(NCreature caster) =>
		Makoto.Feet(caster) + new Vector2(ThemeFx.GateRoot != null ? 250f : -270f, 0f);

	private static Texture2D? _tartarus;
	private static Texture2D? Tartarus => _tartarus ??= ResourceLoader.Exists("res://p3makoto/fx/tartarus.png") ? ResourceLoader.Load<Texture2D>("res://p3makoto/fx/tartarus.png") : null;

	/// <summary>
	/// 暴政（皇帝 IV · 影时间降临）：
	/// 0.0 午夜 —— 人物身后的世界沉进影时间的墨绿色天色，远处升起一轮黄绿色的大月亮；
	/// 0.25 塔尔塔罗斯从远处的地平线下拔地而起（震屏、尘浪），塔尖正好顶在月亮前面；
	/// 0.55 人物左后方一排棺材由近到远依次"显形"（棺材本身不动：绿光扫描线自下而上扫出绿光剪影，再凝成实体；影时间里普通人都变成了棺材），
	///      最前面那口棺材上盖下皇帝的 IV 印章；棺材脚下漫开暗红色的血洼；
	/// 2.0 棺材泛起绿光、从上往下化成光点消散；2.25 塔沉回地平线下，天色和月亮退去。
	/// 全部画在人物身后（back 层的最底下，天色之上），不挡人物；虚空形态的影时间已经在场时，不再重复画天色和月亮。
	/// 塔的贴图（p3makoto/fx/tartarus.png）缺失时跳过塔，其余照常。
	/// </summary>
	public static void Tyranny(NCreature caster)
	{
		Control? back = Makoto.FxLayer(back: true);
		Control? front = Makoto.FxLayer();
		if (back == null || front == null)
		{
			return;
		}
		Vector2 feet = Makoto.Feet(caster);
		Vector2 size = front.GetViewportRect().Size;
		bool formSky = FormFx.SkyIndex >= 0;
		const float total = 2.85f;

		Node2D root = Fx.Root(back, Vector2.Zero, "P3Tyranny");
		root.TopLevel = true;
		root.GlobalPosition = Vector2.Zero;
		back.MoveChild(root, formSky ? FormFx.SkyIndex + 1 : 0);

		// 地平线（远处的地面）与塔的位置：人物和敌人之间偏人物一侧的远景
		float horizon = feet.Y - 70f;
		float towerX = Mathf.Clamp(feet.X + size.X * 0.2f, size.X * 0.3f, size.X * 0.6f);
		float towerH = size.Y * 0.72f;
		Vector2 moonAt = new(towerX + size.X * 0.06f, horizon - towerH * 0.82f);

		// 1) 天色 + 月亮
		ColorRect? sky = null;
		Node2D? moon = null;
		if (!formSky)
		{
			sky = new() { Material = new ShaderMaterial { Shader = Shaders.DarkSky }, Size = size * 3f, Position = -size, MouseFilter = Control.MouseFilterEnum.Ignore };
			root.AddChild(sky);
			((ShaderMaterial)sky.Material).SetShaderParameter("moon", moonAt / size);
			Fx.SetParam(sky, "alpha", 0f);
			moon = new() { Position = moonAt };
			root.AddChild(moon);
			ColorRect halo = Fx.ShaderQuad(moon, Shaders.Flash, 760f);
			Fx.SetParam(halo, "tint", new Color(Pal.DarkHour, 0.45f));
			Fx.SetParam(halo, "alpha", 0.45f);
			Fx.ShaderQuad(moon, Shaders.DarkMoon, 440f);
			moon.Modulate = new Color(1f, 1f, 1f, 0f);
		}

		// 2) 塔尔塔罗斯：地平线以下裁掉，从下往上升起
		Node2D? towerBody = null;
		Sprite2D? towerRim = null;
		if (Tartarus is Texture2D tt)
		{
			float sc = towerH / tt.GetHeight();
			Polygon2D clip = new() { Polygon = RectPts(new Rect2(towerX - size.X, horizon - size.Y * 2f, size.X * 2f, size.Y * 2f)), ClipChildren = CanvasItem.ClipChildrenMode.Only };
			root.AddChild(clip);
			towerBody = new() { Position = new Vector2(towerX, horizon + towerH) };
			clip.AddChild(towerBody);
			// 本体压成影时间的暗绿剪影，外面一层加色的绿光勾边（呼吸）
			towerRim = new() { Texture = tt, Centered = false, Position = new Vector2(-tt.GetWidth() * sc * 0.5f - 4f, -towerH - 6f), Scale = Vector2.One * sc * 1.01f, Material = Fx.Additive, Modulate = new Color(Pal.DarkHour, 0f) };
			towerBody.AddChild(towerRim);
			towerBody.AddChild(new Sprite2D { Texture = tt, Centered = false, Position = new Vector2(-tt.GetWidth() * sc * 0.5f, -towerH), Scale = Vector2.One * sc, Modulate = new Color(0.5f, 0.75f, 0.6f) });
			Fx.After(root, 0.25, "tartarus rise", () =>
			{
				Shake(ShakeStrength.Weak);
				P3Fx.Shockwave(back, new Vector2(towerX, horizon), Pal.DarkHour, 700f, 0.6f, squashY: 0.22f);
				P3Fx.Ripple(back, new Vector2(towerX, horizon), 800f, 0.9f, 0.025f, squashY: 0.3f);
				Node2D dust = Fx.Root(back, new Vector2(towerX, horizon), "P3TartarusDust");
				GpuParticles2D e = Fx.Sparks(dust, Pal.DarkHour, 50, 40f, 160f, 1.0f, gravity: -40f, scaleMin: 0.3f, scaleMax: 0.9f,
					ramp: new[] { new Color(Pal.DarkHour, 0.5f), new Color(0.1f, 0.3f, 0.15f, 0f) }, oneShot: false);
				((ParticleProcessMaterial)e.ProcessMaterial).EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
				((ParticleProcessMaterial)e.ProcessMaterial).EmissionBoxExtents = new Vector3(tt.GetWidth() * sc * 0.6f, 6f, 0f);
				Fx.After(dust, 0.7, "dust off", () => e.Emitting = false);
				Fx.FreeAfter(dust, 1.8);
				Audio.Sfx.Play(Audio.Snd.ThanatosSlash, 0.45f, pitch: 0.6f);
			});
		}

		// 3) 血洼（在棺材下面）
		(Vector2 off, float s)[] row = { (new(-215f, -10f), 0.42f), (new(-335f, -40f), 0.36f), (new(-440f, -62f), 0.31f), (new(-530f, -80f), 0.27f) };
		var pools = new List<(Polygon2D poly, float r)>();
		foreach (var (off, s) in row)
		{
			Polygon2D pool = Fx.Poly(root, Fx.Circle(1f, 24), new Color(0.32f, 0.01f, 0.05f, 0f));
			pool.Position = feet + off + new Vector2(Fx.Rand(-20f, 20f), 4f);
			pools.Add((pool, 150f * s));
		}

		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			float skyIn = Ease.OutCubic(Mathf.Clamp(time / 0.35f, 0f, 1f)), skyOut = Ease.InCubic(Mathf.Clamp((time - 2.35f) / 0.45f, 0f, 1f));
			if (sky != null)
			{
				Fx.SetParam(sky, "alpha", skyIn * (1f - skyOut));
			}
			if (moon != null)
			{
				float m = Ease.OutCubic(Mathf.Clamp((time - 0.05f) / 0.45f, 0f, 1f));
				moon.Modulate = new Color(1f, 1f, 1f, m * (1f - skyOut));
				moon.Scale = Vector2.One * (0.85f + 0.15f * m);
			}
			if (towerBody != null && towerRim != null)
			{
				float rise = Ease.OutCubic(Mathf.Clamp((time - 0.25f) / 0.75f, 0f, 1f));
				float sink = Ease.InCubic(Mathf.Clamp((time - 2.25f) / 0.5f, 0f, 1f));
				towerBody.Position = new Vector2(towerX + (rise < 1f ? Mathf.Sin(time * 70f) * 3f * (1f - rise) : 0f), horizon + towerH * (1f - rise + sink));
				towerRim.Modulate = new Color(Pal.DarkHour, (0.35f + 0.15f * Mathf.Sin(time * 5f)) * rise * (1f - sink));
			}
			for (int i = 0; i < pools.Count; i++)
			{
				float g = Ease.OutCubic(Mathf.Clamp((time - 0.6f - 0.1f * i) / 0.5f, 0f, 1f));
				pools[i].poly.Scale = new Vector2(pools[i].r * g, pools[i].r * 0.22f * g);
				pools[i].poly.Color = new Color(0.32f, 0.01f, 0.05f, 0.6f * g * (1f - skyOut));
			}
		});

		// 4) 棺材：由近到远依次立起，几乎同时沉回；最前面那口盖皇帝 IV 印章
		for (int i = 0; i < row.Length; i++)
		{
			var (off, s) = row[i];
			int k = i;
			Fx.After(root, 0.55 + 0.1 * i, "tyranny coffin", () =>
			{
				RiseCoffin(back, feet + off, s, 1.07f - 0.1f * k, numeral: k == 0);   // 四口棺材同时（2.0s）开始消散
				Audio.Sfx.Play(Audio.Snd.StarSpend, 0.4f, pitch: 0.5f + 0.05f * k);
			});
		}
		Fx.FreeAfter(root, total + 0.05f);
	}

	/// <summary>暴政生效（回合开始消耗一张牌）：同一位置升起一口小棺材，一张牌的虚影从手牌区落进去，绿光一闪，沉下。</summary>
	public static void TyrannyExhaust(NCreature caster)
	{
		Control? back = Makoto.FxLayer(back: true);
		Control? front = Makoto.FxLayer();
		if (back == null || front == null)
		{
			return;
		}
		Vector2 at = CoffinSpot(caster);
		RiseCoffin(back, at, 0.6f, 1.1f, numeral: false);
		Vector2 size = front.GetViewportRect().Size;
		Vector2 from = new(size.X * 0.5f, size.Y - 120f);
		Vector2 to = at + new Vector2(0f, -130f);
		Node2D ghost = Fx.Root(front, from, "P3TyrannyGhost");
		Fx.Poly(ghost, RectPts(new Rect2(-26f, -38f, 52f, 76f)), new Color(Pal.DarkHour, 0.35f), additive: true);
		Fx.Line(ghost, RectPts(new Rect2(-26f, -38f, 52f, 76f)), new Color(Pal.DarkHour, 0.9f), 2f, closed: true);
		Fx.Animate(ghost, 0.45f, t =>
		{
			float e = Ease.InOutCubic(Mathf.Clamp((t - 0.25f) / 0.75f, 0f, 1f));
			ghost.GlobalPosition = Bezier(from, (from + to) / 2f + new Vector2(0f, -200f), to, e);
			ghost.Rotation = e * 2.5f;
			ghost.Scale = Vector2.One * (1f - 0.6f * e);
			ghost.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(t * 5f, 0f, 1f));
		});
		Fx.After(ghost, 0.45, "ghost in", () =>
		{
			ghost.QueueFree();
			P3Fx.Flash(front, to, Pal.DarkHour, 160f, 0.2f);
			Audio.Sfx.Play(Audio.Snd.StarSpend, 0.5f, pitch: 0.6f);
		});
	}

	/// <summary>
	/// 影时间的棺材：棺材本身一动不动，只做"显形 / 消散"——
	/// 显形：一道绿色光线从地面往上扫，扫过的地方先是一整块发亮的绿光剪影，随后光退去、凝成实体（像人在影时间里"变成"棺材）；
	/// 停留时棺身有极淡的绿光呼吸；
	/// 消散：先整体泛起绿光，再从上往下被光线"擦掉"，擦过的地方化成绿色光点往上飘。
	/// </summary>
	private static void RiseCoffin(Control layer, Vector2 floor, float s, float hold, bool numeral)
	{
		Texture2D? tex = Coffin;
		if (tex == null)
		{
			return;
		}
		float h = tex.GetHeight(), w = tex.GetWidth();
		const float tIn = 0.38f, tOut = 0.5f;
		float total = tIn + hold + tOut;
		Node2D root = Fx.Root(layer, floor, "P3Coffin");
		root.Scale = Vector2.One * s;
		// 地面阴影
		Polygon2D shadow = Fx.Poly(root, Fx.Circle(w * 0.62f, 24, 0.16f), new Color(0f, 0.03f, 0.01f, 0f));
		Polygon2D clip = new() { Polygon = RectPts(new Rect2(-w, 0f, w * 2f, 12f)), ClipChildren = CanvasItem.ClipChildrenMode.Only };
		root.AddChild(clip);
		Sprite2D spr = new() { Texture = tex, Centered = false, Position = new Vector2(-w / 2f, -h) };
		clip.AddChild(spr);
		Sprite2D shine = new() { Texture = tex, Centered = false, Position = new Vector2(-w / 2f, -h), Material = Fx.Additive, Modulate = new Color(Pal.DarkHour, 0f) };
		clip.AddChild(shine);
		// 扫描光线（在裁切外面，贴着显形 / 消散的边）
		Node2D scan = new();
		root.AddChild(scan);
		Fx.Poly(scan, RectPts(new Rect2(-w * 0.62f, -3f, w * 1.24f, 6f)), new Color(Pal.Ice, 0.95f), additive: true);
		scan.AddChild(new Sprite2D { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(Pal.DarkHour, 0.8f), Scale = new Vector2(w / 40f, 0.9f) });
		scan.Modulate = new Color(1f, 1f, 1f, 0f);
		Label? iv = null;
		if (numeral)
		{
			iv = Typo.Text(spr, "IV", Typo.Latin, 64, new Color(0.75f, 1f, 0.82f), new Vector2(w / 2f - 30f, h * 0.38f), outline: 4, outlineColor: new Color(0.02f, 0.08f, 0.05f));
			iv.PivotOffset = iv.Size / 2f;
			iv.Modulate = new Color(1f, 1f, 1f, 0f);
			Fx.After(root, tIn + 0.12, "iv stamp", () =>
			{
				Vector2 at = floor + new Vector2(0f, (-h * 0.62f + 32f) * s);
				P3Fx.Flash(layer, at, Pal.DarkHour, 160f * s, 0.2f);
				P3Fx.Shockwave(layer, at, Pal.DarkHour, 260f * s, 0.3f);
				Audio.Sfx.Play(Audio.Snd.StarSpend, 0.6f, pitch: 0.55f);
			});
		}
		GpuParticles2D? motes = null;
		Node2D moteAt = new();                                     // 跟着擦除线走，但不受扫描线淡出的影响
		root.AddChild(moteAt);
		Fx.After(root, tIn + hold + 0.12, "coffin motes", () =>
		{
			motes = Fx.Sparks(moteAt, Pal.DarkHour, 34, 30f, 90f, 0.9f, gravity: -120f, scaleMin: 0.2f, scaleMax: 0.55f,
				ramp: new[] { new Color(Pal.Ice, 0.9f), new Color(Pal.DarkHour, 0.6f), new Color(Pal.DarkHour, 0f) }, oneShot: false);
			((ParticleProcessMaterial)motes.ProcessMaterial).EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
			((ParticleProcessMaterial)motes.ProcessMaterial).EmissionBoxExtents = new Vector3(w * 0.45f, 4f, 0f);
		});
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			float inT = Mathf.Clamp(time / tIn, 0f, 1f);
			float outT = Mathf.Clamp((time - tIn - hold) / tOut, 0f, 1f);
			float reveal = Ease.OutCubic(inT);
			// 消散：前 25% 只泛光，之后从上往下擦掉
			float wipe = Ease.InOutCubic(Mathf.Clamp((outT - 0.25f) / 0.75f, 0f, 1f));
			float top = outT > 0f ? -h * (1f - wipe) : -h * reveal - 2f;
			clip.Polygon = RectPts(new Rect2(-w, top, w * 2f, -top + 12f));
			bool scanning = (inT > 0f && inT < 1f) || (outT > 0.25f && outT < 1f);
			scan.Position = new Vector2(0f, top);
			moteAt.Position = scan.Position;
			scan.Modulate = new Color(1f, 1f, 1f, scanning ? 1f : outT >= 1f ? 0f : Mathf.Max(0f, scan.Modulate.A - 0.15f));
			// 绿光：显形时满亮，0.15s 后凝成实体；停留时淡淡呼吸；消散前重新泛起
			float solid = Ease.OutCubic(Mathf.Clamp((time - tIn * 0.6f) / 0.3f, 0f, 1f));
			float glow = (1f - solid) * 0.95f + solid * (0.08f + 0.05f * Mathf.Sin(time * 4f)) + Ease.OutCubic(Mathf.Clamp(outT / 0.25f, 0f, 1f)) * 0.7f;
			shine.Modulate = new Color(Pal.DarkHour, glow);
			spr.Modulate = new Color(1f, 1f, 1f, 0.25f + 0.75f * solid);
			shadow.Color = new Color(0f, 0.03f, 0.01f, 0.45f * reveal * (1f - wipe));
			if (iv != null)
			{
				// 印章式落下：从两倍大砸到原大小
				float stamp = Mathf.Clamp((time - tIn) / 0.12f, 0f, 1f);
				iv.Scale = Vector2.One * Mathf.Lerp(2.2f, 1f, Ease.InCubic(stamp));
				iv.Modulate = new Color(1f, 1f, 1f, stamp * (1f - Mathf.Clamp(outT * 3f, 0f, 1f)));
			}
			if (outT >= 1f && motes != null)
			{
				motes.Emitting = false;
			}
		});
		Fx.After(root, 0.02, "coffin ground", () => P3Fx.Ripple(layer, floor, 360f * s, 0.7f, 0.015f, squashY: 0.3f));
		Fx.FreeAfter(root, total + 0.9f);
	}

}
