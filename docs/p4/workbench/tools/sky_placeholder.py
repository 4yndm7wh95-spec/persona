"""Placeholder sky-with-clouds picture for the finish card's sky wedge, until Codex paints the real one (task 2i).

Usage:  python3 -I docs/p4/workbench/tools/sky_placeholder.py docs/p4/workbench/gen/sky_clouds.webp

Value-noise fBm, stretched sideways, thresholded softly into cumulus that thicken toward the bottom; lit from above
(white tops, blue-grey undersides) over a deep-blue-to-pale gradient. 1600x600, WEBP quality 86.
"""
import sys

import numpy as np
from PIL import Image, ImageFilter

W, H = 1600, 600
rs = np.random.RandomState(7)


def octave(fx, fy):
    g = rs.rand(fy + 3, fx + 3).astype(np.float32)
    im = Image.fromarray((g * 255).astype(np.uint8), 'L').resize((W + W // fx * 3, H + H // fy * 3), Image.BICUBIC)
    return np.asarray(im, np.float32)[:H, :W] / 255.0


def fbm():
    out = np.zeros((H, W), np.float32)
    amp, tot = 1.0, 0.0
    for o in range(7):
        fx, fy = 6 * 2 ** o, 2 * 2 ** o   # wider than tall
        out += amp * octave(fx, fy)
        tot += amp
        amp *= 0.56
    return out / tot


def smooth(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


n = fbm()
yy = np.linspace(0, 1, H, dtype=np.float32)[:, None]
dens = smooth(0.5, 0.58, n + 0.2 * (yy - 0.5))          # puffy edges, more cloud toward the bottom
blur = np.asarray(Image.fromarray((dens * 255).astype(np.uint8), 'L').filter(ImageFilter.GaussianBlur(10)), np.float32) / 255
d = 14
above = np.vstack([np.repeat(blur[:1], d, 0), blur[:-d]])
light = np.clip(1.0 - 0.55 * above ** 1.6, 0, 1)            # tops lit, undersides in soft shade
sky_top, sky_bot = np.array([22, 104, 205], np.float32), np.array([150, 205, 245], np.float32)
sky = sky_top + (sky_bot - sky_top) * yy[..., None]
shade, white = np.array([172, 190, 218], np.float32), np.array([255, 255, 255], np.float32)
cloud = shade + (white - shade) * light[..., None]
img = sky * (1 - dens[..., None]) + cloud * dens[..., None]
Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), 'RGB').save(sys.argv[1], quality=86)
print('saved', sys.argv[1])
