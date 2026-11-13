"""Serpent Form: small tweak of the five serpents' swimming paths (user 2026-11-11: no fan - keep them swimming loosely around Joker
like before, only fix where they sit and how they move). Plots each head's path over 20 s with the same formula as
SerpentMantle.Orbit (old vs new parameters) around a Joker-sized box, plus the head positions at 4 moments.
Writes docs/p5/preview/serpent_orbit_tweak.png."""
import math, subprocess
from PIL import Image, ImageDraw, ImageFont

OLD = [(0.15, -1.4, 0.5, 0.3, 0.42, 1, 0.0, 28, 0.6, -0.45), (-0.4, -0.9, 0.45, 0.35, -0.55, 1, 1.3, -42, 1, 0.05),
       (0.5, -1.0, 0.38, 0.42, 0.36, 2, 2.4, 12, 1, -0.2), (-0.05, -0.42, 0.72, 0.16, 0.46, 1, 3.6, -10, 1, 0.05),
       (-0.3, -1.72, 0.38, 0.26, -0.38, 1.5, 4.8, 35, 0.5, -0.5)]
# new: same kind of paths (tilted ellipse / figure eight with a slow drift), but each serpent keeps to its own part of the space
# around Joker (upper left behind, top behind, toward the enemies, low across his front, left middle), smaller loops, and the same
# angular speed so they never bunch up on one side
NEW = [(-0.48, -1.3, 0.28, 0.18, 0.45, 1, 0.0, 25, 0.5, -0.45), (0.32, -1.55, 0.3, 0.15, -0.42, 1, 1.9, -15, 0.4, -0.5),
       (0.66, -0.82, 0.24, 0.22, 0.48, 2, 3.4, 15, 0.8, -0.15), (0.05, -0.45, 0.48, 0.1, -0.45, 1, 4.6, -8, 0.9, 0.3),
       (-0.55, -0.24, 0.26, 0.09, 0.44, 1, 5.7, -12, 0.8, 0.0)]
COLS = [(230, 110, 100), (240, 170, 100), (235, 205, 110), (120, 190, 240), (170, 140, 235)]
NAMES = ["左上（身后，头顶高度）", "右上（身后，头顶以上）", "右侧胸口高度朝敌人", "腰胯横过身前", "左下膝盖高度"]

def orbit(o, i, t):
    cx, cy, ax, ay, w, r, ph, tilt, za, zb = o
    a = w * t + ph
    lx, ly = ax * math.sin(a), ay * math.sin(r * a + 1.57 + 0.6 * i)
    c, s = math.cos(-math.radians(tilt)), math.sin(-math.radians(tilt))
    lx, ly = lx * c - ly * s, lx * s + ly * c
    lx += 0.08 * math.sin(t * 0.23 + i * 2.1); ly += 0.1 * math.sin(t * 0.31 + i * 1.3)
    return cx + lx, cy + ly, math.cos(a) * za + zb

FONT = subprocess.check_output(["fc-match", "-f", "%{file}", "WenQuanYi Zen Hei"]).decode()
fb, f, fs = (ImageFont.truetype(FONT, n) for n in (30, 21, 17))
W, H = 1320, 820
im = Image.new("RGB", (W, H), (34, 30, 32)); d = ImageDraw.Draw(im)
d.text((24, 14), "五条蛇的位置和轨迹：小幅调整（还是围着 Joker 游，不展成扇子）", font=fb, fill=(240, 230, 225))
for col, (title, params) in enumerate((("现在", OLD), ("调整后", NEW))):
    ox, oy = 24 + col * 650, 70
    d.rectangle([ox, oy, ox + 630, oy + 560], fill=(70, 20, 24))
    d.text((ox + 12, oy + 8), title, font=f, fill=(235, 225, 220))
    P = lambda x, y: (ox + 330 + x * 190, oy + 520 + y * 190)
    jx0, jy0 = P(-0.17, -1.0); jx1, jy1 = P(0.17, 0.0)
    d.rounded_rectangle([jx0, jy0, jx1, jy1], 18, fill=(40, 40, 52), outline=(150, 150, 170))
    d.text((jx0 + 4, jy1 + 4), "Joker", font=fs, fill=(200, 200, 210))
    d.text((ox + 520, oy + 500), "敌人 →", font=fs, fill=(200, 180, 170))
    for i, o in enumerate(params):
        pts = [orbit(o, i, k * 0.05) for k in range(400)]
        for k in range(len(pts) - 1):
            x0, y0, z0 = pts[k]; x1, y1, _ = pts[k + 1]
            c = COLS[i] if z0 > 0 else tuple(int(v * 0.55) for v in COLS[i])
            d.line([P(x0, y0), P(x1, y1)], fill=c, width=3 if z0 > 0 else 2)
        for tt in (0, 4, 8, 12):
            x, y, z = orbit(o, i, tt)
            px, py = P(x, y)
            d.ellipse([px - 6, py - 6, px + 6, py + 6], fill=COLS[i])
y = 650
for i in range(5):
    d.rectangle([24, y + 4, 40, y + 20], fill=COLS[i]); d.text((48, y), NAMES[i], font=fs, fill=COLS[i]); y += 28 if i != 2 else 28
notes = ["· 线 = 头 20 秒内游过的路线；亮线 = 在 Joker 前面，暗线 = 在身后；圆点 = 同一时刻五个头的位置（4 个时刻）",
         "· 调整后：上下也匀开——头顶以上两条、胸口一条、腰胯一条、膝盖一条，兜的圈小一些，五条转得一样快，",
         "  所以不会一会儿全挤到上面、一会儿一边空；身体跟着头走过的路，圈小了就不会绕成结",
         "· 低处那条从他身前横过去（挡住腰），其余在身后；其它（蛇的样子、蛇巢、攻击、出场）都不变"]
y = 650
for t in notes:
    d.text((260, y), t, font=fs, fill=(205, 200, 195)); y += 30
im.save("docs/p5/preview/serpent_orbit_tweak.png")
print("ok")
