"""按 PileDeck 的叠法在原版坐标里拼出入场每一帧（黑底 → 星纹 / 浅灰板 / 红底 → 黑区白纸 → 3D 帧 → 黑区反色），和原版并排。"""
import sys, re, json, numpy as np
from pathlib import Path
from PIL import Image, ImageDraw
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from common import ROOT, orig
CS = (ROOT / 'p5mod/src/Vfx/PileDeckEntrance.cs').read_text()
META = json.load(open(ROOT / 'research/p5r/menu3d_equip/frames.json'))

def arr(name):
    blk = CS[CS.index(f' {name} ='):]; blk = blk[:blk.index('};') + 2]
    frames = re.findall(r'new Vector2\[\]\[\] \{(.*?)\} \},?\n|new Vector2\[\]\[\] \{  \},', blk)
    out = []
    for line in blk.split('\n'):
        if 'Vector2[][]' not in line: continue
        polys = re.findall(r'new Vector2\[\] \{([^}]*)\}', line)
        out.append([[tuple(map(float, m)) for m in re.findall(r'new\(([-\d.]+)f, ([-\d.]+)f\)', p)] for p in polys])
    return out

STAR, GRAY, RED, DARK = arr('Star'), arr('Gray'), arr('Red'), arr('Dark')
INK, PAPER = 8, 235

def compose(k, W=1920, H=1080):
    im = Image.new('RGB', (W, H), (INK,) * 3)
    o = np.asarray(orig(233 + k)).copy()
    def mask(polys):
        m = Image.new('L', (W, H)); d = ImageDraw.Draw(m)
        for p in polys:
            if len(p) > 2: d.polygon(p, fill=255)
        return np.asarray(m) > 0
    a = np.asarray(im).copy()
    st = mask(STAR[k]); a[st] = o[st]                         # 星纹：用原版的像素当星纹材质（只看区域形状）
    a[mask(GRAY[k])] = (214, 214, 214)
    rd = mask(RED[k]); a[rd] = (140, 12, 22)
    dk = mask(DARK[k]); a[dk] = PAPER
    im = Image.fromarray(a)
    f = META['frames'][str(k)]; fr = Image.open(ROOT / f'research/p5r/menu3d_equip/f{k:02d}.png').convert('RGBA')
    im.paste(fr, (f['x'], f['y']), fr)
    a = np.asarray(im).astype(int).copy()
    a[dk] = np.clip(INK + PAPER - a[dk], 0, 255)
    return Image.fromarray(a.astype(np.uint8))

if __name__ == '__main__':
    rows = []
    for k in range(7):
        ours = compose(k).resize((960, 540)); org = orig(233 + k, (960, 540))
        r = Image.new('RGB', (1920, 540)); r.paste(org, (0, 0)); r.paste(ours, (960, 0))
        ImageDraw.Draw(r).text((8, 8), f'{233 + k} orig', fill=(255, 255, 0)); ImageDraw.Draw(r).text((968, 8), 'ours', fill=(255, 255, 0))
        r.save(f'/tmp/p5fit/eq_cmp_{233 + k}.jpg', quality=88); rows.append(r)
