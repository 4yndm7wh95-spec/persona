"""菜单 3D Joker 的运行时数据（游戏里实时渲染入场，MenuJoker3D.cs）：
research/p5r/menu3d_runtime/mesh.bin + <片段>.f16（stage_p5 原样放进 pck 的 res://p5joker/m3d/）；脸部贴图走 pile_textures（m3d_<贴图名>）。
mesh.bin（小端）：'M3D1'、int 网格数、int 总顶点数；每个网格：int 名字字节数 + 名字、int 顶点数、int 索引数、float×4 颜色、
  int 贴图名字节数 + 贴图名（0 = 无贴图）、byte 剔除、byte alpha 裁切阈值、int32×索引数（已换成 Godot 的绕序）、有贴图时 float×2×顶点数 UV。
片段 .f16 = research/p5r/menu3d_data 原样（[帧][顶点][xyz] float16）。
用法：python3 tools/p5/menu3d/export_runtime.py"""
import json, shutil, struct
from pathlib import Path
ROOT = Path(__file__).resolve().parents[3]
SRC = ROOT / 'research/p5r/menu3d_data'
OUT = ROOT / 'research/p5r/menu3d_runtime'
CLIPS = ['main_0000', 'main_0005', 'main_0010', 'main_0015', 'main_0016']   # 四页入场用到的

def main():
    OUT.mkdir(parents=True, exist_ok=True)
    specs = json.load(open(SRC / 'meshes.json'))
    total = sum(int(s['vertices']) for s in specs)
    b = bytearray(b'M3D1') + struct.pack('<ii', len(specs), total)
    for s in specs:
        name = s['name'].encode(); tex = Path(s['texture']).stem.encode() if s.get('texture') else b''
        tri = list(s['triangles'])
        for o in range(0, len(tri), 3): tri[o + 1], tri[o + 2] = tri[o + 2], tri[o + 1]
        col = s.get('color', [1, 1, 1, 1]) if not tex else [1, 1, 1, 1]
        b += struct.pack('<i', len(name)) + name + struct.pack('<ii', int(s['vertices']), len(tri)) + struct.pack('<4f', *col)
        b += struct.pack('<i', len(tex)) + tex + struct.pack('<BB', 1 if s.get('cull', True) else 0, int(s.get('clip', 0)))
        b += struct.pack(f'<{len(tri)}i', *tri)
        if tex:
            uv = [c for pair in s['uv'] for c in pair]
            b += struct.pack(f'<{len(uv)}f', *uv)
    (OUT / 'mesh.bin').write_bytes(bytes(b))
    for c in CLIPS: shutil.copy(SRC / f'{c}.f16', OUT / f'{c}.f16')
    print('mesh.bin', len(b), 'bytes,', len(specs), 'meshes,', total, 'verts')

if __name__ == '__main__':
    main()
