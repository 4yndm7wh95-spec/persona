# Joker 指挥 v2：关键帧待确认

按用户要求逐项执行，本次只画第 2 张关键帧 `summon_command_a.png`；第 1、3 张等用户确认后再生成。亚森 v7 拆层排在本任务后，尚未开始。

- 以 summon_hold_a 编辑，idle_a3 作比例和分脚参考，face_standard 作脸部参考。由于先交关键帧，新的中间帧尚不存在，未将旧版托手中间帧当底图。
- 本轮生成 1 次，自动重画 0 次；完整提示词和输入见 summon_command_a_generation.json。
- 输出 1024×1536 RGBA。清除 alpha≤2 的残点，四角 alpha 为 0，完全透明像素 999423 个；未对全轮廓的半透明雾边作通过保证。
- 使用远端更新的检查脚本及纯 numpy _ndimage 替代（下载至临时目录运行，未覆盖仓库脚本）。底图脸被遮挡，按规则用 idle_a3 比。

## 检查输出（原样）

```text
summon_command_a   head x1.089  body x0.991  boot height x0.888  boot width x0.880  feet moved [47, 166] px  FAIL
sheet: tmp\sts2_vfx_20261102\command_refs\check_summon_command_a.png | overall: FAIL
```

头部量值比 idle 大约 8.9%，脚位有变化，供用户和 Claude 判断；按长期规则不据此自动重画或缩放。检查联系表留在临时目录。

本轮停在关键帧确认点。normalize 登记、完整序列检查和六帧 check_command.png 等三张齐全后再执行。未改 mod 代码，未启动游戏；不 fetch / merge。推送分支 codex/p5-joker-command-v2。
