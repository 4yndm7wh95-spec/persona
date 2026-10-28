# 两张半张开空手转刀手势报告

- 开始前已 fetch 到远端3c509731并正常 merge --no-edit（本地先前刀图提交需保留）；同步合并后推送4b2d4bef，包含已成功生成的独立刀图 knife_b 与远端双方内容。
- 两张均用内置 image_gen 各生成一次，无安全拒绝，未自行审图、重画或追加帧。
- knife_spin_g 第一输入 knife_set_a，knife_top_g 第一输入 slash_a10；第二输入均 gun_spin_g，仅参考手的半张开放松感，第三输入 face_standard。
- 仅要求改原持刀手为无物件的半张开放松手势：食指中指略伸且稍分开，无名指小指松弯，拇指松开；保持原手臂位置角度、袖口、身体、站位与另一只手。完整提示词和输入见各 generation.json。
- 两张均1024×1536 RGBA，保留生成 alpha。清理人物外极低 alpha1～2 残点：knife_spin_g 649像素，knife_top_g 269像素。
- 每张保存即提交推送：knife_spin_g a027360e，knife_top_g 7d514561。
- 参数逐项照抄底图并断言一致：knife_spin_g=(1.0, 'rear')，knife_top_g=(0.96, 'mid')；只对本组两张归一化一次，合并 normalize.json，其他条目与 final 图不变，无额外位移。
- 指定序列检查只跑一次，原样输出如下，不据此重画或追加帧。
- knife_spin_e 等旧图保留不删。未改 sparse 或其他 Git 配置；未编辑 build_rig_p5.py、p5mod 或游戏原文件。未启动游戏、使用 Computer Use、打开结城理 mod 图片、读取 obs.json 或密码。art/cards 等其他会话本地改动原样保留，未提交、stash、还原或删除。

## 归一化输出

```text
{"knife_spin_g": {"scale": 1.0, "offset": [266, 116], "anchor_foot": "rear"}, "knife_top_g": {"scale": 0.96, "offset": [298, 216], "anchor_foot": "mid"}}
```

## 序列检查原始输出

命令：python tools/p5/sequence_check.py idle_a3 knife_spin_g knife_ready_b slash_a10 knife_top_g slash_follow_a

```text
         idle_a3 -> knife_spin_g     overlap 0.80  face moved    57  feet moved [0, 19]  ok
    knife_spin_g -> knife_ready_b    overlap 0.67  face moved    73  feet moved [1, 52]  ok
   knife_ready_b -> slash_a10        overlap 0.54  face moved   110  feet moved [39, 94]  JUMP - add a frame
       slash_a10 -> knife_top_g      overlap 0.98  face moved     1  feet moved [0, 2]  ok
     knife_top_g -> slash_follow_a   overlap 0.85  face moved     2  feet moved [0, 2]  ok
1 jump(s); overlays: art\p5\poses\seq_idle_a3_slash_follow_a.png
```
