"""Deadly Poison and Snakebite (user 2026-11-10: two common cards, each with its own simple look - p5mod/src/Vfx/VenomFx.cs).
Writes art/sfx_p5/venom_drop.ogg and art/sfx_p5/venom_bite.ogg (44.1 kHz stereo).

venom_drop: the drop lands at 0.0 - a wet "tok", a short faint sizzle as it seeps in, one heartbeat (lub-dub) at 0.27 s when the veins pulse.
venom_bite: 0.19 s of hiss rising while the jaws close in (VenomFx.SnapAfter), the snap (two dry cracks 14 ms apart + a thud),
            the hiss letting go, a heavier heartbeat at 0.19 + 0.44 s."""
import subprocess, numpy as np
from pathlib import Path
from scipy import signal

SR = 44100
OUT = Path(__file__).resolve().parents[3] / "art/sfx_p5"
rng = np.random.default_rng(1111)


def t(sec): return np.arange(int(sec * SR)) / SR
def env(x, a, d): return np.minimum(1, np.maximum(x, 0) / max(a, 1e-4)) * np.exp(-np.maximum(0, x - a) / d) * (x >= 0)
def bp(x, lo, hi, order=2): return signal.sosfilt(signal.butter(order, [lo, hi], "band", fs=SR, output="sos"), x)
def lp(x, f, order=2): return signal.sosfilt(signal.butter(order, f, "low", fs=SR, output="sos"), x)
def hp(x, f, order=2): return signal.sosfilt(signal.butter(order, f, "high", fs=SR, output="sos"), x)
def noise(n): return rng.standard_normal(n)


def reverb(x, decay=0.5, mix=0.14):
    n = int(decay * SR)
    ir = lp(noise(n) * np.exp(-np.arange(n) / (decay * SR / 5)), 5000)
    wet = signal.fftconvolve(x, ir)[: len(x) + n]
    wet /= np.abs(wet).max() + 1e-9
    y = np.zeros(len(x) + n); y[: len(x)] += x * (1 - mix); y[: len(wet)] += wet[: len(y)] * mix * np.abs(x).max()
    return y


def save(name, x):
    x = x / (np.abs(x).max() + 1e-9) * 0.95
    d = int(0.002 * SR)
    st = np.stack([x, np.concatenate([np.zeros(d), x[:-d]])], 1)
    raw = (np.clip(st, -1, 1) * 32767).astype(np.int16).tobytes()
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "s16le", "-ar", str(SR), "-ac", "2", "-i", "-", "-c:a", "libvorbis", "-q:a", "6",
                    str(OUT / f"{name}.ogg")], input=raw, check=True)
    print(name, round(len(x) / SR, 2))


def heartbeat(x, at, gain):
    y = np.zeros_like(x)
    for d, g, f in [(0.0, 1.0, 58), (0.13, 0.7, 50)]:
        tt = x - at - d
        y += np.sin(2 * np.pi * (f * tt + 18 * (1 - np.exp(-np.maximum(tt, 0) / 0.02)) * 0.02)) * env(tt, 0.006, 0.055) * g
        y += lp(noise(len(x)), 180) * env(tt, 0.004, 0.03) * g * 0.5
    return y * gain


# ── venom_drop
x = t(0.85)
tok = np.sin(2 * np.pi * np.cumsum(260 + 760 * np.exp(-x / 0.012)) / SR) * env(x, 0.001, 0.028)       # the wet blip, pitch falling
tok += bp(noise(len(x)), 900, 3600) * env(x, 0.0005, 0.007) * 0.8                                      # contact click
tok += np.sin(2 * np.pi * np.cumsum(500 + 900 * np.clip((x - 0.035) / 0.03, 0, 1)) / SR) * env(x - 0.035, 0.003, 0.016) * 0.3   # a small bubble after it
seep = bp(noise(len(x)), 3500, 8000) * env(x - 0.01, 0.02, 0.13) * 0.16 * (0.6 + 0.4 * np.sin(2 * np.pi * 37 * x))
low = np.sin(2 * np.pi * 85 * x) * env(x, 0.002, 0.05) * 0.5
save("venom_drop", reverb(tok + seep + low + heartbeat(x, 0.27, 0.75)))

# ── venom_bite
S = 0.19
x = t(1.25)
k = np.clip(x / S, 0, 1)
hiss_in = bp(noise(len(x)), 3800, 9500) * k ** 2.2 * (x < S) * 0.4 * (0.75 + 0.25 * np.sin(2 * np.pi * 46 * x))
y = hiss_in.copy()
for d, g in [(0.0, 1.0), (0.014, 0.75)]:                                                             # two rows meeting
    tt = x - S - d
    y += bp(noise(len(x)), 1400, 6500) * env(tt, 0.0003, 0.006) * 2.4 * g
    y += sum(a * np.sin(2 * np.pi * f * tt) for f, a in [(1850, 1.0), (2940, 0.6), (4300, 0.35)]) * env(tt, 0.0005, 0.02) * 0.5 * g
tt = x - S
y += np.tanh(4 * bp(noise(len(x)), 300, 1500) * env(tt, 0.001, 0.035)) * 0.9                              # crunch
y += np.sin(2 * np.pi * np.cumsum(60 + 70 * np.exp(-np.maximum(tt, 0) / 0.03)) / SR) * env(tt, 0.002, 0.09) * 1.3   # thud
y += bp(noise(len(x)), 4200, 9000) * env(tt - 0.02, 0.015, 0.2) * 0.3 * (0.7 + 0.3 * np.sin(2 * np.pi * 31 * x))   # hiss letting go
save("venom_bite", reverb(y + heartbeat(x, S + 0.44, 1.0), 0.6, 0.16))
