"""Make the workbench's web copy of a Codex PNG and, for a TV, measure its screen hole.

Usage:  python3 -I docs/p4/workbench/tools/web_art.py <src.png> <out.webp|out.jpg> [--resize WxH] [--hole]

- Cleans the alpha (>= 250 -> 255, <= 3 -> 0, so the soft fringe imagegen leaves does not show as a haze)
  and saves WEBP quality 92 (or JPEG quality 88 without alpha when the output ends in .jpg).
- --resize WxH: scale first (Lanczos), e.g. izanagi_strike was saved at 1024x683.
- --hole: prints the screen hole of a TV image, i.e. the biggest transparent region that does not touch the
  image border (the gaps between the legs, the antenna or a handle are smaller regions and are ignored), as the
  numbers TVART in p4r_workbench.html uses: hx, hy = centre of the hole's bounding box, hw, hh = its size, all in
  source pixels. drawTVArt(c, key, cx, cy, sw) then scales the whole image so the hole is sw wide and centred
  on (cx, cy); the screen content is drawn first and the TV image on top of it.
Put the output in docs/p4/workbench/gen/ and publish the workbench with it in `files` ("gen/<name>": path).
"""
import sys
from collections import deque

import numpy as np
from PIL import Image


def clean(im):
    a = np.asarray(im.convert('RGBA')).copy()
    al = a[..., 3]
    al[al >= 250] = 255
    al[al <= 3] = 0
    a[..., 3] = al
    return Image.fromarray(a, 'RGBA')


def screen_hole(im):
    tr = np.asarray(im.convert('RGBA'))[..., 3] < 128
    H, W = tr.shape
    lab = np.zeros((H, W), np.int32)
    best = None   # (pixel count, x0, y0, x1, y1)
    n = 0
    for y0 in range(H):
        row = np.nonzero(tr[y0] & (lab[y0] == 0))[0]
        for x0 in row:
            if lab[y0, x0]:
                continue
            n += 1
            lab[y0, x0] = n
            dq = deque([(y0, x0)])
            cnt, border = 0, False
            xa, xb, ya, yb = x0, x0, y0, y0
            while dq:
                y, x = dq.popleft()
                cnt += 1
                if x < xa: xa = x
                if x > xb: xb = x
                if y < ya: ya = y
                if y > yb: yb = y
                if x == 0 or y == 0 or x == W - 1 or y == H - 1:
                    border = True
                for yy, xx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                    if 0 <= yy < H and 0 <= xx < W and tr[yy, xx] and not lab[yy, xx]:
                        lab[yy, xx] = n
                        dq.append((yy, xx))
            if not border and (best is None or cnt > best[0]):
                best = (cnt, xa, ya, xb, yb)
    if best is None:
        return None
    cnt, x0, y0, x1, y1 = best
    return dict(hx=(x0 + x1) / 2, hy=(y0 + y1) / 2, hw=x1 - x0 + 1, hh=y1 - y0 + 1, px=cnt, size=(W, H))


def main(argv):
    if len(argv) < 2:
        print(__doc__)
        return 1
    src, out = argv[0], argv[1]
    resize = None
    if '--resize' in argv:
        w, h = argv[argv.index('--resize') + 1].lower().split('x')
        resize = (int(w), int(h))
    im = clean(Image.open(src))
    if '--hole' in argv:
        hole = screen_hole(im)
        print('screen hole:', hole)
        if hole:
            print('TVART entry: {{ hx: {hx}, hy: {hy}, hw: {hw}, hh: {hh} }}  (aspect {a:.3f})'.format(a=hole['hw'] / hole['hh'], **hole))
    if resize:
        im = im.resize(resize, Image.LANCZOS)
    if out.lower().endswith(('.jpg', '.jpeg')):
        im.convert('RGB').save(out, 'JPEG', quality=88)
    else:
        im.save(out, 'WEBP', quality=92, method=6)
    print('saved', out)
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
