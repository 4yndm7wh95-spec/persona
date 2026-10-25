from pathlib import Path
import sys,subprocess,json,csv
sys.path.insert(0,r'C:\Users\kkk\sts2_tools\p5r_tools\GFS-v0.3.1')
from BlenderToolsForGFS import GFSInterface
REPO=Path(r'C:\Users\kkk\Documents\Codex\2026-10-12\claude-git-c-users-kkk-videos\work\persona-ref-upload');TOOLS=Path(r'C:\Users\kkk\sts2_tools\p5r_tools');LIB=Path(r'C:\Users\kkk\sts2_tools\p5r_rip');LOCAL=LIB/'model/diag_work'
PY=r'C:\Users\kkk\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
BLEND=TOOLS/'blender/blender-4.5.9-windows-x64/blender.exe'
def run(args,log):
    p=subprocess.run([str(x) for x in args],capture_output=True,text=True,encoding='utf-8',errors='replace');log.write_text(p.stdout+'\n'+p.stderr,encoding='utf-8')
    if p.returncode:raise RuntimeError(f'Command failed ({p.returncode}): {log}')
def info(n,src,image):
    g=GFSInterface.from_file(str(src));return {'id':f'{n:04}','source_path':str(src),'texture_names':[t.name for t in g.textures],'mesh_count':len(g.meshes),'front_image':str(image)}
rows=[info(1,LIB/'model/CHARACTER/PERSONA/0001/PS0001.GMD',REPO/'research/p5r/model/diag/arsene_ps0001_front.png')]
for n in range(2,12):
    tag=f'{n:04}';selection=LOCAL/f'select_ps{tag}.txt';selection.write_text(f'MODEL/CHARACTER/PERSONA/{tag}/PS{tag}.GMD\n',encoding='utf-8')
    run([TOOLS/'P5RAssetTool/bin/Release/net9.0/P5RAssetTool.exe',r'G:\Gamebox\P5R\ZS6425\P5R\CPK\BASE.CPK',selection,LIB],LOCAL/f'extract_ps{tag}.log')
    src=LIB/f'model/CHARACTER/PERSONA/{tag}/PS{tag}.GMD';glb=LIB/f'model/arsene_search/ps{tag}.glb';fixed=LIB/f'model/arsene_search/ps{tag}_fixed.glb'
    run([TOOLS/'GFDExport/bin/Release/net9.0/GFDExport.exe',src,glb],LOCAL/f'export_ps{tag}.log')
    run([PY,REPO/'tools/p5r/fix_gfd_glb.py',glb,fixed],LOCAL/f'fix_ps{tag}.log')
    png=LOCAL/f'candidate_ps{tag}_front.png'
    run([BLEND,'-b','--python',TOOLS/'scripts/render_model_check.py','--',fixed,png,'front'],LOCAL/f'render_ps{tag}.log')
    row=info(n,src,png);rows.append(row);(LOCAL/'persona_search.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
    print('CANDIDATE',tag,row['texture_names'],flush=True)
    if any('arsen' in t.lower() for t in row['texture_names']):
        print('Texture name suggests Arsene; stop for visual identification',flush=True);break
print('SEARCH_FINISHED',len(rows),'candidates including PS0001',flush=True)
