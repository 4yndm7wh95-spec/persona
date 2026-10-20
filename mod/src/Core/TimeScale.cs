using Godot;

namespace P3Makoto.Core;

/// <summary>
/// 全局时间缩放的唯一入口（v0.14.26，用户：看牌组时卡牌乱飞）。
/// 之前顿帧（EvokerShot.HitStop）记下"当前速度"再恢复，空间杀法时停（Vorpal.Freeze）直接写 0.02 / 1：
/// 顿帧在时停期间开始时记下的是 0.02，时停结束写回 1 后，顿帧又把速度"恢复"成 0.02 —— 游戏卡在慢动作里，
/// 牌组界面的卡牌动画几乎停住，看起来像乱飞 / 残影卡在半路。
/// 现在每个来源各记各的系数，速度 = 生效系数里最小的那个；全部结束一定回到 1。
///
/// v0.14.51（玩家：装了之后经常"所有东西都变慢、像子弹时间"，回主菜单也不恢复；开了游戏的去除动画就没事）：
/// 之前恢复只靠各处的"N 秒后写回 1"定时器，那一次回调没执行到（例如特效是在后台线程触发的、定时器没建成），速度就永远卡住。现在：
/// · 每个减速都带一个真实时间的截止点，看门狗每帧检查，过期没恢复就自己恢复（并写日志）；
/// · 我们没有任何减速、速度却一直不是 1 超过 2.5 秒（原版自己的顿帧最长不到 1 秒），也拉回 1；
/// · 写 Engine.TimeScale 一律在主线程（不在主线程就推迟到主线程）。
/// 2026-10-20：顿帧按实际时长直接到期；状态与截止点也只在主线程修改。
/// 玩家日志中已结束的 0.08 仍反复残留，针对该数值缩短恢复等待；原版从 0.1 开始的顿帧不受此快速恢复影响。
/// </summary>
public static class TimeScale
{
	private static double _hit = 1.0, _freeze = 1.0;
	private static ulong _hitUntil, _freezeUntil, _offSince;
	private static bool _hooked;
	private static bool _hasHitStop;
	private static ulong _staleHitSince;

	private static ulong Now => Time.GetTicksMsec();

	/// <summary>顿帧系数；scale &lt; 1 时最多保持 maxSeconds（真实时间），过期由看门狗恢复。</summary>
	public static void HitStop(double scale, double maxSeconds = 0.6)
	{
		ulong until = scale < 1.0 ? Now + (ulong)(maxSeconds * 1000.0) : 0;
		OnMain(() => SetHitStop(scale, until));
	}

	private static void SetHitStop(double scale, ulong until)
	{
		// A queued request keeps its original real-time deadline, even if the main thread was busy.
		if (scale < 1.0 && Now >= until) return;
		_hasHitStop |= scale < 1.0;
		_hit = scale;
		_hitUntil = until;
		Apply();
	}

	/// <summary>时停系数；scale &lt; 1 时最多保持 maxSeconds（真实时间）。</summary>
	public static void Freeze(double scale, double maxSeconds = 3.0)
	{
		ulong until = scale < 1.0 ? Now + (ulong)(maxSeconds * 1000.0) : 0;
		OnMain(() =>
		{
			if (scale < 1.0 && Now >= until) return;
			_freeze = scale;
			_freezeUntil = until;
			Apply();
		});
	}

	/// <summary>保险：所有减速来源清零，回到 1 倍速（长演出把流程交还给游戏前调用）。</summary>
	public static void Reset()
	{
		OnMain(ResetOnMain);
	}

	private static void ResetOnMain()
	{
		_hit = 1.0;
		_freeze = 1.0;
		_hitUntil = _freezeUntil = 0;
		_offSince = _staleHitSince = 0;
		Apply();
	}

	/// <summary>mod 初始化时挂上每帧的看门狗。</summary>
	public static void Install()
	{
		if (_hooked || Engine.GetMainLoop() is not SceneTree tree)
		{
			return;
		}
		_hooked = true;
		tree.ProcessFrame += () => ModEntry.Safe("timescale watchdog", Watch);
	}

	private static void Watch()
	{
		ulong now = Now;
		bool changed = false;
		if (_hit < 1.0 && now >= _hitUntil)
		{
			// Normal hit-stop completion uses the frame watchdog, not a second timer.
			_hit = 1.0;
			changed = true;
		}
		if (_freeze < 1.0 && now > _freezeUntil)
		{
			GD.Print($"{ModEntry.Tag} timescale: freeze {_freeze:0.##} was never released -> back to 1");
			_freeze = 1.0;
			changed = true;
		}
		if (changed)
		{
			Apply();
		}
		// Player log: 125 FPS, but an already released 0.08 was present again (27 recoveries).
		// Vanilla hit stop bottoms out at 0.1. Only repair our exact 0.08 signature,
		// after a hit stop has actually run, and never during a valid hit/freeze lease.
		bool staleHit = _hasHitStop && _hit >= 1.0 && _freeze >= 1.0
			&& System.Math.Abs(Engine.TimeScale - 0.08) < 0.000001;
		if (staleHit)
		{
			if (_staleHitSince == 0) _staleHitSince = now;
			else if (now - _staleHitSince >= 100)
			{
				GD.Print($"{ModEntry.Tag} timescale: stale hit stop 0.08 with no active lease for 100ms -> back to 1");
				Engine.TimeScale = 1.0;
				_staleHitSince = _offSince = 0;
			}
		}
		else _staleHitSince = 0;
		// 其他未知残留沿用 2.5 秒兜底；原版最长 Forever 顿帧为 2 秒。
		if (_hit >= 1.0 && _freeze >= 1.0 && Engine.TimeScale < 0.999)
		{
			if (_offSince == 0)
			{
				_offSince = now;
			}
			else if (now - _offSince > 2500)
			{
				GD.Print($"{ModEntry.Tag} timescale: stuck at {Engine.TimeScale:0.###} for 2.5s with no P3Makoto slow-down active -> back to 1");
				Engine.TimeScale = 1.0;
				_offSince = 0;
			}
		}
		else
		{
			_offSince = 0;
		}
	}

	private static void Apply()
	{
		Engine.TimeScale = System.Math.Min(_hit, _freeze);
	}

	private static void OnMain(System.Action action)
	{
		if (OS.GetThreadCallerId() != OS.GetMainThreadId())
		{
			Callable.From(action).CallDeferred();
			return;
		}
		action();
	}
}
