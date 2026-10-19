namespace P3Makoto.Core;

/// <summary>只有连续空闲一段时间才允许预热；原版重新加载或打开界面时重新计时。</summary>
internal sealed class IdleWarmupGate
{
	private ulong? since;
	internal bool Ready(ulong now, bool eligible)
	{
		if (!eligible) { since = null; return false; }
		since ??= now;
		return now - since.Value >= 300;   // v0.14.89：800 → 300ms，十几项在主菜单停几秒就能做完
	}
}
