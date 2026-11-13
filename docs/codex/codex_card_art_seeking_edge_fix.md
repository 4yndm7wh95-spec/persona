# 追踪之刃：修正（Koromaru 长了翅膀）

用户：用 Koromaru 挺可爱，但为什么狗长了翅膀。

**原因**：原版倒挂的 Regent 头是一顶左右很宽的帽子。上一版提示词要求 Koromaru 填满原来的轮廓，模型就把这么宽的帽檐画成了两只巨大的耳朵，看起来像翅膀。

**改法**：
- Koromaru 改成从画面上方的边沿倒着探头往下看：两只前爪扣在画面上沿，头倒挂在顶部中间，嘴里叼着苦无指向下面的剑。
- 头只占画面宽度约 22%，就是一只普通大小的柴犬。耳朵是两只小三角耳，因为倒挂所以朝下。明确写了不要翅膀、蝙蝠耳或任何展开的片状物。
- 原来帽子多出来的那片宽轮廓，用原版的深紫蓝背景补上。
- 下面的橙色尖刺眼睛剑、红色激光线和背景都不动。

Image1：`research/regent_assets/images/packed/card_portraits/regent/seeking_edge.png`。输出 `art/cards/v4/local_edits/round3/fix_seeking_edge/seeking_edge.png`，附对比图（原版 | 上一版 | 新版），状态 `awaiting_user_review`，不自动重试。

```
Use case: precise-object-edit. Image1 is the ORIGINAL card illustration and the strict EDIT TARGET. The previous pass only swapped characters and looked plain and standard: this pass must feel INVENTIVE, reinterpreting the replaced subject as a specific Persona 3 Reload moment, character or object, while the card stays instantly recognizable at thumbnail size: same silhouettes, same action, same footprint, same camera. Keep the exact background: its palette, large shapes, lighting direction, cast shadows, magical effects, enemies and core objects stay as in Image1 unless this prompt names them. Do NOT redraw the scene, do NOT recolor globally, do NOT move or rescale anything beyond what is stated. Completely remove the replaced Regent parts (old brown/gold four-point hat-head, gold gauntlets, blue robe, armor) with no remnants left behind, and refill the cleared area with the original background shapes. Match the original's rich hard cel-shading: crisp dark outlines, deep near-black occlusion planes, clear midtone and lit planes, material thickness, purposeful small details; no blur, no soft glow, no photoreal rendering. Makoto Yuki's face and head never appear (hands and sleeves only); other characters' faces are allowed. Persona creatures' hands are plump cartoon mittens with no long human fingers. If a named character's design is uncertain, follow the described features, keep the rest simple, and never invent text. Keep important features inside x8-92%, y10-85% (crop-safe). Output: opaque, full-bleed, horizontal 1000x760, no text, letters, numbers, frame, UI or watermark. Replace ONLY the huge inverted Regent head at the top center (a wide brown hat pointing downward) with KOROMARU peeking down UPSIDE DOWN from a ledge just above the top edge of the frame: his two front paws hooked over the top edge, his head upside down at the top center, a small kunai held in his mouth pointing straight down at the blade below, alert eyes, his dark-red scarf hanging down. IMPORTANT: Koromaru is a normal small Shiba Inu. His head is only about 22% of the canvas width, much narrower than the old hat. His ears are two SMALL triangular Shiba ears pointing downward (because he is upside down). NO wings, NO bat ears, NO large spread flaps or fins of any kind on the head or body. The rest of the old hat's wide silhouette is NOT part of him: refill it with the original dark violet-blue background and its sparkles. Keep unchanged: the orange spiked eye blade at the bottom, the red laser lines, the dark violet-blue background. Character and object designs: [KOROMARU] = KOROMARU: a small white-and-cream Shiba Inu with a dark-red scarf and bright determined eyes.
```

---

## 修订 2：两边空了

用户：Koromaru 不长翅膀了，但两边空了。原版倒挂的帽子是一个很宽的 V 形，从顶部中间向左下、右下张开，把下面那把剑框住；现在 V 形的两翼变成了空背景。

**改法**：用"追踪之刃"本身的意思填满那两翼。这张卡的效果是"君王之剑会对所有敌人造成伤害"，所以从 Koromaru 嘴里的苦无分出两串飞苦无，沿着原来帽檐的 V 形向左下、右下扇形飞出，每把苦无拖着红色的追踪光痕；他的红色围巾两条长尾巴也顺着 V 形往两边飘。Koromaru 本身、下面的剑、红色激光线都不动。

附图：Image1 = 上一版候选 `art/cards/v4/local_edits/round3/fix_seeking_edge/seeking_edge.png`（编辑底图，保留 Koromaru）；Image2 = 原版 `research/regent_assets/images/packed/card_portraits/regent/seeking_edge.png`（只看 V 形的位置和大小）。输出 `seeking_edge_v2.png`，对比图（原版 | 上一版 | 新版）。

```
Use case: precise-object-edit. Image1 is the CURRENT candidate and the edit target: keep Koromaru (head, paws, kunai in his mouth, red scarf knot), the orange spiked eye blade, the red laser lines and the background exactly as they are. Image2 is the ORIGINAL card, used ONLY to locate the wide V shape of the old inverted hat: from the top center it spreads down to the left (to about x8%, y32%) and down to the right (to about x92%, y32%), framing the blade. In Image1 the two wings of that V are now empty background; fill them as follows. (1) TRACKING KUNAI: from the kunai in Koromaru's mouth, two fans of flying kunai sweep outward along the two arms of the V, three or four kunai per side, each smaller than the one in his mouth and getting smaller toward the ends, all pointing along their flight, each trailing a thin glowing red-orange tracking streak that curves along the V; this shows the blade now hitting every enemy. (2) SCARF TAILS: the two loose ends of his dark-red scarf stream out long and wavy along the same V, under the kunai, filling the upper part of each wing; cloth with folds, clearly a scarf, never wings or ears. Together they give the V the same weight as the original hat brim. No wings, no bat ears, no new characters, no text. Match Image1's hard cel-shading and crisp outlines. Opaque, full-bleed, 1000x760, no frame or watermark.
```
