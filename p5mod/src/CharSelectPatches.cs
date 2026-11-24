using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;

namespace P5Joker;

/// <summary>
/// 选人界面：选中静默猎手时，原版刚放进 AnimatedBg 的背景场景（char_select_bg_silent）换成 P5R 风格的选人画面（Vfx/CharSelectScene.cs）。
/// 普通选人与恐惧模式各有入口，两者共用背景。恐惧模式原有的恐惧信息、锁链和灰阶继续由游戏绘制。
/// 同时：原版静默猎手的选中音效拦掉（选人画面自己安排了分层的声音）；左侧信息面板换成 P5 风格的介绍板（CharSelectInfo），等眼神特写碎开再入场（同 P3 mod）。
/// </summary>
[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.SelectCharacter))]
public static class CharSelectBgPatch
{
	private static readonly AccessTools.FieldRef<NCharacterSelectScreen, Control> BgContainer =
		AccessTools.FieldRefAccess<NCharacterSelectScreen, Control>("_bgContainer");
	private static readonly FieldInfo? SelectedButton = AccessTools.Field(typeof(NCharacterSelectScreen), "_selectedButton");

	private const string OriginalBackground = "p5_original_select_bg";
	private const string SilentBackground = "res://scenes/screens/char_select/char_select_bg_silent.tscn";
	private sealed class Selection
	{
		public Control? Root;
	}
	private static readonly ConditionalWeakTable<Control, Selection> Selections = new();
	private static readonly HashSet<string> LayoutProperties = new()
	{
		"layout_mode", "anchors_preset", "anchor_left", "anchor_top", "anchor_right", "anchor_bottom",
		"offset_left", "offset_top", "offset_right", "offset_bottom", "grow_horizontal", "grow_vertical", "pivot_offset",
	};

	private static bool IsJoker(NCharacterSelectButton button) =>
		!button.IsRandom && !button.IsLocked && button.Character is Silent && JokerCostume.Selected;

	// 0.111 另传 CharacterModel，0.113 从同一个按钮读 Character。两版都以第一个参数为准。
	public static void Prefix(Control __instance, NCharacterSelectButton __0)
	{
		ModEntry.Safe("CharSelectInfo restore", () => CharSelectInfo.Clear(__instance));
		if (__instance is NCharacterSelectScreen)
			ModEntry.Safe("SkinChanger selection setup", SkinChangerSelect.Install);
		CharSelectSfxMute.Armed = IsJoker(__0) && (__instance is not NCharacterSelectScreen
			|| SkinChangerSelect.ShowsJoker() != false);
	}

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
		call.opcode = OpCodes.Call;
		call.operand = AccessTools.Method(typeof(CharSelectBgPatch), nameof(InstantiateBackground));
		return body;
	}

	private static Control InstantiateBackground(PackedScene scene, PackedScene.GenEditState editState)
	{
		// A manager needs the real backdrop so it can restore it when our overlay leaves.
		if (scene.ResourcePath == SilentBackground && !SkinChangerSelect.Installed
			&& JokerCostume.ManagedSelection == null && JokerCostume.HasStandaloneSkeleton)
		{
			Control? root = null;
			try
			{
				SceneState state = scene.GetState();
				if (state.GetNodeCount() > 0 && state.GetNodeType(0).ToString() == nameof(Control))
				{
					// 保留原版场景根节点的布局。Joker 不需要实例化随后会隐藏的 Spine、粒子和灯光。
					root = new Control { Name = state.GetNodeName(0), MouseFilter = Control.MouseFilterEnum.Ignore };
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
			catch (Exception e)
			{
				root?.Free();
				Stage.Report("CharSelect background layout", e);
			}
		}
		return scene.Instantiate<Control>(editState);
	}

	// SkinChanger replays this postfix by the game's parameter name.
	[HarmonyAfter(SkinChangerSelect.ManagerId)]
	public static void Postfix(NCharacterSelectScreen __instance, NCharacterSelectButton charSelectButton)
	{
		CharSelectSfxMute.Armed = false;
		ModEntry.Safe("CharSelectBg", () =>
		{
			if (BgContainer(__instance) is Control container)
				ApplySelection(__instance, container, charSelectButton, SkinChangerSelect.ShowsJoker);
		});
	}

	internal static void ApplySelection(Control screen, Control container, NCharacterSelectButton button,
		Func<bool?>? selected = null, bool waitForTransition = true)
	{
		bool ShowsJoker() => selected?.Invoke() ?? JokerCostume.Selected;
		CharacterModel? model = !button.IsRandom && !button.IsLocked && button.Character is Silent && ShowsJoker()
			? button.Character : null;
		Control? bg = model == null ? null : container.GetNodeOrNull<Control>(model.Id.Entry + "_bg")
			?? container.GetChildren().OfType<Control>().FirstOrDefault(c =>
				c.HasMeta(OriginalBackground) || c.SceneFilePath == SilentBackground);
		if (bg != null && Selections.TryGetValue(screen, out Selection? existing)
			&& existing.Root is { } root && Fx.Active(root) && root.GetParent() == bg) return;
		waitForTransition = waitForTransition && SaveManager.Instance?.PrefsSave?.FastMode is not (FastModeType.Fast or FastModeType.Instant);
		Select(screen, container, bg, () => CharSelectInfo.Prepare(screen, waitForTransition), selected: ShowsJoker,
			waitForTransition: waitForTransition);
	}

	internal static void AfterSkinChangerRefresh(NCharacterSelectScreen screen, CharacterModel character, string groupId)
	{
		if (character is Silent && Fx.Active(screen) && screen.IsVisibleInTree()
			&& JokerCostume.ManagedSelection == null
			&& SelectedButton?.GetValue(screen) is NCharacterSelectButton button && button.Character is Silent
			&& BgContainer(screen) is Control container)
		{
			ApplySelection(screen, container, button, () => SkinChangerSelect.ShowsJoker(groupId));
		}
	}

	internal static void SelectDread(Control screen)
	{
		if (screen.GetNodeOrNull<Control>("%AnimatedBg") is not Control container) return;
		// The game resolves the rotating character before creating the background.
		Control? bg = JokerCostume.Selected ? container.GetChildren().OfType<Control>().FirstOrDefault(c =>
			c.HasMeta(OriginalBackground) || c.SceneFilePath == SilentBackground) : null;
		Select(screen, container, bg);
	}

	internal static void Select(Control screen, Control container, Control? bg, Func<Action<bool?>?>? preparePanel = null,
		bool playIntro = true, Func<bool>? selected = null, bool waitForTransition = true)
	{
		Selection selection = Selections.GetOrCreateValue(screen);
		if (selection.Root is { } previous && GodotObject.IsInstanceValid(previous))
		{
			previous.GetParent()?.RemoveChild(previous);
			previous.QueueFree();
		}
		if (bg == null || !Fx.Active(bg)) return;

		var originalChildren = bg.GetChildren().OfType<CanvasItem>().Where(child => child.Visible).ToArray();
		// SkinChanger records the new roots at the end of the postfix. Attach the
		// root now, then stream its textures and children over subsequent frames.
		Control root = CharSelectScene.CreateRoot(bg);
		selection.Root = root;
		Action<bool?>? panel = null;
		root.TreeExiting += () => ModEntry.Safe("CharSelect restore", () =>
		{
			if (selection.Root == root) selection.Root = null;
			foreach (CanvasItem child in originalChildren)
				if (GodotObject.IsInstanceValid(child) && child.GetParent() == bg) child.Show();
			panel?.Invoke(null);
		});
		foreach (CanvasItem child in originalChildren) child.Hide();
		if (preparePanel != null) ModEntry.Safe("CharSelectInfo", () => panel = preparePanel());
		_ = ShowBackground();

		async Task ShowBackground()
		{
			bool Current() => selection.Root == root && Fx.Active(root) && root.GetParent() == bg
				&& Fx.Active(bg) && bg.GetParent() == container && Fx.Active(screen) && screen.IsVisibleInTree()
				&& (selected?.Invoke() ?? JokerCostume.Selected);
			bool CanDraw() => !waitForTransition || NGame.Instance?.Transition.InTransition != true;
			bool ready = false;
			try
			{
				Warmup.MenuShown();
				bool intro = playIntro && CharSelectScene.IntroEnabled();
				if (await CharSelectScene.BuildAsync(root, intro, Current, CanDraw, prepareInfo: panel != null) && Current())
				{
					if (bg.HasMeta(OriginalBackground)) bg.RemoveMeta(OriginalBackground);
					GD.Print($"[P5Joker] character select background attached: {screen.GetType().Name}");
					panel?.Invoke(intro);
					ready = true;
					await EmbarkTransition.PrepareTextures(bg, Current);
				}
			}
			catch (Exception e)
			{
				ModEntry.Safe("CharSelect fallback", () =>
				{
					if (!Current() || !bg.HasMeta(OriginalBackground)
						|| bg.GetMeta(OriginalBackground).As<PackedScene>() is not PackedScene original) return;
					Control vanilla = original.Instantiate<Control>();
					vanilla.Name = bg.Name;
					container.RemoveChild(bg);
					bg.QueueFree();
					container.AddChild(vanilla);
				});
				Stage.Report("CharSelectBg", e);
			}
			finally
			{
				if (!ready && GodotObject.IsInstanceValid(root)) ModEntry.Safe("CharSelect cleanup", () =>
				{
					root.GetParent()?.RemoveChild(root);
					root.QueueFree();
				});
			}
		}
	}
}

// Blitz has a separate selection screen in v0.113. Resolve it at runtime so
// the same package still loads on versions that predate that mode.
[HarmonyPatch]
public static class BlitzSelectBgPatch
{
	private static readonly MethodInfo? SelectCharacter = typeof(NCharacterSelectScreen).Assembly
		.GetType("MegaCrit.Sts2.Core.Nodes.Screens.Blitz.NBlitzScreen")
		?.GetMethod("SelectCharacter", new[] { typeof(NCharacterSelectButton) });

	public static bool Prepare() => SelectCharacter != null;
	public static MethodBase? TargetMethod() => SelectCharacter;
	public static void Prefix(Control __instance, NCharacterSelectButton __0) => CharSelectBgPatch.Prefix(__instance, __0);
	public static void Finalizer() => CharSelectBgPatch.Finalizer();
	public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => CharSelectBgPatch.Transpiler(instructions);

	[HarmonyAfter(SkinChangerSelect.ManagerId)]
	public static void Postfix(Control __instance, NCharacterSelectButton __0, Control ____bgContainer)
	{
		CharSelectSfxMute.Armed = false;
		// The preferred character can be selected while the submenu is opening.
		Callable.From(() => ModEntry.Safe("Blitz character select", () =>
		{
			if (Fx.Active(__instance) && __instance.IsVisibleInTree() && Fx.Active(____bgContainer)
				&& Traverse.Create(__instance).Field("_selectedButton").GetValue<NCharacterSelectButton>() == __0)
				CharSelectBgPatch.ApplySelection(__instance, ____bgContainer, __0, waitForTransition: false);
		})).CallDeferred();
	}
}

// Resuming multiplayer creates its own character backdrop, for the local saved
// character on both host and client. Leave the lobby controls and save info alone.
internal static class MultiplayerResumeBackground
{
	private static readonly AccessTools.FieldRef<NMultiplayerLoadGameScreen, Control> BgContainer =
		AccessTools.FieldRefAccess<NMultiplayerLoadGameScreen, Control>("_bgContainer");

	internal static void Set(NMultiplayerLoadGameScreen screen, bool open) => ModEntry.Safe("Multiplayer resume backdrop", () =>
	{
		if (BgContainer(screen) is not Control container) return;
		Control? bg = open && JokerCostume.Selected ? container.GetNodeOrNull<Control>("SILENT_bg") : null;
		CharSelectBgPatch.Select(screen, container, bg, playIntro: false);
	});
}

[HarmonyPatch(typeof(NMultiplayerLoadGameScreen), nameof(NMultiplayerLoadGameScreen.OnSubmenuOpened))]
internal static class MultiplayerResumeOpenPatch
{
	public static void Postfix(NMultiplayerLoadGameScreen __instance) => MultiplayerResumeBackground.Set(__instance, true);
}

[HarmonyPatch(typeof(NMultiplayerLoadGameScreen), nameof(NMultiplayerLoadGameScreen.OnSubmenuClosed))]
internal static class MultiplayerResumeClosePatch
{
	public static void Postfix(NMultiplayerLoadGameScreen __instance) => MultiplayerResumeBackground.Set(__instance, false);
}

// The Dread screen is absent in 0.111. Target its actual visual swap, including
// the calls made outside SelectCharacter when a rotating streak ends.
[HarmonyPatch]
public static class DreadSelectBgPatch
{
	private static readonly MethodInfo? Visuals = FindVisuals();

	private static MethodInfo? FindVisuals()
	{
		Type? screen = typeof(NCharacterSelectScreen).Assembly.GetType("MegaCrit.Sts2.Core.Nodes.Screens.Dread.NDreadScreen");
		return screen == null ? null : AccessTools.DeclaredMethod(screen, "SetSelectedCharacterVisuals", new[] { typeof(CharacterModel) });
	}

	public static bool Prepare() => Visuals != null;
	public static MethodBase? TargetMethod() => Visuals;

	public static void Prefix(Control __instance, CharacterModel __0)
	{
		CharacterModel model = __0;
		if (__0.GetType().Name == "RotatingCharacter")
		{
			Traverse screen = Traverse.Create(__instance);
			model = screen.Field("_rotatingDisplayCharacterOverride").GetValue<CharacterModel>()
				?? screen.Field("_rotatingCharacter").GetValue<CharacterModel>() ?? model;
		}
		CharSelectSfxMute.Armed = model is Silent && JokerCostume.Selected;
	}

	public static void Finalizer() => CharSelectBgPatch.Finalizer();
	public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
		CharSelectBgPatch.Transpiler(instructions);
	public static void Postfix(Control __instance)
	{
		CharSelectSfxMute.Armed = false;
		ModEntry.Safe("DreadSelectBg", () => CharSelectBgPatch.SelectDread(__instance));
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
			if (character is Silent && !__instance.IsLocked && JokerCostume.Selected
				&& Fx.Tex("cs_button") is Texture2D tex && Icon(__instance) is TextureRect icon)
			{
				icon.Texture = tex;
			}
		});
	}
}
