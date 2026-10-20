using P3Makoto.Core;
using System.Text.Json;
static void Check(bool ok,string what){if(!ok)throw new Exception(what);Console.WriteLine("PASS "+what);}
for(int i=0;i<40;i++){var f=DiveTimeline.At(i/60.0);Check(f.Splash==i/4,"60Hz sample "+i+" -> "+i/4);}
Check(DiveTimeline.At(.053).Splash==0,"old 53ms boundary stays on shape 0");
Check(DiveTimeline.At(.531).Splash==7,"hitch seeks directly to shape 7");
Check(DiveTimeline.At(0).Alpha==1&&DiveTimeline.At(2.0/3).Alpha==0,"full alpha at start; zero at end");
for(int i=6;i<=10;i++){var a=DiveTimeline.At(i/15.0-1e-7).Alpha;var b=DiveTimeline.At(i/15.0+1e-7).Alpha;Check(Math.Abs(a-b)<1e-5,"fade continuous across boundary "+i);}
Check(Math.Abs(DiveTimeline.At(.4).Reveal-1)<1e-6,"reveal complete at sixth shape");
Check(DiveTimeline.At(.2).Reveal==0,"no early global reveal");
Check(DiveTimeline.At(2.5).Warp==75,"In -> Loop sample 75");
Check(Math.Abs(DiveTimeline.At(2.5+209.0/30).Warp-75)<1e-4,"loop wraps to matching first pose");
var frames=Enumerable.Range(0,180).Select(i=>{double t=i/60.0;var s=DiveTimeline.At(t);return new{time=t,s.Splash,s.Alpha,s.MaskNext,s.MaskMix,s.Reveal,s.Warp};});
if (args.Length > 0) File.WriteAllText(args[0],JsonSerializer.Serialize(frames,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine("PASS: 52 production DiveTimeline checks; single-frame cadence, continuous fade, hitch seek, reveal and loop timing");


