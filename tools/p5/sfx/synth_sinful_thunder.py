"""Satanael's summon / exit thunder, second version (user 2026-11-09: the old one was too "explosive", harsh and blown out - it should be
the clean, crisp roar of thunder). Writes art/sfx_p5/sinful_thunder.ogg.

Shape of a near lightning strike, kept clean (no distortion, no broadband blast):
  0.00  a crisp crack - a few very short ticks in the 700-5000 Hz band, quickly low-passed so it snaps instead of hissing
  0.02  the main body - a dense low-mid roar (90-600 Hz) that blooms fast and decays over ~0.6 s
  0.15+ rolling rumble - deep (30-160 Hz) noise in three or four uneven swells, each lower and softer, tailing off over ~2 s
A soft compressor evens it out so the roar carries without a spiky peak; a long, dark reverb gives the distance.
"""
import subprocess
from pathlib import Path

import numpy as np
from scipy import signal

SR = 44100
OUT = Path(__file__).resolve().parents[3] / "art/sfx_p5"
rng = np.random.default_rng(3107)


def t(sec): return np.arange(int(sec * SR)) / SR
def lp(x, f, order=2): return signal.sosfilt(signal.butter(order, f, "low", fs=SR, output="sos"), x)
def bp(x, lo, hi, order=2): return signal.sosfilt(signal.butter(order, [lo, hi], "band", fs=SR, output="sos"), x)
def env(x, a, d): return np.minimum(1, x / max(a, 1e-4)) * np.exp(-np.maximum(0, x - a) / d)


def reverb(x, decay, mix):
    n = int(decay * SR)
    ir = rng.standard_normal(n) * np.exp(-np.arange(n) / (decay * SR / 5))
    ir = lp(ir, 2500)
    wet = signal.fftconvolve(x, ir)[: len(x) + n]
    wet /= np.abs(wet).max() + 1e-9
    y = np.zeros(len(x) + n)
    y[: len(x)] += x * (1 - mix)
    y[: len(wet)] += wet[: len(y)] * mix * np.abs(x).max()
    return y


def compress(x, thresh=0.35, ratio=3.0, att=0.004, rel=0.12):
    """Simple feed-forward compressor on the envelope (keeps the roar full without a spiky peak)."""
    a, r = np.exp(-1 / (att * SR)), np.exp(-1 / (rel * SR))
    e = np.zeros_like(x)
    lvl = 0.0
    for i, v in enumerate(np.abs(x)):
        lvl = a * lvl + (1 - a) * v if v > lvl else r * lvl + (1 - r) * v
        e[i] = lvl
    e /= e.max() + 1e-9
    gain = np.where(e > thresh, (thresh + (e - thresh) / ratio) / np.maximum(e, 1e-9), 1.0)
    return x * gain


x = t(2.6)
y = np.zeros_like(x)
# crack: a few close ticks, band-limited, short
for at, amp in ((0.0, 1.0), (0.011, 0.6), (0.026, 0.45), (0.05, 0.25)):
    m = x >= at
    tt = x[m] - at
    y[m] += bp(rng.standard_normal(m.sum()), 700, 5000) * env(tt, 0.0006, 0.012) * amp * 0.55
y = lp(y, 4200)
# body: dense low-mid roar
body = bp(rng.standard_normal(len(x)), 90, 600) * env(x - 0.015, 0.025, 0.45) * (x > 0.015)
y += body * 0.9
# rolling rumble: uneven swells, each lower and softer
rum = np.zeros_like(x)
for at, amp, d in ((0.12, 1.0, 0.5), (0.45, 0.75, 0.6), (0.9, 0.5, 0.7), (1.4, 0.3, 0.8)):
    m = x >= at
    tt = x[m] - at
    rum[m] += env(tt, 0.09, d) * amp
rumble = lp(rng.standard_normal(len(x)), 160, 4) * rum * (1 + 0.35 * np.sin(2 * np.pi * 5.3 * x + 1.0))
y += rumble * 2.6
y *= np.clip((2.6 - x) / 0.4, 0, 1)
y = reverb(compress(y), 1.8, 0.28)
y = y / (np.abs(y).max() + 1e-9) * 0.85
d = int(0.004 * SR)
st = np.stack([y, np.concatenate([np.zeros(d), y[:-d]])], 1)
raw = (np.clip(st, -1, 1) * 32767).astype(np.int16).tobytes()
subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "s16le", "-ar", str(SR), "-ac", "2", "-i", "-", "-c:a", "libvorbis", "-q:a", "6",
                str(OUT / "sinful_thunder.ogg")], input=raw, check=True)
print("sinful_thunder", round(len(y) / SR, 2))
