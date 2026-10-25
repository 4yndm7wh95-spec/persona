"""修 GFD Studio 导出的 P5R GLB（人散成碎块）。
顶点本来就是模型空间（Y 上、单位 cm），但导出时
- 蒙皮网格：逆绑定矩阵被乘进了网格节点的变换 → 重写 IBM = inverse(骨骼静止世界矩阵)；
- 没蒙皮、挂在骨骼节点下的网格（头发、面具、眼睛）：会再被节点变换一次 → 顶点乘节点世界矩阵的逆抵消。
python3 tools/p5r/fix_gfd_glb.py in.glb out.glb [--drop 名字片段,名字片段]
--drop：去掉名字含这些片段的网格节点（例如 kamen = 怪盗面具）。"""
import json, struct, sys
import numpy as np

src, dst = sys.argv[1], sys.argv[2]
drop = sys.argv[sys.argv.index('--drop') + 1].split(',') if '--drop' in sys.argv else []
b = open(src, 'rb').read()
l = struct.unpack('<I', b[12:16])[0]
j = json.loads(b[20:20 + l])
data = bytearray(b[20 + l + 8:20 + l + 8 + struct.unpack('<I', b[20 + l:24 + l])[0]])
nodes = j['nodes']
parent = {}
for i, n in enumerate(nodes):
    for c in n.get('children', []):
        parent[c] = i


def local(n):
    if 'matrix' in n:
        return np.array(n['matrix']).reshape(4, 4).T
    t = np.eye(4)
    if 'translation' in n:
        t[:3, 3] = n['translation']
    r = np.eye(4)
    if 'rotation' in n:
        x, y, z, w = n['rotation']
        r[:3, :3] = [[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                     [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                     [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]]
    s = np.eye(4)
    if 'scale' in n:
        s[0, 0], s[1, 1], s[2, 2] = n['scale']
    return t @ r @ s


def world(i):
    m = local(nodes[i])
    while i in parent:
        i = parent[i]
        m = local(nodes[i]) @ m
    return m


done = set()
for sk in j['skins']:
    ai = sk['inverseBindMatrices']
    if ai in done:
        continue
    done.add(ai)
    a = j['accessors'][ai]
    v = j['bufferViews'][a['bufferView']]
    off = v.get('byteOffset', 0) + a.get('byteOffset', 0)
    mats = np.stack([np.linalg.inv(world(jn)) for jn in sk['joints']]).transpose(0, 2, 1)   # 列主序
    data[off:off + len(sk['joints']) * 64] = mats.astype(np.float32).tobytes()
# 没有蒙皮、挂在骨骼节点下的网格（头发、面具、眼睛）：顶点同样已是模型空间，但会再被节点变换一次 → 乘上节点世界矩阵的逆抵消，跟着骨骼动
vdone = set()
for i, n in enumerate(nodes):
    if 'mesh' not in n or 'skin' in n:
        continue
    inv = np.linalg.inv(world(i))
    for p in j['meshes'][n['mesh']]['primitives']:
        for key in ('POSITION', 'NORMAL'):
            ai = p['attributes'].get(key)
            if ai is None or ai in vdone:
                continue
            vdone.add(ai)
            a = j['accessors'][ai]
            v = j['bufferViews'][a['bufferView']]
            assert a['componentType'] == 5126 and a['type'] == 'VEC3' and v.get('byteStride', 12) == 12
            off = v.get('byteOffset', 0) + a.get('byteOffset', 0)
            arr = np.frombuffer(bytes(data[off:off + a['count'] * 12]), np.float32).reshape(-1, 3).astype(np.float64)
            if key == 'POSITION':
                arr = arr @ inv[:3, :3].T + inv[:3, 3]
                a['min'] = arr.min(0).tolist()
                a['max'] = arr.max(0).tolist()
            else:
                arr = arr @ np.linalg.inv(inv[:3, :3]).T.T   # 法线矩阵 = (inv⁻¹)ᵀ
                arr /= np.maximum(np.linalg.norm(arr, axis=1, keepdims=True), 1e-9)
            data[off:off + a['count'] * 12] = arr.astype(np.float32).tobytes()
print('rebased', len(vdone), 'unskinned accessors')
dropped = []
for n in nodes:
    if 'mesh' in n and any(d.lower() in n.get('name', '').lower() for d in drop):
        dropped.append(n['name'])
        del n['mesh']
        n.pop('skin', None)
print('dropped', dropped)
js = json.dumps(j, separators=(',', ':')).encode()
js += b' ' * ((4 - len(js) % 4) % 4)
data += b'\0' * ((4 - len(data) % 4) % 4)
out = struct.pack('<III', 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(data)) + struct.pack('<II', len(js), 0x4E4F534A) + js + struct.pack('<II', len(data), 0x004E4942) + bytes(data)
open(dst, 'wb').write(out)
print('rewrote', len(done), 'IBM accessors ->', dst)
