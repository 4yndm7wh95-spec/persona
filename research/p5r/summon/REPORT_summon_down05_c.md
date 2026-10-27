# 召唤收手中间帧 summon_down05_c（2026-10-27）

开始前fetch + merge快进至d7b13ed24，保留远端既有final及比例更新，没有rebase/reset/强推。

以summon_settle_b为编辑底图，summon_down1_c仅作下一帧位置参考；提示词要求右手、上身和头、后脚、衣摆均位于两者中间。头的大小、比例、脸以底图为准，不画面具和特效。完整提示词和输入记录见summon_down05_c_generation.json。

内置image_gen只生成一次，没有审图或重画。保存1024×1536 RGBA，清理主体外alpha1～2透明残点214像素。原图与生成记录提交732e5d8d9，推送后ls-remote确认origin收到。

登记1.0/front；现有归一化函数仅对本批一张运行一次，合并normalize.json，保留其他final和参数。没有手工缩放、额外位移、修改检查阈值或追加帧。

检查仅运行一次：
`python tools/p5/sequence_check.py summon_settle_b summon_down05_c summon_down1_c summon_down25_c summon_down3_c idle_a3`

```text
 summon_settle_b -> summon_down05_c  overlap 0.53  face moved   118  feet moved [245, 0]  JUMP - add a frame
 summon_down05_c -> summon_down1_c   overlap 0.82  face moved    47  feet moved [10, 0]  ok
  summon_down1_c -> summon_down25_c  overlap 0.79  face moved    33  feet moved [30, 0]  ok
 summon_down25_c -> summon_down3_c   overlap 0.75  face moved    50  feet moved [13, 0]  ok
  summon_down3_c -> idle_a3          overlap 0.88  face moved    14  feet moved [100, 0]  ok
1 jump(s); overlays: art\p5\poses\seq_summon_settle_b_idle_a3.png
```

输出原样记录，仍有1处JUMP；不根据结果重画，判断交给用户和Claude。没有另外跑pose_consistency或进行视觉验收。

art/cards另一会话11个已修改文件原样保留，未提交、未stash、未还原；guard_a及其他无关资源保留。未打开结城理mod图片，未读取obs.json或密码。最终登记及报告推送后核对origin。
