# Joker 召唤指挥 v3

已按 final/gun_aim_b、final/gun_arm_mid_h 作底图局部贴回，仅两只手区域改变；蒙版外改动像素均为 0。头、脸、躯干、腿、靴子和大衣保持底图。指挥帧去掉枪换成张开的红手套；中间帧右手本来就空着，保留原样，只改左手插裤兜。左手仅留窄腕部，手掌和手指藏入兜内。

生成：完整手部修改结果一次、插兜局部补图一次；无安全拒绝，无自动重画。完整生成结果没有整张交付，而是取手部局部按源画布坐标贴回。两个 source final 都是 1536×1632；依照原 normalize.json 的 offset 反推到 1024×1536 原图，未缩放身体。两张只运行一次归一化，比例 1.0、rear，输出偏移与源完全一致。normalize.json 合并保留旧条目。

完整提示词、输入和贴回方法见各自 generation.json。check_command_v3.png 含全身及手部对照。手部观感及接缝最终交用户验收，不将检查数字等同于美术定稿。

## 检查输出

```text
summon_command_a   head x1.000  body x1.000  boot height x1.000  boot width x1.000  feet moved [0, 0] px  ok
sheet: tmp\sts2_vfx_20261102\command_refs\check_summon_command_a.png | overall: ok
summon_command_mid_a head x1.000  body x1.000  boot height x1.000  boot width x1.000  feet moved [0, 0] px  ok
sheet: tmp\sts2_vfx_20261102\command_refs\check_summon_command_mid_a.png | overall: ok
{"summon_command_a": {"scale": 1.0, "offset": [343, 99], "anchor_foot": "rear"}, "summon_command_mid_a": {"scale": 1.0, "offset": [342, 99], "anchor_foot": "rear"}}
   summon_hold_a -> summon_command_mid_a overlap 0.33  face moved   189  feet moved [383, 7]  (allowed)
summon_command_mid_a -> summon_command_a overlap 0.87  face moved     3  feet moved [0, 0]  (allowed)
0 jump(s); overlays: sts2_tools\tmp\sts2_vfx_20261102\seq_summon_hold_a_summon_command_a.png
```

注意：hold→mid 的脸和后脚仍移动较大，按任务允许作为挥臂加插兜爆发动作放行；不是自然连续过渡已验证。未据此补帧。检查使用远端更新的纯 numpy 替代脚本，临时目录运行，不安装依赖，不覆盖仓库其他脚本。

独立推送分支 codex/p5-joker-command-v3，不 fetch、不 merge。后续任务为亚森 v7 拆层。
