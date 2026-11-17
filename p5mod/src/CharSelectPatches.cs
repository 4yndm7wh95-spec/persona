using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace P5Joker;

/// <summary>
/// 选人界面：选中静默猎手时，原版刚放进 AnimatedBg 的背景场景（char_select_bg_silent）换成 P5R 风格的选人画面（Vfx/CharSelectScene.cs）。
/// 原版每次选中都会重新实例化背景，所以每次选中都是"刚被选中"：播一次入场 + 眼神特写。搭建出错就保留原版（CharSelectScene.Build 自己回滚）。
/// 同时：原版静默猎手的选中音效拦掉（选人画面自己安排了分层的声音）；左侧信息面板换成 P5 风格的介绍板（CharSelectInfo），等眼神特写碎开再入场（同 P3 mod）。
/// </summary>
[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.SelectCharacter))]
public static class CharSelectBgPatch
{
	private static readonly AccessTools.FieldRef<NCharacterSelectScreen, Control> BgContainer =
		AccessTools.FieldRefAccess<NCharacterSelectScreen, Control>("_bgContainer");

	private const string OriginalBackground = "p5_original_select_bg";
	private static readonly HashSet<string> LayoutProperties = new()
	{
		"layout_mode", "anchors_preset", "anchor_left", "anchor_top", "anchor_right", "anchor_bottom",
		"offset_left", "offset_top", "offset_right", "offset_bottom", "grow_horizontal", "grow_vertical", "pivot_offset",
	};

	private static bool IsJoker(NCharacterSelectButton button) =>
		!button.IsRandom && !button.IsLocked && button.Character is Silent;

	// 0.111 另传 CharacterModel，0.113 从同一个按钮读 Character。两版都以第一个参数为准。
	public static void Prefix(NCharacterSelectButton __0) => CharSelectSfxMute.Armed = IsJoker(__0);

	public static void Finalizer() => CharSelectSfxMute.Armed = false;

	public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
	{
		List<CodeInstruction> body = instructions.ToList();
		var calls = body.Where(i => (i.opcode == OpCodes.Call || i.opcode == OpCodes.Callvirt)
			&& i.operand is MethodInfo method && method.DeclaringType == typeof(PackedScene)
			&& method.Name == nameof(PackedScene.Instantiate) && method.IsGenericMethod
			&& method.ReturnType == typeof(Control)
			&& method.GetParameters() is { Length: 1 } parameters
			&& parameters[0].ParameterType == typeof(PackedScene.GenEditState)).ToArray();
		if (calls.Length != 1 || calls[0].blocks.Count != 0)
		{
			return body;   // 游戏改动这段 IL 时，仍可在 Postfix 替换原版的背景内容。
		}

		CodeInstruction call = calls[0];
		var button = new CodeInstruction(OpCodes.Ldarg_1).MoveLabelsFrom(call);
		body.Insert(body.IndexOf(call), button);
		call.opcode = OpCodes.Call;
		call.operand = AccessTools.Method(typeof(CharSelectBgPatch), nameof(InstantiateBackground));
		return body;
	}

	private static Control InstantiateBackground(PackedScene scene, PackedScene.GenEditState editState, NCharacterSelectButton button)
	{
		if (IsJoker(button))
		{
			Control? root = null;
			try
			{
				SceneState state = scene.GetState();
				if (state.GetNodeCount() > 0 && state.GetNodeType(0).ToString() == nameof(Control))
				{
					// 保留原版场景根节点的布局。Joker 不需要实例化随后会隐藏的 Spine、粒子和灯光。
					root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
					for (int i = 0; i < state.GetNodePropertyCount(0); i++)
					{
						StringName property = state.GetNodePropertyName(0, i);
						if (LayoutProperties.Contains(property.ToString()))
							root.Set(property, state.GetNodePropertyValue(0, i));
					}
					root.SetMeta(OriginalBackground, scene);
					return root;
				}
			}
			catch (System.Exception e)
			{
				root?.Free();
				Stage.Report("CharSelect background layout", e);
			}
		}
		return scene.Instantiate<Control>(editState);
	}

	public static void Postfix(NCharacterSelectScreen __instance, NCharacterSelectButton __0)
	{
		CharSelectSfxMute.Armed = false;
		bool built = false;
		bool intro = CharSelectScene.IntroEnabled();
		CharacterModel? model = IsJoker(__0) ? __0.Character : null;
		ModEntry.Safe("CharSelectBg", () =>
		{
			if (model == null || BgContainer(__instance) is not Control container
				|| container.GetNodeOrNull<Control>(model.Id.Entry + "_bg") is not Control bg)
				return;

			if (bg.GetNodeOrNull("P5Select") == null)
			{
				try
				{
					CharSelectScene.Build(bg, intro);
				}
				catch
				{
					if (bg.HasMeta(OriginalBackground) && bg.GetMeta(OriginalBackground).As<PackedScene>() is PackedScene original)
					{
						Control vanilla = original.Instantiate<Control>();
						vanilla.Name = bg.Name;
						container.RemoveChild(bg);
						bg.QueueFree();
						container.AddChild(vanilla);
					}
					throw;
				}
			}
			if (bg.HasMeta(OriginalBackground)) bg.RemoveMeta(OriginalBackground);
			built = true;
			Warmup.MenuShown();
		});
		ModEntry.Safe("CharSelectInfo", () => CharSelectInfo.Apply(__instance, __0, built ? model : null, intro));
	}
}

/// <summary>
/// 拦掉一次原版静默猎手的选中音效（event:/sfx/characters/silent/silent_select）：只在 CharSelectBgPatch 要换成 Joker 画面的那一次生效。
/// 另外点出发时的 event:/sfx/ui/wipe_silent 也拦掉（出发过场换成了 EmbarkTransition，自己有声音）。
/// </summary>
[HarmonyPatch]
public static class CharSelectSfxMute
{
	public static bool Armed;
	private const string Event = "event:/sfx/characters/silent/silent_select";

	public static IEnumerable<MethodBase> TargetMethods() =>
		typeof(MegaCrit.Sts2.Core.Nodes.Audio.NAudioManager).GetMethods(BindingFlags.Public | BindingFlags.Instance)
			.Where(m => m.Name == "PlayOneShot" && m.GetParameters().Length > 0 && m.GetParameters()[0].ParameterType == typeof(string));

	public static bool Prefix(object[] __args)
	{
		if (__args.Length == 0 || __args[0] is not string path)
		{
			return true;
		}
		// 点出发时原版的 wipe_silent：出发过场（EmbarkPatches）自己有声音
		if (Embark.MutesWipe(path))
		{
			return false;
		}
		if (!Armed || path != Event)
		{
			return true;
		}
		Armed = false;
		return false;
	}
}

/// <summary>选人界面底部的角色按钮：静默猎手换成 Joker 头像（res://p5joker/fx/cs_button.png，未解锁时原版的锁定图不动）。</summary>
[HarmonyPatch(typeof(NCharacterSelectButton), nameof(NCharacterSelectButton.Init))]
public static class CharSelectButtonPatch
{
	private static readonly AccessTools.FieldRef<NCharacterSelectButton, TextureRect> Icon =
		AccessTools.FieldRefAccess<NCharacterSelectButton, TextureRect>("_icon");

	public static void Postfix(NCharacterSelectButton __instance, CharacterModel character)
	{
		ModEntry.Safe("CharSelectButton", () =>
		{
			if (character is Silent && !__instance.IsLocked && Fx.Tex("cs_button") is Texture2D tex && Icon(__instance) is TextureRect icon)
			{
				icon.Texture = tex;
			}
		});
	}
}
