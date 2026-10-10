"""结城理骨骼构建：切零件 → 生成 Spine 4.2 JSON + atlas → 自动校验 → 导出预览。
用法：python3 tools/rig/build_rig.py
规范见 docs/RIG.md。所有坐标先在母版图像素坐标系（1024×1536，y 向下）里定义，再统一换算。"""
import json
import math
import os
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, str(Path(__file__).parent))
from spine_eval import Skeleton  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
MASTER = ROOT / "art/master/makoto_rig_master.png"   # 已抠图的 armout 母版
OUT = ROOT / "art/rig"
PARTS = OUT / "parts"

K = 1.0                        # 像素 → Spine 单位（v1 的 0.77 在游戏里偏小，放大约 30%）
ORIGIN = (345.0, 1480.0)       # 母版上的地面原点（两脚之间）


def to_spine(p):
    return ((p[0] - ORIGIN[0]) * K, (ORIGIN[1] - p[1]) * K)


# ───────────── 关节（母版像素坐标）─────────────
J = {
    "hip": (395, 600),
    "neck": (395, 212),
    "shoulder": (485, 295),
    "elbow": (605, 300),
    "wrist": (706, 298),
    "hip_l": (335, 625), "knee_l": (300, 1000), "ankle_l": (210, 1395),
    "hip_r": (455, 625), "knee_r": (440, 1000), "ankle_r": (450, 1345),
    "chest": (395, 400),
}


def leg_gap(y):
    """两腿分界线 x(y)，在母版上量得。"""
    pts = [(600, 395), (700, 400), (800, 390), (900, 380), (1000, 375), (1100, 370), (1300, 370), (1400, 385), (1536, 330)]
    for (y0, x0), (y1, x1) in zip(pts, pts[1:]):
        if y0 <= y <= y1:
            return x0 + (x1 - x0) * (y - y0) / (y1 - y0)
    return pts[-1][1]


def neck_cut(x):
    """头部下边界 y(x)：后脑头发 → 下巴。"""
    if x < 340:
        return 190
    if x < 380:
        return 190 + (x - 340) * 0.5
    return 212


def build_masks(h, w):
    ys, xs = np.mgrid[0:h, 0:w]
    gap = np.vectorize(leg_gap)(np.arange(h))[:, None]
    neck = np.vectorize(neck_cut)(np.arange(w))[None, :]
    m = {}
    # 头：下边界以上；脖子皮肤往下多留 22px（被躯干盖住，转头时不露缝）
    m["head"] = (ys < neck) | ((ys < neck + 22) & (xs > 350) & (xs < 425))
    # 手臂：肩部以右、y<370；往左多留 18px 盖住肩头
    arm_zone = (xs >= 467) & (ys < 372) & (ys > 225)
    # 肩部圆形关节帽：以肩关节为圆心，手臂零件带着它旋转，盖住躯干上的切口
    sx, sy = J["shoulder"]
    cap = (xs - (sx + 6)) ** 2 + (ys - sy) ** 2 < 44 ** 2
    # 整条手臂（肩帽 → 手腕）是一张加权网格，肘部和肩部平滑弯曲
    m["arm"] = ((arm_zone & (xs >= sx)) | cap) & (xs < 718)
    m["hand_open"] = arm_zone & (xs >= 700)
    # 腿：y≥600，按分界线左右分；大腿/小腿在膝盖处各多留 20px
    legs = ys >= 600
    flap = ((xs < 280) & (ys < 700)) | ((xs > 412) & (xs < 450) & (ys < 680))  # 西装下摆和插兜的手
    left = legs & (xs < gap) & ~flap
    right = legs & (xs >= gap) & ~flap
    m["thigh_l"] = left & (ys < 1020)
    m["shin_l"] = left & (ys >= 980)
    m["thigh_r"] = right & (ys < 1020)
    m["shin_r"] = right & (ys >= 980)
    # 躯干：除手臂外、头部下边界以下、y<640（盖住大腿根）+ 下摆
    m["torso"] = (ys >= neck - 6) & ~(arm_zone & (xs >= J["shoulder"][0] + 8)) & ((ys < 640) | flap)
    return m


def cut_parts(master):
    a = np.asarray(master)
    h, w = a.shape[:2]
    masks = build_masks(h, w)
    parts = {}
    for name, mk in masks.items():
        alpha = a[..., 3].astype(np.float32) * mk
        # 只对"切口"做 1.5px 羽化，原有轮廓保持不变
        soft = np.asarray(Image.fromarray((mk * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2))).astype(np.float32) / 255
        alpha = a[..., 3].astype(np.float32) * np.minimum(soft * 1.4, 1.0) * (soft > 0.02)
        rgba = a.copy()
        rgba[..., 3] = alpha.clip(0, 255).astype(np.uint8)
        im = Image.fromarray(rgba, "RGBA")
        bbox = im.getchannel("A").point(lambda v: 255 if v > 3 else 0).getbbox()
        crop = im.crop(bbox)
        cx, cy = (bbox[0] + bbox[2]) / 2, (bbox[1] + bbox[3]) / 2
        parts[name] = (crop, (cx, cy))
    return parts


def feather_left(im, px=12):
    """左边缘 alpha 渐变：手和袖口的接缝融合，不出现硬切口。"""
    a = np.asarray(im).copy()
    ramp = np.clip(np.arange(a.shape[1]) / px, 0, 1)[None, :]
    a[..., 3] = (a[..., 3].astype(np.float32) * ramp).astype(np.uint8)
    return Image.fromarray(a, "RGBA")


def arm_mesh(img, center, bone_index, joint):
    """手臂网格：3 行 × N 列网格，按 x 位置给肩（躯干↔上臂）和肘（上臂↔前臂）做线性权重过渡。
    返回 Spine 4.2 加权 mesh 附件（不含 path）。坐标在母版像素中计算后换算到 Spine。"""
    w, h = img.size
    left, top = center[0] - w / 2, center[1] - h / 2
    xs = list(np.arange(0, w, 14.0)) + [float(w)]
    rows = [0.0, h / 2, float(h)]
    n = len(xs)
    order = [(x, rows[0]) for x in xs] + [(x, rows[2]) for x in reversed(xs)] + [(x, rows[1]) for x in xs]
    top_i = lambda i: i
    bot_i = lambda i: 2 * n - 1 - i
    mid_i = lambda i: 2 * n + i
    tris = []
    for i in range(n - 1):
        for a, b in ((top_i, mid_i), (mid_i, bot_i)):
            tris += [a(i), a(i + 1), b(i), a(i + 1), b(i + 1), b(i)]
    sx, ex = J["shoulder"][0], J["elbow"][0]

    wr = J["wrist"][0]

    def weights(xpx):
        ws = []
        u = np.clip((xpx - (sx - 38)) / 60.0, 0, 1)      # 肩：躯干 → 上臂
        e = np.clip((xpx - (ex - 30)) / 60.0, 0, 1)      # 肘：上臂 → 前臂
        k = np.clip((xpx - (wr - 26)) / 26.0, 0, 1)      # 腕：前臂 → 手
        if k > 0:
            ws = [("forearm", 1 - k), ("hand", k)]
        elif e > 0:
            ws = [("upper_arm", 1 - e), ("forearm", e)]
        else:
            ws = [("torso", 1 - u), ("upper_arm", u)]
        return [(b, float(v)) for b, v in ws if v > 1e-4]

    uvs, verts = [], []
    for (x, y) in order:
        px, py = left + x, top + y
        uvs += [round(x / w, 5), round(y / h, 5)]
        ws = weights(px)
        verts.append(len(ws))
        wx, wy = to_spine((px, py))
        for b, v in ws:
            bx, by = to_spine(joint[b])
            verts += [bone_index[b], round(wx - bx, 3), round(wy - by, 3), round(v, 4)]
    return {"type": "mesh", "uvs": uvs, "triangles": tris, "vertices": verts, "hull": 2 * n,
            "width": round(w * K), "height": round(h * K)}


def outline(im, px=3, color=(8, 12, 30)):
    """道具描边：在 alpha 外扩一圈深色，暗背景上也能看清轮廓。"""
    a = im.getchannel("A")
    grown = a.filter(ImageFilter.MaxFilter(px * 2 + 1))
    base = Image.new("RGBA", im.size, color + (0,))
    base.putalpha(grown)
    base.alpha_composite(im)
    return base


def prop_part(src, cuff_px, cuff_h_px, target_cuff, target_h, keep_left, extra=1.0):
    """把单独生成的"前臂+手+道具"图按袖口对齐到母版：
    cuff_px = 道具图中袖口中心，cuff_h_px = 道具图中袖口高度；target_* 为母版上的对应值。
    keep_left = 袖子保留到袖口左侧多少像素（母版坐标）。"""
    s = target_h / cuff_h_px * extra
    im = src.resize((round(src.width * s), round(src.height * s)), Image.LANCZOS)
    cx, cy = cuff_px[0] * s, cuff_px[1] * s
    left = max(0, int(cx - keep_left))
    im = im.crop((left, 0, im.width, im.height))
    cx -= left
    bbox = im.getchannel("A").point(lambda v: 255 if v > 3 else 0).getbbox()
    im = im.crop(bbox)
    cx -= bbox[0]
    cy -= bbox[1]
    # 返回零件图和"零件中心在母版坐标中的位置"
    center = (target_cuff[0] - cx + im.width / 2, target_cuff[1] - cy + im.height / 2)
    return im, center


# ───────────── 道具零件测量值（tools 里用 numpy 量得）─────────────
MASTER_CUFF = (698.0, 298.0)       # 母版：西装袖口末端中心
MASTER_SLEEVE_H = 64.0
PROPS = {
    # 名称: (文件, 袖口中心, 袖口高度, 道具尖端像素)
    "hand_evoker": ("art/parts_src/arm_evoker.png", (812.0, 540.0), 300.0, (1700.0, 282.0)),
    "hand_sword": ("art/parts_src/arm_sword.png", (412.0, 430.0), 152.0, (1755.0, 440.0)),
}
# 姿势专用手绘手臂（sprite swap）：源图中的肩点 → 母版肩点，按 scale 缩放。
# 举枪手臂额外用枪口对齐太阳穴来确定缩放（两点定位），见 docs/ART_SPEC.md 阶段 2.5。
POSE_ARMS = {
    # 名称: (文件, 源图肩点, 目标肩点, 缩放, 骨骼)
    "arm_raised_evoker": ("art/parts_src/arm_raised_evoker.png", (470.0, 430.0), (485.0, 300.0), 0.607, "arm_raised"),
    "arm_hanging": ("art/parts_src/arm_hanging.png", (410.0, 380.0), (488.0, 300.0), 0.80, "arm_hang"),
}
RAISED_MUZZLE_SRC = (400.0, 120.0)
RAISED_GRIP_SRC = (560.0, 190.0)


def pose_arm_point(name, p):
    _, s0, t0, sc, _ = POSE_ARMS[name]
    return (t0[0] + (p[0] - s0[0]) * sc, t0[1] + (p[1] - s0[1]) * sc)


# 道具夸张系数：召唤器放大让它在战斗画面里一眼能认出
PROP_EXTRA = {"hand_evoker": 1.15, "hand_sword": 1.0}


def key_image(path):
    """绿幕抠图（与 tools/chroma_key.py 相同算法）。"""
    im = np.asarray(Image.open(path).convert("RGB")).astype(np.float32)
    r, g, b = im[..., 0], im[..., 1], im[..., 2]
    excess = g - np.maximum(r, b)
    alpha = 1.0 - np.clip((excess - 40.0) / 90.0, 0.0, 1.0)
    g2 = np.minimum(g, np.maximum(r, b) + 10.0)
    return Image.fromarray(np.dstack([r, g2, b, alpha * 255]).clip(0, 255).astype(np.uint8), "RGBA")


def shadow_image():
    w, h = 420, 90
    ys, xs = np.mgrid[0:h, 0:w]
    d = ((xs - w / 2) / (w / 2)) ** 2 + ((ys - h / 2) / (h / 2)) ** 2
    a = (np.clip(1 - d, 0, 1) ** 1.5 * 110).astype(np.uint8)
    rgba = np.zeros((h, w, 4), np.uint8)
    rgba[..., 3] = a
    return Image.fromarray(rgba, "RGBA")


# ───────────── 骨骼定义 ─────────────
BONES = [
    # name, parent, 母版关节
    ("root", None, ORIGIN),
    ("hip", "root", J["hip"]),
    ("torso", "hip", J["hip"]),
    ("chest_particle_attach", "torso", J["chest"]),
    ("head", "torso", J["neck"]),
    ("upper_arm", "torso", J["shoulder"]),
    ("forearm", "upper_arm", J["elbow"]),
    ("arm_particle_attach", "forearm", (650, 300)),
    ("hand", "forearm", J["wrist"]),
    ("evoker_tip", "hand", J["wrist"]),   # 位置在 build_setup() 里按道具尖端修正
    ("sword_tip", "hand", J["wrist"]),
    ("arm_raised", "torso", J["shoulder"]),
    ("arm_hang", "torso", J["shoulder"]),
    ("muzzle", "arm_raised", J["shoulder"]),    # 位置在 build_setup() 里按手绘枪口修正
    ("grip", "arm_raised", J["shoulder"]),
    ("thigh_l", "hip", J["hip_l"]),
    ("shin_l", "thigh_l", J["knee_l"]),
    ("leg_particle_attach_l", "shin_l", (240, 1250)),
    ("thigh_r", "hip", J["hip_r"]),
    ("shin_r", "thigh_r", J["knee_r"]),
    ("leg_particle_attach", "shin_r", (445, 1250)),
]

# 绘制顺序（先画在下面）：远侧腿 → 近侧腿 → 头 → 躯干（衣领盖住脖子）→ 手臂
SLOTS = [
    ("shadow", "root", "shadow"),
    ("shin_r", "shin_r", "shin_r"),
    ("thigh_r", "thigh_r", "thigh_r"),
    ("shin_l", "shin_l", "shin_l"),
    ("thigh_l", "thigh_l", "thigh_l"),
    ("head", "head", "head"),
    ("torso", "torso", "torso"),
    ("arm_hang", "arm_hang", None),
    ("arm", "upper_arm", "arm"),
    ("arm_raised", "arm_raised", None),
    ("hand", "hand", "hand_open"),
]


# ───────────── 构建 setup pose ─────────────
def build_setup():
    master = Image.open(MASTER).convert("RGBA")
    parts = cut_parts(master)
    images, centers = {}, {}
    for name, (im, c) in parts.items():
        images[name] = im
        centers[name] = c
    tips = {}
    for name, (path, cuff, cuff_h, tip) in PROPS.items():
        src = key_image(ROOT / path)
        ex = PROP_EXTRA[name]
        im, center = prop_part(src, cuff, cuff_h, MASTER_CUFF, MASTER_SLEEVE_H, keep_left=8, extra=ex)
        images[name] = outline(feather_left(im, 10), 3)
        centers[name] = center
        s = MASTER_SLEEVE_H / cuff_h * ex
        tips[name] = (MASTER_CUFF[0] + (tip[0] - cuff[0]) * s, MASTER_CUFF[1] + (tip[1] - cuff[1]) * s)
    for name, (path, s0, t0, sc, bone) in POSE_ARMS.items():
        src = key_image(ROOT / path)
        bbox = src.getchannel("A").point(lambda v: 255 if v > 3 else 0).getbbox()
        crop = src.crop(bbox)
        crop = crop.resize((round(crop.width * sc), round(crop.height * sc)), Image.LANCZOS)
        c_src = ((bbox[0] + bbox[2]) / 2, (bbox[1] + bbox[3]) / 2)
        images[name] = crop
        centers[name] = pose_arm_point(name, c_src)
    images["hand_open"] = feather_left(images["hand_open"], 10)
    images["shadow"] = shadow_image()
    centers["shadow"] = (ORIGIN[0] + 10, ORIGIN[1] - 10)

    # 缩放到 Spine 单位
    scaled = {}
    for n, im in images.items():
        if n == "shadow":
            scaled[n] = im
        else:
            scaled[n] = im.resize((max(1, round(im.width * K)), max(1, round(im.height * K))), Image.LANCZOS)

    joint = {b[0]: b[2] for b in BONES}
    joint["muzzle"] = pose_arm_point("arm_raised_evoker", RAISED_MUZZLE_SRC)
    joint["grip"] = pose_arm_point("arm_raised_evoker", RAISED_GRIP_SRC)
    joint["evoker_tip"] = tips["hand_evoker"]
    joint["sword_tip"] = tips["hand_sword"]
    bones = []
    for name, parent, _ in BONES:
        wx, wy = to_spine(joint[name])
        b = {"name": name}
        if parent:
            px, py = to_spine(joint[parent])
            b["parent"] = parent
            b["x"], b["y"] = round(wx - px, 3), round(wy - py, 3)
        bones.append(b)

    bone_of = {s[0]: s[1] for s in SLOTS}
    bone_of.update({"hand_open": "hand", "hand_evoker": "hand", "hand_sword": "hand",
                    "arm_raised_evoker": "arm_raised", "arm_hanging": "arm_hang"})
    skin = {}
    for slot, bone, att in SLOTS:
        skin.setdefault(slot, {})
    bone_index = {b["name"]: i for i, b in enumerate(bones)}
    for att, im in scaled.items():
        slot = "hand" if att.startswith("hand") else {"arm_raised_evoker": "arm_raised", "arm_hanging": "arm_hang"}.get(att, att)
        if att == "arm":
            skin[slot][att] = arm_mesh(images["arm"], centers["arm"], bone_index, joint)
            continue
        bone = bone_of[att] if att in bone_of else slot
        bx, by = to_spine(joint[bone])
        cx, cy = to_spine(centers[att])
        skin[slot][att] = {"x": round(cx - bx, 3), "y": round(cy - by, 3), "width": im.width, "height": im.height}

    data = {
        "skeleton": {"spine": "4.2.43", "images": "./", "audio": "", "x": -300, "y": -20, "width": 820, "height": 1200},
        "bones": bones,
        "slots": [dict({"name": s, "bone": b}, **({"attachment": a} if a else {})) for s, b, a in SLOTS],
        "skins": [{"name": "default", "attachments": skin}],
        "events": {e: {} for e in ["death_particles_start", "death_particles_end", "explode_dead", "explode_end"]},
        "animations": {},
    }
    return data, scaled, master


# ───────────── 动画工具 ─────────────
FPS = 30


def ease_out_cubic(u):
    return 1 - (1 - u) ** 3


def ease_in_out(u):
    return 3 * u * u - 2 * u * u * u


def ease_out_expo(u):
    return 1.0 if u >= 1 else 1 - 2 ** (-10 * u)


def lerp(a, b, u):
    return a + (b - a) * u


class Pose(dict):
    """bone -> {'r': 旋转增量, 'x','y': 平移增量}；未列出的骨骼为 0。"""

    def get_r(self, b):
        return self.get(b, {}).get("r", 0.0)


def mix(p0, p1, u):
    out = Pose()
    for b in set(p0) | set(p1):
        d0, d1 = p0.get(b, {}), p1.get(b, {})
        out[b] = {k: lerp(d0.get(k, 0.0), d1.get(k, 0.0), u) for k in set(d0) | set(d1)}
    return out


def add(p0, p1):
    out = Pose({b: dict(v) for b, v in p0.items()})
    for b, v in p1.items():
        for k, x in v.items():
            out.setdefault(b, {})[k] = out.get(b, {}).get(k, 0.0) + x
    return out


class Rig:
    def __init__(self, data):
        self.data = data
        self.bone = {b["name"]: b for b in data["bones"]}

    def setup_world(self, name):
        x = y = 0.0
        b = self.bone[name]
        while True:
            x += b.get("x", 0.0)
            y += b.get("y", 0.0)
            if "parent" not in b:
                return x, y
            b = self.bone[b["parent"]]

    def leg_ik(self, pose, side):
        """在 hip 平移 / 旋转后反算大腿和小腿旋转，使脚踝保持在 setup 位置。"""
        hx, hy = self.setup_world("hip")
        dx, dy = pose.get("hip", {}).get("x", 0.0), pose.get("hip", {}).get("y", 0.0)
        hip_rot = math.radians(pose.get_r("hip"))
        tx, ty = self.setup_world(f"thigh_{side}")
        kx, ky = self.setup_world(f"shin_{side}")
        ax, ay = to_spine(J[f"ankle_{side}"])
        # 大腿根的新世界位置（hip 绕自身旋转 + 平移）
        ox, oy = tx - hx, ty - hy
        rx = ox * math.cos(hip_rot) - oy * math.sin(hip_rot)
        ry = ox * math.sin(hip_rot) + oy * math.cos(hip_rot)
        root = (hx + dx + rx, hy + dy + ry)
        l1 = math.dist((tx, ty), (kx, ky))
        l2 = math.dist((kx, ky), (ax, ay))
        d = min(math.dist(root, (ax, ay)), l1 + l2 - 1e-3)
        base = math.atan2(ay - root[1], ax - root[0])
        # 膝盖弯曲方向与 setup 中一致（两条腿可能不同）
        # 两条腿都向前（画面右侧，角色朝向）屈膝；腿朝下时 +a 使膝盖偏向 +x
        side_sign = 1.0
        a = math.acos(max(-1, min(1, (l1 * l1 + d * d - l2 * l2) / (2 * l1 * d))))
        thigh_w = base + side_sign * a
        knee = (root[0] + l1 * math.cos(thigh_w), root[1] + l1 * math.sin(thigh_w))
        shin_w = math.atan2(ay - knee[1], ax - knee[0])
        thigh0 = math.atan2(ky - ty, kx - tx)
        shin0 = math.atan2(ay - ky, ax - kx)
        pose[f"thigh_{side}"] = {"r": math.degrees(thigh_w - thigh0) - pose.get_r("hip")}
        pose[f"shin_{side}"] = {"r": math.degrees((shin_w - thigh_w) - (shin0 - thigh0))}
        return pose

    def arm_ik(self, tip_bone, target_px, aim_deg, elbow_up=True, stretch=None):
        """手臂三段 IK：让 tip_bone（枪口 / 剑尖）到达 target_px（母版像素），道具朝向世界角 aim_deg。
        母版手臂是透视缩短的，抬起时需要拉伸回真实长度：stretch=None 时自动取够得着的最小拉伸（1.0~1.7）。
        返回 Pose（upper_arm / forearm / hand 的旋转，以及 forearm / hand 沿肢体方向的外移）。"""
        sx, sy = self.setup_world("upper_arm")
        ex, ey = self.setup_world("forearm")
        wx, wy = self.setup_world("hand")
        tx0, ty0 = self.setup_world(tip_bone)
        tip_len = math.dist((wx, wy), (tx0, ty0))
        tip_ang0 = math.atan2(ty0 - wy, tx0 - wx)
        T = to_spine(target_px)
        aim = math.radians(aim_deg)
        W = (T[0] - tip_len * math.cos(aim), T[1] - tip_len * math.sin(aim))
        e_vec0 = (ex - sx, ey - sy)
        w_vec0 = (wx - ex, wy - ey)
        need = math.dist((sx, sy), W)
        steps = [stretch] if stretch else [1.0 + 0.02 * i for i in range(36)]
        for st in steps:
            d1, d2 = math.hypot(*e_vec0) * (st - 1), math.hypot(*w_vec0) * (st - 1)
            e_vec = (e_vec0[0] + d1, e_vec0[1])
            w_vec = (w_vec0[0] + d2, w_vec0[1])
            l1, l2 = math.hypot(*e_vec), math.hypot(*w_vec)
            if need <= (l1 + l2) * 0.97 or stretch:
                break
        d = min(need, l1 + l2 - 1e-3)
        base = math.atan2(W[1] - sy, W[0] - sx)
        a = math.acos(max(-1, min(1, (l1 * l1 + d * d - l2 * l2) / (2 * l1 * d))))
        cands = []
        for sgn in (1, -1):
            u = base + sgn * a
            elbow = (sx + l1 * math.cos(u), sy + l1 * math.sin(u))
            f = math.atan2(W[1] - elbow[1], W[0] - elbow[0])
            cands.append((elbow[1], u, f))
        _, u, f = max(cands) if elbow_up else min(cands)
        upper0 = math.atan2(e_vec[1], e_vec[0])
        fore0 = math.atan2(w_vec[1], w_vec[0])
        r_upper = math.degrees(u - upper0)
        r_fore = math.degrees((f - u) - (fore0 - upper0))
        r_hand = math.degrees((aim - tip_ang0) - (f - fore0))
        return Pose({"upper_arm": {"r": r_upper}, "forearm": {"r": r_fore, "x": d1}, "hand": {"r": r_hand, "x": d2}}), st


def bake(rig, name, duration, fn, loop=False, slots=None, events=None):
    """fn(t) -> Pose。按 30fps 采样成线性关键帧。loop 时末帧强制等于首帧。"""
    n = max(1, round(duration * FPS))
    frames = []
    for i in range(n + 1):
        t = duration * i / n
        p = fn(0.0 if (loop and i == n) else t)
        p = rig.leg_ik(p, "l")
        p = rig.leg_ik(p, "r")
        frames.append((round(t, 4), p))
    bones = {}
    names = set()
    for _, p in frames:
        names |= set(p)
    for b in sorted(names):
        rot, tr = [], []
        for t, p in frames:
            v = p.get(b, {})
            rot.append({"time": t, "value": round(v.get("r", 0.0), 3)})
            tr.append({"time": t, "x": round(v.get("x", 0.0), 3), "y": round(v.get("y", 0.0), 3)})
        entry = {}
        if any(abs(k["value"]) > 1e-4 for k in rot):
            entry["rotate"] = rot
        if any(abs(k["x"]) > 1e-4 or abs(k["y"]) > 1e-4 for k in tr):
            entry["translate"] = tr
        if entry:
            bones[b] = entry
    anim = {"bones": bones}
    if slots:
        anim["slots"] = slots
    if events:
        anim["events"] = [{"time": t, "name": e} for t, e in events]
    rig.data["animations"][name] = anim


# ───────────── 二阶弹簧动力学（"不塑料"的关键）─────────────
# 每个关节通道像挂在弹簧上一样追赶目标姿势：越靠末端频率越低、阻尼越小 → 自然的滞后、跟随与过冲回弹。
# (f 频率Hz, z 阻尼, r 起始响应)。参考 t3ssel8r 的 second-order dynamics。
DYN = {
    "hip": (3.4, 0.85, 0.0),
    "torso": (3.0, 0.68, 0.0),
    "head": (2.3, 0.5, 0.0),
    "upper_arm": (4.2, 0.6, 0.0),
    "forearm": (4.4, 0.5, 0.0),
    "hand": (5.2, 0.42, 0.0),
    "arm_raised": (4.5, 0.55, 0.0),
    "arm_hang": (3.0, 0.45, 0.0),
}


class Spring:
    def __init__(self, x0, f, z, r):
        self.k1 = z / (math.pi * f)
        self.k2 = 1 / ((2 * math.pi * f) ** 2)
        self.k3 = r * z / (2 * math.pi * f)
        self.xp = x0
        self.y = x0
        self.yd = 0.0

    def step(self, T, x):
        xd = (x - self.xp) / T
        self.xp = x
        k2 = max(self.k2, T * T / 2 + T * self.k1 / 2, T * self.k1)  # 稳定性保证
        self.y += T * self.yd
        self.yd += T * (x + self.k3 * xd - self.y - self.k1 * self.yd) / k2
        return self.y


def bake_dynamic(rig, name, duration, target, loop=False, slots=None, events=None, speed=1.0, dyn_off=()):
    """target(t) -> Pose（目标姿势）。弹簧在 240Hz 上积分，按 30fps 输出；腿部 IK 最后统一解。
    loop：先空跑两个周期进入稳态，取第三个周期，并强制首尾一致。dyn_off：不走弹簧的骨骼。"""
    sub = 240
    T = 1.0 / sub
    chans = {}
    p0 = target(0.0)
    names = set(DYN) | set(p0)
    for b in names:
        f, z, r = DYN.get(b, (4.0, 0.7, 0.0))
        for c in ("r", "x", "y"):
            chans[(b, c)] = Spring(p0.get(b, {}).get(c, 0.0), f * speed, z, r)
    warm = 2 if loop else 0
    total_steps = int(round(duration * sub)) * (warm + 1)
    out_every = sub // FPS
    frames = []
    for i in range(total_steps + 1):
        t_abs = i * T
        t = t_abs % duration if loop else min(t_abs, duration)
        tp = target(t)
        cur = Pose()
        for (b, c), sp in chans.items():
            x = tp.get(b, {}).get(c, 0.0)
            v = x if b in dyn_off else sp.step(T, x)
            if abs(v) > 1e-6:
                cur.setdefault(b, {})[c] = v
        cycle_start = int(round(duration * sub)) * warm
        if i >= cycle_start and (i - cycle_start) % out_every == 0:
            frames.append((round((i - cycle_start) * T, 4), cur))
    if loop:
        frames[-1] = (frames[-1][0], frames[0][1])
    _write_frames(rig, name, frames, slots, events)


def _write_frames(rig, name, frames, slots, events):
    solved = []
    for t, p in frames:
        p = Pose({b: dict(v) for b, v in p.items()})
        p = rig.leg_ik(p, "l")
        p = rig.leg_ik(p, "r")
        solved.append((t, p))
    bones = {}
    names = set()
    for _, p in solved:
        names |= set(p)
    for b in sorted(names):
        rot = [{"time": t, "value": round(p.get(b, {}).get("r", 0.0), 3)} for t, p in solved]
        tr = [{"time": t, "x": round(p.get(b, {}).get("x", 0.0), 3), "y": round(p.get(b, {}).get("y", 0.0), 3)} for t, p in solved]
        entry = {}
        if any(abs(k["value"]) > 1e-4 for k in rot):
            entry["rotate"] = rot
        if b in ("root", "hip", "torso", "forearm", "hand") and any(abs(k["x"]) > 1e-4 or abs(k["y"]) > 1e-4 for k in tr):
            entry["translate"] = tr
        if entry:
            bones[b] = entry
    anim = {"bones": bones}
    if slots:
        anim["slots"] = slots
    if events:
        anim["events"] = [{"time": t, "name": e} for t, e in sorted(events)]
    rig.data["animations"][name] = anim


# ───────────── 动画定义 ─────────────
EVOKER_TARGET = ((440, 112), 180)      # 网格手臂的过渡姿势：枪口指向手绘举枪手臂的枪口位置
EVENTS = ["death_particles_start", "death_particles_end", "explode_dead", "explode_end",
          "weapon_draw", "weapon_windup", "weapon_slash", "weapon_sheath", "evoker_draw", "evoker_fire", "evoker_sheath"]


ARM_STATES = {
    # 手臂显示状态：网格手臂（可弯曲）/ 手绘下垂手臂 / 手绘举枪手臂
    "hang": {"arm_hang": "arm_hanging", "arm": None, "arm_raised": None, "hand": None},
    "mesh": {"arm_hang": None, "arm": "arm", "arm_raised": None, "hand": "hand_open"},
    "mesh_evoker": {"arm_hang": None, "arm": "arm", "arm_raised": None, "hand": "hand_evoker"},
    "mesh_sword": {"arm_hang": None, "arm": "arm", "arm_raised": None, "hand": "hand_sword"},
    "raised": {"arm_hang": None, "arm": None, "arm_raised": "arm_raised_evoker", "hand": None},
}


def arm_slots(states, fade=0.04):
    """states: [(时间, 状态名)]。状态切换时用插槽透明度交叉淡化（fade 秒），同一插槽换图则直接切换。"""
    out = {}
    for slot in ARM_STATES["hang"]:
        att, rgba = [], []
        for i, (t, st) in enumerate(states):
            cur = ARM_STATES[st][slot]
            prev = ARM_STATES[states[i - 1][1]][slot] if i else None
            if i == 0:
                att.append({"time": 0.0, "name": cur})
                rgba.append({"time": 0.0, "color": "ffffffff" if cur else "ffffff00"})
            elif cur == prev:
                continue
            elif cur and prev:
                att.append({"time": t, "name": cur})
            elif cur:
                att.append({"time": t, "name": cur})
                rgba += [{"time": t, "color": "ffffff00"}, {"time": round(t + fade, 4), "color": "ffffffff"}]
            else:
                rgba += [{"time": t, "color": "ffffffff"}, {"time": round(t + fade, 4), "color": "ffffff00"}]
                att.append({"time": round(t + fade, 4), "name": None})
        out[slot] = {"attachment": sorted(att, key=lambda k: k["time"]), "rgba": sorted(rgba, key=lambda k: k["time"])}
    return out


def make_animations(rig):
    rig.data["events"] = {e: {} for e in EVENTS}
    arm_pose, _ = rig.arm_ik("evoker_tip", *EVOKER_TARGET)
    # 隐藏中的骨骼也给一个合理姿势，淡入时从这个角度转到位（例如举枪手臂从下方抬上来）
    ARM_DOWN = Pose({"upper_arm": {"r": -84}, "forearm": {"r": 8}, "hand": {"r": -6}, "arm_raised": {"r": -70}})
    EVOKER = add(arm_pose, Pose({"head": {"r": 5}, "torso": {"r": 1.5}}))
    # 亮枪：召唤器在胸前平端，让观众先看清他拿出了什么
    PRESENT = Pose({"upper_arm": {"r": -28}, "forearm": {"r": 22}, "hand": {"r": -4}, "head": {"r": -3}, "torso": {"r": -1}, "arm_raised": {"r": -40}})
    # 扣扳机后坐：头被猛推开、举枪手臂上跳、上身一震（配合游戏端的顿帧和震屏）
    KICK = add(EVOKER, Pose({"head": {"r": 15}, "arm_raised": {"r": 7}, "torso": {"r": 5}, "hip": {"x": -10, "y": -6}}))
    TENSE = add(EVOKER, Pose({"head": {"r": -2}, "arm_raised": {"r": -1.5}, "torso": {"r": -1}}))
    LOWER = add(PRESENT, Pose({"arm_raised": {"r": -25}}))
    SWORD_READY = Pose({"upper_arm": {"r": -58}, "forearm": {"r": 8}, "hand": {"r": 22}, "torso": {"r": -3}, "head": {"r": -2}, "hip": {"y": -10}, "arm_raised": {"r": -70}})
    ANTIC = Pose({"upper_arm": {"r": 30}, "forearm": {"r": 75}, "hand": {"r": 45}, "torso": {"r": 9}, "head": {"r": 4}, "hip": {"x": -14, "y": -20}, "arm_raised": {"r": -70}})
    WINDUP = Pose({"upper_arm": {"r": 95}, "forearm": {"r": 35}, "hand": {"r": 28}, "torso": {"r": 10}, "head": {"r": 3}, "hip": {"x": -10, "y": -16}, "arm_raised": {"r": -70}})
    SLASH = Pose({"upper_arm": {"r": -48}, "forearm": {"r": -18}, "hand": {"r": -16}, "torso": {"r": -12}, "head": {"r": -5}, "hip": {"x": 40, "y": -30}, "arm_raised": {"r": -70}})
    HURT = add(ARM_DOWN, Pose({"torso": {"r": 13}, "head": {"r": 16}, "arm_hang": {"r": 16}, "hip": {"x": -20, "y": -8}}))

    def breathe(t, period, amp=1.0):
        w = 2 * math.pi * t / period
        return Pose({
            "hip": {"y": -3.0 * amp * (1 - math.cos(w)) / 2},
            "torso": {"r": 1.0 * amp * math.sin(w)},
            "head": {"r": 0.8 * amp * math.sin(w + 0.4)},
            "upper_arm": {"r": 1.2 * amp * math.sin(w)},
            "arm_hang": {"r": 1.6 * amp * math.sin(w - 0.6)},
            "arm_raised": {"r": 0.8 * amp * math.sin(w - 0.4)},
        })

    def seq(keys):
        """keys: [(时间, Pose)]。目标姿势在关键点之间用 smoothstep 过渡；弹簧负责滞后与过冲。"""
        def f(t):
            if t <= keys[0][0]:
                return keys[0][1]
            for (t0, p0), (t1, p1) in zip(keys, keys[1:]):
                if t0 <= t <= t1:
                    return mix(p0, p1, ease_in_out((t - t0) / max(t1 - t0, 1e-6)))
            return keys[-1][1]
        return f

    HANG = arm_slots([(0, "hang")])
    bake_dynamic(rig, "idle_loop", 2.6, lambda t: add(ARM_DOWN, breathe(t, 2.6)), loop=True, slots=HANG)
    bake_dynamic(rig, "relaxed_loop", 3.4, lambda t: add(ARM_DOWN, breathe(t, 3.4, 0.7)), loop=True, slots=HANG)
    SLUMP = add(ARM_DOWN, Pose({"torso": {"r": -6}, "head": {"r": -12}, "hip": {"y": -18}, "arm_hang": {"r": 4}}))
    bake_dynamic(rig, "low_health_loop", 2.0, lambda t: add(SLUMP, breathe(t, 2.0, 1.6)), loop=True, slots=HANG)
    bake_dynamic(rig, "aim_loop", 1.8, lambda t: add(EVOKER, breathe(t, 1.8, 0.6)), loop=True, slots=arm_slots([(0, "raised")]))
    bake_dynamic(rig, "ready_sword_loop", 1.8, lambda t: add(SWORD_READY, breathe(t, 1.8, 0.8)), loop=True, slots=arm_slots([(0, "mesh_sword")]))

    # 攻击：拔剑 → 下蹲后引（预备）→ 举过头顶 → 一刀斩下（极快）→ 余势 → 收剑
    bake_dynamic(rig, "attack", 1.0, seq([(0.0, ARM_DOWN), (0.16, ANTIC), (0.25, WINDUP), (0.3, SLASH), (0.56, SLASH), (0.95, ARM_DOWN)]),
                 slots=arm_slots([(0, "hang"), (0.02, "mesh_sword"), (0.9, "hang")]), speed=1.7,
                 events=[(0.0, "weapon_draw"), (0.22, "weapon_windup"), (0.27, "weapon_slash"), (0.84, "weapon_sheath")])

    # 施法：网格手臂平端亮枪 → 抬起途中交叉淡化成手绘举枪手臂 → 停顿 → 扣扳机后坐 → 放下途中淡回网格手臂 → 下垂
    bake_dynamic(rig, "cast", 0.95, seq([(0.0, ARM_DOWN), (0.08, PRESENT), (0.12, PRESENT), (0.21, EVOKER), (0.26, TENSE), (0.272, KICK), (0.35, KICK), (0.55, EVOKER), (0.66, LOWER), (0.92, ARM_DOWN)]),
                 slots=arm_slots([(0, "hang"), (0.0, "mesh_evoker"), (0.15, "raised"), (0.6, "mesh_evoker"), (0.88, "hang")]), speed=2.0,
                 events=[(0.0, "evoker_draw"), (0.27, "evoker_fire"), (0.82, "evoker_sheath")])
    bake_dynamic(rig, "attack_sovereign", 0.85, seq([(0.0, ARM_DOWN), (0.06, PRESENT), (0.08, PRESENT), (0.16, EVOKER), (0.21, TENSE), (0.225, KICK), (0.3, KICK), (0.48, EVOKER), (0.58, LOWER), (0.83, ARM_DOWN)]),
                 slots=arm_slots([(0, "hang"), (0.0, "mesh_evoker"), (0.11, "raised"), (0.53, "mesh_evoker"), (0.79, "hang")]), speed=2.2,
                 events=[(0.0, "evoker_draw"), (0.22, "evoker_fire"), (0.74, "evoker_sheath")])

    # ───── 预备出牌的过渡动画（拖牌 / 取消 / 从预备姿势直接出招）─────
    # 抬起：先微微下沉蓄力（DIP）→ 沿弧线抬起 → 到位过冲回稳；末帧与对应循环动画首帧一致
    DIP = add(ARM_DOWN, Pose({"hip": {"y": -10}, "torso": {"r": 2}, "head": {"r": -3}, "upper_arm": {"r": -8}, "arm_hang": {"r": -6}}))
    bake_dynamic(rig, "aim_in", 0.4, seq([(0.0, ARM_DOWN), (0.07, DIP), (0.17, PRESENT), (0.31, EVOKER), (0.4, EVOKER)]),
                 slots=arm_slots([(0, "hang"), (0.04, "mesh_evoker"), (0.22, "raised")]), speed=1.5)
    bake_dynamic(rig, "sword_in", 0.36, seq([(0.0, ARM_DOWN), (0.07, DIP), (0.26, SWORD_READY), (0.36, SWORD_READY)]),
                 slots=arm_slots([(0, "hang"), (0.04, "mesh_sword")]), speed=1.5)
    # 放下：先停顿松劲 → 顺着重力放下 → 手臂轻晃停住
    RELAX_AIM = add(EVOKER, Pose({"arm_raised": {"r": -4}, "head": {"r": -2}}))
    bake_dynamic(rig, "aim_out", 0.5, seq([(0.0, EVOKER), (0.08, RELAX_AIM), (0.24, LOWER), (0.44, ARM_DOWN), (0.5, ARM_DOWN)]),
                 slots=arm_slots([(0, "raised"), (0.17, "mesh_evoker"), (0.4, "hang")]), speed=1.3,
                 events=[(0.32, "evoker_sheath")])
    SWORD_RELAX = add(SWORD_READY, Pose({"upper_arm": {"r": 6}, "hand": {"r": 8}}))
    bake_dynamic(rig, "sword_out", 0.5, seq([(0.0, SWORD_READY), (0.08, SWORD_RELAX), (0.42, ARM_DOWN), (0.5, ARM_DOWN)]),
                 slots=arm_slots([(0, "mesh_sword"), (0.4, "hang")]), speed=1.3,
                 events=[(0.2, "weapon_sheath")])
    # 已在预备姿势时出招：直接从当前姿势接上，不再先垂手
    bake_dynamic(rig, "attack_from_ready", 0.85, seq([(0.0, SWORD_READY), (0.1, ANTIC), (0.18, WINDUP), (0.23, SLASH), (0.46, SLASH), (0.8, ARM_DOWN)]),
                 slots=arm_slots([(0, "mesh_sword"), (0.76, "hang")]), speed=1.7,
                 events=[(0.15, "weapon_windup"), (0.2, "weapon_slash"), (0.7, "weapon_sheath")])
    for name in ("cast_from_aim", "sovereign_from_aim"):
        bake_dynamic(rig, name, 0.72, seq([(0.0, EVOKER), (0.05, TENSE), (0.062, KICK), (0.14, KICK), (0.3, EVOKER), (0.42, LOWER), (0.7, ARM_DOWN)]),
                     slots=arm_slots([(0, "raised"), (0.36, "mesh_evoker"), (0.64, "hang")]), speed=2.0,
                     events=[(0.06, "evoker_fire"), (0.56, "evoker_sheath")])

    bake_dynamic(rig, "hurt", 0.6, seq([(0.0, ARM_DOWN), (0.06, HURT), (0.14, HURT), (0.3, ARM_DOWN)]), speed=1.3, slots=HANG)

    DOWN = add(ARM_DOWN, Pose({"torso": {"r": -30}, "head": {"r": -34}, "arm_hang": {"r": -6}, "hip": {"y": -62, "x": 8}}))
    bake_dynamic(rig, "die", 1.5, seq([(0.0, ARM_DOWN), (0.08, HURT), (0.2, HURT), (0.9, DOWN), (1.5, DOWN)]), slots=HANG,
                 events=[(0.1, "death_particles_start"), (1.1, "death_particles_end"), (1.2, "explode_dead"), (1.35, "explode_end")])


# ───────────── 校验 ─────────────
def validate(data, images, master):
    sk = Skeleton(data, images)
    report = []
    ok = True
    # 1. 拼合校验：初始姿势 vs 母版
    W, H = round(1024 * K), round(1536 * K)
    origin = (ORIGIN[0] * K, ORIGIN[1] * K)
    saved = data["slots"][0].get("attachment")
    data["slots"][0]["attachment"] = None           # 不画影子
    r = np.asarray(sk.render(canvas=(W, H), origin=origin)).astype(float)
    data["slots"][0]["attachment"] = saved
    m = np.asarray(master.resize((W, H), Image.LANCZOS)).astype(float)
    body = m[..., 3] > 128
    diff = np.abs(r[..., :3] - m[..., :3]).mean(-1)[body].mean()
    miss = ((r[..., 3] < 64) & body).mean()
    passed = diff < 5 and miss < 0.001
    ok &= passed
    report.append(f"[{'OK' if passed else 'FAIL'}] 拼合校验：平均色差 {diff:.2f}（<5），缺失像素比例 {miss:.5f}（<0.001）")

    # 2. 只有 root / hip / torso，以及网格连接的 forearm / hand（手臂拉伸）允许平移；腿和头只转不移
    movers = {b for a in data["animations"].values() for b, k in a["bones"].items() if "translate" in k}
    passed = movers <= {"root", "hip", "torso", "forearm", "hand"}
    ok &= passed
    report.append(f"[{'OK' if passed else 'FAIL'}] 平移骨骼：{sorted(movers)}")

    # 3. 脚底漂移：脚踝世界坐标与 setup 的最大偏差（换算成母版像素）
    ankles = {}
    for side in "lr":
        b = next(x for x in data["bones"] if x["name"] == f"shin_{side}")
        kx, ky = to_spine(J[f"knee_{side}"])
        ax, ay = to_spine(J[f"ankle_{side}"])
        ankles[side] = (ax - kx, ay - ky)
    base = {s: sk.point(f"shin_{s}", ankles[s]) for s in "lr"}
    for name, anim in data["animations"].items():
        times = sorted({k["time"] for bk in anim["bones"].values() for tl in bk.values() for k in tl}) or [0.0]
        drift = 0.0
        for t in times:
            for s in "lr":
                p = sk.point(f"shin_{s}", ankles[s], name, t)
                drift = max(drift, math.dist(p, base[s]) / K)
        passed = drift <= 2.0
        ok &= passed
        report.append(f"[{'OK' if passed else 'FAIL'}] 脚底漂移 {name}: {drift:.2f}px（≤2）")

    # 5. 衔接校验：相接动画在接缝处的骨骼角度差（度）和平移差（单位）
    def edge(name, which):
        a = data["animations"][name]["bones"]
        out = {}
        for b, tl in a.items():
            for kind in ("rotate", "translate"):
                if kind in tl:
                    k = tl[kind][0 if which == "first" else -1]
                    out[(b, kind)] = (k.get("value", 0.0), k.get("x", 0.0), k.get("y", 0.0))
        return out

    pairs = [("aim_in", "aim_loop"), ("sword_in", "ready_sword_loop"), ("aim_loop", "aim_out"), ("ready_sword_loop", "sword_out"),
             ("aim_out", "idle_loop"), ("sword_out", "idle_loop"), ("ready_sword_loop", "attack_from_ready"),
             ("aim_loop", "cast_from_aim"), ("attack_from_ready", "idle_loop"), ("cast_from_aim", "idle_loop")]
    for a_name, b_name in pairs:
        ea, eb = edge(a_name, "last"), edge(b_name, "first")
        worst = 0.0
        for key in set(ea) | set(eb):
            va, vb = ea.get(key, (0, 0, 0)), eb.get(key, (0, 0, 0))
            worst = max(worst, max(abs(x - y) for x, y in zip(va, vb)))
        passed = worst <= 6.0
        ok &= passed
        report.append(f"[{'OK' if passed else 'FAIL'}] 衔接 {a_name} → {b_name}: 最大差 {worst:.2f}（≤6，其余由 0.1s 混合抹平）")

    # 4. 循环动画首尾一致
    for name, anim in data["animations"].items():
        if not name.endswith("_loop"):
            continue
        bad = 0
        for bk in anim["bones"].values():
            for tl in bk.values():
                a, b = tl[0], tl[-1]
                bad += any(abs(a.get(f, 0) - b.get(f, 0)) > 1e-3 for f in ("value", "x", "y"))
        ok &= bad == 0
        report.append(f"[{'OK' if bad == 0 else 'FAIL'}] 循环首尾一致 {name}")
    return ok, report


# ───────────── 导出 ─────────────
def pack_atlas(images, page="regent.png", width=2048, pad=2):
    items = sorted(images.items(), key=lambda kv: -kv[1].height)
    x = y = shelf = 0
    pos = {}
    for name, im in items:
        if x + im.width + pad > width:
            x, y, shelf = 0, y + shelf + pad, 0
        pos[name] = (x, y)
        x += im.width + pad
        shelf = max(shelf, im.height)
    h = 1
    while h < y + shelf:
        h *= 2
    sheet = Image.new("RGBA", (width, h), (0, 0, 0, 0))
    lines = [page, f"size:{width},{h}", "filter:Linear,Linear", "pma:false"]
    for name, im in items:
        sheet.paste(im, pos[name])
        lines += [name, f"bounds:{pos[name][0]},{pos[name][1]},{im.width},{im.height}"]
    return sheet, "\n".join(lines) + "\n"


def export(data, images):
    out = OUT / "export"
    out.mkdir(parents=True, exist_ok=True)
    sheet, atlas = pack_atlas(images)
    sheet.save(out / "regent.png")
    (out / "regent.atlas").write_text(atlas)
    (out / "regent.spine-json").write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")))
    return out


def previews(data, images):
    out = ROOT / "art/rig_preview"
    out.mkdir(parents=True, exist_ok=True)
    sk = Skeleton(data, images)
    for name, anim in data["animations"].items():
        times = sorted({k["time"] for bk in anim["bones"].values() for tl in bk.values() for k in tl})
        frames = [sk.render(name, t, canvas=(1150, 1620), origin=(470, 1560), bg=(36, 40, 60, 255)).convert("RGB").resize((300, 423), Image.LANCZOS) for t in times]
        frames = [f.convert("P", palette=Image.ADAPTIVE, colors=200) for f in frames]
        frames[0].save(out / f"{name}.gif", save_all=True, append_images=frames[1:], duration=33, loop=0, optimize=True)
    return out


def stage_pck(out):
    """生成 PCK 打包目录（res:// 结构）。Spine 资源直接写成 spine-godot 导入后的格式，
    不依赖 Spine 编辑器插件：.spjson = 原始 JSON；.spatlas = 包着 atlas 文本的 JSON。
    regent.png 用 tools/pck/godot_tex.py 直接生成 .ctex（见 docs/BUILD.md）。"""
    import hashlib
    stage = ROOT / "mod/pck_stage"
    rel = "animations/characters/regent"
    d = stage / rel
    imp = stage / ".godot/imported"
    d.mkdir(parents=True, exist_ok=True)
    imp.mkdir(parents=True, exist_ok=True)
    for f in ("regent.atlas", "regent.spine-json"):
        (d / f).write_bytes((out / f).read_bytes())
    # 贴图直接生成导入产物（png + .import + .ctex），沿用原版 regent.png 的 uid；不再需要本地 Godot 导入
    sys.path.insert(0, str(ROOT / "tools/pck"))
    from godot_tex import write_texture
    from PIL import Image as _Image
    write_texture(stage, f"{rel}/regent.png", _Image.open(out / "regent.png"), "bhmdtitp0en6e")

    def h(path):
        return hashlib.md5(path.encode()).hexdigest()

    atlas_res = f"res://{rel}/regent.atlas"
    atlas_imp = f"regent.atlas-{h(atlas_res)}.spatlas"
    (imp / atlas_imp).write_text(json.dumps({
        "atlas_data": (out / "regent.atlas").read_text(),
        "normal_texture_prefix": "n",
        "source_path": atlas_res,
        "specular_texture_prefix": "s",
    }, separators=(",", ":")))
    # 沿用原版 regent.atlas 的 uid，保证任何按 uid 引用它的地方都指向新图集
    (d / "regent.atlas.import").write_text(f"""[remap]

importer="spine.atlas"
type="SpineAtlasResource"
uid="uid://bpm883hqkd573"
path="res://.godot/imported/{atlas_imp}"

[deps]

source_file="{atlas_res}"
dest_files=["res://.godot/imported/{atlas_imp}"]

[params]

normal_map_prefix="n"
specular_map_prefix="s"
""")

    skel_res = f"res://{rel}/regent.spine-json"
    skel_imp = f"regent.spine-json-{h(skel_res)}.spjson"
    (imp / skel_imp).write_bytes((out / "regent.spine-json").read_bytes())
    (d / "regent.spine-json.import").write_text(f"""[remap]

importer="spine.json"
type="SpineSkeletonFileResource"
path="res://.godot/imported/{skel_imp}"

[deps]

source_file="{skel_res}"
dest_files=["res://.godot/imported/{skel_imp}"]

[params]

""")
    (d / "regent_skel_data.tres").write_bytes((ROOT / "mod/pck_project" / rel / "regent_skel_data.tres").read_bytes())
    return stage


def main():
    data, images, master = build_setup()
    rig = Rig(data)
    make_animations(rig)
    ok, report = validate(data, images, master)
    print("\n".join(report))
    (OUT).mkdir(parents=True, exist_ok=True)
    (OUT / "VALIDATION.txt").write_text("\n".join(report) + "\n")
    if not ok:
        print("校验未通过，不导出。")
        sys.exit(1)
    out = export(data, images)
    print("导出：", out)
    dst = ROOT / "mod/pck_project/animations/characters/regent"
    dst.mkdir(parents=True, exist_ok=True)
    for f in ("regent.png", "regent.atlas", "regent.spine-json"):
        (dst / f).write_bytes((out / f).read_bytes())
    print("已同步到 PCK 工程：", dst)
    print("PCK 打包目录：", stage_pck(out))
    print("预览：", previews(data, images))


if __name__ == "__main__":
    main()
