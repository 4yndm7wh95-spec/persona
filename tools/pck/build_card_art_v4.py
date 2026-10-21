"""Stage the user-selected v4 illustration set under private paths; never replace card frames.

Run from repository root:
  python tools/pck/build_card_art_v4.py --stage <existing validated stage>
Use --art-source <exported art/cards/v4> when another session is editing the artwork.
"""
import argparse
import hashlib
import json
import re
from pathlib import Path
from PIL import Image
from godot_tex import write_texture, read_ctex

REPO = Path(__file__).resolve().parents[2]

def stage_art(stage: Path, source: Path):
    files = sorted(source.glob('*.png')) + sorted((source / 'retained').glob('*.png'))
    cards = {p.stem: p for p in files}
    allowlist = set(re.findall(r'^\s*"([a-z0-9_]+)",$', (REPO/'mod/src/Scenes/ApprovedCardArt.cs').read_text(encoding='utf-8'), re.M))
    assert len(cards) == len(files) == 94 and cards.keys() == allowlist, 'Artwork / code allowlist mismatch'
    # Later, explicitly approved revision supersedes the v4 candidate for this card.
    cards['gather_light'] = REPO/'art/cards/approved/gather_light/gather_light.png'
    results = []
    for name, png in sorted(cards.items()):
        raw = png.read_bytes()
        with Image.open(png) as im:
            pixels = im.convert('RGBA')
            assert pixels.width >= 1000 and abs(pixels.width / pixels.height - 1000 / 760) < 0.005, (name, pixels.size)
            rel = f'p3makoto/card_art_v4/{name}.png'
            dest = write_texture(stage, rel, pixels)
            decoded = read_ctex((stage/'.godot/imported'/dest).read_bytes())
            assert decoded.convert('RGBA').tobytes() == pixels.tobytes(), name
        (stage/rel).write_bytes(raw)
        results.append(dict(id=name, sha256=hashlib.sha256(raw).hexdigest(), path=rel, size=list(pixels.size)))
    (stage/'p3makoto/card_art_v4/manifest.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
    print(f'PASS: {len(results)} illustrations, imported pixel checks, approved gather_light override; private paths only')

if __name__ == '__main__':
    args = argparse.ArgumentParser()
    args.add_argument('--stage',type=Path,required=True)
    args.add_argument('--art-source',type=Path,default=REPO/'art/cards/v4')
    opts = args.parse_args()
    stage_art(opts.stage, opts.art_source)

