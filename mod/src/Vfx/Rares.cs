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

	/// <summary>
	/// 暴政（皇帝 IV · 奥丁的"雷神之怒"Thunder Reign）：
	/// 0.0 影时间降临 —— 人物身后的世界压成墨绿色，脚下张开绿色法阵（压扁透视）；
	/// 0.15 一张巨大的皇帝塔罗（IV）在人物身后翻面立起，牌框流过电光；
	/// 0.45 / 0.6 两道落雷劈在人物左右两侧地面（地面冲击、电火花、震屏）；
	/// 0.9 第三道主雷正中劈进皇帝牌 —— 白闪、牌碎成绿玻璃、脚下法阵炸开一圈冲击、强震屏；
	/// 0.95~1.7 余电在人物身边游走，绿色火星向上飘，影时间慢慢退去。
	/// 雷、牌、法阵都在人物身后（back 层），只有极短的白闪和细小的余电在人物前面，不挡人物。
	/// </summary>
	public static void Tyranny(NCreature caster)
	{
		Control? back = Makoto.FxLayer(back: true);
		Control? front = Makoto.FxLayer();
		if (back == null || front == null)
		{
			return;
		}
		Vector2 feet = Makoto.Feet(caster), center = Makoto.Center(caster), head = Makoto.Head(caster);
		Vector2 size = front.GetViewportRect().Size;
		Color green = Pal.DarkHour;
		const float total = 1.9f;

		// 1) 影时间：人物身后的整个画面压暗成墨绿，落雷那几帧被照亮
		Node2D sky = Fx.Root(back, Vector2.Zero, "P3TyrannySky");
		sky.TopLevel = true;
		sky.GlobalPosition = Vector2.Zero;
		ColorRect dim = new() { Size = size, Color = new Color(0.01f, 0.05f, 0.03f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		sky.AddChild(dim);
		float[] strikes = { 0.45f, 0.6f, 0.9f };

		// 2) 脚下法阵
		Node2D seal = Fx.Root(back, feet, "P3TyrannySeal");
		ColorRect circle = Fx.ShaderQuad(seal, Shaders.MagicCircle, 620f, 0.3f);
		Fx.SetParam(circle, "tint", green);
		Fx.SetParam(circle, "spin", 0.9f);
		ColorRect circle2 = Fx.ShaderQuad(seal, Shaders.MagicCircle, 380f, 0.3f);
		Fx.SetParam(circle2, "tint", Pal.Ice);
		Fx.SetParam(circle2, "spin", -1.4f);

		// 3) 皇帝塔罗：人物身后、头顶上方立起
		Vector2 cardAt = head + new Vector2(-10f, -150f);
		Node2D cardRoot = Fx.Root(back, cardAt, "P3TyrannyCard");
		Node2D card = new();
		cardRoot.AddChild(card);
		const float cw = 120f, ch = 200f;
		Vector2[] rect = { new(-cw, -ch), new(cw, -ch), new(cw, ch), new(-cw, ch) };
		Vector2[] inner = { new(-cw + 14f, -ch + 14f), new(cw - 14f, -ch + 14f), new(cw - 14f, ch - 14f), new(-cw + 14f, ch - 14f) };
		card.AddChild(new Sprite2D { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(green, 0.5f), Scale = new Vector2(11f, 16f) });
		Fx.Poly(card, rect, new Color(0.01f, 0.07f, 0.05f, 0.97f));
		Fx.StrokeClosed(card, rect, green, 6f, additive: false);
		Fx.StrokeClosed(card, inner, new Color(Pal.Ice, 0.75f), 2f, additive: false);
		// 牌面：同心圆 + 放射线 + 巨大的 IV
		RingQuad(card, 86f, 2.5f, new Color(green, 0.8f));
		RingQuad(card, 70f, 1.5f, new Color(Pal.Ice, 0.5f));
		for (int i = 0; i < 16; i++)
		{
			Vector2 dir = Vector2.Right.Rotated(i * Mathf.Tau / 16f);
			float r0 = i % 2 == 0 ? 92f : 96f, r1 = i % 2 == 0 ? 112f : 104f;
			Fx.Stroke(card, new[] { dir * r0, dir * r1 }, new Color(green, 0.75f), i % 2 == 0 ? 3f : 1.5f);
		}
		Label iv = Typo.Text(card, "IV", Typo.Latin, 104, new Color(0.85f, 1f, 0.9f), new Vector2(-62f, -70f), outline: 5, outlineColor: new Color(0.02f, 0.1f, 0.05f));
		Typo.Text(card, "THE EMPEROR", Typo.Latin, 24, Pal.White, new Vector2(-cw + 22f, ch - 52f), outline: 2, outlineColor: new Color(0.02f, 0.1f, 0.05f));
		Typo.Text(card, "IV", Typo.Latin, 30, green, new Vector2(-cw + 20f, -ch + 18f));
		// 牌框上流动的电光（沿边框跑的一小段亮线）
		Polygon2D runner = Fx.Poly(card, new Vector2[3], Pal.White, additive: true);
		Audio.Sfx.Play(Audio.Snd.TarotReveal, 0.85f, pitch: 0.8f);

		// 4) 动画主轴
		Fx.Animate(sky, total, t =>
		{
			float time = t * total;
			float lit = 0f;
			foreach (float st in strikes)
			{
				lit = Mathf.Max(lit, Mathf.Clamp(1f - Mathf.Abs(time - st - 0.03f) / 0.07f, 0f, 1f));
			}
			float shade = Ease.OutCubic(Mathf.Clamp(time / 0.2f, 0f, 1f)) * (1f - Ease.InCubic(Mathf.Clamp((time - 1.4f) / 0.5f, 0f, 1f)));
			dim.Color = new Color(0.01f, 0.05f, 0.03f, 0.55f * shade).Lerp(new Color(0.75f, 1f, 0.85f, 0.35f * shade), lit);

			float sealIn = Ease.OutExpo(Mathf.Clamp(time / 0.25f, 0f, 1f));
			float sealOut = Ease.InCubic(Mathf.Clamp((time - 1.35f) / 0.45f, 0f, 1f));
			seal.Scale = Vector2.One * (0.4f + 0.6f * sealIn + 0.25f * Ease.OutExpo(Mathf.Clamp((time - 0.9f) / 0.3f, 0f, 1f)));
			Fx.SetParam(circle, "alpha", sealIn * (1f - sealOut) * (0.85f + 0.6f * lit));
			Fx.SetParam(circle2, "alpha", Ease.OutExpo(Mathf.Clamp((time - 0.1f) / 0.25f, 0f, 1f)) * (1f - sealOut) * 0.7f);

			if (IsInstanceValid(card))
			{
				float flip = Mathf.Clamp((time - 0.12f) / 0.28f, 0f, 1f);
				card.Scale = new Vector2(Mathf.Abs(Mathf.Cos((1f - Ease.OutCubic(flip)) * Mathf.Pi * 1.5f)), 0.85f + 0.15f * Ease.OutBack(flip));
				card.Position = new Vector2(0f, 60f * (1f - Ease.OutExpo(flip)) + 4f * Mathf.Sin(time * 9f) * Mathf.Clamp((time - 0.45f) * 4f, 0f, 1f));
				card.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(flip * 4f, 0f, 1f));
				iv.Modulate = new Color(1f + lit, 1f + lit, 1f + lit, 1f);
				// 电光沿边框跑：周长参数化
				float per = 4f * (cw + ch);
				float u0 = Mathf.PosMod(time * 2600f, per);
				Vector2 P(float u)
				{
					u = Mathf.PosMod(u, per);
					if (u < 2f * cw) return new Vector2(-cw + u, -ch);
					u -= 2f * cw;
					if (u < 2f * ch) return new Vector2(cw, -ch + u);
					u -= 2f * ch;
					if (u < 2f * cw) return new Vector2(cw - u, ch);
					u -= 2f * cw;
					return new Vector2(-cw, ch - u);
				}
				Fx.Reshape(runner, Fx.StrokePts(new[] { P(u0), P(u0 + 45f), P(u0 + 90f), P(u0 + 135f), P(u0 + 180f) }, 5f, taper: true));
				runner.Modulate = new Color(1f, 1f, 1f, flip >= 1f ? 0.9f : 0f);
			}
		});

		// 5) 落雷：两道劈在人物两侧地面，第三道劈进皇帝牌
		Vector2[] hits = { feet + new Vector2(-240f, 0f), feet + new Vector2(230f, 0f) };
		for (int i = 0; i < 2; i++)
		{
			Vector2 hit = hits[i];
			int k = i;
			Fx.After(sky, strikes[i], "tyranny bolt", () =>
			{
				Bolt(back, new Vector2(hit.X + Fx.Rand(-90f, 90f), -40f), hit, green, 7f, 0.32f);
				P3Fx.Flash(back, hit, Pal.Ice, 280f, 0.18f);
				P3Fx.Shockwave(back, hit, green, 420f, 0.35f, squashY: 0.3f);
				Node2D sp = Fx.Root(back, hit, "P3TyrannySparks");
				Fx.Sparks(sp, Pal.Ice, 30, 220f, 560f, 0.45f, gravity: 1000f, scaleMin: 0.08f, scaleMax: 0.22f,
					ramp: new[] { Pal.White, green, new Color(green, 0f) });
				Fx.FreeAfter(sp, 0.6);
				Shake(ShakeStrength.Weak);
				Audio.Sfx.Play(Audio.Snd.StarSpend, 0.7f, pitch: 0.5f + 0.1f * k);
			});
		}
		Fx.After(sky, strikes[2], "tyranny main bolt", () =>
		{
			Bolt(back, new Vector2(cardAt.X + 40f, -40f), cardAt, Pal.White, 12f, 0.42f, branches: 4);
			Bolt(back, new Vector2(cardAt.X - 70f, -40f), cardAt, green, 6f, 0.3f);
			if (IsInstanceValid(cardRoot))
			{
				cardRoot.QueueFree();
			}
			P3Fx.GlassShatter(back, cardAt, 40, 1.3f, green);
			P3Fx.Flash(back, cardAt, Pal.White, 520f, 0.25f);
			P3Fx.Flash(front, center, new Color(Pal.Ice, 0.6f), 260f, 0.1f);
			P3Fx.Shockwave(back, cardAt, green, 640f, 0.45f);
			P3Fx.Shockwave(back, feet, Pal.Ice, 760f, 0.5f, squashY: 0.3f);
			P3Fx.Ripple(back, feet, 700f, 0.6f, 0.03f, squashY: 0.35f);
			P3Fx.SpeedLines(back, cardAt, green, 20, 140f, 520f, 0.4f);
			Shake(ShakeStrength.Medium);
			Audio.Sfx.Play(Audio.Snd.TarotShatter, 0.9f, pitch: 0.75f);
			Audio.Sfx.Play(Audio.Snd.CutIn, 0.6f, pitch: 0.7f);
		});

		// 6) 余电：人物身边游走的细电弧 + 向上飘的绿火星
		for (int i = 0; i < 9; i++)
		{
			Fx.After(sky, 0.95f + 0.08f * i, "tyranny arc", () =>
			{
				Vector2 a0 = center + Vector2.Right.Rotated(Fx.Rand(0f, Mathf.Tau)) * new Vector2(110f, 150f);
				Vector2 a1 = a0 + Vector2.Right.Rotated(Fx.Rand(0f, Mathf.Tau)) * Fx.Rand(40f, 80f);
				Bolt(front, a0, a1, green, 2f, 0.12f, branches: 0, segments: 6);
			});
		}
		Fx.After(sky, 0.92, "tyranny embers", () =>
		{
			Node2D em = Fx.Root(back, feet + new Vector2(0f, -40f), "P3TyrannyEmbers");
			GpuParticles2D e = Fx.Sparks(em, green, 40, 30f, 110f, 1.1f, gravity: -160f, scaleMin: 0.1f, scaleMax: 0.3f,
				ramp: new[] { new Color(Pal.Ice, 0.9f), new Color(green, 0.7f), new Color(green, 0f) }, oneShot: false);
			((ParticleProcessMaterial)e.ProcessMaterial).EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
			((ParticleProcessMaterial)e.ProcessMaterial).EmissionBoxExtents = new Vector3(220f, 20f, 0f);
			Fx.After(em, 0.6, "embers off", () => e.Emitting = false);
			Fx.FreeAfter(em, 1.8);
		});
		Fx.FreeAfter(seal, total + 0.05f);
		Fx.FreeAfter(sky, total + 0.05f);
		Fx.FreeAfter(cardRoot, total);
	}

	private static bool IsInstanceValid(GodotObject o) => GodotObject.IsInstanceValid(o);

	/// <summary>
	/// 闪电：起点到终点的锯齿折线（白芯 + 有色辉光 + 几条分叉），每 45ms 重新生成一次形状（闪烁抖动），
	/// 前 15% 时间满亮，之后随机明灭着熄灭。
	/// </summary>
	private static void Bolt(Control layer, Vector2 from, Vector2 to, Color tint, float width, float dur, int branches = 2, int segments = 16)
	{
		Node2D root = Fx.Root(layer, Vector2.Zero, "P3Bolt");
		root.TopLevel = true;
		root.GlobalPosition = Vector2.Zero;
		// 都是抗锯齿多边形（Fx.StrokePts 展开折线），不用 Line2D —— 游戏里 Line2D 的斜线有锯齿
		Polygon2D glow = Fx.Poly(root, new Vector2[3], new Color(tint, 0.35f), additive: true);
		Polygon2D core = Fx.Poly(root, new Vector2[3], Pal.White, additive: true);
		Polygon2D[] forks = new Polygon2D[branches];
		for (int i = 0; i < branches; i++)
		{
			forks[i] = Fx.Poly(root, new Vector2[3], new Color(tint.Lerp(Pal.White, 0.5f), 0.85f), additive: true);
		}
		Vector2[] pts = { from, to };
		float len = from.DistanceTo(to);
		int last = -1;
		Fx.Animate(root, dur, t =>
		{
			int f = (int)(t * dur / 0.045f);
			if (f != last)
			{
				last = f;
				pts = Jag(from, to, segments, len * 0.07f);
				Fx.Reshape(glow, Fx.StrokePts(pts, width * 3.5f));
				for (int i = 0; i < branches; i++)
				{
					Vector2 p = pts[1 + (int)Fx.Rand(0f, pts.Length - 3)];
					Vector2 dir = (to - from).Normalized().Rotated(Fx.Rand(-0.9f, 0.9f));
					Fx.Reshape(forks[i], Fx.StrokePts(Jag(p, p + dir * len * Fx.Rand(0.12f, 0.25f), 6, len * 0.03f), width * 0.45f, taper: true));
				}
			}
			float a = t < 0.15f ? 1f : (1f - t) * (Fx.Rand(0f, 1f) < 0.25f ? 0.3f : 1f);
			root.Modulate = new Color(1f, 1f, 1f, a);
			Fx.Reshape(core, Fx.StrokePts(pts, width * (1f - 0.6f * t)));
		});
		Fx.FreeAfter(root, dur + 0.03f);
	}

	/// <summary>细圆环（Ring 着色器，边缘平滑无锯齿）。</summary>
	private static ColorRect RingQuad(Node parent, float r, float w, Color tint)
	{
		float size = r * 2f / 0.8f;
		ColorRect q = Fx.ShaderQuad(parent, Shaders.Ring, size);
		Fx.SetParam(q, "tint", tint);
		Fx.SetParam(q, "radius", 0.8f);
		Fx.SetParam(q, "width", w / (size * 0.5f));
		return q;
	}

	private static Vector2[] Jag(Vector2 a, Vector2 b, int n, float amp)
	{
		Vector2 nrm = (b - a).Orthogonal().Normalized();
		Vector2[] pts = new Vector2[n + 1];
		float off = 0f;
		for (int i = 0; i <= n; i++)
		{
			float k = (float)i / n;
			off = Mathf.Clamp(off + Fx.Rand(-amp, amp) * 0.8f, -amp, amp);
			pts[i] = a.Lerp(b, k) + nrm * off * Mathf.Sin(k * Mathf.Pi);
		}
		return pts;
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

	/// <summary>棺材从地面下升起（只显示地面以上），停留后沉回。</summary>
	private static void RiseCoffin(Control layer, Vector2 floor, float s, float hold, bool numeral)
	{
		Texture2D? tex = Coffin;
		if (tex == null)
		{
			return;
		}
		float h = tex.GetHeight(), w = tex.GetWidth();
		Node2D root = Fx.Root(layer, floor, "P3Coffin");
		root.Scale = Vector2.One * s;
		Polygon2D clip = new() { Polygon = RectPts(new Rect2(-w, -h * 1.5f, w * 2f, h * 1.5f)), ClipChildren = CanvasItem.ClipChildrenMode.Only };
		root.AddChild(clip);
		Node2D body = new();
		clip.AddChild(body);
		Sprite2D spr = new() { Texture = tex, Centered = false, Position = new Vector2(-w / 2f, -h) };
		body.AddChild(spr);
		Sprite2D shine = new() { Texture = tex, Centered = false, Position = new Vector2(-w / 2f, -h), Material = Fx.Additive, Modulate = new Color(Pal.DarkHour, 0f) };
		body.AddChild(shine);
		Label? iv = null;
		if (numeral)
		{
			iv = Typo.Text(body, "IV", Typo.Latin, 64, new Color(0.75f, 1f, 0.82f), new Vector2(-30f, -h * 0.62f), outline: 4, outlineColor: new Color(0.02f, 0.08f, 0.05f));
			iv.PivotOffset = iv.Size / 2f;
			Fx.After(root, 0.52, "iv stamp", () =>
			{
				Vector2 at = floor + new Vector2(0f, (-h * 0.62f + 32f) * s);
				P3Fx.Flash(layer, at, Pal.DarkHour, 160f * s, 0.2f);
				P3Fx.Shockwave(layer, at, Pal.DarkHour, 260f * s, 0.3f);
				Audio.Sfx.Play(Audio.Snd.StarSpend, 0.6f, pitch: 0.55f);
			});
		}
		Fx.Animate(root, hold + 0.6f, t =>
		{
			float time = t * (hold + 0.6f);
			float rise = time < 0.3f ? Ease.OutBack(time / 0.3f) : time < hold ? 1f : 1f - Ease.InCubic(Mathf.Clamp((time - hold) / 0.3f, 0f, 1f));
			body.Position = new Vector2(0f, h * (1f - rise));
			shine.Modulate = new Color(Pal.DarkHour, 0.5f * Mathf.Clamp(1f - Mathf.Abs(time - 0.45f) / 0.25f, 0f, 1f));
			if (iv != null)
			{
				// 印章式落下：0.4 秒时从两倍大砸到原大小
				float stamp = Mathf.Clamp((time - 0.4f) / 0.12f, 0f, 1f);
				iv.Scale = Vector2.One * Mathf.Lerp(2.2f, 1f, Ease.InCubic(stamp));
				iv.Modulate = new Color(1f, 1f, 1f, stamp * (1f - Mathf.Clamp((time - hold + 0.2f) / 0.2f, 0f, 1f)));
			}
		});
		Fx.After(root, 0.02, "coffin dust", () =>
		{
			P3Fx.Ripple(layer, floor, 420f * s, 0.7f, 0.02f, squashY: 0.3f);
			P3Fx.Shockwave(layer, floor, Pal.DarkHour, 380f * s, 0.4f, squashY: 0.3f);
		});
		Fx.FreeAfter(root, hold + 0.65f);
	}
}
