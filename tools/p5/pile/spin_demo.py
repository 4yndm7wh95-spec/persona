"""预告信 3D 旋转演示：真透视 + 厚度边 + 反光 + 投影；转速 / 碰牌踢转照抄 P3 HeroStage（36°/s，0.6s 起转，踢 120°，弹簧 ω=9）。"""
import sys, os, subprocess, math, numpy as np
from PIL import Image, ImageDraw, ImageFilter
BASE = Image.open(sys.argv[1]).convert('RGBA')
OUT = sys.argv[2]
X0, Y0, OW, OH = 700, 280, 854, 480              # 1:1 裁出手掌附近（不缩放，看真实清晰度）
SS = 2; FPS = 30; DUR = 6.5
CX, CY = 1125 - X0, 520 - Y0
front = Image.open('art/p5/ui/pile/calling_card.png').convert('RGBA')
backs = [Image.open(f'art/p5/ui/pile/letters/letter_{k}.png').convert('RGBA') for k in ('madarame', 'kamoshida', 'kaneshiro')]
CW, CH = 282, 202                                 # 屏幕上卡的大小（1080p）
FOC = 1100.0                                      # 透视焦距（越小透视越强）
TILT_Z, TILT_X = math.radians(-8), math.radians(8)
KICKS = [2.4, 4.1]

def coeffs(dst, src):
    A, B = [], []
    for (x, y), (u, v) in zip(dst, src):
        A += [[x, y, 1, 0, 0, 0, -u * x, -u * y], [0, 0, 0, x, y, 1, -v * x, -v * y]]; B += [u, v]
    return np.linalg.solve(np.array(A, float), np.array(B, float)).tolist()

def project(p, bob):
    x, y, z = p
    # 先绕 X 轻微后仰，再绕 Z 歪一点
    y, z = y * math.cos(TILT_X) - z * math.sin(TILT_X), y * math.sin(TILT_X) + z * math.cos(TILT_X)
    x, y = x * math.cos(TILT_Z) - y * math.sin(TILT_Z), x * math.sin(TILT_Z) + y * math.cos(TILT_Z)
    s = FOC / (FOC + z)
    return ((CX + x * s) * SS, (CY + bob + y * s) * SS), z

def frame(theta, t, back):
    cv = BASE.crop((X0, Y0, X0 + OW, Y0 + OH)).resize((OW * SS, OH * SS), Image.LANCZOS)
    c, s = math.cos(theta), math.sin(theta)
    bob = 6 * math.sin(t * 1.7)
    hw, hh, th = CW / 2, CH / 2, 3.0
    def corners(zoff):
        pts = []
        for (x, y) in ((-hw, -hh), (hw, -hh), (hw, hh), (-hw, hh)):
            pts.append(project((x * c + zoff * s, y, -x * s + zoff * c), bob))
        return pts
    F, B = corners(-th), corners(th)
    # 投影（落在手上，跟着卡的宽度变化）
    sh = Image.new('L', cv.size); d = ImageDraw.Draw(sh)
    w = abs(c) * CW * 0.5 + 10
    d.ellipse([(CX - w + 26) * SS, (CY + 112) * SS, (CX + w + 26) * SS, (CY + 132) * SS], fill=110)
    sh = sh.filter(ImageFilter.GaussianBlur(14 * SS))
    cv.paste((40, 0, 0, 255), (0, 0), sh)
    # 厚度边（四条侧面，按深度从远到近画）
    sides = []
    for i in range(4):
        j = (i + 1) % 4
        quad = [F[i][0], F[j][0], B[j][0], B[i][0]]
        depth = (F[i][1] + F[j][1] + B[i][1] + B[j][1]) / 4
        sides.append((depth, quad))
    d = ImageDraw.Draw(cv)
    for depth, q in sorted(sides, key=lambda e: -e[0]):
        d.polygon(q, fill=(70, 6, 10, 255))
    # 朝向镜头的那一面
    facing_front = c > 0
    face = (front if facing_front else back).copy()
    pts = F if facing_front else B
    # 光照：越侧越暗；一道斜向反光随角度扫过
    fw, fh = face.size
    a = np.asarray(face, np.float32)
    lam = 0.62 + 0.38 * abs(c)
    xs = np.linspace(-1, 1, fw)[None, :]; ys = np.linspace(-1, 1, fh)[:, None]
    band = np.exp(-((xs * 0.8 + ys * 0.45 - (s * 1.6)) ** 2) / 0.012) * (0.22 * abs(c) ** 0.5)
    rgb = a[..., :3] * lam + 255 * band[..., None]
    a[..., :3] = np.clip(rgb, 0, 255)
    face = Image.fromarray(a.astype(np.uint8))
    src = [(4, 4), (fw - 4, 4), (fw - 4, fh - 4), (4, fh - 4)]
    if not facing_front: src = [src[1], src[0], src[3], src[2]]
    dst = [p[0] for p in pts]
    warped = face.transform(cv.size, Image.PERSPECTIVE, coeffs(dst, src), Image.BICUBIC)
    m = Image.new('L', cv.size); ImageDraw.Draw(m).polygon(dst, fill=255)
    warped.putalpha(m)
    cv.alpha_composite(warped)
    return cv.resize((OW, OH), Image.LANCZOS).convert('RGB')

def main():
    tmp = OUT + '_frames'; os.makedirs(tmp, exist_ok=True)
    base_angle = extra = vel = target = 0.0
    dt = 1 / FPS; n = int(DUR * FPS); kicks = list(KICKS); turns = 0; back_i = 0; last_side = 1
    for i in range(n):
        t = i * dt
        if kicks and t >= kicks[0]: target += 120; kicks.pop(0)
        ramp = min(t / 0.6, 1.0)
        base_angle += 36 * ramp * ramp * (3 - 2 * ramp) * dt
        acc = 81 * (target - extra) - 18 * vel; vel += acc * dt; extra += vel * dt
        theta = math.radians(base_angle + extra)
        side = 1 if math.cos(theta) > 0 else -1
        if side == 1 and last_side == -1: back_i = (back_i + 1) % len(backs)   # 每转回正面换下一封信
        last_side = side
        frame(theta, t, backs[back_i]).save(f'{tmp}/{i:04d}.png')
    # 声音：每次碰牌一下原版光标音
    snd = 'research/p5r/audio/camp/SOUND_E_SOUND_SYSTEM.ACB_0177.ogg'
    inputs = ['-f', 'lavfi', '-t', str(DUR), '-i', 'anullsrc=r=44100:cl=stereo']
    fl = []
    for k, kt in enumerate(KICKS):
        inputs += ['-i', snd]; fl.append(f'[{k + 2}:a]adelay={int(kt * 1000)}|{int(kt * 1000)}[s{k}]')
    mix = ''.join(f'[s{k}]' for k in range(len(KICKS)))
    fc = ';'.join(fl) + f';[1:a]{mix}amix=inputs={len(KICKS) + 1}:normalize=0[a]'
    subprocess.run(['ffmpeg', '-loglevel', 'error', '-y', '-framerate', str(FPS), '-i', f'{tmp}/%04d.png'] + inputs +
                   ['-filter_complex', fc, '-map', '0:v', '-map', '[a]', '-c:v', 'libx264', '-pix_fmt', 'yuv420p', '-crf', '18', '-c:a', 'aac', '-shortest', OUT], check=True)
main()
