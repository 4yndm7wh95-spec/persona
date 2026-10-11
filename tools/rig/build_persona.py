"""Persona：Thanatos（art/v5/thanatos_a.png，单张立绘 + 加权网格骨骼）。
游戏里由 mod 代码在出"君王之刃"时召唤：summon 0.3s（从下方升起、显形、棺材环张开）→ attack 0.7s（深蓄力 → 挥剑斩下，slash 事件 0.18s = 命中帧）→ dismiss 0.45s（上浮消散）。
idle_loop 用于以后的常驻展示。
骨骼（原图像素）：剑臂以肩为轴、剑以手为轴（挥砍时手腕反向翻转，剑刃最终斜指右下）；爪臂、头、左右两组棺材、披风两节、腿。
用法：python3 tools/rig/build_persona.py [--no-preview]"""
import math
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools/rig"))
sys.path.insert(0, str(ROOT / "tools/pck"))
from build_rig import key_image  # noqa: E402
from mesh_rig import MeshRig, bake, keys, out_cubic, in_cubic, out_back, write, preview  # noqa: E402
from spine_stage import stage_spine  # noqa: E402

SRC = ROOT / "art/v5/thanatos_a.png"
OUT = ROOT / "art/persona/export"
RES_DIR = "p3makoto/persona"
SLOTS = ["thanatos", "thanatos_blade"]


def build():
    rig = MeshRig(key_image(SRC), origin=(545, 735), unit=1.0, step=22, tex_scale=0.6, name="thanatos")
    rig.bone("chest", "root", (580, 560),
             polys=[[(460, 420), (720, 420), (740, 650), (600, 740), (460, 660)]], feather=80)
    rig.bone("head", "chest", (630, 545), circles=[(640, 495, 80)], feather=30)
    ARM = [(360, 40), (470, 40), (565, 420), (505, 500), (440, 470), (395, 250)]
    BLADE = [(470, 92), (900, 413), (1008, 511), (996, 534), (900, 471), (600, 269), (470, 224)]   # 按像素量出的剑刃上下沿
    HILT = [(266, 18), (334, 18), (472, 138), (472, 226), (380, 182), (266, 64)]                     # 剑柄 + 剑首
    rig.bone("sword_arm", "chest", (500, 445), polys=[ARM], feather=26)
    rig.bone("sword", "sword_arm", (425, 140), polys=[BLADE, HILT], circles=[(410, 100, 42)], feather=6, grow=26)
    rig.bone("claw_arm", "chest", (700, 585),
             polys=[[(685, 555), (775, 555), (955, 760), (955, 930), (800, 930), (760, 720), (700, 645)]], feather=26)
    rig.bone("coffins_l", "chest", (560, 520),
             polys=[[(10, 160), (480, 160), (480, 330), (350, 565), (210, 655), (10, 650)]], feather=26)
    rig.bone("coffins_r", "chest", (590, 520),
             polys=[[(530, 255), (995, 255), (995, 870), (880, 870), (780, 640), (700, 565), (560, 420)]], feather=26)
    rig.bone("legs", "root", (560, 860),
             polys=[[(470, 820), (720, 820), (700, 1100), (420, 1470), (300, 1470), (400, 1100)]], feather=50)
    rig.bone("cape_l1", "root", (460, 700),
             polys=[[(0, 600), (470, 640), (520, 900), (360, 1470), (0, 1470)]], feather=70)
    rig.bone("cape_l2", "cape_l1", (260, 1080),
             polys=[[(0, 960), (430, 960), (340, 1470), (0, 1470)]], feather=90)
    rig.bone("cape_r", "root", (760, 900),
             polys=[[(640, 820), (970, 820), (970, 1220), (690, 1220)]], feather=60)
    rig.priority = ["sword", "sword_arm", "claw_arm", "head", "coffins_l", "coffins_r", "legs",
                    "cape_l2", "cape_l1", "cape_r", "chest"]
    # 剑臂会大幅挥动并扫过棺材：切成独立的一块画在最上面；身体上的切口扩散填补，身体不受剑臂骨骼影响
    rig.cut("thanatos_blade", [ARM, BLADE, HILT], base=(505, 440), fade=(20, 80), only=["sword", "sword_arm"], residual="sword_arm")
    rig.body_only = [b for b in rig.priority if b not in ("sword", "sword_arm")]
    data = rig.setup(events=["slash"])

    def flutter(t, amp=1.0):
        w = 2 * math.pi * t
        return {
            "cape_l1": {"r": amp * 3.0 * math.sin(w / 1.6)},
            "cape_l2": {"r": amp * 5.0 * math.sin(w / 1.6 - 0.9)},
            "cape_r": {"r": amp * 3.5 * math.sin(w / 1.3 + 0.7)},
            "coffins_l": {"r": amp * 2.2 * math.sin(w / 2.0)},
            "coffins_r": {"r": -amp * 2.2 * math.sin(w / 2.0 + 0.5)},
            "claw_arm": {"r": amp * 2.5 * math.sin(w / 1.8 + 1.1)},
            "head": {"r": amp * 1.5 * math.sin(w / 2.0 + 0.3)},
        }

    def merge(*poses):
        out = {}
        for p in poses:
            for b, ch in p.items():
                e = out.setdefault(b, {})
                for k, v in ch.items():
                    e[k] = e.get(k, 1.0 if k in ("sx", "sy") else 0.0) * v if k in ("sx", "sy") else e.get(k, 0.0) + v
        return out

    # 召唤：从下方升起、放大、显形；棺材环先收拢再张开
    # 君王之刃的伤害结算被 mod 推迟到出牌后约 0.62s（见 SovereignTimingPatch）：召唤 0.3s，游戏端按结算时间安排斩击
    SUMMON = 0.3

    def summon(t):
        u = out_cubic(t / SUMMON)
        s = 0.82 + 0.18 * out_back(t / SUMMON, 1.3)
        open_ = keys(t, [(0, 28.0), (SUMMON, 0.0, out_back)])
        return merge(flutter(t), {
            "root": {"y": -140 * (1 - u), "sx": s, "sy": s},
            "coffins_l": {"r": -open_}, "coffins_r": {"r": open_},
        })

    bake(data, "summon", SUMMON, summon, slot=SLOTS, slot_alpha=lambda t: out_cubic(t / 0.16))

    # 待机漂浮
    bake(data, "idle_loop", 4.0, lambda t: merge(flutter(t), {"root": {"y": 10 * math.sin(2 * math.pi * t / 2.0)},
                                                             "chest": {"r": 1.2 * math.sin(2 * math.pi * t / 4.0)}}),
         loop=True, slot=SLOTS)

    # 斩击：深蓄力 0~0.14（剑臂后引、上身后仰、棺材环收拢）→ 斩下 0.14~0.2（slash 事件 0.18）→ 余势停顿 → 0.7 收回
    SLASH = 0.18

    def attack(t):
        arm = keys(t, [(0, 0.0), (0.14, 38.0, out_cubic), (0.2, -132.0, in_cubic), (0.4, -138.0, out_cubic), (0.7, -10.0)])
        wrist = keys(t, [(0, 0.0), (0.14, 18.0), (0.2, 80.0, in_cubic), (0.4, 88.0, out_cubic), (0.7, 6.0)])
        lean = keys(t, [(0, 0.0), (0.14, 10.0, out_cubic), (0.2, -16.0, in_cubic), (0.4, -13.0), (0.7, 0.0)])
        lunge = keys(t, [(0, 0.0), (0.14, -40.0, out_cubic), (0.2, 110.0, in_cubic), (0.4, 95.0), (0.7, 0.0)])
        claw = keys(t, [(0, 0.0), (0.14, -14.0), (0.2, 22.0, in_cubic), (0.7, 0.0)])
        spread = keys(t, [(0, 0.0), (0.14, -12.0), (0.2, 16.0, in_cubic), (0.7, 0.0)])
        head = keys(t, [(0, 0.0), (0.14, 6.0), (0.2, -8.0, in_cubic), (0.7, 0.0)])
        return merge(flutter(t, 1.8), {
            "root": {"x": lunge, "y": -0.25 * lunge},
            "chest": {"r": lean}, "head": {"r": head},
            "sword_arm": {"r": arm}, "sword": {"r": wrist},
            "claw_arm": {"r": claw},
            "coffins_l": {"r": spread}, "coffins_r": {"r": -spread},
        })

    bake(data, "attack", 0.7, attack, events=[(SLASH, "slash")], slot=SLOTS)

    # 消散：上浮、略放大、淡出；棺材环张开
    def dismiss(t):
        u = out_cubic(t / 0.45)
        return merge(flutter(t), {"root": {"y": 90 * u, "sx": 1 + 0.08 * u, "sy": 1 + 0.08 * u},
                                  "coffins_l": {"r": 10 * u}, "coffins_r": {"r": -10 * u}})

    bake(data, "dismiss", 0.45, dismiss, slot=SLOTS, slot_alpha=lambda t: 1 - out_cubic(t / 0.45))
    return rig, data


def main():
    rig, data = build()
    write(OUT, "thanatos", data, rig)
    stage_spine(ROOT / "mod/pck_stage", RES_DIR, "thanatos", OUT, "thanatos_skel_data", default_mix=0.0)
    atts = data["skins"][0]["attachments"]
    print("staged thanatos:", {k: len(v[k]["uvs"]) // 2 for k, v in atts.items()}, "vertices")
    if "--no-preview" not in sys.argv:
        for a in ("summon", "attack", "dismiss"):
            preview(data, rig, a, ROOT / f"art/rig_preview/thanatos_{a}.gif", canvas=(1500, 1700), origin=(700, 900), scale=0.3, every=1)


if __name__ == "__main__":
    main()
