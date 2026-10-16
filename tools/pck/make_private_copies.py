"""v0.14.48：把本 mod 覆盖原版储君路径的资源，在 res://p3makoto/rig/ 和 res://p3makoto/override/ 下各放一份"私有路径"的引用。
别的 mod（例如 Skin Changer 选「游戏默认」时）把原版路径换回原版文件后，运行时再用 Resource.TakeOverPath 把这些私有副本盖回原版路径（Core/AssetGuard.cs）。
只复制 .import（指向同一份 .godot/imported 里的导入文件，不重复占空间）和 .tres；图集的 .spatlas 需要另存一份（里面记着 source_path，贴图按它所在目录找）。
用法：python3 tools/pck/make_private_copies.py  （在仓库根目录；改了储君骨骼 / 贴图之后重跑）
"""
import json, os, re, shutil, hashlib

ROOT = os.path.join(os.path.dirname(__file__), "..", "..", "mod", "pck_stage")
IMP = os.path.join(ROOT, ".godot", "imported")

SETS = {
    # 原版目录 → 私有目录
    "animations/characters/regent": "p3makoto/rig/characters",
    "animations/rest_site/regent": "p3makoto/rig/rest_site",
}
IMAGES = [
    "images/packed/character_select/char_select_regent.png",
    "images/packed/character_select/char_select_regent_locked.png",
    "images/packed/map/icons/map_marker_regent.png",
    "images/ui/top_panel/character_icon_regent.png",
    "images/ui/top_panel/character_icon_regent_outline.png",
]
TRES = {
    "animations/characters/regent/regent_skel_data.tres": "p3makoto/rig/characters/regent_skel_data.tres",
    "animations/merchant/regent/regent_merchant_skel_data.tres": "p3makoto/rig/merchant/regent_merchant_skel_data.tres",
    "animations/rest_site/regent/rest_site_regent_skel_data.tres": "p3makoto/rig/rest_site/rest_site_regent_skel_data.tres",
}


def strip_uid(text):
    return re.sub(r'^uid="[^"]*"\n', "", text, flags=re.M)


def copy_import(src_rel, dst_rel):
    src = os.path.join(ROOT, src_rel + ".import")
    text = open(src, encoding="utf-8").read()
    text = strip_uid(text).replace(f'res://{src_rel}"', f'res://{dst_rel}"')
    if src_rel.endswith(".atlas"):
        # 图集：另存一份 .spatlas，source_path 指向私有目录（贴图按这个目录去找）
        m = re.search(r'path="res://\.godot/imported/([^"]+)"', text)
        old = os.path.join(IMP, m.group(1))
        data = json.load(open(old, encoding="utf-8"))
        data["source_path"] = f"res://{dst_rel}"
        name = os.path.basename(dst_rel) + "-p3private-" + hashlib.md5(dst_rel.encode()).hexdigest()[:12] + ".spatlas"
        json.dump(data, open(os.path.join(IMP, name), "w", encoding="utf-8"), ensure_ascii=False)
        text = text.replace(m.group(1), name)
    dst = os.path.join(ROOT, dst_rel + ".import")
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    open(dst, "w", encoding="utf-8").write(text)


for src_dir, dst_dir in SETS.items():
    for f in sorted(os.listdir(os.path.join(ROOT, src_dir))):
        if f.endswith(".import"):
            base = f[: -len(".import")]
            copy_import(f"{src_dir}/{base}", f"{dst_dir}/{base}")
    for f in sorted(os.listdir(os.path.join(ROOT, src_dir))):
        if f.endswith(".atlas"):
            # 图集文本很小，源文件也放一份（保险：万一 Spine 扩展按 source_path 重新读源文件）
            shutil.copyfile(os.path.join(ROOT, src_dir, f), os.path.join(ROOT, dst_dir, f))
for img in IMAGES:
    copy_import(img, "p3makoto/override/" + os.path.basename(img))
for src, dst in TRES.items():
    text = open(os.path.join(ROOT, src), encoding="utf-8").read()
    text = re.sub(r' uid="uid://[^"]*"', "", text, count=1)          # 只去掉文件头的 uid（私有副本不能和原版路径抢同一个 uid）
    for a, b in SETS.items():
        text = text.replace(f"res://{a}/", f"res://{b}/")
    os.makedirs(os.path.dirname(os.path.join(ROOT, dst)), exist_ok=True)
    open(os.path.join(ROOT, dst), "w", encoding="utf-8").write(text)
print("ok")
