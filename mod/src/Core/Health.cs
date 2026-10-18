using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace P3Makoto.Core;

/// <summary>
/// v0.14.60（玩家：玩储君时每次快到一层 Boss 就很卡，像被调低了倍速）：卡顿诊断日志，只写日志不改游戏。
/// · 每 60 秒一行：帧率、时间倍速、节点总数、孤儿节点数、根节点下的子节点数、本 mod 还活着的音效播放器数；
/// · 帧率连续 1.5 秒低于 30（v0.14.64 起；最多 20 秒一次）：额外列出根节点和战斗特效层里数量最多的节点类型（每 60 秒最多一次），用来找越积越多的东西。
/// · v0.14.71（玩家：笔记本 3050，防御多段 / 放大剑 / 结算选卡时掉帧，本 mod 的 CPU 耗时只有 0.2ms/帧）：打开主视口的渲染计时，
///   每分钟一行写显卡 / 渲染 CPU 的平均和最高毫秒；单帧超过 50ms（卡一下）时写一行 hitch（显卡耗时、本 mod 当帧耗时、特效节点数），每分钟最多 20 行
///   —— 用来分清卡在显卡还是 CPU、是不是我们的特效。
/// </summary>
public static class Health
{
	private static ulong _next, _lowSince, _nextDetail, _nextWindow;
	private static bool _hooked;
	private static Rid _vp;
	private static ulong _lastUsec, _hitchMinute;
	private static int _hitches;
	private static double _gpuSum, _gpuMax, _cpuSum, _cpuMax;
	private static int _gpuN;

	public static void Install()
	{
		if (_hooked || Engine.GetMainLoop() is not SceneTree tree)
		{
			return;
		}
		_hooked = true;
		_next = Time.GetTicksMsec() + 60000;
		_vp = tree.Root.GetViewportRid();
		RenderingServer.ViewportSetMeasureRenderTime(_vp, true);
		tree.ProcessFrame += () => ModEntry.Safe("health", () => Tick(tree));
	}

	private static void Tick(SceneTree tree)
	{
		ulong now = Time.GetTicksMsec();
		double fps = Engine.GetFramesPerSecond();
		bool roll = now >= _nextWindow;
		if (roll)
		{
			_nextWindow = now + 2000;
		}
		double frameP3 = Prof.LastFrameMs;
		Prof.Frame(roll);
		double gpu = RenderingServer.ViewportGetMeasuredRenderTimeGpu(_vp), cpu = RenderingServer.ViewportGetMeasuredRenderTimeCpu(_vp);
		_gpuSum += gpu;
		_cpuSum += cpu;
		_gpuMax = System.Math.Max(_gpuMax, gpu);
		_cpuMax = System.Math.Max(_cpuMax, cpu);
		_gpuN++;
		ulong usec = Time.GetTicksUsec();
		double dtMs = _lastUsec == 0 ? 0 : (usec - _lastUsec) / 1000.0;
		_lastUsec = usec;
		if (dtMs > 50.0)
		{
			ulong minute = now / 60000;
			if (minute != _hitchMinute)
			{
				_hitchMinute = minute;
				_hitches = 0;
			}
			if (++_hitches <= 20)
			{
				int vfx = NCombatRoom.Instance is NCombatRoom r && GodotObject.IsInstanceValid(r) ? r.CombatVfxContainer.GetChildCount() + r.BackCombatVfxContainer.GetChildCount() : -1;
				GD.Print($"{ModEntry.Tag} health hitch {dtMs:0}ms gpu={gpu:0.0}ms render_cpu={cpu:0.0}ms p3={frameP3:0.0}ms process={Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000.0:0.0}ms vfx_nodes={vfx} fps={fps:0}");
			}
		}
		if (now >= _next)
		{
			_next = now + 60000;
			GD.Print($"{ModEntry.Tag} health fps={fps:0} timescale={Engine.TimeScale:0.###} nodes={Performance.GetMonitor(Performance.Monitor.ObjectNodeCount):0} orphans={Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount):0} root_children={tree.Root.GetChildCount()} sfx_players={Audio.Sfx.Live}");
			GD.Print($"{ModEntry.Tag} health gpu_ms avg={_gpuSum / System.Math.Max(_gpuN, 1):0.0} max={_gpuMax:0.0} render_cpu_ms avg={_cpuSum / System.Math.Max(_gpuN, 1):0.0} max={_cpuMax:0.0} quality={Vfx.Fx.Quality:0.##}");
			_gpuSum = _gpuMax = _cpuSum = _cpuMax = 0;
			_gpuN = 0;
			GD.Print($"{ModEntry.Tag} health {Prof.MinuteReport(reset: true)}");
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
		if (now - _lowSince < 1500 || now < _nextDetail)
		{
			return;
		}
		_nextDetail = now + 20000;
		GD.Print($"{ModEntry.Tag} health LOW fps={fps:0} for {(now - _lowSince) / 1000}s timescale={Engine.TimeScale:0.###}; root: {Top(tree.Root)}");
		GD.Print($"{ModEntry.Tag} health LOW last 2s: {Prof.WindowReport()}; frame_ms={1000.0 / System.Math.Max(fps, 1.0):0.0} gpu_ms={gpu:0.0} render_cpu_ms={cpu:0.0} process_ms={Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000.0:0.0} draw_calls={Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame):0} objects={Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame):0}");
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
