import re,sys,os
src=open(sys.argv[1]).read(); out=sys.argv[2]; os.makedirs(out,exist_ok=True)
consts=dict(re.findall(r'private const string (\w+) = @"(.*?)";',src,re.S))
n=0
for m in re.finditer(r'public static Shader (\w+) => _\w+ \?\?= Make\(@"(.*?)"\);',src,re.S):
    body=re.sub(r'"\s*\+\s*(\w+)\s*\+\s*@"',lambda x:consts.get(x.group(1),''),m.group(2)).replace('""','"')
    open(f'{out}/{m.group(1)}.gdshader','w').write(body); n+=1
print(n,'shaders')
