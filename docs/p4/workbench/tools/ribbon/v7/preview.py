import sys, numpy as np
from PIL import Image
from render import load, render, curvature_report
from extras import extras
R = load(sys.argv[1]); pref = sys.argv[2]
im = render(R, extra=extras(R)); im.save(pref + '_flat.png')
ref = Image.open('../../../../../../research/p4r/allout/S02_0743_allout_start_yellow_orange_swipe_party_rush.jpg').convert('RGB')
Image.blend(ref, im, 0.55).save(pref + '_blend.png')
im.crop((150, 300, 490, 420)).resize((340 * 3, 120 * 3), Image.LANCZOS).save(pref + '_arrow.png')
im.crop((180, 380, 620, 620)).resize((440 * 2, 240 * 2), Image.LANCZOS).save(pref + '_curl.png')
for nm, rep in curvature_report(R): print(nm, ':', rep)
