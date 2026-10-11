"""选人界面的会动立绘（阶段 11：手持碎镜片）。分层见 v11_layers.py。
骨骼：root → chest（呼吸）→ head（微摆）→ hair_l / hair_r（发梢飘动）、arm（手 + 镜片，跟着头、自己再轻轻浮动）。
插槽（后 → 前）：底层网格、底层轮廓光（同一网格、加色）、闭眼贴片（脸）、下眼蓝光、
              手层、手层轮廓光、闭眼贴片（镜中）、镜中眼蓝光、镜面扫光 ×5。
光影都在游戏里实时叠加（加色插槽 + 透明度动画），不是画死在贴图上：轮廓光呼吸、两只眼睛泛光、镜面定时扫过一道高光。
手层最多移开 REVEAL 像素（v11_layers 只在这一圈补了画），所以手的浮动幅度限制在这个范围内。
用法：python3 tools/ui/build_portrait.py [--no-preview]"""
import json
import math
import sys
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools/rig"))
sys.path.insert(0, str(ROOT / "tools/pck"))
from spine_stage import stage_spine  # noqa: E402

L = ROOT / "art/v11/layers"
OUT = ROOT / "art/portrait/export"
W, H = 1024, 1536
STEP = 24
TEX = 0.8                                   # 贴图缩放（网格坐标不变）
LOOP = 6.0
FPS = 10                                    # 微动很慢：每 0.1s 一个关键帧 + 线性插值就足够平滑

J = {"root": (512, 1536), "chest": (560, 1400), "head": (800, 860), "hair_l": (230, 330), "hair_r": (900, 330),
     "arm": (-40, 700)}
PARENT = {"chest": "root", "head": "chest", "hair_l": "head", "hair_r": "head", "arm": "head"}
EYE_LO, EYE_HI = (425, 645), (552, 338)     # 露出的眼睛 / 镜中的眼睛（底图像素）
GLASS = [(440, 250), (560, 165), (800, 185), (720, 330), (620, 470), (520, 482), (450, 420)]


def sp(p):
    return (p[0] - W / 2, H - p[1])


def load(name):
    return np.asarray(Image.open(L / name).convert("RGBA")).astype(np.float32) / 255


CUT = ((20, 0), (330, H))                   # 左侧斜切线（画布坐标，上 → 下）；CharSelectBg.cs 的斜条沿同一条线


def edge_fade():
    """左边按斜线硬切（P3R 式大斜条压在切口上，不用虚化）；下边、右边出屏，不处理。"""
    ys, xs = np.mgrid[0:H, 0:W].astype(np.float32)
    (x0, y0), (x1, y1) = CUT
    edge_x = x0 + (x1 - x0) * (ys - y0) / (y1 - y0)
    return np.clip((xs - edge_x) / 1.5 + 0.5, 0, 1)


def to_img(a):
    return Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8), "RGBA")


def rim(layer, dx=-9, dy=7, color=(0.45, 0.9, 1.0)):
    """轮廓光：从右上方身后打来的青色边光 —— alpha 减去往左下平移后的自身 alpha。"""
    a = layer[..., 3]
    M = np.float32([[1, 0, dx], [0, 1, dy]])
    shifted = cv2.warpAffine(a, M, (W, H))
    edge = np.clip(a - shifted, 0, 1)
    edge = cv2.GaussianBlur(edge, (0, 0), 2.2) * a
    out = np.zeros_like(layer)
    out[..., :3] = color
    out[..., 3] = np.clip(edge * 1.6, 0, 1)
    return out


def glow_dot(size=200, stretch=3.2):
    ys, xs = np.mgrid[0:size, 0:size].astype(np.float32) - size / 2
    r = np.hypot(xs / stretch, ys)
    core = np.exp(-(np.hypot(xs, ys) / 9) ** 2)
    halo = np.exp(-(np.hypot(xs * 0.8, ys) / 30) ** 2) * 0.5
    streak = np.exp(-(ys / 2.5) ** 2) * np.exp(-np.abs(xs) / 60) * 0.8
    a = np.clip(core + halo + streak * (r < size), 0, 1)
    col = np.stack([0.35 + 0.65 * core, 0.78 + 0.22 * core, np.ones_like(core)], -1)
    return to_img(np.dstack([col, a]))


def crop(img):
    a = np.asarray(img.getchannel("A")) > 3
    ys, xs = np.nonzero(a)
    if len(xs) == 0:
        return Image.new("RGBA", (4, 4), (0, 0, 0, 0)), (600, 300, 604, 304)
    box = (int(max(xs.min() - 2, 0)), int(max(ys.min() - 2, 0)), int(min(xs.max() + 3, W)), int(min(ys.max() + 3, H)))
    return img.crop(box), box


def weights_under():
    ys, xs = np.mgrid[0:H, 0:W].astype(np.float32)

    def poly(points, blur):
        m = Image.new("L", (W, H), 0)
        ImageDraw.Draw(m).polygon(points, fill=255)
        return cv2.GaussianBlur(np.asarray(m).astype(np.float32) / 255, (0, 0), blur)
    head = poly([(0, 0), (W, 0), (W, 720), (930, 790), (840, 880), (700, 900), (470, 880), (330, 930), (0, 960)], 30)
    hair_l = poly([(0, 0), (260, 0), (300, 180), (200, 420), (120, 700), (0, 760)], 45)
    hair_r = poly([(840, 160), (W, 140), (W, 640), (900, 650), (860, 420)], 40)
    return [("hair_l", hair_l * head), ("hair_r", hair_r * head), ("head", head), ("chest", None)]


def mesh(img, box, order, world):
    a = np.asarray(img.getchannel("A")) > 3
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
    names = list(world)
    uvs, verts = [], []
    for (x, y) in verts_px:
        uvs += [round(x / w, 5), round(y / h, 5)]
        px, py = x + box[0], y + box[1]
        xi, yi = min(max(int(px), 0), W - 1), min(max(int(py), 0), H - 1)
        rem, ws = 1.0, []
        for b, m in order[:-1]:
            v = float(m[yi, xi]) * rem
            if v > 1e-3:
                ws.append((b, v))
                rem -= v
        ws.append((order[-1][0], rem))
        ws = [(b, v) for b, v in sorted(ws, key=lambda t: -t[1])[:4] if v > 1e-3]
        s = sum(v for _, v in ws)
        sx, sy = sp((px, py))
        verts.append(len(ws))
        for b, v in ws:
            bx, by = world[b]
            verts += [names.index(b), round(sx - bx, 2), round(sy - by, 2), round(v / s, 4)]
    return {"type": "mesh", "uvs": uvs, "triangles": tris, "vertices": verts, "hull": 0, "width": w, "height": h}


def region(img, box, bone, world):
    cx, cy = sp((box[0] + img.width / 2, box[1] + img.height / 2))
    bx, by = world[bone]
    return {"x": round(cx - bx, 2), "y": round(cy - by, 2), "width": img.width, "height": img.height}


def build():
    fade = edge_fade()[..., None]
    under = load("under.png")
    hand = load("hand.png")
    under[..., 3:] *= fade
    hand[..., 3:] *= fade
    imgs = {}
    imgs["under"], ub = crop(to_img(under))
    imgs["under_rim"] = to_img(rim(under)).crop(ub)
    imgs["hand"], hb = crop(to_img(hand))
    imgs["hand_rim"] = to_img(rim(hand)).crop(hb)
    bf, bfb = crop(Image.open(L / "blink_face.png"))
    bg, bgb = crop(Image.open(L / "blink_glass.png"))
    imgs["blink_face"], imgs["blink_glass"] = bf, bg
    imgs["eye_glow"] = glow_dot()

    # 镜面扫光：镜片区域（去掉手指的肤色）× 斜向光带，5 个位置依次亮起 = 一道光扫过
    gm = Image.new("L", (W, H), 0)
    ImageDraw.Draw(gm).polygon(GLASS, fill=255)
    glass = (np.asarray(gm) > 0) & (hand[..., 3] > 0.5)
    c = hand[..., :3]
    skin = (c[..., 0] > 0.72) & (c[..., 1] > 0.58) & (c[..., 0] - c[..., 2] > 0.06)
    glass &= ~cv2.dilate(skin.astype(np.uint8), np.ones((5, 5), np.uint8)).astype(bool)
    ys, xs = np.mgrid[0:H, 0:W].astype(np.float32)
    gx0, gy0 = min(p[0] for p in GLASS), min(p[1] for p in GLASS)
    gx1, gy1 = max(p[0] for p in GLASS), max(p[1] for p in GLASS)
    sweeps = []
    for i in range(5):
        u = (i + 0.5) / 5
        d = (xs - gx0) + (ys - gy0) * 0.8 - u * ((gx1 - gx0) + (gy1 - gy0) * 0.8)
        band = np.exp(-(d / 22) ** 2) + 0.35 * np.exp(-((d - 46) / 9) ** 2)
        a = np.clip(band * glass, 0, 1)
        im = np.zeros((H, W, 4), np.float32)
        im[..., :3] = (0.85, 0.97, 1.0)
        im[..., 3] = a
        img, box = crop(to_img(im))
        print("sweep", i, img.size, int(a.sum()))
        imgs[f"sweep{i}"] = img
        sweeps.append(box)

    world = {n: sp(p) for n, p in J.items()}
    bones = [{"name": "root"}]
    for n in ("chest", "head", "hair_l", "hair_r", "arm"):
        px, py = world[PARENT[n]]
        bones.append({"name": n, "parent": PARENT[n], "x": round(world[n][0] - px, 2), "y": round(world[n][1] - py, 2)})
    order = weights_under()
    m_under = mesh(imgs["under"], ub, order, world)
    m_rim = dict(m_under, path="under_rim")
    hand_order = [("arm", None)]
    m_hand = mesh(imgs["hand"], hb, hand_order, world)
    m_hand_rim = dict(m_hand, path="hand_rim")
    eye_lo = {"x": round(sp(EYE_LO)[0] - world["head"][0], 2), "y": round(sp(EYE_LO)[1] - world["head"][1], 2),
              "width": 200, "height": 200, "scaleX": 0.9, "scaleY": 0.9, "rotation": 28}
    eye_hi = {"x": round(sp(EYE_HI)[0] - world["arm"][0], 2), "y": round(sp(EYE_HI)[1] - world["arm"][1], 2),
              "width": 200, "height": 200, "scaleX": 0.8, "scaleY": 0.8, "rotation": 28}
    skin_atts = {
        "under": {"under": m_under}, "under_rim": {"under_rim": m_rim},
        "blink_face": {"blink_face": region(bf, bfb, "head", world)},
        "eye_lo": {"eye_glow": eye_lo},
        "hand": {"hand": m_hand}, "hand_rim": {"hand_rim": m_hand_rim},
        "blink_glass": {"blink_glass": region(bg, bgb, "arm", world)},
        "eye_hi": {"eye_glow": eye_hi},
    }
    for i, box in enumerate(sweeps):
        skin_atts[f"sweep{i}"] = {f"sweep{i}": region(imgs[f"sweep{i}"], box, "arm", world)}
    slot_bone = {"under": "root", "under_rim": "root", "blink_face": "head", "eye_lo": "head", "hand": "root", "hand_rim": "root",
                 "blink_glass": "arm", "eye_hi": "arm", **{f"sweep{i}": "arm" for i in range(5)}}
    additive = {"under_rim", "hand_rim", "eye_lo", "eye_hi", *[f"sweep{i}" for i in range(5)]}
    default = {"under": "under", "under_rim": "under_rim", "blink_face": "blink_face", "eye_lo": "eye_glow", "hand": "hand",
               "hand_rim": "hand_rim", "blink_glass": "blink_glass", "eye_hi": "eye_glow", **{f"sweep{i}": f"sweep{i}" for i in range(5)}}
    slots = []
    for s in ["under", "under_rim", "blink_face", "eye_lo", "hand", "hand_rim", "blink_glass", "eye_hi"] + [f"sweep{i}" for i in range(5)]:
        e = {"name": s, "bone": slot_bone[s], "attachment": default[s]}
        if s in additive:
            e["blend"] = "additive"
        if s.startswith("blink") or s.startswith("sweep"):
            e["color"] = "ffffff00"
        slots.append(e)
    data = {"skeleton": {"spine": "4.2.43", "images": "./", "audio": "", "x": -512, "y": 0, "width": 1024, "height": 1536},
            "bones": bones, "slots": slots, "skins": [{"name": "default", "attachments": skin_atts}], "events": {},
            "animations": {"idle_loop": idle()}}
    return data, imgs


def idle():
    """6 秒无缝循环：呼吸（3s）、头微摆、发梢飘、手和镜片浮动、两次眨眼、一次镜面扫光、轮廓光和眼光呼吸。"""
    tau = 2 * math.pi
    n = int(LOOP * FPS)
    times = [round(i / FPS, 4) for i in range(n + 1)]
    S = lambda t, period, ph=0.0: math.sin(tau * t / period + ph)
    blinks = (2.3, 4.95)

    def blink_a(t):
        v = 0.0
        for b in blinks:
            if b <= t < b + 0.05:
                v = (t - b) / 0.05
            elif b + 0.05 <= t < b + 0.11:
                v = 1.0
            elif b + 0.11 <= t < b + 0.2:
                v = 1 - (t - b - 0.11) / 0.09
        return v

    bones = {"chest": {"rotate": [], "scale": [], "translate": []}, "head": {"rotate": [], "translate": []},
             "hair_l": {"rotate": []}, "hair_r": {"rotate": []}, "arm": {"rotate": [], "translate": []}}
    for i, t in enumerate(times):
        tt = 0.0 if i == n else t
        bones["chest"]["rotate"].append({"time": t, "value": round(0.25 * S(tt, 6, 0.3), 3)})
        bones["chest"]["scale"].append({"time": t, "x": 1.0, "y": round(1 + 0.006 * S(tt, 3), 4)})
        bones["chest"]["translate"].append({"time": t, "x": 0.0, "y": round(2.0 * S(tt, 3, -0.4), 2)})
        bones["head"]["rotate"].append({"time": t, "value": round(0.35 * S(tt, 6, 0.9) + 0.12 * S(tt, 2, 0.2), 3)})
        bones["head"]["translate"].append({"time": t, "x": round(1.5 * S(tt, 6, 1.6), 2), "y": round(1.8 * S(tt, 3, -0.9), 2)})
        bones["hair_l"]["rotate"].append({"time": t, "value": round(1.1 * S(tt, 3, 1.3) + 0.5 * S(tt, 2, 0.4) + 0.25 * S(tt, 1.5, 2.0), 3)})
        bones["hair_r"]["rotate"].append({"time": t, "value": round(-0.9 * S(tt, 3, 2.1) - 0.4 * S(tt, 2, 1.1), 3)})
        bones["arm"]["rotate"].append({"time": t, "value": round(0.45 * S(tt, 6, 2.2) + 0.15 * S(tt, 2, 0.7), 3)})
        bones["arm"]["translate"].append({"time": t, "x": round(4.0 * S(tt, 6, 1.4), 2), "y": round(3.0 * S(tt, 3, 0.5), 2)})
    slots = {}
    blink_keys = [{"time": t, "color": "ffffff%02x" % round(255 * blink_a(0.0 if i == n else t))} for i, t in enumerate(times)]
    # 眨眼的关键帧要细一些（0.05s 闭合）：在眨眼附近补关键帧
    extra = sorted({round(b + d, 4) for b in blinks for d in (0, 0.05, 0.11, 0.2)})
    blink_keys = sorted({k["time"]: k for k in blink_keys + [{"time": t, "color": "ffffff%02x" % round(255 * blink_a(t))} for t in extra]}.values(),
                        key=lambda k: k["time"])
    slots["blink_face"] = {"rgba": blink_keys}
    slots["blink_glass"] = {"rgba": blink_keys}

    def hexa(a, base="ffffff"):
        return base + "%02x" % round(255 * max(0.0, min(1.0, a)))
    for s, amp, ph in (("eye_lo", 0.0, 0.0), ("eye_hi", 0.0, 1.2)):
        slots[s] = {"rgba": [{"time": k["time"], "color": hexa((0.62 + 0.25 * S(0.0 if k["time"] >= LOOP else k["time"], 3, ph))
                                                                  * (1 - int(k["color"][6:8], 16) / 255))} for k in blink_keys]}
    for s, ph in (("under_rim", 0.0), ("hand_rim", 0.8)):
        slots[s] = {"rgba": [{"time": t, "color": hexa(0.55 + 0.3 * S(0.0 if i == n else t, 3, ph))} for i, t in enumerate(times)]}
    # 镜面扫光：3.4s 起，5 道光带依次亮灭（每道 0.12s，间隔 0.06s）
    for k in range(5):
        t0 = 3.4 + 0.06 * k
        slots[f"sweep{k}"] = {"rgba": [{"time": 0.0, "color": hexa(0)}, {"time": round(t0, 3), "color": hexa(0)},
                                       {"time": round(t0 + 0.06, 3), "color": hexa(0.9)}, {"time": round(t0 + 0.16, 3), "color": hexa(0)},
                                       {"time": LOOP, "color": hexa(0)}]}
    return {"bones": bones, "slots": slots}


def pack(imgs, name):
    items = []
    for k, im in imgs.items():
        s = TEX if im.width * im.height > 120 * 120 else 1.0
        items.append((k, im.resize((max(1, round(im.width * s)), max(1, round(im.height * s))), Image.LANCZOS)))
    items.sort(key=lambda kv: -kv[1].height)
    width, pad = 2048, 2
    x = y = shelf = 0
    pos = {}
    for k, im in items:
        if x + im.width + pad > width:
            x, y, shelf = 0, y + shelf + pad, 0
        pos[k] = (x, y)
        x += im.width + pad
        shelf = max(shelf, im.height)
    h = 1
    while h < y + shelf:
        h *= 2
    page = Image.new("RGBA", (width, h), (0, 0, 0, 0))
    lines = [f"{name}.png", f"size:{width},{h}", "filter:Linear,Linear", "pma:false"]
    for k, im in items:
        page.paste(im, pos[k])
        lines += [k, f"bounds:{pos[k][0]},{pos[k][1]},{im.width},{im.height}"]
    return page, "\n".join(lines) + "\n"


def full():
    """胜利界面用：不切边的整张（在水之圆里按圆形裁切、放大到盖满整个圆，画布边缘永远不露出来）。"""
    under = load("under.png")
    hand = load("hand.png")
    out = under[..., :3] * (1 - hand[..., 3:]) + hand[..., :3] * hand[..., 3:]
    return to_img(np.dstack([out, np.maximum(under[..., 3], hand[..., 3])]))


def flat(data, imgs):
    """静态版（残影 / 光晕 / 兜底用）：底层 + 手层，左侧斜切。"""
    under = load("under.png")
    hand = load("hand.png")
    out = under[..., :3] * (1 - hand[..., 3:]) + hand[..., :3] * hand[..., 3:]
    a = np.maximum(under[..., 3], hand[..., 3]) * edge_fade()
    return to_img(np.dstack([out, a]))


def main():
    data, imgs = build()
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / "portrait.spine-json").write_text(json.dumps(data, separators=(",", ":")))
    page, atlas = pack(imgs, "portrait")
    page.save(OUT / "portrait.png")
    (OUT / "portrait.atlas").write_text(atlas)
    stage_spine(ROOT / "mod/pck_stage", "p3makoto/portrait", "portrait", OUT, "portrait_skel_data", default_mix=0.0)
    from godot_tex import write_texture
    f = flat(data, imgs)
    f.save(ROOT / "art/ui/charselect_portrait.png")
    write_texture(ROOT / "mod/pck_stage", "p3makoto/ui/charselect_portrait.png", f)
    write_texture(ROOT / "mod/pck_stage", "p3makoto/ui/victory_portrait.png", full())
    print("portrait atlas", page.size, "vertices", len(data["skins"][0]["attachments"]["under"]["under"]["uvs"]) // 2)
    if "--no-preview" not in sys.argv:
        from spine_eval import Skeleton
        sk = Skeleton(data, imgs)
        frames = []
        for t in [0, 1.0, 2.0, 2.35, 3.0, 3.5, 3.56, 3.62, 4.5, 5.0]:
            im = sk.render("idle_loop", t, canvas=(1024, 1536), origin=(512, 1536), bg=(20, 30, 70, 255)).convert("RGB")
            frames.append(im.resize((341, 512)))
        sheet = Image.new("RGB", (341 * 5, 512 * 2))
        for i, im in enumerate(frames):
            sheet.paste(im, ((i % 5) * 341, (i // 5) * 512))
        sheet.save(ROOT / "art/rig_preview/portrait_idle.png")


if __name__ == "__main__":
    main()
