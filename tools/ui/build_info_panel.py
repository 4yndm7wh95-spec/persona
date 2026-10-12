"""选人界面介绍板（P3R 人格面具状态界面风格，方向 A）的离线素材：
- info_head.png：抬头白带（不转角度，运行时再转）—— 白带 + 上沿红线 + 下沿皇家蓝细条 + 大号"0" + ARCANA +
  深蓝渐变的"結城 理"（青色错位残影）+ YUKI MAKOTO + 裁在白带里的浅灰"愚者"；
- info_head_ghost.png：只有"愚者"（运行时单独做缓慢漂移的常驻动画，所以白带图里不画它）。
字体在 tools/ui/fonts/（SIL OFL）：Noto Sans JP Black 子集、Barlow Condensed 粗斜体。2 倍分辨率烘焙，运行时缩 0.5。
用法：python3 tools/ui/build_info_panel.py"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools/pck"))
from godot_tex import write_texture  # noqa: E402

FONTS = ROOT / "tools/ui/fonts"
JP = str(FONTS / "NotoSansJP-Black-subset.ttf")
LAT = str(FONTS / "BarlowCondensed-900Italic.ttf")
LATB = str(FONTS / "BarlowCondensed-700Italic.ttf")
OUT = ROOT / "art/ui"
STAGE = ROOT / "mod/pck_stage"

K = 2                      # 烘焙倍数
W, H = 1000, 220           # 运行时尺寸（像素）
BT, BB = 40, 176           # 白带上下沿
BAND = [(0, BT), (990, BT), (950, BB), (0, BB)]
WHITE = (248, 250, 255)
INK = (8, 10, 24)
NAVY = (6, 16, 66)
ROYAL = (24, 70, 220)
CYAN = (57, 216, 255)
RED = (232, 30, 58)


def mask(text, font, size, spacing=0.0):
    f = ImageFont.truetype(font, size * K)
    probe = ImageDraw.Draw(Image.new("L", (1, 1)))
    w = int(sum(probe.textlength(c, font=f) for c in text) + spacing * size * K * len(text)) + 40 * K
    m = Image.new("L", (w, int(size * K * 1.5)), 0)
    d = ImageDraw.Draw(m)
    x = 10 * K
    for c in text:
        d.text((x, size * K * 0.1), c, font=f, fill=255)
        x += probe.textlength(c, font=f) + spacing * size * K
    return m.crop(m.getbbox())


def fill(m, top, bot=None):
    bot = bot or top
    t = np.linspace(0, 1, m.height)[:, None]
    g = np.zeros((m.height, m.width, 4), np.uint8)
    for i in range(3):
        g[..., i] = top[i] * (1 - t) + bot[i] * t
    g[..., 3] = np.asarray(m)
    return Image.fromarray(g, "RGBA")


def paste_mid(im, layer, x, cy):
    """字形左边对齐 x、竖直居中到 cy（运行时坐标）。"""
    im.alpha_composite(layer, (int(x * K), int(cy * K - layer.height / 2)))


def head():
    im = Image.new("RGBA", (W * K, H * K), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    S = lambda pts: [(x * K, y * K) for x, y in pts]  # noqa: E731
    d.polygon(S([(0, BT - 12), (620, BT - 12), (614, BT - 4), (0, BT - 4)]), fill=RED + (255,))
    d.polygon(S([(0, BB), (820, BB), (812, BB + 12), (0, BB + 12)]), fill=ROYAL + (255,))
    d.polygon(S(BAND), fill=WHITE + (255,))
    mid = (BT + BB) / 2
    z = mask("0", LAT, 96)
    paste_mid(im, fill(z, (24, 30, 70), INK), 52, mid + 8)
    im.alpha_composite(fill(mask("ARCANA", LATB, 19), (120, 130, 160)), (int(58 * K), int((BT + 10) * K)))
    n = mask("結城 理", JP, 76, spacing=0.06)
    ny = mid - 10
    paste_mid(im, fill(n, CYAN), 160 + 6, ny + 5)                       # 青色错位残影
    paste_mid(im, fill(n, (34, 60, 190), NAVY), 160, ny)
    im.alpha_composite(fill(mask("YUKI  MAKOTO", LATB, 20, spacing=0.12), (100, 110, 150)), (int(166 * K), int((BB - 32) * K)))
    return im


def ghost():
    """"愚者"：和白带同一画布，裁在白带形状里（运行时做慢速漂移时用白带形状的遮罩再裁一次）。"""
    im = Image.new("RGBA", (W * K, H * K), (0, 0, 0, 0))
    g = mask("愚者", JP, 104)
    paste_mid(im, fill(g, (224, 229, 241), (200, 208, 228)), 690, (BT + BB) / 2)
    return im


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for name, im in (("info_head", head()), ("info_head_ghost", ghost())):
        im.save(OUT / f"{name}.png")
        write_texture(STAGE, f"p3makoto/ui/{name}.png", im)
        print("staged", name, im.size)


if __name__ == "__main__":
    main()
