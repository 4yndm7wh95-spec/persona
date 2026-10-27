# 召唤收手 down1_c / down25_c（2026-10-27）

开始前fetch + merge快进至f6e08b7ce，保留远端既有final与比例更新（down2_c=1.015/front、down3_c=1.02/front），没有rebase/reset/强推。

## 生成及推送

- summon_down1_c：以summon_settle_b为编辑底图，down2_c作位置参考；提示词要求右手降至腰侧，上身和头整体向后脚移至两参考头位置的1/3处，后脚取中点、身体转回1/3。用于替换down1_b，旧文件保留。原图提交35e663b35。
- summon_down25_c：以summon_down2_c为编辑底图，down3_c仅作下一帧位置参考；提示词要求头、上身、后脚位于两者中间，双手自然下垂，衣摆落回一半。原图提交a906855a7。

两张各用内置image_gen生成一次，没有审图或重画；1024×1536 RGBA，主体外alpha1～2透明残点分别清理370、291像素。完整提示词、输入与原始输出路径见对应_generation.json。无面具和特效为提示词约束，实际画面交给用户验收。每张保存后立即提交推送，并用ls-remote确认origin包含该提交。

## 登记与一次检查

两张均登记1.0/front；现有归一化函数仅对本批运行一次，合并normalize.json，保留其他final文件。没有额外位移、手工缩放、阈值更改或增加帧。

`python tools/p5/sequence_check.py summon_settle_b summon_down1_c summon_down2_c summon_down25_c summon_down3_c idle_a3`

```text
 summon_settle_b -> summon_down1_c   overlap 0.48  face moved   145  feet moved [223, 0]  JUMP - add a frame
  summon_down1_c -> summon_down2_c   overlap 0.84  face moved    21  feet moved [47, 0]  ok
  summon_down2_c -> summon_down25_c  overlap 0.85  face moved    41  feet moved [17, 0]  ok
 summon_down25_c -> summon_down3_c   overlap 0.72  face moved    61  feet moved [4, 0]  ok
  summon_down3_c -> idle_a3          overlap 0.85  face moved    27  feet moved [96, 0]  ok
1 jump(s); overlays: art\p5\poses\seq_summon_settle_b_idle_a3.png
```

输出原样记录：仍有1处JUMP，不根据结果重新生成或追加帧，由用户与Claude决定下一步。没有另跑pose_consistency检查。

art/cards另一会话11个已修改文件原样保留，未提交、未stash、未还原；guard_a和其他无关资源也保留。未打开结城理mod图片，未读取obs.json或密码。报告、生成原图、final与登记均提交推送，最终推送后核对origin。
