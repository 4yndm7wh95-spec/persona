"""Synthesized SFX for the skill / power card effects (p5mod/src/Vfx/SkillFx.cs), user 2026-11-10: the cards that only had a
character action get effects and sounds. Rules (docs/p5/FX_RULES.md): short and clean for cards played all the time, nothing harsh
(energy kept under ~5 kHz), one sound per effect.
Writes art/sfx_p5/: card_flick, card_burn, bullet_load, buff_up, buff_dark, debuff_down, energy_surge, wail, stealth_veil, time_slow,
hex_guard, shadow_mark (44.1 kHz stereo ogg). This script only writes these files."""
import subprocess
from pathlib import Path

import numpy as np
from scipy import signal

SR = 44100
OUT = Path(__file__).resolve().parents[3] / "art/sfx_p5"
rng = np.random.default_rng(1110)


def t(sec): return np.arange(int(sec * SR)) / SR
def env(x, a, d): return np.minimum(1, x / max(a, 1e-4)) * np.exp(-np.maximum(0, x - a) / d)
def bp(x, lo, hi, order=2): return signal.sosfilt(signal.butter(order, [lo, hi], "band", fs=SR, output="sos"), x)
def lp(x, f, order=2): return signal.sosfilt(signal.butter(order, f, "low", fs=SR, output="sos"), x)
def hp(x, f, order=2): return signal.sosfilt(signal.butter(order, f, "high", fs=SR, output="sos"), x)
def nz(x): return rng.standard_normal(len(x))
def at(x, t0): return np.maximum(x - t0, 0.0)
def on(x, t0): return (x >= t0).astype(float)
def chirp(x, f0, f1, dur):
    k = np.clip(x / dur, 0, 1)
    f = f0 * (f1 / f0) ** k
    return np.sin(2 * np.pi * np.cumsum(f) / SR), f


def sweep_noise(x, f0, f1, dur, q=0.4):
    out = np.zeros_like(x); n = nz(x); zi = None
    for i in range(0, len(x), 256):
        k = min(1.0, x[i] / dur); fc = f0 * (f1 / f0) ** k
        sos = signal.butter(2, [fc * (1 - q), min(fc * (1 + q), SR / 2 - 200)], "band", fs=SR, output="sos")
        if zi is None: zi = np.zeros((sos.shape[0], 2))
        out[i:i + 256], zi = signal.sosfilt(sos, n[i:i + 256], zi=zi)
    return out


def room(x, decay=0.5, mix=0.14):
    n = int(decay * SR)
    ir = lp(rng.standard_normal(n) * np.exp(-np.arange(n) / (decay * SR / 5)), 4500)
    wet = signal.fftconvolve(x, ir); wet /= np.abs(wet).max() + 1e-9
    y = np.zeros(len(x) + n); y[:len(x)] += x * (1 - mix); y[:len(wet)] += wet[:len(y)] * mix * np.abs(x).max()
    return y


def save(name, x, peak=0.8, width=0.0015):
    x = lp(x, 6500, 4)                                    # nothing harsh
    x = x / (np.abs(x).max() + 1e-9) * peak
    fade = int(0.012 * SR); x[-fade:] *= np.linspace(1, 0, fade)
    d = int(width * SR)
    st = np.stack([x, np.concatenate([np.zeros(d), x[:-d]]) if d else x], 1)
    raw = (np.clip(st, -1, 1) * 32767).astype(np.int16).tobytes()
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "s16le", "-ar", str(SR), "-ac", "2", "-i", "-", "-c:a", "libvorbis", "-q:a", "6",
                    str(OUT / f"{name}.ogg")], input=raw, check=True)
    print(name, round(len(x) / SR, 2))


# card_flick: a card snapped off the thumb - a dry paper tick, a softer second tick as it leaves, a breath of air
x = t(0.16)
y = bp(nz(x), 1800, 6000) * env(x, 0.0008, 0.012) * 1.0
y += bp(nz(x), 1200, 4500) * env(at(x, 0.028), 0.0008, 0.018) * on(x, 0.028) * 0.55
y += sweep_noise(x, 900, 3200, 0.09) * env(x, 0.02, 0.04) * 0.25
save("card_flick", y, 0.7)

# card_burn: the flick, then the card burning away - a soft "fff" with sparse crackles
x = t(0.55)
y = bp(nz(x), 1500, 5000) * env(x, 0.001, 0.014) * 0.8
y += sweep_noise(at(x, 0.05), 2600, 700, 0.4) * env(at(x, 0.05), 0.05, 0.16) * on(x, 0.05) * 0.5
cr = np.zeros_like(x)
for _ in range(22):
    k = int(rng.uniform(0.1, 0.45) * SR); n = int(0.004 * SR)
    cr[k:k + n] += rng.standard_normal(n) * np.exp(-np.arange(n) / (0.001 * SR)) * rng.uniform(0.2, 0.6) * np.exp(-(k / SR - 0.1) / 0.2)
y += bp(cr, 1800, 5500) * 0.7
save("card_burn", room(y, 0.3, 0.1), 0.7)

# bullet_load: one round dropped back into the hand / chamber - a small bright metal click with a short ring and a tiny thud
x = t(0.16)
y = bp(nz(x), 1600, 5500) * env(x, 0.0004, 0.006) * 1.0
y += bp(nz(x), 2200, 5000) * env(at(x, 0.011), 0.0004, 0.005) * on(x, 0.011) * 0.6
y += sum(a * np.sin(2 * np.pi * f * x + f) for f, a in [(2930, 1.0), (4310, 0.5), (1980, 0.4)]) * env(x, 0.001, 0.035) * 0.22
y += np.sin(2 * np.pi * 190 * x) * env(x, 0.002, 0.03) * 0.5
save("bullet_load", y, 0.75)

# buff_up: light winding up around the body - a rising airy tone (two detuned partials + fifth), a breath of air, a small bright ting at the top
x = t(0.7)
c1, f = chirp(x, 420, 1260, 0.42)
c2, _ = chirp(x, 424, 1272, 0.42)
c3, _ = chirp(x, 630, 1890, 0.42)
tone = (c1 + c2) * 0.5 + c3 * 0.3
y = tone * env(x, 0.08, 0.2) * (0.75 + 0.25 * np.sin(2 * np.pi * 11 * x)) * 0.5
y += sweep_noise(x, 700, 3600, 0.42, 0.5) * env(x, 0.12, 0.16) * 0.3
ting = sum(a * np.sin(2 * np.pi * f0 * at(x, 0.37)) for f0, a in [(2093, 1.0), (3136, 0.5), (4186, 0.25)]) * env(at(x, 0.37), 0.002, 0.11) * on(x, 0.37)
y += ting * 0.35
save("buff_up", room(y, 0.5, 0.18), 0.7)

# buff_dark: the same gesture for curse / shadow buffs - a low growl rising, dark air, a soft thump instead of the ting
x = t(0.7)
c1, _ = chirp(x, 95, 240, 0.45)
c2, _ = chirp(x, 143, 362, 0.45)
y = np.tanh(2.2 * (c1 + 0.6 * c2)) * env(x, 0.1, 0.22) * (0.7 + 0.3 * np.sin(2 * np.pi * 7 * x)) * 0.55
y += lp(sweep_noise(x, 300, 1500, 0.45, 0.6), 2200) * env(x, 0.14, 0.18) * 0.5
y += np.sin(2 * np.pi * np.cumsum(55 + 50 * np.exp(-at(x, 0.38) / 0.05)) / SR) * env(at(x, 0.38), 0.004, 0.14) * on(x, 0.38) * 0.9
save("buff_dark", room(y, 0.5, 0.16), 0.75)

# debuff_down: pressure coming down on an enemy - a falling, beating pair of tones, dark air falling with it, a dull thud on the ground
x = t(0.62)
c1, _ = chirp(x, 880, 230, 0.34)
c2, _ = chirp(x, 905, 222, 0.34)
y = (c1 + c2) * 0.5 * env(x, 0.02, 0.17) * 0.5
y += lp(sweep_noise(x, 2400, 400, 0.34, 0.5), 3000) * env(x, 0.03, 0.14) * 0.4
y += np.sin(2 * np.pi * np.cumsum(48 + 60 * np.exp(-at(x, 0.3) / 0.05)) / SR) * env(at(x, 0.3), 0.004, 0.16) * on(x, 0.3) * 1.0
save("debuff_down", room(y, 0.4, 0.14), 0.75)

# energy_surge: air pulled in to the chest (0.28 s), then a soft bright hit - two ringing partials over a sub thump
x = t(0.75)
y = sweep_noise(x, 500, 3800, 0.28, 0.5) * (np.clip(x / 0.28, 0, 1) ** 2.2) * (x < 0.29) * 0.7
h = at(x, 0.28)
y += sum(a * np.sin(2 * np.pi * f0 * h) for f0, a in [(1568, 1.0), (2349, 0.6), (3136, 0.3)]) * env(h, 0.002, 0.16) * on(x, 0.28) * 0.4
y += np.sin(2 * np.pi * np.cumsum(50 + 70 * np.exp(-h / 0.05)) / SR) * env(h, 0.004, 0.18) * on(x, 0.28) * 1.0
y += bp(nz(x), 800, 4000) * env(h, 0.001, 0.03) * on(x, 0.28) * 0.5
save("energy_surge", room(y, 0.5, 0.16), 0.8)

# wail: a shriek pushed out of the mask - a voiced tone with fast vibrato sliding down through two formants, breath noise, pressure under it
x = t(0.7)
f = 760 * (0.72 ** np.clip(x / 0.5, 0, 1)) * (1 + 0.035 * np.sin(2 * np.pi * 27 * x))
ph = 2 * np.pi * np.cumsum(f) / SR
voice = np.sin(ph) + 0.5 * np.sin(2 * ph) + 0.33 * np.sin(3 * ph) + 0.2 * np.sin(4 * ph)
voice = bp(voice, 600, 1400) * 1.0 + bp(voice, 2000, 3200) * 0.7
y = np.tanh(1.8 * voice) * env(x, 0.03, 0.2) * 0.6
y += bp(nz(x), 1200, 4200) * env(x, 0.02, 0.16) * 0.28
y += np.sin(2 * np.pi * 70 * x) * env(x, 0.03, 0.2) * 0.5
save("wail", room(y, 0.8, 0.22), 0.8)

# stealth_veil: a body slipping into shadow - air closing in (a soft falling whoosh) and a low breath
x = t(0.6)
y = lp(sweep_noise(x, 2600, 350, 0.4, 0.55), 3200) * env(x, 0.06, 0.16) * 0.8
y += np.sin(2 * np.pi * np.cumsum(120 - 60 * np.clip(x / 0.4, 0, 1)) / SR) * env(x, 0.05, 0.2) * 0.5
save("stealth_veil", room(y, 0.5, 0.15), 0.7)

# time_slow: three clock ticks, each later and lower than the last, over a tone dragging down like a tape stopping
x = t(1.1)
y = np.zeros_like(x)
for t0, f0, a in [(0.0, 2400, 1.0), (0.17, 1900, 0.85), (0.42, 1400, 0.7)]:
    h = at(x, t0)
    y += (bp(nz(x), f0 * 0.7, f0 * 1.5) * env(h, 0.0005, 0.008) + np.sin(2 * np.pi * f0 * h) * env(h, 0.001, 0.03) * 0.5) * on(x, t0) * a
drag, _ = chirp(x, 330, 70, 0.85)
y += np.tanh(1.5 * drag) * env(x, 0.05, 0.45) * 0.4
y += lp(nz(x), 600) * env(x, 0.2, 0.35) * 0.35
save("time_slow", room(y, 0.7, 0.2), 0.75)

# hex_guard: the honeycomb shield snapping up cell by cell - a run of small glassy ticks climbing, a short glass hum, a soft low "whum"
x = t(0.5)
y = np.zeros_like(x)
for i in range(7):
    t0 = 0.012 * i + 0.004 * i * i * 0.3
    f0 = 1700 + 290 * i
    h = at(x, t0)
    y += np.sin(2 * np.pi * f0 * h + i) * env(h, 0.0008, 0.022) * on(x, t0) * (0.5 + 0.07 * i)
hum = sum(a * np.sin(2 * np.pi * f0 * x) for f0, a in [(1244, 1.0), (1865, 0.6), (2489, 0.35)])
y += hum * env(x, 0.06, 0.13) * 0.22
y += np.sin(2 * np.pi * np.cumsum(70 + 50 * np.exp(-x / 0.06)) / SR) * env(x, 0.005, 0.14) * 0.8
save("hex_guard", room(y, 0.4, 0.15), 0.7)

# shadow_mark: a target marked from both sides - two short dry ticks closing in and a low lock
x = t(0.35)
y = bp(nz(x), 1500, 4500) * env(x, 0.0006, 0.01) * 0.8
y += bp(nz(x), 1500, 4500) * env(at(x, 0.09), 0.0006, 0.01) * on(x, 0.09) * 0.9
h = at(x, 0.16)
y += (np.sin(2 * np.pi * 660 * h) + 0.5 * np.sin(2 * np.pi * 990 * h)) * env(h, 0.002, 0.07) * on(x, 0.16) * 0.5
y += np.sin(2 * np.pi * 85 * h) * env(h, 0.003, 0.08) * on(x, 0.16) * 0.7
save("shadow_mark", y, 0.7)
