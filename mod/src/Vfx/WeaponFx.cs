using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace P3Makoto.Vfx;

/// <summary>
/// 武器显现 / 消散 / 挥砍拖尾。位置全部读自 Spine 骨骼（hand、sword_tip、evoker_tip），
/// 所以特效永远贴在剑和枪上，不会和动画错位。
/// </summary>
public static class WeaponFx
{
	public static Vector2? Bone(NCreature node, string bone)
	{
		MegaSprite? body = node.Visuals.SpineBody;
		Transform2D? t = P3Makoto.Core.Compat.BoneTransform(body, bone);
		return t?.Origin;
	}

	/// <summary>剑凝聚成形：光粒从四周汇向剑身，一道光从护手扫到剑尖，剑尖闪光。</summary>
	public static void SwordMaterialize(Control layer, NCreature node)
	{
		if (Bone(node, "hand") is not Vector2 hilt || Bone(node, "sword_tip") is not Vector2 tip)
		{
			return;
		}
		Node2D root = Fx.Root(layer, Vector2.Zero, "P3SwordForm");
		root.TopLevel = true;
		root.GlobalPosition = Vector2.Zero;
		// 汇聚光粒
		for (int i = 0; i < 18; i++)
		{
			float u = Fx.Rand(0.15f, 1f);
			Vector2 target = hilt.Lerp(tip, u);
			Vector2 start = target + Fx.RandDir() * Fx.Rand(60f, 140f);
			Sprite2D mote = new Sprite2D { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = Pal.Cyan, Position = start, Scale = Vector2.One * 0.45f };
			root.AddChild(mote);
			float delay = Fx.Rand(0f, 0.08f);
			Fx.Animate(mote, 0.22f + delay, t =>
			{
				float local = Ease.Segment(t, delay / (0.22f + delay), 0.22f / (0.22f + delay));
				mote.Position = start.Lerp(target, Ease.InCubic(local));
				mote.Modulate = new Color(Pal.Cyan, local <= 0f ? 0f : 1f - local * 0.3f);
			});
		}
		// 刃光：一段短而尖的高光从护手划到剑尖（像抛光刀身上的反光），每帧读取骨骼跟随动画
		Curve gleamShape = new Curve();
		gleamShape.AddPoint(new Vector2(0f, 0f));
		gleamShape.AddPoint(new Vector2(0.5f, 1f));
		gleamShape.AddPoint(new Vector2(1f, 0f));
		Line2D halo = Fx.Line(root, new[] { hilt, hilt }, new Color(Pal.Cyan, 0.35f), 14f);
		Line2D gleam = Fx.Line(root, new[] { hilt, hilt }, Pal.White, 5f);
		halo.WidthCurve = gleam.WidthCurve = gleamShape;
		halo.Modulate = gleam.Modulate = new Color(1f, 1f, 1f, 0f);
		Fx.Animate(root, 0.5f, t =>
		{
			if (!GodotObject.IsInstanceValid(node) || Bone(node, "hand") is not Vector2 h || Bone(node, "sword_tip") is not Vector2 s)
			{
				return;
			}
			Vector2 baseP = h.Lerp(s, 0.18f);           // 护手位置
			float u = Ease.InOutCubic(Ease.Segment(t, 0.3f, 0.5f));
			float head = Mathf.Lerp(0f, 1.15f, u);
			float tail = head - 0.28f;
			Vector2 a = baseP.Lerp(s, Mathf.Clamp(tail, 0f, 1f));
			Vector2 b = baseP.Lerp(s, Mathf.Clamp(head, 0f, 1f));
			gleam.Points = halo.Points = new[] { a, b };
			float alpha = (t < 0.3f || u >= 1f) ? 0f : Mathf.Sin(u * Mathf.Pi);
			gleam.Modulate = halo.Modulate = new Color(1f, 1f, 1f, alpha);
		});
		Fx.After(layer, 0.4, "sword tip flash", () =>
		{
			if (GodotObject.IsInstanceValid(node) && Bone(node, "sword_tip") is Vector2 s)
			{
				P3Fx.Flash(layer, s, Pal.Ice, 140f, 0.2f);
				Fx.Sparks(Fx.Root(layer, s, "P3TipSpark"), Pal.Cyan, 14, 80f, 220f, 0.35f);
			}
		});
		Fx.FreeAfter(root, 0.6);
	}

	/// <summary>剑消散：剑身沿线碎成玻璃屑与光点，向上飘散，并放出一只蝴蝶。</summary>
	public static void SwordDissolve(Control layer, NCreature node)
	{
		if (Bone(node, "hand") is not Vector2 hilt || Bone(node, "sword_tip") is not Vector2 tip)
		{
			return;
		}
		for (int i = 0; i < 4; i++)
		{
			Vector2 p = hilt.Lerp(tip, 0.25f + 0.22f * i);
			P3Fx.GlassShatter(layer, p, 6, 0.45f, Pal.Ice);
		}
		Node2D root = Fx.Root(layer, hilt.Lerp(tip, 0.5f), "P3SwordFade");
		GpuParticles2D motes = Fx.Sparks(root, Pal.Cyan, 26, 30f, 120f, 0.8f, gravity: -160f, scaleMin: 0.12f, scaleMax: 0.3f);
		ParticleProcessMaterial pm = (ParticleProcessMaterial)motes.ProcessMaterial;
		pm.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
		pm.EmissionBoxExtents = new Vector3((tip - hilt).Length() * 0.5f, 6f, 0f);
		root.Rotation = (tip - hilt).Angle();
		P3Fx.Butterfly(layer, tip, tip + new Vector2(Fx.Rand(-30f, 50f), -160f), 1.3f, 0.7f);
		Fx.FreeAfter(root, 1.2);
	}

	/// <summary>
	/// 斩击残影：从蓄力开始记录剑尖 / 剑身轨迹，帧与帧之间用 Catmull-Rom 曲线补点（挥剑只用 1~2 帧，不补就只剩一个三角），
	/// 画成一道宽大的新月：外层大范围青色光晕 + 中层色带 + 白色刃芯；扫完后停留一下再收窄淡出。
	/// </summary>
	public static void SlashTrail(Control layer, NCreature node, Color tint, float record = 0.16f, float width = 1f)
	{
		Node2D root = Fx.Root(layer, Vector2.Zero, "P3SlashTrail");
		root.TopLevel = true;
		root.GlobalPosition = Vector2.Zero;
		Polygon2D halo = Fx.Poly(root, new Vector2[3], new Color(tint, 0.35f), additive: true);
		Polygon2D ribbon = Fx.Poly(root, new Vector2[3], new Color(tint, 0.9f), additive: true);
		Polygon2D core = Fx.Poly(root, new Vector2[3], Pal.White, additive: true);
		var hilts = new System.Collections.Generic.List<Vector2>();
		var tips = new System.Collections.Generic.List<Vector2>();
		const float total = 0.55f;
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			if (time < record && GodotObject.IsInstanceValid(node) && Bone(node, "hand") is Vector2 h && Bone(node, "sword_tip") is Vector2 s)
			{
				if (tips.Count == 0 || tips[^1].DistanceSquaredTo(s) > 4f)
				{
					hilts.Add(h.Lerp(s, 0.12f));
					tips.Add(s);
				}
			}
			if (tips.Count < 2)
			{
				return;
			}
			Vector2[] o = Smooth(tips, 8);
			Vector2[] inn = Smooth(hilts, 8);
			int n = o.Length;
			// 收尾：扫完后整条残影从尾部开始收窄
			float shrink = Ease.InCubic(Mathf.Clamp((time - record) / (total - record), 0f, 1f));
			Vector2[] Band(float inner, float outerPush)
			{
				var poly = new Vector2[n * 2];
				for (int i = 0; i < n; i++)
				{
					float age = 1f - (float)i / (n - 1);                    // 0 = 最新
					float w = Mathf.Clamp(1f - age * 0.85f - shrink, 0f, 1f) * width;
					Vector2 dir = (o[i] - inn[i]).Normalized();
					poly[i] = o[i] + dir * outerPush * w;
					poly[n * 2 - 1 - i] = o[i].Lerp(inn[i], inner * w);
				}
				return poly;
			}
			Fx.Reshape(halo, Band(0.95f, 26f));
			Fx.Reshape(ribbon, Band(0.7f, 6f));
			Fx.Reshape(core, Band(0.16f, 0f));
			root.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(Mathf.Clamp((time - 0.3f) / 0.25f, 0f, 1f)));
		});
		Fx.FreeAfter(root, total + 0.05);
	}

	/// <summary>Catmull-Rom 插值：每两个采样点之间补 sub 个点。</summary>
	private static Vector2[] Smooth(System.Collections.Generic.List<Vector2> p, int sub)
	{
		var outp = new System.Collections.Generic.List<Vector2>();
		for (int i = 0; i < p.Count - 1; i++)
		{
			Vector2 p0 = p[Mathf.Max(i - 1, 0)], p1 = p[i], p2 = p[i + 1], p3 = p[Mathf.Min(i + 2, p.Count - 1)];
			for (int k = 0; k < sub; k++)
			{
				float t = k / (float)sub, t2 = t * t, t3 = t2 * t;
				outp.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
			}
		}
		outp.Add(p[^1]);
		return outp.ToArray();
	}

	/// <summary>蓄力到顶：剑尖闪出十字星光（提示"要砍了"）。</summary>
	public static void TipGlint(Control layer, NCreature node)
	{
		if (Bone(node, "sword_tip") is not Vector2 tip)
		{
			return;
		}
		Node2D root = Fx.Root(layer, tip, "P3TipGlint");
		Polygon2D star = Fx.Poly(root, Fx.Star4(46f, 0.1f), Pal.White, additive: true);
		Polygon2D halo = Fx.Poly(root, Fx.Star4(80f, 0.06f), new Color(Pal.Cyan, 0.6f), additive: true);
		halo.Rotation = Mathf.Pi / 4f;
		Fx.Animate(root, 0.22f, t =>
		{
			float pop = t < 0.3f ? Ease.OutExpo(t / 0.3f) : 1f - Ease.InCubic((t - 0.3f) / 0.7f);
			root.Scale = Vector2.One * pop;
			root.Rotation = t * 0.6f;
			if (GodotObject.IsInstanceValid(node) && Bone(node, "sword_tip") is Vector2 p)
			{
				root.GlobalPosition = p;
			}
		});
		Fx.FreeAfter(root, 0.3);
	}

	/// <summary>挥砍残影：每隔 2 帧在剑身位置留一道淡出的剑影，共 3~4 道。</summary>
	public static void BladeGhosts(Control layer, NCreature node, Color tint)
	{
		for (int i = 0; i < 4; i++)
		{
			Fx.After(layer, 0.025 * i, "blade ghost", () =>
			{
				if (!GodotObject.IsInstanceValid(node) || Bone(node, "hand") is not Vector2 h || Bone(node, "sword_tip") is not Vector2 s)
				{
					return;
				}
				Node2D root = Fx.Root(layer, Vector2.Zero, "P3BladeGhost");
				root.TopLevel = true;
				root.GlobalPosition = Vector2.Zero;
				Vector2 a = h.Lerp(s, 0.2f);
				Line2D ghost = Fx.Line(root, new[] { a, s }, new Color(tint, 0.55f), 10f);
				Line2D edge = Fx.Line(root, new[] { a, s }, new Color(Pal.White, 0.7f), 2.5f);
				Fx.Animate(root, 0.22f, t => root.Modulate = new Color(1f, 1f, 1f, 1f - Ease.OutCubic(t)));
				Fx.FreeAfter(root, 0.25);
			});
		}
	}

	/// <summary>踏步扬尘：斩下时脚前扬起一小团尘土（低饱和灰蓝，不抢主体）。</summary>
	public static void StepDust(Control layer, Vector2 feet)
	{
		Node2D root = Fx.Root(layer, feet + new Vector2(60f, -4f), "P3StepDust");
		GpuParticles2D dust = Fx.Sparks(root, new Color(0.65f, 0.7f, 0.8f, 0.45f), 18, 60f, 200f, 0.6f, gravity: 120f, scaleMin: 0.8f, scaleMax: 1.8f);
		dust.Material = null;                         // 非加法：尘土是遮挡而不是发光
		ParticleProcessMaterial pm = (ParticleProcessMaterial)dust.ProcessMaterial;
		pm.Direction = new Vector3(0.6f, -1f, 0f);
		pm.Spread = 50f;
		pm.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
		pm.EmissionBoxExtents = new Vector3(50f, 4f, 0f);
		Fx.FreeAfter(root, 0.9);
	}

	/// <summary>召唤器出现：手部一道银光闪过。</summary>
	public static void EvokerGlint(Control layer, NCreature node)
	{
		if (Bone(node, "evoker_tip") is Vector2 tip && Bone(node, "hand") is Vector2 hand)
		{
			P3Fx.Flash(layer, hand.Lerp(tip, 0.5f), Pal.Ice, 120f, 0.18f);
			Fx.Sparks(Fx.Root(layer, hand.Lerp(tip, 0.5f), "P3EvokerGlint"), Pal.Ice, 10, 60f, 160f, 0.3f);
		}
	}
}
