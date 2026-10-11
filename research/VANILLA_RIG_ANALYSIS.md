# 原版 Regent 战斗骨骼（regent.skel）绑定与动画分析

> 数据来源：`research/regent_assets/animations/characters/regent/regent.skel` + `regent.atlas`。
> 解析器：`tools/spine/skel_reader.py`（逐行移植自 spine-runtimes 4.2 分支 `SkeletonBinary.java`），完整读取 428,656 / 428,656 字节，读指针恰好停在 EOF。完整 JSON 导出：`research/vanilla_regent.json`。
> 约定：所有坐标/长度为 Spine 编辑器单位（scale=1，未乘 atlas 缩放）；旋转/位移/切变的时间轴值是**相对 setup pose 的增量**，缩放值是**乘到 setup 缩放上的倍数**（Spine 4.x 语义）。帧号按骨架文件中记录的 fps=30 换算（f = 秒×30）。

## 0. 文件头与贴图集

| 项 | 值 |
|---|---|
| Spine 版本 | 4.2.43 |
| hash | 4266869473554700372 |
| 包围盒 x / y / w / h | -338 / -88 / 729.3 / 1273 |
| referenceScale | 100 |
| nonessential 数据 | 有（fps=30，images=`./images/`） |
| 骨骼 / 插槽 / 皮肤 | 103 / 81 / 1（仅 `default`） |
| IK / Transform / Path / Physics 约束 | 10 / 9 / 0 / **0** |
| 附件总数 | 78（mesh 40 + linkedmesh 3 + region 35；无 boundingbox/path/point/clipping） |
| 事件定义 | 9 |
| 动画 | 10 |
| atlas | 单页 `regent.png` 952×523，`scale:0.385`，54 个 region，全部被骨骼引用 |

atlas 的 `scale:0.385` 表示贴图是按原画 38.5% 打包的：例如 `belt` 在 atlas 里 bounds 为 38×11，而骨骼中 region 附件的 width/height 为 98×29（38/98≈0.388）。骨骼数据本身仍以原始 1.0 尺寸计量（角色整体高约 1273 单位）。

## 1. 骨骼

- 骨骼总数 **103**，根为 `root`，**最大层级深度 10**（root=0）。深度分布：0:1，1:21，2:22，3:14，4:14，5:10，6:4，7:5，8:5，9:4，10:3。最深链：`root → butt_attach → regent_cog → body → shoulder_l → arm_upper_l → arm_lower_l → hand → pinky_l1/pinky_l4/index_l → …3 节`。
- 结构上这不是“一个人”，而是**王座 + 两个仆从（minion）抬着坐着的 Regent**：`throne_*`（4 根）、右仆从（11 根）、左仆从（7 根）、IK/约束目标骨（9 根：7 个仆从 `*_const` + `elbow_r_ik` + `arm_l_rest_ik_targ`）、Regent 本体（含 2+3+3+3 共 11 根左手手指骨）、武器挂点 `weapons_base` + 8 根武器骨、特效骨（inner_light×5、sparkle×3、death_pop×2、fist_swish×2、minion sweat×2、shadow、emote）。
- Regent 通过 `regent_transform_const`（Transform 约束，mix rot/x/y=1）把 `butt_attach` 钉在 `throne_bone` 上——**整个人是跟着王座动的**；两个仆从的 cog 通过 `minion_l/r_body_const` 以 ~0.5 的位移 mix 跟随 `throne_twist`。
- 4 根粒子挂点骨（`arm_particle_attach`、`chest_particle_attach`、`leg_particle_attach`、`leg_particle_attach_l`）使用 `inherit=noRotationOrReflection`，其余全部 `normal`。没有任何骨骼标记为 skin-required。

### 1.1 骨骼树（带约束标注）

- `root` (父: —, 长 0) — TC目标:headD_rot_const
  - `throne_adjust` (父: root, 长 0)
    - `throne_bone` (父: throne_adjust, 长 207) — TC目标:regent_transform_const
      - `throne_twist` (父: throne_bone, 长 0) — TC目标:minion_l_body_const, TC目标:minion_r_body_const, TC目标:throne_twist_counter_const
        - `minion_l_arm_ik1_const` (父: throne_twist, 长 0) — IK目标:minion_l_arm_ik1_const
        - `minion_r_ik1_const` (父: throne_twist, 长 0) — IK目标:minion_r_arm_ik1_const
        - `arm_l_rest_ik_targ` (父: throne_twist, 长 0) — IK目标:arm_l_rest_ik_const
      - `throne_counter_twist` (父: throne_bone, 长 0) — TC目标:throne_elbow_r_trans_const, TC:throne_twist_counter_const
        - `minion_r_arm_ik2_const` (父: throne_counter_twist, 长 0) — IK目标:minion_r_arm_ik2_const, TC目标:minion_r_arm_tip_rot_const
  - `minion_r_cog` (父: root, 长 68) — TC:minion_r_body_const
    - `minion_shoulder_r` (父: minion_r_cog, 长 0)
      - `minion_r_arm_3` (父: minion_shoulder_r, 长 69) — IK:minion_r_arm_ik1_const
        - `minion_r_arm_4` (父: minion_r_arm_3, 长 70) — IK:minion_r_arm_ik1_const
    - `minion_r_arm_1` (父: minion_r_cog, 长 52) — IK:minion_r_arm_ik2_const
      - `minion_r_arm_2` (父: minion_r_arm_1, 长 69) — IK:minion_r_arm_ik2_const
        - `minion_r_arm_tip` (父: minion_r_arm_2, 长 11) — TC:minion_r_arm_tip_rot_const
    - `minion_r_cog6` (父: minion_r_cog, 长 64) — IK:minion_r_leg_ik1_const
      - `minion_r_cog7` (父: minion_r_cog6, 长 70) — IK:minion_r_leg_ik1_const
    - `minion_r_cog8` (父: minion_r_cog, 长 38) — IK:minion_r_leg_ik2_const
      - `minion_r_cog9` (父: minion_r_cog8, 长 80) — IK:minion_r_leg_ik2_const
  - `minion_l_cog` (父: root, 长 109) — TC:minion_l_body_const
    - `minion_l_leg_1` (父: minion_l_cog, 长 61) — IK:minion_l_leg_ik2_const
      - `minion_l_leg_3` (父: minion_l_leg_1, 长 63) — IK:minion_l_leg_ik2_const
    - `minion_l_leg_2` (父: minion_l_cog, 长 74) — IK:minion_l_leg_ik1_const
      - `minion_l_leg_4` (父: minion_l_leg_2, 长 82) — IK:minion_l_leg_ik1_const
    - `minion_l_arm_1` (父: minion_l_cog, 长 68) — IK:minion_l_arm_ik1_const
      - `minion_l_arm_2` (父: minion_l_arm_1, 长 103) — IK:minion_l_arm_ik1_const
  - `minion_r_leg_ik2_const` (父: root, 长 0) — IK目标:minion_r_leg_ik2_const
  - `minion_r_leg_ik1_const` (父: root, 长 0) — IK目标:minion_r_leg_ik1_const
  - `minion_l_leg_ik2_const` (父: root, 长 0) — IK目标:minion_l_leg_ik2_const
  - `minion_l_leg_ik1_const` (父: root, 长 0) — IK目标:minion_l_leg_ik1_const
  - `butt_attach` (父: root, 长 0) — TC:regent_transform_const
    - `regent_cog` (父: butt_attach, 长 0)
      - `body` (父: regent_cog, 长 105)
        - `neck` (父: body, 长 75)
          - `head` (父: neck, 长 183) — TC目标:head_bubble_const
            - `face_twist` (父: head, 长 0)
              - `eye` (父: face_twist, 长 0)
          - `head_rotation_lock` (父: neck, 长 0) — TC:headD_rot_const
        - `shoulder_r` (父: body, 长 0)
          - `arm_upper_r` (父: shoulder_r, 长 116) — IK:elbow_r_ik_const
            - `arm_lower_r` (父: arm_upper_r, 长 131) — IK:arm_r_cheek_const
              - `hand_r` (父: arm_lower_r, 长 39) — TC:hand_r_rot_const
                - `hand_r2` (父: hand_r, 长 26)
          - `epaulette r` (父: shoulder_r, 长 23)
        - `shoulder_l` (父: body, 长 0)
          - `arm_upper_l` (父: shoulder_l, 长 138) — IK:arm_l_rest_ik_const
            - `arm_lower_l` (父: arm_upper_l, 长 134) — IK:arm_l_rest_ik_const
              - `hand` (父: arm_lower_l, 长 37)
                - `thumb_l` (父: hand, 长 15)
                  - `thumb_l2` (父: thumb_l, 长 19)
                - `pinky_l1` (父: hand, 长 26)
                  - `pinky_l2` (父: pinky_l1, 长 21)
                    - `pinky_l3` (父: pinky_l2, 长 16)
                - `pinky_l4` (父: hand, 长 30)
                  - `pinky_l5` (父: pinky_l4, 长 21)
                    - `pinky_l6` (父: pinky_l5, 长 16)
                - `index_l` (父: hand, 长 23)
                  - `index_l2` (父: index_l, 长 14)
                    - `index_l3` (父: index_l2, 长 22)
              - `cuff_l` (父: arm_lower_l, 长 15)
              - `arm_particle_attach` (父: arm_lower_l, 长 0) — inherit=noRotationOrReflection
            - `shouldr folder l` (父: arm_upper_l, 长 0)
          - `epaulette l` (父: shoulder_l, 长 42)
        - `arm_lower_r2` (父: body, 长 0) — IK目标:arm_r_cheek_const, TC目标:hand_r_rot_const
        - `breathe` (父: body, 长 0)
        - `chest_particle_attach` (父: body, 长 0) — inherit=noRotationOrReflection
      - `leg_upper_l` (父: regent_cog, 长 151)
        - `leg_lower_l` (父: leg_upper_l, 长 154)
          - `foot_l` (父: leg_lower_l, 长 34)
          - `leg_particle_attach_l` (父: leg_lower_l, 长 0) — inherit=noRotationOrReflection
      - `leg_r_upper` (父: regent_cog, 长 143)
        - `leg_r_lower` (父: leg_r_upper, 长 178)
          - `regent_cog4` (父: leg_r_lower, 长 31)
          - `leg_particle_attach` (父: leg_r_lower, 长 0) — inherit=noRotationOrReflection
      - `death_still` (父: regent_cog, 长 100)
      - `belt` (父: regent_cog, 长 19)
    - `inner_light` (父: butt_attach, 长 230)
    - `inner_light2` (父: butt_attach, 长 230)
    - `inner_light3` (父: butt_attach, 长 230)
    - `inner_light4` (父: butt_attach, 长 230)
    - `inner_light5` (父: butt_attach, 长 230)
  - `elbow_r_ik` (父: root, 长 0) — IK目标:elbow_r_ik_const, TC:throne_elbow_r_trans_const
  - `weapons_base` (父: root, 长 0)
    - `weapol_large` (父: weapons_base, 长 389)
    - `weapon_med` (父: weapons_base, 长 240)
    - `weapon_small` (父: weapons_base, 长 129)
    - `weapon_med2` (父: weapons_base, 长 240)
    - `weapon_small2` (父: weapons_base, 长 129)
    - `weapol_large2` (父: weapons_base, 长 389)
    - `weapon_med3` (父: weapons_base, 长 240)
    - `weapol_large3` (父: weapons_base, 长 389)
  - `minion sweat` (父: root, 长 10)
  - `minion sweat2` (父: root, 长 10)
  - `shadow` (父: root, 长 0)
  - `death_pop` (父: root, 长 0)
  - `death_pop2` (父: root, 长 0)
  - `sparkle` (父: root, 长 0)
  - `sparkle2` (父: root, 长 0)
  - `sparkle3` (父: root, 长 0)
  - `emote` (父: root, 长 77) — TC:head_bubble_const
  - `fist_swish1` (父: root, 长 60)
  - `fist_swish2` (父: root, 长 60)


### 1.2 约束设置

| IK 约束 | order | 骨骼 | 目标 | bendPositive | compress | stretch | uniform | mix | softness |
|---|---|---|---|---|---|---|---|---|---|
| minion_l_arm_ik1_const | 3 | minion_l_arm_1, minion_l_arm_2 | minion_l_arm_ik1_const | True | False | False | False | 1 | 0 |
| minion_r_arm_ik1_const | 4 | minion_r_arm_3, minion_r_arm_4 | minion_r_ik1_const | False | False | False | False | 1 | 0 |
| minion_r_arm_ik2_const | 5 | minion_r_arm_1, minion_r_arm_2 | minion_r_arm_ik2_const | True | False | False | False | 1 | 0 |
| minion_r_leg_ik2_const | 6 | minion_r_cog8, minion_r_cog9 | minion_r_leg_ik2_const | False | False | False | False | 1 | 0 |
| minion_r_leg_ik1_const | 7 | minion_r_cog6, minion_r_cog7 | minion_r_leg_ik1_const | False | False | False | False | 1 | 0 |
| minion_l_leg_ik2_const | 8 | minion_l_leg_1, minion_l_leg_3 | minion_l_leg_ik2_const | True | False | False | False | 1 | 0 |
| minion_l_leg_ik1_const | 9 | minion_l_leg_2, minion_l_leg_4 | minion_l_leg_ik1_const | True | False | False | False | 1 | 0 |
| elbow_r_ik_const | 12 | arm_upper_r | elbow_r_ik | True | True | True | False | 1 | 0 |
| arm_r_cheek_const | 13 | arm_lower_r | arm_lower_r2 | True | True | True | False | 1 | 0 |
| arm_l_rest_ik_const | 15 | arm_upper_l, arm_lower_l | arm_l_rest_ik_targ | False | False | False | False | 0 | 0 |

| Transform 约束 | order | 骨骼 | 目标 | local | relative | 偏移 rot / x / y / sx / sy / shY | mix rot / x / y / sx / sy / shY |
|---|---|---|---|---|---|---|---|
| throne_twist_counter_const | 0 | throne_counter_twist | throne_twist | False | False | 0 / 201.596 / 12.2152 / 0 / 0 / 0 | 0 / -1 / -1 / 0 / 0 / 0 |
| minion_l_body_const | 1 | minion_l_cog | throne_twist | False | False | 120.131 / -132.667 / -185.353 / 0 / 0 / 0 | 0 / 0.5192 / 0.4904 / 0 / 0 / 0 |
| minion_r_body_const | 2 | minion_r_cog | throne_twist | False | False | 89.9052 / 214.29 / -197.649 / -5.96046e-08 / -5.96046e-08 / 0 | 0 / 0.4951 / 0.5098 / 0 / 0 / 0 |
| throne_elbow_r_trans_const | 10 | elbow_r_ik | throne_counter_twist | False | False | 4.78452 / 8.11946 / 294.416 / 0 / 0 / 0 | 0 / 0.8 / 0.8 / 0 / 0 / 0 |
| regent_transform_const | 11 | butt_attach | throne_bone | False | False | -90 / 322.25 / -9.55272 / 0 / 0 / 0 | 1 / 1 / 1 / 0 / 0 / 0 |
| headD_rot_const | 14 | head_rotation_lock | root | False | False | 0 / 0 / 0 / 0 / 0 / 0 | 1 / 0 / 0 / 0 / 0 / 0 |
| hand_r_rot_const | 16 | hand_r | arm_lower_r2 | False | False | 106.707 / 0.0222473 / -1.17383 / -0.00842994 / -0.000721932 / -0.299988 | 1 / 0 / 0 / 1 / 1 / 1 |
| minion_r_arm_tip_rot_const | 17 | minion_r_arm_tip | minion_r_arm_ik2_const | False | False | 130.662 / 0.217224 / -0.60553 / -1.19209e-07 / -1.19209e-07 / 0 | 1 / 0 / 0 / 1 / 1 / 1 |
| head_bubble_const | 18 | emote | head | False | True | 0 / 0 / 0 / 0 / 0 / 0 | 0 / 0 / 0 / 0 / 0 / 0 |


要点：
- 共 **10 个 IK**：7 个用于仆从四肢（2 骨 IK，全部 mix=1，无 compress/stretch），3 个用于 Regent 双臂：
  - `elbow_r_ik_const`（单骨 IK，arm_upper_r → 目标 `elbow_r_ik`，compress+stretch 开）与 `arm_r_cheek_const`（单骨 IK，arm_lower_r → `arm_lower_r2`，compress+stretch 开）组成“右臂撑脸颊”的两段式单骨 IK 链；
  - `arm_l_rest_ik_const`（2 骨 IK 左臂 → `arm_l_rest_ik_targ`），setup **mix=0**，只在 `relaxed_loop` 里被 key 到 1（左手搁在王座扶手上）。
- **9 个 Transform 约束**：用于把 Regent 挂到王座（`regent_transform_const`）、仆从跟随王座扭动（mix x/y≈0.5 制造“半跟随”的拖拽感）、`throne_twist_counter_const`（mixX/Y = **-1**，反向抵消扭动）、`headD_rot_const`（head_rotation_lock 仅继承 root 旋转=世界朝向锁定）、`hand_r_rot_const` / `minion_r_arm_tip_rot_const`（把手/尖端旋转锁定到 IK 目标骨）、`head_bubble_const`（emote 气泡跟随头部，setup 全部 mix=0，在 attack/cast/hurt/idle 系列尾部被 key 成 rot/x/y=1）。
- **Path 约束 0 个，Physics 约束 0 个。**

## 2. 插槽、绘制顺序与附件

- 插槽 **81** 个；setup 绘制顺序 = 插槽索引顺序（0 最底，80 最顶）。混合模式：normal 51，additive 30（`* add` 高光层、武器 add 层、发光/特效层）。
- 81 个插槽中 **16 个在本骨骼中没有任何附件**（`big/medium/small weapon*` 共 16 个武器插槽，武器图来自独立的 `regent_weapon.skel`，运行时挂入）；另有 33 个插槽有附件但 setup 附件为空、只在动画中打开（高光层、特效层、sweat/sparkle/inner_light 等）。
- **附件统计**：78 个 = mesh 40（其中**加权 38**、非加权 2）+ linkedmesh 3 + region 35。网格顶点合计 1,636（不含 linked）。
- **sprite swap（同一插槽在多个附件之间切换）共 8 个插槽**：

| 插槽 | 可切换附件 | 在哪些动画中切换 |
|---|---|---|
| eye / eye add | normal eye, half eye, mad eye, star eye（4 张 region） | attack: star→normal(0.867s)；attack_sovereign: mad(0.1)→normal(0.9)；cast: half(0.1)→normal(1.1)；hurt: mad(0.133)→normal(0.6)；inactive/low_health: half/mad 反复切换；die: 2.5s 隐藏 |
| bottom hand | bottom hand (mesh 14v), bottm fist (region), bottom hand freaked (region) | attack_sovereign: 0.133 握拳→0.933 复原；defend: 0.667 换 freaked；die: freaked |
| hand | hand (mesh 59v), top fist (region) | attack_sovereign: 0.2 握拳→1.0 复原（同时隐藏 `hand copy`/`top hand 1`） |
| bottom arm 1 | bottom arm 1, bottom arm 1 flipped (linkedmesh) | attack_sovereign 0.233→0.933；die |
| top arm 1 | top arm 1, top arm 1 flipped (linkedmesh) | attack_sovereign 0.2→0.933；relaxed_loop；die |
| top arm 2 | top arm 2, top arm 2 flipped (linkedmesh) | 同上 |
| emote | anger, bubble | attack_sovereign / low_health: anger；inactive: bubble |

  “flipped” 均为 linkedmesh（复用父网格的顶点和权重，只换贴图），用来在不改骨骼的情况下切换手臂朝向的画法。

### 2.1 插槽 / 绘制顺序全表（setup 顺序，从下到上）

| # | 插槽 | 骨骼 | setup 附件 | 混合 | 附件 (类型/顶点) |
|---|---|---|---|---|---|
| 0 | shadow | shadow | shadow | normal | shadow (region) |
| 1 | left guy | minion_l_cog | left guy | normal | left guy (mesh 95v W) |
| 2 | left guy highlights | minion_l_cog | — | normal | left guy highlights (mesh 56v W) |
| 3 | left guy highlights add | minion_l_cog | — | additive | left guy highlights (mesh 56v W) |
| 4 | throne_bottom | throne_bone | throne_bottom | normal | throne_bottom (mesh 42v W) |
| 5 | throne bottom highlights | throne_bone | — | normal | throne bottom highlights (mesh 55v W) |
| 6 | throne bottom highlights add | throne_bone | — | additive | throne bottom highlights (mesh 55v W) |
| 7 | throne shadows | throne_bone | throne shadows | normal | throne shadows (mesh 59v W) |
| 8 | leg bottom | leg_r_upper | leg bottom | normal | leg bottom (mesh 15v) |
| 9 | leg bottom bottom | leg_r_lower | leg bottom bottom | normal | leg bottom bottom (mesh 31v W) |
| 10 | leg top | leg_upper_l | leg top | normal | leg top (mesh 22v W) |
| 11 | bottom arm 2 | arm_upper_r | bottom arm 2 | normal | bottom arm 2 (mesh 22v W) |
| 12 | bod | body | bod | normal | bod (mesh 48v W) |
| 13 | belt | belt | belt | normal | belt (region) |
| 14 | r shoulder | epaulette r | r shoulder | normal | r shoulder (region) |
| 15 | bottom arm 1 | arm_lower_r | bottom arm 1 | normal | bottom arm 1 (mesh 34v W); bottom arm 1 flipped (linkedmesh 34v W) |
| 16 | bottom hand | hand_r | bottom hand | normal | bottm fist (region); bottom hand (mesh 14v); bottom hand freaked (region) |
| 17 | bottom fingers | hand_r2 | bottom fingers | normal | bottom fingers (mesh 30v W) |
| 18 | head | head | head | normal | head (mesh 48v W) |
| 19 | neck | neck | neck | normal | neck (mesh 37v W) |
| 20 | eye | eye | normal eye | normal | half eye (region); mad eye (region); normal eye (region); star eye (region) |
| 21 | eye add | eye | normal eye | additive | half eye (region); mad eye (region); normal eye (region); star eye (region) |
| 22 | throne top | throne_bone | throne top | normal | throne top (mesh 44v W) |
| 23 | throne top highlights | throne_bone | — | normal | throne top highlights (mesh 44v W) |
| 24 | throne top highlights add | throne_bone | — | additive | throne top highlights (mesh 44v W) |
| 25 | leg bottom bottom layering bit | leg_r_lower | leg bottom bottom | normal | leg bottom bottom (mesh 23v W) |
| 26 | leg top bottom | leg_lower_l | leg top bottom | normal | leg top bottom (mesh 23v W) |
| 27 | left guy top arm | minion_l_arm_1 | left guy top arm | normal | left guy top arm (mesh 29v W) |
| 28 | left guy top arm highlights | minion_l_cog | — | normal | left guy top arm highlights (mesh 27v W) |
| 29 | left guy top arm highlights add | minion_l_cog | — | additive | left guy top arm highlights2 (mesh 27v W) |
| 30 | right guy bottom arm | minion_r_arm_1 | right guy bottom arm | normal | right guy bottom arm (mesh 31v W) |
| 31 | right guy bottom arm highlights | minion_r_cog | — | normal | right guy bottom arm highlights (mesh 21v W) |
| 32 | right guy bottom arm highlights add | minion_r_cog | — | additive | right guy bottom arm highlights (mesh 21v W) |
| 33 | right guy | minion_r_cog | right guy | normal | right guy (mesh 103v W) |
| 34 | right guy highlights | minion_r_cog | — | normal | right guy highlights (mesh 104v W) |
| 35 | right guy highlights add | minion_r_cog | — | additive | right guy highlights (mesh 104v W) |
| 36 | right guy top arm | minion_r_arm_3 | right guy top arm | normal | right guy top arm (mesh 25v W) |
| 37 | right guy top arm highlights | minion_r_cog | — | normal | right guy top arm highlights (mesh 18v W) |
| 38 | right guy top arm highlights add | minion_r_cog | — | additive | right guy top arm highlights (mesh 18v W) |
| 39 | top arm 2 | arm_upper_l | top arm 2 | normal | top arm 2 (mesh 24v W); top arm 2 flipped (linkedmesh 24v W) |
| 40 | chain | body | chain | normal | chain (mesh 24v W) |
| 41 | l shoulder | epaulette l | l shoulder | normal | l shoulder (region) |
| 42 | hand | hand | hand | normal | hand (mesh 59v W); top fist (region) |
| 43 | hand copy | hand | hand copy | normal | hand copy (mesh 34v W) |
| 44 | top hand 1 | hand | top hand 1 | normal | top hand 1 (mesh 42v W) |
| 45 | top arm 1 | arm_lower_l | top arm 1 | normal | top arm 1 (mesh 28v W); top arm 1 flipped (linkedmesh 28v W) |
| 46 | big weapon | weapol_large | — | normal | —（运行时挂载/空） |
| 47 | big weapon3 | weapol_large3 | — | normal | —（运行时挂载/空） |
| 48 | big weapon2 | weapol_large2 | — | normal | —（运行时挂载/空） |
| 49 | big weapon add | weapol_large | — | additive | —（运行时挂载/空） |
| 50 | big weapon add3 | weapol_large3 | — | additive | —（运行时挂载/空） |
| 51 | big weapon add2 | weapol_large2 | — | additive | —（运行时挂载/空） |
| 52 | medium weapon | weapon_med | — | normal | —（运行时挂载/空） |
| 53 | medium weapon5 | weapon_med3 | — | normal | —（运行时挂载/空） |
| 54 | medium weapon3 | weapon_med2 | — | normal | —（运行时挂载/空） |
| 55 | medium weapon add | weapon_med | — | additive | —（运行时挂载/空） |
| 56 | medium weapon add2 | weapon_med3 | — | additive | —（运行时挂载/空） |
| 57 | medium weapon3 add | weapon_med2 | — | additive | —（运行时挂载/空） |
| 58 | small weapon | weapon_small | — | normal | —（运行时挂载/空） |
| 59 | small weapon3 | weapon_small2 | — | normal | —（运行时挂载/空） |
| 60 | small weapon add | weapon_small | — | additive | —（运行时挂载/空） |
| 61 | small weapon add2 | weapon_small2 | — | additive | —（运行时挂载/空） |
| 62 | death flash | death_still | — | additive | death flash (region) |
| 63 | minion sweat | minion sweat | — | normal | minion sweat (region) |
| 64 | minion sweat2 | minion sweat2 | — | normal | minion sweat (region) |
| 65 | regent death pop | death_pop | — | additive | regent death pop (region) |
| 66 | regent death pop2 | death_pop2 | — | additive | regent death pop (region) |
| 67 | sparkle 3 | sparkle | — | additive | sparkle 3 (region) |
| 68 | sparkle 4 | sparkle2 | — | additive | sparkle 3 (region) |
| 69 | sparkle 5 | sparkle3 | — | additive | sparkle 3 (region) |
| 70 | inner_light | inner_light | — | additive | inner_light (region) |
| 71 | inner_light7 | inner_light5 | — | additive | inner_light (region) |
| 72 | inner_light6 | inner_light | — | additive | inner_light (region) |
| 73 | inner_light8 | inner_light5 | — | additive | inner_light (region) |
| 74 | inner_light2 | inner_light2 | — | additive | inner_light (region) |
| 75 | inner_light5 | inner_light2 | — | additive | inner_light (region) |
| 76 | inner_light3 | inner_light3 | — | additive | inner_light (region) |
| 77 | inner_light4 | inner_light4 | — | additive | inner_light (region) |
| 78 | emote | emote | — | normal | anger (region); bubble (region) |
| 79 | fist swish | fist_swish1 | — | normal | fist swish (region) |
| 80 | fist swish2 | fist_swish2 | — | normal | fist swish (region) |


### 2.2 网格明细

| 插槽 | 网格 | 顶点 | hull | 三角形 | 加权 | 影响骨骼数 | 每顶点最多骨骼 | 影响骨骼 |
|---|---|---|---|---|---|---|---|---|
| bod | bod | 48 | 18 | 76 | 是 | 2 | 2 | body, breathe |
| bottom arm 1 | bottom arm 1 | 34 | 29 | 37 | 是 | 1 | 1 | arm_lower_r |
| bottom arm 1 | bottom arm 1 flipped (linked→bottom arm 1) | 34 | 29 | (继承) | 是 | 1 | 1 | arm_lower_r |
| bottom arm 2 | bottom arm 2 | 22 | 20 | 22 | 是 | 1 | 1 | arm_upper_r |
| bottom fingers | bottom fingers | 30 | 23 | 35 | 是 | 2 | 2 | hand_r, hand_r2 |
| bottom hand | bottom hand | 14 | 14 | 12 | 否 | — | — | — |
| chain | chain | 24 | 24 | 22 | 是 | 2 | 2 | body, breathe |
| hand | hand | 59 | 54 | 62 | 是 | 6 | 4 | hand, thumb_l, thumb_l2, index_l, index_l2, index_l3 |
| hand copy | hand copy | 34 | 34 | 32 | 是 | 4 | 3 | hand, pinky_l4, pinky_l5, pinky_l6 |
| head | head | 48 | 31 | 63 | 是 | 2 | 2 | head, face_twist |
| left guy | left guy | 95 | 84 | 104 | 是 | 5 | 3 | minion_l_cog, minion_l_leg_1, minion_l_leg_3, minion_l_leg_2, minion_l_leg_4 |
| left guy highlights | left guy highlights | 56 | 49 | 61 | 是 | 5 | 3 | minion_l_cog, minion_l_leg_1, minion_l_leg_3, minion_l_leg_2, minion_l_leg_4 |
| left guy highlights add | left guy highlights | 56 | 49 | 61 | 是 | 5 | 3 | minion_l_cog, minion_l_leg_1, minion_l_leg_3, minion_l_leg_2, minion_l_leg_4 |
| left guy top arm | left guy top arm | 29 | 28 | 28 | 是 | 2 | 2 | minion_l_arm_1, minion_l_arm_2 |
| left guy top arm highlights | left guy top arm highlights | 27 | 23 | 29 | 是 | 2 | 2 | minion_l_arm_1, minion_l_arm_2 |
| left guy top arm highlights add | left guy top arm highlights2 | 27 | 23 | 29 | 是 | 2 | 2 | minion_l_arm_1, minion_l_arm_2 |
| leg bottom | leg bottom | 15 | 15 | 13 | 否 | — | — | — |
| leg bottom bottom | leg bottom bottom | 31 | 31 | 29 | 是 | 2 | 2 | leg_r_lower, regent_cog4 |
| leg bottom bottom layering bit | leg bottom bottom | 23 | 23 | 21 | 是 | 2 | 2 | leg_r_lower, regent_cog4 |
| leg top | leg top | 22 | 22 | 20 | 是 | 2 | 2 | regent_cog, leg_upper_l |
| leg top bottom | leg top bottom | 23 | 23 | 21 | 是 | 2 | 2 | leg_lower_l, foot_l |
| neck | neck | 37 | 32 | 40 | 是 | 2 | 2 | body, neck |
| right guy | right guy | 103 | 69 | 135 | 是 | 6 | 3 | minion_r_cog, minion_shoulder_r, minion_r_cog6, minion_r_cog7, minion_r_cog8, minion_r_cog9 |
| right guy bottom arm | right guy bottom arm | 31 | 28 | 32 | 是 | 3 | 2 | minion_r_arm_1, minion_r_arm_2, minion_r_arm_tip |
| right guy bottom arm highlights | right guy bottom arm highlights | 21 | 21 | 19 | 是 | 2 | 2 | minion_r_arm_1, minion_r_arm_2 |
| right guy bottom arm highlights add | right guy bottom arm highlights | 21 | 21 | 19 | 是 | 2 | 2 | minion_r_arm_1, minion_r_arm_2 |
| right guy highlights | right guy highlights | 104 | 62 | 144 | 是 | 6 | 3 | minion_r_cog, minion_shoulder_r, minion_r_cog6, minion_r_cog7, minion_r_cog8, minion_r_cog9 |
| right guy highlights add | right guy highlights | 104 | 62 | 144 | 是 | 6 | 3 | minion_r_cog, minion_shoulder_r, minion_r_cog6, minion_r_cog7, minion_r_cog8, minion_r_cog9 |
| right guy top arm | right guy top arm | 25 | 23 | 25 | 是 | 2 | 2 | minion_r_arm_3, minion_r_arm_4 |
| right guy top arm highlights | right guy top arm highlights | 18 | 17 | 17 | 是 | 2 | 2 | minion_r_arm_3, minion_r_arm_4 |
| right guy top arm highlights add | right guy top arm highlights | 18 | 17 | 17 | 是 | 2 | 2 | minion_r_arm_3, minion_r_arm_4 |
| throne bottom highlights | throne bottom highlights | 55 | 44 | 64 | 是 | 2 | 2 | throne_twist, throne_counter_twist |
| throne bottom highlights add | throne bottom highlights | 55 | 44 | 64 | 是 | 2 | 2 | throne_twist, throne_counter_twist |
| throne shadows | throne shadows | 59 | 59 | 57 | 是 | 2 | 2 | throne_twist, throne_counter_twist |
| throne top | throne top | 44 | 27 | 59 | 是 | 2 | 2 | throne_twist, throne_counter_twist |
| throne top highlights | throne top highlights | 44 | 36 | 50 | 是 | 2 | 2 | throne_twist, throne_counter_twist |
| throne top highlights add | throne top highlights | 44 | 36 | 50 | 是 | 2 | 2 | throne_twist, throne_counter_twist |
| throne_bottom | throne_bottom | 42 | 31 | 51 | 是 | 2 | 2 | throne_twist, throne_counter_twist |
| top arm 1 | top arm 1 | 28 | 23 | 31 | 是 | 2 | 2 | arm_lower_l, cuff_l |
| top arm 1 | top arm 1 flipped (linked→top arm 1) | 28 | 23 | (继承) | 是 | 2 | 2 | arm_lower_l, cuff_l |
| top arm 2 | top arm 2 | 24 | 18 | 28 | 是 | 2 | 2 | arm_upper_l, shouldr folder l |
| top arm 2 | top arm 2 flipped (linked→top arm 2) | 24 | 18 | (继承) | 是 | 2 | 2 | arm_upper_l, shouldr folder l |
| top hand 1 | top hand 1 | 42 | 42 | 40 | 是 | 4 | 3 | hand, pinky_l1, pinky_l2, pinky_l3 |


- 加权网格每顶点最多受 **4** 根骨骼影响（`hand`），其余多为 2；仆从身体网格（`right guy` 103v、`left guy` 95v）受 5–6 根骨骼影响、每顶点 ≤3。
- Regent 本体网格都很“省”：`bod` 48v（body+breathe 两骨，breathe 骨用于胸口呼吸鼓胀）、`head` 48v（head+face_twist，face_twist 平移实现“转脸”的伪 3D）、`neck` 37v（body+neck）、四肢 22–34v 基本是 1–2 骨蒙皮。
- 非加权网格只有 `bottom hand`（14v）和 `leg bottom`（15v）。

## 3. 动画总览

10 个动画：`attack`, `attack_sovereign`, `cast`, `defend`, `die`, `hurt`, `idle_loop`, `inactive loop`, `low_health_loop`, `relaxed_loop`。

| 动画 | 时长 s | 帧数@30 | 有时间轴的骨骼 | 实际变化的骨骼 | 时间轴总数 | 关键帧总数 | 关键帧/秒 | 曲线段 bezier / stepped / linear | 帧间距 中位/平均 (帧) |
|---|---|---|---|---|---|---|---|---|---|
| attack | 1.333 | 40 | 68 | 59 | 146 | 621 | 465.8 | 297 / 116 / 54 | 4 / 6.3 |
| attack_sovereign | 1.167 | 35 | 64 | 47 | 140 | 712 | 610.3 | 440 / 85 / 36 | 4 / 4.8 |
| cast | 1.800 | 54 | 62 | 53 | 137 | 552 | 306.7 | 338 / 66 / 9 | 4 / 8.3 |
| defend | 0.900 | 27 | 56 | 22 | 86 | 142 | 157.8 | 0 / 56 / 0 | 7 / 13.1 |
| die | 4.167 | 125 | 88 | 84 | 240 | 1174 | 281.8 | 684 / 136 / 93 | 3 / 8.6 |
| hurt | 0.933 | 28 | 66 | 65 | 118 | 597 | 639.6 | 379 / 89 / 9 | 3 / 5.4 |
| idle_loop | 18.667 | 560 | 65 | 53 | 109 | 2750 | 147.3 | 2468 / 144 / 27 | 3 / 12.2 |
| inactive loop | 18.667 | 560 | 67 | 59 | 127 | 2771 | 148.4 | 2330 / 275 / 22 | 3 / 16.8 |
| low_health_loop | 18.667 | 560 | 68 | 49 | 128 | 2767 | 148.2 | 2153 / 449 / 24 | 2 / 12.9 |
| relaxed_loop | 13.333 | 400 | 59 | 27 | 86 | 1493 | 112.0 | 1356 / 32 / 18 | 2 / 9.2 |


说明：“有时间轴的骨骼”= 该动画中出现过骨骼时间轴的骨骼数；“实际变化的骨骼”= 至少一条时间轴的数值不是常量。曲线段 = 关键帧之间的段（attachment/drawOrder/event/inherit 不计）。所有动画的关键帧都落在 30fps 网格上，例外：attack_sovereign 9 段、die 59 段、low_health_loop 245 段间距非整数帧。

观察：
- **贝塞尔为主**：除 defend 外，bezier 段占 63%–96%。stepped 段大多数是“值相同的 hold”（例如 attack 的 f4→f20、cast 的 f4/5→f24/25），也就是用 stepped 来做**定格（hold）**。
- **defend 全部是 stepped**（56 段，0 bezier）：纯 pose-to-pose 的“弹出”式防御姿势，关键帧间距只有 7 帧和 20 帧两种。
- 三个 18.667s 循环（idle/inactive/low_health）关键帧密度都≈148 个/秒，但其中大量来自两种“伪程序化抖动”时间轴：
  - `throne_adjust.translateY`：每 3 帧（0.1s）在 0 ↔ -1.94/-2.45 之间往返（idle 中 189 个键）——王座悬浮的嗡嗡震动；
  - `minion_l_leg_1/2`、`minion_r_cog6/9` 的 `scaleX`：每 2 帧在 ≈0.98 ↔ 1.036 之间往返（各 281 个键）——仆从腿部的高频颤抖（承重发抖）。
  这 5 条时间轴共 1,313 个键，约占 idle 键数的 48%。这些抖动在 attack/cast/hurt 中同样存在。
- 关键帧间距直方图（idle）：2 帧 1,170 次、3 帧 287 次（抖动）；其余“手 K”的键间距集中在 9–11 帧与 20 帧。

### 3.1 时间轴类型计数（每个动画）

| 动画 | rotate | translate | translatex | translatey | scale | scalex | scaley | shear | inherit | attachment | rgba | rgb | alpha | ik | transform | deform | drawOrder | events |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| attack | 49 | 28 | 2 | 3 | 18 | 4 | 3 | 1 |  | 19 | 5 |  | 10 |  | 2 |  | 1 | 1 |
| attack_sovereign | 48 | 28 | 2 | 2 | 20 | 3 | 3 | 4 | 2 | 17 | 8 | 1 |  |  | 2 |  |  |  |
| cast | 47 | 28 | 2 | 3 | 15 | 4 | 3 | 3 |  | 15 | 15 |  |  |  | 2 |  |  |  |
| defend | 40 | 19 | 2 | 2 | 9 | 3 | 3 | 3 | 1 | 2 |  |  |  |  | 2 |  |  |  |
| die | 62 | 59 | 3 | 4 | 35 |  |  | 10 |  | 39 | 13 | 1 | 1 | 5 | 5 | 1 | 1 | 1 |
| hurt | 51 | 25 | 3 | 4 | 18 | 3 | 3 | 4 |  | 2 | 1 |  |  | 1 | 2 |  | 1 |  |
| idle_loop | 46 | 26 | 4 | 5 | 14 | 4 | 3 | 2 |  | 2 |  |  | 1 |  | 1 | 1 |  |  |
| inactive loop | 49 | 29 | 4 | 5 | 18 | 4 | 3 | 2 | 2 | 5 | 1 |  | 1 | 1 | 2 | 1 |  |  |
| low_health_loop | 51 | 28 | 4 | 5 | 15 | 6 | 4 | 2 | 2 | 5 | 1 |  |  | 1 | 2 | 1 | 1 |  |
| relaxed_loop | 26 | 29 | 2 | 3 | 13 | 4 | 3 | 1 |  | 3 |  |  |  | 1 |  |  | 1 |  |


### 3.2 插槽颜色 / 附件时间轴、deform、draw order、事件

**事件定义（9 个，全部 int=0, float=0, string="", 无音频）**：attack1, attack2, attack_end, attack_test, death_particles_end, death_particles_start, death_particles_start2, explode_dead, explode_end。

| 动画 | 事件（时间 s / 帧） |
|---|---|
| attack | attack1 0.133 (f4)；attack_test 0.267 (f8)；attack_end 0.433 (f13) |
| die | death_particles_start 0.067 (f2)；death_particles_start2 0.333 (f10)；death_particles_end 2.467 (f74)；explode_dead 2.500 (f75)；explode_end 2.967 (f89) |
| 其他 8 个 | 无事件（注意 cast / attack_sovereign / hurt 都没有事件） |

**插槽颜色/透明度时间轴**：
- attack：仆从和王座的高光层（`* highlights` / `* highlights add`，共 10 条 alpha + 4 条 rgba）在 0.133s(f4) 打开，0.233–0.3s 达到满亮，0.4–0.567 保持，到 0.8–0.9s 淡出；中间有 0.4→0.467 的二次闪（alpha 1→0→1）。`sparkle 3/4/5` 在 f5 出现，f12–f16 消失。`emote.rgba` 在 1.3s 置红色 fb3333ff。
- attack_sovereign：`eye` 0.067→0.2 由白变红 ff2121，0.733→0.933 恢复；`eye add` 用 rgb 变 ff6e2c；`fist swish/2` 在 f6 显示、f6→f8 由 efb26eff 淡到 alpha 0（2 帧拳风残影）；王座高光 f6→f8 亮起、到 0.933s 淡出。
- cast：14 条 rgba 高光层在 0.133s(f4) 起、0.2s(f6) 满、0.4–0.5s 开始淡出、0.8–0.867s 全灭。
- hurt：只有 `emote.rgba`（0.9s）。
- die：`death flash` rgb 000000→fc2a00→ffaf53→ffffff（1.867→2.467s）+ alpha 0.02→1（2.467–2.5 定格）→0（2.6s）；inner_light×8 依次亮起；2.5s（f75，与 explode_dead 事件同帧）把 Regent 全部身体插槽（22 个）设为空——身体“爆掉”；`regent death pop` 2.4–3.133s。

**Deform 时间轴**：全文件只有 1 个附件被 deform——`throne shadows`（59v 加权网格，共 106 条骨骼权重，加权网格的 deform 长度 = 权重条目数×2 = 212 个 float；关键帧有的写满 212 个偏移，有的只写局部片段，如 idle 5.8s 从偏移 109 起写 19 个）。出现于 die（4 键）、idle_loop / inactive loop（各 10 键，3.9–13.87s）、low_health_loop（11 键）。attack / cast / hurt 没有 deform。

**Draw order 时间轴**：
- attack：f4 把 `big weapon`/`big weapon3`/`big weapon add3`/`medium weapon3`/`medium weapon3 add` 下移 43–51 位（武器移到身体后面）；f27 再调整 `big weapon`/`big weapon add`。
- hurt：f0 把 `bottom arm 1`、`bottom hand`、`bottom fingers` 上移 24 位（右臂移到身体前面——被打时右臂甩到前景）。
- low_health_loop（8.833s 同样上移右臂、15.1s 复原）、relaxed_loop（0s 同样上移右臂）、die（f7、f51、f100 三次重排，含王座顶层下移、仆从层下移）。
- idle_loop / cast / attack_sovereign / defend / inactive 无 draw order 变化。

## 4. 重点动画逐骨分析（idle_loop / attack / cast / hurt）

表格列说明：旋转/位移/切变为相对 setup 的增量范围（min~max），缩放为倍数范围；“=v” 表示该通道有 key 但为常量。“极值帧”= 与 setup（0 或 1）偏离最大的那个关键帧（括号为通道：r 旋转，tx/ty 位移，sx/sy 缩放）。仅列出至少一个通道真正在变化的骨骼。

### 4.1 idle_loop（18.667s = 560 帧，循环）

**结构**：一个 **80 帧（2.667s）的基础呼吸/漂浮周期重复 7 次**（f0–f480，仆从/王座/肩/颈/手指等通道在 f0,80,160,…,560 重复同一组数值），叠加 2 段一次性“表演”：
1. f57–f136 与 f217–f331：左手手指敲击（thumb/pinky/index 共 11 根指骨，index_l 在 -53.9° ↔ -1.0° 之间，pinky_l2 在 -79.6° ↔ +26.5°，各指节按 2–4 帧错开）；
2. **f480–f560（最后 80 帧）**：右手托腮/揉脸的手势——`arm_lower_r2`（IK 目标）旋转到 -87.3°（f488）→ -45.5°（f515）→ -111.3°（f538），`hand_r` -54°→-22°→-76.7°，`body` 旋转 0→8.3°(f488)→9.1°(f515)→0(f554)，`head` 最大 15.8°(f515)，眼睛 f480–f553 被 stepped 地移动并眯眼（scaleY 0.65→0.09→1）。
- 眨眼：`eye.scaleY` 在 f109→f116 闭合到 0.11，hold 到 f125，f134 睁开（整个眨眼 25 帧，其中闭合 9 帧）；f349–f374 再眨一次。
- 抖动层：`throne_adjust.translateY` 0↔-2.45 每 3 帧；仆从腿 scaleX 0.98↔1.04 每 2 帧（整段持续）。
- 王座/仆从漂浮：`throne_bone.translateX` 5.76 ↔ -7.21（周期 80 帧，极值在 f0/f46），`minion_r_cog.translate` 周期 80 帧极值在 f0/f40，`minion_l_cog` 极值在 f4/f44，`shadow.translateX` 4.96↔-7.16 极值 f0/f45——**同一周期内各部件峰值错开 4–6 帧**，形成轻微的重叠动作。
- 呼吸：`breathe` 骨 scaleX 1.00→1.30、scaleY 1.00→1.13（胸口鼓起，极值 f524）；`neck.translateY` 0→1.51（f43），`head.scaleX` 1.00↔0.98（f37）、`shoulder_l/r` 位移 2–3.8 单位（f43）——呼吸峰值都在周期的 f40–f43 附近。
- 挤压：idle 中最明显的 squash 是手势段 `head.scaleY` 0.92（f492）、`face_twist.scaleY` 0.87（f490）、`hand_r2.scaleY` 0.82（f485）。

| 骨骼 | 旋转 (°) | 位移X | 位移Y | 缩放X | 缩放Y | 切变 | 关键帧数 | 极值帧 | 关键帧时刻 (帧@30fps) |
|---|---|---|---|---|---|---|---|---|---|
| throne_adjust |  |  | -2.5~0.0 |  |  |  | 189 | f9(ty) | 189个, 0–557 |
| throne_bone | -0.2~1.7 | -7.2~5.8 | -22.8~-18.5 |  |  |  | 94 | f23(ty) | 78个, 0–560 |
| throne_twist |  | -0.5~0.3 | -0.5~5.2 |  |  |  | 58 | f53(ty) | 50个, 0–560 |
| minion_r_cog | =0.1 | -1.6~7.9 | -6.5~-0.7 |  |  |  | 16 | f0(tx) | 0 40 80 120 160 200 240 280 320 360 400 440 480 520 560 |
| minion_shoulder_r |  | 0.0~7.0 | -12.9~0.0 |  |  |  | 20 | f250(ty) | 20个, 0–560 |
| minion_r_arm_4 | =-5.2 |  |  | =1.00 | 0.96~1.35 |  | 6 | f262(sy) | 0 260 262 265 271 275 |
| minion_r_cog6 | =5.6 |  |  | 0.98~1.04 | =0.98 |  | 283 | f2(sx) | 281个, 0–560 |
| minion_r_cog7 | =-20.9 |  |  | 1.00~1.05 | =1.00 |  | 16 | f0(sx) | 0 40 80 120 160 200 240 280 320 360 400 440 480 520 560 |
| minion_r_cog8 | =37.7 |  |  | 1.02~1.08 | =1.00 |  | 16 | f40(sx) | 0 40 80 120 160 200 240 280 320 360 400 440 480 520 560 |
| minion_r_cog9 | =-22.6 |  |  | 0.98~1.04 | =0.98 |  | 283 | f0(sx) | 281个, 0–560 |
| minion_l_cog | 0.0~3.8 | -6.1~0.0 | 0.0~2.0 |  |  |  | 44 | f44(r) | 22个, 0–560 |
| minion_l_leg_1 | =-11.3 |  |  | 1.02~1.05 | =1.02 |  | 283 | f0(sx) | 281个, 0–560 |
| minion_l_leg_3 | =26.1 |  |  | 1.00~1.14 | =1.00 |  | 23 | f42(sx) | 22个, 0–560 |
| minion_l_leg_2 | =-18.4 |  |  | 1.00~1.04 |  |  | 282 | f2(sx) | 281个, 0–560 |
| minion_l_arm_ik1_const |  | -23.7~0.0 | -11.2~5.2 |  |  |  | 6 | f177(tx) | 124 164 174 177 179 185 |
| minion_r_ik1_const |  | -57.7~174.1 | -35.0~11.2 |  |  |  | 14 | f251(tx) | 0 240 242 245 246 251 257 258 260 262 263 268 271 276 |
| minion_r_arm_ik2_const |  | 0.0~6.8 | -23.4~0.0 |  |  |  | 4 | f412(ty) | 380 412 415 417 |
| butt_attach | =0.1 | -17.1~5.1 | -19.8~-19.0 |  |  |  | 5 | f44(ty) | 0 44 524 560 |
| body | 0.0~9.1 |  |  |  |  |  | 8 | f515(r) | 480 488 510 515 533 538 542 554 |
| neck | -0.6~2.3 | =0.0 | 0.0~1.5 |  |  |  | 43 | f547(r) | 35个, 0–560 |
| head | -0.4~15.8 |  |  | 0.98~1.00 | 0.92~1.00 |  | 52 | f515(r) | 41个, 0–560 |
| face_twist |  | -1.8~3.7 | -15.5~0.0 | =1.00 | 0.87~1.00 |  | 31 | f490(sy) | 27个, 0–555 |
| eye |  | -4.4~0.0 | -5.9~2.3 | =1.00 | 0.09~1.00 |  | 30 | f543(sy) | 21个, 109–553 |
| elbow_r_ik |  | -18.1~0.0 | =0.0 |  |  |  | 21 | f50(tx) | 21个, 0–558 |
| shoulder_r |  | -0.7~2.8 | -0.1~3.5 |  |  |  | 23 | f10(ty) | 23个, 0–558 |
| arm_upper_r | -7.2~2.4 |  |  |  |  |  | 6 | f485(r) | 0 480 485 548 558 560 |
| shoulder_l |  | -0.0~2.3 | 0.0~3.8 |  |  |  | 29 | f43(ty) | 29个, 0–558 |
| arm_upper_l | -1.8~0.0 |  |  |  |  |  | 19 | f515(r) | 19个, 0–559 |
| arm_lower_l | -5.1~3.4 |  |  |  |  |  | 25 | f485(r) | 25个, 0–560 |
| hand | -20.6~0.0 |  |  |  |  |  | 36 | f15(r) | 36个, 0–558 |
| leg_lower_l | 0.0~1.9 |  |  |  |  |  | 57 | f0(r) | 57个, 0–560 |
| foot_l | 0.1~3.8 |  |  |  |  |  | 85 | f16(r) | 85个, 0–558 |
| leg_r_upper |  | 0.1~4.5 | =0.0 |  |  |  | 29 | f40(tx) | 29个, 0–560 |
| leg_r_lower | -66.1~-65.5 |  |  |  |  |  | 71 | f12(r) | 71个, 0–560 |
| arm_lower_r | -25.6~0.0 | 0.0~0.6 | -0.3~0.0 |  |  |  | 10 | f485(r) | 0 480 485 548 558 560 |
| hand_r | -76.7~-0.6 | 0.0~3.7 | 0.0~3.6 | 0.93~1.03 | 0.85~1.00 | =0.0 -9.7~8.4 | 38 | f542(r) | 0 480 488 510 515 533 538 542 553 560 |
| arm_lower_r2 | -111.3~0.0 | 0.0~0.8 | -71.2~1.6 |  |  |  | 34 | f538(r) | 26个, 0–553 |
| breathe |  | -0.1~4.1 | 0.0~1.7 | 1.00~1.30 | 1.00~1.13 |  | 52 | f524(sx) | 26个, 0–558 |
| thumb_l | 0.0~25.2 | -1.8~0.0 | 0.0~2.8 |  |  |  | 49 | f77(r) | 34个, 0–558 |
| thumb_l2 | -10.1~0.0 |  |  | 0.83~1.00 | =1.00 |  | 46 | f77(sx) | 26个, 0–558 |
| pinky_l1 | -28.6~8.7 |  |  |  |  |  | 29 | f0(r) | 29个, 0–558 |
| pinky_l2 | -79.6~26.5 |  |  |  |  |  | 29 | f0(r) | 29个, 0–558 |
| pinky_l3 | -34.1~28.9 |  |  |  |  |  | 29 | f91(r) | 29个, 0–558 |
| pinky_l4 | -21.8~14.0 |  |  |  |  |  | 27 | f0(r) | 27个, 0–558 |
| pinky_l5 | -55.4~45.1 |  |  |  |  |  | 27 | f0(r) | 27个, 0–558 |
| pinky_l6 | -40.7~29.7 |  |  |  |  |  | 28 | f93(r) | 28个, 0–558 |
| index_l | -53.9~0.0 |  |  |  |  |  | 22 | f0(r) | 22个, 0–558 |
| index_l2 | -59.1~12.8 |  |  |  |  |  | 22 | f0(r) | 22个, 0–558 |
| index_l3 | -20.4~28.8 |  |  |  |  |  | 24 | f80(r) | 24个, 0–558 |
| hand_r2 | -5.2~15.5 | -2.1~0.0 | -4.1~0.0 | =1.00 | 0.82~1.00 |  | 16 | f485(sy) | 480 485 510 515 533 538 542 553 |
| minion sweat | 0.0~34.7 | -982.8~-748.8 | -173.0~-108.9 | 0.51~1.26 | 0.51~1.26 |  | 8 | f267(tx) | 261 267 |
| minion sweat2 | 0.0~34.7 | -916.7~-741.3 | -206.4~-102.9 | 0.51~1.00 | 0.51~1.00 |  | 8 | f265(tx) | 261 265 |
| shadow |  | -7.2~5.0 | =0.0 |  |  |  | 15 | f45(tx) | 0 45 80 125 160 205 240 285 320 365 400 445 480 525 560 |


### 4.2 attack（1.333s = 40 帧）

**节奏（帧 @30fps）**：

| 阶段 | 帧 | 内容 |
|---|---|---|
| 预备（anticipation） | f0 | 第 0 帧本身就是蓄力姿势（与 idle setup 差异很大）：`butt_attach` 后仰 tx=+43.4 / ty=-30.6，`neck` +6.3°，`head` +4.4° 且 **scaleY 0.91（压扁）**，左臂 `arm_upper_l` -41.1°、`hand` -71.6°（手腕后扣）、`hand_r` -80°，`cuff_l` -20° |
| 出手 / 接触 | f0→f4（**4 帧 = 0.133s**） | 全部主要骨骼用 bezier 在 4 帧内冲到出手姿势：`butt_attach` tx 43.4→5.1（前冲 38 单位），`arm_upper_l` -41→-55.5°，`arm_lower_l` 19.6→54.6°（f5），`breathe.scaleX` 1.00→**1.55**（胸口鼓胀 stretch），`arm_lower_l.scaleY` 1.08、`hand.scaleY` 1.09（拉伸）；**事件 `attack1` 在 f4**——伤害/命中点 |
| 定格（hold） | f4→f20（16 帧） | 绝大多数通道 stepped 定格（值不变），只有：`eye` 每 1 帧抖动（23 键，tx ±3.3、scale 0.85–1.14）、`weapons_base` 33 键震动（±6.8）、手指/肩章继续 settle → 冲击震屏感由“局部高频抖动 + 主体定格”实现；`attack_test` f8，`attack_end` f13 |
| 收招（recovery） | f20→f30/f40 | bezier 回到 setup；左臂 f30 回到 0，`throne_bone.translateX` 在 f29 冲过头到 -1.55（setup 动画值 5.76）再 f40 回来——收招 overshoot；`throne_bone.rotate` -2.06(f13)→+1.32(f29)→0.12(f40) 同样过冲 |

**重叠动作（各骨骼到位/极值帧）**：`butt_attach`/`body`/`neck`/`arm_upper_l`/`hand_r` 在 f4 到达出手姿势 → `arm_lower_l`、`epaulette l` f5 → `hand`（大幅回正到 -18.9°）、`hand_r2`、`epaulette r` f8 → `epaulette l` 二次摆 f11 → `breathe.translate` f14。也就是 **根/躯干 f4 → 前臂 f5 → 手 f8 → 附属物 f11–f14**，链上每级落后 1–3 帧。手指：pinky_l1/l2 键在 f2/4/9/16，末节 pinky_l3 在 f2/6/11/18——**末节统一比上一节晚 2 帧**（教科书式 offset）。
**挤压/拉伸**：head scaleY 0.91（f0 蓄力压扁）→ 0.98（f4）→ 1.0（f34）；breathe scaleX 1.55（f4–f20）；arm_upper_l scaleX 0.88；arm_lower_l scaleX 0.94 / scaleY 1.08；hand scaleY 1.09；eye scaleY 0.24（f26–f32 眯眼）。

| 骨骼 | 旋转 (°) | 位移X | 位移Y | 缩放X | 缩放Y | 切变 | 关键帧数 | 极值帧 | 关键帧时刻 (帧@30fps) |
|---|---|---|---|---|---|---|---|---|---|
| throne_adjust |  |  | -2.5~0.0 |  |  |  | 14 | f16(ty) | 0 3 6 10 13 16 19 22 25 28 31 34 37 40 |
| throne_bone | -2.1~1.3 | -1.5~42.2 | -36.2~-18.9 |  |  |  | 14 | f4(tx) | 0 1 4 13 15 17 29 40 |
| throne_twist |  | =0.2 | -5.6~1.0 |  |  |  | 5 | f10(ty) | 0 10 20 30 |
| throne_counter_twist |  | -0.2~0.0 | -1.0~0.0 |  |  |  | 2 | f40(ty) | 4 40 |
| minion_r_cog | -6.2~0.1 | =7.9 | =-6.5 |  |  |  | 5 | f5(r) | 0 5 18 29 |
| minion_shoulder_r |  | 1.7~3.7 | -9.5~-4.9 |  |  |  | 4 | f10(ty) | 0 10 20 29 |
| minion_r_cog6 | =5.6 |  |  | 0.98~1.06 | =0.98 |  | 24 | f0(sx) | 21个, 0–40 |
| minion_r_cog9 | =-22.6 |  |  | 0.98~1.04 | =0.98 |  | 25 | f0(r) | 21个, 0–40 |
| minion_l_cog | 0.1~4.5 | -0.2~6.6 | 0.1~5.6 |  |  |  | 8 | f4(r) | 0 4 18 28 33 |
| minion_l_leg_1 | =-11.3 |  |  | 1.02~1.05 | =1.02 |  | 23 | f0(sx) | 21个, 0–40 |
| minion_l_leg_2 | =-18.4 |  |  | 1.00~1.04 |  |  | 22 | f2(sx) | 21个, 0–40 |
| minion_r_leg_ik2_const |  | 46.1~90.0 | 1.5~12.3 |  |  |  | 5 | f0(tx) | 0 3 24 26 28 |
| butt_attach | -1.2~0.1 | 5.1~43.4 | -30.6~-19.0 |  |  |  | 4 | f0(tx) | 0 4 |
| body | -1.9~2.5 |  |  |  |  |  | 4 | f0(r) | 0 4 20 40 |
| neck | -0.1~6.3 | =0.0 | 0.0~0.2 |  |  |  | 4 | f0(r) | 0 4 |
| head | -0.1~4.4 |  |  | =1.00 | 0.91~1.00 |  | 8 | f0(sy) | 0 4 20 34 40 |
| face_twist |  | 0.0~4.4 | -12.9~-1.3 |  |  |  | 6 | f12(ty) | 0 4 12 20 28 40 |
| eye |  | -3.3~0.1 | -1.9~2.1 | 0.85~1.14 | 0.24~1.14 |  | 44 | f26(sy) | 26个, 0–40 |
| elbow_r_ik |  | -2.9~25.3 | -7.5~0.0 |  |  |  | 4 | f10(tx) | 0 10 20 30 |
| shoulder_r |  | -0.7~0.0 | 0.0~3.8 |  |  |  | 4 | f11(ty) | 0 11 20 34 |
| arm_upper_r | -2.9~2.0 |  |  |  |  |  | 4 | f0(r) | 0 4 20 40 |
| shoulder_l |  | 0.0~6.6 | -1.5~1.2 |  |  |  | 4 | f11(tx) | 0 11 20 30 |
| arm_upper_l | -55.5~0.0 |  |  | 0.88~1.00 | =1.00 |  | 9 | f4(r) | 0 4 10 20 30 |
| arm_lower_l | 0.5~54.6 | -2.1~4.1 | 0.0~18.9 | 0.94~1.00 | 1.00~1.08 |  | 13 | f5(r) | 0 4 5 14 20 30 |
| hand | -71.6~-15.1 | -4.5~0.0 | 0.0~3.2 | 1.00~1.02 | 1.00~1.09 |  | 14 | f0(r) | 0 2 4 8 16 20 29 40 |
| leg_upper_l |  | 0.0~2.7 | -0.5~0.0 |  |  |  | 4 | f11(tx) | 0 11 20 34 |
| leg_lower_l | 0.0~1.9 |  |  | 0.95~1.00 | =1.00 |  | 6 | f10(sx) | 0 10 20 34 40 |
| foot_l | 0.0~2.4 |  |  |  |  |  | 2 | f40(r) | 20 40 |
| leg_r_upper |  | -4.1~1.6 | 0.0~0.5 |  |  |  | 4 | f11(tx) | 0 11 20 34 |
| leg_r_lower | -65.7~-61.7 |  |  |  |  |  | 4 | f0(r) | 0 10 20 34 |
| arm_lower_r | -6.0~-1.6 |  |  |  |  |  | 4 | f4(r) | 0 4 20 40 |
| hand_r | -80.1~-0.7 |  |  | 0.99~1.02 | 1.00~1.04 | =0.0 -1.0~0.9 | 7 | f0(r) | 0 2 4 |
| arm_lower_r2 | -5.1~0.0 | =0.3 | =1.1 |  |  |  | 3 | f0(r) | 0 4 |
| breathe | 0.0~2.1 | -0.1~3.7 | 0.0~12.8 | 1.00~1.55 | =1.00 |  | 13 | f4(sx) | 0 4 14 20 31 |
| thumb_l | 0.0~12.6 |  |  | =1.00 | 0.91~1.00 |  | 6 | f40(r) | 0 4 20 40 |
| thumb_l2 | -20.2~-10.1 |  |  |  |  |  | 4 | f4(r) | 0 4 20 40 |
| pinky_l1 | -41.1~24.9 |  |  |  |  |  | 7 | f0(r) | 0 2 4 9 16 20 33 |
| pinky_l2 | -79.6~22.8 |  |  |  |  |  | 7 | f33(r) | 0 2 4 9 16 20 33 |
| pinky_l3 | -32.3~14.2 |  |  |  |  |  | 7 | f33(r) | 0 2 6 11 18 20 33 |
| pinky_l4 | -29.6~34.1 |  |  |  |  |  | 7 | f9(r) | 0 2 4 9 16 20 35 |
| pinky_l5 | -55.4~26.0 |  |  |  |  |  | 7 | f35(r) | 0 2 4 9 16 20 35 |
| pinky_l6 | -30.2~32.6 |  |  |  |  |  | 7 | f11(r) | 0 2 6 11 18 20 35 |
| index_l | -53.9~7.8 |  |  |  |  |  | 7 | f40(r) | 0 2 4 9 16 20 40 |
| index_l2 | -59.1~10.5 |  |  |  |  |  | 7 | f40(r) | 0 2 4 9 16 20 40 |
| index_l3 | -23.5~32.5 |  |  |  |  |  | 7 | f11(r) | 0 2 6 11 18 20 40 |
| cuff_l | -20.1~0.0 | 0.0~0.4 | -1.6~0.0 | =1.00 | 1.07~1.14 |  | 13 | f0(r) | 0 2 4 11 20 40 |
| epaulette l | -10.8~0.3 | -16.1~0.0 | 0.0~2.9 | 0.91~1.00 | =1.00 |  | 13 | f5(r) | 0 4 5 11 20 22 30 |
| epaulette r | 0.0~3.9 |  |  |  |  |  | 3 | f8(r) | 0 8 20 |
| weapons_base |  | -6.8~5.4 | -6.9~6.8 |  |  |  | 33 | f21(ty) | 33个, 0–32 |
| hand_r2 | 0.0~12.3 |  |  |  |  |  | 4 | f8(r) | 0 8 19 32 |
| shouldr folder l |  | -11.3~0.0 | 0.0~11.7 |  |  |  | 4 | f0(ty) | 0 4 20 39 |
| sparkle |  | -1522.1~-1429.4 | -314.2~-205.3 | 0.51~2.21 | 0.51~2.21 |  | 6 | f0(tx) | 0 5 10 16 |
| sparkle2 |  | -1475.4~-1429.4 | -314.2~-264.9 | 0.51~1.38 | 0.51~1.38 |  | 6 | f0(tx) | 0 5 10 15 |
| sparkle3 |  | -1497.5~-1429.4 | -314.2~-304.6 | 0.51~1.06 | 0.51~1.06 |  | 6 | f0(tx) | 0 5 8 12 |


### 4.3 cast（1.8s = 54 帧）

| 阶段 | 帧 | 内容 |
|---|---|---|
| 起始/预备 | f0–f2 | `hand` 在 -12.7° stepped 定 2 帧（f0–f2）；`head` -9°、`arm_upper_l` -18.7°、`arm_lower_l` +20.4°、`breathe.scaleX` 1.12（吸气胀起） |
| 施法爆发 | f2→f5（3 帧） | `hand` -12.7°→**-170.1°**（f5，翻掌向上），`hand.translate` 到 (5.4, -14.1)（f4）；`elbow_r_ik` ty +58.7（f4）；`throne_bone` ty 下沉到 -40.6（f5）、rot +3.75（f5）；`minion_r_cog` tx +23.5（f5）；`body` +1.73°（f4）；高光层 f4 起、f6 满亮 |
| 定格 | f4/5→f24/25 | 大量通道 stepped hold ~20 帧；`hand` 在 hold 中仍从 -170.1 缓慢漂到 -165.7（f24，“活”的定格） |
| 挤压 | f8–f25 | `breathe.scaleX` 1.12→**0.79**（f10，呼气收缩，hold 到 f25）；`arm_upper_l.scaleY` 0.90（f8）、`arm_lower_l.scaleY` 1.12（f8）、`head.scaleY` 0.91（f5–f23） |
| 收势 | f24→f40（+ 尾部到 f54） | 回到 setup；`arm_lower_l` 先回到 -0.09（f27）再 overshoot 到 +9.2（f32）后 f40 归位；`head` -0.07(f8)→-1.71(f12)→-0.07(f22) 小幅回弹；`minion_r_body_const.mixRotate` 在 1.033s→1.8s 从 0 渐回 1 |

重叠：`elbow_r_ik`/`body`/`hand.translate` f4 → `hand.rotate`/`throne_bone`/`minion_r_cog`/`face_twist` f5 → `hand_r2`/`arm_lower_r2` f6 → `arm_upper_l.scale` f8 → `breathe` f10。眼睛 0.1s 换 half eye，1.1s 恢复。

| 骨骼 | 旋转 (°) | 位移X | 位移Y | 缩放X | 缩放Y | 切变 | 关键帧数 | 极值帧 | 关键帧时刻 (帧@30fps) |
|---|---|---|---|---|---|---|---|---|---|
| throne_adjust |  |  | -2.5~0.0 |  |  |  | 19 | f9(ty) | 19个, 0–53 |
| throne_bone | -1.4~3.8 | 3.6~23.3 | -40.6~-19.1 |  |  |  | 14 | f5(ty) | 0 4 5 15 17 26 43 54 |
| throne_twist |  | =0.2 | -2.3~12.5 |  |  |  | 5 | f0(ty) | 0 27 40 54 |
| throne_counter_twist |  | -0.2~0.0 | -1.0~0.0 |  |  |  | 2 | f40(ty) | 4 40 |
| minion_r_cog | -4.2~0.1 | 7.9~23.5 | -15.9~-2.3 |  |  |  | 8 | f5(tx) | 0 5 26 40 |
| minion_shoulder_r |  | 0.0~1.7 | -4.9~0.0 |  |  |  | 2 | f54(ty) | 27 54 |
| minion_r_cog6 | =7.4 |  |  | 0.98~1.04 | =0.98 |  | 32 | f4(r) | 28个, 0–54 |
| minion_r_cog8 | 30.9~44.8 |  |  | =1.02 | =1.00 |  | 4 | f0(r) | 0 4 40 |
| minion_r_cog9 | -42.2~-26.8 |  |  | 0.98~1.04 | =0.98 |  | 32 | f4(r) | 28个, 0–54 |
| minion_l_cog | =0.1 | =-0.2 | 0.1~10.6 |  |  |  | 6 | f4(ty) | 0 4 26 43 54 |
| minion_l_leg_1 | =-11.3 |  |  | 1.05~1.08 | =1.02 |  | 30 | f2(sx) | 28个, 0–54 |
| minion_l_leg_2 | =-18.4 |  |  | 0.96~1.00 |  |  | 29 | f2(sx) | 28个, 0–54 |
| minion_l_leg_4 | =-1.0 |  |  | =1.00 | 1.00~1.09 |  | 22 | f2(sy) | 21个, 0–40 |
| butt_attach | -0.0~0.1 | -0.0~5.1 | -19.0~0.0 |  |  |  | 6 | f0(ty) | 0 4 40 |
| body | 0.0~1.7 |  |  |  |  |  | 3 | f4(r) | 0 4 8 |
| neck | -0.7~1.2 | =0.0 | =0.2 |  |  |  | 5 | f4(r) | 0 4 10 17 |
| head | -9.0~-0.1 |  |  | =1.00 | 0.91~1.00 |  | 8 | f5(sy) | 0 5 8 12 22 23 40 |
| face_twist |  | -0.0~0.0 | -9.0~4.8 |  |  |  | 5 | f5(ty) | 0 2 5 23 40 |
| eye |  | 0.0~2.2 | =-0.0 | 0.84~1.00 | 1.00~1.16 |  | 6 | f0(sy) | 0 1 3 |
| elbow_r_ik |  | -13.5~6.2 | -1.7~58.7 |  |  |  | 5 | f4(ty) | 0 4 12 27 40 |
| shoulder_r |  | -0.7~0.0 | 0.0~4.8 |  |  |  | 3 | f25(ty) | 0 25 40 |
| arm_upper_r | -0.1~2.0 |  |  |  |  |  | 3 | f40(r) | 0 4 40 |
| shoulder_l |  | 0.0~7.3 | -9.3~1.2 |  |  |  | 3 | f25(ty) | 0 25 40 |
| arm_upper_l | -18.7~0.0 |  |  | 0.87~1.02 | 0.90~1.00 |  | 11 | f0(r) | 0 5 8 14 16 24 30 33 40 |
| arm_lower_l | -4.8~20.4 | -5.0~0.0 | 0.0~15.9 | 1.00~1.03 | 1.00~1.12 | -0.4~0.0 =0.0 | 17 | f0(r) | 0 5 7 8 15 24 27 32 33 40 |
| hand | -170.1~-12.7 | 0.0~5.4 | -14.1~2.6 |  |  |  | 11 | f5(r) | 0 2 4 5 17 24 40 |
| leg_lower_l | 0.0~1.9 |  |  | 0.96~1.00 | =1.00 |  | 5 | f25(sx) | 0 4 25 40 |
| foot_l | 0.0~2.4 |  |  |  |  |  | 2 | f40(r) | 4 40 |
| leg_r_upper |  | -2.5~5.0 | -0.7~0.5 |  |  |  | 3 | f0(tx) | 0 25 40 |
| leg_r_lower | -69.5~-60.6 |  |  |  |  |  | 3 | f0(r) | 0 25 40 |
| arm_lower_r | -1.6~0.7 |  |  |  |  |  | 3 | f40(r) | 0 4 40 |
| hand_r | -4.5~0.0 |  |  | 1.00~1.04 | 1.00~1.00 | =0.0 -0.4~0.6 | 9 | f0(r) | 0 4 40 |
| arm_lower_r2 | -2.8~2.4 | -3.1~4.4 | -2.4~1.1 |  |  |  | 9 | f0(r) | 0 6 14 20 40 |
| breathe |  | -0.1~0.7 | -3.4~4.7 | 0.79~1.12 | =1.00 |  | 8 | f10(sx) | 0 10 25 35 |
| thumb_l | 0.0~17.8 | -5.3~0.0 | 0.0~0.5 | =1.00 | 1.00~1.13 |  | 11 | f0(r) | 0 2 4 24 40 |
| thumb_l2 | -28.0~0.0 |  |  |  |  |  | 5 | f0(r) | 0 2 4 24 40 |
| pinky_l1 | -36.2~5.5 |  |  |  |  |  | 5 | f4(r) | 0 2 4 24 40 |
| pinky_l2 | -79.6~-41.1 | -2.2~0.0 | 0.0~5.6 |  |  |  | 9 | f40(r) | 0 2 4 24 40 |
| pinky_l3 | -36.2~-25.6 | -9.4~0.0 | -4.1~0.0 |  |  |  | 9 | f4(r) | 0 2 4 24 40 |
| pinky_l4 | -22.7~13.5 |  |  |  |  |  | 5 | f4(r) | 0 2 4 24 40 |
| pinky_l5 | -55.4~-38.0 | -1.9~0.0 | 0.0~0.0 |  |  |  | 9 | f40(r) | 0 2 4 24 40 |
| pinky_l6 | -48.1~-11.9 |  |  |  |  |  | 5 | f4(r) | 0 2 4 24 40 |
| index_l | -53.9~7.5 |  |  |  |  |  | 7 | f40(r) | 0 2 5 16 20 25 40 |
| index_l2 | -59.1~7.5 |  |  |  |  |  | 7 | f40(r) | 0 2 4 8 19 24 40 |
| index_l3 | -20.4~66.1 |  |  |  |  |  | 7 | f4(r) | 0 2 4 8 15 24 40 |
| cuff_l | -72.6~4.3 | 0.0~4.2 | -1.8~0.1 | =1.00 | 1.00~1.20 | =0.0 -1.7~0.0 | 17 | f4(r) | 0 2 4 24 40 |
| epaulette l | 0.0~8.7 | -5.9~0.0 | -0.6~0.0 | 0.93~1.00 | =1.00 |  | 13 | f0(r) | 0 4 25 30 40 |
| hand_r2 | -10.2~0.0 | 0.0~3.5 | -3.3~0.0 |  |  |  | 8 | f6(r) | 0 6 8 20 |
| shouldr folder l |  | 0.0~2.6 | -7.1~0.0 |  |  |  | 4 | f5(ty) | 0 5 27 35 |


### 4.4 hurt（0.933s = 28 帧）

| 阶段 | 帧 | 内容 |
|---|---|---|
| 受击前一帧 | f0–f1 | 几乎所有通道 f0→f1 为 stepped 定格（同值），即**第 1 帧原样保持**；f0 起 draw order 把右臂移到前景、眼睛 f4 换 mad eye |
| 反向预备 | f1→f2 | `body` -10.7°→**-12.4°（f2）**，`leg_r_upper` +12.9°（f2）、`arm_lower_r` 拉伸 scaleX 1.15 / scaleY **1.26** / shearX 13.4（f2）——被击中瞬间先往反方向“缩” |
| 击退 | f1→f4/f5（3–4 帧） | `butt_attach` tx +30.6 → **-136.8（f5）**（≈167 单位横向击飞），`throne_bone` tx 14.6 → -82.6（f4）、ty → -47.9（f6）；`body` → **+19.0°（f5）**（1→5 帧摆幅 31°）；`neck` -4.3°(f4)→+14.4°(f6)；`head` -16.7°(f4)；右臂 IK 目标 `elbow_r_ik` 被甩到 (-294.6, +684.7)（f5）；仆从 `minion_r_cog` tx -53.2（f5），`minion_l_cog` tx -29.3（f4） |
| 拖拽 / 跟随 | f5→f13 | `arm_upper_l` -71.2°（f9）、`arm_lower_l` -52.7°（f12）、`epaulette l` -17.5°（f9）、`arm_lower_r2` +81.2°（f9）、`hand_r2` +22.8°（f8，scaleY 0.67 压扁）、`breathe.scaleX` 1.56（f8）、`regent_cog4`(脚) +17.3°（f12）、`leg_r_lower` -9.5°（f10） |
| 衰减回正 | f13→f28 | `head` 呈阻尼振荡：-16.7(f4) → +1.4(f6) → -1.95(f9) → +2.98(f13) → -1.29(f19) → +2.45(f22) → -0.07(f28)，3 次过零；`body` 19.0→14.3→12.3→10.4→2.5→0（f5→f28 缓出）；`hand` 先回 -32.4（f8）再被拉回 -84.6（f20）后 f28 归位（拖拽 + 回弹）；`eye.scaleY` 0.22（f18 紧闭）→1.0（f28） |

重叠时间差（主链极值）：腿/脚 f2–f3 → 王座/根 f4–f5 → 躯干 f5 → 颈 f6 → 上臂/肩章 f9 → 前臂 f12 → 头部回弹峰 f13；每级落后约 1–3 帧，整条链从受力到末端完成需 ~10 帧。

| 骨骼 | 旋转 (°) | 位移X | 位移Y | 缩放X | 缩放Y | 切变 | 关键帧数 | 极值帧 | 关键帧时刻 (帧@30fps) |
|---|---|---|---|---|---|---|---|---|---|
| throne_adjust |  |  | -2.5~0.0 |  |  |  | 11 | f7(ty) | 0 2 5 7 10 13 16 19 22 25 28 |
| throne_bone | -2.9~7.6 | -82.6~14.6 | -47.9~-19.1 |  |  |  | 17 | f4(tx) | 0 1 4 5 6 13 14 28 |
| throne_twist |  | -0.0~0.2 | -15.1~10.2 |  |  |  | 11 | f8(ty) | 0 1 6 8 20 28 |
| throne_counter_twist |  | -0.2~0.0 | -1.0~0.0 |  |  |  | 3 | f28(ty) | 5 6 28 |
| minion_r_cog | -3.1~7.4 | -53.2~7.9 | -6.5~9.9 |  |  |  | 9 | f5(tx) | 1 5 6 13 28 |
| minion_shoulder_r |  | 0.0~12.1 | -24.5~0.0 |  |  |  | 4 | f8(ty) | 1 6 8 28 |
| minion_r_arm_3 | 8.5~23.1 |  |  |  |  |  | 5 | f6(r) | 0 1 5 6 28 |
| minion_r_arm_4 | -17.9~-6.3 |  |  |  |  |  | 5 | f0(r) | 0 1 5 6 28 |
| minion_r_arm_1 | -37.1~44.3 |  |  |  |  |  | 5 | f5(r) | 0 1 5 6 28 |
| minion_r_arm_2 | -29.3~31.1 |  |  |  |  |  | 5 | f0(r) | 0 1 5 6 28 |
| minion_r_cog6 | 7.4~50.6 |  |  | 0.81~1.03 | 0.98~1.00 |  | 23 | f6(r) | 17个, 0–28 |
| minion_r_cog7 | -64.7~-15.9 |  |  | 1.00~1.05 | =1.00 |  | 7 | f6(r) | 0 1 5 6 28 |
| minion_r_cog8 | 6.2~48.8 |  |  | 1.00~1.02 | =1.00 |  | 8 | f8(r) | 0 1 5 6 8 19 28 |
| minion_r_cog9 | -26.8~27.7 |  |  | 0.98~1.04 | 0.98~1.00 |  | 13 | f19(r) | 0 1 5 6 8 19 24 26 28 |
| minion_l_cog | -10.8~7.9 | -29.3~0.0 | -11.8~0.1 |  |  |  | 9 | f4(tx) | 0 1 4 6 28 |
| minion_l_leg_1 | -31.5~4.7 |  |  | 0.95~1.05 | 1.00~1.02 |  | 22 | f6(r) | 0 1 5 6 7 9 11 13 15 17 19 21 23 25 28 |
| minion_l_leg_3 | 6.8~61.8 |  |  | =1.00 | =1.00 |  | 7 | f6(r) | 0 1 5 6 28 |
| minion_l_leg_2 | -47.4~-15.2 |  |  |  |  |  | 5 | f6(r) | 0 1 5 6 28 |
| minion_l_leg_4 | -13.8~7.9 |  |  | =1.00 | =1.00 |  | 8 | f0(r) | 0 1 5 6 14 28 |
| minion_l_arm_1 | -27.6~-7.8 |  |  |  |  |  | 5 | f6(r) | 0 1 5 6 28 |
| minion_l_arm_2 | 6.6~36.9 |  |  |  |  |  | 5 | f6(r) | 0 1 5 6 28 |
| minion_r_leg_ik2_const |  | 46.1~63.6 | 1.5~46.0 |  |  |  | 5 | f3(tx) | 0 1 3 5 19 |
| minion_l_leg_ik2_const |  | -40.7~4.8 | -6.5~4.6 |  |  |  | 6 | f5(tx) | 0 1 2 5 19 26 |
| minion_l_leg_ik1_const |  | -117.3~-35.1 | 4.7~22.2 |  |  |  | 6 | f6(tx) | 0 1 2 6 13 19 |
| butt_attach | -2.9~9.6 | -136.8~30.6 | -47.9~-19.0 |  |  |  | 10 | f5(tx) | 0 1 5 6 28 |
| body | -12.4~19.0 |  |  |  |  |  | 10 | f5(r) | 0 1 2 5 6 8 13 14 22 28 |
| neck | -4.3~16.4 | =0.0 | 0.0~0.2 |  |  |  | 9 | f0(r) | 0 1 4 5 6 13 19 28 |
| head | -16.7~3.0 | 0.0~7.1 | -9.6~5.3 | 0.86~1.00 | 0.97~1.00 |  | 19 | f4(r) | 0 1 4 5 6 9 13 19 22 28 |
| face_twist |  | -26.3~13.6 | -9.6~0.0 | 0.98~1.00 | 0.87~1.00 |  | 13 | f0(tx) | 0 1 6 9 28 |
| eye |  |  |  | 0.98~1.13 | 0.22~1.30 |  | 9 | f18(sy) | 0 1 4 6 14 17 18 25 28 |
| elbow_r_ik |  | -294.6~22.3 | 0.0~684.7 |  |  |  | 7 | f5(ty) | 1 5 8 16 22 26 28 |
| shoulder_r |  | -0.7~5.8 | 0.0~7.8 |  |  |  | 5 | f6(ty) | 1 3 6 14 28 |
| arm_upper_r | 2.0~55.2 |  |  |  |  |  | 5 | f5(r) | 0 1 5 6 28 |
| shoulder_l |  | -11.4~5.7 | -10.1~17.9 |  |  |  | 6 | f0(ty) | 0 1 6 8 15 28 |
| arm_upper_l | -71.1~15.4 | -0.2~0.0 | -1.7~0.0 | 0.85~1.00 | 1.00~1.00 |  | 19 | f9(r) | 0 1 3 4 5 6 7 8 9 18 28 |
| arm_lower_l | -52.7~0.5 | -3.6~7.4 | -8.6~10.6 | 0.79~1.00 | 1.00~1.04 |  | 23 | f12(r) | 0 1 3 4 5 6 8 12 15 20 28 |
| hand | -116.1~-15.1 | 0.0~4.0 | 0.0~0.0 | 1.00~1.19 | 1.00~1.03 | =0.0 0.0~13.2 | 23 | f0(r) | 0 1 4 5 6 8 13 16 20 28 |
| leg_upper_l | -1.1~8.9 |  |  |  |  |  | 5 | f0(r) | 0 1 5 6 14 |
| leg_lower_l | 1.9~39.2 |  |  | 0.87~0.94 | =1.00 |  | 12 | f3(r) | 0 1 3 5 6 13 14 23 28 |
| foot_l | -4.7~6.1 |  |  |  |  |  | 5 | f21(r) | 1 5 14 21 28 |
| leg_r_upper | 0.0~12.9 | 0.0~1.6 | =0.0 |  |  |  | 10 | f2(r) | 0 1 2 4 5 6 28 |
| leg_r_lower | -65.6~-9.5 |  |  |  |  |  | 7 | f0(r) | 0 2 6 10 17 20 25 |
| regent_cog4 | -5.2~17.3 |  |  |  |  |  | 7 | f12(r) | 0 2 4 12 18 23 28 |
| arm_lower_r | -10.1~24.9 |  |  | 1.00~1.15 | 1.00~1.26 | 0.0~13.4 =0.0 | 11 | f2(sy) | 0 1 2 5 6 28 |
| hand_r | -54.2~-0.7 | 0.0~6.0 | -0.8~2.4 | 0.92~1.11 | 1.00~1.37 | =0.0 -0.4~8.9 | 21 | f0(r) | 0 1 4 5 6 9 28 |
| arm_lower_r2 | -54.8~81.2 | -33.4~11.8 | -3.1~63.9 |  |  |  | 13 | f9(r) | 0 1 4 5 6 9 15 28 |
| breathe |  | -0.1~5.2 | -6.0~16.7 | 1.02~1.56 | =1.00 |  | 12 | f8(sx) | 0 1 6 8 15 28 |
| minion_r_arm_tip | -24.3~3.1 |  |  |  |  | =0.0 =-0.0 | 8 | f6(r) | 0 1 5 6 28 |
| thumb_l | 0.0~12.6 |  |  |  |  |  | 2 | f28(r) | 19 28 |
| thumb_l2 | -10.1~0.0 |  |  |  |  |  | 2 | f28(r) | 19 28 |
| pinky_l1 | -28.6~30.3 |  |  |  |  |  | 6 | f13(r) | 1 5 6 13 19 28 |
| pinky_l2 | -79.6~29.5 |  |  |  |  |  | 6 | f28(r) | 1 5 6 13 19 28 |
| pinky_l3 | -32.3~18.0 |  |  |  |  |  | 6 | f28(r) | 1 5 6 13 19 28 |
| pinky_l4 | -21.8~39.1 |  |  |  |  |  | 6 | f13(r) | 1 5 6 13 19 28 |
| pinky_l5 | -55.4~35.0 |  |  |  |  |  | 6 | f28(r) | 1 5 6 13 19 28 |
| pinky_l6 | -25.9~35.2 |  |  |  |  |  | 6 | f13(r) | 1 5 6 13 19 28 |
| index_l | -53.9~23.4 |  |  |  |  |  | 6 | f28(r) | 1 5 6 13 19 28 |
| index_l2 | -59.1~10.6 |  |  |  |  |  | 6 | f28(r) | 1 5 6 13 19 28 |
| index_l3 | -20.4~31.8 |  |  |  |  |  | 6 | f6(r) | 1 5 6 13 19 28 |
| cuff_l | -38.9~0.0 | 0.0~0.4 | -1.6~0.0 | =1.00 | 1.07~1.32 |  | 13 | f0(r) | 0 1 6 15 20 28 |
| epaulette l | -17.5~13.4 | -16.8~4.5 | 0.0~10.0 |  |  |  | 13 | f9(r) | 0 1 3 6 9 15 28 |
| weapol_large | 0.5~6.1 |  |  |  |  |  | 3 | f8(r) | 0 1 8 |
| hand_r2 | 0.0~22.8 |  |  | 1.00~1.06 | 0.67~1.00 |  | 10 | f8(sy) | 4 6 8 15 23 |
| shadow |  | -37.7~12.0 | =0.0 | 0.90~1.00 | =1.00 |  | 7 | f2(tx) | 0 1 2 28 |
| shouldr folder l |  | -27.6~0.0 | -8.4~0.0 |  |  |  | 5 | f5(tx) | 0 5 8 13 18 |


## 5. Physics 约束

**regent.skel 中 Physics 约束数量为 0**（文件头之后的 physics 计数 varint = 0），动画中也没有任何 physics 时间轴（inertia/strength/damping/mass/wind/gravity/mix/reset 全部为 0 条）。Path 约束同样为 0。为对照，同一批资源中的 `regent_weapon.skel`（4.2.08）、`characterselect_regent.skel`（4.2.40）、`restsite_regent.skel`（4.2.43）也都没有 physics / path 约束。

因此原版所有“次级运动”（肩章 `epaulette l/r`、袖口 `cuff_l`、`shouldr folder l`、手指、仆从颤抖、王座悬浮震动）**都是手 K 关键帧**实现的：
- 跟随/拖拽用 1–3 帧的极值错开 + bezier；
- 高频震动用 2–3 帧间隔的往返关键帧（详见 §3）；
- 冲击用“主体 stepped 定格 + 局部（eye、weapons_base）逐帧抖动”。

## 6. 关键数字速查

| 指标 | 值 |
|---|---|
| 骨骼 / 最大深度 | 103 / 10 |
| 插槽 / additive 插槽 / 无附件插槽 | 81 / 25 / 16 |
| 附件：mesh / linkedmesh / region | 40 / 3 / 35（加权 mesh 38） |
| 网格顶点合计；最大网格 | 1,636；`right guy highlights` 104v |
| sprite swap 插槽 | 8（eye, eye add, bottom hand, hand, bottom arm 1, top arm 1, top arm 2, emote） |
| IK / Transform / Path / Physics | 10 / 9 / 0 / 0 |
| attack：接触帧 / 定格 / 总长 | f4（0→4 帧冲击） / f4–f20 / 40 帧 |
| cast：爆发 / 定格 / 总长 | f2→f5 / f5–f24 / 54 帧 |
| hurt：击退峰值 / 链末端 / 总长 | f5（butt_attach -136.8） / f12–f13 / 28 帧 |
| idle：基础周期 | 80 帧（2.667s）×7 + 尾部 80 帧手势 |
| 最大 squash/stretch | breathe scaleX 1.55–1.56（attack/hurt）、0.79（cast）；arm_lower_r scaleY 1.26（hurt）；hand_r2 scaleY 0.67（hurt）；head scaleY 0.91（attack/cast） |
