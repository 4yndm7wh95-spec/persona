"""手机端的小资源包 P5Joker.mobile.pck 的打包目录（p5mod/pck_stage_mobile）。

用户 2026-11-26：部分玩家（主要是手机）玩 Joker 掉帧、闪退。Joker 的战斗骨骼贴图是两张 4096 大图（4096×4096 + 4096×2048），
无损、不压缩打包，解码后内存 / 显存各约 96MB，原版角色是几分之一；低内存手机上很容易被系统杀掉，带宽小的 GPU 每帧从大图上缩小采样也慢。
这里把这两张图和 atlas 都缩到一半（24MB），再加牌堆的两张大立绘缩到 0.75，装成一个只覆盖这几个文件的小包；ModEntry 在手机上检测到 P5Joker.mobile.pck 才挂载，
PC 不挂、也不用带这个文件 —— 没有这个文件时一切和以前一样。
战斗里人物显示高度约 400px（1080p），骨骼里单张姿势图最高 645px，缩一半（322px）放大到 400px，手机屏幕上看不出区别。

为什么只改这三个文件就够：.import 文件不变（路径一样），只换 .godot/imported 里的贴图和 atlas 数据；atlas 里所有坐标（size / bounds / offsets）
一起减半，骨骼里附件的显示尺寸来自 spine-json（不变），所以画出来大小一样、只是贴图分辨率低一半。

用法：先 python3 tools/p5/stage_p5.py（生成 p5mod/pck_stage），再 python3 tools/p5/stage_mobile.py；
打包见 tools/p5/cloud/build_package.sh（会同时打 P5Joker.mobile.pck）。"""
import hashlib
import json
import math
import re
import shutil
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools/pck"))
from godot_tex import ctex_bytes, read_ctex  # noqa: E402

STAGE = ROOT / "p5mod/pck_stage"
OUT = ROOT / "p5mod/pck_stage_mobile"
REL = "animations/characters/silent"
BASE = "silent"


def h(res: str) -> str:
    return hashlib.md5(res.encode()).hexdigest()


def halve_atlas(text: str) -> str:
    """atlas 里的 size / bounds / offsets 全部减半（bounds 往外取整，不切掉边缘像素）。"""
    out = []
    for line in text.split("\n"):
        key, sep, val = line.partition(":")
        k = key.strip()
        if sep and k in ("size", "bounds", "offsets", "xy", "orig", "offset"):
            nums = [int(v) for v in val.split(",")]
            if k == "bounds" and len(nums) == 4:
                x, y, w, hh = nums
                x0, y0 = x // 2, y // 2
                x1, y1 = math.ceil((x + w) / 2), math.ceil((y + hh) / 2)
                nums = [x0, y0, x1 - x0, y1 - y0]
            else:
                nums = [max(1, round(v / 2)) if v > 0 else 0 for v in nums]
            line = key + ":" + ",".join(str(v) for v in nums)
        out.append(line)
    return "\n".join(out)


def main():
    imp = STAGE / ".godot/imported"
    atlas_res = f"res://{REL}/{BASE}.atlas"
    spatlas = imp / f"{BASE}.atlas-{h(atlas_res)}.spatlas"
    if not spatlas.exists():
        sys.exit(f"missing {spatlas}: run tools/p5/stage_p5.py first")
    if OUT.exists():
        shutil.rmtree(OUT)
    (OUT / ".godot/imported").mkdir(parents=True)
    data = json.loads(spatlas.read_text())
    atlas = data["atlas_data"]
    pages = [l.strip() for l in atlas.split("\n") if l.strip().endswith(".png")]
    for page in pages:
        src = imp / f"{page}-{h(f'res://{REL}/{page}')}.ctex"
        im = read_ctex(src.read_bytes()).convert("RGBA")
        half = im.resize((max(1, im.width // 2), max(1, im.height // 2)), Image.LANCZOS)
        (OUT / ".godot/imported" / src.name).write_bytes(ctex_bytes(half))
        print(f"{page}: {im.size} -> {half.size}")
    # 牌堆的两张大立绘（抽牌堆 / 消耗堆，2023² 带 mipmap，约 22MB 一张）：代码按贴图宽度算缩放（PileDraw / PileExhaust 的 _porScale），
    # 换小图大小不变；缩到 0.75（再小在手机上看得出糊）
    for name in ("pile_camptop", "pile_campskill"):
        src = imp / f"{name}.png-{h(f'res://p5joker/fx/{name}.png')}.ctex"
        if not src.exists():
            continue
        im = read_ctex(src.read_bytes()).convert("RGBA")
        small = im.resize((round(im.width * 0.75), round(im.height * 0.75)), Image.LANCZOS)
        (OUT / ".godot/imported" / src.name).write_bytes(ctex_bytes(small, mipmaps=True))
        print(f"{name}: {im.size} -> {small.size}")
    data["atlas_data"] = halve_atlas(atlas)
    (OUT / ".godot/imported" / spatlas.name).write_text(json.dumps(data, separators=(",", ":")))
    # 自检：每个 bounds 都落在页面里
    page_size = None
    for line in data["atlas_data"].split("\n"):
        if line.startswith("size:"):
            page_size = [int(v) for v in line[5:].split(",")]
        m = re.match(r"\s*bounds:(\d+),(\d+),(\d+),(\d+)", line)
        if m and page_size:
            x, y, w, hh = map(int, m.groups())
            assert x + w <= page_size[0] and y + hh <= page_size[1], line
    print("wrote", OUT)


if __name__ == "__main__":
    main()
