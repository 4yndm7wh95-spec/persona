# 拔枪脚位修订及补帧（2026-10-26）

开始前 fetch + merge 成功，快进到 a89bd21c，读取最新 README 与 sequence_check.py。新标准：轮廓重合度<0.62、脸移动>90px、任一只脚一步移动>120px，任一超标即 JUMP。未修改阈值或添加豁免。

## 交付

- gun_draw_a2：以 gun_draw_a 为底图图像编辑，提示词限制只把画面右边前脚外挪、前腿自然连接；身体、手、枪、头与后脚保持底图。原 gun_draw_a 不覆盖、不删除。
- 生成的脚位没有精确落在理想中点；初检 gun_draw_a2→gun_raise_a2 仍前脚165px、overlap0.59，于是按规则补 gun_lift_mid_a。
- 再检 gun_lift_mid_a→gun_raise_a2 仍前脚159px、overlap0.61，继续补 gun_lift_near_a。最终相邻脚位均≤120px。
- 两张追加帧以已定稿 gun_raise_a2 为底图，参考端点或明确中间脚位/手臂位置；中间手持同一把道具手枪由腰侧抬到胸前，再抬至脸旁，左手空着。
- 三张均使用内置 image_gen，各一次生成，无安全拒绝。1024×1536 RGBA真透明底；只清理主体外 alpha1～2残点，不手工缩放。逐帧视觉验收交给用户，未做额外审图。
- 完整提示词、输入与清理统计见对应 *_generation.json。

## 比例检查输出

`python tools/p5/pose_consistency.py gun_draw_a gun_draw_a2`

```text
gun_draw_a2        head x1.005  body x1.000  boot height x1.018  boot width x1.022  feet moved [1, 176] px  ok
sheet: art\p5\poses\check_gun_draw_a2.png | overall: ok
```

`python tools/p5/pose_consistency.py gun_raise_a2 gun_lift_mid_a`

```text
gun_lift_mid_a     head x0.995  body x1.016  boot height x0.986  boot width x1.060  feet moved [34, 201] px  ok
sheet: art\p5\poses\check_gun_lift_mid_a.png | overall: ok
```

`python tools/p5/pose_consistency.py gun_raise_a2 gun_lift_near_a`

```text
gun_lift_near_a    head x0.995  body x1.001  boot height x1.012  boot width x1.030  feet moved [1, 61] px  ok
sheet: art\p5\poses\check_gun_lift_near_a.png | overall: ok
```

## 首次与追加一张后的序列检查

```text
         idle_a3 -> gun_step_a       overlap 0.67  face moved    36  feet moved [0, 61]  ok
      gun_step_a -> gun_draw_a2      overlap 0.69  face moved    61  feet moved [0, 46]  ok
     gun_draw_a2 -> gun_raise_a2     overlap 0.59  face moved    62  feet moved [1, 165]  JUMP - add a frame
    gun_raise_a2 -> gun_a2           overlap 0.89  face moved     3  feet moved [1, 2]  ok
1 jump(s); overlays: art\p5\poses\seq_idle_a3_gun_a2.png
```

```text
         idle_a3 -> gun_step_a       overlap 0.67  face moved    36  feet moved [0, 61]  ok
      gun_step_a -> gun_draw_a2      overlap 0.69  face moved    61  feet moved [0, 46]  ok
     gun_draw_a2 -> gun_lift_mid_a   overlap 0.86  face moved    23  feet moved [0, 15]  ok
  gun_lift_mid_a -> gun_raise_a2     overlap 0.61  face moved    47  feet moved [1, 159]  JUMP - add a frame
    gun_raise_a2 -> gun_a2           overlap 0.89  face moved     3  feet moved [1, 2]  ok
1 jump(s); overlays: art\p5\poses\seq_idle_a3_gun_a2.png
```

## 最终检查

`python tools/p5/sequence_check.py idle_a3 gun_step_a gun_draw_a2 gun_lift_mid_a gun_lift_near_a gun_raise_a2 gun_a2`

```text
         idle_a3 -> gun_step_a       overlap 0.67  face moved    36  feet moved [0, 61]  ok
      gun_step_a -> gun_draw_a2      overlap 0.69  face moved    61  feet moved [0, 46]  ok
     gun_draw_a2 -> gun_lift_mid_a   overlap 0.86  face moved    23  feet moved [0, 15]  ok
  gun_lift_mid_a -> gun_lift_near_a  overlap 0.67  face moved    47  feet moved [0, 104]  ok
 gun_lift_near_a -> gun_raise_a2     overlap 0.79  face moved    38  feet moved [1, 55]  ok
    gun_raise_a2 -> gun_a2           overlap 0.89  face moved     3  feet moved [1, 2]  ok
0 jump(s); overlays: art\p5\poses\seq_idle_a3_gun_a2.png
```

`python tools/p5/sequence_check.py idle_a3 gun_step_a gun_draw_a2 gun_lift_mid_a gun_lift_near_a gun_raise_a2 gun_a2 gun_recoil_a6 gun_a2 gun_raise_a2 gun_lift_near_a gun_lift_mid_a gun_draw_a2 gun_step_a idle_a3`

```text
         idle_a3 -> gun_step_a       overlap 0.67  face moved    36  feet moved [0, 61]  ok
      gun_step_a -> gun_draw_a2      overlap 0.69  face moved    61  feet moved [0, 46]  ok
     gun_draw_a2 -> gun_lift_mid_a   overlap 0.86  face moved    23  feet moved [0, 15]  ok
  gun_lift_mid_a -> gun_lift_near_a  overlap 0.67  face moved    47  feet moved [0, 104]  ok
 gun_lift_near_a -> gun_raise_a2     overlap 0.79  face moved    38  feet moved [1, 55]  ok
    gun_raise_a2 -> gun_a2           overlap 0.89  face moved     3  feet moved [1, 2]  ok
          gun_a2 -> gun_recoil_a6    overlap 0.68  face moved    42  feet moved [0, 15]  ok
   gun_recoil_a6 -> gun_a2           overlap 0.68  face moved    42  feet moved [0, 15]  ok
          gun_a2 -> gun_raise_a2     overlap 0.89  face moved     3  feet moved [1, 2]  ok
    gun_raise_a2 -> gun_lift_near_a  overlap 0.79  face moved    38  feet moved [1, 55]  ok
 gun_lift_near_a -> gun_lift_mid_a   overlap 0.67  face moved    47  feet moved [0, 104]  ok
  gun_lift_mid_a -> gun_draw_a2      overlap 0.86  face moved    23  feet moved [0, 15]  ok
     gun_draw_a2 -> gun_step_a       overlap 0.69  face moved    61  feet moved [0, 46]  ok
      gun_step_a -> idle_a3          overlap 0.67  face moved    36  feet moved [0, 61]  ok
0 jump(s); overlays: art\p5\poses\seq_idle_a3_idle_a3.png
```

前向与完整回待机序列均0 jump(s)。运行现有 normalize_poses.py，将 POSES 中 gun_draw_a 替换为 gun_draw_a2（1.0、rear），追加 gun_lift_mid_a、gun_lift_near_a（均1.0、rear）。未改其他姿势的比例与锚点。旧gun_draw_a及final旧文件仅留历史，不再在新序列使用。报告历史里的旧0-jump结果适用当时不检查脚位的规则，以本报告为准。

art/cards 的11个既有修改原样保留，未暂存提交；既有本地 guard_a 修改与无关未跟踪文件也保留。未打开结城理mod图片，未读取obs.json或密码，不启动游戏，不reset/rebase/强推。提交本次原图、final版本、生成记录、归一化登记及报告；推送后用ls-remote确认origin包含提交。
