"""设置界面（P3R 主菜单风格）静态布局预览：坐标与 mod/src/Scenes/SettingsSkin.cs 一致（1920×1080）。
水下背景、立绘融水只做近似（游戏里是着色器）。用法：python3 tools/ui/settings_preview.py <输出.png>"""
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
LAT = str(ROOT / "mod/pck_stage/p3makoto/fonts/BarlowCondensed-900Italic.ttf")
CJK = "/usr/share/fonts/truetype/wqy/wqy-zenhei.ttc"
EDGE = [(1014, 0), (947, 108), (883, 216), (828, 324), (789, 432), (852, 540), (859, 648), (906, 756), (968, 864), (977, 972), (800, 1080)]
W, H = 1920, 1080


def edge_x(y):
    for (x0, y0), (x1, y1) in zip(EDGE, EDGE[1:]):
        if y <= y1:
            return x0 + (x1 - x0) * (y - y0) / max(y1 - y0, 1)
    return EDGE[-1][0]


def water():
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    v = yy / H
    top, mid, bot = np.array([0.20, 0.80, 1.0]), np.array([0.07, 0.40, 0.95]), np.array([0.03, 0.12, 0.55])
    t1 = np.clip(v / 0.38, 0, 1)[..., None]
    t2 = np.clip((v - 0.38) / 0.62, 0, 1)[..., None]
    c = np.where((v < 0.38)[..., None], top * (1 - t1) + mid * t1, mid * (1 - t2) + bot * t2)
    u = xx / H * 1.0
    n = np.sin(u * 8 + np.sin(v * 13) * 1.6) * np.sin(v * 21 - u * 3.2 + np.sin(u * 4.6) * 1.3)
    blob = np.clip((n - 0.3) / 0.42, 0, 1) * np.clip((0.45 - v) / 0.43, 0, 1)
    c = c * (1 - blob[..., None] * 0.8) + np.array([0.78, 0.97, 1.0]) * blob[..., None] * 0.8
    return Image.fromarray((np.clip(c, 0, 1) * 255).astype(np.uint8), "RGB").convert("RGBA")


def portrait():
    im = Image.open(ROOT / "mod/pck_stage/p3makoto/ui/system_portrait.png").convert("RGBA")
    a = np.asarray(im).astype(np.float32) / 255
    mn, mx = a[..., :3].min(2), a[..., :3].max(2)
    white = np.clip((mn - 0.8) / 0.13, 0, 1) * (1 - np.clip((mx - mn - 0.08) / 0.14, 0, 1))
    xs = np.array([edge_x(y) for y in range(H)])[:, None]
    side = np.clip((np.arange(W)[None, :] - xs + 48) / 96, 0, 1)
    a[..., 3] *= 1 - white * side * 0.88
    a[..., :3] = a[..., :3] * (1 - (white * side * 0.55)[..., None]) + np.array([0.66, 0.92, 1.0]) * (white * side * 0.55)[..., None]
    return Image.fromarray((a * 255).astype(np.uint8), "RGBA")


def text_img(s, font, fill, stroke=0, stroke_fill=(0, 0, 0)):
    l, t, r, b = font.getbbox(s, stroke_width=stroke)
    im = Image.new("RGBA", (r - l + 20, b - t + 20))
    ImageDraw.Draw(im).text((10 - l, 10 - t), s, font=font, fill=fill, stroke_width=stroke, stroke_fill=stroke_fill)
    return im


def main(out):
    im = water()
    d = ImageDraw.Draw(im)
    d.polygon([(-40, -10)] + EDGE + [(-40, H + 10)], fill=(255, 255, 255, 255))
    sysw = text_img("SYSTEM", ImageFont.truetype(LAT, 300), (8, 10, 26, 255)).rotate(90, expand=True)
    gray = text_img("SYSTEM", ImageFont.truetype(LAT, 300), (148, 156, 168, 255)).rotate(90, expand=True)
    im.alpha_composite(gray, (-34 + 16, H + 40 - sysw.height - 40))
    im.alpha_composite(sysw, (-34, H + 40 - sysw.height))
    im.alpha_composite(portrait())
    d = ImageDraw.Draw(im)
    d.rectangle([150, 34, 400, 108], fill=(255, 255, 255), outline=(8, 10, 26), width=2)
    d.text((162, 36), "P3MAKOTO", font=ImageFont.truetype(LAT, 34), fill=(8, 10, 26))
    # 内容面板
    x0, y0, x1, y1 = W * 0.625, H * 0.17, W * 0.97, H * 0.89
    lean = 46
    d.polygon([(x0 - 40 + lean, y0 - 30), (x1 + 30 + lean, y0 - 30), (x1 + 30, y1 + 26), (x0 - 40, y1 + 26)], fill=(8, 20, 71, 220))
    d.polygon([(x0 - 10 + lean, y0 - 30), (x1 + 40 + lean, y0 - 30), (x1 + 40 + lean - 1, y0 - 25), (x0 - 10 + lean - 1, y0 - 25)], fill=(97, 214, 245))
    d.text((x0 + 16, y0 - 120), "GENERAL", font=ImageFont.truetype(LAT, 64), fill="white", stroke_width=5, stroke_fill=(5, 20, 77))
    fc = ImageFont.truetype(CJK, 26)
    rows = ["语言", "快速模式", "恐惧症模式", "屏幕震动", "常用提示", "显示计时器", "显示手牌数量", "长按确认", "跳过开场", "后台限帧"]
    for i, r in enumerate(rows):
        ry = y0 + 10 + i * 66
        sel = i == 2
        poly = [(x0 + 14, ry + 3), (x1 - 10 + 14, ry + 3), (x1 - 10, ry + 60), (x0, ry + 60)]
        if sel:
            d.polygon([(px + 9, py - 6) for px, py in poly], outline=(230, 26, 51), width=3)
            d.polygon(poly, fill=(247, 250, 255))
        else:
            d.polygon(poly, fill=(5, 15, 61, 140))
        d.polygon([(x0 + 14, ry + 3), (x0 + 21, ry + 3), (x0 + 7, ry + 60), (x0, ry + 60)], fill=(97, 214, 245))
        d.text((x0 + 30, ry + 14), r, font=fc, fill=(15, 26, 89) if sel else (255, 255, 255))
        bx = x1 - 90
        d.polygon([(bx + 6, ry + 18), (bx + 32, ry + 18), (bx + 26, ry + 44), (bx, ry + 44)], fill=(97, 214, 245) if i % 2 else (5, 20, 77), outline="white")
    # 分页大字
    words = ["GENERAL", "GRAPHICS", "SOUND", "INPUT", "P3MAKOTO"]
    zig = [0, 30, 12, -10, 26]
    f = ImageFont.truetype(LAT, 76)
    for i, wd in enumerate(words):
        sel = i == 0
        layer = Image.new("RGBA", (900, 200))
        ld = ImageDraw.Draw(layer)
        w = f.getlength(wd)
        if sel:
            h, yy, lean = 76 * 0.42, 76 * 0.36 + 40, 76 * 0.18
            ld.polygon([(24 + lean, yy), (40 + w + 30 + lean, yy), (40 + w + 30, yy + h), (24, yy + h)], fill=(230, 26, 51))
            ld.polygon([(40 + w + 34, yy + h), (40 + w + 84, yy - 52), (40 + w + 70, yy + h * 0.2)], fill=(247, 250, 255), outline=(230, 26, 51))
        ld.text((45, 46), wd, font=f, fill=(255, 255, 255, 240) if sel else (5, 26, 102, 115))
        ld.text((40, 40), wd, font=f, fill=(8, 10, 26) if sel else (158, 224, 255))
        layer = layer.rotate(math.degrees(0.2), resample=Image.BICUBIC, center=(40, 40))
        im.alpha_composite(layer, (int(905 + zig[i] + (10 if sel else 0) - 40), int(330 + i * 76 * 0.86 - 40)))
    d = ImageDraw.Draw(im)
    d.text((W - 330, H - 150), "常规设置", font=ImageFont.truetype(CJK, 26), fill="white")
    d.rectangle([W - 330, H - 110, W - 30, H - 108], fill="white")
    im.convert("RGB").save(out)


if __name__ == "__main__":
    main(sys.argv[1])
