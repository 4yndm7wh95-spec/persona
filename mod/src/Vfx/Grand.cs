using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace P3Makoto.Vfx;

/// <summary>
/// v0.10 新特效库（第三部分）：把原来大量套用"切入大字 / 塔罗牌 / 锻造火花 / 洗牌 / 法阵"模板的卡拆开，各给一个独立的大型演出。
/// · AllOutAttack：P3R 总攻击 —— 敌人身上扬起乱斗烟尘、拳脚星光乱闪，最后全屏斜切色带 "ALL-OUT ATTACK" 收尾；
/// · Shout：漫画集中线 + 爆炸对话框大字（"CHARGE!!" "GUARDS!!!" 一类的喊话）；
/// · AnvilForge：半空中一把烧红的剑被锤打几下（每下火花 + 冲击），最后淬火变青、冒白汽、刃光一扫；
/// · BladeFan：身后展开一排幽蓝光剑，瞄准后依次射向敌人（无目标时绕身旋转后收进体内）；
/// · StatBuff：P3R 的 -kaja / -nda 状态变化 —— 一串箭头光纹穿过身体上升 / 下压 + "ATTACK UP" 字样；
/// · Shackle：三道锁链从外圈旋入、勒紧敌人，"咔"一下锁死；
/// · CardCascade：牌从抽牌堆沿弧线飞出、拖着光尾翻转着飞进手牌；
/// · PrismBurst：一块棱镜凝聚、旋转、折射出彩虹光束，碎成几张彩边卡牌飞进手牌（生成无色牌）；
/// · VelvetDoor：天鹅绒房间的蓝门在身后打开，强光和蓝蝶从门里涌出；
/// · FortuneWheel：命运之轮高速旋转、减速、咔哒咔哒停下，指针一闪、金色星爆；
/// · CoinRain：金币从天而降、叮叮落地闪光。
/// 原则同 Bold / Motifs：尺寸大、层次多（预兆 → 爆发 → 余韵）、必有确认感。坐标都是特效层上的全局坐标。
/// </summary>
public static class Grand
{
	// ───────── 工具 ─────────

	private static Vector2 View(Control layer) => layer.GetViewportRect().Size;

	private static Vector2 HandAt(Control layer) => View(layer) * new Vector2(0.5f, 0.92f);

	private static Vector2 DrawPileAt(Control layer) => View(layer) * new Vector2(0.05f, 0.93f);

	private static void Shake(ShakeStrength s, float angle = 0f) => NGame.Instance?.ScreenShake(s, ShakeDuration.Short, angle);

	/// <summary>剑形：剑尖朝 +X，从护手（原点）到剑尖 len。</summary>
	private static Vector2[] BladeShape(float len, float w) => new[]
	{
		new Vector2(0f, -w * 0.5f), new Vector2(len * 0.8f, -w * 0.5f), new Vector2(len, 0f), new Vector2(len * 0.8f, w * 0.5f), new Vector2(0f, w * 0.5f)
	};

	private static Vector2[] Rect(float w, float h) => new[] { new Vector2(-w, -h), new Vector2(w, -h), new Vector2(w, h), new Vector2(-w, h) };

	private static Vector2 Bezier(Vector2 a, Vector2 ctrl, Vector2 b, float t)
	{
		float k = 1f - t;
		return a * (k * k) + ctrl * (2f * k * t) + b * (t * t);
	}

	private static Sprite2D Glow(Node parent, Color tint, Vector2 scale)
	{
		Sprite2D g = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = tint, Scale = scale };
		parent.AddChild(g);
		return g;
	}

	/// <summary>一张小卡牌（深蓝底 + 彩色描边 + 中心星）。</summary>
	private static Node2D MiniCard(Node parent, Color edge, float s = 1f)
	{
		Node2D c = new() { Scale = Vector2.One * s };
		parent.AddChild(c);
		Vector2[] r = Rect(26f, 38f);
		Glow(c, new Color(edge, 0.45f), new Vector2(2.4f, 3.2f));
		Fx.Poly(c, r, new Color(Pal.Abyss, 0.96f));
		Fx.Line(c, r, edge, 3f, closed: true, additive: false);
		Fx.Poly(c, Fx.Star4(15f, 0.3f), Pal.Ice, additive: true);
		return c;
	}

	// ═════════ 总攻击 ═════════

	/// <summary>P3R 总攻击：烟尘乱斗 0.9s → 全屏 "ALL-OUT ATTACK" 收尾。</summary>
	public static void AllOutAttack(Control layer, Vector2 target, float s = 1f)
	{
		Node2D root = Fx.Root(layer, target, "P3AllOut");
		// 烟尘：一团灰蓝色软云翻滚膨胀（普通混合，压住画面），边缘带亮边
		List<Sprite2D> puffs = new();
		for (int i = 0; i < 16; i++)
		{
			Vector2 off = Fx.RandDir() * Fx.Rand(20f, 150f) * s * new Vector2(1.3f, 0.8f);
			Sprite2D p = new() { Texture = Fx.SoftDot, Position = off, Modulate = new Color(0.62f, 0.7f, 0.85f, 0f), Scale = Vector2.One * Fx.Rand(3f, 5.5f) * s };
			root.AddChild(p);
			puffs.Add(p);
		}
		Fx.Animate(root, 1.25f, t =>
		{
			float time = t * 1.25f;
			float a = Ease.Envelope(t, 0.12f, 0.72f);
			for (int i = 0; i < puffs.Count; i++)
			{
				Sprite2D p = puffs[i];
				float wob = Mathf.Sin(time * 9f + i * 1.7f);
				p.Modulate = new Color(0.62f, 0.7f, 0.85f, 0.85f * a);
				p.Scale = Vector2.One * (3f + 2.5f * Ease.OutCubic(Mathf.Clamp(time / 0.3f, 0f, 1f)) + 0.4f * wob) * s * (1f + (i % 3) * 0.25f);
				p.Rotation = time * (i % 2 == 0 ? 2f : -2f);
			}
		});
		// 乱斗：烟里不断闪出星形打击光、短斩线
		for (int k = 0; k < 14; k++)
		{
			double when = 0.08 + k * 0.055;
			Fx.After(root, when, "all-out hit", () =>
			{
				if (!GodotObject.IsInstanceValid(root)) return;
				Vector2 at = root.GlobalPosition + Fx.RandDir() * Fx.Rand(0f, 130f) * s;
				Node2D hit = Fx.Root(layer, at, "P3AllOutHit");
				hit.Rotation = Fx.Rand(0f, Mathf.Tau);
				Fx.Poly(hit, Fx.Star4(Fx.Rand(50f, 90f) * s, 0.14f), Pal.White, additive: true);
				Vector2 d = Fx.RandDir() * 90f * s;
				Fx.Stroke(hit, new[] { -d, d }, new Color(Pal.Cyan, 0.9f), 5f);
				Fx.Animate(hit, 0.16f, t =>
				{
					hit.Scale = Vector2.One * (0.5f + 0.8f * Ease.OutExpo(t));
					hit.Modulate = new Color(1f, 1f, 1f, 1f - t);
				});
				Fx.FreeAfter(hit, 0.18);
				if (Fx.Rng.Next(3) == 0) Shake(ShakeStrength.Weak, Fx.Rand(0f, 360f));
			});
		}
		// 收尾：全屏斜切色带 + 大字
		Fx.After(root, 0.9, "all-out finish", () =>
		{
			Vector2 v = View(layer);
			Node2D band = Fx.Root(layer, new Vector2(v.X * 0.5f, v.Y * 0.42f), "P3AllOutBand");
			band.Rotation = -0.12f;
			float half = v.X * 0.75f;
			Fx.Poly(band, new[] { new Vector2(-half, -95f), new Vector2(half, -95f), new Vector2(half, 95f), new Vector2(-half, 95f) }, new Color(0.02f, 0.05f, 0.2f, 0.92f));
			Fx.Poly(band, new[] { new Vector2(-half, -95f), new Vector2(half, -95f), new Vector2(half, -86f), new Vector2(-half, -86f) }, Pal.Cyan, additive: true);
			Fx.Poly(band, new[] { new Vector2(-half, 86f), new Vector2(half, 86f), new Vector2(half, 95f), new Vector2(-half, 95f) }, Pal.Blood);
			Label text = Typo.Text(band, "ALL-OUT ATTACK", Typo.LatinItalic, 150, Pal.White, new Vector2(-560f, -92f), outline: 6, outlineColor: Pal.Abyss, shadow: new Color(Pal.Blood, 0.95f), shadowOffset: new Vector2(10f, 8f));
			Fx.Animate(band, 0.95f, t =>
			{
				float time = t * 0.95f;
				float slam = Ease.OutExpo(Mathf.Clamp(time / 0.14f, 0f, 1f));
				float exit = Ease.InCubic(Mathf.Clamp((time - 0.7f) / 0.25f, 0f, 1f));
				band.Scale = new Vector2(1f, Mathf.Max(0.02f, slam * (1f - exit)));
				text.Position = new Vector2(-560f + 900f * (1f - slam) - 300f * exit, -92f);
			});
			Fx.FreeAfter(band, 1.0);
			Audio.Sfx.Play(Audio.Snd.CutIn, 1.1f);
			if (GodotObject.IsInstanceValid(root))
			{
				P3Fx.Flash(layer, root.GlobalPosition, Pal.White, 700f * s, 0.3f);
				P3Fx.Shockwave(layer, root.GlobalPosition, Pal.Cyan, 1100f * s, 0.6f);
				P3Fx.SpeedLines(layer, root.GlobalPosition, Pal.Ice, 30, 120f, 900f, 0.4f);
			}
			Shake(ShakeStrength.Strong);
		});
		Fx.FreeAfter(root, 1.3);
	}

	// ═════════ 喊话 ═════════

	/// <summary>漫画集中线 + 爆炸对话框大字。</summary>
	public static void Shout(Control layer, Vector2 at, string text, Color tint, float s = 1f)
	{
		Audio.Sfx.Play(Audio.Snd.CutIn, 0.9f);
		Vector2 v = View(layer);
		// 集中线：从屏幕边缘指向喊话点的细长白三角，中间留空，快速闪烁
		Node2D lines = Fx.Root(layer, at, "P3ShoutLines");
		float far = v.Length();
		for (int i = 0; i < 44; i++)
		{
			float a = Mathf.Tau * i / 44f + Fx.Rand(-0.05f, 0.05f);
			Vector2 d = Vector2.Right.Rotated(a);
			Vector2 n = d.Orthogonal() * Fx.Rand(6f, 16f);
			float inner = Fx.Rand(300f, 420f) * s;
			Fx.Poly(lines, new[] { d * inner, d * far + n, d * far - n }, new Color(1f, 1f, 1f, Fx.Rand(0.35f, 0.7f)), additive: true);
		}
		Fx.Animate(lines, 0.75f, t =>
		{
			lines.Rotation = Mathf.Sin(t * 60f) * 0.01f;
			lines.Modulate = new Color(1f, 1f, 1f, Ease.Envelope(t, 0.05f, 0.6f) * (0.75f + 0.25f * Mathf.Sin(t * 70f)));
			lines.Scale = Vector2.One * (1.15f - 0.15f * Ease.OutCubic(t));
		});
		Fx.FreeAfter(lines, 0.8);
		// 爆炸对话框：16 个尖角的白色星爆 + 彩色描边，中间深色大字
		Node2D bubble = Fx.Root(layer, at, "P3ShoutBubble");
		int spikes = 16;
		Vector2[] burst = new Vector2[spikes * 2];
		float w = Mathf.Max(220f, text.Length * 46f) * s, h = 120f * s;
		for (int i = 0; i < spikes * 2; i++)
		{
			float a = Mathf.Tau * i / (spikes * 2);
			float r = i % 2 == 0 ? Fx.Rand(1.18f, 1.4f) : 0.92f;
			burst[i] = new Vector2(Mathf.Cos(a) * w * 0.5f * r, Mathf.Sin(a) * h * r);
		}
		Fx.Poly(bubble, burst, tint);
		Vector2[] inner2 = new Vector2[burst.Length];
		for (int i = 0; i < burst.Length; i++) inner2[i] = burst[i] * 0.9f;
		Fx.Poly(bubble, inner2, Pal.White);
		int size = (int)(76 * s);
		Label label = Typo.Text(bubble, text, Typo.LatinItalic, size, Pal.Abyss, Vector2.Zero, outline: 3, outlineColor: Pal.White, shadow: new Color(tint, 0.9f), shadowOffset: new Vector2(5f, 4f));
		label.Position = new Vector2(-text.Length * size * 0.29f, -size * 0.62f);
		bubble.Rotation = -0.08f;
		Fx.Animate(bubble, 0.85f, t =>
		{
			float time = t * 0.85f;
			float pop = Ease.OutBack(Mathf.Clamp(time / 0.14f, 0f, 1f));
			float exit = Ease.InCubic(Mathf.Clamp((time - 0.62f) / 0.23f, 0f, 1f));
			float jitter = time < 0.35f ? Mathf.Sin(time * 90f) * 4f : 0f;
			bubble.Scale = Vector2.One * Mathf.Max(0.01f, pop * (1f + 0.3f * exit));
			bubble.GlobalPosition = new Vector2(at.X + jitter, at.Y);
			bubble.Modulate = new Color(1f, 1f, 1f, 1f - exit);
		});
		Fx.FreeAfter(bubble, 0.9);
		Shake(ShakeStrength.Medium);
	}

	// ═════════ 锻剑 ═════════

	/// <summary>半空中锻剑：烧红的剑被锤 strikes 下（每下火花 + 冲击），最后淬火变青、冒白汽、刃光一扫。</summary>
	public static void AnvilForge(Control layer, Vector2 at, int strikes = 3, float s = 1f)
	{
		Node2D root = Fx.Root(layer, at, "P3AnvilForge");
		root.Rotation = -0.35f;
		float len = 300f * s, w = 30f * s;
		Node2D blade = new() { Position = new Vector2(-len * 0.45f, 0f) };
		root.AddChild(blade);
		Sprite2D heat = Glow(blade, new Color(ElementPal.FireMid, 0f), new Vector2(len / 40f, 2.4f * s));
		heat.Position = new Vector2(len * 0.5f, 0f);
		Polygon2D body = Fx.Poly(blade, BladeShape(len, w), new Color(ElementPal.FireOuter, 0.95f), additive: true);
		Polygon2D core = Fx.Poly(blade, BladeShape(len * 0.96f, w * 0.35f), ElementPal.FireCore, additive: true);
		Fx.Poly(blade, Rect(10f * s, 46f * s), new Color(Pal.Ice, 0.9f));              // 护手
		Fx.Poly(blade, new[] { new Vector2(-70f * s, -8f * s), new Vector2(-6f * s, -8f * s), new Vector2(-6f * s, 8f * s), new Vector2(-70f * s, 8f * s) }, new Color(Pal.Abyss, 0.95f));
		float strikeGap = 0.24f;
		float quenchAt = 0.3f + strikes * strikeGap;
		float total = quenchAt + 0.75f;
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			float appear = Ease.OutCubic(Mathf.Clamp(time / 0.25f, 0f, 1f));
			float quench = Ease.OutCubic(Mathf.Clamp((time - quenchAt) / 0.3f, 0f, 1f));
			blade.Scale = new Vector2(appear, 1f);
			body.Color = new Color(ElementPal.FireOuter.Lerp(Pal.Royal, quench), 0.95f);
			core.Color = ElementPal.FireCore.Lerp(Pal.Ice, quench);
			heat.Modulate = new Color(ElementPal.FireMid.Lerp(Pal.Cyan, quench), 0.6f * appear * (1f - 0.5f * quench));
			root.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(Mathf.Clamp((time - total + 0.22f) / 0.22f, 0f, 1f)));
		});
		for (int i = 0; i < strikes; i++)
		{
			float u = strikes == 1 ? 0.55f : 0.25f + 0.55f * i / (strikes - 1);
			Fx.After(root, 0.3 + i * strikeGap, "anvil strike", () =>
			{
				if (!GodotObject.IsInstanceValid(root)) return;
				Vector2 hitAt = blade.ToGlobal(new Vector2(len * u, 0f));
				Motifs.ForgeSpark(layer, hitAt, 0.75f * s);
				P3Fx.Flash(layer, hitAt, ElementPal.FireCore, 260f * s, 0.18f);
				Shake(ShakeStrength.Weak, -60f);
			});
		}
		// 淬火：白汽上升 + 刃光从护手扫到剑尖
		Fx.After(root, quenchAt, "anvil quench", () =>
		{
			if (!GodotObject.IsInstanceValid(root)) return;
			Vector2 mid = blade.ToGlobal(new Vector2(len * 0.5f, 0f));
			Node2D steam = Fx.Root(layer, mid, "P3Steam");
			GpuParticles2D st = Fx.Sparks(steam, new Color(1f, 1f, 1f, 0.5f), (int)(60 * s), 60f, 220f, 1.0f, gravity: -220f, scaleMin: 0.6f, scaleMax: 1.6f);
			((ParticleProcessMaterial)st.ProcessMaterial).Spread = 60f;
			((ParticleProcessMaterial)st.ProcessMaterial).EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
			((ParticleProcessMaterial)st.ProcessMaterial).EmissionBoxExtents = new Vector3(len * 0.45f, 10f, 0f);
			Fx.FreeAfter(steam, 1.3);
			P3Fx.Flash(layer, mid, Pal.Cyan, 420f * s, 0.3f);
			P3Fx.Shockwave(layer, mid, Pal.Cyan, 520f * s, 0.45f);
			Polygon2D glint = Fx.Poly(blade, Fx.Star4(46f * s, 0.12f), Pal.White, additive: true);
			Fx.Animate(glint, 0.35f, t =>
			{
				glint.Position = new Vector2(len * Ease.InOutCubic(t), 0f);
				glint.Rotation = t * 3f;
				glint.Modulate = new Color(1f, 1f, 1f, Mathf.Sin(t * Mathf.Pi));
			});
		});
		Fx.FreeAfter(root, total + 0.05);
	}

	// ═════════ 剑阵 ═════════

	/// <summary>身后展开一排幽蓝光剑，瞄准后依次射向 target；target 为空时绕身旋转一圈后收进 center。</summary>
	public static void BladeFan(Control layer, Vector2 center, Vector2? target, int count, float s = 1f)
	{
		float len = 170f * s, w = 20f * s;
		for (int i = 0; i < count; i++)
		{
			int k = i;
			float spread = count == 1 ? 0f : (k / (float)(count - 1) - 0.5f);
			Vector2 slot = center + new Vector2(-60f + spread * 60f, -200f * s) + new Vector2(spread * 380f * s, Mathf.Abs(spread) * 90f * s);
			Node2D sword = Fx.Root(layer, slot, "P3BladeFan");
			Glow(sword, new Color(Pal.Cyan, 0.35f), new Vector2(len / 40f, 1.6f * s)).Position = new Vector2(len * 0.5f, 0f);
			Fx.Poly(sword, BladeShape(len, w), new Color(Pal.Royal, 0.9f), additive: true);
			Fx.Poly(sword, BladeShape(len * 0.95f, w * 0.35f), Pal.Ice, additive: true);
			Fx.Poly(sword, Rect(6f * s, 26f * s), new Color(Pal.Cyan, 0.95f), additive: true);
			float appearAt = 0.05f * k;
			float fireAt = 0.55f + 0.09f * k;
			Vector2 aim = target ?? center;
			float total = target.HasValue ? fireAt + 0.22f : 1.3f;
			Vector2 home = slot;
			Fx.Animate(sword, total, t =>
			{
				float time = t * total;
				float appear = Ease.OutBack(Mathf.Clamp((time - appearAt) / 0.2f, 0f, 1f));
				sword.Scale = Vector2.One * Mathf.Max(0.01f, appear);
				if (target.HasValue)
				{
					float aimT = Ease.OutCubic(Mathf.Clamp((time - appearAt - 0.15f) / 0.25f, 0f, 1f));
					float hover = Mathf.Sin(time * 6f + k) * 6f;
					sword.Rotation = Mathf.LerpAngle(Mathf.Pi * 0.5f, (aim - home).Angle(), aimT);
					float fly = Ease.InCubic(Mathf.Clamp((time - fireAt) / 0.2f, 0f, 1f));
					sword.GlobalPosition = (home + new Vector2(0f, hover)).Lerp(aim - (aim - home).Normalized() * len * 0.6f, fly);
					sword.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(appear * 2f, 0f, 1f));
				}
				else
				{
					float orbit = Ease.InOutCubic(Mathf.Clamp((time - 0.3f) / 0.8f, 0f, 1f));
					float ang = (home - center).Angle() + orbit * Mathf.Tau;
					float r = (home - center).Length() * (1f - 0.85f * Ease.InCubic(Mathf.Clamp((time - 0.85f) / 0.35f, 0f, 1f)));
					sword.GlobalPosition = center + Vector2.Right.Rotated(ang) * r;
					sword.Rotation = ang + Mathf.Pi;
					sword.Modulate = new Color(1f, 1f, 1f, appear * (1f - Ease.InCubic(Mathf.Clamp((time - 1.05f) / 0.25f, 0f, 1f))));
				}
			});
			if (target.HasValue)
			{
				Fx.After(sword, fireAt + 0.18, "blade fan hit", () =>
				{
					P3Fx.Flash(layer, aim, Pal.Ice, 280f * s, 0.18f);
					P3Fx.CrossSlash(layer, aim + Fx.RandDir() * 30f, Pal.Cyan, 160f * s);
					Shake(ShakeStrength.Weak, Mathf.RadToDeg((aim - home).Angle()));
				});
			}
			Fx.FreeAfter(sword, total + 0.05);
		}
		if (!target.HasValue)
		{
			Fx.After(layer, 1.15, "blade fan absorb", () => { P3Fx.Flash(layer, center, Pal.Cyan, 460f * s, 0.3f); P3Fx.Shockwave(layer, center, Pal.Cyan, 600f * s, 0.5f); });
		}
	}

	// ═════════ 状态变化 ═════════

	/// <summary>P3R 的 -kaja / -nda：一串 V 形箭头光纹穿过身体上升（up）/ 下压，脚下光环 + 头旁 "ATTACK UP" 字样。</summary>
	public static void StatBuff(Control layer, Vector2 center, Vector2 feet, Vector2 head, string label, Color tint, bool up, float s = 1f)
	{
		float height = Mathf.Max(260f, feet.Y - head.Y + 80f) * s;
		Node2D root = Fx.Root(layer, feet, "P3StatBuff");
		Node2D ring = new() { Scale = new Vector2(1f, 0.3f) };
		root.AddChild(ring);
		Fx.Line(ring, Fx.Circle(150f * s, 48), tint, 5f, closed: true);
		Glow(ring, new Color(tint, 0.5f), new Vector2(8f, 8f) * s);
		List<Node2D> chevrons = new();
		for (int i = 0; i < 9; i++)
		{
			Node2D c = new();
			root.AddChild(c);
			float cw = 90f * s, ch = 34f * s, th = 16f * s;
			float dir = up ? -1f : 1f;
			Vector2[] v = { new(-cw, 0f), new(0f, dir * ch), new(cw, 0f), new(cw, -dir * th), new(0f, dir * (ch - th) - dir * th * 0.2f), new(-cw, -dir * th) };
			Fx.Poly(c, v, new Color(tint, 0.85f), additive: true);
			chevrons.Add(c);
		}
		Fx.Animate(root, 1.1f, t =>
		{
			float env = Ease.Envelope(t, 0.1f, 0.7f);
			ring.Rotation = t * 3f;
			ring.Modulate = new Color(1f, 1f, 1f, env);
			for (int i = 0; i < chevrons.Count; i++)
			{
				float u = Mathf.PosMod(t * 2.2f + i / (float)chevrons.Count, 1f);
				float y = up ? -u * height : -(1f - u) * height;
				chevrons[i].Position = new Vector2(Mathf.Sin(u * 6f + i) * 10f, y);
				chevrons[i].Modulate = new Color(1f, 1f, 1f, env * Mathf.Sin(u * Mathf.Pi));
				chevrons[i].Scale = Vector2.One * (0.7f + 0.4f * Mathf.Sin(u * Mathf.Pi));
			}
		});
		Fx.FreeAfter(root, 1.15);
		P3Fx.LightPillar(layer, feet, tint, 150f * s, height, 0.8f);
		// 字样：从头侧浮出，带彩色下划线
		Node2D tag = Fx.Root(layer, head + new Vector2(90f, -40f), "P3StatTag");
		tag.Skew = -0.2f;
		Label l = Typo.Text(tag, label, Typo.LatinItalic, (int)(54 * s), Pal.White, Vector2.Zero, outline: 4, outlineColor: Pal.Abyss, shadow: new Color(tint, 0.9f), shadowOffset: new Vector2(5f, 4f));
		float lw = label.Length * 30f * s;
		Fx.Poly(tag, new[] { new Vector2(0f, 62f * s), new Vector2(lw, 62f * s), new Vector2(lw - 10f, 70f * s), new Vector2(-10f, 70f * s) }, tint, additive: true);
		Vector2 home = tag.GlobalPosition;
		Fx.Animate(tag, 1.0f, t =>
		{
			tag.GlobalPosition = home + new Vector2(0f, (up ? -60f : 40f) * Ease.OutCubic(t));
			tag.Modulate = new Color(1f, 1f, 1f, Ease.Envelope(t, 0.08f, 0.7f));
			l.VisibleRatio = Mathf.Clamp(t / 0.2f, 0f, 1f);
		});
		Fx.FreeAfter(tag, 1.05);
		P3Fx.Flash(layer, center, tint, 320f * s, 0.25f);
	}

	// ═════════ 锁链 ═════════

	/// <summary>三道锁链从外圈旋入、勒紧目标，"咔"一下锁死，目标身上暗红一闪。</summary>
	public static void Shackle(Control layer, Vector2 center, Color tint, float s = 1f)
	{
		Node2D root = Fx.Root(layer, center, "P3Shackle");
		List<(Node2D ring, float tilt, float phase)> chains = new();
		for (int r = 0; r < 3; r++)
		{
			Node2D ring = new();
			root.AddChild(ring);
			int links = 22;
			for (int i = 0; i < links; i++)
			{
				float a = Mathf.Tau * i / links;
				Node2D link = new() { Position = new Vector2(Mathf.Cos(a) * 150f, Mathf.Sin(a) * 150f), Rotation = a + Mathf.Pi * 0.5f };
				ring.AddChild(link);
				Fx.Line(link, Fx.Circle(14f, 12, 0.55f), i % 2 == 0 ? new Color(0.75f, 0.8f, 0.95f) : new Color(tint, 0.95f), 4f, closed: true, additive: false);
			}
			chains.Add((ring, -0.5f + r * 0.5f, r * 0.12f));
		}
		Fx.Animate(root, 1.1f, t =>
		{
			float time = t * 1.1f;
			foreach ((Node2D ring, float tilt, float phase) in chains)
			{
				float squeeze = Ease.OutBack(Mathf.Clamp((time - phase) / 0.4f, 0f, 1f));
				float radius = Mathf.Lerp(3.2f, 0.75f, squeeze) * s;
				ring.Rotation = tilt;
				ring.Scale = new Vector2(radius, radius * 0.38f);
				ring.Skew = (1f - squeeze) * 1.5f;
			}
			root.Rotation = (1f - Ease.OutCubic(Mathf.Clamp(time / 0.5f, 0f, 1f))) * 2.5f;
			root.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(time / 0.08f, 0f, 1f) * (1f - Ease.InCubic(Mathf.Clamp((time - 0.85f) / 0.25f, 0f, 1f))));
		});
		Fx.After(root, 0.5, "shackle lock", () =>
		{
			P3Fx.Flash(layer, center, tint, 340f * s, 0.22f);
			P3Fx.Shockwave(layer, center, tint, 420f * s, 0.35f);
			Node2D tag = Fx.Root(layer, center + new Vector2(-120f, -200f * s), "P3ShackleTag");
			Typo.Text(tag, "BIND", Typo.LatinItalic, (int)(60 * s), Pal.White, Vector2.Zero, outline: 4, outlineColor: Pal.Abyss, shadow: new Color(tint, 0.9f), shadowOffset: new Vector2(5f, 4f));
			Fx.Animate(tag, 0.6f, t =>
			{
				tag.Scale = Vector2.One * Mathf.Lerp(1.6f, 1f, Ease.OutExpo(Mathf.Clamp(t / 0.2f, 0f, 1f)));
				tag.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(Mathf.Clamp((t - 0.6f) / 0.4f, 0f, 1f)));
			});
			Fx.FreeAfter(tag, 0.65);
			Shake(ShakeStrength.Weak);
		});
		Fx.FreeAfter(root, 1.15);
	}

	// ═════════ 抽牌 ═════════

	/// <summary>牌从抽牌堆沿高弧线飞出，拖着光尾翻转着飞进手牌。via：弧线经过的高点（通常是人物头顶）。</summary>
	public static void CardCascade(Control layer, Vector2 via, int count, Color tint)
	{
		Vector2 from = DrawPileAt(layer), to = HandAt(layer);
		for (int i = 0; i < count; i++)
		{
			int k = i;
			Vector2 ctrl = via + new Vector2(Fx.Rand(-80f, 80f), -220f - 40f * k);
			Vector2 dest = to + new Vector2((k - (count - 1) * 0.5f) * 90f, 0f);
			Node2D card = Fx.Root(layer, from, "P3Cascade");
			Node2D face = MiniCard(card, tint, 1.3f);
			Line2D trail = new() { Width = 14f, DefaultColor = new Color(tint, 0.8f), Material = Fx.Additive, Antialiased = true, TopLevel = true };
			trail.WidthCurve = new Curve();
			trail.WidthCurve.AddPoint(new Vector2(0f, 0f));
			trail.WidthCurve.AddPoint(new Vector2(1f, 1f));
			layer.AddChild(trail);
			List<Vector2> hist = new();
			float delay = 0.11f * k, fly = 0.7f;
			Fx.Animate(card, delay + fly + 0.2f, t =>
			{
				float time = t * (delay + fly + 0.2f);
				float u = Ease.InOutCubic(Mathf.Clamp((time - delay) / fly, 0f, 1f));
				Vector2 p = Bezier(from, ctrl, dest, u);
				card.GlobalPosition = p;
				face.Rotation = u * Mathf.Tau * 1.5f;
				face.Scale = new Vector2(Mathf.Cos(u * Mathf.Pi * 4f) * 1.3f, 1.3f);
				card.Modulate = new Color(1f, 1f, 1f, time < delay ? 0f : 1f - Ease.InCubic(Mathf.Clamp((time - delay - fly) / 0.2f, 0f, 1f)));
				if (GodotObject.IsInstanceValid(trail) && time >= delay)
				{
					hist.Add(p);
					if (hist.Count > 14) hist.RemoveAt(0);
					trail.Points = hist.ToArray();
					trail.Modulate = card.Modulate;
				}
			});
			Fx.After(card, delay + fly, "cascade land", () => P3Fx.Flash(layer, dest, tint, 200f, 0.2f));
			Fx.FreeAfter(card, delay + fly + 0.25);
			Fx.FreeAfter(trail, delay + fly + 0.25);
		}
		P3Fx.Flash(layer, from, tint, 260f, 0.25f);
	}

	// ═════════ 棱镜 ═════════

	private static Color Hue(float h) => Color.FromHsv(Mathf.PosMod(h, 1f), 0.55f, 1f);

	/// <summary>棱镜凝聚、旋转、折射出彩虹光束，碎成 cards 张彩边卡牌飞进手牌（生成无色牌）。</summary>
	public static void PrismBurst(Control layer, Vector2 at, int cards, float s = 1f)
	{
		Node2D root = Fx.Root(layer, at, "P3Prism");
		Vector2[] gem = { new(0f, -110f * s), new(70f * s, 0f), new(0f, 110f * s), new(-70f * s, 0f) };
		Glow(root, new Color(1f, 1f, 1f, 0.5f), new Vector2(7f, 9f) * s);
		Polygon2D shell = Fx.Poly(root, gem, new Color(0.85f, 0.95f, 1f, 0.55f), additive: true);
		Fx.Line(root, gem, Pal.White, 3f, closed: true);
		Node2D beams = new();
		root.AddChild(beams);
		for (int i = 0; i < 7; i++)
		{
			float a = -Mathf.Pi * 0.5f + (i - 3) * 0.3f;
			Vector2 d = Vector2.Right.Rotated(a);
			Vector2 n = d.Orthogonal() * 22f * s;
			Fx.Poly(beams, new[] { d * 60f * s, d * 720f * s + n, d * 720f * s - n }, new Color(Hue(i / 7f), 0.75f), additive: true);
		}
		beams.Scale = new Vector2(0.01f, 0.01f);
		Fx.Animate(root, 1.0f, t =>
		{
			float time = t * 1.0f;
			float form = Ease.OutBack(Mathf.Clamp(time / 0.25f, 0f, 1f));
			root.Scale = new Vector2(Mathf.Max(0.01f, form * Mathf.Cos(time * 14f)), Mathf.Max(0.01f, form));
			shell.Color = new Color(Hue(time * 1.5f), 0.6f);
			float shine = Mathf.Clamp((time - 0.3f) / 0.2f, 0f, 1f);
			beams.Scale = Vector2.One * Mathf.Max(0.01f, Ease.OutExpo(shine));
			beams.Rotation = Mathf.Sin(time * 6f) * 0.15f;
			beams.Modulate = new Color(1f, 1f, 1f, shine * (1f - Mathf.Clamp((time - 0.7f) / 0.2f, 0f, 1f)));
		});
		Fx.After(root, 0.72, "prism shatter", () =>
		{
			if (!GodotObject.IsInstanceValid(root)) return;
			P3Fx.GlassShatter(layer, at, 26, 1.0f * s, Pal.Ice);
			Audio.Sfx.Play(Audio.Snd.TarotShatter, 0.9f);
			P3Fx.Flash(layer, at, Pal.White, 420f * s, 0.25f);
			root.QueueFree();
			Vector2 hand = HandAt(layer);
			for (int i = 0; i < cards; i++)
			{
				int k = i;
				Node2D card = Fx.Root(layer, at, "P3PrismCard");
				MiniCard(card, Hue(k / (float)Math.Max(cards, 1)), 1.4f);
				Vector2 pop = at + Vector2.Up.Rotated((k - (cards - 1) * 0.5f) * 0.5f) * 170f * s;
				Vector2 dest = hand + new Vector2((k - (cards - 1) * 0.5f) * 90f, 0f);
				Fx.Animate(card, 0.85f, t =>
				{
					float out1 = Ease.OutBack(Mathf.Clamp(t / 0.35f, 0f, 1f));
					float fly = Ease.InCubic(Mathf.Clamp((t - 0.45f - 0.05f * k) / 0.4f, 0f, 1f));
					card.GlobalPosition = at.Lerp(pop, out1).Lerp(dest, fly);
					card.Rotation = (1f - out1) * 3f + fly * 2f;
					card.Modulate = new Color(1f, 1f, 1f, 1f - fly * 0.7f);
				});
				Fx.FreeAfter(card, 0.9);
			}
		});
	}

	// ═════════ 天鹅绒房间 ═════════

	/// <summary>身后一扇蓝色的门打开，强光和蓝蝶从门里涌出，然后门关上淡去。at = 门的中心。</summary>
	public static void VelvetDoor(Control layer, Vector2 at, float s = 1f)
	{
		Node2D root = Fx.Root(layer, at, "P3VelvetDoor");
		float w = 120f * s, h = 230f * s;
		Color velvet = new("1a2fa8");
		// 门里的光：先藏在门后，门打开后显出来
		Sprite2D light = Glow(root, new Color(Pal.Ice, 0f), new Vector2(w / 20f, h / 20f));
		Node2D rays = new();
		root.AddChild(rays);
		for (int i = 0; i < 9; i++)
		{
			float x = (i - 4) * w * 0.22f;
			Fx.Poly(rays, new[] { new Vector2(x - 8f, -h * 0.8f), new Vector2(x + 8f, -h * 0.8f), new Vector2(x * 3.2f + 60f, h * 2.2f), new Vector2(x * 3.2f - 60f, h * 2.2f) }, new Color(Pal.Ice, 0.18f), additive: true);
		}
		rays.Modulate = new Color(1f, 1f, 1f, 0f);
		// 门框 + 两扇门
		Vector2[] frame = { new(-w - 16f, h + 10f), new(-w - 16f, -h * 0.7f), new(0f, -h - 26f), new(w + 16f, -h * 0.7f), new(w + 16f, h + 10f) };
		Fx.Line(root, frame, new Color(Pal.Cyan, 0.95f), 6f, closed: false, additive: true);
		Node2D left = new() { Position = new Vector2(-w, 0f) }, right = new() { Position = new Vector2(w, 0f) };
		root.AddChild(left);
		root.AddChild(right);
		Vector2[] leaf = { new(0f, -h * 0.7f), new(w, -h), new(w, h), new(0f, h) };
		Vector2[] leafR = { new(0f, -h * 0.7f), new(-w, -h), new(-w, h), new(0f, h) };
		Fx.Poly(left, leaf, velvet);
		Fx.Line(left, leaf, new Color(Pal.Ice, 0.7f), 2f, closed: true, additive: false);
		Fx.Poly(right, leafR, velvet);
		Fx.Line(right, leafR, new Color(Pal.Ice, 0.7f), 2f, closed: true, additive: false);
		Fx.Animate(root, 1.6f, t =>
		{
			float time = t * 1.6f;
			float appear = Ease.OutCubic(Mathf.Clamp(time / 0.25f, 0f, 1f));
			float open = Ease.OutCubic(Mathf.Clamp((time - 0.3f) / 0.35f, 0f, 1f)) * (1f - Ease.InCubic(Mathf.Clamp((time - 1.15f) / 0.25f, 0f, 1f)));
			left.Scale = new Vector2(Mathf.Max(0.02f, 1f - open * 0.92f), 1f);
			right.Scale = new Vector2(Mathf.Max(0.02f, 1f - open * 0.92f), 1f);
			light.Modulate = new Color(Pal.Ice, 0.9f * open);
			rays.Modulate = new Color(1f, 1f, 1f, open);
			root.Modulate = new Color(1f, 1f, 1f, appear * (1f - Ease.InCubic(Mathf.Clamp((time - 1.35f) / 0.25f, 0f, 1f))));
		});
		Fx.After(root, 0.45, "velvet open", () =>
		{
			P3Fx.Flash(layer, at, Pal.Ice, 520f * s, 0.35f);
			P3Fx.ButterflyBurst(layer, at, 6, 360f * s);
		});
		Fx.FreeAfter(root, 1.65);
	}

	// ═════════ 命运之轮 ═════════

	/// <summary>
	/// 命运之轮（胜券在王）：放在人物身后层、头顶左上方（不挡人）。
	/// 进场：从小放大 + 外圈扩散；转动：外圈刻度反向慢转、轮缘灯珠追光、扇区残影随转速变化；
	/// 停下：顶端扇区亮金、指针回弹、金色星屑；退场：缩小淡出。
	/// </summary>
	public static void FortuneWheel(Control layer, Vector2 at, Color tint, float s = 1f)
	{
		float r = 150f * s;
		at.Y = Mathf.Max(at.Y, r * 1.16f + 100f);           // 外圈不压到顶部状态栏（约 90px 高）
		Node2D root = Fx.Root(layer, at, "P3FortuneWheel");
		Glow(root, new Color(tint, 0.22f), Vector2.One * r / 13f);

		// 外圈：细环 + 40 道刻度（反向慢转）
		Node2D outer = new();
		root.AddChild(outer);
		Fx.Line(outer, Fx.Circle(r * 1.16f, 72), new Color(Pal.Ice, 0.55f), 1.5f, closed: true);
		for (int i = 0; i < 40; i++)
		{
			Vector2 d = Vector2.Up.Rotated(Mathf.Tau * i / 40f);
			float len = i % 5 == 0 ? 14f : 7f;
			Fx.Line(outer, new[] { d * r * 1.16f, d * (r * 1.16f - len * s) }, new Color(Pal.Ice, i % 5 == 0 ? 0.8f : 0.4f), i % 5 == 0 ? 2f : 1f);
		}

		Node2D wheel = new();
		root.AddChild(wheel);
		// 八个扇区：两种深蓝交替，给盘面一点层次
		Color dark = new(Pal.Abyss, 0.95f), light = new(Pal.Abyss.Lightened(0.12f), 0.95f);
		for (int i = 0; i < 8; i++)
		{
			Fx.Poly(wheel, Sector(r * 0.35f, r, Mathf.Tau * i / 8f, Mathf.Tau * (i + 1) / 8f, 10), i % 2 == 0 ? dark : light);
		}
		Fx.Line(wheel, Fx.Circle(r, 64), tint, 6f * s, closed: true);
		Fx.Line(wheel, Fx.Circle(r - 9f * s, 64), new Color(tint, 0.45f), 1.5f, closed: true);
		Fx.Line(wheel, Fx.Circle(r * 0.35f, 40), new Color(tint, 0.85f), 3f, closed: true);
		string[] numerals = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII" };
		for (int i = 0; i < 8; i++)
		{
			Vector2 d = Vector2.Up.Rotated(Mathf.Tau * i / 8f);
			Fx.Line(wheel, new[] { d * r * 0.35f, d * (r - 9f * s) }, new Color(tint, 0.75f), 2.5f);
			Vector2 mid = Vector2.Up.Rotated(Mathf.Tau * (i + 0.5f) / 8f) * r * 0.7f;
			Label l = Typo.Text(wheel, numerals[i], Typo.Latin, (int)(26 * s), Pal.Ice, mid - new Vector2(numerals[i].Length * 6f * s, 16f * s));
			l.Rotation = Mathf.Tau * (i + 0.5f) / 8f;
		}
		// 轴心：深色圆 + 白色四芒星 + 一圈小点
		Fx.Poly(wheel, Fx.Circle(r * 0.33f, 32), new Color(Pal.Abyss.Darkened(0.4f), 1f));
		for (int i = 0; i < 8; i++)
		{
			Fx.Poly(wheel, Fx.Circle(2.5f * s, 8), new Color(tint, 0.8f)).Position = Vector2.Up.Rotated(Mathf.Tau * (i + 0.5f) / 8f) * r * 0.26f;
		}
		Fx.Poly(wheel, Fx.Star4(30f * s, 0.3f), Pal.White, additive: true);

		// 残影：转得快时，盘面上叠一层随转速变亮的光弧
		Polygon2D blur = Fx.Poly(root, Sector(r * 0.4f, r * 0.97f, -0.9f, 0.9f, 24), new Color(Pal.Ice, 0f), additive: true);
		// 轮缘灯珠：16 颗，转动时依次追光
		Polygon2D[] lamps = new Polygon2D[16];
		for (int i = 0; i < 16; i++)
		{
			lamps[i] = Fx.Poly(root, Fx.Circle(4f * s, 10), new Color(Pal.Ice, 0.3f), additive: true);
			lamps[i].Position = Vector2.Up.Rotated(Mathf.Tau * i / 16f) * r * 1.07f;
		}
		// 指针（不转）：白色细三角 + 青色描边
		Node2D pointer = new();
		root.AddChild(pointer);
		Vector2[] tri = { new(-14f * s, -r - 30f * s), new(14f * s, -r - 30f * s), new(0f, -r + 12f * s) };
		Fx.Poly(pointer, tri, Pal.White);
		Fx.Line(pointer, tri, Pal.Cyan, 2f, closed: true, additive: false);
		// 停下后亮起的顶端扇区
		Polygon2D win = Fx.Poly(root, Sector(r * 0.35f, r, -Mathf.Tau / 16f, Mathf.Tau / 16f, 10), new Color(tint, 0f), additive: true);
		// 底部小标签
		Typo.Text(root, "X  WHEEL OF FORTUNE", Typo.LatinItalic, (int)(18 * s), new Color(Pal.Ice, 0.85f), new Vector2(-r, r * 1.22f), HorizontalAlignment.Center).Size = new Vector2(r * 2f, 24f);

		// 进场扩散环
		P3Fx.Shockwave(layer, at, Pal.Ice, r * 2.6f, 0.4f);

		const float total = 1.75f, stopAt = 1.15f;
		float spins = 4.25f;
		float lastTick = 0f;
		float lastAngle = 0f;
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			float spin = Ease.OutCubic(Mathf.Clamp(time / stopAt, 0f, 1f));
			float angle = spin * spins * Mathf.Tau;
			float speed = Mathf.Abs(angle - lastAngle);
			lastAngle = angle;
			wheel.Rotation = angle;
			outer.Rotation = -angle * 0.12f;
			blur.Rotation = angle * 0.5f;
			blur.Color = new Color(Pal.Ice, Mathf.Clamp(speed * 0.35f, 0f, 0.22f));
			// 灯珠：转动中追光，停下后全亮闪两下
			for (int i = 0; i < 16; i++)
			{
				float a;
				if (time < stopAt)
				{
					float head = angle * 0.6f / (Mathf.Tau / 16f);
					float d = Mathf.PosMod(head - i, 16f);
					a = 0.25f + 0.75f * Mathf.Exp(-d * 0.9f);
				}
				else
				{
					a = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin((time - stopAt) * 14f));
				}
				lamps[i].Color = new Color(time < stopAt ? Pal.Ice : tint, a);
			}
			float tick = Mathf.Floor(angle / (Mathf.Tau / 8f));
			if (tick != lastTick && time > 0.55f && time < stopAt)
			{
				lastTick = tick;
				pointer.Rotation = 0.12f;                     // 指针被扇区边线拨了一下
			}
			pointer.Rotation = Mathf.Lerp(pointer.Rotation, 0f, 0.25f);
			// 进场：0.5 → 1（OutBack）；退场：缩到 0.9 并淡出
			float enter = Ease.OutBack(Mathf.Clamp(time / 0.28f, 0f, 1f));
			float exit = Ease.InCubic(Mathf.Clamp((time - (total - 0.25f)) / 0.25f, 0f, 1f));
			root.Scale = Vector2.One * (Mathf.Lerp(0.5f, 1f, enter) - 0.1f * exit);
			root.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(time / 0.1f, 0f, 1f) * (1f - exit));
			if (time >= stopAt)
			{
				float w = (time - stopAt) / 0.5f;
				win.Color = new Color(tint, 0.55f * Mathf.Clamp(1f - w * 0.6f, 0.25f, 1f));
			}
		});
		Fx.After(root, stopAt, "fortune stop", () =>
		{
			Vector2 top = at + new Vector2(0f, -r * 0.65f);
			pointer.Rotation = -0.3f;
			P3Fx.Flash(layer, top, tint, 260f * s, 0.25f);
			Node2D burst = Fx.Root(layer, top, "P3FortuneStars");
			Fx.Sparks(burst, tint, (int)(60 * s), 250f, 650f, 0.9f, gravity: 500f, scaleMin: 0.12f, scaleMax: 0.35f,
				ramp: new[] { Pal.White, tint, ElementPal.FireMid });
			Fx.FreeAfter(burst, 1.1);
			P3Fx.Shockwave(layer, at, tint, 520f * s, 0.45f);
			Audio.Sfx.Play(Audio.Snd.StarGain, 1.0f);
			Shake(ShakeStrength.Weak);
		});
		Fx.FreeAfter(root, total + 0.05);
	}

	/// <summary>圆环扇区（a0..a1，弧度，0 = 正上方顺时针）。</summary>
	private static Vector2[] Sector(float r0, float r1, float a0, float a1, int seg)
	{
		var pts = new List<Vector2>();
		for (int i = 0; i <= seg; i++)
		{
			pts.Add(Vector2.Up.Rotated(Mathf.Lerp(a0, a1, i / (float)seg)) * r1);
		}
		for (int i = seg; i >= 0; i--)
		{
			pts.Add(Vector2.Up.Rotated(Mathf.Lerp(a0, a1, i / (float)seg)) * r0);
		}
		return pts.ToArray();
	}

	// ═════════ 金币雨 ═════════

	/// <summary>金币从天而降，落到 groundY 时一闪一弹。</summary>
	public static void CoinRain(Control layer, Vector2 around, float groundY, int count = 26)
	{
		for (int i = 0; i < count; i++)
		{
			float x = around.X + Fx.Rand(-320f, 320f);
			float delay = Fx.Rand(0f, 0.6f);
			Node2D coin = Fx.Root(layer, new Vector2(x, -60f), "P3Coin");
			Polygon2D face = Fx.Poly(coin, Fx.Circle(16f, 20), ElementPal.HolyGold);
			Fx.Line(coin, Fx.Circle(16f, 20), ElementPal.FireMid, 3f, closed: true, additive: false);
			Fx.Poly(coin, Fx.Star4(8f, 0.3f), Pal.White, additive: true);
			float fall = Fx.Rand(0.55f, 0.75f);
			float spinSpeed = Fx.Rand(14f, 24f);
			float land = groundY + Fx.Rand(-20f, 20f);
			bool landed = false;
			Fx.Animate(coin, delay + fall + 0.35f, t =>
			{
				float time = t * (delay + fall + 0.35f);
				float u = Mathf.Clamp((time - delay) / fall, 0f, 1f);
				float bounce = Mathf.Clamp((time - delay - fall) / 0.35f, 0f, 1f);
				float y = Mathf.Lerp(-60f, land, Ease.InCubic(u)) - Mathf.Sin(bounce * Mathf.Pi) * 40f * (1f - bounce);
				coin.GlobalPosition = new Vector2(x + bounce * 30f, y);
				coin.Scale = new Vector2(Mathf.Cos(time * spinSpeed), 1f);
				coin.Modulate = new Color(1f, 1f, 1f, time < delay ? 0f : 1f - Ease.InCubic(bounce));
				if (!landed && u >= 1f)
				{
					landed = true;
					P3Fx.Flash(layer, new Vector2(x, land), ElementPal.HolyGold, 120f, 0.15f);
				}
			});
			Fx.FreeAfter(coin, delay + fall + 0.4f);
		}
		Audio.Sfx.Play(Audio.Snd.StarGain, 0.9f);
	}
}
