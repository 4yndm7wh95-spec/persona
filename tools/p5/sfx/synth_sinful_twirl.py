"""Sinful Shell gun twirls (user 2026-11-09: the spin needs a sound or it does not read).
Writes art/sfx_p5/sinful_twirl.ogg (Satanael's giant gun) and sinful_twirl_small.ogg (Joker's pistol). Both start at the first
frame of the spin; the catch itself is sinful_catch, played by code.

A whoosh per quarter turn of the gun (barrel and grip each cut the air), timed from the same spin curve the code / rig uses, its pitch and
loudness following the spin speed (fast -> slow), over a whir that winds down. Satanael's is low and heavy (a huge gun, far away),
with a metallic shimmer; Joker's is short, light and bright.
  Satanael: angle = 720 * (1 - (1 - k)^3), k = t / SpinTime (p5mod SatanaelPuppet.SpinTime = 0.45)
  Joker: rig keys of cast_sinful_from_mask (tools/p5/build_rig_p5.py SPIN_SINFUL): 580 -> 4 deg over 0.57 s, slowing
"""
import subprocess
from pathlib import Path

import numpy as np
from scipy import signal

SR = 44100
OUT = Path(__file__).resolve().parents[3] / "art/sfx_p5"
rng = np.random.default_rng(2611)


def t(sec): return np.arange(int(sec * SR)) / SR
def bp(x, lo, hi, order=2): return signal.sosfilt(signal.butter(order, [lo, hi], "band", fs=SR, output="sos"), x)
def lp(x, f, order=2): return signal.sosfilt(signal.butter(order, f, "low", fs=SR, output="sos"), x)


def crossings(times, angles, step=90.0):
    """Times at which the angle passes each multiple of step, with the spin speed (deg/s) there."""
    out = []
    for i in range(1, len(times)):
        a0, a1 = angles[i - 1], angles[i]
        lo, hi = sorted((a0, a1))
        k = np.floor(hi / step) * step
        while k > lo:
            f = (k - a0) / (a1 - a0)
            out.append((times[i - 1] + f * (times[i] - times[i - 1]), abs(a1 - a0) / (times[i] - times[i - 1])))
            k -= step
    return sorted(out)


def whoosh(x, at, speed, vmax, low, high, gain):
    """One air cut: band noise whose band rises with speed, a short swell and a quicker release."""
    v = speed / vmax
    w = 0.018 + 0.05 * (1 - v)
    d = x - at
    envl = np.where(d < 0, np.exp(-(d / (w * 0.9)) ** 2), np.exp(-(d / (w * 0.55)) ** 2))
    fc = low + (high - low) * v
    n = bp(rng.standard_normal(len(x)), fc * 0.55, min(fc * 1.6, SR / 2 - 200))
    return n * envl * gain * (0.35 + 0.65 * v)


def twirl(length, times, angles, low, high, whir_hz, shimmer, gain):
    x = t(length)
    cr = crossings(times, angles)
    vmax = max(s for _, s in cr)
    y = sum(whoosh(x, at, s, vmax, low, high, gain) for at, s in cr)
    # the whir under it: the spin rate as a low buzz that winds down to the catch
    speed = np.interp(x, times[1:], np.abs(np.diff(angles)) / np.diff(times))
    ph = 2 * np.pi * np.cumsum(whir_hz * (0.4 + 0.6 * speed / vmax)) / SR
    y += np.sin(ph) * (speed / vmax) ** 1.5 * 0.25 * gain * (np.sin(ph * 0.5) ** 2)
    if shimmer:
        ring = sum(a * np.sin(2 * np.pi * f * x) for f, a in [(1290, 1), (2030, 0.6), (3170, 0.35)])
        y += ring * (speed / vmax) * 0.06 * gain * (1 + 0.5 * np.sin(np.cumsum(speed) / SR * np.pi / 90))
    y *= np.minimum(1, x / 0.01) * np.clip((times[-1] + 0.12 - x) / 0.12, 0, 1)
    return y


def save(name, x):
    x = x / (np.abs(x).max() + 1e-9) * 0.9
    d = int(0.002 * SR)
    st = np.stack([x, np.concatenate([np.zeros(d), x[:-d]])], 1)
    raw = (np.clip(st, -1, 1) * 32767).astype(np.int16).tobytes()
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "s16le", "-ar", str(SR), "-ac", "2", "-i", "-", "-c:a", "libvorbis", "-q:a", "6",
                    str(OUT / f"{name}.ogg")], input=raw, check=True)
    print(name, round(len(x) / SR, 2))


SPIN = 0.45
ts = np.linspace(0, SPIN, 400)
sat = 720 * (1 - (1 - ts / SPIN) ** 3)
save("sinful_twirl", twirl(SPIN + 0.15, ts, sat, 250, 1400, 70, True, 1.0))

jt = np.array([0, .02, .12, .24, .36, .47, .57])
ja = np.array([580, 540, 360, 210, 100, 35, 4], float)
jts = np.linspace(0, .57, 400)
save("sinful_twirl_small", twirl(.57 + 0.15, jts, np.interp(jts, jt, ja), 900, 4200, 160, False, 0.8))
