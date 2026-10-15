"""星星点点（Patter：格挡 + 活力）的音效：金色星光从脚下升起、箭头升到头顶。
替换原来的 Snd_FX_Btl_StAbsorption_00_1（用户：听起来像晕眩）。刻意避开晕眩声的特征 —— 不打转、没有颤音 / 摇摆，
音高只往上走、干净明亮：
  - 0~0.55s：一串越来越快的上行钟音（五声音阶 C6→E7，轻微不谐和泛音，像小星星一颗颗亮起）；
  - 同时一层从 800Hz 扫到 4kHz 的气流上扬（跟光柱、箭头一起升起）；
  - 0.58s：箭头到顶，一声明亮的「叮」+ 短暂的闪烁余韵。
出牌时播放（CardSigs.SigSounds，延迟 0，和 Motifs.Seal 的金色法阵同时开始）。
用法：python3 tools/sfx/patter_sparkle.py <输出目录>
"""
import sys, subprocess, numpy as np
from scipy.signal import butter, sosfilt

SR = 44100
rng = np.random.default_rng(23)


def band(x, lo, hi, order=2):
    return sosfilt(butter(order, [lo, hi], btype="band", fs=SR, output="sos"), x)


def bell(f, dur, decay, gain):
    n = int(SR * dur)
    t = np.arange(n) / SR
    env = np.minimum(t / 0.002, 1) * np.exp(-t / decay)
    x = (np.sin(2 * np.pi * f * t) + 0.35 * np.sin(2 * np.pi * f * 2.76 * t) * np.exp(-t / (decay * 0.4))
         + 0.15 * np.sin(2 * np.pi * f * 5.4 * t) * np.exp(-t / (decay * 0.2)))
    return x * env * gain


def place(buf, clip, at):
    i = int(at * SR)
    end = min(len(buf), i + len(clip))
    buf[i:end] += clip[: end - i]


def sparkle():
    n = int(SR * 1.5)
    t = np.arange(n) / SR
    buf = np.zeros(n)
    notes = [1046.5, 1174.7, 1318.5, 1568.0, 1760.0, 2093.0, 2349.3, 2637.0]
    for i, f in enumerate(notes):
        k = i / (len(notes) - 1)
        at = 0.04 + 0.5 * (1 - (1 - k) ** 1.7)          # 越来越快
        place(buf, bell(f * rng.uniform(0.997, 1.003), 0.6, 0.22, 0.55 + 0.25 * k), at)
    # 气流上扬：中心频率 800→4000Hz
    noise = rng.standard_normal(n)
    steps = 16
    rise = np.zeros(n)
    seg = int(SR * 0.6) / steps
    for s in range(steps):
        fc = 800 * (4000 / 800) ** (s / (steps - 1))
        y = band(noise, fc * 0.75, fc * 1.3)
        w = np.clip(1 - np.abs(np.arange(n) - (s + 0.5) * seg) / seg, 0, 1)
        rise += y * w
    rise *= np.sin(np.pi * np.clip(t / 0.62, 0, 1)) ** 2
    buf += rise * 0.16
    # 到顶：明亮的「叮」+ 几粒闪烁余韵
    place(buf, bell(3136.0, 1.0, 0.45, 1.0), 0.58)
    place(buf, bell(4186.0, 0.8, 0.3, 0.35), 0.585)
    for _ in range(7):
        place(buf, bell(rng.choice([3520.0, 3951.1, 4698.6, 5274.0]), 0.3, 0.08, 0.18), rng.uniform(0.62, 0.95))
    return buf


def write(path, x, peak=0.75):
    x = x / (np.abs(x).max() + 1e-9) * peak
    pcm = (x * 32767).astype(np.int16).tobytes()
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "s16le", "-ar", str(SR), "-ac", "1", "-i", "-",
                    "-c:a", "libvorbis", "-q:a", "6", path], input=pcm, check=True)


if __name__ == "__main__":
    write(f"{sys.argv[1]}/patter_sparkle.ogg", sparkle())
