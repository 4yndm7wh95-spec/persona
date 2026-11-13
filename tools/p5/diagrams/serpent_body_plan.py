"""Body proportion plan for the Serpent Form smoke serpents: width along the body (current vs proposed) and both silhouettes with the
diagram head. Unit W = neck width (the head plan's unit). Writes docs/p5/preview/serpent_body_plan.png."""
import math, os, subprocess
from PIL import Image, ImageDraw, ImageFont
FONT = subprocess.check_output(["fc-match", "-f", "%{file}", "WenQuanYi Zen Hei"]).decode()
f_big = ImageFont.truetype(FONT, 32); f = ImageFont.truetype(FONT, 21); f_s = ImageFont.truetype(FONT, 18)

def ss(a, b, x):
    t = min(1.0, max(0.0, (x - a) / (b - a))); return t * t * (3 - 2 * t)

# s：0 = 尾尖 → 1 = 颈根；返回全宽（单位 W）
def current(s):
    body = 0.08 + 0.92 * ss(0.0, 0.75, s) ** 0.8
    return 2 * 1.0 * body * (1 - 0.42 * ss(0.8, 0.97, s)) / 1.16
def proposed(s):
    body = 0.04 + (1.1 - 0.04) * ss(0.0, 0.55, s) ** 0.75          # 尾巴：后 55% 慢慢收到很细
    return body * (1 - (1 - 1.0 / 1.1) * ss(0.82, 0.97, s))        # 中段最粗 1.1 W，往颈根收到 1.0 W
HEAD_H = 1.3

UPPER = [(-0.1, 0.64), (0.35, 0.66), (0.78, 0.6), (0.95, 0.55), (1.25, 0.4), (1.47, 0.22), (1.56, 0.04), (1.53, -0.04), (1.36, -0.1), (1.1, -0.24), (0.7, -0.4), (0.35, -0.58), (0.05, -0.64), (-0.1, -0.6)]

W, H = 1200, 1330
im = Image.new("RGB", (W, H), (34, 30, 32)); d = ImageDraw.Draw(im)
d.text((24, 16), "蛇身比例（单位 W = 脖子粗细；蛇头高 ≈ 1.3 W）", font=f_big, fill=(240, 230, 225))

# ① 宽度曲线
ox, oy, sx, sy = 90, 350, 1000, 140
d.text((24, 70), "① 沿身体的粗细（右 = 头，左 = 尾尖）", font=f, fill=(220, 215, 210))
d.line([(ox, oy), (ox + sx, oy)], fill=(90, 85, 85), width=1)
for v in (0.5, 1.0, 1.5):
    y = oy - v * sy; d.line([(ox, y), (ox + sx, y)], fill=(60, 56, 58), width=1); d.text((ox - 50, y - 10), f"{v:.1f}W", font=f_s, fill=(150, 145, 140))
yh = oy - HEAD_H * sy; d.line([(ox + sx - 60, yh), (ox + sx + 40, yh)], fill=(255, 160, 80), width=3); d.text((ox + sx - 130, yh - 28), "蛇头高 1.3W", font=f_s, fill=(255, 170, 100))
for fn, col, lab, yl in ((current, (200, 80, 80), "现在：最粗 1.7W，比头还粗三成；很早就变到最粗", 368), (proposed, (120, 200, 255), "改后：最粗 1.1W（头的八成多），后半段慢慢收细", 396)):
    pts = [(ox + sx * k / 200, oy - fn(k / 200) * sy) for k in range(201)]
    d.line(pts, fill=col, width=4); d.text((ox + 10, yl), "—— " + lab, font=f, fill=col)

# ② / ③ 剪影
def silhouette(fn, y0, title, notes, col_out):
    d.text((24, y0), title, font=f, fill=(220, 215, 210))
    S = 34.0; L = 28.0                                   # 每 W 多少像素；身长 28 W
    cx0, cy0 = 70, y0 + 230
    def spine(u):                                        # u：0 尾尖 → 1 颈根
        x = u * L; return (cx0 + x * S, cy0 + math.sin(u * math.pi * 2.2 + 0.4) * 2.6 * S * (0.4 + 0.6 * u))
    pts = [spine(k / 300) for k in range(301)]
    left, right = [], []
    for k, p in enumerate(pts):
        q = pts[min(k + 1, 300)]; r = pts[max(k - 1, 0)]
        tx, ty = q[0] - r[0], q[1] - r[1]; n = math.hypot(tx, ty) or 1; nx, ny = -ty / n, tx / n
        w = fn(k / 300) / 2 * S
        left.append((p[0] + nx * w, p[1] + ny * w)); right.append((p[0] - nx * w, p[1] - ny * w))
    d.polygon(left + right[::-1], fill=(14, 11, 13), outline=col_out)
    # 头：沿颈根切线放
    p, q = pts[-1], pts[-4]; ang = math.atan2(p[1] - q[1], p[0] - q[0])
    hx, hy = p[0] + math.cos(ang) * 0.3 * S, p[1] + math.sin(ang) * 0.3 * S
    head = [(hx + (x * math.cos(ang) + y * math.sin(ang)) * S, hy + (x * math.sin(ang) - y * math.cos(ang)) * S) for x, y in UPPER]
    d.polygon(head, fill=(14, 11, 13), outline=col_out)
    ex, ey = hx + (0.82 * math.cos(ang) + 0.3 * math.sin(ang)) * S, hy + (0.82 * math.sin(ang) - 0.3 * math.cos(ang)) * S
    d.ellipse([ex - 6, ey - 4, ex + 6, ey + 4], fill=(255, 140, 60))
    y = y0 + 400
    for n_ in notes: d.text((24, y), n_, font=f_s, fill=(200, 195, 190)); y += 28

silhouette(current, 440, "② 现在", ["身子比头还粗，整条看起来像一根粗管子；尾巴很短就粗起来"], (200, 80, 80))
silhouette(proposed, 860, "③ 改后", ["头最宽，脖子略窄（1.0W），身子中段最粗 1.1W，比头窄一些",
                          "从身长一半往尾巴慢慢收细，最后一段又细又长；身长不变（约 28W），看起来更修长"], (120, 200, 255))
im.save("/home/user/persona/docs/p5/preview/serpent_body_plan.png")
