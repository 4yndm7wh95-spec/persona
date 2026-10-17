"""牌堆界面的会动立绘（Codex 出的 art/p3pile/pile_base.png：结城理举着塔罗牌）。做法同选人界面的 build_portrait.py：
一张整图切成网格，按区域权重绑到骨骼上，只做小幅度的形变，所以不用拆层补画。
骨骼：root → chest（呼吸）→ head（微摆）→ hair_l / hair_r / hair_top（发梢飘）
                         → arm（举牌的手和牌，绕肘部轻轻浮动）、arm_r（右臂连臂章，轻摆）
                         → coat_l / coat_r（外套下摆轻摆）、player（耳机线下的随身听，像吊坠一样晃）
插槽（后 → 前）：整图网格、轮廓光（同一网格、加色，左上方打来的青光）、半睁 / 闭眼贴片、牌面扫光 ×5（加色）。
8 秒无缝循环：呼吸 4s、两次眨眼（第二次是连眨）、一次牌面扫光，轮廓光跟着呼吸明暗。
输出：mod/pck_stage/p3makoto/pile/（Spine 资源）+ p3makoto/ui/pile_portrait.png（静态兜底）+ art/rig_preview/pile_idle.png（预览）。
用法：python3 tools/ui/build_pile_portrait.py [--no-preview]"""
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
from build_portrait import crop, mesh, pack, region, rim, sp, to_img  # noqa: E402
from spine_stage import stage_spine  # noqa: E402

SRC = ROOT / "art/p3pile"
OUT = ROOT / "art/p3pile/export"
W, H = 1024, 1536
LOOP = 8.0
FPS = 10
META = json.loads((SRC / "meta.json").read_text())

J = {"root": (512, 1536), "chest": (560, 1300), "head": (590, 520), "hair_l": (420, 160), "hair_r": (690, 130),
     "hair_top": (560, 60), "arm": (90, 1080), "arm_r": (850, 640), "coat_l": (210, 1120), "coat_r": (880, 1020),
     "player": (520, 1000)}
PARENT = {"chest": "root", "head": "chest", "hair_l": "head", "hair_r": "head", "hair_top": "head", "arm": "chest",
          "arm_r": "chest", "coat_l": "chest", "coat_r": "chest", "player": "chest"}
EYE_C = tuple(META["blink_edit"]["ellipse_center"])
EYE_R = tuple(META["blink_edit"]["ellipse_radii"])
CARD = [tuple(META["card_corners"][k]) for k in ("top_left", "top_right", "bottom_right", "bottom_left")]


def load(name):
    """读图并把全透明像素的颜色换成邻近不透明像素的颜色（生成图透明处残留着看不见的杂色，线性过滤时会在边缘渗出光晕）。"""
    a = np.asarray(Image.open(SRC / name).convert("RGBA")).astype(np.float32) / 255
    alpha = a[..., 3:]
    num = cv2.GaussianBlur(a[..., :3] * alpha, (0, 0), 6)
    den = cv2.GaussianBlur(alpha, (0, 0), 6)[..., None]
    fill = num / np.maximum(den, 1e-4)
    a[..., :3] = np.where(alpha > 0.02, a[..., :3], fill)
    a[..., 3] = np.clip(a[..., 3] * 255 / 254, 0, 1)          # 生成图最大 alpha 是 254
    return a


def poly(points, blur):
    m = Image.new("L", (W, H), 0)
    ImageDraw.Draw(m).polygon(points, fill=255)
    return cv2.GaussianBlur(np.asarray(m).astype(np.float32) / 255, (0, 0), blur)


def weights():
    """骨骼权重（前面的优先，最后的 chest 兜底）。区域都按 pile_base.png 的像素坐标画。"""
    arm = poly([(105, 285), (315, 280), (318, 560), (250, 640), (235, 800), (205, 1000), (30, 1010), (45, 760), (62, 560)], 22)
    player = poly([(425, 1290), (545, 1290), (545, 1475), (425, 1475)], 14) * 0.85 \
        + poly([(500, 1000), (580, 1000), (560, 1300), (500, 1300)], 10) * 0.35
    hair_l = poly([(300, 120), (430, 90), (450, 260), (410, 420), (330, 440), (290, 300)], 26)
    hair_r = poly([(690, 40), (800, 80), (800, 290), (720, 300), (690, 180)], 24)
    hair_top = poly([(470, 0), (660, 0), (650, 90), (480, 90)], 20)
    head = poly([(290, 0), (820, 0), (820, 330), (700, 470), (620, 520), (500, 520), (400, 470), (290, 420)], 26)
    arm_r = poly([(790, 620), (1024, 600), (1024, 1150), (820, 1150), (770, 900)], 30)
    coat_l = poly([(0, 1100), (330, 1180), (300, 1536), (0, 1536)], 40)
    coat_r = poly([(830, 1050), (1024, 1000), (1024, 1536), (860, 1536)], 40)
    return [("arm", arm), ("player", np.clip(player, 0, 1)), ("hair_l", hair_l * head), ("hair_r", hair_r * head),
            ("hair_top", hair_top * head), ("head", head), ("arm_r", arm_r), ("coat_l", coat_l), ("coat_r", coat_r), ("chest", None)]


def eye_patch(name):
    """眨眼贴片：只取眼睛周围的椭圆（边缘羽化 8px），其余透明。"""
    im = load(name)
    ys, xs = np.mgrid[0:H, 0:W].astype(np.float32)
    d = np.hypot((xs - EYE_C[0]) / EYE_R[0], (ys - EYE_C[1]) / EYE_R[1])
    m = np.clip((1 - d) * min(EYE_R) / 8, 0, 1)
    im[..., 3] *= m
    return crop(to_img(im))


def build():
    base = load("pile_base.png")
    imgs = {}
    imgs["body"], bb = crop(to_img(base))
    imgs["body_rim"] = to_img(rim(base, dx=9, dy=7, color=(0.5, 0.92, 1.0))).crop(bb)
    imgs["eye_half"], ehb = eye_patch("pile_eyes_half.png")
    imgs["eye_closed"], ecb = eye_patch("pile_eyes_closed.png")

    # 牌面扫光：牌的四边形（去掉手指）× 斜向光带，5 个位置依次亮起
    cm = Image.new("L", (W, H), 0)
    ImageDraw.Draw(cm).polygon(CARD, fill=255)
    card = np.asarray(cm) > 0
    c = base[..., :3]
    skin = (c[..., 0] > 0.8) & (c[..., 1] > 0.8) & (c[..., 2] > 0.8) & (np.abs(c[..., 0] - c[..., 2]) < 0.08)
    navy = c.sum(-1) < 1.2
    card &= ~cv2.dilate((skin & ~navy).astype(np.uint8), np.ones((3, 3), np.uint8)).astype(bool) | navy
    ys, xs = np.mgrid[0:H, 0:W].astype(np.float32)
    gx0, gy0 = min(p[0] for p in CARD), min(p[1] for p in CARD)
    gx1, gy1 = max(p[0] for p in CARD), max(p[1] for p in CARD)
    sweeps = []
    for i in range(5):
        u = (i + 0.5) / 5
        d = (xs - gx0) + (ys - gy0) * 0.6 - u * ((gx1 - gx0) + (gy1 - gy0) * 0.6)
        band = np.exp(-(d / 18) ** 2) + 0.4 * np.exp(-((d - 38) / 7) ** 2)
        a = np.clip(band * card, 0, 1)
        im = np.zeros((H, W, 4), np.float32)
        im[..., :3] = (0.85, 0.97, 1.0)
        im[..., 3] = a
        img, box = crop(to_img(im))
        imgs[f"sweep{i}"] = img
        sweeps.append(box)

    world = {n: sp(p) for n, p in J.items()}
    bones = [{"name": "root"}]
    for n in PARENT:
        px, py = world[PARENT[n]]
        bones.append({"name": n, "parent": PARENT[n], "x": round(world[n][0] - px, 2), "y": round(world[n][1] - py, 2)})
    m_body = mesh(imgs["body"], bb, weights(), world)
    skin_atts = {
        "body": {"body": m_body}, "body_rim": {"body_rim": dict(m_body, path="body_rim")},
        "eye_half": {"eye_half": region(imgs["eye_half"], ehb, "head", world)},
        "eye_closed": {"eye_closed": region(imgs["eye_closed"], ecb, "head", world)},
    }
    for i, box in enumerate(sweeps):
        skin_atts[f"sweep{i}"] = {f"sweep{i}": region(imgs[f"sweep{i}"], box, "arm", world)}
    names = ["body", "body_rim", "eye_half", "eye_closed"] + [f"sweep{i}" for i in range(5)]
    bone_of = {"body": "root", "body_rim": "root", "eye_half": "head", "eye_closed": "head", **{f"sweep{i}": "arm" for i in range(5)}}
    slots = []
    for s in names:
        e = {"name": s, "bone": bone_of[s], "attachment": s}
        if s == "body_rim" or s.startswith("sweep"):
            e["blend"] = "additive"
        if s.startswith("eye") or s.startswith("sweep"):
            e["color"] = "ffffff00"
        slots.append(e)
    data = {"skeleton": {"spine": "4.2.43", "images": "./", "audio": "", "x": -512, "y": 0, "width": 1024, "height": 1536},
            "bones": bones, "slots": slots, "skins": [{"name": "default", "attachments": skin_atts}], "events": {},
            "animations": {"idle_loop": idle()}}
    return data, imgs, base


def idle():
    tau = 2 * math.pi
    n = int(LOOP * FPS)
    times = [round(i / FPS, 4) for i in range(n + 1)]
    S = lambda t, period, ph=0.0: math.sin(tau * t / period + ph)

    bones = {b: {"rotate": []} for b in ("hair_l", "hair_r", "hair_top", "arm_r", "coat_l", "coat_r", "player")}
    bones["chest"] = {"rotate": [], "scale": [], "translate": []}
    bones["head"] = {"rotate": [], "translate": []}
    bones["arm"] = {"rotate": [], "translate": []}
    bones["player"]["translate"] = []
    for i, t in enumerate(times):
        tt = 0.0 if i == n else t
        bones["chest"]["rotate"].append({"time": t, "value": round(0.2 * S(tt, 8, 0.3), 3)})
        bones["chest"]["scale"].append({"time": t, "x": 1.0, "y": round(1 + 0.005 * S(tt, 4), 4)})
        bones["chest"]["translate"].append({"time": t, "x": 0.0, "y": round(2.2 * S(tt, 4, -0.4), 2)})
        bones["head"]["rotate"].append({"time": t, "value": round(0.4 * S(tt, 8, 0.9) + 0.12 * S(tt, 4, 0.2), 3)})
        bones["head"]["translate"].append({"time": t, "x": round(1.2 * S(tt, 8, 1.6), 2), "y": round(1.6 * S(tt, 4, -0.9), 2)})
        # 发梢：水里的慢飘（两个周期叠加，左右相位错开）
        bones["hair_l"]["rotate"].append({"time": t, "value": round(1.3 * S(tt, 4, 1.3) + 0.5 * S(tt, 8 / 3, 0.4), 3)})
        bones["hair_r"]["rotate"].append({"time": t, "value": round(-1.1 * S(tt, 4, 2.1) - 0.4 * S(tt, 2, 1.1), 3)})
        bones["hair_top"]["rotate"].append({"time": t, "value": round(1.6 * S(tt, 4, 0.6) + 0.6 * S(tt, 2, 2.4), 3)})
        # 举牌的手：绕肘部轻浮（牌那里最多移动约 6px）
        bones["arm"]["rotate"].append({"time": t, "value": round(0.35 * S(tt, 8, 2.2) + 0.12 * S(tt, 4, 0.7), 3)})
        bones["arm"]["translate"].append({"time": t, "x": round(2.0 * S(tt, 8, 1.4), 2), "y": round(2.6 * S(tt, 4, 0.5), 2)})
        bones["arm_r"]["rotate"].append({"time": t, "value": round(-0.3 * S(tt, 8, 0.2), 3)})
        bones["coat_l"]["rotate"].append({"time": t, "value": round(0.5 * S(tt, 4, 1.8) + 0.2 * S(tt, 8, 0.3), 3)})
        bones["coat_r"]["rotate"].append({"time": t, "value": round(-0.45 * S(tt, 4, 2.6) - 0.2 * S(tt, 8, 1.0), 3)})
        # 随身听：吊坠式的晃动，比身体慢半拍
        bones["player"]["rotate"].append({"time": t, "value": round(0.9 * S(tt, 4, 2.4), 3)})
        bones["player"]["translate"].append({"time": t, "x": round(2.2 * S(tt, 4, 2.0), 2), "y": 0.0})

    # 眨眼（和选人界面一样是淡入淡出，不是硬切）：半睁贴片先淡入，闭眼贴片叠在上面淡入、停一下、淡出，最后半睁淡出。
    # 时间线线性插值，每次约 0.25s；6.2s 那次是连眨
    blinks = (2.6, 6.2, 6.5)
    curve = [(0.0, 0, 0), (0.04, 1, 0), (0.075, 1, 1), (0.135, 1, 1), (0.18, 1, 0), (0.25, 0, 0)]

    def keys(idx):
        out = [{"time": 0.0, "color": "ffffff00"}]
        for b0 in blinks:
            out += [{"time": round(b0 + c[0], 3), "color": "ffffff%02x" % (255 * c[1 + idx])} for c in curve]
        out.append({"time": LOOP, "color": "ffffff00"})
        return out
    slots = {"eye_half": {"rgba": keys(0)}, "eye_closed": {"rgba": keys(1)}}

    def hexa(a):
        return "ffffff%02x" % round(255 * max(0.0, min(1.0, a)))
    slots["body_rim"] = {"rgba": [{"time": t, "color": hexa(0.5 + 0.3 * S(0.0 if i == n else t, 4, 0.0))} for i, t in enumerate(times)]}
    for k in range(5):
        t0 = 4.0 + 0.06 * k
        slots[f"sweep{k}"] = {"rgba": [{"time": 0.0, "color": hexa(0)}, {"time": round(t0, 3), "color": hexa(0)},
                                       {"time": round(t0 + 0.06, 3), "color": hexa(0.85)}, {"time": round(t0 + 0.16, 3), "color": hexa(0)},
                                       {"time": LOOP, "color": hexa(0)}]}
    return {"bones": bones, "slots": slots}


def main():
    data, imgs, base = build()
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / "pile.spine-json").write_text(json.dumps(data, separators=(",", ":")))
    page, atlas = pack(imgs, "pile")
    page.save(OUT / "pile.png")
    (OUT / "pile.atlas").write_text(atlas)
    stage_spine(ROOT / "mod/pck_stage", "p3makoto/pile", "pile", OUT, "pile_skel_data", default_mix=0.0)
    from godot_tex import write_texture
    write_texture(ROOT / "mod/pck_stage", "p3makoto/ui/pile_portrait.png", to_img(base))
    print("pile atlas", page.size, "vertices", len(data["skins"][0]["attachments"]["body"]["body"]["uvs"]) // 2)
    if "--no-preview" not in sys.argv:
        from spine_eval import Skeleton
        sk = Skeleton(data, imgs)
        frames = []
        for t in [0, 1.0, 2.0, 2.62, 2.68, 3.0, 4.1, 4.18, 5.0, 6.0]:
            im = sk.render("idle_loop", t, canvas=(1024, 1536), origin=(512, 1536), bg=(20, 30, 70, 255)).convert("RGB")
            frames.append(im.resize((341, 512)))
        sheet = Image.new("RGB", (341 * 5, 512 * 2))
        for i, im in enumerate(frames):
            sheet.paste(im, ((i % 5) * 341, (i // 5) * 512))
        (ROOT / "art/rig_preview").mkdir(parents=True, exist_ok=True)
        sheet.save(ROOT / "art/rig_preview/pile_idle.png")


if __name__ == "__main__":
    main()
