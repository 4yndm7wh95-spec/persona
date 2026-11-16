using System;
using Godot;

namespace P5Joker;

/// <summary>One summon owns one clock and one pinch state. A later card cannot change this clip.</summary>
public sealed class CutInPlayback
{
	public readonly float SplitAt;
	public readonly float Seed = Fx.Rand(0f, 100f);
	public readonly bool Crush;
	public bool Ready = true;
	/// <summary>碎片朝镜头（屏幕外）炸开：从横幅后面刺穿过来的那种碎法（苦无，Vfx/KunaiPierce.cs）。</summary>
	public bool Outward;
	public float Pinch;
	public Vector2 PinchAt;
	public Vector2? ShatterFrom;
	internal Node2D? PaperRoot; // The actual sibling for this play, including overlapping summons.
	private ulong _last;
	private float _time;
	private bool _started;

	public CutInPlayback(float splitAt, bool crush = false) { SplitAt = splitAt; Crush = crush; }
	public void Start() { _last = Stage.NowMs(); _started = true; }
	public float Time
	{
		get
		{
			if (!_started) Start();
			ulong now = Stage.NowMs();
			_time += now >= _last ? (now - _last) / 1000f : 0f;
			_last = now;
			// Slow on-apply hooks may still be running: hold the grip, never damage before poison is applied.
			if (!Ready) _time = Mathf.Min(_time, SplitAt - 0.001f);
			return _time;
		}
	}
}
