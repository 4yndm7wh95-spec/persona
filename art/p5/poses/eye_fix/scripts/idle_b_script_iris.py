from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import numpy as np,json,hashlib,shutil
repo=Path(r'C:/Users/kkk/Documents/Codex/2026-10-12/claude-git-c-users-kkk-videos/work/persona-ref-upload');root=repo/'art/p5/poses';e=root/'eye_fix';out=Path(__file__).parent.parent/'outputs/idle_b_review';out.mkdir(parents=True,exist_ok=True)
beforepath=e/'idle_b_script_before.png'
if not beforepath.exists():shutil.copy2(root/'idle_b.png',beforepath)
im=Image.open(beforepath).convert('RGBA');original=np.array(im);arr=original.copy();mask=Image.new('L',im.size,0);d=ImageDraw.Draw(mask)
# Visible near iris interior, mapped at native resolution from the head crop.
# The polygon stays within sclera and below the fixed upper eyelid; highlights
# inside this piece move together with the iris. A one-pixel adjustment back toward center avoids
# resampling the drawing. Far iris is partly occluded and left untouched.
points=[(579,147),(584,149),(586,151),(586,154),(584,157),(581,158),(578,156),(577,152),(577,149)]
d.polygon(points,fill=255);m=np.array(mask)>0;ys,xs=np.where(m);dx=-1;dy=0
# Fill only the newly exposed strip, using adjacent sclera on the same row.
shifted=np.zeros_like(m);shifted[ys+dy,xs+dx]=True;vacated=m&~shifted
for y,x in zip(*np.where(vacated)):
 sx=min(original.shape[1]-1,max(np.where(m[y])[0])+2);arr[y,x,:3]=original[y,sx,:3]
# Move the whole selected iris piece, retaining exact original RGBA values.
arr[ys+dy,xs+dx,:3]=original[ys,xs,:3];changed=np.any(arr!=original,axis=2);allowed=m|shifted
assert not changed[~allowed].any();assert np.array_equal(arr[:,:,3],original[:,:,3]);assert np.array_equal(arr[300:,:,:],original[300:,:,:])
result=Image.fromarray(arr);result.save(root/'idle_b.png');result.save(e/'idle_b_script_after.png');Image.fromarray(np.uint8(allowed)*255).save(e/'idle_b_script_mask.png')
shutil.copy2(beforepath,out/'before.png');result.save(out/'after.png')
canvas=Image.new('RGB',(2048,1584),'#eeeeee');cd=ImageDraw.Draw(canvas);font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',26)
for i,(label,pic) in enumerate([('BEFORE - idle_b',im),('AFTER - iris adjusted 1 px',result)]):
 bg=Image.new('RGBA',pic.size,'#d4d4d4');bg.alpha_composite(pic);canvas.paste(bg.convert('RGB'),(i*1024,48));cd.text((i*1024+24,10),label,font=font,fill='black')
canvas.save(out/'full_body_before_after.png')
head=Image.new('RGB',(1024,512),'#dddddd')
for i,pic in enumerate([im,result]):
 crop=pic.crop((440,30,690,260)).resize((512,471),Image.Resampling.LANCZOS);bg=Image.new('RGBA',crop.size,'#d4d4d4');bg.alpha_composite(crop);head.paste(bg.convert('RGB'),(i*512,32))
ImageDraw.Draw(head).text((8,8),'BEFORE',fill='black');ImageDraw.Draw(head).text((520,8),'AFTER',fill='black');head.save(out/'head_before_after.png')
record={'name':'idle_b','method':'script iris translation','near_iris_shift_xy_native_px':[-1,0],'near_eye_opening_estimated_x':[569,591],'iris_center_estimated_before_x':583,'iris_center_estimated_after_x':582,'iris_center_fraction_after_estimate':0.591,'far_eye':'partially occluded; unchanged','outside_mask_different_pixels':int(changed[~allowed].sum()),'alpha_different_pixels':int((arr[:,:,3]!=original[:,:,3]).sum()),'body_y_ge300_different_pixels':int(np.any(arr[300:]!=original[300:],axis=2).sum()),'changed_pixels':int(changed.sum()),'before_sha256':hashlib.sha256(beforepath.read_bytes()).hexdigest(),'after_sha256':hashlib.sha256((root/'idle_b.png').read_bytes()).hexdigest(),'visual_approval':'pending user confirmation','transparency_cleanup':'not yet; after all eight approvals'}
(e/'idle_b_script_verification.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8');(out/'verification.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(record,ensure_ascii=False))
