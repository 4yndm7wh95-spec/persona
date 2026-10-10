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
		Transform2D? t = body?.GetGlobalBoneTransform(bone);
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
		// 光沿剑身扫过（每帧重新读取骨骼，跟随动画）
		Line2D glow = Fx.Line(root, new[] { hilt, hilt }, new Color(Pal.Cyan, 0.8f), 16f);
		Line2D core = Fx.Line(root, new[] { hilt, hilt }, Pal.White, 4f);
		Fx.Animate(root, 0.42f, t =>
		{
			if (!GodotObject.IsInstanceValid(node) || Bone(node, "hand") is not Vector2 h || Bone(node, "sword_tip") is not Vector2 s)
			{
				return;
			}
			float sweep = Ease.OutExpo(Ease.Segment(t, 0.35f, 0.4f));
			float fade = 1f - Ease.InCubic(Ease.Segment(t, 0.6f, 0.4f));
			Vector2 end = h.Lerp(s, sweep);
			glow.Points = core.Points = new[] { h, end };
			glow.Modulate = core.Modulate = new Color(1f, 1f, 1f, sweep <= 0f ? 0f : fade);
		});
		Fx.After(layer, 0.3, "sword tip flash", () =>
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

	/// <summary>挥砍拖尾：在接下来的 0.16 秒里记录剑身位置，生成一条渐隐的弧形光带。</summary>
	public static void SlashTrail(Control layer, NCreature node, Color tint)
	{
		Node2D root = Fx.Root(layer, Vector2.Zero, "P3SlashTrail");
		root.TopLevel = true;
		root.GlobalPosition = Vector2.Zero;
		Polygon2D ribbon = Fx.Poly(root, new Vector2[3], new Color(tint, 0.75f), additive: true);
		Polygon2D edge = Fx.Poly(root, new Vector2[3], new Color(Pal.White, 0.9f), additive: true);
		var hilts = new System.Collections.Generic.List<Vector2>();
		var tips = new System.Collections.Generic.List<Vector2>();
		Fx.Animate(root, 0.42f, t =>
		{
			if (t < 0.4f && GodotObject.IsInstanceValid(node) && Bone(node, "hand") is Vector2 h && Bone(node, "sword_tip") is Vector2 s)
			{
				hilts.Add(h.Lerp(s, 0.35f));
				tips.Add(s);
			}
			int n = tips.Count;
			if (n < 2)
			{
				return;
			}
			// 带状多边形：外缘 = 剑尖轨迹，内缘 = 剑身 35% 处轨迹；越旧越窄
			var outer = new Vector2[n];
			var inner = new Vector2[n];
			for (int i = 0; i < n; i++)
			{
				float age = (float)i / (n - 1);
				outer[i] = tips[i];
				inner[i] = tips[i].Lerp(hilts[i], age);
			}
			var poly = new Vector2[n * 2];
			for (int i = 0; i < n; i++)
			{
				poly[i] = outer[i];
				poly[n * 2 - 1 - i] = inner[i];
			}
			ribbon.Polygon = poly;
			var thin = new Vector2[n * 2];
			for (int i = 0; i < n; i++)
			{
				thin[i] = outer[i];
				thin[n * 2 - 1 - i] = outer[i].Lerp(inner[i], 0.18f);
			}
			edge.Polygon = thin;
			float fade = 1f - Ease.InCubic(Ease.Segment(t, 0.45f, 0.55f));
			root.Modulate = new Color(1f, 1f, 1f, fade);
		});
		Fx.FreeAfter(root, 0.5);
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
