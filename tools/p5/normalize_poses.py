"""Normalize P5 Joker pose images to idle_a3: same head size and same foot baseline.

Generated poses come out at slightly different scales (head width / boot size differ by 5-8%),
which makes the figure pulse when the game switches images. Each pose is scaled (hand-set factor, see POSES)
and shifted so the anchor foot lands where idle_a3's does.
Output goes to art/p5/poses/final/<name>.png; the originals are not touched.
Usage: python tools/p5/normalize_poses.py
"""
import json
import numpy as np
from pathlib import Path
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[2]
P = ROOT / 'art/p5/poses'
OUT = P / 'final'
REF = 'idle_a3'
PAD = 256  # final canvas is 256 px wider on each side, so flared coats / raised arms never get cut off
TOP = 96  # and 96 px taller at the top
# name -> (scale, foot that stays planted relative to idle: 'rear' = image-left, 'front' = image-right,
# 'mid' = midpoint between the feet, for poses that step both feet).
# Scales are set by hand from head size and body length (chin to sole, for standing frames) against idle_a3;
# automatic head measurement is unreliable because raised black sleeves touch the hair.
POSES = {'idle_a3': (1.0, 'rear'), 'guard_a2': (1.0, 'front'), 'knife_ready_a': (0.96, 'rear'),
         'slash_a3': (0.96, 'mid'), 'slash_follow_a': (0.96, 'mid'),
         'slash_mid_a': (0.96, 'mid'), 'slash_smear_a3': (0.96, 'mid'), 'guard_mid_a': (1.0, 'front'),
         'gun_draw_a2': (1.0, 'rear'), 'gun_a2': (1.03, 'rear'), 'gun_raise_a2': (1.03, 'rear'), 'gun_recoil_a6': (1.045, 'rear'),
         'gun_step_a': (1.0, 'rear'), 'knife_draw_a': (1.0, 'rear'), 'slash_recover_a': (1.0, 'mid'),
         'knife_set_a': (1.0, 'rear'), 'knife_return_a': (1.0, 'mid'), 'gun_lift_mid_a': (1.0, 'rear'),
         'gun_lift_near_a': (1.0, 'rear'), 'summon_hold_a': (1.0, 'front'), 'summon_rip_a2': (1.0, 'front'), 'summon_release_a': (1.035, 'front', 45),
         'summon_lift1_a': (1.0, 'front'), 'summon_lift2_a': (1.0, 'front'), 'summon_fling_a': (1.015, 'front', 45), 'summon_settle_a': (1.035, 'front', 22),
         'summon_lift_mid_a': (1.0, 'front'), 'summon_touch1_a': (1.0, 'front'), 'summon_touch2_a': (1.0, 'front'),
         'summon_fling1_a': (1.0, 'front'), 'summon_fling2_a': (1.015, 'front', 45),
         'summon_lift_bridge_a': (1.0, 'front'), 'summon_touch_mid_a': (1.0, 'front'), 'summon_contact_a': (1.0, 'front'),
         'summon_open1_b': (1.0, 'front'), 'summon_open2_b': (1.0, 'front'),
         'summon_release_b': (1.03, 'front'), 'summon_settle_b': (1.033, 'front'),
         'summon_down1_b': (1.03, 'front'), 'summon_down2_b': (1.03, 'front'), 'summon_down3_b': (1.0, 'front'),
         'summon_down2_c': (1.015, 'front'), 'summon_down3_c': (1.01, 'front'),
         'summon_down1_c': (1.028, 'front'), 'summon_down25_c': (1.016, 'front'),
         'summon_down05_c': (1.0, 'front')}


def load(n):
    return Image.open(P / f'{n}.png').convert('RGBA')


def boots(a):
    r, g, b, al = (a[..., i] for i in range(4))
    m = (al > 200) & (r > 50) & (r - b > 25) & (g < 110) & (r < 200) & (r > g + 15)
    m[:1100] = False
    m = ndimage.binary_closing(m, iterations=3)
    lab, k = ndimage.label(m)
    sz = ndimage.sum(m, lab, range(1, k + 1))
    idx = np.argsort(sz)[::-1][:2] + 1
    out = []
    for i in idx:
        ys, xs = np.nonzero(lab == i)
        out.append((xs.mean(), ys.max()))
    return sorted(out)  # [rear(image-left), front(image-right)]


def main():
    OUT.mkdir(exist_ok=True)
    ref_feet = boots(np.array(load(REF)).astype(int))
    log = {}
    for n, spec in POSES.items():
        s, anchor = spec[0], spec[1]
        nudge = spec[2] if len(spec) > 2 else 0  # extra x shift (px) to smooth a sequence that slides sideways
        im = load(n)
        feet = boots(np.array(im).astype(int))
        if anchor == 'mid':
            fx, fy = (feet[0][0] + feet[1][0]) / 2, feet[0][1]
            tx, ty = (ref_feet[0][0] + ref_feet[1][0]) / 2, ref_feet[0][1]
        else:
            fi = 0 if anchor == 'rear' else 1
            fx, fy = feet[fi]
            tx, ty = ref_feet[fi]
        W, H = im.size
        big = im.resize((round(W * s), round(H * s)), Image.LANCZOS)
        ox, oy = round(tx - fx * s) + nudge, round(ty - fy * s)
        bottom = np.nonzero(np.array(big)[..., 3].max(1) > 20)[0].max()
        oy = int(min(oy, H - 4 - bottom))  # never push the lower foot off the canvas
        ox += PAD
        oy += TOP
        res = Image.new('RGBA', (W + 2 * PAD, H + TOP), (0, 0, 0, 0))
        res.alpha_composite(big, (max(0, ox), max(0, oy)), (max(0, -ox), max(0, -oy)))
        res.save(OUT / f'{n}.png')
        log[n] = {'scale': round(s, 4), 'offset': [ox, oy], 'anchor_foot': anchor}
    (OUT / 'normalize.json').write_text(json.dumps(log, indent=1))
    print(json.dumps(log))


if __name__ == '__main__':
    main()
