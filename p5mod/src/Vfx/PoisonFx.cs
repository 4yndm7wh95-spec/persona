using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 被打的人物本体的「原色」：受击闪、中毒染色都从这里恢复，几个特效同时改 modulate 也不会把颜色卡在半路。
/// </summary>
public static class BodyTint
{
	private static readonly Dictionary<ulong, Color> Rest = new();

	public static Color RestOf(Node2D body)
	{
		ulong id = body.GetInstanceId();
		if (!Rest.TryGetValue(id, out Color c))
		{
			c = body.Modulate;
			if (Rest.Count > 64)
			{
				Rest.Clear();
			}
			Rest[id] = c;
		}
		return c;
	}

	/// <summary>染成 tint（乘在原色上）停 hold 秒，再用 fade 秒回到原色。真实时间，不受顿帧影响。</summary>
	public static void Pulse(Node2D? body, Color tint, double hold, double fade)
	{
		if (body == null || !GodotObject.IsInstanceValid(body))
		{
			return;
		}
		Color rest = RestOf(body);
		Tween tw = body.CreateTween().SetIgnoreTimeScale(true);
		tw.TweenProperty(body, "modulate", rest * tint, 0.06);
		tw.TweenInterval(hold);
		tw.TweenProperty(body, "modulate", rest, fade).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
	}
}

/// <summary>
/// 第四版毒组（咒怨 Eiha：P5 原作的黑红咒火）。用户 2026-11-02：猎人两大流派之一，毒的特效是重点；
/// 施毒和每回合毒伤打得非常频繁，所以短而干净，但材质不降级。不画任何几何形状：
/// · 黑红咒火：原版火焰序列帧换「反向」咒怨 LUT（火芯越亮越黑、边缘亮红），从目标脚下 / 身上舔起来；
/// · 怨灵：P5R 原版的怨灵贴图（be0094soul3 / be217_ghost2）带着扭曲飘起、从边缘烧散；
/// · 底下一层暗红辉光、原版火星（咒怨 LUT）；目标被染成暗红再褪回。
/// 施毒 = PoisonApplyPatch（游戏给敌人加中毒 / 叠毒那一刻，所有毒牌、涂毒、腐蚀波、毒雾都走这里）；
/// 毒伤 = PoisonTickPatch（每轮毒伤结算）。
/// </summary>
public static class PoisonFx
{
	/// <summary>
	/// 一团黑红咒火：底下一层加法的红色径向光（着色器，发光要用加法，半透明混合叠在背景上会变成橙棕色），
	/// 上面原版火焰序列帧用反向 LUT 画成黑芯红边，再错开叠一团更小的让火更密。
	/// </summary>
	private static void CurseFlame(Control layer, string scene, Vector2 at, float scale, float delay = 0f)
	{
		Glow.Flash(layer, at + new Vector2(0f, -60f * scale), new Color(1f, 0.04f, 0.12f), 300f * scale, 0.45f, delay);
		Vanilla.Burst(layer, scene, at, 0f, scale, Lut5.CurseInk, delay: delay);
		Vanilla.Burst(layer, scene, at + new Vector2(Fx.Rand(-30f, 30f) * scale, 6f), 0f, scale * 0.7f, Lut5.CurseInk, delay: delay + 0.07f);
	}

	/// <summary>
	/// 施毒。亚森刚出过手（ArseneFx.TryClaw）：咒怨从他的爪尖甩出去——一道黑红的咒怨光束 0.08s 扫到敌人，沿路拖着怨灵——再在敌人身上炸开；
	/// 没有召唤（带毒刺击、涂毒、毒雾……）就只有落点，轻一些。
	/// at = 目标中心，top = 头顶，width = 目标宽度，amount = 这次加的层数（多就更旺）。
	/// </summary>
	public static void Apply(Control layer, Vector2 at, Vector2 top, float width, Node2D? body, int amount) =>
		Apply(layer, at, top, width, body, amount, true);

	private static void Apply(Control layer, Vector2 at, Vector2 top, float width, Node2D? body, int amount, bool retry)
	{
		// 游戏上毒和亚森出手常在同一帧：上毒先执行时爪尖还没记下 → 有召唤的场合（毒性爆发）等一帧再判断，免得走成没召唤的普通落点
		if (retry && InOutbreak && !ArseneFx.TryClaw(out _))
		{
			Fx.After(layer, 0.05, () => Apply(layer, at, top, width, body, amount, false));
			return;
		}
		if (CorrosiveFx.Suppressing && !ArseneFx.TryClaw(out _))
		{
			return;                                                   // 腐蚀波：抽牌带来的上毒由贴地的咒火浪表现（CorrosiveFx.Wave）
		}
		if (ArseneFx.TryClaw(out Vector2 claw))
		{
			Vector2 d = at - claw;
			float len = d.Length();
			if (len > 40f)
			{
				// slash_thrust（打包后 1024×512）：宽的起爆在爪尖、细线伸向敌人；比枪的弹道粗、用咒怨 LUT
				float sx = len / 940f;
				Vector2 center = claw + d * 0.5f + d.Normalized() * (512f - 530f) * sx;
				Paint.Sweep(layer, "slash_thrust", center, d.Angle(), new Vector2(sx, 0.42f), Lut5.Curse, 0.08f, 0.04f, 0.22f, 0f, 0f, 1.4f);
				for (int i = 1; i <= 3; i++)
				{
					Vector2 p = claw + d * (0.25f * i);
					Paint.Rise(layer, "curse_soul", p, 70f, 0.4f, Lut5.Curse, 0.5f, 0.03f * i);
				}
			}
			_beamLand = Stage.NowMs() + 80;
			// 毒性爆发：光束命中只留「标记」，不炸（用户 2026-11-03：太花、几个高潮挤在一起）——唯一的高潮留给引爆
			if (InOutbreak)
			{
				Fx.After(layer, 0.08, () => Mark(layer, at, top, width, body));
				return;
			}
			Fx.After(layer, 0.08, () => Impact(layer, at, top, width, body, amount, d, true));
			return;
		}
		if (InOutbreak)
		{
			Mark(layer, at, top, width, body);
			return;
		}
		Impact(layer, at, top, width, body, amount, new Vector2(1f, 0.1f), false);
	}

	/// <summary>咒怨打中：顿帧 + 沿来向的震屏 + 一下红白闪 + 空间扭曲 + 黑红咒火从脚下冲起 + 怨灵飘散 + 目标被染暗。</summary>
	private static void Impact(Control layer, Vector2 at, Vector2 top, float width, Node2D? body, int amount, Vector2 dir, bool persona)
	{
		float h = Mathf.Max(80f, (at - top).Length());
		Vector2 foot = at + new Vector2(0f, h * 0.75f);
		float k = Mathf.Clamp(width / 240f, 0.55f, 1.3f);
		bool big = amount >= 6;
		Sfx.Play(Sfx.Slam, persona ? 0.55f : 0.35f);
		Sfx.Play(Sfx.SubBoom, persona ? 0.5f : 0.3f);
		HitStop.Request(persona ? (big ? 0.08 : 0.06) : 0.035);
		if (Stage.ShakeDir != null)
		{
			Stage.ShakeDir(persona ? (big ? 3 : 2) : 1, Mathf.RadToDeg(dir.Angle()));
		}
		Glow.Flash(layer, at, new Color(1f, 0.35f, 0.4f), width * (persona ? 1.5f : 1.1f), 0.18f);
		Glow.Ripple(layer, at, width * 2f, 0.4f, persona ? 0.03f : 0.015f);
		float f = persona ? 1.1f : 0.8f;
		CurseFlame(layer, Vanilla.FireCenter, foot, (big ? 1.05f : 0.9f) * k * f);
		if (big || persona)
		{
			CurseFlame(layer, Vanilla.FireLeft, foot + new Vector2(-0.3f * width, 0f), 0.7f * k * f, 0.05f);
			CurseFlame(layer, Vanilla.FireRight, foot + new Vector2(0.3f * width, 0f), 0.7f * k * f, 0.09f);
		}
		Vanilla.Burst(layer, Vanilla.Embers, foot, 0f, 1.1f * k, Lut5.Curse);
		Vanilla.Burst(layer, Vanilla.Specks, at, dir.Angle(), 0.9f, Lut5.Curse, amount: 18);
		Paint.Rise(layer, "curse_soul", at + new Vector2(Fx.Rand(-0.25f, 0.25f) * width, 0.1f * h), 190f * k, 0.95f * k, Lut5.Curse, 0.8f, 0.06f);
		if (big || persona)
		{
			Paint.Rise(layer, "curse_ghost", at + new Vector2(Fx.Rand(-0.2f, 0.2f) * width, 0f), 240f * k, 0.72f * k, Lut5.Curse, 0.95f, 0.12f);
		}
		BodyTint.Pulse(body, new Color(1.8f, 0.7f, 0.75f), 0.03, 0.08);              // 打中一下提亮（受击闪）
		Fx.After(layer, 0.12, () => BodyTint.Pulse(body, new Color(0.72f, 0.45f, 0.52f), 0.18, 0.45));
	}

	// ───────────────────────── 毒性爆发（稀有）─────────────────────────

	private static ulong _outbreakUntil, _beamLand, _lastBlast;

	/// <summary>
	/// 毒性爆发出牌时（JokerFx.OnPlay）：接下来 seconds 秒里的毒伤结算（牌效果里的「立即触发中毒」）不走普通的一小团，
	/// 而是等亚森甩出去的咒怨光束都落地之后，所有敌人身上的咒火同时冲天而起（Blast），第一下压全屏红黑冲击帧 + 最重的顿帧。
	/// 前面的眼神特写、舞台、亚森现身走 Aim 的稀有召唤流程。
	/// </summary>
	public static void BeginOutbreak(float seconds) => _outbreakUntil = Stage.NowMs() + (ulong)(seconds * 1000f);

	public static bool InOutbreak => Stage.NowMs() < _outbreakUntil;

	/// <summary>毒性爆发期间：离引爆还有几秒（毒伤的伤害数字推迟到这一刻再跳，DamageNumberPatch）。</summary>
	public static double BlastWait()
	{
		ulong now = Stage.NowMs();
		return _beamLand + 380 > now ? (_beamLand + 380 - now) / 1000.0 : 0.0;
	}

	/// <summary>
	/// 毒性爆发的标记（光束命中时）：敌人身上一团暗暗燃着的小咒火 + 一点红光，身体暗下去；没有顿帧、没有震屏、声音很轻。
	/// 引爆前全场都被标上，停 ~0.3s 让人看清，再一起炸。
	/// </summary>
	private static void Mark(Control layer, Vector2 at, Vector2 top, float width, Node2D? body)
	{
		float h = Mathf.Max(80f, (at - top).Length());
		float k = Mathf.Clamp(width / 240f, 0.55f, 1.3f);
		Sfx.Play(Sfx.CurseSoft, 0.3f);
		Glow.Flash(layer, at, new Color(0.9f, 0.08f, 0.15f), width * 0.9f, 0.45f);
		Vanilla.Burst(layer, Vanilla.FireCenter, at + new Vector2(0f, h * 0.35f), 0f, 0.5f * k, Lut5.CurseInk);
		BodyTint.Pulse(body, new Color(0.62f, 0.4f, 0.48f), 0.45, 0.3);
	}

	/// <summary>
	/// 引爆（毒性爆发唯一的高潮）：所有标记同时炸开——一次顿帧 + 震屏 + 红黑帧（重档，0.15s）；每个敌人一根大咒火柱 + 两边各一团 + 一只怨灵，
	/// 之后只留烟和火星慢慢散（不再有第二波，原来的第二波让画面一直很满）。同一批里只有第一下带全屏效果和音效。
	/// </summary>
	private static void Blast(Control layer, Vector2 at, Vector2 top, float width, Node2D? body)
	{
		float h = Mathf.Max(80f, (at - top).Length());
		Vector2 foot = at + new Vector2(0f, h * 0.75f);
		float k = Mathf.Clamp(width / 240f, 0.55f, 1.3f);
		ulong now = Stage.NowMs();
		if (now - _lastBlast > 150)
		{
			Sfx.Play(Sfx.CurseBlast, 0.75f);
			Sfx.Play(Sfx.SubBoom, 0.5f);
			global::P5Joker.Impact.Confirm(layer, null, at, Vector2.Up, Weight.Heavy);
		}
		_lastBlast = now;
		Glow.Flash(layer, at, new Color(1f, 0.3f, 0.35f), width * 1.8f, 0.25f);
		Glow.Ripple(layer, at, width * 2.4f, 0.5f, 0.03f);
		CurseFlame(layer, Vanilla.FireCenter, foot, 1.45f * k);
		CurseFlame(layer, Vanilla.FireLeft, foot + new Vector2(-0.32f * width, 0f), 0.9f * k, 0.05f);
		CurseFlame(layer, Vanilla.FireRight, foot + new Vector2(0.32f * width, 0f), 0.9f * k, 0.08f);
		Vanilla.Burst(layer, Vanilla.Embers, foot, 0f, 1.3f * k, Lut5.Curse, delay: 0.1f);
		Vanilla.Burst(layer, Vanilla.BigSlashSmoke, foot + new Vector2(0f, -0.3f * h), -Mathf.Pi / 2f, 1.1f * k, Lut5.Smoke, delay: 0.15f);
		Paint.Rise(layer, "curse_ghost", at + new Vector2(Fx.Rand(-0.15f, 0.15f) * width, 0f), 260f * k, 0.9f * k, Lut5.Curse, 1.0f, 0.08f);
		BodyTint.Pulse(body, new Color(2f, 0.7f, 0.75f), 0.04, 0.1);
		Fx.After(layer, 0.14, () => BodyTint.Pulse(body, new Color(0.6f, 0.35f, 0.42f), 0.35, 0.6));
	}

	/// <summary>每轮毒伤（回合开始，敌人越多越频繁）：一小团咒火 + 一缕怨灵 + 身体暗一下。</summary>
	public static void Tick(Control layer, Vector2 at, Vector2 top, float width, Node2D? body)
	{
		if (InOutbreak)
		{
			// 毒性爆发的「立即触发中毒」：等咒怨光束落地再一起引爆
			ulong now = Stage.NowMs();
			// 标记之后停 0.3s 再一起引爆（三拍分开：召唤 → 标记 → 引爆）
			double wait = _beamLand + 380 > now ? (_beamLand + 380 - now) / 1000.0 : 0.0;
			Fx.After(layer, wait, () => Blast(layer, at, top, width, body));
			return;
		}
		float h = Mathf.Max(80f, (at - top).Length());
		float k = Mathf.Clamp(width / 240f, 0.55f, 1.3f);
		Sfx.Play(Sfx.CurseSoft, 0.45f);                              // 柔和的咒怨闷响（原来的 continuas_damage 太尖，用户嫌腐蚀波刺耳，毒伤也一起换）
		CurseFlame(layer, Vanilla.FireCenter, at + new Vector2(0f, h * 0.6f), 0.55f * k);
		Paint.Rise(layer, "curse_soul", at + new Vector2(Fx.Rand(-0.2f, 0.2f) * width, 0f), 120f * k, 0.7f * k, Lut5.Curse, 0.65f, 0.05f);
		BodyTint.Pulse(body, new Color(0.85f, 0.62f, 0.68f), 0.06, 0.3);
	}
}
