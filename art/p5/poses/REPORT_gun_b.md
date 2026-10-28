# 开枪组：凭空出枪与转枪生成报告

- 任务变更前已快进同步到 a9602547，变更后再次 fetch + merge --ff-only，远端已同步。
- 原 gun_step_b、gun_draw_b 已推送并保留，未覆盖、不删除；它们本次一并登记。
- gun_lift_mid_b 的生成调用被用户中断，没有返回成图，不重试；gun_lift_near_b 未生成。
- 新方案六张均用内置 image_gen 各生成一次，每张保存后即提交推送；没有安全拒绝，没有自行审图、重画或追加帧。
- 所有新图以 idle_a3 为第一输入，脸以 face_standard 为准；左手插兜参考 gun_step_b。三张常规持枪帧另用对应旧帧仅参考右臂和枪的位置。完整提示词、输入和原始输出见各图 generation.json。
- 原图均为 1024×1536 RGBA，保留生成 alpha，仅清理人物外 alpha 1～2 的极低透明残点。
- 8 张均登记 (1.0, 'rear')，只对这 8 张归一化一次；合并 normalize.json，未改其他登记或 final 图，无额外缩放位移。
- 比例与序列检查各运行一次，以下原样记录；不根据结果重画，验收交用户和 Claude。
- 未改 build_rig_p5.py 或 p5mod。未启动游戏或使用 Computer Use，未改游戏原文件。未打开结城理 mod 图片，未读取 obs.json 或密码。art/cards 等其他会话本地改动原样保留，未提交、stash、还原或删除。

## 每张推送提交

| 图 | 提交 |
| --- | --- |
| gun_step_b | 074c4047 |
| gun_draw_b | 8c731334 |
| gun_twirl_in_b | 2628ad9a |
| gun_twirl_mid_b | 6f0c403b |
| gun_aim_b | 1d78038b |
| gun_recoil_b | daa8f61c |
| gun_raise_b | ab5bbbdc |
| gun_twirl_out_b | 1bbf0cba |

## alpha 残点清理

| 图 | 清理像素数 |
| --- | --- |
| gun_step_b | 425 |
| gun_draw_b | 1999 |
| gun_twirl_in_b | 925 |
| gun_twirl_mid_b | 158 |
| gun_aim_b | 319 |
| gun_recoil_b | 260 |
| gun_raise_b | 346 |
| gun_twirl_out_b | 314 |

## 归一化输出

```text
{"gun_step_b": {"scale": 1.0, "offset": [251, 94], "anchor_foot": "rear"}, "gun_draw_b": {"scale": 1.0, "offset": [256, 96], "anchor_foot": "rear"}, "gun_twirl_in_b": {"scale": 1.0, "offset": [254, 95], "anchor_foot": "rear"}, "gun_twirl_mid_b": {"scale": 1.0, "offset": [256, 95], "anchor_foot": "rear"}, "gun_aim_b": {"scale": 1.0, "offset": [343, 99], "anchor_foot": "rear"}, "gun_recoil_b": {"scale": 1.0, "offset": [255, 94], "anchor_foot": "rear"}, "gun_raise_b": {"scale": 1.0, "offset": [255, 94], "anchor_foot": "rear"}, "gun_twirl_out_b": {"scale": 1.0, "offset": [255, 96], "anchor_foot": "rear"}}
```

## 比例检查原始输出

命令：python tools/p5/pose_consistency.py idle_a3 gun_twirl_in_b gun_twirl_mid_b gun_aim_b gun_recoil_b gun_raise_b gun_twirl_out_b

```text
gun_twirl_in_b     head x1.018  body x0.998  boot height x1.003  boot width x1.012  feet moved [2, 2] px  ok
gun_twirl_mid_b    head x1.009  body x0.999  boot height x1.007  boot width x1.019  feet moved [1, 1] px  ok
gun_aim_b          head x0.991  body x0.998  boot height x1.003  boot width x1.033  feet moved [87, 80] px  ok
gun_recoil_b       head x1.009  body x0.998  boot height x1.003  boot width x1.007  feet moved [2, 1] px  ok
gun_raise_b        head x1.013  body x1.000  boot height x0.993  boot width x1.007  feet moved [2, 1] px  ok
gun_twirl_out_b    head x0.991  body x1.001  boot height x0.990  boot width x1.012  feet moved [0, 1] px  ok
sheet: art\p5\poses\check_gun_twirl_in_b.png | overall: ok
```

## 序列检查原始输出

命令：python tools/p5/sequence_check.py idle_a3 gun_twirl_in_b gun_twirl_mid_b gun_aim_b gun_recoil_b gun_raise_b gun_twirl_out_b idle_a3

```text
         idle_a3 -> gun_twirl_in_b   overlap 0.85  face moved     4  feet moved [0, 1]  ok
  gun_twirl_in_b -> gun_twirl_mid_b  overlap 0.89  face moved     3  feet moved [0, 1]  ok
 gun_twirl_mid_b -> gun_aim_b        overlap 0.81  face moved     6  feet moved [0, 7]  ok
       gun_aim_b -> gun_recoil_b     overlap 0.86  face moved    10  feet moved [0, 6]  ok
    gun_recoil_b -> gun_raise_b      overlap 0.84  face moved     4  feet moved [0, 0]  ok
     gun_raise_b -> gun_twirl_out_b  overlap 0.92  face moved     4  feet moved [0, 2]  ok
 gun_twirl_out_b -> idle_a3          overlap 0.85  face moved     2  feet moved [0, 0]  ok
0 jump(s); overlays: art\p5\poses\seq_idle_a3_idle_a3.png
```
