"""Buff / debuff sounds, second pass (user 2026-11-10: restrained - you should just be able to tell it is there; nothing cartoonish:
no pure tones, no pitch glides). Filtered noise only.
Writes art/sfx_p5/: buff_rise (a breath of air sweeping up + a very soft low bed), buff_rise_dark (darker, lower, a dull low under it),
debuff_press (a short push of air downward into a dull thud), energy_pull (air drawn in to the chest, 0.28 s, then a soft dull pop).
This script only writes these four files."""
import subprocess
from pathlib import Path
import numpy as np
from scipy import signal

SR = 44100
OUT = Path(__file__).resolve().parents[3] / "art/sfx_p5"
rng = np.random.default_rng(1113)


def t(sec): return np.arange(int(sec * SR)) / SR
def env(x, a, d): return np.minimum(1, np.maximum(x, 0) / max(a, 1e-5)) * np.exp(-np.maximum(0, x - a) / d) * (x >= 0)
def bp(x, lo, hi, order=2): return signal.sosfilt(signal.butter(order, [lo, hi], "band", fs=SR, output="sos"), x)
def lp(x, f, order=2): return signal.sosfilt(signal.butter(order, f, "low", fs=SR, output="sos"), x)
def nz(n): return rng.standard_normal(n)


def moving_band(x, f0, f1, dur, q=0.5):
    """noise through a band that opens upward (a breath of air, not a pitch: the band is wide)"""
    out = np.zeros_like(x); n = nz(len(x)); zi = None
    for i in range(0, len(x), 256):
        k = min(1.0, x[i] / dur); fc = f0 * (f1 / f0) ** k
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


# buff_rise: soft attack of air at the feet, the air sweeps up with the column (0.3 s), a faint airy sparkle at the top
x = t(0.7)
swell = np.minimum(1, x / 0.06) * np.exp(-np.maximum(0, x - 0.26) / 0.12)
y = moving_band(x, 500, 3200, 0.3, 0.55) * swell * 1.0
y += lp(nz(len(x)), 180) * env(x, 0.004, 0.08) * 0.9
for _ in range(10):
    d = rng.uniform(0.22, 0.42)
    y += bp(nz(len(x)), 3000, 6500) * env(x - d, 0.0005, 0.006) * rng.uniform(0.06, 0.16)
save("buff_rise", room(y, 0.4, 0.15), 0.7)

# buff_rise_dark: the same gesture lower and darker, with a dull low swell under it
x = t(0.8)
swell = np.minimum(1, x / 0.07) * np.exp(-np.maximum(0, x - 0.3) / 0.15)
y = moving_band(x, 250, 1300, 0.34, 0.6) * swell * 1.0
y += lp(nz(len(x)), 120, 3) * np.minimum(1, x / 0.05) * np.exp(-x / 0.25) * 1.6
save("buff_rise_dark", room(y, 0.5, 0.18), 0.7)

# debuff_press: air pushed down onto the target and a dull thud as the ring clamps
x = t(0.55)
y = moving_band(x, 2200, 500, 0.16, 0.6) * np.minimum(1, x / 0.04) * np.exp(-np.maximum(0, x - 0.12) / 0.06) * 0.8
y += lp(nz(len(x)), 150, 3) * env(x - 0.15, 0.003, 0.09) * 2.2
y += bp(nz(len(x)), 600, 1800) * env(x - 0.15, 0.001, 0.02) * 0.4
save("debuff_press", room(y, 0.35, 0.12), 0.7)

# energy_pull: air drawn in to the chest over 0.28 s (band opening upward, growing), then a soft dull pop as it fills - no ringing
x = t(0.7)
y = moving_band(x, 400, 2600, 0.28, 0.5) * (np.clip(x / 0.28, 0, 1) ** 2.0) * (x < 0.285) * 0.8
y += lp(nz(len(x)), 160, 3) * env(x - 0.28, 0.003, 0.08) * 2.0
y += bp(nz(len(x)), 900, 3500) * env(x - 0.28, 0.001, 0.025) * 0.35
save("energy_pull", room(y, 0.4, 0.14), 0.7)
