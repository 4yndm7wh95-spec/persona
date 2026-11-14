"""Export the approved card illustrations (art/cards/final/final_manifest.json) into the PCK stage.

Usage: python3 tools/pck/export_card_art.py [--stage cardart/pck_stage]
<card>.jpg is the size cards actually draw the portrait at, the vanilla card_atlas region 250x190 (ancients
250x351); <card>_big.jpg is 2x for the 0.113 full-size inspection view (BigPortrait). Lanczos downscale,
quality 95, 4:4:4, plus index.txt (one card id per line). 1.0.2: the old 1000x760 export was minified 4x by the
GPU without mipmaps, which aliased the fine line work into a harsh, over-sharpened look. Loaded at runtime through FileAccess; no .import files.
"""
import argparse, json, os
from PIL import Image

ap = argparse.ArgumentParser()
ap.add_argument("--stage", default="cardart/pck_stage")
a = ap.parse_args()
out = os.path.join(a.stage, "p3cardart", "card_art")
os.makedirs(out, exist_ok=True)
manifest = json.load(open("art/cards/final/final_manifest.json", encoding="utf-8"))
total = 0
for e in manifest:
    im = Image.open(e["file"]).convert("RGB")
    small = (250, 351) if im.width < im.height else (250, 190)
    for suffix, k in (("", 1), ("_big", 2)):
        p = os.path.join(out, e["name"] + suffix + ".jpg")
        im.resize((small[0] * k, small[1] * k), Image.LANCZOS).save(p, quality=95, subsampling=0, optimize=True)
        total += os.path.getsize(p)
open(os.path.join(out, "index.txt"), "w", encoding="utf-8", newline="\n").write("\n".join(e["name"] for e in manifest) + "\n")
print(f"{len(manifest)} card illustrations -> {out} ({total / 1e6:.2f} MB)")
