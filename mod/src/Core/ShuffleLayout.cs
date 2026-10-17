using System;

namespace P3Makoto.Core;

/// <summary>奖励数量决定整行缩放和发牌节奏；不修改对象池卡牌的输入或悬停设置。</summary>
internal static class ShuffleLayout
{
	internal const float Spacing = 350f;
	internal const float StartAt = 0.05f;
	internal const float Fall = 0.45f;

	internal static float RowScale(int count, float viewportWidth, float rowScaleX, float cardWidth, float margin)
	{
		if (count <= 0) return 1f;
		float available = MathF.Max(1f, viewportWidth - 2f * margin);
		float span = ((count - 1) * Spacing + cardWidth) * MathF.Max(0.001f, rowScaleX);
		return Math.Clamp(available / MathF.Max(1f, span), 0f, 1f);
	}

	// 牌多时压缩逐张落下的间隔，不能让固定的安全显示回调先于正常翻牌触发。
	internal static float Gap(int count) => MathF.Min(0.12f, 0.6f / Math.Max(1, count - 1));
	internal static float FlipAt(int count) => StartAt + Gap(count) * Math.Max(0, count - 1) + Fall + 0.3f;
}
