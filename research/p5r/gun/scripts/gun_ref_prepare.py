from pathlib import Path
import json,struct,sys,subprocess,numpy as np
sys.path.insert(0,r'C:/Users/kkk/sts2_tools/p5r_tools/scripts')
from attach_animation_by_name import load
lib=Path(r'C:/Users/kkk/sts2_tools/p5r_rip');w=lib/'model/gun_ref';repo=Path(r'C:/Users/kkk/Documents/Codex/2026-10-12/claude-git-c-users-kkk-videos/work/persona-ref-upload')
def save(j,b,p):
 s=json.dumps(j,separators=(',',':')).encode();s+=b' '*((-len(s))%4);b+=b'\0'*((-len(b))%4)
 p.write_bytes(struct.pack('<III',0x46546c67,2,28+len(s)+len(b))+struct.pack('<II',len(s),0x4e4f534a)+s+struct.pack('<II',len(b),0x004e4942)+b)
j,b=load(w/'gun0001.glb');j.setdefault('skins',[]);save(j,b,w/'gun0001_compat.glb')
subprocess.run([sys.executable,str(repo/'tools/p5r/fix_gfd_glb.py'),str(w/'gun0001_compat.glb'),str(w/'gun0001_fixed.glb'),'--flip-v'],check=True)
for name in ['gun0001_fixed.glb','gun0000_fixed.glb']:
 j,b=load(w/name);print(name,[(i,n.get('name'),n.get('matrix')) for i,n in enumerate(j['nodes'])])
j,b=load(lib/'model/joker/fixed2/joker_thief_nomask.glb')
print('hands',[(i,n) for i,n in enumerate(j['nodes']) if n.get('name') in ['Bip01___R___Hand','h_Motion_hand_R']])
for i in range(51,55):
 j,b=load(lib/f'model/pose_stage1/clip_{i:02}.glb')
 print('clip',i,'rotationchannels',len(j['animations'][0]['channels']))
