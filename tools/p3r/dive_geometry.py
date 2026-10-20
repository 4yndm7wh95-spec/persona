"""Stage the original dive resources with the placement measured from reference frames.
Only NumPy and Pillow are required; no game files are changed.
"""
import json
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw

def close_and_fill(a):
    # Match scipy's default cross structure, three dilation / erosion iterations.
    for dilate in (True, False):
        for _ in range(3):
            p = np.pad(a, 1, constant_values=False)
            parts = [p[1:-1, 1:-1], p[:-2, 1:-1], p[2:, 1:-1], p[1:-1, :-2], p[1:-1, 2:]]
            a = np.logical_or.reduce(parts) if dilate else np.logical_and.reduce(parts)
    im = Image.fromarray(np.pad(a, 1).astype(np.uint8) * 255).copy()
    ImageDraw.floodfill(im, (0, 0), 128)
    return np.asarray(im)[1:-1, 1:-1] != 128

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
    w, h = 480, 270
    acc = np.zeros((h, w), bool)
    for i in range(10):
        src = repo / ("art/p3menu_system/p3r/splash_%02d.png" % i)
        shutil.copyfile(src, out / ("splash_%02d.png" % i))
        a = np.asarray(Image.open(src).convert("RGBA"))[..., 3] > 25
        # First eight cells fill the viewport; last two keep their original cropped dimensions.
        im = Image.fromarray(a.astype(np.uint8) * 255).resize((int(a.shape[1] * w / 961), int(a.shape[0] * h / 949)))
        arr = np.asarray(im) > 127
        x0, y0 = (w - im.width) // 2, 0
        canvas = np.zeros((h, w), bool)
        canvas[y0:y0 + min(h, im.height), x0:x0 + im.width] = arr[:h]
        acc |= close_and_fill(canvas)
        if i == 0:
            assert acc.any() and not acc.all(), "First reveal mask must be partial"
        Image.fromarray(acc.astype(np.uint8) * 255).save(out / ("mask_%02d.png" % i))
    enter = mesh(repo, "UIM_UI_Camp_In")
    loop = mesh(repo, "UIM_UI_Camp_Loop_L")
    assert enter.shape == (75, 10, 17, 2) and loop.shape == (210, 10, 17, 2)
    assert np.allclose(enter[-1], loop[0], atol=0.001), "In / Loop baseline mismatch"
    assert np.allclose(loop[-1], loop[0], atol=0.001), "Loop is not seamless"
    enter.tofile(out / "warp.bin")
    loop.tofile(out / "warp_loop.bin")
    print("dive staged: native viewport placement, 10 masks, In75 + Loop210")

if __name__ == "__main__":
    repo = Path(__file__).resolve().parents[2]
    build(repo, repo / "mod/pck_stage/p3makoto/hero3d/dive")
