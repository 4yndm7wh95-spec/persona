"""主题特效贴图（art/fx 里生图得到的素材）→ mod/pck_stage/p3makoto/fx/*.png（+ .import / .ctex）。

- 按不透明区域裁掉多余的透明边，再缩到游戏里实际用到的大小（约 2 倍显示尺寸，留清晰度）。
- 天鹅绒门：门框缩到 512×768；门板裁掉斜顶，缩到正好填满门框开口（开口坐标见 ThemeFx.Door*）；电梯格栅缩到开口大小。
- 全书：从书脊切成左右两半（左半在开书动画里绕书脊翻转）。
用法：python3 tools/ui/build_fx_assets.py
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools/pck"))
from godot_tex import write_texture  # noqa: E402

SRC = ROOT / "art/fx"
STAGE = ROOT / "mod/pck_stage"
REL = "p3makoto/fx"


def trim(im: Image.Image) -> Image.Image:
    a = np.asarray(im)[..., 3]
    ys, xs = np.where(a > 24)
    return im.crop((xs.min(), ys.min(), xs.max() + 1, ys.max() + 1))


def by_height(im: Image.Image, h: int) -> Image.Image:
    return im.resize((max(1, round(im.width * h / im.height)), h), Image.LANCZOS)


def out(name: str, im: Image.Image):
    write_texture(STAGE, f"{REL}/{name}.png", im)
    print(f"{name}.png {im.size}")


def main():
    load = lambda n: Image.open(SRC / f"{n}.png").convert("RGBA")
    # 天鹅绒门：门框原图 1024×1536，缩一半；开口（原图）x 277–747、y 362–1500 → 缩后 x 138–374、y 181–750
    frame = load("velvet_door_frame").resize((512, 768), Image.LANCZOS)
    out("door_frame", frame)
    ow, oh = 374 - 138, 750 - 181
    leaf = load("velvet_door_leaf").crop((59, 230, 692, 2026)).resize((ow, oh), Image.LANCZOS)
    out("door_leaf", leaf)
    out("door_grille", trim(load("velvet_elevator_grille")).resize((ow, oh), Image.LANCZOS))
    out("contract", by_height(trim(load("igor_contract")), 420))
    book = trim(load("compendium_open"))
    book = book.resize((380, round(book.height * 380 / book.width)), Image.LANCZOS)
    out("book_left", book.crop((0, 0, 190, book.height)))
    out("book_right", book.crop((190, 0, 380, book.height)))
    out("tarot_back", by_height(trim(load("tarot_back")), 300))
    out("coffin", by_height(trim(load("dark_hour_coffin")), 460))
    for n in ("sword", "cup", "wand", "coin"):
        out(f"minor_{n}", by_height(trim(load(f"minor_{n}")), 300))


if __name__ == "__main__":
    main()
