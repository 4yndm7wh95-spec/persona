from pathlib import Path
import sys,csv,json,subprocess,re
sys.path.insert(0,r'C:\Users\kkk\sts2_tools\p5r_tools\GFS-v0.3.1')
from BlenderToolsForGFS import GFSInterface
from BlenderToolsForGFS.src.FileFormats.GFS.Interface import EPLFileInterface
REPO=Path(r'C:\Users\kkk\Documents\Codex\2026-10-12\claude-git-c-users-kkk-videos\work\persona-ref-upload');TOOLS=Path(r'C:\Users\kkk\sts2_tools\p5r_tools');LIB=Path(r'C:\Users\kkk\sts2_tools\p5r_rip');LOCAL=LIB/'model/round3_work';OUT=REPO/'research/p5r/model/diag'
index=list(csv.DictReader((REPO/'research/p5r/index.csv').open(encoding='utf-8-sig')))
entries=[r for r in index if re.match(r'^MODEL/CHARACTER/PERSONA/.*/PS[^/]*\.GMD$',r['path'])]
epl=next(r for r in index if r['path']=='BATTLE/EVENT/BCD/0282/EPL/ARSENE.EPL');entries.append(epl)
def target(r):return LIB/r['path']
missing=[r for r in entries if not target(r).exists() or target(r).stat().st_size!=int(r['size_bytes'])]
print('TEXTURE_SEARCH',len(entries)-1,'GMD + 1 EPL; missing',len(missing),'bytes',sum(int(r['size_bytes']) for r in missing),flush=True)
for cpk in sorted(set(r['cpk'] for r in missing)):
    selected=[r['path'] for r in missing if r['cpk']==cpk];selection=LOCAL/f'texture_search_{cpk}.txt';selection.write_text('\n'.join(selected)+'\n',encoding='utf-8')
    p=subprocess.run([str(TOOLS/'P5RAssetTool/bin/Release/net9.0/P5RAssetTool.exe'),str(Path(r'G:\Gamebox\P5R\ZS6425\P5R\CPK')/cpk),str(selection),str(LIB)],capture_output=True,text=True,encoding='utf-8',errors='replace');(LOCAL/f'texture_search_extract_{cpk}.log').write_text(p.stdout+'\n'+p.stderr,encoding='utf-8')
    print('EXTRACTION',cpk,p.returncode,p.stdout[-300:],flush=True)
    if p.returncode:raise RuntimeError('Extraction failed; see local log')
rows=[];keywords=['arsene','arsenu','arsen','ars_','0001'];hits=[]
for i,r in enumerate(entries):
    row={'cpk':r['cpk'],'package_path':r['path'],'id':r['path'].split('/')[3] if r['path'].startswith('MODEL/') else '', 'texture_names':'','parse_status':'','error':'','keyword_hits':''}
    try:
        if r['path'].endswith('.EPL'):
            obj=EPLFileInterface.from_file(str(target(r)))
            (LOCAL/'arsene_epl_structure.txt').write_text(str(type(obj))+'\n'+str(vars(obj).keys()),encoding='utf-8')
            # Walk parsed objects without dumping binary payloads or reparsing embedded bytes.
            textures=[];seen=set()
            def walk(v):
                if v is None or isinstance(v,(str,bytes,int,float,bool)):return
                if id(v) in seen:return
                seen.add(id(v))
                if type(v).__name__=='TextureInterface':textures.append(v.name);return
                if isinstance(v,(list,tuple)):
                    for x in v:walk(x)
                elif isinstance(v,dict):
                    for x in v.values():walk(x)
                elif hasattr(v,'__dict__'):
                    for k,x in vars(v).items():
                        if k not in ('pixels','payload','vertices','indices'):walk(x)
            walk(obj);names=sorted(set(textures))
            row['parse_status']='ok' if names else 'parsed_no_texture_interface_exposed'
        else:
            g=GFSInterface.from_file(str(target(r)));names=[t.name for t in g.textures];row['parse_status']='ok'
        row['texture_names']=';'.join(names);matches=[k for k in keywords if any(k in n.lower() for n in names)];row['keyword_hits']=';'.join(matches)
        if matches:hits.append(row);print('TEXTURE_HIT',r['path'],names,flush=True)
    except Exception as e:row['parse_status']='failed';row['error']=f'{type(e).__name__}: {e}'
    rows.append(row)
    if (i+1)%50==0:print('PARSED',i+1,'/',len(entries),flush=True)
with (OUT/'persona_texture_names.csv').open('w',encoding='utf-8-sig',newline='') as f:
    w=csv.DictWriter(f,list(rows[0]));w.writeheader();w.writerows(rows)
summary={'gmd_count':len(entries)-1,'epl_count':1,'rows':len(rows),'parse_failed':[r for r in rows if r['parse_status']=='failed'],'keywords':keywords,'hits':hits,'epl_result':rows[-1]}
(LOCAL/'texture_search_summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
print('TEXTURE_SEARCH_DONE',len(rows),'rows; failed',len(summary['parse_failed']),'hits',len(hits),flush=True)
