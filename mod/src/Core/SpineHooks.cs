using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using P3Makoto.Vfx;

namespace P3Makoto.Core;

/// <summary>
/// 监听结城理 Spine 动画里埋的事件（见 tools/rig/build_rig.py 的 EVENTS），在动画的精确帧上触发特效。
/// 同时记录每个角色手上当前拿着什么，避免重复显现 / 消散。
/// </summary>
public static class SpineHooks
{
	public sealed class Hands
	{
		public bool SwordOut;
		public bool EvokerOut;
	}

	private static readonly Dictionary<ulong, Hands> State = new();

	public static Hands Of(NCreature node)
	{
		ulong id = node.GetInstanceId();
		if (!State.TryGetValue(id, out Hands? h))
		{
			h = new Hands();
			State[id] = h;
		}
		return h;
	}

	private static readonly HashSet<ulong> Hooked = new();

	public static void Ensure(NCreature? node)
	{
		MegaSprite? body = node?.Visuals.SpineBody;
		if (node == null || body == null || !Hooked.Add(node.GetInstanceId()))
		{
			return;
		}
		body.ConnectAnimationEvent(Callable.From<GodotObject, GodotObject, GodotObject, GodotObject>((_, _, _, ev) =>
			ModEntry.Safe("spine event", () => OnEvent(node, new MegaEvent(ev).GetData().GetEventName()))));
	}

	private static void OnEvent(NCreature node, string name)
	{
		if (!GodotObject.IsInstanceValid(node))
		{
			return;
		}
		Control? front = Makoto.FxLayer();
		if (front == null)
		{
			return;
		}
		Hands h = Of(node);
		switch (name)
		{
			case "weapon_draw":
				if (!h.SwordOut)
				{
					WeaponFx.SwordMaterialize(front, node);
				}
				h.SwordOut = true;
				break;
			case "weapon_windup":
				WeaponFx.TipGlint(front, node);
				WeaponFx.SlashTrail(front, node, Pal.Cyan);           // 从蓄力就开始记录轨迹，残影才完整
				break;
			case "weapon_slash":
				WeaponFx.BladeGhosts(front, node, Pal.Cyan);
				WeaponFx.StepDust(Makoto.FxLayer(back: true) ?? front, Makoto.Feet(node), Makoto.Facing(node));
				break;
			case "weapon_sheath":
				if (h.SwordOut)
				{
					WeaponFx.SwordDissolve(front, node);
				}
				h.SwordOut = false;
				break;
			case "evoker_draw":
				if (!h.EvokerOut)
				{
					WeaponFx.EvokerGlint(front, node);
				}
				h.EvokerOut = true;
				break;
			case "evoker_fire":
				CardFx.FireEvoker(node);
				break;
			case "evoker_sheath":
				if (h.EvokerOut)
				{
					WeaponFx.EvokerGlint(front, node);
				}
				h.EvokerOut = false;
				break;
		}
	}
}
