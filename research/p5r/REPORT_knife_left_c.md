# 小刀动作左臂第二次修订（2026-10-27）

开始前fetch + merge成功，保留已认可knife_ready_b。slash_smear_b按用户意见作废留档，未作为新图输入；slash_a4/slash_follow_b此前未生成。本轮三张分别以对应原图为唯一底图：

| 新图 | 底图 | 提示词左臂要求 | 原图提交 |
|---|---|---|---|
| slash_mid_b | slash_mid_a | 左手从胸前下拉至腹前偏画面右侧，肘弯指微张 | 680a18e4 |
| slash_a5 | slash_a3 | 左手在画面右侧胯外握拳，腕贴胯、肘后弯，但手不去背后 | 19edc20a |
| slash_follow_c | slash_follow_a | 左手胯边放松垂下一半、手指松开，仍在画面右侧 | a839a1de |

三张提示词都明确左手不得绕背或出现在画面左边、手尺寸不变，其他头脸、右臂和刀、身体、衣摆、腿脚及位置保持底图，不画残影或特效。以上是提示词要求，实际画面交给用户判断，不写成已验收结论。内置image_gen每张只生成一次，未审图或重画；1024×1536 RGBA，主体外alpha1～2残点分别清理738、71、900像素。完整提示词、输入和原始输出路径见对应_generation.json。每张保存即提交推送，ls-remote确认origin包含提交。

## 一次登记归一化及序列检查

knife_ready_b=0.96/rear；slash_mid_b、slash_a5、slash_follow_c=0.96/mid。现有归一化函数仅对这四张运行一次并合并normalize.json，保留其他final及原参数，没有额外缩放位移。

`python tools/p5/sequence_check.py knife_draw_a knife_set_a knife_ready_b slash_mid_b slash_a5 slash_follow_c knife_return_a`

```text
    knife_draw_a -> knife_set_a      overlap 0.90  face moved    24  feet moved [0, 13]  ok
     knife_set_a -> knife_ready_b    overlap 0.67  face moved    73  feet moved [1, 53]  ok
   knife_ready_b -> slash_mid_b      overlap 0.55  face moved    84  feet moved [45, 99]  JUMP - add a frame
     slash_mid_b -> slash_a5         overlap 0.71  face moved    33  feet moved [6, 6]  ok
        slash_a5 -> slash_follow_c   overlap 0.84  face moved     2  feet moved [0, 2]  ok
  slash_follow_c -> knife_return_a   overlap 0.68  face moved    89  feet moved [23, 25]  ok
1 jump(s); overlays: art\p5\poses\seq_knife_draw_a_knife_return_a.png
```

输出原样记录，不据结果重画或加帧，不加--allow、不改阈值；未另跑比例检查。原图和作废图保留不删。检查拼图存本机outputs/p5_knife_left_c，不提交。

art/cards另一会话11个已修改文件原样保留，未提交、未stash、未还原；guard_a及其他无关资源保留。未打开结城理mod图片，未读取obs.json或密码，没有rebase/reset/强推。最终登记和报告推送后核对origin。
