# gun_spin_f 食指修订报告

- 开始前 fetch + merge --ff-only，基线 ae83bf80，远端已同步。
- 内置 image_gen 只生成一次，无安全拒绝，未自行审图、重画或追加帧。
- 输入依次为 gun_spin_e 编辑底图、spin_hand_ref.jpg 用户手照、gun_aim_b 的手指尺寸与画法参考。
- 提示词仅要求修画面右侧食指：正常粗细、圆指尖、两道关节弯痕、自然微弯，朝右前方略向观众；其他部分保持底图。完整提示词和输入路径见 gun_spin_f_generation.json。
- 原图 1024×1536 RGBA，保留生成 alpha，清理极低 alpha 1～2 残点 1426 像素；原图和生成记录已提交推送 0b4c09f5。
- 登记 (1.0, 'rear')，只归一化本张一次并合并 normalize.json，其他条目和 final 图未改，无额外缩放位移。
- 只跑本次指定的序列检查一次，输出如下；不据此重画。未运行未要求的比例检查。
- gun_spin_e 等旧图保留不删。未改 sparse 设置或其他 Git 配置；沿用已授权的两个大目录排除。未改 build_rig_p5.py、p5mod 或游戏原文件，未启动游戏、使用 Computer Use、打开结城理 mod 图片、读取 obs.json 或密码。art/cards 等其他会话改动原样保留，未提交、stash、还原或删除。

## 归一化输出

```text
{"gun_spin_f": {"scale": 1.0, "offset": [343, 99], "anchor_foot": "rear"}}
```

## 序列检查原始输出

命令：python tools/p5/sequence_check.py idle_a3 pocket_in_e gun_spin_f gun_aim_b gun_recoil_b gun_spin_f pocket_in_e idle_a3

```text
         idle_a3 -> pocket_in_e      overlap 0.96  face moved     1  feet moved [0, 0]  ok
     pocket_in_e -> gun_spin_f       overlap 0.82  face moved     4  feet moved [0, 7]  ok
      gun_spin_f -> gun_aim_b        overlap 0.98  face moved     1  feet moved [0, 0]  ok
       gun_aim_b -> gun_recoil_b     overlap 0.86  face moved    10  feet moved [0, 6]  ok
    gun_recoil_b -> gun_spin_f       overlap 0.86  face moved     9  feet moved [0, 6]  ok
      gun_spin_f -> pocket_in_e      overlap 0.82  face moved     4  feet moved [0, 7]  ok
     pocket_in_e -> idle_a3          overlap 0.96  face moved     1  feet moved [0, 0]  ok
0 jump(s); overlays: art\p5\poses\seq_idle_a3_idle_a3.png
```
