"""P5 卡面替换：把审批通过的卡图（第一轮通过 + 第二轮重出的）导出进 PCK 暂存目录。
来源：art/p5/cards/round2/<卡>.png 有就用它，否则 art/p5/cards/round1/batch_*/<卡>.png（审批台 https://claude.ai/artifact/Vrxut7p2i4godAwoYjztFG：77 张第一轮通过，14 张 + 小刀在第二轮重出）。
照 P3CardArt 1.0.2：<卡>.jpg = 卡牌上实际画的尺寸 250×190（远古牌竖图 250×351），<卡>_big.jpg = 2 倍给完整查看大图；index.txt 每行一个卡 id。
1.0.1（用户 2026-11-24：游戏里看着锐化很重、眼睛不舒服）：之前用 Lanczos 缩 4 倍，硬边旁边出振铃（一圈亮暗边），生成图本身边缘也比原版硬，
缩到 250×190 后高频细节比原版多约 30%（拉普拉斯均值 24.0 vs 原版 18.3）。改成先高斯模糊（小图 1.2px、大图 0.6px，按原图像素）再按面积平均缩小（BOX），
小图降到 17.7，和原版差不多。
用法：python3 tools/p5/cardart/export_card_art.py [--stage p5cardart/pck_stage]"""
import argparse, glob, json, os
from PIL import Image, ImageFilter

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
    for suffix, k, blur in (("", 1, 1.2), ("_big", 2, 0.6)):
        p = os.path.join(out, name + suffix + ".jpg")
        im.filter(ImageFilter.GaussianBlur(blur)).resize((small[0] * k, small[1] * k), Image.BOX).save(p, quality=95, subsampling=0, optimize=True)
        total += os.path.getsize(p)
    used.append((name, src))
open(os.path.join(out, "index.txt"), "w", encoding="utf-8", newline="\n").write("\n".join(n for n, _ in used) + "\n")
json.dump(dict(used), open("p5cardart/sources.json", "w"), indent=1)   # 每张用的是哪一轮的图
print(f"{len(used)} card illustrations -> {out} ({total / 1e6:.2f} MB)")
