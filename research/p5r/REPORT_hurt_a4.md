# 受击小幅修订 hurt_a4（2026-10-27）

开始前fetch + merge成功。上轮中断时hurt_a3原图和记录已经提交为f103a0a3并推送，本轮保留，不继续其归一化或恢复帧任务。hurt_a2、hurt_a3、hurt_recover_a原文件均保留。未rebase/reset/强推。

本轮以hurt_a2为底图，face_standard为脸部参考，仅在提示词中要求小幅调整：头往左收回到腰上方或略左，下巴略抬；肩膀放平稍后扳、胸口不前挺；双臂向身体左右展开，肘微弯指张开，不往身后甩。腿脚、衣摆、表情、头身比例保持底图，手大小不变，不增加后仰幅度。不画特效、伤口和血。以上为提示词约束，实际画面由用户判断。完整提示词、输入与原始输出路径见hurt_a4_generation.json。

内置image_gen仅生成一次，未审图重画。保存1024×1536 RGBA，清理主体外alpha1～2透明残点1426像素。原图和生成记录提交bd3f003c，推送后ls-remote确认origin收到。

登记1.0/rear，现有归一化函数仅对本批运行一次并合并normalize.json，保留其他final及既有参数，不加额外位移缩放。检查各一次：

`python tools/p5/pose_consistency.py idle_a3 hurt_a4`

```text
hurt_a4            head x0.920  body x0.977  boot height x1.042  boot width x1.049  feet moved [18, 31] px  FAIL
sheet: art\p5\poses\check_hurt_a4.png | overall: FAIL
```

`python tools/p5/sequence_check.py idle_a3 hurt_a4`

```text
         idle_a3 -> hurt_a4          overlap 0.62  face moved    55  feet moved [0, 41]  ok
0 jump(s); overlays: art\p5\poses\seq_idle_a3_hurt_a4.png
```

输出原样记录，不据结果重画、追加帧、修改阈值或加--allow。等用户看过关键帧才做恢复帧；取消未做的hurt_recover_a2任务。检查拼图存本机outputs/p5_hurt_a4，不提交。

art/cards另一会话11个已修改文件原样保留，未提交、未stash、未还原；guard_a和其他无关资源保留。未打开结城理mod图片，未读取obs.json或密码。最终登记和报告推送后核对origin。
