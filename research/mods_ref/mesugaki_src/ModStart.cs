using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

[ModInitializer("ModInit")]
public static class ModStart
{
	public static void ModInit()
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Expected Obj, but got Unknown
		GD.Print("[Mesugaki Mode] Mod Initialized");
		Harmony val = new Harmony("com.seicoh.mesugaki.mod");
		val.PatchAll();
	}
}
