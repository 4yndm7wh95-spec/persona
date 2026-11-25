"""弃牌堆入场的收尾机位（原版第 186 帧 = 2D 立绘 CAMPITEM_2D01）：3D 最后定在立绘的样子再切，切的时候脸 / 视线不跳。
用户 2026-11-24：185 那一帧的机位（对原版 185 拟合的）低着头、眼睛被头发挡住，立绘是抬眼看玩家 —— 切的时候明显跳一下。
特征点直接从我们游戏里显示的立绘上量（预览 pile_discard 定格后的一帧，1080p 屏幕坐标），按 MapK、(-80, 0) 换回原版屏幕坐标再拟合
（我们的立绘比 MapK 推出来的位置略往左、略小一点，直接量显示出来的才对得上）。
用法：python3 closing.py → 打印每个动作帧的最优机位和误差。"""
import sys, numpy as np
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from fitlib import pts3, best_fit, fit
MAPK = 1500 / 3072 / 0.5
OURS = {'eyeR': (593, 367), 'eyeL': (728, 338), 'nose': (715, 428), 'mouth': (670, 465), 'chin': (683, 523)}
OBS = {k: ((x + 80) / MAPK, y / MAPK) for k, (x, y) in OURS.items()}
W = {'eyeR': 1.0, 'eyeL': 1.0, 'nose': 0.8, 'mouth': 0.6, 'chin': 0.4}
LAST = [-17.784, 168.941, 81.15, -10.921, -23.217, -0.308, 29.999]   # item_entrance.py 的 185

if __name__ == '__main__':
    for f in (8.22, 8.6, 9.0, 9.5, 10.0):
        hp, _ = pts3('main_0010', f)
        p, e = fit([(hp, OBS)], LAST, fix_fov=30.0, w=W)
        print(f, np.round(p, 3).tolist(), np.round(e, 1).tolist())
