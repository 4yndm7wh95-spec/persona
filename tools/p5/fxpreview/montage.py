"""Contact sheet of chosen frames: python3 montage.py <scenario> <frame> <frame> ... [--crop x0,y0,x1,y1]"""
import sys
from PIL import Image, ImageDraw
args = sys.argv[1:]
crop = None
if "--crop" in args:
    i = args.index("--crop"); crop = tuple(int(v) for v in args[i + 1].split(",")); del args[i:i + 2]
s, frames = args[0], [int(f) for f in args[1:]]
ims = []
for f in frames:
    im = Image.open(f"out/{s}/f{f:08d}.png").convert("RGB")
    if crop:
        im = im.crop(crop)
    ims.append(im)
w, h = ims[0].size
k = min(1.0, 640 / w)
w2, h2 = int(w * k), int(h * k)
cols = min(4, len(ims))
sheet = Image.new("RGB", (cols * w2, ((len(ims) + cols - 1) // cols) * h2), "black")
d = ImageDraw.Draw(sheet)
for n, (f, im) in enumerate(zip(frames, ims)):
    x, y = n % cols * w2, n // cols * h2
    sheet.paste(im.resize((w2, h2)), (x, y))
    d.text((x + 6, y + 4), f"{f} ({f / 30:.2f}s)", fill="yellow")
sheet.save(f"out/{s}_sheet.png")
print(f"out/{s}_sheet.png")
