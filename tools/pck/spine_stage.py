"""把一套 Spine 资源（json + atlas + png）写进 PCK 打包目录，全部是 spine-godot / Godot "导入后"的格式：
  <rel>/<base>.spine-json(.import) → .godot/imported/<base>.spine-json-<md5>.spjson（原始 JSON）
  <rel>/<base>.atlas(.import)      → .godot/imported/<base>.atlas-<md5>.spatlas（包着 atlas 文本的 JSON）
  <rel>/<base>.png(.import)        → .godot/imported/<base>.png-<md5>.ctex（godot_tex 生成）
  <rel>/<skel_data>.tres           → SpineSkeletonDataResource，按路径引用上面两个
替换原版资源时传入原版 uid（场景按 uid 引用也能拿到新资源）；新资源不传 uid。"""
import hashlib
import json
from pathlib import Path

from PIL import Image

from godot_tex import write_texture


def _h(path):
    return hashlib.md5(path.encode()).hexdigest()


def stage_spine(stage: Path, rel: str, base: str, src_dir: Path, skel_data_name: str,
                atlas_uid=None, png_uid=None, skel_data_uid=None, default_mix=0.1, mixes=()):
    d = stage / rel
    imp = stage / ".godot/imported"
    d.mkdir(parents=True, exist_ok=True)
    imp.mkdir(parents=True, exist_ok=True)

    atlas_text = (src_dir / f"{base}.atlas").read_text()
    (d / f"{base}.atlas").write_text(atlas_text)
    atlas_res = f"res://{rel}/{base}.atlas"
    atlas_imp = f"{base}.atlas-{_h(atlas_res)}.spatlas"
    (imp / atlas_imp).write_text(json.dumps({"atlas_data": atlas_text, "normal_texture_prefix": "n",
                                             "source_path": atlas_res, "specular_texture_prefix": "s"}, separators=(",", ":")))
    uid_line = f'uid="uid://{atlas_uid}"\n' if atlas_uid else ""
    (d / f"{base}.atlas.import").write_text(f"""[remap]

importer="spine.atlas"
type="SpineAtlasResource"
{uid_line}path="res://.godot/imported/{atlas_imp}"

[deps]

source_file="{atlas_res}"
dest_files=["res://.godot/imported/{atlas_imp}"]

[params]

normal_map_prefix="n"
specular_map_prefix="s"
""")

    skel = (src_dir / f"{base}.spine-json").read_bytes()
    (d / f"{base}.spine-json").write_bytes(skel)
    skel_res = f"res://{rel}/{base}.spine-json"
    skel_imp = f"{base}.spine-json-{_h(skel_res)}.spjson"
    (imp / skel_imp).write_bytes(skel)
    (d / f"{base}.spine-json.import").write_text(f"""[remap]

importer="spine.json"
type="SpineSkeletonFileResource"
path="res://.godot/imported/{skel_imp}"

[deps]

source_file="{skel_res}"
dest_files=["res://.godot/imported/{skel_imp}"]

[params]

""")

    write_texture(stage, f"{rel}/{base}.png", Image.open(src_dir / f"{base}.png"), png_uid)

    mix_res = "".join(f'\n[sub_resource type="SpineAnimationMix" id="mix{i}"]\nfrom = "{a}"\nto = "{b}"\nmix = {m}\n'
                      for i, (a, b, m) in enumerate(mixes))
    mix_list = ", ".join(f'SubResource("mix{i}")' for i in range(len(mixes)))
    uid_attr = f' uid="uid://{skel_data_uid}"' if skel_data_uid else ""
    (d / f"{skel_data_name}.tres").write_text(f"""[gd_resource type="SpineSkeletonDataResource" load_steps={3 + len(mixes)} format=3{uid_attr}]

[ext_resource type="SpineAtlasResource" path="{atlas_res}" id="1_atlas"]
[ext_resource type="SpineSkeletonFileResource" path="{skel_res}" id="2_skel"]
{mix_res}
[resource]
atlas_res = ExtResource("1_atlas")
skeleton_file_res = ExtResource("2_skel")
default_mix = {default_mix}
animation_mixes = [{mix_list}]
""")
    return d
