# 跪姿右腿微调 die_kneel_a2（2026-10-27）

开始前fetch + merge成功，远端与本地06cbedff一致，未rebase/reset/强推。

以die_kneel_a为唯一编辑底图，只要求修改画面左侧跪地右腿：右膝在胯下、可略向右收，小腿向身后平放并收入身体下，被大腿和衣摆遮住而显短；右靴在臀部后下方、鞋尖朝左脚背贴地，不再伸向左边很远。提示词强调其他头脸、上身、双手、左腿左脚、衣摆及头手大小保持底图，不画特效、伤口和血。以上为提示词约束，实际画面直接交给用户验收。完整提示词和输入见die_kneel_a2_generation.json。

内置image_gen仅生成一次，未审图重画。保存1024×1536 RGBA，清理主体外alpha1～2透明残点15像素。原图和记录提交2455e701，推送后已用ls-remote确认origin收到。

新图登记1.0/front，现有归一化函数仅处理本批一次并合并normalize.json，保留其他final及既有参数，不增加缩放位移。

## 一次比例检查（原始输出）

`python tools/p5/pose_consistency.py idle_a3 die_kneel_a2`

```text
die_kneel_a2       head x0.284  body x0.606 (check)  boot height x0.762  boot width x0.948  feet moved [164, 53] px  FAIL
sheet: art\p5\poses\check_die_kneel_a2.png | overall: FAIL
```

按用户要求原样记录；刘海遮住眼睛、低头跪姿会影响测量，身体竖直长度变短正常。不以结果自动重画、修改阈值或重跑。本张未运行sequence_check。检查拼图存本机outputs/p5_die_kneel_a2，不提交。

die_kneel_a原图和final保留不删。art/cards另一会话11个已修改文件原样保留，未提交、未stash、未还原；guard_a及其他无关资源保留。未打开结城理mod图片，未读取obs.json或密码。最终登记报告推送后核对origin。
