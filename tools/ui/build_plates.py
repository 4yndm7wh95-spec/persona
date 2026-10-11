"""P3R 风格的界面"牌子"：离线生成 PNG（运行时系统字体做不出粗斜体 + 多层描边，所以设计字一律烘焙成图）。
- nameplate：选人界面介绍板的名字牌（日文"結城 理" + 罗马字 + ARCANA 0 / THE FOOL 标签）
- endturn：结束回合按钮底板（文字仍是原版标签，只烘焙底板和装饰）
- relic_tag：天赋名字下面的红色斜切标签条
输出到 art/ui/ 预览，并写入 mod/pck_stage/p3makoto/ui/。
用法：python3 tools/ui/build_plates.py"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools/pck"))
from godot_tex import write_texture  # noqa: E402

OUT = ROOT / "art/ui"
STAGE = ROOT / "mod/pck_stage"
CJK = "/usr/share/fonts/truetype/wqy/wqy-zenhei.ttc"
LATIN = "/usr/share/fonts/opentype/inter/Inter-BlackItalic.otf"
LATIN_B = "/usr/share/fonts/opentype/inter/Inter-ExtraBoldItalic.otf"

NAVY = (8, 18, 66)
DEEP = (3, 8, 34)
CYAN = (57, 216, 255)
RED = (230, 20, 56)
WHITE = (248, 250, 255)
INK = (2, 3, 12)

SS = 2          # 超采样


def poly(d, pts, fill):
    d.polygon([(x * SS, y * SS) for x, y in pts], fill=fill)


def slab(d, x0, x1, y0, y1, lean, fill):
    """斜切色块：上边比下边右移 lean。"""
    poly(d, [(x0 + lean, y0), (x1 + lean, y0), (x1, y1), (x0, y1)], fill)


def glyphs(text, font_path, size, stroke=0, skew=0.22, spacing=0.0):
    """文字遮罩（L）：stroke 加粗、右斜。返回 (mask, 左上偏移)。尺寸都按超采样后算。"""
    f = ImageFont.truetype(font_path, size * SS)
    probe = ImageDraw.Draw(Image.new("L", (1, 1)))
    w = int(sum(probe.textlength(c, font=f) for c in text) + spacing * SS * size * (len(text) - 1)) + (stroke * 2 + 40) * SS
    h = int(size * SS * 1.5)
    m = Image.new("L", (w + int(h * skew) + 20 * SS, h), 0)
    d = ImageDraw.Draw(m)
    x = (stroke + 10) * SS + h * skew
    for c in text:
        d.text((x, size * SS * 0.1), c, font=f, fill=255, stroke_width=stroke * SS, stroke_fill=255)
        x += probe.textlength(c, font=f) + spacing * SS * size
    m = m.transform(m.size, Image.AFFINE, (1, skew, 0, 0, 1, 0), resample=Image.BICUBIC)
    return m.crop(m.getbbox())


def paste_mask(im, mask, xy, color, alpha=1.0):
    layer = Image.new("RGBA", mask.size, color + (0,))
    layer.putalpha(mask.point(lambda v: int(v * alpha)))
    im.alpha_composite(layer, (int(xy[0] * SS), int(xy[1] * SS)))


def grow(mask, px):
    pad = px * SS + 2
    big = Image.new("L", (mask.width + pad * 2, mask.height + pad * 2), 0)
    big.paste(mask, (pad, pad))
    return big.filter(ImageFilter.MaxFilter(px * SS * 2 + 1)), pad / SS


def outlined_text(im, mask, xy, fill, outline=None, opx=0, shadow=None, sh_off=(0, 0)):
    """多层设计字：投影（外描边形状错开）→ 外描边 → 字。"""
    if outline is not None and opx > 0:
        o, pad = grow(mask, opx)
        if shadow is not None:
            paste_mask(im, o, (xy[0] - pad + sh_off[0], xy[1] - pad + sh_off[1]), shadow[:3], shadow[3] if len(shadow) > 3 else 1.0)
        paste_mask(im, o, (xy[0] - pad, xy[1] - pad), outline)
    paste_mask(im, mask, xy, fill)


def finish(im):
    return im.resize((im.width // SS, im.height // SS), Image.LANCZOS)


RED_F = (232, 36, 58)       # 选中行的红色错位细框
YELLOW = (255, 214, 40)
BLACK = (6, 8, 20)
ROW = (10, 14, 40)          # 列表行的深色底


def glyphs_c(text, font_path, size, squeeze=0.82, **kw):
    """窄体（P3R 的英文 / 数字都是压窄的粗斜体）。"""
    m = glyphs(text, font_path, size, **kw)
    return m.resize((max(1, int(m.width * squeeze)), m.height), Image.LANCZOS)


def outline_poly(d, pts, color, width):
    p = [(x * SS, y * SS) for x, y in pts]
    d.line(p + [p[0]], fill=color, width=width * SS, joint="curve")


def rot_label(im, text, xy, size, angle=24, fill=WHITE, outline=BLACK, opx=5):
    """行首旋转标签（LEADER / PARTY 那种）：粗斜体窄体白字 + 黑描边，逆时针转。xy 为中心。"""
    m = glyphs_c(text, LATIN, size, squeeze=0.78, skew=0.18)
    o, pad = grow(m, opx)
    tile = Image.new("RGBA", o.size, (0, 0, 0, 0))
    tile.paste(Image.new("RGBA", o.size, outline + (255,)), (0, 0), o)
    inner = Image.new("RGBA", m.size, fill + (255,))
    tile.paste(inner, (int(pad * SS), int(pad * SS)), m)
    tile = tile.rotate(angle, expand=True, resample=Image.BICUBIC)
    im.alpha_composite(tile, (int(xy[0] * SS - tile.width / 2), int(xy[1] * SS - tile.height / 2)))


def selected_row(d, x0, x1, y0, y1, lean):
    """选中行：白色斜切长条 + 右上错开的红色细框。"""
    outline_poly(d, [(x0 + lean + 10, y0 - 9), (x1 + lean + 10, y0 - 9), (x1 + 10, y1 - 9), (x0 + 10, y1 - 9)], RED_F + (255,), 3)
    slab(d, x0, x1, y0, y1, lean, WHITE + (255,))


def tarot_card(d, im, x, y, h, numeral="0"):
    """塔罗小卡（社群列表行首那张）：深蓝斜卡 + 白边 + 黑色罗马数字 / 数字。"""
    w = h * 0.62
    k = h * 0.12
    poly(d, [(x + k, y), (x + w + k, y), (x + w, y + h), (x, y + h)], BLACK + (255,))
    poly(d, [(x + k + 4, y + 4), (x + w + k - 4, y + 4), (x + w - 4, y + h - 4), (x + 4, y + h - 4)], WHITE + (255,))
    m = glyphs_c(numeral, LATIN, int(h * 0.62), skew=0.15)
    paste_mask(im, m, (x + (w + k) / 2 - m.width / SS / 2, y + h / 2 - m.height / SS / 2), BLACK)


def grad_fill(mask, top, bot):
    """竖向渐变填充的文字层（P3R 的字不用纯色）。"""
    h, w = mask.height, mask.width
    t = np.linspace(0, 1, h)[:, None]
    g = np.zeros((h, w, 4), np.uint8)
    for i in range(3):
        g[..., i] = (top[i] * (1 - t) + bot[i] * t).astype(np.uint8)
    g[..., 3] = np.asarray(mask)
    return Image.fromarray(g, "RGBA")


def nameplate():
    """名字字样（透明底，色块由运行时画，和正文块共用左右边缘）：深蓝渐变"結城 理" + 青色错位残影，
    后面隔开一段是浅灰渐变的大号"愚者"。参考点：名字字形左下角；返回 (图, 锚点, 名字字高)。"""
    name = glyphs("結城 理", CJK, 104, stroke=2, skew=0.0, spacing=0.04)
    ghost = glyphs("愚者", CJK, 120, stroke=0, skew=0.0)
    nw, nh = name.width / SS, name.height / SS
    gw, gh = ghost.width / SS, ghost.height / SS
    W, H = int(20 + nw + 90 + gw + 20), int(max(nh, gh) + 40)
    im = Image.new("RGBA", (W * SS, H * SS), (0, 0, 0, 0))
    base = H - 20                                           # 名字底边
    nx, ny = 20, base - nh
    im.alpha_composite(grad_fill(ghost, (222, 228, 240), (190, 200, 224)), (int((nx + nw + 90) * SS), int((base - gh * 0.92) * SS)))
    paste_mask(im, name, (nx + 7, ny + 5), CYAN, 0.65)
    im.alpha_composite(grad_fill(name, (40, 80, 220), (6, 14, 60)), (int(nx * SS), int(ny * SS)))
    return finish(im), (nx, base), nh


def endturn():
    """结束回合（460×150）：照搬 P3R 主菜单的"选中项"——白色楔形碎片（左宽右尖）+ 背后错开的黑色碎片 + 一道品红细边；
    文字（原版标签，换成红色粗字）压在碎片中间偏左 (≈215, 76)。"""
    W, H = 460, 150
    im = Image.new("RGBA", (W * SS, H * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    poly(d, [(30, 52), (414, 18), (330, 128), (52, 118)], BLACK + (255,))          # 黑影碎片（向左下错开）
    poly(d, [(50, 40), (440, 14), (360, 116), (66, 104)], (255, 92, 170, 255))      # 品红细边
    poly(d, [(56, 44), (424, 20), (350, 110), (70, 100)], WHITE + (255,))           # 白色楔形
    return finish(im)


def endturn_sel():
    """"该结束回合了"的提示层：楔形外一圈青色光边（运行时加法混合呼吸）。"""
    W, H = 460, 150
    im = Image.new("RGBA", (W * SS, H * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    poly(d, [(40, 34), (452, 8), (366, 124), (58, 112)], CYAN + (255,))
    return finish(im.filter(ImageFilter.GaussianBlur(6 * SS)))


def relic_tag():
    """天赋分隔条（640×40）：P3R 装备界面"追加效果"那种青色小胶囊标签（深蓝字，中英对照）+ 后面一串青色虚线。"""
    W, H = 640, 40
    im = Image.new("RGBA", (W * SS, H * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    en = glyphs_c("Relic", LATIN, 17, skew=0.18)
    zh = glyphs("・天赋", CJK, 17, stroke=1, skew=0.0)
    pw = 22 + en.width / SS + zh.width / SS + 20
    rrect(d, 4, 6, 4 + pw, 34, 6, CYAN + (255,))
    paste_mask(im, en, (16, 20 - en.height / SS / 2), BLACK)
    paste_mask(im, zh, (16 + en.width / SS + 4, 20 - zh.height / SS / 2), BLACK)
    x = 4 + pw + 16
    while x + 22 < W:
        slab(d, x, x + 22, 18, 22, 0, CYAN + (220,))
        x += 34
    return finish(im)


def rrect(d, x0, y0, x1, y1, r, fill):
    d.rounded_rectangle([x0 * SS, y0 * SS, x1 * SS, y1 * SS], radius=r * SS, fill=fill)


ROYAL_D = (12, 34, 150)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    # 选人介绍板（nameplate / relic_tag）暂时停用，只出结束回合按钮
    items = {"endturn": endturn(), "endturn_sel": endturn_sel()}
    for k, im in items.items():
        im.save(OUT / f"{k}.png")
        write_texture(STAGE, f"p3makoto/ui/{k}.png", im)
        print("staged", k, im.size)
    # 预览（深色底）
    sheet = Image.new("RGBA", (1040, 600), (12, 20, 52, 255))
    y = 10
    for im in items.values():
        sheet.alpha_composite(im, (20, y))
        y += im.height + 20
    sheet.save(OUT / "plates_preview.png")


if __name__ == "__main__":
    main()
