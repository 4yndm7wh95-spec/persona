# 扯手套补帧 glove_mid_a / glove_b（2026-10-27）

开始前fetch + merge成功，远端与本地a86f8b41b一致。保持用户认可的方向：左手抬起，右手扯左手套，不交换左右。

## 本轮生成与推送

- glove_mid_a：以glove_a为底图、face_standard为脸部参考，提示词仅要求改变双臂到抬起半路：左手位于腰胸之间、手指自然下垂，右手未接触手套、两手相隔约一拳，其余头身衣腿脚保持底图。原图提交5cfe76be1。
- glove_b：同样以glove_a为底图、标准脸为参考，提示词要求右手捏左手套腕口向手肘多扯一点，左手指张开绷直、手腕略上翘，其余保持底图。原图提交a88e68062。

内置image_gen每张仅生成一次，没有审图或重画。均保存1024×1536 RGBA，清理主体外alpha1～2透明残点，分别321、325像素。完整提示词、输入和原始输出路径见各自_generation.json。不画特效、保持底图为提示词约束，实际画面直接交给用户验收。两次原图推送后均ls-remote核对origin收到对应提交。

## 一次归一化、一次比例检查、一次序列检查

两张均登记1.0/rear，现有归一化函数仅对本批运行一次、合并normalize.json，保留旧final与配置。没有新增缩放、位移、阈值修改或追加图。

`python tools/p5/pose_consistency.py idle_a3 glove_mid_a glove_b`

```text
glove_mid_a        head x0.978  body x1.003  boot height x1.006  boot width x1.026  feet moved [2, 2] px  ok
glove_b            head x1.000  body x1.001  boot height x1.006  boot width x1.037  feet moved [1, 2] px  ok
sheet: art\p5\poses\check_glove_mid_a.png | overall: ok
```

`python tools/p5/sequence_check.py idle_a3 glove_mid_a glove_a glove_b glove_a`

```text
         idle_a3 -> glove_mid_a      overlap 0.85  face moved     3  feet moved [0, 1]  ok
     glove_mid_a -> glove_a          overlap 0.94  face moved     1  feet moved [0, 0]  ok
         glove_a -> glove_b          overlap 0.97  face moved     1  feet moved [0, 0]  ok
         glove_b -> glove_a          overlap 0.97  face moved     1  feet moved [0, 0]  ok
0 jump(s); overlays: art\p5\poses\seq_idle_a3_glove_a.png
```

以上输出原样记录，不根据结果重新生成或加帧。检查拼图保存在本机outputs/p5_glove_frames，不提交。判断交给用户与Claude。

art/cards另一会话11个已修改文件原样保留，未提交、未stash、未还原；guard_a及其他无关资源保留。未打开结城理mod图片，未读取obs.json或密码。没有rebase/reset/强推。最终报告和登记推送后核对origin。
