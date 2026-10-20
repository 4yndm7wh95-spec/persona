"""Build a small, same-outline font for pile UI; never modify the game font.

Usage: python build_pile_font.py <NotoSansCJKjp-Bold.otf>
Requires fonttools. The source is SIL OFL 1.1; the derivative is renamed.
"""
import re
import sys
from pathlib import Path

from fontTools import subset
from fontTools.ttLib import TTFont
from fontTools.pens.recordingPen import RecordingPen

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "mod/pck_stage/p3makoto/fonts/P3PileUI-Bold.otf"


def build(source):
    # Literal UI text (not comments) plus the already maintained card-name set.
    code = (ROOT / "mod/src/Vfx/PileFx.cs").read_text(encoding="utf-8-sig")
    code = re.sub(r"//[^\n]*|/\*.*?\*/", "", code, flags=re.S)
    words = re.findall(r'"([^"\n]*)"', code)
    cards = (ROOT / "mod/src/Scenes/CardFrameSkin.cs").read_text(encoding="utf-8-sig")
    words += re.findall(r'const string TitleChars\s*=\s*"([^"]*)"', cards)
    chars = set("".join(words)) | set(chr(n) for n in range(32, 127))
    original = TTFont(source)
    cmap = original.getBestCmap()
    wanted = {ord(c) for c in chars if ord(c) in cmap}
    options = subset.Options()
    options.name_IDs = [0, 1, 2, 3, 4, 5, 6, 13, 14, 16, 17]
    options.name_legacy = True
    options.name_languages = [0x409]
    options.layout_features = ["*"]
    options.recalc_timestamp = False
    worker = subset.Subsetter(options=options)
    worker.populate(unicodes=wanted)
    font = TTFont(source)
    worker.subset(font)
    for name in font["name"].names:
        if name.nameID in (1, 3, 4, 6, 16):
            value = "P3PileUI-Bold" if name.nameID == 6 else "P3 Pile UI Bold"
            name.string = value.encode(name.getEncoding())
    if "CFF " in font:
        cff = font["CFF "].cff
        cff.fontNames = ["P3PileUI-Bold"]
        cff.topDictIndex[0].FullName = "P3 Pile UI Bold"
        cff.topDictIndex[0].FamilyName = "P3 Pile UI"
    OUT.parent.mkdir(parents=True, exist_ok=True)
    font.save(OUT)

    # Verify coverage, metrics and every retained glyph outline against the source.
    result = TTFont(OUT)
    reduced = result.getBestCmap()
    assert wanted <= reduced.keys(), "subset lost requested characters"
    old_glyphs, new_glyphs = original.getGlyphSet(), result.getGlyphSet()
    for cp in wanted:
        old, new = cmap[cp], reduced[cp]
        a, b = RecordingPen(), RecordingPen()
        old_glyphs[old].draw(a)
        new_glyphs[new].draw(b)
        assert a.value == b.value, f"outline changed: U+{cp:04X}"
        assert original["hmtx"][old] == result["hmtx"][new], f"metrics changed: {cp}"
    license_source = ROOT / "mod/pck_stage/p3makoto/fonts/OFL-LXGWWenKai.txt"
    license_text = license_source.read_text(encoding="utf-8-sig")
    start = license_text.index("SIL OPEN FONT LICENSE")
    header = ("P3 Pile UI Bold is a subset of Noto Sans CJK JP Bold.\n"
              "Copyright 2014, 2015 Adobe Systems Incorporated (http://www.adobe.com/).\n"
              "Derivative family renamed to P3 Pile UI Bold; source glyphs preserved.\n\n")
    (OUT.parent / "OFL-P3PileUI.txt").write_text(header + license_text[start:], encoding="utf-8")
    print(f"{len(wanted)} characters; {OUT.stat().st_size} bytes; outlines/metrics verified")


if __name__ == "__main__":
    build(Path(sys.argv[1]))
