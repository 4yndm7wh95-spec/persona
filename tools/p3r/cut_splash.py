"""P3R 营地菜单入水水花（原版贴图 T_UI_Camp_03/04/05）切成 10 帧，按原版顺序（大水花 → 水柱 → 渐散）。
图集里每帧占 961×949（03、04 各 2×2），05 是最后 2 帧（908×600，左右两格）。具体 UV 等布局数据导出后再核对。
python3 tools/p3r/cut_splash.py → art/p3menu_system/p3r/splash_00.png … splash_09.png（RGBA，原尺寸）
"""
import os
from PIL import Image

SRC = "research/p3r_camp/tex/Xrd777/UI/Camp/Root/Texture/T_UI_Camp_{}_texture.png"
OUT = "art/p3menu_system/p3r"
os.makedirs(OUT, exist_ok=True)
i = 0
for n, rows, cw, ch in (("03", 2, 961, 949), ("04", 2, 961, 949), ("05", 1, 908, 600)):
    im = Image.open(SRC.format(n)).convert("RGBA")
    for r in range(rows):
        for c in range(2):
            im.crop((c * cw, r * ch, (c + 1) * cw, (r + 1) * ch)).save(f"{OUT}/splash_{i:02d}.png")
            i += 1
print("frames", i)
