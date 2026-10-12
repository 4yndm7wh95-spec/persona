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
		public Polygon2D[] Stripes = System.Array.Empty<Polygon2D>();
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
		// 锥体（原点在锥尖，向上伸出）：外层青色光晕 + 深蓝锥身 + 螺旋条纹（单层裁切）+ 白色锥芯
		Vector2[] cone = { new(-DrillW / 2f, -DrillLen), new(DrillW / 2f, -DrillLen), Vector2.Zero };
		Fx.Poly(d.Body, new[] { new Vector2(-DrillW * 0.75f, -DrillLen - 20f), new Vector2(DrillW * 0.75f, -DrillLen - 20f), new Vector2(0f, 30f) }, new Color(Pal.Cyan, 0.35f), additive: true);
		Fx.Poly(d.Body, cone, new Color(0.05f, 0.14f, 0.5f));
		Polygon2D clip = new() { Polygon = cone, ClipChildren = CanvasItem.ClipChildrenMode.Only };
		d.Body.AddChild(clip);
		d.Stripes = new Polygon2D[7];
		for (int i = 0; i < d.Stripes.Length; i++)
		{
			d.Stripes[i] = Fx.Poly(clip, new Vector2[4], i % 2 == 0 ? Pal.Ice : new Color(Pal.Cyan, 0.9f), additive: true);
		}
		Fx.Poly(d.Body, new[] { new Vector2(-8f, -DrillLen), new Vector2(8f, -DrillLen), Vector2.Zero }, Pal.White, additive: true);
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
			d.Spin += (float)tick.WaitTime * (6f + 3f * Mathf.Min(d.Hits, 6));
			LayStripes(d);
			ulong idle = Time.GetTicksMsec() - d.Last;
			if (idle > (d.Hits == 0 ? 2500UL : 420UL))
			{
				tick.Stop();
				Drills.Remove(id);
				Shatter(front, d);
			}
		});
	}

	/// <summary>螺旋条纹：斜向平行四边形沿锥身向下滚动（看起来在旋转）。</summary>
	private static void LayStripes(Drill d)
	{
		float span = DrillLen / d.Stripes.Length;
		for (int i = 0; i < d.Stripes.Length; i++)
		{
			float y = -DrillLen + ((i * span + d.Spin * 40f) % DrillLen);
			float h = span * 0.42f, slant = 26f;
			float w0 = DrillHalf(y + DrillLen), w1 = DrillHalf(y + h + DrillLen);
			Fx.Reshape(d.Stripes[i], new[] { new Vector2(-w0 - 10f, y), new Vector2(w0 + 10f, y - slant), new Vector2(w1 + 10f, y + h - slant), new Vector2(-w1 - 10f, y + h) });
		}
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
}
