"""结城理骨骼 v6：真正的纸偶（paper doll）。见 docs/PUPPET_PLAN.md。
素材：art/v9（9A 基准图 base_a、9B 分离零件、9C 换手图），先由 v6_register.py 抠图 + 自动配准（修正 imagegen 的缩放 / 位移）。
- 零件：躯干网格（胯 / 腰 / 胸 / 领结 / 衣摆 / 大腿根）、头网格（刘海 / 后发）、两臂各 上臂 / 前臂 / 手 三块刚体、两腿各 大腿 / 小腿 / 脚。
  关节处"圆头搭接"：上一节在关节处多带一个圆盘，下一节从关节线切开盖在上面，绕关节转动不露缝。
- 换手：手部插槽换附件（阶梯，瞬间切换）——垂手 / 握剑 / 握召唤器 / 握拳 / 推掌 / 托掌 / 插兜。
- 腿：Spine 原生两骨 IK，脚钉在地上；身体怎么压，膝盖自然弯，脚不滑。
- 动画：关键姿势 + 贝塞尔缓动直接导出（稀疏关键帧，不再 30fps 逐帧烘焙）；关节按 胯 → 腰 → 胸 → 肩 → 肘 → 手 依次滞后（重叠动作）；
  手臂姿势用两骨 IK 求解（给出手腕位置和手的朝向）；头发 / 领结 / 衣摆是弹簧次级运动，压成平滑稀疏关键帧。
用法：python3 tools/rig/build_rig_v6.py [--no-preview] [--only 动画名,…]"""
import json
import math
import sys
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, str(Path(__file__).parent))
from spine_eval import Skeleton  # noqa: E402
from v6_register import key  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
V9 = ROOT / "art/v9"
REG = V9 / "reg"
OUT = ROOT / "art/rig"
PAD = 400                                   # 零件在加大画布上重新变换（剑尖等超出 1024×1536 的部分不丢）
ORIGIN = (505.0, 1475.0)                    # 两脚之间的地面（基准图像素）
STEP = 16                                   # 网格步长

# 选用的版本（配准残差、内容检查后选定）
PICK = {"torso": "torso_b", "head": "head_a", "arm_n": "arm_near_a", "arm_f": "arm_far_a",
        "leg_n": "leg_near_a", "leg_f": "leg_far_b", "blink": "head_blink_a", "glare": "head_glare_a"}
HANDS_N = {"hang": "arm_near_a", "sword": "near_sword_a", "evoker": "near_evoker_a", "fist": "near_fist_a",
           "palm": "near_palm_a", "palmup": "near_palmup_a", "pocket": "near_pocket_a"}
HANDS_F = {"hang": "arm_far_a", "palm": "far_palm_a", "fist": "far_fist_a", "pocket": "far_pocket_a"}

# ───────────── 关节（基准图像素，量自配准后的零件）─────────────
J = {
    "hip": (530, 600), "spine": (530, 490), "chest": (530, 380), "head": (528, 232),
    "tie": (540, 262), "hair_front": (585, 70), "hair_back": (455, 60),
    "coat_n": (450, 600), "coat_f": (630, 600),
    "sh_n": (405, 315), "el_n": (325, 480), "wr_n": (238, 690),
    "sh_f": (628, 320), "el_f": (683, 500), "wr_f": (800, 690),
    "hp_n": (485, 640), "kn_n": (422, 1040), "an_n": (365, 1390),
    "hp_f": (585, 640), "kn_f": (615, 1040), "an_f": (640, 1382),
    "eye": (605, 128),
}
FOOT_CUT = {"n": 1392, "f": 1372}           # 裤脚以下是鞋


def to_spine(p):
    return (p[0] - ORIGIN[0], ORIGIN[1] - p[1])


def ang(a, b):
    """基准图两点 a → b 的方向（Spine 角度：度，逆时针，y 向上）。"""
    return math.degrees(math.atan2(-(b[1] - a[1]), b[0] - a[0]))


# ───────────── 零件载入 ─────────────
_reg = json.loads((REG / "registration.json").read_text())


def load(name):
    """按配准变换把原图变换到加大画布（四周各 PAD）。返回 float32 RGBA。"""
    src = V9 / ("parts" if (V9 / "parts" / f"{name}.png").exists() else "hands") / f"{name}.png"
    P = key(src)
    M = np.array(_reg[name]["M"], np.float64)
    M[:, 2] += PAD
    h, w = P.shape[:2]
    return cv2.warpAffine(P, M, (w + 2 * PAD, h + 2 * PAD), flags=cv2.INTER_LINEAR, borderValue=(0, 0, 0, 0))


def pp(p):
    """基准图像素 → 加大画布像素。"""
    return (p[0] + PAD, p[1] + PAD)


def half_plane(shape, a, b, through, side):
    """以 a→b 为方向、过 through 的垂线把画布分两半。side=+1 取"前方"（沿 a→b），-1 取后方。返回有符号距离图。"""
    h, w = shape[:2]
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    d = np.array(b, np.float32) - np.array(a, np.float32)
    d /= np.linalg.norm(d)
    t = (xs - through[0]) * d[0] + (ys - through[1]) * d[1]
    return t * side


def disc(shape, c, r):
    h, w = shape[:2]
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    return np.hypot(xs - c[0], ys - c[1]) <= r


def width_at(alpha, a, b, at):
    """肢体在 at 处垂直于 a→b 的宽度（像素）。"""
    d = np.array(b, float) - np.array(a, float)
    d /= np.linalg.norm(d)
    n = np.array([-d[1], d[0]])
    hits = [s for s in range(-120, 121) if 0 <= int(at[1] + n[1] * s) < alpha.shape[0] and 0 <= int(at[0] + n[0] * s) < alpha.shape[1]
            and alpha[int(at[1] + n[1] * s), int(at[0] + n[0] * s)] > 0.5]
    return (max(hits) - min(hits)) if hits else 40


def piece(img, mask, feather=1.5):
    """img × mask → 裁剪后的 PIL 图 + 左上角（加大画布像素）。"""
    m = mask.astype(np.float32)
    if feather:
        m = cv2.GaussianBlur(m, (0, 0), feather)
        m = np.where(mask, np.maximum(m, 0.5), m)          # 只软化切口外侧，不吃掉切口内的像素
    # 去掉切口留下的碎渣（小于 150px 的孤立块）
    solid = (mask & (img[..., 3] > 0.3)).astype(np.uint8)
    n, lab, stats, _ = cv2.connectedComponentsWithStats(solid, 8)
    if n > 2:
        small = np.zeros(n, bool)
        small[1:] = stats[1:, cv2.CC_STAT_AREA] < 150
        drop = cv2.dilate(small[lab].astype(np.uint8), np.ones((5, 5), np.uint8)) > 0
        m = m * ~drop
    out = img.copy()
    out[..., 3] *= m
    a = out[..., 3] > 0.02
    ys, xs = np.nonzero(a)
    x0, y0, x1, y1 = xs.min(), ys.min(), xs.max() + 1, ys.max() + 1
    crop = (np.clip(out[y0:y1, x0:x1], 0, 1) * 255).astype(np.uint8)
    return Image.fromarray(crop, "RGBA"), (int(x0), int(y0))


def cut_limb(img, A, B, C, cap_scale=0.5, overlap=10):
    """肢体 A（根）→ B（中关节）→ C（末关节）切成三块：根段带 B 处圆盘，中段带 C 处圆盘，末段从 C 前 overlap 像素起。"""
    A, B, C = pp(A), pp(B), pp(C)
    a = img[..., 3] > 0.02
    rB = width_at(img[..., 3], A, B, B) * cap_scale
    rC = width_at(img[..., 3], B, C, C) * cap_scale
    rA = width_at(img[..., 3], A, B, A) * 0.55
    past_A = half_plane(img.shape, A, B, A, +1) > 0
    past_B = half_plane(img.shape, A, B, B, +1) > 0
    past_C = half_plane(img.shape, B, C, C, +1) > 0
    # 根部也做成圆头：根关节以上只留一个圆盘（肩头 / 胯部本来就画在躯干上），否则手臂一抬，肩上那块会翘到脖子后面
    upper = a & ((past_A & ~past_B) | disc(img.shape, A, rA) | disc(img.shape, B, rB))
    fore = a & past_B & (~past_C | disc(img.shape, C, rC))
    end = a & (half_plane(img.shape, B, C, C, +1) > -overlap)
    return upper, fore, end


# ───────────── 骨骼 ─────────────
def bone_table():
    """(名字, 父骨骼, 位置(基准图像素), 世界角度(度), 长度)。肢体骨骼沿肢体方向，躯干骨骼角度 0。"""
    T = [("root", None, ORIGIN, 0, 0),
         ("hip", "root", J["hip"], 0, 0),
         ("spine", "hip", J["spine"], 0, 0),
         ("chest", "spine", J["chest"], 0, 0),
         ("head", "chest", J["head"], 0, 0),
         ("hair_front", "head", J["hair_front"], -90, 0),
         ("hair_back", "head", J["hair_back"], -90, 0),
         ("tie", "chest", J["tie"], -90, 0)]
    for s in "nf":
        T += [(f"upper_{s}", "chest", J[f"sh_{s}"], ang(J[f"sh_{s}"], J[f"el_{s}"]), 0),
              (f"fore_{s}", f"upper_{s}", J[f"el_{s}"], ang(J[f"el_{s}"], J[f"wr_{s}"]), 0),
              (f"hand_{s}", f"fore_{s}", J[f"wr_{s}"], ang(J[f"el_{s}"], J[f"wr_{s}"]), 0)]
    T += [("coat_n", "hip", J["coat_n"], -90, 0), ("coat_f", "hip", J["coat_f"], -90, 0)]
    for s in "nf":
        T += [(f"thigh_{s}", "hip", J[f"hp_{s}"], ang(J[f"hp_{s}"], J[f"kn_{s}"]), math.dist(J[f"hp_{s}"], J[f"kn_{s}"])),
              (f"shin_{s}", f"thigh_{s}", J[f"kn_{s}"], ang(J[f"kn_{s}"], J[f"an_{s}"]), math.dist(J[f"kn_{s}"], J[f"an_{s}"])),
              (f"foot_{s}", "root", J[f"an_{s}"], 0, 0)]                 # IK 目标；脚贴图挂在这里（站在地上不跟小腿转）
    return T


ANCHORS = ["hand", "grip", "muzzle", "evoker_tip", "sword_tip"]


def build_bones(anchor_px):
    T = bone_table()
    world = {}
    out = []
    for n, parent, p, a, length in T:
        sp = to_spine(p)
        world[n] = (sp, a)
        b = {"name": n}
        if parent:
            (px, py), pa = world[parent]
            dx, dy = sp[0] - px, sp[1] - py
            r = math.radians(-pa)
            b.update(parent=parent, x=round(dx * math.cos(r) - dy * math.sin(r), 3), y=round(dx * math.sin(r) + dy * math.cos(r), 3))
            if abs(a - pa) > 1e-6:
                b["rotation"] = round(a - pa, 3)
        elif a:
            b["rotation"] = a
        if length:
            b["length"] = round(length, 2)
        out.append(b)
    (hx, hy), ha = world["hand_n"]
    for an in ANCHORS:
        sx, sy = to_spine(anchor_px[an])
        dx, dy = sx - hx, sy - hy
        r = math.radians(-ha)
        out.append({"name": an, "parent": "hand_n", "x": round(dx * math.cos(r) - dy * math.sin(r), 2),
                    "y": round(dx * math.sin(r) + dy * math.cos(r), 2)})
    return out


# ───────────── 附件 ─────────────
def region(im, topleft, bone, W):
    """刚体零件：贴图中心换算到骨骼局部坐标，旋转抵消骨骼世界角度（装配姿势下摆正）。"""
    cx, cy = topleft[0] - PAD + im.width / 2, topleft[1] - PAD + im.height / 2
    inv = np.linalg.inv(W[bone])
    lx, ly = (inv @ np.array([*to_spine((cx, cy)), 1.0]))[:2]
    rot = -math.degrees(math.atan2(W[bone][1, 0], W[bone][0, 0]))
    return {"x": round(lx, 2), "y": round(ly, 2), "rotation": round(rot, 3), "width": im.width, "height": im.height}


def mesh(im, topleft, weight_fn, W, bone_index):
    """网格零件：按 STEP 切格，只保留有像素的格子；每个顶点按 weight_fn(基准图像素) 绑定到 ≤4 根骨骼。"""
    a = np.asarray(im.getchannel("A")) > 8
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
    inv = {b: np.linalg.inv(W[b]) for b in bone_index}
    uvs, verts = [], []
    for (x, y) in verts_px:
        uvs += [round(x / w, 5), round(y / h, 5)]
        px, py = x + topleft[0] - PAD, y + topleft[1] - PAD
        ws = weight_fn(px, py)
        sp = np.array([*to_spine((px, py)), 1.0])
        verts.append(len(ws))
        for b, v in ws:
            lx, ly = (inv[b] @ sp)[:2]
            verts += [bone_index[b], round(lx, 2), round(ly, 2), round(v, 4)]
    return {"type": "mesh", "uvs": uvs, "triangles": tris, "vertices": verts, "hull": 0, "width": w, "height": h}


def ramp(v, a, b):
    return np.clip((v - a) / (b - a), 0, 1)


def weight_maps(shape, order_spec):
    """order_spec: [(骨骼, 权重图)]，按优先级依次分配，剩余给最后的兜底骨骼。返回 fn(x, y)。"""
    h, w = shape

    def fn(x, y):
        xi, yi = min(max(int(x), 0), w - 1), min(max(int(y), 0), h - 1)
        remaining, ws = 1.0, []
        for b, m in order_spec[:-1]:
            v = float(m[yi, xi]) * remaining
            if v > 1e-3:
                ws.append((b, v))
                remaining -= v
        ws.append((order_spec[-1][0], remaining))
        ws = sorted(ws, key=lambda t: -t[1])[:4]
        s = sum(v for _, v in ws)
        return [(n, v / s) for n, v in ws if v / s > 1e-3]
    return fn


def blur(m, s):
    return cv2.GaussianBlur(m.astype(np.float32), (0, 0), s)


def torso_weights():
    h, w = 1536, 1024
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    tie = blur((xs > 512) & (xs < 572) & (ys > 262) & (ys < 350), 6) * ramp(ys, 262, 292)
    pants = (ys > 600)
    gap = 535 + (ys - 600) * 0.15                                   # 两腿分界（裤裆）略向右斜
    thigh_n = blur(pants & (xs < gap) & (xs > 420), 10) * ramp(ys, 625, 700)
    thigh_f = blur(pants & (xs >= gap) & (xs < 625), 10) * ramp(ys, 625, 700)
    coat_n = blur((xs < 445) & (ys > 560), 12) * ramp(ys, 590, 690)
    coat_f = blur((xs > 615) & (ys > 560), 12) * ramp(ys, 590, 690)
    chest = ramp(ys, 470, 390)
    spine = ramp(ys, 390, 470) * ramp(ys, 590, 520)
    return [("tie", tie), ("coat_n", coat_n), ("coat_f", coat_f), ("thigh_n", thigh_n), ("thigh_f", thigh_f),
            ("chest", chest), ("spine", spine), ("hip", None)]


def head_weights():
    h, w = 1536, 1024
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)

    def poly(points, s):
        m = Image.new("L", (w, h), 0)
        ImageDraw.Draw(m).polygon(points, fill=255)
        return blur(np.asarray(m) > 0, s)
    front = poly([(540, 70), (640, 60), (650, 200), (612, 215), (560, 170)], 10) * ramp(ys, 75, 125)
    back = poly([(420, 40), (488, 40), (492, 210), (440, 230), (415, 150)], 10) * ramp(xs, 490, 455)
    return [("hair_front", front), ("hair_back", back), ("head", None)]


def eye_glow_image():
    s = 120
    ys, xs = np.mgrid[0:s, 0:s].astype(np.float32) - s / 2
    r = np.hypot(xs * 0.8, ys * 1.6)
    core = np.exp(-(r / 7) ** 2)
    halo = np.exp(-(r / 22) ** 2) * 0.55
    streak = np.exp(-(ys / 2.2) ** 2) * np.exp(-(np.abs(xs) / 46)) * 0.7
    a = np.clip(core + halo + streak, 0, 1)
    col = np.stack([0.35 + 0.65 * core, 0.75 + 0.25 * core, np.ones_like(core)], -1)
    return Image.fromarray((np.dstack([col, a]) * 255).astype(np.uint8), "RGBA")


def expression_patch(head, variant):
    """表情贴片：variant 与 head 差异明显的区域（外扩 + 羽化）。返回 PIL + 左上角。"""
    d = np.abs(variant[..., :3] - head[..., :3]).sum(-1) * (variant[..., 3] > 0.5)
    m = (d > 0.25).astype(np.uint8)
    m = cv2.morphologyEx(m, cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
    n, lab, stats, _ = cv2.connectedComponentsWithStats(m, 8)
    keep = np.zeros_like(m)
    for i in range(1, n):
        if stats[i, cv2.CC_STAT_AREA] > 30:
            keep[lab == i] = 1
    keep = cv2.dilate(keep, np.ones((15, 15), np.uint8))
    soft = cv2.GaussianBlur(keep.astype(np.float32), (0, 0), 4)
    out = variant.copy()
    out[..., 3] *= np.clip(soft * 1.4, 0, 1)
    ys, xs = np.nonzero(out[..., 3] > 0.02)
    x0, y0, x1, y1 = xs.min(), ys.min(), xs.max() + 1, ys.max() + 1
    return Image.fromarray((np.clip(out[y0:y1, x0:x1], 0, 1) * 255).astype(np.uint8), "RGBA"), (int(x0), int(y0))


def shadow_image():
    w, h = 520, 90
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    r = np.hypot((xs - w / 2) / (w / 2), (ys - h / 2) / (h / 2))
    a = np.clip(1 - r, 0, 1) ** 1.6 * 0.42
    return Image.fromarray((np.dstack([np.zeros((h, w, 3)), a]) * 255).astype(np.uint8), "RGBA")


# 绘制顺序（从后到前）。f_*_fr：远侧手臂伸到身前时用的前景插槽；n_hand_bk：握剑扛肩时手 + 剑在头后面。
SLOTS = ["shadow",
         "f_upper", "f_fore", "f_hand",
         "thigh_f", "foot_f", "shin_f",
         "thigh_n", "foot_n", "shin_n",
         "n_hand_bk",
         "head", "blink", "glare", "eye_glow",
         "torso",
         "f_upper_fr", "f_fore_fr", "f_hand_fr",
         "n_upper", "n_fore", "n_hand"]
SLOT_BONE = {"shadow": "root", "f_upper": "upper_f", "f_fore": "fore_f", "f_hand": "hand_f",
             "f_upper_fr": "upper_f", "f_fore_fr": "fore_f", "f_hand_fr": "hand_f",
             "thigh_f": "thigh_f", "foot_f": "foot_f", "shin_f": "shin_f", "thigh_n": "thigh_n", "foot_n": "foot_n", "shin_n": "shin_n",
             "n_hand_bk": "hand_n", "head": "head", "blink": "head", "glare": "head", "eye_glow": "head", "torso": "root",
             "n_upper": "upper_n", "n_fore": "fore_n", "n_hand": "hand_n"}
DEFAULT = {"shadow": "shadow", "f_upper": "f_upper", "f_fore": "f_fore", "f_hand": "f_hand_hang",
           "thigh_f": "thigh_f", "foot_f": "foot_f", "shin_f": "shin_f", "thigh_n": "thigh_n", "foot_n": "foot_n", "shin_n": "shin_n",
           "head": "head", "torso": "torso", "n_upper": "n_upper", "n_fore": "n_fore", "n_hand": "n_hand_hang"}


def skin_centroid(img):
    c = img[..., :3]
    m = (img[..., 3] > 0.5) & (c[..., 0] > 0.75) & (c[..., 1] > 0.6) & (c[..., 0] - c[..., 2] > 0.08)
    ys, xs = np.nonzero(m)
    return (float(xs.mean()), float(ys.mean())) if len(xs) else None


def farthest(img_alpha, frm):
    ys, xs = np.nonzero(img_alpha > 0.5)
    i = np.argmax(np.hypot(xs - frm[0], ys - frm[1]))
    return float(xs[i]), float(ys[i])


def build_setup():
    images, skin = {}, {}
    pieces = {}                              # 附件名 → (PIL, 左上角, 骨骼)
    base = load_base()

    # 躯干 / 头（网格）
    torso = load(PICK["torso"])
    pieces["torso"] = (*piece(torso, torso[..., 3] > 0.02, 0), "MESH_T")
    head = load(PICK["head"])
    pieces["head"] = (*piece(head, head[..., 3] > 0.02, 0), "MESH_H")
    for k in ("blink", "glare"):
        im, tl = expression_patch(head, load(PICK[k]))
        pieces[k] = (im, tl, "head")

    # 手臂
    hand_px = {}
    for s, table in (("n", HANDS_N), ("f", HANDS_F)):
        arm = load(PICK[f"arm_{s}"])
        up, fo, _ = cut_limb(arm, J[f"sh_{s}"], J[f"el_{s}"], J[f"wr_{s}"])
        pieces[f"{s}_upper"] = (*piece(arm, up), f"upper_{s}")
        pieces[f"{s}_fore"] = (*piece(arm, fo), f"fore_{s}")
        base_alpha = cv2.dilate((arm[..., 3] > 0.3).astype(np.uint8), np.ones((7, 7), np.uint8)) > 0
        for k, name in table.items():
            v = arm if name == PICK[f"arm_{s}"] else load(name)
            _, _, end = cut_limb(v, J[f"sh_{s}"], J[f"el_{s}"], J[f"wr_{s}"])
            # 换手图里新增、但不在手腕线以外的东西（剑柄尾 / 枪身）也归手部
            extra = (v[..., 3] > 0.3) & ~base_alpha & (half_plane(v.shape, pp(J[f"el_{s}"]), pp(J[f"wr_{s}"]), pp(J[f"wr_{s}"]), +1) > -120)
            m = (end | extra) & (v[..., 3] > 0.02)
            pieces[f"{s}_hand_{k}"] = (*piece(v, m), f"hand_{s}")
            hand_px[(s, k)] = v * m[..., None]

    # 腿
    for s in "nf":
        leg = load(PICK[f"leg_{s}"])
        th, sh, _ = cut_limb(leg, J[f"hp_{s}"], J[f"kn_{s}"], J[f"an_{s}"], cap_scale=0.5)
        a = leg[..., 3] > 0.02
        ys = np.mgrid[0:leg.shape[0], 0:leg.shape[1]][0]
        foot = a & (ys >= FOOT_CUT[s] + PAD)
        shin = a & ~th & (ys < FOOT_CUT[s] + PAD + 8) | (sh & (ys < FOOT_CUT[s] + PAD + 8))
        pieces[f"thigh_{s}"] = (*piece(leg, th), f"thigh_{s}")
        pieces[f"shin_{s}"] = (*piece(leg, shin & a), f"shin_{s}")
        pieces[f"foot_{s}"] = (*piece(leg, foot), f"foot_{s}")

    # 特效锚点（基准图像素）：剑尖 / 握把 / 枪口
    wr = pp(J["wr_n"])
    sw = hand_px[("n", "sword")]
    tip = farthest(sw[..., 3], wr)
    grip = skin_centroid(sw) or wr
    ev = hand_px[("n", "evoker")]
    muzzle = farthest(ev[..., 3], wr)
    ev_grip = skin_centroid(ev) or wr
    un = lambda p: (p[0] - PAD, p[1] - PAD)
    anchor_px = {"hand": un(grip), "sword_tip": un(tip), "grip": un(ev_grip), "muzzle": un(muzzle), "evoker_tip": un(muzzle)}

    bones = build_bones(anchor_px)
    data = {"skeleton": {"spine": "4.2.43", "images": "./", "audio": "", "x": -420, "y": -20, "width": 900, "height": 1520},
            "bones": bones, "slots": [], "ik": [
                {"name": f"leg_{s}", "order": i, "bones": [f"thigh_{s}", f"shin_{s}"], "target": f"foot_{s}", "bendPositive": False}
                for i, s in enumerate("nf")],
            "skins": [{"name": "default", "attachments": {}}], "events": {}, "animations": {}}
    sk = Skeleton(data, {})
    W = sk.world()
    bone_index = {b["name"]: i for i, b in enumerate(bones)}

    att = {}
    for name, (im, tl, bone) in pieces.items():
        images[name] = im
        if bone == "MESH_T":
            att[name] = mesh(im, tl, weight_maps((1536, 1024), torso_weights()), W, bone_index)
        elif bone == "MESH_H":
            att[name] = mesh(im, tl, weight_maps((1536, 1024), head_weights()), W, bone_index)
        else:
            att[name] = region(im, tl, bone, W)
    images["shadow"] = shadow_image()
    images["eye_glow"] = eye_glow_image()
    sx, sy = to_spine((ORIGIN[0] + 10, ORIGIN[1] - 6))
    att["shadow"] = {"x": round(sx, 2), "y": round(sy, 2), "width": 520, "height": 90}
    ex, ey = to_spine(J["eye"])
    inv = np.linalg.inv(W["head"])
    lx, ly = (inv @ np.array([ex, ey, 1.0]))[:2]
    att["eye_glow"] = {"x": round(lx, 2), "y": round(ly, 2), "width": 120, "height": 120}

    # 插槽与皮肤
    slot_atts = {s: [] for s in SLOTS}
    slot_atts["shadow"] = ["shadow"]
    slot_atts["torso"] = ["torso"]
    slot_atts["head"] = ["head"]
    slot_atts["blink"] = ["blink"]
    slot_atts["glare"] = ["glare"]
    slot_atts["eye_glow"] = ["eye_glow"]
    for s in "nf":
        for part in ("thigh", "shin", "foot"):
            slot_atts[f"{part}_{s}"] = [f"{part}_{s}"]
    slot_atts["n_upper"] = ["n_upper"]
    slot_atts["n_fore"] = ["n_fore"]
    slot_atts["n_hand"] = [f"n_hand_{k}" for k in HANDS_N]
    slot_atts["n_hand_bk"] = [f"n_hand_{k}" for k in HANDS_N]
    for suf in ("", "_fr"):
        slot_atts[f"f_upper{suf}"] = ["f_upper"]
        slot_atts[f"f_fore{suf}"] = ["f_fore"]
        slot_atts[f"f_hand{suf}"] = [f"f_hand_{k}" for k in HANDS_F]
    skin_out = {}
    for s in SLOTS:
        skin_out[s] = {a: att[a] for a in slot_atts[s]}
        slot = {"name": s, "bone": SLOT_BONE[s]}
        if s in DEFAULT:
            slot["attachment"] = DEFAULT[s]
        if s == "eye_glow":
            slot["blend"] = "additive"
        data["slots"].append(slot)
    data["skins"][0]["attachments"] = skin_out
    return data, images, base, W


def load_base():
    b = key(V9 / "base_a.png")
    return b


# ───────────── 姿势工具 ─────────────
SETUP = {n: (to_spine(p), a, L) for n, _, p, a, L in bone_table()}


def P(**kw):
    """姿势：骨骼 → {r, x, y, sx, sy}（相对装配姿势的增量；sx/sy 是相对 1 的偏移）。"""
    return {b: dict(v) for b, v in kw.items()}


def add(*ps):
    out = {}
    for p in ps:
        for b, ch in p.items():
            d = out.setdefault(b, {})
            for c, v in ch.items():
                d[c] = d.get(c, 0.0) + v
    return out


def scale(p, k):
    return {b: {c: v * k for c, v in ch.items()} for b, ch in p.items()}


def mixp(a, b, u):
    return add(scale(a, 1 - u), scale(b, u))


def _rot(p, c, deg):
    """图像坐标里绕 c 旋转（Spine 正角 = 屏幕上逆时针）。"""
    r = math.radians(deg)
    dx, dy = p[0] - c[0], p[1] - c[1]
    return (c[0] + dx * math.cos(r) + dy * math.sin(r), c[1] - dx * math.sin(r) + dy * math.cos(r))


def to_chest_frame(p, frame):
    """世界（画面）像素 → 胸部装配坐标。frame = (hx, hy, spine, chest)：胯位移（图像 y 向下）+ 腰 / 胸旋转。"""
    hx, hy, sp, ch = frame
    s_c = (J["spine"][0] + hx, J["spine"][1] + hy)
    c_c = _rot((J["chest"][0] + hx, J["chest"][1] + hy), s_c, sp)
    q = _rot(p, c_c, -ch)
    q = _rot(q, s_c, -sp)
    return (q[0] - hx, q[1] - hy)


def arm(side, wrist, hand_dir=None, bend=-1, lean=0.0, hand_rel=0.0, frame=None):
    """两骨 IK：给手腕目标（基准图像素，躯干未转动时）、手的世界方向（Spine 度），求上臂 / 前臂 / 手的旋转增量。
    bend：+1 肘在"肩 → 手腕"连线的逆时针一侧，-1 顺时针一侧。hand_dir 缺省 = 前臂方向 + hand_rel。
    坐标都在"胸部装配坐标系"里（身体怎么动，手相对身体的位置不变）。"""
    if frame is not None:                     # 目标给的是世界坐标：换算到胸部装配坐标；手的方向减去躯干转角
        wrist = to_chest_frame(wrist, frame)
        if hand_dir is not None:
            hand_dir = hand_dir - frame[2] - frame[3]
    S = np.array(J[f"sh_{side}"], float)
    Tg = np.array(wrist, float)
    if lean:                                  # 目标是"胸转之后"的位置：先逆转回胸的装配坐标系
        c = np.array(J["chest"], float)
        r = math.radians(lean)                # 图像坐标 y 向下：Spine 正角（逆时针）对应图像里 -r
        R = np.array([[math.cos(r), math.sin(r)], [-math.sin(r), math.cos(r)]])
        Tg = c + R.T @ (Tg - c)
    L1 = math.dist(J[f"sh_{side}"], J[f"el_{side}"])
    L2 = math.dist(J[f"el_{side}"], J[f"wr_{side}"])
    d = Tg - S
    D = min(max(np.linalg.norm(d), abs(L1 - L2) + 1), L1 + L2 - 0.5)
    base = math.atan2(-d[1], d[0])                       # Spine 角（y 向上）
    a = math.acos((L1 * L1 + D * D - L2 * L2) / (2 * L1 * D))
    phi_u = base + bend * a
    E = S + L1 * np.array([math.cos(phi_u), -math.sin(phi_u)])
    phi_f = math.atan2(-(Tg - E)[1], (Tg - E)[0])
    u0, f0 = SETUP[f"upper_{side}"][1], SETUP[f"fore_{side}"][1]
    pu, pf = math.degrees(phi_u), math.degrees(phi_f)
    if hand_dir is None:
        hand_dir = pf + hand_rel
    du = wrap(pu - u0)
    df = wrap((pf - pu) - (f0 - u0))
    dh = wrap(hand_dir - pf)
    return {f"upper_{side}": {"r": du}, f"fore_{side}": {"r": df}, f"hand_{side}": {"r": dh}}


def wrap(a):
    return (a + 180) % 360 - 180


def feet(n=(0, 0), f=(0, 0), rn=0.0, rf=0.0):
    """脚（IK 目标）的位移（基准图像素，y 向下为正）与旋转。"""
    out = {}
    if n != (0, 0) or rn:
        out["foot_n"] = {"x": n[0], "y": -n[1], "r": rn}
    if f != (0, 0) or rf:
        out["foot_f"] = {"x": f[0], "y": -f[1], "r": rf}
    return out


def body(hx=0.0, hy=0.0, spine=0.0, chest=0.0, head=0.0, sy=0.0):
    """胯位移（图像像素，y 向下为正）+ 腰 / 胸 / 头旋转（Spine 度：正 = 后仰，负 = 前倾）。sy：躯干竖向伸缩。"""
    p = {"hip": {"x": hx, "y": -hy}, "spine": {"r": spine}, "chest": {"r": chest}, "head": {"r": head}}
    if sy:
        p["chest"]["sy"] = sy
        p["spine"]["sy"] = sy * 0.6
    return p


# 手腕的世界方向参考（Spine 度）：装配姿势里近侧前臂朝左下约 -112°，远侧朝右下约 -58°
HANG_N = ang(J["el_n"], J["wr_n"])
HANG_F = ang(J["el_f"], J["wr_f"])
SWORD_DIR = None                              # 握剑手图里剑身相对手骨骼的角度，build 时量出


# ───────────── 关键帧导出 ─────────────
EASE = {"io": (0.45, 0.0, 0.55, 1.0), "out": (0.15, 0.7, 0.3, 1.0), "in": (0.6, 0.0, 0.9, 0.45), "snap": (0.05, 0.9, 0.15, 1.0),
        "back": (0.25, 1.45, 0.45, 1.0), "soft": (0.3, 0.0, 0.3, 1.0), "lin": None, "step": "stepped",
        "whip": (0.7, 0.0, 0.85, 0.6)}
LAG = {"hip": 0.0, "spine": 0.012, "chest": 0.024, "head": 0.045, "upper_n": 0.03, "fore_n": 0.048, "hand_n": 0.066,
       "upper_f": 0.036, "fore_f": 0.054, "hand_f": 0.072, "foot_n": 0.0, "foot_f": 0.0}
CHANNELS = {"rotate": ("r",), "translate": ("x", "y"), "scale": ("sx", "sy")}
SECONDARY = ("hair_front", "hair_back", "tie", "coat_n", "coat_f")


class Anim:
    def __init__(self, name, duration, loop=False, lag=1.0):
        self.name, self.dur, self.loop, self.lag = name, duration, loop, lag
        self.keys = []                        # (t, pose, ease)：完整姿势（缺省骨骼 = 装配姿势）
        self.extra = []                       # (t, 局部姿势, ease)：只给其中几根骨骼加的关键帧（如抬脚），不滞后
        self.slots = {}                       # slot → [(t, 附件名或 None)]
        self.colors = {}                      # slot → [(t, alpha)]
        self.events = []

    def k(self, t, pose, ease="io"):
        self.keys.append((t, pose, ease))
        return self

    def kb(self, t, pose, ease="io"):
        self.extra.append((t, pose, ease))
        return self

    def att(self, t, slot, name):
        self.slots.setdefault(slot, []).append((t, name))
        return self

    def hand(self, t, side, kind, back=False):
        """换手（阶梯）。近侧 back=True：手 + 剑放到头后插槽（握剑扛肩）。"""
        if side == "n":
            self.att(t, "n_hand", None if back else f"n_hand_{kind}")
            self.att(t, "n_hand_bk", f"n_hand_{kind}" if back else None)
        else:
            self.far(t, kind=kind)
        return self

    def far(self, t, kind=None, front=None):
        """远侧手臂：换手 / 换到身前插槽（伸到胸前时）。两组插槽只有一组有附件。"""
        st = getattr(self, "_far", {"kind": "hang", "front": False})
        if kind is not None:
            st["kind"] = kind
        if front is not None:
            st["front"] = front
        self._far = dict(st)
        fr = st["front"]
        self.att(t, "f_upper", None if fr else "f_upper")
        self.att(t, "f_fore", None if fr else "f_fore")
        self.att(t, "f_hand", None if fr else f"f_hand_{st['kind']}")
        self.att(t, "f_upper_fr", "f_upper" if fr else None)
        self.att(t, "f_fore_fr", "f_fore" if fr else None)
        self.att(t, "f_hand_fr", f"f_hand_{st['kind']}" if fr else None)
        return self

    def ev(self, t, name):
        self.events.append((t, name))
        return self

    def alpha(self, slot, keys):
        self.colors[slot] = keys
        return self


def bezier(e, t0, v0, t1, v1):
    x1, y1, x2, y2 = e
    return [t0 + x1 * (t1 - t0), v0 + y1 * (v1 - v0), t0 + x2 * (t1 - t0), v0 + y2 * (v1 - v0)]


def export_anim(a):
    """关键姿势 → Spine 时间轴。每根骨骼的关键帧时间 = 姿势时间 + 该骨骼滞后（首尾不滞后）；再并入该骨骼的额外关键帧。"""
    keys = sorted(a.keys, key=lambda e: e[0])
    bones = sorted({b for _, p, _ in keys for b in p} | {b for _, p, _ in a.extra for b in p})
    out = {}
    for b in bones:
        lag = 0.0 if a.loop else LAG.get(b, 0.03) * a.lag
        seq = []
        for i, (t, p, e) in enumerate(keys):
            tt = t if (i == 0 or i == len(keys) - 1) else t + lag
            seq.append([tt, p.get(b, {}), e])
        for t, p, e in a.extra:
            if b in p:
                seq.append([t, p[b], e])
        seq.sort(key=lambda r: r[0])
        for i in range(1, len(seq)):
            seq[i][0] = max(seq[i][0], seq[i - 1][0] + 0.004)
        if seq[-1][0] > a.dur:
            over = seq[-1][0] - a.dur
            n = len(seq)
            for i in range(n):
                seq[i][0] = min(seq[i][0], a.dur - 0.004 * (n - 1 - i))
        tl = {}
        for kind, chs in CHANNELS.items():
            off = 1.0 if kind == "scale" else 0.0
            vals = [[ch.get(c, 0.0) for c in chs] for _, ch, _ in seq]
            if kind == "rotate":              # 角度连续：相邻关键帧取最短转向（IK 求出的角度在 ±180 处会跳）
                for i in range(1, len(vals)):
                    vals[i][0] += 360 * round((vals[i - 1][0] - vals[i][0]) / 360)
            if all(abs(v) < 1e-4 for row in vals for v in row):
                continue
            arr = []
            for i, (t, _, _) in enumerate(seq):
                k = {"time": round(t, 4)}
                for c, v in zip(chs, vals[i]):
                    k[{"r": "value", "x": "x", "y": "y", "sx": "x", "sy": "y"}[c]] = round(v + off, 4)
                if i + 1 < len(seq):
                    e = EASE[seq[i + 1][2]]
                    if e == "stepped":
                        k["curve"] = "stepped"
                    elif e is not None:
                        curve = []
                        for j in range(len(chs)):
                            curve += [round(x, 4) for x in bezier(e, t, vals[i][j] + off, seq[i + 1][0], vals[i + 1][j] + off)]
                        k["curve"] = curve
                arr.append(k)
            tl[kind] = arr
        if tl:
            out[b] = tl
    return out


def smooth_keys(samples, loop):
    """采样点 [(t, v)] → 平滑贝塞尔关键帧（Catmull-Rom 切线）。"""
    n = len(samples)
    out = []
    for i, (t, v) in enumerate(samples):
        out.append({"time": round(t, 4), "value": round(v, 3)})
    for i in range(n - 1):
        (t0, v0), (t1, v1) = samples[i], samples[i + 1]

        def slope(j):
            if 0 < j < n - 1:
                return (samples[j + 1][1] - samples[j - 1][1]) / (samples[j + 1][0] - samples[j - 1][0])
            if loop:
                tp, vp = samples[-2][0] - samples[-1][0], samples[-2][1]
                tn, vn = samples[1][0], samples[1][1]
                return (vn - vp) / (tn - tp)
            return 0.0
        m0, m1 = slope(i), slope(i + 1)
        dt = t1 - t0
        out[i]["curve"] = [round(t0 + dt / 3, 4), round(v0 + m0 * dt / 3, 3), round(t1 - dt / 3, 4), round(v1 - m1 * dt / 3, 3)]
    return out


def add_secondary(data, images, name, anim, loop):
    """次级运动：头发 / 领结 / 衣摆是挂着的"垂坠物"。每根骨骼想保持世界方向不变（父骨骼转了它就反向摆），
    并被父骨骼原点的加速度甩动；阻尼弹簧积分 120Hz，按 1/15s 取样后压成平滑关键帧。"""
    sk = Skeleton(data, images)
    dt = 1 / 120
    n = int(round(anim["_dur"] * 120))
    par = {b["name"]: b.get("parent") for b in data["bones"]}
    params = {"hair_front": (2.6, 0.32, 0.9, 0.012), "hair_back": (2.2, 0.3, 0.9, 0.014), "tie": (2.0, 0.26, 0.8, 0.016),
              "coat_n": (2.4, 0.36, 0.7, 0.01), "coat_f": (2.4, 0.36, 0.7, 0.01)}
    worlds = []
    for i in range(n + 1):
        W = sk.world(name, min(i * dt, anim["_dur"]))
        worlds.append(W)
    passes = 2 if loop else 1
    for b, (f, z, follow, acc) in params.items():
        p = par[b]
        w0 = 2 * math.pi * f
        th, vel = 0.0, 0.0
        rot0 = math.degrees(math.atan2(worlds[0][p][1, 0], worlds[0][p][0, 0]))
        samples = []
        for ps in range(passes):
            prev_v = None
            for i in range(n + 1):
                Wp = worlds[i][p]
                rot = math.degrees(math.atan2(Wp[1, 0], Wp[0, 0]))
                pos = Wp[:2, 2]
                if i:
                    v = (pos - worlds[i - 1][p][:2, 2]) / dt
                    a_x = 0.0 if prev_v is None else (v[0] - prev_v[0]) / dt
                    prev_v = v
                else:
                    a_x = 0.0
                target = -follow * wrap(rot - rot0) + max(-40, min(40, acc * a_x * (1 if b != "hair_back" else 1.2)))
                acc_t = -w0 * w0 * (th - target) - 2 * z * w0 * vel
                vel += acc_t * dt
                th += vel * dt
                if ps == passes - 1 and i % 8 == 0:
                    samples.append((i * dt, th))
        if loop:
            samples[-1] = (samples[-1][0], samples[0][1])
        if max(abs(v) for _, v in samples) > 0.05:
            anim["bones"].setdefault(b, {})["rotate"] = smooth_keys(samples, loop)


def slot_timelines(a):
    out = {}
    for s, keys in a.slots.items():
        keys = sorted(keys, key=lambda e: e[0])
        out.setdefault(s, {})["attachment"] = [{"time": round(t, 4), "name": n} for t, n in keys]
    for s, keys in a.colors.items():
        out.setdefault(s, {})["rgba"] = [{"time": round(t, 4), "color": "ffffff%02x" % round(255 * max(0, min(1, v)))} for t, v in keys]
    return out


def finish(data, images, a):
    bones = export_anim(a)
    anim = {"bones": bones, "_dur": a.dur}
    data["animations"][a.name] = anim
    add_secondary(data, images, a.name, anim, a.loop)
    del anim["_dur"]
    st = slot_timelines(a)
    if st:
        anim["slots"] = st
    if a.events:
        anim["events"] = [{"time": round(t, 4), "name": n} for t, n in sorted(a.events)]
    # 时长：在末尾补一个关键帧，保证动画长度 = dur
    for tl in anim["bones"].values():
        for kind, arr in tl.items():
            if arr and arr[-1]["time"] < a.dur - 1e-4:
                last = dict(arr[-1])
                last.pop("curve", None)
                last["time"] = round(a.dur, 4)
                arr.append(last)


# 动画在 make_animations.py 风格的独立模块里定义（见 v6_anims.py）
import v6_anims  # noqa: E402


# ───────────── 校验 / 导出 / 预览 ─────────────
def validate(data, images, base):
    rep, ok = [], True

    def mark(passed, msg):
        nonlocal ok
        ok &= passed
        rep.append(f"[{'OK' if passed else 'FAIL'}] {msg}")

    # 1. 拼合：装配姿势渲染 vs 基准图
    saved = [(s, s.get("attachment")) for s in data["slots"]]
    for s in data["slots"]:
        if s["name"] == "shadow":
            s.pop("attachment", None)
    r = np.asarray(Skeleton(data, images).render(canvas=(1024, 1536), origin=ORIGIN)).astype(float) / 255
    for s, a in saved:
        if a:
            s["attachment"] = a
    m = base[..., 3] > 0.5
    diff = np.abs(r[..., :3] - base[..., :3]).mean(-1)[m].mean() * 255
    miss = ((r[..., 3] < 0.25) & m).mean() / m.mean()
    extra = ((r[..., 3] > 0.75) & ~(cv2.dilate(m.astype(np.uint8), np.ones((9, 9), np.uint8)) > 0)).sum()
    mark(diff < 22 and miss < 0.01, f"拼合：平均色差 {diff:.1f}/255，缺失 {miss * 100:.2f}%，多出 {extra}px")
    Image.fromarray((np.clip(r, 0, 1) * 255).astype(np.uint8)).save(OUT / "v6_setup.png")

    # 2. 锚点
    names = {b["name"] for b in data["bones"]}
    mark(set(ANCHORS) <= names, "特效锚点骨骼齐全")
    # 3. 动画齐全
    need = ["idle_loop", "relaxed_loop", "low_health_loop", "aim_loop", "ready_sword_loop", "aim_in", "sword_in", "aim_out", "sword_out",
            "attack", "attack_from_ready", "cast", "attack_sovereign", "sovereign_from_aim", "cast_from_aim", "cast_light", "hurt", "die"]
    missing = [n for n in need if n not in data["animations"]]
    mark(not missing, f"动画齐全 {missing or ''}")
    # 4. 循环首尾一致
    for n, an in data["animations"].items():
        if n.endswith("_loop"):
            bad = [b for b, tl in an["bones"].items() for kind, arr in tl.items()
                   if any(abs(arr[0].get(f, 0) - arr[-1].get(f, 0)) > 1e-2 for f in ("value", "x", "y"))]
            mark(not bad, f"循环首尾一致 {n} {bad or ''}")
    # 5. 衔接：前一个动画末帧 ≈ 后一个首帧（世界坐标下比较手腕 / 头 / 胯 / 脚）
    sk = Skeleton(data, images)
    probe = ["hip", "chest", "head", "hand_n", "hand_f", "foot_n", "foot_f", "fore_n", "fore_f"]

    def pose_at(n, t):
        W = sk.world(n, t)
        return {b: W[b][:2, 2] for b in probe}
    pairs = [("aim_in", "aim_loop"), ("sword_in", "ready_sword_loop"), ("aim_loop", "aim_out"), ("ready_sword_loop", "sword_out"),
             ("aim_out", "idle_loop"), ("sword_out", "idle_loop"), ("ready_sword_loop", "attack_from_ready"), ("aim_loop", "cast_from_aim"),
             ("aim_loop", "sovereign_from_aim"), ("attack_from_ready", "idle_loop"), ("cast_from_aim", "idle_loop"), ("attack", "idle_loop"),
             ("cast", "idle_loop"), ("cast_light", "idle_loop"), ("hurt", "idle_loop"), ("attack_sovereign", "idle_loop"),
             ("sovereign_from_aim", "idle_loop"), ("idle_loop", "attack"), ("idle_loop", "aim_in"), ("idle_loop", "sword_in")]
    for x, y in pairs:
        if x not in data["animations"] or y not in data["animations"]:
            continue
        dur = max(k["time"] for tl in data["animations"][x]["bones"].values() for arr in tl.values() for k in arr)
        a_, b_ = pose_at(x, dur), pose_at(y, 0.0)
        worst = max(np.linalg.norm(a_[b] - b_[b]) for b in probe)
        mark(worst <= 8.0, f"衔接 {x} → {y}: 最大 {worst:.1f}px")
    # 6. IK 可达：腿不能被拉直到脚离开目标
    for n in data["animations"]:
        dur = max(k["time"] for tl in data["animations"][n]["bones"].values() for arr in tl.values() for k in arr)
        worst = 0.0
        for i in range(0, int(dur * 30) + 1):
            W = sk.world(n, i / 30)
            for s in "nf":
                end = W[f"shin_{s}"] @ np.array([SETUP[f"shin_{s}"][2], 0, 1.0])
                worst = max(worst, float(np.linalg.norm(end[:2] - W[f"foot_{s}"][:2, 2])))
        mark(worst <= 3.0, f"脚钉地 {n}: 最大脱离 {worst:.1f}px")
    return ok, rep


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
    import build_rig
    build_rig.stage_pck(out)
    return out, sheet.size


PREVIEW = ("idle_loop", "sword_in", "ready_sword_loop", "attack_from_ready", "attack", "attack_sovereign", "hurt", "cast_light")


def previews(data, images, names=PREVIEW, fps=20):
    out = ROOT / "art/rig_preview"
    out.mkdir(parents=True, exist_ok=True)
    sk = Skeleton(data, images)
    for name in names:
        an = data["animations"][name]
        dur = max(k["time"] for tl in an["bones"].values() for arr in tl.values() for k in arr)
        frames = []
        for i in range(int(dur * fps) + 1):
            im = sk.render(name, i / fps, canvas=(1500, 1700), origin=(700, 1620), bg=(36, 40, 60, 255)).convert("RGB")
            frames.append(im.resize((375, 425), Image.LANCZOS).convert("P", palette=Image.ADAPTIVE, colors=200))
        frames[0].save(out / f"v6_{name}.gif", save_all=True, append_images=frames[1:], duration=int(1000 / fps), loop=0, optimize=True)
    return out


def main():
    data, images, base, W = build_setup()
    only = None
    if "--only" in sys.argv:
        only = set(sys.argv[sys.argv.index("--only") + 1].split(","))
    for a in v6_anims.make(data):
        if only and a.name not in only:
            continue
        finish(data, images, a)
    if only:
        print(previews(data, images, [n for n in only if n in data["animations"]]))
        return
    data["events"] = {e: {} for e in v6_anims.EVENTS}
    ok, rep = validate(data, images, base)
    print("\n".join(r for r in rep if not r.startswith("[OK]")) or "all OK", f"({len(rep)} checks)")
    (OUT / "VALIDATION_v6.txt").write_text("\n".join(rep) + "\n")
    if not ok and "--force-export" not in sys.argv:
        print("校验未通过，不导出。")
        sys.exit(1)
    print("导出：", export(data, images))
    if "--no-preview" not in sys.argv:
        print("预览：", previews(data, images))


if __name__ == "__main__":
    main()
