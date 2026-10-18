"""设置界面倒挂立绘：从 art/p3menu_system 派生接入素材（Codex 的生图没有完全满足"纯白"要求，这里按容差处理）。
· alpha < 12 的零星杂点清成全透明；
· 近白（alpha>0、RGB 最小值 >= 222、饱和度很低）统一成纯白 #FFFFFF —— 游戏里的融水着色器另外还按容差判断白色，两道保险；
· 三帧（base / 半睁 / 闭眼）同样处理，输出到 mod/pck_stage/p3makoto/ui/system_portrait*.png（带 .import / .ctex）。
用法：python3 tools/ui/build_system_portrait.py [预览输出.png]"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools/pck"))
import godot_tex  # noqa: E402

SRC = ROOT / "art/p3menu_system"
STAGE = ROOT / "mod/pck_stage"
FRAMES = {"system_base.png": "system_portrait.png", "system_eyes_half.png": "system_portrait_half.png", "system_eyes_closed.png": "system_portrait_closed.png"}


def clean(im):
    a = np.asarray(im.convert("RGBA")).astype(np.int32)
    rgb, al = a[..., :3], a[..., 3]
    al[al < 12] = 0
    mn, mx = rgb.min(axis=2), rgb.max(axis=2)
    white = (al > 0) & (mn >= 222) & ((mx - mn) <= 26)
    rgb[white] = 255
    rgb[al == 0] = 0
    out = np.dstack([rgb, al]).clip(0, 255).astype(np.uint8)
    return Image.fromarray(out, "RGBA"), int(white.sum())


def main(preview=None):
    done = {}
    for src, dst in FRAMES.items():
        im, n = clean(Image.open(SRC / src))
        godot_tex.write_texture(STAGE, f"p3makoto/ui/{dst}", im)
        done[dst] = (im, n)
        print(dst, im.size, "snapped-to-white", n)
    if preview:
        base = done["system_portrait.png"][0]
        bg = Image.new("RGBA", base.size, (20, 70, 200, 255))
        bg.paste((255, 255, 255, 255), (0, 0, 820, base.height))
        bg.alpha_composite(base)
        bg.convert("RGB").resize((960, 540)).save(preview)


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else None)
