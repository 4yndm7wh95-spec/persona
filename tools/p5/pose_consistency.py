"""Consistency check sheet for P5 Joker pose images: are the new frames the same size as the base?

Generated frames drift in scale and body proportions; switching between them in game makes the figure
pulse. Run this on every new batch BEFORE showing it to the user:
    python tools/p5/pose_consistency.py <base> <new1> [<new2> ...]
e.g. python tools/p5/pose_consistency.py gun_a2 gun_raise_a2 gun_recoil_a2 gun_lower_a2
Names are files in art/p5/poses/ (without .png). It writes art/p5/poses/check_<first new>.png
(all frames side by side at the same scale, with guide lines) and prints, for each frame vs the base:
head height ratio (hair top to chin), boot height / width ratios (boots are rigid, so they show the
scale, but change with the foot angle, so they are for reference) and how far each foot moved.
Body length (chin to sole) more than 2.5% off is marked (check): fine for crouching frames, but for
standing frames it means the body is drawn smaller - normalize_poses.py scales it to match.
FAIL when the head is off by more than 5% - redraw that frame from the base image, do not rescale
it by hand. Small leftover scale / foot offsets are fixed afterwards by normalize_poses.py.
If a hand covers the face (the chin can't be found), pass --occluded <name> for that frame: it is then
compared by overall standing height instead (only valid when both frames stand upright).
"""
import sys
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, str(Path(__file__).resolve().parent))
from normalize_poses import boots  # noqa: E402

P = Path(__file__).resolve().parents[2] / 'art/p5/poses'
HEAD_TOL = 0.05
BODY_TOL = 0.025


def boot_dims(a):
    from scipy import ndimage
    r, g, b, al = (a[..., i] for i in range(4))
    m = (al > 200) & (r > 50) & (r - b > 25) & (g < 110) & (r < 200) & (r > g + 15)
    m[:1100] = False
    m = ndimage.binary_closing(m, iterations=3)
    lab, k = ndimage.label(m)
    sz = ndimage.sum(m, lab, range(1, k + 1))
    dims = []
    for i in np.argsort(sz)[::-1][:2] + 1:
        ys, xs = np.nonzero(lab == i)
        dims.append((xs.mean(), np.ptp(ys), np.ptp(xs)))
    dims.sort()
    return [(h, w) for _, h, w in dims]


def head_height(a):
    """Hair top to chin, measured in a band around the face (raised arms beside the head are excluded)."""
    from scipy import ndimage
    r, g, b, al = (a[..., i] for i in range(4))
    skin = (al > 200) & (r > 215) & (g > 170) & (g < 230) & (b > 140) & (b < 210) & (r - b > 30)
    skin[700:] = False
    lab, k = ndimage.label(skin)
    i = int(np.argmax(ndimage.sum(skin, lab, range(1, k + 1)))) + 1
    ys, xs = np.nonzero(lab == i)
    cx = int(xs.mean())
    band = al[:, max(0, cx - 120):cx + 120]
    return ys.max() - np.nonzero(band.max(1) > 128)[0].min()


def body_length(a):
    """Chin to the lowest sole: the body below the head. A frame can match the head and still be short."""
    from scipy import ndimage
    r, g, b, al = (a[..., i] for i in range(4))
    skin = (al > 200) & (r > 215) & (g > 170) & (g < 230) & (b > 140) & (b < 210) & (r - b > 30)
    skin[700:] = False
    lab, k = ndimage.label(skin)
    i = int(np.argmax(ndimage.sum(skin, lab, range(1, k + 1)))) + 1
    chin = np.nonzero(lab == i)[0].max()
    return np.nonzero(al.max(1) > 128)[0].max() - chin


def standing_height(a):
    al = a[..., 3]
    ys = np.nonzero(al.max(1) > 128)[0]
    return ys.max() - ys.min()


def main():
    names = sys.argv[1:]
    # --occluded <name>: a hand covers that face (e.g. the summon mask pose), so the chin can't be found.
    # For those frames the head / body checks are replaced by the overall standing height (hair top to
    # sole), which only makes sense when both frames stand upright.
    occluded = set()
    while '--occluded' in names:
        i = names.index('--occluded')
        occluded.add(names[i + 1])
        del names[i:i + 2]
    ims = [Image.open(P / f'{n}.png').convert('RGBA') for n in names]
    arrs = [np.array(im).astype(int) for im in ims]
    base_b, base_f, base_h, base_l = boot_dims(arrs[0]), boots(arrs[0]), head_height(arrs[0]), body_length(arrs[0])
    ok = True
    for n, a in zip(names[1:], arrs[1:]):
        bd, f = boot_dims(a), boots(a)
        hr = np.mean([x[0] / y[0] for x, y in zip(bd, base_b)])
        wr = np.mean([x[1] / y[1] for x, y in zip(bd, base_b)])
        if n in occluded:
            sh = standing_height(a) / standing_height(arrs[0])
            moves = [int(np.hypot(x[0] - y[0], x[1] - y[1])) for x, y in zip(f, base_f)]
            bad = abs(sh - 1) > HEAD_TOL
            ok &= not bad
            print(f"{n:18s} (face covered) standing height x{sh:.3f}  boot height x{hr:.3f}  boot width x{wr:.3f}  feet moved {moves} px  {'FAIL' if bad else 'ok'}")
            continue
        hh = head_height(a) / base_h
        bl = body_length(a) / base_l
        moves = [int(np.hypot(x[0] - y[0], x[1] - y[1])) for x, y in zip(f, base_f)]
        # boots change with the foot angle, so they are shown for reference only; body length only counts
        # for standing frames (crouches / kneels are legitimately shorter), so it is a warning, not a FAIL
        bad = abs(hh - 1) > HEAD_TOL
        warn = abs(bl - 1) > BODY_TOL
        ok &= not bad
        print(f"{n:18s} head x{hh:.3f}  body x{bl:.3f}{' (check)' if warn else ''}  boot height x{hr:.3f}  boot width x{wr:.3f}  feet moved {moves} px  {'FAIL' if bad else 'ok'}")
    w = 360
    sheet = Image.new('RGBA', (w * len(ims), 540 + 24), (210, 210, 210, 255))
    d = ImageDraw.Draw(sheet)
    for i, (n, im) in enumerate(zip(names, ims)):
        sheet.alpha_composite(im.resize((w, 540)), (i * w, 0))
        d.text((i * w + 6, 544), n, fill=(0, 0, 0, 255))
    for y in range(0, 540, 27):
        d.line([(0, y), (sheet.width, y)], fill=(130, 130, 255, 255))
    out = P / f'check_{names[1]}.png'
    sheet.convert('RGB').save(out)
    print('sheet:', out.relative_to(P.parents[2]), '| overall:', 'ok' if ok else 'FAIL')


if __name__ == '__main__':
    main()
