# particles：V4素材

内置imagegen；每张生成一次，不根据自检自动重画。raw原图保留，交付版做尺寸归一化与透明残点清理。视觉验收交用户；Persona草图不拆层。

## feathers_sheet

```text
Use case: stylized-concept. A standalone 2D game VFX sprite for a Persona 5 Joker mod: bold hand-painted anime/comic shapes, clean controlled ink accents, high contrast, no 3D or photorealism. GENUINE TRANSPARENT RGBA PNG: alpha zero outside the actual sprite; never paint a dark backdrop, colored haze, vignette, checkerboard or ground. Soft glow must taper to transparent at the outer edge. Keep the whole sprite inside the canvas with transparent margin, no clipping. No text, watermark, characters or additional objects. A precisely aligned 4x4 sprite sheet, 16 separate black feathers, ONE whole feather per 512x512 cell, each different bend/angle, subtle dark crimson edge reflection. No grid lines. Keep at least 48px transparent gap on EACH cell edge, no overlapping cells. Each feather is an individual object, not animation. Canvas 2048x2048 pixels.
```

原始尺寸：[1254, 1254]；交付：[2048, 2048]。生成1次，重画0次。

检查：{"mode": "RGBA", "alpha_range": [0, 255], "corner_alpha": [0, 0, 0, 0], "alpha_zero_pixels": 3132214, "soft_alpha_pixels": 1061832}

## shards_sheet

```text
Use case: stylized-concept. A standalone 2D game VFX sprite for a Persona 5 Joker mod: bold hand-painted anime/comic shapes, clean controlled ink accents, high contrast, no 3D or photorealism. GENUINE TRANSPARENT RGBA PNG: alpha zero outside the actual sprite; never paint a dark backdrop, colored haze, vignette, checkerboard or ground. Soft glow must taper to transparent at the outer edge. Keep the whole sprite inside the canvas with transparent margin, no clipping. No text, watermark, characters or additional objects. A precisely aligned 4x4 sprite sheet, 16 separate sharp red/black glass shards, ONE whole shard per 512x512 cell, varied jagged triangular silhouettes with a single bright highlight edge. No grid lines. At least 48px transparent margin within EACH cell, no overlap. Distinct objects not animation. Canvas 2048x2048 pixels.
```

原始尺寸：[1254, 1254]；交付：[2048, 2048]。生成1次，重画0次。

检查：{"mode": "RGBA", "alpha_range": [0, 255], "corner_alpha": [0, 0, 0, 0], "alpha_zero_pixels": 3102059, "soft_alpha_pixels": 1090785}

## sparks_sheet

```text
Use case: stylized-concept. A standalone 2D game VFX sprite for a Persona 5 Joker mod: bold hand-painted anime/comic shapes, clean controlled ink accents, high contrast, no 3D or photorealism. GENUINE TRANSPARENT RGBA PNG: alpha zero outside the actual sprite; never paint a dark backdrop, colored haze, vignette, checkerboard or ground. Soft glow must taper to transparent at the outer edge. Keep the whole sprite inside the canvas with transparent margin, no clipping. No text, watermark, characters or additional objects. A precisely aligned 4x4 sprite sheet, 16 separate white-core red-edge sparks, ONE whole spark per 512x512 cell. Different four-point stars, long streak sparks, cross glints and small spiky glints. No grid lines, at least 48px transparent margin within EACH cell, no overlap. Distinct objects not animation. Canvas 2048x2048 pixels.
```

原始尺寸：[1254, 1254]；交付：[2048, 2048]。生成1次，重画0次。

检查：{"mode": "RGBA", "alpha_range": [0, 255], "corner_alpha": [0, 0, 0, 0], "alpha_zero_pixels": 3407057, "soft_alpha_pixels": 786725}


## 图集边距限制

三张均是4×4构图，512网格的alpha20边界已写入*_layout.json。24px安全边距不足格数：羽毛13、碎片10、火花12，因此未满足每格48px的理想边距，不宣称直接严格等格裁切即用；没有脚本移动物体，也未自动重画。由Claude验收后决定排版或返工。
