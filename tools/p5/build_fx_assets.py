"""Generated textures for the P5 Joker card effects (p5mod/src), written to art/p5/fx/gen/.

  cutin_band.png    1600x420  the Persona cut-in band: P5 red, black halftone dots thinning to the right,
                              white slash stripes, Joker's face (cropped from idle_a3, duotone-free) on the right
  calling_card.png  220x320   P5 calling card: red card, black border, white collage letters, black top-hat sign
  joker_ghost.png             idle_a3 (final canvas, feet aligned) at 0.3: afterimages / dodge ghosts
  arsene_idle.png / arsene_cast.png + arsene.json   Arsène frame atlases (art/p5/fx/arsene renders), meta in json
Codex can replace cutin_band / calling_card with drawn versions later (same size and layout).
    python tools/p5/build_fx_assets.py
"""
import json
import random
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "art/p5/fx/gen"
RED = (230, 0, 18, 255)
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"


def cutin_band():
    W, H = 1600, 420
    im = Image.new("RGBA", (W, H), RED)
    d = ImageDraw.Draw(im)
    # halftone: dot radius shrinks left -> right, on a 26 px grid tilted rows
    for gy in range(-1, H // 22 + 2):
        for gx in range(-1, W // 26 + 2):
            x = gx * 26 + (13 if gy % 2 else 0)
            y = gy * 22
            r = 11 * max(0.0, 1 - x / (W * 0.62))
            if r > 0.8:
                d.ellipse([x - r, y - r, x + r, y + r], fill=(0, 0, 0, 255))
    # white slash stripes
    for x0, w in ((260, 26), (330, 10), (900, 18)):
        d.polygon([(x0, H), (x0 + w, H), (x0 + w + 160, 0), (x0 + 160, 0)], fill=(255, 255, 255, 255))
    # face: idle_a3 head, eyes in the band's vertical middle
    src = Image.open(ROOT / "art/p5/poses/idle_a3.png").convert("RGBA").crop((440, 60, 700, 260))
    face = src.resize((int(src.width * 2.3), int(src.height * 2.3)), Image.LANCZOS)
    fx, fy = W - face.width - 40, H // 2 - int(118 * 2.3) + 20
    shadow = Image.new("RGBA", face.size, (0, 0, 0, 255))
    shadow.putalpha(face.getchannel("A").point(lambda v: 255 if v > 40 else 0))
    im.alpha_composite(shadow, (fx + 14, fy + 10))                     # hard black drop shadow
    im.alpha_composite(face, (fx, fy))
    # the crop's straight left edge is hidden under a black slanted bar (P5 cut-ins are hard-edged)
    d = ImageDraw.Draw(im)
    d.polygon([(fx - 70, H), (fx + 40, H), (fx + 150, 0), (fx + 40, 0)], fill=(0, 0, 0, 255))
    d.polygon([(fx - 100, H), (fx - 80, H), (fx + 30, 0), (fx + 10, 0)], fill=(255, 255, 255, 255))
    d.rectangle([0, 0, W, 7], fill=(0, 0, 0, 255))
    d.rectangle([0, H - 8, W, H], fill=(0, 0, 0, 255))
    im.save(OUT / "cutin_band.png")


def calling_card():
    W, H = 220, 320
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle([0, 0, W - 1, H - 1], 10, fill=(10, 8, 10, 255))
    d.rounded_rectangle([8, 8, W - 9, H - 9], 6, fill=RED)
    rnd = random.Random(5)
    words = ["TAKE", "YOUR", "HEART"]
    y = 120
    for w in words:
        x = 18
        for ch in w:
            size = rnd.randint(25, 31)
            f = ImageFont.truetype(FONT, size)
            box = d.textbbox((0, 0), ch, font=f)
            cw, chh = box[2] - box[0] + 12, box[3] - box[1] + 12
            tile = Image.new("RGBA", (cw, chh), (255, 255, 255, 255) if rnd.random() < 0.6 else (0, 0, 0, 255))
            td = ImageDraw.Draw(tile)
            td.text((6 - box[0], 6 - box[1]), ch, font=f, fill=(0, 0, 0, 255) if tile.getpixel((0, 0))[0] > 128 else (255, 255, 255, 255))
            tile = tile.rotate(rnd.uniform(-12, 12), expand=True, resample=Image.BICUBIC)
            im.alpha_composite(tile, (x, y + rnd.randint(-4, 4)))
            x += tile.width - 4
        y += 58
    # top-hat calling sign (simplified: black hat over a white smile)
    d.rectangle([84, 28, 136, 70], fill=(0, 0, 0, 255))
    d.rectangle([66, 66, 154, 78], fill=(0, 0, 0, 255))
    d.chord([76, 76, 144, 112], 0, 180, fill=(255, 255, 255, 255))
    im.save(OUT / "calling_card.png")


def joker_ghost():
    im = Image.open(ROOT / "art/p5/poses/final/idle_a3.png").convert("RGBA")
    box = im.getchannel("A").getbbox()
    im = im.crop(box)
    im.resize((int(im.width * 0.3), int(im.height * 0.3)), Image.LANCZOS).save(OUT / "joker_ghost.png")
    return box


def atlas(name, frames, scale, cols):
    ims = [Image.open(f).convert("RGBA") for f in frames]
    w, h = int(ims[0].width * scale), int(ims[0].height * scale)
    rows = (len(ims) + cols - 1) // cols
    sheet = Image.new("RGBA", (w * cols, h * rows), (0, 0, 0, 0))
    for i, m in enumerate(ims):
        sheet.paste(m.resize((w, h), Image.LANCZOS), ((i % cols) * w, (i // cols) * h))
    sheet.save(OUT / f"arsene_{name}.png")
    return {"w": w, "h": h, "cols": cols, "count": len(ims), "scale": scale}


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    cutin_band()
    calling_card()
    box = joker_ghost()
    A = ROOT / "art/p5/fx/arsene"
    meta = {"ghost_box": box}
    idle = sorted((A / "idle").glob("*.png"))[::2]
    meta["idle"] = atlas("idle", idle, 0.45, 9)
    meta["idle"]["anchor"] = json.loads((A / "idle/anchor.json").read_text())["anchors"]
    cast = sorted((A / "cast").glob("*.png"))
    meta["cast"] = atlas("cast", cast, 0.4, 6)
    meta["cast"]["anchor"] = json.loads((A / "cast/anchor.json").read_text())["anchors"]
    (OUT / "arsene.json").write_text(json.dumps(meta, indent=1))
    print(json.dumps(meta))


if __name__ == "__main__":
    main()
