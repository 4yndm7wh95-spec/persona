using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using HarmonyLib;
using P5Joker;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

string gameData = args.Length > 0 ? args[0] : throw new ArgumentException("Pass the game's data_sts2_windows_x86_64 directory");
AssemblyLoadContext.Default.Resolving += (_, name) => {
 string path = Path.Combine(gameData, name.Name + ".dll");
 return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
};
static void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("PASS: " + label); }

// Deterministic real clip code, no native Godot calls or rendered mock timeline.
ulong now = 1000;
var clock = Stage.NowMs;
Stage.NowMs = () => now;
try {
 var first = new CutInPlayback(1f, crush: true) { Ready = false };
 first.Start(); now += 700;
 Check(Math.Abs(first.Time - .7f) < .0001f, "clip advances on the shared clock");
 var next = new CutInPlayback(2f); next.Start();
 first.Pinch = .65f; next.Pinch = .1f;
 now += 600;
 Check(Math.Abs(first.Time - .999f) < .0001f, "slow poison application holds immediately before crush");
 Check(Math.Abs(next.Time - .6f) < .0001f && first.Pinch == .65f, "overlapping cards keep independent time and pinch");
 first.Ready = true; now += 16;
 Check(first.Time > first.SplitAt, "application completion releases the next-frame crush");
 now -= 100; float stable=first.Time;
 Check(stable < 2f, "a clock rewind cannot underflow into a huge elapsed time");
} finally { Stage.NowMs=clock; }

var original = PoisonDamageContextPatch.TargetMethod();
var before = PatchProcessor.GetOriginalInstructions(original).ToList();
var after = PoisonDamageContextPatch.Transpiler(before, original).ToList();
Check(after.Count(i => i.operand is MethodInfo m && m.DeclaringType == typeof(PoisonDamage)) == 1,
 "exactly one real PoisonPower.Trigger damage call tagged; vanilla Trigger loops retained");
Check(after.Count == before.Count + 2, "only the poison owner argument and wrapper call are added");
var harmony = new Harmony("codex.p5.offline.compatibility");
try {
 foreach (Type t in new[] { typeof(PoisonDamageContextPatch), typeof(OutbreakTogetherPatch), typeof(DamageNumberPatch), typeof(TriggerAnimDelayPatch), typeof(CardPlayedPatch) }) {
  harmony.CreateClassProcessor(t).Patch();
  Console.WriteLine("PASS: Harmony applied " + t.Name);
 }
 var damage = typeof(CreatureCmd).GetMethod(nameof(CreatureCmd.Damage), new[] { typeof(PlayerChoiceContext), typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(CardModel), typeof(CardPlay) })!;
 harmony.Patch(damage, prefix: new HarmonyMethod(typeof(FakeDamage), nameof(FakeDamage.Prefix)));
 var one = (PoisonPower)RuntimeHelpers.GetUninitializedObject(typeof(PoisonPower));
 var two = (PoisonPower)RuntimeHelpers.GetUninitializedObject(typeof(PoisonPower));
 var a = PoisonDamage.Apply(null!, null!, 1, default, null, null, one);
 var b = PoisonDamage.Apply(null!, null!, 2, default, null, null, two);
 Check(FakeDamage.Seen.Count == 2 && ReferenceEquals(FakeDamage.Seen[0],one) && ReferenceEquals(FakeDamage.Seen[1],two),
  "overlapping poison damage calls carry their own exact power instance");
 Check(PoisonDamage.Current.Value == null, "poison context does not leak into the caller");
 FakeDamage.Pending[1].SetResult(Array.Empty<DamageResult>()); await b;
 FakeDamage.Pending[0].SetResult(Array.Empty<DamageResult>()); await a;
 Check(PoisonDamage.Current.Value == null, "out-of-order completion clears poison context");
} finally { harmony.UnpatchAll("codex.p5.offline.compatibility"); }
Console.WriteLine("PASS: compatibility patches removed; no game launched");

public static class FakeDamage {
 public static readonly List<PoisonPower?> Seen = new();
 public static readonly List<TaskCompletionSource<IEnumerable<DamageResult>>> Pending = new();
 public static bool Prefix(ref Task<IEnumerable<DamageResult>> __result) {
  Seen.Add(PoisonDamage.Current.Value);
  var pending = new TaskCompletionSource<IEnumerable<DamageResult>>(); Pending.Add(pending);
  __result=pending.Task; return false;
 }
}
