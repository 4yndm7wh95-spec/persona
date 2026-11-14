using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using P3Makoto.Patches;

AssemblyLoadContext.Default.Resolving += (_, name) => {
 string path=Path.Combine(args[0], name.Name+".dll");
 return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
};
var modern=args[1]=="113";
var damage=DamagePatch.TargetMethods().Single();
if(damage.GetParameters()[1].Name != (modern ? "targetData":"targets")) throw new Exception("Incorrect innermost damage target");
Console.WriteLine("PASS damage target: "+damage);
Creature a=(Creature)RuntimeHelpers.GetUninitializedObject(typeof(Creature));
Creature b=(Creature)RuntimeHelpers.GetUninitializedObject(typeof(Creature));
object values=new[]{a,b};
if(modern){
 var type=damage.GetParameters()[1].ParameterType.GetGenericArguments().Single();
 var array=Array.CreateInstance(type,2);
 array.SetValue(Activator.CreateInstance(type,a),0);
 array.SetValue(Activator.CreateInstance(type,b),1);
 values=array;
}
var read=AccessTools.Method(typeof(DamagePatch),"ReadTargets");
var targets=((IEnumerable<Creature>)read.Invoke(null,new[]{values})!).ToArray();
if(targets.Length!=2 || !ReferenceEquals(a,targets[0]) || !ReferenceEquals(b,targets[1])) throw new Exception("Damage target extraction failed");
Console.WriteLine("PASS: damage target extraction preserves both creatures and their order");
var shop=VelvetShopPatch.TargetMethod();
if(shop.DeclaringType!.Name != (modern?"NStandardMerchantRoom":"NMerchantRoom")) throw new Exception("Shop patch runs on incomplete base initialization");
Console.WriteLine("PASS shop ready target: "+shop.DeclaringType.Name+"."+shop.Name);
string owner="p3makoto.compatibility.probe";
var harmony=new Harmony(owner);
try{
 foreach(var patch in new[]{typeof(DamagePatch),typeof(DamageNumberPatch),typeof(AttackHitVfxPatch),typeof(VelvetShopPatch),typeof(CharSelectInfoPatch)}){
  harmony.CreateClassProcessor(patch).Patch();
  Console.WriteLine("PASS patch installed: "+patch.Name);
 }
 if(Harmony.GetPatchInfo(damage)?.Prefixes.Any(p=>p.owner==owner)!=true) throw new Exception("Missing damage prefix");
 if(Harmony.GetPatchInfo(shop)?.Postfixes.Any(p=>p.owner==owner)!=true) throw new Exception("Missing shop postfix");
 Console.WriteLine("PASS: 5 affected patch classes installed against actual game assemblies; no game rendering started");
}finally{harmony.UnpatchAll(owner);}
