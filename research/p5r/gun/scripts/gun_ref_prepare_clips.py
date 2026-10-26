from pathlib import Path
import sys,json,struct,csv
sys.path.insert(0,r'C:/Users/kkk/sts2_tools/p5r_tools/scripts')
from attach_animation_by_name import merge,load
r=Path(r'C:/Users/kkk/Documents/Codex/2026-10-12/claude-git-c-users-kkk-videos/work/persona-ref-upload');lib=Path(r'C:/Users/kkk/sts2_tools/p5r_rip');work=lib/'model/gun_ref'
rows=list(csv.DictReader((r/'research/p5r/pose/index.csv').open(encoding='utf-8-sig')));result=[]
for row in rows:
 n=int(row['number'])
 if n not in [51,52,53,54]:continue
 dest=work/f'clip_{n}_D.glb';merge(lib/'model/joker/fixed2/joker_thief_nomask.glb',row['local_glb_path'],dest)
 j,b=load(dest);j['animations'][0]['channels']=[c for c in j['animations'][0]['channels'] if c['target']['path']=='rotation' or(c['target']['path']=='translation' and j['nodes'][c['target']['node']]['name']=='Bip01')]
 s=json.dumps(j,separators=(',',':')).encode();s+=b' '*((-len(s))%4);dest.write_bytes(struct.pack('<III',0x46546c67,2,28+len(s)+len(b))+struct.pack('<II',len(s),0x4e4f534a)+s+struct.pack('<II',len(b),0x004e4942)+b)
 result.append({**row,'number':n,'attached_glb':str(dest),'variant':'D','frames':list(range(0,int(row['frames_at_30fps_reference']),2))})
(work/'gun_clips.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print('Prepared',len(result),'D clips')
