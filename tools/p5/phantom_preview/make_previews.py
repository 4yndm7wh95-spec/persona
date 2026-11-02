"""Package actual Godot frames as small review GIFs and a labeled contact sheet."""
from pathlib import Path
import shutil
import subprocess
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
DEST = HERE.parents[2] / "docs/p5/preview/phantom_studies"
DEST.mkdir(parents=True, exist_ok=True)
for name in ("summon", "cutin"):
    subprocess.run([
        "ffmpeg", "-loglevel", "error", "-y", "-framerate", "60",
        "-i", str(HERE / f"out/{name}/f%08d.png"),
        "-vf", "fps=30,scale=960:-1:flags=lanczos,split[a][b];[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=4",
        str(DEST / f"{name}.gif"),
    ], check=True)

font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 24)
small = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 17)
sheet = Image.new("RGB", (1280, 850), "#111116")
d = ImageDraw.Draw(sheet)
panels = [
    ("cutin", 14, "出场前 · 背景原亮度"),
    ("cutin", 41, "眼神切入 · 72% 大小 + 背景暗场"),
    ("summon", 79, "召唤释放 · 蓝焰爆发"),
    ("summon", 85, "原版火星 / 流动蓝焰 / 眼神切入"),
]
for i, (name, frame, title) in enumerate(panels):
    x, y = (i % 2) * 640, (i // 2) * 405 + 40
    src = HERE / f"out/{name}/f{frame:08d}.png"
    im = Image.open(src).convert("RGB")
    im.resize((640, 360), Image.Resampling.LANCZOS).save(DEST / f"{name}.png")
    sheet.paste(im.resize((640, 360), Image.Resampling.LANCZOS), (x, y + 30))
    d.text((x + 14, y), title, font=font, fill="#fff8ed")
    shutil.copy2(src, DEST / f"{name}_full.png")
d.text((14, 9), "JOKER / 第一批特效试作 · GODOT 4.5.1 实际渲染", font=small, fill="#e82140")
sheet.save(DEST / "overview.png")
print(DEST)
