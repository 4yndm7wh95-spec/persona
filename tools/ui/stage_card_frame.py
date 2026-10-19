"""把塔罗卡框（art/card_frame/sprites）和重绘卡图（art/cards/v3/*.png）放进 mod/pck_stage（png + .import + .ctex）。
python3 tools/ui/stage_card_frame.py
"""
import glob, os, sys
from pathlib import Path
from PIL import Image
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "pck"))
from godot_tex import write_texture

STAGE = Path("mod/pck_stage")
REDO = {"venerate", "black_hole"}   # 用户否了、第 2 批开头重做的卡图：先不放进游戏
n = 0
for p in sorted(glob.glob("art/card_frame/sprites/*.png")):
    write_texture(STAGE, "p3makoto/card_frame/" + os.path.basename(p), Image.open(p)); n += 1
for p in sorted(glob.glob("art/cards/v3/*.png")):
    if Path(p).stem in REDO:
        continue
    write_texture(STAGE, "p3makoto/cards/" + os.path.basename(p), Image.open(p).convert("RGB").resize((1000, 760), Image.LANCZOS)); n += 1
print("staged", n)
