using P3Makoto.Core;
using System.Numerics;
using System.Globalization;
CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
var profiles = new[] { ("draw",1480f,500f,370f,400f,.3f,118f), ("discard",1510f,490f,350f,400f,.8f,120f), ("exhaust",1530f,530f,260f,340f,2.6f,100f), ("deck",365f,510f,445f,350f,0f,118f) };
using var csv=new StreamWriter(Path.Combine(AppContext.BaseDirectory, "orbit_samples.csv"));csv.WriteLine("kind,t,x,y,depth");
int checks=0;
void Check(bool ok,string msg) { if(!ok) throw new Exception(msg); checks++; }
foreach(var (name,cx,cy,rx,ry,phase,speed) in profiles) {
 var orbit=new ButterflyOrbit(cx,cy,rx,ry,phase,silhouetteLoop:name=="deck");
 Check(Vector3.Distance(orbit.At(0),orbit.At(orbit.Length))<.01f,name+" loop seam");
 float minY=9999,maxY=-9999,minZ=1,maxZ=-1,fast=0,slow=9999;
 for(int i=0;i<768;i++) {
  float d=orbit.Length*i/768f;var p=orbit.At(d);var next=orbit.At(d+1f);
  minY=Math.Min(minY,p.Y);maxY=Math.Max(maxY,p.Y);minZ=Math.Min(minZ,p.Z);maxZ=Math.Max(maxZ,p.Z);
  Check(p.X>=(name=="deck"?-100:10) && p.X<=1900 && p.Y>=100 && p.Y<=950,name+" viewport bounds");
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

// 牌组的大特写：层切换必须发生在原斜裁轮廓外，近侧飞行和入场要避开脸部（包含蝶翼余量）。
var deck=new ButterflyOrbit(365,510,445,350,0,silhouetteLoop:true);
float FaceClearance(Vector3 p) => MathF.Pow((p.X-410f)/320f,2)+MathF.Pow((p.Y-525f)/300f,2);
int switches=0;var prev=deck.At(.01f);
for(int i=1;i<=24000;i++) {
 var p=deck.At(deck.Length*i/24000f+.01f);
 if(p.Z>=0) Check(FaceClearance(p)>1.05f,"Deck near-side butterfly overlaps expanded face region");
 if((prev.Z>=0)!=(p.Z>=0)) {
  float right=820f-(p.Y-70f)*220f/1030f;
  Check(p.X+70f<0 || p.X-70f>right+5f,"Deck changes plane inside clipped portrait");switches++;
 }
 prev=p;
}
Check(switches==2,"Deck orbit must switch depth exactly twice per lap");
for(int i=0;i<=340;i++) {
 var p=deck.EnterAt(i/200f,118,1.7f,new Vector2(860,340));
 Check(FaceClearance(p)>1.05f,"Deck entrance starts/fades on face");
 Check(p.X>780,"Deck entrance must remain outside the right portrait edge");
}
var end=deck.EnterAt(1.7f,118,1.7f,new Vector2(860,340));
Check(Vector3.Distance(end,deck.At(0))<.01f,"Deck entrance/orbit seam");
var before=deck.EnterAt(1.699f,118,1.7f,new Vector2(860,340));var after=deck.EnterAt(1.701f,118,1.7f,new Vector2(860,340));
Check(Vector2.Distance(new(end.X-before.X,end.Y-before.Y),new(after.X-end.X,after.Y-end.Y))<.002f,"Deck entrance velocity discontinuity");
Console.WriteLine($"PASS deck entrance, expanded-face clearance and two outside-silhouette plane switches; total {checks} checks");
