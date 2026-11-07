"""Prepare a standalone Godot project from the real mod shaders and native cut-in mesh data."""
from pathlib import Path
import argparse, re, shutil
p=argparse.ArgumentParser();p.add_argument('--eyes',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
root=Path(__file__).resolve().parents[3];out=a.output;out.mkdir(parents=True,exist_ok=True)
for source,target in [('CrushHand.cs','hand.gdshader'),('CrushWave.cs','wave.gdshader')]:
 text=(root/'p5mod/src/Vfx'/source).read_text(encoding='utf-8')
 code=re.search(r'Code = @"\s*(shader_type.*?)" };',text,re.S).group(1)
 (out/target).write_text(code,encoding='utf-8')
 # Mirror the two exact replacements used by CrushHand.PackedShader.
 if target == 'hand.gdshader':
  old='COLOR = vec4(rgb / max(a, 0.001), a * alpha * keep);'
  assert code.count(old)==1
  packed=code.replace('shader_type canvas_item;','shader_type canvas_item;\nrender_mode blend_disabled, unshaded;').replace(old,'COLOR = vec4(rgb * alpha, (front > 0.5 ? 0.51 : 0.0) + 0.49 * a * alpha);')
  (out/'hand_packed.gdshader').write_text(packed,encoding='utf-8')
composite=(root/'p5mod/src/Vfx/CrushHandFrame.cs').read_text(encoding='utf-8')
code=re.search(r'Code = @"\s*(shader_type.*?)" };',composite,re.S).group(1)
(out/'hand_composite.gdshader').write_text(code,encoding='utf-8')
shutil.copy2(Path(__file__).with_name('hand_cache_check.gd'),out/'hand_cache_check.gd')
shutil.copy2(a.eyes,out/'eyes.png')
shutil.copy2(root/'p5mod/data/eye_cutin.json',out/'eye_cutin.json')
shutil.copy2(Path(__file__).with_name('visual_check.gd'),out/'check.gd')
stage=(root/'p5mod/src/Vfx/SummonStage.cs').read_text(encoding='utf-8')
includes=(root/'p5mod/src/Vfx/CutIn.cs').read_text(encoding='utf-8')
shared=re.search(r'const string ShatterGlsl = @"(.*?)";',includes,re.S).group(1)
shader=re.search(r'Code = @"(.*?)" };',stage,re.S).group(1).replace('" + CutIn.ShatterGlsl + @"',shared)
(out/'stage.gdshader').write_text(shader,encoding='utf-8')
shutil.copy2(Path(__file__).with_name('layer_order.gd'),out/'layer_order.gd')
layer=Path(__file__).with_name('layer_order.gd').read_text(encoding='utf-8')
cache=Path(__file__).with_name('hand_cache_check.gd').read_text(encoding='utf-8')
helpers=cache.split('\n',1)[1].split('func percentile(',1)[0]
layer=layer.replace('var legacy :=',helpers+'\nvar legacy :=',1)
old='rear = make_hand(palm,1)\n\tfront = make_hand(anchored("Fingers"),2)\n\tset_pose(0.8)'
new='''rear = make_hand(palm,1)
	var fingers := anchored("Fingers")
	front = make_hand(fingers,2)
	(palm.get_child(0) as CanvasItem).hide()
	(fingers.get_child(0) as CanvasItem).hide()
	make_cached(palm,palm,fingers,720)
	cache.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	pose(0.8)'''
assert old in layer
layer=layer.replace(old,new)
(out/'layer_cache_check.gd').write_text(layer,encoding='utf-8')
(out/'project.godot').write_text('config_version=5\n[application]\nconfig/name="P5 isolated shader checks"\n[display]\nwindow/size/viewport_width=960\nwindow/size/viewport_height=540\n[rendering]\nrenderer/rendering_method="gl_compatibility"\n',encoding='utf-8')
print(out)
