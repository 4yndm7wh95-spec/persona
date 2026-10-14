using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using P3Makoto.Core;

namespace P3Makoto.Vfx;

/// <summary>
/// 追踪之刃「空间测定」（v0.14.7，替换 v0.14.0 的分裂光剑 + 黑缝横切）。
/// 这张牌让之后的君王之刃变成群体的「空间杀法」—— 打出时不砍东西，而是预演一次空间杀法：
///   0.00  时空错位：画面两次闪成米白（空间杀法反相那一拍的缩小版）；Thanatos 握剑的半透明身影在身后浮现；青白光点外飘
///         音：时间回转（evt_eft_timespin_01，压低音调，1.2s 后缓出）
///   0.45  测量：黑色虚线（空间杀法割裂线的样式，但还没"切"）一条条横贯战场，每个敌人身上两条交叉成 X，再补几条穿过敌人之间
///   锁定：每个敌人画完交叉线 → 头顶落下 P3R 黄色菱形「?」、交叉点闪一下、一声清脆的锁定声（天鹅绒塔罗光标，音高逐个升高）
///   1.30  习得：左上角 P3R 技能变更面板 —— THANATOS 名下「BRAVE BLADE 君王之刃」被一道斜线划掉，翻成「VORPAL BLADE 空间杀法」（白闪 + NEW）
///         音：P3R Persona 技能变更（skill_change）
///   1.90  余韵：虚线网淡出，菱形依次消失，Thanatos 身影散去
/// 战场上只有细线和小标记，敌人始终看得清；面板在左上角，不挡人物。
/// </summary>
public static class SeekFx
{
	private static readonly Color Cream = new(0.97f, 0.93f, 0.86f);
	private static ImageTexture? _dash;

	/// <summary>虚线贴图：26px 实线 + 14px 空隙（Line2D 平铺）。</summary>
	private static ImageTexture Dash
	{
		get
		{
			if (_dash == null)
			{
				Image img = Image.CreateEmpty(40, 8, false, Image.Format.Rgba8);
				for (int x = 0; x < 40; x++)
				{
					for (int y = 0; y < 8; y++)
					{
						img.SetPixel(x, y, x < 26 ? Colors.White : new Color(1f, 1f, 1f, 0f));
					}
				}
				_dash = ImageTexture.CreateFromImage(img);
			}
			return _dash;
		}
	}

	public static void Play(Control front, NCreature caster, Vector2[] targets)
	{
		Vector2 size = front.GetViewportRect().Size;
		Node2D root = Fx.Root(front, Vector2.Zero, "P3SeekEdge");
		root.TopLevel = true;
		root.GlobalPosition = Vector2.Zero;

		// ── 0.00 时空错位 ──
		Audio.Sfx.Clip("evt_eft_timespin_01", 0.75f, 0f, 0.8f);
		ColorRect flick = new() { Size = size, Color = new Color(Cream, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(flick);
		Fx.Animate(flick, 0.3f, t =>
		{
			float time = t * 0.3f;
			bool on = time < 0.05f || (time > 0.11f && time < 0.15f);
			flick.Color = new Color(Cream, on ? 0.38f : 0f);
		});
		PersonaFx.ChargeGhost(caster, true);
		Vector2 center = Makoto.Center(caster);
		Node2D motes = Fx.Root(front, center, "P3SeekMotes");
		Fx.Sparks(motes, Pal.Ice, 40, 120f, 380f, 0.9f, gravity: -40f);
		Fx.FreeAfter(motes, 1.3);

		// ── 0.45 测量 + 锁定 ──
		Node2D net = new();
		root.AddChild(net);
		float diag = size.Length();
		var lines = new List<(Vector2 A, Vector2 B, float At, float W)>();
		int n = targets.Length;
		float gap = n > 0 ? Mathf.Min(0.2f, 0.7f / n) : 0.2f;
		for (int i = 0; i < n; i++)
		{
			Vector2 c = targets[i];
			float at = 0.45f + gap * i;
			float a1 = Fx.Rand(0.3f, 0.6f), a2 = -Fx.Rand(0.3f, 0.6f) + Mathf.Pi;
			lines.Add((c - Vector2.Right.Rotated(a1) * diag, c + Vector2.Right.Rotated(a1) * diag, at, Fx.Rand(3f, 5f)));
			lines.Add((c - Vector2.Right.Rotated(a2) * diag, c + Vector2.Right.Rotated(a2) * diag, at + 0.05f, Fx.Rand(3f, 5f)));
			int idx = i;
			Fx.After(root, at + 0.14f, "seek lock", () =>
			{
				TargetMark(front, c, idx);
			});
		}
		// 补几条穿过敌人之间的线，织成网
		for (int k = 0; k < 3 && n > 0; k++)
		{
			Vector2 p = targets[k % n] + new Vector2(Fx.Rand(-180f, 180f), Fx.Rand(-120f, 120f));
			float ang = Fx.Rand(-0.25f, 0.25f) + (k == 1 ? Mathf.Pi / 2f - 0.2f : 0f);
			lines.Add((p - Vector2.Right.Rotated(ang) * diag, p + Vector2.Right.Rotated(ang) * diag, 0.5f + 0.12f * k + gap * n * 0.5f, Fx.Rand(2f, 3.5f)));
		}
		foreach (var (a, b, at, w) in lines)
		{
			Fx.After(root, at, "seek line", () =>
			{
				Line2D edge = new()
				{
					Width = w + 3f, DefaultColor = new Color(1f, 1f, 1f, 0.35f), Texture = Dash,
					TextureMode = Line2D.LineTextureMode.Tile, TextureRepeat = CanvasItem.TextureRepeatEnum.Enabled,
					Points = new[] { a, a }
				};
				Line2D line = new()
				{
					Width = w, DefaultColor = new Color(0.06f, 0.05f, 0.05f, 0.9f), Texture = Dash,
					TextureMode = Line2D.LineTextureMode.Tile, TextureRepeat = CanvasItem.TextureRepeatEnum.Enabled,
					Points = new[] { a, a }
				};
				net.AddChild(edge);
				net.AddChild(line);
				Fx.Animate(line, 0.16f, t =>
				{
					Vector2 head = a.Lerp(b, Ease.OutExpo(t));
					line.Points = new[] { a, head };
					edge.Points = new[] { a, head };
				});
			});
		}

		// ── 1.30 习得 ──
		Fx.After(root, 1.3, "seek learn", () => SkillChange(front, size));

		// ── 1.90 余韵 ──
		Fx.Animate(net, 2.4f, t =>
		{
			float time = t * 2.4f;
			net.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(Mathf.Clamp((time - 1.9f) / 0.5f, 0f, 1f)));
		});
		Fx.FreeAfter(root, 2.45);
	}

	/// <summary>锁定一个敌人：交叉点闪一下 + 细圈收缩 + 头顶黄色菱形「?」+ 锁定声（音高逐个升高）。</summary>
	private static void TargetMark(Control front, Vector2 c, int index)
	{
		Audio.Sfx.Clip("lock_tick", 0.8f, 0f, 1f + 0.08f * index);
		P3Fx.Flash(front, c, Pal.Ice, 120f, 0.15f);
		Node2D ring = Fx.Root(front, c, "P3SeekRing");
		Fx.StrokeClosed(ring, Fx.Circle(46f, 48), new Color(1f, 1f, 1f, 0.9f), 2f);
		Fx.StrokeClosed(ring, Fx.Circle(38f, 48), new Color(0.06f, 0.05f, 0.05f, 0.8f), 1.5f, additive: false);
		Fx.Animate(ring, 0.3f, t =>
		{
			ring.Scale = Vector2.One * Mathf.Lerp(2.2f, 1f, Ease.OutExpo(t));
			ring.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(Mathf.Clamp((t - 0.5f) / 0.5f, 0f, 1f)));
		});
		Fx.FreeAfter(ring, 0.32);
		Uniq.TargetDiamond(front, c + new Vector2(0f, -150f), 1.3f - 0.1f * index);
	}

	/// <summary>P3R 技能变更面板：THANATOS 名下「BRAVE BLADE 君王之刃」划掉 → 翻成「VORPAL BLADE 空间杀法」。</summary>
	private static void SkillChange(Control front, Vector2 size)
	{
		Audio.Sfx.Clip("skill_change", 0.9f);
		Vector2 home = new(size.X * 0.03f, size.Y * 0.13f);
		Node2D root = Fx.Root(front, home, "P3SkillChange");
		root.TopLevel = true;
		const float w = 440f, h = 132f, slant = 22f;
		// 底板：深蓝斜切 + 青色顶边 + 右侧青色斜块
		Fx.Poly(root, new[] { new Vector2(0f, 0f), new Vector2(w, 0f), new Vector2(w - slant, h), new Vector2(-slant, h) }, new Color(0.03f, 0.1f, 0.38f, 0.94f));
		Fx.Poly(root, new[] { new Vector2(0f, 0f), new Vector2(w, 0f), new Vector2(w - 1.5f, 4f), new Vector2(-1.5f, 4f) }, Pal.Cyan, additive: true);
		Fx.Poly(root, new[] { new Vector2(w - 30f, 0f), new Vector2(w + 12f, 0f), new Vector2(w - 10f, h), new Vector2(w - 52f, h) }, new Color(Pal.Cyan, 0.9f));
		Typo.Text(root, "PERSONA  ▸  THANATOS", Typo.LatinItalic, 18, Pal.Cyan, new Vector2(16f, 8f), outline: 2, outlineColor: new Color(0.02f, 0.05f, 0.18f));
		Fx.Line(root, new[] { new Vector2(14f, 34f), new Vector2(w - 50f, 34f) }, new Color(1f, 1f, 1f, 0.3f), 1f);
		// 旧技能
		Node2D old = new() { Position = new Vector2(16f, 40f) };
		root.AddChild(old);
		Typo.Text(old, "BRAVE BLADE", Typo.LatinItalic, 30, Pal.White, Vector2.Zero, outline: 2, outlineColor: new Color(0.02f, 0.05f, 0.18f));
		Typo.Text(old, "君王之刃", Typo.CjkHeavy, 22, new Color(Pal.Ice, 0.9f), new Vector2(200f, 6f));
		Line2D strike = Fx.Line(old, new[] { new Vector2(-4f, 24f), new Vector2(-4f, 24f) }, new Color(1f, 0.25f, 0.35f, 0.95f), 4f, additive: false);
		// 新技能
		Node2D neu = new() { Position = new Vector2(16f, 82f), Modulate = new Color(1f, 1f, 1f, 0f) };
		root.AddChild(neu);
		Typo.Text(neu, "VORPAL BLADE", Typo.LatinItalic, 32, Pal.White, Vector2.Zero, outline: 3, outlineColor: new Color(0.05f, 0.25f, 0.7f));
		Typo.Text(neu, "空间杀法", Typo.CjkHeavy, 24, new Color(1f, 0.92f, 0.6f), new Vector2(214f, 6f));
		Node2D tag = new() { Position = new Vector2(330f, -6f), Rotation = -0.15f };
		neu.AddChild(tag);
		Fx.Poly(tag, new[] { new Vector2(0f, 0f), new Vector2(54f, 0f), new Vector2(48f, 24f), new Vector2(-6f, 24f) }, new Color(1f, 0.85f, 0.25f));
		Typo.Text(tag, "NEW", Typo.LatinItalic, 18, new Color(0.08f, 0.06f, 0.02f), new Vector2(6f, 0f));
		Polygon2D shine = Fx.Poly(neu, new[] { new Vector2(0f, -4f), new Vector2(30f, -4f), new Vector2(18f, 44f), new Vector2(-12f, 44f) }, new Color(1f, 1f, 1f, 0.6f), additive: true);

		const float total = 1.5f;
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			float inn = Ease.OutExpo(Mathf.Clamp(time / 0.22f, 0f, 1f));
			float outT = Ease.InCubic(Mathf.Clamp((time - 1.25f) / 0.25f, 0f, 1f));
			root.GlobalPosition = home + new Vector2(-w * 1.2f * (1f - inn) - w * 1.2f * outT, 0f);
			// 0.25 划线
			float st = Ease.OutCubic(Mathf.Clamp((time - 0.25f) / 0.15f, 0f, 1f));
			strike.Points = new[] { new Vector2(-4f, 26f), new Vector2(-4f + 300f * st, 12f) };
			old.Modulate = new Color(1f, 1f, 1f, 1f - 0.6f * Mathf.Clamp((time - 0.35f) / 0.15f, 0f, 1f));
			// 0.42 新技能翻入（纵向翻转 + 扫光）
			float flip = Mathf.Clamp((time - 0.42f) / 0.16f, 0f, 1f);
			neu.Modulate = new Color(1f, 1f, 1f, flip);
			neu.Scale = new Vector2(1f, Ease.OutBack(flip));
			float sw = Mathf.Clamp((time - 0.55f) / 0.35f, 0f, 1f);
			shine.Position = new Vector2(Mathf.Lerp(-20f, 360f, Ease.InOutCubic(sw)), 0f);
			shine.Modulate = new Color(1f, 1f, 1f, sw > 0f && sw < 1f ? 1f : 0f);
			tag.Scale = Vector2.One * Ease.OutBack(Mathf.Clamp((time - 0.6f) / 0.15f, 0f, 1f));
		});
		Fx.FreeAfter(root, total + 0.05f);
		Fx.After(root, 0.42, "skill new flash", () => P3Fx.Flash(front, home + new Vector2(180f, 100f), Pal.Ice, 260f, 0.2f));
	}
}
