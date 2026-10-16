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
		ModEntry.Safe("CharSelectBg", () =>
		{
			if (!Makoto.AssetsOurs)
			{
				return;                                 // 储君骨骼被别的换皮 mod 换掉了：选人界面也让给它
			}
			Vfx.Perf.WatchFrames("select makoto", 3.0);                 // 卡顿定位：选中后 3 秒里的慢帧写进日志
			Vfx.Perf.Time("charselect bg build", () => CharSelectBg.Build(__instance));
		});
	}
}

/// <summary>选人界面左侧介绍板：选中结城理时换成 P3R 人格面具状态界面风格，选其他角色时恢复（只改显示，见 CharSelectInfo）。</summary>
[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.SelectCharacter))]
public static class CharSelectInfoPatch
{
	public static void Postfix(NCharacterSelectScreen __instance, NCharacterSelectButton charSelectButton, MegaCrit.Sts2.Core.Models.CharacterModel characterModel)
	{
		ModEntry.Safe("CharSelectInfo", () => Vfx.Perf.Time("info panel apply", () => CharSelectInfo.Apply(__instance, charSelectButton, characterModel)));
	}
}

/// <summary>虚空形态：原版常驻特效换成影时间（见 FormFx）。v0.14.45：原版特效类只有 0.111 起才有，按名字找，找不到（0.107）就不挂（见 VoidFormLegacyPatch）。</summary>
[HarmonyPatch]
public static class VoidFormVfxPatch
{
	public static readonly System.Type? VfxType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.Vfx.Forms.NVoidFormVfx");

	public static bool Prepare() => VfxType != null;

	public static System.Reflection.MethodBase? TargetMethod() => VfxType == null ? null : AccessTools.Method(VfxType, "Create");

	public static void Postfix(MegaCrit.Sts2.Core.Entities.Creatures.Creature target, object? __result)
	{
		ModEntry.Safe("VoidFormVfx", () =>
		{
			if (__result is Node2D vanilla && Makoto.Is(target.Player) && Makoto.Node(target.Player) is MegaCrit.Sts2.Core.Nodes.Combat.NCreature node)
			{
				Vfx.FormFx.DarkHour(vanilla, node);
			}
		});
	}
}

[HarmonyPatch]
public static class VoidFormActivePatch
{
	public static bool Prepare() => VoidFormVfxPatch.VfxType != null;

	public static System.Reflection.MethodBase? TargetMethod() => VoidFormVfxPatch.VfxType == null ? null : AccessTools.Method(VoidFormVfxPatch.VfxType, "SetActive");

	public static void Postfix(object __instance, bool isActive)
	{
		ModEntry.Safe("VoidFormActive", () =>
		{
			if (__instance is Node2D vanilla)
			{
				Vfx.FormFx.SetActive(vanilla, isActive);
			}
		});
	}
}

/// <summary>
/// v0.14.45：0.107.1（Steam 正式版）还没有虚空形态的原版特效（NVoidFormVfx），上面两个补丁在那个版本上挂不上。
/// 那里改为：虚空形态生效时自己在人物节点下建一个宿主节点，影时间挂在它下面（人物 / 战斗房间释放时一起释放）；效果移除时调暗。
/// 0.111 的 VoidFormPower 自己重写了 AfterApplied / AfterRemoved，不会走到基类这里，所以两个版本不会重复。
/// </summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Models.PowerModel), nameof(MegaCrit.Sts2.Core.Models.PowerModel.AfterApplied))]
public static class VoidFormLegacyPatch
{
	public const string HostName = "P3DarkHourHost";

	private static readonly bool NoVanillaVfx = AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.Vfx.Forms.NVoidFormVfx") == null;

	public static void Postfix(MegaCrit.Sts2.Core.Models.PowerModel __instance)
	{
		if (!NoVanillaVfx || __instance is not MegaCrit.Sts2.Core.Models.Powers.VoidFormPower)
		{
			return;
		}
		ModEntry.Safe("VoidFormLegacy", () =>
		{
			MegaCrit.Sts2.Core.Entities.Creatures.Creature? owner = __instance.Owner;
			if (owner?.Player is not { } player || !Makoto.Is(player) || Makoto.Node(player) is not MegaCrit.Sts2.Core.Nodes.Combat.NCreature node)
			{
				return;
			}
			if (node.GetNodeOrNull<Node2D>(HostName) is Node2D old)
			{
				Vfx.FormFx.SetActive(old, true);
				return;
			}
			Node2D host = new() { Name = HostName };
			node.AddChild(host);
			Vfx.FormFx.DarkHour(host, node);
		});
	}
}

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Models.PowerModel), nameof(MegaCrit.Sts2.Core.Models.PowerModel.AfterRemoved))]
public static class VoidFormLegacyRemovedPatch
{
	public static void Postfix(MegaCrit.Sts2.Core.Models.PowerModel __instance, MegaCrit.Sts2.Core.Entities.Creatures.Creature oldOwner)
	{
		if (__instance is not MegaCrit.Sts2.Core.Models.Powers.VoidFormPower)
		{
			return;
		}
		ModEntry.Safe("VoidFormLegacyRemoved", () =>
		{
			if (oldOwner.Player is { } player && Makoto.Node(player)?.GetNodeOrNull<Node2D>(VoidFormLegacyPatch.HostName) is Node2D host)
			{
				Vfx.FormFx.SetActive(host, false);
			}
		});
	}
}
