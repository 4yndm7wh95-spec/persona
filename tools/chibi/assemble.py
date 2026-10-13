"""Q 版零件拼装：把 art/chibi/parts2（重画的零件，任意大小）按目标框放回 design_a 的画布坐标，和 parts 里能用的零件一起叠成整身。
目标框：默认取 art/chibi/parts/<同名>.png（第一批裁切零件）的不透明包围盒；PLACE 里可以手动覆盖 (cx, cy, 高度, 是否水平翻转)。
输出 art/chibi/rig_src/<name>.png（2048 画布、已对齐）和 art/chibi/rig_src/_assembled.png / _compare.png。"""
import sys
from pathlib import Path
from PIL import Image
ROOT = Path(__file__).resolve().parents[2]
P1, P2, OUT = ROOT / "art/chibi/parts", ROOT / "art/chibi/parts2", ROOT / "art/chibi/rig_src"
W = 2048
# 名称: (中心x, 中心y, 目标高度, 水平翻转, 旋转角度°(逆时针为正))。没列出的 = 用第一批零件的包围盒
PLACE = {
    "head": (1050, 655, 610, True, 0),
    "hair_back": (1020, 595, 690, False, 0),
    "hair_front": (1022, 597, 690, False, 0),
    "eye_open": (1250, 718, 150, False, 0),
    "torso": (1062, 1095, 440, False, 0),
    "hips": (1085, 1345, 205, False, 0),
    "leg_l_upper": (935, 1470, 340, False, -8),
    "leg_r_upper": (1160, 1470, 340, False, 0),
    "arm_l_upper": (835, 1140, 290, False, -12),
    "arm_l_lower": (800, 1295, 190, False, -38),
    "hand_l_open": (815, 1420, 150, False, 0),
    "holster": (930, 1395, 210, False, 0),
    "evoker_holstered": (950, 1305, 120, False, 0),
    "coat_tail_back": (860, 1320, 230, False, 0),
    "coat_tail_front": (1065, 1300, 170, False, 0),
}
ORDER = ["hair_back", "leg_l_lower", "leg_l_upper", "arm_l_upper", "arm_l_lower", "hand_l_open", "coat_tail_back",
         "leg_r_lower", "leg_r_upper", "hips", "holster", "evoker_holstered", "torso", "coat_tail_front", "arm_r_pocket",
         "head", "eye_open", "mouth_neutral", "headphones", "hair_front"]


def load(name):
    p2 = P2 / f"{name}.png"
    return (Image.open(p2 if p2.exists() else P1 / f"{name}.png").convert("RGBA"), p2.exists())


def place(name):
    img, redrawn = load(name)
    if not redrawn and name not in PLACE:
        return img
    part = img.crop(img.getbbox())
    if name in PLACE:
        cx, cy, h, flip, rot = PLACE[name]
    else:
        x0, y0, x1, y1 = Image.open(P1 / f"{name}.png").getbbox()
        cx, cy, h, flip, rot = (x0 + x1) / 2, (y0 + y1) / 2, y1 - y0, False, 0
    if flip:
        part = part.transpose(Image.FLIP_LEFT_RIGHT)
    if rot:
        part = part.rotate(rot, resample=Image.BICUBIC, expand=True)
        part = part.crop(part.getbbox())
    k = h / part.height
    part = part.resize((max(1, round(part.width * k)), max(1, round(h))), Image.LANCZOS)
    canvas = Image.new("RGBA", (W, W))
    canvas.alpha_composite(part, (round(cx - part.width / 2), round(cy - part.height / 2)))
    return canvas


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    full = Image.new("RGBA", (W, W))
    for n in ORDER:
        c = place(n)
        c.save(OUT / f"{n}.png")
        full.alpha_composite(c)
    full.save(OUT / "_assembled.png")
    design = Image.open(ROOT / "art/chibi/design/design_a.png").convert("RGBA")
    cmp = Image.new("RGBA", (W * 2, W), (60, 64, 80, 255))
    cmp.alpha_composite(design, (0, 0))
    cmp.alpha_composite(full, (W, 0))
    cmp.save(OUT / "_compare.png")


if __name__ == "__main__":
    main()
