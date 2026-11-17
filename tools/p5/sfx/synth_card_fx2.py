"""Per-card skill sounds, batch 2 (p5mod/src/Vfx/CardSkillFx2.cs, user 2026-11-10). Restrained (you can tell it is there, no more),
noise only - no pure tones, no pitch glides; only the two Forms (rare powers) get some weight.
Writes art/sfx_p5/: shadowmeld_sink, blur_dash, afterimage_orbit, shadow_step_cut, bullet_time_stop, flank_cross, malaise_press,
sweep_low. This script only writes these eight files."""
import subprocess
from pathlib import Path
import numpy as np
from scipy import signal

SR = 44100
OUT = Path(__file__).resolve().parents[3] / "art/sfx_p5"
rng = np.random.default_rng(1115)


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


# shadowmeld_sink: a low breath sinking (band closing down) and two soft thumps as the light rings pop at 0.85 / 0.98 s
x = t(1.3)
y = moving_band(x, 1500, 250, 0.7, 0.55) * np.minimum(1, x / 0.15) * np.exp(-np.maximum(0, x - 0.5) / 0.25) * 0.9
y += thud(x, 0.85, 0.7) + thud(x, 0.98, 0.55)
save("shadowmeld_sink", room(y, 0.5, 0.18), 0.65)

# blur_dash: one short quick rush of air
x = t(0.5)
y = swish(x, 0.0, 900, 3500, 0.16, 1.0) + bp(nz(len(x)), 2000, 5000) * env(x - 0.3, 0.002, 0.03) * 0.25
save("blur_dash", room(y, 0.25, 0.1), 0.6)

# afterimage_orbit: soft air circling (amplitude wobble at the orbit rate) and a gentle pull-in at 0.8 s
x = t(1.2)
wob = 0.6 + 0.4 * np.sin(2 * np.pi * 2.5 * x)
y = bp(nz(len(x)), 1200, 4000) * np.minimum(1, x / 0.1) * np.exp(-np.maximum(0, x - 0.6) / 0.15) * wob * 0.6
y += swish(x, 0.65, 3000, 900, 0.15, 0.7) + thud(x, 0.8, 0.5, 0.06)
save("afterimage_orbit", room(y, 0.4, 0.15), 0.6)

# shadow_step_cut: a cut ("shk") at 0.22 s, the breath sinking away, a dull return at 0.8 s
x = t(1.3)
y = hp(nz(len(x)), 2000) * env(x - 0.22, 0.001, 0.03) * 0.8 + swish(x, 0.2, 4000, 1200, 0.1, 0.8)
y += moving_band(x, 1200, 200, 0.4, 0.55, 0.26) * np.minimum(1, np.maximum(0, x - 0.26) / 0.05) * np.exp(-np.maximum(0, x - 0.4) / 0.15) * (x > 0.26) * 0.7
y += thud(x, 0.8, 0.9, 0.1) + swish(x, 0.72, 400, 1500, 0.15, 0.4)
save("shadow_step_cut", room(y, 0.45, 0.15), 0.65)

# bullet_time_stop: air pressure slowing down (band closing, rate of a flutter slowing), a stop at 0.5 s, a faint tail
x = t(1.4)
rate = 30 * np.exp(-x / 0.25) + 3
ph = np.cumsum(2 * np.pi * rate / SR)
y = bp(nz(len(x)), 500, 2500) * (0.6 + 0.4 * np.sin(ph)) * np.minimum(1, x / 0.06) * np.exp(-np.maximum(0, x - 0.45) / 0.05) * 0.8
y += thud(x, 0.5, 1.0, 0.12) + bp(nz(len(x)), 1500, 4000) * env(x - 0.5, 0.001, 0.02) * 0.4
y += bp(nz(len(x)), 300, 900) * env(x - 0.5, 0.05, 0.4) * 0.2
save("bullet_time_stop", room(y, 0.7, 0.22), 0.65)

# flank_cross: two crossing swishes and one crisp meeting knock at 0.09 s
x = t(0.7)
y = swish(x, 0.0, 1500, 5000, 0.1, 0.7) + swish(x, 0.01, 1300, 4500, 0.1, 0.7)
y += hp(nz(len(x)), 2500) * env(x - 0.09, 0.0005, 0.01) * 1.0 + thud(x, 0.09, 0.6, 0.05)
save("flank_cross", room(y, 0.35, 0.12), 0.65)

# malaise_press: a low swell pressing down, a dull thump at 0.48 s (the ring clamps), then a thin breath drawn away
x = t(1.4)
y = moving_band(x, 900, 200, 0.45, 0.6) * np.minimum(1, x / 0.1) * np.exp(-np.maximum(0, x - 0.4) / 0.1) * 0.8
y += lp(nz(len(x)), 110, 3) * np.minimum(1, x / 0.2) * np.exp(-x / 0.35) * 0.8
y += thud(x, 0.48, 1.0, 0.1)
y += moving_band(x, 600, 2500, 0.5, 0.5, 0.6) * np.minimum(1, np.maximum(0, x - 0.6) / 0.1) * np.exp(-np.maximum(0, x - 0.9) / 0.15) * (x > 0.6) * 0.35
save("malaise_press", room(y, 0.5, 0.15), 0.65)

# sweep_low: one low sweep along the ground, grit scattering after it
x = t(0.8)
y = swish(x, 0.0, 400, 1800, 0.18, 1.0, 0.6) + thud(x, 0.1, 0.6, 0.05)
for _ in range(18):
    d = rng.uniform(0.08, 0.4)
    y += bp(nz(len(x)), 2500, 6000) * env(x - d, 0.0004, 0.004) * rng.uniform(0.08, 0.22)
save("sweep_low", room(y, 0.3, 0.1), 0.65)
