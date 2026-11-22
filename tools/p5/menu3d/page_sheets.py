"""Make side-by-side sheets for the visible P5R menu entrance frames."""

from pathlib import Path

from PIL import Image, ImageDraw


MATCHES = {
    "skill": [
        (109, 15, 0),
        (110, 15, 1),
        (111, 15, 3),
        (112, 15, 4),
        (113, 15, 5),
        (114, 16, 0),
        (115, 16, 0),
        (116, 16, 0),
        (117, 16, 1),
        (118, 16, 1),
        (119, 16, 1),
        (120, 17, 0),
        (121, 17, 1),
    ],
    "item": [(178 + i, 10, i + 1) for i in range(9)],
    "equip": [(234 + i, 5, i + 2) for i in range(9)],
}

BACKGROUND = (67, 76, 89)
PANEL = (768, 432)
LABEL_HEIGHT = 34
GUTTER = 12


def write_sheet(page, matches, references, output):
    width, height = PANEL
    row_height = height + LABEL_HEIGHT
    sheet = Image.new("RGB", (width * 2 + GUTTER, row_height * len(matches)), BACKGROUND)
    labels = ImageDraw.Draw(sheet)
    right = width + GUTTER
    for row, (reference, clip_id, sample) in enumerate(matches):
        reference_path, = references.glob(f"frame_{reference:04d}_*.jpg")
        clip = f"main_{clip_id:04d}"
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
    sheet.save(output / "sheets" / f"{page}_match.png")


def main():
    repo = Path(__file__).resolve().parents[3]
    references = repo / "docs/p5/preview/p5r-20261121-223943-frames-02-12"
    output = repo / "research/p5r/menu3d_pages"
    (output / "sheets").mkdir(exist_ok=True)
    for page, matches in MATCHES.items():
        write_sheet(page, matches, references, output)


if __name__ == "__main__":
    main()
