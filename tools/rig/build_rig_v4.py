"""结城理骨骼 v4：近侧右臂 + 姿势专用手绘手臂（修正 v1~v3 用远侧左臂做动作的解剖错误）。
用法：python3 tools/rig/build_rig_v4.py
- 身体母版：art/v4/body_cut.png（去掉右臂的身体，已抠图）
- 右臂：art/v4/rarm_*.png，每张挂在一根以右肩为轴的骨骼上，状态切换用插槽透明度交叉淡化
- 特效锚点（hand / grip / muzzle / evoker_tip / sword_tip）是 root 的子骨骼，每帧烘焙到"当前显示的那张手臂"的对应位置
规范见 docs/RIG.md 第 9 节。"""
import json
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

sys.path.insert(0, str(Path(__file__).parent))
from spine_eval import Skeleton  # noqa: E402
from build_rig import (Pose, Spring, add, mix, ease_in_out, key_image, shadow_image,  # noqa: E402
                       pack_atlas, outline)

ROOT = Path(__file__).resolve().parents[2]
BODY = ROOT / "art/v4/body_cut.png"
OUT = ROOT / "art/rig"
FPS = 30
K = 1.0
ORIGIN = (500.0, 1480.0)           # 两脚之间的地面


def to_spine(p):
    return ((p[0] - ORIGIN[0]) * K, (ORIGIN[1] - p[1]) * K)


# ───────────── 身体关节（母版像素）─────────────
J = {
    "hip": (520, 600),
    "neck": (530, 205),
    "shoulder": (398, 330),          # 近侧右肩（画面左侧）
    "temple": (505, 110),            # 近侧太阳穴（露出耳朵的一侧）
    "chest": (520, 400),
    "hip_l": (455, 640), "knee_l": (440, 1000), "ankle_l": (370, 1385),     # 近侧右腿（画面左）
    "hip_r": (585, 640), "knee_r": (598, 1000), "ankle_r": (630, 1345),     # 远侧左腿（画面右）
}


def leg_gap(y):
    pts = [(600, 515), (700, 520), (800, 525), (900, 530), (1000, 520), (1100, 510), (1200, 505), (1300, 485), (1400, 470), (1536, 470)]
    for (y0, x0), (y1, x1) in zip(pts, pts[1:]):
        if y0 <= y <= y1:
            return x0 + (x1 - x0) * (y - y0) / (y1 - y0)
    return pts[-1][1]


def neck_cut(x):
    """头部下边界：后脑头发 ~y190，下巴 ~y200。"""
    return 195 if x < 520 else 205


def cut_body(master):
    a = np.asarray(master)
    h, w = a.shape[:2]
    ys, xs = np.mgrid[0:h, 0:w]
    gap = np.vectorize(leg_gap)(np.arange(h))[:, None]
    neck = np.vectorize(neck_cut)(np.arange(w))[None, :]
    m = {}
    m["head"] = (ys < neck) | ((ys < neck + 24) & (xs > 495) & (xs < 575))
    flap = ((xs < 485) & (ys < 728)) | ((xs > 630) & (ys < 700))       # 近侧衣摆 + 枪套、远侧插兜的手
    legs = (ys >= 600) & ~flap
    left, right = legs & (xs < gap), legs & (xs >= gap)
    m["thigh_l"], m["shin_l"] = left & (ys < 1020), left & (ys >= 980)
    m["thigh_r"], m["shin_r"] = right & (ys < 1020), right & (ys >= 980)
    m["torso"] = (ys >= neck - 6) & ((ys < 645) | flap)
    parts = {}
    for name, mk in m.items():
        soft = np.asarray(Image.fromarray((mk * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2))).astype(np.float32) / 255
        rgba = a.copy()
        rgba[..., 3] = (a[..., 3].astype(np.float32) * np.minimum(soft * 1.4, 1.0) * (soft > 0.02)).clip(0, 255).astype(np.uint8)
        im = Image.fromarray(rgba, "RGBA")
        bbox = im.getchannel("A").point(lambda v: 255 if v > 3 else 0).getbbox()
        parts[name] = (im.crop(bbox), ((bbox[0] + bbox[2]) / 2, (bbox[1] + bbox[3]) / 2))
    return parts


# ───────────── 右臂手绘图：源图关键点（像素）与对齐方式 ─────────────
# shoulder：袖子在肩部的开口中心；anchors：特效锚点；scale：缩放（让袖子粗细与身体一致）；
# rot：额外旋转（度，图像坐标系，顺时针为正）；evoker 用"肩点 + 枪口对太阳穴"两点定位自动求 scale / rot。
ARMS = {
    "hang": {"file": "rarm_hang", "shoulder": (390, 370), "scale": 0.54, "rot": 0.0,
             "anchors": {"hand": (335, 1060)}},
    "present": {"file": "rarm_present", "shoulder": (240, 380), "scale": 0.6, "rot": 0.0,
                "anchors": {"hand": (715, 535), "grip": (755, 428), "evoker_tip": (978, 434), "muzzle": (978, 434)}},
    "evoker": {"file": "rarm_evoker", "shoulder": (345, 585), "two_point": ("muzzle", "temple"),
               "anchors": {"hand": (370, 240), "grip": (360, 145), "evoker_tip": (595, 142), "muzzle": (595, 142)}},
    "s_ready": {"file": "rarm_sword_ready", "shoulder": (330, 305), "scale": 0.55, "rot": 0.0,
                "anchors": {"hand": (470, 870), "sword_tip": (1000, 1390)}},
    "s_up": {"file": "rarm_sword_up", "shoulder": (385, 745), "scale": 0.55, "rot": 0.0,
             "anchors": {"hand": (420, 265), "sword_tip": (50, 32)}},
    "s_down": {"file": "rarm_sword_down", "shoulder": (330, 310), "scale": 0.55, "rot": 0.0,
               "anchors": {"hand": (480, 880), "sword_tip": (1000, 1430)}},
}
# grip 取枪管后端（不是握把），这样 muzzle - grip 就是枪管方向，贯穿特效沿枪管穿过头部
ANCHORS = ["hand", "grip", "muzzle", "evoker_tip", "sword_tip"]


def arm_transform(cfg):
    """返回 (scale, rot_deg_image)：源图像素 → 母版像素 的相似变换参数（以肩点为原点）。"""
    if "two_point" in cfg:
        anchor, target = cfg["two_point"]
        s0 = np.array(cfg["shoulder"])
        a0 = np.array(cfg["anchors"][anchor])
        t0, t1 = np.array(J["shoulder"]), np.array(J[target])
        v_src, v_dst = a0 - s0, t1 - t0
        scale = np.linalg.norm(v_dst) / np.linalg.norm(v_src)
        rot = math.degrees(math.atan2(v_dst[1], v_dst[0]) - math.atan2(v_src[1], v_src[0]))
        return float(scale), float(rot)
    return cfg["scale"], cfg.get("rot", 0.0)


def map_point(cfg, p):
    scale, rot = arm_transform(cfg)
    r = math.radians(rot)
    d = np.array(p) - np.array(cfg["shoulder"])
    d = np.array([d[0] * math.cos(r) - d[1] * math.sin(r), d[0] * math.sin(r) + d[1] * math.cos(r)]) * scale
    return (J["shoulder"][0] + d[0], J["shoulder"][1] + d[1])


def load_arm(cfg):
    """抠图 → 按相似变换缩放旋转 → 裁边；返回图像和它在母版中的中心。"""
    src = key_image(ROOT / "art/v4" / (cfg["file"] + ".png"))
    bbox = src.getchannel("A").point(lambda v: 255 if v > 3 else 0).getbbox()
    crop = src.crop(bbox)
    scale, rot = arm_transform(cfg)
    im = crop.resize((max(1, round(crop.width * scale)), max(1, round(crop.height * scale))), Image.LANCZOS)
    c_src = ((bbox[0] + bbox[2]) / 2, (bbox[1] + bbox[3]) / 2)
    center = map_point(cfg, c_src)
    if abs(rot) > 0.05:
        im = im.rotate(-rot, resample=Image.BICUBIC, expand=True)   # PIL 逆时针为正；图像坐标顺时针为正
    im = outline(im, 1, (10, 10, 20))
    return im, center


# ───────────── 骨骼 / 插槽 ─────────────
BODY_BONES = [
    ("root", None, ORIGIN),
    ("hip", "root", J["hip"]),
    ("torso", "hip", J["hip"]),
    ("chest_particle_attach", "torso", J["chest"]),
    ("head", "torso", J["neck"]),
    ("arm_particle_attach", "torso", (380, 520)),
    ("thigh_l", "hip", J["hip_l"]), ("shin_l", "thigh_l", J["knee_l"]), ("leg_particle_attach_l", "shin_l", (400, 1250)),
    ("thigh_r", "hip", J["hip_r"]), ("shin_r", "thigh_r", J["knee_r"]), ("leg_particle_attach", "shin_r", (612, 1250)),
]
ARM_BONE = {k: "rarm_" + k for k in ARMS}


def build_setup():
    master = Image.open(BODY).convert("RGBA")
    parts = cut_body(master)
    images, centers = {}, {}
    for n, (im, c) in parts.items():
        images[n], centers[n] = im, c
    for k, cfg in ARMS.items():
        im, c = load_arm(cfg)
        images["rarm_" + k], centers["rarm_" + k] = im, c
    images["shadow"], centers["shadow"] = shadow_image(), (ORIGIN[0] + 10, ORIGIN[1] - 10)

    joint = {b[0]: b[2] for b in BODY_BONES}
    bones = []
    for name, parent, _ in BODY_BONES:
        b = {"name": name}
        if parent:
            wx, wy = to_spine(joint[name])
            px, py = to_spine(joint[parent])
            b.update(parent=parent, x=round(wx - px, 3), y=round(wy - py, 3))
        bones.append(b)
    for k in ARMS:
        sx, sy = to_spine(J["shoulder"])
        tx, ty = to_spine(J["hip"])
        bones.append({"name": ARM_BONE[k], "parent": "torso", "x": round(sx - tx, 3), "y": round(sy - ty, 3)})
        joint[ARM_BONE[k]] = J["shoulder"]
    for a in ANCHORS:                                      # 锚点：root 子骨骼，位置全靠烘焙的平移
        bones.append({"name": a, "parent": "root"})
        joint[a] = ORIGIN

    slots = [("shadow", "root", "shadow"), ("shin_r", "shin_r", "shin_r"), ("thigh_r", "thigh_r", "thigh_r"),
             ("shin_l", "shin_l", "shin_l"), ("thigh_l", "thigh_l", "thigh_l"), ("head", "head", "head"),
             ("torso", "torso", "torso")]
    slots += [("rarm_" + k, ARM_BONE[k], "rarm_hang" if k == "hang" else None) for k in ARMS]

    skin = {}
    for slot, bone, _ in slots:
        att = slot
        im = images[att]
        bx, by = to_spine(joint[bone])
        cx, cy = to_spine(centers[att])
        skin[slot] = {att: {"x": round(cx - bx, 3), "y": round(cy - by, 3), "width": im.width, "height": im.height}}

    data = {
        "skeleton": {"spine": "4.2.43", "images": "./", "audio": "", "x": -320, "y": -20, "width": 820, "height": 1520},
        "bones": bones,
        "slots": [dict({"name": s, "bone": b}, **({"attachment": a} if a else {})) for s, b, a in slots],
        "skins": [{"name": "default", "attachments": skin}],
        "events": {},
        "animations": {},
    }
    return data, images, master


# ───────────── 腿部 IK（髋部移动时脚钉在地上）─────────────
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
        hx, hy = self.setup_world("hip")
        dx, dy = pose.get("hip", {}).get("x", 0.0), pose.get("hip", {}).get("y", 0.0)
        tx, ty = self.setup_world(f"thigh_{side}")
        kx, ky = self.setup_world(f"shin_{side}")
        ax, ay = to_spine(J[f"ankle_{side}"])
        root = (tx + dx, ty + dy)
        l1, l2 = math.dist((tx, ty), (kx, ky)), math.dist((kx, ky), (ax, ay))
        d = min(math.dist(root, (ax, ay)), l1 + l2 - 1e-3)
        base = math.atan2(ay - root[1], ax - root[0])
        a = math.acos(max(-1, min(1, (l1 * l1 + d * d - l2 * l2) / (2 * l1 * d))))
        thigh_w = base + a                              # 两腿都向前（画面右）屈膝
        knee = (root[0] + l1 * math.cos(thigh_w), root[1] + l1 * math.sin(thigh_w))
        shin_w = math.atan2(ay - knee[1], ax - knee[0])
        thigh0, shin0 = math.atan2(ky - ty, kx - tx), math.atan2(ay - ky, ax - kx)
        pose[f"thigh_{side}"] = {"r": math.degrees(thigh_w - thigh0)}
        pose[f"shin_{side}"] = {"r": math.degrees((shin_w - thigh_w) - (shin0 - thigh0))}
        return pose


# ───────────── 弹簧动力学（同 v3）─────────────
DYN = {"hip": (3.4, 0.85, 0.0), "torso": (3.0, 0.68, 0.0), "head": (2.3, 0.5, 0.0)}
DYN.update({ARM_BONE[k]: (4.6, 0.55, 0.0) for k in ARMS})
DYN[ARM_BONE["hang"]] = (3.2, 0.42, 0.0)          # 下垂手臂更松，晃动更明显

def _hand_dir(k):
    """手臂 k 静止时，肩 → 手 的方向（Spine 坐标，度）。"""
    sx, sy = to_spine(J["shoulder"])
    hx, hy = to_spine(map_point(ARMS[k], ARMS[k]["anchors"]["hand"]))
    return math.degrees(math.atan2(hy - sy, hx - sx))


THETA = {k: _hand_dir(k) for k in ARMS}


def arm_pose(state, extra=None):
    """方向对齐：当前手臂转到 0；其余（隐藏的）手臂都转到和当前手臂"手的方向"一致。
    切换 A→B 时，B 一出现就指向 A 的手所在方向，再转回自己的姿势；A 则转向 B —— 画面上是一条手臂连续扫过。"""
    p = Pose({ARM_BONE[k]: {"r": THETA[state] - THETA[k]} for k in ARMS})
    p[ARM_BONE[state]] = {"r": 0.0}
    if extra:
        p = add(p, extra)
    return p


def _st(entry, fade):
    return (entry[0], entry[1], entry[2] if len(entry) > 2 else fade)


def arm_alpha(states, fade, t):
    """states: [(时间, 手臂状态[, 淡化秒])] → 当前时刻各手臂的透明度（线性交叉淡化）。"""
    states = [_st(e, fade) for e in states]
    alpha = {k: 0.0 for k in ARMS}
    alpha[states[0][1]] = 1.0
    for (t0, a, _), (t1, b, f1) in zip(states, states[1:]):
        if t >= t1:
            u = min(1.0, (t - t1) / f1)
            alpha[a] = 0.0 if a != b else 1.0
            alpha[b] = 1.0
            if a != b and u < 1.0:
                alpha[a] = 1.0 - u
                alpha[b] = u
    return alpha


def slot_timelines(states, fade):
    states = [_st(e, fade) for e in states]
    out = {}
    for k in ARMS:
        slot = "rarm_" + k
        att, rgba = [], []
        prev_on = None
        for i, (t, st, fade) in enumerate(states):
            on = st == k
            if i == 0:
                att.append({"time": 0.0, "name": slot if on else None})
                rgba.append({"time": 0.0, "color": "ffffffff" if on else "ffffff00"})
            elif on and not prev_on:
                att.append({"time": t, "name": slot})
                rgba += [{"time": t, "color": "ffffff00"}, {"time": round(t + fade, 4), "color": "ffffffff"}]
            elif prev_on and not on:
                rgba += [{"time": t, "color": "ffffffff"}, {"time": round(t + fade, 4), "color": "ffffff00"}]
                att.append({"time": round(t + fade, 4), "name": None})
            prev_on = on
        out[slot] = {"attachment": sorted(att, key=lambda x: x["time"]), "rgba": sorted(rgba, key=lambda x: x["time"])}
    return out


def bake(rig, images, name, duration, target, states, loop=False, events=None, speed=1.0, fade=0.05):
    """弹簧积分 240Hz → 30fps 关键帧；腿部 IK；插槽淡化；最后按手臂透明度烘焙特效锚点。"""
    sub, T = 240, 1.0 / 240
    p0 = target(0.0)
    chans = {}
    for b in set(DYN) | set(p0):
        f, z, r = DYN.get(b, (4.0, 0.7, 0.0))
        for c in ("r", "x", "y"):
            chans[(b, c)] = Spring(p0.get(b, {}).get(c, 0.0), f * speed, z, r)
    warm = 2 if loop else 0
    steps = int(round(duration * sub))
    frames = []
    for i in range(steps * (warm + 1) + 1):
        t = (i * T) % duration if loop else min(i * T, duration)
        tp = target(t)
        cur = Pose()
        for (b, c), sp in chans.items():
            v = sp.step(T, tp.get(b, {}).get(c, 0.0))
            if abs(v) > 1e-6:
                cur.setdefault(b, {})[c] = v
        if i >= steps * warm and (i - steps * warm) % (sub // FPS) == 0:
            frames.append((round((i - steps * warm) * T, 4), cur))
    if loop:
        frames[-1] = (frames[-1][0], frames[0][1])
    bones = {}
    solved = [(t, rig.leg_ik(rig.leg_ik(Pose({b: dict(v) for b, v in p.items()}), "l"), "r")) for t, p in frames]
    names = set().union(*[set(p) for _, p in solved])
    for b in sorted(names):
        rot = [{"time": t, "value": round(p.get(b, {}).get("r", 0.0), 3)} for t, p in solved]
        tr = [{"time": t, "x": round(p.get(b, {}).get("x", 0.0), 3), "y": round(p.get(b, {}).get("y", 0.0), 3)} for t, p in solved]
        e = {}
        if any(abs(k["value"]) > 1e-4 for k in rot):
            e["rotate"] = rot
        if b in ("hip",) and any(abs(k["x"]) > 1e-4 or abs(k["y"]) > 1e-4 for k in tr):
            e["translate"] = tr
        if e:
            bones[b] = e
    anim = {"bones": bones, "slots": slot_timelines(states, fade)}
    if events:
        anim["events"] = [{"time": t, "name": n} for t, n in sorted(events)]
    rig.data["animations"][name] = anim

    # 锚点烘焙：各手臂锚点的世界坐标按透明度加权
    sk = Skeleton(rig.data, images)
    local = {k: {a: (to_spine(map_point(ARMS[k], p))[0] - to_spine(J["shoulder"])[0],
                     to_spine(map_point(ARMS[k], p))[1] - to_spine(J["shoulder"])[1])
                 for a, p in ARMS[k]["anchors"].items()} for k in ARMS}
    keys = {a: [] for a in ANCHORS}
    last = {a: (0.0, 0.0) for a in ANCHORS}
    support = {a: [] for a in ANCHORS}
    for t, _ in frames:
        W = sk.world(name, t)
        al = arm_alpha(states, fade, t)
        for a in ANCHORS:
            sx = sy = sw = 0.0
            for k in ARMS:
                if al[k] <= 0 or a not in local[k]:
                    continue
                pt = W[ARM_BONE[k]] @ np.array([local[k][a][0], local[k][a][1], 1.0])
                sx, sy, sw = sx + pt[0] * al[k], sy + pt[1] * al[k], sw + al[k]
            if sw > 0:
                last[a] = (sx / sw, sy / sw)
            elif a != "hand":
                last[a] = last["hand"]                    # 没有道具时锚点跟随手（道具显现时从手里长出来）
            support[a].append(sw > 0.999)
            keys[a].append({"time": t, "x": round(last[a][0], 2), "y": round(last[a][1], 2)})
    for a in ANCHORS:
        bones[a] = {"translate": keys[a]}
    SUPPORT[name] = support
    VISIBLE[name] = (_st(states[0], fade)[1], _st(states[-1], fade)[1])


SUPPORT = {}
VISIBLE = {}


# ───────────── 动画 ─────────────
EVENTS = ["death_particles_start", "death_particles_end", "explode_dead", "explode_end",
          "weapon_draw", "weapon_windup", "weapon_slash", "weapon_sheath", "evoker_draw", "evoker_fire", "evoker_sheath"]


def make_animations(rig, images):
    rig.data["events"] = {e: {} for e in EVENTS}
    A = arm_pose

    def breathe(t, period, amp=1.0, arm="hang"):
        w = 2 * math.pi * t / period
        return Pose({"hip": {"y": -3.0 * amp * (1 - math.cos(w)) / 2}, "torso": {"r": 0.9 * amp * math.sin(w)},
                     "head": {"r": 0.8 * amp * math.sin(w + 0.4)}, ARM_BONE[arm]: {"r": 1.4 * amp * math.sin(w - 0.6)}})

    def seq(keys):
        def f(t):
            if t <= keys[0][0]:
                return keys[0][1]
            for k0, k1 in zip(keys, keys[1:]):
                (t0, p0), (t1, p1) = k0[:2], k1[:2]
                if t0 <= t <= t1:
                    ease = k1[2] if len(k1) > 2 else ease_in_out      # 第三项：进入该关键帧的缓动
                    return mix(p0, p1, ease((t - t0) / max(t1 - t0, 1e-6)))
            return keys[-1][1]
        return f

    B = lambda **kw: Pose({k: v for k, v in kw.items()})
    IDLE = A("hang")
    DIP = add(A("hang"), B(hip={"y": -10}, torso={"r": 2}, head={"r": -3}))
    PRESENT = add(A("present"), B(torso={"r": -1}, head={"r": -2}))
    EVOKER = add(A("evoker"), B(head={"r": -3}, torso={"r": -1.5}))
    ALL = lambda r: {ARM_BONE[k]: {"r": r} for k in ARMS}       # 所有手臂一起转（保持方向对齐）
    TENSE = add(EVOKER, B(head={"r": 1}, **ALL(-1.5)))
    KICK = add(EVOKER, B(head={"r": -14}, torso={"r": -4}, hip={"x": -10, "y": -6}, **ALL(7)))
    S_READY = add(A("s_ready"), B(torso={"r": -3}, head={"r": -2}, hip={"y": -10}))
    ANTIC = add(A("s_ready"), B(torso={"r": 8}, head={"r": 4}, hip={"x": -14, "y": -20}, **ALL(18)))
    WINDUP = add(A("s_up"), B(torso={"r": 10}, head={"r": 3}, hip={"x": -10, "y": -18}, **ALL(8)))
    SLASH = add(A("s_down"), B(torso={"r": -12}, head={"r": -5}, hip={"x": 40, "y": -30}, **ALL(-6)))
    HURT = add(IDLE, B(torso={"r": 13}, head={"r": 16}, hip={"x": -20, "y": -8}, **ALL(-16)))

    bake(rig, images, "idle_loop", 2.6, lambda t: add(IDLE, breathe(t, 2.6)), [(0, "hang")], loop=True)
    bake(rig, images, "relaxed_loop", 3.4, lambda t: add(IDLE, breathe(t, 3.4, 0.7)), [(0, "hang")], loop=True)
    SLUMP = add(IDLE, B(torso={"r": -6}, head={"r": -12}, hip={"y": -18}))
    bake(rig, images, "low_health_loop", 2.0, lambda t: add(SLUMP, breathe(t, 2.0, 1.6)), [(0, "hang")], loop=True)
    bake(rig, images, "aim_loop", 1.8, lambda t: add(EVOKER, breathe(t, 1.8, 0.6, "evoker")), [(0, "evoker")], loop=True)
    bake(rig, images, "ready_sword_loop", 1.8, lambda t: add(S_READY, breathe(t, 1.8, 0.8, "s_ready")), [(0, "s_ready")], loop=True)

    # 预备出牌：抬起 / 放下
    bake(rig, images, "aim_in", 0.42, seq([(0, IDLE), (0.07, DIP), (0.18, PRESENT), (0.32, EVOKER), (0.42, EVOKER)]),
         [(0, "hang"), (0.05, "present", 0.1), (0.19, "evoker", 0.1)], speed=1.5)
    bake(rig, images, "sword_in", 0.3, seq([(0, IDLE), (0.05, DIP), (0.2, S_READY), (0.3, S_READY)]),
         [(0, "hang"), (0.06, "s_ready")], speed=1.8)
    LOWER = add(A("present"), B(head={"r": -1}, **ALL(-30)))
    bake(rig, images, "aim_out", 0.5, seq([(0, EVOKER), (0.08, add(EVOKER, B(**ALL(-4)))), (0.24, LOWER), (0.44, IDLE), (0.5, IDLE)]),
         [(0, "evoker"), (0.14, "present", 0.1), (0.32, "hang", 0.1)], speed=1.3, events=[(0.3, "evoker_sheath")])
    bake(rig, images, "sword_out", 0.5, seq([(0, S_READY), (0.08, add(S_READY, B(**ALL(6)))), (0.42, IDLE), (0.5, IDLE)]),
         [(0, "s_ready"), (0.3, "hang")], speed=1.3, events=[(0.2, "weapon_sheath")])

    # 攻击：游戏在触发 Attack 后 0.15s（快速模式 0.075s）结算伤害、播命中特效 —— 刀必须在这之前落下，否则"不跟手"。
    # 所以蓄力极短（拖牌时的 ready_sword_loop 已经是蓄力），斩下用加速缓动 + 高速弹簧，接触帧约 0.07~0.09s；
    # 之后余势停顿、慢慢收剑（快出慢收）。拖影由游戏端 weapon_slash 事件生成。
    CUT = lambda u: u * u * u                                     # 加速砍下，接触瞬间速度最大
    atk = [(0, IDLE), (0.035, ANTIC), (0.06, WINDUP), (0.09, SLASH, CUT), (0.4, SLASH), (0.85, IDLE)]
    bake(rig, images, "attack", 0.95, seq(atk), [(0, "hang"), (0.005, "s_ready", 0.025), (0.035, "s_up", 0.025), (0.07, "s_down", 0.025), (0.68, "hang", 0.1)],
         speed=3.0, fade=0.03, events=[(0.0, "weapon_draw"), (0.045, "weapon_windup"), (0.075, "weapon_slash"), (0.66, "weapon_sheath")])
    atk_r = [(0, S_READY), (0.03, WINDUP), (0.07, SLASH, CUT), (0.38, SLASH), (0.82, IDLE)]
    bake(rig, images, "attack_from_ready", 0.95, seq(atk_r), [(0, "s_ready"), (0.005, "s_up", 0.025), (0.05, "s_down", 0.025), (0.66, "hang", 0.1)],
         speed=3.0, fade=0.03, events=[(0.02, "weapon_windup"), (0.055, "weapon_slash"), (0.64, "weapon_sheath")])

    # 施法：亮枪 → 抵住太阳穴 → 停顿 → 扣扳机后坐 → 放下
    cast = [(0, IDLE), (0.08, PRESENT), (0.12, PRESENT), (0.22, EVOKER), (0.26, TENSE), (0.272, KICK), (0.35, KICK), (0.55, EVOKER), (0.66, LOWER), (0.92, IDLE)]
    cast_states = [(0, "hang"), (0.02, "present", 0.1), (0.12, "evoker", 0.11), (0.56, "present", 0.1), (0.8, "hang", 0.1)]
    bake(rig, images, "cast", 0.95, seq(cast), cast_states, speed=2.0,
         events=[(0.0, "evoker_draw"), (0.27, "evoker_fire"), (0.82, "evoker_sheath")])
    sov = [(0, IDLE), (0.06, PRESENT), (0.08, PRESENT), (0.16, EVOKER), (0.21, TENSE), (0.225, KICK), (0.3, KICK), (0.48, EVOKER), (0.58, LOWER), (0.83, IDLE)]
    bake(rig, images, "attack_sovereign", 0.85, seq(sov), [(0, "hang"), (0.0, "present", 0.08), (0.08, "evoker", 0.12), (0.5, "present", 0.1), (0.72, "hang", 0.1)],
         speed=2.2, events=[(0.0, "evoker_draw"), (0.22, "evoker_fire"), (0.74, "evoker_sheath")])
    from_aim = [(0, EVOKER), (0.05, TENSE), (0.062, KICK), (0.14, KICK), (0.3, EVOKER), (0.42, LOWER), (0.7, IDLE)]
    for nm in ("cast_from_aim", "sovereign_from_aim"):
        bake(rig, images, nm, 0.72, seq(from_aim), [(0, "evoker"), (0.32, "present", 0.1), (0.56, "hang", 0.1)], speed=2.0,
             events=[(0.06, "evoker_fire"), (0.56, "evoker_sheath")])

    bake(rig, images, "hurt", 0.6, seq([(0, IDLE), (0.06, HURT), (0.14, HURT), (0.3, IDLE)]), [(0, "hang")], speed=1.3)
    DOWN = add(IDLE, B(torso={"r": -30}, head={"r": -34}, hip={"y": -62, "x": 8}, **ALL(6)))
    bake(rig, images, "die", 1.5, seq([(0, IDLE), (0.08, HURT), (0.2, HURT), (0.9, DOWN), (1.5, DOWN)]), [(0, "hang")],
         events=[(0.1, "death_particles_start"), (1.1, "death_particles_end"), (1.2, "explode_dead"), (1.35, "explode_end")])


# ───────────── 校验 ─────────────
def validate(data, images, master):
    rep, ok = [], True

    def mark(passed, msg):
        nonlocal ok
        ok &= passed
        rep.append(f"[{'OK' if passed else 'FAIL'}] {msg}")

    # 1. 拼合：身体零件（不含手臂、影子）对母版
    saved = [(s, s.get("attachment")) for s in data["slots"]]
    for s in data["slots"]:
        if s["name"] == "shadow" or s["name"].startswith("rarm_"):
            s.pop("attachment", None)
    W_, H_ = 1024, 1536
    r = np.asarray(Skeleton(data, images).render(canvas=(W_, H_), origin=ORIGIN)).astype(float)
    for s, a in saved:
        if a:
            s["attachment"] = a
    m = np.asarray(master).astype(float)
    body = m[..., 3] > 128
    diff = np.abs(r[..., :3] - m[..., :3]).mean(-1)[body].mean()
    miss = ((r[..., 3] < 64) & body).mean()
    mark(diff < 5 and miss < 0.001, f"拼合校验：平均色差 {diff:.2f}，缺失 {miss:.5f}")

    # 1b. 锚点必须落在手臂画上（防止量错坐标：v4.0 的枪口点落在空白处，枪被对到了脖子）
    for k, cfg in ARMS.items():
        src = np.asarray(key_image(ROOT / "art/v4" / (cfg["file"] + ".png")))[..., 3]
        for a, (x, y) in cfg["anchors"].items():
            win = src[max(0, y - 8):y + 9, max(0, x - 8):x + 9]
            mark((win > 128).any(), f"锚点在画上 {k}/{a} ({x},{y})")

    # 2. 平移只允许 hip（身体）和特效锚点（root 子骨骼）
    movers = {b for a in data["animations"].values() for b, k in a["bones"].items() if "translate" in k}
    mark(movers <= {"hip"} | set(ANCHORS), f"平移骨骼：{sorted(movers)}")

    sk = Skeleton(data, images)
    ank = {}
    for side in "lr":
        kx, ky = to_spine(J[f"knee_{side}"])
        ax, ay = to_spine(J[f"ankle_{side}"])
        ank[side] = (ax - kx, ay - ky)
    base = {s: sk.point(f"shin_{s}", ank[s]) for s in "lr"}
    for name, anim in data["animations"].items():
        times = sorted({k["time"] for bk in anim["bones"].values() for tl in bk.values() for k in tl})
        drift = max(math.dist(sk.point(f"shin_{s}", ank[s], name, t), base[s]) for t in times for s in "lr") / K
        mark(drift <= 2.0, f"脚底漂移 {name}: {drift:.2f}px")
        # 3. 锚点不瞬移：相邻帧剑尖 / 枪口位移上限（攻击挥砍最快，放宽到 140）
        # 3. 锚点不瞬移：旋转不改变"锚点到肩"的距离；距离的单帧跳变才是换图造成的跳跃
        sh_local = (0.0, 0.0)
        # 每帧可接受的径向变化（像素）：换图淡化 ≥0.06s 时，伸展程度不同的两张手臂之间的过渡读作"手臂收拢 / 伸出"
        limits = {"hand": 80, "muzzle": 90, "sword_tip": 160}
        if name.startswith("attack") and name != "attack_sovereign":
            limits["sword_tip"] = 300      # 挥砍是 1 帧的"拖影帧"：举剑 → 斩下两张图之间剑尖本来就要一下扫过去
        worst = {}
        for a, lim in limits.items():
            tl = anim["bones"].get(a, {}).get("translate", [])
            sup = SUPPORT.get(name, {}).get(a, [True] * len(tl))
            radii = []
            for k in tl:
                sh = sk.point(ARM_BONE["hang"], sh_local, name, k["time"])
                radii.append(math.dist((k["x"], k["y"]), sh))
            w = 0.0
            for i in range(len(radii) - 1):
                if sup[i] and sup[i + 1]:
                    w = max(w, abs(radii[i + 1] - radii[i]))
            worst[a] = w
            mark(w <= lim, f"锚点径向连续 {name}/{a}: 单帧最大 {w:.1f}（≤{lim}）")

    for name, anim in data["animations"].items():
        if name.endswith("_loop"):
            bad = sum(any(abs(tl[0].get(f, 0) - tl[-1].get(f, 0)) > 1e-3 for f in ("value", "x", "y"))
                      for bk in anim["bones"].values() for tl in bk.values())
            mark(bad == 0, f"循环首尾一致 {name}")

    def edge(name, which):
        out = {}
        vis = VISIBLE.get(name, (None, None))[0 if which == "first" else 1]
        for b, tl in data["animations"][name]["bones"].items():
            if b in ANCHORS or (b.startswith("rarm_") and b != ARM_BONE.get(vis)):
                continue
            for kind in ("rotate", "translate"):
                if kind in tl:
                    k = tl[kind][0 if which == "first" else -1]
                    out[(b, kind)] = (k.get("value", 0.0), k.get("x", 0.0), k.get("y", 0.0))
        return out

    pairs = [("aim_in", "aim_loop"), ("sword_in", "ready_sword_loop"), ("aim_loop", "aim_out"), ("ready_sword_loop", "sword_out"),
             ("aim_out", "idle_loop"), ("sword_out", "idle_loop"), ("ready_sword_loop", "attack_from_ready"),
             ("aim_loop", "cast_from_aim"), ("attack_from_ready", "idle_loop"), ("cast_from_aim", "idle_loop"),
             ("attack", "idle_loop"), ("cast", "idle_loop"), ("hurt", "idle_loop")]
    for a_n, b_n in pairs:
        ea, eb = edge(a_n, "last"), edge(b_n, "first")
        worst = max([0.0] + [max(abs(x - y) for x, y in zip(ea.get(k, (0, 0, 0)), eb.get(k, (0, 0, 0)))) for k in set(ea) | set(eb)])
        mark(worst <= 6.0, f"衔接 {a_n} → {b_n}: 最大差 {worst:.2f}")
    return ok, rep


# ───────────── 导出 / 打包目录 / 预览 ─────────────
def export(data, images):
    out = OUT / "export"
    out.mkdir(parents=True, exist_ok=True)
    sheet, atlas = pack_atlas(images)
    sheet.save(out / "regent.png")
    (out / "regent.atlas").write_text(atlas)
    (out / "regent.spine-json").write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")))
    import build_rig
    build_rig.stage_pck(out)
    return out


def previews(data, images):
    out = ROOT / "art/rig_preview"
    out.mkdir(parents=True, exist_ok=True)
    sk = Skeleton(data, images)
    for name, anim in data["animations"].items():
        times = sorted({k["time"] for bk in anim["bones"].values() for tl in bk.values() for k in tl})
        frames = []
        for t in times:
            im = sk.render(name, t, canvas=(1100, 1600), origin=(550, 1540), bg=(36, 40, 60, 255)).convert("RGB")
            frames.append(im.resize((330, 480), Image.LANCZOS).convert("P", palette=Image.ADAPTIVE, colors=200))
        frames[0].save(out / f"{name}.gif", save_all=True, append_images=frames[1:], duration=33, loop=0, optimize=True)
    return out


def main():
    data, images, master = build_setup()
    rig = Rig(data)
    make_animations(rig, images)
    ok, rep = validate(data, images, master)
    print("\n".join(rep))
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / "VALIDATION.txt").write_text("\n".join(rep) + "\n")
    if not ok:
        print("校验未通过，不导出。")
        sys.exit(1)
    print("导出：", export(data, images))
    if "--no-preview" not in sys.argv:
        print("预览：", previews(data, images))


if __name__ == "__main__":
    main()
