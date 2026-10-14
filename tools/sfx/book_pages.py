"""预言（ThemeFx.Compendium）的翻书音效合成。
和抽牌声（card_draw：瞬时"咔"+ 1~3k / 6k 以上的脆响）刻意区分：
  - 没有咔哒起音：每页 40~60ms 柔和起音，纸页"唰"声以 500~2000Hz 为主，6kHz 以上滚降；
  - 每页带 20~35Hz 的纸张颤动（翻动时纸页抖），尾部是一点空气声；
  - 开书：厚书皮"嘭"（低频 + 低中频气流），合书：闷响。
时间轴与 ThemeFx.Compendium 对齐：book_riffle 在特效 0.24s 时播放（封面开始翻开），
页 i 的时刻 = 0.4 + 0.62*(1-(1-i/9)^1.6)（同代码），每页翻动 0.16s。book_close 在 1.3s 播放。
用法：python3 tools/sfx/book_pages.py <输出目录>
"""
import sys, subprocess, numpy as np
from scipy.signal import butter, sosfilt

SR = 44100
rng = np.random.default_rng(7)


def band(x, lo, hi, order=2):
    return sosfilt(butter(order, [lo, hi], btype="band", fs=SR, output="sos"), x)


def low(x, f, order=2):
    return sosfilt(butter(order, f, btype="low", fs=SR, output="sos"), x)


def swept_band(n, f0, f1, q=1.2, steps=24):
    """中心频率随时间扫动的带通噪声（分段滤波后交叉淡化）。"""
    noise = rng.standard_normal(n + 2000)
    out = np.zeros(n)
    seg = n / steps
    for k in range(steps):
        fc = f0 * (f1 / f0) ** (k / max(1, steps - 1))
        y = band(noise, fc / (1 + 1 / (2 * q)), fc * (1 + 1 / (2 * q)))[2000:]
        w = np.clip(1 - np.abs(np.arange(n) - (k + 0.5) * seg) / seg, 0, 1)
        out += y * w
    return out


def page(dur=0.17, f0=650, f1=1900, gain=1.0, flutter=28):
    n = int(SR * dur)
    t = np.arange(n) / SR
    env = np.minimum(t / 0.05, 1) ** 1.5 * np.exp(-np.maximum(t - 0.05, 0) / 0.05)
    flut = 1 - 0.45 * (0.5 + 0.5 * np.sin(2 * np.pi * flutter * t + rng.uniform(0, 6))) * np.exp(-t / 0.1)
    body = swept_band(n, f0, f1) * env * flut
    air = band(rng.standard_normal(n), 220, 600) * env * 0.3
    sheen = band(rng.standard_normal(n), 3000, 5200) * env ** 2 * 0.22   # 纸面摩擦的一点亮度（不做成咔哒）
    return gain * (body + air + sheen)


def place(buf, clip, at):
    i = max(0, int(at * SR))
    end = min(len(buf), i + len(clip))
    buf[i:end] += clip[: end - i]


def riffle():
    start = 0.24
    buf = np.zeros(int(SR * 1.3))
    # 开书：厚书皮翻开（0.24~0.46s）——低频气流涌起 + 结束处一记软"嘭"
    n = int(SR * 0.3); t = np.arange(n) / SR
    whoosh = band(rng.standard_normal(n), 150, 700) * np.sin(np.pi * np.clip(t / 0.24, 0, 1)) ** 2
    place(buf, whoosh * 0.6, 0.0)
    n = int(SR * 0.25); t = np.arange(n) / SR
    thump = np.sin(2 * np.pi * (95 + 40 * np.exp(-t / 0.02)) * t) * np.exp(-t / 0.06) * np.minimum(t / 0.004, 1)
    thump += low(rng.standard_normal(n), 900) * np.exp(-t / 0.025) * np.minimum(t / 0.006, 1) * 0.5
    place(buf, thump * 0.5, 0.20)
    # 9 页：越翻越快、越翻越轻快（音高略升、音量略降）
    for i in range(9):
        t0 = 0.4 + 0.62 * (1 - (1 - i / 9) ** 1.6)
        k = i / 8
        clip = page(dur=0.17 - 0.03 * k, f0=800 * (1 + 0.25 * k) * rng.uniform(0.92, 1.08),
                    f1=2600 * (1 + 0.2 * k) * rng.uniform(0.92, 1.08), gain=(1.0 - 0.35 * k) * rng.uniform(0.85, 1.0),
                    flutter=rng.uniform(22, 34))
        place(buf, clip, t0 - start - 0.03)
    # 翻页期间底下一层很轻的连续纸页沙沙（让快速连翻连成一片）
    n = len(buf); t = np.arange(n) / SR
    bed_env = np.clip((t - 0.25) / 0.3, 0, 1) * np.clip((1.12 - t) / 0.2, 0, 1)
    buf += band(rng.standard_normal(n), 700, 2600) * bed_env * 0.12
    return low(buf, 7000, 4)


def close():
    n = int(SR * 0.45); t = np.arange(n) / SR
    clap = np.sin(2 * np.pi * (80 + 50 * np.exp(-t / 0.015)) * t) * np.exp(-t / 0.07) * np.minimum(t / 0.003, 1)
    slap = band(rng.standard_normal(n), 300, 1800) * np.exp(-t / 0.03) * np.minimum(t / 0.004, 1) * 1.0
    m = int(SR * 0.06)
    pre = band(rng.standard_normal(m), 400, 1600) * np.linspace(0, 1, m) ** 2 * 0.4   # 合上前一瞬的气流
    out = np.zeros(n + m); out[:m] += pre; out[m:] += clap + slap
    return low(out, 5000, 4)


def write(path, x, peak=0.8):
    x = x / (np.abs(x).max() + 1e-9) * peak
    pcm = (x * 32767).astype(np.int16).tobytes()
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "s16le", "-ar", str(SR), "-ac", "1", "-i", "-",
                    "-c:a", "libvorbis", "-q:a", "6", path], input=pcm, check=True)


if __name__ == "__main__":
    out = sys.argv[1]
    write(f"{out}/book_riffle.ogg", riffle())
    write(f"{out}/book_close.ogg", close(), peak=0.7)
