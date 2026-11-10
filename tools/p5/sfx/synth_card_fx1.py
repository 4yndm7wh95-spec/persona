"""Per-card skill sounds, batch 1 (p5mod/src/Vfx/CardSkillFx.cs, user 2026-11-10). Restrained (you can tell it is there, no more),
noise only - no pure tones, no pitch glides; only the two Forms (rare powers) get some weight.
Writes art/sfx_p5/: footwork_steps, reticle_lock, track_scan, venom_coat, fumes_roll, burst_echo, serpent_rise, wraith_form.
This script only writes these eight files."""
import subprocess
from pathlib import Path
import numpy as np
from scipy import signal

SR = 44100
OUT = Path(__file__).resolve().parents[3] / "art/sfx_p5"
rng = np.random.default_rng(1114)


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


# footwork_steps: three quick light steps (0, 0.1, 0.2 s) and a breath of air rising after the last one
x = t(0.75)
y = step(x, 0.0, 0.8) + step(x, 0.1, 0.9) + step(x, 0.2, 1.1)
y += moving_band(x, 600, 3000, 0.25, 0.5, 0.2) * np.minimum(1, np.maximum(0, x - 0.2) / 0.05) * np.exp(-np.maximum(0, x - 0.4) / 0.08) * (x > 0.2) * 0.5
save("footwork_steps", room(y, 0.3, 0.1), 0.7)

# reticle_lock: two small dry mechanical clicks 40 ms apart (the second slightly heavier)
x = t(0.25)
y = np.zeros_like(x)
for d, g in [(0.0, 0.7), (0.04, 1.0)]:
    y += hp(nz(len(x)), 2500) * env(x - d, 0.0002, 0.0025) * g
    y += bp(nz(len(x)), 700, 2200) * env(x - d, 0.0005, 0.008) * 0.5 * g
save("reticle_lock", room(y, 0.2, 0.08), 0.65)

# track_scan: a thin quick swish of air (the lines shooting out)
x = t(0.35)
y = moving_band(x, 1800, 5000, 0.12, 0.35) * env(x, 0.02, 0.06) * 1.0
save("track_scan", room(y, 0.25, 0.1), 0.6)

# venom_coat: thick liquid welling up - low band noise with a slow bubbly wobble, a few soft drips, a quiet sizzle at the end
x = t(1.0)
wob = 0.6 + 0.4 * np.sin(2 * np.pi * 7 * x + 2 * np.sin(2 * np.pi * 2.3 * x))
y = bp(nz(len(x)), 180, 700) * np.minimum(1, x / 0.08) * np.exp(-np.maximum(0, x - 0.4) / 0.2) * wob * 1.2
for d in (0.25, 0.4, 0.55, 0.68):
    y += bp(nz(len(x)), 500, 1600) * env(x - d, 0.001, 0.015) * 0.5
y += bp(nz(len(x)), 3000, 6500) * env(x - 0.76, 0.01, 0.1) * 0.25
save("venom_coat", room(y, 0.35, 0.12), 0.65)

# fumes_roll: low air rolling out along the ground, slowly fading
x = t(1.5)
y = moving_band(x, 250, 900, 0.6, 0.6) * np.minimum(1, x / 0.15) * np.exp(-np.maximum(0, x - 0.5) / 0.35) * 1.0
y += lp(nz(len(x)), 100, 3) * np.minimum(1, x / 0.1) * np.exp(-x / 0.4) * 1.2
save("fumes_roll", room(y, 0.5, 0.15), 0.65)

# burst_echo: a soft dull pop and the same again 0.16 s later, a little brighter
x = t(0.7)
y = np.zeros_like(x)
for d, g, hi in [(0.0, 1.0, 0.25), (0.16, 0.85, 0.4)]:
    y += lp(nz(len(x)), 160, 3) * env(x - d, 0.003, 0.07) * 2.0 * g
    y += bp(nz(len(x)), 800, 3500) * env(x - d, 0.001, 0.03) * hi * g
y += moving_band(x, 600, 2500, 0.3, 0.5, 0.16) * np.minimum(1, np.maximum(0, x - 0.16) / 0.05) * np.exp(-np.maximum(0, x - 0.3) / 0.1) * (x > 0.16) * 0.35
save("burst_echo", room(y, 0.35, 0.12), 0.7)

# serpent_rise (rare power): a dark swell from below, the serpents' hiss over it, the strike (a whip of air and a dull hit at ~1.15 s)
x = t(1.8)
swell = np.minimum(1, x / 0.4) * np.exp(-np.maximum(0, x - 0.6) / 0.35)
y = lp(nz(len(x)), 120, 3) * swell * 2.0
y += moving_band(x, 200, 800, 0.55, 0.6) * swell * 0.8
hiss = np.minimum(1, np.maximum(0, x - 0.3) / 0.25) * np.exp(-np.maximum(0, x - 0.8) / 0.2) * (0.7 + 0.3 * np.sin(2 * np.pi * 5 * x))
y += bp(nz(len(x)), 3500, 6800) * hiss * 0.28
y += moving_band(x, 900, 3500, 0.15, 0.4, 0.95) * env(x - 0.95, 0.06, 0.06) * 0.6
y += lp(nz(len(x)), 150, 3) * env(x - 1.15, 0.003, 0.12) * 2.2
save("serpent_rise", room(y, 0.6, 0.2), 0.8)

# wraith_form (rare power): a cold wind drawn inward (band closing down over 0.5 s), a hollow breath under it, a dull closing thump at 0.55 s
x = t(1.6)
y = moving_band(x, 3000, 600, 0.55, 0.5) * np.minimum(1, x / 0.2) * np.exp(-np.maximum(0, x - 0.55) / 0.08) * 0.9
y += bp(nz(len(x)), 300, 600, 3) * np.minimum(1, x / 0.3) * np.exp(-np.maximum(0, x - 0.5) / 0.4) * 0.8
y += lp(nz(len(x)), 130, 3) * env(x - 0.55, 0.004, 0.16) * 2.4
y += bp(nz(len(x)), 400, 1200) * env(x - 0.55, 0.002, 0.3) * 0.25
save("wraith_form", room(y, 0.8, 0.25), 0.8)
