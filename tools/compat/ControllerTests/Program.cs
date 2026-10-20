using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using P3Makoto.Core;

int checks = 0;
void Check(bool ok, string why) { checks++; if (!ok) throw new Exception(why); }
foreach (string action in new[] { "ui_select", "ui_confirm", "ui_accept" })
{
    Time.Now = 1000;
    var previous = new Control(); previous.GrabFocus();
    var lifetime = new Node();
    int first = 0, second = 0, underlying = 0;
    var hk = NHotkeyManager.Instance;
    Action game = () => underlying++;
    hk.PushHotkeyPressedBinding(action, game);
    hk.PushHotkeyReleasedBinding(action, game);
    var input = new CinematicConfirm(lifetime);
    Check(hk.Blockers == 1 && Control.Focused != previous, "Cinematic owns focus and blocks lower hotkeys");
    input.Arm(() => first++);
    hk.Press(action);
    Check(first == 0, "Opening button cannot immediately confirm");
    Time.Now += 400;
    hk.Press(action); hk.Press(action); hk.Release(action);
    Check(first == 1 && underlying == 0, "Each game confirm alias works once without leaking its release");
    input.Arm(() => second++);
    hk.Press(action);
    Check(second == 0, "Next prompt has its own input grace period");
    Time.Now += 400;
    var focus = Control.Focused!;
    focus.Send(new InputEvent(action, echo: true));
    Check(second == 0, "Keyboard echo cannot advance the next prompt");
    focus.Send(new InputEvent(action));
    hk.Press(action);
    Check(second == 1, "Focused GUI confirmation and hotkey dispatch cannot double-confirm");
    lifetime.Exit(); input.Dispose();
    Check(hk.Blockers == 0 && Control.Focused == previous, "Tree exit cleans blockers and restores prior focus");
    hk.Press(action); hk.Release(action);
    Check(underlying == 2, "Underlying controls work normally after cleanup");
    hk.RemoveHotkeyPressedBinding(action, game); hk.RemoveHotkeyReleasedBinding(action, game);
}

// Old versions may lack ui_confirm: do not register nonexistent actions.
InputMap.Missing = "ui_confirm";
var owner = new Node();
var older = new CinematicConfirm(owner);
int selected = 0; older.Arm(() => selected++);
Time.Now += 400;
NHotkeyManager.Instance.Press("ui_select");
Check(selected == 1, "Older versions retain ui_select when ui_confirm is absent");
owner.Exit();
InputMap.Missing = null;

// Deferred hotkey dispatch may outlive scene teardown.
var abortedOwner = new Node();
var aborted = new CinematicConfirm(abortedOwner);
aborted.Arm(() => throw new Exception("Stale callback must not run"));
Time.Now += 400;
Action delayed = NHotkeyManager.Instance.Top("ui_select");
abortedOwner.Exit(); delayed();
Check(NHotkeyManager.Instance.Blockers == 0, "A queued callback after teardown is harmless");
Console.WriteLine($"PASS: {checks} production cinematic input checks");

namespace Godot
{
    public class GodotObject { public static bool IsInstanceValid(object o) => true; }
    public class Node : GodotObject
    {
        public string Name { get; set; } = "";
        public event Action? TreeExiting;
        public void AddChild(Node n) { }
        public NodePath GetPath() => new();
        public void Exit() => TreeExiting?.Invoke();
        public bool IsInsideTree() => true;
    }
    public sealed class NodePath { }
    public sealed class Viewport { public Control? GuiGetFocusOwner() => Control.Focused; }
    public sealed class Control : Node
    {
        public enum FocusModeEnum { None, All }
        public enum MouseFilterEnum { Ignore }
        public FocusModeEnum FocusMode { get; set; }
        public MouseFilterEnum MouseFilter { get; set; }
        public NodePath? FocusNeighborLeft, FocusNeighborRight, FocusNeighborTop, FocusNeighborBottom, FocusNext, FocusPrevious;
        public static Control? Focused;
        public event Action<InputEvent>? GuiInput;
        public void Send(InputEvent e) => GuiInput?.Invoke(e);
        public Viewport GetViewport() => new();
        public void GrabFocus() => Focused = this;
        public void ReleaseFocus() { if (Focused == this) Focused = null; }
        public void AcceptEvent() { }
        public bool IsVisibleInTree() => true;
    }
    public sealed class InputEvent(string action, bool echo = false)
    {
        public bool IsActionPressed(string a) => a == action;
        public bool IsEcho() => echo;
    }
    public static class Time { public static ulong Now; public static ulong GetTicksMsec() => Now; }
    public static class InputMap { public static string? Missing; public static bool HasAction(string a) => a != Missing; }
}
namespace MegaCrit.Sts2.Core.Nodes.CommonUi
{
    public sealed class NHotkeyManager : GodotObject
    {
        public static NHotkeyManager Instance { get; } = new();
        private readonly Dictionary<string, List<Action>> pressed = new(), released = new();
        public int Blockers;
        public void AddBlockingScreen(Node n) => Blockers++;
        public void RemoveBlockingScreen(Node n) => Blockers--;
        static void Push(Dictionary<string, List<Action>> d, string k, Action a)
        {
            if (!InputMap.HasAction(k)) throw new Exception("Missing action registered");
            if (!d.TryGetValue(k, out var list)) d[k] = list = new();
            list.Add(a);
        }
        public void PushHotkeyPressedBinding(string k, Action a) => Push(pressed, k, a);
        public void PushHotkeyReleasedBinding(string k, Action a) => Push(released, k, a);
        public void RemoveHotkeyPressedBinding(string k, Action a) => pressed[k].Remove(a);
        public void RemoveHotkeyReleasedBinding(string k, Action a) => released[k].Remove(a);
        public Action Top(string k) => pressed[k].Last();
        public void Press(string k) => pressed[k].LastOrDefault()?.Invoke();
        public void Release(string k) => released[k].LastOrDefault()?.Invoke();
    }
}
namespace P3Makoto
{
    public static class ModEntry { public static void Safe(string label, Action a) => a(); }
}
