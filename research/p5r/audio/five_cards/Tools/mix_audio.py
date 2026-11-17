import math
import random
import subprocess
import sys
import wave
from array import array
from functools import lru_cache
from pathlib import Path


RATE = 48_000
DURATION = 14.2
FX = Path(__file__).resolve().parents[1] / "GodotProject/p5joker/sfx"
TAU = math.tau
mix = [0.0] * (round(DURATION * RATE) * 2)
rng = random.Random(1847)


def add(start, seconds, pan, signal):
    offset = round(start * RATE)
    count = min(round(seconds * RATE), len(mix) // 2 - offset)
    left = math.cos((pan + 1) * math.pi / 4)
    right = math.sin((pan + 1) * math.pi / 4)
    for index in range(count):
        x, y = signal(index)
        mix[(offset + index) * 2] += x * left
        mix[(offset + index) * 2 + 1] += y * right


@lru_cache(maxsize=None)
def decode(clip, speed):
    resample = f"aresample={RATE},asetrate={round(RATE * speed)},aresample={RATE}"
    raw = subprocess.check_output([
        "ffmpeg", "-nostdin", "-v", "error", "-i", str(FX / (clip + ".ogg")),
        "-af", resample, "-f", "f32le", "-ac", "2", "pipe:1"
    ])
    result = array("f")
    result.frombytes(raw)
    return result


def cue(clip, start, gain, pan=0.0, speed=1.0, max_time=1.4):
    audio = decode(clip, speed)
    length = min(len(audio) // 2, round(max_time * RATE))
    peak = max(0.1, max(abs(sample) for sample in audio[:length * 2]))
    volume = gain / peak

    def signal(index):
        fade = min(1.0, (index + 1) / 66, (length - index) / 1400)
        value = volume * fade
        return audio[index * 2] * value, audio[index * 2 + 1] * value

    add(start, length / RATE, pan, signal)


def tone(start, gain, hz, decay, pan=0.0, harmonics=0.35):
    def signal(index):
        t = index / RATE
        phase = TAU * (hz * t + 0.11 * (1.0 - math.exp(-32.0 * t)))
        attack = min(1.0, t * 1060.0)
        value = gain * attack * math.exp(-decay * t) * (
            math.sin(phase) + harmonics * math.sin(phase * 2.37)
        )
        return value, value

    add(start, min(1.0, 8 / decay), pan, signal)


def breath(start, seconds, gain, pan=0.0, climb=True):
    body = 0.0
    slow = 0.0

    def signal(index):
        nonlocal body, slow
        t = index / (seconds * RATE)
        env = math.sin(t * math.pi) ** 1.7
        ramp = t ** 2 if climb else (1.0 - t) ** 2
        noise = rng.uniform(-1.0, 1.0)
        body += (0.021 + ramp * 0.24) * (noise - body)
        slow += 0.014 * (body - slow)
        value = gain * env * (body - slow) * 3.4
        return value, value

    add(start, seconds, pan, signal)


cue("swish_04", 0.014, 0.12, -0.28, 1.27, 0.26)
cue("battle_gun_draw", 0.129, 0.21, -0.50, 1.03, 0.46)
tone(0.418, 0.060, 690, 32.0, 0.28)
tone(0.579, 0.078, 1005, 39.0, 0.41)
for i, (t, clip) in enumerate(zip(
    [0.652, 0.824, 0.995, 1.166, 1.388],
    ["gun_02", "gun_07", "gun_02", "gun_10", "gun_00"]
)):
    cue(clip, t, 0.42 if i < 4 else 0.56, -0.36, 1.00 + 0.022 * (i % 3), 0.46)
    cue("damage_enemy", t + 0.037, 0.19 if i < 4 else 0.33, 0.43, 1.20, 0.19)
    tone(t + 0.21, 0.042, 1750 + i * 91, 53.0, -0.45)
cue("sub_boom", 1.421, 0.30, 0.03, 1.04, 0.64)
tone(1.423, 0.26, 47, 10.0, 0.11)

cue("swish_05", 2.709, 0.14, -0.39, 1.22, 0.28)
cue("swish_02", 3.129, 0.19, -0.27, 1.15, 0.40)
breath(3.205, 0.51, 0.095, 0.06)
for i in range(3):
    enter = 3.21 + i * 0.126
    tone(enter + 0.05, 0.087, 945 + i * 160, 24.5, -0.08 + 0.22 * i)
    tone(enter + 0.38, 0.19, 1680 + i * 224, 40, 0.07 + 0.21 * i, 0.28)
    tone(4.35 + i * 0.076, 0.078, 1320 + i * 113, 37, 0.05 + 0.08 * i)
cue("sword_sheath", 4.188, 0.11, 0.14, 1.25, 0.42)

cue("swish_04", 5.201, 0.13, -0.28, 1.15, 0.26)
cue("persona_flame", 5.47, 0.28, -0.38, 1.00, 0.90)
cue("wing_large", 5.74, 0.22, -0.27, 1.20, 0.85)
breath(5.87, 0.29, 0.19, 0.17)
cue("curse_blast", 6.16, 0.41, 0.32, 0.94, 1.06)
cue("skill_ankoku", 6.188, 0.23, 0.26, 0.96, 1.13)
tone(6.17, 0.28, 49, 8.4, 0.27)
cue("curse_soft", 6.91, 0.15, 0.45, 0.91, 0.96)
breath(6.25, 0.89, 0.09, 0.36, False)

cue("swish_02", 8.426, 0.12, -0.29, 1.24, 0.30)
cue("encount_parry", 8.922, 0.30, 0.07, 1.18, 0.61)
cue("glass_burst", 8.935, 0.22, 0.11, 1.05, 0.52)
tone(8.924, 0.14, 554, 15.5, 0.16)
tone(8.98, 0.083, 837, 12.0, -0.08)
cue("swish_05", 9.735, 0.19, 0.35, 1.24, 0.31)
tone(9.87, 0.13, 1180, 20.0, 0.18)

cue("swish_04", 11.01, 0.12, -0.38, 1.24, 0.25)
cue("battle_gun_draw", 11.16, 0.20, -0.40, 0.97, 0.43)
breath(11.42, 0.32, 0.18, 0.05)
cue("sub_boom", 11.736, 0.26, 0.04, 1.15, 0.59)
tone(11.74, 0.25, 44, 8.1, -0.08)
tone(11.807, 0.053, 1402, 4.3, 0.33)
cue("glass_burst", 12.212, 0.23, 0.04, 1.35, 0.30)
for i in range(5):
    tone(12.208 + 0.008 * i, 0.066, 852 + i * 184, 25.0, -0.62 + i * 0.30)
breath(13.02, 0.38, 0.16, 0.16, False)
cue("swish_05", 13.9, 0.14, 0.19, 1.2, 0.24)

pcm = array("h", (round(29_800 * math.tanh(1.28 * x)) for x in mix))
with wave.open(sys.argv[1], "wb") as output:
    output.setparams((2, 2, RATE, 0, "NONE", "not compressed"))
    output.writeframes(pcm.tobytes())
