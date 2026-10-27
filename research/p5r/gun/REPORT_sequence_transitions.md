# Joker 动画连续性补帧报告（2026-10-26）

## 结果

开始前 fetch + merge 成功，快进到 `9fd8023c`，已完整读取 docs/codex/README.md 新增连续性规则和 tools/p5/sequence_check.py。使用内置 image_gen，每帧一次生成，无安全拒绝；不做额外视觉审图，交用户验收。

本次五张原图均为 1024×1536 RGBA，游戏版本由现有归一化脚本生成到 final/，画布为 1152×1536。所有新登记比例均为 1.0，没有手工缩放凑检查数。只清理主体外 alpha=1～2 的透明残点，不改主体颜色像素。

| 新图 | 编辑底图 | 过渡位置 | 对齐 |
|---|---|---|---|
| gun_step_a | idle_a3 | 待机→拔枪起手 | rear |
| knife_draw_a | idle_a3 | 待机→取刀 | rear |
| slash_recover_a | slash_follow_a | 收刀回待机 | mid |
| knife_set_a | knife_ready_a | knife_draw_a→knife_ready_a | rear |
| knife_return_a | slash_follow_a | slash_follow_a→slash_recover_a | mid |

前三张是用户指定，后两张是第一次序列检查检出的 JUMP 所需补帧。每张完整提示词、输入、透明残点清理数量与比例检查输出见对应 `<名称>_generation.json`。

## 比例检查

### gun_step_a

`python tools/p5/pose_consistency.py idle_a3 gun_step_a`

```text
gun_step_a         head x0.973  body x1.001  boot height x0.990  boot width x1.003  feet moved [49, 14] px  ok
sheet: art\p5\poses\check_gun_step_a.png | overall: ok
```

### knife_draw_a

`python tools/p5/pose_consistency.py idle_a3 knife_draw_a`

```text
knife_draw_a       head x1.040  body x0.980  boot height x1.034  boot width x1.037  feet moved [23, 26] px  ok
sheet: art\p5\poses\check_knife_draw_a.png | overall: ok
```

### slash_recover_a

`python tools/p5/pose_consistency.py slash_follow_a slash_recover_a`

```text
slash_recover_a    head x0.996  body x1.090 (check)  boot height x1.071  boot width x1.035  feet moved [124, 62] px  ok
sheet: art\p5\poses\check_slash_recover_a.png | overall: ok
```

### knife_set_a

`python tools/p5/pose_consistency.py knife_ready_a knife_set_a`

```text
knife_set_a        head x0.979  body x1.017  boot height x1.008  boot width x1.057  feet moved [7, 27] px  ok
sheet: art\p5\poses\check_knife_set_a.png | overall: ok
```

### knife_return_a

`python tools/p5/pose_consistency.py slash_follow_a knife_return_a`

```text
knife_return_a     head x0.987  body x1.022  boot height x1.075  boot width x1.070  feet moved [53, 21] px  ok
sheet: art\p5\poses\check_knife_return_a.png | overall: ok
```

五张均为 overall: ok。slash_recover_a 的 body x1.090 被脚本提示 (check)：该帧是从弯腿姿势向站立恢复，身体伸展变化保留记录，头部检查 x0.996 通过。脚部尺寸/位移是脚本参考值，未另设标准、未修改检查阈值。没有运行游戏。

## 首次序列检查

指定三张生成并归一化后，开枪为 0 jump(s)，小刀为 2 jump(s)：取刀到预备帧脸移动 92px、收刀起段脸移动169px且重合度0.56。完整输出如下。

```text
         idle_a3 -> gun_step_a       overlap 0.67  face moved    36  feet moved [0, 61]  ok
      gun_step_a -> gun_draw_a       overlap 0.64  face moved    60  feet moved [0, 221]  ok
      gun_draw_a -> gun_raise_a2     overlap 0.64  face moved    63  feet moved [1, 38]  ok
    gun_raise_a2 -> gun_a2           overlap 0.89  face moved     3  feet moved [1, 2]  ok
          gun_a2 -> gun_recoil_a6    overlap 0.68  face moved    42  feet moved [0, 15]  ok
   gun_recoil_a6 -> gun_a2           overlap 0.68  face moved    42  feet moved [0, 15]  ok
          gun_a2 -> gun_raise_a2     overlap 0.89  face moved     3  feet moved [1, 2]  ok
    gun_raise_a2 -> gun_draw_a       overlap 0.64  face moved    63  feet moved [1, 38]  ok
      gun_draw_a -> gun_step_a       overlap 0.64  face moved    60  feet moved [0, 221]  ok
      gun_step_a -> idle_a3          overlap 0.67  face moved    36  feet moved [0, 61]  ok
0 jump(s); overlays: art\p5\poses\seq_idle_a3_idle_a3.png
```

```text
         idle_a3 -> knife_draw_a     overlap 0.85  face moved    32  feet moved [0, 10]  ok
    knife_draw_a -> knife_ready_a    overlap 0.71  face moved    92  feet moved [0, 58]  JUMP - add a frame
   knife_ready_a -> slash_smear_a3   overlap 0.54  face moved    83  feet moved [46, 99]  (allowed)
  slash_smear_a3 -> slash_a3         overlap 0.71  face moved    34  feet moved [5, 5]  (allowed)
        slash_a3 -> slash_follow_a   overlap 0.91  face moved     1  feet moved [0, 1]  ok
  slash_follow_a -> slash_recover_a  overlap 0.56  face moved   169  feet moved [77, 78]  JUMP - add a frame
 slash_recover_a -> idle_a3          overlap 0.69  face moved    57  feet moved [36, 37]  ok
2 jump(s); overlays: art\p5\poses\seq_idle_a3_idle_a3.png
```

## 补帧后的最终序列检查

`python tools/p5/sequence_check.py idle_a3 gun_step_a gun_draw_a gun_raise_a2 gun_a2 gun_recoil_a6 gun_a2 gun_raise_a2 gun_draw_a gun_step_a idle_a3`

```text
         idle_a3 -> gun_step_a       overlap 0.67  face moved    36  feet moved [0, 61]  ok
      gun_step_a -> gun_draw_a       overlap 0.64  face moved    60  feet moved [0, 221]  ok
      gun_draw_a -> gun_raise_a2     overlap 0.64  face moved    63  feet moved [1, 38]  ok
    gun_raise_a2 -> gun_a2           overlap 0.89  face moved     3  feet moved [1, 2]  ok
          gun_a2 -> gun_recoil_a6    overlap 0.68  face moved    42  feet moved [0, 15]  ok
   gun_recoil_a6 -> gun_a2           overlap 0.68  face moved    42  feet moved [0, 15]  ok
          gun_a2 -> gun_raise_a2     overlap 0.89  face moved     3  feet moved [1, 2]  ok
    gun_raise_a2 -> gun_draw_a       overlap 0.64  face moved    63  feet moved [1, 38]  ok
      gun_draw_a -> gun_step_a       overlap 0.64  face moved    60  feet moved [0, 221]  ok
      gun_step_a -> idle_a3          overlap 0.67  face moved    36  feet moved [0, 61]  ok
0 jump(s); overlays: art\p5\poses\seq_idle_a3_idle_a3.png
```

`python tools/p5/sequence_check.py idle_a3 knife_draw_a knife_set_a knife_ready_a slash_smear_a3 slash_a3 slash_follow_a knife_return_a slash_recover_a idle_a3 --allow slash_smear_a3`

```text
         idle_a3 -> knife_draw_a     overlap 0.85  face moved    32  feet moved [0, 10]  ok
    knife_draw_a -> knife_set_a      overlap 0.90  face moved    24  feet moved [0, 13]  ok
     knife_set_a -> knife_ready_a    overlap 0.69  face moved    73  feet moved [0, 53]  ok
   knife_ready_a -> slash_smear_a3   overlap 0.54  face moved    83  feet moved [46, 99]  (allowed)
  slash_smear_a3 -> slash_a3         overlap 0.71  face moved    34  feet moved [5, 5]  (allowed)
        slash_a3 -> slash_follow_a   overlap 0.91  face moved     1  feet moved [0, 1]  ok
  slash_follow_a -> knife_return_a   overlap 0.68  face moved    89  feet moved [23, 26]  ok
  knife_return_a -> slash_recover_a  overlap 0.73  face moved    82  feet moved [53, 58]  ok
 slash_recover_a -> idle_a3          overlap 0.69  face moved    57  feet moved [36, 37]  ok
0 jump(s); overlays: art\p5\poses\seq_idle_a3_idle_a3.png
```

结果：两段均 0 jump(s)，包含回待机段。残影帧按用户指定 --allow slash_smear_a3，未新增豁免。脚本只以 overlap<0.62 或脸移动>90px 判 JUMP；开枪 gun_step_a↔gun_draw_a 的前脚移动221px虽然未触发当前阈值，仍完整列出供用户验收，不宣称脚位移动小于其他帧。最终播放顺序应采用上面加帧后的命令顺序。

## 归一化及保留

更新 tools/p5/normalize_poses.py 的 POSES 并运行；新图均 1.0，rear/mid 如表。已有登记未改变。原版归一化已有 final/ 资源仅由脚本确定性重跑，不手工还原或覆盖远端版本。

art/cards 下另一会话的11个已修改文件原样保留且未暂存；既有 guard_a 本地修改与其他无关未跟踪资源未提交。不 stash、不 reset、不 rebase、不强推。没有打开结城理 mod 的图片，没有读取 obs.json 或密码。

本次生成原图、final版本、生成记录、归一化登记和本报告一并提交推送；推送后通过 ls-remote 确认 origin 分支收到提交。检查用对比图不提交。

归一化重跑还使12张既有 final PNG 的文件编码发生变化；已逐一与 HEAD 的 RGBA 数组比较，尺寸及所有颜色/alpha像素完全一致。没有还原这些输出，随本次归一化结果提交，原远端画面内容保持逐像素一致。
