from pathlib import Path
import sys,json,struct,numpy as np
sys.path.insert(0,r'C:\Users\kkk\sts2_tools\p5r_tools\scripts')
from attach_animation_by_name import merge,load
REPO=Path(r'C:\Users\kkk\Documents\Codex\2026-10-12\claude-git-c-users-kkk-videos\work\persona-ref-upload');LIB=Path(r'C:\Users\kkk\sts2_tools\p5r_rip');LOCAL=LIB/'model/round3_work'
mp=LIB/'model/joker/fixed2/joker_thief_nomask.glb';ap=LIB/'model/joker/joker_thief_animations/BB0001_051_main_0000.glb'
merge(mp,ap,LOCAL/'anim_A.glb');j,b=load(LOCAL/'anim_A.glb');summary=json.loads((LOCAL/'anim_compare_summary.json').read_text());ratios=summary['translation_ratios']
values=[r['ratio'] for r in ratios];info={'translation_node_for_D':'Bip01','translation_node_path':'RootNode/root/rot/Bip01 (parent of Bip01___Pelvis)','translation_multiplier':1.0,'scale_reason':'No common fixed multiplier across nonzero model/action translations; leave D translation unchanged','ratio_count':len(values),'ratio_min':min(values),'ratio_median':float(np.median(values)),'ratio_max':max(values),'variants':{}}
for variant in ['A','C','D']:
 doc=json.loads(json.dumps(j));kept=[]
 for c in doc['animations'][0]['channels']:
  name=doc['nodes'][c['target']['node']]['name'];path=c['target']['path']
  if variant=='A' or path=='rotation' or (variant=='D' and name=='Bip01' and path=='translation'):kept.append(c)
 doc['animations'][0]['channels']=kept
 if variant=='D':assert sum(c['target']['path']=='translation' for c in kept)==1
 js=json.dumps(doc,separators=(',',':')).encode();js+=b' '*((-len(js))%4)
 (LOCAL/f'anim_{variant}.glb').write_bytes(struct.pack('<III',0x46546c67,2,28+len(js)+len(b))+struct.pack('<II',len(js),0x4e4f534a)+js+struct.pack('<II',len(b),0x004e4942)+b)
 info['variants'][variant]={'channels':len(kept),'rotation':sum(c['target']['path']=='rotation' for c in kept),'translation':sum(c['target']['path']=='translation' for c in kept),'scale':sum(c['target']['path']=='scale' for c in kept)}
print('VARIANTS',info,flush=True)
(LOCAL/'retarget_variants.json').write_text(json.dumps(info,indent=2),encoding='utf-8')
