"""slash_a10 = slash_a9 with the back (left) fist hidden behind the body.

The user (2026-10-28): the fist tucked at the back waist always looked twisted; it should run down along the
forearm and be hidden by the body. So the fist is cut away along the sleeve's outer edge, extended
straight down until it meets the coat; inside that line becomes sleeve black, outside becomes background.
    python tools/p5/hide_back_fist.py
"""
import numpy as np
from PIL import Image
from scipy import ndimage
a=np.array(Image.open('art/p5/poses/slash_a9.png').convert('RGBA')).astype(float)
r,g,b,al=[a[...,i] for i in range(4)]
box=np.zeros(al.shape,bool); box[560:625,436:490]=True
red=(al>40)&(r>120)&(g<110)&(b<110)&box
fist=ndimage.binary_dilation(red,iterations=6)&box
# continue the sleeve's outer edge (420,510)->(452,568) straight down until it meets the coat edge (~478,613)
p0,p1=np.array([420.,510.]),np.array([478.,613.])
d=p1-p0; n=np.array([-d[1],d[0]])/np.hypot(*d)        # normal pointing to image-left (outside)
ys,xs=np.mgrid[0:al.shape[0],0:al.shape[1]]
sd=(xs-p0[0])*n[0]+(ys-p0[1])*n[1]                      # >0 outside the sleeve, <0 inside
out=a.copy()
inside=(fist|((al<250)&box))&(sd<0); outside=fist&(sd>=0)
out[inside,:3]=(16,16,20); out[inside,3]=255*np.clip(0.5-sd,0,1)[inside].clip(0,1)**0*1
cov=np.clip(0.5-sd,0,1)                                 # 1px anti-aliased edge
out[outside,:3]=(16,16,20); out[outside,3]=255*cov[outside]
big=np.zeros(al.shape,bool); big[550:630,425:495]=True
stray=big&(sd>=1)&(out[...,3]<230)
out[stray,3]=0
img=Image.fromarray(out.clip(0,255).astype(np.uint8)); img.save('art/p5/poses/slash_a10.png')
print(red.sum(),inside.sum(),outside.sum())
