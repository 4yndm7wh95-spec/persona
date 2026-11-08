# Joker 召唤姿势 summon_grip_a

以 `summon_touch2_a.png` 为底图，用内置 ImageGen 生成局部素材，再将抬臂、肩、头和垂手两个区域按蒙版合成回底图。重画次数 0。完整提示词、参考路径和蒙版坐标见 `summon_grip_a_generation.json`。

- 原始图为 1024×1536 RGBA。逐像素比对得到蒙版外 RGBA 改动 0，半透明叠图确认下摆、腿和鞋重合。
- 合成时清除了蒙版内 742 个远离主体边缘的半透明像素。画布四边 alpha 均为 0，深灰底检查未见底色残留。按距 alpha > 24 区域超过 3 px 的判据，蒙版外保留底图原有的 836 个极低透明度像素，最高 alpha 为 3/255。
- 仅归一化 `summon_grip_a`。成品为 1536×1632 RGBA，比例 1.0，前脚锚定，偏移 [434, 81]，与 `summon_touch2_a` 的 final 图一致。
- `check_summon_grip.png` 包含三张 final 图、面具手和垂手的放大对照。近侧手的两根手指仍略张开。

## 一致性检查原始输出

```text
summon_grip_a      (face covered) standing height x0.993  boot height x0.967  boot width x0.984  feet moved [83, 178] px  ok
sheet: art\p5\poses\check_summon_grip_a.png | overall: ok
```
