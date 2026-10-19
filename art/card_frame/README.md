# P3R 塔罗风格卡框素材

使用内置 imagegen，每张独立调用一次，共 9 次；全部第一轮，无重画。输入准备图在仓库外 sts2_tools/card_frame_art_inputs，原版放大 2 倍、透明部分铺 #808080 灰底。风格基准为 art/p3r_tex/shuffle/T_FX_Arcana_01_Col.png。

## 后处理与检查

运行仓库 frame_postprocess.py 和 card_frame_preview.py，未改脚本。原始输出保存在 gen；后处理输出含 3 张本体、15 张稀有度变体和 1 张 168×64 RGBA 数字牌。数字牌裁到 alpha 包围盒后等比缩小、透明画布居中，不绘制替代内容。

- `card_frame_attack_s.png`：598×844，alpha 与原版逐像素一致。
- `card_frame_skill_s.png`：598×844，alpha 与原版逐像素一致。
- `card_frame_power_s.png`：598×844，alpha 与原版逐像素一致。
- `card_banner.png`：653×145，alpha 与原版逐像素一致。
- `card_portrait_border_attack_s.png`：551×420，alpha 与原版逐像素一致。
- `card_portrait_border_skill_s.png`：551×420，alpha 与原版逐像素一致。
- `card_portrait_border_power_s.png`：551×420，alpha 与原版逐像素一致。
- `card_portrait_border_plaque_s.png`：123×75，alpha 与原版逐像素一致。

- 数字牌 alpha 范围 0～255，外部真透明。
- 预览叠加两行白色英文描述，检查深色描述框上的可读性。
- 九张合成预览中未见明显原版红/棕/青残留；三种稀有度可区分，未做实机测试。

## 字体

Google Fonts 当前提供可变字体；本机缺少 fontTools，因此改从字体作者 NDISCOVER/Cinzel 的公开源码仓库下载静态 Cinzel-Bold.ttf：https://raw.githubusercontent.com/NDISCOVER/Cinzel/master/fonts/ttf/Cinzel-Bold.ttf 。许可证取自 Google Fonts：https://raw.githubusercontent.com/google/fonts/main/ofl/cinzel/OFL.txt 。未安装依赖。

## 已知问题

- 既有预览脚本卡片画布底部会裁去数字牌下沿约 8px；独立 168×64 素材完整，未修改脚本。
- 既有脚本前两行显示阿拉伯数字 0，第三行 IV；仅预览叠字，原始素材无数字或文字。
- 生成的金属细纹在 300px 卡宽下会有缩小损失；描述框钟表图案仍能看见，但白字清晰。
- 主体卡框三张为分别生成，石纹有细微差异，形状与 alpha 则由原版统一。

## 提示词全文与生成次数

### card_frame_attack_s（生成 1 次，重画 0 次）

```text
Edit image 1 as a production card_frame_attack_s texture. Preserve exact full-bleed layout and aspect ratio, no inset or surrounding padding. Replace red border with P3R tarot slate steel-blue stone (#29435f-#416589, shadows #10243a), fine Celtic interlace engraved relief, restrained corner stars, icy white inner hairline and pale steel-blue bevel. Replace lower brown description area with near-black navy #060c1c to #0d1830, extremely faint large clock ring with 12 ticks and small moon phases along its V-shaped top, details no brighter than #1f3050. Keep gray upper art opening precisely in place. Image 2 is material/style reference only, never reproduce its artwork or numerals. No text letters numbers watermark glow fog particles. Front-facing flat game UI sprite, fine worn texture visible at 300px card width.
```

### card_frame_skill_s（生成 1 次，重画 0 次）

```text
Edit image 1 as a production card_frame_skill_s texture. Preserve exact full-bleed layout and aspect ratio, no inset or surrounding padding. Replace red border with P3R tarot slate steel-blue stone (#29435f-#416589, shadows #10243a), fine Celtic interlace engraved relief, restrained corner stars, icy white inner hairline and pale steel-blue bevel. Replace lower brown description area with near-black navy #060c1c to #0d1830, extremely faint large clock ring with 12 ticks and small moon phases along its horizontal straight top, details no brighter than #1f3050. Keep gray upper art opening precisely in place. Image 2 is material/style reference only, never reproduce its artwork or numerals. No text letters numbers watermark glow fog particles. Front-facing flat game UI sprite, fine worn texture visible at 300px card width.
```

### card_frame_power_s（生成 1 次，重画 0 次）

```text
Edit image 1 as a production card_frame_power_s texture. Preserve exact full-bleed layout and aspect ratio, no inset or surrounding padding. Replace red border with P3R tarot slate steel-blue stone (#29435f-#416589, shadows #10243a), fine Celtic interlace engraved relief, restrained corner stars, icy white inner hairline and pale steel-blue bevel. Replace lower brown description area with near-black navy #060c1c to #0d1830, extremely faint large clock ring with 12 ticks and small moon phases along its trapezoid-notched top, details no brighter than #1f3050. Keep gray upper art opening precisely in place. Image 2 is material/style reference only, never reproduce its artwork or numerals. No text letters numbers watermark glow fog particles. Front-facing flat game UI sprite, fine worn texture visible at 300px card width.
```

### card_banner（生成 1 次，重画 0 次）

```text
Edit image 1, the exact full-canvas layout template for card_banner. Redraw ONLY the cyan shape as finely engraved silver-white metal, icy beveled edge, narrow dark inner incision, delicate worn surface and restrained P3R tarot craftsmanship matching image 2. Preserve shape position, thickness, aspect ratio and gray background exactly; no added margin, no new ornaments outside shape. Preserve bowed ribbon and folded tails, empty center for card name. No text, letters, numbers, watermark, glow, particles, fog, cyan or gold. One sprite only.
```

### card_portrait_border_attack_s（生成 1 次，重画 0 次）

```text
Edit image 1, the exact full-canvas layout template for card_portrait_border_attack_s. Redraw ONLY the cyan shape as finely engraved silver-white metal, icy beveled edge, narrow dark inner incision, delicate worn surface and restrained P3R tarot craftsmanship matching image 2. Preserve shape position, thickness, aspect ratio and gray background exactly; no added margin, no new ornaments outside shape. Keep open top and exact side strips and lower contour; do not fill interior. No text, letters, numbers, watermark, glow, particles, fog, cyan or gold. One sprite only.
```

### card_portrait_border_skill_s（生成 1 次，重画 0 次）

```text
Edit image 1, the exact full-canvas layout template for card_portrait_border_skill_s. Redraw ONLY the cyan shape as finely engraved silver-white metal, icy beveled edge, narrow dark inner incision, delicate worn surface and restrained P3R tarot craftsmanship matching image 2. Preserve shape position, thickness, aspect ratio and gray background exactly; no added margin, no new ornaments outside shape. Keep open top and exact side strips and lower contour; do not fill interior. No text, letters, numbers, watermark, glow, particles, fog, cyan or gold. One sprite only.
```

### card_portrait_border_power_s（生成 1 次，重画 0 次）

```text
Edit image 1, the exact full-canvas layout template for card_portrait_border_power_s. Redraw ONLY the cyan shape as finely engraved silver-white metal, icy beveled edge, narrow dark inner incision, delicate worn surface and restrained P3R tarot craftsmanship matching image 2. Preserve shape position, thickness, aspect ratio and gray background exactly; no added margin, no new ornaments outside shape. Keep open top and exact side strips and lower contour; do not fill interior. No text, letters, numbers, watermark, glow, particles, fog, cyan or gold. One sprite only.
```

### card_portrait_border_plaque_s（生成 1 次，重画 0 次）

```text
Edit image 1, the exact full-canvas layout template for card_portrait_border_plaque_s. Redraw ONLY the cyan shape as finely engraved silver-white metal, icy beveled edge, narrow dark inner incision, delicate worn surface and restrained P3R tarot craftsmanship matching image 2. Preserve shape position, thickness, aspect ratio and gray background exactly; no added margin, no new ornaments outside shape. Small plain metal type plaque, each outer 13/123 width reserved as end cap, flat stretchable middle. No text, letters, numbers, watermark, glow, particles, fog, cyan or gold. One sprite only.
```

### arcana_tab（生成 1 次，重画 0 次）

```text
Create one blank P3R tarot arcana number plaque, width:height exactly 168:64 (2.625:1). Genuine transparent RGBA outside the shape. Steel-blue stone texture, fine Celtic interlace engraving toward ends, icy white thin outline and pale blue inner bevel. Top edge rises in a broad smooth arch at center, bottom edge straight horizontal, small squared bottom corners. Front-facing flat UI asset, full canvas snugly filled. Center kept plain steel blue for later Roman numerals. NO text letters numbers symbols watermark, no shadow outside, no glow. Material and arch inspired by the bottom numeral plaque in reference, but entirely blank.
```

