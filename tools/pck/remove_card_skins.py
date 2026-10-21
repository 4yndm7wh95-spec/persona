"""Remove retired card skins from a staging directory, including orphaned Godot imports.

Usage: python tools/pck/remove_card_skins.py --stage <stage> [--check]
Only retired private card-art paths, their imported products and dedicated fonts are touched.
"""
import argparse
import hashlib
import json
import re
from pathlib import Path

PREFIXES = ('p3makoto/cards/', 'p3makoto/card_frame/', 'p3makoto/approved_cards/', 'p3makoto/card_art_v4/')
EXACT = {'p3makoto/fx/pile_card_back.png', 'p3makoto/fx/pile_card_back.png.import',
         'p3makoto/fonts/Cinzel-Bold.ttf', 'p3makoto/fonts/LXGWWenKai-Bold-CardTitles.ttf',
         'p3makoto/fonts/OFL-Cinzel.txt', 'p3makoto/fonts/OFL-LXGWWenKai.txt'}

def retired_files(stage):
    stage = stage.resolve()
    files = {p.relative_to(stage).as_posix(): p for p in stage.rglob('*') if p.is_file()}
    retired = {rel for rel in files if rel.startswith(PREFIXES) or rel in EXACT}
    for rel in list(retired):
        if rel.endswith('.import'):
            for dest in re.findall(r'res://(\.godot/imported/[^"\n]+)', files[rel].read_text(encoding='utf-8')):
                if dest in files:
                    retired.add(dest)
    # Also remove imported products whose original PNG/.import was already absent.
    for rel in files:
        if not rel.startswith('.godot/imported/'):
            continue
        match = re.fullmatch(r'(.+\.png)-([a-f0-9]{32})(?:\.[a-z0-9]+)*\.ctex', Path(rel).name)
        if not match:
            continue
        name, digest = match.groups()
        candidates = [prefix + name for prefix in PREFIXES]
        if name == 'pile_card_back.png':
            candidates.append('p3makoto/fx/' + name)
        if any(hashlib.md5(('res://' + source).encode()).hexdigest() == digest for source in candidates):
            retired.add(rel)
    return sorted(retired)

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--stage', type=Path, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    retired = retired_files(args.stage)
    if args.check:
        if retired:
            raise SystemExit('Retired card resources remain: ' + json.dumps(retired))
        print('PASS: no retired card art, frames, card backs, fonts or imports')
    else:
        root = args.stage.resolve()
        for rel in retired:
            target = (root / rel).resolve()
            assert target.is_relative_to(root), target
            target.unlink()
        print(json.dumps({'removed': len(retired), 'files': retired}, ensure_ascii=False))
