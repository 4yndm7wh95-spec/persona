"""新卡框的总览预览：3 种类型 × 3 种稀有度，各套一张 v3 卡图，带阿卡纳数字牌。
python3 tools/ui/card_frame_preview.py <新卡框小图目录> <输出.png>
"""
import sys, os
from PIL import Image, ImageDraw
sys.path.insert(0, os.path.dirname(__file__))
from card_compose import compose

V3 = "art/cards/v3/"
ROWS = [("attack", "strike_regent.png", "0"), ("skill", "defend_regent.png", "0"), ("power", "tyranny.png", "IV")]
RAR = ["common", "uncommon", "rare"]


def main(sprites, out):
    sc = 0.5
    W, H = int(600 * sc), int(844 * sc)
    sheet = Image.new("RGB", (len(RAR) * (W + 10) + 10, len(ROWS) * (H + 30) + 10), (21, 23, 28))
    d = ImageDraw.Draw(sheet)
    for i, (typ, por, arc) in enumerate(ROWS):
        for j, r in enumerate(RAR):
            c = compose(V3 + por, sprites, typ, r, False, arc).resize((W, H), Image.LANCZOS)
            x, y = 10 + j * (W + 10), 10 + i * (H + 30)
            sheet.paste(c, (x, y + 20), c)
            d.text((x, y + 2), f"{typ} / {r}", fill=(255, 220, 80))
    sheet.save(out)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
