"""P5 选人界面的贴图（docs/p5/CHARSELECT_PLAN.md）：把 art/p5/charselect/ 的生图处理成游戏用的 res://p5joker/fx/cs_*.png。
stage_p5.fx_textures() 会调用 textures()，所以打包和预览（tools/p5/fxpreview/prepare.py）都走这里。

立绘四层同一画布 1024x1536、已对齐（相位相关偏移 < 0.1px），拼法：
  身体 = 底图，只在手盖住的地方（外扩、羽化）换成"去掉手"那张 —— 静止时身体 + 手和底图逐像素一样；
  手   = 底图里手的像素（用 portrait_hand 的透明度当遮罩）；
  眨眼 = 闭眼图里眼睛那一块（贴片，带羽化），偏移见 BLINK_BOX。
摩尔加纳：身体图本身带尾巴，按尾巴那张的形状抠掉，尾巴单独一层（根部见 TAIL_ROOT）。
用法：python3 tools/p5/charselect_assets.py  → art/p5/charselect/game/ 下出一份检查用的 PNG + preview.png
"""
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "art/p5/charselect"
BLINK_BOX = (330, 320, 690, 570)            # 眨眼贴片在立绘画布里的范围（p5mod/src/Vfx/CharSelectScene.cs 的 BlinkAt 同步）
PORTRAIT_K = 0.75                           # 立绘贴图缩放（1024x1536 → 768x1152，游戏里显示约 0.72 倍原图，够用）


def _rgba(name):
    return Image.open(SRC / f"{name}.png").convert("RGBA")


def _feather(mask, grow, blur):
    m = Image.fromarray((mask * 255).astype(np.uint8), "L")
    if grow:
        m = m.filter(ImageFilter.MaxFilter(grow * 2 + 1))
    if blur:
        m = m.filter(ImageFilter.GaussianBlur(blur))
    return np.asarray(m).astype(np.float32) / 255


def _scale(im, k):
    return im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS)


def _trim(im, thr=8):
    a = np.asarray(im)[..., 3]
    ys, xs = np.nonzero(a > thr)
    return im.crop((xs.min(), ys.min(), xs.max() + 1, ys.max() + 1)), (int(xs.min()), int(ys.min()))


def portrait_layers():
    base = np.asarray(_rgba("portrait_base")).astype(np.float32)
    nohand = np.asarray(_rgba("portrait_nohand")).astype(np.float32)
    hand_a = np.asarray(_rgba("portrait_hand"))[..., 3].astype(np.float32) / 255
    # 手：底图像素 × 手的透明度
    hand = base.copy()
    hand[..., 3] = base[..., 3] * hand_a
    # 身体：手的区域外扩 18px、羽化 8px 的范围里换成去掉手那张
    w = _feather(hand_a > 0.05, 18, 8)[..., None]
    body = base * (1 - w) + nohand * w
    # 眨眼贴片
    blink = np.asarray(_rgba("portrait_blink")).astype(np.float32)
    x0, y0, x1, y1 = BLINK_BOX
    diff = np.abs(blink - base).sum(-1) > 60
    keep = np.zeros(diff.shape, bool)
    keep[y0:y1, x0:x1] = diff[y0:y1, x0:x1]
    bw = _feather(keep, 10, 6)[y0:y1, x0:x1]
    patch = blink[y0:y1, x0:x1].copy()
    patch[..., 3] *= bw
    to = lambda a: Image.fromarray(a.clip(0, 255).astype(np.uint8), "RGBA")
    return {
        "cs_body": _scale(to(body), PORTRAIT_K),
        "cs_hand": _scale(to(hand), PORTRAIT_K),
        "cs_blink": _scale(to(patch), PORTRAIT_K),
    }


def morgana_layers():
    body = np.asarray(_rgba("morgana")).astype(np.float32)
    tail = np.asarray(_rgba("morgana_tail")).astype(np.float32)
    cut = _feather(tail[..., 3] > 20, 3, 1.5)
    body[..., 3] *= 1 - cut
    to = lambda a: Image.fromarray(a.clip(0, 255).astype(np.uint8), "RGBA")
    k = 0.25                                # 1254 → 314：游戏里约 80px 高，留两倍余量
    return {"cs_morgana": _scale(to(body), k), "cs_morgana_tail": _scale(to(tail), k)}


def banner_parts():
    """TAKE YOUR HEART 按连通块拆成几块（字母纸片互相压着，拆不到单个字母），游戏里每块单独"跳"。
    返回 [(名字, 图, (x, y))]，偏移是在整条 cs_banner 里的位置（CharSelectScene.BannerParts 同步）。"""
    import scipy.ndimage as nd
    full = _trim(_rgba("banner_take_your_heart"))[0]
    a = np.asarray(full).astype(np.float32)
    lab, _ = nd.label(a[..., 3] > 128)
    parts = []
    for i, sl in enumerate(nd.find_objects(lab)):
        if (sl[0].stop - sl[0].start) * (sl[1].stop - sl[1].start) < 2000:
            continue
        own = nd.binary_dilation(lab == i + 1, iterations=3)
        im = a.copy()
        im[..., 3] *= own
        x0, y0 = max(sl[1].start - 3, 0), max(sl[0].start - 3, 0)
        x1, y1 = min(sl[1].stop + 3, a.shape[1]), min(sl[0].stop + 3, a.shape[0])
        parts.append((Image.fromarray(im[y0:y1, x0:x1].clip(0, 255).astype(np.uint8), "RGBA"), (x0, y0)))
    parts.sort(key=lambda p: p[1][0])
    return [(f"cs_banner_{i}", im, off) for i, (im, off) in enumerate(parts)]


def burst(size=1024, spikes=13, ss=4):
    """P5 锯齿爆炸形（白色，游戏里染色）：超采样画多边形再缩小 = 抗锯齿的硬边。尖角长短交替、带一点随机。"""
    from PIL import ImageDraw
    rng = np.random.default_rng(5)
    S = size * ss
    c = S / 2
    pts = []
    for i in range(spikes * 2):
        ang = -np.pi / 2 + np.pi * i / spikes + rng.uniform(-0.06, 0.06)
        r = (0.49 if i % 4 == 0 else 0.42) * S if i % 2 == 0 else rng.uniform(0.25, 0.3) * S
        pts.append((c + np.cos(ang) * r, c + np.sin(ang) * r))
    im = Image.new("L", (S, S), 0)
    ImageDraw.Draw(im).polygon(pts, fill=255)
    a = im.resize((size, size), Image.LANCZOS)
    out = Image.new("RGBA", (size, size), (255, 255, 255, 0))
    out.putalpha(a)
    return out


def textures():
    """(name, image)：res://p5joker/fx/<name>.png。"""
    yield from portrait_layers().items()
    yield from morgana_layers().items()
    yield "cs_skyline", _scale(_rgba("city_skyline").crop((0, 351, 1536, 1024)), 1.0)
    yield "cs_rooftop", _rgba("rooftop_foreground").crop((0, 305, 1536, 1024))
    for name, im, _ in banner_parts():
        yield name, im
    yield "cs_burst", burst()
    yield "cs_title", _scale(_trim(_rgba("title_joker"))[0], 0.5)
    yield "cs_card", _scale(_rgba("calling_card"), 0.25)
    yield "cs_button", button_icons()[0]


def button_icons():
    """角色按钮（原版 132x195）：生图 joker_button 按卡片比例裁上部；未解锁版去色压暗。"""
    W, H = 132, 195
    im = Image.open(SRC / "joker_button.png").convert("RGBA")
    ch = round(im.width * H / W)
    im = im.crop((0, 0, im.width, min(ch, im.height))).resize((W, H), Image.LANCZOS)
    g = np.asarray(im.convert("L")).astype(np.float32) * 0.3 + 26
    locked = Image.fromarray(np.dstack([g, g * 0.96, g * 0.96, np.full_like(g, 255)]).clip(0, 255).astype(np.uint8), "RGBA")
    return im, locked


if __name__ == "__main__":
    out = SRC / "game"
    out.mkdir(exist_ok=True)
    ims = dict(textures())
    ims["cs_button_locked"] = button_icons()[1]
    print("banner parts", [(n, off, im.size) for n, im, off in banner_parts()])
    for n, im in ims.items():
        im.save(out / f"{n}.png")
        print(n, im.size)
    # 检查：身体 + 眨眼 + 手 拼回去和底图比
    body, hand, blink = ims["cs_body"].copy(), ims["cs_hand"], ims["cs_blink"]
    body.alpha_composite(blink, (round(BLINK_BOX[0] * PORTRAIT_K), round(BLINK_BOX[1] * PORTRAIT_K)))
    body.alpha_composite(hand)
    bg = Image.new("RGBA", body.size, (150, 20, 30, 255))
    bg.alpha_composite(body)
    bg.convert("RGB").save(out / "preview.png")
