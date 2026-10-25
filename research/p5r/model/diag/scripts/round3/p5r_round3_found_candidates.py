from pathlib import Path
import subprocess,json
REPO=Path(r'C:\Users\kkk\Documents\Codex\2026-10-12\claude-git-c-users-kkk-videos\work\persona-ref-upload');TOOLS=Path(r'C:\Users\kkk\sts2_tools\p5r_tools');LIB=Path(r'C:\Users\kkk\sts2_tools\p5r_rip');LOCAL=LIB/'model/round3_work';PY=r'C:\Users\kkk\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
def run(args,log):
 p=subprocess.run([str(x) for x in args],capture_output=True,text=True,encoding='utf-8',errors='replace');log.write_text(p.stdout+'\n'+p.stderr,encoding='utf-8')
 if p.returncode:raise RuntimeError(str(log))
hits=json.loads((LOCAL/'texture_search_summary.json').read_text())['hits'];results=[]
for r in hits:
 src=LIB/r['package_path'];tag=src.stem;glb=LIB/f'model/arsene_search_round3/{tag}.glb';fixed=glb.with_stem(tag+'_fixed')
 run([TOOLS/'GFDExport/bin/Release/net9.0/GFDExport.exe',src,glb],LOCAL/f'found_export_{tag}.log')
 run([PY,REPO/'tools/p5r/fix_gfd_glb.py',glb,fixed,'--flip-v'],LOCAL/f'found_fix_{tag}.log')
 for view in ['front','threequarter']:
  png=LOCAL/f'found_{tag}_{view}.png';run([TOOLS/'blender/blender-4.5.9-windows-x64/blender.exe','-b','--python',TOOLS/'scripts/render_model_check.py','--',fixed,png,view],LOCAL/f'found_render_{tag}_{view}.log')
 results.append({'package_path':r['package_path'],'textures':r['texture_names'],'glb':str(glb),'fixed_glb':str(fixed),'front':str(LOCAL/f'found_{tag}_front.png'),'threequarter':str(LOCAL/f'found_{tag}_threequarter.png')});print('FOUND_CANDIDATE_RENDERED',tag,flush=True)
(LOCAL/'found_candidates.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')
