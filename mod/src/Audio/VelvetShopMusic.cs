using System;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Saves;

namespace P3Makoto.Audio;

/// <summary>
/// Velvet Room piano loop for the Igor shop. Fades the native BGM out while the
/// room is open and restores it when the room leaves the tree. The music box
/// (P3MusicBox) takes priority: while it plays, this loop stays silent.
/// </summary>
internal static class VelvetShopMusic
{
	private const string Path = "res://p3makoto/bgm/velvet_room.ogg";
	private const string LoopPath = "res://p3makoto/bgm/velvet_room.loop.txt";
	private const float FadeIn = 1.6f, FadeOut = 0.9f, Gain = 0.85f;
	private static AudioStreamOggVorbis? _stream;
	private static bool _loaded;

	private static AudioStreamOggVorbis? Load()
	{
		if (_loaded) return _stream;
		_loaded = true;
		try
		{
			if (!FileAccess.FileExists(Path)) { GD.Print($"{ModEntry.Tag} velvet room bgm missing; native shop music kept"); return null; }
			_stream = AudioStreamOggVorbis.LoadFromBuffer(FileAccess.GetFileAsBytes(Path));
			if (_stream == null) return null;
			_stream.Loop = true;
			// Optional sidecar: loop start in seconds (the file itself ends at the loop end).
			if (FileAccess.FileExists(LoopPath) && double.TryParse(FileAccess.GetFileAsString(LoopPath).Trim(),
				System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double offset))
				_stream.LoopOffset = Math.Clamp(offset, 0, Math.Max(0, _stream.GetLength() - 0.5));
		}
		catch (Exception e) { GD.PrintErr($"{ModEntry.Tag} velvet room bgm failed: {e.Message}"); _stream = null; }
		return _stream;
	}

	private static bool MusicBoxPlaying(SceneTree tree) =>
		tree.Root.GetNodeOrNull<AudioStreamPlayer>("P3MusicBox/P3MusicBoxPlayer") is { } p && p.Playing && !p.StreamPaused && p.VolumeDb > -60f;

	public static void Attach(Node room)
	{
		if (Load() is not { } stream) return;
		var tree = room.GetTree();
		// Lives under the root so it can fade out after the room is freed.
		var player = new AudioStreamPlayer { Stream = stream, Bus = "Master", VolumeDb = -80f, Name = "P3VelvetShopMusic", ProcessMode = Node.ProcessModeEnum.Always };
		tree.Root.AddChild(player);
		player.Play();
		float fade = 0f, nativeApplied = -1f;
		bool leaving = false;
		ulong last = Time.GetTicksMsec();
		void Tick()
		{
			ulong now = Time.GetTicksMsec();
			float dt = Mathf.Min((now - last) / 1000f, 0.1f);
			last = now;
			if (!GodotObject.IsInstanceValid(player)) { tree.ProcessFrame -= Tick; return; }
			bool box = MusicBoxPlaying(tree);
			float target = leaving || box ? 0f : 1f;
			fade = Mathf.MoveToward(fade, target, dt / (target > fade ? FadeIn : FadeOut));
			float smooth = fade * fade * (3f - 2f * fade);
			float bgm = SaveManager.Instance?.SettingsSave?.VolumeBgm ?? 0.5f;
			// The native BGM is applied as volume²; match that loudness curve.
			float lin = smooth * bgm * bgm * Gain;
			player.VolumeDb = lin <= 0.0001f ? -80f : Mathf.LinearToDb(lin);
			// Native BGM: muted while our loop sounds; the music box manages it when it plays.
			if (!box && NAudioManager.Instance is { } am)
			{
				float native = bgm * (1f - smooth);
				if (Mathf.Abs(native - nativeApplied) > 0.002f) { am.SetBgmVol(native); nativeApplied = native; }
			}
			if (leaving && fade <= 0f)
			{
				tree.ProcessFrame -= Tick;
				if (!box && NAudioManager.Instance is { } am2) am2.SetBgmVol(bgm);
				player.QueueFree();
			}
		}
		tree.ProcessFrame += Tick;
		room.TreeExiting += () => leaving = true;
		GD.Print($"{ModEntry.Tag} velvet room bgm started (loop from {stream.LoopOffset:0.00}s)");
	}
}
