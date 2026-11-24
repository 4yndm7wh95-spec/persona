"""P5 卡面替换：把审批通过的卡图（第一轮通过 + 第二轮重出的）导出进 PCK 暂存目录。
来源：art/p5/cards/round2/<卡>.png 有就用它，否则 art/p5/cards/round1/batch_*/<卡>.png（审批台 https://claude.ai/artifact/Vrxut7p2i4godAwoYjztFG：77 张第一轮通过，14 张 + 小刀在第二轮重出）。
照 P3CardArt 1.0.2：<卡>.jpg = 卡牌上实际画的尺寸 250×190（远古牌竖图 250×351），<卡>_big.jpg = 2 倍给完整查看大图；index.txt 每行一个卡 id。
用法：python3 tools/p5/cardart/export_card_art.py [--stage p5cardart/pck_stage]"""
import argparse, glob, json, os
from PIL import Image

ap = argparse.ArgumentParser()
ap.add_argument("--stage", default="p5cardart/pck_stage")
a = ap.parse_args()
out = os.path.join(a.stage, "p5cardart", "card_art")
os.makedirs(out, exist_ok=True)
names = [e["card"] for e in json.load(open("art/p5/cards/prompts.json", encoding="utf-8"))] + ["shiv"]
total, used = 0, []
for name in names:
    src = f"art/p5/cards/round2/{name}.png"
    if not os.path.exists(src):
        found = glob.glob(f"art/p5/cards/round1/batch_*/{name}.png")
        if not found:
            print("missing", name); continue
        src = found[0]
    im = Image.open(src).convert("RGB")
    small = (250, 351) if im.width < im.height else (250, 190)
    for suffix, k in (("", 1), ("_big", 2)):
        p = os.path.join(out, name + suffix + ".jpg")
        im.resize((small[0] * k, small[1] * k), Image.LANCZOS).save(p, quality=95, subsampling=0, optimize=True)
        total += os.path.getsize(p)
    used.append((name, src))
open(os.path.join(out, "index.txt"), "w", encoding="utf-8", newline="\n").write("\n".join(n for n, _ in used) + "\n")
json.dump(dict(used), open("p5cardart/sources.json", "w"), indent=1)   # 每张用的是哪一轮的图
print(f"{len(used)} card illustrations -> {out} ({total / 1e6:.2f} MB)")
