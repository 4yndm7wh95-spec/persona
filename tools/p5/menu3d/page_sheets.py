"""Make side-by-side sheets for the visible P5R menu entrance frames."""

from pathlib import Path

from PIL import Image, ImageDraw


MATCHES = {
    "skill": [
        (109, "near/cross_113_20", 0),
        (110, "near/cross_113_20", 2),
        (111, "near/cross_113_20", 4),
        (112, "near/cross_113_20", 5),
        *[(ref, "mid/main_0113", ref - 107) for ref in range(113, 122)],
    ],
    "item": [(178 + i, "main_0010", i + 1) for i in range(9)],
    "equip": [(234 + i, "main_0005", i + 2) for i in range(9)],
}

POSE_REFERENCES = {
    "skill": [(ref, clip, frame) for ref, clip, frame in MATCHES["skill"] if ref in {111, 113, 117, 120}],
    "item": [(180, "main_0010", 3), (184, "main_0010", 9), (186, "main_0010", 10)],
    "equip": [(236, "main_0005", 4), (240, "main_0005", 10), (242, "main_0005", 10)],
}

BACKGROUND = (67, 76, 89)
PANEL = (768, 432)
LABEL_HEIGHT = 34
GUTTER = 12


def write_sheet(page, matches, references, output, kind):
    width, height = PANEL
    row_height = height + LABEL_HEIGHT
    sheet = Image.new("RGB", (width * 2 + GUTTER, row_height * len(matches)), BACKGROUND)
    labels = ImageDraw.Draw(sheet)
    right = width + GUTTER
    for row, (reference, clip, sample) in enumerate(matches):
        preferred = references.parent / f"ref_3d_{reference:04d}.jpg"
        if kind == "pose_check" and preferred.is_file():
            reference_path = preferred
        else:
            reference_path, = references.glob(f"frame_{reference:04d}_*.jpg")
        filename = f"f{sample:04d}.png"
        render_path = output / page / clip / filename
        y = row * row_height
        with Image.open(reference_path) as source:
            sheet.paste(source.resize(PANEL, Image.Resampling.LANCZOS), (0, y + LABEL_HEIGHT))
        with Image.open(render_path) as source:
            render = source.resize(PANEL, Image.Resampling.LANCZOS)
            sheet.paste(render, (right, y + LABEL_HEIGHT), render.getchannel("A"))
        labels.text((12, y + 8), f"Reference  {reference_path.stem}", fill="white", font_size=16)
        labels.text((right + 12, y + 8), f"Render  {clip}/{filename}", fill="white", font_size=16)
    sheet.save(output / "sheets" / f"{page}_{kind}.png")


def main():
    repo = Path(__file__).resolve().parents[3]
    references = repo / "docs/p5/preview/p5r-20261121-223943-frames-02-12"
    output = repo / "research/p5r/menu3d_pages"
    (output / "sheets").mkdir(exist_ok=True)
    for page, matches in MATCHES.items():
        write_sheet(page, matches, references, output, "match")
        write_sheet(page, POSE_REFERENCES[page], references, output, "pose_check")


if __name__ == "__main__":
    main()
