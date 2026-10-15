using Godot;

namespace P3Makoto.Vfx;

/// <summary>缓动函数，输入输出都在 0~1。见 docs/STYLE_P3R.md 第 2 节。</summary>
public static class Ease
{
	public static float OutExpo(float t) => t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);

	public static float InExpo(float t) => t <= 0f ? 0f : Mathf.Pow(2f, 10f * t - 10f);

	public static float OutQuint(float t) => 1f - Mathf.Pow(1f - t, 5f);

	public static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

	public static float InCubic(float t) => t * t * t;

	public static float InOutCubic(float t) => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

	public static float InOutSine(float t) => -(Mathf.Cos(Mathf.Pi * t) - 1f) / 2f;
	public static float OutSine(float t) => Mathf.Sin(t * Mathf.Pi / 2f);

	public static float OutBack(float t)
	{
		const float c1 = 1.70158f;
		const float c3 = c1 + 1f;
		return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
	}

	/// <summary>把全局进度 t 映射到 [start, start+length] 区间内的局部进度。</summary>
	public static float Segment(float t, float start, float length) => Mathf.Clamp((t - start) / length, 0f, 1f);

	/// <summary>先淡入再淡出的包络：in 段内 0→1，out 段起点之后 1→0。</summary>
	public static float Envelope(float t, float fadeIn, float fadeOutStart)
	{
		if (t < fadeIn)
		{
			return fadeIn <= 0f ? 1f : t / fadeIn;
		}
		if (t > fadeOutStart)
		{
			return 1f - (t - fadeOutStart) / (1f - fadeOutStart);
		}
		return 1f;
	}

	/// <summary>先往反方向稍微回一下再加速（退场用）。</summary>
	public static float InBackLike(float t)
	{
		const float k = 1.70158f;
		return t * t * ((k + 1f) * t - k);
	}
}
