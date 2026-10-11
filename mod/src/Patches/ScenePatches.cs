using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using P3Makoto.Core;
using P3Makoto.Scenes;

namespace P3Makoto.Patches;

/// <summary>
/// 君王之刃：结城理不显示环绕的大剑（v0.9.2 起由 Persona 代替：出牌时眼神切入 → 召唤器 → Thanatos 现身斩击，见 CutInFx / PersonaFx）。
/// 整棵节点透明、悬停框不再接收鼠标（伤害数值在卡牌上照常显示）；仍保留换色，万一别处把它显示出来也是 P3 蓝。
/// </summary>
[HarmonyPatch(typeof(NSovereignBladeVfx), "_Ready")]
public static class SovereignBladeReskinPatch
{
	public static void Postfix(NSovereignBladeVfx __instance)
	{
		ModEntry.Safe("BladeReskin", () =>
		{
			if (Makoto.Is(__instance.Card?.Owner))
			{
				BladeReskin.Apply(__instance);
				BladeReskin.Hide(__instance);
			}
		});
	}
}

/// <summary>选人界面背景换成 P3R 风格（见 CharSelectBg）。</summary>
[HarmonyPatch(typeof(NRegentCharacterSelectBg), "_Ready")]
public static class CharSelectBgPatch
{
	public static void Postfix(NRegentCharacterSelectBg __instance)
	{
		ModEntry.Safe("CharSelectBg", () => CharSelectBg.Build(__instance));
	}
}

/// <summary>选人界面左侧介绍板：选中结城理时换成 P3R 风格，选其他角色时恢复（只改外观，见 CharSelectInfo）。</summary>
[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.SelectCharacter))]
public static class CharSelectInfoPatch
{
	public static void Postfix(NCharacterSelectScreen __instance, NCharacterSelectButton charSelectButton, MegaCrit.Sts2.Core.Models.CharacterModel characterModel)
	{
		ModEntry.Safe("CharSelectInfo", () => CharSelectInfo.Apply(__instance, charSelectButton, characterModel));
	}
}

/// <summary>虚空形态：原版常驻特效换成影时间（见 FormFx）。</summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Vfx.Forms.NVoidFormVfx), nameof(MegaCrit.Sts2.Core.Nodes.Vfx.Forms.NVoidFormVfx.Create))]
public static class VoidFormVfxPatch
{
	public static void Postfix(MegaCrit.Sts2.Core.Entities.Creatures.Creature target, MegaCrit.Sts2.Core.Nodes.Vfx.Forms.NVoidFormVfx? __result)
	{
		ModEntry.Safe("VoidFormVfx", () =>
		{
			if (__result != null && Makoto.Is(target.Player) && Makoto.Node(target.Player) is MegaCrit.Sts2.Core.Nodes.Combat.NCreature node)
			{
				Vfx.FormFx.DarkHour(__result, node);
			}
		});
	}
}

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Vfx.Forms.NVoidFormVfx), nameof(MegaCrit.Sts2.Core.Nodes.Vfx.Forms.NVoidFormVfx.SetActive))]
public static class VoidFormActivePatch
{
	public static void Postfix(MegaCrit.Sts2.Core.Nodes.Vfx.Forms.NVoidFormVfx __instance, bool isActive)
	{
		ModEntry.Safe("VoidFormActive", () => Vfx.FormFx.SetActive(__instance, isActive));
	}
}
