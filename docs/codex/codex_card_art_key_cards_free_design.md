# 关键牌自由设计：不管原版，按我的想法画（6 张）

## 为什么有这一份

用户看完第三轮：还算可以，但我还是太拘束。新规则：**少数关键牌可以不管原版长什么样，按我的想法做**，不用都保留辨识度，也不用沿用底图。"有那么几张就可以，不用太多"。

其他牌（第二轮、第三轮的）照旧，不动。

## 我怎么选的

选牌标准：**稀有 / 远古 / 衍生核心牌**（玩家一定会反复看到），并且**原版插画和 P3R 对不上**（只有敌人、没有 Regent、或者只是几个符号）。入选的 6 张：

| 牌 | 稀有度 | 一句话创意 | 为什么可以不管原版 |
|---|---|---|---|
| `sovereign_blade` 君王之剑 | 衍生牌（Regent 的招牌武器） | 结城理的银剑倒插在棺材田里，剑格上方绽开一圈八把幽灵剑，像一顶王冠。 | 整套卡组的核心，也是这个 mod 的主角武器；本地没有原版插画（只有 v3 和 v4 的重绘稿），原来就没有可保留的东西。 |
| `void_form` 虚空形态 | 稀有 · 能力 | 「黑暗时间」被冻住的一瞬：午夜的大钟，周围凝固的玻璃碎片和悬在半空的塔罗牌，中间一把悬浮的召唤器。没有人物。 | 你点名的；原版只是一个黑色星形人影，没有故事。机制是「结束回合，之后每回合前几张牌免费」，正好是「时间停止」。 |
| `the_sealed_throne` 封印王座 | 远古 · 能力 | 伊戈尔的蓝色天鹅绒高背椅空着，被二十一张塔罗牌围成的椭圆封印锁住；椅子上方独自悬着第二十二张——愚者。 | 远古牌，整套牌最重的一张；原版是一个金色王座，在这个 mod 里没有来由。 |
| `genesis` 创世纪 | 稀有 · 能力 | 一张塔罗牌从中间裂开，金白色的光倾泻而出，俄耳甫斯从裂缝里升起；左下角一只结城理的手刚松开牌角。 | 原版是一只手触碰一颗星（创世纪式构图），在 P3R 里没有对应；「每回合获得星辉」可以对应「每一次选牌，一个人格面具诞生」。 |
| `big_bang` 大爆炸 | 稀有 · 技能 | 从一点白光炸开二十二张大阿尔卡那，近大远小飞向四周，一把结城理的剑混在里面，最大的一张是愚者。 | 「抽牌、能量、星辉、铸造」什么都给一点；原版是两只手加几个符号，比较平。大阿尔卡那同时炸开更有冲击力。 |
| `monarchs_gaze` 王之凝视 | 稀有 · 能力 | 满月变成一只巨大的眼睛（竖瞳）俯视；光柱落下，里面是一只戴着开裂白面具、正在缩小的暗影。 | 原版只有一团绿色史莱姆，没有 Regent；「你攻击的敌人本回合失去力量」= 被注视就萎缩。 |

**我没选的**（原版保留或沿用第二、三轮）：锤子时间、熔炉（你已经通过的）、暴政、七星（可以做但不如以上六张必要）。想加或想换哪张，直接说卡名。

## 版面草图

`art/cards/v4/free_design/layout_sketches.png`：六张的构图色块草图，白框是安全区（x8–92%、y10–76%）。**先看这张图，构图不对的告诉我卡名，我先改构图再让 Codex 画。**

## 规则

- 结城理的脸和头不出现（只允许手和袖口）。这一批里只有 `genesis` 出现一只手。
- 不画文字、数字；塔罗牌上的图案用简单符号。
- 风格对齐现有卡组：硬边赛璐璐、深色描边、大块平涂、缩略图能看清。
- 剑用结城理的剑（`art/parts_src/sword.png`）。
- **每张出 2 个候选（A / B）**，同一个创意，构图或视角不同，让你挑。

## 给 Codex 的执行方式

- 输入：这张卡没有 Image1（没有原图），只附"参考图"里列的文件；风格参考 `art/cards/v4/local_edits/batch_04/kingly_kick.png` 只学画法，不学内容。
- 输出：放 `art/cards/v4/free_design/<卡名>_A.png` 和 `<卡名>_B.png`，尺寸见每张；另存 `prompts_and_sources.json`；状态 `awaiting_user_review`。
- 一次生成一张，不要自动重试，不要自己判断通过。
- 机器可读版本：`art/cards/v4/free_design/key_cards_prompts.json`（提示词已含风格段落）。

## 提示词

### 1. `sovereign_blade` 君王之剑

- 稀有度：衍生牌（Regent 的招牌武器）　尺寸：1000x760　配色：深海军蓝 / 青绿 + 银白
- 创意：结城理的银剑倒插在棺材田里，剑格上方绽开一圈八把幽灵剑，像一顶王冠。
- 参考图：`art/parts_src/sword.png`；`art/cards/v4/local_edits/batch_04/kingly_kick.png（只学画法）`

```
Create ONE new opaque full-bleed horizontal 1000x760 card illustration: a heroic poster of SOVEREIGN BLADE. COMPOSITION (percent of canvas): MAKOTO'S SWORD stands upright, blade pointing down, planted in the ground at the bottom center, tilted about 6 degrees clockwise: pommel at about (51%, 14%), crossguard at about (50%, 30%), blade tip buried at about (48%, 80%). It is the biggest and brightest shape (about 13% of the canvas width at the crossguard). A thin cyan-gold glow runs along its cutting edge. A CROWN OF SPECTRAL BLADES: from a point just behind the grip, EIGHT slimmer translucent ice-cyan ghost copies of the same sword radiate outward like the points of a royal crown or sunburst, fanning across the upper half in an arc of about 140 degrees; the longest two reach about x10% and x90%, the shortest point almost straight up; semi-transparent with bright white edges; all stay inside the safe area. GROUND: a dark plain with two rows of tall narrow black COFFINS (silver trim, pale cyan edge light) standing upright in receding perspective on both sides of the sword, large ones at the lower corners partly cropped, smaller ones fading toward the horizon; a few iron chains lie across the ground; a thin low mist. SKY: deep teal-navy with a few large flat diagonal bands behind the crown and a few four-point star glints; no moon. PALETTE: deep navy and teal about 60%, silver and ice-white, cyan; a single warm gold accent only on the blade-edge glow. AVOID: no people, hands or faces, no Regent star emblem, no gold ornate hilt. Same visual language as the existing card set: bold hard-edged cel-shading, thick dark outlines with a thin bright rim light on the hero shapes, large flat colour planes with a few deep near-black occlusion planes, crisp clean details, a strong poster composition that reads at 250x190 thumbnail size. It is Slay the Spire 2 card art re-skinned for Persona 3 Reload. No blur, no soft bloom, no photoreal rendering, no gradients except subtle sky gradients. Keep every important feature inside x8-92%, y10-76% (the card frame crops the rest). Opaque, full-bleed, no text, letters, numerals, UI, frame, border or watermark. Makoto Yuki's face and head never appear. Generate this as a NEW illustration; there is no original to preserve.
```

### 2. `void_form` 虚空形态

- 稀有度：稀有 · 能力　尺寸：1000x760　配色：Dark Hour 青绿 + 黑 + 冰白，一点粉
- 创意：「黑暗时间」被冻住的一瞬：午夜的大钟，周围凝固的玻璃碎片和悬在半空的塔罗牌，中间一把悬浮的召唤器。没有人物。
- 参考图：`art/cards/v4/local_edits/batch_04/kingly_kick.png（只学画法）`

```
Create ONE new opaque full-bleed horizontal 1000x760 card illustration: VOID FORM, the hidden hour frozen in time. COMPOSITION (percent of canvas): a huge round CLOCK FACE seen almost head-on in the center (center at (50%, 47%), diameter about 62% of the canvas width): dark gunmetal rim with thin silver edging, a deep black face, twelve small four-point-star markers in place of numerals (NO numerals), and both slim silver filigree clock hands pointing straight up at twelve, overlapping. AROUND THE CLOCK a FROZEN EXPLOSION: a ring of hard-edged translucent pale-blue GLASS SHARDS, like a shattered Persona summon, hanging motionless in mid-air, expanding outward from the clock's center, large shards near the edges and tiny ones near the center, with crisp white highlights. Among the shards hang FIVE TAROT CARDS (royal-blue backs with a gold four-point star, no text) at different tilts, each with a tiny cyan glint, positioned roughly at (19%, 27%), (79%, 24%), (15%, 55%), (84%, 53%) and (50%, 15%). At the clock's center floats a small silver EVOKER (a gun-shaped summoner: short barrel ringed with small slots) horizontally, muzzle pointing right, with a tiny hot-pink glint at the muzzle, frozen mid-flash. BACKGROUND: dark teal-green with a few large flat diagonal bands, and a few giant black COFFIN silhouettes as flat dark shapes in the far distance. EVERYTHING IS STILL: no motion blur, no speed lines. PALETTE: Dark Hour teal-green about 40%, black about 25%, ice-white and pale blue glass about 25%, royal-blue cards, one small hot-pink accent. AVOID: no people, no faces, no butterflies or wings, no text or numerals. Same visual language as the existing card set: bold hard-edged cel-shading, thick dark outlines with a thin bright rim light on the hero shapes, large flat colour planes with a few deep near-black occlusion planes, crisp clean details, a strong poster composition that reads at 250x190 thumbnail size. It is Slay the Spire 2 card art re-skinned for Persona 3 Reload. No blur, no soft bloom, no photoreal rendering, no gradients except subtle sky gradients. Keep every important feature inside x8-92%, y10-76% (the card frame crops the rest). Opaque, full-bleed, no text, letters, numerals, UI, frame, border or watermark. Makoto Yuki's face and head never appear. Generate this as a NEW illustration; there is no original to preserve.
```

### 3. `the_sealed_throne` 封印王座

- 稀有度：远古 · 能力　尺寸：606x852（竖图，和原版一样）　配色：靛蓝 / 皇家蓝 + 银 + 金
- 创意：伊戈尔的蓝色天鹅绒高背椅空着，被二十一张塔罗牌围成的椭圆封印锁住；椅子上方独自悬着第二十二张——愚者。
- 参考图：`art/velvet_elevator/base_room.png`；`art/cards/v4/local_edits/batch_04/kingly_kick.png（只学画法）`

```
Create ONE new opaque full-bleed PORTRAIT 606x852 card illustration: THE SEALED THRONE. COMPOSITION (centered, symmetrical): IGOR'S VELVET ROOM ARMCHAIR, a royal-blue tufted high-back wingback with gold studs and silver trim (see art/velvet_elevator/base_room.png for the chair's design), sits EMPTY on a small round silver-and-stone dais at the center (chair centered at (50%, 56%), about 30% of the canvas height). Around it a vertical oval RING of TWENTY-ONE TAROT CARDS floats upright like a halo and a seal (oval about 70% of the width and 62% of the height, centered at (50%, 52%)), each card slightly overlapping the next and facing outward with ornate gold-and-royal-blue faces showing simple stylised arcana emblems (no text, no numerals). From every card a thin iron chain runs inward to the chair's arms and base, twenty-one chains converging in a star pattern to lock it. ABOVE the chair, outside the ring at about (50%, 14%), the TWENTY-SECOND card, THE FOOL, hovers alone, tilted about 10 degrees, glowing bright white-gold, with a soft vertical column of white light falling from it toward the empty seat; this is the only warm light. BACKGROUND: the Velvet Room, deep indigo-navy, silver filigree arches and columns on both sides as flat dark shapes, heavy royal-blue velvet drapes at the top corners, a patterned floor in perspective at the bottom. PALETTE: indigo and royal blue about 55%, silver 15%, gold 15%, white-gold glow. AVOID: no people, no faces, no text, no numerals. Same visual language as the existing card set: bold hard-edged cel-shading, thick dark outlines with a thin bright rim light on the hero shapes, large flat colour planes with a few deep near-black occlusion planes, crisp clean details, a strong poster composition that reads at 250x190 thumbnail size. It is Slay the Spire 2 card art re-skinned for Persona 3 Reload. No blur, no soft bloom, no photoreal rendering, no gradients except subtle sky gradients. Keep every important feature inside x8-92%, y10-76% (the card frame crops the rest). Opaque, full-bleed, no text, letters, numerals, UI, frame, border or watermark. Makoto Yuki's face and head never appear. Generate this as a NEW illustration; there is no original to preserve.
```

### 4. `genesis` 创世纪

- 稀有度：稀有 · 能力　尺寸：1000x760　配色：紫蓝 + 金白
- 创意：一张塔罗牌从中间裂开，金白色的光倾泻而出，俄耳甫斯从裂缝里升起；左下角一只结城理的手刚松开牌角。
- 参考图：`mod/pck_stage/p3makoto/persona/orpheus.png`；`art/cards/v4/local_edits/batch_04/kingly_kick.png（只学画法）`

```
Create ONE new opaque full-bleed horizontal 1000x760 card illustration: GENESIS, the birth of a Persona from a tarot card. COMPOSITION (percent of canvas): at the center (50%, 46%) a single tall TAROT CARD (about 19% of the canvas width, royal-blue and gold, its face a stylised THE FOOL: a small white figure at a cliff edge under a gold sun, no text) floats upright and cracks down its middle; a vertical blade of golden-white light pours out of the crack, and glass-like shards of the card fly outward. Rising out of the crack is the upper body of ORPHEUS (follow the design in mod/pck_stage/p3makoto/persona/orpheus.png exactly), arms open and holding up his lyre, as if stepping out of the card, about as tall as the card. At the lower left, one HAND of Makoto Yuki (pale bare hand, black blazer sleeve with a white cuff) enters from the left edge at about (14%, 78%), the thumb and forefinger just released from the card's lower corner; about 18% of the canvas width; no face, no body, no head. About eighteen tiny golden four-point stars drift upward like sparks in varied sizes. BACKGROUND: deep violet-blue with big flat diagonal bands (violet, indigo, one lighter blue band behind the card) and a soft golden radial glow at the crack. PALETTE: violet and indigo about 55%, gold-white about 25%, royal-blue card about 10%, cyan rim light. AVOID: no faces of Makoto, no text. Same visual language as the existing card set: bold hard-edged cel-shading, thick dark outlines with a thin bright rim light on the hero shapes, large flat colour planes with a few deep near-black occlusion planes, crisp clean details, a strong poster composition that reads at 250x190 thumbnail size. It is Slay the Spire 2 card art re-skinned for Persona 3 Reload. No blur, no soft bloom, no photoreal rendering, no gradients except subtle sky gradients. Keep every important feature inside x8-92%, y10-76% (the card frame crops the rest). Opaque, full-bleed, no text, letters, numerals, UI, frame, border or watermark. Makoto Yuki's face and head never appear. Generate this as a NEW illustration; there is no original to preserve.
```

### 5. `big_bang` 大爆炸

- 稀有度：稀有 · 技能　尺寸：1000x760　配色：深海军蓝 + 白金闪光 + 多色牌
- 创意：从一点白光炸开二十二张大阿尔卡那，近大远小飞向四周，一把结城理的剑混在里面，最大的一张是愚者。
- 参考图：`art/parts_src/sword.png`；`art/cards/v4/local_edits/batch_04/kingly_kick.png（只学画法）`

```
Create ONE new opaque full-bleed horizontal 1000x760 card illustration: BIG BANG, the twenty-two Major Arcana bursting out at once. COMPOSITION (percent of canvas): a radial burst centered at (50%, 48%): a blinding ivory-white and pale-gold flash disc (about 14% of the canvas width) with eight long sharp rays. From it TWENTY-TWO TAROT CARDS fly outward in every direction in strong perspective: small and far near the center, large and near at the edges, each rotated along its flight path, with painted faces showing distinct simple emblems (sun, moon, star, tower, wheel, sword, cup, coin, lantern, scales, chariot, skull and so on) in flat royal blue, gold, cyan and rose-pink; no text or numerals. The nearest, largest card at the lower right at about (79%, 68%) is THE FOOL (a small white figure at a cliff edge under a gold sun). Mixed in among the cards at the upper left, MAKOTO'S SWORD flies outward along a diagonal. Two thin cyan shockwave rings circle the flash, and a few glowing four-point stars sparkle between the cards. BACKGROUND: deep navy with flat radial bands in two blues. PALETTE: navy about 40%, white flash about 15%, gold about 15%, cyan about 10%, with rose-pink and royal-blue card accents. AVOID: no people, hands or faces, no text or numerals. MAKOTO'S SWORD (asset art/parts_src/sword.png): plain silver straight single-edge tapered blade with steel-grey bevel, simple narrow silver crossguard, black leather-wrapped grip, round silver pommel, no gold ornament, gems or crown.Same visual language as the existing card set: bold hard-edged cel-shading, thick dark outlines with a thin bright rim light on the hero shapes, large flat colour planes with a few deep near-black occlusion planes, crisp clean details, a strong poster composition that reads at 250x190 thumbnail size. It is Slay the Spire 2 card art re-skinned for Persona 3 Reload. No blur, no soft bloom, no photoreal rendering, no gradients except subtle sky gradients. Keep every important feature inside x8-92%, y10-76% (the card frame crops the rest). Opaque, full-bleed, no text, letters, numerals, UI, frame, border or watermark. Makoto Yuki's face and head never appear. Generate this as a NEW illustration; there is no original to preserve.
```

### 6. `monarchs_gaze` 王之凝视

- 稀有度：稀有 · 能力　尺寸：1000x760　配色：暗洋红 + 黄绿月 + 黑
- 创意：满月变成一只巨大的眼睛（竖瞳）俯视；光柱落下，里面是一只戴着开裂白面具、正在缩小的暗影。
- 参考图：`art/cards/v4/local_edits/batch_04/kingly_kick.png（只学画法）`

```
Create ONE new opaque full-bleed horizontal 1000x760 card illustration: MONARCH'S GAZE, the Moon as a watching eye. COMPOSITION (percent of canvas): the upper two-thirds are a huge pale FULL MOON (sickly yellow-green, diameter about 70% of the canvas width, centered at (50%, 30%), cropped by the top edge) that is also a giant EYE: a narrow vertical black slit pupil at its center, surrounded by an iris of darker green rings and a few craters acting as eyelid creases, with a thin black upper-eyelid curve across the top. A cold pale cone of light falls straight down from the pupil onto the ground. At the bottom center (50%, 72%), inside the cone, cowers a small SHADOW (a Persona 3 enemy): a black glossy ooze blob with a pale cracked white mask (a stylised smiling mask with a crack running through it) and two thin tentacle arms raised to shield itself, visibly shrinking, with dark drips. GROUND: dark plum, with a few coffins lying as silhouettes at the horizon. SKY: dark magenta-plum with flat diagonal bands and fine cracks radiating from the moon's rim. PALETTE: dark magenta-plum about 40%, sickly yellow-green moon about 25%, black about 25%, bone-white mask about 10%. AVOID: no people, no faces other than the Shadow's mask, no text. Same visual language as the existing card set: bold hard-edged cel-shading, thick dark outlines with a thin bright rim light on the hero shapes, large flat colour planes with a few deep near-black occlusion planes, crisp clean details, a strong poster composition that reads at 250x190 thumbnail size. It is Slay the Spire 2 card art re-skinned for Persona 3 Reload. No blur, no soft bloom, no photoreal rendering, no gradients except subtle sky gradients. Keep every important feature inside x8-92%, y10-76% (the card frame crops the rest). Opaque, full-bleed, no text, letters, numerals, UI, frame, border or watermark. Makoto Yuki's face and head never appear. Generate this as a NEW illustration; there is no original to preserve.
```
