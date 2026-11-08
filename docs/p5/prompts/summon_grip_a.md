# summon_grip_a：毒性爆发，攥紧面具（生图 prompt）

用途：毒性爆发打出那一刻，Joker 扶在面具上的手猛地攥紧，一直攥到巨手捏碎（约 1.2 秒），再松手放下。
动画已经接好（`tools/p5/build_rig_p5.py` 的 `cast_outbreak` / `cast_outbreak_from_mask`）。没有这张图时先用 `summon_hold_a` 顶着；
图放进 `art/p5/poses/final/summon_grip_a.png` 之后会自动换上。

## 参考图（一起喂给生图）

1. `art/p5/poses/final/summon_touch2_a.png`：**身体、外套、腿脚、站姿完全照这张**（选中牌时的姿势，从这张直接切过来）。
2. `art/p5/poses/final/summon_hold_a.png`：手的位置参考（手盖在面具上）。
3. 画风母版：`art/p5/concept/style_A_p5r_official_1.png`。

## Prompt（英文，直接用）

```
Full-body character art of Joker (Persona 5 Royal protagonist, Phantom Thief outfit), same character, same outfit, same
art style and same line weight as the reference images: P5R official illustration style, clean bold black outlines,
cel shading with hard-edged shadows, high contrast, flat colors.

Pose: keep the body, legs, feet, stance and the long black coat EXACTLY as in reference image 1 (standing three-quarter view
facing screen-right, weight on the back leg, coat tails flaring to the left). Only the raised arm, the head and the shoulders change.

The near-side hand (red glove) is on his face over the domino mask, as in reference image 2, but now CLENCHED HARD: the fingers
are bent like claws and dig into the mask and his face, knuckles sharply raised, the glove leather stretched and creased over
the knuckles, the thumb pressed against the cheekbone. The forearm is tense and slightly twisted inward, the elbow raised a little
higher and pulled closer to the body. The shoulder of that arm is hunched up with the strain. The head is tilted slightly down
and forward into the grip. Between the clawed fingers one eye is visible, narrowed and glaring, with a cold, menacing look. The
mouth is pressed into a tight line, a faint vein of tension at the jaw.

The far-side arm hangs at his side as in reference image 1, but that hand is now also clenched into a tight fist (red glove).

No effects, no flames, no glow, no motion lines, no text. Plain flat light grey background (#c8c8c8), the whole figure in frame
from hair to shoes, same size and same position in the frame as reference image 1, feet on the same spot.
```

负面词（如果工具支持）：

```
open hand, relaxed fingers, hand in pocket, pointing, extra fingers, fused fingers, extra arms, different coat, cropped feet,
blue fire, effects, glow, background scenery, 3d render, realistic, blurry, sketch
```

## 交付要求

- 1536×1632、透明背景（生成后抠图），**脚跟 `summon_touch2_a` 对齐**（站位、大小一致；可以用 `tools/p5/normalize_poses.py` 按其他姿势图的方式对齐）。
- 手要是**同一只手**（近侧那只，画面偏左），跟 `summon_touch2_a` 里扶面具的是同一只，否则切过去会跳。
- 检查：和 `summon_touch2_a` 叠在一起看，除了抬起的手臂、肩、头，其他部分几乎不动（外套下摆、腿脚不要变）。

## 放进游戏

```bash
python tools/p5/build_rig_p5.py cast_outbreak_from_mask cast_outbreak
python tools/p5/stage_p5.py
```

然后照 `p5mod/README.md` 重新打 PCK。代码不用改。
