# 亚森草图：Joker画风校正版（待确认）

累计4次生成，均因用户反馈而调整，未自动反复重画。v1/v2/v3保留。用户认为第3版照搬官方画风，本次明确分开两张输入的作用：官方立绘只核对形象结构；已定稿Joker只参考线条、色块和硬阴影。当前草图尚未确认，不拆层、不继续后续组。

- 设计参考：https://p5r.jp/character/hero/
- 风格参考：art/p5/poses/final/idle_a3.png。
- 原始输出：arsene_draft_raw.png；交付1536×2048 RGBA：arsene_draft.png。
- 清alpha<=2残点、全透明RGB；保留raw；没有局部结构修图。
- 深灰/棋盘格检查图：check_arsene.png、check_arsene_checker.png。

## 完整提示词

```text
Create a transparent 2D Arsene sprite for the SAME GAME as IMAGE 2. Two references have STRICTLY SEPARATE ROLES. IMAGE 1 is official Arsene: use ONLY his DESIGN, costume structure and pose. IMAGE 2 is our APPROVED JOKER SPRITE: use its exact DRAWING STYLE, strong clean black outlines, simplified broad flat color regions, sharp two-tone cel shadows. The previous draft failed because it copied the detailed painterly rendering and tiny feather textures from the official art. DO NOT copy Image 1's rendering. REDRAW Arsene as a clean modern anime game sprite in the same graphic language as Image 2. Wings: large readable grouped black feather shapes, each group a broad flat near-black shape plus ONE hard-edged charcoal shadow/highlight region; NO individual feather barbs, hatching, mottling, gradients, painterly brush texture, grain or realistic plumage. Body: crisp red panels, black center, bold ink, limited hard cel shadows matching Joker, no airbrushing. Preserve official Arsene anatomy: elongated tilted black top hat and red angular mask/head projections; white cravat; fitted short red jacket, black waistcoat/hip area, small gold trims; red pointed thigh structures, slender red legs and curved metal BLADE HEELS, not ordinary boots; both enormous black wings with subtle charcoal/purple blocks, NOT red wing lining; no invented huge red tails or extra chains. Upright hovering 3/4 toward screen right, one arm naturally extended outward with curved black clawed fingers, other arm lowered relaxed, legs separated without crossing. Full silhouette inside portrait 1536x2048 frame with generous clear margins including every wingtip and heel. One character only. GENUINE TRANSPARENT RGBA, alpha zero outside character, no backdrop, ground, cast shadow, text, gold border, particles or glow. Match IMAGE 2 in line weight, graphic simplification and color rendering so they look drawn by the same artist.
```

## 检查

{
  "mode": "RGBA",
  "corner_alpha": [
    0,
    0,
    0,
    0
  ],
  "alpha_range": [
    0,
    255
  ],
  "nonzero_bbox": [
    14,
    0,
    1527,
    2036
  ],
  "edge_alpha_max_top_bottom_left_right": [
    3,
    0,
    0,
    0
  ]
}

姿势、形象和画风仍交用户验收，不宣称已经定稿。
