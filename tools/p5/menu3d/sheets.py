import csv
import json
from pathlib import Path

from PIL import Image, ImageDraw


root = Path(__file__).resolve().parents[3]
output = root / "research/p5r/menu3d"
refs = root / "docs/p5/preview/p5r-20261121-223943-frames-02-12"
clips = json.loads((Path(__file__).parent / "scratch/clips.json").read_text())
with (root / "research/p5r/joker_menu_anim_list.csv").open(encoding="utf-8-sig") as stream:
    catalog = {row["original_name"].split("#")[1]: float(row["duration_seconds"]) for row in csv.DictReader(stream)}
decoded = {f"{group}[{int(index)}]": clip["duration"]
           for clip in clips for group, index in [clip["name"].split("_")]}
assert catalog.keys() == decoded.keys()
assert all(abs(catalog[name] - duration) < 0.000002 for name, duration in decoded.items())


def frame(name, number, size):
    img = Image.open(output / name / f"f{number:04}.png")
    assert img.mode == "RGBA" and img.size == (1920, 1080)
    return img.resize(size, Image.Resampling.LANCZOS)


def reference(number, size):
    path, = refs.glob(f"frame_{number:04}_*.jpg")
    return Image.open(path).convert("RGBA").resize(size, Image.Resampling.LANCZOS)


folder = output / "sheets"
folder.mkdir(exist_ok=True)
sheet = Image.new("RGB", (1120, 190 * len(clips)), "#273448")
labels = ImageDraw.Draw(sheet)
for row, clip in enumerate(clips):
    name, frames = clip["name"], clip["frames"]
    y = row * 190
    labels.text((10, y + 78), f"{name}\n{clip['duration']:.3f} s", fill="white")
    for col, number in enumerate((frames[0], frames[len(frames) // 2], frames[-1])):
        img = frame(name, number, (320, 180))
        sheet.paste(img, (140 + col * 325, y + 5), img)
sheet.save(folder / "all_clips.png")

for label, ref, name, number in (
    ("main", 52, "main_0000", 9),
    ("item", 182, "main_0020", 8),
    ("skill", 113, "main_0015", 5),
    ("equip", 238, "main_0030", 6),
):
    real = reference(ref, (960, 540))
    render = frame(name, number, real.size)
    comparison = Image.new("RGB", (1920, 570), "#273448")
    comparison.paste(real, (0, 0))
    comparison.paste(render, (960, 0), render)
    draw = ImageDraw.Draw(comparison)
    draw.text((10, 548), f"P5R frame {ref:04}", fill="white")
    draw.text((970, 548), f"Candidate {name} / f{number:04}", fill="white")
    comparison.save(folder / f"compare_{label}.png")
    if label == "main":
        render.putalpha(render.getchannel("A").point(lambda a: a // 2))
        Image.alpha_composite(real, render).save(folder / "match_frame_0052.png")

sequence = Image.new("RGB", (1280, 6 * 202), "#273448")
labels = ImageDraw.Draw(sequence)
for index in range(12):
    x, y = index % 2 * 640, index // 2 * 202
    number = 49 + index // 2
    actual = reference(number, (320, 180))
    render = frame("main_0000", index, actual.size)
    sequence.paste(actual, (x, y))
    sequence.paste(render, (x + 320, y), render)
    labels.text((x + 4, y + 182), f"P5R {number:04}", fill="white")
    labels.text((x + 324, y + 182), f"main_0000 / f{index:04}", fill="white")
sequence.save(folder / "compare_main_seq.png")

files = list(output.rglob("*.png"))
assert len(files) == sum(len(c["frames"]) for c in clips) + 7
assert all(p.stat().st_size < 20_000_000 for p in files)
assert sum(p.stat().st_size for p in files) < 400_000_000
print(f"{len(clips)} clips; {len(files)} PNGs; {sum(p.stat().st_size for p in files)} bytes")
