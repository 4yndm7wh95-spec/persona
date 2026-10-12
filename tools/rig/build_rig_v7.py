"""rig v7（整身姿势图 + 换图过渡）：召唤器试点。

在 v5 骨骼（art/rig/v5_base，退回的那一版）上叠加 v7 层：
- 待机 / 商店 / 残血待机、举召唤器（aim_in / aim_loop / aim_out）、召唤（cast / cast_from_aim / attack_sovereign /
  sovereign_from_aim）、轻施法（cast_light）这 11 个动画换成 v7；持剑、斩击、受击、死亡暂时保留 v5。
- v7 动画里隐藏 v5 的所有层（地面阴影除外）；v5 动画开头显式隐藏 v7 层、恢复 v5 层。
- 每次换图：旧姿势青色残影（淡出 0.18s）+ 新姿势加色闪光（青 → 白，2 帧）。
- 觉醒（扣下召唤器）：身体被冲击顶一下、召唤器绕握把后坐、尖端闪光、眼中蓝光亮起再退去。
- 待机：整图轻微呼吸位移 + 眨眼（只换眼睛那一小块，art/v12/comp/idle_blink_patch.png）。
- mod 依赖的骨骼 hand / grip / muzzle / evoker_tip 每个姿势落在拳头 / 召唤器尖端；事件名和时间与 v5 完全一致。

坐标：v12 图（1024×1536，身高 ≈ 1505px，脚底 y≈1515）→ 骨骼单位：x = (px − 553)·S + 60.6，y = (1515 − py)·S，S = 0.98
（v5 人物身高 ≈ 1471 单位、脚底在 0、衬衫中心 x≈60.6，见 v7 标定）。贴图按 TEX 倍缩小打进新图集页。
"""
import copy
import json
import math
import sys
from pathlib import Path

import cv2
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(Path(__file__).resolve().parent))
sys.path.insert(0, str(ROOT / "tools/pck"))
import v7_evoker_comp as comp  # noqa: E402

BASE = ROOT / "art/rig/v5_base"
T = ROOT / "art/v12/transparent"
C = ROOT / "art/v12/comp"
OUT = ROOT / "art/rig/v7"
STAGE = ROOT / "mod/pck_stage"
REL = "animations/characters/regent"
S = 0.98
TEX = 0.5
PAGE = 2048
FPS = 30
PEAK = 0.07          # 觉醒冲击帧停留
SMEAR = 0.05         # 斩击拖影帧停留
IMPACT = 0.08        # 受击冲击帧停留

V7_ANIMS = ["idle_loop", "relaxed_loop", "low_health_loop", "aim_in", "aim_loop", "aim_out",
            "cast", "cast_from_aim", "attack_sovereign", "sovereign_from_aim", "cast_light",
            "sword_in", "ready_sword_loop", "sword_out", "attack", "attack_from_ready", "hurt", "die"]
SWORD_POSES = ["sword_rest", "sword_ready", "sword_windup", "sword_slash"]
# 第六批（可选）："放下途中"（胸口 → 待机之间，手放到腰的高度、空手）。图到了才启用
HAVE_LOW = (ROOT / "art/v12/transparent/focus_low_a.png").exists()
# 剑：拳头中心 / 剑尖（原图像素，自动检测剑身 + 握剑手，见提交记录里的标定图）
SWORD = {"sword_rest": ((358.2, 778.4), (807.1, 1263.9)), "sword_ready": ((234.6, 742.9), (821.2, 1378.1)),
         "sword_windup": ((479.9, 132.5), (13.1, 424.6)), "sword_slash": ((637.3, 843.7), (1012.5, 1327.4)),
         "sword_smear": ((665.0, 455.0), (998.0, 92.0)),
         # 第五批（手量）：举剑途中 / 收招途中
         "sword_raise": ((300.0, 264.0), (817.0, 24.0)), "sword_recover": ((468.0, 646.0), (1009.0, 1276.0))}
CONTRACT = ["hand", "grip", "muzzle", "evoker_tip"]
EVOKER_POSES = ["focus_mid", "focus", "awaken", "awaken_peak"]


# 各姿势图相对待机图的比例和水平偏移（生成模型每张画的人物大小不一样）：
# 召唤器三张：鞋和头量出来约 0.89~0.91，但整体身高与待机一致（1.0）——模型把头和鞋画得略大、腿略短。
# 用剪影叠图比较肩宽 / 躯干宽度，0.95 时与待机最吻合（头顶偏低是低头姿势本身），取 0.95。
# 以脚底中点 (553, 1515) 为基准缩放，脚仍踩在原地。水平：抬手 / 余势 整体右移 83px（头和鞋的特征点一致给出）；
# 觉醒右移 5px（躯干基本对齐；它的站姿本来就更开）
#
# 持剑 / 受击 / 死亡：头部特征点配准给出的比例 × 召唤器那组验证过的修正系数（0.95 / 0.891 = 1.066），
# 水平按躯干中心对齐（持剑弓步、斩击前冲、受击后仰的位移保留一部分，切回待机时躯干最多差 ~25px）。
# 第三项（可选）= 该图自己的基准点（缩放中心，映射到待机脚底中点）：跪地图以躯干 / 膝盖底，倒地横版图以腰 / 身体最低点。
POSE_T = {"idle": (1.0, 0.0), "focus_mid": (0.95, 0.0), "focus": (0.95, 0.0), "awaken": (0.95, 5.0),
          "cast_mid": (1.0, 83.0), "cast": (1.0, 83.0),
          "sword_rest": (1.0, 0.0), "sword_ready": (0.915, 10.0), "sword_windup": (0.95, -4.0), "sword_slash": (0.935, -25.0),
          "hurt": (1.0, 25.0), "die_kneel": (0.8, 0.0, (631.0, 1350.0)), "die_down": (1.06, 0.0, (777.0, 952.0)),
          # 第四批中间帧：都是从某张已校准的图编辑出来的 —— 先按特征点配准（只取缩放 + 平移）映射到那张图，再走那张图的校准
          "sword_smear": ("via", "sword_slash", (1.016, 11.2, -68.8)),
          "hurt_impact": ("via", "hurt", (0.962, 20.2, 58.1)),
          "die_fall": ("via", "die_kneel", (0.906, -424.7, 135.5)),
          "awaken_peak": ("via", "awaken", (0.993, -8.6, 5.6)),
          # 第五批过渡帧：只用鞋的特征点配准（tools/rig/v7_register.py，脚踩在原地不动），缩放都≈1
          "sword_raise": ("via", "sword_windup", (1.0, 0.5, 2.1)),
          "sword_recover": ("via", "sword_slash", (1.0, 0.9, 29.5)),
          "hurt_recover": ("via", "hurt", (1.0, -1.0, -2.2)),
          # 第六批：由 focus_mid 编辑出来，到图后用 tools/rig/v7_register.py 按鞋配准再填
          "focus_low": ("via", "focus_mid", (1.0, 0.0, 0.0))}


def U(p, pose=None):
    t = POSE_T.get(pose, (1.0, 0.0)) if pose else (1.0, 0.0)
    if t[0] == "via":
        k, tx, ty = t[2]
        return U((p[0] * k + tx, p[1] * k + ty), t[1])
    s, dx = t[0], t[1]
    px, py = t[2] if len(t) > 2 else (553.0, 1515.0)
    x = 553.0 + (p[0] - px) * s + dx
    y = 1515.0 + (p[1] - py) * s
    return ((x - 553.0) * S + 60.6, (1515.0 - y) * S)


def pose_scale(pose):
    t = POSE_T.get(pose, (1.0, 0.0)) if pose else (1.0, 0.0)
    if t[0] == "via":
        return t[2][0] * pose_scale(t[1])
    return t[0]


def pose_of(region):
    for p in sorted(POSE_T, key=len, reverse=True):
        if region == f"v7_{p}" or region.startswith(f"v7_{p}_"):
            return p
    return None


def load(p):
    return np.asarray(Image.open(p).convert("RGBA")).astype(np.float32) / 255


# ───────────────────────── 图像 ─────────────────────────
def sources():
    src = {
        "v7_idle": load(T / "idle_a.png"),
        "v7_idle_blink": load(C / "idle_blink_patch.png"),
        "v7_focus_mid": load(T / "focus_mid_a.png"),
        "v7_focus": load(comp.POSES["focus"][0]),
        "v7_awaken": load(comp.POSES["awaken"][0]),
        "v7_cast_mid": load(T / "cast_mid_a.png"),
        "v7_cast": load(T / "cast_a.png"),
        "v7_sword_rest": load(T / "sword_rest_a.png"),
        "v7_sword_ready": load(T / "sword_ready_a.png"),
        "v7_sword_windup": load(T / "sword_windup_a.png"),
        "v7_sword_slash": load(T / "sword_slash_b.png"),
        "v7_hurt": load(T / "hurt_b.png"),
        "v7_die_kneel": load(T / "die_kneel_a.png"),
        "v7_die_down": load(T / "die_down_wide_a.png"),
        "v7_sword_smear": load(T / "sword_smear_a.png"),
        "v7_hurt_impact": load(T / "hurt_impact_a.png"),
        "v7_die_fall": load(T / "die_fall_a.png"),
        "v7_sword_raise": load(T / "sword_raise_a.png"),
        "v7_sword_recover": load(T / "sword_recover_a.png"),
        "v7_hurt_recover": load(T / "hurt_recover_a.png"),
        **({"v7_focus_low": load(T / "focus_low_a.png")} if HAVE_LOW else {}),
        "v7_awaken_peak": load(comp.POSES["awaken_peak"][0]),
        "v7_awaken_peak_eyeglow": load(C / "awaken_peak_eyeglow.png"),
        "v7_awaken_eyeglow": load(C / "awaken_eyeglow.png"),
    }
    for p in EVOKER_POSES:
        src[f"v7_{p}_evoker"] = load(C / f"{p}_evoker.png")
        src[f"v7_{p}_hand"] = load(C / f"{p}_hand.png")
    # 召唤器尖端闪光（加色柔光点 + 横向短光）
    n = 160
    ys, xs = np.mgrid[0:n, 0:n].astype(np.float32) - n / 2
    r2 = xs * xs + ys * ys
    a = np.clip(np.exp(-r2 / (2 * 14 ** 2)) + 0.45 * np.exp(-r2 / (2 * 40 ** 2)) + 0.6 * np.exp(-(ys / 3) ** 2) * np.exp(-np.abs(xs) / 30), 0, 1)
    tip = np.zeros((n, n, 4), np.float32)
    tip[..., :3] = (0.8, 0.96, 1.0)
    tip[..., 3] = a
    src["v7_tipflash"] = tip
    return src


def trim(img):
    ys, xs = np.nonzero(img[..., 3] > 0.004)
    x0, y0, x1, y1 = xs.min(), ys.min(), xs.max() + 1, ys.max() + 1
    x0, y0 = max(0, x0 - 2), max(0, y0 - 2)
    x1, y1 = min(img.shape[1], x1 + 2), min(img.shape[0], y1 + 2)
    return img[y0:y1, x0:x1], (x0, y0, x1, y1)


def shrink(img, k):
    pm = img.copy()
    pm[..., :3] *= pm[..., 3:]
    h, w = pm.shape[:2]
    sw, sh = max(1, round(w * k)), max(1, round(h * k))
    out = np.stack([np.asarray(Image.fromarray(pm[..., c]).resize((sw, sh), Image.LANCZOS)) for c in range(4)], -1)
    out = np.clip(out, 0, 1)
    a = out[..., 3:]
    out[..., :3] = np.where(a > 1e-4, out[..., :3] / np.maximum(a, 1e-4), 0)
    return np.clip(out, 0, 1)


def pack(regions):
    """简单的行式装箱，多页 PAGE×PAGE。regions: name -> 缩小后的 RGBA。返回 [(page_img, [(name, x, y, w, h)])]。"""
    items = sorted(regions.items(), key=lambda kv: -kv[1].shape[0])
    pages, cur, x, y, row = [], [], 0, 0, 0
    for name, im in items:
        h, w = im.shape[:2]
        if x + w > PAGE:
            x, y, row = 0, y + row + 2, 0
        if y + h > PAGE:
            pages.append(cur)
            cur, x, y, row = [], 0, 0, 0
        cur.append((name, x, y, w, h))
        x += w + 2
        row = max(row, h)
    if cur:
        pages.append(cur)
    out = []
    for pg in pages:
        canvas = np.zeros((PAGE, PAGE, 4), np.float32)
        for name, x, y, w, h in pg:
            canvas[y:y + h, x:x + w] = regions[name]
        hmax = max(y + h for _, x, y, w, h in pg)
        hp = 1 << int(math.ceil(math.log2(hmax)))
        out.append((Image.fromarray((canvas[:hp] * 255).astype(np.uint8), "RGBA"), pg))
    return out


# ───────────────────────── 几何 ─────────────────────────
def grip_px(name):
    """召唤器握把（拳头里）和尖端在原图里的像素位置（按 v7_evoker_comp 的放置参数反算）。"""
    path, muzzle, s, ang, box = comp.POSES[name]
    t = math.radians(ang)
    R = np.array([[math.cos(t), -math.sin(t)], [math.sin(t), math.cos(t)]])
    g = np.array(muzzle) + R @ ((np.array([255.0, 720.0]) - np.array(comp.MUZZLE)) * s)
    return (float(g[0]), float(g[1])), (float(muzzle[0]), float(muzzle[1]))


def skin_blob(img, box):
    x0, y0, x1, y1 = box
    a = (img[..., :3] * 255).astype(int)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    m = (r > 200) & (g > 150) & (b > 120) & (r > b + 15) & (img[..., 3] > 0.5)
    sub = np.zeros_like(m)
    sub[y0:y1, x0:x1] = m[y0:y1, x0:x1]
    ys, xs = np.nonzero(sub)
    return (float(xs.mean()), float(ys.mean())) if len(xs) else ((x0 + x1) / 2, (y0 + y1) / 2)


# ───────────────────────── 网格微动 ─────────────────────────
GRID = 28            # 网格间距（原图像素）
NO_MESH = {"die_kneel", "die_fall", "die_down"}     # 跪地 / 倒地不做呼吸和发梢（整张图）
# 循环姿势里会动的手 / 衣摆（手工圈的原图像素区域 + 权重从 y0 到 y1 由 0 渐变到 1）。
# 外套和裤子都是黑色，靠颜色分不开，所以手工圈；握剑的手连着剑，不动。
EXTRA = {
    "idle": {"arm": [([(300, 470), (405, 470), (408, 860), (316, 860)], (520, 820))],
             "coat": [([(385, 556), (500, 556), (500, 716), (385, 716)], (580, 712)),
                      ([(638, 556), (722, 556), (722, 672), (638, 672)], (570, 668))]},
    "sword_ready": {"arm": [([(700, 560), (805, 560), (885, 822), (775, 834)], (590, 800))],
                    "coat": [([(272, 520), (420, 520), (420, 706), (272, 706)], (556, 702))]},
    "focus": {"coat": [([(310, 540), (456, 540), (456, 716), (310, 716)], (562, 712)),
                       ([(668, 528), (752, 528), (752, 652), (668, 652)], (540, 648))]},
}


def _region_weight(shape, regions):
    """手工区域 → 平滑权重图（多边形填充、羽化，再乘竖直渐变）。"""
    from PIL import ImageDraw as _D, ImageFilter as _F
    out = np.zeros(shape, np.float64)
    for pts, (ya, yb) in regions:
        m = Image.new("L", (shape[1], shape[0]), 0)
        _D.Draw(m).polygon(pts, fill=255)
        m = np.asarray(m.filter(_F.GaussianBlur(14))).astype(np.float64) / 255
        yy = np.arange(shape[0], dtype=np.float64)[:, None]
        out = np.maximum(out, m * _smoothstep(ya, yb, yy))
    return out


def _smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def _box_mean(m, r):
    """m 的 (2r+1)² 窗口均值（积分图）。"""
    p = np.pad(m.astype(np.float64), ((r + 1, r), (r + 1, r)), mode="constant")
    c = p.cumsum(0).cumsum(1)
    k = 2 * r + 1
    return (c[k:, k:] - c[:-k, k:] - c[k:, :-k] + c[:-k, :-k]) / (k * k)


def mesh_weights(img, sword=None, pose=None):
    """每个像素的 (呼吸权重 b, 发梢权重 h)。
    · 呼吸：胯部（人物高度 50%）以下 0、肩（22%）以上 1，平滑过渡 —— 脚锁死，上身随呼吸抬起，头跟着上身走。
    · 发梢：深蓝头发像素，离头部中心越远权重越大（发根 0、发梢 1）；皮肤（脸 / 手）附近强制 0，脸绝不变形。
    · 剑（亮灰）：整把剑跟握剑的拳头同一个呼吸权重、不带发梢权重 —— 剑不会被拉弯。"""
    a = img[..., 3] > 0.5
    rgb = img[..., :3]
    ys, xs = np.nonzero(a)
    yt, yb = ys.min(), ys.max()
    H = yb - yt
    yy = np.arange(img.shape[0], dtype=np.float64)[:, None] * np.ones((1, img.shape[1]))
    b = 1.0 - _smoothstep(yt + 0.22 * H, yt + 0.5 * H, yy)
    r, g, bl = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    skin = a & (r > 0.78) & (g > 0.58) & (bl > 0.45) & (r > bl + 0.06)
    hair = a & (bl > r + 0.1) & (bl > 0.16) & (rgb.max(-1) < 0.62) & (yy < yt + 0.3 * H) & ~skin     # 深蓝头发（衬衫阴影也偏蓝，但亮）
    hy, hx = np.nonzero(hair)
    h = np.zeros(a.shape, np.float64)
    if len(hx) > 50:
        cx, cy = hx.mean(), hy.mean()
        R = np.percentile(np.hypot(hx - cx, hy - cy), 96)
        xx = np.arange(img.shape[1], dtype=np.float64)[None, :] * np.ones((img.shape[0], 1))
        tip = _smoothstep(0.42 * R, 1.0 * R, np.hypot(xx - cx, yy - cy))
        dens = _box_mean(hair, GRID // 2)
        lock = np.clip(_box_mean(skin, GRID // 2) * 6.0, 0, 1)
        h = np.clip(dens * 1.6, 0, 1) * tip * (1 - lock)
    if sword is not None:
        # 剑 = 拳头到剑尖那条线段附近（不按颜色找：白衬衫也是亮灰）
        (fx, fy), (tx, ty) = sword
        xx = np.arange(img.shape[1], dtype=np.float64)[None, :]
        vx, vy = tx - fx, ty - fy
        L2 = vx * vx + vy * vy
        u = np.clip(((xx - fx) * vx + (yy - fy) * vy) / L2, 0, 1)
        near = np.hypot(xx - fx - u * vx, yy - fy - u * vy) < GRID * 1.2
        fyi = int(np.clip(fy, 0, img.shape[0] - 1))
        b = np.where(near, b[fyi, 0], b)
        h = np.where(near, 0.0, h)
    ex = EXTRA.get(pose, {})
    arm = _region_weight(a.shape, ex["arm"]) if "arm" in ex else np.zeros(a.shape)
    coat = _region_weight(a.shape, ex["coat"]) if "coat" in ex else np.zeros(a.shape)
    h = np.minimum(h, 1.0)
    h = np.where(arm + coat > 0.01, 0.0, h)
    # 手 / 衣摆骨骼挂在呼吸骨骼下面：这些区域的呼吸权重至少要和它们一样大（整体跟着上身起伏，不被拉开）
    b = np.maximum(b, np.clip(arm + coat + h, 0, 1))
    tot = h + arm + coat
    scale = np.where(tot > b, b / np.maximum(tot, 1e-6), 1.0)
    return b, h * scale, arm * scale, coat * scale


def pose_mesh(img, box, pose, bones, sword=None):
    """把一张整身姿势图做成带权重的网格附件（规则网格，只保留有内容的格子）。
    bones = (v7, v7_breath, v7_hair) 的骨骼索引。三根骨骼的初始姿态都在原点、不旋转，所以顶点的骨骼局部坐标 = 世界坐标。"""
    x0, y0, x1, y1 = box
    nx = max(1, int(math.ceil((x1 - x0) / GRID)))
    ny = max(1, int(math.ceil((y1 - y0) / GRID)))
    gx = np.linspace(x0, x1, nx + 1)
    gy = np.linspace(y0, y1, ny + 1)
    b, h, arm, coat = mesh_weights(img, sword, pose)
    alpha = img[..., 3]
    keep = {}
    tris = []

    def vid(i, j):
        if (i, j) not in keep:
            keep[(i, j)] = len(keep)
        return keep[(i, j)]
    for j in range(ny):
        for i in range(nx):
            cx0, cx1 = int(gx[i]) - 2, int(math.ceil(gx[i + 1])) + 2
            cy0, cy1 = int(gy[j]) - 2, int(math.ceil(gy[j + 1])) + 2
            if alpha[max(cy0, 0):cy1, max(cx0, 0):cx1].max() <= 0.004:
                continue
            v00, v10, v01, v11 = vid(i, j), vid(i + 1, j), vid(i, j + 1), vid(i + 1, j + 1)
            tris += [v00, v10, v11, v00, v11, v01]
    verts, uvs = [], []
    iv7, ibr, iha, iarm, icoat = bones
    for (i, j), k in sorted(keep.items(), key=lambda kv: kv[1]):
        px, py = gx[i], gy[j]
        uvs += [round((px - x0) / (x1 - x0), 5), round((py - y0) / (y1 - y0), 5)]
        sx = int(np.clip(px, 0, img.shape[1] - 1))
        sy = int(np.clip(py, 0, img.shape[0] - 1))
        wb, wh, wa, wc = float(b[sy, sx]), float(h[sy, sx]), float(arm[sy, sx]), float(coat[sy, sx])
        if pose in NO_MESH:
            wb = wh = wa = wc = 0.0
        x, y = U((px, py), pose)
        ws = [(iv7, 1.0 - wb), (ibr, max(0.0, wb - wh - wa - wc)), (iha, wh), (iarm, wa), (icoat, wc)]
        ws = [(bi, w) for bi, w in ws if w > 1e-4]
        tot = sum(w for _, w in ws)
        verts.append(len(ws))
        for bi, w in ws:
            verts += [bi, round(x, 2), round(y, 2), round(w / tot, 4)]
    k = pose_scale(pose)
    return {"type": "mesh", "uvs": uvs, "triangles": tris, "vertices": verts, "hull": len(keep),
            "width": round((x1 - x0) * S * k, 2), "height": round((y1 - y0) * S * k, 2)}


# ───────────────────────── 动画 ─────────────────────────
def c8(r, g, b, a):
    return "%02x%02x%02x%02x" % tuple(int(round(max(0, min(1, v)) * 255)) for v in (r, g, b, a))


class Anim:
    def __init__(self, dur):
        self.dur = dur
        self.slots = {}
        self.bones = {}
        self.events = []

    def att(self, slot, t, name):
        self.slots.setdefault(slot, {}).setdefault("attachment", []).append({"time": round(t, 4), "name": name})

    def rgba(self, slot, t, col, stepped=False):
        k = {"time": round(t, 4), "color": col}
        if stepped:
            k["curve"] = "stepped"
        self.slots.setdefault(slot, {}).setdefault("rgba", []).append(k)

    def tr(self, bone, t, x, y, stepped=False):
        k = {"time": round(t, 4), "x": round(x, 2), "y": round(y, 2)}
        if stepped:
            k["curve"] = "stepped"
        self.bones.setdefault(bone, {}).setdefault("translate", []).append(k)

    def rot(self, bone, t, v):
        self.bones.setdefault(bone, {}).setdefault("rotate", []).append({"time": round(t, 4), "value": round(v, 3)})

    def ev(self, t, name):
        self.events.append({"time": round(t, 4), "name": name})

    def json(self):
        out = {}
        for k, v in (("slots", self.slots), ("bones", self.bones)):
            if v:
                for tl in v.values():
                    for arr in tl.values():
                        for e in arr:                           # 超出动画时长的关键帧（末尾残影淡出等）截到结尾，时长与 v5 保持一致
                            e["time"] = min(e["time"], round(self.dur, 4))
                        arr.sort(key=lambda e: e["time"])
                        # 同一时间的重复关键帧只保留最后一个
                        ded = {}
                        for e in arr:
                            ded[e["time"]] = e
                        arr[:] = list(ded.values())
                out[k] = v
        if self.events:
            out["events"] = sorted(self.events, key=lambda e: e["time"])
        return out


class Builder:
    def __init__(self):
        self.data = json.loads((BASE / "regent.spine-json").read_text())
        self.v5_slots = [s["name"] for s in self.data["slots"]]
        self.v5_setup = {s["name"]: s.get("attachment") for s in self.data["slots"]}
        self.v5_dur = {n: self.duration(a) for n, a in self.data["animations"].items()}
        self.v5_events = {n: a.get("events", []) for n, a in self.data["animations"].items()}
        self.src = sources()
        self.geom = {}            # 区域名 -> 原图像素包围盒
        self.grip, self.muzzle = {}, {}
        for p in EVOKER_POSES:
            self.grip[p], self.muzzle[p] = grip_px(p)
        self.hand = {
            "idle": skin_blob(self.src["v7_idle"], (250, 700, 470, 980)),
            "cast_mid": skin_blob(self.src["v7_cast_mid"], (250, 650, 520, 900)),
            "cast": skin_blob(self.src["v7_cast"], (250, 650, 520, 900)),
        }
        for p in EVOKER_POSES:
            self.hand[p] = self.grip[p]
        self.center = {}
        for p in POSE_T:
            img = self.src.get(f"v7_{p}")
            if img is None:
                continue
            a = img[..., 3] > 0.5
            g = (img[..., :3].max(-1) - img[..., :3].min(-1) < 0.12) & (img[..., :3].min(-1) > 0.55)   # 剑身（亮灰）不算进重心
            ys, xs = np.nonzero(a & ~g)
            self.center[p] = U((float(xs.mean()), float(ys.mean())), p)
        self.tip = {}
        for p, (fist, tip) in SWORD.items():
            self.hand[p], self.tip[p] = fist, tip
        self.hand["hurt"] = (553.0, 800.0)
        self.hand["die_kneel"] = (560.0, 1100.0)
        self.hand["die_down"] = (900.0, 800.0)
        self.hand["hurt_impact"] = (553.0, 800.0)
        self.hand["hurt_recover"] = (553.0, 800.0)
        self.hand["focus_low"] = (560.0, 760.0)
        self.hand["die_fall"] = (700.0, 700.0)

    @staticmethod
    def duration(a):
        m = 0.0

        def scan(x):
            nonlocal m
            if isinstance(x, dict):
                if "time" in x:
                    m = max(m, x["time"])
                for v in x.values():
                    scan(v)
            elif isinstance(x, list):
                for v in x:
                    scan(v)
        scan(a)
        return m

    # ── 骨骼 / 槽 / 附件 ──
    def setup(self):
        d = self.data
        d["bones"].append({"name": "v7_settle", "parent": "root"})     # 换图时的"惯性到位"（见 settle）
        d["bones"].append({"name": "v7", "parent": "v7_settle"})
        d["bones"].append({"name": "v7_breath", "parent": "v7"})          # 网格微动：上身呼吸（只平移）
        d["bones"].append({"name": "v7_hair", "parent": "v7_breath"})     # 网格微动：发梢摆动 / 换图后的甩动回弹（只平移）
        d["bones"].append({"name": "v7_arm", "parent": "v7_breath"})      # 网格微动：垂下的手轻轻摆（只平移）
        d["bones"].append({"name": "v7_coat", "parent": "v7_breath"})     # 网格微动：外套下摆摆动 / 甩动回弹（只平移）
        for p in EVOKER_POSES:
            gx, gy = U(self.grip[p], p)
            d["bones"].append({"name": f"v7_ev_{p}", "parent": "v7_breath", "x": round(gx, 2), "y": round(gy, 2)})
        slots = [("v7_ghost", "v7", None), ("v7_body", "v7", None), ("v7_blink", "v7_breath", None), ("v7_old", "v7", None)]
        slots += [(f"v7_ev_{p}", f"v7_ev_{p}", None) for p in EVOKER_POSES]
        slots += [("v7_hand", "v7_breath", None), ("v7_flash", "v7", "additive"), ("v7_glow", "v7_breath", "additive"),
                  ("v7_tip", "v7_ev_awaken_peak", "additive")]
        for name, bone, blend in slots:
            s = {"name": name, "bone": bone}
            if blend:
                s["blend"] = blend
            d["slots"].append(s)
        regions = {}
        for name, img in self.src.items():
            if name == "v7_tipflash":
                tr, box = img, (0, 0, img.shape[1], img.shape[0])
            else:
                tr, box = trim(img)
            self.geom[name] = box
            regions[name] = shrink(tr, TEX)
        att = d["skins"][0]["attachments"]
        poses = ["idle", "focus_mid", "focus", "awaken", "awaken_peak", "cast_mid", "cast"] + SWORD_POSES + \
            ["sword_smear", "sword_raise", "sword_recover", "hurt_impact", "hurt", "hurt_recover", "die_kneel", "die_fall", "die_down"] + \
            (["focus_low"] if HAVE_LOW else [])
        for slot in ("v7_ghost", "v7_old", "v7_flash"):
            att[slot] = {f"v7_{p}": self.region(f"v7_{p}") for p in poses}
        names = [b["name"] for b in d["bones"]]
        bones = (names.index("v7"), names.index("v7_breath"), names.index("v7_hair"), names.index("v7_arm"), names.index("v7_coat"))
        att["v7_body"] = {f"v7_{p}": pose_mesh(self.src[f"v7_{p}"], self.geom[f"v7_{p}"], p, bones, SWORD.get(p))
                          for p in poses}
        att["v7_blink"] = {"v7_idle_blink": self.region("v7_idle_blink")}
        att["v7_hand"] = {f"v7_{p}_hand": self.region(f"v7_{p}_hand") for p in EVOKER_POSES}
        for p in EVOKER_POSES:
            att[f"v7_ev_{p}"] = {f"v7_{p}_evoker": self.region(f"v7_{p}_evoker", origin=self.grip[p])}
        att["v7_glow"] = {"v7_awaken_eyeglow": self.region("v7_awaken_eyeglow"),
                          "v7_awaken_peak_eyeglow": self.region("v7_awaken_peak_eyeglow")}
        mx, my = U(self.muzzle["awaken_peak"], "awaken_peak")
        gx, gy = U(self.grip["awaken_peak"], "awaken_peak")
        k = pose_scale("awaken_peak")
        att["v7_tip"] = {"v7_tipflash": {"x": round(mx - gx, 2), "y": round(my - gy, 2), "width": round(160 * S * k, 2), "height": round(160 * S * k, 2)}}
        return regions

    def region(self, name, origin=None):
        x0, y0, x1, y1 = self.geom[name]
        pose = pose_of(name)
        cx, cy = U(((x0 + x1) / 2, (y0 + y1) / 2), pose)
        if origin is not None:
            ox, oy = U(origin, pose)
            cx, cy = cx - ox, cy - oy
        k = pose_scale(pose)
        return {"x": round(cx, 2), "y": round(cy, 2), "width": round((x1 - x0) * S * k, 2), "height": round((y1 - y0) * S * k, 2)}

    # ── 动画编排 ──
    def settle(self, a, seq, prev, dur):
        """换图的惯性到位：新姿势出现时先停在"上一个姿势重心"和"自己重心"之间（偏回去 40%），
        随后减速滑到原位（约 0.26s，提前遇到下一次换图就截断）。整图平移，不变形；
        和溶解叠在一起，看起来是人物真的从上一个姿势移动过来，而不是一张张切换的幻灯片。"""
        prof = [(0.0, 1.0), (0.04, 0.55), (0.1, 0.22), (0.18, 0.06), (0.26, 0.0)]
        keys = [(0.0, 0.0, 0.0)]
        for i, (t, p) in enumerate(seq):
            before = prev if i == 0 else seq[i - 1][1]
            if before is None or before == p or before not in self.center or p not in self.center:
                continue
            nxt = seq[i + 1][0] if i + 1 < len(seq) else dur
            bx, by = self.center[before]
            cx, cy = self.center[p]
            ox, oy = 0.4 * (bx - cx), 0.4 * (by - cy)
            keys = [k for k in keys if k[0] < t - 1e-4]
            for dt, v in prof:
                if t + dt <= nxt + 1e-4:
                    keys.append((t + dt, ox * v, oy * v))
            if keys[-1][0] < nxt - 1e-4 and keys[-1][1:] != (0.0, 0.0):
                keys.append((min(t + 0.26, nxt), 0.0, 0.0))
        for t, x, y in keys:
            a.tr("v7_settle", t, x, y)

    def sequence(self, a, seq, prev=None):
        """seq: [(t, pose)]。姿势切换 + 过渡特效 + 召唤器 / 拳头层 + 契约骨骼。prev：动画开始前的姿势（t=0 的切换要残影）。"""
        for slot in self.v5_slots:
            if slot != "shadow":
                a.att(slot, 0, None)
        self.settle(a, seq, prev, a.dur)
        self.follow(a, seq, prev)
        for i, (t, p) in enumerate(seq):
            before = prev if i == 0 else seq[i - 1][1]
            a.att("v7_body", t, f"v7_{p}")
            a.att("v7_hand", t, f"v7_{p}_hand" if p in EVOKER_POSES else None)
            for q in EVOKER_POSES:
                a.att(f"v7_ev_{q}", t, f"v7_{q}_evoker" if q == p else None)
            a.att("v7_blink", t, None)
            a.att("v7_glow", t, None)
            a.att("v7_tip", t, None)
            hx, hy = U(self.hand[p], p)
            tx, ty = U(self.muzzle[p], p) if p in EVOKER_POSES else (hx, hy)
            sx, sy = U(self.tip[p], p) if p in self.tip else (hx, hy)
            for b, (x, y) in (("hand", (hx, hy)), ("grip", (hx, hy)), ("muzzle", (tx, ty)), ("evoker_tip", (tx, ty)), ("sword_tip", (sx, sy))):
                a.tr(b, t, x, y, stepped=True)
            if before is not None and before != p:
                f = 1.0 / FPS
                fade = 0.09 if p not in ("sword_smear", "sword_slash", "awaken_peak", "hurt_impact") else 0.04   # 出招 / 扣下 / 受击：溶解更短，保持力度
                nxt = seq[i + 1][0] if i + 1 < len(seq) else a.dur
                fade = max(min(fade, nxt - t - 0.002), f * 0.5)          # 下一次换图之前必须溶解完（"举剑途中"只停一帧）
                # 溶解：新姿势在下面已经完全显示，旧姿势盖在上面从不透明淡到透明 —— 60 帧里每帧都在变，不会一下跳过去
                a.att("v7_old", t, f"v7_{before}")
                a.rgba("v7_old", t, c8(1, 1, 1, 1))
                a.rgba("v7_old", t + fade, c8(1, 1, 1, 0))
                a.att("v7_old", t + fade, None)
                # 不再叠青色残影（用户反馈像重影）；闪光只留给出招 / 扣下 / 受击这几个冲击瞬间
                if p in ("sword_smear", "awaken_peak", "hurt_impact"):
                    a.att("v7_flash", t, f"v7_{p}")
                    a.rgba("v7_flash", t, c8(0.6, 0.9, 1.0, 0.22))
                    a.rgba("v7_flash", t + 3 * f, c8(0.8, 0.95, 1.0, 0.0))
                    a.att("v7_flash", t + 3 * f, None)

    def fire(self, a, T, hold):
        """觉醒（扣下召唤器）那一刻的附加动作；hold：觉醒姿势停留多久（之后蓝光随换图消失）。"""
        k = [(0, 1.0), (0.04, 0.6), (0.08, 0.37), (0.16, 0.12), (0.26, 0.0)]
        for dt, v in k:
            a.tr("v7", T + dt, -8 * S * v, 3 * S * v)
            for b in ("v7_ev_awaken", "v7_ev_awaken_peak"):
                a.rot(b, T + dt, 9.0 * v)
        a.tr("v7", T - 0.001, 0, 0)
        for b in ("v7_ev_awaken", "v7_ev_awaken_peak"):
            a.rot(b, T - 0.001, 0)
        # 冲击帧（PEAK 秒）→ 觉醒：蓝光、尖端闪光跨两张图连续（换图时 sequence 会清掉附件，这里在同一时刻重新挂上）
        a.att("v7_glow", T, "v7_awaken_peak_eyeglow")
        a.att("v7_glow", T + PEAK, "v7_awaken_eyeglow")
        for dt, g in ((0, 0.0), (0.04, 1.0), (0.18, 1.0), (0.45, 0.45), (0.8, 0.12)):
            if dt <= hold:
                a.rgba("v7_glow", T + dt, c8(1, 1, 1, g))
        a.att("v7_tip", T, "v7_tipflash")
        a.att("v7_tip", T + PEAK, "v7_tipflash")
        a.rgba("v7_tip", T, c8(1, 1, 1, 1))
        a.rgba("v7_tip", T + 0.12, c8(1, 1, 1, 0))
        a.att("v7_tip", T + 0.13, None)

    def breathe(self, a, dur, amp=12.6, sway=15.4):
        """循环动画的网格微动（脚锁死，见 mesh_weights）：
        · 呼吸：上身（呼吸骨骼）抬起 amp 再落下，循环里正好整数个周期（约 2.2 秒一次）；
        · 发梢：缓慢左右摆 sway、上下随呼吸轻扬，两个方向周期不同（不机械），首尾为 0、无缝循环；
        · 上身随呼吸轻微左右晃（重心微移，脚不动）。
        幅度按游戏里实际显示定：1 个单位约 0.26 个屏幕像素（v0.9.16 的 7 单位看不出来，v0.9.17 加到 18 / 22 又偏大，v0.9.18 收 30% 到 12.6 / 15.4）。"""
        cyc = max(1, round(dur / 2.2))
        n = max(8, int(round(dur * 10)))
        for i in range(n + 1):
            u = i / n
            t = dur * u
            br = 0.5 * (1 - math.cos(2 * math.pi * cyc * u))
            a.tr("v7_breath", t, 0.22 * amp * math.sin(2 * math.pi * u), amp * br)
            hx = sway * math.sin(2 * math.pi * max(1, cyc - 1) * u) * (0.7 + 0.3 * math.sin(2 * math.pi * u))
            a.tr("v7_hair", t, hx, 0.35 * amp * br + 0.25 * sway * math.sin(2 * math.pi * cyc * u * 2))
            # 垂下的手像钟摆轻轻前后摆（比呼吸慢半拍）；衣摆跟着上身晃、比发梢晚一点
            a.tr("v7_arm", t, 0.7 * sway * math.sin(2 * math.pi * u - 0.6), 0.15 * amp * br)
            a.tr("v7_coat", t, 0.55 * sway * math.sin(2 * math.pi * max(1, cyc - 1) * u - 0.9), 0.2 * amp * br)

    def follow(self, a, seq, prev):
        """换姿势后的发梢跟随：每次换图，发梢先往动作反方向拖（惯性），再越过原位一点、回弹、停住（衰减弹簧，约 0.5 秒）。
        多次换图的弹簧叠加，逐帧采样成关键帧 —— 连续变化，没有跳变。"""
        springs = []
        for i, (t, p) in enumerate(seq):
            before = prev if i == 0 else seq[i - 1][1]
            if before is None or before == p or before not in self.center or p not in self.center or p in NO_MESH:
                continue
            dx = self.center[p][0] - self.center[before][0]
            dy = self.center[p][1] - self.center[before][1]
            m = math.hypot(dx, dy)
            if m < 1.0:
                continue
            A = min(49.0, 14.0 + 0.42 * m)
            springs.append((t, A * dx / m, A * dy / m))
        if not springs:
            return
        step = 1.0 / FPS
        n = int(math.ceil(a.dur / step))
        for k in range(n + 1):
            t = min(k * step, a.dur)
            x = y = 0.0
            for t0, sx, sy in springs:
                tau = t - t0
                if 0.0 <= tau <= 0.7:
                    v = -math.exp(-tau / 0.14) * math.sin(2 * math.pi * tau / 0.32)
                    x += sx * v
                    y += sy * v
            a.tr("v7_hair", t, x, y)
            a.tr("v7_coat", t, 0.8 * x, 0.5 * y)
            a.tr("v7_arm", t, 0.5 * x, 0.3 * y)

    def blink(self, a, t):
        a.att("v7_blink", t, "v7_idle_blink")
        a.att("v7_blink", t + 3.0 / FPS, None)

    def end(self, a, name):
        """保证动画时长与 v5 一致（末尾放一个不改变任何东西的关键帧）。"""
        a.rgba("v7_body", 0, "ffffffff")
        a.rgba("v7_body", self.v5_dur[name], "ffffffff")

    def build_anims(self):
        A = {}
        d = self.v5_dur

        def idle(name, blinks):
            a = Anim(d[name])
            self.sequence(a, [(0, "idle")])
            self.breathe(a, d[name])
            for t in blinks:
                self.blink(a, t)
            return a
        A["idle_loop"] = idle("idle_loop", [1.9])
        A["relaxed_loop"] = idle("relaxed_loop", [1.2, 3.1])
        A["low_health_loop"] = idle("low_health_loop", [1.9])

        a = Anim(d["aim_in"])
        self.sequence(a, [(0, "focus_mid"), (0.2, "focus")], prev="idle")
        a.ev(0.08, "evoker_draw")
        A["aim_in"] = a

        a = Anim(d["aim_loop"])
        self.sequence(a, [(0, "focus")])
        self.breathe(a, d["aim_loop"], amp=9.1, sway=9.8)
        A["aim_loop"] = a

        a = Anim(d["aim_out"])
        self.sequence(a, [(0, "focus"), (0.12, "focus_mid"), (0.3, "idle")])
        A["aim_out"] = a

        def summon(name, seq, T, back_mid, back_idle, prev):
            """召唤：扣下（冲击帧）→ 觉醒 → 收手。收手不再从太阳穴直接跳到胸口再跳到待机，而是
            觉醒 → 凝神（放松、手还在太阳穴旁）→ 胸口 →（有图时）腰部 → 待机，每一步只差一点。"""
            a = Anim(d[name])
            relax = back_mid - 0.08
            back = [(relax, "focus"), (back_mid + 0.03, "focus_mid")]
            if HAVE_LOW:
                back.append((back_mid + 0.03 + (back_idle - back_mid - 0.03) * 0.5, "focus_low"))
            self.sequence(a, seq + [(T, "awaken_peak"), (T + PEAK, "awaken")] + back + [(back_idle, "idle")], prev=prev)
            self.fire(a, T, relax - T)
            return a
        A["cast"] = summon("cast", [(0, "focus_mid"), (0.12, "focus")], 0.27, 0.62, 0.82, "idle")
        A["cast_from_aim"] = summon("cast_from_aim", [(0, "focus")], 0.06, 0.38, 0.56, None)
        A["attack_sovereign"] = summon("attack_sovereign", [(0, "focus_mid"), (0.14, "focus")], 0.43, 0.78, 0.96, "idle")
        A["sovereign_from_aim"] = summon("sovereign_from_aim", [(0, "focus")], 0.40, 0.72, 0.90, None)

        a = Anim(d["cast_light"])
        self.sequence(a, [(0, "cast_mid"), (0.12, "cast"), (0.6, "cast_mid"), (0.74, "idle")], prev="idle")
        A["cast_light"] = a

        # ── 持剑 ──
        a = Anim(d["sword_in"])
        self.sequence(a, [(0, "sword_rest"), (0.07, "sword_recover"), (0.14, "sword_ready")], prev="idle")   # 收招途中那张倒着用：从站直到压低重心
        A["sword_in"] = a

        a = Anim(d["ready_sword_loop"])
        self.sequence(a, [(0, "sword_ready")])
        self.breathe(a, d["ready_sword_loop"], amp=9.8, sway=11.2)
        A["ready_sword_loop"] = a

        a = Anim(d["sword_out"])
        self.sequence(a, [(0, "sword_ready"), (0.08, "sword_recover"), (0.16, "sword_rest"), (0.34, "idle")])
        A["sword_out"] = a

        def strike(name, prev, t_slash, t_rest, t_idle):
            a = Anim(d[name])
            # 拖影帧就是"斩下"那一瞬（weapon_slash 事件那一刻），随后落到斩击收招
            # 起手先过"举剑途中"（约一帧），斩完先"收招途中"再收剑
            self.sequence(a, [(0, "sword_raise"), (t_slash * 0.45, "sword_windup"), (t_slash, "sword_smear"), (t_slash + SMEAR, "sword_slash"),
                              (t_slash + SMEAR + (t_rest - t_slash - SMEAR) * 0.6, "sword_recover"), (t_rest, "sword_rest"), (t_idle, "idle")], prev=prev)
            # 斩下那一刻向前冲一下再收回（整图位移，不变形）
            for dt, v in ((-0.001, 0.0), (0.0, 1.0), (0.05, 0.75), (0.14, 0.3), (0.26, 0.0)):
                a.tr("v7", t_slash + dt, 18 * S * v, 0)
            return a
        A["attack"] = strike("attack", "idle", 0.08, 0.5, 0.72)
        A["attack_from_ready"] = strike("attack_from_ready", "sword_ready", 0.065, 0.48, 0.7)

        # ── 受击 / 死亡 ──
        a = Anim(d["hurt"])
        self.sequence(a, [(0, "hurt_impact"), (IMPACT, "hurt"), (0.27, "hurt_recover"), (0.42, "idle")], prev="idle")
        for dt, v in ((0.0, 1.0), (0.06, 0.6), (0.16, 0.25), (0.3, 0.0)):
            a.tr("v7", dt, -14 * S * v, 0)
        A["hurt"] = a

        a = Anim(d["die"])
        self.sequence(a, [(0, "hurt_impact"), (IMPACT, "hurt"), (0.24, "die_kneel"), (0.6, "die_fall"), (0.85, "die_down")], prev="idle")
        A["die"] = a

        for name, a in A.items():
            for e in self.v5_events.get(name, []):
                a.ev(e["time"], e["name"])
            self.end(a, name)
            self.data["animations"][name] = a.json()

        # 仍是 v5 的动画：开头隐藏 v7 层、恢复 v5 层
        v7_slots = [s["name"] for s in self.data["slots"] if s["name"].startswith("v7_")]
        for name, an in self.data["animations"].items():
            if name in A:
                continue
            sl = an.setdefault("slots", {})
            for s in v7_slots:
                sl.setdefault(s, {})["attachment"] = [{"time": 0, "name": None}]
            for s in self.v5_slots:
                keys = sl.setdefault(s, {}).setdefault("attachment", [])
                if not any(abs(k["time"]) < 1e-6 for k in keys):
                    keys.insert(0, {"time": 0, "name": self.v5_setup[s]})

    def validate(self):
        rep, ok = [], True
        for n in V7_ANIMS:
            dur = self.duration(self.data["animations"][n])
            good = abs(dur - self.v5_dur[n]) < 1e-3
            ev = [(e["name"], e["time"]) for e in self.data["animations"][n].get("events", [])]
            ev5 = [(e["name"], e["time"]) for e in self.v5_events.get(n, [])]
            good_ev = all(x in ev for x in ev5)
            ok &= good and good_ev
            rep.append(f"{'OK ' if good and good_ev else 'BAD'} {n}: 时长 {dur:.3f} / v5 {self.v5_dur[n]:.3f}，事件 {ev}")
        names = {s["name"] for s in self.data["slots"]}
        att = self.data["skins"][0]["attachments"]
        for n, an in self.data["animations"].items():
            for s, tl in an.get("slots", {}).items():
                if s not in names:
                    ok = False
                    rep.append(f"BAD {n}: 未知槽 {s}")
                for k in tl.get("attachment", []):
                    if k["name"] is not None and k["name"] not in att.get(s, {}):
                        ok = False
                        rep.append(f"BAD {n}: 槽 {s} 没有附件 {k['name']}")
        return ok, rep


def stage(out, pages_n):
    import hashlib
    from godot_tex import write_texture
    d = STAGE / REL
    imp = STAGE / ".godot/imported"

    def h(p):
        return hashlib.md5(p.encode()).hexdigest()
    atlas_res = f"res://{REL}/regent.atlas"
    (d / "regent.atlas").write_text((out / "regent.atlas").read_text())
    (imp / f"regent.atlas-{h(atlas_res)}.spatlas").write_text(json.dumps({
        "atlas_data": (out / "regent.atlas").read_text(), "normal_texture_prefix": "n",
        "source_path": atlas_res, "specular_texture_prefix": "s"}, separators=(",", ":")))
    skel_res = f"res://{REL}/regent.spine-json"
    raw = (out / "regent.spine-json").read_bytes()
    (d / "regent.spine-json").write_bytes(raw)
    (imp / f"regent.spine-json-{h(skel_res)}.spjson").write_bytes(raw)
    for i in range(1, pages_n + 1):
        write_texture(STAGE, f"{REL}/regent_v7_{i}.png", Image.open(out / f"regent_v7_{i}.png"))


def main():
    b = Builder()
    regions = b.setup()
    b.build_anims()
    ok, rep = b.validate()
    print("\n".join(rep))
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / "VALIDATION.txt").write_text("\n".join(rep) + "\n")
    if not ok:
        print("校验未通过，不导出。")
        sys.exit(1)
    pages = pack(regions)
    atlas = (BASE / "regent.atlas").read_text().rstrip("\n") + "\n"
    for i, (img, items) in enumerate(pages, 1):
        img.save(OUT / f"regent_v7_{i}.png")
        atlas += f"\nregent_v7_{i}.png\nsize:{img.width},{img.height}\nfilter:Linear,Linear\npma:false\n"
        for name, x, y, w, h in items:
            atlas += f"{name}\nbounds:{x},{y},{w},{h}\n"
    (OUT / "regent.atlas").write_text(atlas)
    (OUT / "regent.spine-json").write_text(json.dumps(b.data, separators=(",", ":")))
    (OUT / "regent.png").write_bytes((BASE / "regent.png").read_bytes())
    print("图集页：", [(p.size, len(it)) for p, it in pages])
    if "--no-stage" not in sys.argv:
        stage(OUT, len(pages))
        print("已写入 PCK 打包目录")


if __name__ == "__main__":
    main()
