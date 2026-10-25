from pathlib import Path
import json,csv,struct,sys
sys.path.insert(0,r'C:\Users\kkk\sts2_tools\p5r_tools\scripts')
from attach_animation_by_name import merge,load
REPO=Path(r'C:\Users\kkk\Documents\Codex\2026-10-12\claude-git-c-users-kkk-videos\work\persona-ref-upload');LIB=Path(r'C:\Users\kkk\sts2_tools\p5r_rip');LOCAL=LIB/'model/pose_stage1';LOCAL.mkdir(exist_ok=True)
rows=[r for r in csv.DictReader((REPO/'research/p5r/joker_battle_anim_list.csv').open(encoding='utf-8-sig')) if r['original_name'].startswith('BB0001_051.GAP#')]
assert len(rows)==64
for i,r in enumerate(rows,1):
    src=LIB/'model/joker/joker_thief_animations'/r['file'];dest=LOCAL/f'clip_{i:02}.glb';merge(LIB/'model/joker/fixed2/joker_thief_nomask.glb',src,dest)
    j,b=load(dest);allchannels=j['animations'][0]['channels'];j['animations'][0]['channels']=[c for c in allchannels if c['target']['path']=='rotation'];assert j['animations'][0]['channels']
    js=json.dumps(j,separators=(',',':')).encode();js+=b' '*((-len(js))%4);dest.write_bytes(struct.pack('<III',0x46546c67,2,28+len(js)+len(b))+struct.pack('<II',len(js),0x4e4f534a)+js+struct.pack('<II',len(b),0x004e4942)+b)
    r.update(number=i,source_glb=str(src),attached_glb=str(dest),rotation_channels=len(j['animations'][0]['channels']),omitted_channels=len(allchannels)-len(j['animations'][0]['channels']))
(LOCAL/'clips.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
print('PREPARED',len(rows),'rotation-only clips',flush=True)
