from pathlib import Path
import sys, csv, json, struct, array, hashlib
import numpy as np
sys.path.insert(0, r'C:\Users\kkk\sts2_tools\p5r_tools\GFS-v0.3.1')
from BlenderToolsForGFS import GFSInterface, GFSBinary

REPO=Path(r'C:\Users\kkk\Documents\Codex\2026-10-12\claude-git-c-users-kkk-videos\work\persona-ref-upload')
OUT=REPO/'research/p5r/model/diag';OUT.mkdir(exist_ok=True)
def raw(v):
    if v is None or isinstance(v,(str,int,float,bool)):return v
    if isinstance(v,bytes):return {'utf8':v.decode('utf-8',errors='replace'),'hex':v.hex()}
    if isinstance(v,(list,tuple,array.array,np.ndarray)):return [raw(x) for x in v]
    if isinstance(v,dict):return {str(k):raw(x) for k,x in v.items()}
    fields=dict(vars(v)) if hasattr(v,'__dict__') else {}
    for c in type(v).__mro__:
        for s in getattr(c,'__slots__',[]):
            if hasattr(v,s):fields[s]=getattr(v,s)
    if hasattr(v,'_value'):fields['_value']=v._value
    return {'__type__':type(v).__name__,**{k:raw(x) for k,x in fields.items()}}
def js(v):return json.dumps(raw(v),ensure_ascii=False,separators=(',',':'))
def csvout(name,rows):
    columns=list(dict.fromkeys(k for r in rows for k in r))
    with (OUT/name).open('w',encoding='utf-8-sig',newline='') as f:
        w=csv.DictWriter(f,columns);w.writeheader();w.writerows(rows)
def glb(p):
    b=p.read_bytes();n=struct.unpack_from('<I',b,12)[0];j=json.loads(b[20:20+n]);o=20+n
    return j,b[o+8:o+8+struct.unpack_from('<I',b,o)[0]]
def accessor(j,b,idx):
    a=j['accessors'][idx];v=j['bufferViews'][a['bufferView']];dim={'VEC3':3,'MAT4':16,'VEC4':4,'VEC2':2,'SCALAR':1}[a['type']]
    dtype={5126:'<f4',5125:'<u4',5123:'<u2',5121:'u1'}[a['componentType']]
    return np.ndarray((a['count'],dim),dtype=dtype,buffer=b,offset=v.get('byteOffset',0)+a.get('byteOffset',0),strides=(v.get('byteStride',np.dtype(dtype).itemsize*dim),np.dtype(dtype).itemsize)).copy()
def materials(j,mat):
    found={}
    def walk(v,path=''):
        if isinstance(v,dict):
            if 'index' in v and ('texture' in path.lower()):
                tex=j['textures'][v['index']];im=j['images'][tex.get('source',tex.get('extensions',{}).get('KHR_texture_basisu',{}).get('source',0))]
                found[path]={'texture_index':v['index'],'image_index':tex.get('source'),'image_name':im.get('name'),'image_uri':im.get('uri'),'mimeType':im.get('mimeType'),'bufferView':im.get('bufferView'),'texCoord':v.get('texCoord',0)}
            for k,x in v.items():walk(x,path+'.'+k if path else k)
        elif isinstance(v,list):
            for i,x in enumerate(v):walk(x,f'{path}[{i}]')
    walk(mat);return found
def main():
    for label,stem,source in [('thief','joker_thief_nomask','C0001_051_00.GMD'),('student','joker_student','C0001_001_00.GMD')]:
        p=REPO/'research/p5r/model/joker/source'/source;g=GFSInterface.from_file(str(p));b=GFSBinary();b.read(str(p))
        bm=next(c.data for c in b.containers if c.type==0x000100fb)
        print(label,'binary material payload fields',list(vars(bm)),flush=True)
        originals=bm.data
        modelraw=next(c.data for c in b.containers if c.type==0x00010003)
        rawnodes={}
        def walknode(n):
            meta={k:v for k,v in vars(n).items() if k not in ('children','attachments')}
            meta['mesh_attachments_raw']=[{k:v for k,v in vars(a.data).items() if k not in ('vertices','indices','_MeshBinary__morph_data')} for a in n.attachments if type(a.data).__name__=='MeshBinary']
            rawnodes[n.name.string.decode('utf-8',errors='replace')]=meta
            for c in n.children.data:walknode(c)
        walknode(modelraw.root_node)
        rows=[]
        for i,m in enumerate(g.materials):
            r={'material_index':i,'material_name':m.name,'source_gmd':source}
            for k,v in vars(m).items():
                r[k]=js(v) if not isinstance(v,(str,int,float,bool)) or v is None else v
            if i<len(originals):
                r['binary_material_raw_json']=js(originals[i]);r['flags_raw_decimal']=originals[i].flags._value;r['flags_raw_hex']=hex(originals[i].flags._value)
            rows.append(r)
        csvout(f'gmd_materials_{label}.csv',rows)
        rows=[]
        for i,mesh in enumerate(g.meshes):
            node=g.bones[mesh.node]
            row={'mesh_index':i,'node_index':mesh.node,'node_name':node.name,'material_name':mesh.material_name.decode('utf-8',errors='replace'),'vertex_count':len(mesh.vertices),'index_count':len(mesh.indices),'node_fields_json':js(node),'binary_node_fields_json':js(rawnodes.get(node.name)),'model_flags_raw':modelraw.flags._value,'mesh_fields_json':js({k:v for k,v in vars(mesh).items() if k not in ('vertices','indices','morphs')}),'morph_count':len(mesh.morphs)}
            rows.append(row)
        csvout(f'gmd_nodes_{label}.csv',rows)
        j,bin=glb(REPO/'research/p5r/model/joker/fixed'/f'{stem}.glb');rows=[]
        for ni,node in enumerate(j['nodes']):
            if 'mesh' not in node:continue
            mesh=j['meshes'][node['mesh']];count=0;bounds=[];mats=[];slots=[];modes=[]
            for pr in mesh['primitives']:
                ac=j['accessors'][pr['attributes']['POSITION']];count+=ac['count'];arr=accessor(j,bin,pr['attributes']['POSITION']);bounds.append([arr.min(0).tolist(),arr.max(0).tolist()])
                mat=j['materials'][pr['material']] if 'material' in pr else {};mats.append(mat.get('name','(default)'));slots.append(materials(j,mat));modes.append(mat.get('alphaMode','OPAQUE'))
            rows.append({'node_index':ni,'node_name':node.get('name'),'mesh_index':node['mesh'],'mesh_name':mesh.get('name'),'skinned':'skin' in node,'skin_index':node.get('skin'),'material_names':js(mats),'texture_slots_json':js(slots),'alphaModes':js(modes),'vertex_count':count,'bbox_mesh_local_by_primitive_json':js(bounds),'node_transform_json':js({k:v for k,v in node.items() if k in ('matrix','translation','rotation','scale')})})
        csvout(f'mesh_table_{label}.csv',rows)
        print(label,'materials',len(g.materials),'GMD meshes',len(g.meshes),'GLB mesh nodes',len(rows),flush=True)
    print('done',OUT)

if __name__=='__main__':main()
