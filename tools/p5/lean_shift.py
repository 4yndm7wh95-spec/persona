"""Make an in-between where the upper body has moved but the feet stay (used for summon_down1_e).

Generated in-betweens kept moving only the feet (or overshooting), so this shears a frame instead:
rows above y_top move dx px, rows below y_bottom stay, linear in between - the torso leans toward the
rear foot while the boots don't move.
Usage: python tools/p5/lean_shift.py in.png out.png dx [y_top=700] [y_bottom=1150]
summon_down1_e = summon_down1_b with dx=-55.
"""
import sys
from PIL import Image


def main():
    src, out, dx = sys.argv[1], sys.argv[2], int(sys.argv[3])
    y_top = int(sys.argv[4]) if len(sys.argv) > 4 else 700
    y_bot = int(sys.argv[5]) if len(sys.argv) > 5 else 1150
    im = Image.open(src).convert('RGBA')
    W, H = im.size
    res = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    for y in range(0, H, 8):
        t = max(0.0, min(1.0, (y_bot - y) / (y_bot - y_top)))
        d = int(dx * t)
        strip = im.crop((0, y, W, min(H, y + 8)))
        res.alpha_composite(strip, (max(0, d), y), (max(0, -d), 0))
    res.save(out)


if __name__ == '__main__':
    main()
