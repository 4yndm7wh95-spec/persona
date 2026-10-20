"""卡框整体提亮（v0.14.90，用户：整体太暗淡、沉闷）：按原版小图的区域（红=边框，棕=描述框）重新上色，保留新卡框的纹理明暗。"""
import sys, os, shutil
import numpy as np
from PIL import Image, ImageFilter
SRC = "research/card_frame_src/sprites/"

def ramp(l, stops):
    xs = np.array([s[0] for s in stops]); cs = np.array([s[1] for s in stops], float)
    return np.stack([np.interp(l, xs, cs[:, i]) for i in range(3)], -1)

def lum(a): return (a[..., 0] * 0.299 + a[..., 1] * 0.587 + a[..., 2] * 0.114) / 255

def norm(l, m):
    lo, hi = np.percentile(l[m], [2, 99.5]); return np.clip((l - lo) / max(hi - lo, 1e-3), 0, 1)

V = {
 "A": dict(border=[(0,(4,22,74)),(0.35,(18,84,200)),(0.65,(64,160,250)),(0.85,(150,214,255)),(1,(236,250,255))],
           desc_top=(20,58,140), desc_bot=(8,26,78), desc_pat=(70,130,220),
           common=[(0,(52,74,110)),(0.4,(150,176,208)),(0.7,(218,232,246)),(1,(255,255,255))],
           uncommon=[(0,(0,40,110)),(0.4,(0,140,230)),(0.7,(90,214,255)),(1,(230,252,255))]),
 "B": dict(border=[(0,(10,30,90)),(0.35,(30,110,220)),(0.65,(80,190,255)),(0.85,(170,232,255)),(1,(245,253,255))],
           desc_top=(26,74,168), desc_bot=(10,34,96), desc_pat=(90,160,240),
           common=[(0,(60,84,120)),(0.4,(166,190,220)),(0.7,(226,238,250)),(1,(255,255,255))],
           uncommon=[(0,(0,50,120)),(0.4,(0,160,240)),(0.7,(110,226,255)),(1,(236,253,255))]),
}

V["C"] = dict(V["A"], desc_top=V["B"]["desc_top"], desc_bot=V["B"]["desc_bot"], desc_pat=V["B"]["desc_pat"])

def main(sp, out, v):
    P = V[v]; os.makedirs(out, exist_ok=True)
    for f in os.listdir(sp): shutil.copy(os.path.join(sp, f), out)
    for t in ["attack", "skill", "power"]:
        n = f"card_frame_{t}_s.png"
        a = np.asarray(Image.open(os.path.join(sp, n)).convert("RGBA")).astype(float)
        o = np.asarray(Image.open(SRC + n).convert("RGBA")).astype(float)
        border = (o[..., 0] - o[..., 1] > 40) & (o[..., 3] > 0)
        desc = (~border) & (o[..., 3] > 0) & (np.abs(o[..., 0] - o[..., 1]) < 40) & (o[..., 0] > 30)
        # 区域边缘羽化，避免硬接缝
        bm = np.asarray(Image.fromarray((border * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.5))) / 255
        dm = np.asarray(Image.fromarray((desc * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.5))) / 255
        l = lum(a)
        lb = norm(l, border); cb = ramp(lb, P["border"])
        p50, p97 = np.percentile(l[desc], [50, 97]); ld = np.clip((l - p50) / max(p97 - p50, 1e-3), 0, 1)
        H = a.shape[0]; ys = np.linspace(0, 1, H)[:, None, None]
        rows = np.where(desc.any(1))[0]; y0, y1 = rows.min() / H, rows.max() / H
        g = np.clip((ys - y0) / (y1 - y0), 0, 1)
        base = np.array(P["desc_top"]) * (1 - g) + np.array(P["desc_bot"]) * g
        cd = base + (np.array(P["desc_pat"]) - base) * ld[..., None] * 0.7
        rgb = a[..., :3] * (1 - bm[..., None] - dm[..., None]).clip(0, 1) + cb * bm[..., None] + cd * dm[..., None]
        a[..., :3] = rgb
        Image.fromarray(a.clip(0, 255).astype(np.uint8)).save(os.path.join(out, n))
    # 阿卡纳数字牌：同边框配色
    t = np.asarray(Image.open(os.path.join(sp, "arcana_tab.png")).convert("RGBA")).astype(float)
    m = t[..., 3] > 128; t[..., :3] = ramp(norm(lum(t), m), P["border"])
    Image.fromarray(t.clip(0, 255).astype(np.uint8)).save(os.path.join(out, "arcana_tab.png"))
    # 横幅 / 卡图边框 / 标签：普通、罕见两套配色提亮（稀有已在 v0.14.86 提亮过）
    gen = "art/card_frame/gen/"
    for name in ["card_banner", "card_portrait_border_attack_s", "card_portrait_border_skill_s", "card_portrait_border_power_s", "card_portrait_border_plaque_s"]:
        g0 = np.asarray(Image.open(os.path.join(out, (name[:-2] if name.endswith("_s") else name) + "_common.png")).convert("RGBA")).astype(float)
        l = lum(g0); m = g0[..., 3] > 128; ln = norm(l, m)
        for r in ["common", "uncommon"]:
            b = g0.copy(); b[..., :3] = ramp(ln, P[r])
            base = name[:-2] if name.endswith("_s") else name
            Image.fromarray(b.astype(np.uint8)).save(os.path.join(out, f"{base}_{r}.png"))

# 用法：python3 tools/ui/frame_vivid.py art/card_frame/sprites_base art/card_frame/sprites C（采用的是 C 方案）
if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2], sys.argv[3])
