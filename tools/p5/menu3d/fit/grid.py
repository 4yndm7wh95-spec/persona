"""量原版截图里的点：把原版第 idx 帧（序号）的一块区域放大、画上网格和坐标（1920×1080 坐标），存成 png 看。
用法：python3 grid.py <序号> <x0> <y0> <x1> <y1> <输出 png>（区域小于 500 宽时网格 20px，否则 50px）"""
import sys
from PIL import Image, ImageDraw
from common import orig
idx = int(sys.argv[1]); x0, y0, x1, y1 = map(int, sys.argv[2:6]); out = sys.argv[6]
c = orig(idx).crop((x0, y0, x1, y1)); s = min(1400 / (x1 - x0), 1000 / (y1 - y0))
c = c.resize((int((x1 - x0) * s), int((y1 - y0) * s)), Image.LANCZOS); d = ImageDraw.Draw(c)
step = 20 if (x1 - x0) < 500 else 50
for x in range((x0 // step + 1) * step, x1, step):
    X = (x - x0) * s; d.line([(X, 0), (X, c.height)], fill=(255, 0, 255) if x % 100 == 0 else (0, 160, 255), width=1)
    if x % 100 == 0: d.text((X + 2, 2), str(x), fill=(255, 255, 0))
for y in range((y0 // step + 1) * step, y1, step):
    Y = (y - y0) * s; d.line([(0, Y), (c.width, Y)], fill=(255, 0, 255) if y % 100 == 0 else (0, 160, 255), width=1)
    if y % 100 == 0: d.text((2, Y + 2), str(y), fill=(255, 255, 0))
c.save(out)
