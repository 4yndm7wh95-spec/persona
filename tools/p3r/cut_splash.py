"""按 SPR_UI_Camp 的实际 UV 提取十张统一 960×948 画布，保留尾段透明边界。"""
import json
from pathlib import Path
from PIL import Image

repo = Path(__file__).resolve().parents[2]
definitions = json.loads((repo / "research/p3r_camp/anim/camp_splash_native.json").read_text("utf-8"))
out = repo / "art/p3menu_system/p3r"
out.mkdir(parents=True, exist_ok=True)
for i, entry in enumerate(definitions["sprites"]):
    src = repo / ("research/p3r_camp/tex/Xrd777/UI/Camp/Root/Texture/" + entry["texture"] + ".png")
    im = Image.open(src).convert("RGBA")
    assert list(im.size) == entry["atlas_size"]
    result = im.crop(entry["crop_box_xyxy"])
    assert result.size == (960, 948)
    result.save(out / ("splash_%02d.png" % i))
print("10 original sprites staged with verified UVs and 960x948 canvas")
