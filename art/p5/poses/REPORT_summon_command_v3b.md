# Joker 指挥 v3b：右手朝下

只修改 summon_command_a 的右手和紧邻袖口边缘。以 v3 定稿画布裁出局部，参考 ref_command_hand_style.png 生成手背朝上、手心向下的手套；补暗红明暗、手背褶子及粗黑线，并处理袖口交界。左手插兜和 summon_command_mid_a 不改。

生成 1 次，重画 0 次。生成结果仅取局部贴回，未用整图重绘。完整提示词、输入、原始输出路径及蒙版坐标见 summon_command_a_generation.json。

贴回前在局部蒙版外逐像素核对为零改动；归一化后再次核对右手裁片范围之外零改动。原图 1024×1536 RGBA；final 为 1536×1632 RGBA。按既有 (1.0, rear) 只对本张归一化一次，偏移 [343,99]，其他 final 与 normalize 条目不改。裁片贴回后按原图画布边界裁去最右端的边缘像素，未缩放全身。美术观感及接缝由用户验收，不自行反复重画。

check_command_v3b.png：上排右手局部、下排全身，左 v3、右 v3b，深灰底。

## 检查输出（原样）

```text
summon_command_a   head x1.000  body x1.000  boot height x1.000  boot width x1.000  feet moved [0, 0] px  ok
sheet: tmp\sts2_vfx_20261102\command_refs\check_summon_command_a.png | overall: ok
{"summon_command_a": {"scale": 1.0, "offset": [343, 99], "anchor_foot": "rear"}}
```

检查使用临时目录里更新的纯 numpy 兼容脚本，未安装依赖、未修改检查脚本。运行脚本中“整个贴回结果必须与逆裁切后归一化结果相同”的额外断言因裁片边缘超出原图右界几个像素触发；最终改为核对编辑区外像素，结果为零。比例检查和归一化没有因此重复运行。

独立推送：codex/p5-joker-command-v3b，不 fetch、不 merge。亚森 v7 拆层已在前一提交 e84bcd95 完成，本轮不重复生成。
