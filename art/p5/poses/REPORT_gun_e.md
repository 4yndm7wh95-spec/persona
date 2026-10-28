# 开枪组第五轮：照片手势与四指入兜

- 使用内置 image_gen，两张各生成一次，无安全拒绝，未自行审图、重画或追加帧。
- gun_spin_e 第一输入 gun_aim_b，第二输入用户手照 spin_hand_ref.jpg，第三输入 face_standard。要求臂展、袖口和其他部分保持底图，只去枪并改手势。
- pocket_in_e 第一输入 idle_a3，第二输入 gun_aim_b 仅参考插兜最终样式，第三输入 face_standard；要求四指全部入裤兜到指根，右侧手垂下。
- 完整提示词、输入、原始输出路径见对应 generation.json。原图均 1024×1536 RGBA；保留生成 alpha，清理极低 alpha 1～2 残点：gun_spin_e 350 像素，pocket_in_e 271 像素。
- 保存后逐张推送：gun_spin_e 596d330d；pocket_in_e ab34612a。
- 两张登记 (1.0, 'rear')，只对这两张归一化一次并合并 normalize.json；其他条目与 final 图保持不变，无额外缩放位移。
- 两项检查各一次，以下原样记录；不根据结果重画，由用户和 Claude 验收。
- 旧图保留不删。未修改 build_rig_p5.py、p5mod 或游戏原文件；未启动游戏或使用 Computer Use，未打开结城理 mod 图片，未读取 obs.json 或密码。art/cards 等其他会话本地改动原样保留，未提交、stash、还原或删除。

## 同步与本地工作区处理

fetch 已取得远端提交，快进合并因缺失对象下载断线。停止重复整次同步后，从 GitHub raw 单独下载缺失文件，逐一验证 Git blob SHA-1，git hash-object -w 补入对象库，随后成功快进至 f294ab86。spin_hand_ref.jpg 的对象为 e9d53ca8e97d87f29778b2267d6362c17c2f7ac3。

按用户新增授权，只追加排除 art/p5/rig/ 与 p5mod/pck_stage/ 两个目录，其他已有 sparse 匹配规则保留。为支持精确否定规则，使用非 cone 匹配格式；未改其他 Git 配置。此后单个文件下载最多两次；生图所需参考、normalize.json 与脚本最新即可开始，不再等待无关大文件。

## 归一化输出

```text
{"gun_spin_e": {"scale": 1.0, "offset": [343, 99], "anchor_foot": "rear"}, "pocket_in_e": {"scale": 1.0, "offset": [256, 96], "anchor_foot": "rear"}}
```

## 比例检查原始输出

命令：python tools/p5/pose_consistency.py idle_a3 pocket_in_e gun_spin_e gun_aim_b

```text
pocket_in_e        head x1.004  body x1.000  boot height x1.003  boot width x1.019  feet moved [0, 0] px  ok
gun_spin_e         head x0.991  body x0.999  boot height x1.019  boot width x1.038  feet moved [87, 80] px  ok
gun_aim_b          head x0.991  body x0.998  boot height x1.003  boot width x1.033  feet moved [87, 80] px  ok
sheet: art\p5\poses\check_pocket_in_e.png | overall: ok
```

## 序列检查原始输出

命令：python tools/p5/sequence_check.py idle_a3 pocket_in_e gun_spin_e gun_aim_b gun_recoil_b gun_spin_e pocket_in_e idle_a3

```text
         idle_a3 -> pocket_in_e      overlap 0.96  face moved     1  feet moved [0, 0]  ok
     pocket_in_e -> gun_spin_e       overlap 0.82  face moved     5  feet moved [0, 6]  ok
      gun_spin_e -> gun_aim_b        overlap 0.99  face moved     1  feet moved [0, 0]  ok
       gun_aim_b -> gun_recoil_b     overlap 0.86  face moved    10  feet moved [0, 6]  ok
    gun_recoil_b -> gun_spin_e       overlap 0.86  face moved    10  feet moved [0, 5]  ok
      gun_spin_e -> pocket_in_e      overlap 0.82  face moved     5  feet moved [0, 6]  ok
     pocket_in_e -> idle_a3          overlap 0.96  face moved     1  feet moved [0, 0]  ok
0 jump(s); overlays: art\p5\poses\seq_idle_a3_idle_a3.png
```
