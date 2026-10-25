from pathlib import Path
import json,sys,hashlib,shutil
sys.path.insert(0,r'C:\Users\kkk\sts2_tools\p5r_tools\scripts')
from attach_animation_by_name import load
REPO=Path(r'C:\Users\kkk\Documents\Codex\2026-10-12\claude-git-c-users-kkk-videos\work\persona-ref-upload');OUT=REPO/'research/p5r/model/diag';LOCAL=Path(r'C:\Users\kkk\sts2_tools\p5r_rip\model\diag_work')
sys.path.insert(0,str(Path(__file__).parent))
from p5r_diag_metadata import accessor
source=Path(r'C:\Users\kkk\sts2_tools\p5r_rip\model\joker\joker_thief_animations\BB0001_051_main_0000.glb');j,b=load(source)
roots=[]
for a in j['animations']:
 for c in a['channels']:
  node=j['nodes'][c['target']['node']]
  if node.get('name')=='root':
   sampler=a['samplers'][c['sampler']];values=accessor(j,b,sampler['output']);times=accessor(j,b,sampler['input'])
   roots.append({'path':c['target']['path'],'source_static_node':node,'time_count':len(times),'times_seconds':times[:,0].tolist(),'values':values.tolist(),'interpolation':sampler.get('interpolation','LINEAR')})
data={'source':str(source),'root_channels':roots,'sample_frames':[0,8,16,24,32,40],'sample_times_seconds':[f/30 for f in [0,8,16,24,32,40]],'fps_basis':'30fps reference only; source FPS unknown','attach_info':json.loads((LOCAL/'animation_attach_info.json').read_text()),'camera':json.loads((LOCAL/'camera_thief.json').read_text()),'render_bounds':json.loads((LOCAL/'animation_render_info.json').read_text())}
(OUT/'animation_evidence.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
print('Root channel values:',[(r['path'],r['values'][0],r['values'][-1]) for r in roots])
