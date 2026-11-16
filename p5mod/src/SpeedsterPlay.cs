using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace P5Joker;

/// <summary>
/// 速行者（用户 2026-11-16：每次抽牌造成伤害时要有微弱的特效）：原版每抽一张在所有敌人身上放一个砍击特效（vfx_attack_slash）→
/// 藏掉，换成速行者那套语言的轻量版：每个敌人身上一道尖头红白疾光划过、一点白闪和火星（SkillRedo5.SpeedsterTick）。条件照原版 AfterCardDrawn。
/// </summary>
[HarmonyPatch(typeof(SpeedsterPower), nameof(SpeedsterPower.AfterCardDrawn))]
public static class SpeedsterDrawPatch
{
	public static void Prefix(SpeedsterPower __instance, PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
	{
		ModEntry.Safe("SpeedsterDraw", () =>
		{
			var owner = __instance.Owner;
			if (fromHandDraw || card.Owner?.Creature != owner || !VanillaSilentFx.IsJoker(owner)) return;
			if (owner.CombatState is not { } combat || combat.CurrentSide != owner.Side) return;
			if (NCombatRoom.Instance is not { } room || Joker.FxLayer() is not Godot.Control layer) return;
			VanillaSilentFx.HideFor(0.8f);
			int i = 0;
			foreach (NCreature n in combat.HittableEnemies.Select(room.GetCreatureNode).OfType<NCreature>())
			{
				Godot.Rect2 box = n.Hitbox.GetGlobalRect();
				SkillRedo5.SpeedsterTick(layer, box.GetCenter(), box.Size.Y, 0.03f * i++);
			}
		});
	}
}
