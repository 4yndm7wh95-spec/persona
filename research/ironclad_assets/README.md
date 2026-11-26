# Vanilla Ironclad reference export (partial)

- Intended baseline: Slay the Spire 2 v0.111.0 (41cef1ea), as recorded in [REPORT.md](../REPORT.md).
- The original recovery at `C:\Users\kkk\sts2_extract` currently contains only a mods folder. Its saved [file inventory](../extract_file_list.txt) records paths and sizes.
- Sources are surviving vanilla exports already in the repository. The selection background comes from the explicitly labeled v0.107.1 archive. Each file identifies its source below.
- Source and copied bytes were compared with SHA-256. Godot paths are preserved; the export does not include all shared dependencies.

## Copy commands

Run from the repository root.

~~~powershell
$destination = 'research/ironclad_assets'
$target = Join-Path $destination 'animations/characters/ironclad/ironclad_skel_data.tres'
New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
Copy-Item -LiteralPath 'research/reference_samples/animations/characters/ironclad/ironclad_skel_data.tres' -Destination $target
$target = Join-Path $destination 'images/packed/map/icons/map_marker_ironclad.png'
New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
Copy-Item -LiteralPath 'research/sts2_transitions/recovered/images/packed/map/icons/map_marker_ironclad.png' -Destination $target
$target = Join-Path $destination 'images/ui/transitions/ironclad_transition.png'
New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
Copy-Item -LiteralPath 'research/sts2_transitions/recovered/images/ui/transitions/ironclad_transition.png' -Destination $target
$target = Join-Path $destination 'images/vfx/characters/ironclad_eye_fire_base.png'
New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
Copy-Item -LiteralPath 'research/reference_samples/images/vfx/characters/ironclad_eye_fire_base.png' -Destination $target
$target = Join-Path $destination 'images/vfx/characters/ironclad_slash_base.png'
New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
Copy-Item -LiteralPath 'research/reference_samples/images/vfx/characters/ironclad_slash_base.png' -Destination $target
$target = Join-Path $destination 'materials/transitions/ironclad_transition_mat.tres'
New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
Copy-Item -LiteralPath 'research/sts2_transitions/recovered/materials/transitions/ironclad_transition_mat.tres' -Destination $target
$target = Join-Path $destination 'scenes/creature_visuals/ironclad.tscn'
New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
Copy-Item -LiteralPath 'research/reference_samples/scenes/creature_visuals/ironclad.tscn' -Destination $target
$target = Join-Path $destination 'scenes/merchant/characters/ironclad_merchant.tscn'
New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
Copy-Item -LiteralPath 'research/sts2_merchant/scenes/merchant/characters/ironclad_merchant.tscn' -Destination $target
$target = Join-Path $destination 'scenes/screens/char_select/char_select_bg_ironclad.tscn'
New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
Copy-Item -LiteralPath 'research/regent_scenes_0.107.1/scenes/screens/char_select/char_select_bg_ironclad.tscn' -Destination $target
$target = Join-Path $destination 'scenes/vfx/card_trail_ironclad.tscn'
New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
Copy-Item -LiteralPath 'research/sts2_vfx/recovered/scenes/vfx/card_trail_ironclad.tscn' -Destination $target
$target = Join-Path $destination 'scenes/vfx/energy/ironclad/ironclad_energy_vfx_back.tscn'
New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
Copy-Item -LiteralPath 'research/sts2_vfx/recovered/scenes/vfx/energy/ironclad/ironclad_energy_vfx_back.tscn' -Destination $target
$target = Join-Path $destination 'scenes/vfx/energy/ironclad/ironclad_energy_vfx_front.tscn'
New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
Copy-Item -LiteralPath 'research/sts2_vfx/recovered/scenes/vfx/energy/ironclad/ironclad_energy_vfx_front.tscn' -Destination $target
~~~

## Files

| Original path | Bytes | UIDs in file | Archived source |
| --- | ---: | --- | --- |
| `res://animations/characters/ironclad/ironclad_skel_data.tres` | 2033 | `uid://8wiohbeiuu21`, `uid://b8e4wst5vt8xh`, `uid://dyk0k8i37hi8g` | `research/reference_samples/animations/characters/ironclad/ironclad_skel_data.tres` |
| `res://images/packed/map/icons/map_marker_ironclad.png` | 3950 | - | `research/sts2_transitions/recovered/images/packed/map/icons/map_marker_ironclad.png` |
| `res://images/ui/transitions/ironclad_transition.png` | 121482 | - | `research/sts2_transitions/recovered/images/ui/transitions/ironclad_transition.png` |
| `res://images/vfx/characters/ironclad_eye_fire_base.png` | 5688 | - | `research/reference_samples/images/vfx/characters/ironclad_eye_fire_base.png` |
| `res://images/vfx/characters/ironclad_slash_base.png` | 9962 | - | `research/reference_samples/images/vfx/characters/ironclad_slash_base.png` |
| `res://materials/transitions/ironclad_transition_mat.tres` | 832 | `uid://cuhsxeswx1qg7`, `uid://jvldwl2cyrvu` | `research/sts2_transitions/recovered/materials/transitions/ironclad_transition_mat.tres` |
| `res://scenes/creature_visuals/ironclad.tscn` | 5808 | `uid://1epaordiol6u`, `uid://1sb3yjut1s62`, `uid://b8e4wst5vt8xh`, `uid://bsw3nxvr118xr`, `uid://c65hcljas0pyo`, `uid://cghtlla3sw0po`, `uid://ctia3rua3hmm8`, `uid://dfgtak7it7exn`, `uid://dryvqo67omhm`, `uid://dtaovewempx07`, `uid://h351k838kpyg`, `uid://ihjpxxm3o87h`, `uid://nabb7hx4fxat`, `uid://r5xwt5ebhstx` | `research/reference_samples/scenes/creature_visuals/ironclad.tscn` |
| `res://scenes/merchant/characters/ironclad_merchant.tscn` | 676 | `uid://b2ww8leotjikm`, `uid://cpfe18byewpmx`, `uid://dt14luwusxnb5` | `research/sts2_merchant/scenes/merchant/characters/ironclad_merchant.tscn` |
| `res://scenes/screens/char_select/char_select_bg_ironclad.tscn` | 11388 | `uid://8tp7h0nkic6u`, `uid://b0oho7pjc1dtg`, `uid://b72mj3cao7go2`, `uid://b7gtl7gvi5tfc`, `uid://bxa6vlqeby20`, `uid://c4ycqv0gdxvmr`, `uid://d2srbops1xqm0`, `uid://d4k2ig4l2q5cw`, `uid://dup7jlcdcvt6` | `research/regent_scenes_0.107.1/scenes/screens/char_select/char_select_bg_ironclad.tscn` |
| `res://scenes/vfx/card_trail_ironclad.tscn` | 5032 | `uid://1wc3p3n7fakr`, `uid://b7vqn8f75qh4x`, `uid://bb3fwupsnhf43`, `uid://bexbor35725ag`, `uid://bk4gpfmy1awke`, `uid://brh5aw8cqk1c5`, `uid://c4ycqv0gdxvmr`, `uid://cl44l3vqar6tb`, `uid://co4rdu87x6mcr` | `research/sts2_vfx/recovered/scenes/vfx/card_trail_ironclad.tscn` |
| `res://scenes/vfx/energy/ironclad/ironclad_energy_vfx_back.tscn` | 1509 | `uid://7y75kcsi1xdw`, `uid://bd46jtcut2h66`, `uid://cq0m6037hbecy` | `research/sts2_vfx/recovered/scenes/vfx/energy/ironclad/ironclad_energy_vfx_back.tscn` |
| `res://scenes/vfx/energy/ironclad/ironclad_energy_vfx_front.tscn` | 9562 | `uid://0vk581j1pecs`, `uid://bd46jtcut2h66`, `uid://c5840cav24a8y`, `uid://cl50phqjh8m8o`, `uid://dgq874fbtq7ew`, `uid://dvsi8v1mbnt3q`, `uid://fdia0chu7d7l` | `research/sts2_vfx/recovered/scenes/vfx/energy/ironclad/ironclad_energy_vfx_front.tscn` |

## Scene and material references

**Included** marks references whose target is in the export.

- `res://animations/characters/ironclad/ironclad_skel_data.tres`
  - `res://animations/characters/ironclad/ironclad.atlas`
  - `res://animations/characters/ironclad/ironclad.skel`
- `res://materials/transitions/ironclad_transition_mat.tres`
  - `res://images/ui/transitions/ironclad_transition.png` **Included**
- `res://scenes/creature_visuals/ironclad.tscn`
  - `res://animations/characters/ironclad/ironclad_skel_data.tres` **Included**
  - `res://images/vfx/characters/ironclad_eye_fire_base.png` **Included**
  - `res://images/vfx/characters/ironclad_slash_base.png` **Included**
  - `res://images/vfx/environment/fire/basic_fire_gradient.png`
  - `res://images/vfx/fire/basic_fire_noise.png`
  - `res://images/vfx/fire/fire_bottom_mask.png`
  - `res://images/vfx/fire/triangle_noise_tile.png`
  - `res://images/vfx/fire/zigzag_fire_distortion.png`
  - `res://images/vfx/slash_shader_flat.tres`
  - `res://images/vfx/tileable_noise_1.png`
  - `res://shaders/vfx/vfx_stepped_shader_fire_flat.tres`
  - `res://src/Core/Nodes/Combat/NCreatureVisuals.cs`
  - `res://src/Core/Nodes/Vfx/NIroncladVfx.cs`
- `res://scenes/merchant/characters/ironclad_merchant.tscn`
  - `res://animations/merchant/ironclad/ironclad_merchant_skel_data.tres`
  - `res://src/Core/Nodes/Screens/Shops/NMerchantCharacter.cs`
- `res://scenes/screens/char_select/char_select_bg_ironclad.tscn`
  - `res://animations/character_select/ironclad/characterselect_ironclad_skel_data.tres`
  - `res://images/vfx/dot.png`
  - `res://images/vfx/light.png`
  - `res://images/vfx/short_grain_rice_particle.png`
  - `res://images/vfx/short_rice_no_glow_particle.png`
  - `res://scenes/backgrounds/little_light_script.gd`
  - `res://src/Core/Nodes/Animation/NSpineAutoPlayer.cs`
  - `res://themes/canvas_item_material_additive_shared.tres`
- `res://scenes/vfx/card_trail_ironclad.tscn`
  - `res://images/packed/vfx/small_card_silhouette.png`
  - `res://images/packed/vfx/trail.png`
  - `res://images/packed/vfx/trail2.png`
  - `res://images/vfx/brush_particle_2.png`
  - `res://images/vfx/vfx_ghostly_power_up/sparkle.png`
  - `res://src/Core/Nodes/Vfx/NCardTrail.cs`
  - `res://src/Core/Nodes/Vfx/NCardTrailVfx.cs`
  - `res://themes/canvas_item_material_additive_shared.tres`
- `res://scenes/vfx/energy/ironclad/ironclad_energy_vfx_back.tscn`
  - `res://scenes/vfx/common/vfx_common_glow.tscn`
  - `res://src/Core/Nodes/Vfx/Utilities/NParticlesContainer.cs`
- `res://scenes/vfx/energy/ironclad/ironclad_energy_vfx_front.tscn`
  - `res://scenes/vfx/common/vfx_common_ring_polar_a.tscn`
  - `res://scenes/vfx/fire_impact/vfx_fire_embers_straight.tscn`
  - `res://scenes/vfx/fire_impact/vfx_fire_flipbook_1.tscn`
  - `res://scenes/vfx/fire_impact/vfx_fire_flipbook_2.tscn`
  - `res://scenes/vfx/fire_impact/vfx_fire_flipbook_3.tscn`
  - `res://src/Core/Nodes/Vfx/Utilities/NParticlesContainer.cs`

## Unavailable assets

These requested or Ironclad-specific referenced paths were unavailable in the surviving archives.

- `res://animations/character_select/ironclad/characterselect_ironclad.atlas`
- `res://animations/character_select/ironclad/characterselect_ironclad.atlas.import`
- `res://animations/character_select/ironclad/characterselect_ironclad.png`
- `res://animations/character_select/ironclad/characterselect_ironclad.png.import`
- `res://animations/character_select/ironclad/characterselect_ironclad.skel`
- `res://animations/character_select/ironclad/characterselect_ironclad.skel.import`
- `res://animations/character_select/ironclad/characterselect_ironclad_skel_data.tres`
- `res://animations/characters/ironclad/ironclad.atlas`
- `res://animations/characters/ironclad/ironclad.atlas.import`
- `res://animations/characters/ironclad/ironclad.png`
- `res://animations/characters/ironclad/ironclad.png.import`
- `res://animations/characters/ironclad/ironclad.skel`
- `res://animations/characters/ironclad/ironclad.skel.import`
- `res://animations/characters/ironclad/ironclad_2.png`
- `res://animations/characters/ironclad/ironclad_2.png.import`
- `res://animations/characters/ironclad/ironclad_3.png`
- `res://animations/characters/ironclad/ironclad_3.png.import`
- `res://animations/characters/ironclad/ironclad_4.png`
- `res://animations/characters/ironclad/ironclad_4.png.import`
- `res://animations/merchant/ironclad/ironclad_merchant_skel_data.tres`
- `res://animations/merchant/ironclad/ironclad_shop.atlas`
- `res://animations/merchant/ironclad/ironclad_shop.atlas.import`
- `res://animations/merchant/ironclad/ironclad_shop.png`
- `res://animations/merchant/ironclad/ironclad_shop.png.import`
- `res://animations/merchant/ironclad/ironclad_shop_2.png`
- `res://animations/merchant/ironclad/ironclad_shop_2.png.import`
- `res://animations/merchant/ironclad/ironclad_shop_3.png`
- `res://animations/merchant/ironclad/ironclad_shop_3.png.import`
- `res://animations/merchant/ironclad/ironclad_shop_4.png`
- `res://animations/merchant/ironclad/ironclad_shop_4.png.import`
- `res://animations/rest_site/ironclad/rest_site_ironclad_skel_data.tres`
- `res://animations/rest_site/ironclad/restsite_ironclad.atlas`
- `res://animations/rest_site/ironclad/restsite_ironclad.atlas.import`
- `res://animations/rest_site/ironclad/restsite_ironclad.png`
- `res://animations/rest_site/ironclad/restsite_ironclad.png.import`
- `res://animations/rest_site/ironclad/restsite_ironclad.skel`
- `res://animations/rest_site/ironclad/restsite_ironclad.skel.import`
- `res://images/packed/card_portraits/ironclad/aggression.png`
- `res://images/packed/card_portraits/ironclad/aggression.png.import`
- `res://images/packed/card_portraits/ironclad/anger.png`
- `res://images/packed/card_portraits/ironclad/anger.png.import`
- `res://images/packed/card_portraits/ironclad/armaments.png`
- `res://images/packed/card_portraits/ironclad/armaments.png.import`
- `res://images/packed/card_portraits/ironclad/ashen_strike.png`
- `res://images/packed/card_portraits/ironclad/ashen_strike.png.import`
- `res://images/packed/card_portraits/ironclad/barricade.png`
- `res://images/packed/card_portraits/ironclad/barricade.png.import`
- `res://images/packed/card_portraits/ironclad/bash.png`
- `res://images/packed/card_portraits/ironclad/bash.png.import`
- `res://images/packed/card_portraits/ironclad/battle_trance.png`
- `res://images/packed/card_portraits/ironclad/battle_trance.png.import`
- `res://images/packed/card_portraits/ironclad/beta/blaze.png`
- `res://images/packed/card_portraits/ironclad/beta/blaze.png.import`
- `res://images/packed/card_portraits/ironclad/beta/cascade.png`
- `res://images/packed/card_portraits/ironclad/beta/cascade.png.import`
- `res://images/packed/card_portraits/ironclad/beta/colossus.png`
- `res://images/packed/card_portraits/ironclad/beta/colossus.png.import`
- `res://images/packed/card_portraits/ironclad/beta/conflagration.png`
- `res://images/packed/card_portraits/ironclad/beta/conflagration.png.import`
- `res://images/packed/card_portraits/ironclad/beta/crimson_mantle.png`
- `res://images/packed/card_portraits/ironclad/beta/crimson_mantle.png.import`
- `res://images/packed/card_portraits/ironclad/beta/demonic_shield.png`
- `res://images/packed/card_portraits/ironclad/beta/demonic_shield.png.import`
- `res://images/packed/card_portraits/ironclad/beta/drum_of_battle.png`
- `res://images/packed/card_portraits/ironclad/beta/drum_of_battle.png.import`
- `res://images/packed/card_portraits/ironclad/beta/fight_me.png`
- `res://images/packed/card_portraits/ironclad/beta/fight_me.png.import`
- `res://images/packed/card_portraits/ironclad/beta/juggling.png`
- `res://images/packed/card_portraits/ironclad/beta/juggling.png.import`
- `res://images/packed/card_portraits/ironclad/beta/midnight.png`
- `res://images/packed/card_portraits/ironclad/beta/midnight.png.import`
- `res://images/packed/card_portraits/ironclad/beta/not_yet.png`
- `res://images/packed/card_portraits/ironclad/beta/not_yet.png.import`
- `res://images/packed/card_portraits/ironclad/beta/outrage.png`
- `res://images/packed/card_portraits/ironclad/beta/outrage.png.import`
- `res://images/packed/card_portraits/ironclad/beta/pillage.png`
- `res://images/packed/card_portraits/ironclad/beta/pillage.png.import`
- `res://images/packed/card_portraits/ironclad/beta/spite.png`
- `res://images/packed/card_portraits/ironclad/beta/spite.png.import`
- `res://images/packed/card_portraits/ironclad/beta/tank.png`
- `res://images/packed/card_portraits/ironclad/beta/tank.png.import`
- `res://images/packed/card_portraits/ironclad/beta/unmovable.png`
- `res://images/packed/card_portraits/ironclad/beta/unmovable.png.import`
- `res://images/packed/card_portraits/ironclad/beta/vicious.png`
- `res://images/packed/card_portraits/ironclad/beta/vicious.png.import`
- `res://images/packed/card_portraits/ironclad/blood_wall.png`
- `res://images/packed/card_portraits/ironclad/blood_wall.png.import`
- `res://images/packed/card_portraits/ironclad/bloodletting.png`
- `res://images/packed/card_portraits/ironclad/bloodletting.png.import`
- `res://images/packed/card_portraits/ironclad/bludgeon.png`
- `res://images/packed/card_portraits/ironclad/bludgeon.png.import`
- `res://images/packed/card_portraits/ironclad/body_slam.png`
- `res://images/packed/card_portraits/ironclad/body_slam.png.import`
- `res://images/packed/card_portraits/ironclad/brand.png`
- `res://images/packed/card_portraits/ironclad/brand.png.import`
- `res://images/packed/card_portraits/ironclad/break.png`
- `res://images/packed/card_portraits/ironclad/break.png.import`
- `res://images/packed/card_portraits/ironclad/breakthrough.png`
- `res://images/packed/card_portraits/ironclad/breakthrough.png.import`
- `res://images/packed/card_portraits/ironclad/bully.png`
- `res://images/packed/card_portraits/ironclad/bully.png.import`
- `res://images/packed/card_portraits/ironclad/burning_pact.png`
- `res://images/packed/card_portraits/ironclad/burning_pact.png.import`
- `res://images/packed/card_portraits/ironclad/cascade.png`
- `res://images/packed/card_portraits/ironclad/cascade.png.import`
- `res://images/packed/card_portraits/ironclad/cinder.png`
- `res://images/packed/card_portraits/ironclad/cinder.png.import`
- `res://images/packed/card_portraits/ironclad/colossus.png`
- `res://images/packed/card_portraits/ironclad/colossus.png.import`
- `res://images/packed/card_portraits/ironclad/conflagration.png`
- `res://images/packed/card_portraits/ironclad/conflagration.png.import`
- `res://images/packed/card_portraits/ironclad/corruption.png`
- `res://images/packed/card_portraits/ironclad/corruption.png.import`
- `res://images/packed/card_portraits/ironclad/crimson_mantle.png`
- `res://images/packed/card_portraits/ironclad/crimson_mantle.png.import`
- `res://images/packed/card_portraits/ironclad/cruelty.png`
- `res://images/packed/card_portraits/ironclad/cruelty.png.import`
- `res://images/packed/card_portraits/ironclad/dark_embrace.png`
- `res://images/packed/card_portraits/ironclad/dark_embrace.png.import`
- `res://images/packed/card_portraits/ironclad/defend_ironclad.png`
- `res://images/packed/card_portraits/ironclad/defend_ironclad.png.import`
- `res://images/packed/card_portraits/ironclad/demon_form.png`
- `res://images/packed/card_portraits/ironclad/demon_form.png.import`
- `res://images/packed/card_portraits/ironclad/demonic_shield.png`
- `res://images/packed/card_portraits/ironclad/demonic_shield.png.import`
- `res://images/packed/card_portraits/ironclad/dismantle.png`
- `res://images/packed/card_portraits/ironclad/dismantle.png.import`
- `res://images/packed/card_portraits/ironclad/dominate.png`
- `res://images/packed/card_portraits/ironclad/dominate.png.import`
- `res://images/packed/card_portraits/ironclad/drum_of_battle.png`
- `res://images/packed/card_portraits/ironclad/drum_of_battle.png.import`
- `res://images/packed/card_portraits/ironclad/evil_eye.png`
- `res://images/packed/card_portraits/ironclad/evil_eye.png.import`
- `res://images/packed/card_portraits/ironclad/expect_a_fight.png`
- `res://images/packed/card_portraits/ironclad/expect_a_fight.png.import`
- `res://images/packed/card_portraits/ironclad/feed.png`
- `res://images/packed/card_portraits/ironclad/feed.png.import`
- `res://images/packed/card_portraits/ironclad/feel_no_pain.png`
- `res://images/packed/card_portraits/ironclad/feel_no_pain.png.import`
- `res://images/packed/card_portraits/ironclad/fiend_fire.png`
- `res://images/packed/card_portraits/ironclad/fiend_fire.png.import`
- `res://images/packed/card_portraits/ironclad/fight_me.png`
- `res://images/packed/card_portraits/ironclad/fight_me.png.import`
- `res://images/packed/card_portraits/ironclad/flame_barrier.png`
- `res://images/packed/card_portraits/ironclad/flame_barrier.png.import`
- `res://images/packed/card_portraits/ironclad/forgotten_ritual.png`
- `res://images/packed/card_portraits/ironclad/forgotten_ritual.png.import`
- `res://images/packed/card_portraits/ironclad/havoc.png`
- `res://images/packed/card_portraits/ironclad/havoc.png.import`
- `res://images/packed/card_portraits/ironclad/headbutt.png`
- `res://images/packed/card_portraits/ironclad/headbutt.png.import`
- `res://images/packed/card_portraits/ironclad/hellraiser.png`
- `res://images/packed/card_portraits/ironclad/hellraiser.png.import`
- `res://images/packed/card_portraits/ironclad/hemokinesis.png`
- `res://images/packed/card_portraits/ironclad/hemokinesis.png.import`
- `res://images/packed/card_portraits/ironclad/howl_from_beyond.png`
- `res://images/packed/card_portraits/ironclad/howl_from_beyond.png.import`
- `res://images/packed/card_portraits/ironclad/impervious.png`
- `res://images/packed/card_portraits/ironclad/impervious.png.import`
- `res://images/packed/card_portraits/ironclad/infernal_blade.png`
- `res://images/packed/card_portraits/ironclad/infernal_blade.png.import`
- `res://images/packed/card_portraits/ironclad/inferno.png`
- `res://images/packed/card_portraits/ironclad/inferno.png.import`
- `res://images/packed/card_portraits/ironclad/inflame.png`
- `res://images/packed/card_portraits/ironclad/inflame.png.import`
- `res://images/packed/card_portraits/ironclad/iron_wave.png`
- `res://images/packed/card_portraits/ironclad/iron_wave.png.import`
- `res://images/packed/card_portraits/ironclad/juggernaut.png`
- `res://images/packed/card_portraits/ironclad/juggernaut.png.import`
- `res://images/packed/card_portraits/ironclad/juggling.png`
- `res://images/packed/card_portraits/ironclad/juggling.png.import`
- `res://images/packed/card_portraits/ironclad/limit_break.png`
- `res://images/packed/card_portraits/ironclad/limit_break.png.import`
- `res://images/packed/card_portraits/ironclad/mangle.png`
- `res://images/packed/card_portraits/ironclad/mangle.png.import`
- `res://images/packed/card_portraits/ironclad/midnight.png`
- `res://images/packed/card_portraits/ironclad/midnight.png.import`
- `res://images/packed/card_portraits/ironclad/molten_fist.png`
- `res://images/packed/card_portraits/ironclad/molten_fist.png.import`
- `res://images/packed/card_portraits/ironclad/not_yet.png`
- `res://images/packed/card_portraits/ironclad/not_yet.png.import`
- `res://images/packed/card_portraits/ironclad/offering.png`
- `res://images/packed/card_portraits/ironclad/offering.png.import`
- `res://images/packed/card_portraits/ironclad/one_two_punch.png`
- `res://images/packed/card_portraits/ironclad/one_two_punch.png.import`
- `res://images/packed/card_portraits/ironclad/pacts_end.png`
- `res://images/packed/card_portraits/ironclad/pacts_end.png.import`
- `res://images/packed/card_portraits/ironclad/perfected_strike.png`
- `res://images/packed/card_portraits/ironclad/perfected_strike.png.import`
- `res://images/packed/card_portraits/ironclad/pillage.png`
- `res://images/packed/card_portraits/ironclad/pillage.png.import`
- `res://images/packed/card_portraits/ironclad/pommel_strike.png`
- `res://images/packed/card_portraits/ironclad/pommel_strike.png.import`
- `res://images/packed/card_portraits/ironclad/primal_force.png`
- `res://images/packed/card_portraits/ironclad/primal_force.png.import`
- `res://images/packed/card_portraits/ironclad/pyre.png`
- `res://images/packed/card_portraits/ironclad/pyre.png.import`
- `res://images/packed/card_portraits/ironclad/rage.png`
- `res://images/packed/card_portraits/ironclad/rage.png.import`
- `res://images/packed/card_portraits/ironclad/rampage.png`
- `res://images/packed/card_portraits/ironclad/rampage.png.import`
- `res://images/packed/card_portraits/ironclad/rupture.png`
- `res://images/packed/card_portraits/ironclad/rupture.png.import`
- `res://images/packed/card_portraits/ironclad/second_wind.png`
- `res://images/packed/card_portraits/ironclad/second_wind.png.import`
- `res://images/packed/card_portraits/ironclad/setup_strike.png`
- `res://images/packed/card_portraits/ironclad/setup_strike.png.import`
- `res://images/packed/card_portraits/ironclad/shrug_it_off.png`
- `res://images/packed/card_portraits/ironclad/shrug_it_off.png.import`
- `res://images/packed/card_portraits/ironclad/spite.png`
- `res://images/packed/card_portraits/ironclad/spite.png.import`
- `res://images/packed/card_portraits/ironclad/stampede.png`
- `res://images/packed/card_portraits/ironclad/stampede.png.import`
- `res://images/packed/card_portraits/ironclad/stoke.png`
- `res://images/packed/card_portraits/ironclad/stoke.png.import`
- `res://images/packed/card_portraits/ironclad/stomp.png`
- `res://images/packed/card_portraits/ironclad/stomp.png.import`
- `res://images/packed/card_portraits/ironclad/stone_armor.png`
- `res://images/packed/card_portraits/ironclad/stone_armor.png.import`
- `res://images/packed/card_portraits/ironclad/strike_ironclad.png`
- `res://images/packed/card_portraits/ironclad/strike_ironclad.png.import`
- `res://images/packed/card_portraits/ironclad/sword_boomerang.png`
- `res://images/packed/card_portraits/ironclad/sword_boomerang.png.import`
- `res://images/packed/card_portraits/ironclad/tank.png`
- `res://images/packed/card_portraits/ironclad/tank.png.import`
- `res://images/packed/card_portraits/ironclad/taunt.png`
- `res://images/packed/card_portraits/ironclad/taunt.png.import`
- `res://images/packed/card_portraits/ironclad/tear_asunder.png`
- `res://images/packed/card_portraits/ironclad/tear_asunder.png.import`
- `res://images/packed/card_portraits/ironclad/thrash.png`
- `res://images/packed/card_portraits/ironclad/thrash.png.import`
- `res://images/packed/card_portraits/ironclad/thunderclap.png`
- `res://images/packed/card_portraits/ironclad/thunderclap.png.import`
- `res://images/packed/card_portraits/ironclad/tremble.png`
- `res://images/packed/card_portraits/ironclad/tremble.png.import`
- `res://images/packed/card_portraits/ironclad/true_grit.png`
- `res://images/packed/card_portraits/ironclad/true_grit.png.import`
- `res://images/packed/card_portraits/ironclad/twin_strike.png`
- `res://images/packed/card_portraits/ironclad/twin_strike.png.import`
- `res://images/packed/card_portraits/ironclad/unmovable.png`
- `res://images/packed/card_portraits/ironclad/unmovable.png.import`
- `res://images/packed/card_portraits/ironclad/unrelenting.png`
- `res://images/packed/card_portraits/ironclad/unrelenting.png.import`
- `res://images/packed/card_portraits/ironclad/uppercut.png`
- `res://images/packed/card_portraits/ironclad/uppercut.png.import`
- `res://images/packed/card_portraits/ironclad/vicious.png`
- `res://images/packed/card_portraits/ironclad/vicious.png.import`
- `res://images/packed/card_portraits/ironclad/whirlwind.png`
- `res://images/packed/card_portraits/ironclad/whirlwind.png.import`
- `res://images/packed/character_select/char_select_ironclad.png`
- `res://images/packed/character_select/char_select_ironclad_locked.png`
- `res://images/packed/sprite_fonts/ironclad_energy_icon.png`
- `res://images/ui/combat/energy_counters/ironclad/ironclad_orb_layer_1.png`
- `res://images/ui/combat/energy_counters/ironclad/ironclad_orb_layer_1.png.import`
- `res://images/ui/combat/energy_counters/ironclad/ironclad_orb_layer_2.png`
- `res://images/ui/combat/energy_counters/ironclad/ironclad_orb_layer_2.png.import`
- `res://images/ui/combat/energy_counters/ironclad/ironclad_orb_layer_3.png`
- `res://images/ui/combat/energy_counters/ironclad/ironclad_orb_layer_3.png.import`
- `res://images/ui/combat/energy_counters/ironclad/ironclad_orb_layer_4.png`
- `res://images/ui/combat/energy_counters/ironclad/ironclad_orb_layer_4.png.import`
- `res://images/ui/combat/energy_counters/ironclad/ironclad_orb_layer_5.png`
- `res://images/ui/combat/energy_counters/ironclad/ironclad_orb_layer_5.png.import`
- `res://images/ui/hands/multiplayer_hand_ironclad_paper.png`
- `res://images/ui/hands/multiplayer_hand_ironclad_point.png`
- `res://images/ui/hands/multiplayer_hand_ironclad_rock.png`
- `res://images/ui/hands/multiplayer_hand_ironclad_scissors.png`
- `res://images/ui/top_panel/character_icon_ironclad.png`
- `res://images/ui/top_panel/character_icon_ironclad_outline.png`
- `res://scenes/combat/energy_counters/ironclad_energy_counter.tscn`
- `res://scenes/rest_site/characters/ironclad_rest_site.tscn`
- `res://scenes/ui/character_icons/ironclad_icon.tscn`
