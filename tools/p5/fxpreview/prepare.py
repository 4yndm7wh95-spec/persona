"""Write the mod's FX textures (tools/p5/stage_p5.py fx_textures) and the stand-in Joker to .tex/ for the preview."""
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
from stage_p5 import ROOT, fx_textures  # noqa: E402
from PIL import Image  # noqa: E402

out = HERE / ".tex"
out.mkdir(exist_ok=True)
for name, im in fx_textures():
    im.save(out / f"{name}.png")
Image.open(ROOT / "art/p5/poses/final/idle_a3.png").save(out / "_joker.png")
print("textures in", out)
