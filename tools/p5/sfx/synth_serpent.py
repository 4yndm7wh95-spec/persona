"""Serpent Form (p5mod/src/Vfx/SerpentMantle.cs): the bite when a serpent's maw snaps shut on the enemy. Only the bite (user
2026-11-11: no sound for the maw rushing out): a hard snap 10 ms in, a low thump and a short dark tail. Played 0.45 s after the attack
starts, so the snap lands on the bite frame. Low-passed at 6.5 kHz like the other skill sounds.
Writes art/sfx_p5/serpent_maw.ogg. This script only writes this file."""
# helpers copied from synth_skill_fx.py (importing it would rerun it and rewrite its files)
import subprocess
from pathlib import Path

import numpy as np
from scipy import signal

SR = 44100
OUT = Path(__file__).resolve().parents[3] / "art/sfx_p5"
rng = np.random.default_rng(1111)


def t(sec): return np.arange(int(sec * SR)) / SR
def env(x, a, d): return np.minimum(1, x / max(a, 1e-4)) * np.exp(-np.maximum(0, x - a) / d)
def bp(x, lo, hi, order=2): return signal.sosfilt(signal.butter(order, [lo, hi], "band", fs=SR, output="sos"), x)
def lp(x, f, order=2): return signal.sosfilt(signal.butter(order, f, "low", fs=SR, output="sos"), x)
def hp(x, f, order=2): return signal.sosfilt(signal.butter(order, f, "high", fs=SR, output="sos"), x)
def nz(x): return rng.standard_normal(len(x))
def at(x, t0): return np.maximum(x - t0, 0.0)
def on(x, t0): return (x >= t0).astype(float)
def chirp(x, f0, f1, dur):
    k = np.clip(x / dur, 0, 1)
    f = f0 * (f1 / f0) ** k
    return np.sin(2 * np.pi * np.cumsum(f) / SR), f


def sweep_noise(x, f0, f1, dur, q=0.4):
    out = np.zeros_like(x); n = nz(x); zi = None
    for i in range(0, len(x), 256):
        k = min(1.0, x[i] / dur); fc = f0 * (f1 / f0) ** k
        sos = signal.butter(2, [fc * (1 - q), min(fc * (1 + q), SR / 2 - 200)], "band", fs=SR, output="sos")
        if zi is None: zi = np.zeros((sos.shape[0], 2))
        out[i:i + 256], zi = signal.sosfilt(sos, n[i:i + 256], zi=zi)
    return out


def room(x, decay=0.5, mix=0.14):
    n = int(decay * SR)
    ir = lp(rng.standard_normal(n) * np.exp(-np.arange(n) / (decay * SR / 5)), 4500)
    wet = signal.fftconvolve(x, ir); wet /= np.abs(wet).max() + 1e-9
    y = np.zeros(len(x) + n); y[:len(x)] += x * (1 - mix); y[:len(wet)] += wet[:len(y)] * mix * np.abs(x).max()
    return y


def save(name, x, peak=0.8, width=0.0015):
    x = lp(x, 6500, 4)                                    # nothing harsh
    x = x / (np.abs(x).max() + 1e-9) * peak
    fade = int(0.012 * SR); x[-fade:] *= np.linspace(1, 0, fade)
    d = int(width * SR)
    st = np.stack([x, np.concatenate([np.zeros(d), x[:-d]]) if d else x], 1)
    raw = (np.clip(st, -1, 1) * 32767).astype(np.int16).tobytes()
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "s16le", "-ar", str(SR), "-ac", "2", "-i", "-", "-c:a", "libvorbis", "-q:a", "6",
                    str(OUT / f"{name}.ogg")], input=raw, check=True)
    print(name, round(len(x) / SR, 2))



x = t(0.5)
BITE = 0.01
y = at(x, BITE)
snap = hp(nz(x), 1400) * env(y, 0.0008, 0.014) * on(x, BITE) * 1.6
click = bp(nz(x), 2500, 5500) * env(at(x, BITE + 0.012), 0.0005, 0.008) * on(x, BITE + 0.012) * 0.8
thump = np.sin(2 * np.pi * (85 * y - 30 * y ** 2)) * env(y, 0.002, 0.12) * on(x, BITE) * 1.6
body = lp(nz(x), 800) * env(y, 0.003, 0.07) * on(x, BITE) * 0.8
save("serpent_maw", room(snap + click + thump + body, 0.4, 0.12), peak=0.85)
