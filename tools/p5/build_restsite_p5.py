"""P5 rest site (replaces the Silent's): Joker on the campfire log with Morgana at his shoulder.

One picture (art/p5/poses/rest_sit_d_A.png, 1536x1024) on a weighted mesh, like tools/rig/build_restsite.py:
Joker breathes (chest and shoulders rise) and his head sways; Morgana's head bobs and his tail swings; the
tail and both coat tails are two-joint chains whose outer joint lags, so they ripple smoothly (user
2026-10-28: clearly visible but fine - the P3 rest site's motion was too faint). Joker and Morgana blink at different times with the eye patches made by
tools/p5/blink_patch.py (region attachments riding on the head bones).
The picture draws no seat (the log is the game's background): its soles go where the Silent's feet are
(~190 units under her hips), so Joker sits in front of the log rather than floating above it.
Output: art/p5/rig/restsite_silent.{spine-json,atlas,png} + art/p5/rig/preview/restsite.gif.
    python tools/p5/build_restsite_p5.py [--no-preview]
"""
import json
import math
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools/rig"))
from mesh_rig import MeshRig, bake, render_frame  # noqa: E402

SRC = ROOT / "art/p5/poses/rest_sit_d_A.png"
POSES = ROOT / "art/p5/poses"
OUT = ROOT / "art/p5/rig"
UNIT = 0.58                 # source px -> skeleton units: seated Joker ~580 units, the Silent's seated pose ~560
SOLE = 1005                 # lowest boot pixel in the picture
ORIGIN = (720, round(SOLE - 190 / UNIT))   # Joker's hip column; the soles land 190 units under the root
LOOP = 8.0
BLINK = 0.14
BLINKS = {"blink_joker": [2.0, 5.6], "blink_mona": [3.7, 7.0]}


def build():
    rig = MeshRig(Image.open(SRC), origin=ORIGIN, unit=UNIT, step=20, tex_scale=0.8, name="rest")
    rig.bone("chest", "root", (700, 760),
             polys=[[(420, 230), (900, 220), (930, 420), (880, 640), (700, 720), (470, 680), (420, 450)]], feather=90)
    rig.bone("head", "chest", (720, 270), circles=[(745, 130, 150)], polys=[[(650, 180), (830, 180), (820, 280), (660, 280)]],
             feather=50)
    rig.bone("mona_head", "root", (420, 390), circles=[(380, 260, 150)], feather=40)
    # tail and coat tails are two-joint chains: the outer joint lags, so they ripple instead of bending stiffly
    rig.bone("mona_tail1", "root", (313, 620), polys=[[(140, 430), (335, 430), (345, 655), (250, 665), (140, 520)]], feather=30)
    rig.bone("mona_tail2", "mona_tail1", (275, 535), polys=[[(140, 430), (325, 430), (305, 545), (165, 545)]], feather=30)
    rig.bone("hem_l1", "root", (470, 790), polys=[[(40, 800), (470, 760), (490, 970), (60, 990)]], feather=60)
    rig.bone("hem_l2", "hem_l1", (250, 880), polys=[[(40, 820), (280, 790), (290, 990), (40, 980)]], feather=60)
    rig.bone("hem_r1", "root", (1120, 760), polys=[[(1100, 700), (1510, 800), (1490, 890), (1110, 870)]], feather=60)
    rig.bone("hem_r2", "hem_r1", (1300, 820), polys=[[(1290, 770), (1510, 810), (1490, 890), (1290, 880)]], feather=50)
    rig.priority = ["head", "mona_head", "mona_tail2", "mona_tail1", "chest", "hem_l2", "hem_r2", "hem_l1", "hem_r1"]
    data = rig.setup()

    # eye patches ride on the head bones
    world = {n: rig.sp(p) for n, _, p in rig.bones}
    patches, att = {}, data["skins"][0]["attachments"]
    for slot, bone, name in (("blink_joker", "head", "rest_sit_d_A_blink_joker_patch"),
                             ("blink_mona", "mona_head", "rest_sit_d_A_blink_mona_patch")):
        meta = json.loads((POSES / f"{name}.json").read_text())
        im = Image.open(POSES / f"{name}.png").convert("RGBA")
        cx, cy = rig.sp((meta["x"] + im.width / 2, meta["y"] + im.height / 2))
        att[slot] = {slot: {"x": round(cx - world[bone][0], 2), "y": round(cy - world[bone][1], 2),
                            "width": round(im.width * UNIT, 2), "height": round(im.height * UNIT, 2)}}
        data["slots"].append({"name": slot, "bone": bone})
        patches[slot] = im

    def pose(t):
        b = math.sin(2 * math.pi * t / 4.0)              # breath
        sway = math.sin(2 * math.pi * t / LOOP)
        bob = math.sin(2 * math.pi * t / 2.0)
        w = 2 * math.pi * t / (LOOP / 3)                 # tail
        wind = 2 * math.pi * t / (LOOP / 2)               # coat tails
        return {
            "chest": {"sy": 1 + 0.013 * b, "sx": 1 - 0.004 * b, "r": 0.5 * b + 0.4 * sway},
            "head": {"r": 1.4 * math.sin(2 * math.pi * t / LOOP - 0.6) - 0.4 * b},
            "mona_head": {"r": 2.4 * bob, "y": 2.0 * (1 + bob)},
            "mona_tail1": {"r": 7.0 * math.sin(w)},
            "mona_tail2": {"r": 12.0 * math.sin(w - 1.0)},
            "hem_l1": {"r": -1.6 * math.sin(wind + 0.7)},
            "hem_l2": {"r": -2.6 * math.sin(wind - 0.2)},
            "hem_r1": {"r": 1.4 * math.sin(wind + 0.2)},
            "hem_r2": {"r": 2.4 * math.sin(wind - 0.7)},
        }

    for name in ("overgrowth_loop", "hive_loop", "glory_loop"):
        bake(data, name, LOOP, pose, loop=True, slot="rest")
        slots = data["animations"][name].setdefault("slots", {})
        for slot, starts in BLINKS.items():
            keys = [{"time": 0, "name": None}]
            for b in starts:
                keys += [{"time": b, "name": slot}, {"time": round(b + BLINK, 4), "name": None}]
            slots[slot] = {"attachment": keys}
    return rig, data, patches


def write(rig, data, patches):
    OUT.mkdir(parents=True, exist_ok=True)
    page, atlas = rig.texture_and_atlas("restsite_silent.png")
    # append the two eye patches under the mesh texture
    extra = [(n, im.resize((max(1, round(im.width * rig.tex_scale)), max(1, round(im.height * rig.tex_scale))), Image.LANCZOS))
             for n, im in patches.items()]
    W = max(page.width, sum(im.width for _, im in extra) + 2 * len(extra))
    H = page.height + 2 + max(im.height for _, im in extra)
    sheet = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    sheet.alpha_composite(page, (0, 0))
    lines = atlas.rstrip("\n").split("\n")
    lines[1] = f"size:{W},{H}"
    x = 0
    for n, im in extra:
        sheet.alpha_composite(im, (x, page.height + 2))
        lines += [n, f"bounds:{x},{page.height + 2},{im.width},{im.height}"]
        x += im.width + 2
    sheet.save(OUT / "restsite_silent.png")
    (OUT / "restsite_silent.atlas").write_text("\n".join(lines) + "\n")
    (OUT / "restsite_silent.spine-json").write_text(json.dumps(data, separators=(",", ":")))
    print("rest atlas", sheet.size)


if __name__ == "__main__":
    rig, data, patches = build()
    write(rig, data, patches)
    if "--no-preview" not in sys.argv:
        from spine_eval import Skeleton
        sk = Skeleton(data, {**rig.images(), **patches})
        frames = [render_frame(sk, data, "glory_loop", i / 10, (1000, 700), (560, 560), (28, 32, 48, 255)).convert("RGB")
                  .resize((600, 420), Image.LANCZOS).convert("P", palette=Image.ADAPTIVE, colors=200) for i in range(0, 80, 2)]
        (OUT / "preview").mkdir(parents=True, exist_ok=True)
        frames[0].save(OUT / "preview/restsite.gif", save_all=True, append_images=frames[1:], duration=200, loop=0, optimize=True)
