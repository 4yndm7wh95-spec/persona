using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using P3Makoto.Scenes;
using P3Makoto.Core;
using P3Makoto.Vfx;

namespace P3Makoto.Patches;

/// <summary>
/// 鼠标光标换成 P3R 风格（p3makoto/ui/cursor*.png，64×64，热点 (14,5)）。
/// 启动、晚加载补应用、原版刷新共用一次性替换；保留查看/临时覆盖/手柄隐藏逻辑。
/// </summary>
[HarmonyPatch(typeof(NCursorManager), "_EnterTree")]
public static class CursorPatch
{
	private sealed class State
	{
		internal bool Applied;
		internal bool? RequestedSkin;
		internal Image? OriginalNormal, OriginalTilted;
		internal int Attempts;
		internal ulong NextTry;
	}
	private static readonly ConditionalWeakTable<NCursorManager, State> States = new();
	private static readonly FieldInfo? NormalField = AccessTools.Field(typeof(NCursorManager), "_cursorNotTilted");
	private static readonly FieldInfo? TiltedField = AccessTools.Field(typeof(NCursorManager), "_cursorTilted");
	private static readonly MethodInfo? Update = AccessTools.Method(typeof(NCursorManager), "UpdateCursor");
	private static Image? normal, tilted;

	public static void Prefix(NCursorManager __instance) => Ensure(__instance);

	internal static bool Ensure(NCursorManager manager)
	{
		if (!GodotObject.IsInstanceValid(manager)) return false;
		State state = States.GetValue(manager, _ => new State());
		bool wantSkin = P3Settings.Current.CursorSkin;
		if (state.RequestedSkin != wantSkin)
		{
			state.RequestedSkin = wantSkin;
			state.Attempts = 0;
			state.NextTry = 0;
		}
		if (state.Applied == wantSkin) return true;
		if (state.Attempts >= 3 || Time.GetTicksMsec() < state.NextTry) return false;
		state.Attempts++;
		state.NextTry = Time.GetTicksMsec() + 1000;
		try
		{
			if (NormalField == null || TiltedField == null || Update == null)
				throw new MissingMemberException("NCursorManager cursor fields/UpdateCursor unavailable");
			if (wantSkin)
			{
				normal ??= Load("res://p3makoto/ui/cursor.png");
				tilted ??= Load("res://p3makoto/ui/cursor_tilted.png");
				// 保留这个游戏版本、这个节点实际加载的原图；恢复时不猜路径或换成系统箭头。
				state.OriginalNormal ??= NormalField.GetValue(manager) as Image;
				state.OriginalTilted ??= TiltedField.GetValue(manager) as Image;
				if (!GodotObject.IsInstanceValid(state.OriginalNormal) || !GodotObject.IsInstanceValid(state.OriginalTilted))
					throw new InvalidOperationException("vanilla cursor images unavailable");
				NormalField.SetValue(manager, normal);
				TiltedField.SetValue(manager, tilted);
			}
			else
			{
				NormalField.SetValue(manager, state.OriginalNormal);
				TiltedField.SetValue(manager, state.OriginalTilted);
			}
			state.Applied = wantSkin;
			GD.Print($"{ModEntry.Tag} cursor style applied: {(wantSkin ? "P3R" : "vanilla")}, attempt={state.Attempts}");
			return true;
		}
		catch (Exception e)
		{
			ModEntry.Warn($"cursor skin attempt {state.Attempts}/3 failed: {e.Message}; keeping current cursor");
			if (state.Attempts < 3 && manager.IsInsideTree())
			{
				manager.GetTree().CreateTimer(1.05).Timeout += () => ModEntry.Safe("cursor retry", () => Refresh(manager));
			}
			return false;
		}
	}

	private static void Refresh(NCursorManager manager)
	{
		if (GodotObject.IsInstanceValid(manager) && manager.IsInsideTree() && Ensure(manager))
			Update?.Invoke(manager, null); // 让原版选择当前普通/按下/临时覆盖，不强制写一个 Arrow 覆盖其他状态。
	}

	/// <summary>兼容 mod 初始化之前光标节点已经入树的情况；初始化和设置切换时扫描，不逐帧抢占光标。</summary>
	internal static void RefreshExisting()
	{
		if (Engine.GetMainLoop() is not SceneTree tree) return;
		Stack<Node> pending = new();
		pending.Push(tree.Root);
		while (pending.Count > 0)
		{
			Node node = pending.Pop();
			if (node is NCursorManager manager) Refresh(manager);
			foreach (Node child in node.GetChildren()) pending.Push(child);
		}
	}

	private static Image Load(string path)
	{
		Image? img = null;
		try
		{
			if (ResourceLoader.Exists(path) && ResourceLoader.Load(path) is Texture2D tex) img = tex.GetImage();
			if (img != null && img.IsCompressed() && img.Decompress() != Error.Ok) img = null;
		}
		catch (Exception e) { img = null; GD.Print($"{ModEntry.Tag} cursor texture read failed: {path}: {e.Message}; trying PNG"); }
		if (img == null || img.IsEmpty())
		{
			if (!FileAccess.FileExists(path)) throw new InvalidOperationException("cursor image missing: " + path);
			img = new Image();
			Error error = img.LoadPngFromBuffer(FileAccess.GetFileAsBytes(path));
			if (error != Error.Ok) throw new InvalidOperationException($"cursor PNG decode failed ({error}): {path}");
			GD.Print($"{ModEntry.Tag} cursor raw PNG fallback: {path}");
		}
		if (img.GetWidth() <= 14 || img.GetHeight() <= 5 || img.GetWidth() > 256 || img.GetHeight() > 256)
			throw new InvalidOperationException($"invalid cursor size {img.GetWidth()}x{img.GetHeight()}: {path}");
		img.Convert(Image.Format.Rgba8);
		return img;
	}
}

[HarmonyPatch(typeof(NCursorManager), "UpdateCursor")]
public static class CursorRefreshPatch
{
	public static void Prefix(NCursorManager __instance) => CursorPatch.Ensure(__instance);
}

/// <summary>
/// 结城理的对局：原版"战斗开始"横条不显示（回合数由 StageFx 的 P3R 回合条负责）。
/// 只隐藏画面，节点照常运行 —— 它播完后负责生成第 1 回合的回合条，不能直接去掉。
/// </summary>
[HarmonyPatch(typeof(NCombatStartBanner), nameof(NCombatStartBanner._Ready))]
public static class CombatStartBannerPatch
{
	public static void Postfix(NCombatStartBanner __instance)
	{
		ModEntry.Safe("combat start banner", () =>
		{
			if (Stage.MakotoCombat())
			{
				foreach (Node c in __instance.GetChildren())
				{
					if (c is CanvasItem ci)
					{
						ci.Visible = false;
					}
				}
			}
		});
	}
}

/// <summary>结束回合按钮：结城理的对局换成 P3R 风格外观（见 EndTurnSkin），逻辑完全是原版的。</summary>
[HarmonyPatch(typeof(NEndTurnButton), nameof(NEndTurnButton._Ready))]
public static class EndTurnButtonPatch
{
	public static void Postfix(NEndTurnButton __instance)
	{
		ModEntry.Safe("end turn skin", () =>
		{
			EndTurnSkin.Apply(__instance);                  // 总是装上：按钮在角色加载前就创建了，是否显示 P3R 外观由它每帧自己判断
		});
	}
}

/// <summary>v0.14.59：能量 / 辉星 / 牌堆按钮换成 P3R 风格（见 HudSkin），只在结城理的对局里显示，逻辑完全是原版的。</summary>
[HarmonyPatch(typeof(NEnergyCounter), nameof(NEnergyCounter._Ready))]
public static class EnergyCounterSkinPatch
{
	public static void Postfix(NEnergyCounter __instance) => ModEntry.Safe("hud energy skin", () => HudSkin.ApplyEnergy(__instance));
}

[HarmonyPatch(typeof(NStarCounter), nameof(NStarCounter._Ready))]
public static class StarCounterSkinPatch
{
	public static void Postfix(NStarCounter __instance) => ModEntry.Safe("hud star skin", () => HudSkin.ApplyStars(__instance));
}

/// <summary>三个牌堆按钮的 _Ready 都只调用 ConnectSignals（基类的），在它之后挂装饰。</summary>
[HarmonyPatch(typeof(NCombatCardPile), "ConnectSignals")]
public static class CombatPileSkinPatch
{
	public static void Postfix(NCombatCardPile __instance) => ModEntry.Safe("hud pile skin", () => HudSkin.ApplyPile(__instance));
}

/// <summary>
/// v0.14.66：结城理的对局里，玩家角色的状态说明改成血条右边的「状态」按钮 + P3R 滚动列表（Scenes/TipList）；
/// 悬停玩家角色不再弹出原版那一大片文字说明（引用卡牌的预览照常）。敌人保持原版。设置里可关。
/// </summary>
[HarmonyPatch(typeof(NCreature), nameof(NCreature.ShowHoverTips))]
public static class CreatureTipListPatch
{
	public static bool Prefix(NCreature __instance, IEnumerable<MegaCrit.Sts2.Core.HoverTips.IHoverTip> hoverTips)
	{
		if (!Core.P3Settings.TipList || !Stage.MakotoCombat() || __instance.Entity?.Player is not MegaCrit.Sts2.Core.Entities.Players.Player p || !Core.Makoto.Is(p))
		{
			return true;
		}
		bool handled = false;
		ModEntry.Safe("tip list hover", () => handled = TipList.Hover(__instance, hoverTips));
		return !handled;
	}
}

/// <summary>玩家角色节点就绪后挂上「状态」按钮（是否显示由按钮每帧自己判断）。</summary>
[HarmonyPatch(typeof(NCreature), nameof(NCreature._Ready))]
public static class CreatureStatusButtonPatch
{
	public static void Postfix(NCreature __instance)
	{
		NCreature c = __instance;
		Callable.From(() => ModEntry.Safe("status button", () =>
		{
			if (GodotObject.IsInstanceValid(c) && c.Entity?.Player is MegaCrit.Sts2.Core.Entities.Players.Player p && Core.Makoto.Is(p))
			{
				StatusButton.Attach(c);
			}
		})).CallDeferred();
	}
}
