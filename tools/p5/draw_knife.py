"""art/p5/props/knife_c.png: Joker's knife drawn in code (the image generator refused a lone knife; the prop is ~35 px
on screen, so a clean cel-shaded drawing in the poses' style is enough). Pointing right, handle on the left;
the spin pivot (middle of the grip) is recorded in knife_c.json.
    python tools/p5/draw_knife.py
"""
import json
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "art/p5/props"
SS = 4                       # supersample
INK, STEEL, SHADE, EDGE, GRIP = (14, 14, 18), (205, 210, 218), (128, 134, 146), (250, 252, 255), (28, 28, 34)


def main():
    # final-canvas pixels (like the poses): grip 80 long, guard, blade 200 long; canvas padded for the outline
    W, H, cy = 330, 60, 30
    im = Image.new("RGBA", (W * SS, H * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    s = lambda pts: [(x * SS, y * SS) for x, y in pts]
    grip = [(14, cy - 8), (92, cy - 9), (92, cy + 9), (14, cy + 8), (8, cy)]
    guard = [(92, cy - 14), (102, cy - 14), (102, cy + 14), (92, cy + 14)]
    blade = [(102, cy - 8), (250, cy - 6), (306, cy + 1), (250, cy + 9), (102, cy + 9)]
    spine = [(102, cy - 8), (250, cy - 6), (306, cy + 1), (102, cy + 1)]
    for poly, fill in ((grip, GRIP), (guard, INK), (blade, STEEL)):
        d.polygon(s(poly), fill=INK)                                     # outline: the same shape grown
    big = im.copy()
    o = 4 * SS                                                           # 4 px ink outline
    out = Image.new("RGBA", im.size, (0, 0, 0, 0))
    for dx in range(-o, o + 1, SS):
        for dy in range(-o, o + 1, SS):
            if dx * dx + dy * dy <= o * o:
                out.alpha_composite(big, (max(0, dx), max(0, dy)), (max(0, -dx), max(0, -dy)))
    d = ImageDraw.Draw(out)
    d.polygon(s(grip), fill=GRIP)
    d.line(s([(20, cy - 3), (86, cy - 4)]), fill=(70, 70, 80), width=2 * SS)       # grip sheen
    d.polygon(s(guard), fill=INK)
    d.polygon(s(blade), fill=STEEL)
    d.polygon(s(spine), fill=SHADE)                                      # cel shade: the back half darker
    d.line(s([(108, cy + 6), (248, cy + 6), (300, cy + 1)]), fill=EDGE, width=2 * SS)   # bright cutting edge
    res = out.resize((W, H), Image.LANCZOS)
    OUT.mkdir(parents=True, exist_ok=True)
    res.save(OUT / "knife_c.png")
    (OUT / "knife_c.json").write_text(json.dumps({"pivot": [53, cy], "tip": [306, cy + 1], "note": "final-canvas px"}, indent=1))
    print(OUT / "knife_c.png", res.size)


if __name__ == "__main__":
    main()
