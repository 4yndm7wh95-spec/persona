# Regent visual replacement contracts (non-combat visuals + weapon)

Research date 2026-10-10. Sources: `research/src_full` (decompiled sts2.dll), `research/regent_assets` (extracted Regent files),
`research/regent_assets/mods/Mesugaki_Regent_0.1.1/Mesugaki` (GDRE-recovered Mesugaki pck), `research/mods_ref/mesugaki_src` (decompiled Mesugaki dll),
`research/extract_file_list.txt` (full game file listing).

Spine data was parsed with the official `@esotericsoftware/spine-core@4.2.80` `SkeletonBinary` (Node script in the scratchpad:
`/tmp/claude-0/-home-user-persona/2326f5fe-d6f1-5976-844b-a0702da38517/scratchpad/skel/dump.mjs <skel> <atlas>`; dumps bones, slots, skins, events, animations, plus any atlas regions the skel needs that the atlas lacks).
All game skeletons are Spine **4.2.x** binary (4.2.08 to 4.2.43). The Mesugaki ones are 4.2.38. All of them load in the game's spine-godot runtime.

General path helpers (from `CharacterModel.cs`, with `Id.Entry.ToLowerInvariant()` == `regent`):
- `SceneHelper.GetScenePath("x")` -> `res://scenes/x.tscn`
- `ImageHelper.GetImagePath("x")` -> `res://images/x`

---

## 1. Sovereign Blade / Regent weapon spine

### 1a. Two separate things are involved
1. **`regent_weapon` skeleton**: the small "floating weapons" that slash during the Regent's normal `attack` animation. They are part of the combat creature scene.
2. **The Sovereign Blade card VFX** (the big orbiting sword created by Forge): `res://scenes/vfx/sovereign_blade.tscn`, driven by `NSovereignBladeVfx`. It uses its **own** spine,
   `res://animations/vfx/vfx_sovereign_blade/soveriegn_blade.skel` (note the game's misspelling "soveriegn"), plus `sovereign_blade.atlas/.png` and `sovereign_blade_skel_data.tres`,
   and textures under `res://images/vfx/sovereign_blade/*.png`. These files are in the game pck (listed in extract_file_list.txt) but **were not extracted** into research/.
   It does **not** use `regent_weapon.*`.

### 1b. regent_weapon files
| res path | notes |
|---|---|
| `res://animations/characters/regent/regent_weapon_skel_data.tres` | uid `uid://tjpvqmcen4vb`; `atlas_res`=regent_weapon.atlas (uid `u0e8t6d1wowb`), `skeleton_file_res`=regent_weapon.skel (uid `bitv6cp6gcvao`); no default_mix |
| `res://animations/characters/regent/regent_weapon.skel` | 9737 B, Spine 4.2.08, importer `spine.skel` -> `res://.godot/imported/regent_weapon.skel-eb5748d45dfdb21acd5f11da647cab6a.spskel` |
| `res://animations/characters/regent/regent_weapon.atlas` | page `regent_weapon.png`, `size:231,134`, `scale:0.4`; regions: `medium weapon`, `small big weapon`, `small weapon`. importer `spine.atlas` -> `regent_weapon.atlas-21617671264f7238b97e6acee344b1c3.spatlas` |
| `res://animations/characters/regent/regent_weapon.png` | **231x134** RGBA, 13442 B; uid `uid://ofbcey3ceehb`; compress/mode=0 (lossless), no mipmaps; ctex `regent_weapon.png-1ac109124cc3d828fea1d2cd94be4d30.ctex` (10572 B) |

**Parsed skeleton (regent_weapon.skel)**
- size x=-295.0 y=-55.4 w=694 h=1272, fps 28.8
- Bones: `root`, `weapons_base`<-root, then children of `weapons_base`: `weapol_large`, `weapon_med`, `weapon_small`, `weapon_med2`, `weapon_small2`, `weapol_large2`, `weapon_med3`, `weapol_large3` (the "weapol" misspelling is in the data)
- Slots (setup attachment is **null on all**, so the weapons are invisible at rest):
  `big weapon`, `big weapon3`, `big weapon2`, `big weapon add`(additive), `big weapon add3`(add), `big weapon add2`(add), `medium weapon`, `medium weapon5`, `medium weapon3`, `medium weapon add`(add), `medium weapon add2`(add), `medium weapon3 add`(add), `small weapon`, `small weapon3`, `small weapon add`(add), `small weapon add2`(add), `placeholder`(root)
- Skin `default`: big weapon* slots -> attachment `small big weapon`; medium* -> `medium weapon`; small* -> `small weapon`
- Events defined: `attack1`, `attack2`, `attack_end`, `attack_test`, `death_particles_end`, `death_particles_start`, `death_particles_start2`, `explode_dead`, `explode_end` (copied from the body rig; only `attack_test` is keyed)
- Animations: **`attack`** (0.972 s, event attack_test), **`attack2`** (0.972 s, event attack_test). There is **no idle**.
- The main `regent.skel` contains the same `weapons_base`/`weapol_*`/`weapon_*` bones and the same 16 weapon slots (all null). So the weapon rig was split out of the body rig.

**Scene attachment**: `res://scenes/creature_visuals/regent.tscn` (root `Regent` Node2D, script `NCreatureVisuals.cs`):
```
Regent (Node2D, NCreatureVisuals)
├─ %FormVfx (Control)
├─ %Visuals (SpineSprite, regent_skel_data.tres, pos (2,-20), scale 0.29, preview "idle_loop")
│   ├─ Weapons (SpineSlotNode, slot_name = "shadow", show_behind_parent, pos (-19.94,-1.52))
│   │   ├─ WeaponAnim1 (SpineSprite, regent_weapon_skel_data.tres, pos (-19.3117,-131.808), rot 0.00574)
│   │   └─ WeaponAnim2 (SpineSprite, regent_weapon_skel_data.tres, pos (-19.312,-131.808), rot 0.00524)
│   ├─ Explosion (GPUParticles2D)
│   ├─ SpineArmBone (SpineBoneNode bone_name="arm_particle_attach")/Particles
│   ├─ SpineChestBone (SpineBoneNode bone_name="chest_particle_attach")/Particles, /ParticlesBack
│   ├─ SpineLegBoneL (SpineBoneNode bone_name="leg_particle_attach_l")/Particles
│   ├─ SpineLegBone (SpineBoneNode bone_name="leg_particle_attach")/Particles
│   └─ NRegentVfx (Node, NRegentVfx.cs)
├─ %Bounds (Control)  ├─ %IntentPos (Marker2D (24,-414))  └─ %CenterPos (Marker2D (0,-178))
```
So the weapon sprites follow the body slot **`shadow`** (bone `shadow` in regent.skel), not an `*_attach` bone. The `*_attach` bones (`arm_particle_attach`, `chest_particle_attach`, `leg_particle_attach`, `leg_particle_attach_l`) carry only death particles.

**Code: `MegaCrit.Sts2.Core.Nodes.Vfx/NRegentVfx.cs`** (`res://src/Core/Nodes/Vfx/NRegentVfx.cs`, a child of `Visuals`)
- `_Ready`: `_parent = GetParent<Node2D>()` (Visuals); `GetNode("Weapons/WeaponAnim1")`, `GetNode("Weapons/WeaponAnim2")`; GPU particles at `SpineArmBone/Particles`, `SpineChestBone/Particles`, `SpineChestBone/ParticlesBack`, `SpineLegBone/Particles`, `SpineLegBoneL/Particles`, `Explosion`. Connects the body's animation event and animation started signals.
- `OnAnimationEvent` on the **body** skeleton by event name: `death_particles_start`, `death_particles_start2`, `death_particles_end`, `explode_dead`, `explode_end`, **`attack1`** -> `Attack()`.
- `Attack()` alternates: WeaponAnim1 `SetAnimation("attack", loop:false)`, the next time WeaponAnim2 `SetAnimation("attack2", loop:false)`.
- `OnAnimationStart`: if the current anim != `"die"` it turns off the particles.
- In regent.skel, event `attack1` is keyed in animation `attack` (along with `attack_test`, `attack_end`). The `die` animation keys `death_particles_start|death_particles_start2|death_particles_end|explode_dead|explode_end`.

**Contract to replace the weapon**: ship a skeleton at the same path (or repoint `regent_weapon_skel_data.tres`) that has animations named exactly `attack` and `attack2`. Its setup pose should be empty or invisible, because it is never set to idle and shows its setup pose all the time. Alternatively, ship an empty skeleton with just those two names to hide it. The body skel must fire event `attack1` to trigger it, and must have a slot named `shadow` for the SpineSlotNode parent. If the slot is missing, spine-godot just warns. The Mesugaki body rig has no `shadow` slot and the mod did not touch regent.tscn.

### 1c. NSovereignBladeVfx (Forge sword) contract
`MegaCrit.Sts2.Core.Nodes.Vfx/NSovereignBladeVfx.cs`, scene `SceneHelper.GetScenePath("vfx/sovereign_blade")` = `res://scenes/vfx/sovereign_blade.tscn` (88211 B in pck).
- Created by `ForgeCmd.PlayCombatRoomForgeVfx(player, card)` (`MegaCrit.Sts2.Core.Commands/ForgeCmd.cs`): `NSovereignBladeVfx.Create(card)` -> `nCreature.AddChildSafely(...)`, `Position = Vector2.Zero` (child of the player's **NCreature**, not of Visuals), then `.Forge(card.DynamicVars.Damage.IntValue, isNew)`. SFX `event:/sfx/characters/regent/regent_forge` / `regent_refine`. Multiple blades get `OrbitProgress = i/count`.
- `SovereignBlade.OnPlay` (`Models.Cards/SovereignBlade.cs`): attacker anim `"sovereignBladeTrigger"` if the character is Regent, else `"Cast"`, delay 0.25. It calls `vfxNode.Attack(targetCreature.VfxSpawnPosition)` in BeforeDamage. `Regent.AnimationStates` maps trigger `sovereignBladeTrigger` -> body anim **`attack_sovereign`**.
- Node paths required in sovereign_blade.tscn (exact):
  `SpineSword` (Node2D/SpineSprite; MegaSprite), `SpineSword/SwordBone/ScaleContainer/BladeGlow`, `.../ForgeSparks`, `.../SpawnFlames`, `.../SpawnFlamesBack`, `SpineSword/SlashParticles`, `.../ScaleContainer/ChargeParticles`, `.../Spikes`, `.../Spikes2`, `.../SpikeCircle`, `.../SpikeCircle2` (all GPUParticles2D except BladeGlow Node2D), `.../ScaleContainer/Hilt`, `Hilt2`, `Detail` (TextureRect), `Trail` (Line2D), `%Path` (Path2D orbit curve), `%Hitbox` (Control), `%SelectionReticle` (NSelectionReticle).
- Spine animations played on SpineSword: **`idle_loop`** (on ready, looping), **`attack`** (loop false), back to `idle_loop` after the attack.
- Forge logic: `_bladeSize = clamp(damage/200)`; Hilt visible if size<0.3, else Hilt2; `Detail.Visible = bladeDamage >= 0.66`; glow tween `#ff7300`; scale `lerp(0.9,2,size)`. Orbit at 60 px/s along `%Path`. The blade moves behind the character when the orbit phase is in (0.25, 0.78).
- **Simplest reskin**: replace the textures `images/vfx/sovereign_blade/sovereign_blade_*.png` (blade, blade_glow, blade_shine, decoration, hilt, hilt_2, outline_mask, spike, spike_2, star_center, star_center2, trail) and/or `animations/vfx/vfx_sovereign_blade/sovereign_blade.png` (+atlas). These must be extracted from the game pck first.

### 1d. Did Mesugaki replace it?
**No.** The Mesugaki pck has no regent_weapon.*, sovereign_blade.*, or creature_visuals/regent.tscn. It replaced `animations/characters/regent/regent.skel/.atlas/.png/regent_skel_data.tres` (body rig: 231 bones, 151 slots, anims `attack`(events attack1, attack_end), `cast`, `die`, `hurt`, `idle_loop`, `relaxed_loop`; **no `attack_sovereign`, no `shadow` slot, no `*_particle_attach` bones**). The original weapon spine therefore still floats on the missing `shadow` slot.

---

## 2. Rest site

**Scene** `res://scenes/rest_site/characters/regent_rest_site.tscn` (uid `uid://c8cilisfrt6xc`). Loaded via `CharacterModel.RestSiteAnimPath` = `SceneHelper.GetScenePath("rest_site/characters/regent_rest_site")`.
```
RegentRestSite (Node2D, scale 0.760006, script res://src/Core/Nodes/RestSite/NRestSiteCharacter.cs)
├─ SpineSprite2 (SpineSprite, skeleton_data_res=rest_site_regent_skel_data.tres, pos (7.89,11.84), preview "_tracks/light_off", skin "Default")
└─ ControlRoot (Control, offset (17.1,25.0))          <- GetNode("ControlRoot")
    ├─ %SelectionReticle (instance res://scenes/ui/selection_reticle.tscn, rect -238,-567 .. 203,136)
    ├─ %Hitbox (Control, -238,-570 .. 203,139)
    ├─ %ThoughtBubbleRight (Control at 168.42,-410.52)
    └─ %ThoughtBubbleLeft (Control at -169.74,-392.10)
```
**Code** `MegaCrit.Sts2.Core.Nodes.RestSite/NRestSiteCharacter.cs`
- `Create(player, idx)` instantiates the scene. `NRestSiteRoom` (`Nodes.Rooms/NRestSiteRoom.cs` ~l.336) adds it to `_characterContainers[i]` and calls `FlipX()` for odd indices.
- `_Ready`: unique nodes `%Hitbox`, `%SelectionReticle`, `%ThoughtBubbleLeft`, `%ThoughtBubbleRight`, `ControlRoot`.
  **Every direct child whose class is `SpineSprite`** (name does not matter) gets `SetAnimation(animName)` (looping) with a random start time, where animName depends on act:
  act 0 -> **`overgrowth_loop`**, act 1 -> **`hive_loop`**, act 2 -> **`glory_loop`**. Any other act throws InvalidOperationException.
- `HideFlameGlow()` (called by `NRestSiteRoom.ExtinguishFireIfAble`): if `HasAnimation("_tracks/light_off")`, it plays `_tracks/light_off` on **track 1**, looping. This is optional.
- `FlipX()` negates scale.x/position.x of child SpineSprites and ControlRoot.scale.x.
- `Necrobinder`-only nodes (`%NecroFire`, `%OstyFire`, `Osty`) are irrelevant.
- No smith/heal animations exist. Options only change thought-bubble icons.

**Data**
- `res://animations/rest_site/regent/rest_site_regent_skel_data.tres` uid `uid://cpcw77otglq37`; atlas uid `bvadtv5xwjjqq`, skel uid `bhc8fygyqmx31`; `default_mix = 0.2`.
- `restsite_regent.skel` 41916 B, Spine 4.2.43, w405 h781. Bones: root, cog, spine1, spine2, neck, head, face, eye, shoulder_l, shoulder_r, arm_l_upper, arm_l_Lower, arm_r_lower, arm_upper_r, breathe. IK `arm_l_IK_const`, `arm_r_IK_const`. Skin `default`. No events.
  Slots: `Background ref`, `ground shadow`, `throne`, `throne shadow`(multiply), `guys`, `bottom leg`, `top leg`, `lower arm`, `body`, `head`, `neck`, `top arm`, `chain`, `shoulders`, `hands`, `eye`, `shadow`, `front_minion_shadow`, `back_minion_shadow`, `highlight`(attachment `light`).
  Animations: `glory_loop` (8 s), `hive_loop` (8 s), `overgrowth_loop` (8 s), `_tracks/light_off` (0 s), `_tracks/light_on` (0 s).
- `restsite_regent.atlas`: page `restsite_regent.png`, `size:1833,713`, `scale:1.01`; regions: `body`, `bottom leg`, `chain`, `eye`, `ground shadow`, `guys`, `hands`, `head`, `light`, `lower arm`, `neck`, `shadow`, `shoulders`, `throne`, `throne shadow`, `top arm`, `top leg`.
- `restsite_regent.png` **1833x713** RGBA (472948 B), uid `uid://lwg6bnvvhfdg`, compress/mode=0, ctex `restsite_regent.png-4d66c44c2f847a4406b60a9ecab0785c.ctex` (294160 B).
- .skel.import -> `restsite_regent.skel-ec924cdb115783ec26dd49f16c211dfb.spskel`; .atlas.import -> `restsite_regent.atlas-966fe3c4717de76dfb16f4d6080388a9.spatlas`.

**Mesugaki approach**: it kept regent_rest_site.tscn and shipped a new **binary .skel** (921 B, Spine 4.2.38) + .atlas + .png + .import files + rest_site_regent_skel_data.tres (new uid `uid://dg3tgf1vyhq0d`, no default_mix; the scene's uid ref mismatches, so the path is used).
Its skeleton has bones `root`, `FX_Particle`, slots `Rest`, `FX_Particle`, and animations `glory_loop`, `hive_loop`, `loop`, `overgrowth_loop` (4 s each). It has no `_tracks/light_off`, which is fine because that call is guarded.
Atlas `size: 1024, 1024`, `scale: 0.5`, regions `FX_Particle`, `Rest`; png 1024x1024. The .png.import uid is `uid://dde8733x5fa1i`, dest path identical to vanilla (`...4d66c44c...ctex`), compress/mode=0.
No JSON. (The liuchuan reference mod in research/mods_ref used `.spine-json` instead.)

---

## 3. Merchant

**Scene** `res://scenes/merchant/characters/regent_merchant.tscn` (uid `uid://b1o0sdn255167`), via `CharacterModel.MerchantAnimPath` = `merchant/characters/regent_merchant`.
```
RegentMerchant (Node2D, script res://src/Core/Nodes/Screens/Shops/NMerchantCharacter.cs)
└─ SpineSprite (SpineSprite, regent_merchant_skel_data.tres, scale 0.47, preview "relaxed_loop", skin "Default")
```
**Code** `MegaCrit.Sts2.Core.Nodes.Screens.Shops/NMerchantCharacter.cs`: it uses **`GetChild(0)`** (so the SpineSprite must be the first child).
`_Ready` -> `PlayAnimation("relaxed_loop", loop:true)` with a random track time. `NGameOverScreen` (~l.982) calls `PlayAnimation("die")` (no loop) when the run ends in a shop.
`NMerchantRoom` (~l.249) instantiates, `Position = (-140*i - 275*j, -50*i)` grid in `_characterContainer`; rows i>0 get modulate 0.5 grey.

**Data**
- `res://animations/merchant/regent/regent_merchant_skel_data.tres` uid `uid://bs70xw06n8w5b`: `atlas_res` = `res://animations/merchant/regent/regent_shop.atlas` (uid `ckmvm451vebwy`), **`skeleton_file_res` = `res://animations/characters/regent/regent.skel`** (uid `dyyhq03h15cp3`). So the merchant uses the **combat body skeleton** with a different atlas.
  `default_mix = 0.05`; mixes cast->hurt, hurt->hurt, idle_loop->hurt (0.02), hurt->die.
- No merchant-specific .skel exists. Animations available (regent.skel): `attack`, `attack_sovereign`, `cast`, `defend`, `die`, `hurt`, `idle_loop`, `inactive loop`, `low_health_loop`, `relaxed_loop`. The merchant uses `relaxed_loop` and `die`.
- `regent_shop.atlas`: `size:2001,745`, `scale:0.674`; it covers every region regent.skel needs (verified: 0 missing). Region names: anger, belt, bod, bottm fist, bottom arm 1, bottom arm 1 flipped, bottom arm 2, bottom fingers, bottom hand, bottom hand freaked, bubble, chain, death flash, fist swish, half eye, hand, hand copy, head, inner_light, l shoulder, left guy, left guy highlights, left guy top arm, left guy top arm highlights, leg bottom, leg bottom bottom, leg top, leg top bottom, mad eye, minion sweat, neck, normal eye, r shoulder, regent death pop, right guy, right guy bottom arm, right guy bottom arm highlights, right guy highlights, right guy top arm, right guy top arm highlights, shadow, sparkle 3, star eye, throne bottom highlights, throne shadows, throne top, throne top highlights, throne_bottom, top arm 1, top arm 1 flipped, top arm 2, top arm 2 flipped, top fist, top hand 1.
- `regent_shop.png` **2001x745** RGBA (1033365 B), uid `uid://jyys7ulxmpgf`, mode 0, ctex `regent_shop.png-e988c5881b50293e1ab5a56cb92c600b.ctex` (747900 B). atlas.import -> `regent_shop.atlas-e43f8281849a38c6f5ba6194779968af.spatlas`.

**Mesugaki approach**: it replaced `regent_merchant.tscn` (scale **0.3**, skin "default", same script) and `regent_merchant_skel_data.tres` (uid `uid://df3n1pjvxovrj`) pointing to **its own body rig** `res://animations/characters/regent/regent.atlas` + `regent.skel`. That reuses the combat character (which has `relaxed_loop` and `die`). It did not ship a regent_shop atlas. The recovered tscn shows `script = ExtResource("1")` also on SpineSprite, which looks like a GDRE artifact. Do not copy it.

---

## 4. Character select

**Background scene** `res://scenes/screens/char_select/char_select_bg_regent.tscn` (uid `uid://b01ljrdqx0k6r`), via `CharacterModel.CharacterSelectBg` = `screens/char_select/char_select_bg_regent`.
Instantiated by `NCharacterSelectScreen` (l.695/837) and `NMultiplayerLoadGameScreen` (l.475) as a `Control` into `_bgContainer`, renamed `<Id>_bg`.
```
RegentBg (Control, anchors center, offsets -960,-521 .. 1600,679, pivot (1280,600), script NRegentCharacterSelectBg.cs)
├─ SpineSprite (SpineSprite, characterselect_regent_skel_data.tres, pos (-185,-20), scale 0.46, preview "animation", skin "default")
│   ├─ 36x SpineMesh2D children (_SpineMesh2D_148096.. / @SpineMesh2D@2309xx) – editor-baked, can be dropped
│   └─ NSpineAutoPlayer (Node, res://src/Core/Nodes/Animation/NSpineAutoPlayer.cs)
├─ SphereGuardianHover (Control 825,70..1025,256)
├─ DecaHover (172,74..509,360)
├─ SentryHover (-136,497..63,898)
├─ SneckoHover (348,675..671,989)
├─ CultistHover (1794,403..2062,646)
├─ ShapesHover (1930,839..2135,1032)
├─ AmogusHover (-170,9..35,202)
└─ stars (CPUParticles2D, texture res://images/vfx/dot.png, 300 particles)
```
**`NRegentCharacterSelectBg.cs`** (`MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect`, ScriptPath `res://src/Core/Nodes/Screens/CharacterSelect/NRegentCharacterSelectBg.cs`), full logic:
- `_spineController = new MegaSprite(GetNode("SpineSprite"))`
- `GetNode<Control>` for the 7 hover nodes (**hard GetNode, so they must exist**): `SphereGuardianHover`, `DecaHover`, `SentryHover`, `SneckoHover`, `CultistHover`, `ShapesHover`, `AmogusHover`.
- MouseEntered -> `SetSkin(...)`: `"spheric guardian constellation"`, `"deca outline"`, `"sentry constellation"`, `"snecko constellation"`, `"cultist constellation"`, `"shapes constellation"`, `"amogus constellation"`. MouseExited -> `SetSkin("normal")`.
- `SetSkin(name)`: `skeleton.SetSkin(skeleton.GetData().FindSkin(name)); skeleton.SetSlotsToSetupPose()`. `MegaSkeleton.SetSkin` is null-safe, so missing skins are no-ops (but SetSlotsToSetupPose still runs).
- No animation is driven by this script.

**`NSpineAutoPlayer.cs`** (`Nodes.Animation`): on spine ready it requires the parent skeleton to have **exactly 1 animation**, otherwise it throws `InvalidOperationException`. It plays that animation (looping).

**Data**
- `characterselect_regent_skel_data.tres` uid `uid://cclyt84b6otgm`; atlas uid `cp3i8hyc6sf5t`, skel uid `cwe3mjowcji7c`.
- `characterselect_regent.skel` 17305 B, Spine 4.2.40, w5517.9 h2548. One animation: **`animation`** (6.667 s). Skins: `default`, `amogus constellation`, `cultist constellation`, `deca outline`, `normal`, `sentry constellation`, `shapes constellation`, `snecko constellation`, `spheric guardian constellation`. No events.
  Bones: root, bone..bone31, `top arm`, `top arm2`, `bottom arm`, `bottom arm2`, `head`, `top leg copy`, `bottom leg`, `bottom leg2`, `eye`, `amogus`, `deca`, `spheric guardian`, `sentry`, `snecko`, `cultist`, `shapes`.
  Slots: Layer 5, swirls, stars_a..stars_h, throne, head, eye, sleeve hole, bottom hand, bottom arm, bod, chains shadow, shoulders, chain, top leg, bottom leg, top leg copy, throne arm, top hand glow, top hand, top arm, spheric guardian, snecko, amogus, sentry, shapes, deca, cultist, guardian_outline, snecko_outline, amogus_outline, sentry_outline, shapes_outline, deca_outline, cultist_outline.
- `characterselect_regent.atlas`: `size:5627,2435`, `scale:0.5`, 47 regions (Layer 5, amogus, amogus_outline, bod, bottom arm, bottom hand, bottom leg, chain, chains shadow, cultist(_outline), deca(_outline), eye, guardian_outline, head, sentry(_outline), shapes(_outline), shoulders, sleeve hole, snecko(_outline), spheric guardian, stars_a..stars_h, swirls, throne, throne arm, top arm, top hand, top hand glow, top leg, top leg copy).
- `characterselect_regent.png` **5628x2436** RGBA (6093450 B), uid `uid://dn8n5p1uygysl`, **VRAM compressed** (`compress/mode=2`, `normal_map=2`, `detect_3d/compress_to=1`, `imported_formats ["s3tc_bptc","etc2_astc"]`), ctex `res://.godot/imported/characterselect_regent.png-65cd54b849bfc5e8b98e4b2715b51ff0.s3tc.ctex` (13709860 B), import key `path.s3tc=`.

**Mesugaki approach**: it replaced the bg tscn (uid `mmotol4axbj0`): offsets -576,-305..1984,895, SpineSprite pos (-270,-170) scale 0.7, preview "-- Empty --". It has **no NSpineAutoPlayer**, no SpineMesh2D children and no stars. It kept the 7 hover Controls but set them `visible = false`, so the script's GetNode calls still succeed.
It also shipped a new skel (333 B, one bone `root`, slot `BG`, one animation `animation` of 0 s), an atlas (`size: 2048, 2048`, `scale: 0.7`, region `BG`), and a png 2048x2048. Its .png.import uses **compress/mode=0** with dest `...65cd54b849bfc5e8b98e4b2715b51ff0.ctex` (no `.s3tc`, plain `path=`). Its skel_data.tres has uid `bl0cf2pce1td2`.

**Button portrait** (`NCharacterSelectButton.cs`): `_icon = GetNode<TextureRect>("%Icon")` (with an HSV ShaderMaterial), `%IconAdd`, `%Lock`.
Locked -> `_icon.Texture = character.CharacterSelectLockedIcon`, else `CharacterSelectIcon`. The unlock animation sets `_iconAdd.Texture = _icon.Texture`.
Both are loaded with `ResourceLoader.Load<CompressedTexture2D>(path)`, so they **must be CompressedTexture2D** (an imported png) and cannot be an AtlasTexture:
- `res://images/packed/character_select/char_select_regent.png` **132x195** RGBA, uid `uid://4rucvor0rrtj`, mode 0, ctex `char_select_regent.png-76fe3440499e294a36f663ff4cc83733.ctex` (23860 B)
- `res://images/packed/character_select/char_select_regent_locked.png` **132x195**, uid `uid://ct84r6405x1tk`, mode 0, ctex `char_select_regent_locked.png-b22fbeba54c127648b00bcb04fc1d932.ctex` (2284 B)
- Mesugaki replaced both (132x195, mode 0, same dest paths, new uids `cg7f1w2u0vc5g` / `bato5j1a3wfkm`).

Related: `CharacterSelectTransitionPath` = `res://materials/transitions/regent_transition_mat.tres` (ShaderMaterial; it references `res://images/ui/transitions/ironclad_transition.png`). `AssetPathsCharacterSelect` = {CharacterSelectBg, char_select png, top-panel icon png, locked png, transition mat}.

---

## 5. Top panel icon / map marker

`CharacterModel.cs` (`MegaCrit.Sts2.Core.Models/CharacterModel.cs` l.117-175):
- `IconTexturePath` = `res://images/ui/top_panel/character_icon_regent.png` -> `IconTexture` = `PreloadManager.Cache.GetTexture2D(...)` (Texture2D). Used by NContinueRunInfo, NMultiplayerPlayerState, NRemoteLobbyPlayer, NRunHistoryPlayerIcon, NAncientDialogueLine (`Icon`), NBestiaryCharacterFilter (`_image` + `%Shadow`), NBestiary, NMultiplayerVoteContainer.
- `IconOutlineTexturePath` = `.../character_icon_regent_outline.png` -> `IconOutlineTexture`. Used by NAncientDialogueLine (`Icon/Outline`), NBestiary, NMultiplayerVoteContainer (`Outline`).
- `IconPath` = `res://scenes/ui/character_icons/regent_icon.tscn` -> `Icon` (instantiated Control). Used by **`NTopBarPortrait.Initialize`** (`AddChildSafely(player.Character.Icon)`) and NDailyRunCharacterContainer.
- `regent_icon.tscn` (uid `uid://npq0fyqjj5ih`): a single node `RegentIcon` (TextureRect, full-rect anchors, mouse_filter 2, `expand_mode = 1`, `stretch_mode = 5` keep-aspect-centered) with texture ext_resource uid `ddwry85j3nb6` = character_icon_regent.png. No script.
- `MapMarkerPath` = `res://images/packed/map/icons/map_marker_regent.png` -> `MapMarker` = `PreloadManager.Cache.GetCompressedTexture2D` (**must be CompressedTexture2D**). Used by `NMapMarker.Initialize` (`Texture = ...`, offset `(-Size.X/2, -35)`) and `NReactionWheel` (`_marker.Texture`).
  The other characters' markers have `ui_atlas.sprites/map/icons/map_marker_*.tres`, but **Regent's has none**: it is standalone.
- `AssetPaths` (preloaded per run) = VisualsPath, IconTexturePath, IconPath, EnergyCounterPath, RestSiteAnimPath, MerchantAnimPath, transition mat, MapMarkerPath, TrailPath.

Sizes:
| file | size | import |
|---|---|---|
| `images/ui/top_panel/character_icon_regent.png` | **88x88** RGBA 7882 B | uid `uid://ddwry85j3nb6`, **VRAM** `compress/mode=2`, `high_quality=true` (BPTC), `detect_3d/compress_to=1`, key `path.bptc=` -> `res://.godot/imported/character_icon_regent.png-8d6d8217cfa17ab5d1d955141395ae1c.bptc.ctex` (7796 B) |
| `images/ui/top_panel/character_icon_regent_outline.png` | **88x88** RGBA 1051 B | uid `uid://br0nmhqx2ci7a`, VRAM mode 2, high_quality=false (S3TC), key `path.s3tc=` -> `character_icon_regent_outline.png-8f100b17804ffdd41fc2808ffaf95e5b.s3tc.ctex` (7796 B) |
| `images/packed/map/icons/map_marker_regent.png` | **49x64** RGBA 4533 B | uid `uid://2mjv464plgnx`, mode 0 -> `map_marker_regent.png-90c6692dc569c548f7b9af956c0226ea.ctex` (3946 B) |

Mesugaki replaced both top-panel icons (88x88) and **switched them to compress/mode=0** with dest `...8d6d8217....ctex` / `...8f100b17....ctex` (no `.bptc`/`.s3tc` suffix, plain `path=`). New uids: `c4aa5nkegjoda` / `d3dgtk2y305xw`. It did not replace regent_icon.tscn (whose uid ref then misses, so the path is used) or the map marker.

---

## 6. Import metadata summary (for shipping in a pck)

All the textures below are **standalone CompressedTexture2D** (`importer="texture"`, `type="CompressedTexture2D"`), **not** atlas sprites:

| res:// source | uid | mode | imported dest |
|---|---|---|---|
| animations/characters/regent/regent_weapon.png (231x134) | ofbcey3ceehb | 0 | .godot/imported/regent_weapon.png-1ac109124cc3d828fea1d2cd94be4d30.ctex |
| animations/rest_site/regent/restsite_regent.png (1833x713) | lwg6bnvvhfdg | 0 | restsite_regent.png-4d66c44c2f847a4406b60a9ecab0785c.ctex |
| animations/merchant/regent/regent_shop.png (2001x745) | jyys7ulxmpgf | 0 | regent_shop.png-e988c5881b50293e1ab5a56cb92c600b.ctex |
| animations/character_select/regent/characterselect_regent.png (5628x2436) | dn8n5p1uygysl | 2 (s3tc/etc2, normal_map=2) | characterselect_regent.png-65cd54b849bfc5e8b98e4b2715b51ff0.s3tc.ctex |
| images/packed/character_select/char_select_regent.png (132x195) | 4rucvor0rrtj | 0 | char_select_regent.png-76fe3440499e294a36f663ff4cc83733.ctex |
| images/packed/character_select/char_select_regent_locked.png (132x195) | ct84r6405x1tk | 0 | char_select_regent_locked.png-b22fbeba54c127648b00bcb04fc1d932.ctex |
| images/ui/top_panel/character_icon_regent.png (88x88) | ddwry85j3nb6 | 2 bptc HQ | character_icon_regent.png-8d6d8217cfa17ab5d1d955141395ae1c.bptc.ctex |
| images/ui/top_panel/character_icon_regent_outline.png (88x88) | br0nmhqx2ci7a | 2 s3tc | character_icon_regent_outline.png-8f100b17804ffdd41fc2808ffaf95e5b.s3tc.ctex |
| images/packed/map/icons/map_marker_regent.png (49x64) | 2mjv464plgnx | 0 | map_marker_regent.png-90c6692dc569c548f7b9af956c0226ea.ctex |

Common mode-0 params: `compress/high_quality=false, lossy_quality=1.0, normal_map=0, mipmaps/generate=false, process/fix_alpha_border=false, premult_alpha=false, size_limit=0, detect_3d/compress_to=0`; `metadata={"vram_texture": false}`.
Spine resources: `importer="spine.skel"` type `SpineSkeletonFileResource` -> `.spskel`; `importer="spine.atlas"` type `SpineAtlasResource` -> `.spatlas` (params `normal_map_prefix="n"`, `specular_map_prefix="s"`). The atlas loads its page png by name relative to the .atlas file, so the png must sit next to it with its own .import.

The hash in the dest filename is md5 of the source res path, so a replacement at the **same res path gets the same dest name**. Mesugaki shipped `.png` + `.png.import` + `.godot/imported/*.ctex` (+ `.md5`) for every override, and changed VRAM icons to mode 0 by rewriting the `.import` (the remap `path=` is authoritative, so the dest name can differ from vanilla).

**Atlas-packed (harder)**: `images/atlases/*.sprites/*.tres` are `AtlasTexture`s into big shared atlases: card portraits (`card_atlas.sprites/regent/*.tres`), relics (`relic_atlas.sprites/mini_regent.tres`, `yummy_cookie_regent.tres`, plus `relic_outline_atlas.sprites/...`), `ui_atlas.sprites/card/energy_regent.tres`.
None of the items in this document are in an atlas. Mesugaki's trick for atlas sprites: override the `.tres` itself so it points at a standalone png, e.g. `relic_atlas.sprites/mini_regent.tres` -> `atlas = ExtResource("res://images/relics/mini_regent.png")`, `region = Rect2(2, 0, 256, 256.5)`.

---

## 7. Mesugaki mod mechanics

- `ModStart.cs` (`[ModInitializer("ModInit")]`): it only does `GD.Print` + `new Harmony("com.seicoh.mesugaki.mod").PatchAll()`. The decompiled dll (`research/mods_ref/mesugaki_src`) contains **no `[HarmonyPatch]` classes**, so it is effectively a pure pck-override mod. Manifest: `has_pck: true, has_dll: true, affects_gameplay: false`.
- PCK contents (60 files, Godot 4.5.1): body rig (`animations/characters/regent/regent.{skel,atlas,png}` + imports + `regent_skel_data.tres`), rest site (skel/atlas/png/imports + skel_data.tres), merchant (`regent_merchant.tscn` + `regent_merchant_skel_data.tres`), char select (skel/atlas/png/imports + skel_data.tres + `char_select_bg_regent.tscn`), `char_select_regent(_locked).png`, top panel icons, `multiplayer_hand_regent_{point,rock,paper,scissors}.png` (422x1200), relic `mini_regent(_outline).png` + atlas .tres overrides.
- Scenes and .tres were exported as binary (`.godot/exported/133200997/export-<md5>-<name>.scn/.res` with remaps). The `.tscn` files in research/ are GDRE reconstructions.
- It shipped placeholder 1-byte `src/Core/Nodes/Screens/CharacterSelect/NRegentCharacterSelectBg.cs` and `src/Core/Nodes/Screens/Shops/NMerchantCharacter.cs` (export side effect of referencing game scripts by path). The scripts themselves resolve to the game's sts2.dll classes via ScriptPath.
- New uids everywhere. Scenes that it did not override (e.g. regent_rest_site.tscn, regent_icon.tscn) reference vanilla uids, which then fall back to the `path=` in ext_resource. This works, but the engine logs warnings.
- Not replaced: regent_weapon, sovereign blade VFX, creature_visuals/regent.tscn, map marker, regent_shop atlas, energy counter, card trail.

## Key exact names checklist
- Weapon: anims `attack`, `attack2`; body event `attack1`; SpineSlotNode slot `shadow`; node paths `Visuals/Weapons/WeaponAnim1`, `Visuals/Weapons/WeaponAnim2`.
- Body (combat, also the merchant): `idle_loop`, `relaxed_loop`, `die`, `hurt`, `cast`, `attack`, `attack_sovereign` (trigger `sovereignBladeTrigger`); events `death_particles_start`, `death_particles_start2`, `death_particles_end`, `explode_dead`, `explode_end`, `attack1`; bones `arm_particle_attach`, `chest_particle_attach`, `leg_particle_attach`, `leg_particle_attach_l`.
- Sovereign blade VFX spine: `idle_loop`, `attack`.
- Rest site: `overgrowth_loop`, `hive_loop`, `glory_loop`, optional `_tracks/light_off` (track 1); nodes `ControlRoot`, `%Hitbox`, `%SelectionReticle`, `%ThoughtBubbleLeft`, `%ThoughtBubbleRight`; any direct SpineSprite child.
- Merchant: child 0 must be the SpineSprite; `relaxed_loop` (loop), `die`.
- Char select bg: node `SpineSprite` + `NSpineAutoPlayer` (exactly one animation); 7 hover Controls by name; skins `normal`, `spheric guardian constellation`, `deca outline`, `sentry constellation`, `snecko constellation`, `cultist constellation`, `shapes constellation`, `amogus constellation`.
