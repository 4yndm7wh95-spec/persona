"""P5 选人界面的贴图（docs/p5/CHARSELECT_PLAN.md）：把 art/p5/charselect/ 的生图处理成游戏用的 res://p5joker/fx/cs_*.png。
stage_p5.fx_textures() 会调用 textures()，所以打包和预览（tools/p5/fxpreview/prepare.py）都走这里。

立绘四层同一画布 1024x1536、已对齐（相位相关偏移 < 0.1px），拼法：
  身体 = 底图，只在手盖住的地方（外扩、羽化）换成"去掉手"那张 —— 静止时身体 + 手和底图逐像素一样；
  手   = 底图里手的像素（用 portrait_hand 的透明度当遮罩）；
  眨眼 = 闭眼图里眼睛那一块（贴片，带羽化），偏移见 BLINK_BOX。
摩尔加纳：身体图本身带尾巴，按尾巴那张的形状抠掉，尾巴单独一层（根部见 TAIL_ROOT）。
描边（白边 + 外圈黑边）离线烘焙成 cs_body_line / cs_hand_line，游戏里画在人物后面，运行时不用描边着色器。
天际线重新上色（recolor）：原图的白色楼面、自带的前景楼顶全部压成剪影，只留小窗灯 —— 原图白块太多是"脏"的主要来源。
锯齿球（cs_burst，游戏里染红、压在立绘面板的暗红黑底上）、斜条上的竖排小字（cs_slab_text）、标题黑带（cs_header_band）都是超采样画的，边缘抗锯齿；
眼睛蓝光的横向光条是 cs_eye_streak。
用法：python3 tools/p5/charselect_assets.py  → art/p5/charselect/game/ 下出一份检查用的 PNG + preview.png
"""
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "art/p5/charselect"
BLINK_BOX = (330, 320, 690, 570)            # 眨眼贴片在立绘画布里的范围（p5mod/src/Vfx/CharSelectScene.cs 的 BlinkAt 同步）
PORTRAIT_K = 1.0                            # 立绘贴图原尺寸（游戏里按图 2 的范围显示，约 0.92 倍）
LINE_WHITE, LINE_BLACK = 10.0, 17.0         # 描边：白边宽 / 白 + 黑总宽（画布像素）
# 锯齿球：贴图 1024，尖角外半径 400（游戏里缩放到画布 700，CharSelectScene.BurstK 同步）
BURST_SIZE, BURST_R, BURST_SPIKES = 1024, 400.0, 12
SLAB_TEXT = "PHANTOM THIEVES  //  CODE NAME : JOKER  //  ARCANA 0  //  "
FONT = ROOT / "tools/ui/fonts/BarlowCondensed-700Italic.ttf"


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


def outline(im, white=LINE_WHITE, black=LINE_BLACK):
    """剪影描边：离人物 white 以内白色、再往外到 black 黑色（距离场 + 1px 过渡 = 抗锯齿）。里面是实心的，画在人物后面。"""
    import scipy.ndimage as nd
    a = np.asarray(im)[..., 3] > 127
    d = nd.distance_transform_edt(~a)
    w = np.clip(white + 0.5 - d, 0, 1)
    al = np.clip(black + 0.5 - d, 0, 1)
    rgb = np.stack([w * 255 + (1 - w) * 10, w * 255 + (1 - w) * 6, w * 255 + (1 - w) * 8], -1)
    return Image.fromarray(np.dstack([rgb, al * 255]).clip(0, 255).astype(np.uint8), "RGBA")


def recolor(im, base=(12, 2, 5), win_k=0.85, seed=3):
    """天际线压成剪影：只有紧凑的小亮块（窗灯）保留原色（30% 调暗一半），大块白色楼面 / 线条 → 剪影色；大块红色（电波塔）→ 暗红。"""
    import scipy.ndimage as nd
    a = np.asarray(im).astype(np.float32)
    rgb, al = a[..., :3], a[..., 3]
    bright = (rgb.max(-1) > 110) & (al > 100)
    lab, n = nd.label(bright, structure=np.ones((3, 3)))
    area = nd.sum(bright, lab, range(1, n + 1))
    keep = np.zeros(n + 1, np.float32)
    rng = np.random.default_rng(seed)
    for i, sl in enumerate(nd.find_objects(lab)):
        h, w = sl[0].stop - sl[0].start, sl[1].stop - sl[1].start
        if area[i] <= 260 and h <= 26 and w <= 26 and area[i] >= 0.55 * h * w:
            keep[i + 1] = win_k * (0.45 if rng.random() < 0.3 else 1.0)
    k = keep[lab]
    out = np.zeros_like(a)
    out[..., :3] = np.array(base, np.float32)
    red = (rgb[..., 0] > 150) & (rgb[..., 1] < 100) & bright & (k == 0)
    out[..., :3][red] = (95, 8, 16)
    win = k > 0
    out[..., :3][win] = rgb[win] * k[win][:, None] + np.array(base, np.float32) * (1 - k[win][:, None])
    out[..., 3] = al
    return Image.fromarray(out.clip(0, 255).astype(np.uint8), "RGBA")


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


def _star(c, R, n, seed=5):
    rng = np.random.default_rng(seed)
    pts = []
    for i in range(n * 2):
        ang = -np.pi / 2 + np.pi * i / n + rng.uniform(-0.06, 0.06)
        r = (R if i % 4 == 0 else R * 0.86) if i % 2 == 0 else rng.uniform(0.5, 0.6) * R
        pts.append((c + np.cos(ang) * r, c + np.sin(ang) * r))
    return pts


def _poly_tex(pts_fn, size, ss=4):
    from PIL import ImageDraw
    im = Image.new("L", (size * ss, size * ss), 0)
    ImageDraw.Draw(im).polygon(pts_fn(ss), fill=255)
    out = Image.new("RGBA", (size, size), (255, 255, 255, 0))
    out.putalpha(im.resize((size, size), Image.LANCZOS))
    return out


def burst():
    """P5 锯齿爆炸形（白色，游戏里染红）。"""
    c = BURST_SIZE / 2
    return _poly_tex(lambda k: [(x * k, y * k) for x, y in _star(c, BURST_R, BURST_SPIKES)], BURST_SIZE)


def slab_text():
    """斜条上滚动的竖排小字（2 倍大烘焙，游戏里缩 0.5）：宽度正好是一个循环单元，首尾相接不跳。"""
    from PIL import ImageDraw, ImageFont
    font = ImageFont.truetype(str(FONT), 68)
    w = round(font.getlength(SLAB_TEXT))
    im = Image.new("RGBA", (w, 96), (255, 255, 255, 0))
    ImageDraw.Draw(im).text((0, 10), SLAB_TEXT, font=font, fill=(255, 255, 255, 235))
    return im


def header_band():
    """左上角标题的黑带：平行四边形（右端斜切），四周留透明边，游戏里旋转时边缘靠贴图过滤抗锯齿。"""
    from PIL import ImageDraw
    W, H, m, ss = 1060, 138, 4, 4
    im = Image.new("L", ((W + 2 * m) * ss, (H + 2 * m) * ss), 0)
    pts = [(m, m + 16), (m + W, m), (m + W - 34, m + H - 8), (m, m + H)]
    ImageDraw.Draw(im).polygon([(x * ss, y * ss) for x, y in pts], fill=255)
    out = Image.new("RGBA", (W + 2 * m, H + 2 * m), (10, 6, 8, 0))
    out.putalpha(im.resize((W + 2 * m, H + 2 * m), Image.LANCZOS).point(lambda v: v * 240 // 255))
    return out


def eye_streak(w=512, h=64):
    """眼睛的横向光条（白色，游戏里染蓝、加法混合）：中间粗、两头尖，外层柔光 + 中间一道更细更亮的芯。"""
    x = np.linspace(-1, 1, w)[None, :]
    y = np.linspace(-1, 1, h)[:, None]
    taper = np.clip(1 - np.abs(x), 0, 1)
    soft = np.exp(-(y * 3.2) ** 2) * taper ** 1.8
    core = np.exp(-(y * 11.0) ** 2) * taper ** 3.0
    a = np.clip(0.55 * soft + 0.9 * core, 0, 1)
    rgb = np.full((h, w, 3), 255, np.float32)
    return Image.fromarray(np.dstack([rgb, a * 255]).astype(np.uint8), "RGBA")


def _pad(im, m=6):
    out = Image.new("RGBA", (im.width + 2 * m, im.height + 2 * m), (0, 0, 0, 0))
    out.alpha_composite(im, (m, m))
    return out


def textures():
    """(name, image)：res://p5joker/fx/<name>.png。"""
    layers = portrait_layers()
    yield from layers.items()
    yield "cs_body_line", outline(layers["cs_body"])
    yield "cs_hand_line", outline(layers["cs_hand"])
    yield from morgana_layers().items()
    yield "cs_skyline", recolor(_rgba("city_skyline").crop((0, 351, 1536, 1024)))
    yield "cs_rooftop", _rgba("rooftop_foreground").crop((0, 305, 1536, 1024))
    for name, im, _ in banner_parts():
        yield name, im
    yield "cs_burst", burst()
    yield "cs_slab_text", slab_text()
    yield "cs_eye_streak", eye_streak()
    yield "cs_header_band", header_band()
    yield "cs_card", _pad(_scale(_rgba("calling_card"), 0.25))
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
