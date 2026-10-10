"""绿幕抠图：python3 tools/chroma_key.py 输入.png 输出.png
按"绿色超出红蓝的程度"计算透明度，并对边缘做去溢色。"""
import sys
import numpy as np
from PIL import Image

src, dst = sys.argv[1], sys.argv[2]
im = np.asarray(Image.open(src).convert("RGB")).astype(np.float32)
r, g, b = im[..., 0], im[..., 1], im[..., 2]
excess = g - np.maximum(r, b)                      # 绿色超出量
alpha = 1.0 - np.clip((excess - 40.0) / 90.0, 0.0, 1.0)
# 去溢色：把绿色压到不超过红蓝的较大值
g2 = np.minimum(g, np.maximum(r, b) + 10.0)
out = np.dstack([r, g2, b, alpha * 255.0]).clip(0, 255).astype(np.uint8)
img = Image.fromarray(out, "RGBA")
bbox = img.getchannel("A").point(lambda a: 255 if a > 20 else 0).getbbox()
print("bbox", bbox)
img.save(dst)
