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
# 剑：拳头中心 / 剑尖（原图像素，自动检测剑身 + 握剑手，见提交记录里的标定图）
SWORD = {"sword_rest": ((358.2, 778.4), (807.1, 1263.9)), "sword_ready": ((234.6, 742.9), (821.2, 1378.1)),
         "sword_windup": ((479.9, 132.5), (13.1, 424.6)), "sword_slash": ((637.3, 843.7), (1012.5, 1327.4)),
         "sword_smear": ((665.0, 455.0), (998.0, 92.0))}
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
          "awaken_peak": ("via", "awaken", (0.993, -8.6, 5.6))}


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
        for p in EVOKER_POSES:
            gx, gy = U(self.grip[p], p)
            d["bones"].append({"name": f"v7_ev_{p}", "parent": "v7", "x": round(gx, 2), "y": round(gy, 2)})
        slots = [("v7_ghost", "v7", None), ("v7_body", "v7", None), ("v7_blink", "v7", None), ("v7_old", "v7", None)]
        slots += [(f"v7_ev_{p}", f"v7_ev_{p}", None) for p in EVOKER_POSES]
        slots += [("v7_hand", "v7", None), ("v7_flash", "v7", "additive"), ("v7_glow", "v7", "additive"),
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
            ["sword_smear", "hurt_impact", "hurt", "die_kneel", "die_fall", "die_down"]
        for slot in ("v7_ghost", "v7_body", "v7_old", "v7_flash"):
            att[slot] = {f"v7_{p}": self.region(f"v7_{p}") for p in poses}
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
                # 溶解：新姿势在下面已经完全显示，旧姿势盖在上面从不透明淡到透明 —— 60 帧里每帧都在变，不会一下跳过去
                a.att("v7_old", t, f"v7_{before}")
                a.rgba("v7_old", t, c8(1, 1, 1, 1))
                a.rgba("v7_old", t + fade, c8(1, 1, 1, 0))
                a.att("v7_old", t + fade, None)
                a.att("v7_ghost", t, f"v7_{before}")
                a.rgba("v7_ghost", t, c8(0.3, 0.7, 1.0, 0.3))
                a.rgba("v7_ghost", t + 0.2, c8(0.3, 0.7, 1.0, 0.0))
                a.att("v7_ghost", t + 0.2, None)
                a.att("v7_flash", t, f"v7_{p}")
                a.rgba("v7_flash", t, c8(0.35, 0.85, 1.0, 0.18))
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

    def breathe(self, a, dur, amp=1.6, phase=0.0):
        n = 8
        for i in range(n + 1):
            t = dur * i / n
            a.tr("v7", t, 0, amp * 0.5 * (1 - math.cos(2 * math.pi * (i / n + phase))))

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
        self.breathe(a, d["aim_loop"], amp=-1.2)
        A["aim_loop"] = a

        a = Anim(d["aim_out"])
        self.sequence(a, [(0, "focus"), (0.12, "focus_mid"), (0.3, "idle")])
        A["aim_out"] = a

        def summon(name, seq, T, back_mid, back_idle, prev):
            a = Anim(d[name])
            self.sequence(a, seq + [(T, "awaken_peak"), (T + PEAK, "awaken"), (back_mid, "focus_mid"), (back_idle, "idle")], prev=prev)
            self.fire(a, T, back_mid - T)
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
        self.sequence(a, [(0, "sword_rest"), (0.14, "sword_ready")], prev="idle")
        A["sword_in"] = a

        a = Anim(d["ready_sword_loop"])
        self.sequence(a, [(0, "sword_ready")])
        self.breathe(a, d["ready_sword_loop"], amp=-1.4)
        A["ready_sword_loop"] = a

        a = Anim(d["sword_out"])
        self.sequence(a, [(0, "sword_ready"), (0.16, "sword_rest"), (0.34, "idle")])
        A["sword_out"] = a

        def strike(name, prev, t_slash, t_rest, t_idle):
            a = Anim(d[name])
            # 拖影帧就是"斩下"那一瞬（weapon_slash 事件那一刻），随后落到斩击收招
            self.sequence(a, [(0, "sword_windup"), (t_slash, "sword_smear"), (t_slash + SMEAR, "sword_slash"), (t_rest, "sword_rest"), (t_idle, "idle")], prev=prev)
            # 斩下那一刻向前冲一下再收回（整图位移，不变形）
            for dt, v in ((-0.001, 0.0), (0.0, 1.0), (0.05, 0.75), (0.14, 0.3), (0.26, 0.0)):
                a.tr("v7", t_slash + dt, 18 * S * v, 0)
            return a
        A["attack"] = strike("attack", "idle", 0.08, 0.5, 0.72)
        A["attack_from_ready"] = strike("attack_from_ready", "sword_ready", 0.065, 0.48, 0.7)

        # ── 受击 / 死亡 ──
        a = Anim(d["hurt"])
        self.sequence(a, [(0, "hurt_impact"), (IMPACT, "hurt"), (0.42, "idle")], prev="idle")
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
