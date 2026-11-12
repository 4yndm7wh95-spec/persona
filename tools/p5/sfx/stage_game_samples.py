"""Skill card sound palette from the game's own samples (user 2026-11-12: one card sound everywhere, many motions silent; sounds should be
quiet but present). Takes decoded Slay the Spire 2 samples (research/sts2_audio/samples, from the user's own game files), trims the
silence before the hit (so a sound can be played exactly on its frame), cuts the tail with a fade, normalizes to -6 dBFS peak, and writes
art/sfx_p5/game/<name>_<i>.ogg (several takes per name for variety). This script only writes those files."""
import glob, subprocess
from pathlib import Path
import numpy as np

ROOT = Path(__file__).resolve().parents[3]
SRC = ROOT / "research/sts2_audio/samples"
OUT = ROOT / "art/sfx_p5/game"
SR = 44100
# name: (sample glob, max takes, max length s)
PALETTE = {
    "gx_card_slide": ("*cardmvmt_slide*", 6, 0.4),
    "gx_card_stack": ("*cardmvmt_stack*", 6, 0.3),
    "gx_fan_open": ("*ui_deck_open*", 3, 0.4),
    "gx_fan_close": ("*ui_deck_close*", 3, 0.35),
    "gx_whoosh": ("*silent_attack_whoosh*", 4, 0.4),
    "gx_slice": ("*silent_attack_blade*", 4, 0.35),
    "gx_burn": ("*attack_fire_highend*", 3, 0.7),
    "gx_ping": ("*ui_settings_check_on*", 1, 0.15),
    "gx_pop": ("*Block_Break_Low*", 1, 0.6),
}

def load(f):
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", f, "-f", "s16le", "-ac", "2", "-ar", str(SR), "-"], capture_output=True, check=True).stdout
    return np.frombuffer(raw, np.int16).astype(np.float32).reshape(-1, 2) / 32768

OUT.mkdir(parents=True, exist_ok=True)
for name, (pat, takes, maxlen) in PALETTE.items():
    files = sorted(glob.glob(str(SRC / pat)))[:takes]
    for i, f in enumerate(files):
        x = load(f)
        m = np.abs(x).max(axis=1)
        pk = m.max()
        hit = int(np.argmax(m > 0.3 * pk))
        start = max(0, hit - int(0.008 * SR))
        y = x[start:start + int(maxlen * SR)].copy()
        fade = int(min(0.08, maxlen * 0.3) * SR)
        y[-fade:] *= np.linspace(1, 0, fade)[:, None]
        y[:int(0.002 * SR)] *= np.linspace(0, 1, int(0.002 * SR))[:, None]
        y = y / (np.abs(y).max() + 1e-9) * 0.5
        raw = (np.clip(y, -1, 1) * 32767).astype(np.int16).tobytes()
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "s16le", "-ar", str(SR), "-ac", "2", "-i", "-", "-c:a", "libvorbis", "-q:a", "5",
                        str(OUT / f"{name}_{i}.ogg")], input=raw, check=True)
    print(name, len(files))
