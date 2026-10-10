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

K = 0.77                       # 像素 → Spine 单位（让角色高度接近原版储君）
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
    m["upper_arm"] = ((arm_zone & (xs >= sx)) | cap) & (xs < 625)
    m["forearm"] = arm_zone & (xs >= 588) & (xs < 718)
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


def prop_part(src, cuff_px, cuff_h_px, target_cuff, target_h, keep_left):
    """把单独生成的"前臂+手+道具"图按袖口对齐到母版：
    cuff_px = 道具图中袖口中心，cuff_h_px = 道具图中袖口高度；target_* 为母版上的对应值。
    keep_left = 袖子保留到袖口左侧多少像素（母版坐标）。"""
    s = target_h / cuff_h_px
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
    ("upper_arm", "upper_arm", "upper_arm"),
    ("forearm", "forearm", "forearm"),
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
        im, center = prop_part(src, cuff, cuff_h, MASTER_CUFF, MASTER_SLEEVE_H, keep_left=10)
        images[name] = im
        centers[name] = center
        s = MASTER_SLEEVE_H / cuff_h
        tips[name] = (MASTER_CUFF[0] + (tip[0] - cuff[0]) * s, MASTER_CUFF[1] + (tip[1] - cuff[1]) * s)
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
    bone_of.update({"hand_open": "hand", "hand_evoker": "hand", "hand_sword": "hand"})
    skin = {}
    for slot, bone, att in SLOTS:
        skin.setdefault(slot, {})
    for att, im in scaled.items():
        slot = "hand" if att.startswith("hand") else att
        bone = bone_of[att] if att in bone_of else slot
        bx, by = to_spine(joint[bone])
        cx, cy = to_spine(centers[att])
        skin[slot][att] = {"x": round(cx - bx, 3), "y": round(cy - by, 3), "width": im.width, "height": im.height}

    data = {
        "skeleton": {"spine": "4.2.43", "images": "./", "audio": "", "x": -300, "y": -20, "width": 820, "height": 1200},
        "bones": bones,
        "slots": [{"name": s, "bone": b, "attachment": a} for s, b, a in SLOTS],
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

    def arm_ik(self, tip_bone, target_px, aim_deg, elbow_up=True):
        """手臂三段 IK：让 tip_bone（枪口 / 剑尖）到达 target_px（母版像素），道具朝向世界角 aim_deg。
        返回 (upper_arm, forearm, hand) 的旋转增量。"""
        sx, sy = self.setup_world("upper_arm")
        ex, ey = self.setup_world("forearm")
        wx, wy = self.setup_world("hand")
        tx0, ty0 = self.setup_world(tip_bone)
        tip_len = math.dist((wx, wy), (tx0, ty0))
        tip_ang0 = math.atan2(ty0 - wy, tx0 - wx)        # setup 中 道具尖端相对手腕的方向
        l1 = math.dist((sx, sy), (ex, ey))
        l2 = math.dist((ex, ey), (wx, wy))
        T = to_spine(target_px)
        aim = math.radians(aim_deg)
        # 手腕位置 = 目标 - 道具向量（道具整体旋转 aim - tip_ang0）
        W = (T[0] - tip_len * math.cos(aim), T[1] - tip_len * math.sin(aim))
        d = min(math.dist((sx, sy), W), l1 + l2 - 1e-3)
        base = math.atan2(W[1] - sy, W[0] - sx)
        a = math.acos(max(-1, min(1, (l1 * l1 + d * d - l2 * l2) / (2 * l1 * d))))
        cands = []
        for sgn in (1, -1):
            u = base + sgn * a
            elbow = (sx + l1 * math.cos(u), sy + l1 * math.sin(u))
            f = math.atan2(W[1] - elbow[1], W[0] - elbow[0])
            cands.append((elbow[1], u, f))
        _, u, f = max(cands) if elbow_up else min(cands)
        upper0 = math.atan2(ey - sy, ex - sx)
        fore0 = math.atan2(wy - ey, wx - ex)
        hand_world = aim - tip_ang0 + fore0          # 手的世界旋转（相对 setup 方向的增量 + setup 方向）
        r_upper = math.degrees(u - upper0)
        r_fore = math.degrees((f - u) - (fore0 - upper0))
        r_hand = math.degrees((aim - tip_ang0) - (f - fore0))
        return r_upper, r_fore, r_hand


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


# ───────────── 动画定义 ─────────────
EVOKER_TARGET = ((418, 112), 225)      # 枪口抵住的太阳穴位置（母版像素）和枪管朝向


def make_animations(rig):
    eu, ef, eh = rig.arm_ik("evoker_tip", *EVOKER_TARGET)
    ARM_DOWN = Pose({"upper_arm": {"r": -84}, "forearm": {"r": 6}, "hand": {"r": -4}})
    EVOKER = Pose({"upper_arm": {"r": eu}, "forearm": {"r": ef}, "hand": {"r": eh}, "head": {"r": 5}, "torso": {"r": 1.5}})
    SWORD_READY = Pose({"upper_arm": {"r": -58}, "forearm": {"r": 8}, "hand": {"r": 22}, "torso": {"r": -3}, "head": {"r": -2}, "hip": {"y": -8}})
    WINDUP = Pose({"upper_arm": {"r": 78}, "forearm": {"r": 48}, "hand": {"r": 30}, "torso": {"r": 7}, "head": {"r": 3}, "hip": {"x": -10, "y": -14}})
    SLASH = Pose({"upper_arm": {"r": -42}, "forearm": {"r": -16}, "hand": {"r": -12}, "torso": {"r": -10}, "head": {"r": -4}, "hip": {"x": 34, "y": -26}})
    HURT = Pose({"torso": {"r": 11}, "head": {"r": 14}, "upper_arm": {"r": -55}, "forearm": {"r": 18}, "hip": {"x": -18, "y": -6}})

    def breathe(t, period, amp=1.0):
        """呼吸：躯干轻微前后摆 + 重心起伏，头和手臂带相位滞后（跟随感）。"""
        w = 2 * math.pi * t / period
        return Pose({
            "hip": {"y": -2.5 * amp * (1 - math.cos(w)) / 2},
            "torso": {"r": 0.9 * amp * math.sin(w)},
            "head": {"r": 1.2 * amp * math.sin(w - 0.6)},
            "upper_arm": {"r": 1.6 * amp * math.sin(w - 0.9)},
            "forearm": {"r": 1.2 * amp * math.sin(w - 1.3)},
        })

    def seq(t, keys):
        """keys: [(时间, Pose, 缓动)]，在相邻关键姿势间插值。"""
        if t <= keys[0][0]:
            return keys[0][1]
        for (t0, p0, _), (t1, p1, ez) in zip(keys, keys[1:]):
            if t0 <= t <= t1:
                return mix(p0, p1, ez((t - t0) / (t1 - t0)))
        return keys[-1][1]

    hand = lambda *pairs: {"hand": {"attachment": [{"time": t, "name": n} for t, n in pairs]}}

    bake(rig, "idle_loop", 2.4, lambda t: add(ARM_DOWN, breathe(t, 2.4)), loop=True)
    bake(rig, "relaxed_loop", 3.2, lambda t: add(ARM_DOWN, breathe(t, 3.2, 0.7)), loop=True)
    SLUMP = add(ARM_DOWN, Pose({"torso": {"r": -6}, "head": {"r": -12}, "hip": {"y": -16}, "upper_arm": {"r": 6}}))
    bake(rig, "low_health_loop", 2.0, lambda t: add(SLUMP, breathe(t, 2.0, 1.6)), loop=True)

    bake(rig, "aim_loop", 1.6, lambda t: add(EVOKER, breathe(t, 1.6, 0.6)), loop=True, slots=hand((0, "hand_evoker")))
    bake(rig, "ready_sword_loop", 1.6, lambda t: add(SWORD_READY, breathe(t, 1.6, 0.8)), loop=True, slots=hand((0, "hand_sword")))

    # 攻击：举剑蓄力 → 斩下（极快）→ 定格 → 收势
    attack_keys = [(0.0, ARM_DOWN, ease_out_cubic), (0.14, WINDUP, ease_out_cubic), (0.22, SLASH, ease_out_expo),
                   (0.42, SLASH, ease_out_cubic), (0.8, ARM_DOWN, ease_in_out)]
    bake(rig, "attack", 0.8, lambda t: seq(t, attack_keys), slots=hand((0, "hand_sword"), (0.78, "hand_open")))

    # 施法：举起召唤器抵住太阳穴 → 扣扳机（头和手轻微后坐）→ 放下
    KICK = add(EVOKER, Pose({"head": {"r": 6}, "hand": {"r": -8}, "torso": {"r": 2}}))
    cast_keys = [(0.0, ARM_DOWN, ease_out_cubic), (0.2, EVOKER, ease_out_expo), (0.26, EVOKER, ease_out_cubic),
                 (0.3, KICK, ease_out_expo), (0.55, EVOKER, ease_out_cubic), (0.9, ARM_DOWN, ease_in_out)]
    bake(rig, "cast", 0.9, lambda t: seq(t, cast_keys), slots=hand((0, "hand_evoker"), (0.85, "hand_open")))
    sov_keys = [(0.0, ARM_DOWN, ease_out_cubic), (0.14, EVOKER, ease_out_expo), (0.2, KICK, ease_out_expo),
                (0.45, EVOKER, ease_out_cubic), (0.75, ARM_DOWN, ease_in_out)]
    bake(rig, "attack_sovereign", 0.75, lambda t: seq(t, sov_keys), slots=hand((0, "hand_evoker"), (0.7, "hand_open")))

    hurt_keys = [(0.0, ARM_DOWN, ease_out_cubic), (0.08, HURT, ease_out_expo), (0.45, ARM_DOWN, ease_in_out)]
    bake(rig, "hurt", 0.45, lambda t: seq(t, hurt_keys))

    # 死亡：受击后仰 → 失去力气向前垂下，重心下沉（脚仍钉在地面）
    DOWN = Pose({"torso": {"r": -30}, "head": {"r": -34}, "upper_arm": {"r": -96}, "forearm": {"r": 12}, "hand": {"r": 8}, "hip": {"y": -62, "x": 8}})
    die_keys = [(0.0, ARM_DOWN, ease_out_cubic), (0.12, HURT, ease_out_expo), (0.9, DOWN, ease_in_out), (1.4, DOWN, ease_out_cubic)]
    bake(rig, "die", 1.4, lambda t: seq(t, die_keys),
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

    # 2. 只有 root / hip / torso 允许平移（四肢只转不移 → 关节不会脱开）
    movers = {b for a in data["animations"].values() for b, k in a["bones"].items() if "translate" in k}
    passed = movers <= {"root", "hip", "torso"}
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
        frames = [sk.render(name, t, canvas=(900, 1250), origin=(380, 1180), bg=(36, 40, 60, 255)).convert("RGB").resize((300, 416), Image.LANCZOS) for t in times]
        frames = [f.convert("P", palette=Image.ADAPTIVE, colors=200) for f in frames]
        frames[0].save(out / f"{name}.gif", save_all=True, append_images=frames[1:], duration=33, loop=0, optimize=True)
    return out


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
    print("预览：", previews(data, images))


if __name__ == "__main__":
    main()
