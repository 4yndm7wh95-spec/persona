"""消耗堆（P5R SKILL 页）静态排版图 1920x1080。原版素材：CAMPSKILL_2D01 立绘、CAMP.PLG 队伍行框、菜单字图、星纹数据。"""
import sys, numpy as np
sys.path.insert(0, 'tools/p5/pile')
import plg
from mock_draw import stars, spr, tint, place, outline, R, SS, W, H, FONT, CJK
from PIL import Image, ImageDraw, ImageFont

def main(out):
    cv = Image.new('RGBA', (W * SS, H * SS))
    # 1. 底：右侧红星纹（同抽牌堆的红底）
    red = stars(W * SS, H * SS, base=160, n=8)
    v = np.asarray(red.convert('L'), np.float32) / 232.0
    base = np.array([138, 10, 18], np.float32)
    rgb = base * 0.5 * (1 - v[..., None]) + base * 0.86 * v[..., None]
    cv.alpha_composite(Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8)).convert('RGBA'))
    d = ImageDraw.Draw(cv)
    S = SS
    # 2. 原版 SKILL 页的大斜块：黑色主块（中间到右上）+ 左下浅灰楔形（立绘的白色下半身接在这里）
    d.polygon([(560*S, 0), (1650*S, 0), (1920*S, 300*S), (1920*S, 760*S), (1350*S, 1080*S), (560*S, 1080*S)], fill=(8, 8, 8))
    d.polygon([(560*S, 1080*S), (560*S, 620*S), (1350*S, 1080*S)], fill=(214, 214, 214))
    # 黑白星纹条（原版立绘后面的那条斜带）
    st = stars(W * S, H * S, base=118, n=8)
    m = Image.new('L', (W * S, H * S)); ImageDraw.Draw(m).polygon([(600*S, 0), (900*S, 0), (700*S, 520*S), (560*S, 520*S)], fill=255)
    st.putalpha(m); cv.alpha_composite(st)
    # 3. Joker（原版 CAMPSKILL_2D01，按原版比例）
    art = Image.open(R + 'art/portrait/BASE__P5_CAMPSKILL_2D01__5af504dfb6.png').convert('RGBA')
    # 立绘下边缘（原图 y≈2212）落在屏幕底边外、左边缘（x=0，y 1568–2248）藏在左侧类型列表后面：用户 2026-11-22 不要露出立绘边缘
    A = 1500
    place(cv, art, -80 + A / 2, 40 + A / 2, A / art.width)
    # 4. 左：类型列表（原版队伍列表的锯齿行：PLG party 行框，黑底白边）
    P = {p['name']: p for p in plg.read(R + 'ui/camp_main/raw/CAMP.PLG') if p['v']}
    fb = ImageFont.truetype(FONT, 56 * S); fs = ImageFont.truetype(FONT, 26 * S); fz = ImageFont.truetype(CJK, 26 * S)
    rows = [('EXHAUST', '消耗', 12, None), ('ATTACK', '攻击', 4, (182, 48, 40)), ('SKILL', '技能', 6, (40, 96, 200)), ('POWER', '能力', 1, (220, 170, 40)), ('STATUS', '状态', 1, (120, 120, 120))]
    y = 30
    for i, (en, zh, n, col) in enumerate(rows):
        key = f'party_0{i + 1}_01'
        p = P[key]
        sc = 1.0 if i == 0 else 0.82
        layer = Image.new('RGBA', (560 * S, 200 * S)); ld = ImageDraw.Draw(layer)
        for t in range(0, len(p['idx']) - 2, 3):
            tri = [((p['v'][q][0] * sc + 30) * S, (p['v'][q][1] * sc + 20) * S) for q in p['idx'][t:t + 3]]
            ld.polygon(tri, fill=(240, 240, 240))
        inner = Image.new('RGBA', layer.size)
        a = layer.getchannel('A').filter(__import__('PIL.ImageFilter', fromlist=['x']).MinFilter(13 * S | 1))
        blk = Image.new('RGBA', layer.size, (8, 8, 8, 255)); blk.putalpha(a)
        layer.alpha_composite(blk)
        cv.alpha_composite(layer, (0, int(y * S)))
        ld = ImageDraw.Draw(cv)
        tx = 120 if i else 150
        ld.text(((tx) * S, (y + 22) * S), str(n), font=fb, fill=(240, 240, 240))
        ld.text(((tx + 80) * S, (y + 28) * S), zh, font=fz, fill=(240, 240, 240))
        ld.text(((tx + 82) * S, (y + 60) * S), en, font=fs, fill=(90, 220, 200) if i else (230, 25, 28))
        if col: ld.rectangle([(tx + 200) * S, (y + 68) * S, (tx + 200 + 16 * n) * S, (y + 78) * S], fill=col)
        y += 150 if i == 0 else 118
    # 5. 右上标题牌 MeNu&SKiLL（原版字图）
    plate = Image.new('RGBA', (380 * S, 130 * S)); pd = ImageDraw.Draw(plate)
    pd.rectangle([0, 0, 380 * S, 130 * S], fill=(235, 235, 235)); pd.rectangle([6 * S, 6 * S, 374 * S, 124 * S], fill=(10, 10, 10))
    place(cv, plate, 1700, 90, 1 / S, 4)
    place(cv, spr('00048'), 1610, 75, 0.36, 4); place(cv, spr('00226'), 1760, 80, 0.33, 4); place(cv, spr('00479'), 1705, 130, 0.5, 4)
    # 6. 右：消耗的牌（原版技能列表的位置），4 列
    fn = ImageFont.truetype(CJK, 15 * S)
    cols = [(182, 48, 40)] * 4 + [(40, 96, 160)] * 6 + [(196, 150, 40)] + [(110, 110, 110)]
    cw, ch, g = 160, 225, 12
    x0, y0 = 1150, 200
    for i, c in enumerate(cols):
        r_, k_ = divmod(i, 4)
        cd = Image.new('RGBA', (cw * S, ch * S)); cdr = ImageDraw.Draw(cd)
        cdr.rounded_rectangle([0, 0, cw * S - 1, ch * S - 1], 10 * S, fill=(30, 28, 32), outline=(150, 150, 150), width=3 * S)
        cdr.rectangle([10 * S, 26 * S, cw * S - 10 * S, 120 * S], fill=tuple(int(v * 0.7) for v in c))
        cdr.text((12 * S, 130 * S), '卡牌占位', font=fn, fill=(170, 170, 170))
        place(cv, cd, x0 + k_ * (cw + g) + cw / 2, y0 + r_ * (ch + g) + ch / 2, 1 / S, 0)
    # 7. COMMAND（原版）+ 说明
    place(cv, outline(spr('00201'), 10), 760, 960, 0.36, 4); place(cv, spr('00201'), 760, 960, 0.36, 4)
    cap = Image.new('RGBA', (420 * S, 60 * S)); ImageDraw.Draw(cap).text((6 * S, 6 * S), '消耗堆 · 12 张', font=ImageFont.truetype(CJK, 30 * S), fill=(240, 240, 240), stroke_width=4 * S, stroke_fill=(10, 10, 10))
    place(cv, cap, 800, 1050, 1 / S, 4)
    cv.resize((W, H), Image.LANCZOS).convert('RGB').save(out, quality=92)

if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else 'docs/p5/preview/pile_exhaust_layout_v1.png')
