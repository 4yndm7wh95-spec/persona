"""Stage native sprite UV cuts, five original geometry masks and the cached water mesh.
Only NumPy and Pillow are required; no game files are changed.
"""
import json
from pathlib import Path
import numpy as np
from PIL import Image

def mesh(repo, name):
    root = repo / "research/p3r_camp"
    j = json.loads((root / ("json/Xrd777/UI/Camp/Root/Uim/" + name + ".json")).read_text(encoding="utf-8"))
    gv = j[0]["Properties"]["UimData"]["p2DGeomVertUV"]
    base = np.array([[v["x"], v["y"]] for v in gv], np.float32)
    uv = np.array([[v["u0"], v["v0"]] for v in gv], np.float32)
    anim = json.loads((root / ("anim/" + name + "__" + name + ".json")).read_text(encoding="utf-8"))
    off = np.array([k["value"] for k in anim["tracks"][0]["keys"]], np.float32)
    rest = np.stack([uv[:, 0] * 1920 - 960, uv[:, 1] * 1184 - 556], -1)
    dev = (base[None] + off - rest[None]).reshape(len(off), 10, 17, 2)
    order = np.argsort(uv.reshape(10, 17, 2)[:, 0, 1])
    return dev[:, order].astype(np.float32)

def build(repo, out):
    import shutil
    repo, out = Path(repo), Path(out)
    out.mkdir(parents=True, exist_ok=True)
    for i in range(10):
        src = repo / ("art/p3menu_system/p3r/splash_%02d.png" % i)
        assert Image.open(src).size == (960, 948), "Run cut_splash.py for native UV cuts first"
        shutil.copyfile(src, out / ("splash_%02d.png" % i))
    # Frozen raster caches from PLG_UI_Camp[12..16], including vertex edge alpha.
    # Rebuild with tools/p3r/mask_render/render.gd; no runtime mesh work.
    for i in range(5):
        src = repo / ("art/p3menu_system/p3r/in_mask_%02d.png" % i)
        assert Image.open(src).size == (1280, 720)
        shutil.copyfile(src, out / ("mask_%02d.png" % i))
    Image.new("L", (1, 1), 0).save(out / "mask_blank.png")
    enter = mesh(repo, "UIM_UI_Camp_In")
    loop = mesh(repo, "UIM_UI_Camp_Loop_L")
    assert enter.shape == (75, 10, 17, 2) and loop.shape == (210, 10, 17, 2)
    assert np.allclose(enter[-1], loop[0], atol=0.001), "In / Loop baseline mismatch"
    assert np.allclose(loop[-1], loop[0], atol=0.001), "Loop is not seamless"
    enter.tofile(out / "warp.bin")
    loop.tofile(out / "warp_loop.bin")
    print("dive staged: native UV canvas, 5 original masks + blank, In75 + Loop210; legacy mask_05..09 unused")

if __name__ == "__main__":
    repo = Path(__file__).resolve().parents[2]
    build(repo, repo / "mod/pck_stage/p3makoto/hero3d/dive")
