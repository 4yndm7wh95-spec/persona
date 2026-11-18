using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 0.9.49 审阅第二批（2026-11-17）：跟踪（小改）、磨蚀（大改）、触媒（大改）、萎靡（大改）、谋划专家（新做，原来走通用积木）。
/// 雷点照旧：不用贴图拼形状、不用棱角分明的多边形、不用光束 / 大片小光点 / 围着敌人转的光环、敌人那里不做画面扭曲；节奏慢快慢、音效克制。
/// 2026-11-18 用户看第二批：红焰（触媒、萎靡）太像贴图 → 拉黑，火焰着色器整个删掉；磨蚀只有光环当主体不行 → 光环降成点缀、另做主体。
/// </summary>
public static partial class SkillRedo5
{
	public static void RegisterBatch10(Dictionary<string, Action<SkillCtx>> cards)
	{
		cards["Tracking"] = Tracking10;
		cards["Abrasive"] = Abrasive10;
		cards["Accelerant"] = Accelerant10;
		cards["Malaise"] = Malaise10;
		cards["MasterPlanner"] = MasterPlanner;
	}

	/// <summary>贴身红残影往外一胀（肾上腺素心跳那种）：冻住此刻的他，k = 放大比例。</summary>
	private static void EchoPulse(SkillCtx c, float k, float dur = 0.36f, float alpha = 0.8f)
	{
		if (RedEcho(c, dur + 0.04) is not { } echo) return;
		var (sp, m) = echo;
		Vector2 pivot = c.Chest - c.Region.Position;
		Fx.Animate(sp, 0, dur, t =>
		{
			float sc = 1f + k * Fx.OutCubic(t);
			sp.Scale = Vector2.One * sc;
			sp.Position = pivot * (1f - sc);
			m.SetShaderParameter("fill", 0.35f);
			m.SetShaderParameter("smear", 0f);
			m.SetShaderParameter("alpha", alpha * (1f - t));
		});
	}

	private static Control BackOf(SkillCtx c) => c.Back != null && GodotObject.IsInstanceValid(c.Back) ? c.Back : c.Front;

	// ═══════════════════════════ 跟踪（小改）═══════════════════════════

	/// <summary>
	/// 跟踪（虚弱的敌人受到的攻击伤害 +50%；审阅：还不错但太简约 → 小改）：原来的「面具寒光 → 细红线连到每个敌人 → 准星锁住」保留；
	/// 加上：扫的时候四周稍微暗下来、他身后一团暗红慢慢亮起；每锁住一个，敌人身上一下红光（不是光环）；全部锁完，面具上一颗更大的星光、身后红光一跳。
	/// </summary>
	public static void Tracking10(SkillCtx c)
	{
		float h = c.Tall;
		Control back = BackOf(c);
		var targets = new List<SkillFx.Victim>(c.Enemies);
		if (targets.Count == 0 && c.Target is SkillFx.Victim only) targets.Add(only);
		float end = 0.07f + 0.11f * Mathf.Max(0, targets.Count - 1) + 0.12f + 0.24f;
		Vector2 mid = c.Feet + new Vector2(0f, -h * 0.55f);
		Dim(back, mid + new Vector2(c.Face * h * 1.5f, 0f), new Vector2(h * 6f, h * 3f), end + 0.6, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.15f)) * (1f - Ease5.Seg(s, 0.75f, 0.25f)) * 0.55f);
		Glow.Flash(back, c.Mask, new Color(0.8f, 0.04f, 0.12f), h * 1.2f, end + 0.3f);
		CardSkillFx.Tracking(c);
		for (int i = 0; i < targets.Count; i++)
		{
			SkillFx.Victim v = targets[i];
			Vector2 at = v.Feet.Lerp(v.Top, 0.55f);
			float lockAt = 0.07f + 0.11f * i + 0.12f + 0.24f;
			Fx.After(c.Front, lockAt, () => Glow.Flash(c.Front, at, Red, Mathf.Max(v.Width, h * 0.5f) * 1.1f, 0.25f));
		}
		Fx.After(c.Front, end + 0.04f, () =>
		{
			Glow.Glint(c.Front, c.Mask + new Vector2(c.Face * h * 0.03f, 0f), 1.3f);
			Glow.Flash(back, c.Mask, Red, h * 1.5f, 0.35f);
		});
	}

	// ═══════════════════════════ 磨蚀（大改，第三版）═══════════════════════════

	/// <summary>
	/// 磨蚀（敏捷 + 荆棘）。第二版只有绕身风刃（光环当主体）被否 → 第三版「磨刃」：主体是手上磨出来的火花，风刃降成点缀。
	/// 他扯手套，指尖像在磨刀石上刮过：
	/// 慢：第一下「嚓」—— 一道白热的短疾光沿着手划过去，一路往后下方喷出一小串带重力的火花；
	/// 快：第二下更长更狠 —— 疾光更长、火花喷成一大扇（像砂轮），手上白闪、顿帧轻震、身上一下红光；
	///     同一瞬间从他全身往外迸出一圈尖头火花（荆棘：谁碰谁挨扎），三道细风刃贴身转一圈就散（敏捷，点缀）；
	/// 慢：手上留着一点余热，火花落地前淡掉。
	/// </summary>
	public static void Abrasive10(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		Control back = BackOf(c), front = c.Front;
		const float s1 = 0.14f, beat = 0.44f;
		Vector2 mid = c.Feet + new Vector2(0f, -h * 0.55f);
		Vector2 hand = c.Hand;
		S("gx_slice", 0.14f, s1);
		S("gx_slice", 0.26f, beat);
		Dim(back, mid, new Vector2(h * 3.4f, h * 2.8f), beat + 0.8, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.15f)) * (1f - Ease5.Seg(s, 0.7f, 0.3f)) * 0.6f);
		Glow.Flash(back, mid, new Color(0.7f, 0.03f, 0.12f), h * 1.5f, beat + 0.15f);
		Gather(front, hand, h, u, s1, 10, 0.42f);                     // 蓄力：光线往手上收
		Grind(c, hand, s1, 0.12f, h * 0.32f, 4, 8, 0.75f);
		Gather(front, hand, h, u, beat - 0.04f, 8, 0.32f);
		Grind(c, hand, beat - 0.05f, 0.16f, h * 0.62f, 6, 14, 1.25f);
		Fx.After(front, s1, () => Glow.Flash(front, hand, Hot, h * 0.18f, 0.08f));
		if (c.SelfTint is { } tint)
			Fx.Animate(front, 0, beat + 0.6, t =>
			{
				float s = t * (beat + 0.6f);
				float k = 0.3f * Mathf.Exp(-Mathf.Pow((s - s1 - 0.04f) / 0.05f, 2f)) + 0.9f * Mathf.Exp(-Mathf.Pow((s - beat - 0.04f) / 0.07f, 2f));
				tint(Colors.White.Lerp(new Color(1.5f, 0.75f, 0.8f), k));
			});
		Fx.After(front, beat + 0.04f, () =>
		{
			HitStop.Request(0.045);
			Fx.Shake(1);
			Glow.Flash(front, hand, Hot, h * 0.4f, 0.12f);
			Glow.Flash(back, mid, new Color(1f, 0.12f, 0.2f), h * 2.2f, 0.4f);
			Cross(front, hand, h * 0.32f);
			Glow.Glint(front, hand + new Vector2(f * h * 0.05f, -h * 0.04f), 1.1f);
			EchoPulse(c, 0.07f);                                         // 贴身红残影往外一胀：带刺的轮廓
			// 荆棘：全身往外迸的尖头火花（不是放射直线，是会减速、带一点重力的碎光）
			Phantom.Shards(front, mid, 12, 480f * u, 820f * u, Vector2.Up, 180f, 0.3f, 400f * u, 13f * u);
		});
		// 点缀：三道细风刃贴身转一圈就散
		Vortex(front, back, c.Feet, h, h * 0.42f, 3, beat + 0.02f, 0.42f, f, 0.2f, 0.85f, 0.7f, 0.05f);
		// 火花落到地上溅起的小火星
		for (int i = 0; i < 5; i++)
		{
			float d = beat + 0.2f + 0.05f * i + Fx.Rand(0f, 0.04f);
			Vector2 g = new(hand.X - f * h * Fx.Rand(0.05f, 0.4f), c.Feet.Y + Fx.Rand(-0.02f, 0.03f) * h);
			Fx.After(front, d, () => Glow.Flash(front, g, new Color(1f, 0.4f, 0.4f), h * 0.07f, 0.12f));
			Phantom.Shards(front, g, 3, 120f * u, 260f * u, Vector2.Up, 50f, 0.22f, 900f * u, 7f * u, d);
		}
		// 手上的余热：一层暖光 + 几粒慢慢往上飘的火星
		Fx.After(front, beat + 0.2f, () => Glow.Flash(front, hand, new Color(1f, 0.35f, 0.3f), h * 0.22f, 0.45f));
		Phantom.Shards(front, hand, 5, 60f * u, 140f * u, Vector2.Up, 30f, 0.6f, -80f * u, 8f * u, beat + 0.25f);
	}

	/// <summary>
	/// 磨一下：t0 起、dur 秒内，一道白热疾光从手往前上方划过 len；划的时候分 bursts 次沿路喷火花（往后下方，带重力）。power 放大火花。
	/// </summary>
	private static void Grind(SkillCtx c, Vector2 hand, float t0, float dur, float len, int bursts, int perBurst, float power)
	{
		float u = c.Unit, f = c.Face;
		Vector2 dir = new Vector2(f * 0.92f, -0.4f).Normalized();
		Vector2 from = hand - dir * len * 0.35f, to = hand + dir * len * 0.65f;
		Vector2 bow = new Vector2(-dir.Y, dir.X) * len * 0.08f * f;
		Func<float, Vector2> path = e => from.Lerp(to, e) + bow * 4f * e * (1f - e);
		DashFly(c.Front, path, t0, dur + 0.1f, len * 0.6f, 6f * u * power, new Color(1f, 0.25f, 0.3f), 1.4f, Ease5.OutQuint, 0.6f, 1f);
		Vector2 spray = new Vector2(-f * 0.7f, 0.7f).Normalized();
		for (int i = 0; i < bursts; i++)
		{
			float e = (i + 0.5f) / bursts;
			Vector2 at = path(Ease5.OutQuint(e));
			Phantom.Shards(c.Front, at, perBurst, 300f * u * power, 820f * u * power, spray, 38f, 0.5f, 1500f * u, 15f * u * power, t0 + dur * e);
		}
	}

	// ═══════════════════════════ 触媒（大改，第三版）═══════════════════════════

	/// <summary>
	/// 触媒（中毒额外触发一次）。第二版的红焰太像贴图被拉黑 → 第三版「毒心加速」，不用火。
	/// 他挥手，伸直的掌心上方浮起涂毒那团翻滚的咒毒（用户认可的画法），越转越快（催化）；
	/// 第一跳：毒团一胀、身上一下淡红、手边一圈很小的波；
	/// 第二跳（主拍）：毒团猛地胀大一圈再缩回，掌心白闪、身后一大团暗红、脚下一圈扁冲击波、往上迸一把碎光、顿帧轻震 —— 两跳 = 毒多触发一次；
	/// 然后毒团转着收进掌心。只在他身上，不连到敌人。
	/// </summary>
	public static void Accelerant10(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		Control back = BackOf(c), front = c.Front;
		Vector2 palm = c.Feet + new Vector2(f * h * 0.28f, -h * 0.8f);    // 挥手伸直的手
		Vector2 orb = palm + new Vector2(0f, -h * 0.1f);
		const float b1 = 0.1f, b2 = 0.26f, life = 0.44f;
		S("gx_pop", 0.12f, b1);
		S("gx_pop", 0.22f, b2);
		S("gx_whoosh", 0.1f, b2 + 0.02f);
		Vector2 mid = c.Feet + new Vector2(0f, -h * 0.6f);
		Dim(back, mid, new Vector2(h * 3.4f, h * 3f), life + 0.6, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.2f)) * (1f - Ease5.Seg(s, 0.7f, 0.3f)) * 0.65f);
		Glow.Flash(back, orb, new Color(0.6f, 0.02f, 0.1f), h * 1.3f, b2 + 0.1f);
		Gather(front, orb, h, u, b2, 12, 0.3f);                       // 光线往毒团收（催化），收得紧一些，不铺满画面
		Node2D root = Fx.Root(front, orb, "P5Catalyst", life + 0.1);
		ColorRect q = CardSkillFx.VenomBlob(root, h * 0.42f);
		float clock = 0f, last = 0f;
		Fx.Animate(q, 0, life, t =>
		{
			float s = t * life;
			// 越转越快：转速从 1 倍升到 5 倍，第二跳之后慢下来
			float speed = Mathf.Lerp(1f, 5f, Ease5.Seg(s, 0f, b2)) * (1f - 0.7f * Ease5.Seg(s, b2, 0.3f));
			clock += (s - last) * speed;
			last = s;
			float p1 = 0.14f * Mathf.Exp(-Mathf.Pow((s - b1) / 0.03f, 2f));
			float p2 = 0.42f * Mathf.Exp(-Mathf.Pow((s - b2) / 0.04f, 2f));
			float grow = Ease5.OutBack(Ease5.Seg(s, 0f, 0.1f)) * (1f + p1 + p2) * (1f - 0.9f * Fx.InCubic(Ease5.Seg(s, life - 0.14f, 0.14f)));
			((ShaderMaterial)q.Material).SetShaderParameter("clock", clock);
			((ShaderMaterial)q.Material).SetShaderParameter("grow", grow);
			((ShaderMaterial)q.Material).SetShaderParameter("alpha", 1f - Ease5.Seg(s, life - 0.18f, 0.18f));
			root.GlobalPosition = orb.Lerp(palm, Fx.InCubic(Ease5.Seg(s, life - 0.14f, 0.14f)));
		});
		Fx.After(front, b1, () =>
		{
			Glow.Flash(front, orb, new Color(1f, 0.55f, 0.55f), h * 0.25f, 0.1f);
			Glow.Flash(back, mid, new Color(0.7f, 0.03f, 0.12f), h * 1.4f, 0.25f);
			EchoPulse(c, 0.04f, 0.26f, 0.5f);
		});
		Fx.After(front, b2, () =>
		{
			HitStop.Request(0.04);
			Fx.Shake(1);
			Glow.Flash(front, orb, Hot, h * 0.4f, 0.12f);
			Glow.Flash(back, mid, Red, h * 2.5f, 0.45f);
			Phantom.Shards(front, orb, 16, 360f * u, 800f * u, Vector2.Up, 75f, 0.45f, 700f * u, 18f * u);
			Cross(front, orb, h * 0.45f);
			EchoPulse(c, 0.09f);
		});
		// 收进掌心：一点星光；然后毒液从手上滴几滴下去
		Fx.After(front, life - 0.02f, () => Glow.Glint(front, palm, 0.8f));
		for (int i = 0; i < 4; i++)
			CardSkillFx.Drop(front, palm + new Vector2(Fx.Rand(-0.05f, 0.05f) * h, h * 0.05f), c.Feet.Y + Fx.Rand(-0.02f, 0.03f) * h, b2 + 0.08f + 0.11f * i, h, u);
		if (c.SelfTint is { } tint)
			Fx.Animate(front, 0, life + 0.6, t =>
			{
				float s = t * (life + 0.6f);
				float k = 0.35f * Mathf.Exp(-Mathf.Pow((s - b1) / 0.06f, 2f)) + 0.95f * Mathf.Exp(-Mathf.Pow((s - b2) / 0.05f, 2f));
				k = Mathf.Max(k, s > b2 ? 0.95f * Mathf.Exp(-(s - b2) * 4f) : 0f);
				tint(Colors.White.Lerp(new Color(1.6f, 0.7f, 0.75f), k));
			});
	}

	// ═══════════════════════════ 萎靡（大改，第三版）═══════════════════════════

	/// <summary>
	/// 萎靡（敌人失去力量 + 虚弱）。第二版的黑焰太像贴图被拉黑；审阅还要求：敌人那里别扭曲画面、别用小光点和光环 → 第三版「怪盗夺力」。
	/// 慢：他指向敌人，一张预告信从手里甩出去（带拖尾、翻着飞），「噌」地钉在敌人胸口，白闪；
	/// 停一下：预告信越来越亮，敌人身上慢慢暗下来（力量被吸到牌上）；
	/// 快：预告信被猛地抽回来，拖出一条长长的红光带（偷走的力量），敌人身上一下变灰、往下一沉的暗闪、顿帧；
	/// 慢：牌飞回来收进他胸口，十字光、身上一层红残影；敌人保持灰暗一会儿再慢慢恢复。
	/// </summary>
	public static void Malaise10(SkillCtx c)
	{
		if (c.Target is not SkillFx.Victim v) return;
		float jh = c.Tall, u = c.Unit, f = c.Face, h = Mathf.Max(120f, (v.Feet - v.Top).Length());
		Control back = BackOf(c), front = c.Front;
		const float throwAt = 0.04f, hit = 0.36f, yank = 0.76f, home = 1.08f;
		float w = jh * 0.2f;
		Vector2 hand = c.Hand, chest = v.Feet.Lerp(v.Top, 0.58f);
		Vector2 heart = c.Chest;   // 抽回来时他的手已经放下了 → 牌收进胸口
		S("gx_card_slide", 0.16f, throwAt);
		S("gx_ping", 0.18f, hit);
		S("gx_whoosh", 0.18f, yank);
		S("gx_card_stack", 0.12f, home);
		Phantom.P5Card card = Phantom.Card(front, hand, w, home + 0.1);
		card.Root.Visible = false;
		Vector2 ctrlOut = hand.Lerp(chest, 0.5f) + new Vector2(0f, -jh * 0.7f);
		Vector2 ctrlBack = heart.Lerp(chest, 0.5f) + new Vector2(0f, jh * 0.2f);
		Fx.Animate(card.Root, throwAt, home - throwAt, t =>
		{
			card.Root.Visible = true;
			float s = throwAt + t * (home - throwAt);
			if (s < hit)
			{
				float e = Fx.OutCubic((s - throwAt) / (hit - throwAt));
				card.Place(hand.Lerp(ctrlOut, e).Lerp(ctrlOut.Lerp(chest, e), e), f * (0.3f + (1f - e) * 9f), 0f, 0.7f + 0.5f * e);
			}
			else if (s < yank)
			{
				float k = (s - hit) / (yank - hit);
				card.Place(chest + new Vector2(Mathf.Sin(s * 60f) * k * u * 2f, 0f), f * 0.3f, 0f, 1.2f);
				card.Set("glow", 0.3f + 0.7f * k * k);
			}
			else
			{
				float e = Fx.InCubic(Mathf.Min(1f, (s - yank) / (home - yank)));
				card.Place(chest.Lerp(ctrlBack, e).Lerp(ctrlBack.Lerp(heart, e), e), f * (0.3f - e * 7f), 0f, 1.2f - 0.6f * e);
				card.Set("glow", 1f);
			}
		});
		Fx.After(front, throwAt, () =>
		{
			Glow.Flash(front, hand, Hot, jh * 0.18f, 0.08f);
			Glow.Glint(front, hand, 0.7f);
		});
		Fx.After(front, hit, () =>
		{
			Phantom.Shards(front, chest, 8, 260f * u, 560f * u, (chest - hand).Normalized(), 55f, 0.3f, 400f * u, 12f * u);
			Glow.Flash(front, chest, Hot, h * 0.3f, 0.1f);
			Glow.Flash(back, chest, Red, h * 1.1f, 0.3f);
			Glow.Glint(front, chest + new Vector2(w * 0.4f, -w * 0.7f), 0.7f);
		});
		// 吸：一波波细光线从敌人身上往牌里收（力量被吸到牌上）
		for (int i = 0; i < 3; i++)
			Gather(front, chest, h, u, hit + 0.16f + 0.12f * i, 7 + 2 * i, 0.55f + 0.1f * i);
		// 抽回：牌后面跟着一团柔红光（偷走的力量），从敌人身上带出几缕碎光
		Node2D loot = Fx.Root(front, chest, "P5MalaiseLoot", home + 0.1);
		Sprite2D lootGlow = Glow.SoftSprite(loot, Vector2.Zero, new Color(1f, 0.12f, 0.2f, 0.9f), 5f * u);
		Sprite2D lootCore = Glow.SoftSprite(loot, Vector2.Zero, new Color(1f, 0.85f, 0.85f, 0.9f), 1.8f * u);
		loot.Visible = false;
		Fx.Animate(loot, hit, home - hit, t =>
		{
			float s = hit + t * (home - hit);
			loot.Visible = true;
			loot.GlobalPosition = card.Root.GlobalPosition;
			float k = s < yank ? Ease5.Seg(s, hit, yank - hit) * 0.6f : 1f;
			loot.Modulate = new Color(1f, 1f, 1f, k);
			loot.Scale = Vector2.One * (0.6f + 0.6f * k);
		});
		Fx.After(front, yank, () => Phantom.Shards(front, chest, 9, 300f * u, 700f * u, (heart - chest).Normalized(), 25f, 0.35f, 0f, 16f * u));
		Fx.After(front, yank, () =>
		{
			HitStop.Request(0.04);
			Fx.Shake(1);
			Glow.Flash(front, chest, Hot, h * 0.45f, 0.12f);
			Glow.Flash(back, v.Feet.Lerp(v.Top, 0.35f), new Color(0.32f, 0.02f, 0.06f), h * 2.0f, 0.5f);
		});
		Fx.After(front, home, () =>
		{
			Glow.Flash(front, heart, Hot, jh * 0.25f, 0.1f);
			Glow.Flash(back, heart, Red, jh * 0.9f, 0.35f);
			Cross(front, heart, jh * 0.4f);
			EchoPulse(c, 0.06f, 0.3f, 0.6f);
			Phantom.Shards(front, heart, 7, 200f * u, 450f * u, Vector2.Up, 60f, 0.28f, 300f * u, 14f * u);
		});
		if (v.Tint is { } vt)
		{
			Node2D driver = Fx.Root(front, v.Feet, "P5MalaiseTint", home + 1.0);
			Fx.Animate(driver, 0, home + 0.9, t =>
			{
				float s = t * (home + 0.9f);
				float dark = 0.35f * Ease5.Seg(s, hit, yank - hit) + 0.65f * Ease5.Seg(s, yank, 0.08f);
				dark *= 1f - 0.8f * Ease5.Seg(s, home + 0.2f, 0.7f);
				vt(Colors.White.Lerp(new Color(0.46f, 0.4f, 0.44f), dark));
			});
		}
		if (c.SelfTint is { } tint)
			Fx.After(front, home, () => Fx.Animate(front, 0, 0.5, t => tint(Colors.White.Lerp(new Color(1.5f, 0.75f, 0.8f), (1f - t) * (1f - t)))));
	}

	// ═══════════════════════════ 谋划专家（新做）═══════════════════════════

	/// <summary>
	/// 谋划专家（打出技能牌时该牌获得奇巧；原来走通用积木太简单 → 新做）：「排兵布阵」。
	/// 慢：五张红黑的预告牌从他手里一张张弹出去，在他头顶后方摊成一道扇面（带拖尾）；停一下，牌面从左到右依次一亮（每张都被「做了手脚」= 奇巧）；
	/// 快：五张一齐收拢叠成一张、白闪、短顿帧；慢：那一张翻着落回他手里，化成一点红光收进掌心。
	/// </summary>
	public static void MasterPlanner(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face, w = h * 0.16f;
		Control back = BackOf(c);
		Vector2 hub = c.Feet + new Vector2(-f * h * 0.08f, -h * 1.28f);
		const int n = 5;
		const float out0 = 0.04f, step = 0.06f, fly = 0.22f, mark0 = 0.42f, markStep = 0.06f, gather = 0.82f, home = 1.12f;
		S("gx_card_slide", 0.16f);
		S("gx_card_stack", 0.16f, gather);
		Glow.Flash(back, hub, new Color(0.75f, 0.03f, 0.12f), h * 1.6f, home + 0.3f);
		Vector2 palm = c.Hand + new Vector2(f * h * 0.04f, 0f);
		for (int i = 0; i < n; i++)
		{
			int k = i;
			float ang = -Mathf.Pi * 0.5f + f * (k - 2f) * 0.42f;
			Vector2 slot = hub + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * h * 0.42f + new Vector2(0f, h * 0.18f);
			float tilt = f * (k - 2f) * 0.3f;
			float d = out0 + step * k, markAt = mark0 + markStep * k;
			Phantom.P5Card card = Phantom.Card(c.Front, palm, w, home + 0.2, 0.8f);
			card.Root.Visible = false;
			Vector2 ctrl = palm.Lerp(slot, 0.5f) + new Vector2(f * h * 0.25f, -h * 0.1f);
			Fx.Animate(card.Root, d, home - d, t =>
			{
				card.Root.Visible = true;
				float s = d + t * (home - d);
				if (s < d + fly)
				{
					float e = Ease5.OutQuint((s - d) / fly);
					card.Place(palm.Lerp(ctrl, e).Lerp(ctrl.Lerp(slot, e), e), tilt * e, (1f - e) * 8f, 0.6f + 0.4f * e);
				}
				else if (s < gather)
				{
					card.Place(slot + new Vector2(0f, Mathf.Sin(s * 5f + k) * h * 0.008f), tilt, 0f, 1f);
					float m = s >= markAt ? Mathf.Exp(-(s - markAt) * 7f) : 0f;
					card.Set("glow", m);
					card.Set("sheen", s >= markAt ? Mathf.Min(1f, (s - markAt) / 0.15f) * 3f - 1f : -1f);
					if (card.Trail != null) card.Trail.Fade = 1f - Mathf.Min(1f, (s - d - fly) / 0.1f);
				}
				else
				{
					float e = Fx.InCubic(Mathf.Min(1f, (s - gather) / 0.1f));
					card.Place(slot.Lerp(hub, e), tilt * (1f - e), 0f, 1f);
					card.Set("glow", e);
					if (k != 2 && e >= 1f) card.Root.Visible = false;
				}
			});
			Fx.After(c.Front, markAt, () => Glow.Glint(c.Front, slot + new Vector2(w * 0.3f, -w * 0.55f), 0.5f));
			Fx.After(c.Front, home, () => { card.Trail?.Kill(); card.Root.Visible = false; });
		}
		Fx.After(c.Front, gather + 0.1f, () =>
		{
			HitStop.Request(0.03);
			Glow.Flash(c.Front, hub, Hot, w * 3f, 0.1f);
			Glow.Flash(back, hub, Red, h * 1.2f, 0.3f);
			Glow.Glint(c.Front, hub, 1f);
		});
		// 叠成的那一张翻着落回手里
		Phantom.P5Card last = Phantom.Card(c.Front, hub, w, home + 0.6, 0.9f);
		last.Root.Visible = false;
		Fx.Animate(last.Root, gather + 0.1f, home + 0.35f - gather - 0.1f, t =>
		{
			last.Root.Visible = true;
			float e = Fx.InCubic(t) * 0.7f + t * 0.3f;
			Vector2 ctrl2 = hub.Lerp(palm, 0.5f) + new Vector2(f * h * 0.3f, -h * 0.05f);
			last.Place(hub.Lerp(ctrl2, e).Lerp(ctrl2.Lerp(palm, e), e), e * f * 1.2f, e * 10f, 1f - 0.5f * e);
		});
		Fx.After(c.Front, home + 0.35f, () =>
		{
			last.Trail?.Kill();
			last.Root.Visible = false;
			Glow.Flash(c.Front, palm, Hot, h * 0.25f, 0.1f);
			Glow.Flash(back, palm, Red, h * 0.9f, 0.3f);
			Phantom.Shards(c.Front, palm, 6, 200f * u, 450f * u, Vector2.Up, 60f, 0.25f, 300f * u, 14f * u);
		});
	}
}
