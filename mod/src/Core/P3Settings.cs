using System;
using System.IO;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;

namespace P3Makoto.Core;

/// <summary>
/// v0.14.46 演出开关（用户：有的玩家看腻了不想每次都看）：每项「完整 / 关闭」，关闭 = 不播这段演出、直接走原版流程。
/// 游戏常规设置里的结城理选项读写这里；存在 user://p3makoto_settings.json（%APPDATA%\SlayTheSpire2\）。
/// 游戏自带的快速模式开到「极速」时，这几项一律当作关闭。
/// </summary>
public static class P3Settings
{
	public sealed class Data
	{
		public bool AllOut { get; set; } = true;          // 总攻击（精英 / Boss 战胜利）
		public bool Victory { get; set; } = true;         // 战斗胜利大字
		public bool ShuffleTime { get; set; } = true;     // 选卡 Shuffle Time
		public bool ShuffleFull { get; set; } = false;    // v0.14.101 选卡节奏：false = 精简（默认：三张牌正面从中间展开、马上能选）；true = 完整（标题 → 洗牌发牌 → 翻牌）
		public bool CutIn { get; set; } = true;           // Persona 召唤切入（眼神特写）
		public bool SelectSummon { get; set; } = true;    // 选人时的召唤演出
		public bool Architect { get; set; } = true;       // 建筑师房间（关闭 = 全部原版）
		public bool PileScreen { get; set; } = true;      // 抽牌堆 / 弃牌堆界面（P3R 菜单风格）
		public bool TipList { get; set; } = true;         // v0.14.66 角色状态说明：P3R 滚动列表
		public bool SettingsSkin { get; set; } = true;    // v0.14.67 设置界面（P3R 主菜单风格）
		public bool CursorSkin { get; set; } = true;      // 鼠标光标：关闭恢复游戏原版，立即生效
		public bool DrawPileOrder { get; set; } = true;   // v0.14.98 抽牌堆按原版顺序排（SL 看抽牌顺序用）；false = 按类型分区
		public bool DeckPileOrder { get; set; } = false;
		public bool DiscardPileOrder { get; set; } = true;
		public bool ExhaustPileOrder { get; set; } = true;
	}

	public static int Mode
	{
		get
		{
			Data d = Current;
			bool[] flags = { d.AllOut, d.Victory, d.ShuffleTime, d.ShuffleFull, d.CutIn, d.SelectSummon, d.Architect, d.PileScreen, d.TipList, d.CursorSkin };
			bool all = true;
			foreach (bool flag in flags) all &= flag;
			bool simple = d.AllOut && !d.Victory && d.ShuffleTime && !d.ShuffleFull && !d.CutIn && d.SelectSummon && !d.Architect && !d.PileScreen && d.TipList && d.CursorSkin;
			return all ? 1 : simple ? 0 : 2;
		}
	}

	public static void ApplyMode(bool enabled)
	{
		Data d = Current;
		d.AllOut = d.ShuffleTime = d.SelectSummon = d.TipList = d.CursorSkin = true;
		d.Victory = d.ShuffleFull = d.CutIn = d.Architect = d.PileScreen = enabled;
		Save();
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
	public static bool ShuffleFull => Current.ShuffleFull;
	public static bool CutIn => Current.CutIn && !Instant;
	public static bool SelectSummon => Current.SelectSummon && !Instant;
	public static bool Architect => Current.Architect && !Instant;
	// 牌堆界面不是演出（不拖节奏），极速模式下也照常
	public static bool PileScreen => Current.PileScreen;
	public static bool TipList => Current.TipList;
	public static bool SettingsSkin => Current.SettingsSkin;
}
