"""Joker 选人 → 进游戏的过场剪影（p5mod/src/Vfx/JokerTransition.cs 用）：
从 art/p5/master/joker_master_a.png 的 alpha 取剪影，放在 16:9 画布中间（高 = 画布 0.9），算距离场：
  R：剪影外的距离（0 = 剪影上，1 = 离剪影最远的角），过场的黑色从剪影往外漫的顺序；
  G：贴边的细距离（0.5 = 边，± 28px），画干净的剪影边和红色柔光；
  B：剪影本身（抗锯齿后的 alpha）。
输出 art/p5/fx/gen/joker_transition.png（stage_p5.py 的 GEN_TEX 打进 res://p5joker/fx/）。"""
from pathlib import Path
import numpy as np
from PIL import Image
from scipy.ndimage import distance_transform_edt

ROOT = Path(__file__).resolve().parents[2]
W, H = 1280, 720
src = Image.open(ROOT / "art/p5/master/joker_master_a.png").convert("RGBA")
a = np.array(src)[:, :, 3]
ys, xs = np.nonzero(a > 16)
crop = src.crop((xs.min(), ys.min(), xs.max() + 1, ys.max() + 1))
k = H * 0.9 / crop.height
crop = crop.resize((round(crop.width * k), round(crop.height * k)), Image.LANCZOS)
canvas = Image.new("L", (W, H), 0)
x0 = (W - crop.width) // 2
y0 = round(H * 0.06)
canvas.paste(crop.getchannel("A"), (x0, y0))
m = np.array(canvas).astype(np.float32) / 255.0
inside = m > 0.5
d_out = distance_transform_edt(~inside)
d_in = distance_transform_edt(inside)
sd = d_out - d_in                                    # + 外面 / - 里面（像素）
R = np.clip(d_out / d_out.max(), 0, 1)
G = np.clip(0.5 + sd / 56.0, 0, 1)
B = m
img = np.stack([R, G, B], -1)
out = ROOT / "art/p5/fx/gen/joker_transition.png"
Image.fromarray((img * 255 + 0.5).astype(np.uint8), "RGB").save(out)
print("wrote", out.relative_to(ROOT), W, H, "max dist", round(float(d_out.max())))
