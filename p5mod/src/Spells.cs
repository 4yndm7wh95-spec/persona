using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace P5Joker;

/// <summary>
/// 亚森的技能（静默猎手的毒 = 咒怨侵蚀，docs/p5/ART_SPEC.md）：
///  · Eiha / Maeiha：敌人脚下暗紫色的环 → 两侧窜起原版 P5R 咒怨的红黑火舌（fire64），中间升起紫色怨灵（soul3 / ghost），
///    火和怨灵都在敌人身后、身体两侧，不盖住敌人本体；
///  · Eigaon：先在敌人身后收缩出一个黑紫色的球，然后炸开成更大的 Eiha；
///  · Kaja（增益）/ Nda（减益）：P5 的光柱 + 上升 / 下压的箭头。
/// </summary>
public static class Spells
{
	public static readonly Color Purple = new(0.62f, 0.2f, 1f), Red = new(0.92f, 0.05f, 0.12f);

	public static void Eiha(NCreature target, float power = 1f, float delay = 0f)
	{
		Control? back = Joker.FxLayer(back: true), front = Joker.FxLayer();
		if (back == null || front == null)
		{
			return;
		}
		Vector2 feet = target.GlobalPosition, center = target.VfxSpawnPosition;
		float halfW = Mathf.Max(80f, target.Hitbox?.Size.X * 0.5f ?? 110f);
		Node2D root = Fx.Root(back, feet, "P5Eiha", delay + 1.3);
		// 地面的暗紫色椭圆环
		Line2D ring = new() { Closed = true, DefaultColor = Purple, Width = 6f, Visible = false };
		root.AddChild(ring);
		Fx.Animate(ring, delay, 0.6, t =>
		{
			ring.Visible = true;
			Vector2[] c = Fx.Circle(halfW * (0.6f + 0.8f * Fx.OutCubic(t)) * power, 36);
			for (int i = 0; i < c.Length; i++)
			{
				c[i] = new Vector2(c[i].X, c[i].Y * 0.28f);
			}
			ring.Points = c;
			ring.Modulate = new Color(1, 1, 1, 1f - t);
		});
		// 红黑火舌：从身体两侧往上窜
		if (Fx.Tex("curse_fire") is Texture2D fire)
		{
			int n = Mathf.RoundToInt(8 * power);
			for (int i = 0; i < n; i++)
			{
				float side = i % 2 == 0 ? -1f : 1f;
				float x = side * Fx.Rand(halfW * 0.55f, halfW * 1.05f) * power;
				Sprite2D f = Fx.Sprite(root, fire, additive: false);
				f.Position = new Vector2(x, 0f);
				f.Offset = new Vector2(0f, -fire.GetHeight() * 0.5f);
				f.FlipH = side > 0;
				float h = Fx.Rand(1.6f, 2.6f) * power, sway = Fx.Rand(-0.25f, 0.25f);
				f.Visible = false;
				Fx.Animate(f, delay + Fx.Rand(0.05f, 0.3f), Fx.Rand(0.55f, 0.8f), t =>
				{
					f.Visible = true;
					float grow = t < 0.3f ? Fx.OutCubic(t / 0.3f) : 1f;
					f.Scale = new Vector2(1.3f * power, h * grow);
					f.Rotation = sway * Mathf.Sin(t * 9f) - side * 0.12f;
					f.Modulate = new Color(1, 1, 1, 1f - Fx.InCubic(t));
				});
			}
		}
		// 紫色怨灵往上飘
		if (Fx.Tex("curse_soul") is Texture2D soul)
		{
			for (int i = 0; i < (int)(3 * power) + 1; i++)
			{
				Sprite2D s = Fx.Sprite(root, soul);
				float x = Fx.Rand(-halfW, halfW) * 0.9f;
				s.Visible = false;
				Fx.Animate(s, delay + 0.15f + i * 0.08f, 0.8, t =>
				{
					s.Visible = true;
					s.Position = new Vector2(x + 18f * Mathf.Sin(t * 8f + x), -40f - (center.Y < feet.Y ? feet.Y - center.Y : 150f) * 1.4f * Fx.OutCubic(t));
					s.Scale = Vector2.One * 1.4f * power;
					s.Modulate = new Color(1, 1, 1, Mathf.Sin(Mathf.Pi * t));
				});
			}
		}
		// 前景只有一圈很淡的紫色闪光，不挡住敌人
		Fx.After(root, delay + 0.1, () =>
		{
			if (Fx.Tex("glow") is not Texture2D glow)
			{
				return;
			}
			Node2D flashRoot = Fx.Root(front, center, "P5EihaFlash", 0.4);
			Sprite2D g = Fx.Sprite(flashRoot, glow);
			Fx.Animate(g, 0, 0.3, t =>
			{
				g.Scale = Vector2.One * (2f + 3f * t) * power;
				g.Modulate = new Color(Purple.R, Purple.G, Purple.B, 0.45f * (1f - t));
			});
		});
		Fx.After(root, delay + 0.15, () => Fx.Shake(power > 1.3f ? ShakeStrength.Medium : ShakeStrength.Weak));
	}

	public static void Maeiha(IList<NCreature> targets, float power = 0.85f)
	{
		for (int i = 0; i < targets.Count; i++)
		{
			Eiha(targets[i], power, i * 0.07f);
		}
	}

	public static void Eigaon(NCreature target)
	{
		Control? back = Joker.FxLayer(back: true);
		if (back == null)
		{
			return;
		}
		Node2D root = Fx.Root(back, target.VfxSpawnPosition, "P5Eigaon", 0.8);
		Polygon2D core = Fx.Poly(root, Fx.Circle(1f, 40), new Color(0.05f, 0f, 0.08f));
		Line2D rim = new() { Closed = true, DefaultColor = Purple, Width = 7f };
		root.AddChild(rim);
		Fx.Animate(root, 0, 0.32, t =>
		{
			float r = 260f * (1f - Fx.OutCubic(t)) + 30f;
			core.Polygon = Fx.Circle(r, 40);
			rim.Points = Fx.Circle(r, 40);
			core.Modulate = rim.Modulate = new Color(1, 1, 1, 0.3f + 0.6f * t);
		});
		Fx.After(root, 0.32, () =>
		{
			root.Visible = false;
			Eiha(target, 1.6f);
		});
	}

	/// <summary>命中时的小咒怨：身后一簇红黑火舌 + 一只怨灵（毒刃之类）。</summary>
	public static void CurseLick(NCreature target) => Eiha(target, 0.55f);

	/// <summary>增益（-kaja）：脚下一根柔光柱 + 往上飞的光线 + 四角星；color 区分种类。</summary>
	public static void Kaja(NCreature node, Color color, float delay = 0f)
	{
		Control? back = Joker.FxLayer(back: true), front = Joker.FxLayer();
		if (back == null || front == null)
		{
			return;
		}
		Vector2 feet = node.GlobalPosition;
		Node2D col = Fx.Root(back, feet, "P5Kaja", delay + 1.0);
		if (Fx.Tex("buff_column") is Texture2D column)
		{
			Sprite2D c = Fx.Sprite(col, column);
			c.Offset = new Vector2(0f, -column.GetHeight() * 0.5f);
			Fx.Animate(c, delay, 0.8, t =>
			{
				c.Scale = new Vector2(3.2f * (1f - 0.4f * t), 4.2f * Fx.OutCubic(Mathf.Min(1f, t * 3f)));
				c.Modulate = new Color(color.R, color.G, color.B, 0.85f * (1f - Fx.InCubic(t)));
			});
		}
		Node2D up = Fx.Root(front, feet, "P5KajaFront", delay + 1.0);
		if (Fx.Tex("buff_line") is Texture2D line)
		{
			for (int i = 0; i < 9; i++)
			{
				Sprite2D l = Fx.Sprite(up, line);
				float x = Fx.Rand(-90f, 90f);
				l.Visible = false;
				Fx.Animate(l, delay + Fx.Rand(0f, 0.35f), 0.45, t =>
				{
					l.Visible = true;
					l.Position = new Vector2(x, -40f - 380f * Fx.OutCubic(t));
					l.Scale = new Vector2(0.5f, 0.9f);
					l.Modulate = new Color(color.R, color.G, color.B, 1f - t);
				});
			}
		}
		for (int i = 0; i < 3; i++)
		{
			Polygon2D a = Fx.Poly(up, Fx.Arrow(26f, 40f), color, additive: true);
			float x = (i - 1) * 70f;
			a.Visible = false;
			Fx.Animate(a, delay + 0.1f + i * 0.08f, 0.55, t =>
			{
				a.Visible = true;
				a.Position = new Vector2(x, -120f - 200f * Fx.OutCubic(t));
				a.Modulate = new Color(1, 1, 1, Mathf.Sin(Mathf.Pi * t));
			});
		}
	}

	/// <summary>减益（-nda）：头顶往下压的红 / 蓝箭头 + 一圈往里收的环。</summary>
	public static void Nda(NCreature target, Color color, float delay = 0f)
	{
		Control? front = Joker.FxLayer();
		if (front == null)
		{
			return;
		}
		Vector2 top = target.VfxSpawnPosition + new Vector2(0f, -(target.Hitbox?.Size.Y ?? 260f) * 0.55f);
		Node2D root = Fx.Root(front, top, "P5Nda", delay + 0.9);
		for (int i = 0; i < 3; i++)
		{
			Polygon2D a = Fx.Poly(root, Fx.Arrow(24f, 38f), color);
			a.Rotation = Mathf.Pi;
			Line2D o = new() { Points = Fx.Arrow(24f, 38f), Closed = true, Width = 4f, DefaultColor = Colors.Black };
			a.AddChild(o);
			float x = (i - 1) * 64f;
			a.Visible = false;
			Fx.Animate(a, delay + i * 0.07f, 0.6, t =>
			{
				a.Visible = true;
				a.Position = new Vector2(x, -60f + 110f * Fx.OutCubic(t));
				a.Modulate = new Color(1, 1, 1, Mathf.Sin(Mathf.Pi * t));
			});
		}
		Line2D ring = new() { Closed = true, Width = 5f, DefaultColor = color, Visible = false };
		root.AddChild(ring);
		Fx.Animate(ring, delay, 0.45, t =>
		{
			ring.Visible = true;
			ring.Points = Fx.Circle(150f * (1f - 0.6f * Fx.OutCubic(t)));
			ring.Position = new Vector2(0f, 120f);
			ring.Modulate = new Color(1, 1, 1, 1f - t);
		});
	}
}
