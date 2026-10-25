using System.Numerics;
using System.Text.Json;
using P3Makoto.Core;

string root = args.Length > 0 ? args[0] : "mod/pck_stage/p3makoto/hero3d/anim";
string[] names = { "CampItemStart", "CampItemLoop", "CampSkillStart", "CampSkillLoop", "CampTopStart", "CampTopLoop", "CommuStart" };
var clips = names.ToDictionary(n => n, n => Load(n));
var boneNames = clips.Values.SelectMany(c => c.Names).Distinct().ToArray();
var lookup = boneNames.Select((n, i) => (n, i)).ToDictionary(v => v.n, v => v.i);
var pose = Enumerable.Repeat(Vector3.One, boneNames.Length).ToArray();
int gun = lookup["Jnt_L_AttachHand01_00"], checks = 0, resets = 0;
void Check(bool ok, string why) { checks++; if (!ok) throw new Exception(why); }
void Apply(Clip clip, BoneScaleChannels? channels, bool last = false)
{
    int[] mapped = clip.Names.Select(n => lookup[n]).ToArray();
    channels?.Enter(clip, mapped);
    int start = last ? clip.Values.Length - clip.Names.Length : 0;
    for (int i = 0; i < mapped.Length; i++) pose[mapped[i]] = clip.Values[start + i];
}

// Reproduce from shipped data, rather than inventing a gun hide track.
Apply(clips["CampItemStart"], null);
Apply(clips["CampSkillStart"], null);
Check(pose[gun].X < .002f, "Legacy ITEM -> SKILL must reproduce the hidden gun");
Check(!clips["CampSkillStart"].Names.Contains("Jnt_L_AttachHand01_00"), "SKILL omits the unit-scale gun track");
Array.Fill(pose, Vector3.One);
var state = new BoneScaleChannels(i => { pose[i] = Vector3.One; resets++; });
for (int cycle = 0; cycle < 32; cycle++)
{
    Apply(clips["CampItemStart"], state);
    Check(pose[gun].X < .002f, "ITEM must still intentionally hide its unused gun");
    Apply(clips["CampItemLoop"], state, true);
    Apply(clips["CampSkillStart"], state);
    Check(pose[gun] == Vector3.One, "Switching to SKILL restores the gun to full scale");
    int count = resets;
    Apply(clips["CampSkillStart"], state, true);
    Check(resets == count, "Playback within a clip must not reset valid animated scales");
    Apply(clips["CampSkillLoop"], state);
    Check(pose[gun] == Vector3.One, "Gun remains visible through the SKILL loop transition");
    foreach (string n in new[] { "CampTopStart", "CampTopLoop", "CommuStart" }) Apply(clips[n], state);
}
state.Enter(new object(), new[] { -1 });
state.Enter(new object(), Array.Empty<int>());
Check(true, "Missing bones are safe to skip");
Console.WriteLine($"PASS: {checks} production sparse scale checks using original animation binaries");

Clip Load(string name)
{
    using var j = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, name + ".json")));
    string[] bones = j.RootElement.GetProperty("scale_bones").EnumerateArray().Select(x => x.GetString()!).ToArray();
    byte[] bytes = File.ReadAllBytes(Path.Combine(root, name + ".scale.bin"));
    var values = new Vector3[bytes.Length / 12];
    for (int i = 0; i < values.Length; i++) values[i] = new(BitConverter.ToSingle(bytes, i * 12), BitConverter.ToSingle(bytes, i * 12 + 4), BitConverter.ToSingle(bytes, i * 12 + 8));
    return new(bones, values);
}
record Clip(string[] Names, Vector3[] Values);
