using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;
using P3Makoto.Core;

namespace P3Makoto.Vfx;

/// <summary>
/// Persona 召唤（骨骼动画，tools/rig/build_persona.py 生成）。出"君王之刃"时召唤 Thanatos：
/// 1. 显形（summon 0.3s）：结城理身后升起，脚下光柱 + 水波 + 影时间闪光，棺材环张开；之后悬浮待命。
/// 2. 冲刺 + 斩击（attack）：在伤害结算前 0.18s 开始 —— 从结城理身后冲到敌人面前（沿途留下速度线残影），
///    深蓄力后劈下；slash 事件 = 命中帧：画面瞬间压暗成影时间的墨绿、敌人身上一道巨大的黑色斩痕（青边白芯）
///    停留后碎成玻璃，叠加十字斩、冲击波、速度线、顿帧和强震屏。
/// 3. 退回并上浮消散（dismiss）。
/// 伤害结算时间由 SovereignTimingPatch 推迟到出牌后 0.62s（快速模式 0.25s），这里按同一规则算斩击时刻。
/// </summary>
public static class PersonaFx
{
	private const string Thanatos = "res://p3makoto/persona/thanatos_skel_data.tres";
	private const float FigureHeight = 1440f;          // 立绘在骨骼单位下的高度（原图像素）
	public const float SovereignWait = 0.92f;          // 君王之刃：出牌 → 伤害结算（普通速度）
	public const float VorpalWait = 2.0f;              // 追踪之刃生效时（空间杀法，群体）：出牌 → 伤害结算，多出约 1s 给"割裂空间"
	private const float SlashInAttack = 0.18f;         // attack 动画里 slash 事件的时刻

	/// <summary>当前速度设置下，出牌到伤害结算的秒数（与 CreatureCmd.TriggerAnim 的等待规则一致）。</summary>
	public static float DamageDelay(bool vorpal = false) => SaveManager.Instance?.PrefsSave.FastMode switch
	{
		FastModeType.Fast => Mathf.Min((vorpal ? VorpalWait : SovereignWait) * 0.5f, 0.25f),   // 与游戏的快速模式等待规则一致
		FastModeType.Instant => 0f,
		_ => vorpal ? VorpalWait : SovereignWait,
	};

	/// <summary>追踪之刃生效中：君王之刃改为群体 → 演出换成空间杀法。</summary>
	public static bool IsVorpal(NCreature caster) =>
		caster.Entity?.HasPower<MegaCrit.Sts2.Core.Models.Powers.SeekingEdgePower>() == true;

	/// <summary>elapsed：从出牌到现在（扣扳机那一刻）已经过去的秒数，用来对齐伤害结算时刻。</summary>
	public static void SummonThanatos(NCreature caster, NCreature? target, float elapsed = 0f)
	{
		Control? back = Makoto.FxLayer(back: true);
		Control? front = Makoto.FxLayer();
		if (back == null || front == null || ResourceLoader.Load(Thanatos) is not Resource data)
		{
			return;
		}
		Vector2 feet = Makoto.Feet(caster);
		Vector2 center = Makoto.Center(caster);
		float height = Mathf.Max(260f, (feet.Y - center.Y) * 2.1f) * 1.7f;   // Thanatos 显示高度：约结城理的 1.7 倍
		float scale = height / FigureHeight;
		Vector2 home = new(center.X - height * 0.2f, center.Y - height * 0.18f);
		Node2D root = Fx.Root(back, home, "P3Thanatos");
		root.Scale = Vector2.One * scale;

		Node2D? glow = Sprite(root, data, new Color(Pal.Cyan, 0.5f), additive: true);
		if (glow != null)
		{
			glow.Scale = Vector2.One * 1.04f;
		}
		Node2D? body = Sprite(root, data, new Color(0.8f, 0.88f, 1f, 0.95f), additive: false);
		if (body == null)
		{
			root.QueueFree();
			return;
		}

		// 显形音：玻璃碎裂（召唤器枪声）之后紧接着 Persona 涌现的上扬声，峰值约 0.55s，正好接上斩击 —— 填掉碎裂和劈下之间的空档
		Audio.Sfx.Clip("Snd_FX_Btl_Summon_00", 0.65f);
		// 显形：脚下光柱 + 水波 + 墨绿闪光 + 光点
		P3Fx.LightPillar(back, new Vector2(home.X, feet.Y), Pal.DarkHour, height * 0.45f, height * 1.1f, 0.6f);
		P3Fx.Ripple(back, new Vector2(home.X, feet.Y), 900f, 0.8f, 0.03f, squashY: 0.35f);
		P3Fx.Flash(back, home, Pal.DarkHour, height * 0.9f, 0.35f);          // 闪光在 Persona 身后、结城理身后，不盖住人物
		Node2D motes = Fx.Root(front, home, "P3ThanatosMotes");
		Fx.Sparks(motes, Pal.DarkHour, 60, 160f, 520f, 0.8f, gravity: -80f);
		Fx.FreeAfter(motes, 1.2);

		bool vorpal = IsVorpal(caster);
		float release = DamageDelay(vorpal) - elapsed;
		float attackAt = Mathf.Max(release - SlashInAttack, 0.05f);
		Vector2 strikeAt = target != null && GodotObject.IsInstanceValid(target)
			? target.VfxSpawnPosition + new Vector2(-height * 0.42f, -height * 0.32f)
			: home + new Vector2(height * 0.6f, 0f);
		Play(glow, attackAt, null);
		if (vorpal)
		{
			// 空间杀法：Persona 不冲刺，原地挥剑；全屏"割裂空间"对准伤害结算（Vorpal.Play）
			Play(body, attackAt, null);
			var centers = new System.Collections.Generic.List<Vector2>();
			if (MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom.Instance is { } room)
			{
				foreach (NCreature n in room.CreatureNodes)
				{
					if (GodotObject.IsInstanceValid(n) && !n.Entity.IsPlayer && n.Entity.IsAlive)
					{
						centers.Add(n.VfxSpawnPosition);
					}
				}
			}
			Vorpal.Play(front, centers, Mathf.Max(release, 0.4f));
			Uniq.SkillBanner(front, "空间杀法", "VORPAL BLADE", Mathf.Max(release, 0.4f) + 0.6f);   // P3R：技能名横幅挂到技能结束
			Fx.After(root, attackAt, "vorpal persona", () => Audio.Sfx.Play(Audio.Snd.ThanatosSlash, 0.9f));
			Fx.FreeAfter(root, attackAt + 1.6f);
			return;
		}
		Play(body, attackAt, () => OnSlash(front, target, height));

		// 冲刺：在 attack 开始时冲到敌人面前（0.16s），沿途速度线；斩完停留，dismiss 时往回退
		Fx.After(root, attackAt, "thanatos dash", () =>
		{
			Audio.Sfx.Play(Audio.Snd.ThanatosSlash, 1.1f);            // 巨响在 0.18s，正好对上 slash 事件
			Vector2 from = root.GlobalPosition;
			Fx.Animate(root, 0.16f, t => root.GlobalPosition = from.Lerp(strikeAt, Ease.OutCubic(t)));
			for (int i = 0; i < 4; i++)
			{
				float k = i / 4f;
				Fx.After(front, 0.04 * i, "thanatos streak", () =>
					P3Fx.SpeedLines(front, from.Lerp(strikeAt, 0.25f + 0.2f * k), Pal.DarkHour, 10, 30f, height * 0.5f, 0.25f));
			}
		});
		Fx.After(root, attackAt + 0.7, "thanatos retreat", () =>
		{
			Vector2 from = root.GlobalPosition;
			Fx.Animate(root, 0.45f, t => root.GlobalPosition = from.Lerp(from.Lerp(home, 0.5f), Ease.OutCubic(t)));
		});
		Fx.FreeAfter(root, attackAt + 0.7 + 0.45 + 0.3);
	}

	private static Node2D? Sprite(Node2D parent, Resource data, Color modulate, bool additive)
	{
		if (ClassDB.Instantiate("SpineSprite").AsGodotObject() is not Node2D spine)
		{
			return null;
		}
		parent.AddChild(spine);
		spine.Set("skeleton_data_res", data);
		spine.Modulate = modulate;
		if (additive)
		{
			spine.Set("normal_material", Fx.Additive);
		}
		return spine;
	}

	/// <summary>不攻击的召唤牌：召唤器碎裂后 Persona 在结城理身后显形（光柱 + 水波 + 闪光），悬浮一拍后消散。</summary>
	public static void Apparition(NCreature caster, Color tint)
	{
		Control? back = Makoto.FxLayer(back: true);
		Control? front = Makoto.FxLayer();
		if (back == null || front == null || ResourceLoader.Load(Thanatos) is not Resource data)
		{
			return;
		}
		Vector2 feet = Makoto.Feet(caster);
		Vector2 center = Makoto.Center(caster);
		float height = Mathf.Max(260f, (feet.Y - center.Y) * 2.1f) * 1.6f;
		Vector2 home = new(center.X - height * 0.22f, center.Y - height * 0.2f);
		Node2D root = Fx.Root(back, home, "P3PersonaApparition");
		root.Scale = Vector2.One * (height / FigureHeight);
		Node2D? glow = Sprite(root, data, new Color(tint, 0.5f), additive: true);
		if (glow != null)
		{
			glow.Scale = Vector2.One * 1.04f;
		}
		Node2D? body = Sprite(root, data, new Color(0.8f, 0.88f, 1f, 0.95f), additive: false);
		P3Fx.LightPillar(back, new Vector2(home.X, feet.Y), tint, height * 0.45f, height * 1.1f, 0.6f);
		P3Fx.Ripple(back, new Vector2(home.X, feet.Y), 900f, 0.8f, 0.03f, squashY: 0.35f);
		P3Fx.Flash(back, home, tint, height * 0.8f, 0.3f);
		Play(glow, 0.95f, null, attack: false);
		Play(body, 0.95f, null, attack: false);
		Vector2 from = home + new Vector2(0f, height * 0.06f);
		Vector2 to = home - new Vector2(0f, height * 0.04f);
		Fx.Animate(root, 1.5, t => root.GlobalPosition = from.Lerp(to, Ease.OutCubic(Mathf.Clamp(t * 1.5f / 0.4f, 0f, 1f))));
		Fx.FreeAfter(root, 1.6);
	}

	/// <summary>显形 → 悬浮待命 → attackAt 时开始攻击（接消散）；attack = false 时到点直接消散。onSlash 在 slash 事件触发。</summary>
	private static void Play(Node2D? spine, float attackAt, System.Action? onSlash, int triesLeft = 20, bool attack = true)
	{
		if (spine == null || !GodotObject.IsInstanceValid(spine))
		{
			return;
		}
		MegaSprite sprite = new(spine);
		if (sprite.TryGetAnimationState() is not MegaAnimationState state)
		{
			if (triesLeft > 0)
			{
				Fx.After(spine, 0.016, "persona spine", () => Play(spine, attackAt - 0.016f, onSlash, triesLeft - 1, attack));
			}
			return;
		}
		state.SetAnimation("summon", loop: false);
		state.AddAnimation("idle_loop", 0f, loop: true);
		Fx.After(spine, Mathf.Max(attackAt, 0f), "persona attack", () =>
		{
			if (sprite.TryGetAnimationState() is MegaAnimationState s)
			{
				if (attack)
				{
					s.SetAnimation("attack", loop: false);
					s.AddAnimation("dismiss", 0f, loop: false);
				}
				else
				{
					s.SetAnimation("dismiss", loop: false);
				}
			}
		});
		if (onSlash != null)
		{
			sprite.ConnectAnimationEvent(Callable.From<GodotObject, GodotObject, GodotObject, GodotObject>((_, _, _, ev) =>
				ModEntry.Safe("persona event", () =>
				{
					if (new MegaEvent(ev).GetData().GetEventName() == "slash")
					{
						onSlash();
					}
				})));
		}
	}

	/// <summary>斩击命中帧。</summary>
	private static void OnSlash(Control front, NCreature? target, float height)
	{
		Vector2 at = target != null && GodotObject.IsInstanceValid(target)
			? target.VfxSpawnPosition
			: front.GetViewportRect().Size * new Vector2(0.68f, 0.45f);
		DarkHourFlash(front);
		GiantSlash(front, at, -0.95f, height * 1.25f);
		P3Fx.CrossSlash(front, at, Pal.Royal, 300f);
		P3Fx.Shockwave(front, at, Pal.DarkHour, 820f, 0.55f);
		P3Fx.SpeedLines(front, at, Pal.Ice, 22, 80f, 620f, 0.4f);
		P3Fx.GlassShatter(front, at, 36, 1.3f, Pal.Ice);
		EvokerShot.HitStop(0.09);
		NGame.Instance?.ScreenShake(ShakeStrength.Strong, ShakeDuration.Short);
	}

	/// <summary>全屏瞬间压暗成影时间的墨绿（暗而不刺眼），0.35s 退去。</summary>
	private static void DarkHourFlash(Control layer)
	{
		Vector2 size = layer.GetViewportRect().Size;
		Node2D root = Fx.Root(layer, Vector2.Zero, "P3DarkHourFlash");
		root.TopLevel = true;
		root.GlobalPosition = Vector2.Zero;
		ColorRect rect = new() { Size = size, Color = new Color(0.02f, 0.12f, 0.06f, 0.5f), MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(rect);
		Fx.Animate(rect, 0.35f, t => rect.Color = new Color(0.02f, 0.12f, 0.06f, 0.5f * (1f - Ease.OutCubic(t))));
		Fx.FreeAfter(root, 0.4);
	}

	/// <summary>巨大的新月斩痕：黑色刃身 + 青色刃边 + 白色刃芯，从一端"划开"，停留后碎成玻璃。</summary>
	public static void GiantSlash(Control layer, Vector2 at, float angle, float length)
	{
		Motifs.ArcSlash(layer, at, Pal.Cyan, length, 60f, 46f, angle, length / 700f, darkCore: true);
		Fx.After(layer, 0.24, "giant slash shatter", () => P3Fx.GlassShatter(layer, at, 24, 1.1f, Pal.Cyan));
	}
}
