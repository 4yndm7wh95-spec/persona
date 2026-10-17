using System;
using System.Numerics;

namespace P3Makoto.Core;

/// <summary>闭合的空间曲线投影；按屏幕弧长推进，控制点或转弯不会让蓝蝶突然加速。</summary>
internal sealed class ButterflyOrbit
{
	private const int Samples = 768;
	private readonly Vector3[] points = new Vector3[Samples + 1];
	private readonly float[] distances = new float[Samples + 1];
	internal float Length => distances[Samples];
	internal ButterflyOrbit(float cx, float cy, float rx, float ry, float phase, bool silhouetteLoop = false)
	{
		for (int i = 0; i <= Samples; i++)
		{
			float a = MathF.Tau * i / Samples + phase;
			// 非正圆：绕远、升降、纵深的节奏错开，像在人物周围自由盘旋。
			float depth = silhouetteLoop ? MathF.Sin(a) : MathF.Sin(a + 0.45f);
			float x = silhouetteLoop ? rx * MathF.Cos(a) + 20f * MathF.Sin(2f * a)
				: rx * (MathF.Cos(a) + 0.10f * MathF.Sin(2f * a + 0.8f));
			float y = silhouetteLoop ? ry * MathF.Sin(a) + 22f * MathF.Sin(2f * a) + 12f * MathF.Cos(3f * a)
				: ry * (0.76f * MathF.Sin(a) + 0.21f * MathF.Sin(2f * a - 0.6f)) + 24f * depth;
			points[i] = new Vector3(cx + x, cy + y, depth);
			if (i > 0) distances[i] = distances[i - 1] + Vector2.Distance(new(points[i].X, points[i].Y), new(points[i - 1].X, points[i - 1].Y));
		}
		points[Samples] = points[0];
	}

	internal Vector3 At(float distance)
	{
		distance = (distance % Length + Length) % Length;
		int lo = 0, hi = Samples;
		while (hi - lo > 1)
		{
			int mid = (lo + hi) / 2;
			if (distances[mid] < distance) lo = mid; else hi = mid;
		}
		return Vector3.Lerp(points[lo], points[hi], (distance - distances[lo]) / MathF.Max(0.001f, distances[hi] - distances[lo]));
	}

	// 速度有缓慢、轻微的呼吸；积分连续，绕完一圈不重新起飞、不停顿。
	internal static float Distance(float time, float speed) => speed * (time + 0.14f / 0.65f * MathF.Sin(0.65f * time));

	/// <summary>先从人物外侧以 Hermite 曲线进入，再接闭环；末端切线/速度与闭环一致。</summary>
	internal Vector3 EnterAt(float time, float speed, float duration, Vector2 from)
	{
		if (time >= duration) return At(Distance(time - duration, speed));
		Vector3 end = At(0f), ahead = At(1f);
		Vector2 target = new(end.X, end.Y);
		Vector2 tangent = Vector2.Normalize(new Vector2(ahead.X - end.X, ahead.Y - end.Y)) * speed * 1.14f * duration;
		float u = Math.Clamp(time / duration, 0f, 1f), u2 = u * u, u3 = u2 * u;
		Vector2 p = from * (2f * u3 - 3f * u2 + 1f) + target * (-2f * u3 + 3f * u2) + tangent * (u3 - u2);
		return new Vector3(p, end.Z);
	}
}
