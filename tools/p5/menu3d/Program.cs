using GFDLibrary;
using GFDLibrary.Animations;
using GFDLibrary.Models;
using System.Numerics;
using System.Text.Json;

if (args.Length != 2) throw new ArgumentException("Usage: Export <original source directory> <scratch directory>");
string stem = Path.Combine(args[0], "MENU_C0001_001_A");
var model = Resource.Load<ModelPack>(stem + ".GMD");
var pack = Resource.Load<AnimationPack>(stem + ".GAP");
if (pack.ErrorsOccuredDuringLoad || model.AnimationPack?.ErrorsOccuredDuringLoad == true)
    throw new InvalidDataException("ErrorsOccuredDuringLoad");
Console.WriteLine($"nodes={model.Model.Nodes.Count()} main={pack.Animations.Count} blend={pack.BlendAnimations.Count}");
Directory.CreateDirectory(args[1]);
var nodes = model.Model.Nodes.ToList();
var initial = nodes.Select(n => (n.Translation, n.Rotation, n.Scale)).ToArray();
var meshes = nodes.SelectMany(n => n.Meshes.Select(m => (Node: n, Mesh: m))).ToArray();
var cameras = nodes.Where(n => n.Attachments.Any(a => a.GetType().Name.Contains("Camera")) || n.Name.Contains("camera", StringComparison.OrdinalIgnoreCase)).ToArray();
Console.WriteLine($"camera nodes: {string.Join(", ", cameras.Select(n => n.Name))}");
if (cameras.Length != 0) throw new InvalidDataException("Native camera needs export");
var meshInfo = meshes.Select(item =>
{
    var mesh = item.Mesh;
    var mat = model.Materials[mesh.MaterialName];
    Vector4 color = mat.LegacyParameters?.DiffuseColor ?? Vector4.One;
    return new {
        name = item.Node.Name, material = mesh.MaterialName, vertices = mesh.VertexCount,
        texture = mat.DiffuseMap?.Name, color = new[] {color.X, color.Y, color.Z, color.W},
        cull = mat.DisableBackfaceCulling == 0, draw = mat.DrawMethod.ToString(),
        clip = mat.AlphaClip, clip_mode = mat.AlphaClipMode.ToString(),
        uv = mesh.TexCoordsChannel0?.Select(v => new[] {v.X, v.Y}),
        triangles = mesh.Triangles.SelectMany(t => new[] {(int)t.A, (int)t.B, (int)t.C})
    };
}).ToArray();
File.WriteAllText(Path.Combine(args[1], "meshes.json"), JsonSerializer.Serialize(meshInfo));
var manifest = new List<object>();
foreach (var (group, clips) in new[] { ("main", pack.Animations), ("blend", pack.BlendAnimations) })
    for (int index = 0; index < clips.Count; index++)
    {
        var clip = clips[index];
        if (clip.Duration == 0 && clip.Controllers.Count == 0) continue;
        string name = $"{group}_{index:D4}";
        int end = (int)MathF.Ceiling(clip.Duration * 30 - 0.0001f);
        int[] frames = (group == "main" ? index >= 100 : index == 1)
            ? new[] {0, end / 2, end} : Enumerable.Range(0, end + 1).ToArray();
        var tracks = clip.Controllers.Select(controller =>
        {
            if (controller.TargetKind != TargetKind.Node) throw new InvalidDataException($"Non-node track: {name}");
            var target = controller.TargetId < nodes.Count && nodes[(int)controller.TargetId].Name == controller.TargetName
                ? nodes[(int)controller.TargetId] : nodes.FirstOrDefault(n => n.Name == controller.TargetName);
            var keys = controller.Layers.SelectMany(layer =>
            {
                if (layer.Keys.Any(k => k is not PRSKey)) throw new InvalidDataException($"Non-PRS layer: {name}");
                return layer.Keys.Cast<PRSKey>().Select(k => (key: k, layer.PositionScale, layer.ScaleScale));
            }).OrderBy(k => k.key.Time).ToArray();
            return (target,
                position: keys.Where(k => k.key.HasPosition).Select(k => (k.key.Time, k.key.Position * k.PositionScale)).ToArray(),
                rotation: keys.Where(k => k.key.HasRotation).Select(k => (k.key.Time, Quaternion.Normalize(k.key.Rotation))).ToArray(),
                scale: keys.Where(k => k.key.HasScale).Select(k => (k.key.Time, k.key.Scale * k.ScaleScale)).ToArray());
        }).ToArray();
        int unmapped = tracks.Count(t => t.target == null);
        foreach (int frame in frames)
        {
            float time = Math.Min(frame / 30f, clip.Duration);
            for (int i = 0; i < nodes.Count; i++)
                (nodes[i].Translation, nodes[i].Rotation, nodes[i].Scale) = initial[i];
            foreach (var track in tracks)
            {
                if (track.target == null) continue;
                var node = track.target;
                node.Translation = Sample(track.position, time, node.Translation, Vector3.Lerp);
                node.Rotation = Sample(track.rotation, time, node.Rotation, Quaternion.Slerp);
                node.Scale = Sample(track.scale, time, node.Scale, Vector3.Lerp);
            }
            using var writer = new BinaryWriter(File.Create(Path.Combine(args[1], $"{name}_{frame:D4}.bin")));
            foreach (var (node, mesh) in meshes)
            {
                var (positions, _) = mesh.Transform(node, nodes, model.Model.Bones, includeWeights: true, includeParentTransform: true);
                foreach (var v in positions)
                {
                    writer.Write(v.X);
                    writer.Write(v.Y);
                    writer.Write(v.Z);
                }
            }
        }
        manifest.Add(new { name, duration = clip.Duration, speed = clip.Speed, frames, unmapped });
        Console.WriteLine($"{name} {clip.Duration:R} {clip.Speed} frames={frames.Length} unmapped={unmapped}");
    }
File.WriteAllText(Path.Combine(args[1], "clips.json"), JsonSerializer.Serialize(manifest));

static T Sample<T>((float Time, T Value)[] keys, float time, T fallback, Func<T, T, float, T> mix)
{
    if (keys.Length == 0) return fallback;
    int next = Array.FindIndex(keys, k => k.Time > time);
    if (next < 0) return keys[^1].Value;
    if (next == 0) return keys[0].Value;
    var a = keys[next - 1];
    var b = keys[next];
    return mix(a.Value, b.Value, (time - a.Time) / (b.Time - a.Time));
}
