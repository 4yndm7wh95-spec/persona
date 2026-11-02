using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using GFDLibrary;
using GFDLibrary.Animations;
using GFDLibrary.Models;

// Read only the two original GMD files. No game process, CPK writes, or generated artwork.
if (args.Length != 2) throw new ArgumentException("Usage: Export <BATTLE/CUTIN directory> <output.json>");
string[] sources = { "BCT_P_SUMMON.GMD", "BCT_P_SUMMON_BT.GMD" };
var parts = new List<Part>();
var hashes = new Dictionary<string, string>();
float? duration = null, speed = null;
foreach (string name in sources)
{
    string path = Path.Combine(args[0], name);
    hashes[name] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    ModelPack pack = Resource.Load<ModelPack>(path);
    if (pack.AnimationPack.ErrorsOccuredDuringLoad || pack.AnimationPack.Animations.Count != 1)
        throw new InvalidDataException("Could not completely decode " + name);
    Animation anim = pack.AnimationPack.Animations[0];
    if (duration != null && (duration != anim.Duration || speed != anim.Speed))
        throw new InvalidDataException("Source clocks differ");
    duration = anim.Duration; speed = anim.Speed;
    var tracks = new Dictionary<string, (float[][] Keys, bool Linear)>();
    foreach (AnimationController c in anim.Controllers)
    {
        if (c.TargetKind != TargetKind.Material || c.Layers.Count != 1)
            throw new InvalidDataException("Unsupported controller: " + c);
        AnimationLayer layer = c.Layers[0];
        if (layer.KeyType != KeyType.SingleAlt_2 && layer.KeyType != KeyType.MaterialSingle_4)
            throw new InvalidDataException("Unsupported material track: " + layer.KeyType);
        tracks[c.TargetName] = (layer.Keys.Cast<SingleKey>().Select(k => new[] { k.Time, k.Value }).ToArray(), layer.KeyType == KeyType.MaterialSingle_4);
    }
    foreach (Node node in pack.Model.Nodes)
    foreach (Mesh mesh in node.Meshes)
    {
        var mat = pack.Materials[mesh.MaterialName];
        Vector4 color = mat.LegacyParameters.DiffuseColor;
        Vector3[] world = mesh.Vertices.Select(v => Vector3.Transform(v, node.WorldTransform)).ToArray();
        float[][] vertices = world.Select(v => new[] { MathF.Round(v.X + 640f, 5), MathF.Round(360f - v.Y, 5) }).ToArray();
        var indices = new List<int>();
        foreach (var triangle in mesh.Triangles)
        {
            int a = checked((int)triangle.A), b = checked((int)triangle.B), c = checked((int)triangle.C);
            var va = vertices[a]; var vb = vertices[b]; var vc = vertices[c];
            float area = (vb[0]-va[0])*(vc[1]-va[1])-(vb[1]-va[1])*(vc[0]-va[0]);
            if (MathF.Abs(area) > 0.00001f) indices.AddRange(new[] { a, b, c });
        }
        bool textured = mat.DiffuseMap != null;
        if (textured && mat.DiffuseMap!.Name != "btl_cutin.dds")
            throw new InvalidDataException("Unexpected texture: " + mat.DiffuseMap.Name);
        var track = tracks[mesh.MaterialName];
        parts.Add(new Part(node.Name, mesh.MaterialName, name, world.Average(v => v.Z),
            new[] { color.X, color.Y, color.Z, color.W }, textured, vertices,
            textured ? mesh.TexCoordsChannel0.Select(v => new[] { v.X, v.Y }).ToArray() : Array.Empty<float[]>(),
            indices.ToArray(), track.Keys, track.Linear));
    }
}
var clip = new { sources = sources.Select(s => "BATTLE/CUTIN/" + s), source_sha256 = hashes,
    canvas = new[] {1280,720}, duration, speed, meshes = parts.OrderBy(p => p.Z).ToArray() };
File.WriteAllText(args[1], JsonSerializer.Serialize(clip, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
Console.WriteLine($"Exported {parts.Count} meshes, {parts.Sum(p => p.Indices.Length / 3)} triangles; duration={duration}, native speed={speed}");
record Part(string Name, string Material, string Source, float Z, float[] Color, bool Textured,
    float[][] Vertices, float[][] Uv, int[] Indices, float[][] Keys, bool Linear);
