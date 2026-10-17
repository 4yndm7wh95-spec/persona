using System;
using Godot;
using P3Makoto.Core;

namespace P3Makoto.Vfx;

/// <summary>每个牌堆独有一只蓝蝶；由界面现有 Tick 驱动，没有额外全局事件或无限 Tween。</summary>
internal sealed class PileButterfly
{
	private readonly ButterflyVisual visual;
	private ButterflyVisual? foreground;
	private readonly ButterflyOrbit orbit;
	private readonly float k, rx, speed, visualSize;
	private readonly bool deck;
	private Vector2 position, heading = Vector2.Up, exitAt, exitHeading;
	private float angle, depth, exitDepth, exitTime = -1f;
	private bool first = true;

	internal PileButterfly(Node2D parent, PileFx.Kind kind, float k, float rx, float portraitScale)
	{
		this.k = k;
		deck = kind == PileFx.Kind.Deck;
		this.rx = kind == PileFx.Kind.Deck ? 0f : rx;
		(float x, float y, float wide, float high, float phase, float pace, float headWidth) = kind switch
		{
			PileFx.Kind.Deck => (365f, 510f, 445f, 350f, 0f, 118f, 1080f),
			PileFx.Kind.Discard => (1510f, 490f, 350f, 400f, 0.8f, 120f, 460f),
			PileFx.Kind.Exhaust => (1530f, 530f, 260f, 340f, 2.6f, 100f, 210f),
			_ => (1480f, 500f, 370f, 400f, 0.3f, 118f, 520f),
		};
		orbit = new ButterflyOrbit(x, y, wide, high, phase, silhouetteLoop: deck);
		speed = pace;
		// 原图头部（含发型）的横向跨度 × 实际立绘缩放。不能用相同的画布宽度：消耗堆有大量透明留白。
		// 普通页面张翼约为头宽的 1/4；牌组特写改为 0.14，避免遮脸。贴图有效横向比例约 365/384。
		visualSize = headWidth * portraitScale * k * (deck ? 0.14f : 0.25f) / (2f * (365f / 384f));
		visual = new ButterflyVisual(parent, visualSize);
		Update(0f, 0f);
	}

	/// <summary>立绘创建完后接上前景层；两份同步外观按深度混合，不逐帧 Reparent，也不抬到卡牌 UI 之上。</summary>
	internal void AttachForeground(Node2D parent)
	{
		foreground = new ButterflyVisual(parent, visualSize);
		Update(0f, 0f);
	}

	internal void BeginExit(float now)
	{
		if (exitTime >= 0f) return;
		exitTime = now;
		exitAt = position;
		exitHeading = heading;
		exitDepth = depth;
	}

	internal void Update(float now, float dt)
	{
		float flight = MathF.Max(0f, now - 0.12f);
		float d = ButterflyOrbit.Distance(flight, speed);
		System.Numerics.Vector3 p, ahead;
		if (deck)
		{
			// 先从右侧留白飞入；不把闭环中间的脸部位置当作淡入起点。
			System.Numerics.Vector2 from = new(860f, 340f);
			p = orbit.EnterAt(flight, speed, 1.7f, from);
			ahead = orbit.EnterAt(flight + 0.025f, speed, 1.7f, from);
		}
		else
		{
			p = orbit.At(d);
			ahead = orbit.At(d + 3f);
		}
		heading = new Vector2(ahead.X - p.X, ahead.Y - p.Y).Normalized();
		float entry = Mathf.Clamp(flight / 0.9f, 0f, 1f);
		position = new Vector2(p.X * k + rx, p.Y * k);
		// 从轨迹的反切线方向轻轻飞入；和人物整组的滑入同步，不会在脸旁突然显形。
		if (!deck) position -= heading * (100f * k * (1f - Ease.OutCubic(entry)));
		depth = p.Z;
		float fade = Ease.InOutSine(entry);
		if (exitTime >= 0f)
		{
			float t = Mathf.Clamp((now - exitTime) / 0.42f, 0f, 1f);
			position = exitAt + (exitHeading * 110f + new Vector2(0f, -30f)) * k * Ease.InCubic(t);
			heading = exitHeading;
			depth = exitDepth;
			fade *= 1f - Ease.InOutSine(t);
		}
		float target = heading.Angle() + Mathf.Pi / 2f;
		angle = first ? target : Mathf.LerpAngle(angle, target, 1f - MathF.Exp(-8f * dt));
		first = false;
		float near = (depth + 1f) / 2f;
		// 牌组在轮廓外完成换层，不在脸上把两份外观交叉淡出；其他页面保持原有表现。
		float frontWeight = foreground == null ? 0f : deck ? (depth >= 0f ? 1f : 0f) : Mathf.SmoothStep(-0.16f, 0.16f, depth);
		Color tint = new(1f, 1f, 1f, fade * Mathf.Lerp(0.72f, 1f, near));
		float perspective = Mathf.Lerp(0.72f, 1.08f, near);
		bool emit = exitTime < 0f && entry > 0.02f;
		visual.Root.Modulate = tint;
		visual.Update(position, angle, flight, 1f, perspective, emit: emit && frontWeight < 0.5f, planeAlpha: 1f - frontWeight);
		if (foreground != null)
		{
			foreground.Root.Modulate = tint;
			foreground.Update(position, angle, flight, 1f, perspective, emit: emit && frontWeight >= 0.5f, planeAlpha: frontWeight);
		}
	}
}
