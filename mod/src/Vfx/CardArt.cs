using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Models;
using P3Makoto.Core;

namespace P3Makoto.Vfx;

/// <summary>
/// 0.15.31: the 94 approved P3R card illustrations (art/cards/final, exported by tools/pck/export_card_art.py
/// to p3makoto/card_art/&lt;card&gt;.jpg at the vanilla portrait size). Only the portrait texture changes:
/// frame, banner, fonts, text and layout stay vanilla. Card ids are matched by name, so any card without a
/// file keeps its vanilla art. Off when the setting is off, the pack is missing, or another Regent skin owns
/// the rig. Textures load on first use and stay for the session; while a run is open the deck's cards are
/// decoded on a worker thread and uploaded one per frame so the first draw does not stall.
/// </summary>
internal static class CardArt
{
	private const string Dir = "res://p3makoto/card_art/";
	private static HashSet<string>? names;
	private static readonly Dictionary<string, Texture2D> Textures = new(StringComparer.Ordinal);
	private static readonly ConcurrentDictionary<string, Image> Decoded = new(StringComparer.Ordinal);
	private static readonly ConcurrentDictionary<string, byte> Queued = new(StringComparer.Ordinal);
	private static SceneTree? tree;
	private static ulong nextDeckCheck;

	private static bool Enabled => P3Settings.Current.CardArt && AssetGuard.PackLoaded && Makoto.AssetsOurs;

	private static HashSet<string> Names
	{
		get
		{
			if (names != null) return names;
			names = new HashSet<string>(StringComparer.Ordinal);
			// index.txt is written by the export script; listing a directory inside a pck is not relied on.
			if (FileAccess.FileExists(Dir + "index.txt"))
				foreach (string line in FileAccess.GetFileAsString(Dir + "index.txt").Split('\n'))
					if (line.Trim() is { Length: > 0 } id && FileAccess.FileExists(Dir + id + ".jpg")) names.Add(id);
			GD.Print($"{ModEntry.Tag} card art: {names.Count} illustrations in pck");
			return names;
		}
	}

	/// <summary>The P3R illustration for this card, or null to keep the vanilla portrait.</summary>
	internal static Texture2D? For(CardModel card)
	{
		if (!Enabled) return null;
		string id = card.Id.Entry.ToLowerInvariant();
		return Names.Contains(id) ? Get(id) : null;
	}

	private static Texture2D? Get(string id)
	{
		if (Textures.TryGetValue(id, out Texture2D? tex)) return tex;
		Image? image = Decoded.TryRemove(id, out Image? ready) ? ready : Decode(id);
		if (image == null) return null;
		tex = ImageTexture.CreateFromImage(image);
		image.Dispose();
		Textures[id] = tex;
		return tex;
	}

	private static Image? Decode(string id)
	{
		string path = Dir + id + ".jpg";
		if (!FileAccess.FileExists(path)) return null;
		Image image = new();
		if (image.LoadJpgFromBuffer(FileAccess.GetFileAsBytes(path)) == Error.Ok && !image.IsEmpty()) return image;
		image.Dispose();
		GD.PrintErr($"{ModEntry.Tag} card art {id}.jpg could not be decoded");
		return null;
	}

	internal static void Watch()
	{
		if (tree != null || Engine.GetMainLoop() is not SceneTree t) return;
		tree = t;
		t.ProcessFrame += () => ModEntry.Safe("card art prefetch", Tick);
	}

	private static void Tick()
	{
		// One upload per frame (~1 ms) for whatever the worker has finished.
		foreach (KeyValuePair<string, Image> pair in Decoded)
		{
			if (Textures.ContainsKey(pair.Key)) { if (Decoded.TryRemove(pair.Key, out Image? dup)) dup.Dispose(); }
			else Get(pair.Key);
			break;
		}
		ulong now = Time.GetTicksMsec();
		if (now < nextDeckCheck) return;
		nextDeckCheck = now + 2000;
		if (!Enabled || !Patches.Stage.MakotoRun) return;
		var players = MegaCrit.Sts2.Core.Runs.RunManager.Instance?.DebugOnlyGetState()?.Players;
		if (players == null || MegaCrit.Sts2.Core.Context.LocalContext.GetMe(players) is not { } me) return;
		List<string>? todo = null;
		foreach (CardModel card in me.Deck.Cards)
		{
			string id = card.Id.Entry.ToLowerInvariant();
			if (!Names.Contains(id) || Textures.ContainsKey(id) || !Queued.TryAdd(id, 0)) continue;
			(todo ??= new()).Add(id);
		}
		if (todo == null) return;
		Task.Run(() =>
		{
			foreach (string id in todo)
				if (Decode(id) is Image image && !Decoded.TryAdd(id, image)) image.Dispose();
		});
	}
}
