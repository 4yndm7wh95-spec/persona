"""Joker gunshot SFX matching the opening volley of the Five Cards demo (research/p5r/audio/five_cards: Tools/mix_audio.py, reel_mix.wav),
split into single shots for the game (user 2026-11-09). Same layers, gains, pitch, pans, fades and soft clip as mix_audio.py:
  per shot i: cue(gun clip, t, 0.42, pan -0.36, speed 1 + 0.022*(i%3), max 0.46) + casing tick tone(t+0.21, 0.042, 1750+91i Hz, decay 53, pan -0.45)
              cue(damage_enemy, t+0.037, 0.19, pan 0.43, speed 1.2, max 0.19)
  last shot:  gun gain 0.56, hit gain 0.33, + sub_boom(t+0.033, 0.30, speed 1.04, max 0.64) + 47 Hz tone(t+0.035, 0.26, decay 10)
Outputs (art/sfx_p5/): gun_shot_1..4 (gun + casing tick, the demo's first four shots), gun_shot_final (gun_00 at the heavy gain + tick),
gun_hit (normal hit), gun_hit_final (heavy hit + sub + low tone, offsets kept relative to the hit), and five_cards_volley_check.wav
(the five shots re-mixed from these files at the demo times, to compare with reel_mix.wav)."""
import math, subprocess, sys
from pathlib import Path
import numpy as np

ROOT = Path(__file__).resolve().parents[3]
SRC = ROOT / "p5mod/pck_stage/p5joker/sfx"
OUT = ROOT / "art/sfx_p5"
RATE = 48_000


def decode(clip, speed):
    af = f"aresample={RATE},asetrate={round(RATE * speed)},aresample={RATE}"
    raw = subprocess.check_output(["ffmpeg", "-nostdin", "-v", "error", "-i", str(SRC / f"{clip}.ogg"), "-af", af, "-f", "f32le", "-ac", "2", "pipe:1"])
    return np.frombuffer(raw, np.float32).reshape(-1, 2).astype(np.float64)


def panlr(pan): return math.cos((pan + 1) * math.pi / 4), math.sin((pan + 1) * math.pi / 4)


def cue(buf, clip, start, gain, pan=0.0, speed=1.0, max_time=1.4):
    a = decode(clip, speed)
    n = min(len(a), round(max_time * RATE))
    peak = max(0.1, np.abs(a[:n]).max())
    i = np.arange(n)
    fade = np.minimum(np.minimum(1.0, (i + 1) / 66), (n - i) / 1400)
    sig = a[:n] * (gain / peak) * fade[:, None]
    l, r = panlr(pan)
    o = round(start * RATE)
    m = min(n, len(buf) - o)
    buf[o:o + m, 0] += sig[:m, 0] * l
    buf[o:o + m, 1] += sig[:m, 1] * r


def tone(buf, start, gain, hz, decay, pan=0.0, harmonics=0.35):
    n = round(min(1.0, 8 / decay) * RATE)
    t = np.arange(n) / RATE
    phase = 2 * math.pi * (hz * t + 0.11 * (1 - np.exp(-32 * t)))
    v = gain * np.minimum(1, t * 1060) * np.exp(-decay * t) * (np.sin(phase) + harmonics * np.sin(phase * 2.37))
    l, r = panlr(pan)
    o = round(start * RATE)
    m = min(n, len(buf) - o)
    buf[o:o + m, 0] += v[:m] * l
    buf[o:o + m, 1] += v[:m] * r


def clip_out(buf): return 29_800 / 32_768 * np.tanh(1.28 * buf)


def save(name, buf, ogg=True):
    y = clip_out(buf)
    nz = np.nonzero(np.abs(y).max(1) > 1e-4)[0]
    y = y[: nz[-1] + 1] if len(nz) else y
    raw = (np.clip(y, -1, 1) * 32767).astype(np.int16).tobytes()
    path = OUT / (f"{name}.ogg" if ogg else f"{name}.wav")
    args = ["-c:a", "libvorbis", "-q:a", "7"] if ogg else []
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "s16le", "-ar", str(RATE), "-ac", "2", "-i", "-", *args, str(path)], input=raw, check=True)
    print(name, round(len(y) / RATE, 3))


# User 2026-11-10: the shiv-card gunshot should be louder. The play volume in code is already at its ceiling (Sfx.DemoMixVolume),
# so the game files themselves are mixed hotter: gun + casing tick x SHOT_GAIN through the same soft clip (peak about -2.5 dB
# instead of -7.9). The hit files and the volley check keep the demo's own gains.
SHOT_GAIN = 1.8
TIMES = [0.652, 0.824, 0.995, 1.166, 1.388]
CLIPS = ["gun_02", "gun_07", "gun_02", "gun_10", "gun_00"]
for i, clip in enumerate(CLIPS):
    b = np.zeros((RATE, 2))
    cue(b, clip, 0.0, (0.42 if i < 4 else 0.56) * SHOT_GAIN, -0.36, 1.00 + 0.022 * (i % 3), 0.46)
    tone(b, 0.21, 0.042 * SHOT_GAIN, 1750 + i * 91, 53.0, -0.45)
    save(f"gun_shot_{i + 1}" if i < 4 else "gun_shot_final", b)
b = np.zeros((RATE, 2)); cue(b, "damage_enemy", 0.0, 0.19, 0.43, 1.20, 0.19); save("gun_hit", b)
b = np.zeros((RATE * 2, 2)); cue(b, "damage_enemy", 0.004, 0.33, 0.43, 1.20, 0.19)
cue(b, "sub_boom", 0.0, 0.30, 0.03, 1.04, 0.64); tone(b, 0.002, 0.26, 47, 10.0, 0.11); save("gun_hit_final", b)

# check: the demo's volley (0.6 .. 2.4 s) straight from mix_audio.py's recipe vs reel_mix.wav
full = np.zeros((round(2.4 * RATE), 2))
for i, (t, clip) in enumerate(zip(TIMES, CLIPS)):
    cue(full, clip, t, 0.42 if i < 4 else 0.56, -0.36, 1.00 + 0.022 * (i % 3), 0.46)
    cue(full, "damage_enemy", t + 0.037, 0.19 if i < 4 else 0.33, 0.43, 1.20, 0.19)
    tone(full, t + 0.21, 0.042, 1750 + i * 91, 53.0, -0.45)
cue(full, "sub_boom", 1.421, 0.30, 0.03, 1.04, 0.64); tone(full, 1.423, 0.26, 47, 10.0, 0.11)
save("five_cards_volley_check", full, ogg=False)
