"""抽牌堆（P5R 主菜单）静态排版图：1920x1080。全部用原版素材 + 原版星纹数据。"""
import struct, sys, numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter
R = 'research/p5r/'
SP = R + 'ui/camp_main/sprites/'
W, H = 1920, 1080
SS = 2                                  # 超采样
FONT = 'tools/ui/fonts/BarlowCondensed-900Italic.ttf'
CJK = '/usr/share/fonts/truetype/wqy/wqy-zenhei.ttc'

def spr(n): return Image.open(SP + n + '.png').convert('RGBA')
def tint(im, rgb):
    a = im.getchannel('A'); out = Image.new('RGBA', im.size, rgb + (255,)); out.putalpha(a); return out
def outline(im, r):
    a = im.getchannel('A').filter(ImageFilter.MaxFilter(r * 2 + 1)); o = Image.new('RGBA', im.size, (10, 10, 10, 255)); o.putalpha(a); return o
def place(dst, im, x, y, scale=1.0, rot=0.0):
    im = im.resize((max(1, int(im.width * scale * SS)), max(1, int(im.height * scale * SS))), Image.LANCZOS)
    if rot: im = im.rotate(rot, Image.BICUBIC, expand=True)
    dst.alpha_composite(im, (int(x * SS - im.width / 2), int(y * SS - im.height / 2)))

# ── 星纹（原版 CAMPBGSTARANIME.DAT：49 颗星，1280x720 坐标）──
def stars(w, h, base=118, n=8, inner=0.46):
    d = open(R + 'ui/camp_main/raw/CAMPBGSTARANIME.DAT', 'rb').read()
    rows = [struct.unpack_from('>8i', d, o) for o in range(0x20, 0x20 + 49 * 32, 32)]
    k = w / 1280
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32) / k
    img = np.full((h, w), 232, np.float32)
    seg = np.pi / 5
    P1 = np.array([0, 1.0]); P2 = np.array([np.sin(seg) * inner, np.cos(seg) * inner])
    nn = np.array([P2[1] - P1[1], -(P2[0] - P1[0])]); nn /= np.linalg.norm(nn); c0 = nn @ P1
    for x, y, col, sc, rot, *_ in rows:
        s = sc / 1000 * base; th = np.deg2rad(rot / 1000)
        dx, dy = (xx - x / 1000) / s, (yy - y / 1000) / s
        px = dx * np.cos(th) + dy * np.sin(th); py = -dx * np.sin(th) + dy * np.cos(th)
        a = np.abs(np.mod(np.arctan2(px, -py), 2 * seg) - seg); rr = np.hypot(px, py)
        dist = (rr * np.sin(a) * nn[0] + rr * np.cos(a) * nn[1]) / c0
        ink = 8 if (col & 0xffffff00) == 0 else 96
        v = np.where(np.mod(n - 1 - np.floor(dist * n), 2) == 0, ink, 232)
        img = np.where(dist < 1, v, img)
    return Image.fromarray(img.astype(np.uint8)).convert('RGBA')

def main(out, card_on=True, bg='flat'):
    cv = Image.new('RGBA', (W * SS, H * SS))
    # 背景：不透明深红 + 轻微纸张明暗（低频，不是网点）
    g = np.linspace(0, 1, W * SS)[None, :] * 0.35 + np.linspace(0, 1, H * SS)[:, None] * 0.25
    rng = np.random.default_rng(3)
    paper = Image.fromarray((rng.random((H // 8, W // 8)) * 255).astype(np.uint8)).resize((W * SS, H * SS), Image.BICUBIC)
    p = np.asarray(paper, np.float32) / 255 - 0.5
    base = np.array([138, 10, 18], np.float32)
    rgb = base[None, None, :] * (1 - g[..., None]) * (1 + p[..., None] * 0.10)
    cv.alpha_composite(Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8)).convert('RGBA'))
    if bg == 'stars':
        # 左侧：同一份原版星纹，红上压暗红（同色系），远处感 = 放大 + 轻微模糊
        rs = stars(W * SS, H * SS, base=160, n=8)
        v = np.asarray(rs.convert('L'), np.float32) / 232.0           # 1=底色 0=星纹墨色
        dark = base * 0.50; lite = base * 0.86
        rgbs = (dark[None, None, :] * (1 - v[..., None]) + lite[None, None, :] * v[..., None]) * (1 - g[..., None] * 0.9) * (1 + p[..., None] * 0.08)
        rimg = Image.fromarray(np.clip(rgbs, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2 * SS)).convert('RGBA')
        cv.alpha_composite(rimg)
    # 星纹面板：右侧，左边是斜边
    st = stars(W * SS, H * SS)
    mask = Image.new('L', (W * SS, H * SS)); ImageDraw.Draw(mask).polygon([(1150 * SS, 0), (W * SS, 0), (W * SS, H * SS), (1000 * SS, H * SS)], fill=255)
    st.putalpha(mask); cv.alpha_composite(st)
    # 标题牌（原版字图 + 底板）
    def plate(x, y, w, h, rot, fill, edge):
        im = Image.new('RGBA', (int(w * SS) + 24, int(h * SS) + 24))
        dr = ImageDraw.Draw(im); dr.rectangle([12, 12, 12 + w * SS, 12 + h * SS], fill=edge)
        dr.rectangle([12 + 6 * SS, 12 + 6 * SS, 12 + w * SS - 6 * SS, 12 + h * SS - 6 * SS], fill=fill)
        place(cv, im, x, y, 1 / SS, rot)
    plate(1520, 72, 470, 92, 6, (10, 10, 10), (235, 235, 235)); place(cv, spr('00046'), 1520, 72, 0.58, 6)
    plate(1700, 150, 330, 96, 2, (235, 235, 235), (10, 10, 10)); place(cv, tint(spr('00047'), (10, 10, 10)), 1700, 150, 0.6, 2)
    # Joker + 白手 + 红手套（原版 CAMPTOP，离线 Lanczos 缩到显示尺寸）
    art = Image.open(R + 'art/portrait/BASE__P5_CAMPTOP_2D__e441c53ea3.png').convert('RGBA')
    A = 1200; place(cv, art, 788 + A / 2, 158 + A / 2, A / art.width)
    # 标题牌 MeNu&Main（右上，在 Joker 头右边）
    plate(1760, 300, 300, 130, -5, (10, 10, 10), (235, 235, 235))
    place(cv, spr('00048'), 1700, 288, 0.36, -5); place(cv, spr('00061'), 1830, 280, 0.36, -5)
    place(cv, spr('00479'), 1765, 340, 0.52, -5)
    # 牌数 = 原版页码大数字（白字黑描边，叠在身体上）
    for n, x, y, r in (('00035', 1700, 560, -4), ('00036', 1840, 572, 3)):
        im = spr(n); place(cv, outline(im, 9), x, y, 0.8, r); place(cv, tint(im, (240, 240, 240)), x, y, 0.8, r)
    # COMMAND（原版字图）+ 说明
    place(cv, outline(spr('00201'), 10), 1430, 955, 0.42, 4); place(cv, spr('00201'), 1430, 955, 0.42, 4)
    dr = ImageDraw.Draw(cv)
    f = ImageFont.truetype(CJK, 30 * SS)
    cap = Image.new('RGBA', (420 * SS, 60 * SS)); d2 = ImageDraw.Draw(cap)
    d2.text((6 * SS, 6 * SS), '抽牌堆 · 23 张', font=f, fill=(240, 240, 240), stroke_width=4 * SS, stroke_fill=(10, 10, 10))
    place(cv, cap, 1700, 1052, 1 / SS, 4)
    # 预告信：悬在白手掌心上方，正在转（横向压窄表示转到一半）
    if card_on:
      card = Image.open('art/p5/ui/pile/calling_card.png').convert('RGBA')
      sq = card.resize((int(card.width * 0.62), card.height), Image.LANCZOS)
      glow = Image.new('RGBA', (sq.width + 120, sq.height + 120)); glow.alpha_composite(tint(sq, (255, 255, 255)), (60, 60))
      glow = glow.filter(ImageFilter.GaussianBlur(26)); glow.putalpha(glow.getchannel('A').point(lambda v: v * 0.55))
      place(cv, glow, 1172, 590, 0.43, -8); place(cv, sq, 1172, 590, 0.43, -8)
    # 牌格子（左侧，占位卡）
    fb = ImageFont.truetype(FONT, 40 * SS); fz = ImageFont.truetype(CJK, 26 * SS); fn = ImageFont.truetype(CJK, 15 * SS)
    cols = {'攻击': (182, 48, 40), '技能': (40, 96, 160), '能力': (196, 150, 40)}
    secs = [('ATTACK', '攻击', 7), ('SKILL', '技能', 9), ('POWER', '能力', 3)]
    x0, y = 70, 120
    cw, ch, gap = 150, 210, 22
    hover = (0, 2)
    for si, (en, zh, n) in enumerate(secs):
        # 分区标题：黑色斜纸片 + 白字（P5 剪报风格）
        hd = Image.new('RGBA', (520 * SS, 70 * SS)); hdr = ImageDraw.Draw(hd)
        hdr.polygon([(0, 8 * SS), (300 * SS, 0), (310 * SS, 60 * SS), (6 * SS, 66 * SS)], fill=(10, 10, 10))
        hdr.text((18 * SS, 6 * SS), en, font=fb, fill=(240, 240, 240))
        hdr.text((200 * SS, 18 * SS), zh, font=fz, fill=(230, 25, 28))
        hdr.text((262 * SS, 18 * SS), '×%d' % n, font=fz, fill=(240, 240, 240))
        place(cv, hd, x0 + 250, y + 30, 1 / SS, 2)
        y += 72
        for i in range(n):
            r, c = divmod(i, 5)
            cx, cy = x0 + c * (cw + gap) + cw / 2, y + r * (ch + gap) + ch / 2
            if (si, i) == hover: continue
            cd = Image.new('RGBA', (cw * SS, ch * SS)); cdr = ImageDraw.Draw(cd)
            cdr.rounded_rectangle([0, 0, cw * SS - 1, ch * SS - 1], 10 * SS, fill=(28, 26, 30), outline=(200, 200, 200), width=3 * SS)
            cdr.rectangle([10 * SS, 26 * SS, cw * SS - 10 * SS, 110 * SS], fill=cols[zh])
            cdr.text((12 * SS, 120 * SS), '卡牌占位', font=fn, fill=(200, 200, 200))
            place(cv, cd, cx, cy, 1 / SS)
        y += ((n + 4) // 5) * (ch + gap) + 14
        if y > 1000: break
    # 悬停中的牌：放大 + 名牌
    si, i = hover; r, c = divmod(i, 5)
    hx, hy = x0 + c * (cw + gap) + cw / 2, 120 + 72 + r * (ch + gap) + ch / 2
    cd = Image.new('RGBA', (cw * SS, ch * SS)); cdr = ImageDraw.Draw(cd)
    cdr.rounded_rectangle([0, 0, cw * SS - 1, ch * SS - 1], 10 * SS, fill=(28, 26, 30), outline=(255, 255, 255), width=4 * SS)
    cdr.rectangle([10 * SS, 26 * SS, cw * SS - 10 * SS, 110 * SS], fill=cols['攻击'])
    place(cv, cd, hx, hy - 14, 1.28 / SS)
    npl = Image.new('RGBA', (260 * SS, 54 * SS)); nd = ImageDraw.Draw(npl)
    nd.polygon([(0, 6 * SS), (250 * SS, 0), (258 * SS, 48 * SS), (4 * SS, 54 * SS)], fill=(230, 25, 28))
    nd.text((16 * SS, 8 * SS), '悬停：名牌', font=fz, fill=(255, 255, 255), stroke_width=2 * SS, stroke_fill=(10, 10, 10))
    place(cv, npl, hx, hy + 150, 1 / SS, -3)
    # 排序切换（左上）
    sw = Image.new('RGBA', (300 * SS, 50 * SS)); sd = ImageDraw.Draw(sw)
    sd.polygon([(0, 4 * SS), (140 * SS, 0), (144 * SS, 46 * SS), (2 * SS, 50 * SS)], fill=(240, 240, 240))
    sd.text((14 * SS, 8 * SS), '原顺序', font=fz, fill=(10, 10, 10))
    sd.polygon([(150 * SS, 4 * SS), (290 * SS, 0), (294 * SS, 46 * SS), (152 * SS, 50 * SS)], fill=(10, 10, 10))
    sd.text((166 * SS, 8 * SS), '按类型', font=fz, fill=(240, 240, 240))
    place(cv, sw, 225, 60, 1 / SS, 2)
    cv.resize((W, H), Image.LANCZOS).convert('RGB').save(out, quality=92)

if __name__ == '__main__':
    a = sys.argv[1:]
    main(a[0] if a else 'docs/p5/preview/pile_draw_layout_v1.png', card_on='nocard' not in a, bg='stars' if 'stars' in a else 'flat')
