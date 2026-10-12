"""P3R 菜单风立绘：art/p3menu 的正常画风全身 / 半身图 → 平涂（白 / 天蓝 / 宝蓝 / 深蓝四档 + 深蓝线条，红色只留耳机和臂章）。

- 结算画面（水之圆）：menu_v2_a（漂浮、闭眼）取上半身，脸放在画布中上部 → p3makoto/menu/victory.png
- 其余几张先输出平涂预览到 art/p3menu/flat/，接进游戏时再按用途裁切。
用法：python3 tools/ui/build_menu_art.py
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools/pck"))
from godot_tex import write_texture  # noqa: E402

SRC = ROOT / "art/p3menu"
FLAT = SRC / "flat"
PAL = [(0.04, 0.07, 0.25), (0.10, 0.25, 0.85), (0.45, 0.70, 1.0), (0.97, 0.98, 1.0)]


def flat(im: Image.Image) -> Image.Image:
    a = np.asarray(im.convert("RGBA")).astype(np.float32) / 255
    rgb, alpha = a[..., :3], a[..., 3]
    lum = 0.3 * rgb[..., 0] + 0.59 * rgb[..., 1] + 0.11 * rgb[..., 2]
    L8 = Image.fromarray((lum * 255).astype(np.uint8))
    smooth = np.asarray(L8.filter(ImageFilter.MedianFilter(7)).filter(ImageFilter.GaussianBlur(1.5))).astype(np.float32) / 255
    blur = np.asarray(L8.filter(ImageFilter.GaussianBlur(3))).astype(np.float32) / 255
    lines = np.clip((blur - lum - 0.07) * 8, 0, 1)
    idx = np.digitize(smooth, [0.16, 0.40, 0.72])
    out = np.zeros_like(rgb)
    for i, c in enumerate(PAL):
        out[idx == i] = c
    red = (rgb[..., 0] > 0.5) & (rgb[..., 1] < 0.35) & (rgb[..., 2] < 0.35)
    out[red] = (0.9, 0.1, 0.2)
    out = out * (1 - lines[..., None]) + np.array([0.02, 0.03, 0.12]) * lines[..., None]
    return Image.fromarray((np.dstack([out, alpha]) * 255).clip(0, 255).astype(np.uint8))


def victory() -> Image.Image:
    im = flat(Image.open(SRC / "menu_v2_a.png"))
    face = (560, 140)                       # 原图里脸的位置（1024×1536）
    canvas = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
    canvas.alpha_composite(im, (512 - face[0], 330 - face[1]))
    return canvas.rotate(-12, resample=Image.BICUBIC, center=(512, 330))     # 微微倾斜：漂在水里


def main():
    FLAT.mkdir(parents=True, exist_ok=True)
    v = victory()
    write_texture(ROOT / "mod/pck_stage", "p3makoto/menu/victory.png", v)
    v.save(FLAT / "victory.png")
    for n in ("skill_v2_a", "commu_v2_a", "equip_v2_a"):
        flat(Image.open(SRC / f"{n}.png")).save(FLAT / f"{n}.png")
    print("ok")


if __name__ == "__main__":
    main()
