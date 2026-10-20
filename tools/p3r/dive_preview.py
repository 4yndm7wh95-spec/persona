import json, sys, os
import numpy as np
from PIL import Image, ImageFilter
from scipy.ndimage import map_coordinates, binary_closing, binary_fill_holes, binary_dilation
S = sys.argv[1]; R = 'research/p3r_camp/'
W, H = 1920, 1080; sc = W / 1920
game = np.asarray(Image.open(S + '/../images/209.webp').convert('RGB').resize((W, H))).astype(float) / 255
# ── 水下层：暂停画面按原版曲线重新上色 + 往下压深蓝 + 上方浅水泛青 + 原版焦散（同牌堆 COMMU 背景）
l = (game ** 2.2) @ np.array([0.2126, 0.7152, 0.0722]); l = np.clip((l - 0.5) * 1.2 + 0.5, 0, 1)
ci = lambda ks: np.interp(l, [k[0] for k in ks], [k[1] for k in ks])
base = np.stack([ci([(0, 0), (0.1, 0.1), (0.9, 0.6), (1, 0.8)]), ci([(0, .3), (.1, .5), (.7, .7), (1, .85)]), ci([(0, .7), (.1, .8), (.7, .85), (1, .9)])], -1)
yy = np.linspace(0, 1, H)[:, None, None]
base = base * (1 - 0.7 * yy) + np.array([0, 0.063, 0.506]) * 0.7 * yy
sh = np.clip(1 - yy / 0.32, 0, 1) ** 1.5
base = base * (1 - 0.75 * sh) + np.array([0.235, 0.843, 0.941]) * 0.75 * sh
ct = np.asarray(Image.open(R + 'tex/Xrd777/UI/Camp/Root/Texture/T_UI_Camp_Caustics_00.png').convert('RGBA')).astype(float) / 255
cl = ct[..., :3].mean(-1) * ct[..., 3]
tw, th = int(900 * sc), int(240 * sc)
tile = np.asarray(Image.fromarray((cl * 255).astype(np.uint8)).resize((tw, th))).astype(float) / 255
def caustics(t):
    out = np.zeros((H, W)); off = int(t * 0.012 * tw * 30) % tw
    for r, y in enumerate(range(0, H, th)):
        row = np.roll(np.tile(tile, (1, W // tw + 3)), -(off + (tw // 2 if r % 2 else 0)), axis=1)[:, :W]
        out[y:y + th] = row[:min(th, H - y)]
    return out * np.clip(1 - yy[..., 0] * 1.4, 0, 1)
# ── 原版 UIM_UI_Camp_In：17×10 网格逐帧顶点偏移（1920×1080 坐标，y 向下）
uim = json.load(open(R + 'anim/UIM_UI_Camp_In__UIM_UI_Camp_In.json'))
OFF = np.array([k['value'] for k in uim['tracks'][0]['keys']])
g = json.load(open(R + 'json/Xrd777/UI/Camp/Root/Uim/UIM_UI_Camp_In.json')); g = g[0] if isinstance(g, list) else g
GV = g['Properties']['UimData']['p2DGeomVertUV']
V = np.array([[v['x'], v['y']] for v in GV]); UV = np.array([[v['u0'], v['v0']] for v in GV])
from scipy.interpolate import griddata
PY, PX = np.mgrid[0:H, 0:W]
SP = np.stack([PX.ravel() / sc - 960, PY.ravel() / sc - 540], -1)
_cache = {}
def warp(img, fi):
    """原版是一张 17×10 网格：顶点画在 base+偏移 的位置，贴图按顶点 UV 取 —— 反过来求每个屏幕像素对应的 UV 再取色。"""
    fi = min(max(fi, 0), 74)
    if fi not in _cache:
        uv = griddata(V + OFF[fi], UV, SP, method='linear')
        nan = np.isnan(uv[:, 0]); uv[nan] = np.stack([(SP[nan, 0] + 960) / 1920, (SP[nan, 1] + 540) / 1080], -1)
        _cache[fi] = uv
    uv = _cache[fi]
    sy = (uv[:, 1] * H).reshape(H, W); sx = (uv[:, 0] * W).reshape(H, W)
    return np.stack([map_coordinates(img[..., c], [sy, sx], order=1, mode='nearest') for c in range(img.shape[2])], -1)
# ── 原版水花 10 帧
spl = [np.asarray(Image.open('art/p3menu_system/p3r/splash_%02d.png' % i).convert('RGBA')).astype(float) / 255 for i in range(10)]
ssx, ssy = 1.06 * W / 961, 1.6 * H / 949   # 横向铺满 16:9 全屏（原版图集按原版画面比例画，等比放大两边会留空）
def place(a):
    h, w = a.shape[:2]; im = Image.fromarray((a * 255).astype(np.uint8)).resize((int(w * ssx), int(h * ssy)))
    out = np.zeros((H, W, 4)); x0 = (W - im.width) // 2; y0 = int(-0.10 * H)
    arr = np.asarray(im).astype(float) / 255
    ys0, xs0 = max(0, -y0), max(0, -x0); ye, xe = min(im.height, H - y0), min(im.width, W - x0)
    out[y0 + ys0:y0 + ye, x0 + xs0:x0 + xe] = arr[ys0:ye, xs0:xe]; return out
SPL = [place(a) for a in spl]
masks = []
acc = np.zeros((H, W), bool)
for i in range(10):
    m = SPL[i][..., 3] > 0.1
    m = binary_fill_holes(binary_closing(m, iterations=6)); acc |= m; masks.append(acc.copy())
# ── 主角（离线渲染，960×540，绿色 = 透水）
def hero(fi):
    p = S + '/topf/color_%03d.png' % min(fi, 70)
    a = np.asarray(Image.open(p).convert('RGBA')).astype(float) / 255
    rgb, al = a[..., :3], a[..., 3]
    rgb = np.clip(rgb / np.maximum(al, 1e-3)[..., None], 0, 1)
    s = np.clip(rgb[..., 1] - np.maximum(rgb[..., 0], rgb[..., 2]), 0, 1)
    c = rgb.copy(); c[..., 1] -= s; c = np.clip(c / np.maximum(1 - s, 1e-3)[..., None], 0, 1)
    return c, (1 - s) * al
T0, HERO0, FULL = 0.40, 0.33, 0.70
frames = []
for n in range(0, 80):
    t = n / 30
    under = base.copy()
    cs = caustics(t); under = 1 - (1 - under) * (1 - cs[..., None] * np.array([0.6, 0.95, 1.0]) * 0.8)
    if t >= HERO0:
        c, a = hero(int((t - HERO0) * 30))
        under = under * (1 - a[..., None]) + c * a[..., None]
    if t >= T0:
        under = warp(under, int((t - T0) * 30))
    si = int((t - T0) / 0.053) if t >= T0 else -1
    if t < T0: m = np.zeros((H, W))
    elif t >= FULL: m = np.ones((H, W))
    else:
        m = masks[min(si, 9)].astype(float)
        m = np.asarray(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(2))).astype(float) / 255
    img = game * (1 - m[..., None]) + under * m[..., None]
    if 0 <= si <= 9:
        sp = SPL[si]; fade = 1.0 if si < 7 else 1 - (si - 6) / 4
        a = sp[..., 3:4] * fade
        img = img * (1 - a) + sp[..., :3] * a
    frames.append(Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8)))
os.makedirs(S + '/dive/frames', exist_ok=True)
for i, f in enumerate(frames): f.save(S + '/dive/frames/f%03d.png' % i)
print('ok', len(frames))
