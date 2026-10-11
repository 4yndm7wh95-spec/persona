"""rig v7 召唤器试点：预览动图（Python 合成，不经过 Spine；验证节奏和切换手感）。

待机 → 凝神（举起召唤器）→ 觉醒（扣下：召唤器后坐、尖端闪光、眼中蓝光亮起又退去、身体被冲击顶一下）→ 召唤余势 → 待机。
每次换图：2 帧过渡（旧姿势残影 + 新姿势青色闪光），新姿势带一点惯性落位。
输出 art/rig_preview/v7_evoker_pilot.gif（30fps，半尺寸）。
"""
import math
import sys
from pathlib import Path

import cv2
import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
import v7_evoker_comp as comp  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
T = ROOT / "art/v12/transparent"
C = ROOT / "art/v12/comp"
FPS = 30
W, H = 1024, 1536
OUT_SCALE = 0.5


def load(p):
    return np.asarray(Image.open(p).convert("RGBA")).astype(np.float32) / 255


POSE = {
    "idle": load(T / "idle_a.png"),
    "focus": load(comp.POSES["focus"][0]),
    "awaken": load(comp.POSES["awaken"][0]),
    "cast": load(T / "cast_a.png"),
}
EVOKER = {n: load(C / f"{n}_evoker.png") for n in ("focus", "awaken")}
HAND = {n: load(C / f"{n}_hand.png") for n in ("focus", "awaken")}
MUZZLE = {n: comp.POSES[n][1] for n in ("focus", "awaken")}
GLOW = comp.eye_glow(*comp.EYE["awaken"])

# (开始时间, 姿势)
TIMELINE = [(0.0, "idle"), (0.5, "focus"), (1.0667, "awaken"), (1.5, "cast"), (2.2, "idle")]   # 切换落在整帧上
END = 2.75
FIRE = 1.05


def pose_at(t):
    cur = TIMELINE[0]
    for k in TIMELINE:
        if t >= k[0]:
            cur = k
    return cur


def shift(img, dx, dy, rot=0.0, center=None):
    if dx == 0 and dy == 0 and rot == 0:
        return img
    c = center or (W / 2, H)
    M = cv2.getRotationMatrix2D(c, rot, 1.0)
    M[:, 2] += (dx, dy)
    return cv2.warpAffine(img, M, (W, H), flags=cv2.INTER_LINEAR, borderValue=(0, 0, 0, 0))


def over(dst, src, k=1.0):
    a = src[..., 3:] * k
    out = dst.copy()
    out[..., :3] = src[..., :3] * a + dst[..., :3] * (1 - a)
    out[..., 3:] = a + dst[..., 3:] * (1 - a)
    return out


def add(dst, src, k=1.0):
    out = dst.copy()
    out[..., :3] = np.clip(dst[..., :3] + src[..., :3] * src[..., 3:] * k, 0, 1)
    return out


def silhouette(img, color):
    s = np.zeros_like(img)
    s[..., :3] = color
    s[..., 3] = img[..., 3]
    return s


def ease_out(x):
    return 1 - (1 - min(max(x, 0), 1)) ** 3


def render_pose(name, t, local):
    """一个姿势在 local 秒（进入该姿势后的时间）时的画面（带各自的小动作）。返回 (画面 RGBA, 发光 RGB)。
    发光层单独返回、在铺背景之后再加：光晕会晕出人物轮廓，不能只加在人物不透明的像素上。"""
    emit = np.zeros((H, W, 3), np.float32)
    dx = dy = 0.0
    if name == "idle":
        dy = 1.5 * math.sin(t * 2 * math.pi / 2.4)                 # 呼吸起伏（预览用位移代替，rig 里是网格）
    elif name == "focus":
        dy = 3.0 * ease_out(local / 0.5)                           # 凝神：重心微沉
    elif name == "awaken":
        k = math.exp(-local / 0.08) * (local >= 0)
        dx, dy = -8.0 * k, -3.0 * k                                 # 冲击把身体向后顶一下
    elif name == "cast":
        dx = 4.0 * ease_out(local / 0.6)
    base = POSE[name]
    if name in EVOKER:
        rot = 0.0
        if name == "awaken":
            rot = 9.0 * math.exp(-local / 0.07)                     # 后坐：尖端上扬再回落
        pivot = (MUZZLE[name][0] - 60, MUZZLE[name][1] + 20)           # 后坐绕握把（拳头里）转
        kick = -3.0 * math.exp(-local / 0.07) if name == "awaken" else 0.0
        layer = over(base, shift(EVOKER[name], 0, kick, rot, pivot))
        layer = over(layer, HAND[name])
        base = layer
    img = shift(base, dx, dy)
    if name == "awaken":
        g = min(local / 0.05, 1.0) * (1.0 if local < 0.18 else math.exp(-(local - 0.18) / 0.35))
        gl = shift(GLOW, dx, dy)
        emit += gl[..., :3] * gl[..., 3:] * g
        f = math.exp(-local / 0.05)
        if f > 0.02:
            ys, xs = np.mgrid[0:H, 0:W].astype(np.float32)
            mx, my = MUZZLE["awaken"][0] + dx, MUZZLE["awaken"][1] + dy
            d2 = (xs - mx) ** 2 + (ys - my) ** 2
            fl = np.clip(np.exp(-d2 / (2 * 22 ** 2)) + 0.4 * np.exp(-d2 / (2 * 70 ** 2)), 0, 1)
            emit += fl[..., None] * np.array([0.75, 0.95, 1.0], np.float32) * f
    return img, emit


def frame(t):
    start, name = pose_at(t)
    local = t - start
    fi = int(math.floor(local * FPS + 1e-6))   # 进入该姿势后的第几帧（整数，避免浮点比较）
    img, emit = render_pose(name, t, local)
    prev = None
    for j, k in enumerate(TIMELINE):
        if k[0] == start and j > 0:
            prev = TIMELINE[j - 1]
    if prev is not None and fi < 2:
        old, _ = render_pose(prev[1], t, t - prev[0])
        if fi == 0:                                                 # 第 1 帧：旧姿势残影 + 新姿势青色闪光
            img = add(img, silhouette(POSE[name], (0.35, 0.85, 1.0)), 0.3)
            img = over(img, old, 0.3)
        else:                                                       # 第 2 帧：新姿势淡白闪
            img = add(img, silhouette(POSE[name], (0.8, 0.95, 1.0)), 0.1)
    elif prev is not None and local < 0.18:                         # 旧姿势青色残影淡出
        old, _ = render_pose(prev[1], t, t - prev[0])
        ghost = silhouette(old, (0.3, 0.7, 1.0))
        ghost[..., 3] *= 0.35 * (1 - local / 0.18)
        bg = np.zeros_like(img)
        img = over(over(bg, ghost), img)
    return img, emit


def main():
    frames = []
    n = int(END * FPS)
    bgc = np.array([0.09, 0.12, 0.22])
    for k in range(n):
        t = k / FPS
        img, emit = frame(t)
        rgb = np.clip(img[..., :3] * img[..., 3:] + bgc * (1 - img[..., 3:]) + emit, 0, 1)
        im = Image.fromarray((rgb * 255).astype(np.uint8)).resize((int(W * OUT_SCALE), int(H * OUT_SCALE)), Image.LANCZOS)
        frames.append(im)
        print(k, end=" ", flush=True)
    out = ROOT / "art/rig_preview/v7_evoker_pilot.gif"
    frames[0].save(out, save_all=True, append_images=frames[1:], duration=int(1000 / FPS), loop=0, optimize=True)
    print("\n", out)


if __name__ == "__main__":
    main()
