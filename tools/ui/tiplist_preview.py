"""状态说明 P3R 滚动列表的静态预览（参数与 mod/src/Scenes/TipList.cs 的 Layout 一致）。
用法：python3 tools/ui/tiplist_preview.py <战斗截图或空> <输出.png> [滚动像素]"""
import math
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
LAT = str(ROOT / "mod/pck_stage/p3makoto/fonts/BarlowCondensed-900Italic.ttf")
CJK = "/usr/share/fonts/truetype/wqy/wqy-zenhei.ttc"   # 预览用；游戏里是思源黑体 Bold
NAVY = (8, 18, 61, 240)
WINE = (61, 8, 23, 240)
CYAN = (97, 214, 245)
RED = (227, 33, 59)
INK = (5, 10, 36)
TIPS = [("光谱偏移", "在你的回合开始时，将1张随机无色牌添加到你的手牌中。", 0), ("招架", "君王之剑现在能让你获得10点格挡。", 0),
        ("环绕轨道", "你每花费4能量，获得能量。", 0), ("能量", "能量用于打出你手牌中的卡牌。", 0), ("黑洞", "每当你花费或获得辉星时，对所有敌人造成3点伤害。", 0),
        ("群星之子", "每花费一点辉星，获得2点格挡。", 0), ("熔炉", "在你的回合开始时，铸造5。", 0), ("铸造", "每场战斗你第一次铸造时，将君王之剑加入你的手牌。为君王之剑增加额外伤害。", 0),
        ("保留", "保留的牌不会在回合结束时被弃掉。", 0), ("虚弱", "造成的攻击伤害减少25%。", 1), ("暗淡蓝点", "如果你在一回合内打出至少5张牌，则在你的下一回合开始时额外抽1张牌。", 0),
        ("下回合抽牌", "在你的下回合开始时，额外抽1张牌。", 0)]


def font(p, s):
    return ImageFont.truetype(p, s)


def wrap(text, f, width):
    lines, cur = [], ""
    for ch in text:
        if f.getlength(cur + ch) > width:
            lines.append(cur)
            cur = ch
        else:
            cur += ch
    return lines + [cur]


def main(bg, out, scroll=0.0):
    W, H = 1920, 1080
    im = Image.open(bg).convert("RGBA").resize((W, H)) if bg and Path(bg).exists() else Image.new("RGBA", (W, H), (40, 70, 40, 255))
    ft, fd = font(CJK, 24), font(CJK, 20)
    rows = []
    for t, d, debuff in TIPS:
        lines = wrap(d, fd, 300)
        rows.append((t, lines, debuff, 360, 44 + 28 * len(lines)))
    y, ys = 0, []
    for r in rows:
        ys.append(y)
        y += r[4] + 18
    content = y - 18
    window = min(content, H * 0.62)
    hit = (180, 380, 330, 760)          # 人物判定框（左侧玩家）
    w = 360
    base_x = hit[2] + 34
    center_y = min(max((hit[1] + hit[3]) / 2 - 30, window / 2 + 120), H - window / 2 - 40)
    top = center_y - window / 2
    half = window / 2
    bulge = 64 * min(max((len(rows) - 1) / 4, 0), 1)

    def ss(a, b, x):
        t = min(max((x - a) / (b - a), 0), 1)
        return t * t * (3 - 2 * t)
    for (t, lines, debuff, rw, rh), ry in zip(rows, ys):
        c = ry + rh / 2 - scroll
        d = (c - half) / half
        ad = abs(d)
        alpha = 1 - ss(0.62, 1.08, ad)
        sc = 1 - 0.08 * ss(0.3, 1, ad)
        arc = bulge * (1 - min(ad * ad, 1.6))
        x = base_x + arc
        if alpha < 0.01:
            continue
        lay = Image.new("RGBA", (W, H))
        dr = ImageDraw.Draw(lay)
        x0, x1, y0, y1, lean = -16, rw + 14, -6, rh + 6, 12
        ox, oy = x, top + c - rh / 2

        def P(pts):
            return [(ox + (px) * sc, oy + rh / 2 + (py - rh / 2) * sc) for px, py in pts]

        def box(a, b, cc, dd):
            return P([(a + lean, cc), (b + lean, cc), (b, dd), (a, dd)])
        dr.polygon(box(x0 + 8, x1 + 8, y0 + 8, y1 + 8), fill=(0, 0, 8, 115))
        dr.polygon(box(x0, x1, y0, y1), fill=WINE if debuff else NAVY)
        dr.polygon(P([(x0 + lean, y0), (x0 + lean + 9, y0), (x0 + 9, y1), (x0, y1)]), fill=(RED if debuff else CYAN) + (255,))
        rx = x0 + (x1 - x0) * 0.45
        dr.polygon(P([(rx, y1 - 4), (x1, y1 - 4), (x1, y1), (rx, y1)]), fill=RED + (255,))
        dr.line(box(x0, x1, y0, y1) + [box(x0, x1, y0, y1)[0]], fill=(255, 255, 255, 70), width=2)
        tx, ty = P([(14, 8)])[0]
        dr.text((tx, ty), t, font=font(CJK, int(24 * sc)), fill=(255, 158, 173) if debuff else (255, 255, 255), stroke_width=2, stroke_fill=INK)
        for i, ln in enumerate(lines):
            lx, ly = P([(14, 42 + 28 * i)])[0]
            dr.text((lx, ly), ln, font=font(CJK, int(20 * sc)), fill=(235, 228, 205))
        a = lay.split()[3].point(lambda v: int(v * alpha))
        lay.putalpha(a)
        im.alpha_composite(lay)
    dr = ImageDraw.Draw(im)
    hx, hy = base_x + 6, top - 68
    dr.polygon([(hx - 4, hy + 30), (hx + 190, hy + 30), (hx + 184, hy + 36), (hx - 10, hy + 36)], fill=NAVY)
    dr.polygon([(hx - 4, hy + 30), (hx + 8, hy + 30), (hx + 2, hy + 36), (hx - 10, hy + 36)], fill=CYAN + (255,))
    fl = font(LAT, 34)
    dr.text((hx, hy - 10), "STATUS", font=fl, fill=(255, 255, 255), stroke_width=4, stroke_fill=INK)
    dr.text((hx + fl.getlength("STATUS") + 8, hy - 6), f"×{len(rows)}", font=font(LAT, 30), fill=CYAN, stroke_width=4, stroke_fill=INK)
    if content > window + 1:
        tx = base_x - 16
        ln = window - 40
        dr.line([(tx, top + 20), (tx, top + 20 + ln)], fill=(255, 255, 255, 60), width=2)
        th = max(24, ln * window / content)
        u = scroll / max(1, content - window)
        dr.rectangle([tx - 2.5, top + 20 + (ln - th) * u, tx + 2.5, top + 20 + (ln - th) * u + th], fill=CYAN)
    dr.rectangle(hit, outline=(255, 80, 80), width=2)
    im.convert("RGB").save(out)


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "", sys.argv[2], float(sys.argv[3]) if len(sys.argv) > 3 else 0.0)
