"""Serpent Form (p5mod/src/Vfx/SerpentMantle.cs): the bite when a serpent's maw snaps shut on the enemy.
2026-11-11 v2 (user: the attack sound is not pleasant -> a light sound with a sense of confirmation): a short soft tick 10 ms in,
a small muffled "tok" under it and a faint puff of smoke - no hard snap, no deep thump. Played 0.45 s after the attack starts,
so the tick lands on the bite frame. Low-passed at 6.5 kHz like the other skill sounds.

serpent_rise (2026-11-11, user: the intro sound is not pleasant; redone for the new intro storyboard, t = 0 when the intro starts):
a soft inhaling whoosh as the screen darkens (0-0.35 s), a low swell under the mist (0.1-1.4 s), a faint puff of smoke as each of the
five serpents comes out of the nest (0.4 s + 0.11 s each), a soft low push when the first huge back surfaces (0.9 s) and a light
settling tick-and-tok when the eyes light up (1.1 s). No hiss, no pure tones or glides.
Writes art/sfx_p5/serpent_maw.ogg and art/sfx_p5/serpent_rise.ogg. This script only writes these two files."""
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



x = t(0.32)
BITE = 0.01
y = at(x, BITE)
tick = bp(nz(x), 1800, 4200) * env(y, 0.0006, 0.005) * on(x, BITE) * 0.9
tok = np.sin(2 * np.pi * (175 * y - 60 * y ** 2)) * env(y, 0.0015, 0.04) * on(x, BITE) * 0.9
tok_n = bp(nz(x), 250, 900) * env(y, 0.001, 0.03) * on(x, BITE) * 0.5
puff = bp(nz(x), 400, 1600) * env(at(x, BITE + 0.01), 0.012, 0.08) * on(x, BITE + 0.01) * 0.22
save("serpent_maw", room(tick + tok + tok_n + puff, 0.35, 0.1), peak=0.6)


# ── serpent_rise: the intro ──
x = t(2.0)
def seg_env(x, t0, a, d): return env(at(x, t0), a, d) * on(x, t0)
# inhale: a band of air closing downward, swelling into 0.3 s, gone by 0.5 s
inhale = sweep_noise(x, 2200, 500, 0.4, 0.5) * np.minimum(1, x / 0.3) ** 2 * np.exp(-np.maximum(0, x - 0.3) / 0.08)
# low swell under the mist
swell = np.minimum(1, at(x, 0.1) / 0.45) * np.exp(-np.maximum(0, x - 0.75) / 0.4) * on(x, 0.1)
low = lp(nz(x), 160, 3) * swell * 1.4 + bp(nz(x), 180, 520) * swell * 0.35
# five puffs of smoke as the serpents come out
puffs = np.zeros_like(x)
for i in range(5):
    t0 = 0.4 + 0.11 * i
    lo, hi = 450 + 60 * i, 1700 + 120 * i
    puffs += bp(nz(x), lo, hi) * seg_env(x, t0, 0.015, 0.09) * (0.26 - 0.02 * i)
# the first huge back surfacing: a soft low push
push = lp(nz(x), 110, 3) * seg_env(x, 0.88, 0.04, 0.2) * 1.4
# eyes light up: the same light confirming family as the bite, softer
y1 = at(x, 1.1)
tick = bp(nz(x), 1600, 3800) * env(y1, 0.0006, 0.005) * on(x, 1.1) * 0.45
tok = np.sin(2 * np.pi * (150 * y1 - 50 * y1 ** 2)) * env(y1, 0.002, 0.05) * on(x, 1.1) * 0.55
save("serpent_rise", room(inhale * 0.5 + low + puffs + push + tick + tok, 0.6, 0.18), peak=0.7)
