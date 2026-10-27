# 扯手套单次拉紧：接近腕口及松手试戴（2026-10-27）

开始前fetch + merge快进至c9c5a72fb，保留远端修正后的glove_b原图与final，未重写旧图。无rebase/reset/强推。

- glove_reach_a：以glove_a为底图，face_standard为脸部参考。提示词要求左手位置保持、指头放松，右手拇指与食指张开刚到左腕旁，尚未捏住或下扯。原图提交aa0736313。
- glove_flex_a：以最新glove_b为底图，face_standard为脸部参考。提示词要求右手松开降至腰前，左手胸前半握拳、手背朝外略转腕，保持微笑。原图提交9daf3c871。

两张均强调手掌尺寸、手指长度与各自底图一致，不画大手或长手指；头、脸、身体、外套、腿脚保持底图，不画特效。以上为提示词约束，实际画面由用户验收。完整提示词及输入见各自_generation.json。

内置image_gen每张仅生成一次，没有审图重画。保存1024×1536 RGBA，主体外alpha1～2残点分别清理601、643像素。每张原图与记录保存后即提交推送，并用ls-remote确认origin包含提交。

新图均登记1.0/rear。现有归一化函数只对本批运行一次并合并normalize.json，保留其他final与原有参数，没有额外缩放和位移。各跑一次检查：

`python tools/p5/pose_consistency.py idle_a3 glove_reach_a glove_flex_a`

```text
glove_reach_a      head x0.991  body x1.001  boot height x1.000  boot width x1.005  feet moved [1, 2] px  ok
glove_flex_a       head x1.004  body x1.000  boot height x1.019  boot width x1.042  feet moved [1, 2] px  ok
sheet: art\p5\poses\check_glove_reach_a.png | overall: ok
```

`python tools/p5/sequence_check.py idle_a3 glove_mid_a glove_reach_a glove_a glove_b glove_flex_a glove_mid_a idle_a3`

```text
         idle_a3 -> glove_mid_a      overlap 0.85  face moved     3  feet moved [0, 1]  ok
     glove_mid_a -> glove_reach_a    overlap 0.94  face moved     2  feet moved [0, 0]  ok
   glove_reach_a -> glove_a          overlap 0.99  face moved     2  feet moved [0, 0]  ok
         glove_a -> glove_b          overlap 0.98  face moved     1  feet moved [0, 0]  ok
         glove_b -> glove_flex_a     overlap 0.97  face moved     1  feet moved [0, 0]  ok
    glove_flex_a -> glove_mid_a      overlap 0.94  face moved     2  feet moved [0, 0]  ok
     glove_mid_a -> idle_a3          overlap 0.85  face moved     3  feet moved [0, 1]  ok
0 jump(s); overlays: art\p5\poses\seq_idle_a3_idle_a3.png
```

输出原样记录，不根据结果重新生成或追加帧。顺序只经过一次glove_b拉紧，再松手试戴并回待机；本轮仅提供新帧及检查，没有修改游戏播放时长代码。判断和时长编排交给用户与Claude。

art/cards另一会话11个已修改文件原样保留，未提交、未stash、未还原；guard_a及其他无关资源保留。未打开结城理mod图片，未读取obs.json或密码。最终登记报告推送后核对origin。
