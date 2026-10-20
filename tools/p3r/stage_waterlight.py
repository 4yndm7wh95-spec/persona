"""原版营地页的水光（Nami）→ mod 运行时贴图：mod/pck_stage/p3makoto/hero3d/tex/<page>_waterlight.png。
python3 tools/p3r/stage_waterlight.py item
原版 UIM_UI_Camp_<Page>_WaterLight_a / b 各 30 帧单帧网格（2000 / 3000 顶点，无 UV），坐标以 1920×1080 屏幕中心为原点、y 向下；
DT_CampParamCommon 的 Nami<Page>A/B OfsPos / Scale 在这里烘进去，颜色和不透明度留给运行时（Shaders.WaterLight）。
图集：R = a 层、G = b 层；每格是屏幕上方 0..768 像素（全部帧最低到 y≈742）缩到 1/3（640×256），5 列 × 6 行，第 i 帧在 (i%5, i//5)。
原版叠加前有一次很轻的高斯模糊（VeryWeak），全分辨率 2.5 像素，在这里画完再缩小。
"""
import json, os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

R = "research/p3r_camp/"
OUT = "mod/pck_stage/p3makoto/hero3d/tex/"
W, H, TOP, DIV, COLS = 1920, 1080, 768, 3, 5
PAGE = {"item": ("Item", ((120, 0), (1.0, 1.0)), ((0, 0), (1.0, 1.0)))}


def main(page):
    name, pa, pb = PAGE[page]
    cw, ch = W // DIV, TOP // DIV
    atlas = np.zeros((ch * 6, cw * COLS, 4), np.uint8)
    atlas[..., 3] = 255
    for ch_i, (ab, (ofs, sc)) in enumerate((("a", pa), ("b", pb))):
        for i in range(30):
            d = json.load(open(R + "pages/uim/Xrd777/UI/Camp/%s/Uim/UIM_UI_Camp_%s_WaterLight_%s_%02d.json" % (name, name, ab, i)))
            d = d[0] if isinstance(d, list) else d
            u = d["Properties"]["UimData"]
            v, ix = u["p2DGeomVertex"], u["Indices"]
            im = Image.new("L", (W, TOP), 0)
            dr = ImageDraw.Draw(im)
            P = [(W / 2 + p["x"] * sc[0] + ofs[0], H / 2 + p["y"] * sc[1] + ofs[1]) for p in v]
            for t in range(0, len(ix) - 2, 3):
                dr.polygon([P[ix[t]], P[ix[t + 1]], P[ix[t + 2]]], fill=255)
            im = im.filter(ImageFilter.GaussianBlur(2.5)).resize((cw, ch), Image.LANCZOS)
            x, y = (i % COLS) * cw, (i // COLS) * ch
            atlas[y:y + ch, x:x + cw, ch_i] = np.asarray(im)
    os.makedirs(OUT, exist_ok=True)
    Image.fromarray(atlas).convert("RGB").save(OUT + page + "_waterlight.png", optimize=True)
    print(OUT + page + "_waterlight.png", atlas.shape)


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "item")
