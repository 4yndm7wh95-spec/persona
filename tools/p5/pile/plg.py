import struct,sys,json
from PIL import Image,ImageDraw
def read(path):
    d=open(path,'rb').read(); n=struct.unpack_from('<I',d,0x1c)[0]; out=[]
    for i in range(n):
        o=0x38+i*72
        a,b,c,nv,ni,z,k=struct.unpack_from('<IIIHHII',d,o)
        name=d[o+40:o+72].split(b'\0')[0].decode('latin1')
        vs=[];cols=[]
        for j in range(nv):
            col,x,y,_=struct.unpack_from('<Ifff',d,o+a+16*j); vs.append((x,y)); cols.append(col)
        idx=struct.unpack_from(f'<{ni}H',d,o+b)
        out.append(dict(name=name,v=vs,col=cols,idx=list(idx),bbox=struct.unpack_from('<4f',d,o+24)))
    return out
if __name__=='__main__':
    P=read(sys.argv[1]); C=6; W=330
    R=(len(P)+C-1)//C; sh=Image.new('RGB',(C*W,R*W),(70,90,140)); dr=ImageDraw.Draw(sh)
    P=[p for p in P if p['v']]
    for i,p in enumerate(P):
        xs=[v[0] for v in p['v']]; ys=[v[1] for v in p['v']]
        x0,y0,x1,y1=min(xs),min(ys),max(xs),max(ys); s=(W-30)/max(x1-x0,y1-y0,1)
        ox,oy=(i%C)*W+10,(i//C)*W+20
        for t in range(0,len(p['idx'])-2,3):
            tri=[(ox+(p['v'][q][0]-x0)*s,oy+(p['v'][q][1]-y0)*s) for q in p['idx'][t:t+3]]
            c=p['col'][p['idx'][t]]; rgb=(c&255,(c>>8)&255,(c>>16)&255)
            dr.polygon(tri,fill=rgb)
        dr.text((ox,oy-16),f"{p['name']} {x1-x0:.0f}x{y1-y0:.0f}",fill='yellow')
    sh.save(sys.argv[2],quality=85)
    json.dump(P,open(sys.argv[2]+'.json','w'))
