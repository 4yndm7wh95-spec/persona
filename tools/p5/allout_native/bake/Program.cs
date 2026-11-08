using System.Numerics;
using System.Text.Json;
using GFDLibrary;
using GFDLibrary.Animations;
using GFDLibrary.Models;

string root=args[0];
var timeline=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"godot/timeline.json")));
var baked=new List<object>();
int verified=0, bindings=0;
for(int layerIndex=0;layerIndex<6;layerIndex++)
{
 var pack=Resource.Load<ModelPack>(Path.Combine(root,$"nested/layer_{layerIndex:00}.gfs"));
 var nodes=pack.Model.Nodes.ToArray();
 var indices=nodes.Select((n,i)=>(n,i)).ToDictionary(x=>x.n,x=>x.i);
 var parent=nodes.Select(n=>n.Parent==null?-1:indices[n.Parent]).ToArray();
 var animation=pack.AnimationPack?.Animations.FirstOrDefault();
 var controllers=animation?.Controllers.Where(c=>c.TargetKind==TargetKind.Node).ToArray()??Array.Empty<AnimationController>();
 foreach(var c in controllers) {
  if(c.TargetId<0||c.TargetId>=nodes.Length||nodes[c.TargetId].Name!=c.TargetName)
   throw new Exception($"Binding mismatch layer {layerIndex}: {c.TargetId}/{c.TargetName}");
  bindings++;
 }
 var frames=new List<float[][]>();
 var worldSamples=new Dictionary<int,float[][]>();
 float start=timeline.RootElement.GetProperty("layers")[layerIndex].GetProperty("start").GetSingle();
 for(int frame=0;frame<240;frame++) {
  float time=MathF.Max(0,frame/60f-start)*(animation?.Speed??1f);
  var local=new Matrix4x4[nodes.Length];
  for(int i=0;i<nodes.Length;i++) {
   var node=nodes[i]; var pos=node.Translation; var rot=node.Rotation; var scale=node.Scale;
   foreach(var c in controllers.Where(c=>c.TargetId==i))
   foreach(var l in c.Layers) {
    var keys=l.Keys.OfType<PRSKey>().ToArray();
    if(keys.Length==0)continue;
    int k=0;while(k+1<keys.Length&&keys[k+1].Time<=time+0.000001f)k++;
    var a=keys[k];var b=keys[Math.Min(k+1,keys.Length-1)];
    float f=b.Time>a.Time?Math.Clamp((time-a.Time)/(b.Time-a.Time),0,1):0;
    if(a.HasPosition)pos=Vector3.Lerp(a.Position,b.Position,f)*l.PositionScale;
    if(a.HasRotation)rot=Quaternion.Slerp(a.Rotation,b.Rotation,f);
    if(a.HasScale)scale=Vector3.Lerp(a.Scale,b.Scale,f)*l.ScaleScale;
   }
   local[i]=Matrix4x4.CreateFromQuaternion(rot)*Matrix4x4.CreateScale(scale);
   local[i].Translation=pos;
  }
  var world=new Matrix4x4[nodes.Length];
  for(int i=0;i<nodes.Length;i++)world[i]=parent[i]<0?local[i]:local[i]*world[parent[i]];
  frames.Add(local.Select(Matrix).ToArray());
  if(new[]{0,132,180,228,239}.Contains(frame))worldSamples[frame]=world.Select(Matrix).ToArray();
 }
 // Validate transposition and hierarchy against the source's independent bind matrices.
 for(int i=0;i<nodes.Length;i++) {
  Matrix4x4 local=nodes[i].LocalTransform;
  var values=Matrix(local);
  Vector3 p=new(0.37f,-1.23f,2.51f);
  var source=Vector3.Transform(p,local);
  var target=new Vector3(values[0]*p.X+values[3]*p.Y+values[6]*p.Z+values[9],
   values[1]*p.X+values[4]*p.Y+values[7]*p.Z+values[10],
   values[2]*p.X+values[5]*p.Y+values[8]*p.Z+values[11]);
  if(Vector3.Distance(source,target)>0.001f)throw new Exception("Matrix conversion mismatch");
  verified++;
 }
 baked.Add(new{names=nodes.Select(n=>n.Name),parent,frames,worldSamples});
}
File.WriteAllText(Path.Combine(root,"godot/native_matrices.json"),JsonSerializer.Serialize(baked));
Console.WriteLine($"PASS: {bindings} numeric/name bindings, {verified} bind matrix conversions, 240 frames per layer exported.");
static float[] Matrix(Matrix4x4 m)=>new[]{m.M11,m.M12,m.M13,m.M21,m.M22,m.M23,m.M31,m.M32,m.M33,m.M41,m.M42,m.M43};
