# Joker 扶面具到指挥：过渡帧交付（未全部通过）

生成三张，每张仅一次、重画零次。达到任务最多三张的上限后停止，不根据数字继续生成。没有修改既有定稿姿势、mod 代码或游戏文件，没有启动游戏。

## 文件及顺序

- summon_bridge1_a：以 summon_hold_a 编辑，手离脸、左手向胯抬、后脚撤出。
- summon_bridge2_a：以 summon_command_mid_a 编辑，手回到胸前偏右、左手接近入兜、站距介于两端。
- summon_bridge3_a：第一次检查后，针对 bridge1 → bridge2 的较大脸位移补一张，以 bridge1 编辑、bridge2 参考。
- 播放顺序：hold → bridge1 → bridge3 → bridge2 → command_mid → command。编号不等于播放顺序。
- check_command_bridge.png：以上六张按顺序并排，深灰底。
- 各自完整提示词、输入和原始输出位置见 generation.json。

## 归一化与限制

新帧按 1.0 登记：bridge1 和 bridge3 用 front，bridge2 用 rear，未额外缩放或手工挪动凑分。只归一化新增帧，旧 final 不改；normalize.json 合并保留旧条目。检查用更新的纯 numpy 兼容脚本，在临时目录调用，未安装依赖、未覆盖其他脚本。

两张先生成检查，再按任务补第三张并对新序列复查。没有自动重画。最终仍未实现每步无 JUMP：hold → bridge1 后脚移 212px；bridge3 → bridge2 脸移 191px、overlap 0.34。bridge2 头部只有底图的 0.910，比例 FAIL。此批为待调整素材，不是流畅动画验收通过。

生图以透明 RGBA 请求交付，清理 alpha≤2 残点。人物边缘可能保留原参考中带入的半透明雾状光晕，未做轮廓透明的人工修补，不宣称边缘验收通过。
- summon_bridge1_a：1024×1536，RGBA，四角 alpha [0, 0, 0, 0]。
- summon_bridge2_a：1024×1536，RGBA，四角 alpha [0, 0, 0, 0]。
- summon_bridge3_a：1024×1536，RGBA，四角 alpha [0, 0, 0, 0]。

## 首次检查输出（原样）

```text
summon_bridge1_a   head x1.004  body x0.991  boot height x0.984  boot width x0.958  feet moved [35, 205] px  ok
sheet: tmp\sts2_vfx_20261102\command_refs\check_summon_bridge1_a.png | overall: ok
summon_bridge2_a   head x0.910  body x1.016  boot height x0.991  boot width x0.795  feet moved [49, 68] px  FAIL
sheet: tmp\sts2_vfx_20261102\command_refs\check_summon_bridge2_a.png | overall: FAIL
{"summon_bridge1_a": {"scale": 1.0, "offset": [461, 79], "anchor_foot": "front"}, "summon_bridge2_a": {"scale": 1.0, "offset": [293, 99], "anchor_foot": "rear"}}
   summon_hold_a -> summon_bridge1_a overlap 0.77  face moved    22  feet moved [212, 0]  JUMP - add a frame
summon_bridge1_a -> summon_bridge2_a overlap 0.26  face moved   233  feet moved [172, 109]  JUMP - add a frame
summon_bridge2_a -> summon_command_mid_a overlap 0.69  face moved    38  feet moved [0, 117]  ok
summon_command_mid_a -> summon_command_a overlap 0.87  face moved     3  feet moved [0, 0]  ok
2 jump(s); overlays: sts2_tools\tmp\sts2_vfx_20261102\seq_summon_hold_a_summon_command_a.png
```

## 补第三张后的检查输出（原样）

```text
summon_bridge3_a   head x1.018  body x1.000  boot height x0.986  boot width x0.951  feet moved [2, 43] px  ok
sheet: tmp\sts2_vfx_20261102\command_refs\check_summon_bridge3_a.png | overall: ok
{"summon_bridge3_a": {"scale": 1.0, "offset": [418, 80], "anchor_foot": "front"}}
   summon_hold_a -> summon_bridge1_a overlap 0.77  face moved    22  feet moved [212, 0]  JUMP - add a frame
summon_bridge1_a -> summon_bridge3_a overlap 0.72  face moved    42  feet moved [40, 0]  ok
summon_bridge3_a -> summon_bridge2_a overlap 0.34  face moved   191  feet moved [132, 109]  JUMP - add a frame
summon_bridge2_a -> summon_command_mid_a overlap 0.69  face moved    38  feet moved [0, 117]  ok
summon_command_mid_a -> summon_command_a overlap 0.87  face moved     3  feet moved [0, 0]  ok
2 jump(s); overlays: sts2_tools\tmp\sts2_vfx_20261102\seq_summon_hold_a_summon_command_a.png
```

独立推送分支：codex/p5-joker-command-bridge。不 fetch、不 merge；本轮只提交新过渡图、相应 final/登记/生成记录、对比图与本报告。
