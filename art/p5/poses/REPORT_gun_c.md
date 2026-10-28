# 开枪组第三轮：独立手枪与空手手势

- 开始前 fetch + merge --ff-only，基线 6660f19d，远端已同步。
- 使用内置 image_gen，pistol_b、gun_flick_c、gun_twirl_out_c 每张只生成一次，未自行审图或重画，无安全拒绝。
- pistol_b 以 gun_aim_b 中的手枪为造型参考；原始生成画布 1254×1254，保存时缩放为要求的 1024×1024 RGBA。未登记、不归一化。
- 两张人物图以 idle_a3 为编辑底图，gun_aim_b 仅参考画面右侧动作手、左侧裤兜手，face_standard 为脸参考。人物帧中要求不画枪，枪由 Claude 骨骼动画另挂。
- 完整提示词、输入路径与原始输出见对应 generation.json；清理极低 alpha 1～2 残点：pistol_b 139 像素，gun_flick_c 2725 像素，gun_twirl_out_c 181 像素。
- 三张均保存后立即提交推送：pistol_b 064c9954；gun_flick_c 46652db1；gun_twirl_out_c f442336a。
- 两张人物图登记 (1.0, 'rear')，只对这两张归一化一次，合并 normalize.json；其他登记和 final 图保持不变，没有额外缩放位移。
- gun_aim_b 和 gun_recoil_b 保留；上轮不再使用的转枪图保留不删。本轮没有动画编排改动。
- 检查各运行一次，原样输出如下，不根据结果重画或追加帧。
- 未改 tools/p5/build_rig_p5.py 或 p5mod；未使用 Computer Use、未启动游戏、未改游戏原文件、未打开结城理 mod 图片、未读取 obs.json 或密码。art/cards 等其他会话改动原样保留，未提交、stash、还原或删除。

## 归一化输出

```text
{"gun_flick_c": {"scale": 1.0, "offset": [314, 98], "anchor_foot": "rear"}, "gun_twirl_out_c": {"scale": 1.0, "offset": [326, 97], "anchor_foot": "rear"}}
```

## 比例检查原始输出

命令：python tools/p5/pose_consistency.py idle_a3 gun_flick_c gun_aim_b gun_recoil_b gun_twirl_out_c

```text
gun_flick_c        head x0.991  body x1.000  boot height x1.006  boot width x1.021  feet moved [57, 37] px  ok
gun_aim_b          head x0.991  body x0.998  boot height x1.003  boot width x1.033  feet moved [87, 80] px  ok
gun_recoil_b       head x1.009  body x0.998  boot height x1.003  boot width x1.007  feet moved [2, 1] px  ok
gun_twirl_out_c    head x1.004  body x0.999  boot height x1.006  boot width x1.028  feet moved [69, 47] px  ok
sheet: art\p5\poses\check_gun_flick_c.png | overall: ok
```

## 序列检查原始输出

命令：python tools/p5/sequence_check.py idle_a3 gun_flick_c gun_aim_b gun_recoil_b gun_twirl_out_c idle_a3

```text
         idle_a3 -> gun_flick_c      overlap 0.79  face moved     6  feet moved [0, 20]  ok
     gun_flick_c -> gun_aim_b        overlap 0.88  face moved     3  feet moved [0, 13]  ok
       gun_aim_b -> gun_recoil_b     overlap 0.86  face moved    10  feet moved [0, 6]  ok
    gun_recoil_b -> gun_twirl_out_c  overlap 0.84  face moved    14  feet moved [0, 21]  ok
 gun_twirl_out_c -> idle_a3          overlap 0.77  face moved     9  feet moved [0, 22]  ok
0 jump(s); overlays: art\p5\poses\seq_idle_a3_idle_a3.png
```
