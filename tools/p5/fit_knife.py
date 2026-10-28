"""art/p5/props/knife_d.png: the generated knife (knife_gen_raw.png, user 2026-10-28) re-proportioned to the knife in
the poses - its blade is ~2.4x the grip there, ~1.4x in the generated one. Only straight stretches are resized, so
the pommel, guard and tip keep their shapes: the grip's straight middle is shortened, the blade's even part (before
the tip taper) lengthened. Columns in the raw image: grip 58-505, guard 505-585, blade 585-1202.
    python tools/p5/fit_knife.py [blade_scale] [grip_scale]
"""
import json
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SRC, OUT = ROOT / "art/p5/props/knife_gen_raw.png", ROOT / "art/p5/props"
GRIP_MID = (200, 400)        # straight part of the grip
BLADE_MID = (620, 990)       # even part of the blade, before the tip


def main():
    blade_k = float(sys.argv[1]) if len(sys.argv) > 1 else 1.7
    grip_k = float(sys.argv[2]) if len(sys.argv) > 2 else 0.6
    im = Image.open(SRC).convert("RGBA")
    box = im.getchannel("A").point(lambda v: 255 if v > 8 else 0).getbbox()
    y0, y1 = box[1] - 4, box[3] + 4
    cuts = [box[0] - 4, *GRIP_MID, *BLADE_MID, box[2] + 4]
    scales = [1, grip_k, 1, blade_k, 1]
    parts = []
    for (a, b), k in zip(zip(cuts, cuts[1:]), scales):
        p = im.crop((a, y0, b, y1))
        parts.append(p if k == 1 else p.resize((round(p.width * k), p.height), Image.LANCZOS))
    out = Image.new("RGBA", (sum(p.width for p in parts), y1 - y0), (0, 0, 0, 0))
    x = 0
    for p in parts:
        out.alpha_composite(p, (x, 0))
        x += p.width
    # the pivot for the spins: middle of the (shortened) grip; the tip: the far right
    grip_end = (GRIP_MID[0] - cuts[0]) + round((GRIP_MID[1] - GRIP_MID[0]) * grip_k) + (505 - GRIP_MID[1])
    pivot = [round(grip_end * 0.55), round((640 - y0))]
    # final-canvas scale: the blade in knife_ready_b is ~205 px long (guard to tip)
    blade_px = out.width - grip_end - 80
    k = 205 / blade_px
    small = out.resize((round(out.width * k), round(out.height * k)), Image.LANCZOS)
    small.save(OUT / "knife_d.png")
    out.save(OUT / "knife_d_full.png")
    meta = {"pivot": [round(pivot[0] * k), round(pivot[1] * k)], "tip": [small.width - 2, round(pivot[1] * k)],
            "blade_scale": blade_k, "grip_scale": grip_k, "note": "final-canvas px"}
    (OUT / "knife_d.json").write_text(json.dumps(meta, indent=1))
    print(OUT / "knife_d.png", small.size, meta)


if __name__ == "__main__":
    main()
