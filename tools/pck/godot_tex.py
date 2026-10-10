"""在云端直接生成 Godot 4.5 的贴图导入产物（不需要本地 Godot 导入）。

CompressedTexture2D（.ctex，无损模式 compress/mode=0）的结构，按原版文件逐字节核对过：
  "GST2" | u32 version=1 | u32 width | u32 height | u32 flags=0x0D000000 | i32 mipmap_limit=-1 | u32 reserved×3
  | u32 data_format=2(WebP) | u16 width | u16 height | u32 mipmaps=0 | u32 image_format=5(RGBA8)
  | u32 size | <无损 WebP 字节>
导入后的文件名 = <文件名>-<md5(res 路径)>.ctex。沿用原版 uid，按 uid 引用这张图的场景就会拿到我们的图。
"""
import hashlib
import io
import struct
from pathlib import Path

from PIL import Image

IMPORT_TEMPLATE = """[remap]

importer="texture"
type="CompressedTexture2D"
{uid_line}path="res://.godot/imported/{dest}"
metadata={{
"vram_texture": false
}}

[deps]

source_file="res://{rel}"
dest_files=["res://.godot/imported/{dest}"]

[params]

compress/mode=0
compress/high_quality=false
compress/lossy_quality=1.0
compress/uastc_level=2
compress/rdo_quality_loss=0
compress/hdr_compression=0
compress/normal_map=0
compress/channel_pack=0
mipmaps/generate=false
mipmaps/limit=-1
roughness/mode=0
roughness/src_normal=""
process/channel_remap/red=0
process/channel_remap/green=1
process/channel_remap/blue=2
process/channel_remap/alpha=3
process/fix_alpha_border=false
process/premult_alpha=false
process/normal_map_invert_y=false
process/hdr_as_srgb=false
process/hdr_clamp_exposure=false
process/size_limit=0
detect_3d/compress_to=0
"""


def ctex_bytes(im: Image.Image) -> bytes:
    im = im.convert("RGBA")
    buf = io.BytesIO()
    im.save(buf, "WEBP", lossless=True, quality=100, method=4, exact=True)
    webp = buf.getvalue()
    w, h = im.size
    head = b"GST2" + struct.pack("<IIIIiIII", 1, w, h, 0x0D000000, -1, 0, 0, 0)
    head += struct.pack("<IHHII", 2, w, h, 0, 5)
    return head + struct.pack("<I", len(webp)) + webp


def read_ctex(data: bytes) -> Image.Image:
    assert data[:4] == b"GST2"
    fmt = struct.unpack("<I", data[36:40])[0]
    assert fmt == 2, f"only lossless WebP ctex supported, got data_format {fmt}"
    return Image.open(io.BytesIO(data[56:]))


def write_texture(stage: Path, rel: str, im: Image.Image, uid=None):
    """rel：res:// 之后的路径（如 images/ui/top_panel/character_icon_regent.png）。写 png + .import + .ctex。"""
    src = stage / rel
    src.parent.mkdir(parents=True, exist_ok=True)
    im.convert("RGBA").save(src)
    name = Path(rel).name
    dest = f"{name}-{hashlib.md5(('res://' + rel).encode()).hexdigest()}.ctex"
    imp = stage / ".godot/imported"
    imp.mkdir(parents=True, exist_ok=True)
    (imp / dest).write_bytes(ctex_bytes(im))
    (stage / (rel + ".import")).write_text(IMPORT_TEMPLATE.format(uid_line=f'uid="uid://{uid}"\n' if uid else "", dest=dest, rel=rel))
    return dest


if __name__ == "__main__":
    # 自检：解码原版 ctex → 重新编码 → 头部一致、像素一致
    import sys
    p = Path(sys.argv[1])
    raw = p.read_bytes()
    im = read_ctex(raw)
    again = ctex_bytes(im)
    assert again[:52] == raw[:52], (again[:52], raw[:52])
    assert read_ctex(again).convert("RGBA").tobytes() == im.convert("RGBA").tobytes()
    print("ctex round-trip OK", im.size)
