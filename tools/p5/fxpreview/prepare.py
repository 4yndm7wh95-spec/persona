"""Write the mod's FX textures (tools/p5/stage_p5.py fx_textures) to .tex/ for the preview."""
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
from stage_p5 import ROOT, fx_textures  # noqa: E402

out = HERE / ".tex"
out.mkdir(exist_ok=True)
for name, im in fx_textures():
    im.save(out / f"{name}.png")
print("textures in", out)

# 字体（P5Ui 读 res://p5joker/fonts/…，游戏里在 PCK 里）：拷到预览工程的同一路径
import shutil  # noqa: E402
fonts = HERE / "p5joker/fonts"
fonts.mkdir(parents=True, exist_ok=True)
for f in ("BarlowCondensed-900Italic.ttf", "BarlowCondensed-700Italic.ttf"):
    if not (fonts / f).exists():
        shutil.copy(ROOT / "tools/ui/fonts" / f, fonts / f)

# 菜单 3D Joker 的网格 / 动作片段（MenuJoker3D 读 res://p5joker/m3d/…）
m3d = HERE / "p5joker/m3d"
m3d.mkdir(parents=True, exist_ok=True)
for f in sorted((ROOT / "research/p5r/menu3d_runtime").iterdir()):
    shutil.copy(f, m3d / f.name)

# 原版怪物（research/sts2_preview_scene）：.skel → JSON（tools/spine/skel_reader.py），给 SpinePlayer 播
import subprocess  # noqa: E402
mon = HERE / ".mon"
mon.mkdir(exist_ok=True)
SRC = ROOT / "research/sts2_preview_scene/recovered/animations/monsters"
for m in ("chomper", "inklet", "cultists", "fat_gremlin"):
    d = SRC / m
    if d.exists() and not (mon / f"{m}.json").exists():
        subprocess.run([sys.executable, str(ROOT / "tools/spine/skel_reader.py"), str(d / f"{m}.skel"), str(mon / f"{m}.json")], check=False,
                       stdout=subprocess.DEVNULL)
