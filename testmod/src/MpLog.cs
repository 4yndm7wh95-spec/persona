using System;
using System.IO;
using System.Linq;
using System.Text;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace P3TestMod;

/// <summary>
/// 多人日志（v1.2）：每个游戏进程单独写一个文件，同一台电脑开两个游戏也不会互相覆盖（原版 godot.log 两个进程会抢同一个文件）。
/// 位置：%APPDATA%\SlayTheSpire2\logs\p3_mp\p3mp_日期_时间_pid进程号.log（面板上"打开日志文件夹"），只保留最近 30 个。
/// 内容：
/// · 原版自己的全部日志（Log.LogCallback：联机握手、动作队列、校验和、状态分歧报错等）；联机时把 Actions / GameSync 两类调到 Debug，动作同步的细节也记下；
/// · 本工具的事件（EVT）：联机身份变化、进房间、战斗开始 / 结束、测试命令的发送和执行；
/// · 状态快照（SNAP）：每回合开始、战斗结束时所有生物的血量 / 格挡 / 能力和每个玩家的能量 / 辉星 / 牌堆张数。
///   两个玩家的日志里同一回合的 SNAP 应该一模一样 —— 不一样的那一行就是开始不同步的地方；
/// · 未捕获的异常（EXC）。
/// 每行立即写盘，游戏崩溃也不会丢。
/// </summary>
public static class MpLog
{
	private static readonly object Lock = new();
	private static StreamWriter? _writer;
	private static bool _hooked;
	private static NetGameType _lastType = NetGameType.None;
	private static ulong _lastCheck;

	public static string Dir => ProjectSettings.GlobalizePath("user://logs/p3_mp");

	public static string? FilePath { get; private set; }

	public static void Start()
	{
		try
		{
			Directory.CreateDirectory(Dir);
			foreach (FileInfo old in new DirectoryInfo(Dir).GetFiles("p3mp_*.log").OrderByDescending(f => f.CreationTimeUtc).Skip(29))
			{
				try { old.Delete(); } catch { }
			}
			FilePath = Path.Combine(Dir, $"p3mp_{DateTime.Now:yyyyMMdd_HHmmss}_pid{System.Environment.ProcessId}.log");
			_writer = new StreamWriter(new FileStream(FilePath, FileMode.Create, System.IO.FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false)) { AutoFlush = true };
			Write("EVT", $"P3TestMod {TestEntry.Version} log start, pid {System.Environment.ProcessId}, args: {string.Join(" ", OS.GetCmdlineArgs())}");
			GD.Print($"{TestEntry.Tag} multiplayer log -> {FilePath}");
		}
		catch (Exception e)
		{
			GD.PrintErr($"{TestEntry.Tag} mp log start failed: {e}");
		}
	}

	/// <summary>挂上原版日志回调和游戏事件（第一帧调用，此时各单例都已创建）。</summary>
	public static void Hook()
	{
		if (_hooked)
		{
			return;
		}
		_hooked = true;
		Log.LogCallback += (level, text, _) => Write(level.ToString().ToUpperInvariant(), text);
		AppDomain.CurrentDomain.UnhandledException += (_, e) => Write("EXC", $"unhandled: {e.ExceptionObject}");
		System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) => Write("EXC", $"unobserved task: {e.Exception}");
		RunManager.Instance.RoomEntered += () => TestEntry.Safe("mplog room", RoomEntered);
		CombatManager.Instance.CombatSetUp += s => TestEntry.Safe("mplog setup", () => Snapshot("combat set up", s));
		CombatManager.Instance.TurnStarted += s => TestEntry.Safe("mplog turn", () => Snapshot($"turn start (round {s.RoundNumber}, side {s.CurrentSide})", s));
		CombatManager.Instance.CombatEnded += r => TestEntry.Safe("mplog end", () => Event($"combat ended: room {r.RoomType} {r.ModelId.Entry}"));
	}

	public static void Event(string text) => Write("EVT", text);

	/// <summary>每帧：联机身份（单人 / 房主 / 客机）变化时记一笔，并调整原版的日志等级。</summary>
	public static void Tick()
	{
		ulong now = Time.GetTicksMsec();
		if (now - _lastCheck < 500)
		{
			return;
		}
		_lastCheck = now;
		INetGameService? net = RunManager.Instance.NetService;
		NetGameType type = net?.Type ?? NetGameType.None;
		if (type == _lastType)
		{
			return;
		}
		Event($"net role {_lastType} -> {type}, local netId {net?.NetId}, connected {net?.IsConnected}, game {NGame.GetGameVersion()}");
		bool mp = type is NetGameType.Host or NetGameType.Client;
		MegaCrit.Sts2.Core.Logging.Logger.SetLogLevelForType(LogType.Actions, mp ? LogLevel.Debug : LogLevel.Info);
		MegaCrit.Sts2.Core.Logging.Logger.SetLogLevelForType(LogType.GameSync, mp ? LogLevel.Debug : LogLevel.Info);
		if (mp)
		{
			Event($"mods gameplay: [{string.Join(", ", ModManager.GetGameplayRelevantModNameList() ?? new())}] other: [{string.Join(", ", ModManager.GetNonGameplayRelevantModNameList() ?? new())}]");
			if (RunManager.Instance.IsInProgress && RunManager.Instance.DebugOnlyGetState() is RunState run)
			{
				Event("players: " + string.Join(", ", run.Players.Select(p => $"{p.NetId}={p.Character.Id.Entry}")));
			}
		}
		_lastType = type;
	}

	private static void RoomEntered()
	{
		if (RunManager.Instance.DebugOnlyGetState() is not RunState run)
		{
			return;
		}
		AbstractRoom? room = run.CurrentRoom;
		Event($"room entered: {room?.RoomType} {room?.ModelId?.Entry} floor {run.TotalFloor} act {run.CurrentActIndex}");
	}

	public static void Snapshot(string when, CombatState s)
	{
		StringBuilder sb = new();
		sb.Append(when).Append(" | ");
		foreach (Creature c in s.Creatures)
		{
			string who = c.Player != null ? $"P{c.Player.NetId}" : c.Monster?.Id.Entry ?? c.Name;
			string powers = string.Join(",", c.Powers.Select(p => $"{p.Id.Entry}:{p.Amount}"));
			sb.Append($"{who} hp {c.CurrentHp}/{c.MaxHp} blk {c.Block} [{powers}]");
			if (c.Player?.PlayerCombatState is { } pcs)
			{
				sb.Append($" en {pcs.Energy} st {pcs.Stars} hand {pcs.Hand.Cards.Count} draw {pcs.DrawPile.Cards.Count} disc {pcs.DiscardPile.Cards.Count}");
			}
			sb.Append(" | ");
		}
		Write("SNAP", sb.ToString());
	}

	private static void Write(string tag, string text)
	{
		lock (Lock)
		{
			try
			{
				_writer?.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [{tag}] {text}");
			}
			catch
			{
			}
		}
	}
}
