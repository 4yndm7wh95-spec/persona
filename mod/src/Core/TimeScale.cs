using Godot;

namespace P3Makoto.Core;

/// <summary>
/// 全局时间缩放的唯一入口（v0.14.26，用户：看牌组时卡牌乱飞）。
/// 之前顿帧（EvokerShot.HitStop）记下"当前速度"再恢复，空间杀法时停（Vorpal.Freeze）直接写 0.02 / 1：
/// 顿帧在时停期间开始时记下的是 0.02，时停结束写回 1 后，顿帧又把速度"恢复"成 0.02 —— 游戏卡在慢动作里，
/// 牌组界面的卡牌动画几乎停住，看起来像乱飞 / 残影卡在半路。
/// 现在每个来源各记各的系数，速度 = 生效系数里最小的那个；全部结束一定回到 1。
/// </summary>
public static class TimeScale
{
	private static double _hit = 1.0, _freeze = 1.0;

	public static void HitStop(double scale) { _hit = scale; Apply(); }

	public static void Freeze(double scale) { _freeze = scale; Apply(); }

	/// <summary>保险：所有减速来源清零，回到 1 倍速（长演出把流程交还给游戏前调用）。</summary>
	public static void Reset() { _hit = 1.0; _freeze = 1.0; Apply(); }

	private static void Apply() => Engine.TimeScale = System.Math.Min(_hit, _freeze);
}
