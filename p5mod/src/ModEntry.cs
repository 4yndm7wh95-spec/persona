using System;
using System.IO;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace P5Joker;

[ModInitializer("Init")]
public static class ModEntry
{
	public const string Tag = "[P5Joker]";
	public const string Version = "0.9.83";

	public static void Init()
	{
		if (!MountLocalContent()) return;
		GD.Print($"{Tag} init v{Version}");
		Callable.From(() => Stage.Safe("asset diagnostics", () =>
		{
			bool effectsPresent = ResourceLoader.Exists("res://p5joker/fx/curse_ghost.png");
			GD.Print($"{Tag} assets: curse_ghost={effectsPresent}; OS={OS.GetName()}; renderer={RenderingServer.GetCurrentRenderingMethod()}");
		})).CallDeferred();
		Stage.Report = (where, e) => GD.PrintErr($"{Tag} {where}: {e}");
		P5Settings.Load();
		Stage.Shake = s => NGame.Instance?.ScreenShake(s switch
		{
			<= 1 => ShakeStrength.VeryWeak,
			2 => ShakeStrength.Weak,
			3 => ShakeStrength.Medium,
			_ => ShakeStrength.Strong,
		}, ShakeDuration.Short);
		Stage.PlayEvent = (e, v) => MegaCrit.Sts2.Core.Commands.SfxCmd.Play(e, v);
		Stage.ShakeDir = (s, deg) => NGame.Instance?.ScreenShake(Strength(s), ShakeDuration.Short, deg);
		BulletTimePlay.Install();
		WraithPlay.Install();
		// 毒雾的常驻咒怨雾：Joker 还在战斗里、身上还有毒雾能力就一直留着（Vfx/FumesField.cs）
		FumesField.Alive = () =>
		{
			if (MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom.Instance is not { } room) return false;
			foreach (var n in room.CreatureNodes)
			{
				if (n.Entity is { IsPlayer: true, IsDead: false } c && c.Player is { } p && Joker.Is(p)
					&& c.GetPower<MegaCrit.Sts2.Core.Models.Powers.NoxiousFumesPower>() != null) return true;
			}
			return false;
		};
		// 逐个补丁类应用：一个失败（别的游戏版本里目标被改名）不影响其他
		Harmony harmony = new("brooks.p5joker");
		int ok = 0, failed = 0;
		foreach (Type t in AccessTools.GetTypesFromAssembly(typeof(ModEntry).Assembly))
		{
			try
			{
				if (t.GetCustomAttributes(typeof(HarmonyPatch), true).Length == 0)
				{
					continue;
				}
				harmony.CreateClassProcessor(t).Patch();
				ok++;
			}
			catch (Exception e)
			{
				failed++;
				GD.PrintErr($"{Tag} patch {t.Name} failed: {e}");
			}
		}
		GD.Print($"{Tag} harmony patches applied: {ok} ok, {failed} failed");
		WarmupGame.Install();
	}

	// The loader normally mounts PCKs before calling Init. The manifest opts out
	// so a Workshop installation cannot replace even a vanilla texture.
	private static bool MountLocalContent()
	{
		string? directory = Path.GetDirectoryName(typeof(ModEntry).Assembly.Location);
		var candidates = ModManager.Mods.Where(m => m.manifest?.id == "P5Joker").ToArray();
		var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
		var source = candidates.FirstOrDefault(m => string.Equals(
			Path.GetFullPath(m.path), directory, comparison));
		if (source == null && candidates.Length == 1) source = candidates[0];
		if (source?.modSource == ModSource.SteamWorkshop || source?.workshopId > 0
			|| (source == null && directory?.Replace('\\', '/').Contains(
				"/steamapps/workshop/content/2868840/", StringComparison.OrdinalIgnoreCase) == true))
		{
			GD.Print($"{Tag} Workshop distribution is not enabled.");
			return false;
		}
		if (source?.manifest is { } manifest && (manifest.author != "B站川川errr 抖音川川" || manifest.description != "MOD唯一作者：B站川川errr 抖音川川。如果您在B站、抖音、玩家动力之外的渠道获取本MOD，均为盗版！"))
		{
			GD.PrintErr($"{Tag} The author or distribution notice differs from the release manifest.");
			return false;
		}
		string? root = source?.path ?? directory;
		if (string.IsNullOrEmpty(root)) return false;
		string pack = Path.Combine(root, "P5Joker.pck");
		if (!File.Exists(pack))
		{
			GD.PrintErr($"{Tag} Missing local resource pack: {pack}");
			return false;
		}
		// Managers scan manifests before initializers. Advertise only this validated
		// local installation on its next launch; the distributed manifest stays opt-out.
		if (source?.manifest != null && ModManager.Mods.Any(m =>
			m.manifest?.id == "Gurio.SkinChanger"))
			EnableLocalPackDiscovery(root);
		if (ResourceLoader.Exists("res://p5joker/fx/curse_ghost.png")) return true;
		if (ProjectSettings.LoadResourcePack(pack)) return true;
		GD.PrintErr($"{Tag} Could not mount local resource pack: {pack}");
		return false;
	}

	private static void EnableLocalPackDiscovery(string root)
	{
		string path = Path.Combine(root, "P5Joker.json");
		string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
		try
		{
			var doc = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path));
			if (doc?["id"]?.GetValue<string>() != "P5Joker" || doc["has_pck"]?.GetValue<bool>() != false) return;
			doc["has_pck"] = true;
			File.WriteAllText(temp, doc.ToJsonString(new System.Text.Json.JsonSerializerOptions {
				WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
			}));
			File.Move(temp, path, true);
			GD.Print($"{Tag} Local skin pack discovery enabled. Restart the game once to refresh the manager catalog.");
		}
		catch (Exception e) { GD.PrintErr($"{Tag} Could not enable local pack discovery at {path}: {e.Message}"); }
		finally { try { File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
	}

	private static ShakeStrength Strength(int s) => s switch
	{
		<= 1 => ShakeStrength.VeryWeak,
		2 => ShakeStrength.Weak,
		3 => ShakeStrength.Medium,
		_ => ShakeStrength.Strong,
	};

	/// <summary>特效出错只记日志，绝不打断游戏流程。</summary>
	public static void Safe(string where, Action action) => Stage.Safe(where, action);
}
