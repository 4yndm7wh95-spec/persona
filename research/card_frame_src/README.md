# 原版卡框参考资料

来源：`C:/Users/kkk/sts2_extract`（已有 GDRE 解包目录，只读复制）。

复制 71 个文件，总计 3757651 字节。包含 UI 图集、card AtlasTexture 定义、卡框及横幅材质、依赖着色器、卡牌场景和能量图标等贴图；未复制角色、卡牌插画、脚本或字体。场景仅供结构参考，并非可独立运行的完整项目。

运行：`python tools/ui/crop_card_sprites.py`（需要已有 Pillow）。按 region 原尺寸裁图，不补 margin；margin 原值记录到 index.csv。预览图按比例缩略，PNG 保持原尺寸与透明度。

缺失文件：无。

跳过文件：无。

## 复制清单

- `images/atlases/compressed.sprites/card_template/ancient_flame/ancient_card_flame_0.tres`
- `images/atlases/compressed.sprites/card_template/ancient_flame/ancient_card_flame_1.tres`
- `images/atlases/compressed.sprites/card_template/ancient_flame/ancient_card_flame_2.tres`
- `images/atlases/compressed.sprites/card_template/ancient_flame/ancient_card_flame_3.tres`
- `images/atlases/compressed.sprites/card_template/ancient_flame/ancient_card_flame_4.tres`
- `images/atlases/compressed.sprites/card_template/ancient_flame/ancient_card_flame_5.tres`
- `images/atlases/compressed.sprites/card_template/ancient_flame/ancient_card_flame_6.tres`
- `images/atlases/compressed.sprites/card_template/ancient_flame/ancient_card_flame_7.tres`
- `images/atlases/compressed.sprites/card_template/ancient_flame/ancient_card_flame_8.tres`
- `images/atlases/compressed.sprites/card_template/ancient_flame/ancient_card_flame_9.tres`
- `images/atlases/compressed.sprites/card_template/ancient_portrait_mask_large.tres`
- `images/atlases/compressed_0.png`
- `images/atlases/ui_atlas.sprites/card/ancient_banner.tres`
- `images/atlases/ui_atlas.sprites/card/card_banner.tres`
- `images/atlases/ui_atlas.sprites/card/card_enchant_s.tres`
- `images/atlases/ui_atlas.sprites/card/card_frame_ancient_s.tres`
- `images/atlases/ui_atlas.sprites/card/card_frame_attack_s.tres`
- `images/atlases/ui_atlas.sprites/card/card_frame_power_s.tres`
- `images/atlases/ui_atlas.sprites/card/card_frame_quest_s.tres`
- `images/atlases/ui_atlas.sprites/card/card_frame_skill_s.tres`
- `images/atlases/ui_atlas.sprites/card/card_portrait_border_attack_s.tres`
- `images/atlases/ui_atlas.sprites/card/card_portrait_border_plaque_s.tres`
- `images/atlases/ui_atlas.sprites/card/card_portrait_border_power_s.tres`
- `images/atlases/ui_atlas.sprites/card/card_portrait_border_skill_s.tres`
- `images/atlases/ui_atlas.sprites/card/card_unplayable_icon.tres`
- `images/atlases/ui_atlas.sprites/card/energy_colorless.tres`
- `images/atlases/ui_atlas.sprites/card/energy_defect.tres`
- `images/atlases/ui_atlas.sprites/card/energy_ironclad.tres`
- `images/atlases/ui_atlas.sprites/card/energy_necrobinder.tres`
- `images/atlases/ui_atlas.sprites/card/energy_quest.tres`
- `images/atlases/ui_atlas.sprites/card/energy_regent.tres`
- `images/atlases/ui_atlas.sprites/card/energy_silent.tres`
- `images/atlases/ui_atlas_0.png`
- `images/atlases/ui_atlas_1.png`
- `images/packed/card_template/card_flash.png`
- `images/packed/card_template/card_flash.png.import`
- `images/packed/card_template/card_frame_sdf.exr`
- `images/packed/card_template/card_frame_sdf.exr.import`
- `images/packed/common_ui/locked_card.png`
- `images/ui/cards/card_portrait_border_plaque2.png`
- `images/ui/cards/upgrade_preview/upgrade_arrow.png`
- `images/ui/combat/energy_star.png`
- `images/vfx/ui/card/ancient/ui_card_ancient_border_main.png`
- `images/vfx/ui/ui_card_mask.png`
- `materials/cards/banners/card_banner_ancient_mat.tres`
- `materials/cards/banners/card_banner_common_mat.tres`
- `materials/cards/banners/card_banner_curse_mat.tres`
- `materials/cards/banners/card_banner_event_mat.tres`
- `materials/cards/banners/card_banner_quest_mat.tres`
- `materials/cards/banners/card_banner_rare_mat.tres`
- `materials/cards/banners/card_banner_status_mat.tres`
- `materials/cards/banners/card_banner_uncommon_mat.tres`
- `materials/cards/frames/card_frame_blue_mat.tres`
- `materials/cards/frames/card_frame_colorless_mat.tres`
- `materials/cards/frames/card_frame_curse_mat.tres`
- `materials/cards/frames/card_frame_green_mat.tres`
- `materials/cards/frames/card_frame_orange_mat.tres`
- `materials/cards/frames/card_frame_pink_mat.tres`
- `materials/cards/frames/card_frame_quest_mat.tres`
- `materials/cards/frames/card_frame_red_mat.tres`
- `scenes/cards/card.tscn`
- `scenes/cards/card_canvas_group_blur_material.tres`
- `scenes/cards/card_canvas_group_mask_blur_material.tres`
- `scenes/cards/card_canvas_group_mask_material.tres`
- `scenes/cards/card_portrait_blur_material.tres`
- `scenes/cards/tiny_card.tscn`
- `scenes/cards/upgrade_preview.tscn`
- `shaders/blur/Blur.gdshader`
- `shaders/blur/canvas_group_mask_blur.gdshader`
- `shaders/colorize_addblend.tres`
- `shaders/hsv.gdshader`
