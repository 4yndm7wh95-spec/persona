using P3Makoto.Core;
using System.Numerics;
using System.Globalization;
CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
var profiles = new[] { ("draw",1480f,500f,370f,400f,.3f,118f), ("discard",1510f,490f,350f,400f,.8f,120f), ("exhaust",1530f,530f,260f,340f,2.6f,100f), ("deck",490f,490f,350f,410f,2.2f,118f) };
using var csv=new StreamWriter(Path.Combine(AppContext.BaseDirectory, "orbit_samples.csv"));csv.WriteLine("kind,t,x,y,depth");
int checks=0;
void Check(bool ok,string msg) { if(!ok) throw new Exception(msg); checks++; }
foreach(var (name,cx,cy,rx,ry,phase,speed) in profiles) {
 var orbit=new ButterflyOrbit(cx,cy,rx,ry,phase);
 Check(Vector3.Distance(orbit.At(0),orbit.At(orbit.Length))<.01f,name+" loop seam");
 float minY=9999,maxY=-9999,minZ=1,maxZ=-1,fast=0,slow=9999;
 for(int i=0;i<768;i++) {
  float d=orbit.Length*i/768f;var p=orbit.At(d);var next=orbit.At(d+1f);
  minY=Math.Min(minY,p.Y);maxY=Math.Max(maxY,p.Y);minZ=Math.Min(minZ,p.Z);maxZ=Math.Max(maxZ,p.Z);
  Check(p.X>=10 && p.X<=1900 && p.Y>=100 && p.Y<=950,name+" viewport bounds");
  Check(Vector2.Distance(new(p.X,p.Y),new(next.X,next.Y))>.98f,name+" no cusp or stalled arc");
 }
 Check(maxY-minY>450,name+" vertical travel");Check(maxZ-minZ>1.99f,name+" depth variation");
 foreach(float hz in new[]{30f,60f,144f}) {
  var old=orbit.At(0f);
  for(int i=1;i<=hz*180;i++) {float t=i/hz;var p=orbit.At(ButterflyOrbit.Distance(t,speed));float step=Vector2.Distance(new(p.X,p.Y),new(old.X,old.Y))*hz;fast=Math.Max(fast,step);slow=Math.Min(slow,step);Check(step>speed*.82f&&step<speed*1.18f,name+" speed / wrap / frame continuity");old=p;}
 }
 for(int i=0;i<=600;i++){float t=i*.1f;var p=orbit.At(ButterflyOrbit.Distance(t,speed));csv.WriteLine($"{name},{t},{p.X},{p.Y},{p.Z}");}
 Console.WriteLine($"PASS {name}: orbit {orbit.Length:F1}px, vertical span {maxY-minY:F1}px, speed {slow:F1}..{fast:F1}px/s");
}
Console.WriteLine($"PASS: {checks} trajectory checks (30/60/144 fps, 180s each, wrap/bounds/depth/vertical travel)");
