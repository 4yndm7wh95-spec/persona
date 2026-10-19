"""把新卡图套进游戏里真实的卡框（攻击牌，用户截图 art/cards/ref/card_attack_solar_strike.png）看效果。

卡图窗口由原版 solar_strike 卡图模板匹配得到：卡图缩放到 486×369，左上角放在 (107,138)；
窗口形状（被卡框 / 卡名横幅 / 类型标签盖住后露出来的部分）存在 card_attack_portrait_mask.png。
用法：python3 tools/ui/card_mockup.py 输出.png 卡图1.png [卡图2.png ...]
"""
import sys
from PIL import Image, ImageDraw

REF = "art/cards/ref/card_attack_solar_strike.png"
MASK = "art/cards/ref/card_attack_portrait_mask.png"
W, H, X0, Y0 = 486, 369, 107, 138


def mock(portrait):
    card = Image.open(REF).convert("RGB")
    canvas = Image.new("RGB", card.size)
    canvas.paste(Image.open(portrait).convert("RGB").resize((W, H), Image.LANCZOS), (X0, Y0))
    card.paste(canvas, (0, 0), Image.open(MASK).convert("L"))
    return card


def main():
    out, paths = sys.argv[1], sys.argv[2:]
    cards = [mock(p) for p in paths]
    cw, ch = cards[0].size
    scale = 0.5
    tw, th = int(cw * scale), int(ch * scale)
    cols = min(4, len(cards))
    rows = (len(cards) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * (tw + 10) + 10, rows * (th + 28) + 10), "#111")
    d = ImageDraw.Draw(sheet)
    for i, (c, p) in enumerate(zip(cards, paths)):
        x, y = 10 + (i % cols) * (tw + 10), 10 + (i // cols) * (th + 28)
        sheet.paste(c.resize((tw, th), Image.LANCZOS), (x, y + 18))
        d.text((x, y), p.split("/")[-1], fill="yellow")
    sheet.save(out)


if __name__ == "__main__":
    main()
