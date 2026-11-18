using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 0.9.49 审阅第六批（2026-11-18）：压制、狩猎、夜魇、必备工具。紧勒在 StrangleFx。
/// 素材用 Codex 新画的手绘一批（art/p5/fx/v4/painted/：重劈、弧斩、突刺、爪痕、冲击序列帧、暗影手），不再复用打击牌的 slash_* 刀光（用户要求）。
/// </summary>
public static class Batch6Fx
{
	private static readonly Color Hot = new(1f, 0.95f, 0.94f), Red = new(1f, 0.12f, 0.2f);

	private static float U(Control layer) => Fx.Screen(layer).Y / 1080f;

	/// <summary>一个 4×4 序列帧（冲击星芒）在 at 播一遍；size = 一格画多大，rot 角度。</summary>
	public static void Flipbook(Control layer, string sheet, Vector2 at, float size, float rot, float dur, float delay = 0f)
	{
		Fx.After(layer, delay, () =>
		{
			if (Fx.Tex(sheet) is not Texture2D tex) return;
			Node2D root = Fx.Root(layer, at, "P5Flip", dur + 0.05);
			var sp = new Sprite2D { Texture = tex, Hframes = 4, Vframes = 4, Frame = 0 };
			root.AddChild(sp);
			root.Rotation = rot;
			float k = size / (tex.GetWidth() / 4f);
			root.Scale = Vector2.One * k;
			Fx.Animate(root, 0, dur, t => sp.Frame = Mathf.Min(15, (int)(t * 16f)));
		});
	}

	/// <summary>暗影手（shadow_grasp_sheet 的第 idx 格，单个物体）：返回节点，位置 / 缩放 / 透明度自己动。</summary>
	private static Node2D? Grasp(Control layer, Vector2 at, float size, int idx, double life)
	{
		if (Fx.Tex("shadow_grasp_sheet") is not Texture2D tex) return null;
		Node2D root = Fx.Root(layer, at, "P5Grasp", life);
		var sp = new Sprite2D { Texture = tex, Hframes = 4, Vframes = 4, Frame = idx % 16 };
		root.AddChild(sp);
		root.Scale = Vector2.One * size / (tex.GetWidth() / 4f);
		return root;
	}

	// ═══════════════════════════ 压制 ═══════════════════════════

	/// <summary>
	/// 压制（远古牌：伤害 + 虚弱；审阅：参考中和，但要更丰富华丽）：「重劈压顶」。
	/// 敌人四周一暗，一记重劈从它头顶上方直劈下来（chop_heavy，从上往下扫出），劈到身上 —— 冲击星芒序列帧炸开、白闪、重顿帧、震屏，
	/// 两侧各一道斜劈（swipe）交叉补上，脚下压出一圈扁冲击波 + 往下溅的火星；紧接着中和那套虚弱锁环（用户认可的）箍上来往下压。
	/// </summary>
	public static void Suppress(Control layer, Control? back, Vector2 at, Vector2 top, float width, float face, Action<Color>? tint)
	{
		float u = U(layer), h = Mathf.Max(100f * u, (at - top).Length());
		Vector2 foot = at + new Vector2(0f, h * 0.9f);
		SkillRedo5.Dim(back ?? layer, at, new Vector2(width * 3f, h * 4f), 0.9, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.1f)) * (1f - Ease5.Seg(s, 0.55f, 0.45f)) * 0.55f);
		// 重劈：从头顶上方往下扫（转好的贴图细端在左 → rot = 90° 细端朝上）
		float len = h * 2.6f, k = len / 1024f;
		Paint.Sweep(layer, Fx.Rng.Next(2) == 0 ? "chop_heavy_01" : "chop_heavy_02", at + new Vector2(0f, -h * 0.55f), Mathf.Pi / 2f,
			new Vector2(k, k * 1.1f), Lut5.Blood, 0.07f, 0.05f, 0.3f, 50f, 0f, 1.25f);
		Sfx.Take("gx_whoosh", 0.2f);
		Fx.After(layer, 0.07f, () =>
		{
			Sfx.Take("gx_slice", 0.26f);
			HitStop.Request(0.08);
			Fx.Shake(2);
			Glow.Flash(layer, at, Hot, h * 0.9f, 0.1f);
			Glow.Flash(back ?? layer, at, Red, h * 3f, 0.45f);
			Flipbook(layer, "impact_star_sheet", at, h * 2.2f, Fx.Rand(-0.3f, 0.3f), 0.4f);
			Glow.Shockwave(layer, foot, new Color(0.9f, 0.05f, 0.14f), width * 2.6f, 0.45f, 0.22f);
			Glow.Sparks(layer, at, 22, 300f * u, 900f * u, 0.55f, new[] { Hot, Red, new Color(0.4f, 0f, 0.06f) }, 1400f * u, 0.04f * u, 0.1f * u, Vector2.Down, 70f);
			tint?.Invoke(new Color(0.6f, 0.4f, 0.45f));
		});
		// 两侧交叉补两刀（弧斩，剪刀形那张不用）
		for (int i = 0; i < 2; i++)
		{
			float side = i == 0 ? -1f : 1f, d = 0.14f + 0.05f * i;
			Paint.Sweep(layer, i == 0 ? "swipe_arc_01" : "swipe_arc_02", at + new Vector2(side * width * 0.1f, -h * 0.1f), side * 0.7f + (side < 0 ? Mathf.Pi : 0f),
				new Vector2(h * 1.4f / 2048f * 2f, h * 1.4f / 2048f * 2f), Lut5.Blood, 0.05f, 0.04f, 0.24f, 30f, d, 1.2f);
		}
		CardFx.Weaken(layer, at, top, 0.3f);
		if (tint != null) Fx.After(layer, 0.7f, () => Fx.Animate(layer, 0, 0.4, t => tint(Colors.White.Lerp(new Color(0.6f, 0.4f, 0.45f), 1f - t))));
	}

	// ═══════════════════════════ 狩猎 ═══════════════════════════

	private static Vector2 _huntAt;
	private static ulong _huntUntil;

	/// <summary>
	/// 狩猎（命中）：「猎爪」—— 四道爪痕（claw_rake_02）斜着撕过目标，冲击星芒、白闪、顿帧；记下位置，等斩杀时用。
	/// </summary>
	public static void HuntHit(Control layer, Vector2 at, float h, float face)
	{
		float u = U(layer);
		_huntAt = at;
		_huntUntil = Stage.NowMs() + 2500;
		float k = h * 2.2f / 1024f;
		Paint.Sweep(layer, "claw_rake_02", at, -0.15f * face, new Vector2(k * face, k), Lut5.Blood, 0.06f, 0.06f, 0.3f, 40f, 0f, 1.25f);
		Fx.After(layer, 0.05f, () =>
		{
			Sfx.Take("gx_slice", 0.22f);
			HitStop.Request(0.06);
			Fx.Shake(1);
			Glow.Flash(layer, at, Hot, h * 0.8f, 0.1f);
			Glow.Flash(layer, at, Red, h * 2f, 0.35f);
			Flipbook(layer, "impact_star_sheet", at, h * 1.6f, 0f, 0.36f);
		});
	}

	/// <summary>
	/// 狩猎（斩杀 → 额外一次卡牌奖励，CombatRoom.AddExtraReward）：猎物身上「啪」地弹出一张预告信（金边一亮），翻着往上飞到画面顶上（奖励），
	/// 一路拖尾、星光，飞到顶上一闪收起，一声「叮」。用户：特效稍微表达一下「斩杀时额外获得一次卡牌奖励」。
	/// </summary>
	public static void HuntReward(Control layer)
	{
		if (Stage.NowMs() > _huntUntil) return;
		float u = U(layer);
		Vector2 from = _huntAt, to = new(Fx.Screen(layer).X * 0.5f, 90f * u);
		float w = 90f * u;
		Fx.After(layer, 0.15f, () =>
		{
			Sfx.Take("gx_card_slide", 0.2f);
			Glow.Flash(layer, from, new Color(1f, 0.85f, 0.4f), 260f * u, 0.3f);
			Glow.Glint(layer, from, 1f);
			Phantom.P5Card card = Phantom.Card(layer, from, w, 1.0, 0.7f);
			Vector2 ctrl = from.Lerp(to, 0.4f) + new Vector2(0f, -200f * u);
			Fx.Animate(card.Root, 0, 0.8, t =>
			{
				float e = t < 0.25f ? 0f : Fx.InCubic((t - 0.25f) / 0.75f);
				Vector2 pop = from + new Vector2(0f, -120f * u * Ease5.OutBack(Mathf.Min(1f, t / 0.25f)));
				Vector2 p = e <= 0f ? pop : pop.Lerp(ctrl, e).Lerp(ctrl.Lerp(to, e), e);
				card.Place(p, 0.2f * Mathf.Sin(t * 9f), t * 14f, 1.2f - 0.5f * e);
				card.Set("glow", 0.6f + 0.4f * Mathf.Sin(t * 20f));
			});
			Fx.After(layer, 0.8f, () =>
			{
				Sfx.Take("gx_ping", 0.2f);
				card.Trail?.Kill();
				card.Root.Visible = false;
				Glow.Flash(layer, to, new Color(1f, 0.85f, 0.4f), 220f * u, 0.3f);
				Glow.Glint(layer, to, 0.9f);
			});
		});
	}

	// ═══════════════════════════ 夜魇 ═══════════════════════════

	/// <summary>
	/// 夜魇（选一张手牌，下回合得到它的 3 张复制）：「梦魇之手」。四周压成暗红黑，他手上方浮起一张预告信；
	/// 几只暗影手（shadow_grasp）从他身边的地上 / 身后伸上来、扑向那张牌、攥住；牌被拽成三张、往后翻进暗处（下回合才回来），
	/// 每张一下暗红闪光，最后暗影手缩回、四周慢慢亮回来。
	/// </summary>
	public static void Nightmare(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		Control back = c.Back != null && GodotObject.IsInstanceValid(c.Back) ? c.Back : c.Front, front = c.Front;
		Vector2 mid = c.Feet + new Vector2(0f, -h * 0.55f);
		Vector2 cardAt = c.Hand + new Vector2(f * h * 0.15f, -h * 0.35f);
		const float grab = 0.42f, split = 0.62f, end = 1.25f;
		Sfx.Take("gx_whoosh", 0.12f, 0.05f);
		Sfx.Take("gx_freeze", 0.18f, grab);
		Sfx.Take("gx_card_stack", 0.14f, split + 0.3f);
		SkillRedo5.Dim(back, mid, new Vector2(h * 5f, h * 3.5f), end + 0.4, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.15f)) * (1f - Ease5.Seg(s, 0.75f, 0.25f)) * 0.75f);
		Glow.Flash(back, cardAt, new Color(0.45f, 0.02f, 0.08f), h * 2f, end);
		// 浮起的牌
		Phantom.P5Card card = Phantom.Card(front, c.Hand, h * 0.16f, split + 0.05);
		Fx.Animate(card.Root, 0, split, t =>
		{
			float s = t * split;
			float e = Ease5.OutBack(Mathf.Min(1f, s / 0.25f));
			Vector2 shake = s > grab ? new Vector2(Fx.Rand(-1f, 1f), Fx.Rand(-1f, 1f)) * 4f * u : Vector2.Zero;
			card.Place(c.Hand.Lerp(cardAt, e) + shake, 0.15f * Mathf.Sin(s * 4f), 0f, 0.7f + 0.3f * e);
			card.Set("glow", s > grab ? 0.8f : 0.2f);
		});
		// 暗影手：从地上 / 身后伸上来扑向牌
		int[] frames = { 0, 4, 9, 13, 2 };
		for (int i = 0; i < frames.Length; i++)
		{
			int k = i;
			float ang = Mathf.Lerp(-2.3f, -0.8f, k / (frames.Length - 1f));
			Vector2 start = cardAt + new Vector2(Mathf.Cos(ang) * h * 1.1f * f, h * 0.9f + Mathf.Sin(ang) * h * 0.25f);
			float d = 0.08f + 0.05f * k;
			if (Grasp(k % 2 == 0 ? front : back, start, h * 0.85f, frames[k], end + 0.05) is not { } g) continue;
			g.Visible = false;
			float rot0 = (start - cardAt).Angle() - Mathf.Pi / 2f;
			Fx.Animate(g, d, end - d, t =>
			{
				g.Visible = true;
				float s = d + t * (end - d);
				float reach = Ease5.OutQuint(Mathf.Min(1f, (s - d) / (grab - d)));
				float back2 = Fx.InCubic(Ease5.Seg(s, split + 0.15f, 0.35f));
				Vector2 p = start.Lerp(cardAt + (start - cardAt).Normalized() * h * 0.3f, reach * (1f - back2));
				g.GlobalPosition = p;
				g.Rotation = rot0 + Mathf.Pi;
				g.Modulate = new Color(0.9f, 0.7f, 0.75f, Mathf.Min(1f, (s - d) / 0.08f) * (1f - Ease5.Seg(s, split + 0.25f, 0.3f)));
			});
		}
		Fx.After(front, grab, () =>
		{
			HitStop.Request(0.04);
			Glow.Flash(front, cardAt, new Color(0.9f, 0.05f, 0.14f), h * 0.6f, 0.3f);
		});
		// 牌被拽成三张、往后翻进暗处
		for (int i = 0; i < 3; i++)
		{
			int k = i;
			Phantom.P5Card cp = Phantom.Card(back, cardAt, h * 0.16f, end + 0.05, 0.5f);
			cp.Root.Visible = false;
			Vector2 dest = cardAt + new Vector2((k - 1) * h * 0.35f * f - f * h * 0.3f, -h * 0.25f + Mathf.Abs(k - 1) * h * 0.1f);
			Fx.Animate(cp.Root, split, end - split, t =>
			{
				cp.Root.Visible = true;
				float e = Ease5.OutQuint(Mathf.Min(1f, t / 0.4f));
				cp.Place(cardAt.Lerp(dest, e), (k - 1) * 0.3f * e, e * 2.5f, 1f - 0.45f * Fx.InCubic(Ease5.Seg(t, 0.5f, 0.5f)));
				cp.Set("glow", 0.5f * (1f - t));
				cp.Rect.Modulate = new Color(0.6f, 0.35f, 0.42f, 1f - Ease5.Seg(t, 0.55f, 0.45f));
			});
			Fx.After(front, split + 0.05f * k, () => Glow.Flash(back, dest, new Color(0.7f, 0.03f, 0.12f), h * 0.4f, 0.3f));
		}
	}

	// ═══════════════════════════ 必备工具 ═══════════════════════════

	/// <summary>
	/// 必备工具（能力：每回合开始抽 1 弃 1；审阅：用了通用特效没特色）：「怪盗的工具」。
	/// 他一挥手，身前「咔嚓」一下两刃交错的剪刀斩（swipe_arc_03）一合 —— 白闪、星光；同时一张牌从剪刃交点弹进他手里（抽），
	/// 另一张从手里翻出去、被剪刃一剪两半、碎成碎光（弃）；最后身上一层红光收紧（能力生效）。
	/// </summary>
	public static void Tools(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		Control back = c.Back != null && GodotObject.IsInstanceValid(c.Back) ? c.Back : c.Front, front = c.Front;
		Vector2 at = c.Hand + new Vector2(f * h * 0.45f, -h * 0.15f);
		const float snip = 0.12f;
		Sfx.Take("gx_whoosh", 0.14f);
		Sfx.Take("gx_slice", 0.2f, snip);
		Sfx.Take("gx_card_slide", 0.14f, snip + 0.1f);
		Glow.Flash(back, at, new Color(0.7f, 0.03f, 0.12f), h * 1.4f, 0.8f);
		float k = h * 1.3f / 2048f * 2f;
		Paint.Sweep(front, "swipe_arc_03", at, f > 0 ? 0f : Mathf.Pi, new Vector2(k, k), Lut5.Blood, 0.08f, 0.06f, 0.26f, 20f, 0f, 1.25f);
		Fx.After(front, snip, () =>
		{
			HitStop.Request(0.035);
			Glow.Flash(front, at, Hot, h * 0.4f, 0.1f);
			Glow.Glint(front, at, 0.9f);
			Flipbook(front, "impact_star_sheet", at, h * 0.9f, 0f, 0.3f);
		});
		// 抽：一张牌从交点弹进手里
		Phantom.P5Card inCard = Phantom.Card(front, at, h * 0.14f, 0.7, 0.6f);
		inCard.Root.Visible = false;
		Fx.Animate(inCard.Root, snip, 0.35, t =>
		{
			inCard.Root.Visible = true;
			float e = Fx.InCubic(t);
			Vector2 ctrl = at.Lerp(c.Hand, 0.5f) + new Vector2(0f, -h * 0.2f);
			inCard.Place(at.Lerp(ctrl, e).Lerp(ctrl.Lerp(c.Hand, e), e), e * 0.6f * f, e * 8f, 1f - 0.4f * e);
		});
		Fx.After(front, snip + 0.35f, () =>
		{
			inCard.Trail?.Kill();
			inCard.Root.Visible = false;
			Glow.Flash(front, c.Hand, Hot, h * 0.18f, 0.08f);
		});
		// 弃：一张牌从手里翻出去，被剪开碎掉
		Phantom.P5Card outCard = Phantom.Card(front, c.Hand, h * 0.14f, 0.8);
		Vector2 cutAt = at + new Vector2(f * h * 0.3f, h * 0.05f);
		Fx.Animate(outCard.Root, 0, snip + 0.18f, t =>
		{
			float e = Ease5.OutQuint(Mathf.Min(1f, t * 1.3f));
			outCard.Place(c.Hand.Lerp(cutAt, e), -0.4f * f * e, e * 6f, 0.8f + 0.2f * e);
		});
		Fx.After(front, snip + 0.18f, () =>
		{
			outCard.Root.Visible = false;
			Phantom.Shards(front, cutAt, 12, 200f * u, 600f * u, new Vector2(f, -0.3f), 80f, 0.35f, 600f * u, 14f * u);
			Glow.Flash(front, cutAt, new Color(1f, 0.3f, 0.35f), h * 0.35f, 0.2f);
		});
		Fx.After(front, snip + 0.4f, () => SkillRedo5.EchoPulsePublic(c, 0.05f));
	}
}
