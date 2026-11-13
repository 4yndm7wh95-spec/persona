"""Export the approved card illustrations (art/cards/final/final_manifest.json) into the PCK stage.

Usage: python3 tools/pck/export_card_art.py [--stage mod/pck_stage]
Each image is resized to the vanilla portrait size of that card (1000x760, ancients 606x852) and written as
p3makoto/card_art/<card>.jpg (quality 90, 4:4:4) plus index.txt (one card id per line). Loaded at runtime through FileAccess; no .import files.
"""
import argparse, json, os
from PIL import Image

ap = argparse.ArgumentParser()
ap.add_argument("--stage", default="mod/pck_stage")
a = ap.parse_args()
out = os.path.join(a.stage, "p3makoto", "card_art")
os.makedirs(out, exist_ok=True)
manifest = json.load(open("art/cards/final/final_manifest.json", encoding="utf-8"))
total = 0
for e in manifest:
    im = Image.open(e["file"]).convert("RGB")
    size = (606, 852) if im.width < im.height else (1000, 760)
    if im.size != size:
        im = im.resize(size, Image.LANCZOS)
    p = os.path.join(out, e["name"] + ".jpg")
    im.save(p, quality=90, subsampling=0, optimize=True)
    total += os.path.getsize(p)
open(os.path.join(out, "index.txt"), "w", encoding="utf-8", newline="\n").write("\n".join(e["name"] for e in manifest) + "\n")
print(f"{len(manifest)} card illustrations -> {out} ({total / 1e6:.1f} MB)")
