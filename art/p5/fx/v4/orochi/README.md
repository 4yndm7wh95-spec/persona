# orochi：V4素材

内置imagegen；每张生成一次，不根据自检自动重画。raw原图保留，交付版做尺寸归一化与透明残点清理。视觉验收交用户；Persona草图不拆层。

## orochi_draft

```text
Use case: stylized-concept. Transparent 2D game character design draft: Yamata-no-Orochi as depicted in Persona 5. Image 1 is a local original PS0052 model design reference ONLY: it is a stiff bind-pose preview, DO NOT copy its rigid straight vertical neck arrangement. Use its deep green reptilian heads, tan segmented underbellies, green scales, long serpentine anatomy and small fierce eyes. Image 2 is our approved Joker sprite and determines the DRAWING STYLE: clean bold black outlines, broad flat colors and crisp hard-edged cel shadows, no realistic texture or 3D rendering. Draw exactly EIGHT distinct snake heads rising on eight elegantly curved necks from a single large intertwined coiled serpent mass. Arrange eight readable head silhouettes in a broad dramatic fan, each at a different height/angle, all connected to the coils; green upper scales, warm ochre/tan belly plates, pale pointed fangs. Match the Persona design, not a Western dragon: no legs, no wings, no invented horns or extra heads. A clean finished approval draft, full creature completely inside a 2048x1280 landscape canvas with clear transparent margin, balanced composition. Genuine transparent RGBA, alpha zero outside creature. No scene, ground, cast shadow, fire, breath, magic, text or other characters. No painterly tiny scale texture, no gradients, no grain, no photorealism; match the graphic simplicity of Image 2.
```

原始尺寸：[1536, 1024]；交付：[2048, 1280]。生成1次，重画0次。

检查：{"mode": "RGBA", "alpha_range": [0, 255], "corner_alpha": [0, 0, 0, 0], "alpha_zero_pixels": 1281230, "soft_alpha_pixels": 1337734}


## 原版设计核对

PS0052.GMD 的贴图名含 ps_orochi，确认编号0052。已用既有GFDExport、fix_gfd_glb和Blender导出orochi_ref.png；原模型和GLB留本机，不提交。参考是颈部伸直的绑定姿势，仅核对绿鳞、黄褐腹甲与蛇头，不作为草图姿势。草图尚未确认，不拆层；草图有半透明外缘/暗色晕染，检查图照实保留，交用户验收。
