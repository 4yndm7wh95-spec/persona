"""Per-card skill sounds, batch 3 (p5mod/src/Vfx/CardSkillFx3.cs, user 2026-11-10). Restrained (you can tell it is there, no more),
noise only - no pure tones, no pitch glides; only the two Forms (rare powers) get some weight.
Writes art/sfx_p5/: retain_lock, smoke_puff, card_cut, gamble_spin, heartbeat. This script only writes these five files."""
import subprocess
from pathlib import Path
import numpy as np
from scipy import signal

SR = 44100
OUT = Path(__file__).resolve().parents[3] / "art/sfx_p5"
rng = np.random.default_rng(1116)


def t(sec): return np.arange(int(sec * SR)) / SR
def env(x, a, d): return np.minimum(1, np.maximum(x, 0) / max(a, 1e-5)) * np.exp(-np.maximum(0, x - a) / d) * (x >= 0)
def bp(x, lo, hi, order=2): return signal.sosfilt(signal.butter(order, [lo, hi], "band", fs=SR, output="sos"), x)
def lp(x, f, order=2): return signal.sosfilt(signal.butter(order, f, "low", fs=SR, output="sos"), x)
def hp(x, f, order=2): return signal.sosfilt(signal.butter(order, f, "high", fs=SR, output="sos"), x)
def nz(n): return rng.standard_normal(n)


def moving_band(x, f0, f1, dur, q=0.5, t0=0.0):
    out = np.zeros_like(x); n = nz(len(x)); zi = None
    for i in range(0, len(x), 256):
        k = min(1.0, max(0.0, x[i] - t0) / dur); fc = f0 * (f1 / f0) ** k
        sos = signal.butter(2, [fc * (1 - q), min(fc * (1 + q), SR / 2 - 200)], "band", fs=SR, output="sos")
        if zi is None: zi = np.zeros((sos.shape[0], 2))
        out[i:i + 256], zi = signal.sosfilt(sos, n[i:i + 256], zi=zi)
    return out


def room(x, decay=0.4, mix=0.12):
    n = int(decay * SR)
    ir = lp(nz(n) * np.exp(-np.arange(n) / (decay * SR / 5)), 4500)
    wet = signal.fftconvolve(x, ir); wet /= np.abs(wet).max() + 1e-9
    y = np.zeros(len(x) + n); y[:len(x)] += x * (1 - mix); y[:len(wet)] += wet[:len(y)] * mix * np.abs(x).max()
    return y


def save(name, x, peak=0.7):
    x = lp(x, 7000, 4)
    x = x / (np.abs(x).max() + 1e-9) * peak
    fade = int(0.015 * SR); x[-fade:] *= np.linspace(1, 0, fade)
    d = int(0.0012 * SR)
    st = np.stack([x, np.concatenate([np.zeros(d), x[:-d]])], 1)
    raw = (np.clip(st, -1, 1) * 32767).astype(np.int16).tobytes()
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "s16le", "-ar", str(SR), "-ac", "2", "-i", "-", "-c:a", "libvorbis", "-q:a", "6",
                    str(OUT / f"{name}.ogg")], input=raw, check=True)
    print(name, round(len(x) / SR, 2))


def step(x, t0, g=1.0):
    """a light foot tap: a short scuff of grit and a soft low knock"""
    return (bp(nz(len(x)), 1500, 5000) * env(x - t0, 0.0006, 0.012) * 0.6 + lp(nz(len(x)), 220) * env(x - t0, 0.001, 0.025) * 1.4) * g



def swish(x, t0, f0, f1, dur, g=1.0, q=0.45):
    return moving_band(x, f0, f1, dur, q, t0) * env(x - t0, dur * 0.3, dur * 0.5) * g


def thud(x, t0, g=1.0, d=0.08):
    return lp(nz(len(x)), 150, 3) * env(x - t0, 0.003, d) * 2.0 * g


# retain_lock: one very small dry click with a soft body
x = t(0.2)
y = hp(nz(len(x)), 2500) * env(x, 0.0003, 0.003) * 0.7 + bp(nz(len(x)), 600, 1800) * env(x, 0.0008, 0.012) * 0.5
save("retain_lock", room(y, 0.2, 0.08), 0.55)

# smoke_puff: a soft "pff" - a burst of low-mid air, no bang
x = t(0.7)
y = bp(nz(len(x)), 300, 2200) * env(x, 0.006, 0.12) * 1.0 + thud(x, 0.0, 0.5, 0.05)
y += bp(nz(len(x)), 800, 3000) * np.minimum(1, x / 0.05) * np.exp(-x / 0.25) * 0.25
save("smoke_puff", room(y, 0.35, 0.12), 0.6)

# card_cut: a fast thin slice and the paper parting
x = t(0.5)
y = swish(x, 0.0, 2500, 6000, 0.06, 0.6, 0.35) + hp(nz(len(x)), 3000) * env(x - 0.03, 0.0005, 0.012) * 0.9
y += bp(nz(len(x)), 1500, 4500) * env(x - 0.04, 0.002, 0.05) * 0.3
save("card_cut", room(y, 0.25, 0.1), 0.6)

# gamble_spin: ticks of a wheel slowing down over 0.7 s, then a stop and a soft whoosh of the burn
x = t(1.2)
y = np.zeros_like(x)
tt, gap = 0.0, 0.035
while tt < 0.68:
    y += bp(nz(len(x)), 1800, 5000) * env(x - tt, 0.0003, 0.004) * 0.6
    tt += gap; gap *= 1.13
y += thud(x, 0.7, 0.8, 0.07) + swish(x, 0.7, 2500, 700, 0.25, 0.5)
save("gamble_spin", room(y, 0.35, 0.12), 0.6)

# heartbeat: two dull beats ("lub-DUB") at 0 and 0.24 s, the second heavier
x = t(0.9)
y = thud(x, 0.0, 0.8, 0.07) + thud(x, 0.24, 1.2, 0.1)
y += bp(nz(len(x)), 80, 220) * (env(x, 0.004, 0.06) * 0.6 + env(x - 0.24, 0.004, 0.08) * 0.9)
save("heartbeat", room(y, 0.35, 0.1), 0.65)
