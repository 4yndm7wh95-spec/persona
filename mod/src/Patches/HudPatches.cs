using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using P3Makoto.Scenes;
using P3Makoto.Vfx;

namespace P3Makoto.Patches;

/// <summary>
/// 鼠标光标换成 P3R 风格（p3makoto/ui/cursor*.png，48×48，尖端在原版热点 (14,5)）：
/// 在光标管理器第一次设置光标之前替换它的两张默认图（普通 / 按下倾斜），原版的按下切换、"查看"光标、临时覆盖都照常工作。
/// </summary>
[HarmonyPatch(typeof(NCursorManager), "_EnterTree")]
public static class CursorPatch
{
	public static void Prefix(NCursorManager __instance)
	{
		ModEntry.Safe("cursor", () =>
		{
			if (Load("res://p3makoto/ui/cursor.png") is Image normal && Load("res://p3makoto/ui/cursor_tilted.png") is Image tilted)
			{
				Traverse t = Traverse.Create(__instance);
				t.Field("_cursorNotTilted").SetValue(normal);
				t.Field("_cursorTilted").SetValue(tilted);
			}
		});
	}

	private static Image? Load(string path)
	{
		if (ResourceLoader.Load(path) is not Texture2D tex || tex.GetImage() is not Image img)
		{
			return null;
		}
		if (img.IsCompressed())
		{
			img.Decompress();
		}
		img.Convert(Image.Format.Rgba8);
		return img;
	}
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
