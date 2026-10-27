"""Find the jumps in a P5 Joker animation: which consecutive frames change too much.

The user's rule (2026-10-26): animations must not jump - add in-between frames until every step is
small. This measures each step of a sequence (images from art/p5/poses/final/) and flags the ones
that need another in-between frame:
    python tools/p5/sequence_check.py idle_a3 gun_draw_a gun_raise_a2 gun_a2
Per step it prints: silhouette overlap (IoU of the alpha masks; 1.0 = identical outline), how far
the face moved and how far each foot moved, in pixels on the 1152x1536 final canvas.
A step JUMPS when overlap < MIN_IOU, the face moves more than MAX_FACE px or a foot moves more than
MAX_FOOT px; add a frame there.
It also writes art/p5/poses/seq_<first>_<last>.png: each step as a red (from) / blue (to) overlay.
Smear / impact frames are meant to be sudden; pass --allow name to skip the steps touching them.
"""
import sys
from pathlib import Path
import numpy as np
from PIL import Image
from scipy import ndimage

sys.path.insert(0, str(Path(__file__).resolve().parent))
from normalize_poses import boots  # noqa: E402

F = Path(__file__).resolve().parents[2] / 'art/p5/poses/final'
MIN_IOU = 0.62
MAX_FACE = 90
MAX_FOOT = 120  # a planted foot sliding further than this in one frame reads as a skate / jump


def face(a):
    r, g, b, al = (a[..., i] for i in range(4))
    skin = (al > 200) & (r > 215) & (g > 170) & (g < 230) & (b > 140) & (b < 210) & (r - b > 30)
    skin[700:] = False
    lab, k = ndimage.label(skin)
    i = int(np.argmax(ndimage.sum(skin, lab, range(1, k + 1)))) + 1
    ys, xs = np.nonzero(lab == i)
    return np.array([xs.mean(), ys.mean()])


def main():
    args = sys.argv[1:]
    allow = set()
    while '--allow' in args:
        i = args.index('--allow')
        allow.add(args[i + 1])
        del args[i:i + 2]
    ims = [Image.open(F / f'{n}.png').convert('RGBA') for n in args]
    arrs = [np.array(im).astype(int) for im in ims]
    masks = [a[..., 3] > 128 for a in arrs]
    jumps = 0
    tiles = []
    for i in range(len(args) - 1):
        a, b = arrs[i], arrs[i + 1]
        iou = (masks[i] & masks[i + 1]).sum() / (masks[i] | masks[i + 1]).sum()
        fm = float(np.hypot(*(face(b) - face(a))))
        feet = [int(np.hypot(p[0] - q[0], p[1] - q[1])) for p, q in zip(boots(a), boots(b))]
        skip = args[i] in allow or args[i + 1] in allow
        jump = (iou < MIN_IOU or fm > MAX_FACE or max(feet) > MAX_FOOT) and not skip
        jumps += jump
        tag = 'JUMP - add a frame' if jump else ('(allowed)' if skip else 'ok')
        print(f'{args[i]:>16s} -> {args[i + 1]:<16s} overlap {iou:.2f}  face moved {fm:5.0f}  feet moved {feet}  {tag}')
        over = Image.new('RGB', ims[i].size, (255, 255, 255))
        for m, c in ((masks[i], (255, 60, 60)), (masks[i + 1], (60, 60, 255))):
            over.paste(Image.new('RGB', over.size, c), (0, 0), Image.fromarray((m * 110).astype(np.uint8)))
        tiles.append(over.resize((288, 384)))
    sheet = Image.new('RGB', (288 * len(tiles), 384), 'white')
    for i, t in enumerate(tiles):
        sheet.paste(t, (i * 288, 0))
    out = F.parent / f'seq_{args[0]}_{args[-1]}.png'
    sheet.save(out)
    print(f'{jumps} jump(s); overlays: {out.relative_to(F.parents[3])}')


if __name__ == '__main__':
    main()
