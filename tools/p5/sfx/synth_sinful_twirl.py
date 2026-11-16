"""Satanael's gun twirl, third version (user 2026-11-09: the earlier ones were too heavy, odd and too deliberate - a twirl
should be crisp, clean and simple, and only just noticeable).
Writes art/sfx_p5/sinful_twirl.ogg, played from the first frame of the spin (code volume is low too).

One short, bright "fft" each half turn (a thin band of air, ~25 ms, no body, nothing tonal), timed from the spin curve the
code uses, getting softer and a little lower as the spin slows; a tiny dry "tk" when the gun lands in the hand.
  angle = 720 * (1 - (1 - k)^3), k = t / SpinTime (p5mod SatanaelPuppet.SpinTime = 0.45)
"""
import subprocess
from pathlib import Path

import numpy as np
from scipy import signal

SR = 44100
OUT = Path(__file__).resolve().parents[3] / "art/sfx_p5"
rng = np.random.default_rng(2612)
SPIN = 0.45


def bp(x, lo, hi, order=2): return signal.sosfilt(signal.butter(order, [lo, hi], "band", fs=SR, output="sos"), x)


def save(name, x):
    x = x / (np.abs(x).max() + 1e-9) * 0.8
    d = int(0.0015 * SR)
    st = np.stack([x, np.concatenate([np.zeros(d), x[:-d]])], 1)
    raw = (np.clip(st, -1, 1) * 32767).astype(np.int16).tobytes()
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "s16le", "-ar", str(SR), "-ac", "2", "-i", "-", "-c:a", "libvorbis", "-q:a", "6",
                    str(OUT / f"{name}.ogg")], input=raw, check=True)
    print(name, round(len(x) / SR, 2))


n = int((SPIN + 0.12) * SR)
x = np.arange(n) / SR
y = np.zeros(n)
# half-turn passes: angle = 180 * j  ->  k = 1 - (1 - a / 720)^(1/3)
for j in range(1, 4):
    at = SPIN * (1 - (1 - 180 * j / 720) ** (1 / 3))
    v = 1 - j / 4                                   # spin speed falls off
    d = x - at
    env = np.where(d < 0, np.exp(-(d / 0.012) ** 2), np.exp(-(d / 0.009) ** 2))
    lo = 2600 + 1800 * v
    y += bp(rng.standard_normal(n), lo, lo * 1.9) * env * (0.45 + 0.55 * v)
# the catch: a tiny dry click (two very short ticks)
for off, g in ((0.0, 1.0), (0.006, 0.45)):
    m = x >= SPIN + off
    tt = x[m] - SPIN - off
    y[m] += bp(rng.standard_normal(m.sum()), 1800, 5000) * np.exp(-tt / 0.0035) * 0.55 * g
save("sinful_twirl", y)
