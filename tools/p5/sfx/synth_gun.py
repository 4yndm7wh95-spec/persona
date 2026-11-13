"""Gunshot candidates for Joker's revolver (user 2026-11-09: replace the current gun_10 + sub_boom). Writes art/sfx_p5/gun_cand_{a,b,c}.ogg."""
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
from synth_sinful import *          # noqa: F401,F403  (t, env, bp, lp, hp, reverb, save, rng, SR)
import numpy as np

def tail_echo(y, taps):
    for d, g in taps:
        n = int(d * SR); e = np.zeros_like(y); e[n:] = lp(y[:-n], 3000) * g; y = y + e
    return y

# A: crisp revolver - sharp crack, tight mid body, short slapback
x = t(0.9)
crack = hp(rng.standard_normal(len(x)), 2500) * env(x, 0.0003, 0.006)
body = bp(rng.standard_normal(len(x)), 300, 2500) * env(x, 0.001, 0.05)
thump = np.sin(2 * np.pi * (60 + 120 * np.exp(-x / 0.02)) * x) * env(x, 0.001, 0.07)
a = crack * 1.0 + np.tanh(2.5 * body) * 0.8 + thump * 0.9
save("gun_cand_a", reverb(tail_echo(a, [(0.07, 0.25), (0.15, 0.1)]), 0.5, 0.18))

# B: heavy magnum - bright crack, thick distorted blast, deep sub, metallic ring, longer tail
x = t(1.4)
crack = hp(rng.standard_normal(len(x)), 1800) * env(x, 0.0003, 0.01)
blast = np.tanh(4 * bp(rng.standard_normal(len(x)), 150, 1800) * env(x, 0.002, 0.11))
sub = np.sin(2 * np.pi * np.cumsum(40 + 90 * np.exp(-x / 0.05)) / SR) * env(x, 0.002, 0.22)
ring = sum(g * np.sin(2 * np.pi * f * x) for f, g in [(1250, 1), (1980, .6), (3110, .35)]) * env(x, 0.003, 0.18) * 0.12
b = crack + blast * 0.9 + sub * 1.3 + ring
save("gun_cand_b", reverb(tail_echo(b, [(0.11, 0.3), (0.25, 0.15), (0.42, 0.07)]), 1.0, 0.25))

# C: stylized P5 - snappy crack + "whip" swish + punchy sub + glassy sparkle
x = t(1.0)
crack = hp(rng.standard_normal(len(x)), 3000) * env(x, 0.0002, 0.005)
f = 5000 * np.exp(-x / 0.03) + 400
whip = np.sin(2 * np.pi * np.cumsum(f) / SR) * env(x, 0.0005, 0.03) * 0.35
punch = np.sin(2 * np.pi * np.cumsum(55 + 160 * np.exp(-x / 0.015)) / SR) * env(x, 0.001, 0.09)
spark = hp(rng.standard_normal(len(x)), 6000) * env(np.maximum(x - 0.01, 0), 0.002, 0.08) * (rng.random(len(x)) < 0.15) * 0.5
c = crack * 1.1 + whip + punch * 1.2 + np.tanh(3 * bp(rng.standard_normal(len(x)), 400, 3000) * env(x, 0.001, 0.035)) * 0.6 + spark
save("gun_cand_c", reverb(tail_echo(c, [(0.09, 0.2)]), 0.6, 0.2))
