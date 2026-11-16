"""Scene open / close for Sinful Shell (handoff item B, user 2026-11-09: light, just the feel of it;
v2, same day: the opening was hard to hear at all - brighter whoosh band that sits above the thunder's low end, more crackle).
A low-passed blue-flame sweep with a little crackle. Open: strong at once, fading as the ring slows (0.75 s).
Close: the reverse - swells as the ring shrinks into Joker's feet and ends on a soft thump.
Writes art/sfx_p5/sinful_sweep_open.ogg, sinful_sweep_close.ogg."""
import subprocess, numpy as np
from pathlib import Path
from scipy import signal

SR = 44100
OUT = Path(__file__).resolve().parents[3] / "art/sfx_p5"
rng = np.random.default_rng(11)


def lp(x, f, order=2): return signal.sosfilt(signal.butter(order, f, "low", fs=SR, output="sos"), x)
def bp(x, lo, hi, order=2): return signal.sosfilt(signal.butter(order, [lo, hi], "band", fs=SR, output="sos"), x)


def save(name, x):
    x = x / (np.abs(x).max() + 1e-9) * 0.9
    st = np.stack([x, np.concatenate([np.zeros(90), x[:-90]])], 1)
    raw = (np.clip(st, -1, 1) * 32767).astype(np.int16).tobytes()
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "s16le", "-ar", str(SR), "-ac", "2", "-i", "-", "-c:a", "libvorbis", "-q:a", "6",
                    str(OUT / f"{name}.ogg")], input=raw, check=True)
    print(name, len(x) / SR)


def sweep(sec):
    n = int(sec * SR); x = np.arange(n) / SR; k = x / sec
    # roar: noise through a low-pass whose cutoff glides down as the ring slows
    body = np.zeros(n)
    nz = rng.standard_normal(n)
    for i in range(0, n, 2048):
        f = 5000 * (1 - k[i]) ** 1.3 + 500
        seg = slice(i, min(n, i + 2048))
        body[seg] = lp(nz[max(0, i - 4096):seg.stop], f)[-(seg.stop - i):]
    env = np.minimum(1, x / 0.03) * (1 - k) ** 1.2
    # a whoosh band (600-2500 Hz) that rises a little then fades, so the sweep reads above the thunder
    whoosh = bp(rng.standard_normal(n), 600, 2500) * np.minimum(1, x / 0.06) * (1 - k) ** 0.9
    crack = np.zeros(n)
    for _ in range(60):
        s0 = int(rng.uniform(0, sec * 0.8) * SR); m = int(0.012 * SR)
        if s0 + m < n: crack[s0:s0 + m] += rng.standard_normal(m) * np.exp(-np.arange(m) / (0.002 * SR)) * rng.uniform(0.2, 0.6)
    crack = bp(crack, 1500, 6000) * env
    return body * env * 0.8 + whoosh * 0.9 + crack * 0.9


op = sweep(0.8)
save("sinful_sweep_open", op)
cl = sweep(0.7)[::-1].copy()
n = len(cl); thump = np.sin(2 * np.pi * (55 + 40 * np.exp(-np.arange(int(0.25 * SR)) / SR / 0.04)) * np.arange(int(0.25 * SR)) / SR) * np.exp(-np.arange(int(0.25 * SR)) / SR / 0.07)
save("sinful_sweep_close", np.concatenate([cl, np.zeros(len(thump))]) + np.concatenate([np.zeros(n), thump * 0.6]))
