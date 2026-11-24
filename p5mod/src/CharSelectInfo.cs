using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;

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
		public Color OriginalModulate;
		public Node2D Anchor = null!;
	}

	private static readonly Dictionary<ulong, Saved> State = new();

	internal static void Clear(Control screen)
	{
		if (Traverse.Create(screen).Field("_infoPanel").GetValue<Control>() is Control panel)
			Restore(panel);
	}

	// Hide the stock panel as soon as Joker is selected. Reveal our panel after
	// the backdrop is ready, or restore the stock panel if the backdrop fails.
	internal static Action<bool?>? Prepare(Control screen, bool waitForTransition = true)
	{
		Control? panel = Traverse.Create(screen).Field("_infoPanel").GetValue<Control>();
		if (panel == null || !GodotObject.IsInstanceValid(panel))
		{
			return null;
		}
		if (panel.GetParent() is not Node parent || panel.GetTree() is not SceneTree tree)
			return null;

		Restore(panel);
		Node2D anchor = new() { Name = DecoName };
		Saved saved = new() { Anchor = anchor, OriginalModulate = panel.Modulate };
		State[panel.GetInstanceId()] = saved;
		try
		{
			parent.AddChild(anchor);
			anchor.TreeExiting += Detach;
			panel.TreeExiting += PanelExiting;
			parent.MoveChild(anchor, panel.GetIndex() + 1);
			tree.ProcessFrame += Align;
			Align();
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

			bool StillOwnsPanel() => GodotObject.IsInstanceValid(panel) && Fx.Active(anchor) &&
				anchor.GetParent() == parent && State.TryGetValue(panel.GetInstanceId(), out Saved? current) && current == saved;

			void Align()
			{
				if (!StillOwnsPanel()) return;
				anchor.Transform = panel.GetTransform();
				anchor.Visible = panel.Visible;
				if (panel.Modulate.A != 0f) panel.Modulate = new Color(panel.Modulate, 0f);
			}

			void PanelExiting() => Restore(panel, saved);
			void Detach()
			{
				tree.ProcessFrame -= Align;
				if (GodotObject.IsInstanceValid(panel)) panel.TreeExiting -= PanelExiting;
				Restore(panel, saved, removeAnchor: false);
			}

			void Show()
			{
				if (!StillOwnsPanel()) return;
				try
				{
				if (waitForTransition && NGame.Instance?.Transition.InTransition == true)
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
		panel.Modulate = saved.OriginalModulate;
		if (removeAnchor && GodotObject.IsInstanceValid(saved.Anchor))
		{
			saved.Anchor.Hide();
			saved.Anchor.GetParent()?.RemoveChild(saved.Anchor);
			saved.Anchor.QueueFree();
		}
	}
}
