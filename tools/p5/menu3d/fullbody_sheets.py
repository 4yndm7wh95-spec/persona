"""Plan and assemble the full-body menu pose survey from baked vertices."""
import argparse
import json
from math import sqrt
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[3]
SCRATCH = Path(__file__).resolve().parent / "scratch"
OUTPUT = ROOT / "research/p5r/menu3d_pages/sheets/all_clips_fullbody.png"


def read_plan():
    return json.loads((SCRATCH / "fullbody.json").read_text())


def plan():
    specs = json.loads((SCRATCH / "meshes.json").read_text())
    body = next(i for i, spec in enumerate(specs) if spec["name"] == "c0001_body_051_UI_main")
    start = sum(spec["vertices"] for spec in specs[:body])
    end = start + specs[body]["vertices"]
    clips = json.loads((SCRATCH / "clips.json").read_text())
    views = []
    for clip in clips:
        name, frames = clip["name"], clip["frames"]
        if not name.startswith(("main_", "blend_")):
            continue
        if clip["duration"] >= 1.0:
            frames = sorted(set(frames[::5] + frames[-1:]))
        for frame in frames:
            source = SCRATCH / f"{name}_{frame:04d}.bin"
            vertices = np.fromfile(source, dtype="<f4").reshape(-1, 3)[start:end]
            low, high = vertices.min(axis=0), vertices.max(axis=0)
            distance = max(190.0, float(max(high - low)) * 1.78)
            position = (low + high) / 2 + np.array([0.5, 0, sqrt(0.75)]) * distance
            views.append({
                "folder": "tracked", "clips": [name], "frames": {name: [frame]},
                "draft": True, "position": position.tolist(), "rotation_degrees": [0, 30, 0], "fov": 42,
            })
    (SCRATCH / "fullbody.json").write_text(json.dumps(views), encoding="utf-8")


def sheet():
    rows = {}
    for view in read_plan():
        name, = view["clips"]
        rows.setdefault(name, []).extend(view["frames"][name])
    width, height, label = 96, 108, 114
    size = (label + max(map(len, rows.values())) * width, len(rows) * height)
    canvas = Image.new("RGB", size, "#314055")
    draw = ImageDraw.Draw(canvas)
    for i, (name, frames) in enumerate(rows.items()):
        y = i * height
        draw.text((5, y + 4), name, fill="white")
        for j, frame in enumerate(frames):
            file = SCRATCH / f"tracked/{name}/f{frame:04d}.png"
            with Image.open(file) as source:
                source = source.convert("RGBA")
                box = source.getbbox()
                if box is None:
                    raise ValueError(f"No visible pixels: {file}")
                crop = source.crop((min(128, box[0]), 0, max(256, box[2]), source.height))
                crop.thumbnail((width, height - 12), Image.Resampling.LANCZOS)
                x = label + j * width
                canvas.paste(crop, (x + (width - crop.width) // 2, y), crop)
                draw.text((x + 3, y + height - 12), f"f{frame:04d}", fill="white")
    canvas.save(OUTPUT, optimize=True)
    print(f"{len(rows)} clips, {sum(map(len, rows.values()))} frames")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("plan", "sheet"))
    args = parser.parse_args()
    (plan if args.action == "plan" else sheet)()
