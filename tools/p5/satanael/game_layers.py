"""Satanael v6 layers (art/p5/fx/v4/satanael/layers_v6, Codex) -> game textures: each layer cropped to its alpha box and
scaled by K (= the on-screen size at 1080p, so the game draws them 1:1). Writes art/p5/fx/v4/satanael/game/sat_<layer>.png
and game.json (per layer: offset of the cropped image in scaled canvas pixels, pivot, z) + landmarks, all in scaled pixels."""
import json
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[3]
import sys
VER = sys.argv[1] if len(sys.argv) > 1 else "v10"          # v6 (side-on, retired) / v10 (distant, from the original model)
SRC = ROOT / f"art/p5/fx/v4/satanael/layers_{VER}"
OUT = ROOT / "art/p5/fx/v4/satanael/game"
K = float(sys.argv[2]) if len(sys.argv) > 2 else (0.6 if VER == "v6" else 0.42)
meta = json.load(open(SRC / "layers.json"))
OUT.mkdir(parents=True, exist_ok=True)
out = {"k": K, "layers": {}, "landmarks": {k: ([v[0] * K, v[1] * K] if isinstance(v, list) else v) for k, v in meta["landmarks"].items()},
       "parenting": meta.get("parenting", {})}
for L in meta["layers"]:
    name = L["file"][:-4]
    im = Image.open(SRC / L["file"]).convert("RGBA")
    box = im.getbbox()
    if box is None:
        print(name, "empty, skipped"); continue
    pad = 90 if name.startswith("wing") else 8      # wings bend in the shader (tips swing out): room around them
    box = (max(0, box[0] - pad), max(0, box[1] - pad), min(im.width, box[2] + pad), min(im.height, box[3] + pad))
    crop = im.crop(box)
    crop = crop.resize((round(crop.width * K), round(crop.height * K)), Image.LANCZOS)
    crop.save(OUT / f"sat_{name}.png")
    out["layers"][name] = {"offset": [box[0] * K, box[1] * K], "pivot": [L["pivot"][0] * K, L["pivot"][1] * K], "z": L["z_index"],
                           "size": list(crop.size)}
    print(name, crop.size)
json.dump(out, open(OUT / "game.json", "w"), indent=1)
