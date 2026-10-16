using System;
using System.IO;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;

namespace P3Makoto.Core;

/// <summary>
/// v0.14.46 演出开关（用户：有的玩家看腻了不想每次都看）：每项「完整 / 关闭」，关闭 = 不播这段演出、直接走原版流程。
/// 设置界面里的 P3MAKOTO 一栏（Patches/SettingsPatches.cs）读写这里；存在 user://p3makoto_settings.json（%APPDATA%\SlayTheSpire2\）。
/// 游戏自带的快速模式开到「极速」时，这几项一律当作关闭。
/// </summary>
public static class P3Settings
{
	public sealed class Data
	{
		public bool AllOut { get; set; } = true;          // 总攻击（精英 / Boss 战胜利）
		public bool Victory { get; set; } = true;         // 战斗胜利大字
		public bool ShuffleTime { get; set; } = true;     // 选卡 Shuffle Time
		public bool CutIn { get; set; } = true;           // Persona 召唤切入（眼神特写）
		public bool SelectSummon { get; set; } = true;    // 选人时的召唤演出
		public bool Architect { get; set; } = true;       // 建筑师房间（关闭 = 全部原版）
	}

	private static Data? _data;

	private static string Path => ProjectSettings.GlobalizePath("user://p3makoto_settings.json");

	public static Data Current
	{
		get
		{
			if (_data != null)
			{
				return _data;
			}
			try
			{
				_data = File.Exists(Path) ? JsonSerializer.Deserialize<Data>(File.ReadAllText(Path)) : null;
			}
			catch (Exception e)
			{
				ModEntry.Warn($"settings read failed, using defaults: {e.Message}");
			}
			return _data ??= new Data();
		}
	}

	public static void Save()
	{
		try
		{
			File.WriteAllText(Path, JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
		}
		catch (Exception e)
		{
			ModEntry.Warn($"settings save failed: {e.Message}");
		}
	}

	/// <summary>游戏的快速模式是「极速」：所有演出都不播。</summary>
	public static bool Instant
	{
		get
		{
			try
			{
				return SaveManager.Instance?.PrefsSave?.FastMode == FastModeType.Instant;
			}
			catch (Exception)
			{
				return false;
			}
		}
	}

	public static bool AllOut => Current.AllOut && !Instant;
	public static bool Victory => Current.Victory && !Instant;
	public static bool ShuffleTime => Current.ShuffleTime && !Instant;
	public static bool CutIn => Current.CutIn && !Instant;
	public static bool SelectSummon => Current.SelectSummon && !Instant;
	public static bool Architect => Current.Architect && !Instant;
}
