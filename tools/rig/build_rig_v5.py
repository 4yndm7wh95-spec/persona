"""结城理骨骼 v5：从"提线木偶"到"活的人"。
v4 的问题：躯干 / 头 / 腿各是一整块刚体图，手臂是整张换图 + 整条转动 —— 关节不会弯，身体不会联动，没有任何次级运动。
v5：
- 身体是一张加权网格（mesh），骨骼链 hip → spine → torso → head 可以弯曲；膝盖处网格平滑过渡（腿部仍用 IK 钉住脚）。
- 次级运动骨骼：刘海 / 后发 / 侧发、领结、近侧衣摆 —— 由身体的速度驱动弹簧（惯性滞后 + 回弹），待机时再叠加微风飘动。
- 眨眼：头部骨骼上的闭眼贴片（程序生成：取周围肤色填眼 + 睫毛线）。
- 手臂：6 张手绘仍做插槽交叉淡化，但每张都是网格；垂手和三张持剑手臂有 肩 → 肘 → 腕 骨骼链，可以弯肘、甩腕。
- 动画按"全身联动"重做：攻击由髋部发力 → 脊柱 → 胸 → 手臂 → 剑依次带动（每一节比上一节晚一点），头发衣摆滞后甩出。
接触帧时序保持 v0.5 的设定（攻击 ≈0.09s / 预备接招 ≈0.07s，早于游戏 0.15s 的伤害结算）。
用法：python3 tools/rig/build_rig_v5.py [--no-preview]"""
import json
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, str(Path(__file__).parent))
import build_rig_v4 as v4  # noqa: E402
from build_rig import Pose, Spring, add, mix, ease_in_out, key_image, shadow_image, pack_atlas  # noqa: E402
from spine_eval import Skeleton  # noqa: E402

ROOT = v4.ROOT
J, ARMS, ANCHORS, ORIGIN, K = v4.J, v4.ARMS, v4.ANCHORS, v4.ORIGIN, v4.K

# 阶段 7 新手臂（上撩斜斩）：剑拉到身后下方蓄力 / 手臂伸直剑指敌人定格。
# scale 按袖子粗细换算（与 rarm_sword_ready 的 0.55 对齐：袖宽 124 → 115 / 86 像素）。
ARMS["s_back"] = {"file": "../v7/rarm_sword_back_b", "shoulder": (580, 300), "scale": 0.61, "rot": 0.0,
                  "anchors": {"hand": (372, 880), "sword_tip": (24, 1302)}}
ARMS["s_ext"] = {"file": "../v7/rarm_sword_extend_a", "shoulder": (88, 429), "scale": 0.79, "rot": 0.0,
                 "anchors": {"hand": (512, 461), "sword_tip": (1008, 341)}}
for _k in ("s_back", "s_ext"):
    v4.ARM_BONE[_k] = "rarm_" + _k
    v4.THETA[_k] = v4._hand_dir(_k)
to_spine, map_point = v4.to_spine, v4.map_point
OUT = ROOT / "art/rig"
FPS = 30
STEP = 18                                    # 网格步长（母版像素）

# ───────────── 新增关节（母版像素）─────────────
J5 = dict(J)
J5.update({
    "spine": (525, 470), "chest_pivot": (528, 380),
    "hair_fringe": (562, 76), "hair_back": (470, 70), "hair_side": (615, 62),
    "tie": (545, 255), "coat": (445, 600),
    "eye": (599, 129),
})

# 手臂肩 → 肘 → 腕（各自手绘图的像素）；present / evoker 是单骨骼（刚体）
CHAINS = {
    "hang": [(390, 370), (360, 740), (336, 1010)],
    "s_ready": [(330, 305), (400, 570), (468, 800)],
    "s_up": [(385, 745), (285, 535), (360, 320)],
    "s_down": [(330, 310), (400, 570), (478, 815)],
    "s_back": [(580, 300), (514, 621), (390, 768)],
    "s_ext": [(88, 429), (352, 437), (445, 447)],
}
FORE = {k: f"rarm_{k}_fore" for k in CHAINS}
HAND = {k: f"rarm_{k}_hand" for k in CHAINS}
ARM_BONE = v4.ARM_BONE


def deepest(k):
    return HAND.get(k, ARM_BONE[k])


# ───────────── 网格 ─────────────
def mesh_from(img, box_xy, weight_fn, bone_world, bone_index):
    """img：块图（RGBA），box_xy：它左上角在母版中的像素位置；weight_fn(x, y) -> [(骨骼, 权重)]（母版像素）。"""
    a = np.asarray(img.getchannel("A")) > 8
    h, w = a.shape
    xs = list(range(0, w, STEP)) + ([w] if (w - 1) % STEP else [])
    ys = list(range(0, h, STEP)) + ([h] if (h - 1) % STEP else [])
    index, verts_px, tris = {}, [], []

    def vid(i, j):
        if (i, j) not in index:
            index[(i, j)] = len(verts_px)
            verts_px.append((xs[i], ys[j]))
        return index[(i, j)]

    for j in range(len(ys) - 1):
        for i in range(len(xs) - 1):
            if a[ys[j]:ys[j + 1], xs[i]:xs[i + 1]].any():
                v00, v10, v01, v11 = vid(i, j), vid(i + 1, j), vid(i, j + 1), vid(i + 1, j + 1)
                tris += [v00, v10, v01, v10, v11, v01]
    uvs, verts = [], []
    for (x, y) in verts_px:
        uvs += [round(x / w, 5), round(y / h, 5)]
        px, py = x + box_xy[0], y + box_xy[1]
        ws = weight_fn(px, py)
        sx, sy = to_spine((px, py))
        verts.append(len(ws))
        for b, v in ws:
            bx, by = bone_world[b]
            verts += [bone_index[b], round(sx - bx, 2), round(sy - by, 2), round(v, 4)]
    return {"type": "mesh", "uvs": uvs, "triangles": tris, "vertices": verts, "hull": 0,
            "width": round(w * K), "height": round(h * K)}


def soft(mask, feather):
    m = Image.fromarray((mask * 255).astype(np.uint8))
    if feather > 0:
        m = m.filter(ImageFilter.GaussianBlur(feather / 2))
    return np.asarray(m).astype(np.float32) / 255


def ramp(v, a, b):
    """v 从 a 到 b 由 0 线性升到 1（a > b 时反向）。"""
    return np.clip((v - a) / (b - a), 0, 1)


def body_weights(master):
    """身体各骨骼的权重图（母版大小），按优先级依次分配，剩余给 hip。"""
    a = np.asarray(master)
    h, w = a.shape[:2]
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    gap = np.vectorize(v4.leg_gap)(np.arange(h))[:, None]
    neck = np.vectorize(v4.neck_cut)(np.arange(w))[None, :]
    head = (ys < neck) | ((ys < neck + 24) & (xs > 495) & (xs < 575))
    flap = ((xs < 485) & (ys < 728)) | ((xs > 630) & (ys < 700))
    legs = (ys >= 600) & ~flap
    left, right = legs & (xs < gap), legs & (xs >= gap)

    def poly(points, feather):
        m = Image.new("L", (w, h), 0)
        ImageDraw.Draw(m).polygon(points, fill=255)
        return soft(np.asarray(m) > 0, feather)

    W = {}
    # 头发次级（在头部范围内）
    W["hair_fringe"] = poly([(520, 95), (615, 88), (622, 172), (585, 170), (540, 150)], 16) * ramp(ys, 92, 120)
    W["hair_back"] = poly([(420, 60), (478, 60), (480, 150), (440, 160), (415, 120)], 16) * ramp(xs, 478, 450)
    W["hair_side"] = poly([(606, 55), (640, 60), (640, 175), (612, 170)], 12) * ramp(ys, 62, 95)
    W["head"] = soft(head, 14)
    W["tie"] = poly([(522, 262), (572, 258), (585, 345), (520, 345)], 10) * ramp(ys, 262, 290)
    # 腿：大腿 / 小腿在膝盖处 50px 过渡
    knee_l, knee_r = J["knee_l"][1], J["knee_r"][1]
    W["shin_l"] = soft(left, 6) * ramp(ys, knee_l - 25, knee_l + 25)
    W["shin_r"] = soft(right, 6) * ramp(ys, knee_r - 25, knee_r + 25)
    W["thigh_l"] = soft(left, 6) * ramp(ys, 590, 650)
    W["thigh_r"] = soft(right, 6) * ramp(ys, 590, 650)
    W["coat"] = soft(flap & (xs < 485), 8) * ramp(ys, 610, 690)
    # 躯干：胸 → 脊柱 → 髋 竖向过渡
    W["torso"] = ramp(ys, 470, 380)
    W["spine"] = ramp(ys, 380, 470) * ramp(ys, 600, 520)
    order = ["hair_fringe", "hair_back", "hair_side", "tie", "head", "shin_l", "shin_r", "thigh_l", "thigh_r", "coat", "torso", "spine"]
    return W, order


def arm_weights(k, cfg):
    """手臂块的权重：沿 肩 → 肘 → 腕 的弧长参数在关节处 40px 过渡。返回 fn(x, y)。"""
    if k not in CHAINS:
        return lambda x, y: [(ARM_BONE[k], 1.0)]
    S, E, Wr = [np.array(map_point(cfg, p)) for p in CHAINS[k]]
    tip = np.array(map_point(cfg, cfg["anchors"].get("sword_tip", cfg["anchors"]["hand"])))
    if np.linalg.norm(tip - Wr) < 30:
        tip = Wr + (Wr - E) * 0.6
    segs = [(S, E), (E, Wr), (Wr, tip)]
    lens = [np.linalg.norm(b - a) for a, b in segs]
    LE, LW = lens[0], lens[0] + lens[1]

    def fn(x, y):
        p = np.array([x, y], dtype=float)
        best, s_best = 1e9, 0.0
        acc = 0.0
        for (a, b), L in zip(segs, lens):
            d = b - a
            u = float(np.clip(np.dot(p - a, d) / max(L * L, 1e-6), 0, 1))
            dist = np.linalg.norm(a + d * u - p)
            if dist < best:
                best, s_best = dist, acc + u * L
            acc += L
        f = 22.0
        hand = float(np.clip((s_best - (LW - f)) / (2 * f), 0, 1))
        fore = float(np.clip((s_best - (LE - f)) / (2 * f), 0, 1)) * (1 - hand)
        upper = 1 - hand - fore
        return [(b, v) for b, v in ((ARM_BONE[k], upper), (FORE[k], fore), (HAND[k], hand)) if v > 1e-3]
    return fn


def eyelid_patch(master):
    """闭眼贴片：在眼睛的椭圆范围内、且不是头发的像素上，填上脸颊的肤色，再画一道下弯的睫毛线。
    （只覆盖露出来的那只眼；头发像素保持透明，所以刘海仍然盖在上面。）"""
    a = np.asarray(master).astype(np.float32)
    ex, ey = J5["eye"]
    cheek = a[ey + 14:ey + 26, ex - 8:ex + 10, :3].reshape(-1, 3)
    skin = cheek[(cheek[:, 0] > 200) & (cheek[:, 1] > 150)].mean(0)
    W_, H_ = 28, 24
    x0, y0 = int(ex - W_ / 2), int(ey - H_ / 2)
    patch = a[y0:y0 + H_, x0:x0 + W_]
    hair = (patch[..., 2] > patch[..., 0] + 25) & (patch[..., 0] < 110)            # 深蓝头发
    ell = Image.new("L", (W_, H_), 0)
    ImageDraw.Draw(ell).ellipse((2, 3, W_ - 2, H_ - 3), fill=255)
    ell = np.asarray(ell.filter(ImageFilter.GaussianBlur(1.4))).astype(np.float32) / 255
    alpha = ell * (~hair) * (patch[..., 3] > 128)
    rgba = np.zeros((H_, W_, 4), np.uint8)
    rgba[..., :3] = skin.astype(np.uint8)
    rgba[..., 3] = (alpha * 255).astype(np.uint8)
    im = Image.fromarray(rgba, "RGBA")
    lash = Image.new("RGBA", (W_, H_), (0, 0, 0, 0))
    ImageDraw.Draw(lash).arc((4, 2, W_ - 5, H_ - 4), start=20, end=160, fill=(45, 32, 40, 255), width=2)
    lash.putalpha(Image.fromarray(np.minimum(np.asarray(lash.getchannel("A")), (~hair * 255).astype(np.uint8))))
    im.alpha_composite(lash)
    return im, (x0, y0)


# ───────────── 骨骼 / 插槽 / 网格 ─────────────
BONES = [
    ("root", None, ORIGIN),
    ("hip", "root", J["hip"]),
    ("spine", "hip", J5["spine"]),
    ("torso", "spine", J5["chest_pivot"]),
    ("chest_particle_attach", "torso", J["chest"]),
    ("head", "torso", J["neck"]),
    ("hair_fringe", "head", J5["hair_fringe"]),
    ("hair_back", "head", J5["hair_back"]),
    ("hair_side", "head", J5["hair_side"]),
    ("tie", "torso", J5["tie"]),
    ("arm_particle_attach", "torso", (380, 520)),
    ("coat", "hip", J5["coat"]),
    ("thigh_l", "hip", J["hip_l"]), ("shin_l", "thigh_l", J["knee_l"]), ("leg_particle_attach_l", "shin_l", (400, 1250)),
    ("thigh_r", "hip", J["hip_r"]), ("shin_r", "thigh_r", J["knee_r"]), ("leg_particle_attach", "shin_r", (612, 1250)),
]


def build_setup():
    master = Image.open(v4.BODY).convert("RGBA")
    bones = list(BONES)
    for k, cfg in ARMS.items():
        bones.append((ARM_BONE[k], "torso", J["shoulder"]))
        if k in CHAINS:
            S, E, Wr = [map_point(cfg, p) for p in CHAINS[k]]
            bones.append((FORE[k], ARM_BONE[k], E))
            bones.append((HAND[k], FORE[k], Wr))
    for a in ANCHORS:
        bones.append((a, "root", ORIGIN))
    world = {n: to_spine(p) for n, _, p in bones}
    bone_index = {n: i for i, (n, _, _) in enumerate(bones)}

    out_bones = []
    for n, parent, _ in bones:
        b = {"name": n}
        if parent:
            b.update(parent=parent, x=round(world[n][0] - world[parent][0], 3), y=round(world[n][1] - world[parent][1], 3))
        out_bones.append(b)

    images, skin, slots = {}, {}, []
    # 影子
    images["shadow"] = shadow_image()
    sx, sy = to_spine((ORIGIN[0] + 10, ORIGIN[1] - 10))
    skin["shadow"] = {"shadow": {"x": round(sx, 2), "y": round(sy, 2), "width": images["shadow"].width, "height": images["shadow"].height}}
    slots.append({"name": "shadow", "bone": "root", "attachment": "shadow"})

    # 身体网格
    W, order = body_weights(master)
    h, w = W["head"].shape

    def body_fn(x, y):
        xi, yi = min(max(int(x), 0), w - 1), min(max(int(y), 0), h - 1)
        remaining, ws = 1.0, []
        for b in order:
            v = float(W[b][yi, xi]) * remaining
            if v > 1e-3:
                ws.append((b, v))
                remaining -= v
        ws.append(("hip", remaining))
        ws = sorted(ws, key=lambda t: -t[1])[:4]
        s = sum(v for _, v in ws)
        return [(n, v / s) for n, v in ws if v / s > 1e-3]

    bbox = master.getchannel("A").point(lambda v: 255 if v > 8 else 0).getbbox()
    body_img = master.crop(bbox)
    images["body"] = body_img
    skin["body"] = {"body": mesh_from(body_img, bbox[:2], body_fn, world, bone_index)}
    slots.append({"name": "body", "bone": "root", "attachment": "body"})

    # 眨眼贴片（头部骨骼上的普通区域附件）
    lid, (lx, ly) = eyelid_patch(master)
    images["eyelid"] = lid
    cx, cy = to_spine((lx + lid.width / 2, ly + lid.height / 2))
    hx, hy = world["head"]
    skin["eyelid"] = {"eyelid": {"x": round(cx - hx, 2), "y": round(cy - hy, 2), "width": lid.width, "height": lid.height}}
    slots.append({"name": "eyelid", "bone": "head"})

    # 手臂网格（每张手绘一块，放在身体前面）
    for k, cfg in ARMS.items():
        im, center = v4.load_arm(cfg)
        name = "rarm_" + k
        images[name] = im
        box = (center[0] - im.width / 2, center[1] - im.height / 2)
        skin[name] = {name: mesh_from(im, box, arm_weights(k, cfg), world, bone_index)}
        slots.append({"name": name, "bone": "root", **({"attachment": name} if k == "hang" else {})})

    data = {
        "skeleton": {"spine": "4.2.43", "images": "./", "audio": "", "x": -320, "y": -20, "width": 820, "height": 1520},
        "bones": out_bones,
        "slots": slots,
        "skins": [{"name": "default", "attachments": skin}],
        "events": {},
        "animations": {},
    }
    return data, images, master, world


# ───────────── 动力学 ─────────────
# (频率 Hz, 阻尼, 响应)：主干较硬，次级部件软、阻尼小 —— 晃得更久
DYN = {"hip": (3.4, 0.85, 0.0), "spine": (3.6, 0.75, 0.0), "torso": (3.0, 0.62, 0.0), "head": (2.4, 0.5, 0.0),
       "hair_fringe": (2.2, 0.32, 0.0), "hair_back": (2.0, 0.3, 0.0), "hair_side": (2.4, 0.32, 0.0),
       "tie": (2.0, 0.28, 0.0), "coat": (2.2, 0.35, 0.0)}
for _k in ARMS:
    DYN[ARM_BONE[_k]] = (4.6, 0.55, 0.0)
for _k in CHAINS:
    DYN[FORE[_k]] = (4.2, 0.5, 0.0)
    DYN[HAND[_k]] = (3.8, 0.42, 0.0)
DYN[ARM_BONE["hang"]] = (3.2, 0.42, 0.0)
DYN["foot_l"] = DYN["foot_r"] = (5.0, 0.8, 0.0)          # 踏步：脚踝目标位移（只喂给 IK，不导出）
SECONDARY = ("hair_fringe", "hair_back", "hair_side", "tie", "coat")


def secondary_targets(tp, tprev, dt):
    """次级部件的目标角度 = 惯性（身体速度的反向）+ 重力方向修正。返回要叠加到 tp 上的 {骨骼: r}。"""
    def vel(b, c):
        return (tp.get(b, {}).get(c, 0.0) - tprev.get(b, {}).get(c, 0.0)) / dt
    vx = vel("hip", "x")
    vy = vel("hip", "y")
    vr_torso = vel("torso", "r") + vel("spine", "r")
    vr_head = vel("head", "r")
    whole = vr_torso + vr_head
    # 身体逆时针转（后仰）→ 头发相对向顺时针滞后；向右冲（vx>0）→ 头发衣摆向左后甩
    lag = -0.09 * whole + 0.035 * vx
    tilt = tp.get("torso", {}).get("r", 0.0) + tp.get("spine", {}).get("r", 0.0) + tp.get("head", {}).get("r", 0.0)
    out = {
        "hair_fringe": 0.9 * lag - 0.25 * tilt,
        "hair_back": 1.1 * lag - 0.3 * tilt,
        "hair_side": 0.8 * lag - 0.2 * tilt,
        "tie": 0.7 * lag - 0.15 * (tp.get("torso", {}).get("r", 0.0) + tp.get("spine", {}).get("r", 0.0)) + 0.01 * vy,
        "coat": 0.5 * lag - 0.2 * tp.get("spine", {}).get("r", 0.0) + 0.012 * vy,
    }
    return {b: max(-28.0, min(28.0, v)) for b, v in out.items()}


def breeze(t, period, amp=1.0):
    """待机微动：头发、领结、衣摆的随机感飘动（几个不可约周期的正弦叠加，但都整除 period → 无缝循环）。"""
    w = 2 * math.pi * t / period
    return Pose({
        "hair_fringe": {"r": amp * (1.4 * math.sin(w + 0.3) + 0.7 * math.sin(3 * w + 1.1))},
        "hair_back": {"r": amp * (1.8 * math.sin(w + 1.7) + 0.8 * math.sin(2 * w + 0.4))},
        "hair_side": {"r": amp * (1.2 * math.sin(2 * w + 2.2) + 0.6 * math.sin(5 * w))},
        "tie": {"r": amp * (1.5 * math.sin(w + 0.9) + 0.6 * math.sin(4 * w + 2.0))},
        "coat": {"r": amp * (0.9 * math.sin(w + 2.5))},
    })


def bake(rig, images, name, duration, target, states, loop=False, events=None, speed=1.0, fade=0.05, blinks=()):
    """弹簧积分 240Hz → 30fps；次级部件目标由身体速度推出；腿部 IK；插槽淡化；锚点烘焙；眨眼。"""
    sub, T = 240, 1.0 / 240
    p0 = target(0.0)
    chans = {}
    for b in set(DYN) | set(p0):
        f, z, r = DYN.get(b, (4.0, 0.7, 0.0))
        for c in ("r", "x", "y", "sy"):               # sy 是相对 1 的偏移量（0 = 不缩放），这样姿势混合时缺省值一致
            init = p0.get(b, {}).get(c, 0.0)
            chans[(b, c)] = Spring(init, f * (speed if b not in SECONDARY else min(speed, 1.6)), z, r)
    warm = 2 if loop else 0
    steps = int(round(duration * sub))
    frames = []
    prev_tp = p0
    for i in range(steps * (warm + 1) + 1):
        t = (i * T) % duration if loop else min(i * T, duration)
        tp = target(t)
        sec = secondary_targets(tp, prev_tp if i else tp, T)
        prev_tp = tp
        cur = Pose()
        for (b, c), sp in chans.items():
            goal = tp.get(b, {}).get(c, 0.0)
            if c == "r" and b in sec:
                goal += sec[b]
            v = sp.step(T, goal)
            if abs(v) > 1e-6:
                cur.setdefault(b, {})[c] = v
        if i >= steps * warm and (i - steps * warm) % (sub // FPS) == 0:
            frames.append((round((i - steps * warm) * T, 4), cur))
    if loop:
        frames[-1] = (frames[-1][0], frames[0][1])
    bones = {}
    solved = [(t, rig.leg_ik(rig.leg_ik(Pose({b: dict(v) for b, v in p.items()}), "l"), "r")) for t, p in frames]
    names = {n for n in set().union(*[set(p) for _, p in solved]) if not n.startswith("foot_")}
    for b in sorted(names):
        rot = [{"time": t, "value": round(p.get(b, {}).get("r", 0.0), 3)} for t, p in solved]
        tr = [{"time": t, "x": round(p.get(b, {}).get("x", 0.0), 3), "y": round(p.get(b, {}).get("y", 0.0), 3)} for t, p in solved]
        sc = [{"time": t, "x": 1.0, "y": round(1.0 + p.get(b, {}).get("sy", 0.0), 4)} for t, p in solved]
        e = {}
        if any(abs(k["value"]) > 1e-4 for k in rot):
            e["rotate"] = rot
        if b == "hip" and any(abs(k["x"]) > 1e-4 or abs(k["y"]) > 1e-4 for k in tr):
            e["translate"] = tr
        if any(abs(k["y"] - 1) > 1e-4 for k in sc):
            e["scale"] = sc
        if e:
            bones[b] = e
    slots = v4.slot_timelines(states, fade)
    if blinks:
        rgba = [{"time": 0.0, "color": "ffffff00"}]
        att = [{"time": 0.0, "name": "eyelid"}]
        for bt in blinks:                       # 眨眼：闭 0.05s → 停 0.05s → 睁 0.06s
            rgba += [{"time": round(bt, 4), "color": "ffffff00"}, {"time": round(bt + 0.05, 4), "color": "ffffffff"},
                     {"time": round(bt + 0.1, 4), "color": "ffffffff"}, {"time": round(bt + 0.16, 4), "color": "ffffff00"}]
        slots["eyelid"] = {"attachment": att, "rgba": rgba}
    anim = {"bones": bones, "slots": slots}
    if events:
        anim["events"] = [{"time": t, "name": n} for t, n in sorted(events)]
    rig.data["animations"][name] = anim

    # 锚点烘焙：各手臂锚点（相对该手臂最末端骨骼）的世界坐标按透明度加权
    sk = Skeleton(rig.data, images)
    local = {}
    for k in ARMS:
        bx, by = WORLD[deepest(k)]
        local[k] = {a: (to_spine(map_point(ARMS[k], p))[0] - bx, to_spine(map_point(ARMS[k], p))[1] - by)
                    for a, p in ARMS[k]["anchors"].items()}
    keys = {a: [] for a in ANCHORS}
    last = {a: (0.0, 0.0) for a in ANCHORS}
    support = {a: [] for a in ANCHORS}
    for t, _ in frames:
        Wm = sk.world(name, t)
        al = v4.arm_alpha(states, fade, t)
        for a in ANCHORS:
            sx = sy = sw = 0.0
            for k in ARMS:
                if al[k] <= 0 or a not in local[k]:
                    continue
                pt = Wm[deepest(k)] @ np.array([local[k][a][0], local[k][a][1], 1.0])
                sx, sy, sw = sx + pt[0] * al[k], sy + pt[1] * al[k], sw + al[k]
            if sw > 0:
                last[a] = (sx / sw, sy / sw)
            elif a != "hand":
                last[a] = last["hand"]
            support[a].append(sw > 0.999)
            keys[a].append({"time": t, "x": round(last[a][0], 2), "y": round(last[a][1], 2)})
    for a in ANCHORS:
        bones[a] = {"translate": keys[a]}
    v4.SUPPORT[name] = support
    v4.VISIBLE[name] = (v4._st(states[0], fade)[1], v4._st(states[-1], fade)[1])


WORLD = {}


# ───────────── 动画 ─────────────
def make_animations(rig, images):
    rig.data["events"] = {e: {} for e in v4.EVENTS}
    A = v4.arm_pose
    B = lambda **kw: Pose({k: v for k, v in kw.items()})

    def seq(keys):
        def f(t):
            if t <= keys[0][0]:
                return keys[0][1]
            for k0, k1 in zip(keys, keys[1:]):
                (t0, p0), (t1, p1) = k0[:2], k1[:2]
                if t0 <= t <= t1:
                    ease = k1[2] if len(k1) > 2 else ease_in_out
                    return mix(p0, p1, ease((t - t0) / max(t1 - t0, 1e-6)))
            return keys[-1][1]
        return f

    out_back = lambda u: 1 + 2.4 * (u - 1) ** 3 + 1.4 * (u - 1) ** 2      # 快速到位并略微过冲
    out_cubic = lambda u: 1 - (1 - u) ** 3
    in_out_sine = lambda u: 0.5 - 0.5 * math.cos(math.pi * u)

    def breathe(t, period, amp=1.0, arm="hang"):
        w = 2 * math.pi * t / period
        p = Pose({"hip": {"y": -3.0 * amp * (1 - math.cos(w)) / 2, "x": 1.6 * amp * math.sin(w / 1 + 0.5)},
                  "spine": {"r": 0.5 * amp * math.sin(w + 0.2)},
                  "torso": {"r": 0.7 * amp * math.sin(w), "sy": 0.008 * amp * math.sin(w - 0.4)},
                  "head": {"r": 0.9 * amp * math.sin(w + 0.6) + 0.5 * amp * math.sin(2 * w + 1.3)},
                  ARM_BONE[arm]: {"r": 1.4 * amp * math.sin(w - 0.6)}})
        if arm in FORE:
            p[FORE[arm]] = {"r": 1.6 * amp * math.sin(w - 1.0)}
            p[HAND[arm]] = {"r": 2.0 * amp * math.sin(w - 1.4)}
        return add(p, breeze(t, period, amp))

    IDLE = add(A("hang"), B(**{FORE["hang"]: {"r": 4}, HAND["hang"]: {"r": 3}}))           # 肘部微屈，不再笔直
    DIP = add(IDLE, B(hip={"y": -12, "x": -4}, spine={"r": 2}, torso={"r": 2}, head={"r": -3}))
    PRESENT = add(A("present"), B(spine={"r": -1}, torso={"r": -1}, head={"r": -2}))
    EVOKER = add(A("evoker"), B(head={"r": -4}, spine={"r": -1}, torso={"r": -1.5}, hip={"x": -3}))
    ALL = lambda r: {ARM_BONE[k]: {"r": r} for k in ARMS}
    TENSE = add(EVOKER, B(head={"r": 1.5}, torso={"r": 0.5}, **ALL(-1.5)))
    KICK = add(EVOKER, B(head={"r": -16}, spine={"r": -2}, torso={"r": -5}, hip={"x": -12, "y": -8}, **ALL(7)))
    S_READY = add(A("s_ready"), B(hip={"y": -14, "x": 4}, spine={"r": -2}, torso={"r": -3}, head={"r": -2},
                                  **{FORE["s_ready"]: {"r": 6}, HAND["s_ready"]: {"r": -4}}))
    # 攻击：髋部先动，脊柱 / 胸 / 手臂依次跟上（弹簧本身给每节一点滞后），剑在接触帧前甩腕加速
    # 上撩斜斩（v0.8.2）：剑拉到右胯后下方蓄力（上身后拧、前脚抬起）→ 前脚踏出，髋部带上身前转，
    # 剑从后下方经过最低点斜向前上划出 → 手臂伸直、剑尖指敌定格（弓步、前压、头盯敌人）→ 甩腕把剑尖甩下 → 撤步收招。
    BACK = add(A("s_back"), B(hip={"x": -16, "y": -28}, spine={"r": 6}, torso={"r": 12}, head={"r": 3},
                              foot_r={"x": 22, "y": 20},
                              **{FORE["s_back"]: {"r": -6}, HAND["s_back"]: {"r": -8}}))
    BACK_DEEP = add(BACK, B(torso={"r": 3}, **ALL(-5), **{HAND["s_back"]: {"r": -6}}))
    EXTEND = add(A("s_ext"), B(hip={"x": 54, "y": -62}, spine={"r": -11}, torso={"r": -15}, head={"r": 10},
                               foot_r={"x": 80, "y": 0},
                               **ALL(6), **{FORE["s_ext"]: {"r": 4}, HAND["s_ext"]: {"r": 6}}))
    HOLD = add(A("s_ext"), B(hip={"x": 58, "y": -66}, spine={"r": -12}, torso={"r": -16}, head={"r": 11},
                             foot_r={"x": 82, "y": 0},
                             **ALL(2), **{FORE["s_ext"]: {"r": 0}, HAND["s_ext"]: {"r": 2}}))
    FLICK = add(A("s_ext"), B(hip={"x": 30, "y": -36}, spine={"r": -6}, torso={"r": -8}, head={"r": 5},
                              foot_r={"x": 40, "y": 14},
                              **ALL(-14), **{FORE["s_ext"]: {"r": -12}, HAND["s_ext"]: {"r": -38}}))
    HURT = add(IDLE, B(spine={"r": 6}, torso={"r": 10}, head={"r": 16}, hip={"x": -20, "y": -8}, **ALL(-16),
                       **{FORE["hang"]: {"r": -14}}))

    bake(rig, images, "idle_loop", 3.2, lambda t: add(IDLE, breathe(t, 3.2)), [(0, "hang")], loop=True, blinks=(1.9,))
    bake(rig, images, "relaxed_loop", 4.0, lambda t: add(IDLE, breathe(t, 4.0, 0.7)), [(0, "hang")], loop=True, blinks=(1.1, 3.3))
    SLUMP = add(IDLE, B(spine={"r": -3}, torso={"r": -5}, head={"r": -12}, hip={"y": -18}))
    bake(rig, images, "low_health_loop", 2.4, lambda t: add(SLUMP, breathe(t, 2.4, 1.6)), [(0, "hang")], loop=True, blinks=(1.5,))
    bake(rig, images, "aim_loop", 2.0, lambda t: add(EVOKER, breathe(t, 2.0, 0.6, "evoker")), [(0, "evoker")], loop=True, blinks=(1.2,))
    bake(rig, images, "ready_sword_loop", 2.0, lambda t: add(S_READY, breathe(t, 2.0, 0.8, "s_ready")), [(0, "s_ready")], loop=True,
         blinks=(1.4,))

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

    CUT = lambda u: u * u * u
    # 接触帧（剑扫过最低点、朝向敌人）约 0.08s；0.10s 到定格，早于游戏 0.15s 的伤害结算
    atk = [(0, IDLE), (0.04, BACK, out_cubic), (0.06, BACK_DEEP), (0.1, EXTEND, CUT), (0.22, HOLD, out_cubic), (0.44, HOLD),
           (0.6, FLICK, in_out_sine), (0.9, IDLE, out_cubic)]
    bake(rig, images, "attack", 0.95, seq(atk), [(0, "hang"), (0.005, "s_back", 0.03), (0.065, "s_ext", 0.03), (0.72, "hang", 0.12)],
         speed=3.0, fade=0.03, events=[(0.0, "weapon_draw"), (0.045, "weapon_windup"), (0.08, "weapon_slash"), (0.7, "weapon_sheath")])
    atk_r = [(0, S_READY), (0.03, BACK, out_cubic), (0.045, BACK_DEEP), (0.085, EXTEND, CUT), (0.2, HOLD, out_cubic), (0.42, HOLD),
             (0.58, FLICK, in_out_sine), (0.88, IDLE, out_cubic)]
    bake(rig, images, "attack_from_ready", 0.95, seq(atk_r), [(0, "s_ready"), (0.005, "s_back", 0.025), (0.05, "s_ext", 0.03), (0.7, "hang", 0.12)],
         speed=3.0, fade=0.03, events=[(0.02, "weapon_windup"), (0.065, "weapon_slash"), (0.68, "weapon_sheath")])
    v4.STEP_ANIMS.update({"attack", "attack_from_ready"})

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

    # 不召唤的施法：抬起前臂、掌心向上托出（游戏端在手上方翻出一张塔罗牌），身体随之前倾、低头看手，再自然放下
    GATHER = add(IDLE, B(hip={"y": -7}, spine={"r": 1}, torso={"r": 2}, head={"r": -2},
                         **{ARM_BONE["hang"]: {"r": -3}, FORE["hang"]: {"r": 8}, HAND["hang"]: {"r": -6}}))
    FLICK = add(IDLE, B(hip={"y": 2, "x": 3}, spine={"r": -1}, torso={"r": -2}, head={"r": -6},
                        **{ARM_BONE["hang"]: {"r": 14}, FORE["hang"]: {"r": 36}, HAND["hang"]: {"r": 30}}))
    FLOAT = add(FLICK, B(head={"r": -4}, **{FORE["hang"]: {"r": 39}, HAND["hang"]: {"r": 24}}))
    LET_GO = add(IDLE, B(hip={"y": -3}, head={"r": -2}, **{ARM_BONE["hang"]: {"r": 5}, FORE["hang"]: {"r": 14}, HAND["hang"]: {"r": 26}}))
    bake(rig, images, "cast_light", 0.9, seq([(0, IDLE), (0.08, GATHER, out_cubic), (0.17, FLICK, out_back), (0.44, FLOAT, in_out_sine),
                                              (0.64, LET_GO, in_out_sine), (0.88, IDLE, out_cubic)]),
         [(0, "hang")], speed=2.2)

    IDLE0 = add(IDLE, breathe(0.0, 3.2))                  # idle_loop 第一帧的姿势：结束在这里，接待机无缝
    bake(rig, images, "hurt", 0.7, seq([(0, IDLE), (0.06, HURT), (0.14, HURT), (0.34, IDLE0)]), [(0, "hang")], speed=1.3)
    DOWN = add(IDLE, B(spine={"r": -10}, torso={"r": -22}, head={"r": -34}, hip={"y": -62, "x": 8}, **ALL(6), **{FORE["hang"]: {"r": 20}}))
    bake(rig, images, "die", 1.5, seq([(0, IDLE), (0.08, HURT), (0.2, HURT), (0.9, DOWN), (1.5, DOWN)]), [(0, "hang")],
         events=[(0.1, "death_particles_start"), (1.1, "death_particles_end"), (1.2, "explode_dead"), (1.35, "explode_end")],
         blinks=(0.85,))


# ───────────── 校验 / 导出 ─────────────
def validate(data, images, master):
    """沿用 v4 的全部校验（拼合、锚点在画上、平移白名单、脚底漂移、锚点径向连续、循环首尾、动画衔接）。
    拼合校验会去掉 rarm_* 和 shadow 插槽；眨眼插槽默认无附件，不影响。v5 甩腕让挥砍那一帧剑尖多扫出一段，上限放宽到 340。"""
    v4.SWORD_SMEAR = 340
    return v4.validate(data, images, master)


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


PREVIEW = ("idle_loop", "attack", "attack_from_ready", "cast_light", "aim_in", "ready_sword_loop")


def previews(data, images, names=PREVIEW, every=2):
    out = ROOT / "art/rig_preview"
    out.mkdir(parents=True, exist_ok=True)
    sk = Skeleton(data, images)
    for name in names:
        anim = data["animations"][name]
        times = sorted({k["time"] for bk in anim["bones"].values() for tl in bk.values() for k in tl})[::every]
        frames = []
        for t in times:
            im = sk.render(name, t, canvas=(1100, 1600), origin=(550, 1540), bg=(36, 40, 60, 255)).convert("RGB")
            frames.append(im.resize((330, 480), Image.LANCZOS).convert("P", palette=Image.ADAPTIVE, colors=200))
        frames[0].save(out / f"v5_{name}.gif", save_all=True, append_images=frames[1:], duration=33 * every, loop=0, optimize=True)
    return out


def main():
    data, images, master, world = build_setup()
    WORLD.update(world)
    rig = v4.Rig(data)
    make_animations(rig, images)
    ok, rep = validate(data, images, master)
    print("\n".join(r for r in rep if not r.startswith("[OK]")) or "all OK", f"({len(rep)} checks)")
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
