"""Layout plan for the five Serpent Form serpents: current frame vs a fan layout (five necks rising out of the nest behind Joker,
each in its own sector, S-curved, heads swaying in small envelopes; two lower ones wrap once across his front).
Writes docs/p5/preview/serpent_layout_plan.png. Uses a rendered preview frame as the "now" panel."""
import math, subprocess, sys
from PIL import Image, ImageDraw, ImageFont
FONT = subprocess.check_output(["fc-match", "-f", "%{file}", "WenQuanYi Zen Hei"]).decode()
f_big = ImageFont.truetype(FONT, 30); f = ImageFont.truetype(FONT, 20); f_s = ImageFont.truetype(FONT, 17)
now = Image.open(sys.argv[1]).convert("RGB").crop((0, 130, 960, 830)).resize((640, 467))

W, H = 1320, 1060
im = Image.new("RGB", (W, H), (34, 30, 32)); d = ImageDraw.Draw(im)
d.text((24, 14), "五条蛇的分布和轨迹", font=f_big, fill=(240, 230, 225))
d.text((24, 62), "① 现在（视频截帧）", font=f, fill=(220, 215, 210))
im.paste(now, (24, 92))
y = 575
for t in ["· 每条蛇的头沿一个闭合的 8 字 / 椭圆兜圈，身体跟着头走过的路",
          "  → 身体会绕成一个个圈、打结，几条之间互相乱穿",
          "· 五个头的位置是随机的，有时全挤在上面，有时一边空",
          "· 没有一个整体的形，看不出是一个东西"]:
    d.text((24, y), t, font=f_s, fill=(200, 195, 190)); y += 28

# ② 方案
ox, oy, S = 670, 92, 1.0
d.text((ox, 62), "② 方案：五条蛇从身后的蛇巢里伸出来，展成一把扇子", font=f, fill=(220, 215, 210))
P = lambda x, y: (ox + 320 + x * 120, oy + 400 + y * 120)      # 单位：Joker 身高的一半；原点在 Joker 脚底
d.rectangle([ox, oy, ox + 640, oy + 467], fill=(70, 20, 24))
# 蛇巢
nx, ny = P(-0.15, -1.15)
d.ellipse([nx - 46, ny - 34, nx + 46, ny + 34], fill=(18, 8, 10), outline=(150, 40, 45))
d.text((nx - 30, ny - 10), "蛇巢", font=f_s, fill=(200, 120, 120))

def bez(p0, p1, p2, p3, n=40):
    pts = []
    for k in range(n + 1):
        t = k / n; u = 1 - t
        pts.append(tuple(u ** 3 * p0[i] + 3 * u * u * t * p1[i] + 3 * u * t * t * p2[i] + t ** 3 * p3[i] for i in (0, 1)))
    return pts

def catmull(pts, n=12):
    out = []
    for i in range(len(pts) - 1):
        p0 = pts[max(0, i - 1)]; p1 = pts[i]; p2 = pts[i + 1]; p3 = pts[min(len(pts) - 1, i + 2)]
        for k in range(n):
            t = k / n; t2 = t * t; t3 = t2 * t
            out.append(tuple(0.5 * (2 * p1[j] + (-p0[j] + p2[j]) * t + (2 * p0[j] - 5 * p1[j] + 4 * p2[j] - p3[j]) * t2 + (-p0[j] + 3 * p1[j] - 3 * p2[j] + p3[j]) * t3) for j in (0, 1)))
    out.append(pts[-1]); return out

# 五条：脊线（蛇巢 → 头，S 形），头的活动范围，标签，前后
N0 = (-0.15, -1.15)
snakes = [
    ([N0, (-0.55, -1.45), (-0.75, -2.0), (-1.3, -2.15), (-1.75, -2.7)], (0.25, 0.18), "左上：最高，头朝外上", "后"),
    ([N0, (0.05, -1.7), (-0.25, -2.25), (0.05, -2.75), (0.4, -3.0)], (0.22, 0.15), "正上：头在最高处朝敌人", "后"),
    ([N0, (0.45, -1.35), (0.7, -1.95), (1.15, -2.0), (1.7, -2.35)], (0.25, 0.2), "右上：朝敌人探出去", "后"),
    ([N0, (0.55, -1.0), (0.35, -0.55), (0.95, -0.45), (1.6, -0.7)], (0.22, 0.16), "右下：从他身前绕过、贴着腰朝敌人", "前"),
    ([N0, (-0.75, -0.95), (-0.45, -0.5), (-1.1, -0.4), (-1.7, -0.6)], (0.22, 0.16), "左下：从他身前绕到左边", "前"),
]
cols = [(230, 120, 110), (240, 170, 110), (230, 200, 120), (130, 200, 240), (160, 150, 240)]
def draw_snake(spine_pts, col):
    pts = [P(*q) for q in catmull(spine_pts)]
    left, right = [], []
    n = len(pts)
    for k, p_ in enumerate(pts):
        q = pts[min(k + 1, n - 1)]; r = pts[max(k - 1, 0)]
        tx, ty = q[0] - r[0], q[1] - r[1]; ln = math.hypot(tx, ty) or 1; nx, ny = -ty / ln, tx / ln
        s_ = 1 - k / (n - 1)          # 颈根（蛇巢里）→ 头：这里画的是颈部那一截，粗细基本一致
        w = 9 * (0.75 + 0.25 * min(1, k / 6))
        left.append((p_[0] + nx * w, p_[1] + ny * w)); right.append((p_[0] - nx * w, p_[1] - ny * w))
    d.polygon(left + right[::-1], fill=(14, 11, 13), outline=col)
    # 头：顺着末端方向的小楔形
    hx, hy = pts[-1]; px, py = pts[-4]; ang = math.atan2(hy - py, hx - px)
    head = [(-0.2, 0.55), (0.6, 0.5), (1.2, 0.12), (1.25, -0.1), (0.6, -0.45), (-0.2, -0.5)]
    sc = 16
    d.polygon([(hx + (x * math.cos(ang) - y * math.sin(ang)) * sc, hy + (x * math.sin(ang) + y * math.cos(ang)) * sc) for x, y in head], fill=(14, 11, 13), outline=col)
for (sp, (rx, ry), lab, fb), col in zip(snakes, cols):
    if fb == "后": draw_snake(sp, col)
jx, jy = P(0, 0)
d.rounded_rectangle([jx - 24, jy - 240, jx + 24, jy], radius=14, fill=(40, 40, 52), outline=(170, 170, 200))
d.text((jx - 26, jy + 6), "Joker", font=f_s, fill=(200, 200, 220))
for (sp, (rx, ry), lab, fb), col in zip(snakes, cols):
    if fb == "前": draw_snake(sp, col)
for (sp, (rx, ry), lab, fb), col in zip(snakes, cols):
    hx, hy = P(*sp[-1])
    d.ellipse([hx - rx * 120, hy - ry * 120, hx + rx * 120, hy + ry * 120], outline=col, width=2)
for lab_y, ((_sp, _r, lab, fb), col) in zip(range(575, 1000, 28), zip(snakes, cols)):
    d.text((ox, lab_y), f"■ {lab}（{'在 Joker 前面' if fb == '前' else '在身后'}）", font=f_s, fill=col)
y = 575 + 28 * 5 + 8
for t in ["· 圆圈 = 头的活动范围：头在里面慢慢漂、上下起伏",
          "  身体是一条 S 形，跟着轻轻波动，不再兜圈打结",
          "· 五个头错开高低、左右，整体是从身后展开的一把扇子",
          "  （八岐大蛇式的一体多头）",
          "· 下面两条从他身前横过去（挡住他的腰），上面三条在身后",
          "  → 有环绕感又不乱",
          "· 每条自己的节奏慢慢摆，不会五条同时一起动"]:
    d.text((ox, y), t, font=f_s, fill=(200, 195, 190)); y += 28
im.save("/home/user/persona/docs/p5/preview/serpent_layout_plan.png")
