using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace P5Joker;

/// <summary>
/// 召唤亚森（docs/p5/FX_PLAN.md 第 3 节；用户 2026-10-28：用人格面具的时候要华丽、有冲击力，像结城理那个碎玻璃）。
/// 完整版 Summon（大招）：
///  1. 背景压暗成红黑，画面上方斜插进 P5 的红色切入横幅（网点 + 白斜线 + Joker 侧脸，art/p5/fx/gen/cutin_band.png），左上角弹出技能名斜框；
///  2. 0.42 秒：横幅以 Joker 的脸为中心炸成十几块红玻璃（碎片就是横幅贴图的切块），翻转着飞向镜头，白闪 + 震屏；
///  3. 同时 Joker 脸上燃起蓝焰面具（原版 PERSONA_C/D 的火焰形，青蓝加色），亚森在他身后从蓝焰里现身（原版模型渲的待机序列帧），
///     onSkill 回调放技能（咒怨火、增益光柱等），亚森停留约 1.1 秒后化成蓝焰散去。
/// 简化版 Quick（小技能）：只有蓝焰面具一闪 + 技能名斜框 + 半透明的亚森在身后一晃。
/// </summary>
public static class Persona
{
	private static readonly Color Red = new(0.9f, 0f, 0.07f), Blue = new(0.35f, 0.85f, 1f);

	public static void Summon(NCreature joker, string skill, Action? onSkill, bool big = false)
	{
		Control? front = Joker.FxLayer();
		Control? back = Joker.FxLayer(back: true);
		if (front == null || back == null)
		{
			onSkill?.Invoke();
			return;
		}
		Vector2 screen = Fx.Screen(front);
		Vector2 face = Joker.PosePoint(joker, new Vector2(805f, 215f));
		float side = Joker.Facing(joker);

		// 压暗：红黑渐变的底，留出人物（在身后的特效层）
		ColorRect dim = new() { Color = new Color(0.08f, 0f, 0.02f, 0f), Size = screen * 1.3f, MouseFilter = Control.MouseFilterEnum.Ignore };
		back.AddChild(dim);
		dim.GlobalPosition = -screen * 0.15f;
		Fx.FreeAfter(dim, 1.9);
		Fx.Animate(dim, 0, 1.8, t =>
		{
			float a = t < 0.1f ? t / 0.1f : t > 0.75f ? 1f - (t - 0.75f) / 0.25f : 1f;
			dim.Color = new Color(0.1f, 0f, 0.02f, 0.62f * a);
		});

		SkillTag(front, skill, 1.6);
		Band(back, screen, face, big);
		Fx.After(back, 0.42, () =>
		{
			Fx.Shake(big ? ShakeStrength.Strong : ShakeStrength.Medium);
			Flash(front, screen, 0.12f);
			MaskFlame(front, face, side, 0.9);
			Arsene(back, joker, side, big ? 1.5 : 1.15);
		});
		Fx.After(back, 0.62, () => onSkill?.Invoke());
	}

	public static void Quick(NCreature joker, string skill, Action? onSkill)
	{
		Control? front = Joker.FxLayer();
		Control? back = Joker.FxLayer(back: true);
		if (front == null || back == null)
		{
			onSkill?.Invoke();
			return;
		}
		Vector2 face = Joker.PosePoint(joker, new Vector2(805f, 215f));
		float side = Joker.Facing(joker);
		SkillTag(front, skill, 1.1);
		Fx.After(back, 0.2, () =>
		{
			MaskFlame(front, face, side, 0.55);
			Arsene(back, joker, side, 0.75, ghost: true);
		});
		Fx.After(back, 0.38, () => onSkill?.Invoke());
	}

	/// <summary>P5 的技能名：黑底斜框白字，左上角从左滑进来，下面垫一条红色错位的斜框。</summary>
	public static void SkillTag(Control layer, string text, double hold)
	{
		Vector2 screen = Fx.Screen(layer);
		Node2D root = Fx.Root(layer, new Vector2(screen.X * 0.05f, screen.Y * 0.2f), "P5SkillTag", hold + 0.4);
		float w = 60f + 34f * text.Length;
		Fx.Poly(root, Fx.Slant(w + 30f, 74f, 22f), Red).Position = new Vector2(10f, 10f);
		Fx.Poly(root, Fx.Slant(w, 74f, 22f), Colors.Black);
		Label l = Fx.Text(root, text, 52, Colors.White, Colors.Black, 2);
		l.Position = new Vector2(30f, -72f);
		root.Rotation = -0.05f;
		Fx.Animate(root, 0, hold + 0.3, t =>
		{
			float time = (float)((hold + 0.3) * t);
			float inT = Mathf.Clamp(time / 0.12f, 0f, 1f), outT = Mathf.Clamp((time - (float)hold) / 0.25f, 0f, 1f);
			root.Position = new Vector2(screen.X * 0.05f - 500f * (1f - Fx.OutCubic(inT)) - 600f * Fx.InCubic(outT), screen.Y * 0.2f);
		});
	}

	private static void Flash(Control layer, Vector2 screen, float seconds)
	{
		ColorRect f = new() { Color = Colors.White, Size = screen * 1.2f, MouseFilter = Control.MouseFilterEnum.Ignore };
		layer.AddChild(f);
		f.GlobalPosition = -screen * 0.1f;
		Fx.FreeAfter(f, seconds + 0.05);
		Fx.Animate(f, 0, seconds, t => f.Color = new Color(1f, 1f, 1f, 0.75f * (1f - t)));
	}

	/// <summary>切入横幅：从左边斜着插进来，0.42 秒时以 Joker 的脸为中心炸碎。</summary>
	private static void Band(Control layer, Vector2 screen, Vector2 face, bool big)
	{
		if (Fx.Tex("cutin_band") is not Texture2D tex)
		{
			return;
		}
		float bw = screen.X * 1.3f, bh = screen.Y * (big ? 0.34f : 0.28f);
		Node2D root = Fx.Root(layer, new Vector2(screen.X * 0.5f, screen.Y * 0.36f), "P5CutIn", 1.4);
		root.Rotation = -0.1f;
		Vector2 tsize = tex.GetSize();
		Vector2 half = new(bw / 2f, bh / 2f);
		Vector2 Uv(Vector2 p) => new((p.X + half.X) / bw * tsize.X, (p.Y + half.Y) / bh * tsize.Y);
		Vector2[] quad = { -half, new(half.X, -half.Y), half, new(-half.X, half.Y) };
		Polygon2D band = new() { Texture = tex, Polygon = quad, UV = quad.Select(Uv).ToArray() };
		root.AddChild(band);
		Line2D edgeTop = new() { Points = new[] { quad[0], quad[1] }, Width = 10f, DefaultColor = Colors.White };
		Line2D edgeBottom = new() { Points = new[] { quad[3], quad[2] }, Width = 10f, DefaultColor = Colors.White };
		root.AddChild(edgeTop);
		root.AddChild(edgeBottom);
		// 滑入 0.13 秒，之后慢慢往右平移
		Fx.Animate(band, 0, 0.42, t =>
		{
			float time = 0.42f * t;
			float x = -screen.X * 1.2f * (1f - Fx.OutCubic(Mathf.Clamp(time / 0.13f, 0f, 1f))) + 30f * time;
			band.Position = edgeTop.Position = edgeBottom.Position = new Vector2(x, 0f);
		});
		Fx.After(root, 0.42, () =>
		{
			Vector2 impact = root.ToLocal(face);
			impact = new Vector2(Mathf.Clamp(impact.X, -half.X * 0.8f, half.X * 0.8f), Mathf.Clamp(impact.Y, -half.Y * 0.6f, half.Y * 0.6f));
			band.Visible = edgeTop.Visible = edgeBottom.Visible = false;
			Shatter(root, tex, quad, Uv, band.Position, impact, big ? 22 : 16);
		});
	}

	/// <summary>横幅碎成玻璃：以冲击点为中心，边上取一圈点，相邻两点和冲击点组成三角（再对半切一次），
	/// 每块带着自己那一片贴图往外飞、翻转、放大（冲向镜头），白色描边。</summary>
	private static void Shatter(Node2D root, Texture2D tex, Vector2[] quad, Func<Vector2, Vector2> uv, Vector2 offset, Vector2 impact, int n)
	{
		List<Vector2> ring = new();
		for (int e = 0; e < 4; e++)
		{
			Vector2 a = quad[e], b = quad[(e + 1) % 4];
			int k = e % 2 == 0 ? n / 3 : n / 6;
			for (int i = 0; i < k; i++)
			{
				ring.Add(a.Lerp(b, (i + Fx.Rand(0.1f, 0.9f)) / k));
			}
		}
		ring = ring.OrderBy(p => Mathf.Atan2(p.Y - impact.Y, p.X - impact.X)).ToList();
		for (int i = 0; i < ring.Count; i++)
		{
			Vector2 p0 = ring[i], p1 = ring[(i + 1) % ring.Count];
			Vector2 mid = impact.Lerp(p0.Lerp(p1, 0.5f), Fx.Rand(0.35f, 0.6f));
			foreach (Vector2[] tri in new[] { new[] { impact, p0, mid }, new[] { mid, p0, p1 }, new[] { impact, mid, p1 } })
			{
				Piece(root, tex, tri, uv, offset, impact);
			}
		}
	}

	private static void Piece(Node2D root, Texture2D tex, Vector2[] tri, Func<Vector2, Vector2> uv, Vector2 offset, Vector2 impact)
	{
		Vector2 c = (tri[0] + tri[1] + tri[2]) / 3f;
		Vector2[] local = tri.Select(p => p - c).ToArray();
		Node2D piece = new() { Position = c + offset };
		root.AddChild(piece);
		piece.AddChild(new Polygon2D { Texture = tex, Polygon = local, UV = tri.Select(uv).ToArray() });
		piece.AddChild(new Line2D { Points = local.Append(local[0]).ToArray(), Width = 3f, DefaultColor = new Color(1f, 1f, 1f, 0.9f) });
		Vector2 dir = (c - impact).Normalized();
		if (dir == Vector2.Zero)
		{
			dir = Vector2.Up;
		}
		float dist = Fx.Rand(260f, 620f), spin = Fx.Rand(-5f, 5f), grow = Fx.Rand(0.4f, 1.1f);
		Vector2 start = piece.Position;
		Fx.Animate(piece, 0, Fx.Rand(0.45f, 0.7f), t =>
		{
			float e = Fx.OutCubic(t);
			piece.Position = start + dir * dist * e + new Vector2(0f, 160f * t * t);
			piece.Rotation = spin * e;
			piece.Scale = Vector2.One * (1f + grow * e);
			piece.Modulate = new Color(1, 1, 1, 1f - Fx.InCubic(t));
		});
	}

	/// <summary>蓝焰面具：脸上一圈往上窜的青蓝火舌（原版的面具火焰形），中心一团亮白，seconds 后散掉。</summary>
	public static void MaskFlame(Control layer, Vector2 face, float side, double seconds)
	{
		Node2D root = Fx.Root(layer, face, "P5MaskFlame", seconds + 0.2);
		root.Scale = new Vector2(side, 1f);
		if (Fx.Tex("glow") is Texture2D glow)
		{
			Sprite2D g = Fx.Sprite(root, glow);
			Fx.Animate(g, 0, seconds, t =>
			{
				g.Scale = Vector2.One * (1.6f + 0.6f * Mathf.Sin(t * 30f) * 0.1f) * (t < 0.15f ? t / 0.15f : 1f);
				g.Modulate = new Color(Blue.R, Blue.G, Blue.B, 0.9f * (1f - Fx.InCubic(t)));
			});
		}
		Texture2D? flame = Fx.Tex("blue_flame");
		if (flame == null)
		{
			return;
		}
		// 面具的轮廓：眼罩形的一圈（左右两个上翘的尖），每个点一簇火舌
		for (int i = 0; i < 14; i++)
		{
			float a = Mathf.Tau * i / 14f;
			Vector2 p = new(Mathf.Cos(a) * 62f, Mathf.Sin(a) * 26f - (Mathf.Abs(Mathf.Cos(a)) > 0.8f ? 22f : 0f));
			Sprite2D s = Fx.Sprite(root, flame);
			s.Position = p;
			float phase = Fx.Rand(0f, 6f), size = Fx.Rand(0.9f, 1.4f);
			Fx.Animate(s, Fx.Rand(0f, 0.08f), seconds, t =>
			{
				float flick = 0.85f + 0.15f * Mathf.Sin(phase + t * 40f);
				s.Scale = new Vector2(0.9f, 1.3f * flick) * size * (t < 0.1f ? t / 0.1f : 1f);
				s.Position = p + new Vector2(0f, -30f * t);
				s.Modulate = new Color(Blue.R, Blue.G, Blue.B, 1f - Fx.InCubic(t));
			});
		}
	}

	/// <summary>亚森：原版模型渲的待机序列帧（art/p5/fx/gen/arsene_idle.png，9 列，41 帧，15fps），站在 Joker 身后左上方，
	/// 从蓝焰里现身（蓝色 → 本色），seconds 后再染蓝淡出。ghost = 半透明一晃（简化召唤）。</summary>
	public static void Arsene(Control layer, NCreature joker, float side, double seconds, bool ghost = false)
	{
		if (Fx.Tex("arsene_idle") is not Texture2D sheet)
		{
			return;
		}
		const int cols = 9, count = 41;
		const float fw = 393f, fh = 460f;
		Vector2 feet = joker.GlobalPosition;
		Node2D root = Fx.Root(layer, feet + new Vector2(-side * 110f, -70f), "P5Arsene", seconds + 0.1);
		root.Scale = new Vector2(side, 1f) * 1.15f;
		Sprite2D s = new() { Texture = sheet, RegionEnabled = true, Centered = false };
		s.Position = new Vector2(-633.951f * 0.45f, -878.61f * 0.45f);   // 帧里亚森脚底（anchor.json）对齐 root
		root.AddChild(s);
		float peak = ghost ? 0.55f : 1f;
		Fx.Animate(s, 0, seconds, t =>
		{
			float time = (float)(seconds * t);
			int f = (int)(time * 15f) % count;
			s.RegionRect = Fx.Frame(f, cols, fw, fh);
			float inT = Mathf.Clamp(time / 0.25f, 0f, 1f), outT = Mathf.Clamp((time - (float)seconds + 0.3f) / 0.3f, 0f, 1f);
			float blue = 1f - inT + outT;
			s.Modulate = new Color(Mathf.Lerp(1f, Blue.R, blue), Mathf.Lerp(1f, Blue.G, blue), Mathf.Lerp(1f, Blue.B, blue), peak * inT * (1f - outT));
			root.Position = feet + new Vector2(-side * 110f, -70f - 18f * Mathf.Sin(time * 3f));
		});
		if (!ghost && Fx.Tex("blue_flame") is Texture2D flame)
		{
			// 现身时脚下一圈蓝焰往上卷
			for (int i = 0; i < 10; i++)
			{
				Sprite2D f = Fx.Sprite(root, flame);
				float x = Fx.Rand(-150f, 150f);
				Fx.Animate(f, Fx.Rand(0f, 0.12f), 0.5, t =>
				{
					f.Position = new Vector2(x, -40f - 260f * Fx.OutCubic(t));
					f.Scale = new Vector2(1.6f, 2.4f) * (1f - 0.5f * t);
					f.Modulate = new Color(Blue.R, Blue.G, Blue.B, 1f - t);
				});
			}
		}
	}
}
