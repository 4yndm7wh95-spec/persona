using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace P5Joker;

/// <summary>
/// 选人界面左侧的介绍板（照结城理 mod 的 CharSelectInfo）：选中 Joker（静默猎手）时把原版面板整块藏掉，在它下面搭 P5R 风格的介绍板
/// （Vfx/CharSelectInfoPanel.cs，数值 / 天赋名称 / 说明 / 图标都实时读原版的标签）；选别的角色时全部恢复原样。只改显示。
/// 介绍板等眼神特写碎开之后再入场（CharSelectScene.InfoPanelDelay），两者不同时出现。
/// </summary>
public static class CharSelectInfo
{
	private const string DecoName = "P5InfoDeco";

	private sealed class Saved
	{
		public readonly List<(CanvasItem Item, Color Color, bool Self)> Hidden = new();
		public Node2D Anchor = null!;
	}

	private static readonly Dictionary<ulong, Saved> State = new();

	internal static void Clear(NCharacterSelectScreen screen)
	{
		if (Traverse.Create(screen).Field("_infoPanel").GetValue<Control>() is Control panel)
			Restore(panel);
	}

	// Hide the stock panel as soon as Joker is selected. Reveal our panel after
	// the backdrop is ready, or restore the stock panel if the backdrop fails.
	internal static Action<bool?>? Prepare(NCharacterSelectScreen screen)
	{
		Control? panel = Traverse.Create(screen).Field("_infoPanel").GetValue<Control>();
		if (panel == null || !GodotObject.IsInstanceValid(panel))
		{
			return null;
		}
		Node2D anchor = new() { Name = DecoName };
		Saved saved = new() { Anchor = anchor };
		State[panel.GetInstanceId()] = saved;
		anchor.TreeExiting += () => Restore(panel, saved, removeAnchor: false);   // 别的 mod 拆掉我们的节点时也恢复原版
		try
		{
			panel.AddChild(anchor);
			// 原版面板整块隐藏：底板（面板自身 + 非容器子节点）用 SelfModulate，内容容器用 Modulate（连同所有子节点）
			saved.Hidden.Add((panel, panel.SelfModulate, true));
			panel.SelfModulate = new Color(1f, 1f, 1f, 0f);
			foreach (Node child in panel.GetChildren())
			{
				if (child is not CanvasItem ci || child == anchor || child.Name == "P3InfoDeco")
				{
					continue;
				}
				if (child is Container)
				{
					saved.Hidden.Add((ci, ci.Modulate, false));
					ci.Modulate = new Color(1f, 1f, 1f, 0f);
				}
				else
				{
					saved.Hidden.Add((ci, ci.SelfModulate, true));
					ci.SelfModulate = new Color(1f, 1f, 1f, 0f);
				}
			}
			string hp = Traverse.Create(screen).Field("_hp").GetValue<Label>()?.Text ?? "";
			string gold = Traverse.Create(screen).Field("_gold").GetValue<Label>()?.Text ?? "";
			RichTextLabel? relicTitle = Traverse.Create(screen).Field("_relicTitle").GetValue<RichTextLabel>();
			RichTextLabel? relicDesc = Traverse.Create(screen).Field("_relicDescription").GetValue<RichTextLabel>();
			Texture2D? relicIcon = Traverse.Create(screen).Field("_relicIcon").GetValue<TextureRect>()?.Texture;
			CharSelectInfoPanel.Data data = new(hp, gold, relicTitle?.GetParsedText() ?? "", relicDesc?.Text ?? "",
				relicIcon, relicDesc?.CustomEffects);
			return intro =>
			{
				if (!StillOwnsPanel()) return;
				if (!intro.HasValue)
				{
					Restore(panel, saved);
					return;
				}
				Fx.After(anchor, intro.Value ? CharSelectScene.InfoPanelDelay : 0.1, Show);
			};

			bool StillOwnsPanel() => GodotObject.IsInstanceValid(anchor) && anchor.GetParent() == panel &&
				!anchor.IsQueuedForDeletion() && State.TryGetValue(panel.GetInstanceId(), out Saved? current) && current == saved;

			void Show()
			{
				if (!StillOwnsPanel()) return;
				try
				{
					if (NGame.Instance?.Transition.InTransition == true)
					{
						Restore(panel, saved);
						return;
					}
					CharSelectInfoPanel.Build(anchor, data);
				}
				catch (System.Exception)
				{
					Restore(panel, saved);                          // 搭新面板出错时恢复原版面板（不能留下一块空白）
					throw;
				}
			}
		}
		catch
		{
			Restore(panel, saved);
			throw;
		}
	}

	private static void Restore(Control panel, Saved? expected = null, bool removeAnchor = true)
	{
		if (!GodotObject.IsInstanceValid(panel)
			|| !State.TryGetValue(panel.GetInstanceId(), out Saved? saved)
			|| (expected != null && saved != expected))
		{
			return;
		}
		State.Remove(panel.GetInstanceId());                       // 先撤销所有权：RemoveChild 会同步触发 TreeExiting
		foreach ((CanvasItem item, Color c, bool self) in saved.Hidden)
		{
			if (!GodotObject.IsInstanceValid(item)) continue;
			if (self) item.SelfModulate = c;
			else item.Modulate = c;
		}
		if (removeAnchor && GodotObject.IsInstanceValid(saved.Anchor))
		{
			if (saved.Anchor.GetParent() == panel) panel.RemoveChild(saved.Anchor);
			saved.Anchor.QueueFree();
		}
	}
}
