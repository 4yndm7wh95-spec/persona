using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 技能牌特效重做第三版（2026-11-12，用户：按你的审美来，好看、帅、细节足够不粗糙）。积木在 Vfx/Phantom.cs（怪盗风：精细的预告信卡、
/// 红白拖尾、网点斜切色带、拉长的火花、红黑白纸屑）。这里登记的牌覆盖 CardSkillFx2 / 3 里的旧版本。
/// </summary>
public static class SkillRedo
{
	public static void Register(Dictionary<string, Action<SkillCtx>> cards)
	{
		cards["Acrobatics"] = Acrobatics;
		cards["Prepared"] = Prepared;
		cards["Expertise"] = Expertise;
		cards["Reflex"] = Reflex;
		cards["Backflip"] = Backflip;
		cards["EscapePlan"] = EscapePlan;
	}

	private static float CardW(SkillCtx c) => c.Tall * 0.16f;
	// 音效跟着画面走（用户 2026-11-12：音效和动画各放各的）：出手那一下一声、每张牌落进手牌区那一刻一声、斩 / 交叉 / 烟弹各在那一帧（PlayHit 按样本的起音提前）
	private static void DrawTick(int k, float delay = 0f) { }
	/// <summary>音色板里的一个音（Sfx.Palette），at 秒后、对在那一帧。都压得很轻：有，但不抢。</summary>
	private static void S(string name, float vol, float at = 0f) => Sfx.Take(name, vol * 0.8f, at);
	private static void Snap(float at, float vol = 0.16f) => Sfx.PlayHit(Sfx.CardDraw, vol, at);

	/// <summary>从 a 飞进手牌区（先慢后快、带一点弧），越飞越小；结束时拖尾收掉、牌藏起、手牌区几根火花。</summary>
	private static Phantom.P5Card ToHand(SkillCtx c, Vector2 a, float delay, float life, float tilt0, float arc, float trail = 0.85f, float spin = 9f)
	{
		float u = c.Unit, w = CardW(c);
		Vector2 hand = SkillFx.HandArea(c.Front), ctrl = a.Lerp(hand, 0.35f) + new Vector2(c.Face * c.Tall * 0.2f, -arc);
		Phantom.P5Card card = Phantom.Card(c.Front, a, w, delay + life + 0.2, trail);
		card.Root.Visible = false;
		Fx.Animate(card.Root, delay, life, t =>
		{
			card.Root.Visible = true;
			float e = Fx.InCubic(t) * 0.7f + t * 0.3f;
			card.Place(a.Lerp(ctrl, e).Lerp(ctrl.Lerp(hand, e), e), tilt0 + e * c.Face * 1.1f, e * spin, 1f - 0.45f * e);
		});
		S("gx_card_slide", 0.16f, delay);
		S("gx_card_stack", 0.22f, delay + life);
		Fx.After(c.Front, delay + life, () =>
		{
			card.Trail?.Kill();
			card.Root.Visible = false;
			Phantom.Shards(c.Front, hand, 5, 200f * u, 480f * u, Vector2.Up, 50f, 0.28f, 500f * u, 14f * u);
		});
		return card;
	}

	/// <summary>一张牌烧掉、碎成纸屑：在 at 处（倾斜 tilt）从一角烧起，0.35s 后迸出纸屑和余烬。</summary>
	private static void BurnAway(SkillCtx c, Phantom.P5Card card, Vector2 at, float tilt, Vector2 drift)
	{
		float u = c.Unit;
		card.Set("clip", new Vector3(0.6f, 0.8f, -1.6f));
		Fx.Animate(card.Root, 0, 0.45, t =>
		{
			card.Place(at + drift * Ease5.OutQuint(t), tilt + t * 0.8f, 0f, 1f);
			card.Set("burn", t * 1.3f);
		});
		Fx.After(c.Front, 0.28f, () =>
		{
			Phantom.Confetti(c.Front, at + drift, 12, 240f * u, drift.Normalized() + Vector2.Up * 0.3f, 70f, 0.9f, 7f * u);
			Glow.Sparks(c.Front, at + drift, 10, 40f, 150f, 0.5f, new[] { Phantom.Hot, Phantom.Red, new Color(0.3f, 0f, 0.05f) }, -80f, 0.02f * u, 0.05f * u, Vector2.Up, 120f);
		});
		Fx.After(c.Front, 0.46f, () => { card.Trail?.Kill(); card.Root.Visible = false; });
	}

	// ═══════════════════════════ 早有准备：一换一 ═══════════════════════════

	/// <summary>
	/// 早有准备（抽 N 弃 N）：两对牌交错——旧牌从他指间甩出、新牌从手牌区那边射上来，两张在他身前斜着擦身而过，交叉的一刹那「啪」：
	/// 一道白色斜闪 + 火花 + 极短顿帧；旧牌飞远、从一角烧掉碎成纸屑，新牌划个弧回到手牌区。约 1s。
	/// </summary>
	public static void Prepared(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face, w = CardW(c);
		Vector2 hand = SkillFx.HandArea(c.Front);
		S("gx_card_slide", 0.24f);
		for (int pr = 0; pr < 2; pr++)
		{
			int k = pr;
			float d = 0.16f * k;
			Vector2 meet = c.Chest + new Vector2(f * h * (0.42f + 0.1f * k), -h * (0.18f + 0.2f * k));
			const float toMeet = 0.2f;
			// 旧牌：指间 → 交汇点 → 甩远
			Vector2 far = meet + new Vector2(f * h * 0.5f, -h * 0.3f);
			Phantom.P5Card old = Phantom.Card(c.Front, c.Hand, w, d + 0.9, 0.7f);
			old.Root.Visible = false;
			Fx.Animate(old.Root, d, toMeet + 0.18f, t =>
			{
				old.Root.Visible = true;
				float s = t * (toMeet + 0.18f);
				Vector2 pos = s < toMeet ? c.Hand.Lerp(meet, Ease5.OutQuint(s / toMeet)) : meet.Lerp(far, Ease5.OutQuint((s - toMeet) / 0.18f));
				old.Place(pos, f * (0.5f + s * 4f), Mathf.Pi + s * 14f, 1f);
			});
			Fx.After(c.Front, d + toMeet + 0.18f, () => BurnAway(c, old, far, f * 1.2f, new Vector2(f * h * 0.12f, -h * 0.05f)));
			S("gx_burn", 0.16f, d + toMeet + 0.2f);
			S("gx_card_slide", 0.18f, d);
			// 新牌：手牌区 → 交汇点 → 回手牌区
			Phantom.P5Card fresh = Phantom.Card(c.Front, hand, w, d + toMeet + 0.05, 0.9f);
			fresh.Root.Visible = false;
			Fx.Animate(fresh.Root, d, toMeet, t =>
			{
				fresh.Root.Visible = true;
				fresh.Place(hand.Lerp(meet, Ease5.OutQuint(t)), -f * (0.4f + t), t * 10f, 0.7f + 0.3f * t);
			});
			Fx.After(c.Front, d + toMeet, () => { fresh.Trail?.Kill(); fresh.Root.Visible = false; });
			ToHand(c, meet, d + toMeet, 0.42f, -f * 1.4f, h * 0.25f);
			DrawTick(k, d);
			S("gx_slice", 0.24f, d + toMeet);
			Fx.After(c.Front, d + toMeet, () =>
			{
				Cut.Razor(c.Front, meet, f > 0f ? -0.9f : Mathf.Pi + 0.9f, w * 3.4f, 3f * u, 0.09f);
				Glow.Flash(c.Front, meet, Phantom.Hot, w * 2.4f, 0.1f);
				Glow.Glint(c.Front, meet, 0.6f);
				Phantom.Shards(c.Front, meet, 10, 250f * u, 700f * u, new Vector2(f, -0.4f), 70f, 0.3f, 300f * u, 20f * u);
				HitStop.Request(0.025);
			});
		}
	}

	// ═══════════════════════════ 独门技术：魔术师的扇 ═══════════════════════════

	/// <summary>
	/// 独门技术（抽牌，本回合保留）：手一抹，身前「唰」地展开一把扇形牌（五张一张压一张转开，像魔术师），一道高光从左到右依次扫过每张，
	/// 每张被扫到时四角一亮扣上一圈白框（保留）；扇子「啪」地收拢成一叠，整叠翻一下飞进手牌区。约 1.2s。
	/// </summary>
	public static void Expertise(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face, w = CardW(c) * 1.45f;
		Vector2 pivot = c.Chest + new Vector2(f * h * 0.45f, h * 0.12f);       // 扇轴（牌的下端握在这里）
		const int n = 5;
		const float open = 0.22f, hold = 0.5f, close = 0.12f;
		S("gx_fan_open", 0.3f, 0.02f);
		S("gx_fan_close", 0.3f, open + hold);
		for (int i = 0; i < n; i++)
		{
			int k = i;
			float target = (k - (n - 1) / 2f) * 0.36f * f;                           // 扇开后的角度
			Phantom.P5Card card = Phantom.Card(c.Front, pivot, w, open + hold + close + 0.1);
			Vector2 off = new(0f, -w * 0.62f);                                      // 牌中心离扇轴
			float sweepAt = open + 0.06f + 0.07f * k;
			Fx.Animate(card.Root, 0, open + hold + close, t =>
			{
				float s = t * (open + hold + close);
				float a = s < open ? target * Ease5.OutBack(s / open) : s < open + hold ? target : target * (1f - Ease5.OutQuint((s - open - hold) / close));
				float lift = s < open ? Ease5.OutQuint(s / open) : 1f;
				Vector2 pos = pivot + new Vector2(0f, -h * 0.3f * lift) + off.Rotated(a);
				card.Place(pos, a, s < open ? (1f - lift) * Mathf.Pi : 0f, 0.8f + 0.2f * lift);
				float sh = (s - sweepAt) / 0.14f;
				card.Set("sheen", sh * 3f - 1f);
				card.Set("glow", Mathf.Max(0f, 1f - Mathf.Abs(sh - 0.5f) * 2f) * 0.5f);
			});
			Fx.After(c.Front, sweepAt + 0.07f, () =>
			{
				Vector2 pos = pivot + new Vector2(0f, -h * 0.3f) + off.Rotated(target);
				Brackets(c.Front, pos, target, w, u);
				S("gx_ping", 0.1f + 0.02f * k);
			});
			if (k < 3) DrawTick(k);
		}
		Fx.After(c.Front, open + hold + close, () =>
		{
			Vector2 stack = pivot + new Vector2(0f, -h * 0.3f) + new Vector2(0f, -w * 0.62f);
			Glow.Flash(c.Front, stack, Phantom.Hot, w * 2.2f, 0.1f);
			Phantom.Shards(c.Front, stack, 8, 200f * u, 500f * u, Vector2.Up, 80f, 0.25f, 400f * u, 16f * u);
			for (int j = 0; j < 3; j++) ToHand(c, stack + new Vector2(j * 2f, -j * 2f), 0.02f * j, 0.4f, 0f, h * 0.2f, 0.8f, 6.3f);
		});
	}

	/// <summary>一张牌四角扣上的白色角括号（倾斜 ang，牌宽 w），一下扣紧、停一会儿淡掉。</summary>
	private static void Brackets(Control layer, Vector2 at, float ang, float w, float u)
	{
		Node2D root = Fx.Root(layer, at, "P5Retain", 0.7);
		root.Rotation = ang;
		Vector2 half = new(w * 0.62f, w * 0.84f);
		float arm = w * 0.22f, th = Mathf.Max(1.5f, 2.6f * u);
		var corners = new List<(Node2D N, Vector2 S)>();
		for (int i = 0; i < 4; i++)
		{
			Vector2 sg = new(i % 2 == 0 ? -1f : 1f, i < 2 ? -1f : 1f);
			Node2D n = new();
			root.AddChild(n);
			Vector2[] L = { Vector2.Zero, new(-sg.X * arm, 0f), new(-sg.X * arm, -sg.Y * th), new(-sg.X * th, -sg.Y * th), new(-sg.X * th, -sg.Y * arm), new(0f, -sg.Y * arm) };
			Fx.Poly(n, L, new Color(1f, 0.3f, 0.35f, 0.8f), additive: true).Scale = Vector2.One * 1.3f;
			Fx.Poly(n, L, Phantom.Hot, additive: true);
			corners.Add((n, sg));
		}
		Fx.Animate(root, 0, 0.6, t =>
		{
			float k = Ease5.OutBack(Ease5.Seg(t, 0f, 0.25f));
			foreach (var (n, sg) in corners) n.Position = sg * half * Mathf.Lerp(1.5f, 1f, k);
			root.Modulate = new Color(1f, 1f, 1f, 1f - Ease5.Seg(t, 0.6f, 0.4f));
		});
	}

	// ═══════════════════════════ 本能反应：快 ═══════════════════════════

	/// <summary>
	/// 本能反应（抽牌）：快到看不清——手边一下白闪、几根速度线往后甩，牌几乎是直线「射」进手牌区：每张中途「闪现」一次
	/// （原地留下一个淡下去的残影、往前跳一段），拖着拉长的红白光痕；一张紧跟一张。约 0.5s。
	/// </summary>
	public static void Reflex(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face, w = CardW(c);
		Vector2 hand = SkillFx.HandArea(c.Front);
		Glow.Flash(c.Front, c.Hand, Phantom.Hot, h * 0.3f, 0.08f);
		Phantom.Shards(c.Front, c.Hand, 10, 600f * u, 1200f * u, new Vector2(-f, 0.1f), 18f, 0.18f, 0f, 30f * u);
		S("gx_whoosh", 0.24f);
		for (int i = 0; i < 3; i++)
		{
			int k = i;
			float d = 0.06f * k;
			Vector2 start = c.Hand + new Vector2(f * h * 0.12f, -h * (0.05f + 0.06f * k));
			Vector2 mid = start.Lerp(hand, 0.45f) + new Vector2(f * h * 0.15f, -h * 0.12f);
			DrawTick(k);
			Phantom.P5Card a = Phantom.Card(c.Front, start, w, d + 0.4, 1.1f);
			a.Root.Visible = false;
			Fx.Animate(a.Root, d, 0.12, t =>
			{
				a.Root.Visible = true;
				a.Place(start.Lerp(mid, Ease5.OutQuint(t)), f * 0.6f, t * 6f, 0.9f);
			});
			Fx.After(c.Front, d + 0.12f, () =>
			{
				// 闪现：原地留一个白亮的残影淡掉，牌向前跳一段
				a.Set("glow", 1f);
				Fx.Animate(a.Root, 0, 0.12, t => a.Set("alpha", 1f - t));
				a.Trail?.Kill();
				Glow.Flash(c.Front, mid, Phantom.Hot, w * 1.8f, 0.08f);
				Vector2 jump = mid.Lerp(hand, 0.35f);
				Phantom.Shards(c.Front, mid, 6, 300f * u, 700f * u, (jump - mid).Normalized(), 20f, 0.2f, 0f, 22f * u);
				S("gx_ping", 0.08f);
				ToHand(c, jump, 0f, 0.18f, f * 0.6f, h * 0.02f, 1.2f, 4f);
			});
		}
	}

	// ═══════════════════════════ 后空翻：身后一道翻过去的弧光 ═══════════════════════════

	/// <summary>
	/// 后空翻（格挡 + 抽 2）：身后甩出一道竖着翻过去的红白弧光（像他翻了一圈留下的刀光），弧顶甩出两张牌、翻着落进手牌区，
	/// 落地那一下脚边一圈扁的白波和贴地迸开的火花。约 0.9s。格挡的蜂巢盾由格挡钩子另放。
	/// </summary>
	public static void Backflip(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		Control back = c.Back != null && GodotObject.IsInstanceValid(c.Back) ? c.Back : c.Front;
		Vector2 hub = c.Feet + new Vector2(-f * h * 0.12f, -h * 0.62f);
		Paint.Sweep(back, "slash_thin", hub, Mathf.Pi * 0.5f, new Vector2(0.75f, 0.75f) * u * h / 300f, Lut5.Blood, 0.05f, 0.04f, 0.28f, -45f * f, 0.02f, 1.25f);
		Glow.Flash(back, hub + new Vector2(0f, -h * 0.4f), Phantom.Red, h * 1.1f, 0.22f);
		S("gx_whoosh", 0.32f);
		S("gx_pop", 0.1f, 0.2f);
		Vector2 top = hub + new Vector2(-f * h * 0.05f, -h * 0.62f);
		for (int i = 0; i < 2; i++)
		{
			int k = i;
			float d = 0.1f + 0.07f * k;
			DrawTick(k, d);
			Fx.After(c.Front, d, () => Glow.Glint(c.Front, top, 0.5f));
			ToHand(c, top + new Vector2(f * h * 0.08f * k, 0f), d, 0.55f, f * 0.3f, h * 0.35f, 0.85f, 11f);
		}
		Fx.After(c.Front, 0.2f, () =>
		{
			Glow.Shockwave(c.Front, c.Feet, Phantom.Hot, h * 1.3f, 0.32f, 0.22f);
			Phantom.Shards(c.Front, c.Feet, 8, 200f * u, 500f * u, new Vector2(-1f, -0.2f), 15f, 0.28f, 400f * u, 18f * u);
			Phantom.Shards(c.Front, c.Feet, 8, 200f * u, 500f * u, new Vector2(1f, -0.2f), 15f, 0.28f, 400f * u, 18f * u);
		});
	}

	// ═══════════════════════════ 逃脱计划：一张牌 + 烟弹 ═══════════════════════════

	/// <summary>
	/// 逃脱计划（抽 1，抽到技能加格挡）：一张牌从指间弹到头顶、翻着停住一瞬——脚下「噗」地炸开一团红黑纸屑和一圈白闪（烟弹），
	/// 几根火花贴地往两边窜；牌翻到正面一亮，拉着拖尾冲进手牌区。约 0.9s。
	/// </summary>
	public static void EscapePlan(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face, w = CardW(c);
		Vector2 top = c.Hand + new Vector2(f * h * 0.08f, -h * 0.58f);
		S("gx_card_slide", 0.22f);
		S("gx_whoosh", 0.1f, 0.12f);
		S("gx_ping", 0.12f, 0.42f);
		DrawTick(0);
		Phantom.P5Card card = Phantom.Card(c.Front, c.Hand, w, 0.5, 0.6f);
		Fx.Animate(card.Root, 0, 0.42, t =>
		{
			float s = t * 0.42f;
			Vector2 pos = s < 0.22f ? c.Hand.Lerp(top, Ease5.OutQuint(s / 0.22f)) : top + new Vector2(0f, Mathf.Sin((s - 0.22f) / 0.2f * Mathf.Pi) * -h * 0.02f);
			card.Place(pos, 0f, s < 0.36f ? Mathf.Pi + s * 22f : Mathf.Tau * 2f, 1f);
			card.Set("glow", s > 0.36f ? (s - 0.36f) / 0.06f : 0f);
		});
		Fx.After(c.Front, 0.42f, () => { card.Trail?.Kill(); card.Root.Visible = false; Glow.Glint(c.Front, top, 0.7f); });
		ToHand(c, top, 0.42f, 0.4f, 0f, h * 0.15f, 1f, 6.3f);
		S("gx_pop", 0.3f, 0.24f);
		Fx.After(c.Front, 0.24f, () =>
		{
			Vector2 at = c.Feet + new Vector2(0f, -h * 0.12f);
			Glow.Flash(c.Front, at, Phantom.Hot, h * 0.7f, 0.12f);
			Glow.Shockwave(c.Front, c.Feet, Phantom.Hot, h * 1.4f, 0.3f, 0.25f);
			Phantom.Confetti(c.Front, at, 40, 620f * u, Vector2.Up, 80f, 1.1f, 10f * u);
			Phantom.Confetti(c.Front, at, 16, 420f * u, new Vector2(f, -0.5f), 40f, 0.9f, 8f * u, 0.04f);
			Glow.Shockwave(c.Front, at, Phantom.Red, h * 1.0f, 0.35f, 0.6f, 0.04f);
			Glow.Sparks(c.Front, at, 18, 80f, 320f, 0.6f, new[] { Phantom.Hot, Phantom.Red, new Color(0.2f, 0.02f, 0.05f) }, -60f, 0.03f * u, 0.08f * u, Vector2.Up, 150f);
			Phantom.Shards(c.Front, c.Feet, 8, 300f * u, 700f * u, new Vector2(-1f, -0.1f), 12f, 0.3f, 0f, 24f * u);
			Phantom.Shards(c.Front, c.Feet, 8, 300f * u, 700f * u, new Vector2(1f, -0.1f), 12f, 0.3f, 0f, 24f * u);
			HitStop.Request(0.02);
		});
	}

	/// <summary>
	/// 杂技（抽 3 弃 1）：手腕一抖，三张预告信卡从指间弹出，绕着他甩出一个大翻身弧（身后一道网点斜切色带同时扫过），
	/// 在头顶扇形一顿、闪一下；两张拉着红白拖尾俯冲进手牌区，第三张被一刀斩成两半，切口一道白，两半翻着飞开、碎成纸屑和余烬。约 1.1s。
	/// </summary>
	public static void Acrobatics(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		float w = h * 0.16f;                                    // 牌宽
		Vector2 hub = c.Chest + new Vector2(-f * h * 0.05f, -h * 0.12f);
		float R = h * 0.62f;
		Vector2 handUi = SkillFx.HandArea(c.Front);

		// 出手：指尖一点白闪 + 几根往外弹的火花；身后的斜切色带
		Glow.Flash(c.Front, c.Hand, Phantom.Hot, h * 0.25f, 0.1f);
		Phantom.Shards(c.Front, c.Hand, 8, 300f * u, 700f * u, new Vector2(f, -0.6f), 40f, 0.3f, 600f * u, 22f * u);
		// （试过身后的网点斜切色带：在这张牌上像一根红棍，不加）
		S("gx_card_slide", 0.24f);
		S("gx_whoosh", 0.2f, 0.06f);
		S("gx_ping", 0.12f, 0.36f);
		S("gx_card_slide", 0.16f, 0.46f);

		const float arc = 0.36f, hold = 0.08f;
		for (int i = 0; i < 3; i++)
		{
			int k = i;
			float lag = 0.05f * k;
			// 翻身弧：从手（右下）往后甩过头顶，落在头顶上方的扇形位置
			float a0 = Mathf.Atan2(c.Hand.Y - hub.Y, (c.Hand.X - hub.X)), a1 = -Mathf.Pi / 2f + f * (k - 1) * 0.42f;
			float sweep = -f * (Mathf.Tau * 0.9f);                  // 反着绕一大圈（像翻跟头）
			float aEnd = a0 + sweep;
			aEnd += Mathf.Wrap(a1 - aEnd, -Mathf.Pi, Mathf.Pi);
			Vector2 apex = hub + new Vector2(Mathf.Cos(aEnd), Mathf.Sin(aEnd)) * R * 1.05f;
			bool cut = k == 1;
			float total = arc + hold + (cut ? 0.05f : 0.42f);
			Phantom.P5Card card = Phantom.Card(c.Front, c.Hand, w, lag + total + 0.15, 0.85f);
			card.Root.Visible = false;
			Fx.Animate(card.Root, lag, total, t =>
			{
				card.Root.Visible = true;
				float s = t * total;
				if (s < arc)
				{
					float e = Ease5.OutQuint(s / arc);
					float a = Mathf.Lerp(a0, aEnd, e);
					float rr = Mathf.Lerp(c.Hand.DistanceTo(hub), R * 1.05f, Mathf.Min(1f, e * 1.6f));
					card.Place(hub + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr, a + Mathf.Pi / 2f, e * 12f, 0.7f + 0.3f * e);
					card.Set("sheen", Mathf.PosMod(e * 3f, 1f) * 3f - 1f);
				}
				else if (s < arc + hold)
				{
					float k2 = (s - arc) / hold;
					card.Place(apex, f * (k - 1) * 0.42f, 0f, 1f + 0.12f * Mathf.Sin(k2 * Mathf.Pi));
					card.Set("glow", Mathf.Sin(k2 * Mathf.Pi));
					card.Set("sheen", k2 * 3f - 1f);
					if (card.Trail != null) card.Trail.Fade = 1f - k2;
				}
				else if (!cut)
				{
					float k3 = (s - arc - hold) / 0.42f;
					float e = Fx.InCubic(k3) * 0.75f + k3 * 0.25f;
					Vector2 ctrl = apex + new Vector2(f * h * 0.35f, -h * 0.15f);
					Vector2 p = apex.Lerp(ctrl, e).Lerp(ctrl.Lerp(handUi, e), e);
					if (card.Trail != null) card.Trail.Fade = Mathf.Min(1f, k3 * 3f);
					card.Place(p, f * (k - 1) * 0.42f + e * f * 1.2f, e * 9f, 1f - 0.45f * e);
					card.Set("glow", 0f);
				}
			});
			Fx.After(c.Front, lag + arc, () =>
			{
				Glow.Glint(c.Front, apex + new Vector2(w * 0.4f, -w * 0.6f), 0.55f);
				if (k == 0) Glow.Flash(c.Front, hub + new Vector2(0f, -R), Phantom.Red, h * 0.9f, 0.18f);
			});
			if (!cut)
			{
				S("gx_card_stack", 0.22f, lag + total);
				Fx.After(c.Front, lag + total, () =>
				{
					card.Trail?.Kill();
					card.Root.Visible = false;
					Phantom.Shards(c.Front, handUi, 6, 200f * u, 500f * u, Vector2.Up, 50f, 0.3f, 500f * u, 16f * u);
				});
				continue;
			}
			// 被斩的那张：一刀横斩、短顿帧，两半翻开烧掉、碎成纸屑
			float cutAt = lag + arc + hold + 0.03f;
			S("gx_slice", 0.3f, cutAt);
			S("gx_burn", 0.15f, cutAt + 0.12f);
			Fx.After(c.Front, cutAt, () =>
			{
				card.Trail?.Kill();
				card.Root.Visible = false;
				float ang = f > 0f ? -0.5f : Mathf.Pi + 0.5f;
				Cut.Razor(c.Front, apex, ang, w * 4.2f, 3.5f * u, 0.1f);
				Glow.Flash(c.Front, apex, Phantom.Hot, w * 2.6f, 0.1f);
				HitStop.Request(0.035);
				Fx.Shake(1);
				Phantom.Shards(c.Front, apex, 14, 300f * u, 900f * u, Vector2.Right.Rotated(ang), 25f, 0.35f, 300f * u, 26f * u);
				Phantom.Shards(c.Front, apex, 8, 200f * u, 600f * u, Vector2.Right.Rotated(ang + Mathf.Pi), 25f, 0.3f, 300f * u, 20f * u);
				Vector2 cn = Vector2.Right.Rotated(ang + Mathf.Pi / 2f);   // 切线的法向（屏幕）
				for (int side = -1; side <= 1; side += 2)
				{
					int sd = side;
					Phantom.P5Card half = Phantom.Card(c.Front, apex, w, 0.75);
					float tilt0 = f * (k - 1) * 0.42f;
					// 牌内坐标里的切线法向：把屏幕法向转进牌的坐标（牌旋转 tilt0，宽高比 5:7）
					Vector2 ln = cn.Rotated(-tilt0);
					half.Set("clip", new Vector3(ln.X * sd, ln.Y * sd * 1.4f, 0f));
					Vector2 away = apex + cn * sd * w * 1.6f + new Vector2(f * w * 0.6f, w * 0.8f);
					Fx.Animate(half.Root, 0, 0.7, t =>
					{
						float e = Ease5.OutQuint(t);
						half.Place(apex.Lerp(away, e) + new Vector2(0f, h * 0.35f * t * t), tilt0 + sd * e * 1.4f, sd * e * 1.2f, 1f);
						half.Set("burn", Mathf.Clamp((t - 0.1f) / 0.7f, 0f, 1f));
					});
					Fx.After(c.Front, 0.32f, () => Phantom.Confetti(c.Front, away, 10, 260f * u, cn * sd, 60f, 0.9f, 7f * u));
				}
				Glow.Sparks(c.Front, apex, 12, 40f, 160f, 0.6f, new[] { Phantom.Hot, Phantom.Red, new Color(0.3f, 0f, 0.05f) }, -80f, 0.02f * u, 0.05f * u, Vector2.Up, 120f);
			});
		}
	}
}
