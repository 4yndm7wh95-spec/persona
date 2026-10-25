using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;

namespace P3Makoto.Core;

/// <summary>只清理经过 P3 菜单/选牌演出的借用控件；不改原版选卡规则、禁用条件或鼠标穿透设置。</summary>
internal static class PooledCardInput
{
	private const string Tracked = "p3_input_cleanup";

	public static void Track(NGridCardHolder holder)
	{
		if (holder.HasMeta(Tracked)) return;
		holder.SetMeta(Tracked, true);
		void Release()
		{
			holder.TreeExiting -= Release;
			holder.RemoveMeta(Tracked);
			ModEntry.Safe("pooled card input cleanup", () =>
			{
				// 原版 OnReturnedFromPool 会恢复缩放/可点击，但没有清按下、悬停和内部焦点状态。
				// 离开当前界面时清理，而不是在奖励/升级界面强行将禁用牌设为可点击。
				var tweenField = AccessTools.Field(typeof(NCardHolder), "_hoverTween");
				if (tweenField?.GetValue(holder) is Tween tween && GodotObject.IsInstanceValid(tween)) tween.Kill();
				tweenField?.SetValue(holder, null);
				AccessTools.Field(typeof(NCardHolder), "_currentPressedAction")?.SetValue(holder, null);
				ClearFlags(holder, "_isHovered", "_isFocused");
				if (!holder.IsNodeReady()) return;
				var hitbox = holder.Hitbox;
				if (hitbox == null || !GodotObject.IsInstanceValid(hitbox)) return;
				ClearFlags(hitbox, "_isHovered", "_isControllerFocused", "_isPressed");
				AccessTools.Property(hitbox.GetType(), "IsFocused")?.SetValue(hitbox, false);
			});
		}
		holder.TreeExiting += Release;
	}

	private static void ClearFlags(object obj, params string[] names)
	{
		foreach (string name in names)
		{
			var field = AccessTools.Field(obj.GetType(), name);
			if (field?.FieldType == typeof(bool)) field.SetValue(obj, false);
		}
	}
}
