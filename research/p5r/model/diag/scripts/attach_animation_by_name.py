import json,struct,copy,sys
from pathlib import Path
def load(p):
 b=Path(p).read_bytes();n=struct.unpack_from('<I',b,12)[0];j=json.loads(b[20:20+n]);off=20+n
 return j,b[off+8:off+8+struct.unpack_from('<I',b,off)[0]]
def merge(model,anim,out):
 m,mb=load(model);a,ab=load(anim);mb+=b'\0'*((-len(mb))%4)
 names={n.get('name'):i for i,n in enumerate(m['nodes'])};voff=len(m.get('bufferViews',[]));aoff=len(m.get('accessors',[]));boff=len(mb)
 for v in a.get('bufferViews',[]):
  v=copy.deepcopy(v);v['buffer']=0;v['byteOffset']=v.get('byteOffset',0)+boff;m.setdefault('bufferViews',[]).append(v)
 for ac in a.get('accessors',[]):
  ac=copy.deepcopy(ac)
  if 'bufferView' in ac:ac['bufferView']+=voff
  if 'sparse' in ac:raise ValueError('Unsupported sparse animation accessor')
  m.setdefault('accessors',[]).append(ac)
 animations=copy.deepcopy(a['animations'])
 for an in animations:
  for s in an['samplers']:s['input']+=aoff;s['output']+=aoff
  for c in an['channels']:c['target']['node']=names[a['nodes'][c['target']['node']]['name']]
 m['animations']=animations;binary=mb+ab;binary+=b'\0'*((-len(binary))%4);m['buffers']=[{'byteLength':len(binary)}]
 js=json.dumps(m,separators=(',',':')).encode();js+=b' '*((-len(js))%4)
 Path(out).parent.mkdir(parents=True,exist_ok=True);Path(out).write_bytes(struct.pack('<III',0x46546c67,2,28+len(js)+len(binary))+struct.pack('<II',len(js),0x4e4f534a)+js+struct.pack('<II',len(binary),0x004e4942)+binary)
 print('Mapped animation channels by exact node name:',len(animations[0]['channels']))
if __name__=='__main__':merge(*sys.argv[1:])
