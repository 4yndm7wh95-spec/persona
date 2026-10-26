"""P5 Joker pose plan: stick-figure layout guides on the 1024x1536 pose canvas.

Red = right limbs (near the camera, image-left), blue = left limbs (far side).
Character faces image-right (enemy side). Output: art/p5/ref/plan/<pose>.png + pose_plan.png.
"""
import os, sys
from PIL import Image, ImageDraw, ImageFont

W, H, GROUND = 1024, 1536, 1470
R, L, BODY = (210, 30, 30), (40, 80, 220), (20, 20, 20)

# joints: head, neck, pelvis, rs/ls shoulders, re/le elbows, rh/lh hands, rk/lk knees, rf/lf feet
P = {
 'idle': dict(head=(560,200), neck=(540,300), pelvis=(520,760), rs=(440,350), ls=(620,340), re=(400,580), le=(660,570), rh=(390,760), lh=(690,750), rk=(440,1100), lk=(640,1100), rf=(400,1460), lf=(700,1450)),
 'guard_mid': dict(head=(550,220), neck=(525,320), pelvis=(505,790), rs=(430,380), ls=(610,370), re=(470,600), le=(640,590), rh=(560,520), lh=(660,750), rk=(410,1110), lk=(640,1110), rf=(360,1460), lf=(700,1460)),
 'guard': dict(head=(530,270), neck=(500,370), pelvis=(480,820), rs=(410,420), ls=(590,410), re=(560,580), le=(610,610), rh=(650,350), lh=(570,760), rk=(360,1130), lk=(640,1120), rf=(300,1460), lf=(720,1460)),
 'knife_ready': dict(head=(602, 285), neck=(572, 375), pelvis=(520, 775), rs=(475, 428), ls=(645, 413), re=(505, 623), le=(678, 595), rh=(645, 753), lh=(722, 742), rk=(380, 1110), lk=(700, 1100), rf=(290, 1455), lf=(770, 1415), knife=((645, 753), (583, 838))),
 'slash_smear': dict(head=(634, 280), neck=(604, 370), pelvis=(552, 770), rs=(507, 420), ls=(662, 410), re=(640, 525), le=(665, 592), rh=(800, 470), lh=(652, 745), rk=(390, 1110), lk=(730, 1090), rf=(290, 1455), lf=(770, 1415), knife=((800, 470), (806, 572))),
 'slash': dict(head=(675, 280), neck=(640, 365), pelvis=(578, 765), rs=(573, 413), ls=(688, 407), re=(728, 400), le=(642, 592), rh=(863, 270), lh=(602, 735), rk=(400, 1115), lk=(752, 1080), rf=(290, 1455), lf=(770, 1415), knife=((863, 270), (892, 372))),
 'slash_follow': dict(head=(658, 281), neck=(623, 366), pelvis=(566, 768), rs=(551, 411), ls=(676, 408), re=(681, 491), le=(652, 596), rh=(796, 461), lh=(626, 748), rk=(395, 1112), lk=(742, 1085), rf=(290, 1455), lf=(770, 1415), knife=((796, 461), (798, 560))),
 'gun_raise': dict(head=(590,220), neck=(560,320), pelvis=(530,780), rs=(470,370), ls=(630,360), re=(470,600), le=(680,590), rh=(640,560), lh=(660,760), rk=(450,1110), lk=(650,1110), rf=(400,1460), lf=(710,1460), gun=((640,560),(730,510))),
 'gun': dict(head=(600,230), neck=(565,330), pelvis=(540,780), rs=(490,380), ls=(630,370), re=(670,390), le=(690,590), rh=(840,395), lh=(650,760), rk=(450,1110), lk=(660,1110), rf=(410,1460), lf=(720,1460), gun=((840,395),(940,385))),
 'gun_recoil': dict(head=(595,232), neck=(558,332), pelvis=(536,782), rs=(480,384), ls=(624,372), re=(660,385), le=(686,592), rh=(825,370), lh=(648,760), rk=(450,1110), lk=(660,1110), rf=(410,1460), lf=(720,1460), gun=((825,370),(915,335))),
 'gun_lower': dict(head=(590,220), neck=(560,320), pelvis=(530,775), rs=(470,370), ls=(630,360), re=(520,590), le=(680,590), rh=(640,700), lh=(660,760), rk=(450,1110), lk=(650,1110), rf=(400,1460), lf=(710,1460), gun=((640,700),(670,800))),
 'summon_mid': dict(head=(570,230), neck=(545,330), pelvis=(515,790), rs=(450,380), ls=(625,370), re=(420,590), le=(680,600), rh=(530,470), lh=(700,760), rk=(430,1120), lk=(645,1115), rf=(380,1460), lf=(710,1460)),
 'summon_grab': dict(head=(600,310), neck=(560,400), pelvis=(500,830), rs=(460,450), ls=(640,440), re=(400,330), le=(700,610), rh=(560,300), lh=(740,740), rk=(420,1150), lk=(650,1130), rf=(370,1460), lf=(720,1460), mask=(600,300)),
 'summon_rip': dict(head=(610,250), neck=(565,340), pelvis=(520,770), rs=(480,390), ls=(650,380), re=(360,250), le=(720,560), rh=(260,140), lh=(820,680), rk=(420,1120), lk=(660,1110), rf=(360,1460), lf=(740,1460), mask=(215,115)),
 'summon_end': dict(head=(575,215), neck=(548,315), pelvis=(518,770), rs=(450,370), ls=(625,360), re=(400,530), le=(670,580), rh=(420,700), lh=(700,750), rk=(435,1110), lk=(645,1105), rf=(390,1460), lf=(705,1455)),
 'glove_mid': dict(head=(575,210), neck=(545,310), pelvis=(520,760), rs=(445,360), ls=(625,350), re=(430,590), le=(660,580), rh=(510,620), lh=(640,680), rk=(440,1100), lk=(640,1100), rf=(400,1460), lf=(700,1450)),
 'glove': dict(head=(585,225), neck=(550,315), pelvis=(520,760), rs=(450,365), ls=(625,355), re=(460,580), le=(670,600), rh=(610,470), lh=(575,505), rk=(440,1100), lk=(640,1100), rf=(400,1460), lf=(700,1450)),
 'hurt_impact': dict(head=(510,250), neck=(500,345), pelvis=(520,790), rs=(430,400), ls=(600,385), re=(430,590), le=(640,590), rh=(520,560), lh=(610,560), rk=(440,1120), lk=(640,1115), rf=(400,1460), lf=(700,1455)),
 'hurt_recover': dict(head=(545,215), neck=(525,315), pelvis=(515,770), rs=(435,365), ls=(612,355), re=(380,580), le=(660,580), rh=(400,740), lh=(690,730), rk=(440,1105), lk=(640,1102), rf=(400,1460), lf=(700,1452)),
 'die_kneel': dict(head=(660,760), neck=(610,840), pelvis=(500,1140), rs=(560,880), ls=(680,870), re=(560,1100), le=(700,1040), rh=(580,1460), lh=(660,1170), rk=(440,1440), lk=(650,1180), rf=(290,1470), lf=(700,1460)),
 'die_fall': dict(head=(300,1150), neck=(370,1190), pelvis=(570,1340), rs=(390,1230), ls=(420,1170), re=(300,1350), le=(530,1150), rh=(240,1460), lh=(600,1230), rk=(720,1250), lk=(760,1300), rf=(820,1465), lf=(900,1460)),
 'down': dict(head=(150,1395), neck=(230,1395), pelvis=(560,1420), rs=(260,1415), ls=(270,1380), re=(340,1460), le=(380,1360), rh=(440,1465), lh=(470,1390), rk=(730,1290), lk=(760,1330), rf=(880,1465), lf=(930,1460)),
}

ORDER = ['idle', 'guard_mid', 'guard', 'knife_ready', 'slash_smear', 'slash', 'slash_follow',
         'gun_raise', 'gun', 'gun_recoil', 'gun_lower', 'summon_mid', 'summon_grab', 'summon_rip', 'summon_end',
         'glove_mid', 'glove', 'hurt_impact', 'hurt_recover', 'die_kneel', 'die_fall', 'down']


def font(n):
    for f in ['/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf', 'C:/Windows/Fonts/arialbd.ttf']:
        if os.path.exists(f):
            return ImageFont.truetype(f, n)
    return ImageFont.load_default()


def draw(name, j):
    im = Image.new('RGB', (W, H), (245, 245, 240))
    d = ImageDraw.Draw(im)
    d.line([(0, GROUND), (W, GROUND)], fill=(150, 150, 150), width=3)
    d.text((20, 20), name, fill=(0, 0, 0), font=font(48))
    d.text((20, 80), 'enemy  -->', fill=(120, 0, 0), font=font(32))
    # far (left) limbs first so near (right) limbs draw on top
    for a, b in [('ls', 'le'), ('le', 'lh'), ('pelvis', 'lk'), ('lk', 'lf')]:
        d.line([j[a], j[b]], fill=L, width=22)
    d.line([j['neck'], j['pelvis']], fill=BODY, width=60)
    d.line([j['rs'], j['ls']], fill=BODY, width=30)
    for a, b in [('rs', 're'), ('re', 'rh'), ('pelvis', 'rk'), ('rk', 'rf')]:
        d.line([j[a], j[b]], fill=R, width=30)
    for k, c in [('rh', R), ('lh', L)]:
        x, y = j[k]; d.ellipse([x - 22, y - 22, x + 22, y + 22], fill=c)
    hx, hy = j['head']
    d.ellipse([hx - 80, hy - 90, hx + 80, hy + 90], fill=(255, 225, 200), outline=BODY, width=8)
    # nose tick shows the face direction (3/4 to image-right)
    dx, dy = hx - j['neck'][0], hy - j['neck'][1]
    n = (dx * dx + dy * dy) ** .5 or 1
    px, py = -dy / n, dx / n  # perpendicular, pointing forward when upright
    if px < 0: px, py = -px, -py
    d.line([(hx + px * 30, hy + py * 30), (hx + px * 95, hy + py * 95)], fill=BODY, width=10)
    if 'knife' in j:
        d.line(j['knife'], fill=(140, 140, 150), width=14)
    if 'gun' in j:
        d.line(j['gun'], fill=(60, 60, 60), width=26)
    if 'mask' in j:
        x, y = j['mask']; d.ellipse([x - 75, y - 35, x + 75, y + 35], fill=(255, 255, 255), outline=BODY, width=8)
    return im


def main(out):
    os.makedirs(out, exist_ok=True)
    tiles = []
    for name in ORDER:
        im = draw(name, P[name]); im.save(os.path.join(out, name + '.png'))
        tiles.append(im.resize((256, 384)))
    cols = 8
    sheet = Image.new('RGB', (256 * cols, 384 * ((len(tiles) + cols - 1) // cols)), 'white')
    for i, t in enumerate(tiles):
        sheet.paste(t, ((i % cols) * 256, (i // cols) * 384))
    sheet.save(os.path.join(out, 'pose_plan.png'))


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else 'art/p5/ref/plan')
