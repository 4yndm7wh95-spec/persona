using GFDLibrary;
using GFDLibrary.Animations;
using GFDLibrary.Models;
using System.Numerics;
using System.Text.Json;

if (args.Length is < 2 or > 3) throw new ArgumentException("Usage: Export <original source directory> <scratch directory> [--fullbody|mixes.json]");
bool fullBody = args.Length == 3;
MixJob[] mixes = fullBody && args[2] != "--fullbody"
    ? JsonSerializer.Deserialize<MixJob[]>(File.ReadAllText(args[2]), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!
    : [];
var mixInputs = mixes.SelectMany(mix => new[] { mix.Base, mix.Layer }).ToHashSet();
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
var poses = new Dictionary<string, Func<float, Pose[]>>();
var tracksByClip = new Dictionary<string, int[]>();
foreach (var (group, clips) in new[] { ("main", pack.Animations), ("blend", pack.BlendAnimations) })
    for (int index = 0; index < clips.Count; index++)
    {
        var clip = clips[index];
        if (clip.Duration == 0 && clip.Controllers.Count == 0) continue;
        string name = $"{group}_{index:D4}";
        int end = (int)MathF.Ceiling(clip.Duration * 30 - 0.0001f);
        int[] frames = (group == "main" ? index >= 100 : index == 1)
            ? new[] {0, end / 2, end} : Enumerable.Range(0, end + 1).ToArray();
        if (fullBody) frames = Enumerable.Range(0, end + 1).ToArray();
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
        Pose[] GetPose(float time)
        {
            Pose[] result = initial.Select(p => new Pose(p.Translation, p.Rotation, p.Scale)).ToArray();
            foreach (var track in tracks)
            {
                if (track.target == null) continue;
                int i = nodes.IndexOf(track.target);
                Pose bind = result[i];
                result[i] = new(
                    Sample(track.position, time, bind.Position, Vector3.Lerp),
                    Sample(track.rotation, time, bind.Rotation, Quaternion.Slerp),
                    Sample(track.scale, time, bind.Scale, Vector3.Lerp));
            }
            return result;
        }
        if (mixInputs.Contains(name))
        {
            poses.Add(name, frame => GetPose(Math.Clamp(frame / 30f, 0, clip.Duration)));
            tracksByClip.Add(name, tracks.Where(t => t.target != null).Select(t => nodes.IndexOf(t.target!)).Distinct().ToArray());
        }
        if (group == "blend")
            Console.WriteLine($"{name} flags={clip.Flags}: {string.Join(", ", tracks.Select(t => t.target?.Name ?? "?"))}");
        foreach (int frame in frames)
            WritePose(name, frame, GetPose(Math.Min(frame / 30f, clip.Duration)));
        manifest.Add(new { name, duration = clip.Duration, speed = clip.Speed, frames, unmapped });
        Console.WriteLine($"{name} {clip.Duration:R} {clip.Speed} frames={frames.Length} unmapped={unmapped}");
    }
foreach (MixJob job in mixes)
{
    var frames = Enumerable.Range(0, job.Frames).ToArray();
    Pose[] layerOrigin = poses[job.Layer](0);
    foreach (int frame in frames)
    {
        Pose[] body = poses[job.Base](job.BaseStart + frame);
        Pose[] layer = poses[job.Layer](job.LayerStart + frame);
        float weight = job.Mode == "crossfade"
            ? Math.Clamp((job.StartWeight + frame / (30f * job.Seconds)), 0, 1)
            : job.StartWeight;
        IEnumerable<int> affected = job.Mode == "crossfade" ? Enumerable.Range(0, nodes.Count) : tracksByClip[job.Layer];
        foreach (int i in affected)
        {
            Pose a = body[i], b = layer[i];
            if (job.Mode == "additive")
            {
                Quaternion delta = Quaternion.Normalize(Quaternion.Inverse(layerOrigin[i].Rotation) * b.Rotation);
                body[i] = a with { Rotation = Quaternion.Normalize(a.Rotation * Quaternion.Slerp(Quaternion.Identity, delta, weight)) };
            }
            else body[i] = new(Vector3.Lerp(a.Position, b.Position, weight),
                Quaternion.Slerp(a.Rotation, b.Rotation, weight), Vector3.Lerp(a.Scale, b.Scale, weight));
        }
        WritePose(job.Name, frame, body);
    }
    manifest.Add(new { name = job.Name, duration = (job.Frames - 1) / 30f, speed = 1, frames, unmapped = 0 });
}
File.WriteAllText(Path.Combine(args[1], "clips.json"), JsonSerializer.Serialize(manifest));

void WritePose(string name, int frame, Pose[] pose)
{
    for (int i = 0; i < nodes.Count; i++)
        (nodes[i].Translation, nodes[i].Rotation, nodes[i].Scale) = (pose[i].Position, pose[i].Rotation, pose[i].Scale);
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

readonly record struct Pose(Vector3 Position, Quaternion Rotation, Vector3 Scale);
sealed record MixJob(string Name, string Mode, string Base, int BaseStart, string Layer, int LayerStart,
    float StartWeight, float Seconds, int Frames);
