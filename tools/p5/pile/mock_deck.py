"""牌组（P5R EQUIP 页）静态排版图 1920x1080。原版素材：CAMPEQUIP_2D01 立绘、菜单字图、星纹数据。
原版构图（frame 247–257，即序号 246–256）：立绘 CAMPEQUIP_2D01 按 0.5 倍贴屏幕左上角（reg2d 配准：0.4996、(0, 0)）；
左上黑白星纹拼贴（MeNu&EQUIP 标题牌斜着压在上面）、中间黑块（「更换谁的装备？」的队伍名单）、一条从右上斜到左下的浅灰宽带
（Joker 的黑身体 / 持枪手臂压在上面，立绘自带的白斜条正好接上这条带）、右边装备栏、右下红底（枪伸进红底里）。
我们的排版：同一个变换（缩放 0.977、平移 (-80, 0)，同弃牌堆）放进来；名单 → 类型列表（点一行跳到那一段），装备栏 → 牌格子（4 列，在立绘上面一层，
枪在格子下面露出来）。"""
import sys, glob, numpy as np
sys.path.insert(0, 'tools/p5/pile')
from mock_draw import stars, spr, place, R, SS, W, H, CJK, tint
from mock_discard import txt, small_digits, digits, paste_at, tinted
from PIL import Image, ImageDraw, ImageFont

MAP = 1500 / 3072 / 0.5
ART_AT = (-80, 0)
def m(p): return (p[0] * MAP + ART_AT[0], p[1] * MAP + ART_AT[1])
# 原版 1920×1080 坐标的块面。两条分界线是从立绘自带的白斜条量出来的（用户 2026-11-24：色块要和立绘接上，不能错位）：
#   白斜条左边 x = 721 − 0.3275 y（往上延长 = 左上星纹拼贴的右边），右边 x = 1265 − 0.628 y（往上延长 = 右边浅灰板的左边）；
#   两线之间、身体上面是黑块（原版名单所在），白斜条只在身体上（立绘自带），所以背景里没有中间那条灰带
STAR = [(0, 0), (721, 0), (367, 1080), (0, 1080)]
GRAY = [(1265, 0), (1490, 0), (1600, 80), (1730, 300), (1300, 680), (1120, 760), (860, 1080), (587, 1080)]
RED = [(1490, 0), (1920, 0), (1920, 1080), (860, 1080), (1120, 760), (1300, 680), (1730, 300), (1600, 80)]
ROWS = [('deck', 20), ('attack', 8), ('skill', 9), ('power', 2), ('status', 0), ('curse', 1)]


def poly(d, pts, fill, S):
    d.polygon([(x * S, y * S) for x, y in (m(p) for p in pts)], fill=fill)


def main(out):
    S = SS
    cv = Image.new('RGBA', (W * S, H * S), (8, 8, 8, 255))
    # 1. 红星纹（右下）、黑底、浅灰宽带、左上黑白星纹拼贴
    red = stars(W * S, H * S, base=160, n=8)
    v = np.asarray(red.convert('L'), np.float32) / 232.0
    base = np.array([138, 10, 18], np.float32)
    redbg = Image.fromarray(np.clip(base * 0.5 * (1 - v[..., None]) + base * 0.86 * v[..., None], 0, 255).astype(np.uint8)).convert('RGBA')
    mk = Image.new('L', (W * S, H * S)); md = ImageDraw.Draw(mk)
    md.polygon([(x * S, y * S) for x, y in [m(p) for p in RED[:2]] + [(W + 40, -40), (W + 40, H + 40)] + [m(p) for p in RED[3:]]], fill=255)
    cv.paste(redbg, (0, 0), mk)
    d = ImageDraw.Draw(cv)
    poly(d, GRAY, (214, 214, 214), S)
    st = stars(W * S, H * S, base=118, n=8)
    sm = Image.new('L', (W * S, H * S)); ImageDraw.Draw(sm).polygon([(x * S, y * S) for x, y in [(-40, -40)] + [m(p) for p in STAR[1:3]] + [(-40, H + 40)]], fill=255)
    st.putalpha(sm); cv.alpha_composite(st)
    # 2. 标题牌 MeNu&DECK：原版 MeNu&EQUIP 斜着压在星纹拼贴上（左上，逆时针约 15°）
    plate = Image.new('RGBA', (430 * S, 130 * S)); pd = ImageDraw.Draw(plate)
    pd.rectangle([0, 0, 430 * S, 130 * S], fill=(235, 235, 235)); pd.rectangle([6 * S, 6 * S, 424 * S, 124 * S], fill=(10, 10, 10))
    t = Image.open('art/p5/ui/pile_text/txt_menu_deck.png').convert('RGBA')
    pl = Image.new('RGBA', plate.size); pl.alpha_composite(plate)
    hm = spr('00048'); hm = hm.resize((int(hm.width * 0.36 * S), int(hm.height * 0.36 * S)), Image.LANCZOS); pl.alpha_composite(hm, (14 * S, 8 * S))
    tt = t.resize((int(t.width * 52 * S / t.height), 52 * S), Image.LANCZOS); pl.alpha_composite(tt, (min(190 * S, pl.width - tt.width - 10 * S), 10 * S))
    cs = spr('00479'); cs = cs.resize((int(cs.width * 0.5 * S), int(cs.height * 0.5 * S)), Image.LANCZOS); pl.alpha_composite(cs, ((pl.width - cs.width) // 2, 76 * S))
    TITLE = pl
    # 3. 牌格子（原版装备栏的位置）：4 列，先画牌、再画立绘？—— 不：牌在立绘上面（枪在牌下面、格子下方露出来）
    art = Image.open(glob.glob(R + 'art/portrait/BASE__P5_CAMPEQUIP_2D01__*.png')[0]).convert('RGBA')
    k = 0.5 * MAP
    a2 = art.resize((round(art.width * k * S), round(art.height * k * S)), Image.LANCZOS)
    ox = int(ART_AT[0] * S)
    a2 = a2.crop((-ox, 0, a2.width, a2.height))
    cv.alpha_composite(a2.crop((0, 0, min(a2.width, cv.width), min(a2.height, cv.height))), (0, 0))
    fn = ImageFont.truetype(CJK, 15 * S)
    cols = [(182, 48, 40)] * 8 + [(40, 96, 160)] * 4
    cw, ch, cellw, cellh = 158, 222, 190, 252
    x0, y0 = 1090, 210
    for i, c in enumerate(cols):
        r_, k_ = divmod(i, 4)
        if y0 + r_ * cellh > 820: break
        cd = Image.new('RGBA', (cw * S, ch * S)); cdr = ImageDraw.Draw(cd)
        cdr.rounded_rectangle([0, 0, cw * S - 1, ch * S - 1], 10 * S, fill=(30, 28, 32), outline=(150, 150, 150), width=3 * S)
        cdr.rectangle([10 * S, 26 * S, cw * S - 10 * S, 120 * S], fill=tuple(int(v * 0.7) for v in c))
        cdr.text((12 * S, 130 * S), '卡牌占位', font=fn, fill=(170, 170, 170))
        # 第三排只露上半截（往下滚动的提示，下面是枪）
        if y0 + r_ * cellh + ch > 880:
            cut = int((880 - (y0 + r_ * cellh)) * S)
            if cut <= 0: continue
            cd = cd.crop((0, 0, cd.width, cut))
            fade = Image.linear_gradient('L').rotate(180).resize((cd.width, cd.height))
            a = np.asarray(cd).copy(); a[..., 3] = (a[..., 3] * (np.asarray(fade) / 255.0)).astype(np.uint8); cd = Image.fromarray(a)
            place(cv, cd, x0 + k_ * cellw + cellw / 2, y0 + r_ * cellh + cut / S / 2, 1 / S, 0)
            continue
        place(cv, cd, x0 + k_ * cellw + cellw / 2, y0 + r_ * cellh + ch / 2, 1 / S, 0)
    # 段标题（同另外三页：白边黑剪报纸片）
    def head(x, y, key, n):
        e = txt(key, 40); z = txt(key + '_zh', 28); num = small_digits(n, 34)
        w = e.width + z.width + num.width + 60
        hp = Image.new('RGBA', ((w + 20) * S, 70 * S)); hd = ImageDraw.Draw(hp)
        hd.polygon([(0, 2 * S), ((w + 4) * S, 0), ((w + 12) * S, 62 * S), (2 * S, 66 * S)], fill=(240, 240, 240))
        hd.polygon([(4 * S, 5 * S), (w * S, 3 * S), ((w + 8) * S, 58 * S), (6 * S, 62 * S)], fill=(10, 10, 10))
        hp = hp.resize((w + 20, 70), Image.LANCZOS)
        hp.alpha_composite(e, (16, 12)); hp.alpha_composite(tinted(z, (230, 26, 28)), (26 + e.width, 20)); hp.alpha_composite(num, (40 + e.width + z.width, 16))
        paste_at(cv, hp, x, y, 1.0, 1.5)
    head(1080, 140, 'attack', 8)
    # 4. 中间：类型列表（原版「更换谁的装备？」的队伍名单：白字斜排，选中那行垫青 / 红斜条）
    lx, ly, step = 880, 70, 88         # 原版名单 x 680–1060（我们 584–955），斜着往左下排
    for i, (key, n) in enumerate(ROWS):
        x = lx - i * 46; y = ly + i * step          # 跟着右边灰板的斜边往左下排（原版名单也是），最后一行不压到灰板上
        a = 1.0 if n > 0 or i == 0 else 0.42
        if i == 1:     # 选中行：原版名单第一行的青 / 红斜条（白底黑字）
            rb = Image.new('RGBA', (340 * S, 84 * S)); rd = ImageDraw.Draw(rb)
            rd.polygon([(0, 54 * S), (300 * S, 0), (334 * S, 78 * S), (30 * S, 84 * S)], fill=(230, 26, 28))
            rd.polygon([(0, 54 * S), (150 * S, 27 * S), (170 * S, 80 * S), (30 * S, 84 * S)], fill=(80, 230, 240))
            rd.polygon([(24 * S, 10 * S), (286 * S, 4 * S), (300 * S, 70 * S), (40 * S, 76 * S)], fill=(240, 240, 240))
            paste_at(cv, rb.resize((280, 84), Image.LANCZOS), x - 36, y - 18, 1.0)
        col = (12, 12, 12) if i == 1 else (240, 240, 240)
        nm = tinted(txt(key + '_zh', 44), col)
        if i != 1:
            o = tinted(txt(key + '_zh', 44), (10, 10, 10))
            for dx, dy in ((-3, 0), (3, 0), (0, -3), (0, 3)): paste_at(cv, o, x + dx, y + dy, a)
        paste_at(cv, nm, x, y, a)
        num = digits(n, 52) if i != 1 else tinted(digits(n, 52), (12, 12, 12))
        paste_at(cv, num, x + nm.width + 24, y - 6, a)
    # 5. 排列切换：右上（原版这里是红底上的装备栏右上角）
    sw = Image.new('RGBA', (310 * S, 56 * S)); sd = ImageDraw.Draw(sw)
    sd.polygon([(0, 4 * S), (306 * S, 0), (310 * S, 52 * S), (2 * S, 56 * S)], fill=(240, 240, 240))
    sd.polygon([(4 * S, 8 * S), (302 * S, 4 * S), (306 * S, 48 * S), (6 * S, 52 * S)], fill=(10, 10, 10))
    sd.polygon([(8 * S, 10 * S), (154 * S, 7 * S), (156 * S, 46 * S), (10 * S, 49 * S)], fill=(230, 26, 28))
    for key, cx in (('order_zh', 81), ('bytype_zh', 230)):
        t2 = Image.open(f'art/p5/ui/pile_text/txt_{key}.png').convert('RGBA'); t2 = t2.resize((max(1, int(t2.width * 28 * S / t2.height)), 28 * S), Image.LANCZOS)
        if key == 'bytype_zh': t2 = Image.fromarray((np.asarray(t2) * np.array([0.55, 0.55, 0.55, 1])).astype(np.uint8))
        sw.alpha_composite(t2, (int(cx * S - t2.width / 2), 14 * S))
    place(cv, sw, 1730, 1030, 1 / S, -2)                # 原版右下「C 返回 / 决定」的位置
    place(cv, TITLE, 1665, 84, 1 / S, -4)               # 标题牌：原版 MeNu&EQUIP 压在左上星纹上被身体挡住大半，我们放右上（同另外三页）
    # 6. COMMAND（原版：灰字压在黑身体上，下面一行说明）+ 说明
    place(cv, tint(spr('00201'), (214, 214, 214)), 330, 840, 0.36, 4)
    cap = Image.new('RGBA', (420 * S, 60 * S)); ImageDraw.Draw(cap).text((6 * S, 6 * S), '牌组 · 20 张', font=ImageFont.truetype(CJK, 30 * S), fill=(240, 240, 240), stroke_width=4 * S, stroke_fill=(10, 10, 10))
    place(cv, cap, 470, 985, 1 / S, 4)
    cv.resize((W, H), Image.LANCZOS).convert('RGB').save(out, quality=92)


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else 'docs/p5/preview/pile_deck_layout_v1.png')
