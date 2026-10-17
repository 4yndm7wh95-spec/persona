"""牌组 / 弃牌堆 / 消耗堆三个界面的会动立绘（做法同 build_pile_portrait.py：整图切网格、按区域权重绑骨骼、小幅度形变，不拆层）。
· 牌组（art/p3pile_deck/deck_base.png，1254×1254 横躺脸部特写）：呼吸、头微摆、手和头一起动、三处发梢、领结尾巴、眨眼（tools/art/deck_blink.py 画的帧）；
· 弃牌堆（art/p3pile_discard/discard_base.png，后仰下沉）：整个人慢慢下沉又浮回、呼吸、两手在水里漂、领结飘、发梢、眨眼；
· 消耗堆（art/p3pile_exhaust/exhaust_base.png，远处背影）：整体慢慢转着漂、伸出的手探、垂下的手摆、外套下摆和两腿飘。
每个都另有加色的轮廓光（随呼吸明暗）。8 秒无缝循环。
输出：mod/pck_stage/p3makoto/pile_<name>/（Spine）+ p3makoto/ui/pile_<name>.png（静态兜底）+ art/rig_preview/pile_<name>_idle.png。
用法：python3 tools/ui/build_pile_rigs.py [deck|discard|exhaust ...] [--no-preview]"""
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
sys.path.insert(0, str(ROOT / "tools/ui"))
import build_portrait as bp  # noqa: E402
from spine_stage import stage_spine  # noqa: E402

LOOP = 8.0
FPS = 10
TAU = 2 * math.pi


def S(t, period, ph=0.0):
    return math.sin(TAU * t / period + ph)


# ── 每张立绘的配置：画布、骨骼（画布像素坐标）、父骨骼、权重区域（前面的优先，最后一个兜底）、眨眼椭圆、轮廓光方向、动作 ──

def deck_weights(poly):
    hand = poly([(60, 150), (520, 160), (560, 480), (330, 640), (300, 820), (60, 820)], 30)
    hair_top = poly([(330, 0), (1000, 0), (1000, 170), (330, 170)], 30)
    hair_r = poly([(940, 150), (1120, 180), (1110, 470), (960, 470)], 30)
    hair_l = poly([(20, 280), (250, 300), (260, 760), (20, 760)], 30)
    head = poly([(0, 0), (1150, 0), (1110, 520), (1000, 720), (800, 800), (560, 820), (330, 820), (0, 820)], 40)
    tie = poly([(930, 880), (1254, 860), (1254, 1254), (980, 1254)], 30)
    return [("hand", hand * head), ("hair_top", hair_top * head), ("hair_r", hair_r * head), ("hair_l", hair_l * head),
            ("head", head), ("tie", tie), ("chest", None)]


def deck_motion(t):
    return {
        "chest": {"rotate": 0.2 * S(t, 8, 0.3), "y": 2.4 * S(t, 4, -0.4), "sy": 1 + 0.004 * S(t, 4)},
        "head": {"rotate": 0.45 * S(t, 8, 0.9) + 0.12 * S(t, 4, 0.2), "x": 1.4 * S(t, 8, 1.6), "y": 1.8 * S(t, 4, -0.9)},
        "hand": {"rotate": 0.3 * S(t, 8, 2.0), "y": 1.2 * S(t, 4, 0.7)},
        "hair_top": {"rotate": 1.3 * S(t, 4, 0.6) + 0.5 * S(t, 2, 2.4)},
        "hair_r": {"rotate": -1.4 * S(t, 4, 2.1) - 0.5 * S(t, 8 / 3, 1.1)},
        "hair_l": {"rotate": 1.1 * S(t, 4, 1.3) + 0.4 * S(t, 8 / 3, 0.4)},
        "tie": {"rotate": 1.2 * S(t, 4, 2.4) + 0.4 * S(t, 2, 0.8)},
    }


def discard_weights(poly):
    hair_l = poly([(30, 70), (300, 60), (300, 420), (40, 430)], 30)
    hair_top = poly([(250, 0), (560, 0), (560, 130), (250, 130)], 26)
    head = poly([(20, 20), (580, 0), (600, 380), (500, 440), (360, 440), (40, 440)], 34)
    tie = poly([(560, 500), (840, 520), (860, 800), (660, 820), (560, 640)], 26)
    arm_l = poly([(0, 760), (300, 700), (360, 1000), (340, 1450), (60, 1450), (0, 1200)], 34)
    arm_r = poly([(720, 760), (1024, 740), (1024, 1080), (820, 1080), (700, 900)], 30)
    legs = poly([(430, 1050), (1024, 980), (1024, 1536), (430, 1536)], 50)
    return [("hair_l", hair_l * head), ("hair_top", hair_top * head), ("head", head), ("tie", tie), ("arm_l", arm_l),
            ("arm_r", arm_r), ("legs", legs), ("chest", None)]


def discard_motion(t):
    sink = S(t, 8, 0.0)
    return {
        "root": {"x": 3.0 * S(t, 8, 1.2), "y": 10.0 * sink},                       # 慢慢下沉又浮回
        "chest": {"rotate": 0.5 * S(t, 8, 0.7), "y": 2.2 * S(t, 4, -0.4), "sy": 1 + 0.005 * S(t, 4)},
        "head": {"rotate": 0.6 * S(t, 8, 1.5) + 0.15 * S(t, 4, 0.2), "y": 1.6 * S(t, 4, -0.9)},
        "hair_l": {"rotate": 1.6 * S(t, 4, 1.3) + 0.6 * S(t, 8 / 3, 0.4)},
        "hair_top": {"rotate": -1.4 * S(t, 4, 0.6) - 0.5 * S(t, 2, 2.4)},
        "tie": {"rotate": 2.2 * S(t, 4, 2.4) + 0.8 * S(t, 2, 0.8)},
        "arm_l": {"rotate": 1.3 * S(t, 8, 2.2) + 0.4 * S(t, 4, 0.5)},
        "arm_r": {"rotate": -1.2 * S(t, 8, 0.9) - 0.4 * S(t, 4, 1.7)},
        "legs": {"rotate": 0.5 * S(t, 8, 2.8)},
    }


def exhaust_weights(poly):
    arm_up = poly([(540, 330), (720, 330), (690, 600), (580, 640), (540, 560)], 18)
    arm_dn = poly([(350, 690), (460, 690), (470, 1000), (350, 1000)], 16)
    coat_r = poly([(600, 700), (740, 720), (720, 840), (610, 820)], 16)
    coat_l = poly([(420, 760), (500, 760), (500, 880), (420, 880)], 14)
    hair = poly([(360, 460), (520, 460), (520, 540), (360, 540)], 14)
    head = poly([(350, 450), (530, 450), (540, 610), (360, 610)], 18)
    legs_lo = poly([(380, 1040), (600, 1040), (600, 1320), (380, 1320)], 22)
    legs = poly([(400, 840), (650, 840), (640, 1320), (380, 1320)], 26)
    return [("arm_up", arm_up), ("arm_dn", arm_dn), ("coat_r", coat_r), ("coat_l", coat_l), ("hair", hair * head), ("head", head),
            ("legs_lo", legs_lo * legs), ("legs", legs), ("body", None)]


def exhaust_motion(t):
    return {
        "root": {"rotate": 2.0 * S(t, 8, 0.4), "x": 6.0 * S(t, 8, 1.4), "y": 8.0 * S(t, 8, 0.0)},   # 整体慢慢转着漂
        "body": {"rotate": 0.6 * S(t, 4, 0.7), "sy": 1 + 0.005 * S(t, 4)},
        "head": {"rotate": 1.0 * S(t, 8, 1.9)},
        "hair": {"rotate": 2.0 * S(t, 4, 1.3) + 0.6 * S(t, 2, 0.4)},
        "arm_up": {"rotate": 2.4 * S(t, 4, 2.0) + 0.8 * S(t, 8 / 3, 0.9)},
        "arm_dn": {"rotate": -2.0 * S(t, 4, 0.8)},
        "coat_r": {"rotate": 3.0 * S(t, 2, 1.1) + 1.0 * S(t, 4, 0.2)},
        "coat_l": {"rotate": -2.4 * S(t, 2, 2.0)},
        "legs": {"rotate": 1.4 * S(t, 4, 2.6)},
        "legs_lo": {"rotate": 2.2 * S(t, 4, 3.4)},
    }


CONFIG = {
    "deck": {
        "dir": "art/p3pile_deck", "base": "deck_base.png", "half": "deck_eyes_half.png", "closed": "deck_eyes_closed.png",
        "eye": ((606, 456), (62, 50)),
        "joints": {"root": (627, 1254), "chest": (700, 1060), "head": (720, 820), "hand": (300, 760), "hair_top": (660, 170),
                   "hair_r": (960, 300), "hair_l": (250, 500), "tie": (960, 880)},
        "parent": {"chest": "root", "head": "chest", "hand": "head", "hair_top": "head", "hair_r": "head", "hair_l": "head", "tie": "chest"},
        "weights": deck_weights, "motion": deck_motion, "rim": (-9, 7),             # 光从右上方来（脸朝右）
        "blinks": (2.4, 5.9, 6.2),
    },
    "discard": {
        "dir": "art/p3pile_discard", "base": "discard_base.png", "half": "discard_eyes_half.png", "closed": "discard_eyes_closed.png",
        "eye": ((330, 222), (54, 34)),
        "joints": {"root": (600, 1536), "chest": (520, 900), "head": (450, 420), "hair_l": (300, 250), "hair_top": (420, 130),
                   "tie": (580, 520), "arm_l": (220, 700), "arm_r": (720, 760), "legs": (640, 1060)},
        "parent": {"chest": "root", "head": "chest", "hair_l": "head", "hair_top": "head", "tie": "chest", "arm_l": "chest",
                   "arm_r": "chest", "legs": "chest"},
        "weights": discard_weights, "motion": discard_motion, "rim": (9, 9),        # 光从上方水面来
        "blinks": (3.1, 6.6),
    },
    "exhaust": {
        "dir": "art/p3pile_exhaust", "base": "exhaust_base.png", "half": None, "closed": None,
        "eye": None,
        "joints": {"root": (520, 900), "body": (520, 760), "head": (470, 600), "hair": (440, 540), "arm_up": (580, 600),
                   "arm_dn": (420, 700), "coat_r": (610, 740), "coat_l": (460, 780), "legs": (520, 860), "legs_lo": (500, 1050)},
        "parent": {"body": "root", "head": "body", "hair": "head", "arm_up": "body", "arm_dn": "body", "coat_r": "body",
                   "coat_l": "body", "legs": "body", "legs_lo": "legs"},
        "weights": exhaust_weights, "motion": exhaust_motion, "rim": (6, 8),
        "blinks": (),
    },
}


def load(path, W, H):
    """读图；alpha < 6 的零星杂点清成全透明（Codex 报告的 1~3/255 残留），透明像素的颜色换成邻近不透明像素的颜色（避免线性过滤渗出光晕）。"""
    a = np.asarray(Image.open(path).convert("RGBA")).astype(np.float32) / 255
    if a.shape[:2] != (H, W):
        raise ValueError(f"{path}: expected {W}x{H}")
    a[..., 3] = np.where(a[..., 3] < 6 / 255, 0.0, a[..., 3])
    alpha = a[..., 3:]
    num = cv2.GaussianBlur(a[..., :3] * alpha, (0, 0), 6)
    den = cv2.GaussianBlur(alpha, (0, 0), 6)[..., None]
    a[..., :3] = np.where(alpha > 0.02, a[..., :3], num / np.maximum(den, 1e-4))
    a[..., 3] = np.clip(a[..., 3] * 255 / 254, 0, 1)
    return a


def build(name):
    cfg = CONFIG[name]
    src = ROOT / cfg["dir"]
    W, H = Image.open(src / cfg["base"]).size
    bp.W, bp.H = W, H                                   # build_portrait 的网格 / 坐标函数按模块里的画布尺寸算

    def poly(points, blur):
        m = Image.new("L", (W, H), 0)
        ImageDraw.Draw(m).polygon(points, fill=255)
        return cv2.GaussianBlur(np.asarray(m).astype(np.float32) / 255, (0, 0), blur)

    base = load(src / cfg["base"], W, H)
    imgs = {}
    imgs["body"], bb = bp.crop(bp.to_img(base))
    dx, dy = cfg["rim"]
    imgs["body_rim"] = bp.to_img(bp.rim(base, dx=dx, dy=dy, color=(0.5, 0.92, 1.0))).crop(bb)
    eyes = []
    if cfg["eye"]:
        (ex, ey), (rx, ry) = cfg["eye"]
        ys, xs = np.mgrid[0:H, 0:W].astype(np.float32)
        dd = np.hypot((xs - ex) / rx, (ys - ey) / ry)
        m = np.clip((1 - dd) * min(rx, ry) / 8, 0, 1)
        for key in ("half", "closed"):
            im = load(src / cfg[key], W, H)
            im[..., 3] *= m
            imgs[f"eye_{key}"], box = bp.crop(bp.to_img(im))
            eyes.append((f"eye_{key}", box))

    world = {n: bp.sp(p) for n, p in cfg["joints"].items()}
    bones = [{"name": "root", "x": round(world["root"][0], 2), "y": round(world["root"][1], 2)}]
    for n, par in cfg["parent"].items():
        px, py = world[par]
        bones.append({"name": n, "parent": par, "x": round(world[n][0] - px, 2), "y": round(world[n][1] - py, 2)})
    # root 骨骼放在 joints["root"]：mesh 顶点按世界坐标减骨骼世界坐标存，所以 world 里 root 也要是这个位置
    m_body = bp.mesh(imgs["body"], bb, cfg["weights"](poly), world)
    atts = {"body": {"body": m_body}, "body_rim": {"body_rim": dict(m_body, path="body_rim")}}
    head = "head"
    for key, box in eyes:
        atts[key] = {key: bp.region(imgs[key], box, head, world)}
    slots = [{"name": "body", "bone": "root", "attachment": "body"},
             {"name": "body_rim", "bone": "root", "attachment": "body_rim", "blend": "additive"}]
    for key, _ in eyes:
        slots.append({"name": key, "bone": head, "attachment": key, "color": "ffffff00"})
    data = {"skeleton": {"spine": "4.2.43", "images": "./", "audio": "", "x": -W / 2, "y": 0, "width": W, "height": H},
            "bones": bones, "slots": slots, "skins": [{"name": "default", "attachments": atts}], "events": {},
            "animations": {"idle_loop": idle(cfg, bool(eyes))}}
    return data, imgs, base, (W, H)


def idle(cfg, has_eyes):
    n = int(LOOP * FPS)
    times = [round(i / FPS, 4) for i in range(n + 1)]
    tl = {}
    for i, t in enumerate(times):
        tt = 0.0 if i == n else t
        for bone, mv in cfg["motion"](tt).items():
            b = tl.setdefault(bone, {})
            if "rotate" in mv:
                b.setdefault("rotate", []).append({"time": t, "value": round(mv["rotate"], 3)})
            if "x" in mv or "y" in mv:
                b.setdefault("translate", []).append({"time": t, "x": round(mv.get("x", 0.0), 2), "y": round(mv.get("y", 0.0), 2)})
            if "sy" in mv:
                b.setdefault("scale", []).append({"time": t, "x": 1.0, "y": round(mv["sy"], 4)})
    slots = {}
    if has_eyes:
        # 眨眼：半睁先淡入、闭眼叠上去淡入停一下再淡出、半睁淡出（和选人界面一样是淡入淡出）；最后两个挨得近 = 连眨
        curve = [(0.0, 0, 0), (0.04, 1, 0), (0.075, 1, 1), (0.135, 1, 1), (0.18, 1, 0), (0.25, 0, 0)]

        def keys(idx):
            out = [{"time": 0.0, "color": "ffffff00"}]
            for b0 in cfg["blinks"]:
                out += [{"time": round(b0 + c[0], 3), "color": "ffffff%02x" % (255 * c[1 + idx])} for c in curve]
            out.append({"time": LOOP, "color": "ffffff00"})
            return out
        slots["eye_half"] = {"rgba": keys(0)}
        slots["eye_closed"] = {"rgba": keys(1)}
    slots["body_rim"] = {"rgba": [{"time": t, "color": "ffffff%02x" % round(255 * (0.5 + 0.3 * S(0.0 if i == n else t, 4)))}
                                  for i, t in enumerate(times)]}
    return {"bones": tl, "slots": slots}


def main():
    names = [a for a in sys.argv[1:] if not a.startswith("--")] or list(CONFIG)
    from godot_tex import write_texture
    for name in names:
        data, imgs, base, (W, H) = build(name)
        out = ROOT / CONFIG[name]["dir"] / "export"
        out.mkdir(parents=True, exist_ok=True)
        base_name = f"pile_{name}"
        (out / f"{base_name}.spine-json").write_text(json.dumps(data, separators=(",", ":")))
        page, atlas = bp.pack(imgs, base_name)
        page.save(out / f"{base_name}.png")
        (out / f"{base_name}.atlas").write_text(atlas)
        stage_spine(ROOT / "mod/pck_stage", f"p3makoto/{base_name}", base_name, out, f"{base_name}_skel_data", default_mix=0.0)
        write_texture(ROOT / "mod/pck_stage", f"p3makoto/ui/{base_name}.png", bp.to_img(base))
        print(name, "atlas", page.size, "vertices", len(data["skins"][0]["attachments"]["body"]["body"]["uvs"]) // 2)
        if "--no-preview" not in sys.argv:
            from spine_eval import Skeleton
            sk = Skeleton(data, imgs)
            blinks = CONFIG[name]["blinks"]
            ts = [0, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0] + ([blinks[0] + 0.06, blinks[0] + 0.1] if blinks else [1.5, 2.5])
            sc = 340 / max(W, H)
            frames = [sk.render("idle_loop", t, canvas=(W, H), origin=(W / 2, H), bg=(20, 30, 70, 255)).convert("RGB").resize((int(W * sc), int(H * sc)))
                      for t in ts]
            fw, fh = frames[0].size
            sheet = Image.new("RGB", (fw * 5, fh * 2))
            for i, im in enumerate(frames):
                sheet.paste(im, ((i % 5) * fw, (i // 5) * fh))
            (ROOT / "art/rig_preview").mkdir(parents=True, exist_ok=True)
            sheet.save(ROOT / f"art/rig_preview/{base_name}_idle.png")


if __name__ == "__main__":
    main()
