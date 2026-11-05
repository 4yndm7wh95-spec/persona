"""Prepare a standalone Godot project from the real mod shaders and native cut-in mesh data."""
from pathlib import Path
import argparse, re, shutil
p=argparse.ArgumentParser();p.add_argument('--eyes',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
root=Path(__file__).resolve().parents[3];out=a.output;out.mkdir(parents=True,exist_ok=True)
for source,target in [('CrushHand.cs','hand.gdshader'),('CrushWave.cs','wave.gdshader')]:
 text=(root/'p5mod/src/Vfx'/source).read_text(encoding='utf-8')
 code=re.search(r'Code = @"\s*(shader_type.*?)" };',text,re.S).group(1)
 (out/target).write_text(code,encoding='utf-8')
shutil.copy2(a.eyes,out/'eyes.png')
shutil.copy2(root/'p5mod/data/eye_cutin.json',out/'eye_cutin.json')
shutil.copy2(Path(__file__).with_name('visual_check.gd'),out/'check.gd')
stage=(root/'p5mod/src/Vfx/SummonStage.cs').read_text(encoding='utf-8')
includes=(root/'p5mod/src/Vfx/CutIn.cs').read_text(encoding='utf-8')
shared=re.search(r'const string ShatterGlsl = @"(.*?)";',includes,re.S).group(1)
shader=re.search(r'Code = @"(.*?)" };',stage,re.S).group(1).replace('" + CutIn.ShatterGlsl + @"',shared)
(out/'stage.gdshader').write_text(shader,encoding='utf-8')
shutil.copy2(Path(__file__).with_name('layer_order.gd'),out/'layer_order.gd')
(out/'project.godot').write_text('config_version=5\n[application]\nconfig/name="P5 isolated shader checks"\n[display]\nwindow/size/viewport_width=960\nwindow/size/viewport_height=540\n[rendering]\nrenderer/rendering_method="gl_compatibility"\n',encoding='utf-8')
print(out)
