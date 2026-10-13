"""Q 版骨骼可行性小样：直接从 design_a 切成几大块（像素完全来自设定图，不重画），
被手臂 / 头挡住的地方用 OpenCV 修补，然后用 2D 仿射变换渲染几段动画 GIF。"""
import sys, math
from pathlib import Path
import numpy as np, cv2
from PIL import Image, ImageDraw
ROOT = Path(__file__).resolve().parents[2]
OUT = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / "art/chibi/proto"
OUT.mkdir(parents=True, exist_ok=True)
D = np.array(Image.open(ROOT / "art/chibi/design/design_a.png").convert("RGBA"))
H, W = D.shape[:2]
A = D[..., 3] > 0

def poly_mask(pts):
    im = Image.new("L", (W, H), 0); ImageDraw.Draw(im).polygon(pts, fill=255)
    return np.array(im) > 0

ARM_L = [(845,965),(900,985),(905,1100),(905,1225),(880,1345),(870,1390),(875,1470),(855,1500),(780,1500),(752,1460),(756,1380),(733,1350),(758,1250),(788,1140),(813,1040)]
ARM_R_UP = [(1172,962),(1215,972),(1252,1040),(1272,1190),(1285,1232),(1212,1250),(1200,1215),(1178,1170),(1170,1060)]
ARM_R_LO = [(1285,1225),(1312,1290),(1332,1345),(1338,1415),(1302,1494),(1262,1484),(1232,1440),(1238,1350),(1216,1300),(1206,1238)]
yy, xx = np.mgrid[0:H, 0:W]
r, g, b = D[...,0].astype(int), D[...,1].astype(int), D[...,2].astype(int)
hair = (b > r + 25) & (b > 60) & A & (yy < 960)
m_head = A & ((yy < 885) | hair) & ~((yy > 870) & (xx > 900) & (xx < 1195) & ~hair)
m_al, m_ru, m_rl = poly_mask(ARM_L) & A, poly_mask(ARM_R_UP) & A, poly_mask(ARM_R_LO) & A
m_body = A & ~m_head & ~m_al & ~m_ru & ~m_rl

def layer(mask):
    out = np.zeros_like(D); out[mask] = D[mask]; return out

def fill_body():
    """身体：手臂和头挡住的地方用周围颜色修补（只在身体剪影内部）。"""
    body = layer(m_body)
    hole = (m_al | m_ru | m_rl | (m_head & (yy > 840))) & A
    # 修补范围限制在躯干外轮廓（原剪影去掉手臂后的凸包附近）
    rgb = cv2.cvtColor(body[..., :3], cv2.COLOR_RGB2BGR)
    filled = cv2.inpaint(rgb, hole.astype(np.uint8) * 255, 9, cv2.INPAINT_TELEA)
    filled = cv2.cvtColor(filled, cv2.COLOR_BGR2RGB)
    torso_zone = poly_mask([(830,900),(1240,900),(1240,1300),(905,1300),(870,1100)])
    keep = hole & torso_zone
    body[keep, :3] = filled[keep]; body[keep, 3] = 255
    return body

PARTS = {"body": fill_body(), "head": layer(m_head), "arm_l": layer(m_al), "arm_r_up": layer(m_ru), "arm_r_lo": layer(m_rl)}
for k, v in PARTS.items():
    Image.fromarray(v).save(OUT / f"part_{k}.png")
PIV = {"head": (1060, 905), "arm_l": (862, 1005), "arm_r_up": (1200, 995), "arm_r_lo": (1250, 1238), "feet": (1050, 1900)}

def tf(img, piv, ang=0.0, sx=1.0, sy=1.0, dx=0.0, dy=0.0, about=None):
    """绕 piv 旋转 / 缩放后平移。about：缩放中心（默认 piv）。"""
    M = cv2.getRotationMatrix2D(piv, ang, 1.0)
    S = np.array([[sx, 0, (1 - sx) * (about or piv)[0]], [0, sy, (1 - sy) * (about or piv)[1]], [0, 0, 1]])
    M3 = np.vstack([M, [0, 0, 1]]) @ S
    M3[0, 2] += dx; M3[1, 2] += dy
    return cv2.warpAffine(img, M3[:2], (W, H), flags=cv2.INTER_LINEAR, borderValue=(0, 0, 0, 0)), M3

def apply(M3, p):
    v = M3 @ np.array([p[0], p[1], 1.0]); return (float(v[0]), float(v[1]))

def over(dst, src):
    a = src[..., 3:4] / 255.0
    dst[..., :3] = (src[..., :3] * a + dst[..., :3] * (1 - a)).astype(np.uint8)
    dst[..., 3] = np.maximum(dst[..., 3], src[..., 3])

def pose(breath=0.0, head=0.0, al=0.0, ru=0.0, rl=0.0, lean=0.0, hop=0.0):
    """breath: 呼吸（-1..1）；head/al/ru/rl：角度；lean：整体前倾角度；hop：上下位移。"""
    sy = 1 + 0.018 * breath; sx = 1 - 0.008 * breath
    body, Mb = tf(PARTS["body"], PIV["feet"], lean, sx, sy, 0, -hop)
    hp = apply(Mb, PIV["head"]); ap = apply(Mb, PIV["arm_l"]); rp = apply(Mb, PIV["arm_r_up"])
    head, _ = tf(PARTS["head"], PIV["head"], head + lean, dx=hp[0] - PIV["head"][0], dy=hp[1] - PIV["head"][1] - 6 * breath)
    arml, _ = tf(PARTS["arm_l"], PIV["arm_l"], al + lean, dx=ap[0] - PIV["arm_l"][0], dy=ap[1] - PIV["arm_l"][1])
    up, Mu = tf(PARTS["arm_r_up"], PIV["arm_r_up"], ru + lean, dx=rp[0] - PIV["arm_r_up"][0], dy=rp[1] - PIV["arm_r_up"][1])
    ep = apply(Mu, PIV["arm_r_lo"])
    lo, _ = tf(PARTS["arm_r_lo"], PIV["arm_r_lo"], ru + rl + lean, dx=ep[0] - PIV["arm_r_lo"][0], dy=ep[1] - PIV["arm_r_lo"][1])
    canvas = np.zeros((H, W, 4), np.uint8); canvas[...] = (48, 52, 68, 255)
    for layer_ in (arml, body, head, up, lo):
        over(canvas, layer_)
    return Image.fromarray(canvas).crop((560, 180, 1560, 1960)).resize((500, 890), Image.LANCZOS)

def ease(t): return 0.5 - 0.5 * math.cos(math.pi * t)

def save(name, frames, ms):
    frames[0].convert("RGB").save(OUT / f"{name}.gif", save_all=True, append_images=[f.convert("RGB") for f in frames[1:]], duration=ms, loop=0)

# 1) 待机呼吸（2.4s 循环）
fr = []
for i in range(36):
    t = i / 36; s = math.sin(2 * math.pi * t)
    fr.append(pose(breath=s, head=1.5 * math.sin(2 * math.pi * t - 0.6), al=2.0 * math.sin(2 * math.pi * t - 1.0), ru=-1.5 * math.sin(2 * math.pi * t - 1.0)))
save("idle", fr, 66)
# 2) 举召唤器到太阳穴（抬臂 → 停 → 放下）
fr = []
for i in range(40):
    t = i / 39
    k = ease(min(t / 0.3, 1)) if t < 0.65 else 1 - ease((t - 0.65) / 0.35)
    fr.append(pose(breath=0.3 * math.sin(6 * t), head=-6 * k, ru=150 * k, rl=-115 * k, lean=-2 * k))
save("evoker", fr, 50)
# 3) 挥砍（远侧手臂蓄力 → 快速挥下 → 收回）
fr = []
for i in range(30):
    t = i / 29
    if t < 0.35: k = -ease(t / 0.35) * 0.6
    elif t < 0.5: k = -0.6 + 1.6 * ease((t - 0.35) / 0.15)
    else: k = 1.0 - ease((t - 0.5) / 0.5)
    fr.append(pose(al=-100 * max(-k, 0) + 70 * max(k, 0), lean=6 * max(k, 0) - 3 * max(-k, 0), hop=-10 * max(k, 0)))
save("slash", fr, 40)
print("ok", OUT)
