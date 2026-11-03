using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace P5Joker;

/// <summary>
/// 骨骼动画的接续（用户 2026-10-28）：
///  · 拿起小刀牌就转枪出枪瞄准（aim_in → aim_loop），打出去直接开火（shiv 换成 shiv_from_aim；转枪还没转完就打出，
///    换成 shiv 从同一时刻接着播），放回去转枪收走（aim_out）；
///  · 拿起打击类攻击牌就在腰侧转刀起手（knife_in → knife_loop），打出去直接上撩（attack 换成 attack_from_ready；
///    转刀还没转完就打出，换回 attack 从同一时刻接着播），放回去转刀收起（knife_out）；
///  · 技能 / 能力 / 格挡牌（用户 2026-10-29：每张牌拿起来都要有架势和特效）：手扣面具（mask_*，蓝焰从指缝漏出）、
///    拳头半举（guard_*）、伸手去扯手套（glove_*），打出去从架势直接接施法（cast_from_mask / cast_guard_from_ready /
///    cast_glove_from_ready），放回去收手（*_out）；架势上的特效见 Vfx/Ready.cs，指到敌人时的锁定准星见 LockOn；
///  · 连续出攻击牌时，上一刀还在收势就接下一刀（attack 换成 attack_chain，从手里握着刀直接再蓄力），不再从拔刀重播；
///    连续开枪同理（shiv 换成 shiv_from_aim）。
/// 只动 Joker 的骨骼（原版静默猎手被我们的骨骼替换了，这些动画名只在 art/p5/rig/joker.spine-json 里有）。
/// </summary>
public static class Aim
{
	private static readonly HashSet<string> Raised = new() { "aim_in", "aim_loop", "shiv", "shiv_from_aim" };
	private static readonly HashSet<string> Slashing = new() { "attack", "attack_chain", "attack_from_ready" };

	/// <summary>换动画之后要跳到的时间（AnimRenamePatch 的 Postfix 用）。</summary>
	public static readonly Dictionary<ulong, float> SkipTo = new();

	/// <summary>SetAnimation 之后：这个 Joker 有待跳的时间就跳过去，并且不做交叉淡化（同一套画面接着播）。返回跳过的秒数。</summary>
	public static float ApplySkip(NCreature node, MegaAnimationState state)
	{
		if (!SkipTo.Remove(node.GetInstanceId(), out float t))
		{
			return 0f;
		}
		using MegaTrackEntry? entry = state.GetCurrent(0);
		entry?.SetTrackTime(t);
		entry?.SetMixDuration(0f);
		return t;
	}

	/// <summary>出牌时（CardPlayedPatch）记下的施法动作：游戏随后要播 cast 时换成对应版本。</summary>
	public static readonly Dictionary<ulong, Cast> PendingCast = new();

	/// <summary>这个动画状态是不是本地 Joker 的（换动画的入口所有生物都会经过）。</summary>
	internal static NCreature? Owner(MegaAnimationState state)
	{
		NCombatRoom? room = NCombatRoom.Instance;
		if (room == null || state.BoundObject is not GodotObject native)
		{
			return null;
		}
		ulong id = native.GetInstanceId();
		foreach (NCreature n in room.CreatureNodes)
		{
			if (n.Entity?.Player is { } p && Joker.Is(p) && n.Visuals?.SpineBody?.TryGetAnimationState()?.BoundObject is GodotObject o && o.GetInstanceId() == id)
			{
				return n;
			}
		}
		return null;
	}

	public static string? Rename(MegaAnimationState state, string requested)
	{
		if (Owner(state) is not NCreature node || node.Visuals.SpineBody is not MegaSprite body)
		{
			return null;
		}
		if (requested == "cast")
		{
			if (!PendingCast.Remove(node.GetInstanceId(), out Cast kind))
			{
				return null;
			}
			// 撕面具甩手那版（cast_big）不用了（用户 2026-10-29：只留简单的召唤 cast）
			// 正摆着这张牌的架势：从架势直接接施法
			string? held = Current(state);
			string? name = kind switch
			{
				Cast.Guard => held is "guard_in" or "guard_loop" ? "cast_guard_from_ready" : "cast_guard",
				Cast.Glove => held is "glove_in" or "glove_loop" ? "cast_glove_from_ready" : "cast_glove",
				_ => held is "mask_in" or "mask_loop" ? "cast_from_mask" : null,
			};
			return name != null && body.HasAnimation(name) ? name : null;
		}
		string? current = Current(state);
		if (requested == "shiv" && current == "aim_in")
		{
			// 转枪还没转完就打出去了：aim_in 就是 shiv 的前 0.18 秒（tools/p5/build_rig_p5.py GUN_IN），
			// 换成 shiv 并从同一时刻接着播 —— 转枪转完、接住、开火，不从头重来也不硬切
			using MegaTrackEntry? entry = state.GetCurrent(0);
			if (entry != null)
			{
				SkipTo[node.GetInstanceId()] = entry.GetTrackTime();
			}
			return "shiv";
		}
		if (requested == "shiv" && current != null && Raised.Contains(current) && body.HasAnimation("shiv_from_aim"))
		{
			return "shiv_from_aim";
		}
		if (requested == "attack" && current == "knife_in")
		{
			// 转刀还没转完就打出去了：knife_in 就是 attack 的前 0.14 秒，换回 attack 并从同一时刻接着播
			using MegaTrackEntry? entry = state.GetCurrent(0);
			if (entry != null)
			{
				SkipTo[node.GetInstanceId()] = entry.GetTrackTime();
			}
			return "attack";
		}
		if (requested == "attack" && current == "knife_loop" && body.HasAnimation("attack_from_ready"))
		{
			return "attack_from_ready";
		}
		if (requested == "attack" && current != null && Slashing.Contains(current) && body.HasAnimation("attack_chain"))
		{
			return "attack_chain";
		}
		return null;
	}

	/// <summary>拿起的这张牌要摆哪种架势。</summary>
	public static Stance StanceFor(CardModel card) =>
		Joker.IsShot(card) ? Stance.Gun
		: card.Type == MegaCrit.Sts2.Core.Entities.Cards.CardType.Attack ? Stance.Knife
		: Casts.For(card) switch { Cast.Guard => Stance.Guard, Cast.Glove => Stance.Glove, _ => Stance.Mask };

	private static string Prefix(Stance s) => s switch
	{
		Stance.Gun => "aim", Stance.Knife => "knife", Stance.Guard => "guard", Stance.Glove => "glove", _ => "mask",
	};

	/// <summary>本地 Joker 正摆着的架势特效（一次只有一张牌在手上）。</summary>
	private static ReadyFx? _fx;
	private static CardModel? _fxCard;
	private static LockOn? _lock;
	private static ulong _lockId;

	/// <summary>拿起一张牌（单体牌开始选目标 / 群体牌拖进出牌区）：摆架势、放架势特效。</summary>
	public static void Raise(CardModel card)
	{
		if (!Joker.OwnsCard(card) || !LocalContext.IsMe(card.Owner))
		{
			return;
		}
		NCreature? node = Joker.Node(card.Owner);
		MegaSprite? body = node?.Visuals?.SpineBody;
		if (node == null || body == null || body.TryGetAnimationState() is not MegaAnimationState state)
		{
			return;
		}
		Stance stance = StanceFor(card);
		string pre = Prefix(stance);
		string? current = Current(state);
		if (stance == Stance.Gun && current is "shiv" or "shiv_from_aim")
		{
			state.SetAnimation("aim_loop", true, 0);              // 上一枪还没收完：直接回到瞄准
		}
		else if (current != pre + "_in" && current != pre + "_loop")
		{
			if (!Idle(current) || !body.HasAnimation(pre + "_loop"))
			{
				return;                                           // 正在出别的牌 / 受击：不打断（刚收起的可以直接换）
			}
			state.SetAnimation(pre + "_in", false, 0);
			state.AddAnimation(pre + "_loop", 0f, true, 0);
		}
		if (_fx != null && _fxCard == card && _fx.Stance == stance)
		{
			return;
		}
		_fx?.End(false);
		_fx = null;
		if (Joker.FxLayer() is Control front && Joker.FxLayer(back: true) is Control back)
		{
			BodyAura.CloneBody = parent => CloneBody(node, parent, "mask_loop");
			_fx = ReadyFx.Start(front, back, stance, v => Joker.PosePoint(node, v), Joker.Facing(node));
			_fxCard = card;
		}
	}

	/// <summary>
	/// 在 target 下面复制一份 Joker 的骨骼，同一个动作、同一时刻，之后接着播 then；返回复制品和本体此刻的全局变换
	/// （轮廓蓝焰 BodyAura 把它画进离屏画面取剪影）。
	/// </summary>
	private static (Node2D, Transform2D)? CloneBody(NCreature joker, Node target, string then)
	{
		if (joker.Visuals?.Body is not Node2D body || joker.Visuals.SpineBody?.TryGetAnimationState() is not MegaAnimationState src
			|| body.Duplicate() is not Node2D clone)
		{
			return null;
		}
		target.AddChild(clone);
		var sprite = new MegaSprite(clone);
		if (sprite.TryGetAnimationState() is MegaAnimationState st)
		{
			string? anim = Current(src);
			float time = 0f;
			try
			{
				using MegaTrackEntry? e = src.GetCurrent(0);
				time = e?.GetTrackTime() ?? 0f;
			}
			catch (System.Exception)
			{
			}
			if (anim != null && sprite.HasAnimation(anim))
			{
				st.SetAnimation(anim, anim == then, 0);
				using MegaTrackEntry? ce = st.GetCurrent(0);
				ce?.SetTrackTime(time);
				ce?.SetMixDuration(0f);
				if (anim != then && sprite.HasAnimation(then))
				{
					st.AddAnimation(then, 0f, true, 0);
				}
			}
		}
		return (clone, body.GlobalTransform);
	}

	private static bool Idle(string? current) =>
		current is "idle_loop" or "low_health_loop" or "aim_out" or "knife_out" or "mask_out" or "guard_out" or "glove_out";

	/// <summary>Joker 自己开始施法的时刻（毫秒）：之后 1.5 秒内游戏再要播 cast 就不重播（AnimRenamePatch 吞掉）。</summary>
	private static readonly Dictionary<ulong, ulong> SelfCast = new();

	/// <summary>
	/// 出技能 / 能力牌（CardPlayedPatch）：Joker 自己施法 —— 摆着架势就从架势接（*_from_*），站着就播对应的 cast。
	/// 原版很多牌（防御等）根本不触发 Cast 动画，不这样做人会一直停在架势上。返回是否播了。
	/// </summary>
	public static bool PlayCast(NCreature node, CardModel card)
	{
		Stance stance = StanceFor(card);
		MegaSprite? body = node.Visuals?.SpineBody;
		if (stance is Stance.Knife or Stance.Gun || body == null || body.TryGetAnimationState() is not MegaAnimationState state)
		{
			return false;
		}
		string pre = Prefix(stance);
		string? current = Current(state);
		string? name = current == pre + "_in" || current == pre + "_loop"
			? stance switch { Stance.Guard => "cast_guard_from_ready", Stance.Glove => "cast_glove_from_ready", _ => "cast_from_mask" }
			: Idle(current) ? stance switch { Stance.Guard => "cast_guard", Stance.Glove => "cast_glove", _ => "cast" } : null;
		if (name == null || !body.HasAnimation(name))
		{
			return false;
		}
		bool low = node.Entity.GetHpPercentRemaining() <= 0.25 && body.HasAnimation("low_health_loop");
		PendingCast.Remove(node.GetInstanceId());
		state.SetAnimation(name, false, 0);
		state.AddAnimation(low ? "low_health_loop" : "idle_loop", 0f, true, 0);
		SelfCast[node.GetInstanceId()] = Time.GetTicksMsec() + 1500;
		if (_held is ReadyFx held && Joker.FxLayer() is Control layer)
		{
			Fx.After(layer, 0.04, () => Release(held));             // 手扣紧面具（summon_hold_a）的那一下炸开
		}
		Tier tier = Tiers.For(card);
		if (stance == Stance.Mask && (tier >= Tier.Uncommon || Grips(card.GetType().Name)) && Joker.FxLayer(back: true) is Control back)
		{
			// 按稀有度分级（Tiers）：稀有 = 完整召唤；罕见 = 只有人格面具现身；普通毒牌 = 亚森伸手
			// 标杆 1（2026-11-02，照结城理 mod 召唤器 → 君王之剑的两拍结构）：
			//   拍 1「召唤」：眼神特写撕开后停一下（EvokeAt），从 Joker 的面具朝人格面具喷出一道贯穿光 + 锥形玻璃 + 蓝焰（Evoke），
			//                眼神特写同时碎开，人格面具从这团蓝焰里现身；
			//   拍 2「出手」：人格面具出手 = 伤害 / 上毒结算（各张牌自己的命中特效带顿帧和冲击声）。
			Persona.Spec spec = PersonaFor(card);
			float evoke = tier == Tier.Rare ? SummonStage.EvokeAt : 0f;
			Summoning[node.GetInstanceId()] = (Time.GetTicksMsec() + 2000, HitFor(card));
			if (card.GetType().Name == "Outbreak")
			{
				OutbreakHand(node, back);
				return true;
			}
			Vector2 head = Joker.PosePoint(node, ReadyFx.MaskFace);
			Fx.After(back, 0.04, () =>
			{
				Vector2 feet = Joker.PosePoint(node, ReadyFx.Feet);
				float tall = (feet - Joker.PosePoint(node, new Vector2(815f, 120f))).Length();
				float face = Joker.Facing(node);
				if (tier == Tier.Rare)
				{
					CutIn.SplitAt = evoke - 0.05f;                      // 裂纹定格 0.05s，拍 1 碎开
					if (Joker.FxLayer() is Control fr)
					{
						SummonStage.Play(back, fr, head);
					}
					CutIn.Eyes(back);
					Vector2 chest = Persona.ChestOf(spec, feet, tall * 1.35f, face);
					Fx.After(back, evoke, () =>
					{
						SummonStage.Beat(Joker.FxLayer() ?? back, head, back, face, chest);
						Persona.Show(spec, back, feet, tall * 1.35f, face);
					});
				}
				else
				{
					Sfx.Play(Sfx.PersonaFlame, 0.8f);                   // 没有舞台：人格面具现身的蓝焰声自己放
					Persona.Show(spec, back, feet, tall * 1.35f, face);
				}
			});
		}
		return true;
	}

	/// <summary>召唤时亚森出手的时刻（施法后几秒）：游戏结算这张牌（中毒、伤害）的等待被推到这一刻（TriggerAnimDelayPatch），出牌特效也对齐到这一刻。</summary>
	public const float SummonHit = 0.04f + Persona.AttackAt;

	/// <summary>这张牌召唤哪个人格面具：毒性爆发 = 伊邪那岐大神（万物真言），其他 = 亚森。</summary>
	public static Persona.Spec PersonaFor(CardModel card) => PersonaFor(card.GetType().Name);

	public static Persona.Spec PersonaFor(string cardName) => cardName switch
	{
		"Outbreak" => Persona.ArseneSpec with { First = 18, Linger = CrushHand.CrushAfter + 0.5f },   // 巨手版：一现身就出手（0.25s）= 手冲出来，捏碎后再停 0.5s 退场
		var n when Grips(n) => Persona.ArseneSpec with { Linger = 1.1f },   // 伸手抓人：等手收回来再走
		_ => Persona.ArseneSpec,
	};

	/// <summary>召唤人格面具、单体上毒的牌：亚森伸手攥住中毒的怪（Grip）。</summary>
	public static bool Grips(string cardName) => cardName is "BubbleBubble" or "DeadlyPoison" or "Snakebite";   // 用户 2026-11-01：只要用毒就亚森 + 手捏

	/// <summary>这张牌召唤时人格面具出手的时刻（施法后几秒）：出牌特效对齐到这一刻。</summary>
	public static float HitFor(CardModel card) => card.GetType().Name == "Outbreak"
		? 0.04f + OutbreakEmerge + CrushHand.CrushAfter               // 巨手版：结算 = 捏碎那一刻
		: 0.04f + (Tiers.For(card) == Tier.Rare ? SummonStage.EvokeAt : 0f) + PersonaFor(card).AttackAt;

	/// <summary>
	/// 毒性爆发：亚森现身的时刻（眼神特写开始后几秒）和巨手冲出来的时刻（= 亚森出手）。
	/// 用户 2026-11-03：原来是两个高潮（召唤那一拍 + 捏碎）太拖沓 → 只留一个高潮：亚森不走召唤那一拍（顿帧、震屏、面具喷光），
	/// 横幅一出来就伴着蓝焰声现身，一现身就出手捏碎。
	/// </summary>
	private const float OutbreakShow = 0.3f;

	private static float OutbreakEmerge => OutbreakShow + PersonaFor("Outbreak").AttackAt;

	/// <summary>
	/// 毒性爆发（用户 2026-11-03 创意）：舞台 + 眼神特写 → 亚森照常召唤现身（拍 1，但眼神特写不碎）→ 亚森往前出手，横幅里冲出一只咒烟巨手（CrushHand）
	/// → 攥住横幅（所有敌人被标记）→ 捏碎 = 横幅碎开 = 游戏结算毒伤 = 所有敌人咒火一起引爆（PoisonFx.Blast）→ 亚森退场。没有光束。
	/// </summary>
	private static void OutbreakHand(NCreature node, Control back)
	{
		Vector2 head = Joker.PosePoint(node, ReadyFx.MaskFace);
		Persona.Spec spec = PersonaFor("Outbreak");
		Fx.After(back, 0.04, () =>
		{
			Vector2 feet = Joker.PosePoint(node, ReadyFx.Feet);
			float tall = (feet - Joker.PosePoint(node, new Vector2(815f, 120f))).Length();
			float face = Joker.Facing(node);
			CutIn.SplitAt = OutbreakEmerge + CrushHand.CrushAfter;      // 横幅碎开 = 捏碎
			if (Joker.FxLayer() is Control fr)
			{
				SummonStage.Play(back, fr, head, maskFlame: false);     // 用户 2026-11-03：亚森出现只响一声蓝焰（原来舞台一声 + 现身一声）
			}
			CutIn.Eyes(back);
			Fx.After(back, OutbreakShow, () =>
			{
				Sfx.Play(Sfx.PersonaFlame, 0.5f);                       // 和退场同一个声、同样的音量
				ArseneFx.NextClench = (spec.AttackAt + CrushHand.FistAfter, spec.AttackAt + CrushHand.CrushAfter);   // 不扑：伸手攥拳，跟巨手同步
				Persona.Show(spec, back, feet, tall * 1.35f, face);
			});
			CrushHand.Outbreak(back, OutbreakEmerge, onFist: () =>
			{
				if (Joker.FxLayer() is not Control layer || NCombatRoom.Instance is not NCombatRoom room || node.Entity?.CombatState is not { } cs)
				{
					return;
				}
				var targets = new List<PoisonFx.Target>();
				foreach (Creature c in cs.HittableEnemies)
				{
					if (room.GetCreatureNode(c) is NCreature n)
					{
						targets.Add(new PoisonFx.Target(n.VfxSpawnPosition, n.GetTopOfHitbox(), n.Hitbox.GetGlobalRect().Size.X, n.Visuals));
					}
				}
				PoisonFx.Squeeze(layer, targets);
			});
		});
	}

	/// <summary>正在召唤的 Joker：(到这个时刻为止的毫秒, 结算要推迟到施法后几秒)。</summary>
	private static readonly Dictionary<ulong, (ulong Until, float Hit)> Summoning = new();

	/// <summary>这个 Joker 这次施法是 Persona 召唤吗：是就返回结算要推迟到几秒（TriggerAnimDelayPatch 用，用一次就清掉），不是返回 0。</summary>
	public static float TakeSummon(NCreature node) =>
		Summoning.Remove(node.GetInstanceId(), out var s) && Time.GetTicksMsec() < s.Until ? s.Hit : 0f;

	/// <summary>游戏要播 cast，但 Joker 刚自己施过法：不重播。</summary>
	public static bool SwallowCast(MegaAnimationState state) =>
		Owner(state) is NCreature node && SelfCast.TryGetValue(node.GetInstanceId(), out ulong until) && Time.GetTicksMsec() < until;

	/// <summary>打出去了：架势特效一闪收掉（出牌特效接上）。</summary>
	public static void Played()
	{
		Unlock();
		if (_fx is { Stance: Stance.Mask } held)
		{
			// 面具等 Joker 真正扣面具施法（PlayCast）那一刻才炸开 —— 出牌队列可能还要等一会儿；万一没等到，0.8s 后自己炸
			_held = held;
			_fx = null;
			_fxCard = null;
			if (Joker.FxLayer() is Control layer)
			{
				Fx.After(layer, 0.8, () => Release(held));
			}
			return;
		}
		_fx?.End(true);
		_fx = null;
		_fxCard = null;
	}

	/// <summary>打出去、等着施法的面具。</summary>
	private static ReadyFx? _held;

	private static void Release(ReadyFx fx)
	{
		fx.End(true);
		if (_held == fx)
		{
			_held = null;
		}
	}

	/// <summary>选目标时指到一个生物：敌人身上出锁定准星。</summary>
	public static void Hover(NCreature creature)
	{
		if (_fx == null || creature.Entity == null || creature.Entity.IsPlayer || Joker.FxLayer() is not Control layer)
		{
			return;
		}
		if (_lock != null && _lockId == creature.GetInstanceId())
		{
			return;
		}
		Unlock();
		Vector2 at = creature.VfxSpawnPosition;
		float size = Mathf.Clamp((at - creature.GetTopOfHitbox()).Length() * 2.4f, 160f, 340f);
		_lock = LockOn.Start(layer, at, size);
		_lockId = creature.GetInstanceId();
	}

	public static void Unhover() => Unlock();

	private static void Unlock()
	{
		_lock?.Release();
		_lock = null;
		_lockId = 0;
	}

	public static void Lower(NCreature? node)
	{
		MegaSprite? body = node?.Visuals?.SpineBody;
		if (body == null || body.TryGetAnimationState() is not MegaAnimationState state)
		{
			return;
		}
		string? current = Current(state);
		string? putAway = current switch
		{
			"aim_in" or "aim_loop" => "aim_out",
			"knife_in" or "knife_loop" => "knife_out",
			"mask_in" or "mask_loop" => "mask_out",
			"guard_in" or "guard_loop" => "guard_out",
			"glove_in" or "glove_loop" => "glove_out",
			_ => null,
		};
		if (putAway == null || !body.HasAnimation(putAway))
		{
			return;
		}
		bool low = node!.Entity.GetHpPercentRemaining() <= 0.25 && body.HasAnimation("low_health_loop");
		state.SetAnimation(putAway, false, 0);
		state.AddAnimation(low ? "low_health_loop" : "idle_loop", 0f, true, 0);
	}

	public static void LowerLocal()
	{
		_fx?.End(false);
		_fx = null;
		_fxCard = null;
		Unlock();
		NCombatRoom? room = NCombatRoom.Instance;
		if (room == null)
		{
			return;
		}
		foreach (NCreature n in room.CreatureNodes.Where(n => n.Entity?.Player is { } p && Joker.Is(p) && LocalContext.IsMe(p)))
		{
			Lower(n);
		}
	}

	private static string? Current(MegaAnimationState state)
	{
		try
		{
			return state.GetCurrentAnimationName(0);
		}
		catch (System.Exception)
		{
			return null;
		}
	}
}

/// <summary>换动画的唯一入口：Joker 正举着枪 / 正在收刀时，把 shiv / attack 换成接续版本。</summary>
[HarmonyPatch(typeof(MegaAnimationState), nameof(MegaAnimationState.SetAnimation))]
public static class AnimRenamePatch
{
	public static bool Prefix(MegaAnimationState __instance, ref string animationName)
	{
		if (animationName is not ("shiv" or "attack" or "cast"))
		{
			return true;
		}
		if (animationName == "cast")
		{
			bool swallow = false;
			ModEntry.Safe("AnimSwallow", () => swallow = Aim.SwallowCast(__instance));
			if (swallow)
			{
				return false;
			}
		}
		string requested = animationName;
		string? replacement = null;
		ModEntry.Safe("AnimRename", () => replacement = Aim.Rename(__instance, requested));
		if (replacement != null)
		{
			animationName = replacement;
		}
		return true;
	}

	public static void Postfix(MegaAnimationState __instance, string animationName)
	{
		if (animationName is not ("shiv" or "shiv_from_aim" or "attack" or "attack_chain" or "attack_from_ready"))
		{
			return;
		}
		ModEntry.Safe("AnimFx", () =>
		{
			if (Aim.Owner(__instance) is not NCreature node)
			{
				return;
			}
			float skipped = animationName is "shiv" or "attack" ? Aim.ApplySkip(node, __instance) : 0f;
			JokerFx.OnAnim(node, animationName, skipped);
		});
	}
}

/// <summary>单体牌开始选目标：摆架势（Aim.Raise）。</summary>
[HarmonyPatch]
public static class StartTargetingPatch
{
	public static IEnumerable<MethodBase> TargetMethods() =>
		typeof(NTargetManager).GetMethods(BindingFlags.Instance | BindingFlags.Public)
			.Where(m => m.Name == "StartTargeting" && m.GetParameters().Length > 1 && m.GetParameters()[1].ParameterType == typeof(Control));

	public static void Postfix(object[] __args)
	{
		ModEntry.Safe("StartTargeting", () =>
		{
			if (__args[1] is NCard { Model: CardModel card })
			{
				Aim.Raise(card);
			}
		});
	}
}

/// <summary>不选目标的牌（群体 / 自己）拖进出牌区：摆架势。</summary>
[HarmonyPatch(typeof(NCardPlay), "ShowMultiCreatureTargetingVisuals")]
public static class MultiTargetingPatch
{
	public static void Postfix(NCardPlay __instance)
	{
		ModEntry.Safe("MultiTargeting", () =>
		{
			if (Traverse.Create(__instance).Property("Card").GetValue() is CardModel card)
			{
				Aim.Raise(card);
			}
		});
	}
}

/// <summary>出牌流程收尾：取消（isFinished = false）就收起架势；打出则架势特效一闪收掉，动作交给 *_from_* 接着出招。</summary>
[HarmonyPatch(typeof(NCardPlay), "Cleanup")]
public static class CardPlayCleanupPatch
{
	/// <summary>Cleanup 里也会调 HideTargetingVisuals，那次不算"拖出了出牌区"。</summary>
	public static bool Inside;

	public static void Prefix(bool isFinished)
	{
		Inside = true;
		ModEntry.Safe("CardPlayCleanup", isFinished ? Aim.Played : Aim.LowerLocal);
	}

	public static void Postfix() => Inside = false;
}

/// <summary>群体牌拖回出牌区下面（原版藏起目标框）：收起架势；再拖上去 MultiTargetingPatch 又会摆起来。</summary>
[HarmonyPatch(typeof(NCardPlay), "HideTargetingVisuals")]
public static class HideTargetingPatch
{
	public static void Postfix()
	{
		if (!CardPlayCleanupPatch.Inside)
		{
			ModEntry.Safe("HideTargeting", Aim.LowerLocal);
		}
	}
}

/// <summary>选目标时指到 / 移开一个生物：锁定准星（Aim.Hover）。</summary>
[HarmonyPatch(typeof(NCardPlay), "OnCreatureHover")]
public static class CreatureHoverPatch
{
	public static void Postfix(NCreature creature) => ModEntry.Safe("CreatureHover", () => Aim.Hover(creature));
}

[HarmonyPatch(typeof(NCardPlay), "OnCreatureUnhover")]
public static class CreatureUnhoverPatch
{
	public static void Postfix() => ModEntry.Safe("CreatureUnhover", Aim.Unhover);
}

[HarmonyPatch(typeof(NTargetManager), "FinishTargeting")]
public static class FinishTargetingPatch
{
	public static void Postfix(bool cancel)
	{
		if (cancel)
		{
			ModEntry.Safe("FinishTargeting", Aim.LowerLocal);
		}
	}
}
