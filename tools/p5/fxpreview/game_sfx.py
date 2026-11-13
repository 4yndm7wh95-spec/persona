"""Preview only: find the decoded game samples (research/sts2_audio/samples/<sample name>.ogg|.wav) for a game FMOD event path, so the
preview's mix can play an approximation of the event (user 2026-11-11: let me hear the game sounds in the preview). Nothing here goes
into the mod.

The banks (research/sts2_audio/banks, from the user's SlayTheSpire2.pck) hold one FSB5 sample bank each. Events are not parsed - they are
matched to samples by name: the event's last path segment (e.g. scroll_of_biting_bite) against the sample names with the prefixes
sts2_sfx_ / act3_ / E-A1_ ... removed, preferring names that start with it, skipping voice lines (VO) unless nothing else matches.
Variants (rr1, rr2 ...) are alternatives - the first is taken; layers (layer1, layer2 ...) play together.
Usage: python3 game_sfx.py event:/sfx/buff ...   (prints the samples it would use)"""
import re
import struct
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
BANKS = ROOT / "research/sts2_audio/banks"
SAMPLES = ROOT / "research/sts2_audio/samples"


def sample_names():
    names = []
    for bank in sorted(BANKS.glob("*.bank")):
        d = bank.read_bytes()
        o = d.find(b"FSB5")
        if o < 0:
            continue
        _ver, ns, shs, _nts, _ds, _mode = struct.unpack_from("<6I", d, o + 4)
        nt = o + 0x3C + shs
        for x in struct.unpack_from(f"<{ns}I", d, nt):
            e = d.index(b"\0", nt + x)
            names.append(d[nt + x:e].decode("utf-8", "replace"))
    return names


def norm(n):
    n = n.lower().replace("-", "_")
    n = re.sub(r"^sts2_sfx_", "", n)
    n = re.sub(r"^(act\d+[ab]?|e_a\d+[ab]?)_", "", n)
    return n.replace("rubyraiders_crossbow", "crossbow_ruby_raider_").replace("__", "_")


def variant_key(n):
    m = re.search(r"rr(\d+)", n)
    return (int(m.group(1)) if m else 0, n)


# events whose sample names do not contain the event name
ALIAS = {"enchant_shimmer": "enchant_v2_tonal_shimmer", "gold_1": "gold_few", "gold_2": "gold_medium", "gold_3": "gold_many"}


def resolve(event, names=None):
    names = names if names is not None else sample_names()
    tok = event.rstrip("/").split("/")[-1].lower()
    tok = ALIAS.get(tok, tok)
    if tok.startswith("the_"):
        tok = tok[4:]
    cands = [(n, norm(n)) for n in names]
    pick = [n for n, k in cands if k.startswith(tok + "_") or k == tok]
    if not pick:
        pick = [n for n, k in cands if tok in k]
    if not pick:
        # crossbow_ruby_raider_reload -> crossbowreload style names
        squash = tok.replace("_", "")
        pick = [n for n, k in cands if squash in k.replace("_", "")]
    non_vo = [n for n in pick if "_vo_" not in "_" + norm(n) and not norm(n).startswith("vo_")]
    pick = non_vo or pick
    layers = {}
    for n in pick:
        m = re.search(r"layer(\d+)", n.lower())
        layers.setdefault(m.group(1) if m else "", []).append(n)
    return [sorted(v, key=variant_key)[0] for _, v in sorted(layers.items())]


def files(event, names=None):
    out = []
    for n in resolve(event, names):
        for ext in (".ogg", ".wav"):
            f = SAMPLES / f"{n}{ext}"
            if f.exists():
                out.append(f)
                break
    return out


if __name__ == "__main__":
    ns = sample_names()
    for ev in sys.argv[1:]:
        print(ev, "->", resolve(ev, ns), "files:", [f.name for f in files(ev, ns)])
