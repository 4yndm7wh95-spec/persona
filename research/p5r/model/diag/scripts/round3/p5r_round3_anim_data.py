from pathlib import Path
import sys,json,csv,re,numpy as np
sys.path.insert(0,r'C:\Users\kkk\sts2_tools\p5r_tools\scripts')
from attach_animation_by_name import load
from p5r_diag_metadata import accessor
REPO=Path(r'C:\Users\kkk\Documents\Codex\2026-10-12\claude-git-c-users-kkk-videos\work\persona-ref-upload');LIB=Path(r'C:\Users\kkk\sts2_tools\p5r_rip');LOCAL=LIB/'model/round3_work';OUT=REPO/'research/p5r/model/diag'
mp=LIB/'model/joker/fixed2/joker_thief_nomask.glb';ap=LIB/'model/joker/joker_thief_animations/BB0001_051_main_0000.glb';m,mb=load(mp);a,ab=load(ap)
def names(j):return {n['name']:i for i,n in enumerate(j['nodes'])}
def parents(j):return {c:i for i,n in enumerate(j['nodes']) for c in n.get('children',[])}
mn=names(m);an=names(a);pm=parents(m);pa=parents(a);channels={}
for anim in a['animations']:
 for ch in anim['channels']:
    s=anim['samplers'][ch['sampler']];target=a['nodes'][ch['target']['node']]['name'];times=accessor(a,ab,s['input'])[:,0];values=accessor(a,ab,s['output']);first=int(np.argmin(times))
    channels.setdefault(target,{})[ch['target']['path']]={'first':values[first].tolist(),'sampler':ch['sampler'],'times_first':float(times[first]),'key_count':len(times),'interpolation':s.get('interpolation','LINEAR')}
def translation(n):return np.array(n['matrix'],dtype=float).reshape(4,4,order='F')[:3,3].tolist() if 'matrix' in n else n.get('translation',[0,0,0])
def pname(j,p,i):return j['nodes'][p[i]]['name'] if i in p else ''
rows=[];ratios=[]
for name in dict.fromkeys(list(mn)+list(an)):
 mi=mn.get(name);ai=an.get(name);node=m['nodes'][mi] if mi is not None else {};ac=channels.get(name,{})
 r={'node_name':name,'model_parent':pname(m,pm,mi),'animation_parent':pname(a,pa,ai),'model_translation':json.dumps(node['translation']) if 'translation' in node else '', 'model_rotation':json.dumps(node['rotation']) if 'rotation' in node else '', 'model_scale':json.dumps(node['scale']) if 'scale' in node else '', 'model_matrix':json.dumps(node['matrix']) if 'matrix' in node else '', 'animation_first_translation':json.dumps(ac['translation']['first']) if 'translation' in ac else '', 'animation_first_rotation':json.dumps(ac['rotation']['first']) if 'rotation' in ac else '', 'animation_first_scale':json.dumps(ac['scale']['first']) if 'scale' in ac else '', 'animation_channels':';'.join(ac),'channel_details_json':json.dumps(ac,separators=(',',':')),'model_effective_local_translation':json.dumps(translation(node)) if node else '', 'model_to_animation_translation_norm_ratio':''}
 if node and 'translation' in ac:
    mt=np.array(translation(node));at=np.array(ac['translation']['first']);normm=np.linalg.norm(mt);norma=np.linalg.norm(at)
    if normm>1e-5 and norma>1e-5:
        ratio=float(normm/norma);r['model_to_animation_translation_norm_ratio']=ratio;ratios.append({'name':name,'ratio':ratio,'model_translation':mt.tolist(),'animation_translation':at.tolist()})
 rows.append(r)
def hierarchy(j):
 roots=j.get('scenes',[{}])[j.get('scene',0)].get('nodes',[]);levels=[];current=roots
 for depth in range(4):
    levels.append({'depth_from_scene_root':depth,'nodes':[{'index':i,'name':j['nodes'][i]['name'],'children':[j['nodes'][c]['name'] for c in j['nodes'][i].get('children',[])]} for i in current]});current=[c for i in current for c in j['nodes'][i].get('children',[])]
 return levels
hm=hierarchy(m);ha=hierarchy(a)
def named_levels(h,j,p):return [sorted((n['name'],pname(j,p,n['index'])) for n in lv['nodes']) for lv in h]
same=named_levels(hm,m,pm)==named_levels(ha,a,pa)
parent_diffs=[{'node_name':n,'model_parent':pname(m,pm,mn[n]),'animation_parent':pname(a,pa,an[n])} for n in mn.keys()&an.keys() if pname(m,pm,mn[n])!=pname(a,pa,an[n])]
summary={'source_model':str(mp),'source_animation':str(ap),'model_mesh_count':len(m.get('meshes',[])),'animation_mesh_count':len(a.get('meshes',[])),'model_node_count':len(mn),'animation_node_count':len(an),'model_scene_roots_and_first_three_descendant_levels':hm,'animation_scene_roots_and_first_three_descendant_levels':ha,'same_first_three_levels':same,'shared_node_parent_differences':parent_diffs,'model_only_nodes':sorted(mn.keys()-an.keys()),'animation_only_nodes':sorted(an.keys()-mn.keys()),'translation_ratios':ratios,'translation_channel_count':sum('translation' in c for c in channels.values()),'rotation_channel_count':sum('rotation' in c for c in channels.values()),'scale_channel_count':sum('scale' in c for c in channels.values())}
summaryrow={k:'' for k in rows[0]};summaryrow['node_name']='__SUMMARY__';summaryrow['channel_details_json']=json.dumps(summary,ensure_ascii=False,separators=(',',':'));rows.append(summaryrow)
with (OUT/'anim_compare.csv').open('w',encoding='utf-8-sig',newline='') as f:
 w=csv.DictWriter(f,list(rows[0]));w.writeheader();w.writerows(rows)
(LOCAL/'anim_compare_summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
print('COUNTS',len(rows)-1,summary['translation_channel_count'],summary['rotation_channel_count'],summary['scale_channel_count'],'same hierarchy',same,'animation meshes',summary['animation_mesh_count'])
for row in rows:
 if row['node_name'] in ['RootNode','root','rot','Bip01','Bip01 Pelvis','Bip01___Pelvis'] or re.search(r'hip|waist|center|pelvis',row['node_name'],re.I):print('CENTER',row['node_name'],row['model_parent'],row['animation_parent'],row['model_effective_local_translation'],row['animation_first_translation'],row['animation_channels'])
print('NONZERO_RATIOS',len(ratios),'median',float(np.median([r['ratio'] for r in ratios])) if ratios else None)
print('FIRST_RATIOS',ratios[:12])
