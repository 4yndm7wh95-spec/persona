# 受击关键帧 hurt_a2（2026-10-27）

开始前fetch + merge快进至8c81c3b7e，保留扯手套定稿与现有final、归一化参数，未rebase/reset/强推。

以idle_a3为编辑底图，hurt_a仅参考受冲击后仰的动作感觉，不采用它偏小的比例；face_standard为脸部参考。提示词要求双脚位置和站距完全照idle、膝盖可微弯，上身向画面左后方约15度、头后移下巴微抬但脸仍朝右，皱眉眯眼咬牙；双臂往两侧和身后张开，手指张开、手大小照idle，衣摆向左后方飘起。头身大小、服装照idle，不画特效、伤口和血。以上为提示词约束，实际画面交给用户判断。

内置image_gen仅生成一次，没有审图或重画，没有生成过渡帧。保存1024×1536 RGBA，清理主体外alpha1～2透明残点429像素。完整提示词、输入和原始输出路径见hurt_a2_generation.json。原图与记录提交5f17e6532，推送后ls-remote确认origin收到。

登记1.0/rear，现有归一化函数仅对本批一张运行一次并合并normalize.json，保留其他final及参数，不加额外位移、缩放。检查各一次：

`python tools/p5/pose_consistency.py idle_a3 hurt_a2`

```text
hurt_a2            head x0.960  body x0.983  boot height x1.036  boot width x1.031  feet moved [18, 30] px  ok
sheet: art\p5\poses\check_hurt_a2.png | overall: ok
```

`python tools/p5/sequence_check.py idle_a3 hurt_a2`

```text
         idle_a3 -> hurt_a2          overlap 0.62  face moved    37  feet moved [0, 40]  ok
0 jump(s); overlays: art\p5\poses\seq_idle_a3_hurt_a2.png
```

输出原样记录，不根据结果重画、不追加帧、不加--allow、不改阈值。等待用户看过关键帧再画过渡帧。检查拼图保留在本机outputs/p5_hurt_a2，不提交。

art/cards另一会话11个已修改文件原样保留，未提交、未stash、未还原；guard_a及其他无关资源保留。未打开结城理mod图片，未读取obs.json或密码。最终登记与报告推送后核对origin。
