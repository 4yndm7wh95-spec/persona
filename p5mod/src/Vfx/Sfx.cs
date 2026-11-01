using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 音效（2026-11-01）：P5R 原声，tools/p5/stage_p5.py 把 research/p5r/audio 里挑出来的 .ogg 原样放进 pck 的 res://p5joker/sfx/，
/// 这里读原始字节解码（不走导入系统），同 P3 mod 的 mod/src/Audio/Sfx.cs。只用 Godot，预览工具也能编译。
/// </summary>
public static class Sfx
{
	/// <summary>原名 → pck 里的文件名（不带 .ogg）。</summary>
	public const string
		OneMore = "battle_onemore",     // 弱点 / 1 MORE 眼神特写的「WEAK」音（SOUND_J SYSTEM.ACB #12）
		CutIn = "cutin",                // 切入（#178）
		PersonaFlame = "persona_flame", // Persona 蓝焰（#44）
		Curse = "skill_ankoku",         // 暗黑系技能音（SKILLSE/FLOATING/F_ANKOKU，咒怨候选）
		Glass = "glass_burst";          // 玻璃碎裂（P3R ShuffleTime_ArcanaBurst，碎法移植自结城理 mod）

	private const string Dir = "res://p5joker/sfx/";
	private static readonly Dictionary<string, AudioStreamOggVorbis?> Cache = new();
	private static readonly Dictionary<string, ulong> Last = new();

	private static AudioStreamOggVorbis? Load(string name)
	{
		if (Cache.TryGetValue(name, out var s))
		{
			return s;
		}
		try
		{
			string path = Dir + name + ".ogg";
			byte[] bytes = FileAccess.FileExists(path) ? FileAccess.GetFileAsBytes(path) : Array.Empty<byte>();
			s = bytes.Length > 0 ? AudioStreamOggVorbis.LoadFromBuffer(bytes) : null;
		}
		catch (Exception e)
		{
			Stage.Report("sfx " + name, e);
			s = null;
		}
		Cache[name] = s;
		return s;
	}

	/// <summary>播放；同一音效 45ms 内不重复叠加。volume 0～1.4。</summary>
	public static void Play(string name, float volume = 1f, float delay = 0f) => Stage.Safe("sfx " + name, () =>
	{
		if (Engine.GetMainLoop() is not SceneTree tree)
		{
			return;
		}
		if (delay > 0f)
		{
			tree.CreateTimer(delay, processAlways: true).Timeout += () => Play(name, volume);
			return;
		}
		ulong now = Time.GetTicksMsec();
		if (Last.TryGetValue(name, out ulong last) && now - last < 45)
		{
			return;
		}
		Last[name] = now;
		if (Load(name) is not AudioStreamOggVorbis stream)
		{
			return;
		}
		StringName bus = AudioServer.GetBusIndex("SFX") >= 0 ? new StringName("SFX") : new StringName("Master");
		AudioStreamPlayer p = new() { Stream = stream, Bus = bus, VolumeDb = Mathf.LinearToDb(Mathf.Clamp(volume, 0.0001f, 1.4f) * 0.75f) };
		tree.Root.AddChild(p);
		p.Finished += p.QueueFree;
		p.Play();
		tree.CreateTimer(stream.GetLength() + 2.0, processAlways: true).Timeout += () =>
		{
			if (GodotObject.IsInstanceValid(p))
			{
				p.QueueFree();
			}
		};
	});
}
