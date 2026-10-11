"""rig v7 召唤器合成：把 art/v12/transparent/evoker_prop.png 放进凝神 / 觉醒两张姿势图的手里。

层级：人物 → 召唤器 → 从原图切出的拳头层（肤色 + 描边），手背朝镜头时拳头盖住握把、套筒露在虎口上方。
每张图的放置参数（枪口点、缩放、角度、拳头区域）手工测量后写在 POSES 里；输出：
  art/v12/comp/<name>.png        合成后的整张（rig 用）
  art/v12/comp/<name>_evoker.png 只有召唤器（对齐后的单独一层，rig 里做后坐 / 闪光用）
  art/v12/comp/<name>_hand.png   只有拳头层
  art/rig_preview/v7_<name>_grip.png  放大 3 倍的握持特写
"""
import sys
from pathlib import Path

import cv2
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "art/v12/transparent"
OUT = ROOT / "art/v12/comp"
PREV = ROOT / "art/rig_preview"
PROP = SRC / "evoker_prop.png"
MUZZLE = (1221.0, 395.0)          # 道具图里枪口中心
# name: (输入图, 枪口落点, 缩放, 角度(度, 正 = 枪口朝下), 拳头 + 前臂框 x0,y0,x1,y1)
# 缩放 0.115：全长约 133px，约等于头宽；握把超出拳头的部分藏在手腕 / 前臂后面（遮挡层包含前臂）
POSES = {
    "focus": (SRC / "batch2_review_20261011/focus_b.png", (550.0, 141.0), 0.115, 2.0, (395, 108, 505, 265)),
    "awaken": (SRC / "batch2_review_20261011/awaken_a.png", (530.0, 148.0), 0.115, -6.0, (385, 128, 495, 262)),   # 开枪瞬间：枪口后坐上抬几度
}


def load(p):
    return np.asarray(Image.open(p).convert("RGBA")).astype(np.float32) / 255


def place_prop(muzzle, scale, angle, size=(1024, 1536)):
    """预乘 alpha 后先用 Lanczos 高质量缩小（一次缩到 0.1 倍若直接仿射会严重锯齿），再绕枪口旋转、平移到落点。"""
    prop = load(PROP)
    pm = prop.copy()
    pm[..., :3] *= pm[..., 3:]
    h, w = pm.shape[:2]
    sw, sh = max(1, round(w * scale)), max(1, round(h * scale))
    small = np.stack([np.asarray(Image.fromarray(pm[..., c]).resize((sw, sh), Image.LANCZOS)) for c in range(4)], -1)
    mx, my = MUZZLE[0] * sw / w, MUZZLE[1] * sh / h
    M = cv2.getRotationMatrix2D((mx, my), -angle, 1.0)
    M[:, 2] += np.array(muzzle) - np.array((mx, my))
    out = cv2.warpAffine(np.clip(small, 0, 1), M, size, flags=cv2.INTER_CUBIC, borderValue=(0, 0, 0, 0))
    out = np.clip(out, 0, 1)
    a = out[..., 3:]
    out[..., :3] = np.where(a > 1e-4, out[..., :3] / np.maximum(a, 1e-4), 0)
    return np.clip(out, 0, 1)


def hand_layer(img, box):
    x0, y0, x1, y1 = box
    rgb, a = img[..., :3], img[..., 3]
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    skin = (r > 0.78) & (g > 0.6) & (b > 0.5) & (r > b + 0.06) & (a > 0.5)
    m = np.zeros(a.shape, np.uint8)
    m[y0:y1, x0:x1] = skin[y0:y1, x0:x1]
    m = cv2.morphologyEx(m, cv2.MORPH_CLOSE, np.ones((5, 5), np.uint8))
    n, lab, st, _ = cv2.connectedComponentsWithStats(m, 8)
    if n > 1:                                                        # 指尖和拳头之间隔着描边：保留所有像样的肤色块，不只最大的那块
        keep = np.zeros(n, bool)
        keep[1:] = st[1:, cv2.CC_STAT_AREA] >= 25
        m = keep[lab].astype(np.uint8)
    filled = m.copy()
    cnts, _ = cv2.findContours(m, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
    cv2.drawContours(filled, cnts, -1, 1, -1)
    grown = cv2.dilate(filled, np.ones((7, 7), np.uint8))           # 带上手的黑色描边
    dark = rgb.max(-1) < 0.35
    mask = filled.astype(bool) | (grown.astype(bool) & dark)
    mask &= np.pad(np.ones((y1 - y0, x1 - x0), bool), ((y0, a.shape[0] - y1), (x0, a.shape[1] - x1)))
    soft = cv2.GaussianBlur(mask.astype(np.float32), (0, 0), 0.6)
    layer = img.copy()
    layer[..., 3] = a * np.clip(soft, 0, 1)
    return layer


def over(dst, src):
    a = src[..., 3:]
    out = dst.copy()
    out[..., :3] = src[..., :3] * a + dst[..., :3] * (1 - a)
    out[..., 3:] = a + dst[..., 3:] * (1 - a)
    return out


def to_img(a):
    return Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8), "RGBA")


def build(name):
    path, muzzle, scale, angle, box = POSES[name]
    img = load(path)
    gun = place_prop(muzzle, scale, angle)
    hand = hand_layer(img, box)
    comp = over(over(img, gun), hand)
    OUT.mkdir(parents=True, exist_ok=True)
    to_img(comp).save(OUT / f"{name}.png")
    to_img(gun).save(OUT / f"{name}_evoker.png")
    to_img(hand).save(OUT / f"{name}_hand.png")
    bg = Image.new("RGBA", (1024, 1536), (120, 130, 150, 255))
    bg.alpha_composite(to_img(comp))
    x0, y0, x1, y1 = box
    cx, cy = (x0 + muzzle[0]) / 2, (y0 + y1) / 2
    crop = bg.crop((int(cx - 110), int(cy - 90), int(cx + 110), int(cy + 90))).resize((660, 540), Image.LANCZOS)
    wide = bg.crop((250, 0, 750, 500)).resize((500, 500), Image.LANCZOS)
    sheet = Image.new("RGBA", (1160, 540), (0, 0, 0, 255))
    sheet.paste(crop, (0, 0))
    sheet.paste(wide, (660, 20))
    PREV.mkdir(parents=True, exist_ok=True)
    sheet.convert("RGB").save(PREV / f"v7_{name}_grip.png")


if __name__ == "__main__":
    for n in (sys.argv[1:] or ["focus"]):
        build(n)
