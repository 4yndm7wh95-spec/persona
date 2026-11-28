"""Explanation figures for the workbench (numbered pink markers, a label tag, grids), the way the "this round" section uses them.

Usage (run from anywhere; images in, JPEG out):
  python3 -I annotate_figs.py mark <in.png> <out.jpg> "x,y,r;x,y,r;..."   numbered markers at fractions of the image (r = circle radius
                                                                          as a fraction of the width; leave it out for a bare marker)
  python3 -I annotate_figs.py tag  <in.png> <out.jpg> "<label>"           a pink label in the top-left corner (Chinese is fine)
  python3 -I annotate_figs.py grid <out.jpg> <cols> <width> <img...>      tile images (each scaled to <width> px) in <cols> columns
  python3 -I annotate_figs.py zoom <in.png> <out.jpg> x0,y0,x1,y1 <factor> ["<label>"]   crop (fractions) and enlarge, pixels kept sharp
Put the results in docs/p4/workbench/ref/ and publish them with the page in `files` ("ref/<name>.jpg": path).
Frames to annotate come from tools/render.js (node render.js <dir> <tab> <times> <prefix> <dur>).
"""
import sys

from PIL import Image, ImageDraw, ImageFont

FONT = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf', 24)
try:
    CJK = ImageFont.truetype('/usr/share/fonts/truetype/wqy/wqy-zenhei.ttc', 24)
except OSError:
    CJK = FONT
PINK = (255, 40, 120)


def mark(im, pts):
    im = im.convert('RGB'); W, H = im.size; d = ImageDraw.Draw(im)
    r = max(14, int(W * 0.017))
    for i, p in enumerate(pts):
        x, y = p[0] * W, p[1] * H
        if len(p) > 2:
            rr = p[2] * W
            d.ellipse([x - rr, y - rr, x + rr, y + rr], outline=PINK, width=max(3, W // 300))
            x, y = x + rr * 0.72, y - rr * 0.72
        d.ellipse([x - r, y - r, x + r, y + r], fill=PINK, outline=(255, 255, 255), width=3)
        t = str(i + 1); tw = d.textlength(t, font=FONT)
        d.text((x - tw / 2, y - r * 0.7), t, fill=(255, 255, 255), font=FONT)
    return im


def tag(im, t):
    im = im.convert('RGB'); d = ImageDraw.Draw(im)
    tw = d.textlength(t, font=CJK)
    d.rectangle([0, 0, tw + 20, 36], fill=PINK); d.text((10, 4), t, fill=(255, 255, 255), font=CJK)
    return im


def grid(ims, cols, w):
    ims = [im.convert('RGB').resize((w, int(im.height * w / im.width)), Image.LANCZOS) for im in ims]
    h = max(i.height for i in ims); rows = (len(ims) + cols - 1) // cols
    g = Image.new('RGB', (cols * w + (cols - 1) * 8, rows * h + (rows - 1) * 8), (20, 20, 22))
    for i, im in enumerate(ims):
        g.paste(im, ((i % cols) * (w + 8), (i // cols) * (h + 8)))
    return g


def save(im, path):
    im.convert('RGB').save(path, quality=84, optimize=True)
    print(path, im.size)


if __name__ == '__main__':
    cmd = sys.argv[1]
    if cmd == 'mark':
        pts = [tuple(float(v) for v in q.split(',')) for q in sys.argv[4].split(';') if q.strip()]
        save(mark(Image.open(sys.argv[2]), pts), sys.argv[3])
    elif cmd == 'tag':
        save(tag(Image.open(sys.argv[2]), sys.argv[4]), sys.argv[3])
    elif cmd == 'grid':
        save(grid([Image.open(f) for f in sys.argv[5:]], int(sys.argv[3]), int(sys.argv[4])), sys.argv[2])
    elif cmd == 'zoom':
        im = Image.open(sys.argv[2]); W, H = im.size
        x0, y0, x1, y1 = (float(v) for v in sys.argv[4].split(','))
        k = int(sys.argv[5]); z = im.crop((int(W * x0), int(H * y0), int(W * x1), int(H * y1)))
        z = z.resize((z.width * k, z.height * k), Image.NEAREST)
        if len(sys.argv) > 6:
            z = tag(z, sys.argv[6])
        save(z, sys.argv[3])
    else:
        sys.exit(__doc__)
