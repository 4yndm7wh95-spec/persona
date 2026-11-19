"""P5 选人界面的贴图（docs/p5/CHARSELECT_PLAN.md）：把 art/p5/charselect/ 的生图处理成游戏用的 res://p5joker/fx/cs_*.png。
stage_p5.fx_textures() 会调用 textures()，所以打包和预览（tools/p5/fxpreview/prepare.py）都走这里。

立绘四层同一画布 1024x1536、已对齐（相位相关偏移 < 0.1px），拼法：
  身体 = 底图，只在手盖住的地方（外扩、羽化）换成"去掉手"那张 —— 静止时身体 + 手和底图逐像素一样；
  手   = 底图里手的像素（用 portrait_hand 的透明度当遮罩）；
  眨眼 = 闭眼图里眼睛那一块（贴片，带羽化），偏移见 BLINK_BOX。
描边（白边 + 外圈黑边）离线烘焙成 cs_body_line / cs_hand_line，游戏里画在人物后面，运行时不用描边着色器。
天际线重新上色（recolor）：原图的白色楼面、自带的前景楼顶全部压成剪影，只留小窗灯 —— 原图白块太多是"脏"的主要来源。
斜条上的竖排小字（cs_slab_text）、介绍板抬头（cs_info_head / cs_info_ghost，同结城理 mod 的 build_info_panel 几何）都是超采样画的，边缘抗锯齿；
眼睛金光的横向光条是 cs_eye_streak。
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


def warp_map(body):
    """
    立绘变形权重图（1/4 分辨率，游戏里铺满画布采样；CharSelectScene 的 PortraitShader 用）：
      R 头（头发 + 脸，以脖子为支点轻摆）  G 外圈发梢（随风飘）  B 左边领尖（抖动）；A 不用（填满 —— 透明度会让缩放 / 导入改掉 RGB）。
    胸口 / 肩的呼吸权重只和高度有关，在着色器里算。全部是平滑过渡，变形不会撕开。
    """
    import scipy.ndimage as nd
    a = np.asarray(body).astype(np.float32)
    al = a[..., 3] / 255
    lum = a[..., :3].max(-1)
    H, W = al.shape
    y, x = np.mgrid[0:H, 0:W].astype(np.float32)
    # 头：以脸 + 头发为中心的椭圆，外圈平滑衰减；右边竖起的大衣领子不算头
    r = np.sqrt(((x - 560) / 380) ** 2 + ((y - 300) / 330) ** 2)
    head = np.clip((1.0 - r) / 0.28, 0, 1)
    collar_r = np.clip((x - 760) / 120, 0, 1) * np.clip((y - 360) / 140, 0, 1)
    head *= 1 - collar_r
    # 外圈发梢：头部范围里的深色像素，离透明背景 50px 以内
    hair = (lum < 80) & (al > 0.5) & (y < 620)
    dist = nd.distance_transform_edt(al > 0.5)
    tips = np.clip(1 - dist / 50, 0, 1) * hair * np.clip(head * 2, 0, 1)
    tips = nd.gaussian_filter(tips, 4)
    # 左边领尖：x 越往左越大，只在领子那一条（y 320..470）
    band = np.clip(1 - np.abs(y - 395) / 80, 0, 1)
    collar = np.clip((330 - x) / 260, 0, 1) * band
    collar = nd.gaussian_filter(collar, 8)
    chans = [Image.fromarray((np.clip(c, 0, 1) * 255).astype(np.uint8), "L").resize((W // 4, H // 4), Image.LANCZOS) for c in (head, tips, collar)]
    return Image.merge("RGBA", chans + [Image.new("L", chans[0].size, 255)])


def slab_text():
    """斜条上滚动的竖排小字（2 倍大烘焙，游戏里缩 0.5）：宽度正好是一个循环单元，首尾相接不跳。"""
    from PIL import ImageDraw, ImageFont
    font = ImageFont.truetype(str(FONT), 68)
    w = round(font.getlength(SLAB_TEXT))
    im = Image.new("RGBA", (w, 96), (255, 255, 255, 0))
    ImageDraw.Draw(im).text((0, 10), SLAB_TEXT, font=font, fill=(255, 255, 255, 235))
    return im


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


# ───────── 介绍板抬头（照结城理 mod tools/ui/build_info_panel.py：同一套几何，运行时 1:1 贴，坐标沿用 P3 的 CharSelectInfo） ─────────
IK = 2                                       # 烘焙倍数
IW, IH, BT, BB = 1000, 220, 40, 176          # 运行时尺寸、带子上下沿
JP = ROOT / "tools/ui/fonts/NotoSansJP-Black-subset.ttf"
LAT = ROOT / "tools/ui/fonts/BarlowCondensed-900Italic.ttf"


def _mask(text, font, size, spacing=0.0):
    from PIL import ImageDraw, ImageFont
    f = ImageFont.truetype(str(font), size * IK)
    probe = ImageDraw.Draw(Image.new("L", (1, 1)))
    w = int(sum(probe.textlength(c, font=f) for c in text) + spacing * size * IK * len(text)) + 40 * IK
    m = Image.new("L", (w, int(size * IK * 1.5)), 0)
    d = ImageDraw.Draw(m)
    x = 10 * IK
    for c in text:
        d.text((x, size * IK * 0.1), c, font=f, fill=255)
        x += probe.textlength(c, font=f) + spacing * size * IK
    return m.crop(m.getbbox())


def _fill(m, top, bot=None):
    bot = bot or top
    t = np.linspace(0, 1, m.height)[:, None]
    g = np.zeros((m.height, m.width, 4), np.uint8)
    for i in range(3):
        g[..., i] = top[i] * (1 - t) + bot[i] * t
    g[..., 3] = np.asarray(m)
    return Image.fromarray(g, "RGBA")


def _mid(im, layer, x, cy):
    im.alpha_composite(layer, (int(x * IK), int(cy * IK - layer.height / 2)))


def _tilt(im):
    """绕中心逆时针转 5°（右端上扬，expand），再从 2 倍缩回 1 倍（同 P3）。"""
    im = im.rotate(5.0, expand=True, resample=Image.BICUBIC)
    return im.resize((im.width // IK, im.height // IK), Image.LANCZOS)


def info_head():
    """
    P5R 状态界面风格的抬头：黑色倾斜带 + 上沿红条 + 下沿白细条；左边大号「0」+ ARCANA；中间勒索信剪贴字 JOKER（生图 title_joker）
    压在带子上、上下略出界；右边 CODE NAME / REN AMAMIYA。
    """
    from PIL import ImageDraw
    im = Image.new("RGBA", (IW * IK, IH * IK), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    S = lambda pts: [(x * IK, y * IK) for x, y in pts]  # noqa: E731
    d.polygon(S([(0, BT - 12), (620, BT - 12), (614, BT - 4), (0, BT - 4)]), fill=(222, 16, 30, 255))
    d.polygon(S([(0, BB + 2), (800, BB + 2), (795, BB + 9), (0, BB + 9)]), fill=(240, 236, 236, 255))
    d.polygon(S([(0, BT), (990, BT), (950, BB), (0, BB)]), fill=(14, 10, 12, 255))
    mid = (BT + BB) / 2
    _mid(im, _fill(_mask("0", LAT, 96), (250, 248, 248), (200, 190, 192)), 52, mid + 8)
    im.alpha_composite(_fill(_mask("ARCANA", LAT, 19), (170, 160, 165)), (int(58 * IK), int((BT + 10) * IK)))
    title = _trim(_rgba("title_joker"))[0]
    th = 150 * IK
    title = title.resize((round(title.width * th / title.height), th), Image.LANCZOS)
    im.alpha_composite(title, (int(156 * IK), int((mid - 4) * IK - th / 2)))
    x_txt = 156 + title.width / IK + 18
    im.alpha_composite(_fill(_mask("CODE NAME", LAT, 18, spacing=0.1), (232, 40, 54)), (int(x_txt * IK), int((mid - 30) * IK)))
    im.alpha_composite(_fill(_mask("REN  AMAMIYA", LAT, 22, spacing=0.1), (236, 232, 232), (190, 184, 186)), (int(x_txt * IK), int((mid - 2) * IK)))
    return _tilt(im)


def info_ghost():
    """「愚者」：和抬头同一画布、同一位置，深红色，运行时叠在黑带上单独慢慢漂移（只在带子里）。"""
    im = Image.new("RGBA", (IW * IK, IH * IK), (0, 0, 0, 0))
    _mid(im, _fill(_mask("愚者", JP, 104), (40, 12, 16), (30, 9, 12)), 728, (BT + BB) / 2)
    return _tilt(im)


def _pad(im, m=6):
    out = Image.new("RGBA", (im.width + 2 * m, im.height + 2 * m), (0, 0, 0, 0))
    out.alpha_composite(im, (m, m))
    return out


def textures():
    """(name, image)：res://p5joker/fx/<name>.png。"""
    layers = portrait_layers()
    yield from layers.items()
    yield "cs_body_line", outline(layers["cs_body"])
    yield "cs_warp", warp_map(layers["cs_body"])
    yield "cs_hand_line", outline(layers["cs_hand"])
    yield "cs_skyline", recolor(_rgba("city_skyline").crop((0, 351, 1536, 1024)))
    yield "cs_rooftop", _rgba("rooftop_foreground").crop((0, 305, 1536, 1024))
    yield "cs_info_head", info_head()
    yield "cs_info_ghost", info_ghost()
    yield "cs_slab_text", slab_text()
    yield "cs_eye_streak", eye_streak()
    yield "cs_card", _pad(_scale(_rgba("calling_card"), 0.25))
    yield "cs_button", button_icons()[0]


def button_icons():
    """角色按钮（原版 132x195）：生图 joker_button 按卡片比例裁上部；未解锁版去色压暗。"""
    W, H = 132, 195
    # 2026-11-19：Codex 专门画的按钮图（art/p5/ui/icons/joker_select_button.png，660x975 = 同比例 x5）；没有就退回从生图裁
    new = ROOT / "art/p5/ui/icons/joker_select_button.png"
    if new.exists():
        im = Image.open(new).convert("RGBA").resize((W, H), Image.LANCZOS)
    else:
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
