# summon_rip_a2：从通过的扶脸帧编辑

开始前fetch+merge成功，远端无新增，基于14a7832d6。用户明确作废旧summon_rip_a（双手交叉举到头顶）；旧文件不改，验收状态标记作废。本轮只生成一次summon_rip_a2，不使用key_rip_mid或旧summon_rip_a作为输入。

## 生成

编辑底图为已通过的summon_hold_a，face_standard仅补全露出的脸。提示词要求只动扶脸的右手：向外上方扯开，离脸至少一个拳头，弯曲收紧手指，不摸额头/头发；左手保持原图自然垂下，身体腿脚大衣保持底图，头仅微后仰并看画面右方。无面具、血、特效。内置image_gen，1024×1536 RGBA，透明残点仅清除主体外alpha1～2的233像素，其余像素不改。完整提示词、输入及源文件位置见summon_rip_a2_generation.json。没有额外主观审图。

## 用户指定检查输出

`python tools/p5/pose_consistency.py summon_hold_a summon_rip_a2`

```text
summon_rip_a2      head x1.316  body x0.961 (check)  boot height x0.990  boot width x0.995  feet moved [5, 8] px  FAIL
sheet: art\p5\poses\check_summon_rip_a2.png | overall: FAIL
```

## 补充定位检查

新图脸露出，未使用--occluded。底图summon_hold_a本身脸被挡；目前脚本仍先对底图运行head_height，使用被遮挡的最大肤色连通块当作下巴。--occluded只对待测新图生效，不会替换被遮挡底图的head基准。因此指定命令如实记录FAIL，不能用这个比例直接把新图缩头。

为区分底图遮挡基准与新图比例，额外按未遮挡待机对比（不替代用户指定结果）：

`python tools/p5/pose_consistency.py idle_a3 summon_rip_a2`

```text
summon_rip_a2      head x1.000  body x1.000  boot height x0.961  boot width x0.916  feet moved [209, 171] px  ok
sheet: art\p5\poses\check_summon_rip_a2.png | overall: ok
```

两次输出共同保留。没有改脚本/阈值、没有对露出脸的新图加--occluded，也没有手工缩放凑数；脚相对编辑底图只移动5/8px。底图遮挡作为比较基准的处理方式未在现有脚本中覆盖，按“遇到没写到的情况停下来报告”停止进一步重画和归一化，不为了指定FAIL强行改变头部大小。当前图推送供用户验收，summon_release未生成。

## 保留与提交

提交当前图、生成记录、验收状态和本报告。art/cards另一会话11个已修改文件原样保留、不提交/不stash/不还原；既有guard_a修改及其他无关资源保留。未打开结城理mod图片，未读obs.json或密码，未启动游戏，不rebase/reset/强推。推送后用ls-remote确认origin收到提交。
