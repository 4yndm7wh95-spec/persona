"""把原版营地主角 + COMMU 塔罗牌整理成 mod 运行时资源：mod/pck_stage/p3makoto/hero3d/。
python3 tools/p3r/stage_hero3d.py
- 模型 GLB、贴图 PNG 原样复制（运行时用 GLTFDocument / Image 直接读文件，不走导入）。
- 动作：PSA → <名>.bin（float32 帧×骨骼×[px py pz qx qy qz qw]，glTF 坐标，见 psa_to_bin.py）+ <名>.json。
  fps 用原版 AnimSequence 的 SequenceLength：(帧数-1)/时长（= 30），不用 PSA 里那个不准的 rate。
- 缩放：原版靠骨骼缩放到 0.001 藏道具（召唤器、椅子等）。只保留偏离 1 的骨骼，
  存成 <名>.scale.bin（float32 帧×骨骼×[sx sy sz]，已换到 glTF 轴序 x,z,y），骨骼名写进 json 的 scale_bones。
"""
import json, os, shutil, subprocess, sys, tempfile
import numpy as np

R = "research/p3r_camp/"
OUT = "mod/pck_stage/p3makoto/hero3d/"
HERO = {  # 名字 → (PSA 编号, 是否分块)
    "CommuStart": ("0013", False), "CommuLoop": ("0014", True), "CommuListStart": ("0015", False),
    "CommuListLoop": ("0016", True), "CommuListCancel": ("0025", False),
    "CampTopStart": ("0001", False), "CampTopLoop": ("0002", True),   # 营地主菜单：倒入水 → 水下待机（牌组牌堆 / ESC 设置用）
}
CARD = {"CommuStart": "0023", "CommuLoop": "0024", "CommuListStart": "0025", "CommuListLoop": "0026", "CommuListCancel": "0027"}


def psa(src, prefix, keep=None):
    args = [sys.executable, "tools/p3r/psa_to_bin.py", src, prefix] + ([keep] if keep else [])
    subprocess.run(args, check=True, stdout=subprocess.DEVNULL)


def fix_fps(prefix, seq_json):
    j = json.load(open(prefix + ".json"))
    sl = json.load(open(seq_json))[0]["Properties"]["SequenceLength"]
    j["fps"] = (j["frames"] - 1) / sl
    return j


def commu_word(out):
    from PIL import Image, ImageDraw
    pd = json.load(open(R + "plg/json/Xrd777/UI/Camp/Root/Plg/PLG_UI_Camp.json"))
    pd = pd[0] if isinstance(pd, list) else pd
    p = next(x for x in pd["Properties"]["PlgData"]["PlgDatas"] if x["Name"] == "COMMU")
    im = Image.new("RGBA", (2200, 520), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    v = [((q["X"]) * 2 + 1100, (q["Y"]) * 2 + 260) for q in p["Vertices"]]; ix = p["Indices"]
    for t in range(0, len(ix), 3): d.polygon([v[ix[t]], v[ix[t + 1]], v[ix[t + 2]]], fill=(165, 165, 172, 255))
    im.save(out)


def dive(out):
    """入水转场：原版水花 10 帧（art/p3menu_system/p3r/splash_NN.png）+ 每帧的"露出水下"遮罩（水花轮廓填实后逐帧累积，480×270）
    + 原版 UIM_UI_Camp_In 网格的逐帧偏差（17×10 网格，各顶点 位置 −（按 UV 的静止位置），1920×1184 坐标），存成 warp.bin（float32 75×10×17×2）。"""
    from PIL import Image
    from scipy.ndimage import binary_closing, binary_fill_holes
    os.makedirs(out, exist_ok=True)
    W, H = 480, 270
    acc = np.zeros((H, W), bool)
    for i in range(10):
        src = "art/p3menu_system/p3r/splash_%02d.png" % i
        shutil.copy(src, out + "splash_%02d.png" % i)
        a = np.asarray(Image.open(src).convert("RGBA"))[..., 3] > 25
        # 和游戏里水花的摆法一致：横向铺满 1.06 倍屏宽，纵向 1.6 倍（949 高），上边缘在 −10% 处，水平居中
        im = Image.fromarray((a * 255).astype(np.uint8)).resize((int(a.shape[1] * 1.06 * W / 961), int(a.shape[0] * 1.6 * H / 949)))
        m = np.zeros((H, W), bool); arr = np.asarray(im) > 127
        x0 = (W - im.width) // 2; y0 = int(-0.10 * H)
        ys, xs = max(0, -y0), max(0, -x0); ye, xe = min(im.height, H - y0), min(im.width, W - x0)
        m[y0 + ys:y0 + ye, x0 + xs:x0 + xe] = arr[ys:ye, xs:xe]
        acc |= binary_fill_holes(binary_closing(m, iterations=3))
        Image.fromarray((acc * 255).astype(np.uint8)).save(out + "mask_%02d.png" % i)
    uim = json.load(open(R + "anim/UIM_UI_Camp_In__UIM_UI_Camp_In.json"))
    off = np.array([k["value"] for k in uim["tracks"][0]["keys"]], np.float32)          # 75×170×2
    g = json.load(open(R + "json/Xrd777/UI/Camp/Root/Uim/UIM_UI_Camp_In.json")); g = g[0] if isinstance(g, list) else g
    gv = g["Properties"]["UimData"]["p2DGeomVertUV"]
    base = np.array([[v["x"], v["y"]] for v in gv], np.float32); uv = np.array([[v["u0"], v["v0"]] for v in gv], np.float32)
    rest = np.stack([uv[:, 0] * 1920 - 960, uv[:, 1] * 1184 - 556], -1)
    dev = (base[None] + off - rest[None]).reshape(75, 10, 17, 2)
    # 网格行是从下往上排的（第 0 行 v=1）：翻成从上往下，方便当贴图用
    order = np.argsort(uv.reshape(10, 17, 2)[:, 0, 1])
    dev[:, :, :, :] = dev[:, order]
    dev.astype(np.float32).tofile(out + "warp.bin")


def main():
    os.makedirs(OUT + "anim", exist_ok=True)
    for f in ["SK_PC0051_C002.glb", "SK_PC0051_F000.glb", "SK_PC0051_H000.glb"]:
        shutil.copy(R + "hero3d/model/" + f, OUT + f)
    shutil.copy(R + "commu_card/mesh/SK_Pp2001_000.glb", OUT + "SK_Pp2001_000.glb")
    os.makedirs(OUT + "tex", exist_ok=True)
    for f in ["T_PC0051_C002_00_Col.png", "T_PC0051_F000_00_Col.png", "T_PC0051_E000_00_Col.png"]:
        shutil.copy(R + "hero3d/textures/" + f, OUT + "tex/" + f)
    for f in ["T_Pp2001_030_0017.png", "T_Pp2001_Backside_00.png"]:
        shutil.copy(R + "commu_card/textures/" + f, OUT + "tex/" + f)
    # COMMU 页背景用：原版焦散贴图；原版 PLG 的 COMMU 字形（灰 165,165,172，2 倍分辨率，游戏里按 0.7 缩放）
    shutil.copy(R + "tex/Xrd777/UI/Camp/Root/Texture/T_UI_Camp_Caustics_00.png", OUT + "tex/T_UI_Camp_Caustics_00.png")
    commu_word(OUT + "tex/commu_word.png")
    dive(OUT + "dive/")
    tmp = tempfile.mkdtemp()
    for name, (num, split) in HERO.items():
        base = f"A_PC0051_UIA{num}_BASE_{name}"
        src = R + "hero3d/animations/" + base + ".psa"
        if split:
            src = os.path.join(tmp, base + ".psa")
            parts = sorted(p for p in os.listdir(R + "hero3d/animations_split") if p.startswith(base + ".psa.part"))
            with open(src, "wb") as o:
                for p in parts: o.write(open(R + "hero3d/animations_split/" + p, "rb").read())
        pre = OUT + "anim/" + name
        psa(src, pre, "tools/p3r/hero_keep_bones.json")
        j = fix_fps(pre, f"{R}json/Xrd777/Characters/Player/PC0051/AnmUI/{base}.json")
        sj = json.load(open(f"{R}hero3d/scale/{base}.json"))
        s = np.fromfile(f"{R}hero3d/scale/{base}.bin", np.float32).reshape(sj["frames"], len(sj["bones"]), 3)
        idx = [i for i in range(s.shape[1]) if np.abs(s[:, i] - 1).max() > 1e-3]
        s = s[:, idx][:, :, [0, 2, 1]]
        s.astype(np.float32).tofile(pre + ".scale.bin")
        j["scale_bones"] = [sj["bones"][i] for i in idx]
        json.dump(j, open(pre + ".json", "w"))
    for name, num in CARD.items():
        base = f"A_Pp2001_UIA{num}_BASE_{name}"
        pre = OUT + "anim/Card_" + name
        psa(R + "commu_card/animations/" + base + ".psa", pre)
        j = fix_fps(pre, R + "commu_card/animations/" + base + "_full.json")
        json.dump(j, open(pre + ".json", "w"))
    shutil.rmtree(tmp)
    print("staged", OUT)


if __name__ == "__main__":
    main()
