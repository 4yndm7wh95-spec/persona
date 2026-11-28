"""Contact sheets: python3 -I sheet.py <out_prefix> <cols> <thumb_w> <img...>"""
import sys
from PIL import Image, ImageDraw
out, cols, tw = sys.argv[1], int(sys.argv[2]), int(sys.argv[3])
files = sys.argv[4:]
th = tw * 9 // 16
per = cols * 3
for s in range(0, len(files), per):
    chunk = files[s:s + per]
    rows = (len(chunk) + cols - 1) // cols
    sheet = Image.new('RGB', (cols * tw, rows * (th + 16)), (30, 30, 30))
    d = ImageDraw.Draw(sheet)
    for i, f in enumerate(chunk):
        im = Image.open(f).convert('RGB')
        im.thumbnail((tw, th))
        x, y = (i % cols) * tw, (i // cols) * (th + 16)
        sheet.paste(im, (x, y + 16))
        d.text((x + 3, y + 2), f.split('/')[-1][:70], fill=(255, 255, 0))
    sheet.save(f'{out}_{s // per:02d}.jpg', quality=88)
    print(f'{out}_{s // per:02d}.jpg')
