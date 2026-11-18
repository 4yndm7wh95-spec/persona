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
		// 重劈（用户 2026-11-18：贴图感太重 → 主体换成着色器的能量鞭）：从头顶高处一记往下劈的流纹能量鞭，略带弧度，头宽尾尖；
		// 后面一道细的白热疾光；手绘重劈贴图只淡淡垫在后面（压暗、半透明）
		Vector2 sky = at + new Vector2(-face * h * 0.35f, -h * 2.4f), land = at + new Vector2(face * h * 0.08f, h * 0.35f);
		Vector2 bend = sky.Lerp(land, 0.5f) + new Vector2(face * h * 0.25f, 0f);
		Func<float, Vector2> chop = e => sky.Lerp(bend, e).Lerp(bend.Lerp(land, e), e);
		SkillRedo5.Whip(layer, back ?? layer, chop, 0f, 0.08f, 0.26f, h * 1.1f);
		SkillRedo5.StreakSharp(layer, chop, 0f, 0.09f, h * 1.6f, 26f * u, new Color(1f, 0.9f, 0.88f), 2f);
		float len = h * 2.6f, k = len / 1024f;
		Fx.After(layer, 0.02f, () =>
		{
			var st = Paint.Sweep(back ?? layer, Fx.Rng.Next(2) == 0 ? "chop_heavy_01" : "chop_heavy_02", at + new Vector2(0f, -h * 0.55f), Mathf.Pi / 2f,
				new Vector2(k, k * 1.1f), Lut5.Blood, 0.06f, 0.04f, 0.26f, 40f, 0f, 0.8f);
			if (st != null) st.Root.Modulate = new Color(0.6f, 0.6f, 0.6f, 0.45f);
		});
		Sfx.Take("gx_whoosh", 0.2f);
		Fx.After(layer, 0.07f, () =>
		{
			Sfx.Take("gx_slice", 0.26f);
			HitStop.Request(0.08);
			Fx.Shake(2);
			Glow.Flash(layer, at, Hot, h * 0.9f, 0.1f);
			Glow.Flash(back ?? layer, at, Red, h * 3f, 0.45f);
			Glow.Ripple(layer, at, h * 2.4f, 0.4f, 0.025f);
			SkillRedo5.Cross(layer, at, h * 0.5f);
			Glow.Shockwave(layer, foot, new Color(0.9f, 0.05f, 0.14f), width * 2.6f, 0.45f, 0.22f);
			Glow.Sparks(layer, at, 22, 300f * u, 900f * u, 0.55f, new[] { Hot, Red, new Color(0.4f, 0f, 0.06f) }, 1400f * u, 0.04f * u, 0.1f * u, Vector2.Down, 70f);
			tint?.Invoke(new Color(0.6f, 0.4f, 0.45f));
		});
		// 劈完两侧各一道着色器小横劈交叉补上（由里向外、带倾斜）
		SkillRedo5.SlashArc(layer, at + new Vector2(0f, -h * 0.05f), h * 0.75f, h * 0.24f, 0.55f, face, 0.15f, 0.12f, 0.2f, h);
		SkillRedo5.SlashArc(layer, at + new Vector2(0f, h * 0.05f), h * 0.7f, h * 0.22f, -0.55f, -face, 0.2f, 0.12f, 0.2f, h);
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
		// 四道着色器爪痕（用户：贴图感太重 → 主体换成实时疾光，手绘爪痕压暗垫底）：从左上往右下一道接一道撕下去，粗、头尖尾尖、白热芯
		Vector2 dir = new Vector2(face * 0.55f, 1f).Normalized(), across = new Vector2(-dir.Y, dir.X);
		for (int i = 0; i < 4; i++)
		{
			Vector2 c0 = at + across * (i - 1.5f) * h * 0.22f - dir * h * 0.75f, c1 = c0 + dir * h * 1.5f;
			float d = 0.025f * i;
			SkillRedo5.StreakSharp(layer, e => c0.Lerp(c1, e), d, 0.1f, h * 1.3f, 80f * u, Red, 1.6f);
			SkillRedo5.StreakSharp(layer, e => c0.Lerp(c1, e), d + 0.01f, 0.1f, h * 1.0f, 22f * u, new Color(1f, 0.9f, 0.88f), 2f);
		}
		float k = h * 2.2f / 1024f;
		var cl = Paint.Sweep(layer, "claw_rake_02", at, -0.15f * face, new Vector2(k * face, k), Lut5.Blood, 0.06f, 0.06f, 0.3f, 40f, 0.04f, 0.7f);
		if (cl != null) cl.Root.Modulate = new Color(0.55f, 0.55f, 0.55f, 0.4f);
		Fx.After(layer, 0.05f, () =>
		{
			Sfx.Take("gx_slice", 0.22f);
			HitStop.Request(0.06);
			Fx.Shake(1);
			Glow.Flash(layer, at, Hot, h * 0.8f, 0.1f);
			Glow.Flash(layer, at, Red, h * 2f, 0.35f);
			Glow.Ripple(layer, at, h * 1.8f, 0.35f, 0.02f);
			Phantom.Shards(layer, at, 10, 260f * u, 700f * u, dir, 50f, 0.32f, 600f * u, 14f * u);
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
			Vector2 c2 = from.Lerp(to, 0.4f) + new Vector2(0f, -200f * u);
			SkillRedo5.StreakSharp(layer, e => from.Lerp(c2, e).Lerp(c2.Lerp(to, e), e), 0.2f, 0.6f, 260f * u, 14f * u, new Color(1f, 0.8f, 0.45f), 1.6f);
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
		// 暗影触须（用户 2026-11-18：贴图感太重、没有着色器特效 → 主体换成着色器的流纹能量鞭）：五条从他脚边 / 身后的地上
		// 卷上来、一条接一条扑向那张牌，头宽尾尖、暗红黑带红边；暗影手贴图只淡淡叠在触须头上（压暗、半透明）
		for (int i = 0; i < 5; i++)
		{
			int k = i;
			float x = (k - 2f) / 2f;
			Vector2 root0 = c.Feet + new Vector2((x * h * 0.85f - h * 0.1f) * f, h * 0.02f);
			Vector2 tip = cardAt + new Vector2(x * h * 0.3f * f, h * 0.1f + Mathf.Abs(x) * h * 0.08f);
			Vector2 c1 = root0.Lerp(tip, 0.35f) + new Vector2(x * h * 0.5f * f + h * 0.15f, -h * 0.1f);
			Vector2 c2 = root0.Lerp(tip, 0.75f) + new Vector2(-x * h * 0.35f * f, 0f);
			Func<float, Vector2> path = e =>
			{
				Vector2 a = root0.Lerp(c1, e), b = c1.Lerp(c2, e), cc = c2.Lerp(tip, e);
				return a.Lerp(b, e).Lerp(b.Lerp(cc, e), e);
			};
			float d = 0.1f + 0.06f * k;
			SkillRedo5.Whip(k % 2 == 0 ? front : back, back, path, d, grab - d, split + 0.3f - grab, h * 0.14f);
			if (Grasp(k % 2 == 0 ? front : back, tip, h * 0.4f, new[] { 0, 4, 9, 13, 2 }[k], end) is { } g)
			{
				g.Visible = false;
				Fx.Animate(g, grab - 0.06f, split + 0.25f - grab, t =>
				{
					g.Visible = true;
					g.GlobalPosition = path(1f);
					g.Rotation = (path(1f) - path(0.92f)).Angle() + Mathf.Pi / 2f;
					g.Modulate = new Color(0.6f, 0.55f, 0.55f, 0.5f * Mathf.Min(1f, t * 6f) * (1f - Ease5.Seg(t, 0.6f, 0.4f)));
				});
			}
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
				cp.Set("clip", new Vector3(0f, 1f, -1.3f));
				cp.Set("burn", Ease5.Seg(t, 0.35f, 0.6f) * 1.2f);   // 用卡牌着色器的燃烧从下往上烧进暗处
				cp.Rect.Modulate = new Color(0.75f, 0.45f, 0.52f, 1f);
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
		// 剪刀（用户：贴图感太重 → 两道着色器小横劈交叉一合，由里向外、一左一右）
		SkillRedo5.SlashArc(front, at, h * 0.7f, h * 0.25f, 0.6f, f, 0f, snip, 0.22f, h);
		SkillRedo5.SlashArc(front, at, h * 0.7f, h * 0.25f, -0.6f, -f, 0f, snip, 0.22f, h);
		Fx.After(front, snip, () =>
		{
			HitStop.Request(0.035);
			Glow.Flash(front, at, Hot, h * 0.4f, 0.1f);
			Glow.Glint(front, at, 0.9f);
			Glow.Ripple(front, at, h * 0.9f, 0.3f, 0.015f);
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
		Phantom.P5Card outCard = Phantom.Card(front, c.Hand, h * 0.14f, 0.9);
		Vector2 cutAt = at + new Vector2(f * h * 0.3f, h * 0.05f);
		Fx.Animate(outCard.Root, 0, snip + 0.18f, t =>
		{
			float e = Ease5.OutQuint(Mathf.Min(1f, t * 1.3f));
			outCard.Place(c.Hand.Lerp(cutAt, e), -0.4f * f * e, e * 6f, 0.8f + 0.2f * e);
		});
		// 被剪的那张：沿剪口一切两半、从切口烧掉（卡牌着色器的切开 + 燃烧）
		Fx.Animate(outCard.Root, snip + 0.18f, 0.45, t =>
		{
			outCard.Set("clip", new Vector3(0.7f, 0.7f, 0f));
			outCard.Set("burn", Mathf.Min(1.2f, t * 1.4f));
			outCard.Place(cutAt + new Vector2(f * 0.04f * h * t, h * 0.08f * t * t), -0.4f * f - 0.3f * t * f, 6f, 1f);
		});
		Fx.After(front, snip + 0.18f, () => Glow.Flash(front, cutAt, new Color(1f, 0.3f, 0.35f), h * 0.35f, 0.2f));
		Fx.After(front, snip + 0.4f, () => SkillRedo5.EchoPulsePublic(c, 0.05f));
	}
}
