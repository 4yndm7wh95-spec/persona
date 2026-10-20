"""原版营地 UI 图集按 SPR_UI_Camp（简中）的真实 UV 切图。from camp_spr import spr; spr(16) → PIL RGBA。"""
import json, glob, os
from PIL import Image
R = os.path.join(os.path.dirname(__file__), '../../research/p3r_camp/')
_d = json.load(open(R + 'pages/widget/L10N/zh-Hans/Xrd777/UI/Camp/Root/Texture/SPR_UI_Camp.json'))
_d = _d[0] if isinstance(_d, list) else _d
SPR = _d['Properties']['SprDatas'][0]['Value']['SprDatas']
_png = glob.glob(R + '**/*.png', recursive=True)
_cache = {}


def tex(name):
    if name not in _cache:
        c = [p for p in _png if os.path.basename(p) == name + '.png' and '/Astrea/' not in p]   # Astrea（女主角篇）同名图集布局不同
        c = [p for p in c if 'zh-Hans' in p] or [p for p in c if 'L10N' not in p] or c
        assert c, name
        _cache[name] = Image.open(c[0]).convert('RGBA')
    return _cache[name]


def spr(i):
    s = SPR[i]
    im = tex(s['Texture']['ObjectName'].split("'")[1])
    W, H = im.size
    box = (round(s['U0'] * W), round(s['V0'] * H), round(s['U1'] * W), round(s['V1'] * H))
    out = im.crop(box)
    assert abs(out.width - s['Width']) <= 1 and abs(out.height - s['Height']) <= 1, (i, out.size, s['Width'], s['Height'])
    return out
