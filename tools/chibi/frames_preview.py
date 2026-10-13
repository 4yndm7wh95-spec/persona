"""Q 版整图换帧小样：待机呼吸 → 溶解到挥砍（前冲顿帧）→ 溶解回待机。输出 mp4。"""
import sys, math, subprocess
from pathlib import Path
import numpy as np, cv2
from PIL import Image
ROOT = Path(__file__).resolve().parents[2]
F = ROOT / "art/chibi/frames"
OUT = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / "art/chibi/proto/frames_preview.mp4"
W = H = 2048
FEET = (1020, 1900)
idle = np.array(Image.open(F / "idle_a.png").convert("RGBA")).astype(np.float32) / 255
slash = np.array(Image.open(F / "slash_a.png").convert("RGBA")).astype(np.float32) / 255

def breathe(img, s):
    """呼吸：以脚底为基准纵向轻微伸缩，越往上位移越大（头最明显），腿几乎不动。"""
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    w = np.clip((FEET[1] - yy) / (FEET[1] - 250), 0, 1) ** 1.5
    mapy = yy + s * 14 * w
    mapx = xx - s * 3 * w * (xx - FEET[0]) / 400
    return cv2.remap(img, mapx, mapy, cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT, borderValue=0)

def shift(img, dx, dy=0):
    M = np.float32([[1, 0, dx], [0, 1, dy]]); return cv2.warpAffine(img, M, (W, H), borderValue=0)

def comp(img):
    bg = np.empty((H, W, 3), np.float32); bg[...] = (48 / 255, 52 / 255, 68 / 255)
    a = img[..., 3:4]; rgb = img[..., :3] * a + bg * (1 - a)
    c = (rgb[150:1960, 160:1880] * 255).astype(np.uint8)
    return cv2.resize(c, (860, 906), interpolation=cv2.INTER_AREA)

def blend(a, b, t):
    """溶解：新图在下完全显示，旧图在上淡出（和写实版 v7 一样，不会两张都半透明）。"""
    out = b.copy(); k = (1 - t)
    out[..., :3] = a[..., :3] * a[..., 3:4] * k + out[..., :3] * (1 - a[..., 3:4] * k)
    out[..., 3:4] = np.maximum(out[..., 3:4], a[..., 3:4] * k)
    return out

fps = 30; frames = []
for i in range(60):                                   # 2s 待机
    frames.append(comp(breathe(idle, math.sin(2 * math.pi * i / 60))))
for i in range(3):                                    # 0.1s 溶解到挥砍（带前冲位移）
    t = (i + 1) / 3; frames.append(comp(blend(idle, shift(slash, 40 * t), t)))
for i in range(15):                                   # 0.5s 挥砍：前冲后顿住，轻微回弹
    t = i / 14; frames.append(comp(shift(slash, 40 - 12 * (1 - math.exp(-6 * t)) + 4 * math.sin(t * 9) * math.exp(-5 * t))))
for i in range(4):                                    # 溶解回待机
    t = (i + 1) / 4; frames.append(comp(blend(shift(slash, 28), idle, t)))
for i in range(45):
    frames.append(comp(breathe(idle, math.sin(2 * math.pi * i / 60))))
p = subprocess.Popen(["ffmpeg", "-v", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", "860x906", "-r", str(fps), "-i", "-",
                      "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", "-movflags", "+faststart", str(OUT)], stdin=subprocess.PIPE)
for f in frames: p.stdin.write(f.tobytes())
p.stdin.close(); p.wait(); print(OUT, len(frames))
