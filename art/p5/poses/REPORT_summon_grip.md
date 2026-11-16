# summon_grip_a 第 2 版

底图 summon_hold_a，内置 ImageGen 仅生成两个 192×192 局部裁剪的替换素材。提示词未提及实体面具。两张素材各生成一次，重画次数 0。

按底图高饱和红色选区扩张 14 px，再保护原来的皮肤、灰眼睛、头发、领口和袖子，保留手套邻近黑描边。蒙版外逐像素 RGBA 改动为 0。原始眼睛和 3/4 侧脸保留。全身未生成、未缩放或对齐。局部生成输出缩回原裁剪的 192×192 像素网格后合成。

仅归一化本张，1536×1632 RGBA，比例 1.0，前脚锚定，偏移 [435,81]。与 summon_hold_a final 对齐。蒙版外继承底图原有透明边缘数据，未对全图清理。远离轮廓的低 alpha 残点可能仍存在。

check_summon_grip.png 为三张 final 并排及手部放大。差异图保留在本机 outputs/summon_grip_v2_diff.png，红色改动仅在两只手套区域。选区位图本机路径和选区算法记录在生成元数据。

## 一致性检查原始输出

```text
summon_grip_a      (face covered) standing height x0.999  boot height x0.971  boot width x0.921  feet moved [204, 179] px  ok
sheet: art\p5\poses\check_summon_grip_a.png | overall: ok
```
