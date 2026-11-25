using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>Impact slowdown for P5-owned tweens only.</summary>
public static class HitStop
{
	public static Func<double> RealSeconds = () => Time.GetTicksMsec() / 1000.0;

	private static readonly List<Tween> Tweens = new();
	private static SceneTree? _tree;
	private static double _began, _until, _blockUntil, _lostTime, _scale = 1.0;
	private static int _registered;

	internal static double VisualSeconds()
	{
		double now = RealSeconds();
		double pending = _began < _until ? Math.Clamp(now - _began, 0, _until - _began) * (1 - _scale) : 0;
		return now - _lostTime - pending;
	}

	internal static Tween Track(Tween tween)
	{
		if (++_registered % 64 == 0) Tweens.RemoveAll(t => !t.IsValid());
		Tweens.Add(tween);
		if (_began < _until) tween.SetSpeedScale((float)_scale);
		return tween;
	}

	public static void Request(double seconds, double scale = 0.08)
	{
		if (OS.GetThreadCallerId() != OS.GetMainThreadId())
		{
			Callable.From(() => Request(seconds, scale)).CallDeferred();
			return;
		}
		double now = RealSeconds();
		if (now < _blockUntil || Engine.GetMainLoop() is not SceneTree tree) return;
		Finish();
		_began = now;
		_until = now + seconds;
		_blockUntil = _until + 0.35;
		_scale = scale;
		SetSpeeds((float)scale);
		if (!ReferenceEquals(_tree, tree))
		{
			_tree = tree;
			tree.ProcessFrame += Watch;
		}
	}

	private static void Watch()
	{
		if (_began < _until && RealSeconds() >= _until) Finish();
	}

	private static void Finish()
	{
		if (_began >= _until) return;
		_lostTime += (_until - _began) * (1 - _scale);
		_began = _until;
		SetSpeeds(1f);
	}

	private static void SetSpeeds(float speed)
	{
		Tweens.RemoveAll(t => !t.IsValid());
		foreach (Tween tween in Tweens) tween.SetSpeedScale(speed);
	}
}
