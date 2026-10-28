# slash_a8 左手收紧版本生成报告

- fetch + merge 后远端已同步，基线 6b033c4e。
- 内置 image_gen 编辑 slash_a7，仅要求修改左臂和左手：沿后背向下收紧，拳头别在后腰，大部分被身体和大衣遮挡，仅露约三分之一红手套；其余部分保持底图。
- 只生成一次，不自行审图、不重画。完整提示词与输入见 slash_a8_generation.json。
- 极低 alpha 透明残点清理：185 像素。
- 登记 slash_a8 (0.96, 'mid')，仅对本次新图运行一次归一化，保留其他 final 图和登记。
- 本次未要求 pose_consistency 或 sequence_check，因此未运行。
- 历史原图保留，等待用户判断。
- art/cards 的 11 个修改文件原样保留，未提交、stash 或还原。未打开结城理 mod 图片，未读取 obs.json 或密码。

## 归一化输出

```text
{"slash_a8": {"scale": 0.96, "offset": [300, 215], "anchor_foot": "mid"}}
```
