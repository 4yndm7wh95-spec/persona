"""Add the native Silent portrait paths to a standalone Godot 4 PCK."""

import argparse
import hashlib
from pathlib import Path
import re
import shutil
import struct
from typing import BinaryIO, NamedTuple


PORTRAITS = {
    "images/packed/character_select/char_select_silent.png": "cs_button",
    "images/ui/top_panel/character_icon_silent.png": "icon_top",
    "images/ui/top_panel/character_icon_silent_outline.png": "icon_top_outline",
}
HEADER = struct.Struct("<4s5I2Q")
ENTRY = struct.Struct("<QQ16sI")
UINT = struct.Struct("<I")
OFFSET = struct.Struct("<Q")


class Entry(NamedTuple):
    offset: int
    size: int
    digest: bytes
    flags: int


class Pack:
    def __init__(self, stream: BinaryIO):
        self.stream = stream
        magic, version, _, _, _, flags, self.base, directory = HEADER.unpack(stream.read(HEADER.size))
        if magic != b"GDPC" or version != 3 or flags != 2:
            raise ValueError("Expected a standalone unencrypted Godot PCK v3")
        stream.seek(directory)
        count, = UINT.unpack(stream.read(UINT.size))
        self.files: dict[str, Entry] = {}
        for _ in range(count):
            name_size, = UINT.unpack(stream.read(UINT.size))
            path = stream.read(name_size).rstrip(b"\0").decode("utf-8").removeprefix("res://")
            self.files[path] = Entry(*ENTRY.unpack(stream.read(ENTRY.size)))

    def read(self, path: str) -> bytes:
        entry = self.files[path]
        if entry.flags:
            raise ValueError(f"Unexpected flags for {path}")
        self.stream.seek(self.base + entry.offset)
        return self.stream.read(entry.size)


def aliases(game: Pack, joker: Pack) -> dict[str, bytes]:
    imports = {}
    for target, name in PORTRAITS.items():
        source = f"p5joker/fx/{name}.png"
        stock_import = game.read(target + ".import").decode("utf-8")
        uid = re.search(r'^uid="(uid://[^"]+)"', stock_import, re.MULTILINE)
        if uid is None:
            raise ValueError(f"No stock UID for {target}")
        original = joker.read(source + ".import").decode("utf-8").replace("\r", "")
        if 'type="CompressedTexture2D"\n' not in original or f'source_file="res://{source}"' not in original:
            raise ValueError(f"Unexpected import descriptor for {source}")
        remap = re.search(r'^path="res://([^"]+\.ctex)"', original, re.MULTILINE)
        if remap is None or remap[1] not in joker.files:
            raise ValueError(f"No imported texture for {source}")
        result = re.sub(r'^uid="[^"]*"\n', "", original, flags=re.MULTILINE)
        result = result.replace('type="CompressedTexture2D"\n', f'type="CompressedTexture2D"\nuid="{uid[1]}"\n', 1)
        result = result.replace(f'source_file="res://{source}"', f'source_file="res://{target}"', 1)
        imports[target + ".import"] = result.encode("utf-8")
    return imports


def align(stream: BinaryIO) -> None:
    stream.write(b"\0" * (-stream.tell() % 16))


def write_pack(game: Pack, joker: Pack, output: Path) -> None:
    imports = aliases(game, joker)
    files = dict(joker.files)
    for target, name in PORTRAITS.items():
        if target in files or target + ".import" in files:
            raise ValueError(f"Portrait path already present in source PCK: {target}")
        files[target] = files[f"p5joker/fx/{name}.png"]

    joker.stream.seek(0)
    with output.open("xb") as stream:
        shutil.copyfileobj(joker.stream, stream)
        for path, payload in imports.items():
            align(stream)
            files[path] = Entry(stream.tell() - joker.base, len(payload), hashlib.md5(payload).digest(), 0)
            stream.write(payload)
        align(stream)
        directory = stream.tell()
        stream.write(UINT.pack(len(files)))
        for path, entry in sorted(files.items()):
            raw_path = path.encode("utf-8")
            raw_path += b"\0" * (-len(raw_path) % 4)
            stream.write(UINT.pack(len(raw_path)))
            stream.write(raw_path)
            stream.write(ENTRY.pack(*entry))
        stream.seek(HEADER.size - OFFSET.size)
        stream.write(OFFSET.pack(directory))


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("game_pck", type=Path, help="Stock SlayTheSpire2.pck with the original texture UIDs")
    parser.add_argument("input_pck", type=Path, help="Newly packed P5Joker PCK")
    parser.add_argument("output_pck", type=Path, help="New P5Joker PCK with the SkinChanger portraits")
    args = parser.parse_args()
    with args.game_pck.open("rb") as game, args.input_pck.open("rb") as joker:
        write_pack(Pack(game), Pack(joker), args.output_pck)


if __name__ == "__main__":
    main()
