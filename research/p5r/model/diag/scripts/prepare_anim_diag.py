from pathlib import Path
import sys,json,struct
sys.path.insert(0,r'C:\Users\kkk\sts2_tools\p5r_tools\scripts')
from attach_animation_by_name import merge,load
repo=Path(r'C:\Users\kkk\Documents\Codex\2026-10-12\claude-git-c-users-kkk-videos\work\persona-ref-upload');local=Path(r'C:\Users\kkk\sts2_tools\p5r_rip\model\diag_work')
model=repo/'research/p5r/model/joker/fixed/joker_thief_nomask.glb';anim=Path(r'C:\Users\kkk\sts2_tools\p5r_rip\model\joker\joker_thief_animations\BB0001_051_main_0000.glb')
merge(model,anim,local/'anim_original.glb');j,bin=load(local/'anim_original.glb');removed=[]
for a in j['animations']:
 keep=[]
 for c in a['channels']:
  n=j['nodes'][c['target']['node']]['name']
  if n=='root' and c['target']['path']=='rotation':removed.append(c)
  else:keep.append(c)
 a['channels']=keep
assert len(removed)==1,(len(removed),removed)
js=json.dumps(j,separators=(',',':')).encode();js+=b' '*((-len(js))%4);(local/'anim_skip_root_rotation.glb').write_bytes(struct.pack('<III',0x46546c67,2,28+len(js)+len(bin))+struct.pack('<II',len(js),0x4e4f534a)+js+struct.pack('<II',len(bin),0x004e4942)+bin)
print('Omitted exactly one channel targeting node root.rotation; all other channel values unchanged',removed)
(local/'animation_attach_info.json').write_text(json.dumps({'source_model':str(model),'source_animation':str(anim),'removed_channels':removed,'method':'omit rotation channel of exact node name root, leave static model root transform unchanged'},indent=2))
