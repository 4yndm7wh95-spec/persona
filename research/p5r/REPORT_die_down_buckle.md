# 横版躺地与膝软下坠（2026-10-27）

开始前fetch + merge快进至c883bdb6，使用远端新增ground对齐逻辑，未改该逻辑。没有rebase/reset/强推。

- die_down_b：1536×1024横版RGBA，参考idle_a3的比例服装、face_standard脸，以及die_down_a倒地方向。提示词要求头右脚左、右侧胸口贴地、身体展开不蜷缩、腿基本伸直，右臂向右前伸、左臂身侧落地、衣摆铺地。强调头约225px、头至鞋尖1300～1350px、画布下沿上方约20px落地并留边，头手不缩放。原图提交afb87118，主体外alpha1～2残点清理465像素。
- die_buckle_a：1024×1536竖版RGBA，以hurt_a4为底图、标准脸为参考。提示词要求脚位不动、双膝深弯、身体降至站高约3/4，右膝前下方近地、头垂刘海遮眼、右手准备撑地左手落向左膝、衣摆回落，头手大小照hurt_a4。原图提交ff1be1fb，清理残点169像素。

以上为提示词要求，实际画面交给用户验收。内置image_gen每张仅生成一次，没有审图重画、没有做die_slump_a。均不画特效、伤口和血。完整提示词、输入和原始输出路径见对应_generation.json。各张保存即提交推送，ls-remote核对origin包含提交。

## 登记与检查

die_down_b登记1.0/ground，die_buckle_a登记1.0/rear。现有归一化函数仅对本批运行一次并合并normalize.json；ground将横版图放入1536×1632最终画布，对齐idle鞋底地面。保留其他final及既有参数，没有额外缩放位移。

仅运行一次：`python tools/p5/pose_consistency.py idle_a3 die_buckle_a`

```text
die_buckle_a       head x1.124  body x0.655 (check)  boot height x1.071  boot width x1.049  feet moved [76, 62] px  FAIL
sheet: art\p5\poses\check_die_buckle_a.png | overall: FAIL
```

输出原样记录，不按结果重画或重跑；下坠姿态的身体竖直长度变化不能直接当作身体比例缩小。横版die_down_b未运行比例检查，本轮未跑sequence_check。没有修改游戏时长或增加其他帧。原die_down_a等旧图保留。

art/cards另一会话11个已修改文件原样保留，未提交、未stash、未还原；guard_a及其他无关资源保留。未打开结城理mod图片，未读取obs.json或密码。最终登记和报告推送后核对origin。
