"""弃牌堆（P5R ITEM 页）静态排版图 1920x1080。原版素材：CAMPITEM_2D01 立绘、菜单字图、星纹数据。
原版构图（frame 197，即序号 196）：立绘按 0.5 倍贴屏幕左上角；一条斜线（原版 x = 874 - 0.419 (y - 560)）右边，
立绘的黑色身体在运行时画成浅灰（立绘图本身全黑，撞色是游戏实时做的），衬在右边的黑色道具列表块上；底下 COMMAND 压在这片浅灰上。
我们的排版：立绘和斜线按同一个变换（缩放 0.488 / 0.5、平移 (-80, 0)，大小和消耗堆一致）放进来，牌格子在右边黑块里（原版道具列表的位置）。"""
import sys, glob, numpy as np
sys.path.insert(0, 'tools/p5/pile')
from mock_draw import stars, spr, place, outline, R, SS, W, H, FONT, CJK
from PIL import Image, ImageDraw, ImageFont

ART_K = 1500 / 3072            # 我们的立绘缩放（同消耗堆 PorW 1500）
ART_AT = (-80, 0)              # 立绘左上角：上边缘贴屏幕顶边（举起的手臂、头发都是被画布上缘截断的，同原版贴顶，不露截边）
MAP = ART_K / 0.5              # 原版屏幕 → 我们：原版立绘 0.5 倍、左上 (0, 0)
def m(p): return (p[0] * MAP + ART_AT[0], p[1] * MAP + ART_AT[1])
def Lx_orig(y): return 874 - 0.419 * (y - 560)
A_TOP, A_BOT = m((Lx_orig(-40), -40)), m((Lx_orig(1120), 1120))     # 我们排版里的撞色斜线（两端伸出屏幕）

def main(out):
    S = SS
    cv = Image.new('RGBA', (W * S, H * S))
    # 1. 底：红星纹（同另外两个牌堆）
    red = stars(W * S, H * S, base=160, n=8)
    v = np.asarray(red.convert('L'), np.float32) / 232.0
    base = np.array([138, 10, 18], np.float32)
    rgb = base * 0.5 * (1 - v[..., None]) + base * 0.86 * v[..., None]
    redbg = Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8)).convert('RGBA')
    cv.alpha_composite(redbg)
    d = ImageDraw.Draw(cv)
    # 2. 原版 ITEM 页的块面（frame 194–206 取中值后描的边，原版坐标 → m() 换到我们的排版）：
    #    黑块 = 撞色斜线右边、顶上一个红三角（星纹条和黑块之间）、右边一条斜边外是红底；右下一条浅灰斜带（原版放「消耗品 ●○○ Q/E」切分类），斜带下面是红底
    def lx(y): return A_TOP[0] + (A_BOT[0] - A_TOP[0]) * (y - A_TOP[1]) / (A_BOT[1] - A_TOP[1])
    TL, TR, BR = m((1456, 0)), m((1700, 0)), m((1916, 795))
    TIP, BOT = m((1309, 960)), m((1150, 1080))
    BOT2 = (BOT[0] + (BOT[0] - TIP[0]) / (BOT[1] - TIP[1]) * (H - BOT[1]), H)
    TRI = m((1078, 112))
    slab = [(lx(TRI[1]), TRI[1]), TL, (TR[0], 0), BR, TIP, BOT, BOT2, (lx(H), H)]
    d.polygon([(x * S, y * S) for x, y in slab], fill=(8, 8, 8))
    BAND = [m((1868, 632)), BR, TIP]
    globals()['BAND'] = BAND
    # 3. 黑白星纹条：原版在头的右边、斜线左边（PHANTOM / ITEM 那块）
    st = stars(W * S, H * S, base=118, n=8)
    band = [(690, 0), (lx(0), 0), (lx(440), 440), (740, 440)]       # 左边塞到头发后面、下边一直到肩膀（原版星纹条被头 / 肩挡住，不露红缝）
    mk = Image.new('L', (W * S, H * S)); ImageDraw.Draw(mk).polygon([(x * S, y * S) for x, y in band], fill=255)
    st.putalpha(mk); cv.alpha_composite(st)
    # 3b. 弃掉的牌：3 列放进黑块（右边是原版那条斜边，4 列会伸到红底上）。先画牌、再画立绘和斜带：
    #     牌只在黑色窗口里露出来，往下滚时从立绘的浅灰身体 / 斜带下面钻过去（原版道具列表也只在黑块里）
    fn = ImageFont.truetype(CJK, 15 * S)
    cols = [(182, 48, 40)] * 5 + [(40, 96, 160)] * 5 + [(196, 150, 40)] * 2
    cw, ch, cellw, cellh = 158, 222, 190, 252
    x0, y0 = 1062, 180
    for i, c in enumerate(cols):
        r_, k_ = divmod(i, 3)
        cd = Image.new('RGBA', (cw * S, ch * S)); cdr = ImageDraw.Draw(cd)
        cdr.rounded_rectangle([0, 0, cw * S - 1, ch * S - 1], 10 * S, fill=(30, 28, 32), outline=(150, 150, 150), width=3 * S)
        cdr.rectangle([10 * S, 26 * S, cw * S - 10 * S, 120 * S], fill=tuple(int(v * 0.7) for v in c))
        cdr.text((12 * S, 130 * S), '卡牌占位', font=fn, fill=(170, 170, 170))
        place(cv, cd, x0 + k_ * cellw + cellw / 2, y0 + r_ * cellh + cellh / 2, 1 / S, 0)
    # 4. Joker（原版 CAMPITEM_2D01）：斜线右边的黑色身体 → 浅灰、白描边 → 黑（原版的实时撞色）
    art = Image.open(glob.glob(R + 'art/portrait/BASE__P5_CAMPITEM_2D01__*.png')[0]).convert('RGBA')
    size = round(art.width * ART_K * S)
    a2 = np.asarray(art.resize((size, size), Image.LANCZOS)).astype(np.float32)
    ys, xs = np.mgrid[0:size, 0:size].astype(np.float32)
    X = xs / S + ART_AT[0]; Y = ys / S + ART_AT[1]
    right = X >= lx(Y)
    lum = a2[..., :3].mean(-1)
    inv = np.clip(222 - lum * (214 / 255), 8, 230)           # 黑 → 214 浅灰，白 → 8 黑
    a2[right, :3] = inv[right][:, None]
    lay = Image.fromarray(a2.astype(np.uint8))
    # 只贴一次：左边伸出屏幕的部分先裁掉（之前这里先把没裁的整张贴在 x=0、再贴裁过的，立绘错开 80px 叠了两层 = 重影）
    ox, oy = int(ART_AT[0] * S), int(ART_AT[1] * S)
    if ox < 0:
        lay = lay.crop((-ox, 0, lay.width, lay.height)); ox = 0
    cv.alpha_composite(lay, (ox, oy))
    # 5. 右上标题牌 MeNu&DISCARD（原版 MeNu& 字图 + P5 字图 DISCARD + 原版 COMMAND SELECT）
    plate = Image.new('RGBA', (430 * S, 130 * S)); pd = ImageDraw.Draw(plate)
    pd.rectangle([0, 0, 430 * S, 130 * S], fill=(235, 235, 235)); pd.rectangle([6 * S, 6 * S, 424 * S, 124 * S], fill=(10, 10, 10))
    place(cv, plate, 1680, 90, 1 / S, 4)
    place(cv, spr('00048'), 1555, 72, 0.36, 4)
    txt = Image.open('art/p5/ui/pile_text/txt_menu_discard.png').convert('RGBA')
    place(cv, txt, 1745, 64, 52 / txt.height, 4)
    place(cv, spr('00479'), 1685, 130, 0.5, 4)
    # 斜带和斜带下面的红底盖在牌上面（牌从下面钻过去）
    d = ImageDraw.Draw(cv)
    under = Image.new('L', (W * S, H * S)); ImageDraw.Draw(under).polygon([(x * S, y * S) for x, y in [BR, (W + 40, BR[1] + 8), (W + 40, H + 40), (BOT2[0], H + 40), BOT2, BOT, TIP]], fill=255)
    cv.paste(redbg, (0, 0), under)
    d.polygon([(x * S, y * S) for x, y in BAND], fill=(214, 214, 214))
    # 排列切换放在斜带上（原版这里是「消耗品 ●○○ Q/E」切分类）
    sw = Image.new('RGBA', (310 * S, 56 * S)); sd = ImageDraw.Draw(sw)
    sd.polygon([(0, 4 * S), (306 * S, 0), (310 * S, 52 * S), (2 * S, 56 * S)], fill=(240, 240, 240))
    sd.polygon([(4 * S, 8 * S), (302 * S, 4 * S), (306 * S, 48 * S), (6 * S, 52 * S)], fill=(10, 10, 10))
    sd.polygon([(8 * S, 10 * S), (154 * S, 7 * S), (156 * S, 46 * S), (10 * S, 49 * S)], fill=(230, 26, 28))
    for key, cx in (('order_zh', 81), ('bytype_zh', 230)):
        t = Image.open(f'art/p5/ui/pile_text/txt_{key}.png').convert('RGBA'); t = t.resize((max(1, int(t.width * 28 * S / t.height)), 28 * S), Image.LANCZOS)
        if key == 'bytype_zh': t = Image.fromarray((np.asarray(t) * np.array([0.55, 0.55, 0.55, 1])).astype(np.uint8))
        sw.alpha_composite(t, (int(cx * S - t.width / 2), 14 * S))
    place(cv, sw, 1600, 728, 1 / S, 16)
    # 7. COMMAND（原版字图，黑字压在浅灰身体上，同原版 ITEM 页）+ 说明
    from mock_draw import tint
    place(cv, tint(spr('00201'), (12, 12, 12)), 800, 940, 0.36, 4)      # 原版 ITEM 页：COMMAND 是黑字压在浅灰身体上
    cap = Image.new('RGBA', (420 * S, 60 * S)); ImageDraw.Draw(cap).text((6 * S, 6 * S), '弃牌堆 · 12 张', font=ImageFont.truetype(CJK, 30 * S), fill=(240, 240, 240), stroke_width=4 * S, stroke_fill=(10, 10, 10))
    place(cv, cap, 840, 1040, 1 / S, 4)
    cv.resize((W, H), Image.LANCZOS).convert('RGB').save(out, quality=92)

if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else 'docs/p5/preview/pile_discard_layout_v1.png')
