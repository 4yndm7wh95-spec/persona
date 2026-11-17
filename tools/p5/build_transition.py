"""Joker 选人 → 进游戏过场的「时间图」（p5mod/src/Vfx/JokerTransition.cs 用），做法照原版 res://images/ui/transitions/*_transition.png：
一张图记录每个像素「什么时候变黑」，阈值从 0 推到 1。原版静默猎手：几道细刀痕先划过屏幕，再把刀痕围出的块一块块抹黑
（research/sts2_transitions/steps_silent.png）。Joker 版同一个节奏，加红：
  G：刀痕划到这里的时间（只在刀痕上有意义）；B：刀痕覆盖度（两头尖、抗锯齿）；
  R：这个像素变黑的时间 —— 刀痕围出的块从外往里一块块抹黑，每块从靠外的一侧扫过去。
输出 art/p5/fx/gen/joker_transition.png（1920×1080，无损），stage_p5.py 的 GEN_TEX 打进 res://p5joker/fx/。
加 --preview 还会用 numpy 模拟着色器，把几个时间点拼成一张图（给自己看构图）。"""
import sys
from pathlib import Path
import numpy as np
from PIL import Image
from scipy.ndimage import distance_transform_edt, label

ROOT = Path(__file__).resolve().parents[2]
W, H = 1920, 1080
OUT = ROOT / "art/p5/fx/gen/joker_transition.png"

# 刀痕：起点、终点（屏幕比例坐标，可以出界）、弯曲（垂直方向偏移 / 长度）、最粗处半宽（px）、开始划的时间、划完用时
CUTS = [
    ((-0.05, 0.80), (1.05, 0.16), 0.035, 6.5, 0.04, 0.075),
    ((0.17, -0.05), (0.43, 1.05), -0.025, 5.0, 0.10, 0.07),
    ((1.05, 0.97), (0.56, -0.05), 0.03, 7.0, 0.16, 0.07),
    ((-0.05, 0.28), (0.76, 1.05), -0.03, 5.0, 0.22, 0.07),
    ((0.63, 1.05), (0.94, -0.05), 0.025, 6.0, 0.28, 0.065),
    ((1.05, 0.63), (-0.05, 0.53), 0.04, 4.5, 0.33, 0.075),
    ((0.33, -0.05), (0.04, 0.58), -0.02, 4.0, 0.38, 0.06),
]
FILL0, FILL1, FILL_DUR = 0.42, 0.74, 0.085     # 抹黑：第一块开始 → 最后一块开始，每块扫过去的用时
LINE_TB = 0.9                                    # 刀痕本身最后才黑（着色器在 0.9 起整屏收黑）


def bezier(p0, p1, bend, n=4000):
    p0, p1 = np.array(p0) * [W, H], np.array(p1) * [W, H]
    d = p1 - p0
    nrm = np.array([-d[1], d[0]]) / np.linalg.norm(d)
    c = (p0 + p1) / 2 + nrm * bend * np.linalg.norm(d)
    s = np.linspace(0, 1, n)[:, None]
    return (1 - s) ** 2 * p0 + 2 * (1 - s) * s * c + s ** 2 * p1, s[:, 0]


def build():
    yy, xx = np.mgrid[0:H, 0:W]
    cover = np.zeros((H, W), np.float32)      # B
    tr = np.ones((H, W), np.float32)          # G
    best = np.full((H, W), 9.0, np.float32)   # 盖住这个像素的刀痕里最早的那道的时间
    near_d = np.full((H, W), 1e9, np.float32)
    for p0, p1, bend, hw, t0, dur in CUTS:
        pts, s = bezier(p0, p1, bend)
        ix, iy = np.round(pts[:, 0]).astype(int), np.round(pts[:, 1]).astype(int)
        ok = (ix >= 0) & (ix < W) & (iy >= 0) & (iy < H)
        mask = np.ones((H, W), bool)
        simg = np.zeros((H, W), np.float32)
        mask[iy[ok], ix[ok]] = False
        simg[iy[ok], ix[ok]] = s[ok]
        dist, (ny, nx) = distance_transform_edt(mask, return_indices=True)
        sn = simg[ny, nx]
        half = hw * np.clip(np.sin(np.pi * np.clip(sn, 0, 1)), 0, 1) ** 0.55          # 两头尖
        c = np.clip(half - dist + 0.5, 0, 1).astype(np.float32)
        # 划到的时间：沿刀痕走；离中线越远越晚一点 → 刀头是尖的
        t = t0 + dur * (sn + 0.05 * np.clip(dist / np.maximum(half, 0.5), 0, 3))
        t = np.minimum(t, 0.98).astype(np.float32)
        # 交叉处：只用最早划到的那道的形状和时间（后面那道不会提前冒出一截）
        win = (c > 0) & (t < best)
        best = np.where(win, t, best)
        cover = np.where(win, c, cover)
        # 刀痕外面一圈（没被任何刀痕盖住）：时间取最近那道的，过滤时不跳变
        closer = (dist < near_d) & (dist < half + 4)
        near_d = np.where(closer, dist, near_d)
        tr = np.where(closer, t, tr)
    tr = np.where(best < 9.0, best, tr)
    # 刀痕围出的块
    walls = cover > 0.35
    lab, n = label(~walls)
    tb = np.full((H, W), LINE_TB, np.float32)
    cx, cy = W / 2, H / 2
    regions = []
    for k in range(1, n + 1):
        m = lab == k
        area = m.sum()
        if area == 0:
            continue
        my, mx = yy[m].mean(), xx[m].mean()
        regions.append((k, area, mx, my, np.hypot((mx - cx) / H, (my - cy) / H)))
    regions.sort(key=lambda r: -r[4])                                  # 外面的先黑
    for i, (k, area, mx, my, dc) in enumerate(regions):
        m = lab == k
        start = FILL0 + (FILL1 - FILL0) * (i / max(1, len(regions) - 1))
        u = np.array([cx - mx, cy - my])
        u = u / (np.linalg.norm(u) + 1e-6) if np.linalg.norm(u) > 1 else np.array([1.0, 0.0])
        proj = (xx[m] - mx) * u[0] + (yy[m] - my) * u[1]
        f = (proj - proj.min()) / max(proj.max() - proj.min(), 1.0)      # 从靠外的一侧扫向靠里的一侧
        tb[m] = start + FILL_DUR * f
    # 半盖住的刀痕边缘：用最近的块的时间（着色器先画黑、再把刀痕画在上面）
    edge = walls
    _, (ny, nx) = distance_transform_edt(edge, return_indices=True)
    tb = np.where(edge & (cover < 0.999), np.minimum(tb[ny, nx], LINE_TB), tb)
    return tb, tr, cover


def shade(bg, tb, tr, cover, t):
    """numpy 版着色器（和 JokerTransition.cs 一致），用来看构图。"""
    red = np.array([0.86, 0.04, 0.11])
    col = bg.astype(np.float32) / 255.0
    tb = tb + (np.random.default_rng(1).random(tb.shape).astype(np.float32) - 0.5) * 0.004   # 抖一点，避免 8 位的色带
    blk = np.clip((t - tb) / 0.0015 + 0.5, 0, 1)[..., None]
    fresh = np.clip(1 - (t - tb) / 0.015, 0, 1)[..., None] * blk        # 刚盖住的地方先是暗红再变黑
    cov = blk
    col = col * (1 - cov) + (red * 0.35) * fresh
    drawn = (np.clip((t - tr) / 0.004 + 0.5, 0, 1) * cover)[..., None]
    age = (t - tr)[..., None]
    lc = np.where(age < 0.08, red + (np.array([1.0, 0.85, 0.8]) - red) * np.clip(1 - age / 0.08, 0, 1), red)
    fade = 1 - np.clip((t - 0.84) / 0.09, 0, 1)
    col = col * (1 - drawn * fade) + lc * drawn * fade
    final = np.clip((t - 0.9) / 0.07, 0, 1)
    col = col * (1 - final)
    return (np.clip(col, 0, 1) * 255).astype(np.uint8)


def main():
    tb, tr, cover = build()
    img = np.stack([tb, tr, cover], -1)
    OUT.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(np.round(np.clip(img, 0, 1) * 255).astype(np.uint8), "RGB").save(OUT)
    print("wrote", OUT.relative_to(ROOT))
    if "--preview" in sys.argv:
        bgp = sys.argv[sys.argv.index("--preview") + 1]
        out = sys.argv[sys.argv.index("--preview") + 2]
        bg = np.array(Image.open(bgp).convert("RGB").resize((W, H)))
        ts = [0.08, 0.16, 0.24, 0.32, 0.40, 0.48, 0.56, 0.64, 0.72, 0.80, 0.88, 0.95]
        cells = [Image.fromarray(shade(bg, tb, tr, cover, t)).resize((480, 270)) for t in ts]
        sheet = Image.new("RGB", (480 * 4, 270 * 3))
        for i, c in enumerate(cells):
            sheet.paste(c, ((i % 4) * 480, (i // 4) * 270))
        sheet.save(out)
        print("preview", out)


if __name__ == "__main__":
    main()
