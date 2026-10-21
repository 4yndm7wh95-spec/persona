using System;

namespace P3Makoto.Core;

/// <summary>Clip scale channels are sparse: omitted channels mean unit scale, not previous pose.</summary>
internal sealed class BoneScaleChannels(Action<int> reset)
{
	private object? _owner;
	private int[] _channels = Array.Empty<int>();
	internal void Enter(object owner, int[] channels)
	{
		if (ReferenceEquals(_owner, owner)) return;
		foreach (int index in _channels)
			if (index >= 0) reset(index);
		_owner = owner;
		_channels = channels;
	}
}
