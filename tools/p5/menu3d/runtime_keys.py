"""各页入场 3D Joker 的关键帧（原版逐帧拟合的姿势 + 机位）→ p5mod/src/Vfx/MenuJoker3DKeys.cs（MenuJoker3D 在关键帧之间插值实时渲染）。
数据来源：消耗堆 skill_entrance.py（FRAMES、C2）、弃牌堆 item_entrance.py（FRAMES）、牌组 fit/equip_example/final.json。
用法：python3 tools/p5/menu3d/runtime_keys.py"""
import json, sys
from pathlib import Path
ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(Path(__file__).parent))

def cs_key(u, a, fa, b, fb, w, cam):
    c = ', '.join(f'{float(v):.4f}f' for v in cam)
    return f'\t\tnew({u:.3f}f, "{a}", {fa:.4f}f, "{b}", {fb:.4f}f, {w:.4f}f, new[] {{ {c} }}),\n'

def main():
    import skill_entrance as sk, item_entrance as it
    out = []
    lines = ''
    for idx, (spec, delta) in sk.FRAMES.items():
        cam = list(sk.C2); cam[3] += delta[0]; cam[4] += delta[1]
        w = spec[3] if spec[0] == 'blend' else 0.0
        lines += cs_key(idx - 112, spec[1], spec[2], 'main_0016', 0.0, w, cam)
    out.append(('Exhaust', '消耗堆（原版 SKILL 页 112–122，U = 原版帧 − 112）', lines))
    lines = ''
    for idx, (f, cam) in it.FRAMES.items():
        lines += cs_key(idx - 177, 'main_0010', f, 'main_0010', f, 0.0, cam)
    out.append(('Discard', '弃牌堆（原版 ITEM 页 177–185 + 收尾「186」= 对上 2D 立绘，U = 原版帧 − 177）', lines))
    fin = ROOT / 'tools/p5/menu3d/fit/equip_example/final.json'
    if fin.exists():
        lines = ''
        for idx, (f, cam) in sorted(json.load(open(fin)).items(), key=lambda kv: int(kv[0])):
            lines += cs_key(int(idx) - 233, 'main_0005', f, 'main_0005', f, 0.0, cam)
        out.append(('Deck', '牌组（原版 EQUIP 页 233–239 + 收尾「240」= 枪甩下来对上 2D 立绘，U = 原版帧 − 233）', lines))
    fin = ROOT / 'tools/p5/menu3d/fit/draw_example/final.json'
    split = ''
    if fin.exists():
        d = json.load(open(fin)); lines = ''
        for idx, (f, cam) in sorted(d['keys'].items(), key=lambda kv: int(kv[0])):
            lines += cs_key(int(idx) - 46, 'main_0000', f, 'main_0000', f, 0.0, cam)
        out.append(('Draw', '抽牌堆（原版主菜单 46–52 + 「53」= main_0000 最后一帧、手臂抬起来（机位同 52），然后 2D 手掌砸下来；U = 原版帧 − 46）', lines))
        split = ('\t/// <summary>抽牌堆 46–52 每帧红底 / 星纹的分界线 x = a + b·y（原版坐标，fit/draw_example/split.py）。</summary>\n'
                 '\tpublic static readonly float[][] DrawSplit = { ' + ', '.join(f'new[] {{ {a:.2f}f, {b:.4f}f }}' for a, b in d['split']) + ' };\n')
    body = split + ''.join(f'\t/// <summary>{doc}</summary>\n\tpublic static readonly MenuJoker3D.Key[] {name} =\n\t{{\n{ls}\t}};\n' for name, doc, ls in out)
    (ROOT / 'p5mod/src/Vfx/MenuJoker3DKeys.cs').write_text('// 生成的文件（tools/p5/menu3d/runtime_keys.py），别手改。\nnamespace P5Joker;\n\n'
        '/// <summary>各页入场 3D Joker 的关键帧：原版逐帧拟合的姿势和机位（原版 1920×1080 屏幕坐标的 Godot Camera3D 参数）。</summary>\n'
        'internal static class MenuJoker3DKeys\n{\n' + body + '}\n', encoding='utf-8')
    print('ok', [n for n, _, _ in out])

if __name__ == '__main__':
    main()
