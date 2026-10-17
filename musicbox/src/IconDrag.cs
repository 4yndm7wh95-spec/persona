using System;
using System.Numerics;

namespace P3MusicBox;

/// <summary>GUI 信号和逐帧轮询共用一个长按手势；不在按下时触发短按。</summary>
internal sealed class IconDrag
{
	internal const ulong HoldMs = 450;
	internal const float DragThreshold = 6f;
	internal bool Pressed { get; private set; }
	internal bool Dragging { get; private set; }
	internal bool Moved { get; private set; }
	internal Vector2 Position { get; private set; }
	private Vector2 offset, pressAt, original;
	private ulong downAt;
	private float travel;

	internal void Begin(ulong now, Vector2 cursor, Vector2 position)
	{
		if (Pressed) return;
		Pressed = true;
		Dragging = Moved = false;
		downAt = now;
		pressAt = cursor;
		offset = cursor - position;
		Position = original = position;
		travel = 0f;
	}

	internal void Step(ulong now, Vector2 cursor, Vector2 viewport)
	{
		if (!Pressed) return;
		travel = MathF.Max(travel, Vector2.Distance(cursor, pressAt));
		if (now - downAt >= HoldMs || travel >= DragThreshold) Dragging = true;
		if (Dragging)
		{
			Position = Clamp(cursor - offset, viewport);
			Moved |= Vector2.DistanceSquared(Position, original) > 0.25f;
		}
	}

	internal bool Release(ulong now, Vector2 cursor, Vector2 viewport, bool inside)
	{
		if (!Pressed) return false;
		Step(now, cursor, viewport);
		bool click = !Dragging && travel <= 8f && inside;
		Pressed = false;
		return click;
	}

	internal void Cancel() => Pressed = Dragging = false;

	// 留出外圈在悬停放大时的余量，限制的是整个可见图标而非仅中心点。
	private static float Margin(float extent) => MathF.Min(8f, MathF.Max(0f, (extent - 52f) / 2f));
	private static float Span(float extent) => MathF.Max(0f, extent - 52f - 2f * Margin(extent));
	internal static Vector2 Clamp(Vector2 p, Vector2 view) => new(
		Math.Clamp(float.IsFinite(p.X) ? p.X : 8f, Margin(view.X), Margin(view.X) + Span(view.X)),
		Math.Clamp(float.IsFinite(p.Y) ? p.Y : 8f, Margin(view.Y), Margin(view.Y) + Span(view.Y)));
	internal static Vector2 FromSaved(Vector2 unit, Vector2 view) => Clamp(new(
		Margin(view.X) + Span(view.X) * unit.X, Margin(view.Y) + Span(view.Y) * unit.Y), view);
	internal static Vector2 ToSaved(Vector2 p, Vector2 view) => new(
		Math.Clamp((p.X - Margin(view.X)) / MathF.Max(1f, Span(view.X)), 0f, 1f),
		Math.Clamp((p.Y - Margin(view.Y)) / MathF.Max(1f, Span(view.Y)), 0f, 1f));
}
