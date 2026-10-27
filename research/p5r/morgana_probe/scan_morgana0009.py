import sys,csv,json,subprocess
from pathlib import Path
sys.path.insert(0,r'C:/Users/kkk/sts2_tools/p5r_tools/GFS-v0.3.1')
from BlenderToolsForGFS import GFSInterface
r=Path(r'C:/Users/kkk/Documents/Codex/2026-10-12/claude-git-c-users-kkk-videos/work/persona-ref-upload');lib=Path(r'C:/Users/kkk/sts2_tools/p5r_rip');o=lib/'model/morgana_ref';rows=[x for x in csv.DictReader((r/'research/p5r/index.csv').open(encoding='utf-8-sig')) if x['path'].startswith('MODEL/CHARACTER/0009/') and x['path'].endswith('.GMD')];sel=o/'selection0009.txt';sel.write_text('\n'.join(x['path'] for x in rows)+'\n',encoding='utf-8');p=subprocess.run([r'C:/Users/kkk/sts2_tools/p5r_tools/P5RAssetTool/bin/Release/net9.0/P5RAssetTool.exe',r'G:/Gamebox/P5R/ZS6425/P5R/CPK/BASE.CPK',str(sel),str(lib)],capture_output=True,text=True,encoding='utf-8',errors='replace');(o/'extract0009.log').write_text(p.stdout+'\n'+p.stderr,encoding='utf-8');print('extract',p.returncode,p.stdout[-180:],flush=True)
if p.returncode:raise RuntimeError('extract failed')
out=[]
for x in rows:
 try:
  g=GFSInterface.from_file(str(lib/x['path']));names=[t.name for t in g.textures];out.append({'path':x['path'],'textures':names,'meshes':len(g.meshes)});print(Path(x['path']).name,','.join(names[:3]),flush=True)
 except Exception as e:out.append({'path':x['path'],'error':str(e)})
(o/'0009_texture_inventory.json').write_text(json.dumps(out,ensure_ascii=False,indent=2),encoding='utf-8')
