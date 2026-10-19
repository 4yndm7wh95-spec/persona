"""把 imagegen 画的新卡框小图对齐到原版小图的准确形状，并做出三种稀有度的配色。

python3 tools/ui/frame_postprocess.py <生成图目录> <输出目录>
- 生成图目录里放 imagegen 的原始输出，文件名和原版小图一致（card_frame_attack_s.png …），尺寸可以不同（会等比缩放到原版尺寸）；
- 输出：同名 PNG，尺寸和原版完全一样，透明度直接用原版的 alpha（形状严丝合缝）；
  卡框（card_frame_*）的卡图窗口（原版 alpha=0 的地方）保持透明；
- 横幅 / 卡图边框 / 类型标签（原版靠稀有度材质上色的三种）另外输出 _common / _uncommon / _rare 三份：
  按亮度重新映射到 银白 / 冰蓝 / 暗金 三套渐变（生成图本身画成银白色）。
"""
import os, sys
import numpy as np
from PIL import Image

SRC = "research/card_frame_src/sprites"
FRAMES = ["card_frame_attack_s", "card_frame_skill_s", "card_frame_power_s"]
TINTED = ["card_banner", "card_portrait_border_attack_s", "card_portrait_border_skill_s",
          "card_portrait_border_power_s", "card_portrait_border_plaque_s"]
# 亮度 → 颜色（取自 P3R 原版阿卡纳牌 T_FX_Arcana_01_Col 的银白，另加冰蓝、暗金两套）
RAMPS = {
    "common": [(0, (24, 34, 50)), (0.4, (110, 136, 164)), (0.7, (196, 214, 230)), (1, (244, 248, 252))],
    "uncommon": [(0, (8, 30, 64)), (0.4, (40, 112, 186)), (0.7, (120, 192, 240)), (1, (226, 246, 255))],
    # v0.14.86（用户：金卡偏暗淡）：整体提亮、更饱和的暖金
    "rare": [(0, (110, 72, 8)), (0.3, (222, 164, 36)), (0.6, (255, 214, 92)), (0.85, (255, 238, 170)), (1, (255, 252, 236))],
}


def ramp(l, stops):
    xs = np.array([s[0] for s in stops]); cs = np.array([s[1] for s in stops], float)
    return np.stack([np.interp(l, xs, cs[:, i]) for i in range(3)], -1)


def fit(gen, orig):
    w, h = orig.size
    g = gen.convert("RGBA")
    # 等比缩放后居中裁到原版尺寸
    sc = max(w / g.width, h / g.height)
    g = g.resize((max(w, round(g.width * sc)), max(h, round(g.height * sc))), Image.LANCZOS)
    l, t = (g.width - w) // 2, (g.height - h) // 2
    g = g.crop((l, t, l + w, t + h))
    a = np.asarray(g).astype(np.float32)
    a[..., 3] = np.asarray(orig.convert("RGBA"))[..., 3]
    return a


def main(gen_dir, out_dir):
    os.makedirs(out_dir, exist_ok=True)
    for name in FRAMES + TINTED:
        gp = os.path.join(gen_dir, name + ".png")
        if not os.path.exists(gp):
            print("missing", gp); continue
        orig = Image.open(os.path.join(SRC, name + ".png"))
        a = fit(Image.open(gp), orig)
        if name in FRAMES:
            Image.fromarray(a.astype(np.uint8)).save(os.path.join(out_dir, name + ".png"))
            continue
        lum = (a[..., 0] * 0.299 + a[..., 1] * 0.587 + a[..., 2] * 0.114) / 255
        lo, hi = np.percentile(lum[a[..., 3] > 128], [2, 99.5])
        lum = np.clip((lum - lo) / max(hi - lo, 1e-3), 0, 1)
        for r, stops in RAMPS.items():
            b = a.copy(); b[..., :3] = ramp(lum, stops)
            base = name[:-2] if name.endswith("_s") else name
            Image.fromarray(b.astype(np.uint8)).save(os.path.join(out_dir, f"{base}_{r}.png"))
    print("done")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
