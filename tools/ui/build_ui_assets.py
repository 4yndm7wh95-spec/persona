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


def cutin_eyes():
    """召唤器切入的"眼神特写"横幅（P3R 技能切入风格）：从 art/v6/portrait_a.png 裁出露出的那只眼睛所在的横条，
    放大到 1600×440，做青蓝双色调（亮度 → 深海军蓝 / 宝蓝 / 青 / 白 的渐变映射），透明处填深蓝→青渐变，
    左右两端渐隐。游戏里再叠气泡、横向缓慢平移。"""
    sys.path.insert(0, str(ROOT / "tools/rig"))
    from build_rig import key_image
    from PIL import ImageFilter
    im = key_image(ROOT / "art/v6/portrait_a.png").crop((290, 182, 870, 342))
    W, H = 1600, 440
    im = im.resize((W, H), Image.LANCZOS).filter(ImageFilter.UnsharpMask(radius=3, percent=80, threshold=2))
    a = np.asarray(im).astype(np.float32) / 255
    rgb, alpha = a[..., :3], a[..., 3:4]
    lum = (rgb * [0.3, 0.55, 0.15]).sum(-1)
    lum = np.clip((lum - 0.05) / 0.85, 0, 1) ** 0.85
    stops = np.array([[0.02, 0.05, 0.18], [0.07, 0.25, 0.75], [0.2, 0.78, 1.0], [0.85, 0.98, 1.0]])
    pos = np.array([0.0, 0.35, 0.7, 1.0])
    out = np.stack([np.interp(lum, pos, stops[:, c]) for c in range(3)], -1)
    ys, xs = np.mgrid[0:H, 0:W].astype(np.float32)
    ys, xs = ys / H, xs / W
    bg = np.stack([0.03 + 0.12 * xs, 0.12 + 0.45 * xs * (1 - ys * 0.5), 0.35 + 0.55 * xs], -1)   # 深蓝 → 青
    col = out * alpha + bg * (1 - alpha)
    # ── 光影（参照原版切入：左上漏光、下方阴影、眼睛泛蓝光 + 横向镜头光、淡淡的水下焦散）──
    Y, X = np.mgrid[0:H, 0:W].astype(np.float32)
    leak = np.clip(1 - np.hypot((X - W * 0.1) / (W * 0.55), (Y + H * 0.3) / (H * 1.1)), 0, 1) ** 2
    col = col + np.array([0.25, 0.55, 0.8]) * leak[..., None] * 0.45
    beams = sum(np.clip(1 - np.abs((X - Y * 0.9) - W * c) / (W * w), 0, 1) ** 2 for c, w in ((0.18, 0.03), (0.33, 0.012), (0.62, 0.02)))
    col = col + np.array([0.5, 0.8, 1.0]) * beams[..., None] * 0.18
    shade = np.clip((Y / H - 0.55) / 0.45, 0, 1) ** 1.5
    col = col * (1 - 0.5 * shade[..., None]) * (1 - 0.25 * np.clip(1 - Y / (H * 0.12), 0, 1)[..., None])
    ex, ey = EYE
    r = np.hypot((X - ex) / 1.0, (Y - ey) / 0.85)
    col = col + np.array([0.3, 0.85, 1.0]) * (np.exp(-r / 38) * 0.9 + np.exp(-r / 120) * 0.35)[..., None]
    streak = np.exp(-np.abs(Y - ey) / 5) * np.exp(-np.abs(X - ex) / 420)
    col = col + np.array([0.45, 0.9, 1.0]) * streak[..., None] * 0.55
    # 焦散（和游戏里水之圆同一个公式，只在暗部淡淡叠一层）
    p = np.stack([X / W * 9, Y / H * 2.5], -1)
    i = p.copy(); c = np.ones((H, W), np.float32); inten = 0.005; t = 3.0
    for n in range(4):
        tt = t * (1 - 3.5 / (n + 1))
        i = p + np.stack([np.cos(tt - i[..., 0]) + np.sin(tt + i[..., 1]), np.sin(tt - i[..., 1]) + np.cos(tt + i[..., 0])], -1)
        c += 1 / np.hypot(p[..., 0] / (np.sin(i[..., 0] + tt) / inten), p[..., 1] / (np.cos(i[..., 1] + tt) / inten))
    c = np.abs(1.17 - (c / 4) ** 1.4) ** 8
    dark = np.clip(1 - lum, 0, 1)[..., None] * (1 - alpha * 0.6)
    col = col + np.array([0.4, 0.75, 1.0]) * np.clip(c, 0, 1)[..., None] * dark * 0.35
    edge = np.clip(np.minimum(xs, 1 - xs) / 0.08, 0, 1)[..., None]
    rgba = np.concatenate([col, edge], -1)
    return Image.fromarray((np.clip(rgba, 0, 1) * 255).astype(np.uint8), "RGBA")


EYE = (690, 215)          # 横幅贴图里露出的那只眼睛（游戏里眼睛的动态光也对准这里：CutInFx.EyeTex）


def cutin_ka():
    """切入右侧的"咔"：原版那种手绘感的空心双描边大字 —— 斜切、白色粗外描边 + 黑色外缘 + 内部细青线、
    空心（内部半透明深蓝）、右下错开的深蓝投影、几道速度斜线。离线用粗黑体字形做遮罩生成 PNG。"""
    from PIL import ImageDraw, ImageFont, ImageFilter
    font = ImageFont.truetype("/usr/share/fonts/truetype/wqy/wqy-zenhei.ttc", 300)
    S = 520
    m = Image.new("L", (S, S), 0)
    ImageDraw.Draw(m).text((60, 40), "咔", font=font, fill=255, stroke_width=6, stroke_fill=255)
    m = m.transform(m.size, Image.AFFINE, (1, 0.32, -70, 0, 1, 0), resample=Image.BICUBIC)       # 斜体
    g = np.asarray(m).astype(np.float32) / 255
    def grow(a, r):
        return np.asarray(Image.fromarray((a * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(r))).astype(np.float32) / 255
    def shrink(a, r):
        return np.asarray(Image.fromarray((a * 255).astype(np.uint8)).filter(ImageFilter.MinFilter(r))).astype(np.float32) / 255
    outer = grow(g, 23)              # 黑色外缘
    white = grow(g, 15)              # 白色粗描边
    inner = shrink(g, 7)             # 内部
    cyan = np.clip(g - shrink(g, 5), 0, 1)
    out = np.zeros((S, S, 4), np.float32)
    shadow = np.roll(np.roll(outer, 14, 0), 18, 1)
    out[..., :3] = [0.03, 0.08, 0.3]; out[..., 3] = shadow * 0.85
    def over(rgb, a):
        out[..., :3] = out[..., :3] * (1 - a[..., None]) + np.array(rgb) * a[..., None]
        out[..., 3] = out[..., 3] * (1 - a) + a
    over([0.02, 0.02, 0.06], outer)
    over([1, 1, 1], white)
    over([0.04, 0.1, 0.32], inner * 0.8)
    over([0.35, 0.9, 1.0], cyan)
    # 速度斜线
    im = Image.fromarray((np.clip(out, 0, 1) * 255).astype(np.uint8), "RGBA")
    d = ImageDraw.Draw(im)
    for k, (x, y, L) in enumerate(((30, 380, 150), (380, 90, 120), (410, 140, 70))):
        d.line([(x, y), (x + L, y - L * 0.55)], fill=(255, 255, 255, 230), width=7 - 2 * (k == 2))
    return im


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
    ka = cutin_ka()
    ka.save(OUT / "cutin_ka.png")
    write_texture(STAGE, "p3makoto/ui/cutin_ka.png", ka)
    eyes = cutin_eyes()
    eyes.save(OUT / "cutin_eyes.png")
    write_texture(STAGE, "p3makoto/ui/cutin_eyes.png", eyes)
    print("staged cut-in eyes", eyes.size)
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
