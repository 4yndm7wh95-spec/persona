# knife_lower_h 收手过渡帧报告

- 开始前 fetch + merge --ff-only，同步到a5312fc7，使用远端最新 normalize 脚本与 knife_ready_b 归一化图。
- 内置 image_gen 只生成一次，无安全拒绝，未自行审图、重画或追加帧。
- 输入依次为 knife_spin_g 编辑底图、knife_ready_b 仅参考画面右侧手抬在胸前的起点、face_standard 脸标准。
- 提示词要求只改画面右臂到胸前与身侧之间：手在腰高、身体前侧，肘弯、手指放松；手里不画刀，其他部分和左侧腰旁半张开手保持底图。完整提示词、输入和原始输出见 knife_lower_h_generation.json。
- 原图1024×1536 RGBA，保留生成 alpha；清理人物外 alpha1～2 极低透明残点332像素。原图与生成记录已推送e8a1ccb9。
- knife_lower_h 完全照抄 knife_spin_g 的 (1.0, 'rear') 并断言一致；只归一化本张一次、合并 normalize.json，其他条目和 final 图不变，无额外缩放位移。
- 指定序列检查只跑一次，原样输出如下；不根据结果重画或追加帧，验收由用户和Claude决定。
- 未改 sparse 或其他Git配置，未编辑 build_rig_p5.py、p5mod 或游戏原文件。未启动游戏、使用Computer Use、打开结城理mod图片、读取obs.json或密码。art/cards等其他会话本地改动原样保留，未提交、stash、还原或删除。Git提示的已有图集和打包目录本地改动未处理。

## 归一化输出

```text
{"knife_lower_h": {"scale": 1.0, "offset": [265, 117], "anchor_foot": "rear"}}
```

## 序列检查原始输出

命令：python tools/p5/sequence_check.py knife_ready_b knife_lower_h knife_spin_g idle_a3

```text
   knife_ready_b -> knife_lower_h    overlap 0.79  face moved    24  feet moved [0, 35]  ok
   knife_lower_h -> knife_spin_g     overlap 0.94  face moved     2  feet moved [0, 2]  ok
    knife_spin_g -> idle_a3          overlap 0.80  face moved    57  feet moved [0, 19]  ok
0 jump(s); overlays: art\p5\poses\seq_knife_ready_b_idle_a3.png
```
