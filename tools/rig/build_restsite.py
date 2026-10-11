"""休息处：结城理坐在篝火旁戴着耳机听歌（art/v5/rest_sit_a.png，单张立绘 + 加权网格）。
动画：呼吸（4s）+ 跟着节拍轻轻点头（90 BPM）+ 缓慢摇晃（8s），三个幕的循环动画用同一份数据，8 秒无缝循环。
契约（research/VISUAL_CONTRACTS.md §2）：直接子节点 SpineSprite 播 overgrowth_loop / hive_loop / glory_loop。
用法：python3 tools/rig/build_restsite.py [--no-preview]"""
import math
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools/rig"))
sys.path.insert(0, str(ROOT / "tools/pck"))
from build_rig import key_image  # noqa: E402
from mesh_rig import MeshRig, bake, write, preview  # noqa: E402
from spine_stage import stage_spine  # noqa: E402

SRC = ROOT / "art/v5/rest_sit_a.png"
OUT = ROOT / "art/rest/export"
LOOP = 8.0
BEAT = LOOP / 12                     # 90 BPM
UNIT = 0.6                           # 原图像素 → 骨骼单位（v0.9.1 游戏内反馈偏小：0.42 → 0.6，约放大 1.43 倍）


def build():
    rig = MeshRig(key_image(SRC), origin=(650, 935), unit=UNIT, step=20, tex_scale=0.95, name="makoto")
    rig.bone("chest", "root", (600, 690),
             polys=[[(250, 170), (720, 170), (740, 330), (700, 620), (560, 690), (330, 690), (250, 520)]], feather=90)
    rig.bone("head", "chest", (610, 255), circles=[(585, 120, 165)], polys=[[(540, 200), (680, 200), (690, 300), (540, 300)]], feather=50)
    rig.priority = ["head", "chest"]
    data = rig.setup()

    def pose(t):
        breath = math.sin(2 * math.pi * t / 4.0)
        sway = math.sin(2 * math.pi * t / LOOP)
        beat = (1 - math.cos(2 * math.pi * t / BEAT)) / 2               # 0→1→0 每拍一次
        nod = beat ** 2.2                                               # 落拍时更明显
        return {
            "chest": {"sy": 1 + 0.012 * breath, "sx": 1 - 0.004 * breath, "r": 0.5 * breath + 0.4 * sway},
            "head": {"r": -2.2 * nod + 1.2 * sway, "y": -1.2 * nod},
        }

    for name in ("overgrowth_loop", "hive_loop", "glory_loop"):
        bake(data, name, LOOP, pose, loop=True, slot="makoto")
    return rig, data


def main():
    rig, data = build()
    write(OUT, "restsite_regent", data, rig)
    # 替换原版：沿用原版 atlas / png / skel_data 的 uid（regent_rest_site.tscn 按 uid 引用 skel_data）
    stage_spine(ROOT / "mod/pck_stage", "animations/rest_site/regent", "restsite_regent", OUT, "rest_site_regent_skel_data",
                atlas_uid="bvadtv5xwjjqq", png_uid="lwg6bnvvhfdg", skel_data_uid="cpcw77otglq37", default_mix=0.2)
    print("staged rest site:", len(data["skins"][0]["attachments"]["makoto"]["makoto"]["uvs"]) // 2, "vertices")
    if "--no-preview" not in sys.argv:
        preview(data, rig, "glory_loop", ROOT / "art/rig_preview/restsite.gif", canvas=(700, 460), origin=(350, 420), scale=0.6)


if __name__ == "__main__":
    main()
