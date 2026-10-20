using Godot;
using Clock = P3Makoto.Core.TimeScale;

// Exercise the actual production watchdog with a deterministic real-time clock.
// Godot-facing stubs deliberately never fire the lost hit-stop recovery timer.
int checks = 0;
void Check(bool ok, string message)
{
    checks++;
    if (!ok) throw new Exception(message);
}
void Advance(ulong ms)
{
    Time.Msec += ms;
    Engine.Tree.Step();
}
Time.Msec = 1000;
Clock.Install();
Clock.Install();
Check(Engine.Tree.Listeners == 1, "Install must not accumulate frame handlers");

Clock.HitStop(0.08, 0.35);
Check(Engine.TimeScale == 0.08, "Reproduce the player's 0.08 hit stop");
Advance(300);
Check(Engine.TimeScale == 0.08, "Do not cut off an active hit stop prematurely");
Advance(51);
Check(Engine.TimeScale == 1, "Lost timer must recover from 0.08 within its real-time deadline");

Clock.Freeze(0.02, 3);
Clock.HitStop(0.08, 0.35);
Advance(351);
Check(Engine.TimeScale == 0.02, "Expired hit stop must preserve a valid overlapping freeze");
Advance(2650);
Check(Engine.TimeScale == 1, "Lost freeze recovery must also expire");

Clock.HitStop(0.08, 0.35);
Clock.HitStop(1);
Advance(400);
Check(Engine.TimeScale == 1, "A normal release must remain at normal speed");

Engine.TimeScale = 0.08; // Another writer / old state, with no active P3 source.
Advance(1);
Advance(99);
Check(Engine.TimeScale == 0.08, "Stale hit stop gets a short grace period");
Advance(1);
Check(Engine.TimeScale == 1, "Player's stale 0.08 must recover after 100ms, not 2.5 seconds");

Engine.TimeScale = 0.1;
Advance(1);
Advance(150);
Check(Engine.TimeScale == 0.1, "Do not cut off vanilla's 0.1 hit stop");
Engine.TimeScale = 0.35;
Advance(150);
Check(Engine.TimeScale == 0.35, "Do not cut off vanilla's easing back to normal");
Engine.TimeScale = 1;
Advance(1);

Engine.TimeScale = 0.1;
Advance(1);
Advance(2000);
Check(Engine.TimeScale == 0.1, "Preserve vanilla's full two-second Forever hit stop");
Engine.TimeScale = 1;
Advance(1);
Engine.TimeScale = 0.4;
Advance(1);
Advance(2501);
Check(Engine.TimeScale == 1, "Retain the existing 2.5s fallback for unknown stale scales");

Clock.Freeze(0.02, 3);
Engine.TimeScale = 0.08;
Advance(200);
Check(Engine.TimeScale == 0.08, "Stale hit recovery must not release an active cinematic freeze");
Clock.Freeze(1);

// Background requests must not mutate live state or gain extra duration while queued.
OS.Caller = 2;
Clock.HitStop(0.08, 0.04);
Check(Engine.TimeScale == 1, "Background hit stop must wait for the main thread");
Time.Msec += 100;
OS.Caller = 1;
Callable.Flush();
Check(Engine.TimeScale == 1, "An expired queued hit stop must never start late");
OS.Caller = 2;
Clock.HitStop(0.08, 0.1);
Time.Msec += 25;
OS.Caller = 1;
Callable.Flush();
Check(Engine.TimeScale == 0.08, "A still-valid queued hit stop must start on main thread");
Advance(75);
Check(Engine.TimeScale == 1, "Queued hit stop must retain the original deadline");

// The actual pulse length is 40ms; a missing timer cannot add a 300ms safety tail.
Clock.HitStop(0.08, 0.04);
Advance(39);
Check(Engine.TimeScale == 0.08, "Keep intended short pulse");
Advance(1);
Check(Engine.TimeScale == 1, "Short pulse ends exactly at its real-time deadline");

Clock.Freeze(0.02);
Clock.HitStop(0.08);
Clock.Reset();
Check(Engine.TimeScale == 1, "Reset must clear both slowdown sources");
OS.Caller = 2;
Clock.Freeze(0.02, 1);
Check(Engine.TimeScale == 1, "Background freeze must wait for main thread");
OS.Caller = 1;
Callable.Flush();
Check(Engine.TimeScale == 0.02, "Queued freeze must apply on main thread");
OS.Caller = 2;
Clock.Reset();
Check(Engine.TimeScale == 0.02, "Background reset must not mutate live state");
OS.Caller = 1;
Callable.Flush();
Check(Engine.TimeScale == 1, "Queued reset must release freeze on main thread");
Check(GD.Lines.Any(s => s.Contains("freeze")) &&
      GD.Lines.Any(s => s.Contains("stale hit stop 0.08")), "Abnormal recoveries must leave diagnostic evidence");
Console.WriteLine($"PASS: {checks} production TimeScale checks; lost timer, overlapping sources, orphan slowdown, reset, single subscription");

namespace Godot
{
    public sealed class SceneTree
    {
        public event Action? ProcessFrame;
        public int Listeners => ProcessFrame?.GetInvocationList().Length ?? 0;
        public void Step() => ProcessFrame?.Invoke();
    }
    public static class Time { public static ulong Msec; public static ulong GetTicksMsec() => Msec; }
    public static class Engine
    {
        public static double TimeScale = 1;
        public static readonly SceneTree Tree = new();
        public static object GetMainLoop() => Tree;
    }
    public static class OS
    {
        public static ulong Caller = 1;
        public static ulong GetThreadCallerId() => Caller;
        public static ulong GetMainThreadId() => 1;
    }
    public sealed class Callable
    {
        private readonly Action _action;
        private Callable(Action action) => _action = action;
        public static Callable From(Action action) => new(action);
        private static readonly Queue<Action> Pending = new();
        public void CallDeferred() => Pending.Enqueue(_action);
        public static void Flush() { while (Pending.TryDequeue(out var action)) action(); }
    }
    public static class GD
    {
        public static readonly List<string> Lines = new();
        public static void Print(string text) { Lines.Add(text); Console.WriteLine(text); }
    }
}
namespace P3Makoto
{
    public static class ModEntry
    {
        public const string Tag = "[P3Makoto]";
        public static void Safe(string label, Action action) => action();
    }
}
