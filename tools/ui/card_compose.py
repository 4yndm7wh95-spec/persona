"""按游戏的卡牌场景（research/card_frame_src/scenes/cards/card.tscn）把卡框小图拼成一张完整的卡（2 倍尺寸，600×844），用来预览新卡框。

用法：
  python3 tools/ui/card_compose.py 输出.png 卡图.png --sprites <小图目录> --type attack|skill|power --rarity common|uncommon|rare [--vanilla]
  --sprites 默认 research/card_frame_src/sprites（原版）；新卡框放在 art/card_frame/sprites/，文件名和原版一致，
  横幅 / 卡图边框 / 类型标签按稀有度分别是 card_banner_<rarity>.png、card_portrait_border_<type>_<rarity>.png、card_portrait_border_plaque_<rarity>.png（没有就用不带稀有度的）。
  --vanilla：按原版材质（hsv 着色器）给原版小图上色，用来和游戏截图对照。
"""
import argparse, os
import numpy as np
from PIL import Image

C = (300, 422)  # 卡牌中心（2 倍）
# 节点矩形（场景单位，×2 后相对中心）：left, top, right, bottom
RECT = {
    "portrait": (-125, -168, 125, 22),
    "frame": (-150, -211, 150, 211),
    "border": (-137.5, -164, 137.5, 46),
    "banner": (-163, -207, 164, -124),
    "plaque": (-30.5, 1, 30.5, 38),
    "arcana": (-42, 183, 42, 215),  # 新增：卡框下沿正中的阿卡纳数字牌（游戏里由 mod 加的节点，位置同此）
}
FONTS = ["mod/pck_stage/p3makoto/fonts/Cinzel-Bold.ttf", "/usr/share/fonts/truetype/liberation/LiberationSerif-Bold.ttf"]
# 原版材质（materials/cards/*）的 hsv 参数
FRAME_HSV = {"orange": (0.12, 1.5, 1.2)}
BANNER_HSV = {"common": (1.0, 0.0, 0.85), "uncommon": None, "rare": (0.563, 1.198, 1.14)}


def hsv(img, h, s, v):
    a = np.asarray(img.convert("RGBA")).astype(np.float32) / 255
    M = np.array([[0.2989, 0.5870, 0.1140], [0.5959, -0.2774, -0.3216], [0.2115, -0.5229, 0.3114]])
    yiq = a[..., :3] @ M.T
    hue = (1 - h) * 6.283185
    c, sn = np.cos(hue), np.sin(hue)
    i, q = yiq[..., 1].copy(), yiq[..., 2].copy()
    # GLSL：col.rgb *= hue_shift（行向量乘矩阵）
    yiq[..., 1] = i * c - q * sn
    yiq[..., 2] = i * sn + q * c
    yiq[..., 1:] *= s
    yiq *= v
    rgb = yiq @ np.linalg.inv(M).T
    a[..., :3] = np.clip(rgb, 0, 1)
    return Image.fromarray((a * 255).astype(np.uint8))


def rect(name):
    l, t, r, b = RECT[name]
    return (round(C[0] + 2 * l), round(C[1] + 2 * t), round(C[0] + 2 * r), round(C[1] + 2 * b))


def pick(d, base, rarity, typ=None):
    names = []
    if typ:
        names += [f"{base}_{typ}_{rarity}.png", f"{base}_{typ}_s_{rarity}.png", f"{base}_{typ}_s.png", f"{base}_{typ}.png"]
    names += [f"{base}_{rarity}.png", f"{base}_s_{rarity}.png", f"{base}_s.png", f"{base}.png"]
    for n in names:
        p = os.path.join(d, n)
        if os.path.exists(p):
            return p
    raise FileNotFoundError(f"{base} ({typ}, {rarity}) not in {d}")


def compose(portrait, sprites, typ, rarity, vanilla, arcana=None):
    card = Image.new("RGBA", (600, 844), (0, 0, 0, 0))

    def put(img, name):
        l, t, r, b = rect(name)
        card.alpha_composite(img.convert("RGBA").resize((r - l, b - t), Image.LANCZOS), (l, t))

    put(Image.open(portrait), "portrait")
    frame = Image.open(pick(sprites, "card_frame", rarity, typ))
    border = Image.open(pick(sprites, "card_portrait_border", rarity, typ))
    banner = Image.open(pick(sprites, "card_banner", rarity))
    if banner.size == (653, 145):
        # 原版 AtlasTexture 的 margin = Rect2(1, 23, 2, 25)：四周补透明边后再拉伸进节点矩形
        pad = Image.new("RGBA", (655, 170), (0, 0, 0, 0)); pad.paste(banner, (1, 23)); banner = pad
    plaque = Image.open(pick(sprites, "card_portrait_border_plaque", rarity))
    if vanilla:
        frame = hsv(frame, *FRAME_HSV["orange"])
        bh = BANNER_HSV[rarity]
        if bh:
            border, banner, plaque = (hsv(x, *bh) for x in (border, banner, plaque))
    put(frame, "frame")
    put(border, "border")
    put(banner, "banner")
    put(plaque, "plaque")
    tab = os.path.join(sprites, "arcana_tab.png")
    if arcana is not None and os.path.exists(tab):
        from PIL import ImageDraw, ImageFont
        put(Image.open(tab), "arcana")
        l, t, r, b = rect("arcana")
        font = ImageFont.truetype(next(f for f in FONTS if os.path.exists(f)), 34)
        d = ImageDraw.Draw(card)
        w = d.textlength(arcana, font=font)
        d.text(((l + r - w) / 2, t + 12), arcana, font=font, fill=(255, 255, 255), stroke_width=2, stroke_fill=(16, 36, 57))
    return card


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("out"); ap.add_argument("portrait")
    ap.add_argument("--sprites", default="research/card_frame_src/sprites")
    ap.add_argument("--type", default="attack"); ap.add_argument("--rarity", default="common")
    ap.add_argument("--vanilla", action="store_true")
    ap.add_argument("--plain", action="store_true", help="不加深色背景，输出透明 PNG")
    ap.add_argument("--arcana", default=None, help="阿卡纳罗马数字（例 XIII），需要 <sprites>/arcana_tab.png")
    a = ap.parse_args()
    card = compose(a.portrait, a.sprites, a.type, a.rarity, a.vanilla, a.arcana)
    if a.plain:
        card.save(a.out)
    else:
        bg = Image.new("RGBA", (600, 844), (21, 23, 28, 255)); bg.alpha_composite(card); bg.convert("RGB").save(a.out)
