using System.Numerics;
using P3MusicBox;
using P3Makoto.Core;

int checks = 0;
void Check(bool ok, string message)
{
    checks++;
    if (!ok) throw new Exception(message);
}
void Near(Vector2 a, Vector2 b, string message) => Check(Vector2.Distance(a, b) < 0.002f, message);

Vector2 view = new(1920, 1080), pos = new(100, 100), down = new(117, 123);
IconDrag drag = new();
drag.Begin(1000, down, pos);
Check(drag.Pressed && !drag.Dragging, "press must not open popup or start dragging");
drag.Begin(1030, down + new Vector2(3), pos); // GUI + poll reporting the same physical press.
drag.Step(1449, down + new Vector2(2), view);
Check(!drag.Dragging, "duplicate begin must not reset or advance the hold timer");
Check(drag.Release(1449, down, view, true), "449ms stationary press is a click");
Check(!drag.Release(1449, down, view, true), "one release must not toggle the popup twice");

drag.Begin(2000, down, pos);
drag.Step(2450, down + new Vector2(83, 77), view);
Check(drag.Dragging, "450ms reaches drag mode");
Near(drag.Position, new(183, 177), "drag must preserve the cursor's original grab point");
Check(drag.Moved, "moving icon must request persistence");
Check(!drag.Release(2500, down + new Vector2(83, 77), view, true), "drag release must not open popup");
Check(!drag.Pressed, "release must free input capture");

drag.Begin(3000, down, pos);
Check(!drag.Release(3450, down, view, true), "long hold without movement must not become a click");
Check(!drag.Moved, "stationary long hold need not save a new position");
drag.Begin(4000, down, pos);
Check(!drag.Release(4100, down + new Vector2(20, 0), view, true), "short swipes must not toggle popup");
drag.Begin(5000, down, pos);
Check(!drag.Release(5050, down, view, false), "release outside icon cancels click");
drag.Begin(6000, down, pos);
drag.Step(6450, new(-1000, -1000), view);
Near(drag.Position, new(8, 8), "dragging outside upper-left must stop at the viewport edge");
drag.Step(6500, new(5000, 5000), view);
Near(drag.Position, new(1860, 1020), "dragging outside bottom-right must stop at the viewport edge");
drag.Cancel(); // Focus lost / capstone opened while held.
Check(!drag.Pressed && !drag.Dragging, "cancellation must release capture and drag state");
Check(!drag.Release(7000, down, view, true), "focus-loss release must not open popup");
drag.Begin(8000, down, pos);
Check(!drag.Moved && !drag.Dragging, "next gesture must reset cancelled gesture's flags");
Check(drag.Release(8100, down, view, true), "short click works again after cancellation");

Random random = new(0x5033);
foreach (Vector2 size in new[] { new Vector2(68, 68), new(640, 480), new(1280, 720), view, new(2560, 1440), new(3440, 1440), new(3840, 2160) })
{
    for (int i = 0; i < 1000; i++)
    {
        Vector2 p = new((float)(random.NextDouble() * size.X * 4 - size.X), (float)(random.NextDouble() * size.Y * 4 - size.Y));
        Vector2 clamped = IconDrag.Clamp(p, size);
        Vector2 center = clamped + new Vector2(26);
        // The outer stroke extends to radius 30; hovering enlarges the visible art to 32.4.
        Check(center.X - 32.4f >= 0 && center.Y - 32.4f >= 0 && center.X + 32.4f <= size.X && center.Y + 32.4f <= size.Y,
            $"whole hovered icon must fit, not just the hitbox: {size} / {clamped}");
        Vector2 saved = IconDrag.ToSaved(clamped, size);
        Check(saved.X >= 0 && saved.X <= 1 && saved.Y >= 0 && saved.Y <= 1, "saved coordinates must be normalized");
        Near(IconDrag.FromSaved(saved, size), clamped, "save/load roundtrip changed icon position");
        Vector2 resized = IconDrag.FromSaved(saved, view);
        Check(resized.X >= 8 && resized.Y >= 8 && resized.X + 60 <= view.X && resized.Y + 60 <= view.Y, "resolution changes must retain valid placement");
    }
}
Near(IconDrag.FromSaved(new(1, 1), new(1280, 720)), new(1220, 660), "bottom-right preference follows resolution changes");
Vector2 finite = IconDrag.Clamp(new(float.NaN, float.PositiveInfinity), view);
Check(float.IsFinite(finite.X) && float.IsFinite(finite.Y), "invalid prefs must not propagate NaN into Godot transforms");

IdleWarmupGate gate = new();
Check(!gate.Ready(0, false), "Logo/loading must never start a warmup");
Check(!gate.Ready(5000, false), "long startup wait must not count as idle time");
Check(!gate.Ready(6000, true), "first idle frame must not synchronously load assets");
Check(!gate.Ready(6799, true), "menu must stay idle for the full delay");
Check(gate.Ready(6800, true), "stable menu can start work after 800ms");
Check(gate.Ready(7000, true), "next serialized job can reuse stable menu state");
Check(!gate.Ready(7010, false), "native loading/combat/modal must immediately stop new jobs");
Check(!gate.Ready(10000, true), "returning to idle must restart the quiet period");
Check(!gate.Ready(10799, true), "no stale idle timestamp after native loader interruption");
Check(gate.Ready(10800, true), "work resumes after a fresh quiet period");
for (ulong cycle = 0; cycle < 100; cycle++)
{
    ulong t = 20000 + cycle * 1000;
    Check(!gate.Ready(t, false), "busy window must block warmup");
    Check(!gate.Ready(t + 10, true), "each interrupted menu session must restart delay");
    Check(!gate.Ready(t + 809, true), "rapid enter/exit must not leak one warmup into combat");
    Check(gate.Ready(t + 810, true), "uninterrupted idle window must eventually progress");
}
Console.WriteLine($"PASS: {checks} checks (long-press/click/cancel, viewport bounds, persistence, safe idle warmup gating)");
