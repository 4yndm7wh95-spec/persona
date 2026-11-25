"""把原版预告信拉正：正面 P5_CARD02；背面 = 画廊里 7 封信的正文（鸭志田、斑目、金城、双叶、奥村、新岛、丸喜）。
4 倍超采样透视变换 → Lanczos 缩到 640x458，四周留 4px 透明边（转动时边缘靠双线性过滤抗锯齿）。"""
import numpy as np
from PIL import Image
W, H, P, S = 640, 458, 4, 4
def coeffs(dst, src):
    A, B = [], []
    for (x, y), (u, v) in zip(dst, src):
        A += [[x, y, 1, 0, 0, 0, -u * x, -u * y], [0, 0, 0, x, y, 1, -v * x, -v * y]]; B += [u, v]
    return np.linalg.solve(np.array(A, float), np.array(B, float)).tolist()
def deskew(src_path, quad, out):
    im = Image.open(src_path).convert('RGBA')
    big = im.transform((W * S, H * S), Image.PERSPECTIVE, coeffs([(0, 0), (W * S, 0), (W * S, H * S), (0, H * S)], quad), Image.BICUBIC)
    big.putalpha(255)
    o = Image.new('RGBA', (W + 2 * P, H + 2 * P)); o.paste(big.resize((W, H), Image.LANCZOS), (P, P)); o.save(out)
G = 'research/p5r/calling_card/found/gallery__IMG%s.png'
SAME = [(1470, 1820), (3540, 1612), (3700, 3015), (1642, 3346)]      # 098–102、126 构图相同
LETTERS = {
    'kamoshida': ('097', [(1310, 1636), (3612, 1824), (3554, 3292), (1238, 3382)]),
    'madarame': ('098', SAME), 'kaneshiro': ('099', SAME), 'futaba': ('100', SAME),
    'okumura': ('101', SAME), 'sae': ('102', SAME), 'maruki': ('126', SAME),
}
if __name__ == '__main__':
    deskew('research/p5r/ui/camp/SC__P5_CARD02__90df21923c.png', [(241, 67), (764, 87), (784, 421), (250, 442)], 'art/p5/ui/pile/calling_card.png')
    for k, (n, q) in LETTERS.items():
        deskew(G % n, q, f'art/p5/ui/pile/letters/letter_{k}.png')
