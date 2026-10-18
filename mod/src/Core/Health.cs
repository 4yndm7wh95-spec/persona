using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace P3Makoto.Core;

/// <summary>
/// v0.14.60（玩家：玩储君时每次快到一层 Boss 就很卡，像被调低了倍速）：卡顿诊断日志，只写日志不改游戏。
/// · 每 60 秒一行：帧率、时间倍速、节点总数、孤儿节点数、根节点下的子节点数、本 mod 还活着的音效播放器数；
/// · 帧率连续 5 秒低于 30：额外列出根节点和战斗特效层里数量最多的节点类型（每 60 秒最多一次），用来找越积越多的东西。
/// </summary>
public static class Health
{
	private static ulong _next, _lowSince, _nextDetail;
	private static bool _hooked;

	public static void Install()
	{
		if (_hooked || Engine.GetMainLoop() is not SceneTree tree)
		{
			return;
		}
		_hooked = true;
		_next = Time.GetTicksMsec() + 60000;
		tree.ProcessFrame += () => ModEntry.Safe("health", () => Tick(tree));
	}

	private static void Tick(SceneTree tree)
	{
		ulong now = Time.GetTicksMsec();
		double fps = Engine.GetFramesPerSecond();
		if (now >= _next)
		{
			_next = now + 60000;
			GD.Print($"{ModEntry.Tag} health fps={fps:0} timescale={Engine.TimeScale:0.###} nodes={Performance.GetMonitor(Performance.Monitor.ObjectNodeCount):0} orphans={Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount):0} root_children={tree.Root.GetChildCount()} sfx_players={Audio.Sfx.Live}");
		}
		if (fps >= 30.0)
		{
			_lowSince = 0;
			return;
		}
		if (_lowSince == 0)
		{
			_lowSince = now;
			return;
		}
		if (now - _lowSince < 5000 || now < _nextDetail)
		{
			return;
		}
		_nextDetail = now + 60000;
		GD.Print($"{ModEntry.Tag} health LOW fps={fps:0} for {(now - _lowSince) / 1000}s timescale={Engine.TimeScale:0.###}; root: {Top(tree.Root)}");
		if (NCombatRoom.Instance is NCombatRoom room && GodotObject.IsInstanceValid(room))
		{
			GD.Print($"{ModEntry.Tag} health LOW combat vfx: front {room.CombatVfxContainer.GetChildCount()} [{Top(room.CombatVfxContainer)}], back {room.BackCombatVfxContainer.GetChildCount()} [{Top(room.BackCombatVfxContainer)}]");
		}
	}

	/// <summary>直接子节点按"类型:名字前缀"分组，数量最多的 6 组。</summary>
	private static string Top(Node parent)
	{
		Dictionary<string, int> counts = new();
		foreach (Node c in parent.GetChildren())
		{
			string name = c.Name.ToString();
			int at = name.IndexOf('@');
			string key = c.GetClass() + ":" + (at > 0 ? name[..at] : name.Length > 24 ? name[..24] : name);
			counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
		}
		return string.Join(", ", counts.OrderByDescending(kv => kv.Value).Take(6).Select(kv => $"{kv.Key}×{kv.Value}"));
	}
}
