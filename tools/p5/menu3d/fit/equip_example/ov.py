"""叠图：rast 剪影轮廓（红）+ 投影的特征点（绿 = 模型、黄 = 原版量的）叠在原版帧上，7 帧拼一张。"""
import sys, json, numpy as np
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from PIL import Image, ImageDraw, ImageFilter
from common import orig
from fitlib import pts3, cam_from, m3d
from rast import silhouette
from landmarks import OBS

def sheet(sched, cams, out, clip='main_0005', idx=range(233, 240)):
    tiles = []
    for i in idx:
        hp, V = pts3(clip, sched[i]); cam = cam_from(cams[i])
        s = silhouette(V, cam, 960, 540)
        m = Image.fromarray((s * 255).astype(np.uint8)); e = np.asarray(m.filter(ImageFilter.FIND_EDGES)) > 0
        o = np.asarray(orig(i, (960, 540))).astype(np.uint8).copy(); o[e] = (255, 0, 0)
        im = Image.fromarray(o); d = ImageDraw.Draw(im)
        for nm, p in OBS.get(i, {}).items():
            q, _ = m3d.project(np.array([hp[nm]]), cam); q = q[0] / 2; p = np.array(p) / 2
            d.ellipse([p[0] - 4, p[1] - 4, p[0] + 4, p[1] + 4], outline=(255, 255, 0), width=2)
            d.ellipse([q[0] - 3, q[1] - 3, q[0] + 3, q[1] + 3], fill=(0, 255, 0)); d.line([tuple(p), tuple(q)], fill=(0, 255, 0))
        d.text((6, 6), f'{i}  f{sched[i]:.2f}', fill=(255, 255, 0)); tiles.append(im)
    W = Image.new('RGB', (960 * 2, 540 * ((len(tiles) + 1) // 2)))
    for k, t in enumerate(tiles): W.paste(t, ((k % 2) * 960, (k // 2) * 540))
    W.save(out, quality=85)

if __name__ == '__main__':
    d = json.load(open('/tmp/p5fit/eq_joint.json'))[sys.argv[1]]
    sheet({int(k): v for k, v in d['sched'].items()}, {int(k): v for k, v in d['cams'].items()}, sys.argv[2])
