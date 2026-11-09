"""Synthesized SFX for Sinful Shell (Grand Finale), user 2026-11-08: "synthesize it yourself, not an ordinary pistol".
Writes art/sfx_p5/sinful_{thunder,charge,catch,fire}.ogg (44.1 kHz stereo)."""
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


# ── thunder: sharp crack (several close noise bursts, bright) + rolling low rumble
x = t(2.2)
crack = np.zeros_like(x)
for k, (at, amp) in enumerate([(0.0, 1.0), (0.018, 0.7), (0.045, 0.85), (0.09, 0.5), (0.16, 0.35)]):
    m = x >= at
    crack[m] += amp * rng.standard_normal(m.sum()) * env(x[m] - at, 0.0008, 0.025)
crack = hp(crack, 1800) * 0.9 + bp(crack, 400, 3000) * 0.6
rumble = lp(rng.standard_normal(len(x)), 180, 4) * env(x, 0.06, 0.7) * (1 + 0.5 * np.sin(2 * np.pi * 3.1 * x))
thunder = reverb(crack + rumble * 2.2, 1.6, 0.3)
save("sinful_thunder", thunder)

# ── charge: rising whine + growing sub hum + crackle, ~1 s
x = t(1.05)
k = x / x[-1]
f = 220 + 1400 * k ** 2.2
ph = 2 * np.pi * np.cumsum(f) / SR
whine = (np.sin(ph) + 0.4 * np.sin(2.01 * ph) + 0.2 * np.sin(3.02 * ph)) * (0.15 + 0.85 * k ** 1.5)
hum = np.sin(2 * np.pi * 48 * x) * (0.3 + 0.9 * k) + 0.4 * np.sin(2 * np.pi * 96 * x) * k
crackle = hp(rng.standard_normal(len(x)), 3000) * (rng.random(len(x)) < 0.02 + 0.1 * k) * 0.8 * k
charge = whine * 0.5 + hum * 0.7 + crackle
charge *= np.minimum(1, x / 0.05) * (1 - np.clip((x - 0.98) / 0.07, 0, 1))
save("sinful_charge", reverb(charge, 0.6, 0.2))

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

# ── the shot, rebuilt (user 2026-11-09: no time stop; the impact should come from sound as much as picture). Impact lands at 0.30 s:
#    0.00-0.26 reverse-swell inhale (air pulled toward the muzzle) → 0.26-0.30 dead silence → 0.30 crack + sub drop (90→28 Hz) + saturated boom
#    → metallic ring-out → debris patter → three rolling thunder echoes off the far clouds → a thin tinnitus ring that fades over ~2.5 s
x = t(4.5)
y = np.zeros_like(x)
I = 0.30
m = x < 0.26; tt = x[m]
y[m] += hp(rng.standard_normal(m.sum()), 900) * (tt / 0.26) ** 3.2 * 0.9 + np.sin(2 * np.pi * (60 + 140 * tt / 0.26) * tt) * (tt / 0.26) ** 2 * 0.5
m = x >= I; tt = x[m] - I
crack = hp(rng.standard_normal(m.sum()), 1500) * env(tt, 0.0004, 0.02) * 2.2
sub_f = 28 + 62 * np.exp(-tt / 0.18)
sub = np.sin(2 * np.pi * np.cumsum(sub_f) / SR) * env(tt, 0.003, 0.9) * 2.6
boom = np.tanh(3.5 * lp(rng.standard_normal(m.sum()), 500, 4) * env(tt, 0.002, 0.28) * 4) * 1.2
ring = sum(a * np.sin(2 * np.pi * f * tt) for f, a in [(523, 0.5), (1187, 0.35), (2093, 0.25), (3370, 0.15)]) * env(tt, 0.002, 0.6) * 0.35
pat = np.zeros(m.sum())
for _ in range(40):
    s0 = rng.uniform(0.15, 1.6); k = int(s0 * SR)
    n = int(0.03 * SR)
    if k + n < len(pat): pat[k:k + n] += rng.standard_normal(n) * np.exp(-np.arange(n) / (0.006 * SR)) * rng.uniform(0.1, 0.35) * np.exp(-s0 / 0.9)
pat = bp(pat, 800, 5000)
echo = np.zeros(m.sum())
for d, a in [(0.55, 0.8), (1.15, 0.55), (1.9, 0.35)]:
    k = int(d * SR); r = lp(rng.standard_normal(len(echo) - k), 220, 4) * env(np.arange(len(echo) - k) / SR, 0.08, 0.7)
    echo[k:] += r * a * 3.0
tin = np.sin(2 * np.pi * 3520 * tt) * 0.06 * np.minimum(1, tt / 0.15) * np.exp(-tt / 1.4)
y[m] += crack + sub + boom + ring + pat + echo + tin
save("sinful_shot", reverb(y, 1.6, 0.22))

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
