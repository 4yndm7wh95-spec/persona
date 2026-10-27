# 召唤过渡帧当前成果与剩余跳变

## 当前状态（尚未全部通过）

开始前fetch+merge成功，快进到e935d8d32。hold/rip/release三张按用户确认记为定稿；沿用Claude已登记的1.0/front对齐，没有改关键帧原图或检查阈值。

本轮生成12张不同过渡帧，含指定的lift1、lift2、fling、settle及为检查追加的8张。仅lift_mid因比例FAIL重做一次；lift2首次常规检查因手指挡住脸不适用，按用户规则改用--occluded，未重新生成。其余各一次。所有当前帧比例/遮挡高度检查通过，均按1.0/front归一化。

用户随后反馈“你别重复生成那么多了”“搞定了就推送”。已停止继续追加生图，整理并推送当前成果。**完整召唤仍3处JUMP，普通召唤收回仍1处JUMP，不能宣称本任务已达到0跳变。** 没有为通过而放宽阈值、添加--allow豁免或手工缩放图片。

## 新图

| 帧 | 用途 |
|---|---|
| summon_lift1_a | idle底图，手到胸口 |
| summon_lift_mid_a | lift1与lift2间，手到领口 |
| summon_lift_bridge_a | 同段细分投影/脚位 |
| summon_lift2_a | hold底图，手到脸旁 |
| summon_touch1_a / summon_touch_mid_a / summon_contact_a / summon_touch2_a | 靠近扶脸、缩小站距的细分 |
| summon_fling1_a / summon_fling2_a / summon_fling_a | 撕开后向外展开的细分，fling_a为指定中间帧 |
| summon_settle_a | release底图，手放下一半、大衣回落 |

均为内置image_gen编辑，原图1024×1536 RGBA真透明，只有主体外alpha1～2残点清零，不脚本改姿势；不画面具和特效，不做额外主观审图。每张完整提示词、输入、清理统计、检查与归一化记录见对应*_generation.json。

## 每张比例检查输出

`python tools/p5/pose_consistency.py idle_a3 summon_lift1_a`

```text
summon_lift1_a     head x1.009  body x0.996  boot height x0.994  boot width x0.993  feet moved [46, 3] px  ok
sheet: art\p5\poses\check_summon_lift1_a.png | overall: ok
```

`python tools/p5/pose_consistency.py idle_a3 summon_lift2_a --occluded summon_lift2_a`

```text
summon_lift2_a     (face covered) standing height x0.998  boot height x0.971  boot width x1.009  feet moved [3, 90] px  ok
sheet: art\p5\poses\check_summon_lift2_a.png | overall: ok
```

`python tools/p5/pose_consistency.py idle_a3 summon_fling_a`

```text
summon_fling_a     head x0.978  body x0.982  boot height x1.015  boot width x1.012  feet moved [123, 13] px  ok
sheet: art\p5\poses\check_summon_fling_a.png | overall: ok
```

`python tools/p5/pose_consistency.py idle_a3 summon_settle_a`

```text
summon_settle_a    head x0.987  body x0.988  boot height x1.016  boot width x0.995  feet moved [83, 24] px  ok
sheet: art\p5\poses\check_summon_settle_a.png | overall: ok
```

`python tools/p5/pose_consistency.py idle_a3 summon_lift_mid_a`

```text
summon_lift_mid_a  head x0.982  body x1.000  boot height x0.994  boot width x1.010  feet moved [58, 4] px  ok
sheet: art\p5\poses\check_summon_lift_mid_a.png | overall: ok
```

`python tools/p5/pose_consistency.py idle_a3 summon_touch1_a --occluded summon_touch1_a`

```text
summon_touch1_a    (face covered) standing height x0.994  boot height x0.945  boot width x1.037  feet moved [65, 158] px  ok
sheet: art\p5\poses\check_summon_touch1_a.png | overall: ok
```

`python tools/p5/pose_consistency.py idle_a3 summon_touch2_a --occluded summon_touch2_a`

```text
summon_touch2_a    (face covered) standing height x0.992  boot height x0.967  boot width x0.984  feet moved [83, 178] px  ok
sheet: art\p5\poses\check_summon_touch2_a.png | overall: ok
```

`python tools/p5/pose_consistency.py idle_a3 summon_fling1_a`

```text
summon_fling1_a    head x0.991  body x0.995  boot height x0.954  boot width x0.926  feet moved [83, 160] px  ok
sheet: art\p5\poses\check_summon_fling1_a.png | overall: ok
```

`python tools/p5/pose_consistency.py idle_a3 summon_fling2_a`

```text
summon_fling2_a    head x0.996  body x0.986  boot height x0.999  boot width x0.985  feet moved [129, 15] px  ok
sheet: art\p5\poses\check_summon_fling2_a.png | overall: ok
```

`python tools/p5/pose_consistency.py idle_a3 summon_lift_bridge_a --occluded summon_lift_bridge_a`

```text
summon_lift_bridge_a (face covered) standing height x0.995  boot height x0.945  boot width x0.968  feet moved [74, 42] px  ok
sheet: art\p5\poses\check_summon_lift_bridge_a.png | overall: ok
```

`python tools/p5/pose_consistency.py idle_a3 summon_touch_mid_a --occluded summon_touch_mid_a`

```text
summon_touch_mid_a (face covered) standing height x0.989  boot height x0.970  boot width x1.097  feet moved [25, 178] px  ok
sheet: art\p5\poses\check_summon_touch_mid_a.png | overall: ok
```

`python tools/p5/pose_consistency.py idle_a3 summon_contact_a --occluded summon_contact_a`

```text
summon_contact_a   (face covered) standing height x0.999  boot height x0.967  boot width x0.924  feet moved [51, 177] px  ok
sheet: art\p5\poses\check_summon_contact_a.png | overall: ok
```

lift2、lift_bridge、touch/contact这些手指局部遮住脸的帧使用--occluded；其余脸露出的用常规检查。lift_mid第一次head x0.889 FAIL，按规则从底图重做一次后head x0.982 ok，未手工缩放。

## 完整召唤：当前输出

`python tools/p5/sequence_check.py idle_a3 summon_lift1_a summon_lift_mid_a summon_lift_bridge_a summon_lift2_a summon_touch1_a summon_touch_mid_a summon_contact_a summon_touch2_a summon_hold_a summon_rip_a2 summon_fling1_a summon_fling2_a summon_fling_a summon_release_a summon_settle_a idle_a3`

```text
         idle_a3 -> summon_lift1_a   overlap 0.86  face moved     5  feet moved [48, 0]  ok
  summon_lift1_a -> summon_lift_mid_a overlap 0.91  face moved    13  feet moved [14, 0]  ok
summon_lift_mid_a -> summon_lift_bridge_a overlap 0.73  face moved    48  feet moved [54, 0]  ok
summon_lift_bridge_a -> summon_lift2_a   overlap 0.76  face moved    47  feet moved [24, 0]  ok
  summon_lift2_a -> summon_touch1_a  overlap 0.68  face moved    62  feet moved [8, 0]  ok
 summon_touch1_a -> summon_touch_mid_a overlap 0.83  face moved    18  feet moved [65, 0]  ok
summon_touch_mid_a -> summon_contact_a overlap 0.92  face moved     7  feet moved [72, 0]  ok
summon_contact_a -> summon_touch2_a  overlap 0.94  face moved     6  feet moved [34, 0]  ok
 summon_touch2_a -> summon_hold_a    overlap 0.91  face moved     5  feet moved [123, 0]  JUMP - add a frame
   summon_hold_a -> summon_rip_a2    overlap 0.89  face moved    35  feet moved [2, 0]  ok
   summon_rip_a2 -> summon_fling1_a  overlap 0.78  face moved    11  feet moved [137, 0]  JUMP - add a frame
 summon_fling1_a -> summon_fling2_a  overlap 0.48  face moved    96  feet moved [100, 0]  JUMP - add a frame
 summon_fling2_a -> summon_fling_a   overlap 0.85  face moved    19  feet moved [34, 0]  ok
  summon_fling_a -> summon_release_a overlap 0.85  face moved     4  feet moved [8, 0]  ok
summon_release_a -> summon_settle_a  overlap 0.73  face moved    50  feet moved [13, 0]  ok
 summon_settle_a -> idle_a3          overlap 0.64  face moved    60  feet moved [106, 0]  ok
3 jump(s); overlays: art\p5\poses\seq_idle_a3_idle_a3.png
```

`python tools/p5/sequence_check.py summon_hold_a summon_touch2_a summon_contact_a summon_touch_mid_a summon_touch1_a summon_lift2_a summon_lift_bridge_a summon_lift_mid_a summon_lift1_a idle_a3`

```text
   summon_hold_a -> summon_touch2_a  overlap 0.91  face moved     5  feet moved [123, 0]  JUMP - add a frame
 summon_touch2_a -> summon_contact_a overlap 0.94  face moved     6  feet moved [34, 0]  ok
summon_contact_a -> summon_touch_mid_a overlap 0.92  face moved     7  feet moved [72, 0]  ok
summon_touch_mid_a -> summon_touch1_a  overlap 0.83  face moved    18  feet moved [65, 0]  ok
 summon_touch1_a -> summon_lift2_a   overlap 0.68  face moved    62  feet moved [8, 0]  ok
  summon_lift2_a -> summon_lift_bridge_a overlap 0.76  face moved    47  feet moved [24, 0]  ok
summon_lift_bridge_a -> summon_lift_mid_a overlap 0.73  face moved    48  feet moved [54, 0]  ok
summon_lift_mid_a -> summon_lift1_a   overlap 0.91  face moved    13  feet moved [14, 0]  ok
  summon_lift1_a -> idle_a3          overlap 0.86  face moved     5  feet moved [48, 0]  ok
1 jump(s); overlays: art\p5\poses\seq_summon_hold_a_idle_a3.png
```

## 仍未解决的三步

1. summon_touch2_a → summon_hold_a：后脚123px，超过120px；普通收回的反向也因此JUMP。
2. summon_rip_a2 → summon_fling1_a：后脚137px，超过120px。
3. summon_fling1_a → summon_fling2_a：overlap0.48、脸96px，分别低于0.62/高于90px。

其余当前相邻步均ok。脚本不评价站位是否来回摆动或动作语义是否自然，因此量化通过也不代替用户验收。最初四张检查为完整3/普通2处JUMP；追加细分后部分原有跳变已缩小，但未全部清零。sequence_current.json提供当前顺序及未完成状态，供Claude协作处理，不能直接当作已验收的最终播放列表。

## 归一化与文件说明

新增12张全部登记为1.0/front，运行现有normalize_poses.py。既有22张final PNG因平台编码不同发生字节变化，已与HEAD逐一比较，RGBA尺寸和所有像素完全一致；本轮未改变它们画面。归一化脚本会按固定画布合成，下面如实记录新图被裁掉的alpha>128像素数，不把有裁切的帧写成完整无裁切：

- summon_lift1_a: 0
- summon_lift2_a: 0
- summon_fling_a: 0
- summon_settle_a: 0
- summon_lift_mid_a: 0
- summon_touch1_a: 1267
- summon_touch2_a: 2627
- summon_fling1_a: 1943
- summon_fling2_a: 0
- summon_lift_bridge_a: 0
- summon_touch_mid_a: 2758
- summon_contact_a: 2330

本轮未擅自扩统一画布、改锚点或替换已定稿关键帧。当前完整输出、原图、final版本、生成记录、归一化登记与报告一并推送。检查用网格/叠色图不提交。

art/cards另一会话11个修改原样保留且不提交/不stash/不还原；既有guard_a修改和其他无关未跟踪资源保留。未打开结城理mod图片、未读obs.json或密码、未启动游戏，不reset/rebase/强推。推送后通过ls-remote核实origin包含提交。
