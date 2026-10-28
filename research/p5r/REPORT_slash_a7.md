# 出刀折中关键帧 slash_a7 生成报告

- 开始时 fetch + merge，远端已同步，基线 9d94a39c。
- 使用内置 image_gen，slash_a3 为编辑底图，face_standard 为脸参考，slash_lunge_a 只参考后手方向，幅度减半；完整提示词和输入路径见 slash_a7_generation.json。
- slash_a7 只生成一次，未自行审图或重画；清理极低 alpha 残点 2737 像素。
- 上次中断后生成的 slash_lunge_a 已保存保留，用户作废，不登记为定稿。旧图均保留。
- 原图与生成记录已提交推送：1e6d563d。
- slash_a7 登记 (0.96, 'mid')，归一化仅本次新图运行一次，保留其他已归一化图；比例检查一次，输出如下。
- 先只交这一张，等用户看过再决定后续。
- art/cards 的 11 个修改文件原样保留，未提交、stash 或还原；未打开结城理 mod 图片，未读取 obs.json 或密码。

## 比例检查原始输出

```text
slash_a7           head x0.973  body x0.903 (check)  boot height x1.011  boot width x1.027  feet moved [97, 55] px  ok
sheet: art\p5\poses\check_slash_a7.png | overall: ok
```
