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
Advance(2499);
Check(Engine.TimeScale == 0.08, "Orphan slowdown gets the watchdog's grace period");
Advance(2);
Check(Engine.TimeScale == 1, "Orphan 0.08 must recover after 2.5 seconds");

Clock.Freeze(0.02);
Clock.HitStop(0.08);
Clock.Reset();
Check(Engine.TimeScale == 1, "Reset must clear both slowdown sources");
Check(GD.Lines.Any(s => s.Contains("hit stop")) && GD.Lines.Any(s => s.Contains("freeze")) &&
      GD.Lines.Any(s => s.Contains("stuck at 0.08")), "Every watchdog recovery must leave diagnostic evidence");
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
        public static ulong GetThreadCallerId() => 1;
        public static ulong GetMainThreadId() => 1;
    }
    public sealed class Callable
    {
        private readonly Action _action;
        private Callable(Action action) => _action = action;
        public static Callable From(Action action) => new(action);
        public void CallDeferred() => _action();
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
