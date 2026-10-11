"""界面小图：左上角头像（+ 白色外轮廓）、地图标记、选人按钮头像（+ 未解锁剪影）。
全部从同一张母版（art/v4/body_cut.png）裁出结城理的胸像，统一 P3R 配色。
输出到 art/ui/ 预览，并写入 mod/pck_stage（png + .import + .ctex，沿用原版 uid）。
用法：python3 tools/ui/build_ui_assets.py"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools/pck"))
from godot_tex import write_texture  # noqa: E402

BODY = ROOT / "art/v4/body_cut.png"
OUT = ROOT / "art/ui"
STAGE = ROOT / "mod/pck_stage"

ABYSS, ROYAL, CYAN, ICE = (7, 26, 74), (31, 92, 255), (57, 216, 255), (216, 244, 255)
INK = (8, 12, 30)


def bust(box, size):
    """从母版裁胸像并缩放到 size（宽高按 box 比例）。"""
    im = Image.open(BODY).convert("RGBA").crop(box)
    return im.resize(size, Image.LANCZOS)


def dilate(alpha: Image.Image, px: int) -> Image.Image:
    return alpha.filter(ImageFilter.MaxFilter(px * 2 + 1))


def outlined(im: Image.Image, px: int, color) -> Image.Image:
    a = dilate(im.getchannel("A"), px)
    base = Image.new("RGBA", im.size, color + (0,))
    base.putalpha(a)
    base.alpha_composite(im)
    return base


def radial_badge(size, r, center):
    """深蓝→皇家蓝的圆形徽章 + 青色描边。"""
    w, h = size
    ys, xs = np.mgrid[0:h, 0:w]
    d = np.hypot(xs - center[0], ys - center[1]) / r
    t = np.clip(1 - d, 0, 1)
    rgb = np.array(ABYSS)[None, None] * (1 - t[..., None]) + np.array(ROYAL)[None, None] * t[..., None] * 0.85
    a = (np.clip((1 - d) * r, 0, 1) * 255).astype(np.uint8)
    im = Image.fromarray(np.dstack([rgb.clip(0, 255).astype(np.uint8), a]), "RGBA")
    ring = Image.new("RGBA", size, (0, 0, 0, 0))
    ImageDraw.Draw(ring).ellipse((center[0] - r, center[1] - r, center[0] + r, center[1] + r), outline=CYAN + (255,), width=2)
    im.alpha_composite(ring)
    return im


def pop_out(badge, figure, center, r):
    """胸像在徽章圆内被裁切，但圆心以上的部分（头发）可以探出圆外 —— 立体感。"""
    w, h = badge.size
    ys, xs = np.mgrid[0:h, 0:w]
    inside = np.hypot(xs - center[0], ys - center[1]) <= r - 1
    above = ys < center[1]
    mask = Image.fromarray(((inside | above) * 255).astype(np.uint8))
    fig = figure.copy()
    fig.putalpha(Image.fromarray(np.minimum(np.asarray(fig.getchannel("A")), np.asarray(mask))))
    out = badge.copy()
    out.alpha_composite(outlined(fig, 1, INK))
    return out


def top_icon():
    S = 88
    center, r = (44, 50), 36
    badge = radial_badge((S, S), r, center)
    fig = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    fig.alpha_composite(bust((360, 0, 680, 320), (82, 82)), (4, 2))
    icon = outlined(pop_out(badge, fig, center, r), 1, INK)
    outline = Image.new("RGBA", (S, S), (255, 255, 255, 0))
    outline.putalpha(dilate(icon.getchannel("A"), 3))
    return icon, outline


def map_marker():
    W, H = 49, 64
    center, r = (24, 30), 21
    canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    tip = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(tip).polygon([(14, 44), (34, 44), (24, 61)], fill=ROYAL + (255,), outline=CYAN + (255,))
    canvas.alpha_composite(tip)
    badge = radial_badge((W, H), r, center)
    canvas.alpha_composite(badge)
    fig = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    fig.alpha_composite(bust((370, 0, 670, 300), (48, 48)), (0, 4))
    return outlined(pop_out(canvas, fig, center, r), 1, INK)


def char_select():
    W, H = 132, 195
    ys, xs = np.mgrid[0:H, 0:W]
    t = ys / H
    rgb = np.array(ROYAL)[None, None] * (1 - t[..., None]) * 0.75 + np.array(ABYSS)[None, None] * t[..., None]
    stripes = ((xs + ys * 0.55) % 38 < 9) * 0.12                    # P3R 斜条纹
    rgb = rgb * (1 + stripes[..., None])
    bg = Image.fromarray(np.dstack([rgb.clip(0, 255).astype(np.uint8), np.full((H, W), 255, np.uint8)]), "RGBA")
    glow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(glow).ellipse((-30, 10, 150, 190), fill=CYAN + (60,))
    bg.alpha_composite(glow.filter(ImageFilter.GaussianBlur(18)))
    fig = bust((330, 0, 720, 580), (172, 256))
    portrait = bg.copy()
    portrait.alpha_composite(outlined(fig, 1, INK), (-26, 8))
    d = ImageDraw.Draw(portrait)
    d.rectangle((0, 0, W - 1, H - 1), outline=INK + (255,), width=2)
    d.line((2, H - 4, W - 3, H - 4), fill=CYAN + (255,), width=1)
    # 未解锁：灰底 + 纯黑剪影（与原版一致）
    locked = Image.new("RGBA", (W, H), (68, 68, 72, 255))
    sil = Image.new("RGBA", fig.size, (0, 0, 0, 255))
    sil.putalpha(fig.getchannel("A"))
    locked.alpha_composite(sil, (-26, 8))
    ImageDraw.Draw(locked).rectangle((0, 0, W - 1, H - 1), outline=(40, 40, 44, 255), width=2)
    return portrait, locked


ASSETS = {
    # res 相对路径: (原版 uid, 生成函数里的哪一张)
    "images/ui/top_panel/character_icon_regent.png": "ddwry85j3nb6",
    "images/ui/top_panel/character_icon_regent_outline.png": "br0nmhqx2ci7a",
    "images/packed/map/icons/map_marker_regent.png": "2mjv464plgnx",
    "images/packed/character_select/char_select_regent.png": "4rucvor0rrtj",
    "images/packed/character_select/char_select_regent_locked.png": "ct84r6405x1tk",
}


def charselect_portrait():
    """选人界面特写（art/v6/portrait_a.png）：抠绿幕、裁边、缩到高 1400；底部 12% 渐隐（腿部被画框截断处不留硬边）。"""
    sys.path.insert(0, str(ROOT / "tools/rig"))
    from build_rig import key_image
    im = key_image(ROOT / "art/v6/portrait_a.png")
    im = im.crop(im.getchannel("A").point(lambda v: 255 if v > 8 else 0).getbbox())
    h = 1400
    im = im.resize((round(im.width * h / im.height), h), Image.LANCZOS)
    a = np.asarray(im).copy()
    ys = np.arange(h)[:, None]
    fade = np.clip((h - ys) / (h * 0.12), 0, 1)
    a[..., 3] = (a[..., 3] * fade).astype(np.uint8)
    return Image.fromarray(a, "RGBA")


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    portrait_img = charselect_portrait()
    portrait_img.save(OUT / "charselect_portrait.png")
    write_texture(STAGE, "p3makoto/ui/charselect_portrait.png", portrait_img)
    print("staged charselect portrait", portrait_img.size)
    icon, outline = top_icon()
    portrait, locked = char_select()
    images = {
        "images/ui/top_panel/character_icon_regent.png": icon,
        "images/ui/top_panel/character_icon_regent_outline.png": outline,
        "images/packed/map/icons/map_marker_regent.png": map_marker(),
        "images/packed/character_select/char_select_regent.png": portrait,
        "images/packed/character_select/char_select_regent_locked.png": locked,
    }
    for rel, im in images.items():
        im.save(OUT / Path(rel).name)
        write_texture(STAGE, rel, im, ASSETS[rel])
        print("staged", rel, im.size)
    # 预览拼图（放大 3 倍，深色底）
    sheet = Image.new("RGBA", (3 * (88 + 88 + 49 + 132 + 132) + 120, 3 * 195 + 20), (40, 44, 56, 255))
    x = 10
    for im in images.values():
        big = im.resize((im.width * 3, im.height * 3), Image.NEAREST)
        sheet.alpha_composite(big, (x, 10))
        x += big.width + 20
    sheet.save(OUT / "preview.png")


if __name__ == "__main__":
    main()
