# 受击站回过渡 hurt_recover_a（2026-10-27）

开始前fetch + merge成功，远端与本地3a28d5e7一致。未rebase/reset/强推。

以已认可的hurt_a2为底图编辑、face_standard为脸部参考。提示词要求上身回正一半只剩轻微后仰，头回正下巴放低，仍皱眉但视线重新看向画面右方；双臂向身体两侧收一半、手肘略弯、手指放松；衣摆回落一半。双脚完全保持底图位置，手掌大小和指长、头身比例、服装、脸照底图。不画特效、伤口和血。以上为提示词约束，实际画面直接交给用户判断。

内置image_gen只生成一次，未审图重画、未追加帧。保存1024×1536 RGBA，清理主体外alpha1～2透明残点513像素；完整提示词、输入及原始输出路径见hurt_recover_a_generation.json。原图与记录提交02545240，推送后已ls-remote确认origin收到。

新图登记1.0/rear，现有归一化函数仅对本批运行一次并合并normalize.json，保留其他final及配置，不加额外缩放位移。各检查一次：

`python tools/p5/pose_consistency.py idle_a3 hurt_recover_a`

```text
hurt_recover_a     head x1.049  body x0.976  boot height x1.023  boot width x1.038  feet moved [17, 30] px  ok
sheet: art\p5\poses\check_hurt_recover_a.png | overall: ok
```

`python tools/p5/sequence_check.py idle_a3 hurt_a2 hurt_recover_a idle_a3`

```text
         idle_a3 -> hurt_a2          overlap 0.62  face moved    37  feet moved [0, 40]  ok
         hurt_a2 -> hurt_recover_a   overlap 0.72  face moved    10  feet moved [0, 0]  ok
  hurt_recover_a -> idle_a3          overlap 0.72  face moved    42  feet moved [0, 39]  ok
0 jump(s); overlays: art\p5\poses\seq_idle_a3_idle_a3.png
```

输出原样记录，不根据结果重画、不追加帧、不加--allow、不改阈值。检查拼图存本机outputs/p5_hurt_recover，不提交。

art/cards另一会话11个已修改文件原样保留，未提交、未stash、未还原；guard_a及其他无关资源保留。未打开结城理mod图片，未读取obs.json或密码。最终报告与登记推送后核对origin。
