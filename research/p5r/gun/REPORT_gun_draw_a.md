# Joker 拔枪起手帧 gun_draw_a

## 本次交付

- 新图：`art/p5/poses/gun_draw_a.png`，1024×1536 RGBA，待用户验收。
- 使用内置 image_gen，以本地已有 `idle_a3.png` 为编辑底图；`gun_raise_a2.png` 仅提供后续动作端点，`research/p5r/gun/pick/51_frames/frame_002.png` 仅参考抽出道具手枪的起手动作。
- 动作要求：身体转到一半、前脚收拢一半，右手在右腰侧拿出一把黑色道具手枪，枪口朝下；左手空着靠向大腿外侧，头和视线朝画面右方。完整提示词与输入列表见 `gun_draw_a_generation.json`。
- 一次生成，无安全拒绝，没有额外审图或按主观画面评价重试，视觉验收交给用户。
- 仅清理远离主体的 alpha 1～2 残点，共 251 个像素；其余像素不变。alpha=0 像素共 975062 个。

## 按要求运行比例检查

命令：`python tools/p5/pose_consistency.py idle_a3 gun_draw_a`

```text
gun_draw_a         head x0.987  body x0.997  boot height x0.970  boot width x0.951  feet moved [110, 174] px  ok
sheet: art\p5\poses\check_gun_draw_a.png | overall: ok
```

检查通过，未手工缩放。脚位移动是本次收拢站距动作的一部分。检查对比图不提交。

## 合并与保留说明

本轮前序断线重试后已快进合并到 `fd2a3928`。按用户新指示先使用本地输入生成并提交，再在推送前以 HTTP/1.1、禁用低速中断及大缓冲参数 fetch，随后 merge --no-edit 和 push；失败最多四次，等待按 5/10/20/40 秒安排，耗尽即完整报告错误，不 reset、不强推。

按用户本轮明确指示，没有运行归一化，也没有本地改写、还原 `art/p5/poses/final/`；其远端新版保持合并结果，后续游戏接入待用户验收与 Claude 协作处理。`art/cards` 的 11 个已修改文件原样保留且不提交；既有本地 guard_a 修改与其他未跟踪资源也不提交。未打开结城理 mod 图片，未读取 obs.json 或密码，未启动游戏。
