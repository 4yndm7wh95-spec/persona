using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 生物身体（Visuals）被我们推一下、抖一下时的「原位」。
/// 以前每个击退都在开始时读 body.Position 当原位：两次击退重叠（多人、倍速 mod 下很常见）时，
/// 后一次读到的是被推歪的位置，回弹回去的也是歪的位置——一次次累积，敌人模型就被推出屏幕，血条却留在原地
/// （玩家 2026-11-26 实机：4 人局 + 10 倍速，毒性爆发后怪被挤到屏幕外）。
/// 这里只在「没有任何特效在推它」时记一次原位，所有推动都回到这个位置；新的击退会接管旧的。
/// </summary>
internal static class BodyRest
{
	private sealed class Entry
	{
		public Vector2 Pos;
		public int Holds;
		public Tween? Knock;
	}

	private static readonly Dictionary<ulong, Entry> Map = new();

	/// <summary>开始推这个身体：返回原位。用完必须 <see cref="Release"/>。</summary>
	public static Vector2 Hold(Node2D body)
	{
		ulong id = body.GetInstanceId();
		if (!Map.TryGetValue(id, out Entry? e))
		{
			if (Map.Count > 64) Prune();
			e = new Entry { Pos = body.Position };
			Map[id] = e;
		}
		e.Holds++;
		return e.Pos;
	}

	/// <summary>推完了：最后一个放手时把身体放回原位。</summary>
	public static void Release(Node2D body)
	{
		ulong id = body.GetInstanceId();
		if (!Map.TryGetValue(id, out Entry? e) || --e.Holds > 0) return;
		Map.Remove(id);
		if (GodotObject.IsInstanceValid(body)) body.Position = e.Pos;
	}

	/// <summary>
	/// 一次击退：停掉这个身体上还没回弹完的上一次击退，返回新的补间和原位。
	/// 补间最后会自己放回原位（调用方只管往里加推出去、弹回 home 的步骤）。
	/// </summary>
	public static Tween Knock(Node2D body, out Vector2 home)
	{
		home = Hold(body);
		Entry e = Map[body.GetInstanceId()];
		if (e.Knock != null && e.Knock.IsValid())
		{
			e.Knock.Kill();
			e.Holds--;                                                // 被接管的那次不会走到自己的 Release
		}
		Tween tw = body.CreateTween();
		e.Knock = tw;
		return tw;
	}

	/// <summary>击退补间的最后一步：放手。</summary>
	public static void EndKnock(Tween tw, Node2D body) =>
		tw.TweenCallback(Callable.From(() =>
		{
			if (Map.TryGetValue(body.GetInstanceId(), out Entry? e) && e.Knock == tw) e.Knock = null;
			Release(body);
		}));

	/// <summary>场景提前结束：立刻停掉这次击退并放回原位（只在它还是当前那次时）。</summary>
	public static void Abort(Tween tw, Node2D body)
	{
		if (!Map.TryGetValue(body.GetInstanceId(), out Entry? e) || e.Knock != tw) return;
		if (tw.IsValid()) tw.Kill();
		e.Knock = null;
		Release(body);
	}

	private static void Prune()
	{
		List<ulong>? dead = null;
		foreach (ulong id in Map.Keys)
			if (GodotObject.InstanceFromId(id) == null) (dead ??= new()).Add(id);
		if (dead != null) foreach (ulong id in dead) Map.Remove(id);
	}
}
