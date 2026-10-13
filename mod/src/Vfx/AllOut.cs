using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace P3Makoto.Vfx;

/// <summary>
/// 总攻击（All-Out Attack，v0.14.1）：精英 / Boss 战的最后一击之后、胜利结算之前播放（AllOutPatch 把战斗结束流程推迟 Duration 秒）。
/// 逐帧参照 research/p3r_video/NOTES.md 第 3 节，把原版约 12 秒压到约 3.7 秒，保留最有辨识度的四拍：
///   0.00  右下角斜切蓝色大横幅「ALL-OUT ATTACK / 总攻击」滑入
///   0.40  同心圆光圈转场（浅蓝 / 深蓝大圆从画面边缘扫过）
///   0.85  乱斗烟尘：一大团蓝白卡通烟云罩住敌群，云里不断闪出四芒星和斩击线
///   1.95  全屏白
///   2.15  敌人位置炸开黑红墨点 + 一道红色斩线；结城理立绘从右侧滑入
///   2.40  结束立绘：青色底 + 巨大圆环 + 「DONE AND DUSTED.」+ 背后巨大的深蓝斜体字，停留后褪成黑白、淡出
/// 结束立绘先用 victory_portrait 顶着（art/v13/finish 出图后换）。
/// </summary>
public static class AllOut
{
	public const float Duration = 3.75f;

	private static Shader? _gray;

	private static Shader Gray => _gray ??= new Shader
	{
		Code = @"
shader_type canvas_item;
uniform float gray = 0.0;
void fragment() {
	vec4 c = texture(TEXTURE, UV) * COLOR;
	float l = dot(c.rgb, vec3(0.299, 0.587, 0.114));
	COLOR = vec4(mix(c.rgb, vec3(l), gray), c.a);
}",
	};

	public static void Play(Control front, IReadOnlyList<Vector2> enemies)
	{
		Vector2 size = front.GetViewportRect().Size;
		float W = size.X, H = size.Y;
		Vector2 focus = enemies.Count > 0 ? Avg(enemies) : size * new Vector2(0.68f, 0.5f);
		Node2D root = Fx.Root(front, Vector2.Zero, "P3AllOut");
		root.TopLevel = true;
		root.GlobalPosition = Vector2.Zero;

		Audio.Sfx.Clip("Snd_FX_Btl_AlloutUI_00", 0.9f);
		Banner(root, W, H);
		Fx.After(root, 0.4, "allout iris", () => { Audio.Sfx.Clip("Btl_Allout_Wipe", 0.8f); Iris(root, W, H); });
		Fx.After(root, 0.85, "allout brawl", () => { Audio.Sfx.Clip("allout_brawl", 0.9f); Brawl(root, focus, W, H); });
		Audio.Sfx.Clip("Snd_FX_Btl_Allout_Kime01", 1.0f, 1.7f);
		Fx.After(root, 1.95, "allout white", () => WhiteOut(root, W, H));
		Fx.After(root, 2.15, "allout splat", () =>
		{
			foreach (Vector2 e in enemies.Count > 0 ? enemies : new List<Vector2> { focus })
			{
				Splatter(root, e);
			}
			RedSlash(root, focus, W);
			NGame.Instance?.ScreenShake(ShakeStrength.Strong, ShakeDuration.Short);
		});
		Fx.After(root, 2.2, "allout finish", () => Finish(root, W, H));
		Fx.FreeAfter(root, Duration + 0.1f);
	}

	private static Vector2 Avg(IReadOnlyList<Vector2> pts)
	{
		Vector2 c = Vector2.Zero;
		foreach (Vector2 p in pts) c += p;
		return c / pts.Count;
	}

	// 1. 右下角斜切大横幅
	private static void Banner(Node2D root, float W, float H)
	{
		Node2D b = new() { Position = new Vector2(W, H * 0.78f), Rotation = -0.2f };
		root.AddChild(b);
		float bw = W * 0.62f, bh = H * 0.15f;
		Fx.Poly(b, new[] { new Vector2(-bw, -bh * 0.5f), new Vector2(60f, -bh * 0.5f), new Vector2(60f, bh * 0.5f), new Vector2(-bw + 40f, bh * 0.5f) }, new Color(0.05f, 0.25f, 0.85f, 0.96f));
		Fx.Poly(b, new[] { new Vector2(-bw, -bh * 0.5f), new Vector2(60f, -bh * 0.5f), new Vector2(60f, -bh * 0.5f + 5f), new Vector2(-bw + 1f, -bh * 0.5f + 5f) }, Pal.Cyan, additive: true);
		Typo.Text(b, "ALL-OUT ATTACK", Typo.LatinItalic, (int)(bh * 0.55f), Pal.White, new Vector2(-bw + 60f, -bh * 0.42f), outline: 4, outlineColor: new Color(0.02f, 0.08f, 0.3f));
		Typo.Text(b, "总攻击", Typo.CjkHeavy, (int)(bh * 0.3f), new Color(Pal.Ice, 0.9f), new Vector2(-bw + 66f, bh * 0.12f));
		Fx.Animate(b, 0.75f, t =>
		{
			float time = t * 0.75f;
			float inn = Ease.OutExpo(Mathf.Clamp(time / 0.18f, 0f, 1f));
			float outt = Ease.InCubic(Mathf.Clamp((time - 0.5f) / 0.25f, 0f, 1f));
			b.Position = new Vector2(W + bw * (1f - inn) * 0.6f - W * 0.4f * outt, H * 0.78f);
			b.Modulate = new Color(1f, 1f, 1f, inn);
		});
		Fx.FreeAfter(b, 0.8);
	}

	// 2. 同心圆光圈转场：三层大圆从右下扫过，最后整屏蓝
	private static void Iris(Node2D root, float W, float H)
	{
		Node2D n = new() { Position = new Vector2(W * 1.05f, H * 1.1f) };
		root.AddChild(n);
		Color[] cols = { new(0.45f, 0.85f, 1f), new(0.12f, 0.45f, 0.95f), new(0.04f, 0.16f, 0.62f) };
		var discs = new List<Polygon2D>();
		for (int i = 0; i < 3; i++)
		{
			discs.Add(Fx.Poly(n, Fx.Circle(10f, 96), cols[i]));
		}
		Line2D rim = Fx.Line(n, Fx.Circle(10f, 96), new Color(1f, 1f, 1f, 0.8f), 4f, closed: true);
		float R = new Vector2(W, H).Length() * 1.3f;
		Fx.Animate(n, 0.5f, t =>
		{
			for (int i = 0; i < 3; i++)
			{
				float u = Ease.OutCubic(Mathf.Clamp((t - 0.12f * i) / 0.64f, 0f, 1f));
				discs[i].Scale = Vector2.One * (R * u / 10f);
			}
			rim.Scale = discs[0].Scale * 1.01f;
		});
		Fx.After(n, 0.5, "iris hold", () =>
		{
			Fx.Animate(n, 0.35f, t => n.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(t) * 0.55f));
		});
		Fx.FreeAfter(n, 1.12);
	}

	// 3. 乱斗烟尘：蓝白烟团 + 四芒星 + 斩击线（地面一层深蓝底，像原版的蓝色舞台）
	private static void Brawl(Node2D root, Vector2 focus, float W, float H)
	{
		ColorRect stage = new() { Size = new Vector2(W, H), Color = new Color(0.03f, 0.12f, 0.45f, 0.72f), MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(stage);
		Node2D cloud = new() { Position = focus };
		root.AddChild(cloud);
		var puffs = new List<(Sprite2D S, Vector2 Base, float Ph)>();
		for (int i = 0; i < 26; i++)
		{
			Vector2 p = new(Fx.Rand(-W * 0.26f, W * 0.26f), Fx.Rand(-H * 0.2f, H * 0.18f));
			Sprite2D s = new() { Texture = Fx.SoftDot, Position = p, Scale = Vector2.One * Fx.Rand(3.5f, 6.5f), Modulate = new Color(0.85f, 0.93f, 1f, 0.95f) };
			cloud.AddChild(s);
			puffs.Add((s, s.Scale, Fx.Rand(0f, Mathf.Tau)));
		}
		Fx.Animate(cloud, 1.15f, t =>
		{
			float time = t * 1.15f;
			float inn = Ease.OutBack(Mathf.Clamp(time / 0.2f, 0f, 1f));
			foreach (var (s, b, ph) in puffs)
			{
				s.Scale = b * inn * (1f + 0.12f * Mathf.Sin(time * 22f + ph));
			}
			cloud.Position = focus + new Vector2(8f * Mathf.Sin(time * 31f), 6f * Mathf.Cos(time * 27f));
		});
		for (int i = 0; i < 14; i++)
		{
			float at = 0.12f + 0.07f * i;
			Fx.After(cloud, at, "brawl star", () =>
			{
				Vector2 p = focus + new Vector2(Fx.Rand(-W * 0.22f, W * 0.22f), Fx.Rand(-H * 0.16f, H * 0.14f));
				Node2D st = Fx.Root(root.GetParent<Control>(), p, "P3BrawlStar");
				Fx.Poly(st, Fx.Star4(Fx.Rand(40f, 80f), 0.12f), Pal.White, additive: true);
				Fx.Animate(st, 0.18f, t => { st.Scale = Vector2.One * Mathf.Sin(t * Mathf.Pi); st.Rotation = t * 0.6f; });
				Fx.FreeAfter(st, 0.2);
				if (Fx.Rng.Next(2) == 0)
				{
					Vector2 d = Vector2.Right.Rotated(Fx.Rand(0f, Mathf.Pi));
					Node2D sl = Fx.Root(root.GetParent<Control>(), p, "P3BrawlSlash");
					Polygon2D poly = Fx.Stroke(sl, new[] { -d * 140f, d * 140f }, new Color(1f, 1f, 1f, 0.95f), 5f);
					Fx.Animate(sl, 0.14f, t => sl.Modulate = new Color(1f, 1f, 1f, 1f - t));
					Fx.FreeAfter(sl, 0.16);
				}
			});
		}
		Fx.After(cloud, 1.1, "brawl end", () => { cloud.QueueFree(); stage.QueueFree(); });
	}

	private static void WhiteOut(Node2D root, float W, float H)
	{
		ColorRect w = new() { Size = new Vector2(W, H), Color = new Color(0.97f, 0.98f, 1f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(w);
		Fx.Animate(w, 0.45f, t => w.Color = new Color(0.97f, 0.98f, 1f, t < 0.25f ? t / 0.25f : 1f - (t - 0.25f) / 0.75f));
		Fx.FreeAfter(w, 0.47);
	}

	// 4. 敌人炸开的黑红墨点
	private static void Splatter(Node2D root, Vector2 at)
	{
		Node2D s = new() { Position = at };
		root.AddChild(s);
		for (int i = 0; i < 22; i++)
		{
			Node2D d = new();
			s.AddChild(d);
			float r = Fx.Rand(5f, 18f);
			Fx.Poly(d, Fx.Circle(r, 10), i % 4 == 0 ? new Color(0.8f, 0.05f, 0.12f) : new Color(0.05f, 0.03f, 0.06f));
			Vector2 v = Fx.RandDir() * Fx.Rand(120f, 420f);
			Fx.Animate(d, 0.6f, t =>
			{
				d.Position = v * Ease.OutCubic(t) + new Vector2(0f, 120f * t * t);
				d.Scale = new Vector2(1f + 0.8f * t, 1f - 0.4f * t);
				d.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(t));
			});
		}
		Fx.FreeAfter(s, 0.62);
	}

	private static void RedSlash(Node2D root, Vector2 focus, float W)
	{
		Node2D n = new();
		root.AddChild(n);
		Vector2 a = focus + new Vector2(-W * 0.5f, 80f), b = focus + new Vector2(W * 0.5f, -60f);
		Polygon2D poly = Fx.Poly(n, new Vector2[3], new Color(0.95f, 0.1f, 0.2f, 0.95f), additive: true);
		Fx.Animate(n, 0.4f, t =>
		{
			float g = Ease.OutExpo(Mathf.Clamp(t / 0.3f, 0f, 1f));
			Fx.Reshape(poly, Fx.StrokePts(new[] { a, a.Lerp(b, g) }, 7f * (1f - 0.5f * t), taper: true));
			n.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(Mathf.Clamp((t - 0.6f) / 0.4f, 0f, 1f)));
		});
		Fx.FreeAfter(n, 0.42);
	}

	// 5. 结束立绘
	private static void Finish(Node2D root, float W, float H)
	{
		Node2D f = new();
		root.AddChild(f);
		ColorRect bg = new() { Size = new Vector2(W, H), Color = new Color(0.25f, 0.72f, 0.95f, 0.96f), MouseFilter = Control.MouseFilterEnum.Ignore };
		f.AddChild(bg);
		Node2D ring = new() { Position = new Vector2(W * 0.66f, H * 0.5f) };
		f.AddChild(ring);
		Fx.StrokeClosed(ring, Fx.Circle(H * 0.55f, 96), new Color(0.12f, 0.45f, 0.9f, 0.9f), H * 0.09f);
		Fx.StrokeClosed(ring, Fx.Circle(H * 0.42f, 96), new Color(1f, 1f, 1f, 0.5f), 3f);
		Node2D word = new() { Position = new Vector2(W * 0.02f, H * 0.62f), Rotation = -0.12f, Skew = -0.25f };
		f.AddChild(word);
		Typo.Text(word, "MAJESTIC", Typo.LatinItalic, (int)(H * 0.3f), new Color(0.03f, 0.08f, 0.3f, 0.92f), Vector2.Zero);
		Sprite2D hero = new() { Texture = ResourceLoader.Load("res://p3makoto/ui/victory_portrait.png") as Texture2D, Material = new ShaderMaterial { Shader = Gray } };
		f.AddChild(hero);
		float hs = hero.Texture != null ? H * 1.15f / hero.Texture.GetHeight() : 1f;
		hero.Scale = Vector2.One * hs;
		Vector2 heroAt = new(W * 0.68f, H * 0.6f);
		Node2D text = new() { Position = new Vector2(W * 0.05f, H * 0.12f) };
		f.AddChild(text);
		int fs = (int)(H * 0.075f);
		Typo.Text(text, "DONE", Typo.LatinItalic, fs, Pal.White, new Vector2(0f, 0f), outline: 4, outlineColor: new Color(0.03f, 0.08f, 0.3f));
		Typo.Text(text, "AND", Typo.LatinItalic, fs, Pal.White, new Vector2(fs * 0.4f, fs * 0.9f), outline: 4, outlineColor: new Color(0.03f, 0.08f, 0.3f));
		Typo.Text(text, "DUSTED.", Typo.LatinItalic, fs, Pal.White, new Vector2(0f, fs * 1.8f), outline: 4, outlineColor: new Color(0.03f, 0.08f, 0.3f));
		ShaderMaterial gm = (ShaderMaterial)hero.Material;
		const float total = 1.5f;
		Fx.Animate(f, total, t =>
		{
			float time = t * total;
			float inn = Ease.OutExpo(Mathf.Clamp(time / 0.25f, 0f, 1f));
			hero.Position = heroAt + new Vector2(W * 0.25f * (1f - inn) - 20f * time, 0f);
			ring.Rotation = time * 0.2f;
			ring.Scale = Vector2.One * (0.85f + 0.15f * inn);
			word.Position = new Vector2(W * 0.02f - 160f * (1f - inn) + 30f * time, H * 0.62f);
			text.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp((time - 0.2f) / 0.12f, 0f, 1f));
			text.Scale = Vector2.One * (1f + 0.4f * (1f - Ease.OutBack(Mathf.Clamp((time - 0.2f) / 0.15f, 0f, 1f))));
			float g = Mathf.Clamp((time - 1.0f) / 0.2f, 0f, 1f);
			gm.SetShaderParameter("gray", g);
			bg.Color = new Color(Mathf.Lerp(0.25f, 0.7f, g), Mathf.Lerp(0.72f, 0.72f, g), Mathf.Lerp(0.95f, 0.74f, g), 0.96f);
			f.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(Mathf.Clamp((time - 1.25f) / 0.25f, 0f, 1f)));
		});
		Fx.FreeAfter(f, total + 0.05f);
	}
}
