"""牌组 / 弃牌堆 / 消耗堆三个界面的静态版式预览（坐标与 mod/src/Vfx/PileFx.cs 的 For(kind) / DeckDecor / DiscardDecor / ExhaustDecor 一致）。
卡牌用截图里裁出来的牌面充当；立绘用静态图（游戏里是会动的 Spine）。
用法：python3 tools/ui/pile_kinds_preview.py <截图> <输出目录>"""
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools/ui"))
import pile_preview as pp  # noqa: E402

W, H = 1920, 1080
CYAN = (96, 214, 244)
RED = (226, 32, 58)
NAVY = (8, 18, 61)

LAYOUT = {
    "deck": dict(cols=5, card_w=166, cell_w=186, cell_h=252, grid_x=1920 - 70 - 5 * 186, top=262, bottom=1010, slant=-0.05, title_x=930,
                 title="DECK", note="整副牌组 · 按类型 → 费用排列"),
    "discard": dict(cols=5, card_w=160, cell_w=178, cell_h=252, grid_x=200, top=270, bottom=1010, slant=-0.04, title_x=58,
                    title="DISCARD PILE", note="按进入牌堆的先后排列 · 最新在前"),
    "exhaust": dict(cols=5, card_w=160, cell_w=178, cell_h=252, grid_x=190, top=262, bottom=1010, slant=0.0, title_x=58,
                    title="EXHAUST PILE", note="本场战斗中消耗的牌 · 最新在前"),
}


def portrait(im, path, center, scale, clip=None):
    p = Image.open(ROOT / path).convert("RGBA")
    p = p.resize((int(p.width * scale), int(p.height * scale)), Image.LANCZOS)
    lay = Image.new("RGBA", (W, H))
    lay.alpha_composite(p, (int(center[0] - p.width / 2), int(center[1] - p.height / 2))) if center[1] - p.height / 2 >= 0 and center[0] - p.width / 2 >= 0 else lay.paste(p, (int(center[0] - p.width / 2), int(center[1] - p.height / 2)), p)
    if clip is not None:
        lay.putalpha(ImageChops.multiply(lay.split()[3], clip))
    im.alpha_composite(lay)


def word(im, text, at, rot, size, alpha, clip=None):
    """灰色立体大字（深灰投影 + 浅灰字身），at = 文字起点，rot = 顺时针角度（与 Godot 的 Rotation 一致）。"""
    f = pp.lat(size)
    l, t, r, b = f.getbbox(text)
    blk = Image.new("RGBA", (r - l + 60, b - t + 60))
    d = ImageDraw.Draw(blk)
    d.text((22 - l, 18 - t), text, font=f, fill=(118, 128, 142, int(255 * alpha)))
    d.text((-l, -t), text, font=f, fill=(214, 220, 230, int(255 * alpha)), stroke_width=3, stroke_fill=(150, 160, 175, int(255 * alpha)))
    big = Image.new("RGBA", (blk.width * 3, blk.height * 3))
    big.alpha_composite(blk, (blk.width, blk.height))
    big = big.rotate(-rot, center=(blk.width, blk.height), resample=Image.BICUBIC)
    lay = Image.new("RGBA", (W, H))
    lay.alpha_composite(big, (int(at[0] - blk.width), int(at[1] - blk.height))) if at[0] - blk.width >= 0 and at[1] - blk.height >= 0 else lay.paste(big, (int(at[0] - blk.width), int(at[1] - blk.height)), big)
    if clip is not None:
        lay.putalpha(ImageChops.multiply(lay.split()[3], clip))
    im.alpha_composite(lay)


def poly_mask(pts):
    m = Image.new("L", (W, H))
    ImageDraw.Draw(m).polygon(pts, fill=255)
    return m


def deck_decor(im):
    top, bot = np.array([820, 70.0]), np.array([600, 1100.0])
    n = np.array([1030, 220.0]) / np.hypot(1030, 220)
    d = ImageDraw.Draw(im)

    def strip(a, b, c):
        d.polygon([tuple(top + n * a), tuple(top + n * b), tuple(bot + n * b), tuple(bot + n * a)], fill=c)
    strip(0, 46, (8, 18, 61, 235))
    strip(52, 56, CYAN + (255,))
    strip(70, 73, RED + (255,))
    slab = [(-80, 70), (820, 70), (600, 1100), (-80, 1100)]
    clip = poly_mask(slab)
    lay = Image.new("RGBA", (W, H))
    ImageDraw.Draw(lay).polygon(slab, fill=(242, 247, 255, 247))
    im.alpha_composite(lay)
    portrait(im, "art/p3pile_deck/deck_base.png", (514, 546), 0.9, clip)


def discard_decor(im):
    d = ImageDraw.Draw(im)
    d.polygon([(1300, 1080), (1360, 1080), (1760, 330), (1720, 330)], fill=(140, 224, 255, 90))
    up = [(1240, 70), (1920, 70), (1920, 660)]
    lay = Image.new("RGBA", (W, H))
    ImageDraw.Draw(lay).polygon(up, fill=(242, 247, 255, 242))
    im.alpha_composite(lay)
    d = ImageDraw.Draw(im)
    d.polygon([(1500, 1080), (1920, 560), (1920, 1080)], fill=(242, 247, 255, 237))
    d.polygon([(1232, 70), (1240, 70), (1920, 668), (1920, 676)], fill=RED + (255,))
    portrait(im, "art/p3pile_discard/discard_base.png", (1552, 537), 0.72)


def exhaust_decor(im):
    im.alpha_composite(Image.new("RGBA", (W, H), (0, 5, 26, 128)))
    d = ImageDraw.Draw(im)
    for x in range(0, W, 96):
        d.line([(x, 0), (x, H)], fill=(115, 204, 255, 18))
    for y in range(0, H, 96):
        d.line([(0, y), (W, y)], fill=(115, 204, 255, 18))
    word(im, "EXHAUST", (1700, 1100), -90, 230, 0.22)
    portrait(im, "art/p3pile_exhaust/exhaust_base.png", (1500, 560), 0.8)


def grid(im, faces, lay, kind):
    names = ["打击", "打击", "陨星", "防御", "防御", "崇拜", "打击", "防御", "陨星", "崇拜"]
    sections = [("ATTACK", "攻击", names[:5]), ("SKILL", "技能", names[5:])] if kind == "deck" else [("RECENT", "最新在前", names[:8])]
    sc = lay["card_w"] / 260
    y = 0.0

    def scr(x, yy):
        sy = lay["top"] + yy
        return lay["grid_x"] + x + (sy - lay["top"]) * lay["slant"], sy
    d = ImageDraw.Draw(im)
    for en, zh, cards in sections:
        hx, hy = scr(0, y + 29)
        hx -= 14
        d.polygon([(hx + 12, hy - 22), (hx + 330, hy - 22), (hx + 320, hy + 22), (hx, hy + 22)], fill=(5, 8, 23, 240))
        d.polygon([(hx + 12, hy - 22), (hx + 24, hy - 22), (hx + 14, hy + 22), (hx + 2, hy + 22)], fill=CYAN + (255,))
        pp.put(im, pp.text_img(en, pp.lat(36), (255, 255, 255, 255)), (hx + 34, hy), "lm")
        pp.put(im, pp.text_img(zh, pp.cjk(20), (150, 220, 255, 255), bold=1), (hx + 44 + pp.lat(36).getlength(en), hy + 2), "lm")
        pp.put(im, pp.text_img(f"×{len(cards)}", pp.lat(38), CYAN + (255,)), (hx + 310, hy), "rm")
        wdt = lay["cols"] * lay["cell_w"]
        d.line([(hx + 338, hy), (hx + wdt - 6, hy)], fill=(150, 220, 255, 140), width=2)
        y += 58
        for i, n in enumerate(cards):
            cx, cy = scr((i % lay["cols"]) * lay["cell_w"] + lay["cell_w"] / 2, y + (i // lay["cols"]) * lay["cell_h"] + lay["cell_h"] / 2)
            spr = pp.card_sprite(faces[n], 1, sc).rotate(2 if lay["slant"] < 0 else (-2 if lay["slant"] > 0 else 0), expand=True, resample=Image.BICUBIC)
            if kind == "exhaust":
                a = np.asarray(spr).astype(np.float32)
                a[..., 0] *= 0.78
                a[..., 1] *= 0.87
                spr = Image.fromarray(a.astype(np.uint8), "RGBA")
            pp.put(im, spr, (cx, cy), "mm")
        y += math.ceil(len(cards) / lay["cols"]) * lay["cell_h"] + 16


def header(im, lay, n):
    tx = lay["title_x"]
    t = pp.text_img(lay["title"], pp.lat(92), (255, 255, 255, 255), stroke=5, stroke_fill=NAVY + (255,))
    pp.put(im, t, (tx, 90))
    pp.put(im, pp.text_img(str(n), pp.lat(112), CYAN + (255,), stroke=5, stroke_fill=NAVY + (255,)), (tx + 26 + t.width, 74))
    d = ImageDraw.Draw(im)
    d.polygon([(tx - 2, 200), (tx + 502, 200), (tx + 494, 228), (tx - 10, 228)], fill=(8, 18, 60, 240))
    d.polygon([(tx - 2, 200), (tx + 12, 200), (tx + 4, 228), (tx - 10, 228)], fill=CYAN + (255,))
    pp.put(im, pp.text_img(lay["note"], pp.cjk(19), (205, 237, 255, 255), bold=1), (tx + 26, 214), "lm")


def main(shot, outdir):
    out = Path(outdir)
    out.mkdir(parents=True, exist_ok=True)
    faces, _ = pp.deck(shot, "draw")
    for kind, decor in (("deck", deck_decor), ("discard", discard_decor), ("exhaust", exhaust_decor)):
        im = pp.background(1.0)
        pp.bubbles(im, np.random.default_rng(3), 1.0)
        decor(im)
        grid(im, faces, LAYOUT[kind], kind)
        header(im, LAYOUT[kind], 10 if kind == "deck" else 8)
        pp.chrome(im, shot)
        im.convert("RGB").save(out / f"pile_{kind}.png")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
