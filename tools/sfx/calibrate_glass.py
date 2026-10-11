"""把 Sfx.cs 里玻璃层（Snd.GlassOnly）的倍频程分布自动校准到 P3R 原声（research/p3r_audio/glass_ref.json）。
流程：渲染 → 测倍频程能量分布 → 和原声比 → 改 GlassEq → 重复。需要 tools/sfx/preview 工程（渲染 WAV）。
用法：python3 tools/sfx/calibrate_glass.py <预览工程目录> [轮数]"""
import json, re, subprocess, sys, wave
from pathlib import Path
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
SFX = ROOT / "mod/src/Audio/Sfx.cs"
REF = json.load(open(ROOT / "research/p3r_audio/glass_ref.json"))
OCT = REF["oct"]
SR = 44100


def render(proj, out):
    """从 Sfx.cs 生成一个不依赖 Godot 的控制台程序，把每个音效渲染成 WAV。"""
    src = SFX.read_text()
    enum = src[src.index("public enum Snd"):src.index("}", src.index("public enum Snd")) + 1]
    drive = src[src.index("	private static float Drive"):src.index("	private static byte[] Encode")]
    enc = src[src.index("	private static byte[] Encode"):src.index("	// ───────────── 合成器")]
    syn = src[src.index("	// ───────────── 合成器"):src.rindex("}")]
    prog = ("using System; using System.IO;\n" + enum + "\npublic static class Sfx {\n	private const int SR = 44100;\n" + drive + enc + syn + """
	public static void Main(string[] a) {
		foreach (Snd s in Enum.GetValues<Snd>()) {
			byte[] d = Encode(Synth(s), Drive(s));
			using var f = File.Create(Path.Combine(a[0], s + ".wav")); using var w = new BinaryWriter(f);
			w.Write("RIFF"u8.ToArray()); w.Write(36 + d.Length); w.Write("WAVE"u8.ToArray()); w.Write("fmt "u8.ToArray()); w.Write(16); w.Write((short)1); w.Write((short)1);
			w.Write(SR); w.Write(SR * 2); w.Write((short)2); w.Write((short)16); w.Write("data"u8.ToArray()); w.Write(d.Length); w.Write(d);
		}
	}
}""")
    Path(proj).mkdir(parents=True, exist_ok=True)
    (Path(proj) / "Program.cs").write_text(prog)
    (Path(proj) / "sfxprev.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
                                              '<TargetFramework>net8.0</TargetFramework><Nullable>enable</Nullable></PropertyGroup></Project>')
    r = subprocess.run(["dotnet", "run", "-c", "Release", "--", str(out)], cwd=proj, capture_output=True, text=True)
    if r.returncode:
        print(r.stdout[-3000:], r.stderr[-2000:])
        raise SystemExit(1)


def profile(path):
    w = wave.open(str(path))
    x = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16) / 32768
    X = np.abs(np.fft.rfft(x * np.hanning(len(x)))) ** 2
    f = np.fft.rfftfreq(len(x), 1 / SR)
    e = np.array([X[(f >= c / np.sqrt(2)) & (f < c * np.sqrt(2))].sum() for c in OCT])
    d = 10 * np.log10(e + 1e-20)
    return d - d.max()                      # 相对最强频段（原声分布是多段的 dB 平均，不能按"占总能量"比）


def main():
    proj, rounds = sys.argv[1], int(sys.argv[2]) if len(sys.argv) > 2 else 4
    out = ROOT / "art/sfx_preview"
    ref = np.array(REF["profile"])
    ref = ref - ref.max()
    for r in range(rounds):
        render(proj, out)
        ours = profile(out / "GlassOnly.wav")
        src = SFX.read_text()
        m = re.search(r"GlassEq = \{([^}]*)\}", src)
        gains = np.array([float(v.strip().rstrip("f")) for v in m.group(1).split(",")])
        diff = ref - ours
        # 只校准 1kHz 以上：原声 1kHz 以下在各片段间差 15~25dB（有的片段带重击，有的没有），不可靠；低频不动
        hi = np.array(OCT) >= 1000
        diff[~hi] = 0
        gains[~hi] = 0
        gains = np.clip(gains + 0.5 * diff, -8, 8)
        err = np.abs((ref - ours)[hi]).max()
        print(f"第 {r + 1} 轮：最大偏差 {err:.1f} dB  原声 {np.round(ref, 1).tolist()}\n          我们 {np.round(ours, 1).tolist()}")
        new = ", ".join(f"{g:.1f}f" for g in gains)
        SFX.write_text(src[:m.start(1)] + " " + new + " " + src[m.end(1):])
    render(proj, out)
    ours = profile(out / "GlassOnly.wav")
    print("最终：", np.round(ours, 1).tolist())


if __name__ == "__main__":
    main()
