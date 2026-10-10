"""把展示模式录下的 JPG 帧合成为 GIF（Claude 在云端用）。
用法：python3 tools/make_gifs.py research/showcase/run1 [宽度]
每个子文件夹（01_evoker 等）输出一个同名 .gif 到 run 目录下。"""
import sys
from pathlib import Path
from PIL import Image

run = Path(sys.argv[1])
width = int(sys.argv[2]) if len(sys.argv) > 2 else 640
for shot in sorted(p for p in run.iterdir() if p.is_dir()):
    frames = sorted(shot.glob("f*.jpg"))
    if not frames:
        continue
    imgs = []
    for f in frames:
        im = Image.open(f).convert("RGB")
        h = round(im.height * width / im.width)
        imgs.append(im.resize((width, h), Image.LANCZOS).convert("P", palette=Image.ADAPTIVE, colors=192))
    out = run / f"{shot.name}.gif"
    imgs[0].save(out, save_all=True, append_images=imgs[1:], duration=33, loop=0, optimize=True)
    print(out, len(imgs), "frames")
