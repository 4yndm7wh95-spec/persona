# 亚森草图（待用户确认）

## 方法与状态

- 内置imagegen生图；不是原版模型三渲二。原版 art/p5/fx/arsene/arsene_still.png 只核对官方设计，Joker final/idle_a3.png 只参考二维画风。
- 共生成2次。第1张被用户指出身体过于修长、姿势怪，保留在 arsene_draft_v1_raw.png，附完整生成记录，不作为定稿。
- 第2张按用户反馈重做：缩短腿和躯干、加宽肩胸，双腿不交叉，直立稳定悬浮，一只手自然前伸、另一只手放松，双翼展开。
- arsene_draft.png 为当前提交的待确认草图，尚未通过，不拆分身体/翅膀/手臂/燕尾/锁链。任务要求到这里停下来等用户确认；八岐大蛇和其他组留待后续。
- 生成原图另存 arsene_draft_raw.png，完整提示词、参考路径和检查数值见 arsene_draft_generation.json。

## 当前草图完整提示词

```text
Use case: stylized-concept. Asset type: transparent 2D Arsene character draft for a Persona 5 Joker mod, revised after user feedback. Image 1 is the original Arsene model render, design reference only. Image 2 is the approved Joker illustration, reference ONLY for bold clean ink and cel-shaded 2D drawing style, do not draw Joker. Draw official Persona 5 Arsene with his red fitted tailcoat, black top hat, red-and-black beaked demonic mask head, white cravat, black waistcoat, gold buttons and hanging chains, red high-heeled boots, two huge black feathered wings with dark crimson inner feathers. USER CORRECTION: avoid an overly long thin stretched body and awkward contorted posture. Make the torso and legs appreciably more compact, chest and shoulders wider, limbs clearly jointed and believable, head about one seventh to one eighth of the complete body height including hat, not a tiny head on extremely long legs. Elegant athletic proportions, not skinny stilt legs, not giant hands. New pose: a calm, balanced upright hovering spellcaster, three-quarter FRONT toward screen right, torso upright and untwisted, shoulders level, hips aligned under ribcage. Two legs hang naturally beneath the hips, knees slightly bent, separated so neither leg crosses or wraps around the other, both boots visible. One arm extends toward screen right at lower-chest height with a gentle elbow bend and a relaxed open casting hand (fingers naturally curved, not a huge claw lunging into the lens); other arm hangs slightly outward, elbow softly bent and hand relaxed. Both wings open in a balanced, broad shallow V behind him, full feather silhouettes comfortably INSIDE the canvas, no wingtip cutoff. Tailcoat tails hang and sweep gently behind, chains follow gravity. Draw a polished OFFICIAL P5-like HAND-DRAWN CEL illustration with crisp bold black outlines, flat crimson/black/white colors and hard graphic shadows, matching Image 2. NO 3D rendering, no photoreal textures or glossy plastic, no extreme fisheye/foreshortening. Portrait canvas 1536 by 2048. Leave a clear transparent margin on every side, keep the entire hat, both wings, boots, chains and coat visible. Genuine transparent RGBA PNG, alpha 0 outside the single character. No backdrop, ground, cast shadow, VFX, glow, particles, text, sheets or extra characters.
```

## 检查

- 原图1086×1448；交付1536×2048 RGBA（尺寸归一化，未声称原生2048细节）。
- alpha范围(0, 255)；四角alpha=[0, 0, 0, 0]；非零alpha边界=(19, 90, 1518, 1987)；上/下/左/右画布边最大alpha=[0, 0, 0, 0]。
- 清掉alpha<=2残点与全透明像素RGB，保留原始alpha边缘，没有白/绿底抠图。
- check_arsene.png 为深灰底，check_arsene_checker.png 为棋盘格底。
- 结构、比例、姿势和设计是否合意交用户确认，未据自检重画。
- 不改mod代码、不启动游戏，不拆层。
