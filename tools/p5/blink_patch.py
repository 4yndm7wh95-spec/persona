"""Build the P5 Joker blink frame from idle_a3 + only the eyes of a generated closed-eye frame.

The generated idle_blink_a.png (2026-10-27) closed the eyes but redrew the whole picture soft, with
dark fringes and a glow round the red gloves. So the blink frame is idle_a3 with only the two eye
areas copied in (feathered ellipses, skin tone matched to idle). Writes the full frame back to
art/p5/poses/idle_blink_a.png (the raw generation is kept as idle_blink_a_raw.png) and the small
patch art/p5/poses/idle_blink_patch.png + .json (its position on idle_a3) for the rig.
    python tools/p5/blink_patch.py
"""
import json
from pathlib import Path
import numpy as np
from PIL import Image, ImageFilter

P = Path(__file__).resolve().parents[2] / 'art/p5/poses'
# (cx, cy, rx, ry) on the 1024x1536 source canvas: near (visible) eye, far eye beside the hair
EYES = [(577, 135, 34, 20), (614, 146, 13, 11)]
FEATHER = 4


def main():
    raw = P / 'idle_blink_a_raw.png'
    if not raw.exists():
        (P / 'idle_blink_a.png').rename(raw)
    idle = np.array(Image.open(P / 'idle_a3.png').convert('RGBA')).astype(float)
    gen = np.array(Image.open(raw).convert('RGBA')).astype(float)
    m = Image.new('L', (idle.shape[1], idle.shape[0]), 0)
    from PIL import ImageDraw
    d = ImageDraw.Draw(m)
    for cx, cy, rx, ry in EYES:
        d.ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=255)
    m = np.array(m.filter(ImageFilter.GaussianBlur(FEATHER))).astype(float) / 255
    # match the generated skin tone to idle in a ring around the eyes
    ring = (m > 0.02) & (m < 0.6)
    skin = ring & (idle[..., 0] > 200) & (idle[..., 1] > 160)
    gain = idle[skin][:, :3].mean(0) / np.maximum(gen[skin][:, :3].mean(0), 1)
    gen[..., :3] = np.clip(gen[..., :3] * gain, 0, 255)
    out = idle.copy()
    out[..., :3] = idle[..., :3] * (1 - m[..., None]) + gen[..., :3] * m[..., None]
    Image.fromarray(out.astype(np.uint8)).save(P / 'idle_blink_a.png')
    ys, xs = np.nonzero(m > 0.01)
    x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
    patch = out[y0:y1, x0:x1].copy()
    patch[..., 3] = np.minimum(patch[..., 3], m[y0:y1, x0:x1] * 255)
    Image.fromarray(patch.astype(np.uint8)).save(P / 'idle_blink_patch.png')
    (P / 'idle_blink_patch.json').write_text(json.dumps({'base': 'idle_a3', 'x': int(x0), 'y': int(y0),
                                                         'w': int(x1 - x0), 'h': int(y1 - y0)}, indent=1))
    print('patch', x0, y0, x1 - x0, y1 - y0, 'skin gain', gain.round(3))


if __name__ == '__main__':
    main()
