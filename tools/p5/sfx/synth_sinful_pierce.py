"""Sinful Shell pierce impact (user 2026-11-09: the plain enemy-hit sound is too flat for a shot this heavy - make it louder and
more shocking). Writes art/sfx_p5/sinful_pierce.ogg (each enemy the round passes through) and sinful_pierce_last.ogg (the last
one: bigger, with a longer sub tail and debris). Impact at 0.0 s - no lead-in, the transient is the first sample.

Layers: a hard 0.4 ms crack -> a metallic tear (bright noise sweeping down, the armor ripping open) -> a saturated body slam
(150-600 Hz, tanh) -> a sub punch dropping 75 -> 32 Hz -> the round tearing out the far side (noise swept high -> low over
~0.18 s) -> a short ring of torn metal -> (last) shrapnel patter and a rolling low tail. Reuses the helpers of synth_sinful.py.
"""
import subprocess
from pathlib import Path

import numpy as np
from scipy import signal

SR = 44100
DRIVE = 4.0     # soft-clip drive: the hit has to sit louder than the vanilla damage_enemy (~-7 dB peak RMS)
OUT = Path(__file__).resolve().parents[3] / "art/sfx_p5"
rng = np.random.default_rng(1109)


def t(sec): return np.arange(int(sec * SR)) / SR
def env(x, a, d): return np.minimum(1, x / max(a, 1e-4)) * np.exp(-np.maximum(0, x - a) / d)
def bp(x, lo, hi, order=2): return signal.sosfilt(signal.butter(order, [lo, hi], "band", fs=SR, output="sos"), x)
def lp(x, f, order=2): return signal.sosfilt(signal.butter(order, f, "low", fs=SR, output="sos"), x)
def hp(x, f, order=2): return signal.sosfilt(signal.butter(order, f, "high", fs=SR, output="sos"), x)


def reverb(x, decay=1.0, mix=0.25):
    n = int(decay * SR)
    ir = lp(rng.standard_normal(n) * np.exp(-np.arange(n) / (decay * SR / 5)), 5000)
    wet = signal.fftconvolve(x, ir)
    wet /= np.abs(wet).max() + 1e-9
    y = np.zeros(len(x) + n)
    y[: len(x)] += x * (1 - mix)
    y[: len(wet)] += wet[: len(y)] * mix * np.abs(x).max()
    return y


def sweep_noise(x, f0, f1, dur, q=0.35):
    """Noise through a band that slides from f0 to f1 over dur (block-wise; the round tearing through)."""
    out = np.zeros_like(x)
    nz = rng.standard_normal(len(x))
    blk = 256
    zi = None
    for i in range(0, len(x), blk):
        k = min(1.0, x[i] / dur)
        fc = f0 * (f1 / f0) ** k
        sos = signal.butter(2, [fc * (1 - q), min(fc * (1 + q), SR / 2 - 100)], "band", fs=SR, output="sos")
        if zi is None:
            zi = np.zeros((sos.shape[0], 2))
        out[i:i + blk], zi = signal.sosfilt(sos, nz[i:i + blk], zi=zi)
    return out


def pierce(length, big):
    x = t(length)
    crack = hp(rng.standard_normal(len(x)), 2000) * env(x, 0.0003, 0.006) * 2.4
    tear = sweep_noise(x, 7000, 1500, 0.09) * env(x, 0.0008, 0.05) * 1.6
    body = np.tanh(4.0 * bp(rng.standard_normal(len(x)), 150, 600) * env(x, 0.001, 0.07 if not big else 0.11) * 3.0) * 1.3
    sub_f = 32 + 43 * np.exp(-x / (0.07 if not big else 0.12))
    sub = np.sin(2 * np.pi * np.cumsum(sub_f) / SR) * env(x, 0.002, 0.22 if not big else 0.55) * (2.2 if not big else 3.0)
    out_t = x - 0.025
    rip = sweep_noise(np.maximum(out_t, 0), 5000, 400, 0.18) * env(np.maximum(out_t, 0), 0.01, 0.08) * (out_t > 0) * 1.1
    ring = sum(a * np.sin(2 * np.pi * f * x + f) for f, a in [(611, 1.0), (1433, 0.6), (2389, 0.45), (3911, 0.25)])
    ring *= env(x, 0.001, 0.16 if not big else 0.35) * 0.22
    y = crack + tear + body + sub + rip + ring
    if big:
        pat = np.zeros_like(x)
        for _ in range(30):
            s0 = rng.uniform(0.06, 1.0)
            k, n = int(s0 * SR), int(0.025 * SR)
            if k + n < len(pat):
                pat[k:k + n] += rng.standard_normal(n) * np.exp(-np.arange(n) / (0.005 * SR)) * rng.uniform(0.15, 0.4) * np.exp(-s0 / 0.5)
        y += bp(pat, 900, 6000)
        y += lp(rng.standard_normal(len(x)), 160, 4) * env(x, 0.05, 0.6) * 2.4     # rolling low tail
    y = reverb(y, 0.9 if not big else 1.4, 0.15 if not big else 0.2)
    return np.tanh(DRIVE * y / (np.abs(y).max() + 1e-9)) / np.tanh(DRIVE)          # glue + loudness (soft clip after the room)


def save(name, x):
    x = x / (np.abs(x).max() + 1e-9) * 0.97
    d = int(0.0025 * SR)
    st = np.stack([x, np.concatenate([np.zeros(d), x[:-d]])], 1)
    raw = (np.clip(st, -1, 1) * 32767).astype(np.int16).tobytes()
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "s16le", "-ar", str(SR), "-ac", "2", "-i", "-", "-c:a", "libvorbis", "-q:a", "6",
                    str(OUT / f"{name}.ogg")], input=raw, check=True)
    print(name, round(len(x) / SR, 2))


save("sinful_pierce", pierce(0.9, False))
save("sinful_pierce_last", pierce(2.0, True))
