# 开枪组第四轮：朝镜头食指与插兜过渡

- 开始前 fetch + merge --ff-only，快进到 50e6383d。
- 内置 image_gen，三张各生成一次，无安全拒绝，未自行审图、重画或追加帧。
- 三张第一输入均为 idle_a3 编辑底图，第三输入为 face_standard；第二输入分别 gun_flick_c、gun_twirl_out_c、gun_aim_b，仅参考手臂或插兜。
- 两张转枪手势要求食指轴朝镜头、透视缩短，指尖无遮挡；人物手中不画枪。pocket_in_d 要求左侧指尖刚入裤兜、右侧空手低位。
- 完整提示词、输入及原始输出路径见各图 generation.json。原图 1024×1536 RGBA，保留 alpha；清理极低 alpha 1～2 残点：gun_flick_d 2071 像素、gun_twirl_out_d 1516 像素、pocket_in_d 355 像素。
- 每张保存即提交推送：gun_flick_d 057e1902；gun_twirl_out_d 5b107e91；pocket_in_d 7c31ab4b。
- 三张登记 (1.0, 'rear')，只对本组三张归一化一次并合并 normalize.json；其他登记与 final 图未改，无额外缩放或位移。
- 两项检查各一次，原样输出如下，判断交用户和 Claude；不根据结果重画。
- 上轮图保留不删。未改 build_rig_p5.py 和 p5mod，未启动游戏、使用 Computer Use 或改游戏原文件。未打开结城理 mod 图片，未读取 obs.json 或密码。art/cards 等其他会话本地改动原样保留，未提交、stash、还原或删除。

## 归一化输出

```text
{"gun_flick_d": {"scale": 1.0, "offset": [311, 97], "anchor_foot": "rear"}, "gun_twirl_out_d": {"scale": 1.0, "offset": [278, 96], "anchor_foot": "rear"}, "pocket_in_d": {"scale": 1.0, "offset": [256, 95], "anchor_foot": "rear"}}
```

## 比例检查原始输出

命令：python tools/p5/pose_consistency.py idle_a3 pocket_in_d gun_flick_d gun_aim_b gun_recoil_b gun_twirl_out_d

```text
pocket_in_d        head x0.969  body x1.005  boot height x1.009  boot width x1.046  feet moved [1, 0] px  ok
gun_flick_d        head x1.000  body x0.998  boot height x1.025  boot width x1.040  feet moved [55, 29] px  ok
gun_aim_b          head x0.991  body x0.998  boot height x1.003  boot width x1.033  feet moved [87, 80] px  ok
gun_recoil_b       head x1.009  body x0.998  boot height x1.003  boot width x1.007  feet moved [2, 1] px  ok
gun_twirl_out_d    head x1.000  body x0.999  boot height x1.003  boot width x1.021  feet moved [22, 1] px  ok
sheet: art\p5\poses\check_pocket_in_d.png | overall: ok
```

## 序列检查原始输出

命令：python tools/p5/sequence_check.py idle_a3 pocket_in_d gun_flick_d gun_aim_b gun_recoil_b gun_twirl_out_d pocket_in_d idle_a3

```text
         idle_a3 -> pocket_in_d      overlap 0.93  face moved     5  feet moved [0, 1]  ok
     pocket_in_d -> gun_flick_d      overlap 0.83  face moved     4  feet moved [0, 25]  ok
     gun_flick_d -> gun_aim_b        overlap 0.89  face moved     1  feet moved [0, 18]  ok
       gun_aim_b -> gun_recoil_b     overlap 0.86  face moved    10  feet moved [0, 6]  ok
    gun_recoil_b -> gun_twirl_out_d  overlap 0.85  face moved     8  feet moved [0, 20]  ok
 gun_twirl_out_d -> pocket_in_d      overlap 0.82  face moved     4  feet moved [0, 21]  ok
     pocket_in_d -> idle_a3          overlap 0.93  face moved     5  feet moved [0, 1]  ok
0 jump(s); overlays: art\p5\poses\seq_idle_a3_idle_a3.png
```
