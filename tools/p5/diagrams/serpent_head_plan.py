"""Structure plan for the Serpent Form smoke serpent head (side view): proportions, eye, mouth line, jaw hinge, fangs; closed / slightly open / strike. Writes docs/p5/preview/serpent_head_plan.png."""
import math
from PIL import Image, ImageDraw, ImageFont
FONT = "/usr/share/fonts/truetype/wqy/wqy-zenhei.ttc"
import os
if not os.path.exists(FONT):
    import subprocess; FONT = subprocess.check_output(["fc-match", "-f", "%{file}", "WenQuanYi Zen Hei"]).decode()
f_big = ImageFont.truetype(FONT, 34); f = ImageFont.truetype(FONT, 22); f_s = ImageFont.truetype(FONT, 18)

def catmull(pts, n=16):
    out = []
    for i in range(len(pts) - 1):
        p0 = pts[max(0, i - 1)]; p1 = pts[i]; p2 = pts[i + 1]; p3 = pts[min(len(pts) - 1, i + 2)]
        for k in range(n):
            t = k / n; t2 = t * t; t3 = t2 * t
            out.append(tuple(0.5 * (2 * p1[j] + (-p0[j] + p2[j]) * t + (2 * p0[j] - 5 * p1[j] + 4 * p2[j] - p3[j]) * t2 + (-p0[j] + 3 * p1[j] - 3 * p2[j] + p3[j]) * t3) for j in (0, 1)))
    out.append(pts[-1]); return out

# 模型坐标：单位 = 脖子粗细 W，x 朝前（吻尖在右），y 朝上
UPPER = [(-0.95, 0.5), (-0.45, 0.52), (-0.1, 0.64), (0.35, 0.66), (0.78, 0.6), (0.95, 0.55), (1.25, 0.4), (1.47, 0.22), (1.56, 0.04)]   # 脖子上沿 → 后脑 → 头顶 → 眉骨 → 吻尖
MOUTH = [(1.53, -0.04), (1.2, -0.08), (0.75, -0.1), (0.35, -0.13), (0.14, -0.2), (0.06, -0.3)]                                     # 嘴线：吻尖下 → 眼睛后下方 → 弯进颚角
LOWER = [(1.36, -0.1), (1.1, -0.24), (0.7, -0.4), (0.3, -0.58), (0.02, -0.66), (-0.3, -0.6), (-0.55, -0.5), (-0.95, -0.5)]               # 下巴 → 颚下沿 → 颚角 → 脖子下沿
HINGE = (0.06, -0.3)
EYE = (0.82, 0.3)

def rot(p, c, a):
    s, co = math.sin(a), math.cos(a); x, y = p[0] - c[0], p[1] - c[1]
    return (c[0] + co * x - s * y, c[1] + s * x + co * y)

def panel(title, open_deg, up_deg, notes, labels):
    W, H = 900, 640; S = 300; ox, oy = 330, 330
    im = Image.new("RGB", (W, H), (34, 30, 32)); d = ImageDraw.Draw(im)
    P = lambda p: (ox + p[0] * S, oy - p[1] * S)
    up = catmull(UPPER); mouth = catmull(MOUTH)
    a_low = -math.radians(open_deg); a_up = math.radians(up_deg)
    U = lambda p: rot(p, HINGE, a_up)
    L = lambda p: rot(p, HINGE, a_low)
    # 不动的部分（上颚 + 后脑 + 脖子）：脖子上沿 → 头顶 → 吻尖 → 嘴线往后到铰点 → 后脑下沿 → 脖子下沿
    fixed = [U(p) for p in up] + [U(p) for p in mouth] + [(0.0, -0.56), (-0.3, -0.56), (-0.6, -0.5), (-0.95, -0.5)]
    # 下颚：嘴线（下巴 → 铰点）+ 下沿（铰点 → 颚角 → 下巴），整块绕铰点转
    jaw_top = catmull([(1.36, -0.1), (1.2, -0.085), (0.75, -0.1), (0.35, -0.13), (0.14, -0.2), HINGE])
    jaw_under = catmull([HINGE, (-0.02, -0.5), (0.05, -0.64), (0.35, -0.58), (0.75, -0.4), (1.12, -0.24), (1.36, -0.1)])
    jaw = [L(p) for p in jaw_top] + [L(p) for p in jaw_under]
    # 口腔：两条嘴线之间，只填靠嘴角的那一段（往吻尖是空的、看得见后面）
    if open_deg > 0:
        # 口腔：沿嘴线一条条往吻尖淡掉（嘴角最深，往前渐渐透出后面）
        um = [p for p in mouth][::-1]
        jm = jaw_top[::-1]
        n = min(len(um), len(jm))
        for k in range(n - 1):
            x = um[k][0]
            fade = max(0.0, min(1.0, (1.35 - x) / 0.6))
            col = tuple(int(34 + (c - 34) * fade) for c in (70, 12, 18)) if fade < 1 else (70, 12, 18)
            col = tuple(int(34 * (1 - fade) + c * fade) for c in (70, 12, 18))
            quad = [U(um[k]), U(um[k + 1]), L(jm[min(k + 1, n - 1)]), L(jm[k])]
            d.polygon([P(p) for p in quad], fill=col)
    d.polygon([P(p) for p in jaw], fill=(12, 10, 12), outline=(150, 40, 45))
    d.polygon([P(p) for p in fixed], fill=(12, 10, 12), outline=(150, 40, 45))
    for p0, p1 in ((jaw_top[0], jaw_top[0]),):
        pass
    # 嘴线（闭合时）
    if open_deg == 0:
        d.line([P(p) for p in mouth], fill=(200, 70, 70), width=3)
    # 眉骨 + 眼睛
    e = U(EYE)
    d.line([P(U((0.62, 0.5))), P(U((0.85, 0.52))), P(U((1.02, 0.44)))], fill=(150, 40, 45), width=4)
    ex, ey = P(e); d.ellipse([ex - 18, ey - 11, ex + 18, ey + 11], fill=(255, 140, 60)); d.line([ex, ey - 10, ex, ey + 10], fill=(20, 0, 0), width=4)
    # 獠牙：上排（近 / 远）、下排（近 / 远）
    def fang(base, length, ang, src, col, wid):
        pts = []
        for k in range(11):
            t = k / 10; pts.append((base[0] - 0.12 * length * t * t, base[1] + ang * length * t))
        pts = [src(p) for p in pts]
        for k in range(10):
            w = wid * (1 - k / 10) + 1
            d.line([P(pts[k]), P(pts[k + 1])], fill=col, width=int(w))
    if open_deg > 0:
        gap = lambda x: (x - HINGE[0]) * math.tan(math.radians(open_deg + up_deg))
        fang((1.24, -0.085), min(0.36, gap(1.24) * 0.62), -1, U, (225, 205, 190), 9)      # 上排近
        fang((1.14, -0.09), min(0.28, gap(1.14) * 0.5), -1, U, (120, 95, 90), 7)          # 上排远（稍后、暗）
        fang((1.02, -0.1), min(0.22, gap(1.02) * 0.4), 1, L, (200, 180, 165), 7)          # 下排近
        fang((0.93, -0.105), min(0.17, gap(0.93) * 0.32), 1, L, (110, 85, 80), 6)         # 下排远
    # 铰点
    hx, hy = P(HINGE); d.ellipse([hx - 7, hy - 7, hx + 7, hy + 7], outline=(120, 200, 255), width=3)
    # 标注
    d.text((20, 18), title, font=f_big, fill=(240, 230, 225))
    for (txt, at, to) in labels:
        a = P(U(at) if at[1] > -0.11 or open_deg == 0 else L(at)) if isinstance(at, tuple) else at
        d.line([a, to], fill=(120, 200, 255), width=2); d.text((to[0] + 6, to[1] - 12), txt, font=f, fill=(170, 220, 255))
    y = H - 30 * len(notes) - 10
    for n in notes:
        d.text((20, y), n, font=f_s, fill=(200, 195, 190)); y += 30
    return im

common = [("眉骨（压住眼睛）", (0.85, 0.53), (560, 70)), ("眼睛：头长 40% 处、靠上", EYE, (640, 150)),
          ("吻尖：圆钝", (1.56, 0.04), (820, 300)), ("颚角铰点（下颚绕它转）", HINGE, (40, 470)),
          ("后脑最宽", (-0.1, 0.64), (40, 120)), ("细脖子", (-0.8, 0.5), (40, 200))]
a = panel("① 闭嘴：比例和轮廓", 0, 0,
    ["头长 ≈ 脖子粗细 × 1.6～1.9；后脑高 ≈ × 1.25；头比脖子宽约三成",
     "嘴线从吻尖下方几乎水平往后，到眼睛后下方再弯进颚角", "上颚（含眼睛、后脑）占头高 2/3，下颚只占 1/3，下巴比吻尖略缩"],
    common + [("嘴线", (0.75, -0.1), (560, 420)), ("下颚：后厚前薄、下巴略缩", (0.7, -0.4), (560, 500))])
b = panel("② 平时：微微张嘴（下颚转 12°）", 12, 2,
    ["只有下颚绕颚角往下转，缺口是尖朝后、口朝前的三角", "里面是暗红的口腔；上排一对长獠牙在吻尖稍后，下排一对短的稍靠后、和上排交错",
     "每对左右两颗：近的亮、长；远的稍靠后、暗（立体感）"],
    [("颚角铰点", HINGE, (40, 470)), ("上排獠牙（近 / 远）", (1.2, -0.25), (640, 470)), ("下排獠牙（近 / 远）", (1.0, -0.25), (560, 540))])
c = panel("③ 扑咬：张大（下颚 40°、上颚抬 8°）", 40, 8,
    ["扑出去时张到最大，上颚也往上抬一点；咬中那一下合上", "烟雾只在后脑、脖子散开；吻部、颚、嘴线的轮廓保持干净"],
    [("颚角铰点", HINGE, (40, 470))])
out = Image.new("RGB", (900, 640 * 3), (0, 0, 0))
for i, im in enumerate((a, b, c)): out.paste(im, (0, 640 * i))
out.save("/home/user/persona/docs/p5/preview/serpent_head_plan.png")
