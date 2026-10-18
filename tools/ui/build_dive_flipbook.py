"""v0.14.73 设置界面入水转场的水花逐帧图：直接从用户录的 P3R 60fps 分镜（art/p3menu_system/ref/menu_transition_frames_020-107）
里抠出原版水的轮廓（用户：自己拼的水柱形状、排布都不自然 —— 照原版画）。
每帧一张 800×500 的图（分镜是 16:10，游戏里拉满全屏）：
  R = 水（水柱、泡沫、白色水花云、气泡）的覆盖（下采样自带抗锯齿，游戏里再锐化）
  G = 色调：0.15 = 天青半透明（泡沫暗面、气泡），0.4~1 = 浅青 → 近白（保留原版的明暗块）
      （不用 alpha 通道 —— 无损 WebP 会丢掉 alpha=0 处的颜色）
  B = V 形水团（深蓝的水体，加上浅青的水）范围 —— 设置界面只在这个范围里显示；#38 起整屏
帧 20~52（原视频 0.333~0.867s）。录像是隔帧重复的（实际 30fps）：和上一帧一样的帧不输出，游戏里按存在的帧号插值。剔除：右上 / 左上的界面框和文字、#41 起人物所在的左边、店铺背景里的零星杂色。
用法：python3 tools/ui/build_dive_flipbook.py [预览输出.png]"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools/pck"))
import godot_tex  # noqa: E402

REF = ROOT / "art/p3menu_system/ref/menu_transition_frames_020-107/storyboards"
STAGE = ROOT / "mod/pck_stage"
FIRST, LAST = 20, 52
OUT_W, OUT_H = 800, 500
CELLS = [(24, 138), (1968, 138), (24, 1090), (1968, 1090)]   # 每张分镜 2×2 帧，单帧 1920×884
CONTENT = (244, 0, 1658, 884)                                   # 单帧里去掉左右黑边后的画面（1414×884，16:10）


def frame(i):
    page, cell = (i - 20) // 4 + 1, (i - 20) % 4
    sb = Image.open(REF / f"storyboard_{page:02d}.png").convert("RGB")
    x, y = CELLS[cell]
    return np.asarray(sb.crop((x, y, x + 1920, y + 884)).crop(CONTENT)).astype(np.int32)


def masks(i):
    """返回 (覆盖, 色调, 水团)。色调：0.15 = 天青半透明（泡沫的暗面、气泡），0.4~1 = 浅青 → 近白。"""
    a = frame(i)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    h, w = r.shape
    yy, xx = np.mgrid[0:h, 0:w]
    pale = (b > 200) & (g > 170) & (r > 125) & (b - r > 35)      # 浅青（偏蓝），纯白的界面框 / 文字天然不在里面
    # 录像上的水印 / 界面：左上"毒红豆汤 bilibili"和金额框、左下"人正在看"、右下按键提示
    ui = ((xx < 0.25 * w) & (yy < 0.16 * h)) | ((xx > 0.83 * w) & (yy > 0.84 * h)) | ((xx < 0.12 * w) & (yy > 0.93 * h))
    if i <= 34:
        pale |= (r > 215) & (g > 230) & (b > 235) & ~ui           # 白色水花云（#24~#34）
    pale &= ~(ui & (yy < 0.07 * h)) & ~((xx < 0.12 * w) & (yy > 0.93 * h))   # 水印字
    light = np.clip((r - 125) / 130.0, 0.0, 1.0)
    # v0.14.74（用户：泡沫里有很多透明 / 空心的地方）：原版泡沫的暗面和气泡是天青色的半透明块（约 RGB 75~120, 175~210, 240+），单独一档
    mid = (b > 215) & (g > 145) & (r > 40) & (r <= 140) & (b - r > 90) & ~pale
    if i >= 36:
        gb = ndimage.uniform_filter(g.astype(float), 31)
        mid &= g - gb > 22                                          # 整屏都是水以后，只取比周围亮一截的（气泡），不取水本身
    mid &= ~((xx < 0.12 * w) & (yy > 0.93 * h)) & ~((xx > 0.83 * w) & (yy > 0.84 * h))
    cover = pale | mid
    lab, n = ndimage.label(cover)
    if n:
        size = ndimage.sum(np.ones_like(lab), lab, index=np.arange(1, n + 1))
        cover &= np.isin(lab, np.arange(1, n + 1)[size >= 12])    # 去掉零星杂点
    # 小的空洞补上（抠图阈值漏掉的描边 / 阴影）：面积 < 700 的洞填成天青
    filled = ndimage.binary_fill_holes(cover)
    holes, hn = ndimage.label(filled & ~cover)
    if hn:
        hs = ndimage.sum(np.ones_like(holes), holes, index=np.arange(1, hn + 1))
        cover |= np.isin(holes, np.arange(1, hn + 1)[hs < 700])
    tone = np.where(pale, 0.4 + 0.6 * light, 0.15)
    if i >= 38:
        body = np.ones_like(cover)
    else:
        blue = (b > 150) & (r < 90) & (g < 170) & (b - r > 110)
        body = ndimage.binary_closing(blue | cover, iterations=6)
        lab, n = ndimage.label(body)
        if n:
            size = ndimage.sum(np.ones_like(lab), lab, index=np.arange(1, n + 1))
            body = lab == (1 + int(np.argmax(size)))
        body = ndimage.binary_fill_holes(body)
        body |= cover & (yy < 0.5 * h)                            # 开场往下泼的泡沫也算在水里
    fade = np.ones(cover.shape)
    if i >= 40:
        # 人物进画面以后：人物的浅青头发 / 衣服不算水 —— 和人物白色部分大面积挨着的浅青块去掉（水柱是从人物前面穿过的大块，挨着的比例很小，保留）
        char = ndimage.binary_dilation((r > 225) & (g > 225) & (b > 225) & (xx < 0.5 * w), iterations=18)
        lab, n = ndimage.label(cover)
        if n:
            idx = np.arange(1, n + 1)
            tot = ndimage.sum(np.ones_like(lab), lab, index=idx)
            hit = ndimage.sum(char, lab, index=idx)
            cover &= ~np.isin(lab, idx[hit / np.maximum(tot, 1) > 0.25])
    if i >= 44:
        # 菜单字（QUEST / COMMU …，天青色）从这时起出现在画面中下部，不是水
        cover &= ~((xx > 0.40 * w) & (xx < 0.78 * w) & (yy > 0.40 * h) & (tone < 0.3))
    return cover * fade, tone * cover * fade, body


def build(preview=None):
    shots, prev, kept = [], None, []
    for old in (STAGE / "p3makoto/ui/dive").glob("dive_*") if (STAGE / "p3makoto/ui/dive").exists() else []:
        old.unlink()
    for old in (STAGE / ".godot/imported").glob("dive_*.ctex"):
        old.unlink()
    for i in range(FIRST, LAST + 1):
        cover, tone, body = masks(i)
        rgb = np.dstack([cover * 255.0, tone * 255.0, body * 255.0]).astype(np.uint8)
        if prev is not None and np.abs(rgb.astype(int) - prev.astype(int)).mean() < 0.5:
            continue
        prev = rgb
        im = Image.fromarray(rgb, "RGB").resize((OUT_W, OUT_H), Image.LANCZOS)
        godot_tex.write_texture(STAGE, f"p3makoto/ui/dive/dive_{i:03d}.png", im)
        shots.append(im)
        kept.append(i)
    print("frames kept:", kept)
    print(f"dive flipbook: {len(shots)} frames {OUT_W}x{OUT_H}")
    if preview:
        cols = 6
        sheet = Image.new("RGB", (OUT_W // 2 * cols, OUT_H // 2 * ((len(shots) + cols - 1) // cols)))
        for k, im in enumerate(shots):
            sheet.paste(im.resize((OUT_W // 2, OUT_H // 2)), (k % cols * OUT_W // 2, k // cols * OUT_H // 2))
        sheet.save(preview)


if __name__ == "__main__":
    build(sys.argv[1] if len(sys.argv) > 1 else None)
