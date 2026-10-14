"""总攻击背景水波的半径表（AllOut.RippleTab）生成器。

环带弹簧模型（v0.14.16 第十轮，用户认可的节奏）：每圈边界是质点（初速 V0 冲出），相邻两圈之间的环带是只抗压缩的弹簧
（拉伸只给 8% 的回拉），阻尼 2.4/s，速度不低于 25px/s。240Hz 积分后做 σ=0.09s 的双向高斯平滑（用户：展开速率偏僵硬 ——
去掉起速 / 急刹 / 推挤时的折角，节奏和挤压不变），按 60Hz 采样，输出 C# 表（720p 像素，游戏里按 H/720 缩放）。

用法：python3 tools/vfx/allout_ripple.py > /tmp/tab.cs，把输出替换 mod/src/Vfx/AllOutRipple.cs。
"""
import numpy as np

# (出生时刻 s, 初速 px/s, 和外面那圈之间环带的静止宽度 px) —— 外 → 内，颜色在 AllOut.cs 里
SPAWNS = [(0.0, 5200, 0), (0.06, 3600, 430), (0.12, 2700, 400),      # 第一滴：深 · 浅 · 深
          (0.36, 3800, 340), (0.42, 3000, 320), (0.48, 2300, 300)]   # 第二滴：浅 · 深 · 浅
K, DAMP, DRIFT, T, SIGMA, FPS = 60.0, 2.4, 25.0, 3.6, 0.09, 60


def build():
    dt = 1 / 240
    n = len(SPAWNS)
    r, v, alive, hist = [0.0] * n, [0.0] * n, [False] * n, []
    for s in range(int(T / dt)):
        t = s * dt
        for i in range(n):
            if not alive[i] and SPAWNS[i][0] <= t + 1e-9:
                alive[i], v[i] = True, SPAWNS[i][1]
        a = [-DAMP * v[i] for i in range(n)]
        for i in range(n - 1):
            if not alive[i + 1]:
                break
            f = K * (SPAWNS[i + 1][2] - (r[i] - r[i + 1]))
            if f < 0:
                f *= 0.08
            a[i] += f
            a[i + 1] -= f
        for i in range(n):
            if alive[i]:
                v[i] = max(v[i] + a[i] * dt, DRIFT)
                r[i] += v[i] * dt
        for i in range(n - 1):
            if alive[i + 1] and r[i + 1] > r[i] - 2:
                r[i + 1] = r[i] - 2
        hist.append([r[i] if alive[i] else 0.0 for i in range(n)])
    h = np.array(hist)
    sig = SIGMA / dt
    ks = int(4 * sig)
    x = np.arange(-ks, ks + 1)
    g = np.exp(-x ** 2 / (2 * sig ** 2))
    g /= g.sum()
    slope = (h[-1] - h[-41]) / (40 * dt)
    pad = np.vstack([np.zeros((ks, n)), h, h[-1] + np.outer(np.arange(1, ks + 1) * dt, slope)])
    smooth = np.array([np.convolve(pad[:, i], g, 'valid') for i in range(n)]).T
    return smooth[:: int(round(1 / dt / FPS))]


def main():
    tab = build()
    rows = ",\n".join("\t\t{ " + ", ".join(f"{x:.1f}f" for x in row) + " }" for row in tab)
    print("// 由 tools/vfx/allout_ripple.py 生成，不要手改。")
    print("namespace P3Makoto.Vfx;\n")
    print("public static partial class AllOut\n{")
    print(f"\t/// <summary>水波半径表：{FPS}Hz × {tab.shape[1]} 圈（外 → 内），720p 像素；未出生 = 0。</summary>")
    print(f"\tprivate const float RippleFps = {FPS}f;\n")
    print("\tprivate static readonly float[,] RippleTab =\n\t{\n" + rows + "\n\t};\n}")


if __name__ == "__main__":
    main()
