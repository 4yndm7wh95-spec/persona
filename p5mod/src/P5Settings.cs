using System;
using System.IO;
using System.Text.Json;
using Godot;

namespace P5Joker;

internal static class P5Settings
{
	internal enum Option { Reward, AllOut }
	private sealed record Preferences(bool Reward = true, bool AllOut = true);
	private sealed record SavedPreferences(bool? Reward = null, bool? AllOut = null, int Profile = 1, int Custom = 4)
	{
		// The previous build stored the effective reward option in a preset or bit mask.
		internal bool LegacyReward => Profile != 0 && (Profile != 2 || (Custom & (1 << 2)) != 0);
	}
	private static Preferences _preferences = new();
	private static string? _path;

	internal static bool Enabled(Option option) => option == Option.Reward ? _preferences.Reward : _preferences.AllOut;

	internal static void Load(string? path = null)
	{
		_path = path ?? ProjectSettings.GlobalizePath("user://p5joker_settings.json");
		_preferences = new();
		try
		{
			using FileStream reader = File.OpenRead(_path);
			if (JsonSerializer.Deserialize<SavedPreferences>(reader) is { } saved)
				_preferences = new Preferences(saved.Reward ?? saved.LegacyReward, saved.AllOut ?? true);
		}
		catch (FileNotFoundException) { }
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
		{
			Stage.Report("settings load", ex);
		}
	}

	internal static bool Set(Option option, bool on) => Save(option == Option.Reward
		? _preferences with { Reward = on } : _preferences with { AllOut = on });

	private static bool Save(Preferences preferences)
	{
		if (preferences == _preferences) return true;
		string path = _path ?? ProjectSettings.GlobalizePath("user://p5joker_settings.json");
		string temp = path + ".tmp";
		try
		{
			File.WriteAllText(temp, JsonSerializer.Serialize(preferences));
			File.Move(temp, path, overwrite: true);
			_preferences = preferences;
			return true;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Stage.Report("settings save", ex);
			return false;
		}
	}
}
