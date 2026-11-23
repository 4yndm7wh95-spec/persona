"""交付前的逐帧对照图：原版第 a…b 帧（按立绘配准换到我们排版的坐标）和预览同一时刻的帧并排。
用法：python3 compare.py <输出 jpg> <预览帧目录 out/<场景>> <a> <b> <原版切立绘那一帧的序号> <我们切立绘的时刻（秒）>
       <原版立绘缩放> <原版立绘左上 x> <原版立绘左上 y> <我们立绘左上 x> <我们立绘左上 y> <我们立绘宽> <立绘原图宽> [打开时刻 0.5] [每帧秒数 0.0335]
例（消耗堆）：python3 compare.py /tmp/cmp.jpg tools/p5/fxpreview/out/pile_exhaust 112 124 123 0.428 0.50795 0.47 18.05 -80 40 1500 3072
预览里牌堆在 0.5s 打开（Preview.cs 的 p.At(0.5)），--write-movie 第一帧有一帧偏移，所以默认 open = 0.5 − 1/30。"""
import sys, glob, numpy as np
from PIL import Image, ImageDraw, ImageFont
from common import orig
out, fr = sys.argv[1], sys.argv[2]
a, b, cut = int(sys.argv[3]), int(sys.argv[4]), int(sys.argv[5])
hand = float(sys.argv[6])
os_, ox, oy, px, py, pw, aw = (float(v) for v in sys.argv[7:14])
OPEN = float(sys.argv[14]) if len(sys.argv) > 14 else 0.5 - 1 / 30
DT = float(sys.argv[15]) if len(sys.argv) > 15 else 0.0335
MK = pw / aw / os_                                   # 原版屏幕 → 我们
files = sorted(glob.glob(fr + '/f*.png'))
font = ImageFont.truetype('/usr/share/fonts/truetype/wqy/wqy-zenhei.ttc', 22)
CW, CH = 640, 360; rows = []
for i in range(a, b + 1):
    t = hand - (cut - i) * DT
    n = max(0, min(len(files) - 1, int(round((OPEN + t) * 30))))
    o = orig(i); om = o.resize((round(1920 * MK), round(1080 * MK)), Image.LANCZOS)
    c = Image.new('RGB', (1920, 1080), (30, 30, 30)); c.paste(om, (round(px - ox * MK), round(py - oy * MK)))
    row = Image.new('RGB', (CW * 2 + 12, CH + 34), (18, 18, 18))
    row.paste(c.resize((CW, CH), Image.LANCZOS), (0, 34)); row.paste(Image.open(files[n]).convert('RGB').resize((CW, CH), Image.LANCZOS), (CW + 12, 34))
    d = ImageDraw.Draw(row); d.text((6, 5), f'原版 frame {i + 1}', font=font, fill=(255, 220, 80)); d.text((CW + 18, 5), f'我们 t={t:.3f}s', font=font, fill=(255, 120, 120))
    rows.append(row)
s = Image.new('RGB', (rows[0].width, sum(r.height for r in rows)), (18, 18, 18)); y = 0
for r in rows: s.paste(r, (0, y)); y += r.height
s.save(out, quality=80); print(out, len(rows))
