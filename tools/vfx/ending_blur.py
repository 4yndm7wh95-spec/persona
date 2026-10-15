"""闭眼结局背景的两档模糊（GreatSeal.CloseEyes 里按眼睑合上的程度交叉淡入）。
用法：python3 tools/vfx/ending_blur.py <原图.png>
写入 mod/pck_stage/p3makoto/ui/ending_rooftop.png（原图）、ending_rooftop_blur1.png、ending_rooftop_blur2.png。
模糊半径按图宽比例：blur1 ≈ 0.6%、blur2 ≈ 1.4%（2048 宽约 12px / 29px）——越来越糊，但仍看得出人影和色块位置，不糊成一片。
"""
import sys
from pathlib import Path
from PIL import Image, ImageFilter
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "pck"))
from godot_tex import write_texture

src = Image.open(sys.argv[1]).convert("RGBA")
stage = Path("mod/pck_stage")
write_texture(stage, "p3makoto/ui/ending_rooftop.png", src)
w = src.width
for name, r in (("ending_rooftop_blur1", 0.006), ("ending_rooftop_blur2", 0.014)):
    write_texture(stage, f"p3makoto/ui/{name}.png", src.filter(ImageFilter.GaussianBlur(w * r)))
print("ok")
