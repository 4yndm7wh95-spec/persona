using P3Makoto.Core;
using System.Text.Json;
int checks=0;
void Check(bool ok,string what){if(!ok)throw new Exception(what);checks++;Console.WriteLine("PASS "+what);}
// Independent midpoint fixtures from the recovered native sprite table, IDs 140..149.
double[] midpoints={.033333,.083333,.15,.216667,.283333,.35,.416667,.50,.60,.716667};
for(int i=0;i<midpoints.Length;i++)Check(DiveTimeline.At(midpoints[i]).Splash==i,"native sprite "+(140+i));
Check(DiveTimeline.At(0).Splash==10&&DiveTimeline.At(0).Mask==-1,"initial frame hides splash and entry scene");
Check(DiveTimeline.At(.0165).Splash==10&&DiveTimeline.At(.0169).Splash==0,"roundf clock first half-tick boundary");
Check(DiveTimeline.At(.0499).Splash==0&&DiveTimeline.At(.0501).Splash==1,"first sprite boundary");
Check(DiveTimeline.At(.5499).Splash==7&&DiveTimeline.At(.5501).Splash==8,"longer native tail hold");
Check(DiveTimeline.At(.6499).Splash==8&&DiveTimeline.At(.6501).Splash==9,"last native sprite boundary");
Check(DiveTimeline.At(.7832).Alpha==1&&DiveTimeline.At(.7835).Alpha==0,"stop at tick 24 without extra fade");
double[] maskTimes={.033333,.1,.166667,.233333,.283333};
for(int i=0;i<5;i++)Check(DiveTimeline.At(maskTimes[i]).Mask==i&&DiveTimeline.At(maskTimes[i]).Reveal==0,"original mask "+(12+i));
Check(DiveTimeline.At(.3165).Reveal==0&&DiveTimeline.At(.3169).Reveal==1,"entry scene fully shown at tick 10");
Check(DiveTimeline.At(.6).Splash==8&&DiveTimeline.At(1.2).Splash==10,"hitch skips directly to native state");
Check(DiveTimeline.At(-1).Splash==10&&DiveTimeline.At(-1).Warp==0,"negative clock clamps to start");
Check(DiveTimeline.At(2.5).Warp==75,"In to Loop baseline preserved");
Check(Math.Abs(DiveTimeline.At(2.5+209.0/30).Warp-75)<1e-4,"water loop wrap preserved");
var frames=Enumerable.Range(0,180).Select(i=>{double t=i/60.0;var s=DiveTimeline.At(t);return new{time=t,s.Splash,s.Alpha,s.Mask,s.Reveal,s.Warp};});
if(args.Length>0)File.WriteAllText(args[0],JsonSerializer.Serialize(frames,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS: {checks} native timing, mask, hitch, alpha and continuous water checks");
