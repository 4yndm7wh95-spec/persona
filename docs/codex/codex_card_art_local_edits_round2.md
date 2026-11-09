# 卡图第二轮：P3R 局部替换提示词（未做的 59 张）

用户规则：**不改背景，沿用原版卡图的背景，构图不变，只把 Regent 本体 / 手 / 武器等换成 P3R 的东西**。风格以已经做好的 30 张局部修改（`art/cards/v4/local_edits/batch_01～06`）为准。

## 0. 先看这里（给用户）

我只能从仓库确认到这些状态：

| 状态 | 卡 |
|---|---|
| 仓库里记录为"用户否定" | `hammer_time` 修订 1～3（改成锤子/虎钳/只清理残留，都没读懂原图）；`furnace` 修订 1（改动太大）。目前待你审的是 `hammer_time` 修订 4、`furnace` 修订 2，**我没有再写新提示词**，你不满意再说。 |
| 已迭代过的 | `strike_regent`、`hidden_cache`、`bundle_of_joy`、`refine_blade`、`the_smith`、`tutor`（`earlier_candidates` 里是旧稿） |
| 其余 30 张 | 状态全部是 `awaiting_user_review`，仓库里**没有**记录你通过或否定了哪几张 |

**所以：被你否定的卡我只能认出上面这几张。其他被否定的，把卡名告诉我，我按"修订提示词模板"（文末）逐张写。**

另有几个我替你做的假设，不对请改：
1. **敌人、场景、特效都不动**，只换 Regent 本体 / 手 / 武器（和 batch_04～06 一致，比如 `reflect`、`hegemony` 里的敌人没动）。
2. **结城理的脸和头永远不出现**，只出现手、袖口；带头的人物全部换成 P3R 的魔物（杰克冰人 / 南瓜灯杰克 / 小精灵）。
3. 原图没有 Regent 的 10 张（第 3 节），建议**保持原图**，不生成。
4. `pillar_of_creation` 沿用你之前的指令：改成天鹅绒房间的蓝门，无人物。
5. `meteor_shower`、`the_sealed_throne` 原图是 606×852 竖图，这次**保持竖图**（v4 当时被硬转成横图，是上次的问题之一）。
6. `sovereign_blade` 本地没有原版插画（只有 v3 重绘稿），**不写**；需要先找到原图。

## 1. 执行方式（给 Codex）

- 每批 **5 张**，生成后停下，等用户审；**不要自动重试、不要自己判断通过**（沿用 batch_01 的 policy：`User alone determines approval. No autonomous visual review or retry.`）。
- 输入：Image1 = 原版图（路径见每张），需要时再附下面第 2 节的参考图（只附该张列出的）。
- 输出：1000×760（竖图那两张是 606×852）不透明 PNG，放 `art/cards/v4/local_edits/round2/batch_NN/<卡名>.png`，另存 `comparison.png`（原版 | 新图）和 `prompts_and_sources.json`（格式同 batch_06，状态填 `awaiting_user_review`）。
- 不改卡框、代码、v3/v4 旧文件，不接入游戏。
- 机器可读版本：`art/cards/v4/local_edits/round2_prompts.json`（同样的提示词，已展开通用前缀和角色描述，可直接喂给生图）。

## 2. 参考图


- **JF**：art/cards/v4/local_edits/batch_04/kingly_kick.png (Jack Frost)
- **PJ**：art/cards/v4/local_edits/batch_04/kingly_punch.png (Pyro Jack)
- **PX**：art/cards/v4/local_edits/batch_05/manifest_authority.png (Pixie)
- **MKH**：art/cards/v4/local_edits/batch_01/know_thy_place.png + mod/pck_stage/p3makoto/ui/charselect_portrait.png (Makoto hand/uniform only)
- **SWORD**：art/parts_src/sword.png (Makoto sword)

## 3. 建议保持原图的 10 张（原图里没有 Regent，只有敌人或特效）

| 卡 | 原因 |
|---|---|
| `black_hole` 黑洞 | 只有敌人和青色闪电环，无 Regent。可选：左下角棕色长方块 → P3R 的黑色棺材（同位置同形状）。 |
| `bombardment` 轰击 | 只有燃烧的陨石和青色光环，无 Regent。可选：把环绕陨石的青色光环换成一圈小塔罗牌（同路径同大小）；建议保持原图。 |
| `dying_star` 星灭 | 无 Regent，只有星爆漩涡和三个红色小敌人。建议保持原图。 |
| `guiding_star` 引导之星 | 无 Regent，只有青色螺旋光束和被击飞的敌人。建议保持原图。 |
| `lunar_blast` 月面射击 | 无 Regent，只有白色月涡和被击中的岩石敌人。建议保持原图。 |
| `monarchs_gaze` 王之凝视 | 只有绿色史莱姆敌人，无 Regent。建议保持原图。 |
| `particle_wall` 粒子墙 | 只有绿色植物敌人和蓝色星座砖墙，无 Regent。建议保持原图。 |
| `seven_stars` 七星 | 只有三只暗影敌人和七星连线，无 Regent。建议保持原图。 |
| `solar_strike` 太阳打击 | 只有蓝色敌人、橙黄光束和白色弯月斩，无 Regent。建议保持原图。 |
| `spectrum_shift` 光谱偏移 | 只有彩虹光带，无 Regent。建议保持原图。 |

其中 `black_hole`、`bombardment` 我各写了一条"可选装饰"提示词（见第 4 节末尾），不想做就跳过。

## 4. 提示词（49 张，按批次）

通用前缀、角色描述已经展开写进每条提示词里，复制即可用。每张上面的中文一行是"这张要换什么"，方便你不用读英文就能审方向。


### 第 1 批 · hands only 只有手

#### 1. `genesis` 创世纪

- 效果：在你的回合开始时，获得{StarsPerTurn:starIcons()}。
- 换什么：右上伸来的手 → 结城理的手（黑袖白袖口，同样食指触碰星光的姿势）；金色四角星、星空背景不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/genesis.png`
- 参考图：art/cards/v4/local_edits/batch_01/know_thy_place.png + mod/pck_stage/p3makoto/ui/charselect_portrait.png (Makoto hand/uniform only)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace ONLY the hand reaching in from the upper right (cuffed sleeve, index finger about to touch the star) with MAKOTO HAND in the same pose: index finger touching toward the golden star, sleeve entering from the upper-right edge. Keep unchanged: the large golden four-point star, the small stars, the blue-purple starfield background. Character designs: [MAKOTO HAND] = MAKOTO YUKI's natural warm pale bare hand(s) with black school-blazer sleeve and white shirt cuff (a small red armband only if the upper arm is visible); NO Makoto face, head, hair or neck anywhere, sleeves leave the canvas at the same edges where the original arms did.
```

#### 2. `stardust` 星尘

- 效果：随机对敌人造成{Damage:diff()}点伤害X次。
- 换什么：上方撒星尘的金色护手 → 结城理的手（黑袖白袖口，同样撒落的姿势）；金色星尘、红色火光、蓝色背景不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/stardust.png`
- 参考图：art/cards/v4/local_edits/batch_01/know_thy_place.png + mod/pck_stage/p3makoto/ui/charselect_portrait.png (Makoto hand/uniform only)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace ONLY the golden gauntlet hand entering from the top and pouring stardust with MAKOTO HAND in the same pinching and pouring pose, sleeve entering from the top edge at the same place. Keep unchanged: the falling gold stars and dust, the red-orange glow below, the blue background and its dark shapes. Character designs: [MAKOTO HAND] = MAKOTO YUKI's natural warm pale bare hand(s) with black school-blazer sleeve and white shirt cuff (a small red armband only if the upper arm is visible); NO Makoto face, head, hair or neck anywhere, sleeves leave the canvas at the same edges where the original arms did.
```

#### 3. `supermassive` 超质量体

- 效果：造成{CalculatedDamage:diff()}点伤害。 你在本场战斗中每生成过一张牌，这张牌就额外造成{ExtraDamage:diff()}点伤害。
- 换什么：画面中央张开的金棕色巨大护手（掌心带发光圆环）→ 结城理的手（同样张开姿势，掌心保留发光圆环符文）；黄色闪电、绿色背景不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/supermassive.png`
- 参考图：art/cards/v4/local_edits/batch_01/know_thy_place.png + mod/pck_stage/p3makoto/ui/charselect_portrait.png (Makoto hand/uniform only)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace ONLY the huge golden-brown gauntlet in the center (open hand facing the viewer, glowing ring sigil on the palm) with MAKOTO HAND in the same open pose and scale, blazer sleeve entering from the lower edge. The glowing green-yellow ring sigil stays on the palm as a glowing magic mark. Keep unchanged: the yellow-green lightning arcs, the green background and its shapes. Character designs: [MAKOTO HAND] = MAKOTO YUKI's natural warm pale bare hand(s) with black school-blazer sleeve and white shirt cuff (a small red armband only if the upper arm is visible); NO Makoto face, head, hair or neck anywhere, sleeves leave the canvas at the same edges where the original arms did.
```

#### 4. `crescent_spear` 新月长矛

- 效果：造成{CalculatedDamage:diff()}点伤害。 你每有一张拥有{singleStarIcon}耗能的卡牌，这张牌就额外造成{ExtraDamag
- 换什么：左下角握着长矛的蓝袖棕手 → 结城理的手（黑袖白袖口）；粉紫发光新月矛头、星光、背景不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/crescent_spear.png`
- 参考图：art/cards/v4/local_edits/batch_01/know_thy_place.png + mod/pck_stage/p3makoto/ui/charselect_portrait.png (Makoto hand/uniform only)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace ONLY the brown gloved hand and blue sleeve at the lower left that grips the spear shaft with MAKOTO HAND gripping the same place on the shaft, same grip angle, sleeve leaving at the same lower-left edge. Keep unchanged: the pink-white crescent spearhead and its sparkles, the glowing outline, the purple background. Character designs: [MAKOTO HAND] = MAKOTO YUKI's natural warm pale bare hand(s) with black school-blazer sleeve and white shirt cuff (a small red armband only if the upper arm is visible); NO Makoto face, head, hair or neck anywhere, sleeves leave the canvas at the same edges where the original arms did.
```

#### 5. `quasar` 类星体

- 效果：从3张随机{IfUpgraded:show:升级过的|}无色牌中选择1张加入你的手牌。
- 换什么：拉着青色丝带的两只棕色护手 → 结城理的两只手（握拳，黑袖白袖口）；背后昏暗的兜帽轮廓 → 同色天鹅绒窗帘褶皱；旋转星系圆盘、紫蓝背景不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/quasar.png`
- 参考图：art/cards/v4/local_edits/batch_01/know_thy_place.png + mod/pck_stage/p3makoto/ui/charselect_portrait.png (Makoto hand/uniform only)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace the two brown gauntlet hands (a fist at the upper left and a fist at the lower right pulling the cyan ribbon around the galaxy disc) with two MAKOTO HAND in the same grips and angles. The dim hooded Regent shape behind (top center) becomes plain purple Velvet Room drapery in the same colors and folds, without any Regent head or face. Keep unchanged: the swirling galaxy disc, the cyan ribbon and sparkles, the purple-blue background. Character designs: [MAKOTO HAND] = MAKOTO YUKI's natural warm pale bare hand(s) with black school-blazer sleeve and white shirt cuff (a small red armband only if the upper arm is visible); NO Makoto face, head, hair or neck anywhere, sleeves leave the canvas at the same edges where the original arms did.
```

### 第 2 批 · hands + Pixie 手和小精灵

#### 6. `big_bang` 大爆炸

- 效果：抽{Cards:diff()}张牌。 获得{Energy:energyIcons()}。 获得{Stars:starIcons()}。 铸造{Forge:dif
- 换什么：左右两只蓝金护手 → 结城理的两只手（掌心向上，只露手和袖口）；中间漂浮的金色剑符号换成结城理剑的形状、卡牌符号换成塔罗牌背（金橙发光色不变）。不出现结城理的脸或头。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/big_bang.png`
- 参考图：art/cards/v4/local_edits/batch_01/know_thy_place.png + mod/pck_stage/p3makoto/ui/charselect_portrait.png (Makoto hand/uniform only)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace the two large blue-and-gold armored gauntlets (left and right of center, palms open and fingers spread) with two MAKOTO HAND, same positions, palm poses and finger spread, forearms entering from the lower-left and lower-right edges where the old blue forearms were. Among the floating orange-gold symbols: the sword symbol takes the silhouette of MAKOTO SWORD but keeps its orange-gold glow color; the small orange card symbol becomes a tarot-card back with a silver crescent motif and no text; the star and the sun symbol stay. NO head or face of Makoto anywhere in the image. Keep the dark blue background, its shapes and the burst between the hands unchanged. Character designs: [MAKOTO HAND] = MAKOTO YUKI's natural warm pale bare hand(s) with black school-blazer sleeve and white shirt cuff (a small red armband only if the upper arm is visible); NO Makoto face, head, hair or neck anywhere, sleeves leave the canvas at the same edges where the original arms did. [MAKOTO SWORD] = Makoto's sword (asset art/parts_src/sword.png): plain SILVER straight single-edge tapered blade with steel-grey bevel, simple narrow silver crossguard, black leather-wrapped grip, round silver pommel, no gold ornament, gems or crown.
```

#### 7. `constellation` 群星荟萃

- 效果：使另一名玩家抽{Cards:diff()}张牌，获得{Energy:energyIcons()}，并获得{Block:diff()} 点格挡
- 换什么：两只棕色护手 → 结城理的两只手（下方握拳那只和右上递出那只，各拿一块青色星座牌）；背景里大块蓝紫色长袍 → 同样颜色的天鹅绒窗帘（去掉 Regent 的链子和兜帽轮廓）；中间吊着的金色小星座人 → 杰克冰人造型的星座线。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/constellation.png`
- 参考图：art/cards/v4/local_edits/batch_01/know_thy_place.png + mod/pck_stage/p3makoto/ui/charselect_portrait.png (Makoto hand/uniform only)；art/cards/v4/local_edits/batch_04/kingly_kick.png (Jack Frost)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace the two brown gauntlet hands (the lower-left fist and the upper-right reaching hand, each holding a cyan constellation tile) with two MAKOTO HAND, same grips, same angles; black blazer sleeves with white cuffs replace the blue sleeves. The large violet-blue cloth mass behind (the Regent's hooded chest, chain and clasp) becomes plain Velvet Room velvet drapery in the SAME colors and fold shapes, with every Regent ornament removed. The tiny golden constellation stick-figure hanging in the center below becomes a constellation drawn as JACK FROST (jester hat points, round body), same size, same gold lines and dots. Keep the cyan tiles, sparkles and violet-blue background unchanged. No Makoto face or head. Character designs: [MAKOTO HAND] = MAKOTO YUKI's natural warm pale bare hand(s) with black school-blazer sleeve and white shirt cuff (a small red armband only if the upper arm is visible); NO Makoto face, head, hair or neck anywhere, sleeves leave the canvas at the same edges where the original arms did.
```

#### 8. `glimmer` 微光

- 效果：抽{Cards:diff()}张牌。 将你手牌中的{PutBack:diff()}张牌放到抽牌堆顶部。
- 换什么：右侧探入的 Regent（尖帽、蓝袖、张开的手）→ 从右边探入的 Pixie（同位置，小手托着星云）；青粉色星云漩涡不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/glimmer.png`
- 参考图：art/cards/v4/local_edits/batch_05/manifest_authority.png (Pixie)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace ONLY the Regent entering from the right edge (pointed hat at the upper right, blue sleeve arm reaching under the galaxy, another open hand at the top center) with PIXIE reaching in from the same edge: her violet-gloved hands cupped under and over the swirl in the same two positions, wing edge visible at the right. Keep unchanged: the cyan and pink galaxy swirl with sparkles, the purple background, the black corner shape. Character designs: [PIXIE] = PIXIE: tiny fairy with short dark-blue bob hair, pointed ears, violet/navy leotard, violet gloves and thigh-high boots, translucent lavender insect wings, natural light skin, small delicate hands.
```

#### 9. `glitterstream` 流光溢彩

- 效果：获得{Block:diff()}点格挡。 在下一回合获得{BlockNextTurn:diff()}点格挡。
- 换什么：上方的 Regent 头 → 探出头的 Pixie；左下角戴金色护腕握着发光球的手 → Pixie 紫手套的手（银色细手环）；青色闪光水流、紫色背景不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/glitterstream.png`
- 参考图：art/cards/v4/local_edits/batch_05/manifest_authority.png (Pixie)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace ONLY the Regent head in the upper center (hooded purple face with a gold chain) with PIXIE peering down, face visible, short blue hair and pointed ears inside the same silhouette. The hand at the lower left wearing a gold bracer and holding the glowing star becomes PIXIE's violet-gloved hand with a thin silver bangle, same pose and same glowing star. Keep unchanged: the cyan sparkling stream, the white star accents, the purple-blue background. Character designs: [PIXIE] = PIXIE: tiny fairy with short dark-blue bob hair, pointed ears, violet/navy leotard, violet gloves and thigh-high boots, translucent lavender insect wings, natural light skin, small delicate hands.
```

#### 10. `prophesize` 预言

- 效果：抽{Cards:diff()}张牌。
- 换什么：双手托着发光橙色星座球的 Regent（昏暗兜帽）→ Pixie 捧着同一个球；球、深紫背景不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/prophesize.png`
- 参考图：art/cards/v4/local_edits/batch_05/manifest_authority.png (Pixie)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace ONLY the dim hooded Regent holding the glowing orange constellation orb with two brown gauntlets with PIXIE, same position and scale: face visible in the hood area with short dark-blue hair, two small violet-gloved hands cupping the same orb at the same spot. Keep unchanged: the glowing orange orb with its constellation lines, the dark purple background and the faint hood shape behind. Character designs: [PIXIE] = PIXIE: tiny fairy with short dark-blue bob hair, pointed ears, violet/navy leotard, violet gloves and thigh-high boots, translucent lavender insect wings, natural light skin, small delicate hands.
```

### 第 3 批 · sword 剑

#### 11. `photon_cut` 光子切割

- 效果：造成{Damage:diff()}点伤害。 抽{Cards:diff()}张牌。 将手牌中的一张牌放到抽牌堆顶部。
- 换什么：发光弧形剑的黑白条纹剑柄 → 结城理剑的剑柄（黑皮缠绕、银色护手、圆形剑首）；发光弧刃、被砍的蘑菇敌人、右边小人影、暗红背景不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/photon_cut.png`
- 参考图：art/parts_src/sword.png (Makoto sword)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace ONLY the black-and-white striped sword hilt with the gold star at the lower right with the hilt of MAKOTO SWORD at the same position, angle and size: black leather-wrapped grip, simple silver crossguard, round silver pommel, joined to the same glowing curved orange-yellow blade arc. Keep unchanged: the glowing blade arc, the mushroom enemy being cut, the tiny figure at the right, the dark crimson background. Character designs: [MAKOTO SWORD] = Makoto's sword (asset art/parts_src/sword.png): plain SILVER straight single-edge tapered blade with steel-grey bevel, simple narrow silver crossguard, black leather-wrapped grip, round silver pommel, no gold ornament, gems or crown.
```

#### 12. `shining_strike` 明耀打击

- 效果：造成{Damage:diff()}点伤害。 获得{Stars:starIcons()}。 将这张牌放置于你的抽牌堆顶部。
- 换什么：左上角斜劈下来的金色宽刃 → 结城理剑的银色剑刃带暖金反光（同角度）；青绿色敌人、白色星爆、红色背景不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/shining_strike.png`
- 参考图：art/parts_src/sword.png (Makoto sword)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace ONLY the broad golden blade slashing in from the upper left with the blade of MAKOTO SWORD at the same angle, length and impact point, silver blade lit with the same warm gold flare and sparks. Keep unchanged: the teal ghost enemy, the ivory star burst, the grey-green leaf shapes, the dark red background. Character designs: [MAKOTO SWORD] = Makoto's sword (asset art/parts_src/sword.png): plain SILVER straight single-edge tapered blade with steel-grey bevel, simple narrow silver crossguard, black leather-wrapped grip, round silver pommel, no gold ornament, gems or crown.
```

#### 13. `radiate` 辐射

- 效果：你在本回合每获得一点{Stars:starIcons()}，此牌就对所有敌人造成{Damage:diff()}点伤害一次。{InCombat: （命中{Calc
- 换什么：白光中央金色的小 Regent 人影 → 金色逆光的小 Pixie；竖直的金色剑 → 结城理的剑（保留金色发光）；放射光线、红色背景不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/radiate.png`
- 参考图：art/cards/v4/local_edits/batch_05/manifest_authority.png (Pixie)；art/parts_src/sword.png (Makoto sword)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace ONLY the small golden Regent figure in the white burst at the center with a tiny golden-lit PIXIE with arms spread, same size and position, covered by the same blinding white core. The vertical golden sword becomes MAKOTO SWORD in shape but keeps its golden glow color. Keep unchanged: the white rays, the black and red background, the sparkles. Character designs: [PIXIE] = PIXIE: tiny fairy with short dark-blue bob hair, pointed ears, violet/navy leotard, violet gloves and thigh-high boots, translucent lavender insect wings, natural light skin, small delicate hands. [MAKOTO SWORD] = Makoto's sword (asset art/parts_src/sword.png): plain SILVER straight single-edge tapered blade with steel-grey bevel, simple narrow silver crossguard, black leather-wrapped grip, round silver pommel, no gold ornament, gems or crown.
```

#### 14. `sword_sage` 剑圣

- 效果：君王之剑获得重放{SwordSagePower:diff()}。
- 换什么：左边的 Regent（兜帽蓝袍，右手握剑）→ 南瓜灯杰克（灰色连指手握剑，剑柄换成结城理的剑柄）；晶体光刃、紫色背景不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/sword_sage.png`
- 参考图：art/cards/v4/local_edits/batch_04/kingly_punch.png (Pyro Jack)；art/parts_src/sword.png (Makoto sword)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace ONLY the hooded Regent at the center-left (blue robe, gripping the crystal sword with one brown hand at the right) with PYRO JACK in the same pose and scale, grey mitten gripping the same place; the hilt of the held sword becomes the hilt of MAKOTO SWORD. Keep unchanged: the glowing crystal blades and the crystal sword tower, the purple background and its shapes, the dark grey rock shape behind. Character designs: [PYRO JACK] = JACK-O'-LANTERN (Pyro Jack): carved ORANGE ribbed pumpkin head with black crescent eyes and a jagged grin, deep-violet floppy witch hat with a buckled band, violet cloak with a round button, small GREY cartoon mitten hands with short rounded lobes (no human fingers or fingernails). [MAKOTO SWORD] = Makoto's sword (asset art/parts_src/sword.png): plain SILVER straight single-edge tapered blade with steel-grey bevel, simple narrow silver crossguard, black leather-wrapped grip, round silver pommel, no gold ornament, gems or crown.
```

#### 15. `tyranny` 暴政

- 效果：在你的回合开始时，抽一张牌，并从你的手牌中消耗1张牌。
- 换什么：踩着东西、握剑的 Regent（红色描边、尖帽）→ 南瓜灯杰克握结城理的剑（剑尖朝下插地），橙色描边光保留；深蓝背景和黑色尖刺剪影不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/tyranny.png`
- 参考图：art/cards/v4/local_edits/batch_04/kingly_punch.png (Pyro Jack)；art/parts_src/sword.png (Makoto sword)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace ONLY the Regent at the center (brown pointed hat, red outline, standing with a vertical sword) with PYRO JACK in the same pose and scale, grey mittens gripping MAKOTO SWORD planted point-down in the same place as the old sword; keep the red-orange rim light on his outline. Keep unchanged: the dark navy background, the black spiky silhouettes at the bottom, the faint dark red shapes. Character designs: [PYRO JACK] = JACK-O'-LANTERN (Pyro Jack): carved ORANGE ribbed pumpkin head with black crescent eyes and a jagged grin, deep-violet floppy witch hat with a buckled band, violet cloak with a round button, small GREY cartoon mitten hands with short rounded lobes (no human fingers or fingernails). [MAKOTO SWORD] = Makoto's sword (asset art/parts_src/sword.png): plain SILVER straight single-edge tapered blade with steel-grey bevel, simple narrow silver crossguard, black leather-wrapped grip, round silver pommel, no gold ornament, gems or crown.
```

### 第 4 批 · Pyro Jack 南瓜灯 1

#### 16. `begone` 下去！

- 效果：选择你手牌中的一张牌，将其变化为{IfUpgraded:show:仆从打击+|仆从打击}。
- 换什么：左下方坐着、抬手指挥的昏暗 Regent → 南瓜灯杰克（同座位同手势）；蓝色圆球加方块、粉色爆光、红色虚线星座全部不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/begone.png`
- 参考图：art/cards/v4/local_edits/batch_04/kingly_punch.png (Pyro Jack)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace ONLY the dim Regent figure sitting at the lower left on the throne-like seat (his raised gesturing hand points toward the blue sphere) with PYRO JACK sitting on the same seat in the same pose, scale and gesture, grey mitten raised toward the sphere; keep the seat and the dim purple shading style. Keep unchanged: the blue sphere with the yellow-edged cube, the pink-white starburst, the red dotted constellation lines and the purple background. Character designs: [PYRO JACK] = JACK-O'-LANTERN (Pyro Jack): carved ORANGE ribbed pumpkin head with black crescent eyes and a jagged grin, deep-violet floppy witch hat with a buckled band, violet cloak with a round button, small GREY cartoon mitten hands with short rounded lobes (no human fingers or fingernails).
```

#### 17. `bulwark` 铸墙

- 效果：获得{Block:diff()}点格挡。 铸造{Forge:diff()}。
- 换什么：画面下方双臂高举的小 Regent → 小南瓜灯杰克（背影，双手高举灰色连指手）；水晶墙、岩浆火焰背景不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/bulwark.png`
- 参考图：art/cards/v4/local_edits/batch_04/kingly_punch.png (Pyro Jack)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace ONLY the small Regent at the bottom center, standing with both arms raised toward the crystal wall, with PYRO JACK seen from behind at the same small size (about 12% of the image height), both grey mittens raised in the same pose. Keep unchanged: the cyan crystal wall with its cracks and lightning, the lava and flame shapes above, the navy background and the foreground rocks. Character designs: [PYRO JACK] = JACK-O'-LANTERN (Pyro Jack): carved ORANGE ribbed pumpkin head with black crescent eyes and a jagged grin, deep-violet floppy witch hat with a buckled band, violet cloak with a round button, small GREY cartoon mitten hands with short rounded lobes (no human fingers or fingernails).
```

#### 18. `celestial_might` 天穹之力

- 效果：造成{Damage:diff()}点伤害{Repeat:diff()}次。
- 换什么：后方巨大的紫色星座长袍 Regent → 巨大的南瓜灯杰克（紫帽顶替原来的尖顶头）；中间发光的蓝色晶体、飞散的骨头碎块、红色背景不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/celestial_might.png`
- 参考图：art/cards/v4/local_edits/batch_04/kingly_punch.png (Pyro Jack)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace the large dark-purple Regent figure behind (pointed four-star hat-head at top center, constellation-patterned robe) with PYRO JACK at the same huge scale: the deep-violet witch hat tip takes the place of the old hat-head tip, the pumpkin face sits where the face slit was, the robe becomes the violet cloak with the constellation pattern kept as tiny white stars. Keep unchanged: the glowing cyan crystalline object in the center, the flying brown bone-like debris, the red radial background. Character designs: [PYRO JACK] = JACK-O'-LANTERN (Pyro Jack): carved ORANGE ribbed pumpkin head with black crescent eyes and a jagged grin, deep-violet floppy witch hat with a buckled band, violet cloak with a round button, small GREY cartoon mitten hands with short rounded lobes (no human fingers or fingernails).
```

#### 19. `convergence` 汇流

- 效果：在本回合保留你的手牌。 在下个回合， 获得{Energy:energyIcons()}与{Stars:starIcons()}。
- 换什么：中间侧身举着魔杖的 Regent → 南瓜灯杰克（同姿势，魔杖是细银杖不变）；右上角那只小棕手 → 杰克的灰色连指手；青紫色轨道线、深紫背景不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/convergence.png`
- 参考图：art/cards/v4/local_edits/batch_04/kingly_punch.png (Pyro Jack)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace ONLY the Regent in the center (blue robe, pointed brown hat, raised arm holding a thin wand with a sparkle tip at the upper left) with PYRO JACK in the same pose, scale and orientation, grey mitten gripping the same thin silver wand. The small brown hand at the upper right becomes PJ's other grey mitten. Keep the cyan, white and violet orbit lines, the wand sparkle and the dark violet background unchanged. Character designs: [PYRO JACK] = JACK-O'-LANTERN (Pyro Jack): carved ORANGE ribbed pumpkin head with black crescent eyes and a jagged grin, deep-violet floppy witch hat with a buckled band, violet cloak with a round button, small GREY cartoon mitten hands with short rounded lobes (no human fingers or fingernails).
```

#### 20. `cosmic_indifference` 宇宙冷漠

- 效果：获得{Block:diff()}点格挡。 将弃牌堆中的一张牌放到你的抽牌堆的顶部。
- 换什么：正面坐着的巨大 Regent（蓝色星纹长袍、顶部金色徽记）→ 正面坐着的南瓜灯杰克（长袍变紫色、星纹保留为小白星）；黄色闪电、星空背景不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/cosmic_indifference.png`
- 参考图：art/cards/v4/local_edits/batch_04/kingly_punch.png (Pyro Jack)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace ONLY the large frontal seated Regent (hooded blue robe with star patterns, gold emblem at the top, brown knees) with PYRO JACK seated frontally at the same huge scale: pumpkin face inside the hood area, deep-violet witch hat in place of the pointed hood, the robe becomes the violet cloak with the star pattern kept as small white stars and the same cyan rim light, grey mitten hands resting where the old hands were. Keep unchanged: the yellow lightning bolts, the starry blue background, the glowing emblems at the sides (only the gold crest at the top may become a small silver crescent). Character designs: [PYRO JACK] = JACK-O'-LANTERN (Pyro Jack): carved ORANGE ribbed pumpkin head with black crescent eyes and a jagged grin, deep-violet floppy witch hat with a buckled band, violet cloak with a round button, small GREY cartoon mitten hands with short rounded lobes (no human fingers or fingernails).
```

### 第 5 批 · Pyro Jack 南瓜灯 2

#### 21. `crash_landing` 迫降

- 效果：对所有敌人造成{Damage:diff()}点伤害。 用碎屑填满你的手牌。
- 换什么：斜着砸进地面的 Regent → 头朝下俯冲的南瓜灯杰克；白色爆炸、碎石、粉色敌人残骸、红色背景不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/crash_landing.png`
- 参考图：art/cards/v4/local_edits/batch_04/kingly_punch.png (Pyro Jack)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace ONLY the armored Regent diving diagonally into the ground at the upper center with PYRO JACK in the same diagonal dive pose, scale and trajectory, comic wide-eyed grin, cloak and hat streaming behind. Keep unchanged: the white-cyan impact explosion, the cracked ground and cream rubble, the pink debris of the enemies at the right, the red background. Character designs: [PYRO JACK] = JACK-O'-LANTERN (Pyro Jack): carved ORANGE ribbed pumpkin head with black crescent eyes and a jagged grin, deep-violet floppy witch hat with a buckled band, violet cloak with a round button, small GREY cartoon mitten hands with short rounded lobes (no human fingers or fingernails).
```

#### 22. `crush_under` 下砸

- 效果：对所有敌人造成{Damage:diff()}点伤害。所有敌人在本回合失去{StrengthLoss:diff()}点力量。
- 换什么：中间蹲着砸地的 Regent（红描边）→ 南瓜灯杰克双拳砸地；绿色梨形敌人、灰色碎石、黄色放射光背景不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/crush_under.png`
- 参考图：art/cards/v4/local_edits/batch_04/kingly_punch.png (Pyro Jack)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace ONLY the crouching hooded Regent at the center (red outline, smashing the ground) with PYRO JACK in the same crouch, both grey mitten fists slamming into the same spot, hat tilted, same scale and red-orange rim light. Keep unchanged: the green pear-shaped enemies, the grey rubble, the white-yellow impact flash, the yellow radial rays and the dark red background. Character designs: [PYRO JACK] = JACK-O'-LANTERN (Pyro Jack): carved ORANGE ribbed pumpkin head with black crescent eyes and a jagged grin, deep-violet floppy witch hat with a buckled band, violet cloak with a round button, small GREY cartoon mitten hands with short rounded lobes (no human fingers or fingernails).
```

#### 23. `decisions_decisions` 抉择，抉择

- 效果：抽{Cards:diff()}张牌。 选择你手牌中的一张技能牌，并将其打出{Repeat:diff()}次。
- 换什么：坐着、伸手指向金色行星的大 Regent → 南瓜灯杰克（灰色连指手指向行星）；金色行星、螺旋、深蓝背景不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/decisions_decisions.png`
- 参考图：art/cards/v4/local_edits/batch_04/kingly_punch.png (Pyro Jack)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace ONLY the large hooded Regent in the upper left (blue robe, pointed brown hat, one brown hand pointing at the golden planets) with PYRO JACK in the same seated pose and scale, pumpkin face under the witch hat, one grey mitten pointing at the same spot. Keep unchanged: the orbiting golden planets with rings, the yellow starburst, the blue-purple spiral and the dark navy background. Character designs: [PYRO JACK] = JACK-O'-LANTERN (Pyro Jack): carved ORANGE ribbed pumpkin head with black crescent eyes and a jagged grin, deep-violet floppy witch hat with a buckled band, violet cloak with a round button, small GREY cartoon mitten hands with short rounded lobes (no human fingers or fingernails).
```

#### 24. `devastate` 葬送

- 效果：造成{Damage:diff()}点伤害。
- 换什么：举起双臂的巨大棕色铠甲 Regent → 巨大的南瓜灯杰克（双手高举）；左上角的小 Regent → 小 Pixie；黄色光束、红色背景不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/devastate.png`
- 参考图：art/cards/v4/local_edits/batch_04/kingly_punch.png (Pyro Jack)；art/cards/v4/local_edits/batch_05/manifest_authority.png (Pixie)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace the huge armored brown Regent in the lower center whose arms are raised toward the upper left with PYRO JACK at the same huge scale and pose, both grey mittens raised, hat tip where the old helmet crest was. The tiny Regent figure at the upper left holding a trident becomes a tiny PIXIE with the same trident. Keep unchanged: the yellow-orange converging light beams, the red-black background shapes. Character designs: [PYRO JACK] = JACK-O'-LANTERN (Pyro Jack): carved ORANGE ribbed pumpkin head with black crescent eyes and a jagged grin, deep-violet floppy witch hat with a buckled band, violet cloak with a round button, small GREY cartoon mitten hands with short rounded lobes (no human fingers or fingernails). [PIXIE] = PIXIE: tiny fairy with short dark-blue bob hair, pointed ears, violet/navy leotard, violet gloves and thigh-high boots, translucent lavender insect wings, natural light skin, small delicate hands.
```

#### 25. `gamma_blast` 伽马爆破

- 效果：造成{Damage:diff()}点伤害。 给予{WeakPower:diff()}层虚弱。 给予{VulnerablePower:diff()}层易伤。
- 换什么：大特写的 Regent 头和双手 → 南瓜灯杰克的南瓜脸（紫巫师帽盖住画面上方）和灰色连指手；中间白色光球和细针不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/gamma_blast.png`
- 参考图：art/cards/v4/local_edits/batch_04/kingly_punch.png (Pyro Jack)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace ONLY the close-up Regent (huge pointed hat covering the top, black face slit, two hands holding the white energy sphere with a needle) with PYRO JACK in the same close-up framing: huge violet witch hat covering the top, glowing pumpkin face where the face slit was, two grey mittens holding the same white sphere and needle. Keep unchanged: the white glowing sphere with its needle, the orange-red background with dark red shapes, the white outline accents. Character designs: [PYRO JACK] = JACK-O'-LANTERN (Pyro Jack): carved ORANGE ribbed pumpkin head with black crescent eyes and a jagged grin, deep-violet floppy witch hat with a buckled band, violet cloak with a round button, small GREY cartoon mitten hands with short rounded lobes (no human fingers or fingernails).
```

### 第 6 批 · Pyro Jack 南瓜灯 3

#### 26. `heavenly_drill` 天际钻头

- 效果：造成{Damage:diff()}点伤害X次。 如果X的最终数值为{Energy:diff()}或以上，则将X的数值翻倍。
- 换什么：左边趴着的 Regent（蓝袖、棕帽、手握钻头）→ 南瓜灯杰克（灰色连指手握同一个钻头）；金色钻头、金色星形帆、红色背景不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/heavenly_drill.png`
- 参考图：art/cards/v4/local_edits/batch_04/kingly_punch.png (Pyro Jack)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace ONLY the Regent lying and lunging at the left (blue sleeve, brown pointed hat, hand gripping the drill) with PYRO JACK in the same lunging pose and scale, pumpkin face where the face was, grey mitten gripping the same drill handle. Keep unchanged: the white-and-gold striped drill, the golden four-point sail shape behind it, the sparkles, the red-orange background. Character designs: [PYRO JACK] = JACK-O'-LANTERN (Pyro Jack): carved ORANGE ribbed pumpkin head with black crescent eyes and a jagged grin, deep-violet floppy witch hat with a buckled band, violet cloak with a round button, small GREY cartoon mitten hands with short rounded lobes (no human fingers or fingernails).
```

#### 27. `knockout_blow` 决胜一击

- 效果：造成{Damage:diff()}点伤害。 如果此牌击杀了敌人，获得{Stars:starIcons()}。
- 换什么：左边出拳的 Regent → 南瓜灯杰克出拳；中间的青色线框拳头、白色骷髅月亮敌人、金币堆、红色背景闪电不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/knockout_blow.png`
- 参考图：art/cards/v4/local_edits/batch_04/kingly_punch.png (Pyro Jack)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace ONLY the Regent at the left (brown pointed hat, blue robe, punching toward the pale skull-moon enemy) with PYRO JACK at the same size and place, throwing the same punch with a grey mitten. Keep unchanged: the cyan wireframe fist, the pale skull-moon enemy with the white-yellow lightning, the coin stack, the green rubble, the red background. Character designs: [PYRO JACK] = JACK-O'-LANTERN (Pyro Jack): carved ORANGE ribbed pumpkin head with black crescent eyes and a jagged grin, deep-violet floppy witch hat with a buckled band, violet cloak with a round button, small GREY cartoon mitten hands with short rounded lobes (no human fingers or fingernails).
```

#### 28. `plot` 筹划

- 效果：下一回合，所有玩家抽{Cards}张牌。
- 换什么：右边那个 Regent（兜帽、锁链、蓝袍）→ 南瓜灯杰克（掌心托着星座牌）；左边的骷髅同伴、发光星座牌、紫色背景不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/plot.png`
- 参考图：art/cards/v4/local_edits/batch_04/kingly_punch.png (Pyro Jack)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace ONLY the Regent on the right (hooded pointed head with a chain, blue robe, open hand under the glowing tile) with PYRO JACK in the same position, scale and pose: pumpkin face and witch hat, one grey mitten open under the glowing constellation tile exactly where the old hand was. Keep unchanged: the skeleton companion with blue flames on the left, the glowing green-cyan constellation tile and its sparks, the purple background shapes. Character designs: [PYRO JACK] = JACK-O'-LANTERN (Pyro Jack): carved ORANGE ribbed pumpkin head with black crescent eyes and a jagged grin, deep-violet floppy witch hat with a buckled band, violet cloak with a round button, small GREY cartoon mitten hands with short rounded lobes (no human fingers or fingernails).
```

#### 29. `resonance` 共鸣

- 效果：获得{StrengthPower:diff()}点力量。所有敌人失去1点力量。
- 换什么：斜靠着、手举向左上的 Regent（蓝袖、螺纹头）→ 南瓜灯杰克（灰色连指手举起）；黄绿色同心波纹、黑色背景不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/resonance.png`
- 参考图：art/cards/v4/local_edits/batch_04/kingly_punch.png (Pyro Jack)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace ONLY the Regent leaning in from the lower left (blue sleeve, hooded head with a spiral pattern, raised hand at the top left) with PYRO JACK in the same pose, scale and position, grey mitten raised in the same place. Keep unchanged: the yellow and green concentric ripple waves, the black corner shape, the white glow lines. Character designs: [PYRO JACK] = JACK-O'-LANTERN (Pyro Jack): carved ORANGE ribbed pumpkin head with black crescent eyes and a jagged grin, deep-violet floppy witch hat with a buckled band, violet cloak with a round button, small GREY cartoon mitten hands with short rounded lobes (no human fingers or fingernails).
```

#### 30. `wrought_in_war` 战火铸就

- 效果：造成{Damage:diff()}点伤害。 铸造{Forge:diff()}。
- 换什么：左边的 Regent（尖帽）→ 南瓜灯杰克握着长剑，剑柄换成结城理的剑柄；右侧蓝色穹顶和握剑的蓝色护手（红描边）、青色光刃、红色背景不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/wrought_in_war.png`
- 参考图：art/cards/v4/local_edits/batch_04/kingly_punch.png (Pyro Jack)；art/parts_src/sword.png (Makoto sword)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace ONLY the Regent at the left (pointed brown hat-head and robe holding the long glowing blade) with PYRO JACK in the same pose and scale, grey mitten gripping the same place, pumpkin face under the witch hat; the lower-left hilt end of the blade takes the hilt and round pommel of MAKOTO SWORD. Keep unchanged: the long glowing cyan blade with its white shards, the blue-teal dome and the blue armored fist with the red outline at the right, the red background. Character designs: [PYRO JACK] = JACK-O'-LANTERN (Pyro Jack): carved ORANGE ribbed pumpkin head with black crescent eyes and a jagged grin, deep-violet floppy witch hat with a buckled band, violet cloak with a round button, small GREY cartoon mitten hands with short rounded lobes (no human fingers or fingernails). [MAKOTO SWORD] = Makoto's sword (asset art/parts_src/sword.png): plain SILVER straight single-edge tapered blade with steel-grey bevel, simple narrow silver crossguard, black leather-wrapped grip, round silver pommel, no gold ornament, gems or crown.
```

### 第 7 批 · Pixie 小精灵

#### 31. `arsenal` 武器库

- 效果：每当你生成一张牌，就获得{ArsenalPower:diff()}点力量。
- 换什么：画面中央那个挥武器的小 Regent → 小精灵 Pixie（同姿势同大小）；他手里的武器 → 结城理的剑；金色飞盘、闪电、石板、背景、右下敌人手不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/arsenal.png`
- 参考图：art/cards/v4/local_edits/batch_05/manifest_authority.png (Pixie)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace ONLY the small Regent figure at the center (blue robe, pointed brown hat) who thrusts a weapon toward the right with PIXIE in the same pose, scale and screen position: leaping/flying, one small violet-gloved hand gripping the weapon, wings opening where his robe flares. The weapon in his hand becomes MAKOTO SWORD at the same angle and length. Keep unchanged: the floating orange-gold spinning discs, the other floating blades and spears, the yellow-orange lightning, the grey stone slab behind (restore the slab where his hat and hands overlapped it), the dark navy background and the creature hand at the lower right. Character designs: [PIXIE] = PIXIE: tiny fairy with short dark-blue bob hair, pointed ears, violet/navy leotard, violet gloves and thigh-high boots, translucent lavender insect wings, natural light skin, small delicate hands. [MAKOTO SWORD] = Makoto's sword (asset art/parts_src/sword.png): plain SILVER straight single-edge tapered blade with steel-grey bevel, simple narrow silver crossguard, black leather-wrapped grip, round silver pommel, no gold ornament, gems or crown.
```

#### 32. `comet` 彗星

- 效果：造成{Damage:diff()}点伤害。 给予{WeakPower:diff()}层虚弱。 给予{VulnerablePower:diff()}层易伤。
- 换什么：左下角那个很小的灰色持杖 Regent → 小 Pixie 指向绿鸟；绿色大鸟、蓝色彗星拖尾、棕色背影、橙色背景不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/comet.png`
- 参考图：art/cards/v4/local_edits/batch_05/manifest_authority.png (Pixie)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace ONLY the small grey armored Regent at the lower left (holding a staff toward the green bird) with a tiny PIXIE in the same pose and position, pointing a small violet-gloved hand toward the bird; the staff becomes a thin silver rod with the same tip. Keep unchanged: the big green bird, the blue-white comet streak at the upper right, the brown shape behind the bird, the orange background. Character designs: [PIXIE] = PIXIE: tiny fairy with short dark-blue bob hair, pointed ears, violet/navy leotard, violet gloves and thigh-high boots, translucent lavender insect wings, natural light skin, small delicate hands.
```

#### 33. `child_of_the_stars` 群星之子

- 效果：每当你花费{singleStarIcon}时，每花费一点{singleStarIcon}，获得{BlockForStars:diff()}点格挡。
- 换什么：中间背对镜头的 Regent → 背对镜头的 Pixie（同身高同位置）；两侧晶体星形翅膀、紫色背景不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/child_of_the_stars.png`
- 参考图：art/cards/v4/local_edits/batch_05/manifest_authority.png (Pixie)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace ONLY the Regent seen from behind in the center-left (blue robe, pointed hat) with PIXIE seen from behind at the same height and position, arms down, short dark-blue hair visible, her small insect wings mostly hidden behind the existing large white-cyan crystal star-shaped wings. Keep the crystal wings, the ice shards and the purple background exactly. Character designs: [PIXIE] = PIXIE: tiny fairy with short dark-blue bob hair, pointed ears, violet/navy leotard, violet gloves and thigh-high boots, translucent lavender insect wings, natural light skin, small delicate hands.
```

#### 34. `make_it_so` 如此甚好

- 效果：造成{Damage:diff()}点伤害。 你每在一回合内打出{Cards:diff()}张技能牌，就将这张牌放入你的手牌。
- 换什么：左边大尺寸的 Regent（兜帽、蓝袍、手势）→ Pixie 向前指挥；青色星形彗星弹幕、红色背景不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/make_it_so.png`
- 参考图：art/cards/v4/local_edits/batch_05/manifest_authority.png (Pixie)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace ONLY the large Regent at the left (hat and hood filling the top, blue robe with chain, one hand gesturing) with PIXIE at the same huge scale: face and short blue hair filling the area of the hood, one violet-gloved hand making the same commanding gesture, wing edges visible behind the shoulder. Keep unchanged: the streaming cyan star-shaped comets, the red background shapes, the dark grey shape at the top left. Character designs: [PIXIE] = PIXIE: tiny fairy with short dark-blue bob hair, pointed ears, violet/navy leotard, violet gloves and thigh-high boots, translucent lavender insect wings, natural light skin, small delicate hands.
```

#### 35. `neutron_aegis` 中子护盾

- 效果：获得{PlatingPower:diff()}层覆甲。
- 换什么：被原子环绕的 Regent（粉脸、蓝袍）→ 被同样环绕的 Pixie；白色轨道环和灰色球体、深蓝背景不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/neutron_aegis.png`
- 参考图：art/cards/v4/local_edits/batch_05/manifest_authority.png (Pixie)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace ONLY the Regent at the center-left (pointed hat, pink face, blue robe, hand over the face) with PIXIE in the same curled pose, scale and position, wings folded behind. Keep unchanged: the white orbit rings with their grey spheres, the white lightning edges, the dark navy background. Character designs: [PIXIE] = PIXIE: tiny fairy with short dark-blue bob hair, pointed ears, violet/navy leotard, violet gloves and thigh-high boots, translucent lavender insect wings, natural light skin, small delicate hands.
```

### 第 8 批 · Jack Frost 杰克冰人

#### 36. `astral_pulse` 星界脉冲

- 效果：对所有敌人造成{Damage:diff()}点伤害两次。
- 换什么：右上方蜷在粉色星球上的金色 Regent → 圆滚滚的杰克冰人（同轮廓）；左边那只小棕手 → 冰人另一只白色连指手；粉色星球和背景不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/astral_pulse.png`
- 参考图：art/cards/v4/local_edits/batch_04/kingly_kick.png (Jack Frost)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace ONLY the golden-olive hooded Regent figure curled over the pink planet at the upper right with JACK FROST curled into a round ball on the planet horizon, inside the same silhouette, size and position (the jester hat points sit where the Regent's hood tip was and do not extend past the old silhouette). The small brown hand at the left becomes JACK FROST's other small white mitten reaching up, same position and gesture. Keep the magenta-pink planet with its white sparkles and the purple background exactly. Character designs: [JACK FROST] = JACK FROST: compact round ivory-white snowman demon, vivid blue three-point jester hat with white pompoms, blue collar with small bells, wide happy black oval eyes and open grin, stubby plump rounded MITTEN hands (thumb nub, at most two simple finger separations), small dark-blue boots, white planes with lavender-grey shadow planes.
```

#### 37. `foregone_conclusion` 既定事项

- 效果：在下个回合，从你的抽牌堆中选择{Cards:diff()}张牌放入你的手牌。
- 换什么：举着卷轴的棕色大 Regent 头 → 从卷轴后探出的杰克冰人（圆脑袋加小丑帽）；卷轴上的绿色星座、背景不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/foregone_conclusion.png`
- 参考图：art/cards/v4/local_edits/batch_04/kingly_kick.png (Jack Frost)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace ONLY the large brown Regent head and hands peeking behind the scroll on the left with JACK FROST peeking over the same scroll edge, round white head with jester hat, two white mitten hands holding the scroll's rolled ends at the same positions. Keep unchanged: the scroll with its green constellation diagrams, its yellow-green outline, the olive green background. Character designs: [JACK FROST] = JACK FROST: compact round ivory-white snowman demon, vivid blue three-point jester hat with white pompoms, blue collar with small bells, wide happy black oval eyes and open grin, stubby plump rounded MITTEN hands (thumb nub, at most two simple finger separations), small dark-blue boots, white planes with lavender-grey shadow planes.
```

#### 38. `i_am_invincible` 所向无敌

- 效果：获得{Block:diff()}点格挡。 在你的回合结束时，如果这张牌位于抽牌堆顶部，则将其打出。
- 换什么：青色线框英雄姿势的 Regent → 同样线框网格风格的杰克冰人（圆身体、两臂举起秀肌肉）；右上金色飞行物、紫色背景不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/i_am_invincible.png`
- 参考图：art/cards/v4/local_edits/batch_04/kingly_kick.png (Jack Frost)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace ONLY the cyan wireframe Regent striking a flexing pose with a wireframe JACK FROST: the same cyan wire-mesh style with the same node dots and lightning crackle, the body silhouette becomes Jack Frost's round body with jester hat points where the Regent's hat point was, both short arms flexed in the same two positions. Keep unchanged: the golden floating device at the upper right, the purple background and its shapes. Character designs: [JACK FROST] = JACK FROST: compact round ivory-white snowman demon, vivid blue three-point jester hat with white pompoms, blue collar with small bells, wide happy black oval eyes and open grin, stubby plump rounded MITTEN hands (thumb nub, at most two simple finger separations), small dark-blue boots, white planes with lavender-grey shadow planes.
```

#### 39. `seeking_edge` 追踪之刃

- 效果：铸造{Forge:diff()}。 君王之剑现在会对所有敌人造成伤害。
- 换什么：上方倒挂的巨大 Regent 头 → 倒挂的杰克冰人脑袋（小丑帽尖朝下，替代原来向下的帽尖）；下方带眼睛的橙色尖刺剑和红色激光线不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/seeking_edge.png`
- 参考图：art/cards/v4/local_edits/batch_04/kingly_kick.png (Jack Frost)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace ONLY the huge inverted Regent head at the top center (brown hat pointing downward) with JACK FROST's head hanging upside down in the same silhouette and size: jester hat points hang downward where the old hat point was, round white face with the wide grin, small white mitten hands only if they were visible. Keep unchanged: the orange spiked eye blade at the bottom, the red laser lines, the dark violet-blue background and its sparkles. Character designs: [JACK FROST] = JACK FROST: compact round ivory-white snowman demon, vivid blue three-point jester hat with white pompoms, blue collar with small bells, wide happy black oval eyes and open grin, stubby plump rounded MITTEN hands (thumb nub, at most two simple finger separations), small dark-blue boots, white planes with lavender-grey shadow planes.
```

#### 40. `meteor_shower` 流星雨

- 效果：对所有敌人造成{Damage:diff()}点伤害。 给予所有敌人{WeakPower:diff()}层虚弱和易伤。
- 换什么：爆炸中心很小的红色人影 → 同样红橙逆光剪影的小杰克冰人；蓝白爆炸、岩石、竖构图全部不动。（原图是 606×852 竖图，保持竖图！）
- Image1：`research/regent_assets/images/packed/card_portraits/regent/meteor_shower.png`
- 参考图：art/cards/v4/local_edits/batch_04/kingly_kick.png (Jack Frost)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. IMPORTANT: Image1 is a PORTRAIT 606x852 illustration; keep the output at 606x852 portrait with identical framing (do not convert to landscape). Replace ONLY the tiny red backlit silhouette of the Regent with raised arms in the center of the blast with a tiny JACK FROST silhouette of the same size and position, same red-orange backlit shading, arms raised, jester hat points visible. Keep unchanged: the blue-white explosion, the falling rocks, the dark grey stones, the magenta ground. Character designs: [JACK FROST] = JACK FROST: compact round ivory-white snowman demon, vivid blue three-point jester hat with white pompoms, blue collar with small bells, wide happy black oval eyes and open grin, stubby plump rounded MITTEN hands (thumb nub, at most two simple finger separations), small dark-blue boots, white planes with lavender-grey shadow planes.
```

### 第 9 批 · mixed 混合

#### 41. `conqueror` 征服者

- 效果：铸造{Forge:diff()}。 君王之剑在本回合对敌人造成双倍伤害。
- 换什么：巨大的灰色爪形护手 → 结城理的手（同样张开前探的姿势，袖口从下边缘进入）；顶部中间漂浮的小 Regent 头 → 小杰克冰人；左边青色发光的剑加上结城理剑的形状；红色背景不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/conqueror.png`
- 参考图：art/cards/v4/local_edits/batch_01/know_thy_place.png + mod/pck_stage/p3makoto/ui/charselect_portrait.png (Makoto hand/uniform only)；art/cards/v4/local_edits/batch_04/kingly_kick.png (Jack Frost)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace the huge grey clawed gauntlet in the center (fingers spread, reaching toward the viewer) with MAKOTO HAND in the same pose and scale, sleeve entering from the bottom edge. The tiny floating Regent head with the orange four-point hat on the small rock at the top center becomes a tiny JACK FROST sitting on the same rock. The glowing cyan blade with white shards at the left keeps its cyan magic and shard positions but takes the blade silhouette and hilt of MAKOTO SWORD. NO Makoto head or face. Keep the red background, its radial shapes and the dark claws-shaped shadow shapes unchanged. Character designs: [JACK FROST] = JACK FROST: compact round ivory-white snowman demon, vivid blue three-point jester hat with white pompoms, blue collar with small bells, wide happy black oval eyes and open grin, stubby plump rounded MITTEN hands (thumb nub, at most two simple finger separations), small dark-blue boots, white planes with lavender-grey shadow planes. [MAKOTO HAND] = MAKOTO YUKI's natural warm pale bare hand(s) with black school-blazer sleeve and white shirt cuff (a small red armband only if the upper arm is visible); NO Makoto face, head, hair or neck anywhere, sleeves leave the canvas at the same edges where the original arms did. [MAKOTO SWORD] = Makoto's sword (asset art/parts_src/sword.png): plain SILVER straight single-edge tapered blade with steel-grey bevel, simple narrow silver crossguard, black leather-wrapped grip, round silver pommel, no gold ornament, gems or crown.
```

#### 42. `terraforming` 地形改造

- 效果：获得{VigorPower:diff()}点活力。
- 换什么：正面的 Regent 头（尖帽）和捏着星形网格的手 → Pixie（捏着同一块星形网格）；青色星形网格、蓝色背景不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/terraforming.png`
- 参考图：art/cards/v4/local_edits/batch_05/manifest_authority.png (Pixie)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace ONLY the Regent in the center (pointed hat-head, blue sleeve, brown hand pinching the cyan star-shaped grid tile) with PIXIE in the same position and scale: face partly hidden behind the tile like the original, short blue hair, one small violet-gloved hand pinching the same tile at the same spot. Keep unchanged: the cyan star-shaped grid tiles, the blue sky gradient and its shapes. Character designs: [PIXIE] = PIXIE: tiny fairy with short dark-blue bob hair, pointed ears, violet/navy leotard, violet gloves and thigh-high boots, translucent lavender insect wings, natural light skin, small delicate hands.
```

#### 43. `orbit` 环绕轨道

- 效果：你每花费{energyPrefix:energyIcons(4)}， 就获得{Energy:energyIcons()}。
- 换什么：左上和右下两块棕色圆形指尖（Regent 护手）→ 南瓜灯杰克灰色连指手的指尖；青色星形轨道环、绿色背景不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/orbit.png`
- 参考图：art/cards/v4/local_edits/batch_04/kingly_punch.png (Pyro Jack)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. The two brown rounded shapes at the upper-left and lower-right corners are the Regent's gauntlet fingertips holding the ring. Replace ONLY these two shapes with the fingertips of PYRO JACK's grey mitten hands in the same positions, sizes and angles (rounded mitten lobes, no human fingernails). Keep unchanged: the cyan star orbit ring with the glowing core, the white sparkle outlines, the dark green background. Character designs: [PYRO JACK] = JACK-O'-LANTERN (Pyro Jack): carved ORANGE ribbed pumpkin head with black crescent eyes and a jagged grin, deep-violet floppy witch hat with a buckled band, violet cloak with a round button, small GREY cartoon mitten hands with short rounded lobes (no human fingers or fingernails).
```

#### 44. `collision_course` 碰撞轨迹

- 效果：造成{Damage:diff()}点伤害。 将一张碎屑添加至你的手牌。
- 换什么：旋转滚动的棕绿色团块里，如果有 Regent 的铠甲片段，则换成蜷成球滚动的杰克冰人（同弧线同大小）；绿色史莱姆、紫粉色晶体碎片、奶油色拖影、红色背景不动。（这张原图比较难辨认，先看一眼再决定要不要做）
- Image1：`research/regent_assets/images/packed/card_portraits/regent/collision_course.png`
- 参考图：art/cards/v4/local_edits/batch_04/kingly_kick.png (Jack Frost)

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. In the rolling brown-and-green mass at the center, identify the Regent's armored brown segments (the segments with the four-point hat shape and gold trim) and replace ONLY those with JACK FROST tucked into a ball, rolling along the same arc at the same size; its jester hat points trail along the motion. Keep unchanged: the green slime blobs, the purple and pink crystal shards, the cream-white swoosh arcs, the dark red background. If no Regent part can be identified, leave the image untouched. Character designs: [JACK FROST] = JACK FROST: compact round ivory-white snowman demon, vivid blue three-point jester hat with white pompoms, blue collar with small bells, wide happy black oval eyes and open grin, stubby plump rounded MITTEN hands (thumb nub, at most two simple finger separations), small dark-blue boots, white planes with lavender-grey shadow planes.
```

#### 45. `pillar_of_creation` 创世之柱

- 效果：你每次生成卡牌时，获得{Block:diff()}点格挡。
- 换什么：按你之前的指令：石碑 → 天鹅绒房间的蓝门（同位置同轮廓，门缝透光），去掉抱着石碑的 Regent，只剩背景；右侧红色飘形、紫蓝背景、左边深灰岩石不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/pillar_of_creation.png`

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Follow the user's standing decision for this card: no character. Replace the tall cyan monolith in the center with a Velvet Room DOOR in the same position, height and silhouette: ornate royal-blue velvet-panelled double door with thin silver filigree frame, a small silver keyhole plate, a soft cold-white light leaking from the center seam. Remove the Regent entirely (the brown pointed hat-head at the left and the blue arm wrapped around the pillar) and refill that area with the original dark grey rock and the blue-violet background shapes. Keep unchanged: the dark grey rock at the left edge, the red feathery shapes at the right, the vivid blue-violet background.
```

### 第 10 批 · symbols 符号/物件

#### 46. `void_form` 虚空形态

- 效果：结束你的回合。 你可以免费打出每回合的前{VoidFormPower:diff()}张牌。
- 换什么：头顶一圈四角星形光瓣 → 一对简洁的天鹅绒房间蓝蝶翅膀（只要一对大翅膀，不要碎片）；黑色身影、星系漩涡、棕色星座背景不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/void_form.png`

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Replace ONLY the radiating cyan four-point star petals and ring around the black figure's head with ONE clean pair of large Velvet Room blue butterfly wings spread behind the head, same overall footprint and glow color, simple big wing shapes with a few vein lines — no scattered shards, no flower-like petals. Keep unchanged: the black figure silhouette with its cyan outline, the small swirling galaxy in its hands, the brown background with its constellation lines.
```

#### 47. `alignment` 星位序列

- 效果：获得{Energy:energyIcons()}。
- 换什么：没有角色。只把三颗四角星和星座连线换成天鹅绒房间的银丝边加皇家蓝材质；形状、位置、绿色背景全部不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/alignment.png`

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. There is NO character in Image1. Change ONLY the finish of the three large four-point stars and their thin constellation links: keep every star silhouette, size, position, facet layout and link route exactly. New finish: thin silver filigree rim, deep royal-blue and ivory facets, a tiny engraved crescent-moon motif on the largest star's blue facet; the links become fine silver chains. Keep unchanged: the green background, its dark leaf shapes and its glow.
```

#### 48. `charge` 冲锋！！

- 效果：选择你抽牌堆中的{Cards:diff()}张牌，将其变化为 {IfUpgraded:show:仆从俯冲+|仆从俯冲}。
- 换什么：没有角色。橙棕色的弯曲翼形（Regent 的形状）→ 深蓝天鹅绒配银边；中间金色四角星和青绿色边线不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/charge.png`

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. There is NO character in Image1. Change ONLY the material of the large orange-brown curved four-wing shape around the center: keep its silhouette and shading structure exactly, but make it deep royal-blue velvet with thin silver filigree edging and soft hard-edged velvet fold planes. Keep unchanged: the golden four-point star in the center, the teal-green edge lines, the dark navy background.
```

#### 49. `the_sealed_throne` 封印王座

- 效果：你每打出一张牌，获得{singleStarIcon}。
- 换什么：（竖图 606×852）漂浮岛上的金色王座 → 伊戈尔的蓝色天鹅绒高背椅（同位置同大小同轮廓）；岛、裂开的石框、星空不动。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/the_sealed_throne.png`

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. IMPORTANT: Image1 is a PORTRAIT 606x852 illustration; keep the output at 606x852 portrait with identical framing (do not convert to landscape). Replace ONLY the small golden throne on the floating rock island in the center with Igor's Velvet Room high-back armchair: royal-blue tufted velvet wingback with gold studs and a silver-trimmed base, same position, height and silhouette footprint, a small cyan star glint kept at its top. Keep unchanged: the floating rock island, the cracked dark stone frame around it, the deep blue starry sky and its constellation lines.
```

### 可选 · 原图无 Regent 的两张装饰

#### `black_hole` 黑洞

- 换什么：只有敌人和青色闪电环，无 Regent。可选：左下角棕色长方块 → P3R 的黑色棺材（同位置同形状）。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/black_hole.png`

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Optional accent only. Image1 has no Regent. Replace ONLY the brown rectangular slab at the lower left with a P3R Dark Hour COFFIN in the same position, shape and scale: near-black coffin lid with thin steel-blue edge lines and a small silver crescent mark, same cast shadow. Keep unchanged: the teal one-eyed enemy, the cyan lightning ring, the dark vines, the red-orange background.
```

#### `bombardment` 轰击

- 换什么：只有燃烧的陨石和青色光环，无 Regent。可选：把环绕陨石的青色光环换成一圈小塔罗牌（同路径同大小）；建议保持原图。
- Image1：`research/regent_assets/images/packed/card_portraits/regent/bombardment.png`

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET and composition master. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything. Make LOCAL replacements only, at the same positions, silhouette footprints and scale as the replaced parts. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind as props, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. New P3R characters use their canonical colors. Makoto Yuki's face and head must never appear (hands and sleeves only). Persona hands are plump cartoon mittens with no long human fingers, knuckles or fingernails. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760 unless this prompt says portrait, no text, letters, numbers, frame, UI or watermark. Optional accent only. Image1 has no Regent. Replace ONLY the thin cyan ring orbiting the fireball with a ring of tiny tarot-card backs (royal-blue with a silver crescent, no text) following the same ring path, tilt and size. Keep unchanged: the fiery meteor with its cyan-white core, the speed streaks, the dark red background.
```

## 5. 修订提示词模板（某张被你否定后用）

把"否定原因"填进 `<COMPLAINT>`，Image1 用**被否定的那张新图**，Image2 用**原版图**：

```
Use case: precise-object-edit. Image1 is the CURRENT candidate and the edit target; Image2 is the ORIGINAL card for composition and background reference. User complaint: <COMPLAINT>. Fix ONLY what the complaint names, with the smallest possible local change. Everything else in Image1 stays pixel-identical in position, scale and colors: background, composition, effects, enemies, props, the other characters. Keep the original's hard cel-shading, crisp dark outlines and near-black occlusion planes. Persona hands are plump cartoon mittens, no long human fingers. Makoto Yuki's face and head never appear. Opaque, full-bleed, same size as Image1, no text, frame, UI or watermark.
```

常见否定原因对应的补充句：
- 手太人类 → `Persona hands are small plump mittens: short thumb nub, broad rounded mass, at most two simple finger separations, no knuckles, tendons or fingernails.`
- 位置/构图变了 → `Place every element exactly where it is in Image2 (original); trace the original's hand and body footprints and overlaps.`
- 改动太大 → `This is a very small local change; lock all existing shapes, colors and positions listed above exactly.`
- 背景被动了 → `Restore the background shapes and colors exactly as in Image2 wherever they were altered.`
