import sys
from PIL import Image, ImageDraw
d, out = sys.argv[1], sys.argv[2]
frames = [int(x) for x in sys.argv[3].split(',')]
box = tuple(int(x) for x in sys.argv[4].split(',')) if len(sys.argv) > 4 else (0, 250, 760, 1000)
cols = 4
ims = []
for f in frames:
    im = Image.open(f'{d}/f{f:08d}.png').convert('RGB').crop(box)
    ImageDraw.Draw(im).text((8, 8), str(f), fill=(255, 255, 0))
    ims.append(im)
w, h = ims[0].size
rows = (len(ims) + cols - 1) // cols
W = Image.new('RGB', (w * cols, h * rows))
for k, im in enumerate(ims):
    W.paste(im, ((k % cols) * w, (k // cols) * h))
W.save(out)
