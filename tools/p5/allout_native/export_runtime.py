"""
P5R 总攻击收尾（BKSK_HERO_CTE，THE SHOW'S OVER）打成 mod 运行时读的一个二进制文件：p5mod/src/Vfx/AllOutFinale.cs 播放。
输入：godot/layers.json（六层原版网格、材质）、godot/native_matrices.json（每层每节点 240 帧局部矩阵，bake 工具按原版运算生成）、
godot/placements.json（外层世界矩阵）、godot/timeline.json（外层启停、颜色、透明度轨道）。规则与 godot/capture.gd 一致
（--native-cull --camera-bank --hide-lower-matte）：三角形 A,C,B 绕序、UV 矩阵、背面剔除、相机 Single_8 当 bank 角。

用法：python3 tools/p5/allout_native/export_runtime.py <输出 .bin>
格式（小端）：b"AOA1", fps(f32), frames(i32), fov, near, far, camera_layer(i32), camera_node(i32), bank[frames](f32), layers(i32)，每层：
  placement 12f（basis 三列 + 原点）、start f、end f、hidden(i32)、ortho(i32)、tint[frames] 4f（rgb + 不透明度）、
  nodes(i32)、parent[nodes] i32、materials(i32)×{rgba 4f, cull i32, tex 名(i32 长度 + utf8)}、
  meshes(i32)×{node i32, material i32, verts i32, xyz…, uvs i32, uv…, idx i32, index…}、frames×nodes×12f 局部矩阵。
"""
import json
import struct
import sys
from pathlib import Path

G = Path(__file__).resolve().parent / "godot"
FPS, FRAMES = 60.0, 240
HIDDEN = {4}            # 下底板：用户要求关闭（docs/p5/ALLOUT_IMPLEMENTATION.md）
ORTHO = {5}             # 竖排文字：独立正交相机


def scalar(keys, time, field):
    k = 0
    while k + 1 < len(keys) and keys[k + 1]["Time"] <= time + 1e-6:
        k += 1
    a, b = keys[k], keys[min(k + 1, len(keys) - 1)]
    f = min(max((time - a["Time"]) / (b["Time"] - a["Time"]), 0.0), 1.0) if b["Time"] > a["Time"] else 0.0
    return a[field] + (b[field] - a[field]) * f


def main(out: Path):
    layers = json.loads((G / "layers.json").read_text())
    native = json.loads((G / "native_matrices.json").read_text())
    places = json.loads((G / "placements.json").read_text())
    timeline = json.loads((G / "timeline.json").read_text())
    b = bytearray(b"AOA1")
    f = lambda *v: b.extend(struct.pack(f"<{len(v)}f", *v))
    i = lambda *v: b.extend(struct.pack(f"<{len(v)}i", *v))

    def s(text):
        raw = text.encode()
        i(len(raw))
        b.extend(raw)

    # 相机：layer 0 的 Camera01；bank 角按全局时间取 Single_8 轨道
    cam_names = native[0]["names"]
    cam_node = cam_names.index("Camera01")
    bank = [0.0] * FRAMES
    for ctl in layers[0]["AnimationPack"]["Animations"][0]["Controllers"]:
        if ctl.get("TargetKind") != "Camera":
            continue
        for tr in ctl["Layers"]:
            if tr["KeyType"] == "Single_8" and tr["Keys"]:
                bank = [scalar(tr["Keys"], n / FPS, "Value") for n in range(FRAMES)]
    f(FPS)
    i(FRAMES)
    f(26.231289, 10.0, 4000.0)
    i(0, cam_node)
    f(*bank)
    i(len(layers))
    for li, layer in enumerate(layers):
        m = places[li]
        f(m["X"]["X"], m["X"]["Y"], m["X"]["Z"], m["Y"]["X"], m["Y"]["Y"], m["Y"]["Z"], m["Z"]["X"], m["Z"]["Y"], m["Z"]["Z"],
          m["M41"], m["M42"], m["M43"])
        ev = timeline["layers"][li]
        f(ev["start"], ev["end"] + 1.0 / timeline["fps_field"])
        i(1 if li in HIDDEN else 0, 1 if li in ORTHO else 0)
        for n in range(FRAMES):
            t = max(0.0, n / FPS - ev["start"])
            tint, op = [1.0, 1.0, 1.0], 1.0
            for tr in ev["tracks"]:
                if not tr["Keys"]:
                    continue
                if tr["KeyType"] == "Single4Byte":
                    tint = [scalar(tr["Keys"], t, k) for k in ("Field00", "Field04", "Field08")]
                if tr["KeyType"] == "SingleByte":
                    op = scalar(tr["Keys"], t, "Field00")
            f(*tint, op)
        nat = native[li]
        names = nat["names"]
        i(len(names))
        i(*nat["parent"])
        mats = [p["Value"] for p in (layer.get("Materials") or [])]
        keys = [p["Key"] for p in (layer.get("Materials") or [])]
        i(len(mats))
        for mt in mats:
            c = mt["LegacyParameters"]["DiffuseColor"]
            f(c["X"], c["Y"], c["Z"], c["W"])
            i(1 if mt["DisableBackfaceCulling"] == 0 else 0)
            dm = mt.get("DiffuseMap")
            s(dm["Name"].replace(".dds", "") if dm else "")
        meshes = []

        def walk(node):
            idx = len(order)
            order.append(node["Name"])
            for a in node["Attachments"]:
                if a.get("$type") == "NodeMeshAttachment":
                    meshes.append((idx, a["Mesh"]))
            for c in node["Children"]:
                walk(c)

        order = []
        walk(layer["Model"]["RootNode"])
        assert order == names
        i(len(meshes))
        for idx, mesh in meshes:
            mi = keys.index(mesh["MaterialName"]) if mesh["MaterialName"] in keys else -1
            i(idx, mi)
            verts = mesh["Vertices"]
            i(len(verts))
            for p in verts:
                f(p["X"], p["Y"], p["Z"])
            uvs = mesh.get("TexCoordsChannel0") or []
            tm = mats[mi].get("DiffuseMap") if mi >= 0 else None
            out_uv = []
            for p in uvs:
                u, v = p["X"], p["Y"]
                if tm and int(tm["Flags"]) & 1:
                    u, v = (p["X"] * tm["Field4C"] + p["Y"] * tm["Field5C"] + tm["Field7C"],
                            p["X"] * tm["Field50"] + p["Y"] * tm["Field60"] + tm["Field80"])
                out_uv += [u, v]
            if len(uvs) != len(verts):
                out_uv = []
            i(len(out_uv) // 2)
            if out_uv:
                f(*out_uv)
            tris = []
            for t in mesh["Triangles"]:
                tris += [t["A"], t["C"], t["B"]]
            i(len(tris))
            i(*tris)
        for fr in nat["frames"]:
            for mat in fr:
                f(*mat)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_bytes(bytes(b))
    print(out, len(b), "bytes")


def splatter_textures():
    """BKSK_SPLATTER.EPL 里内嵌的三张 DDS（splatterBlood 的血团 smoke、滴落遮罩 upaura、噪声 test）→ (名字, PIL 图)。"""
    import io
    import re
    from PIL import Image
    raw = (Path(__file__).resolve().parent / "source/BKSK_SPLATTER.EPL").read_bytes()
    offs = [m.start() for m in re.finditer(rb"DDS \|", raw)]
    for k, o in enumerate(offs):
        name = re.findall(rb"([A-Za-z0-9_]+)\.dds", raw[max(0, o - 80):o])
        im = Image.open(io.BytesIO(raw[o:offs[k + 1] if k + 1 < len(offs) else len(raw)]))
        im.load()
        yield (name[-1].decode() if name else f"tex{k}"), im.convert("RGBA")


if __name__ == "__main__":
    main(Path(sys.argv[1]))
