"""v0.14.70 设置界面的水声（程序合成，不用任何外部录音）：
· sys_dive.ogg  —— 打开设置：0.00s 入水"噗"（低通噪声爆发 + 往下扫的低频），0.05~0.35s 水往下冲的嘶声，0.08~0.9s 一串上浮气泡的"啵"（上扬的正弦小啁啾）；
· sys_blob.ogg  —— 切分页：深蓝水团涌出的闷响 + 两三个气泡。
用法：python3 tools/audio/synth_water.py   （输出到 mod/pck_stage/p3makoto/sfx/）"""
import subprocess
import sys
import tempfile
import wave
from pathlib import Path

import numpy as np
from scipy import signal

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "mod/pck_stage/p3makoto/sfx"
SR = 44100
rng = np.random.default_rng(7)


def env(n, a, d):
    t = np.arange(n) / SR
    return np.minimum(1, t / max(a, 1e-4)) * np.exp(-t / d)


def lowpass_sweep(x, f0, f1):
    # 分段低通，截止频率从 f0 滑到 f1
    out = np.zeros_like(x)
    seg = 1024
    zi = None
    for i in range(0, len(x), seg):
        f = f0 + (f1 - f0) * min(1, i / len(x))
        b, a = signal.butter(2, min(f, SR / 2 - 100) / (SR / 2))
        if zi is None or len(zi) != max(len(a), len(b)) - 1:
            zi = signal.lfilter_zi(b, a) * 0
        out[i:i + seg], zi = signal.lfilter(b, a, x[i:i + seg], zi=zi)
    return out


def bubble(f0, dur, amp):
    n = int(dur * SR)
    t = np.arange(n) / SR
    f = f0 * (1 + 1.8 * t / dur)                 # 气泡：频率往上扬
    ph = 2 * np.pi * np.cumsum(f) / SR
    return amp * np.sin(ph) * env(n, 0.002, dur * 0.35)


def place(buf, x, at):
    i = int(at * SR)
    j = min(len(buf), i + len(x))
    buf[i:j] += x[:j - i]


def dive():
    buf = np.zeros(int(1.2 * SR))
    n = int(0.5 * SR)
    plop = lowpass_sweep(rng.standard_normal(n), 2400, 300) * env(n, 0.004, 0.09) * 0.9
    t = np.arange(n) / SR
    thump = np.sin(2 * np.pi * np.cumsum(140 * np.exp(-t * 7) + 45) / SR) * env(n, 0.003, 0.12) * 0.7
    place(buf, plop + thump, 0.0)
    n = int(0.45 * SR)
    hiss = signal.lfilter(*signal.butter(2, [900 / (SR / 2), 5200 / (SR / 2)], "band"), rng.standard_normal(n))
    hiss *= np.sin(np.pi * np.clip(np.arange(n) / n, 0, 1)) ** 1.5 * 0.35
    place(buf, hiss, 0.05)
    for k in range(16):
        at = 0.08 + 0.8 * (k / 16) ** 1.3 + rng.uniform(-0.02, 0.02)
        place(buf, bubble(rng.uniform(380, 900), rng.uniform(0.03, 0.07), rng.uniform(0.12, 0.3) * (1 - k / 22)), at)
    return buf


def blob():
    buf = np.zeros(int(0.7 * SR))
    n = int(0.4 * SR)
    t = np.arange(n) / SR
    whoom = lowpass_sweep(rng.standard_normal(n), 300, 1600) * np.sin(np.pi * np.clip(t / 0.4, 0, 1)) * 0.55
    body = np.sin(2 * np.pi * np.cumsum(70 + 60 * t / 0.4) / SR) * env(n, 0.03, 0.15) * 0.5
    place(buf, whoom + body, 0.0)
    for at, f in ((0.16, 520), (0.24, 680), (0.33, 610)):
        place(buf, bubble(f, 0.05, 0.22), at)
    return buf


def save(name, x):
    x = x / max(1e-6, np.abs(x).max()) * 0.85
    fade = int(0.02 * SR)
    x[-fade:] *= np.linspace(1, 0, fade)
    with tempfile.TemporaryDirectory() as d:
        w = Path(d) / "a.wav"
        with wave.open(str(w), "wb") as f:
            f.setnchannels(1)
            f.setsampwidth(2)
            f.setframerate(SR)
            f.writeframes((x * 32767).astype(np.int16).tobytes())
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", str(w), "-c:a", "libvorbis", "-q:a", "5", str(OUT / name)], check=True)
    print(name, f"{len(x) / SR:.2f}s")


if __name__ == "__main__":
    save("sys_dive.ogg", dive())
    save("sys_blob.ogg", blob())
    sys.exit(0)
