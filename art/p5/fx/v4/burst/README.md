# burst：V4素材

内置imagegen；每张生成一次，不根据自检自动重画。raw原图保留，交付版做尺寸归一化与透明残点清理。视觉验收交用户；Persona草图不拆层。

## burst_01

```text
Use case: stylized-concept. A standalone 2D game VFX sprite for a Persona 5 Joker mod: bold hand-painted anime/comic shapes, clean controlled ink accents, high contrast, no 3D or photorealism. GENUINE TRANSPARENT RGBA PNG: alpha zero outside the actual sprite; never paint a dark backdrop, colored haze, vignette, checkerboard or ground. Soft glow must taper to transparent at the outer edge. Keep the whole sprite inside the canvas with transparent margin, no clipping. No text, watermark, characters or additional objects. One Persona 5 impact comic burst frame, solid red fill, bold thick black outline, subtle black halftone dots inside, long asymmetric radial needle spikes. No lettering, no white page, transparent outside. Canvas 1024x1024 pixels.
```

原始尺寸：[1254, 1254]；交付：[1024, 1024]。生成1次，重画0次。

检查：{"mode": "RGBA", "alpha_range": [0, 255], "corner_alpha": [0, 0, 0, 0], "alpha_zero_pixels": 797109, "soft_alpha_pixels": 247577}

## burst_02

```text
Use case: stylized-concept. A standalone 2D game VFX sprite for a Persona 5 Joker mod: bold hand-painted anime/comic shapes, clean controlled ink accents, high contrast, no 3D or photorealism. GENUINE TRANSPARENT RGBA PNG: alpha zero outside the actual sprite; never paint a dark backdrop, colored haze, vignette, checkerboard or ground. Soft glow must taper to transparent at the outer edge. Keep the whole sprite inside the canvas with transparent margin, no clipping. No text, watermark, characters or additional objects. One Persona 5 impact comic burst frame, solid red fill, bold thick black outline, subtle black halftone dots inside, broad angular explosive star with alternating long and short points. No lettering, no white page, transparent outside. Canvas 1024x1024 pixels.
```

原始尺寸：[1254, 1254]；交付：[1024, 1024]。生成1次，重画0次。

检查：{"mode": "RGBA", "alpha_range": [0, 255], "corner_alpha": [0, 0, 0, 0], "alpha_zero_pixels": 687558, "soft_alpha_pixels": 357083}

## burst_03

```text
Use case: stylized-concept. A standalone 2D game VFX sprite for a Persona 5 Joker mod: bold hand-painted anime/comic shapes, clean controlled ink accents, high contrast, no 3D or photorealism. GENUINE TRANSPARENT RGBA PNG: alpha zero outside the actual sprite; never paint a dark backdrop, colored haze, vignette, checkerboard or ground. Soft glow must taper to transparent at the outer edge. Keep the whole sprite inside the canvas with transparent margin, no clipping. No text, watermark, characters or additional objects. One Persona 5 impact comic burst frame, solid red fill, bold thick black outline, subtle black halftone dots inside, compressed diagonal jagged burst with dramatic side spikes. No lettering, no white page, transparent outside. Canvas 1024x1024 pixels.
```

原始尺寸：[1254, 1254]；交付：[1024, 1024]。生成1次，重画0次。

检查：{"mode": "RGBA", "alpha_range": [0, 255], "corner_alpha": [0, 0, 0, 0], "alpha_zero_pixels": 801939, "soft_alpha_pixels": 242624}

