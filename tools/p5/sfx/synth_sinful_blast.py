"""Sinful Shell: the charge and the shot (user 2026-11-10: after the twirl the gun charges - "zzzz" - then time stops right
before the shot, then one shot that must be very loud and explosive).
Writes art/sfx_p5/sinful_charge.ogg and art/sfx_p5/sinful_shot.ogg (44.1 kHz stereo).

sinful_charge: CHARGE s of electric sizzle that gets denser and higher, cut dead at the freeze (a tiny tick), then FREEZE s of
               near silence with only a very faint high ping - so the stillness is in the same file and always in sync.
sinful_shot:   comes straight out of that silence; the impact is at 0.02 s (p5mod/src/Vfx/Sfx.cs Lead).
CHARGE / FREEZE must match SinfulShell.ChargeTime / FreezeTime."""
import subprocess, numpy as np
from pathlib import Path
from scipy import signal

SR = 44100
OUT = Path(__file__).resolve().parents[3] / "art/sfx_p5"
rng = np.random.default_rng(1110)
CHARGE, FREEZE = 0.95, 0.42


def t(sec): return np.arange(int(sec * SR)) / SR
def env(x, a, d): return np.minimum(1, x / max(a, 1e-4)) * np.exp(-np.maximum(0, x - a) / d)
def bp(x, lo, hi, order=2): return signal.sosfilt(signal.butter(order, [lo, hi], "band", fs=SR, output="sos"), x)
def lp(x, f, order=2): return signal.sosfilt(signal.butter(order, f, "low", fs=SR, output="sos"), x)
def hp(x, f, order=2): return signal.sosfilt(signal.butter(order, f, "high", fs=SR, output="sos"), x)


def reverb(x, decay=1.2, mix=0.35, top=6000):
    n = int(decay * SR)
    ir = lp(rng.standard_normal(n) * np.exp(-np.arange(n) / (decay * SR / 5)), top)
    wet = signal.fftconvolve(x, ir)[: len(x) + n]
    wet /= np.abs(wet).max() + 1e-9
    y = np.zeros(len(x) + n); y[: len(x)] += x * (1 - mix); y[: len(wet)] += wet[: len(y)] * mix * np.abs(x).max()
    return y


def stereo(x, width=0.003):
    d = int(width * SR)
    return np.stack([x, np.concatenate([np.zeros(d), x[:-d]])], 1)


def save(name, x):
    x = x / (np.abs(x).max() + 1e-9) * 0.97
    raw = (np.clip(stereo(x), -1, 1) * 32767).astype(np.int16).tobytes()
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "s16le", "-ar", str(SR), "-ac", "2", "-i", "-", "-c:a", "libvorbis", "-q:a", "7",
                    str(OUT / f"{name}.ogg")], input=raw, check=True)
    print(name, round(len(x) / SR, 2))


# ── charge: "zzzz"
x = t(CHARGE); k = x / CHARGE
gf = 52 + 110 * k ** 1.5                                   # buzz rate climbs
gph = np.cumsum(gf) / SR
gate = np.clip(np.sin(2 * np.pi * gph) * 4, 0, 1)
sizzle = bp(rng.standard_normal(len(x)), 2400, 9000) * (0.2 + 0.8 * gate)
saw = 2 * ((gph * 2) % 1) - 1
buzz = bp(saw, 350, 3600) * 0.9
wf = 480 + 3400 * k ** 2.4                                 # thin whine climbing over it
wph = 2 * np.pi * np.cumsum(wf) / SR
whine = np.sin(wph) * 0.2 + np.sin(2.003 * wph) * 0.07
sub = np.sin(2 * np.pi * np.cumsum(40 + 34 * k) / SR) * 0.55
crk = np.zeros(len(x))                                     # sparks: more and more of them
for i in np.nonzero(rng.random(len(x)) < (0.0006 + 0.006 * k ** 2))[0]:
    n = min(int(0.004 * SR), len(x) - i)
    crk[i:i + n] += rng.standard_normal(n) * np.exp(-np.arange(n) / (0.0009 * SR)) * rng.uniform(0.5, 1.4)
crk = hp(crk, 3200)
charge = (sizzle * 0.85 + buzz * 0.45 + whine + sub * 0.6 + crk * 0.9) * (0.1 + 0.9 * k ** 1.6)
charge *= np.minimum(1, x / 0.03) * np.clip((CHARGE - x) / 0.003, 0, 1)   # 3 ms cut at the freeze
z = t(FREEZE)
tick = hp(rng.standard_normal(len(z)), 2500) * np.exp(-z / 0.0025) * 0.4
ping = (np.sin(2 * np.pi * 6200 * z) + 0.5 * np.sin(2 * np.pi * 9300 * z)) * np.exp(-z / 0.3) * 0.02 * np.minimum(1, z / 0.01)
save("sinful_charge", np.concatenate([charge, tick + ping]))

# ── the shot, v3: a very loud, splitting "PAK" straight out of silence, then the sky rolling back
I = 0.02
x = t(4.6); y = np.zeros_like(x)


def at(d):
    m = x >= I + d
    return m, x[m] - I - d


# the crack: a hard full-band transient, then two more slaps (the round's own crack behind the muzzle report)
for d, g in [(0.0, 1.0), (0.042, 0.62), (0.105, 0.36)]:
    m, tt = at(d)
    y[m] += hp(rng.standard_normal(m.sum()), 900) * env(tt, 0.0003, 0.011) * 3.4 * g
    y[m] += bp(rng.standard_normal(m.sum()), 1800, 5200) * env(tt, 0.0005, 0.034) * 2.2 * g
m, tt = at(0.0)
# the body of the blast: wide band, driven hard
body = np.tanh(7.0 * bp(rng.standard_normal(m.sum()), 90, 3600) * env(tt, 0.001, 0.17)) * 1.7
# sub drop 125 → 32 Hz, slightly driven so small speakers still get its harmonics
sub = np.tanh(1.8 * np.sin(2 * np.pi * np.cumsum(32 + 93 * np.exp(-tt / 0.13)) / SR)) * env(tt, 0.002, 0.75) * 2.3
# tearing roar: the air ripped open along the path, rippling
rip = 0.55 + 0.45 * np.clip(lp(rng.standard_normal(m.sum()), 28, 2) * 28, -1, 1)
roar = bp(rng.standard_normal(m.sum()), 140, 2800) * env(tt, 0.015, 0.7) * rip * 1.3
ring = sum(a * np.sin(2 * np.pi * f * tt) for f, a in [(523, 0.5), (1187, 0.35), (2093, 0.25), (3370, 0.15)]) * env(tt, 0.002, 0.5) * 0.22
# the sky answers: three rolls coming back off the far clouds (bright enough to be heard, not a muffled thud)
echo = np.zeros(m.sum())
for d, a in [(0.5, 0.8), (1.05, 0.55), (1.75, 0.34)]:
    k0 = int(d * SR); n = len(echo) - k0
    e = np.arange(n) / SR
    echo[k0:] += (lp(rng.standard_normal(n), 700, 3) * 2.6 + bp(rng.standard_normal(n), 900, 3200) * 0.5 * np.exp(-e / 0.12)) * env(e, 0.05, 0.6) * a
pat = np.zeros(m.sum())                                      # debris
for _ in range(46):
    s0 = rng.uniform(0.18, 1.7); k0 = int(s0 * SR); n = int(0.03 * SR)
    if k0 + n < len(pat): pat[k0:k0 + n] += rng.standard_normal(n) * np.exp(-np.arange(n) / (0.006 * SR)) * rng.uniform(0.1, 0.35) * np.exp(-s0 / 0.9)
pat = bp(pat, 800, 5000)
tin = np.sin(2 * np.pi * 3520 * tt) * 0.05 * np.minimum(1, tt / 0.15) * np.exp(-tt / 1.4)
y[m] += body + sub + roar + ring + echo + pat + tin
y = np.tanh(1.5 * y / np.abs(y).max()) / np.tanh(1.5)          # push the whole thing into the ceiling: dense, loud
y = reverb(y, 1.7, 0.2, 7500)
y[: int(I * SR)] = 0                                           # nothing before the impact
y = y[: int(4.8 * SR)]
y[-int(0.6 * SR):] *= np.linspace(1, 0, int(0.6 * SR))
save("sinful_shot", y)
