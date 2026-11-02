"""Mix the SFX a preview run logged (out/<scenario>/sfx.txt: "<seconds> <name> <volume>") over its frames into out/<scenario>.mp4.
Sounds come from p5mod/pck_stage/p5joker/sfx/<name>.ogg (run tools/p5/stage_p5.py first). Usage: python3 mix.py <scenario>"""
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
SFX = HERE.parents[2] / "p5mod/pck_stage/p5joker/sfx"
s = sys.argv[1]
events = [l.split() for l in (HERE / "out" / s / "sfx.txt").read_text().splitlines() if l.strip()]
events = [(float(t), n, float(v)) for t, n, v in events if (SFX / f"{n}.ogg").exists()]
cmd = ["ffmpeg", "-loglevel", "error", "-y", "-framerate", "30", "-i", str(HERE / "out" / s / "f%08d.png")]
for _, n, _ in events:
    cmd += ["-i", str(SFX / f"{n}.ogg")]
if events:
    parts = [f"[{i + 1}]adelay={int(t * 1000)}|{int(t * 1000)},volume={v * 0.75:.3f}[a{i}]" for i, (t, n, v) in enumerate(events)]
    mix = "".join(f"[a{i}]" for i in range(len(events))) + f"amix=inputs={len(events)}:normalize=0,alimiter=limit=0.95[a]"
    cmd += ["-filter_complex", ";".join(parts) + ";" + mix, "-map", "0:v", "-map", "[a]", "-c:a", "aac", "-b:a", "160k"]
cmd += ["-c:v", "libx264", "-pix_fmt", "yuv420p", "-vf", "scale=1280:-2", "-shortest" if events else "-an", str(HERE / "out" / f"{s}.mp4")]
cmd = [c for c in cmd if c]
subprocess.run(cmd, check=True)
print(HERE / "out" / f"{s}.mp4", len(events), "sounds")
