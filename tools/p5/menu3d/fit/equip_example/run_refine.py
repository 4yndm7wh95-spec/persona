import sys, json
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent)); sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from refine import refine
key, i = sys.argv[1], int(sys.argv[2]); wref = float(sys.argv[3]) if len(sys.argv) > 3 else 0.6
d = json.load(open('/tmp/p5fit/eq_joint2.json'))[key]
cam = d['cams'][str(i)]; f0 = d['sched'][str(i)]
import refine as R
obj = R.make_obj(i, cam[6], cam, w_ref=wref)
print('start', obj(list(cam[:6]) + [f0], True), flush=True)
q, det = refine(i, cam, f0, cam[6], cam)
print('end', det, flush=True)
json.dump({'i': i, 'q': [float(v) for v in q], 'fov': cam[6], 'det': [float(x) for x in det]}, open(f'/tmp/p5fit/eq_ref_{i}.json', 'w'))
