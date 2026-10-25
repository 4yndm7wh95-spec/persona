from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import numpy as np,json,hashlib,shutil
repo=Path(r'C:/Users/kkk/Documents/Codex/2026-10-12/claude-git-c-users-kkk-videos/work/persona-ref-upload');root=repo/'art/p5/poses';e=root/'eye_fix';out=Path(__file__).parent.parent/'outputs/guard_a_review';out.mkdir(parents=True,exist_ok=True)
beforepath=e/'guard_a_script_before.png'
if not beforepath.exists():shutil.copy2(root/'guard_a.png',beforepath)
im=Image.open(beforepath).convert('RGBA');original=np.array(im);arr=original.copy();mask=Image.new('L',im.size,0);d=ImageDraw.Draw(mask)
# Visible near iris interior, mapped at native resolution from the head crop.
# The polygon stays within sclera and below the fixed upper eyelid; highlights
# inside this piece move together with the iris. A one-pixel integer shift avoids
# resampling the drawing. Far iris is partly occluded and left untouched.
points=[(593,187),(598,188),(601,189),(603,192),(602,195),(600,198),(596,198),(593,196),(591,192),(591,189)]
d.polygon(points,fill=255);m=np.array(mask)>0;ys,xs=np.where(m);dx=-1;dy=0
# Fill only the newly exposed strip, using adjacent sclera on the same row.
shifted=np.zeros_like(m);shifted[ys+dy,xs+dx]=True;vacated=m&~shifted
for y,x in zip(*np.where(vacated)):
 sx=min(original.shape[1]-1,max(np.where(m[y])[0])+2);arr[y,x,:3]=original[y,sx,:3]
# Move the whole selected iris piece, retaining exact original RGBA values.
arr[ys+dy,xs+dx,:3]=original[ys,xs,:3];changed=np.any(arr!=original,axis=2);allowed=m|shifted
assert not changed[~allowed].any();assert np.array_equal(arr[:,:,3],original[:,:,3]);assert np.array_equal(arr[300:,:,:],original[300:,:,:])
result=Image.fromarray(arr);result.save(root/'guard_a.png');result.save(e/'guard_a_script_after.png');Image.fromarray(np.uint8(allowed)*255).save(e/'guard_a_script_mask.png')
shutil.copy2(beforepath,out/'before.png');result.save(out/'after.png')
canvas=Image.new('RGB',(2048,1584),'#eeeeee');cd=ImageDraw.Draw(canvas);font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',26)
for i,(label,pic) in enumerate([('BEFORE - guard_a',im),('AFTER - iris adjusted 1 px',result)]):
 bg=Image.new('RGBA',pic.size,'#d4d4d4');bg.alpha_composite(pic);canvas.paste(bg.convert('RGB'),(i*1024,48));cd.text((i*1024+24,10),label,font=font,fill='black')
canvas.save(out/'full_body_before_after.png')
head=Image.new('RGB',(1024,512),'#dddddd')
for i,pic in enumerate([im,result]):
 crop=pic.crop((450,50,700,280)).resize((512,471),Image.Resampling.LANCZOS);bg=Image.new('RGBA',crop.size,'#d4d4d4');bg.alpha_composite(crop);head.paste(bg.convert('RGB'),(i*512,32))
ImageDraw.Draw(head).text((8,8),'BEFORE',fill='black');ImageDraw.Draw(head).text((520,8),'AFTER',fill='black');head.save(out/'head_before_after.png')
record={'name':'guard_a','method':'script iris translation','near_iris_shift_xy_native_px':[-1,0],'near_eye_opening_estimated_x':[583,607],'iris_center_estimated_before_x':598,'iris_center_estimated_after_x':597,'iris_center_fraction_after_estimate':0.583,'far_eye':'partially occluded; unchanged','outside_mask_different_pixels':int(changed[~allowed].sum()),'alpha_different_pixels':int((arr[:,:,3]!=original[:,:,3]).sum()),'body_y_ge300_different_pixels':int(np.any(arr[300:]!=original[300:],axis=2).sum()),'changed_pixels':int(changed.sum()),'before_sha256':hashlib.sha256(beforepath.read_bytes()).hexdigest(),'after_sha256':hashlib.sha256((root/'guard_a.png').read_bytes()).hexdigest(),'visual_approval':'pending user confirmation','transparency_cleanup':'not yet; after all eight approvals'}
(e/'guard_a_script_verification.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8');(out/'verification.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(record,ensure_ascii=False))
