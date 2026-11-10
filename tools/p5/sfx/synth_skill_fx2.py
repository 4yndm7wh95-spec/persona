"""Second pass of four skill-effect sounds (user 2026-11-10: the first ones sounded cartoonish - they were pitch sweeps and pure tones).
These are made only from filtered noise bursts and very short damped resonances: no gliding pitch, no sustained tone.
Writes art/sfx_p5/: card_flick (draw), bullet_load (a round seating in the cylinder), expose_break (armor cracked open), wail (pressure wave from Joker).
This script only writes these four files."""
import subprocess
from pathlib import Path
import numpy as np
from scipy import signal

SR = 44100
OUT = Path(__file__).resolve().parents[3] / "art/sfx_p5"
rng = np.random.default_rng(1112)


def t(sec): return np.arange(int(sec * SR)) / SR
def env(x, a, d): return np.minimum(1, np.maximum(x, 0) / max(a, 1e-5)) * np.exp(-np.maximum(0, x - a) / d) * (x >= 0)
def bp(x, lo, hi, order=2): return signal.sosfilt(signal.butter(order, [lo, hi], "band", fs=SR, output="sos"), x)
def lp(x, f, order=2): return signal.sosfilt(signal.butter(order, f, "low", fs=SR, output="sos"), x)
def hp(x, f, order=2): return signal.sosfilt(signal.butter(order, f, "high", fs=SR, output="sos"), x)
def nz(n): return rng.standard_normal(n)


def room(x, decay=0.4, mix=0.12):
    n = int(decay * SR)
    ir = lp(nz(n) * np.exp(-np.arange(n) / (decay * SR / 5)), 5000)
    wet = signal.fftconvolve(x, ir); wet /= np.abs(wet).max() + 1e-9
    y = np.zeros(len(x) + n); y[:len(x)] += x * (1 - mix); y[:len(wet)] += wet[:len(y)] * mix * np.abs(x).max()
    return y


def save(name, x, peak=0.85):
    x = x / (np.abs(x).max() + 1e-9) * peak
    fade = int(0.01 * SR); x[-fade:] *= np.linspace(1, 0, fade)
    d = int(0.0012 * SR)
    st = np.stack([x, np.concatenate([np.zeros(d), x[:-d]])], 1)
    raw = (np.clip(st, -1, 1) * 32767).astype(np.int16).tobytes()
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "s16le", "-ar", str(SR), "-ac", "2", "-i", "-", "-c:a", "libvorbis", "-q:a", "6",
                    str(OUT / f"{name}.ogg")], input=raw, check=True)
    print(name, round(len(x) / SR, 2))


# ── card_flick: a stiff card snapped off the deck - one dry paper snap and a short flutter
x = t(0.14); y = np.zeros_like(x)
y += bp(nz(len(x)), 1800, 7500) * env(x, 0.0004, 0.008)
for d, g in [(0.011, 0.45), (0.02, 0.3), (0.031, 0.18)]:
    y += bp(nz(len(x)), 2200, 6500) * env(x - d, 0.0004, 0.004) * g
y += bp(nz(len(x)), 300, 900) * env(x, 0.001, 0.012) * 0.35
save("card_flick", y, 0.75)

# ── bullet_load: a round pushed into a chamber - a light tick, then the seat: a short clack with a very short metal ring and a dull thunk
x = t(0.16); y = np.zeros_like(x)
y += hp(nz(len(x)), 2500) * env(x, 0.0003, 0.0018) * 0.6
s0 = x - 0.014
y += bp(nz(len(x)), 900, 5200) * env(s0, 0.0003, 0.005)
y += sum(a * np.sin(2 * np.pi * f * np.maximum(s0, 0) + ph) for f, a, ph in [(2870, 1.0, 0.3), (4310, 0.7, 1.1), (5930, 0.45, 2.0)]) * env(s0, 0.0004, 0.007) * 0.28
y += bp(nz(len(x)), 150, 420) * env(s0, 0.001, 0.014) * 0.6
save("bullet_load", y, 0.8)

# ── expose_break: armor split open - two hard dry cracks, a mid crunch, then debris ticking away
x = t(0.65); y = np.zeros_like(x)
for d, g in [(0.0, 1.0), (0.022, 0.7)]:
    y += hp(nz(len(x)), 1200) * env(x - d, 0.0003, 0.009) * 1.6 * g
y += np.tanh(4 * bp(nz(len(x)), 400, 2000) * env(x, 0.001, 0.05)) * 0.8
y += lp(nz(len(x)), 160) * env(x, 0.002, 0.07) * 1.2
for _ in range(26):
    d = rng.uniform(0.05, 0.5)
    y += bp(nz(len(x)), 2500, 6500) * env(x - d, 0.0003, 0.003) * rng.uniform(0.12, 0.4) * np.exp(-d / 0.22)
save("expose_break", room(y, 0.35, 0.1), 0.85)

# ── wail: a pressure wave thrown off Joker - a hard burst of air, a hollow howl of filtered noise that flutters as the fronts go out, a low thump
x = t(0.95); y = np.zeros_like(x)
y += bp(nz(len(x)), 500, 4500) * env(x, 0.004, 0.11) * 1.0
flutter = 0.65 + 0.35 * np.sign(np.sin(2 * np.pi * 26 * x)) * np.exp(-x / 0.5)
y += (bp(nz(len(x)), 750, 1150, 3) * 1.4 + bp(nz(len(x)), 2300, 3000, 3) * 0.7) * env(x, 0.02, 0.4) * flutter
y += lp(nz(len(x)), 120, 3) * env(x, 0.004, 0.16) * 2.4
y += hp(nz(len(x)), 3500) * env(x - 0.01, 0.03, 0.3) * 0.16
save("wail", room(y, 0.7, 0.2), 0.85)
