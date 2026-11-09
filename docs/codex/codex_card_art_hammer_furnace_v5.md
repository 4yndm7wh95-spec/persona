# 锤子时间 / 熔炉：重做提示词（第五版）

用户确认被否定的是这两张。仓库里能查到的历史：

| 卡 | 版本 | 结果（来自提示词里记录的用户反馈） |
|---|---|---|
| hammer_time | batch_06 | 把手臂当成锤子，没读懂原图 |
| hammer_time | 修订 1、2 | 加虎钳 / 只清理残留，仍是锤子 |
| hammer_time | 修订 3 | 被否：手太像人、位置和剑样式不对 |
| hammer_time | 修订 4 | 手改成连指手、剑改成结城理的剑；用户仍列为否定 |
| furnace | batch_06 | 只换锤子材质 → 用户说改动不够 |
| furnace | 修订 1 | 塔罗熔流 + 钳子 + 新剑 → 用户说改动太大 |
| furnace | 修订 2 | 只改坩埚外壁的小图案 → 用户仍列为否定 |

**我不知道修订 4 / 修订 2 具体哪里不满意**，下面是按"原图结构 + 上述历史"重新分析后写的，每张都附了原因说明。你看完出图告诉我哪里不对，我再改。

通用：输出 1000×760 不透明 PNG；放 `art/cards/v4/local_edits/round2/hammer_furnace_v5/`；`comparison.png` 放原版 | 新图；状态 `awaiting_user_review`；不自动重试；不改代码和卡框。

---

## 一、hammer_time（锤子时间）

### 我读到的原图结构
（对照 `art/cards/v4/local_edits/round2/guides/hammer_time_layout_guide.png`，左半是原图加编号，右半是图例）

- **一把剑，四只手，三个角色**：A、B 是同一个角色（Regent）的两只手：A 在左上，带一条橙色铠甲前臂伸向右上角；B 在中间，带一条深棕色粗前臂伸向右边缘。C 是另一个角色：带灰色腕带的深色手臂从左边伸进来，最左边是绿色袖子，C 的手在 B 后面。D 是第三个角色：手指绕在剑身根部，蓝色袖子从左下伸进来，在 B 下面。
- 剑柄是灰绿色缠绕柄，从左上斜向中间，大部分被手遮住；剑身是青色晶体，带白色射线和碎片，在右下角。
- 手的排布是一团紧凑的斜向拳头堆，**不是整齐的一列**。

### 前几版为什么不对（我的判断）
1. 把铠甲前臂当成锤头、把手臂当成木柄 → 变成锻造锤。
2. 修订 3 重排了手的位置，把手叠成一列。
3. 修订 4 位置对了，但手臂变成了又长又直的白色管子，形状和原图的前臂块不一样。

### 提示词
附图：Image1 = `research/regent_assets/images/packed/card_portraits/regent/hammer_time.png`；Image2 = `art/cards/v4/local_edits/round2/guides/hammer_time_layout_guide.png`；Image3 = `art/parts_src/sword.png`。

```
Use case: precise-object-edit. Image1 is the ORIGINAL hammer_time card and the strict EDIT TARGET. Image2 is a LAYOUT GUIDE: use its letters and legend only, never copy its circles, lines or text. Image3 is the Makoto sword reference: sword design only, ignore its background.

SEMANTICS: this is NOT a hammer and NOT a forging scene. ONE sword is gripped by FOUR hands that belong to THREE different characters. The grey-green wrapped grip runs diagonally from the upper left to the center, mostly hidden under the hands; the cyan crystal blade with white rays and shards is at the bottom right.

METHOD: edit in place, outline for outline. Keep Image1's cyan rim-light outline around every hand and arm, every overlap, every wrist angle and the exact crop. Change only what each hand and sleeve is made of. Do not redraw the scene. Do not straighten, tidy, lengthen or re-pose any arm. Do not stack the hands in a column. Do not expose more of the grip than Image1 shows. The two large forearm shapes (A's armor plank to the upper right and B's dark band to the right edge) stay as large shapes of the same width and direction, they must not turn into thin long tubes.

A and B (one character) become JACK FROST: two plump ivory-white mitten hands in exactly the old fist footprints. A's curled fingers become three rounded mitten lobes inside A's old contour, B's fingers likewise; no knuckles, tendons, nails or five long fingers. A's armored forearm plank becomes a short plush white forearm in the SAME diagonal band and width, with ONE pointed blue jester cuff with small gold bells where the armor's wrist end was. B's big dark forearm band running to the right edge becomes a white forearm of the same band and width with a blue jester cuff near the edge. Soft lavender shadow planes on the white. No brown left anywhere on A or B.

C becomes PIXIE: the hand on the grip with the wrist wrap becomes a small slender hand in a violet glove, same position, still lying BEHIND B. The arm band from the left stays as a slimmer arm with a violet sleeve; the green sleeve at the far-left edge becomes the same shape in violet fabric; the grey wrist wrap becomes a thin silver bangle.

D becomes PYRO JACK: the hand wrapped near the blade base becomes a small grey cartoon mitten with short rounded lobes, same position, still BELOW B. The blue sleeve entering from the lower left becomes a deep-violet cloak sleeve of the same shape and band.

SWORD: the grey-green grip wrap becomes black leather wrap in diagonal bands wherever it is visible; any pommel or crossguard that shows is plain silver with a round pommel (design from Image3). The blade keeps its exact silhouette, rays and shards at the same positions, but it is now silver steel with a cyan magical glow inside, so it still reads as the same glowing blade.

KEEP: purple and magenta background and its shapes, the black shadow shapes at the top right and left, all cyan rim lights, all positions and the crop. NO Makoto, no heads, no bodies, no hammer, no wooden shaft, no vise or clamp, no tools, no extra hands. Rich hard cel-shading, crisp dark outlines. Opaque, full-bleed, 1000x760, no text, frame or watermark.
```

---

## 二、furnace（熔炉）

### 我读到的原图结构
- 左上角一个巨大的倾倒坩埚（暗红色壳、奶白色边），倒出橙红色熔流，落到中下方。
- 中间是锤头（圆润的钟形，深栗色，黄色描边，中间一个菱形窗，里面是橙色四角星 = Regent 的标志），粗壮的深栗色锤柄从右边缘伸进来。锤头砸在剑模上，有黄白色冲击闪光。
- 下方一条斜长的剑模，青色边线，里面是红橙到洋红的发光色。橙色工作台。深紫色背景。

### 历史说明
材质替换被说"不够"，大改被说"太多"，小图案被否。中间那一档应该是：**构图全部不变，但画面里出现一个一眼能认出的 P3R 东西**。我写两个方案，让 Codex 都出，你选方向。

### 方案 F1（推荐）：杰克冰人握锤柄 + 锤头换月亮
附图：Image1 = `research/regent_assets/images/packed/card_portraits/regent/furnace.png`；Image2 = `art/cards/v4/local_edits/batch_04/kingly_kick.png`（杰克冰人造型参考）。

```
Use case: precise-object-edit. Image1 is the ORIGINAL furnace card and the strict EDIT TARGET. Image2 is a Jack Frost design reference only; ignore its background and pose. Feedback history: a pure material swap of the hammer was rejected as too little, a redesign with tarot cards, tongs and a new sword was rejected as too much. Target: a MEDIUM change with an obviously P3R character, same composition.

KEEP EXACTLY: the deep-purple background and its dark blobs, the tilted crucible at the upper left with its orange-red molten pour, the hammer head's silhouette and strike point, the yellow-white impact flash, the long cyan-edged blade mould with its red-orange and magenta interior, the orange bench, all positions and the crop.

CHANGE 1 (character): the thick dark-maroon shaft entering from the right edge (whether it is a shaft or the Regent's arm) becomes a dark wooden hammer handle wrapped in navy leather, gripped near the right edge (about x 78-100%, y 22-42%) by JACK FROST's plump ivory-white mitten hand (thumb nub, at most two simple finger separations, no knuckles or nails) with a short plush white forearm and ONE pointed blue jester cuff with small gold bells. Only the hand and forearm are visible, no head, no body, same thickness band as the old shaft.

CHANGE 2 (emblem): keep the hammer head's silhouette, thickness and contact point. Its surface becomes dark gunmetal steel with silver bevels, the old yellow outline becomes thin gold edging of the same width, and the diamond window keeps its shape but the orange four-point Regent star inside it becomes an ivory crescent moon with one small cyan gem.

NOTHING ELSE changes: no tarot cards, no tongs, no new sword, no new shapes, no text. Rich hard cel-shading, crisp dark outlines, near-black occlusion planes. Opaque, full-bleed, 1000x760, no frame or watermark.
```

### 方案 F2：只动物件，不出现角色，但比修订 2 明显
附图：Image1 同上。

```
Use case: precise-object-edit. Image1 is the ORIGINAL furnace card and the strict EDIT TARGET. Feedback history: a small crucible motif was rejected as too little, tarot cards plus tongs plus a new sword as too much. Target: bold object-only P3R Velvet Room redesign of three objects, with every silhouette and position unchanged and no characters.

KEEP EXACTLY: the deep-purple background and dark blobs, the orange-red molten pour and its shape, the yellow-white impact flash, the long cyan-edged blade mould with its red-orange and magenta interior, the orange bench, every silhouette, position and the crop.

CHANGE 1: the tilted crucible at the upper left keeps its exact silhouette, lip and hot red interior, but its whole outer wall becomes a Velvet Room silver-and-navy cauldron: deep royal-blue enamel body, wide silver filigree bands following the curve, a polished silver rim, ONE ivory crescent moon medallion, hard-edged cel-shaded bevels with warm orange bounce light from the pour. No floating tarot cards or fragments.

CHANGE 2: the hammer head keeps its silhouette and strike point. Its surface becomes dark gunmetal steel with silver bevels and thin gold edging where the old yellow outline was; the diamond window keeps its shape but the orange Regent star becomes an ivory crescent moon with one small cyan gem.

CHANGE 3: the thick dark-maroon shaft from the right edge keeps its shape and thickness but becomes dark wood wrapped in navy leather with silver ring bands.

NO characters, no hands, no tarot cards, no tongs, no new sword, no text. Rich hard cel-shading, crisp dark outlines, near-black occlusion planes. Opaque, full-bleed, 1000x760, no frame or watermark.
```
