import subprocess,sys,json
from pathlib import Path
lib=Path(r'C:/Users/kkk/sts2_tools/p5r_rip');t=Path(r'C:/Users/kkk/sts2_tools/p5r_tools');r=Path(r'C:/Users/kkk/Documents/Codex/2026-10-12/claude-git-c-users-kkk-videos/work/persona-ref-upload');o=lib/'model/morgana_ref';o.mkdir(parents=True,exist_ok=True)
def run(args,name):
 p=subprocess.run(list(map(str,args)),capture_output=True,text=True,encoding='utf-8',errors='replace');(o/(name+'.log')).write_text(p.stdout+'\n'+p.stderr,encoding='utf-8');print(name,p.returncode,p.stdout[-800:],flush=True)
 if p.returncode:raise RuntimeError(str(o/(name+'.log')))
sel=o/'selection.txt';sel.write_text('MODEL/CHARACTER/0009/C0009_051_00.GMD\n',encoding='utf-8')
run([t/'P5RAssetTool/bin/Release/net9.0/P5RAssetTool.exe',r'G:/Gamebox/P5R/ZS6425/P5R/CPK/BASE.CPK',sel,lib],'extract')
src=lib/'MODEL/CHARACTER/0009/C0009_051_00.GMD';raw=o/'morgana.glb';fixed=o/'morgana_fixed.glb'
run([t/'GFDExport/bin/Release/net9.0/GFDExport.exe',src,raw],'export')
run([sys.executable,r/'tools/p5r/fix_gfd_glb.py',raw,fixed,'--drop','Blur','--flip-v'],'fix')
