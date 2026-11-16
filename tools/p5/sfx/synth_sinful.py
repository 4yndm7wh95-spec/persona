"""Synthesized SFX for Sinful Shell (Grand Finale), user 2026-11-08: "synthesize it yourself, not an ordinary pistol".
Writes art/sfx_p5/sinful_{catch,fire,flyby}.ogg (thunder: synth_sinful_thunder.py; charge and shot: synth_sinful_blast.py) (44.1 kHz stereo)."""
import subprocess, numpy as np
from pathlib import Path
from scipy import signal

SR = 44100
OUT = Path(__file__).resolve().parents[3] / "art/sfx_p5"
rng = np.random.default_rng(7)


def t(sec): return np.arange(int(sec * SR)) / SR
def env(x, a, d): return np.minimum(1, x / max(a, 1e-4)) * np.exp(-np.maximum(0, x - a) / d)
def bp(x, lo, hi, order=2): return signal.sosfilt(signal.butter(order, [lo, hi], "band", fs=SR, output="sos"), x)
def lp(x, f, order=2): return signal.sosfilt(signal.butter(order, f, "low", fs=SR, output="sos"), x)
def hp(x, f, order=2): return signal.sosfilt(signal.butter(order, f, "high", fs=SR, output="sos"), x)


def reverb(x, decay=1.2, mix=0.35):
    n = int(decay * SR)
    ir = rng.standard_normal(n) * np.exp(-np.arange(n) / (decay * SR / 5))
    ir = lp(ir, 6000)
    wet = signal.fftconvolve(x, ir)[: len(x) + n]
    wet /= np.abs(wet).max() + 1e-9
    y = np.zeros(len(x) + n); y[: len(x)] += x * (1 - mix); y[: len(wet)] += wet[: len(y)] * mix * np.abs(x).max()
    return y


def stereo(x, width=0.003):
    d = int(width * SR)
    l = x; r = np.concatenate([np.zeros(d), x[:-d]]) if d else x
    return np.stack([l, r], 1)


def save(name, x):
    x = x / (np.abs(x).max() + 1e-9) * 0.95
    if x.ndim == 1: x = stereo(x)
    raw = (np.clip(x, -1, 1) * 32767).astype(np.int16).tobytes()
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "s16le", "-ar", str(SR), "-ac", "2", "-i", "-", "-c:a", "libvorbis", "-q:a", "6",
                    str(OUT / f"{name}.ogg")], input=raw, check=True)
    print(name, len(x) / SR)


# ── thunder: moved to tools/p5/sfx/synth_sinful_thunder.py (user 2026-11-09: the old one was too explosive)

# ── charge: moved to tools/p5/sfx/synth_sinful_blast.py (user 2026-11-10: charge → time stop → shot)

# ── catch: heavy metal clack (two transients + short ring)
x = t(0.6)
clack = rng.standard_normal(len(x)) * env(x, 0.0005, 0.012)
clack += 0.7 * np.concatenate([np.zeros(int(0.028 * SR)), rng.standard_normal(len(x) - int(0.028 * SR))]) * env(np.maximum(0, x - 0.028), 0.0005, 0.01)
clack = bp(clack, 900, 7000)
ring = sum(a * np.sin(2 * np.pi * fr * x) for fr, a in [(1830, 1), (2710, 0.6), (4120, 0.4)]) * env(x, 0.001, 0.12) * 0.25
thud = np.sin(2 * np.pi * 95 * x) * env(x, 0.002, 0.05) * 0.8
save("sinful_catch", reverb(clack + ring + thud, 0.4, 0.15))

# ── Sinful Shell fire: reverse suck-in -> crack -> sub slam -> distorted blast -> metallic ring -> long noise tail + echoes
pre = 0.12
x = t(3.4)
y = np.zeros_like(x)
# reverse swell (noise rising into the shot)
sw = x < pre
y[sw] += bp(rng.standard_normal(sw.sum()), 300, 5000) * (x[sw] / pre) ** 3 * 0.6
s = x - pre
m = s >= 0
crack = rng.standard_normal(len(x)) * env(np.maximum(s, 0), 0.0004, 0.018) * m
y += hp(crack, 1200) * 1.0
sub_f = 30 + 70 * np.exp(-np.maximum(s, 0) / 0.08)
sub = np.sin(2 * np.pi * np.cumsum(sub_f) / SR) * env(np.maximum(s, 0), 0.003, 0.45) * m
y += sub * 1.6
blast = np.tanh(3.5 * bp(rng.standard_normal(len(x)), 120, 1800) * env(np.maximum(s, 0), 0.004, 0.22)) * m
y += blast * 0.9
ring = sum(a * np.sin(2 * np.pi * fr * x + 1.3 * fr) for fr, a in [(730, 1), (1095, 0.7), (1642, 0.5), (2461, 0.3)])
y += ring * env(np.maximum(s, 0), 0.01, 0.6) * m * 0.18
tail = lp(rng.standard_normal(len(x)), 900, 2) * env(np.maximum(s, 0), 0.05, 1.1) * m
y += tail * 0.7
# echoes off the night sky
for d, g in [(0.23, 0.35), (0.47, 0.2), (0.78, 0.1)]:
    n = int(d * SR); e = np.zeros_like(y); e[n:] = lp(y[:-n], 2500) * g; y += e
save("sinful_fire", reverb(y, 1.8, 0.25))

# ── the shot: moved to tools/p5/sfx/synth_sinful_blast.py (v3, user 2026-11-10: much louder and more explosive)

# ── fly-by: the round comes out of the deep toward the viewer — a doppler whoosh (rising, roaring past, falling), ~0.9 s, passing at 0.35 s
x = t(0.9)
d = (x - 0.35)
f = 300 + 2600 / (1 + np.exp(d / 0.05))
nz = rng.standard_normal(len(x))
wh = np.zeros_like(x)
for lo in (0.6, 1.0, 1.6):
    wh += signal.lfilter(*signal.butter(2, [min(0.99, f.mean() * lo / (SR / 2)) * 0.5, min(0.99, f.mean() * lo / (SR / 2))], "band"), nz) * 0.3
wh *= np.exp(-(d / 0.12) ** 2) * 1.0 + np.exp(-np.maximum(d, 0) / 0.18) * (d > 0) * 0.5
tone = np.sin(2 * np.pi * np.cumsum(f * 0.25) / SR) * np.exp(-(d / 0.1) ** 2) * 0.4
save("sinful_flyby", wh + tone)
