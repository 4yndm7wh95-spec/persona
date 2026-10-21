using System.Collections.Generic;
using System.Linq;
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
		Fx.StrokeClosed(h, RectPts(new Rect2(-55f * s, -195f * s, 110f * s, 48f * s)), Pal.White, 3f);
		return h;
	}

	// ══════════════════ 锻造系 ══════════════════

	/// <summary>
	/// 铸剑者（华丽版，约 1.9s）：
	/// 0.0 人物身旁地面张开一座橙金色的"光之砧"法阵（压扁），身后升起锻炉之火（ForgeFire，back 层）；
	///     熔融的火星从四周被吸进砧心，凝成一块烧红的剑胚（由短到长、带热浪涟漪）；
	/// 0.35 / 0.6 / 0.85 光之锤三连锤：每锤火花喷泉 + 冲击环 + 剑身上一排符文亮一下，剑胚被锤长、由红转白；
	/// 1.1 第四锤重砸（锤更大、强震屏、法阵炸开），剑身一道白光从剑柄扫到剑尖；
	/// 1.2 淬火：蒸汽喷涌、剑变成 P3 的青蓝，冰晶般的十字星闪；
	/// 1.45 新剑立起、转向，化作一道青光飞进人物手里（被吸收的闪光 + 小冲击环）。
	/// </summary>
	public static void TheSmith(Control back, Control front, Vector2 center, float facing = 1f)
	{
		Vector2 at = center + FxSpace.Vector(front, new Vector2(380f * facing, 50f));       // 剑胚中心：离人物足够远，法阵不碰到人物脚下
		Vector2 floor = at + FxSpace.Vector(front, new Vector2(0f, 95f));
		Color hot = new(1f, 0.5f, 0.12f), gold = new(1f, 0.82f, 0.4f);
		const float total = 1.9f;

		// 光之砧：地面法阵（橙金 → 淬火后变青）
		Node2D anvil = Fx.Root(back, floor, "P3SmithAnvil");
		ColorRect circle = Fx.ShaderQuad(anvil, Shaders.MagicCircle, 360f, 0.3f);
		Fx.SetParam(circle, "tint", gold);
		Fx.SetParam(circle, "spin", 0.7f);
		// 锻炉之火：剑胚后面
		Node2D fireRoot = Fx.Root(back, floor, "P3SmithFire");
		ColorRect fire = new() { Size = new Vector2(300f, 380f), Position = new Vector2(-150f, -370f), MouseFilter = Control.MouseFilterEnum.Ignore, Material = new ShaderMaterial { Shader = Shaders.ForgeFire } };
		fireRoot.AddChild(fire);
		Fx.SetParam(fire, "height", 0f);

		// 剑胚
		Node2D billet = Fx.Root(front, at, "P3SmithBillet");
		Sprite2D heat = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(hot, 0f), Scale = new Vector2(5f, 2.2f) };
		billet.AddChild(heat);
		Node2D sw = Sword(billet, 190f, hot);
		sw.Position = new Vector2(-80f, 0f);
		// 剑身上的符文（锤一下亮一下）
		var runes = new List<Polygon2D>();
		for (int i = 0; i < 5; i++)
		{
			runes.Add(Fx.Poly(sw, Fx.Star4(7f, 0.3f), new Color(Pal.Ice, 0f), additive: true));
			runes[i].Position = new Vector2(30f + i * 26f, 0f);
		}
		// 扫过剑身的白光
		Polygon2D sweep = Fx.Poly(sw, RectPts(new Rect2(-10f, -14f, 20f, 28f)), new Color(1f, 1f, 1f, 0f), additive: true);
		billet.Rotation = 0.04f;

		// 锤柄握点在剑胚右侧：锤头从右上方抡下砸在剑胚上，永远不会扫到人物那一侧
		Node2D hamRoot = Fx.Root(front, at + FxSpace.Vector(front, new Vector2(200f * facing, -30f)), "P3SmithHammer");
		Node2D ham = Hammer(hamRoot, 0.9f, Pal.Ice);
		Sprite2D hamGlow = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(Pal.Cyan, 0.45f), Scale = Vector2.One * 2.5f, Position = new Vector2(0f, -170f) };
		ham.AddChild(hamGlow);

		// 熔融火星被吸进砧心
		P3Fx.Gather(front, at, hot, 420f, 0.3f);
		for (int i = 0; i < 14; i++)
		{
			float ang = Fx.Rand(0f, Mathf.Tau);
			Vector2 from = at + Vector2.Right.Rotated(ang) * Fx.Rand(180f, 280f);
			Node2D m = Fx.Root(front, from, "P3SmithMote");
			m.AddChild(new Sprite2D { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = hot, Scale = Vector2.One * Fx.Rand(0.35f, 0.6f) });
			float delay = Fx.Rand(0f, 0.12f);
			Fx.Animate(m, 0.3f, t =>
			{
				float e = Ease.InCubic(Mathf.Clamp((t * 0.3f - delay) / 0.18f, 0f, 1f));
				m.GlobalPosition = from.Lerp(at, e);
				m.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Max(e - 0.8f, 0f) * 5f);
			});
			Fx.FreeAfter(m, 0.32);
		}
		P3Fx.Ripple(back, at + FxSpace.Vector(front, new Vector2(0f, -40f)), 360f, 1.2f, 0.015f);

		float[] blows = { 0.35f, 0.6f, 0.85f, 1.1f };
		for (int i = 0; i < blows.Length; i++)
		{
			int k = i;
			bool big = k == 3;
			Fx.After(billet, blows[i], "smith blow", () =>
			{
				Vector2 hit = at + FxSpace.Vector(front, new Vector2((big ? 0f : 10f + 15f * k) * facing, -20f));
				Node2D sp = Fx.Root(front, hit, "P3SmithSparks");
				Fx.Sparks(sp, hot, big ? 80 : 45, 220f, big ? 760f : 540f, 0.55f, gravity: 1000f, scaleMin: 0.08f, scaleMax: 0.28f,
					ramp: new[] { new Color(1f, 0.97f, 0.8f), new Color(1f, 0.6f, 0.15f), new Color(0.6f, 0.1f, 0.05f, 0f) });
				Fx.FreeAfter(sp, 0.7);
				P3Fx.Flash(front, hit, big ? Pal.White : gold, big ? 300f : 180f, big ? 0.22f : 0.14f);
				P3Fx.Shockwave(back, hit, big ? Pal.Ice : gold, big ? 620f : 360f, 0.35f);
				P3Fx.Shockwave(back, floor, hot, big ? 520f : 300f, 0.3f, squashY: 0.3f);
				if (big)
				{
					P3Fx.SpeedLines(back, hit, gold, 18, 90f, 420f, 0.35f);
				}
				Shake(big ? ShakeStrength.Medium : ShakeStrength.Weak);
				Audio.Sfx.Play(Audio.Snd.Anvil, big ? 1.0f : 0.75f, pitch: big ? 0.88f : 1f + 0.04f * k);   // 每一锤：打铁声
			});
		}

		Fx.Animate(hamRoot, 1.25f, t =>
		{
			float time = t * 1.25f;
			// 每一锤：抬起（0.6）→ 砸下（0.4，加速）
			const float strike = -1.45f;                    // 锤头水平朝左、正好落在剑胚上
			float swing = time > 1.1f ? strike : 0.25f;
			for (int i = 0; i < blows.Length; i++)
			{
				float s0 = blows[i] - 0.25f;
				if (time >= s0 && time <= blows[i] + 0.05f)
				{
					float ph = (time - s0) / 0.25f;
					float lift = i == 3 ? 0.75f : 0.25f;          // 向右上方抬起（第四锤抬得更高）
					swing = ph < 0.55f ? Mathf.Lerp(strike, lift, Ease.OutCubic(ph / 0.55f)) : Mathf.Lerp(lift, strike, Ease.InCubic(Mathf.Clamp((ph - 0.55f) / 0.45f, 0f, 1f)));
				}
			}
			hamRoot.Rotation = swing * facing;
			hamRoot.Scale = new Vector2(facing, 1f) * (time > 0.85f ? 1.25f : 1f);
			hamRoot.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp((time - 0.08f) / 0.08f, 0f, 1f) * (1f - Mathf.Clamp((time - 1.12f) / 0.12f, 0f, 1f)));
		});

		Vector2 hand = center + FxSpace.Vector(front, new Vector2(-10f * facing, 20f));
		Fx.Animate(billet, total, t =>
		{
			float time = t * total;
			float form = Ease.OutExpo(Mathf.Clamp((time - 0.12f) / 0.2f, 0f, 1f));
			int done = 0;
			foreach (float b in blows) { if (time >= b) done++; }
			float len = 0.55f + 0.45f * Mathf.Min(done / 3f, 1f);
			sw.Scale = new Vector2(len * form, form);
			float heatT = Mathf.Clamp(time / 1.0f, 0f, 1f), q = Ease.OutCubic(Mathf.Clamp((time - 1.2f) / 0.2f, 0f, 1f));
			Color c = hot.Lerp(new Color(1f, 0.95f, 0.8f), heatT).Lerp(new Color(0.6f, 0.88f, 1f), q);
			sw.Modulate = c;
			heat.Modulate = new Color(hot.Lerp(Pal.Cyan, q), 0.75f * form * (1f - 0.5f * q));
			// 符文：每锤后亮起并衰减；淬火后常亮
			for (int i = 0; i < runes.Count; i++)
			{
				float lit = 0f;
				foreach (float b in blows) { lit = Mathf.Max(lit, Mathf.Clamp(1f - Mathf.Abs(time - b - 0.03f * i) / 0.12f, 0f, 1f)); }
				runes[i].Color = new Color(Pal.Ice, Mathf.Max(lit, q * 0.8f));
				runes[i].Rotation = time * 3f;
			}
			// 第四锤后白光扫过剑身
			float sw0 = Mathf.Clamp((time - 1.12f) / 0.18f, 0f, 1f);
			sweep.Position = new Vector2(Mathf.Lerp(-30f, 200f, sw0), 0f);
			sweep.Color = new Color(1f, 1f, 1f, sw0 > 0f && sw0 < 1f ? 0.9f : 0f);
			// 立起、飞进人物手里
			float rise = Ease.OutBack(Mathf.Clamp((time - 1.42f) / 0.16f, 0f, 1f));
			float fly = Ease.InCubic(Mathf.Clamp((time - 1.58f) / 0.2f, 0f, 1f));
			billet.Rotation = (Mathf.Lerp(0.04f, -Mathf.Pi / 2f, rise) + fly * 1.2f) * facing;
			billet.GlobalPosition = (at + FxSpace.Vector(front, new Vector2(0f, -60f * rise))).Lerp(hand, fly);
			billet.Scale = new Vector2(facing, 1f) * (1f - 0.5f * fly);
			billet.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(time / 0.12f, 0f, 1f));

			float fh = time < 0.05f ? 0f : time < 1.15f ? Ease.OutCubic(Mathf.Clamp((time - 0.05f) / 0.3f, 0f, 1f)) : 1f - Ease.InCubic(Mathf.Clamp((time - 1.15f) / 0.3f, 0f, 1f));
			Fx.SetParam(fire, "height", 0.1f + 0.9f * fh);
			Fx.SetParam(fire, "alpha", Mathf.Clamp(fh * 1.4f, 0f, 1f) * 0.85f);
			float ain = Ease.OutExpo(Mathf.Clamp(time / 0.25f, 0f, 1f)), aout = Ease.InCubic(Mathf.Clamp((time - 1.45f) / 0.35f, 0f, 1f));
			anvil.Scale = Vector2.One * (0.5f + 0.5f * ain + (time > 1.1f ? 0.25f * Ease.OutExpo(Mathf.Clamp((time - 1.1f) / 0.25f, 0f, 1f)) : 0f));
			Fx.SetParam(circle, "tint", gold.Lerp(Pal.Cyan, q));
			Fx.SetParam(circle, "alpha", ain * (1f - aout));
		});
		Fx.After(billet, 1.2, "smith quench", () =>
		{
			Node2D steam = Fx.Root(front, at + FxSpace.Vector(front, new Vector2(20f, -10f)), "P3SmithSteam");
			GpuParticles2D e = Fx.Sparks(steam, Pal.White, 40, 60f, 220f, 0.8f, gravity: -260f, scaleMin: 0.4f, scaleMax: 1.0f,
				ramp: new[] { new Color(1f, 1f, 1f, 0.7f), new Color(0.75f, 0.9f, 1f, 0f) });
			((ParticleProcessMaterial)e.ProcessMaterial).EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
			((ParticleProcessMaterial)e.ProcessMaterial).EmissionBoxExtents = new Vector3(110f, 8f, 0f);
			Fx.FreeAfter(steam, 1.0);
			Node2D star = Fx.Root(front, at + FxSpace.Vector(front, new Vector2(90f, 0f)), "P3SmithGlint");
			Fx.Poly(star, Fx.Star4(70f, 0.08f), Pal.White, additive: true);
			Fx.Animate(star, 0.3f, t => { star.Scale = Vector2.One * Mathf.Sin(t * Mathf.Pi); star.Rotation = t * 0.8f; });
			Fx.FreeAfter(star, 0.32);
			P3Fx.Flash(front, at, Pal.Ice, 220f, 0.18f);
			Audio.Sfx.Play(Audio.Snd.Weak, 0.55f, pitch: 0.9f);
		});
		Fx.After(billet, 1.78, "smith absorb", () =>
		{
			P3Fx.Flash(front, hand, Pal.Ice, 200f, 0.15f);
			P3Fx.Shockwave(front, hand, Pal.Cyan, 280f, 0.3f);
			Audio.Sfx.Play(Audio.Snd.StarGain, 0.5f, pitch: 1.1f);
		});
		Fx.FreeAfter(billet, 1.8);
		Fx.FreeAfter(hamRoot, 1.3);
		Fx.FreeAfter(anvil, total + 0.05f);
		Fx.FreeAfter(fireRoot, total + 0.05f);
	}

	/// <summary>
	/// 锻打成型：一柄稍小、锤头烧得橙红的锻锤从左上方抡下砸在敌人头顶（OverheadSmash，和传承之锤同一套抡锤，但是锻炉的颜色），
	/// 砸中时铁砧般的橙色火花向上和两侧喷溅、头顶闪光、脚下地面裂开。
	/// </summary>
	public static void BeatIntoShape(Control front, Vector2 target, Vector2 feet)
	{
		Color hot = new(1f, 0.55f, 0.15f);
		OverheadSmash(front, target, feet, 1.5f, hot, hot, impact =>
		{
			Audio.Sfx.Play(Audio.Snd.Anvil, 0.9f, pitch: 0.92f);            // 锻打成形：砸下的那一刻一声打铁
			Motifs.GroundCrack(front, feet, 1.3f);
			Node2D sp = Fx.Root(front, impact, "P3BeatSparks");
			GpuParticles2D e = Fx.Sparks(sp, hot, 55, 260f, 640f, 0.55f, gravity: 1000f, scaleMin: 0.1f, scaleMax: 0.3f,
				ramp: new[] { Pal.White, hot, new Color(0.5f, 0.1f, 0.05f, 0f) });
			((ParticleProcessMaterial)e.ProcessMaterial).Direction = new Vector3(0f, -1f, 0f);
			((ParticleProcessMaterial)e.ProcessMaterial).Spread = 80f;
			Fx.FreeAfter(sp, 0.7);
			P3Fx.Flash(front, impact, new Color(1f, 0.9f, 0.7f), 300f, 0.16f);
			P3Fx.Shockwave(front, impact, hot, 420f, 0.35f, squashY: 0.55f);
			Shake(ShakeStrength.Medium);
		});
	}

	/// <summary>武器库：人物身后浮现一排扇形排开的光剑（武器架），依次亮起，最后一齐插进他脚下的地面。</summary>
	public static void Arsenal(Control back, Control front, Vector2 feet, Vector2 center, float facing = 1f)
	{
		const int n = 7;
		var swords = new List<Node2D>();
		Node2D root = Fx.Root(back, center + FxSpace.Vector(front, new Vector2(-10f * facing, -40f)), "P3Arsenal");
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
				Vector2 ground = FxSpace.LocalVector(back, feet - root.GlobalPosition) + new Vector2((i - n / 2) * 46f, 0f);
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
	public static void SwordSage(Control back, Control front, Vector2 center, float facing = 1f)
	{
		Node2D root = Fx.Root(front, center, "P3SwordSage");
		root.Scale = new Vector2(facing, 1f);
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
			Vector2 tip = center + FxSpace.Vector(front, new Vector2(200f * facing, -50f));
			P3Fx.Flash(front, tip, Pal.White, 180f, 0.2f);
			P3Fx.SpeedLines(front, tip, Pal.Ice, 12, 30f, 260f, 0.3f);
			Audio.Sfx.Play(Audio.Snd.Weak, 0.6f, pitch: 1.2f);
		});
		Fx.FreeAfter(root, 1.25);
	}

	/// <summary>铸墙：身前的火花聚拢成一块块六边形钢板，咔哒咔哒拼成一面墙，最后整面一亮。</summary>
	public static void Bulwark(Control front, Vector2 center, float facing = 1f)
	{
		Vector2 wall = center + FxSpace.Vector(front, new Vector2(130f * facing, 0f));
		var cells = new List<Vector2>();
		for (int row = -2; row <= 2; row++)
		{
			for (int col = 0; col < 2; col++)
			{
				cells.Add(new Vector2(col * 52f + (row % 2 == 0 ? 0f : 26f), row * 45f));
			}
		}
		Node2D root = Fx.Root(front, wall, "P3Bulwark");
		root.Scale = new Vector2(facing, 1f);
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
			Bang(front, wall + FxSpace.Vector(front, new Vector2(26f * facing, 0f)), Pal.Ice, 0.8f);
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

	/// <summary>P3R 全体目标标记：黄色菱形（黑描边）+ 中间问号，从上方落下弹一下，停留后淡出。</summary>
	public static void TargetDiamond(Control front, Vector2 at, float hold)
	{
		Node2D root = Fx.Root(front, at, "P3TargetDiamond");
		Vector2[] dia = { new(0f, -22f), new(17f, 0f), new(0f, 22f), new(-17f, 0f) };
		Vector2[] big = { new(0f, -27f), new(22f, 0f), new(0f, 27f), new(-22f, 0f) };
		Fx.Poly(root, big, new Color(0.05f, 0.05f, 0.08f, 0.95f));
		Fx.Poly(root, dia, new Color(1f, 0.82f, 0.18f));
		Fx.Poly(root, new[] { new Vector2(0f, -22f), new Vector2(17f, 0f), new Vector2(0f, 0f) }, new Color(1f, 0.95f, 0.6f));
		Typo.Text(root, "?", Typo.LatinItalic, 26, new Color(0.08f, 0.08f, 0.12f), new Vector2(-6f, -18f));
		float total = 0.18f + hold + 0.2f;
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			float inn = Ease.OutBack(Mathf.Clamp(time / 0.18f, 0f, 1f));
			root.GlobalPosition = at + new Vector2(0f, -40f * (1f - inn) + 4f * Mathf.Sin(time * 5f));
			root.Scale = new Vector2(Mathf.Max(0.05f, Mathf.Abs(Mathf.Cos((1f - inn) * Mathf.Pi))), 1f);
			root.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(time / 0.08f, 0f, 1f) * (1f - Mathf.Clamp((time - 0.18f - hold) / 0.2f, 0f, 1f)));
		});
		Fx.FreeAfter(root, total + 0.05);
	}

	/// <summary>P3R 技能名小横幅（左上角）：蓝色斜切条 + 白色技能名 + 小号英文，从左滑入、停留、滑出。</summary>
	public static void SkillBanner(Control front, string name, string latin, float hold)
	{
		front = FxSpace.Screen(front);
		Vector2 size = front.GetViewportRect().Size;
		Vector2 home = new(size.X * 0.03f, size.Y * 0.13f);
		Node2D root = Fx.Root(front, home, "P3SkillBanner");
		root.TopLevel = true;
		const float w = 360f, h = 52f;
		Fx.Poly(root, new[] { new Vector2(0f, 0f), new Vector2(w, 0f), new Vector2(w - 18f, h), new Vector2(-18f, h) }, new Color(0.05f, 0.18f, 0.62f, 0.95f));
		Fx.Poly(root, new[] { new Vector2(0f, 0f), new Vector2(w, 0f), new Vector2(w - 1.5f, 4f), new Vector2(-1.5f, 4f) }, Pal.Cyan, additive: true);
		Fx.Poly(root, new[] { new Vector2(w - 26f, 0f), new Vector2(w + 10f, 0f), new Vector2(w - 8f, h), new Vector2(w - 44f, h) }, new Color(Pal.Cyan, 0.9f));
		Typo.Text(root, name, Typo.CjkHeavy, 30, Pal.White, new Vector2(18f, 6f), outline: 2, outlineColor: new Color(0.02f, 0.06f, 0.2f));
		Typo.Text(root, latin, Typo.LatinItalic, 16, new Color(Pal.Ice, 0.85f), new Vector2(150f, 22f));
		float total = 0.2f + hold + 0.25f;
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			float inn = Ease.OutExpo(Mathf.Clamp(time / 0.2f, 0f, 1f));
			float outt = Ease.InCubic(Mathf.Clamp((time - 0.2f - hold) / 0.25f, 0f, 1f));
			root.GlobalPosition = home + new Vector2(-w * (1f - inn) - w * 0.6f * outt, 0f);
			root.Modulate = new Color(1f, 1f, 1f, inn * (1f - outt));
		});
		Fx.FreeAfter(root, total + 0.05);
	}

	// ══════════════════ 抽牌 / 生成系 ══════════════════

	/// <summary>
	/// 微光：掌心托起一颗光球，光球一明一暗地"呼吸"三下，每亮一次就放出一圈十字星芒；周围一颗接一颗亮起闪烁的小星，
	/// 萤火般的光点向上飘；最后光球分成两道光尾，一道飞向抽牌堆、一道绕回来落进手牌区（抽牌、放回）。
	/// </summary>
	public static void Glimmer(Control back, Control front, Vector2 center, Vector2 above, float facing = 1f)
	{
		// v0.14.20（用户：微光的亮斑挡住人物）：光球放到头顶上方、人物身后的一层，光晕也缩小
		Vector2 at = above + FxSpace.Vector(front, new Vector2(10f * facing, -30f));
		Node2D orb = Fx.Root(back, at, "P3GlimmerOrb");
		Sprite2D halo = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Scale = Vector2.One * 3.2f, Modulate = new Color(Pal.Ice, 0.7f) };
		orb.AddChild(halo);
		Polygon2D core = Fx.Poly(orb, Fx.Circle(16f, 24), Pal.White, additive: true);
		Polygon2D flare = Fx.Poly(orb, Fx.Star4(90f, 0.06f), new Color(Pal.White, 0f), additive: true);
		Polygon2D flare2 = Fx.Poly(orb, Fx.Star4(60f, 0.06f), new Color(Pal.Cyan, 0f), additive: true);
		flare2.Rotation = Mathf.Pi / 4f;
		P3Fx.Flash(back, at, Pal.Ice, 300f, 0.45f);
		const float total = 1.4f;
		Fx.Animate(orb, total, t =>
		{
			float time = t * total;
			float grow = Ease.OutBack(Mathf.Clamp(time / 0.2f, 0f, 1f));
			// 三次呼吸：0.2 / 0.45 / 0.7 秒各亮一下
			float pulse = 0f;
			foreach (float p0 in new[] { 0.2f, 0.45f, 0.7f })
			{
				pulse = Mathf.Max(pulse, Mathf.Clamp(1f - Mathf.Abs(time - p0) / 0.1f, 0f, 1f));
			}
			orb.Scale = Vector2.One * grow * (1f + 0.25f * pulse);
			halo.Modulate = new Color(Pal.Ice, 0.5f + 0.4f * pulse);
			flare.Color = new Color(Pal.White, 0.9f * pulse);
			flare2.Color = new Color(Pal.Cyan, 0.7f * pulse);
			flare.Rotation = time * 0.8f;
			flare2.Rotation = Mathf.Pi / 4f - time * 0.8f;
			orb.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp((time - 0.95f) / 0.1f, 0f, 1f));
		});
		for (int i = 0; i < 3; i++)
		{
			float p0 = 0.2f + 0.25f * i;
			Fx.After(orb, p0, "glimmer pulse", () =>
			{
				P3Fx.Shockwave(front, at, Pal.Ice, 260f, 0.35f);
				Audio.Sfx.Play(Audio.Snd.Weak, 0.35f, pitch: 1.5f + 0.15f * (p0 * 4f));
			});
		}
		// 四周依次亮起的闪烁小星
		for (int i = 0; i < 8; i++)
		{
			float a = Mathf.Tau * i / 8f + 0.3f;
			Vector2 p = center + new Vector2(Mathf.Cos(a) * 170f, Mathf.Sin(a) * 120f - 30f);
			float d = 0.1f + 0.08f * i;
			Fx.After(orb, d, "glimmer twinkle", () =>
			{
				Node2D st = Fx.Root(front, p, "P3GlimmerTwinkle");
				Fx.Poly(st, Fx.Star4(26f, 0.1f), Pal.White, additive: true);
				Fx.Animate(st, 0.4f, t => { st.Scale = Vector2.One * Mathf.Sin(t * Mathf.Pi); st.Rotation = t * 1.2f; });
				Fx.FreeAfter(st, 0.42);
			});
		}
		Node2D motesAt = Fx.Root(front, center, "P3GlimmerMotes");
		GpuParticles2D motes = Fx.Sparks(motesAt, Pal.Ice, 50, 10f, 50f, 1.4f, gravity: -80f, scaleMin: 0.12f, scaleMax: 0.3f, oneShot: false);
		((ParticleProcessMaterial)motes.ProcessMaterial).EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
		((ParticleProcessMaterial)motes.ProcessMaterial).EmissionSphereRadius = 170f;
		Fx.After(motesAt, 0.9, "glimmer motes off", () => motes.Emitting = false);
		Fx.FreeAfter(motesAt, 2.4);
		// 收尾：光球分成两道光尾 —— 一道飞去抽牌堆，一道绕个弯落回手牌区
		Fx.After(orb, 1.0, "glimmer split", () =>
		{
			Vector2 size = front.GetViewportRect().Size;
			Vector2 pile = new(70f, size.Y - 60f), handArea = new(size.X * 0.5f, size.Y - 110f);
			Streak(front, at, at + new Vector2(-200f, -120f), pile);
			Streak(front, at, at + new Vector2(260f, -160f), handArea);
			P3Fx.Flash(front, at, Pal.White, 160f, 0.2f);
		});
		Fx.FreeAfter(orb, total + 0.05);
	}

	private static void Streak(Control layer, Vector2 from, Vector2 ctrl, Vector2 to)
	{
		Node2D root = Fx.Root(layer, Vector2.Zero, "P3GlimmerStreak");
		root.TopLevel = true;
		root.GlobalPosition = Vector2.Zero;
		Line2D l = new() { Width = 7f, Material = Fx.Additive, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round,
			Gradient = new Gradient { Colors = new[] { new Color(Pal.Cyan, 0f), new Color(Pal.White, 0.95f) } } };
		root.AddChild(l);
		Fx.Animate(root, 0.5f, t =>
		{
			float e = Ease.InOutCubic(Mathf.Clamp(t / 0.8f, 0f, 1f));
			var pts = new Vector2[8];
			for (int i = 0; i < 8; i++)
			{
				pts[i] = Bezier(from, ctrl, to, Mathf.Max(0f, e - 0.3f + 0.3f * i / 7f));
			}
			l.Points = pts;
			l.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp((t - 0.8f) / 0.2f, 0f, 1f));
		});
		Fx.After(root, 0.4, "streak land", () => P3Fx.Flash(layer, to, Pal.Ice, 110f, 0.2f));
		Fx.FreeAfter(root, 0.52);
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
		Vector2 Cell((int, int) c) => board.ToGlobal(new Vector2(c.Item1 * cell, c.Item2 * cell));
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
	public static void BundleOfJoy(Control back, Control front, Vector2 head, float facing = 1f)
	{
		// 光蛋放在头顶左上方的空处（和其他物件同一位置），不挡脸
		Vector2 at = head + FxSpace.Vector(front, new Vector2(-200f * facing, -150f));
		at.Y = Mathf.Max(at.Y, 160f);
		at.X = Mathf.Max(at.X, 120f);
		Node2D egg = Fx.Root(front, at, "P3JoyEgg");
		Fx.Poly(egg, Fx.Circle(22f, 32, 1.3f), new Color(1f, 0.95f, 0.85f), additive: true);
		Fx.Line(egg, Fx.Circle(22f, 32, 1.3f), new Color(1f, 0.85f, 0.5f, 0.9f), 2f, closed: true);
		Sprite2D g = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Scale = Vector2.One * 2.6f, Modulate = new Color(1f, 0.85f, 0.5f, 0.5f) };
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
	public static void SpectrumShift(Control back, Control front, Vector2 head, float facing = 1f)
	{
		Vector2 at = head + FxSpace.Vector(front, new Vector2(60f * facing, -150f));
		Node2D root = Fx.Root(back, at, "P3Prism");
		Vector2[] tri = { new(0f, -60f), new(52f, 30f), new(-52f, 30f) };
		// 入射光：抗锯齿多边形（Line2D 的抗锯齿在游戏里不生效，斜线有锯齿）
		Vector2 b0 = new(-420f, -160f), b1 = new(-24f, -10f);
		Polygon2D beamGlow = Fx.Poly(root, Fx.StrokePts(new[] { b0, b1 }, 18f), new Color(Pal.Ice, 0f), additive: true);
		Polygon2D inBeam = Fx.Poly(root, Fx.StrokePts(new[] { b0, b1 }, 6f), new Color(Pal.White, 0f), additive: true);
		Color[] spec = { Pal.Cyan, Pal.Royal, new(0.6f, 0.4f, 1f), new(1f, 0.85f, 0.45f), Pal.Ice };
		var fans = new List<Polygon2D>();
		for (int i = 0; i < spec.Length; i++)
		{
			fans.Add(Fx.Poly(root, new Vector2[3], new Color(spec[i], 0f), additive: true));
		}
		// 棱镜：玻璃体 + 内部高光面 + 抗锯齿描边（画在光束上面）
		Node2D prism = new();
		root.AddChild(prism);
		Fx.Poly(prism, tri, new Color(0.8f, 0.9f, 1f, 0.25f), additive: true);
		Fx.Poly(prism, new[] { new Vector2(0f, -60f), new Vector2(14f, 30f), new Vector2(-52f, 30f) }, new Color(1f, 1f, 1f, 0.12f), additive: true);
		Fx.StrokeClosed(prism, tri, Pal.White, 3f);
		Fx.Stroke(prism, new[] { new Vector2(-4f, -44f), new Vector2(-38f, 22f) }, new Color(1f, 1f, 1f, 0.5f), 1.5f);
		const float total = 1.6f;
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			// 退场分三步、互相错开：入射光先从左往右收回（0.9–1.1）→ 光谱从远端往棱镜收拢（1.0–1.35）→ 棱镜转着缩小、闪一下消失（1.3–1.6）
			float beamOut = Ease.InCubic(Mathf.Clamp((time - 0.9f) / 0.2f, 0f, 1f));
			float fanOut = Ease.InOutCubic(Mathf.Clamp((time - 1.0f) / 0.35f, 0f, 1f));
			float prismOut = Ease.InBackLike(Mathf.Clamp((time - 1.3f) / 0.3f, 0f, 1f));
			Vector2[] beam = { b0.Lerp(b1, beamOut), b1 };
			float ba = Mathf.Clamp(time / 0.15f, 0f, 1f) * (1f - beamOut);
			Fx.Reshape(inBeam, Fx.StrokePts(beam, 6f));
			Fx.Reshape(beamGlow, Fx.StrokePts(beam, 18f));
			inBeam.Color = new Color(Pal.White, 0.9f * ba);
			beamGlow.Color = new Color(Pal.Ice, 0.25f * ba);
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
	public static void Quasar(Control back, Control front, Vector2 head, float facing = 1f)
	{
		Vector2 at = head + FxSpace.Vector(front, new Vector2(-30f * facing, -160f));
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

	/// <summary>
	/// 独白（舞台）：两侧的深蓝天鹅绒幕布滑进画面框住舞台；全场熄灯，"咔"一声，一束聚光灯从顶上打在他身上
	/// （着色器：锥形光柱里有翻涌的烟雾和被照亮的浮尘、地面一圈柔和光斑、灯外压暗）；灯口有一点眩光，
	/// 他身后泛起一圈暖白的轮廓光；独白结束，灯光收窄熄灭，幕布退场。
	/// </summary>
	public static void Monologue(Control back, Control front, Vector2 head, Vector2 feet)
	{
		Vector2 size = front.GetViewportRect().Size;
		const float total = 2.1f;
		Node2D root = Fx.Root(back, Vector2.Zero, "P3Stage");
		ColorRect spot = new() { Position = -size, Size = size * 3f, MouseFilter = Control.MouseFilterEnum.Ignore, Material = new ShaderMaterial { Shader = Shaders.Spotlight } };
		root.AddChild(spot);
		var sm = (ShaderMaterial)spot.Material;
		Vector2 lamp = new(head.X - 30f, 0f);
		sm.SetShaderParameter("aspect", size.X / size.Y);
		sm.SetShaderParameter("lamp", lamp / size);
		sm.SetShaderParameter("floor_pos", (feet + new Vector2(0f, -6f)) / size);
		sm.SetShaderParameter("w_top", 0.018f);
		sm.SetShaderParameter("w_floor", 0.1f);
		sm.SetShaderParameter("open", 0f);
		sm.SetShaderParameter("alpha", 0f);
		// 轮廓光：人物身后一圈暖白柔光（身后层，所以只从人物边缘透出来）
		Node2D rimRoot = Fx.Root(back, (head + feet) / 2f, "P3StageRim");
		Sprite2D rim = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Scale = new Vector2(3.2f, 7f), Modulate = new Color(1f, 0.95f, 0.85f, 0f) };
		rimRoot.AddChild(rim);
		// 灯口眩光
		Node2D flare = Fx.Root(front, lamp + new Vector2(0f, 26f), "P3StageFlare");
		Polygon2D flareStar = Fx.Poly(flare, Fx.Star4(60f, 0.05f), new Color(1f, 0.96f, 0.86f, 0f), additive: true);
		Sprite2D flareGlow = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Scale = Vector2.One * 2.2f, Modulate = new Color(1f, 0.96f, 0.86f, 0f) };
		flare.AddChild(flareGlow);
		// 幕布（前景，压在画面两侧边缘）
		var curtains = new List<ColorRect>();
		float cw = size.X * 0.16f;
		for (int k = 0; k < 2; k++)
		{
			ColorRect c = new() { Size = new Vector2(cw, size.Y + 40f), MouseFilter = Control.MouseFilterEnum.Ignore, Material = new ShaderMaterial { Shader = Shaders.Curtain } };
			((ShaderMaterial)c.Material).SetShaderParameter("side", k == 0 ? 1f : -1f);
			front.AddChild(c);
			curtains.Add(c);
		}
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			float curtainIn = Ease.OutCubic(Mathf.Clamp(time / 0.3f, 0f, 1f)) * (1f - Ease.InCubic(Mathf.Clamp((time - 1.75f) / 0.35f, 0f, 1f)));
			curtains[0].Position = new Vector2(-cw + cw * curtainIn, -20f);
			curtains[1].Position = new Vector2(size.X - cw * curtainIn, -20f);
			float lightOn = time < 0.35f ? 0f : 1f;
			float narrow = 1f - Ease.InCubic(Mathf.Clamp((time - 1.45f) / 0.3f, 0f, 1f));
			sm.SetShaderParameter("alpha", Mathf.Clamp(time / 0.25f, 0f, 1f) * (1f - Mathf.Clamp((time - 1.75f) / 0.3f, 0f, 1f)));
			sm.SetShaderParameter("open", lightOn * Mathf.Max(0.02f, narrow) * (1f + 0.04f * Mathf.Sin(time * 3f)));
			rim.Modulate = new Color(1f, 0.95f, 0.85f, 0.35f * lightOn * narrow);
			float f = lightOn * narrow * (time < 0.45f ? 1.6f : 1f);
			flareStar.Color = new Color(1f, 0.96f, 0.86f, 0.7f * f);
			flareGlow.Modulate = new Color(1f, 0.96f, 0.86f, 0.8f * f);
			flare.Rotation = time * 0.3f;
		});
		Fx.After(root, 0.35, "spot on", () =>
		{
			Audio.Sfx.Play(Audio.Snd.Gunshot, 0.22f, pitch: 0.45f);
			P3Fx.Flash(back, feet + new Vector2(0f, -10f), new Color(1f, 0.95f, 0.85f), 260f, 0.25f);
		});
		Fx.FreeAfter(root, total + 0.05);
		Fx.FreeAfter(rimRoot, total + 0.05);
		Fx.FreeAfter(flare, total + 0.05);
		foreach (ColorRect c in curtains)
		{
			Fx.FreeAfter(c, total + 0.05);
		}
	}

	/// <summary>共鸣：他身前荡出一组声波弧线（音叉般一层层），扫过敌人时敌人身上震一下、泛起涟漪。</summary>
	public static void Resonance(Control back, Control front, Vector2 center, Vector2[] targets, float facing = 1f)
	{
		for (int i = 0; i < 5; i++)
		{
			float d = 0.09f * i;
			Fx.After(front, d, "resonance wave", () =>
			{
				Node2D arc = Fx.Root(front, center + FxSpace.Vector(front, new Vector2(40f * facing, 0f)), "P3ResonanceArc");
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
	public static void Charge(Control back, Control front, Vector2 feet, Vector2 target, float facing = 1f)
	{
		Vector2 size = front.GetViewportRect().Size;
		P3Fx.SpeedLines(front, feet + FxSpace.Vector(front, new Vector2(-60f * facing, -160f)), Pal.Ice, 18, 80f, 700f, 0.4f);
		Node2D wave = Fx.Root(front, feet + FxSpace.Vector(front, new Vector2(60f * facing, 0f)), "P3ChargeWave");
		Polygon2D crest = Fx.Poly(wave, new[] { new Vector2(-30f, 0f), new Vector2(0f, -90f), new Vector2(40f, -40f), new Vector2(60f, 0f) }, new Color(Pal.Ice, 0.8f), additive: true);
		float startX = feet.X + FxSpace.Vector(front, new Vector2(60f * facing, 0f)).X;
		float endX = Mathf.Clamp(target.X + FxSpace.Vector(front, new Vector2(120f * facing, 0f)).X, 40f, size.X - 40f);
		Fx.Animate(wave, 0.5f, t =>
		{
			float e = Ease.OutCubic(t);
			wave.GlobalPosition = new Vector2(Mathf.Lerp(startX, endX, e), feet.Y);
			wave.Scale = new Vector2(facing * (1f + t), 1f - 0.5f * t);
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

	/// <summary>
	/// 星位序列（获得能量）：头顶展开一座星象仪 —— 中心一颗小太阳，三圈带刻度的倾斜轨道慢慢转；
	/// 五颗各不相同的行星（带环的、带卫星的、冰蓝 / 金 / 紫）拖着轨迹沿轨道绕行，越转越慢，最后一颗接一颗滑进同一条斜线；
	/// 对齐的一瞬，一道光线贯穿所有行星（"SYZYGY"），行星依次闪亮；随后能量沿这条线倾泻进他的胸口，星象仪收拢消失。
	/// </summary>
	public static void Alignment(Control back, Control front, Vector2 above, Vector2 chest, float facing = 1f)
	{
		Vector2 at = above + FxSpace.Vector(front, new Vector2(-40f * facing, -10f));
		at.Y = Mathf.Max(at.Y, 190f);
		const float tilt = 0.32f;                          // 轨道面倾角（压扁比）
		Node2D root = Fx.Root(back, at, "P3Orrery");
		// 中心小太阳
		Sprite2D sunGlow = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Scale = Vector2.One * 3.2f, Modulate = new Color(1f, 0.88f, 0.55f, 0.8f) };
		root.AddChild(sunGlow);
		Fx.Poly(root, Fx.Circle(13f, 24), new Color(1f, 0.95f, 0.8f), additive: true);
		// 三圈轨道 + 刻度（一起慢慢转）
		Node2D orbits = new() { Scale = new Vector2(1f, tilt) };
		root.AddChild(orbits);
		float[] radii = { 120f, 190f, 260f };
		foreach (float r in radii)
		{
			Fx.Line(orbits, Fx.Circle(r, 96), new Color(Pal.Ice, 0.4f), 2f, closed: true);
			for (int k = 0; k < 24; k++)
			{
				Vector2 d = Vector2.Right.Rotated(Mathf.Tau * k / 24f);
				Fx.Line(orbits, new[] { d * (r - (k % 6 == 0 ? 10f : 4f)), d * (r + (k % 6 == 0 ? 10f : 4f)) }, new Color(Pal.Ice, k % 6 == 0 ? 0.6f : 0.3f), k % 6 == 0 ? 2f : 1f);
			}
		}
		// 五颗行星：所在轨道、起始角、角速度、半径、颜色、特征
		(int orbit, float a0, float speed, float r, Color c, int kind)[] ps =
		{
			(0, 0.4f, 2.6f, 9f, Pal.Cyan, 0),
			(1, 2.2f, 1.9f, 13f, new Color(1f, 0.82f, 0.45f), 1),       // 带环
			(1, 4.4f, 1.9f, 8f, new Color(0.65f, 0.55f, 1f), 0),
			(2, 1.1f, 1.3f, 15f, Pal.Ice, 2),                          // 带卫星
			(2, 3.6f, 1.3f, 10f, Pal.Royal, 0),
		};
		// 对齐后所在的斜线（从左上指向他的胸口方向）
		Vector2 lineDir = (chest - at).Normalized().Rotated(Mathf.Pi);
		var nodes = new List<Node2D>();
		var trails = new List<Line2D>();
		var trailPts = new List<List<Vector2>>();
		foreach (var p in ps)
		{
			Node2D pl = new();
			root.AddChild(pl);
			Sprite2D g = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Scale = Vector2.One * p.r / 9f, Modulate = new Color(p.c, 0.55f) };
			pl.AddChild(g);
			Fx.Poly(pl, Fx.Circle(p.r, 24), p.c.Darkened(0.35f));
			Fx.Poly(pl, Fx.Circle(p.r * 0.8f, 20), new Color(p.c, 0.9f)).Position = new Vector2(-p.r * 0.18f, -p.r * 0.18f);      // 受光面
			Fx.Poly(pl, Fx.Circle(p.r * 0.3f, 12), new Color(1f, 1f, 1f, 0.8f), additive: true).Position = new Vector2(-p.r * 0.4f, -p.r * 0.4f);
			if (p.kind == 1)
			{
				Node2D ring = new() { Scale = new Vector2(1f, 0.3f), Rotation = -0.3f };
				pl.AddChild(ring);
				Fx.Line(ring, Fx.Circle(p.r * 1.9f, 40), new Color(p.c, 0.85f), 2.5f, closed: true);
			}
			if (p.kind == 2)
			{
				Node2D moon = new() { Name = "Moon" };
				pl.AddChild(moon);
				Fx.Poly(moon, Fx.Circle(4f, 12), Pal.White, additive: true);
			}
			Line2D tr = new() { Width = p.r * 0.9f, Material = Fx.Additive, Gradient = new Gradient { Colors = new[] { new Color(p.c, 0f), new Color(p.c, 0.5f) } } };
			root.AddChild(tr);
			root.MoveChild(tr, 1);
			nodes.Add(pl);
			trails.Add(tr);
			trailPts.Add(new List<Vector2>());
		}
		Line2D beam = Fx.Line(root, new[] { Vector2.Zero, Vector2.Zero }, new Color(Pal.White, 0f), 3f);
		Line2D beamGlow = Fx.Line(root, new[] { Vector2.Zero, Vector2.Zero }, new Color(Pal.Cyan, 0f), 16f);
		Label tag = Typo.Text(root, "SYZYGY", Typo.LatinItalic, 26, new Color(Pal.Ice, 0f), new Vector2(40f, -90f), outline: 4, outlineColor: new Color(0.02f, 0.05f, 0.2f));
		const float alignAt = 0.85f, total = 1.9f;
		float spin = 0f, last = 0f;
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			float dt = time - last;
			last = time;
			float open = Ease.OutBack(Mathf.Clamp(time / 0.25f, 0f, 1f));
			float close = Ease.InCubic(Mathf.Clamp((time - (total - 0.3f)) / 0.3f, 0f, 1f));
			root.Scale = Vector2.One * (open * (1f - 0.4f * close));
			root.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(time / 0.12f, 0f, 1f) * (1f - close));
			orbits.Rotation = time * 0.25f;
			spin += dt * Mathf.Max(0f, 1f - time / alignAt);       // 越转越慢，对齐时停下
			float settle = Ease.InOutCubic(Mathf.Clamp((time - 0.35f) / (alignAt - 0.35f), 0f, 1f));
			for (int i = 0; i < ps.Length; i++)
			{
				var p = ps[i];
				float R = radii[p.orbit];
				float a = p.a0 + spin * p.speed * 2f;
				Vector2 free = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * tilt) * R;
				// 对齐目标：同一条斜线上，按轨道由内到外排开（同一轨道的第二颗放在太阳另一侧）
				float side = i == 2 || i == 4 ? -1f : 1f;
				Vector2 lined = lineDir * (side * (R * 0.85f + i * 6f));
				Vector2 pos = free.Lerp(lined, settle);
				nodes[i].Position = pos;
				Node? m = nodes[i].GetNodeOrNull("Moon");
				if (m is Node2D moon)
				{
					moon.Position = new Vector2(Mathf.Cos(time * 7f), Mathf.Sin(time * 7f) * 0.5f) * p.r * 2.1f;
				}
				var tp = trailPts[i];
				tp.Add(pos);
				if (tp.Count > 10)
				{
					tp.RemoveAt(0);
				}
				trails[i].Points = tp.ToArray();
				trails[i].Modulate = new Color(1f, 1f, 1f, 1f - settle);
			}
			float lit = Mathf.Clamp((time - alignAt) / 0.08f, 0f, 1f) * (1f - Mathf.Clamp((time - 1.45f) / 0.3f, 0f, 1f));
			Vector2 far = lineDir * 330f;
			beam.Points = beamGlow.Points = new[] { far, -far };
			beam.DefaultColor = new Color(Pal.White, 0.95f * lit);
			beamGlow.DefaultColor = new Color(Pal.Cyan, 0.35f * lit);
			tag.Modulate = new Color(1f, 1f, 1f, lit);
		});
		// 对齐：行星由内到外依次闪亮，音调递升
		Fx.After(root, alignAt, "syzygy", () =>
		{
			P3Fx.Flash(back, at, Pal.White, 300f, 0.25f);
			Shake(ShakeStrength.Weak);
			for (int i = 0; i < nodes.Count; i++)
			{
				Node2D n = nodes[i];
				int k = i;
				Fx.After(root, 0.05 * k, "planet ping", () =>
				{
					if (GodotObject.IsInstanceValid(n))
					{
						P3Fx.Flash(front, n.GlobalPosition, Pal.White, 90f, 0.18f);
						P3Fx.Shockwave(front, n.GlobalPosition, Pal.Ice, 120f, 0.25f);
					}
					Audio.Sfx.Play(Audio.Snd.Weak, 0.35f, pitch: 1.0f + 0.12f * k);
				});
			}
		});
		// 能量沿斜线倾泻进胸口
		Fx.After(root, alignAt + 0.3, "alignment pour", () =>
		{
			Vector2 from = root.GlobalPosition;
			Node2D pour = Fx.Root(front, Vector2.Zero, "P3AlignPour");
			pour.TopLevel = true;
			pour.GlobalPosition = Vector2.Zero;
			Line2D l = new() { Width = 14f, Material = Fx.Additive, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round,
				Gradient = new Gradient { Colors = new[] { new Color(Pal.Cyan, 0.2f), new Color(Pal.White, 0.95f) } } };
			pour.AddChild(l);
			Fx.Animate(pour, 0.35f, t =>
			{
				float e = Ease.InCubic(Mathf.Clamp(t / 0.7f, 0f, 1f));
				l.Points = new[] { from.Lerp(chest, Mathf.Max(0f, e - 0.4f)), from.Lerp(chest, e) };
				l.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp((t - 0.7f) / 0.3f, 0f, 1f));
			});
			Fx.After(pour, 0.25, "alignment in", () =>
			{
				P3Fx.Flash(front, chest, new Color(1f, 0.88f, 0.55f), 240f, 0.25f);
				P3Fx.Shockwave(front, chest, Pal.Ice, 320f, 0.35f);
				Node2D sp = Fx.Root(front, chest, "P3AlignSparks");
				Fx.Sparks(sp, Pal.Ice, 30, 120f, 340f, 0.5f, scaleMin: 0.1f, scaleMax: 0.25f);
				Fx.FreeAfter(sp, 0.6);
				Audio.Sfx.Play(Audio.Snd.StarGain, 0.8f);
			});
			Fx.FreeAfter(pour, 0.4);
		});
		Fx.FreeAfter(root, total + 0.05);
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

	/// <summary>
	/// 暗淡蓝点（旅行者号那张照片）：画面压暗、带胶片颗粒，从画外左上方斜射进几道散射光带（宽窄不一、带色散），
	/// 最亮的一道正好穿过一颗极小的淡蓝色光点；一个取景框从大到小收拢锁定它，旁边浮出 "PALE BLUE DOT" 和距离读数；
	/// 远处星星一颗颗亮起。最后光带先退去，蓝点独自闪两下熄灭。
	/// </summary>
	public static void PaleBlueDot(Control back, Control front, Vector2 above, float facing = 1f)
	{
		Vector2 size = front.GetViewportRect().Size;
		Vector2 dotAt = above + FxSpace.Vector(front, new Vector2(220f * facing, -30f));
		dotAt.Y = Mathf.Max(dotAt.Y, 160f);
		Node2D root = Fx.Root(back, Vector2.Zero, "P3PaleBlueDot");
		ColorRect rays = new() { Position = -size, Size = size * 3f, MouseFilter = Control.MouseFilterEnum.Ignore, Material = new ShaderMaterial { Shader = Shaders.PaleRays } };
		root.AddChild(rays);
		var mat = (ShaderMaterial)rays.Material;
		mat.SetShaderParameter("aspect", size.X / size.Y);
		mat.SetShaderParameter("alpha", 0f);
		// 远处的星：小点一颗颗亮起、闪烁
		var stars = new List<Polygon2D>();
		for (int i = 0; i < 40; i++)
		{
			Polygon2D st = Fx.Poly(root, Fx.Circle(Fx.Rand(1f, 2.4f), 6), new Color(0.8f, 0.9f, 1f, 0f), additive: true);
			st.Position = new Vector2(Fx.Rand(0f, size.X), Fx.Rand(0f, size.Y * 0.7f));
			stars.Add(st);
		}
		// 蓝点 + 柔光
		Node2D dot = Fx.Root(front, dotAt, "P3BlueDot");
		Sprite2D halo = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Scale = Vector2.One * 0.9f, Modulate = new Color(0.5f, 0.8f, 1f, 0f) };
		dot.AddChild(halo);
		Polygon2D core = Fx.Poly(dot, Fx.Circle(3.5f, 12), new Color(0.6f, 0.85f, 1f, 0f), additive: true);
		// 取景框：四角括号 + 细圆环，从大到小收拢
		Node2D frame = Fx.Root(front, dotAt, "P3DotFrame");
		for (int k = 0; k < 4; k++)
		{
			Node2D corner = new() { Rotation = k * Mathf.Pi / 2f };
			frame.AddChild(corner);
			Fx.Line(corner, new[] { new Vector2(-40f, -26f), new Vector2(-40f, -40f), new Vector2(-26f, -40f) }, new Color(Pal.Ice, 0.9f), 2f, additive: false);
		}
		Line2D ring = Fx.Line(frame, Fx.Circle(18f, 32), new Color(Pal.Ice, 0.6f), 1.2f, closed: true);
		Label title = Typo.Text(front, "PALE BLUE DOT", Typo.LatinItalic, 22, new Color(Pal.Ice, 0f), dotAt + new Vector2(34f, -42f));
		Label dist = Typo.Text(front, "6.0 × 10⁹ km", Typo.Latin, 15, new Color(Pal.Ice, 0f), dotAt + new Vector2(36f, -16f));
		const float total = 2.3f;
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			float a = Mathf.Clamp(time / 0.3f, 0f, 1f);
			float raysOut = Mathf.Clamp((time - 1.55f) / 0.35f, 0f, 1f);
			float allOut = Mathf.Clamp((time - 2.0f) / 0.3f, 0f, 1f);
			mat.SetShaderParameter("alpha", a * (1f - allOut));
			mat.SetShaderParameter("dot_pos", dotAt / size);
			rays.Modulate = new Color(1f, 1f, 1f, 1f - raysOut * 0.6f);
			for (int i = 0; i < stars.Count; i++)
			{
				float on = Mathf.Clamp((time - 0.2f - i * 0.02f) / 0.15f, 0f, 1f);
				stars[i].Color = new Color(0.8f, 0.9f, 1f, on * (0.35f + 0.3f * Mathf.Sin(time * 5f + i)) * (1f - allOut));
			}
			float dotOn = Mathf.Clamp((time - 0.35f) / 0.2f, 0f, 1f);
			float blink = time > 1.9f ? (Mathf.Sin((time - 1.9f) * 30f) > 0f ? 1f : 0.2f) : 1f;
			core.Color = new Color(0.6f, 0.85f, 1f, dotOn * blink * (1f - allOut));
			halo.Modulate = new Color(0.5f, 0.8f, 1f, 0.7f * dotOn * blink * (1f - allOut));
			dot.Scale = Vector2.One * (1f + 0.25f * Mathf.Sin(time * 4f));
			float lockT = Ease.OutCubic(Mathf.Clamp((time - 0.45f) / 0.45f, 0f, 1f));
			frame.Scale = Vector2.One * Mathf.Lerp(4f, 1f, lockT);
			frame.Rotation = (1f - lockT) * 0.6f;
			frame.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp((time - 0.45f) / 0.1f, 0f, 1f) * (1f - raysOut));
			ring.DefaultColor = new Color(Pal.Ice, 0.6f * lockT);
			float txt = Mathf.Clamp((time - 0.95f) / 0.15f, 0f, 1f) * (1f - raysOut);
			title.Modulate = new Color(1f, 1f, 1f, txt);
			dist.Modulate = new Color(1f, 1f, 1f, txt * 0.8f);
			title.VisibleRatio = Mathf.Clamp((time - 0.95f) / 0.3f, 0f, 1f);
		});
		Fx.After(root, 0.9, "dot lock", () => { P3Fx.Flash(front, dotAt, Pal.Ice, 70f, 0.15f); Audio.Sfx.Play(Audio.Snd.Weak, 0.35f, pitch: 1.8f); });
		Fx.FreeAfter(root, total + 0.05);
		Fx.FreeAfter(dot, total + 0.05);
		Fx.FreeAfter(frame, total + 0.05);
		Fx.FreeAfter(title, total + 0.05);
		Fx.FreeAfter(dist, total + 0.05);
	}

	// ══════════════════ 防御系 ══════════════════

	/// <summary>招架：身前两把光剑交叉成 X 格挡，"铛"地一声火花四溅，随后剑身化光散去。</summary>
	public static void Parry(Control front, Vector2 center, float facing = 1f)
	{
		Vector2 at = center + FxSpace.Vector(front, new Vector2(110f * facing, -20f));
		Node2D root = Fx.Root(front, at, "P3Parry");
		root.Scale = new Vector2(facing, 1f);
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
	public static void NeutronAegis(Control back, Control front, Vector2 center, float facing = 1f)
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
			Extras.HexShield(front, center, 1.4f, false, 0.8f, facing);
			Bang(front, center, Pal.Ice, 0.9f);
			Shake(ShakeStrength.Medium);
			Audio.Sfx.Play(Audio.Snd.StarSpend, 0.8f, pitch: 0.6f);
		});
	}

	/// <summary>所向无敌（物理反射 Tetrakarn）：身前竖起一面由大块晶体拼成的护壁，晶面一道高光扫过，最后整面一亮。</summary>
	public static void Tetrakarn(Control front, Vector2 center, float facing = 1f)
	{
		Vector2 at = center + FxSpace.Vector(front, new Vector2(130f * facing, -10f));
		Node2D root = Fx.Root(front, at, "P3Tetrakarn");
		root.Scale = new Vector2(facing, 1f);
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
	public static void Makarakarn(Control front, Vector2 center, float facing = 1f)
	{
		Vector2 at = center + FxSpace.Vector(front, new Vector2(130f * facing, -10f));
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
				root.GlobalPosition = ground + new Vector2(0f, -600f * (1f - drop));
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

	/// <summary>
	/// 抡锤：一柄巨大的光之锤从敌人左上方抡下来（握点在空中，锤头绕握点划一道大弧），锤头底面正正砸在敌人头顶；
	/// 锤头后面拖一道彗尾形的弧形残影；砸中瞬间顿帧 0.07s、锤身被反震弹起一点。onHit(撞击点) 在砸中那一刻调用。
	/// </summary>
	private static void OverheadSmash(Control front, Vector2 target, Vector2 feet, float sc, Color tint, Color glowTint, System.Action<Vector2> onHit)
	{
		// 敌人头顶：中心点往上再走半个身位
		Vector2 impact = new(target.X, target.Y - Mathf.Max(feet.Y - target.Y, 60f) * 0.8f);
		// 锤柄水平朝右时，锤头中心在握点右边 171·sc，锤头底面比握点低 55·sc
		Vector2 grip = impact + new Vector2(-171f * sc, -55f * sc);
		const float a0 = -0.5f, a1 = Mathf.Pi / 2f, tSwing = 0.22f, tStop = 0.07f, tOut = 0.3f;
		Vector2 At(float ang, float r) => grip + new Vector2(Mathf.Sin(ang), -Mathf.Cos(ang)) * r;

		Node2D smearRoot = Fx.Root(front, Vector2.Zero, "P3HammerSmear");
		smearRoot.TopLevel = true;
		smearRoot.GlobalPosition = Vector2.Zero;
		Polygon2D smear = Fx.Poly(smearRoot, new Vector2[3], new Color(glowTint, 0f), additive: true);
		Polygon2D smearCore = Fx.Poly(smearRoot, new Vector2[3], new Color(Pal.White, 0f), additive: true);
		void Sector(Polygon2D poly, float from, float to, float r0, float r1)
		{
			const int n = 16;
			var pts = new Vector2[(n + 1) * 2];
			for (int i = 0; i <= n; i++)
			{
				float ang = Mathf.Lerp(from, to, i / (float)n);
				pts[i] = At(ang, r1);
				pts[(n + 1) * 2 - 1 - i] = At(ang, Mathf.Lerp(r1, r0, i / (float)n));   // 越靠后越窄：彗尾形
			}
			Fx.Reshape(poly, pts);
		}

		Node2D root = Fx.Root(front, grip, "P3Hammer");
		Node2D h = Hammer(root, sc, tint);
		h.AddChild(new Sprite2D { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(glowTint, 0.55f), Scale = new Vector2(3.4f, 2f), Position = new Vector2(0f, -171f * sc) });
		root.Rotation = a0;
		float total = tSwing + tStop + tOut;
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			float sw = Ease.InCubic(Mathf.Clamp(time / tSwing, 0f, 1f));
			float rec = Mathf.Clamp((time - tSwing - tStop) / tOut, 0f, 1f);
			float ang = Mathf.Lerp(a0, a1, sw) - 0.12f * Mathf.Sin(rec * Mathf.Pi) * (1f - rec * 0.5f);
			root.Rotation = ang;
			root.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(time / 0.05f, 0f, 1f) * (1f - Ease.InCubic(Mathf.Clamp((rec - 0.4f) / 0.6f, 0f, 1f))));
			float tail = Mathf.Min(0.9f, ang - a0);
			float sa = time < tSwing ? 0.5f * sw : 0.5f * (1f - Mathf.Clamp((time - tSwing) / 0.15f, 0f, 1f));
			if (tail > 0.02f)
			{
				Sector(smear, ang - tail, ang, 150f * sc, 200f * sc);
				Sector(smearCore, ang - tail * 0.6f, ang, 168f * sc, 192f * sc);
			}
			smear.Color = new Color(glowTint, sa);
			smearCore.Color = new Color(Pal.White, sa * 0.8f);
		});
		Fx.After(root, tSwing, "hammer hit", () => onHit(impact));
		Fx.FreeAfter(root, total + 0.03f);
		Fx.FreeAfter(smearRoot, total + 0.03f);
	}

	/// <summary>传承之锤：巨大的青白光之锤从左上方抡下砸在敌人头顶（OverheadSmash），头顶白闪 + 冲击环，脚下裂地 + 压扁的冲击波，火花向上和两侧溅开，强震屏。</summary>
	public static void HeirloomHammer(Control front, Vector2 target, Vector2 feet)
	{
		OverheadSmash(front, target, feet, 1.9f, Pal.Cyan, Pal.Cyan, impact =>
		{
			P3Fx.Flash(front, impact, Pal.White, 380f, 0.18f);
			P3Fx.Shockwave(front, impact, Pal.Ice, 520f, 0.35f, squashY: 0.55f);
			P3Fx.Shockwave(front, feet, Pal.Cyan, 640f, 0.4f, squashY: 0.25f);
			Motifs.GroundCrack(front, feet, 1.5f);
			Node2D sp = Fx.Root(front, impact, "P3HeirloomSparks");
			GpuParticles2D e = Fx.Sparks(sp, Pal.Ice, 50, 260f, 620f, 0.5f, gravity: 1100f, scaleMin: 0.08f, scaleMax: 0.25f,
				ramp: new[] { Pal.White, Pal.Cyan, new Color(Pal.Cyan, 0f) });
			((ParticleProcessMaterial)e.ProcessMaterial).Direction = new Vector3(0f, -1f, 0f);
			((ParticleProcessMaterial)e.ProcessMaterial).Spread = 75f;
			Fx.FreeAfter(sp, 0.7);
			P3Fx.SpeedLines(front, impact, Pal.Ice, 14, 80f, 320f, 0.3f);
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

	// ═════════ 下砸（v0.14.40，用户：要真的有东西砸下来，并且体现群伤）═════════
	// 一块横跨所有敌人的血红符文石板：出牌时在敌人群上方现身、缓缓下压（地上一整片阴影罩住所有敌人、头顶红色压力线），
	// 伤害结算那一刻（CardFx.OnHit）猛地砸在所有敌人头顶 —— 敌人一起被压扁回弹，石板碎成大块四散落地，
	// 地面一圈贯穿整排敌人的冲击波 + 两侧尘墙 + 每人脚下地裂。石板只落到头顶高度，不挡身体。

	private sealed class Slab
	{
		public Node2D Root = null!;
		public Node2D Shadow = null!;
		public List<MegaCrit.Sts2.Core.Nodes.Combat.NCreature> Targets = new();
		public float Cx, HalfW, ImpactY, GroundY, Y;
		public bool Dropped;
	}

	private static Slab? _slab;
	private static readonly Color SlabTop = new(0.30f, 0.12f, 0.15f, 0.96f);
	private static readonly Color SlabSide = new(0.13f, 0.04f, 0.06f, 0.97f);
	private const float SlabDepth = 92f, SlabThick = 48f;

	/// <summary>石板一个面的四个角（中心 x、顶面后沿 y）：顶面是透视压扁的平行四边形，侧面是它下面的厚度。</summary>
	private static Vector2[] SlabTopFace(float hw) => new[]
	{
		new Vector2(-hw + 70f, -SlabDepth), new Vector2(hw + 70f, -SlabDepth), new Vector2(hw, 0f), new Vector2(-hw, 0f),
	};

	private static Vector2[] SlabFrontFace(float hw) => new[]
	{
		new Vector2(-hw, 0f), new Vector2(hw, 0f), new Vector2(hw, SlabThick), new Vector2(-hw, SlabThick),
	};

	private static Vector2[] SlabSideFace(float hw) => new[]
	{
		new Vector2(hw, 0f), new Vector2(hw + 70f, -SlabDepth), new Vector2(hw + 70f, -SlabDepth + SlabThick), new Vector2(hw, SlabThick),
	};

	/// <summary>出牌（施法抬手）时：石板在敌人群上方现身、缓缓下压，等 CrushUnderImpact 砸下（1.8s 没等到就自己砸）。</summary>
	public static void CrushUnderBegin(Control front, Control back, List<MegaCrit.Sts2.Core.Nodes.Combat.NCreature> targets)
	{
		// v0.14.45（审查：下砸途中保存退出后，旧石板节点已被释放，这里每次都抛异常，下砸特效整局失效）
		if (_slab != null && GodotObject.IsInstanceValid(_slab.Root))
		{
			_slab.Root.QueueFree();
		}
		_slab = null;
		targets = targets.Where(GodotObject.IsInstanceValid).ToList();
		if (targets.Count == 0)
		{
			return;
		}
		float minX = targets.Min(t => P3Makoto.Core.Makoto.Feet(t).X), maxX = targets.Max(t => P3Makoto.Core.Makoto.Feet(t).X);
		float top = targets.Min(t => t.GetTopOfHitbox().Y);
		float ground = targets.Average(t => P3Makoto.Core.Makoto.Feet(t).Y);
		Slab sl = new()
		{
			Targets = targets,
			Cx = (minX + maxX) * 0.5f,
			HalfW = (maxX - minX) * 0.5f + 210f,
			ImpactY = top - SlabThick - 6f,
			GroundY = ground,
		};
		// 不出屏：右端（含透视后沿 70px）和左端都留 20px
		float view = front.GetViewportRect().Size.X;
		sl.HalfW = Mathf.Min(sl.HalfW, (view - 110f) * 0.5f);
		sl.Cx = Mathf.Clamp(sl.Cx, sl.HalfW + 20f, view - sl.HalfW - 90f);
		float startY = Mathf.Min(sl.ImpactY - 260f, 150f);
		sl.Y = startY;
		_slab = sl;
		Audio.Sfx.Clip("swish_heavy_1", 0.45f);

		// 地上阴影：一整片扁椭圆罩住所有敌人，随石板下压变深
		Node2D shadow = Fx.Root(back, new Vector2(sl.Cx, ground), "P3SlabShadow");
		Sprite2D sh = new() { Texture = Fx.SoftDot, Modulate = new Color(0.12f, 0f, 0.03f, 0f), Scale = new Vector2(sl.HalfW * 2.4f, 140f) / Fx.SoftDot.GetWidth() };
		shadow.AddChild(sh);
		sl.Shadow = shadow;

		// 石板本体
		Node2D root = Fx.Root(front, new Vector2(sl.Cx, startY), "P3Slab");
		root.Modulate = new Color(1f, 1f, 1f, 0f);
		sl.Root = root;
		float hw = sl.HalfW;
		Sprite2D under = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Position = new Vector2(0f, SlabThick + 10f), Scale = new Vector2(hw * 2.6f, 120f) / Fx.SoftDot.GetWidth(), Modulate = new Color(Pal.Blood, 0f) };
		root.AddChild(under);
		Fx.Poly(root, SlabSideFace(hw), SlabSide);
		Fx.Poly(root, SlabFrontFace(hw), SlabSide);
		Fx.Poly(root, SlabTopFace(hw), SlabTop);
		// 顶面的符文：压扁的双圆环 + 六边形 + 一圈刻度，沿顶面中心
		Node2D runes = new() { Position = new Vector2(35f, -SlabDepth * 0.5f), Scale = new Vector2(1f, 0.26f) };
		root.AddChild(runes);
		float rr = Mathf.Min(hw * 0.55f, 260f);
		Line2D ring1 = Fx.Line(runes, Fx.Circle(rr, 64), new Color(Pal.Blood, 0f), 7f, closed: true);
		Line2D ring2 = Fx.Line(runes, Fx.Circle(rr * 0.78f, 64), new Color(Pal.Blood, 0f), 3f, closed: true);
		Line2D hex = Fx.Line(runes, Fx.Hexagon(rr * 0.7f), new Color(Pal.Blood, 0f), 4f, closed: true);
		var ticks = new List<Line2D>();
		for (int i = 0; i < 18; i++)
		{
			Vector2 d = Vector2.Right.Rotated(Mathf.Tau * i / 18f);
			ticks.Add(Fx.Line(runes, new[] { d * rr * 0.84f, d * rr * 0.96f }, new Color(Pal.Blood, 0f), 4f));
		}
		// 边缘的发光接缝（顶面前沿 + 侧面中线）
		Line2D seamTop = Fx.Line(root, new[] { new Vector2(-hw, 0f), new Vector2(hw, 0f), new Vector2(hw + 70f, -SlabDepth) }, new Color(1f, 0.45f, 0.45f, 0f), 3f);
		Line2D seamMid = Fx.Line(root, new[] { new Vector2(-hw + 6f, SlabThick * 0.5f), new Vector2(hw - 6f, SlabThick * 0.5f) }, new Color(Pal.Blood, 0f), 2f);

		// 每个敌人头顶的压力线（红色细线往下压）
		foreach (var t in targets)
		{
			Vector2 head = t.GetTopOfHitbox();
			for (int k = 0; k < 5; k++)
			{
				float x = head.X + Fx.Rand(-70f, 70f);
				Node2D streak = Fx.Root(front, new Vector2(x, head.Y - 220f), "P3SlabPress");
				Line2D l = Fx.Line(streak, new[] { Vector2.Zero, new Vector2(0f, Fx.Rand(60f, 120f)) }, new Color(Pal.Blood, 0f), Fx.Rand(2f, 4f));
				float delay = 0.15f + k * 0.12f;
				Fx.Animate(streak, 1.4f, u =>
				{
					float time = u * 1.4f - delay;
					float ph = Mathf.PosMod(time, 0.55f) / 0.55f;
					streak.Position = new Vector2(x, head.Y - 230f + 200f * Ease.InCubic(ph));
					l.DefaultColor = new Color(Pal.Blood, time < 0f || (_slab?.Dropped ?? true) ? 0f : 0.7f * Mathf.Sin(Mathf.Pi * ph));
				});
				Fx.FreeAfter(streak, 1.45);
			}
		}

		float hoverEnd = sl.ImpactY - 150f;
		Fx.Animate(root, 1.8f, u =>
		{
			if (sl.Dropped)
			{
				return;
			}
			float time = u * 1.8f;
			float a = Ease.OutCubic(Mathf.Clamp(time / 0.2f, 0f, 1f));
			root.Modulate = new Color(1f, 1f, 1f, a);
			// 缓缓下压（越来越快），微微晃
			sl.Y = Mathf.Lerp(startY, hoverEnd, Ease.InOutSine(Mathf.Clamp(time / 1.1f, 0f, 1f)));
			root.Position = new Vector2(sl.Cx + 3f * Mathf.Sin(time * 17f) * Mathf.Clamp(time / 0.6f, 0f, 1f), sl.Y);
			root.Rotation = 0.012f * Mathf.Sin(time * 3.1f);
			// 符文依次点亮
			float glow = Mathf.Clamp((time - 0.1f) / 0.4f, 0f, 1f);
			ring1.DefaultColor = new Color(Pal.Blood, 0.9f * glow);
			ring2.DefaultColor = new Color(Pal.Blood, 0.7f * Mathf.Clamp((time - 0.2f) / 0.3f, 0f, 1f));
			hex.DefaultColor = new Color(1f, 0.5f, 0.5f, 0.8f * Mathf.Clamp((time - 0.3f) / 0.3f, 0f, 1f));
			for (int i = 0; i < ticks.Count; i++)
			{
				ticks[i].DefaultColor = new Color(Pal.Blood, Mathf.Clamp((time - 0.15f - i * 0.02f) / 0.1f, 0f, 1f) * (0.6f + 0.4f * Mathf.Sin(time * 9f + i)));
			}
			runes.Rotation = time * 0.6f;
			seamTop.DefaultColor = new Color(1f, 0.45f, 0.45f, 0.8f * glow);
			seamMid.DefaultColor = new Color(Pal.Blood, 0.6f * glow);
			float near = Mathf.Clamp((sl.Y - startY) / Mathf.Max(1f, hoverEnd - startY), 0f, 1f);
			under.Modulate = new Color(Pal.Blood, 0.35f * near * glow);
			sh.Modulate = new Color(0.12f, 0f, 0.03f, 0.25f + 0.4f * near);
			sh.Scale = new Vector2(sl.HalfW * (2.9f - 0.5f * near), 140f) / Fx.SoftDot.GetWidth();
		});
		// 兜底：一直没等到伤害结算就自己砸下（画面不能停在半空）
		Fx.After(front, 1.8, "crush slab failsafe", () =>
		{
			if (_slab == sl && !sl.Dropped)
			{
				CrushUnderImpact(front, true);
			}
		});
	}

	/// <summary>伤害结算那一刻：石板 0.08s 砸到所有敌人头顶 → 碎裂四散 + 冲击波 + 尘墙 + 地裂 + 敌人压扁回弹。</summary>
	public static void CrushUnderImpact(Control front, bool silent = false)
	{
		Slab? sl = _slab;
		if (sl == null || sl.Dropped || !GodotObject.IsInstanceValid(sl.Root))
		{
			if (sl != null && !GodotObject.IsInstanceValid(sl.Root))
			{
				_slab = null;
			}
			return;
		}
		sl.Dropped = true;
		Node2D root = sl.Root;
		float fromY = root.Position.Y;
		root.Modulate = Colors.White;
		Fx.Animate(root, 0.08f, u => root.Position = new Vector2(sl.Cx, Mathf.Lerp(fromY, sl.ImpactY, Ease.InCubic(u))));
		Fx.After(front, 0.08, "crush slab hit", () => Shatter(front, sl, silent));
	}

	private static void Shatter(Control front, Slab sl, bool silent)
	{
		if (_slab == sl)
		{
			_slab = null;
		}
		if (!silent)
		{
			EvokerShot.HitStop(0.08);
			MegaCrit.Sts2.Core.Nodes.NGame.Instance?.ScreenShake(MegaCrit.Sts2.Core.Nodes.Vfx.Utilities.ShakeStrength.Strong, MegaCrit.Sts2.Core.Nodes.Vfx.Utilities.ShakeDuration.Short);
		}
		Audio.Sfx.Clip("crush_gravity", 0.95f);
		float hw = sl.HalfW, cx = sl.Cx, y = sl.ImpactY;
		if (GodotObject.IsInstanceValid(sl.Root))
		{
			sl.Root.QueueFree();
		}
		// 撞击闪光：石板底沿一道横向白红闪
		Node2D flash = Fx.Root(front, new Vector2(cx, y + SlabThick), "P3SlabFlash");
		Sprite2D fl = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Scale = new Vector2(hw * 2.6f, 90f) / Fx.SoftDot.GetWidth(), Modulate = new Color(1f, 0.75f, 0.75f, 0.95f) };
		flash.AddChild(fl);
		Fx.Animate(flash, 0.3f, u => { fl.Modulate = new Color(1f, 0.6f, 0.6f, 0.95f * (1f - Ease.OutCubic(u))); flash.Scale = new Vector2(1f + 0.3f * u, 1f); });
		Fx.FreeAfter(flash, 0.32);
		// 碎块：沿石板宽度切成十几块，各自带一截侧面，往两侧 / 上方崩开再落下、旋转、淡出
		int n = Mathf.Clamp((int)(hw / 45f), 10, 18);
		for (int i = 0; i < n; i++)
		{
			float x0 = -hw + 2f * hw * i / n, x1 = -hw + 2f * hw * (i + 1) / n;
			float jag = Fx.Rand(-14f, 14f);
			Vector2 c0 = new((x0 + x1) * 0.5f, 0f);
			Vector2[] top = { new(x0 + 70f * 0.5f - c0.X + jag, -SlabDepth * 0.5f), new(x1 + 35f - c0.X, -SlabDepth * 0.5f), new(x1 - c0.X, 0f), new(x0 - c0.X + jag * 0.5f, 0f) };
			Vector2[] side = { new(x0 - c0.X + jag * 0.5f, 0f), new(x1 - c0.X, 0f), new(x1 - c0.X, SlabThick), new(x0 - c0.X, SlabThick) };
			Node2D chunk = Fx.Root(front, new Vector2(cx + c0.X, y), "P3SlabChunk");
			Fx.Poly(chunk, side, SlabSide);
			Fx.Poly(chunk, top, SlabTop);
			Fx.Line(chunk, new[] { top[3], top[2] }, new Color(1f, 0.45f, 0.45f, 0.8f), 2f);
			float side01 = (c0.X / hw);
			Vector2 v = new(side01 * Fx.Rand(260f, 520f) + Fx.Rand(-60f, 60f), Fx.Rand(-420f, -180f));
			float spin = Fx.Rand(-5f, 5f), life = Fx.Rand(0.7f, 0.95f);
			Vector2 p0 = chunk.Position;
			float floor = sl.GroundY - 10f;
			Fx.Animate(chunk, life, u =>
			{
				float t = u * life;
				Vector2 p = p0 + v * t + new Vector2(0f, 1500f * t * t);
				if (p.Y > floor) p.Y = floor;
				chunk.Position = p;
				chunk.Rotation = spin * t;
				chunk.Scale = Vector2.One * (1f - 0.35f * u);
				chunk.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(Mathf.Clamp((u - 0.55f) / 0.45f, 0f, 1f)));
			});
			Fx.FreeAfter(chunk, life + 0.02);
		}
		// 碎石屑
		Node2D bits = Fx.Root(front, new Vector2(cx, y + SlabThick * 0.5f), "P3SlabBits");
		Fx.Sparks(bits, new Color(0.45f, 0.2f, 0.22f), 50, 200f, 520f, 0.8f, gravity: 1100f, scaleMin: 0.08f, scaleMax: 0.2f);
		Fx.FreeAfter(bits, 1.0);
		// 地面：贯穿整排敌人的压扁冲击波（两层）+ 地上一道血红闪
		for (int k = 0; k < 2; k++)
		{
			Node2D ring = Fx.Root(front, new Vector2(cx, sl.GroundY), "P3SlabWave");
			Fx.StrokeClosed(ring, Fx.Circle(hw * 0.7f, 64, 0.2f), k == 0 ? new Color(1f, 0.8f, 0.8f, 0.95f) : new Color(Pal.Blood, 0.85f), k == 0 ? 6f : 4f);
			float len = k == 0 ? 0.4f : 0.6f, grow = k == 0 ? 1.0f : 1.6f;
			float delay = 0.04f * k;
			Fx.Animate(ring, len + delay, u =>
			{
				float t = Mathf.Clamp((u * (len + delay) - delay) / len, 0f, 1f);
				ring.Scale = new Vector2(0.5f + grow * Ease.OutExpo(t), 0.5f + grow * Ease.OutExpo(t));
				ring.Modulate = new Color(1f, 1f, 1f, t <= 0f ? 0f : 1f - Ease.InCubic(t));
			});
			Fx.FreeAfter(ring, len + delay + 0.02);
		}
		Node2D groundGlow = Fx.Root(front, new Vector2(cx, sl.GroundY), "P3SlabGroundGlow");
		Sprite2D gg = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Scale = new Vector2(hw * 2.8f, 110f) / Fx.SoftDot.GetWidth(), Modulate = new Color(Pal.Blood, 0.75f) };
		groundGlow.AddChild(gg);
		Fx.Animate(groundGlow, 0.5f, u => gg.Modulate = new Color(Pal.Blood, 0.75f * (1f - Ease.OutCubic(u))));
		Fx.FreeAfter(groundGlow, 0.52);
		// 两侧尘墙：从整排敌人的两端往外翻滚
		foreach (float dir in new[] { -1f, 1f })
		{
			for (int i = 0; i < 9; i++)
			{
				Vector2 start = new(cx + dir * (hw - 120f + Fx.Rand(-40f, 40f)), sl.GroundY + Fx.Rand(-10f, 6f));
				Sprite2D d = new() { Texture = Fx.SoftDot, Position = start, Modulate = new Color(0.6f, 0.5f, 0.5f, 0f) };
				front.AddChild(d);
				Vector2 drift = new(dir * Fx.Rand(120f, 260f), Fx.Rand(-70f, -15f));
				float sz = Fx.Rand(70f, 130f) / Fx.SoftDot.GetWidth(), life = Fx.Rand(0.6f, 0.9f);
				Fx.Animate(d, life, u =>
				{
					d.GlobalPosition = start + drift * Ease.OutCubic(u);
					d.Scale = new Vector2(1.6f, 1f) * sz * (0.6f + 0.9f * u);
					d.Modulate = new Color(0.6f, 0.5f, 0.5f, 0.5f * Ease.Envelope(u, 0.1f, 0.55f));
				});
				Fx.FreeAfter(d, life + 0.02);
			}
		}
		// 每个敌人：石板底到头顶一道红色冲击光柱（个子矮、没被石板直接碰到的也挨了这一下）+ 脚下地裂 + 一起被压扁再回弹（只改显示缩放，结束时还原）
		foreach (var t in sl.Targets)
		{
			if (!GodotObject.IsInstanceValid(t))
			{
				continue;
			}
			Vector2 head = t.GetTopOfHitbox();
			float from = y + SlabThick;
			float len = Mathf.Max(30f, head.Y - from + 40f);
			Node2D beam = Fx.Root(front, new Vector2(head.X, from), "P3SlabBeam");
			Sprite2D bm = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Position = new Vector2(0f, len * 0.5f), Scale = new Vector2(150f, len * 2.2f) / Fx.SoftDot.GetWidth(), Modulate = new Color(1f, 0.35f, 0.4f, 0.85f) };
			beam.AddChild(bm);
			Fx.Animate(beam, 0.28f, u =>
			{
				bm.Modulate = new Color(1f, 0.35f, 0.4f, 0.85f * (1f - Ease.OutCubic(u)));
				beam.Scale = new Vector2(1f - 0.6f * u, 1f);
			});
			Fx.FreeAfter(beam, 0.3);
			Motifs.GroundCrack(front, P3Makoto.Core.Makoto.Feet(t), 1.2f);
			Squash(t);
		}
		if (GodotObject.IsInstanceValid(sl.Shadow))
		{
			Node2D shadow = sl.Shadow;
			Fx.Animate(shadow, 0.4f, u => shadow.Modulate = new Color(1f, 1f, 1f, 1f - u));
			Fx.FreeAfter(shadow, 0.42);
		}
	}

	private static void Squash(MegaCrit.Sts2.Core.Nodes.Combat.NCreature t)
	{
		Node2D? vis = t.Visuals;
		if (vis == null || !GodotObject.IsInstanceValid(vis) || vis.HasMeta("p3_squash"))
		{
			return;
		}
		Vector2 orig = vis.Scale;
		vis.SetMeta("p3_squash", true);
		Fx.Animate(vis, 0.45f, u =>
		{
			// 先压扁（0.06s）再弹簧回弹
			float k = u < 0.13f ? Ease.OutCubic(u / 0.13f) : Mathf.Exp(-6f * (u - 0.13f)) * Mathf.Cos(14f * (u - 0.13f));
			vis.Scale = orig * new Vector2(1f + 0.10f * k, 1f - 0.20f * k);
		}).Finished += () =>
		{
			if (GodotObject.IsInstanceValid(vis))
			{
				vis.Scale = orig;
				vis.RemoveMeta("p3_squash");
			}
		};
	}
}
