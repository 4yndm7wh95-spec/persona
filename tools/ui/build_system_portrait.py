"""设置界面倒挂立绘：从 art/p3menu_system 派生接入素材（Codex 的生图没有完全满足"纯白"要求，这里按容差处理）。
· alpha < 12 的零星杂点清成全透明；
· 近白（alpha>0、RGB 最小值 >= 222、饱和度很低）统一成纯白 #FFFFFF —— 游戏里的融水着色器另外还按容差判断白色，两道保险；
· v0.14.70（用户实机反馈）：去掉脸右边多出来的那只手（前臂从袖口起整块抠掉，袖口补一道轮廓线）；
  眨眼帧不再用 Codex 的编辑图（只眨了一只眼，而且眼睑方向反了），改成从 base 程序生成：
  脸是倒着的，上眼睑（粗睫毛）在画面下方 —— 眨眼 = 睫毛带往画面上方抬、盖住虹膜；被头发半挡住的那只眼也一起眨；
· 三帧（base / 半睁 / 闭眼）同样处理，输出到 mod/pck_stage/p3makoto/ui/system_portrait*.png（带 .import / .ctex）；
  同时打印人物右侧轮廓折线（去掉手以后重算，SettingsSkin.Edge 用）。
用法：python3 tools/ui/build_system_portrait.py [预览输出.png]"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools/pck"))
import godot_tex  # noqa: E402

SRC = ROOT / "art/p3menu_system"
STAGE = ROOT / "mod/pck_stage"
OUT = {"open": "system_portrait.png", "half": "system_portrait_half.png", "closed": "system_portrait_closed.png"}


def remove_hand(im):
    """多出来的那只手：袖口（770,668）~（884,596）一线以下、和手相连的整块不透明区域抠成透明，再沿袖口补一道深蓝轮廓。"""
    a = np.asarray(im.convert("RGBA")).copy()
    poly = [(768, 670), (812, 661), (856, 650), (884, 596), (1030, 596), (1030, 1040), (890, 1040), (842, 950), (835, 930), (828, 880), (818, 830), (800, 760), (766, 680)]
    zone = Image.new("L", im.size, 0)
    ImageDraw.Draw(zone).polygon(poly, fill=255)
    zone = np.asarray(zone) > 0
    lab, _ = ndimage.label((a[..., 3] > 8) & zone)
    hand = lab == lab[900, 900]
    hand = ndimage.binary_dilation(hand, iterations=3) & zone
    a[hand, 3] = 0
    out = Image.fromarray(a, "RGBA")
    ImageDraw.Draw(out).line([(770, 668), (812, 659), (856, 648)], fill=(20, 52, 200, 255), width=4)
    return out


def blink(base, f):
    """f = 0 睁眼 … 1 闭眼。"""
    a0 = np.asarray(base).astype(np.float32)
    a = a0.copy()
    # 主眼（脸中下部，青色虹膜）：每一列把"眼睑线以下"的内容（粗睫毛带 + 下面的睫毛 / 阴影）往画面上方平移 d（亚像素插值），
    # 平移量中间大两边小（半椭圆），空出来的地方是眼皮（白）；右边压着的头发保持在上面
    xs = np.arange(446, 549)
    lid = np.interp(xs, [446, 470, 495, 520, 548], [930, 936, 940, 938, 934])
    lift = 46.0 * np.sqrt(1 - np.clip(np.abs(xs - 497) / 52.0, 0, 1) ** 2)
    bot = 968
    rows = np.arange(a.shape[0])
    for i, x in enumerate(xs):
        d = f * lift[i]
        if d <= 0.01:
            continue
        col = a0[:, x]
        ys = np.arange(int(lid[i] - d) - 1, bot)
        src = ys + d
        lo = np.floor(src).astype(int)
        t = (src - lo)[:, None]
        v = col[lo] * (1 - t) + col[np.minimum(lo + 1, a.shape[0] - 1)] * t
        v[src >= bot] = (255, 255, 255, 255)
        moved = ys >= lid[i] - d
        a[ys[moved], x] = v[moved]
        if x >= 530:
            hair = (col[:, 2] > 150) & (col[:, 1] < 120) & (col[:, 0] < 70) & (rows >= lid[i] - d - 1) & (rows < bot)
            a[hair, x] = col[hair]
    # 被头发半挡住的那只眼（左边，侧面）：开口（眼白 / 虹膜 / 瞳孔）和下方黑色睫毛楔形从下往上盖成眼皮，在新的眼睑线画一道两头尖的睫毛
    poly = [(347, 815), (352, 813), (360, 822), (368, 834), (372, 848), (370, 860), (362, 870), (356, 880), (349, 866), (346, 846), (345, 826)]
    m = Image.new("L", base.size, 0)
    ImageDraw.Draw(m).polygon(poly, fill=255)
    m = np.asarray(m) > 0
    r, g, b = a0[..., 0], a0[..., 1], a0[..., 2]
    hair = (b > 170) & (g < 110) & (r < 60)
    yb = 858 - f * 38
    yy = rows[:, None].repeat(a.shape[1], 1)
    a[m & ~hair & (yy > yb - 1)] = (255, 255, 255, 255)
    big = Image.new("RGBA", (320, 360), (0, 0, 0, 0))
    d = ImageDraw.Draw(big)
    ox, oy = 340, 800
    xl, xr = 346.5, 371 - (8 * f if f > 0.8 else 2 * f)
    thick = 6.0 + 1.5 * f
    pts = []
    for k in range(21):
        u = k / 20
        pts.append((xl + (xr - xl) * u, yb + 2.0 - 3.0 * np.sin(np.pi * u) - 2.0 * u, thick * np.sin(np.pi * min(1, u * 1.15 + 0.05)) ** 0.6))
    top = [((x - ox) * 4, (y - t * 0.5 - oy) * 4) for x, y, t in pts]
    low = [((x - ox) * 4, (y + t * 0.5 - oy) * 4) for x, y, t in pts][::-1]
    d.polygon(top + low, fill=(8, 10, 30, 255))
    for u, ln in ((0.3, 4), (0.55, 5), (0.78, 4)):
        x = xl + (xr - xl) * u
        y = yb + 2.0 - 3.0 * np.sin(np.pi * u) - 2.0 * u
        d.line([((x - ox) * 4, (y - oy) * 4), ((x - 1.5 - ox) * 4, (y + ln - oy) * 4)], fill=(8, 10, 30, 255), width=5)
    big = big.resize((80, 90), Image.LANCZOS)
    out = Image.fromarray(a.clip(0, 255).astype(np.uint8), "RGBA")
    out.alpha_composite(big, (ox, oy))
    o = np.asarray(out).copy()
    o[hair & m] = np.asarray(base)[hair & m]
    return Image.fromarray(o, "RGBA")


def edge_polyline(im):
    """人物右侧外轮廓：11 行（0, 108, … 1079）每行最右边 alpha>127 的像素。"""
    al = np.asarray(im)[..., 3]
    pts = []
    for y in [0, 108, 216, 324, 432, 540, 648, 756, 864, 972, 1079]:
        xs = np.where(al[y] > 127)[0]
        pts.append((int(xs.max()) if len(xs) else 0, y))
    return pts


def clean(im):
    a = np.asarray(im.convert("RGBA")).astype(np.int32)
    rgb, al = a[..., :3], a[..., 3]
    al[al < 12] = 0
    mn, mx = rgb.min(axis=2), rgb.max(axis=2)
    white = (al > 0) & (mn >= 222) & ((mx - mn) <= 26)
    rgb[white] = 255
    rgb[al == 0] = 0
    out = np.dstack([rgb, al]).clip(0, 255).astype(np.uint8)
    return Image.fromarray(out, "RGBA"), int(white.sum())


def main(preview=None):
    done = {}
    base = remove_hand(Image.open(SRC / "system_base.png"))
    print("edge", edge_polyline(base))
    frames = {"open": base, "half": blink(base, 0.5), "closed": blink(base, 1.0)}
    for key, dst in OUT.items():
        im, n = clean(frames[key])
        godot_tex.write_texture(STAGE, f"p3makoto/ui/{dst}", im)
        done[dst] = (im, n)
        print(dst, im.size, "snapped-to-white", n)
    if preview:
        base = done["system_portrait.png"][0]
        bg = Image.new("RGBA", base.size, (20, 70, 200, 255))
        bg.paste((255, 255, 255, 255), (0, 0, 820, base.height))
        bg.alpha_composite(base)
        bg.convert("RGB").resize((960, 540)).save(preview)


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else None)
