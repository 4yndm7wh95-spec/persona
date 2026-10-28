# gun_arm_mid_h 出收枪共用中间帧报告

- 开始前 fetch 已取到远端 c865babf，快进因缺失 gun_spin_g 对象下载断线；改用 GitHub raw 单独下载本次变更文件、逐一校验 blob 哈希并 hash-object -w 补齐，成功快进至 c865babf。没有反复重试整次合并。
- 内置 image_gen 只生成一次，无安全拒绝，未自行审图、重画或追加帧。
- 输入依次：gun_spin_g 编辑底图、pocket_in_e 仅参考手臂下垂起点、face_standard 脸标准。要求只改画面右臂为向右下约40～45度、肘微弯的空手中间姿势，保持身体、站位与左侧插兜手。完整提示词和输入见 gun_arm_mid_h_generation.json。
- 原图1024×1536 RGBA，保留生成 alpha，清理人物外极低 alpha1～2 残点660像素。原图与生成记录已提交推送4ccf3ffd。
- 登记 (1.0, 'rear')，只归一化本张一次，合并 normalize.json；不改其他条目和 final 图，无额外缩放位移。
- 只运行指定序列检查一次，输出原样记录如下；不根据结果重画或追加帧。未额外跑比例检查。
- 未改 sparse 或其他 Git 配置。未编辑 build_rig_p5.py、p5mod 或游戏原文件。未启动游戏、使用 Computer Use、打开结城理 mod 图片、读取 obs.json 或密码。art/cards 等其他会话本地改动原样保留，未提交、stash、还原或删除。

## 归一化输出

```text
{"gun_arm_mid_h": {"scale": 1.0, "offset": [342, 99], "anchor_foot": "rear"}}
```

## 序列检查原始输出

命令：python tools/p5/sequence_check.py idle_a3 pocket_in_e gun_arm_mid_h gun_spin_g gun_aim_b gun_recoil_b gun_spin_g gun_arm_mid_h pocket_in_e idle_a3

```text
         idle_a3 -> pocket_in_e      overlap 0.96  face moved     1  feet moved [0, 0]  ok
     pocket_in_e -> gun_arm_mid_h    overlap 0.82  face moved     3  feet moved [0, 7]  ok
   gun_arm_mid_h -> gun_spin_g       overlap 0.88  face moved     2  feet moved [0, 0]  ok
      gun_spin_g -> gun_aim_b        overlap 0.98  face moved     1  feet moved [0, 0]  ok
       gun_aim_b -> gun_recoil_b     overlap 0.86  face moved    10  feet moved [0, 6]  ok
    gun_recoil_b -> gun_spin_g       overlap 0.86  face moved     9  feet moved [0, 6]  ok
      gun_spin_g -> gun_arm_mid_h    overlap 0.88  face moved     2  feet moved [0, 0]  ok
   gun_arm_mid_h -> pocket_in_e      overlap 0.82  face moved     3  feet moved [0, 7]  ok
     pocket_in_e -> idle_a3          overlap 0.96  face moved     1  feet moved [0, 0]  ok
0 jump(s); overlays: art\p5\poses\seq_idle_a3_idle_a3.png
```
