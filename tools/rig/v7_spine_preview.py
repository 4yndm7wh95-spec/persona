"""用 spine_eval 直接渲染 art/rig/v7 的 Spine 数据（验证骨骼文件本身），按游戏里的出牌顺序串起来出预览视频。
输出到 scratchpad 或命令行给的目录（预览视频不进仓库）。"""
import json
import subprocess
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(Path(__file__).resolve().parent))
from spine_eval import Skeleton  # noqa: E402

V7 = ROOT / "art/rig/v7"
FPS = 30


def images():
    imgs, page, cur = {}, None, None
    for line in (V7 / "regent.atlas").read_text().split("\n"):
        line = line.strip()
        if not line:
            continue
        if line.endswith(".png"):
            page = Image.open(V7 / line).convert("RGBA")
        elif line.startswith(("size:", "filter:", "pma:", "rotate:")):
            continue
        elif line.startswith("bounds:"):
            x, y, w, h = map(int, line[7:].split(","))
            imgs[cur] = page.crop((x, y, x + w, y + h))
        else:
            cur = line
    return imgs


PLAYLISTS = {
    "summon": [("idle_loop", 0.6), ("aim_in", None), ("aim_loop", 0.5), ("sovereign_from_aim", None), ("idle_loop", 0.8)],
    "cast": [("idle_loop", 0.5), ("attack_sovereign", None), ("idle_loop", 0.6)],
    "light": [("idle_loop", 0.5), ("cast_light", None), ("idle_loop", 2.4)],
    "sword": [("idle_loop", 0.5), ("sword_in", None), ("ready_sword_loop", 0.6), ("attack_from_ready", None), ("idle_loop", 0.5),
              ("attack", None), ("idle_loop", 0.5)],
    "hurt": [("idle_loop", 0.4), ("hurt", None), ("idle_loop", 0.6), ("die", None)],
}


def main(out_dir, which):
    data = json.loads((V7 / "regent.spine-json").read_text())
    sk = Skeleton(data, images())
    out_dir = Path(out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)
    for name in which:
        frames = []
        for anim, length in PLAYLISTS[name]:
            dur = length or Skeleton_duration(data["animations"][anim])
            n = int(round(dur * FPS))
            for k in range(n):
                im = sk.render(anim, k / FPS, canvas=(1400, 1600), origin=(560, 1540), bg=(23, 31, 56, 255)).convert("RGB")
                frames.append(im.resize((700, 800), Image.LANCZOS))
        d = out_dir / f"v7_{name}"
        d.mkdir(exist_ok=True)
        for i, f in enumerate(frames):
            f.save(d / f"{i:04d}.png")
        mp4 = out_dir / f"v7_{name}.mp4"
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-framerate", str(FPS), "-i", str(d / "%04d.png"),
                        "-pix_fmt", "yuv420p", "-c:v", "libx264", "-crf", "18", str(mp4)], check=True)
        print(mp4, len(frames))


def Skeleton_duration(a):
    m = 0.0

    def scan(x):
        nonlocal m
        if isinstance(x, dict):
            if "time" in x:
                m = max(m, x["time"])
            for v in x.values():
                scan(v)
        elif isinstance(x, list):
            for v in x:
                scan(v)
    scan(a)
    return m


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2:] or list(PLAYLISTS))
